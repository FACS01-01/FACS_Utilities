#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Type = System.Type;

namespace FACS01.Utilities
{
    public static class ComponentDependencies
    {
        private const string RichToolName = Logger.ToolTag + "[Component Dependencies]" + Logger.EndTag;

        private static readonly Dictionary<string, Type> somewhatNativeTypes = new();
        private static readonly Dictionary<Type, ORFH> componentsWithOR = new(); //componentT: {paths to assets}
        private static readonly Dictionary<Type, ORFH> componentsWithUOR = new();
        private static readonly Dictionary<Type, List<Type>> assetsUsedIn = new(); //assetT: list(componentTs)
        private static GameObject tempGO;
        private static SerializedObject tempSO;
        private static SerializedProperty tempSP;

        private static bool debug = false;
        
        public static void ReduceToMainTypes(List<Type> types)
        {
            if (types.Count < 2) return;
            var types2 = types.Distinct().Where(T => T != null).ToArray();
            types.Clear();
            foreach (var t in types2) if (!types2.Any(tt => t.IsSubclassOf(tt))) types.Add(t);
        }

        public static List<Type> ComponentsUsingAsset(Type assetT)
        {
            var res = new List<Type>();
            ComponentsUsingAsset(assetT, res);
            return res;
        }

        public static void ComponentsUsingAsset(Type assetT, List<Type> addTo)
        {
            foreach (var aT in assetsUsedIn.Keys)
            {
                if (aT.IsSubclassOf(assetT) || aT == assetT)
                {
                    foreach (var t in assetsUsedIn[aT])
                    { if (!addTo.Contains(t)) addTo.Add(t); }
                }
            }
        }

        public static int DoesComponentUseAsset(Type componentT, Type assetT)
        {
            if (componentsWithUOR.ContainsKey(componentT)) return 2;
            if (componentsWithOR.TryGetValue(componentT, out var orfh))
            {
                foreach (var t in orfh.availableTypes.Keys)
                { if (t.IsSubclassOf(assetT) || t == assetT) return 1; }
            }
            return 0;
        }

        public static List<Object> GetAllAssetsOfType(Component c, Type assetT)
        {
            var addTo = new List<Object>();
            GetAllAssetsOfType(c, assetT, addTo);
            return addTo;
        }

        public static void GetAllAssetsOfType(Component c, Type assetT, List<Object> addTo)
        {
            if (!c) return;
            if (!componentsWithOR.TryGetValue(c.GetType(), out var orfh) && !componentsWithUOR.TryGetValue(c.GetType(), out orfh)) return;
            orfh.GetAllAssetsOfType(c, assetT, addTo);
        }

        public static bool TryGetAssetsAndSources(List<GameObject> GOs, Type[] AssetTypes, out List<GameObject> roots, out List<List<Object>> sources)
        {
            roots = new(); sources = new();

            var cleanGOs = GOs.Where(go => go).Distinct().ToList();
            var cleanGOsCount = cleanGOs.Count;
            if (cleanGOsCount == 0)
            {
                roots = null; sources = null;
                return false;
            }
            for (int i = 0; i < cleanGOsCount; i++)
            {
                for (int j = 0; j < cleanGOsCount; j++)
                {
                    if (i == j) continue;
                    if (cleanGOs[i].transform.IsChildOf(cleanGOs[j].transform))
                    {
                        cleanGOs.RemoveAt(i);
                        cleanGOsCount--;
                        i--;
                        break;
                    }
                }
            }

            if (AssetTypes.Length == 0)
            {
                roots = null; sources = null;
                return false;
            }
            var AssetTypes2 = new List<Type>(AssetTypes);
            ReduceToMainTypes(AssetTypes2);
            var ComponentTsUsingAssets = new List<Type>();
            foreach (var T in AssetTypes2) ComponentsUsingAsset(T, ComponentTsUsingAssets);
            if (ComponentTsUsingAssets.Count == 0)
            {
                roots = null; sources = null;
                return false;
            }
            ReduceToMainTypes(ComponentTsUsingAssets);

            Dictionary<GameObject, List<Component>> groupedRoots = new();
            foreach (var go in cleanGOs)
            {
                foreach (var T in ComponentTsUsingAssets)
                {
                    var Cs = go.GetComponentsInChildren(T, true).Where(c=>c).ToList();
                    if (Cs.Count != 0)
                    {
                        foreach (var go_Cs in Cs.GroupBy(c => c.gameObject))
                        {
                            if (groupedRoots.TryGetValue(go_Cs.Key, out var comps)) comps.AddRange(go_Cs);
                            else groupedRoots[go_Cs.Key] = go_Cs.ToList();
                        }
                    }
                }
            }

            foreach (var go_components in groupedRoots)
            {
                var foundAssets = new List<Object>();
                foreach (var assetT in AssetTypes2) foreach (var c in go_components.Value) GetAllAssetsOfType(c, assetT, foundAssets);
                if (foundAssets.Count != 0) { roots.Add(go_components.Key); sources.Add(foundAssets); }
            }

            if (roots.Count != 0) return true;
            roots = null; sources = null;
            return false;
        }

