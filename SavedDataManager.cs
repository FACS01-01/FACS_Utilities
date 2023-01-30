#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
//using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class SavedDataManager
    {
        public static readonly string DictDataSeparator = " ]|[ ";
        public static readonly string DictDataIndent = "-";

        public static string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("\\","/");
        public static string MainFolder = LocalAppData + "/FACS Utils";

        public static bool PathExist(string path)
        {
            return Directory.Exists(path);
        }
        public static bool FileExist(string path)
        {
            return File.Exists(path);
        }
        public static void PathCreate(string path)
        {
            Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Creating FACS Utils saved data folder at: {path}");
            Directory.CreateDirectory(path);
        }
        public static void FileCreate(string path, string header)
        {
            Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Creating FACS Utils saved data file at: {path}");
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine(header+"\n");
            }
        }
        private static void ChechSavedData(string folderpath, string filepath, string header)
        {
            if (!PathExist(folderpath))
            {
                PathCreate(folderpath);
            }
            if (!FileExist(filepath))
            {
                FileCreate(filepath, header);
            }
        }
        public static void WriteSavedData(string subfolder, string file, string header, string configParam, string configValue)
        {
            ChechSavedData(MainFolder + subfolder, MainFolder + subfolder + "/" + file, header);

            string[] lines = File.ReadAllLines(MainFolder + subfolder + "/" + file);
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
            File.WriteAllLines(MainFolder + subfolder + "/" + file, lines);
            Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Saving {configParam.Substring(0, configParam.Length - 1)}");
        }
        public static string ReadSavedData(string subfolder, string file, string header, string configParam, bool createnew)
        {
            ChechSavedData(MainFolder + subfolder, MainFolder + subfolder + "/" + file, header);

            string[] lines = File.ReadAllLines(MainFolder + subfolder + "/" + file);
            var findConfigParam = lines.Where(o => o.StartsWith(configParam));
            if (findConfigParam.Any())
            {
                Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Reading {configParam.Substring(0, configParam.Length - 1)}");
                return findConfigParam.First().Substring(configParam.Length).Replace("\n","");
            }
            else
            {
                if (createnew)
                {
                    List<string> tmp = lines.ToList(); tmp.Add(configParam);
                    lines = tmp.ToArray();
                    File.WriteAllLines(MainFolder + subfolder + "/" + file, lines);
                    Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Creating Saved Data for {configParam.Substring(0, configParam.Length - 1)}");
                }
                return "";
            }
        }
        public static void DeleteSavedData(string subfolder, string file, string header, string configParam)
        {
            ChechSavedData(MainFolder + subfolder, MainFolder + subfolder + "/" + file, header);

            string[] lines = File.ReadAllLines(MainFolder + subfolder + "/" + file);
            var findConfigParam = lines.Where(o => o.StartsWith(configParam));
            if (findConfigParam.Any())
            {
                int configParamIndex = Array.IndexOf(lines, findConfigParam.First());
                List<string> tmp = lines.ToList();
                tmp.RemoveAt(configParamIndex);
                lines = tmp.ToArray();

                File.WriteAllLines(MainFolder + subfolder + "/" + file, lines);
                Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Deleting {configParam.Substring(0, configParam.Length - 1)}");
            }
        }
        public static List<string[]> ReadDictData(string subfolder, string file, string header, List<string> branch)
        {
            ChechSavedData(MainFolder + subfolder, MainFolder + subfolder + "/" + file, header);

            List<string> branchsearch = new List<string>();
            using (StreamReader sr = new StreamReader(MainFolder + subfolder + "/" + file))
            {
                bool findDeep = branch.Contains("");
                bool cancel = false;
                string readline = "";
                bool consume = true;

                sr.ReadLine();//header
                sr.ReadLine();//newline
                for (int i = 0; i < branch.Count; i++)
                {
                    while (sr.Peek() != -1)
                    {
                        if (consume) { readline = sr.ReadLine(); consume = true; }
                        if (readline.IndexOf(DictDataIndent) == i)
                        {
                            string tmp = readline.Substring(i + 1).Replace("\n", "");
                            if (findDeep && branch[i]=="")
                            {
                                branchsearch.Add(tmp);
                                break;
                            }
                            int itmp = tmp.IndexOf(DictDataSeparator);
                            itmp = itmp != -1 ? itmp : tmp.Length;
                            string tmp1 = tmp.Substring(0, itmp);
                            if (tmp1 == branch[i])
                            {
                                branchsearch.Add(tmp);
                                break;
                            }
                        }
                        else if (readline.IndexOf(DictDataIndent) < i)
                        {
                            if (findDeep)
                            {
                                i = Math.Max(-1, readline.IndexOf(DictDataIndent) - 1);
                                if (i == -1)
                                {
                                    branchsearch = new List<string>();
                                }
                                else { branchsearch = branchsearch.GetRange(0, i+1); }
                                consume = false;
                                break;
                            }
                            cancel = true; break;
                        }
                    }
                    if (cancel || sr.Peek() == -1) { break; }
                }
            }
            if (branch.Count == branchsearch.Count)
            {
                List<string[]> output = new List<string[]>();
                foreach (string branch_i in branchsearch)
                {
                    output.Add(branch_i.Split(new[] { DictDataSeparator }, StringSplitOptions.RemoveEmptyEntries));
                }
                return output;
            }
            else { return null; }
        }
        public static void WriteDictData(string subfolder, string file, string header, List<string[]> branch)
        {
            ChechSavedData(MainFolder + subfolder, MainFolder + subfolder + "/" + file, header);

            List<string> lines = File.ReadAllLines(MainFolder + subfolder + "/" + file).ToList();

            int linesCount = lines.Count;
            int lineIndex = 1;
            int[] overwritelinesIndex = Enumerable.Repeat(-1, branch.Count).ToArray();
            bool cancel = false;

            if (linesCount > 2) for (int i = 0; i < branch.Count; i++)
            {
                while (lineIndex +1 < linesCount)
                {
                    lineIndex++;
                    if (lines[lineIndex].IndexOf(DictDataIndent) == i)
                    {
                        string tmp = lines[lineIndex].Substring(i + 1).Replace("\n", "");
                        int itmp = tmp.IndexOf(DictDataSeparator);
                        itmp = itmp != -1 ? itmp : tmp.Length;
                        string tmp1 = tmp.Substring(0, itmp);
                        if (tmp1 == branch[i][0])
                        {
                            overwritelinesIndex[i] = lineIndex;
                            break;
                        }
                    }
                    else if (lines[lineIndex].IndexOf(DictDataIndent) < i)
                    {
                        cancel = true; break;
                    }
                }
                if (cancel || lineIndex + 1 >= linesCount) { break; }
            }

            for (int i = 0; i < overwritelinesIndex.Length; i++)
            {
                if (overwritelinesIndex[i] != -1)
                {
                    lines[overwritelinesIndex[i]] = new string(' ', i) + DictDataIndent + String.Join(DictDataSeparator, branch[i]);
                }
                else
                {
                    if (i == 0)
                    {
                        for (int j = 0; j < branch.Count; j++)
                        {
                            lines.Add(new string(' ', j) + DictDataIndent + String.Join(DictDataSeparator, branch[j]));
                        }
                    }
                    else
                    {
                        int lastIndex = overwritelinesIndex[i-1];
                        if (lastIndex+1<lines.Count) for (int j = i; j < branch.Count; j++)
                        {
                            lines.Insert(lastIndex + j-i+1, new string(' ', j) + DictDataIndent + String.Join(DictDataSeparator, branch[j]));
                        }
                        else for (int j = i; j < branch.Count; j++)
                        {
                            lines.Add(new string(' ', j) + DictDataIndent + String.Join(DictDataSeparator, branch[j]));
                        }
                    }
                    break;
                }
            }

            string[] resultlines = lines.ToArray();
            File.WriteAllLines(MainFolder + subfolder + "/" + file, resultlines);
            //Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Saving data");
        }
        public static void DeleteDictData(string subfolder, string file, string header, List<string[]> branch)
        {
            ChechSavedData(MainFolder + subfolder, MainFolder + subfolder + "/" + file, header);

            List<string> lines = File.ReadAllLines(MainFolder + subfolder + "/" + file).ToList();

            int linesCount = lines.Count;
            int lineIndex = 1;
            int[] deletelinesIndex = Enumerable.Repeat(-1, branch.Count).ToArray();
            bool cancel = false;

            for (int i = 0; i < branch.Count; i++)
            {
                while (lineIndex - 1 < linesCount)
                {
                    lineIndex++;
                    if (lines[lineIndex].IndexOf(DictDataIndent) == i)
                    {
                        string tmp = lines[lineIndex].Substring(i + 1).Replace("\n", "");
                        int itmp = tmp.IndexOf(DictDataSeparator);
                        itmp = itmp != -1 ? itmp : tmp.Length;
                        string tmp1 = tmp.Substring(0, itmp);
                        if (tmp1 == branch[i][0])
                        {
                            deletelinesIndex[i] = lineIndex;
                            break;
                        }
                    }
                    else if (lines[lineIndex].IndexOf(DictDataIndent) < i)
                    {
                        cancel = true; break;
                    }
                }
                if (cancel || lineIndex - 1 >= linesCount) { break; }
            }

            for (int i = deletelinesIndex.Length; i-- > 0;)
            {
                int deleteIndex = deletelinesIndex[i];
                if (deleteIndex == -1) { continue; }
                int deleteIndent = lines[deleteIndex].IndexOf(DictDataIndent);
                lines.RemoveAt(deleteIndex);
                while (deleteIndex < lines.Count && lines[deleteIndex].IndexOf(DictDataIndent) > deleteIndent)
                {
                    lines.RemoveAt(deleteIndex);
                }
            }

            string[] resultlines = lines.ToArray();
            File.WriteAllLines(MainFolder + subfolder + "/" + file, resultlines);
            //Debug.Log($"[<color=cyan>FACS SavedDataManager</color>] Deleting data");
        }
    }
}
#endif