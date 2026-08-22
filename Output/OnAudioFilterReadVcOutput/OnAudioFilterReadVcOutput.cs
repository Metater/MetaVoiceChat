//#define LOG_OnAudioFilterReadVcOutput

using MetaVoiceChat.NetEq;
using MetaVoiceChat.Output;
using MetaVoiceChat.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;

namespace MetaVoiceChat.Output.OnAudioFilterReadVcOutput
{
    [RequireComponent(typeof(UnityEngine.AudioSource))]
    public sealed class OnAudioFilterReadVcOutput : VcAudioOutput
    {
        public float Gain { get; set; } = 1f;

        // NetEQ constants
        public const int DefaultMaxPacketsInBuffer = 50;
        public const int DefaultAdditionalDelayMs = 0;
        public const OnAudioFilterReadVcConfig.JitterBufferMode DefaultJitterBufferMode = OnAudioFilterReadVcConfig.JitterBufferMode.Balanced;
        // Speex resampler constants
        public const int DefaultResamplerQuality = 4;
        public const int DefaultResamplerBufferMs = 10;
        // Other constants
        private const int PendingFramePoolCapacity = 32;
        private const int MaxPacketDurationMs = 500;
        private const int MaxPendingFrameAgeMs = 60;
        private const int MaxPacketsDrainedPerCallback = 32;
        private const int MaxNetEqReadsPerCallback = 32;

        [SerializeField] private OnAudioFilterReadVcConfig audioFilterReadConfig;
        private const bool createPlaybackClip = true;

#if LOG_OnAudioFilterReadVcOutput
        [Header("Temporary Diagnostics")]
        [SerializeField] private bool logDiagnostics = true;
        [SerializeField, Min(0.1f)] private float diagnosticsLogIntervalSeconds = 1f;
#endif

        private UnityEngine.AudioSource audioSource;
        private AudioClip playbackClip;
        private float[] playbackClipSamples = Array.Empty<float>();

        private readonly ConcurrentQueue<PendingFrame> pendingFrames = new ConcurrentQueue<PendingFrame>();
        private readonly ConcurrentQueue<PendingFrame> availableFrames = new ConcurrentQueue<PendingFrame>();
        private readonly object framePoolInitializationLock = new object();
        private int framePoolInitialized;

        private int acceptingFrames;
        private int outputActive;
        private int recreatePlaybackClipRequested;
        private int resetAudioThreadStateRequested;
        private int resetOutputConversionRequested;
        private int cachedOutputSampleRate;
        private int cachedOutputChannels = 2;
        private int cachedDspBufferLength;
        private int cachedDspBufferCount;
        private int cachedDspBufferMs;
        private int cachedResamplerQuality = DefaultResamplerQuality;
        private int cachedResamplerBufferMs = DefaultResamplerBufferMs;
        private NetEqSettingsSnapshot cachedNetEqSettings = new NetEqSettingsSnapshot(
            DefaultMaxPacketsInBuffer,
            DefaultAdditionalDelayMs,
            DefaultJitterBufferMode,
            0,
            0);

        private int currentBufferSizeMs;
        private int receiveToInsertLatencyMs;
        private int localOutputBufferMs;

        private IntPtr netEqPtr = IntPtr.Zero;
        private readonly OneWayResampler resampler = new OneWayResampler();
        private readonly SampleFifo outputFifo = new SampleFifo();

        private float[] netEqReadBuffer = Array.Empty<float>();
        private float[] resamplerInputBuffer = Array.Empty<float>();
        private float[] resamplerOutputBuffer = Array.Empty<float>();
        private float[] channelConvertBuffer = Array.Empty<float>();
        private float[] silencePacketBuffer = Array.Empty<float>();
        private float[] spatializationInputBuffer = Array.Empty<float>();
        private int netEqSampleRate;
        private int netEqChannels;
        private NetEqConfig audioThreadNetEqConfig;
        private int audioThreadOutputSampleRate;
        private int audioThreadOutputChannels;

#if LOG_OnAudioFilterReadVcOutput
        private int receivedFrameCount;
        private int acceptedFrameCount;
        private int droppedInvalidFrameCount;
        private int droppedFullFrameCount;
        private int audioCallbackCount;
        private int drainedFrameCount;
        private int droppedStaleFrameCount;
        private int netEqCreateCount;
        private int getAudioCallCount;
        private int getAudioSampleCount;
        private int lastAudioDataLength;
        private int lastAudioChannels;
        private int lastGetAudioSamples;
        private int lastOutputPeakPpm;
        private int audioThreadExceptionCount;
        private string lastAudioThreadException;
        private double nextDiagnosticsLogTime;
#endif

        public int CurrentBufferSizeMs
        {
            get { return Volatile.Read(ref currentBufferSizeMs); }
        }

        public int GetEstimatedLocalReceiveToDspLatencyMs()
        {
            int pendingMs = GetOldestPendingFrameAgeMs();
            int ingressMs = Math.Max(pendingMs, Volatile.Read(ref receiveToInsertLatencyMs));

            return ingressMs +
                Volatile.Read(ref currentBufferSizeMs) +
                Volatile.Read(ref localOutputBufferMs) +
                Volatile.Read(ref cachedDspBufferMs);
        }

