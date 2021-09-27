using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace FACS01.Utilities
{
    [CustomEditor(typeof(FACSLoadBundle2019))]
    public class FACSLoadBundle2019Editor : Editor
    {
        private List<string> ShaderUsageIn;
        private List<List<string>> MaterialUsageIn;
        private readonly string[] filters = { "VRChat Files (vrca, vrcw)", "vrca,vrcw", "All files", "*" };
        public override void OnInspectorGUI()
        {
            FACSLoadBundle2019 LoadBundleScript = (FACSLoadBundle2019)target;
            GUIStyle newstyle = new GUIStyle(GUI.skin.GetStyle("HelpBox"));
            newstyle.richText = true;
            newstyle.fontSize = 12;
            EditorGUIUtility.labelWidth = 94;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"<color=green>Bundle Source</color>", newstyle, GUILayout.Height(20), GUILayout.Width(93)))
            {
                string path = EditorUtility.OpenFilePanelWithFilters("Select Asset Bundle", "", filters);
                if (path.Length != 0)
                {
                    GUIUtility.keyboardControl = 0;
                    LoadBundleScript.AssetSource = path;
                }
            }
            LoadBundleScript.AssetSource = EditorGUILayout.TextField("", LoadBundleScript.AssetSource);
            EditorGUILayout.EndHorizontal();

            LoadBundleScript.Name = EditorGUILayout.TextField(" Bundle Name", LoadBundleScript.Name);

            EditorGUILayout.Space();
            LoadBundleScript.ShaderUsage = EditorGUILayout.Foldout(LoadBundleScript.ShaderUsage, "Shaders used in Asset", true);

            if (LoadBundleScript.ShaderUsage)
            {
                if (LoadBundleScript.DidAssetLoad)
                {
                    if (ShaderUsageIn == null)
                    {
                        (ShaderUsageIn, MaterialUsageIn) = LoadBundleScript.getShaderUsage();
                    }

                    EditorGUILayout.LabelField($"<color=cyan><b>{ShaderUsageIn.Count}</b> different shaders</color> were found:", newstyle);

                    for (int i = 0; i < ShaderUsageIn.Count; i++)
                    {
                        string temp = String.Join("\n\t", MaterialUsageIn[i]);
                        EditorGUILayout.LabelField($"<color=cyan><b>{ShaderUsageIn[i]}</b></color>\n\t{temp}", newstyle);
                    }
                }
                else EditorGUILayout.LabelField("Load the Bundle on Play Mode!", newstyle);
            }
            else if (ShaderUsageIn != null || MaterialUsageIn != null)
            {
                (ShaderUsageIn, MaterialUsageIn) = (null, null);
            }
        }
    }
}