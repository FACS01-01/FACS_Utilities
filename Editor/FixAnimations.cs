#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FACS01.Utilities
{
    public class FixAnimations : EditorWindow
    {
        private GameObject source;
        private AnimatorController animContr;
        private AnimatorOverrideController animContrOR;

        private static FACSGUIStyles FacsGUIStyles;

        private readonly string[] brokenPropertyNameKeys = { "blendShape.", "typetree_", "script_", "ParticleSystem_", "material." };

        private Transform sourceT;
        private Dictionary<string, int> Paths_nPath;
        private Dictionary<uint, (string,int)> Hashes_Paths_nPath;
        private List<Dictionary<string,int>> nPath_Types_nType;
        private List<List<List<string>>> nPath_nType_Props;
        private List<List<Dictionary<uint,string>>> nPath_nType_Hashes_Props;
        private int nPath, nType;
        private string results;
        private int[] results_int;
        private bool isBroken;

        private float totalTasks = 1.0f;
        private int nTask = 0;
        private float progBar = 0.0f;
        private List<(AnimationClip, EditorCurveBinding)> allAnimClipsCB;
        private List<Transform> allPathsTransforms;

        [MenuItem("FACS Utils/Repair Avatar/Fix Animations Missing Paths", false, 1004)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(FixAnimations), false, "Fix Animations Paths", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField($"<color=cyan><b>Fix Animations Missing Paths</b></color>\n\n" +
                $"Scans Animations inside the selected Animator Controller or Animator Override, " +
                $"and tries to repair missing paths, comparing them to the selected GameObject's hierarchy.\n\n" +
                $"This will overwrite all modified Animations, but can be reverted with Undo.\n", FacsGUIStyles.helpbox);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck()) NullVars();
            EditorGUILayout.BeginVertical();
            int height;
            if (animContr == null && animContrOR == null) height = 20; else height = 40;
            if (animContrOR == null) animContr = (AnimatorController)EditorGUILayout.ObjectField(animContr, typeof(AnimatorController), true, GUILayout.Height(height));
            if (animContr == null) animContrOR = (AnimatorOverrideController)EditorGUILayout.ObjectField(animContrOR, typeof(AnimatorOverrideController), true, GUILayout.Height(height));
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if ((source != null && (animContr != null || animContrOR != null)) && GUILayout.Button("Run Fix!", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                RunFix();
            }
            if (results != null && results != "")
            {
                FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(results, FacsGUIStyles.helpbox);
            }
        }

        public void RunFix()
        {
            sourceT = source.transform;
            Paths_nPath = new Dictionary<string, int>();
            Hashes_Paths_nPath = new Dictionary<uint, (string, int)>();
            nPath_Types_nType = new List<Dictionary<string, int>>();
            nPath_nType_Props = new List<List<List<string>>>();
            nPath_nType_Hashes_Props = new List<List<Dictionary<uint, string>>>();
            nPath = 0; nType = 0;
            results_int = new int[] {0,0,0,0,0,0,0,0};
            allPathsTransforms = new List<Transform>();

            Gen_ComponentLists(sourceT);

            IEnumerable<AnimationClip> allAnimClips;
            if (animContr != null) allAnimClips = animContr.animationClips.Distinct();
            else allAnimClips = animContrOR.animationClips.Distinct();
            
            allAnimClipsCB = new List<(AnimationClip, EditorCurveBinding)>();
            foreach (AnimationClip animClip in allAnimClips)
            {
                EditorCurveBinding[] objCurveBinds = AnimationUtility.GetObjectReferenceCurveBindings(animClip);
                EditorCurveBinding[] flCurveBinds = AnimationUtility.GetCurveBindings(animClip);
                if (objCurveBinds.Length > 0) foreach (EditorCurveBinding curveBind in objCurveBinds) allAnimClipsCB.Add((animClip, curveBind));
                if (flCurveBinds.Length > 0) foreach (EditorCurveBinding curveBind in flCurveBinds) allAnimClipsCB.Add((animClip, curveBind));
            }
            
            totalTasks = allAnimClipsCB.Count() + allPathsTransforms.Count();
            nTask = 0;
            progBar = 0.0f;

            foreach (Transform t in allPathsTransforms)
            {
                EditorUtility.DisplayProgressBar("Animation Fixer", "Please wait...", progBar);
                Gen_ComponentLists2(t);
                nTask++;
                progBar = nTask / totalTasks;
            }

            foreach ((AnimationClip, EditorCurveBinding) task in allAnimClipsCB)
            {
                EditorUtility.DisplayProgressBar("Animation Fixer", "Please wait...", progBar);
                AnalizePath(task.Item1, task.Item2);
                nTask++;
                progBar = nTask/totalTasks;
            }

            GenerateResults();
            EditorUtility.ClearProgressBar();

            var AnimationWindow = Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
            var windows = Resources.FindObjectsOfTypeAll(AnimationWindow);
            if (windows != null && windows.Length > 0)
            {
                var window = (EditorWindow)windows[0];
                if (window) window.Repaint();
            }

            Debug.Log($"[<color=green>Fix Animations</color>] Finished fixing animations!\n");
        }

        private void Gen_ComponentLists(Transform t)
        {
            allPathsTransforms.Add(t);
            foreach (Transform childT in t) Gen_ComponentLists(childT);
        }

        private void Gen_ComponentLists2(Transform t)
        {
            var gPath = AnimationUtility.CalculateTransformPath(t, sourceT);
            var gPath_hash = (uint)Animator.StringToHash(gPath);

            var l_cB_Types = AnimationUtility.GetAnimatableBindings(t.gameObject, source).GroupBy(o => o.type.ToString()).Select(o => o.ToList());

            nType = 0;
            List<Dictionary<uint, string>> tmp4 = new List<Dictionary<uint, string>>();
            Dictionary<string, int> tmp2 = new Dictionary<string, int>();
            var nType_Props = new List<List<string>>();
            foreach (var l_cB_1type in l_cB_Types)
            {
                tmp2.Add(l_cB_1type[0].type.ToString(), nType);

                var l_cB_uniques = l_cB_1type.Distinct().ToList();

                Dictionary<uint, string> tmp3 = new Dictionary<uint, string>();
                List<string> props = new List<string>();
                foreach (EditorCurveBinding cB in l_cB_uniques)
                {
                    props.Add(cB.propertyName);
                    uint addkey;
                    if (cB.propertyName.StartsWith("blendShape."))
                    {
                        addkey = (uint)Animator.StringToHash(cB.propertyName.Substring(11)) & 0xFFFFFFF;
                    }
                    else if (cB.propertyName.StartsWith("material."))
                    {
                        if (cB.propertyName.EndsWith(".x"))
                        {
                            addkey = (uint)Animator.StringToHash(cB.propertyName.Substring(9).Remove(cB.propertyName.Length - 11)) & 0xFFFFFFF;
                        }
                        else if (cB.propertyName.EndsWith(".y"))
                        {
                            addkey = ((uint)Animator.StringToHash(cB.propertyName.Substring(9).Remove(cB.propertyName.Length - 11)) & 0xFFFFFFF) + (uint)268435456;
                        }
                        else if (cB.propertyName.EndsWith(".z"))
                        {
                            addkey = ((uint)Animator.StringToHash(cB.propertyName.Substring(9).Remove(cB.propertyName.Length - 11)) & 0xFFFFFFF) + (uint)536870912;
                        }
                        else if (cB.propertyName.EndsWith(".w"))
                        {
                            addkey = ((uint)Animator.StringToHash(cB.propertyName.Substring(9).Remove(cB.propertyName.Length - 11)) & 0xFFFFFFF) + (uint)805306368;
                        }
                        else
                        {
                            addkey = (uint)Animator.StringToHash(cB.propertyName.Substring(9)) & 0xFFFFFFF;
                        }
                    }
                    else
                    {
                        addkey = (uint)Animator.StringToHash(cB.propertyName) & 0xFFFFFFF;
                    }
                    if (!tmp3.ContainsKey(addkey))
                    {
                        tmp3.Add(addkey, cB.propertyName);
                    }
                }
                if (l_cB_1type[0].type.ToString() == "UnityEngine.Transform")
                {//transform component, weird angles hax
                    props.Add("localEulerAnglesRaw.x");
                    tmp3.Add((uint)Animator.StringToHash("localEulerAnglesRaw.x") & 0xFFFFFFF, "localEulerAnglesRaw.x");
                    props.Add("localEulerAnglesRaw.y");
                    tmp3.Add((uint)Animator.StringToHash("localEulerAnglesRaw.y") & 0xFFFFFFF, "localEulerAnglesRaw.y");
                    props.Add("localEulerAnglesRaw.z");
                    tmp3.Add((uint)Animator.StringToHash("localEulerAnglesRaw.z") & 0xFFFFFFF, "localEulerAnglesRaw.z");
                }

                tmp4.Add(tmp3);

                nType_Props.Add(props);
                nType++;
            }

            if (!Paths_nPath.TryGetValue(gPath, out int nnpath))
            {
                Paths_nPath.Add(gPath, nPath);
                Hashes_Paths_nPath.Add(gPath_hash, (gPath, nPath));

                nPath_Types_nType.Add(tmp2);
                nPath_nType_Hashes_Props.Add(tmp4);
                nPath_nType_Props.Add(nType_Props);

                nPath++;
            }
        }

        private void AnalizePath(AnimationClip ac, EditorCurveBinding cB)
        {
            isBroken = false;
            string cBpath = cB.path;
            if (Paths_nPath.TryGetValue(cBpath, out nPath))
            {
                AnalizeType(ac, cB, cBpath);
            }
            else if (cBpath.StartsWith("path_") && uint.TryParse(cBpath.Substring(5), out uint hash))
            {
                if (Hashes_Paths_nPath.TryGetValue(hash, out (string, int) t))
                {
                    nPath = t.Item2;
                    isBroken = true;
                    AnalizeType(ac, cB, t.Item1);
                }
                else
                {
                    // hashed path in GO doesnt exist
                    results_int[1]++;
                    Debug.LogWarning($"[<color=green>Fix Animations</color>] <color=yellow>Selected GameObject doesn't have hashed path:</color> {cBpath}<color=yellow>. AnimationClip:</color> {ac.name}\n");
                }
            }
            else
            {
                // path in GO doesnt exist
                results_int[2]++;
                Debug.LogWarning($"[<color=green>Fix Animations</color>] <color=yellow>Selected GameObject doesn't have path:</color> {cBpath}<color=yellow>. AnimationClip:</color> {ac.name}\n");
            }
        }

        private void AnalizeType(AnimationClip ac, EditorCurveBinding cB, string newPath)
        {
            string cBtype = cB.type.ToString();
            if (nPath_Types_nType[nPath].TryGetValue(cBtype, out int nt))
            {
                nType = nt;
                AnalizeProp(ac, cB, newPath, cB.type);
            }
            else if (cBtype == "UnityEngine.MonoBehaviour")
            {
                if (nPath_Types_nType[nPath].TryGetValue("DynamicBone", out nt))
                {
                    nType = nt;
                    Type newType = Type.GetType("DynamicBone, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
                    if (newType != null)
                    {
                        isBroken = true;
                        AnalizeProp(ac, cB, newPath, newType);
                    }
                }
                else if (nPath_Types_nType[nPath].TryGetValue("DynamicBoneCollider", out nt))
                {
                    nType = nt;
                    Type newType = Type.GetType("DynamicBoneCollider, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
                    if (newType != null)
                    {
                        isBroken = true;
                        AnalizeProp(ac, cB, newPath, newType);
                    }
                }
            }
            else
            {
                // type not found in path
                results_int[4]++;
                Debug.LogWarning($"[<color=green>Fix Animations</color>] <color=yellow><b>{cBtype}</b> component not found in path:</color> {newPath}<color=yellow>. AnimationClip:</color> {ac.name}\n");
            }
        }

        private void AnalizeProp(AnimationClip ac, EditorCurveBinding cB, string newPath, Type newType)
        {
            string cBpropName = cB.propertyName;

            if (nPath_nType_Props[nPath][nType].Contains(cBpropName))
            {
                if (isBroken)
                {
                    SetCurveFix(ac, cB, newType, newPath, cBpropName);
                    return;
                }
                else
                {
                    // it was fully working already
                    results_int[7]++;
                    return;
                }
            }
            foreach (string key in brokenPropertyNameKeys)
            {
                if (cBpropName.StartsWith(key) && uint.TryParse(cBpropName.Substring(key.Length), out uint hash))
                {
                    if (nPath_nType_Hashes_Props[nPath][nType].TryGetValue(hash & 0xFFFFFFFF, out string newPropName))
                    {
                        SetCurveFix(ac, cB, newType, newPath, newPropName);
                        return;
                    }
                    else if (nPath_nType_Hashes_Props[nPath][nType].TryGetValue(hash & 0xFFFFFFF, out newPropName))
                    {
                        SetCurveFix(ac, cB, newType, newPath, newPropName);
                        return;
                    }
                    else
                    {
                        // property name not present
                        results_int[5]++;
                        Debug.LogWarning($"[<color=green>Fix Animations</color>] {newType} <color=yellow>property name</color> {cBpropName} <color=yellow>not found, at path:</color> {newPath}<color=yellow>. AnimationClip:</color> {ac.name}\n");
                        return;
                    }
                }
            }

            // unknown property name format
            results_int[6]++;
            Debug.LogWarning($"[<color=green>Fix Animations</color>] <color=yellow>Unknown</color> {newType} <color=yellow>property name</color> {cBpropName} <color=yellow>at path:</color> {newPath}<color=yellow>. AnimationClip:</color> {ac.name}\n");
        }

        private void SetCurveFix(AnimationClip ac, EditorCurveBinding cB, System.Type newtype, string newpath, string newpropertyName)
        {
            Undo.RegisterCompleteObjectUndo(ac, "Animation Fixes");
            //Debug.Log($"<color=yellow>OLD</color>: TYPE:" + cB.type+ "<color=cyan>||</color>PATH:" + cB.path+ "<color=cyan>||</color>PROP:" + cB.propertyName+
            //    "\n                   <color=green>NEW</color>: TYPE:" + newtype.ToString()+ "<color=cyan>||</color>PATH:" + newpath + "<color=cyan>||</color>PROP:" + newpropertyName);
            AnimationCurve floatCurve = AnimationUtility.GetEditorCurve(ac, cB);
            if (floatCurve != null)
            {
                AnimationUtility.SetEditorCurve(ac, cB, null);
                cB.type = newtype;
                cB.path = newpath;
                cB.propertyName = newpropertyName;
                AnimationUtility.SetEditorCurve(ac, cB, floatCurve);
            }
            else
            {
                ObjectReferenceKeyframe[] objectCurve = AnimationUtility.GetObjectReferenceCurve(ac, cB);
                AnimationUtility.SetObjectReferenceCurve(ac, cB, null);
                cB.type = newtype;
                cB.path = newpath;
                cB.propertyName = newpropertyName;
                AnimationUtility.SetObjectReferenceCurve(ac, cB, objectCurve);
            }
            //animation property was fixed
            results_int[0]++;
        }

        private void GenerateResults()
        {
            results = $"Results:\n   • <color=green>Already working properties:</color> {results_int[7]}\n   • <color=green>Fixed properties:</color> {results_int[0]}\n";
            if (results_int[2] > 0) results += $"   • <color=gray>Paths not found in GameObject:</color> {results_int[2]}\n";
            if (results_int[1] > 0) results += $"   • <color=gray>Encrypted paths not found in GameObject:</color> {results_int[1]}\n";
            if (results_int[4] > 0) results += $"   • <color=gray>Components not found in respective paths:</color> {results_int[4]}\n";
            if (results_int[5] > 0) results += $"   • <color=yellow>Properties not found in respective component&path:</color> {results_int[5]}\n";
            if (results_int[6] > 0) results += $"   • <color=yellow>Properties with unknown format:</color> {results_int[6]}\n";
            if (results_int[3] > 0) results += $"   • <color=yellow>May need Dynamic Bones for fixing:</color> {results_int[3]}\n";
        }

        void OnDestroy()
        {
            source = null;
            animContr = null;
            animContrOR = null;
            FacsGUIStyles = null;
            NullVars();
        }

        void NullVars()
        {
            Paths_nPath = null;
            Hashes_Paths_nPath = null;
            nPath_Types_nType = null;
            nPath_nType_Props = null;
            nPath_nType_Hashes_Props = null;
            allAnimClipsCB = null;
            allPathsTransforms = null;
            sourceT = null;
            results = null;
            results_int = null;
        }
    }
}
#endif