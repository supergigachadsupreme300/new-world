using UnityEngine;

/// <summary>
/// Builds the triangulated ground mesh for a chunk from its ChunkData.
///
/// Each 1x1 chunk is a quad split into 4 triangles around a central pivot:
///
///   NW --------------- NE
///   |  \     T1     /  |
///   |    \       /    |
///   | T4   \   /   T2 |
///   |       CE        |
///   | T3   /   \      |
///   |    /       \    |
///   |  /    T5     \  |
///   SW --------------- SE
///
/// Corner-height contract (VERY important for a gapless mesh):
///   Every chunk computes its 4 corner heights from pure world-space noise at the
///   exact corner coordinates. A neighbouring chunk shares those same corners and
///   therefore computes the same heights, so shared edges always line up with
///   ZERO gaps. The center (CE) vertex is unique to this chunk and carries a
///   deterministic pseudo-random pivot offset so the surface looks naturally
///   terraformed without ever breaking the outer boundary.
///
/// Vertex slot layout (matches ChunkData):
///   0 = NW (minX, maxZ)
///   1 = NE (maxX, maxZ)
///   2 = SE (maxX, minZ)
///   3 = SW (minX, minZ)
///   4 = Center
/// </summary>
public static class ChunkMeshGenerator
{
    /// <summary>
    /// Builds the mesh. If the ChunkData already carries 5 heights they are used
    /// directly (fast load path); otherwise they are derived from world noise.
    /// </summary>
    public static Mesh BuildMesh(ChunkData data, NoiseLayerConfig[] layers = null)
    {
        float worldScale = ChunkData.Size;
        float minX = data.ChunkX * worldScale;
        float minZ = data.ChunkZ * worldScale;
        float maxX = minX + worldScale;
        float maxZ = minZ + worldScale;

        // Resolve the 5 heights. Respect stored data when present (load path);
        // otherwise generate deterministically from the world seed (first load).
        float[] h = new float[ChunkData.VertexCount];
        for (int i = 0; i < ChunkData.CornerCount; i++)
        {
            h[i] = (data.IsValid && data.Heights != null)
                ? data.Heights[i]
                : SampleCornerHeight(data, i, layers);
        }
        h[ChunkData.CenterIndex] = ResolveCenterHeight(data, h);

        // Vertex positions: slot layout 0=NW,1=NE,2=SE,3=SW,4=Center.
        Vector3[] vertices =
        {
            new Vector3(minX, h[0], maxZ), // 0 = NW
            new Vector3(maxX, h[1], maxZ), // 1 = NE
            new Vector3(maxX, h[2], minZ), // 2 = SE
            new Vector3(minX, h[3], minZ), // 3 = SW
            new Vector3(data.CenterWorldX, h[4], data.CenterWorldZ), // 4 = Center
        };

        // Triangles face UP. Verified winding (left-handed system, RecalculateNormals):
        //   flat XZ, normal must point +Y. The two triangles whose trailing edge
        //   runs toward -Z are wound so their surface normal still points up.
        int[] triangles =
        {
            // T1: NW, NE, Center -> +Y
            0, 1, 4,
            // T2: NE, SE, Center -> +Y
            1, 2, 4,
            // T3: SW, Center, SE -> +Y
            3, 4, 2,
            // T4: NW, Center, SW -> +Y
            0, 4, 3,
        };

        // UVs: 0,0 at SW up to 1,1 at NE; center at 0.5,0.5.
        Vector2[] uv =
        {
            new Vector2(0f, 1f), // NW
            new Vector2(1f, 1f), // NE
            new Vector2(1f, 0f), // SE
            new Vector2(0f, 0f), // SW
            new Vector2(0.5f, 0.5f), // Center
        };

        Mesh mesh = new Mesh
        {
            name = $"ChunkMesh_{data.ChunkX}_{data.ChunkZ}"
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
    /// Center height: the average of the 4 corners plus a deterministic random
    /// pivot offset (per chunk) — the "random angle pivot" terraform look.
    /// A stored value is honoured on the load path.
    /// </summary>
    private static float ResolveCenterHeight(ChunkData data, float[] cornerHeights)
    {
        if (data.IsValid && data.Heights != null)
            return data.Heights[ChunkData.CenterIndex];

        float sum = 0f;
        for (int i = 0; i < ChunkData.CornerCount; i++)
            sum += cornerHeights[i];
        float centerBase = sum * 0.25f;

        System.Random rng = new System.Random(
            data.Seed.GetHashCode() ^ (data.ChunkX * 73856093) ^ (data.ChunkZ * 19349663));
        float pivot = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.35f;
        return centerBase + pivot;
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
