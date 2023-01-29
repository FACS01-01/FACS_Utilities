#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FACS01.Utilities
{
    [InitializeOnLoad]
    public class CheckForUpdates
    {
        public static SynchronizationContext syncContext = SynchronizationContext.Current;
        static CheckForUpdates()
        {
            if (SessionState.GetBool("FACSUtilities_CheckUpdatesStartup", true))
            {
                SessionState.SetBool("FACSUtilities_CheckUpdatesStartup", false);
                if (PlayerPrefs.GetInt("FACSUtilities_CheckUpdates", 1) == 1) StartCFUThread();
            }
        }

        [MenuItem("FACS Utils/Check for Updates", false, 999)]
        public static void CheckForUpdatesManual()
        {
            StartCFUThread(true);
        }

        public static void StartCFUThread(bool manual = false)
        {
            if (manual) PlayerPrefs.SetInt("FACSUtilities_CheckUpdates", 1);
            Thread thread = new Thread(() => CheckForInternetConnection());
            thread.Start();
        }

        public static void CheckForInternetConnection()
        {
            bool goodtogo = false;
            try
            {
                using (var client = new WebClient())
                {
                    using (client.OpenRead("https://raw.githubusercontent.com/FACS01-01/FACS_Utilities/main/version.txt"))
                    { goodtogo = true; }
                }
            }
            catch
            { Debug.LogWarning($"[<color=cyan>FACS Utilities</color>] Can't Check for Updates. no Internet connection? or GitHub is down?\n"); }
            if (goodtogo) syncContext.Post(_ => { RunCFU(); }, null);
        }

        public static void RunCFU()
        {
            string myVersion = File.ReadLines(Application.dataPath + "/FACS01 Utilities/version.txt").First();
            string[] myVersionSplit = myVersion.Split('.');
            string latestVersion = "";
            using (WebClient wc = new WebClient())
            { latestVersion = wc.DownloadString("https://raw.githubusercontent.com/FACS01-01/FACS_Utilities/main/version.txt"); }
            string[] latestVersionSplit = latestVersion.Split('.');
            decimal ver = int.Parse(myVersionSplit[0]) * 365.25m + int.Parse(myVersionSplit[1]) * 30.4375m + int.Parse(myVersionSplit[2]);
            decimal latestver = int.Parse(latestVersionSplit[0]) * 365.25m + int.Parse(latestVersionSplit[1]) * 30.4375m + int.Parse(latestVersionSplit[2]);

            if (ver == latestver)
            { Debug.Log($"[<color=cyan>FACS Utilities</color>] Tools are up to date!\n"); return; }

            if (ver > latestver)
            { Debug.LogWarning($"[<color=cyan>FACS Utilities</color>] Are you a time traveller? Tools version is higher than source!\n"); return; }

            int updateN;
            string lastFullReinstall = "";
            using (WebClient wc = new WebClient())
            { lastFullReinstall = wc.DownloadString("https://raw.githubusercontent.com/FACS01-01/FACS_Utilities/main/last%20full%20reinstall.txt"); }
            string[] lastFullReinstallSplit = lastFullReinstall.Split('.');
            decimal lastfullreins = int.Parse(lastFullReinstallSplit[0]) * 365.25m + int.Parse(lastFullReinstallSplit[1]) * 30.4375m + int.Parse(lastFullReinstallSplit[2]);

            if (ver < lastfullreins)
            {
                Debug.Log($"[<color=cyan>FACS Utilities</color>] Your tools are out of date, and are incompatible with newer versions.\n");
                updateN = EditorUtility.DisplayDialogComplex("FACS Utilities : Check for Updates", "Your tools are out of date," +
                    " and are incompatible with newer versions!\n\nGo to FACS Utilities GitHub page and get the latest Release.\nAnd remember to delete 'FACS Utilities' folder" +
                    " before importing the newer one!", "Open GitHub", "Ignore", "Don't Remind Again");
                if (updateN == 0) Process.Start("https://github.com/FACS01-01/FACS_Utilities");
                else if (updateN == 2) { PlayerPrefs.SetInt("FACSUtilities_CheckUpdates", 0); }
                return;
            }

            Debug.Log($"[<color=cyan>FACS Utilities</color>] Your tools are out of date.\n");
            updateN = EditorUtility.DisplayDialogComplex("FACS Utilities : Check for Updates", "Your tools are out of date!\n\n" +
                "Go to FACS Utilities GitHub page and get the latest Release!", "Open GitHub", "Ignore", "Don't Remind Again");
            if (updateN == 0) Process.Start("https://github.com/FACS01-01/FACS_Utilities");
            else if (updateN == 2) { PlayerPrefs.SetInt("FACSUtilities_CheckUpdates", 0); }
        }
    }
}
#endif