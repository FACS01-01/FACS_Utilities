#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
	internal static class OptimizedModelImportToggle
    {
		private const string RichToolName = Logger.ToolTag + "[Optimized Model Import]" + Logger.EndTag;
		private const string MenuPath = "FACS Utils/Avatar Tools/Optimized Model Import/";
		private static bool IsEnable;

		[InitializeOnLoadMethod]
		private static void OnInitialized()
		{
			IsEnable = GetAsmdefEnabled();
		}

		private static AsmdefData GetAsmdef(out string omiAsmPath)
        {
			var omiAsmGUID = AssetDatabase.FindAssets("a:packages t:asmdef FACS01.Utilities.Editor.OMI")[0];
			omiAsmPath = AssetDatabase.GUIDToAssetPath(omiAsmGUID);
			string omiJson = File.ReadAllText(omiAsmPath);
			var omiAsmDef = JsonUtility.FromJson<AsmdefData>(omiJson);
			return omiAsmDef;
		}

		private static bool GetAsmdefEnabled()
        {
			var oniAsmDef = GetAsmdef(out _);
			return oniAsmDef.defineConstraints.Length == 0;
		}

		private static void SetAsmdefEnabled(bool enable)
		{
			var oniAsmDef = GetAsmdef(out string omiAsmPath);
			if (enable) oniAsmDef.defineConstraints = new string[0];
			else oniAsmDef.defineConstraints = new string[1] { "FACSUTILS_DISABLE_OMI" };

			File.WriteAllText(omiAsmPath, JsonUtility.ToJson(oniAsmDef, true));
			AssetDatabase.ImportAsset(omiAsmPath, ImportAssetOptions.ForceUpdate);
		}

		[MenuItem(MenuPath + "Enable", true, 1101)]
		private static bool IsDisabled() => !IsEnable;

		[MenuItem(MenuPath + "Disable", true, 1102)]
		private static bool IsEnabled() => IsEnable;

		[MenuItem(MenuPath + "Enable", false, 1101)]
		private static void ToggleOn()
		{
			SetAsmdefEnabled(true);
			Logger.Log($"{RichToolName} Enabled! Reloading scripts...");
		}

		[MenuItem(MenuPath + "Disable", false, 1102)]
		private static void ToggleOff()
		{
			SetAsmdefEnabled(false);
			Logger.Log($"{RichToolName} Disabled. Reloading scripts...");
		}

		[MenuItem(MenuPath + "Reimport All", false, 1103)]
		private static void ReimportAll()
		{
			var msg = "Are you sure you want to reimport all Models in the Project? It can take some time.\n\n";
			if (IsEnable) msg += "Optimized Model Import will be used!";
			else msg += "Optimized Model Import is not active.";
			if (!EditorUtility.DisplayDialog("FACS Utilities - Reimport Models", msg, "Yes", "No"))
				return;

			string[] modelGUIDs = AssetDatabase.FindAssets("a:assets t:model");
			if (modelGUIDs.Length == 0)
			{
				Logger.LogWarning($"{RichToolName} No {Logger.RichModel} found in the project's {Logger.RichAssetsFolder}.");
				return;
			}

			string[] modelPaths = modelGUIDs.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).Where(path => !string.IsNullOrEmpty(path)).ToArray();
			if (modelPaths.Length == 0)
			{
				Logger.LogWarning($"{RichToolName} No valid {Logger.RichModel} found in the project's {Logger.RichAssetsFolder}.");
				return;
			}

			foreach (string path in modelPaths)
			{
				Logger.Log($"{RichToolName} Reimporting {Logger.RichModel} at: {path}.");
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
			Logger.Log($"{RichToolName} Reimported <b>{modelPaths.Length}</b> {Logger.RichModel}.");
		}

		[System.Serializable]
		private class AsmdefData
		{
			public string name;
			public string rootNamespace;
			public string[] references;
			public string[] includePlatforms;
			public string[] excludePlatforms;
			public bool allowUnsafeCode;
			public bool overrideReferences;
			public string[] precompiledReferences;
			public bool autoReferenced;
			public string[] defineConstraints;
			public string[] versionDefines;
			public bool noEngineReferences;
		}
	}
}
#endif