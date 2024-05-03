#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class FACSLoadBundle : EditorWindow
    {
        private static readonly Color hoverColor = new(0.5f, 0.5f, 0.5f, 0.3f);
        private static readonly string[] FilePanelFilters = { "All Files", "*", "AssetBundle Files", "unity3d", "VRChat Files", "vrca,vrcw" };
        internal static GUIContent SceneAssetIcon;
        private static FACSGUIStyles FacsGUIStyles;
        private static Vector2 scrollView;
        private static List<FACSLoadBundleData.LoadBundleEntry> LoadBundleEntries => FACSLoadBundleData.instance.LoadBundleEntries;
        private static readonly List<FACSLoadBundleData.LoadBundleEntry> ProcessingBundleEntries = new();
        internal static FACSLoadBundle instance;
        private static bool preventSelection = false;

        [MenuItem("FACS Utils/Asset Bundle/FACS Load Bundle", false, 1101)]
        private static void ShowWindow()
        {
            var window = (FACSLoadBundle)GetWindow(typeof(FACSLoadBundle), false, "FACS Load Bundle", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnEnable()
        {
            instance = this;
            SceneAssetIcon = new GUIContent(AssetPreview.GetMiniTypeThumbnail(typeof(SceneAsset)));
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles();
                FacsGUIStyles.Button.fontSize = 20;
                FacsGUIStyles.Button.contentOffset = new Vector2(0, -0.5f);
            }
            EditorGUILayout.LabelField($"<color=cyan><b>FACS Load Bundle</b></color>\n\n" +
                $"Playtest Assets and Scenes from Asset Bundles!\n\n" +
                $"Put the bundle's file path or URL into Bundle Source, click the number to the left and it will be loaded into memory.\n" +
                $"Double click an Asset/Scene to load it from it's bundle.\n\n" +
                $"For Assets, you can do Drag & Drop them and temporarily add them to other assets and scenes.\n", FacsGUIStyles.Helpbox);

            DisplayProgressBars();
            DisplayLBEntries();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("<b>+</b>", FacsGUIStyles.Button, GUILayout.MaxWidth(60), GUILayout.Height(25))) LoadBundleEntries.Add(new());
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DisplayProgressBars()
        {
            for (int i = 0; i < LoadBundleEntries.Count; i++)
            {
                var entry = LoadBundleEntries[i];
                if (entry.progress != -1)
                {
                    if (entry.isSceneBundle)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(SceneAssetIcon, GUILayout.Width(20));
                    }
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), entry.progress, "Loading AssetBundle #" + (i + 1));
                    if (entry.isSceneBundle) EditorGUILayout.EndHorizontal();
                }
            }
        }

        internal static void AddToProcessQ(FACSLoadBundleData.LoadBundleEntry entry)
        {
            ProcessingBundleEntries.Add(entry);
            EditorApplication.update -= ProcessAsync;
            EditorApplication.update += ProcessAsync;
        }

        private static void DisplayLBEntries()
        {
            var e = Event.current;
            scrollView = EditorGUILayout.BeginScrollView(scrollView);
            for (int i = 1; i < LoadBundleEntries.Count + 1; i++)
            {
                var entry = LoadBundleEntries[i - 1];
                EditorGUILayout.BeginHorizontal();
                if (entry.loadedAssetBundle)
                {
                    if (GUITools.TintedButton(Color.green, i.ToString(), FacsGUIStyles.ButtonSmall, GUILayout.Width(22), GUILayout.Height(20))) entry.Unload(true);
                }
                else
                {
                    if (GUILayout.Button(i.ToString(), FacsGUIStyles.ButtonSmall, GUILayout.Width(22), GUILayout.Height(20)))
                    {
                        SubscribeUnloadAll();
                        entry.process = entry.BeginLoadBundle();
                        AddToProcessQ(entry);
                    }
                }
                if (GUILayout.Button("Bundle Source", FacsGUIStyles.ButtonSmall, GUILayout.Width(94), GUILayout.Height(20)))
                {
                    string bundlePath = EditorUtility.OpenFilePanelWithFilters("Select Asset Bundle", string.Empty, FilePanelFilters);
                    if (!string.IsNullOrEmpty(bundlePath))
                    { GUIUtility.keyboardControl = 0; entry.BundleSource = bundlePath; }
                }
                entry.BundleSource = EditorGUILayout.TextField(entry.BundleSource, GUILayout.Height(20));
                if (GUITools.TintedButton(Color.red, "X", FacsGUIStyles.ButtonSmall, GUILayout.Width(20), GUILayout.Height(20)))
                {
                    if (entry.loadedAssetBundle || entry.process != null) entry.Unload(true);
                    LoadBundleEntries.RemoveAt(i - 1); i--;
                    if (LoadBundleEntries.Count == 0) LoadBundleEntries.Add(new());
                }
                EditorGUILayout.EndHorizontal();

                if (entry.loadedAssetBundle)
                {
                    //display assets list
                    var foldoutHeader = $"{entry.assetNames.Length} {(entry.isSceneBundle ? "Scene": "Asset")}{(entry.assetNames.Length>1?"s":"")} Available:";
                    entry.assetNamesFoldout = EditorGUILayout.Foldout(entry.assetNamesFoldout, foldoutHeader, true);

                    if (entry.assetNamesFoldout)
                    {
                        for (int j = 0; j < entry.assetNameDisplays.Length; j++)
                        {
                            Object asset = null; GUIContent icon = null; string displayName;
                            var isLoaded = entry.loadedAssets.TryGetValue(j, out var obj_cont_disp);
                            if (isLoaded) { asset = obj_cont_disp.asset; icon = obj_cont_disp.icon;  }
                            if (isLoaded && entry.editingDisplayName != j) displayName = obj_cont_disp.display;
                            else displayName = entry.assetNameDisplays[j];
                            EditorGUILayout.BeginHorizontal();
                            var rect = EditorGUILayout.GetControlRect();
                            if (rect.Contains(e.mousePosition))
                            {
                                EditorGUI.DrawRect(rect, hoverColor);
                                if (entry.editingDisplayName != j)
                                {
                                    switch (e.type)
                                    {
                                        case EventType.MouseDown:
                                            preventSelection = false;
                                            if (e.clickCount == 2)
                                            {
                                                preventSelection = entry.ToggleAsset(j);
                                            }
                                            e.Use();
                                            break;
                                        case EventType.MouseUp:
                                            if (asset && !preventSelection)
                                            {
                                                Selection.activeObject = asset;
                                            }
                                            e.Use();
                                            break;
                                        case EventType.MouseDrag:
                                            if (!entry.isSceneBundle && e.button == 0 && asset)
                                            {
                                                DragAndDrop.PrepareStartDrag(); var draggedObj = new Object[1];
                                                asset.name = entry.assetNameDisplays[j];
                                                draggedObj[0] = asset;
                                                DragAndDrop.objectReferences = draggedObj;
                                                if (asset is GameObject)
                                                {
                                                    var sceneDropper = (DragAndDrop.SceneDropHandler)SceneDropper;
                                                    var hierarchyDropper = (DragAndDrop.HierarchyDropHandler)HierarchyDropper;
                                                    if (!DragAndDrop.HasHandler(DragAndDropWindowTarget.sceneView, sceneDropper))
                                                        DragAndDrop.AddDropHandler(SceneDropper);
                                                    if (!DragAndDrop.HasHandler(DragAndDropWindowTarget.hierarchy, hierarchyDropper))
                                                        DragAndDrop.AddDropHandler(HierarchyDropper);
                                                    DragAndDrop.SetGenericData("FACSLoadBundle_RejectPrefab", true);
                                                }
                                                DragAndDrop.StartDrag(asset.name);
                                                preventSelection = true;
                                                e.Use();
                                            }
                                            break;
                                    }
                                }
                            }
                            if (icon?.image != null) EditorGUI.LabelField(rect, icon, FacsGUIStyles.Label);
                            if (entry.editingDisplayName == -1 && GUILayout.Button(GUITools.EditIcon, GUILayout.ExpandWidth(false))) entry.editingDisplayName = j;
                            if (entry.editingDisplayName != j) EditorGUI.LabelField(rect, displayName, FacsGUIStyles.Label);
                            else
                            {
                                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                                { entry.editingDisplayName = -1; e.Use(); }
                                else
                                {
                                    var newDisplay = EditorGUI.DelayedTextField(rect, displayName);
                                    if (newDisplay != displayName)
                                    {
                                        entry.editingDisplayName = -1;
                                        if (entry.ChangeDisplayName(newDisplay, j, out newDisplay))
                                        {
                                            if (asset) asset.name = newDisplay;
                                        }
                                        else if (asset) asset.name = newDisplay;
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }   //END display assets list
                GUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();
        }

        private static DragAndDropVisualMode SceneDropper(Object _0, Vector3 _1, Vector2 _2, Transform _3, bool perform)
        {
            return RejectPrefabDrop(perform);
        }

        private static DragAndDropVisualMode HierarchyDropper(int _0, HierarchyDropFlags _1, Transform _2, bool perform)
        {
            return RejectPrefabDrop(perform);
        }

        private static DragAndDropVisualMode RejectPrefabDrop(bool perform)
        {
            var data = DragAndDrop.GetGenericData("FACSLoadBundle_RejectPrefab");
            if (data == null || data is not bool b || b == false) return DragAndDropVisualMode.None;

            if (perform)
            {
                DragAndDrop.objectReferences = new Object[0];
                DragAndDrop.RemoveDropHandler(SceneDropper);
                DragAndDrop.RemoveDropHandler(HierarchyDropper);
            }
            return DragAndDropVisualMode.Rejected;
        }

        private static void SubscribeUnloadAll()
        {
            UnsubscribeUnloadAll();
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += UnloadAll;
            EditorApplication.wantsToQuit += UnloadAllQuitting;
        }

        private static void UnsubscribeUnloadAll()
        {
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= UnloadAll;
            EditorApplication.wantsToQuit -= UnloadAllQuitting;
        }

        internal static void TryUnsubscribeUnloadAll()
        {
            foreach (var entry in LoadBundleEntries)
            {
                if (entry.loadedAssetBundle) return;
            }
            UnsubscribeUnloadAll();
        }

        private static void PlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || 
                state == PlayModeStateChange.ExitingPlayMode) UnloadAll();
        }

        private static void UnloadAll()
        {
            foreach (var entry in LoadBundleEntries)
            {
                if (entry.loadedAssetBundle || entry.process != null) entry.Unload();
            }

            if (!Application.isPlaying) EditorSceneManager.MarkAllScenesDirty();
            UnsubscribeUnloadAll();
        }

        private static bool UnloadAllQuitting()
        {
            UnloadAll();
            foreach (var tag in UnityEditorInternal.InternalEditorUtility.tags)
                if (tag.StartsWith("FACS01LB_")) UnityEditorInternal.InternalEditorUtility.RemoveTag(tag);
            return true;
        }

        internal static void ProcessAsync()
        {
            for (int i = 0; i < ProcessingBundleEntries.Count; i++)
            {
                var entry = ProcessingBundleEntries[i];
                if (entry.process == null || !entry.process.MoveNext())
                {
                    entry.process = null; entry.progress = -1;
                    entry.loadingAsset = -1;
                    ProcessingBundleEntries.RemoveAt(i); i--;
                    if (instance) instance.Repaint();
                    if (ProcessingBundleEntries.Count == 0) EditorApplication.update -= ProcessAsync;
                }
            }
        }

        private void OnDisable()
        {
            FACSLoadBundleData.SaveData();
            instance = null;
        }
    }
}
#endif