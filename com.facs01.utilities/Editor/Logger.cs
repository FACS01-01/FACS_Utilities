#if UNITY_EDITOR
using UnityEngine;

namespace FACS01.Utilities
{
    internal static class Logger
    {
        private static readonly object[] LogFormatArgs = new object[0];

        internal const string ToolTag = "<color=cyan>";
        internal const string TypeTag = "<color=lime>";
        internal const string AssetTag = "<color=lightblue>";
        internal const string ConceptTag = "<color=#F0A0A0ff>";
        internal const string OffTag = "<color=grey>";
        internal const string EndTag = "</color>";

        internal const string RichAnimationClip = TypeTag + "AnimationClip" + EndTag;
        internal const string RichAnimationClips = TypeTag + "Animation Clips" + EndTag;
        internal const string RichAnimatorController = TypeTag + "AnimatorController" + EndTag;
        internal const string RichAsset = TypeTag + "Asset" + EndTag;
        internal const string RichAssetBundle = TypeTag + "AssetBundle" + EndTag;
        internal const string RichBlendTree = TypeTag + "BlendTree" + EndTag;
        internal const string RichGameObject = TypeTag + "GameObject" + EndTag;
        internal const string RichScene = TypeTag + "Scene" + EndTag;
        internal const string RichSkinnedMeshRenderer = TypeTag + "SkinnedMeshRenderer" + EndTag;

        internal const string RichModelPrefab = ConceptTag + "Model Prefab" + EndTag;

        public static void Log(object message)
        {
            LogFormat(LogType.Log, message, null);
        }

        public static void Log(object message, Object context)
        {
            LogFormat(LogType.Log, message, context);
        }

        public static void LogWarning(object message)
        {
            LogFormat(LogType.Warning, message, null);
        }

        public static void LogWarning(object message, Object context)
        {
            LogFormat(LogType.Warning, message, context);
        }

        public static void LogError(object message)
        {
            LogFormat(LogType.Error, message, null);
        }

        public static void LogError(object message, Object context)
        {
            LogFormat(LogType.Error, message, context);
        }

        private static void LogFormat(LogType type, object message, Object context)
        {
            if (message is not string msg) msg = message.ToString();
            Debug.LogFormat(type, LogOption.NoStacktrace, context, msg, LogFormatArgs);
        }
    }
}
#endif