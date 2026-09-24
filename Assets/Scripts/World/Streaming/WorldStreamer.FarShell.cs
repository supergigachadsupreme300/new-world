using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Far shell (1ef): extends the streamed terrain out to the ~900 m view (30-chunk default since
    /// 1eo; was ~2 km at 67) at a fraction of the
/// object/vertex cost by rendering one coarse sector mesh per cell BEYOND the near real-chunk
/// ring.
///
/// Split of responsibilities around the focus:
///   - Real ring (rings 0..NearRingRadius, default 9): normal full-fidelity ChunkObjects —
///     deformable, collidable, prop-bearing, LOD'd. (StreamAround(near) keeps them loaded one extra
///     ring for hysteresis, i.e. through ring 10.)
///   - Far shell (rings near+1 .. view+FarOuterKeep, default 10..69): one GameObject per cell
///     carrying a decimated grid mesh sampled from the same pure-noise/save corner grid the real
///     chunks use, so the whole map stays watertight. No colliders, no props, no deformation —
///     digs can never reach it (collider ring 8 &lt; rim start 10), so far meshes never re-generate.
///
/// Cell hierarchy (1ej: EVERY cell shares the same world-aligned 3 m grid, so adjacent cells of every
/// span carry coincident edge rows — no T-junction cracks between different-span/different-ring cells):
///   - Span 1 (rim): each chunk ring in [near+1, keep] is its own 3 m-step grid (11x11 verts).
///     This band also owns the "active shadow" rule: real chunks and far cells overlap ONLY at
///     ring near+1 (the StreamAround hysteresis ring) — a span-1 far cell is created there even
///     under a loaded real chunk and simply held inactive (active = !_loadedChunks.ContainsKey),
///     so when the real chunk stream moves on the far mesh shows in the SAME poll it unloads.
///   - Span 3 (band B): 3x3-chunk cells (90 m wide) at ring >= FarBandBMin, step 3 (31x31 verts).
///   - Span 6 (band C): 6x6-chunk cells (180 m wide) at ring >= FarBandCMin, step 3 (61x61 verts).
///   A finer cell is suppressed whenever its coarser parent cell is required, so every annulus
///   chunk belongs to exactly one generated cell. Spread of a span-3 box is <= 2 rings and of a
///   span-6 box <= 5, so a required parent only ever overrides fine cells at >= ring 12 (B) or
///   >= ring 35 (C) — never inside the near/rim bands they would z-fight in.
///
/// Ownership swaps (1eq): requiredness is a hard ring cut relative to the focus, so every 30 m
/// chunk step the player crosses, boxes around rings 13-16 flip between span-1 and span-3
/// ownership. The swap is covered, never exposed: a fine cell stays live as the tenant while its
/// coarser replacement is required-but-not-yet-live (promote retain), a newly live coarser cell
/// hides its finer siblings the poll it is created, and a demoted coarse cell keeps rendering
/// until EVERY finer replacement has been generated, then hands ownership to them in one poll
/// (CompleteFarHandoff). One live owner per region at all times — no hole, no z-fight.
///
/// Generation: the ring is re-walked each poll (cheap int math) into a pending list (deduped),
/// dispatched to the ThreadPool like real chunks (MaxFarInFlight cap) and finalized on the main
/// thread at MaxFarFinalizePerPoll/poll (~320-960 cell meshes/s; the initial ~1,000-cell fill at the
    /// 30-chunk default — 1eo, down from ~1,400 at 67 — takes ~1.5-4 s and coasts in the background
    /// while the player moves). Cells are dispatched
/// near-first (1ek; the pending list is iterated closest-first so the region around the player —
/// where a void is most visible — closes before the distant fringe, which fills a moment later);
/// generation AND retention use the
/// SAME predicate (is-required), so a cell whose ring falls outside keep while in-flight is dropped
/// at finalize instead of being created stale. Removals are capped per poll (MaxFarUnloadsPerPoll)
/// with a backlog flag that keeps the stream working until the excess is destroyed.
///
/// Thread-safety: epoch + seed + step are captured on the main thread and passed by value into the
/// worker (never read shared fields there); workers only touch their own arrays plus statics that
/// are already used by the real background builder (ChunkSaveManager.TryLoadChunk,
/// ChunkMeshGenerator, TerrainNoiseGenerator).
/// </summary>
public partial class WorldStreamer
{
    // --- Far shell bands (chunk Chebyshev rings) ---
    /// <summary>First ring where span-3 cells may appear (>= FarBandBMin so their box is safely
    /// clear of the rim/near bands; spread <= 2 keeps them off rings &lt; 13).</summary>
    private const int FarBandBMin = 15;
    /// <summary>First ring where span-6 cells may appear (>= FarBandCMin; spread <= 5 keeps them
    /// off rings &lt; 35 — never over the real/rim bands). 1eo: not reached at the 30-chunk default
    /// render radius — needs view >= 36 — so span-6 is a high-radius-only band today.</summary>
    private const int FarBandCMin = 36;
    /// <summary>Extra rings beyond the render radius that far cells still generate/retain to, so
    /// the last populated ring clears the nominal view frustum edge with margin.</summary>
    private const int FarOuterKeep = 2;

    // --- Far shell budgets (main thread) ---
    /// <summary>Max far-sector meshes being built on the ThreadPool simultaneously (1ek: raised from
    /// 48 so the near-first dispatch keeps the region around the player filling fast even while the
    /// heavy outer span-6 cells build).</summary>
    private const int MaxFarInFlight = 96;
    /// <summary>Hard cap: max far-sector GameObjects created per poll tick (~320-960/s at 60 fps).</summary>
    private const int MaxFarFinalizePerPoll = 16;
    /// <summary>Soft cap: far-sector creation is additionally time-budgeted per poll (1eh) so a poll
    /// never spends more than this creating GameObjects + uploading meshes on the main thread.</summary>
    private const float FarFinalizeBudgetMs = 2.5f;
    /// <summary>Max far sectors destroyed per poll while the shell shrinks.</summary>
    private const int MaxFarUnloadsPerPoll = 32;
    /// <summary>Consecutive fully-settled polls before the far shell bakes its static batch (1eh).
    /// ~20 polls at the 20 Hz beat ≈ 1 s of a settled shell.</summary>
    private const int FarSettlePollsBeforeBake = 20;
    /// <summary>Far-shell static bake master switch (1eh). DISABLED: the once-combined batch (via
    /// <see cref="StaticBatchingUtility.Combine"/>) rendered far meshes only from below — a back-face/
    /// combined-mesh artifact, so the whole shell renders dynamic again (known-good from 1ef). Re-enable
    /// only after a Unity-side root cause on combined-mesh winding. See §1eh-status in PROGRESS.md.</summary>
    private const bool FarBakeEnabled = false;

    // --- Far shell state ---
    private readonly Dictionary<FarCell, GameObject> _farSectors = new Dictionary<FarCell, GameObject>();
    private readonly ConcurrentDictionary<FarCell, byte> _farInFlight = new ConcurrentDictionary<FarCell, byte>();
    private readonly ConcurrentQueue<FarMeshData> _farReady = new ConcurrentQueue<FarMeshData>();
    private readonly List<FarCell> _farPending = new List<FarCell>();
    private readonly HashSet<FarCell> _farVisited = new HashSet<FarCell>();
    /// <summary>Generation epoch — bumped by <see cref="ClearFarShell"/> so stale worker output is
    /// dropped at finalize instead of creating sectors over a wiped/regenerating world.</summary>
    private int _farEpoch;
    private bool _farUnloadBacklog;
    private Transform _farRoot;
    /// <summary>True once the initial far shell has been static-batched (1eh): the bulk (span-3/6
    /// cells) now render as one combined mesh. Never un-baked; ClearFarShell wipes it wholesale.
    /// Currently inert — see <see cref="FarBakeEnabled"/> (disabled).</summary>
    private bool _farBaked;
    /// <summary>Combined root of the baked span-3/6 cells, or null before the bake.</summary>
    private Transform _farBatchRoot;
    /// <summary>Cells whose geometry is baked into <see cref="_farBatchRoot"/> — skipped by the
    /// removal scan (a combined mesh cannot be torn apart cell by cell).</summary>
    private readonly HashSet<FarCell> _farBakedCells = new HashSet<FarCell>();
    /// <summary>Consecutive polls with a fully settled shell — gates the static bake.</summary>
    private int _farIdlePolls;

    /// <summary>Number of far-shell sector GameObjects currently live (bench readout, 1ef).</summary>
    public int FarSectorCount => _farSectors.Count;

    /// <summary>A coarse far-shell cell: an aligned Span x Span block of terrain chunks.</summary>
    private readonly struct FarCell : System.IEquatable<FarCell>
    {
        public readonly int X;
        public readonly int Z;
        public readonly int Span;

        public FarCell(int x, int z, int span)
        {
            X = x;
            Z = z;
            Span = span;
        }

        public bool Equals(FarCell other) => X == other.X && Z == other.Z && Span == other.Span;
        public override bool Equals(object obj) => obj is FarCell other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X;
                hash = hash * 31 + Z;
                hash = hash * 31 + Span;
                return hash;
            }
        }
        public override string ToString() => $"FarCell({X},{Z},span{Span})";
    }

    /// <summary>Completed far-sector payload handed from a worker to the main thread.</summary>
    private readonly struct FarMeshData
    {
        public readonly FarCell Cell;
        public readonly int Epoch;
        public readonly MergedChunkMeshData Merged;

        public FarMeshData(FarCell cell, int epoch, MergedChunkMeshData merged)
        {
            Cell = cell;
            Epoch = epoch;
            Merged = merged;
        }
    }

    /// <summary>Floor division for negative indices (C# '/' truncates toward zero, which would
    /// mis-align negative chunk coords onto cells).</summary>
    private static int FloorDiv(int a, int b) => (int)System.Math.Floor((double)a / b);

    /// <summary>Chebyshev ring extent of a cell box around the focus. minRing is the distance of
    /// the box's nearest corner; maxRing of its farthest corner (kept since 1ej only for call-site
    /// stability — the survive/reject predicates and the uniform step).</summary>
    private static void FarCellRings(FarCell cell, TerrainChunkCoord centre, out int minRing, out int maxRing)
    {
        int minX = cell.X;
        int maxX = cell.X + cell.Span - 1;
        int minZ = cell.Z;
        int maxZ = cell.Z + cell.Span - 1;
        int cx = centre.X;
        int cz = centre.Z;

        int nearX = cx < minX ? minX - cx : (cx > maxX ? cx - maxX : 0);
        int farX = Mathf.Max(Mathf.Abs(minX - cx), Mathf.Abs(maxX - cx));
        int nearZ = cz < minZ ? minZ - cz : (cz > maxZ ? cz - maxZ : 0);
        int farZ = Mathf.Max(Mathf.Abs(minZ - cz), Mathf.Abs(maxZ - cz));

        minRing = Mathf.Max(nearX, nearZ);
        maxRing = Mathf.Max(farX, farZ);
    }

    /// <summary>Decimation step (metres between grid vertices) for a cell. 1ej: EVERY cell uses the
    /// uniform 3 m step that matches the real Lod2 lattice at the rim. Because sibling cells of every
    /// span sample the same world-anchored corner grids, shared edge rows are coincident — watertight
    /// by construction. The pre-1ej radius ladder (3/6/9/12/15 by maxRing) left different-step
    /// neighbors with T-junction rows along their shared edges, read as permanent cracks ("thin lines
    /// along every chunk edge"), so it was removed. Step 3 divides every tile span (30/90/180), keeping
    /// grid rows exactly on chunk boundaries. <paramref name="span"/>/<paramref name="maxRing"/> remain
    /// for call-site stability (unused).</summary>
    private static int FarSectorStep(int span, int maxRing)
    {
        return 3;
    }

    /// <summary>True when <paramref name="cell"/> must exist as a far sector (generation AND
    /// retention predicate — one rule, so in-flight cells that outlived their ring are dropped at
    /// finalize). A coarser required parent suppresses its finer children, giving every annulus
    /// chunk exactly one owning cell (checks are cheap pure int math on the box rings).</summary>
    private bool RequiredFarCell(FarCell cell, TerrainChunkCoord centre, int near, int keep)
    {
        FarCellRings(cell, centre, out int minRing, out int maxRing);
        if (minRing > keep)
            return false;

        if (cell.Span >= 6)
            return maxRing >= FarBandCMin;

        if (cell.Span >= 3)
            return maxRing >= FarBandBMin
                && !RequiredFarCell(new FarCell(FloorDiv(cell.X, 6) * 6, FloorDiv(cell.Z, 6) * 6, 6), centre, near, keep);

        return maxRing >= near + 1
            && !RequiredFarCell(new FarCell(FloorDiv(cell.X, 3) * 3, FloorDiv(cell.Z, 3) * 3, 3), centre, near, keep)
            && !RequiredFarCell(new FarCell(FloorDiv(cell.X, 6) * 6, FloorDiv(cell.Z, 6) * 6, 6), centre, near, keep);
    }

    /// <summary>The single far cell that owns annulus chunk (x, z), or null when the chunk is
    /// inside the real ring (or past keep). Tries the span-6 parent, then span-3, then the span-1
    /// rim cell — exactly the same predicate the retention scan uses. Deliberately no loaded-chunk
    /// check here: rim cells must also exist as shadow under loaded ring-10 real chunks.</summary>
    private FarCell? FarCellForChunk(int x, int z, TerrainChunkCoord centre, int near, int keep)
    {
        int ring = Mathf.Max(Mathf.Abs(x - centre.X), Mathf.Abs(z - centre.Z));

        FarCell parent6 = new FarCell(FloorDiv(x, 6) * 6, FloorDiv(z, 6) * 6, 6);
        if (RequiredFarCell(parent6, centre, near, keep))
            return parent6;

        FarCell parent3 = new FarCell(FloorDiv(x, 3) * 3, FloorDiv(z, 3) * 3, 3);
        if (RequiredFarCell(parent3, centre, near, keep))
            return parent3;

        if (ring >= near + 1 && ring <= keep)
            return new FarCell(x, z, 1);
        return null;
    }

    /// <summary>True when a coarser far cell that owns this footprint is already live (1eq), so
    /// <paramref name="cell"/> must render nothing — it is a reserved shadow that takes over the
    /// instant the coarser owner leaves (promote-hide keeps the pair invisible until the removal
    /// scan destroys the fine cell; the demote handoff activates these shadows atomically). Checks
    /// EVERY coarser level, so a span-1 under a live span-6 is shadowed even with no span-3 in
    /// between.</summary>
    private bool FarShadowedByCoarse(FarCell cell)
    {
        if (cell.Span >= 6)
            return false;
        FarCell p6 = new FarCell(FloorDiv(cell.X, 6) * 6, FloorDiv(cell.Z, 6) * 6, 6);
        if (_farSectors.ContainsKey(p6))
            return true;
        if (cell.Span >= 3)
            return false;
        FarCell p3 = new FarCell(FloorDiv(cell.X, 3) * 3, FloorDiv(cell.Z, 3) * 3, 3);
        return _farSectors.ContainsKey(p3);
    }

    /// <summary>True when every next-finer cell the current ownership predicate would assign to
    /// <paramref name="cell"/>'s footprint is already live (1eq demote gate). A demoted coarse cell
    /// must keep rendering as the tenant until ALL of its replacements exist — destroying it sooner
    /// would open a hole for the async rebuild window.</summary>
    private bool FarCoverageReady(FarCell cell, TerrainChunkCoord centre, int near, int keep)
    {
        bool span3 = cell.Span == 3;
        int axis = span3 ? 3 : 2;
        int step = span3 ? 1 : 3;
        int childSpan = span3 ? 1 : 3;
        for (int j = 0; j < axis; j++)
        {
            for (int i = 0; i < axis; i++)
            {
                FarCell child = new FarCell(cell.X + i * step, cell.Z + j * step, childSpan);
                if (!RequiredFarCell(child, centre, near, keep))
                    continue;
                if (!_farSectors.ContainsKey(child))
                    return false;
            }
        }
        return true;
    }

    /// <summary>Hide every finer cell still registered inside <paramref name="cell"/>'s footprint —
    /// a newly live coarser owner takes over without a frame of overlapping render (1eq promote
    /// handoff). The hidden fine cells are stale and destroyed by the next removal scan.</summary>
    private void HideFinerChildren(FarCell cell)
    {
        bool span3 = cell.Span == 3;
        int axis = span3 ? 3 : 2;
        int step = span3 ? 1 : 3;
        int childSpan = span3 ? 1 : 3;
        for (int j = 0; j < axis; j++)
        {
            for (int i = 0; i < axis; i++)
            {
                FarCell child = new FarCell(cell.X + i * step, cell.Z + j * step, childSpan);
                if (_farSectors.TryGetValue(child, out GameObject g) && g != null && g.activeSelf)
                    g.SetActive(false);
            }
        }
        if (!span3)
        {
            // span-1 grandchildren under the span-3 children (nested case, high-radius only).
            for (int j = 0; j < 6; j++)
            {
                for (int i = 0; i < 6; i++)
                {
                    FarCell g = new FarCell(cell.X + i, cell.Z + j, 1);
                    if (_farSectors.TryGetValue(g, out GameObject gg) && gg != null && gg.activeSelf)
                        gg.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Atomically swap a demoted coarse cell's ownership to its (already generated, currently hidden)
    /// finer replacement cells (1eq): remove the tenant from <see cref="_farSectors"/>, activate
    /// every replacement that is not itself shadowed by a loaded real chunk or a live coarser owner,
    /// then tear the tenant's GameObject down. All in one main-thread poll, so the region always has
    /// exactly one live owner — the swap exposes neither a hole nor double-drawn ground.
    /// </summary>
    private void CompleteFarHandoff(FarCell cell)
    {
        if (!_farSectors.TryGetValue(cell, out GameObject tenant))
            return;
        _farSectors.Remove(cell);

        bool span3 = cell.Span == 3;
        int axis = span3 ? 3 : 2;
        int step = span3 ? 1 : 3;
        int childSpan = span3 ? 1 : 3;
        for (int j = 0; j < axis; j++)
        {
            for (int i = 0; i < axis; i++)
            {
                FarCell child = new FarCell(cell.X + i * step, cell.Z + j * step, childSpan);
                if (!_farSectors.TryGetValue(child, out GameObject g) || g == null)
                    continue;
                bool shadow = FarShadowedByCoarse(child)
                    || (childSpan == 1 && _loadedChunks.ContainsKey(new TerrainChunkCoord(child.X, child.Z)));
                if (g.activeSelf == shadow)
                    g.SetActive(!shadow);
            }
        }

        // Tear down the removed tenant (its pooled mesh returns to the shared cache).
        var mf = tenant.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            ChunkMeshGenerator.ReleaseChunkMesh(mf.sharedMesh);
        Destroy(tenant);
    }

    /// <summary>
    /// Per-poll far-shell pass, run AFTER <see cref="FinalizeChunks"/> so a just-materialized real
    /// chunk shadows its rim cell in the same Update. Steps:
    ///   (1) sync the rim shadow state (span-1 cells under loaded real chunks stay inactive),
    ///       then collect/shape the removal list (rim cells are kept while their real chunk is
    ///       queued or in-flight so the approach edge never shows a hole), capped per poll with a
    ///       backlog flag;
    ///   (2) re-walk the annulus ring near+1..keep into a deduped pending list;
    ///   (3) dispatch to the ThreadPool (MaxFarInFlight), passing seed/epoch/step by value;
    ///   (4) finalize up to MaxFarFinalizePerPoll — dropping stale epochs and cells that are no
    ///       longer required, then creating the sector GameObject.
    /// </summary>
    private void FarShellTick(TerrainChunkCoord centre, int view, int near)
    {
        int keep = view + FarOuterKeep;

        // (1a) Active-shadow sync: a cell renders only where nothing covers it — a span-1 rim cell
        // under a loaded real chunk, and any cell under a live coarser far owner (1eq ownership
        // handoff), stay inactive. A real chunk loads (FinalizeChunks already ran) -> its far cell
        // goes inactive this poll; a real chunk unloads -> its far cell activates this poll. Zero
        // hole, zero z-fight at ring near+1 and during fine/coarse ownership swaps.
        foreach (KeyValuePair<FarCell, GameObject> kv in _farSectors)
        {
            FarCell cell = kv.Key;
            bool wantActive = true;
            if (cell.Span == 1 && _loadedChunks.ContainsKey(new TerrainChunkCoord(cell.X, cell.Z)))
                wantActive = false;
            if (FarShadowedByCoarse(cell))
                wantActive = false;
            if (kv.Value != null && kv.Value.activeSelf != wantActive)
                kv.Value.SetActive(wantActive);
        }

        // (1b) Removal scan. Skip required cells; rim cells also live while their real chunk is
        // mid-stream so the shell never opens a hole ahead of a materializing chunk.
        // 1eq: ownership changes around the span-1/span-3 boundary (every chunk step the focus
        // crosses, boxes flip requiredness around rings 13-16) must never expose the ground. A fine
        // cell stays live as the tenant while its coarser replacement is required but not yet
        // created (promote retain), and a demoted coarse cell stays live until EVERY finer
        // replacement has been created, then hands ownership to them in one poll. Without this the
        // removal scan would destroy the covering cell in the same poll the replacement is only
        // enqueued, opening a visible hole for the async rebuild window (~50-400 ms).
        List<FarCell> stale = null;
        List<FarCell> handoffs = null;
        int removed = 0;
        foreach (KeyValuePair<FarCell, GameObject> kv in _farSectors)
        {
            FarCell cell = kv.Key;
            if (RequiredFarCell(cell, centre, near, keep))
                continue;
            if (_farBakedCells.Contains(cell))
                continue;
            if (cell.Span == 1)
            {
                TerrainChunkCoord tc = new TerrainChunkCoord(cell.X, cell.Z);
                if (_pendingChunks.Contains(tc) || _chunksInFlight.ContainsKey(tc))
                    continue;
            }
            // Promote retain: keep a fine cell (or a span-3 under a newly required span-6) rendering
            // while its coarser replacement is required but not yet live. The check is the same
            // ownership predicate the ring walk uses, so the tenant and its replacement never both
            // draw and never leave the region uncovered.
            FarCell? owner = FarCellForChunk(cell.X, cell.Z, centre, near, keep);
            if (owner.HasValue && owner.Value.Span > cell.Span && !_farSectors.ContainsKey(owner.Value))
                continue;
            if (cell.Span >= 3)
            {
                // A live coarser owner (owner.Value.Span > cell.Span — present here because the
                // promote-retain above already kept the not-yet-live case) renders this footprint
                // itself, and this cell is hidden by the active-shadow sync — plain destroy below
                // is safe and the ground stays covered.
                bool coveredByLiveCoarse = owner.HasValue && owner.Value.Span > cell.Span;
                if (!coveredByLiveCoarse && !FarCoverageReady(cell, centre, near, keep))
                {
                    // Demote tenant retain: ownership is passing to this cell's FINER children
                    // (span-1 cells, or span-3 cells for a span-6 at high radius), which are
                    // generated asynchronously. Keep rendering the ground until EVERY required
                    // replacement exists — destroying it now would open a hole for the async
                    // rebuild window (~50-400 ms) that reads as far terrain blinking away and back
                    // while the player moves.
                    continue;
                }
                if (!coveredByLiveCoarse)
                {
                    // Every replacement is live: hand ownership to them atomically in one poll.
                    if (handoffs == null) handoffs = new List<FarCell>();
                    handoffs.Add(cell);
                    if (++removed >= MaxFarUnloadsPerPoll)
                    {
                        _farUnloadBacklog = true;
                        break;
                    }
                    continue;
                }
                // coveredByLiveCoarse: fall through to the stale list (destroyed, already hidden).
            }
            if (stale == null) stale = new List<FarCell>();
            stale.Add(cell);
            if (++removed >= MaxFarUnloadsPerPoll)
            {
                _farUnloadBacklog = true;
                break;
            }
        }
        if (stale != null)
        {
            for (int i = 0; i < stale.Count; i++)
                DestroyFarSector(stale[i]);
        }
        if (handoffs != null)
        {
            for (int i = 0; i < handoffs.Count; i++)
                CompleteFarHandoff(handoffs[i]);
        }
        if (removed < MaxFarUnloadsPerPoll)
            _farUnloadBacklog = false;

        // (2) Ring walk near+1..keep, closest first (same 4-edge pattern as StreamAround).
        _farPending.Clear();
        _farVisited.Clear();
        for (int r = near + 1; r <= keep; r++)
        {
            int startX = centre.X - r, endX = centre.X + r;
            int startZ = centre.Z - r, endZ = centre.Z + r;

            for (int x = startX; x <= endX; x++)
                FarConsiderCell(new TerrainChunkCoord(x, endZ), centre, near, keep);
            for (int x = startX; x <= endX; x++)
                FarConsiderCell(new TerrainChunkCoord(x, startZ), centre, near, keep);
            for (int z = startZ + 1; z < endZ; z++)
                FarConsiderCell(new TerrainChunkCoord(startX, z), centre, near, keep);
            for (int z = startZ + 1; z < endZ; z++)
                FarConsiderCell(new TerrainChunkCoord(endX, z), centre, near, keep);
        }

        // (3) Dispatch. State captured up front: step (uniform 3 m since 1ej) + span passed by value
        // on the worker, epoch from the field read now. The pending list is walked closest-first,
        // so iterating it FORWARD (1ek) dispatches the rim/near cells that surround the player FIRST —
        // the void around the player closes immediately and the distant fringe fills a moment later.
        // (Pre-1ek this iterated in reverse — horizon-first — which let the heavy outer span-6 cells
        // hog every flight slot and starved the near cells into a permanent-looking empty ring.)
        long seed = Seed;
        int epoch = _farEpoch;
        for (int i = 0; i < _farPending.Count && _farInFlight.Count < MaxFarInFlight; i++)
        {
            FarCell cell = _farPending[i];
            if (!_farInFlight.TryAdd(cell, 0))
                continue;
            FarCellRings(cell, centre, out _, out int maxRing);
            ThreadPool.QueueUserWorkItem(_ => BackgroundGenerateFarCell(cell, seed, epoch, maxRing));
        }

        // (4) Finalize. Time-budgeted (1eh): the 1eg throughput stays for the fast fill, but a single
        // poll never spends more than FarFinalizeBudgetMs creating GameObjects + uploading meshes on
        // the main thread (mirrors the real-chunk adaptive budget in WorldStreamer.Mesh.cs).
        System.Diagnostics.Stopwatch farFinalizeSw = System.Diagnostics.Stopwatch.StartNew();
        int finalized = 0;
        while (finalized < MaxFarFinalizePerPoll && _farReady.TryDequeue(out FarMeshData data))
        {
            byte _;
            _farInFlight.TryRemove(data.Cell, out _);
            if (data.Epoch != _farEpoch)
                continue;                       // stale generation after ClearFarShell
            if (!RequiredFarCell(data.Cell, centre, near, keep))
                continue;                       // focus moved past it while generating
            CreateFarSector(data.Cell, data.Merged);
            finalized++;
            if (farFinalizeSw.Elapsed.TotalMilliseconds >= FarFinalizeBudgetMs)
                break;
        }

        // (5) Static bake (1eh): once the shell has fully settled (and only while baking is enabled),
        // merge all span-3/6 cells into one combined mesh — the single biggest far-shell draw-call cut.
        // Span-1 rim cells stay dynamic (they own the active shadow and must be able to hide under a
        // loaded real chunk). Baked cells stay live on shrink (never torn out of the combined mesh) and
        // ClearFarShell wipes the batch wholesale. DISABLED for now: the combined batch rendered only
        // from below, so every generated cell is left dynamic on its own mesh.
        if (!FarBakeEnabled)
            return;
        if (_farPending.Count == 0 && _farInFlight.Count == 0 && _farReady.IsEmpty && !_farUnloadBacklog)
        {
            if (++_farIdlePolls >= FarSettlePollsBeforeBake)
                TryBakeFarShell();
        }
        else
        {
            _farIdlePolls = 0;
        }
    }

    /// <summary>Map one annulus chunk to its owning far cell and add it to the pending queue unless
    /// it already exists/queued/in-flight.</summary>
    private void FarConsiderCell(TerrainChunkCoord tc, TerrainChunkCoord centre, int near, int keep)
    {
        FarCell? maybe = FarCellForChunk(tc.X, tc.Z, centre, near, keep);
        if (maybe == null)
            return;
        FarCell cell = maybe.Value;
        if (!_farVisited.Add(cell))
            return;
        if (_farSectors.ContainsKey(cell))
            return;
        if (_farInFlight.ContainsKey(cell))
            return;
        _farPending.Add(cell);
    }

    /// <summary>ThreadPool entry: builds the sector's merged arrays and queues them for the main
    /// thread. Failure removes the in-flight mark so the cell can be re-dispatched next poll.</summary>
    private void BackgroundGenerateFarCell(FarCell cell, long seed, int epoch, int maxRing)
    {
        try
        {
            _farReady.Enqueue(new FarMeshData(cell, epoch, BuildFarSector(cell, seed, maxRing)));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WorldStreamer] Far shell generation failed for {cell}: {ex.Message}");
            byte _;
            _farInFlight.TryRemove(cell, out _);
        }
    }

    /// <summary>
    /// Builds one far sector's decimated grid mesh on a worker thread. Heights come from the SAME
    /// per-chunk corner grid the real chunks use (disk save stamps + pure-noise regeneration), so
    /// the far surface matches what the real chunks would show and seams against them are exact.
    /// Sampled on the uniform 3 m step (1ej; <paramref name="maxRing"/> kept for signature stability);
    /// vertex colors use the memoized band
    /// lookup so far terrain keeps the grass/dirt/stone strata read.
    /// </summary>
    private MergedChunkMeshData BuildFarSector(FarCell cell, long seed, int maxRing)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int span = cell.Span;
        int tilesPerAxis = span * cs;
        int step = FarSectorStep(span, maxRing);
        int axis = tilesPerAxis / step + 1;

        // One (gridSize x gridSize) corner grid per covered chunk (build outside the vertex loop so
        // shared edges between sibling cells see identical worlds). Index: row-major over the chunks.
        // cell.X/Z are the block's MIN chunk coordinate in chunk units (1el fix: they were multiplied
        // by span here and in CreateFarSector, so every span-3/6 cell sampled + rendered 3x/6x further
        // out — the far shell's mid-band stayed permanently empty). The owning-cell math
        // (FarCellForChunk/RequiredFarCell/FarCellRings) was already chunk-min based, so only this
        // build + the placement were wrong.
        float[,][,] grids = new float[span, span][,];
        for (int cz = 0; cz < span; cz++)
        {
            for (int cx = 0; cx < span; cx++)
                grids[cz, cx] = BuildFarChunkCorners(
                    new TerrainChunkCoord(cell.X + cx, cell.Z + cz), seed);
        }

        float SampleHeight(int gx, int gz)
        {
            int t = gx * step;
            int cxi = Mathf.Min(t / cs, span - 1);
            int lx = t - cxi * cs;
            int u = gz * step;
            int czi = Mathf.Min(u / cs, span - 1);
            int lz = u - czi * cs;
            return ChunkMeshGenerator.SanitizeHeight(grids[czi, cxi][lx, lz]);
        }

        // 1ek: at the cell's edge rows/cols the slope's "beyond" side falls outside this cell's
        // chunk grids, so it is sampled directly from the pure world heights. The far band has no
        // save mods (collider ring 8 < rim start 10, digs can never reach it), so direct GetHeight
        // equals exactly what the neighboring cell's grid holds there — both cells then compute
        // byte-identical boundary normals and no lighting crease shows along any shared edge
        // (or at the rim/real-junction).
        float WorldHeight(int tileX, int tileZ)
        {
            return ChunkMeshGenerator.SanitizeHeight(TerrainNoiseGenerator.GetHeight(seed, tileX, tileZ));
        }

        int count = axis * axis;
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var uvs = new Vector2[count];
        var colors = new Color[count];

        // Memoized pristine heights for the band colors exactly like the merged chunk builder.
        var heightMemo = new Dictionary<long, float>(count);
        // Tile-unit origin of the block's MIN chunk (1el: was cell.X * span, matching the fixed
        // chunk mapping above — cell.X is already the min chunk coordinate).
        int cellTileOriginX = cell.X * cs;
        int cellTileOriginZ = cell.Z * cs;

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int gz = 0, v = 0; gz < axis; gz++)
        {
            for (int gx = 0; gx < axis; gx++, v++)
            {
                float h = SampleHeight(gx, gz);
                int wx = cellTileOriginX + gx * step;
                int wz = cellTileOriginZ + gz * step;
                vertices[v] = new Vector3(gx * step, h, gz * step);
                colors[v] = ChunkMeshGenerator.TerrainBandColor(seed, wx, wz, h, heightMemo);
                uvs[v] = Vector2.zero;

                // Central-difference slope normals (spacing = step). Interiors read the chunk grids
                // as before; the clamped seam side is pulled across the boundary via WorldHeight so
                // both cells at a shared row agree exactly (no seam crease, no T-junction lighting).
                int a = gx - 1, b = gx + 1;
                int c = gz - 1, d = gz + 1;
                float hl = a < 0 ? WorldHeight(wx - step, wz) : SampleHeight(a, gz);
                float hr = b >= axis ? WorldHeight(wx + step, wz) : SampleHeight(b, gz);
                float hu = c < 0 ? WorldHeight(wx, wz - step) : SampleHeight(gx, c);
                float hd = d >= axis ? WorldHeight(wx, wz + step) : SampleHeight(gx, d);
                float dhdx = (hr - hl) / (2f * step);
                float dhdz = (hd - hu) / (2f * step);
                normals[v] = new Vector3(-dhdx, 1f, -dhdz).normalized;

                if (h < minY) minY = h;
                if (h > maxY) maxY = h;
            }
        }

        // Same grid winding as the chunk LOD children (BuildLodChild): +X is the next column,
        // +Z is the next row.
        int[] triangles = new int[(axis - 1) * (axis - 1) * 6];
        for (int gz = 0, t = 0; gz < axis - 1; gz++)
        {
            for (int gx = 0; gx < axis - 1; gx++)
            {
                int i00 = gz * axis + gx;
                int i10 = i00 + 1;
                int i01 = i00 + axis;
                int i11 = i01 + 1;
                triangles[t++] = i00; triangles[t++] = i10; triangles[t++] = i11;
                triangles[t++] = i00; triangles[t++] = i11; triangles[t++] = i01;
            }
        }

        float spanM = span * cs * ChunkData.Size;
        Bounds bounds = new Bounds(
            new Vector3(spanM * 0.5f, minY < maxY ? (minY + maxY) * 0.5f : minY, spanM * 0.5f),
            new Vector3(spanM, Mathf.Max(0.1f, minY < maxY ? (maxY - minY) + 0.1f : 0.1f), spanM));

        return new MergedChunkMeshData
        {
            Vertices = vertices,
            Triangles = triangles,
            UV = uvs,
            Normals = normals,
            Colors = colors,
            Bounds = bounds,
        };
    }

    /// <summary>
    /// Per-chunk 31x31 corner grid for the far shell: a deliberate duplicate of the real chunk
    /// builder's load/noise path (BuildOrLoadChunk) because that method couples the corner grid to
    /// the 900-tile mesh pass. Same NaN seed, same IsSaneHeight-gated save stamps, same 5-octave
    /// noise fill, so far cells render the true world surface (including the player's saved digs).
    /// Static/thread-safe like its source.
    /// </summary>
    private float[,] BuildFarChunkCorners(TerrainChunkCoord tc, long seed)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int gridSize = TerrainChunkCoord.CornerGridSize;

        bool loaded = ChunkSaveManager.TryLoadChunk(seed, tc, out ChunkSaveData save);
        Dictionary<int, ChunkTileMod> mods = null;
        if (loaded && save.Mods.Count > 0)
        {
            mods = new Dictionary<int, ChunkTileMod>();
            for (int i = 0; i < save.Mods.Count; i++)
            {
                ChunkTileMod m = save.Mods[i];
                if (m.LocalX >= 0 && m.LocalX < cs && m.LocalZ >= 0 && m.LocalZ < cs)
                    mods[m.LocalZ * cs + m.LocalX] = m;
            }
        }

        float[,] corners = new float[gridSize, gridSize];
        for (int gi = 0; gi < gridSize * gridSize; gi++)
            corners[gi / gridSize, gi % gridSize] = float.NaN;
        if (mods != null)
        {
            foreach (KeyValuePair<int, ChunkTileMod> kv in mods)
            {
                ChunkTileMod m = kv.Value;
                if (m.Heights == null || m.Heights.Length < ChunkData.VertexCount)
                    continue;
                corners[m.LocalX, m.LocalZ + 1] = IsSaneHeight(m.Heights[0]) ? m.Heights[0] : float.NaN;     // NW
                corners[m.LocalX + 1, m.LocalZ + 1] = IsSaneHeight(m.Heights[1]) ? m.Heights[1] : float.NaN; // NE
                corners[m.LocalX + 1, m.LocalZ] = IsSaneHeight(m.Heights[2]) ? m.Heights[2] : float.NaN;     // SE
                corners[m.LocalX, m.LocalZ] = IsSaneHeight(m.Heights[3]) ? m.Heights[3] : float.NaN;         // SW
            }
        }
        for (int gz = 0; gz < gridSize; gz++)
        {
            for (int gx = 0; gx < gridSize; gx++)
            {
                bool missing = mods == null || float.IsNaN(corners[gx, gz]);
                if (missing)
                {
                    float worldX = (tc.X * cs + gx) * ChunkData.Size;
                    float worldZ = (tc.Z * cs + gz) * ChunkData.Size;
                    corners[gx, gz] = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
                }
            }
        }
        return corners;
    }

    /// <summary>Create the far-sector GameObject (static, pooled mesh, shared ground material).
    /// Span-1 cells start inactive when their real chunk is present (active shadow).</summary>
    private void CreateFarSector(FarCell cell, MergedChunkMeshData merged)
    {
        var go = new GameObject($"FarCell_{cell.X}_{cell.Z}_{cell.Span}");
        go.isStatic = true;
        go.transform.SetParent(EnsureFarRoot(), false);
        // Block-origin placement in world units. cell.X/Z are the block's MIN chunk coordinate and a
        // chunk is ChunkSize*Size metres, so position = cell.X * 30 exactly (1el: the old
        // cell.X * cell.Span * … misplaced every span-3/6 cell to 3x/6x its true block). Span-1 rim
        // cells are unaffected (Span == 1 makes the two formulas identical).
        go.transform.position = new Vector3(
            cell.X * TerrainChunkCoord.ChunkSize * ChunkData.Size,
            0f,
            cell.Z * TerrainChunkCoord.ChunkSize * ChunkData.Size);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        Mesh mesh = ChunkMeshGenerator.AcquireChunkMesh($"FarMesh_{cell.X}_{cell.Z}_{cell.Span}");
        ChunkMeshGenerator.UploadMerged(merged, mesh);
        mf.sharedMesh = mesh;
        // Far cells render with the double-sided (Cull Off) variant (1ei) — their tops show even if a
        // mesh's winding/culling would hide the upper face. They cast NO shadows (1ei): ~1,000 cells at
        // 270 m+ contribute nothing to the sun's shadow map, so dropping them from the shadow pass is a
        // large per-frame cut with zero gameplay change.
        mr.sharedMaterial = FarGroundMaterial != null ? FarGroundMaterial : GroundMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Active-shadow discipline (1eq): a cell that is already covered — by a loaded real chunk
        // (rim) or by a live coarser far owner (ownership handoff) — is created hidden and merely
        // reserved for the moment the covering owner leaves, so a swap never exposes a hole.
        if (FarShadowedByCoarse(cell)
            || (cell.Span == 1 && _loadedChunks.ContainsKey(new TerrainChunkCoord(cell.X, cell.Z))))
            go.SetActive(false);

        _farSectors.Add(cell, go);

        // Promote handoff: this newly live coarser owner takes over its footprint NOW — hide any
        // finer cells still registered inside it (they are stale and the next removal scan destroys
        // them), so a coarse/fine pair never renders the same ground simultaneously.
        if (cell.Span >= 3)
            HideFinerChildren(cell);
    }

    /// <summary>Tear down one far sector, returning its mesh to the pooled-mesh cache.</summary>
    private void DestroyFarSector(FarCell cell)
    {
        if (!_farSectors.TryGetValue(cell, out GameObject go))
            return;
        _farSectors.Remove(cell);
        if (go != null)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                ChunkMeshGenerator.ReleaseChunkMesh(mf.sharedMesh);
            Destroy(go);
        }
    }

    private Transform EnsureFarRoot()
    {
        if (_terrainRoot == null)
            _terrainRoot = new GameObject("Terrain").transform;
        if (_farRoot == null)
        {
            _farRoot = new GameObject("FarShell").transform;
            _farRoot.SetParent(_terrainRoot, false);
        }
        return _farRoot;
    }

    /// <summary>
    /// Static-batches the settled far shell (1eh): moves every span-3/6 sector under a fresh root
    /// and calls <see cref="StaticBatchingUtility.Combine"/> so the bulk (~1,200 cells) renders as
    /// ONE combined mesh — the biggest far-shell draw-call cut, with perf win also for moving, since
    /// the combined geometry is dispatched as a single renderer. Span-1 rim cells are excluded: they
    /// own the active shadow (toggle active under loaded real chunks) and baking them would keep
    /// drawing under a materialized chunk. Baked cells are never torn out of the combined mesh (the
    /// removal scan skips <see cref="_farBakedCells"/>) and are wiped wholesale by
    /// <see cref="ClearFarShell"/>. Cells spawned later (player movement) stay dynamic. On failure
    /// the moved cells are re-parented and the shell keeps running dynamic.
    /// </summary>
    private void TryBakeFarShell()
    {
        if (!FarBakeEnabled)
            return;
        if (_farBaked || _farSectors.Count == 0)
            return;

        var root = new GameObject("FarBaked");
        root.transform.SetParent(_farRoot != null ? _farRoot : EnsureFarRoot(), false);

        var moved = new List<GameObject>();
        try
        {
            foreach (KeyValuePair<FarCell, GameObject> kv in _farSectors)
            {
                if (kv.Key.Span == 1 || kv.Value == null)
                    continue;
                if (kv.Value.GetComponent<MeshRenderer>() == null)
                    continue;
                kv.Value.transform.SetParent(root.transform, true);
                _farBakedCells.Add(kv.Key);
                moved.Add(kv.Value);
            }
            if (moved.Count == 0)
            {
                Destroy(root);
                return;
            }
            StaticBatchingUtility.Combine(root);
            _farBatchRoot = root.transform;
            _farBaked = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WorldStreamer] Far shell static bake failed: {ex.Message}");
            for (int i = 0; i < moved.Count; i++)
            {
                if (moved[i] != null && moved[i].transform.parent == root.transform)
                    moved[i].transform.SetParent(EnsureFarRoot(), true);
            }
            _farBakedCells.Clear();
            _farBatchRoot = null;
            _farBaked = false;
            if (root != null)
                Destroy(root);
        }
    }

    /// <summary>
    /// Wipe the whole far shell (world/save reset path): bump the epoch so any in-flight or queued
    /// generation is dropped at finalize, destroy every sector, clear all queues, and re-arm the
    /// streaming poll so the shell regenerates from the pristine world next tick.
    /// </summary>
    public void ClearFarShell()
    {
        _farEpoch++;
        // Baked batch first: its cells' MeshFilters may point at the combined mesh, so they are
        // destroyed WITHOUT returning anything to the pooled-mesh cache (never pool a combined mesh).
        if (_farBakedCells.Count > 0)
        {
            var baked = new List<FarCell>(_farBakedCells);
            for (int i = 0; i < baked.Count; i++)
            {
                if (_farSectors.TryGetValue(baked[i], out GameObject bgo) && bgo != null)
                    Destroy(bgo);
                _farSectors.Remove(baked[i]);
            }
            _farBakedCells.Clear();
            if (_farBatchRoot != null)
                Destroy(_farBatchRoot.gameObject);
            _farBatchRoot = null;
            _farBaked = false;
        }
        if (_farSectors.Count > 0)
        {
            var all = new List<FarCell>(_farSectors.Keys);
            for (int i = 0; i < all.Count; i++)
                DestroyFarSector(all[i]);
        }
        _farPending.Clear();
        _farVisited.Clear();
        _farInFlight.Clear();
        while (_farReady.TryDequeue(out _))
        {
        }
        _farUnloadBacklog = false;
        _farIdlePolls = 0;
        _worldDirty = true;
    }
}
