#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class OptimizedModelImport : AssetPostprocessor
    {
		private const string RichToolName = Logger.ToolTag + "[Optimized Model Import]" + Logger.EndTag;

        public override uint GetVersion()
        {
			return 2;
		}

        private void OnPostprocessModel(GameObject go)
		{
			OptimizeModel(go);
		}

		private void OptimizeModel(GameObject rootGO)
        {
			var SMRs = rootGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			if (SMRs.Length == 0 || SMRs.All(smr => !smr.sharedMesh)) return;

			var mesh_SMRs = SMRs.Where(smr => smr.sharedMesh).GroupBy(smr => smr.sharedMesh);
			bool optimized = false;
			foreach (var msh_smrs in mesh_SMRs)
            {
				var data = OptimizeMesh(msh_smrs.Key);
				if (data.removeIdxs == null) continue;
				foreach (var smr in msh_smrs) OptimizeSMR(smr, data);
				optimized = true;
			}

			if (optimized) Logger.Log($"{RichToolName} Optimized {Logger.RichModel}: {rootGO.name}");
		}

		private MeshData OptimizeMesh(Mesh mesh)
        {
			var newData = new MeshData();

			var boneWeights = mesh.GetAllBoneWeights();
			bool[] KeepBoneIdx = null;
			int toDeleteCount = 0;
			using (var so = new SerializedObject(mesh))
			{
				var bindPose = so.FindProperty("m_BindPose");
				var boneCount = bindPose.arraySize;
				newData.oldBonesCount = boneCount;
				KeepBoneIdx = new bool[boneCount];
				foreach (var bw in boneWeights) if (bw.boneIndex < boneCount) KeepBoneIdx[bw.boneIndex] = true;

				var rootBoneHash = so.FindProperty("m_RootBoneNameHash").uintValue;
				var boneNameHashes = so.FindProperty("m_BoneNameHashes");
				var boneNameHashesCount = boneNameHashes.arraySize;
				if (boneNameHashesCount > 0)
				{
					var boneNameHashIter = boneNameHashes.GetArrayElementAtIndex(0);
					for (int i = 0; i < boneNameHashesCount; i++)
					{
						if (rootBoneHash == boneNameHashIter.uintValue) { KeepBoneIdx[i] = true; break; }
						boneNameHashIter.Next(false);
					}
				}

				toDeleteCount = KeepBoneIdx.Count(b => !b);
				newData.newBonesCount = boneCount - toDeleteCount;
				if (toDeleteCount == 0) goto BlendShapeOptimizations;
				newData.removeIdxs = new int[toDeleteCount];
				int rmvIdx = 0;
				for (int i = 0; i < KeepBoneIdx.Length; i++) if (!KeepBoneIdx[i]) newData.removeIdxs[rmvIdx++] = i;

				var bonesAABB = so.FindProperty("m_BonesAABB");

				for (int i = 0; i < newData.removeIdxs.Length; i++)
				{
					rmvIdx = newData.removeIdxs[i] - i;
					bindPose.DeleteArrayElementAtIndex(rmvIdx);
					bonesAABB.DeleteArrayElementAtIndex(rmvIdx);
					boneNameHashes.DeleteArrayElementAtIndex(rmvIdx);
				}
				so.ApplyModifiedPropertiesWithoutUndo();
			}
			Dictionary<int, int> newBoneIdxs = new();
			int boneIdx = 0;
			int firstDiffBoneIdx = -1;
			for (int i = 0; i < KeepBoneIdx.Length; i++)
			{
				if (KeepBoneIdx[i]) newBoneIdxs.Add(i, boneIdx++);
				else if (firstDiffBoneIdx == -1) firstDiffBoneIdx = i;
			}
			var weightsArray = new Unity.Collections.NativeArray<BoneWeight1>(boneWeights, Unity.Collections.Allocator.Temp);
			for (int i = 0; i < weightsArray.Length; i++)
			{
				var oldW = weightsArray[i];
				if (oldW.boneIndex < firstDiffBoneIdx) continue;
				oldW.boneIndex = newBoneIdxs[oldW.boneIndex];
				weightsArray[i] = oldW;
			}
			var bonesPerVertex = mesh.GetBonesPerVertex();
			mesh.SetBoneWeights(bonesPerVertex, weightsArray);
			bonesPerVertex.Dispose();
			weightsArray.Dispose();
		BlendShapeOptimizations:
			boneWeights.Dispose();

			return newData;
		}

		private void OptimizeSMR(SkinnedMeshRenderer smr, MeshData data)
        {
			using (var so = new SerializedObject(smr))
			{
				var bones = so.FindProperty("m_Bones");
				var boneCount = bones.arraySize;

				if (boneCount > data.oldBonesCount)
				{ for (int i = data.oldBonesCount; i < boneCount; i++) bones.DeleteArrayElementAtIndex(data.oldBonesCount); }

				for (int i = 0; i < data.removeIdxs.Length; i++)
				{
					var rmvIdx = data.removeIdxs[i] - i;
					if (rmvIdx < boneCount)
					{ bones.DeleteArrayElementAtIndex(rmvIdx); boneCount--; }
				}

				so.ApplyModifiedPropertiesWithoutUndo();
			}
		}

		private record MeshData
        {
			public int oldBonesCount;
			public int newBonesCount;
			public int[] removeIdxs = null;
		}
	}
}
#endif