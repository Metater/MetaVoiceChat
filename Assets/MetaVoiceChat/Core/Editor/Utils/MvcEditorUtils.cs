#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor.Utils
{
    public static class MvcEditorUtils
    {
        public static void AddDefineSymbol(string defineSymbol)
        {
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);

            // Prepare named target
            NamedBuildTarget namedBuildTarget;
            if (buildTargetGroup == BuildTargetGroup.Standalone)
            {
                StandaloneBuildSubtarget standaloneSubTarget = EditorUserBuildSettings.standaloneBuildSubtarget;
                if (standaloneSubTarget == StandaloneBuildSubtarget.Server)
                    namedBuildTarget = NamedBuildTarget.Server;
                else
                    namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            }
            else
            {
                namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            }

            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

            // Check if the symbol already exists
            if (currentDefines.Contains(defineSymbol))
                return;

            // Add the new define symbol
            string newDefines = string.IsNullOrEmpty(currentDefines)
                ? defineSymbol
                : currentDefines + ";" + defineSymbol;

            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newDefines);

            // Force save the project settings
            AssetDatabase.SaveAssets();
        }

        public static void DrawFooter()
        {
#if !META_VOICE_CHAT_RNNOISE
            {
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Install RNNoise", EditorStyles.boldLabel);

                if (GUILayout.Button("Open Installation Window"))
                {
                    MetaVoiceChatEditorWindow.ShowWindow();
                }

                GUILayout.Label("It is highly recommended to install RNNoise for production games. People will have shitty microphones. This helps with backround noise, echo cancellation, and staticy microphones.", EditorStyles.wordWrappedLabel);
            }
#endif

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Documentation", EditorStyles.boldLabel);

            if (EditorGUILayout.LinkButton("MetaVoiceChat Discord"))
            {
                Application.OpenURL("https://discord.gg/k4ZtGAA2Nt");
            }

            if (EditorGUILayout.LinkButton("VIDEO TUTORIAL TODO"))
            {
                Application.OpenURL("https://www.youtube.com/watch?v=2fSqSAnRS5M");
            }

            if (EditorGUILayout.LinkButton("Meta Voice Chat"))
            {
                Application.OpenURL("https://github.com/Metater/MetaVoiceChat");
            }

            if (EditorGUILayout.LinkButton("Opus Recommended Settings"))
            {
                Application.OpenURL("https://wiki.xiph.org/Opus_Recommended_Settings");
            }

            if (EditorGUILayout.LinkButton("libopus 1.1.2"))
            {
                Application.OpenURL("https://opus-codec.org/docs/opus_api-1.1.2/index.html");
            }

            if (EditorGUILayout.LinkButton("VisioForge Opus"))
            {
                Application.OpenURL("https://www.visioforge.com/help/docs/dotnet/general/audio-encoders/opus");
            }

            if (EditorGUILayout.LinkButton("Concentus: Opus for Everyone"))
            {
                Application.OpenURL("https://github.com/lostromb/concentus");
            }

            if (EditorGUILayout.LinkButton("RNNoise4Unity GitHub"))
            {
                Application.OpenURL("https://github.com/adrenak/RNNoise4Unity");
            }
        }
    }
}
#endif