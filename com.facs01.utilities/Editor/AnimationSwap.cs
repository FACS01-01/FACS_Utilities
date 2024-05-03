#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class AnimationSwap : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Animation Swap]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private static AnimatorController animatorController;
        private static string animatorControllerGUID;
        private static string[] blendTreeNames = null;
        private static AnimatorControllerHierarchy animatorHierarchy;

        private Vector2 scrollView = new();

        [MenuItem("FACS Utils/Animation/Animation Swap", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(AnimationSwap), false, "Animation Swap", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter; }
            EditorGUILayout.LabelField($"<color=cyan><b>Animation Swap</b></color>\n\n" +
                $"Scans the selected Animator Controller, lists all Animation Clips in it and where are used, and lets you replace them by other Animation Clips.\n" +
                $"Dependencies on Blend Trees external to the selected Animator Controller, will be cloned and added as sub assets.\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            animatorController = (AnimatorController)EditorGUILayout.ObjectField(animatorController, typeof(AnimatorController), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck()) NullVars();
            if (!animatorController) { NullVars(); return; }

            if (GUILayout.Button("Scan!", FacsGUIStyles.Button, GUILayout.Height(40)))
            {
                if (!RunScan()) return;
                ShowNotification(new GUIContent("Scan Done!"), 1.5f);
            }

            var canRun = false;
            if (animatorHierarchy != null)
            {
                scrollView = EditorGUILayout.BeginScrollView(scrollView, GUILayout.ExpandHeight(true));
                foreach (var kvp in animatorHierarchy.clipReplacers)
                {
                    var originalClip = kvp.Key; var props = kvp.Value;
                    var paths = props.paths;
                    EditorGUILayout.BeginHorizontal();
                    props.foldout = GUILayout.Toggle(props.foldout, props.foldoutText, FacsGUIStyles.Foldout, GUILayout.Height(21), GUILayout.Width(props.foldoutWidth));
                    var firsttoggle = paths[0].replace;
                    if (GUILayout.Button($"{(firsttoggle ? "X" : "\u2713")}", GUILayout.Width(21)))
                    {
                        foreach (var path in paths) path.replace = !firsttoggle;
                    }
                    if (GUILayout.Button('\u2197'.ToString(), GUILayout.Width(21))) EditorGUIUtility.PingObject(originalClip);
                    EditorGUI.BeginChangeCheck();
                    props.newClip = (AnimationClip)EditorGUILayout.ObjectField(props.newClip, typeof(AnimationClip), false, GUILayout.Height(21), GUILayout.Width(170));
                    if (EditorGUI.EndChangeCheck() && originalClip == props.newClip) props.newClip = null;
                    EditorGUILayout.EndHorizontal();
                    if (props.foldout)
                    {
                        foreach (var path in paths) path.replace = EditorGUILayout.ToggleLeft(path.path, path.replace, FacsGUIStyles.Label);
                    }
                    if (!canRun && props.newClip)
                    {
                        foreach (var path in paths)
                        {
                            if (path.replace) { canRun = true; break; }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            if (canRun && GUILayout.Button("Swap!", FacsGUIStyles.Button, GUILayout.Height(40)))
            {
                RunSwap();
                RunScan();
            }
        }

        private bool RunScan()
        {
            var animatorLayers = animatorController.layers;
            if (animatorLayers == null || animatorLayers.Length == 0)
            {
                NullVars();
                ShowNotification(new GUIContent("AnimatorController\nhas 0 layers"), 3);
                return false;
            }
            var clips = animatorController.animationClips;
            if (clips == null || clips.Length == 0)
            {
                NullVars();
                ShowNotification(new GUIContent("AnimatorController\nhas 0 AnimationClips"), 3);
                return false;
            }

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(animatorController, out var guid, out long _))
            {
                animatorControllerGUID = guid;
            }
            animatorHierarchy = new();
            animatorHierarchy.ScanAnimatorController(animatorController);

            return true;
        }

        private static int SortAnimationClipPaths(AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Animation_Clip c1, AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Animation_Clip c2)
        {
            return string.Compare(c1.path, c2.path);
        }

        private static string GenerateAssetHash(UnityEngine.Object obj)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long fileID))
            {
                return guid + fileID.ToString() + ";";
            }
            return obj.GetInstanceID().ToString() + ";";
        }

        private void AddBlend_TreesRecursive(AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Blend_Tree bt,
            HashSet<AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Blend_Tree> L)
        {
            if (bt.parentBlendTree != null && !L.Contains(bt.parentBlendTree))
            {
                L.Add(bt.parentBlendTree);
                AddBlend_TreesRecursive(bt.parentBlendTree, L);
            }
        }

        private void ReplacersCleanup()
        {
            var blend_trees = new HashSet<AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Blend_Tree>();
            animatorHierarchy.blend_treesInUse = new();
            var toRemove = new List<AnimationClip>();
            foreach (var kvp in animatorHierarchy.clipReplacers)
            {
                if (!kvp.Value.newClip)
                {
                    foreach (var ac in kvp.Value.paths) ac.Remove();
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    for (int i = kvp.Value.paths.Count - 1; i >= 0; i--)
                    {
                        var ac = kvp.Value.paths[i];
                        if (!ac.replace)
                        {
                            ac.Remove();
                            kvp.Value.paths.RemoveAt(i);
                        }
                        else
                        {
                            ac.newClip = kvp.Value.newClip;
                            if (ac.parentBlendTree != null && !blend_trees.Contains(ac.parentBlendTree))
                            {
                                blend_trees.Add(ac.parentBlendTree);
                                AddBlend_TreesRecursive(ac.parentBlendTree, blend_trees);
                            }
                        }
                    }
                    if (kvp.Value.paths.Count == 0) toRemove.Add(kvp.Key);
                }
            }
            foreach (var ac in toRemove)
            {
                animatorHierarchy.clipReplacers.Remove(ac);
            }

            if (blend_trees.Count > 0)
            {
                animatorHierarchy.blend_treeHashes = new();
                foreach (var b_t in blend_trees.OrderByDescending(bt => bt.depth))
                {
                    var hashstart = b_t.hashCode + ">";
                    foreach (var kvp in b_t.slots.OrderBy(o => o.Key))
                    {
                        if (kvp.Value is AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Animation_Clip a_c)
                        {
                            hashstart += kvp.Key.ToString() + ":" + a_c.hashCode + ",";
                        }
                        else if (kvp.Value is AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Blend_Tree b_t2)
                        {
                            hashstart += kvp.Key.ToString() + ":" + b_t2.hashCode + b_t2.btHash + ",";
                        }
                    }
                    b_t.btHash = HashString(hashstart);
                    if (!animatorHierarchy.blend_treeHashes.ContainsKey(b_t.btHash)) animatorHierarchy.blend_treeHashes[b_t.btHash] = null;
                }
            }
        }

        private static string HashString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            using (var sha = new System.Security.Cryptography.SHA256Managed())
            {
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(s);
                byte[] hashBytes = sha.ComputeHash(textBytes);
                return BitConverter.ToString(hashBytes).Replace("-", String.Empty);
            }
        }

        private void GetBlendTreeNames()
        {
            var blendtreeobjs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(animatorController)).Where(o => o is BlendTree);
            if (blendtreeobjs.Any())
            {
                blendTreeNames = blendtreeobjs.Select(o => ((BlendTree)o).name).Distinct().ToArray();
            }
            else blendTreeNames = null;
        }

        private void RunSwap()
        {
            AssetDatabase.SaveAssets();
            ReplacersCleanup();
            GetBlendTreeNames();
            Undo.IncrementCurrentGroup();
            var undoIndex = Undo.GetCurrentGroup();
            using (var animatorControllerSO = new SerializedObject(animatorController))
            {
                var layersSP = animatorControllerSO.FindProperty("m_AnimatorLayers");
                for (int i = 0; i < layersSP.arraySize; i++)
                {
                    var layerSP = layersSP.GetArrayElementAtIndex(i);
                    if (animatorHierarchy.TryGetLayer(layerSP.FindPropertyRelative("m_Name").stringValue, out var al))
                    {
                        using (var stateMachineSO = new SerializedObject(layerSP.FindPropertyRelative("m_StateMachine").objectReferenceValue))
                        {
                            ProcessStateMachine(stateMachineSO, al.stateMachine);
                        }
                        animatorHierarchy.layers.Remove(al);
                        if (animatorHierarchy.layers.Count == 0)
                        {
                            animatorHierarchy.layers = null;
                            break;
                        }
                    }
                }
            }
            Undo.SetCurrentGroupName("Animation Swap");
            Undo.CollapseUndoOperations(undoIndex);
            Undo.SetCurrentGroupName("Animation Swap");
            AssetDatabase.SaveAssets();
        }

        private void ProcessStateMachine(SerializedObject stateMachineSO, AnimatorControllerHierarchy.AnimatorLayer.StateMachine stateMachine)
        {
            if (stateMachine.ChildStates != null)
            {
                var childStatesSP = stateMachineSO.FindProperty("m_ChildStates");
                for (int i = 0; i < childStatesSP.arraySize; i++)
                {
                    var childStateSP = childStatesSP.GetArrayElementAtIndex(i);
                    using (var stateSO = new SerializedObject(childStateSP.FindPropertyRelative("m_State").objectReferenceValue))
                    {
                        if (stateMachine.TryGetState(stateSO.FindProperty("m_Name").stringValue, out var state))
                        {
                            var motion = stateSO.FindProperty("m_Motion");
                            ProcessMotion(motion, state.motion);
                            if (stateSO.hasModifiedProperties) stateSO.ApplyModifiedProperties();
                            stateMachine.ChildStates.Remove(state);
                            if (stateMachine.ChildStates.Count == 0)
                            {
                                stateMachine.ChildStates = null;
                                break;
                            }
                        }
                    }
                }
            }
            if (stateMachine.ChildStateMachines != null)
            {
                var childStateMachinesSP = stateMachineSO.FindProperty("m_ChildStateMachines");
                for (int i = 0; i < childStateMachinesSP.arraySize; i++)
                {
                    var childStateMachineSP = childStateMachinesSP.GetArrayElementAtIndex(i);
                    using (var stateMachine2SO = new SerializedObject(childStateMachineSP.FindPropertyRelative("m_StateMachine").objectReferenceValue))
                    {
                        if (stateMachine.TryGetStateMachine(stateMachine2SO.FindProperty("m_Name").stringValue, out var sm))
                        {
                            ProcessStateMachine(stateMachine2SO, sm);
                            stateMachine.ChildStateMachines.Remove(sm);
                            if (stateMachine.ChildStateMachines.Count == 0)
                            {
                                stateMachine.ChildStateMachines = null;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void ProcessMotion(SerializedProperty motion, AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Motion m)
        {
            if (m is AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Animation_Clip a_c)
            {
                motion.objectReferenceValue = a_c.newClip;
            }
            else if (m is AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Blend_Tree b_t)
            {
                ProcessBlendTree(motion, b_t);
            }
        }

        private void ProcessBlendTree(SerializedProperty motion, AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Blend_Tree b_t)
        {
            if (animatorHierarchy.blend_treeHashes[b_t.btHash] == null)
            {
                var blendTree = (BlendTree)motion.objectReferenceValue;
                SerializedObject blendTreeSO;
                if (!animatorHierarchy.blend_treesInUse.Contains(blendTree) &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(blendTree, out var guid2, out long _) && guid2 == animatorControllerGUID)
                {
                    blendTreeSO = new(blendTree);
                    animatorHierarchy.blend_treeHashes[b_t.btHash] = blendTree;
                    animatorHierarchy.blend_treesInUse.Add(blendTree);
                }
                else
                {
                    var newblendTree = new BlendTree();
                    Undo.RegisterCreatedObjectUndo(newblendTree, "Animation Swap");
                    EditorUtility.CopySerialized(blendTree, newblendTree);
                    if (blendTreeNames != null)
                    {
                        newblendTree.name = ObjectNames.GetUniqueName(blendTreeNames, blendTree.name);
                        var tempL = blendTreeNames.ToList(); tempL.Add(newblendTree.name);
                        blendTreeNames = tempL.ToArray();
                    }
                    else
                    {
                        blendTreeNames = new string[] { blendTree.name };
                    }
                    AssetDatabase.AddObjectToAsset(newblendTree, animatorController);
                    motion.objectReferenceValue = newblendTree;
                    blendTreeSO = new(newblendTree);

                    animatorHierarchy.blend_treeHashes[b_t.btHash] = newblendTree;
                }

                var blendTreeChildsSP = blendTreeSO.FindProperty("m_Childs"); var indexLimit = blendTreeChildsSP.arraySize;
                foreach (var kvp in b_t.slots)
                {
                    if (kvp.Key >= indexLimit) continue;
                    var blendTreeChildSP = blendTreeChildsSP.GetArrayElementAtIndex(kvp.Key);
                    ProcessMotion(blendTreeChildSP.FindPropertyRelative("m_Motion"), kvp.Value);
                }

                if (blendTreeSO.hasModifiedProperties) blendTreeSO.ApplyModifiedProperties();
                blendTreeSO.Dispose();
            }
            else motion.objectReferenceValue = animatorHierarchy.blend_treeHashes[b_t.btHash];
        }

        private void OnDestroy()
        {
            animatorController = null;
            FacsGUIStyles = null;
            NullVars();
        }

        private void NullVars()
        {
            animatorHierarchy = null;
            animatorControllerGUID = null;
            blendTreeNames = null;
            scrollView = new();
        }

        private class AnimationClipProps
        {
            public AnimationClip newClip = null;
            public bool foldout = false;
            public List<AnimatorControllerHierarchy.AnimatorLayer.StateMachine.State.Animation_Clip> paths = new();
        }

        private class AnimatorControllerHierarchy
        {
            internal class AnimationClipReplacer
            {
                public AnimationClip newClip = null;
                public bool foldout = false;
                public GUIContent foldoutText = null;
                public float foldoutWidth;
                public List<AnimatorLayer.StateMachine.State.Animation_Clip> paths = new();

                public AnimationClipReplacer(AnimationClip oldClip)
                {
                    foldoutText = new GUIContent($"<b>{oldClip.name}</b>");
                    var w = FacsGUIStyles.Foldout.CalcSize(foldoutText).x + 10;
                    foldoutWidth = w>200 ? w : 200;
                }
            }
            
            public List<AnimatorLayer> layers;
            public Dictionary<AnimationClip, AnimationClipReplacer> clipReplacers;
            public Dictionary<string, BlendTree> blend_treeHashes;
            public HashSet<BlendTree> blend_treesInUse;

            public void ScanAnimatorController(AnimatorController ac)
            {
                clipReplacers = ac.animationClips.Distinct().OrderBy(c => c.name).ToDictionary(_ => _, _ => new AnimationClipReplacer(_));
                layers = new();
                foreach (var layer in ac.layers)
                {
                    var l = new AnimatorLayer(layer);
                    if (l.stateMachine != null)
                    {
                        layers.Add(l);
                    }
                }
                foreach (var props in clipReplacers.Values) props.paths.Sort(SortAnimationClipPaths);
            }

            public bool TryGetLayer(string name, out AnimatorLayer al)
            {
                foreach (var layer in layers)
                {
                    if (layer.name == name)
                    {
                        al = layer;
                        return true;
                    }
                }
                al = null;
                return false;
            }

            internal class AnimatorLayer
            {
                public string name = null;
                public StateMachine stateMachine = null;

                public AnimatorLayer(AnimatorControllerLayer acl)
                {
                    name = acl.name;
                    var sm = new StateMachine(this, acl.stateMachine, $"{name}<b><color=black>></color></b>");
                    if (sm.ChildStates != null || sm.ChildStateMachines != null)
                    {
                        stateMachine = sm;
                    }
                }

                internal class StateMachine
                {
                    public AnimatorLayer parentLayer = null;
                    public StateMachine parentStateMachine = null;

                    public string name = null;
                    public List<State> ChildStates = null;
                    public List<StateMachine> ChildStateMachines = null;

                    public StateMachine(AnimatorLayer al, AnimatorStateMachine asm, string path)
                    {
                        parentLayer = al;
                        this.Populate(asm, path);
                    }
                    public StateMachine(StateMachine sm, AnimatorStateMachine asm, string path)
                    {
                        parentStateMachine = sm;
                        this.Populate(asm, path);
                    }
                    public void Populate(AnimatorStateMachine asm, string path)
                    {
                        name = asm.name;
                        var path2 = parentLayer != null ? path : $"{path}{name}<b><color=black>:</color></b> ";
                        var states = asm.states;
                        if (states != null && states.Length > 0)
                        {
                            ChildStates = new();
                            foreach (var childState in states)
                            {
                                var st = new State(this, childState.state, path2);
                                if (st.motion != null)
                                {
                                    ChildStates.Add(st);
                                }
                            }
                            if (ChildStates.Count == 0) ChildStates = null;
                        }
                        var stateMachines = asm.stateMachines;
                        if (stateMachines != null && stateMachines.Length > 0)
                        {
                            ChildStateMachines = new();
                            foreach (var childStateMachine in stateMachines)
                            {
                                var csm = new StateMachine(this, childStateMachine.stateMachine, path2);
                                if (csm.ChildStates != null || csm.ChildStateMachines != null)
                                {
                                    ChildStateMachines.Add(csm);
                                }
                            }
                            if (ChildStateMachines.Count == 0) ChildStateMachines = null;
                        }
                    }

                    public void Remove(State s)
                    {
                        ChildStates.Remove(s);
                        if (ChildStates.Count == 0) ChildStates = null;
                        if (ChildStates == null && ChildStateMachines == null)
                        {
                            Remove();
                        }
                    }
                    public void Remove(StateMachine sm)
                    {
                        ChildStateMachines.Remove(sm);
                        if (ChildStateMachines.Count == 0) ChildStateMachines = null;
                        if (ChildStateMachines == null && ChildStates == null)
                        {
                            Remove();
                        }
                    }
                    public void Remove()
                    {
                        if (parentStateMachine != null)
                        {
                            parentStateMachine.Remove(this);
                        }
                        if (parentLayer != null)
                        {
                            animatorHierarchy.layers.Remove(parentLayer);
                        }
                    }
                    public bool TryGetState(string name, out State s)
                    {
                        s = null;
                        if (ChildStates == null) return false;
                        foreach (var state in ChildStates)
                        {
                            if (state.name == name)
                            {
                                s = state;
                                return true;
                            }
                        }
                        return false;
                    }
                    public bool TryGetStateMachine(string name, out StateMachine sm)
                    {
                        sm = null;
                        if (ChildStateMachines == null) return false;
                        foreach (var sm2 in ChildStateMachines)
                        {
                            if (sm2.name == name)
                            {
                                sm = sm2;
                                return true;
                            }
                        }
                        return false;
                    }

                    internal class State
                    {
                        public StateMachine parentStateMachine = null;

                        public string name = null;
                        public Motion motion = null;

                        public State(StateMachine sm, AnimatorState astate, string path)
                        {
                            parentStateMachine = sm;
                            name = astate.name;
                            var path2 = path + name;
                            if (!astate.motion) return;
                            if (astate.motion is AnimationClip ac)
                            {
                                motion = new Animation_Clip(this, ac, path2);
                            }
                            else if (astate.motion is BlendTree bt)
                            {
                                var bt2 = new Blend_Tree(this, bt, $"{path2}<b><color=black>/</color></b>");
                                if (bt2.slots != null)
                                {
                                    motion = bt2;
                                }
                            }
                        }

                        internal class Motion
                        {
                            public State parentState = null;
                            public Blend_Tree parentBlendTree = null;
                            public int parentBlendTreePos = -1;

                            public string name = null;
                            public string hashCode = null;

                            public Motion(State s, UnityEngine.Motion m)
                            {
                                parentState = s;
                                name = m.name;
                                hashCode = GenerateAssetHash(m);
                            }
                            public Motion(Blend_Tree bt, UnityEngine.Motion m)
                            {
                                parentBlendTree = bt;
                                name = m.name;
                                hashCode = GenerateAssetHash(m);
                            }
                        }
                        internal class Animation_Clip : Motion
                        {
                            public bool replace = false;
                            public string path = null;
                            public AnimationClip newClip = null;

                            public Animation_Clip(State s, AnimationClip ac, string path2) : base(s, ac)
                            {
                                path = path2;
                                animatorHierarchy.clipReplacers[ac].paths.Add(this);
                            }
                            public Animation_Clip(Blend_Tree bt, AnimationClip ac, string path2) : base(bt, ac)
                            {
                                path = path2;
                                animatorHierarchy.clipReplacers[ac].paths.Add(this);
                            }

                            public void Remove()
                            {
                                if (parentState != null)
                                {
                                    parentState.parentStateMachine.Remove(parentState);
                                }
                                if (parentBlendTree != null)
                                {
                                    parentBlendTree.Remove(parentBlendTreePos);
                                }
                            }
                        }
                        internal class Blend_Tree : Motion
                        {
                            public Dictionary<int, Motion> slots = null;
                            public int depth = 0;
                            public string btHash = "";
                            public BlendTree localBlendTree = null;

                            public Blend_Tree(State s, BlendTree bt, string path) : base(s, bt)
                            {
                                this.Populate(bt, path);
                            }
                            public Blend_Tree(Blend_Tree bt2, BlendTree bt, string path) : base(bt2, bt)
                            {
                                depth = bt2.depth + 1;
                                this.Populate(bt, path);
                            }
                            private void Populate(BlendTree bt, string path)
                            {
                                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bt, out string guid, out long _) &&
                                    guid == animatorControllerGUID)
                                {
                                    localBlendTree = bt;
                                }
                                if (bt.children != null && bt.children.Any(c => c.motion))
                                {
                                    slots = new();
                                    for (int i = 0; i < bt.children.Length; i++)
                                    {
                                        var mot = bt.children[i].motion;
                                        if (mot)
                                        {
                                            if (mot is AnimationClip ac)
                                            {
                                                var ac2 = new Animation_Clip(this, ac, path + $"Slot #{i + 1}");
                                                ac2.parentBlendTreePos = i;
                                                slots[i] = ac2;
                                            }
                                            else if (mot is BlendTree bt2)
                                            {
                                                var bt3 = new Blend_Tree(this, bt2, path + $"Slot #{i + 1}<b><color=black>/</color></b>");
                                                if (bt3.slots != null)
                                                {
                                                    bt3.parentBlendTreePos = i;
                                                    slots[i] = bt3;
                                                }
                                            }
                                        }
                                    }
                                    if (slots.Count == 0) slots = null;
                                }
                            }
                            public void Remove(int ind)
                            {
                                slots.Remove(ind);
                                if (slots.Count == 0)
                                {
                                    slots = null;
                                    if (localBlendTree != null && !animatorHierarchy.blend_treesInUse.Contains(localBlendTree))
                                    {
                                        animatorHierarchy.blend_treesInUse.Add(localBlendTree);
                                    }
                                    if (parentState != null)
                                    {
                                        parentState.parentStateMachine.Remove(parentState);
                                    }
                                    if (parentBlendTree != null)
                                    {
                                        parentBlendTree.Remove(parentBlendTreePos);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
#endif