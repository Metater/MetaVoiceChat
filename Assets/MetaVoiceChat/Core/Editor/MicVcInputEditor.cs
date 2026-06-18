using System;
using UnityEditor;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor
{
    [CustomEditor(typeof(MicVcInput))]
    [CanEditMultipleObjects]
    public sealed class MicVcInputEditor : UnityEditor.Editor
    {
        private const float RecommendedMinimumReconnectPollInterval = MicVcInput.MinimumReconnectPollInterval;
        private const float RecommendedMaximumReconnectPollInterval = 5f;
        private const float RecommendedMaximumReconnectFailureTimeout = 5f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "selectedDevice");
            DrawSelectedDeviceDropdown();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawWarningPanels();
        }

        private void DrawSelectedDeviceDropdown()
        {
            SerializedProperty selectedDevice = serializedObject.FindProperty("selectedDevice");
            if (selectedDevice == null)
            {
                return;
            }

            string[] devices = Microphone.devices ?? Array.Empty<string>();
            string[] labels = BuildDeviceLabels(devices, selectedDevice.stringValue, out int selectedIndex);

            EditorGUI.showMixedValue = selectedDevice.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "Selected Device",
                    "Editor-only microphone picker. Choose Default Microphone to use the first available device at runtime."),
                selectedIndex,
                labels);
            if (EditorGUI.EndChangeCheck())
            {
                selectedDevice.stringValue = nextIndex <= 0 ? string.Empty : labels[nextIndex];
            }

            EditorGUI.showMixedValue = false;
        }

        private void DrawWarningPanels()
        {
            foreach (UnityEngine.Object targetObject in targets)
            {
                if (targetObject is MicVcInput input)
                {
                    DrawWarningPanelsFor(input);
                }
            }
        }

        private void DrawWarningPanelsFor(MicVcInput input)
        {
            if (!input.gameObject.activeInHierarchy)
            {
                DrawPanel(
                    "Inactive GameObject",
                    "This GameObject is inactive in the hierarchy. The microphone input will not capture, reconnect, or send frames until it is active.",
                    MessageType.Error);
            }
            else if (!input.enabled)
            {
                DrawPanel(
                    "Disabled Mic Input",
                    "This component is disabled. It will not start Unity Microphone, reconnect, or send voice frames until it is enabled.",
                    MessageType.Error);
            }

            DrawPipelinePanels(input);
            DrawFramePanels(input);
            DrawDevicePanels(input);
            DrawReconnectPanels(input);
            DrawRuntimePanels(input);
        }

        private static void DrawPipelinePanels(MicVcInput input)
        {
            VcPipeline pipeline = GetPipeline(input);
            if (pipeline == null)
            {
                DrawPanel(
                    "Missing Pipeline",
                    $"Assign a {nameof(VcPipeline)} or microphone frames will be captured and discarded.",
                    MessageType.Error);
                return;
            }

            if (!pipeline.gameObject.activeInHierarchy)
            {
                DrawPanel(
                    "Inactive Pipeline",
                    "The assigned pipeline's GameObject is inactive, so captured frames may not reach an active voice path.",
                    MessageType.Warning);
            }
            else if (!pipeline.enabled)
            {
                DrawPanel(
                    "Disabled Pipeline",
                    "The assigned pipeline component is disabled. Frames are still passed to Process, but this is usually not the intended setup.",
                    MessageType.Info);
            }
        }

        private static void DrawFramePanels(MicVcInput input)
        {
            int requestedFrequency = input.RequestedFrequency > 0 ? input.RequestedFrequency : GetFrequencyValue(input);
            int milliseconds = input.FrameMilliseconds > 0 ? input.FrameMilliseconds : GetMillisecondsValue(input);
            int samplesPerFrame = Math.Max(1, requestedFrequency * milliseconds / 1000);

            DrawPanel(
                "Mono Microphone Frames",
                $"{nameof(MicVcInput)} always sends mono frames: {samplesPerFrame} samples per frame at {requestedFrequency} Hz and {milliseconds} ms. There is no stereo microphone option.",
                MessageType.Info);

            DrawPanel(
                "Fixed Clip Loop",
                $"Unity Microphone is always started with Loop on and a {MicVcInput.ClipLoopSeconds} second clip. The read buffer stays at one frame and GetData is called once per available frame.",
                MessageType.Info);
        }

        private static void DrawDevicePanels(MicVcInput input)
        {
            string[] devices = Microphone.devices ?? Array.Empty<string>();
            if (devices.Length == 0)
            {
                DrawPanel(
                    "No Microphone Devices",
                    input.AutoReconnect
                        ? "Unity currently reports no microphone devices. This input will keep polling and reconnect automatically when one appears."
                        : "Unity currently reports no microphone devices. Auto reconnect is disabled in the mic config.",
                    MessageType.Warning);
                return;
            }

            string selectedDevice = GetSelectedDevice(input);
            if (!string.IsNullOrEmpty(selectedDevice) && !ContainsDevice(devices, selectedDevice))
            {
                DrawPanel(
                    "Selected Device Missing",
                    $"The selected device \"{selectedDevice}\" is not currently available. The input will use the first available microphone until that device appears.",
                    MessageType.Warning);
            }

            DrawPanel(
                "Available Microphones",
                $"{devices.Length} device(s) available. Choose Default Microphone to use the first one.",
                MessageType.Info);
        }

        private static void DrawReconnectPanels(MicVcInput input)
        {
            MicVcConfig config = GetConfigObject(input);
            if (config == null)
            {
                DrawPanel(
                    "Default Reconnect Settings",
                    $"No config asset is assigned. Auto reconnect is enabled with a {MicVcInput.DefaultReconnectInitialDelay:0.##} second initial delay, {MicVcInput.DefaultReconnectPollInterval:0.##} second device polling, and {MicVcInput.DefaultReconnectFailureTimeout:0.##} second failure retries.",
                    MessageType.Info);
                return;
            }

            using SerializedObject serializedConfig = new SerializedObject(config);
            SerializedProperty autoReconnect = serializedConfig.FindProperty("autoReconnect");
            SerializedProperty reconnectInitialDelay = serializedConfig.FindProperty("reconnectInitialDelay");
            SerializedProperty reconnectPollInterval = serializedConfig.FindProperty("reconnectPollInterval");
            SerializedProperty reconnectFailureTimeout = serializedConfig.FindProperty("reconnectFailureTimeout");

            if (autoReconnect != null && !autoReconnect.boolValue)
            {
                DrawPanel(
                    "Auto Reconnect Disabled",
                    "The input will attempt to start normally, but it will not keep retrying failed starts or recover automatically after device loss.",
                    MessageType.Info);
                return;
            }

            float pollInterval = reconnectPollInterval != null ? reconnectPollInterval.floatValue : 0f;
            float failureTimeout = reconnectFailureTimeout != null ? reconnectFailureTimeout.floatValue : 0f;
            float initialDelay = reconnectInitialDelay != null ? reconnectInitialDelay.floatValue : 0f;

            if (initialDelay < 0f)
            {
                DrawPanel(
                    "Invalid Initial Delay",
                    "Reconnect Initial Delay is below 0 seconds. The runtime clamps it to 0, but the config asset should be corrected.",
                    MessageType.Error);
            }

            if (pollInterval < MicVcInput.MinimumReconnectPollInterval)
            {
                DrawPanel(
                    "Very Fast Polling",
                    $"Reconnect Poll Interval is below {RecommendedMinimumReconnectPollInterval:0.##} seconds. The runtime clamps it upward to avoid repeatedly querying Unity Microphone too aggressively.",
                    MessageType.Warning);
            }
            else if (pollInterval > RecommendedMaximumReconnectPollInterval)
            {
                DrawPanel(
                    "Slow Reconnect Polling",
                    $"Reconnect Poll Interval is above {RecommendedMaximumReconnectPollInterval:0.##} seconds. Newly plugged microphones may take a while to become active.",
                    MessageType.Warning);
            }

            if (failureTimeout < MicVcInput.MinimumReconnectFailureTimeout)
            {
                DrawPanel(
                    "Very Fast Failure Retry",
                    $"Reconnect Failure Timeout is below {MicVcInput.MinimumReconnectFailureTimeout:0.##} seconds. The runtime clamps it upward so Unity has time to recover after a failed microphone start.",
                    MessageType.Warning);
            }

            if (failureTimeout > RecommendedMaximumReconnectFailureTimeout)
            {
                DrawPanel(
                    "Slow Failure Retry",
                    $"Reconnect Failure Timeout is above {RecommendedMaximumReconnectFailureTimeout:0.##} seconds. A transient Unity start failure may take a while to recover.",
                    MessageType.Warning);
            }
        }

        private static void DrawRuntimePanels(MicVcInput input)
        {
#if LOG_MicVcInput
            if (!Application.isPlaying || !input.ExposeRuntimeDiagnostics)
            {
                return;
            }

            string activeDevice = string.IsNullOrEmpty(input.ActiveDevice) ? "none" : input.ActiveDevice;
            DrawPanel(
                "Runtime State",
                $"Recording: {input.IsRecording}. Active device: {activeDevice}. Actual frequency: {input.ActualFrequency} Hz. Frame size: {input.FrameSize}. Read buffer: {input.ReadBufferSize}. Available samples: {input.AvailableSamples}. Frames sent: {input.FramesSent}. Dropped samples: {input.DroppedSamples}.",
                MessageType.Info);
#else
            _ = input;
#endif
        }

        private static string[] BuildDeviceLabels(string[] devices, string selectedDevice, out int selectedIndex)
        {
            int extraMissingSelected = !string.IsNullOrEmpty(selectedDevice) && !ContainsDevice(devices, selectedDevice) ? 1 : 0;
            string[] labels = new string[devices.Length + 1 + extraMissingSelected];
            labels[0] = "Default Microphone (first available)";
            selectedIndex = 0;

            for (int i = 0; i < devices.Length; i++)
            {
                labels[i + 1] = devices[i];
                if (devices[i] == selectedDevice)
                {
                    selectedIndex = i + 1;
                }
            }

            if (extraMissingSelected != 0)
            {
                labels[labels.Length - 1] = selectedDevice;
                selectedIndex = labels.Length - 1;
            }

            return labels;
        }

        private static VcPipeline GetPipeline(MicVcInput input)
        {
            using SerializedObject serializedInput = new SerializedObject(input);
            SerializedProperty pipeline = serializedInput.FindProperty("vcPipeline");
            return pipeline != null ? pipeline.objectReferenceValue as VcPipeline : null;
        }

        private static MicVcConfig GetConfigObject(MicVcInput input)
        {
            using SerializedObject serializedInput = new SerializedObject(input);
            SerializedProperty config = serializedInput.FindProperty("micVcConfig");
            return config != null ? config.objectReferenceValue as MicVcConfig : null;
        }

        private static string GetSelectedDevice(MicVcInput input)
        {
            using SerializedObject serializedInput = new SerializedObject(input);
            SerializedProperty selectedDevice = serializedInput.FindProperty("selectedDevice");
            return selectedDevice != null ? selectedDevice.stringValue : string.Empty;
        }

        private static int GetFrequencyValue(MicVcInput input)
        {
            using SerializedObject serializedInput = new SerializedObject(input);
            SerializedProperty frequency = serializedInput.FindProperty("vcFrequency");
            return frequency != null && frequency.enumValueIndex >= 0
                ? ((VcFrequency)frequency.enumValueIndex).ToInt()
                : VcFrequency.Hz48000.ToInt();
        }

        private static int GetMillisecondsValue(MicVcInput input)
        {
            using SerializedObject serializedInput = new SerializedObject(input);
            SerializedProperty milliseconds = serializedInput.FindProperty("vcMilliseconds");
            return milliseconds != null && milliseconds.enumValueIndex >= 0
                ? ((VcMilliseconds)milliseconds.enumValueIndex).ToInt()
                : VcMilliseconds.Ms20.ToInt();
        }

        private static bool ContainsDevice(string[] devices, string device)
        {
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i] == device)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawPanel(string title, string message, MessageType messageType)
        {
            EditorGUILayout.HelpBox($"{title}\n{message}", messageType);
        }
    }
}
