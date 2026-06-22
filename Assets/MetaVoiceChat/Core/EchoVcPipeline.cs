using MetaVoiceChat.Core.Opus;
using MetaVoiceChat.Core.RNNoise;
using System;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    public class EchoVcPipeline : VcPipeline
    {
        [Header("Opus Settings")]
        public int tempMaxDataBytesPerPacket = MetaVoiceChatConstants.MaxPacketSize;
        public OpusUserConfigScriptableObject opusUserConfigScriptableObject;

        public OnAudioFilterReadVcOutput[] outputs;

        private readonly RnnoiseVcProcessor rnnoise = new();
        private readonly OpusEncoderVcProcessor encoder = new(default);
        private readonly OpusDecoderVcDataProcessor decoder = new();

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            rnnoise.Process(frame, frameSize, frequency, channels, sequenceNumber, timestamp);
            ReadOnlySpan<float> rnnoiseFrame = rnnoise.DenoisedSamples;

            var userConfig = encoder.UserConfig;
            OpusUserConfig targetUserConfig = opusUserConfigScriptableObject.ToOpusUserConfig(tempMaxDataBytesPerPacket);
            if (!userConfig.Equals(targetUserConfig))
            {
                encoder.UserConfig = targetUserConfig;
            }

            encoder.Process(rnnoiseFrame, frameSize, frequency, channels, sequenceNumber, timestamp);
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
