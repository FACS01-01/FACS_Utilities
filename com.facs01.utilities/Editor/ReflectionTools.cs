#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FACS01.Utilities
{
    public static class ReflectionTools
    {
        #region ObjectFields

        public static Object ObjectField2T(Object obj, Type objType1, Type objType2, bool allowSceneObjects, params GUILayoutOption[] options)
        {
            return ObjectFieldTs(obj, new Type[] { objType1, objType2 }, allowSceneObjects, options);
        }

        public static Object ObjectFieldTs(Object obj, Type[] objTypes, bool allowSceneObjects, params GUILayoutOption[] options)
        {
            InitDoObjectField();
            Rect _position = EditorGUILayout.GetControlRect(false, 18f, options);
            int controlID = GUIUtility.GetControlID("s_ObjectFieldHash".GetHashCode(), FocusType.Keyboard, _position);
            Rect position = EditorGUI.IndentedRect(_position);
            var style = GUI.skin.FindStyle("ObjectField"); //EditorStyles.objectField;
            var buttonStyle = GUI.skin.FindStyle("ObjectFieldButton"); //internal
            return DoObjectFieldTs(position, position, controlID, obj, null, objTypes, allowSceneObjects, style, buttonStyle);
        }

        private static FieldInfo ObjectSelector_objectSelectorID = null;
        private static PropertyInfo ObjectSelector_get = null;
        private static PropertyInfo GUIClip_enable = null;
        private static MethodInfo EditorGUI_DrawObjectFieldLargeThumb = null;
        private static MethodInfo EditorGUI_DrawObjectFieldMiniThumb = null;
        private static MethodInfo EditorGUI_HasValidScript = null;
        private static MethodInfo EditorGUI_PingObjectInSceneViewOnClick = null;
        private static MethodInfo EditorGUI_PingObjectOrShowPreviewOnClick = null;
        private static MethodInfo ObjectSelector_GetCurrentObject = null;
        private static MethodInfo ObjectSelector_Show = null;
        private static MethodInfo PropertyEditor_OpenPropertyEditor = null;
        private static MethodInfo SpriteUtility_TextureToSprite = null;

        private static readonly Object[] AssignSelectedObjectArray = new Object[1];
        private static readonly object[] InvokeArray2 = new object[2];
        private static readonly object[] InvokeArray4 = new object[4];
        private static readonly object[] ObjectSelector_Show_InvokeArray = new object[8] { null, null, null, null, null, null, null, true };
        private static readonly GUIContent GUIContent_s_Text = new();
        private static Color EditorGUI_s_MixedValueContentColorTemp = default;
        private static readonly Color EditorGUI_s_MixedValueContentColor = new(1f, 1f, 1f, 0.5f);

        private enum EditorGUI_ObjectFieldVisualType
        {
            IconAndText,
            LargePreview,
            MiniPreview
        }

        private static void InitDoObjectField()
        {
            if (ObjectSelector_Show == null)
            {
                var EditorGUIT = typeof(EditorGUI);
                var UnityEditor_CoreModule_ASM = EditorGUIT.Assembly; var UnityEngine_IMGUIModule_ASM = typeof(Event).Assembly;
                var GUIClipT = UnityEngine_IMGUIModule_ASM.GetType("UnityEngine.GUIClip");
                var SpriteUtilityT = UnityEditor_CoreModule_ASM.GetType("UnityEditor.SpriteUtility");
                var PropertyEditorT = UnityEditor_CoreModule_ASM.GetType("UnityEditor.PropertyEditor");
                var ObjectSelectorT = UnityEditor_CoreModule_ASM.GetType("UnityEditor.ObjectSelector");
                var paramMods = new ParameterModifier[] { };
                GUIClip_enable = GUIClipT.GetProperty("enabled", BindingFlags.NonPublic | BindingFlags.Static);
                SpriteUtility_TextureToSprite = SpriteUtilityT.GetMethod("TextureToSprite", BindingFlags.Public | BindingFlags.Static);
                PropertyEditor_OpenPropertyEditor = PropertyEditorT.GetMethod("OpenPropertyEditor", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new Type[] { typeof(Object), typeof(bool) }, paramMods);
                EditorGUI_DrawObjectFieldLargeThumb = EditorGUIT.GetMethod("DrawObjectFieldLargeThumb", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new Type[] { typeof(Rect), typeof(int), typeof(Object), typeof(GUIContent) }, paramMods);
                EditorGUI_DrawObjectFieldMiniThumb = EditorGUIT.GetMethod("DrawObjectFieldMiniThumb", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new Type[] { typeof(Rect), typeof(int), typeof(Object), typeof(GUIContent) }, paramMods);
                EditorGUI_PingObjectOrShowPreviewOnClick = EditorGUIT.GetMethod("PingObjectOrShowPreviewOnClick", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new Type[] { typeof(Object), typeof(Rect) }, paramMods);
                EditorGUI_PingObjectInSceneViewOnClick = EditorGUIT.GetMethod("PingObjectInSceneViewOnClick", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new Type[] { typeof(Material) }, paramMods);
                EditorGUI_HasValidScript = EditorGUIT.GetMethod("HasValidScript", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new Type[] { typeof(Object) }, paramMods);
                ObjectSelector_objectSelectorID = ObjectSelectorT.GetField("objectSelectorID", BindingFlags.NonPublic | BindingFlags.Instance);
                ObjectSelector_GetCurrentObject = ObjectSelectorT.GetMethod("GetCurrentObject", BindingFlags.Public | BindingFlags.Static);
                ObjectSelector_get = ObjectSelectorT.GetProperty("get", BindingFlags.Public | BindingFlags.Static);
                ObjectSelector_Show = ObjectSelectorT.GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new Type[] { typeof(Object), typeof(Type[]), typeof(Object), typeof(bool), typeof(List<int>), typeof(Action<Object>), typeof(Action<Object>), typeof(bool) },
                    paramMods);
            }
        }

        private static Object DoObjectFieldTs(Rect position, Rect dropRect, int id, Object obj, Object objBeingEdited, Type[] objTypes, bool allowSceneObjects, GUIStyle style, GUIStyle buttonStyle)
        {
            Event currentEvent = Event.current;
            EventType eventType = currentEvent.type;
            if (!GUI.enabled && (bool)GUIClip_enable.GetValue(null) && currentEvent.rawType == EventType.MouseDown) eventType = EventType.MouseDown;
            var commonType = FindCommonType(objTypes);
            var objectFieldVisualType = EditorGUI_ObjectFieldVisualType.IconAndText;
            Vector2 iconSize = EditorGUIUtility.GetIconSize();
            if (EditorGUIUtility.HasObjectThumbnail(commonType))
            {
                if (position.height > 18f)
                { objectFieldVisualType = EditorGUI_ObjectFieldVisualType.LargePreview; EditorGUIUtility.SetIconSize(new Vector2(64f, 64f)); }
                else if (position.width <= 32f) objectFieldVisualType = EditorGUI_ObjectFieldVisualType.MiniPreview;
            }
            if (objectFieldVisualType == EditorGUI_ObjectFieldVisualType.IconAndText) EditorGUIUtility.SetIconSize(new Vector2(12f, 12f));
            if (((eventType == EventType.MouseDown && currentEvent.button == 1) || (eventType == EventType.ContextClick &&
                objectFieldVisualType == EditorGUI_ObjectFieldVisualType.IconAndText)) && position.Contains(currentEvent.mousePosition))
            {
                Object actualObject = obj;
                GenericMenu genericMenu = new();
                genericMenu.AddItem(GUIContent_Temp("Properties..."), false,
                    delegate {
                        InvokeArray2[0] = actualObject; InvokeArray2[1] = true;
                        PropertyEditor_OpenPropertyEditor.Invoke(null, InvokeArray2);
                        InvokeArray2[0] = null; InvokeArray2[1] = null;
                    });
                genericMenu.DropDown(position);
                currentEvent.Use();
            }
            if (eventType != EventType.MouseDown)
            {
                switch (eventType)
                {
                    case EventType.KeyDown:
                        if (GUIUtility.keyboardControl == id)
                        {
                            if (currentEvent.keyCode == KeyCode.Backspace || (currentEvent.keyCode == KeyCode.Delete &&
                                (currentEvent.modifiers & EventModifiers.Shift) == EventModifiers.None))
                            {
                                obj = null;
                                GUI.changed = true;
                                currentEvent.Use();
                            }
                            if (Event_MainActionKeyForControl(currentEvent, id))
                            {
                                ObjectSelector_get_Show(obj, objTypes, objBeingEdited, allowSceneObjects);
                                ObjectSelector_objectSelectorID.SetValue(ObjectSelector_get.GetValue(null), id);
                                currentEvent.Use();
                                GUIUtility.ExitGUI();
                            }
                        }
                        break;
                    case EventType.Repaint:
                        GUIContent guicontent;
                        if (EditorGUI.showMixedValue) guicontent = EditorGUIUtility.TrTextContent("\u2014", "Mixed Values");
                        else if (obj) guicontent = EditorGUIUtility.ObjectContent(obj, obj.GetType());
                        else guicontent = EditorGUIUtility.ObjectContent(obj, commonType);
                        switch (objectFieldVisualType)
                        {
                            case EditorGUI_ObjectFieldVisualType.IconAndText:
                            default:
                                EditorGUI_s_MixedValueContentColorTemp = GUI.contentColor;
                                GUI.contentColor = (EditorGUI.showMixedValue ? (GUI.contentColor * EditorGUI_s_MixedValueContentColor) : GUI.contentColor);
                                style.Draw(position, guicontent, id, DragAndDrop.activeControlID == id, position.Contains(currentEvent.mousePosition));
                                Rect rect = buttonStyle.margin.Remove(EditorGUI_GetButtonRect(objectFieldVisualType, position));
                                buttonStyle.Draw(rect, GUIContent.none, id, DragAndDrop.activeControlID == id, rect.Contains(currentEvent.mousePosition));
                                GUI.contentColor = EditorGUI_s_MixedValueContentColorTemp;
                                break;
                            case EditorGUI_ObjectFieldVisualType.LargePreview:
                                InvokeArray4[0] = position; InvokeArray4[1] = id; InvokeArray4[2] = obj; InvokeArray4[3] = guicontent;
                                EditorGUI_DrawObjectFieldLargeThumb.Invoke(null, InvokeArray4);
                                InvokeArray4[0] = InvokeArray4[1] = InvokeArray4[2] = InvokeArray4[3] = null;
                                break;
                            case EditorGUI_ObjectFieldVisualType.MiniPreview:
                                InvokeArray4[0] = position; InvokeArray4[1] = id; InvokeArray4[2] = obj; InvokeArray4[3] = guicontent;
                                EditorGUI_DrawObjectFieldMiniThumb.Invoke(null, InvokeArray4);
                                InvokeArray4[0] = InvokeArray4[1] = InvokeArray4[2] = InvokeArray4[3] = null;
                                break;
                        }
                        break;
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if (eventType == EventType.DragPerform)
                        {
                            if (!EditorGUI_ValidDroppedObject(DragAndDrop.objectReferences, out var errorStr))
                            {
                                EditorUtility.DisplayDialog("Can't assign script", errorStr, "OK");
                                break;
                            }
                        }
                        if (GUI.enabled && dropRect.Contains(currentEvent.mousePosition))
                        {
                            Object object2 = EditorGUI_CUSTOM_ObjectFieldValidator(DragAndDrop.objectReferences, objTypes);
                            if (object2 != null && (allowSceneObjects || EditorUtility.IsPersistent(object2)))
                            {
                                if (DragAndDrop.visualMode == DragAndDropVisualMode.None)
                                { DragAndDrop.visualMode = DragAndDropVisualMode.Generic; }
                                if (eventType == EventType.DragPerform)
                                {
                                    obj = object2;
                                    GUI.changed = true;
                                    DragAndDrop.AcceptDrag();
                                    DragAndDrop.activeControlID = 0;
                                }
                                else DragAndDrop.activeControlID = id;
                                currentEvent.Use();
                            }
                        }
                        break;
                    case EventType.ValidateCommand:
                        if ((currentEvent.commandName == "Delete" || currentEvent.commandName == "SoftDelete") && GUIUtility.keyboardControl == id) currentEvent.Use();
                        break;
                    case EventType.ExecuteCommand:
                        if (currentEvent.commandName == "ObjectSelectorUpdated" &&
                                (int)ObjectSelector_objectSelectorID.GetValue(ObjectSelector_get.GetValue(null)) == id && GUIUtility.keyboardControl == id)
                        { return EditorGUI_AssignSelectedObject(objTypes, currentEvent); }
                        if ((currentEvent.commandName == "Delete" || currentEvent.commandName == "SoftDelete") && GUIUtility.keyboardControl == id)
                        {
                            obj = null;
                            GUI.changed = true;
                            currentEvent.Use();
                        }
                        break;
                    case EventType.DragExited:
                        if (GUI.enabled) HandleUtility.Repaint();
                        break;
                }
            }
            else
            {
                if (position.Contains(currentEvent.mousePosition) && currentEvent.button == 0)
                {
                    Rect buttonRect = EditorGUI_GetButtonRect(objectFieldVisualType, position);
                    EditorGUIUtility.editingTextField = false;
                    if (buttonRect.Contains(currentEvent.mousePosition))
                    {
                        if (GUI.enabled)
                        {
                            GUIUtility.keyboardControl = id;
                            ObjectSelector_get_Show(obj, objTypes, objBeingEdited, allowSceneObjects);
                            ObjectSelector_objectSelectorID.SetValue(ObjectSelector_get.GetValue(null), id);
                            currentEvent.Use();
                            GUIUtility.ExitGUI();
                        }
                    }
                    else
                    {
                        Object object3 = obj;
                        if (EditorGUI.showMixedValue) object3 = null;
                        else if (obj is Component c) object3 = c.gameObject;
                        if (currentEvent.clickCount == 1)
                        {
                            GUIUtility.keyboardControl = id;
                            InvokeArray2[0] = object3; InvokeArray2[1] = position;
                            EditorGUI_PingObjectOrShowPreviewOnClick.Invoke(null, InvokeArray2);
                            InvokeArray2[0] = null; InvokeArray2[1] = null;
                            if (object3 is Material mat)
                            {
                                InvokeArray1[0] = mat;
                                EditorGUI_PingObjectInSceneViewOnClick.Invoke(null, InvokeArray1);
                                InvokeArray1[0] = null;
                            }
                            currentEvent.Use();
                        }
                        else if (currentEvent.clickCount == 2 & object3)
                        {
                            AssetDatabase.OpenAsset(object3);
                            currentEvent.Use();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
            EditorGUIUtility.SetIconSize(iconSize);
            return obj;
        }

        private static GUIContent GUIContent_Temp(string t)
        {
            GUIContent_s_Text.text = t;
            GUIContent_s_Text.tooltip = string.Empty;
            return GUIContent_s_Text;
        }

        private static Rect EditorGUI_GetButtonRect(EditorGUI_ObjectFieldVisualType visualType, Rect position)
        {
            Rect rect;
            switch (visualType)
            {
                case EditorGUI_ObjectFieldVisualType.LargePreview:
                    rect = new(position.xMax - 36f, position.yMax - 14f, 36f, 14f);
                    break;
                case EditorGUI_ObjectFieldVisualType.MiniPreview:
                    rect = new(position.xMax - 14f, position.y, 14f, position.height);
                    break;
                case EditorGUI_ObjectFieldVisualType.IconAndText:
                default:
                    rect = new(position.xMax - 19f, position.y, 19f, position.height);
                    break;
            }
            return rect;
        }

        private static Object EditorGUI_AssignSelectedObject(Type[] objTypes, Event evt)
        {
            AssignSelectedObjectArray[0] = (Object)ObjectSelector_GetCurrentObject.Invoke(null, null);
            var obj = EditorGUI_CUSTOM_ObjectFieldValidator(AssignSelectedObjectArray, objTypes);
            AssignSelectedObjectArray[0] = null;//
            GUI.changed = true;
            evt.Use();
            return obj;
        }

        private static bool EditorGUI_ValidDroppedObject(Object[] references, out string errorString)
        {
            errorString = "";
            if (references == null || references.Length == 0) return true;
            Object object2 = references[0];
            if ((object2 is MonoBehaviour || object2 is ScriptableObject))
            {
                InvokeArray1[0] = object2;
                var hasValid = (bool)EditorGUI_HasValidScript.Invoke(null, InvokeArray1);
                InvokeArray1[0] = null;
                if (!hasValid)
                {
                    errorString = string.Format("Type cannot be found: {0}. Containing file and class name must match.", object2.GetType());
                    return false;
                }
            }
            return true;
        }

        private static bool Event_MainActionKeyForControl(Event evt, int controlId)
        {
            bool flag2;
            if (GUIUtility.keyboardControl != controlId) flag2 = false;
            else
            {
                bool flag3 = evt.alt || evt.shift || evt.command || evt.control;
                flag2 = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !flag3;
            }
            return flag2;
        }

        private static void ObjectSelector_get_Show(Object obj, Type[] requiredTypes, Object objectBeingEdited, bool allowSceneObjects)
        {
            ObjectSelector_Show_InvokeArray[0] = obj;
            ObjectSelector_Show_InvokeArray[1] = requiredTypes;
            ObjectSelector_Show_InvokeArray[2] = objectBeingEdited;
            ObjectSelector_Show_InvokeArray[3] = allowSceneObjects;
            var objSelector = ObjectSelector_get.GetValue(null);
            ObjectSelector_Show.Invoke(objSelector, ObjectSelector_Show_InvokeArray);
            ObjectSelector_Show_InvokeArray[0] = ObjectSelector_Show_InvokeArray[1] = ObjectSelector_Show_InvokeArray[2] = ObjectSelector_Show_InvokeArray[3] = null;
        }

        private static Type FindCommonType(Type[] types)
        {
            if (types == null || types.Length == 0) return null;
            Type commonType = types[0];
            for (int i = 1; i < types.Length; i++)
            {
                commonType = FindCommonBaseType(commonType, types[i]);
                if (commonType == typeof(object)) break;
            }
            return commonType;
        }

        private static Type FindCommonBaseType(Type type1, Type type2)
        {
            if (type1 == null || type2 == null) return typeof(object);
            if (type1.IsAssignableFrom(type2)) return type1;
            if (type2.IsAssignableFrom(type1)) return type2;

            Type[] commonInterfaces = type1.GetInterfaces().Intersect(type2.GetInterfaces()).ToArray();
            if (commonInterfaces.Length > 0) return commonInterfaces[0];

            while (type1 != null && type1 != typeof(object))
            {
                type1 = type1.BaseType;
                if (type2.IsAssignableFrom(type1)) return type1;
            }
            return typeof(object);
        }

        private static Object EditorGUI_CUSTOM_ObjectFieldValidator(Object[] references, Type[] objTypes)
        {
            if (references.Length != 0)
            {
                if (references[0] != null && references[0] is Texture2D tex2D && objTypes.Contains(typeof(Sprite)) && DragAndDrop.objectReferences.Length != 0)
                {
                    InvokeArray1[0] = tex2D;
                    var sprite = (Object)SpriteUtility_TextureToSprite.Invoke(null, InvokeArray1);
                    InvokeArray1[0] = null;
                    return sprite;
                }
                if (references[0] != null && references[0] is GameObject gameObject2 && AssignableFromAny(typeof(Component), objTypes))
                { Object[] array = gameObject2.GetComponents(typeof(Component)); references = array; }
                foreach (Object object2 in references)
                { if (object2 != null && AnyAssignableFrom(objTypes, object2.GetType())) return object2; }
            }
            return null;
        }

        private static bool AnyAssignableFrom(IEnumerable<Type> types, Type type)
        {
            foreach (var T in types) { if (T.IsAssignableFrom(type)) return true; }
            return false;
        }

        private static bool AssignableFromAny(Type type, IEnumerable<Type> types)
        {
            foreach (var T in types) { if (type.IsAssignableFrom(T)) return true; }
            return false;
        }

        #endregion

        #region YAML

        public static string ToYAMLString(Object obj)
        {
            if (object.Equals(obj, null)) return null;
            if (SerializationDebug_ToYAMLString == null)
            {
                var UnityEditor_CoreModule_ASM = typeof(EditorGUI).Assembly;
                var SerializationDebugT = UnityEditor_CoreModule_ASM.GetType("UnityEditor.SerializationDebug");
                SerializationDebug_ToYAMLString = SerializationDebugT.GetMethod("ToYAMLString", BindingFlags.NonPublic | BindingFlags.Static);
            }
            InvokeArray1[0] = obj;
            var yaml = SerializationDebug_ToYAMLString.Invoke(null, InvokeArray1);
            InvokeArray1[0] = null;
            if (yaml == null) return null;
            return (string)yaml;
        }

        private static MethodInfo SerializationDebug_ToYAMLString = null;

        internal static bool TryGetScriptGUIDandFileIDfromYAML(string yaml, out string GUID, out long FileID)
        {
            GUID = ""; FileID = 0;
            if (string.IsNullOrEmpty(yaml)) return false;
            var monoScriptPos = yaml.IndexOf("\n  m_Script: ");
            if (monoScriptPos == -1) return false;
            var instanceIDPos = yaml.IndexOf("instanceID:", monoScriptPos);
            var instanceIDEndPos = yaml.IndexOf('}', instanceIDPos);
            var instanceID = yaml[(instanceIDPos + 12)..instanceIDEndPos];
            if (!int.TryParse(instanceID, out int ID)) return false;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ID, out string _GUID, out long _FileID))
            { GUID = _GUID; FileID = _FileID; return true; }
            return false;
        }

        #endregion

        #region InspectorWindows

        public static EditorWindow[] GetAllInspectorWindows()
        {
            if (InspectorWindowT == null) InspectorWindowT = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var windows = Resources.FindObjectsOfTypeAll(InspectorWindowT);
            var output = new EditorWindow[windows.Length];
            for (int i = 0; i < windows.Length; i++) { output[i] = (EditorWindow)windows[i]; }
            return output;
        }

        #endregion

        #region FACS Internals

        private static readonly object[] InvokeArray1 = new object[1];
        private static MethodInfo PrefabUtility_IsInstanceIDPartOfNonAssetPrefabInstance = null;

        internal static bool IsInstanceIDPartOfNonAssetPrefabInstance(int componentOrGameObjectInstanceID)
        {
            if (PrefabUtility_IsInstanceIDPartOfNonAssetPrefabInstance == null)
                PrefabUtility_IsInstanceIDPartOfNonAssetPrefabInstance = typeof(PrefabUtility).GetMethod("IsInstanceIDPartOfNonAssetPrefabInstance", (BindingFlags)40);
            InvokeArray1[0] = componentOrGameObjectInstanceID;
            var b = (bool)PrefabUtility_IsInstanceIDPartOfNonAssetPrefabInstance.Invoke(null, InvokeArray1);
            InvokeArray1[0] = null;
            return b;
        }

        private static Type InspectorWindowT = null;
        private static FieldInfo InspectorWindow_Tracker = null;

        internal static ActiveEditorTracker GetInspectorWindowTracker(EditorWindow inspector)
        {
            if (!inspector) return null;
            if (InspectorWindowT == null) InspectorWindowT = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (!InspectorWindowT.IsAssignableFrom(inspector.GetType())) return null;
            if (InspectorWindow_Tracker == null) InspectorWindow_Tracker = InspectorWindowT.GetField("m_Tracker", (BindingFlags)36);
            var tr = InspectorWindow_Tracker.GetValue(inspector);
            if (tr == null) return null;
            return (ActiveEditorTracker)tr;
        }

        internal static ActiveEditorTracker CreateTracker(Object obj)
        {
            if (object.Equals(obj, null)) return null;
            if (ActiveEditorTracker_SetObjectsLockedByThisTracker == null)
                ActiveEditorTracker_SetObjectsLockedByThisTracker = typeof(ActiveEditorTracker).GetMethod("SetObjectsLockedByThisTracker", (BindingFlags)36);

            var tracker = new ActiveEditorTracker() { isLocked = true };
            InvokeListObject1[0] = obj;
            InvokeArray1[0] = InvokeListObject1;
            ActiveEditorTracker_SetObjectsLockedByThisTracker.Invoke(tracker, InvokeArray1);
            InvokeListObject1[0] = null;
            InvokeArray1[0] = null;
            tracker.isLocked = true;
            tracker.ForceRebuild();
            return tracker;
        }

        private static MethodInfo ActiveEditorTracker_SetObjectsLockedByThisTracker = null;
        private static readonly List<Object> InvokeListObject1 = new(capacity: 1) { null };

        #endregion
    }
}
#endif