using Concentus;
using System;

namespace MetaVoiceChat.Utils
{
    public class OneWayResampler
    {
        private IResampler resampler;

        private int channels;
        private int inputFrequency;
        private int outputFrequency;
        private int quality;

        /// <summary>
        /// Configures the resampler with the specified parameters. If the parameters are the same as the current configuration, it does nothing.
        /// </summary>
        /// <param name="channels">The number of channels to be processed, 1 or 2</param>
        /// <param name="inputFrequency">Input sampling rate, in hertz</param>
        /// <param name="outputFrequency">Output sampling rate, in hertz</param>
        /// <param name="quality">Resampling quality, from 0 to 10</param>
        public void Configure(int channels, int inputFrequency, int outputFrequency, int quality)
        {
            bool alreadyConfigured = this.channels == channels &&
                this.inputFrequency == inputFrequency &&
                this.outputFrequency == outputFrequency &&
                this.quality == quality;

            if (alreadyConfigured)
            {
                return;
            }

            Free();

            this.channels = channels;
            this.inputFrequency = inputFrequency;
            this.outputFrequency = outputFrequency;
            this.quality = quality;

            resampler = ResamplerFactory.CreateResampler(channels, inputFrequency, outputFrequency, quality);
        }

        /// <summary>
        /// Resamples an interleaved float array. The stride is automatically determined by the number of channels of the resampler.
        /// </summary>
        /// <param name="input">Input buffer</param>
        /// <param name="in_len">Number of input samples (NOT MULTIPLIED BY CHANNELS)</param>
        /// <param name="output">Output buffer</param>
        /// <param name="out_len">Number of output samples (NOT MULTIPLIED BY CHANNELS)</param>
        public void ProcessInterleaved(Span<float> input, ref int in_len, Span<float> output, ref int out_len)
        {
            resampler.ProcessInterleaved(input, ref in_len, output, ref out_len);
        }

        /// <summary>
        /// Clears the resampler buffers so a new (unrelated) stream can be processed.
        /// </summary>
        public void ResetMem()
        {
            resampler?.ResetMem();
        }

        /// <summary>
        /// Frees the resampler.
        /// </summary>
        public void Free()
        {
            resampler?.Dispose();
            resampler = null;

            channels = 0;
            inputFrequency = 0;
            outputFrequency = 0;
            quality = 0;
        }
    }
}
