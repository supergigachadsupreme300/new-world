using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Column-run terrain data behind the WorldStreamer voxel experiment (1et).
///
/// The terrain is a 1-metre grid of vertical columns of solid earth. Each chunk
/// (30x30 columns) stores, per column, a sorted list of solid runs [YBot, YTop];
/// phase 1 holds exactly one run per edited column. A missing (null) list means the
/// column is untouched: its top regenerates from the same deterministic world noise
/// the pristine heightfield uses, so pristine chunks cost zero storage and re-roll
/// identically on every load. Material (grass/dirt/stone) is DERIVED from the dig
/// depth below that pristine surface via ChunkMeshGenerator.TerrainBandColor — never
/// stored — so a run only ever needs two integers.
///
/// Columns span the whole column floor to their top (runs [ColumnBaseY .. YTop]), so
/// excavated pits stay solid as deep as gameplay can reach (200 m of headroom below
/// the safety clamp) and the stored shape is a pure height function — the phase 1
/// "height-field adapter" (every column = one run) that keeps tools/spells/shovel
/// gates reading the ChunkData 4-corner API unchanged. Phase 2 adds multi-run sculpt
/// ops for overhangs and free 3D carving.
/// </summary>
public sealed class VoxelChunkData
{
    /// <summary>Column-floor of every run (the huge vertical clamp): solid earth spans from this
    /// Y up to the column's top. Tops are clamped to ±200 like the heightfield's SanitizeHeight,
    /// so 200 m of headroom below the floor keeps any reachable pit solid.</summary>
    public const int ColumnBaseY = -1000;

    /// <summary>Mesh/collider-safety clamp for column tops (mirrors
    /// <see cref="ChunkMeshGenerator.MaxTerrainHeight"/>). Soil this far below the noise surface
    /// reads pure stone via the strata bands; raising past this band is forbidden for the same
    /// reason it is forbidden in the smooth path.</summary>
    public const int MinTop = -200;
    public const int MaxTop = 200;

    public readonly TerrainChunkCoord Coord;
    public readonly long Seed;

    /// <summary>True when any column diverges from its pristine (noise-rounded) top. O(1) —
    /// a count is kept in sync by <see cref="SetColumnTop"/>.</summary>
    public bool HasModifications => _modifiedCount > 0;

    // Sparse per-column run lists, indexed localZ * ChunkSize + localX. Null = untouched.
    private readonly List<VoxelRun>[] _columns;
    private int _modifiedCount;

    private VoxelChunkData(TerrainChunkCoord coord, long seed)
    {
        Coord = coord;
        Seed = seed;
        _columns = new List<VoxelRun>[TerrainChunkCoord.ChunkArea];
    }

    public static VoxelChunkData Create(TerrainChunkCoord coord, long seed)
    {
        return new VoxelChunkData(coord, seed);
    }

    /// <summary>
    /// Deterministic pristine top of a world column at its integer (x, z): the rounded world-space
    /// noise at the column centre, clamped into the mesh-safe band. Pure function of (seed, x, z) —
    /// every chunk that touches the column derives the SAME value, so untouched seams (including
    /// across chunk borders) are always level.
    /// </summary>
    public static int RoundNoiseTop(long seed, int wx, int wz)
    {
        float h = TerrainNoiseGenerator.GetHeight(seed, wx + 0.5f, wz + 0.5f);
        return Mathf.Clamp(Mathf.RoundToInt(h), MinTop, MaxTop);
    }

    /// <summary>Integer top of the local column (0..29 x/z): the stored run top when edited, else
    /// the pristine noise-rounded top.</summary>
    public int ColumnTop(int x, int z)
    {
        List<VoxelRun> runs = _columns[z * TerrainChunkCoord.ChunkSize + x];
        if (runs != null && runs.Count > 0)
            return runs[runs.Count - 1].YTop;
        return RoundNoiseTop(Seed, WorldX(x), WorldZ(z));
    }

    /// <summary>
    /// Replace the local column's solid volume with one run [ColumnBaseY .. top]. Writing the
    /// column's own pristine value clears it back to untouched (keeps save files sparse). Phase 1
    /// uses this single-run form — the height-field adapter drops tile corners to an integer column
    /// top; multi-run column sculpt ops land with phase 2.
    /// </summary>
    public void SetColumnTop(int x, int z, int top)
    {
        int i = z * TerrainChunkCoord.ChunkSize + x;
        int clamped = Mathf.Clamp(top, MinTop, MaxTop);
        List<VoxelRun> runs = _columns[i];
        if (clamped == RoundNoiseTop(Seed, WorldX(x), WorldZ(z)))
        {
            if (runs != null)
            {
                _columns[i] = null;
                _modifiedCount--;
            }
            return;
        }
        if (runs == null)
        {
            runs = new List<VoxelRun>(1);
            _columns[i] = runs;
            _modifiedCount++;
        }
        else
        {
            runs.Clear();
        }
        runs.Add(new VoxelRun { YBot = ColumnBaseY, YTop = clamped });
    }

    /// <summary>Visit every edited local column with its (single-run) top — the phase 1
    /// persistence reader/writer path.</summary>
    public void ForEachColumnTop(System.Action<int, int, int> visit)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        for (int i = 0; i < _columns.Length; i++)
        {
            List<VoxelRun> runs = _columns[i];
            if (runs == null || runs.Count == 0)
                continue;
            visit(i % cs, i / cs, runs[runs.Count - 1].YTop);
        }
    }

    private int WorldX(int x) => Coord.X * TerrainChunkCoord.ChunkSize + x;
    private int WorldZ(int z) => Coord.Z * TerrainChunkCoord.ChunkSize + z;
}

/// <summary>A solid vertical run of one column: every integer level y with YBot &lt; y &lt;= YTop.
/// Material is derived from depth below the pristine noise surface, never stored, so a run only
/// needs two integers.</summary>
public struct VoxelRun
{
    public int YBot;
    public int YTop;
}