        public void Process(
            Span<float> frame,
            int frameSize,
            int frequency,
            int channels,
            ushort sequenceNumber,
            uint timestamp)
        {
#if LOG_OnAudioFilterReadVcOutput
            Interlocked.Increment(ref receivedFrameCount);
#endif

            if (Volatile.Read(ref acceptingFrames) == 0 ||
                !IsValidFrameShape(frameSize, frequency, channels))
            {
#if LOG_OnAudioFilterReadVcOutput
                Interlocked.Increment(ref droppedInvalidFrameCount);
#endif

                //UnityEngine.Debug.Log("Invalid frame shape or not accepting frames. FrameSize: " + frameSize + ", Frequency: " + frequency + ", Channels: " + channels, this);
                return;
            }

            bool isSilence = frame.IsEmpty;
            if (!isSilence && frame.Length < frameSize)
            {
#if LOG_OnAudioFilterReadVcOutput
                Interlocked.Increment(ref droppedInvalidFrameCount);
#endif
                return;
            }

            InitializePendingFramePool();
            if (!availableFrames.TryDequeue(out PendingFrame pendingFrame))
            {
#if LOG_OnAudioFilterReadVcOutput
                Interlocked.Increment(ref droppedFullFrameCount);
#endif
                return;
            }

            bool published = false;
            try
            {
                if (!isSilence)
                {
                    pendingFrame.EnsureCapacity(frameSize);
                    frame.Slice(0, frameSize).CopyTo(pendingFrame.Samples);
                    ApplyGain(pendingFrame.Samples, frameSize);
                }

                pendingFrame.SampleLength = frameSize;
                pendingFrame.SampleRate = frequency;
                pendingFrame.Channels = channels;
                pendingFrame.SequenceNumber = sequenceNumber;
                pendingFrame.Timestamp = timestamp;
                pendingFrame.ReceivedTimestamp = Stopwatch.GetTimestamp();
                pendingFrame.IsSilence = isSilence;

                pendingFrame.NetEqConfig = GetCachedNetEqConfig(frameSize / channels, frequency);

                if (Volatile.Read(ref acceptingFrames) == 0)
                {
                    return;
                }

                pendingFrames.Enqueue(pendingFrame);
                published = true;
#if LOG_OnAudioFilterReadVcOutput
                Interlocked.Increment(ref acceptedFrameCount);
#endif
            }
            finally
            {
                if (!published)
                {
                    availableFrames.Enqueue(pendingFrame);
                }
            }
        }

        private void Awake()
        {
            InitializePendingFramePool();
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            CacheMainThreadSettings();
            InitializePendingFramePool();
            ClearPendingFrames();

            if (!TryGetComponent(out audioSource))
            {
                Volatile.Write(ref acceptingFrames, 0);
                Volatile.Write(ref outputActive, 0);
                return;
            }

            ConfigureAudioSource(audioSource);

            Volatile.Write(ref resetAudioThreadStateRequested, 1);
            Volatile.Write(ref resetOutputConversionRequested, 1);
            Volatile.Write(ref outputActive, 1);
            Volatile.Write(ref acceptingFrames, 1);

            if (createPlaybackClip)
            {
                CreatePlaybackClip();
            }

            audioSource.Play();
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            Volatile.Write(ref acceptingFrames, 0);
            Volatile.Write(ref outputActive, 0);
            Volatile.Write(ref resetAudioThreadStateRequested, 1);

            if (audioSource != null)
            {
                audioSource.Stop();
                if (createPlaybackClip && audioSource.clip == playbackClip)
                {
                    audioSource.clip = null;
                }
            }

            if (playbackClip != null)
            {
                Destroy(playbackClip);
                playbackClip = null;
            }
        }

        private void OnDestroy()
        {
            Volatile.Write(ref acceptingFrames, 0);
            Volatile.Write(ref outputActive, 0);
            Volatile.Write(ref resetAudioThreadStateRequested, 1);
        }

        private void Update()
        {
            CacheMainThreadSettings();

            if (Volatile.Read(ref outputActive) != 0 && audioSource != null)
            {
                ConfigureAudioSource(audioSource);
                if (createPlaybackClip && Interlocked.Exchange(ref recreatePlaybackClipRequested, 0) != 0)
                {
                    CreatePlaybackClip(forceRecreate: true);
                }

                RestartPlaybackIfNeeded();
            }

#if LOG_OnAudioFilterReadVcOutput
            LogDiagnosticsIfNeeded();
#endif
        }

