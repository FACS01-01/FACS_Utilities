using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace FACS01.Utilities
{
    public class ShaderUsage : EditorWindow
    {
        public GameObject source = null;
        private static FACSGUIStyles FacsGUIStyles;
        private static string sourcename = "";
        private static bool _bHaveRun = false;
        private Vector2 scrollPos;
        private static List<string> all_shaders;
        private static List<List<string>> mats_with_shader;
        private static int shaderCount;

        [MenuItem("FACS Utils/Misc/Shaders Used by Object", false, 1000)]
        public static void ShowWindow()
        {
            GetWindow(typeof(ShaderUsage), false, "Shaders Used by Object", true);
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }

            EditorGUILayout.LabelField($"<color=cyan><b>Shaders Used by Object</b></color>\n\nScans the selected GameObject and displays all materials currently on it, grouped by their shaders.\n", FacsGUIStyles.helpbox);

            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(50));

            if (GUILayout.Button("Search!", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                if (source == null)
                    ShowNotification(new GUIContent("Empty field?"));
                else
                {
                    runAlg();
                }
            }
            if (!_bHaveRun) return;

            EditorGUILayout.TextArea($"<color=cyan><b>{shaderCount}</b> different shaders</color> were found in <color=green>{sourcename}</color> :", FacsGUIStyles.helpbox);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < all_shaders.Count; i++)
            {
                string temp = String.Join("\n", mats_with_shader[i]);
                EditorGUILayout.TextArea($"<color=cyan><b>{all_shaders[i]}</b></color>\n{temp}", FacsGUIStyles.helpbox);
            }
            EditorGUILayout.EndScrollView();
        }
        void OnDestroy()
        {
            source = null;
            sourcename = "";
            _bHaveRun = false;
            FacsGUIStyles = null;
        }
        public void runAlg()
        {
            sourcename = source.name;

            List<Material> all_materials = new List<Material>();
            Renderer[] all_renderers = source.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer rend in all_renderers)
            {
                foreach (Material mat in rend.sharedMaterials)
                {
                    if (!all_materials.Contains(mat) && mat != null)
                    {
                        all_materials.Add(mat);
                    }
                }
            }
            all_shaders = new List<string>();
            mats_with_shader = new List<List<string>>();
            foreach (Material mat in all_materials)
            {
                if (!all_shaders.Contains(mat.shader.name))
                {
                    all_shaders.Add(mat.shader.name);
                    mats_with_shader.Add(new List<string> { mat.shader.name, mat.name });
                }
                else
                {
                    if (!mats_with_shader[all_shaders.IndexOf(mat.shader.name)].Contains(mat.name))
                    {
                        mats_with_shader[all_shaders.IndexOf(mat.shader.name)].Add(mat.name);
                    }
                }
            }
            all_shaders.Sort();
            mats_with_shader.Sort((a, b) => a[0].CompareTo(b[0]));
            shaderCount = all_shaders.Count;

            for (int i=0; i < shaderCount; i++)
            {
                mats_with_shader[i].RemoveAt(0);
                mats_with_shader[i].Sort();
            }

            _bHaveRun = true;
        }
    }
}
