using System;
using Concentus;

namespace Assets.Metater.MetaVoiceChat.General
{
    public class VcDecoder : IDisposable
    {
        private readonly IOpusDecoder opusDecoder;
        private readonly short[] buffer;

        public string Version => opusDecoder.GetVersionString();

        public VcDecoder(VcConfig config)
        {
            opusDecoder = OpusCodecFactory.CreateDecoder(VcConfig.SamplesPerSecond, VcConfig.ChannelCount);

            //opusDecoder.Gain

            buffer = new short[config.general.samplesPerFrame];
        }

        public ReadOnlySpan<short> Decode(ReadOnlySpan<byte> frame, bool decodeFec = false)
        {
            int frameSize = buffer.Length;
            int samplesDecoded = opusDecoder.Decode(frame, buffer, frameSize, decodeFec);
            return buffer.AsSpan(0, samplesDecoded);
        }

        public void ResetState()
        {
            opusDecoder.ResetState();
        }

        public void Dispose()
        {
            opusDecoder.Dispose();
        }
    }
}