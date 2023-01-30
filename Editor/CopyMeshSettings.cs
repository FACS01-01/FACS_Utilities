#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class CopyMeshSettings : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject copyFrom;
        private static GameObject copyTo;
        private static ComponentHierarchy componentHierarchy;
        private static Dictionary<Transform, Transform> GO_Dictionary; //from,to
        private static Dictionary<Component, Component> C_Dictionary; //from,to
        private bool toggleSelection = false;

        [MenuItem("FACS Utils/Misc/Copy Mesh Settings", false, 1001)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(CopyMeshSettings), false, "Copy Mesh Settings", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter; }
            EditorGUILayout.LabelField($"<color=cyan><b>Copy Mesh Settings</b></color>\n\n" +
                $"Scans the selected GameObject to copy from, lists all available Mesh Renderer and Skinned Mesh Renderer components, " +
                $"and lets you choose which ones to copy materials and settings into the next GameObject.\n" +
                $"This doesn't replace the meshes currently in place.", FacsGUIStyles.helpbox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"<b>Copy From</b>", FacsGUIStyles.helpbox);
            EditorGUI.BeginChangeCheck();
            copyFrom = (GameObject)EditorGUILayout.ObjectField(copyFrom, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() || (!copyFrom && componentHierarchy != null)) NulldRends();
            EditorGUILayout.EndVertical();
            if (componentHierarchy != null)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"<b>Copy To</b>", FacsGUIStyles.helpbox);
                EditorGUI.BeginChangeCheck();
                copyTo = (GameObject)EditorGUILayout.ObjectField(copyTo, typeof(GameObject), true, GUILayout.Height(40));
                if (EditorGUI.EndChangeCheck())
                {
                    if (copyTo && PrefabUtility.IsPartOfPrefabAsset(copyTo) && PrefabUtility.GetPrefabAssetType(copyTo) == PrefabAssetType.Model)
                    {
                        Debug.LogWarning($"[<color=green>Copy Mesh Settings</color>] Can't edit Model Prefabs: {copyTo.name}\n");
                        copyTo = null;
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            if (copyFrom != null && GUILayout.Button("Scan!", FacsGUIStyles.button, GUILayout.Height(40))) GetAvailableComponents();

            if (componentHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>Available Renderers to Copy Settings from</b></color>:", FacsGUIStyles.helpbox);

                bool anyOn = componentHierarchy.DisplayGUI();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();
                if (!componentHierarchy.hierarchyDisplay && GUILayout.Button($"{(toggleSelection ? "Select" : "Deselect")} All", FacsGUIStyles.button, GUILayout.Height(30)))
                {
                    SelectAll(toggleSelection); toggleSelection = !toggleSelection;
                }
                if (componentHierarchy.hierarchyDisplay)
                {
                    if (GUILayout.Button("Collapse All", FacsGUIStyles.button, GUILayout.Height(30))) CollapseAll();
                }
                if (GUILayout.Button($"{(componentHierarchy.hierarchyDisplay ? "Simple" : "Hierarchy")} View", FacsGUIStyles.button, GUILayout.Height(30)))
                {
                    componentHierarchy.hierarchyDisplay = !componentHierarchy.hierarchyDisplay;
                }
                EditorGUILayout.EndHorizontal();

                if (anyOn && copyTo != null && GUILayout.Button("Copy!", FacsGUIStyles.button, GUILayout.Height(40))) RunCopy();
            }
        }

        private void SelectAll(bool yesno)
        {
            componentHierarchy.SetAllToggles(typeof(SkinnedMeshRenderer).GetHashCode(), yesno);
            componentHierarchy.SetAllToggles(typeof(MeshRenderer).GetHashCode(), yesno);
        }

        private void RunCopy()
        {
            GODictionary(copyFrom.transform, copyTo.transform);
            CollapseAll();
            componentHierarchy.UseUnfoldedAsMarker();
            ComponentDictionary();
            CopyMaterialsDict();
            CollapseAll();
            if (PrefabUtility.IsPartOfPrefabAsset(copyTo)) PrefabUtility.SavePrefabAsset(copyTo);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Debug.Log($"[<color=green>Copy Mesh Settings</color>] Finished copying materials!\n");
        }

        private void CopyMaterialsDict()
        {
            foreach (var pair in C_Dictionary)
            {
                var rendFrom = (Renderer)pair.Key; var rendTo = (Renderer)pair.Value;

                var rendCopyFrom_mats = rendFrom.sharedMaterials;
                var rendCopyTo_mats = rendTo.sharedMaterials;
                int IndexFrom = rendCopyFrom_mats.Length; int IndexTo = rendCopyTo_mats.Length;
                Material[] tempMats = new Material[IndexTo];

                int CommonIndex = Math.Min(IndexFrom, IndexTo);
                for (int j = 0; j < CommonIndex; j++)
                {
                    if (rendCopyFrom_mats[j] && AssetDatabase.IsMainAsset(rendCopyFrom_mats[j]))
                    {
                        tempMats[j] = rendCopyFrom_mats[j];
                    }
                    else tempMats[j] = rendCopyTo_mats[j];
                }
                if (IndexTo > IndexFrom)
                {
                    for (int j = CommonIndex; j < IndexTo; j++)
                    {
                        tempMats[j] = rendCopyTo_mats[j];
                    }
                }

                Undo.RecordObject(rendTo, "Copy Mesh Settings");
                rendTo.gameObject.SetActive(rendFrom.gameObject.activeSelf);
                rendTo.enabled = rendFrom.enabled;
                rendTo.sharedMaterials = tempMats;
                rendTo.shadowCastingMode = rendFrom.shadowCastingMode;
                rendTo.receiveShadows = rendFrom.receiveShadows;
                rendTo.lightProbeUsage = rendFrom.lightProbeUsage;
                rendTo.reflectionProbeUsage = rendFrom.reflectionProbeUsage;
                rendTo.allowOcclusionWhenDynamic = rendFrom.allowOcclusionWhenDynamic;

                if (rendTo.GetType() == typeof(SkinnedMeshRenderer))
                {
                    var smrFrom = (SkinnedMeshRenderer)rendFrom;
                    var smrTo = (SkinnedMeshRenderer)rendTo;
                    var meshFrom = smrFrom.sharedMesh; var meshTo = smrTo.sharedMesh;
                    if (meshFrom && meshTo)
                    {
                        for (int i = 0; i < meshFrom.blendShapeCount; i++)
                        {
                            var bsToIndex = meshTo.GetBlendShapeIndex(meshFrom.GetBlendShapeName(i));
                            if (bsToIndex != -1) smrTo.SetBlendShapeWeight(bsToIndex, smrFrom.GetBlendShapeWeight(i));
                        }
                    }

                    smrTo.localBounds = smrFrom.localBounds;
                    smrTo.updateWhenOffscreen = smrFrom.updateWhenOffscreen;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(rendTo) && PrefabUtility.GetPrefabAssetType(rendTo) != PrefabAssetType.MissingAsset)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rendTo);
            }
        }

        private void ComponentDictionary()
        {
            C_Dictionary = new Dictionary<Component, Component>();
            foreach (var THC in componentHierarchy.toggles_by_type.Keys)
            {
                var T = ComponentHierarchy.Types[THC].type;
                var CtoCopy = componentHierarchy.GetAllToggles(THC, 1);
                foreach (var pair in GO_Dictionary)
                {
                    var t_from = pair.Key; var t_to = pair.Value;
                    var Cs_from = t_from.GetComponents(T); if (Cs_from.Length == 0) continue;
                    if (t_to == null)
                    {
                        Debug.LogWarning($"[<color=green>Copy Mesh Settings</color>] Couldn't copy ({Cs_from.Length}) {T.Name} because the destination GameObject doesn't exist: {AnimationUtility.CalculateTransformPath(t_from, copyFrom.transform)}\n");
                        continue;
                    }
                    var Cs_to = t_to.GetComponents(T);
                    for (int i = 0; i < Cs_from.Length; i++)
                    {
                        var Cs_from_i = Cs_from[i];
                        var copy = CtoCopy != null && CtoCopy.Contains(Cs_from_i);
                        if (!copy) continue;
                        else if (i < Cs_to.Length)
                        {
                            C_Dictionary.Add(Cs_from_i, Cs_to[i]);
                        }
                        else
                        {
                            Debug.LogWarning($"[<color=green>Copy Mesh Settings</color>] Couldn't copy {T.Name} because there are less {T.Name}s to match on the destination GameObject: {AnimationUtility.CalculateTransformPath(t_from, copyFrom.transform)}\n");
                        }
                    }
                }
            }
        }

        private void GODictionary(Transform from, Transform to)
        {
            var TDict = new Dictionary<Transform, Transform>();
            TDict.Add(from, to);
            GODictionaryRecursive(TDict, from, to);
            GO_Dictionary = TDict;
        }

        private void GODictionaryRecursive(Dictionary<Transform, Transform> dict, Transform from, Transform to)
        {
            int toChildsN = to.childCount;
            if (toChildsN == 0)
            {
                GODictionaryNullRecursive(dict, from); return;
            }
            var toList = new List<Transform>();
            foreach (Transform t in to)
            {
                toList.Add(t);
            }
            foreach (Transform ch_from in from)
            {
                bool match = false;
                for (int i = 0; i < toList.Count; i++)
                {
                    if (ch_from.name == toList[i].name)
                    {
                        dict.Add(ch_from, toList[i]); toList.RemoveAt(i); match = true;
                        if (ch_from.childCount > 0) GODictionaryRecursive(dict, ch_from, dict[ch_from]);
                        break;
                    }
                }
                if (!match)
                {
                    dict.Add(ch_from, null);
                    GODictionaryNullRecursive(dict, ch_from);
                }
            }
        }

        private void GODictionaryNullRecursive(Dictionary<Transform, Transform> dict, Transform from)
        {
            foreach (Transform t in from)
            {
                dict.Add(t, null);
                GODictionaryNullRecursive(dict, t);
            }
        }

        private void CollapseAll()
        {
            componentHierarchy.CollapseHierarchy();
        }

        private void GetAvailableComponents()
        {
            componentHierarchy = null;
            componentHierarchy = new ComponentHierarchy(copyFrom.transform); componentHierarchy.hideTypeFoldout = true;
            bool any = false;
            var smr = copyFrom.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in smr)
            {
                var hasValidMat = false;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat && AssetDatabase.IsMainAsset(mat))
                    {
                        any = hasValidMat = true;
                    }
                }
                if (hasValidMat) componentHierarchy.AddComponentToggle(r);
            }
            var mr = copyFrom.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in mr)
            {
                var hasValidMat = false;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat && AssetDatabase.IsMainAsset(mat))
                    {
                        any = hasValidMat = true;
                    }
                }
                if (hasValidMat) componentHierarchy.AddComponentToggle(r);
            }
            componentHierarchy.SortElements(false, true);
            if (!any)
            {
                Debug.LogWarning($"[<color=green>Copy Mesh Settings</color>] No valid Renderer component to copy settings from: {copyFrom.name}\n");
                NulldRends();
            }
        }
        public void OnDestroy()
        {
            NullVars();
        }
        private void NulldRends()
        {
            componentHierarchy = null;
            GO_Dictionary = null;
            C_Dictionary = null;
            toggleSelection = true;
        }
        private void NullVars()
        {
            FacsGUIStyles = null;
            copyFrom = null;
            copyTo = null;
            NulldRends();
        }
    }
}
#endif