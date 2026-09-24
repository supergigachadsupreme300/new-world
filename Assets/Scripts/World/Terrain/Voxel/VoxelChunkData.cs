using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Column-run terrain data behind the WorldStreamer voxel terrain (1et, multi-run 1eu).
///
/// The terrain is a 1-metre grid of vertical columns of solid earth. Each chunk
/// (30x30 columns) stores, per column, a sorted list of solid runs [YBot, YTop].
/// A missing (null) list means the column is untouched: its shape is exactly one run
/// [ColumnBaseY .. pristineTop], where pristineTop regenerates from the same deterministic
/// world noise the pristine heightfield uses — pristine columns cost zero storage and
/// re-roll identically on every load. Material (grass/dirt/stone) is DERIVED from the dig
/// depth below that pristine surface via ChunkMeshGenerator.TerrainBandColor — never
/// stored — so a run only ever needs two integers.
///
/// Phase 1 (1et) held exactly one run per edited column (the height-field adapter: every
/// column = one full run, top = the surface). Phase 2 (1eu) adds BOOLEAN volume ops
/// (<see cref="RemoveSolid"/>/<see cref="AddSolid"/>) on the same store, so a carve can dig
/// a roofed cave or a raised step — columns legitimately carry 2-3 runs. Surface tools
/// (shovel/pickaxe/spells) still write the whole column via <see cref="SetColumnTop"/>
/// (top-down excavation), while the new volume ops are what create overhangs and chambers.
///
/// Columns span the whole column floor to their top; the floor sits at
/// <see cref="ColumnBaseY"/> (-1000) so any pit is solid as deep as gameplay can reach,
/// and a removed volume never needs to intersect the floor for its ceiling to render.
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

    /// <summary>True when any column diverges from its pristine (noise-rounded) shape. O(1) —
    /// a count is kept in sync by every mutating op.</summary>
    public bool HasModifications => _modifiedCount > 0;

    // Sparse per-column run lists, indexed localZ * ChunkSize + localX. Null = untouched
    // (= one run [ColumnBaseY .. RoundNoiseTop(seed, worldX, worldZ)]).
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

    private int Index(int x, int z) => z * TerrainChunkCoord.ChunkSize + x;
    private int WorldX(int x) => Coord.X * TerrainChunkCoord.ChunkSize + x;
    private int WorldZ(int z) => Coord.Z * TerrainChunkCoord.ChunkSize + z;

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

    /// <summary>Integer surface top of the local column (0..29 x/z): the highest solid run's top,
    /// else the pristine noise-rounded top.</summary>
    public int ColumnTop(int x, int z)
    {
        List<VoxelRun> runs = _columns[Index(x, z)];
        if (runs != null && runs.Count > 0)
            return runs[runs.Count - 1].YTop;
        return RoundNoiseTop(Seed, WorldX(x), WorldZ(z));
    }

    /// <summary>The column's solid runs, or null when the column is untouched (pristine — implicitly
    /// the single run <see cref="PristineRun"/>). Callers must NOT mutate the returned list.</summary>
    public IList<VoxelRun> RunsAt(int x, int z)
    {
        return _columns[Index(x, z)];
    }

    /// <summary>The implicit solo run of an untouched column at local (x, z).</summary>
    public VoxelRun PristineRun(int x, int z)
    {
        return new VoxelRun { YBot = ColumnBaseY, YTop = RoundNoiseTop(Seed, WorldX(x), WorldZ(z)) };
    }

    /// <summary>Visit every edited local column with its run list (the persistence read path). The
    /// list passed to <paramref name="visit"/> must be treated read-only.</summary>
    public void VisitColumns(System.Action<int, int, IList<VoxelRun>> visit)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        for (int i = 0; i < _columns.Length; i++)
        {
            List<VoxelRun> runs = _columns[i];
            if (runs == null || runs.Count == 0)
                continue;
            visit(i % cs, i / cs, runs);
        }
    }

    /// <summary>Set the edited column's runs verbatim (the v3 load path). Prunes back to null when
    /// the result is the column's own pristine shape, as every other mutator does.</summary>
    public void SetColumnRuns(int x, int z, IList<VoxelRun> runs)
    {
        int i = Index(x, z);
        if (runs == null || runs.Count == 0)
        {
            if (_columns[i] != null)
            {
                _columns[i] = null;
                _modifiedCount--;
            }
            return;
        }
        if (_columns[i] == null)
            _modifiedCount++;
        var store = new List<VoxelRun>(runs.Count);
        for (int k = 0; k < runs.Count; k++)
            store.Add(runs[k]);
        _columns[i] = store;
        TryPruneToPristine(i, x, z);
    }

    /// <summary>
    /// Replace the local column's solid volume with one run [ColumnBaseY .. top]. Writing the
    /// column's own pristine top clears it back to untouched (keeps save files sparse). Surface
    /// tools/spells use this single-run form — top-down excavation replaces the whole column, so a
    /// cave dug by <see cref="RemoveSolid"/> is preserved by rebuilds that only overlay SURFACE
    /// edits (tying a builder's SetColumnTop to the topmost run would delete the cave).
    /// </summary>
    public void SetColumnTop(int x, int z, int top)
    {
        int i = Index(x, z);
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
        runs.Clear();
        runs.Add(new VoxelRun { YBot = ColumnBaseY, YTop = clamped });
    }

    /// <summary>
    /// Surface-tool edit (top-down excavation / raise): move the column's SURFACE to the given top
    /// while leaving any deeper runs untouched. Implemented as a volume op on the overburden only
    /// (shave [newTop..oldTop] off above, or add [oldTop..newTop] into below), NOT a full-column
    /// replace — so re-digging the ground above a cave never collapses the chamber (SetColumnTop
    /// replaces the whole column and would). The rebuild/overlay paths use this; the column is
    /// pruned back to pristine when it ends up exactly at its noise shape.
    /// </summary>
    public void SetSurfaceTop(int x, int z, int top)
    {
        int clamped = Mathf.Clamp(top, MinTop, MaxTop);
        int current = ColumnTop(x, z);
        if (clamped == current)
            return;
        if (clamped < current)
            RemoveSolid(x, z, clamped, current);
        else
            AddSolid(x, z, current, clamped);
    }

    /// <summary>
    /// Boolean remove: carve the solid volume [yBot, yTop] out of the local column. Supports
    /// overhangs and caves — a run fully inside the range vanishes, partially covered runs clip
    /// to their outside slivers, so the column may split into two runs (a roofed chamber). Returns
    /// true when the column's solid geometry actually changed. y values clamp into the solid band.
    /// </summary>
    public bool RemoveSolid(int x, int z, int yBot, int yTop)
    {
        int rBot = Mathf.Clamp(yBot, ColumnBaseY, MaxTop);
        int rTop = Mathf.Clamp(yTop, ColumnBaseY, MaxTop);
        if (rBot >= rTop)
            return false;

        int i = Index(x, z);
        List<VoxelRun> runs = _columns[i];
        if (runs == null)
        {
            VoxelRun pristine = PristineRun(x, z);
            if (rTop <= pristine.YBot || rBot >= pristine.YTop)
                return false;
            runs = new List<VoxelRun>(2) { pristine };
            _columns[i] = runs;
            _modifiedCount++;
        }

        bool changed = false;
        var kept = new List<VoxelRun>(runs.Count + 1);
        foreach (VoxelRun run in runs)
        {
            if (run.YTop <= rBot || run.YBot >= rTop)
            {
                kept.Add(run);
                continue;
            }
            changed = true;
            if (run.YBot < rBot)
                kept.Add(new VoxelRun { YBot = run.YBot, YTop = rBot });
            if (run.YTop > rTop)
                kept.Add(new VoxelRun { YBot = rTop, YTop = run.YTop });
        }
        if (!changed)
            return false;

        if (kept.Count == 0)
        {
            _columns[i] = null;
            _modifiedCount--;
            return true;
        }
        _columns[i] = kept;
        TryPruneToPristine(i, x, z);
        return true;
    }

    /// <summary>
    /// Boolean add: fill the solid volume [yBot, yTop] into the local column (a raised step, a
    /// plug, a bridge). Merges adjacent/touching runs so a column never holds a 0-height seam.
    /// Returns true when the column's solid geometry actually changed.
    /// </summary>
    public bool AddSolid(int x, int z, int yBot, int yTop)
    {
        int aBot = Mathf.Clamp(yBot, ColumnBaseY, MaxTop);
        int aTop = Mathf.Clamp(yTop, ColumnBaseY, MaxTop);
        if (aBot >= aTop)
            return false;

        int a = Index(x, z);
        List<VoxelRun> runs = _columns[a];
        if (runs == null)
        {
            VoxelRun pristine = PristineRun(x, z);
            if (aTop <= pristine.YTop)
                return false; // fully inside the already-solid column → nothing to add
            runs = new List<VoxelRun>(2) { pristine };
            _columns[a] = runs;
            _modifiedCount++;
        }

        // Sorted interval merge of [aBot, aTop] into the run list (touching/overlapping runs fuse).
        int ns = aBot, ne = aTop;
        bool pending = true;
        var merged = new List<VoxelRun>(runs.Count + 1);
        for (int k = 0; k < runs.Count; k++)
        {
            VoxelRun run = runs[k];
            if (run.YTop < ns)
            {
                merged.Add(run);
                continue;
            }
            if (run.YBot > ne)
            {
                if (pending)
                {
                    merged.Add(new VoxelRun { YBot = ns, YTop = ne });
                    pending = false;
                }
                merged.Add(run);
                continue;
            }
            // Overlap/touch: absorb the run into the pending interval.
            if (run.YBot < ns) ns = run.YBot;
            if (run.YTop > ne) ne = run.YTop;
        }
        if (pending)
            merged.Add(new VoxelRun { YBot = ns, YTop = ne });

        if (_columns[a] == null)
            _modifiedCount++;
        _columns[a] = merged;
        TryPruneToPristine(a, x, z);
        return true;
    }

    /// <summary>When a column's edited runs collapse back to its own pristine solo column
    /// [ColumnBaseY .. pristineTop], drop the entry so saves stay sparse and HasModifications
    /// tracks only real edits.</summary>
    private void TryPruneToPristine(int i, int x, int z)
    {
        List<VoxelRun> runs = _columns[i];
        if (runs == null)
            return;
        VoxelRun pristine = PristineRun(x, z);
        if (runs.Count == 1 && runs[0].YBot == pristine.YBot && runs[0].YTop == pristine.YTop)
        {
            _columns[i] = null;
            _modifiedCount--;
        }
    }
}

/// <summary>A solid vertical run of one column: every integer level y with YBot &lt; y &lt;= YTop.
/// Material is derived from depth below the pristine noise surface, never stored, so a run only
/// needs two integers.</summary>
public struct VoxelRun
{
    public int YBot;
    public int YTop;
}