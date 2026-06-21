using Concentus.Enums;
using MetaVoiceChat.Core.Opus;
using System;

namespace MetaVoiceChat.Core
{
    public class EchoVcPipeline : VcPipeline
    {
        public int maxDataBytesPerPacket = OpusConfig.MaxPacketSize;
        public int complexity = 3;
        public bool isMusic;
        public bool shouldOverride;
        public OpusApplication overrideApplication;
        public OpusBandwidth overrideBandwidth;
        public OpusMode overrideMode;
        public OpusSignal overrideSignal;


        public OnAudioFilterReadVcOutput[] outputs;

        private readonly OpusEncoderVcProcessor encoder = new(default);
        private readonly OpusDecoderVcDataProcessor decoder = new();

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            var userConfig = encoder.UserConfig;
            OpusUserConfig targetUserConfig = new(maxDataBytesPerPacket, complexity, isMusic, shouldOverride, overrideApplication, overrideBandwidth, overrideMode, overrideSignal);
            if (!userConfig.Equals(targetUserConfig))
            {
                encoder.UserConfig = targetUserConfig;
            }

            encoder.Process(frame, frameSize, frequency, channels, sequenceNumber, timestamp);
            decoder.Process(encoder.EncodedData, frameSize, frequency, channels, sequenceNumber, timestamp);
            ReadOnlySpan<float> testFrame = decoder.DecodedData;

            if (outputs != null)
            {
                foreach (var output in outputs)
                {
                    if (output != null)
                    {
                        output.Process(testFrame, frameSize, frequency, channels, sequenceNumber, timestamp);
                    }
                }
            }
        }
    }
}