        private void OnValidate()
        {
            if (TryGetComponent(out UnityEngine.AudioSource source))
            {
                ConfigureAudioSource(source);
            }
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            CacheMainThreadSettings();
            Volatile.Write(ref resetOutputConversionRequested, 1);

            if (audioSource != null && createPlaybackClip)
            {
                CreatePlaybackClip(forceRecreate: true);
            }

            if (Volatile.Read(ref outputActive) != 0 && audioSource != null)
            {
                RestartPlaybackIfNeeded();
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null)
            {
                return;
            }

#if LOG_OnAudioFilterReadVcOutput
            Interlocked.Increment(ref audioCallbackCount);
            Volatile.Write(ref lastAudioDataLength, data.Length);
            Volatile.Write(ref lastAudioChannels, channels);
#endif

            try
            {
                int outputSampleRate = Volatile.Read(ref cachedOutputSampleRate);
                if (outputSampleRate <= 0 || channels <= 0)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                if (Volatile.Read(ref outputActive) == 0)
                {
                    ClearPendingFrames();
                    DisposeAudioThreadState();
                    Volatile.Write(ref resetAudioThreadStateRequested, 0);
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                if (Interlocked.Exchange(ref resetAudioThreadStateRequested, 0) != 0)
                {
                    DisposeAudioThreadState();
                }

                if (Interlocked.Exchange(ref resetOutputConversionRequested, 0) != 0 ||
                    audioThreadOutputSampleRate != outputSampleRate ||
                    audioThreadOutputChannels != channels)
                {
                    ResetOutputConversionState();
                }

                audioThreadOutputSampleRate = outputSampleRate;
                audioThreadOutputChannels = channels;

                float[] spatializationBuffer = EnsureAudioBuffer(ref spatializationInputBuffer, data.Length, clearNewBuffer: false);
                Array.Copy(data, 0, spatializationBuffer, 0, data.Length);

                DrainPendingFramesAudioThread();

                if (netEqPtr == IntPtr.Zero)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                FillOutputFifo(data.Length, outputSampleRate, channels);

                int copied = outputFifo.Read(data, 0, data.Length);
                Assert.AreEqual(copied, data.Length, "Expected data read from output FIFO to match DSP buffer size.");
                if (copied < data.Length)
                {
                    Array.Clear(data, copied, data.Length - copied);
                }

                ApplySpatializationMask(data, spatializationBuffer, data.Length);
#if LOG_OnAudioFilterReadVcOutput
                CacheOutputPeak(data);
#endif
                CacheAudioThreadLatency();
            }
            catch (Exception exception)
            {
#if LOG_OnAudioFilterReadVcOutput
                Interlocked.Increment(ref audioThreadExceptionCount);
                lastAudioThreadException = exception.GetType().Name + ": " + exception.Message;
#else
                _ = exception;
#endif
                DisposeAudioThreadState();
                Array.Clear(data, 0, data.Length);
            }
        }

        private void DrainPendingFramesAudioThread()
        {
            int processedThisCallback = 0;
            long now = Stopwatch.GetTimestamp();
            while (processedThisCallback < MaxPacketsDrainedPerCallback &&
                pendingFrames.TryDequeue(out PendingFrame frame))
            {
                processedThisCallback++;

                try
                {
                    if (TimestampAgeMs(frame.ReceivedTimestamp, now) > MaxPendingFrameAgeMs)
                    {
#if LOG_OnAudioFilterReadVcOutput
                        Interlocked.Increment(ref droppedStaleFrameCount);
#endif
                        continue;
                    }

                    if (!EnsureNetEqFor(frame.SampleRate, frame.Channels, frame.NetEqConfig))
                    {
                        continue;
                    }

                    float[] packetSamples;
                    if (frame.IsSilence)
                    {
                        packetSamples = EnsureAudioBuffer(ref silencePacketBuffer, frame.SampleLength, clearNewBuffer: false);
                        Array.Clear(packetSamples, 0, frame.SampleLength);
                    }
                    else
                    {
                        packetSamples = frame.Samples;
                    }

                    NetEqInterop.InsertPacket(
                        netEqPtr,
                        frame.SequenceNumber,
                        frame.Timestamp,
                        packetSamples,
                        frame.SampleLength,
                        (uint)frame.SampleRate,
                        (byte)frame.Channels,
                        frame.NetEqConfig.PacketDurationMs);

                    Volatile.Write(
                        ref receiveToInsertLatencyMs,
                        TimestampAgeMs(frame.ReceivedTimestamp, Stopwatch.GetTimestamp()));

#if LOG_OnAudioFilterReadVcOutput
                    Interlocked.Increment(ref drainedFrameCount);
#endif
                }
                finally
                {
                    availableFrames.Enqueue(frame);
                }
            }

            if (netEqPtr != IntPtr.Zero)
            {
                Volatile.Write(ref currentBufferSizeMs, (int)NetEqInterop.CurrentBufferSizeMs(netEqPtr));
            }
        }

        private bool EnsureNetEqFor(int sampleRate, int channels, NetEqConfig config)
        {
            if (sampleRate <= 0 || channels <= 0 || channels > 2)
            {
                return false;
            }

            if (netEqPtr != IntPtr.Zero &&
                netEqSampleRate == sampleRate &&
                netEqChannels == channels &&
                audioThreadNetEqConfig.Equals(config))
            {
                return true;
            }

            DisposeAudioThreadState();

            netEqPtr = NetEqInterop.CreateNetEq(
                (uint)sampleRate,
                (byte)channels,
                config.MaxPacketsInBuffer,
                (uint)config.MaxDelayMs,
                (uint)config.MinDelayMs,
                (uint)config.AdditionalDelayMs);

            netEqSampleRate = sampleRate;
            netEqChannels = channels;
            audioThreadNetEqConfig = config;
#if LOG_OnAudioFilterReadVcOutput
            Interlocked.Increment(ref netEqCreateCount);
#endif
            return true;
        }

        private void FillOutputFifo(int requiredSamples, int outputSampleRate, int outputChannels)
        {
            if (requiredSamples <= 0 || outputSampleRate <= 0 || outputChannels <= 0)
            {
                return;
            }

            int safety = 0;
            while (outputFifo.Count < requiredSamples && safety++ < MaxNetEqReadsPerCallback)
            {
                int samplesPerChannel = TenMsSamplesPerChannel(netEqSampleRate);
                int readLength = samplesPerChannel * netEqChannels;
                float[] readBuffer = EnsureAudioBuffer(ref netEqReadBuffer, readLength, clearNewBuffer: false);
                int readSamples = netEq.GetAudio(readBuffer, readLength);
#if LOG_OnAudioFilterReadVcOutput
                Interlocked.Increment(ref getAudioCallCount);
                Interlocked.Add(ref getAudioSampleCount, readSamples);
                Volatile.Write(ref lastGetAudioSamples, readSamples);
#endif

                if (readSamples <= 0)
                {
                    break;
                }

                AppendToOutputFifo(readBuffer, readSamples, outputSampleRate, outputChannels);
            }

            CacheAudioThreadLatency();
        }

        private void AppendToOutputFifo(float[] input, int inputSampleCount, int outputSampleRate, int outputChannels)
        {
            float[] samples = input;
            int sampleCount = inputSampleCount;
            int channels = netEqChannels;

            if (netEqSampleRate != outputSampleRate)
            {
                if (netEqSampleRate <= 0 || outputSampleRate <= 0)
                {
                    return;
                }

                int inputFrames = inputSampleCount / netEqChannels;
                int outputFrames = Math.Max(
                    1,
                    (int)Math.Ceiling(inputFrames * (double)outputSampleRate / netEqSampleRate) + 8);
                int outputSampleCapacity = outputFrames * netEqChannels;

                float[] resampleInput = EnsureAudioBuffer(ref resamplerInputBuffer, inputSampleCount, clearNewBuffer: false);
                Array.Copy(input, 0, resampleInput, 0, inputSampleCount);

                float[] resampleOutput = EnsureAudioBuffer(ref resamplerOutputBuffer, outputSampleCapacity, clearNewBuffer: false);
                int inLen = inputFrames;
                int outLen = outputFrames;

                resampler.Configure(
                    netEqChannels,
                    netEqSampleRate,
                    outputSampleRate,
                    Volatile.Read(ref cachedResamplerQuality));
                resampler.ProcessInterleaved(
                    resampleInput.AsSpan(0, inputSampleCount),
                    ref inLen,
                    resampleOutput.AsSpan(0, outputSampleCapacity),
                    ref outLen);

                if (inLen != inputFrames)
                {
                    return;
                }

                samples = resampleOutput;
                sampleCount = outLen * netEqChannels;
            }

            if (channels == outputChannels)
            {
                outputFifo.Write(samples, 0, sampleCount);
                return;
            }

            int frameCount = sampleCount / channels;
            int convertedSampleCount = frameCount * outputChannels;
            float[] converted = EnsureAudioBuffer(ref channelConvertBuffer, convertedSampleCount, clearNewBuffer: false);

            if (channels == 1)
            {
                for (int inIndex = 0, outIndex = 0; inIndex < frameCount; inIndex++)
                {
                    float sample = samples[inIndex];
                    for (int outputChannel = 0; outputChannel < outputChannels; outputChannel++, outIndex++)
                    {
                        converted[outIndex] = sample;
                    }
                }
            }
            else if (channels == 2 && outputChannels == 1)
            {
                for (int inIndex = 0, outIndex = 0; outIndex < frameCount; inIndex += 2, outIndex++)
                {
                    converted[outIndex] = (samples[inIndex] + samples[inIndex + 1]) * 0.5f;
                }
            }
            else if (channels == 2)
            {
                for (int inIndex = 0, outIndex = 0; inIndex < sampleCount; inIndex += 2)
                {
                    converted[outIndex++] = samples[inIndex];
                    if (outputChannels > 1)
                    {
                        converted[outIndex++] = samples[inIndex + 1];
                    }

                    for (int outputChannel = 2; outputChannel < outputChannels; outputChannel++, outIndex++)
                    {
                        converted[outIndex] = (samples[inIndex] + samples[inIndex + 1]) * 0.5f;
                    }
                }
            }
            else
            {
                Array.Clear(converted, 0, convertedSampleCount);
            }

            outputFifo.Write(converted, 0, convertedSampleCount);
        }

        private static void ApplySpatializationMask(float[] output, float[] spatializationMask, int length)
        {
            int count = Math.Min(length, Math.Min(output.Length, spatializationMask.Length));
            for (int i = 0; i < count; i++)
            {
                output[i] *= spatializationMask[i];
            }

            if (count < length)
            {
                Array.Clear(output, count, length - count);
            }
        }

        private void DisposeAudioThreadState()
        {
            if (netEqPtr != IntPtr.Zero)
            {
                NetEqInterop.FreeNetEq(netEqPtr);
                netEqPtr = IntPtr.Zero;
            }

            resampler.Free();
            outputFifo.Clear();
            netEqSampleRate = 0;
            netEqChannels = 0;
            audioThreadNetEqConfig = default;
            audioThreadOutputSampleRate = 0;
            audioThreadOutputChannels = 0;

            Volatile.Write(ref currentBufferSizeMs, 0);
            Volatile.Write(ref receiveToInsertLatencyMs, 0);
            Volatile.Write(ref localOutputBufferMs, 0);
        }

        private void ResetOutputConversionState()
        {
            resampler.Free();
            outputFifo.Clear();
            Volatile.Write(ref localOutputBufferMs, 0);
        }

        private void CacheAudioThreadLatency()
        {
            int sampleRate = audioThreadOutputSampleRate;
            int channels = audioThreadOutputChannels;
            int bufferedMs = 0;

            if (sampleRate > 0 && channels > 0)
            {
                bufferedMs = outputFifo.Count / channels * 1000 / sampleRate;
            }

            Volatile.Write(ref localOutputBufferMs, bufferedMs);
            if (netEqPtr != IntPtr.Zero)
            {
                Volatile.Write(ref currentBufferSizeMs, (int)NetEqInterop.CurrentBufferSizeMs(netEqPtr));
            }
        }

#if LOG_OnAudioFilterReadVcOutput
        private void CacheOutputPeak(float[] data)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float value = Math.Abs(data[i]);
                if (value > peak)
                {
                    peak = value;
                }
            }

