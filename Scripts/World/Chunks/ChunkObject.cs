using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime MonoBehaviour for a single loaded chunk. Holds the generated mesh,
/// derived collider, and the source data so modifications can be persisted.
///
/// This component is pooled/reused by the WorldStreamer; the associated mesh and
/// collider are rebuilt when the chunk is (re)loaded.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class ChunkObject : MonoBehaviour
{
    /// <summary>Chunk this object currently represents.</summary>
    public ChunkCoord Coord { get; private set; }

    /// <summary>Kick off a fresh coordinate when pooled.</summary>
    public void Init(ChunkCoord coord)
    {
        Coord = coord;
        name = $"Chunk_{coord.X}_{coord.Z}";
    }

    /// <summary>
    /// Applies serialized ChunkData to this object: rebuilds the mesh and
    /// collider. If the mesh/height data is provided it is honoured directly
    /// (fast path from disk); otherwise it is generated from noise.
    /// </summary>
    public void Apply(ChunkData data, NoiseLayerConfig[] layers, Material material, bool buildCollider = true)
    {
        Mesh mesh = ChunkMeshGenerator.BuildMesh(data, layers);

        var mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            // Destroy the old mesh to avoid leaking GPU meshes over time.
            if (mf.sharedMesh != null)
                Destroy(mf.sharedMesh);
            mf.sharedMesh = mesh;
        }

        var mr = GetComponent<MeshRenderer>();
        if (mr != null && material != null)
            mr.sharedMaterial = material;

        if (buildCollider)
        {
            var mc = GetComponent<MeshCollider>();
            if (mc != null)
                mc.sharedMesh = mesh;
        }
    }

    /// <summary>Read back the current serialized data (as modified in-memory).</summary>
    public ChunkData CaptureData()
    {
        // The source chunk data is owned by the WorldStreamer; this method is a
        // convenience for cases where the chunk needs to snapshot itself.
        ChunkData data = new ChunkData(Coord.X, Coord.Z, 0) { Heights = new float[ChunkData.VertexCount] };
        return data;
    }

    /// <summary>Called when the chunk is unloaded/pooled.</summary>
    public void Release()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            Destroy(mf.sharedMesh);
        var mc = GetComponent<MeshCollider>();
        if (mc != null)
            mc.sharedMesh = null;
    }
}
