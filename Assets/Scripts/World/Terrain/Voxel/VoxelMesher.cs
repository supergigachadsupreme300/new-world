using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the stepped voxel mesh for a terrain chunk (WorldStreamer voxel experiment, 1et).
///
/// The mesh is a 1-metre stepped world: each column renders a flat top quad (the column's
/// surface), and wherever two neighbouring columns top out at different integer heights the higher
/// one emits a vertical terrace wall down to the lower, subdivided one band per metre (self-similar
/// to the legacy flat-slab walls) so a 1 m difference reads as one stackable step. Each shared face
/// is emitted exactly once (the higher side owns it) and only where the tops differ — identical
/// tops add zero geometry. Runs of consecutive edges with identical (high, low, facing) tops are
/// merged into one wall strip per boundary plane so gentle terrain stays near the smooth chunk's
/// vertex count.
///
/// The four outer chunk faces consult a border map of real neighbour-chunk column tops when loaded
/// (so seam walls between two edited chunks are level), and fall back to the same deterministic
/// noise rounding a pristine neighbour would derive — untouched seams never show phantom walls.
///
/// Pure C# arrays (via lists) — safe to build on a worker thread; the result feeds the same
/// pooled-mesh ChunkObject.ApplyMerged path as smooth chunks.
/// </summary>
public static class VoxelMesher
{
    /// <summary>1 m steps up to this drop; deeper drops quantize to fixed-width bands so a deep
    /// pit wall never explodes the tri count. Phase 1 bound — phase 3 formalizes a wall-tri budget
    /// per chunk.</summary>
    private const int MaxStepBands = 32;
    private const int QuantizedBandCount = 16;

