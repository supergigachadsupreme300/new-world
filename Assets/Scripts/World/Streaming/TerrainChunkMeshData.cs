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
    /// </summary>
    public ChunkMeshData[] Tiles;
}
