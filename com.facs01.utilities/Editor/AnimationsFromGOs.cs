#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class AnimationsFromGOs
    {
        private const string RichAnimationRootName = Logger.ConceptTag + "Animation Root" + Logger.EndTag;
        private const string RichAnimationSourcesName = Logger.ConceptTag + "Animation Sources" + Logger.EndTag;

        public float windowWidthHalf;
        public readonly List<GameObject> animationRoots = new() { null };
        public readonly List<List<Object>> animationSources = new() { new() { null } };
        private static readonly System.Type[] AnimationSourceTypes = new System.Type[] { typeof(RuntimeAnimatorController), typeof(AnimationClip) };
        private Vector2 scrollView;

        public void OnGUI(FACSGUIStyles FacsGUIStyles, float windowWidth)
        {
            windowWidthHalf = windowWidth / 2;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Animation Roots", FacsGUIStyles.Helpbox, GUILayout.MaxWidth(windowWidthHalf));
            EditorGUILayout.LabelField("Animation Sources", FacsGUIStyles.Helpbox, GUILayout.MaxWidth(windowWidthHalf));
            EditorGUILayout.EndHorizontal();

            scrollView = EditorGUILayout.BeginScrollView(scrollView, GUILayout.ExpandHeight(true));
            int root_i = 0;
            while (root_i < animationRoots.Count)
            { if (DisplayAnimationRoot(root_i)) root_i++; }
            if (animationRoots[^1])
            { animationRoots.Add(null); animationSources.Add(new() { null }); }
            GUILayout.Space(4);
            EditorGUILayout.EndScrollView();
        }

        private bool DisplayAnimationRoot(int i)
        {
            if (!animationRoots[i] && i != animationRoots.Count - 1)
            { animationRoots.RemoveAt(i); animationSources.RemoveAt(i); return false; }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            var newRoot_i = (GameObject)EditorGUILayout.ObjectField(animationRoots[i], typeof(GameObject), true, GUILayout.MaxWidth(windowWidthHalf));
            if (EditorGUI.EndChangeCheck())
            { if (!newRoot_i || !animationRoots.Contains(newRoot_i)) animationRoots[i] = newRoot_i; }
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            if (animationRoots[i])
            {
                int source_i_j = 0;
                var sources_i = animationSources[i];
                while (source_i_j < sources_i.Count)
                { if (DisplayAnimationSources(sources_i, source_i_j)) source_i_j++; }
                if (sources_i[^1]) sources_i.Add(null);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            return true;
        }

        private bool DisplayAnimationSources(List<Object> sources_i, int j)
        {
            if (!sources_i[j] && j != sources_i.Count - 1)
            { sources_i.RemoveAt(j); return false; }
            EditorGUI.BeginChangeCheck();
            var newsource_i_j = ReflectionTools.ObjectFieldTs(sources_i[j], AnimationSourceTypes, true, GUILayout.MaxWidth(windowWidthHalf));
            if (EditorGUI.EndChangeCheck())
            {
                if (!newsource_i_j) { sources_i[j] = null; return true; }
                var newsourceT = newsource_i_j.GetType();
                if ((newsourceT.IsSubclassOf(typeof(RuntimeAnimatorController)) || newsourceT == typeof(AnimationClip))
                    && !sources_i.Contains(newsource_i_j)) sources_i[j] = newsource_i_j;
            }
            return true;
        }

        public bool AutoDetect(string toolName = null)
        {
            if (ComponentDependencies.TryGetAssetsAndSources(animationRoots, AnimationSourceTypes, out List<GameObject> roots, out List<List<Object>> sources))
            {
                animationRoots.Clear(); animationSources.Clear();
                animationRoots.AddRange(roots);
                foreach (var s in sources) { s.Add(null); animationSources.Add(s); }
                animationRoots.Add(null); animationSources.Add(new() { null });
                if (!string.IsNullOrEmpty(toolName)) Logger.Log(toolName + " Auto Detect finished!");
                return true;
            }
            if (!string.IsNullOrEmpty(toolName)) Logger.LogWarning($"{toolName} No {RichAnimationRootName} has {RichAnimationSourcesName}.");
            return false;
        }

        public void Clear()
        {
            animationRoots.Clear(); animationRoots.Add(null);
            animationSources.Clear(); animationSources.Add(new() { null });
        }
    }
}
#endif