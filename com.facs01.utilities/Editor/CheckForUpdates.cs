#if UNITY_EDITOR
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace FACS01.Utilities
{
    [InitializeOnLoad]
    internal static class CheckForUpdates
    {
        private const string RichToolName = Logger.ToolTag + "[FACS Utilities - Check For Updates]" + Logger.EndTag;

        static CheckForUpdates()
        {
            if (SessionState.GetBool("FACSUtilities_CheckUpdatesStartup", true))
            {
                SessionState.SetBool("FACSUtilities_CheckUpdatesStartup", false);
                if (PlayerPrefs.GetInt("FACSUtilities_CheckUpdates", 1) == 1) CheckForUpdatesTask();
            }
        }

        [MenuItem("FACS Utils/Check for Updates", false, 999)]
        private static void CheckForUpdatesManual()
        {
            CheckForUpdatesTask(true);
        }

        [MenuItem("FACS Utils/Open GitHub", false, 999)]
        private static void OpenFACSUtilitiesGitHub()
        {
            Application.OpenURL("https://github.com/FACS01-01/FACS_Utilities");
        }

        private static void CheckForUpdatesTask(bool manual = false)
        {
            var myVersion = PackageInfo.FindForAssembly(typeof(CheckForUpdates).Assembly).version;
            if (manual) PlayerPrefs.SetInt("FACSUtilities_CheckUpdates", 1);
            Task.Run(async ()=>
            {
                string latestVersion;
                using (var client = new HttpClient())
                {
                    client.Timeout = new(0, 0, 5);
                    client.DefaultRequestHeaders.CacheControl = new() { NoCache=true };
                    try
                    {
                        latestVersion = await client.GetStringAsync("https://raw.githubusercontent.com/FACS01-01/FACS_Utilities/main/version.txt");
                    }
                    catch (System.Exception e)
                    {
                        Logger.LogWarning($"{RichToolName} Can't check for updates. No Internet connection? Is GitHub down? (Your version is: {myVersion})\n{e.Message}");
                        return;
                    }
                }
                RunCFU(myVersion, latestVersion);
            });
        }

        private static void RunCFU(string myVersion, string latestVersion)
        {
            var hasBeta = myVersion.EndsWith("-beta");
            if (hasBeta) myVersion = myVersion[0..^5];
            string[] myVersionSplit = myVersion.Split('.');

            string[] latestVersions = latestVersion.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            var latestIsBeta = latestVersions[0].EndsWith("-beta");
            string latestRelease; string latestBeta;
            if (latestIsBeta)
            {
                latestBeta = latestVersions[0][0..^5];
                latestRelease = latestVersions[1];
            }
            else
            {
                latestRelease = latestBeta = latestVersions[0];
            }

            var result = 0;
            string[] latestVersionSplit = hasBeta ? latestBeta.Split('.') : latestRelease.Split('.');
            for (int i = 0; i < 3; i++)
            {
                var myVer_i = int.Parse(myVersionSplit[i]);
                var latestVer_i = int.Parse(latestVersionSplit[i]);
                if (myVer_i > latestVer_i) { result = 1; break; }
                else if (myVer_i < latestVer_i) { result = -1; break; }
            }
            if (result == 0)
            {
                if (hasBeta) Logger.Log($"{RichToolName} On the latest beta! ({myVersion})");
                else Logger.Log($"{RichToolName} Tools are up to date! ({myVersion})");
                return;
            }
            if (result == 1)
            {
                Logger.LogWarning($"{RichToolName} Are you a time traveller? Your {(hasBeta ? "beta" : "version")} ({myVersion}) " +
                    $"is higher than source ({(hasBeta ? latestBeta : latestRelease)})!");
                return;
            }

            int updateN;
            if (hasBeta)
            {
                if (latestIsBeta)
                {
                    string[] latestReleaseSplit = latestRelease.Split('.');
                    result = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        var myVer_i = int.Parse(myVersionSplit[i]);
                        var latestVer_i = int.Parse(latestReleaseSplit[i]);
                        if (myVer_i > latestVer_i) { result = 1; break; }
                        else if (myVer_i < latestVer_i) { result = -1; break; }
                    }
                    if (result == -1)
                    {
                        Logger.Log($"{RichToolName} Your beta ({myVersion}) is behind latest full release and latest beta! Latest release is: {latestRelease}");
                        updateN = EditorUtility.DisplayDialogComplex("FACS Utilities : Check for Updates", "Your beta is out of date!\n\n" +
                            "Go to FACS Tools Discord Server and get the latest Beta!", "Open GitHub", "Ignore", "Don't Remind Again");
                    }
                    else
                    {
                        Logger.Log($"{RichToolName} Your beta ({myVersion}) is out of date. Latest beta is: {latestBeta}");
                        updateN = EditorUtility.DisplayDialogComplex("FACS Utilities : Check for Updates", "Your beta is out of date!\n\n" +
                            "Go to FACS Tools Discord Server and get the latest Beta!", "Open GitHub", "Ignore", "Don't Remind Again");
                    }
                }
                else
                {
                    Logger.Log($"{RichToolName} Your beta ({myVersion}) is behind latest full release! Latest version is: {latestRelease}");
                    updateN = EditorUtility.DisplayDialogComplex("FACS Utilities : Check for Updates", "Your beta is out of date!\n\n" +
                        "Go to FACS Utilities GitHub page and get the latest Release!", "Open GitHub", "Ignore", "Don't Remind Again");
                }
            }
            else
            {
                Logger.Log($"{RichToolName} Your tools ({myVersion}) are out of date. Latest version is: {latestRelease}");
                updateN = EditorUtility.DisplayDialogComplex("FACS Utilities : Check for Updates", "Your tools are out of date!\n\n" +
                    "Go to FACS Utilities GitHub page and get the latest Release!", "Open GitHub", "Ignore", "Don't Remind Again");
            }

            if (updateN == 0) OpenFACSUtilitiesGitHub();
            else if (updateN == 2) { PlayerPrefs.SetInt("FACSUtilities_CheckUpdates", 0); }
        }
    }
}
#endif