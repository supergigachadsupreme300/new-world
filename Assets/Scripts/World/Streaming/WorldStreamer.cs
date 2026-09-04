using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The main orchestrator for the seamless open world.
///
/// Responsibilities:
///   - Holds the world seed exposed publicly for other systems (respawn, biome).
///   - Tracks the focus (player) world position each frame.
///   - Determines which chunks fall inside the render radius.
///   - Loads chunks that enter the radius (from disk first, then generated).
///   - Unloads chunks that leave the radius (and dirty ones are saved).
///   - Guarantees neighbour-aware generation so shared edges always line up.
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

    [Tooltip("Extra margin in chunk units to keep neighbours loaded during generation.")]
    public int NeighbourMargin = 1;

    private readonly Dictionary<ChunkCoord, ChunkData> _loadedData = new Dictionary<ChunkCoord, ChunkData>();
    private readonly Dictionary<ChunkCoord, ChunkObject> _loadedObjects = new Dictionary<ChunkCoord, ChunkObject>();
    private readonly HashSet<ChunkCoord> _dirty = new HashSet<ChunkCoord>();

    private Transform _focus;
    private float _timer;
    private const float PollInterval = 0.1f;

    /// <summary>All currently loaded chunk objects.</summary>
    public IReadOnlyDictionary<ChunkCoord, ChunkObject> Loaded => _loadedObjects;

    /// <summary>Returns the in-memory ChunkData for a loaded chunk, if present.</summary>
    public bool TryGetData(ChunkCoord coord, out ChunkData data)
    {
        return _loadedData.TryGetValue(coord, out data);
    }

    /// <summary>True if a chunk is currently loaded / object present.</summary>
    public bool IsLoaded(ChunkCoord coord) => _loadedObjects.ContainsKey(coord);

    /// <summary>Provide the focus transform (usually the player camera/root).</summary>
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
        StreamAround(WorldToChunk(_focus.position), radius);
    }

    /// <summary>Convert a world position to the chunk it lies in (floor).</summary>
    public static ChunkCoord WorldToChunk(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / ChunkData.Size);
        int z = Mathf.FloorToInt(pos.z / ChunkData.Size);
        return new ChunkCoord(x, z);
    }

    /// <summary>
    /// Load/unload chunks around a centre coord for the given radius.
    /// A cache is kept in memory; unloaded-but-retained data stays in _loadedData
    /// unless the pool decides to evict it. Dirty chunks are persisted on unload.
    /// </summary>
    public void StreamAround(ChunkCoord centre, int radius)
    {
        // ---- Unload chunks that fell outside the radius ----
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

        // ---- Load chunks entering the radius ----
        // Process outward from centre for a nicer pop-in experience.
        for (int r = 0; r <= radius; r++)
        {
            int startX = centre.X - r, endX = centre.X + r;
            int startZ = centre.Z - r, endZ = centre.Z + r;

            // top row
            for (int x = startX; x <= endX; x++)
                EnsureChunk(new ChunkCoord(x, endZ));
            // bottom row
            for (int x = startX; x <= endX; x++)
                EnsureChunk(new ChunkCoord(x, startZ));
            // left col (skip corners already handled)
            for (int z = startZ + 1; z < endZ; z++)
                EnsureChunk(new ChunkCoord(startX, z));
            // right col
            for (int z = startZ + 1; z < endZ; z++)
                EnsureChunk(new ChunkCoord(endX, z));
        }
    }

    /// <summary>Load a chunk if not already present (safe to call repeatedly).</summary>
    public void EnsureChunk(ChunkCoord coord)
    {
        if (_loadedObjects.ContainsKey(coord))
            return;

        // 1) Try the in-memory cache first.
        ChunkData data;
        if (!_loadedData.TryGetValue(coord, out data))
        {
            // 2) Try the disk cache.
            if (ChunkSaveManager.TryLoad(Seed, coord.X, coord.Z, out data))
            {
                // honour stored heights; seeds match file contents
            }
            else
            {
                // 3) Generate fresh from noise (first visit). Neighbouring chunk
                //    heights are NOT required because corners are world-derived.
                data = GenerateChunk(coord);
            }
            _loadedData[coord] = data.ShallowCopy();
        }

        // Pull an object from the pool / create one and apply the mesh.
        ChunkObject obj = CreateOrPool(coord);
        obj.Apply(data, TerrainNoiseGenerator.DefaultLayers, GroundMaterial, buildCollider: true);
        obj.SpawnProps(Seed);
        _loadedObjects[coord] = obj;
    }

    private ChunkData GenerateChunk(ChunkCoord coord)
    {
        ChunkData data = new ChunkData(coord.X, coord.Z, Seed);

        // Corner heights are pure world-space noise -> identical across neighbours.
        // Slot layout: 0=NW, 1=NE, 2=SE, 3=SW
        data.Heights[0] = TerrainNoiseGenerator.GetHeight(Seed, coord.X * ChunkData.Size, (coord.Z + 1) * ChunkData.Size);
        data.Heights[1] = TerrainNoiseGenerator.GetHeight(Seed, (coord.X + 1) * ChunkData.Size, (coord.Z + 1) * ChunkData.Size);
        data.Heights[2] = TerrainNoiseGenerator.GetHeight(Seed, (coord.X + 1) * ChunkData.Size, coord.Z * ChunkData.Size);
        data.Heights[3] = TerrainNoiseGenerator.GetHeight(Seed, coord.X * ChunkData.Size, coord.Z * ChunkData.Size);
        data.Version = 1;

        return data;
    }

    private ChunkObject CreateOrPool(ChunkCoord coord)
    {
        // Simple pooling: reuse a pooled object if available.
        GameObject go = new GameObject($"Tile_{coord.X}_{coord.Z}");
        go.isStatic = false;
        go.transform.SetParent(null);
        go.transform.position = new Vector3(coord.X * ChunkData.Size, 0f, coord.Z * ChunkData.Size);
        var obj = go.AddComponent<ChunkObject>();
        obj.Init(coord);
        return obj;
    }

    /// <summary>Save any modifications then unload an object.</summary>
    public void UnloadChunk(ChunkCoord coord)
    {
        ChunkObject obj;
        if (!_loadedObjects.TryGetValue(coord, out obj))
            return;

        // Persist dirty chunks before dropping the object.
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

    /// <summary>Mark a chunk dirty so it gets saved on unload (or immediately).</summary>
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
        // Flush all dirty chunks so nothing is lost on scene teardown.
        foreach (ChunkCoord coord in _dirty)
        {
            if (_loadedData.TryGetValue(coord, out ChunkData data))
                ChunkSaveManager.Save(Seed, coord.X, coord.Z, data);
        }
        _dirty.Clear();
    }
}
