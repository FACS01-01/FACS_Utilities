#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class FixAnimations : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[Fix Animations]" + Logger.EndTag;
        private const string RichAnimationSourcesName = Logger.ConceptTag + "Animation Sources" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private readonly static AnimationsFromGOs AFGOs = new();

        private static string Results = null;

        [MenuItem("FACS Utils/Repair Project/Fix Animations", false, 1054)]
        [MenuItem("FACS Utils/Animation/Fix Animations", false, 1101)]
        private static void ShowWindow()
        {
            var window = (FixAnimations)GetWindow(typeof(FixAnimations), false, "Fix Animations", true);
            window.maxSize = new Vector2(1000, 700); window.minSize = new Vector2(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null)
            {
                FacsGUIStyles = new FACSGUIStyles();
                FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter;
                FacsGUIStyles.Label.wordWrap = false;
            }
            var windowWidth = this.position.size.x;
            EditorGUILayout.LabelField($"<color=cyan><b>Fix Animations</b></color>\n\n" +
                $"Scans Animations inside the selected Animation Sources, and tries to repair broken paths and properties, " +
                $"comparing them to their respective Animation Root.\n", FacsGUIStyles.Helpbox);

            AFGOs.OnGUI(FacsGUIStyles, windowWidth);

            GUILayout.Space(10);
            if (!string.IsNullOrEmpty(Results)) EditorGUILayout.LabelField(Results, FacsGUIStyles.Helpbox);
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            if (AFGOs.animationRoots.Count > 1 &&
                GUILayout.Button("Auto Detect", FacsGUIStyles.Button, GUILayout.Height(20), GUILayout.MaxWidth(windowWidth * 0.75f))) AutoDetect();
            if (AFGOs.animationSources.Any(animS => animS.Count > 1) &&
                GUILayout.Button("Run!", FacsGUIStyles.Button, GUILayout.Height(40), GUILayout.MaxWidth(windowWidth * 0.75f))) RunFix();
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        private void AutoDetect()
        {
            Results = null;

            if (!AFGOs.AutoDetect(RichToolName)) ShowNotification(new GUIContent("No Animation Root\nhas Animation Sources"));
        }

        private static void RunFix()
        {
            Results = null; _ = new FixAnimationsData();
            Logger.Log(RichToolName + " Finished fixing animations!");
        }

        private void OnDestroy()
        {
            NullVars();
            FacsGUIStyles = null;
        }

        private void NullVars()
        {
            AFGOs.Clear();
            Results = null;
        }

        private class FixAnimationsData
        {
            private const System.Globalization.NumberStyles hexStyle = System.Globalization.NumberStyles.HexNumber;
            private readonly System.Globalization.CultureInfo hexCulture = System.Globalization.CultureInfo.InvariantCulture;

            Dictionary<GameObject, Dictionary<string, List<GameObject>>> GO_Paths = new();
            Dictionary<string, List<GameObject>> buildingPaths = null;
            Dictionary<GameObject, Dictionary<uint, string>> GO_HashesToPaths = new();
            Dictionary<uint, string> buildingHTP = null;
            Dictionary<AnimationClip, List<GameObject>> AnimsToOrigins = new();
            HashSet<AnimationClip> addedACs = new();

            Dictionary<GameObject, Dictionary<System.Type, HashSet<string>>> GO_Types_Props = new();
            Dictionary<GameObject, Dictionary<System.Type, Dictionary<uint, string>>> GO_Types_HashesToProps = new();

            Dictionary<GameObject, Dictionary<System.Type, HashSet<Shader>>> GO_Types_Shaders = new();
            Dictionary<Shader, HashSet<string>> Shader_Props = new();
            Dictionary<Shader, Dictionary<uint, string>> Shader_HashesToProps = new();

            private class FixedData
            {
                public Dictionary<string, string> paths = new();
                public Dictionary<string, Dictionary<System.Type, Dictionary<string, string>>> paths_types_props = new();
            }
            private class BrokenData
            {
                public HashSet<string> paths = new();
                public Dictionary<string, HashSet<System.Type>> paths_types = new();
                public Dictionary<string, Dictionary<System.Type, HashSet<string>>> paths_types_props = new();
            }
            Dictionary<AnimationClip, FixedData> Fixes = new();
            Dictionary<AnimationClip, BrokenData> Brokens = new();

            static Regex PathRegex = new(@"^path_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex GameObjectRegex = new(@"^missed_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex MonoBehaviourRegex = new(@"^script_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex EngineTypeRegex = new(@"^typetree_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex ParticleSystemRegex = new(@"^ParticleSystem_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex VisualEffectRegex = new(@"^VisualEffect_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex ParticleForceFieldRegex = new(@"^ParticleForceField_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex UserDefinedRegex = new(@"^UserDefined_0x([0-F]+)_([a-zA-Z0-9]+)");
            static Regex MeshFilterRegex = new(@"^MeshFilter_0x([0-F]+)_([a-zA-Z0-9]+)");

            static (bool,bool,uint) TryParseUint(string hash)
            {
                var hasbrokenHash = uint.TryParse(hash, out var brokenHash);
                return (true, hasbrokenHash, brokenHash);
            }

            Dictionary<System.Type, System.Func<(bool hasbrokenKW, bool hasbrokenHash, uint brokenHash)>> TypePropUnhash = new()
            {
                {
                    typeof(SkinnedMeshRenderer), () =>
                    {
                        if (analyzingPROPERTYNAME.StartsWith("blendShape.")) return TryParseUint(analyzingPROPERTYNAME[11..]);
                        return (false, false, 0);
                    }
                },
                {
                    typeof(ParticleSystem), () =>
                    {
                        var match = ParticleSystemRegex.Match(analyzingPROPERTYNAME);
                        if (match.Success) return TryParseUint(match.Groups[1].Value);
                        return (false, false, 0);
                    }
                },
                {
                    typeof(UnityEngine.VFX.VisualEffect), () =>
                    {
                        var match = VisualEffectRegex.Match(analyzingPROPERTYNAME);
                        if (match.Success) return TryParseUint(match.Groups[1].Value);
                        return (false, false, 0);
                    }
                },
                {
                    typeof(ParticleSystemForceField), () =>
                    {
                        var match = ParticleForceFieldRegex.Match(analyzingPROPERTYNAME);
                        if (match.Success) return TryParseUint(match.Groups[1].Value);
                        return (false, false, 0);
                    }
                },
                {
                    typeof(MeshFilter), () =>
                    {
                        var match = MeshFilterRegex.Match(analyzingPROPERTYNAME);
                        if (match.Success) return TryParseUint(match.Groups[1].Value);
                        return (false, false, 0);
                    }
                },
                {
                    typeof(GameObject), () =>
                    {
                        var match = GameObjectRegex.Match(analyzingPROPERTYNAME);
                        if (match.Success) return TryParseUint(match.Groups[1].Value);
                        return (false, false, 0);
                    }
                },
            };

            bool analyzingCBObjRef = false;
            AnimationClip analyzingAC = null;
            EditorCurveBinding analyzingECB = default;
            string analyzingPATH = null;
            string analyzingNEWPATH = null;
            System.Type analyzingTYPE = null;
            bool analyzingNEWTYPE = false;
            static string analyzingPROPERTYNAME = null;

            internal FixAnimationsData()
            {
                analyzingPROPERTYNAME = null;
                for (int i = AFGOs.animationRoots.Count - 2; i >= 0; i--)
                {
                    var validGO = false; addedACs.Clear();
                    var sources = AFGOs.animationSources[i];
                    for (int j = 0; j < sources.Count - 1; j++)
                    {
                        var asset = sources[j];
                        if (!asset) { sources.RemoveAt(j); j--; continue; }
                        if (asset is RuntimeAnimatorController rac)
                        {
                            if (AddAnimationClips(rac.animationClips, AFGOs.animationRoots[i])) validGO = true;
                            else { sources.RemoveAt(j); j--; }
                        }
                        else if (asset is AnimationClip ac)
                        {
                            if (AddAnimationClip(ac, AFGOs.animationRoots[i])) validGO = true;
                            else { sources.RemoveAt(j); j--; }
                        }
                    }
                    if (!validGO) { AFGOs.animationRoots.RemoveAt(i); AFGOs.animationSources.RemoveAt(i); }
                }
                addedACs.Clear();
                var animClipsN = AnimsToOrigins.Count * 2;
                if (animClipsN == 0)
                {
                    Logger.LogWarning(RichToolName + " No " + Logger.RichAnimationClip + " found in " + RichAnimationSourcesName + ".");
                    return;
                }

                var animClip_i = 1.0f / animClipsN; var animClipProcess = 0.0f;
                analyzingCBObjRef = true;
                foreach (var kvp in AnimsToOrigins)
                {
                    animClipProcess += animClip_i;
                    EditorUtility.DisplayProgressBar("FACS Utilities - Fix Animations", "Please wait...", animClipProcess);
                    analyzingAC = kvp.Key;
                    foreach (var cb in AnimationUtility.GetObjectReferenceCurveBindings(analyzingAC)) AnalyzePath(kvp.Value, cb);
                }
                analyzingCBObjRef = false;
                foreach (var kvp in AnimsToOrigins)
                {
                    animClipProcess += animClip_i;
                    EditorUtility.DisplayProgressBar("FACS Utilities - Fix Animations", "Please wait...", animClipProcess);
                    analyzingAC = kvp.Key;
                    foreach (var cb in AnimationUtility.GetCurveBindings(analyzingAC)) AnalyzePath(kvp.Value, cb);
                }
                EditorUtility.DisplayProgressBar("FACS Utilities - Fix Animations", "Please wait...", 1);
                foreach (var modAC in addedACs) AssetDatabase.SaveAssetIfDirty(modAC);

                Results = "";
                if (Fixes.Count>0) Results += $"\nFixed {Logger.RichAnimationClips}: <b>{Fixes.Count}</b>";
                if (Brokens.Count>0) Results += $"\n{Logger.RichAnimationClips} with problems: <b>{Brokens.Count}</b>";
                Results += "\n";

                foreach (var kvp in Fixes)
                {
                    var sb = new System.Text.StringBuilder($"{RichToolName} Fixed {Logger.RichAnimationClip} {Logger.AssetTag}{kvp.Key.name}{Logger.EndTag}");
                    var fixedData = kvp.Value;
                    if (fixedData.paths.Count>0)
                    {
                        sb.Append("\n- <b>Fixed Paths</b>:");
                        foreach (var oldPath in fixedData.paths.Keys.OrderBy(k => k))
                        { sb.Append($"\n   * {oldPath} => {fixedData.paths[oldPath]}"); }
                    }
                    if (fixedData.paths_types_props.Count>0)
                    {
                        sb.Append("\n- <b>Fixed Properties</b>:");
                        foreach (var path in fixedData.paths_types_props.Keys.OrderBy(k=>k))
                        {
                            if (string.IsNullOrEmpty(path)) sb.Append($"\n   * <Animation Root>:");
                            else sb.Append($"\n   * {path}:");
                            var types_props = fixedData.paths_types_props[path];
                            foreach (var type in types_props.Keys.OrderBy(k=>k.FullName))
                            {
                                var props = types_props[type];
                                foreach (var oldProp in props.Keys.OrderBy(k => k))
                                { sb.Append($"\n     ** {type.Name}:  {oldProp} => {props[oldProp]}"); }
                            }
                        }
                    }
                    Logger.Log(sb, kvp.Key);
                }
                foreach (var kvp in Brokens)
                {
                    var sb = new System.Text.StringBuilder($"{RichToolName} Problems in {Logger.RichAnimationClip} {Logger.AssetTag}{kvp.Key.name}{Logger.EndTag}");
                    var brokenData = kvp.Value;
                    if (brokenData.paths.Count>0)
                    {
                        sb.Append("\n- <b>Broken Paths</b>:");
                        foreach (var bpath in brokenData.paths.OrderBy(k=>k)) sb.Append($"\n   * {bpath}");
                    }
                    if (brokenData.paths_types.Count>0)
                    {
                        sb.Append("\n- <b>Missing Components</b>:");
                        foreach (var path in brokenData.paths_types.Keys.OrderBy(k=>k))
                        {
                            if (string.IsNullOrEmpty(path)) sb.Append($"\n   * <Animation Root>:");
                            else sb.Append($"\n   * {path}:");
                            foreach (var type in brokenData.paths_types[path].Select(k => k.FullName).OrderBy(k => k))
                            { sb.Append($"\n     ** {type}"); }
                        }
                    }
                    if (brokenData.paths_types_props.Count>0)
                    {
                        sb.Append("\n- <b>Missing Properties</b>:");
                        foreach (var path in brokenData.paths_types_props.Keys.OrderBy(k => k))
                        {
                            if (string.IsNullOrEmpty(path)) sb.Append($"\n   * <Animation Root>:");
                            else sb.Append($"\n   * {path}:");
                            var types_props = brokenData.paths_types_props[path];
                            foreach (var type in types_props.Keys.OrderBy(k => k.FullName))
                            { foreach (var prop in types_props[type].OrderBy(k => k)) sb.Append($"\n     ** {type.Name}:  {prop}"); }
                        }
                    }
                    Logger.LogWarning(sb, kvp.Key);
                }
                EditorUtility.ClearProgressBar();
            }

            private void AnalyzePath(List<GameObject> GOs, EditorCurveBinding cB)
            {
                analyzingECB = cB;
                analyzingPATH = cB.path;
                analyzingTYPE = cB.type;
                analyzingPROPERTYNAME = cB.propertyName;
                if (analyzingPATH == null) analyzingPATH = "";
                analyzingNEWPATH = null; analyzingNEWTYPE = false;

                if (Brokens.TryGetValue(analyzingAC, out var brokenData1) && brokenData1.paths.Contains(analyzingPATH)) return;

                var match = PathRegex.Match(analyzingPATH);
                if (match.Success && uint.TryParse(match.Groups[1].Value, hexStyle, hexCulture, out uint pathHash))
                {
                    foreach (var rootGO in GOs)
                    {
                        if (GO_HashesToPaths[rootGO].TryGetValue(pathHash, out var foundPath))
                        {
                            analyzingNEWPATH = foundPath;
                            var isFixed = AnalyzeType(rootGO);
                            if (isFixed == 2) return;
                            else if (isFixed == 1) { SetCurveFix(); return; }
                        }
                    }
                }
                foreach (var rootGO in GOs)
                {
                    if (GO_Paths[rootGO].ContainsKey(analyzingPATH))
                    { analyzingNEWPATH = analyzingPATH; if (AnalyzeType(rootGO) > 0) return; }
                }

                if (analyzingNEWPATH != null)
                {
                    if (analyzingNEWPATH != analyzingPATH) SetCurveFix();
                    if (!analyzingNEWTYPE)
                    {
                        if (!Brokens.TryGetValue(analyzingAC, out var brokenData)) { brokenData = new(); Brokens[analyzingAC] = brokenData; }
                        if (!brokenData.paths_types.TryGetValue(analyzingNEWPATH, out var types)) { types = new(); brokenData.paths_types[analyzingNEWPATH] = types; }
                        types.Add(analyzingTYPE);
                    }
                    else CollectBrokenProp();
                }
                else
                {
                    if (!Brokens.TryGetValue(analyzingAC, out var brokenData)) { brokenData = new(); Brokens[analyzingAC] = brokenData; }
                    brokenData.paths.Add(analyzingPATH);
                }
            }

            private int AnalyzeType(GameObject rootGO) //0:broken;1:works;2:fixed
            {
                var paths = GO_Paths[rootGO][analyzingNEWPATH];
                var hasbrokenKW = false; var hasbrokenHash = false; var brokenHash = 0U;
                if (typeof(Renderer).IsAssignableFrom(analyzingTYPE))
                {
                    if (analyzingPROPERTYNAME.StartsWith("material."))
                    {
                        foreach (var path in paths)
                        {
                            GetAnimatableBindings(path);
                            if (!GO_Types_Shaders.TryGetValue(path, out var types)) continue;
                            foreach (var t in types.Keys.OrderBy(t => ComponentDependencies.InheritanceDepth(t, typeof(Component))))
                            {
                                if (!analyzingTYPE.IsAssignableFrom(t)) continue;
                                analyzingNEWTYPE = true;
                                var shadersInT = types[t];
                                var matpropfix = AnalyzeMaterialProperty(shadersInT);
                                if (matpropfix > 0) return matpropfix;
                            }
                        }
                        return 0;
                    }
                }

                if (TypePropUnhash.TryGetValue(analyzingTYPE, out var typePropUnhasher))
                {
                    var res = typePropUnhasher();
                    hasbrokenKW = res.hasbrokenKW; hasbrokenHash = res.hasbrokenHash; brokenHash = res.brokenHash;
                }
                if (!hasbrokenKW)
                {
                    Match match = typeof(MonoBehaviour).IsAssignableFrom(analyzingTYPE) ?
                        MonoBehaviourRegex.Match(analyzingPROPERTYNAME) : EngineTypeRegex.Match(analyzingPROPERTYNAME);
                    if (match.Success) hasbrokenHash = uint.TryParse(match.Groups[1].Value, hexStyle, hexCulture, out brokenHash);
                }

                foreach (var path in paths)
                {
                    GetAnimatableBindings(path);
                    var Types_HashesToProps = GO_Types_HashesToProps[path];
                    foreach (var t in Types_HashesToProps.Keys.OrderBy(t => ComponentDependencies.InheritanceDepth(t, typeof(Component))))
                    {
                        if (!analyzingTYPE.IsAssignableFrom(t)) continue;
                        analyzingNEWTYPE = true;
                        var props = GO_Types_Props[path][t];
                        if (props.Contains(analyzingPROPERTYNAME)) return 1;
                        if (hasbrokenHash && Types_HashesToProps[t].TryGetValue(brokenHash, out var reversedProp))
                        { SetCurveFix(reversedProp); return 2; }
                    }
                }
                return 0;
            }

            private int AnalyzeMaterialProperty(HashSet<Shader> Shaders) //0:broken;1:works;2:fixed
            {
                var matProp = analyzingPROPERTYNAME[9..];
                var hashParsed = uint.TryParse(matProp, out uint materialHash);
                foreach (var shader in Shaders)
                {
                    if (hashParsed && Shader_HashesToProps[shader].TryGetValue(materialHash, out var reversedProp))
                    { SetCurveFix("material."+reversedProp); return 2; }
                    if (Shader_Props[shader].Contains(matProp)) return 1;
                }
                return 0;
            }

            private void GetAnimatableBindings(GameObject go)
            {
                if (!GO_Types_HashesToProps.ContainsKey(go)) GenerateAnimatableBindings(go);
                if (analyzingCBObjRef && typeof(Renderer).IsAssignableFrom(analyzingTYPE))
                {
                    var ORkfs = AnimationUtility.GetObjectReferenceCurve(analyzingAC, analyzingECB).Select(kf => kf.value).Distinct().Where(o => o && o is Material);
                    if (ORkfs.Any())
                    {
                        var shaders = ORkfs.Select(m => ((Material)m).shader).Distinct().Where(sh => sh);
                        if (shaders.Any())
                        {
                            if (!GO_Types_Shaders.TryGetValue(go, out var Types_Shaders))
                            { Types_Shaders = new(); GO_Types_Shaders[go] = Types_Shaders; }
                            if (!Types_Shaders.TryGetValue(analyzingTYPE, out var Shaders))
                            { Shaders = new(); Types_Shaders[analyzingTYPE] = Shaders; }
                            foreach (var shader in shaders)
                            {
                                Shaders.Add(shader);
                                if (!Shader_HashesToProps.ContainsKey(shader)) GenerateAnimatableShaderBindings(shader);
                            }
                        }
                    }
                }
            }

            private void GenerateAnimatableBindings(GameObject go)
            {
                var Types_HashesToProps = new Dictionary<System.Type, Dictionary<uint, string>>(); GO_Types_HashesToProps[go] = Types_HashesToProps;
                var Types_Props = new Dictionary<System.Type, HashSet<string>>(); GO_Types_Props[go] = Types_Props;
                var tParent = go.transform.parent; var hasRenderer = false;
                var CBs = AnimationUtility.GetAnimatableBindings(go, tParent ? tParent.gameObject : go).GroupBy(cb => cb.type);
                foreach (var cbGroup in CBs)
                {
                    var htp = new Dictionary<uint, string>();
                    var props = new HashSet<string>();
                    var cbT = cbGroup.Key;
                    foreach (var cb in cbGroup)
                    {
                        var propName = cb.propertyName;
                        if (typeof(Renderer).IsAssignableFrom(cbT) && propName.StartsWith("material.")) continue;
                        props.Add(propName);
                        if (cbT == typeof(SkinnedMeshRenderer) && propName.StartsWith("blendShape."))
                        { htp[(uint)Animator.StringToHash(propName[11..])] = propName; }
                        else htp[(uint)Animator.StringToHash(propName)] = propName;
                    }
                    if (typeof(Transform).IsAssignableFrom(cbT))
                    {
                        props.Add("localEulerAnglesRaw.x"); props.Add("localEulerAnglesRaw.y"); props.Add("localEulerAnglesRaw.z");
                        htp[2327998371U] = "localEulerAnglesRaw.x"; htp[4257570613U] = "localEulerAnglesRaw.y"; htp[1691094671U] = "localEulerAnglesRaw.z";
                    }
                    if (typeof(Renderer).IsAssignableFrom(cbT)) hasRenderer = true;
                    Types_HashesToProps[cbT] = htp;
                    Types_Props[cbT] = props;
                }
                if (hasRenderer)
                {
                    var rendererGroup = go.GetComponents<Renderer>().GroupBy(r => r.GetType());
                    if (!GO_Types_Shaders.TryGetValue(go, out var Types_Shaders))
                    { Types_Shaders = new(); GO_Types_Shaders[go] = Types_Shaders; }
                    foreach (var gr in rendererGroup)
                    {
                        var rendererShaders = gr.SelectMany(r => r.sharedMaterials.Select(m => m ? m.shader : null)).Distinct().Where(sh => sh);
                        if (!rendererShaders.Any()) continue;
                        if (!Types_Shaders.TryGetValue(gr.Key, out var Shaders))
                        { Shaders = new(); Types_Shaders[gr.Key] = Shaders; }
                        foreach (var shader in rendererShaders)
                        {
                            Shaders.Add(shader);
                            if (!Shader_HashesToProps.ContainsKey(shader)) GenerateAnimatableShaderBindings(shader);
                        }
                    }
                }
            }

            private void GenerateAnimatableShaderBindings(Shader shader)
            {
                Dictionary<uint, string> htp = new();
                HashSet<string> props = new();
                var shaderPropCount = shader.GetPropertyCount();
                for (int i = 0; i < shaderPropCount; i++)
                {
                    var propName = shader.GetPropertyName(i);
                    var propType = shader.GetPropertyType(i);
                    switch (propType)
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                            AddShaderProp(props, htp, propName + "_ST", true);
                            AddShaderProp(props, htp, propName + "_TexelSize", true);
                            AddShaderProp(props, htp, propName + "_HDR", true);
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                            AddShaderProp(props, htp, propName, true);
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Color:
                            AddShaderProp(props, htp, propName, true, true);
                            break;
                        default:
                            AddShaderProp(props, htp, propName, false);
                            break;
                    }
                }
                Shader_Props[shader] = props;
                Shader_HashesToProps[shader] = htp;
            }

            private void AddShaderProp(HashSet<string> props, Dictionary<uint, string> htp, string propName, bool subprop, bool rgba = false)
            {
                var hash = (uint)Animator.StringToHash(propName) & 268435455U;
                if (!subprop)
                { hash += 2147483648U; htp[hash] = propName; props.Add(propName); }
                else if (rgba)
                {
                    hash += 1073741824U;
                    var matPropName_ = propName + ".r"; htp[hash] = matPropName_; props.Add(matPropName_);
                    hash += 268435456U;
                    matPropName_ = propName + ".g"; htp[hash] = matPropName_; props.Add(matPropName_);
                    hash += 268435456U;
                    matPropName_ = propName + ".b"; htp[hash] = matPropName_; props.Add(matPropName_);
                    hash += 268435456U;
                    matPropName_ = propName + ".a"; htp[hash] = matPropName_; props.Add(matPropName_);
                }
                else
                {
                    var matPropName_ = propName + ".x"; htp[hash] = matPropName_; props.Add(matPropName_);
                    hash += 268435456U;
                    matPropName_ = propName + ".y"; htp[hash] = matPropName_; props.Add(matPropName_);
                    hash += 268435456U;
                    matPropName_ = propName + ".z"; htp[hash] = matPropName_; props.Add(matPropName_);
                    hash += 268435456U;
                    matPropName_ = propName + ".w"; htp[hash] = matPropName_; props.Add(matPropName_);
                }
            }

            private bool AddAnimationClips(IEnumerable<AnimationClip> acs, GameObject go)
            {
                var ret = false;
                foreach (var ac in acs) if (AddAnimationClip(ac, go)) ret = true;
                return ret;
            }

            private bool AddAnimationClip(AnimationClip ac, GameObject go)
            {
                if (!ac || !addedACs.Add(ac)) return false;
                if (!AnimsToOrigins.TryGetValue(ac, out var L)) { L = new(); AnimsToOrigins[ac] = L; }
                L.Add(go);
                if (!GO_Paths.ContainsKey(go)) AddPaths(go);
                return true;
            }

            private void AddPaths(GameObject go)
            {
                Dictionary<string, List<GameObject>> paths_to_hashes = new(); GO_Paths[go] = paths_to_hashes;
                Dictionary<uint, string> hashes_to_paths = new(); GO_HashesToPaths[go] = hashes_to_paths;
                paths_to_hashes[""] = new() { go }; ; hashes_to_paths[0] = "";
                buildingPaths = paths_to_hashes; buildingHTP = hashes_to_paths;
                foreach (Transform t in go.transform) AddPathsRecursive(t, "");
                buildingPaths = null; buildingHTP = null;
            }

            private void AddPathsRecursive(Transform t, string path)
            {
                path += t.name;
                var hash = (uint)Animator.StringToHash(path);
                if (!buildingHTP.ContainsKey(hash)) { buildingPaths[path] = new() { t.gameObject }; buildingHTP[hash] = path; }
                else buildingPaths[path].Add(t.gameObject);
                foreach (Transform t2 in t) AddPathsRecursive(t2, path + '/');
            }

            private void SetCurveFix(string newpropertyName = null)
            {
                var fixedPath = analyzingNEWPATH != analyzingPATH;
                var fixedProp = newpropertyName != null;
                if (addedACs.Add(analyzingAC)) Undo.RegisterCompleteObjectUndo(analyzingAC, "Fix Animations");
                if (analyzingCBObjRef)
                {
                    ObjectReferenceKeyframe[] objectCurve = AnimationUtility.GetObjectReferenceCurve(analyzingAC, analyzingECB);
                    AnimationUtility.SetObjectReferenceCurve(analyzingAC, analyzingECB, null);
                    if (fixedPath) analyzingECB.path = analyzingNEWPATH;
                    if (fixedProp) analyzingECB.propertyName = newpropertyName;
                    AnimationUtility.SetObjectReferenceCurve(analyzingAC, analyzingECB, objectCurve);
                }
                else
                {
                    AnimationCurve floatCurve = AnimationUtility.GetEditorCurve(analyzingAC, analyzingECB);
                    AnimationUtility.SetEditorCurve(analyzingAC, analyzingECB, null);
                    if (fixedPath) analyzingECB.path = analyzingNEWPATH;
                    if (fixedProp) analyzingECB.propertyName = newpropertyName;
                    AnimationUtility.SetEditorCurve(analyzingAC, analyzingECB, floatCurve);
                }

                if (!Fixes.TryGetValue(analyzingAC, out var fixedData)) { fixedData = new(); Fixes[analyzingAC] = fixedData; }
                if (fixedPath && !fixedData.paths.ContainsKey(analyzingPATH)) fixedData.paths[analyzingPATH] = analyzingNEWPATH;
                if (fixedProp)
                {
                    if (!fixedData.paths_types_props.TryGetValue(analyzingNEWPATH, out var types_props))
                    { types_props = new(); fixedData.paths_types_props[analyzingNEWPATH] = types_props; }
                    if (!types_props.TryGetValue(analyzingTYPE, out var props))
                    { props = new(); types_props[analyzingTYPE] = props; }
                    if (!props.ContainsKey(analyzingPROPERTYNAME)) props[analyzingPROPERTYNAME] = newpropertyName;
                }
            }

            private void CollectBrokenProp()
            {
                if (!Brokens.TryGetValue(analyzingAC, out var brokenData)) { brokenData = new(); Brokens[analyzingAC] = brokenData; }
                if (!brokenData.paths_types_props.TryGetValue(analyzingNEWPATH, out var types_props))
                { types_props = new(); brokenData.paths_types_props[analyzingNEWPATH] = types_props; }
                if (!types_props.TryGetValue(analyzingTYPE, out var props))
                { props = new(); types_props[analyzingTYPE] = props; }
                props.Add(analyzingPROPERTYNAME);
            }
        }
    }
}
#endif