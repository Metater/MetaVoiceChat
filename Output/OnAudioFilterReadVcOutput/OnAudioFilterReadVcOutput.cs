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
    public sealed partial class OnAudioFilterReadVcOutput : VcAudioOutput
    {
        // NetEQ constants
        public const int DefaultMaxPacketsInBuffer = 50;
        public const int DefaultAdditionalDelayMs = 0;
        public const OnAudioFilterReadVcConfig.JitterBufferMode DefaultJitterBufferMode = OnAudioFilterReadVcConfig.JitterBufferMode.Balanced;
        // Speex resampler constants
        public const int DefaultResamplerQuality = 4;
        public const int DefaultResamplerBufferMs = 10;
        // Other constants
        private const int PendingFramePoolCapacity = 32;
        private const int MaxPendingFrameAgeMs = 60;
        private const int MaxPacketsDrainedPerCallback = 32;
        private const int MaxNetEqReadsPerCallback = 32;
        private const int NetworkInputSampleRate = VcConfig.SamplesPerSecond;

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
        private int unsupportedPlatformErrorLogged;
        private int audioCallbacksBlocked = 1;
        private int audioCallbackDepth;
        private int disposeAudioThreadStateRequested;
        private int activateOutputRequested;
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
        private readonly UnmanagedFloatArray netEqPacketBuffer = new UnmanagedFloatArray();
        private readonly UnmanagedFloatArray netEqReadBuffer = new UnmanagedFloatArray();

        private float[] netEqBatchBuffer = Array.Empty<float>();
        private float[] resamplerOutputBuffer = Array.Empty<float>();
        private float[] channelConvertBuffer = Array.Empty<float>();
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
        private int netEqCreateFailureCount;
        private int netEqFreeCount;
        private int invalidGetAudioResultCount;
        private int cleanupRequestCount;
        private int deferredCleanupCount;
        private int completedCleanupCount;
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

            if (channels != 1)
            {
                throw new NotSupportedException(
                    $"{nameof(OnAudioFilterReadVcOutput)} only supports mono network audio. Received {channels} channels.");
            }

            if (frequency != NetworkInputSampleRate)
            {
                throw new NotSupportedException(
                    $"{nameof(OnAudioFilterReadVcOutput)} only supports {NetworkInputSampleRate} Hz network audio. Received {frequency} Hz.");
            }

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
            Volatile.Write(ref acceptingFrames, 0);
            Volatile.Write(ref outputActive, 0);
            Volatile.Write(ref audioCallbacksBlocked, 1);

            if (!IsNativeNetEqSupportedPlatform())
            {
                if (Interlocked.Exchange(ref unsupportedPlatformErrorLogged, 1) == 0)
                {
                    UnityEngine.Debug.LogError(
                        $"{nameof(OnAudioFilterReadVcOutput)} requires the trusted Windows x64 NetEQ plugin. " +
                        "Use VcAudioSourceOutput on unsupported platforms.",
                        this);
                }
                enabled = false;
                return;
            }

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
            Volatile.Write(ref activateOutputRequested, 1);
            TryActivateOutput();
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            Volatile.Write(ref activateOutputRequested, 0);
            RequestAudioThreadStateDisposal();

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

            ClearPendingFrames();
        }

        private void OnDestroy()
        {
            Volatile.Write(ref activateOutputRequested, 0);
            RequestAudioThreadStateDisposal();
        }

        private void Update()
        {
            CacheMainThreadSettings();
            TryCompleteRequestedAudioThreadStateDisposal();
            TryActivateOutput();

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
            Volatile.Write(ref recreatePlaybackClipRequested, 1);
        }

        private void TryActivateOutput()
        {
            if (Volatile.Read(ref activateOutputRequested) == 0 || audioSource == null)
            {
                return;
            }

            TryCompleteRequestedAudioThreadStateDisposal();
            if (Volatile.Read(ref disposeAudioThreadStateRequested) != 0 ||
                Volatile.Read(ref audioCallbackDepth) != 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref activateOutputRequested, 0, 1) != 1)
            {
                return;
            }

            // Reaching activation means the prior audio-thread state is already disposed.
            Volatile.Write(ref resetAudioThreadStateRequested, 0);
            Volatile.Write(ref resetOutputConversionRequested, 1);
            Volatile.Write(ref outputActive, 1);
            Volatile.Write(ref acceptingFrames, 1);

            if (createPlaybackClip)
            {
                CreatePlaybackClip(
                    forceRecreate: Interlocked.Exchange(ref recreatePlaybackClipRequested, 0) != 0);
            }

            Volatile.Write(ref audioCallbacksBlocked, 0);
            audioSource.Play();
        }

        private void RequestAudioThreadStateDisposal()
        {
            Volatile.Write(ref acceptingFrames, 0);
            Volatile.Write(ref audioCallbacksBlocked, 1);
            Volatile.Write(ref outputActive, 0);
            Volatile.Write(ref resetAudioThreadStateRequested, 0);
            Interlocked.Exchange(ref disposeAudioThreadStateRequested, 1);

#if LOG_OnAudioFilterReadVcOutput
            Interlocked.Increment(ref cleanupRequestCount);
            if (Volatile.Read(ref audioCallbackDepth) != 0)
            {
                Interlocked.Increment(ref deferredCleanupCount);
            }
#endif

            TryCompleteRequestedAudioThreadStateDisposal();
        }

        private bool TryCompleteRequestedAudioThreadStateDisposal()
        {
            if (Volatile.Read(ref audioCallbackDepth) != 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref disposeAudioThreadStateRequested, 0, 1) != 1)
            {
                return true;
            }

            DisposeAudioThreadState();