        internal static Component TryAddComponent(GameObject GO, Type t)
        {
            Component c = null;
            try { c = ObjectFactory.AddComponent(GO, t); }
            catch (System.Exception e)
            {
                var msg = e.Message;
                if (msg.StartsWith("Adding component failed. Add required component of type"))
                {
                    var cas = t.GetCustomAttributes(typeof(RequireComponent), true);
                    if (cas.Length != 0)
                    {
                        foreach (var ca in cas)
                        {
                            if (AddRequireComponents(GO, (RequireComponent)ca))
                            { try { c = ObjectFactory.AddComponent(GO, t); break; } catch { } }
                        }
                    }
                }
                else if (!msg.StartsWith("'type' parameter is abstract and can't be used in the ObjectFactory") &&
                    !msg.EndsWith("because it is an editor script. To attach a script it needs to be outside the 'Editor' folder."))
                { Logger.LogWarning($"{RichToolName} {t.FullName}: {msg}"); }
            }
            return c;
        }

        [InitializeOnLoadMethod]
        private static void GenerateORFH()
        {
            somewhatNativeTypes.Clear(); assetsUsedIn.Clear();
            componentsWithOR.Clear(); componentsWithUOR.Clear();
            somewhatNativeTypes.Add("Object", typeof(Object));
            ORFH.ClearBuilder();
            var _somewhatNativeTypes = TypeCache.GetTypesDerivedFrom(typeof(Object))
                .Where(t => t.DeclaringType == null && !IsScript(t) && !IsObsolete(t))
                .OrderBy(t=>{
                    var tName = t.FullName;
                    if (tName.StartsWith("UnityEngine.")) return 0;
                    if (tName.StartsWith("UnityEditor.")) return 1;
                    if (tName.StartsWith("UnityEngineInternal.")) return 2;
                    if (tName.StartsWith("UnityEditorInternal.")) return 3;
                    return 4;});
            foreach (var t in _somewhatNativeTypes)
            {
                var tName = t.Name;
                if (!somewhatNativeTypes.ContainsKey(tName)) somewhatNativeTypes[tName] = t;
            }
            _somewhatNativeTypes = null;

            tempGO = new() { hideFlags = (HideFlags)23 }; //HideInHierarchy,HideInInspector,DontSaveInEditor,DontSaveInBuild
            tempGO.SetActive(false); tempGO.AddComponent<RectTransform>();
            tempSO = new(tempGO); tempSP = tempSO.FindProperty("m_Component");

            var allComponents = TypeCache.GetTypesDerivedFrom(typeof(Component))
                .Where(t => t.DeclaringType == null && !t.IsAbstract && !IsObsolete(t))
                .OrderByDescending(t => InheritanceDepth(t, typeof(Component)))
                .ThenBy(t=>t.FullName);

            var allValidMonoBehaviours = allComponents.Where(t=>t.IsSubclassOf(typeof(MonoBehaviour))).Where(t=> {
                if (!t.IsSubclassOf(typeof(MonoBehaviour))) return false;
                return ORFH.GetFields(t).Count > 0;
            });
            var allNonMonoBehaviours = allComponents.Where(t => !t.IsSubclassOf(typeof(MonoBehaviour)));

            ProcessComponents(allNonMonoBehaviours);
            ProcessComponents(allValidMonoBehaviours);

            tempSP.Dispose(); tempSO.Dispose(); Object.DestroyImmediate(tempGO);
            if (debug) Debug.Log($"[Component Dependencies] Finished scanning component types!\n" +
                $"Components With Object References: {componentsWithOR.Count}\n" +
                $"Components With Unknown Object References: {componentsWithUOR.Count}\n");
            debug = false;
        }

