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

        var combinedMesh = new Mesh();
        combinedMesh.name = "Combined_" + root.name;
        combinedMesh.subMeshCount = groups.Count;

        var vertices = new List<Vector3>(1024);
        var normals = new List<Vector3>(1024);
        var uvs = new List<Vector2>(1024);
        var tangents = new List<Vector4>(1024);
        var triangles = new List<List<int>>(groups.Count);
        for (int i = 0; i < groups.Count; i++)
            triangles.Add(new List<int>(1024));

        for (int g = 0; g < groups.Count; g++)
        {
            var groupMesh = new Mesh();
            groupMesh.CombineMeshes(groups[g].ToArray(), true, true);

            int offset = vertices.Count;
            vertices.AddRange(groupMesh.vertices);
            normals.AddRange(groupMesh.normals);
            if (groupMesh.uv.Length > 0)
                uvs.AddRange(groupMesh.uv);
            if (groupMesh.tangents.Length > 0)
                tangents.AddRange(groupMesh.tangents);

            var tris = groupMesh.GetTriangles(0);
            for (int i = 0; i < tris.Length; i++)
                triangles[g].Add(tris[i] + offset);
        }

        combinedMesh.vertices = vertices.ToArray();
        if (normals.Count == vertices.Count)
            combinedMesh.normals = normals.ToArray();
        if (uvs.Count == vertices.Count)
            combinedMesh.uv = uvs.ToArray();
        if (tangents.Count == vertices.Count)
            combinedMesh.tangents = tangents.ToArray();
        for (int g = 0; g < groups.Count; g++)
            combinedMesh.SetTriangles(triangles[g], g);

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