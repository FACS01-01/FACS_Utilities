#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class ShaderUsage : EditorWindow
    {
        private static GameObject source;
        private static FACSGUIStyles FacsGUIStyles;
        private static List<(string, string)> shaders_and_mats;
        private static bool[] foldouts;
        private static Vector2 scrollPos;
        private static int shaderCount;

        [MenuItem("FACS Utils/Misc/Shaders Used by Object", false, 1002)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(ShaderUsage), false, "Shaders Used by Object", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }

            EditorGUILayout.LabelField($"<color=cyan><b>Shaders Used by Object</b></color>\n\n" +
                $"Scans the selected GameObject and displays all materials currently on it, grouped by their shaders.\n", FacsGUIStyles.helpbox);

            EditorGUI.BeginChangeCheck();
            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() || (source == null && shaders_and_mats != null)) NullVars();
            if (source && GUILayout.Button("Search!", FacsGUIStyles.button, GUILayout.Height(40))) runAlg();

            if (shaders_and_mats != null)
            {
                EditorGUILayout.LabelField($"<color=cyan><b>{shaderCount}</b> different shaders</color> were found in <color=green>{source.name}</color> :", FacsGUIStyles.helpbox);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                int i = 0;
                foreach (var sh_mat in shaders_and_mats)
                {
                    var shaderName = sh_mat.Item1; var matList = sh_mat.Item2;
                    foldouts[i] = EditorGUILayout.Foldout(foldouts[i], shaderName, true, FacsGUIStyles.foldout);
                    if (foldouts[i]) EditorGUILayout.LabelField(matList, FacsGUIStyles.helpbox);
                    i++;
                }
                EditorGUILayout.EndScrollView();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy to Clipboard", FacsGUIStyles.button, GUILayout.Height(40)))
                {
                    var clipb = $"Shaders in {source.name}:";
                    foreach (var sh_mats in shaders_and_mats)
                    {
                        clipb += $"\n• {sh_mats.Item1.Replace("<color=cyan><b>", "").Replace("</b></color>", "")}\n  - {sh_mats.Item2.Replace("\n", "\n  - ")}";
                    }
                    GUIUtility.systemCopyBuffer = clipb;
                    ShowNotification(new GUIContent("Copied!"));
                }
            }
        }

        private void runAlg()
        {
            NullVars();
            var shaders_and_mats_t = new SortedDictionary<string, List<Material>>();
            List<Material> all_materials = new List<Material>();
            Renderer[] all_renderers = source.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer rend in all_renderers)
            {
                foreach (Material mat in rend.sharedMaterials)
                {
                    if (shaders_and_mats_t.TryGetValue(mat.shader.name, out List<Material> matL))
                    {
                        if (!matL.Contains(mat)) matL.Add(mat);
                    }
                    else shaders_and_mats_t[mat.shader.name] = new List<Material> { mat };
                }
            }
            if (shaders_and_mats_t.Count == 0)
            {
                Debug.LogWarning($"[<color=green>Shader Usage</color>] Didn't find any materials in: {source.name}");
                return;
            }

            foreach (var L in shaders_and_mats_t.Values) L.Sort((a, b) => a.name.CompareTo(b.name));
            shaderCount = shaders_and_mats_t.Count;
            shaders_and_mats = shaders_and_mats_t.Select(x => ($"<color=cyan><b>{x.Key}</b></color>", String.Join("\n", x.Value.Select(m=>m.name)))).ToList();
            foldouts = new bool[shaderCount];
        }

        void OnDestroy()
        {
            source = null;
            FacsGUIStyles = null;
            NullVars();
        }

        private void NullVars()
        {
            shaders_and_mats = null;
            foldouts = null;
            shaderCount = 0;
            scrollPos = default;
        }
    }
}
#endif