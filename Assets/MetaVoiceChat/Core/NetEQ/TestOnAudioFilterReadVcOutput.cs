//#define LOG_TestOnAudioFilterReadVcOutput

using System;
using UnityEngine;

namespace MetaVoiceChat.Core.NetEQ
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OnAudioFilterReadVcOutput))]
    public sealed class TestOnAudioFilterReadVcOutput : MonoBehaviour
    {
        [Header("Playback")]
        [SerializeField] private bool feedOnEnable = true;
        [SerializeField] private bool feedInUpdate = true;
        [SerializeField] private bool feedInFixedUpdate = false;
        [SerializeField] private bool sendSilence = false;
        [SerializeField] private bool sendNullSilenceFrames = true;
        [SerializeField, Min(32)] private int maxFramesPerUpdate = 32;
        [SerializeField, Min(0)] private int prerollFramesOnFirstUpdate = 6;

        [Header("Frame")]
        [SerializeField] private int inputSampleRate = 48000;
        [SerializeField, Range(1, 2)] private int inputChannels = 1;
        [SerializeField, Min(1)] private int frameDurationMs = 10;
        [SerializeField] private ushort initialSequenceNumber;
        [SerializeField] private uint initialTimestamp;

        [Header("Sine")]
        [SerializeField, Min(0f)] private float frequencyHz = 440f;
        [SerializeField, Range(0f, 1f)] private float amplitude = 0.2f;
        [SerializeField, Range(-1f, 1f)] private float dcOffset;
        [SerializeField, Range(-180f, 180f)] private float rightChannelPhaseOffsetDegrees;
        [SerializeField, Min(0f)] private float rightChannelFrequencyMultiplier = 1f;
        [SerializeField] private bool resetPhaseOnEnable = true;

        [Header("Two Channel Test Mode")]
        [SerializeField] private bool twoChannelDifferentSines;
        [SerializeField, Min(0f)] private float rightChannelFrequencyHz = 660f;
        [SerializeField, Range(0f, 1f)] private float rightChannelAmplitude = 0.2f;
        [SerializeField, Range(-1f, 1f)] private float rightChannelDcOffset;

        [Header("Debug")]
        [SerializeField] private bool exposeRuntimeLatency;
#if LOG_TestOnAudioFilterReadVcOutput
        [SerializeField] private bool logGeneratorDiagnostics = true;
        [SerializeField, Min(0.1f)] private float generatorDiagnosticsLogIntervalSeconds = 1f;
#endif
        [SerializeField] private int currentNetEqBufferMs;
        [SerializeField] private int receiveToEarLatencyMs;

        private OnAudioFilterReadVcOutput output;
        private float[] frameBuffer = Array.Empty<float>();
        private double frameAccumulator;
        private double leftPhase;
        private double rightPhase;
        private ushort sequenceNumber;
        private uint timestamp;
        private bool isFeeding;
        private int pendingPrerollFrames;
#if LOG_TestOnAudioFilterReadVcOutput
        private int sentFrameCount;
        private double nextGeneratorDiagnosticsLogTime;
#endif

        public bool IsFeeding
        {
            get { return isFeeding; }
            set { isFeeding = value; }
        }

        public void StartFeeding()
        {
            EnsureOutput();
            isFeeding = true;
        }

        public void StopFeeding()
        {
            isFeeding = false;
            frameAccumulator = 0.0;
        }

        public void ResetGenerator()
        {
            frameAccumulator = 0.0;
            leftPhase = 0.0;
            rightPhase = DegreesToRadians(rightChannelPhaseOffsetDegrees);
            sequenceNumber = initialSequenceNumber;
            timestamp = initialTimestamp;
            pendingPrerollFrames = prerollFramesOnFirstUpdate;
        }

        public void SendOneFrame()
        {
            EnsureOutput();
            CacheValidatedSettings(out int sampleRate, out int channels, out int frameSamplesPerChannel);
            SendFrame(sampleRate, channels, frameSamplesPerChannel);
        }

        private void OnEnable()
        {
            EnsureOutput();
            if (resetPhaseOnEnable)
            {
                ResetGenerator();
            }
            else
            {
                sequenceNumber = initialSequenceNumber;
                timestamp = initialTimestamp;
                pendingPrerollFrames = prerollFramesOnFirstUpdate;
            }

            isFeeding = feedOnEnable;
        }

        private void OnDisable()
        {
            StopFeeding();
        }

        private void Update()
        {
            if (exposeRuntimeLatency && output != null)
            {
                currentNetEqBufferMs = output.CurrentBufferSizeMs;
                receiveToEarLatencyMs = output.GetEstimatedLocalReceiveToDspLatencyMs();
            }

#if LOG_TestOnAudioFilterReadVcOutput
            LogGeneratorDiagnosticsIfNeeded();
#endif

            if (!feedInUpdate || !isFeeding)
            {
                return;
            }
            PumpFrames(Time.unscaledDeltaTime);
        }

        private void FixedUpdate()
        {
            if (!feedInFixedUpdate || !isFeeding)
            {
                return;
            }

            PumpFrames(Time.fixedUnscaledDeltaTime);
        }

        private void PumpFrames(float deltaTime)
        {
            CacheValidatedSettings(out int sampleRate, out int channels, out int frameSamplesPerChannel);

            double frameSeconds = frameSamplesPerChannel / (double)sampleRate;
            frameAccumulator += deltaTime;

            int framesSent = 0;
            while (pendingPrerollFrames > 0 && framesSent < maxFramesPerUpdate)
            {
                SendFrame(sampleRate, channels, frameSamplesPerChannel);
                pendingPrerollFrames--;
                framesSent++;
            }

            while (frameAccumulator >= frameSeconds && framesSent < maxFramesPerUpdate)
            {
                SendFrame(sampleRate, channels, frameSamplesPerChannel);
                frameAccumulator -= frameSeconds;
                framesSent++;
            }

            if (framesSent == maxFramesPerUpdate && frameAccumulator >= frameSeconds)
            {
                frameAccumulator = Math.Min(frameAccumulator, frameSeconds);
            }
        }

        private void OnValidate()
        {
            inputChannels = Mathf.Clamp(inputChannels, 1, 2);
            if (twoChannelDifferentSines)
            {
                inputChannels = 2;
            }

            frameDurationMs = Math.Max(1, frameDurationMs);
            maxFramesPerUpdate = Math.Max(1, maxFramesPerUpdate);
            prerollFramesOnFirstUpdate = Math.Max(0, prerollFramesOnFirstUpdate);
#if LOG_TestOnAudioFilterReadVcOutput
            generatorDiagnosticsLogIntervalSeconds = Math.Max(0.1f, generatorDiagnosticsLogIntervalSeconds);
#endif
            amplitude = Mathf.Clamp01(amplitude);
            dcOffset = Mathf.Clamp(dcOffset, -1f, 1f);
            rightChannelPhaseOffsetDegrees = Mathf.Clamp(rightChannelPhaseOffsetDegrees, -180f, 180f);
            rightChannelFrequencyMultiplier = Math.Max(0f, rightChannelFrequencyMultiplier);
            rightChannelFrequencyHz = Math.Max(0f, rightChannelFrequencyHz);
            rightChannelAmplitude = Mathf.Clamp01(rightChannelAmplitude);
            rightChannelDcOffset = Mathf.Clamp(rightChannelDcOffset, -1f, 1f);
        }

        private void EnsureOutput()
        {
            if (output == null)
            {
                output = GetComponent<OnAudioFilterReadVcOutput>();
            }
        }

        private void CacheValidatedSettings(out int sampleRate, out int channels, out int frameSamplesPerChannel)
        {
            sampleRate = inputSampleRate;
            channels = twoChannelDifferentSines ? 2 : Math.Clamp(inputChannels, 1, 2);
            frameSamplesPerChannel = Math.Max(1, sampleRate * Math.Max(1, frameDurationMs) / 1000);
        }

        private void SendFrame(int sampleRate, int channels, int frameSamplesPerChannel)
        {
            int frameSize = frameSamplesPerChannel * channels;
            ReadOnlySpan<float> frame = ReadOnlySpan<float>.Empty;

            if (!sendSilence || !sendNullSilenceFrames)
            {
                float[] frameArray = EnsureFrameBuffer(frameSize);
                FillFrame(frameArray, frameSamplesPerChannel, sampleRate, channels, !sendSilence);
                frame = frameArray;
            }

            output.Process(
                frame,
                frameSize,
                sampleRate,
                channels,
                sequenceNumber,
                timestamp);

            sequenceNumber++;
            timestamp = unchecked(timestamp + (uint)frameSamplesPerChannel);
#if LOG_TestOnAudioFilterReadVcOutput
            sentFrameCount++;
#endif
        }

        private float[] EnsureFrameBuffer(int frameSize)
        {
            if (frameBuffer.Length != frameSize)
            {
                frameBuffer = new float[frameSize];
            }

            return frameBuffer;
        }

        private void FillFrame(float[] frame, int samplesPerChannel, int sampleRate, int channels, bool audible)
        {
            if (!audible)
            {
                Array.Clear(frame, 0, samplesPerChannel * channels);
                return;
            }

            double leftStep = TwoPi * frequencyHz / sampleRate;
            double rightStep = twoChannelDifferentSines
                ? TwoPi * rightChannelFrequencyHz / sampleRate
                : leftStep * rightChannelFrequencyMultiplier;
            float rightAmplitude = twoChannelDifferentSines ? rightChannelAmplitude : amplitude;
            float rightOffset = twoChannelDifferentSines ? rightChannelDcOffset : dcOffset;

            if (channels == 1)
            {
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    frame[i] = ClampSample((float)(Math.Sin(leftPhase) * amplitude + dcOffset));
                    leftPhase = WrapPhase(leftPhase + leftStep);
                }

                rightPhase = WrapPhase(leftPhase + DegreesToRadians(rightChannelPhaseOffsetDegrees));
                return;
            }

            for (int frameIndex = 0, sampleIndex = 0; frameIndex < samplesPerChannel; frameIndex++, sampleIndex += 2)
            {
                frame[sampleIndex] = ClampSample((float)(Math.Sin(leftPhase) * amplitude + dcOffset));
                frame[sampleIndex + 1] = ClampSample((float)(Math.Sin(rightPhase) * rightAmplitude + rightOffset));

                leftPhase = WrapPhase(leftPhase + leftStep);
                rightPhase = WrapPhase(rightPhase + rightStep);
            }
        }

        private static float ClampSample(float sample)
        {
            return Mathf.Clamp(sample, -1f, 1f);
        }

        private static double WrapPhase(double phase)
        {
            if (phase >= TwoPi)
            {
                phase -= TwoPi * Math.Floor(phase / TwoPi);
            }

            return phase;
        }

        private static double DegreesToRadians(float degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static int ClosestSupportedSampleRate(int sampleRate)
        {
            int closest = SupportedSampleRates[0];
            int closestDistance = Math.Abs(sampleRate - closest);
            for (int i = 1; i < SupportedSampleRates.Length; i++)
            {
                int distance = Math.Abs(sampleRate - SupportedSampleRates[i]);
                if (distance < closestDistance)
                {
                    closest = SupportedSampleRates[i];
                    closestDistance = distance;
                }
            }

            return closest;
        }

#if LOG_TestOnAudioFilterReadVcOutput
        private void LogGeneratorDiagnosticsIfNeeded()
        {
            if (!logGeneratorDiagnostics || Time.unscaledTimeAsDouble < nextGeneratorDiagnosticsLogTime)
            {
                return;
            }

            nextGeneratorDiagnosticsLogTime = Time.unscaledTimeAsDouble + Math.Max(0.1f, generatorDiagnosticsLogIntervalSeconds);
            CacheValidatedSettings(out int sampleRate, out int channels, out int frameSamplesPerChannel);

            UnityEngine.Debug.Log(
                $"{nameof(TestOnAudioFilterReadVcOutput)} diagnostics " +
                $"feeding={isFeeding} feedInUpdate={feedInUpdate} feedInFixedUpdate={feedInFixedUpdate} sent={sentFrameCount} " +
                $"sampleRate={sampleRate} channels={channels} frameSamplesPerChannel={frameSamplesPerChannel} " +
                $"frameMs={frameDurationMs} accumulator={frameAccumulator:0.0000} pendingPreroll={pendingPrerollFrames} " +
                $"silence={sendSilence} nullSilence={sendNullSilenceFrames} twoChannelDifferentSines={twoChannelDifferentSines} " +
                $"frequency={frequencyHz:0.##} amplitude={amplitude:0.###}",
                this);
        }
#endif

        private const double TwoPi = Math.PI * 2.0;
        private static readonly int[] SupportedSampleRates = { 8000, 16000, 32000, 48000 };
    }
}
