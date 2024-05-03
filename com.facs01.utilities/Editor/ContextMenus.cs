#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FACS01.Utilities
{
    internal static class ContextMenus
    {
        private const string RichToolName = Logger.ToolTag + "[FACS Utilities - Context Menus]" + Logger.EndTag;

        #region Hide and Show Assets

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/Hide Sub Assets", true, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/Hide Sub Assets", true, 201)]
        private static bool CanHideSubAssets(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (string.IsNullOrEmpty(assetPath)) return false;
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (menuCommand.context != mainAsset) return false;
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            return subassets.Length != 0;
        }

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/Hide Sub Assets", false, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/Hide Sub Assets", false, 201)]
        private static void HideSubAssets(MenuCommand menuCommand)
        {
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(menuCommand.context));
            var counter = 0;
            foreach (var sa in subassets)
            {
                if (!sa) continue;
                using (var so = new SerializedObject(sa))
                {
                    so.FindProperty("m_ObjectHideFlags").intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                counter++;
            }
            AssetDatabase.SaveAssets();
            if (counter == 1)
                Logger.Log($"{RichToolName} {Logger.AssetTag}{subassets[0].name}{Logger.EndTag} was hidden in {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag}", menuCommand.context);
            else Logger.Log($"{RichToolName} <b>{counter}</b> sub assets were hidden in {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag}", menuCommand.context);
        }

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/Show Sub Assets", true, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/Show Sub Assets", true, 201)]
        private static bool CanShowSubAssets(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (string.IsNullOrEmpty(assetPath)) return false;
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (menuCommand.context != mainAsset) return false;
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var nullAssetsN = allAssets.Where(o => object.Equals(o, null)).Count();
            if (allAssets.Length - nullAssetsN < 2) return false;
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            return allAssets.Length - nullAssetsN - 1 != subassets.Length;
        }

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/Show Sub Assets", false, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/Show Sub Assets", false, 201)]
        private static void ShowSubAssets(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var nullAssetsN = allAssets.Where(o => object.Equals(o, null)).Count();
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            var visibleSubAssets = new HashSet<Object>(subassets);
            var counter = 0;
            foreach (var asset in allAssets)
            {
                if (!asset || asset == mainAsset || visibleSubAssets.Contains(asset)) continue;
                using (var so = new SerializedObject(asset))
                {
                    so.FindProperty("m_ObjectHideFlags").intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                counter++;
            }
            AssetDatabase.SaveAssets();
            if (nullAssetsN != 0)
                Logger.LogWarning($"{RichToolName} Encountered <b>{nullAssetsN}</b> broken assets in {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag}", menuCommand.context);
            Logger.Log($"{RichToolName} <b>{counter}</b> sub asset{(counter>1?"s were":" was")} shown in {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag}", menuCommand.context);
        }

        #endregion

        #region Connect and Disconnect BlendTrees

        [MenuItem("CONTEXT/BlendTree/FACS Utils/Disconnect from Asset", true, 351)]
        private static bool BlendTreeCanDisconnect(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var _ = AssetDatabase.LoadMainAssetAtPath(assetPath);
                return menuCommand.context != _;
            }
            return false;
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/Disconnect from Asset", false, 351)]
        private static void BlendTreeDisconnect(MenuCommand menuCommand)
        {
            var bt = menuCommand.context as BlendTree;
            var mainassetType = AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GetAssetPath(bt));
            var initsavepanel = new System.IO.FileInfo(AssetDatabase.GetAssetPath(bt)).Directory.FullName;
            var savepath = EditorUtility.SaveFilePanel($"Disconnect Blend Tree from {ObjectNames.NicifyVariableName(mainassetType.Name)}", initsavepanel, bt.name, "asset");
            if (string.IsNullOrEmpty(savepath)) return;
            savepath = System.IO.Path.GetFullPath(savepath);
            if (System.IO.File.Exists(savepath)) { Logger.LogWarning(RichToolName + " Overwriting an existing asset is not supported."); return; }
            var projPath = System.IO.Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) + System.IO.Path.DirectorySeparatorChar; ;
            if (!savepath.StartsWith(projPath))
            { Logger.LogWarning(RichToolName + " Select a save location for the " + Logger.RichBlendTree + " inside this project!"); return; }
            savepath = savepath.Replace(projPath, "");

            var btCopy = new BlendTree();
            EditorUtility.CopySerialized(bt, btCopy);
            btCopy.hideFlags = HideFlags.None;
            btCopy.name = System.IO.Path.GetFileNameWithoutExtension(savepath);
            AssetDatabase.CreateAsset(btCopy, savepath);
            var newBtAssets = new Dictionary<BlendTree, BlendTree>() { { bt, btCopy } };
            ReplaceOldBTs(newBtAssets);
            AssetDatabase.RemoveObjectFromAsset(bt);
            AssetDatabase.SaveAssets();
            Selection.objects = new Object[1] { btCopy };
            EditorGUIUtility.PingObject(btCopy);
            Logger.Log($"{RichToolName} New {Logger.RichBlendTree} saved in: \"{savepath}\"", btCopy);
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/Add to Animator Controller", true, 301)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/Add to Blend Tree", true, 301)]
        private static bool BlendTreeCanConnect(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var _ = AssetDatabase.LoadMainAssetAtPath(assetPath);
                return menuCommand.context == _;
            }
            return false;
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/Add to Animator Controller", false, 301)]
        private static void BlendTreeConnect_AC(MenuCommand menuCommand)
        {
            var bt = menuCommand.context as BlendTree;
            var btPath = AssetDatabase.GetAssetPath(bt);
            var initsavepanel = new System.IO.FileInfo(btPath).Directory.FullName;
            var savepath = EditorUtility.OpenFilePanelWithFilters("Add BlendTree to Animator Controller", initsavepanel, new string[2] { "Animator Controller", "controller" });
            if (string.IsNullOrEmpty(savepath)) return;
            savepath = System.IO.Path.GetFullPath(savepath);
            var projPath = System.IO.Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) + System.IO.Path.DirectorySeparatorChar;
            if (!savepath.StartsWith(projPath)) { Logger.LogWarning(RichToolName + " Select an " + Logger.RichAnimatorController + " inside this project!"); return; }
            savepath = savepath.Replace(projPath, "");
            BlendTreeConnect(bt, btPath, savepath);
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/Add to Blend Tree", false, 301)]
        private static void BlendTreeConnect_BT(MenuCommand menuCommand)
        {
            var bt = menuCommand.context as BlendTree;
            var btPath = AssetDatabase.GetAssetPath(bt);
            var initsavepanel = new System.IO.FileInfo(btPath).Directory.FullName;
            var savepath = EditorUtility.OpenFilePanelWithFilters("Add BlendTree to BlendTree", initsavepanel, new string[2] { "Blend Tree", "asset" });
            if (string.IsNullOrEmpty(savepath)) return;
            savepath = System.IO.Path.GetFullPath(savepath);
            var projPath = System.IO.Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) + System.IO.Path.DirectorySeparatorChar;
            if (!savepath.StartsWith(projPath)) { Logger.LogWarning(RichToolName + " Select a " + Logger.RichBlendTree + " inside this project!"); return; }
            savepath = savepath.Replace(projPath, "");
            var mainBT = AssetDatabase.LoadMainAssetAtPath(savepath);
            if (mainBT is not BlendTree) { Logger.LogWarning(RichToolName + " The destination asset must be a " + Logger.RichBlendTree); return; }
            if (mainBT == menuCommand.context) { Logger.LogWarning(RichToolName + " Don't add " + Logger.RichBlendTree + " to itself."); return; }
            BlendTreeConnect(bt, btPath, savepath);
        }

        private static void BlendTreeConnect(BlendTree bt, string btPath, string savepath)
        {
            var newBtAssets = new Dictionary<BlendTree, BlendTree>();
            var btAssets = AssetDatabase.LoadAllAssetsAtPath(btPath);
            foreach (var asset in btAssets)
            {
                if (!asset || asset is not BlendTree btAsset) continue;
                var btCopy = new BlendTree();
                EditorUtility.CopySerialized(btAsset, btCopy);
                btCopy.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(btCopy, savepath);
                newBtAssets[btAsset] = btCopy;
            }
            ReplaceOldBTs(newBtAssets, true);
            AssetDatabase.DeleteAsset(btPath);
            AssetDatabase.SaveAssets();
            var mainNew = newBtAssets[bt];
            Selection.objects = new Object[1] { mainNew };
            EditorGUIUtility.PingObject(mainNew);
            Logger.Log($"{RichToolName} {Logger.RichBlendTree} added as sub asset in: \"{savepath}\"", mainNew);
        }

        private static void ReplaceOldBTs(Dictionary<BlendTree, BlendTree> OldNewBTs, bool addingToAsset = false)
        {
            var progBarTitle = addingToAsset ? "Adding BlendTree to Asset" : "Disconnecting BlendTree";

            var acPaths = AssetDatabase.FindAssets("t:AnimatorController").Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToList();
            var btPaths = AssetDatabase.FindAssets("t:BlendTree").Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToHashSet();

            int j = 0;
            for (; j < acPaths.Count; j++)
            {
                var acPath = acPaths[j];
                EditorUtility.DisplayProgressBar(progBarTitle, $"Processing Animator Controller at\n  {acPath}", (float)(j + 1) / acPaths.Count);
                var animatorStates = AssetDatabase.LoadAllAssetsAtPath(acPath).Where(o => o is AnimatorState).Cast<AnimatorState>();
                foreach (var animatorState in animatorStates)
                {
                    using (var so = new SerializedObject(animatorState))
                    {
                        var sp = so.FindProperty("m_Motion");
                        if (sp == null || sp.objectReferenceValue is not BlendTree oldBT || !oldBT) continue;
                        if (OldNewBTs.TryGetValue(oldBT, out var newBT)) sp.objectReferenceValue = newBT;
                        if (so.hasModifiedProperties) so.ApplyModifiedProperties();
                    }
                }
                if (!btPaths.Contains(acPath)) continue;
                btPaths.Remove(acPath);
                var blendTrees = AssetDatabase.LoadAllAssetsAtPath(acPath).Where(o => o is BlendTree).Cast<BlendTree>();
                foreach (var blendTree in blendTrees)
                {
                    ReplaceBTsin1BT(OldNewBTs, blendTree);
                }
            }

            j = 1;
            foreach (var btPath in btPaths)
            {
                EditorUtility.DisplayProgressBar(progBarTitle, $"Processing BlendTrees at\n  {btPath}", (float)j / btPaths.Count);
                var blendTrees = AssetDatabase.LoadAllAssetsAtPath(btPath).Where(o => o is BlendTree).Cast<BlendTree>();
                foreach (var blendTree in blendTrees)
                {
                    ReplaceBTsin1BT(OldNewBTs, blendTree);
                }
                j++;
            }
            EditorUtility.ClearProgressBar();
        }

        private static void ReplaceBTsin1BT(Dictionary<BlendTree, BlendTree> newBtAssets, BlendTree bt)
        {
            using (var so = new SerializedObject(bt))
            {
                var sp = so.FindProperty("m_Childs");
                if (sp == null || sp.arraySize == 0) return;
                for (int i = 0; i < sp.arraySize; i++)
                {
                    var sp_i = sp.GetArrayElementAtIndex(i);
                    var m = sp_i.FindPropertyRelative("m_Motion");
                    if (m == null || m.objectReferenceValue is not BlendTree oldBT || !oldBT) continue;
                    if (newBtAssets.TryGetValue(oldBT, out var newBT)) m.objectReferenceValue = newBT;
                }
                if (so.hasModifiedProperties) so.ApplyModifiedProperties();
            }
        }

        #endregion

        #region Scriptable Object Type

        [MenuItem("CONTEXT/ScriptableObject/FACS Utils/Scriptable Object Type?", true, 0)]
        private static bool CanScriptableObjectType(MenuCommand menuCommand)
        {
            return menuCommand.context.GetType() != typeof(ScriptableObject);
        }

        [MenuItem("CONTEXT/ScriptableObject/FACS Utils/Scriptable Object Type?", false, 0)]
        private static void ScriptableObjectType(MenuCommand menuCommand)
        {
            var t = menuCommand.context.GetType();
            Logger.Log($"{RichToolName} {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag} is {Logger.TypeTag}{t.Name}{Logger.EndTag}");
        }

        #endregion

    }
}
#endif