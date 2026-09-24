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
    /// <summary>Final mesh-safety clamp band (matches WorldStreamer's height sanitization):
    /// a non-finite or absurd height must never reach a chunk mesh (and from there a
    /// MeshCollider whose corrupted bounds break the physics broadphase).</summary>
    public const float MaxTerrainHeight = 200f;

    // Strata bands (dig depth below the pristine noise surface, in metres):
    //   depth <=        DirtBandStart -> grass (untouched surface, raised terrain too)
    //   DirtBandStart .. DirtBandEnd  -> grass->dirt blend
    //   DirtBandEnd .. StoneBandStart -> dirt
    //   StoneBandStart .. StoneBandEnd-> dirt->stone blend
    //   depth >=        StoneBandEnd  -> stone (deep excavation)
    public const float DirtBandStart = 0.35f;
    public const float DirtBandEnd = 0.65f;
    public const float StoneBandStart = 2.3f;
    public const float StoneBandEnd = 2.7f;

    /// <summary>
    /// Per-vertex surface color by dig depth below the pristine noise surface at the corner's
    /// world coordinate (see the strata band constants above). Pure math + deterministic noise,
    /// so it is thread-safe and needs NO saved state: the reference surface is recomputed from
    /// the seed, and a saved corner's own height is what it is compared against. Raised terrain
    /// (depth &lt;= 0) and untouched ground (depth ~ 0) render pure grass. Blends are small so
    /// layers read as discrete strata.
    /// </summary>
    public static Color TerrainBandColor(long seed, int worldX, int worldZ, float vertexY)
    {
        return BandColor(vertexY, TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ));
    }

    /// <summary>
    /// Band-color overload that reuses a chunk build's height memo (1dt): TerrainBandColor
    /// resampled the 5-octave pristine surface once per vertex (~3,600 GetHeight calls per
    /// chunk); with the memo the 31×31 world corners do the sampling once (~961 calls) and every
    /// vertex reads the same deterministic value back — identical colors, ~3.7× fewer Perlin
    /// evaluations on the background build. The memo is local to one chunk build (thread-confined).
    /// </summary>
    public static Color TerrainBandColor(long seed, int worldX, int worldZ, float vertexY,
        System.Collections.Generic.Dictionary<long, float> heightMemo)
    {
        return BandColor(vertexY, MemoizedHeight(seed, worldX, worldZ, heightMemo));
    }

    private static Color BandColor(float vertexY, float surfaceHeight)
    {
        float depth = surfaceHeight - vertexY;
        if (depth <= DirtBandStart)
            return ColorPalette.GrassGreen;
        if (depth < DirtBandEnd)
            return Color.Lerp(ColorPalette.GrassGreen, ColorPalette.DirtBrown,
                (depth - DirtBandStart) / (DirtBandEnd - DirtBandStart));
        if (depth <= StoneBandStart)
            return ColorPalette.DirtBrown;
        if (depth < StoneBandEnd)
            return Color.Lerp(ColorPalette.DirtBrown, ColorPalette.StoneGray,
                (depth - StoneBandStart) / (StoneBandEnd - StoneBandStart));
        return ColorPalette.StoneGray;
    }

    /// <summary>Clamps/normalizes a height value for mesh safety. Pure math, thread-safe.</summary>
    public static float SanitizeHeight(float h)
    {
        if (!float.IsFinite(h))
            return 0f;
        return Mathf.Clamp(h, -MaxTerrainHeight, MaxTerrainHeight);
    }

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
            h[i] = SanitizeHeight((data.IsValid && data.Heights != null)
                ? data.Heights[i]
                : SampleCornerHeight(data, i, layers));
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
    /// Deterministic noise-backed tile data for a world tile (defensive fill). Used when a loaded
    /// chunk's tile bookkeeping is momentarily incomplete (unload/reload races at the streaming
    /// edge / arena-lane rebuild): the tile's quad is then emitted with the SAME pure-noise corner
    /// heights the pristine corner grid would produce — never a hole in the merged mesh, never a
    /// null slot that crashes a patch.
    /// </summary>
    public static ChunkData BuildFallbackTileData(int worldX, int worldZ, long seed)
    {
        var data = new ChunkData(worldX, worldZ, seed);
        data.Heights[0] = SanitizeHeight(TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ + 1));     // NW
        data.Heights[1] = SanitizeHeight(TerrainNoiseGenerator.GetHeight(seed, worldX + 1, worldZ + 1)); // NE
        data.Heights[2] = SanitizeHeight(TerrainNoiseGenerator.GetHeight(seed, worldX + 1, worldZ));     // SE
        data.Heights[3] = SanitizeHeight(TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ));         // SW
        data.Version = 1;
        data.HasModifications = false;
        return data;
    }

    /// <summary>Chunk-local mesh data for a fallback tile at local <paramref name="lx"/>/<paramref name="lz"/>
    /// of a chunk whose origin tile is at world <paramref name="origin"/> (pure-noise corners).</summary>
    private static ChunkMeshData BuildFallbackTile(ChunkCoord origin, int lx, int lz, long seed)
    {
        int worldX = origin.X + lx;
        int worldZ = origin.Z + lz;
        return BuildMeshData(BuildFallbackTileData(worldX, worldZ, seed), TerrainNoiseGenerator.DefaultLayers);
    }

    /// <summary>
    /// True when a tile's 4 corner heights are (near-)equal — a flat-top block. Only LEGACY 1cg
    /// deformation wrote tiles flat, so flatness is derivable from the heights alone (no extra
    /// persisted field); those saved tiles are the ones that must render with vertical side walls
    /// instead of stretched quads. (1cj smooth deforms never produce a flat block — corners stay
    /// equal with neighbours — so they emit no walls.)
    /// </summary>
    public static bool IsFlatTile(ChunkData data)
    {
        if (!data.IsValid) return false;
        float h = data.Heights[0];
        for (int i = 1; i < ChunkData.VertexCount; i++)
        {
            if (Mathf.Abs(data.Heights[i] - h) > 0.001f)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Merges the per-tile mesh arrays of a terrain chunk into one thread-safe chunk-local mesh
    /// (ONE GameObject + ONE collider per chunk). Runs on the background thread; no Unity API
    /// objects are touched. Per-tile top-quad UVs/normals are preserved unchanged.
    /// The buffer layout is TOPS-FIRST: the 4 * tileCount top-quad vertices occupy a contiguous
    /// block, then all vertical side-wall vertices follow. ChunkObject.PatchRegion exploits the
    /// fixed top layout ((lz * cs + lx) * 4) to re-skin just one region after a height edit, so the
    /// two groups must never interleave.
    ///
    /// On top of the top-surface quads, wherever a height discontinuity sits between two
    /// neighbouring tiles (only legacy flat-slab tiles, whole-metre or older fractional carves),
    /// the higher
    /// tile emits vertical side walls down to the lower tile, subdivided one horizontal band per
    /// metre so each band reads as a single stackable terrace step. Each wall is emitted exactly
    /// once (by
    /// the higher tile), so a vertical drop is never double-rendered, and touched smooth-smooth
    /// edges (shared corners equal) add no geometry at all.
    /// <paramref name="border"/> maps world corner coords ((x &lt;&lt; 32) | z) that lie one tile
    /// OUTSIDE the chunk to their current heights, so seam walls against another chunk use that
    /// chunk's real heights; missing corners fall back to the same deterministic world noise the
    /// pristine corner grid uses (no phantom walls on untouched seams).
    /// </summary>
    public static MergedChunkMeshData BuildMergedMeshData(ChunkMeshData[] tiles,
        System.Collections.Generic.IReadOnlyDictionary<long, float> border = null, long seed = 0)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int tileCount = cs * cs;
        int topVertsPerTile = ChunkData.VertexCount;      // 4
        int topTrisPerTile = ChunkData.TriangleCount * 3; // 6

        // Defensive fill: a null tile (partial chunk bookkeeping under unload/reload races) MUST
        // still emit its quad, or the merged mesh gets a literal hole in it — a fall-through the
        // player can hit. Substitute the same deterministic noise heights the pristine corner grid
        // uses; never silence the tile.
        ChunkCoord fillOrigin = default;
        for (int i = 0; i < tileCount; i++)
        {
            if (tiles[i].Vertices != null)
            {
                fillOrigin = tiles[i].Coord;
                break;
            }
        }
        {
            var resolved = new ChunkMeshData[tileCount];
            for (int i = 0; i < tileCount; i++)
            {
                ChunkMeshData t = tiles[i];
                if (t.Vertices == null)
                    t = BuildFallbackTile(fillOrigin, i % cs, i / cs, seed);
                resolved[i] = t;
            }
            tiles = resolved;
        }

        // Pass 1 — count the side-wall bands (4 verts + 6 tris each) so the arrays fit exactly.
        // Per-build pristine-height memo (1dt): the band colors and the out-of-chunk seam corners
        // share the 31×31 grid of 5-octave samples instead of re-sampling the noise per vertex.
        var heightMemo = new System.Collections.Generic.Dictionary<long, float>(
            TerrainChunkCoord.CornerGridSize * TerrainChunkCoord.CornerGridSize);
        int wallBands = 0;
        for (int i = 0; i < tileCount; i++)
        {
            ChunkMeshData tile = tiles[i];
            if (tile.Vertices == null)
                continue;
            int lx = i % cs;
            int lz = i / cs;
            for (int e = 0; e < 4; e++)
            {
                if (!EdgeIsRaised(tile, lx, lz, e, tiles, cs, border, seed, heightMemo))
                    continue;
                EdgeHeights(tile, lx, lz, e, tiles, cs, border, seed, heightMemo,
                    out float topA, out float topB, out float botA, out float botB);
                wallBands += SideBandCount(topA, topB, botA, botB);
            }
        }

        int vertCount = tileCount * topVertsPerTile + wallBands * 4;
        int triCount = tileCount * topTrisPerTile + wallBands * 6;

        Vector3[] vertices = new Vector3[vertCount];
        int[] triangles = new int[triCount];
        Vector2[] uv = new Vector2[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Color[] colors = new Color[vertCount];

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        int vertex = 0;
        int tri = 0;

        // Pass 2 — emit every tile's top quad first. All 4 * tileCount top vertices stay in one
        // contiguous block so PatchRegion's fixed quad offsets ((lz * cs + lx) * 4) are always
        // valid, even when a chunk mixes flat slab tiles (side walls) with smooth deforms.
        for (int i = 0; i < tileCount; i++)
        {
            ChunkMeshData tile = tiles[i];
            int localX = i % cs;
            int localZ = i / cs;
            Vector3 offset = new Vector3(localX * ChunkData.Size, 0f, localZ * ChunkData.Size);

            if (tile.Vertices == null)
                continue;

            for (int k = 0; k < topVertsPerTile; k++)
            {
                Vector3 p = tile.Vertices[k] + offset;
                p.y = SanitizeHeight(p.y);
                int v = vertex + k;
                vertices[v] = p;
                uv[v] = k < tile.UV.Length ? tile.UV[k] : Vector2.zero;
                normals[v] = k < tile.Normals.Length ? tile.Normals[k] : Vector3.up;
                int wx, wz;
                switch (k)
                {
                    case 0: wx = tile.Coord.X; wz = tile.Coord.Z + 1; break;      // NW
                    case 1: wx = tile.Coord.X + 1; wz = tile.Coord.Z + 1; break;  // NE
                    case 2: wx = tile.Coord.X + 1; wz = tile.Coord.Z; break;      // SE
                    default: wx = tile.Coord.X; wz = tile.Coord.Z; break;         // SW
                }
                colors[v] = TerrainBandColor(seed, wx, wz, p.y, heightMemo);
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            for (int k = 0; k < tile.Triangles.Length; k++)
                triangles[tri++] = tile.Triangles[k] + vertex;

            vertex += topVertsPerTile;
        }

        // Pass 3 — emit the vertical side walls after every top quad so the 4 * tileCount top
        // vertices stay one contiguous block (PatchRegion's fixed quad offsets stay valid even
        // when walls exist). Same iteration and per-edge order as Pass 1 keeps counts aligned.
        for (int i = 0; i < tileCount; i++)
        {
            ChunkMeshData tile = tiles[i];
            if (tile.Vertices == null)
                continue;
            int localX = i % cs;
            int localZ = i / cs;

            // --- Vertical side walls where this tile is taller than its neighbour ---
            for (int e = 0; e < 4; e++)
            {
                if (!EdgeIsRaised(tile, localX, localZ, e, tiles, cs, border, seed, heightMemo))
                    continue;

                EdgeHeights(tile, localX, localZ, e, tiles, cs, border, seed, heightMemo,
                    out float topA, out float topB, out float botA, out float botB);
                EdgeEnds(localX, localZ, e, out int ex0, out int ez0, out int ex1, out int ez1);
                int bands = SideBandCount(topA, topB, botA, botB);
                Vector3 outward = EdgeOutward(e);

                for (int b = 0; b < bands; b++)
                {
                    float f0 = (float)b / bands;
                    float f1 = (float)(b + 1) / bands;

                    Vector3 p0 = new Vector3(ex0, Mathf.Lerp(topA, botA, f0), ez0);
                    Vector3 p1 = new Vector3(ex1, Mathf.Lerp(topB, botB, f0), ez1);
                    Vector3 p2 = new Vector3(ex1, Mathf.Lerp(topB, botB, f1), ez1);
                    Vector3 p3 = new Vector3(ex0, Mathf.Lerp(topA, botA, f1), ez0);
                    p0.y = SanitizeHeight(p0.y);
                    p1.y = SanitizeHeight(p1.y);
                    p2.y = SanitizeHeight(p2.y);
                    p3.y = SanitizeHeight(p3.y);

                    int iv0 = vertex, iv1 = vertex + 1, iv2 = vertex + 2, iv3 = vertex + 3;
                    vertices[iv0] = p0;
                    vertices[iv1] = p1;
                    vertices[iv2] = p2;
                    vertices[iv3] = p3;

                    // Strata colors by the world corner each wall vertex stands at (chunk-local
                    // coords + the chunk's min-tile world coord). A tall drop therefore shows the
                    // grass rim then dirt, fading into stone as the wall descends.
                    ChunkCoord origin = tiles.Length > 0 ? tiles[0].Coord : new ChunkCoord(0, 0);
                    colors[iv0] = TerrainBandColor(seed, origin.X + ex0, origin.Z + ez0, p0.y, heightMemo);
                    colors[iv1] = TerrainBandColor(seed, origin.X + ex1, origin.Z + ez1, p1.y, heightMemo);
                    colors[iv2] = TerrainBandColor(seed, origin.X + ex1, origin.Z + ez1, p2.y, heightMemo);
                    colors[iv3] = TerrainBandColor(seed, origin.X + ex0, origin.Z + ez0, p3.y, heightMemo);

                    // U tiles across the 1 m edge; V tiles once per band so each slab face
                    // shows one full texture repeat (the stacked-slab read).
                    uv[iv0] = new Vector2(0f, b);
                    uv[iv1] = new Vector2(1f, b);
                    uv[iv2] = new Vector2(1f, b + 1f);
                    uv[iv3] = new Vector2(0f, b + 1f);

                    // Flat-facing side normal, oriented to look outward.
                    Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                    if (n.sqrMagnitude > 1e-10f)
                        n = n.normalized;
                    else
                        n = outward;
                    if (Vector3.Dot(n, outward) < 0f)
                        n = -n;
                    normals[iv0] = n;
                    normals[iv1] = n;
                    normals[iv2] = n;
                    normals[iv3] = n;

                    bool ccw = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) >= 0f;
                    if (ccw)
                    {
                        triangles[tri++] = iv0; triangles[tri++] = iv1; triangles[tri++] = iv2;
                        triangles[tri++] = iv0; triangles[tri++] = iv2; triangles[tri++] = iv3;
                    }
                    else
                    {
                        triangles[tri++] = iv0; triangles[tri++] = iv2; triangles[tri++] = iv1;
                        triangles[tri++] = iv0; triangles[tri++] = iv3; triangles[tri++] = iv2;
                    }

                    if (p0.y < minY) minY = p0.y;
                    if (p0.y > maxY) maxY = p0.y;
                    if (p1.y < minY) minY = p1.y;
                    if (p1.y > maxY) maxY = p1.y;
                    if (p2.y < minY) minY = p2.y;
                    if (p2.y > maxY) maxY = p2.y;
                    if (p3.y < minY) minY = p3.y;
                    if (p3.y > maxY) maxY = p3.y;

                    vertex += 4;
                }
            }
        }

        float span = cs * ChunkData.Size;
        Bounds bounds = new Bounds(
            new Vector3(span * 0.5f, minY < maxY ? (minY + maxY) * 0.5f : minY, span * 0.5f),
            new Vector3(span, Mathf.Max(0.1f, minY < maxY ? (maxY - minY) + 0.1f : 0.1f), span));

        return new MergedChunkMeshData
        {
            Vertices = vertices,
            Triangles = triangles,
            UV = uv,
            Normals = normals,
            Colors = colors,
            Bounds = bounds,
        };
    }

    /// <summary>Number of 1 m horizontal bands for a side wall between a high edge and a low edge.</summary>
    private static int SideBandCount(float topA, float topB, float botA, float botB)
    {
        float drop = Mathf.Max(topA, topB) - Mathf.Min(botA, botB);
        return Mathf.Clamp(Mathf.CeilToInt(drop), 1, 256);
    }

    /// <summary>The two edge-corner heights of this tile plus its neighbour's two matching
    /// heights on the shared edge. Neighbour data is read from the in-chunk <paramref name="tiles"/>
    /// array when present, else from the <paramref name="border"/> corner map, else from the same
    /// deterministic world noise the pristine corner grid uses (memoized per build, 1dt).</summary>
    private static void EdgeHeights(ChunkMeshData tile, int lx, int lz, int edge,
        ChunkMeshData[] tiles, int cs,
        System.Collections.Generic.IReadOnlyDictionary<long, float> border, long seed,
        System.Collections.Generic.Dictionary<long, float> heightMemo,
        out float thisA, out float thisB, out float nbrA, out float nbrB)
    {
        float[] h = tile.Data.Heights;
        switch (edge)
        {
            case 0: thisA = h[0]; thisB = h[1]; break; // North: NW, NE
            case 1: thisA = h[1]; thisB = h[2]; break; // East:  NE, SE
            case 2: thisA = h[3]; thisB = h[2]; break; // South: SW, SE
            default: thisA = h[0]; thisB = h[3]; break; // West:  NW, SW
        }

        int nlx = lx, nlz = lz, sA = 0, sB = 0;
        switch (edge)
        {
            case 0: nlz = lz + 1; sA = 3; sB = 2; break; // neighbour South = its SW, SE
            case 1: nlx = lx + 1; sA = 0; sB = 3; break; // neighbour West  = its NW, SW
            case 2: nlz = lz - 1; sA = 0; sB = 1; break; // neighbour North = its NW, NE
            default: nlx = lx - 1; sA = 1; sB = 2; break; // neighbour East  = its NE, SE
        }

        if (nlx >= 0 && nlx < cs && nlz >= 0 && nlz < cs)
        {
            ChunkMeshData nbr = tiles[nlz * cs + nlx];
            if (nbr.Data.IsValid)
            {
                nbrA = nbr.Data.Heights[sA];
                nbrB = nbr.Data.Heights[sB];
                return;
            }
        }

        // Neighbour outside this chunk (or malformed): its shared corners are the SAME world
        // corner positions as this tile's edge corners, so key the border/noise lookup by those.
        ChunkCoord origin = tiles.Length > 0 ? tiles[0].Coord : new ChunkCoord(0, 0);
        int x0 = origin.X + lx, z0 = origin.Z + lz;
        int wxA, wzA, wxB, wzB;
        switch (edge)
        {
            case 0: wxA = x0; wzA = z0 + 1; wxB = x0 + 1; wzB = z0 + 1; break;
            case 1: wxA = x0 + 1; wzA = z0 + 1; wxB = x0 + 1; wzB = z0; break;
            case 2: wxA = x0; wzA = z0; wxB = x0 + 1; wzB = z0; break;
            default: wxA = x0; wzA = z0 + 1; wxB = x0; wzB = z0; break;
        }

        nbrA = CornerHeight(wxA, wzA, border, seed, heightMemo);
        nbrB = CornerHeight(wxB, wzB, border, seed, heightMemo);
    }

    /// <summary>True when this tile is the higher owner of the shared edge — the side that must
    /// render the wall. Pristine smooth-smooth edges have identical corners, so they never raise.</summary>
    private static bool EdgeIsRaised(ChunkMeshData tile, int lx, int lz, int edge,
        ChunkMeshData[] tiles, int cs,
        System.Collections.Generic.IReadOnlyDictionary<long, float> border, long seed,
        System.Collections.Generic.Dictionary<long, float> heightMemo)
    {
        if (!tile.Data.IsValid)
            return false;
        EdgeHeights(tile, lx, lz, edge, tiles, cs, border, seed, heightMemo,
            out float topA, out float topB, out float nbrA, out float nbrB);
        return Mathf.Max(topA, topB) > Mathf.Max(nbrA, nbrB) + 0.001f;
    }

    /// <summary>Height of a world corner from the border map (current neighbour-chunk height),
    /// else the deterministic world noise at that exact corner (memoized per build, 1dt).</summary>
    private static float CornerHeight(int wx, int wz,
        System.Collections.Generic.IReadOnlyDictionary<long, float> border, long seed,
        System.Collections.Generic.Dictionary<long, float> heightMemo)
    {
        if (border != null && border.TryGetValue(((long)wx << 32) | (uint)wz, out float h))
            return h;
        return MemoizedHeight(seed, wx, wz, heightMemo);
    }

    /// <summary>
    /// Deterministic pristine height for a world corner, cached per chunk build (1dt). Corner
    /// heights are pure functions of (seed, x, z), so a chunk-local memo is exact — the merged
    /// builder's 3,600 band-color vertex samples collapse onto the 961 corner grid's samples.
    /// Local to one build call; thread-confined by construction.
    /// </summary>
    private static float MemoizedHeight(long seed, int wx, int wz,
        System.Collections.Generic.Dictionary<long, float> memo)
    {
        long key = ((long)wx << 32) | (uint)wz;
        if (memo.TryGetValue(key, out float h))
            return h;
        h = TerrainNoiseGenerator.GetHeight(seed, wx, wz);
        memo.Add(key, h);
        return h;
    }

    /// <summary>Chunk-local end points of a tile's shared edge (start, then end along the edge).</summary>
    private static void EdgeEnds(int lx, int lz, int edge, out int x0, out int z0, out int x1, out int z1)
    {
        switch (edge)
        {
            case 0: x0 = lx; z0 = lz + 1; x1 = lx + 1; z1 = lz + 1; break; // North
            case 1: x0 = lx + 1; z0 = lz + 1; x1 = lx + 1; z1 = lz; break; // East
            case 2: x0 = lx; z0 = lz; x1 = lx + 1; z1 = lz; break;          // South
            default: x0 = lx; z0 = lz + 1; x1 = lx; z1 = lz; break;         // West
        }
    }

    /// <summary>Horizontal direction a wall must face (away from the tile centre).</summary>
    private static Vector3 EdgeOutward(int edge)
    {
        switch (edge)
        {
            case 0: return Vector3.forward;  // North +z
            case 1: return Vector3.right;    // East  +x
            case 2: return Vector3.back;     // South -z
            default: return Vector3.left;    // West  -x
        }
    }

    /// <summary>
    /// Creates a Unity Mesh from the pre-built merged chunk arrays. Main thread only.
    /// Uses SetVertices/SetTriangles/SetNormals/SetUVs then one UploadMeshData pass so
    /// the upload happens once instead of five sequential dirty-setter passes.
    /// </summary>
    public static Mesh CreateMeshFromMerged(MergedChunkMeshData md, string meshName)
    {
        Mesh mesh = AcquireChunkMesh(meshName);
        UploadMerged(md, mesh);
        return mesh;
    }

    /// <summary>Cap on pooled chunk Mesh objects (1dv) — bounds the GPU memory held by the
    /// freed-mesh pool; anything beyond the cap is destroyed outright on release.</summary>
    private const int PooledChunkMeshCap = 48;

    private static readonly System.Collections.Generic.Queue<Mesh> _chunkMeshPool =
        new System.Collections.Generic.Queue<Mesh>();

    /// <summary>
    /// Returns a cached chunk Mesh when one is free (a released chunk's buffer, same uniform
    /// ~961-vert / ~1800-tri size), else a fresh Mesh (1dv). Main thread only — chunk meshes are
    /// created/uploaded on the main thread by this project's convention.
    /// </summary>
    public static Mesh AcquireChunkMesh(string meshName)
    {
        Mesh mesh = _chunkMeshPool.Count > 0 ? _chunkMeshPool.Dequeue() : new Mesh();
        mesh.name = meshName;
        return mesh;
    }

    /// <summary>
    /// Returns a chunk Mesh to the freed-mesh pool (capped, 1dv), or destroys it when the pool is
    /// full. Overwrite-only reuse is safe: every merged chunk uploads via <see cref="UploadMerged"/>,
    /// which fully re-specifies vertices/indices and <see cref="Mesh.Clear"/>s upfront whenever the
    /// vertex count changed (Unity buffers never shrink through the setters), so a stale buffer is
    /// never partially referenced — it only ever grows in place, and a smaller re-upload is reset
    /// fresh instead of hitting an out-of-bounds channel write.
    /// </summary>
    public static void ReleaseChunkMesh(Mesh mesh)
    {
        if (mesh == null)
            return;
        if (_chunkMeshPool.Count >= PooledChunkMeshCap)
        {
            Object.Destroy(mesh);
            return;
        }
        _chunkMeshPool.Enqueue(mesh);
    }

    /// <summary>
    /// Uploads the merged chunk arrays into a Mesh in one pass (trimmed setter sequence + a single
    /// UploadMeshData). Works identically for a fresh Mesh and for a pooled one being re-uploaded in
    /// place (1dv), so rebuilds never allocate a new Mesh object. <paramref name="markNoLongerReadable"/>
    /// (1es) passes straight through to UploadMeshData: far-shell cells set it true — they never
    /// deform, so the CPU-side copy is freed right after upload and the pooled buffer re-specifies
    /// fully on its next reuse. Real chunks keep the default false (deformation reads/modifies arrays).
    /// </summary>
    public static void UploadMerged(MergedChunkMeshData md, Mesh mesh, bool markNoLongerReadable = false)
    {
        // Pooled reuse (1dv fix): Unity Mesh buffers only ever GROW through the typed setter
        // APIs — a pooled mesh whose last upload held MORE vertices (e.g. a slab chunk with side
        // walls) keeps that larger buffer, so a smaller re-upload fails the SetNormals/SetUVs/
        // SetColors size check against the retained vertex count. Clear() resets all channel
        // buffers to zero so the setters below grow them fresh to md's size. Skipped on the hot
        // path (identical counts → no clear), so consecutive same-size rebuilds stay allocation-free.
        if (mesh.vertexCount != md.Vertices.Length)
            mesh.Clear();
        mesh.SetVertices(md.Vertices);
        mesh.SetTriangles(md.Triangles, 0);
        mesh.SetNormals(md.Normals);
        mesh.SetUVs(0, md.UV);
        if (md.Colors != null)
            mesh.SetColors(md.Colors);
        mesh.bounds = md.Bounds;
        mesh.UploadMeshData(markNoLongerReadable);
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
