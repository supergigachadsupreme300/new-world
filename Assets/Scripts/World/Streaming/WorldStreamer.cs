using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// The main orchestrator for the seamless open world.
///
/// The world is divided into TerrainChunks, each covering a 30x30 block of
/// individual tiles. Background threads generate one TerrainChunk at a time,
/// pre-computing the 31x31 corner heights and merging the 900 tile meshes into
/// a single thread-safe chunk mesh. The main thread then creates ONE GameObject
/// per chunk (mesh + collider + props), so a fully streamed radius is ~50
/// objects instead of ~44,100 tiles — physics, draw calls and scene-graph cost
/// drop by roughly three orders of magnitude.
/// </summary>
public class WorldStreamer : MonoBehaviour
{
    [Header("World")]
    [Tooltip("Shared world seed. Same seed + coords => same terrain everywhere.")]
    public long Seed = 1337;

    public bool StreamInUpdate = true;

    [Header("Material")]
    public Material GroundMaterial;

    [Header("Render Distance")]
    public RenderDistanceController RenderDistance;

    [Header("Threading")]
    [Tooltip("Max terrain chunks finalized per poll tick (main-thread work).")]
    public int ChunksPerFrame = 8;

    [Tooltip("Max terrain chunks being generated on background threads simultaneously.")]
    public int MaxInFlight = 8;

    // --- Tile-level state (existing public API; 900 entries per loaded chunk) ---
    private readonly Dictionary<ChunkCoord, ChunkData> _loadedData = new Dictionary<ChunkCoord, ChunkData>();
    private readonly Dictionary<ChunkCoord, ChunkObject> _loadedObjects = new Dictionary<ChunkCoord, ChunkObject>();
    // Modified tiles not yet persisted (flushed per terrain chunk — one file write per cast).
    private readonly HashSet<ChunkCoord> _dirtyTiles = new HashSet<ChunkCoord>();

    // --- Chunk-level state (authoritative object identity; one entry per loaded chunk) ---
    private readonly Dictionary<TerrainChunkCoord, ChunkObject> _loadedChunks = new Dictionary<TerrainChunkCoord, ChunkObject>();

    // --- Chunk-level dispatch ---
    private readonly HashSet<TerrainChunkCoord> _pendingChunks = new HashSet<TerrainChunkCoord>();
    private readonly List<TerrainChunkCoord> _chunkDispatchOrder = new List<TerrainChunkCoord>();
    private readonly ConcurrentDictionary<TerrainChunkCoord, byte> _chunksInFlight = new ConcurrentDictionary<TerrainChunkCoord, byte>();
    private readonly ConcurrentQueue<TerrainChunkMeshData> _readyChunks = new ConcurrentQueue<TerrainChunkMeshData>();

    // --- Hierarchy container (Terrain > Chunks > Chunk_X_Z) ---
    private Transform _terrainRoot;
    private Transform _chunksRoot;

    private Transform _focus;
    private float _timer;
    private const float PollInterval = 0.1f;

    public IReadOnlyDictionary<ChunkCoord, ChunkObject> Loaded => _loadedObjects;

    /// <summary>Loaded terrain chunks keyed by chunk coord (one object per chunk).</summary>
    public IReadOnlyDictionary<TerrainChunkCoord, ChunkObject> LoadedChunks => _loadedChunks;

    // --- Public tile-level API ---

    public bool TryGetData(ChunkCoord coord, out ChunkData data)
    {
        return _loadedData.TryGetValue(coord, out data);
    }

    public bool IsLoaded(ChunkCoord coord) => _loadedObjects.ContainsKey(coord);

    public void SetFocus(Transform focus)
    {
        _focus = focus;
    }

    // --- Main loop ---

    private void Update()
    {
        if (!StreamInUpdate)
            return;

        _timer += Time.deltaTime;
        if (_timer < PollInterval)
            return;
        _timer = 0f;

        if (_focus == null)
            return;

        int radius = RenderDistance != null ? RenderDistance.Radius : 3;
        TerrainChunkCoord centre = TerrainChunkCoord.FromWorld(_focus.position);

        StreamAround(centre, radius);
        DispatchPending();
        FinalizeChunks();
        StepChunkProps();
    }

    // --- Chunk-level streaming ---

