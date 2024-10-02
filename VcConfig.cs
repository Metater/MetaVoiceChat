using System;
using Concentus.Enums;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    [Serializable]
    public class VcConfig
    {
        public const int BitsPerSample = 16;
        public const int SamplesPerSecond = 16_000;
        public const int ClipLoopSeconds = 1;
        public const int SamplesPerClip = SamplesPerSecond * ClipLoopSeconds;

        public const OpusBandwidth Bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
        public const OpusBandwidth MaxBandwidth = Bandwidth;
        public const OpusMode Mode = OpusMode.MODE_SILK_ONLY;

        [Tooltip("Optimizes the codec for a particular application. The default is VOIP.")]
        public OpusApplication application = OpusApplication.OPUS_APPLICATION_VOIP;

        [Tooltip("0 gives the fastest encoding but lower quality, while 10 gives the highest quality but slower encoding. The default is 10. 10 is still pretty darn fast.")]
        [Range(0, 10)]
        public int complexity = 10;

        [Tooltip("The size of the groups of networked audio samples in milliseconds. The only valid choices are 10ms (160 samples), 20ms (320 samples), and 40ms (640 samples). The default is 20ms (320 samples). Longer frame sizes reduce network traffic but increase susceptibility to dropped packets and introduce more latency in the audio output buffer.")]
        public OpusFramesize framesize = OpusFramesize.OPUS_FRAMESIZE_20_MS;

        [Tooltip("Hints the expected signal type to the encoder. The default is voice.")]
        public OpusSignal signal = OpusSignal.OPUS_SIGNAL_VOICE;

        [Tooltip("The time window in which the RMS jitter values are calculated. The units are seconds.")]
        [Range(0.040f, 1.200f)]
        public float jitterTimeWindow = 0.240f; // 240ms is divisible by 10ms, 20ms, and 40ms -- does divisibility matter, IDK.
        [Tooltip("The mean time offset calculation window size used for jitter calculation. The units are updates.")]
        [Range(10, 1000)]
        public int jitterMeanOffsetWindow = 100;

        [Tooltip("The minimum number of fractional frames that are kept in the output buffer between the read and write positions. Lower means less latency, but too low means the output audio buffer read and write positions may overlap and do strange things. The units are fractional frames.")]
        [Range(1, 10)]
        public float outputMinBufferSize = 2.0f;

        [NonSerialized] public int framePeriodMs;
        [NonSerialized] public int framesPerSecond;
        [NonSerialized] public float secondsPerFrame;
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
                _ => throw new NotSupportedException("Unknown Opus framesize encountered.")
            };

            framesPerSecond = 1000 / framePeriodMs;
            secondsPerFrame = framePeriodMs / 1000f;

            samplesPerFrame = SamplesPerSecond / framesPerSecond;

            framesPerClip = framesPerSecond * ClipLoopSeconds;
        }
    }
}