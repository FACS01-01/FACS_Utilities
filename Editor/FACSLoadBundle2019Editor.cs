using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace FACS01.Utilities
{
    [CustomEditor(typeof(FACSLoadBundle2019))]
    public class FACSLoadBundle2019Editor : Editor
    {
        private static FACSGUIStyles FacsGUIStyles;
        private List<string> ShaderUsageIn;
        private List<List<string>> MaterialUsageIn;
        private readonly string[] filters = { "VRChat Files (vrca, vrcw)", "vrca,vrcw", "All files", "*" };
        public override void OnInspectorGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); FacsGUIStyles.helpboxSmall.alignment = TextAnchor.MiddleLeft; }

            FACSLoadBundle2019 LoadBundleScript = (FACSLoadBundle2019)target;
            EditorGUIUtility.labelWidth = 94;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Bundle Source", FacsGUIStyles.buttonSmall, GUILayout.Height(20), GUILayout.Width(93)))
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

                    EditorGUILayout.LabelField($"<color=cyan><b>{ShaderUsageIn.Count}</b> different shaders</color> were found:", FacsGUIStyles.helpboxSmall);

                    for (int i = 0; i < ShaderUsageIn.Count; i++)
                    {
                        string temp = String.Join("\n\t", MaterialUsageIn[i]);
                        EditorGUILayout.LabelField($"<color=cyan><b>{ShaderUsageIn[i]}</b></color>\n\t{temp}", FacsGUIStyles.helpboxSmall);
                    }
                }
                else EditorGUILayout.LabelField("Load the Bundle on Play Mode!", FacsGUIStyles.helpboxSmall);
            }
            else if (ShaderUsageIn != null || MaterialUsageIn != null)
            {
                (ShaderUsageIn, MaterialUsageIn) = (null, null);
            }
        }
    }
}