#define META_VOICE_CHAT_AUDIO_LOGGING

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace MetaVoiceChat.Core
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
#if META_VOICE_CHAT_AUDIO_LOGGING
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
#if META_VOICE_CHAT_AUDIO_LOGGING
        private int sentFrameCount;
        private float nextGeneratorDiagnosticsLogTime;
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
                receiveToEarLatencyMs = output.GetReceiveToEarLatencyMs();
            }

#if META_VOICE_CHAT_AUDIO_LOGGING
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
            inputSampleRate = ClosestSupportedSampleRate(inputSampleRate);
            inputChannels = Mathf.Clamp(inputChannels, 1, 2);
            if (twoChannelDifferentSines)
            {
                inputChannels = 2;
            }

            frameDurationMs = Math.Max(1, frameDurationMs);
            maxFramesPerUpdate = Math.Max(1, maxFramesPerUpdate);
            prerollFramesOnFirstUpdate = Math.Max(0, prerollFramesOnFirstUpdate);
#if META_VOICE_CHAT_AUDIO_LOGGING
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
            sampleRate = ClosestSupportedSampleRate(inputSampleRate);
            channels = twoChannelDifferentSines ? 2 : Math.Clamp(inputChannels, 1, 2);
            frameSamplesPerChannel = Math.Max(1, sampleRate * Math.Max(1, frameDurationMs) / 1000);
        }

        private void SendFrame(int sampleRate, int channels, int frameSamplesPerChannel)
        {
            int frameSize = frameSamplesPerChannel * channels;
            float[] frame = null;

            if (!sendSilence || !sendNullSilenceFrames)
            {
                frame = EnsureFrameBuffer(frameSize);
                FillFrame(frame, frameSamplesPerChannel, sampleRate, channels, !sendSilence);
            }

            output.ReceiveFrame(
                frame,
                frameSize,
                sampleRate,
                channels,
                sequenceNumber,
                timestamp);

            sequenceNumber++;
            timestamp = unchecked(timestamp + (uint)frameSamplesPerChannel);
#if META_VOICE_CHAT_AUDIO_LOGGING
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

#if META_VOICE_CHAT_AUDIO_LOGGING
        private void LogGeneratorDiagnosticsIfNeeded()
        {
            if (!logGeneratorDiagnostics || Time.unscaledTime < nextGeneratorDiagnosticsLogTime)
            {
                return;
            }

            nextGeneratorDiagnosticsLogTime = Time.unscaledTime + Math.Max(0.1f, generatorDiagnosticsLogIntervalSeconds);
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

#if UNITY_EDITOR
        public static void RunAutomaticMatrixFromCommandLine()
        {
            bool passed = RunAutomaticMatrix();
            EditorApplication.Exit(passed ? 0 : 1);
        }

        [MenuItem("Meta Voice Chat/Run OnAudioFilterRead Matrix")]
        public static void RunAutomaticMatrixFromMenu()
        {
            AudioMatrixRunner runner = new AudioMatrixRunner();
            runner.StartEditorRunWithPauses();
        }

        public static bool RunAutomaticMatrix()
        {
            AudioMatrixRunner runner = new AudioMatrixRunner();
            AudioMatrixResult result = runner.Run();
            LogResult(result);
            return result.Passed;
        }

        private static void LogResult(AudioMatrixResult result)
        {
            string report = result.BuildReport();
            if (result.Passed)
            {
                UnityEngine.Debug.Log(report);
            }
            else
            {
                UnityEngine.Debug.LogError(report);
            }
        }
#endif

        private const double TwoPi = Math.PI * 2.0;
        private static readonly int[] SupportedSampleRates = { 8000, 16000, 32000, 48000 };

#if UNITY_EDITOR
        private sealed class AudioMatrixRunner
        {
            private static readonly MethodInfo OutputOnEnable = OutputMethod("OnEnable");
            private static readonly MethodInfo OutputOnDisable = OutputMethod("OnDisable");
            private static readonly MethodInfo OutputOnDestroy = OutputMethod("OnDestroy");
            private static readonly MethodInfo OutputOnAudioFilterRead = OutputMethod("OnAudioFilterRead");
            private static readonly FieldInfo CachedOutputSampleRate = OutputField("cachedOutputSampleRate");
            private static readonly FieldInfo ResetOutputConversionRequested = OutputField("resetOutputConversionRequested");
            private static readonly FieldInfo DroppedInvalidFrameCount = OutputField("droppedInvalidFrameCount");
            private static readonly FieldInfo AudioThreadExceptionCount = OutputField("audioThreadExceptionCount");
            private static readonly FieldInfo LastOutputPeakPpm = OutputField("lastOutputPeakPpm");

            private const int PauseBetweenCasesMs = 10000;
            private const double PauseBetweenCasesSeconds = PauseBetweenCasesMs / 1000.0;
            private readonly AudioMatrixResult result = new AudioMatrixResult();

            public AudioMatrixResult Run()
            {
                List<MatrixCase> cases = BuildCases();
                for (int i = 0; i < cases.Count; i++)
                {
                    RunCase(cases[i].Name, cases[i].Body);
                }

                return result;
            }

            public void StartEditorRunWithPauses()
            {
                List<MatrixCase> cases = BuildCases();
                int index = 0;
                double nextRunTime = EditorApplication.timeSinceStartup;

                UnityEngine.Debug.Log(
                    $"META_VOICE_CHAT_MATRIX start cases={cases.Count} pauseSeconds={PauseBetweenCasesSeconds:0}");

                EditorApplication.CallbackFunction update = null;
                update = () =>
                {
                    if (EditorApplication.timeSinceStartup < nextRunTime)
                    {
                        return;
                    }

                    if (index >= cases.Count)
                    {
                        EditorApplication.update -= update;
                        LogResult(result);
                        return;
                    }

                    MatrixCase matrixCase = cases[index++];
                    RunCase(matrixCase.Name, matrixCase.Body);
                    nextRunTime = EditorApplication.timeSinceStartup + PauseBetweenCasesSeconds;
                    UnityEngine.Debug.Log(
                        $"META_VOICE_CHAT_MATRIX_CASE stabilizing {PauseBetweenCasesMs / 1000} seconds after {matrixCase.Name}");
                };

                EditorApplication.update += update;
            }

            private List<MatrixCase> BuildCases()
            {
                return new List<MatrixCase>
                {
                    new MatrixCase("input 8 kHz mono", output => RunSignalCase(output, 8000, 1, 48000, 2, 12)),
                    new MatrixCase("input 16 kHz mono", output => RunSignalCase(output, 16000, 1, 48000, 2, 12)),
                    new MatrixCase("input 32 kHz mono", output => RunSignalCase(output, 32000, 1, 48000, 2, 12)),
                    new MatrixCase("input 48 kHz mono", output => RunSignalCase(output, 48000, 1, 48000, 2, 12)),
                    new MatrixCase("input 48 kHz stereo", output => RunSignalCase(output, 48000, 2, 48000, 2, 12)),
                    new MatrixCase("invalid 44.1 kHz NetEQ input", RunInvalid44100Case),
                    new MatrixCase("alternating mono/stereo metadata", RunAlternatingChannelsCase),
                    new MatrixCase("alternating 16/48 kHz metadata", RunAlternatingRatesCase),
                    new MatrixCase("Unity mix rate 44.1 kHz", output => RunSignalCase(output, 48000, 1, 44100, 2, 16)),
                    new MatrixCase("Unity mix rate 48 kHz", output => RunSignalCase(output, 48000, 1, 48000, 2, 12)),
                    new MatrixCase("Unity mix rate 96 kHz", output => RunSignalCase(output, 48000, 1, 96000, 2, 24)),
                    new MatrixCase("enable/disable while packets arrive", RunEnableDisableWhilePacketsArriveCase),
                    new MatrixCase("destroy while ReceiveFrame is executing", RunDestroyWhileReceiveFrameExecutesCase),
                    new MatrixCase("destroy while GetAudio is executing", RunDestroyWhileGetAudioExecutesCase),
                    new MatrixCase("default output changes between stereo and mono", RunOutputChannelChangeCase),
                    new MatrixCase("reordering", output => RunNetworkPatternCase(output, NetworkPattern.Reordered)),
                    new MatrixCase("duplicate packets", output => RunNetworkPatternCase(output, NetworkPattern.Duplicated)),
                    new MatrixCase("sequence wrap", output => RunNetworkPatternCase(output, NetworkPattern.SequenceWrap)),
                    new MatrixCase("timestamp wrap", output => RunNetworkPatternCase(output, NetworkPattern.TimestampWrap)),
                    new MatrixCase("100-500 ms burst arrival", RunBurstArrivalCase),
                    new MatrixCase("long stall followed by a large backlog", RunLongStallBacklogCase),
                    new MatrixCase("packet-duration transition", RunPacketDurationTransitionCase),
                    new MatrixCase("stream index/timestamp reset", RunTimestampResetCase)
                };
            }

            private void RunCase(string name, Action<OnAudioFilterReadVcOutput> body)
            {
                GameObject gameObject = new GameObject("MetaVoiceChatAudioMatrix");
                try
                {
                    UnityEngine.Debug.Log("META_VOICE_CHAT_MATRIX_CASE start " + name);
                    gameObject.AddComponent<AudioSource>();
                    OnAudioFilterReadVcOutput output = gameObject.AddComponent<OnAudioFilterReadVcOutput>();
                    Invoke(OutputOnEnable, output);
                    body(output);
                    AssertNoAudioThreadExceptions(output);
                    result.Pass(name);
                    UnityEngine.Debug.Log("META_VOICE_CHAT_MATRIX_CASE pass " + name);
                }
                catch (Exception exception)
                {
                    Exception unwrapped = Unwrap(exception);
                    string failure = unwrapped.GetType().Name + ": " + unwrapped.Message;
                    result.Fail(name, failure);
                    UnityEngine.Debug.LogError("META_VOICE_CHAT_MATRIX_CASE fail " + name + " - " + failure);
                }
                finally
                {
                    OnAudioFilterReadVcOutput output = gameObject.GetComponent<OnAudioFilterReadVcOutput>();
                    if (output != null)
                    {
                        TryInvoke(OutputOnDisable, output);
                        TryInvoke(OutputOnDestroy, output);
                    }

                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            private void RunSignalCase(
                OnAudioFilterReadVcOutput output,
                int inputSampleRate,
                int inputChannels,
                int outputSampleRate,
                int outputChannels,
                int callbackCount)
            {
                SetOutputFormat(output, outputSampleRate);
                SignalCursor cursor = new SignalCursor(inputSampleRate, inputChannels, 0, 0);
                for (int i = 0; i < 24; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, i == 0);
                }

                float peak = PumpAudio(output, outputSampleRate, outputChannels, callbackCount);
                if (peak <= 0.000001f)
                {
                    throw new InvalidOperationException("No non-zero output was observed.");
                }
            }

            private void RunInvalid44100Case(OnAudioFilterReadVcOutput output)
            {
                int before = ReadInt(output, DroppedInvalidFrameCount);
                SignalCursor cursor = new SignalCursor(44100, 1, 0, 0);
                SendDeterministicFrame(output, cursor, 10, true);
                if (ReadInt(output, DroppedInvalidFrameCount) <= before)
                {
                    throw new InvalidOperationException("44.1 kHz input was not rejected.");
                }

                PumpAudio(output, 48000, 2, 2);
            }

            private void RunAlternatingChannelsCase(OnAudioFilterReadVcOutput output)
            {
                SetOutputFormat(output, 48000);
                SignalCursor mono = new SignalCursor(48000, 1, 100, 1000);
                SignalCursor stereo = new SignalCursor(48000, 2, 100, 1000);
                for (int i = 0; i < 18; i++)
                {
                    SendDeterministicFrame(output, (i & 1) == 0 ? mono : stereo, 10, i == 0);
                }

                PumpAudio(output, 48000, 2, 8);
            }

            private void RunAlternatingRatesCase(OnAudioFilterReadVcOutput output)
            {
                SetOutputFormat(output, 48000);
                SignalCursor low = new SignalCursor(16000, 1, 200, 2000);
                SignalCursor high = new SignalCursor(48000, 1, 200, 2000);
                for (int i = 0; i < 18; i++)
                {
                    SendDeterministicFrame(output, (i & 1) == 0 ? low : high, 10, i == 0);
                }

                PumpAudio(output, 48000, 2, 8);
            }

            private void RunEnableDisableWhilePacketsArriveCase(OnAudioFilterReadVcOutput output)
            {
                SignalCursor cursor = new SignalCursor(48000, 1, 300, 3000);
                for (int i = 0; i < 24; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, i == 0);
                }

                Invoke(OutputOnDisable, output);
                for (int i = 0; i < 24; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, false);
                }

                Invoke(OutputOnEnable, output);
                for (int i = 0; i < 24; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, false);
                }

                PumpAudio(output, 48000, 2, 8);
            }

            private void RunDestroyWhileReceiveFrameExecutesCase(OnAudioFilterReadVcOutput output)
            {
                SignalCursor cursor = new SignalCursor(48000, 1, 400, 4000);
                for (int i = 0; i < 24; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, i == 0);
                }

                Invoke(OutputOnDestroy, output);
                for (int i = 0; i < 24; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, false);
                }

                PumpAudio(output, 48000, 2, 2);
            }

            private void RunDestroyWhileGetAudioExecutesCase(OnAudioFilterReadVcOutput output)
            {
                SignalCursor cursor = new SignalCursor(48000, 1, 500, 5000);
                for (int i = 0; i < 48; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, i == 0);
                }

                PumpAudio(output, 48000, 2, 1);
                Invoke(OutputOnDestroy, output);
                PumpAudio(output, 48000, 2, 2);
            }

            private void RunOutputChannelChangeCase(OnAudioFilterReadVcOutput output)
            {
                SignalCursor cursor = new SignalCursor(48000, 2, 600, 6000);
                for (int i = 0; i < 36; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, i == 0);
                }

                PumpAudio(output, 48000, 2, 4);
                PumpAudio(output, 48000, 1, 4);
                PumpAudio(output, 48000, 2, 4);
            }

            private void RunNetworkPatternCase(OnAudioFilterReadVcOutput output, NetworkPattern pattern)
            {
                SetOutputFormat(output, 48000);
                SignalCursor cursor = new SignalCursor(48000, 1, 65000, 0xFFFFFF00u);
                List<FramePacket> packets = new List<FramePacket>();
                for (int i = 0; i < 24; i++)
                {
                    packets.Add(BuildPacket(cursor, 10, i == 0));
                }

                if (pattern == NetworkPattern.Reordered)
                {
                    for (int i = 0; i + 1 < packets.Count; i += 4)
                    {
                        FramePacket temp = packets[i];
                        packets[i] = packets[i + 1];
                        packets[i + 1] = temp;
                    }
                }

                foreach (FramePacket packet in packets)
                {
                    SendPacket(output, packet);
                    if (pattern == NetworkPattern.Duplicated && (packet.SequenceNumber & 3) == 0)
                    {
                        SendPacket(output, packet);
                    }
                }

                PumpAudio(output, 48000, 2, 12);
            }

            private void RunBurstArrivalCase(OnAudioFilterReadVcOutput output)
            {
                SetOutputFormat(output, 48000);
                SignalCursor cursor = new SignalCursor(48000, 1, 700, 7000);
                for (int burstMs = 100; burstMs <= 500; burstMs += 100)
                {
                    int frames = burstMs / 10;
                    for (int i = 0; i < frames; i++)
                    {
                        SendDeterministicFrame(output, cursor, 10, i == 0 && burstMs == 100);
                    }

                    PumpAudio(output, 48000, 2, 3);
                }
            }

            private void RunLongStallBacklogCase(OnAudioFilterReadVcOutput output)
            {
                SetOutputFormat(output, 48000);
                SignalCursor cursor = new SignalCursor(48000, 1, 800, 8000);
                for (int i = 0; i < 8; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, i == 0);
                }

                System.Threading.Thread.Sleep(300);
                for (int i = 0; i < 80; i++)
                {
                    SendDeterministicFrame(output, cursor, 10, false);
                }

                PumpAudio(output, 48000, 2, 20);
            }

            private void RunPacketDurationTransitionCase(OnAudioFilterReadVcOutput output)
            {
                SetOutputFormat(output, 48000);
                SignalCursor cursor = new SignalCursor(48000, 1, 900, 9000);
                int[] durations = { 10, 20, 30, 60, 10, 20 };
                for (int i = 0; i < durations.Length; i++)
                {
                    SendDeterministicFrame(output, cursor, durations[i], i == 0);
                }

                PumpAudio(output, 48000, 2, 12);
            }

            private void RunTimestampResetCase(OnAudioFilterReadVcOutput output)
            {
                SetOutputFormat(output, 48000);
                SignalCursor first = new SignalCursor(48000, 1, 1000, 10000);
                SignalCursor reset = new SignalCursor(48000, 1, 0, 0);
                for (int i = 0; i < 8; i++)
                {
                    SendDeterministicFrame(output, first, 10, i == 0);
                }

                for (int i = 0; i < 8; i++)
                {
                    SendDeterministicFrame(output, reset, 10, i == 0);
                }

                PumpAudio(output, 48000, 2, 10);
            }

            private static void SendDeterministicFrame(
                OnAudioFilterReadVcOutput output,
                SignalCursor cursor,
                int durationMs,
                bool impulse)
            {
                SendPacket(output, BuildPacket(cursor, durationMs, impulse));
            }

            private static FramePacket BuildPacket(SignalCursor cursor, int durationMs, bool impulse)
            {
                int samplesPerChannel = cursor.SampleRate * durationMs / 1000;
                int sampleCount = samplesPerChannel * cursor.Channels;
                float[] samples = new float[sampleCount];
                double leftStep = TwoPi * 440.0 / cursor.SampleRate;
                double rightStep = TwoPi * 660.0 / cursor.SampleRate;

                for (int frameIndex = 0, sampleIndex = 0; frameIndex < samplesPerChannel; frameIndex++)
                {
                    float left = impulse && frameIndex == 0 ? 0.9f : (float)(Math.Sin(cursor.LeftPhase) * 0.25);
                    cursor.LeftPhase = WrapPhase(cursor.LeftPhase + leftStep);

                    if (cursor.Channels == 1)
                    {
                        samples[sampleIndex++] = left;
                    }
                    else
                    {
                        float right = impulse && frameIndex == 0 ? -0.7f : (float)(Math.Sin(cursor.RightPhase) * 0.2);
                        cursor.RightPhase = WrapPhase(cursor.RightPhase + rightStep);
                        samples[sampleIndex++] = left;
                        samples[sampleIndex++] = right;
                    }
                }

                FramePacket packet = new FramePacket
                {
                    Samples = samples,
                    SampleRate = cursor.SampleRate,
                    Channels = cursor.Channels,
                    SequenceNumber = cursor.SequenceNumber,
                    Timestamp = cursor.Timestamp
                };

                cursor.SequenceNumber++;
                cursor.Timestamp = unchecked(cursor.Timestamp + (uint)samplesPerChannel);
                return packet;
            }

            private static void SendPacket(OnAudioFilterReadVcOutput output, FramePacket packet)
            {
                output.ReceiveFrame(
                    packet.Samples,
                    packet.Samples.Length,
                    packet.SampleRate,
                    packet.Channels,
                    packet.SequenceNumber,
                    packet.Timestamp);
            }

            private static float PumpAudio(
                OnAudioFilterReadVcOutput output,
                int outputSampleRate,
                int outputChannels,
                int callbacks)
            {
                SetOutputFormat(output, outputSampleRate);
                int framesPerCallback = Math.Max(64, outputSampleRate / 100);
                float peak = 0f;

                for (int i = 0; i < callbacks; i++)
                {
                    float[] data = new float[framesPerCallback * outputChannels];
                    for (int sampleIndex = 0; sampleIndex < data.Length; sampleIndex++)
                    {
                        data[sampleIndex] = 1f;
                    }

                    Invoke(OutputOnAudioFilterRead, output, data, outputChannels);
                    peak = Math.Max(peak, Peak(data));
                }

                peak = Math.Max(peak, ReadInt(output, LastOutputPeakPpm) / 1000000f);
                return peak;
            }

            private static void SetOutputFormat(OnAudioFilterReadVcOutput output, int sampleRate)
            {
                CachedOutputSampleRate.SetValue(output, sampleRate);
                ResetOutputConversionRequested.SetValue(output, 1);
            }

            private static void AssertNoAudioThreadExceptions(OnAudioFilterReadVcOutput output)
            {
                int exceptions = ReadInt(output, AudioThreadExceptionCount);
                if (exceptions != 0)
                {
                    throw new InvalidOperationException("Audio thread exception count is " + exceptions + ".");
                }
            }

            private static float Peak(float[] data)
            {
                float peak = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    peak = Math.Max(peak, Math.Abs(data[i]));
                }

                return peak;
            }

            private static int ReadInt(OnAudioFilterReadVcOutput output, FieldInfo field)
            {
                return (int)field.GetValue(output);
            }

            private static void Invoke(MethodInfo method, object target, params object[] args)
            {
                method.Invoke(target, args);
            }

            private static void TryInvoke(MethodInfo method, object target)
            {
                try
                {
                    method.Invoke(target, Array.Empty<object>());
                }
                catch
                {
                }
            }

            private static Exception Unwrap(Exception exception)
            {
                return exception is TargetInvocationException targetInvocationException &&
                    targetInvocationException.InnerException != null
                    ? targetInvocationException.InnerException
                    : exception;
            }

            private static MethodInfo OutputMethod(string methodName)
            {
                return typeof(OnAudioFilterReadVcOutput).GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            private static FieldInfo OutputField(string fieldName)
            {
                return typeof(OnAudioFilterReadVcOutput).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            private enum NetworkPattern
            {
                Reordered,
                Duplicated,
                SequenceWrap,
                TimestampWrap
            }

            private readonly struct MatrixCase
            {
                public readonly string Name;
                public readonly Action<OnAudioFilterReadVcOutput> Body;

                public MatrixCase(string name, Action<OnAudioFilterReadVcOutput> body)
                {
                    Name = name;
                    Body = body;
                }
            }

            private sealed class SignalCursor
            {
                public readonly int SampleRate;
                public readonly int Channels;
                public ushort SequenceNumber;
                public uint Timestamp;
                public double LeftPhase;
                public double RightPhase;

                public SignalCursor(int sampleRate, int channels, ushort sequenceNumber, uint timestamp)
                {
                    SampleRate = sampleRate;
                    Channels = channels;
                    SequenceNumber = sequenceNumber;
                    Timestamp = timestamp;
                    RightPhase = Math.PI * 0.5;
                }
            }

            private struct FramePacket
            {
                public float[] Samples;
                public int SampleRate;
                public int Channels;
                public ushort SequenceNumber;
                public uint Timestamp;
            }
        }

        private sealed class AudioMatrixResult
        {
            private readonly List<string> passed = new List<string>();
            private readonly List<string> failed = new List<string>();

            public bool Passed
            {
                get { return failed.Count == 0; }
            }

            public void Pass(string name)
            {
                passed.Add(name);
            }

            public void Fail(string name, string message)
            {
                failed.Add(name + " - " + message);
            }

            public string BuildReport()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("META_VOICE_CHAT_MATRIX ");
                builder.Append(Passed ? "PASS" : "FAIL");
                builder.Append(" passed=");
                builder.Append(passed.Count);
                builder.Append(" failed=");
                builder.Append(failed.Count);

                for (int i = 0; i < passed.Count; i++)
                {
                    builder.AppendLine();
                    builder.Append("PASS ");
                    builder.Append(passed[i]);
                }

                for (int i = 0; i < failed.Count; i++)
                {
                    builder.AppendLine();
                    builder.Append("FAIL ");
                    builder.Append(failed[i]);
                }

                return builder.ToString();
            }
        }
#endif
    }
}