    /// <summary>
    /// Unloads out-of-range chunks and populates the pending chunk queue
    /// for background generation.
    ///
    /// Loading and unloading use different ranges (hysteresis): chunks are generated out to
    /// <c>radius</c> but STAY loaded until they pass <c>radius + KeepMargin</c>. Without the
    /// margin, a chunk at the fill boundary that completes just as the focus moves on gets
    /// unloaded the very next tick — its freshly spawned trees/rocks would disappear a frame
    /// or two after appearing.
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
            foreach (TerrainChunkCoord tc in toUnload)
                UnloadChunk(tc);
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
    /// so closest chunks load first.
    /// </summary>
    private void DispatchPending()
    {
        TerrainChunkCoord focus = _focus != null
            ? TerrainChunkCoord.FromWorld(_focus.position)
            : default;

        _chunkDispatchOrder.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.X - focus.X) + Mathf.Abs(a.Z - focus.Z);
            int db = Mathf.Abs(b.X - focus.X) + Mathf.Abs(b.Z - focus.Z);
            return da.CompareTo(db);
        });

        for (int i = 0; i < _chunkDispatchOrder.Count && _chunksInFlight.Count < MaxInFlight; i++)
        {
            TerrainChunkCoord tc = _chunkDispatchOrder[i];
            if (_chunksInFlight.ContainsKey(tc))
                continue;

            _chunksInFlight.TryAdd(tc, 0);
            long seed = Seed;
            ThreadPool.QueueUserWorkItem(_ => BackgroundGenerateChunk(tc, seed));
        }

        // Drop fully-loaded chunks from the dispatch list.
        _chunkDispatchOrder.RemoveAll(c => _pendingChunks.Contains(c) && _loadedChunks.ContainsKey(c));
    }

    // --- Background thread: generate entire chunk ---

    /// <summary>
    /// Runs on a ThreadPool thread. Builds the chunk from disk deformation mods when they
    /// exist, otherwise from noise (a cache-miss), then merges the tile meshes into one
    /// thread-safe chunk mesh.
    /// </summary>
    private void BackgroundGenerateChunk(TerrainChunkCoord tc, long seed)
    {
        try
        {
            _readyChunks.Enqueue(BuildOrLoadChunk(tc, seed));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WorldStreamer] Background chunk generation failed for {tc}: {ex.Message}");
            byte _;
            _chunksInFlight.TryRemove(tc, out _);
        }
    }

    /// <summary>
    /// Builds a terrain chunk's 900 tile meshes plus the merged chunk mesh. Reads the terrain
    /// chunk's save file first: deformed tiles restore their saved heights (so revisiting an
    /// edited area is fast and exact); every other corner regenerates from noise (the cache
    /// miss path). Shared-edge contract is preserved because every tile whose corner a deformed
    /// tile touches is saved/handled together by DeformAt.
    /// </summary>
    private TerrainChunkMeshData BuildOrLoadChunk(TerrainChunkCoord tc, long seed)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        int gridSize = TerrainChunkCoord.CornerGridSize; // 31

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

        // Corner grid: NaN marks a corner that must regenerate from noise. Deformed tiles stamp
        // their saved corner heights first so shared edges within the patch stay gapless.
        float[,] corners = new float[gridSize, gridSize];
        if (mods != null)
        {
            foreach (KeyValuePair<int, ChunkTileMod> kv in mods)
            {
                ChunkTileMod m = kv.Value;
                if (m.Heights == null || m.Heights.Length < ChunkData.VertexCount)
                    continue;
                corners[m.LocalX, m.LocalZ + 1] = m.Heights[0];     // NW
                corners[m.LocalX + 1, m.LocalZ + 1] = m.Heights[1]; // NE
                corners[m.LocalX + 1, m.LocalZ] = m.Heights[2];     // SE
                corners[m.LocalX, m.LocalZ] = m.Heights[3];         // SW
            }
        }
        for (int gz = 0; gz < gridSize; gz++)
        {
            for (int gx = 0; gx < gridSize; gx++)
            {
                bool missing = mods == null || float.IsNaN(corners[gx, gz]);
                if (!missing)
                    continue;
                float worldX = (tc.X * cs + gx) * ChunkData.Size;
                float worldZ = (tc.Z * cs + gz) * ChunkData.Size;
                corners[gx, gz] = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
            }
        }

        // Build tile mesh data (saved heights where a mod exists, corner grid otherwise).
        ChunkMeshData[] tiles = new ChunkMeshData[cs * cs];
        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                ChunkCoord tileCoord = new ChunkCoord(tc.X * cs + tx, tc.Z * cs + tz);
                ChunkData data = new ChunkData(tileCoord.X, tileCoord.Z, seed);
                if (mods != null && mods.TryGetValue(tz * cs + tx, out ChunkTileMod m))
                {
                    if (m.Heights != null && m.Heights.Length >= ChunkData.VertexCount)
                    {
                        for (int k = 0; k < ChunkData.VertexCount; k++)
                            data.Heights[k] = m.Heights[k];
                    }
                    data.Version = m.Version;
                    data.HasModifications = true;
                }
                else
                {
                    data.Heights[0] = corners[tx, tz + 1];     // NW
                    data.Heights[1] = corners[tx + 1, tz + 1]; // NE
                    data.Heights[2] = corners[tx + 1, tz];     // SE
                    data.Heights[3] = corners[tx, tz];          // SW
                    data.Version = 1;
                }
                tiles[tz * cs + tx] = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers);
            }
        }

        return new TerrainChunkMeshData
        {
            Coord = tc,
            Tiles = tiles,
            Merged = ChunkMeshGenerator.BuildMergedMeshData(tiles),
        };
    }

    // --- Main thread: finalize chunk data + create the chunk GameObject ---

    /// <summary>
    /// Dequeue completed terrain chunks and create ONE GameObject (merged mesh +
    /// single collider) per chunk on the main thread. A capped budget
    /// (ChunksPerFrame, max 8/tick) PLUS a wall-clock time budget (~6ms) spreads the
    /// work so the whole render radius fills in about a second without frame hitches.
    /// Props are NOT spawned here — they stream in over the next ticks (StepChunkProps).
    /// </summary>
    private void FinalizeChunks()
    {
        int finalized = 0;
        int budget = Mathf.Max(1, Mathf.Min(ChunksPerFrame, 8));
        float start = Time.realtimeSinceStartup;
        while (finalized < budget && _readyChunks.TryDequeue(out TerrainChunkMeshData chunk))
        {
            byte _;
            _chunksInFlight.TryRemove(chunk.Coord, out _);
            _pendingChunks.Remove(chunk.Coord);

            if (_loadedChunks.ContainsKey(chunk.Coord))
                continue;

            CreateChunkGameObject(chunk);
            finalized++;
            if ((Time.realtimeSinceStartup - start) * 1000f >= 6f)
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
    /// Time-budgeted prop spawning: each tick, a global budget of prop tiles is consumed
    /// across the newest pending chunks. This keeps a full render-radius fill (450 props,
    /// some trees 100+ cubes) from hitching a single frame.
    /// </summary>
    private const int PropTilesPerTick = 40;

    private void StepChunkProps()
    {
        int remaining = PropTilesPerTick;
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            ChunkObject obj = kv.Value;
            if (obj == null || !obj.PropsPending)
                continue;
            remaining -= obj.StepProps(Mathf.Max(1, remaining));
            if (remaining <= 0)
                break;
        }
    }

    // --- Synchronous generation (for startup) ---

    /// <summary>
    /// Generate an entire terrain chunk synchronously on the main thread.
    /// Used at startup to ensure the spawn tile has terrain + colliders before
    /// the player is placed. Uses the shared noise-or-disk builder, then creates a
    /// single merged GameObject per chunk. Props begin queuing immediately and stream
    /// in over the next ticks so boot stays light.
    /// </summary>
    public void GenerateChunkSync(TerrainChunkCoord tc)
    {
        if (_loadedChunks.ContainsKey(tc))
            return;

        CreateChunkGameObject(BuildOrLoadChunk(tc, Seed));
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
    /// Respects ChunkSaveManager.SynchronousWrites: when false, the write defers to
    /// unload/session-end instead of happening immediately.
    /// </summary>
    private void FlushDirtyChunk(TerrainChunkCoord tc)
    {
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

    // --- Runtime terrain deformation (Earth school, §3.8) ---

    /// <summary>
    /// Reshape the loaded heightmap around a world-space center (main thread only).
    /// <para>
    /// Earth spells carry no status effect — instead they deform the ground (Ring: raised
    /// annulus circling the impact; Spikes: scattered rock spikes). Corner heights are edited
    /// in the tile-level data so shared corners always match (gapless mesh), each touched tile
    /// is marked modified/dirty so it persists and syncs, and the whole affected chunk(s) are
    /// rebuilt (merged mesh + collider) in place. Unloaded tiles are ignored — spells only
    /// deform terrain the streamer already has in memory.
    /// </para>
    /// </summary>
    public void DeformAt(Vector3 center, float radius, TerrainShape shape)
    {
        if (shape == TerrainShape.None || radius <= 0f) return;

        float feather = 0.5f;
        float reach = radius + feather;
        int minCX = Mathf.FloorToInt(center.x - reach);
        int maxCX = Mathf.FloorToInt(center.x + reach);
        int minCZ = Mathf.FloorToInt(center.z - reach);
        int maxCZ = Mathf.FloorToInt(center.z + reach);

        // Ring: a raised annulus with its center left level. Spikes: smooth mound + sparse
        // deterministic peaks so the ground reads jagged but never chessboard-y.
        float lift = shape == TerrainShape.Ring ? 0.9f : 0.7f;
        float ringMid = radius * 0.72f;
        float ringHalfWidth = Mathf.Max(0.6f, radius * 0.28f);

        // New height for every world corner (integer x/z) inside the reach.
        var newHeights = new Dictionary<long, float>();

        for (int cz = minCZ; cz <= maxCZ; cz++)
        {
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                float wx = cx + 0.5f;
                float wz = cz + 0.5f;
                float dx = wx - center.x;
                float dz = wz - center.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                float influence;
                if (shape == TerrainShape.Ring)
                {
                    float off = Mathf.Abs(dist - ringMid);
                    influence = off >= ringHalfWidth ? 0f : 1f - off / ringHalfWidth;
                }
                else
                {
                    float fall = 1f - Mathf.Clamp01(dist / reach);
                    influence = fall * fall;
                }

                if (influence <= 0f)
                    continue;

                // Smooth the influence curve (smootherstep) so the deform blends out at the rim.
                float s = influence * influence * (3f - 2f * influence) * lift;

                if (shape == TerrainShape.Spikes)
                {
                    int raw = (cx * 73856093) ^ (cz * 19349663) ^ Seed.GetHashCode();
                    float r = (raw & 0x7fffffff) / (float)0x7fffffff;
                    if (r > 0.78f)
                        s += lift * (0.4f + r * 0.6f) * influence * influence;
                }

                newHeights[EncodeCorner(cx, cz)] = CurrentHeightOf(cx, cz) + s;
            }
        }

        if (newHeights.Count == 0)
            return;

        // Apply edits to every loaded tile touched by the corner set. Corners not in the
        // influence set simply keep their current (unchanged) height, so shared edges with
        // untouched neighbours line up perfectly.
        var rebuiltChunks = new HashSet<TerrainChunkCoord>();
        bool changedAny = false;
        for (int cz = minCZ; cz <= maxCZ; cz++)
        {
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                var tile = new ChunkCoord(cx, cz);
                if (!_loadedData.TryGetValue(tile, out ChunkData data))
                    continue;

                data.Heights[0] = CornerOrBase(cx, cz + 1, newHeights);     // NW
                data.Heights[1] = CornerOrBase(cx + 1, cz + 1, newHeights); // NE
                data.Heights[2] = CornerOrBase(cx + 1, cz, newHeights);     // SE
                data.Heights[3] = CornerOrBase(cx, cz, newHeights);         // SW
                data.HasModifications = true;
                data.Version++;
                _loadedData[tile] = data;
                MarkDirty(tile);
                rebuiltChunks.Add(TerrainChunkCoord.FromTile(tile));
                changedAny = true;
            }
        }

        if (!changedAny)
            return;

        // Rebuild only the touched sub-region of each affected chunk in place (mesh + collider)
        // and batch-persist the modified tiles (one file per chunk, not one per tile).
        foreach (var tc in rebuiltChunks)
        {
            ChunkObject obj;
            if (!_loadedChunks.TryGetValue(tc, out obj))
                continue;
            RebuildChunkRegion(tc, obj, minCX, minCZ, maxCX, maxCZ);
            FlushDirtyChunk(tc);
        }
    }

    /// <summary>
    /// Rebuilds the mesh quads spanned by the deformation over one chunk. Builds mesh data for
    /// just the touched local-tile rectangle and patches the merged mesh in place (fast, and the
    /// collider re-cooks once against the same mesh). Falls back to a full 900-tile rebuild only
    /// when the patch would cover nearly the whole chunk.
    /// </summary>
    private void RebuildChunkRegion(TerrainChunkCoord tc, ChunkObject obj,
        int minCX, int minCZ, int maxCX, int maxCZ)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);

        int lMinX = Mathf.Clamp(minCX - cminX, 0, cs - 1);
        int lMinZ = Mathf.Clamp(minCZ - cminZ, 0, cs - 1);
        int lMaxX = Mathf.Clamp(maxCX - cminX, 0, cs - 1);
        int lMaxZ = Mathf.Clamp(maxCZ - cminZ, 0, cs - 1);
        int w = lMaxX - lMinX + 1;
        int h = lMaxZ - lMinZ + 1;

        if (w * h >= TerrainChunkCoord.ChunkArea * 0.75f)
        {
            // Region is (almost) the whole chunk — full rebuild is the same cost and safest.
            var tiles = new ChunkMeshData[TerrainChunkCoord.ChunkArea];
            for (int localZ = 0; localZ < cs; localZ++)
            {
                for (int localX = 0; localX < cs; localX++)
                {
                    var tileCoord = new ChunkCoord(cminX + localX, cminZ + localZ);
                    if (!_loadedData.TryGetValue(tileCoord, out ChunkData tileData))
                        continue;
                    tiles[localZ * cs + localX] = ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers);
                }
            }
            obj.ApplyMerged(ChunkMeshGenerator.BuildMergedMeshData(tiles), GroundMaterial, buildCollider: true);
            return;
        }

        var region = new ChunkMeshData[w * h];
        for (int localZ = lMinZ; localZ <= lMaxZ; localZ++)
        {
            for (int localX = lMinX; localX <= lMaxX; localX++)
            {
                var tileCoord = new ChunkCoord(cminX + localX, cminZ + localZ);
                if (!_loadedData.TryGetValue(tileCoord, out ChunkData tileData))
                    continue;
                region[(localZ - lMinZ) * w + (localX - lMinX)] =
                    ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers);
            }
        }
        obj.PatchRegion(lMinX, lMinZ, lMaxX, lMaxZ, region);
    }

    private static long EncodeCorner(int cx, int cz) => ((long)cx << 32) | (uint)cz;

    private float CornerOrBase(int cx, int cz, Dictionary<long, float> newHeights)
    {
        return newHeights.TryGetValue(EncodeCorner(cx, cz), out float h) ? h : CurrentHeightOf(cx, cz);
    }

    /// <summary>Current height of a world corner from whichever loaded tile owns it
    /// (shared corners agree, so the first loaded tile wins).</summary>
    private float CurrentHeightOf(int cx, int cz)
    {
        ChunkCoord[] owners =
        {
            new ChunkCoord(cx, cz),         // SW slot of tile (cx, cz)
            new ChunkCoord(cx - 1, cz),     // SE slot of tile (cx-1, cz)
            new ChunkCoord(cx, cz - 1),     // NW slot of tile (cx, cz-1)
            new ChunkCoord(cx - 1, cz - 1), // NE slot of tile (cx-1, cz-1)
        };
        if (_loadedData.TryGetValue(owners[0], out ChunkData d0)) return d0.Heights[3];
        if (_loadedData.TryGetValue(owners[1], out ChunkData d1)) return d1.Heights[2];
        if (_loadedData.TryGetValue(owners[2], out ChunkData d2)) return d2.Heights[0];
        if (_loadedData.TryGetValue(owners[3], out ChunkData d3)) return d3.Heights[1];
        // Corner has no loaded owner tile — neutral base (only ever read by loaded tiles).
        return TerrainNoiseGenerator.GetHeight(Seed, cx + 0.5f, cz + 0.5f);
    }

    private void OnDestroy()
    {
        // Group remaining modified tiles by terrain chunk and persist each chunk once.
        if (_dirtyTiles.Count > 0)
        {
            var chunks = new HashSet<TerrainChunkCoord>();
            foreach (ChunkCoord tile in _dirtyTiles)
                chunks.Add(TerrainChunkCoord.FromTile(tile));
            foreach (TerrainChunkCoord tc in chunks)
                FlushDirtyChunk(tc);
        }
        _dirtyTiles.Clear();
    }
}