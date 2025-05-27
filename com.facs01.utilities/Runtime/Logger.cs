#if UNITY_EDITOR
using UnityEngine;

namespace FACS01.Utilities
{
    public static class Logger
    {
        private static readonly object[] LogFormatArgs = new object[0];

        public const string ToolTag = "<color=cyan>";
        public const string TypeTag = "<color=lime>";
        public const string AssetTag = "<color=lightblue>";
        public const string ConceptTag = "<color=#F0A0A0ff>";
        public const string OffTag = "<color=grey>";
        public const string EndTag = "</color>";

        public const string RichAnimationClip = TypeTag + "AnimationClip" + EndTag;
        public const string RichAnimationClips = TypeTag + "Animation Clips" + EndTag;
        public const string RichAnimatorController = TypeTag + "AnimatorController" + EndTag;
        public const string RichAsset = TypeTag + "Asset" + EndTag;
        public const string RichAssetBundle = TypeTag + "AssetBundle" + EndTag;
        public const string RichBlendTree = TypeTag + "BlendTree" + EndTag;
        public const string RichGameObject = TypeTag + "GameObject" + EndTag;
        public const string RichScene = TypeTag + "Scene" + EndTag;
        public const string RichSkinnedMeshRenderer = TypeTag + "SkinnedMeshRenderer" + EndTag;

        public const string RichAssetsFolder = ConceptTag + "Assets Folder" + EndTag;
        public const string RichModel = ConceptTag + "Model" + EndTag;
        public const string RichModelPrefab = ConceptTag + "Model Prefab" + EndTag;

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