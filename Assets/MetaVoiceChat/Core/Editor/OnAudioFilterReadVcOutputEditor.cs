using UnityEditor;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor
{
    [CustomEditor(typeof(OnAudioFilterReadVcOutput))]
    [CanEditMultipleObjects]
    public sealed class OnAudioFilterReadVcOutputEditor : UnityEditor.Editor
    {
        private const int RecommendedMinMaxDelayMs = 40;
        private const int RecommendedMaxDelayMs = 300;
        private const int RecommendedMaxAdditionalDelayMs = 100;
        private const int RecommendedMaxResamplerQuality = 4;
        private const int RecommendedMaxVoiceLatencyMs = 300;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawWarningPanels();
        }

        private void DrawWarningPanels()
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is OnAudioFilterReadVcOutput output)
                {
                    DrawWarningPanelsFor(output);
                }
            }
        }

        private void DrawWarningPanelsFor(OnAudioFilterReadVcOutput output)
        {
            if (!output.gameObject.activeInHierarchy)
            {
                DrawPanel(
                    "Inactive GameObject",
                    "This GameObject is inactive in the hierarchy. The output will not receive Unity lifecycle calls or produce voice audio until it is active.",
                    MessageType.Error);
            }
            else if (!output.enabled)
            {
                DrawPanel(
                    "Disabled Voice Output",
                    "This component is disabled. It will not start the AudioSource, accept frames, or run OnAudioFilterRead until it is enabled.",
                    MessageType.Error);
            }

            AudioSource source = output.GetComponent<AudioSource>();
            if (source == null)
            {
                DrawPanel(
                    "Missing AudioSource",
                    "This component needs an AudioSource on the same GameObject before Unity can invoke OnAudioFilterRead.",
                    MessageType.Error);
                return;
            }

            DrawConfigPanels(output);
            DrawAudioSourcePanels(source);
            DrawUnityAudioSettingsPanels();
        }

        private void DrawConfigPanels(OnAudioFilterReadVcOutput output)
        {
            Object configObject = GetConfigObject(output);
            if (configObject == null)
            {
                DrawPanel(
                    "Default NetEQ Settings",
                    $"No config asset is assigned. For 20 ms packets, the output will use defaults: {OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer} packets, {OnAudioFilterReadVcConfig.GetMinDelayMs(20, OnAudioFilterReadVcOutput.DefaultJitterBufferMode)}-{OnAudioFilterReadVcConfig.GetMaxDelayMs(20, OnAudioFilterReadVcOutput.DefaultJitterBufferMode)} ms NetEQ delay, {OnAudioFilterReadVcOutput.DefaultAdditionalDelayMs} ms additional delay, quality {OnAudioFilterReadVcOutput.DefaultResamplerQuality}, and a {OnAudioFilterReadVcOutput.DefaultResamplerBufferMs} ms local output buffer target. NetEQ GetAudio is pulled in 10 ms chunks.",
                    MessageType.Info);
                return;
            }

            using SerializedObject config = new SerializedObject(configObject);
            SerializedProperty maxPacketsInBuffer = config.FindProperty("maxPacketsInBuffer");
            SerializedProperty maxDelayMs = config.FindProperty("maxDelayMs");
            SerializedProperty minDelayMs = config.FindProperty("minDelayMs");
            SerializedProperty additionalDelayMs = config.FindProperty("additionalDelayMs");
            SerializedProperty resamplerQuality = config.FindProperty("resamplerQuality");
            SerializedProperty resamplerBufferMs = config.FindProperty("resamplerBufferMs");

            int maxPackets = GetWholeNumberValue(maxPacketsInBuffer);
            int maxDelay = GetWholeNumberValue(maxDelayMs);
            int minDelay = GetWholeNumberValue(minDelayMs);
            int additionalDelay = GetWholeNumberValue(additionalDelayMs);
            int quality = GetWholeNumberValue(resamplerQuality);
            int resamplerBuffer = GetWholeNumberValue(resamplerBufferMs);

            if (maxPacketsInBuffer != null && maxPackets < 1)
            {
                DrawPanel(
                    "Invalid Packet Buffer",
                    "Max Packets In Buffer must be at least 1. The runtime clamps this, but the asset value should be corrected.",
                    MessageType.Error);
            }

            if (minDelayMs != null &&
                maxDelayMs != null &&
                minDelay > maxDelay)
            {
                DrawPanel(
                    "Invalid NetEQ Delay",
                    "Min Delay is greater than Max Delay. The runtime clamps Max Delay upward, but the asset should be fixed so the intended jitter-buffer range is obvious.",
                    MessageType.Error);
            }

            if (maxDelayMs != null && maxDelay < RecommendedMinMaxDelayMs)
            {
                DrawPanel(
                    "Aggressive NetEQ Delay",
                    $"Max Delay is below {RecommendedMinMaxDelayMs} ms. This is very responsive, but even modest network jitter can cause underruns or artifacts.",
                    MessageType.Warning);
            }

            if (maxDelayMs != null && maxDelay > RecommendedMaxDelayMs)
            {
                DrawPanel(
                    "High NetEQ Delay",
                    $"Max Delay is above {RecommendedMaxDelayMs} ms. This can make voice playback feel noticeably late.",
                    MessageType.Warning);
            }

            if (additionalDelayMs != null && additionalDelay > RecommendedMaxAdditionalDelayMs)
            {
                DrawPanel(
                    "Additional Delay",
                    $"Additional Delay is above {RecommendedMaxAdditionalDelayMs} ms. This intentionally adds latency on top of the adaptive jitter buffer.",
                    MessageType.Warning);
            }

            int roundedResamplerBuffer = RoundResamplerBufferMs(resamplerBuffer);
            int estimatedVoiceBufferMs = Mathf.Max(minDelay, maxDelay) + additionalDelay + roundedResamplerBuffer;
            if (maxDelayMs != null &&
                additionalDelayMs != null &&
                resamplerBufferMs != null &&
                estimatedVoiceBufferMs > RecommendedMaxVoiceLatencyMs)
            {
                DrawPanel(
                    "High Voice Buffering",
                    $"This config can buffer about {estimatedVoiceBufferMs} ms before Unity DSP latency. Voice may feel delayed even if packets arrive cleanly.",
                    MessageType.Warning);
            }

            if (resamplerQuality != null && quality > RecommendedMaxResamplerQuality)
            {
                DrawPanel(
                    "Expensive Resampling",
                    $"Resampler Quality above {RecommendedMaxResamplerQuality} can be costly for real-time voice playback.",
                    MessageType.Warning);
            }

            if (resamplerBufferMs != null)
            {
                if (resamplerBuffer != roundedResamplerBuffer)
                {
                    DrawPanel(
                        "Rounded Resampler Buffer",
                        $"Resampler Buffer is {resamplerBuffer} ms. Runtime rounds it to {roundedResamplerBuffer} ms, so edit the asset if that was not intentional.",
                        MessageType.Info);
                }

                int callbackMs = GetDspCallbackMs();
                if (callbackMs > 0 && roundedResamplerBuffer > callbackMs && roundedResamplerBuffer > 10)
                {
                    DrawPanel(
                        "Large Resampler Chunk",
                        $"Resampler Buffer is {roundedResamplerBuffer} ms, larger than the current {callbackMs} ms Unity DSP callback. This can add avoidable local buffering.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawAudioSourcePanels(AudioSource source)
        {
            if (!source.enabled)
            {
                DrawPanel(
                    "Disabled AudioSource",
                    "The AudioSource is disabled. Unity will not invoke OnAudioFilterRead for this output until the AudioSource is enabled.",
                    MessageType.Error);
            }

            if (source.mute)
            {
                DrawPanel(
                    "Muted AudioSource",
                    "The AudioSource is muted, so voice output will be silent even while frames are accepted and processed.",
                    MessageType.Error);
            }

            if (source.volume <= 0f)
            {
                DrawPanel(
                    "Zero AudioSource Volume",
                    "AudioSource Volume is 0, so voice output will be silent.",
                    MessageType.Error);
            }
            else if (source.volume < 0.25f)
            {
                DrawPanel(
                    "Low AudioSource Volume",
                    "AudioSource Volume is very low. Voice may appear broken even though packets and OnAudioFilterRead are working.",
                    MessageType.Warning);
            }

            if (source.pitch <= 0f)
            {
                DrawPanel(
                    "Invalid AudioSource Pitch",
                    "AudioSource Pitch is 0 or negative. Keep Pitch at 1 for normal voice playback.",
                    MessageType.Error);
            }
            else if (!Mathf.Approximately(source.pitch, 1f))
            {
                DrawPanel(
                    "Changed AudioSource Pitch",
                    "AudioSource Pitch is not 1. This can change playback timing and make voice sound wrong.",
                    MessageType.Warning);
            }

            if (source.clip != null)
            {
                DrawPanel(
                    "Playback Clip Overridden",
                    "This component creates and assigns its own generated playback clip at runtime. Any clip currently assigned here is only an editor-time placeholder.",
                    MessageType.Info);
            }

            if (source.spatialBlend <= 0f)
            {
                DrawPanel(
                    "2D AudioSource",
                    "Spatial Blend is 0, so Unity will not apply 3D spatial attenuation or panning to this voice output.",
                    MessageType.Info);
            }

            if (!Mathf.Approximately(source.dopplerLevel, 0f) ||
                source.spatializePostEffects ||
                !source.loop ||
                source.priority != 0)
            {
                DrawPanel(
                    "AudioSource Values Overridden",
                    "At runtime this component forces Loop on, Priority to 0, Doppler Level to 0, and Spatialize Post Effects off.",
                    MessageType.Info);
            }
        }

        private void DrawUnityAudioSettingsPanels()
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
            int outputSampleRate = AudioSettings.outputSampleRate;
            int dspMs = outputSampleRate > 0 ? bufferLength * numBuffers * 1000 / outputSampleRate : 0;
            int callbackMs = outputSampleRate > 0 ? bufferLength * 1000 / outputSampleRate : 0;

            if (dspMs > 40)
            {
                DrawPanel(
                    "High DSP Buffer Latency",
                    $"The current Unity DSP buffer is about {dspMs} ms ({bufferLength} x {numBuffers} at {outputSampleRate} Hz). This latency is added after NetEQ output. Consider changing the DSP Buffer Size to \"Best latency\" in the project audio settings.",
                    MessageType.Warning);
            }

            if (callbackMs >= 40)
            {
                DrawPanel(
                    "Large DSP Callback",
                    $"Each Unity audio callback is about {callbackMs} ms. Smaller DSP callbacks usually make voice output feel more responsive.",
                    MessageType.Info);
            }

            AudioSpeakerMode speakerMode = AudioSettings.speakerMode;
            if (speakerMode == AudioSpeakerMode.Mono)
            {
                DrawPanel(
                    "Mono Speaker Mode",
                    "Unity is currently in mono speaker mode. Voice output will be collapsed to one channel, so stereo cues and spatial panning are limited.",
                    MessageType.Warning);
            }
            else if (GetSpeakerModeChannelCount(speakerMode) > 2)
            {
                DrawPanel(
                    "Multichannel Speaker Mode",
                    $"Unity is currently using {speakerMode}. NetEQ accepts mono/stereo packets; this output will convert voice audio to the active speaker layout.",
                    MessageType.Info);
            }
        }

        private static Object GetConfigObject(OnAudioFilterReadVcOutput output)
        {
            using SerializedObject serializedOutput = new SerializedObject(output);
            SerializedProperty config = serializedOutput.FindProperty("audioFilterReadConfig");
            return config != null ? config.objectReferenceValue : null;
        }

        private static int GetWholeNumberValue(SerializedProperty property)
        {
            return property != null && property.propertyType == SerializedPropertyType.Integer ? property.intValue : 0;
        }

        private static int GetDspCallbackMs()
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out _);
            int outputSampleRate = AudioSettings.outputSampleRate;
            return outputSampleRate > 0 ? bufferLength * 1000 / outputSampleRate : 0;
        }

        private static int RoundResamplerBufferMs(int value)
        {
            return Mathf.Clamp((value + 5) / 10 * 10, 10, 100);
        }

        private static int GetSpeakerModeChannelCount(AudioSpeakerMode speakerMode)
        {
            switch (speakerMode)
            {
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Stereo:
                    return 2;
                case AudioSpeakerMode.Quad:
                    return 4;
                case AudioSpeakerMode.Surround:
                    return 5;
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                case AudioSpeakerMode.Prologic:
                    return 2;
                default:
                    return 2;
            }
        }

        private static void DrawPanel(string title, string message, MessageType messageType)
        {
            EditorGUILayout.HelpBox($"{title}\n{message}", messageType);
        }
    }
}
