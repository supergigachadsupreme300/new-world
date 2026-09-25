using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Chunk streaming + lifecycle dispatch portion of the WorldStreamer partial class.
/// </summary>

public partial class WorldStreamer
{

    // --- Chunk-level streaming ---

    /// <summary>Max ChunkObjects released by the StreamAround DEEP-UNLOAD sweep per poll (1es,
    /// reframed by 1gc). 1gc demotes each out-of-ring chunk to a dormant hide instead of destroying
    /// it, so the trailing-edge sweep no longer tears anything down; only a chunk past the dormant
    /// band (keep + DormantRingDepth) goes through UnloadChunk. That still walks 900 tiles x 3
    /// dictionary removals plus the mesh/LOD prop teardown, so the cap of 6 spreads a deep crossing's
    /// drain over ~4 polls (~0.2 s); <see cref="_chunkUnloadBacklog"/> keeps the poll alive until
    /// every last one is gone.</summary>
    private const int MaxChunkUnloadsPerPoll = 6;

    /// <summary>True while out-of-ring chunks still await the capped unload sweep (1es). Works like
    /// <see cref="_farUnloadBacklog"/>: participates in the Update idle gate so a partially-drained
    /// crossing keeps polling — <see cref="StreamAround"/> re-collects the remainder each poll and
    /// clears the flag once the sweep fully catches up.</summary>
    private bool _chunkUnloadBacklog;

    // (1gd) Per-poll temp lists, pooled so a live poll never allocates transient lists for the
    // wake/demote/deep-unload scans or the rebuild dispatch scan. Each is used within a single call
    // and cleared by the caller before reuse.
    private readonly List<TerrainChunkCoord> _tempWake = new List<TerrainChunkCoord>();
    private readonly List<TerrainChunkCoord> _tempDemote = new List<TerrainChunkCoord>();
    private readonly List<TerrainChunkCoord> _tempUnload = new List<TerrainChunkCoord>();
    private readonly List<TerrainChunkCoord> _tempRebuildScan = new List<TerrainChunkCoord>();

    // (1gd) Hard wall-clock interrupter for the deep-unload sweep (ms). Even ONE UnloadChunk can
    // run past 1 ms (~900-tile teardown); the slice bounds the sweep's effect on the poll that owns
    // it. The cap (MaxChunkUnloadsPerPoll) and the backlog flag still spread the drain over polls;
    // the slice just adds the slow-machine/edge-case bound.
    private const float DeepUnloadSliceMs = 1.2f;

    // --- Async seam rebuild (1gd) ---
    //
    // A chunk that loads into territory the player previously edited (or sits next to such a chunk
    // — modified BORDER corners) must re-emit its merged surface with the REAL neighbour border
    // corners so cross-chunk slab walls are seamless. Before 1gd this ran synchronously on the main
    // thread: ReconcileNewlyLoadedChunk -> FullRebuildChunk, a 900-tile re-emit + merged-mesh merge
    // + full upload, and ReconcileModifiedBorders could chain several of them, fired EVERY poll the
    // stream passed an edited chunk. At high player speed that stacked into the "immense lag".
    // 1gd moves the heavy part (per-tile re-emit + merged-mesh merge) onto ThreadPool workers and
    // leaves the main thread only a budgeted finalize cap (MaxRebuildFinalizePerPoll) that reuses
    // the shared StreamBudget accounting. Loading speed is unchanged — edges still load at the same
    // rate; only the seam fixes stop holding the gameplay frame. The flow mirrors the existing
    // BuildOrLoadChunk pipeline: request -> snapshot job on the main thread -> worker builds mesh
    // data off-thread -> main thread drains and uploads.
    //
    // Thread-safety contract (mirrors ChunkBuild): the worker reads only the exact fields
    // ChunkMeshGenerator reads (Heights float[], IsValid, ChunkX/ChunkZ, Seed) from raw ChunkData
    // references plus an immutable border-corners dictionary. It NEVER touches _loadedData,
    // _loadedChunks or any ChunkObject. The main thread must not mutate a snapped chunk's tiles
    // while jobs are in flight (same discipline as a generated chunk awaiting finalize).
    private readonly HashSet<TerrainChunkCoord> _rebuildPending = new HashSet<TerrainChunkCoord>();
    private readonly ConcurrentDictionary<TerrainChunkCoord, byte> _rebuildsInFlight = new ConcurrentDictionary<TerrainChunkCoord, byte>();
    private readonly ConcurrentQueue<ChunkRebuildResult> _readyRebuilds = new ConcurrentQueue<ChunkRebuildResult>();

