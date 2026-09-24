using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Main-thread chunk finalization / chunk GameObject creation portion of the WorldStreamer partial class.
/// </summary>

public partial class WorldStreamer
{

    // --- Main thread: finalize chunk data + create the chunk GameObject ---

    /// <summary>Target frame length (60 fps) used to self-tune the per-tick main-thread budgets.</summary>
    private const float TargetFrameMs = 16.7f;

    /// <summary>
    /// Scales a per-tick main-thread budget to the previous frame's length so the streaming spread
    /// self-tunes (1di): a slow frame (dropped below ~60fps) shrinks this tick's work and a fast one
    /// spends the full budget, so chunk finalization + collider cooking + prop spawning ride under
    /// the frame budget instead of bursting past it. Never zero — the stream is still guaranteed to
    /// make progress every tick. Since 1es this also sizes the shared StreamBudgetMs pool that every
    /// load-bearing stage draws from.
    /// </summary>
    private float AdaptiveBudgetMs(float baseMs)
    {
        float scale = Mathf.Clamp(TargetFrameMs / Mathf.Max(1f, Time.deltaTime * 1000f), 0.35f, 1.2f);
        return baseMs * scale;
    }

    /// <summary>
    /// Dequeue completed terrain chunks and create ONE GameObject (merged mesh +
    /// single collider) per chunk on the main thread. A count cap (ChunksPerFrame, max 12/tick) PLUS
    /// the shared streaming budget (~4 ms base, shrinks while frames hitch — 1di was a per-stage cap,
    /// 1es replaced it with the poll-wide pool <see cref="SpendStreamBudget"/>) spreads the burst so
    /// the render-radius fill completes without dropping a steady 60 fps. Props are NOT
    /// spawned here — they stream in over the next ticks (SyncPropRing + StepChunkProps). Deferrable:
    /// when the shared budget is already dry this poll the ready queue simply waits a poll.
    /// </summary>
    private void FinalizeChunks()
    {
        if (_streamCapped)
            return;
        int finalized = 0;
        int budget = Mathf.Max(1, Mathf.Min(ChunksPerFrame, 12));
        float chunkStart = Time.realtimeSinceStartup;
        while (finalized < budget && !_streamCapped && _readyChunks.TryDequeue(out TerrainChunkMeshData chunk))
        {
            byte _;
            _chunksInFlight.TryRemove(chunk.Coord, out _);
            _pendingChunks.Remove(chunk.Coord);

            if (_loadedChunks.ContainsKey(chunk.Coord))
                continue;

            CreateChunkGameObject(chunk);
            // Slab seam walls need BOTH sides of a chunk boundary in memory to render with real
            // neighbour heights — reconcile now that this chunk's tiles exist.
            ReconcileNewlyLoadedChunk(chunk.Coord, chunk.HadLoadedMods);
            finalized++;
            SpendStreamBudget((Time.realtimeSinceStartup - chunkStart) * 1000f);
            chunkStart = Time.realtimeSinceStartup;
        }
    }

    private Transform EnsureChunksRoot()
    {
        if (_terrainRoot == null)
            _terrainRoot = new GameObject("Terrain").transform;
        if (_chunksRoot == null)
        {
            _chunksRoot = new GameObject("Chunks").transform;
            _chunksRoot.SetParent(_terrainRoot, false);
        }
        return _chunksRoot;
    }

    /// <summary>
    /// Creates the chunk GameObject (mesh + optional collider) and registers its 900 tiles in the
    /// tile-level lookup dictionaries for persistence/validation. Props are NOT queued here —
    /// <see cref="SyncPropRing"/> owns the prop stream and starts it only for chunks inside the
    /// prop ring, so most of the distant radius-N ring never spawns trees/rocks (1di).
    /// Streaming chunks build COLLIDER-LESS (1dq) — the per-poll ReconcileColliders assigns the
    /// MeshCollider once a chunk enters the player or magic ring. The synchronous boot chunk keeps
    /// its collider so the player can land on it before the first poll.
    /// </summary>
    private void CreateChunkGameObject(TerrainChunkMeshData chunk, bool buildCollider = false)
    {
        TerrainChunkCoord tc = chunk.Coord;
        Vector3 origin = new Vector3(
            tc.X * TerrainChunkCoord.ChunkSize * ChunkData.Size,
            0f,
            tc.Z * TerrainChunkCoord.ChunkSize * ChunkData.Size);

        var go = new GameObject($"TerrainChunk_{tc.X}_{tc.Z}");
        go.isStatic = false;
        go.transform.SetParent(EnsureChunksRoot(), false);
        go.transform.position = origin;

        var obj = go.AddComponent<ChunkObject>();
        obj.Init(tc);
        obj.ApplyMerged(chunk.Merged, GroundMaterial, buildCollider);
        // Voxel mode (1et): flag the chunk so its LOD decimation (which indexes the smooth
        // TOPS-FIRST layout) never samples the stepped mesh — the full mesh stays visible.
        obj.VoxelMesh = VoxelTerrainEnabled;
        // Voxel mode (1eu): attach the column-store so rebuilds/flushes reuse the loaded runs
        // (caves/overhangs survive) instead of regenerating from the flat tile grid.
        obj.VoxelStore = chunk.Voxel;
        _loadedChunks[tc] = obj;
        NoteChunkSetChanged();

        for (int i = 0; i < chunk.Tiles.Length; i++)
        {
            ChunkCoord tile = chunk.Tiles[i].Coord;
            _loadedData[tile] = chunk.Tiles[i].Data;
            _loadedObjects[tile] = obj;
        }
    }

    /// <summary>
    /// Full-rebuilds a just-loaded chunk (and any loaded modified neighbour) with real border
    /// heights so seam slab walls are correct once both sides of a seam are in memory.
    /// Since 1ea the modified-chunk test is an O(1) set lookup (fed by live edits and by the
    /// <paramref name="hadLoadedMods"/> flag carried out of the background loader) — the old
    /// per-chunk 900-tile scan is gone.
    /// </summary>
    private void ReconcileNewlyLoadedChunk(TerrainChunkCoord tc, bool hadLoadedMods)
    {
        if (hadLoadedMods)
            _modifiedChunks.Add(tc);
        if (_modifiedChunks.Contains(tc))
            FullRebuildChunk(tc);
        ReconcileModifiedBorders(tc);
    }
}