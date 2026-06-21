using Concentus;
using System;
using System.Threading;

// References:
// https://wiki.xiph.org/Opus_Recommended_Settings
// https://datatracker.ietf.org/doc/html/rfc6716#section-2
// https://ddanilov.me/how-to-enable-in-band-fec-for-opus-codec/

// Conclusions:
// FEC may not be super useful -- it only helps with single packet loss
// https://ddanilov.me/how-to-enable-in-band-fec-for-opus-codec/
// And it only works with SILK?

// Raw bitrate without encoding = 16 bits * 48000 Hz = 768000 bits/s

namespace MetaVoiceChat.Core.Opus
{
    public class OpusEncoderVcProcessor : IVcProcessor
    {
        private readonly byte[] buffer = new byte[OpusConfig.MaxPacketSize];

        private OpusUserConfigBox userConfig;

        public OpusUserConfig UserConfig
        {
            get => Volatile.Read(ref userConfig).value;
            set => Volatile.Write(ref userConfig, new OpusUserConfigBox(value));
        }

        private IOpusEncoder encoder;
        private OpusConfig config;

        public OpusEncoderVcProcessor(OpusUserConfig userConfig)
        {
            this.userConfig = new(userConfig);
        }

        private int lastBytesEncoded = 0;
        public ReadOnlySpan<byte> EncodedData => buffer.AsSpan(0, lastBytesEncoded);

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            OpusUserConfig currentUserConfig = UserConfig;
            OpusConfig targetConfig = currentUserConfig.GetConfig(frequency, channels);

            if (frame.Length != frameSize)
            {
                encoder?.ResetState();
                return;
            }

            if (encoder == null || !targetConfig.Equals(config))
            {
                encoder?.Dispose();
                encoder = OpusCodecFactory.CreateEncoder(targetConfig.sampleRate, targetConfig.numChannels, targetConfig.application);

                encoder.Bandwidth = targetConfig.bandwidth;
                //opusEncoder.Bitrate
                encoder.Complexity = targetConfig.complexity;
                //opusEncoder.ExpertFrameDuration
                //opusEncoder.FinalRange
                //opusEncoder.ForceChannels
                encoder.ForceMode = targetConfig.mode;
                encoder.LSBDepth = OpusConfig.BitsPerSample;
                encoder.MaxBandwidth = targetConfig.bandwidth;
                //opusEncoder.PacketLossPercent
                //opusEncoder.PredictionDisabled
                encoder.SignalType = targetConfig.signal;
                //opusEncoder.UseConstrainedVBR
                //opusEncoder.UseDTX // Hey future me, I tried this. all it did was throw exceptions in SILK and Hybrid modes.
                //opusEncoder.UseInbandFEC
                //opusEncoder.UseVBR

                config = targetConfig;
            }

            lastBytesEncoded = encoder.Encode(frame, frameSize / channels, buffer, currentUserConfig.maxDataBytesPerPacket);
        }
    }
}