    /// <summary>Max seam rebuilds generating on ThreadPool workers at once (1gd). Small cap — each
    /// is a full chunk re-emit that competes with the chunk-generation pool for CPU.</summary>
    private const int MaxRebuildInFlight = 8;

    /// <summary>Max rebuild results applied (mesh + collider upload) per poll (1gd). They reuse the
    /// shared <see cref="SpendStreamBudget"/> accounting, so a trailing rebuild never stacks a full
    /// upload burst on a busy poll.</summary>
    private const int MaxRebuildFinalizePerPoll = 3;

    /// <summary>QA readout: seam rebuilds in the back-queue (used by the bench HUD).</summary>
    public int RebuildPendingCount => _rebuildPending.Count;

    /// <summary>
    /// Requests an asynchronous full rebuild of <paramref name="tc"/> so its merged surface uses the
    /// CURRENT neighbour border corners (edited terrain staying seamless as the stream passes). The
    /// heavy 900-tile re-emit + merge run on a ThreadPool worker; the result is applied later under
    /// the shared finalize budget (see <see cref="DrainRebuildResults"/>). Dedupes against pending
    /// and in-flight work. Voxel-mode chunks (opt-in experimental) keep the old synchronous path so
    /// the voxel mesh pipeline is never bypassed.
    /// </summary>
    private void RequestChunkRebuild(TerrainChunkCoord tc)
    {
        if (!_loadedChunks.ContainsKey(tc))
            return;
        if (VoxelTerrainEnabled)
        {
            FullRebuildChunk(tc);
            return;
        }
        if (_rebuildPending.Contains(tc) || _rebuildsInFlight.ContainsKey(tc))
            return;
        _rebuildPending.Add(tc);
    }

    /// <summary>Snapshot + dispatch any pending seam rebuilds up to the in-flight cap. Runs on the
    /// main thread so the data reads are safe; the worker only touches the snapshotted refs.</summary>
    private void DispatchRebuilds()
    {
        if (_rebuildPending.Count == 0 || _rebuildsInFlight.Count >= MaxRebuildInFlight)
            return;

        _tempRebuildScan.Clear();
        foreach (TerrainChunkCoord tc in _rebuildPending)
            _tempRebuildScan.Add(tc);

        for (int i = 0; i < _tempRebuildScan.Count && _rebuildsInFlight.Count < MaxRebuildInFlight; i++)
        {
            TerrainChunkCoord tc = _tempRebuildScan[i];
            if (_rebuildsInFlight.ContainsKey(tc))
            {
                _rebuildPending.Remove(tc);
                continue;
            }
            if (!_loadedChunks.TryGetValue(tc, out ChunkObject obj) || obj == null)
            {
                _rebuildPending.Remove(tc);
                continue;
            }
            _rebuildPending.Remove(tc);
            _rebuildsInFlight.TryAdd(tc, 0);

            ChunkRebuildJob job = SnapshotRebuildJob(tc, obj.MeshRebuildStamp);
            if (!ThreadPool.QueueUserWorkItem(BackgroundChunkRebuild, job))
            {
                // Thread pool refused — fall back to the synchronous path rather than losing the seam.
                _rebuildsInFlight.TryRemove(tc, out _);
                FullRebuildChunk(tc);
                continue;
            }
        }
    }

