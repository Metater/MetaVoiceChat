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

//// Referenced: https://wiki.xiph.org/Opus_Recommended_Settings
//// Bandwidth Transition Thresholds
//// Note the Nyquist frequency is half the sampling rate
//return frequency switch
//{
//    8000 => OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND,
//    12000 => OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND,
//    16000 => OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND,
//    24000 => OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND,
//    48000 => OpusBandwidth.OPUS_BANDWIDTH_FULLBAND,
//    _ => OpusBandwidth.OPUS_BANDWIDTH_FULLBAND,
//};

//if (frequency == 8000 || frequency == 12000)
//{
//    return OpusMode.MODE_SILK_ONLY;
//}

namespace MetaVoiceChat.Core.Opus
{
    public class OpusEncoderVcProcessor : IVcProcessor
    {
        private readonly byte[] buffer = new byte[MetaVoiceChatConstants.MaxPacketSize];

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
            Volatile.Write(ref this.userConfig, new(userConfig));

#if ENABLE_IL2CPP
            OpusCodecFactory.AttemptToUseNativeLibrary = false;
#endif
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

                encoder.Complexity = targetConfig.complexity;

                if (targetConfig.overrideBandwidth)
                {
                    encoder.Bandwidth = targetConfig.bandwidth;
                }

                if (targetConfig.overrideBitrate)
                {
                    encoder.Bitrate = targetConfig.bitrate;
                }

                if (targetConfig.overrideExpertFrameDuration)
                {
                    encoder.ExpertFrameDuration = targetConfig.expertFrameDuration;
                }

                if (targetConfig.overrideForceChannels)
                {
                    encoder.ForceChannels = targetConfig.forceChannels;
                }

                if (targetConfig.overrideForceMode)
                {
                    encoder.ForceMode = targetConfig.forceMode;
                }

                if (targetConfig.overrideLSBDepth)
                {
                    encoder.LSBDepth = targetConfig.lsbDepth;
                }

                if (targetConfig.overrideMaxBandwidth)
                {
                    encoder.MaxBandwidth = targetConfig.maxBandwidth;
                }

                if (targetConfig.overridePacketLossPercent)
                {
                    encoder.PacketLossPercent = targetConfig.packetLossPercent;
                }

                if (targetConfig.overridePredictionDisabled)
                {
                    encoder.PredictionDisabled = targetConfig.predictionDisabled;
                }

                if (targetConfig.overrideSignalType)
                {
                    encoder.SignalType = targetConfig.signalType;
                }

                if (targetConfig.overrideUseConstrainedVBR)
                {
                    encoder.UseConstrainedVBR = targetConfig.useConstrainedVBR;
                }

                if (targetConfig.overrideUseDTX)
                {
                    encoder.UseDTX = targetConfig.useDTX;
                }

                if (targetConfig.overrideUseInbandFEC)
                {
                    encoder.UseInbandFEC = targetConfig.useInbandFEC;
                }

                if (targetConfig.overrideUseVBR)
                {
                    encoder.UseVBR = targetConfig.useVBR;
                }

                config = targetConfig;
            }

            lastBytesEncoded = encoder.Encode(frame, frameSize / channels, buffer, currentUserConfig.maxDataBytesPerPacket);
        }
    }
}
