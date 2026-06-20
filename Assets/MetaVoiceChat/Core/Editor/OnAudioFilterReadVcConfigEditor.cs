using UnityEditor;

namespace MetaVoiceChat.Core.Editor
{
    [CustomEditor(typeof(OnAudioFilterReadVcConfig))]
    [CanEditMultipleObjects]
    public sealed class OnAudioFilterReadVcConfigEditor : UnityEditor.Editor
    {
        private const string JitterBufferModePropertyName = "jitterBufferMode";
        private const string CustomMinDelayMsPropertyName = "customMinDelayMs";
        private const string CustomMaxDelayMsPropertyName = "customMaxDelayMs";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(
                serializedObject,
                CustomMaxDelayMsPropertyName,
                CustomMinDelayMsPropertyName);

            SerializedProperty jitterBufferMode = serializedObject.FindProperty(JitterBufferModePropertyName);
            if (jitterBufferMode == null || jitterBufferMode.hasMultipleDifferentValues)
            {
                serializedObject.ApplyModifiedProperties();
                return;
            }

            OnAudioFilterReadVcConfig.JitterBufferMode mode =
                (OnAudioFilterReadVcConfig.JitterBufferMode)jitterBufferMode.enumValueIndex;
            EditorGUILayout.Space();

            if (mode == OnAudioFilterReadVcConfig.JitterBufferMode.Custom)
            {
                EditorGUILayout.LabelField(
                    "Custom NetEQ Delay Settings (Only Used In Custom Mode)",
                    EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(CustomMinDelayMsPropertyName));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(CustomMaxDelayMsPropertyName));
            }
            else
            {
                DrawPresetDelayFields(mode);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawPresetDelayFields(OnAudioFilterReadVcConfig.JitterBufferMode mode)
        {
            EditorGUILayout.LabelField("NetEQ Delay Settings", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                DrawPresetDelayFieldsForPacketDuration(mode, 10);
                DrawPresetDelayFieldsForPacketDuration(mode, 20);
                DrawPresetDelayFieldsForPacketDuration(mode, 40);
            }
        }

        private static void DrawPresetDelayFieldsForPacketDuration(
            OnAudioFilterReadVcConfig.JitterBufferMode mode,
            int packetDurationMs)
        {
            int minDelayMs = OnAudioFilterReadVcConfig.GetMinDelayMs(packetDurationMs, mode, null);
            int maxDelayMs = OnAudioFilterReadVcConfig.GetMaxDelayMs(packetDurationMs, mode, null);

            EditorGUILayout.LabelField($"{packetDurationMs} ms Packets", EditorStyles.miniBoldLabel);
            EditorGUILayout.IntField("Minimum Delay (ms)", minDelayMs);
            EditorGUILayout.IntField("Maximum Delay (ms)", maxDelayMs);
        }
    }
}
