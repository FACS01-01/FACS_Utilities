#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace FACS01.Utilities
{
    public class FACSGUIStyles
    {
        private GUIStyle _Button;
        private GUIStyle _CenteredLabel;
        private GUIStyle _DDB;
        private GUIStyle _Foldout;
        private GUIStyle _Helpbox;
        private GUIStyle _Label;
        private GUIStyle _TextField;
        private GUIStyle _Toggle;
        private GUIStyle _ButtonSmall;
        private GUIStyle _HelpboxSmall;

        public GUIStyle Button
        {get{
                if (_Button == null)
                    _Button = new("button")
                    {
                        richText = true,
                        fontSize = 13,
                        alignment = TextAnchor.MiddleCenter
                    };
                return _Button;
        }}
        public GUIStyle CenteredLabel
        {
            get
            {
                if (_CenteredLabel == null)
                    _CenteredLabel = new(EditorStyles.label)
                    {
                        richText = true,
                        fontSize = 13,
                        alignment = TextAnchor.MiddleCenter
                    };
                return _CenteredLabel;
            }
        }
        public GUIStyle DDB
        {get{
                if (_DDB == null)
                    _DDB = new("DropDownButton")
                    {
                        richText = true,
                        alignment = TextAnchor.MiddleLeft
                    };
                return _DDB;
        }}
        public GUIStyle Foldout
        {get{
                if (_Foldout == null)
                    _Foldout = new(EditorStyles.foldout)
                    {
                        richText = true,
                        fontSize = 13,
                        alignment = TextAnchor.MiddleLeft
                    };
                return _Foldout;
        }}
        public GUIStyle Helpbox
        {get{
                if (_Helpbox == null)
                    _Helpbox = new(EditorStyles.helpBox)
                    {
                        richText = true,
                        fontSize = 13,
                        wordWrap = true,
                        alignment = TextAnchor.MiddleCenter
                    };
                return _Helpbox;
        }}
        public GUIStyle Label
        {get{
                if (_Label == null)
                    _Label = new(EditorStyles.label)
                    {
                        richText = true,
                        fontSize = 13
                    };
                return _Label;
        }}
        public GUIStyle TextField
        {get{
                if (_TextField == null)
                    _TextField = new(EditorStyles.textField)
                    {
                        fontSize = 13,
                        alignment = TextAnchor.MiddleLeft
                    };
                return _TextField;
        }}
        public GUIStyle Toggle
        {get{
                if (_Toggle == null)
                    _Toggle = new(EditorStyles.toggle)
                    {
                        richText = true,
                        fontSize = 13,
                        alignment = TextAnchor.MiddleCenter
                    };
                return _Toggle;
        }}
        public GUIStyle ButtonSmall
        {get{
                if (_ButtonSmall == null)
                    _ButtonSmall = new("button")
                    {
                        richText = true,
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter,
                        //padding = new RectOffset(4, 4, 1, 2)
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                return _ButtonSmall;
        }}
        public GUIStyle HelpboxSmall
        {get{
                if (_HelpboxSmall == null)
                    _HelpboxSmall = new(EditorStyles.helpBox)
                    {
                        richText = true,
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };
                return _HelpboxSmall;
        }}
    }
}
#endif