using System;
using System.Runtime.InteropServices;

namespace MetaVoiceChat.Core.RNNoise
{
#if META_VOICE_CHAT_RNNOISE
    using Adrenak.RNNoise4Unity;

    public sealed class RnnoiseVcProcessor : IVcProcessor
    {
        private const int SupportedFrequencyHz = 48000;

        private readonly float[] denoisedSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];
        private readonly float[] leftChannel = new float[Native.FRAME_SIZE];
        private readonly float[] rightChannel = new float[Native.FRAME_SIZE];

        private DenoiserSafeHandle leftDenoiser;
        private DenoiserSafeHandle rightDenoiser;
        private int lastSamplesDenoised;

        /// <summary>
        /// Denoised PCM samples from the most recent <see cref="Process"/> call.
        /// The returned span is backed by an internal buffer and must be read immediately.
        /// </summary>
        public ReadOnlySpan<float> DenoisedSamples => denoisedSamples.AsSpan(0, lastSamplesDenoised);

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
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

            if (frameSize > denoisedSamples.Length)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} frame is larger than the maximum supported buffer size.");
            }

            frame.Slice(0, frameSize).CopyTo(denoisedSamples);
            if (frame.Length < frameSize)
            {
                Array.Clear(denoisedSamples, frame.Length, frameSize - frame.Length);
            }

            if (leftDenoiser == null)
            {
                leftDenoiser = new DenoiserSafeHandle(new Denoiser());
            }

            if (channels == 2 && rightDenoiser == null)
            {
                rightDenoiser = new DenoiserSafeHandle(new Denoiser());
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
                Denoiser left = leftDenoiser.Denoiser;
                for (int i = 0; i < denoiserFrames; i++)
                {
                    int sampleOffset = i * Native.FRAME_SIZE;
                    Array.Copy(denoisedSamples, sampleOffset, leftChannel, 0, Native.FRAME_SIZE);
                    left.Denoise(leftChannel);
                    Array.Copy(leftChannel, 0, denoisedSamples, sampleOffset, Native.FRAME_SIZE);
                }
            }
            else
            {
                Denoiser left = leftDenoiser.Denoiser;
                Denoiser right = rightDenoiser.Denoiser;

                for (int i = 0; i < denoiserFrames; i++)
                {
                    int frameOffset = i * Native.FRAME_SIZE * 2;

                    for (int j = 0; j < Native.FRAME_SIZE; j++)
                    {
                        int interleavedIndex = frameOffset + j * 2;
                        leftChannel[j] = denoisedSamples[interleavedIndex];
                        rightChannel[j] = denoisedSamples[interleavedIndex + 1];
                    }

                    left.Denoise(leftChannel);
                    right.Denoise(rightChannel);

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

        private sealed class DenoiserSafeHandle : SafeHandle
        {
            private GCHandle gcHandle;

            public DenoiserSafeHandle(Denoiser denoiser)
                : base(IntPtr.Zero, ownsHandle: true)
            {
                if (denoiser == null)
                {
                    throw new ArgumentNullException(nameof(denoiser));
                }

                gcHandle = GCHandle.Alloc(denoiser, GCHandleType.Normal);
                SetHandle(GCHandle.ToIntPtr(gcHandle));
            }

            public Denoiser Denoiser
            {
                get
                {
                    if (IsClosed || IsInvalid || !gcHandle.IsAllocated)
                    {
                        throw new ObjectDisposedException(nameof(DenoiserSafeHandle));
                    }

                    if (gcHandle.Target is not Denoiser denoiser)
                    {
                        throw new ObjectDisposedException(nameof(DenoiserSafeHandle));
                    }

                    return denoiser;
                }
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                try
                {
                    if (gcHandle.IsAllocated && gcHandle.Target is Denoiser denoiser)
                    {
                        try
                        {
                            denoiser.Dispose();
                        }
                        catch
                        {
                            // ReleaseHandle may run on the finalizer thread.
                            // Do not allow cleanup exceptions to escape.
                        }
                    }
                }
                finally
                {
                    if (gcHandle.IsAllocated)
                    {
                        gcHandle.Free();
                    }

                    gcHandle = default;
                    handle = IntPtr.Zero;
                }

                return true;
            }
        }
    }
#else
    public sealed class RnnoiseVcProcessor : IVcProcessor
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

            frame.Slice(0, frameSize).CopyTo(denoisedSamples);
            if (frame.Length < frameSize)
            {
                Array.Clear(denoisedSamples, frame.Length, frameSize - frame.Length);
            }

            lastSamplesDenoised = frameSize;
        }
    }
#endif
}
