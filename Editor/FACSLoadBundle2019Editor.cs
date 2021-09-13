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
        private Vector2 scrollPos;
        public override void OnInspectorGUI()
        {
            FACSLoadBundle2019 LoadBundleScript = (FACSLoadBundle2019)target;

            LoadBundleScript.AssetSource = EditorGUILayout.TextField("Asset Source: ", LoadBundleScript.AssetSource);
            LoadBundleScript.Name = EditorGUILayout.TextField("Asset Name: ", LoadBundleScript.Name);
            EditorGUILayout.Space();
            LoadBundleScript.ShaderUsage = EditorGUILayout.Foldout(LoadBundleScript.ShaderUsage, "Shaders used in Asset", true);

            GUIStyle newstyle = new GUIStyle(GUI.skin.GetStyle("HelpBox"));
            newstyle.richText = true;
            newstyle.fontSize = 12;

            if (LoadBundleScript.ShaderUsage)
            {
                if (LoadBundleScript.DidAssetLoad)
                {
                    if (ShaderUsageIn == null)
                    {
                        (ShaderUsageIn, MaterialUsageIn) = LoadBundleScript.getShaderUsage();
                    }
                    
                    EditorGUILayout.TextArea($"<color=cyan><b>{ShaderUsageIn.Count}</b> different shaders</color> were found:", newstyle);
                    //scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                    for (int i = 0; i < ShaderUsageIn.Count; i++)
                    {
                        string temp = String.Join("\n\t", MaterialUsageIn[i]);
                        EditorGUILayout.TextArea($"<color=cyan><b>{ShaderUsageIn[i]}</b></color>\n\t{temp}", newstyle);
                    }
                    //EditorGUILayout.EndScrollView();
                }
                else GUILayout.TextArea("Load the Bundle on Play Mode!");
            }
        }
    }
}