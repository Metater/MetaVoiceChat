using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    [Serializable]
    public class VcConfig
    {
        public MonoBehaviour CoroutineProvider { get; set; }

        [Header("General")]
        [SerializeField] private VcAudioProcessor audioProcessor;
        [SerializeField] private AudioSource outputAudioSource;
        [SerializeField] private int samplesPerSecond = 16000;
        [SerializeField] private int segmentPeriodMs = 25;
        [SerializeField] private int loopSeconds = 1;

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

        public VcAudioProcessor AudioProcessor => audioProcessor;
        public AudioSource OutputAudioSource => outputAudioSource;
        public int SamplesPerSecond => samplesPerSecond;
        public int SegmentPeriodMs => segmentPeriodMs;
        public int ChannelCount => 1;
        public int SegmentsPerSecond => 1000 / SegmentPeriodMs;
        public int SamplesPerSegment => SamplesPerSecond * ChannelCount / SegmentsPerSecond;

        //public float DetectionValue => detectionValue;
        //public float DetectionPercentage => detectionPercentage;
        //public float DetectionLatchSeconds => detectionLatchSeconds;

        public int LoopSeconds => loopSeconds;

        public int OutputSegmentCount => SegmentsPerSecond * LoopSeconds;
        public int OutputSampleCount => SamplesPerSegment * OutputSegmentCount;
        public Vector2Int OutputLagSegmentsRange => outputLagSegmentsRange;
        public int OutputLagSegmentsTarget => (outputLagSegmentsRange.x + outputLagSegmentsRange.y) / 2;
        public int OutputMaxLeadSegments => outputMaxLeadSegments;
        public float OutputMaxNegativeLatency => (float)OutputMaxLeadSegments / SegmentsPerSecond;
        public int OutputSegmentLifetime => outputSegmentLifetime;
        public float OutputPitchP => outputPitchP;
        public float OutputErrorBias => outputErrorBias;
    }
}