using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collapses the child cube renderers of a static root into a single combined
/// mesh (one renderer per root). Source GameObjects and their colliders are kept
/// so gameplay raycasts (mining, chopping) still work; only renderers are removed.
/// Safe for roots that are always mutated as a whole (destroyed/rebuilt), e.g.
/// rocks and crop fields. Do not use on trees: the chop/fell system re-parents
/// individual child cubes at runtime.
/// </summary>
public static class MeshCombiner
{
    private const string MarkerName = "_Combined";

    /// <summary>Combines a root's children into one renderer. Idempotent per root.</summary>
    public static bool CombineStaticParts(GameObject root)
    {
        if (root == null) return false;
        if (root.transform.Find(MarkerName) != null) return false;

        var mats = new List<Material>();
        var groups = new List<List<CombineInstance>>();
        var materialIndex = new Dictionary<Material, int>();

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.name == MarkerName) continue;
            if (mf.sharedMesh == null) continue;

            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            var sourceMats = mr.sharedMaterials;
            int count = Mathf.Min(mf.sharedMesh.subMeshCount, sourceMats.Length);
            for (int s = 0; s < count; s++)
            {
                var mat = sourceMats[s];
                if (!materialIndex.TryGetValue(mat, out int idx))
                {
                    idx = groups.Count;
                    materialIndex.Add(mat, idx);
                    mats.Add(mat);
                    groups.Add(new List<CombineInstance>());
                }

                groups[idx].Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = s,
                    transform = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix
                });
            }
        }

        if (groups.Count == 0) return false;

        var arrays = new CombineInstance[groups.Count][];
        for (int i = 0; i < groups.Count; i++)
            arrays[i] = groups[i].ToArray();

        var combinedMesh = new Mesh();
        combinedMesh.name = "Combined_" + root.name;
        combinedMesh.CombineMeshes(arrays, true, true);

        var combinedGo = new GameObject(MarkerName);
        combinedGo.transform.SetParent(root.transform, false);
        combinedGo.transform.localPosition = Vector3.zero;
        combinedGo.transform.localRotation = Quaternion.identity;
        combinedGo.transform.localScale = Vector3.one;

        var filter = combinedGo.AddComponent<MeshFilter>();
        filter.sharedMesh = combinedMesh;

        var renderer = combinedGo.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = mats.ToArray();

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null || mr.name == MarkerName) continue;
            Object.Destroy(mr);
        }

        return true;
    }
}