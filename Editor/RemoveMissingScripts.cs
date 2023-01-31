#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class RemoveMissingScripts : EditorWindow 
    {
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject source;
        private static int GameObjectsScanned;
        private static int missingScriptsCount;
        private static string results;

        [MenuItem("FACS Utils/Repair Avatar/Remove Missing Scripts", false, 1002)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(RemoveMissingScripts), false, "Remove Missing Scripts", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField($"<color=cyan><b>Remove Missing Scripts</b></color>\n\n" +
                $"Scans the selected GameObject and deletes any missing scripts inside it's hierarchy.\n" +
                $"This operation can't be undone.\n", FacsGUIStyles.helpbox);

            EditorGUI.BeginChangeCheck();
            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                NullVars();
                if (source && PrefabUtility.IsPartOfPrefabAsset(source) && PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.Model)
                {
                    Debug.LogWarning($"[<color=green>Copy Materials</color>] Can't edit Model Prefabs: {source.name}\n");
                    source = null;
                }
            }
            if (!source && !String.IsNullOrEmpty(results)) NullVars();

            if (source && GUILayout.Button("Run Fix!", FacsGUIStyles.button, GUILayout.Height(40))) Run(source);
            if (!String.IsNullOrEmpty(results))
            {
                FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(results, FacsGUIStyles.helpbox);
            }
        }
        
        private static void Run(GameObject src)
        {
            NullVars();
            if (PrefabUtility.IsPartOfPrefabAsset(src)) FixPrefab(src);
            else
            {
                var srcRoot = PrefabUtility.IsPartOfPrefabInstance(src) ? PrefabUtility.GetNearestPrefabInstanceRoot(src) : src;
                var gos = srcRoot.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject).Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g));
                var gos_MA = gos.Where(g=> PrefabUtility.GetPrefabAssetType(g) == PrefabAssetType.MissingAsset);
                var gos_nMA = gos.Where(g => PrefabUtility.GetPrefabAssetType(g) != PrefabAssetType.MissingAsset)
                    .Select(g => PrefabUtility.GetCorrespondingObjectFromSource(g)).Distinct();
                foreach (var go in gos_nMA) FixPrefab(go);
                foreach (var go in gos_MA)
                {
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                }
                GameObjectsScanned = 0;
                FindMSAndRemoveResursive(srcRoot);
                source = srcRoot;
            }

            GenerateResults();
            Debug.Log($"[<color=green>Remove Missing Scripts</color>] Finished removing missing scripts!\n");
        }

        private static void FixPrefab(GameObject src2)
        {
            var src = PrefabUtility.InstantiatePrefab(src2) as GameObject;
            if (FindMSAndRemoveResursive(src) > 0)
            {
                PrefabUtility.ApplyPrefabInstance(src, InteractionMode.AutomatedAction);
            }
            DestroyImmediate(src);
        }

        private static int FindMSAndRemoveResursive(GameObject g)
        {
            GameObjectsScanned++;
            int MBWMSCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g);
			if (MBWMSCount > 0) MBWMSCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(g);
            missingScriptsCount += MBWMSCount;
            foreach (Transform childT in g.transform)
            {
                MBWMSCount += FindMSAndRemoveResursive(childT.gameObject);
            }
            return MBWMSCount;
        }

        private static void GenerateResults()
        {
            results = $"Results:\n";
            results += $"   • <color=green>Child GameObjects scanned:</color> {GameObjectsScanned}\n";
            results += $"   • <color=green>Missing scripts deleted:</color> {missingScriptsCount}\n";
        }

        public void OnDestroy()
        {
            source = null;
            FacsGUIStyles = null;
            NullVars();
        }

        private static void NullVars()
        {
            GameObjectsScanned = 0;
            missingScriptsCount = 0;
            results = null;
        }
    }
}
#endif