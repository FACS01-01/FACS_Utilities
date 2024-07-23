#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class FixScripts : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Fix Scripts]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private static DefaultAsset selectedFolder;
        private static Vector2 scrollView;
        private static bool deleteMetas;
        private static string outputMessage;

        internal static bool manual;
        internal static readonly List<ManualFix> manualFixes = new() { new() };
        private static float scriptFilterLabelSize = 0;
        private static string scriptFilter = "";

        private static System.Type[] availableMB = null;
        private static System.Type[] availableSO = null;
        private static System.Type[] availableSMB = null;
        private static string[] availableMBNames = null;
        private static string[] availableSONames = null;
        private static string[] availableSMBNames = null;

        [MenuItem("FACS Utils/Repair Project/Fix Scripts", false, 1051)]
        [MenuItem("FACS Utils/Script/Fix Scripts", false, 1101)]
        internal static void ShowWindow()
        {
            var window = GetWindow<FixScripts>(false, "Fix Scripts", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); scriptFilterLabelSize = FacsGUIStyles.Label.CalcSize(new GUIContent(" <b>Filter Scripts</b> ")).x; }
            var windowWidth = this.position.size.x;
            FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField($"<color=cyan><b>Fix Scripts</b></color>\n\n" +
                $"Scans the selected folder and subfolders, and assigns the correct scripts to " +
                $".anim, .controller, .prefab, .asset and .unity files.\n\n" +
                $"If no folder is selected, will scan the entire project's Assets folder.\n", FacsGUIStyles.Helpbox);

            scrollView = EditorGUILayout.BeginScrollView(scrollView);

            EditorGUILayout.BeginHorizontal();
            if (GUITools.ToggleButton(!manual, "Automatic", FacsGUIStyles.Button, GUILayout.Height(25), GUILayout.MaxWidth(windowWidth/2)))
                manual = false;
            if (GUITools.ToggleButton(manual, "Manual Fix", FacsGUIStyles.Button, GUILayout.Height(25), GUILayout.MaxWidth(windowWidth/2)))
                manual = true;
            EditorGUILayout.EndHorizontal();

            if (manual)
            {
                if (availableMB == null) InitAvailableScripts();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("<b>Filter Scripts</b>", FacsGUIStyles.Label, GUILayout.Width(scriptFilterLabelSize));
                EditorGUI.BeginChangeCheck();
                scriptFilter = EditorGUILayout.DelayedTextField(scriptFilter, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck()) FilterScripts();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("<b>+ Manual Fix</b>", FacsGUIStyles.Button, GUILayout.MaxWidth(windowWidth / 3))) manualFixes.Add(new());
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                for (int i = 0; i < manualFixes.Count; i++)
                {
                    if (DisplayManualFix(i)) i--;
                    EditorGUILayout.Space(10);
                }

                var rect = EditorGUILayout.BeginHorizontal();
                using (new Handles.DrawingScope(Color.gray))
                {
                    Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width, rect.y));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(10);
            }

            EditorGUILayout.LabelField("<b>Target Folder</b>", FacsGUIStyles.CenteredLabel);
            selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField(selectedFolder, typeof(DefaultAsset), false, GUILayout.Height(40));
            EditorGUILayout.Space(10);

            if (!manual)
            {
                deleteMetas = EditorGUILayout.ToggleLeft("Delete dummy scripts after reassign?", deleteMetas);
                EditorGUILayout.Space(10);
            }
            
            if (GUILayout.Button("Run Fix!", FacsGUIStyles.Button, GUILayout.Height(40))) RunFix();

            if (!string.IsNullOrEmpty(outputMessage))
            {
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(outputMessage, FacsGUIStyles.Helpbox);
            }

            EditorGUILayout.EndScrollView();
        }

        private static bool DisplayManualFix(int i)
        {
            var mf = manualFixes[i];

            EditorGUILayout.BeginHorizontal();
            var btn = GUITools.TintedButton(Color.red, "X", FacsGUIStyles.ButtonSmall, GUILayout.Width(20), GUILayout.Height(18));

            EditorGUILayout.BeginVertical();
            GUILayout.Space(2);
            mf.foldout = EditorGUILayout.Foldout(mf.foldout, $"Reassigning to: {(mf.newType != null ? $"<b>{mf.newType.Name}</b>" : "NONE")}", true, FacsGUIStyles.Foldout);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            if (btn)
            {
                manualFixes.RemoveAt(i);
                if (manualFixes.Count == 0) { manualFixes.Add(new()); return false; }
                return true;
            }
            if (!mf.foldout) return false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GUID:", FacsGUIStyles.Label, GUILayout.Width(scriptFilterLabelSize));
            EditorGUI.BeginChangeCheck();
            var newGUID = EditorGUILayout.TextField(mf.oldGUID);
            if (EditorGUI.EndChangeCheck())
            {
                mf.oldGUID = System.Text.RegularExpressions.Regex.Replace(newGUID, @"[^a-zA-Z0-9]", "").ToLower();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("File ID:", FacsGUIStyles.Label, GUILayout.Width(scriptFilterLabelSize));
            mf.oldFileID = EditorGUILayout.LongField(mf.oldFileID);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Script:", FacsGUIStyles.Label, GUILayout.Width(scriptFilterLabelSize));
            EditorGUI.BeginChangeCheck();
            mf.type = (ScriptType)EditorGUILayout.EnumPopup(mf.type);
            if (EditorGUI.EndChangeCheck()) { mf.newTypeI = -1; mf.newType = null; }
            switch (mf.type)
            {
                case ScriptType.MonoBehaviour:
                    EditorGUI.BeginChangeCheck();
                    mf.newTypeI = EditorGUILayout.Popup(mf.newTypeI, availableMBNames);
                    if (EditorGUI.EndChangeCheck()) { mf.newType = availableMB.FirstOrDefault(t => t.FullName == availableMBNames[mf.newTypeI]); }
                    break;
                case ScriptType.ScriptableObject:
                    EditorGUI.BeginChangeCheck();
                    mf.newTypeI = EditorGUILayout.Popup(mf.newTypeI, availableSONames);
                    if (EditorGUI.EndChangeCheck()) { mf.newType = availableSO.FirstOrDefault(t => t.FullName == availableSONames[mf.newTypeI]); }
                    break;
                case ScriptType.StateMachineBehaviour:
                    EditorGUI.BeginChangeCheck();
                    mf.newTypeI = EditorGUILayout.Popup(mf.newTypeI, availableSMBNames);
                    if (EditorGUI.EndChangeCheck()) { mf.newType = availableSMB.FirstOrDefault(t => t.FullName == availableSMBNames[mf.newTypeI]); }
                    break;
            }
            EditorGUILayout.EndHorizontal();
            if (mf.newType != null) { EditorGUILayout.LabelField($"<b>Selected Namespace</b>:  {mf.newType.Namespace}", FacsGUIStyles.Label); }
            return false;
        }

        private static void FilterScripts()
        {
            scriptFilter = scriptFilter.Trim();
            if (string.IsNullOrEmpty(scriptFilter)) DefaultScriptLists();
            else FilterScriptLists();
            foreach (var mf in manualFixes)
            {
                if (mf.newType == null) continue;
                switch (mf.type)
                {
                    case ScriptType.MonoBehaviour:
                        mf.newTypeI = System.Array.FindIndex(availableMBNames, tName => tName == mf.newType.FullName);
                        break;
                    case ScriptType.ScriptableObject:
                        mf.newTypeI = System.Array.FindIndex(availableSONames, tName => tName == mf.newType.FullName);
                        break;
                    case ScriptType.StateMachineBehaviour:
                        mf.newTypeI = System.Array.FindIndex(availableSMBNames, tName => tName == mf.newType.FullName);
                        break;
                }
            }
        }

        private static void RunFix()
        {
            outputMessage = null;
            if (manual)
            {
                for (int i = manualFixes.Count - 1; i >= 0; i--)
                {
                    var mf = manualFixes[i];
                    if (string.IsNullOrEmpty(mf.oldGUID) || mf.oldFileID == 0 || mf.newType == null) manualFixes.RemoveAt(i);
                }
                if (manualFixes.Count == 0)
                {
                    Logger.LogWarning(RichToolName + " There was no valid " + Logger.ConceptTag + "Manual Fix" + Logger.EndTag + " item to reassign.");
                    return;
                }
            }
            string rootFolder;
            if (selectedFolder) rootFolder = AssetDatabase.GetAssetPath(selectedFolder);
            else
            {
                rootFolder = Application.dataPath;
                if (!EditorUtility.DisplayDialog("FACS Utilities - Fix Scripts", "If you don't select a folder to start scanning and fixing assets, " +
                    "the program will use the main Assets folder of the project. The more assets inside the selected folder, " +
                    "the more time it will take to complete.", "Continue", "Cancel")) return;
            }

            var OldScripts = new Dictionary<string, ManualFix>();
            var NewScripts = new List<NewScript>();
            var scriptFolders = new string[0];

            if (manual)
            {
                var NewScriptsLookup = new Dictionary<System.Type, NewScript>();
                for (int i = 0; i < manualFixes.Count; i++)
                {
                    var mf = manualFixes[i];
                    var oldID = $"fileID: {mf.oldFileID}, guid: {mf.oldGUID}";
                    if (!OldScripts.ContainsKey(oldID))
                    {
                        if (!NewScriptsLookup.TryGetValue(mf.newType, out var newScript))
                        {
                            newScript = new NewScript(mf.newType.FullName) { newType=mf.newType };
                            NewScripts.Add(newScript);
                        }
                        OldScripts[oldID] = new() { newScript=newScript };
                    }
                    else
                    {
                        Logger.LogError($"{RichToolName} {Logger.ConceptTag}Manual Fix{Logger.EndTag} #{i+1} is duplicated. Remove it and try again.");
                        return;
                    }
                }
            }
            else
            {
                scriptFolders = Directory.EnumerateDirectories(rootFolder, ".Scripts", SearchOption.AllDirectories)
                .Select(p => p.Replace(@"\", "/")).ToArray();
                if (scriptFolders.Length == 0)
                {
                    Logger.LogWarning(RichToolName + " There is no \".Scripts\" folder inside the selected folder.");
                    return;
                }
                if (!GetOldScripts(scriptFolders, OldScripts, NewScripts)) return;
                scriptFolders = scriptFolders.Where(f => f != null).ToArray();
            }
            GetNewScripts(NewScripts);

            AssetDatabase.SaveAssets();
            var assetPaths = Directory.GetFiles(rootFolder, "*.asset",
                SearchOption.AllDirectories).Select(path => path.Replace(@"\", "/")).ToArray();
            var assetPaths2 = Directory.GetFiles(rootFolder, "*.controller",
                SearchOption.AllDirectories).Select(path => path.Replace(@"\", "/"));
            var assetPaths3 = Directory.GetFiles(rootFolder, "*.prefab",
                SearchOption.AllDirectories).Select(path => path.Replace(@"\", "/"));
            var assetPaths4 = Directory.GetFiles(rootFolder, "*.unity",
                SearchOption.AllDirectories).Select(path => path.Replace(@"\", "/"));
            assetPaths = assetPaths.Concat(assetPaths2).Concat(assetPaths3).Concat(assetPaths4).ToArray();
            string[] animationClipPaths = Directory.GetFiles(rootFolder, "*.anim",
                SearchOption.AllDirectories).Select(path => path.Replace(@"\", "/")).ToArray();
            int fixedFiles = 0; int progress = 0; float progressTotal = assetPaths.Length + animationClipPaths.Length;

            if (progressTotal == 0)
            {
                Logger.LogWarning(RichToolName + " No asset inside the selected folder to fix."); return;
            }

            Selection.activeObject = null;
            var reimportFiles = new HashSet<string>();
            foreach (string filePath in assetPaths)
            {
                progress++;
                EditorUtility.DisplayProgressBar("FACS Utilities - Fix Scripts", $"Processing file: {Path.GetFileName(filePath)}", progress / progressTotal);
                if (FixAsset(filePath, OldScripts)) { reimportFiles.Add(filePath); fixedFiles++; }
            }
            foreach (string filePath in animationClipPaths)
            {
                progress++;
                EditorUtility.DisplayProgressBar("FACS Utilities - Fix Scripts", $"Processing file: {Path.GetFileName(filePath)}", progress / progressTotal);
                if (FixAnimationClip(filePath, OldScripts)) { reimportFiles.Add(filePath); fixedFiles++; }
            }
            if (fixedFiles > 0)
            {
                foreach (var file in reimportFiles)
                {
                    AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);
                }
            }
            EditorUtility.ClearProgressBar();

            GenerateResults(fixedFiles, NewScripts);

            if (!manual && deleteMetas) DeleteUnnecessaryScripts(scriptFolders, OldScripts);
        }

        private static void DeleteUnnecessaryScripts(string[] scriptFolders, Dictionary<string, ManualFix> OldScripts)
        {
            var unnecessaryScripts = OldScripts.Values.Where(s => !s.seen || s.newScript.replaces > 0).SelectMany(s => s.metaPaths);
            foreach (var file in unnecessaryScripts)
            {
                if (File.Exists(file)) File.Delete(file);
                var csFile = file[0..^5];
                if (File.Exists(csFile)) File.Delete(csFile);
            }
            foreach (var scriptFolder in scriptFolders) DeleteEmptyDirs(scriptFolder);
            foreach (var scriptFolder in scriptFolders)
                if (!Directory.Exists(scriptFolder)) Logger.Log($"{RichToolName} The folder \"{scriptFolder}\" wasn't needed anymore and was deleted.");
        }

        private static void DeleteEmptyDirs(string mainDir)
        {
            foreach (var dir in Directory.GetDirectories(mainDir)) DeleteEmptyDirs(dir);
            if (Directory.GetFiles(mainDir, "*.meta", SearchOption.TopDirectoryOnly).Length == 0 &&
                    Directory.GetDirectories(mainDir).Length == 0) Directory.Delete(mainDir, true);
        }

        private static void GenerateResults(int fixedFiles, List<NewScript> NewScripts)
        {
            outputMessage = $"\n <color=green>Fixed files:</color> <b>{fixedFiles}</b>\n";
            var scrL = NewScripts.OrderByDescending(s => s.replaces + (s.seen ? 1 : 0)).ThenBy(s => s.newType != null ? s.newType.Name : s.fullName);
            foreach (var scr in scrL.Where(s => s.replaces > 0)) outputMessage += $"   • <color=cyan>{scr.newType.Name}:</color> " +
                        $"<b>{scr.replaces}</b> subasset{(scr.replaces > 1 ? "s" : "")} fixed\n";
            var missing = scrL.Where(s => s.seen && string.IsNullOrEmpty(s.newGUID));
            if (missing.Any())
            {
                outputMessage += $"\n <color=grey><b>Missing Scripts in project:</b>\n";
                foreach (var m in missing) outputMessage += $"   • {m.fullName}\n";
                outputMessage += "</color>";
            }
        }

        private static bool FixAsset(string filepath, Dictionary<string, ManualFix> OldScripts)
        {
            var fixedfile = false;
            var tempfilepath = FileUtil.GetUniqueTempPathInProject();
            using (var reader = new StreamReader(filepath))
            {
                using (var writer = new StreamWriter(tempfilepath))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (line.StartsWith("--- !u!114 "))
                        {
                            do { writer.WriteLine(line); line = reader.ReadLine(); }
                            while (line != null && !line.StartsWith("  m_Script: "));
                            if (line == null) break;
                            if (line.Contains("guid:"))
                            {
                                var oldFileID_GUID = line[line.IndexOf("fileID")..line.LastIndexOf(',')];
                                if (OldScripts.TryGetValue(oldFileID_GUID, out var oldScr))
                                {
                                    oldScr.seen = oldScr.newScript.seen = true;
                                    if (oldScr.newScript.newGUID != null)
                                    {
                                        line = $"  m_Script: {oldScr.newScript.newGUID}";
                                        oldScr.newScript.replaces++; fixedfile = true;
                                    }
                                }
                            }
                        }
                        writer.WriteLine(line);
                    }
                }
            }
            if (fixedfile)
            {
                FileUtil.DeleteFileOrDirectory(filepath);
                FileUtil.MoveFileOrDirectory(tempfilepath, filepath);
            }
            else FileUtil.DeleteFileOrDirectory(tempfilepath);
            return fixedfile;
        }

        private static bool FixAnimationClip(string filepath, Dictionary<string, ManualFix> OldScripts)
        {
            var fixedfile = false;
            var tempfilepath = FileUtil.GetUniqueTempPathInProject();
            using (var reader = new StreamReader(filepath))
            {
                using (var writer = new StreamWriter(tempfilepath))
                {
                    string line = reader.ReadLine();
                    while (line != null)
                    {
                        while (line != null && !line.StartsWith("--- !u!74 "))
                        {
                            writer.WriteLine(line); line = reader.ReadLine();
                        }
                        if (line == null) break;
                        var subAsset = line;
                        writer.WriteLine(line); line = reader.ReadLine();
                        while (line != null && !line.StartsWith("--- !u!"))
                        {
                            if (line.Contains("    script: {fileID:") && line.Contains("guid:"))
                            {
                                var oldFileID_GUID = line[line.IndexOf("fileID")..line.LastIndexOf(',')];
                                if (OldScripts.TryGetValue(oldFileID_GUID, out var oldScr))
                                {
                                    oldScr.seen = oldScr.newScript.seen = true;
                                    if (oldScr.newScript.newGUID != null)
                                    {
                                        int whitespaces = line.Length - line.TrimStart().Length;
                                        line = $"{new string(' ', whitespaces)}script: {oldScr.newScript.newGUID}";
                                        if (oldScr.newScript.lastAsset != subAsset)
                                        {
                                            oldScr.newScript.lastAsset = subAsset;
                                            oldScr.newScript.replaces++; fixedfile = true;
                                        }
                                    }
                                }
                            }
                            writer.WriteLine(line);
                            line = reader.ReadLine();
                        }
                    }
                }
            }
            if (fixedfile)
            {
                FileUtil.DeleteFileOrDirectory(filepath);
                FileUtil.MoveFileOrDirectory(tempfilepath, filepath);
            }
            else FileUtil.DeleteFileOrDirectory(tempfilepath);
            return fixedfile;
        }

        private static bool GetOldScripts(string[] scriptFolders,
            Dictionary<string, ManualFix> OldScripts, List<NewScript> NewScripts)
        {
            Dictionary<string, NewScript> NewScriptsLookup = new();
            for (int j = 0; j < scriptFolders.Length; j++)
            {
                var scriptFolder = scriptFolders[j];
                var metaPaths = Directory.GetFiles(scriptFolder, "*.meta", SearchOption.AllDirectories)
                    .Select(path => path.Replace(@"\", "/")).ToArray();
                if (!metaPaths.Any())
                {
                    scriptFolders[j] = null;
                    var relativeFolderPath = scriptFolder[(Directory.GetCurrentDirectory().Length + 1)..];
                    if (EditorUtility.DisplayDialog("FACS Utilities - Fix Scripts", $"The folder \"{relativeFolderPath}\" doesn't contain any .meta file. Delete?", "Yes", "No"))
                    {
                        Directory.Delete(scriptFolder, true);
                    }
                    Logger.Log($"{RichToolName} The folder \"{relativeFolderPath}\" did not contain .meta files.");
                    continue;
                }
                var scriptsFullNames = metaPaths.Select(path => path.Replace(scriptFolder + "/", ""))
                    .Select(p => p.Substring(p.IndexOf('/') + 1, p.Length - 9 - p.IndexOf('/')).Replace("/", ".")).ToArray();
                for (int i = 0; i < metaPaths.Length; i++)
                {
                    using (var file = new StreamReader(metaPaths[i], System.Text.Encoding.UTF8))
                    {
                        while (!file.EndOfStream)
                        {
                            var line = file.ReadLine();
                            if (line.StartsWith("guid:"))
                            {
                                var fileId_guid = $"fileID: 11500000, guid: {line[6..]}";

                                if (!OldScripts.TryGetValue(fileId_guid, out var oldScr))
                                {
                                    if (!NewScriptsLookup.TryGetValue(scriptsFullNames[i], out var newScr))
                                    {
                                        newScr = new(scriptsFullNames[i]);
                                        NewScriptsLookup[scriptsFullNames[i]] = newScr;
                                        NewScripts.Add(newScr);
                                    }
                                    oldScr = new() { newScript = newScr };
                                    OldScripts[fileId_guid] = oldScr;
                                }
                                else if (oldScr.newScript.fullName != scriptsFullNames[i])
                                {
                                    Logger.LogError($"{RichToolName} {Logger.TypeTag}GUID{Logger.EndTag} is used for two different script types.\n" +
                                        $"\"{scriptsFullNames[i]}\" at \"{metaPaths[i]}\"\n\"{oldScr.newScript.fullName}\" at \"{string.Join("\"; \"", oldScr.metaPaths)}\"");
                                    return false;
                                }
                                oldScr.metaPaths.Add(metaPaths[i]);
                                break;
                            }
                        }
                    }
                }
            }
            if (OldScripts.Count > 0) return true;
            Logger.LogWarning(RichToolName + " There were no valid .meta files inside the \".Scripts\" folder.");
            return false;
        }

        internal static IEnumerable<System.Type> AvailableMonoBehaviours()
        {
            return TypeCache.GetTypesDerivedFrom(typeof(MonoBehaviour))
                .Where(t => t.DeclaringType == null && !t.IsAbstract).OrderBy(t=>t.FullName);
        }

        internal static IEnumerable<System.Type> AvailableScriptableObjects()
        {
            return TypeCache.GetTypesDerivedFrom(typeof(ScriptableObject))
                .Where(t => t.DeclaringType == null && !t.IsAbstract && !t.IsSubclassOf(typeof(StateMachineBehaviour))).OrderBy(t => t.FullName);
        }

        internal static IEnumerable<System.Type> AvailableStateMachineBehaviours()
        {
            return TypeCache.GetTypesDerivedFrom(typeof(StateMachineBehaviour))
                .Where(t => t.DeclaringType == null && !t.IsAbstract).OrderBy(t => t.FullName);
        }

        private static System.Type[] AvailableScriptTypes()
        {
            var allTypes = new List<System.Type>();
            allTypes.AddRange(AvailableMonoBehaviours());
            allTypes.AddRange(AvailableScriptableObjects());
            allTypes.AddRange(AvailableStateMachineBehaviours());
            return allTypes.ToArray();
        }

        private static void GetNewScripts(List<NewScript> NewScripts)
        {
            var allTypes = AvailableScriptTypes();
            foreach (var newScr in NewScripts)
            {
                if (newScr.newType == null)
                {
                    newScr.newType = allTypes.FirstOrDefault(t => t.FullName == newScr.fullName);
                }
                if (newScr.newType != null)
                {
                    MonoScript ms = null; bool isMB = false;
                    if (newScr.newType.IsSubclassOf(typeof(ScriptableObject)))
                    {
                        var tempSO = ScriptableObject.CreateInstance(newScr.newType);
                        ms = MonoScript.FromScriptableObject(tempSO);
                        DestroyImmediate(tempSO);
                    }
                    else if (newScr.newType.IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        isMB = true;
                        var tempGO = EditorUtility.CreateGameObjectWithHideFlags("temp go", HideFlags.HideAndDontSave);
                        var tempMB = (MonoBehaviour)ComponentDependencies.TryAddComponent(tempGO, newScr.newType);
                        ms = MonoScript.FromMonoBehaviour(tempMB);
                        DestroyImmediate(tempMB); DestroyImmediate(tempGO);
                    }
                    if (ms != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ms, out string guid, out long fileID))
                    {
                        newScr.newGUID = $"{{fileID: {fileID}, guid: {guid}, type: 3}}";
                    }
                    else
                    {
                        Logger.LogError($"{RichToolName} Failed to get GUID and FileID for {Logger.TypeTag}{(isMB? "MonoBehaviour": "ScriptableObject")}{Logger.EndTag} script: {Logger.AssetTag}{newScr.newType.FullName}{Logger.EndTag}");
                        newScr.newType = null;
                    }
                }
            }
        }

        private static void InitAvailableScripts()
        {
            availableMB = AvailableMonoBehaviours().ToArray();
            availableSO = AvailableScriptableObjects().ToArray();
            availableSMB = AvailableStateMachineBehaviours().ToArray();
            DefaultScriptLists();
        }

        private static void DefaultScriptLists()
        {
            availableMBNames = availableMB.Select(t => t.FullName).ToArray();
            availableSONames = availableSO.Select(t => t.FullName).ToArray();
            availableSMBNames = availableSMB.Select(t => t.FullName).ToArray();
        }

        private static void FilterScriptLists()
        {
            availableMBNames = availableMB.Select(t => t.FullName)
                .Where(t => t.Contains(scriptFilter, System.StringComparison.OrdinalIgnoreCase)).ToArray();
            availableSONames = availableSO.Select(t => t.FullName)
                .Where(t => t.Contains(scriptFilter, System.StringComparison.OrdinalIgnoreCase)).ToArray();
            availableSMBNames = availableSMB.Select(t => t.FullName)
                .Where(t => t.Contains(scriptFilter, System.StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        private void OnDestroy()
        {
            NullVars();
            selectedFolder = null;
            FacsGUIStyles = null;
        }

        private static void NullVars()
        {
            outputMessage = null;
            deleteMetas = false;
            manual = false;
            manualFixes.Clear(); manualFixes.Add(new());
            scriptFilter = "";
            if (availableMB != null) DefaultScriptLists();
        }

        internal class NewScript
        {
            public string fullName;
            public System.Type newType = null;
            public string newGUID = null;
            public bool seen = false;
            public int replaces = 0;
            public string lastAsset = null;

            public NewScript(string fn)
            {
                fullName = fn;
            }
        }

        internal enum ScriptType
        {
            MonoBehaviour,
            ScriptableObject,
            StateMachineBehaviour
        }

        internal class ManualFix
        {
            public bool foldout = false;
            public string oldGUID = "";
            public long oldFileID = 0;
            public ScriptType type = ScriptType.MonoBehaviour;
            public int newTypeI = -1;
            public System.Type newType = null;

            public bool seen;
            public List<string> metaPaths = new();
            public NewScript newScript;
        }
    }
}
#endif