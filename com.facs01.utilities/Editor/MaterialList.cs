#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class MaterialList : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;

        private static MaterialItem[] materials;
        private static ShaderGroup[] shaderGroups;
        private static int lastClickedMaterialIndex = -1;
        private static int selectedMaterialCount = 0;
        private static Vector2 scrollPosition;
        private static bool selecting = false;

        private static GameObject source; 
        private static GUIStyle selectedObjectStyle;
        private static GUIStyle defaultObjectStyle;
        private bool shouldRepaint = false;
        private static Texture2D selectionBG;

        [MenuItem("FACS Utils/Shader+Material/Material List", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(MaterialList), false, "Material List", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }
        
        private void OnEnable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            selectionBG = MakeTexture(2, 2, new Color(60.0f/255, 95.0f/255, 145.0f/255));
        }

        private void OnSelectionChanged()
        {
            if (selecting) selecting = false;
            else if (materials != null)
            {
                lastClickedMaterialIndex = -1;
                var sel = Selection.objects;
                for (int i = 0; i < materials.Length; i++)
                {
                    var mi = materials[i];
                    if (mi.material && sel.Contains(mi.material))
                    {
                        mi.selected = true;
                        lastClickedMaterialIndex = i;
                    }
                    else mi.selected = false;
                }
                selectedMaterialCount = materials.Where(mi => mi.selected).Count();
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null)
            {
                FacsGUIStyles = new();
                selectedObjectStyle = new("label");
                selectedObjectStyle.normal.background = selectionBG;
                defaultObjectStyle = new("label");
            }

            if (shouldRepaint)
            {
                shouldRepaint = false;
                NullMats(); GetAvailableMaterials();
                selecting = false; OnSelectionChanged();
                return;
            }
            EditorGUILayout.LabelField($"<color=cyan><b>Material List</b></color>\n\n" +
                $"Scans the selected GameObject and its children, lists all attached Materials," +
                $" and lets you select them for editing and locating in Project folders.\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() || (!source && materials != null)) NullMats();

            if (source && GUILayout.Button("Scan!", FacsGUIStyles.Button, GUILayout.Height(40))) GetAvailableMaterials();
            if (materials == null) return;

            EditorGUILayout.LabelField($"<color=green><b>{shaderGroups.Length} Shaders, {materials.Length} Materials:</b></color>", FacsGUIStyles.Helpbox);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            var listRect = EditorGUILayout.BeginVertical();
            int i = 0;
            foreach (var shaderG in shaderGroups)
            {
                if (!shaderG.shader) goto goRepaint;

                shaderG.unfolded = GUILayout.Toggle(shaderG.unfolded, shaderG.shaderName, FacsGUIStyles.Foldout);
                if (shaderG.unfolded)
                {
                    for (int j = 0; j < shaderG.matItems.Length; j++)
                    {
                        var matItem = shaderG.matItems[j];
                        if (!matItem.material || matItem.material.shader != shaderG.shader) goto goRepaint;

                        GUIStyle matStyle = matItem.selected ? selectedObjectStyle : defaultObjectStyle;
                        if (GUILayout.Button("   " + matItem.materialName, matStyle)) HandleObjectClick(i);
                        if (j < shaderG.matItems.Length - 1) GUILayout.Space(-2);
                        i++;
                    }
                }
                else
                {
                    foreach (var matItem in shaderG.matItems)
                    {
                        if (!matItem.material || matItem.material.shader != shaderG.shader) goto goRepaint;
                    }
                    i += shaderG.matItems.Length;
                }
            }
            EditorGUILayout.EndVertical();
            var outsideListClick = Event.current.type == EventType.MouseDown && Event.current.mousePosition.y > listRect.height;
            GUILayout.Space(15);
            EditorGUILayout.EndScrollView();
            if (!outsideListClick && Event.current.type == EventType.MouseDown)
            {
                var scrollRect = GUILayoutUtility.GetLastRect();
                var cursorY = Event.current.mousePosition.y;
                outsideListClick = cursorY < scrollRect.yMin || cursorY > scrollRect.yMax;
            }
            
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy to Clipboard", FacsGUIStyles.Button, GUILayout.Height(40)))
            {
                var clipb = $"Shaders in {source.name}:";
                foreach (var shaderG in shaderGroups)
                {
                    clipb += $"\n• {shaderG.shader.name}\n  - {string.Join("\n  - ", shaderG.matItems.Select(mi=>mi.material.name))}";
                }
                GUIUtility.systemCopyBuffer = clipb;
                ShowNotification(new GUIContent("Copied!"));
            }

            if (Event.current.type == EventType.MouseDown && selectedMaterialCount != 0 && outsideListClick)
            {
                foreach (var mi in materials) mi.selected = false;
                selectedMaterialCount = 0;
                Selection.objects = new Object[0];
                Repaint();
            }

            return;
        goRepaint:
            shouldRepaint = true;
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            return;
        }

        private void GetAvailableMaterials()
        {
            var renderers = source.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) { ShowNotification(new GUIContent("No Renderer found!")); return; }

            var notPrefabAsset = !PrefabUtility.IsPartOfPrefabAsset(source);
            var matDict = new Dictionary<Material, MaterialItem>();
            var matDup = new List<Material>();
            foreach (var rend in renderers)
            {
                foreach (var mat in rend.sharedMaterials)
                {
                    if (!mat || matDup.Contains(mat)) continue;
                    if (!matDict.TryGetValue(mat, out var matItem))
                    {
                        matDict[mat] = new(mat, notPrefabAsset ? rend : null);
                    }
                    else if (notPrefabAsset) matItem.renderers.Add(rend);
                    matDup.Add(mat);
                }
                matDup.Clear();
            }

            if (matDict.Count == 0) { ShowNotification(new GUIContent("No Material found!")); return; }

            var shGroups = new List<ShaderGroup>();
            var groupedMats = matDict.Values.GroupBy(mi => mi.material.shader);
            foreach (var group in groupedMats)
            {
                var sgm = group.OrderBy(mi => mi.materialName).ToArray();
                shGroups.Add(new ShaderGroup(group.Key, sgm));
            }
            shGroups.Sort((x,y)=>string.Compare(x.shaderName,y.shaderName));

            shaderGroups = shGroups.ToArray();
            materials = shGroups.SelectMany(sg => sg.matItems).ToArray();
            lastClickedMaterialIndex = -1;
            selectedMaterialCount = 0;
            selecting = false;
            scrollPosition = default;
        }

        private void HandleObjectClick(int clickedIndex)
        {
            var matItem = materials[clickedIndex];
            if (Event.current.control)
            {
                if (matItem.selected) { matItem.selected = false; selectedMaterialCount--; }
                else { matItem.selected = true; selectedMaterialCount++; }
                selecting = true;
            }
            else if (Event.current.shift && lastClickedMaterialIndex != -1)
            {
                int start = Mathf.Min(lastClickedMaterialIndex, clickedIndex);
                int end = Mathf.Max(lastClickedMaterialIndex, clickedIndex);
                for (int i = start; i <= end; i++)
                {
                    if (!materials[i].selected)
                    {
                        materials[i].selected = true;
                        selectedMaterialCount++;
                        selecting = true;
                    }
                }
            }
            else
            {
                if (selectedMaterialCount > 1)
                {
                    foreach (var mi in materials) mi.selected = false;
                    matItem.renderPing = 0;
                    selecting = true;
                }
                else if (selectedMaterialCount == 0) { selecting = true; matItem.renderPing = 0; }
                else
                {
                    if (clickedIndex == lastClickedMaterialIndex) matItem.PingRenderers();
                    else
                    {
                        materials[lastClickedMaterialIndex].selected = false;
                        matItem.renderPing = 0; selecting = true;
                    }
                }
                matItem.selected = true;
                selectedMaterialCount = 1;
            }
            lastClickedMaterialIndex = clickedIndex;
            if (selecting)
            {
                Selection.objects = materials.Where(mi => mi.selected && mi.material).Select(mi => mi.material).ToArray();
            }
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            Texture2D texture = new(width, height);
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            NullVars();
        }

        private void NullMats()
        {
            shaderGroups = null;
            materials = null;
            lastClickedMaterialIndex = -1;
            selectedMaterialCount = 0;
            scrollPosition = default;
            selecting = false;
        }

        private void NullVars()
        {
            NullMats();
            source = null;
            selectedObjectStyle = null;
            defaultObjectStyle = null;
            FacsGUIStyles = null;
            selectionBG = null;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private class ShaderGroup
        {
            internal bool unfolded = false;
            internal Shader shader;
            internal string shaderName;
            internal MaterialItem[] matItems;

            internal ShaderGroup(Shader sh, MaterialItem[] items)
            {
                shader = sh; matItems = items;
                var shNameU = $"<color=cyan><b>{sh.name}</b></color>";
                shaderName = string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sh)) ? shNameU + " (*)" : shNameU;
            }
        }

        private class MaterialItem
        {
            internal Material material;
            internal string materialName;
            internal List<Renderer> renderers;
            internal int renderPing = 0;
            internal bool selected = false;

            internal MaterialItem(Material mat, Renderer rend)
            {
                material = mat;
                materialName = string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat)) ? mat.name + " (*)" : mat.name;
                renderers = rend ? new(){ rend } : null;
            }

            internal void PingRenderers()
            {
                if (renderers == null) return;
                while (!renderers[renderPing])
                {
                    renderers.RemoveAt(renderPing);
                    if (renderers.Count == 0) { renderers = null; return; }
                    if (renderPing >= renderers.Count) renderPing = 0;
                }
                EditorGUIUtility.PingObject(renderers[renderPing].gameObject);
                renderPing++;
                if (renderPing >= renderers.Count) renderPing = 0;
            }
        }
    }
}
#endif