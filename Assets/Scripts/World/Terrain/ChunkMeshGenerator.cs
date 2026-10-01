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
/// 1ew: a tile whose 4 corner heights differ by more than the refine threshold renders instead as
/// a 2x2 sub-quad grid (bilinear interior heights) so stretched faces split into several smaller
/// faces — same smooth height field, but steep slopes become a cluster of small editable faces
/// instead of one stretched membrane.
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

    // --- 1ew: adaptive stretch-split refinement ---
    //
    // When adjacent corners of a 1x1 tile differ by more than this many metres the single quad is
    // one huge stretched membrane the editor cannot bite into (it can only push the 4 corners).
    // A tile whose max corner delta exceeds the threshold renders as a 2x2 sub-quad grid (16
    // vertices / 8 triangles) whose interior heights are bilinear interpolations of the 4 coarse
    // corners — the split faces are exactly on the coarse surface (edge midpoints are linear in
    // the shared corners, so a refined tile meets its coarse neighbours with ZERO cracks) and the
    // world stays a smooth height field, never steps. Refinement is DERIVED from the corners in
    // 1ew (never stored): persistence, collision and the coarse corners are identical either way,
    // so pristine chunks still store zero data.
    public const float DefaultRefineThreshold = 2.5f;

    /// <summary>Sub-division factor of one refined tile (2x2 = 4 sub-quads).</summary>
    public const int RefineSubdiv = 2;

    /// <summary>Vertices of a refined tile block: 4 sub-quads x 4 corners = 16.</summary>
    public const int RefinedVertexCount = RefineSubdiv * RefineSubdiv * 4;

    /// <summary>Triangle indices of a refined tile block: 4 sub-quads x 2 triangles x 3.</summary>
    public const int RefinedTriangleIndexCount = RefineSubdiv * RefineSubdiv * 2 * 3;

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
    public static ChunkMeshData BuildMeshData(ChunkData data, NoiseLayerConfig[] layers = null, float refineThreshold = 0f)
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

        // 1ew: adaptive stretch-split. A tile whose corners spread more than the threshold emits the
        // refined 2x2 sub-quad block instead of the single stretched quad. Derived (never stored) and
        // interior-of-chunk only (the 1 m border ring keeps the unchanged cross-chunk contract).
        if (refineThreshold > 0f && IsRefinable(data, h, refineThreshold))
            return BuildRefinedMeshData(data, h);

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
    /// True when a tile should render as the refined 2x2 sub-quad block (1ew): its 4 corners differ
    /// by more than <paramref name="threshold"/> metres AND it is not on the chunk border ring.
    /// The interior-only rule keeps refinement strictly intra-chunk: the 1 m border ring stays the
    /// standard 4-corner quad so the cross-chunk shared-corner contract (and the 1ex fine-edit lattice)
    /// never needs to reason about a fine node duplicated across two chunks.
    /// </summary>
    private static bool IsRefinable(ChunkData data, float[] h, float threshold)
    {
        float minV = h[0], maxV = h[0];
        for (int i = 1; i < h.Length; i++)
        {
            if (h[i] < minV) minV = h[i];
            if (h[i] > maxV) maxV = h[i];
        }
        if (maxV - minV <= threshold)
            return false;

        int cs = TerrainChunkCoord.ChunkSize;
        int gx = data.ChunkX % cs;
        if (gx < 0) gx += cs;
        int gz = data.ChunkZ % cs;
        if (gz < 0) gz += cs;
        return gx > 0 && gx < cs - 1 && gz > 0 && gz < cs - 1;
    }

    /// <summary>
    /// Refined 2x2 sub-quad block for one tile (1ew). The 3x3 fine heights are the bilinear
    /// interpolation of the tile's 4 coarse corners, so every interior/mod-edge point lies exactly
    /// on the coarse bilinear surface — a refined tile grafts onto its coarse neighbours with zero
    /// cracks (edge midpoints are linear in the two shared corners, which is what the neighbour's
    /// straight edge passes through). Each sub-quad keeps the whole-tile 1m UV so texture density
    /// never changes; normals are flat per sub-quad (same style as the coarse quad).
    /// Vertex slots: 4 sub-quads in row-major order (jq=0 south row first), each 4 verts NW/NE/SE/SW.
    /// </summary>
    private static ChunkMeshData BuildRefinedMeshData(ChunkData data, float[] h)
    {
        // Fine lattice: idx = j * 3 + i, i (0..2) = localX 0/0.5/1, j (0..2) = localZ 0/0.5/1.
        // Bilinear over SW h3, SE h2, NW h0, NE h1: h(u,v) = lerp(lerp(h3,h2,u), lerp(h0,h1,u), v).
        float[] fine = new float[3 * 3];
        for (int j = 0; j < 3; j++)
        {
            float v = j * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                float u = i * 0.5f;
                float southToNorth = Mathf.Lerp(Mathf.Lerp(h[3], h[2], u), Mathf.Lerp(h[0], h[1], u), v);
                fine[j * 3 + i] = SanitizeHeight(southToNorth);
            }
        }

        var vertices = new Vector3[RefinedVertexCount];
        var triangles = new int[RefinedTriangleIndexCount];
        var uv = new Vector2[RefinedVertexCount];
        var normals = new Vector3[RefinedVertexCount];

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int jq = 0; jq < RefineSubdiv; jq++)
        {
            for (int iq = 0; iq < RefineSubdiv; iq++)
            {
                int quad = jq * RefineSubdiv + iq;
                int baseV = quad * 4;

                // Sub-quad local corner coords (X rows go south->north; vertex slots NW,NE,SE,SW).
                float lxNw = iq * 0.5f,        lzNw = (jq + 1) * 0.5f;
                float lxNe = (iq + 1) * 0.5f,  lzNe = (jq + 1) * 0.5f;
                float lxSe = (iq + 1) * 0.5f,  lzSe = jq * 0.5f;
                float lxSw = iq * 0.5f,        lzSw = jq * 0.5f;

                // Mapping to the fine lattice: local(lx,lz) -> (i = lx*2, j = lz*2).
                float yNw = fine[(int)(lzNw * 2f) * 3 + (int)(lxNw * 2f)];
                float yNe = fine[(int)(lzNe * 2f) * 3 + (int)(lxNe * 2f)];
                float ySe = fine[(int)(lzSe * 2f) * 3 + (int)(lxSe * 2f)];
                float ySw = fine[(int)(lzSw * 2f) * 3 + (int)(lxSw * 2f)];

                vertices[baseV + 0] = new Vector3(lxNw, yNw, lzNw);
                vertices[baseV + 1] = new Vector3(lxNe, yNe, lzNe);
                vertices[baseV + 2] = new Vector3(lxSe, ySe, lzSe);
                vertices[baseV + 3] = new Vector3(lxSw, ySw, lzSw);

                uv[baseV + 0] = new Vector2(lxNw, 1f - lzNw);
                uv[baseV + 1] = new Vector2(lxNe, 1f - lzNe);
                uv[baseV + 2] = new Vector2(lxSe, 1f - lzSe);
                uv[baseV + 3] = new Vector2(lxSw, 1f - lzSw);

                Vector3 a = vertices[baseV + 1] - vertices[baseV + 0];
                Vector3 b = vertices[baseV + 2] - vertices[baseV + 0];
                Vector3 n = Vector3.Cross(a, b).normalized;
                normals[baseV + 0] = n;
                normals[baseV + 1] = n;
                normals[baseV + 2] = n;
                normals[baseV + 3] = n;

                triangles[baseV + 0] = baseV + 0; triangles[baseV + 1] = baseV + 1; triangles[baseV + 2] = baseV + 2;
                triangles[baseV + 3] = baseV + 0; triangles[baseV + 4] = baseV + 2; triangles[baseV + 5] = baseV + 3;

                for (int k = 0; k < 4; k++)
                {
                    float y = vertices[baseV + k].y;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        Bounds bounds = new Bounds(
            new Vector3(0.5f, minY < maxY ? (minY + maxY) * 0.5f : minY, 0.5f),
            new Vector3(1f, Mathf.Max(0.1f, minY < maxY ? (maxY - minY) + 0.1f : 0.1f), 1f));

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

    /// <summary>True when a tile block is the refined 2x2 variant (1ew). The merged builder and the
    /// editor derive refinement from the emitted vertex count, so the block table and the per-tile
    /// mesh data can never disagree.</summary>
    public static bool IsRefined(ChunkMeshData tile)
    {
        return tile.Vertices != null && tile.Vertices.Length > ChunkData.VertexCount;
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
    /// The buffer layout is TOPS-FIRST: every tile's top block — 4 vertices (coarse quad) or 16
    /// vertices (1ew refined 2x2 block) — occupies a contiguous block in tile order, then all
    /// vertical side-wall vertices follow. <see cref="MergedChunkMeshData.TileVertexBase"/> /
    /// <see cref="MergedChunkMeshData.TileVertexCount"/> record each block's offset + size
    /// (ChunkObject.PatchRegion re-skis just one region through the table, so refined and coarse
    /// tiles can safely mix), and <see cref="MergedChunkMeshData.Corners"/> samples the coarse 31x31
    /// corner lattice for the LOD children, whose fixed per-tile stride would otherwise break.
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
        System.Collections.Generic.IReadOnlyDictionary<long, float> border = null, long seed = 0,
        int lowPolyStep = 0)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int tileCount = cs * cs;

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

        // (1hi.1) LOW-POLY ROOT: when the streamer asks for coarse facets (LowPolyStep > 0) the whole
        // merged mesh IS the decimated lattice surface — flat per-quad facets sampled every
        // `step`-th node of the same 31x31 corner grid the far shell and LOD children share, so the
        // near ring reads as crisp facets and the seam to the far shell is exact by construction.
        // This skips the per-tile top blocks and side walls entirely; the 1 m lattice still tracks
        // every edit (PatchCornerGrid restamps it) and the collider rides the SAME step, so
        // deformation and physics need no special-casing here.
        if (lowPolyStep > 0)
            return BuildLowPolyMerged(tiles, cs, seed, lowPolyStep);

        // Per-tile TOP block table (1ew): refined tiles emit 16 vertices, coarse tiles 4, so the
        // merged mesh is a sequence of variable-size blocks the patch/lod paths index through this
        // table instead of a fixed (tileIndex * 4) stride. Built AFTER the defensive fill so every
        // tile contributes its real vertex/index count.
        int[] tileVertBase = new int[tileCount];
        int[] tileVertCount = new int[tileCount];
        int topVerts = 0;
        int topTriIndices = 0;
        for (int i = 0; i < tileCount; i++)
        {
            ChunkMeshData tile = tiles[i];
            int verts = tile.Vertices != null ? tile.Vertices.Length : ChunkData.VertexCount;
            tileVertBase[i] = topVerts;
            tileVertCount[i] = verts;
            topVerts += verts;
            topTriIndices += tile.Triangles != null ? tile.Triangles.Length : 0;
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

        int vertCount = topVerts + wallBands * 4;
        int triCount = topTriIndices + wallBands * 6;

        Vector3[] vertices = new Vector3[vertCount];
        int[] triangles = new int[triCount];
        Vector2[] uv = new Vector2[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Color[] colors = new Color[vertCount];

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        int vertex = 0;
        int tri = 0;

        // Pass 2 — emit every tile's top block first. All top vertices stay in one contiguous sequence
        // of per-tile blocks (4 or 16 verts each, 1ew), indexed by the table built above, so the
        // two kind of blocks can never interleave with the later side-wall vertices.
        for (int i = 0; i < tileCount; i++)
        {
            ChunkMeshData tile = tiles[i];
            int localX = i % cs;
            int localZ = i / cs;
            Vector3 offset = new Vector3(localX * ChunkData.Size, 0f, localZ * ChunkData.Size);

            if (tile.Vertices == null)
                continue;

            int count = tile.Vertices.Length;
            for (int k = 0; k < count; k++)
            {
                Vector3 p = tile.Vertices[k] + offset;
                p.y = SanitizeHeight(p.y);
                int v = vertex + k;
                vertices[v] = p;
                uv[v] = k < tile.UV.Length ? tile.UV[k] : Vector2.zero;
                normals[v] = k < tile.Normals.Length ? tile.Normals[k] : Vector3.up;
                // Strata by the world corner under the vertex; refined sub-quad corners land on
                // fractional world coords, floored to the same tile cell they stand in.
                float tileLocalX = p.x - offset.x;
                float tileLocalZ = p.z - offset.z;
                int wx = tile.Coord.X + Mathf.FloorToInt(tileLocalX + 0.0001f);
                int wz = tile.Coord.Z + Mathf.FloorToInt(tileLocalZ + 0.0001f);
                colors[v] = TerrainBandColor(seed, wx, wz, p.y, heightMemo);
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            for (int k = 0; k < tile.Triangles.Length; k++)
                triangles[tri++] = tile.Triangles[k] + vertex;

            vertex += count;
        }

        // Coarse 31x31 corner lattice for the LOD children (1ew): the LOD decimation can no longer
        // sample the merged arrays by a fixed per-tile stride (refined blocks break it), so the
        // merged data carries the lattice the children resample. Canonical owner per corner mirrors
        // the old WorldCornerIndex rule, so the LOD surface is identical to the pre-1ew build.
        ChunkCornerGrid corners = BuildCornerGrid(tiles, cs, seed, heightMemo);

        // (1hi) Collider arrays, sampled from the SAME lattice above so the physics surface is
        // seam-proof by construction. Built on this worker thread and carried on the merged data —
        // the main-thread collider upload then cooks a mesh with no side walls and no refined
        // blocks, instead of the full render surface. Re-derivable after deformation via
        // BuildDecimatedCollider over the patch-restamped lattice. Since 1ex this is the full 1 m
        // lattice (961 verts / 1800 tris), so the cook is no longer ~4x cheaper — see
        // ChunkColliderDecimation for why the size claim changed and what the path still buys.
        BuildDecimatedCollider(corners, ChunkColliderDecimation,
            out Vector3[] colliderVertices, out int[] colliderTriangles);

        // Pass 3 — emit the vertical side walls after every top block so the merged shallow vertices
        // stay one contiguous sequence of per-tile blocks (the TileVertexBase table stays valid even
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
            BuildStamp = WorldStreamer.TerrainBuildStamp,
            TileVertexBase = tileVertBase,
            TileVertexCount = tileVertCount,
            Corners = corners,
            ColliderVertices = colliderVertices,
            ColliderTriangles = colliderTriangles,
        };
    }

    /// <summary>
    /// Builds a chunk root from the coarse corner lattice when low-poly facets are enabled (1hi.1):
    /// every <paramref name="step"/>-th node of the 31x31 world-corner grid (step MUST divide
    /// TerrainChunkCoord.ChunkSize so the last sample lands exactly on the chunk border) emits one
    /// flattened quad with a +Y-dominant cross normal — the same facet language as the far shell
    /// (1hi; 3 m again since 1ia), so near and far surfaces read identically and share world corner
    /// nodes across the seam. The 1 m lattice is still canonical (saves, edits, prop heights); only
    /// what is rendered and
    /// cooked for the collider decimates to the step. The patch tables are intentionally null — the
    /// low-poly PatchRegion re-samples this root from the restamped lattice instead of a per-tile
    /// skim. Pure arrays — thread-safe. NOT REACHED by default since 1ia: <c>WorldStreamer.LowPolyFacets</c>
    /// is false, so <c>BuildMergedMeshData</c> takes the full 1 m per-tile path and this is dead until
    /// the flag is turned back on.
    /// </summary>
    private static MergedChunkMeshData BuildLowPolyMerged(ChunkMeshData[] tiles, int cs, long seed,
        int step)
    {
        int grid = TerrainChunkCoord.CornerGridSize;
        var heightMemo = new System.Collections.Generic.Dictionary<long, float>(grid * grid);
        ChunkCornerGrid corners = BuildCornerGrid(tiles, cs, seed, heightMemo);

        EmitLowPolySurface(corners, cs, step,
            out Vector3[] vertices, out Vector3[] normals, out Vector2[] uv, out Color[] colors,
            out Bounds bounds);

        // Collider at the SAME step so the stand-surface matches the visible facets exactly.
        BuildDecimatedCollider(corners, step,
            out Vector3[] colliderVertices, out int[] colliderTriangles);

        return new MergedChunkMeshData
        {
            Vertices = vertices,
            Triangles = EmitLowPolyIndices(cs, step),
            UV = uv,
            Normals = normals,
            Colors = colors,
            Bounds = bounds,
            BuildStamp = WorldStreamer.TerrainBuildStamp,
            TileVertexBase = null,
            TileVertexCount = null,
            Corners = corners,
            ColliderVertices = colliderVertices,
            ColliderTriangles = colliderTriangles,
            LowPolyStep = step,
        };
    }

    /// <summary>Flat-facet quad arrays over the lattice (1hi.1): every `step`-th node of the 31x31
    /// corner grid, four vertices per quad sharing one +Y-dominant flat normal, UV and strata colors
    /// sampled from the lattice itself (they are already the canonical per-corner values, so the
    /// build, patch and far-shell paths read identical colors). Pure arrays — thread-safe.</summary>
    private static void EmitLowPolySurface(ChunkCornerGrid corners, int cs, int step,
        out Vector3[] vertices, out Vector3[] normals, out Vector2[] uv, out Color[] colors,
        out Bounds bounds)
    {
        int axis = (cs / step) + 1;
        int quads = (axis - 1) * (axis - 1);
        int grid = TerrainChunkCoord.CornerGridSize;
        int cornerCount = grid * grid;

        var verts = new Vector3[quads * 4];
        var nrm = new Vector3[quads * 4];
        var uvs = new Vector2[quads * 4];
        var cols = new Color[quads * 4];

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int gz = 0; gz < axis - 1; gz++)
        {
            for (int gx = 0; gx < axis - 1; gx++)
            {
                // Sample the same lattice the LOD children / far shell use (1e6 winding: +X next
                // column, +Z next row), so edited corners re-appear here via the patch restamp.
                int s00 = (gz * step) * grid + (gx * step);
                int s10 = s00 + step;
                int s01 = s00 + step * grid;
                int s11 = s01 + step;

                Vector3 p00 = new Vector3(gx * step, SanitizeHeight(corners.Y[s00]), gz * step);
                Vector3 p10 = new Vector3((gx + 1) * step, SanitizeHeight(corners.Y[s10]), gz * step);
                Vector3 p01 = new Vector3(gx * step, SanitizeHeight(corners.Y[s01]), (gz + 1) * step);
                Vector3 p11 = new Vector3((gx + 1) * step, SanitizeHeight(corners.Y[s11]), (gz + 1) * step);

                // Flat +Y-dominant facet normal — the far-shell rule (1hi), so steep faces never
                // shade upside-down.
                Vector3 n = Vector3.Cross(p10 - p00, p01 - p00);
                if (n.sqrMagnitude > 1e-12f)
                    n = n.normalized;
                else
                    n = Vector3.up;
                if (n.y < 0f)
                    n = -n;

                int v = (gx + gz * (axis - 1)) * 4;
                verts[v + 0] = p00; verts[v + 1] = p10; verts[v + 2] = p11; verts[v + 3] = p01;
                nrm[v + 0] = n; nrm[v + 1] = n; nrm[v + 2] = n; nrm[v + 3] = n;
                uvs[v + 0] = s00 < cornerCount ? corners.UV[s00] : Vector2.zero;
                uvs[v + 1] = s10 < cornerCount ? corners.UV[s10] : Vector2.zero;
                uvs[v + 2] = s11 < cornerCount ? corners.UV[s11] : Vector2.zero;
                uvs[v + 3] = s01 < cornerCount ? corners.UV[s01] : Vector2.zero;
                cols[v + 0] = s00 < cornerCount ? corners.Colors[s00] : Color.white;
                cols[v + 1] = s10 < cornerCount ? corners.Colors[s10] : Color.white;
                cols[v + 2] = s11 < cornerCount ? corners.Colors[s11] : Color.white;
                cols[v + 3] = s01 < cornerCount ? corners.Colors[s01] : Color.white;

                if (p00.y < minY) minY = p00.y;
                if (p00.y > maxY) maxY = p00.y;
                if (p10.y < minY) minY = p10.y;
                if (p10.y > maxY) maxY = p10.y;
                if (p01.y < minY) minY = p01.y;
                if (p01.y > maxY) maxY = p01.y;
                if (p11.y < minY) minY = p11.y;
                if (p11.y > maxY) maxY = p11.y;
            }
        }

        float span = cs * ChunkData.Size;
        bounds = new Bounds(
            new Vector3(span * 0.5f, minY < maxY ? (minY + maxY) * 0.5f : minY, span * 0.5f),
            new Vector3(span, Mathf.Max(0.1f, minY < maxY ? (maxY - minY) + 0.1f : 0.1f), span));

        vertices = verts;
        normals = nrm;
        uv = uvs;
        colors = cols;
    }

    /// <summary>Shared index windup for the low-poly facet grid (1hi.1). Each lattice quad is
    /// emitted in the SAME corner order as the smooth tile builder — NW, NE, SE, SW (v+3, v+2,
    /// v+1, v+0) with BuildMeshData's exact two-triangle pattern (0,1,2)/(0,2,3) — so every facet
    /// front-faces +Y like the 1 m tiles. (1hi.2: the original build used the back-facing p00-first
    /// winding that the far shell/LOD children share; the far shell masked it with a double-sided
    /// material, but with one-sided GroundMaterial the upper face was culled — the reported
    /// upside-down root.) Pure array — thread-safe.</summary>
    private static int[] EmitLowPolyIndices(int cs, int step)
    {
        int axis = (cs / step) + 1;
        int quads = (axis - 1) * (axis - 1);
        int[] triangles = new int[quads * 6];
        for (int gz = 0; gz < axis - 1; gz++)
        {
            for (int gx = 0; gx < axis - 1; gx++)
            {
                int v = (gx + gz * (axis - 1)) * 4;
                int t = (gx + gz * (axis - 1)) * 6;
                triangles[t + 0] = v + 3; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 3; triangles[t + 4] = v + 1; triangles[t + 5] = v + 0;
            }
        }
        return triangles;
    }

    /// <summary>
    /// Re-samples a chunk's low-poly ROOT surface from its (already patch-restamped) lattice and
    /// returns the updated merged data (1hi.1). PatchRegion re-stamps the corners with
    /// PatchCornerGrid, then this rewrites the root's vertex/normal/UV/color arrays + bounds from
    /// them — the coarse mesh is tiny (100 quads / 121 verts at 3 m, the 1ia default; 25 quads /
    /// 36 verts at 6 m) so a full re-emit is far cheaper than the
    /// 1 m per-tile skim it replaces. Returns a fresh struct because MergedChunkMeshData is a
    /// value type; the render arrays are replaced with fresh equal-length arrays (vertex count
    /// never changes for a fixed step), and the collider is re-cooked separately at the same step.
    /// Main thread only.
    /// </summary>
    public static MergedChunkMeshData ResampleLowPolySurface(MergedChunkMeshData md, int step)
    {
        EmitLowPolySurface(md.Corners, TerrainChunkCoord.ChunkSize, step,
            out Vector3[] vertices, out Vector3[] normals, out Vector2[] uv, out Color[] colors,
            out Bounds bounds);
        md.Vertices = vertices;
        md.Normals = normals;
        md.UV = uv;
        md.Colors = colors;
        md.Bounds = bounds;
        md.LowPolyStep = step;
        return md;
    }

    /// <summary>
    /// Builds the coarse (axes x axes per chunk, axes = <see cref="TerrainChunkCoord.CornerGridSize"/>)
    /// world-corner lattice the LOD children sample from (1ew). Each lattice node copies the EXACT
    /// merged shallow-block slot that used to sit at a fixed (gz * cs + gx) * 4 + slot offset, so a
    /// LOD child stays pixel-identical to the pre-refinement build. Ownership per corner: interior
    /// corner (gx,gz) → tile(gx,gz) SW (slot 3), north boundary (gz==cs) → tile(gx, cs-1) NW
    /// (slot 0), east boundary (gx==cs) → tile(cs-1, gz) **SE (slot 2)**, the far corner
    /// (cs,cs) → tile(cs-1,cs-1) SE (slot 2). Every rule reads the corner the node actually STANDS
    /// ON: 1hk corrected the east column from NE to SE, which had been stamping it one metre north
    /// of itself. Band colors are recomputed here from the same world corner each node stands on.
    /// </summary>
    private static ChunkCornerGrid BuildCornerGrid(ChunkMeshData[] tiles, int cs, long seed,
        System.Collections.Generic.Dictionary<long, float> heightMemo)
    {
        int axes = TerrainChunkCoord.CornerGridSize;
        var grid = new ChunkCornerGrid(axes * axes);
        for (int gz = 0; gz < axes; gz++)
        {
            for (int gx = 0; gx < axes; gx++)
            {
                int ownerIdx, slot;
                // Exact-corner arithmetic: the boundary branches require the OTHER axis inside the
                // chunk, so the far corner (cs,cs) falls through to the corner case below. Pre-1fx
                // the `else if (gz == cs)` matched (gx=cs, gz=cs) FIRST and indexed tiles[900] (out
                // of bounds) on every build — which killed the entire real-chunk ring (the whole near
                // world never materialized; only the far shell rendered past ~300 m).
                //
                // 1hk: the east column is the tile's SE corner, NOT its NE. Node (cs, gz) stands on
                // world corner (Ox+cs, Oz+gz), and tile (cs-1, gz)'s SE corner is exactly that
                // point; its NE corner is one metre further north. The old `slot = 1` therefore
                // stamped the whole east column (rows 0..cs-1) with the height of the corner BEHIND
                // it, so every east seam paired a node against a different world corner than its
                // west-side twin: 0.5 m holes in the low-poly root, a stepped collider seam, LOD
                // children sheared 1 m, and a matching step against the far shell. The far-side node
                // (0, gz) is tile (0, gz)'s SW = the same point, which is why only ONE side of the
                // seam looked wrong and why ChunkValidator (tile-vs-tile) never caught it.
                if (gx < cs && gz < cs) { ownerIdx = gz * cs + gx; slot = 3; }        // SW of tile
                else if (gx < cs)       { ownerIdx = (cs - 1) * cs + gx; slot = 0; } // north edge: NW of tile
                else if (gz < cs)       { ownerIdx = gz * cs + (cs - 1); slot = 2; } // east edge: SE of tile
                else                    { ownerIdx = (cs - 1) * cs + (cs - 1); slot = 2; } // corner (cs,cs): SE of tile

                ChunkMeshData owner = tiles[ownerIdx];
                int idx = gz * axes + gx;

                float y = 0f;
                if (owner.Vertices != null && slot < owner.Vertices.Length)
                    y = SanitizeHeight(owner.Vertices[slot].y);
                else if (owner.Vertices != null && owner.Vertices.Length > 0)
                    y = SanitizeHeight(owner.Vertices[0].y);   // defensive: resize-block safety fallback
                grid.Y[idx] = y;

                grid.Normals[idx] = (owner.Normals != null && slot < owner.Normals.Length)
                    ? owner.Normals[slot] : Vector3.up;
                grid.UV[idx] = (owner.UV != null && slot < owner.UV.Length)
                    ? owner.UV[slot] : Vector2.zero;

                int wx = owner.Coord.X + (slot == 1 || slot == 2 ? 1 : 0);
                int wz = owner.Coord.Z + (slot < 2 ? 1 : 0);
                grid.Colors[idx] = TerrainBandColor(seed, wx, wz, y, heightMemo);
            }
        }
        return grid;
    }

    /// <summary>
    /// Builds the DECIMATED collider lattice for a chunk (1hi): every <paramref name="step"/>-th node
    /// of the 31x31 world-corner grid (<see cref="ChunkCornerGrid"/>; <b>every node since 1ex</b>,
    /// <see cref="ChunkColliderDecimation"/> = 1), indexed with the same up-facing winding as the smooth
    /// tile builder (1hi.2: NW, NE, SE, SW — i01, i11, i10, i00 — triangles (01,11,10)/(01,10,00), so the
    /// surface the player stands on fronts the MeshCollider like the pre-1hi full-mesh collider; the
    /// original p00-first winding was back-facing and cast the player through).
    ///
    /// <para><b>Horizontal quads ONLY</b> — there is no vertical strip pass in this method (the RENDER
    /// mesh has one for cliff faces). A vertical step is therefore sampled as a ramp, and that ramp's
    /// gradient is what the player climbs: at step 2 a 1 m cliff presented as ~26.6°, at step 1 it is
    /// exactly 45° — the <c>CharacterController</c>'s default <c>slopeLimit</c>, which this project never
    /// assigns (only <c>skinWidth</c> and <c>stepOffset</c>). <b>Cliff traversal is a 1ex play-test item.</b>
    ///
    /// <para>The lattice holds the EXACT world corners the LOD children (and neighbour chunks) use, so the
    /// physics surface is seam-proof across chunks by construction — and re-derivable from the
    /// patch-restamped lattice after deformation. Pure arrays — thread-safe.
    /// </summary>
    public static void BuildDecimatedCollider(ChunkCornerGrid corners, int step,
        out Vector3[] vertices, out int[] triangles)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int axis = (cs / step) + 1;
        int grid = TerrainChunkCoord.CornerGridSize;
        var verts = new Vector3[axis * axis];
        for (int gz = 0, v = 0; gz < axis; gz++)
        {
            for (int gx = 0; gx < axis; gx++, v++)
            {
                int s = gz * step * grid + gx * step;
                verts[v] = new Vector3(gx * step,
                    s < corners.Y.Length ? SanitizeHeight(corners.Y[s]) : 0f,
                    gz * step);
            }
        }
        var tris = new int[(axis - 1) * (axis - 1) * 6];
        for (int gz = 0, t = 0; gz < axis - 1; gz++)
        {
            for (int gx = 0; gx < axis - 1; gx++)
            {
                int i00 = gz * axis + gx;
                int i10 = i00 + 1;
                int i01 = i00 + axis;
                int i11 = i01 + 1;
                tris[t++] = i01; tris[t++] = i11; tris[t++] = i10;
                tris[t++] = i01; tris[t++] = i10; tris[t++] = i00;
            }
        }
        vertices = verts;
        triangles = tris;
    }

    /// <summary>
    /// Restamps only the <paramref name="region"/>'s corner-grid nodes after ChunkObject.PatchRegion
    /// had re-skimmed those tiles (1ew). A node is re-stamped when its canonical owner tile lives
    /// inside the patched region; nodes owned by tiles outside the region keep their old values.
    /// <paramref name="region"/> is a row-major (w x h) array of re-built tiles starting at chunk-local
    /// tile (<paramref name="regionX"/>, <paramref name="regionZ"/>).
    /// </summary>
    public static void PatchCornerGrid(ChunkCornerGrid grid, ChunkMeshData[] region,
        int cs, int regionX, int regionZ, int w, int h, long seed,
        System.Collections.Generic.Dictionary<long, float> heightMemo = null)
    {
        int axes = TerrainChunkCoord.CornerGridSize;
        for (int gz = regionZ; gz <= regionZ + h; gz++)
        {
            if (gz < 0 || gz >= axes) continue;
            for (int gx = regionX; gx <= regionX + w; gx++)
            {
                if (gx < 0 || gx >= axes) continue;

                int ownerLx, ownerLz, slot;
                // Must mirror BuildCornerGrid's exact-corner ownership (1fx): the boundary branches
                // key on the OUTER axis being at the chunk edge, so the far corner (cs,cs) resolves
                // to the corner case — owner tile (cs-1,cs-1) SE. The pre-fix `else if (gz == cs)`
                // claimed (cs,cs) for tile (gx, cs-1) = also (30, 29), which the region bounds check
                // then skipped, so the chunk's NE lattice node was never re-stamped after a patch.
                // 1hk: east column is the owner's SE (was NE) — same rule as the build path above,
                // or a patch would stamp the column differently from a fresh build and the two would
                // drift apart by exactly the 1 m the old build rule was off by.
                if (gx < cs && gz < cs) { ownerLx = gx; ownerLz = gz; slot = 3; }
                else if (gx < cs)       { ownerLx = gx; ownerLz = cs - 1; slot = 0; }
                else if (gz < cs)       { ownerLx = cs - 1; ownerLz = gz; slot = 2; }
                else                    { ownerLx = cs - 1; ownerLz = cs - 1; slot = 2; }

                if (ownerLx < regionX || ownerLx > regionX + w - 1 ||
                    ownerLz < regionZ || ownerLz > regionZ + h - 1)
                    continue; // owner lives outside the patched tiles — corner unchanged

                int ri = (ownerLz - regionZ) * w + (ownerLx - regionX);
                ChunkMeshData owner = region[ri];
                if (owner.Vertices == null || slot >= owner.Vertices.Length)
                    continue;

                int idx = gz * axes + gx;
                float y = SanitizeHeight(owner.Vertices[slot].y);
                grid.Y[idx] = y;
                grid.Normals[idx] = (owner.Normals != null && slot < owner.Normals.Length)
                    ? owner.Normals[slot] : Vector3.up;
                grid.UV[idx] = (owner.UV != null && slot < owner.UV.Length)
                    ? owner.UV[slot] : Vector2.zero;
                int wx = owner.Coord.X + (slot == 1 || slot == 2 ? 1 : 0);
                int wz = owner.Coord.Z + (slot < 2 ? 1 : 0);
                grid.Colors[idx] = heightMemo != null
                    ? TerrainBandColor(seed, wx, wz, y, heightMemo)
                    : TerrainBandColor(seed, wx, wz, y);
            }
        }
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

    /// <summary>Decimation step (m) of the smooth chunk's decimated collider lattice (1hi).
    ///
    /// <para><b>1ex: 2 → 1.</b> The player has NO separate ground raycast — <c>CharacterController.Move</c>
    /// sweeps this MeshCollider directly (<c>PlayerController.Movement.cs</c>), so this lattice IS the
    /// ground the player stands on. At step 2 a carve was sampled at its worst case (every 2nd node, so a
    /// footprint centred on an odd x or z had NO sampled node inside it at all) and the player walked
    /// over a visible 1.1 m crater. Step 1 samples every corner, so the physics surface now carries the
    /// full 1 m render resolution: 961 verts / 1800 tris.
    ///
    /// <para><b>The 1hi "~4x cheaper cook" premise is DEAD at this value</b> — 961/1800 is the same size
    /// as the full render surface, so the decimation buys nothing in cook cost on the smooth default.
    /// What it still buys is <i>topology</i>: this is horizontal quads only, no side walls and no
    /// refined blocks, so it stays smaller than the render mesh that carries both. The mechanism is kept
    /// deliberately rather than inlined as a literal 1: <c>ColliderStep</c> still prefers the chunk's own
    /// low-poly facet step (1hi.1), and this stays the one place the fallback is stated.
    ///
    /// <para><b>Invariant:</b> the step must divide 30 (ChunkSize) or the last grid row falls short of the
    /// chunk boundary — a visible crack along every chunk edge. Any legal value is {1, 2, 3, 5, 6, 10, 15, 30}.</summary>
    public const int ChunkColliderDecimation = 1;

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
    /// place (1dv), so rebuilds never allocate a new Mesh object. Meshes are NEVER uploaded with
    /// markNoLongerReadable (a 1es attempt to free CPU buffers on far cells was reverted — the pooled
    /// meshes are re-specified on every reuse by real chunks (deformation) and far cells alike, and a
    /// non-readable pooled Mesh throws on the next SetNormals/SetVertices).
    /// </summary>
    public static void UploadMerged(MergedChunkMeshData md, Mesh mesh)
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
        mesh.UploadMeshData(false);
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
