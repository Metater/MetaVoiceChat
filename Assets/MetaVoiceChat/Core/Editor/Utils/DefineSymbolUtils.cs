#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;

namespace MetaVoiceChat.Core.Editor.Utils
{
    public static class DefineSymbolUtils
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
    }
}
#endif