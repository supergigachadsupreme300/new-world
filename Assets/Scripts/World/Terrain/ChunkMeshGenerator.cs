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
    /// Builds pure C# arrays for the mesh — safe to call from a background thread.
    /// No Unity API types are allocated; only arrays and a Bounds struct.
    /// </summary>
    public static ChunkMeshData BuildMeshData(ChunkData data, NoiseLayerConfig[] layers = null)
    {
        float worldScale = ChunkData.Size;

        float minX = 0f;
        float minZ = 0f;
        float maxX = worldScale;
        float maxZ = worldScale;

        float[] h = new float[ChunkData.VertexCount];
        for (int i = 0; i < ChunkData.CornerCount; i++)
        {
            h[i] = (data.IsValid && data.Heights != null)
                ? data.Heights[i]
                : SampleCornerHeight(data, i, layers);
        }

        Vector3[] vertices =
        {
            new Vector3(minX, h[0], maxZ),
            new Vector3(maxX, h[1], maxZ),
            new Vector3(maxX, h[2], minZ),
            new Vector3(minX, h[3], minZ),
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3,
        };

        Vector2[] uv =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
        };

        Vector3[] normals = new Vector3[4];
        Vector3 a = vertices[1] - vertices[0];
        Vector3 b = vertices[2] - vertices[0];
        Vector3 n = Vector3.Cross(a, b).normalized;
        for (int i = 0; i < 4; i++)
            normals[i] = n;

        Bounds bounds = new Bounds(
            new Vector3(minX + (maxX - minX) * 0.5f, (h[0] + h[1] + h[2] + h[3]) * 0.25f, minZ + (maxZ - minZ) * 0.5f),
            new Vector3(maxX - minX, Mathf.Max(h) - Mathf.Min(h) + 0.1f, maxZ - minZ));

        return new ChunkMeshData
        {
            Coord = new ChunkCoord(data.ChunkX, data.ChunkZ),
            Data = data,
            Vertices = vertices,
            Triangles = triangles,
            UV = uv,
            Normals = normals,
            Bounds = bounds,
        };
    }

    /// <summary>
    /// Builds the Unity Mesh on the main thread. Wraps BuildMeshData() and
    /// creates the GPU-side Mesh object from the arrays.
    /// </summary>
    public static Mesh BuildMesh(ChunkData data, NoiseLayerConfig[] layers = null)
    {
        ChunkMeshData md = BuildMeshData(data, layers);

        Mesh mesh = new Mesh
        {
            name = $"TileMesh_{data.ChunkX}_{data.ChunkZ}"
        };
        mesh.Clear();
        mesh.vertices = md.Vertices;
        mesh.uv = md.UV;
        mesh.triangles = md.Triangles;
        mesh.normals = md.Normals;
        mesh.bounds = md.Bounds;
        return mesh;
    }

    /// <summary>
    /// Creates a Unity Mesh from pre-built thread-safe arrays. Main thread only.
    /// </summary>
    public static Mesh CreateMeshFromData(ChunkMeshData md)
    {
        Mesh mesh = new Mesh
        {
            name = $"TileMesh_{md.Data.ChunkX}_{md.Data.ChunkZ}"
        };
        mesh.Clear();
        mesh.vertices = md.Vertices;
        mesh.uv = md.UV;
        mesh.triangles = md.Triangles;
        mesh.normals = md.Normals;
        mesh.bounds = md.Bounds;
        return mesh;
    }

    /// <summary>
    /// Merges the 900 per-tile mesh arrays of a terrain chunk into one thread-safe
    /// chunk-local mesh (1800 triangles -> ONE GameObject + ONE collider per chunk).
    /// Runs on the background thread; no Unity API objects are touched.
    /// Per-tile UVs/normals are preserved unchanged, so visuals match the old
    /// one-GameObject-per-tile setup exactly.
    /// </summary>
    public static MergedChunkMeshData BuildMergedMeshData(ChunkMeshData[] tiles)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int tileCount = cs * cs;
        int vertCount = tileCount * 4;
        int triCount = tileCount * 6;

        Vector3[] vertices = new Vector3[vertCount];
        int[] triangles = new int[triCount];
        Vector2[] uv = new Vector2[vertCount];
        Vector3[] normals = new Vector3[vertCount];

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        int baseIndex = 0;
        int tri = 0;
        for (int i = 0; i < tileCount; i++)
        {
            ChunkMeshData tile = tiles[i];
            int localX = i % cs;
            int localZ = i / cs;
            Vector3 offset = new Vector3(localX * ChunkData.Size, 0f, localZ * ChunkData.Size);

            for (int k = 0; k < 4; k++)
            {
                Vector3 p = tile.Vertices[k] + offset;
                int v = baseIndex + k;
                vertices[v] = p;
                uv[v] = k < tile.UV.Length ? tile.UV[k] : Vector2.zero;
                normals[v] = k < tile.Normals.Length ? tile.Normals[k] : Vector3.up;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            for (int k = 0; k < tile.Triangles.Length; k++)
                triangles[tri++] = tile.Triangles[k] + baseIndex;

            baseIndex += 4;
        }

        float span = cs * ChunkData.Size;
        Bounds bounds = new Bounds(
            new Vector3(span * 0.5f, (minY + maxY) * 0.5f, span * 0.5f),
            new Vector3(span, Mathf.Max(0.1f, (maxY - minY) + 0.1f), span));

        return new MergedChunkMeshData
        {
            Vertices = vertices,
            Triangles = triangles,
            UV = uv,
            Normals = normals,
            Bounds = bounds,
        };
    }

    /// <summary>
    /// Creates a Unity Mesh from the pre-built merged chunk arrays. Main thread only.
    /// </summary>
    public static Mesh CreateMeshFromMerged(MergedChunkMeshData md, string meshName)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.Clear();
        mesh.vertices = md.Vertices;
        mesh.uv = md.UV;
        mesh.triangles = md.Triangles;
        mesh.normals = md.Normals;
        mesh.bounds = md.Bounds;
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
            case 0: x = minX; z = maxZ; break;
            case 1: x = maxX; z = maxZ; break;
            case 2: x = maxX; z = minZ; break;
            case 3: x = minX; z = minZ; break;
        }

        return TerrainNoiseGenerator.GetHeight(data.Seed, x, z, layers, 0f);
    }
}
