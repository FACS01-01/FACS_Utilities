using UnityEngine;
using UnityEditor;

namespace FACS01.Utilities
{
    public class FACSGUIStyles
    {
        public GUIStyle helpbox;
        public GUIStyle dropdownbutton;
        public GUIStyle button;
        public GUIStyle helpboxSmall;
        public GUIStyle buttonSmall;
        public GUIStyle wrappedLabel;
        public GUIStyle foldout;

        public FACSGUIStyles()
        {
            helpbox = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                wordWrap = true
            };
            
            dropdownbutton = new GUIStyle("dropdownbutton")
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft
            };

            button = new GUIStyle("button")
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };

            helpboxSmall = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(4, 4, 1, 2)
            };

            buttonSmall = new GUIStyle("button")
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(4, 4, 1, 2)
            };

            wrappedLabel = new GUIStyle()
            {
                richText = true,
                fontSize = 13,
                wordWrap = true
            };

            foldout = new GUIStyle(EditorStyles.foldout)
            {
                richText = true,
                fontSize = 13
            };
        }
    }
}