#if LOG_OnAudioFilterReadVcOutput
            Interlocked.Increment(ref completedCleanupCount);
#endif
            return true;
        }

        private bool TryEnterAudioCallback()
        {
            if (Volatile.Read(ref audioCallbacksBlocked) != 0)
            {
                return false;
            }

            Interlocked.Increment(ref audioCallbackDepth);
            if (Volatile.Read(ref audioCallbacksBlocked) == 0)
            {
                return true;
            }

            ExitAudioCallback();
            return false;
        }

        private void ExitAudioCallback()
        {
            int remaining = Interlocked.Decrement(ref audioCallbackDepth);
            Assert.IsTrue(remaining >= 0, "Audio callback depth became negative.");
            if (remaining == 0)
            {
                TryCompleteRequestedAudioThreadStateDisposal();
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null)
            {
                return;
            }

            if (!TryEnterAudioCallback())
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            try
            {
                OnEditorAudioCallbackAdmitted();
                OnAudioFilterReadAdmitted(data, channels);
            }
            finally
            {
                ExitAudioCallback();
            }
        }

        private void OnAudioFilterReadAdmitted(float[] data, int channels)
        {

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

                // Creating or reconfiguring NetEQ disposes the prior audio-thread state,
                // including these conversion fields. Restore the callback's active format
                // before filling/caching the output FIFO.
                audioThreadOutputSampleRate = outputSampleRate;
                audioThreadOutputChannels = channels;

                FillOutputFifo(data.Length, outputSampleRate, channels);

                int copied = outputFifo.Read(data, 0, data.Length);
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
                RecordAudioThreadException(exception);
                DisposeAudioThreadState();
                Array.Clear(data, 0, data.Length);
            }
        }

        private void RecordAudioThreadException(Exception exception)
        {
#if LOG_OnAudioFilterReadVcOutput
            Interlocked.Increment(ref audioThreadExceptionCount);
            lastAudioThreadException = exception.GetType().Name + ": " + exception.Message;
#else
            _ = exception;
#endif
            OnEditorAudioThreadException(exception);
        }

        partial void OnEditorAudioCallbackAdmitted();
        partial void OnEditorAudioThreadException(Exception exception);
        partial void OnEditorAudioThreadStateDisposed();

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

                    IntPtr packetSamples = netEqPacketBuffer.GetOrInit(frame.SampleLength, out _);
                    if (frame.IsSilence)
                    {
                        netEqPacketBuffer.Zero();
                    }
                    else
                    {
                        netEqPacketBuffer.Fill(new ArraySegment<float>(frame.Samples, 0, frame.SampleLength));
                    }

                    NetEqInterop.InsertPacket(
                        netEqPtr,
                        frame.SequenceNumber,
                        frame.Timestamp,
                        packetSamples,
                        frame.SampleLength,
                        (uint)frame.SampleRate,
                        (byte)1,
                        (uint)frame.NetEqConfig.PacketDurationMs);

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
            if (sampleRate != NetworkInputSampleRate || channels != 1)
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

            IntPtr createdNetEq = NetEqInterop.CreateNetEq(
                (uint)sampleRate,
                (byte)1,
                config.MaxPacketsInBuffer,
                (uint)config.MaxDelayMs,
                (uint)config.MinDelayMs,
                (uint)config.AdditionalDelayMs);

            if (createdNetEq == IntPtr.Zero)
            {
#if LOG_OnAudioFilterReadVcOutput
                Interlocked.Increment(ref netEqCreateFailureCount);
#endif
                return false;
            }

            netEqPtr = createdNetEq;
            netEqSampleRate = sampleRate;
            netEqChannels = 1;
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

            int readsThisCallback = 0;
            int batchMs = Math.Clamp(Volatile.Read(ref cachedResamplerBufferMs), 10, 100);
            batchMs = Math.Clamp((batchMs + 5) / 10 * 10, 10, 100);
            int targetBatchSamples = Math.Max(
                TenMsSamplesPerChannel(netEqSampleRate) * netEqChannels,
                netEqSampleRate * batchMs / 1000 * netEqChannels);

            while (outputFifo.Count < requiredSamples && readsThisCallback < MaxNetEqReadsPerCallback)
            {
                float[] batchBuffer = EnsureAudioBuffer(ref netEqBatchBuffer, targetBatchSamples, clearNewBuffer: false);
                int batchSampleCount = 0;

                while (batchSampleCount < targetBatchSamples && readsThisCallback < MaxNetEqReadsPerCallback)
                {
                    int samplesPerChannel = TenMsSamplesPerChannel(netEqSampleRate);
                    int readLength = samplesPerChannel * netEqChannels;
                    IntPtr readSamplesPtr = netEqReadBuffer.GetOrInit(readLength, out float[] readBuffer, initToZero: false);
                    int readSamples = NetEqInterop.GetAudio(netEqPtr, readSamplesPtr, readLength);
                    readsThisCallback++;
#if LOG_OnAudioFilterReadVcOutput
                    Interlocked.Increment(ref getAudioCallCount);
                    Interlocked.Add(ref getAudioSampleCount, Math.Max(0, readSamples));
                    Volatile.Write(ref lastGetAudioSamples, readSamples);
#endif

                    if (readSamples < 0 || readSamples > readLength)
                    {
#if LOG_OnAudioFilterReadVcOutput
                        Interlocked.Increment(ref invalidGetAudioResultCount);
#endif
                        throw new InvalidOperationException(
                            $"NetEQ returned {readSamples} samples for a buffer with capacity {readLength}.");
                    }

                    if (readSamples == 0)
                    {
                        break;
                    }

                    netEqReadBuffer.ReadFromUnmanaged(readSamples);
                    Array.Copy(readBuffer, 0, batchBuffer, batchSampleCount, readSamples);
                    batchSampleCount += readSamples;

                    if (readSamples < readLength)
                    {
                        break;
                    }
                }

                if (batchSampleCount == 0)
                {
                    break;
                }

                AppendToOutputFifo(batchBuffer, batchSampleCount, outputSampleRate, outputChannels);
            }

            CacheAudioThreadLatency();
        }

        private void AppendToOutputFifo(float[] input, int inputSampleCount, int outputSampleRate, int outputChannels)
        {
            float[] samples = input;
            int sampleCount = inputSampleCount;
            int channels = netEqChannels;

            if (channels != 1 || inputSampleCount <= 0)
            {
                return;
            }

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

                float[] resampleOutput = EnsureAudioBuffer(ref resamplerOutputBuffer, outputSampleCapacity, clearNewBuffer: false);
                int inLen = inputFrames;
                int outLen = outputFrames;

                resampler.Configure(
                    netEqChannels,
                    netEqSampleRate,
                    outputSampleRate,
                    Volatile.Read(ref cachedResamplerQuality));
                resampler.ProcessInterleaved(
                    input.AsSpan(0, inputSampleCount),
                    ref inLen,
                    resampleOutput.AsSpan(0, outputSampleCapacity),
                    ref outLen);

                if (inLen != inputFrames)
                {
                    throw new InvalidOperationException(
                        $"The resampler consumed {inLen} of {inputFrames} input frames.");
                }

                samples = resampleOutput;
                sampleCount = outLen * netEqChannels;
            }

            if (outputChannels == 1)
            {
                outputFifo.Write(samples, 0, sampleCount);
                return;
            }

            int frameCount = sampleCount / channels;
            int convertedSampleCount = frameCount * outputChannels;
            float[] converted = EnsureAudioBuffer(ref channelConvertBuffer, convertedSampleCount, clearNewBuffer: false);

            for (int inIndex = 0, outIndex = 0; inIndex < frameCount; inIndex++)
            {
                float sample = samples[inIndex];
                for (int outputChannel = 0; outputChannel < outputChannels; outputChannel++, outIndex++)
                {
                    converted[outIndex] = sample;
                }
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
            Exception cleanupException = null;
            IntPtr pointerToFree = netEqPtr;
            netEqPtr = IntPtr.Zero;

            if (pointerToFree != IntPtr.Zero)
            {
                try
                {
                    NetEqInterop.FreeNetEq(pointerToFree);
#if LOG_OnAudioFilterReadVcOutput
                    Interlocked.Increment(ref netEqFreeCount);
#endif
                }
                catch (Exception exception)
                {
                    cleanupException = exception;
                }
            }

            try
            {
                netEqPacketBuffer.Free();
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            try
            {
                netEqReadBuffer.Free();
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            try
            {
                resampler.Free();
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            outputFifo.Clear();
            netEqSampleRate = 0;
            netEqChannels = 0;
            audioThreadNetEqConfig = default;
            audioThreadOutputSampleRate = 0;
            audioThreadOutputChannels = 0;

            Volatile.Write(ref currentBufferSizeMs, 0);
            Volatile.Write(ref receiveToInsertLatencyMs, 0);
            Volatile.Write(ref localOutputBufferMs, 0);

            if (cleanupException != null)
            {
                RecordAudioThreadException(cleanupException);
            }

            OnEditorAudioThreadStateDisposed();
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
                $"blocked={Volatile.Read(ref audioCallbacksBlocked)} callbackDepth={Volatile.Read(ref audioCallbackDepth)} " +
                $"disposePending={Volatile.Read(ref disposeAudioThreadStateRequested)} activatePending={Volatile.Read(ref activateOutputRequested)} " +
                $"source={(audioSource != null ? "yes" : "no")} playing={(audioSource != null && audioSource.isPlaying)} " +
                $"clip={(audioSource != null && audioSource.clip != null ? audioSource.clip.name : "none")} " +
                $"dspRate={Volatile.Read(ref cachedOutputSampleRate)} callbacks={Volatile.Read(ref audioCallbackCount)} " +
                $"audioLen={Volatile.Read(ref lastAudioDataLength)} audioCh={Volatile.Read(ref lastAudioChannels)} " +
                $"received={Volatile.Read(ref receivedFrameCount)} accepted={Volatile.Read(ref acceptedFrameCount)} " +
                $"pending={pendingFrames.Count} available={availableFrames.Count} drained={Volatile.Read(ref drainedFrameCount)} " +
                $"dropInvalid={Volatile.Read(ref droppedInvalidFrameCount)} dropFull={Volatile.Read(ref droppedFullFrameCount)} " +
                $"dropStale={Volatile.Read(ref droppedStaleFrameCount)} " +
                $"netEq={(netEqPtr != IntPtr.Zero ? "yes" : "no")} creates={Volatile.Read(ref netEqCreateCount)} " +
                $"createFailures={Volatile.Read(ref netEqCreateFailureCount)} frees={Volatile.Read(ref netEqFreeCount)} " +
                $"netEqRate={netEqSampleRate} netEqCh={netEqChannels} currentBufferMs={Volatile.Read(ref currentBufferSizeMs)} " +
                $"getAudioCalls={Volatile.Read(ref getAudioCallCount)} getAudioSamples={Volatile.Read(ref getAudioSampleCount)} " +
                $"lastGetAudio={Volatile.Read(ref lastGetAudioSamples)} invalidReads={Volatile.Read(ref invalidGetAudioResultCount)} " +
                $"batchMs={Volatile.Read(ref cachedResamplerBufferMs)} localOutMs={Volatile.Read(ref localOutputBufferMs)} " +
                $"cleanupRequests={Volatile.Read(ref cleanupRequestCount)} deferredCleanup={Volatile.Read(ref deferredCleanupCount)} " +
                $"completedCleanup={Volatile.Read(ref completedCleanupCount)} " +
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
            if (!forceRecreate &&
                playbackClip != null &&
                playbackClip.frequency == sampleRate &&
                playbackClip.channels == 1)
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
                // The carrier represents the mono network voice. Unity expands and
                // spatializes it into the active output layout before the callback.
                1,
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

            _ = clip.SetData(playbackClipSamples, 0);
        }

        private static void ConfigureAudioSource(UnityEngine.AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.pitch = 1f;
            source.dopplerLevel = 0f;
            source.bypassReverbZones = true;
            source.reverbZoneMix = 0f;
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
                inputSampleRate != NetworkInputSampleRate ||
                inputChannels != 1 ||
                frameSize % inputChannels != 0)
            {
                return false;
            }

            int samplesPerChannel = frameSize / inputChannels;
            int durationMs = samplesPerChannel * 1000 / inputSampleRate;

            return samplesPerChannel * 1000 == durationMs * inputSampleRate &&
                (durationMs == 10 || durationMs == 20 || durationMs == 40);
        }

        private static bool IsNativeNetEqSupportedPlatform()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return IntPtr.Size == 8;
#else
            return false;
#endif
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
            VcConfig config = metaVc != null ? metaVc.config : null;
            if (config == null || config.samplesPerFrame <= 0)
            {
                return;
            }

            int frameSize = config.samplesPerFrame;
            ulong frameIndex = (ulong)Math.Max(index, 0);
            ushort sequenceNumber = (ushort)(frameIndex % (ushort.MaxValue + 1UL));
            uint timestamp = (uint)(frameIndex * (ulong)frameSize % (uint.MaxValue + 1UL));
            Span<float> frame = samples == null ? Span<float>.Empty : samples.AsSpan();

            Process(
                frame,
                frameSize,
                NetworkInputSampleRate,
                1,
                sequenceNumber,
                timestamp);
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
