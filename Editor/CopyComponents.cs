#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class CopyComponents : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject copyFrom;
        private static GameObject copyFromTmp;
        private static GameObject copyTo;
        private static (Type, bool)[] copyComponents;
        private static Type[] copyComponentTypes;
        private static int copyMissing = 0;
        private static int TotalComponents = 0;
        private static Vector2 scroll;
        private static EditorWindow window;

        [MenuItem("FACS Utils/Repair Avatar/Copy Components", false, 1005)]
        public static void ShowWindow()
        {
            window = GetWindow(typeof(CopyComponents), false, "Copy Components", true);
            window.maxSize = new Vector2(500, 750);
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"<color=cyan><b>Copy Components</b></color>\nScans the selected GameObject to copy Components from, lists all available Components, " +
                $"and lets you choose which ones to paste into the other selected GameObject.", FacsGUIStyles.helpbox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"<b>Copy From</b>", FacsGUIStyles.helpbox);
            copyFromTmp = (GameObject)EditorGUILayout.ObjectField(copyFrom, typeof(GameObject), true, GUILayout.Height(40));
            EditorGUILayout.EndVertical();
            if (copyFromTmp != copyFrom)
            {
                copyComponents = null;
                copyFrom = copyFromTmp;
            }
            if (copyComponents != null)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"<b>Copy To</b>", FacsGUIStyles.helpbox);
                copyTo = (GameObject)EditorGUILayout.ObjectField(copyTo, typeof(GameObject), true, GUILayout.Height(40));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            if (copyFrom != null)
            {
                if (GUILayout.Button("Scan!", FacsGUIStyles.button, GUILayout.Height(40))) { GetAvailableComponentTypes(); }
            }
            else if (copyComponents != null) { copyMissing = 0; copyComponents = null; }

            if (copyComponents != null)
            {
                bool enable = false;
                EditorGUILayout.LabelField($"<color=green><b>Available Components to Copy</b></color>:", FacsGUIStyles.helpbox);

                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
                for (int i = 0; i < copyComponents.Length; i++)
                {
                    copyComponents[i].Item2 = GUILayout.Toggle(copyComponents[i].Item2, copyComponents[i].Item1.Name);
                    if (copyComponents[i].Item2) { enable = true; }
                }
                EditorGUILayout.EndScrollView();


                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", FacsGUIStyles.button, GUILayout.Height(20))) { SelectAll(true); }
                if (GUILayout.Button("Deselect All", FacsGUIStyles.button, GUILayout.Height(20))) { SelectAll(false); }
                EditorGUILayout.EndHorizontal();
                if (copyMissing > 0) { EditorGUILayout.LabelField($"Skipping Missing Scripts: {copyMissing}", FacsGUIStyles.helpboxSmall); }

                if (enable && copyTo != null && GUILayout.Button("Run!", FacsGUIStyles.button, GUILayout.Height(40)))
                {
                    //run copy paste
                    RunCopy();
                    Debug.Log($"[<color=green>CopySerialized</color>] Finished copying components!");
                }

                GUILayout.FlexibleSpace();
            }
        }
        private void RunCopy()
        {
            bool doGameObjects = false;
            bool doTransforms = false;
            List<Type> tmp = new List<Type>();
            foreach (var tuple in copyComponents)
            {
                if (tuple.Item2)
                {
                    if (tuple.Item1 != typeof(GameObject) && tuple.Item1 != typeof(Transform))
                    {
                        tmp.Add(tuple.Item1);
                    }
                    else if (tuple.Item1 == typeof(Transform))
                    {
                        doTransforms = true;
                    }
                    else
                    {
                        doGameObjects = true;
                    }
                }
            }
            copyComponentTypes = tmp.ToArray();

            if (doTransforms || doGameObjects)
            {
                if (doTransforms)
                {
                    copyTo.transform.localRotation = copyFrom.transform.localRotation;
                    copyTo.transform.localEulerAngles = copyFrom.transform.localEulerAngles;
                    copyTo.transform.localScale = copyFrom.transform.localScale;
                }
                CopyGameObjectsTransforms(copyFrom.transform, copyTo.transform, doGameObjects, doTransforms);
            }

            if (copyComponentTypes.Any())
            {
                CreateMissingComponents(copyFrom.transform, copyTo.transform);
                CopyComponentsSerializedData(copyFrom.transform, copyTo.transform);
            }
        }
        private void ComponentArrayAdder(List<(Component[], Component[])> ComponentArrayTuples, Transform t_from, Transform t_to)
        {
            foreach (Type type in copyComponentTypes)
            {
                Component[] from = t_from.GetComponents(type);
                Component[] to = t_to.GetComponents(type);
                ComponentArrayTuples.Add((from, to));
                TotalComponents += from.Length;
            }
            if (t_from.childCount > 0)
            {
                foreach (Transform child_from in t_from)
                {
                    Transform child_to = t_to.Find(child_from.name);
                    if (child_to != null) { ComponentArrayAdder(ComponentArrayTuples, child_from, child_to); }
                }
            }
        }
        private void CopyComponentsSerializedData(Transform t_from, Transform t_to)
        {
            List<(Component[], Component[])> ComponentArrayTuples = new List<(Component[], Component[])>();
            TotalComponents = 0;
            ComponentArrayAdder(ComponentArrayTuples, t_from, t_to);
            int componentCount = 0;

            int ComponentArrayTuplesCount = ComponentArrayTuples.Count;
            for (int j = 0; j < ComponentArrayTuplesCount; j++)
            {
                Component[] from = ComponentArrayTuples[j].Item1;
                Component[] to = ComponentArrayTuples[j].Item2;
                for (int i = 0; i < from.Length; i++)
                {
                    componentCount++;
                    EditorUtility.DisplayProgressBar("Copy Components", "Please wait...", (float)componentCount / TotalComponents);
                    var comp_from = from[i]; var comp_to = to[i];
                    SerializedObject SO_from = new SerializedObject(comp_from);
                    SerializedObject SO_to = new SerializedObject(comp_to);

                    SerializedProperty SO_from_iterator = SO_from.GetIterator();
                    SO_from_iterator.Next(true);
                    CopySerialized(SO_from_iterator, SO_to);
                    while (SO_from_iterator.Next(false))
                    {
                        CopySerialized(SO_from_iterator, SO_to);
                    }
                    SO_to.ApplyModifiedProperties();
                }
            }
            EditorUtility.ClearProgressBar();
        }
        private void CopySerialized(SerializedProperty from_iterator, SerializedObject to)
        {
            if (!from_iterator.isArray)
            {
                if (from_iterator.propertyType == SerializedPropertyType.Generic)
                {
                    if (from_iterator.hasChildren)
                    {
                        from_iterator.Next(true);
                        CopySerialized(from_iterator, to);
                    }
                    else { CopySerialized(from_iterator, to); }
                }
                else if (from_iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (from_iterator.name != "m_Script")
                    {
                        try
                        {
                            if (from_iterator.name == "m_CorrespondingSourceObject") { return; }
                            Component tmp = (Component)from_iterator.objectReferenceValue;
                            if (tmp != null)
                            {
                                string componentpath = AnimationUtility.CalculateTransformPath(tmp.transform, copyFrom.transform);
                                Transform child_to = copyTo.transform.Find(componentpath);
                                if (componentpath == "") { child_to = copyTo.transform; }
                                if (child_to != null)
                                {
                                    var tmptype = tmp.GetType();
                                    Component comp = child_to.GetComponent(tmptype);
                                    UnityEngine.Object ttmp = from_iterator.objectReferenceValue;
                                    from_iterator.objectReferenceValue = comp;
                                    to.CopyFromSerializedProperty(from_iterator);
                                    from_iterator.objectReferenceValue = ttmp;
                                }
                            }
                            else
                            {
                                UnityEngine.Object ttmp = from_iterator.objectReferenceValue;
                                from_iterator.objectReferenceValue = null;
                                to.CopyFromSerializedProperty(from_iterator);
                                from_iterator.objectReferenceValue = ttmp;
                                //Debug.LogWarning($"[<color=green>CopySerialized</color>] No component in serialized property");
                            }
                        }
                        catch (InvalidCastException)
                        {
                            UnityEngine.Object tmp2 = from_iterator.objectReferenceValue;
                            if (tmp2 != null)
                            {
                                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(tmp2, out string guid, out long localId))
                                {
                                    string assetpath = AssetDatabase.GUIDToAssetPath(guid);
                                    if (!String.IsNullOrEmpty(assetpath)) { to.CopyFromSerializedProperty(from_iterator); }
                                    else if (uint.Parse(guid) != 0) { Debug.LogWarning($"[<color=green>CopySerialized</color>] Asset file in CopyFrom with GUID not found in project: [{tmp2.GetType().Name}] {tmp2.name} | {guid}"); }
                                    else { Debug.Log($"[<color=green>CopySerialized</color>] Skipping Asset file in CopyFrom with no GUID: [{tmp2.GetType().Name}] {tmp2.name}"); }
                                }
                                else
                                {
                                    //Debug.LogWarning($"[<color=green>CopySerialized</color>] Asset file in CopyFrom not found in project: {tmp2.name}");
                                }
                            }
                            else
                            {
                                from_iterator.objectReferenceValue = null;
                                to.CopyFromSerializedProperty(from_iterator);
                                from_iterator.objectReferenceValue = tmp2;
                                //Debug.LogWarning($"[<color=green>CopySerialized</color>] No asset in serialized property");
                            }
                        }
                    }
                }
                else
                {
                    to.CopyFromSerializedProperty(from_iterator);
                }
            }
            else
            {
                if (from_iterator.propertyType != SerializedPropertyType.String)
                {
                    from_iterator.Next(true);
                    CopySerialized(from_iterator, to);
                }
            }
        }
        private void CreateMissingComponents(Transform t_from, Transform t_to)
        {
            foreach (Type type in copyComponentTypes)
            {
                int from = t_from.GetComponents(type).Length;
                int to = t_to.GetComponents(type).Length;
                int needsCreate = from - to;
                if (needsCreate > 0)
                {
                    for (int i = 0; i < needsCreate; i++)
                    {
                        t_to.gameObject.AddComponent(type);
                    }
                }
            }
            if (t_from.childCount > 0)
            {
                foreach (Transform child_from in t_from)
                {
                    Transform child_to = t_to.Find(child_from.name);
                    if (child_to != null) { CreateMissingComponents(child_from, child_to); }
                }
            }
        }
        private void CopyGameObjectsTransforms(Transform t_from, Transform t_to, bool doGO, bool doT)
        {
            foreach (Transform child_from in t_from)
            {
                Transform child_to = t_to.Find(child_from.name);
                if (doGO && child_to == null)
                {
                    GameObject new_go = new GameObject(child_from.name);
                    new_go.transform.parent = t_to;
                    child_to = new_go.transform;
                }
                if (child_to != null)
                {
                    if (doT)
                    {
                        child_to.localPosition = child_from.localPosition;
                        child_to.localRotation = child_from.localRotation;
                        child_to.localEulerAngles = child_from.localEulerAngles;
                        child_to.localScale = child_from.localScale;
                    }
                    child_to.gameObject.SetActive(child_from.gameObject.activeSelf);
                    CopyGameObjectsTransforms(child_from, child_to, doGO, doT);
                }
            }
        }
        private void SelectAll(bool yesno)
        {
            for (int i = 0; i < copyComponents.Length; i++)
            {
                copyComponents[i].Item2 = yesno;
            }
        }
        private void GetAvailableComponentTypes()
        {
            copyMissing = 0;
            copyComponents = null;

            List<Type> tmp_components = new List<Type>();
            List<(Type, bool)> tmp_components2 = new List<(Type, bool)>();
            Transform[] gos_t = copyFrom.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in gos_t)
            {
                Component[] components = t.GetComponents(typeof(Component));
                foreach (Component component in components)
                {
                    if (component != null)
                    {
                        Type type = component.GetType();
                        if (!tmp_components.Contains(type)) { tmp_components.Add(type); tmp_components2.Add((type, false)); }
                    }
                    else
                    {
                        copyMissing++;
                    }
                }
            }
            if (tmp_components2.Any())
            {
                tmp_components2.Insert(0, (typeof(GameObject), false));
                copyComponents = tmp_components2.ToArray();
            }
            else { Debug.LogWarning($"No components to copy from {copyFrom.name}"); }
        }
        void OnDestroy()
        {
            FacsGUIStyles = null;
            NullVars();
        }
        void NullVars()
        {
            FacsGUIStyles = null;
            copyFrom = null;
            copyFromTmp = null;
            copyTo = null;
            copyComponents = null;
            copyComponentTypes = null;
            copyMissing = 0;
        }
    }
}
#endif