            Volatile.Write(ref lastOutputPeakPpm, (int)Math.Round(peak * 1000000f));
        }
#endif

#if LOG_OnAudioFilterReadVcOutput
        private void LogDiagnosticsIfNeeded()
        {
            if (!logDiagnostics || Time.unscaledTimeAsDouble < nextDiagnosticsLogTime)
            {
                return;
            }

            nextDiagnosticsLogTime = Time.unscaledTimeAsDouble + Math.Max(0.1f, diagnosticsLogIntervalSeconds);

            string exception = lastAudioThreadException;
            if (string.IsNullOrEmpty(exception))
            {
                exception = "none";
            }

            UnityEngine.Debug.Log(
                $"{nameof(OnAudioFilterReadVcOutput)} diagnostics " +
                $"active={Volatile.Read(ref outputActive)} accepting={Volatile.Read(ref acceptingFrames)} " +
                $"source={(audioSource != null ? "yes" : "no")} playing={(audioSource != null && audioSource.isPlaying)} " +
                $"clip={(audioSource != null && audioSource.clip != null ? audioSource.clip.name : "none")} " +
                $"dspRate={Volatile.Read(ref cachedOutputSampleRate)} callbacks={Volatile.Read(ref audioCallbackCount)} " +
                $"audioLen={Volatile.Read(ref lastAudioDataLength)} audioCh={Volatile.Read(ref lastAudioChannels)} " +
                $"received={Volatile.Read(ref receivedFrameCount)} accepted={Volatile.Read(ref acceptedFrameCount)} " +
                $"pending={pendingFrames.Count} available={availableFrames.Count} drained={Volatile.Read(ref drainedFrameCount)} " +
                $"dropInvalid={Volatile.Read(ref droppedInvalidFrameCount)} dropFull={Volatile.Read(ref droppedFullFrameCount)} " +
                $"dropStale={Volatile.Read(ref droppedStaleFrameCount)} " +
                $"netEq={(netEq != null ? "yes" : "no")} netEqCreates={Volatile.Read(ref netEqCreateCount)} " +
                $"netEqRate={netEqSampleRate} netEqCh={netEqChannels} currentBufferMs={Volatile.Read(ref currentBufferSizeMs)} " +
                $"getAudioCalls={Volatile.Read(ref getAudioCallCount)} getAudioSamples={Volatile.Read(ref getAudioSampleCount)} " +
                $"lastGetAudio={Volatile.Read(ref lastGetAudioSamples)} localOutMs={Volatile.Read(ref localOutputBufferMs)} " +
                $"peak={Volatile.Read(ref lastOutputPeakPpm) / 1000000f:0.000000} " +
                $"exceptions={Volatile.Read(ref audioThreadExceptionCount)} lastException={exception}",
                this);
        }
#endif