        private static bool IsScript(Type t)
        {
            return t.IsSubclassOf(typeof(MonoBehaviour)) || t.IsSubclassOf(typeof(ScriptableObject));
        }

        private static bool IsObsolete(Type t)
        {
            return t.IsDefined(typeof(System.ObsoleteAttribute), true);
        }

        public static int InheritanceDepth(Type t, Type _base)
        {
            var res = 1;
            while (t.BaseType != null && t.BaseType != _base) { res++; t = t.BaseType; }
            return res;
        }

        private static void ProcessComponents(IEnumerable<Type> types)
        {
            foreach (var t in types)
            {
                if (t == typeof(Transform) || t.IsSubclassOf(typeof(Transform))) continue;
                var c = TryAddComponent(tempGO, t); if (c == null) continue;
                var orfh = ORFH.Generate(c);
                tempSO.UpdateIfRequiredOrScript();
                for (int i = tempSP.arraySize - 1; i > 0; i--)
                { Object.DestroyImmediate(tempSP.GetArrayElementAtIndex(i).FindPropertyRelative("component").objectReferenceValue); }

                if (debug && orfh != null)//
                {
                    Debug.Log($"**{orfh.componentType.FullName}**\n  " + string.Join("\n  ",
                        orfh.availableTypes.Select(kvp => $"{kvp.Key.FullName}::{{\n     {string.Join("\n     ", kvp.Value.Select(a => { var str = ""; foreach (var p in a) { str += $"{p.relativePath} ({p.rank})/ "; } return str[..^2]; }))}}}"))
                        + "\n");
                }
            }
        }

        private static bool AddRequireComponents(GameObject GO, RequireComponent rc)
        {
            var added = false;
            if (rc.m_Type0 != null && FindTypeAndTryAdd(GO, rc.m_Type0)) added = true;
            if (rc.m_Type1 != null && FindTypeAndTryAdd(GO, rc.m_Type1)) added = true;
            if (rc.m_Type2 != null && FindTypeAndTryAdd(GO, rc.m_Type2)) added = true;
            return added;
        }

        private static Component FindTypeAndTryAdd(GameObject GO, Type baseType)
        {
            Component c = null;
            if (baseType == null || typeof(Transform).IsAssignableFrom(baseType)) return c;
            try { c = ObjectFactory.AddComponent(GO, baseType); return c; }
            catch (System.Exception e)
            {
                if (e.Message.StartsWith("'type' parameter is abstract and can't be used in the ObjectFactory"))
                {
                    var types = TypeCache.GetTypesDerivedFrom(baseType);
                    foreach (var type in types) { c = FindTypeAndTryAdd(GO, type); if (c != null) return c; }
                }
            }
            return c;
        }

        private class ORFH //object reference field hierarchy
        {
            internal readonly struct ORFHPath
            {
                internal enum Rank
                {
                    UnknownOR,
                    ObjRef,
                    Collection,
                    CollectionUOR,
                    CollectionOR,
                    Container
                }
                internal readonly Rank rank;
                internal readonly string relativePath;

                internal ORFHPath(Rank r, string relPath)
                {
                    rank = r; relativePath = relPath;
                }
            }