    /// <summary>Captures everything a worker needs to re-emit one chunk off-thread: raw tile data
    /// refs (shallow read-only contract), the real border corners, and the ChunkObject's
    /// <see cref="ChunkObject.MeshRebuildStamp"/> at snapshot time so stale results are rejected.</summary>
    private ChunkRebuildJob SnapshotRebuildJob(TerrainChunkCoord tc, int stamp)
    {
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        int cs = TerrainChunkCoord.ChunkSize;
        int tiles = cs * cs;
        var job = new ChunkRebuildJob { Coord = tc, Stamp = stamp };

        var snap = new ChunkData[tiles];
        for (int lz = 0; lz < cs; lz++)
        {
            for (int lx = 0; lx < cs; lx++)
            {
                var tileCoord = new ChunkCoord(cminX + lx, cminZ + lz);
                if (_loadedData.TryGetValue(tileCoord, out ChunkData cd))
                    snap[lz * cs + lx] = cd;
            }
        }
        job.Tiles = snap;
        job.Seed = Seed;
        job.Refine = RefineThreshold;
        job.Border = BuildBorderCorners(tc);
        return job;
    }

    /// <summary>ThreadPool worker: re-emits every tile of the chunk + merges them against the border
    /// corners, exactly the heavy half of the old synchronous FullRebuildChunk. The main thread
    /// drains the result under the shared budget (see <see cref="DrainRebuildResults"/>).
    /// Instance method (not static) because it enqueues into this streamer's
    /// <see cref="_readyRebuilds"/>; the job payload itself is plain snapshot data.</summary>
    private void BackgroundChunkRebuild(object state)
    {
        var job = (ChunkRebuildJob)state;
        try
        {
            var tiles = new ChunkMeshData[job.Tiles.Length];
            for (int i = 0; i < job.Tiles.Length; i++)
            {
                ChunkData cd = job.Tiles[i];
                if (cd != null)
                    tiles[i] = ChunkMeshGenerator.BuildMeshData(cd, TerrainNoiseGenerator.DefaultLayers, job.Refine);
            }
            MergedChunkMeshData merged = ChunkMeshGenerator.BuildMergedMeshData(tiles, job.Border, job.Seed);
            _readyRebuilds.Enqueue(new ChunkRebuildResult(job.Coord, job.Stamp, merged));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[WorldStreamer] Seam rebuild for " + job.Coord + " failed — surface keeps its last state.\n" + ex);
            _rebuildsInFlight.TryRemove(job.Coord, out _);
        }
    }

    /// <summary>Applies finished seam rebuilds under the shared stream budget. A result is dropped if
    /// its chunk was unloaded/demoted while generating or if a newer apply (MeshRebuildStamp) already
    /// superseded it, so a stale upload can never revert fresher edits.</summary>
    private void DrainRebuildResults()
    {
        int applied = 0;
        float drainStart = Time.realtimeSinceStartup;
        while (applied < MaxRebuildFinalizePerPoll && !_streamCapped && _readyRebuilds.TryDequeue(out ChunkRebuildResult res))
        {
            _rebuildsInFlight.TryRemove(res.Coord, out _);
            if (!_loadedChunks.TryGetValue(res.Coord, out ChunkObject obj) || obj == null)
                continue;
            if (obj.MeshRebuildStamp != res.Stamp)
                continue;
            try
            {
                obj.ApplyMerged(res.Merged, GroundMaterial, buildCollider: obj.HasCollider);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                continue;
            }
            applied++;
            SpendStreamBudget((Time.realtimeSinceStartup - drainStart) * 1000f);
            drainStart = Time.realtimeSinceStartup;
        }
    }

    /// <summary>Working state handed to a seam-rebuild worker. All member refs are snapshot data:
    /// raw ChunkData refs (read-only contract) + an immutable border-corners dictionary. Safe to
    /// pass across threads because neither side mutates it during flight.</summary>
    private sealed class ChunkRebuildJob
    {
        public TerrainChunkCoord Coord;
        public int Stamp;
        public ChunkData[] Tiles;
        public long Seed;
        public float Refine;
        public Dictionary<long, float> Border;
    }

    /// <summary>Finished rebuild payload: the merged mesh data plus the snapshot stamp that gates
    /// whether the result is still current when it is applied on the main thread.</summary>
    private readonly struct ChunkRebuildResult
    {
        public readonly TerrainChunkCoord Coord;
        public readonly int Stamp;
        public readonly MergedChunkMeshData Merged;

        public ChunkRebuildResult(TerrainChunkCoord coord, int stamp, MergedChunkMeshData merged)
        {
            Coord = coord;
            Stamp = stamp;
            Merged = merged;
        }
    }

