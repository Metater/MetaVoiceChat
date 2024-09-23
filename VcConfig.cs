using System;
using Concentus.Enums;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    [Serializable]
    public class VcConfig
    {
        public const int ChannelCount = 1;
        public const int BitsPerSample = sizeof(short) * 8; // 16
        public const int SamplesPerSecond = 16000;
        public const int ClipLoopSeconds = 1;

        public GeneralConfig general;
        public OpusConfig opus;

        [Header("Output")]
        [SerializeField] private Vector2Int outputLagSegmentsRange = new(4, 6);
        [SerializeField] private int outputMaxLeadSegments = 4;
        [SerializeField] private int outputSegmentLifetime = 16;
        [SerializeField] private float outputPitchP = 0.01f; // Units = percent per segment of error
        [SerializeField] private float outputErrorBias = 0; // Units = percent, 0.01f

        //[Header("Detection")]
        //[SerializeField] private float detectionValue = 0.002f;
        //[SerializeField] private float detectionPercentage = 0.05f;
        //[SerializeField] private float detectionLatchSeconds = 0.1f;

        //public float DetectionValue => detectionValue;
        //public float DetectionPercentage => detectionPercentage;
        //public float DetectionLatchSeconds => detectionLatchSeconds;

        public int OutputSegmentCount => general.framesPerSecond * ClipLoopSeconds;
        public int OutputSampleCount => general.samplesPerFrame * OutputSegmentCount;
        public Vector2Int OutputLagSegmentsRange => outputLagSegmentsRange;
        public int OutputLagSegmentsTarget => (outputLagSegmentsRange.x + outputLagSegmentsRange.y) / 2;
        public int OutputMaxLeadSegments => outputMaxLeadSegments;
        public float OutputMaxNegativeLatency => (float)OutputMaxLeadSegments / general.framesPerSecond;
        public int OutputSegmentLifetime => outputSegmentLifetime;
        public float OutputPitchP => outputPitchP;
        public float OutputErrorBias => outputErrorBias;

        [Serializable]
        public class OpusConfig
        {
            public const OpusBandwidth Bandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            public const OpusBandwidth MaxBandwidth = OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND;
            public const OpusMode Mode = OpusMode.MODE_SILK_ONLY;

            public OpusApplication application = OpusApplication.OPUS_APPLICATION_VOIP;

            [Tooltip("0 gives the fastest encoding but lower quality, while 10 gives the highest quality but slower encoding.")]
            [Range(0, 10)]
            public int complexity = 10;

            public OpusFramesize framesize = OpusFramesize.OPUS_FRAMESIZE_20_MS;

            public OpusSignal signal = OpusSignal.OPUS_SIGNAL_VOICE;
        }

        [Serializable]
        public class GeneralConfig
        {
            public VcInputFilter inputFilter;
            public AudioSource outputAudioSource;

            [NonSerialized] public int framePeriodMs;
            [NonSerialized] public int framesPerSecond;
            [NonSerialized] public int samplesPerFrame;
            [NonSerialized] public MonoBehaviour coroutineProvider;

            public void Init(VcConfig config, MonoBehaviour coroutineProvider)
            {
                outputAudioSource.dopplerLevel = 0;

                framePeriodMs = config.opus.framesize switch
                {
                    OpusFramesize.OPUS_FRAMESIZE_2_5_MS => throw new NotSupportedException("2.5ms Opus framesize not supported."),
                    OpusFramesize.OPUS_FRAMESIZE_5_MS => throw new NotSupportedException("5ms Opus framesize not supported."),
                    OpusFramesize.OPUS_FRAMESIZE_10_MS => 10,
                    OpusFramesize.OPUS_FRAMESIZE_20_MS => 20,
                    OpusFramesize.OPUS_FRAMESIZE_40_MS => 40,
                    OpusFramesize.OPUS_FRAMESIZE_60_MS => throw new NotSupportedException("60ms Opus framesize not supported."),
                    OpusFramesize.OPUS_FRAMESIZE_ARG => throw new NotSupportedException("Argument Opus framesize not supported."),
                    OpusFramesize.OPUS_FRAMESIZE_VARIABLE => throw new NotSupportedException("Variable Opus framesize not supported."),
                    _ => throw new NotSupportedException("Unknown Opus framesize not supported.")
                };

                framesPerSecond = 1000 / framePeriodMs;

                samplesPerFrame = SamplesPerSecond * ChannelCount / framesPerSecond;

                this.coroutineProvider = coroutineProvider;
            }
        }
    }
}