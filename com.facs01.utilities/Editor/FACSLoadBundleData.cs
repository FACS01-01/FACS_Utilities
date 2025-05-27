#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace FACS01.Utilities
{
    [FilePath("FACS01 Data/FACSLoadBundle", FilePathAttribute.Location.ProjectFolder)]
    internal class FACSLoadBundleData : ScriptableSingleton<FACSLoadBundleData>
    {
        private const string RichToolName = Logger.ToolTag + "[FACS Load Bundle]" + Logger.EndTag;
        internal const string FLBTag = "FACS01LB_";
        [SerializeField]
        internal List<LoadBundleEntry> LoadBundleEntries;
        
        private FACSLoadBundleData()
        {
            LoadBundleEntries = new() { new() };
        }

        [System.Serializable]
        internal class LoadBundleEntry
        {
            internal IEnumerator process;
            private UnityWebRequest bundleWebRequest;
            private AssetBundleCreateRequest bundleFileRequest;
            [System.NonSerialized]
            internal AssetBundle loadedAssetBundle;
            private AsyncOperation ao;
            [System.NonSerialized]
            internal float progress = -1;
            [System.NonSerialized]
            internal string[] assetNames;
            [System.NonSerialized]
            internal string[] assetNameDisplays;
            [System.NonSerialized]
            internal bool assetNamesFoldout = false;
            [System.NonSerialized]
            internal Dictionary<int, (Object asset, GUIContent icon, string display)> loadedAssets = new();
            [System.NonSerialized]
            internal int loadingAsset = -1;
            [System.NonSerialized]
            internal bool isSceneBundle;
            [System.NonSerialized]
            internal int editingDisplayName = -1;
            [System.NonSerialized]
            private List<GameObject> instantiated = new();

            [SerializeField]
            private string lastBundleSource = "";
            [SerializeField]
            private string bundleSource;
            internal string BundleSource
            {
                get { return bundleSource; }
                set
                {
                    if (bundleSource != value)
                    {
                        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(bundleSource) ||
                            IsURL(bundleSource) || IsURL(value) ||
                            System.IO.Path.GetFileName(bundleSource) != System.IO.Path.GetFileName(value))
                            BundleSourceChanged();

                        bundleSource = value;
                    }
                }
            }
            [SerializeField]
            internal string bundleName;
            [SerializeField]
            internal string bundleTag;
            [SerializeField]
            private List<int> customDisplayNamesIndex;
            [SerializeField]
            private List<string> customDisplayNames;

            internal LoadBundleEntry()
            {
                customDisplayNames = new();
                customDisplayNamesIndex = new();
            }

            private void BundleSourceChanged()
            {
                customDisplayNames = new();
                customDisplayNamesIndex = new();
            }

            internal bool ChangeDisplayName(string newDisplay, int assetIndex, out string finalName)
            {
                var reset = false;
                var originalName = OriginalDisplayName(assetIndex);
                if (string.IsNullOrWhiteSpace(newDisplay))
                {
                    reset = true;
                    newDisplay = originalName;
                    var i = customDisplayNamesIndex.IndexOf(assetIndex);
                    if (i != -1)
                    {
                        customDisplayNames.RemoveAt(i);
                        customDisplayNamesIndex.RemoveAt(i);
                    }
                }
                assetNameDisplays[assetIndex] = newDisplay;
                if (loadedAssets.ContainsKey(assetIndex))
                {
                    var (asset, icon, _) = loadedAssets[assetIndex];
                    var displayName = icon?.image ? $"     <b>{assetNameDisplays[assetIndex]}</b>" : $"<b>{assetNameDisplays[assetIndex]}</b>";
                    loadedAssets[assetIndex] = (asset, icon, displayName);
                }
                if (newDisplay != originalName)
                {
                    customDisplayNamesIndex.Add(assetIndex);
                    customDisplayNames.Add(newDisplay);
                }
                finalName = newDisplay;
                return reset;
            }

            private string OriginalDisplayName(int assetIndex)
            {
                if (isSceneBundle) return assetNames[assetIndex];
                return System.IO.Path.GetFileName(assetNames[assetIndex]);
            }
            
            internal bool ToggleAsset(int i)
            {
                if (loadingAsset == -1)
                {
                    loadingAsset = i;
                    if (isSceneBundle)
                    {
                        if (!loadedAssets.ContainsKey(i))
                        {
                            process = LoadScene();
                            FACSLoadBundle.AddToProcessQ(this);
                        }
                        else
                        {
                            var sceneInstance = SceneManager.GetSceneByPath(assetNames[loadingAsset]);
                            if (sceneInstance.IsValid() && sceneInstance.isLoaded)
                            {
                                SceneManager.UnloadSceneAsync(sceneInstance, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                                Logger.Log($"{RichToolName} {Logger.RichScene} {Logger.AssetTag}{assetNameDisplays[loadingAsset]}{Logger.EndTag} has been unloaded.");
                            }
                            loadedAssets.Remove(i);
                            FACSLoadBundle.AddToProcessQ(this);
                        }
                        return false;
                    }
                    else if (!loadedAssets.TryGetValue(i, out var obj_cont_disp))
                    {
                        process = LoadAsset();
                        FACSLoadBundle.AddToProcessQ(this);
                        return false;
                    }
                    else
                    {
                        var asset = obj_cont_disp.asset;
                        var select = false;
                        if (asset && asset is GameObject prefab)
                        {
                            var instance = Instantiate(prefab);
                            if (!Application.isPlaying) ApplyTagRecursive(instance.transform);
                            instance.name = assetNameDisplays[i];
                            GameObjectUtility.EnsureUniqueNameForSibling(instance);
                            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {instance.name}");
                            instantiated.Add(instance);
                            Selection.activeGameObject = instance; select = true;
                            EditorGUIUtility.PingObject(instance);
                        }
                        loadingAsset = -1;
                        return select;
                    }
                }
                else
                {
                    Logger.LogWarning(RichToolName + " Can't load multiple assets from the same " +Logger.RichAssetBundle+ " at the same time.");
                }
                return false;
            }

            private void UnloadSelectedAsset()
            {
                progress = -1;
                if (ao != null)
                {
                    ao.allowSceneActivation = false;
                    process = null; ao = null;
                    Logger.LogWarning($"{RichToolName} Stopped loading {(isSceneBundle? Logger.RichScene:Logger.RichAsset)} {Logger.AssetTag}{assetNameDisplays[loadingAsset]}{Logger.EndTag}.");
                }
                if (isSceneBundle)
                {
                    foreach (var sceneIndex in loadedAssets.Keys)
                    {
                        var sceneInstance = SceneManager.GetSceneByName(assetNames[sceneIndex]);
                        if (sceneInstance.IsValid() && sceneInstance.isLoaded)
                        {
                            SceneManager.UnloadSceneAsync(sceneInstance, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                            Logger.Log($"{RichToolName} {Logger.RichScene} {Logger.AssetTag}{assetNameDisplays[sceneIndex]}{Logger.EndTag} has been unloaded.");
                        }
                    }
                }
                else
                {
                    var destroyed = false;
                    if (!string.IsNullOrEmpty(bundleTag))
                    {

                        for (int i = 0; i < SceneManager.loadedSceneCount; i++)
                        {
                            var SceneRoots = SceneManager.GetSceneAt(i).GetRootGameObjects();
                            foreach (var root in SceneRoots) destroyed = DestroyByTagRecursive(root.transform) || destroyed;
                        }
                    }
                    if (instantiated.Count > 0)
                    {
                        foreach (var inst in instantiated) if (inst) { DestroyImmediate(inst); destroyed = true; }
                    }
                    instantiated.Clear();
                    if (destroyed) Logger.Log($"{RichToolName} Instantiated {Logger.RichGameObject} was removed from scene.");
                }
            }

            internal void Unload(bool single = false)
            {
                UnloadSelectedAsset(); loadingAsset = -1; loadedAssets.Clear();
                if (!string.IsNullOrEmpty(bundleTag)) UnityEditorInternal.InternalEditorUtility.RemoveTag(bundleTag);
                assetNames = assetNameDisplays = null; bundleTag = ""; isSceneBundle = false;
                editingDisplayName = -1;
                if (process != null)
                {
                    if (bundleWebRequest != null)
                    {
                        bundleWebRequest.Abort();
                        bundleWebRequest.Dispose();
                        bundleWebRequest = null;
                    }
                    if (bundleFileRequest != null)
                    {
                        bundleFileRequest.assetBundle.Unload(true);
                        bundleFileRequest = null;
                    }
                    if (!loadedAssetBundle)
                        Logger.LogWarning($"{RichToolName} Aborting {Logger.RichAssetBundle} from: {lastBundleSource}");
                    process = null;
                }
                if (loadedAssetBundle)
                {
                    loadedAssetBundle.Unload(true);
                    loadedAssetBundle = null;
                    Logger.Log($"{RichToolName} Unloaded {Logger.RichAssetBundle} from: {lastBundleSource}");
                }
                lastBundleSource = "";
                if (single) FACSLoadBundle.TryUnsubscribeUnloadAll();
            }

            internal IEnumerator BeginLoadBundle()
            {
                if (string.IsNullOrWhiteSpace(bundleSource)) yield break;

                lastBundleSource = bundleSource;
                var isURL = IsURL(lastBundleSource);
                if (isURL)
                {
                    Logger.Log($"{RichToolName} Getting {Logger.RichAssetBundle} from {Logger.ConceptTag}URL{Logger.EndTag}: {lastBundleSource}");
                    progress = 0;
                    yield return null;
                    bundleWebRequest = UnityWebRequestAssetBundle.GetAssetBundle(lastBundleSource);
                    var tmp = bundleWebRequest.SendWebRequest();
                    while (!tmp.isDone)
                    {
                        if (tmp.progress - progress >= 0.01f) { progress = tmp.progress;
                            if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint(); }
                        yield return null;
                    }
                    if (!string.IsNullOrEmpty(bundleWebRequest.error))
                    {
                        Logger.LogError($"{RichToolName} Encountered an error while obtaining {Logger.RichAssetBundle} from {Logger.ConceptTag}URL{Logger.EndTag}: {lastBundleSource}\n{bundleWebRequest.error}");
                        bundleWebRequest.Dispose(); bundleWebRequest = null; progress = -1; lastBundleSource = "";
                        if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                        yield break;
                    }
                    loadedAssetBundle = DownloadHandlerAssetBundle.GetContent(bundleWebRequest);
                    bundleWebRequest.Dispose(); bundleWebRequest = null;
                }
                else if (System.IO.File.Exists(lastBundleSource))
                {
                    Logger.Log($"{RichToolName} Getting {Logger.RichAssetBundle} from {Logger.ConceptTag}local file{Logger.EndTag}: {lastBundleSource}");
                    progress = 0;
                    yield return null;
                    try { bundleFileRequest = AssetBundle.LoadFromFileAsync(lastBundleSource); }
                    catch (System.Exception e)
                    {
                        Logger.LogError($"{RichToolName} Encountered an error while obtaining {Logger.RichAssetBundle} from {Logger.ConceptTag}local file{Logger.EndTag}: {lastBundleSource}\n{e.Message}");
                        bundleFileRequest = null;
                    }
                    if (bundleFileRequest == null)
                    {
                        lastBundleSource = ""; progress = -1;
                        if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                        yield break;
                    }
                    while (!bundleFileRequest.isDone)
                    {
                        if (bundleFileRequest.progress - progress >= 0.01f)
                        {
                            progress = bundleFileRequest.progress;
                            if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                        }
                        yield return null;
                    }
                    loadedAssetBundle = bundleFileRequest.assetBundle;
                    bundleFileRequest = null;
                }
                else
                {
                    Logger.LogWarning($"{RichToolName} Invalid {Logger.ConceptTag}URL{Logger.EndTag} or {Logger.ConceptTag}file path{Logger.EndTag}: {lastBundleSource}");
                    progress = -1; if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                    lastBundleSource = ""; yield break;
                }

                if (loadedAssetBundle == null)
                {
                    Logger.LogError($"{RichToolName} Failed to load {Logger.RichAssetBundle} from {Logger.ConceptTag}{(isURL ? "URL" : "file")}{Logger.EndTag}: {lastBundleSource}");
                    progress = -1; if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                    lastBundleSource = ""; yield break;
                }

                bundleTag = FLBTag + System.Guid.NewGuid().ToString().Replace("-", "");
                UnityEditorInternal.InternalEditorUtility.AddTag(bundleTag);
                isSceneBundle = loadedAssetBundle.isStreamedSceneAssetBundle;
                if (isSceneBundle)
                {
                    assetNames = loadedAssetBundle.GetAllScenePaths();
                    if (assetNames.Length == 0)
                    {
                        var lbs = lastBundleSource; Unload(true);
                        Logger.LogError($"{RichToolName} The {Logger.RichScene} {Logger.RichAssetBundle} doesn't contain scene paths: {lbs}");
                        yield break;
                    }
                }
                else
                {
                    assetNames = loadedAssetBundle.GetAllAssetNames();
                    if (assetNames.Length == 0)
                    {
                        var lbs = lastBundleSource; Unload(true);
                        Logger.LogError($"{RichToolName} The {Logger.RichAssetBundle} doesn't contain asset paths: {lbs}");
                        yield break;
                    }
                }
                assetNameDisplays = new string[assetNames.Length]; System.Array.Copy(assetNames, assetNameDisplays, assetNames.Length);
                Logger.Log($"{RichToolName} <b>{assetNames.Length}</b> {Logger.TypeTag}{(isSceneBundle ? "Scene" : "Asset")}{(assetNames.Length>1 ? $"s{Logger.EndTag} are": $"{Logger.EndTag} is")}" +
                    $" ready to be loaded from {Logger.AssetTag}{(isURL ? lastBundleSource : System.IO.Path.GetFileName(lastBundleSource))}{Logger.EndTag}!");
                for (int ind = 0; ind < customDisplayNamesIndex.Count; ind++)
                {
                    var assetIndex = customDisplayNamesIndex[ind];
                    if (assetIndex < assetNameDisplays.Length) assetNameDisplays[assetIndex] = customDisplayNames[ind];
                    else { customDisplayNames.RemoveAt(ind); customDisplayNamesIndex.RemoveAt(ind); ind--; }
                }
                yield break;
            }
            
            private IEnumerator LoadAsset()
            {
                var assetPath = assetNames[loadingAsset];
                progress = 0;
                if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                yield return null;
                var _ao = loadedAssetBundle.LoadAssetAsync(assetPath); ao = _ao;
                while (!ao.isDone)
                {
                    if (ao.progress - progress > 0.01f)
                    {
                        progress = ao.progress;
                        if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                    }
                    yield return null;
                }
                ao = null; progress = 1;
                var asset = _ao.asset;
                if (asset == null)
                {
                    var lbs = lastBundleSource; var and = assetNameDisplays[loadingAsset];
                    if (assetNames.Length == 1) Unload(true);
                    Logger.LogError($"{RichToolName} The {Logger.RichAsset} {and} is {Logger.ConceptTag}NULL{Logger.EndTag}\nBundle source: {lbs}");
                    if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                    yield break;
                }
                asset.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                if (asset is GameObject assetGO)
                {
                    asset = assetGO;
                    assetGO.transform.localPosition = Vector3.zero;
#if VRC_SDK_VRCSDK3 && !UDON
                    DestroyAllComponents<VRC.Core.PipelineManager>(assetGO);
                    if (!Application.isPlaying) FixVRCAvatarDescriptorInspector(assetGO);
#elif VRC_SDK_VRCSDK2
                    DestroyAllComponents<VRC.Core.PipelineManager>(assetGO);
#endif
                }
                var assetIcon = ComponentHierarchy.GetTypeData(asset).content.image;
                var content = assetIcon ? new GUIContent(assetIcon) : null;
                var displayName = assetIcon ? $"     <b>{assetNameDisplays[loadingAsset]}</b>" : $"<b>{assetNameDisplays[loadingAsset]}</b>";
                loadedAssets.Add(loadingAsset, (asset, content, displayName));
                if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                Logger.Log($"{RichToolName} {Logger.TypeTag}{asset.GetType().Name}{Logger.EndTag} {Logger.AssetTag}{assetNameDisplays[loadingAsset]}{Logger.EndTag} has been loaded!");
                yield break;
            }

            private void ApplyTagRecursive(Transform t)
            {
                t.gameObject.tag = bundleTag;
                t.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                foreach (Transform child in t) ApplyTagRecursive(child);
            }

            private bool DestroyByTagRecursive(Transform t)
            {
                if (t.gameObject.CompareTag(bundleTag)) { DestroyImmediate(t.gameObject); return true; }
                else
                {
                    var destroy = false;
                    foreach (Transform child in t) destroy = DestroyByTagRecursive(child) || destroy;
                    return destroy;
                }
            }

            private IEnumerator LoadScene()
            {
                if (!Application.isPlaying)
                {
                    Logger.LogWarning(RichToolName + " Can't load scenes on " + Logger.ConceptTag + "Edit Mode" + Logger.EndTag +
                        ". Please enter " + Logger.ConceptTag + "Play Mode" + Logger.EndTag + ".");
                    yield break;
                }
                var scenePath = assetNames[loadingAsset];
                progress = 0;
                if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                yield return null;
                ao = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                ao.allowSceneActivation = true;
                while (!ao.isDone)
                {
                    if (ao.progress - progress > 0.01f)
                    {
                        progress = ao.progress;
                        if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                    }
                    yield return null;
                }
                ao = null; progress = 1;
                var sceneInstance = SceneManager.GetSceneByPath(scenePath);
                if (!sceneInstance.IsValid())
                {
                    if (sceneInstance.isLoaded) SceneManager.UnloadSceneAsync(sceneInstance, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                    var lbs = lastBundleSource; var and = assetNameDisplays[loadingAsset];
                    if (assetNames.Length == 1) Unload(true);
                    Logger.LogError($"{RichToolName} The {Logger.RichScene} {and} is {Logger.ConceptTag}NULL{Logger.EndTag}\nBundle source: {lbs}");
                    if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                    yield break;
                }
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
                foreach (var rootObject in sceneInstance.GetRootGameObjects()) DestroyAllComponents<VRC.Core.PipelineManager>(rootObject);
#endif
                var content = FACSLoadBundle.SceneAssetIcon?.image ? FACSLoadBundle.SceneAssetIcon : null;
                var displayName = content!=null ? $"     <b>{assetNameDisplays[loadingAsset]}</b>" : $"<b>{assetNameDisplays[loadingAsset]}</b>";
                loadedAssets.Add(loadingAsset, (null, content, displayName));
                if (FACSLoadBundle.instance) FACSLoadBundle.instance.Repaint();
                Logger.Log($"{RichToolName} {Logger.RichScene} {Logger.AssetTag}{assetNameDisplays[loadingAsset]}{Logger.EndTag} has been loaded!");
                yield break;
            }

            private static void DestroyAllComponents<T>(GameObject go) where T : Component
            {
                T[] components = go.GetComponentsInChildren<T>(true);
                foreach (var c in components) { DestroyImmediate(c, true); }
            }

#if VRC_SDK_VRCSDK3 && !UDON
            private static void FixVRCAvatarDescriptorInspector(GameObject go)
            {
                var animator = go.GetComponent<Animator>();
                if (animator && animator.isHuman)
                {
                    var descriptor = go.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                    if (!descriptor) return;
                    foreach (var layer in descriptor.baseAnimationLayers)
                    {
                        if (!layer.isDefault && layer.animatorController &&
                            layer.animatorController is UnityEditor.Animations.AnimatorController ac &&
                            ac.layers.Length == 0)
                        {
                            ac.layers = new UnityEditor.Animations.AnimatorControllerLayer[1]
                            {
                            new UnityEditor.Animations.AnimatorControllerLayer()
                            };
                        }
                    }
                }
            }
#endif
        }

        private static bool IsURL(string uri)
        {
            return System.Uri.TryCreate(uri, System.UriKind.Absolute, out System.Uri uriResult)
                && (uriResult.Scheme == System.Uri.UriSchemeHttps || uriResult.Scheme == System.Uri.UriSchemeHttp);
        }

        internal static void SaveData()
        {
            instance.Save(true);
        }
    }
}
#endif