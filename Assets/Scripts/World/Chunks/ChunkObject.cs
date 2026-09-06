using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime MonoBehaviour for a single loaded terrain chunk. Owns the merged
/// heightmap mesh, its single derived collider, and the chunk's props
/// (trees/rocks). One ChunkObject per TerrainChunkCoord replaces the old 900
/// per-tile objects — physics, draw calls and scene-graph cost drop ~1000x.
/// Tile heightmaps stay addressable through WorldStreamer._loadedData /
/// ChunkSaveManager; no per-tile GameObject is created anymore.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class ChunkObject : MonoBehaviour
{
    public TerrainChunkCoord ChunkCoord { get; private set; }

    private readonly List<GameObject> _props = new List<GameObject>();

    private MeshFilter _mf;
    private MeshRenderer _mr;
    private MeshCollider _mc;

    private void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mc = GetComponent<MeshCollider>();
    }

    public void Init(TerrainChunkCoord chunkCoord)
    {
        ChunkCoord = chunkCoord;
        name = $"TerrainChunk_{chunkCoord.X}_{chunkCoord.Z}";
    }

    /// <summary>
    /// Applies the pre-built merged chunk mesh arrays to this object. Main thread only.
    /// </summary>
    public void ApplyMerged(MergedChunkMeshData md, Material material, bool buildCollider = true)
    {
        Mesh mesh = ChunkMeshGenerator.CreateMeshFromMerged(md, $"ChunkMesh_{ChunkCoord.X}_{ChunkCoord.Z}");

        if (_mf != null)
        {
            if (_mf.sharedMesh != null)
                Destroy(_mf.sharedMesh);
            _mf.sharedMesh = mesh;
        }

        if (_mr != null && material != null)
            _mr.sharedMaterial = material;

        if (buildCollider && _mc != null)
            _mc.sharedMesh = mesh;
    }

    /// <summary>
    /// Spawns deterministic trees/rocks into this chunk at tile resolution
    /// (1/200 per tile each, mirroring the pre-merge placement exactly). Props
    /// are parented to the chunk root at their world positions.
    /// </summary>
    public void SpawnProps(long seed)
    {
        ChunkCoord.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                // Skip nature props inside the test-platform footprint so trees/rocks can't
                // poke their colliders up through the floating bench and eject the player.
                if (NewWorldTestGround.IsInsidePlatform(x + 0.5f, z + 0.5f))
                    continue;

                var rng = new System.Random(seed.GetHashCode() ^ (x * 73856093) ^ (z * 19349663));

                if (rng.Next(200) == 0)
                    SpawnTree(seed, x, z, rng);

                if (rng.Next(200) == 0)
                    SpawnRock(seed, x, z, rng);
            }
        }
    }

    private void SpawnTree(long seed, int tileX, int tileZ, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = tileX + localX;
        float worldZ = tileZ + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var tree = MapBuilder.BuildTree(transform, new Vector3(worldX, worldY, worldZ));
        tree.name = $"Tree_{tileX}_{tileZ}";
        _props.Add(tree);
    }

    private void SpawnRock(long seed, int tileX, int tileZ, System.Random rng)
    {
        float localX = (float)rng.NextDouble();
        float localZ = (float)rng.NextDouble();
        float worldX = tileX + localX;
        float worldZ = tileZ + localZ;
        float worldY = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
        var rock = MapBuilder.BuildStone(transform, new Vector3(worldX, worldY, worldZ));
        rock.name = $"Rock_{tileX}_{tileZ}";
        _props.Add(rock);
    }

    public void Release()
    {
        for (int i = _props.Count - 1; i >= 0; i--)
        {
            if (_props[i] != null)
                Destroy(_props[i]);
        }
        _props.Clear();

        if (_mf != null && _mf.sharedMesh != null)
            Destroy(_mf.sharedMesh);
        if (_mc != null)
            _mc.sharedMesh = null;
    }
}