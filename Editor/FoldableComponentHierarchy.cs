#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class ComponentHierarchy
    {
        public static Dictionary<int, TypeData> Types = new Dictionary<int, TypeData>();
        private static readonly GUIStyle foldoutStyle = new GUIStyle("Foldout");
        private static readonly GUIContent GO_enabled_content = new GUIContent("GameObject Enable State", EditorGUIUtility.IconContent("animationvisibilitytoggleon").image);
        private static readonly GUIContent GO_enabled_content_icon = new GUIContent(GO_enabled_content.image);
        private static readonly float GO_enabled_content_width = GUI.skin.toggle.CalcSize(new GUIContent("GameObject Enable State")).x + 20;

        public bool hierarchyDisplay = false;
        public bool hideTypeFoldout = false;
        private Vector2 scrollView = new Vector2();

        private readonly Transform originT;
        private readonly FoldableHierarchy origin;
        public Dictionary<int, FoldableType> toggles_by_type;

        private FoldableHierarchy[] go_enables = null;
        public FoldableHierarchy[] GO_enables { get { return go_enables; } }
        private bool go_enables_display = false;
        private bool go_enables_unfolded = false;
        public bool go_enables_hidden = false;

        public ComponentHierarchy(Transform origin_)
        {
            originT = origin_;
            origin = new FoldableHierarchy(origin_);
            toggles_by_type = new Dictionary<int, FoldableType>();
        }

        public ComponentToggle AddComponentToggle(Component c)
        {
            Type compT = c.GetType(); int Thc = compT.GetHashCode();
            if (!Types.ContainsKey(Thc))
            {
                Types.Add(compT.GetHashCode(), new TypeData(compT));
            }
            var typecontent = Types[Thc].content;
            if (!toggles_by_type.ContainsKey(Thc))
            {
                toggles_by_type.Add(Thc, new FoldableType(typecontent));
            }

            var hierarchy = GetGOHierarchy(c.transform);
            
            var newCompToggle = origin.Add(c, typecontent, hierarchy);
            toggles_by_type[Thc].toggleList.Add(newCompToggle);
            return newCompToggle;
        }

        public void GenerateGOEnables()
        {
            var l = new HashSet<FoldableHierarchy>{ origin };
            origin.AddChildrenToListRecursive(l);
            go_enables = l.ToArray();
            go_enables_display = true;
        }

        public void SortElements(bool byName = true, bool recursive = false)
        {
            if (byName) origin.SortByName(recursive);
            else origin.SortByHierarchy(recursive);
        }

        public void SortTypes()
        {
            var THC = typeof(Transform).GetHashCode();
            var sortedTypes = new List<Type>();
            var sortedMonoB = new List<Type>();
            var typeHCL = toggles_by_type.Keys;
            foreach (var i in typeHCL)
            {
                if (i == THC) { continue; }
                var T = ComponentHierarchy.Types[i].type;
                if (T.IsSubclassOf(typeof(MonoBehaviour))) { sortedMonoB.Add(T); }
                else { sortedTypes.Add(T); }
            }
            var finalsort = sortedTypes.OrderBy(t => t.Name).Select(t => t.GetHashCode()).ToList();
            finalsort.AddRange(sortedMonoB.OrderBy(t => t.Name).Select(t => t.GetHashCode()));

            var newTogglesByType = new Dictionary<int, FoldableType>();
            if (toggles_by_type.ContainsKey(THC)) newTogglesByType.Add(THC, toggles_by_type[THC]);
            foreach (var i in finalsort)
            {
                newTogglesByType.Add(i, toggles_by_type[i]);
            }
            toggles_by_type = newTogglesByType;
        }

        public HashSet<T> GetAllToggles<T>(int filter = 0) where T : Component  // -1:falses , 1:trues
        {
            var typeHash = typeof(T).GetHashCode();
            if (!toggles_by_type.ContainsKey(typeHash)) return null;
            return toggles_by_type[typeHash].GetAllToggles<T>(filter);
        }

        public HashSet<Component> GetAllToggles(int type, int filter = 0)  // -1:falses , 1:trues
        {
            if (!toggles_by_type.ContainsKey(type)) return null;
            return toggles_by_type[type].GetAllToggles<Component>(filter);
        }

        public void SetAllToggles(int type, bool on_off)
        {
            if (!toggles_by_type.ContainsKey(type)) return;
            toggles_by_type[type].SetAllToggles(on_off);
        }

        public void SetAllGOEnables(bool on_off)
        {
            if (go_enables_display)
            {
                foreach (var fh in go_enables) fh.GO_enable = on_off;
            }
        }

        public bool DisplayGUI()
        {
            scrollView = EditorGUILayout.BeginScrollView(scrollView, GUILayout.ExpandHeight(true));
            bool anyOn = false;
            if (hierarchyDisplay)
            {
                origin.DisplayFoldable(go_enables_display && !go_enables_hidden);
                DisplayHierarchyRecursive(origin, origin.unfolded, go_enables_display && !go_enables_hidden, ref anyOn);
            }
            else
            {
                DisplayAllByTypes(ref anyOn);
            }
            EditorGUILayout.EndScrollView();
            return anyOn;
        }

        private void DisplayAllByTypes(ref bool anyon)
        {
            if (go_enables_display)
            {
                if (hideTypeFoldout)
                {
                    foreach (var fh in go_enables)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(GO_enabled_content_icon, GUILayout.Width(16));
                        fh.GO_enable = EditorGUILayout.ToggleLeft(fh.content.text, fh.GO_enable, GUILayout.Height(16), GUILayout.Width(fh.content_width));
                        EditorGUILayout.EndHorizontal();
                        if (!anyon && fh.GO_enable) anyon = true;
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    go_enables_unfolded = GUILayout.Toggle(go_enables_unfolded, GO_enabled_content, foldoutStyle, GUILayout.Height(16), GUILayout.Width(GO_enabled_content_width));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("All", GUILayout.Height(16))) SetAllGOEnables(true);
                    if (GUILayout.Button("None", GUILayout.Height(16))) SetAllGOEnables(false);
                    if (GUILayout.Button($"{(go_enables_hidden ? "Show" : "Hide")}", GUILayout.Height(16)))
                    {
                        go_enables_hidden = !go_enables_hidden;
                        if (go_enables != null) CalculateHidden(go_enables);
                    }
                    EditorGUILayout.EndHorizontal();

                    if (go_enables_unfolded)
                    {
                        EditorGUI.BeginDisabledGroup(go_enables_hidden);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(14f);
                        EditorGUILayout.BeginVertical();
                        foreach (var fh in go_enables)
                        {
                            fh.GO_enable = EditorGUILayout.ToggleLeft(fh.content.text, fh.GO_enable, GUILayout.Height(16), GUILayout.Width(fh.content_width));
                            if (!anyon && fh.GO_enable) anyon = true;
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        EditorGUI.EndDisabledGroup();
                    }
                    else if (!anyon)
                    {
                        foreach (var fh in go_enables)
                        {
                            if (fh.GO_enable) { anyon = true; break; }
                        }
                    }
                }
            }

            foreach (var ft in toggles_by_type.Values)
            {
                if (hideTypeFoldout)
                {
                    foreach (var toggle in ft.toggleList)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(new GUIContent(toggle.content.image), GUILayout.Width(16));
                        toggle.state = EditorGUILayout.ToggleLeft(toggle.GOName, toggle.state, GUILayout.Height(16), GUILayout.Width(toggle.GOName_width));
                        EditorGUILayout.EndHorizontal();
                        if (!anyon && toggle.state) anyon = true;
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    ft.unfolded = GUILayout.Toggle(ft.unfolded, ft.content, foldoutStyle, GUILayout.Height(16), GUILayout.Width(ft.content_width));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("All", GUILayout.Height(16))) ft.SetAllToggles(true);
                    if (GUILayout.Button("None", GUILayout.Height(16))) ft.SetAllToggles(false);
                    if (GUILayout.Button($"{(ft.hidden ? "Show" : "Hide")}", GUILayout.Height(16)))
                    {
                        var L = new HashSet<FoldableHierarchy>();
                        SetTypeHidden(ft, !ft.hidden, L);
                        CalculateHidden(CompleteUp(L));
                    }
                    EditorGUILayout.EndHorizontal();

                    if (ft.unfolded)
                    {
                        EditorGUI.BeginDisabledGroup(ft.hidden);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(14f);
                        EditorGUILayout.BeginVertical();
                        foreach (var toggle in ft.toggleList)
                        {
                            toggle.state = EditorGUILayout.ToggleLeft(toggle.GOName, toggle.state, GUILayout.Height(16), GUILayout.Width(toggle.GOName_width));
                            if (!anyon && toggle.state) anyon = true;
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        EditorGUI.EndDisabledGroup();
                    }
                    else if (!anyon)
                    {
                        foreach (var toggle in ft.toggleList)
                        {
                            if (toggle.state) { anyon = true; break; }
                        }
                    }
                }
            }
        }

        private void DisplayHierarchyRecursive(FoldableHierarchy fh, bool isUnfolded, bool go_enables_disp, ref bool anyOn)
        {
            if (isUnfolded)
            {
                if (fh.children != null)
                {
                    foreach (var ch in fh.children.Values)
                    {
                        if (!ch.hidden) ch.DisplayFoldable(go_enables_disp);
                        DisplayHierarchyRecursive(ch, isUnfolded && ch.unfolded && !ch.hidden, go_enables_disp, ref anyOn);
                    }
                }
                if (go_enables_disp || fh.selfToggles != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(fh.foldoutDepth * 13 + 2);
                    EditorGUILayout.BeginVertical();

                    if (go_enables_disp)
                    {
                        fh.GO_enable = GUILayout.Toggle(fh.GO_enable, GO_enabled_content, GUILayout.Height(16), GUILayout.Width(GO_enabled_content_width));
                        if (!anyOn && fh.GO_enable) anyOn = true;
                    }
                    if (fh.selfToggles != null)
                    {
                        foreach (var toggle in fh.selfToggles)
                        {
                            if (!toggle.hidden) toggle.state = GUILayout.Toggle(toggle.state, toggle.content, GUILayout.Height(16), GUILayout.Width(toggle.content_width));
                            if (!anyOn && toggle.state) anyOn = true;
                        }
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
            }
            else if (!anyOn)
            {
                if (fh.GO_enable) { anyOn = true; return; }
                if (fh.selfToggles != null)
                {
                    foreach (var toggle in fh.selfToggles)
                    {
                        if (toggle.state) { anyOn = true; return; }
                    }
                }
                if (fh.children != null)
                {
                    foreach (var ch in fh.children.Values)
                    {
                        DisplayHierarchyRecursive(ch, false, go_enables_disp, ref anyOn);
                    }
                }
            }
        }

        public void HideShowAll(bool b)
        {
            if (go_enables != null)
            {
                go_enables_hidden = b;
                foreach (var ft in toggles_by_type.Values) SetTypeHidden(ft, b);
                CalculateHidden(go_enables);
            }
            else
            {
                var L = new HashSet<FoldableHierarchy>();
                foreach (var ft in toggles_by_type.Values) SetTypeHidden(ft, b, L);
                CalculateHidden(CompleteUp(L));
            }
        }

        private void SetTypeHidden(FoldableType ft, bool b, HashSet<FoldableHierarchy> L)
        {
            ft.hidden = b;
            ft.SetAllHidden(b, L);
        }

        private void SetTypeHidden(FoldableType ft, bool b)
        {
            ft.hidden = b;
            ft.SetAllHidden(b);
        }

        private HashSet<FoldableHierarchy> CompleteUp(HashSet<FoldableHierarchy> L)
        {
            var newL = new HashSet<FoldableHierarchy>(L);
            foreach (var fh in L)
            {
                CompleteUpRecursive(fh, newL);
            }
            return newL;
        }

        private void CompleteUpRecursive(FoldableHierarchy fh, HashSet<FoldableHierarchy> L)
        {
            var parent = fh.parent;
            if (parent == null || L.Contains(parent)) return;
            L.Add(parent); CompleteUpRecursive(parent, L);
        }

        private void CalculateHidden(IEnumerable<FoldableHierarchy> L)
        {
            var sortedL = L.OrderByDescending(x=>x.foldoutDepth).ToList();
            while (sortedL.Count > 0)
            {
                var fh = sortedL[0];
                var shouldHide = go_enables_display ? go_enables_hidden : true;
                if (shouldHide && fh.selfToggles != null)
                {
                    foreach (var toggle in fh.selfToggles)
                    {
                        if (!toggle.hidden)
                        {
                            shouldHide = false; break;
                        }
                    }
                }
                if (shouldHide && fh.children != null)
                {
                    foreach (var ch in fh.children.Values)
                    {
                        if (!ch.hidden)
                        {
                            shouldHide = false; break;
                        }
                    }
                }
                if (!shouldHide)
                {
                    fh.hidden = false;
                    ClearUpRecursive(fh, sortedL);
                }
                else fh.hidden = true;
                sortedL.RemoveAt(0);
            }
        }

        private void ClearUpRecursive(FoldableHierarchy fh, List<FoldableHierarchy> L)
        {
            var parent = fh.parent;
            if (parent != null)
            {
                var ind = L.IndexOf(parent);
                if (ind != -1)
                {
                    parent.hidden = false; L.RemoveAt(ind);
                    ClearUpRecursive(parent, L);
                }
            }
        }

        public void CollapseHierarchy()
        {
            origin.SetAllFoldouts(false);
        }

        public void UseUnfoldedAsMarker()
        {
            origin.UseUnfoldedAsMarkerRecursive();
        }

        private List<Transform> GetGOHierarchy(Transform t) //from finish to start
        {
            List<Transform> output = new List<Transform>();
            while (t && t != originT)
            {
                output.Add(t);
                t = t.parent;
            }
            if (output.Count > 0) return output;
            return null;
        }

        public class FoldableHierarchy
        {
            public bool unfolded = false;
            public Dictionary<int, FoldableHierarchy> children = null;
            public List<ComponentToggle> selfToggles = null;
            public FoldableHierarchy parent = null;
            public int foldoutDepth = 0;
            public bool hidden = false;

            public Transform t;
            public GUIContent content;
            public float content_width;

            public bool GO_enable = false;

            public FoldableHierarchy(Transform _t)
            {
                t = _t;
                content = new GUIContent(_t.name, PrefabUtility.GetIconForGameObject(_t.gameObject));
                content_width = GUI.skin.toggle.CalcSize(new GUIContent(content.text)).x + 20;
            }

            public FoldableHierarchy(Transform _t, FoldableHierarchy _parent)
            {
                t = _t;
                content = new GUIContent(_t.name, PrefabUtility.GetIconForGameObject(_t.gameObject));
                content_width = GUI.skin.toggle.CalcSize(new GUIContent(content.text)).x + 20;
                parent = _parent;
                foldoutDepth = _parent.foldoutDepth + 1;
            }

            public ComponentToggle Add(Component c, GUIContent _content, List<Transform> hierarchy)
            {
                if (hierarchy == null)
                {
                    if (selfToggles == null) { selfToggles = new List<ComponentToggle>(); }
                    var newToggle = new ComponentToggle(c, _content, this);
                    selfToggles.Add(newToggle); return newToggle;
                }
                else
                {
                    FoldableHierarchy child;
                    var GOhash = hierarchy[hierarchy.Count - 1].GetHashCode();
                    if (children == null) { children = new Dictionary<int, FoldableHierarchy>(); }
                    if (!children.ContainsKey(GOhash))
                    {
                        child = new FoldableHierarchy(hierarchy[hierarchy.Count - 1], this);
                        children.Add(GOhash, child);
                    }
                    else
                    {
                        child = children[GOhash];
                    }
                    hierarchy.RemoveAt(hierarchy.Count - 1); if (hierarchy.Count == 0) hierarchy = null;
                    return child.Add(c, _content, hierarchy);
                }
            }

            public void AddChildrenToListRecursive(HashSet<FoldableHierarchy> l)
            {
                if (children != null)
                {
                    foreach (var ch in children.Values)
                    {
                        l.Add(ch); ch.AddChildrenToListRecursive(l);
                    }
                }
            }

            public void SetAllTogglesRecursive(bool off_on, bool includeGoEnable)
            {
                SetAllToggles(off_on, includeGoEnable);
                if (children != null)
                {
                    if (includeGoEnable)
                    {
                        foreach (var child in children.Values)
                        {
                            child.SetAllTogglesRecursive(off_on, includeGoEnable);
                        }
                    }
                    else
                    {
                        foreach (var child in children.Values)
                        {
                            if (!child.hidden) child.SetAllTogglesRecursive(off_on, includeGoEnable);
                        }
                    }
                }
            }

            private void SetAllToggles(bool off_on, bool includeGoEnable)
            {
                if (includeGoEnable) GO_enable = off_on;
                if (selfToggles != null)
                {
                    foreach (var toggle in selfToggles)
                    {
                        if (!toggle.hidden) toggle.state = off_on;
                    }
                }
            }

            public void SetAllFoldouts(bool off_on)
            {
                unfolded = off_on;
                if (children != null)
                {
                    foreach (var child in children.Values)
                    {
                        child.SetAllFoldouts(off_on);
                    }
                }
            }

            public void ExpandEmptyFoldouts()
            {
                if (children == null) return;
                FoldableHierarchy nextch = null;
                foreach (var ch in children.Values)
                {
                    if (!ch.hidden)
                    {
                        if (nextch == null)
                        {
                            nextch = ch;
                        }
                        else return;
                    }
                }

                if (selfToggles != null)
                {
                    foreach (var toggle in selfToggles)
                    {
                        if (!toggle.hidden) return;
                    }
                }
                nextch.unfolded = true; nextch.ExpandEmptyFoldouts();
            }

            public Component[] GetAllToggles(int filterByState = -1)
            {
                var alltoggles = new List<Component>();
                GetAllToggles(alltoggles, filterByState);
                if (alltoggles.Count > 0) return alltoggles.ToArray();
                return null;
            }

            private void GetAllToggles(List<Component> alltoggles, int filterByState)
            {
                if (selfToggles != null)
                {
                    foreach (var toggle in selfToggles)
                    {
                        if (filterByState == -1 ||
                            (filterByState == 1 && toggle.state) ||
                            !toggle.state) alltoggles.Add(toggle.component);
                    }
                }
                if (children != null)
                {
                    foreach (var child in children.Values)
                    {
                        child.GetAllToggles(alltoggles, filterByState);
                    }
                }
            }
            public bool UseUnfoldedAsMarkerRecursive()
            {
                bool anyon = false;
                if (children != null)
                {
                    foreach (var ch in children.Values)
                    {
                        anyon = ch.UseUnfoldedAsMarkerRecursive() || anyon;
                    }
                }
                if (!anyon)
                {
                    anyon = GO_enable;
                    if (!anyon && selfToggles != null)
                    {
                        foreach (var t in selfToggles)
                        {
                            if (t.state) { anyon = true; break; }
                        }
                    }
                }
                unfolded = anyon;
                return anyon;
            }

            public void DisplayFoldable(bool go_enables_disp)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(foldoutDepth * 13);
                EditorGUI.BeginChangeCheck();
                unfolded = GUILayout.Toggle(unfolded, content, foldoutStyle, GUILayout.Height(16), GUILayout.Width(content_width));
                if (EditorGUI.EndChangeCheck())
                {
                    if (Event.current.control)
                    {
                        if (unfolded)
                        {
                            if (!go_enables_disp) ExpandEmptyFoldouts();
                        }
                        else SetAllFoldouts(false);
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("All", GUILayout.Height(16)))
                {
                    if (Event.current.control) SetAllTogglesRecursive(true, go_enables_disp);
                    else SetAllToggles(true, go_enables_disp);
                }
                if (GUILayout.Button("None", GUILayout.Height(16)))
                {
                    if (Event.current.control) SetAllTogglesRecursive(false, go_enables_disp);
                    else SetAllToggles(false, go_enables_disp);
                }

                EditorGUILayout.EndHorizontal();
            }

            public void SortByName(bool recursive = false)
            {
                if (selfToggles != null && selfToggles.Count > 1) selfToggles.Sort((x, y) => x.content.text.CompareTo(y.content.text));
                if (children != null)
                {
                    if (children.Count > 1) children = children.OrderBy(x => x.Value.content.text).ToDictionary(x=>x.Key,x=>x.Value);
                    if (recursive)
                    {
                        foreach (var ch in children.Values) ch.SortByName(recursive);
                    }
                }
            }

            public void SortByHierarchy(bool recursive = false)
            {
                if (selfToggles != null && selfToggles.Count > 1) selfToggles.Sort((x, y) => x.content.text.CompareTo(y.content.text));
                if (children != null)
                {
                    if (children.Count > 1)
                    {
                        var newD = new Dictionary<int, FoldableHierarchy>();
                        foreach (Transform tr in t)
                        {
                            var tHC = tr.GetHashCode();
                            if (children.ContainsKey(tHC)) newD.Add(tHC, children[tHC]);
                        }
                        children = newD;
                    }
                    if (recursive)
                    {
                        foreach (var ch in children.Values) ch.SortByHierarchy(recursive);
                    }
                }
            }
        }

        public class FoldableType
        {
            public bool unfolded = false;
            public bool hidden = false;

            public List<ComponentToggle> toggleList;
            public GUIContent content;
            public float content_width;

            public FoldableType(GUIContent _content)
            {
                toggleList = new List<ComponentToggle>();
                content = _content;
                content_width = GUI.skin.toggle.CalcSize(new GUIContent(content.text)).x + 20;
            }

            public HashSet<T> GetAllToggles<T>(int filter) where T : Component
            {
                var selectedToggles = new HashSet<T>();
                switch (filter)
                {
                    case 1:
                        foreach (var t in toggleList)
                        {
                            if (t.state) selectedToggles.Add(t.component as T);
                        }
                        break;
                    case -1:
                        foreach (var t in toggleList)
                        {
                            if (!t.state) selectedToggles.Add(t.component as T);
                        }
                        break;
                    default:
                        foreach (var t in toggleList) selectedToggles.Add(t.component as T);
                        break;
                }
                if (selectedToggles.Count > 0)
                {
                    return selectedToggles;
                }
                return null;
            }

            public void SetAllToggles(bool on_off)
            {
                foreach (var t in toggleList) t.state = on_off;
            }

            public void SetAllHidden(bool b, HashSet<FoldableHierarchy> L)
            {
                foreach (var t in toggleList)
                {
                    t.hidden = b; var p = t.parent;
                    if (!L.Contains(p)) L.Add(p);
                }
            }

            public void SetAllHidden(bool b)
            {
                foreach (var t in toggleList) t.hidden = b;
            }
        }

        public class ComponentToggle
        {
            public bool state = false;
            public bool hidden = false;

            public Component component;
            public string GOName;
            public GUIContent content;
            public float content_width;
            public float GOName_width;
            public FoldableHierarchy parent;

            public ComponentToggle(Component c, GUIContent _content, FoldableHierarchy _parent)
            {
                component = c;
                GOName = c.name;
                content = _content;
                content_width = GUI.skin.toggle.CalcSize(new GUIContent(content.text)).x + 20;
                GOName_width = GUI.skin.toggle.CalcSize(new GUIContent(GOName)).x + 20;
                parent = _parent;
            }
        }
    }

    public class TypeData : IComparable<TypeData>
    {
        public readonly Type type;
        public readonly GUIContent content;

        public TypeData(Type T)
        {
            type = T;
            content = new GUIContent(ObjectNames.NicifyVariableName(T.Name), EditorGUIUtility.ObjectContent(GameObject.FindObjectOfType(T), T).image);
        }

        public int CompareTo(TypeData other) => string.Compare(content.text, other.content.text);  //IComparable
    }
}
#endif