    public static MergedChunkMeshData Build(VoxelChunkData vc,
        IReadOnlyDictionary<long, int> borderTops = null)
    {
        long seed = vc.Seed;
        int cs = TerrainChunkCoord.ChunkSize;
        int originX = vc.Coord.X * cs;
        int originZ = vc.Coord.Z * cs;

        var vertices = new List<Vector3>(8192);
        var triangles = new List<int>(16384);
        var uv = new List<Vector2>(8192);
        var normals = new List<Vector3>(8192);
        var colors = new List<Color>(8192);

        // Pristine-height memo (mirrors BuildMergedMeshData's 1dt memo): every colour sample reads
        // the deterministic 5-octave surface once per column/edge instead of re-sampling per vertex.
        var memo = new Dictionary<long, float>();
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        // Pass 1 — top faces: merge equal-height columns into row runs (one quad per run), so a
        // flat field renders as a handful of quads instead of 900.
        for (int z = 0; z < cs; z++)
        {
            int x = 0;
            while (x < cs)
            {
                int top = vc.ColumnTop(x, z);
                int xEnd = x + 1;
                while (xEnd < cs && vc.ColumnTop(xEnd, z) == top)
                    xEnd++;
                EmitTopRun(vertices, triangles, uv, normals, colors, seed, memo,
                    originX, originZ, x, xEnd, z, top, ref minY, ref maxY);
                x = xEnd;
            }
        }

        // Pass 2 — vertical walls, one X boundary plane at a time (planes local X = 0..cs; the
        // plane at X sits between local column X-1 (-X side) and column X (+X side), with X==0 and
        // X==cs facing the neighbour chunks). Run-merge consecutive z with identical wall shape.
        for (int planeX = 0; planeX <= cs; planeX++)
        {
            int z = 0;
            while (z < cs)
            {
                int topA = XPlaneTop(vc, borderTops, seed, originX, originZ, planeX, z, true);
                int topB = XPlaneTop(vc, borderTops, seed, originX, originZ, planeX, z, false);
                if (topA == topB)
                {
                    z++;
                    continue;
                }
                bool leftHigher = topA > topB;
                int hi = leftHigher ? topA : topB;
                int lo = leftHigher ? topB : topA;
                Vector3 outward = leftHigher ? Vector3.left : Vector3.right;
                int zEnd = z + 1;
                while (zEnd < cs)
                {
                    int nA = XPlaneTop(vc, borderTops, seed, originX, originZ, planeX, zEnd, true);
                    int nB = XPlaneTop(vc, borderTops, seed, originX, originZ, planeX, zEnd, false);
                    if (nA == nB || (nA > nB) != leftHigher)
                        break;
                    if ((nA > nB ? nA : nB) != hi || (nA > nB ? nB : nA) != lo)
                        break;
                    zEnd++;
                }
                EmitWallStrip(vertices, triangles, uv, normals, colors, seed, memo,
                    originX, originZ, hi, lo, outward,
                    new Vector3(planeX, 0f, zEnd), new Vector3(planeX, 0f, z),
                    ref minY, ref maxY);
                z = zEnd;
            }
        }

        // Pass 3 — the Z boundary planes (planes local Z = 0..cs between local column Z-1 and Z),
        // run-merging consecutive x.
        for (int planeZ = 0; planeZ <= cs; planeZ++)
        {
            int x = 0;
            while (x < cs)
            {
                int topA = ZPlaneTop(vc, borderTops, seed, originX, originZ, x, planeZ, true);
                int topB = ZPlaneTop(vc, borderTops, seed, originX, originZ, x, planeZ, false);
                if (topA == topB)
                {
                    x++;
                    continue;
                }
                bool belowHigher = topA > topB;
                int hi = belowHigher ? topA : topB;
                int lo = belowHigher ? topB : topA;
                Vector3 outward = belowHigher ? Vector3.back : Vector3.forward;
                int xEnd = x + 1;
                while (xEnd < cs)
                {
                    int nA = ZPlaneTop(vc, borderTops, seed, originX, originZ, xEnd, planeZ, true);
                    int nB = ZPlaneTop(vc, borderTops, seed, originX, originZ, xEnd, planeZ, false);
                    if (nA == nB || (nA > nB) != belowHigher)
                        break;
                    if ((nA > nB ? nA : nB) != hi || (nA > nB ? nB : nA) != lo)
                        break;
                    xEnd++;
                }
                EmitWallStrip(vertices, triangles, uv, normals, colors, seed, memo,
                    originX, originZ, hi, lo, outward,
                    new Vector3(xEnd, 0f, planeZ), new Vector3(x, 0f, planeZ),
                    ref minY, ref maxY);
                x = xEnd;
            }
        }

        // Vertices span the full 30 m x/z extent; size the bounds from the sampled y-range.
        float span = cs * ChunkData.Size;
        return new MergedChunkMeshData
        {
            Vertices = vertices.ToArray(),
            Triangles = triangles.ToArray(),
            UV = uv.ToArray(),
            Normals = normals.ToArray(),
            Colors = colors.ToArray(),
            Bounds = new Bounds(
                new Vector3(span * 0.5f, minY < maxY ? (minY + maxY) * 0.5f : minY, span * 0.5f),
                new Vector3(span, Mathf.Max(0.1f, (maxY - minY) + 0.1f), span)),
        };
    }

    // --- Top faces ---

    private static void EmitTopRun(
        List<Vector3> vertices, List<int> triangles, List<Vector2> uv, List<Vector3> normals,
        List<Color> colors, long seed, Dictionary<long, float> memo,
        int originX, int originZ, int x0, int xEnd, int z, int top,
        ref float minY, ref float maxY)
    {
        int len = xEnd - x0;
        int v = vertices.Count;
        // Slot winding matches BuildMeshData's flat quad (NW, NE, SE, SW), normal +Y.
        VertPoint(vertices, x0, top, z + 1);
        VertPoint(vertices, xEnd, top, z + 1);
        VertPoint(vertices, xEnd, top, z);
        VertPoint(vertices, x0, top, z);
        triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
        triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
        // U tiles once per metre so the texture density stays fixed across merged runs.
        uv.Add(new Vector2(0f, 1f));
        uv.Add(new Vector2(len, 1f));
        uv.Add(new Vector2(len, 0f));
        uv.Add(new Vector2(0f, 0f));
        for (int k = 0; k < 4; k++)
            normals.Add(Vector3.up);
        colors.Add(ColorAt(seed, memo, originX + x0, originZ + z + 1, top));
        colors.Add(ColorAt(seed, memo, originX + xEnd, originZ + z + 1, top));
        colors.Add(ColorAt(seed, memo, originX + xEnd, originZ + z, top));
        colors.Add(ColorAt(seed, memo, originX + x0, originZ + z, top));
        TrackY(ref minY, ref maxY, top);
    }

