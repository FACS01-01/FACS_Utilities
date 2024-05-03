#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public static class GUITools
    {
        private static readonly Color toggledColor = new(32 * 70f / (11 * 256), 32 * 96f / (11 * 256), 32 * 124f / (11 * 256), 1f);

        private static GUIContent _EditIcon;
        public static GUIContent EditIcon
        {
            get
            {
                if (_EditIcon == null) _EditIcon = EditorGUIUtility.IconContent("d_editicon.sml");
                return _EditIcon;
            }
        }

        private static GUIContent _ImportIcon;
        public static GUIContent ImportIcon
        {
            get
            {
                if (_ImportIcon == null) _ImportIcon = EditorGUIUtility.IconContent("d_Import");
                return _ImportIcon;
            }
        }

        public static bool TintedButton(Color color, string text, params GUILayoutOption[] options)
        {
            return TintedButton(color, text, GUI.skin.button, options);
        }

        public static bool TintedButton(Color color, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            if (GUILayout.Button(text, style, options)) { GUI.backgroundColor = originalColor; return true; }
            GUI.backgroundColor = originalColor; return false;
        }

        public static bool TintedButton(Color color, GUIContent content, params GUILayoutOption[] options)
        {
            return TintedButton(color, content, GUI.skin.button, options);
        }

        public static bool TintedButton(Color color, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            if (GUILayout.Button(content, style, options)) { GUI.backgroundColor = originalColor; return true; }
            GUI.backgroundColor = originalColor; return false;
        }

        public static bool ToggleButton(bool OnCondition, string text, params GUILayoutOption[] options)
        {
            return ToggleButton(OnCondition, text, GUI.skin.button, options);
        }

        public static bool ToggleButton(bool OnCondition, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            if (OnCondition)
            {
                if (TintedButton(toggledColor, text, style, options)) return true;
            }
            else
            {
                if (GUILayout.Button(text, style, options)) return true;
            }
            return false;
        }

        public static void ColoredToggle(ref bool toggle, string text, params GUILayoutOption[] options)
        {
            if (TintedButton(toggle ? Color.blue : Color.red, text, options)) toggle = !toggle;
        }

        public class GUIBGColorScope : GUI.Scope
        {
            public Color restoreColor;
            public GUIBGColorScope()
            {
                restoreColor = GUI.backgroundColor;
            }
            public GUIBGColorScope(Color toRestore)
            {
                restoreColor = toRestore;
            }
            protected override void CloseScope()
            {
                GUI.backgroundColor = restoreColor;
            }
        }
    }
}
#endif