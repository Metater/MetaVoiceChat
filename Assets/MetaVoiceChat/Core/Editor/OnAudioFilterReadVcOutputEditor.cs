using UnityEditor;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor
{
    [CustomEditor(typeof(OnAudioFilterReadVcOutput))]
    [CanEditMultipleObjects]
    public sealed class OnAudioFilterReadVcOutputEditor : UnityEditor.Editor
    {
        private SerializedProperty audioFilterReadConfig;
        private SerializedProperty pendingFrameCapacity;

        private void OnEnable()
        {
            audioFilterReadConfig = serializedObject.FindProperty("audioFilterReadConfig");
            pendingFrameCapacity = serializedObject.FindProperty("pendingFrameCapacity");
        }

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
            AudioSource source = output.GetComponent<AudioSource>();
            if (source == null)
            {
                DrawPanel(
                    "Missing AudioSource",
                    "This component needs an AudioSource on the same GameObject before Unity can invoke OnAudioFilterRead.",
                    MessageType.Error);
                return;
            }

            if (GetPendingFrameCapacity() < OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer)
            {
                DrawPanel(
                    "Small Pending Queue",
                    $"Pending Frame Capacity is below the default NetEQ packet buffer ({OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer}). Bursty receive timing may drop packets before the audio thread can drain them.",
                    MessageType.Warning);
            }

            DrawConfigPanels();
            DrawAudioSourcePanels(source);
            DrawUnityAudioSettingsPanels();
        }

        private void DrawConfigPanels()
        {
            Object configObject = audioFilterReadConfig != null ? audioFilterReadConfig.objectReferenceValue : null;
            if (configObject == null)
            {
                DrawPanel(
                    "Default NetEQ Settings",
                    "No config asset is assigned. The output will use built-in NetEQ and resampler defaults.",
                    MessageType.Info);
                return;
            }

            using SerializedObject config = new SerializedObject(configObject);
            SerializedProperty maxPacketsInBuffer = config.FindProperty("maxPacketsInBuffer");
            SerializedProperty maxDelayMs = config.FindProperty("maxDelayMs");
            SerializedProperty minDelayMs = config.FindProperty("minDelayMs");
            SerializedProperty additionalDelayMs = config.FindProperty("additionalDelayMs");
            SerializedProperty resamplerQuality = config.FindProperty("resamplerQuality");

            if (maxPacketsInBuffer != null && maxPacketsInBuffer.intValue < 1)
            {
                DrawPanel(
                    "Invalid Packet Buffer",
                    "Max Packets In Buffer must be at least 1. The runtime clamps this, but the asset value should be corrected.",
                    MessageType.Error);
            }

            if (minDelayMs != null &&
                maxDelayMs != null &&
                GetWholeNumberValue(minDelayMs) > GetWholeNumberValue(maxDelayMs))
            {
                DrawPanel(
                    "Invalid NetEQ Delay",
                    "Min Delay is greater than Max Delay. NetEQ delay targets should be ordered from minimum to maximum.",
                    MessageType.Error);
            }

            if (maxDelayMs != null && GetWholeNumberValue(maxDelayMs) > 500)
            {
                DrawPanel(
                    "High NetEQ Delay",
                    "Max Delay is above 500 ms. This can make voice playback feel noticeably late.",
                    MessageType.Warning);
            }

            if (additionalDelayMs != null && GetWholeNumberValue(additionalDelayMs) > 100)
            {
                DrawPanel(
                    "Additional Delay",
                    "Additional Delay is above 100 ms. This intentionally adds latency on top of the jitter buffer.",
                    MessageType.Warning);
            }

            if (resamplerQuality != null && resamplerQuality.intValue > 4)
            {
                DrawPanel(
                    "Expensive Resampling",
                    "Resampler Quality above 4 can be costly for real-time voice playback.",
                    MessageType.Warning);
            }
        }

        private void DrawAudioSourcePanels(AudioSource source)
        {
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

            if (dspMs > 100)
            {
                DrawPanel(
                    "High DSP Buffer Latency",
                    $"The current Unity DSP buffer is about {dspMs} ms ({bufferLength} x {numBuffers} at {outputSampleRate} Hz). This latency is added after NetEQ output. Consider changing the DSP Buffer Size to \"Best latency\" in the project audio settings.",
                    MessageType.Warning);
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

        private int GetPendingFrameCapacity()
        {
            return pendingFrameCapacity != null ? pendingFrameCapacity.intValue : OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer;
        }

        private static long GetWholeNumberValue(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Integer ? property.intValue : 0L;
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
