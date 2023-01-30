#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class EasyShaderReassign : EditorWindow
    {
        private DefaultAsset folderWithMaterials;
        private static FACSGUIStyles FacsGUIStyles;
        private List<(string, string, List<(Material, string)>)> OldShaders;
        private bool[] OldFoldOuts;
        private ShaderDropDownSelector[] OldDropDowns;
        private Tuple<string, string, string>[] OldResults;
        private List<(string, string, List<(Material, string)>)> GoodShaders;
        private bool[] GoodFoldOuts;
        private ShaderDropDownSelector[] GoodDropDowns;
        private Tuple<string, string, string>[] GoodResults;
        private Vector2 scrollPos;

        [MenuItem("FACS Utils/Repair Avatar/Easy Shader Reassign", false, 1000)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(EasyShaderReassign), false, "Easy Shader Reassign", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField($"<color=cyan><b>Easy Shader Reassign</b></color>\n\n" +
                $"Scans the selected Materials folder and helps you assign the correct shaders to broken materials.\n", FacsGUIStyles.helpbox);

            EditorGUI.BeginChangeCheck();
            folderWithMaterials = (DefaultAsset)EditorGUILayout.ObjectField(folderWithMaterials, typeof(DefaultAsset), false, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() || (!folderWithMaterials && (OldShaders != null || GoodShaders != null))) NullVars();
            if (folderWithMaterials && GUILayout.Button("Scan!", FacsGUIStyles.button, GUILayout.Height(40))) GetMaterials();

            if (OldShaders != null || GoodShaders != null)
            {
                bool newShader = false;
                FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleLeft;
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                if (OldShaders != null)
                {
                    for (int i = 0; i < OldResults.Length; i++)
                    {
                        EditorGUILayout.LabelField(OldResults[i].Item1, FacsGUIStyles.helpbox);
                        EditorGUILayout.BeginHorizontal();
                        OldDropDowns[i].Display(FacsGUIStyles.dropdownbutton);
                        if (OldDropDowns[i].m_Shader != null)
                        {
                            newShader = true;
                            if (GUILayout.Button("Reset", FacsGUIStyles.buttonSmall, GUILayout.Height(18), GUILayout.Width(43))) OldDropDowns[i].m_Shader = null;
                        }
                        EditorGUILayout.EndHorizontal();
                        OldFoldOuts[i] = EditorGUILayout.Foldout(OldFoldOuts[i], OldResults[i].Item2, true);
                        if (OldFoldOuts[i])
                        {
                            EditorGUILayout.LabelField(OldResults[i].Item3, FacsGUIStyles.helpbox);
                        }
                        EditorGUILayout.Space(20);
                    }
                }
                if (GoodShaders != null)
                {
                    for (int i = 0; i < GoodResults.Length; i++)
                    {
                        EditorGUILayout.LabelField(GoodResults[i].Item1, FacsGUIStyles.helpbox);
                        EditorGUILayout.BeginHorizontal();
                        GoodDropDowns[i].Display(FacsGUIStyles.dropdownbutton);
                        if (GoodDropDowns[i].m_Shader != null && GUILayout.Button("Reset", FacsGUIStyles.buttonSmall, GUILayout.Height(18), GUILayout.Width(43)))
                        {
                            GoodDropDowns[i].m_Shader = null;
                        }
                        if (GoodDropDowns[i].m_Shader != null) newShader = true;
                        EditorGUILayout.EndHorizontal();
                        GoodFoldOuts[i] = EditorGUILayout.Foldout(GoodFoldOuts[i], GoodResults[i].Item2, true);
                        if (GoodFoldOuts[i])
                        {
                            EditorGUILayout.LabelField(GoodResults[i].Item3, FacsGUIStyles.helpbox);
                        }
                        EditorGUILayout.Space(20);
                    }
                }
                EditorGUILayout.EndScrollView();
                FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;
                if (newShader && GUILayout.Button("Apply Shaders!", FacsGUIStyles.button, GUILayout.Height(40)))
                {
                    if (OldShaders != null)
                    {
                        for (int i = 0; i < OldShaders.Count; i++)
                        {
                            if (OldDropDowns[i].m_Shader != null)
                            {
                                foreach (var mat in OldShaders[i].Item3)
                                {
                                    Material editedMat = mat.Item1;
                                    Undo.RecordObject(editedMat, "Shader Reassign");
                                    editedMat.shader = OldDropDowns[i].m_Shader;
                                }
                            }
                        }
                    }
                    if (GoodShaders != null)
                    {
                        for (int i = 0; i < GoodShaders.Count; i++)
                        {
                            if (GoodDropDowns[i].m_Shader != null)
                            {
                                foreach (var mat in GoodShaders[i].Item3)
                                {
                                    Material editedMat = mat.Item1;
                                    Undo.RecordObject(editedMat, "Shader Reassign");
                                    editedMat.shader = GoodDropDowns[i].m_Shader;
                                }
                            }
                        }
                    }
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                    Debug.Log($"[<color=green>Easy Shader Reassign</color>] Finished applying shaders!\n");
                    GetMaterials();
                }
            }
        }

        private void GetMaterials()
        {
            NullVars();
            OldShaders = new List<(string, string, List<(Material, string)>)>();
            GoodShaders = new List<(string, string, List<(Material, string)>)>();
            FindOldShaders(AssetDatabase.GetAssetPath(folderWithMaterials));
            ListMaterials();
            Cleanup();
        }

        private void Cleanup()
        {
            for (int i = 0; i < OldShaders.Count(); i++)
            {
                if (!OldShaders.ElementAt(i).Item3.Any())
                {
                    OldShaders.RemoveAt(i); i--;
                }
            }
            if (!OldShaders.Any())
            {
                OldShaders = null;
            }
            else
            {
                OldFoldOuts = new bool[OldShaders.Count];
                OldDropDowns = new ShaderDropDownSelector[OldShaders.Count];
                OldResults = new Tuple<string, string, string>[OldShaders.Count];
                for (int i = 0; i < OldDropDowns.Length; i++)
                {
                    OldDropDowns[i] = new ShaderDropDownSelector();
                }
                for (int i = 0; i < OldResults.Length; i++)
                {
                    OldResults[i] = Tuple.Create($"<b>Old Shader: <color=cyan>{OldShaders[i].Item2}</color></b>", $"Used on {OldShaders[i].Item3.Count} materials", $"    " + String.Join("\n    ", OldShaders[i].Item3.Select(o => o.Item2)));
                }
            }
            if (!GoodShaders.Any())
            {
                GoodShaders = null;
            }
            else
            {
                GoodFoldOuts = new bool[GoodShaders.Count];
                GoodDropDowns = new ShaderDropDownSelector[GoodShaders.Count];
                GoodResults = new Tuple<string, string, string>[GoodShaders.Count];
                for (int i = 0; i < GoodDropDowns.Length; i++)
                {
                    GoodDropDowns[i] = new ShaderDropDownSelector();
                }
                for (int i = 0; i < GoodResults.Length; i++)
                {
                    GoodResults[i] = Tuple.Create($"<b>Shader: <color=orange>{GoodShaders[i].Item2}</color></b>", $"Used on {GoodShaders[i].Item3.Count} materials", $"    " + String.Join("\n    ", GoodShaders[i].Item3.Select(o => o.Item2)));
                }
            }
        }

        private void ListMaterials()
        {
            string[] materialPaths = Directory.GetFiles(AssetDatabase.GetAssetPath(folderWithMaterials), "*.mat", SearchOption.AllDirectories);
            if (materialPaths.Length > 0)
            {
                for (int i = 0; i < materialPaths.Length; i++)
                {
                    materialPaths[i] = materialPaths[i].Replace('\\', '/');
                }
                foreach (string matpath in materialPaths)
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matpath);
                    string mat_shadername = mat.shader.name;
                    StreamReader sr = new StreamReader(matpath);
                    bool addedtolist = false;
                    string GUID = "";
                    string OriginalShader = @"";
                    while (sr.Peek() > 0)
                    {
                        string line = sr.ReadLine();
                        if (line.StartsWith("  m_Shader: "))
                        {
                            if (line.Contains("guid: "))
                            {
                                int pFrom = line.IndexOf("guid: ") + "guid: ".Length;
                                int pTo = line.LastIndexOf(", type:");
                                GUID = line.Substring(pFrom, pTo - pFrom);
                            }
                            else
                            {
                                GUID = "NoGUID";
                            }
                        }
                        else if (line.StartsWith("    OriginalShader:"))
                        {
                            OriginalShader = line.Substring(20);
                        }
                        else if (line.StartsWith("  m_SavedProperties:"))
                        {
                            break;
                        }
                    }
                    OriginalShader = Regex.Unescape(OriginalShader).Replace("\"","");
                    sr.Close();
                    if (mat_shadername == "Hidden/InternalErrorShader")
                    {
                        if (OldShaders.Any()) foreach (var oldshader in OldShaders)
                        {
                            if (GUID == oldshader.Item1)
                            {
                                oldshader.Item3.Add((mat, Path.GetFileNameWithoutExtension(matpath)));
                                addedtolist = true;
                                break;
                            }
                            else if (OriginalShader != "" && OriginalShader == oldshader.Item2)
                            {
                                oldshader.Item3.Add((mat, Path.GetFileNameWithoutExtension(matpath)));
                                addedtolist = true;
                                break;
                            }
                        }
                        if (!addedtolist)
                        {
                            if (OriginalShader != "")
                            {
                                OldShaders.Add(("UnknownGUID", OriginalShader, new List<(Material, string)> { (mat, Path.GetFileNameWithoutExtension(matpath)) }));
                                addedtolist = true;
                            }
                            else if (GUID == "NoGUID")
                            {
                                OldShaders.Add(("NoGUID", "Internal Error Shader (no shader)", new List<(Material, string)> { (mat, Path.GetFileNameWithoutExtension(matpath)) }));
                                addedtolist = true;
                            }
                            else
                            {
                                OldShaders.Add((GUID, "Unknown Shader", new List<(Material, string)> { (mat, Path.GetFileNameWithoutExtension(matpath)) }));
                                addedtolist = true;
                            }
                        }
                    }

                    if (mat_shadername != "Hidden/InternalErrorShader" && GoodShaders.Any())
                    {
                        foreach (var goodshader in GoodShaders)
                        {
                            if (mat_shadername == goodshader.Item2)
                            {
                                goodshader.Item3.Add((mat, Path.GetFileNameWithoutExtension(matpath)));
                                addedtolist = true;
                                break;
                            }
                        }
                    }
                    if (!addedtolist && mat_shadername != "Hidden/InternalErrorShader")
                    {
                        GoodShaders.Add((GUID, mat_shadername, new List<(Material, string)> { (mat, Path.GetFileNameWithoutExtension(matpath)) }));
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[<color=green>Easy Shader Reassign</color>] No material found in selected folder: {AssetDatabase.GetAssetPath(folderWithMaterials)}\n");
            }
            
        }

        private void FindOldShaders(string matsPath)
        {
            string AssetsPath = matsPath.Substring(0, matsPath.LastIndexOf("/")) + "/";
            string[] FoldersInAssets = Directory.GetDirectories(AssetsPath);
            string shadersPath = "";
            foreach (string dir in FoldersInAssets)
            {
                if (dir.EndsWith("/.Shader") || dir.EndsWith("/Shader"))
                {
                    shadersPath = dir; break;
                }
            }
            if (shadersPath != "")
            {
                string[] metaFiles = Directory.GetFiles(shadersPath, "*.shader.meta", SearchOption.AllDirectories);
                string[] shaderFiles = Directory.GetFiles(shadersPath, "*.shader", SearchOption.AllDirectories);
                for (int i = 0; i < metaFiles.Length; i++)
                {
                    metaFiles[i] = metaFiles[i].Replace('\\', '/');
                }
                for (int i = 0; i < shaderFiles.Length; i++)
                {
                    shaderFiles[i] = shaderFiles[i].Replace('\\', '/');
                }
                foreach (string path in metaFiles)
                {
                    string GUID = "";
                    string shaderNamePath = "Unknown";
                    StreamReader sr = new StreamReader(path);
                    while (sr.Peek() > 0)
                    {
                        string line = sr.ReadLine();
                        if (line.StartsWith("guid: "))
                        {
                            GUID = line.Substring(6);
                            break;
                        }
                    }
                    sr.Close();

                    if (GUID != "" && shaderFiles.Contains(path.Substring(0, path.Length - 5)))
                    {
                        string header = File.ReadLines(path.Substring(0, path.Length - 5)).First();
                        if (header.StartsWith("Shader \""))
                        {
                            int pFrom = header.IndexOf("Shader \"") + "Shader \"".Length;
                            int pTo = header.LastIndexOf("\"");
                            shaderNamePath = header.Substring(pFrom, pTo - pFrom);
                        }
                        OldShaders.Add((GUID, shaderNamePath, new List<(Material,string)>()));
                    }
                }
                if (!OldShaders.Any())
                {
                    Debug.LogWarning($"[<color=green>Easy Shader Reassign</color>] No valid Shader found in dummy shaders folder: {shadersPath}\n");
                }
            }
            else
            {
                Debug.LogWarning($"[<color=green>Easy Shader Reassign</color>] Folder with dummy shaders not found in: {AssetsPath}\n");
            }
        }

        private void OnDestroy()
        {
            folderWithMaterials = null;
            FacsGUIStyles = null;
            NullVars();
        }

        private void NullVars()
        {
            OldShaders = null;
            OldFoldOuts = null;
            OldDropDowns = null;
            OldResults = null;
            GoodShaders = null;
            GoodFoldOuts = null;
            GoodDropDowns = null;
            GoodResults = null;
        }
    }
}
#endif