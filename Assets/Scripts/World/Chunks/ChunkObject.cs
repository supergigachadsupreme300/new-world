using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime MonoBehaviour for a single loaded chunk. Holds the generated mesh,
/// derived collider, and the source data so modifications can be persisted.
/// Each chunk owns its own prop list (trees/rocks) — no global tracking needed.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class ChunkObject : MonoBehaviour
{
    /// <summary>Chunk this object currently represents.</summary>
    public ChunkCoord Coord { get; private set; }

    /// <summary>Per-chunk prop list (trees, rocks). Destroyed when chunk unloads.</summary>
    private readonly List<GameObject> _props = new List<GameObject>();

    /// <summary>Kick off a fresh coordinate when pooled.</summary>
    public void Init(ChunkCoord coord)
    {
        Coord = coord;
        name = $"Tile_{coord.X}_{coord.Z}";
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

    /// <summary>
    /// Deterministically spawn trees and rocks for this chunk.
    /// Uses seeded RNG from the world seed + chunk coords so the same
    /// props appear every time this chunk is loaded.
    /// </summary>
    public void SpawnProps(long seed)
    {
        // Deterministic RNG from seed + chunk coords.
        var rng = new System.Random(seed.GetHashCode() ^ (Coord.X * 73856093) ^ (Coord.Z * 19349663));

        // ~1 tree per 4 chunks.
        if (rng.Next(4) == 0)
            SpawnTree(seed, rng);

        // ~1 rock per 6 chunks.
        if (rng.Next(6) == 0)
            SpawnRock(seed, rng);
    }

    private void SpawnTree(long seed, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = Coord.X + localX;
        float worldZ = Coord.Z + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var tree = MapBuilder.BuildTree(transform, new Vector3(localX, worldY, localZ));
        tree.name = $"Tree_{Coord.X}_{Coord.Z}";
        _props.Add(tree);
    }

    private void SpawnRock(long seed, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = Coord.X + localX;
        float worldZ = Coord.Z + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var rock = MapBuilder.BuildStone(transform, new Vector3(localX, worldY, localZ));
        rock.name = $"Rock_{Coord.X}_{Coord.Z}";
        _props.Add(rock);
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
        // Destroy per-chunk props (trees, rocks).
        for (int i = _props.Count - 1; i >= 0; i--)
        {
            if (_props[i] != null)
                Destroy(_props[i]);
        }
        _props.Clear();

        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            Destroy(mf.sharedMesh);
        var mc = GetComponent<MeshCollider>();
        if (mc != null)
            mc.sharedMesh = null;
    }
}
