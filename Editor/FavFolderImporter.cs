#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class FavFolderImporter : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;
        private static List<string> savedFolders;
        private static int savedFoldersSize;

        private static readonly string SavedData_file = "savedData.txt";
        private static readonly string SavedData_header = "FACS Utilities - Configs";
        private static readonly string SavedData_configParam = "FavFolderImporter.FavFolders:";

        [MenuItem("FACS Utils/Misc/Fav Folder Importer", false, 1003)]
        public static void ShowWindow()
        {
            GetWindow(typeof(FavFolderImporter), false, "Fav Folder Importer", true);
        }
        private void OnEnable()
        {
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
            AssetDatabase.importPackageFailed += OnImportPackageFailed;

            string gettingsavedfolders = SavedDataManager.ReadSavedData("", SavedData_file, SavedData_header, SavedData_configParam, true);
            if (gettingsavedfolders != "") { savedFolders = new List<string>(gettingsavedfolders.Split(new string[] { "||" }, StringSplitOptions.None)); }
            else { savedFolders = new List<string>(); }
            savedFoldersSize = savedFolders.Count;
        }
        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;
            FacsGUIStyles.button.contentOffset = new Vector2(x:1,y:0);

            EditorGUILayout.LabelField($"<color=cyan><b>Fav Folder Importer</b></color>\n\n" +
                $"Imports all Unity Packages in your selected folder automatically (overwritting pre existing files).\n" +
                $"Your saved folders are remembered across all projects.\n" +
                $"Packages are imported in alphabetical order, consider it if you have packages with dependencies between them.\n" +
                $"Packages starting with \"<b>.</b>\" aren't imported.\n", FacsGUIStyles.helpbox);

            if (savedFoldersSize>0)
            {
                for (int i = 0; i < savedFoldersSize; i++)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button($"<b>X</b>", FacsGUIStyles.button, GUILayout.Height(25), GUILayout.Width(25)))
                    {
                        // remove folder
                        savedFolders.RemoveAt(i); savedFoldersSize--; i--;
                        continue;
                    }
                    if (GUILayout.Button(savedFolders[i], FacsGUIStyles.button, GUILayout.Height(25)))
                    {
                        // use folder
                        string folderPath = savedFolders[i];
                        if (Directory.Exists(folderPath))
                        {
                            string[] files = Directory.GetFiles(folderPath, "*.unitypackage");
                            if (files.Length > 0)
                            {
                                List<string> filenames = new List<string>();
                                foreach (string file in files)
                                {
                                    string filename = Path.GetFileNameWithoutExtension(file);
                                    if (!filename.StartsWith(".")) { filenames.Add(filename); }
                                }
                                if (filenames.Count > 0)
                                {
                                    filenames.Sort();
                                    string dialog = "Packages that will be imported:\n\n";
                                    foreach (string file in filenames)
                                    {
                                        dialog += "   " + file + "\n";
                                    }
                                    if (EditorUtility.DisplayDialog("Fav Folder Importer", dialog, "Import All", "Cancel"))
                                    {
                                        foreach (string file in filenames)
                                        {
                                            AssetDatabase.ImportPackage(folderPath + "/" + file + ".unitypackage", false);
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[<color=green>Fav Folder Importer</color>] All Unity Packages in folder are marked not to be imported: {folderPath}\n");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[<color=green>Fav Folder Importer</color>] Selected folder doesn't have any Unity Package: {folderPath}\n");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[<color=green>Fav Folder Importer</color>] Selected folder no longer exist: {folderPath}\n");
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("Add a Favorite Folder", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                string folderPath = EditorUtility.OpenFolderPanel("Select Folder with Unity Packages", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "");
                if (!String.IsNullOrEmpty(folderPath))
                {
                    if (!savedFolders.Contains(folderPath))
                    {
                        savedFolders.Add(folderPath); savedFoldersSize++;
                    }
                    else { Debug.LogWarning($"[<color=green>Fav Folder Importer</color>] Selected folder already listed: {folderPath}\n"); }
                }
            }
        }
        private static void OnImportPackageCompleted(string packagename)
        {
            Debug.Log($"[<color=green>Fav Folder Importer</color>] Imported package: {packagename}\n");
        }
        private static void OnImportPackageFailed(string packagename, string errormessage)
        {
            Debug.Log($"[<color=green>Fav Folder Importer</color>] Failed importing package: {packagename} with error: {errormessage}\n");
        }
        void OnDisable()
        {
            FacsGUIStyles = null;
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            AssetDatabase.importPackageFailed -= OnImportPackageFailed;
            if (savedFolders.Any()) { SavedDataManager.WriteSavedData("", SavedData_file, SavedData_header, SavedData_configParam, String.Join("||", savedFolders)); }
            else { SavedDataManager.WriteSavedData("", SavedData_file, SavedData_header, SavedData_configParam, ""); }
        }
    }
}
#endif