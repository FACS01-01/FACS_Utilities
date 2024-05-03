#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class UPCImporter : EditorWindow
    {
        private static readonly string[] unityPackageFilter = new string[] { "Unity Package", "unitypackage" };
        private static List<UPCImporterData.UPCollection> Collections => UPCImporterData.instance.collections;
        private static FACSGUIStyles FacsGUIStyles;
        private static Vector2 scrollView;
        private static int editing = -1;

        [MenuItem("FACS Utils/Miscellaneous/UPC Importer", false, 1100)]
        private static void ShowWindow()
        {
            var window = (UPCImporter)GetWindow(typeof(UPCImporter), false, "UPC Importer", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new();
                FacsGUIStyles.Button.clipping = TextClipping.Overflow;
                FacsGUIStyles.Button.contentOffset = new Vector2(1, 0);
                FacsGUIStyles.Foldout.fontSize = 12;}
            var windowWidth = this.position.size.x;

            EditorGUILayout.LabelField($"<color=cyan><b>UnityPackage Collection Importer</b></color>\n\n" +
                $"Imports all Unity Packages from your personal collection (overwritting pre existing files).\n" +
                $"Your collections are shared across all projects.\n", FacsGUIStyles.Helpbox);

            if (Collections.Count == 0) Collections.Add(new(1));
            scrollView = EditorGUILayout.BeginScrollView(scrollView);
            for (int i = 0; i < Collections.Count; i++)
            {
                var coll = Collections[i];
                EditorGUILayout.BeginHorizontal();
                if (editing == -1)
                {
                    if (i != 0 && GUILayout.Button("<b>\u2191</b>", FacsGUIStyles.Button, GUILayout.Width(13)))
                    { Collections.RemoveAt(i); Collections.Insert(i - 1, coll); }
                    if (i != Collections.Count - 1 && GUILayout.Button("<b>\u2193</b>", FacsGUIStyles.Button, GUILayout.Width(13)))
                    { Collections.RemoveAt(i); Collections.Insert(i + 1, coll); }
                }

                if (editing == i)
                {
                    coll.collectionName = EditorGUILayout.TextField(coll.collectionName);
                    var e = Event.current;
                    if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return)) { editing = -1; e.Use(); }
                }
                else EditorGUILayout.LabelField("<b>" + coll.collectionName + "</b>", FacsGUIStyles.Label);

                if (editing == -1)
                {
                    var importBtnColor = (coll.folders.Count == 0 && coll.packages.Count == 0) ? Color.gray : Color.green;
                    if (GUITools.TintedButton(importBtnColor, GUITools.ImportIcon, GUILayout.ExpandWidth(false)) &&
                        importBtnColor == Color.green) { coll.Import(); }
                    if (GUILayout.Button(GUITools.EditIcon, GUILayout.ExpandWidth(false))) editing = i;
                    if (GUITools.TintedButton(Color.red, "X", FacsGUIStyles.ButtonSmall, GUILayout.Width(20), GUILayout.Height(20))) { Collections.RemoveAt(i); i--; }
                }
                EditorGUILayout.EndHorizontal();
                
                if (editing == -1)
                {
                    coll.foldout = GUILayout.Toggle(coll.foldout, $"{coll.folders.Count} folders and {coll.packages.Count} single packages:", FacsGUIStyles.Foldout);
                    if (coll.foldout)
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("+ Folder", FacsGUIStyles.Button, GUILayout.MaxWidth(windowWidth / 2)))
                        {
                            var folderPath = EditorUtility.OpenFolderPanel("Add Package to " + coll.collectionName, "", "");
                            if (!string.IsNullOrEmpty(folderPath) && !UPCImporterData.UPContent.IsIn(folderPath, coll.folders))
                            { coll.folders.Add(new(folderPath)); }
                        }
                        if (GUILayout.Button("+ Package", FacsGUIStyles.Button, GUILayout.MaxWidth(windowWidth / 2)))
                        {
                            var packagePath = EditorUtility.OpenFilePanelWithFilters("Add Package to "+coll.collectionName, "", unityPackageFilter);
                            if (!string.IsNullOrEmpty(packagePath) &&!UPCImporterData.UPContent.IsIn(packagePath, coll.packages))
                            { coll.packages.Add(new(packagePath)); }
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.BeginVertical(GUILayout.Width(20));
                        for (int j = 0; j < coll.folders.Count; j++)
                        {
                            if (GUITools.TintedButton(Color.red, "X", FacsGUIStyles.ButtonSmall, GUILayout.Width(20), GUILayout.Height(19))) { coll.folders.RemoveAt(j); j--; }
                        }
                        for (int j = 0; j < coll.packages.Count; j++)
                        {
                            if (GUITools.TintedButton(Color.red, "X", FacsGUIStyles.ButtonSmall, GUILayout.Width(20), GUILayout.Height(19))) { coll.packages.RemoveAt(j); j--; }
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginVertical();
                        coll.scrollView = EditorGUILayout.BeginScrollView(coll.scrollView, GUILayout.ExpandHeight(false));
                        for (int j = 0; j < coll.folders.Count; j++) EditorGUILayout.LabelField(coll.folders[j].path, GUILayout.Width(coll.folders[j].width));
                        for (int j = 0; j < coll.packages.Count; j++) EditorGUILayout.LabelField(coll.packages[j].path, GUILayout.Width(coll.packages[j].width));
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            FacsGUIStyles.Button.contentOffset = new Vector2(0, -0.5f);
            if (GUILayout.Button("<b><size=20>+</size></b>", FacsGUIStyles.Button, GUILayout.MaxWidth(60), GUILayout.Height(25))) Collections.Add(new(Collections.Count + 1));
            FacsGUIStyles.Button.contentOffset = new Vector2(1, 0);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void OnDisable()
        {
            UPCImporterData.SaveData();
        }
    }
}
#endif