#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class RemoveComponents : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Remove Components]" + Logger.EndTag;

        private static readonly Type TransformType = typeof(Transform);
        private static readonly Type RectTransformType = typeof(RectTransform);
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject toRemoveFrom;
        private static bool isPrefabAsset = false;
        private static bool hasMissingScripts = false;
        private static ComponentHierarchy componentHierarchy;
        private static bool toggleSelection = true;
        private static bool toggleHideSelection = true;

        [MenuItem("FACS Utils/Miscellaneous/Remove Components", false, 1100)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(RemoveComponents), false, "Remove Components", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter; }
            
            EditorGUILayout.LabelField($"<color=cyan><b>Remove Components</b></color>\n\n" +
                $"Scans the selected GameObject, lists all available Components in it, " +
                $"and lets you choose which ones to delete.\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            toRemoveFrom = (GameObject)EditorGUILayout.ObjectField(toRemoveFrom, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() && toRemoveFrom)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(toRemoveFrom) && PrefabUtility.GetPrefabAssetType(toRemoveFrom) == PrefabAssetType.Model)
                {
                    Logger.LogWarning($"{RichToolName} Can't edit {Logger.RichModelPrefab}: {Logger.AssetTag}{toRemoveFrom.name}{Logger.EndTag}", toRemoveFrom);
                    toRemoveFrom = null;
                }
                ClearOldSelection();
            }
            if (!toRemoveFrom) ClearOldSelection();

            if (toRemoveFrom != null && GUILayout.Button("Scan!", FacsGUIStyles.Button, GUILayout.Height(40))) ScanSelection();

            if (componentHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>Available Components to Delete</b></color>:", FacsGUIStyles.Helpbox);

                bool anyOn = componentHierarchy.DisplayGUI();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();
                if (componentHierarchy.hierarchyDisplay)
                {
                    if (GUILayout.Button("Collapse All", FacsGUIStyles.Button, GUILayout.Height(30))) CollapseAll();
                }
                else
                {
                    if (GUILayout.Button($"{(toggleSelection ? "Select" : "Deselect")} All", FacsGUIStyles.Button, GUILayout.Height(30)))
                    {
                        foreach (var ft in componentHierarchy.toggles_by_type.Values)
                        {
                            ft.SetAllToggles(toggleSelection);
                        }
                        componentHierarchy.SetAllGOEnables(toggleSelection);
                        toggleSelection = !toggleSelection;
                    }
                    if (GUILayout.Button($"{(toggleHideSelection ? "Hide" : "Show")} All", FacsGUIStyles.Button, GUILayout.Height(30)))
                    {
                        componentHierarchy.HideShowAll(toggleHideSelection);
                        toggleHideSelection = !toggleHideSelection;
                    }
                }
                if (GUILayout.Button($"{(componentHierarchy.hierarchyDisplay ? "List" : "Hierarchy")} View", FacsGUIStyles.Button, GUILayout.Height(30)))
                {
                    componentHierarchy.hierarchyDisplay = !componentHierarchy.hierarchyDisplay;
                }
                EditorGUILayout.EndHorizontal();
                if (isPrefabAsset && hasMissingScripts) { EditorGUILayout.LabelField($"<color=orange>Will delete some Missing Scripts to save the Prefab</color>", FacsGUIStyles.HelpboxSmall); }
                if (anyOn && toRemoveFrom != null && GUILayout.Button("Run!", FacsGUIStyles.Button, GUILayout.Height(40))) RunDelete();
            }
        }

        private static void ScanSelection()
        {
            ClearOldSelection();
            toggleSelection = toggleHideSelection = true;
            isPrefabAsset = TrullyPersistent(toRemoveFrom);
            GetAvailableComponentTypes();
        }

        private static void ClearOldSelection()
        {
            isPrefabAsset = false;
            if (componentHierarchy != null) NullCompHierarchy();
        }

        private static bool TrullyPersistent(GameObject go)
        {
            if (!EditorUtility.IsPersistent(go)) return false;
            return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go));
        }

        private void RunDelete()
        {
            var toDelete = componentHierarchy.GetAllToggles(1);
            var actualDeletions = 0;
            var missingScripts = 0;
            if (toDelete.Length > 0)
            {
                Undo.SetCurrentGroupName("Remove Components");
                if (isPrefabAsset && hasMissingScripts)
                {
                    missingScripts = RemoveMissingScripts.RemoveMissingScriptsRecursive(toRemoveFrom, out _);
                }
                foreach (var c in toDelete)
                {
                    if (c) { Undo.DestroyObjectImmediate(c); actualDeletions++; }
                }
                AssetDatabase.SaveAssets();
                if (isPrefabAsset) AssetDatabase.Refresh();
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            }
            ScanSelection();
            Logger.Log($"{RichToolName} Finished deleting <b>{actualDeletions}</b> components{(missingScripts>0?$" and {missingScripts} missing scripts" :"")}!");
        }

        private void CollapseAll()
        {
            componentHierarchy.CollapseHierarchy();
        }

        private static void GetAvailableComponentTypes()
        {
            componentHierarchy = null;

            componentHierarchy = new(toRemoveFrom.transform);

            Transform[] gos_t = toRemoveFrom.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in gos_t)
            {
                Component[] components = t.GetComponents(typeof(Component));
                foreach (Component component in components)
                {
                    if (component)
                    {
                        var cT = component.GetType();
                        if (cT != TransformType && cT != RectTransformType) componentHierarchy.AddComponentToggle(component);
                    }
                    else if (!hasMissingScripts) hasMissingScripts = true;
                }
            }

            componentHierarchy.SortElements(false, true);
            componentHierarchy.SortTypes();
        }

        private static void NullCompHierarchy()
        {
            componentHierarchy = null;
            toggleSelection = true;
            toggleHideSelection = true;
            hasMissingScripts = false;
        }

        private void OnDestroy()
        {
            NullVars();
        }

        private void NullVars()
        {
            FacsGUIStyles = null;
            toRemoveFrom = null;
            NullCompHierarchy();
            ClearOldSelection();
        }
    }
}
#endif