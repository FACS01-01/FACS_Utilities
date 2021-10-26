#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class SavedDataManager
    {
        public static string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("\\","/");
        public static string SavedDataFolder = LocalAppData + "/FACS Utils";
        public static string SavedDataFile = SavedDataFolder + "/savedData.txt";

        public static bool SavedDataPathExist()
        {
            return Directory.Exists(SavedDataFolder);
        }
        public static bool SavedDataFileExist()
        {
            return File.Exists(SavedDataFile);
        }
        public static void SavedDataPathCreate()
        {
            Debug.Log($"[<color=green>FACS SavedDataManager</color>] Creating FACS Utils saved data folder at: {SavedDataFolder}");
            Directory.CreateDirectory(SavedDataFolder);
        }
        public static void SavedDataFileCreate()
        {
            Debug.Log($"[<color=green>FACS SavedDataManager</color>] Creating FACS Utils saved data file at: {SavedDataFile}");
            using (StreamWriter sw = File.CreateText(SavedDataFile))
            {
                sw.WriteLine("FACS Utilities - Configs\n");
            }
        }
        public static void WriteSavedData(string configParam, string configValue)
        {
            if (!SavedDataPathExist())
            {
                SavedDataPathCreate();
            }
            if (!SavedDataFileExist())
            {
                SavedDataFileCreate();
            }
            string[] lines = File.ReadAllLines(SavedDataFile);
            var findConfigParam = lines.Where(o => o.StartsWith(configParam));
            if (findConfigParam.Any())
            {
                int configParamIndex = Array.IndexOf(lines, findConfigParam.First());
                lines[configParamIndex] = configParam + configValue;
            }
            else
            {
                List<string> tmp = lines.ToList(); tmp.Add(configParam + configValue);
                lines = tmp.ToArray();
            }
            File.WriteAllLines(SavedDataFile, lines);
            Debug.Log($"[<color=green>FACS SavedDataManager</color>] Saving {configParam.Substring(0, configParam.Length - 1)}");
        }
        public static string ReadSavedData(string configParam)
        {
            if (!SavedDataPathExist())
            {
                SavedDataPathCreate();
            }
            if (!SavedDataFileExist())
            {
                SavedDataFileCreate();
            }
            string[] lines = File.ReadAllLines(SavedDataFile);
            var findConfigParam = lines.Where(o => o.StartsWith(configParam));
            if (findConfigParam.Any())
            {
                Debug.Log($"[<color=green>FACS SavedDataManager</color>] Reading {configParam.Substring(0, configParam.Length - 1)}");
                return findConfigParam.First().Substring(configParam.Length).Replace("\n","");
            }
            else
            {
                List<string> tmp = lines.ToList(); tmp.Add(configParam);
                lines = tmp.ToArray();
                File.WriteAllLines(SavedDataFile, lines);
                Debug.Log($"[<color=green>FACS SavedDataManager</color>] Creating Saved Data for {configParam.Substring(0, configParam.Length - 1)}");
                return "";
            }
        }
    }
}
#endif