        private void InitializePendingFramePool()
        {
            if (Volatile.Read(ref framePoolInitialized) != 0)
            {
                return;
            }

            lock (framePoolInitializationLock)
            {
                if (Volatile.Read(ref framePoolInitialized) != 0)
                {
                    return;
                }

                for (int i = 0; i < PendingFramePoolCapacity; i++)
                {
                    availableFrames.Enqueue(new PendingFrame());
                }

                Volatile.Write(ref framePoolInitialized, 1);
            }
        }

        private void ClearPendingFrames()
        {
            while (pendingFrames.TryDequeue(out PendingFrame frame))
            {
                availableFrames.Enqueue(frame);
            }
        }

        private int GetOldestPendingFrameAgeMs()
        {
            if (!pendingFrames.TryPeek(out PendingFrame frame))
            {
                return 0;
            }

            return TimestampAgeMs(frame.ReceivedTimestamp, Stopwatch.GetTimestamp());
        }

        private void CacheMainThreadSettings()
        {
            int previousOutputSampleRate = Volatile.Read(ref cachedOutputSampleRate);
            int previousOutputChannels = Volatile.Read(ref cachedOutputChannels);
            int previousDspBufferLength = Volatile.Read(ref cachedDspBufferLength);
            int previousDspBufferCount = Volatile.Read(ref cachedDspBufferCount);

            int outputSampleRate = AudioSettings.outputSampleRate;
            Volatile.Write(ref cachedOutputSampleRate, outputSampleRate);
            Volatile.Write(ref cachedOutputChannels, GetSpeakerModeChannelCount(AudioSettings.speakerMode));

            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
            Volatile.Write(ref cachedDspBufferLength, bufferLength);
            Volatile.Write(ref cachedDspBufferCount, numBuffers);
            int dspMs = outputSampleRate > 0 ? bufferLength * numBuffers * 1000 / outputSampleRate : 0;
            Volatile.Write(ref cachedDspBufferMs, dspMs);

            // Avoid treating the initial cache population as an audio configuration change.
            if (previousOutputSampleRate != 0 &&
                (previousOutputSampleRate != outputSampleRate ||
                    previousOutputChannels != Volatile.Read(ref cachedOutputChannels) ||
                    previousDspBufferLength != bufferLength ||
                    previousDspBufferCount != numBuffers))
            {
                Volatile.Write(ref resetOutputConversionRequested, 1);
                Volatile.Write(ref recreatePlaybackClipRequested, 1);
            }

            OnAudioFilterReadVcConfig config = audioFilterReadConfig;
            CacheNetEqSettings(config);
            Volatile.Write(ref cachedResamplerQuality, Math.Clamp(config != null ? config.resamplerQuality : DefaultResamplerQuality, 0, 10));
            Volatile.Write(ref cachedResamplerBufferMs, Math.Clamp(config != null ? config.ResamplerBufferMs : DefaultResamplerBufferMs, 10, 100));
        }

