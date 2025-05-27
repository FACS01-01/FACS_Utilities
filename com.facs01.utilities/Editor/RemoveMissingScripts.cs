#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class RemoveMissingScripts : EditorWindow 
    {
        private const string RichToolName = Logger.ToolTag + "[Remove Missing Scripts]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private static Vector2 scrollView = new();
        private static GameObject source;
        private static GameObject sourcePrefab;
        private static int GameObjectsScanned;
        private static int removedMissingScripts;
        private static string results;
        private static int scanTotal = 1;
        private static float scanned = 0;
        private static List<MonoBehaviour> MBTemp = null;
        private static System.Diagnostics.Stopwatch PBStopwatch = null;
        private const HideFlags sourcePrefabFlags = HideFlags.HideAndDontSave & ~HideFlags.DontUnloadUnusedAsset;

        [MenuItem("FACS Utils/Repair Project/Remove Missing Scripts", false, 1052)]
        [MenuItem("FACS Utils/Script/Remove Missing Scripts", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(RemoveMissingScripts), false, "Remove Missing Scripts", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); }
            EditorGUILayout.LabelField($"<color=cyan><b>Remove Missing Scripts</b></color>\n\n" +
                $"Scans the selected GameObject and deletes any missing scripts inside it's hierarchy.\n" +
                $"This operation can't be undone.\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                NullVars();
                if (source && PrefabUtility.IsPartOfPrefabAsset(source) && PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.Model)
                {
                    Logger.LogWarning($"{RichToolName} Can't edit {Logger.RichModelPrefab}: {Logger.AssetTag}{source.name}{Logger.EndTag}");
                    source = null;
                }
            }
            if (!source && (!string.IsNullOrEmpty(results) || MissingMBCollection.CanDisplay())) NullVars();

            if (source && GUILayout.Button("Scan!", FacsGUIStyles.Button, GUILayout.Height(40)))
            {
                NullVars();
                if (PrefabUtility.IsPartOfPrefabAsset(source))
                {
                    var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        sourcePrefab = PrefabUtility.LoadPrefabContents(prefabPath);
                        ApplyFlags(sourcePrefab.transform);
                        AssemblyReloadEvents.beforeAssemblyReload += NullVars;
                        EditorApplication.wantsToQuit += NullVarsB;
                    }
                }
                var Tlist = new List<Transform>();
                var scanSource = sourcePrefab ? sourcePrefab : source;
                scanSource.GetComponentsInChildren<Transform>(true, Tlist); scanTotal = Tlist.Count; scanned = 0;
                EditorUtility.DisplayProgressBar("FACS Utilities - Remove Missing Scripts", $"Scanning \"{scanSource.name}\"...", 0);
                MBTemp = new(); PBStopwatch = new(); PBStopwatch.Start();
                Scan(scanSource.transform);
                PBStopwatch.Stop(); PBStopwatch = null; MBTemp.Clear(); MBTemp = null;
                if (MissingMBCollection.CanDisplay()) MissingMBCollection.FinalizeNames();
                else
                {
                    MissingMBCollection.Clear();
                    ShowNotification(new GUIContent("No Missing Script Found!"));
                }
                EditorUtility.ClearProgressBar();
            }

            DisplayScan();

            if (source && GUILayout.Button("Run Fix!", FacsGUIStyles.Button, GUILayout.Height(40))) Run(source);
            if (!string.IsNullOrEmpty(results))
            {
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(results, FacsGUIStyles.Helpbox);
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter;
            }
        }
        
        private static void DisplayScan()
        {
            if (!MissingMBCollection.CanDisplay()) return;
            EditorGUILayout.LabelField($"<color=green><b>Missing Scripts Found</b></color>{(sourcePrefab ? " <color=yellow>[ReadOnly]</color>" : "")}:", FacsGUIStyles.Helpbox);
            FacsGUIStyles.Button.alignment = TextAnchor.MiddleLeft;
            scrollView = EditorGUILayout.BeginScrollView(scrollView);
            if (MissingMBCollection.FromPrefabsN > 0)
            {
                MissingMBCollection.FromPrefabsFold = GUILayout.Toggle(MissingMBCollection.FromPrefabsFold, MissingMBCollection.FromPrefabsName, FacsGUIStyles.Foldout);
                if (MissingMBCollection.FromPrefabsFold)
                {
                    foreach (var go in MissingMBCollection.FromPrefabs) PingeGOBtn(go);
                }
            }
            if (MissingMBCollection.FromInstanceN > 0)
            {
                MissingMBCollection.FromInstanceFold = GUILayout.Toggle(MissingMBCollection.FromInstanceFold, MissingMBCollection.FromInstanceName, FacsGUIStyles.Foldout);
                if (MissingMBCollection.FromInstanceFold)
                {
                    foreach (var go in MissingMBCollection.FromInstance) PingeGOBtn(go);
                }
            }
            if (MissingMBCollection.Collections.Count > 0)
            {
                foreach (var col in MissingMBCollection.Collections)
                {
                    col.foldout = GUILayout.Toggle(col.foldout, col.Name, FacsGUIStyles.Foldout);
                    if (col.foldout) foreach (var go in col.GOs) PingeGOBtn(go);
                }
            }
            EditorGUILayout.EndScrollView();
            FacsGUIStyles.Button.alignment = TextAnchor.MiddleCenter;
        }

        private static void PingeGOBtn(GameObject go)
        {
            if (!go) return;
            if (GUITools.TintedButton(Color.gray, $"  {go.name}", FacsGUIStyles.Button))
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }
        }

        private static void Scan(Transform t)
        {
            if (PBStopwatch.ElapsedMilliseconds >= 16)
            {
                EditorUtility.DisplayProgressBar("FACS Utilities - Remove Missing Scripts", $"Scanning \"{t.name}\"...", scanned / scanTotal);
                PBStopwatch.Restart();
            }
            scanned++;

            var gameObject = t.gameObject;
            MBTemp.Clear(); gameObject.GetComponents<MonoBehaviour>(MBTemp);
            if (MBTemp.Any(mb => !mb))
            {
                var tracker = ReflectionTools.CreateTracker(gameObject); if (tracker == null) return;
                foreach (var editor in tracker.activeEditors)
                {
                    var ms = editor.target;
                    if (!object.Equals(ms, null) && ms.GetType() == typeof(MonoBehaviour))
                    {
                        if (ReflectionTools.IsInstanceIDPartOfNonAssetPrefabInstance(ms.GetInstanceID()))
                        { MissingMBCollection.AddPrefab(gameObject); }
                        else
                        {
                            var yaml = ReflectionTools.ToYAMLString(ms);
                            if (ReflectionTools.TryGetScriptGUIDandFileIDfromYAML(yaml, out var guid, out var fileId))
                            {
                                if (MissingMBCollection.TryGet(guid, fileId, out var col)) col.Add(gameObject);
                                else MissingMBCollection.Add(guid, fileId, gameObject);
                            }
                            else MissingMBCollection.AddLost(gameObject);
                        }
                    }
                    DestroyImmediate(editor);
                }
                tracker.Destroy();
            }

            foreach (Transform child in t) Scan(child);
        }

        private void Run(GameObject src)
        {
            NullVars();
            if (PrefabUtility.IsPartOfPrefabAsset(src) && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(src))) FixPrefab(src);
            else
            {
                var srcRoot = PrefabUtility.IsPartOfPrefabInstance(src) ? PrefabUtility.GetNearestPrefabInstanceRoot(src) : src;
                var gos = srcRoot.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject).Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g));
                var gos_MA = gos.Where(g => PrefabUtility.GetPrefabAssetType(g) == PrefabAssetType.MissingAsset);
                var gos_nMA = gos.Where(g => PrefabUtility.GetPrefabAssetType(g) != PrefabAssetType.MissingAsset)
                    .Select(g => PrefabUtility.GetCorrespondingObjectFromSource(g)).Distinct();
                foreach (var go in gos_nMA) FixPrefab(go);
                foreach (var go in gos_MA)
                {
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                }
                removedMissingScripts += RemoveMissingScriptsRecursive(srcRoot, out GameObjectsScanned);
                source = srcRoot;
            }
            if (removedMissingScripts == 0)
            {
                ShowNotification(new GUIContent("No Missing Script Removed!"));
                NullVars(); return;
            }
            GenerateResults();
            Logger.Log($"{RichToolName} Finished removing {removedMissingScripts} missing scripts!");
        }

        private static void FixPrefab(GameObject src2)
        {
            var src = PrefabUtility.InstantiatePrefab(src2) as GameObject;
            var rmvCount = RemoveMissingScriptsRecursive(src, out var goScanned);
            removedMissingScripts += rmvCount; GameObjectsScanned += goScanned;
            if (rmvCount>0) PrefabUtility.ApplyPrefabInstance(src, InteractionMode.AutomatedAction);
            DestroyImmediate(src);
        }

        public static int RemoveMissingScriptsRecursive(GameObject go, out int goScanned)
        {
            return RemoveMissingScriptsRecursive(go.transform, out goScanned);
        }

        public static int RemoveMissingScriptsRecursive(Transform t, out int goScanned)
        {
            goScanned = 1;
            int rmvCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (Transform child in t)
            {
                rmvCount += RemoveMissingScriptsRecursive(child, out var _goScanned);
                goScanned += _goScanned;
            }
            return rmvCount;
        }

        private static void GenerateResults()
        {
            results = $"Results:\n";
            results += $"   • <color=green>Child GameObjects scanned:</color> {GameObjectsScanned}\n";
            results += $"   • <color=green>Missing scripts deleted:</color> {removedMissingScripts}\n";
        }

        private static void ApplyFlags(Transform t)
        {
            t.gameObject.hideFlags = sourcePrefabFlags;
            foreach (Transform child in t) ApplyFlags(child);
        }

        private void OnDestroy()
        {
            source = null;
            FacsGUIStyles = null;
            NullVars();
        }

        private static bool NullVarsB()
        {
            NullVars();
            return true;
        }

        private static void UnloadSourcePrefab()
        {
            if (sourcePrefab) PrefabUtility.UnloadPrefabContents(sourcePrefab);
            sourcePrefab = null;
            AssemblyReloadEvents.beforeAssemblyReload -= NullVars;
            EditorApplication.wantsToQuit -= NullVarsB;
        }

        private static void NullVars()
        {
            UnloadSourcePrefab();
            GameObjectsScanned = 0;
            removedMissingScripts = 0;
            results = null;
            MissingMBCollection.Clear();
            scanTotal = 1;
            scanned = 0;
        }

        private class MissingMBCollection
        {
            private static int nameIndex = 1;
            public static readonly List<MissingMBCollection> Collections = new();
            public static readonly HashSet<GameObject> FromPrefabs = new();
            public static int FromPrefabsN = 0;
            public static string FromPrefabsName = "";
            public static bool FromPrefabsFold = false;
            public static readonly HashSet<GameObject> FromInstance = new();
            public static int FromInstanceN = 0;
            public static string FromInstanceName = "";
            public static bool FromInstanceFold = false;

            public string Name;
            public string GUID;
            public long FileID;
            public HashSet<GameObject> GOs;
            public int missingCount = 1;
            public bool foldout = false;

            public static void Clear()
            {
                Collections.Clear();
                FromPrefabs.Clear(); FromInstance.Clear();
                nameIndex = 1; FromPrefabsN = FromInstanceN = 0;
                FromPrefabsName = FromInstanceName = "";
                FromPrefabsFold = FromInstanceFold = false;
            }

            public static bool TryGet(string GUID, long FileID, out MissingMBCollection col)
            {
                col = null;
                if (Collections.Count == 0) return false;
                foreach (var item in Collections)
                {
                    if (item.FileID == FileID && item.GUID == GUID)
                    {
                        col = item;
                        return true;
                    }
                }
                return false;
            }

            public static void Add(string guid, long fileId, GameObject go)
            {
                var col = new MissingMBCollection(guid, fileId, go);
                Collections.Add(col);
            }

            public static void AddPrefab(GameObject go)
            {
                FromPrefabs.Add(go); FromPrefabsN++;
            }

            public static void AddLost(GameObject go)
            {
                FromInstance.Add(go); FromInstanceN++;
            }

            public static void FinalizeNames()
            {
                FromPrefabsName = $"<b>Unknowns From Prefab</b> ({FromPrefabsN} scripts in {FromPrefabs.Count} GameObjects)";
                FromInstanceName = $"<b>Lost Sources</b> ({FromInstanceN} scripts in {FromInstance.Count} GameObjects)";
                foreach (var col in Collections)
                {
                    col.Name += $" ({col.missingCount} scripts in {col.GOs.Count} GameObjects)";
                }
            }

            public static bool CanDisplay()
            {
                return FromPrefabsN > 0 || FromInstanceN > 0 || Collections.Count > 0;
            }

            public void Add(GameObject go)
            {
                GOs.Add(go); missingCount++;
            }

            private MissingMBCollection(string guid, long fileId, GameObject go)
            {
                GUID = guid; FileID = fileId;
                Name = $"<b>Missing Type #{nameIndex}</b>"; nameIndex++;
                GOs = new() { go };
            }
        }
    }
}
#endif