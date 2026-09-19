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

    /// <summary>Plausible terrain-height band (5-octave noise max ≈ ±63.5 m + ≤ ~4.4 m
    /// deformation headroom). Rejects garbage from a corrupt/non-finite chunk save so it can
    /// never reach a chunk mesh + MeshCollider — a stray ±1000s vertex poisons the physics
    /// broadphase and the Character Controller depenetrates the player thousands of metres on
    /// one step.</summary>
    private const float MaxTerrainHeight = 200f;

    /// <summary>True when a height value is finite and inside the plausible terrain band.
    /// Invalid values are treated as missing corners so they regenerate from noise.</summary>
    private static bool IsSaneHeight(float h)
    {
        return float.IsFinite(h) && h > -MaxTerrainHeight && h < MaxTerrainHeight;
    }

    /// <summary>How much of a legacy 1cg flat-slab level is kept when re-smoothing a tile toward
    /// its noise on load (1 = keep the slab, 0 = fully revert to noise). 0.5 turns a hard block
    /// into a gentle rounded rise/dip while still visibly preserving the player's edit.</summary>
    private const float OldSlabRelaxKeep = 0.5f;

    /// <summary>
    /// Re-smooths any flat modified tile when it loads (1cl): if all 4 corners are equal and the
    /// level clearly deviates from the local noise, each corner is blended back toward its own
    /// noise height rather than set to the same level — the plateau becomes a gentle smooth
    /// rise/dip with no vertical step. This covers BOTH legacy 1cg-era whole-metre slabs and the
    /// fractional flat plateaus that older Earth carves produced (clamped crater floors / shape
    /// caps), so a carve from any build always reads as smooth terrain instead of a torn-out or
    /// blocked tile. Pure-noise flat tiles (corners already match noise) are left untouched.
    /// Current Earth shapes are never flat to begin with (per-corner caps), so they never re-blend.
    /// In-memory only: the save file keeps the plateau so the relaxation is deterministic and
    /// idempotent, and the next player deformation on the tile persists the smooth values
    /// naturally.
    /// </summary>
    private static void RelaxLegacySlabTile(ref ChunkData data)
    {
        if (!data.HasModifications || !ChunkMeshGenerator.IsFlatTile(data))
            return;

        float flat = data.Heights[0];

        float[] noise = new float[4];
        noise[0] = TerrainNoiseGenerator.GetHeight(data.Seed, data.VertexWorldX(0), data.VertexWorldZ(0)); // NW
        noise[1] = TerrainNoiseGenerator.GetHeight(data.Seed, data.VertexWorldX(1), data.VertexWorldZ(1)); // NE
        noise[2] = TerrainNoiseGenerator.GetHeight(data.Seed, data.VertexWorldX(2), data.VertexWorldZ(2)); // SE
        noise[3] = TerrainNoiseGenerator.GetHeight(data.Seed, data.VertexWorldX(3), data.VertexWorldZ(3)); // SW

        // A slab that already sits at the noise level is just normal terrain — leave it alone.
        float worstDeviation = 0f;
        for (int i = 0; i < 4; i++)
            worstDeviation = Mathf.Max(worstDeviation, Mathf.Abs(noise[i] - flat));
        if (worstDeviation < 0.15f)
            return;

        for (int i = 0; i < 4; i++)
            data.Heights[i] = ChunkMeshGenerator.SanitizeHeight(Mathf.Lerp(noise[i], flat, OldSlabRelaxKeep));
        data.Version++;
    }

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

    private void Awake()
    {
        // Cache the chunk save path on the MAIN thread: Application.persistentDataPath is
        // main-thread-only in modern Unity, but chunk generation reads it on background
        // threads (BuildOrLoadChunk -> ChunkSaveManager.TryLoadChunk). Warm it up before the
        // first background dispatch so the worker threads only touch the cached string.
        ChunkSaveManager.Warmup();
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
        // CRITICAL: the grid MUST be NaN-seeded, not zero-seeded. `new float[,]` zero-fills every
        // element, and zero is a VALID height — so an unstamped corner would read as "present"
        // (float.IsNaN(0f) is false) and the whole chunk would collapse to height 0 instead of
        // regenerating from noise. Prefill with NaN so only genuinely saved corners count as
        // present and every other corner rolls.
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
                // Only stamp finite, in-band heights; any other value leaves the corner as
                // NaN so the regeneration loop below re-rolls it from noise (never garbage).
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
                        // Reject any garbage slot — fall back to the (already-sanitized/
                        // regenerated) corner grid so the high value never enters the mesh.
                        data.Heights[0] = IsSaneHeight(m.Heights[0]) ? m.Heights[0] : corners[tx, tz + 1];     // NW
                        data.Heights[1] = IsSaneHeight(m.Heights[1]) ? m.Heights[1] : corners[tx + 1, tz + 1]; // NE
                        data.Heights[2] = IsSaneHeight(m.Heights[2]) ? m.Heights[2] : corners[tx + 1, tz];     // SE
                        data.Heights[3] = IsSaneHeight(m.Heights[3]) ? m.Heights[3] : corners[tx, tz];          // SW
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
                // Re-smooth legacy flat-slab tiles toward their noise on load (1cj) BEFORE the
                // mesh is built, so the rendered terrain matches the heights.
                RelaxLegacySlabTile(ref data);
                tiles[tz * cs + tx] = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers);
            }
        }

        return new TerrainChunkMeshData
        {
            Coord = tc,
            Tiles = tiles,
            Merged = ChunkMeshGenerator.BuildMergedMeshData(tiles, null, seed),
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
            // Slab seam walls need BOTH sides of a chunk boundary in memory to render with real
            // neighbour heights — reconcile now that this chunk's tiles exist.
            ReconcileNewlyLoadedChunk(chunk.Coord);
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

        TerrainChunkMeshData chunk = BuildOrLoadChunk(tc, Seed);
        CreateChunkGameObject(chunk);
        ReconcileNewlyLoadedChunk(tc);
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
    /// </summary>
    public void ResetTerrainSaves()
    {
        ChunkSaveManager.ResetWorldSaves(Seed);
        _dirtyTiles.Clear();

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
    /// Earth spells carry no status effect — instead they deform the ground as smooth feathered
    /// terrain edits (Ring: a raised annular wall; Spikes: scattered stone spikes; Wall: an
    /// elongated ridge rearing along <paramref name="dir"/>; Pillar: a tall column at the
    /// center; Crater: a wide shallow dish excavated downward). Heights are written as
    /// continuous per-corner elevations — never quantized blocks — so a deform blends into the
    /// untouched turf with a smoothstep rim. Raised shapes (Wall/Ring/Pillar/Spikes) are bounded
    /// AND idempotent: they raise toward a per-corner target of (original noise height + blended
    /// lift) applied with Max against the current height, so a repeat cast reproduces the same
    /// profile and can never stack higher. A Crater is deliberately the inverse — each cast/swing
    /// excavates another CraterStep below the current floor, so pits dig progressively deeper
    /// (revealing the dirt/stone strata bands) with no cap of their own: only the ±MaxTerrainHeight
    /// sanity band bounds them. Each touched tile is marked modified/dirty so it persists and syncs
    /// (deformations last forever — chunk save files, §2.6), and the affected region of each chunk is
    /// rebuilt (merged mesh + collider) in place. Unloaded tiles are ignored — spells only deform
    /// terrain the streamer has in memory.
    /// </para>
    /// </summary>
    public void DeformAt(Vector3 center, float radius, TerrainShape shape, Vector3 dir = default)
    {
        if (shape == TerrainShape.None || radius <= 0f) return;

        // Never raise the ground directly beneath the player's feet: a Wall/ring/pillar rearing
        // up under the capsule embeds it in the rebuilt chunk collider, and the next physics step
        // depenetrates it violently — reads as a teleport. Raised shapes skip tiles inside a small
        // keep-out ring around the player's feet; Crater (excavation) is unaffected.
        float keepOutR = 0.9f; // player capsule radius + margin
        bool protectCaster = shape != TerrainShape.Crater;
        Vector3? casterFeet = null;
        if (protectCaster)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
                casterFeet = player.transform.position;
        }

        float feather = 0.5f;
        float reach = radius + feather;
        int minCX = Mathf.FloorToInt(center.x - reach);
        int maxCX = Mathf.FloorToInt(center.x + reach);
        int minCZ = Mathf.FloorToInt(center.z - reach);
        int maxCZ = Mathf.FloorToInt(center.z + reach);

        // Wall orientation: the cast direction projected onto the XZ plane.
        Vector3 wallDir = new Vector3(dir.x, 0f, dir.z);
        if (wallDir.sqrMagnitude < 0.0001f)
            wallDir = Vector3.right;
        wallDir.Normalize();

        // Ring: a raised annulus with its center left level. Spikes: a smooth mound + sparse
        // deterministic peaks so the ground reads jagged but never chessboard-y. Wall: a ridge
        // band along the cast direction (tall enough to fully block the player). Pillar: a tall
        // column. Crater: a wide dish, dug down.
        float lift = shape == TerrainShape.Ring ? 0.9f
            : shape == TerrainShape.Pillar ? 1.8f
            : shape == TerrainShape.Wall ? 2.6f
            : 0.7f; // Spikes
        // Excavation step per Crater cast/swing (~1.1 m at full influence, feathered at the rim).
        // Unlike the raised shapes (which are IDEMPOTENT and capped), a crater ratchets the floor
        // DOWN by the step every cast: pits dig progressively deeper — through dirt, then stone —
        // with no floor cap of their own. The only bound is WorldStreamer's ±MaxTerrainHeight
        // sanity band (SanitizeHeight), which exists to protect the mesh/collider, not to limit
        // how deep an excavator may go.
        const float CraterStep = 1.1f;
        float ringMid = radius * 0.72f;
        float ringHalfWidth = Mathf.Max(0.6f, radius * 0.28f);
        float pillarCore = radius * 0.45f;
        float wallHalfThick = Mathf.Max(0.6f, radius * 0.25f);
        float wallHalfLen = radius;

        // New height for every world corner (integer x/z) inside the reach. Continuous values,
        // smoothstep-blended at the rim, so the deform reads as genuine terrain (not blocks).
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
                else if (shape == TerrainShape.Pillar)
                {
                    influence = dist <= pillarCore ? 1f
                        : Mathf.Clamp01(1f - (dist - pillarCore) / Mathf.Max(0.01f, radius - pillarCore));
                }
                else if (shape == TerrainShape.Wall)
                {
                    // Distance perpendicular to the cast axis (the ridge spine) + rounded length caps.
                    float along = dx * wallDir.x + dz * wallDir.z;
                    float perp = Mathf.Sqrt(Mathf.Max(0f, dx * dx + dz * dz - along * along));
                    float band = 1f - Mathf.Clamp01((perp - wallHalfThick) / Mathf.Max(0.01f, wallHalfThick));
                    float ends = 1f - Mathf.Clamp01((Mathf.Abs(along) - (wallHalfLen - wallHalfThick)) / Mathf.Max(0.01f, wallHalfThick));
                    influence = Mathf.Min(band, ends);
                }
                else if (shape == TerrainShape.Crater)
                {
                    influence = 1f - Mathf.Clamp01(dist / reach);
                }
                else // Spikes
                {
                    float fall = 1f - Mathf.Clamp01(dist / reach);
                    influence = fall * fall;
                }

                if (influence <= 0f)
                    continue;

                // Skip raising the ground inside the player's keep-out ring: this prevents a
                // Wall / Ring / Pillar from growing directly under the capsule and triggering
                // a violent depenetration "teleport" on the next physics step.
                if (protectCaster && casterFeet.HasValue)
                {
                    float pdx = wx - casterFeet.Value.x;
                    float pdz = wz - casterFeet.Value.z;
                    if (pdx * pdx + pdz * pdz <= keepOutR * keepOutR)
                        continue;
                }

                // Smooth the influence curve (smootherstep) so the deform blends out at the rim.
                float s = influence * influence * (3f - 2f * influence);
                float current = CurrentHeightOf(cx, cz);

                if (shape == TerrainShape.Crater)
                {
                    // Deliberate per-cast excavation: lower each corner by s*CraterStep below its
                    // CURRENT floor. Repeating the cast (or swinging a digging tool) deepens the pit
                    // each time — the inverse of the raised shapes' idempotency — so the player can
                    // dig indefinitely deep (revealing the dirt/stone strata bands). The rim stays
                    // feathered (s ~ 0 at influence edge) so the pit is a smooth bowl, never a cliff;
                    // corners keep their own slope, so the dish is never a flat slab floor.
                    float target = current - s * CraterStep;
                    newHeights[EncodeCorner(cx, cz)] = target;
                }
                else
                {
                    // Raise toward the per-corner ridge target (pristine noise + blended lift), then
                    // Max against the CURRENT height so the edit is IDEMPOTENT: a repeat cast at the
                    // same spot recomputes the same target and changes nothing. The old additive form
                    // (`current + s*lift` capped) kept lifting the whole influence footprint every
                    // cast — steepest near the crest, but a wide low-influence swath too — so the
                    // ground visibly rose across the chunk on the second+ cast. The per-corner target
                    // also keeps the crest a smooth rounded ridge (never a flat slab), and Max can
                    // never LOWER terrain that already sits above the target.
                    float baseY = TerrainNoiseGenerator.GetHeight(Seed, cx, cz);
                    float target = baseY + s * lift;

                    // Spikes: a deterministic few corners jump higher so the field reads jagged.
                    if (shape == TerrainShape.Spikes)
                    {
                        int raw = (cx * 73856093) ^ (cz * 19349663) ^ Seed.GetHashCode();
                        float r = (raw & 0x7fffffff) / (float)0x7fffffff;
                        if (r > 0.78f)
                            target += lift * (0.4f + r * 0.6f) * influence * influence;
                    }

                    // Preserve the absolute raise cap (pristine + lift): the target never exceeds it,
                    // so no shape — zone, storm, summon, or projectile — can stack unbounded.
                    target = Mathf.Min(target, baseY + lift);

                    newHeights[EncodeCorner(cx, cz)] = Mathf.Max(current, target);
                }
            }
        }

        if (newHeights.Count == 0)
            return;

        ApplyHeightEdits(minCX, minCZ, maxCX, maxCZ, newHeights);
    }

    /// <summary>
    /// Full rebuild of one chunk's merged mesh + collider from the in-memory tile data, feeding
    /// the boundary ring (adjacent loaded chunks' corner heights) so cross-chunk seams are
    /// seamless (used when border-corner reconciles need full re-emission). Main thread only.
    /// </summary>
    private void FullRebuildChunk(TerrainChunkCoord tc)
    {
        if (!_loadedChunks.TryGetValue(tc, out ChunkObject obj))
            return;

        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);
        int cs = TerrainChunkCoord.ChunkSize;
        var tiles = new ChunkMeshData[cs * cs];
        bool anyMissing = false;
        for (int localZ = 0; localZ < cs; localZ++)
        {
            for (int localX = 0; localX < cs; localX++)
            {
                var tileCoord = new ChunkCoord(cminX + localX, cminZ + localZ);
                if (_loadedData.TryGetValue(tileCoord, out ChunkData tileData))
                    tiles[localZ * cs + localX] =
                        ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers);
                else
                    anyMissing = true;
            }
        }

        MergedChunkMeshData merged;
        if (anyMissing)
        {
            // A loaded chunk with missing tile bookkeeping (mid unload/reload at the streaming
            // edge or the arena-lane rebuild race) must NOT rebuild sparse: null entries are
            // filled with noise by the merged-mesh builder, which is better than a gap, but the
            // cleanest result is a whole-chunk rebuild from saves/noise — every quad emitted.
            merged = BuildOrLoadChunk(tc, Seed).Merged;
        }
        else
        {
            merged = ChunkMeshGenerator.BuildMergedMeshData(tiles, BuildBorderCorners(tc), Seed);
        }
        obj.ApplyMerged(merged, GroundMaterial, buildCollider: true);
    }

    /// <summary>
    /// Corner heights for the one-tile ring OUTSIDE a chunk, from loaded tiles only. Unloaded
    /// neighbour terrain is left out so the mesh builder falls back to the same deterministic
    /// world noise the pristine corner grid uses (no phantom walls on untouched seams).
    /// </summary>
    private Dictionary<long, float> BuildBorderCorners(TerrainChunkCoord tc)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);

        var border = new Dictionary<long, float>(cs * 4);
        for (int gx = 0; gx <= cs; gx++)
        {
            CornerIfLoaded(cminX + gx, cminZ - 1, border);
            CornerIfLoaded(cminX + gx, cmaxZ + 1, border);
        }
        for (int gz = 0; gz <= cs; gz++)
        {
            CornerIfLoaded(cminX - 1, cminZ + gz, border);
            CornerIfLoaded(cmaxX + 1, cminZ + gz, border);
        }
        return border;
    }

    private void CornerIfLoaded(int wx, int wz, Dictionary<long, float> border)
    {
        ChunkCoord[] owners =
        {
            new ChunkCoord(wx, wz),         // SW slot of tile (wx, wz)
            new ChunkCoord(wx - 1, wz),     // SE slot of tile (wx-1, wz)
            new ChunkCoord(wx, wz - 1),     // NW slot of tile (wx, wz-1)
            new ChunkCoord(wx - 1, wz - 1), // NE slot of tile (wx-1, wz-1)
        };
        if (_loadedData.TryGetValue(owners[0], out ChunkData d0)) border[EncodeCorner(wx, wz)] = d0.Heights[3];
        else if (_loadedData.TryGetValue(owners[1], out ChunkData d1)) border[EncodeCorner(wx, wz)] = d1.Heights[2];
        else if (_loadedData.TryGetValue(owners[2], out ChunkData d2)) border[EncodeCorner(wx, wz)] = d2.Heights[0];
        else if (_loadedData.TryGetValue(owners[3], out ChunkData d3)) border[EncodeCorner(wx, wz)] = d3.Heights[1];
    }

    /// <summary>True when any tile of the chunk carries a localised player modification.</summary>
    private bool ChunkHasModifiedTiles(TerrainChunkCoord tc)
    {
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                if (_loadedData.TryGetValue(new ChunkCoord(x, z), out ChunkData d) && d.HasModifications)
                    return true;
            }
        }
        return false;
    }

    /// <summary>True when any tile of the chunk is a flat-top block (held any 1cg slab).</summary>
    private bool ChunkContainsFlatTile(TerrainChunkCoord tc)
    {
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                if (_loadedData.TryGetValue(new ChunkCoord(x, z), out ChunkData d)
                    && ChunkMeshGenerator.IsFlatTile(d))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// After a chunk loads/changes, rebuild any loaded modified orthogonal neighbour so a slab
    /// wall that straddles a chunk seam gets the true neighbour edge as its wall bottom (the
    /// neighbour owns the wall when it is the higher side). Bounded — only modified chunks.
    /// </summary>
    private void ReconcileModifiedBorders(TerrainChunkCoord tc)
    {
        TerrainChunkCoord[] neighbours =
        {
            new TerrainChunkCoord(tc.X + 1, tc.Z),
            new TerrainChunkCoord(tc.X - 1, tc.Z),
            new TerrainChunkCoord(tc.X, tc.Z + 1),
            new TerrainChunkCoord(tc.X, tc.Z - 1),
        };
        foreach (var n in neighbours)
        {
            if (!_loadedChunks.ContainsKey(n) || !ChunkHasModifiedTiles(n))
                continue;
            FullRebuildChunk(n);
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

    /// <summary>
    /// Levels a rectangular patch of the loaded heightmap to a target height, blending out over a
    /// feathered rim. Routes through the exact same tile-edit + chunk-rebuild + persistence pipeline
    /// as <see cref="DeformAt"/>, so the result is genuine generated terrain (mesh, collider, save
    /// files), not a floating overlay. Unloaded tiles are ignored, so callers must wait for the
    /// patch's chunks (see <see cref="LoadedChunks"/>) before flattening.
    /// </summary>
    public void FlattenAt(Vector3 center, float halfSize, float targetHeight, float feather = 3f)
    {
        if (halfSize <= 0f) return;

        int minCX = Mathf.FloorToInt(center.x - halfSize - feather);
        int maxCX = Mathf.FloorToInt(center.x + halfSize + feather);
        int minCZ = Mathf.FloorToInt(center.z - halfSize - feather);
        int maxCZ = Mathf.FloorToInt(center.z + halfSize + feather);

        // Pad interiors go fully level to the target; the rim blends influence 1 → 0 over `feather`
        // units (smootherstep) so the flat arena melts into the untouched surrounding terrain.
        var newHeights = new Dictionary<long, float>();
        for (int cz = minCZ; cz <= maxCZ; cz++)
        {
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                float wx = cx + 0.5f;
                float wz = cz + 0.5f;
                float ix = 1f - Mathf.Clamp01((Mathf.Abs(wx - center.x) - halfSize) / Mathf.Max(0.01f, feather));
                float iz = 1f - Mathf.Clamp01((Mathf.Abs(wz - center.z) - halfSize) / Mathf.Max(0.01f, feather));
                float influence = Mathf.Min(ix, iz);
                if (influence <= 0f) continue;
                float s = influence * influence * (3f - 2f * influence);
                newHeights[EncodeCorner(cx, cz)] = Mathf.Lerp(CurrentHeightOf(cx, cz), targetHeight, s);
            }
        }

        if (newHeights.Count == 0)
            return;

        ApplyHeightEdits(minCX, minCZ, maxCX, maxCZ, newHeights);
    }

    /// <summary>
    /// Writes an edited corner set into every loaded tile it touches, marks them dirty, and rebuilds
    /// the affected chunks' meshes + colliders (and flushes their save files). Shared by shape
    /// deformation (<see cref="DeformAt"/>) and <see cref="FlattenAt"/>.
    /// Corners not in the set simply keep their current (unchanged) height, so shared edges with
    /// untouched neighbours line up perfectly.
    /// </summary>
    private void ApplyHeightEdits(int minCX, int minCZ, int maxCX, int maxCZ, Dictionary<long, float> newHeights)
    {
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
    /// Rebuilds the mesh quads spanned by the deformation over one chunk. Slab side walls change
    /// the merged mesh's vertex counts, so once a chunk holds any flat-top block tile it is FULL
    /// rebuilt (with real neighbour border heights); otherwise the fast in-place region patch
    /// (smooth blending — e.g. farm-plot flattening) is used, falling back to a full rebuild when
    /// the patch would cover nearly the whole chunk.
    /// </summary>
    private void RebuildChunkRegion(TerrainChunkCoord tc, ChunkObject obj,
        int minCX, int minCZ, int maxCX, int maxCZ)
    {
        int cs = TerrainChunkCoord.ChunkSize;
        tc.GetTileRange(out int cminX, out int cminZ, out int cmaxX, out int cmaxZ);

        // Any flat slab tile in the chunk forces a full rebuild (side walls change vertex counts).
        if (ChunkContainsFlatTile(tc))
        {
            FullRebuildChunk(tc);
            return;
        }

        int lMinX = Mathf.Clamp(minCX - cminX, 0, cs - 1);
        int lMinZ = Mathf.Clamp(minCZ - cminZ, 0, cs - 1);
        int lMaxX = Mathf.Clamp(maxCX - cminX, 0, cs - 1);
        int lMaxZ = Mathf.Clamp(maxCZ - cminZ, 0, cs - 1);
        int w = lMaxX - lMinX + 1;
        int h = lMaxZ - lMinZ + 1;

        if (w * h >= TerrainChunkCoord.ChunkArea * 0.75f)
        {
            // Region is (almost) the whole chunk — full rebuild is the same cost and safest.
            FullRebuildChunk(tc);
            return;
        }

        var region = new ChunkMeshData[w * h];
        for (int localZ = lMinZ; localZ <= lMaxZ; localZ++)
        {
            for (int localX = lMinX; localX <= lMaxX; localX++)
            {
                var tileCoord = new ChunkCoord(cminX + localX, cminZ + localZ);
                if (!_loadedData.TryGetValue(tileCoord, out ChunkData tileData))
                    tileData = ChunkMeshGenerator.BuildFallbackTileData(cminX + localX, cminZ + localZ, Seed);
                region[(localZ - lMinZ) * w + (localX - lMinX)] =
                    ChunkMeshGenerator.BuildMeshData(tileData, TerrainNoiseGenerator.DefaultLayers);
            }
        }
        obj.PatchRegion(lMinX, lMinZ, lMaxX, lMaxZ, region, Seed);
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

    /// <summary>
    /// Current dig depth at a world-space ground point: how far the current floor sits BELOW the
    /// pristine noise surface (positive = dug down, ~0 = untouched, negative = raised terrain).
    /// Tools use this to gate the dirt/stone boundary — e.g. the shovel stops once a pit reaches
    /// the stone band and the pickaxe takes over.
    /// </summary>
    public float GetDigDepth(float worldX, float worldZ)
    {
        int cx = Mathf.FloorToInt(worldX);
        int cz = Mathf.FloorToInt(worldZ);
        return TerrainNoiseGenerator.GetHeight(Seed, cx, cz) - CurrentHeightOf(cx, cz);
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