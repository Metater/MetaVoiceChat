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

        [Header("Processing Stages")]
        [Tooltip("Apply high-pass filtering before the other audio processing stages.")]
        public bool enableHighPassFilter = true;

        [Tooltip("Apply acoustic echo cancellation.")]
        public bool enableAcousticEchoCancellation = true;

        [Tooltip("Apply RNNoise denoising.")]
        public bool enableRnnoise = true;

        [Tooltip("Apply automatic gain control.")]
        public bool enableAutomaticGainControl = true;

        [Tooltip("Microphone input volume supplied to AGC2.")]
        [Range(0, 255)]
        public int appliedInputVolume = 255;

        [Tooltip("Encode and decode audio through Opus before sending it to outputs.")]
        public bool enableOpus = true;

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
            ReadOnlySpan<float> processedSamples = frame;

            if (enableHighPassFilter)
            {
                hpf.Process(processedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
                processedSamples = hpf.HighPassFilteredSamples;
            }

            if (enableAcousticEchoCancellation)
            {
                aec3.Process(processedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
                processedSamples = aec3.EchoCancelledSamples;
            }

            if (enableRnnoise)
            {
                rnnoise.Process(processedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
                processedSamples = rnnoise.DenoisedSamples;
            }

            if (enableAutomaticGainControl)
            {
                agc2.AppliedInputVolume = appliedInputVolume;
                agc2.Process(processedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
                processedSamples = agc2.ProcessedSamples;
            }

            if (enableOpus)
            {
                var userConfig = encoder.UserConfig;
                OpusUserConfig targetUserConfig = opusUserConfigScriptableObject.ToOpusUserConfig(tempMaxDataBytesPerPacket);
                if (!userConfig.Equals(targetUserConfig))
                {
                    encoder.UserConfig = targetUserConfig;
                }

                encoder.Process(processedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
                decoder.Process(encoder.EncodedData, frameSize, frequency, channels, sequenceNumber, timestamp);
                processedSamples = decoder.DecodedData;
            }

            if (outputs != null)
            {
                foreach (var output in outputs)
                {
                    if (output != null)
                    {
                        output.Process(processedSamples, frameSize, frequency, channels, sequenceNumber, timestamp);
                    }
                }
            }
        }
    }
}
