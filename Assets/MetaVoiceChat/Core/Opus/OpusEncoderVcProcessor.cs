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
            this.userConfig = new(userConfig);

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

                encoder.Bandwidth = targetConfig.bandwidth;
                //encoder.Bitrate
                encoder.Complexity = targetConfig.complexity;
                //encoder.ExpertFrameDuration
                //encoder.FinalRange
                //encoder.ForceChannels
                encoder.ForceMode = targetConfig.mode;
                encoder.LSBDepth = OpusConfig.BitsPerSample;
                encoder.MaxBandwidth = targetConfig.bandwidth;
                //encoder.PacketLossPercent
                //encoder.PredictionDisabled
                encoder.SignalType = targetConfig.signal;
                //encoder.UseConstrainedVBR
                //encoder.UseDTX // Hey future me, I tried this. all it did was throw exceptions in SILK and Hybrid modes.
                //encoder.UseInbandFEC
                //encoder.UseVBR

                config = targetConfig;
            }

            lastBytesEncoded = encoder.Encode(frame, frameSize / channels, buffer, currentUserConfig.maxDataBytesPerPacket);
        }
    }
}