            private static readonly List<ORFHPath> buildingPath = new();
            private static readonly Dictionary<Type, List<ORFHPath[]>> buildingKnowns = new();
            private static readonly List<ORFHPath[]> buildingUnknowns = new();

            internal Type componentType;
            internal Dictionary<Type, List<ORFHPath[]>> availableTypes = null;
            private List<ORFHPath[]> unknownTypes = null;
            private Type unknownTypeLookup = null;
            private Type unknownTypeFound = null;

            private ORFH(Type t)
            {
                componentType = t;
            }

            internal void GetAllAssetsOfType(Component c, Type assetT, List<Object> addTo)
            {
                using (var so = new SerializedObject(c))
                {
                    if (availableTypes != null)
                    {
                        foreach (var path in availableTypes
                            .Where(kvp => kvp.Key.IsSubclassOf(assetT) || kvp.Key == assetT).SelectMany(kvp => kvp.Value))
                        { ExplorePath(so.FindProperty(path[0].relativePath), path, addTo); }
                    }
                    if (unknownTypes != null)
                    {
                        unknownTypeLookup = assetT;
                        for (int i = unknownTypes.Count - 1; i >= 0; i--)
                        {
                            var unknownPath = unknownTypes[i];
                            ExplorePath(so.FindProperty(unknownPath[0].relativePath), unknownPath, addTo);
                            if (unknownTypeFound != null)
                            {
                                var cType = c.GetType();
                                if (availableTypes == null) availableTypes = new();
                                if (!availableTypes.TryGetValue(unknownTypeFound, out var L))
                                {
                                    L = new(); availableTypes[unknownTypeFound] = L;
                                    if (!assetsUsedIn.TryGetValue(unknownTypeFound, out var containers))
                                    { containers = new(); assetsUsedIn[unknownTypeFound] = containers; }
                                    containers.Add(cType);
                                }
                                var lastPath = unknownPath[^1]; var lastIsOR = lastPath.rank == ORFHPath.Rank.UnknownOR;
                                unknownPath[^1] =
                                    new ORFHPath(lastIsOR ? ORFHPath.Rank.ObjRef : ORFHPath.Rank.CollectionOR, lastPath.relativePath);
                                L.Add(unknownPath); unknownTypes.RemoveAt(i); unknownTypeFound = null;
                                if (!componentsWithOR.ContainsKey(cType)) componentsWithOR[cType] = this;
                                if (unknownTypes.Count == 0)
                                { unknownTypes = null; componentsWithUOR.Remove(cType); }
                            }
                        }
                        unknownTypeLookup = null;
                    }
                }
            }

            private void ExplorePath(SerializedProperty sp, ORFHPath[] path, List<Object> addTo, int index = 0)
            {
                var currentSubPath = path[index];
                switch (currentSubPath.rank)
                {
                    case ORFHPath.Rank.ObjRef:
                        TryCollectObjectReference(sp, addTo, out _);
                        break;
                    case ORFHPath.Rank.CollectionOR:
                        CollectCollection(sp, addTo);
                        break;
                    case ORFHPath.Rank.Container:
                        index++;
                        sp = sp.FindPropertyRelative(path[index].relativePath);
                        ExplorePath(sp, path, addTo, index);
                        break;
                    case ORFHPath.Rank.Collection:
                        index++;
                        for (int i = 0; i < sp.arraySize; i++)
                        {
                            var sp_i = sp.GetArrayElementAtIndex(i);
                            sp_i = sp_i.FindPropertyRelative(path[index].relativePath);
                            ExplorePath(sp_i, path, addTo, index);
                        }
                        break;
                    case ORFHPath.Rank.UnknownOR:
                        if (TryCollectObjectReference(sp, addTo, out var OR, true)
                            && unknownTypeFound == null) GetUnknownType(sp.type, OR);
                        break;
                    case ORFHPath.Rank.CollectionUOR:
                        CollectUnknownCollection(sp, addTo);
                        break;
                }
            }

