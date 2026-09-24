using UnityEngine;

/// <summary>
/// Thread-safe mesh data for an entire terrain chunk (30x30 = 900 tiles).
/// Produced by a single background thread dispatch and consumed on the main
/// thread during finalization.
/// </summary>
public struct TerrainChunkMeshData
{
    public TerrainChunkCoord Coord;

    /// <summary>
    /// Per-tile mesh data arrays. Length is always ChunkArea (900).
    /// Index = (localZ * ChunkSize + localX) where local coords are 0-29.
    /// Kept so tile heightmaps can be written into WorldStreamer._loadedData
    /// (persistence / validation) without keeping per-tile GameObjects.
    /// </summary>
    public ChunkMeshData[] Tiles;

    /// <summary>
    /// Single merged chunk mesh (built on the worker thread). Vertices are
    /// chunk-local metres (0..30), so the main thread creates just ONE
    /// GameObject + ONE MeshCollider per chunk instead of 900 tile objects.
    /// </summary>
    public MergedChunkMeshData Merged;

    /// <summary>True when BuildOrLoadChunk restored saved deformation mods from disk. The main
    /// thread uses it to flag the chunk as modified in O(1) (1ea) instead of re-scanning 900 tiles.</summary>
    public bool HadLoadedMods;

    /// <summary>The multi-run column store of a VOXEL chunk (1eu), piped from the worker's
    /// BuildVoxelChunk to the reconstructed ChunkObject's VoxelStore. Null for smooth chunks.
    /// This is the authoritative edit record: clear-only rebuilds overlay surface tiles onto it and
    /// saves serialize it, so carved caves/overhangs survive both.</summary>
    public VoxelChunkData Voxel;
}

/// <summary>
/// Pure C# arrays for one merged terrain chunk mesh. Contains only arrays —
/// no Unity API objects, so it is safe to build on a background thread.
/// </summary>
public struct MergedChunkMeshData
{
    public Vector3[] Vertices;
    public int[] Triangles;
    public Vector2[] UV;
    public Vector3[] Normals;
    public Color[] Colors;
    public Bounds Bounds;
}