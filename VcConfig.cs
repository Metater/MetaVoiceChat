using System;
using Concentus.Enums;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    [Serializable]
    public class VcConfig
    {
        public const int BitsPerSample = sizeof(short) * 8; // 16
        public const int SamplesPerSecond = 16000;
        public const int ClipLoopSeconds = 1;
        public const int SamplesPerClip = SamplesPerSecond * ClipLoopSeconds;

        public const OpusBandwidth Bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
        public const OpusBandwidth MaxBandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
        public const OpusMode Mode = OpusMode.MODE_SILK_ONLY;

        [Tooltip("Optimizes the codec for a particular application. The default is VOIP.")]
        public OpusApplication application = OpusApplication.OPUS_APPLICATION_VOIP;

        [Tooltip("0 gives the fastest encoding but lower quality, while 10 gives the highest quality but slower encoding. The default is 10.")]
        [Range(0, 10)]
        public int complexity = 10;

        [Tooltip("The size of the groups of audio samples sent in miliiseconds. The default is 20ms.")]
        public OpusFramesize framesize = OpusFramesize.OPUS_FRAMESIZE_20_MS;

        [Tooltip("Hints to the encoder the expected signal type. The default is voice.")]
        public OpusSignal signal = OpusSignal.OPUS_SIGNAL_VOICE;

        [NonSerialized] public int framePeriodMs;
        [NonSerialized] public int framesPerSecond;
        [NonSerialized] public int samplesPerFrame;
        [NonSerialized] public int framesPerClip;

        public void Init()
        {
            framePeriodMs = framesize switch
            {
                OpusFramesize.OPUS_FRAMESIZE_2_5_MS => throw new NotSupportedException("2.5ms Opus framesize not supported."),
                OpusFramesize.OPUS_FRAMESIZE_5_MS => throw new NotSupportedException("5ms Opus framesize not supported."),
                OpusFramesize.OPUS_FRAMESIZE_10_MS => 10,
                OpusFramesize.OPUS_FRAMESIZE_20_MS => 20,
                OpusFramesize.OPUS_FRAMESIZE_40_MS => 40,
                OpusFramesize.OPUS_FRAMESIZE_60_MS => throw new NotSupportedException("60ms Opus framesize not supported."),
                OpusFramesize.OPUS_FRAMESIZE_ARG => throw new NotSupportedException("Argument Opus framesize not supported."),
                OpusFramesize.OPUS_FRAMESIZE_VARIABLE => throw new NotSupportedException("Variable Opus framesize not supported."),
                _ => throw new NotSupportedException("Unknown Opus framesize found.")
            };

            framesPerSecond = 1000 / framePeriodMs;

            samplesPerFrame = SamplesPerSecond / framesPerSecond;

            framesPerClip = framesPerSecond * ClipLoopSeconds;
        }
    }
}