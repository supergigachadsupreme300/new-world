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
    /// <summary>Double-sided (Cull Off) sibling of <see cref="GroundMaterial"/> used ONLY by far-shell
    /// cells (1ei): renders the decimated far terrain from above regardless of mesh winding/culling
    /// artifacts that once hid it from the upper face. Real chunks keep GroundMaterial (Cull Back).
    /// Falls back to GroundMaterial if never assigned.</summary>
    public Material FarGroundMaterial;

    [Header("Render Distance")]
    public RenderDistanceController RenderDistance;

    [Header("Props")]
    [Tooltip("Prop-ring floor: trees/rocks stream on every chunk the REAL chunk stream holds (rings 0..near+1), and this value may push the prop stream wider still (Chebyshev ring, 1di). Since 1en the floor is enforced ({near+1}) so the prop range never trails the chunk range again — a serialized 4 no longer silently limits props to 120 m while terrain renders to 300 m. The terrain mesh + collider stay loaded for the whole ring; only the prop GameObjects follow this ring, so the distant radius-N ring never holds the ~33k prop GOs / ~450k BoxColliders of the old full-stream.")]
    public int PropRingRadius = 9;

    [Header("Colliders")]
    [Tooltip("Collider-on-demand ring (1dq/1eh): terrain MeshColliders exist only on chunks within this many chunks of the focus (plus any chunk under an active spell projectile). Everything further still renders its full mesh but has no physics — the draw stays identical while the collider cooks / 7k-tri broadphase bodies drop ~92% at the default radius. 1eh: 8 -> 7 (289 -> 225 bodies swept by every Move) — still below NearRingRadius so every collider stays on a real chunk.")]
    public int ColliderRingRadius = 7;

    /// <summary>Chunks around each magic collider request that also keep a collider (1dq).</summary>
    public const int ColliderRequestExpand = 1;

    [Header("Far Shell")]
    [Tooltip("Size of the REAL chunk ring around the focus (1ef). Within this many chunks terrain streams as full-fidelity ChunkObjects — deformable, collidable, prop-bearing, LOD'd. From this ring out to the render radius the far shell (WorldStreamer.FarShell.cs) covers the ground with coarse background-generated sector meshes. Keep this >= ColliderRingRadius so every collider sits on a real chunk; the real stream additionally keeps one hysteresis ring (near+1) loaded.")]
    public int NearRingRadius = 9;

    [Header("Voxel (experiment 1et)")]
    [Tooltip("Render terrain as a 1-metre stepped voxel mesh (experimental, OFF by default). When on, the same chunk grid / pooling / budgets / deformation API / save files are kept, but every chunk mesh renders as flat column tops + terrace walls (VoxelChunkData + VoxelMesher) and saves use the v2 column-run format (legacy height-field saves migrate on read; smooth chunks are never written from voxel). LOD children and the far shell intentionally stay smooth in the experiment. Flip BEFORE the world streams (or via the test ground's QA toggle) — mid-run flips produce mixed terrain until the stream reloads.")]
    public bool VoxelTerrainEnabled = false;

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

    // Cached distance comparer for the pending sort (1ea): the closure capture allocated a fresh
    // delegate every poll; the focus is fed through a field instead so Sort is allocation-free.
    // Bound once in the constructor — a field initializer cannot reference the instance method
    // (CS0236), not even through a lambda.
    private TerrainChunkCoord _dispatchFocus;
    private readonly System.Comparison<TerrainChunkCoord> _dispatchSort;

    public WorldStreamer()
    {
        _dispatchSort = CompareDispatchDistance;
    }

    /// <summary>Manhattan distance to <see cref="_dispatchFocus"/>, feeding the cached sort comparer.</summary>
    private int CompareDispatchDistance(TerrainChunkCoord a, TerrainChunkCoord b)
    {
        int da = Mathf.Abs(a.X - _dispatchFocus.X) + Mathf.Abs(a.Z - _dispatchFocus.Z);
        int db = Mathf.Abs(b.X - _dispatchFocus.X) + Mathf.Abs(b.Z - _dispatchFocus.Z);
        return da.CompareTo(db);
    }

    // --- Hierarchy container (Terrain > Chunks > Chunk_X_Z) ---
    private Transform _terrainRoot;
    private Transform _chunksRoot;

    private Transform _focus;
    private float _timer;
    private const float PollInterval = 0.05f;

    // Idle-poll gate (1ee): the streaming pipeline re-runs every poll ONLY when something actually
    // changed — the focus crossed into a new chunk centre, terrain data was marked dirty, or a
    // chunk is still queued/in-flight/ready-to-finalize. An idle, fully-streamed player pays zero.
    // Sentinel (1ef): never equal to chunk 0,0 so the very first poll always runs the far-shell
    // pass even if the player spawns at the world origin.
    private TerrainChunkCoord _lastStreamCentre = new TerrainChunkCoord(int.MinValue, int.MinValue);
    private bool _worldDirty = true;

    // Collider reconcile (1ea): the full-map collider walk runs only when the ring box moved, a
    // collider request changed, or the loaded-chunk set changed; a per-poll cook budget caps PhysX
    // mesh cooking so a ring crossing never bursts a frame. An idle player pays zero for this poll.
    private int _colliderLastX = int.MinValue;
    private int _colliderLastZ = int.MinValue;
    private int _colliderLastRequestVersion = -1;
    private bool _collidersDirty = true;
    /// <summary>Max chunk MeshColliders enabled (cooked) per poll. 1ei: 4 -> 2 — a synchronous PhysX
    /// cook can cost 1-3 ms on the main thread, so ring-crossing cooks now spread over an extra poll
    /// (the same _collidersDirty resume already covers a larger ring change).</summary>
    private const int MaxColliderCooksPerPoll = 2;

    // Shared stream budget (1es): ONE wall-clock ceiling across the load-bearing streaming stages in
    // a poll (real-chunk finalize, the far-shell pass, prop spawning). Every stage measures its own
    // elapsed wall time and charges it to the shared pool (SpendStreamBudget); when the pool runs
    // dry the remaining DEFERRABLE stages hold until the next poll, so a poll can never legally sum
    // 6 ms chunks + 2.5 ms far + 3 ms props on top of a frame. The scaled floor keeps at least one
    // unit of work flowing every poll, so the stream always makes progress (same guarantee the old
    // per-stage caps gave). Visibility-critical passes (far-shell shadow sync, the collider walk,
    // the capped unload sweep) are NOT gated at entry — they run every live poll but keep their own
    // hard caps.
    private const float StreamBudgetMs = 4f;
    private float _streamBudgetRemaining;
    private bool _streamCapped;

    /// <summary>Charge <paramref name="ms"/> of main-thread wall time against the shared stream
    /// budget, flagging <see cref="_streamCapped"/> once the pool is dry (1es).</summary>
    private void SpendStreamBudget(float ms)
    {
        _streamBudgetRemaining -= ms;
        if (_streamBudgetRemaining <= 0f)
            _streamCapped = true;
    }

    // Chunks containing at least one modified tile (locally edited or loaded from a save). O(1)
    // membership replaces the old per-chunk 900-tile scans in the load-reconcile paths (1ea).
    private readonly HashSet<TerrainChunkCoord> _modifiedChunks = new HashSet<TerrainChunkCoord>();

    /// <summary>Flags that the loaded-chunk set changed so the next poll recomputes colliders.</summary>
    private void NoteChunkSetChanged()
    {
        _collidersDirty = true;
    }

    /// <summary>Plausible terrain-height band (5-octave noise max ≈ ±82.6 m since <c>1eo</c> + ≤ ~4.4 m
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
        _worldDirty = true; // a new focus object re-arms the next poll
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

        int view = RenderDistance != null ? RenderDistance.Radius : 3;
        int near = Mathf.Min(Mathf.Max(NearRingRadius, 0), view);
        TerrainChunkCoord centre = TerrainChunkCoord.FromWorld(_focus.position);

        // 1ee idle gate: skip the whole pipeline while nothing moved and nothing is queued. The
        // far-shell queues (1ef) participate — an initial far fill or a shrinking shell keeps the
        // poll alive until it finishes.
        bool working = _chunkDispatchOrder.Count > 0 || _chunksInFlight.Count > 0 || !_readyChunks.IsEmpty
            || _farInFlight.Count > 0 || !_farReady.IsEmpty || _farPending.Count > 0 || _farUnloadBacklog
            || _chunkUnloadBacklog;
        if (centre == _lastStreamCentre && !_worldDirty && !working)
            return;
        _lastStreamCentre = centre;
        _worldDirty = false;

        // (1es) reset the pooled stream budget for this poll. The adaptive scale keeps a fast frame
        // spending the full 4 ms and a hitched frame shrinking to its floor (0.35x) — never zero, so
        // the stream still progresses on the worst frame.
        _streamBudgetRemaining = Mathf.Max(1f, AdaptiveBudgetMs(StreamBudgetMs));
        _streamCapped = false;

        StreamAround(centre, near);
        DispatchPending();
        FinalizeChunks();
        FarShellTick(centre, view, near);
        ReconcileCollidersIfChanged(centre);
        SyncPropRing(centre, near);
        StepChunkProps();
    }

    /// <summary>
    /// Collider-on-demand (1dq + 1ea): keeps the MeshCollider only on chunks inside the
    /// <see cref="ColliderRingRadius"/> ring around the focus and on chunks under active magic
    /// (spell projectile flight paths — <see cref="ColliderRequestRegistry"/>). The far radius-N
    /// world still renders its full meshes; only the physics load (the per-chunk collider cook and
    /// the ~7k-tri broadphase bodies behind every raycast/overlap) is gated.
    /// Since 1ea the full-map walk runs ONLY when something that affects the ring actually changed
    /// (focus crossed a chunk boundary, a collider request was added/removed, or a chunk was
    /// finalized/unloaded) — an idle player pays nothing. A per-poll cook budget additionally
    /// spreads a ring crossing so PhysX meshes cook gradually instead of bursting one frame.
    /// </summary>
    private void ReconcileCollidersIfChanged(TerrainChunkCoord centre)
    {
        int reqVersion = ColliderRequestRegistry.Version;
        bool ringMoved = centre.X != _colliderLastX || centre.Z != _colliderLastZ;
        if (!_collidersDirty && !ringMoved && reqVersion == _colliderLastRequestVersion)
            return;

        _colliderLastX = centre.X;
        _colliderLastZ = centre.Z;
        _colliderLastRequestVersion = reqVersion;
        _collidersDirty = false;

        int cooked = 0;
        foreach (KeyValuePair<TerrainChunkCoord, ChunkObject> kv in _loadedChunks)
        {
            bool want = ColliderRingRadius > 0
                && Mathf.Abs(kv.Key.X - centre.X) <= ColliderRingRadius
                && Mathf.Abs(kv.Key.Z - centre.Z) <= ColliderRingRadius;
            if (!want && ColliderRequestRegistry.HasNear(kv.Key, ColliderRequestExpand))
                want = true;
            if (kv.Value.HasCollider == want)
                continue;

            if (want)
            {
                if (cooked >= MaxColliderCooksPerPoll)
                {
                    // Defer to the next poll; keep walking so disables still apply this tick.
                    _collidersDirty = true;
                    continue;
                }
                cooked++;
                kv.Value.SetColliderActive(true);
            }
            else
            {
                kv.Value.SetColliderActive(false);
            }
        }
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
        // (1es) drain every queued save on the main thread before the app tears down — the
        // background writer may still hold unflushed chunks.
        ChunkSaveManager.FlushPendingSaves();
    }
}