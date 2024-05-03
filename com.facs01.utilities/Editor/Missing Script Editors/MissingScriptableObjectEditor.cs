#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace FACS01.Utilities
{
    [CustomEditor(typeof(ScriptableObject), false, isFallback = true)]
    internal class MissingScriptableObjectEditor : MissingScriptEditor
    {
        protected override FixScripts.ScriptType GetEditingType() => FixScripts.ScriptType.ScriptableObject;
    }
}
#endif