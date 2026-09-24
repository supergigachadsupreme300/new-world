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

    /// <summary>Max ChunkObjects released by the StreamAround trailing-edge sweep per poll (1es).
    /// The uncapped sweep released the whole out-of-ring column (~21 chunks at keep 10) in a single
    /// poll — 900 tiles x 3 dictionary removals per chunk plus the mesh/LOD teardown — the biggest
    /// frame spike when travelling. A cap of 6 spreads a crossing's drain over ~4 polls (~0.2 s);
    /// nothing needs the trailing chunks gone instantly, and <see cref="_chunkUnloadBacklog"/> keeps
    /// the poll alive until every last one is gone.</summary>
    private const int MaxChunkUnloadsPerPoll = 6;

    /// <summary>True while out-of-ring chunks still await the capped unload sweep (1es). Works like
    /// <see cref="_farUnloadBacklog"/>: participates in the Update idle gate so a partially-drained
    /// crossing keeps polling — <see cref="StreamAround"/> re-collects the remainder each poll and
    /// clears the flag once the sweep fully catches up.</summary>
    private bool _chunkUnloadBacklog;

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
    /// </summary>
    public void StreamAround(TerrainChunkCoord centre, int radius)
    {
        // Hysteresis: keep already-generated chunks loaded beyond the load radius so props
        // (trees/rocks) never pop in and despawn immediately at the streaming edge.
        int keep = radius + 1;

        // Unload chunks that fell outside the (extended) radius.
        List<TerrainChunkCoord> toUnload = null;
        foreach (TerrainChunkCoord tc in _loadedChunks.Keys)
        {
            int dx = Mathf.Abs(tc.X - centre.X);
            int dz = Mathf.Abs(tc.Z - centre.Z);
            if (dx > keep || dz > keep)
            {
                if (toUnload == null) toUnload = new List<TerrainChunkCoord>();
                toUnload.Add(tc);
            }
        }
        if (toUnload != null)
        {
            // 1es: the trailing column (~21 chunks at keep 10) is released at a CAPPED rate. An
            // unlimited sweep destroyed the whole column in one poll — every UnloadChunk walks 900
            // tiles through _dirtyTiles/_loadedObjects/_loadedData (3 dictionary removals per tile)
            // plus the Release mesh/LOD teardown, which burst exactly when the player crossed a chunk
            // boundary. Capping spreads the drain over ~4 polls; _chunkUnloadBacklog keeps the idle
            // gate alive until the last trailing chunk is gone (Unity defers the actual GameObjects'
            // Destroy anyway, so nothing disappears late).
            int unloaded = 0;
            for (int i = 0; i < toUnload.Count && unloaded < MaxChunkUnloadsPerPoll; i++)
            {
                UnloadChunk(toUnload[i]);
                unloaded++;
            }
            _chunkUnloadBacklog = unloaded < toUnload.Count;
        }
        else
        {
            _chunkUnloadBacklog = false;
        }

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

    private void EnqueueChunkIfNeeded(TerrainChunkCoord tc)
    {
        if (_loadedChunks.ContainsKey(tc))
            return;
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
            return;

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
        foreach (TerrainChunkCoord tc in loaded)
        {
            UnloadChunk(tc);
            EnqueueChunkIfNeeded(tc);
        }
        Debug.Log($"[WorldStreamer] Reset terrain saves for seed {Seed} — {loaded.Count} loaded chunk(s) requeued to regenerate from noise.");
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