using System;

namespace MetaVoiceChat.Core.RNNoise
{
    public class RnnoiseVcProcessor : IVcProcessor
    {
        private readonly float[] buffer = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            if (frameSize != 10 && frameSize != 20 && frameSize != 40)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} frame size must be 10, 20, or 40 ms");
            }

            if (frequency != 48000)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} frequency must be exactly 48000 Hz");
            }

            if (channels != 1 && channels != 2)
            {
                throw new ArgumentException($"{nameof(RnnoiseVcProcessor)} channels must be either 1 or 2");
            }

        }
    }
}
