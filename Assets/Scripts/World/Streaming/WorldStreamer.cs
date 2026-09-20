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
public partial class WorldStreamer : MonoBehaviour
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
    public int ChunksPerFrame = 16;

    [Tooltip("Max terrain chunks being generated on background threads simultaneously.")]
    public int MaxInFlight = 24;

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
    private const float PollInterval = 0.05f;

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