            private void CollectCollection(SerializedProperty sp, List<Object> addTo)
            {
                for (int i = 0; i < sp.arraySize; i++)
                {
                    var sp_i = sp.GetArrayElementAtIndex(i);
                    TryCollectObjectReference(sp_i, addTo, out _);
                }
            }

            private void CollectUnknownCollection(SerializedProperty sp, List<Object> addTo)
            {
                for (int i = 0; i < sp.arraySize; i++)
                {
                    var sp_i = sp.GetArrayElementAtIndex(i);
                    if (TryCollectObjectReference(sp_i, addTo, out var OR, true)
                        && unknownTypeFound == null) GetUnknownType(sp_i.type, OR);
                }
            }

            private bool TryCollectObjectReference(SerializedProperty sp, List<Object> addTo, out Object OR, bool isUnknown = false)
            {
                OR = sp.objectReferenceValue;
                if (OR)
                {
                    if (!isUnknown || OR.GetType().IsSubclassOf(unknownTypeLookup) || OR.GetType() == unknownTypeLookup)
                    { if (!addTo.Contains(OR)) addTo.Add(OR); }
                    return true;
                }
                return false;
            }

            private void GetUnknownType(string typeName, Object OR)
            {
                typeName = GetCleanTypeName(typeName);
                var orType = OR.GetType();
                while(typeName != orType.Name)
                {
                    orType = orType.BaseType;
                    if (orType == null || orType == typeof(Object)) return;
                }
                unknownTypeFound = orType;
            }

            internal static void ClearBuilder()
            {
                buildingPath.Clear(); buildingKnowns.Clear(); buildingUnknowns.Clear();
            }

            internal static ORFH Generate(Component c)
            {
                ORFH orfh = null; var cType = c.GetType();
                using (var SO = new SerializedObject(c))
                {
                    var startProp = cType.IsSubclassOf(typeof(MonoBehaviour)) || cType == typeof(MonoBehaviour) ?
                        "m_EditorClassIdentifier" : "m_GameObject";
                    var sp = SO.FindProperty(startProp);
                    var fields = GetFields(cType);
                    while (sp.Next(false))
                    {
                        var sp2 = sp.Copy();
                        IsValidType(sp2, fields);
                    }
                }
                if (buildingKnowns.Keys.Count > 0 || buildingUnknowns.Count > 0)
                {
                    orfh = new(cType);
                    if (buildingKnowns.Keys.Count > 0)
                    {
                        componentsWithOR.Add(cType, orfh);
                        orfh.availableTypes = new();
                        foreach (var kvp in buildingKnowns) orfh.availableTypes[kvp.Key] = new(kvp.Value);
                    }
                    if (buildingUnknowns.Count > 0)
                    {
                        orfh.unknownTypes = new(buildingUnknowns);
                        componentsWithUOR.Add(cType, orfh);
                    }
                    foreach (var assetT in orfh.availableTypes.Keys)
                    {
                        if (!assetsUsedIn.TryGetValue(assetT, out var containers))
                        { containers = new(); assetsUsedIn[assetT] = containers; }
                        containers.Add(cType);
                    }
                }
                ClearBuilder();
                return orfh;
            }

            internal static List<FieldInfo> GetFields(Type t)
            {
                if (t == null) return null;
                var fields = new List<FieldInfo>();
                foreach (var fi in t.GetFields((BindingFlags)54)) //DeclaredOnly,Instance,Public,NonPublic
                { if (IsSerializable(fi)) fields.Add(fi); }
                t = t.BaseType;
                while (t != null)
                {
                    foreach (var fi in t.GetFields((BindingFlags)54))
                    { if (IsSerializable(fi)) fields.Add(fi); }
                    t = t.BaseType;
                }
                return fields;
            }

            private static string GetCleanTypeName(string typename)
            {
                typename = typename.Replace("$", "");
                if (typename.StartsWith("PPtr<")) typename = typename[5..^1];
                return typename;
            }

