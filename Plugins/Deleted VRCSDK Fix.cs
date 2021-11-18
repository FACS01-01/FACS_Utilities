#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Assembly = System.Reflection.Assembly;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FACS01.Utilities
{
    [InitializeOnLoad]
    public static class DeletedVRCSDKFix
    {
        static DeletedVRCSDKFix()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
        }

        public static void OnCompilationStarted(object value) =>
            ConfigureSettings();

        public static void ConfigureSettings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating) return;
            ConfigurePlayerSettings();
        }

        private static void ConfigurePlayerSettings()
        {
            if (!PlayerSettings.runInBackground) PlayerSettings.runInBackground = true;
            SetActiveSDKDefines();
        }

        private static bool IsSdkDllActive(SdkVersion version)
        {
            string assembly = version.ToString();
            PluginImporter importer = AssetImporter.GetAtPath($"Assets/VRCSDK/Plugins/{assembly}.dll") as PluginImporter;
            if (importer == false)
            {
                importer = AssetImporter.GetAtPath($"Assets/VRCSDK/Plugins/{assembly + "A"}.dll") as PluginImporter;
                if (importer == false) return false;
            }

            return importer.GetCompatibleWithAnyPlatform();
        }

        private enum SdkVersion { VRCSDK2, VRCSDK3 };

        private static void SetActiveSDKDefines()
        {
            bool definesChanged = false;
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            List<string> defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup).Split(';').ToList();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (defines.Contains("UDON") && !assemblies.Any(assembly => assembly.GetType("VRC.Udon.UdonBehaviour") != null))
            {
                defines.Remove("UDON"); definesChanged = true;
            }

            if (defines.Contains("VRC_SDK_VRCSDK2") && !IsSdkDllActive(SdkVersion.VRCSDK2))
            {
                defines.Remove("VRC_SDK_VRCSDK2"); definesChanged = true;
            }

            if (defines.Contains("VRC_SDK_VRCSDK3") && !IsSdkDllActive(SdkVersion.VRCSDK3))
            {
                defines.Remove("VRC_SDK_VRCSDK3"); definesChanged = true;
            }

            if (definesChanged)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";", defines.ToArray()));
                Debug.Log("FACS Utilities: removed scraps from old VRC SDK");
                CompilationPipeline.RequestScriptCompilation();
            }
            else
            {
                //Debug.Log("FACS Utilities: nothing");
            }
        }
    }
}
#endif
