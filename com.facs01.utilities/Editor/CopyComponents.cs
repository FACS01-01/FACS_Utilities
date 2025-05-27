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
        private static bool advancedFoldout = false;
        private static Dictionary<Transform, Transform> GO_Dictionary; //from,to
        private static Dictionary<Component, Component> C_Dictionary; //from,to
        private static int copyMissing = 0;
        private static bool selDepsRecursive = false;
        private static bool recordUndo = false;
        private static bool skipExistingComponents = false;
        private static bool onlyExistingComponents = false;
        private static bool onlyExistingPaths = false;
        private static ulong anythingCopiedN = 0;
        private static Dictionary<UnityEngine.Object, HashSet<UnityEngine.Object>> EXTssets = null;
        private static Dictionary<UnityEngine.Object, HashSet<UnityEngine.Object>> MIAssets = null;
        private static Dictionary<UnityEngine.Object, HashSet<UnityEngine.Object>> NANssets = null;
        private static Dictionary<object, object> ManagedReferenceCopies = null;
        private static bool fromFLB = false;

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
                    GUITools.ColoredToggle(ref selDepsRecursive, "Recursive", GUILayout.Height(22));
                    EditorGUILayout.EndHorizontal();

                    if (copyToPrefab)
                    {
                        EditorGUILayout.Space(1, false);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space(14, false);

                        var advRect = EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                        var newR = new Rect(advRect.position - Vector2.right * 4, advRect.size + Vector2.right * 8);
                        EditorGUI.DrawRect(newR, GUITools.GetTintedBGColor(0.05f));
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(4);
                        advancedFoldout = GUILayout.Toggle(advancedFoldout, "<b>Advanced Options</b>", FacsGUIStyles.Foldout, GUILayout.ExpandWidth(true));
                        EditorGUILayout.EndHorizontal();
                        if (advancedFoldout)
                        {
                            EditorGUILayout.BeginHorizontal();

                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.LabelField($"Matching Components:", FacsGUIStyles.Helpbox);
                            EditorGUILayout.LabelField($"Matching Paths:", FacsGUIStyles.Helpbox);
                            EditorGUILayout.EndVertical();

                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.BeginHorizontal();
                            if (GUITools.ColoredToggle(ref skipExistingComponents, "Skip", GUILayout.Height(22), GUILayout.ExpandHeight(true)))
                            {
                                if (skipExistingComponents) onlyExistingComponents = false;
                            }
                            if (GUITools.ColoredToggle(ref onlyExistingComponents, "Only", GUILayout.Height(22), GUILayout.ExpandHeight(true)))
                            {
                                if (onlyExistingComponents) { skipExistingComponents = false; onlyExistingPaths = true; }
                            }
                            EditorGUILayout.EndHorizontal();
                            if (GUITools.ColoredToggle(ref onlyExistingPaths, "Only", GUILayout.Height(22)))
                            {
                                if (!onlyExistingPaths) onlyExistingComponents = false;
                            }
                            EditorGUILayout.EndVertical();

                            EditorGUILayout.EndHorizontal();

                            if (GUILayout.Button("Filter Selection", FacsGUIStyles.Button, GUILayout.Height(20), GUILayout.ExpandWidth(true)))
                            {
                                FilterSelection();
                            }
                            GUILayout.Space(4);
                        }
                        else GUILayout.Space(1);
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.Space(10, false);
                        EditorGUILayout.EndHorizontal();

                        if (!advancedFoldout) EditorGUILayout.Space(2, false);
                        if (GUILayout.Button("Copy!", FacsGUIStyles.Button, GUILayout.Height(40)))
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
        }

        private static bool TrullyPersistent(GameObject go)
        {
            if (!EditorUtility.IsPersistent(go)) return false;
            return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go));
        }

        private void FilterSelection()
        {
            GO_Dictionary = GODictionary(copyFrom.transform, copyToPrefab.transform);
            CollapseAll();
            componentHierarchy.UseUnfoldedAsMarker();
            var toggledCount = FilterSelectionGOs();
            toggledCount += FilterSelectionTransforms();
            toggledCount += FilterSelectionComponents();
            GO_Dictionary = null;
            CollapseAll();
            if (toggledCount == 1) Logger.Log($"{RichToolName} <b>1</b> component was deselected.");
            else Logger.Log($"{RichToolName} <b>{toggledCount}</b> components were deselected.");
        }

        private void RunCopy()
        {
            anythingCopiedN = 0;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            if (copyTo) DestroyImmediate(copyTo);
            recordUndo = !TrullyPersistent(copyToPrefab);
            if (recordUndo) copyTo = copyToPrefab;
            else
            {
                copyTo = PrefabUtility.InstantiatePrefab(copyToPrefab) as GameObject;
                Undo.RegisterCreatedObjectUndo(copyTo, "Copy Components");
            }

            GO_Dictionary = GODictionary(copyFrom.transform, copyTo.transform);
            CollapseAll();
            componentHierarchy.UseUnfoldedAsMarker();
            fromFLB = copyFrom.tag?.StartsWith(FACSLoadBundleData.FLBTag) ?? false;
            CreateMissingsAndCopyGOs();
            CopyTransforms();

            var Components_to_copy = ComponentDictionary_CreateMissingC();
            var CToCopyCount = Components_to_copy.Count;
            var warnings = 0;
            if (CToCopyCount > 0)
            {
                MIAssets = new(); NANssets = new(); EXTssets = new();
                ManagedReferenceCopies = new();
                CopyComponentsSerializedData(Components_to_copy);
                ManagedReferenceCopies.Clear(); ManagedReferenceCopies = null;
                LogSceneExternalComponents();
                LogSkippedAssets();
                warnings = MIAssets.Count + NANssets.Count + EXTssets.Count;
                MIAssets.Clear(); NANssets.Clear(); EXTssets.Clear();
                MIAssets = NANssets = EXTssets = null;
            }
            CollapseAll();
            anythingCopiedN += (uint)CToCopyCount;
            var anythingCopied = anythingCopiedN > 0;
            if (!recordUndo)
            {
                if (anythingCopied)
                {
                    Undo.SetCurrentGroupName("Copy Components");
                    PrefabUtility.ApplyPrefabInstance(copyTo, InteractionMode.UserAction);
                }
                DestroyImmediate(copyTo);
            }
            copyTo = null;
            GO_Dictionary = null;
            C_Dictionary = null;
            if (anythingCopied)
            {
                Undo.CollapseUndoOperations(undoGroup);
                Logger.Log($"{RichToolName} Finished copying <b>{anythingCopiedN}</b> components!{(warnings > 0 ? $" <color=orange>(with {warnings} warnings)</color>" : "")}");
            }
            else
            {
                Logger.Log($"{RichToolName} <color=orange>No components were copied! Check your copy settings.</color>");
            }
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

                var NoCopyProps = new List<string> { "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Script" };
                var hideFlags = SO_from.FindProperty("m_ObjectHideFlags");
                var hideFlagsVal = hideFlags.enumValueFlag;
                if (fromFLB) hideFlagsVal &= ~(int)(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild);
                SO_to.FindProperty("m_ObjectHideFlags").enumValueFlag = hideFlagsVal;

                SerializedProperty SO_from_iterator = SO_from.GetIterator();
                SO_from_iterator.Next(true);
                
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
                                if (GO_Dictionary.TryGetValue(t_from, out Transform t_to))
                                {
                                    if (t_to) objref = t_to.gameObject;
                                    else objref = null;
                                }
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
                    {
                        var to_prop = to.FindProperty(from_iterator.propertyPath);
                        var from_mrv = from_iterator.managedReferenceValue;
                        if (from_mrv == null)
                        {
                            if (to_prop.managedReferenceValue != null) to_prop.managedReferenceValue = null;
                        }
                        else
                        {
                            if (!ManagedReferenceCopies.TryGetValue(from_mrv, out var copy_obj))
                            {
                                var fromManagedType = from_mrv.GetType();
                                if (fromManagedType.IsSubclassOf(typeof(UnityEngine.Object)))
                                { copy_obj = Instantiate((UnityEngine.Object)from_mrv); }
                                else
                                {
                                    var newObj = Activator.CreateInstance(fromManagedType);
                                    copy_obj = newObj;
                                }
                                ManagedReferenceCopies[from_mrv] = copy_obj;
                            }
                            to_prop.managedReferenceValue = copy_obj;
                            goto case SerializedPropertyType.Generic;
                        }
                        break;
                    }
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
                        { to.CopyFromSerializedProperty(from_iterator); }
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
                    var Cs_to = t_to != null ? t_to.GetComponents(T) : null;
                    for (int i = 0; i < Cs_from.Length; i++)
                    {
                        var Cs_from_i = Cs_from[i];
                        if (Cs_to == null) { C_Dictionary.Add(Cs_from_i, null); continue; }
                        var copy = CtoCopy != null && CtoCopy.Contains(Cs_from_i);
                        if (i < Cs_to.Length)
                        {
                            C_Dictionary.Add(Cs_from_i, Cs_to[i]);
                            if (copy && !skipExistingComponents) L.Add(Cs_from_i);
                        }
                        else if (copy && !onlyExistingComponents)
                        {
                            var newC = recordUndo ? Undo.AddComponent(t_to.gameObject, T) : t_to.gameObject.AddComponent(T);
                            C_Dictionary.Add(Cs_from_i, newC);
                            L.Add(Cs_from_i);
                        }
                        else C_Dictionary.Add(Cs_from_i, null);
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
                if (toggle.state)
                {
                    var isDiffTType = EnforceTransform(toggle, out var t_from, out var t_to);
                    if (!copiedOrigin && toggle.component.transform == copyFrom.transform)
                    {
                        copiedOrigin = true;
                        if (t_to == null ||
                            (isDiffTType!=1 && skipExistingComponents) ||
                            (isDiffTType==1 && onlyExistingComponents))
                            continue;
                        t_to.localRotation = t_from.localRotation;
                        t_to.localEulerAngles = t_from.localEulerAngles;
                        t_to.localScale = t_from.localScale;
                        anythingCopiedN++;
                    }
                    else
                    {
                        if (t_to == null ||
                            (isDiffTType!=1 && skipExistingComponents) ||
                            (isDiffTType==1 && onlyExistingComponents))
                            continue;
                        CopyT(t_from, t_to);
                        anythingCopiedN++;
                    }
                }
            }
            if (componentHierarchy.toggles_by_type.TryGetValue(RectTransformType, out var RTTL))
            {
                foreach (var toggle in RTTL.toggleList)
                {
                    if (toggle.state)
                    {
                        var isDiffTType = EnforceRectTransform(toggle, out var t_from, out var t_to);
                        if (!copiedOrigin && toggle.component.transform == copyFrom.transform)
                        {
                            copiedOrigin = true;
                            if (t_to == null ||
                                (isDiffTType!=1 && skipExistingComponents) ||
                                (isDiffTType==1 && onlyExistingComponents))
                                continue;
                            t_to.localRotation = t_from.localRotation;
                            t_to.localEulerAngles = t_from.localEulerAngles;
                            t_to.localScale = t_from.localScale;
                            CopyRT(t_from, t_to);
                            anythingCopiedN++;
                        }
                        else
                        {
                            if (t_to == null ||
                                (isDiffTType!=1 && skipExistingComponents) ||
                                (isDiffTType==1 && onlyExistingComponents))
                                continue;
                            CopyT(t_from, t_to);
                            CopyRT(t_from, t_to);
                            anythingCopiedN++;
                        }
                    }
                }
            }
        }

        private static sbyte EnforceTransform(ComponentHierarchy.ComponentToggle toggle, out Transform t_from, out Transform t_to, bool testOnly = false)
        {
            t_from = toggle.component.transform;
            t_to = GO_Dictionary[t_from];
            if (t_to == null) return -1;
            if (t_to.GetType() == typeof(RectTransform))
            {
                if (!onlyExistingComponents && !testOnly)
                {
                    var go = t_to.gameObject;
                    if (recordUndo) Undo.DestroyObjectImmediate(t_to);
                    else DestroyImmediate(t_to);
                    t_to = go.transform;
                    GO_Dictionary[t_from] = t_to;
                }
                return 1;
            }
            if (recordUndo && !testOnly) Undo.RecordObject(t_to, "Copy Components");
            return 0;
        }

        private static sbyte EnforceRectTransform(ComponentHierarchy.ComponentToggle toggle, out RectTransform t_from, out RectTransform t_to, bool testOnly = false)
        {
            t_to = null;
            t_from = (RectTransform)toggle.component;
            var t_t0 = GO_Dictionary[t_from];
            if (t_t0 == null) return -1;
            if (t_t0 is RectTransform t_toRT)
            {
                t_to = t_toRT;
                if (recordUndo && !testOnly) Undo.RecordObject(t_to, "Copy Components");
                return 0;
            }
            if (!onlyExistingComponents)
            {
                var go = t_t0.gameObject;
                if (!testOnly) t_to = recordUndo ? Undo.AddComponent<RectTransform>(go) : go.AddComponent<RectTransform>();
                GO_Dictionary[t_from] = t_to;
            }
            return 1;
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

        private int FilterSelectionGOs()
        {
            int toggledCount = 0;
            foreach (var fh in componentHierarchy.GO_enables)
            {
                if (fh.unfolded) // folded used as marker. true => its needed to place some Components in hierarchy
                {
                    var from_t = fh.t;
                    if (GO_Dictionary[from_t] == null)
                    {
                        if (onlyExistingPaths)
                        {
                            // deselect this GO, this Components, and all children GO and C
                            toggledCount += fh.SetAllTogglesRecursive(false, true, true);
                        }
                        // else create GO
                    }
                    else if (skipExistingComponents && fh.GO_enable)
                    {
                        fh.GO_enable = false; toggledCount++;
                    }
                }
            }
            return toggledCount;
        }

        private int FilterSelectionTransforms()
        {
            int toggledCount = 0;
            var TTL = componentHierarchy.toggles_by_type[TransformType].toggleList;
            foreach (var toggle in TTL)
            {
                if (toggle.state)
                {
                    var isDiffTType = EnforceTransform(toggle, out var t_from, out var t_to, true);                    
                    if ((isDiffTType==0 && skipExistingComponents) || (isDiffTType!=0 && onlyExistingComponents))
                    {
                        toggle.state = false;
                        toggledCount++;
                    }
                }
            }
            if (componentHierarchy.toggles_by_type.TryGetValue(RectTransformType, out var RTTL))
            {
                foreach (var toggle in RTTL.toggleList)
                {
                    if (toggle.state)
                    {
                        var isDiffTType = EnforceRectTransform(toggle, out var t_from, out var t_to, true);
                        if ((isDiffTType==0 && skipExistingComponents) || (isDiffTType!=0 && onlyExistingComponents))
                        {
                            toggle.state = false;
                            toggledCount++;
                        }
                    }
                }
            }
            return toggledCount;
        }

        private int FilterSelectionComponents()
        {
            int toggledCount = 0;
            foreach (var T in componentHierarchy.toggles_by_type.Keys)
            {
                if (T == TransformType || T == RectTransformType) continue;
                var CtoCopy = componentHierarchy.GetAllTogglesDict(T, 1);
                foreach (var pair in GO_Dictionary)
                {
                    var t_from = pair.Key; var t_to = pair.Value;
                    var Cs_from = t_from.GetComponents(T); if (Cs_from.Length == 0) continue;
                    var Cs_to = t_to != null ? t_to.GetComponents(T) : null;
                    for (int i = 0; i < Cs_from.Length; i++)
                    {
                        var Cs_from_i = Cs_from[i];
                        if (CtoCopy != null && CtoCopy.TryGetValue(Cs_from_i, out var ct))
                        {
                            if (Cs_to == null) { if (onlyExistingComponents) { ct.state = false; toggledCount++; } }
                            else if (i < Cs_to.Length)
                            {
                                if (skipExistingComponents) { ct.state = false; toggledCount++; }
                            }
                            else if (onlyExistingComponents)
                            {
                                ct.state = false; toggledCount++;
                            }
                        }
                    }
                }
            }

            return toggledCount;
        }

        private void CreateMissingsAndCopyGOs()
        {
            foreach (var fh in componentHierarchy.GO_enables)
            {
                if (fh.unfolded) // folded used as marker. true => its needed to place some Components in hierarchy
                {
                    var from_t = fh.t;
                    var go_from = from_t.gameObject;
                    if (GO_Dictionary[from_t] == null)
                    {
                        if (!onlyExistingPaths)
                        {
                            var go_to = new GameObject(go_from.name);
                            if (recordUndo) Undo.RegisterCreatedObjectUndo(go_to, "Copy Components");
                            var new_t = go_to.transform;
                            new_t.parent = GO_Dictionary[from_t.parent];
                            CopyT(from_t, new_t);
                            if (fromFLB)
                            {
                                go_to.hideFlags = go_from.hideFlags & ~(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild);
                            }
                            else
                            {
                                go_to.tag = go_from.tag;
                                go_to.hideFlags = go_from.hideFlags;
                            }
                            go_to.layer = go_from.layer;
                            go_to.SetActive(go_from.activeSelf);

                            GO_Dictionary[from_t] = new_t;
                            anythingCopiedN++;
                        }
                    }
                    else if (!skipExistingComponents && fh.GO_enable)
                    {
                        var go_to = GO_Dictionary[from_t].gameObject;
                        if (recordUndo) Undo.RecordObject(go_to, "Copy Components");
                        if (fromFLB)
                        {
                            go_to.hideFlags = go_from.hideFlags & ~(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild);
                        }
                        else
                        {
                            go_to.tag = go_from.tag;
                            go_to.hideFlags = go_from.hideFlags;
                        }
                        go_to.layer = go_from.layer;
                        go_to.SetActive(go_from.activeSelf);
                        anythingCopiedN++;
                    }
                }
            }
        }

        private Dictionary<Transform, Transform> GODictionary(Transform from, Transform to)
        {
            var TDict = new Dictionary<Transform, Transform>() { {from,to} };
            GODictionaryRecursive(TDict, from, to);
            return TDict;
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
                    var ch_to = toList[i];
                    if (ch_from.name == ch_to.name)
                    {
                        dict.Add(ch_from, ch_to); toList.RemoveAt(i); match = true;
                        if (ch_from.childCount > 0) GODictionaryRecursive(dict, ch_from, ch_to);
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
            selDepsRecursive = false;
            skipExistingComponents = false;
            onlyExistingComponents = false;
            onlyExistingPaths = false;
            anythingCopiedN = 0;
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
            advancedFoldout = false;
            NullCompHierarchy();
        }
    }
}
#endif