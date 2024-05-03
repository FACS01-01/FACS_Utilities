#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FACS01.Utilities
{
    internal class CopyComponents : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Copy Components]" + Logger.EndTag;

        private static readonly Type TransformType = typeof(Transform);
        private static readonly Type RectTransformType = typeof(RectTransform);
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject copyFrom;
        private static GameObject copyFromPrefab;
        private static GameObject copyTo;
        private static GameObject copyToPrefab;
        private static ComponentHierarchy componentHierarchy;
        private static bool toggleSelection = true;
        private static bool toggleHideSelection = true;
        private static Dictionary<Transform, Transform> GO_Dictionary; //from,to
        private static Dictionary<Component, Component> C_Dictionary; //from,to
        private static int copyMissing = 0;
        private static bool selDepsRecursive = false;
        private static bool recordUndo = false;
        private static Dictionary<UnityEngine.Object, HashSet<UnityEngine.Object>> EXTssets = null;
        private static Dictionary<UnityEngine.Object, HashSet<UnityEngine.Object>> MIAssets = null;
        private static Dictionary<UnityEngine.Object, HashSet<UnityEngine.Object>> NANssets = null;

        [MenuItem("FACS Utils/Repair Project/Copy Components", false, 1053)]
        [MenuItem("FACS Utils/Copy/Copy Components", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(CopyComponents), false, "Copy Components", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter; }
            
            EditorGUILayout.LabelField($"<color=cyan><b>Copy Components</b></color>\n\n" +
                $"Scans the selected GameObject to copy Components from, lists all available Components in it, " +
                $"and lets you choose which ones to paste into the next GameObject.\n", FacsGUIStyles.Helpbox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"<b>Copy From</b>", FacsGUIStyles.Helpbox);
            EditorGUI.BeginChangeCheck();
            copyFromPrefab = (GameObject)EditorGUILayout.ObjectField(copyFromPrefab, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                if (EditorUtility.IsPersistent(copyFromPrefab) && AssetDatabase.GetAssetPath(copyFromPrefab) is string prefabPath &&
                    !string.IsNullOrEmpty(prefabPath) && System.IO.Path.GetExtension(prefabPath) != ".prefab")
                {
                    Logger.LogWarning(RichToolName + " Copying from Project assets only allowed with " + Logger.TypeTag + "Prefabs" + Logger.EndTag + ".");
                    copyFromPrefab = null;
                }
                NullCompHierarchy();
            }
            else if (!copyFromPrefab && componentHierarchy != null) NullCompHierarchy();
            EditorGUILayout.EndVertical();
            if (componentHierarchy != null)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"<b>Copy To</b>", FacsGUIStyles.Helpbox);
                EditorGUI.BeginChangeCheck();
                copyToPrefab = (GameObject)EditorGUILayout.ObjectField(copyToPrefab, typeof(GameObject), true, GUILayout.Height(40));
                if (EditorGUI.EndChangeCheck())
                {
                    if (copyToPrefab && PrefabUtility.IsPartOfPrefabAsset(copyToPrefab) && PrefabUtility.GetPrefabAssetType(copyToPrefab) == PrefabAssetType.Model)
                    {
                        Logger.LogWarning($"{RichToolName} Can't edit {Logger.RichModelPrefab}: {Logger.AssetTag}{copyToPrefab.name}{Logger.EndTag}", copyToPrefab);
                        copyToPrefab = null;
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            if (copyFromPrefab != null && GUILayout.Button("Scan!", FacsGUIStyles.Button, GUILayout.Height(40)))
            {
                toggleSelection = toggleHideSelection = true;
                if (!TrullyPersistent(copyFromPrefab)) copyFrom = copyFromPrefab;
                else copyFrom = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(copyFromPrefab));
                GetAvailableComponentTypes();
            }

            if (componentHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>Available Components to Copy</b></color>:", FacsGUIStyles.Helpbox);

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

                if (copyMissing > 0) { EditorGUILayout.LabelField($"Skipping Missing Scripts: {copyMissing}", FacsGUIStyles.HelpboxSmall); }

                if (anyOn)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select Component Dependencies", FacsGUIStyles.Button, GUILayout.Height(22), GUILayout.ExpandWidth(true)))
                    {
                        componentHierarchy.SelectDependencies("Copy Components", selDepsRecursive);
                    }
                    GUITools.ColoredToggle(ref selDepsRecursive, "Recursive Selection", GUILayout.Height(22));
                    EditorGUILayout.EndHorizontal();
                    if (copyToPrefab != null && GUILayout.Button("Copy!", FacsGUIStyles.Button, GUILayout.Height(40)))
                    {
                        if (copyFromPrefab == copyToPrefab)
                        {
                            Logger.LogWarning($"{RichToolName} You shouldn't use the same {Logger.RichGameObject} for {Logger.ConceptTag}CopyFrom{Logger.EndTag} and {Logger.ConceptTag}CopyTo{Logger.EndTag}.");
                        }
                        else RunCopy();
                    }
                }
            }
        }

        private static bool TrullyPersistent(GameObject go)
        {
            if (!EditorUtility.IsPersistent(go)) return false;
            return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go));
        }

        private void RunCopy()
        {
            if (copyTo) DestroyImmediate(copyTo);
            recordUndo = !TrullyPersistent(copyToPrefab);
            if (recordUndo) copyTo = copyToPrefab;
            else
            {
                copyTo = PrefabUtility.InstantiatePrefab(copyToPrefab) as GameObject;
                Undo.RegisterCreatedObjectUndo(copyTo, "Copy Components");
            }
            
            GODictionary(copyFrom.transform, copyTo.transform);
            CollapseAll();
            componentHierarchy.UseUnfoldedAsMarker();
            CreateMissingGOsAndSetEnables();
            CopyTransforms();
            var Components_to_copy = ComponentDictionary_CreateMissingC();
            MIAssets = new(); NANssets = new(); EXTssets = new();
            CopyComponentsSerializedData(Components_to_copy);
            LogSceneExternalComponents();
            LogSkippedAssets();
            var warnings = MIAssets.Count + NANssets.Count + EXTssets.Count;
            MIAssets.Clear(); NANssets.Clear(); EXTssets.Clear();
            MIAssets = NANssets = EXTssets = null;
            CollapseAll();

            if (!recordUndo)
            {
                Undo.SetCurrentGroupName("Copy Components");
                PrefabUtility.ApplyPrefabInstance(copyTo, InteractionMode.UserAction);
                DestroyImmediate(copyTo);
            }
            copyTo = null;
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Logger.Log($"{RichToolName} Finished copying components!{(warnings>0?$" (with {warnings} warnings)":"")}");
        }

        private void CopyComponentsSerializedData(List<Component> comps_to_copy)
        {
            var undoable = false;
            int TotalComponents = comps_to_copy.Count;
            float componentCountStep = 1.0f / TotalComponents;
            float componentCount = 0; float displayProgressBar = 0;
            
            foreach (var comp_from in comps_to_copy)
            {
                var comp_to = C_Dictionary[comp_from];
                
                if (displayProgressBar >= 0.01f)
                {
                    displayProgressBar %= 0.01f;
                    EditorUtility.DisplayProgressBar("FACS Utilities - Copy Components", "Please wait...", componentCount);
                }
                
                var SO_from = new SerializedObject(comp_from);
                var SO_to = new SerializedObject(comp_to);
                SerializedProperty SO_from_iterator = SO_from.GetIterator();
                SO_from_iterator.Next(true);

                var NoCopyProps = new List<string> { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Script" };
                do
                {
                    if (NoCopyProps.Contains(SO_from_iterator.name)) NoCopyProps.Remove(SO_from_iterator.name);
                    else CopySerialized(SO_from_iterator, SO_to);
                }
                while (SO_from_iterator.Next(false));

                if (SO_to.hasModifiedProperties)
                {
                    undoable = true;
                    if (recordUndo) SO_to.ApplyModifiedProperties();
                    else SO_to.ApplyModifiedPropertiesWithoutUndo();
                }
                SO_from_iterator.Dispose(); SO_from.Dispose(); SO_to.Dispose();
                componentCount += componentCountStep; displayProgressBar += componentCountStep;
            }
            EditorUtility.ClearProgressBar();

            if (recordUndo && undoable) Undo.SetCurrentGroupName("Copy Components");
        }

        private void CopySerializedAsset(SerializedProperty from, SerializedProperty to)
        {
            var objref = from.objectReferenceValue;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(objref, out string guid, out long localId))
            {
                if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    to.objectReferenceValue = objref;
                }
                else if (!uint.TryParse(guid, out var ui) || ui != 0)
                {
                    if (!MIAssets.TryGetValue(objref, out var mia)) { mia = new(); MIAssets[objref] = mia; }
                    mia.Add(from.serializedObject.targetObject);
                }
                else
                {
                    if (!NANssets.TryGetValue(objref, out var nan)) { nan = new(); NANssets[objref] = nan; }
                    nan.Add(from.serializedObject.targetObject);
                }
            }
        }

        private static void SaveSceneExternalComponent(UnityEngine.Object obj, SerializedObject to)
        {
            if (!EXTssets.TryGetValue(obj, out var ext)) { ext = new(); EXTssets[obj] = ext; }
            ext.Add(to.targetObject);
        }

        private static void LogSceneExternalComponents()
        {
            foreach (var ext in EXTssets.OrderByDescending(ext => ext.Value.Count).ThenBy(ext => ext.Key.GetType().Name))
            {
                var extK = ext.Key; var extV = ext.Value;
                Logger.LogWarning($"{RichToolName} Copying {Logger.TypeTag}{extK.GetType().Name}{Logger.EndTag} {Logger.AssetTag}{extK.name}{Logger.EndTag} outside of {Logger.ConceptTag}CopyFrom{Logger.EndTag} hierarchy. Its used on <b>{extV.Count}</b> component{(extV.Count > 1 ? "s" : "")}:\n  - " +
                    string.Join("  - ", extV.Select(o => $"{Logger.TypeTag}{o.GetType().Name}{Logger.EndTag} {Logger.AssetTag}{o.name}{Logger.EndTag}\n")), extK);
                extV.Clear();
            }
        }

        private static void LogSkippedAssets()
        {
            foreach (var mia in MIAssets.OrderByDescending(mia => mia.Value.Count).ThenBy(mia => mia.Key.GetType().Name))
            {
                var miaK = mia.Key; var miaV = mia.Value;
                Logger.LogWarning($"{RichToolName} {Logger.TypeTag}{miaK.GetType().Name}{Logger.EndTag} {Logger.AssetTag}{miaK.name}{Logger.EndTag} in {Logger.ConceptTag}CopyFrom{Logger.EndTag} not found in Project files. Its used on <b>{miaV.Count}</b> component{(miaV.Count>1?"s":"")}:\n  - " +
                    string.Join("  - ", miaV.Select(o => $"{Logger.TypeTag}{o.GetType().Name}{Logger.EndTag} {Logger.AssetTag}{o.name}{Logger.EndTag}\n")));
                miaV.Clear();
            }
            foreach (var nan in NANssets.OrderByDescending(nan => nan.Value.Count).ThenBy(nan => nan.Key.GetType().Name))
            {
                var nanK = nan.Key; var nanV = nan.Value;
                Logger.LogWarning($"{RichToolName} {Logger.TypeTag}{nanK.GetType().Name}{Logger.EndTag} {Logger.AssetTag}{nanK.name}{Logger.EndTag} in {Logger.ConceptTag}CopyFrom{Logger.EndTag} is not a permanent asset. Its used on <b>{nanV.Count}</b> component{(nanV.Count > 1 ? "s" : "")}:\n  - " +
                    string.Join("  - ", nanV.Select(o => $"{Logger.TypeTag}{o.GetType().Name}{Logger.EndTag} {Logger.AssetTag}{o.name}{Logger.EndTag}\n")));
                nanV.Clear();
            }
        }

        private void CopySerialized(SerializedProperty from_iterator, SerializedObject to)
        {
            switch (from_iterator.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    {
                        var objref = from_iterator.objectReferenceValue;
                        var to_prop = to.FindProperty(from_iterator.propertyPath);
                        if (objref)
                        {
                            if (objref == to_prop.objectReferenceValue) break;
                            var T = objref.GetType();
                            if (T.IsSubclassOf(typeof(Component)))
                            {
                                if (TransformType.IsAssignableFrom(T))
                                {
                                    var t_from = (Transform)objref;
                                    if (GO_Dictionary.TryGetValue(t_from, out Transform t_to)) objref = t_to;
                                    else if (PrefabUtility.IsPartOfPrefabAsset(objref))
                                    { CopySerializedAsset(from_iterator, to_prop); break; }
                                    else if (t_from.gameObject.scene.name == null || t_from.gameObject.scene.name != copyToPrefab.scene.name)
                                    { objref = null; }
                                    else { SaveSceneExternalComponent(objref, to); }
                                }
                                else
                                {
                                    var c_from = (Component)objref;
                                    if (C_Dictionary.TryGetValue(c_from, out Component c_to)) objref = c_to;
                                    else if (PrefabUtility.IsPartOfPrefabAsset(objref))
                                    { CopySerializedAsset(from_iterator, to_prop); break; }
                                    else if (c_from.gameObject.scene.name == null || c_from.gameObject.scene.name != copyToPrefab.scene.name)
                                    { objref = null; }
                                    else { SaveSceneExternalComponent(objref, to); }
                                }
                            }
                            else if (T == typeof(GameObject))
                            {
                                var go = (GameObject)objref;
                                var t_from = go.transform;
                                if (GO_Dictionary.TryGetValue(t_from, out Transform t_to)) objref = t_to.gameObject;
                                else if (PrefabUtility.IsPartOfPrefabAsset(objref))
                                { CopySerializedAsset(from_iterator, to_prop); break; }
                                else if (go.scene.name == null || go.scene.name != copyToPrefab.scene.name)
                                { objref = null; }
                                else { SaveSceneExternalComponent(objref, to); }
                            }
                            else
                            {
                                CopySerializedAsset(from_iterator, to_prop); break;
                            }
                            to_prop.objectReferenceValue = objref;
                        }
                        else if (!object.Equals(to_prop.objectReferenceValue, null))
                        {
                            to_prop.objectReferenceValue = null;
                        }
                        break;
                    }

                case SerializedPropertyType.ManagedReference:
                case SerializedPropertyType.Generic:
                    {
                        from_iterator.Next(true);
                        CopySerialized(from_iterator, to);
                        break;
                    }

                case SerializedPropertyType.ExposedReference://do nothing
                    break;
                default://all the rest just copy
                    {
                        var to_prop = to.FindProperty(from_iterator.propertyPath);
                        if (!SerializedProperty.DataEquals(from_iterator, to_prop))
                        {
                            to.CopyFromSerializedProperty(from_iterator);
                        }
                        break;
                    }
            }
        }

        private List<Component> ComponentDictionary_CreateMissingC()
        {
            C_Dictionary = new();
            var L = new List<Component>();
            foreach (var T in componentHierarchy.toggles_by_type.Keys)
            {
                if (T == TransformType || T == RectTransformType) continue;
                var CtoCopy = componentHierarchy.GetAllToggles(T, 1);
                foreach (var pair in GO_Dictionary)
                {
                    var t_from = pair.Key; var t_to = pair.Value;
                    var Cs_from = t_from.GetComponents(T); if (Cs_from.Length == 0) continue;
                    var Cs_to = t_to != null ? t_to.GetComponents(T) : new Component[0];
                    for (int i = 0; i < Cs_from.Length; i++)
                    {
                        var Cs_from_i = Cs_from[i];
                        var copy = CtoCopy != null && CtoCopy.Contains(Cs_from_i);
                        if (i < Cs_to.Length)
                        {
                            C_Dictionary.Add(Cs_from_i, Cs_to[i]);
                        }
                        else if (copy)
                        {
                            var newC = recordUndo ? Undo.AddComponent(t_to.gameObject, T) : t_to.gameObject.AddComponent(T);
                            C_Dictionary.Add(Cs_from_i, newC);
                        }
                        else C_Dictionary.Add(Cs_from_i, null);
                        if (copy) L.Add(Cs_from_i);
                    }
                }
            }

            return L;
        }

        private void CopyTransforms()
        {
            bool copiedOrigin = false;
            var TTL = componentHierarchy.toggles_by_type[TransformType].toggleList;
            foreach (var toggle in TTL)
            {
                if (!copiedOrigin && toggle.component.transform == copyFrom.transform)
                {
                    copiedOrigin = true;
                    if (toggle.state)
                    {
                        EnforceTransform(toggle, out var t_from, out var t_to);
                        t_to.localRotation = t_from.localRotation;
                        t_to.localEulerAngles = t_from.localEulerAngles;
                        t_to.localScale = t_from.localScale;
                    }
                }
                else if (toggle.state)
                {
                    EnforceTransform(toggle, out var t_from, out var t_to);
                    CopyT(t_from, t_to);
                }
            }
            if (componentHierarchy.toggles_by_type.TryGetValue(RectTransformType, out var RTTL))
            {
                foreach (var toggle in RTTL.toggleList)
                {
                    if (!copiedOrigin && toggle.component.transform == copyFrom.transform)
                    {
                        copiedOrigin = true;
                        if (toggle.state)
                        {
                            EnforceRectTransform(toggle, out var t_from, out var t_to);
                            t_to.localRotation = t_from.localRotation;
                            t_to.localEulerAngles = t_from.localEulerAngles;
                            t_to.localScale = t_from.localScale;
                            CopyRT(t_from, t_to);
                        }
                    }
                    else if (toggle.state)
                    {
                        EnforceRectTransform(toggle, out var t_from, out var t_to);
                        CopyT(t_from, t_to);
                        CopyRT(t_from, t_to);
                    }
                }
            }
        }

        private static void EnforceTransform(ComponentHierarchy.ComponentToggle toggle, out Transform t_from, out Transform t_to)
        {
            t_from = toggle.component.transform;
            t_to = GO_Dictionary[t_from];
            if (t_to.GetType() == typeof(RectTransform))
            {
                var go = t_to.gameObject;
                if (recordUndo) Undo.DestroyObjectImmediate(t_to);
                else DestroyImmediate(t_to);
                t_to = go.transform;
                GO_Dictionary[t_from] = t_to;
            }
            else if (recordUndo) Undo.RecordObject(t_to, "Copy Components");
        }

        private static void EnforceRectTransform(ComponentHierarchy.ComponentToggle toggle, out RectTransform t_from, out RectTransform t_to)
        {
            t_from = (RectTransform)toggle.component;
            var t_t0 = GO_Dictionary[t_from];
            if (t_t0 is RectTransform t_toRT)
            {
                t_to = t_toRT;
                if (recordUndo) Undo.RecordObject(t_to, "Copy Components");
            }
            else
            {
                var go = t_t0.gameObject;
                t_to = recordUndo ? Undo.AddComponent<RectTransform>(go) : go.AddComponent<RectTransform>();
                GO_Dictionary[t_from] = t_to;
            }
        }

        private static void CopyT(Transform from, Transform to)
        {
            to.localPosition = from.localPosition;
            to.localRotation = from.localRotation;
            to.localEulerAngles = from.localEulerAngles;
            to.localScale = from.localScale;
            TransformUtils.SetConstrainProportions(to, TransformUtils.GetConstrainProportions(from));
        }

        private void CopyRT(RectTransform from, RectTransform to)
        {
            to.anchoredPosition = from.anchoredPosition;
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.sizeDelta = from.sizeDelta;
            to.pivot = from.pivot;
        }

        private void CreateMissingGOsAndSetEnables()
        {
            foreach (var fh in componentHierarchy.GO_enables)
            {
                if (fh.unfolded)
                {
                    var from_t = fh.t;
                    var go_from = from_t.gameObject;
                    if (GO_Dictionary[from_t] == null)
                    {
                        var go_to = new GameObject(go_from.name);
                        if (recordUndo) Undo.RegisterCreatedObjectUndo(go_to, "Copy Components");
                        var new_t = go_to.transform;
                        new_t.parent = GO_Dictionary[from_t.parent];
                        CopyT(from_t, new_t);

                        go_to.tag = go_from.tag;
                        go_to.layer = go_from.layer;
                        go_to.SetActive(go_from.activeSelf);

                        GO_Dictionary[from_t] = new_t;
                    }
                    else if (fh.GO_enable)
                    {
                        var go_to = GO_Dictionary[from_t].gameObject;
                        if (recordUndo) Undo.RecordObject(go_to, "Copy Components");
                        go_to.SetActive(go_from.activeSelf);
                    }
                }
            }
        }

        private void GODictionary(Transform from, Transform to)
        {
            var TDict = new Dictionary<Transform, Transform>() { {from,to} };
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

        private void GetAvailableComponentTypes()
        {
            copyMissing = 0;
            componentHierarchy = null;

            componentHierarchy = new(copyFrom.transform);

            Transform[] gos_t = copyFrom.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in gos_t)
            {
                Component[] components = t.GetComponents(typeof(Component));
                foreach (Component component in components)
                {
                    if (component != null) componentHierarchy.AddComponentToggle(component);
                    else copyMissing++;
                }
            }

            componentHierarchy.SortElements(false, true);
            componentHierarchy.SortTypes();
            componentHierarchy.GenerateGOEnables();
        }

        private void NullCompHierarchy()
        {
            UnloadTempPrefab(copyFrom);
            copyFrom = null;
            if (copyTo) DestroyImmediate(copyTo);
            copyTo = null;
            componentHierarchy = null;
            toggleSelection = true;
            toggleHideSelection = true;
            GO_Dictionary = null;
            C_Dictionary = null;
            copyMissing = 0;
        }

        private void UnloadTempPrefab(GameObject go)
        {
            if (go)
            {
                var inScene = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (go.scene == SceneManager.GetSceneAt(i)) { inScene = true; break; }
                }
                if (!inScene && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go))) PrefabUtility.UnloadPrefabContents(go);
            }
        }

        private void OnDestroy()
        {
            NullVars();
        }

        private void NullVars()
        {
            FacsGUIStyles = null;
            copyFromPrefab = null;
            copyToPrefab = null;
            NullCompHierarchy();
        }
    }
}
#endif