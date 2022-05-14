#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class FixVRC3Scripts : EditorWindow
    {
        public DefaultAsset selectedFolder;

        private static FACSGUIStyles FacsGUIStyles;

        private readonly string header = "MonoBehaviour:";
        private readonly string separator = "--- !u!";
        private readonly string beginParams = "  m_EditorClassIdentifier:";
        private readonly Dictionary<string, string[]> VRCScriptsAndParams = new Dictionary<string, string[]>()
        {
            { "VRCAnimatorLayerControl", new string[] { "playable", "layer", "goalWeight", "blendDuration" } },//**
            { "VRCAnimatorLocomotionControl", new string[] { "disableLocomotion" } },
            { "VRCAnimatorTemporaryPoseSpace", new string[] { "enterPoseSpace", "fixedDelay", "delayTime" } },
            { "VRCAnimatorTrackingControl", new string[] { "trackingHead", "trackingLeftHand", "trackingRightHand", "trackingHip" } },
            { "VRCAvatarDescriptor", new string[] { "ViewPosition", "ScaleIPD", "customExpressions", "expressionParameters" } },
            { "VRCAvatarParameterDriver", new string[] { "parameters", "localOnly" } },//*
            { "VRCExpressionParameters", new string[] { "parameters" } },//*
            { "VRCExpressionsMenu", new string[] { "controls" } },
            { "VRCPlayableLayerControl", new string[] { "layer", "goalWeight", "blendDuration" } },//**
            { "VRCSpatialAudioSource", new string[] { "Gain", "Far", "Near", "VolumetricRadius", "EnableSpatialization", "UseAudioSourceVolumeCurve" } },
            { "VRCStation", new string[] { "PlayerMobility", "canUseStationFromStation", "animatorController", "disableStationExit", "seated", "stationEnterPlayerLocation", "stationExitPlayerLocation", "controlsObject" } },
			{ "VRCPhysBoneCollider", new string[] { "rootTransform", "shapeType", "insideBounds", "radius", "height", "position", "rotation", "bonesAsSpheres" } },
			{ "VRCPhysBone", new string[] { "rootTransform", "ignoreTransforms", "endpointPosition", "multiChildType", "pull", "pullCurve", "spring", "springCurve", "stiffness", "stiffnessCurve", "immobileType", "immobile", "immobileCurve", "gravity", "gravityCurve", "gravityFalloff", "gravityFalloffCurve", "allowCollision", "radius", "radiusCurve", "colliders", "limitType", "maxAngleX", "maxAngleXCurve", "maxAngleZ", "maxAngleZCurve", "limitRotation", "limitRotationXCurve", "limitRotationYCurve", "limitRotationZCurve", "allowGrabbing", "allowPosing", "grabMovement", "maxStretch", "maxStretchCurve", "isAnimated", "parameter", "showGizmos", "boneOpacity", "limitOpacity"} }
        };

        private Dictionary<(string, string), string> VRCScriptsGUIDs;
        private List<(string, string)> GUIDsIgnore;
        private Dictionary<(string, string), (string, string)> GUIDsOldToNew;
        private Dictionary<(string, string), (int, int)> GUIDsFixStats;

        private readonly string VRCSDK3Adll_path = "Assets/VRCSDK/Plugins/VRCSDK3A.dll";
        private readonly string PhysBonedll_path = "Assets/VRCSDK/Plugins/VRC.SDK3.Dynamics.PhysBone.dll";

        private int fixedFilesCount;
        private string output_print;

        
        
        [MenuItem("FACS Utils/Repair Avatar/Fix VRC3 Scripts", false, 1003)]
        public static void ShowWindow()
        {
            GetWindow(typeof(FixVRC3Scripts), false, "Fix VRC3 Scripts", true);
        }
        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"<color=cyan><b>Fix VRC SDK3 Scripts</b></color>\n\nScans the selected folder and subfolders, and assigns the correct scripts to *prefab, *controller and *asset files.\n", FacsGUIStyles.helpbox);
            selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField(selectedFolder, typeof(DefaultAsset), false, GUILayout.Height(50));

            if (GUILayout.Button("Run Fix!", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                if (selectedFolder != null)
                {
                    Debug.Log("FIX VRC3 SCRIPTS - BEGINS");

                    fixedFilesCount = 0;
                    output_print = "";
                    VRCScriptsGUIDs = new Dictionary<(string, string), string>();
                    GUIDsIgnore = new List<(string, string)>();
                    GUIDsOldToNew = new Dictionary<(string, string), (string, string)>();
                    GUIDsFixStats = new Dictionary<(string, string), (int, int)>();

                    GetScriptsGUIDs(); //VRCScriptsGUIDs
                    foreach (var guidKey in VRCScriptsGUIDs.Keys)
                    {
                        GUIDsFixStats.Add(guidKey, (0,0));
                    }

                    string[] filePaths = Directory.GetFiles(AssetDatabase.GetAssetPath(selectedFolder), "*.asset", SearchOption.AllDirectories);
                    string[] filePaths1 = Directory.GetFiles(AssetDatabase.GetAssetPath(selectedFolder), "*.controller", SearchOption.AllDirectories);
                    string[] filePaths2 = Directory.GetFiles(AssetDatabase.GetAssetPath(selectedFolder), "*.prefab", SearchOption.AllDirectories);
                    filePaths = filePaths.Concat(filePaths1).Concat(filePaths2).ToArray();

                    bool refresh = false;
                    int progress = 0; float progressTotal = filePaths.Length;
                    foreach (string filePath in filePaths)
                    {
                        progress++;
                        string path2 = filePath.Replace('\\', '/');

                        EditorUtility.DisplayProgressBar("Fix VRC3 Scripts", $"Processing file: {Path.GetFileName(filePath)}", progress/progressTotal);

                        if (IsYAML(path2)) { Fix(path2); refresh = true; }
                    }
                    if (refresh) AssetDatabase.Refresh();

                    GenerateResults();
                    EditorUtility.ClearProgressBar();

                    Debug.Log("FIX VRC3 SCRIPTS - FINISHED");
                }
                else
                {
                    ShowNotification(new GUIContent("Empty field?"));
                }

            }
            if (output_print != null && output_print != "")
            {
                FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(output_print, FacsGUIStyles.helpbox);
            }
        }
        private void GenerateResults()
        {
            string end = $"";
            output_print = $"Results:\n   • <color=green>Fixed files:</color> {fixedFilesCount}\n";
            var tmp = GUIDsFixStats.Where(key => key.Value.Item2 + key.Value.Item1 > 0);
            foreach (KeyValuePair<(string, string), (int, int)> valuepair in tmp.OrderBy(key => VRCScriptsGUIDs[key.Key]))
            {
                if (valuepair.Value.Item2 == 0)
                {
                    end += $"   • {VRCScriptsGUIDs[valuepair.Key]}:  {valuepair.Value.Item1} already working\n";
                }
                else output_print += $"   • <color=cyan>{VRCScriptsGUIDs[valuepair.Key]}:</color>  {valuepair.Value.Item2} fixed, {valuepair.Value.Item1} already working\n";
            }
            output_print += end;
        }
        private bool CompareGUID(string[] arrLine, int scriptLineIndex, (string, string) scriptGUID, List<string> paramsCollection)
        {
            bool WasModified = false;
            if (GUIDsOldToNew.TryGetValue(scriptGUID, out (string, string) newGUID))
            {
                arrLine[scriptLineIndex] = "  m_Script: {fileID: " + newGUID.Item2 + ", guid: " + newGUID.Item1 + ", type: 3}";
                var tmp = GUIDsFixStats[newGUID]; tmp.Item2++; GUIDsFixStats[newGUID] = tmp;
                WasModified = true;
            }
            else if (VRCScriptsGUIDs.TryGetValue(scriptGUID, out string scriptname))
            {
                // ya era bueno
                var tmp = GUIDsFixStats[scriptGUID]; tmp.Item1++; GUIDsFixStats[scriptGUID] = tmp;
            }
            else if (GUIDsIgnore.Contains(scriptGUID))
            {
                // ignorar, no es vrc
            }
            else
            {
                List<((string, string), int, int)> filter = new List<((string, string), int, int)>();
                foreach (var guidKey in VRCScriptsGUIDs.Keys)
                {
                    int intersect = paramsCollection.Intersect(VRCScriptsAndParams[VRCScriptsGUIDs[guidKey]]).Count();
                    if (intersect > 0)
                    {
                        filter.Add((guidKey, intersect, VRCScriptsAndParams[VRCScriptsGUIDs[guidKey]].Length));
                    }
                }
                if (filter.Any())
                {
                    if (filter.Count() == 1)
                    {
                        newGUID = filter.First().Item1;
                        GUIDsOldToNew.Add(scriptGUID, newGUID);
                        arrLine[scriptLineIndex] = "  m_Script: {fileID: " + newGUID.Item2 + ", guid: " + newGUID.Item1 + ", type: 3}";
                        var tmp = GUIDsFixStats[newGUID]; tmp.Item2++; GUIDsFixStats[newGUID] = tmp;
                        WasModified = true;
                    }
                    else
                    {
                        var orderedFilter = filter.OrderByDescending(x => x.Item2).ThenBy(x => x.Item3);
                        newGUID = orderedFilter.First().Item1;
                        GUIDsOldToNew.Add(scriptGUID, newGUID);
                        arrLine[scriptLineIndex] = "  m_Script: {fileID: " + newGUID.Item2 + ", guid: " + newGUID.Item1 + ", type: 3}";
                        var tmp = GUIDsFixStats[newGUID]; tmp.Item2++; GUIDsFixStats[newGUID] = tmp;
                        WasModified = true;
                    }
                }
                else
                {
                    //add ignore, no es vrc
                    GUIDsIgnore.Add(scriptGUID);
                }
            }
            return WasModified;
        }
        private void Fix(string path)
        {
            bool WasModified = false;
            string[] arrLine = File.ReadAllLines(path);
            int arrLineIndex = 2; int arrLineCount = arrLine.Length;
            bool inspect = false; int scriptLineIndex = 0;
            bool collect = false; (string, string) scriptGUID = ("", "");
            List<string> paramsCollection = new List<string>();
            while (arrLineIndex < arrLineCount)
            {
                string line = arrLine[arrLineIndex];

                if (inspect)
                {
                    if (line.StartsWith(separator))
                    {
                        inspect = collect = false;
                        // process:scriptGUID, paramsCollection
                        WasModified = CompareGUID(arrLine, scriptLineIndex, scriptGUID, paramsCollection) || WasModified;
                        scriptGUID = ("", ""); paramsCollection = new List<string>();
                    }
                    else if (line[2] != ' ')
                    {
                        if (line.StartsWith("  m_"))
                        {
                            if (collect)
                            {
                                collect = false;
                            }
                            if (line.StartsWith("  m_Script: "))
                            {
                                scriptLineIndex = arrLineIndex;
                                scriptGUID = GetGUIDfromLine(line);
                            }
                            else if (line.StartsWith(beginParams))
                            {
                                collect = true;
                            }
                        }
                        else if (collect && !line.StartsWith("  - "))
                        {
                            paramsCollection.Add(line.Substring(2, line.IndexOf(":") - 2));
                        }
                    }
                }
                else if (line == header)
                {
                    inspect = true;
                    paramsCollection = new List<string>();
                }

                arrLineIndex++;
            }
            //if had something, process
            if (paramsCollection.Any())
            {
                WasModified = CompareGUID(arrLine, scriptLineIndex, scriptGUID, paramsCollection) || WasModified;
            }

            if (WasModified)
            {
                File.WriteAllLines(path, arrLine); fixedFilesCount++;
            }
        }
        private (string, string) GetGUIDfromLine(string line)
        {
            if (!line.Contains("fileID: ") || !line.Contains("guid: "))
            {
                return ("", "");
            }
            string tmp = line.Substring(line.IndexOf("fileID: ")+8);
            int itmp = tmp.IndexOf(",");
            string FileID = tmp.Substring(0, itmp);
            tmp = tmp.Substring(tmp.IndexOf("guid: ") + 6);
            itmp = tmp.IndexOf(",");
            string GUID = tmp.Substring(0, itmp);

            return (GUID, FileID);
        }
        private void GetScriptsGUIDs()
        {
            string[] VRCScriptsNames = VRCScriptsAndParams.Keys.ToArray();
            Object[] VRCSDK3A_scripts = AssetDatabase.LoadAllAssetRepresentationsAtPath(VRCSDK3Adll_path);
            foreach (Object script in VRCSDK3A_scripts)
            {
                if (VRCScriptsNames.Contains(script.name) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGUID, out long scriptFileId))
                {
                    VRCScriptsGUIDs.Add((scriptGUID, scriptFileId.ToString()), script.name);
                    //VRCScriptsNewAndOld.Add(((script.name, VRCScriptsAndParams[script.name]), scriptGUID, scriptFileId.ToString(), "", ""));
                }
            }

            Object[] PhyBone_scripts = AssetDatabase.LoadAllAssetRepresentationsAtPath(PhysBonedll_path);
            foreach (Object script in PhyBone_scripts)
            {
                if (VRCScriptsNames.Contains(script.name) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGUID, out long scriptFileId))
                {
                    VRCScriptsGUIDs.Add((scriptGUID, scriptFileId.ToString()), script.name);
                    //VRCScriptsNewAndOld.Add(((script.name, VRCScriptsAndParams[script.name]), scriptGUID, scriptFileId.ToString(), "", ""));
                }
            }
        }
        static bool IsYAML(string path)
        {
            bool isYAML = false;
            if (!File.Exists(path)) return isYAML;
            using (StreamReader sr = new StreamReader(path))
            {
                if (sr.Peek() >= 0 && sr.ReadLine().Contains("%YAML 1.1")) isYAML = true;
            }
            return isYAML;
        }
        void OnDestroy()
        {
            FacsGUIStyles = null;
            VRCScriptsGUIDs = null;
            GUIDsIgnore = null;
            GUIDsOldToNew = null;
            GUIDsFixStats = null;
            fixedFilesCount = default;
            output_print = "";
        }
    }
}
#endif
