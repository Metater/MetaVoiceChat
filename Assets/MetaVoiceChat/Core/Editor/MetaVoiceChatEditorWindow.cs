#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor
{
    public class MetaVoiceChatEditorWindow : EditorWindow
    {
        [MenuItem("MetaVoiceChat/Install RNNoise")]
        public static void ShowWindow()
        {
            GetWindow<MetaVoiceChatEditorWindow>("Install RNNoise");
        }

        private void OnGUI()
        {
#if META_VOICE_CHAT_RNNOISE
            GUILayout.Label("RNNoise is already installed, but you may click the button again to fix a broken installation.", EditorStyles.wordWrappedLabel);
#endif

            EditorGUILayout.Space();

            GUILayout.Label("Step 1 - Press the button", EditorStyles.boldLabel);

            if (GUILayout.Button("Install RNNoise"))
            {
                ManifestUtils.AddScopedRegistry("npmjs", "https://registry.npmjs.org", new string[] { "com.npmjs", "com.adrenak.rnnoise4unity" });
                ManifestUtils.AddDependency("com.adrenak.rnnoise4unity", "1.0.0");
                MvcEditorUtils.AddDefineSymbol("META_VOICE_CHAT_RNNOISE");
            }

            GUILayout.Label("This will add com.adrenak.rnnoise4unity to your package.json scoped registries and install the package. It will also add META_VOICE_CHAT_RNNOISE to your define symbols.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();

            GUILayout.Label("Step 2 - Restart Unity if there are errors", EditorStyles.boldLabel);

            GUILayout.Label("Restarting Unity might be necessary if Unity doesn't recognize all of the changes.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();

            GUILayout.Label("Step 3 - Check your manifest.json", EditorStyles.boldLabel);
            GUILayout.Label("AI was used to generate the code that modifies manifest.json. Please use source control and double check the modifications made.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            GUILayout.Label("Step 4 - It will just start working", EditorStyles.boldLabel);
            GUILayout.Label($"RNNoise can now be used. Place an RNNoise filter in your input pipeline and ensure 48 kHz input.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();

            GUILayout.Label("Thank you!", EditorStyles.boldLabel);
            GUILayout.Label("A massive thank you to Vatsal Ambastha for creating RNNoise4Unity which makes using RNNoise super easy! FYI, it has a BSD 3-Clause license.", EditorStyles.wordWrappedLabel);

            if (EditorGUILayout.LinkButton("Vatsal Ambastha GitHub"))
            {
                Application.OpenURL("https://github.com/adrenak");
            }

            if (EditorGUILayout.LinkButton("RNNoise4Unity GitHub"))
            {
                Application.OpenURL("https://github.com/adrenak/RNNoise4Unity");
            }
        }
    }
}
#endif