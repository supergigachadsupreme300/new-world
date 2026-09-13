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
    private readonly HashSet<ChunkCoord> _dirty = new HashSet<ChunkCoord>();

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
    /// Runs on a ThreadPool thread. Generates all 900 tiles for a terrain chunk,
    /// pre-computing 31x31 = 961 corner heights to avoid redundant noise calls,
    /// then merges the tile meshes into one thread-safe chunk mesh.
    /// </summary>
    private void BackgroundGenerateChunk(TerrainChunkCoord tc, long seed)
    {
        try
        {
            int cs = TerrainChunkCoord.ChunkSize;
            int gridSize = TerrainChunkCoord.CornerGridSize; // 31

            // Pre-compute all corner heights for the chunk (31x31 grid)
            float[,] corners = new float[gridSize, gridSize];
            for (int gz = 0; gz < gridSize; gz++)
            {
                for (int gx = 0; gx < gridSize; gx++)
                {
                    float worldX = (tc.X * cs + gx) * ChunkData.Size;
                    float worldZ = (tc.Z * cs + gz) * ChunkData.Size;
                    corners[gx, gz] = TerrainNoiseGenerator.GetHeight(seed, worldX, worldZ);
                }
            }

            // Build mesh data for each tile in the chunk
            ChunkMeshData[] tiles = new ChunkMeshData[cs * cs];
            for (int tz = 0; tz < cs; tz++)
            {
                for (int tx = 0; tx < cs; tx++)
                {
                    ChunkCoord tileCoord = new ChunkCoord(tc.X * cs + tx, tc.Z * cs + tz);
                    ChunkData data = new ChunkData(tileCoord.X, tileCoord.Z, seed);
                    data.Heights[0] = corners[tx, tz + 1];     // NW
                    data.Heights[1] = corners[tx + 1, tz + 1]; // NE
                    data.Heights[2] = corners[tx + 1, tz];     // SE
                    data.Heights[3] = corners[tx, tz];          // SW
                    data.Version = 1;

                    tiles[tz * cs + tx] = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers);
                }
            }

            TerrainChunkMeshData result = new TerrainChunkMeshData
            {
                Coord = tc,
                Tiles = tiles,
                Merged = ChunkMeshGenerator.BuildMergedMeshData(tiles),
            };
            _readyChunks.Enqueue(result);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WorldStreamer] Background chunk generation failed for {tc}: {ex.Message}");
            byte _;
            _chunksInFlight.TryRemove(tc, out _);
        }
    }

    // --- Main thread: finalize chunk data + create the chunk GameObject ---

    /// <summary>
    /// Dequeue completed terrain chunks and create ONE GameObject (merged mesh +
    /// single collider + props) per chunk on the main thread. A capped budget
    /// (ChunksPerFrame, max 8/tick) spreads the work so the whole render radius
    /// fills in about a second without frame hitches.
    /// </summary>
    private void FinalizeChunks()
    {
        int finalized = 0;
        int budget = Mathf.Max(1, Mathf.Min(ChunksPerFrame, 8));
        while (finalized < budget && _readyChunks.TryDequeue(out TerrainChunkMeshData chunk))
        {
            byte _;
            _chunksInFlight.TryRemove(chunk.Coord, out _);
            _pendingChunks.Remove(chunk.Coord);

            if (_loadedChunks.ContainsKey(chunk.Coord))
                continue;

            CreateChunkGameObject(chunk);
            finalized++;
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
    /// Creates the chunk GameObject (mesh + collider + props) and registers its
    /// 900 tiles in the tile-level lookup dictionaries for persistence/validation.
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
        obj.SpawnProps(Seed);
        _loadedChunks[tc] = obj;

        for (int i = 0; i < chunk.Tiles.Length; i++)
        {
            ChunkCoord tile = chunk.Tiles[i].Coord;
            _loadedData[tile] = chunk.Tiles[i].Data;
            _loadedObjects[tile] = obj;
        }
    }

    // --- Synchronous generation (for startup) ---

    /// <summary>
    /// Generate an entire terrain chunk synchronously on the main thread.
    /// Used at startup to ensure the spawn tile has terrain + colliders before
    /// the player is placed. Now builds a single merged mesh per chunk, so the
    /// boot charge is one GameObject + mesh instead of 900 tiles.
    /// </summary>
    public void GenerateChunkSync(TerrainChunkCoord tc)
    {
        if (_loadedChunks.ContainsKey(tc))
            return;

        int cs = TerrainChunkCoord.ChunkSize;
        int gridSize = TerrainChunkCoord.CornerGridSize;

        float[,] corners = new float[gridSize, gridSize];
        for (int gz = 0; gz < gridSize; gz++)
        {
            for (int gx = 0; gx < gridSize; gx++)
            {
                float worldX = (tc.X * cs + gx) * ChunkData.Size;
                float worldZ = (tc.Z * cs + gz) * ChunkData.Size;
                corners[gx, gz] = TerrainNoiseGenerator.GetHeight(Seed, worldX, worldZ);
            }
        }

        ChunkMeshData[] tiles = new ChunkMeshData[cs * cs];
        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                ChunkCoord tileCoord = new ChunkCoord(tc.X * cs + tx, tc.Z * cs + tz);
                ChunkData data = new ChunkData(tileCoord.X, tileCoord.Z, Seed);
                data.Heights[0] = corners[tx, tz + 1];
                data.Heights[1] = corners[tx + 1, tz + 1];
                data.Heights[2] = corners[tx + 1, tz];
                data.Heights[3] = corners[tx, tz];
                data.Version = 1;
                tiles[tz * cs + tx] = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers);
            }
        }

        TerrainChunkMeshData chunk = new TerrainChunkMeshData
        {
            Coord = tc,
            Tiles = tiles,
            Merged = ChunkMeshGenerator.BuildMergedMeshData(tiles),
        };
        CreateChunkGameObject(chunk);
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

        // Persist any modified tiles in the chunk, then drop tile-level bookkeeping.
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                ChunkCoord tile = new ChunkCoord(x, z);
                if (_dirty.Contains(tile) && _loadedData.TryGetValue(tile, out ChunkData data))
                {
                    ChunkSaveManager.Save(Seed, tile.X, tile.Z, data);
                    _dirty.Remove(tile);
                }
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
        _dirty.Add(coord);
        if (ChunkSaveManager.SynchronousWrites)
        {
            if (_loadedData.TryGetValue(coord, out ChunkData data))
                ChunkSaveManager.Save(Seed, coord.X, coord.Z, data);
        }
    }

    private void OnDestroy()
    {
        foreach (ChunkCoord coord in _dirty)
        {
            if (_loadedData.TryGetValue(coord, out ChunkData data))
                ChunkSaveManager.Save(Seed, coord.X, coord.Z, data);
        }
        _dirty.Clear();
    }
}