using UnityEngine;

/// <summary>
/// Builds the triangulated ground mesh for a tile from its ChunkData.
///
/// Each 1x1 tile is a flat quad split into 2 triangles:
///
///   NW --------------- NE
///   |       T1       / |
///   |             /    |
///   |           /      |
///   |         /        |
///   |       /    T2    |
///   |     /            |
///   |   /              |
///   SW --------------- SE
///
/// Corner-height contract (VERY important for a gapless mesh):
///   Every tile computes its 4 corner heights from pure world-space noise at the
///   exact corner coordinates. A neighbouring tile shares those same corners and
///   therefore computes the same heights, so shared edges always line up with
///   ZERO gaps.
///
/// Vertex slot layout (matches ChunkData):
///   0 = NW (minX, maxZ)
///   1 = NE (maxX, maxZ)
///   2 = SE (maxX, minZ)
///   3 = SW (minX, minZ)
/// </summary>
public static class ChunkMeshGenerator
{
    /// <summary>
    /// Builds the mesh. If the ChunkData already carries 4 heights they are used
    /// directly (fast load path); otherwise they are derived from world noise.
    /// </summary>
    public static Mesh BuildMesh(ChunkData data, NoiseLayerConfig[] layers = null)
    {
        float worldScale = ChunkData.Size;

        // Local-space origin and extent. The tile GameObject is already placed
        // at (ChunkX * Size, 0, ChunkZ * Size) by the WorldStreamer, so mesh
        // vertices must be relative to that position.
        float minX = 0f;
        float minZ = 0f;
        float maxX = worldScale;
        float maxZ = worldScale;

        // Resolve the 4 corner heights. Respect stored data when present (load path);
        // otherwise they are generated deterministically from the world seed.
        float[] h = new float[ChunkData.VertexCount];
        for (int i = 0; i < ChunkData.CornerCount; i++)
        {
            h[i] = (data.IsValid && data.Heights != null)
                ? data.Heights[i]
                : SampleCornerHeight(data, i, layers);
        }

        // Vertex positions: local-space, slot layout 0=NW,1=NE,2=SE,3=SW.
        Vector3[] vertices =
        {
            new Vector3(minX, h[0], maxZ), // 0 = NW
            new Vector3(maxX, h[1], maxZ), // 1 = NE
            new Vector3(maxX, h[2], minZ), // 2 = SE
            new Vector3(minX, h[3], minZ), // 3 = SW
        };

        // Two triangles, flat quad, face UP.
        int[] triangles =
        {
            // T1: NW, NE, SE -> +Y
            0, 1, 2,
            // T2: NW, SE, SW -> +Y
            0, 2, 3,
        };

        // UVs: 0,0 at SW up to 1,1 at NE.
        Vector2[] uv =
        {
            new Vector2(0f, 1f), // NW
            new Vector2(1f, 1f), // NE
            new Vector2(1f, 0f), // SE
            new Vector2(0f, 0f), // SW
        };

        Mesh mesh = new Mesh
        {
            name = $"TileMesh_{data.ChunkX}_{data.ChunkZ}"
        };
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Deterministic corner height for a given slot by sampling the world-space
    /// corner coordinate. Slot layout (matches ChunkData):
    ///   0=NW, 1=NE, 2=SE, 3=SW.
    /// </summary>
    private static float SampleCornerHeight(ChunkData data, int slot, NoiseLayerConfig[] layers)
    {
        float worldScale = ChunkData.Size;
        float minX = data.ChunkX * worldScale;
        float minZ = data.ChunkZ * worldScale;
        float maxX = minX + worldScale;
        float maxZ = minZ + worldScale;

        float x = 0f, z = 0f;
        switch (slot)
        {
            case 0: x = minX; z = maxZ; break; // NW
            case 1: x = maxX; z = maxZ; break; // NE
            case 2: x = maxX; z = minZ; break; // SE
            case 3: x = minX; z = minZ; break; // SW
        }

        return TerrainNoiseGenerator.GetHeight(data.Seed, x, z, layers, 0f);
    }
}
