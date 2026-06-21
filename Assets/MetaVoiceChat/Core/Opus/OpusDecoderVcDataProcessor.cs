using System;

namespace MetaVoiceChat.Core.Opus
{
    public class OpusDecoderVcDataProcessor : IVcDataProcessor
    {
        private const int MaxPossibleBufferSize = 48000 * 2 * 40 / 1000;
        private readonly float[] buffer = new float[MaxPossibleBufferSize];

        private int lastSamplesDecoded = 0;
        public ReadOnlySpan<float> DecodedData => buffer.AsSpan(0, lastSamplesDecoded);

        public void Process(ReadOnlySpan<byte> data, int dataSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {

        }
    }
}
