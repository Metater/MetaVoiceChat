using System;

namespace MetaVoiceChat.Core.RNNoise
{
#if META_VOICE_CHAT_RNNOISE
    using Adrenak.RNNoise4Unity;

    public sealed class RnnoiseVcProcessor : IVcProcessor, IDisposable
    {
        private const int SupportedFrequencyHz = 48000;

        private readonly object processorLock = new object();
        private readonly float[] denoisedSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];
        private readonly float[] leftChannel = new float[Native.FRAME_SIZE];
        private readonly float[] rightChannel = new float[Native.FRAME_SIZE];

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
            _ = sequenceNumber;
            _ = timestamp;

            if (frameSize <= 0 || frame.Length < frameSize)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} requires a non-empty PCM frame.");
            }

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

            int samplesPerChannel = frameSize / channels;
            if (samplesPerChannel != Native.FRAME_SIZE &&
                samplesPerChannel != Native.FRAME_SIZE * 2 &&
                samplesPerChannel != Native.FRAME_SIZE * 4)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} only supports 10, 20, or 40 ms frames at 48 kHz.");
            }

            lock (processorLock)
            {
                if (frameSize > denoisedSamples.Length)
                {
                    throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} frame is larger than the maximum supported buffer size.");
                }

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

                if (frame.IsEmpty)
                {
                    lastSamplesDenoised = 0;
                    return;
                }

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
        }

        public void Dispose()
        {
            lock (processorLock)
            {
                leftDenoiser?.Dispose();
                leftDenoiser = null;

                rightDenoiser?.Dispose();
                rightDenoiser = null;

                lastSamplesDenoised = 0;
            }
        }
    }
#else
    public sealed class RnnoiseVcProcessor : IVcProcessor
    {
        public ReadOnlySpan<float> DenoisedSamples => ReadOnlySpan<float>.Empty;

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            _ = frame;
            _ = frameSize;
            _ = frequency;
            _ = channels;
            _ = sequenceNumber;
            _ = timestamp;
        }
    }
#endif
}
