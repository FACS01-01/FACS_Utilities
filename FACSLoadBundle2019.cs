#if (VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3)
using VRC.Core;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace FACS01.Utilities
{
    public class FACSLoadBundle2019 : MonoBehaviour
    {
        public string AssetSource;
        public string Name;
        public bool ShaderUsage;
        public bool DidAssetLoad;
        private GameObject avatarInstance;
        private AssetBundle LoadedAssetBundle;
        public IEnumerator coroutine;

        public void OnEnable()
        {
            coroutine = LoadBundle();
            StartCoroutine(coroutine);
        }
        public void OnDisable()
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
                coroutine = null;
            }
            if (avatarInstance != null)
            {
                Object.Destroy(avatarInstance);
                avatarInstance = null;
            }
            if (LoadedAssetBundle != null)
            {
                LoadedAssetBundle.Unload(true);
                LoadedAssetBundle = null;
            }
            DidAssetLoad = false;
            
        }
        public IEnumerator LoadBundle()
        {
            if (AssetSource == "") { yield break; }

            bool isURL = AssetSource.StartsWith("http");

            if (isURL)
            {
                Debug.Log($"Getting <color=cyan>AssetBundle</color> from <color=green>URL</color>.\nPlease wait a moment...");
                var bundleLoadRequest = UnityWebRequestAssetBundle.GetAssetBundle(AssetSource);
                yield return bundleLoadRequest.SendWebRequest();
                LoadedAssetBundle = DownloadHandlerAssetBundle.GetContent(bundleLoadRequest);
            }
            else
            {
                Debug.Log($"Getting <color=cyan>AssetBundle</color> from local <color=green>file</color>.\n");
                var bundleLoadRequest = AssetBundle.LoadFromFileAsync(AssetSource);
                yield return bundleLoadRequest;
                LoadedAssetBundle = bundleLoadRequest.assetBundle;
            }
            if (LoadedAssetBundle == null)
            {
                if (isURL) Debug.LogError($"Failed to load AssetBundle from URL: {AssetSource}\n");
                else Debug.LogError($"Failed to load AssetBundle from file: {AssetSource}\n");
                yield break;
            }

            string[] scenePaths = LoadedAssetBundle.GetAllScenePaths();

            if (scenePaths.Length == 0)
            {
                foreach (string asset in LoadedAssetBundle.GetAllAssetNames())
                {
                    if (asset.EndsWith(".prefab"))
                    {
                        avatarInstance = Instantiate((GameObject)LoadedAssetBundle.LoadAsset(asset), this.transform, false);
                        avatarInstance.transform.position = new Vector3(0, 0, 0);

#if VRC_SDK_VRCSDK3
                        DestroyImmediate(avatarInstance.GetComponent<PipelineSaver>());
                        DestroyImmediate(avatarInstance.GetComponent<PipelineManager>());
#elif VRC_SDK_VRCSDK2
                        DestroyImmediate(avatarInstance.GetComponent<PipelineManager>());
#endif

                        if (!String.IsNullOrWhiteSpace(Name))
                        {
                            avatarInstance.name = Name;
                        }
                        else
                        {
                            string fileName = Path.GetFileNameWithoutExtension(AssetSource);
                            string prefabName = avatarInstance.name;

                            if (prefabName.EndsWith("(Clone)")) { prefabName = prefabName.Substring(0, prefabName.LastIndexOf("(Clone)")); }

                            if (isURL) avatarInstance.name = prefabName;
                            else avatarInstance.name = fileName + " (" + prefabName + ")";
                        }

                        DidAssetLoad = true;

                        Debug.Log($"<color=green>{avatarInstance.name}</color> was loaded from <color=cyan>AssetBundle</color>!\n");

                        break;
                    }
                }
            }

            else
            {
                string sceneName = Path.GetFileNameWithoutExtension(scenePaths[0]);
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);

                DidAssetLoad = true;

                Debug.Log($"<color=green>{sceneName}</color> was loaded from <color=cyan>AssetBundle</color>!\n");
            }
        }
        public (List<string>, List<List<string>>) getShaderUsage()
        {
            List<Material> materialList = new List<Material>();

            if (avatarInstance != null)
            {
                Renderer[] all_renderers = avatarInstance.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer rend in all_renderers)
                {
                    foreach (Material mat in rend.sharedMaterials)
                    {
                        if (!materialList.Contains(mat) && mat != null)
                        {
                            materialList.Add(mat);
                        }
                    }
                }
            }
            else
            {
                GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (GameObject go in rootObjects)
                {
                    Renderer[] all_renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer rend in all_renderers)
                    {
                        foreach (Material mat in rend.sharedMaterials)
                        {
                            if (!materialList.Contains(mat) && mat != null)
                            {
                                materialList.Add(mat);
                            }
                        }
                    }
                }
            }

            List<string> all_shaders = new List<string>();
            List<List<string>> mats_with_shader = new List<List<string>>();

            foreach (Material mat in materialList)
            {
                if (!all_shaders.Contains(mat.shader.name))
                {
                    all_shaders.Add(mat.shader.name);
                    mats_with_shader.Add(new List<string> { mat.shader.name, mat.name });
                }
                else
                {
                    if (!mats_with_shader[all_shaders.IndexOf(mat.shader.name)].Contains(mat.name))
                    {
                        mats_with_shader[all_shaders.IndexOf(mat.shader.name)].Add(mat.name);
                    }
                }
            }

            all_shaders.Sort();
            mats_with_shader.Sort((a, b) => a[0].CompareTo(b[0]));
            int shaderCount = all_shaders.Count;

            for (int i = 0; i < shaderCount; i++)
            {
                mats_with_shader[i].RemoveAt(0);
                mats_with_shader[i].Sort();
            }

            return (all_shaders, mats_with_shader);
        }
    }
}