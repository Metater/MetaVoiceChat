#if UNITY_EDITOR
using MetaVoiceChat.Core.Editor.RNNoise;
using UnityEditor;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor.Utils
{
    public static class MetaVoiceChatFooterUtils
    {
        public static void DrawFooter()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

#if !META_VOICE_CHAT_RNNOISE
            {
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Install RNNoise", EditorStyles.boldLabel);

                if (GUILayout.Button("Open Installation Window"))
                {
                    InstallRnnoiseWindow.ShowWindow();
                }

                GUILayout.Label("It is highly recommended to install RNNoise for production games. People have shitty microphones. This helps with backround noise, echo cancellation, and staticy microphones.", EditorStyles.wordWrappedLabel);
            }
#endif

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MetaVoiceChat Links", EditorStyles.boldLabel);

            if (EditorGUILayout.LinkButton("MetaVoiceChat Discord (Support)"))
            {
                Application.OpenURL("https://discord.gg/k4ZtGAA2Nt");
            }

            if (EditorGUILayout.LinkButton("MetaVoiceChat GitHub (Documentation)"))
            {
                Application.OpenURL("https://github.com/Metater/MetaVoiceChat");
            }
        }
    }
}
#endif