        private void CacheNetEqSettings(OnAudioFilterReadVcConfig config)
        {
            int maxPacketsInBuffer = Math.Max(1, config != null ? config.maxPacketsInBuffer : DefaultMaxPacketsInBuffer);
            int additionalDelayMs = Math.Max(0, config != null ? config.additionalDelayMs : DefaultAdditionalDelayMs);
            OnAudioFilterReadVcConfig.JitterBufferMode jitterBufferMode = config != null
                ? config.jitterBufferMode
                : DefaultJitterBufferMode;
            int customMinDelayMs = Math.Max(0, config != null ? config.customMinDelayMs : 0);
            int customMaxDelayMs = Math.Max(0, config != null ? config.customMaxDelayMs : 0);

            NetEqSettingsSnapshot currentSettings = Volatile.Read(ref cachedNetEqSettings);
            if (currentSettings.Matches(
                    maxPacketsInBuffer,
                    additionalDelayMs,
                    jitterBufferMode,
                    customMinDelayMs,
                    customMaxDelayMs))
            {
                return;
            }

            NetEqSettingsSnapshot settings = new NetEqSettingsSnapshot(
                maxPacketsInBuffer,
                additionalDelayMs,
                jitterBufferMode,
                customMinDelayMs,
                customMaxDelayMs);
            Volatile.Write(ref cachedNetEqSettings, settings);
        }

        private NetEqConfig GetCachedNetEqConfig(int samplesPerChannel, int sampleRate)
        {
            NetEqSettingsSnapshot settings = Volatile.Read(ref cachedNetEqSettings);

            int packetDurationMs = Math.Max(1, (int)Math.Round(samplesPerChannel * 1000.0 / sampleRate));
            int minDelayMs;
            int maxDelayMs;

            if (settings.JitterBufferMode == OnAudioFilterReadVcConfig.JitterBufferMode.Custom)
            {
                minDelayMs = settings.CustomMinDelayMs;
                maxDelayMs = settings.CustomMaxDelayMs;
            }
            else
            {
                minDelayMs = OnAudioFilterReadVcConfig.GetMinDelayMs(packetDurationMs, settings.JitterBufferMode);
                maxDelayMs = OnAudioFilterReadVcConfig.GetMaxDelayMs(packetDurationMs, settings.JitterBufferMode);
            }

            minDelayMs = Math.Max(0, minDelayMs);
            maxDelayMs = Math.Max(minDelayMs, maxDelayMs);

            return new NetEqConfig(
                packetDurationMs,
                settings.MaxPacketsInBuffer,
                maxDelayMs,
                minDelayMs,
                settings.AdditionalDelayMs);
        }

