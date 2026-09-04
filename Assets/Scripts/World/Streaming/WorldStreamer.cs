using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// The main orchestrator for the seamless open world.
///
/// Terrain generation, disk I/O, and mesh data computation run on background
/// threads via ThreadPool. Only GameObject creation, mesh assignment, and prop
/// spawning happen on the main thread — keeping the player responsive at all
/// times while tiles pop in as they finish.
///
/// The world is infinite in XZ: any chunk coordinate can be requested.
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
    [Tooltip("Max mesh builds finalized per poll tick (main-thread work).")]
    public int ChunksPerFrame = 8;

    [Tooltip("Max chunks being generated on background threads simultaneously.")]
    public int MaxInFlight = 32;

    private readonly Dictionary<ChunkCoord, ChunkData> _loadedData = new Dictionary<ChunkCoord, ChunkData>();
    private readonly Dictionary<ChunkCoord, ChunkObject> _loadedObjects = new Dictionary<ChunkCoord, ChunkObject>();
    private readonly HashSet<ChunkCoord> _dirty = new HashSet<ChunkCoord>();

    private readonly ConcurrentQueue<ChunkMeshData> _readyQueue = new ConcurrentQueue<ChunkMeshData>();

    // Thread-safe O(1) pending tracking + ordered dispatch list
    private readonly HashSet<ChunkCoord> _pendingSet = new HashSet<ChunkCoord>();
    private readonly List<ChunkCoord> _dispatchOrder = new List<ChunkCoord>();
    private readonly ConcurrentDictionary<ChunkCoord, byte> _inFlight = new ConcurrentDictionary<ChunkCoord, byte>();

    private Transform _focus;
    private float _timer;
    private const float PollInterval = 0.1f;

    public IReadOnlyDictionary<ChunkCoord, ChunkObject> Loaded => _loadedObjects;

    public bool TryGetData(ChunkCoord coord, out ChunkData data)
    {
        return _loadedData.TryGetValue(coord, out data);
    }

    public bool IsLoaded(ChunkCoord coord) => _loadedObjects.ContainsKey(coord);

    public void SetFocus(Transform focus)
    {
        _focus = focus;
    }

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

        int radius = RenderDistance != null ? RenderDistance.Radius : 5;
        ChunkCoord centre = WorldToChunk(_focus.position);

        StreamAround(centre, radius);
        FinalizeReadyChunks();
        DispatchPending();
    }

    public static ChunkCoord WorldToChunk(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / ChunkData.Size);
        int z = Mathf.FloorToInt(pos.z / ChunkData.Size);
        return new ChunkCoord(x, z);
    }

    /// <summary>
    /// Unloads out-of-range chunks and populates the pending queue for
    /// background generation. Does NOT generate anything synchronously.
    /// </summary>
    public void StreamAround(ChunkCoord centre, int radius)
    {
        // Unload chunks that fell outside the radius
        List<ChunkCoord> toUnload = new List<ChunkCoord>();
        foreach (ChunkCoord c in _loadedObjects.Keys)
        {
            int dx = Mathf.Abs(c.X - centre.X);
            int dz = Mathf.Abs(c.Z - centre.Z);
            if (dx > radius || dz > radius)
                toUnload.Add(c);
        }
        foreach (ChunkCoord c in toUnload)
            UnloadChunk(c);

        // Remove pending coords that fell outside the radius
        for (int i = _dispatchOrder.Count - 1; i >= 0; i--)
        {
            ChunkCoord c = _dispatchOrder[i];
            int dx = Mathf.Abs(c.X - centre.X);
            int dz = Mathf.Abs(c.Z - centre.Z);
            if (dx > radius || dz > radius)
            {
                _pendingSet.Remove(c);
                _dispatchOrder.RemoveAt(i);
            }
        }

        // Populate pending queue: walk rings closest-first
        for (int r = 0; r <= radius; r++)
        {
            int startX = centre.X - r, endX = centre.X + r;
            int startZ = centre.Z - r, endZ = centre.Z + r;

            for (int x = startX; x <= endX; x++)
                EnqueueIfNeeded(new ChunkCoord(x, endZ));
            for (int x = startX; x <= endX; x++)
                EnqueueIfNeeded(new ChunkCoord(x, startZ));
            for (int z = startZ + 1; z < endZ; z++)
                EnqueueIfNeeded(new ChunkCoord(startX, z));
            for (int z = startZ + 1; z < endZ; z++)
                EnqueueIfNeeded(new ChunkCoord(endX, z));
        }
    }

    private void EnqueueIfNeeded(ChunkCoord coord)
    {
        if (_loadedObjects.ContainsKey(coord))
            return;
        if (_inFlight.ContainsKey(coord))
            return;
        if (_pendingSet.Contains(coord))
            return;
        _pendingSet.Add(coord);
        _dispatchOrder.Add(coord);
    }

    /// <summary>
    /// Dispatch pending coords to the ThreadPool for background generation.
    /// Sorts by distance each tick so closest tiles load first.
    /// </summary>
    private void DispatchPending()
    {
        // Sort dispatch order: closest to focus first
        ChunkCoord focus = _focus != null ? WorldToChunk(_focus.position) : default;
        _dispatchOrder.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.X - focus.X) + Mathf.Abs(a.Z - focus.Z);
            int db = Mathf.Abs(b.X - focus.X) + Mathf.Abs(b.Z - focus.Z);
            return da.CompareTo(db);
        });

        // Dispatch up to MaxInFlight, closest first
        for (int i = 0; i < _dispatchOrder.Count && _inFlight.Count < MaxInFlight; i++)
        {
            ChunkCoord coord = _dispatchOrder[i];

            // Skip if already loaded or in-flight
            if (_loadedObjects.ContainsKey(coord))
                continue;
            if (_inFlight.ContainsKey(coord))
                continue;

            _inFlight.TryAdd(coord, 0);
            long seed = Seed;
            ThreadPool.QueueUserWorkItem(_ => BackgroundGenerate(coord, seed));
        }

        // Clean up loaded coords from dispatch list
        _dispatchOrder.RemoveAll(c => _loadedObjects.ContainsKey(c) || !_pendingSet.Contains(c));
    }

    /// <summary>
    /// Runs on a ThreadPool thread. Generates ChunkData + mesh arrays,
    /// then enqueues the result for main-thread finalization.
    /// </summary>
    private void BackgroundGenerate(ChunkCoord coord, long seed)
    {
        try
        {
            ChunkData data;

            // 1) Try disk cache (thread-safe: independent FileStream per file)
            if (!ChunkSaveManager.TryLoad(seed, coord.X, coord.Z, out data))
            {
                // 2) Generate fresh from noise
                data = new ChunkData(coord.X, coord.Z, seed);
                data.Heights[0] = TerrainNoiseGenerator.GetHeight(seed, coord.X * ChunkData.Size, (coord.Z + 1) * ChunkData.Size);
                data.Heights[1] = TerrainNoiseGenerator.GetHeight(seed, (coord.X + 1) * ChunkData.Size, (coord.Z + 1) * ChunkData.Size);
                data.Heights[2] = TerrainNoiseGenerator.GetHeight(seed, (coord.X + 1) * ChunkData.Size, coord.Z * ChunkData.Size);
                data.Heights[3] = TerrainNoiseGenerator.GetHeight(seed, coord.X * ChunkData.Size, coord.Z * ChunkData.Size);
                data.Version = 1;
            }

            // 3) Build mesh arrays (pure C#, no Unity API)
            ChunkMeshData md = ChunkMeshGenerator.BuildMeshData(data, TerrainNoiseGenerator.DefaultLayers);
            _readyQueue.Enqueue(md);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WorldStreamer] Background generation failed for {coord}: {ex.Message}");
            byte _;
            _inFlight.TryRemove(coord, out _);
        }
    }

    /// <summary>
    /// Dequeue completed mesh data and finalize on the main thread:
    /// create GO, assign mesh, spawn props.
    /// </summary>
    private void FinalizeReadyChunks()
    {
        int finalized = 0;
        ChunkMeshData md;
        while (finalized < ChunksPerFrame && _readyQueue.TryDequeue(out md))
        {
            ChunkCoord coord = md.Coord;
            byte _;
            _inFlight.TryRemove(coord, out _);

            // Skip if the chunk was unloaded while being generated
            if (_loadedObjects.ContainsKey(coord))
                continue;

            // Store data
            ChunkData data = md.Data;
            _loadedData[coord] = data;

            // Create GO + apply mesh on main thread
            ChunkObject obj = CreateOrPool(coord);
            obj.ApplyMeshData(md, GroundMaterial, buildCollider: true);
            obj.SpawnProps(Seed);
            _loadedObjects[coord] = obj;

            finalized++;
        }
    }

    private ChunkObject CreateOrPool(ChunkCoord coord)
    {
        GameObject go = new GameObject($"Tile_{coord.X}_{coord.Z}");
        go.isStatic = false;
        go.transform.SetParent(null);
        go.transform.position = new Vector3(coord.X * ChunkData.Size, 0f, coord.Z * ChunkData.Size);
        var obj = go.AddComponent<ChunkObject>();
        obj.Init(coord);
        return obj;
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