    /// <summary>
    /// Unloads out-of-range chunks and populates the pending chunk queue
    /// for background generation.
    ///
    /// Loading and unloading use different ranges (hysteresis): chunks are generated out to
    /// <c>radius</c> but STAY loaded until they pass <c>radius + KeepMargin</c>. Without the
    /// margin, a chunk at the fill boundary that completes just as the focus moves on gets
    /// unloaded the very next tick — its freshly spawned trees/rocks would disappear a frame
    /// or two after appearing.
    ///
    /// Since 1ef the <paramref name="radius"/> passed here is the NEAR real-chunk ring
    /// (<see cref="NearRingRadius"/>), NOT the render radius — the open ground beyond it is the
    /// far shell's job (<see cref="FarShellTick"/>), so the real chunk stream is only ever a
    /// 361-chunk world at the default near ring 9 (vs 18,961 at the 67 render radius). Unloaded ring
    /// edges hand straight to already-existing far cells in the same poll (FarShellTick's
    /// active-shadow sync).
    ///
    /// 1gc dormant keep-ring: a chunk passing <c>keep</c> is no longer DESTROYED the tick it leaves
    /// the loaded ring. It is demoted to a dormant (hidden-but-retained) state out to ring
    /// <c>keep + DormantRingDepth</c> — removed from <c>_loadedChunks</c> so the coarse far cell
    /// covers it exactly as the destroyed chunk was, but its tile data / pooled mesh / GameObject /
    /// LOD children stay alive. Re-crossing the boundary wakes the SAME object in place (no
    /// background regeneration, no mesh upload), killing the destroy+regenerate churn the player
    /// saw at the close-range edge. Only a dormant chunk passing the dormant band is finally
    /// unloaded, via the same capped sweep as before. Pass order: wake -> demote -> deep-unload.
    /// </summary>
    public void StreamAround(TerrainChunkCoord centre, int radius)
    {
        // Hysteresis: keep already-generated chunks loaded beyond the load radius so props
        // (trees/rocks) never pop in and despawn immediately at the streaming edge.
        int keep = radius + 1;
        // 1gc: beyond the keep ring chunks sleep as dormant (hidden, retained) out to the dormant
        // band; only past that band are they truly unloaded.
        int deep = keep + Mathf.Max(1, DormantRingDepth);

        // (1gc) Wake pass: dormant chunks that slid back inside the hysteresis ring are re-shown in
        // place — the same GameObject, same pooled mesh, same tile data. Runs FIRST so the demote /
        // deep-unload scans below already see the woken chunk as loaded again.
        // (1gd) The wake/demote/unload scans now reuse pooled per-poll lists instead of allocating
        // fresh ones every live poll.
        _tempWake.Clear();
        if (_dormantChunks.Count > 0)
        {
            foreach (TerrainChunkCoord tc in _dormantChunks.Keys)
            {
                int dx = Mathf.Abs(tc.X - centre.X);
                int dz = Mathf.Abs(tc.Z - centre.Z);
                if (dx <= keep && dz <= keep)
                    _tempWake.Add(tc);
            }
            for (int i = 0; i < _tempWake.Count; i++)
                WakeChunk(_tempWake[i]);
        }

        // (1gc) Demote + deep-unload: chunks that fell outside the hysteresis ring sleep as dormant;
        // dormant chunks past the dormant band are truly unloaded. Demote is a cheap visual hide +
        // dict move (no 900-tile teardown), so the whole trailing column can demote in one poll —
        // the far cell under it activates in the same poll, so the view is unchanged. The destroy
        // work stays capped (1es) so a deep crossing never bursts a frame.
        _tempDemote.Clear();
        foreach (TerrainChunkCoord tc in _loadedChunks.Keys)
        {
            int dx = Mathf.Abs(tc.X - centre.X);
            int dz = Mathf.Abs(tc.Z - centre.Z);
            if (dx > keep || dz > keep)
                _tempDemote.Add(tc);
        }
        for (int i = 0; i < _tempDemote.Count; i++)
            DemoteChunk(_tempDemote[i]);

        _tempUnload.Clear();
        if (_dormantChunks.Count > 0)
        {
            foreach (TerrainChunkCoord tc in _dormantChunks.Keys)
            {
                int dx = Mathf.Abs(tc.X - centre.X);
                int dz = Mathf.Abs(tc.Z - centre.Z);
                if (dx > deep || dz > deep)
                    _tempUnload.Add(tc);
            }
        }
        int unloaded = 0;
        if (_tempUnload.Count > 0)
        {
            // 1es + 1gd: the deep-unload sweep drains a far column at a CAPPED rate AND a hard
            // wall-clock slice. Every UnloadChunk walks 900 tiles through
            // _dirtyTiles/_loadedObjects/_loadedData (3 dictionary removals per tile) plus the
            // Release mesh/LOD/prop teardown — a burst exactly when the player leaves the dormant
            // band. Capping spreads the drain over polls and _chunkUnloadBacklog keeps the idle
            // gate alive until the last dormant chunk is gone (Unity defers the actual
            // GameObjects' Destroy anyway, so nothing disappears late). The 1gd slice interrupter
            // additionally stops mid-sweep on a slow machine: even ONE UnloadChunk can run past
            // 1 ms, and six of them would stack into a 6 ms+ hit on the frame that leaves the band.
            // Wall-clock-checking each iteration bounds the sweep's effect on the poll that owns it.
            float sweepStart = Time.realtimeSinceStartup;
            for (int i = 0; i < _tempUnload.Count && unloaded < MaxChunkUnloadsPerPoll; i++)
            {
                UnloadChunk(_tempUnload[i]);
                unloaded++;
                if ((Time.realtimeSinceStartup - sweepStart) * 1000f >= DeepUnloadSliceMs)
                    break;
            }
        }
        _chunkUnloadBacklog = unloaded < _tempUnload.Count;

        // Remove pending chunks that fell outside the (extended) radius
        for (int i = _chunkDispatchOrder.Count - 1; i >= 0; i--)
        {
            TerrainChunkCoord tc = _chunkDispatchOrder[i];
            int dx = Mathf.Abs(tc.X - centre.X);
            int dz = Mathf.Abs(tc.Z - centre.Z);
            if (dx > keep || dz > keep)
            {
                _pendingChunks.Remove(tc);
                _chunkDispatchOrder.RemoveAt(i);
            }
        }

        // Populate pending queue: walk chunk rings closest-first
        for (int r = 0; r <= radius; r++)
        {
            int startX = centre.X - r, endX = centre.X + r;
            int startZ = centre.Z - r, endZ = centre.Z + r;

            for (int x = startX; x <= endX; x++)
                EnqueueChunkIfNeeded(new TerrainChunkCoord(x, endZ));
            for (int x = startX; x <= endX; x++)
                EnqueueChunkIfNeeded(new TerrainChunkCoord(x, startZ));
            for (int z = startZ + 1; z < endZ; z++)
                EnqueueChunkIfNeeded(new TerrainChunkCoord(startX, z));
            for (int z = startZ + 1; z < endZ; z++)
                EnqueueChunkIfNeeded(new TerrainChunkCoord(endX, z));
        }
    }

