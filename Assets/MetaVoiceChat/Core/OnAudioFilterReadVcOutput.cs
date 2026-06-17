using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class OnAudioFilterReadVcOutput : MonoBehaviour
    {
        public const int DefaultMaxPacketsInBuffer = 50;
        public const int DefaultMaxDelayMs = 500;
        public const int DefaultMinDelayMs = 10;
        public const int DefaultAdditionalDelayMs = 0;
        public const int DefaultResamplerQuality = 4;
        public const int DefaultResamplerBufferMs = 10;
        private const int MinimumPendingFrameCapacity = 8;

        [SerializeField] private OnAudioFilterReadVcConfig audioFilterReadConfig;
        [SerializeField] private int pendingFrameCapacity = 128;
        [SerializeField] private bool createPlaybackClip = true;

        [Header("Temporary Diagnostics")]
        [SerializeField] private bool logDiagnostics = true;
        [SerializeField, Min(0.1f)] private float diagnosticsLogIntervalSeconds = 1f;

        private AudioSource audioSource;
        private AudioClip playbackClip;
        private float[] playbackClipSamples = Array.Empty<float>();

        private FrameSlot[] pendingFrames = Array.Empty<FrameSlot>();
        private int pendingFrameMask;
        private int writeCursor;
        private int readCursor;

        private int acceptingFrames;
        private int outputActive;
        private int resetAudioThreadStateRequested;
        private int cachedOutputSampleRate;
        private int cachedDspBufferMs;
        private int cachedMaxPacketsInBuffer = DefaultMaxPacketsInBuffer;
        private int cachedMaxDelayMs = DefaultMaxDelayMs;
        private int cachedMinDelayMs = DefaultMinDelayMs;
        private int cachedAdditionalDelayMs = DefaultAdditionalDelayMs;
        private int cachedResamplerQuality = DefaultResamplerQuality;
        private int cachedResamplerBufferMs = DefaultResamplerBufferMs;

        private int currentBufferSizeMs;
        private int receiveToInsertLatencyMs;
        private int localOutputBufferMs;

        private NetEqInterop.Instance netEq;
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
        private int audioThreadOutputSampleRate;
        private int audioThreadOutputChannels;

        private int receivedFrameCount;
        private int acceptedFrameCount;
        private int droppedInvalidFrameCount;
        private int droppedFullFrameCount;
        private int audioCallbackCount;
        private int drainedFrameCount;
        private int netEqCreateCount;
        private int getAudioCallCount;
        private int getAudioSampleCount;
        private int lastAudioDataLength;
        private int lastAudioChannels;
        private int lastGetAudioSamples;
        private int lastOutputPeakPpm;
        private int audioThreadExceptionCount;
        private string lastAudioThreadException;
        private float nextDiagnosticsLogTime;

        public int CurrentBufferSizeMs
        {
            get { return Volatile.Read(ref currentBufferSizeMs); }
        }

        public int GetReceiveToEarLatencyMs()
        {
            int pendingMs = GetOldestPendingFrameAgeMs();
            int ingressMs = Math.Max(pendingMs, Volatile.Read(ref receiveToInsertLatencyMs));

            return ingressMs +
                Volatile.Read(ref currentBufferSizeMs) +
                Volatile.Read(ref localOutputBufferMs) +
                Volatile.Read(ref cachedDspBufferMs);
        }

        public void ReceiveFrame(
            float[] frame,
            int frameSize,
            int inputSampleRate,
            int inputChannels,
            ulong frameIndex,
            ushort sequenceNumber)
        {
            Interlocked.Increment(ref receivedFrameCount);

            if (Volatile.Read(ref acceptingFrames) == 0 ||
                !IsValidFrameShape(frameSize, inputSampleRate, inputChannels))
            {
                Interlocked.Increment(ref droppedInvalidFrameCount);
                return;
            }

            bool isSilence = frame == null;
            int copiedSamples = isSilence ? 0 : Math.Min(frameSize, frame.Length);
            if (!isSilence && copiedSamples < frameSize)
            {
                Interlocked.Increment(ref droppedInvalidFrameCount);
                return;
            }

            if (!TryReservePendingSlot(out int reservedCursor, out FrameSlot slot))
            {
                Interlocked.Increment(ref droppedFullFrameCount);
                return;
            }

            Volatile.Write(ref slot.State, FrameSlot.Writing);

            if (!isSilence)
            {
                slot.EnsureCapacity(frameSize);
                Array.Copy(frame, 0, slot.Samples, 0, frameSize);
            }

            slot.SampleLength = frameSize;
            slot.InputSampleRate = inputSampleRate;
            slot.InputChannels = inputChannels;
            slot.FrameIndex = frameIndex;
            slot.SequenceNumber = sequenceNumber;
            slot.ReceivedTimestamp = Stopwatch.GetTimestamp();
            slot.IsSilence = isSilence;
            slot.Cursor = reservedCursor;

            Volatile.Write(ref slot.State, FrameSlot.Ready);
            Interlocked.Increment(ref acceptedFrameCount);
        }

        private void OnEnable()
        {
            CacheMainThreadSettings();
            EnsurePendingFrames();
            TryPreloadNetEqNativeLibrary();

            if (TryGetComponent(out audioSource))
            {
                ConfigureAudioSource(audioSource);

                if (createPlaybackClip)
                {
                    CreatePlaybackClip();
                }

                audioSource.Play();
            }

            Volatile.Write(ref outputActive, 1);
            Volatile.Write(ref resetAudioThreadStateRequested, 1);
            Volatile.Write(ref acceptingFrames, 1);
        }

        private void OnDisable()
        {
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

            DisposeAudioThreadState();
            ReleasePendingFrameBuffers();
        }

        private void Update()
        {
            CacheMainThreadSettings();

            if (Volatile.Read(ref outputActive) != 0 && audioSource != null)
            {
                ConfigureAudioSource(audioSource);
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }

            LogDiagnosticsIfNeeded();
        }

        private void OnValidate()
        {
            if (TryGetComponent(out AudioSource source))
            {
                ConfigureAudioSource(source);
            }
        }

        private void TryPreloadNetEqNativeLibrary()
        {
            try
            {
                NetEqInterop.PreloadNativeLibrary(Application.dataPath);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError(
                    $"{nameof(OnAudioFilterReadVcOutput)} failed to preload NetEQ native library: " +
                    $"{exception.GetType().Name}: {exception.Message}",
                    this);
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null)
            {
                return;
            }

            Interlocked.Increment(ref audioCallbackCount);
            Volatile.Write(ref lastAudioDataLength, data.Length);
            Volatile.Write(ref lastAudioChannels, channels);

            try
            {
                int outputSampleRate = Volatile.Read(ref cachedOutputSampleRate);
                if (outputSampleRate <= 0 || channels <= 0 || channels > 2)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                if (Volatile.Read(ref outputActive) == 0)
                {
                    DropPendingFramesAudioThread();
                    DisposeAudioThreadState();
                    Volatile.Write(ref resetAudioThreadStateRequested, 0);
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                if (Interlocked.Exchange(ref resetAudioThreadStateRequested, 0) != 0)
                {
                    DisposeAudioThreadState();
                }

                audioThreadOutputSampleRate = outputSampleRate;
                audioThreadOutputChannels = channels;

                float[] spatializationBuffer = EnsureAudioBuffer(ref spatializationInputBuffer, data.Length, clearNewBuffer: false);
                Array.Copy(data, 0, spatializationBuffer, 0, data.Length);

                DrainPendingFramesAudioThread();

                if (netEq == null)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                FillOutputFifo(data.Length, outputSampleRate, channels);

                int copied = outputFifo.Read(data, 0, data.Length);
                if (copied < data.Length)
                {
                    Array.Clear(data, copied, data.Length - copied);
                }

                ApplySpatializationMask(data, spatializationBuffer, data.Length);
                CacheOutputPeak(data);
                CacheAudioThreadLatency();
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref audioThreadExceptionCount);
                lastAudioThreadException = exception.GetType().Name + ": " + exception.Message;
                DisposeAudioThreadState();
                Array.Clear(data, 0, data.Length);
            }
        }

        private void DrainPendingFramesAudioThread()
        {
            while (TryPeekPendingFrame(out FrameSlot slot))
            {
                if (!EnsureNetEqFor(slot.InputSampleRate, slot.InputChannels))
                {
                    ReleasePendingFrameAudioThread(slot);
                    continue;
                }

                float[] packetSamples = slot.IsSilence
                    ? EnsureAudioBuffer(ref silencePacketBuffer, slot.SampleLength, clearNewBuffer: false)
                    : slot.Samples;

                if (slot.IsSilence)
                {
                    Array.Clear(packetSamples, 0, slot.SampleLength);
                }

                int samplesPerChannel = slot.SampleLength / slot.InputChannels;
                int durationMs = Math.Max(1, (int)Math.Round(samplesPerChannel * 1000.0 / slot.InputSampleRate));
                uint timestamp = unchecked((uint)(slot.FrameIndex * (ulong)samplesPerChannel));

                netEq.InsertPacket(
                    slot.SequenceNumber,
                    timestamp,
                    packetSamples,
                    slot.SampleLength,
                    slot.InputSampleRate,
                    slot.InputChannels,
                    durationMs);

                Volatile.Write(
                    ref receiveToInsertLatencyMs,
                    TimestampAgeMs(slot.ReceivedTimestamp, Stopwatch.GetTimestamp()));

                ReleasePendingFrameAudioThread(slot);
                Interlocked.Increment(ref drainedFrameCount);
            }

            if (netEq != null)
            {
                Volatile.Write(ref currentBufferSizeMs, netEq.CurrentBufferSizeMs);
            }
        }

        private bool EnsureNetEqFor(int sampleRate, int channels)
        {
            if (sampleRate <= 0 || channels <= 0 || channels > 2)
            {
                return false;
            }

            if (netEq != null && netEqSampleRate == sampleRate && netEqChannels == channels)
            {
                return true;
            }

            DisposeAudioThreadState();

            netEq = NetEqInterop.Create(
                sampleRate,
                channels,
                Volatile.Read(ref cachedMaxPacketsInBuffer),
                Volatile.Read(ref cachedMaxDelayMs),
                Volatile.Read(ref cachedMinDelayMs),
                Volatile.Read(ref cachedAdditionalDelayMs));

            netEqSampleRate = sampleRate;
            netEqChannels = channels;
            Interlocked.Increment(ref netEqCreateCount);
            return true;
        }

        private void FillOutputFifo(int requiredSamples, int outputSampleRate, int outputChannels)
        {
            if (requiredSamples <= 0 || outputSampleRate <= 0 || outputChannels <= 0)
            {
                return;
            }

            int safety = 0;
            while (outputFifo.Count < requiredSamples && safety++ < 64)
            {
                int samplesPerChannel = Math.Max(1, netEqSampleRate / 100);
                int readLength = samplesPerChannel * netEqChannels;
                float[] readBuffer = EnsureAudioBuffer(ref netEqReadBuffer, readLength, clearNewBuffer: false);
                int readSamples = netEq.GetAudio(readBuffer, readLength);
                Interlocked.Increment(ref getAudioCallCount);
                Interlocked.Add(ref getAudioSampleCount, readSamples);
                Volatile.Write(ref lastGetAudioSamples, readSamples);

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

            if (channels == 1 && outputChannels == 2)
            {
                for (int inIndex = 0, outIndex = 0; inIndex < frameCount; inIndex++, outIndex += 2)
                {
                    float sample = samples[inIndex];
                    converted[outIndex] = sample;
                    converted[outIndex + 1] = sample;
                }
            }
            else if (channels == 2 && outputChannels == 1)
            {
                for (int inIndex = 0, outIndex = 0; outIndex < frameCount; inIndex += 2, outIndex++)
                {
                    converted[outIndex] = (samples[inIndex] + samples[inIndex + 1]) * 0.5f;
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
            if (netEq != null)
            {
                netEq.Dispose();
                netEq = null;
            }

            resampler.Free();
            outputFifo.Clear();
            netEqSampleRate = 0;
            netEqChannels = 0;
            audioThreadOutputSampleRate = 0;
            audioThreadOutputChannels = 0;

            Volatile.Write(ref currentBufferSizeMs, 0);
            Volatile.Write(ref receiveToInsertLatencyMs, 0);
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
            if (netEq != null)
            {
                Volatile.Write(ref currentBufferSizeMs, netEq.CurrentBufferSizeMs);
            }
        }

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

        private void LogDiagnosticsIfNeeded()
        {
            if (!logDiagnostics || Time.unscaledTime < nextDiagnosticsLogTime)
            {
                return;
            }

            nextDiagnosticsLogTime = Time.unscaledTime + Math.Max(0.1f, diagnosticsLogIntervalSeconds);

            int read = Volatile.Read(ref readCursor);
            int write = Volatile.Read(ref writeCursor);
            int pending = Math.Max(0, write - read);
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
                $"pending={pending} read={read} write={write} drained={Volatile.Read(ref drainedFrameCount)} " +
                $"dropInvalid={Volatile.Read(ref droppedInvalidFrameCount)} dropFull={Volatile.Read(ref droppedFullFrameCount)} " +
                $"netEq={(netEq != null ? "yes" : "no")} netEqCreates={Volatile.Read(ref netEqCreateCount)} " +
                $"netEqRate={netEqSampleRate} netEqCh={netEqChannels} currentBufferMs={Volatile.Read(ref currentBufferSizeMs)} " +
                $"getAudioCalls={Volatile.Read(ref getAudioCallCount)} getAudioSamples={Volatile.Read(ref getAudioSampleCount)} " +
                $"lastGetAudio={Volatile.Read(ref lastGetAudioSamples)} localOutMs={Volatile.Read(ref localOutputBufferMs)} " +
                $"peak={Volatile.Read(ref lastOutputPeakPpm) / 1000000f:0.000000} " +
                $"exceptions={Volatile.Read(ref audioThreadExceptionCount)} lastException={exception}",
                this);
        }

        private bool TryReservePendingSlot(out int reservedCursor, out FrameSlot slot)
        {
            FrameSlot[] slots = pendingFrames;
            int capacity = slots.Length;

            while (capacity > 0)
            {
                int write = Volatile.Read(ref writeCursor);
                int read = Volatile.Read(ref readCursor);

                if (write - read >= capacity)
                {
                    reservedCursor = -1;
                    slot = null;
                    return false;
                }

                if (Interlocked.CompareExchange(ref writeCursor, write + 1, write) == write)
                {
                    reservedCursor = write;
                    slot = slots[write & pendingFrameMask];
                    return true;
                }
            }

            reservedCursor = -1;
            slot = null;
            return false;
        }

        private bool TryPeekPendingFrame(out FrameSlot slot)
        {
            FrameSlot[] slots = pendingFrames;
            if (slots.Length == 0)
            {
                slot = null;
                return false;
            }

            int read = Volatile.Read(ref readCursor);
            if (read >= Volatile.Read(ref writeCursor))
            {
                slot = null;
                return false;
            }

            FrameSlot candidate = slots[read & pendingFrameMask];
            if (Volatile.Read(ref candidate.State) != FrameSlot.Ready ||
                candidate.Cursor != read)
            {
                slot = null;
                return false;
            }

            slot = candidate;
            return true;
        }

        private void ReleasePendingFrameAudioThread(FrameSlot slot)
        {
            Volatile.Write(ref slot.State, FrameSlot.Empty);
            Volatile.Write(ref readCursor, slot.Cursor + 1);
        }

        private void DropPendingFramesAudioThread()
        {
            while (TryPeekPendingFrame(out FrameSlot slot))
            {
                ReleasePendingFrameAudioThread(slot);
            }
        }

        private int GetOldestPendingFrameAgeMs()
        {
            if (!TryPeekPendingFrame(out FrameSlot slot))
            {
                return 0;
            }

            return TimestampAgeMs(slot.ReceivedTimestamp, Stopwatch.GetTimestamp());
        }

        private void EnsurePendingFrames()
        {
            int capacity = NextPowerOfTwo(Math.Max(MinimumPendingFrameCapacity, pendingFrameCapacity));
            if (pendingFrames.Length == capacity)
            {
                ResetPendingFrameCursors();
                return;
            }

            ReleasePendingFrameBuffers();

            pendingFrames = new FrameSlot[capacity];
            for (int i = 0; i < pendingFrames.Length; i++)
            {
                pendingFrames[i] = new FrameSlot();
            }

            pendingFrameMask = capacity - 1;
            ResetPendingFrameCursors();
        }

        private void ResetPendingFrameCursors()
        {
            Volatile.Write(ref writeCursor, 0);
            Volatile.Write(ref readCursor, 0);

            for (int i = 0; i < pendingFrames.Length; i++)
            {
                Volatile.Write(ref pendingFrames[i].State, FrameSlot.Empty);
            }
        }

        private void ReleasePendingFrameBuffers()
        {
            for (int i = 0; i < pendingFrames.Length; i++)
            {
                pendingFrames[i]?.Release();
            }

            pendingFrames = Array.Empty<FrameSlot>();
            pendingFrameMask = 0;
            ResetPendingFrameCursors();
        }

        private void CacheMainThreadSettings()
        {
            int outputSampleRate = AudioSettings.outputSampleRate;
            Volatile.Write(ref cachedOutputSampleRate, outputSampleRate);

            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
            int dspMs = outputSampleRate > 0 ? bufferLength * numBuffers * 1000 / outputSampleRate : 0;
            Volatile.Write(ref cachedDspBufferMs, dspMs);

            OnAudioFilterReadVcConfig config = audioFilterReadConfig;
            Volatile.Write(ref cachedMaxPacketsInBuffer, Math.Max(1, config != null ? config.maxPacketsInBuffer : DefaultMaxPacketsInBuffer));
            Volatile.Write(ref cachedMaxDelayMs, Math.Max(0, config != null ? (int)config.maxDelayMs : DefaultMaxDelayMs));
            Volatile.Write(ref cachedMinDelayMs, Math.Max(0, config != null ? (int)config.minDelayMs : DefaultMinDelayMs));
            Volatile.Write(ref cachedAdditionalDelayMs, Math.Max(0, config != null ? (int)config.additionalDelayMs : DefaultAdditionalDelayMs));
            Volatile.Write(ref cachedResamplerQuality, Mathf.Clamp(config != null ? config.resamplerQuality : DefaultResamplerQuality, 0, 10));
            Volatile.Write(ref cachedResamplerBufferMs, config != null ? config.ResamplerBufferMs : DefaultResamplerBufferMs);
        }

        private void CreatePlaybackClip()
        {
            int sampleRate = Math.Max(1, Volatile.Read(ref cachedOutputSampleRate));
            if (playbackClip != null && playbackClip.frequency == sampleRate)
            {
                audioSource.clip = playbackClip;
                return;
            }

            if (playbackClip != null)
            {
                Destroy(playbackClip);
            }

            playbackClip = AudioClip.Create(
                nameof(OnAudioFilterReadVcOutput),
                sampleRate,
                2,
                sampleRate,
                false);
            FillPlaybackClip(playbackClip);
            audioSource.clip = playbackClip;
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

            clip.SetData(playbackClipSamples, 0);
        }

        private static void ConfigureAudioSource(AudioSource source)
        {
            source.loop = true;
            source.priority = 0;
            source.dopplerLevel = 0f;
        }

        private static bool IsValidFrameShape(int frameSize, int inputSampleRate, int inputChannels)
        {
            return frameSize > 0 &&
                inputSampleRate > 0 &&
                (inputChannels == 1 || inputChannels == 2) &&
                frameSize % inputChannels == 0;
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

        private sealed class FrameSlot
        {
            public const int Empty = 0;
            public const int Writing = 1;
            public const int Ready = 2;

            public float[] Samples = Array.Empty<float>();
            public int State;
            public int Cursor;
            public int SampleLength;
            public int InputSampleRate;
            public int InputChannels;
            public ulong FrameIndex;
            public ushort SequenceNumber;
            public long ReceivedTimestamp;
            public bool IsSilence;

            public void EnsureCapacity(int sampleCount)
            {
                if (Samples.Length >= sampleCount)
                {
                    return;
                }

                if (Samples.Length > 0)
                {
                    ArrayPool<float>.Shared.Return(Samples);
                }

                Samples = ArrayPool<float>.Shared.Rent(sampleCount);
            }

            public void Release()
            {
                if (Samples.Length > 0)
                {
                    ArrayPool<float>.Shared.Return(Samples);
                    Samples = Array.Empty<float>();
                }

                State = Empty;
                Cursor = 0;
                SampleLength = 0;
                InputSampleRate = 0;
                InputChannels = 0;
                FrameIndex = 0;
                SequenceNumber = 0;
                ReceivedTimestamp = 0;
                IsSilence = false;
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
