#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace FACS01.Utilities
{
    public class FixVRC3Expressions : EditorWindow
    {
        public DefaultAsset folderWithExpressions;

        private static FACSGUIStyles FacsGUIStyles;
        private readonly string[] searchFiles = { "VRCExpressionParameters", "VRCExpressionsMenu" };
        private readonly string[] searchIn = { "Assets/VRCSDK" };
        private Dictionary<string, (string, string)> searchFiles_IDs;
        private int fixCount;
        private string output_print;

        [MenuItem("FACS Utils/Repair Avatar/Fix VRC3 Expressions", false, 2)]
        public static void ShowWindow2()
        {
            GetWindow(typeof(FixVRC3Expressions), false, "Fix VRC3 Expressions", true);
        }
        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"<color=cyan><b>Fix VRC SDK3 Expression Menus and Parameters</b></color>\n\nScans the selected folder and assigns the correct scripts to broken Expression Menus and Parameters.\n", FacsGUIStyles.helpbox);
            folderWithExpressions = (DefaultAsset)EditorGUILayout.ObjectField(folderWithExpressions, typeof(DefaultAsset), false, GUILayout.Height(50));

            if (GUILayout.Button("Run Fix!", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                if (folderWithExpressions != null)
                {
                    Debug.Log("FIX VRC3 EXPRESSIONS - BEGINS");

                    fixCount = 0;
                    output_print = "";
                    searchFiles_IDs = new Dictionary<string, (string, string)>();

                    Run();

                    string[] filePaths = Directory.GetFiles(AssetDatabase.GetAssetPath(folderWithExpressions), "*.asset", SearchOption.AllDirectories);

                    foreach (string filePath in filePaths)
                    {
                        string path2 = filePath.Replace('\\', '/');
                        if (IsYAML(path2)) { Fix(path2); AssetDatabase.Refresh(); }
                    }

                    output_print = $"Results:\n   • <color=green>Fixed files:</color> {fixCount}\n";
                    Debug.Log("FIX VRC3 EXPRESSIONS - FINISHED");
                }
                else
                {
                    ShowNotification(new GUIContent("Empty field?"));
                }

            }
            if (output_print != null && output_print != "")
            {
                FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(output_print, FacsGUIStyles.helpbox);
            }
        }
        private void Fix(string path)
        {
            string[] arrLine = File.ReadAllLines(path);
            int mScriptIndex = ArrayUtility.IndexOf(arrLine, arrLine.First(o => o.StartsWith("  m_Script: ")));
            if (arrLine.Contains("  parameters:"))
            {
                string goodFile = "VRCExpressionParameters";
                string fileGUID = searchFiles_IDs[goodFile].Item1;
                string fileID = searchFiles_IDs[goodFile].Item2;
                arrLine[mScriptIndex] = $"  m_Script: {{fileID: {fileID}, guid: {fileGUID}, type: 3}}";
                File.WriteAllLines(path, arrLine);
                fixCount++;
            }
            else if (arrLine.Contains("  controls:"))
            {
                string goodFile = "VRCExpressionsMenu";
                string fileGUID = searchFiles_IDs[goodFile].Item1;
                string fileID = searchFiles_IDs[goodFile].Item2;
                arrLine[mScriptIndex] = $"  m_Script: {{fileID: {fileID}, guid: {fileGUID}, type: 3}}";
                File.WriteAllLines(path, arrLine);
                fixCount++;
            }
        }
        private void Run()
        {
            List<string> searchFiles_Paths = new List<string>();
            foreach (string goodFile in searchFiles)
            {
                string[] GUIDList = AssetDatabase.FindAssets(goodFile, searchIn);
                foreach (string GUID in GUIDList)
                {
                    string path = AssetDatabase.GUIDToAssetPath(GUID);
                    if (!searchFiles_Paths.Contains(path)) searchFiles_Paths.Add(path);
                }
            }
            foreach (string file_path in searchFiles_Paths)
            {
                Object[] assemblyObjects = AssetDatabase.LoadAllAssetRepresentationsAtPath(file_path);
                foreach (Object assemblyObject in assemblyObjects)
                {
                    if (searchFiles.Contains(assemblyObject.name) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assemblyObject, out string dllGuid, out long dllFileId))
                    {
                        if (!searchFiles_IDs.ContainsKey(assemblyObject.name)) searchFiles_IDs.Add(assemblyObject.name,(dllGuid, dllFileId.ToString()));
                    }
                }
            }
        }
        static bool IsYAML(string path)
        {
            if (!File.Exists(path)) return false;

            StreamReader sr = new StreamReader(path);
            if (sr.Peek() >= 0)
            {
                string header = sr.ReadLine();
                if (header.Contains("%YAML 1.1"))
                {
                    sr.Close();
                    return true;
                }
                else return false;
            }
            else
            {
                sr.Close();
                return false;
            }
        }
        void OnDestroy()
        {
            output_print = null;
            searchFiles_IDs = null;
            FacsGUIStyles = null;
        }
    }
}
#endif
