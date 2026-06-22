using MetaVoiceChat.Core.Opus;
using UnityEditor;

namespace MetaVoiceChat.Core.Editor
{
    [CustomEditor(typeof(OpusUserConfigScriptableObject))]
    [CanEditMultipleObjects]
    public sealed class OpusUserConfigScriptableObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("User-Friendly Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("application"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("complexity"));
            DrawOverrideField("overrideSignalType", "signalType");
            DrawOverrideField("overrideMaxBandwidth", "maxBandwidth");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Advanced Settings (Do research before changing anything here!)",
                EditorStyles.boldLabel);

            DrawOverrideField("overrideForceMode", "forceMode");
            DrawOverrideField("overrideBandwidth", "bandwidth");
            DrawOverrideField("overrideLSBDepth", "lsbDepth");
            DrawOverrideField("overrideForceChannels", "forceChannels");
            DrawOverrideField("overrideBitrate", "bitrate");
            DrawOverrideField("overrideUseDTX", "useDTX");
            DrawOverrideField("overrideUseInbandFEC", "useInbandFEC");
            DrawOverrideField("overridePacketLossPercent", "packetLossPercent");
            DrawOverrideField("overrideUseVBR", "useVBR");
            DrawOverrideField("overrideUseConstrainedVBR", "useConstrainedVBR");
            DrawOverrideField("overrideExpertFrameDuration", "expertFrameDuration");
            DrawOverrideField("overridePredictionDisabled", "predictionDisabled");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawOverrideField(string overridePropertyName, string valuePropertyName)
        {
            SerializedProperty overrideProperty = serializedObject.FindProperty(overridePropertyName);
            SerializedProperty valueProperty = serializedObject.FindProperty(valuePropertyName);

            if (overrideProperty == null || valueProperty == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(overrideProperty);
            if (overrideProperty.boolValue || overrideProperty.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(valueProperty);
            }
        }
    }
}
