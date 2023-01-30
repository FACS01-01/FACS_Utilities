#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class BetterBounds : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject avatar;
        private static bool avatar_IsNotPartOfPrefabAsset;
        private static ComponentHierarchy skinnedRendHierarchy;
        private static bool toggleSelection = true;

        [MenuItem("FACS Utils/Misc/Better Avatar Bounds", false, 1000)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(BetterBounds), false, "Better Bounds", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter; }
            EditorGUILayout.LabelField($"<color=cyan><b>Better Avatar Bounds</b></color>\n\n" +
                $"Scans the selected GameObject (avatar), lists all available Skinned Mesh Renderers (skipping the ones inside the avatar's Armature), and lets you choose which ones to give a new bounding box.\n\n" +
                $"The 'Loose Bounds' option should fix issues with parts of the avatar disappearing when viewed from certain angles.\n", FacsGUIStyles.helpbox);

            EditorGUI.BeginChangeCheck();
            avatar = (GameObject)EditorGUILayout.ObjectField(avatar, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                NullskinnedRend();
                if (avatar)
                {
                    avatar_IsNotPartOfPrefabAsset = !PrefabUtility.IsPartOfPrefabAsset(avatar);
                    if (!avatar_IsNotPartOfPrefabAsset && PrefabUtility.GetPrefabAssetType(avatar) == PrefabAssetType.Model)
                    {
                        Debug.LogWarning($"[<color=green>Better Bounds</color>] Can't edit Model Prefabs: {avatar.name}\n");
                        avatar = null; return;
                    }
                }
            }
            if (!avatar && skinnedRendHierarchy != null) { NullskinnedRend(); return; }
            if (avatar && GUILayout.Button("Scan!", FacsGUIStyles.button, GUILayout.Height(40))) GetAvailableComponents();

            if (skinnedRendHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>Available Skinned Mesh Renderers to Modify</b></color>:", FacsGUIStyles.helpbox);

                bool anyOn = skinnedRendHierarchy.DisplayGUI();
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"{(toggleSelection ? "Select" : "Deselect")} All", FacsGUIStyles.button, GUILayout.Height(30)))
                {
                    SelectAll(toggleSelection); toggleSelection = !toggleSelection;
                }
                if (skinnedRendHierarchy.hierarchyDisplay)
                {
                    if (GUILayout.Button("Collapse All", FacsGUIStyles.button, GUILayout.Height(30))) CollapseAll();
                }
                if (GUILayout.Button($"{(skinnedRendHierarchy.hierarchyDisplay ? "Simple" : "Hierarchy")} View", FacsGUIStyles.button, GUILayout.Height(30)))
                {
                    skinnedRendHierarchy.hierarchyDisplay = !skinnedRendHierarchy.hierarchyDisplay;
                }
                EditorGUILayout.EndHorizontal();

                if (anyOn)
                {
                    if (GUILayout.Button("Exact Bounds", FacsGUIStyles.button, GUILayout.Height(40)))
                    {
                        var tmp = skinnedRendHierarchy.GetAllToggles<SkinnedMeshRenderer>(1);
                        float N = 1.0f / tmp.Count; float n = N / 2;
                        foreach (var smr in tmp)
                        {
                            EditorUtility.DisplayProgressBar("Better Avatar Bounds - Exact Bounds", $"Processing: {smr.name}", n);
                            Undo.RecordObject(smr, "Better Bounds");
                            ExactBounds(smr);
                            if (avatar_IsNotPartOfPrefabAsset) SaveChangesOnPrefabInstance(smr);
                            n += N;
                        }
                        EditorUtility.ClearProgressBar();
                        if (!avatar_IsNotPartOfPrefabAsset) PrefabUtility.SavePrefabAsset(avatar);
                        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                        Debug.Log($"[<color=green>Better Bounds</color>] Finished applying Exact Bounds to: {avatar.name}\n");
                    }
                    if (GUILayout.Button("Loose Bounds", FacsGUIStyles.button, GUILayout.Height(40)))
                    {
                        var tmp = skinnedRendHierarchy.GetAllToggles<SkinnedMeshRenderer>(1);
                        float N = 1.0f / tmp.Count; float n = N / 2;
                        foreach (var smr in tmp)
                        {
                            EditorUtility.DisplayProgressBar("Better Avatar Bounds - Loose Bounds", $"Processing: {smr.name}", n);
                            Undo.RecordObject(smr, "Better Bounds");
                            ExactBounds(smr);
                            n += N;
                        }
                        EditorUtility.ClearProgressBar();
                        Dictionary<Transform, List<SkinnedMeshRenderer>> groups = new Dictionary<Transform, List<SkinnedMeshRenderer>>();
                        foreach (var smr in tmp)
                        {
                            if (groups.ContainsKey(smr.rootBone)) groups[smr.rootBone].Add(smr);
                            else groups.Add(smr.rootBone, new List<SkinnedMeshRenderer> { smr });
                        }
                        foreach (var l in groups.Values) LooseBounds(l);
                        if (!avatar_IsNotPartOfPrefabAsset) PrefabUtility.SavePrefabAsset(avatar);
                        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                        Debug.Log($"[<color=green>Better Bounds</color>] Finished applying Loose Bounds to: {avatar.name}\n");
                    }
                }
            }
        }

        private void SaveChangesOnPrefabInstance(Object obj)
        {
            switch (PrefabUtility.GetPrefabAssetType(obj))
            {
                case PrefabAssetType.Regular:
                case PrefabAssetType.Variant:
                case PrefabAssetType.Model:
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                        break;
                    }
                case PrefabAssetType.MissingAsset:
                    {
                        PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(obj),
                            PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                        SaveChangesOnPrefabInstance(obj);
                        break;
                    }
                default:
                    break;
            }
        }

        private void ExactBounds(SkinnedMeshRenderer smr)
        {
            smr.updateWhenOffscreen = false;

            var rootBone = smr.rootBone;
            if (!rootBone || !smr.bones.Contains(rootBone)) rootBone = smr.bones[0];

            var deltaPos = smr.transform.position - rootBone.position;
            var deltaRot = smr.transform.rotation;
            var smrMatrix = Matrix4x4.TRS(deltaPos, deltaRot, Vector3.one);
            var rootBoneMatrix = rootBone.worldToLocalMatrix;
            Mesh tempMesh = new Mesh(); smr.BakeMesh(tempMesh);
            tempMesh.vertices = tempMesh.vertices.Select(v => (Vector3)(rootBoneMatrix*smrMatrix.MultiplyPoint3x4(v))).ToArray();
            tempMesh.RecalculateBounds();
            smr.localBounds = tempMesh.bounds; smr.rootBone = rootBone;
        }

        private void LooseBounds(List<SkinnedMeshRenderer> smrL)
        {
            Bounds finalBounds = smrL[0].localBounds;
            if (smrL.Count > 1)
            {
                for (int i = 1; i < smrL.Count; i++)
                {
                    finalBounds.Encapsulate(smrL[i].localBounds);
                }
            }

            var minX = finalBounds.min.x; var maxX = finalBounds.max.x;
            var minY = finalBounds.min.y; var maxY = finalBounds.max.y;
            var minZ = finalBounds.min.z; var maxZ = finalBounds.max.z;
            //hip bone (Root Bone) needs to be inside bounding box for next step
            if (minX > 0 || minY > 0 || minZ > 0 || maxX < 0 || maxY < 0 || maxZ < 0)
            {
                finalBounds.extents *= 1.1f;
                foreach (SkinnedMeshRenderer smr in smrL)
                {
                    smr.localBounds = finalBounds;
                    if (avatar_IsNotPartOfPrefabAsset) SaveChangesOnPrefabInstance(smr);
                }
                return;
            }

            var Max = Mathf.Max(maxX, -minX, maxY, -minY, maxZ, -minZ) * 1.1f;
            Bounds finalBounds2 = new Bounds();
            finalBounds2.center = new Vector3(0, 0, 0);
            finalBounds2.extents = new Vector3(Max, Max, Max);
            foreach (SkinnedMeshRenderer smr in smrL)
            {
                smr.localBounds = finalBounds2;
                if (avatar_IsNotPartOfPrefabAsset) SaveChangesOnPrefabInstance(smr);
            }
        }

        private void SelectAll(bool yesno)
        {
            skinnedRendHierarchy.SetAllToggles(typeof(SkinnedMeshRenderer).GetHashCode(), yesno);
        }

        private void CollapseAll()
        {
            skinnedRendHierarchy.CollapseHierarchy();
        }

        private void GetAvailableComponents()
        {
            NullskinnedRend();
            skinnedRendHierarchy = new ComponentHierarchy(avatar.transform); skinnedRendHierarchy.hideTypeFoldout = true;
            bool any = false;
            var avtSMRs = avatar.GetComponents<SkinnedMeshRenderer>();
            foreach (var avtSMR in avtSMRs)
            {
                if (avtSMR.sharedMesh && avtSMR.bones.Length > 0)
                {
                    skinnedRendHierarchy.AddComponentToggle(avtSMR);
                    any = true;
                }
            }
            foreach (Transform childT in avatar.transform)
            {
                if (childT.name == "Armature") continue;
                SkinnedMeshRenderer[] childSMRL = childT.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var childSMR in childSMRL)
                {
                    if (childSMR.sharedMesh && childSMR.bones.Length > 0)
                    {
                        skinnedRendHierarchy.AddComponentToggle(childSMR);
                        any = true;
                    }
                }
            }
            skinnedRendHierarchy.SortElements(false, true);
            if (!any)
            {
                Debug.LogWarning($"[<color=green>Better Bounds</color>] No valid skinned mesh renderers to edit in: {avatar.name}\n");
                NullskinnedRend();
            }
        }

        public void OnDestroy()
        {
            NullVars();
        }

        private void NullskinnedRend()
        {
            skinnedRendHierarchy = null;
            toggleSelection = true;
        }

        private void NullVars()
        {
            NullskinnedRend();
            FacsGUIStyles = null;
            avatar = null;
        }
    }
}
#endif