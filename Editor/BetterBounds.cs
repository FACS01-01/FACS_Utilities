#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FACS01.Utilities
{
    public class BetterBounds : EditorWindow
    {
        public GameObject source;

        private static FACSGUIStyles FacsGUIStyles;

        [MenuItem("FACS Utils/Misc/Better Avatar Bounds", false, 1000)]
        public static void ShowWindow()
        {
            GetWindow(typeof(BetterBounds), false, "Better Avatar Bounds", true);
        }
        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"<color=cyan><b>Better Avatar Bounds</b></color>\n\nScans all Skinned Mesh Renderers directly under the selected GameObject (avatar), and calculates a new bounding box for all of them.\n\nThe new and bigger bounding box should fix issues with parts of the avatar disappearing when viewed from certain angles.\n", FacsGUIStyles.helpbox);

            source = (GameObject)EditorGUILayout.ObjectField(source, typeof(GameObject), true, GUILayout.Height(40));

            if (GUILayout.Button("Run Fix!", FacsGUIStyles.button, GUILayout.Height(40)))
            {
                if (source != null)
                {
                    Debug.Log("BETTER AVATAR BOUNDS BEGINS");
                    RunFix();
                    Debug.Log("BETTER AVATAR BOUNDS FINISHED");
                }
                else
                {
                    ShowNotification(new GUIContent("Nothing selected?"));
                }
            }
        }
        private void RunFix()
        {
            Transform rootT = source.transform;
            Bounds finalBounds = new Bounds();
            List<SkinnedMeshRenderer> SMRList = new List<SkinnedMeshRenderer>();
            foreach (Transform childT in rootT)
            {
                SkinnedMeshRenderer childSMR = childT.GetComponent<SkinnedMeshRenderer>();
                if (childSMR != null) SMRList.Add(childSMR);
            }
            int childSMRCount = SMRList.Count;
            if (childSMRCount == 0)
            {
                Debug.LogError($"No Skinned Mesh Renderer found under {rootT.name}");
                return;
            }
            finalBounds = SMRList[0].localBounds;
            bool allBoundsEqual = false;
            if (childSMRCount > 1)
            {
                allBoundsEqual = true;
                for (int i = 1; i < childSMRCount; i++)
                {
                    if (allBoundsEqual && finalBounds != SMRList[i].localBounds)
                    {
                        allBoundsEqual = false;
                    }
                    finalBounds.Encapsulate(SMRList[i].localBounds);
                }
            }
            if (allBoundsEqual)
            {
                Debug.LogWarning("All bounding boxes are the same size. Did you use the fix already? If not, just edit any of the bounds a bit and try again.");
            }
            else
            {
                Vector3 Center = finalBounds.center;
                Vector3 Extents = finalBounds.extents;
                float dx = Extents.x; float dy = Extents.y; float dz = Extents.z;

                Vector3 tmpMax = Center + new Vector3 { x = dy, y = dx, z = dz };
                Vector3 tmpMin = Center - new Vector3 { x = dy, y = dx, z = dz };
                finalBounds.Encapsulate(tmpMax); finalBounds.Encapsulate(tmpMin);
                tmpMax = Center + new Vector3 { x = dz, y = dx, z = dy };
                tmpMin = Center - new Vector3 { x = dz, y = dx, z = dy };
                finalBounds.Encapsulate(tmpMax); finalBounds.Encapsulate(tmpMin);

                Center = finalBounds.center;
                Extents = finalBounds.extents;
                dx = Extents.x; dy = Extents.y; dz = Extents.z;
                tmpMax = Center + new Vector3 { x = dx, y = 1.5f * dy, z = dz };
                finalBounds.Encapsulate(tmpMax);
                foreach (SkinnedMeshRenderer childSMR in SMRList)
                {
                    Undo.RegisterCompleteObjectUndo(childSMR, "Better Bounds");
                    childSMR.localBounds = finalBounds;
                }
                Debug.Log($"Better Bounds applied to {childSMRCount} Skinned Mesh Renderer{(childSMRCount>1?"s":"")}!");
            }

        }
        void OnDestroy()
        {
            source = null;
        }
    }
}
#endif