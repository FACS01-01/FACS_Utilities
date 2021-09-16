using UnityEditor;
using UnityEngine;
using System;

namespace FACS01.Utilities
{
    public class RemoveMissingScripts : EditorWindow 
    {
        public GameObject source;
        private static int GameObjectsScanned;
        private static int missingScriptsCount;
 
        private static string results;

        [MenuItem("Tools/FACS Utilities/Remove Missing Scripts")]
        public static void ShowWindow()
        {
            EditorWindow editorWindow = GetWindow(typeof(RemoveMissingScripts), false, "Remove Missing Scripts", true);
            editorWindow.autoRepaintOnSceneChange = true;
        }
        public void OnGUI()
        {
            GUIStyle newstyle = new GUIStyle(GUI.skin.GetStyle("HelpBox"));
            newstyle.richText = true;
            newstyle.fontSize = 13;
            newstyle.wordWrap = true;

            EditorGUILayout.LabelField($"<color=cyan><b>Remove Missing Scripts</b></color>\n\nScans the selected GameObject" +
                $" and tries to delete any missing scripts inside it's hierarchy.\n" +
                $"After that, if the selected GameObject is a Prefab, it will be unpacked completely.\n\n" +
                $"The fix can be Undone, but it will give errors on Console.\n", newstyle);

            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(40));

            newstyle.alignment = TextAnchor.MiddleCenter;
            if (GUILayout.Button("Run!", newstyle, GUILayout.Height(40)))
            {
                if (source != null)
                {
                    FindInSelected(source);
                }
                else
                {
                    ShowNotification(new GUIContent("Empty selection?"));
                    NullVars();
                }
            }
            if (results != null && results != "")
            {
                newstyle.alignment = TextAnchor.MiddleLeft;
                EditorGUILayout.LabelField(results, newstyle);
            }
        }
        
        private static void FindInSelected(GameObject src)
        {
            GameObjectsScanned = 0;
            missingScriptsCount = 0;
            results = "";

            FindInGo(src);

            if (missingScriptsCount > 0)
            {
                try
                {
                    PrefabUtility.UnpackPrefabInstance(src, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                catch (ArgumentException) { }
            }

            GenerateResults();
        }
        private static void FindInGo(GameObject g)
        {
            GameObjectsScanned++;
         
            var tempCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g);
			
			if (tempCount > 0) {
                Undo.RegisterCompleteObjectUndo(g, "Remove Empty Scripts");
                missingScriptsCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(g);
			}
            
            foreach (Transform childT in g.transform)
            {
                FindInGo(childT.gameObject);
            }
        }
        private static void GenerateResults()
        {
            results = $"Results:\n";
            results += $"   • <color=green>Child GameObjects scanned:</color> {GameObjectsScanned}\n";
            results += $"   • <color=green>Missing scripts deleted:</color> {missingScriptsCount}\n";
        }
        private void OnDestroy()
        {
            source = null;
            NullVars();
        }
        void NullVars()
        {
            results = null;
        }
    }
}