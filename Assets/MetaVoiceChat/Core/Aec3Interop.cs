#nullable disable

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MetaVoiceChat.Core
{
    public static class Aec3Interop
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string LibraryName = "__Internal";
#else
        private const string LibraryName = "meta_voice_chat_aec3";
#endif

        public const int StatusOk = 0;
        public const int StatusNeedsRnnoise = 1;
        public const int StatusNullPointer = -1;
        public const int StatusInvalidConfig = -2;
        public const int StatusInvalidArgument = -3;
        public const int StatusBufferTooSmall = -4;
        public const int StatusNoPendingRnnoise = -5;
        public const int StatusPanic = -99;
        public const int StatusBusy = -1000;

        private const int MaxRenderChannels = 8;
        private const int MaxRenderSamples = 48000 * 40 / 1000 * MaxRenderChannels;

#pragma warning disable 0169
        private static IntPtr loadedNativeLibrary;
#pragma warning restore 0169

        public enum NoiseSuppressionMode
        {
            None = 0,
            WebRtc = 1,
            Rnnoise = 2
        }

        public enum NoiseSuppressionLevel
        {
            Db6 = 0,
            Db12 = 1,
            Db18 = 2,
            Db21 = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Config
        {
            public int SampleRateHz;
            public int RenderSampleRateHz;
            public int FrameSizeMs;
            public int CaptureChannels;
            public int RenderChannels;
            public int EnableHighPassFilter;
            public int EnableAec3;
            public int NoiseSuppressionMode;
            public int NoiseSuppressionLevel;
            public int EnableAgc2;
            public float Agc2FixedGainDb;
            public int Agc2AdaptiveDigital;
            public int Agc2InputVolumeController;
            public int AppliedInputVolume;
            public int CaptureOutputUsed;
            public float UserMicrophoneGain;
            public int EnablePostLimiter;
            public int InitialDelayMs;
            public float VadThreshold;
            public int ExportLinearAecOutput;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeStats
        {
            public int StructSize;
            public int Status;
            public int ProcessedSamples;
            public int OutputSamples;
            public int AecTapSamples;
            public int RnnoiseInputSamples;
            public int Speech16kSamples;
            public int SampleRateHz;
            public int FrameSizeMs;
            public int CaptureChannels;
            public int RenderChannels;
            public int InternalSampleRateHz;
            public int AecEnabled;
            public int HighPassEnabled;
            public int NoiseSuppressionMode;
            public float VadProbability;
            public int VadIsVoice;
            public float Rms;
            public float Peak;
            public int RecommendedInputVolume;
            public int PostLimiterApplied;
            public double EchoReturnLoss;
            public double EchoReturnLossEnhancement;
            public int DelayMs;
            public int RenderJitterMin;
            public int RenderJitterMax;
            public int CaptureJitterMin;
            public int CaptureJitterMax;
            public IntPtr FftMagnitudes;
            public int FftCapacity;
            public int FftBinsWritten;
            public int FftSize;
            public int FftSampleRateHz;
        }

        public sealed class StatsBuffer : IDisposable
        {
            private readonly IntPtr nativeStatsPtr;
            private readonly IntPtr fftPtr;
            private readonly int fftCapacity;
            private bool disposed;

            public StatsBuffer(int fftCapacity)
            {
                if (fftCapacity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(fftCapacity), "FFT capacity cannot be negative.");
                }

                this.fftCapacity = fftCapacity;
                nativeStatsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeStats)));
                fftPtr = fftCapacity > 0
                    ? Marshal.AllocHGlobal(sizeof(float) * fftCapacity)
                    : IntPtr.Zero;

                FftMagnitudes = new float[fftCapacity];
                Native = new NativeStats();
                PrepareForNative();
            }

            ~StatsBuffer()
            {
                Dispose(false);
            }

            public NativeStats Native { get; private set; }

            public float[] FftMagnitudes { get; private set; }

            internal IntPtr NativePointer
            {
                get
                {
                    ThrowIfDisposed();
                    return nativeStatsPtr;
                }
            }

            internal void PrepareForNative()
            {
                ThrowIfDisposed();

                NativeStats stats = new NativeStats
                {
                    StructSize = Marshal.SizeOf(typeof(NativeStats)),
                    FftMagnitudes = fftPtr,
                    FftCapacity = fftCapacity
                };

                Marshal.StructureToPtr(stats, nativeStatsPtr, false);
            }

            internal void RefreshFromNative()
            {
                ThrowIfDisposed();

                Native = (NativeStats)Marshal.PtrToStructure(nativeStatsPtr, typeof(NativeStats));
                int binsToCopy = Native.FftBinsWritten;
                if (binsToCopy < 0)
                {
                    binsToCopy = 0;
                }

                if (binsToCopy > fftCapacity)
                {
                    binsToCopy = fftCapacity;
                }

                if (binsToCopy > 0 && fftPtr != IntPtr.Zero)
                {
                    Marshal.Copy(fftPtr, FftMagnitudes, 0, binsToCopy);
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (disposed)
                {
                    return;
                }

                if (fftPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(fftPtr);
                }

                if (nativeStatsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(nativeStatsPtr);
                }

                disposed = true;
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(StatsBuffer));
                }
            }
        }

        public sealed class Instance : IDisposable
        {
            private readonly RenderFrameQueue renderQueue;
            private IntPtr ptr;
            private int nativeGate;
            private int captureSamplesPerFrame;
            private int outputSamplesPerFrame;
            private int rnnoiseSamplesPerFrame;
            private int speech16kSamplesPerFrame;
            private int fftBinsPerFrame;
            private int renderSampleRateHz;
            private int frameSizeMs;

            public Instance(Config config, int renderQueueCapacity)
            {
                ValidateConfig(config);
                ValidatePositive(renderQueueCapacity, nameof(renderQueueCapacity));

                renderQueue = new RenderFrameQueue(renderQueueCapacity, MaxRenderSamples);
                ptr = meta_aec3_create(ref config);
                if (ptr == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create native AEC3 instance.");
                }

                RefreshCachedSizes(ptr, config);
            }

            public Instance(Config config)
                : this(config, 8)
            {
            }

            ~Instance()
            {
                Dispose(false);
            }

            public IntPtr NativePtr
            {
                get
                {
                    return GetLivePtr();
                }
            }

            public int CaptureSamplesPerFrame
            {
                get { return Volatile.Read(ref captureSamplesPerFrame); }
            }

            public int OutputSamplesPerFrame
            {
                get { return Volatile.Read(ref outputSamplesPerFrame); }
            }

            public int RnnoiseSamplesPerFrame
            {
                get { return Volatile.Read(ref rnnoiseSamplesPerFrame); }
            }

            public int Speech16kSamplesPerFrame
            {
                get { return Volatile.Read(ref speech16kSamplesPerFrame); }
            }

            public int FftBinsPerFrame
            {
                get { return Volatile.Read(ref fftBinsPerFrame); }
            }

            public int QueuedRenderFrames
            {
                get { return renderQueue.Count; }
            }

            public long DroppedRenderFrames
            {
                get { return renderQueue.DroppedFrames; }
            }

            public Config CurrentConfig
            {
                get
                {
                    IntPtr nativePtr = EnterNative();
                    try
                    {
                        Config config;
                        ThrowIfError(meta_aec3_get_config(nativePtr, out config));
                        return config;
                    }
                    finally
                    {
                        ExitNative();
                    }
                }
            }

            public void Configure(Config config)
            {
                ValidateConfig(config);
                IntPtr nativePtr = EnterNative();
                try
                {
                    ThrowIfError(meta_aec3_configure(nativePtr, ref config));
                    renderQueue.Clear();
                    RefreshCachedSizes(nativePtr, config);
                }
                finally
                {
                    ExitNative();
                }
            }

            public void Reset()
            {
                IntPtr nativePtr = EnterNative();
                try
                {
                    ThrowIfError(meta_aec3_reset(nativePtr));
                    renderQueue.Clear();
                }
                finally
                {
                    ExitNative();
                }
            }

            public void SetStreamDelayMs(int delayMs)
            {
                ValidateNonNegative(delayMs, nameof(delayMs));
                IntPtr nativePtr = EnterNative();
                try
                {
                    ThrowIfError(meta_aec3_set_stream_delay_ms(nativePtr, delayMs));
                }
                finally
                {
                    ExitNative();
                }
            }

            public void SetUserMicrophoneGain(float gain)
            {
                if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0.0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(gain), "Gain must be finite and non-negative.");
                }

                IntPtr nativePtr = EnterNative();
                try
                {
                    ThrowIfError(meta_aec3_set_user_microphone_gain(nativePtr, gain));
                }
                finally
                {
                    ExitNative();
                }
            }

            public bool TryEnqueueRender(float[] samples, int samplesLen, int channels)
            {
                if (Volatile.Read(ref ptr) == IntPtr.Zero)
                {
                    return false;
                }

                if (!IsValidRenderFrame(samples, samplesLen, channels))
                {
                    return false;
                }

                return renderQueue.TryEnqueue(samples, samplesLen, channels);
            }

            public void EnqueueRender(float[] samples, int samplesLen, int channels)
            {
                ValidateRenderFrame(samples, samplesLen, channels);
                if (!renderQueue.TryEnqueue(samples, samplesLen, channels))
                {
                    throw new InvalidOperationException("The AEC3 render queue is full.");
                }
            }

            public int DrainQueuedRenderFrames()
            {
                IntPtr nativePtr = EnterNative();
                try
                {
                    return DrainQueuedRenderFramesNoLock(nativePtr);
                }
                finally
                {
                    ExitNative();
                }
            }

            public int ProcessRender(float[] samples, int samplesLen, int channels)
            {
                ValidateRenderFrame(samples, samplesLen, channels);
                IntPtr nativePtr = EnterNative();
                try
                {
                    int status;
                    using (PinnedFloatArray samplesPin = new PinnedFloatArray(samples))
                    {
                        status = meta_aec3_process_render(nativePtr, samplesPin.Pointer, samplesLen, channels);
                    }

                    ThrowIfError(status);
                    return status;
                }
                finally
                {
                    ExitNative();
                }
            }

            public bool TryProcessRender(float[] samples, int samplesLen, int channels, out int status)
            {
                status = StatusBusy;
                if (!IsValidRenderFrame(samples, samplesLen, channels))
                {
                    status = StatusInvalidArgument;
                    return false;
                }

                IntPtr nativePtr;
                if (!TryEnterNative(out nativePtr))
                {
                    return false;
                }

                try
                {
                    using (PinnedFloatArray samplesPin = new PinnedFloatArray(samples))
                    {
                        status = meta_aec3_process_render(nativePtr, samplesPin.Pointer, samplesLen, channels);
                    }

                    return status >= StatusOk;
                }
                finally
                {
                    ExitNative();
                }
            }

            public int ProcessCapture(
                float[] captureSamples,
                int captureSamplesLen,
                float[] outputSamples,
                int outputSamplesLen,
                float[] aecTapSamples,
                int aecTapSamplesLen,
                float[] rnnoiseInputSamples,
                int rnnoiseInputSamplesLen,
                float[] speech16kSamples,
                int speech16kSamplesLen,
                StatsBuffer stats)
            {
                ValidateSamples(captureSamples, captureSamplesLen, nameof(captureSamples));
                ValidateOptionalBuffer(outputSamples, outputSamplesLen, nameof(outputSamples));
                ValidateOptionalBuffer(aecTapSamples, aecTapSamplesLen, nameof(aecTapSamples));
                ValidateOptionalBuffer(rnnoiseInputSamples, rnnoiseInputSamplesLen, nameof(rnnoiseInputSamples));
                ValidateOptionalBuffer(speech16kSamples, speech16kSamplesLen, nameof(speech16kSamples));

                IntPtr nativePtr = EnterNative();
                try
                {
                    DrainQueuedRenderFramesNoLock(nativePtr);
                    int status;
                    if (stats != null)
                    {
                        stats.PrepareForNative();
                    }

                    using (PinnedFloatArray capturePin = new PinnedFloatArray(captureSamples))
                    using (PinnedFloatArray outputPin = new PinnedFloatArray(outputSamples))
                    using (PinnedFloatArray aecTapPin = new PinnedFloatArray(aecTapSamples))
                    using (PinnedFloatArray rnnoisePin = new PinnedFloatArray(rnnoiseInputSamples))
                    using (PinnedFloatArray speech16kPin = new PinnedFloatArray(speech16kSamples))
                    {
                        status = meta_aec3_process_capture(
                            nativePtr,
                            capturePin.Pointer,
                            captureSamplesLen,
                            outputPin.Pointer,
                            outputSamplesLen,
                            aecTapPin.Pointer,
                            aecTapSamplesLen,
                            rnnoisePin.Pointer,
                            rnnoiseInputSamplesLen,
                            speech16kPin.Pointer,
                            speech16kSamplesLen,
                            stats != null ? stats.NativePointer : IntPtr.Zero);
                    }

                    if (stats != null)
                    {
                        stats.RefreshFromNative();
                    }

                    ThrowIfError(status);
                    return status;
                }
                finally
                {
                    ExitNative();
                }
            }

            public int ProcessCapture(
                float[] captureSamples,
                float[] outputSamples,
                StatsBuffer stats)
            {
                return ProcessCapture(
                    captureSamples,
                    captureSamples != null ? captureSamples.Length : 0,
                    outputSamples,
                    outputSamples != null ? outputSamples.Length : 0,
                    null,
                    0,
                    null,
                    0,
                    null,
                    0,
                    stats);
            }

            public int FinishRnnoiseFrame(
                float[] rnnoiseOutputSamples,
                int rnnoiseOutputSamplesLen,
                float[] outputSamples,
                int outputSamplesLen,
                StatsBuffer stats)
            {
                ValidateSamples(rnnoiseOutputSamples, rnnoiseOutputSamplesLen, nameof(rnnoiseOutputSamples));
                ValidateSamples(outputSamples, outputSamplesLen, nameof(outputSamples));

                IntPtr nativePtr = EnterNative();
                try
                {
                    int status;
                    if (stats != null)
                    {
                        stats.PrepareForNative();
                    }

                    using (PinnedFloatArray rnnoisePin = new PinnedFloatArray(rnnoiseOutputSamples))
                    using (PinnedFloatArray outputPin = new PinnedFloatArray(outputSamples))
                    {
                        status = meta_aec3_finish_rnnoise_frame(
                            nativePtr,
                            rnnoisePin.Pointer,
                            rnnoiseOutputSamplesLen,
                            outputPin.Pointer,
                            outputSamplesLen,
                            stats != null ? stats.NativePointer : IntPtr.Zero);
                    }

                    if (stats != null)
                    {
                        stats.RefreshFromNative();
                    }

                    ThrowIfError(status);
                    return status;
                }
                finally
                {
                    ExitNative();
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                IntPtr nativePtr;
                if (!TryEnterNativeForDispose(out nativePtr))
                {
                    return;
                }

                try
                {
                    renderQueue.Clear();
                    meta_aec3_free(nativePtr);
                }
                finally
                {
                    ExitNative();
                }
            }

            private int DrainQueuedRenderFramesNoLock(IntPtr nativePtr)
            {
                int drained = 0;
                RenderFrame frame;
                while (renderQueue.TryPeek(out frame))
                {
                    int status;
                    using (PinnedFloatArray samplesPin = new PinnedFloatArray(frame.Samples))
                    {
                        status = meta_aec3_process_render(
                            nativePtr,
                            samplesPin.Pointer,
                            frame.SamplesLen,
                            frame.Channels);
                    }

                    renderQueue.Pop();
                    ThrowIfError(status);
                    drained++;
                }

                return drained;
            }

            private void RefreshCachedSizes(IntPtr nativePtr, Config config)
            {
                int capture = meta_aec3_capture_samples_per_frame(nativePtr);
                int output = meta_aec3_output_samples_per_frame(nativePtr);
                int rnnoise = meta_aec3_rnnoise_samples_per_frame(nativePtr);
                int speech16k = meta_aec3_speech_16k_samples_per_frame(nativePtr);
                int fftBins = meta_aec3_fft_bins_per_frame(nativePtr);

                ThrowIfError(capture);
                ThrowIfError(output);
                ThrowIfError(rnnoise);
                ThrowIfError(speech16k);
                ThrowIfError(fftBins);

                Volatile.Write(ref captureSamplesPerFrame, capture);
                Volatile.Write(ref outputSamplesPerFrame, output);
                Volatile.Write(ref rnnoiseSamplesPerFrame, rnnoise);
                Volatile.Write(ref speech16kSamplesPerFrame, speech16k);
                Volatile.Write(ref fftBinsPerFrame, fftBins);
                Volatile.Write(ref renderSampleRateHz, config.RenderSampleRateHz > 0 ? config.RenderSampleRateHz : config.SampleRateHz);
                Volatile.Write(ref frameSizeMs, config.FrameSizeMs);
            }

            private IntPtr EnterNative()
            {
                SpinWait wait = new SpinWait();
                while (Interlocked.CompareExchange(ref nativeGate, 1, 0) != 0)
                {
                    wait.SpinOnce();
                }

                IntPtr nativePtr = Volatile.Read(ref ptr);
                if (nativePtr == IntPtr.Zero)
                {
                    ExitNative();
                    throw new ObjectDisposedException(nameof(Instance));
                }

                return nativePtr;
            }

            private bool TryEnterNative(out IntPtr nativePtr)
            {
                nativePtr = IntPtr.Zero;
                if (Interlocked.CompareExchange(ref nativeGate, 1, 0) != 0)
                {
                    return false;
                }

                nativePtr = Volatile.Read(ref ptr);
                if (nativePtr == IntPtr.Zero)
                {
                    ExitNative();
                    return false;
                }

                return true;
            }

            private bool TryEnterNativeForDispose(out IntPtr nativePtr)
            {
                SpinWait wait = new SpinWait();
                while (Interlocked.CompareExchange(ref nativeGate, 1, 0) != 0)
                {
                    wait.SpinOnce();
                }

                nativePtr = Interlocked.Exchange(ref ptr, IntPtr.Zero);
                if (nativePtr == IntPtr.Zero)
                {
                    ExitNative();
                    return false;
                }

                return true;
            }

            private void ExitNative()
            {
                Volatile.Write(ref nativeGate, 0);
            }

            private IntPtr GetLivePtr()
            {
                IntPtr nativePtr = Volatile.Read(ref ptr);
                if (nativePtr == IntPtr.Zero)
                {
                    throw new ObjectDisposedException(nameof(Instance));
                }

                return nativePtr;
            }

            private bool IsValidRenderFrame(float[] samples, int samplesLen, int channels)
            {
                if (samples == null || channels < 1 || channels > MaxRenderChannels)
                {
                    return false;
                }

                if (samplesLen <= 0 || samplesLen > samples.Length || samplesLen > MaxRenderSamples)
                {
                    return false;
                }

                int rate = Volatile.Read(ref renderSampleRateHz);
                int frameMs = Volatile.Read(ref frameSizeMs);
                int expected = rate * frameMs / 1000 * channels;
                return samplesLen == expected;
            }

            private void ValidateRenderFrame(float[] samples, int samplesLen, int channels)
            {
                ValidateSamples(samples, samplesLen, nameof(samples));
                if (channels < 1 || channels > MaxRenderChannels)
                {
                    throw new ArgumentOutOfRangeException(nameof(channels), "Render channels must be in [1, 8].");
                }

                int rate = Volatile.Read(ref renderSampleRateHz);
                int frameMs = Volatile.Read(ref frameSizeMs);
                int expected = rate * frameMs / 1000 * channels;
                if (samplesLen != expected)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(samplesLen),
                        $"Expected {expected} render samples for {frameMs} ms at {rate} Hz with {channels} channels.");
                }
            }
        }

        public static Config CreateDefaultConfig()
        {
            Config config;
            ThrowIfError(meta_aec3_default_config(out config));
            return config;
        }

        public static Instance Create(Config config)
        {
            return new Instance(config);
        }

        public static Instance Create(Config config, int renderQueueCapacity)
        {
            return new Instance(config, renderQueueCapacity);
        }

        public static int NativeStatusOk()
        {
            return meta_aec3_status_ok();
        }

        public static int NativeStatusNeedsRnnoise()
        {
            return meta_aec3_status_needs_rnnoise();
        }

        public static void ThrowIfError(int status)
        {
            if (status >= StatusOk)
            {
                return;
            }

            switch (status)
            {
                case StatusNullPointer:
                    throw new InvalidOperationException("Native AEC3 received a null pointer.");
                case StatusInvalidConfig:
                    throw new ArgumentException("Native AEC3 rejected the configuration.");
                case StatusInvalidArgument:
                    throw new ArgumentException("Native AEC3 rejected an argument.");
                case StatusBufferTooSmall:
                    throw new ArgumentException("A buffer passed to native AEC3 is too small.");
                case StatusNoPendingRnnoise:
                    throw new InvalidOperationException("No RNNoise frame is pending in native AEC3.");
                case StatusPanic:
                    throw new InvalidOperationException("Native AEC3 caught a Rust panic.");
                case StatusBusy:
                    throw new InvalidOperationException("Native AEC3 instance is busy.");
                default:
                    throw new InvalidOperationException($"Native AEC3 failed with status {status}.");
            }
        }

        public static void PreloadNativeLibrary(string assetsPath)
        {
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !UNITY_IOS
            if (Volatile.Read(ref loadedNativeLibrary) != IntPtr.Zero)
            {
                return;
            }

            string architecture =
#if UNITY_EDITOR
                IntPtr.Size == 8 ? "x86_64" : "x86";
#elif UNITY_64
                "x86_64";
#else
                "x86";
#endif

            string pluginPath = Path.Combine(
                assetsPath,
                "MetaVoiceChat",
                "Core",
                "Plugins",
                "meta-voice-chat-aec3",
                "Windows",
                architecture,
                "meta_voice_chat_aec3.dll");

            if (!File.Exists(pluginPath))
            {
                return;
            }

            IntPtr handle = LoadLibrary(pluginPath);
            if (handle == IntPtr.Zero)
            {
                throw new DllNotFoundException(
                    $"Failed to load '{pluginPath}'. Windows error {Marshal.GetLastWin32Error()}.");
            }

            IntPtr previous = Interlocked.CompareExchange(ref loadedNativeLibrary, handle, IntPtr.Zero);
            if (previous != IntPtr.Zero)
            {
                FreeLibrary(handle);
            }
#else
            _ = assetsPath;
#endif
        }

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !UNITY_IOS
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);
#endif

        [DllImport(LibraryName, EntryPoint = "meta_aec3_default_config", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_default_config(out Config config);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_create", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr meta_aec3_create(ref Config config);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void meta_aec3_free(IntPtr processor);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_configure", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_configure(IntPtr processor, ref Config config);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_get_config", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_get_config(IntPtr processor, out Config config);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_reset", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_reset(IntPtr processor);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_set_stream_delay_ms", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_set_stream_delay_ms(IntPtr processor, int delayMs);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_set_user_microphone_gain", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_set_user_microphone_gain(IntPtr processor, float gain);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_capture_samples_per_frame", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_capture_samples_per_frame(IntPtr processor);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_output_samples_per_frame", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_output_samples_per_frame(IntPtr processor);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_rnnoise_samples_per_frame", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_rnnoise_samples_per_frame(IntPtr processor);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_speech_16k_samples_per_frame", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_speech_16k_samples_per_frame(IntPtr processor);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_fft_bins_per_frame", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_fft_bins_per_frame(IntPtr processor);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_process_render", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_process_render(
            IntPtr processor,
            IntPtr samples,
            int samplesLen,
            int channels);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_process_capture", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_process_capture(
            IntPtr processor,
            IntPtr captureSamples,
            int captureSamplesLen,
            IntPtr outputSamples,
            int outputSamplesLen,
            IntPtr aecTapSamples,
            int aecTapSamplesLen,
            IntPtr rnnoiseInputSamples,
            int rnnoiseInputSamplesLen,
            IntPtr speech16kSamples,
            int speech16kSamplesLen,
            IntPtr stats);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_finish_rnnoise_frame", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_finish_rnnoise_frame(
            IntPtr processor,
            IntPtr rnnoiseOutputSamples,
            int rnnoiseOutputSamplesLen,
            IntPtr outputSamples,
            int outputSamplesLen,
            IntPtr stats);

        [DllImport(LibraryName, EntryPoint = "meta_aec3_status_ok", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_status_ok();

        [DllImport(LibraryName, EntryPoint = "meta_aec3_status_needs_rnnoise", CallingConvention = CallingConvention.Cdecl)]
        private static extern int meta_aec3_status_needs_rnnoise();

        private static void ValidateConfig(Config config)
        {
            ValidateRate(config.SampleRateHz, nameof(config.SampleRateHz));
            if (config.RenderSampleRateHz > 0)
            {
                ValidateRate(config.RenderSampleRateHz, nameof(config.RenderSampleRateHz));
            }

            if (config.FrameSizeMs != 10 && config.FrameSizeMs != 20 && config.FrameSizeMs != 40)
            {
                throw new ArgumentOutOfRangeException(nameof(config.FrameSizeMs), "Frame size must be 10, 20, or 40 ms.");
            }

            if (config.CaptureChannels != 1 && config.CaptureChannels != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(config.CaptureChannels), "Capture channels must be mono or stereo.");
            }

            if (config.RenderChannels < 1 || config.RenderChannels > MaxRenderChannels)
            {
                throw new ArgumentOutOfRangeException(nameof(config.RenderChannels), "Render channels must be in [1, 8].");
            }

            if (config.NoiseSuppressionMode < 0 || config.NoiseSuppressionMode > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(config.NoiseSuppressionMode));
            }

            if (config.NoiseSuppressionLevel < 0 || config.NoiseSuppressionLevel > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(config.NoiseSuppressionLevel));
            }

            if (float.IsNaN(config.Agc2FixedGainDb) || float.IsInfinity(config.Agc2FixedGainDb) ||
                config.Agc2FixedGainDb < 0.0f || config.Agc2FixedGainDb >= 50.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(config.Agc2FixedGainDb));
            }

            if (float.IsNaN(config.UserMicrophoneGain) || float.IsInfinity(config.UserMicrophoneGain) ||
                config.UserMicrophoneGain < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(config.UserMicrophoneGain));
            }

            if (float.IsNaN(config.VadThreshold) || float.IsInfinity(config.VadThreshold) ||
                config.VadThreshold < 0.0f || config.VadThreshold > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(config.VadThreshold));
            }
        }

        private static void ValidateRate(int rate, string paramName)
        {
            if (rate != 8000 && rate != 12000 && rate != 16000 && rate != 24000 && rate != 48000)
            {
                throw new ArgumentOutOfRangeException(paramName, "Sample rate must be 8000, 12000, 16000, 24000, or 48000 Hz.");
            }
        }

        private static void ValidateSamples(float[] samples, int samplesLen, string paramName)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(paramName);
            }

            ValidatePositive(samplesLen, paramName + "Len");
            if (samplesLen > samples.Length)
            {
                throw new ArgumentOutOfRangeException(paramName, "Sample length cannot exceed the array length.");
            }
        }

        private static void ValidateOptionalBuffer(float[] samples, int samplesLen, string paramName)
        {
            if (samples == null)
            {
                if (samplesLen != 0)
                {
                    throw new ArgumentOutOfRangeException(paramName, "Null optional buffers must use length 0.");
                }

                return;
            }

            ValidateNonNegative(samplesLen, paramName + "Len");
            if (samplesLen > samples.Length)
            {
                throw new ArgumentOutOfRangeException(paramName, "Sample length cannot exceed the array length.");
            }
        }

        private static void ValidatePositive(int value, string paramName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Value must be positive.");
            }
        }

        private static void ValidateNonNegative(int value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Value cannot be negative.");
            }
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
                if (handle.IsAllocated)
                {
                    handle.Free();
                }

                Pointer = IntPtr.Zero;
            }
        }

        private sealed class RenderFrame
        {
            public readonly float[] Samples;
            public int SamplesLen;
            public int Channels;

            public RenderFrame(int maxSamples)
            {
                Samples = new float[maxSamples];
            }
        }

        private sealed class RenderFrameQueue
        {
            private readonly RenderFrame[] frames;
            private int readIndex;
            private int writeIndex;
            private long droppedFrames;

            public RenderFrameQueue(int capacity, int maxSamples)
            {
                frames = new RenderFrame[capacity + 1];
                for (int i = 0; i < frames.Length; i++)
                {
                    frames[i] = new RenderFrame(maxSamples);
                }
            }

            public int Count
            {
                get
                {
                    int read = Volatile.Read(ref readIndex);
                    int write = Volatile.Read(ref writeIndex);
                    if (write >= read)
                    {
                        return write - read;
                    }

                    return frames.Length - read + write;
                }
            }

            public long DroppedFrames
            {
                get { return Interlocked.Read(ref droppedFrames); }
            }

            public bool TryEnqueue(float[] samples, int samplesLen, int channels)
            {
                int write = Volatile.Read(ref writeIndex);
                int next = Next(write);
                if (next == Volatile.Read(ref readIndex))
                {
                    Interlocked.Increment(ref droppedFrames);
                    return false;
                }

                RenderFrame frame = frames[write];
                Array.Copy(samples, 0, frame.Samples, 0, samplesLen);
                frame.SamplesLen = samplesLen;
                frame.Channels = channels;
                Volatile.Write(ref writeIndex, next);
                return true;
            }

            public bool TryPeek(out RenderFrame frame)
            {
                int read = Volatile.Read(ref readIndex);
                if (read == Volatile.Read(ref writeIndex))
                {
                    frame = null;
                    return false;
                }

                frame = frames[read];
                return true;
            }

            public void Pop()
            {
                int read = Volatile.Read(ref readIndex);
                if (read != Volatile.Read(ref writeIndex))
                {
                    Volatile.Write(ref readIndex, Next(read));
                }
            }

            public void Clear()
            {
                Volatile.Write(ref readIndex, Volatile.Read(ref writeIndex));
            }

            private int Next(int index)
            {
                index++;
                return index == frames.Length ? 0 : index;
            }
        }
    }
}
