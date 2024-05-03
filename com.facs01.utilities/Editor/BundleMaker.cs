#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class BundleMaker : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Bundle Maker]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private static bool makeScenesBundle;
        private static string saveFolder;
        private static string bundleName;
        private static Compression compression = Compression.DefaultCompression;
        private static List<AssetItem> selectedAssets = new() { new() };
        private static readonly List<int> selectionCleanup = new();
        private static Vector2 scrollView = new();

        private static BuildTarget[] buildTargets;
        private static string[] buildTargetNames;

        [MenuItem("FACS Utils/Asset Bundle/Bundle Maker", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(BundleMaker), false, "Bundle Maker", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnEnable()
        {
            saveFolder = string.IsNullOrEmpty(saveFolder) ? Application.temporaryCachePath : saveFolder;
            buildTargets =
            (from bt in System.Enum.GetValues(typeof(BuildTarget)) as BuildTarget[]
             where BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(bt), bt)
             orderby bt.ToString()
             select bt).ToArray();
            buildTargetNames = buildTargets.Select(bt => bt.ToString()).ToArray();
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Button.wordWrap = true; }
            EditorGUILayout.LabelField($"<color=cyan><b>Bundle Maker</b></color>\n\n" +
                $"Creates an Asset Bundle from a group of Assets, or from a group of Scenes\n", FacsGUIStyles.Helpbox);
            var windowWidth = this.position.size.x;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            var activebuildtarget = EditorUserBuildSettings.activeBuildTarget;
            var activeBuildTargetIndex = System.Array.IndexOf(buildTargets, activebuildtarget);
            EditorGUILayout.LabelField("Build Target:", FacsGUIStyles.Label, GUILayout.Width(90));
            EditorGUI.BeginChangeCheck();
            var selectedIndex = EditorGUILayout.Popup(activeBuildTargetIndex, buildTargetNames);
            if (EditorGUI.EndChangeCheck() && selectedIndex != activeBuildTargetIndex)
            {
                var newBuildTarget = buildTargets[selectedIndex];
                if (EditorUtility.DisplayDialog("FACS01 Utilities - Bundle Maker",
                    $"Do you want to change your project's Build Target from \"{activebuildtarget}\" to \"{newBuildTarget}\"?\n" +
                    $"This will take some time.",
                    "Yes", "No"))
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(newBuildTarget), newBuildTarget);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Compression:", FacsGUIStyles.Label, GUILayout.Width(90));
            compression = (Compression)EditorGUILayout.EnumPopup(compression);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            if (GUILayout.Button($"<b>Save Folder</b>:\n{saveFolder}", FacsGUIStyles.Button))
            {
                string newFolderPath = EditorUtility.OpenFolderPanel("Select Folder", saveFolder, "");
                if (!string.IsNullOrEmpty(newFolderPath)) saveFolder = newFolderPath;
            }
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUITools.ToggleButton(!makeScenesBundle, "Assets Bundle", GUILayout.Height(40), GUILayout.MaxWidth(windowWidth/2)))
            {
                if (makeScenesBundle) NullVars();
                else SelectionCleanup();
                makeScenesBundle = false;
            }
            if (GUITools.ToggleButton(makeScenesBundle, "Scenes Bundle", GUILayout.Height(40), GUILayout.MaxWidth(windowWidth/2)))
            {
                if (!makeScenesBundle) NullVars();
                else SelectionCleanup();
                makeScenesBundle = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Bundle Name: ", FacsGUIStyles.Label, GUILayout.Width(90));
            EditorGUI.BeginChangeCheck();
            bundleName = EditorGUILayout.DelayedTextField(bundleName);
            if (EditorGUI.EndChangeCheck()) bundleName = SanitizeFileName(bundleName);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Assets", FacsGUIStyles.Helpbox, GUILayout.MaxWidth(windowWidth/2));
            EditorGUILayout.LabelField("Addresses", FacsGUIStyles.Helpbox, GUILayout.MaxWidth(windowWidth/2));
            EditorGUILayout.EndHorizontal();
            scrollView = EditorGUILayout.BeginScrollView(scrollView);
            var shouldCleanup = false;
            for (int i = 0; i < selectedAssets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                Object selAsset = makeScenesBundle ? EditorGUILayout.ObjectField(selectedAssets[i].obj, typeof(SceneAsset), false, GUILayout.MaxWidth(windowWidth/2)) :
                    EditorGUILayout.ObjectField(selectedAssets[i].obj, typeof(Object), false, GUILayout.MaxWidth(windowWidth/2));
                if (EditorGUI.EndChangeCheck()) shouldCleanup = true;

                if (selectedAssets[i].obj != selAsset)
                {
                    if (selAsset == null) { selectedAssets[i] = new(); EditorGUILayout.EndHorizontal(); continue; }
                    if (!makeScenesBundle && selAsset is SceneAsset)
                    {
                        Logger.LogWarning($"{RichToolName} Adding a {Logger.TypeTag}Scene{Logger.EndTag} asset to an {Logger.ConceptTag}Assets Bundle{Logger.EndTag} is not allowed.");
                        EditorGUILayout.EndHorizontal(); continue;
                    }
                    var path = AssetDatabase.GetAssetPath(selAsset);
                    if (!AssetDatabase.IsMainAsset(selAsset))
                    {
                        Logger.LogWarning($"{RichToolName} {Logger.AssetTag}\"{selAsset.name}\" [{selAsset.GetType().Name}]{Logger.EndTag} is not the main asset at \"{path}\".");
                        EditorGUILayout.EndHorizontal(); continue;
                    }
                    if (string.IsNullOrEmpty(path))
                    {
                        Logger.LogWarning($"{RichToolName} {Logger.AssetTag}{selAsset.name}{Logger.EndTag} needs to be saved to an asset file first.");
                        EditorGUILayout.EndHorizontal(); continue;
                    }
                    selectedAssets[i].obj = selAsset;
                    selectedAssets[i].objPath = path;
                }
                selectedAssets[i].addr = EditorGUILayout.TextField(selectedAssets[i].addr, GUILayout.MaxWidth(windowWidth/2));
                EditorGUILayout.EndHorizontal();
            }
            if (shouldCleanup) SelectionCleanup();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(bundleName) && selectedAssets.Count > 1)
            {
                if (GUILayout.Button($"Generate \"{bundleName}\"!", FacsGUIStyles.Button, GUILayout.Height(40)))
                {
                    GenerateBundle();
                }
            }
        }

        private static void GenerateBundle()
        {
            if (!Directory.Exists(saveFolder))
            {
                Logger.LogError($"{RichToolName} Save Folder not found: \"{saveFolder}\""); return;
            }
            var saveLocation = Path.GetFullPath(Path.Combine(saveFolder, bundleName));
            if (File.Exists(saveLocation))
            {
                if (!EditorUtility.DisplayDialog("FACS01 Utilities - Bundle Maker", "A file with the same name already exists " +
                    "in the destination folder.\nDo you want to overwrite it?", "Yes", "No"))
                { Logger.Log(RichToolName + " Bundle generation was cancelled."); return; }
            }

            AssetDatabase.SaveAssets();
            var items = selectedAssets.Where(s => s.obj);
            var assetPaths = items.Select(i => i.objPath).ToArray();
            var assetAddresses = items.Select(i => i.addr).ToArray();
            var bundleData = new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = assetPaths,
                addressableNames = assetAddresses
            };
            var options = BuildAssetBundleOptions.ForceRebuildAssetBundle;
            if (compression == Compression.Uncompressed) options |= BuildAssetBundleOptions.UncompressedAssetBundle;
            else if (compression == Compression.ChunkBasedCompression) options |= BuildAssetBundleOptions.ChunkBasedCompression;

            var tempLoc = Path.GetTempPath();
            var tempbundle = Path.GetFullPath(Path.Combine(tempLoc, bundleName));
            if (File.Exists(tempbundle)) File.Delete(tempbundle);

            try
            {
                BuildPipeline.BuildAssetBundles(tempLoc, new AssetBundleBuild[1] { bundleData }, options, EditorUserBuildSettings.activeBuildTarget);
            }
            catch (System.Exception e)
            {
                Logger.LogError($"{RichToolName} Exception while generating new bundle.\n{e}");
                return;
            }

            if (!File.Exists(tempbundle))
            {
                Logger.LogWarning($"{RichToolName} {Logger.RichAssetBundle} was not generated. Check Console.");
                return;
            }

            if (tempbundle != saveLocation)
            {
                if (File.Exists(saveLocation)) File.Delete(saveLocation);
                File.Move(tempbundle, saveLocation);
            }
            Logger.Log($"{RichToolName} Bundle \"{bundleName}\" was created successfully!\n{Logger.RichAssetBundle} Manifest located at: \"{tempbundle}.manifest\"");
        }

        private static void SelectionCleanup()
        {
            var distincts = new HashSet<Object>();
            for (int i = 0; i < selectedAssets.Count; i++)
            {
                if ((!selectedAssets[i].obj && i != selectedAssets.Count - 1) || !distincts.Add(selectedAssets[i].obj))
                    selectionCleanup.Insert(0, i);
            }
            if (selectionCleanup.Count > 0)
            {
                foreach (var removeI in selectionCleanup) selectedAssets.RemoveAt(removeI);
            }
            if (selectedAssets[^1].obj) selectedAssets.Add(new());
            selectionCleanup.Clear();
        }

        private string SanitizeFileName(string input)
        {
            var tmp = Regex.Replace(input, @"[^a-zA-Z0-9 \._-]", "").Trim();
            return Regex.Replace(tmp, @"^[.\s]+|[.\s]+$", "");
        }

        private void OnDestroy()
        {
            FacsGUIStyles = null;
            makeScenesBundle = false;
            bundleName = null;
            buildTargets = null;
            buildTargetNames = null;
            NullVars();
        }

        private void NullVars()
        {
            selectedAssets = new() { new() };
        }

        private class AssetItem
        {
            public Object obj;
            public string objPath;
            public string addr = "";
        }

        private enum Compression
        {
            [Tooltip("Fastest asset access but largest size.")]
            Uncompressed,
            [Tooltip("aka LZ4HC. Medium size with good access speed.")]
            ChunkBasedCompression,
            [Tooltip("aka LZMA. Smallest size but slower access.")]
            DefaultCompression
        }
    }
}
#endif