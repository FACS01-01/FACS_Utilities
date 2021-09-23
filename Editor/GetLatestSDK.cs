using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System;
using System.IO;
using System.ComponentModel;
using System.Text;
#if VRC_SDK_VRCSDK3
using VRC.SDK3;
#endif

namespace FACS01.Utilities
{
    public class GetLatestSDK : EditorWindow 
    {
        private (int[], string, string) SDK2;
#pragma warning disable 0414
        private (int[], string, string) SDK3Avatar;
        private (int[], string, string) SDK3World;
        private static int canUpdate;
#pragma warning restore 0414
        private string TempFolderPath = Path.GetTempPath();
        private int DownLoadPercentage;
        private (int[], string) InstalledVer;
        private bool isWorking = false;

        private string timeNow;

        [MenuItem("Tools/FACS Utilities/Get Latest VRC SDK")]
        public static void ShowWindow()
        {
            EditorWindow editorWindow = GetWindow(typeof(GetLatestSDK), false, "Get Latest SDK", true);
            editorWindow.autoRepaintOnSceneChange = true;
        }
        public void OnGUI()
        {
            GUIStyle newstyle = new GUIStyle(GUI.skin.GetStyle("HelpBox"));
            newstyle.richText = true;
            newstyle.fontSize = 13;
            newstyle.wordWrap = true;
            newstyle.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"<color=cyan><b>Get Latest VRChat SDK</b></color>", newstyle);

#if VRC_SDK_VRCSDK3
            Type SDK3AvatarsType = Type.GetType("VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu, VRCSDK3A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", false);
            if (SDK3AvatarsType != null)
            {   // sdk3 avatar
                EditorGUILayout.LabelField($"You are using:\nVRChat SDK3-Avatars", newstyle);
                if (GUILayout.Button($"<color=green>Check for Updates</color>", newstyle, GUILayout.Height(40)) && !isWorking)
                {
                    GetLatestVersions("SDK3-Avatars");
                }
                if (canUpdate == 1)
                {
                    EditorGUILayout.LabelField($"{timeNow}\nCurrent version: {InstalledVer.Item2}\nLatest version: {SDK3Avatar.Item3}", newstyle);
                    if (GUILayout.Button("<color=green>Download Update!</color>", newstyle, GUILayout.Height(40)) && !isWorking)
                    {
                        isWorking = true;
                        string filepath = Path.Combine(TempFolderPath, "SDK3Avatars_" + SDK3Avatar.Item3 + ".unitypackage");
                        if (!File.Exists(filepath)) DownloadFile(SDK3Avatar.Item2, filepath);
                        else { AssetDatabase.ImportPackage(filepath, true); canUpdate = 0; isWorking = false; }
                    }
                }
                else if (canUpdate == 2)
                {
                    EditorGUILayout.LabelField($"{timeNow}\nCurrent version: {InstalledVer.Item2}\nLatest version: {SDK3Avatar.Item3}\n<color=green>You are up to date!</color>", newstyle);
                }
            }
            else
            {   // sdk3 world
                EditorGUILayout.LabelField($"You are using:\nVRChat SDK3-Worlds", newstyle);
                if (GUILayout.Button($"<color=green>Check for Updates</color>", newstyle, GUILayout.Height(40)) && !isWorking)
                {
                    GetLatestVersions("SDK3-Worlds");
                }
                if (canUpdate == 1)
                {
                    EditorGUILayout.LabelField($"{timeNow}\nCurrent version: {InstalledVer.Item2}\nLatest version: {SDK3World.Item3}", newstyle);
                    if (GUILayout.Button("<color=green>Download Update!</color>", newstyle, GUILayout.Height(40)) && !isWorking)
                    {
                        isWorking = true;
                        string filepath = Path.Combine(TempFolderPath, "SDK3Worlds_" + SDK3World.Item3 + ".unitypackage");
                        if (!File.Exists(filepath)) DownloadFile(SDK3World.Item2, filepath);
                        else { AssetDatabase.ImportPackage(filepath, true); canUpdate = 0; isWorking = false; }
                    }
                }
                else if (canUpdate == 2)
                {
                    EditorGUILayout.LabelField($"{timeNow}\nCurrent version: {InstalledVer.Item2}\nLatest version: {SDK3Avatar.Item3}\n<color=green>You are up to date!</color>", newstyle);
                }
            }
#elif VRC_SDK_VRCSDK2
            // sdk2
            EditorGUILayout.LabelField($"You are using:\nVRChat SDK2", newstyle);
                if (GUILayout.Button($"<color=green>Check for Updates</color>", newstyle, GUILayout.Height(40)) && !isWorking)
                {
                    GetLatestVersions("SDK2");
                }
                if (canUpdate == 1)
                {
                    EditorGUILayout.LabelField($"{timeNow}\nCurrent version: {InstalledVer.Item2}\nLatest version: {SDK2.Item3}", newstyle);
                    if (GUILayout.Button("<color=green>Download Update!</color>", newstyle, GUILayout.Height(40)) && !isWorking)
                    {
                        isWorking = true;
                        string filepath = Path.Combine(TempFolderPath, "SDK2_" + SDK2.Item3 + ".unitypackage");
                        if (!File.Exists(filepath)) DownloadFile(SDK2.Item2, filepath);
                        else { AssetDatabase.ImportPackage(filepath, true); canUpdate = 0; isWorking = false;}
                    }
                }
                else if (canUpdate == 2)
                {
                    EditorGUILayout.LabelField($"{timeNow}\nCurrent version: {InstalledVer.Item2}\nLatest version: {SDK2.Item3}\n<color=green>You are up to date!</color>", newstyle);
                }
#else
            // no sdk yet
            EditorGUILayout.LabelField($"Seems you don't have any VRChat SDK installed.\nWhich one would you like to download?", newstyle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"<color=green>SDK2</color>", newstyle, GUILayout.Height(40)) && !isWorking)
            {
                GetLatestVersions("NEW-SDK2");
            }
            else if (GUILayout.Button($"<color=green>SDK3\nAvatars</color>", newstyle, GUILayout.Height(40)) && !isWorking)
            {
                GetLatestVersions("NEW-SDK3-Avatars");
            }
            else if (GUILayout.Button($"<color=green>SDK3\nWorlds</color>", newstyle, GUILayout.Height(40)) && !isWorking)
            {
                GetLatestVersions("NEW-SDK3-Worlds");
            }
            EditorGUILayout.EndHorizontal();
#endif
        }
        private void GetLatestVersions(string sdk)
        {
            isWorking = true;
            Debug.Log("Requesting SDK versions.");
            SDK2.Item3 = null; SDK3Avatar.Item3 = null; SDK3World.Item3 = null; canUpdate = 0;
            using (WebClient wc = new WebClient())
            {
                wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(DownloadString_Completed);
                wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadString_ProgressChanged);
                wc.DownloadStringAsync(new Uri("https://api.vrchat.cloud/api/1/config"), sdk);
            }
        }
        void DownloadString_ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage != DownLoadPercentage && e.ProgressPercentage % 20 == 0)
            {
                DownLoadPercentage = e.ProgressPercentage;
                string tmp = "Downloading SDK versions: " + e.ProgressPercentage + "%";
                Debug.Log(tmp);
            }
        }
        void DownloadString_Completed(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Debug.LogError("Web Exception. Couldn't connect to source, maybe Internet is down?\n\n" + e.Error.StackTrace);
                isWorking = false;
            }
            else
            {
                string userState = (string)e.UserState;
                string json = e.Result;
                var tmp = json.Substring(15 + json.IndexOf("downloadUrls"));
                var tmp2 = tmp.Substring(0, tmp.IndexOf("}")).Replace("\"", "").Split(',');
                List<(string, string, string)> sdks = new List<(string, string, string)>();
                foreach (var i in tmp2)
                {
                    string sdkType = i.Substring(0, i.IndexOf(":"));
                    string sdkLink = i.Substring(i.IndexOf(":") + 1);

                    int pFrom = sdkLink.LastIndexOf("-") + 1;
                    int pTo = sdkLink.LastIndexOf("_");
                    string sdkVer = sdkLink.Substring(pFrom, pTo - pFrom);
                    int[] sdkVerParse = sdkVer.Split('.').Select(n => Convert.ToInt32(n)).ToArray();

                    if (sdkType == "sdk2")
                    {
                        SDK2 = (sdkVerParse, sdkLink, sdkVer);
                    }
                    else if (sdkType == "sdk3-worlds")
                    {

                        SDK3World = (sdkVerParse, sdkLink, sdkVer);
                    }
                    else
                    {
                        SDK3Avatar = (sdkVerParse, sdkLink, sdkVer);
                    }
                    // Debug.Log("SDK VER: " + sdkType + " ; SDK LINK: " + sdkLink + " :: " + sdkVer);
                }
                if (!userState.StartsWith("NEW-")) //  userState == "SDK2" || userState == "SDK3-Avatars" || userState == "SDK3-Worlds"
                {
                    GetInstalledVer();
                    if (userState == "SDK2" && SDK2.Item3 != null) CanUpdate(SDK2);
                    else if (userState == "SDK3-Avatars" && SDK3Avatar.Item3 != null) CanUpdate(SDK3Avatar);
                    else if (SDK3World.Item3 != null) CanUpdate(SDK3World);
                    this.Repaint();
                    isWorking = false;
                }
                else
                {
                    if (userState == "NEW-SDK2" && SDK2.Item3 != null)
                    {
                        string filepath = Path.Combine(TempFolderPath, "SDK2_" + SDK2.Item3 + ".unitypackage");
                        if (!File.Exists(filepath)) DownloadFile(SDK2.Item2, filepath);
                        else { AssetDatabase.ImportPackage(filepath, true); canUpdate = 0; isWorking = false; }
                    }
                    else if (userState == "NEW-SDK3-Avatars" && SDK3Avatar.Item3 != null)
                    {
                        string filepath = Path.Combine(TempFolderPath, "SDK3Avatars_" + SDK3Avatar.Item3 + ".unitypackage");
                        if (!File.Exists(filepath)) DownloadFile(SDK3Avatar.Item2, filepath);
                        else { AssetDatabase.ImportPackage(filepath, true); canUpdate = 0; isWorking = false; }
                    }
                    else if (SDK3World.Item3 != null)
                    {
                        string filepath = Path.Combine(TempFolderPath, "SDK3Worlds_" + SDK3World.Item3 + ".unitypackage");
                        if (!File.Exists(filepath)) DownloadFile(SDK3World.Item2, filepath);
                        else { AssetDatabase.ImportPackage(filepath, true); canUpdate = 0; isWorking = false; }
                    }
                    else { canUpdate = 0; isWorking = false; }
                }
            }
        }
        private void DownloadFile(string url, string filepath)
        {
            WebClient client = new WebClient();
            client.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
            client.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFile_Completed);
            client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadFile_ProgressChanged);
            client.DownloadFileAsync(new Uri(url), filepath, filepath);
        }
        void DownloadFile_ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage != DownLoadPercentage && e.ProgressPercentage % 10 == 0)
            {
                DownLoadPercentage = e.ProgressPercentage;
                string tmp = "Downloading SDK: " + e.ProgressPercentage + "%";
                Debug.Log(tmp);
            }
        }
        void DownloadFile_Completed(object sender, AsyncCompletedEventArgs e)
        {
            string filepath = (string)e.UserState;
            if (e.Error != null)
            {
                Debug.LogError("Web Exception. Couldn't connect to source, maybe Internet is down?\n\n" + e.Error.StackTrace);
                if (File.Exists(filepath)) File.Delete(filepath);
                isWorking = false;
            }
            else
            {
                AssetDatabase.ImportPackage(filepath, true);
                DownLoadPercentage = 0;
                canUpdate = 0;
                isWorking = false;
            }
        }
        private void GetInstalledVer()
        {
            string versionPath = Application.dataPath + "/VRCSDK/version.txt";
            if (!File.Exists(versionPath))
            {
                Debug.Log("Version.txt not found in Assets/VRCSDK. Can't get SDK current version.");
                return;
            }
            else
            {
                StreamReader sr = new StreamReader(versionPath);
                string line1 = sr.ReadLine();
                sr.Close();
                int[] currentSDKparse = line1.Split('.').Select(n => Convert.ToInt32(n)).ToArray(); ;
                InstalledVer = (currentSDKparse, line1);
            }
        }
        private void CanUpdate((int[], string, string) sdk)
        {
            timeNow = DateTime.Now.ToString();

            int compareLength = Mathf.Min(InstalledVer.Item1.Length, sdk.Item1.Length);
            for (int i = 0; i < compareLength; i++)
            {
                if (InstalledVer.Item1[i] > sdk.Item1[i])
                {
                    canUpdate = 2;
                    return;
                }
                else if (InstalledVer.Item1[i] < sdk.Item1[i])
                {
                    canUpdate = 1;
                    return;
                }
            }
            canUpdate = 2;
        }
        private void OnDestroy()
        {
            canUpdate = 0;
        }
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            canUpdate = 0;
        }
    }
}