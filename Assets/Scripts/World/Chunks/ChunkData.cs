using System;
using UnityEngine;

/// <summary>
/// Pure heightmap data for a single 1x1 tile.
///
/// A tile holds 4 corner vertex heights:
///   [0] NW (north-west corner of the tile quad)
///   [1] NE
///   [2] SE
///   [3] SW
///
/// The quad corners are shared with adjacent tiles. Their heights are derived
/// deterministically from WORLD coordinates so neighbouring tiles always agree
/// on shared-edge heights — this is what guarantees a gapless triangulated mesh.
///
/// This struct is serializable so it can be written to / read from disk quickly.
/// </summary>
[Serializable]
public struct ChunkData
{
    public const int CornerCount = 4;
    public const int VertexCount = 4;
    public const int TriangleCount = 2;

    /// <summary>World-space size of one tile along X and Z (1x1).</summary>
    public const float Size = 1f;

    public int ChunkX;
    public int ChunkZ;
    public long Seed;

    /// <summary>Heights array. Indices are the vertex slots documented above.</summary>
    public float[] Heights;

    /// <summary>
    /// Modification version stamp. Incremented whenever a player deforms the
    /// terrain so the persistence layer knows to rewrite the tile file.
    /// </summary>
    public int Version;

    /// <summary>True if this tile has localised player modifications (deltas).</summary>
    public bool HasModifications;

    public ChunkData(int chunkX, int chunkZ, long seed)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        Seed = seed;
        Heights = new float[VertexCount];
        Version = 0;
        HasModifications = false;
    }

    /// <summary>World-space X of a given corner vertex slot.</summary>
    public float VertexWorldX(int slot)
    {
        int x = chunkXOffset(slot);
        return ChunkX * Size + (x > 0 ? Size : 0f);
    }

    /// <summary>World-space Z of a given corner vertex slot.</summary>
    public float VertexWorldZ(int slot)
    {
        int z = chunkZOffset(slot);
        return ChunkZ * Size + (z > 0 ? Size : 0f);
    }

    /// <summary>World X offset (0 or Size) from the tile's min corner.</summary>
    private static int chunkXOffset(int slot)
    {
        switch (slot)
        {
            case 0: return 0; // NW
            case 1: return 1; // NE
            case 2: return 1; // SE
            case 3: return 0; // SW
            default: return 0;
        }
    }

    /// <summary>World Z offset (0 or Size) from the tile's min corner.</summary>
    private static int chunkZOffset(int slot)
    {
        switch (slot)
        {
            case 0: return 1; // NW
            case 1: return 1; // NE
            case 2: return 0; // SE
            case 3: return 0; // SW
            default: return 0;
        }
    }

    /// <summary>
    /// Validity check — the heights array must be the correct length.
    /// </summary>
    public bool IsValid => Heights != null && Heights.Length == VertexCount;

    /// <summary>
    /// Make an identical copy (for caching / references that do not alias).
    /// </summary>
    public ChunkData ShallowCopy()
    {
        ChunkData copy = this;
        if (Heights != null)
            copy.Heights = (float[])Heights.Clone();
        return copy;
    }
}