    /// <summary>
    /// Hide a loaded chunk in place and move it to the dormant set (1gc). Its 900-tile bookkeeping,
    /// pooled mesh, LOD children and VoxelStore are retained — only its visuals + collider turn off.
    /// Leaving <c>_loadedChunks</c> makes every "does the real chunk cover this?" check treat it as
    /// absent, so FarShellTick's active-shadow sync activates the coarse cell under it in the SAME
    /// poll — the identical view the old destroy-and-cover showed, minus the teardown.
    /// </summary>
    private void DemoteChunk(TerrainChunkCoord tc)
    {
        ChunkObject obj;
        if (!_loadedChunks.TryGetValue(tc, out obj))
            return;
        obj.ReleaseProps();            // hide trees/rocks (RNG/cursor kept, 1du)
        obj.SetVisualActive(false);    // hide root merged mesh + LOD children
        obj.SetColliderActive(false);  // dormant chunks never carry physics
        obj.Dormant = true;            // ChunkLodManager skips dormant entries (LOD sweep gap guard)
        _loadedChunks.Remove(tc);
        _dormantChunks.Add(tc, obj);
    }

    /// <summary>
    /// Re-show a dormant chunk in place (1gc): move it back into the loaded ring and turn its visuals
    /// on. The collider and props return through their own ring passes (ReconcileColliders /
    /// SyncPropRing) later in the same poll, and the LOD manager re-registers via NewWorldSystems'
    /// LoadedChunks delta-diff (RegisterChunk starts BandIndex=-1, so the correct band is applied on
    /// its next sweep). No dispatch, no background build, no mesh upload — the chunk was never gone.
    /// </summary>
    private void WakeChunk(TerrainChunkCoord tc)
    {
        ChunkObject obj;
        if (!_dormantChunks.TryGetValue(tc, out obj) || obj == null)
        {
            _dormantChunks.Remove(tc);
            return;
        }
        _dormantChunks.Remove(tc);
        obj.SetVisualActive(true);     // band-0 look; LOD re-applies on re-registration
        obj.Dormant = false;
        _loadedChunks[tc] = obj;
        NoteChunkSetChanged();
    }

