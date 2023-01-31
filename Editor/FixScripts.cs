#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class FixScripts : EditorWindow
    {
        private readonly string YAML_header = "%YAML 1.1";
        private readonly string MonoBehaviour_header = "MonoBehaviour:";
        private readonly string separator = "--- !u!";
        private readonly string scriptguidflag = "  m_Script:";

        private string MonoScriptFolder;
        private string[] ScriptsFullNames;
        private Dictionary<string, string> OldScriptsGUIDs;
        private Dictionary<string, (string, string)> NewScriptsGUIDs;
        private string[] OldScripts_NoNewMatch;
        private SortedDictionary<string, int[]> ScriptsFixStats;
        private Dictionary<string, int[]> OldGUID_Stats;
        private Dictionary<string, int[]> NewGUID_Stats;
        private int fixedFilesCount;

        private static FACSGUIStyles FacsGUIStyles;
        private DefaultAsset selectedFolder;
        private string output_print;

        [MenuItem("FACS Utils/Repair Avatar/Fix Scripts", false, 1001)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(FixScripts), false, "Fix Scripts", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) FacsGUIStyles = new FACSGUIStyles();
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField($"<color=cyan><b>Fix Scripts</b></color>\n\n" +
                $"Scans the selected folder and subfolders, and assigns the correct scripts to " +
                $".prefab, .controller and .asset files.\n", FacsGUIStyles.helpbox);
            
            EditorGUI.BeginChangeCheck();
            selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField(selectedFolder, typeof(DefaultAsset), false, GUILayout.Height(50));
            if (EditorGUI.EndChangeCheck() || (!selectedFolder && !String.IsNullOrEmpty(output_print))) NullVars();

            if (selectedFolder && GUILayout.Button("Run Fix!", FacsGUIStyles.button, GUILayout.Height(40))) RunFix();

            if (!String.IsNullOrEmpty(output_print))
            {
                FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(output_print, FacsGUIStyles.helpbox);
            }
        }

        private void RunFix()
        {
            MonoScriptFolder = AssetDatabase.GetAssetPath(selectedFolder) + "/.Scripts";
            output_print = "";
            if (HasMonoScripts())
            {
                if (GetNewScripts())
                {
                    AssetDatabase.SaveAssets();
                    FixFiles();
                    GenerateResults();
                    Cleanup();
                    Debug.Log($"[<color=green>Fix Scripts</color>] Finished fixing scripts!\n");
                }
                else
                {
                    output_print = $"<color=orange>There are no matching scripts to replace the extracted/ripped ones.</color>\nPlease install:\n";
                    foreach (var script in OldScripts_NoNewMatch) output_print += $"   • {script}\n";
                    Debug.LogWarning($"[<color=green>Fix Scripts</color>] There are no matching scripts to replace.\n");
                }
            }
            else
            {
                Debug.LogWarning($"[<color=green>Fix Scripts</color>] There is no \".MonoScript\" folder inside the selected folder, or it doesn't contain any .meta file.\n");
            }
        }

        private bool HasMonoScripts()
        {
            if (!Directory.Exists(MonoScriptFolder)) return false;
            var metapaths = Directory.GetFiles(MonoScriptFolder, "*.meta", SearchOption.AllDirectories).Select(path => path.Replace(@"\","/")).ToArray();
            if (metapaths.Length == 0) return false;

            ScriptsFullNames = metapaths.Select(path => path.Replace(MonoScriptFolder+"/",""))
                .Select(p => p.Substring(p.IndexOf('/') + 1, p.Length-9-p.IndexOf('/')).Replace("/",".")).ToArray();

            OldScriptsGUIDs = new Dictionary<string, string>();
            for (int i = 0; i < metapaths.Length; i++)
            {
                var metafile = File.ReadLines(metapaths[i]);
                foreach (var line in metafile)
                {
                    if (line.StartsWith("guid:")) { OldScriptsGUIDs.Add(ScriptsFullNames[i], line.Substring(6)); break; }
                }
            }
            ScriptsFullNames = ScriptsFullNames.OrderBy(s => s).ToArray();
            return true;
        }

        private bool GetNewScripts()
        {
            bool hassome = false;
            var alltypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
                .Where(type => ScriptsFullNames.Contains(type.FullName)).ToList();
            NewScriptsGUIDs = new Dictionary<string, (string, string)>();
            var go = EditorUtility.CreateGameObjectWithHideFlags("temp go", HideFlags.HideAndDontSave);

            foreach (var type in alltypes)
            {
                UnityEngine.Object temp = null;
                Component temp2 = null;
                if (type.IsSubclassOf(typeof(ScriptableObject))) temp = ScriptableObject.CreateInstance(type.Name);
                else if (type.IsSubclassOf(typeof(MonoBehaviour))) { temp2 = go.AddComponent(type); temp = go; }
                if (temp)
                {
                    var temp3 = EditorUtility.CollectDependencies(new UnityEngine.Object[] { temp });
                    foreach (var dependency in temp3)
                    {
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(dependency, out string guid, out long localid))
                        {
                            NewScriptsGUIDs.Add(type.FullName, (guid, localid.ToString()));
                            hassome = true;
                        }
                    }
                    if (temp2) DestroyImmediate(temp2);
                    else DestroyImmediate(temp);
                }
            }

            DestroyImmediate(go);
            OldScripts_NoNewMatch = ScriptsFullNames.Where(typeFN => !NewScriptsGUIDs.ContainsKey(typeFN))
                .Select(type => type.Contains(".") ? type.Substring(type.LastIndexOf(".")+1) : type).OrderBy(s => s).ToArray();
            return hassome;
        }

        private void FixFiles()
        {
            fixedFilesCount = 0;
            ScriptsFixStats = new SortedDictionary<string, int[]>();
            OldGUID_Stats = new Dictionary<string, int[]>();
            NewGUID_Stats = new Dictionary<string, int[]>();
            foreach (var script in NewScriptsGUIDs.Keys)
            {
                int[] stats = new int[2];
                ScriptsFixStats.Add(script.Contains(".") ? script.Substring(script.LastIndexOf(".")+1) : script, stats);
                OldGUID_Stats.Add(OldScriptsGUIDs[script], stats);
                NewGUID_Stats.Add(NewScriptsGUIDs[script].Item1+","+ NewScriptsGUIDs[script].Item2, stats);
            }

            string[] filePaths = Directory.GetFiles(AssetDatabase.GetAssetPath(selectedFolder), "*.asset", SearchOption.AllDirectories);
            string[] filePaths1 = Directory.GetFiles(AssetDatabase.GetAssetPath(selectedFolder), "*.controller", SearchOption.AllDirectories);
            string[] filePaths2 = Directory.GetFiles(AssetDatabase.GetAssetPath(selectedFolder), "*.prefab", SearchOption.AllDirectories);
            filePaths = filePaths.Concat(filePaths1).Concat(filePaths2).Select(path => path.Replace(@"\", "/"))
                .Where(path => File.ReadLines(path).First().Contains(YAML_header)).ToArray();

            bool refresh = false;
            int progress = 0; float progressTotal = filePaths.Length;
            foreach (string filePath in filePaths)
            {
                progress++;
                EditorUtility.DisplayProgressBar("Fix VRC3 Scripts", $"Processing file: {Path.GetFileName(filePath)}", progress / progressTotal);
                if (FixFile(filePath)) refresh = true;
            }
            if (refresh) AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
        }

        private bool FixFile(string filepath)
        {
            bool fixedfile = false;
            Dictionary<long, string> linestoreplace = new Dictionary<long, string>();
            using (StreamReader reader = new StreamReader(filepath))
            {
                long lineN = 0;
                bool inMonoBehaviour = false;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (inMonoBehaviour)
                    {
                        if (line.StartsWith(separator)) inMonoBehaviour = false;
                        else if (line.StartsWith(scriptguidflag) && line.Contains("fileID:") && line.Contains("guid:"))
                        {
                            var guid_fileid = Extract_GUID_FileID(line);
                            var guidfileid = guid_fileid.Item1 + "," + guid_fileid.Item2;
                            if (OldGUID_Stats.ContainsKey(guid_fileid.Item1))
                            {
                                var scriptname = OldScriptsGUIDs.FirstOrDefault(x => x.Value == guid_fileid.Item1).Key;
                                linestoreplace.Add(lineN, $"  m_Script: {{fileID: {NewScriptsGUIDs[scriptname].Item2}, guid: {NewScriptsGUIDs[scriptname].Item1}, type: 3}}");
                                OldGUID_Stats[guid_fileid.Item1][0]++;
                            }
                            else if (NewGUID_Stats.ContainsKey(guidfileid)) NewGUID_Stats[guidfileid][1]++;
                        }
                    }
                    else if (line.StartsWith(MonoBehaviour_header)) inMonoBehaviour = true;

                    lineN++;
                }
            }
            if (linestoreplace.Count > 0)
            {
                fixedfile = true;
                var tempfilepath = FileUtil.GetUniqueTempPathInProject();
                using (StreamWriter tempfile = new StreamWriter(tempfilepath))
                {
                    using (StreamReader reader = new StreamReader(filepath))
                    {
                        long lineN = 0;
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            if (linestoreplace.ContainsKey(lineN)) tempfile.WriteLine(linestoreplace[lineN]);
                            else tempfile.WriteLine(line);
                            lineN++;
                        }
                    }
                }
                FileUtil.DeleteFileOrDirectory(filepath);
                FileUtil.MoveFileOrDirectory(tempfilepath, filepath);
                fixedFilesCount++;
            }
            return fixedfile;
        }

        private void GenerateResults()
        {
            string end = $"";
            output_print = $"Results:\n   • <color=green>Fixed files:</color> {fixedFilesCount}\n";
            foreach (KeyValuePair<string, int[]> valuepair in ScriptsFixStats)
            {
                if (valuepair.Value[0] == 0) end += $"   • {valuepair.Key}:  {valuepair.Value[1]} already working\n";
                else output_print += $"   • <color=cyan>{valuepair.Key}:</color>  {valuepair.Value[0]} fixed, {valuepair.Value[1]} already working\n";
            }
            if (OldScripts_NoNewMatch.Length > 0)
            {
                end += $"<color=grey> Scripts not found:\n";
                foreach (var nomatch in OldScripts_NoNewMatch) end += $"   • {nomatch}\n";
                end += $"</color>";
            }
            output_print += end;
        }

        private (string, string) Extract_GUID_FileID(string line)
        {
            string tmp = line.Substring(line.IndexOf("fileID:")+8);
            string fileid = tmp.Substring(0, tmp.IndexOf(","));
            string tmp2 = tmp.Substring(tmp.IndexOf("guid:")+6);
            string guid = tmp2.Substring(0, tmp2.IndexOf(","));
            return (guid, fileid);
        }

        private void Cleanup()
        {
            MonoScriptFolder = null;
            ScriptsFullNames = null;
            OldScriptsGUIDs = null;
            NewScriptsGUIDs = null;
            OldScripts_NoNewMatch = null;
            ScriptsFixStats = null;
            OldGUID_Stats = null;
            NewGUID_Stats = null;
            fixedFilesCount = default;
        }

        public void OnDestroy()
        {
            NullVars();
            selectedFolder = null;
            FacsGUIStyles = null;
        }

        private void NullVars()
        {
            MonoScriptFolder = null;
            ScriptsFullNames = null;
            OldScriptsGUIDs = null;
            NewScriptsGUIDs = null;
            OldScripts_NoNewMatch = null;
            ScriptsFixStats = null;
            OldGUID_Stats = null;
            NewGUID_Stats = null;
            fixedFilesCount = default;
            output_print = null;
        }
    }
}
#endif