    // --- Boundary-plane neighbours ---

    /// <summary>Top of the column on an X-plane's -X side (true) or +X side (false): the in-chunk
    /// local column when it exists, else the outer neighbour via the border map / noise.</summary>
    private static int XPlaneTop(VoxelChunkData vc, IReadOnlyDictionary<long, int> borderTops,
        long seed, int originX, int originZ, int planeX, int z, bool leftSide)
    {
        int localCol = leftSide ? planeX - 1 : planeX;
        if (localCol >= 0 && localCol < TerrainChunkCoord.ChunkSize)
            return vc.ColumnTop(localCol, z);
        return OuterColumnTop(borderTops, seed, originX + (leftSide ? -1 : TerrainChunkCoord.ChunkSize), originZ + z);
    }

    /// <summary>Top of the column on a Z-plane's -Z side (true) or +Z side (false): the in-chunk
    /// local column when it exists, else the outer neighbour via the border map / noise.</summary>
    private static int ZPlaneTop(VoxelChunkData vc, IReadOnlyDictionary<long, int> borderTops,
        long seed, int originX, int originZ, int x, int planeZ, bool belowSide)
    {
        int localCol = belowSide ? planeZ - 1 : planeZ;
        if (localCol >= 0 && localCol < TerrainChunkCoord.ChunkSize)
            return vc.ColumnTop(x, localCol);
        return OuterColumnTop(borderTops, seed, originX + x, originZ + (belowSide ? -1 : TerrainChunkCoord.ChunkSize));
    }

    /// <summary>World-column top from the real neighbour border map when present, else the same
    /// deterministic noise rounding a pristine neighbour derives (no phantom walls on untouched
    /// seams).</summary>
    private static int OuterColumnTop(IReadOnlyDictionary<long, int> borderTops, long seed, int wx, int wz)
    {
        if (borderTops != null && borderTops.TryGetValue(((long)wx << 32) | (uint)wz, out int top))
            return top;
        return VoxelChunkData.RoundNoiseTop(seed, wx, wz);
    }

    // --- Side walls ---

