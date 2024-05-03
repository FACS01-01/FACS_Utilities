#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    [FilePath("FACS01 Data/UPCImporter", FilePathAttribute.Location.PreferencesFolder)]
    internal class UPCImporterData : ScriptableSingleton<UPCImporterData>
    {
        private const string RichToolName = Logger.ToolTag + "[UPC Importer]" + Logger.EndTag;
        [SerializeField]
        internal List<UPCollection> collections;

        private UPCImporterData()
        {
            collections = new();
        }

        [System.Serializable]
        internal class UPCollection
        {
            [System.NonSerialized]
            public bool foldout = false;
            [System.NonSerialized]
            public Vector2 scrollView = default;

            [SerializeField]
            internal string collectionName;
            [SerializeField]
            internal List<UPContent> folders;
            [SerializeField]
            internal List<UPContent> packages;

            internal UPCollection(int i)
            {
                collectionName = "My Collection #" + i;
                folders = new(); packages = new();
            }

            internal void Import()
            {
                var UPList = new HashSet<string>();
                foreach (var f in folders)
                {
                    var fpath = System.IO.Path.GetFullPath(f.path);
                    if (!System.IO.Directory.Exists(fpath))
                    { Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}Folder{Logger.EndTag} not found: {fpath}"); continue; }
                    var ups = System.IO.Directory.GetFiles(fpath, "*.unitypackage", System.IO.SearchOption.AllDirectories);
                    if (ups.Length == 0)
                    { Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}Folder{Logger.EndTag} doesn't contain any {Logger.ConceptTag}Unity Package{Logger.EndTag}: {fpath}"); continue; }
                    foreach (var up in ups) UPList.Add(up);
                }
                foreach (var p in packages)
                {
                    var ppath = System.IO.Path.GetFullPath(p.path);
                    if (!System.IO.File.Exists(ppath))
                    { Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}Unity Package{Logger.EndTag} not found: {ppath}"); continue; }
                    UPList.Add(ppath);
                }
                if (UPList.Count == 0)
                {
                    Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}Collection{Logger.EndTag} {Logger.AssetTag}{collectionName}{Logger.EndTag}" +
                        $" didn't find any {Logger.ConceptTag}Unity Package{Logger.EndTag} to import."); return;
                }
                var UPNames = string.Join("\n", UPList.Select(up => System.IO.Path.GetFileNameWithoutExtension(up)));
                if (!EditorUtility.DisplayDialog("UnityPackage Collection Importer", $"Do you want to import {UPList.Count} packages into your project?\n\n" +
                    $"{UPNames}", "Yes", "No")) return;

                EditorApplication.LockReloadAssemblies(); AssetDatabase.DisallowAutoRefresh();
                var failedUP = "";
                try
                {
                    foreach (var UP in UPList)
                    {
                        failedUP = UP;
                        AssetDatabase.ImportPackage(UP, false);
                        Logger.Log($"{RichToolName} Imported {Logger.ConceptTag}Unity Package{Logger.EndTag}: {System.IO.Path.GetFileNameWithoutExtension(UP)}");
                    }
                    Logger.Log($"{RichToolName} Successfully imported <b>{UPList.Count}</b> {Logger.ConceptTag}Unity Packages{Logger.EndTag}!\n\n{UPNames}");
                }
                catch (System.Exception e)
                {
                    Logger.LogError($"{RichToolName} Failed to import {Logger.ConceptTag}Unity Package{Logger.EndTag}: {failedUP}\n{e}");
                }
                finally { EditorApplication.UnlockReloadAssemblies(); AssetDatabase.AllowAutoRefresh(); }
            }
        }

        [System.Serializable]
        internal class UPContent
        {
            [SerializeField]
            internal string path;
            [System.NonSerialized]
            private bool initWidth = false;
            [System.NonSerialized]
            private float _width = -1;
            internal float width
            {
                get
                {
                    if (!initWidth)
                    {
                        _width = GUI.skin.label.CalcSize(new GUIContent(path)).x;
                        initWidth = true;
                    }
                    return _width;
                }
            }

            internal UPContent(string str)
            {
                path = str; initWidth = false;
            }

            internal static bool IsIn(string str, IEnumerable<UPContent> ie)
            {
                foreach (var elem in ie) if (elem.path == str) return true;
                return false;
            }
        }

        internal static void SaveData()
        {
            instance.Save(true);
        }
    }
}
#endif