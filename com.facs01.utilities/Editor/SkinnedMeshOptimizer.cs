#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class SkinnedMeshOptimizer : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Skinned Mesh Optimizer]" + Logger.EndTag;
        private static readonly System.Type[] AnimationSourceTypes = new System.Type[] { typeof(RuntimeAnimatorController), typeof(AnimationClip) };
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject rootGO;
        private static NewMesh[] newMeshes;
        private static Vector3[] baseVertices = null;
        private static Vector3[] baseNormals = null;
        private static bool hasNewNormals = false;
        private static Vector4[] baseTangents = null;
        private static bool hasNewTangents = false;
        private static bool displayBlendShapes = false;
        private static bool blendShapeFoldout = false;
        private static bool animationSourcesFoldout = false;
        private static bool addNonOptimized = false;
        private static AnimationsFromGOs AFGOs = new();
        private static Vector2 scrollView;
        private static string outputMessage;

        [MenuItem("FACS Utils/Avatar Tools/Skinned Mesh Optimizer", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(SkinnedMeshOptimizer), false, "Skinned Mesh Optimizer", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Foldout.alignment = TextAnchor.MiddleCenter; }
            
            EditorGUILayout.LabelField($"<color=cyan><b>Skinned Mesh Optimizer</b></color>\n\n" +
                $"Finds all Skinned Mesh Renderers in the selected GameObject/Prefab/FBX (avatar), and helps you remove blendshapes and unused internal bone references from them.\n" +
                $"The selected GameObject is cloned into a new Prefab asset, with new Mesh files embedded as sub-assets.\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            rootGO = (GameObject)EditorGUILayout.ObjectField(rootGO, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck()) NullVars();
            if (rootGO) { if (GUILayout.Button("Scan!", FacsGUIStyles.Button, GUILayout.Height(40))) Scan(); }
            else if (newMeshes != null) NullVars();

            if (newMeshes != null)
            {
                if (displayBlendShapes)
                {
                    var rect = EditorGUILayout.BeginHorizontal();
                    EditorGUI.DrawRect(rect, GUITools.GetTintedBGColor(0.05f));
                    blendShapeFoldout = GUILayout.Toggle(blendShapeFoldout, "<color=green><b>Available BlendShapes to Remove, by Mesh:</b></color>", FacsGUIStyles.Foldout, GUILayout.Height(16), GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();

                    if (blendShapeFoldout)
                    {
                        scrollView = EditorGUILayout.BeginScrollView(scrollView, GUILayout.ExpandHeight(true));
                        foreach (var nm in newMeshes)
                        {
                            if (nm.BSData == null) continue;
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.BeginChangeCheck();
                            nm.BSDataFoldout = GUILayout.Toggle(nm.BSDataFoldout, nm.oldMesh.name, GUITools.FoldoutStyle, GUILayout.Height(18), GUILayout.ExpandWidth(true));
                            if (EditorGUI.EndChangeCheck()) GUI.FocusControl(null);
                            //GUILayout.FlexibleSpace();
                            if (nm.BSData[0].ApplyDelete)
                            {
                                if (GUILayout.Button("None", FacsGUIStyles.Button, GUILayout.Height(18), GUILayout.Width(45))) foreach (var bsdata in nm.BSData) bsdata.ApplyDelete = false;
                            }
                            else
                            {
                                if (GUILayout.Button("All", FacsGUIStyles.Button, GUILayout.Height(18), GUILayout.Width(45))) foreach (var bsdata in nm.BSData) bsdata.ApplyDelete = true;
                            }
                            if (GUILayout.Button("Empties Only", FacsGUIStyles.Button, GUILayout.Height(18), GUILayout.Width(95)))
                            {
                                foreach (var bsdata in nm.BSData) { if (bsdata.isEmpty) bsdata.ApplyDelete = true; else bsdata.ApplyDelete = false; }
                            }
                            EditorGUILayout.EndHorizontal();
                            if (nm.BSDataFoldout)
                            {
                                foreach (var bsdata in nm.BSData)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.Space(11);
                                    bsdata.ApplyDelete = GUILayout.Toggle(bsdata.ApplyDelete, bsdata.name, GUILayout.Height(19), GUILayout.MinWidth(nm.contentWidth));
                                    GUILayout.FlexibleSpace();

                                    EditorGUI.BeginDisabledGroup(bsdata.isEmpty || !bsdata.ApplyDelete);
                                    if (bsdata.isEmpty)
                                    {
                                        EditorGUILayout.LabelField("------------------", GUILayout.Height(18), GUILayout.Width(126));
                                    }
                                    else
                                    {
                                        bsdata.weight = EditorGUILayout.FloatField(bsdata.weight, GUILayout.Height(18), GUILayout.MaxWidth(84));
                                        EditorGUILayout.LabelField($"/{bsdata.maxWeight}", GUILayout.Height(18), GUILayout.Width(nm.contentWidth2));
                                    }

                                    EditorGUI.EndDisabledGroup();

                                    EditorGUILayout.EndHorizontal();
                                }

                            }
                            EditorGUILayout.Space(2);
                        }
                        EditorGUILayout.EndScrollView();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space(30);

                        if (GUILayout.Button("All", FacsGUIStyles.Button, GUILayout.Height(20))) SetAllBSApply(true);
                        if (GUILayout.Button("Empties Only", FacsGUIStyles.Button, GUILayout.Height(20))) SetOnAllBSEmpies();
                        if (GUILayout.Button("None", FacsGUIStyles.Button, GUILayout.Height(20))) SetAllBSApply(false);

                        EditorGUILayout.Space(30);
                        EditorGUILayout.EndHorizontal();
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.Space(10);

                    rect = EditorGUILayout.BeginHorizontal();
                    EditorGUI.DrawRect(rect, GUITools.GetTintedBGColor(0.05f));
                    animationSourcesFoldout = GUILayout.Toggle(animationSourcesFoldout, "<color=green><b>Keep BlendShapes from Animation Sources:</b></color>", FacsGUIStyles.Foldout, GUILayout.Height(16), GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();

                    if (animationSourcesFoldout)
                    {
                        AFGOs.OnGUI(FacsGUIStyles, this.position.size.x);
                        if (AFGOs.animationRoots.Count > 1)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.Space(30);
                            EditorGUILayout.BeginVertical();
                            if (GUILayout.Button("Auto Detect", FacsGUIStyles.Button, GUILayout.Height(20))) AutoDetect();
                            if (AFGOs.animationSources.Any(animS => animS.Count > 1) &&
                                GUILayout.Button("Deselect Blendshapes", FacsGUIStyles.Button, GUILayout.Height(20))) DeselectBlendshapes();
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.Space(30);
                            EditorGUILayout.EndHorizontal();
                            GUILayout.FlexibleSpace();
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"<color=green><b>No BlendShapes to Optimize</b></color>", FacsGUIStyles.Helpbox);
                }
                EditorGUILayout.Space(10);
                GUITools.ColoredToggle(ref addNonOptimized, "Add Non Optimized Meshes to Prefab", GUILayout.Height(22));
                if (GUILayout.Button("Run!", FacsGUIStyles.Button, GUILayout.Height(40))) Run();
            }

            if (!string.IsNullOrEmpty(outputMessage))
            {
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleLeft;
                scrollView = EditorGUILayout.BeginScrollView(scrollView, GUILayout.ExpandHeight(true));
                EditorGUILayout.LabelField(outputMessage, FacsGUIStyles.Helpbox);
                EditorGUILayout.EndScrollView();
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void Scan()
        {
            NullVars();
            var SMRs = rootGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (SMRs.Length == 0) { ShowNotification(new GUIContent("No Skinned Mesh Renderer found!")); return; }
            if (SMRs.All(smr => !smr.sharedMesh)) { ShowNotification(new GUIContent("No Mesh in any\nSkinned Mesh Renderer?")); return; }

            newMeshes = SMRs.Where(smr => smr.sharedMesh).GroupBy(smr => smr.sharedMesh).Select(gr => new NewMesh(gr.First())).ToArray();
            displayBlendShapes = newMeshes.Any(nm => nm.BSData != null);
            if (displayBlendShapes) { AFGOs = new(); AFGOs.animationRoots.Insert(0, rootGO); AFGOs.animationSources.Add(new() { null }); }
            foreach (var nm in newMeshes)
            {
                if (nm.BSData == null) continue;
                var maxContentWidth = nm.BSData.Max(bsd => GUI.skin.toggle.CalcSize(new GUIContent(bsd.name)).x) + 20;
                var maxContentWidth2 = nm.BSData.Max(bsd => GUI.skin.toggle.CalcSize(new GUIContent(bsd.maxWeight.ToString())).x);
                nm.contentWidth = maxContentWidth; nm.contentWidth2 = maxContentWidth2;
            }
        }

        private void SetAllBSApply(bool b)
        {
            foreach (var nm in newMeshes)
            {
                if (nm.BSData == null) continue;
                foreach (var bsdata in nm.BSData) bsdata.ApplyDelete = b;
            }
        }

        private void SetOnAllBSEmpies()
        {
            foreach (var nm in newMeshes)
            {
                if (nm.BSData == null) continue;
                foreach (var bsdata in nm.BSData)
                {
                    if (bsdata.isEmpty) bsdata.ApplyDelete = true;
                    else bsdata.ApplyDelete = false;
                }
            }
        }

        private void AutoDetect()
        {
            if (!AFGOs.AutoDetect(RichToolName)) ShowNotification(new GUIContent("No Animation Root\nhas Animation Sources"));
        }

        private void Run()
        {
            float totalMeshes = newMeshes.Length;
            float meshN = 0.5f;
            foreach (var nm in newMeshes)
            {
                EditorUtility.DisplayProgressBar("FACS Utilities - Skinned Mesh Optimizer", $"Optimizing \"{nm.oldMesh.name}\"...", meshN / totalMeshes);
                meshN++;
                OptimizeMesh(nm);
            }
            EditorUtility.ClearProgressBar();
            if (newMeshes.All(nm => !nm.newMesh))
            {
                ShowNotification(new GUIContent("All Meshes are\noptimized already!"));
                foreach (var nm in newMeshes) if (nm.newMesh0) DestroyImmediate(nm.newMesh0, true);
                return;
            }
            var newRootGO = Instantiate(rootGO);
            RemoveMissingScripts.RemoveMissingScriptsRecursive(newRootGO, out _);

            const string OutputDir = "Assets/FACS Optimized Meshes";
            if (!System.IO.Directory.Exists(OutputDir)) System.IO.Directory.CreateDirectory(OutputDir);
            var newPrefabPath = OutputDir + "/" + rootGO.name + ".prefab";
            newPrefabPath = AssetDatabase.GenerateUniqueAssetPath(newPrefabPath);
            var newPrefab = PrefabUtility.SaveAsPrefabAsset(newRootGO, newPrefabPath);
            DestroyImmediate(newRootGO);

            var mesh_SMR = newPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(smr => smr.sharedMesh)
                .GroupBy(smr => smr.sharedMesh).Where(gr => newMeshes.Any(nm => nm.oldMesh == gr.Key)).ToDictionary(gr => gr.Key, gr => gr.ToArray());

            foreach (var nm in newMeshes)
            {
                if (!nm.newMesh)
                {
                    if (nm.newMesh0)
                    {
                        AssetDatabase.AddObjectToAsset(nm.newMesh0, newPrefabPath);
                        var SMRs = mesh_SMR[nm.oldMesh];
                        foreach (var smr in SMRs)
                        {
                            smr.sharedMesh = nm.newMesh0;
                        }
                    }
                    continue;
                }
                OptimizeSMRs(nm, newPrefabPath, mesh_SMR[nm.oldMesh], out var oldB, out var newB);
            }

            AssetDatabase.SaveAssets();
            Selection.objects = new Object[1] { newPrefab };
            EditorGUIUtility.PingObject(newPrefab);

            GenerateReport(newPrefab);
            newMeshes = null; scrollView = default;
        }

        private void DeselectBlendshapes()
        {
            var rootCount = AFGOs.animationRoots.Count - 1;
            var deselCount = 0;
            var anyBSfound = false;
            for (int i = 0; i < rootCount; i++)
            {
                var GO = AFGOs.animationRoots[i];

                HashSet<AnimationClip> ACs = new();
                foreach (var obj in AFGOs.animationSources[i])
                {
                    if (!obj) continue;
                    if (obj is AnimationClip ac) ACs.Add(ac);
                    else if (obj is RuntimeAnimatorController rac)
                    {
                        foreach (var ac2 in rac.animationClips)
                        {
                            if (!ac2) continue;
                            ACs.Add(ac2);
                        }
                    }
                }

                var rootMeshes = GO.GetComponents<SkinnedMeshRenderer>().Where(smr => smr.sharedMesh).Select(smr => smr.sharedMesh).ToHashSet();
                Dictionary<string, NewMesh[]> pathsToMeshes = new() { { "", newMeshes.Where(nm => rootMeshes.Contains(nm.oldMesh)).ToArray() } };
                foreach (var ac in ACs)
                {
                    foreach (var cb in AnimationUtility.GetCurveBindings(ac))
                    {
                        if (cb.type != typeof(SkinnedMeshRenderer) || !cb.propertyName.StartsWith("blendShape.")) continue;
                        if (!pathsToMeshes.TryGetValue(cb.path, out var nms))
                        {
                            var t = GO.transform.Find(cb.path);
                            if (!t) nms = new NewMesh[0];
                            else
                            {
                                var meshes = t.GetComponents<SkinnedMeshRenderer>().Where(smr => smr.sharedMesh).Select(smr => smr.sharedMesh).ToHashSet();
                                nms = newMeshes.Where(nm => meshes.Contains(nm.oldMesh)).ToArray();
                            }
                            pathsToMeshes[cb.path] = nms;
                        }
                        if (nms.Length == 0) continue;
                        var blendShapeName = cb.propertyName[11..];
                        foreach (var nm in nms)
                        {
                            foreach (var bsd in nm.BSData)
                            {
                                if (bsd.name == blendShapeName)
                                {
                                    anyBSfound = true;
                                    if (bsd.ApplyDelete) { bsd.ApplyDelete = false; deselCount++; }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            if (deselCount != 0) Logger.Log($"{RichToolName} Deselected <b>{deselCount}</b> blendshapes!");
            else
            {
                if (anyBSfound) Logger.Log($"{RichToolName} No extra blendshape was deselected.");
                else
                {
                    ShowNotification(new GUIContent("No Blendshape\nfrom Animations\nfound?"));
                    Logger.LogWarning($"{RichToolName} No Blendshape from selected Meshes was found in Animation Sources.");
                }
            }
        }

        private void GenerateReport(GameObject newPrefab)
        {
            int totalRMVBoneRefs = 0; int oldTotalBoneRefs = 0;
            int totalRMVBSs = 0; int oldTotalBSs = 0;

            var NMs = newMeshes.Where(nm => nm.newMesh).ToArray();
            foreach (var bs in NMs)
            {
                bs.bonesRMVpct = (bs.newBonesCount - bs.oldBonesCount) * 100f / bs.oldBonesCount;
                bs.bsRMVpct = (bs.newBSCount - bs.oldBSCount) * 100f / bs.oldBSCount;
                totalRMVBoneRefs += (bs.oldBonesCount - bs.newBonesCount) * bs.SMRCount; oldTotalBoneRefs += bs.oldBonesCount * bs.SMRCount;
                totalRMVBSs += (bs.oldBSCount - bs.newBSCount) * bs.SMRCount; oldTotalBSs += bs.oldBSCount * bs.SMRCount;
            }
            int totalOptimizedMeshes = NMs.Length;

            var optimizedBones = NMs.Where(nm => nm.oldBonesCount != nm.newBonesCount).ToList();
            optimizedBones = optimizedBones.OrderBy(e => e.bonesRMVpct).ThenByDescending(e => e.SMRCount).ToList();

            var optimizedBSs = NMs.Where(nm => nm.oldBSCount != nm.newBSCount).ToList();
            optimizedBSs = optimizedBSs.OrderBy(e => e.bsRMVpct).ThenByDescending(e => e.SMRCount).ToList();

            var sb = new System.Text.StringBuilder();
            sb.Append($"\n<b>New Prefab: <color=cyan>{newPrefab.name}</color></b>\n");
            sb.Append($"<b><color=green>Total Optimized Meshes:</color> {totalOptimizedMeshes}</b> <color=green>out of</color> <b>{newMeshes.Length}</b>\n");
            sb.Append($"\n <color=green>Optimized Meshes (<b>Bone References</b>):</color> <b>{optimizedBones.Count}</b>\n");
            foreach (var ob in optimizedBones)
            {
                sb.Append($"   • <color=cyan>{ob.oldMesh.name}:</color> ");
                sb.Append($"<b>{ob.oldBonesCount}</b> \u21d2 <b>{ob.newBonesCount}</b> ");
                sb.Append($"(<b>{((ob.newBonesCount - ob.oldBonesCount) * 100f / ob.oldBonesCount):0.0}%</b>)\n");
            }
            sb.Append($"\n <color=green>Optimized Meshes (<b>Blendshapes</b>):</color> <b>{optimizedBSs.Count}</b>\n");
            foreach (var ob in optimizedBSs)
            {
                sb.Append($"   • <color=cyan>{ob.oldMesh.name}:</color> ");
                sb.Append($"<b>{ob.oldBSCount}</b> \u21d2 <b>{ob.newBSCount}</b> ");
                sb.Append($"(<b>{((ob.newBSCount - ob.oldBSCount) * 100f / ob.oldBSCount):0.0}%</b>)\n");
            }

            outputMessage = sb.ToString();
            Logger.Log($"{RichToolName} Finished optimizing <b>{totalOptimizedMeshes}</b> meshes! " +
                $"<b>{totalRMVBoneRefs}</b> (<b>{(totalRMVBoneRefs * 100f / oldTotalBoneRefs):0.0}%</b>) bone references removed, and " +
                $"<b>{totalRMVBSs}</b> (<b>{(totalRMVBSs * 100f / oldTotalBSs):0.0}%</b>) blendshapes removed!", newPrefab);
        }

        private void OptimizeSMRs(NewMesh nm, string newPrefabPath, SkinnedMeshRenderer[] SMRs, out int oldTotalBoneRefs, out int newTotalBoneRefs)
        {
            nm.SMRCount = SMRs.Length;
            oldTotalBoneRefs = newTotalBoneRefs = 0;
            AssetDatabase.AddObjectToAsset(nm.newMesh, newPrefabPath);
            foreach (var smr in SMRs)
            {
                using (var so = new SerializedObject(smr))
                {
                    var mesh = so.FindProperty("m_Mesh");
                    mesh.objectReferenceValue = nm.newMesh;
                    var bones = so.FindProperty("m_Bones");
                    var boneCount = bones.arraySize;
                    oldTotalBoneRefs += boneCount;

                    if (boneCount > nm.oldBonesCount) { for (int i = nm.oldBonesCount; i < boneCount; i++) bones.DeleteArrayElementAtIndex(nm.oldBonesCount); }
                    if (nm.removeIdxs != null)
                    {
                        for (int i = 0; i < nm.removeIdxs.Length; i++)
                        {
                            var rmvIdx = nm.removeIdxs[i]-i;
                            if (rmvIdx < boneCount) { bones.DeleteArrayElementAtIndex(rmvIdx); boneCount--; }
                        }
                    }

                    var bsWeights = so.FindProperty("m_BlendShapeWeights");
                    var weightCount = bsWeights.arraySize;
                    var weightsTotal = nm.oldBSCount;
                    if (weightCount > weightsTotal) for (int i = weightsTotal; i < weightCount; i++) bsWeights.DeleteArrayElementAtIndex(weightsTotal);
                    else if (weightCount < weightsTotal) weightsTotal = weightCount;
                    if (nm.BSData != null)
                    {
                        var bsDeleteCount = 0;
                        for (int i = 0; i < weightsTotal; i++)
                        {
                            if (nm.BSData[i].ApplyDelete)
                            {
                                bsWeights.DeleteArrayElementAtIndex(i-bsDeleteCount);
                                bsDeleteCount++;
                            }
                        }
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                    newTotalBoneRefs += so.FindProperty("m_Bones").arraySize;
                }
            }
        }

        private void OptimizeMesh(NewMesh nm)
        {
            nm.newMesh = Instantiate(nm.oldMesh);
            if (nm.oldMesh.name.EndsWith("(OPTIMIZED)"))
            {
                nm.newMesh.name = nm.oldMesh.name[..^1] + "*)";
            }
            else if (nm.oldMesh.name.EndsWith("(OPTIMIZED*)"))
            {
                nm.newMesh.name = nm.oldMesh.name;
            }
            else
            {
                nm.newMesh.name = nm.oldMesh.name + "(OPTIMIZED)";
            }
            var boneWeights = nm.oldMesh.GetAllBoneWeights();
            bool[] KeepBoneIdx = null;
            int toDeleteCount = 0;
            using (var so = new SerializedObject(nm.newMesh))
            {
                var bindPose = so.FindProperty("m_BindPose");
                var boneCount = bindPose.arraySize;
                nm.oldBonesCount = boneCount;
                KeepBoneIdx = new bool[boneCount];
                foreach (var bw in boneWeights) if (bw.boneIndex < boneCount) KeepBoneIdx[bw.boneIndex] = true;
                
                var rootBoneHash = so.FindProperty("m_RootBoneNameHash").uintValue;
                var boneNameHashes = so.FindProperty("m_BoneNameHashes");
                var boneNameHashesCount = boneNameHashes.arraySize;
                if (boneNameHashesCount > 0)
                {
                    var boneNameHashIter = boneNameHashes.GetArrayElementAtIndex(0);
                    for (int i = 0; i < boneNameHashesCount; i++)
                    {
                        if (rootBoneHash == boneNameHashIter.uintValue) { KeepBoneIdx[i] = true; break; }
                        boneNameHashIter.Next(false);
                    }
                }

                toDeleteCount = KeepBoneIdx.Count(b => !b);
                nm.newBonesCount = boneCount - toDeleteCount;
                if (toDeleteCount == 0) goto BlendShapeOptimizations;
                nm.removeIdxs = new int[toDeleteCount];
                int rmvIdx = 0;
                for (int i = 0; i < KeepBoneIdx.Length; i++) if (!KeepBoneIdx[i]) nm.removeIdxs[rmvIdx++] = i;

                var bonesAABB = so.FindProperty("m_BonesAABB");

                for (int i = 0; i < nm.removeIdxs.Length; i++)
                {
                    rmvIdx = nm.removeIdxs[i]-i;
                    bindPose.DeleteArrayElementAtIndex(rmvIdx);
                    bonesAABB.DeleteArrayElementAtIndex(rmvIdx);
                    boneNameHashes.DeleteArrayElementAtIndex(rmvIdx);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Dictionary<int, int> newBoneIdxs = new();
            int boneIdx = 0;
            int firstDiffBoneIdx = -1;
            for (int i = 0; i < KeepBoneIdx.Length; i++)
            {
                if (KeepBoneIdx[i]) newBoneIdxs.Add(i, boneIdx++);
                else if (firstDiffBoneIdx == -1) firstDiffBoneIdx = i;
            }
            var weightsArray = new Unity.Collections.NativeArray<BoneWeight1>(boneWeights, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < weightsArray.Length; i++)
            {
                var oldW = weightsArray[i];
                if (oldW.boneIndex < firstDiffBoneIdx) continue;
                oldW.boneIndex = newBoneIdxs[oldW.boneIndex];
                weightsArray[i] = oldW;
            }
            var bonesPerVertex = nm.oldMesh.GetBonesPerVertex();
            nm.newMesh.SetBoneWeights(bonesPerVertex, weightsArray);
            bonesPerVertex.Dispose();
            weightsArray.Dispose();

        BlendShapeOptimizations:
            boneWeights.Dispose();
            if (nm.BSData != null && nm.BSData.All(bs => !bs.ApplyDelete)) nm.BSData = null;
            if (nm.BSData == null)
            {
                if (toDeleteCount == 0)
                {
                    if (addNonOptimized) { nm.newMesh0 = nm.newMesh; nm.newMesh0.name = nm.oldMesh.name; }
                    else { DestroyImmediate(nm.newMesh, true); }
                    nm.newMesh = null;
                }
                return;
            }
            
            nm.newBSCount = nm.BSData.Count(bs => !bs.ApplyDelete);
            baseVertices = baseNormals = null; baseTangents = null;
            hasNewNormals = false; hasNewTangents = false;
            if (nm.BSData.Any(bs => bs.ApplyDelete && !bs.isEmpty))
            {
                baseVertices = nm.newMesh.vertices;
                baseNormals = nm.newMesh.normals;
                baseTangents = nm.newMesh.tangents;
            }
            using (var so = new SerializedObject(nm.newMesh))
            {
                var Channels = so.FindProperty("m_Shapes.channels");
                var Shapes = so.FindProperty("m_Shapes.shapes");
                var FullWeights = so.FindProperty("m_Shapes.fullWeights");
                var Vertices = so.FindProperty("m_Shapes.vertices");
                for (int i = 0; i < nm.BSData.Length; i++)
                {
                    var bsData = nm.BSData[i];
                    if (bsData.ApplyDelete && !bsData.isEmpty) ApplyBlendShape(i, bsData.weight, Channels, Shapes, FullWeights, Vertices);
                }
            }
            DeleteBlendShape(nm);
            if (baseVertices != null)
            {
                nm.newMesh.vertices = baseVertices; baseVertices = null;
                if (hasNewNormals) nm.newMesh.normals = baseNormals;
                baseNormals = null; hasNewNormals = false;
                if (hasNewTangents) nm.newMesh.tangents = baseTangents;
                baseTangents = null; hasNewTangents = false;
            }
        }

        private void DeleteBlendShape(NewMesh nm)
        {
            nm.newMesh.ClearBlendShapes();
            if (nm.BSData.All(bsData => bsData.ApplyDelete)) return;
            using (var so = new SerializedObject(nm.oldMesh))
            {
                var deltaVertices = new Vector3[nm.oldMesh.vertexCount];
                var deltaNormals = new Vector3[nm.oldMesh.vertexCount];
                var deltaTangents = new Vector3[nm.oldMesh.vertexCount];
                var Channels = so.FindProperty("m_Shapes.channels");
                var Shapes = so.FindProperty("m_Shapes.shapes");
                var FullWeights = so.FindProperty("m_Shapes.fullWeights");
                var channel_iter = Channels.GetArrayElementAtIndex(0);
                for (int i = 0; i < nm.BSData.Length; i++, channel_iter.Next(false))
                {
                    var bsData = nm.BSData[i];
                    if (bsData.ApplyDelete) continue;

                    var shapeIdxBeing = channel_iter.FindPropertyRelative("frameIndex").intValue;
                    var shapeIdxEnd = shapeIdxBeing + channel_iter.FindPropertyRelative("frameCount").intValue;
                    for (int j = shapeIdxBeing; j < shapeIdxEnd; j++)
                    {
                        var weight = FullWeights.GetArrayElementAtIndex(j).floatValue;
                        var shape_ij = Shapes.GetArrayElementAtIndex(j);
                        Vector3[] deltaNormals2 = null; Vector3[] deltaTangents2 = null;
                        if (shape_ij.FindPropertyRelative("hasNormals").boolValue) deltaNormals2 = deltaNormals;
                        if (shape_ij.FindPropertyRelative("hasTangents").boolValue) deltaTangents2 = deltaTangents;
                        nm.oldMesh.GetBlendShapeFrameVertices(i, j-shapeIdxBeing, deltaVertices, deltaNormals2, deltaTangents2);
                        nm.newMesh.AddBlendShapeFrame(bsData.name, weight, deltaVertices, deltaNormals2, deltaTangents2);
                    }
                }
            }
        }

        private void ApplyBlendShape(int bsIdx, float weight, SerializedProperty Channels, SerializedProperty Shapes, SerializedProperty FullWeights, SerializedProperty Vertices)
        {
            var bsChannel = Channels.GetArrayElementAtIndex(bsIdx);
            var frameArrOffset = bsChannel.FindPropertyRelative("frameIndex").intValue;
            var frameWeight0 = FullWeights.GetArrayElementAtIndex(frameArrOffset).floatValue;
            if (weight == 0 && frameWeight0 > 0) return;
            if (frameWeight0 == weight) { AddDeltaVertices(Shapes, Vertices, frameArrOffset); return; }
            if (weight < frameWeight0) { AddDeltaVerticesWeighted(Shapes, Vertices, weight / frameWeight0, frameArrOffset); return; }
            var frameCount = bsChannel.FindPropertyRelative("frameCount").intValue;
            for (int i = 1; i < frameCount; i++)
            {
                var frameWeight = FullWeights.GetArrayElementAtIndex(frameArrOffset + i).floatValue;
                if (frameWeight == weight) { AddDeltaVertices(Shapes, Vertices, frameArrOffset + i); return; }
                if (weight < frameWeight)
                {
                    var w1 = (weight - frameWeight0) / (frameWeight - frameWeight0);
                    AddDeltaVerticesWeighted(Shapes, Vertices, w1, frameArrOffset + i - 1);
                    AddDeltaVerticesWeighted(Shapes, Vertices, 1 - w1, frameArrOffset + i);
                    return;
                }
                frameWeight0 = frameWeight;
            }
            AddDeltaVerticesWeighted(Shapes, Vertices, weight / frameWeight0, frameArrOffset + frameCount - 1);
        }

        private void AddDeltaVertices(SerializedProperty Shapes, SerializedProperty Vertices, int frameIdx)
        {
            var bsFrame = Shapes.GetArrayElementAtIndex(frameIdx);
            var hasNormals = bsFrame.FindPropertyRelative("hasNormals").boolValue;
            var hasTangents = bsFrame.FindPropertyRelative("hasTangents").boolValue;
            var vertexCount = bsFrame.FindPropertyRelative("vertexCount").intValue;

            if (!hasNewNormals) hasNewNormals = hasNormals;
            if (!hasNewTangents) hasNewTangents = hasTangents;
            if (vertexCount > 0)
            {
                var deltaVertIter = Vertices.GetArrayElementAtIndex(bsFrame.FindPropertyRelative("firstVertex").intValue);
                for (int i = 0; i < vertexCount; i++)
                {
                    var vertIdx = deltaVertIter.FindPropertyRelative("index").intValue;
                    baseVertices[vertIdx] += deltaVertIter.FindPropertyRelative("vertex").vector3Value;
                    if (hasNormals) baseNormals[vertIdx] += deltaVertIter.FindPropertyRelative("normal").vector3Value;
                    if (hasTangents)
                    {
                        var deltaV3 = deltaVertIter.FindPropertyRelative("tangent").vector3Value;
                        baseTangents[vertIdx] += new Vector4(deltaV3.x, deltaV3.y, deltaV3.z, 0);
                    }
                    deltaVertIter.Next(false);
                }
            }
        }

        private void AddDeltaVerticesWeighted(SerializedProperty Shapes, SerializedProperty Vertices, float w, int frameIdx)
        {
            var bsFrame = Shapes.GetArrayElementAtIndex(frameIdx);
            var hasNormals = bsFrame.FindPropertyRelative("hasNormals").boolValue;
            var hasTangents = bsFrame.FindPropertyRelative("hasTangents").boolValue;
            var vertexCount = bsFrame.FindPropertyRelative("vertexCount").intValue;

            if (!hasNewNormals) hasNewNormals = hasNormals;
            if (!hasNewTangents) hasNewTangents = hasTangents;
            if (vertexCount > 0)
            {
                var deltaVert = Vertices.GetArrayElementAtIndex(bsFrame.FindPropertyRelative("firstVertex").intValue);
                for (int i = 0; i < vertexCount; i++)
                {
                    var vertIdx = deltaVert.FindPropertyRelative("index").intValue;
                    baseVertices[vertIdx] += deltaVert.FindPropertyRelative("vertex").vector3Value * w;
                    if (hasNormals) baseNormals[vertIdx] += deltaVert.FindPropertyRelative("normal").vector3Value * w;
                    if (hasTangents)
                    {
                        var deltaV3 = deltaVert.FindPropertyRelative("tangent").vector3Value;
                        baseTangents[vertIdx] += new Vector4(deltaV3.x, deltaV3.y, deltaV3.z, 0) * w;
                    }
                    deltaVert.Next(false);
                }
            }
        }

        private void OnDestroy()
        {
            FacsGUIStyles = null;
            rootGO = null;
            addNonOptimized = false;
            NullVars();
        }

        private void NullVars()
        {
            newMeshes = null;
            baseVertices = null;
            baseNormals = null;
            hasNewNormals = false;
            baseTangents = null;
            hasNewTangents = false;
            displayBlendShapes = false;
            blendShapeFoldout = false;
            animationSourcesFoldout = false;
            AFGOs = null;
            scrollView = default;
            outputMessage = null;
        }

        record NewMesh
        {
            public Mesh oldMesh;
            public Mesh newMesh = null;
            public Mesh newMesh0 = null;
            public int oldBonesCount = 0;
            public int newBonesCount = 0;
            public int[] removeIdxs = null;
            public BlendShapeData[] BSData = null;
            public int oldBSCount = 0;
            public int newBSCount = 0;
            public int SMRCount = 0;
            public float bonesRMVpct = 0;
            public float bsRMVpct = 0;
            public bool BSDataFoldout = false;
            public float contentWidth;
            public float contentWidth2;

            public NewMesh(SkinnedMeshRenderer smr)
            {
                oldMesh = smr.sharedMesh;
                using (var so = new SerializedObject(oldMesh))
                {
                    var Channels = so.FindProperty("m_Shapes.channels");
                    if (Channels.arraySize == 0) return;
                    var Shapes = so.FindProperty("m_Shapes.shapes");
                    var FullWeights = so.FindProperty("m_Shapes.fullWeights");
                    BSData = new BlendShapeData[Channels.arraySize];
                    oldBSCount = newBSCount = BSData.Length;
                    var bsChannelIter = Channels.GetArrayElementAtIndex(0);
                    for (int i = 0; i < BSData.Length; i++)
                    {
                        BSData[i] = new(bsChannelIter.FindPropertyRelative("name").stringValue);
                        int frameIdxBegin = bsChannelIter.FindPropertyRelative("frameIndex").intValue;
                        var frameCount = bsChannelIter.FindPropertyRelative("frameCount").intValue;
                        if (frameCount > 0)
                        {
                            var bsShape_ijIter = Shapes.GetArrayElementAtIndex(frameIdxBegin);
                            for (int j = 0; j < frameCount; j++)
                            {
                                var bsShape_ij = Shapes.GetArrayElementAtIndex(j);
                                if (bsShape_ij.FindPropertyRelative("vertexCount").intValue > 0)
                                {
                                    BSData[i].isEmpty = false;
                                    BSData[i].ApplyDelete = false;
                                    break;
                                }
                                bsShape_ijIter.Next(false);
                            }
                            BSData[i].maxWeight = FullWeights.GetArrayElementAtIndex(frameIdxBegin + frameCount - 1).floatValue;
                        }
                        bsChannelIter.Next(false);
                    }
                }
                for (int i = 0; i < BSData.Length; i++) BSData[i].weight = smr.GetBlendShapeWeight(i);
            }

            public record BlendShapeData
            {
                public string name;

                public bool isEmpty = true;
                public bool ApplyDelete = true;
                public float weight = 0;
                public float maxWeight = 1;

                public BlendShapeData(string bsName)
                {
                    name = bsName;
                }
            }
        }
    }
}
#endif