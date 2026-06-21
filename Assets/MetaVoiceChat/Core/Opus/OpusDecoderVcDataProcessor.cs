using Concentus;
using System;

namespace MetaVoiceChat.Core.Opus
{
    public class OpusDecoderVcDataProcessor : IVcDataProcessor
    {
        private const int MaxPossibleBufferSize = 48000 * 2 * 40 / 1000;
        private readonly float[] buffer = new float[MaxPossibleBufferSize];
        private IOpusDecoder decoder;

        private int lastSamplesDecoded = 0;
        public ReadOnlySpan<float> DecodedData => buffer.AsSpan(0, lastSamplesDecoded);

        public OpusDecoderVcDataProcessor()
        {
#if ENABLE_IL2CPP
            OpusCodecFactory.AttemptToUseNativeLibrary = false;
#endif
        }

        public void Process(ReadOnlySpan<byte> data, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            if (data.Length == 0)
            {
                lastSamplesDecoded = frameSize;
                Array.Clear(buffer, 0, lastSamplesDecoded);
                return;
            }

            if (decoder == null || decoder.SampleRate != frequency || decoder.NumChannels != channels)
            {
                decoder?.Dispose();
                decoder = OpusCodecFactory.CreateDecoder(frequency, channels);
            }

            int samplesDecoded = decoder.Decode(data, buffer, buffer.Length / channels);
            lastSamplesDecoded = samplesDecoded * channels;
        }
    }
}
