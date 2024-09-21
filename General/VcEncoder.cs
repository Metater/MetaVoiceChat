using System;
using Concentus;

// References:
// https://wiki.xiph.org/Opus_Recommended_Settings
// https://datatracker.ietf.org/doc/html/rfc6716#section-2
// https://ddanilov.me/how-to-enable-in-band-fec-for-opus-codec/

// Conclusions:
// FEC may not be super useful -- it only helps with single packet loss

// Raw bitrate without encoding = 16 * 16000 = 256000

namespace Assets.Metater.MetaVoiceChat.General
{
    public class VcEncoder : IDisposable
    {
        private readonly IOpusEncoder opusEncoder;
        private readonly byte[] buffer;
        private readonly int frameSize;

        public string Version => opusEncoder.GetVersionString();

        public VcEncoder(VcConfig config, int maxDataBytesPerPacket)
        {
            maxDataBytesPerPacket = Math.Min(maxDataBytesPerPacket, 1275);

            opusEncoder = OpusCodecFactory.CreateEncoder(VcConfig.SamplesPerSecond, VcConfig.ChannelCount, config.opus.application);

            opusEncoder.Bandwidth = VcConfig.OpusConfig.Bandwidth;
            //opusEncoder.Bitrate
            opusEncoder.Complexity = config.opus.complexity;
            //opusEncoder.ExpertFrameDuration
            //opusEncoder.FinalRange
            //opusEncoder.ForceChannels
            opusEncoder.ForceMode = VcConfig.OpusConfig.Mode;
            opusEncoder.LSBDepth = VcConfig.BitsPerSample;
            opusEncoder.MaxBandwidth = VcConfig.OpusConfig.MaxBandwidth;
            //opusEncoder.PacketLossPercent
            //opusEncoder.PredictionDisabled
            opusEncoder.SignalType = config.opus.signal;
            //opusEncoder.UseConstrainedVBR
            //opusEncoder.UseDTX
            //opusEncoder.UseInbandFEC = true;
            //opusEncoder.UseVBR

            buffer = new byte[maxDataBytesPerPacket];
            frameSize = config.general.samplesPerFrame;
        }

        public ReadOnlySpan<byte> Encode(ReadOnlySpan<short> frame)
        {
            int bytesEncoded = opusEncoder.Encode(frame, frameSize, buffer, buffer.Length);
            return buffer.AsSpan(0, bytesEncoded);
        }

        public void ResetState()
        {
            opusEncoder.ResetState();
        }

        public void Dispose()
        {
            opusEncoder.Dispose();
        }
    }
}