    /// <summary>
    /// Emits one merged wall strip on a boundary plane. <paramref name="edgeStartZ"/> /
    /// <paramref name="edgeEndZ"/> are the strip's two ends along the plane axis (chunk-local, y=0);
    /// the wall spans [start, end] on that axis between the high top <paramref name="hi"/> and the
    /// low top <paramref name="lo"/>. Winding matches the merged builder (cross product vs the
    /// outward normal). Colours are sampled per band vertex world corner like the slab walls, so a
    /// tall drop shows the grass rim then dirt, fading into stone as the wall descends.
    /// </summary>
    private static void EmitWallStrip(
        List<Vector3> vertices, List<int> triangles, List<Vector2> uv, List<Vector3> normals,
        List<Color> colors, long seed, Dictionary<long, float> memo,
        int originX, int originZ, int hi, int lo, Vector3 outward,
        Vector3 edgeStart, Vector3 edgeEnd,
        ref float minY, ref float maxY)
    {
        int drop = hi - lo;
        if (drop <= 0)
            return;
        int bands;
        if (drop <= MaxStepBands)
        {
            bands = drop;
        }
        else
        {
            bands = QuantizedBandCount;
        }

        // Axis length of the strip (metres) for per-metre U tiling.
        float len = (edgeStart - edgeEnd).magnitude;

        // The two edge corners' world coords (same for all four face columns along the plane).
        int wxStart = originX + Mathf.RoundToInt(edgeStart.x);
        int wzStart = originZ + Mathf.RoundToInt(edgeStart.z);
        int wxEnd = originX + Mathf.RoundToInt(edgeEnd.x);
        int wzEnd = originZ + Mathf.RoundToInt(edgeEnd.z);

        for (int b = 0; b < bands; b++)
        {
            float f0 = (float)b / bands;
            float f1 = (float)(b + 1) / bands;

            Vector3 p0 = EdgePoint(edgeStart, edgeEnd, hi, lo, f0);
            Vector3 p1 = EdgePoint(edgeEnd, edgeStart, hi, lo, f0);
            Vector3 p2 = EdgePoint(edgeEnd, edgeStart, hi, lo, f1);
            Vector3 p3 = EdgePoint(edgeStart, edgeEnd, hi, lo, f1);

            int iv0 = vertices.Count, iv1 = iv0 + 1, iv2 = iv0 + 2, iv3 = iv0 + 3;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);

            colors.Add(ColorAt(seed, memo, wxStart, wzStart, p0.y));
            colors.Add(ColorAt(seed, memo, wxEnd, wzEnd, p1.y));
            colors.Add(ColorAt(seed, memo, wxEnd, wzEnd, p2.y));
            colors.Add(ColorAt(seed, memo, wxStart, wzStart, p3.y));

            // U tiles across the strip's length (per metre); V tiles once per band (stacked-slab read).
            uv.Add(new Vector2(0f, b));
            uv.Add(new Vector2(len, b));
            uv.Add(new Vector2(len, b + 1f));
            uv.Add(new Vector2(0f, b + 1f));

            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.sqrMagnitude > 1e-10f)
                n = n.normalized;
            else
                n = outward;
            if (Vector3.Dot(n, outward) < 0f)
                n = -n;
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);

            bool ccw = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) >= 0f;
            if (ccw)
            {
                triangles.Add(iv0); triangles.Add(iv1); triangles.Add(iv2);
                triangles.Add(iv0); triangles.Add(iv2); triangles.Add(iv3);
            }
            else
            {
                triangles.Add(iv0); triangles.Add(iv2); triangles.Add(iv1);
                triangles.Add(iv0); triangles.Add(iv3); triangles.Add(iv2);
            }

            TrackY(ref minY, ref maxY, p0.y, p1.y, p2.y, p3.y);
        }
    }

    /// <summary>Point on a wall edge interpolating between the high and low tops: lerps vertically
    /// from <paramref name="hiEnd"/> (the end at the high top) toward <paramref name="loEnd"/> (the
    /// same end at the low top) by <paramref name="f"/>. Mirrors the merged builder's band lerp.</summary>
    private static Vector3 EdgePoint(Vector3 hiEnd, Vector3 loEnd, int hi, int lo, float f)
    {
        float x = Mathf.Lerp(hiEnd.x, loEnd.x, f);
        float y = Mathf.Lerp(hi, lo, f);
        float z = Mathf.Lerp(hiEnd.z, loEnd.z, f);
        return new Vector3(x, y, z);
    }

    // --- Shared helpers ---

    private static void VertPoint(List<Vector3> vertices, float x, float y, float z)
    {
        vertices.Add(new Vector3(x, y, z));
    }

    private static Color ColorAt(long seed, Dictionary<long, float> memo, int wx, int wz, float y)
    {
        return ChunkMeshGenerator.TerrainBandColor(seed, wx, wz, y, memo);
    }

    private static void TrackY(ref float minY, ref float maxY, float a)
    {
        if (a < minY) minY = a;
        if (a > maxY) maxY = a;
    }

    private static void TrackY(ref float minY, ref float maxY, float a, float b, float c, float d)
    {
        if (a < minY) minY = a;
        if (b < minY) minY = b;
        if (c < minY) minY = c;
        if (d < minY) minY = d;
        if (a > maxY) maxY = a;
        if (b > maxY) maxY = b;
        if (c > maxY) maxY = c;
        if (d > maxY) maxY = d;
    }
}