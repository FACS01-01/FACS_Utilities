#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace FACS01.Utilities
{
    public class BatchLoadBundle : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;
        public SearchField searchField;
        private string folderPath = "";
        public string searchFieldinput = "";

        private GameObject goHolder;
        private FACSLoadBundle2019 LoadBundleScript;
        //(avi author, avi name, avi source)
        private List<(string,string,string)> LBSelectables = new List<(string, string, string)>();
        private List<(string, string, string)> LBSelectablesFiltered;
        private Vector2 scrollPos;
        private int startingPos = 0;
        private int LBSelectablesSize;

        private (string, string, string) LBSelected = ("","","");
        private bool LBSelected_isLoaded = false;
        private string LBSelected_Name = "";

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        [MenuItem("FACS Utils/Misc/Batch Load Bundles", false, 1000)]
        public static void ShowWindow()
        {
            if (!Application.isPlaying) { GetWindow(typeof(BatchLoadBundle), false, "Batch Load Bundles", true); }
        }
        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;

            if (searchField == null) { searchField = new SearchField(); }
            goHolder = GameObject.Find("goHolder");
            if (goHolder == null)
            {
                goHolder = EditorUtility.CreateGameObjectWithHideFlags("goHolder", HideFlags.HideInHierarchy, new Type[] { typeof(FACSLoadBundle2019) });
            }
            if (LoadBundleScript == null)
            {
                LoadBundleScript = goHolder.GetComponent<FACSLoadBundle2019>();
                LoadBundleScript.runOnPlayMode = false; LoadBundleScript.attachAsChild = false;
            }

            EditorGUILayout.LabelField($"<color=cyan><b>FACS Batch Load Bundles</b></color>\nScans the selected folder and generates a menu " +
                $"for loading AssetBundles stored as .vrca or .txt(Log) files.\nDoes not work with Unity on Play Mode.", FacsGUIStyles.helpbox);

            if (GUILayout.Button("Select Folder", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                folderPath = EditorUtility.OpenFolderPanel("Select Folder with Bundles", folderPath, "");
                if (!String.IsNullOrEmpty(folderPath))
                {
                    string[] filesArray = Directory.GetFiles(folderPath);
                    if (filesArray.Any())
                    {
                        LBSelectables = new List<(string, string, string)>();
                        var vrcalogs = filesArray.Where(o => o.EndsWith(".txt"));
                        if (vrcalogs.Any()) foreach (string filepath in vrcalogs)
                            {
                                using (StreamReader sr = new StreamReader(filepath))//.vrcalog
                                {
                                    string header = sr.ReadLine().Replace("\n", "");//header
                                    if (header == "Original Mod by KeafyIsHere and Maintained by LargestBoi, cassell1337 & MonkeyBoi(Boppr)") while (sr.Peek() != -1)
                                        {
                                            sr.ReadLine();//time detected
                                            if (sr.Peek() == -1) { break; }
                                            string aviID = sr.ReadLine().Substring(10).Replace("\n", "");//avi id
                                            string aviName = sr.ReadLine().Substring(12).Replace("\n", "");//avi name
                                            sr.ReadLine();//avi descr
                                            string authorID = sr.ReadLine().Substring(10).Replace("\n", "");//author id
                                            string authorName = sr.ReadLine().Substring(12).Replace("\n", "");//author name
                                            string aviSource = sr.ReadLine().Substring(10).Replace("\n", "");//asset url
                                            sr.ReadLine();//image url
                                            sr.ReadLine();//thumbnail url
                                            sr.ReadLine();//release status
                                            sr.ReadLine();//unity version
                                            sr.ReadLine();//platform
                                            sr.ReadLine();//api version
                                            sr.ReadLine();//version
                                            sr.ReadLine();//tags
                                            sr.ReadLine();//newline
                                            LBSelectables.Add(("Author: " + authorName + " || " + authorID, "Avatar: " + aviName + " || " + aviID, aviSource));
                                        }
                                }
                            }
                        var vrcalist = filesArray.Where(o => o.EndsWith(".vrca"));
                        if (vrcalist.Any()) foreach (string filepath in vrcalist)
                        {
                                LBSelectables.Add(("Author: ? (from .vrca)","Avatar: "+Path.GetFileNameWithoutExtension(filepath),filepath));
                        }
                        if (!LBSelectables.Any())
                        {
                            LBSelectables = new List<(string, string, string)>();
                            Debug.LogWarning("No .vrca or .vrcalog files in folder: "+ folderPath);
                            //no vrca or vrcalist in folder
                        }
                        else
                        {
                            LBSelectablesFiltered = LBSelectables; LBSelectablesSize = LBSelectablesFiltered.Count;
                            LBSelected = ("", "", "");
                        }
                    }
                    else
                    {
                        //no files in folder
                        Debug.LogWarning("No .vrca or .vrcalog files in folder: " + folderPath);
                    }
                }
            }

            if (LBSelectables.Any())
            {
                if (LBSelected.Item1 != "")
                {
                    EditorGUILayout.LabelField($"Selected Bundle:\n{LBSelected.Item1}\n{LBSelected.Item2}\nSource: {LBSelected.Item3}", FacsGUIStyles.helpbox);
                    LBSelected_Name = EditorGUILayout.TextField("Bundle Name:", LBSelected_Name);
                    if (GUILayout.Button("Load/Unload Asset Bundle", FacsGUIStyles.button, GUILayout.Height(40)))
                    {
                        if (LBSelected_isLoaded)
                        {
                            LoadBundleScript.OnDisable();
                        }
                        else
                        {
                            LoadBundleScript.AssetSource = LBSelected.Item3;
                            LoadBundleScript.Name = LBSelected_Name;
                            LoadBundleScript.RunLB();
                        }
                        LBSelected_isLoaded = !LBSelected_isLoaded;
                    }
                }

                string newinput = searchField.OnGUI(searchFieldinput);
                if (newinput != searchFieldinput)
                {
                    searchFieldinput = newinput;
                    if (String.IsNullOrEmpty(searchFieldinput)) { LBSelectablesFiltered = LBSelectables; }
                    else
                    {
                        LBSelectablesFiltered = LBSelectables.Where(o => o.Item1.Contains(searchFieldinput) || o.Item2.Contains(searchFieldinput)).ToList();
                    }
                    LBSelectablesSize = LBSelectablesFiltered.Count;
                    startingPos = 0;
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
                    var tmp = LBSelectablesFiltered.ElementAt(i);
                    //EditorGUILayout.LabelField($"{tmp.Item1}\n{tmp.Item2}", FacsGUIStyles.helpbox);
                    if (GUILayout.Button($"{tmp.Item1}\n{tmp.Item2}", FacsGUIStyles.button))
                    {
                        if (LBSelected_isLoaded)
                        {
                            LoadBundleScript.OnDisable();
                            LBSelected_isLoaded = false;
                        }
                        LBSelected = tmp;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }
        public void PlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                this.Close();
            }
        }
        void OnDestroy()
        {
            FacsGUIStyles = null;
            NullVars();
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
        }
        void NullVars()
        {
            searchField = null;
            if (LoadBundleScript != null) { LoadBundleScript.OnDisable(); }
            LoadBundleScript = null;
            DestroyImmediate(goHolder);
            goHolder = null;
        }
    }
}
#endif