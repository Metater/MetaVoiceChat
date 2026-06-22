#nullable disable

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MetaVoiceChat.Core
{
    /// <summary>
    /// Managed C# interface for the split native high-pass filter, AEC3, and AGC2 handles.
    /// Audio is interleaved, normalized floating-point PCM (-1.0 to 1.0).
    ///
    /// The normal capture sequence is: HighPassFilter.ProcessInPlace ->
    /// EchoCanceller.ProcessCapture -> external RNN denoiser -> Agc2.Process.
    /// WebRTC noise suppression is intentionally not exposed by this API.
    /// </summary>
    public static class Aec3Interop
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string LibraryName = "__Internal";
#else
        private const string LibraryName = "meta_voice_chat_aec3";
#endif

        public const int StatusOk = 0;
        public const int StatusNullPointer = -1;
        public const int StatusInvalidConfig = -2;
        public const int StatusInvalidArgument = -3;
        public const int StatusBufferTooSmall = -4;
        public const int StatusPanic = -99;
        /// <summary>Managed-only result returned by non-blocking direct calls.</summary>
        public const int StatusBusy = -1000;

        public const int MaxRenderChannels = 8;
        public const int MaxCaptureChannels = 2;
        public const int AecLinearTapSampleRateHz = 16000;
        public const int AecLinearTapSamplesPer10MsPerChannel = 160;

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct HighPassConfig
        {
            public int SampleRateHz;
            public int Channels;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct HighPassStats
        {
            public int StructSize;
            public int SampleRateHz;
            public int Channels;
            public int ProcessedSamples;
            public ulong TotalProcessedSamples;
            public float OutputRms;
            public float OutputPeak;
        }

        /// <summary>
        /// Complete native AEC3 creation configuration. Use CreateDefaultAecConfig
        /// before overriding advanced tuning fields.
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct AecConfig
        {
            public int SampleRateHz;
            public int RenderChannels;
            public int CaptureChannels;
            public int InitialDelayMs;
            public int FixedCaptureDelaySamples;

            public int ExcessRenderDetectionIntervalBlocks;
            public int MaxAllowedExcessRenderBlocks;
            public int DelayDefaultBlocks;
            public int DelayDownSamplingFactor;
            public int DelayNumFilters;
            public int DelayHeadroomSamples;
            public int DelayHysteresisLimitBlocks;
            public float DelayEstimateSmoothing;
            public float DelayCandidateDetectionThreshold;
            public int DelaySelectionThresholdInitial;
            public int DelaySelectionThresholdConverged;
            public int DelayUseExternalEstimator;
            public int DelayLogWarnings;

            public int RenderAlignmentDownmix;
            public int RenderAlignmentAdaptiveSelection;
            public float RenderAlignmentActivityPowerThreshold;
            public int RenderAlignmentPreferFirstTwoChannels;
            public int CaptureAlignmentDownmix;
            public int CaptureAlignmentAdaptiveSelection;
            public float CaptureAlignmentActivityPowerThreshold;
            public int CaptureAlignmentPreferFirstTwoChannels;

            public int MainFilterLengthBlocks;
            public float MainFilterLeakageConverged;
            public float MainFilterLeakageDiverged;
            public float MainFilterErrorFloor;
            public float MainFilterErrorCeil;
            public float MainFilterNoiseGate;
            public int MainInitialFilterLengthBlocks;
            public float MainInitialFilterLeakageConverged;
            public float MainInitialFilterLeakageDiverged;
            public float MainInitialFilterErrorFloor;
            public float MainInitialFilterErrorCeil;
            public float MainInitialFilterNoiseGate;
            public int ShadowFilterLengthBlocks;
            public int ShadowInitialFilterLengthBlocks;
            public float ShadowFilterRate;
            public float ShadowFilterNoiseGate;
            public float ShadowInitialFilterRate;
            public float ShadowInitialFilterNoiseGate;
            public int FilterConfigChangeDurationBlocks;
            public float FilterInitialStateSeconds;
            public int ShadowResetHangoverBlocks;
            public int UseShadowResetHangover;
            public int ConservativeInitialPhase;
            public int EnableShadowFilterOutputUsage;
            public int UseLinearFilter;
            public int ExportLinearAecOutput;

            public float ErleMin;
            public float ErleMaxLow;
            public float ErleMaxHigh;
            public int ErleOnsetDetection;
            public int ErleNumSections;
            public int ErleClampToZero;
            public int ErleClampToOne;
            public float EpStrengthDefaultGain;
            public float EpStrengthDefaultLen;
            public int EpStrengthEchoCanSaturate;
            public int EpStrengthBoundedErl;
            public float EchoAudibilityLowRenderLimit;
            public float EchoAudibilityNormalRenderLimit;
            public float EchoAudibilityFloorPower;
            public float EchoAudibilityThresholdLf;
            public float EchoAudibilityThresholdMf;
            public float EchoAudibilityThresholdHf;
            public int EchoAudibilityUseStationarityProperties;
            public int EchoAudibilityUseStationarityPropertiesAtInit;
            public float RenderLevelsActiveRenderLimit;
            public float RenderLevelsPoorExcitationRenderLimit;
            public float RenderLevelsPoorExcitationRenderLimitDs8;
            public float RenderLevelsRenderPowerGainDb;
            public int HasClockDrift;
            public int LinearAndStableEchoPath;
            public int EnableTransparentMode;
            public int TransparentModeUseHmm;

            public int EchoModelNoiseFloorHold;
            public float EchoModelMinNoiseFloorPower;
            public float EchoModelStationaryGateSlope;
            public float EchoModelNoiseGatePower;
            public float EchoModelNoiseGateSlope;
            public int EchoModelRenderPreWindowSize;
            public int EchoModelRenderPostWindowSize;

            public int SuppressorNearendAverageBlocks;
            public float SuppressorNormalLfEnrTransparent;
            public float SuppressorNormalLfEnrSuppress;
            public float SuppressorNormalLfEmrTransparent;
            public float SuppressorNormalHfEnrTransparent;
            public float SuppressorNormalHfEnrSuppress;
            public float SuppressorNormalHfEmrTransparent;
            public float SuppressorNormalMaxIncFactor;
            public float SuppressorNormalMaxDecFactorLf;
            public float SuppressorNearendLfEnrTransparent;
            public float SuppressorNearendLfEnrSuppress;
            public float SuppressorNearendLfEmrTransparent;
            public float SuppressorNearendHfEnrTransparent;
            public float SuppressorNearendHfEnrSuppress;
            public float SuppressorNearendHfEmrTransparent;
            public float SuppressorNearendMaxIncFactor;
            public float SuppressorNearendMaxDecFactorLf;
            public float DominantNearendEnrThreshold;
            public float DominantNearendEnrExitThreshold;
            public float DominantNearendSnrThreshold;
            public int DominantNearendHoldDuration;
            public int DominantNearendTriggerThreshold;
            public int DominantNearendUseDuringInitialPhase;
            public int SubbandNearendAverageBlocks;
            public int Subband1Low;
            public int Subband1High;
            public int Subband2Low;
            public int Subband2High;
            public float SubbandNearendThreshold;
            public float SubbandSnrThreshold;
            public int EnableSubbandNearendDetection;
            public float HighBandsEnrThreshold;
            public float HighBandsMaxGainDuringEcho;
            public float HighBandsAntiHowlingActivationThreshold;
            public float HighBandsAntiHowlingGain;
            public float SuppressorFloorFirstIncrease;

            public float VadThreshold;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct AecStats
        {
            public int StructSize;
            public int SampleRateHz;
            public int RenderChannels;
            public int CaptureChannels;
            public int ProcessedSamples;
            public int OutputSamples;
            public ulong TotalRenderSamples;
            public ulong TotalCaptureSamples;
            public float VoiceProbability;
            public int VoiceDetected;
            public float OutputRms;
            public float OutputPeak;
            public double EchoReturnLoss;
            public double EchoReturnLossEnhancement;
            public int DelayMs;
            public int RenderJitterMin;
            public int RenderJitterMax;
            public int CaptureJitterMin;
            public int CaptureJitterMax;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct AecMetrics
        {
            public double EchoReturnLoss;
            public double EchoReturnLossEnhancement;
            public int DelayMs;
            public int RenderJitterMin;
            public int RenderJitterMax;
            public int CaptureJitterMin;
            public int CaptureJitterMax;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct Agc2Config
        {
            public int SampleRateHz;
            public int Channels;
            public float FixedGainDb;
            public int EnableAdaptiveDigital;
            public float AdaptiveHeadroomDb;
            public float AdaptiveMaxGainDb;
            public float AdaptiveInitialGainDb;
            public float AdaptiveMaxGainChangeDbPerSecond;
            public float AdaptiveMaxOutputNoiseLevelDbfs;
            public int EnableInputVolumeController;
            public int UseInternalVad;
            public int CaptureOutputUsed;
            public int IvcMinInputVolume;
            public int IvcClippedLevelMin;
            public int IvcClippedLevelStep;
            public float IvcClippedRatioThreshold;
            public int IvcClippedWaitFrames;
            public int IvcEnableClippingPredictor;
            public int IvcTargetRangeMaxDbfs;
            public int IvcTargetRangeExperimentalMaxDbfs;
            public int IvcTargetRangeMinDbfs;
            public int IvcUpdateInputVolumeWaitFrames;
            public float IvcSpeechProbabilityThreshold;
            public float IvcSpeechRatioThreshold;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct Agc2Stats
        {
            public int StructSize;
            public int SampleRateHz;
            public int Channels;
            public int ProcessedSamples;
            public ulong TotalProcessedSamples;
            public int AppliedInputVolume;
            public int RecommendedInputVolume;
            public float VoiceProbability;
            public float OutputRms;
            public float OutputPeak;
        }

        public static HighPassConfig CreateDefaultHighPassConfig()
        {
            HighPassConfig config;
            ThrowIfError(meta_aec3_high_pass_default_config(out config));
            return config;
        }

        public static AecConfig CreateDefaultAecConfig()
        {
            AecConfig config;
            ThrowIfError(meta_aec3_aec_default_config(out config));
            return config;
        }

        public static Agc2Config CreateDefaultAgc2Config()
        {
            Agc2Config config;
            ThrowIfError(meta_aec3_agc2_default_config(out config));
            return config;
        }

        public static int NativeHighPassStatsSize { get { return meta_aec3_sizeof_high_pass_stats(); } }
        public static int NativeAecStatsSize { get { return meta_aec3_sizeof_aec_stats(); } }
        public static int NativeAgc2StatsSize { get { return meta_aec3_sizeof_agc2_stats(); } }

        /// <summary>
        /// Verifies the three exported telemetry records against this managed ABI.
        /// Call this once during application startup after the native plugin loads.
        /// </summary>
        public static void ValidateNativeStructLayouts()
        {
            ValidateNativeStructSize(typeof(HighPassStats), NativeHighPassStatsSize);
            ValidateNativeStructSize(typeof(AecStats), NativeAecStatsSize);
            ValidateNativeStructSize(typeof(Agc2Stats), NativeAgc2StatsSize);
        }

        /// <summary>One thread-affine native high-pass filter instance.</summary>
        public sealed class HighPassFilter : IDisposable
        {
            private readonly HighPassSafeHandle handle;

            public HighPassFilter(HighPassConfig config)
            {
                ValidateHighPassConfig(config);
                handle = meta_aec3_high_pass_create(ref config);
                if (handle == null || handle.IsInvalid)
                {
                    handle?.Dispose();
                    throw new InvalidOperationException("Failed to create native high-pass filter.");
                }
            }

            public int SamplesPer10Ms { get { return GetPositiveResult(meta_aec3_high_pass_samples_per_10ms(GetHandle())); } }

            public HighPassConfig CurrentConfig
            {
                get
                {
                    HighPassConfig config;
                    ThrowIfError(meta_aec3_high_pass_get_config(GetHandle(), out config));
                    return config;
                }
            }

            public void Reconfigure(HighPassConfig config)
            {
                ValidateHighPassConfig(config);
                ThrowIfError(meta_aec3_high_pass_reconfigure(GetHandle(), ref config));
            }

            public void Reset()
            {
                ThrowIfError(meta_aec3_high_pass_reset(GetHandle()));
            }

            public void ProcessInPlace(float[] audio, int audioLength)
            {
                ValidateSamples(audio, audioLength, nameof(audio));
                using (PinnedFloatArray pin = new PinnedFloatArray(audio))
                {
                    ThrowIfError(meta_aec3_high_pass_process(GetHandle(), pin.Pointer, audioLength, IntPtr.Zero));
                }
            }

            public HighPassStats ProcessInPlaceWithStats(float[] audio, int audioLength)
            {
                ValidateSamples(audio, audioLength, nameof(audio));
                HighPassStats stats;
                using (PinnedFloatArray pin = new PinnedFloatArray(audio))
                {
                    ThrowIfError(meta_aec3_high_pass_process_with_stats(GetHandle(), pin.Pointer, audioLength, out stats));
                }
                return stats;
            }

            public void Dispose()
            {
                handle.Dispose();
            }

            private HighPassSafeHandle GetHandle()
            {
                if (handle.IsClosed || handle.IsInvalid)
                {
                    throw new ObjectDisposedException(nameof(HighPassFilter));
                }
                return handle;
            }
        }

        /// <summary>
        /// AEC3 native state plus a single-producer/single-consumer render queue.
        ///
        /// Call TryEnqueueRenderFrame from the render/playback thread. It never
        /// enters native AEC3 and never waits for capture. ProcessCapture, called
        /// by the capture thread, drains that queue immediately before processing
        /// capture audio. Direct render APIs are available for non-realtime use.
        /// </summary>
        public sealed class EchoCanceller : IDisposable
        {
            private readonly AecSafeHandle handle;
            private readonly RenderFrameQueue renderQueue;
            private readonly int renderSamplesPer10Ms;
            private readonly int captureSamplesPer10Ms;
            private readonly int sampleRateHz;
            private readonly int renderChannels;
            private readonly int captureChannels;
            private int nativeGate;
            private int disposed;

            public EchoCanceller(AecConfig config, int renderQueueCapacity = 8)
            {
                ValidateAecConfig(config);
                ValidatePositive(renderQueueCapacity, nameof(renderQueueCapacity));
                handle = meta_aec3_aec_create(ref config);
                if (handle == null || handle.IsInvalid)
                {
                    handle?.Dispose();
                    throw new InvalidOperationException("Failed to create native AEC3 instance.");
                }

                renderSamplesPer10Ms = GetPositiveResult(meta_aec3_aec_render_samples_per_10ms(handle));
                captureSamplesPer10Ms = GetPositiveResult(meta_aec3_aec_capture_samples_per_10ms(handle));
                sampleRateHz = config.SampleRateHz;
                renderChannels = config.RenderChannels;
                captureChannels = config.CaptureChannels;
                renderQueue = new RenderFrameQueue(renderQueueCapacity, renderSamplesPer10Ms);
            }

            public int RenderSamplesPer10Ms { get { return renderSamplesPer10Ms; } }
            public int CaptureSamplesPer10Ms { get { return captureSamplesPer10Ms; } }
            public int LinearTapSamplesPer10Ms { get { return AecLinearTapSamplesPer10MsPerChannel * captureChannels; } }
            public int QueuedRenderFrames { get { return renderQueue.Count; } }
            public long DroppedRenderFrames { get { return renderQueue.DroppedFrames; } }

            public AecConfig CurrentConfig
            {
                get
                {
                    EnterNative();
                    try
                    {
                        AecConfig config;
                        ThrowIfError(meta_aec3_aec_get_config(GetHandle(), out config));
                        return config;
                    }
                    finally { ExitNative(); }
                }
            }

            /// <summary>
            /// Lock-free (SPSC) render submission. The producer must be one render
            /// thread and the consumer is the capture thread. Input must be exactly
            /// one 10-ms render frame for this AEC instance.
            /// </summary>
            public bool TryEnqueueRenderFrame(float[] render, int renderLength)
            {
                ThrowIfDisposed();
                if (render == null || renderLength != renderSamplesPer10Ms || renderLength > render.Length)
                {
                    return false;
                }
                return renderQueue.TryEnqueue(render, renderLength);
            }

            public void EnqueueRenderFrame(float[] render, int renderLength)
            {
                ValidateSamples(render, renderLength, nameof(render));
                if (renderLength != renderSamplesPer10Ms)
                {
                    throw new ArgumentOutOfRangeException(nameof(renderLength), "Queued render input must contain exactly one 10-ms frame.");
                }
                if (!TryEnqueueRenderFrame(render, renderLength))
                {
                    throw new InvalidOperationException("The AEC3 render queue is full.");
                }
            }

            /// <summary>
            /// Directly feeds one or more 10-ms render frames to native AEC3. This
            /// serializes with capture; use TryEnqueueRenderFrame for real-time use.
            /// </summary>
            public void ProcessRenderDirect(float[] render, int renderLength)
            {
                ValidateAecRender(render, renderLength, renderSamplesPer10Ms);
                EnterNative();
                try { ProcessRenderDirectNoLock(render, renderLength); }
                finally { ExitNative(); }
            }

            public bool TryProcessRenderDirect(float[] render, int renderLength, out int status)
            {
                status = StatusInvalidArgument;
                if (!IsValidAecRender(render, renderLength, renderSamplesPer10Ms))
                {
                    return false;
                }
                if (!TryEnterNative())
                {
                    status = StatusBusy;
                    return false;
                }
                try
                {
                    using (PinnedFloatArray pin = new PinnedFloatArray(render))
                    {
                        status = meta_aec3_aec_process_render(GetHandle(), pin.Pointer, renderLength);
                    }
                    return status == StatusOk;
                }
                finally { ExitNative(); }
            }

            /// <summary>Drains render frames queued by TryEnqueueRenderFrame.</summary>
            public int DrainQueuedRenderFrames()
            {
                EnterNative();
                try { return DrainQueuedRenderFramesNoLock(); }
                finally { ExitNative(); }
            }

            public void ProcessCapture(
                float[] capture,
                int captureLength,
                float[] output,
                int outputLength,
                float[] linearOutput = null,
                int linearOutputLength = 0,
                bool inputVolumeChanged = false)
            {
                ValidateAecCaptureArguments(capture, captureLength, output, outputLength, linearOutput, linearOutputLength);
                EnterNative();
                try
                {
                    DrainQueuedRenderFramesNoLock();
                    using (PinnedFloatArray capturePin = new PinnedFloatArray(capture))
                    using (PinnedFloatArray outputPin = new PinnedFloatArray(output))
                    using (PinnedFloatArray linearPin = new PinnedFloatArray(linearOutput))
                    {
                        ThrowIfError(meta_aec3_aec_process_capture(
                            GetHandle(), capturePin.Pointer, captureLength, outputPin.Pointer, outputLength,
                            linearPin.Pointer, linearOutputLength, inputVolumeChanged ? 1 : 0, IntPtr.Zero));
                    }
                }
                finally { ExitNative(); }
            }

            public AecStats ProcessCaptureWithStats(
                float[] capture,
                int captureLength,
                float[] output,
                int outputLength,
                float[] linearOutput = null,
                int linearOutputLength = 0,
                bool inputVolumeChanged = false)
            {
                ValidateAecCaptureArguments(capture, captureLength, output, outputLength, linearOutput, linearOutputLength);
                EnterNative();
                try
                {
                    DrainQueuedRenderFramesNoLock();
                    AecStats stats;
                    using (PinnedFloatArray capturePin = new PinnedFloatArray(capture))
                    using (PinnedFloatArray outputPin = new PinnedFloatArray(output))
                    using (PinnedFloatArray linearPin = new PinnedFloatArray(linearOutput))
                    {
                        ThrowIfError(meta_aec3_aec_process_capture_with_stats(
                            GetHandle(), capturePin.Pointer, captureLength, outputPin.Pointer, outputLength,
                            linearPin.Pointer, linearOutputLength, inputVolumeChanged ? 1 : 0, out stats));
                    }
                    return stats;
                }
                finally { ExitNative(); }
            }

            public AecMetrics GetMetrics()
            {
                EnterNative();
                try
                {
                    AecMetrics metrics;
                    ThrowIfError(meta_aec3_aec_get_metrics(GetHandle(), out metrics));
                    return metrics;
                }
                finally { ExitNative(); }
            }

            public void SetStreamDelayMs(int delayMs)
            {
                ValidateNonNegative(delayMs, nameof(delayMs));
                EnterNative();
                try { ThrowIfError(meta_aec3_aec_set_stream_delay_ms(GetHandle(), delayMs)); }
                finally { ExitNative(); }
            }

            public void SetEchoLeakageDetected(bool detected)
            {
                EnterNative();
                try { ThrowIfError(meta_aec3_aec_set_echo_leakage_status(GetHandle(), detected ? 1 : 0)); }
                finally { ExitNative(); }
            }

            public void Reconfigure(AecConfig config)
            {
                ValidateAecConfig(config);
                if (config.SampleRateHz != sampleRateHz || config.RenderChannels != renderChannels || config.CaptureChannels != captureChannels)
                {
                    throw new InvalidOperationException("Reconfiguration that changes the 10-ms format requires a new EchoCanceller instance.");
                }
                EnterNative();
                try
                {
                    ThrowIfError(meta_aec3_aec_reconfigure(GetHandle(), ref config));
                    renderQueue.Clear();
                }
                finally { ExitNative(); }
            }

            public void Reset()
            {
                EnterNative();
                try
                {
                    ThrowIfError(meta_aec3_aec_reset(GetHandle()));
                    renderQueue.Clear();
                }
                finally { ExitNative(); }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }
                SpinWait wait = new SpinWait();
                while (Interlocked.CompareExchange(ref nativeGate, 1, 0) != 0)
                {
                    wait.SpinOnce();
                }
                try
                {
                    renderQueue.Clear();
                    handle.Dispose();
                }
                finally { ExitNative(); }
            }

            private void ProcessRenderDirectNoLock(float[] render, int renderLength)
            {
                using (PinnedFloatArray pin = new PinnedFloatArray(render))
                {
                    ThrowIfError(meta_aec3_aec_process_render(GetHandle(), pin.Pointer, renderLength));
                }
            }

            private int DrainQueuedRenderFramesNoLock()
            {
                int drained = 0;
                float[] frame;
                int length;
                while (renderQueue.TryPeek(out frame, out length))
                {
                    ProcessRenderDirectNoLock(frame, length);
                    renderQueue.Pop();
                    drained++;
                }
                return drained;
            }

            private void ValidateAecCaptureArguments(float[] capture, int captureLength, float[] output, int outputLength, float[] linearOutput, int linearOutputLength)
            {
                ValidateAecCapture(capture, captureLength, captureSamplesPer10Ms, nameof(capture));
                ValidateSamples(output, outputLength, nameof(output));
                if (ReferenceEquals(capture, output))
                {
                    throw new ArgumentException("AEC capture and output arrays must not overlap.", nameof(output));
                }
                if (outputLength < captureLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(outputLength), "AEC output must hold every capture sample.");
                }
                if (linearOutput == null)
                {
                    if (linearOutputLength != 0) throw new ArgumentOutOfRangeException(nameof(linearOutputLength));
                    return;
                }
                ValidateNonNegative(linearOutputLength, nameof(linearOutputLength));
                if (linearOutputLength > linearOutput.Length) throw new ArgumentOutOfRangeException(nameof(linearOutputLength));
                int expectedLinear = captureLength / captureSamplesPer10Ms * LinearTapSamplesPer10Ms;
                if (linearOutputLength < expectedLinear)
                {
                    throw new ArgumentOutOfRangeException(nameof(linearOutputLength), "AEC linear output is 16-kHz low-band audio.");
                }
            }

            private void EnterNative()
            {
                ThrowIfDisposed();
                SpinWait wait = new SpinWait();
                while (Interlocked.CompareExchange(ref nativeGate, 1, 0) != 0)
                {
                    wait.SpinOnce();
                }
                if (Volatile.Read(ref disposed) != 0)
                {
                    ExitNative();
                    throw new ObjectDisposedException(nameof(EchoCanceller));
                }
            }

            private bool TryEnterNative()
            {
                if (Volatile.Read(ref disposed) != 0 || Interlocked.CompareExchange(ref nativeGate, 1, 0) != 0)
                {
                    return false;
                }
                if (Volatile.Read(ref disposed) != 0)
                {
                    ExitNative();
                    return false;
                }
                return true;
            }

            private void ExitNative() { Volatile.Write(ref nativeGate, 0); }

            private AecSafeHandle GetHandle()
            {
                if (handle.IsClosed || handle.IsInvalid || Volatile.Read(ref disposed) != 0)
                {
                    throw new ObjectDisposedException(nameof(EchoCanceller));
                }
                return handle;
            }

            private void ThrowIfDisposed()
            {
                if (Volatile.Read(ref disposed) != 0 || handle.IsClosed || handle.IsInvalid)
                {
                    throw new ObjectDisposedException(nameof(EchoCanceller));
                }
            }
        }

        /// <summary>One thread-affine native AGC2 instance.</summary>
        public sealed class Agc2 : IDisposable
        {
            private readonly Agc2SafeHandle handle;

            public Agc2(Agc2Config config)
            {
                ValidateAgc2Config(config);
                handle = meta_aec3_agc2_create(ref config);
                if (handle == null || handle.IsInvalid)
                {
                    handle?.Dispose();
                    throw new InvalidOperationException("Failed to create native AGC2 instance.");
                }
            }

            public int SamplesPer10Ms { get { return GetPositiveResult(meta_aec3_agc2_samples_per_10ms(GetHandle())); } }

            public Agc2Config CurrentConfig
            {
                get
                {
                    Agc2Config config;
                    ThrowIfError(meta_aec3_agc2_get_config(GetHandle(), out config));
                    return config;
                }
            }

            public void Reconfigure(Agc2Config config)
            {
                ValidateAgc2Config(config);
                ThrowIfError(meta_aec3_agc2_reconfigure(GetHandle(), ref config));
            }

            public void Reset() { ThrowIfError(meta_aec3_agc2_reset(GetHandle())); }
            public void SetFixedGainDb(float gainDb) { ThrowIfError(meta_aec3_agc2_set_fixed_gain_db(GetHandle(), gainDb)); }
            public void SetCaptureOutputUsed(bool used) { ThrowIfError(meta_aec3_agc2_set_capture_output_used(GetHandle(), used ? 1 : 0)); }

            public void Process(float[] input, int inputLength, float[] output, int outputLength, int appliedInputVolume, bool inputVolumeChanged = false)
            {
                ValidateAgc2ProcessArguments(input, inputLength, output, outputLength, appliedInputVolume);
                using (PinnedFloatArray inputPin = new PinnedFloatArray(input))
                using (PinnedFloatArray outputPin = new PinnedFloatArray(output))
                {
                    ThrowIfError(meta_aec3_agc2_process(
                        GetHandle(), inputPin.Pointer, inputLength, outputPin.Pointer, outputLength,
                        appliedInputVolume, inputVolumeChanged ? 1 : 0, IntPtr.Zero));
                }
            }

            public Agc2Stats ProcessWithStats(float[] input, int inputLength, float[] output, int outputLength, int appliedInputVolume, bool inputVolumeChanged = false)
            {
                ValidateAgc2ProcessArguments(input, inputLength, output, outputLength, appliedInputVolume);
                Agc2Stats stats;
                using (PinnedFloatArray inputPin = new PinnedFloatArray(input))
                using (PinnedFloatArray outputPin = new PinnedFloatArray(output))
                {
                    ThrowIfError(meta_aec3_agc2_process_with_stats(
                        GetHandle(), inputPin.Pointer, inputLength, outputPin.Pointer, outputLength,
                        appliedInputVolume, inputVolumeChanged ? 1 : 0, out stats));
                }
                return stats;
            }

            public void Dispose() { handle.Dispose(); }

            private void ValidateAgc2ProcessArguments(float[] input, int inputLength, float[] output, int outputLength, int appliedInputVolume)
            {
                ValidateAecCapture(input, inputLength, SamplesPer10Ms, nameof(input));
                ValidateSamples(output, outputLength, nameof(output));
                if (ReferenceEquals(input, output)) throw new ArgumentException("AGC2 input and output arrays must not overlap.", nameof(output));
                if (outputLength < inputLength) throw new ArgumentOutOfRangeException(nameof(outputLength));
                if (appliedInputVolume < 0 || appliedInputVolume > 255) throw new ArgumentOutOfRangeException(nameof(appliedInputVolume));
            }

            private Agc2SafeHandle GetHandle()
            {
                if (handle.IsClosed || handle.IsInvalid) throw new ObjectDisposedException(nameof(Agc2));
                return handle;
            }
        }

        public static void ThrowIfError(int status)
        {
            if (status == StatusOk) return;
            switch (status)
            {
                case StatusNullPointer: throw new InvalidOperationException("Native AEC3 received a null or misaligned pointer.");
                case StatusInvalidConfig: throw new ArgumentException("Native AEC3 rejected the configuration.");
                case StatusInvalidArgument: throw new ArgumentException("Native AEC3 rejected an argument.");
                case StatusBufferTooSmall: throw new ArgumentException("A native output buffer is too small.");
                case StatusPanic: throw new InvalidOperationException("Native AEC3 caught a Rust panic.");
                default: throw new InvalidOperationException("Native AEC3 failed with status " + status + ".");
            }
        }

        private static int GetPositiveResult(int result)
        {
            // Frame-size exports return a positive sample count on success, unlike
            // the status-returning exports handled by ThrowIfError.
            if (result < StatusOk) ThrowIfError(result);
            if (result <= 0) throw new InvalidOperationException("Native AEC3 returned a non-positive frame size.");
            return result;
        }

        private static void ValidateNativeStructSize(Type type, int nativeSize)
        {
            int managedSize = Marshal.SizeOf(type);
            if (managedSize != nativeSize)
            {
                throw new TypeLoadException(type.Name + " is " + managedSize + " bytes in C#, but native AEC3 expects " + nativeSize + " bytes.");
            }
        }

        private static void ValidateHighPassConfig(HighPassConfig config)
        {
            ValidateRate(config.SampleRateHz, nameof(config.SampleRateHz));
            if (config.Channels < 1 || config.Channels > MaxRenderChannels) throw new ArgumentOutOfRangeException(nameof(config.Channels));
        }

        private static void ValidateAecConfig(AecConfig config)
        {
            ValidateRate(config.SampleRateHz, nameof(config.SampleRateHz));
            if (config.RenderChannels < 1 || config.RenderChannels > MaxRenderChannels) throw new ArgumentOutOfRangeException(nameof(config.RenderChannels));
            if (config.CaptureChannels < 1 || config.CaptureChannels > MaxCaptureChannels) throw new ArgumentOutOfRangeException(nameof(config.CaptureChannels));
            ValidateNonNegative(config.InitialDelayMs, nameof(config.InitialDelayMs));
            if (float.IsNaN(config.VadThreshold) || float.IsInfinity(config.VadThreshold) || config.VadThreshold < 0.0f || config.VadThreshold > 1.0f) throw new ArgumentOutOfRangeException(nameof(config.VadThreshold));
        }

        private static void ValidateAgc2Config(Agc2Config config)
        {
            ValidateRate(config.SampleRateHz, nameof(config.SampleRateHz));
            if (config.Channels < 1 || config.Channels > MaxRenderChannels) throw new ArgumentOutOfRangeException(nameof(config.Channels));
            if (float.IsNaN(config.FixedGainDb) || float.IsInfinity(config.FixedGainDb) || config.FixedGainDb < 0.0f || config.FixedGainDb >= 50.0f) throw new ArgumentOutOfRangeException(nameof(config.FixedGainDb));
        }

        private static bool IsValidAecRender(float[] samples, int samplesLength, int samplesPer10Ms)
        {
            return samples != null && samplesLength > 0 && samplesLength <= samples.Length && samplesLength % samplesPer10Ms == 0;
        }

        private static void ValidateAecRender(float[] samples, int samplesLength, int samplesPer10Ms)
        {
            ValidateSamples(samples, samplesLength, nameof(samples));
            if (samplesLength % samplesPer10Ms != 0) throw new ArgumentOutOfRangeException(nameof(samplesLength), "Audio must contain complete 10-ms frames.");
        }

        private static void ValidateAecCapture(float[] samples, int samplesLength, int samplesPer10Ms, string paramName)
        {
            ValidateSamples(samples, samplesLength, paramName);
            if (samplesLength % samplesPer10Ms != 0) throw new ArgumentOutOfRangeException(paramName, "Audio must contain complete 10-ms frames.");
        }

        private static void ValidateRate(int rate, string paramName)
        {
            if (rate != 16000 && rate != 32000 && rate != 48000) throw new ArgumentOutOfRangeException(paramName, "Native AEC3 supports 16000, 32000, and 48000 Hz.");
        }

        private static void ValidateSamples(float[] samples, int samplesLength, string paramName)
        {
            if (samples == null) throw new ArgumentNullException(paramName);
            if (samplesLength <= 0 || samplesLength > samples.Length) throw new ArgumentOutOfRangeException(paramName);
        }

        private static void ValidatePositive(int value, string paramName)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(paramName);
        }

        private static void ValidateNonNegative(int value, string paramName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(paramName);
        }

        private struct PinnedFloatArray : IDisposable
        {
            private GCHandle handle;
            public PinnedFloatArray(float[] array)
            {
                if (array == null)
                {
                    handle = default(GCHandle);
                    Pointer = IntPtr.Zero;
                    return;
                }
                handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                Pointer = handle.AddrOfPinnedObject();
            }
            public IntPtr Pointer { get; private set; }
            public void Dispose()
            {
                if (handle.IsAllocated) handle.Free();
                Pointer = IntPtr.Zero;
            }
        }

        private sealed class RenderFrameQueue
        {
            private readonly float[][] frames;
            private readonly int[] lengths;
            private int readIndex;
            private int writeIndex;
            private long droppedFrames;

            public RenderFrameQueue(int capacity, int samplesPerFrame)
            {
                frames = new float[capacity + 1][];
                lengths = new int[capacity + 1];
                for (int i = 0; i < frames.Length; i++) frames[i] = new float[samplesPerFrame];
            }

            public int Count
            {
                get
                {
                    int read = Volatile.Read(ref readIndex);
                    int write = Volatile.Read(ref writeIndex);
                    return write >= read ? write - read : frames.Length - read + write;
                }
            }

            public long DroppedFrames { get { return Interlocked.Read(ref droppedFrames); } }

            public bool TryEnqueue(float[] samples, int length)
            {
                int write = Volatile.Read(ref writeIndex);
                int next = Next(write);
                if (next == Volatile.Read(ref readIndex))
                {
                    Interlocked.Increment(ref droppedFrames);
                    return false;
                }
                Array.Copy(samples, 0, frames[write], 0, length);
                lengths[write] = length;
                Volatile.Write(ref writeIndex, next);
                return true;
            }

            public bool TryPeek(out float[] frame, out int length)
            {
                int read = Volatile.Read(ref readIndex);
                if (read == Volatile.Read(ref writeIndex))
                {
                    frame = null;
                    length = 0;
                    return false;
                }
                frame = frames[read];
                length = lengths[read];
                return true;
            }

            public void Pop()
            {
                int read = Volatile.Read(ref readIndex);
                if (read != Volatile.Read(ref writeIndex)) Volatile.Write(ref readIndex, Next(read));
            }

            public void Clear() { Volatile.Write(ref readIndex, Volatile.Read(ref writeIndex)); }
            private int Next(int index) { index++; return index == frames.Length ? 0 : index; }
        }

        private sealed class HighPassSafeHandle : SafeHandle
        {
            private HighPassSafeHandle() : base(IntPtr.Zero, true) { }
            public override bool IsInvalid { get { return handle == IntPtr.Zero; } }
            protected override bool ReleaseHandle() { meta_aec3_high_pass_free(handle); return true; }
        }

        private sealed class AecSafeHandle : SafeHandle
        {
            private AecSafeHandle() : base(IntPtr.Zero, true) { }
            public override bool IsInvalid { get { return handle == IntPtr.Zero; } }
            protected override bool ReleaseHandle() { meta_aec3_aec_free(handle); return true; }
        }

        private sealed class Agc2SafeHandle : SafeHandle
        {
            private Agc2SafeHandle() : base(IntPtr.Zero, true) { }
            public override bool IsInvalid { get { return handle == IntPtr.Zero; } }
            protected override bool ReleaseHandle() { meta_aec3_agc2_free(handle); return true; }
        }

        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_default_config", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_high_pass_default_config(out HighPassConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_create", CallingConvention = CallingConvention.Cdecl)] private static extern HighPassSafeHandle meta_aec3_high_pass_create(ref HighPassConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_free", CallingConvention = CallingConvention.Cdecl)] private static extern void meta_aec3_high_pass_free(IntPtr handle);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_get_config", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_high_pass_get_config(HighPassSafeHandle handle, out HighPassConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_reconfigure", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_high_pass_reconfigure(HighPassSafeHandle handle, ref HighPassConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_reset", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_high_pass_reset(HighPassSafeHandle handle);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_process", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_high_pass_process(HighPassSafeHandle handle, IntPtr audio, int length, IntPtr stats);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_process", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_high_pass_process_with_stats(HighPassSafeHandle handle, IntPtr audio, int length, out HighPassStats stats);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_high_pass_samples_per_10ms", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_high_pass_samples_per_10ms(HighPassSafeHandle handle);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_default_config", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_default_config(out AecConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_create", CallingConvention = CallingConvention.Cdecl)] private static extern AecSafeHandle meta_aec3_aec_create(ref AecConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_free", CallingConvention = CallingConvention.Cdecl)] private static extern void meta_aec3_aec_free(IntPtr handle);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_get_config", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_get_config(AecSafeHandle handle, out AecConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_reconfigure", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_reconfigure(AecSafeHandle handle, ref AecConfig config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_reset", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_reset(AecSafeHandle handle);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_set_stream_delay_ms", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_set_stream_delay_ms(AecSafeHandle handle, int delayMs);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_set_echo_leakage_status", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_set_echo_leakage_status(AecSafeHandle handle, int detected);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_process_render", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_process_render(AecSafeHandle handle, IntPtr render, int length);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_process_capture", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_process_capture(AecSafeHandle handle, IntPtr capture, int captureLength, IntPtr output, int outputLength, IntPtr linearOutput, int linearOutputLength, int inputVolumeChanged, IntPtr stats);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_process_capture", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_process_capture_with_stats(AecSafeHandle handle, IntPtr capture, int captureLength, IntPtr output, int outputLength, IntPtr linearOutput, int linearOutputLength, int inputVolumeChanged, out AecStats stats);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_get_metrics", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_get_metrics(AecSafeHandle handle, out AecMetrics metrics);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_render_samples_per_10ms", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_render_samples_per_10ms(AecSafeHandle handle);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_aec_capture_samples_per_10ms", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_aec_capture_samples_per_10ms(AecSafeHandle handle);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_default_config", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_default_config(out Agc2Config config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_create", CallingConvention = CallingConvention.Cdecl)] private static extern Agc2SafeHandle meta_aec3_agc2_create(ref Agc2Config config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_free", CallingConvention = CallingConvention.Cdecl)] private static extern void meta_aec3_agc2_free(IntPtr handle);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_get_config", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_get_config(Agc2SafeHandle handle, out Agc2Config config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_reconfigure", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_reconfigure(Agc2SafeHandle handle, ref Agc2Config config);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_reset", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_reset(Agc2SafeHandle handle);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_set_fixed_gain_db", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_set_fixed_gain_db(Agc2SafeHandle handle, float gainDb);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_set_capture_output_used", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_set_capture_output_used(Agc2SafeHandle handle, int used);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_process", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_process(Agc2SafeHandle handle, IntPtr input, int inputLength, IntPtr output, int outputLength, int appliedInputVolume, int inputVolumeChanged, IntPtr stats);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_process", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_process_with_stats(Agc2SafeHandle handle, IntPtr input, int inputLength, IntPtr output, int outputLength, int appliedInputVolume, int inputVolumeChanged, out Agc2Stats stats);
        [DllImport(LibraryName, EntryPoint = "meta_aec3_agc2_samples_per_10ms", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_agc2_samples_per_10ms(Agc2SafeHandle handle);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_sizeof_high_pass_stats", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_sizeof_high_pass_stats();
        [DllImport(LibraryName, EntryPoint = "meta_aec3_sizeof_aec_stats", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_sizeof_aec_stats();
        [DllImport(LibraryName, EntryPoint = "meta_aec3_sizeof_agc2_stats", CallingConvention = CallingConvention.Cdecl)] private static extern int meta_aec3_sizeof_agc2_stats();
    }
}
