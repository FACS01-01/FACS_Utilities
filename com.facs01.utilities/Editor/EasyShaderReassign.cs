#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace FACS01.Utilities
{
    internal class EasyShaderReassign : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Easy Shader Reassign]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private DefaultAsset folderWithMaterials;
        private Vector2 scrollPos;
        private ShaderMatCollection[] workingShaders = null;
        private ShaderMatCollection[] missingShaders = null;
        private List<ShaderMatCollection> groupedShaders = null;
        private string groupedShadersKey = "";
        private ShaderDropDownSelector SDS = null;
        private Stopwatch timer = new();
        private float progressbarTotal = 1;
        private int progressbar = 0;
        private bool registerUndo = true;

        [MenuItem("FACS Utils/Repair Project/Easy Shader Reassign", false, 1050)]
        [MenuItem("FACS Utils/Shader+Material/Easy Shader Reassign", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(EasyShaderReassign), false, "Easy Shader Reassign", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) FacsGUIStyles = new();
            FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField($"<color=cyan><b>Easy Shader Reassign</b></color>\n\n" +
                $"Scans the selected Materials folder and helps you assign the correct shaders to broken materials.\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            folderWithMaterials = (DefaultAsset)EditorGUILayout.ObjectField(folderWithMaterials, typeof(DefaultAsset), false, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() || (!folderWithMaterials && (workingShaders != null || missingShaders != null))) NullVars();
            if (folderWithMaterials && GUILayout.Button("Scan!", FacsGUIStyles.Button, GUILayout.Height(40))) GetMaterials();

            if (workingShaders != null || missingShaders != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(" Group Shaders ", GUI.skin.label, GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent(" Group Shaders ")).x));
                EditorGUI.BeginChangeCheck();
                var searchfilter = EditorGUILayout.TextField(groupedShadersKey, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck() && searchfilter.Trim() != groupedShadersKey.Trim())
                {
                    groupedShadersKey = searchfilter;
                    if (string.IsNullOrEmpty(groupedShadersKey.Trim())) { groupedShaders = null; SDS = null; timer.Reset(); }
                    else timer.Restart();
                }
                EditorGUILayout.EndHorizontal();

                bool newShader = false;
                if (groupedShaders != null)
                {
                    FacsGUIStyles.Helpbox.alignment = TextAnchor.UpperCenter;
                    EditorGUILayout.LabelField($"<b>Grouped shaders: {groupedShaders.Count}</b>", FacsGUIStyles.Helpbox, GUILayout.Height(21));
                    SDS.Display(FacsGUIStyles.DDB);
                    scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                    FacsGUIStyles.Helpbox.alignment = TextAnchor.UpperLeft; FacsGUIStyles.Helpbox.wordWrap = false;
                    GUILayout.Label($"    " + string.Join("\n    ", groupedShaders.Select(v =>$"<color={(v.isMissing ? "cyan": "orange")}>{v.oldShader_Name}</color>")),
                        FacsGUIStyles.Helpbox, GUILayout.Height(16*groupedShaders.Count+8.5f));
                    FacsGUIStyles.Helpbox.wordWrap = true;
                    EditorGUILayout.EndScrollView();
                    if (SDS.m_Shader != null)
                    {
                        foreach (var smc in groupedShaders)
                        {
                            smc.sds.m_Shader = SDS.m_Shader;
                        }
                        groupedShaders = null; SDS = null; groupedShadersKey = ""; scrollPos = default;
                        Repaint();
                    }
                    return;
                }

                EditorGUILayout.GetControlRect(false, -1);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleLeft;
                if (missingShaders != null) newShader = DisplaySMCArray(missingShaders, true);
                if (workingShaders != null) newShader = DisplaySMCArray(workingShaders, false) || newShader;
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter;
                EditorGUILayout.EndScrollView();

                if (GUITools.ToggleButton(registerUndo, "Register Undo", GUILayout.Height(25))) registerUndo = !registerUndo;

                if (newShader && GUILayout.Button("Apply Shaders!", FacsGUIStyles.Button, GUILayout.Height(40)))
                {
                    progressbarTotal = 0; progressbar = 0;
                    if (missingShaders != null) progressbarTotal += missingShaders.Where(v => v.sds.m_Shader != null).Select(v => v.materials.Where(m => m.toggle && m.mat).Count()).Sum();
                    if (workingShaders != null) progressbarTotal += workingShaders.Where(v => v.sds.m_Shader != null).Select(v => v.materials.Where(m => m.toggle && m.mat).Count()).Sum();
                    if (missingShaders != null) ApplyShaders(missingShaders);
                    if (workingShaders != null) ApplyShaders(workingShaders);
                    if (registerUndo)
                    {
                        EditorUtility.DisplayProgressBar("FACS Utilities - Easy Shader Reassign", $"Collapsing Undo operations into one. This could take a bit...", 1);
                        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                    }
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.SaveAssets();
                    Logger.Log(RichToolName + $" Finished applying shaders to <b>{(int)progressbarTotal}</b> materials!");
                    GetMaterials();
                }
            }
        }

        private void ApplyShaders(ShaderMatCollection[] SMCA)
        {
            foreach (var smc in SMCA)
            {
                var sh = smc.sds.m_Shader;
                if (sh == null) continue;
                foreach (var mat in smc.materials)
                {
                    if (!mat.toggle || !mat.mat) continue;
                    EditorUtility.DisplayProgressBar("FACS Utilities - Easy Shader Reassign", $"Reassigning {mat.name} to {sh.name}", progressbar/progressbarTotal);
                    progressbar++;
                    
                    if (registerUndo) Undo.RecordObject(mat.mat, "Shader Reassign");
                    mat.mat.shader = sh;
                    if (!registerUndo) EditorUtility.SetDirty(mat.mat);
                }
            }
        }

        private bool DisplaySMCArray(ShaderMatCollection[] SMCA, bool isMissing)
        {
            bool newShader = false;
            var color = isMissing ? "cyan" : "orange";
            var isOld = isMissing ? "Old " : "";
            foreach (var smc in SMCA)
            {
                EditorGUILayout.LabelField($"<b>{isOld}Shader: <color={color}>{smc.oldShader_Name}</color></b>", FacsGUIStyles.Helpbox);
                EditorGUILayout.BeginHorizontal();
                smc.sds.Display(FacsGUIStyles.DDB);
                if (smc.sds.m_Shader != null)
                {
                    newShader = true;
                    if (GUILayout.Button("Reset", FacsGUIStyles.ButtonSmall, GUILayout.Height(18), GUILayout.Width(43))) smc.sds.m_Shader = null;
                }
                EditorGUILayout.EndHorizontal();
                smc.materialFoldout = GUILayout.Toggle(smc.materialFoldout, $"Used on {smc.materials.Count} materials", FacsGUIStyles.Foldout);
                if (smc.materialFoldout)
                {
                    FacsGUIStyles.ButtonSmall.alignment = TextAnchor.MiddleLeft;
                    foreach (var matToggle in smc.materials)
                    {
                        if (!matToggle.mat) continue;
                        EditorGUILayout.BeginHorizontal();
                        matToggle.toggle = EditorGUILayout.Toggle(matToggle.toggle,GUILayout.Width(14), GUILayout.Height(18));
                        if (GUITools.TintedButton(Color.gray, matToggle.name, FacsGUIStyles.ButtonSmall, GUILayout.Height(17)))
                        {
                            Selection.activeObject = matToggle.mat;
                            EditorGUIUtility.PingObject(matToggle.mat);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    FacsGUIStyles.ButtonSmall.alignment = TextAnchor.MiddleLeft;
                }
                EditorGUILayout.Space(20);
            }
            return newShader;
        }

        private void GetMaterials()
        {
            NullVars();
            var workingShadersDic = new Dictionary<string, ShaderMatCollection>();
            var missingShadersDic = new Dictionary<string, ShaderMatCollection>();
            ScanMaterials(workingShadersDic, missingShadersDic);
            if (workingShadersDic.Count > 0)
            {
                workingShaders = workingShadersDic.Select(p => p.Value).OrderBy(smc => smc.oldShader_Name).ToArray();
            }
            if (missingShadersDic.Count > 0)
            {
                missingShaders = FillMissing(missingShadersDic);
            }
        }

        private ShaderMatCollection[] FillMissing(Dictionary<string, ShaderMatCollection> missingShadersDic)
        {
            string matsPath = AssetDatabase.GetAssetPath(folderWithMaterials);
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
            if (shadersPath == "")
            {
                Logger.LogWarning($"{RichToolName} {Logger.TypeTag}Materials{Logger.EndTag} with missing {Logger.TypeTag}Shaders{Logger.EndTag} found, but dummy {Logger.TypeTag}Shaders{Logger.EndTag} folder (\".Shader\") not found in: \"{AssetsPath}\"");
                return missingShadersDic.Select(p => p.Value).ToArray();
            }

            List<(string guid, string shaderName, string file)> guid_name = new();

            string[] metaFiles = Directory.GetFiles(shadersPath, "*.shader.meta", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).ToArray();
            string[] shaderFiles = Directory.GetFiles(shadersPath, "*.shader", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).ToArray();
            foreach (string path in metaFiles)
            {
                var shaderpath = path[0..^5];
                if (!shaderFiles.Contains(shaderpath)) continue;

                string GUID = "";
                using (StreamReader sr = new(path))
                {
                    while (sr.Peek() != -1)
                    {
                        string line = sr.ReadLine();
                        if (line.StartsWith("guid: "))
                        {
                            GUID = line[6..];
                            break;
                        }
                    }
                }
                if (GUID == "") continue;

                string shaderName = "Unknown Name";
                string header = File.ReadLines(shaderpath).First();
                if (header.StartsWith("Shader \""))
                {
                    int pFrom = header.IndexOf("Shader \"") + "Shader \"".Length;
                    int pTo = header.LastIndexOf("\"");
                    shaderName = header.Substring(pFrom, pTo - pFrom);
                }
                guid_name.Add((GUID, shaderName, shaderpath));
            }

            if (guid_name.Count == 0) return missingShadersDic.Select(p => p.Value).ToArray();

            var newmissings = new List<ShaderMatCollection>();
            foreach (var (guid, shaderName, file) in guid_name)
            {
                foreach (var key in missingShadersDic.Keys)
                {
                    if (key.StartsWith(guid))
                    {
                        var value = missingShadersDic[key];
                        value.oldShader_Name = shaderName;
                        value.oldShader_FilePath = file;
                        newmissings.Add(value);
                        missingShadersDic.Remove(key);
                        break;
                    }
                }
                if (missingShadersDic.Count == 0) break;
            }

            if (newmissings.Count > 1) newmissings.Sort((x,y)=>string.Compare(x.oldShader_Name, y.oldShader_Name));
            if (missingShadersDic.Count > 0) newmissings = newmissings.Concat(missingShadersDic.Select(v => v.Value)).ToList();
            return newmissings.ToArray();
        }

        private void ScanMaterials(Dictionary<string, ShaderMatCollection> workingShadersDic, Dictionary<string, ShaderMatCollection> missingShadersDic)
        {
            string[] materialPaths = Directory.GetFiles(AssetDatabase.GetAssetPath(folderWithMaterials), "*.mat", SearchOption.AllDirectories);
            if (materialPaths.Length > 0)
            {
                materialPaths = materialPaths.Select(p => p.Replace('\\', '/')).ToArray();
                foreach (string matpath in materialPaths)
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matpath);
                    var shader = mat.shader;
                    string shaderName = shader.name;
                    string shaderGUID = ""; string shaderFileID = "";
                    if (shaderName == "Hidden/InternalErrorShader")
                    {
                        using (StreamReader sr = new(matpath))
                        {
                            while (sr.Peek() > 0)
                            {
                                string line = sr.ReadLine();
                                if (line.StartsWith("  m_Shader: "))
                                {
                                    if (line.Contains("guid: "))
                                    {
                                        int pFrom = line.IndexOf("guid: ") + "guid: ".Length;
                                        int pTo = line.LastIndexOf(", type:");
                                        shaderGUID = line[pFrom..pTo];
                                        pFrom = line.IndexOf("fileID: ") + "fileID: ".Length;
                                        pTo = line.LastIndexOf(", guid:");
                                        shaderFileID = line[pFrom..pTo];
                                    }
                                    else shaderGUID = "NoGUID_" + Guid.NewGuid().ToString();
                                }
                            }
                        }
                        var key = shaderGUID + "," + shaderFileID;
                        if (!missingShadersDic.TryGetValue(key, out var smc))
                        {
                            smc = new();
                            missingShadersDic[key] = smc;
                        }
                        smc.materials.Add(new(mat));
                    }
                    else
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out shaderGUID, out long localID);
                        shaderFileID = localID.ToString();
                        var key = shaderGUID + "," + shaderFileID;
                        if (!workingShadersDic.TryGetValue(key, out var smc))
                        {
                            smc = new(shaderName, false);
                            workingShadersDic[key] = smc;
                        }
                        smc.materials.Add(new(mat));
                    }
                }
            }
            else
            {
                Logger.LogWarning($"{RichToolName} No {Logger.TypeTag}Material{Logger.EndTag} found in selected folder: \"{AssetDatabase.GetAssetPath(folderWithMaterials)}\"");
            }
        }

        private void UpdateGroupedShaders()
        {
            var key = groupedShadersKey.Trim().ToLower();
            if (string.IsNullOrEmpty(key)) { groupedShaders = null; return; }
            var L = new List<ShaderMatCollection>();
            if (missingShaders != null)
            {
                foreach (var smc in missingShaders)
                {
                    if (smc.oldShader_Name.ToLower().Contains(key)) L.Add(smc);
                }
            }
            if (workingShaders != null)
            {
                foreach (var smc in workingShaders)
                {
                    if (smc.oldShader_Name.ToLower().Contains(key)) L.Add(smc);
                }
            }
            if (L.Count > 0) groupedShaders = L;
            else groupedShaders = null;
        }

        private void Update()
        {
            if (timer.IsRunning && timer.ElapsedMilliseconds > 500)
            {
                timer.Reset();
                UpdateGroupedShaders();
                if (groupedShaders != null) SDS = new();
                else SDS = null;
                this.Repaint();
            }
        }

        private void OnDestroy()
        {
            FacsGUIStyles = null;
            folderWithMaterials = null;
            registerUndo = true;
            NullVars();
        }

        private void NullVars()
        {
            scrollPos = default;
            workingShaders = null;
            missingShaders = null;
            groupedShaders = null;
            groupedShadersKey = "";
            SDS = null;
            timer = new();
        }

        private class ShaderMatCollection
        {
            internal ShaderDropDownSelector sds = new();
            internal List<MaterialToggle> materials = new();
            internal bool materialFoldout = false;

            internal string oldShader_FilePath = "";
            internal string oldShader_Name;
            internal bool isMissing = false;

            internal ShaderMatCollection(string oldshader_Name = "UNKNOWN", bool ismissing = true)
            {
                oldShader_Name = oldshader_Name; isMissing = ismissing;
            }

            internal class MaterialToggle
            {
                public Material mat;
                public string name;
                public bool toggle = true;

                public MaterialToggle(Material material)
                {
                    mat = material;
                    name = $" {material.name}";
                }
            }
        }
    }
}
#endif