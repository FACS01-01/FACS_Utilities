#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FACS01.Utilities
{
    public class CopyComponents : EditorWindow
    {
        private static readonly int TransformHashCode = typeof(Transform).GetHashCode();
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
        private static bool recordUndo = false;

        [MenuItem("FACS Utils/Repair Avatar/Copy Components", false, 1003)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(CopyComponents), false, "Copy Components", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter; }

            EditorGUILayout.LabelField($"<color=cyan><b>Copy Components</b></color>\n\n" +
                $"Scans the selected GameObject to copy Components from, lists all available Components in it, " +
                $"and lets you choose which ones to paste into the next GameObject.\n", FacsGUIStyles.helpbox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"<b>Copy From</b>", FacsGUIStyles.helpbox);
            EditorGUI.BeginChangeCheck();
            copyFromPrefab = (GameObject)EditorGUILayout.ObjectField(copyFromPrefab, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() || (!copyFromPrefab && componentHierarchy != null)) NullCompHierarchy();
            EditorGUILayout.EndVertical();
            if (componentHierarchy != null)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"<b>Copy To</b>", FacsGUIStyles.helpbox);
                EditorGUI.BeginChangeCheck();
                copyToPrefab = (GameObject)EditorGUILayout.ObjectField(copyToPrefab, typeof(GameObject), true, GUILayout.Height(40));
                if (EditorGUI.EndChangeCheck())
                {
                    if (copyToPrefab && PrefabUtility.IsPartOfPrefabAsset(copyToPrefab) && PrefabUtility.GetPrefabAssetType(copyToPrefab) == PrefabAssetType.Model)
                    {
                        Debug.LogWarning($"[<color=green>Copy Components</color>] Can't edit Model Prefabs: {copyToPrefab.name}\n");
                        copyToPrefab = null;
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            if (copyFromPrefab != null && GUILayout.Button("Scan!", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                if (!EditorUtility.IsPersistent(copyFromPrefab)) copyFrom = copyFromPrefab;
                else copyFrom = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(copyFromPrefab));
                GetAvailableComponentTypes();
            }

            if (componentHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>Available Components to Copy</b></color>:", FacsGUIStyles.helpbox);

                bool anyOn = componentHierarchy.DisplayGUI();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();
                if (componentHierarchy.hierarchyDisplay)
                {
                    if (GUILayout.Button("Collapse All", FacsGUIStyles.button, GUILayout.Height(30))) CollapseAll();
                }
                else
                {
                    if (GUILayout.Button($"{(toggleSelection ? "Select" : "Deselect")} All", FacsGUIStyles.button, GUILayout.Height(30)))
                    {
                        foreach (var ft in componentHierarchy.toggles_by_type.Values)
                        {
                            ft.SetAllToggles(toggleSelection);
                        }
                        componentHierarchy.SetAllGOEnables(toggleSelection);
                        toggleSelection = !toggleSelection;
                    }
                    if (GUILayout.Button($"{(toggleHideSelection ? "Hide" : "Show")} All", FacsGUIStyles.button, GUILayout.Height(30)))
                    {
                        componentHierarchy.HideShowAll(toggleHideSelection);
                        toggleHideSelection = !toggleHideSelection;
                    }
                }
                if (GUILayout.Button($"{(componentHierarchy.hierarchyDisplay ? "Simple" : "Hierarchy")} View", FacsGUIStyles.button, GUILayout.Height(30)))
                {
                    componentHierarchy.hierarchyDisplay = !componentHierarchy.hierarchyDisplay;
                }
                EditorGUILayout.EndHorizontal();

                if (copyMissing > 0) { EditorGUILayout.LabelField($"Skipping Missing Scripts: {copyMissing}", FacsGUIStyles.helpboxSmall); }

                if (anyOn && copyToPrefab != null && GUILayout.Button("Copy!", FacsGUIStyles.button, GUILayout.Height(40))) RunCopy();
            }
        }

        private void RunCopy()
        {
            if (copyTo) DestroyImmediate(copyTo);
            recordUndo = !EditorUtility.IsPersistent(copyToPrefab);
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
            CopyComponentsSerializedData(Components_to_copy);
            CollapseAll();

            if (!recordUndo)
            {
                Undo.SetCurrentGroupName("Copy Components");
                PrefabUtility.ApplyPrefabInstance(copyTo, InteractionMode.UserAction);
                DestroyImmediate(copyTo);
            }
            copyTo = null;
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Debug.Log($"[<color=green>Copy Components</color>] Finished copying components!\n");
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
                    EditorUtility.DisplayProgressBar("Copy Components", "Please wait...", componentCount);
                }
                
                SerializedObject SO_from = new SerializedObject(comp_from);
                SerializedObject SO_to = new SerializedObject(comp_to);
                SerializedProperty SO_from_iterator = SO_from.GetIterator();

                var NoCopyProps = new List<string> { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Script" };
                
                SO_from_iterator.Next(true);
                if (NoCopyProps.Contains(SO_from_iterator.name)) NoCopyProps.Remove(SO_from_iterator.name);
                else CopySerialized(SO_from_iterator, SO_to);
                while (SO_from_iterator.Next(false))
                {
                    if (NoCopyProps.Contains(SO_from_iterator.name)) NoCopyProps.Remove(SO_from_iterator.name);
                    else CopySerialized(SO_from_iterator, SO_to);
                }
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

        private void CopySerializedAsset(SerializedProperty from, SerializedObject to)
        {
            var objref = from.objectReferenceValue;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(objref, out string guid, out long localId))
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(guid);
                if (!String.IsNullOrEmpty(assetpath)) { to.CopyFromSerializedPropertyIfDifferent(from); }
                else if (uint.Parse(guid) != 0) { Debug.LogWarning($"[<color=green>Copy Components</color>] Asset in CopyFrom not found in project: [{objref.GetType().Name}] {objref.name} | {guid}\n"); }
                else { Debug.Log($"[<color=green>Copy Components</color>] Skipping Asset in CopyFrom with no GUID: [{objref.GetType().Name}] {objref.name} from [{from.serializedObject.targetObject.GetType().Name}] in {from.serializedObject.targetObject.name}\n"); }
            }
        }

        private void CopySerialized(SerializedProperty from_iterator, SerializedObject to)
        {
            switch (from_iterator.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    {
                        var objref = from_iterator.objectReferenceValue;
                        if (objref != null)
                        {
                            var T = objref.GetType();
                            if (T.IsSubclassOf(typeof(Component)))
                            {
                                if (T == typeof(Transform))
                                {
                                    var t_from = (Transform)objref;
                                    GO_Dictionary.TryGetValue(t_from, out Transform t_to);
                                    from_iterator.objectReferenceValue = t_to;
                                }
                                else
                                {
                                    var c_from = (Component)objref;
                                    C_Dictionary.TryGetValue(c_from, out Component c_to);
                                    from_iterator.objectReferenceValue = c_to;
                                }
                                to.CopyFromSerializedPropertyIfDifferent(from_iterator);
                            }
                            else if (T == typeof(GameObject))
                            {
                                var go = (GameObject)objref;
                                if (go.scene.name != null)
                                {
                                    var t_from = go.transform;
                                    GO_Dictionary.TryGetValue(t_from, out Transform t_to);
                                    if (t_to != null) from_iterator.objectReferenceValue = t_to.gameObject;
                                    else from_iterator.objectReferenceValue = null;
                                    to.CopyFromSerializedPropertyIfDifferent(from_iterator);
                                }
                                else CopySerializedAsset(from_iterator, to);
                            }
                            else
                            {
                                CopySerializedAsset(from_iterator, to);
                            }
                        }
                        else to.CopyFromSerializedPropertyIfDifferent(from_iterator);
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
            C_Dictionary = new Dictionary<Component, Component>();
            var L = new List<Component>();
            foreach (var THC in componentHierarchy.toggles_by_type.Keys)
            {
                if (THC == TransformHashCode) continue;
                var T = ComponentHierarchy.Types[THC].type;
                var CtoCopy = componentHierarchy.GetAllToggles(THC, 1);
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
            var TTL = componentHierarchy.toggles_by_type[TransformHashCode].toggleList;
            foreach (var toggle in TTL)
            {
                if (!copiedOrigin)
                {
                    if (toggle.component.transform == copyFrom.transform)
                    {
                        copiedOrigin = true;
                        if (toggle.state)
                        {
                            var t_from = toggle.component.transform;
                            var t_to = GO_Dictionary[t_from];
                            if (recordUndo) Undo.RecordObject(t_to, "Copy Components");
                            t_to.localRotation = t_from.localRotation;
                            t_to.localEulerAngles = t_from.localEulerAngles;
                            t_to.localScale = t_from.localScale;
                        }
                        continue;
                    }
                }
                if (toggle.state)
                {
                    var t_from = toggle.component.transform;
                    var t_to = GO_Dictionary[t_from];
                    if (recordUndo) Undo.RecordObject(t_to, "Copy Components");
                    CopyT(t_from, t_to);
                }
            }
        }

        private void CopyT(Transform from, Transform to)
        {
            to.localPosition = from.localPosition;
            to.localRotation = from.localRotation;
            to.localEulerAngles = from.localEulerAngles;
            to.localScale = from.localScale;
        }

        private void CreateMissingGOsAndSetEnables()
        {
            foreach (var fh in componentHierarchy.GO_enables)
            {
                if (fh.unfolded)
                {
                    var from_t = fh.t;
                    if (GO_Dictionary[from_t] == null)
                    {
                        var go_to = new GameObject(from_t.name);
                        if (recordUndo) Undo.RegisterCreatedObjectUndo(go_to, "Copy Components");
                        var new_t = go_to.transform;
                        new_t.parent = GO_Dictionary[from_t.parent];

                        CopyT(from_t, new_t);

                        go_to.tag = from_t.gameObject.tag;
                        go_to.layer = from_t.gameObject.layer;
                        go_to.hideFlags = from_t.gameObject.hideFlags;
                        go_to.SetActive(from_t.gameObject.activeSelf);

                        GO_Dictionary[from_t] = new_t;
                    }
                    else if (fh.GO_enable)
                    {
                        var go = GO_Dictionary[from_t].gameObject;
                        if (recordUndo) Undo.RecordObject(go, "Copy Components");
                        go.SetActive(from_t.gameObject.activeSelf);
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

        private void GetAvailableComponentTypes()
        {
            copyMissing = 0;
            componentHierarchy = null;

            componentHierarchy = new ComponentHierarchy(copyFrom.transform);

            Transform[] gos_t = copyFrom.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in gos_t)
            {
                Component[] components = t.GetComponents(typeof(Component));
                foreach (Component component in components)
                {
                    if (component != null)
                    {
                        componentHierarchy.AddComponentToggle(component);
                    }
                    else
                    {
                        copyMissing++;
                    }
                }
            }

            componentHierarchy.SortElements(false, true);
            componentHierarchy.SortTypes();
            componentHierarchy.GenerateGOEnables();
        }

        void OnDestroy()
        {
            NullVars();
        }

        void NullCompHierarchy()
        {
            UnloadTempPrefab(copyFrom);
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
                if (!inScene) PrefabUtility.UnloadPrefabContents(go);
            }
            go = null;
        }

        void NullVars()
        {
            FacsGUIStyles = null;
            copyFromPrefab = null;
            copyToPrefab = null;
            NullCompHierarchy();
        }
    }
}
#endif