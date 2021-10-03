using UnityEngine;

namespace FACS01.Utilities
{
    public class FACSGUIStyles
    {
        public GUIStyle helpbox;
        public GUIStyle dropdownbutton;
        public GUIStyle button;
        public GUIStyle helpboxSmall;
        public GUIStyle buttonSmall;

        public FACSGUIStyles()
        {
            helpbox = new GUIStyle("HelpBox")
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

            helpboxSmall = new GUIStyle("HelpBox")
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
        }
    }
}