    private void EnqueueChunkIfNeeded(TerrainChunkCoord tc)
    {
        if (_loadedChunks.ContainsKey(tc))
            return;
        // 1gc: a dormant chunk inside the walk radius is woken instead of re-dispatched — never
        // background-regenerate an object we kept alive (belt-and-braces; the ring walk is <= radius
        // while dormant chunks live beyond keep, but the guard keeps it sound regardless).
        if (_dormantChunks.ContainsKey(tc))
        {
            WakeChunk(tc);
            return;
        }
        if (_chunksInFlight.ContainsKey(tc))
            return;
        if (_pendingChunks.Contains(tc))
            return;
        _pendingChunks.Add(tc);
        _chunkDispatchOrder.Add(tc);
    }

    // --- Background dispatch ---

    /// <summary>
    /// Dispatch pending chunks to the ThreadPool. Sorts by distance each tick
    /// so closest chunks load first. Since 1ea the sort is allocation-free (a cached
    /// comparer reads the current focus via a field) and the whole pass early-outs when
    /// there is nothing queued, so an idle/fully-streamed world costs zero per poll.
    /// </summary>
    private void DispatchPending()
    {
        if (_chunkDispatchOrder.Count == 0)
            return;

        _dispatchFocus = _focus != null
            ? TerrainChunkCoord.FromWorld(_focus.position)
            : default;
        _chunkDispatchOrder.Sort(_dispatchSort);

        for (int i = 0; i < _chunkDispatchOrder.Count && _chunksInFlight.Count < MaxInFlight; i++)
        {
            TerrainChunkCoord tc = _chunkDispatchOrder[i];
            if (_chunksInFlight.ContainsKey(tc))
                continue;
            if (_loadedChunks.ContainsKey(tc))
                continue; // already materialized — never re-generate (1em)

            _chunksInFlight.TryAdd(tc, 0);
            long seed = Seed;
            // Capture the mesh mode here (main thread) so the worker builds a consistent chunk.
            bool voxel = VoxelTerrainEnabled;
            ThreadPool.QueueUserWorkItem(_ => BackgroundGenerateChunk(tc, seed, voxel));
        }

        // Drop already-loaded chunks from the dispatch list. 1em: the old guard required BOTH
        // pending AND loaded, but FinalizeChunks removes the pending mark at finalize, so a
        // finalized/loaded chunk could never match — it stayed in the list and was re-dispatched
        // (regenerated) every poll. Since dispatch is nearest-first and MaxInFlight saturates with
        // the nearest chunks before the scan reaches the outer rings, the freed slots were always
        // refilled by those SAME nearest chunks and the rest of the near ring starved forever
        // (the ~24-chunk bubble; the far shell was unaffected because it skips completed cells).
        for (int i = _chunkDispatchOrder.Count - 1; i >= 0; i--)
        {
            TerrainChunkCoord c = _chunkDispatchOrder[i];
            if (_loadedChunks.ContainsKey(c))
            {
                _pendingChunks.Remove(c);
                _chunkDispatchOrder.RemoveAt(i);
            }
        }
    }