        private void CreatePlaybackClip(bool forceRecreate = false)
        {
            int sampleRate = Math.Max(1, Volatile.Read(ref cachedOutputSampleRate));
            int channels = Math.Max(1, Volatile.Read(ref cachedOutputChannels));
            if (!forceRecreate &&
                playbackClip != null &&
                playbackClip.frequency == sampleRate &&
                playbackClip.channels == channels)
            {
                audioSource.clip = playbackClip;
                return;
            }

            bool shouldResume = Volatile.Read(ref outputActive) != 0 &&
                audioSource != null &&
                audioSource.isPlaying;

            if (shouldResume)
            {
                audioSource.Stop();
            }

            if (playbackClip != null)
            {
                Destroy(playbackClip);
            }

            playbackClip = AudioClip.Create(
                nameof(OnAudioFilterReadVcOutput),
                sampleRate,
                channels,
                sampleRate,
                false);
            FillPlaybackClip(playbackClip);
            audioSource.clip = playbackClip;

            if (shouldResume)
            {
                audioSource.Play();
            }
        }

        private void FillPlaybackClip(AudioClip clip)
        {
            int sampleCount = clip.samples * clip.channels;
            if (playbackClipSamples.Length != sampleCount)
            {
                playbackClipSamples = new float[sampleCount];
            }

            for (int i = 0; i < playbackClipSamples.Length; i++)
            {
                playbackClipSamples[i] = 1f;
            }

#if LOG_OnAudioFilterReadVcOutput
            if (!clip.SetData(playbackClipSamples, 0))
            {
                UnityEngine.Debug.LogWarning(
                    $"{nameof(OnAudioFilterReadVcOutput)} failed to initialize the generated playback clip.",
                    this);
            }
#else
            _ = clip.SetData(playbackClipSamples, 0);
#endif
        }

        private static void ConfigureAudioSource(UnityEngine.AudioSource source)
        {
            source.loop = true;
            source.priority = 0;
            source.dopplerLevel = 0f;
            source.spatializePostEffects = false;
        }

        private void RestartPlaybackIfNeeded()
        {
            if (audioSource == null || audioSource.isPlaying)
            {
                return;
            }

            if (createPlaybackClip && audioSource.clip == null)
            {
                CreatePlaybackClip();
            }

            audioSource.Play();
        }

        private static int GetSpeakerModeChannelCount(AudioSpeakerMode speakerMode)
        {
            switch (speakerMode)
            {
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Stereo:
                    return 2;
                case AudioSpeakerMode.Quad:
                    return 4;
                case AudioSpeakerMode.Surround:
                    return 5;
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                case AudioSpeakerMode.Prologic:
                    return 2;
                default:
                    return 2;
            }
        }

        private static bool IsValidFrameShape(int frameSize, int inputSampleRate, int inputChannels)
        {
            if (frameSize <= 0 ||
                inputSampleRate <= 0 ||
                (inputChannels != 1 && inputChannels != 2) ||
                frameSize % inputChannels != 0)
            {
                return false;
            }

            int samplesPerChannel = frameSize / inputChannels;
            int durationMs = samplesPerChannel * 1000 / inputSampleRate;
            int maxSamples = inputSampleRate * MaxPacketDurationMs / 1000 * inputChannels;

            return frameSize <= maxSamples &&
                samplesPerChannel * 1000 == durationMs * inputSampleRate &&
                durationMs > 0 &&
                durationMs <= MaxPacketDurationMs;
        }

        private void ApplyGain(float[] samples, int sampleCount)
        {
            float gain = Mathf.Clamp(Gain, 0f, 3f);
            if (gain <= 0f)
            {
                Array.Clear(samples, 0, sampleCount);
                return;
            }

            float tanhGain = (float)Math.Tanh(gain);
            for (int i = 0; i < sampleCount; i++)
            {
                const float bias = 0.01f;
                float x = (samples[i] + bias) * gain;
                samples[i] = (float)Math.Tanh(x) / tanhGain - bias;
            }
        }

        private static int TenMsSamplesPerChannel(int sampleRate)
        {
            return Math.Max(1, (int)Math.Round(sampleRate * 0.01));
        }

        private static float[] EnsureAudioBuffer(ref float[] buffer, int requiredLength, bool clearNewBuffer)
        {
            if (buffer.Length >= requiredLength)
            {
                return buffer;
            }

            buffer = new float[requiredLength];
            if (clearNewBuffer)
            {
                Array.Clear(buffer, 0, buffer.Length);
            }

            return buffer;
        }

        private static int TimestampAgeMs(long timestamp, long now)
        {
            if (timestamp <= 0 || now <= timestamp)
            {
                return 0;
            }

            double ms = (now - timestamp) * 1000.0 / Stopwatch.Frequency;
            return ms >= int.MaxValue ? int.MaxValue : (int)Math.Round(ms);
        }

