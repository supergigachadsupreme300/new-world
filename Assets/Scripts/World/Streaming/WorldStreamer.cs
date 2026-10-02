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

    [Tooltip("Master switch for the streaming/render loop (the scene's live/legacy stream).")]
    public bool StreamInUpdate = true;

    [Tooltip("1gd/1xd: decouple the streaming/render loop from the gameplay Update. The map runs on its OWN coroutine clock (StreamHz) instead of the gameplay frame, and after a HEAVY poll (one that exhausted the shared stream budget) it yields one cool-down frame before the next slice — so a heavy map-render beat can never double-load a gameplay frame. Light busy polls keep the full StreamHz cadence. Loading speed is unchanged: the real-chunk/far rings still chase the player at every tick; only the main-thread render work is gated by the per-stage budgets and slices.")]
    public bool DecoupleRenderFromGameplay = true;

    [Tooltip("Renderer clock (1gd): polls per second of the decoupled streaming loop. 20 = the legacy 20 Hz beat.")]
    public int StreamHz = 20;

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
    [Tooltip("Collider-on-demand ring (1dq/1eh): terrain MeshColliders exist only on chunks within this many chunks of the focus (plus any chunk under an active spell projectile). Everything further still renders its full mesh but has no physics — the draw stays identical while the collider cooks / broadphase body count is gated to what the gameplay uses. 1eh: 8 -> 7 (289 -> 225 bodies swept by every Move) — still below NearRingRadius so every collider stays on a real chunk. 1ex raised each ring collider from 450 to 1800 tris (2 m -> 1 m, so the player stops walking through visible craters): expect ~405k collider triangles at full ring, and measure it on the F2 frame-budget lane.")]
    public int ColliderRingRadius = 7;

    /// <summary>Chunks around each magic collider request that also keep a collider (1dq).</summary>
    public const int ColliderRequestExpand = 1;

    [Header("Far Shell")]
    [Tooltip("Size of the REAL chunk ring around the focus (1ef). Within this many chunks terrain streams as full-fidelity ChunkObjects — deformable, collidable, prop-bearing, each drawn as one full-detail mesh (1f6 removed the detail LOD bands; the root mesh is now the chunk's only surface at every distance). From this ring out to the render radius the far shell (WorldStreamer.FarShell.cs) covers the ground with coarse background-generated sector meshes. Keep this >= ColliderRingRadius so every collider sits on a real chunk; the real stream additionally keeps one hysteresis ring (near+1) loaded.")]
    public int NearRingRadius = 9;

    [Tooltip("Dormant keep-ring depth (1gc): real chunks passing the near+1 hysteresis ring are no longer destroyed outright — they are hidden (visuals off; collider already off by the collider ring) and retained as dormant out to ring near+1+DormantRingDepth, while the coarse far shell covers the view exactly as it did when the chunk was destroyed. Re-entering the active ring wakes the SAME object (no background regeneration / mesh upload). Depth 2 retains at worst ~184 chunks (~tens of MB of cached mesh+data) so the player can bounce across the close-range edge without destroy/regenerate churn. Beyond the dormant band chunks unload as before.")]
    public int DormantRingDepth = 2;

    [Header("Voxel (experiment 1et/1eu)")]
    [Tooltip("Render terrain as a 1-metre stepped voxel mesh (experimental, OPT-IN). Since 1ev the smooth heightfield is the default again — the stepped column look read as too Minecraft-like/blocky in play-test, so the voxel model was un-defaulted but kept for experiments. Flip ON via this field or the test ground's QA toggle `EnableVoxelTerrain` to preview it. When on, the same chunk grid / pooling / budgets / deformation API / save files are kept, but every chunk mesh renders as flat column tops + terrace walls (VoxelChunkData + VoxelMesher), saves use the v3 multi-run column format (legacy v1 height-field / v2 single-run saves migrate on read; smooth chunks are never written from voxel), the far shell renders stepped voxel variants, and the sculpt API (SculptVoxelCave/Raise) + directed dig carve actual column runs. Flip BEFORE the world streams — mid-run flips produce mixed terrain until the stream reloads.")]
    public bool VoxelTerrainEnabled = false;

    [Header("Smooth Terrain Refinement (1ew)")]
    [Tooltip("Adaptive stretch-split of the smooth heightfield: a 1x1 tile whose 4 corner heights differ by MORE than this many metres renders as a 2x2 sub-quad grid (bilinear interior heights) instead of one hugely stretched quad. The face count of a steep slope splits into several smaller faces so the corner-grab editor lands a fine handle on a real corner again — the world stays a smooth heightfield at every zoom, never stepped like the voxel mode. 0 disables refinement entirely (full 1m quads everywhere). Derived from the corners (never stored), interior-of-chunk only (the 1 m border ring stays one quad per tile so cross-chunk shared corners stay untouched).")]
    public float RefineThreshold = ChunkMeshGenerator.DefaultRefineThreshold;

    [Header("Low-Poly Facets (1hi — OFF since 1ia)")]
    [Tooltip("QA/render (1hi): LOW-POLY FACET look. When ON the far shell emits FLAT per-quad normals (crisp facets instead of the smooth sample-grid haze — triangles unchanged; vertices 4x but far cells upload once per cell lifetime, never per frame) AND the 1ew adaptive refinement passes a 0 threshold so steep near slopes keep big flat quads instead of splitting into 2x2 sub-quads. Pure render/geometry-read change (1hi): saves, the 1m tile grid, props, draw calls and the budgeted collider pipeline are untouched. Flip BEFORE the far shell builds (like the voxel toggle) for a clean read.\n\n1ia: DEFAULT IS NOW OFF. The user asked for the terrain algorithm to be reverted to its pre-1hi state, and OFF is exactly that state on the render path — the code is untouched, only the default flipped:\n  - EffectiveRefineThreshold returns RefineThreshold again, so the 1ew adaptive stretch-split is back (steep slopes subdivide instead of staying one huge quad);\n  - EffectiveLowPolyStep is 0, so the near root is the full 1 m per-tile surface WITH side walls and PatchRegion takes its per-tile skim instead of the lattice re-emit;\n  - the far shell builds with flatFacets=false, i.e. the smooth central-difference haze with its Cull Off material, as before 1hi.\nThe far shell's sampling step is a SEPARATE constant (FarSectorStep, back to 3 m since 1ia) and is NOT gated by this flag. Turn this back ON to A/B the facet look; the code is unchanged either way.")]
    public bool LowPolyFacets = false;

    [Tooltip("Low-poly facet size for the REAL near chunks (1hi.1): every LowPolyStep-th node of the 31x31 world-corner lattice becomes one flat facet (must divide the 30 m chunk side). Only read while LowPolyFacets is ON, which it is not by default since 1ia — see that field's tooltip. 1ia put the default back to the pre-1hx value of 3 (and FarSectorStep back to 3 with it), so re-enabling the facet look restores the world as it read at 1hi.1/1hi.2 rather than the coarser 6 m facets of 1hx. History: 1hx moved 3 -> 6 because at 3 m the sampled facets on this 5-octave field (base octave amplitude 55 m at frequency 0.0012) came out near-coplanar, so adjacent flat normals barely differed and the world still read as smooth haze; facet shading contrast scales with curvature x span. The far shell MUST use the same value (FarSectorStep), which also has to divide the 90 m and 180 m cell spans so every far grid row still lands on a chunk boundary. The 1 m corner grid remains the source of truth for saves, edits and prop heights; the collider rides the same step so you stand exactly on the visual — coarser facets mean lumpier footing and more prop float/sink.")]
    public int LowPolyStep = 3;

    /// <summary>Effective refinement threshold routed through every build path (1hi): the low-poly
    /// look disables the 1ew adaptive stretch-split (0 = full 1m quads everywhere), so far-band
    /// facets and near-band steep slopes read as the same chunky language.</summary>
    private float EffectiveRefineThreshold => LowPolyFacets ? 0f : RefineThreshold;

    /// <summary>Effective low-poly facet step routed through every BuildMergedMeshData call site
    /// (1hi.1): 0 = full-resolution 1 m root (the smooth look), &gt;0 = the near ring's root renders
    /// as coarse flat facets while the far shell already matches. Captured on the main thread and
    /// snapshotted into worker jobs like the voxel flag. A step that does not divide the 30 m chunk
    /// side would leave the last facet short of the chunk border (a visible seam), so such values
    /// fall back to 0 (full-res) rather than emitting a broken grid. (1hx) The far shell runs its own
    /// copy of this rule in <c>FarSectorStep</c> (WorldStreamer.FarShell.cs); the two must be changed
    /// together or the far band renders at a different density than the near ring.</summary>
    private int EffectiveLowPolyStep => LowPolyFacets && LowPolyStep > 0
        && TerrainChunkCoord.ChunkSize % LowPolyStep == 0
        ? LowPolyStep : 0;

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

    // --- Dormant keep-ring (1gc): chunks within the retained band between the hysteresis ring and
    // the dormant band (ring keep .. keep+DormantRingDepth). They are REMOVED from _loadedChunks so
    // every "is the real chunk covering here?" check (far-shell active-shadow, prop ring, collider
    // reconcile, distance-cull registration) treats them as absent and the coarse far cell renders over
    // them — but their tile data / pooled mesh / GameObject / VoxelStore are retained, so a
    // wake back into the active ring is an instant re-show instead of destroy + regenerate churn.
    private readonly Dictionary<TerrainChunkCoord, ChunkObject> _dormantChunks = new Dictionary<TerrainChunkCoord, ChunkObject>();

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

    // --- Per-stage poll ms instrument (1gf, QA readout) ---
    // One stream loop poll's main-thread wall time split by stage, so the bench HUD can show WHICH
    // stage a long-sprint crossing actually eats instead of guessing. Value-type snapshot — allocation
    // free every poll. Read-only for consumers (the only writer is StreamOnce/ResetPollStagePeaks).
    private PollStageStats _pollStats;
    private PollStageStats _peakPollStats;
    private int _heavyPollsSinceRead;

    /// <summary>The last live poll's stage split (ms), or zero before the first poll (1gf).</summary>
    public PollStageStats LastPollStats => _pollStats;

    /// <summary>Running per-stage maxima across polls since the last <see cref="ResetPollStagePeaks"/>
    /// (the readout refresh resets them each window, so the HUD always shows the worst poll in view).</summary>
    public PollStageStats PeakPollStats => _peakPollStats;

    /// <summary>Heavy polls (budget-exhausting, cool-down yielding) since the last reset (1gf).</summary>
    public int HeavyPollsSinceLastRead => _heavyPollsSinceRead;

    /// <summary>Clears the rolling peak stage split + heavy-poll counter (called by the bench HUD each
    /// refresh window so the peaks cover only the visible sprint).</summary>
    public void ResetPollStagePeaks()
    {
        _peakPollStats = PollStageStats.Zero;
        _heavyPollsSinceRead = 0;
    }

    /// <summary>One poll's main-thread wall time (ms) split by stream stage (1gf QA readout).</summary>
    public readonly struct PollStageStats
    {
        /// <summary>StreamAround: wake/demote (far-cover gate)/deep-unload + ring-walk enqueue.</summary>
        public readonly float StreamAroundMs;
        /// <summary>DispatchPending + DispatchRebuilds (background job dispatch only).</summary>
        public readonly float DispatchMs;
        /// <summary>FinalizeChunks: create chunk GameObjects + merged-mesh uploads.</summary>
        public readonly float FinalizeMs;
        /// <summary>DrainRebuildResults: apply async seam-rebuild meshes.</summary>
        public readonly float RebuildDrainMs;
        /// <summary>FarShellTick scan phases: shadow sync, removal scan, ring walk, pre-warm, dispatch.</summary>
        public readonly float FarScanMs;
        /// <summary>FarShellTick finalize: far sector GameObject creation + mesh uploads.</summary>
        public readonly float FarFinalizeMs;
        /// <summary>ReconcileCollidersIfChanged: collider ring walk + PhysX cooks.</summary>
        public readonly float CollidersMs;
        /// <summary>SyncPropRing: prop ring begin/release toggles.</summary>
        public readonly float PropSyncMs;
        /// <summary>StepChunkProps: budgeted prop spawning.</summary>
        public readonly float PropsMs;

        /// <summary>Whole far-shell pass (scan + finalize).</summary>
        public float FarMs => FarScanMs + FarFinalizeMs;

        /// <summary>Whole poll (all stages summed).</summary>
        public float TotalMs =>
            StreamAroundMs + DispatchMs + FinalizeMs + RebuildDrainMs + FarMs
            + CollidersMs + PropSyncMs + PropsMs;

        public static readonly PollStageStats Zero = new PollStageStats(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

        public PollStageStats(float streamAroundMs, float dispatchMs, float finalizeMs, float rebuildDrainMs,
            float farScanMs, float farFinalizeMs, float collidersMs, float propSyncMs, float propsMs)
        {
            StreamAroundMs = streamAroundMs;
            DispatchMs = dispatchMs;
            FinalizeMs = finalizeMs;
            RebuildDrainMs = rebuildDrainMs;
            FarScanMs = farScanMs;
            FarFinalizeMs = farFinalizeMs;
            CollidersMs = collidersMs;
            PropSyncMs = propSyncMs;
            PropsMs = propsMs;
        }

        public static PollStageStats Max(PollStageStats a, PollStageStats b)
        {
            return new PollStageStats(
                Mathf.Max(a.StreamAroundMs, b.StreamAroundMs),
                Mathf.Max(a.DispatchMs, b.DispatchMs),
                Mathf.Max(a.FinalizeMs, b.FinalizeMs),
                Mathf.Max(a.RebuildDrainMs, b.RebuildDrainMs),
                Mathf.Max(a.FarScanMs, b.FarScanMs),
                Mathf.Max(a.FarFinalizeMs, b.FarFinalizeMs),
                Mathf.Max(a.CollidersMs, b.CollidersMs),
                Mathf.Max(a.PropSyncMs, b.PropSyncMs),
                Mathf.Max(a.PropsMs, b.PropsMs));
        }
    }

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

    /// <summary>Dormant (hidden-but-retained) chunk objects, 1gc. Not in <see cref="LoadedChunks"/>.</summary>
    public IReadOnlyDictionary<TerrainChunkCoord, ChunkObject> DormantChunks => _dormantChunks;
    public int DormantChunkCount => _dormantChunks.Count;

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

    // --- Main loop (1gd: speed-decoupled renderer clock) ---

    // The streaming/render loop runs on ITS OWN coroutine clock instead of the gameplay Update.
    // Before 1gd every poll happened inside Update(): when the player moved fast (vehicle/jet), the
    // 20 Hz poll fired on a random gameplay frame backed by a multi-ms budget (chunk finalize +
    // far shell + props + collider cooks), and crossing into edited terrain stacked synchronous
    // 900-tile full rebuilds — each one re-emitted and re-uploaded a whole merged chunk on the main
    // thread, PER POLL, at speed. Player speed therefore directly dragged the gameplay frame.
    // 1gd decouples the two:
    //   * The loop lives in a coroutine ticked at StreamHz (renderer's own clock); it checks
    //     StreamInUpdate itself, so the scene toggle still works at runtime.
    //   * After a heavy poll (one that exhausted the shared stream budget) the loop yields ONE
    //     cool-down frame before the next slice, so a heavy map-render beat can never double-load the
    //     frame right next to it. Light busy polls (dispatch-only, small finalizes) keep the full
    //     StreamHz beat so the ring fill rate at speed is unchanged (1xd).
    //   * Heavy seam rebuilds (edits re-emitting merged chunks when the stream passes) moved to
    //     ThreadPool workers (see RequestChunkRebuild/DrainRebuildResults). Loading speed is
    //     unchanged: the real-chunk/far rings still chase the player every tick; only the main-
    //     thread render work is gated by the per-stage budgets, slices and finalize cap.
    private Coroutine _streamLoop;

    private void OnEnable()
    {
        if (_streamLoop == null)
            _streamLoop = StartCoroutine(StreamLoop());
    }

    private void OnDisable()
    {
        if (_streamLoop != null)
        {
            StopCoroutine(_streamLoop);
            _streamLoop = null;
        }
    }

    private System.Collections.IEnumerator StreamLoop()
    {
        float delay = StreamHz > 0 ? 1f / StreamHz : PollInterval;
        var wait = new WaitForSecondsRealtime(delay);
        while (true)
        {
            yield return wait;
            if (!StreamInUpdate || _focus == null)
                continue;
            if (StreamOnce() && DecoupleRenderFromGameplay)
                yield return null; // cool-down frame: never stack a HEAVY poll onto the next gameplay frame
        }
    }

    /// <summary>One streaming/render poll on the decoupled clock. Returns true when the poll was a HEAVY
    /// beat (it exhausted the shared stream budget) so <see cref="StreamLoop"/> spreads heavy render
    /// work across its own frames instead of stacking it onto adjacent gameplay frames.</summary>
    private bool StreamOnce()
    {
        int view = RenderDistance != null ? RenderDistance.Radius : 3;
        int near = Mathf.Min(Mathf.Max(NearRingRadius, 0), view);
        TerrainChunkCoord centre = TerrainChunkCoord.FromWorld(_focus.position);

        // 1ee idle gate: skip the whole pipeline while nothing moved and nothing is queued. The
        // far-shell queues (1ef) and the 1gd seam-rebuild queues participate — an initial far fill,
        // a shrinking shell or a trailing rebuild keeps the poll alive until it finishes.
        bool working = _chunkDispatchOrder.Count > 0 || _chunksInFlight.Count > 0 || !_readyChunks.IsEmpty
            || _farInFlight.Count > 0 || !_farReady.IsEmpty || _farPending.Count > 0 || _farUnloadBacklog
            || _chunkUnloadBacklog || _demoteBacklog
            || _rebuildPending.Count > 0 || _rebuildsInFlight.Count > 0 || !_readyRebuilds.IsEmpty;
        if (centre == _lastStreamCentre && !_worldDirty && !working)
            return false;
        _lastStreamCentre = centre;
        _worldDirty = false;

        // (1es) reset the pooled stream budget for this poll. The adaptive scale keeps a fast frame
        // spending the full 4 ms and a hitched frame shrinking to its floor (0.35x) — never zero, so
        // the stream still progresses on the worst frame.
        _streamBudgetRemaining = Mathf.Max(1f, AdaptiveBudgetMs(StreamBudgetMs));
        _streamCapped = false;

        // (1gf) per-stage wall-clock boundaries for the poll ms readout. Time.realtimeSinceStartup is
        // monotonic and cheap to read per stage; the split records where a sprint crossing actually
        // spends its main-thread time without touching any stage's budget behavior.
        float t0 = Time.realtimeSinceStartup;
        StreamAround(centre, near);
        float t1 = Time.realtimeSinceStartup;
        DispatchPending();
        DispatchRebuilds();
        float t3 = Time.realtimeSinceStartup;
        FinalizeChunks();
        float t4 = Time.realtimeSinceStartup;
        DrainRebuildResults();
        float t5 = Time.realtimeSinceStartup;
        FarShellTick(centre, view, near, out float farScanMs, out float farFinalMs);
        float t6 = Time.realtimeSinceStartup;
        ReconcileCollidersIfChanged(centre);
        float t7 = Time.realtimeSinceStartup;
        SyncPropRing(centre, near);
        float t8 = Time.realtimeSinceStartup;
        StepChunkProps();
        float t9 = Time.realtimeSinceStartup;

        // (1gf) record this poll's split + rolling peaks for the bench overlay (read-only QA readout).
        _pollStats = new PollStageStats(
            t1 - t0,
            t3 - t1,
            t4 - t3,
            t5 - t4,
            farScanMs,
            farFinalMs,
            t7 - t6,
            t8 - t7,
            t9 - t8);
        _peakPollStats = PollStageStats.Max(_peakPollStats, _pollStats);
        if (_streamCapped)
            _heavyPollsSinceRead++;

        // (1xd) Return whether this poll was a HEAVY beat (it exhausted the shared stream budget) so
        // StreamLoop's cool-down frame only follows polls that actually stacked main-thread render
        // work. Busy-but-light polls (dispatch-only, small finalizes, gate re-checks) keep the full
        // StreamHz cadence — restoring the loading throughput the "cool-down after every busy poll"
        // cut at speed (rings filled slower, so the player kept closing on unready ground).
        return _streamCapped;
    }

    /// <summary>
    /// Collider-on-demand (1dq + 1ea): keeps the MeshCollider only on chunks inside the
    /// <see cref="ColliderRingRadius"/> ring around the focus and on chunks under active magic
    /// (spell projectile flight paths — <see cref="ColliderRequestRegistry"/>). The far radius-N
    /// world still renders its full meshes; only the physics load (the per-chunk collider cook and
    /// the broadphase bodies behind every raycast/overlap) is gated. Since 1ex each ring collider is
    /// 1800 tris rather than 450, so the <see cref="ColliderRingRadius"/>-7 Chebyshev square
    /// (15x15 = 225 chunks) carries ~405k collider triangles at full ring — this is the number to
    /// watch on the F2 frame-budget lane, not a claim that it is free.
    /// Since 1ea the full-map walk runs ONLY when something that affects the ring actually changed
    /// (focus crossed a chunk boundary, a collider request was added/removed, or a chunk was
    /// finalized/unloaded) — an idle player pays nothing. A per-poll cook budget additionally
    /// spreads a ring crossing so PhysX meshes cook gradually instead of bursting one frame.
    /// 1gg: chunks within <see cref="ReconcileCollidersIfChanged"/>'s must-collide ring (the centre
    /// cell + a 2-chunk floor around the focus) cook UNCONDITIONALLY (budget-exempt, bounded), and
    /// every poll advances the closest deferred cook anyway — so the ground the player is standing
    /// on / just stepping onto is never left collider-less by a budget-starved heavy sprint.
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
        // (1gg) "player-floor" guarantee: chunks within this many chunks (Chebyshev) of the focus are
        // COLLISION-CRITICAL — their collider must exist no matter how loaded the stream is, because
        // the falling player is standing on exactly these cells. Without the exemption, a heavy
        // sprint (every poll _streamCapped) defers a chunk's first cook for as long as the budget
        // stays dry, and the player then steps onto RENDERED but collider-less ground and falls
        // through the world. Exemption is bounded (MustCollideCap, above the ring-fill cap) and
        // charged to the shared stream budget afterwards, so correctness wins a frame slice but the
        // queue still shows up in the poll accounting.
        const int MustCollideRadius = 2;
        const int MustCollideCap = 6;
        // If budget/cap forces a defer, cook the single CLOSEST pending chunk anyway at the end, so
        // the approach fill (and the centre cell when the cap batch runs long) advances at least one
        // chunk every poll regardless of stream load.
        ChunkObject closestPending = null;
        int closestCheb = int.MaxValue;
        bool deferredAny = false;

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
                int cheb = Mathf.Max(Mathf.Abs(kv.Key.X - centre.X), Mathf.Abs(kv.Key.Z - centre.Z));
                if (ColliderRingRadius > 0 && cheb <= MustCollideRadius && cooked < MustCollideCap)
                {
                    // 1gg: under/just-ahead-of-the-player cells cook unconditionally (bounded).
                    float cookStart = Time.realtimeSinceStartup;
                    cooked++;
                    kv.Value.SetColliderActive(true);
                    // Charge the shared pool so the (already-scheduled) prop stage still sees the
                    // real frame spend; never gates further must-collide cooks this poll.
                    SpendStreamBudget((Time.realtimeSinceStartup - cookStart) * 1000f);
                    continue;
                }
                // (1xd) The per-poll cook cap spreads a ring crossing, and the stream budget gates
                // it further: once this poll's shared pool is dry (finalize/far work ate it), cooks
                // defer to the next poll instead of stacking unbudgeted PhysX time on top of a heavy
                // beat. _collidersDirty keeps the walk alive so the deferred cooks still land.
                if (_streamCapped || cooked >= MaxColliderCooksPerPoll)
                {
                    deferredAny = true;
                    if (cheb < closestCheb)
                    {
                        closestCheb = cheb;
                        closestPending = kv.Value;
                    }
                    continue;
                }
                cooked++;
                kv.Value.SetColliderActive(true);
            }
            else
            {
                // Disables are instant and cheap (sharedMesh = null, no cook) — the player already
                // left these cells, so they never need the exemption, only the re-enabled ring-fill.
                kv.Value.SetColliderActive(false);
            }
        }

        if (deferredAny)
        {
            // 1gg: always advance the approach fill at least one cell per poll — the centre cell
            // (cheb 0) wins the closest test even when the must-collide cap batch ran long, so the
            // player's own chunk can never stay collider-less across polls.
            if (closestPending != null)
                closestPending.SetColliderActive(true);
            _collidersDirty = true;
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