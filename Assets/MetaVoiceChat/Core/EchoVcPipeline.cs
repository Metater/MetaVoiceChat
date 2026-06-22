using MetaVoiceChat.Core.AEC3;
using MetaVoiceChat.Core.NetEQ;
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

        private readonly AcousticEchoCancellation3VcProcessor aec3 = new();
        private readonly HighPassFilterVcProcessor hpf = new();
        private readonly AutomaticGainControl2VcProcessor agc2 = new();
        private readonly RnnoiseVcProcessor rnnoise = new();
        private readonly OpusEncoderVcProcessor encoder = new(default);
        private readonly OpusDecoderVcDataProcessor decoder = new();

        private void OnDestroy()
        {
            rnnoise.Dispose();
            encoder.Dispose();
            decoder.Dispose();
        }

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            var userConfig = encoder.UserConfig;
            OpusUserConfig targetUserConfig = opusUserConfigScriptableObject.ToOpusUserConfig(tempMaxDataBytesPerPacket);
            if (!userConfig.Equals(targetUserConfig))
            {
                encoder.UserConfig = targetUserConfig;
            }

            hpf.Process(frame, frameSize, frequency, channels, sequenceNumber, timestamp);
            aec3.Process(hpf.HighPassFilteredSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
            rnnoise.Process(aec3.EchoCancelledSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
            agc2.Process(rnnoise.DenoisedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);

            encoder.Process(agc2.ProcessedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
            decoder.Process(encoder.EncodedData, frameSize, frequency, channels, sequenceNumber, timestamp);

            if (outputs != null)
            {
                foreach (var output in outputs)
                {
                    if (output != null)
                    {
                        output.Process(decoder.DecodedData, frameSize, frequency, channels, sequenceNumber, timestamp);
                    }
                }
            }
        }
    }
}
