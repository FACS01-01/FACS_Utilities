using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    public class FindMissingScriptsRecursivelyAndRemove : EditorWindow 
    {
        public GameObject source;
        private static string sourcename;
        private static int _goCount;
        private static int _missingCount;
 
        private static bool _bHaveRun;
 
        [MenuItem("Tools/FACS Utilities/Find Missing Scripts And Remove")]
        public static void ShowWindow()
        {
            GetWindow(typeof(FindMissingScriptsRecursivelyAndRemove), false, "Find Missing Scripts And Remove", true);
        }
 
        public void OnGUI()
        {
            GUIStyle newstyle = new GUIStyle(GUI.skin.GetStyle("HelpBox"));
            newstyle.richText = true;
            newstyle.fontSize = 13;

            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(UnityEngine.Object), true);

            if (GUILayout.Button("Scan!") && source != null)
            {
                FindInSelected(source);
            }
 
            if (!_bHaveRun) return;

            EditorGUILayout.TextArea($"Selected Object: <color=green>{sourcename}</color>", newstyle);

            if (_goCount<=0) EditorGUILayout.TextField($"No Game Objects Scanned.", newstyle);
            else EditorGUILayout.TextArea($"{_goCount} GameObjects Scanned.\n{_missingCount} Scripts Deleted.", newstyle);
        }
        
        private static void FindInSelected(GameObject src)
        {
            _goCount = 0;
            _missingCount = 0;
            sourcename = src.name;

            FindInGo(src);
 
            _bHaveRun = true;
            
            AssetDatabase.SaveAssets();
        }
		
        private static void FindInGo(GameObject g)
        {
            _goCount++;
         
            var tempCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g);
			
			if (tempCount > 0) {
				var s = g.name;
				var t = g.transform;
				while (t.parent != null) {
					s = t.parent.name +"/"+s;
					t = t.parent;
				}
				
				_missingCount = _missingCount + GameObjectUtility.RemoveMonoBehavioursWithMissingScript(g);
			}
            
            foreach (Transform childT in g.transform)
            {
                FindInGo(childT.gameObject);
            }
        }
    }
}