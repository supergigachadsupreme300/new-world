using System;
using UnityEngine;

/// <summary>
/// Identifies a terrain chunk in the infinite XZ plane. Each terrain chunk
/// covers a 30x30 block of individual tiles (30 world units per side).
///
/// Background threads generate one TerrainChunk at a time, producing all
/// 900 tile meshes in a single dispatch. This reduces ThreadPool overhead
/// from ~3,721 individual dispatches to ~49 chunk dispatches for the same
/// render radius.
/// </summary>
[Serializable]
public struct TerrainChunkCoord : IEquatable<TerrainChunkCoord>
{
    /// <summary>Number of tiles per chunk side.</summary>
    public const int ChunkSize = 30;

    /// <summary>Total tiles in one chunk (ChunkSize * ChunkSize).</summary>
    public const int ChunkArea = ChunkSize * ChunkSize;

    /// <summary>Number of corner heights needed (ChunkSize+1 per side).</summary>
    public const int CornerGridSize = ChunkSize + 1; // 31

    public int X;
    public int Z;

    public TerrainChunkCoord(int x, int z)
    {
        X = x;
        Z = z;
    }

    /// <summary>
    /// Convert a tile-space coordinate to the terrain chunk that contains it.
    /// Handles negative coordinates correctly via FloorToInt.
    /// </summary>
    public static TerrainChunkCoord FromTile(ChunkCoord tile)
    {
        return new TerrainChunkCoord(
            Mathf.FloorToInt((float)tile.X / ChunkSize),
            Mathf.FloorToInt((float)tile.Z / ChunkSize)
        );
    }

    /// <summary>
    /// Convert a world-space position to the terrain chunk that contains it.
    /// </summary>
    public static TerrainChunkCoord FromWorld(Vector3 worldPos)
    {
        int tileX = Mathf.FloorToInt(worldPos.x / ChunkData.Size);
        int tileZ = Mathf.FloorToInt(worldPos.z / ChunkData.Size);
        return FromTile(new ChunkCoord(tileX, tileZ));
    }

    /// <summary>
    /// Returns the tile-space range covered by this chunk.
    /// Tiles go from (minX, minZ) to (maxX, maxZ) inclusive.
    /// </summary>
    public void GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ)
    {
        minX = X * ChunkSize;
        minZ = Z * ChunkSize;
        maxX = minX + ChunkSize - 1;
        maxZ = minZ + ChunkSize - 1;
    }

    /// <summary>
    /// World-space position of the chunk's minimum corner (tile 0,0).
    /// </summary>
    public Vector3 WorldOrigin => new Vector3(X * ChunkSize * ChunkData.Size, 0f, Z * ChunkSize * ChunkData.Size);

    public bool Equals(TerrainChunkCoord other)
    {
        return X == other.X && Z == other.Z;
    }

    public override bool Equals(object obj)
    {
        return obj is TerrainChunkCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + X;
            hash = hash * 31 + Z;
            return hash;
        }
    }

    public static bool operator ==(TerrainChunkCoord a, TerrainChunkCoord b) => a.Equals(b);
    public static bool operator !=(TerrainChunkCoord a, TerrainChunkCoord b) => !a.Equals(b);

    public override string ToString() => $"TChunk({X},{Z})";
}
