using System;
using Concentus;

// References:
// https://wiki.xiph.org/Opus_Recommended_Settings
// https://datatracker.ietf.org/doc/html/rfc6716#section-2
// https://ddanilov.me/how-to-enable-in-band-fec-for-opus-codec/

// Conclusions:
// FEC may not be super useful -- it only helps with single packet loss

// Raw bitrate without encoding = 16 bits * 16000 Hz = 256000 bits/s

namespace Assets.Metater.MetaVoiceChat.Opus
{
    public class VcEncoder : IDisposable
    {
        private readonly IOpusEncoder opusEncoder;
        private readonly byte[] buffer;
        private readonly int frameSize;

        public string Version => opusEncoder.GetVersionString();

        public bool HasEncodedYet { get; private set; } = false;

        public VcEncoder(VcConfig config, int maxDataBytesPerPacket)
        {
            // 1275 is the maximum packet size for Opus
            maxDataBytesPerPacket = Math.Min(maxDataBytesPerPacket, 1275);

            opusEncoder = OpusCodecFactory.CreateEncoder(VcConfig.SamplesPerSecond, numChannels: 1, config.application);

            opusEncoder.Bandwidth = VcConfig.Bandwidth;
            //opusEncoder.Bitrate
            opusEncoder.Complexity = config.complexity;
            //opusEncoder.ExpertFrameDuration
            //opusEncoder.FinalRange
            //opusEncoder.ForceChannels
            opusEncoder.ForceMode = VcConfig.Mode;
            opusEncoder.LSBDepth = VcConfig.BitsPerSample;
            opusEncoder.MaxBandwidth = VcConfig.MaxBandwidth;
            //opusEncoder.PacketLossPercent
            //opusEncoder.PredictionDisabled
            opusEncoder.SignalType = config.signal;
            //opusEncoder.UseConstrainedVBR
            //opusEncoder.UseDTX
            //opusEncoder.UseInbandFEC = true;
            //opusEncoder.UseVBR

            buffer = new byte[maxDataBytesPerPacket];
            frameSize = config.samplesPerFrame;
        }

        public ReadOnlySpan<byte> EncodeFrame(ReadOnlySpan<float> samples)
        {
            HasEncodedYet = true;

            int bytesEncoded = opusEncoder.Encode(samples, frameSize, buffer, buffer.Length);
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