#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace FACS01.Utilities
{
    [CustomEditor(typeof(MonoBehaviour), false, isFallback = true)]
    internal class MissingMonoBehaviourEditor : MissingScriptEditor
    {
        protected override FixScripts.ScriptType GetEditingType() => FixScripts.ScriptType.MonoBehaviour;
    }
}
#endif