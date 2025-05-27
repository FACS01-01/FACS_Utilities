#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    internal class FACSFastPhoto : MonoBehaviour
    {
        private const string RichToolName = Logger.ToolTag + "[FACS Fast Photo]" + Logger.EndTag;
        private const int MaxSize = 8000;

        [SerializeField, HideInInspector]
        public Camera cam;
        [SerializeField, HideInInspector]
        private bool newCam = true;

        [SerializeField, Min(1)]
        public int PhotoWidth = 1920;
        [SerializeField, Min(1)]
        public int PhotoHeight = 1080;
        [SerializeField]
        public CameraBackground camBg = CameraBackground.Transparent;
        [SerializeField]
        public Color bgColor = Color.green;
        [SerializeField]
        public bool saveInProj = true;
        [SerializeField]
        public bool settingsFoldout = true;
        [SerializeField]
        public bool previewFoldout = false;

        [System.NonSerialized]
        private CameraBackground _camBg = (CameraBackground)4;
        [System.NonSerialized]
        private bool doValidation = false;

        private void Awake()
        {
            cam = this.gameObject.GetComponent<Camera>();
            if (newCam)
            {
                CamDefaultSettings();
                UnityEditorInternal.ComponentUtility.MoveComponentUp(this);
            }
            NewRT();
            DoValidation();
            cam.enabled = this.enabled;
        }

        private void CamDefaultSettings()
        {
            camBg = CameraBackground.Transparent;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 15;
            cam.usePhysicalProperties = true;
            cam.focalLength = 20.78461f;
            cam.gateFit = Camera.GateFitMode.Horizontal;
        }

        private void Update()
        {
            if (doValidation)
            {
                doValidation = false;
                DoValidation();
            }
            if (newCam)
            {
                newCam = false;
                cam.hideFlags = HideFlags.HideInInspector;
                EditorUtility.SetDirty(cam);
            }
        }

        private void OnValidate()
        {
            doValidation = true;
        }

        private void DoValidation()
        {
            ValidateCam();
            ValidateRT();
            ValidateWH();
            ValidateCamBg();
        }

        private void ValidateCam()
        {
            if (!cam)
            {
                cam = this.gameObject.GetComponent<Camera>();
                if (!cam)
                {
                    newCam = true;
                    cam = this.gameObject.AddComponent<Camera>();
                    CamDefaultSettings();
                    cam.hideFlags = HideFlags.HideInInspector;
                }
            }
        }

        private void ValidateRT()
        {
            if (!cam.targetTexture) NewRT();
        }

        private void NewRT()
        {
            cam.targetTexture = new RenderTexture(PhotoWidth, PhotoHeight, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture.name = "DO NOT TOUCH >:C";
            cam.targetTexture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }

        private void ValidateWH()
        {
            if (!cam.usePhysicalProperties) cam.usePhysicalProperties = true;
            if (PhotoWidth > MaxSize) PhotoWidth = MaxSize;
            if (PhotoHeight > MaxSize) PhotoHeight = MaxSize;
            if (PhotoWidth > 100 * PhotoHeight) PhotoWidth = 100 * PhotoHeight;
            if (PhotoHeight > 100 * PhotoWidth) PhotoHeight = 100 * PhotoWidth;
            if (cam.targetTexture.width != PhotoWidth || cam.targetTexture.height != PhotoHeight || newCam)
            {
                cam.targetTexture.Release();
                cam.targetTexture.width = PhotoWidth;
                cam.targetTexture.height = PhotoHeight;
                cam.targetTexture.Create();
                float sensorSizeX = 24; float sensorSizeY = 24; // base sensor size
                if (PhotoWidth != PhotoHeight)
                {
                    var landscape = PhotoWidth >= PhotoHeight;
                    if (landscape) sensorSizeX = PhotoWidth * sensorSizeY / PhotoHeight;
                    else sensorSizeY = PhotoHeight * sensorSizeX / PhotoWidth;
                }
                cam.sensorSize = new Vector2(sensorSizeX, sensorSizeY);
                cam.Render();
            }
        }

        private void ValidateCamBg()
        {
            if (camBg != _camBg)
            {
                switch (camBg)
                {
                    case CameraBackground.Skybox:
                        cam.clearFlags = CameraClearFlags.Skybox;
                        break;
                    case CameraBackground.Color:
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = bgColor;
                        break;
                    case CameraBackground.Transparent:
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = new Color(0, 0, 0, 0);
                        break;
                }
                _camBg = camBg;
            }
            if (camBg != CameraBackground.Transparent && cam.backgroundColor != bgColor)
            {
                cam.backgroundColor = bgColor;
            }
        }

        public void TakePhoto()
        {
            string fileName;
            if (saveInProj)
            {
                var takePhotosPath = "Assets/Fast Photos";
                if (!System.IO.Directory.Exists(takePhotosPath)) System.IO.Directory.CreateDirectory(takePhotosPath);
                var date = System.DateTime.Now;
                fileName = takePhotosPath + "/" + date.ToString("yyyy-MM-dd--HH-mm-ss") + ".png";
            }
            else
            {
                fileName = EditorUtility.SaveFilePanel($"FACS Fast Photo - Save New Photo", "", "", "png");
                if (string.IsNullOrEmpty(fileName)) return;
            }

            ValidateRT();

            cam.Render();

            var _rt = RenderTexture.active;
            try
            {
                RenderTexture.active = cam.targetTexture;

                Texture2D image = new Texture2D(cam.targetTexture.width, cam.targetTexture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
                image.Apply();

                byte[] bytes = image.EncodeToPNG();
                DestroyImmediate(image);
                
                if (System.IO.File.Exists(fileName)) System.IO.File.Delete(fileName);
                System.IO.File.WriteAllBytes(fileName, bytes);

                fileName = System.IO.Path.GetFullPath(fileName);
                var projPath = System.IO.Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) + System.IO.Path.DirectorySeparatorChar;
                if (fileName.StartsWith(projPath + "Assets"))
                {
                    fileName = fileName.Replace(projPath, "");
                    AssetDatabase.ImportAsset(fileName, ImportAssetOptions.ForceUpdate);
                    var imageAsset = AssetDatabase.LoadMainAssetAtPath(fileName);
                    EditorGUIUtility.PingObject(imageAsset);
                    Selection.activeObject = imageAsset;
                }
                
                Logger.Log($"{RichToolName} <color=green>New photo saved at:</color> {fileName}");
            }
            catch (System.Exception e)
            {
                Logger.LogError($"{RichToolName} {e}");
            }
            finally
            {
                RenderTexture.active = _rt;
            }
        }

        private void OnEnable()
        {
            ValidateCam();
            cam.enabled = true;
        }

        private void OnDisable()
        {
            if (!cam) cam = this.gameObject.GetComponent<Camera>();
            if (cam) cam.enabled = false;
        }

        private void OnDestroy()
        {
            var rt = cam.targetTexture;
            if (rt)
            {
                cam.targetTexture = null;
                rt.Release();
                DestroyImmediate(rt);
            }

            cam.hideFlags = HideFlags.None;
        }

        internal enum CameraBackground
        {
            Skybox,
            Color,
            Transparent
        }
    }
}
#endif