    // --- Synchronous generation (for startup) ---

    /// <summary>
    /// Generate an entire terrain chunk synchronously on the main thread.
    /// Used at startup to ensure the spawn tile has terrain + colliders before
    /// the player is placed. Uses the shared noise-or-disk builder, then creates a
    /// single merged GameObject per chunk. The boot chunk keeps its collider (1dq) so the
    /// player can land before the first ReconcileColliders poll. Props begin queuing
    /// immediately and stream in over the next ticks so boot stays light.
    /// </summary>
    public void GenerateChunkSync(TerrainChunkCoord tc)
    {
        if (_loadedChunks.ContainsKey(tc))
            return;

        // 1fx diagnostic guard: the boot chunk builds on the MAIN thread with no worker catch, so a
        // throw here aborts the caller's Start sequence mid-way and the spawn chunk silently never
        // appears (while the far shell keeps the rest of the view). Log the full exception instead of
        // dying; StreamAround re-enqueues the chunk on the next poll so it still arrives via the
        // background path.
        try
        {
            TerrainChunkMeshData chunk = VoxelTerrainEnabled
                ? BuildVoxelChunk(tc, Seed)
                : BuildOrLoadChunk(tc, Seed);
            CreateChunkGameObject(chunk, buildCollider: true);
            ReconcileNewlyLoadedChunk(tc, chunk.HadLoadedMods);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// Ensure the terrain chunk containing the given tile is loaded. Synchronous
    /// fallback for non-streaming callers (e.g. validation). Generates the whole
    /// chunk, since tiles are no longer individual GameObjects.
    /// </summary>
    public void EnsureChunk(ChunkCoord coord)
    {
        GenerateChunkSync(TerrainChunkCoord.FromTile(coord));
    }

    public void UnloadChunk(ChunkCoord coord)
    {
        UnloadChunk(TerrainChunkCoord.FromTile(coord));
    }

    public void UnloadChunk(TerrainChunkCoord tc)
    {
        ChunkObject obj;
        if (!_loadedChunks.TryGetValue(tc, out obj))
        {
            // 1gc: a dormant chunk deep-unloads without ever re-entering the loaded ring — read it
            // from the dormant set instead. Its 900-tile bookkeeping is shared with the loaded ring,
            // so the flush + tile-drop + Release below are identical.
            if (!_dormantChunks.TryGetValue(tc, out obj) || obj == null)
                return;
            _dormantChunks.Remove(tc);
        }

        // Persist any modified tiles in the chunk as one file, then drop tile-level bookkeeping.
        FlushDirtyChunk(tc);
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                ChunkCoord tile = new ChunkCoord(x, z);
                _dirtyTiles.Remove(tile);
                _loadedObjects.Remove(tile);
                _loadedData.Remove(tile);
            }
        }

        obj.Release();
        if (obj != null)
            Destroy(obj.gameObject);
        _loadedChunks.Remove(tc);
        _modifiedChunks.Remove(tc);
        NoteChunkSetChanged();
    }

    /// <summary>
    /// Force an in-memory rebuild of the historical farming-lane chunks (tc -1,0 / -1,1 / -1,2)
    /// so any pre-NaN-fix flat mesh from an earlier session is dropped and regenerated from the
    /// corner grid (saved sparse lane heights + rolling noise for every unstamped corner). This
    /// is the runtime-side of the lane-flatten purge: data files are already clean, so a plain
    /// unload + re-dispatch makes the patch re-roll without touching saved files.
    /// </summary>
    public void ForceRebuildArenaLane()
    {
        ForceRebuildChunk(new TerrainChunkCoord(-1, 0));
        ForceRebuildChunk(new TerrainChunkCoord(-1, 1));
        ForceRebuildChunk(new TerrainChunkCoord(-1, 2));
    }

