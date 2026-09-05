using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// The main orchestrator for the seamless open world.
///
/// The world is divided into TerrainChunks, each covering a 30x30 block of
/// individual tiles. Background threads generate one TerrainChunk at a time,
/// producing all 900 tile meshes in a single ThreadPool dispatch. The main
/// thread creates GameObjects from a tile finalization queue, spreading the
/// work across frames to avoid hitches.
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
    [Tooltip("Max tiles finalized per poll tick (main-thread work).")]
    public int ChunksPerFrame = 32;

    [Tooltip("Max terrain chunks being generated on background threads simultaneously.")]
    public int MaxInFlight = 4;

    // --- Tile-level state (existing public API) ---
    private readonly Dictionary<ChunkCoord, ChunkData> _loadedData = new Dictionary<ChunkCoord, ChunkData>();
    private readonly Dictionary<ChunkCoord, ChunkObject> _loadedObjects = new Dictionary<ChunkCoord, ChunkObject>();
    private readonly HashSet<ChunkCoord> _dirty = new HashSet<ChunkCoord>();

    // --- Chunk-level dispatch ---
    private readonly HashSet<TerrainChunkCoord> _pendingChunks = new HashSet<TerrainChunkCoord>();
    private readonly List<TerrainChunkCoord> _chunkDispatchOrder = new List<TerrainChunkCoord>();
    private readonly ConcurrentDictionary<TerrainChunkCoord, byte> _chunksInFlight = new ConcurrentDictionary<TerrainChunkCoord, byte>();
    private readonly ConcurrentQueue<TerrainChunkMeshData> _readyChunks = new ConcurrentQueue<TerrainChunkMeshData>();

    // --- Tile finalization queue ---
    private struct TileFinalization
    {
        public ChunkCoord Tile;
        public ChunkMeshData MeshData;
    }
    private readonly Queue<TileFinalization> _tileFinalizeQueue = new Queue<TileFinalization>();

    // --- Hierarchy containers (Terrain > Chunks > Chunk_X_Z > Tile_X_Z) ---
    private Transform _terrainRoot;
    private Transform _chunksRoot;
    private readonly Dictionary<TerrainChunkCoord, Transform> _chunkContainers = new Dictionary<TerrainChunkCoord, Transform>();
    private readonly Dictionary<TerrainChunkCoord, int> _chunkTileCount = new Dictionary<TerrainChunkCoord, int>();

    private Transform _focus;
    private float _timer;
    private const float PollInterval = 0.1f;

    public IReadOnlyDictionary<ChunkCoord, ChunkObject> Loaded => _loadedObjects;

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
        FinalizeReadyChunks();
        FinalizeTileQueue();
    }

    // --- Chunk-level streaming ---

    /// <summary>
    /// Unloads out-of-range chunks and populates the pending chunk queue
    /// for background generation.
    /// </summary>
    public void StreamAround(TerrainChunkCoord centre, int radius)
    {
        // Unload tiles from chunks that fell outside the radius
        List<ChunkCoord> toUnload = new List<ChunkCoord>();
        foreach (ChunkCoord tile in _loadedObjects.Keys)
        {
            TerrainChunkCoord tc = TerrainChunkCoord.FromTile(tile);
            int dx = Mathf.Abs(tc.X - centre.X);
            int dz = Mathf.Abs(tc.Z - centre.Z);
            if (dx > radius || dz > radius)
                toUnload.Add(tile);
        }
        foreach (ChunkCoord tile in toUnload)
            UnloadChunk(tile);

        // Remove pending chunks that fell outside the radius
        for (int i = _chunkDispatchOrder.Count - 1; i >= 0; i--)
        {
            TerrainChunkCoord tc = _chunkDispatchOrder[i];
            int dx = Mathf.Abs(tc.X - centre.X);
            int dz = Mathf.Abs(tc.Z - centre.Z);
            if (dx > radius || dz > radius)
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
        // Skip if any tile in this chunk is already loaded
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        if (_loadedObjects.ContainsKey(new ChunkCoord(minX, minZ)))
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

        // Clean up loaded chunks from dispatch list
        _chunkDispatchOrder.RemoveAll(c => _pendingChunks.Contains(c) && IsChunkFullyLoaded(c));
    }

    private bool IsChunkFullyLoaded(TerrainChunkCoord tc)
    {
        tc.GetTileRange(out int minX, out int minZ, out int maxX, out int maxZ);
        return _loadedObjects.ContainsKey(new ChunkCoord(minX, minZ));
    }

    // --- Background thread: generate entire chunk ---

    /// <summary>
    /// Runs on a ThreadPool thread. Generates all 900 tiles for a terrain chunk,
    /// pre-computing 31x31 = 961 corner heights to avoid redundant noise calls.
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

                    ChunkMeshData md = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers);
                    tiles[tz * cs + tx] = md;
                }
            }

            TerrainChunkMeshData result = new TerrainChunkMeshData
            {
                Coord = tc,
                Tiles = tiles,
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

    // --- Main thread: finalize chunk data + tile queue ---

    /// <summary>
    /// Dequeue completed terrain chunks and push their tiles into the
    /// finalization queue for main-thread GO creation.
    /// </summary>
    private void FinalizeReadyChunks()
    {
        TerrainChunkMeshData chunk;
        if (_readyChunks.TryDequeue(out chunk))
        {
            byte _;
            _chunksInFlight.TryRemove(chunk.Coord, out _);
            _pendingChunks.Remove(chunk.Coord);

            // Skip if tiles already exist (e.g. re-loaded from disk)
            ChunkCoord firstTile = new ChunkCoord(
                chunk.Coord.X * TerrainChunkCoord.ChunkSize,
                chunk.Coord.Z * TerrainChunkCoord.ChunkSize);
            if (_loadedObjects.ContainsKey(firstTile))
                return;

            // Push all tiles into the finalization queue
            for (int i = 0; i < chunk.Tiles.Length; i++)
            {
                _tileFinalizeQueue.Enqueue(new TileFinalization
                {
                    Tile = chunk.Tiles[i].Coord,
                    MeshData = chunk.Tiles[i],
                });
            }
        }
    }

    /// <summary>
    /// Pop tiles from the finalization queue and create GameObjects on the main
    /// thread. Limited to ChunksPerFrame per tick to avoid hitches.
    /// </summary>
    private void FinalizeTileQueue()
    {
        int finalized = 0;
        while (finalized < ChunksPerFrame && _tileFinalizeQueue.Count > 0)
        {
            TileFinalization tf = _tileFinalizeQueue.Dequeue();

            if (_loadedObjects.ContainsKey(tf.Tile))
                continue;

            ChunkData data = tf.MeshData.Data;
            _loadedData[tf.Tile] = data;

            ChunkObject obj = CreateOrPool(tf.Tile);
            obj.ApplyMeshData(tf.MeshData, GroundMaterial, buildCollider: true);
            obj.SpawnProps(Seed);
            _loadedObjects[tf.Tile] = obj;

            finalized++;
        }
    }

    // --- Synchronous generation (for startup) ---

    /// <summary>
    /// Generate an entire terrain chunk synchronously on the main thread.
    /// Used at startup to ensure the spawn area has terrain + colliders before
    /// the player is placed.
    /// </summary>
    public void GenerateChunkSync(TerrainChunkCoord tc)
    {
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

        for (int tz = 0; tz < cs; tz++)
        {
            for (int tx = 0; tx < cs; tx++)
            {
                ChunkCoord tileCoord = new ChunkCoord(tc.X * cs + tx, tc.Z * cs + tz);
                if (_loadedObjects.ContainsKey(tileCoord))
                    continue;

                ChunkData data = new ChunkData(tileCoord.X, tileCoord.Z, Seed);
                data.Heights[0] = corners[tx, tz + 1];
                data.Heights[1] = corners[tx + 1, tz + 1];
                data.Heights[2] = corners[tx + 1, tz];
                data.Heights[3] = corners[tx, tz];
                data.Version = 1;

                _loadedData[tileCoord] = data;

                ChunkObject obj = CreateOrPool(tileCoord);
                obj.Apply(data, TerrainNoiseGenerator.DefaultLayers, GroundMaterial, buildCollider: true);
                obj.SpawnProps(Seed);
                _loadedObjects[tileCoord] = obj;
            }
        }
    }

    // --- Object lifecycle ---

    private ChunkObject CreateOrPool(ChunkCoord coord)
    {
        GameObject go = new GameObject($"Tile_{coord.X}_{coord.Z}");
        go.isStatic = false;
        go.transform.SetParent(GetChunkContainer(coord), false);
        go.transform.position = new Vector3(coord.X * ChunkData.Size, 0f, coord.Z * ChunkData.Size);
        var obj = go.AddComponent<ChunkObject>();
        obj.Init(coord);
        return obj;
    }

    /// <summary>
    /// Get (or lazily create) the container the given tile's terrain chunk lives under.
    /// Organises the scene hierarchy as Terrain > Chunks > Chunk_X_Z > Tile_X_Z so tiles
    /// are grouped into their 30x30 chunks instead of sitting flat at the scene root.
    /// Containers stay at the world origin; tiles keep world-space positions.
    /// </summary>
    private Transform GetChunkContainer(ChunkCoord tile)
    {
        TerrainChunkCoord tc = TerrainChunkCoord.FromTile(tile);
        Transform container;
        if (_chunkContainers.TryGetValue(tc, out container))
        {
            _chunkTileCount[tc] = _chunkTileCount[tc] + 1;
            return container;
        }

        if (_terrainRoot == null)
            _terrainRoot = new GameObject("Terrain").transform;
        if (_chunksRoot == null)
        {
            _chunksRoot = new GameObject("Chunks").transform;
            _chunksRoot.SetParent(_terrainRoot, false);
        }

        GameObject chunk = new GameObject($"Chunk_{tc.X}_{tc.Z}");
        chunk.transform.SetParent(_chunksRoot, false);
        _chunkContainers[tc] = chunk.transform;
        _chunkTileCount[tc] = 1;
        return chunk.transform;
    }

    /// <summary>
    /// Synchronous fallback: generate chunk on the main thread.
    /// Used by EnsureChunk for non-streaming callers (e.g. validation).
    /// </summary>
    public void EnsureChunk(ChunkCoord coord)
    {
        if (_loadedObjects.ContainsKey(coord))
            return;

        ChunkData data;
        if (!_loadedData.TryGetValue(coord, out data))
        {
            if (ChunkSaveManager.TryLoad(Seed, coord.X, coord.Z, out data))
            {
                // loaded from disk
            }
            else
            {
                data = GenerateChunk(coord);
            }
            _loadedData[coord] = data.ShallowCopy();
        }

        ChunkObject obj = CreateOrPool(coord);
        obj.Apply(data, TerrainNoiseGenerator.DefaultLayers, GroundMaterial, buildCollider: true);
        obj.SpawnProps(Seed);
        _loadedObjects[coord] = obj;
    }

    private ChunkData GenerateChunk(ChunkCoord coord)
    {
        ChunkData data = new ChunkData(coord.X, coord.Z, Seed);
        data.Heights[0] = TerrainNoiseGenerator.GetHeight(Seed, coord.X * ChunkData.Size, (coord.Z + 1) * ChunkData.Size);
        data.Heights[1] = TerrainNoiseGenerator.GetHeight(Seed, (coord.X + 1) * ChunkData.Size, (coord.Z + 1) * ChunkData.Size);
        data.Heights[2] = TerrainNoiseGenerator.GetHeight(Seed, (coord.X + 1) * ChunkData.Size, coord.Z * ChunkData.Size);
        data.Heights[3] = TerrainNoiseGenerator.GetHeight(Seed, coord.X * ChunkData.Size, coord.Z * ChunkData.Size);
        data.Version = 1;
        return data;
    }

    public void UnloadChunk(ChunkCoord coord)
    {
        ChunkObject obj;
        if (!_loadedObjects.TryGetValue(coord, out obj))
            return;

        if (_dirty.Contains(coord) && _loadedData.TryGetValue(coord, out ChunkData data))
        {
            ChunkSaveManager.Save(Seed, coord.X, coord.Z, data);
            _dirty.Remove(coord);
        }

        obj.Release();
        if (obj != null)
            Destroy(obj.gameObject);
        _loadedObjects.Remove(coord);

        // Destroy the chunk container once its last tile is gone.
        TerrainChunkCoord tc = TerrainChunkCoord.FromTile(coord);
        Transform container;
        if (_chunkContainers.TryGetValue(tc, out container))
        {
            int remaining = _chunkTileCount[tc] - 1;
            if (remaining <= 0)
            {
                _chunkTileCount.Remove(tc);
                _chunkContainers.Remove(tc);
                if (container != null)
                    Destroy(container.gameObject);
            }
            else
            {
                _chunkTileCount[tc] = remaining;
            }
        }
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
