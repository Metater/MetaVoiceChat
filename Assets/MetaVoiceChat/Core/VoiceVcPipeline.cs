using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    [DisallowMultipleComponent]
    public sealed class VoiceVcPipeline : VcPipeline
    {
        private const int MaxRenderChannels = 8;
        private const int DefaultRenderQueueCapacity = 8;
        private const int DefaultMaxPendingCaptureFrames = 8;
        private const int DefaultRenderResamplerQuality = 4;
        private const int ErrorLogIntervalMs = 3000;

        [Header("Output")]
        [Tooltip("Pipeline that receives processed voice frames.")]
        public VcPipeline networkOutput;

        [Header("Threading")]
        [SerializeField] private bool processOnWorkerThread = true;
        [SerializeField, Min(1)] private int maxPendingCaptureFrames = DefaultMaxPendingCaptureFrames;

        [Header("AEC3")]
        [SerializeField] private bool enableNativeProcessing = true;
        [SerializeField] private bool enableHighPassFilter = true;
        [SerializeField] private bool enableAec3 = true;
        [SerializeField, Min(1)] private int renderQueueCapacity = DefaultRenderQueueCapacity;
        [SerializeField] private bool feedAudioListenerRender = true;
        [SerializeField, Min(0)] private int initialDelayMs;

        [Header("Noise Suppression")]
        [SerializeField] private Aec3Interop.NoiseSuppressionMode noiseSuppressionMode = Aec3Interop.NoiseSuppressionMode.WebRtc;
        [SerializeField] private Aec3Interop.NoiseSuppressionLevel noiseSuppressionLevel = Aec3Interop.NoiseSuppressionLevel.Db12;

        [Header("AGC")]
        [SerializeField] private bool enableAgc2 = true;
        [SerializeField, Range(0f, 49f)] private float agc2FixedGainDb;
        [SerializeField] private bool agc2AdaptiveDigital = true;
        [SerializeField] private bool agc2InputVolumeController;
        [SerializeField, Range(0, 255)] private int appliedInputVolume = 255;

        [Header("Gain")]
        [SerializeField, Min(0f)] private float userMicrophoneGain = 1f;
        [SerializeField] private bool enablePostLimiter = true;

        [Header("Native Taps")]
        [SerializeField] private bool captureOutputUsed = true;
        [SerializeField] private bool exportLinearAecOutput;
        [SerializeField] private bool publishSpeech16kFrames = true;
        [SerializeField] private bool publishRnnoiseInputFrames = true;
        [SerializeField] private bool collectStats = true;
        [SerializeField, Min(0)] private int fftCapacity = 256;
        [SerializeField, Range(0f, 1f)] private float vadThreshold = 0.5f;

        [Header("Fallback")]
        [SerializeField] private bool forwardOriginalOnNativeFailure = true;
        [SerializeField] private bool logNativeErrors = true;
        [SerializeField, Range(0, 10)] private int renderResamplerQuality = DefaultRenderResamplerQuality;

        private readonly ConcurrentQueue<CaptureFrame> pendingCaptureFrames = new ConcurrentQueue<CaptureFrame>();
        private readonly ConcurrentQueue<CaptureFrame> availableCaptureFrames = new ConcurrentQueue<CaptureFrame>();

        private AutoResetEvent workerSignal;
        private Thread workerThread;
        private int workerRunning;
        private int pendingCaptureFrameCount;

        private Aec3Interop.Instance aec3;
        private Aec3Interop.StatsBuffer statsBuffer;
        private int activeConfigHash;
        private int activeCaptureSampleRateHz;
        private int activeCaptureChannels;
        private int activeFrameSizeMs;
        private int activeRenderSampleRateHz;
        private int activeRenderChannels;

        private float[] inlineCaptureBuffer = Array.Empty<float>();
        private float[] outputBuffer = Array.Empty<float>();
        private float[] aecTapBuffer = Array.Empty<float>();
        private float[] rnnoiseInputBuffer = Array.Empty<float>();
        private float[] rnnoiseOutputBuffer = Array.Empty<float>();
        private float[] speech16kBuffer = Array.Empty<float>();
        private float[] lastFftMagnitudes = Array.Empty<float>();

        private readonly OneWayResampler renderResampler = new OneWayResampler();
        private float[] renderChannelConvertBuffer = Array.Empty<float>();
        private float[] renderResampleBuffer = Array.Empty<float>();
        private float[] renderAccumulator = Array.Empty<float>();
        private int renderAccumulatorCount;
        private int configuredRenderSampleRateHz;
        private int configuredRenderChannels;
        private int configuredRenderFrameSamples;
        private int renderStateSampleRateHz;
        private int renderStateChannels;
        private int renderStateFrameSamples;

        private int cachedUnityRenderSampleRateHz;
        private int cachedUnityRenderChannels = 2;
        private int droppedCaptureFrames;
        private int droppedInvalidCaptureFrames;
        private int droppedRenderFrames;
        private int lastStatus;
        private long nextErrorLogTicks;
        private Aec3Interop.NativeStats lastStats;

        public delegate bool RnnoiseFrameProcessor(
            float[] inputSamples,
            int inputSamplesLen,
            float[] outputSamples,
            int outputSamplesLen,
            int sampleRateHz,
            int channels);

        public event Action<NativeFrame> AecTapFrameReady;
        public event Action<NativeFrame> RnnoiseInputFrameReady;
        public event Action<NativeFrame> Speech16kFrameReady;
        public event Action<StatsFrame> StatsUpdated;

        public RnnoiseFrameProcessor RnnoiseProcessor { get; set; }

        public Aec3Interop.NativeStats LastStats => lastStats;
        public float[] LastFftMagnitudes => lastFftMagnitudes;
        public int LastStatus => Volatile.Read(ref lastStatus);
        public int QueuedRenderFrames => Volatile.Read(ref aec3)?.QueuedRenderFrames ?? 0;
        public long DroppedNativeRenderFrames => Volatile.Read(ref aec3)?.DroppedRenderFrames ?? 0;
        public int DroppedCaptureFrames => Volatile.Read(ref droppedCaptureFrames);
        public int DroppedInvalidCaptureFrames => Volatile.Read(ref droppedInvalidCaptureFrames);
        public int DroppedRenderFrames => Volatile.Read(ref droppedRenderFrames);
        public bool HasNativeProcessor => Volatile.Read(ref aec3) != null;

        public override void Process(
            ReadOnlySpan<float> frame,
            int frameSize,
            int inputFrequency,
            int inputChannels,
            ushort sequenceNumber,
            uint timestamp)
        {
            if (!IsBasicCaptureFrameValid(frame, frameSize, inputFrequency, inputChannels))
            {
                Interlocked.Increment(ref droppedInvalidCaptureFrames);
                return;
            }

            if (processOnWorkerThread && Volatile.Read(ref workerRunning) != 0)
            {
                EnqueueCaptureFrame(frame, frameSize, inputFrequency, inputChannels, sequenceNumber, timestamp);
                return;
            }

            float[] capture = EnsureAudioBuffer(ref inlineCaptureBuffer, frameSize);
            frame.Slice(0, frameSize).CopyTo(capture);
            ProcessCaptureFrame(capture, frameSize, inputFrequency, inputChannels, sequenceNumber, timestamp);
        }

        private void Awake()
        {
            CacheAudioSettings();
            InitializeCaptureFramePool();
        }

        private void OnEnable()
        {
            ValidateSerializedSettings();
            CacheAudioSettings();
            InitializeCaptureFramePool();
            TryPreloadAec3NativeLibrary();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            AudioListenerVcInput.OnAudioFilterReadEvent += HandleAudioListenerRender;

            if (processOnWorkerThread)
            {
                StartWorker();
            }
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            AudioListenerVcInput.OnAudioFilterReadEvent -= HandleAudioListenerRender;
            StopWorker();
            ClearPendingCaptureFrames();
            DisposeNativeState();
            ResetRenderState();
        }

        private void OnDestroy()
        {
            StopWorker();
            DisposeNativeState();

            if (workerSignal != null)
            {
                workerSignal.Dispose();
                workerSignal = null;
            }
        }

        private void Update()
        {
            ValidateSerializedSettings();
            CacheAudioSettings();

            if (processOnWorkerThread && Volatile.Read(ref workerRunning) == 0)
            {
                StartWorker();
            }
            else if (!processOnWorkerThread && Volatile.Read(ref workerRunning) != 0)
            {
                StopWorker();
            }
        }

        private void OnValidate()
        {
            ValidateSerializedSettings();
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            CacheAudioSettings();
        }

        private void StartWorker()
        {
            if (Interlocked.CompareExchange(ref workerRunning, 1, 0) != 0)
            {
                return;
            }

            if (workerSignal == null)
            {
                workerSignal = new AutoResetEvent(false);
            }

            workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = nameof(VoiceVcPipeline)
            };
            workerThread.Start();
        }

        private void StopWorker()
        {
            if (Interlocked.Exchange(ref workerRunning, 0) == 0)
            {
                return;
            }

            AutoResetEvent signal = workerSignal;
            signal?.Set();

            Thread thread = workerThread;
            if (thread != null && thread.IsAlive)
            {
                thread.Join(500);
            }

            workerThread = null;
        }

        private void WorkerLoop()
        {
            while (Volatile.Read(ref workerRunning) != 0)
            {
                DrainCaptureQueue();
                workerSignal?.WaitOne(10);
            }

            DrainCaptureQueue();
        }

        private void EnqueueCaptureFrame(
            ReadOnlySpan<float> frame,
            int frameSize,
            int inputFrequency,
            int inputChannels,
            ushort sequenceNumber,
            uint timestamp)
        {
            int maxPending = Math.Max(1, Volatile.Read(ref maxPendingCaptureFrames));
            if (Interlocked.Increment(ref pendingCaptureFrameCount) > maxPending)
            {
                Interlocked.Decrement(ref pendingCaptureFrameCount);
                Interlocked.Increment(ref droppedCaptureFrames);
                return;
            }

            if (!availableCaptureFrames.TryDequeue(out CaptureFrame captureFrame))
            {
                captureFrame = new CaptureFrame();
            }

            captureFrame.CopyFrom(frame, frameSize, inputFrequency, inputChannels, sequenceNumber, timestamp);
            pendingCaptureFrames.Enqueue(captureFrame);
            workerSignal?.Set();
        }

        private void DrainCaptureQueue()
        {
            while (pendingCaptureFrames.TryDequeue(out CaptureFrame captureFrame))
            {
                Interlocked.Decrement(ref pendingCaptureFrameCount);

                try
                {
                    ProcessCaptureFrame(
                        captureFrame.Samples,
                        captureFrame.SampleLength,
                        captureFrame.SampleRateHz,
                        captureFrame.Channels,
                        captureFrame.SequenceNumber,
                        captureFrame.Timestamp);
                }
                finally
                {
                    availableCaptureFrames.Enqueue(captureFrame);
                }
            }
        }

        private void ClearPendingCaptureFrames()
        {
            while (pendingCaptureFrames.TryDequeue(out CaptureFrame captureFrame))
            {
                availableCaptureFrames.Enqueue(captureFrame);
            }

            Volatile.Write(ref pendingCaptureFrameCount, 0);
        }

        private void InitializeCaptureFramePool()
        {
            int target = Math.Max(1, maxPendingCaptureFrames);
            while (availableCaptureFrames.Count < target)
            {
                availableCaptureFrames.Enqueue(new CaptureFrame());
            }
        }

        private void ProcessCaptureFrame(
            float[] captureSamples,
            int captureSamplesLen,
            int inputFrequency,
            int inputChannels,
            ushort sequenceNumber,
            uint timestamp)
        {
            if (!enableNativeProcessing ||
                !IsSupportedNativeSampleRate(inputFrequency) ||
                !TryGetFrameSizeMs(captureSamplesLen, inputFrequency, inputChannels, out int frameSizeMs))
            {
                ForwardOriginal(captureSamples, captureSamplesLen, inputFrequency, inputChannels, sequenceNumber, timestamp);
                return;
            }

            Aec3Interop.Instance processor;
            try
            {
                processor = EnsureNativeProcessor(inputFrequency, inputChannels, frameSizeMs);
            }
            catch (Exception exception)
            {
                ReportNativeException("create/configure", exception);
                ForwardOriginalIfEnabled(captureSamples, captureSamplesLen, inputFrequency, inputChannels, sequenceNumber, timestamp);
                return;
            }

            if (processor == null)
            {
                ForwardOriginalIfEnabled(captureSamples, captureSamplesLen, inputFrequency, inputChannels, sequenceNumber, timestamp);
                return;
            }

            try
            {
                int outputSamplesLen = processor.OutputSamplesPerFrame;
                int aecTapSamplesLen = exportLinearAecOutput ? outputSamplesLen : 0;
                int rnnoiseSamplesLen = noiseSuppressionMode == Aec3Interop.NoiseSuppressionMode.Rnnoise
                    ? processor.RnnoiseSamplesPerFrame
                    : 0;
                int speech16kSamplesLen = publishSpeech16kFrames ? processor.Speech16kSamplesPerFrame : 0;

                float[] output = EnsureAudioBuffer(ref outputBuffer, outputSamplesLen);
                float[] aecTap = exportLinearAecOutput
                    ? EnsureAudioBuffer(ref aecTapBuffer, aecTapSamplesLen)
                    : null;
                float[] rnnoiseInput = rnnoiseSamplesLen > 0
                    ? EnsureAudioBuffer(ref rnnoiseInputBuffer, rnnoiseSamplesLen)
                    : null;
                float[] speech16k = speech16kSamplesLen > 0
                    ? EnsureAudioBuffer(ref speech16kBuffer, speech16kSamplesLen)
                    : null;
                Aec3Interop.StatsBuffer stats = collectStats ? EnsureStatsBuffer(processor) : null;

                int status = processor.ProcessCapture(
                    captureSamples,
                    captureSamplesLen,
                    output,
                    outputSamplesLen,
                    aecTap,
                    aecTapSamplesLen,
                    rnnoiseInput,
                    rnnoiseSamplesLen,
                    speech16k,
                    speech16kSamplesLen,
                    stats);

                if (status == Aec3Interop.StatusNeedsRnnoise)
                {
                    status = FinishRnnoiseFrame(
                        processor,
                        rnnoiseInput,
                        rnnoiseSamplesLen,
                        output,
                        outputSamplesLen,
                        stats,
                        sequenceNumber,
                        timestamp);
                }

                Volatile.Write(ref lastStatus, status);
                PublishNativeResults(
                    output,
                    outputSamplesLen,
                    aecTap,
                    aecTapSamplesLen,
                    speech16k,
                    speech16kSamplesLen,
                    inputFrequency,
                    inputChannels,
                    sequenceNumber,
                    timestamp,
                    stats);
            }
            catch (Exception exception)
            {
                ReportNativeException("process capture", exception);
                ForwardOriginalIfEnabled(captureSamples, captureSamplesLen, inputFrequency, inputChannels, sequenceNumber, timestamp);
            }
        }

        private int FinishRnnoiseFrame(
            Aec3Interop.Instance processor,
            float[] rnnoiseInput,
            int rnnoiseSamplesLen,
            float[] output,
            int outputSamplesLen,
            Aec3Interop.StatsBuffer stats,
            ushort sequenceNumber,
            uint timestamp)
        {
            if (rnnoiseInput == null || rnnoiseSamplesLen <= 0)
            {
                throw new InvalidOperationException("Native AEC3 requested RNNoise, but no RNNoise input frame was produced.");
            }

            if (publishRnnoiseInputFrames)
            {
                RnnoiseInputFrameReady?.Invoke(new NativeFrame(
                    rnnoiseInput,
                    rnnoiseSamplesLen,
                    48000,
                    activeCaptureChannels,
                    sequenceNumber,
                    timestamp));
            }

            float[] rnnoiseOutput = EnsureAudioBuffer(ref rnnoiseOutputBuffer, rnnoiseSamplesLen);
            bool processed = false;
            RnnoiseFrameProcessor rnnoise = RnnoiseProcessor;
            if (rnnoise != null)
            {
                processed = rnnoise(
                    rnnoiseInput,
                    rnnoiseSamplesLen,
                    rnnoiseOutput,
                    rnnoiseSamplesLen,
                    48000,
                    activeCaptureChannels);
            }

            if (!processed)
            {
                Array.Copy(rnnoiseInput, 0, rnnoiseOutput, 0, rnnoiseSamplesLen);
            }

            return processor.FinishRnnoiseFrame(
                rnnoiseOutput,
                rnnoiseSamplesLen,
                output,
                outputSamplesLen,
                stats);
        }

        private void PublishNativeResults(
            float[] output,
            int outputSamplesLen,
            float[] aecTap,
            int aecTapSamplesLen,
            float[] speech16k,
            int speech16kSamplesLen,
            int outputSampleRateHz,
            int outputChannels,
            ushort sequenceNumber,
            uint timestamp,
            Aec3Interop.StatsBuffer stats)
        {
            Aec3Interop.NativeStats nativeStats = stats != null ? stats.Native : default;
            int processedOutputSamples = GetNativeSampleCount(nativeStats.OutputSamples, outputSamplesLen);

            if (stats != null)
            {
                PublishStats(stats, sequenceNumber, timestamp);
            }

            if (aecTap != null && aecTapSamplesLen > 0)
            {
                int tapSamples = GetNativeSampleCount(nativeStats.AecTapSamples, aecTapSamplesLen);
                if (tapSamples > 0)
                {
                    AecTapFrameReady?.Invoke(new NativeFrame(
                        aecTap,
                        tapSamples,
                        outputSampleRateHz,
                        outputChannels,
                        sequenceNumber,
                        timestamp));
                }
            }

            if (speech16k != null && speech16kSamplesLen > 0)
            {
                int speechSamples = GetNativeSampleCount(nativeStats.Speech16kSamples, speech16kSamplesLen);
                if (speechSamples > 0)
                {
                    Speech16kFrameReady?.Invoke(new NativeFrame(
                        speech16k,
                        speechSamples,
                        16000,
                        1,
                        sequenceNumber,
                        timestamp));
                }
            }

            ForwardToNetwork(output, processedOutputSamples, outputSampleRateHz, outputChannels, sequenceNumber, timestamp);
        }

        private void PublishStats(Aec3Interop.StatsBuffer stats, ushort sequenceNumber, uint timestamp)
        {
            Aec3Interop.NativeStats nativeStats = stats.Native;
            lastStats = nativeStats;

            int binsWritten = Clamp(nativeStats.FftBinsWritten, 0, stats.FftMagnitudes.Length);
            if (binsWritten > 0)
            {
                float[] fft = EnsureAudioBuffer(ref lastFftMagnitudes, binsWritten);
                Array.Copy(stats.FftMagnitudes, 0, fft, 0, binsWritten);
            }

            StatsUpdated?.Invoke(new StatsFrame(nativeStats, lastFftMagnitudes, binsWritten, sequenceNumber, timestamp));
        }

        private Aec3Interop.Instance EnsureNativeProcessor(int inputFrequency, int inputChannels, int frameSizeMs)
        {
            int renderSampleRateHz = ResolveRenderSampleRate(inputFrequency);
            int renderChannels = ResolveRenderChannels(renderSampleRateHz);
            int configHash = HashConfig(inputFrequency, inputChannels, frameSizeMs, renderSampleRateHz, renderChannels);

            Aec3Interop.Instance current = Volatile.Read(ref aec3);
            if (current != null &&
                activeConfigHash == configHash &&
                activeCaptureSampleRateHz == inputFrequency &&
                activeCaptureChannels == inputChannels &&
                activeFrameSizeMs == frameSizeMs &&
                activeRenderSampleRateHz == renderSampleRateHz &&
                activeRenderChannels == renderChannels)
            {
                return current;
            }

            Aec3Interop.Config config = Aec3Interop.CreateDefaultConfig();
            config.SampleRateHz = inputFrequency;
            config.RenderSampleRateHz = renderSampleRateHz;
            config.FrameSizeMs = frameSizeMs;
            config.CaptureChannels = inputChannels;
            config.RenderChannels = renderChannels;
            config.EnableHighPassFilter = BoolToInt(enableHighPassFilter);
            config.EnableAec3 = BoolToInt(enableAec3);
            config.NoiseSuppressionMode = (int)noiseSuppressionMode;
            config.NoiseSuppressionLevel = (int)noiseSuppressionLevel;
            config.EnableAgc2 = BoolToInt(enableAgc2);
            config.Agc2FixedGainDb = agc2FixedGainDb;
            config.Agc2AdaptiveDigital = BoolToInt(agc2AdaptiveDigital);
            config.Agc2InputVolumeController = BoolToInt(agc2InputVolumeController);
            config.AppliedInputVolume = appliedInputVolume;
            config.CaptureOutputUsed = BoolToInt(captureOutputUsed);
            config.UserMicrophoneGain = userMicrophoneGain;
            config.EnablePostLimiter = BoolToInt(enablePostLimiter);
            config.InitialDelayMs = initialDelayMs;
            config.VadThreshold = vadThreshold;
            config.ExportLinearAecOutput = BoolToInt(exportLinearAecOutput);

            Aec3Interop.Instance created = Aec3Interop.Create(config, renderQueueCapacity);
            Aec3Interop.Instance previous = Interlocked.Exchange(ref aec3, created);
            previous?.Dispose();

            activeConfigHash = configHash;
            activeCaptureSampleRateHz = inputFrequency;
            activeCaptureChannels = inputChannels;
            activeFrameSizeMs = frameSizeMs;
            activeRenderSampleRateHz = renderSampleRateHz;
            activeRenderChannels = renderChannels;

            Volatile.Write(ref configuredRenderSampleRateHz, renderSampleRateHz);
            Volatile.Write(ref configuredRenderChannels, renderChannels);
            Volatile.Write(ref configuredRenderFrameSamples, renderSampleRateHz * frameSizeMs / 1000 * renderChannels);
            EnsureStatsBuffer(created);

            return created;
        }

        private void DisposeNativeState()
        {
            Aec3Interop.Instance processor = Interlocked.Exchange(ref aec3, null);
            processor?.Dispose();
            statsBuffer?.Dispose();
            statsBuffer = null;
            activeConfigHash = 0;
            activeCaptureSampleRateHz = 0;
            activeCaptureChannels = 0;
            activeFrameSizeMs = 0;
            activeRenderSampleRateHz = 0;
            activeRenderChannels = 0;
            Volatile.Write(ref configuredRenderSampleRateHz, 0);
            Volatile.Write(ref configuredRenderChannels, 0);
            Volatile.Write(ref configuredRenderFrameSamples, 0);
        }

        private Aec3Interop.StatsBuffer EnsureStatsBuffer(Aec3Interop.Instance processor)
        {
            if (!collectStats)
            {
                return null;
            }

            int requiredFftCapacity = Math.Max(0, Math.Max(fftCapacity, processor.FftBinsPerFrame));
            if (statsBuffer != null && statsBuffer.FftMagnitudes.Length >= requiredFftCapacity)
            {
                return statsBuffer;
            }

            statsBuffer?.Dispose();
            statsBuffer = new Aec3Interop.StatsBuffer(requiredFftCapacity);
            return statsBuffer;
        }

        private void HandleAudioListenerRender(AudioListenerVcInput.OnAudioFilterReadFrame frame)
        {
            if (frame.data == null)
            {
                return;
            }

            int dataLength = frame.dataLength > 0 && frame.dataLength <= frame.data.Length
                ? frame.dataLength
                : frame.data.Length;
            int sourceSampleRateHz = frame.sampleRateHz > 0
                ? frame.sampleRateHz
                : Volatile.Read(ref cachedUnityRenderSampleRateHz);
            int sourceChannels = SanitizeRenderChannels(frame.channels);

            Volatile.Write(ref cachedUnityRenderSampleRateHz, sourceSampleRateHz);
            Volatile.Write(ref cachedUnityRenderChannels, sourceChannels);

            if (!feedAudioListenerRender || frame.data == null || dataLength <= 0)
            {
                return;
            }

            Aec3Interop.Instance processor = Volatile.Read(ref aec3);
            if (processor == null)
            {
                return;
            }

            int targetSampleRateHz = Volatile.Read(ref configuredRenderSampleRateHz);
            int targetChannels = Volatile.Read(ref configuredRenderChannels);
            int targetFrameSamples = Volatile.Read(ref configuredRenderFrameSamples);
            if (targetSampleRateHz <= 0 || targetChannels <= 0 || targetFrameSamples <= 0)
            {
                return;
            }

            dataLength -= dataLength % sourceChannels;
            if (dataLength <= 0)
            {
                return;
            }

            if (renderStateSampleRateHz != targetSampleRateHz ||
                renderStateChannels != targetChannels ||
                renderStateFrameSamples != targetFrameSamples)
            {
                ResetRenderState();
                renderStateSampleRateHz = targetSampleRateHz;
                renderStateChannels = targetChannels;
                renderStateFrameSamples = targetFrameSamples;
            }

            if (!TryPrepareRenderSamples(
                frame.data,
                dataLength,
                sourceSampleRateHz,
                sourceChannels,
                targetSampleRateHz,
                targetChannels,
                out float[] preparedSamples,
                out int preparedSamplesLen))
            {
                Interlocked.Increment(ref droppedRenderFrames);
                return;
            }

            AppendRenderSamples(processor, preparedSamples, preparedSamplesLen, targetFrameSamples, targetChannels);
        }

        private bool TryPrepareRenderSamples(
            float[] source,
            int sourceSamplesLen,
            int sourceSampleRateHz,
            int sourceChannels,
            int targetSampleRateHz,
            int targetChannels,
            out float[] preparedSamples,
            out int preparedSamplesLen)
        {
            preparedSamples = source;
            preparedSamplesLen = sourceSamplesLen;

            if (sourceSampleRateHz <= 0 || targetSampleRateHz <= 0 ||
                sourceChannels <= 0 || targetChannels <= 0)
            {
                return false;
            }

            if (sourceChannels != targetChannels)
            {
                int frameCount = sourceSamplesLen / sourceChannels;
                int convertedSamplesLen = frameCount * targetChannels;
                float[] converted = EnsureAudioBuffer(ref renderChannelConvertBuffer, convertedSamplesLen);
                ConvertChannels(source, sourceChannels, converted, targetChannels, frameCount);
                preparedSamples = converted;
                preparedSamplesLen = convertedSamplesLen;
            }

            if (sourceSampleRateHz == targetSampleRateHz)
            {
                return true;
            }

            if (targetChannels > 2)
            {
                return false;
            }

            int inputFrames = preparedSamplesLen / targetChannels;
            int outputFramesCapacity = Math.Max(
                1,
                (int)Math.Ceiling(inputFrames * (double)targetSampleRateHz / sourceSampleRateHz) + 8);
            float[] resampled = EnsureAudioBuffer(ref renderResampleBuffer, outputFramesCapacity * targetChannels);

            renderResampler.Configure(targetChannels, sourceSampleRateHz, targetSampleRateHz, renderResamplerQuality);
            int inLen = inputFrames;
            int outLen = outputFramesCapacity;
            renderResampler.ProcessInterleaved(
                new Span<float>(preparedSamples, 0, preparedSamplesLen),
                ref inLen,
                new Span<float>(resampled, 0, resampled.Length),
                ref outLen);

            preparedSamples = resampled;
            preparedSamplesLen = outLen * targetChannels;
            return preparedSamplesLen > 0;
        }

        private void AppendRenderSamples(
            Aec3Interop.Instance processor,
            float[] samples,
            int samplesLen,
            int frameSamples,
            int channels)
        {
            if (samplesLen <= 0 || frameSamples <= 0)
            {
                return;
            }

            EnsureAudioBuffer(ref renderAccumulator, frameSamples);
            int sourceIndex = 0;
            int remaining = samplesLen;

            while (remaining > 0)
            {
                int copy = Math.Min(remaining, frameSamples - renderAccumulatorCount);
                Array.Copy(samples, sourceIndex, renderAccumulator, renderAccumulatorCount, copy);
                sourceIndex += copy;
                remaining -= copy;
                renderAccumulatorCount += copy;

                if (renderAccumulatorCount == frameSamples)
                {
                    if (!processor.TryEnqueueRender(renderAccumulator, frameSamples, channels))
                    {
                        Interlocked.Increment(ref droppedRenderFrames);
                    }

                    renderAccumulatorCount = 0;
                }
            }
        }

        private void ResetRenderState()
        {
            ResetRenderAccumulator();
            renderResampler.Free();
            renderStateSampleRateHz = 0;
            renderStateChannels = 0;
            renderStateFrameSamples = 0;
        }

        private void ResetRenderAccumulator()
        {
            renderAccumulatorCount = 0;
        }

        private void ForwardOriginalIfEnabled(
            float[] samples,
            int samplesLen,
            int sampleRateHz,
            int channels,
            ushort sequenceNumber,
            uint timestamp)
        {
            if (forwardOriginalOnNativeFailure)
            {
                ForwardOriginal(samples, samplesLen, sampleRateHz, channels, sequenceNumber, timestamp);
            }
        }

        private void ForwardOriginal(
            float[] samples,
            int samplesLen,
            int sampleRateHz,
            int channels,
            ushort sequenceNumber,
            uint timestamp)
        {
            ForwardToNetwork(samples, samplesLen, sampleRateHz, channels, sequenceNumber, timestamp);
        }

        private void ForwardToNetwork(
            float[] samples,
            int samplesLen,
            int sampleRateHz,
            int channels,
            ushort sequenceNumber,
            uint timestamp)
        {
            if (samples == null || samplesLen <= 0)
            {
                return;
            }

            VcPipeline output = networkOutput;
            if (ReferenceEquals(output, null))
            {
                return;
            }

            output.Process(
                new ReadOnlySpan<float>(samples, 0, Math.Min(samplesLen, samples.Length)),
                Math.Min(samplesLen, samples.Length),
                sampleRateHz,
                channels,
                sequenceNumber,
                timestamp);
        }

        private void CacheAudioSettings()
        {
            Volatile.Write(ref cachedUnityRenderSampleRateHz, AudioSettings.outputSampleRate);
            Volatile.Write(ref cachedUnityRenderChannels, GetSpeakerModeChannelCount(AudioSettings.speakerMode));
        }

        private void TryPreloadAec3NativeLibrary()
        {
            try
            {
                Aec3Interop.PreloadNativeLibrary(Application.dataPath);
            }
            catch (Exception exception)
            {
                ReportNativeException("preload", exception);
            }
        }

        private void ReportNativeException(string operation, Exception exception)
        {
            if (!logNativeErrors || exception == null)
            {
                return;
            }

            long now = Stopwatch.GetTimestamp();
            long next = Volatile.Read(ref nextErrorLogTicks);
            if (next > now)
            {
                return;
            }

            long delayTicks = ErrorLogIntervalMs * Stopwatch.Frequency / 1000;
            Volatile.Write(ref nextErrorLogTicks, now + delayTicks);
            UnityEngine.Debug.LogWarning(
                $"{nameof(VoiceVcPipeline)} failed to {operation} AEC3: {exception.GetType().Name}: {exception.Message}");
        }

        private int ResolveRenderSampleRate(int captureSampleRateHz)
        {
            int unityRenderRate = Volatile.Read(ref cachedUnityRenderSampleRateHz);
            return IsSupportedNativeSampleRate(unityRenderRate)
                ? unityRenderRate
                : captureSampleRateHz;
        }

        private int ResolveRenderChannels(int renderSampleRateHz)
        {
            int channels = SanitizeRenderChannels(Volatile.Read(ref cachedUnityRenderChannels));
            int unityRenderRate = Volatile.Read(ref cachedUnityRenderSampleRateHz);
            if (unityRenderRate != renderSampleRateHz && channels > 2)
            {
                return 2;
            }

            return channels;
        }

        private int HashConfig(
            int captureSampleRateHz,
            int captureChannels,
            int frameSizeMs,
            int renderSampleRateHz,
            int renderChannels)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + captureSampleRateHz;
                hash = hash * 31 + captureChannels;
                hash = hash * 31 + frameSizeMs;
                hash = hash * 31 + renderSampleRateHz;
                hash = hash * 31 + renderChannels;
                hash = hash * 31 + BoolToInt(enableHighPassFilter);
                hash = hash * 31 + BoolToInt(enableAec3);
                hash = hash * 31 + renderQueueCapacity;
                hash = hash * 31 + initialDelayMs;
                hash = hash * 31 + (int)noiseSuppressionMode;
                hash = hash * 31 + (int)noiseSuppressionLevel;
                hash = hash * 31 + BoolToInt(enableAgc2);
                hash = hash * 31 + agc2FixedGainDb.GetHashCode();
                hash = hash * 31 + BoolToInt(agc2AdaptiveDigital);
                hash = hash * 31 + BoolToInt(agc2InputVolumeController);
                hash = hash * 31 + appliedInputVolume;
                hash = hash * 31 + BoolToInt(captureOutputUsed);
                hash = hash * 31 + userMicrophoneGain.GetHashCode();
                hash = hash * 31 + BoolToInt(enablePostLimiter);
                hash = hash * 31 + vadThreshold.GetHashCode();
                hash = hash * 31 + BoolToInt(exportLinearAecOutput);
                return hash;
            }
        }

        private void ValidateSerializedSettings()
        {
            maxPendingCaptureFrames = Math.Max(1, maxPendingCaptureFrames);
            renderQueueCapacity = Math.Max(1, renderQueueCapacity);
            initialDelayMs = Math.Max(0, initialDelayMs);
            agc2FixedGainDb = Clamp(agc2FixedGainDb, 0f, 49f);
            appliedInputVolume = Clamp(appliedInputVolume, 0, 255);
            userMicrophoneGain = Math.Max(0f, userMicrophoneGain);
            vadThreshold = Clamp(vadThreshold, 0f, 1f);
            fftCapacity = Math.Max(0, fftCapacity);
            renderResamplerQuality = Clamp(renderResamplerQuality, 0, 10);

            if (!Enum.IsDefined(typeof(Aec3Interop.NoiseSuppressionMode), noiseSuppressionMode))
            {
                noiseSuppressionMode = Aec3Interop.NoiseSuppressionMode.WebRtc;
            }

            if (!Enum.IsDefined(typeof(Aec3Interop.NoiseSuppressionLevel), noiseSuppressionLevel))
            {
                noiseSuppressionLevel = Aec3Interop.NoiseSuppressionLevel.Db12;
            }
        }

        private static bool IsBasicCaptureFrameValid(
            ReadOnlySpan<float> frame,
            int frameSize,
            int inputFrequency,
            int inputChannels)
        {
            return frameSize > 0 &&
                frame.Length >= frameSize &&
                inputFrequency > 0 &&
                (inputChannels == 1 || inputChannels == 2) &&
                frameSize % inputChannels == 0;
        }

        private static bool TryGetFrameSizeMs(
            int sampleCount,
            int sampleRateHz,
            int channels,
            out int frameSizeMs)
        {
            frameSizeMs = 0;
            if (sampleCount <= 0 || sampleRateHz <= 0 || channels <= 0 || sampleCount % channels != 0)
            {
                return false;
            }

            int samplesPerChannel = sampleCount / channels;
            int numerator = samplesPerChannel * 1000;
            if (numerator % sampleRateHz != 0)
            {
                return false;
            }

            frameSizeMs = numerator / sampleRateHz;
            return frameSizeMs == 10 || frameSizeMs == 20 || frameSizeMs == 40;
        }

        private static bool IsSupportedNativeSampleRate(int sampleRateHz)
        {
            return sampleRateHz == 8000 ||
                sampleRateHz == 12000 ||
                sampleRateHz == 16000 ||
                sampleRateHz == 24000 ||
                sampleRateHz == 48000;
        }

        private static int GetNativeSampleCount(int reportedSamples, int fallbackSamples)
        {
            if (reportedSamples <= 0)
            {
                return fallbackSamples;
            }

            return Math.Min(reportedSamples, fallbackSamples);
        }

        private static void ConvertChannels(
            float[] source,
            int sourceChannels,
            float[] destination,
            int destinationChannels,
            int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                int sourceIndex = frame * sourceChannels;
                int destinationIndex = frame * destinationChannels;

                if (destinationChannels == 1)
                {
                    float sum = 0f;
                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        sum += source[sourceIndex + channel];
                    }

                    destination[destinationIndex] = sum / sourceChannels;
                    continue;
                }

                if (sourceChannels == 1)
                {
                    float sample = source[sourceIndex];
                    for (int channel = 0; channel < destinationChannels; channel++)
                    {
                        destination[destinationIndex + channel] = sample;
                    }

                    continue;
                }

                int copiedChannels = Math.Min(sourceChannels, destinationChannels);
                for (int channel = 0; channel < copiedChannels; channel++)
                {
                    destination[destinationIndex + channel] = source[sourceIndex + channel];
                }

                if (copiedChannels < destinationChannels)
                {
                    float center = 0f;
                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        center += source[sourceIndex + channel];
                    }

                    center /= sourceChannels;
                    for (int channel = copiedChannels; channel < destinationChannels; channel++)
                    {
                        destination[destinationIndex + channel] = center;
                    }
                }
            }
        }

        private static float[] EnsureAudioBuffer(ref float[] buffer, int requiredLength)
        {
            requiredLength = Math.Max(0, requiredLength);
            if (buffer.Length >= requiredLength)
            {
                return buffer;
            }

            buffer = new float[requiredLength];
            return buffer;
        }

        private static int SanitizeRenderChannels(int channels)
        {
            return Clamp(channels <= 0 ? 2 : channels, 1, MaxRenderChannels);
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

        private static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public readonly struct NativeFrame
        {
            public readonly float[] Samples;
            public readonly int SampleLength;
            public readonly int SampleRateHz;
            public readonly int Channels;
            public readonly ushort SequenceNumber;
            public readonly uint Timestamp;

            public NativeFrame(
                float[] samples,
                int sampleLength,
                int sampleRateHz,
                int channels,
                ushort sequenceNumber,
                uint timestamp)
            {
                Samples = samples;
                SampleLength = sampleLength;
                SampleRateHz = sampleRateHz;
                Channels = channels;
                SequenceNumber = sequenceNumber;
                Timestamp = timestamp;
            }
        }

        public readonly struct StatsFrame
        {
            public readonly Aec3Interop.NativeStats Stats;
            public readonly float[] FftMagnitudes;
            public readonly int FftBinsWritten;
            public readonly ushort SequenceNumber;
            public readonly uint Timestamp;

            public StatsFrame(
                Aec3Interop.NativeStats stats,
                float[] fftMagnitudes,
                int fftBinsWritten,
                ushort sequenceNumber,
                uint timestamp)
            {
                Stats = stats;
                FftMagnitudes = fftMagnitudes;
                FftBinsWritten = fftBinsWritten;
                SequenceNumber = sequenceNumber;
                Timestamp = timestamp;
            }
        }

        private sealed class CaptureFrame
        {
            public float[] Samples = Array.Empty<float>();
            public int SampleLength;
            public int SampleRateHz;
            public int Channels;
            public ushort SequenceNumber;
            public uint Timestamp;

            public void CopyFrom(
                ReadOnlySpan<float> source,
                int sampleLength,
                int sampleRateHz,
                int channels,
                ushort sequenceNumber,
                uint timestamp)
            {
                EnsureCapacity(sampleLength);
                source.Slice(0, sampleLength).CopyTo(Samples);
                SampleLength = sampleLength;
                SampleRateHz = sampleRateHz;
                Channels = channels;
                SequenceNumber = sequenceNumber;
                Timestamp = timestamp;
            }

            private void EnsureCapacity(int requiredSamples)
            {
                if (Samples.Length < requiredSamples)
                {
                    Samples = new float[requiredSamples];
                }
            }
        }
    }
}
