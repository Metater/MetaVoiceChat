using System;
using Concentus;

namespace Assets.Metater.MetaVoiceChat.General
{
    public class VcDecoder : IDisposable
    {
        private readonly IOpusDecoder opusDecoder;
        private readonly float[] buffer;

        public string Version => opusDecoder.GetVersionString();

        public VcDecoder(VcConfig config)
        {
            opusDecoder = OpusCodecFactory.CreateDecoder(VcConfig.SamplesPerSecond, VcConfig.ChannelCount);

            //opusDecoder.Gain

            buffer = new float[config.general.samplesPerFrame];
        }

        public ReadOnlySpan<float> DecodeFrame(ReadOnlySpan<byte> data, bool decodeFec = false)
        {
            int samplesDecoded = opusDecoder.Decode(data, buffer, buffer.Length, decodeFec);
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