        private static int NextPowerOfTwo(int value)
        {
            value = Math.Max(1, value);
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        protected override void ReceiveFrame(int index, float[] samples, float targetLatency)
        {
            throw new NotImplementedException();
        }

        private sealed class PendingFrame
        {
            public float[] Samples = Array.Empty<float>();
            public int SampleLength;
            public int SampleRate;
            public int Channels;
            public ushort SequenceNumber;
            public uint Timestamp;
            public long ReceivedTimestamp;
            public bool IsSilence;
            public NetEqConfig NetEqConfig;

            public void EnsureCapacity(int requiredSamples)
            {
                if (Samples.Length < requiredSamples)
                {
                    Samples = new float[requiredSamples];
                }
            }
        }

        private sealed class NetEqSettingsSnapshot
        {
            public readonly int MaxPacketsInBuffer;
            public readonly int AdditionalDelayMs;
            public readonly OnAudioFilterReadVcConfig.JitterBufferMode JitterBufferMode;
            public readonly int CustomMinDelayMs;
            public readonly int CustomMaxDelayMs;

            public NetEqSettingsSnapshot(
                int maxPacketsInBuffer,
                int additionalDelayMs,
                OnAudioFilterReadVcConfig.JitterBufferMode jitterBufferMode,
                int customMinDelayMs,
                int customMaxDelayMs)
            {
                MaxPacketsInBuffer = maxPacketsInBuffer;
                AdditionalDelayMs = additionalDelayMs;
                JitterBufferMode = jitterBufferMode;
                CustomMinDelayMs = customMinDelayMs;
                CustomMaxDelayMs = customMaxDelayMs;
            }

            public bool Matches(
                int maxPacketsInBuffer,
                int additionalDelayMs,
                OnAudioFilterReadVcConfig.JitterBufferMode jitterBufferMode,
                int customMinDelayMs,
                int customMaxDelayMs)
            {
                return MaxPacketsInBuffer == maxPacketsInBuffer &&
                    AdditionalDelayMs == additionalDelayMs &&
                    JitterBufferMode == jitterBufferMode &&
                    CustomMinDelayMs == customMinDelayMs &&
                    CustomMaxDelayMs == customMaxDelayMs;
            }
        }

        private readonly struct NetEqConfig : IEquatable<NetEqConfig>
        {
            public readonly int PacketDurationMs;
            public readonly int MaxPacketsInBuffer;
            public readonly int MaxDelayMs;
            public readonly int MinDelayMs;
            public readonly int AdditionalDelayMs;

            public NetEqConfig(
                int packetDurationMs,
                int maxPacketsInBuffer,
                int maxDelayMs,
                int minDelayMs,
                int additionalDelayMs)
            {
                PacketDurationMs = packetDurationMs;
                MaxPacketsInBuffer = maxPacketsInBuffer;
                MaxDelayMs = maxDelayMs;
                MinDelayMs = minDelayMs;
                AdditionalDelayMs = additionalDelayMs;
            }

            public bool Equals(NetEqConfig other)
            {
                return PacketDurationMs == other.PacketDurationMs &&
                    MaxPacketsInBuffer == other.MaxPacketsInBuffer &&
                    MaxDelayMs == other.MaxDelayMs &&
                    MinDelayMs == other.MinDelayMs &&
                    AdditionalDelayMs == other.AdditionalDelayMs;
            }

            public override bool Equals(object obj)
            {
                return obj is NetEqConfig other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PacketDurationMs, MaxPacketsInBuffer, MaxDelayMs, MinDelayMs, AdditionalDelayMs);
            }
        }

        private sealed class SampleFifo
        {
            private float[] buffer = Array.Empty<float>();
            private int head;
            private int count;

            public int Count
            {
                get { return count; }
            }

            public void Write(float[] source, int sourceIndex, int length)
            {
                if (length <= 0)
                {
                    return;
                }

                EnsureCapacity(count + length);

                int tail = (head + count) % buffer.Length;
                int first = Math.Min(length, buffer.Length - tail);
                Array.Copy(source, sourceIndex, buffer, tail, first);

                int remaining = length - first;
                if (remaining > 0)
                {
                    Array.Copy(source, sourceIndex + first, buffer, 0, remaining);
                }

                count += length;
            }

            public int Read(float[] destination, int destinationIndex, int length)
            {
                int copied = Math.Min(length, count);
                if (copied <= 0)
                {
                    return 0;
                }

                int first = Math.Min(copied, buffer.Length - head);
                Array.Copy(buffer, head, destination, destinationIndex, first);

                int remaining = copied - first;
                if (remaining > 0)
                {
                    Array.Copy(buffer, 0, destination, destinationIndex + first, remaining);
                }

                head = (head + copied) % buffer.Length;
                count -= copied;

                if (count == 0)
                {
                    head = 0;
                }

                return copied;
            }

            public void Clear()
            {
                head = 0;
                count = 0;
            }

            private void EnsureCapacity(int required)
            {
                if (buffer.Length >= required)
                {
                    return;
                }

                int existingCount = count;
                int newLength = NextPowerOfTwo(Math.Max(256, required));
                float[] newBuffer = new float[newLength];

                if (existingCount > 0)
                {
                    Read(newBuffer, 0, existingCount);
                }

                buffer = newBuffer;
                head = 0;
                count = existingCount;
            }
        }
    }
}