            private static bool IsSerializable(FieldInfo fi)
            {
                if (fi.GetCustomAttribute(typeof(System.NonSerializedAttribute)) != null) return false;
                if (!fi.IsPublic && fi.GetCustomAttribute(typeof(SerializeField)) == null) return false;
                if (fi.IsInitOnly) return false;
                var fiT = fi.FieldType;
                if (fiT.IsPrimitive || fiT.IsEnum) return false; //false if looking for ObjectReferences
                return true;
            }

            private static void TryGenerateOR(SerializedProperty sp, Type assetT, bool isCollection = false)
            {
                var subpath = sp.name; ORFHPath.Rank r;
                if (isCollection) sp = sp.GetArrayElementAtIndex(0);
                if (assetT == null)
                {
                    if (!somewhatNativeTypes.TryGetValue(GetCleanTypeName(sp.type), out assetT))
                    {
                        r = isCollection ? ORFHPath.Rank.CollectionUOR : ORFHPath.Rank.UnknownOR;
                        buildingPath.Add(new ORFHPath(r, subpath));
                        buildingUnknowns.Add(buildingPath.ToArray());
                        goto clear;
                    }
                }
                r = isCollection ? ORFHPath.Rank.CollectionOR : ORFHPath.Rank.ObjRef;
                buildingPath.Add(new ORFHPath(r, subpath));
                if (!buildingKnowns.TryGetValue(assetT, out var L))
                { L = new(); buildingKnowns[assetT] = L; }
                L.Add(buildingPath.ToArray());
            clear:
                buildingPath.RemoveAt(buildingPath.Count-1);
            }

            private static void IsValidType(SerializedProperty sp, List<FieldInfo> fis)
            {
                if (sp.propertyType != SerializedPropertyType.ObjectReference &&
                        sp.propertyType != SerializedPropertyType.Generic) return;
                var fi = fis?.FirstOrDefault(fi => fi.Name == sp.name); //Instance,Public,NonPublic
                if (sp.propertyType == SerializedPropertyType.ObjectReference)
                { TryGenerateOR(sp, fi?.FieldType); return; }
                if (sp.propertyType == SerializedPropertyType.String) return;
                if (sp.isArray)
                { IsValidCollectionType(sp, GetCollectionType(fi?.FieldType)); return; }
                buildingPath.Add(new ORFHPath(ORFHPath.Rank.Container, sp.name));
                ObjectReferenceFields(sp, fi?.FieldType);
                buildingPath.RemoveAt(buildingPath.Count - 1);
            }

            private static Type GetCollectionType(Type t)
            {
                if (t == null) return null;
                if (t.IsArray) return t.GetElementType();
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return t.GetGenericArguments()[0];
                return null;
            }

            private static void IsValidCollectionType(SerializedProperty sp, Type collT)
            {
                if (sp.arraySize == 0) sp.InsertArrayElementAtIndex(0);
                var sp_i = sp.GetArrayElementAtIndex(0);
                if (sp_i.propertyType != SerializedPropertyType.ObjectReference &&
                    sp_i.propertyType != SerializedPropertyType.Generic) return;
                if (sp_i.propertyType == SerializedPropertyType.ObjectReference)
                { TryGenerateOR(sp, collT, true); return; }
                if (sp_i.propertyType == SerializedPropertyType.String) return;
                buildingPath.Add(new ORFHPath(ORFHPath.Rank.Collection, sp.name));
                ObjectReferenceFields(sp_i, collT);
                buildingPath.RemoveAt(buildingPath.Count-1);
            }

            private static void ObjectReferenceFields(SerializedProperty sp, Type t)
            {
                var currentDepth = sp.depth;
                var sp2 = sp.Copy();
                if (!sp2.Next(true) || sp2.depth <= currentDepth) return;
                var fields = GetFields(t);
                do { IsValidType(sp2, fields); }
                while (sp2.Next(false) && sp2.depth > currentDepth);
            }
        }
    }
}
#endif