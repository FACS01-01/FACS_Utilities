#if UNITY_EDITOR
#if (VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3)
using VRC.Core;
#endif
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FACS01.Utilities
{
    public class BatchLoadBundle : EditorWindow
    {
        private readonly string avatarCache_file = "avatarCache.txt";
        private readonly string avatarCache_cacheFolder = "/Avatar Cache";
        private readonly string avatarCache_header = "FACS Utilities - Avatar Cache";
        private static readonly string SavedData_file = "savedData.txt";
        private static readonly string SavedData_header = "FACS Utilities - Configs";
        private static readonly string SavedData_configParam = "BatchLoadBundle.LastFolder:";

        private static FACSGUIStyles FacsGUIStyles;
        private Vector2 scrollPos;
        private SearchField searchField;
        private string searchFieldinput;
        private string folderPath;

        //[(authorID,authorName), (aviID,aviName), ('PC':#,path), ('Quest':#,path)]
        private List<((string, string), (string, string), (string, string), (string, string))> LBSelectables;
        private List<((string, string), (string, string), (string, string), (string, string))> LBSelectablesFiltered;
        private ((string, string), (string, string), (string, string, string)) LBSelected;
        private int LBSelectablesSize;
        private int startingPos;

        private string LBSelected_Name;
        private string LBSource;
        private GameObject objectInstance;
        private AssetBundle LoadedAssetBundle;

        private bool isWorking;

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
            folderPath = SavedDataManager.ReadSavedData("", SavedData_file, SavedData_header, SavedData_configParam, true);
            scrollPos = new Vector2(0,0);
            searchField = new SearchField();
            searchFieldinput = "";
            LBSelectables = new List<((string, string), (string, string), (string, string), (string, string))>();
            LBSelectablesFiltered = null;
            startingPos = 0;
            LBSelected = (("", ""), ("", ""), ("", "", ""));
            LBSelected_Name = "";
            isWorking = false;
        }
        public void PlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                UnloadAssetBundle();
            }
        }
        [MenuItem("FACS Utils/Misc/Batch Load Bundles", false, 1000)]
        public static void ShowWindow()
        {
            GetWindow(typeof(BatchLoadBundle), false, "Batch Load Bundles", true);
        }
        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"<color=cyan><b>FACS Batch Load Bundles</b></color>\nScans the selected folder and generates a menu " +
                $"for loading AssetBundles stored as .vrca or .txt(Log) files.", FacsGUIStyles.helpbox);

            if (GUILayout.Button("Select Folder", FacsGUIStyles.button, GUILayout.Height(40)) && !isWorking)
            {
                string newfolderPath = EditorUtility.OpenFolderPanel("Select Folder with Bundles", folderPath, "");
                if (!String.IsNullOrEmpty(newfolderPath))
                {
                    if (newfolderPath != folderPath)
                    {
                        folderPath = newfolderPath;
                        SavedDataManager.WriteSavedData("", SavedData_file, SavedData_header, SavedData_configParam, folderPath);
                    }
                    
                    string[] filesArray = Directory.GetFiles(folderPath);
                    if (filesArray.Any())
                    {
                        LBSelectables = new List<((string, string), (string, string), (string, string), (string, string))>();
                        var vrcalogs = filesArray.Where(o => o.EndsWith(".txt"));
                        if (vrcalogs.Any()) foreach (string filepath in vrcalogs)
                            {
                                using (StreamReader sr = new StreamReader(filepath))//Log
                                {
                                    string header = sr.ReadLine().Replace("\n", "");//header
                                    if (header == "Mod By LargestBoi & Yui")
                                    {
                                        while (sr.Peek() != -1)
                                        {
                                            sr.ReadLine();//Time Detected
                                            if (sr.Peek() == -1) { break; }
                                            string aviID = sr.ReadLine().Substring(10).Replace("\n", "");//Avatar ID
                                            string aviName = sr.ReadLine().Substring(12).Replace("\n", "");//Avatar Name
                                            sr.ReadLine();//Avatar Description
                                            string authorID = sr.ReadLine().Substring(10).Replace("\n", "");//Author ID
                                            string authorName = sr.ReadLine().Substring(12).Replace("\n", "");//Author Name
                                            string aviSource = sr.ReadLine().Substring(13).Replace("\n", "");//PC Asset URL
                                            string aviSourceQuest = sr.ReadLine().Substring(16).Replace("\n", "");//Quest Asset URL
                                            sr.ReadLine();//Image URL
                                            sr.ReadLine();//Thumbnail URL
                                            sr.ReadLine();//Unity Version
                                            sr.ReadLine();//Release Status
                                            sr.ReadLine();//Tags
                                            sr.ReadLine();//newline

                                            string PCversion = "None";
                                            string Questversion = "None";

                                            if (aviSource != "None")
                                            {
                                                int tmp1 = aviSource.LastIndexOf("/", aviSource.Length);
                                                int tmp2 = aviSource.LastIndexOf("/", tmp1-1) +1;
                                                PCversion = aviSource.Substring(tmp2, tmp1 - tmp2);
                                            }
                                            if (aviSourceQuest != "None")
                                            {
                                                int tmp3 = aviSourceQuest.LastIndexOf("/", aviSourceQuest.Length);
                                                int tmp4 = aviSourceQuest.LastIndexOf("/", tmp3 - 1) + 1;
                                                Questversion = aviSourceQuest.Substring(tmp4, tmp3 - tmp4);
                                            }
                                            LBSelectables.Add(((authorID, authorName), (aviID, aviName), (PCversion, aviSource), (Questversion, aviSourceQuest)));
                                        }
                                    }
                                    else
                                    {
                                        // invalid log header
                                    }
                                }
                            }
                        var vrcalist = filesArray.Where(o => o.EndsWith(".vrca"));
                        if (vrcalist.Any()) foreach (string filepath in vrcalist)
                            {
                                LBSelectables.Add((("?", "? (unknown)"), ("?", "Avatar: " + Path.GetFileNameWithoutExtension(filepath)), ("?", filepath.Replace("\\", "/")), ("?", "None*")));
                            }
                        if (!LBSelectables.Any())
                        {
                            LBSelectables = new List<((string, string), (string, string), (string, string), (string, string))>();
                            Debug.LogWarning($"[<color=green>BatchLoadBundle</color>] No .vrca or Log files in folder: {folderPath}");
                        }
                        else
                        {
                            LBSelectablesFiltered = LBSelectables; LBSelectablesSize = LBSelectablesFiltered.Count;
                            LBSelected = (("", ""), ("", ""), ("", "", ""));
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[<color=green>BatchLoadBundle</color>] No .vrca or Log files in folder: {folderPath}");
                    }
                }
                RunSearchFilter();
            }

            if (LBSelectables.Any())
            {
                if (LBSelected.Item1.Item1 != "")
                {
                    EditorGUILayout.LabelField($"<color=green>Selected Bundle:</color>\n" +
                        $"Author: {LBSelected.Item1.Item2} || {LBSelected.Item1.Item1}\n" +
                        $"Avatar: {LBSelected.Item2.Item2} || {LBSelected.Item2.Item1}\n" +
                        $"For {LBSelected.Item3.Item1}. Version {LBSelected.Item3.Item2}\n" +
                        $"Source: {LBSelected.Item3.Item3}", FacsGUIStyles.helpbox);

                    LBSelected_Name = EditorGUILayout.TextField("Bundle Name:", LBSelected_Name);
                    if (GUILayout.Button("Load/Unload Asset Bundle", FacsGUIStyles.button, GUILayout.Height(40)) && !isWorking)
                    {
                        if (LoadedAssetBundle)
                        {
                            UnloadAssetBundle();
                        }
                        else
                        {
                            LBSource = LBSelected.Item3.Item3;
                            LoadAssetBundle();
                        }
                    }
                }

                string newinput = searchField.OnGUI(searchFieldinput);
                if (newinput != searchFieldinput)
                {
                    searchFieldinput = newinput;
                    RunSearchFilter();
                }
                if (LBSelectablesSize > 50)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Prev", FacsGUIStyles.button) && startingPos >= 50)
                    {
                        startingPos -= 50;
                    }
                    EditorGUILayout.LabelField($"Displaying items {startingPos+1} to {Math.Min(startingPos+50, LBSelectablesSize)}, from {LBSelectablesSize}", FacsGUIStyles.helpbox);
                    if (GUILayout.Button("Next", FacsGUIStyles.button) && startingPos+50 <= LBSelectablesSize)
                    {
                        startingPos += 50;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else { EditorGUILayout.LabelField($"Displaying {LBSelectablesSize} items", FacsGUIStyles.helpbox); }
                
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                for (int i = startingPos; i < Math.Min(LBSelectablesSize, startingPos+50); i++)
                {
                    var selectableElement = LBSelectablesFiltered.ElementAt(i);
                    if (GUILayout.Button($"Author: {selectableElement.Item1.Item2} || {selectableElement.Item1.Item1}\n" +
                        $"Avatar: {selectableElement.Item2.Item2} || {selectableElement.Item2.Item1}\n" +
                        $"PC version: {selectableElement.Item3.Item1} || Quest version: {selectableElement.Item4.Item1}", FacsGUIStyles.button) && !isWorking) {
                        if (LoadedAssetBundle)
                        {
                            UnloadAssetBundle();
                        }
                        if (selectableElement.Item3.Item2 != "None" && !selectableElement.Item4.Item2.StartsWith("None"))
                        {
                            if (!EditorUtility.DisplayDialog("PC or Quest version?", "The selected avatar has a PC version and a Quest version available.\n\nWhich one do you want to load?", "Quest", "PC"))
                            {
                                LBSelected = ((selectableElement.Item1.Item1, selectableElement.Item1.Item2), (selectableElement.Item2.Item1, selectableElement.Item2.Item2), ("PC", selectableElement.Item3.Item1, selectableElement.Item3.Item2));
                            }
                            else
                            {
                                LBSelected = ((selectableElement.Item1.Item1, selectableElement.Item1.Item2), (selectableElement.Item2.Item1, selectableElement.Item2.Item2), ("Quest", selectableElement.Item4.Item1, selectableElement.Item4.Item2));
                            }
                        }
                        else if (selectableElement.Item3.Item2 != "None")
                        {
                            if (selectableElement.Item4.Item2 == "None")
                            {
                                LBSelected = ((selectableElement.Item1.Item1, selectableElement.Item1.Item2), (selectableElement.Item2.Item1, selectableElement.Item2.Item2), ("PC", selectableElement.Item3.Item1, selectableElement.Item3.Item2));
                            }
                            else { LBSelected = ((selectableElement.Item1.Item1, selectableElement.Item1.Item2), (selectableElement.Item2.Item1, selectableElement.Item2.Item2), ("?", selectableElement.Item3.Item1, selectableElement.Item3.Item2)); }
                        }
                        else if (!selectableElement.Item4.Item2.StartsWith("None"))
                        {
                            LBSelected = ((selectableElement.Item1.Item1, selectableElement.Item1.Item2), (selectableElement.Item2.Item1, selectableElement.Item2.Item2), ("Quest", selectableElement.Item4.Item1, selectableElement.Item4.Item2));
                        }
                        else
                        {
                            // doesnt have pc or quest source?
                            Debug.LogWarning($"[<color=green>BatchLoadBundle</color>] Selected element doesn't have PC or Quest source available?");
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }
        private void UnloadAssetBundle()
        {
            if (objectInstance != null)
            {
                Debug.Log($"[<color=green>BatchLoadBundle</color>] Removing Instance of <color=green>{objectInstance.name}</color>\n");
                DestroyImmediate(objectInstance);
            }
            if (LoadedAssetBundle != null)
            {
                Debug.Log($"[<color=green>BatchLoadBundle</color>] Unloading <color=cyan>AssetBundle</color> {LBSource}\n");
                LoadedAssetBundle.Unload(true);
                LoadedAssetBundle = null;
            }
        }
        void DownloadFile_Completed(object sender, AsyncCompletedEventArgs e)
        {
            string filename = (string)e.UserState;
            string filepath = SavedDataManager.MainFolder + avatarCache_cacheFolder + "/" + filename;
            EditorUtility.ClearProgressBar();
            if (e.Error != null)
            {
                Debug.LogError($"[<color=green>BatchLoadBundle</color>] Error while downloading file from {LBSource}\n{e.Error.StackTrace}");
            }
            else
            {
                List<string[]> cache = SavedDataManager.ReadDictData("", avatarCache_file, avatarCache_header, new List<string> { LBSelected.Item1.Item1, LBSelected.Item2.Item1, LBSelected.Item3.Item1, LBSelected.Item3.Item2, LBSelected.Item3.Item3 });
                if (cache == null)
                {
                    List<string[]> tmp = new List<string[]>();
                    tmp.Add(new string[] { LBSelected.Item1.Item1, LBSelected.Item1.Item2 });//author
                    tmp.Add(new string[] { LBSelected.Item2.Item1, LBSelected.Item2.Item2 });//avi
                    tmp.Add(new string[] { LBSelected.Item3.Item1 });//pc or quest
                    tmp.Add(new string[] { LBSelected.Item3.Item2 });//version
                    tmp.Add(new string[] { LBSelected.Item3.Item3, filename });//link
                    SavedDataManager.WriteDictData("", avatarCache_file, avatarCache_header, tmp);
                    Debug.Log($"[<color=green>BatchLoadBundle</color>] File added to cache: {filepath}");
                }
                LBSource = filepath;
                LBSelected.Item3.Item3 = filepath;
                LoadAssetBundle();
            }
            isWorking = false;
        }
        void DownloadFile_ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            int progress = e.ProgressPercentage;
            EditorUtility.DisplayProgressBar("Batch Load Bundle - Downloading Bundle", $"File size: {e.TotalBytesToReceive/(1048576.0f):n3} MB. Please wait...", progress / 100.0f);
        }
        private void LoadAssetBundle()
        {
            if (LBSource.StartsWith("http"))
            {
                string filename = LBSource.Substring(LBSource.IndexOf("file_") + 5);
                filename = filename.Substring(0, filename.IndexOf("/"));
                filename = LBSelected.Item3.Item1 + "_" + filename + "_" + LBSelected.Item3.Item2 + ".vrca";
                string filepath = SavedDataManager.MainFolder + avatarCache_cacheFolder + "/" + filename;

                if (!Directory.Exists(SavedDataManager.MainFolder + avatarCache_cacheFolder))
                {
                    Directory.CreateDirectory(SavedDataManager.MainFolder + avatarCache_cacheFolder);
                }

                if (!File.Exists(filepath))
                {
                    if (!EditorUtility.DisplayDialog("Download Asset Bundle from URL", $"You want to load an avatar from a URL:\n{LBSource}\nThe file will be added to the cache folder at:\n{SavedDataManager.MainFolder + avatarCache_cacheFolder}\n\n\nProceed?", "Yes", "No"))
                    {
                        Debug.LogWarning($"[<color=green>BatchLoadBundle</color>] Not authorized by user to download file from {LBSource}");
                        return;
                    }
                    WebClient wC = new WebClient();
                    wC.Headers.Add("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
                    wC.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFile_Completed);
                    wC.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadFile_ProgressChanged);
                    wC.DownloadFileAsync(new Uri(LBSource), filepath, filename);
                    isWorking = true;
                    return;
                }
                List<string[]> cache = SavedDataManager.ReadDictData("", avatarCache_file, avatarCache_header, new List<string> { LBSelected.Item1.Item1, LBSelected.Item2.Item1, LBSelected.Item3.Item1, LBSelected.Item3.Item2, LBSelected.Item3.Item3 });
                if (cache == null)
                {
                    List<string[]> tmp = new List<string[]>();
                    tmp.Add(new string[] { LBSelected.Item1.Item1, LBSelected.Item1.Item2 });//author
                    tmp.Add(new string[] { LBSelected.Item2.Item1, LBSelected.Item2.Item2 });//avi
                    tmp.Add(new string[] { LBSelected.Item3.Item1 });//pc or quest
                    tmp.Add(new string[] { LBSelected.Item3.Item2 });//version
                    tmp.Add(new string[] { LBSelected.Item3.Item3, filename });//link
                    SavedDataManager.WriteDictData("", avatarCache_file, avatarCache_header, tmp);
                    Debug.Log($"[<color=green>BatchLoadBundle</color>] Restored cache entry for file: {filepath}");
                }
                LBSource = filepath;
                LBSelected.Item3.Item3 = filepath;
            }
            
            LoadedAssetBundle = AssetBundle.LoadFromFile(LBSource);
            if (LoadedAssetBundle == null)
            {
                Debug.LogError($"[<color=green>BatchLoadBundle</color>] Failed to load AssetBundle from file: {LBSource}\n"); return;
            }

            string[] scenePaths = LoadedAssetBundle.GetAllScenePaths();

            if (scenePaths.Length == 0)
            {
                foreach (string asset in LoadedAssetBundle.GetAllAssetNames())
                {
                    if (asset.EndsWith(".prefab"))
                    {
                        objectInstance = Instantiate((GameObject)LoadedAssetBundle.LoadAsset(asset));
                        objectInstance.transform.position = new Vector3(0, 0, 0);

#if VRC_SDK_VRCSDK3
                        DestroyImmediate(objectInstance.GetComponent<PipelineSaver>());
                        DestroyImmediate(objectInstance.GetComponent<PipelineManager>());
#elif VRC_SDK_VRCSDK2
                        DestroyImmediate(objectInstance.GetComponent<PipelineManager>());
#endif

                        if (!String.IsNullOrWhiteSpace(LBSelected_Name))
                        {
                            objectInstance.name = LBSelected_Name;
                        }
                        else
                        {
                            string fileName = Path.GetFileNameWithoutExtension(LBSource);
                            string prefabName = objectInstance.name;

                            if (prefabName.EndsWith("(Clone)")) { prefabName = prefabName.Substring(0, prefabName.LastIndexOf("(Clone)")); }

                            objectInstance.name = fileName + " (" + prefabName + ")";
                        }

                        Debug.Log($"<color=green>{objectInstance.name}</color> was loaded from <color=cyan>AssetBundle</color>!\n");

                        break;
                    }
                }
            }

            else
            {
                string sceneName = Path.GetFileNameWithoutExtension(scenePaths[0]);
                SceneManager.LoadScene(sceneName);

                Debug.Log($"<color=green>{sceneName}</color> was loaded from <color=cyan>AssetBundle</color>!\n");
            }
        }
        private void RunSearchFilter()
        {
            if (String.IsNullOrEmpty(searchFieldinput)) { LBSelectablesFiltered = LBSelectables; }
            else
            {
                LBSelectablesFiltered = LBSelectables.Where(o => o.Item1.Item2.Contains(searchFieldinput) || o.Item2.Item2.Contains(searchFieldinput) || o.Item1.Item1.Contains(searchFieldinput) || o.Item2.Item1.Contains(searchFieldinput)).ToList();
            }
            LBSelectablesSize = LBSelectablesFiltered.Count;
            startingPos = 0;
        }
        void OnDestroy()
        {
            if (LoadedAssetBundle)
            {
                UnloadAssetBundle();
            }
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            FacsGUIStyles = null;
            scrollPos = default;
            searchField = null;
            searchFieldinput = null;
            folderPath = null;
            LBSelectables = null;
            LBSelectablesFiltered = null;
            LBSelected = default;
            LBSelectablesSize = default;
            startingPos = default;
            LBSelected_Name = null;
            LBSource = null;
            objectInstance = null;
            LoadedAssetBundle = null;
            isWorking = false;
        }
    }
}
#endif