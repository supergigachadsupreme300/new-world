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

    /// <summary>
    /// Dequeue completed terrain chunks and create ONE GameObject (merged mesh +
    /// single collider) per chunk on the main thread. A capped budget
    /// (ChunksPerFrame, max 16/tick) PLUS a wall-clock time budget (~12ms) spreads the
    /// work so the render-radius fill completes in ~10-15s (1dg) without a burst hitches
    /// a single frame. Props are NOT spawned here — they stream in over the next ticks
    /// (StepChunkProps).
    /// </summary>
    private void FinalizeChunks()
    {
        int finalized = 0;
        int budget = Mathf.Max(1, Mathf.Min(ChunksPerFrame, 16));
        float start = Time.realtimeSinceStartup;
        while (finalized < budget && _readyChunks.TryDequeue(out TerrainChunkMeshData chunk))
        {
            byte _;
            _chunksInFlight.TryRemove(chunk.Coord, out _);
            _pendingChunks.Remove(chunk.Coord);

            if (_loadedChunks.ContainsKey(chunk.Coord))
                continue;

            CreateChunkGameObject(chunk);
            // Slab seam walls need BOTH sides of a chunk boundary in memory to render with real
            // neighbour heights — reconcile now that this chunk's tiles exist.
            ReconcileNewlyLoadedChunk(chunk.Coord);
            finalized++;
            if ((Time.realtimeSinceStartup - start) * 1000f >= 12f)
                break;
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
    /// Creates the chunk GameObject (mesh + collider) and registers its 900 tiles in the
    /// tile-level lookup dictionaries for persistence/validation. Props are queued via
    /// BeginProps and spawned incrementally by StepChunkProps on later ticks.
    /// </summary>
    private void CreateChunkGameObject(TerrainChunkMeshData chunk)
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
        obj.ApplyMerged(chunk.Merged, GroundMaterial, buildCollider: true);
        obj.BeginProps(Seed);
        _loadedChunks[tc] = obj;

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
    /// </summary>
    private void ReconcileNewlyLoadedChunk(TerrainChunkCoord tc)
    {
        if (ChunkHasModifiedTiles(tc))
            FullRebuildChunk(tc);
        ReconcileModifiedBorders(tc);
    }
}