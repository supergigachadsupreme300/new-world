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

    /// <summary>
    /// Start index into <see cref="Vertices"/> of each tile's merged shallow block (1ew). May be
    /// null for non-terrain merged builders (e.g. the voxel path).
    /// </summary>
    public int[] TileVertexBase;

    /// <summary>
    /// Vertex count of each tile's merged shallow block: 4 (coarse quad, legacy layout) or 16
    /// (1ew refined 2x2 block). Consumers use the base+count pair instead of the old fixed
    /// (tileIndex * 4) stride so refined and coarse tiles can safely mix inside one chunk mesh.
    /// </summary>
    public int[] TileVertexCount;

    /// <summary>
    /// Coarse (31x31) world-corner lattice the LOD children sample from (1ew). Built by the chunk
    /// mesh generator on the worker thread, then re-stamped per patched region by PatchCornerGrid.
    /// Y is tile-relative height (0..1), mirrors the merged shallow block exactly. Null on
    /// non-terrain builders (voxel path builds its own LOD).
    /// </summary>
    public ChunkCornerGrid Corners;

    /// <summary>
    /// Decimated 2 m collider surface (1hi): every 2nd node of <see cref="Corners"/> re-indexed with
    /// the LOD winding — 256 verts / 450 tris vs. the full merged render mesh (up to ~1800+ tris).
    /// Built on the worker thread so the chunk's MeshCollider can be ~4x cheaper to cook on the
    /// gameplay frame. Null on non-terrain builders (voxel path re-cooks its render mesh).
    /// </summary>
    public Vector3[] ColliderVertices;

    /// <summary>Triangles of the decimated collider surface (1hi, see <see cref="ColliderVertices"/>).</summary>
    public int[] ColliderTriangles;
}

/// <summary>
/// The coarse lattice of a terrain chunk's world corners (1ew): one node per corner of the 31x31
/// corner grid (TerrainChunkCoord.CornerGridSize), stored in corner-lattice order gz * 31 + gx so a
/// LOD child can sample axis-aligned strides without ever touching the merged block table. Carried
/// by MergedChunkMeshData because refined (16-vertex) shallow blocks break the fixed stride the old
/// LOD sampler used on the merged vertex array.
/// </summary>
public struct ChunkCornerGrid
{
    public float[] Y;
    public Vector3[] Normals;
    public Vector2[] UV;
    public Color[] Colors;

    public ChunkCornerGrid(int size)
    {
        Y = new float[size];
        Normals = new Vector3[size];
        UV = new Vector2[size];
        Colors = new Color[size];
    }
}