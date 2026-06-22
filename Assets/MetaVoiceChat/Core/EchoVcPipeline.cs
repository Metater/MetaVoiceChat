using Concentus.Enums;
using MetaVoiceChat.Core.Opus;
using System;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    public class EchoVcPipeline : VcPipeline
    {
        [Header("Opus Settings")]
        public int maxDataBytesPerPacket = MetaVoiceChatConstants.MaxPacketSize;
        [Tooltip("0 gives the fastest encoding but lower quality, while 10 gives the highest quality but slower encoding.")]
        [Range(0, 10)] public int complexity = 3;
        public bool isMusic = false;
        [Header("Advanced Opus Overrides (not recommended)")]
        [Header("Do research before changing anything here!")]
        [Tooltip("Whether to override the default Opus settings. Only change this if you know what you're doing!")]
        public bool shouldOverride = false;
        [Tooltip("Optimizes the codec for a particular application. If sending speech, use VoIP. If sending music, use Audio.")]
        public OpusApplication overrideApplication = OpusApplication.OPUS_APPLICATION_VOIP;
        [Tooltip("The audio bandwidth that will be used.")]
        public OpusBandwidth overrideBandwidth = OpusBandwidth.OPUS_BANDWIDTH_AUTO;
        [Tooltip("The Opus encoding mode to use.")]
        public OpusMode overrideMode = OpusMode.MODE_AUTO;
        [Tooltip("Hints the expected signal type to the encoder.")]
        public OpusSignal overrideSignal = OpusSignal.OPUS_SIGNAL_AUTO;


        public OnAudioFilterReadVcOutput[] outputs;

        private readonly OpusEncoderVcProcessor encoder = new(default);
        private readonly OpusDecoderVcDataProcessor decoder = new();

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            //var userConfig = encoder.UserConfig;
            //OpusUserConfig targetUserConfig = new(maxDataBytesPerPacket, complexity, isMusic, shouldOverride, overrideApplication, overrideBandwidth, overrideMode, overrideSignal);
            //if (!userConfig.Equals(targetUserConfig))
            //{
            //    encoder.UserConfig = targetUserConfig;
            //}

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