    /// <summary>
    /// Unload one terrain chunk (persisting its dirty tiles atomically, dropping the in-memory
    /// object + tile bookkeeping) then re-queue it for background generation via the SAME
    /// BuildOrLoadChunk path as normal streaming.
    /// </summary>
    private void ForceRebuildChunk(TerrainChunkCoord tc)
    {
        UnloadChunk(tc);
        EnqueueChunkIfNeeded(tc); // background regen: saved heights + NaN-missing corners
    }

    /// <summary>
    /// QA helper: deliberately start a clean map for this seed. Wipes every terrain-chunk save
    /// file, clears in-memory dirty marks, then unloads + requeues every loaded chunk so the
    /// ground regenerates pristine from noise (no leftover slabs, closed mesh, no holes). The
    /// files are gone afterwards — this is a permanent discard of all terrain edits for the seed.
    /// The far shell (1ef) is wiped first: its cells were sampled from the old saves, so it must
    /// regenerate from the pristine noise to stay in sync with the real chunks.
    /// </summary>
    public void ResetTerrainSaves()
    {
        ClearFarShell();
        // 1es: drain queued saves BEFORE the wipe — the background writer could otherwise write a
        // chunk's file back AFTER ResetWorldSaves deletes it, resurrecting the very edits the reset
        // is meant to discard.
        ChunkSaveManager.FlushPendingSaves();
        ChunkSaveManager.ResetWorldSaves(Seed);
        _dirtyTiles.Clear();
        _modifiedChunks.Clear();

        var loaded = new List<TerrainChunkCoord>(_loadedChunks.Keys);
        loaded.AddRange(_dormantChunks.Keys);
        foreach (TerrainChunkCoord tc in loaded)
        {
            UnloadChunk(tc);
            EnqueueChunkIfNeeded(tc);
        }
        Debug.Log($"[WorldStreamer] Reset terrain saves for seed {Seed} — {loaded.Count} loaded/dormant chunk(s) requeued to regenerate from noise.");
    }

    public void MarkDirty(ChunkCoord coord)
    {
        // Record only; the actual write is batched per terrain chunk (FlushDirtyChunk),
        // so a single Earth cast no longer triggers one tiny file write per tile.
        _dirtyTiles.Add(coord);
    }

    /// <summary>
    /// Persist every modified tile of a terrain chunk as ONE chunk file, atomically.
    /// Snapshots the heights so the write is safe even if more deformation happens later.
    /// Gated by ChunkSaveManager.SynchronousWrites (default true): when false, the snapshot is
    /// skipped entirely and the dirty marks are simply dropped (the deferred-save path is currently
    /// unused). Since 1es the actual file write ALWAYS runs on a background worker — SaveChunk only
    /// pushes the snapshot into the writer queue — so persisting a chunk costs the main thread a few
    /// array clones and a ConcurrentQueue push, never a disk write. The write cannot be lost
    /// silently on quit: WorldStreamer.OnDestroy drains the queue via
    /// ChunkSaveManager.FlushPendingSaves.
    /// </summary>
    private void FlushDirtyChunk(TerrainChunkCoord tc)
    {
        if (VoxelTerrainEnabled)
        {
            FlushVoxelChunk(tc);
            return;
        }
        if (!ChunkSaveManager.SynchronousWrites)
            return;

        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        ChunkSaveData save = null;
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                var tile = new ChunkCoord(x, z);
                if (!_dirtyTiles.Contains(tile))
                    continue;
                if (!_loadedData.TryGetValue(tile, out ChunkData data))
                    continue;
                if (save == null)
                    save = new ChunkSaveData();
                save.Mods.Add(new ChunkTileMod
                {
                    LocalX = x - minX,
                    LocalZ = z - minZ,
                    Heights = (float[])data.Heights.Clone(),
                    Version = data.Version,
                });
                _dirtyTiles.Remove(tile);
            }
        }
        if (save != null && save.Mods.Count > 0)
            ChunkSaveManager.SaveChunk(Seed, tc, save);
    }
}