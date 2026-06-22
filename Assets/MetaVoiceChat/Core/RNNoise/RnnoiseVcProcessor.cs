using System;
using System.Runtime.InteropServices;

namespace MetaVoiceChat.Core.RNNoise
{
#if META_VOICE_CHAT_RNNOISE
    using Native = Adrenak.RNNoise4Unity.Native;

    public sealed class RnnoiseVcProcessor : IVcProcessor, IDisposable
    {
        private const int SupportedFrequencyHz = 48000;

        private readonly float[] denoisedSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];
        private readonly float[] leftChannel = new float[Native.FRAME_SIZE];
        private readonly float[] rightChannel = new float[Native.FRAME_SIZE];

        private bool disposed;
        private Denoiser leftDenoiser;
        private Denoiser rightDenoiser;
        private int lastSamplesDenoised;

        /// <summary>
        /// Denoised PCM samples from the most recent <see cref="Process"/> call.
        /// The returned span is backed by an internal buffer and must be read immediately.
        /// </summary>
        public ReadOnlySpan<float> DenoisedSamples => denoisedSamples.AsSpan(0, lastSamplesDenoised);

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(RnnoiseVcProcessor));
            }

            _ = sequenceNumber;
            _ = timestamp;

            if (frameSize < 0 || frameSize > denoisedSamples.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(frameSize));
            }

            if (frame.IsEmpty)
            {
                lastSamplesDenoised = frameSize;
                Array.Clear(denoisedSamples, 0, frameSize);
                return;
            }

            ValidateFrame(frame, frameSize, frequency, channels);

            frame.Slice(0, frameSize).CopyTo(denoisedSamples);

            if (leftDenoiser == null)
            {
                leftDenoiser = new Denoiser();
            }

            if (channels == 2 && rightDenoiser == null)
            {
                rightDenoiser = new Denoiser();
            }
            else if (channels == 1 && rightDenoiser != null)
            {
                rightDenoiser.Dispose();
                rightDenoiser = null;
            }

            int samplesPerChannel = frameSize / channels;
            int denoiserFrames = samplesPerChannel / Native.FRAME_SIZE;

            if (channels == 1)
            {
                for (int i = 0; i < denoiserFrames; i++)
                {
                    int sampleOffset = i * Native.FRAME_SIZE;
                    Array.Copy(denoisedSamples, sampleOffset, leftChannel, 0, Native.FRAME_SIZE);
                    leftDenoiser.Denoise(leftChannel);
                    Array.Copy(leftChannel, 0, denoisedSamples, sampleOffset, Native.FRAME_SIZE);
                }
            }
            else
            {
                for (int i = 0; i < denoiserFrames; i++)
                {
                    int frameOffset = i * Native.FRAME_SIZE * 2;

                    for (int j = 0; j < Native.FRAME_SIZE; j++)
                    {
                        int interleavedIndex = frameOffset + j * 2;
                        leftChannel[j] = denoisedSamples[interleavedIndex];
                        rightChannel[j] = denoisedSamples[interleavedIndex + 1];
                    }

                    leftDenoiser.Denoise(leftChannel);
                    rightDenoiser.Denoise(rightChannel);

                    for (int j = 0; j < Native.FRAME_SIZE; j++)
                    {
                        int interleavedIndex = frameOffset + j * 2;
                        denoisedSamples[interleavedIndex] = leftChannel[j];
                        denoisedSamples[interleavedIndex + 1] = rightChannel[j];
                    }
                }
            }

            lastSamplesDenoised = frameSize;
        }

        private static void ValidateFrame(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels)
        {
            if (frequency != SupportedFrequencyHz)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} frequency must be exactly {SupportedFrequencyHz} Hz.");
            }

            if (channels != 1 && channels != 2)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} channels must be either 1 or 2.");
            }

            if (frameSize % channels != 0)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} frame size must be divisible by the channel count.");
            }

            if (frame.Length > 0 && frame.Length < frameSize)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} frame length must be at least frameSize when data is present.");
            }

            int samplesPerChannel = frameSize / channels;
            if (samplesPerChannel != Native.FRAME_SIZE &&
                samplesPerChannel != Native.FRAME_SIZE * 2 &&
                samplesPerChannel != Native.FRAME_SIZE * 4)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} only supports 10, 20, or 40 ms frames at 48 kHz.");
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            leftDenoiser?.Dispose();
            leftDenoiser = null;

            rightDenoiser?.Dispose();
            rightDenoiser = null;
        }

        private sealed class Denoiser : SafeHandle
        {
            public Denoiser()
                : base(IntPtr.Zero, ownsHandle: true)
            {
                SetHandle(Native.rnnoise_create(IntPtr.Zero));
                if (IsInvalid)
                {
                    throw new InvalidOperationException("Failed to create RNNoise denoiser state.");
                }
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            public unsafe int Denoise(Span<float> buffer, bool finish = false)
            {
                if (buffer.Length == 0)
                {
                    return 0;
                }

                if (buffer.Length != Native.FRAME_SIZE)
                {
                    throw new ArgumentException($"RNNoise denoiser requires exactly {Native.FRAME_SIZE} samples.");
                }

                if (IsClosed || IsInvalid)
                {
                    throw new ObjectDisposedException(nameof(Denoiser));
                }

                bool addedRef = false;
                try
                {
                    DangerousAddRef(ref addedRef);

                    fixed (float* data = buffer)
                    {
                        for (int i = 0; i < Native.FRAME_SIZE; i++)
                        {
                            data[i] *= 32767f;
                        }

                        Native.rnnoise_process_frame(handle, data, data);

                        for (int i = 0; i < Native.FRAME_SIZE; i++)
                        {
                            data[i] *= 3.051851E-05f;
                        }
                    }

                    _ = finish;
                    return Native.FRAME_SIZE;
                }
                finally
                {
                    if (addedRef)
                    {
                        DangerousRelease();
                    }
                }
            }

            protected override bool ReleaseHandle()
            {
                IntPtr handleToRelease = handle;

                if (handleToRelease != IntPtr.Zero)
                {
                    try
                    {
                        Native.rnnoise_destroy(handleToRelease);
                    }
                    catch
                    {
                        // Finalizer thread cleanup should not throw.
                    }
                }

                handle = IntPtr.Zero;
                return true;
            }
        }
    }
#else
    public sealed class RnnoiseVcProcessor : IVcProcessor, IDisposable
    {
        private readonly float[] denoisedSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];
        private int lastSamplesDenoised;

        public ReadOnlySpan<float> DenoisedSamples => denoisedSamples.AsSpan(0, lastSamplesDenoised);

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            _ = sequenceNumber;
            _ = timestamp;
            _ = frequency;
            _ = channels;

            if (frameSize < 0 || frameSize > denoisedSamples.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(frameSize));
            }

            if (frame.IsEmpty)
            {
                lastSamplesDenoised = frameSize;
                Array.Clear(denoisedSamples, 0, frameSize);
                return;
            }

            int copyCount = Math.Min(frame.Length, frameSize);
            frame.Slice(0, copyCount).CopyTo(denoisedSamples);

            if (copyCount < frameSize)
            {
                Array.Clear(denoisedSamples, copyCount, frameSize - copyCount);
            }

            lastSamplesDenoised = frameSize;
        }

        public void Dispose()
        {
        }
    }
#endif
}
