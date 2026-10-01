using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Testing ground for the open world. Drop this ONE component on a GameObject and it builds an
/// INDEPENDENT floating platform and lays a deterministic, perfectly level test bench on top of it.
/// The world's procedural terrain is never edited in any way — no carve, no flatten, no chunk-save
/// writes, no prop clearing/suppression: the platform is a self-contained mesh + collider that
/// floats clear of the local ground, so the world keeps its own rolling terrain underneath.
/// Contents: the player tool/seed kit, a farming plot, livestock pens, an enemy arena, a building
/// row, an NPC row, and a crafted fast-travel POI hub. It composes existing public APIs only (no
/// rewrites of live contracts), so every Phase 4-8/9 system can be exercised from a single area.
///
/// Opt-in lanes via serialized toggles; spawn once (idempotent) on <see cref="AutoSpawnOnStart"/>.
/// </summary>
public sealed class NewWorldTestGround : MonoBehaviour
{
    [Header("Arena")]
    [Tooltip("Footprint of the independent floating test platform (X/Z world units).")]
    public float PlatformSize = 120f;
    [Tooltip("Centre of the platform in world space (X/Z). Its Y is set to the platform's top surface.")]
    public Vector3 PlatformCenter = new Vector3(0f, 50f, 0f);
    [Tooltip("Build the independent floating platform on Awake. The world terrain is never edited.")]
    public bool CreatePlatform = true;
    [Tooltip("How far the platform's top floats above the highest local terrain sample.")]
    public float PlatformClearance = 12f;
    [Tooltip("Thickness of the platform slab below its top surface.")]
    public float PlatformThickness = 0.6f;

    [Header("Spawning")]
    [Tooltip("Spawn the bench automatically on Awake.")]
    public bool AutoSpawnOnStart = true;
    [Tooltip("Automatically teleport the player onto the platform when the bench spawns. ON by default since 1dn — the player now spawns on the test ground. Disable it to start on the world's boot chunk near (0, terrain, -10) and walk to the platform.")]
    public bool AutoTeleportPlayerOnStart = true;

    [Header("Lanes")]
    [Tooltip("Lay the tool/food discovery kit along the platform's east edge as world pickups to grab with E.")]
    public bool EnableTools = true;
    public bool EnableFarming = true;
    public bool EnableLivestock = true;
    [Tooltip("Spawn the enemy arena down the platform's south side (20 roster types + dummies + optional boss). OFF by default since 1dl follow-up — the user is play-testing magic models now; the spawn code is fully kept for later (tick back on to test enemies).")]
    public bool EnableEnemies = false;
    public bool EnableBuildings = true;
    public bool EnableNpcs = true;
    [Tooltip("Build the three holy places (taoist shrine / church / pagoda) + worship NPCs (taoist / priest / monk) so the Faith system can be exercised.")]
    public bool EnableReligion = true;
    public bool EnablePoiHub = true;
    public bool IncludeBoss = false;

    [Header("Phase 10 - Weapons & Skills")]
    [Tooltip("Equip the starter weapon and make all 15 weapons available (cycle via Character Info > Equipment).")]
    public bool EnableWeapons = true;
    [Tooltip("Grant an unlimited skill-point budget and wire the skill profile + hotkey bindings (testing).")]
    public bool EnableSkills = true;
    [Tooltip("Equip a starter armor/accessory set into the 21-slot equipment system (testing).")]
    public bool EnableGear = true;
    [Tooltip("Wire the RaceChangeManager, unlock every race, and grant Ritual Stones for the Race tab (testing).")]
    public bool EnableRaces = true;
    [Tooltip("Place EVERY castable magic spell on the platform's middle band as a static, school-colored projectile-style model on a pedestal with an in-game label (mirrors the MagicTestMatrix roster, 1dk). Pure visuals for looking at/editing each spell's model — no collision, no interaction.")]
    public bool EnableMagicModels = false;
    [Tooltip("Cast Earth-shape terrain demos (Wall smooth ridge, Pillar, Crater smooth dent) onto the streamed terrain just off the platform. The Wall is cast twice to show repeat casts are CAPPED (smooth feathered deforms, no slab stacking — 1cj). Deforms REAL terrain — permanent chunk saves — so it is off by default and never touches the platform or legacy village.")]
    public bool EnableTerrainSlabDemo = false;
    [Tooltip("QA the layered strata (grass -> dirt -> stone): two craters excavated on the streamed terrain just off the platform by repeating the shared crater digs (each cast ratchets the floor a step deeper, like the shovel/pickaxe path). One pit reaches the dirt band, the other digs through into stone. Deforms REAL terrain — permanent chunk saves — so it is off by default and never touches the platform or legacy village.")]
    public bool EnableDigLayersDemo = false;
    [Tooltip("QA: delete this world's terrain chunk saves and regenerate every loaded chunk from noise — a deliberate clean map (no leftover slabs, closed mesh). Permanently discards ALL terrain edits for the current seed, so it is off by default and never touches the platform or legacy village.")]
    public bool EnableResetTerrainSaves = false;
    [Tooltip("QA: self-apply every combat status (Burn DoT, Wet, Blind, two wet-conducted Chill stacks) plus the food/drink stamina buff to the local player, so the status strip under the HUD bars can be play-tested. Applies to the player directly — no world placement.")]
    public bool EnableStatusEffectsDemo = false;
    [Tooltip("QA/perf (1ea): show a screen-space perf readout (avg FPS, frame ms, loaded chunk count, active collider count) refreshed ~4x/second so optimization passes can be A/B'd in the Editor without a profiler. Read-only — no world placement. On by default since 1ee so the baseline is visible; flip off to hide.")]
    public bool EnableFpsStats = true;
    [Tooltip("QA/perf (1gf): extend the FPS overlay with the WorldStreamer poll's per-stage ms split (near/finalize/colliders/farManage/farFinalize/props/rebuild drain) + the rolling worst-poll peaks and heavy-poll count, plus the ChunkLodManager band-sweep ms (last + peak). Read-only; needs EnableFpsStats on to display. Lets a long-sprint run show WHICH stage actually eats the gameplay frame instead of guessing. On by default so the baseline is visible; flip off to hide the extra lines.")]
    public bool EnablePollStageStats = true;
    [Tooltip("QA (1et): render the open world as the 1-metre stepped voxel terrain instead of the smooth heightfield (the experimental terrain model). Applied in Awake, BEFORE the WorldStreamer's first stream poll, so the whole world builds voxel from the start; leave OFF to keep smooth terrain.")]
    public bool EnableVoxelTerrain = false;
    [Tooltip("QA/render (1hi): LOW-POLY FACET world. Applied in Awake, BEFORE the WorldStreamer's first stream poll, so the whole smooth world reads as crisp flat facets: the far shell emits per-quad flat normals (crisp mesas on the horizon) and the 1ew adaptive stretch-split passes a 0 threshold so steep near slopes stay big flat quads instead of sub-dividing. Pure render/geometry-read change — saves, the 1 m tile grid, props, draw calls and the budgeted collider pipeline are untouched.\n\n1ia: DEFAULT IS NOW OFF. The user asked for the terrain algorithm to be reverted to its pre-1hi state, and this lane is what pushes the value onto the scene's WorldStreamer in Awake — so leaving it true would have re-enabled the facet look and silently undone the revert. Off means: the far shell builds its smooth central-difference haze, the 1ew stretch-split runs at RefineThreshold, and the near chunks render the full 1 m per-tile surface with side walls. Turn ON to A/B the old facet look; the streamer code is unchanged either way.")]
    public bool EnableLowPolyTerrain = false;
    [Tooltip("QA/render (1hi.1): coarse facet size for the REAL near chunks — every LowPolyStep-th world corner becomes one flat facet (must divide the 30 m chunk side; the far shell uses the same value and must also divide 90/180). Only meaningful while EnableLowPolyTerrain is ON, which it is not by default since 1ia. 1ia put the default back to 3, the pre-1hx value, so re-enabling the facet look gives the 1hi.1/1hi.2 world rather than the coarser 6 m facets of 1hx. Coarser facets also mean lumpier footing (the collider rides the same step) and more prop float/sink. The 1 m corner grid, saves, props and edits are untouched either way — an edit only visibly moves a facet vertex when the edited corner lands on this grid.")]
    public int LowPolyStep = 3;
    [Tooltip("QA (1eu): exercise the voxel sculpt API on the streamed terrain just off the platform — a directed crater (toolbar dig with cast direction clips the dent into a slope-front scoop), a SculptVoxelCave under a ridge, and a SculptVoxelRaise pillar. Needs the voxel terrain enabled to be meaningful (no-op on smooth terrain), edits REAL terrain — permanent chunk saves — and never touches the platform or legacy village.")]
    public bool EnableVoxelSculptDemo = false;
    [Tooltip("QA/perf (1gd): wire the WorldStreamer's speed-decoupled renderer clock — the streaming/render loop runs on its OWN coroutine beat (StreamHz, default 20 Hz) and yields one cool-down frame after any busy poll, and edited-terrain seam rebuilds run on background threads instead of holding the gameplay frame. That is exactly the 'immense lag at high player speed' scenario. Config-only lane: no world placement — any WorldStreamer found in the scene gets StreamInUpdate=true + DecoupleRenderFromGameplay=true (it just uses the scene's own toggle values otherwise).")]
    public bool EnableSpeedDecoupleRender = false;
    [Tooltip("QA (1gh): extend the FPS overlay with a chunk-diagnostics line for ChunkInspectX/Z — that chunk's real load state (loaded/dormant/absent), root GameObject active, renderer+mesh present, Lod1/Lod2 children on/off, LOD band, collider, and which far cell owns it (and whether that cell is LIVE or MISSING). For any 'chunk invisible for no reason' report: one screenshot answers whether it is missing, hidden by the LOD sweep, or under a dead far cell. Read-only; needs EnableFpsStats on to display.")]
    public bool EnableChunkDiagnostics = true;
    [Tooltip("QA (1gh): chunk coords inspected by the diagnostics line (the coords of the reported monster/relic chunk −8_3 in chunk-space, X −8, Z 3).")]
    public int ChunkInspectX = -8;
    [Tooltip("QA (1gh): chunk coords inspected by the diagnostics line (the coords of the reported monster/relic chunk −8_3 in chunk-space, X −8, Z 3).")]
    public int ChunkInspectZ = 3;
    [Tooltip("QA (1hy): press CornerAuditKey for a read-only audit of the terrain the player is actually LOOKING at, for the 'in every chunk corner the edge will not match — the player can see the void through that gap' report. Three sections, asked in the order the questions depend on each other. A fingerprint: more than one (facet step, vertex count) bucket means the resident world was built by two versions of the generator, which voids the other two as evidence. B void: any chunk footprint in the fully-owned ring with neither a visible real chunk nor a live far cell — the only failure mode that opens a real hole in an otherwise watertight chunk set, and the direct test of a visible 'gap'. C corners: does every loaded chunk place a rendered vertex AT each of its four corners, do the chunks meeting at a node agree on the corner height, and does each rendered corner match that chunk's own lattice. The VERDICT line names the first failure and the world XZ to walk to. Read-only — no rebuild, no re-stamp, no forced poll, so it describes the frame the key was pressed on. Needs EnableFpsStats on to display.")]
    public bool EnableCornerAudit = true;
    [Tooltip("QA (1hy): key that runs the rendered-corner + void audit. F3 because F1 is a skill hotkey and F5-F12 are editor cutscene shortcuts.")]
    public Key CornerAuditKey = Key.F3;
    private string _cornerAuditText;

    [Tooltip("QA (1ic): press LookAuditKey for a read-only audit of the per-spell visual identity — how many of the spell roster's resolved looks are actually distinguishable. Two spells count as the SAME identity when their impact family, cast family, projectile body shape and core colour (quantised to 8 bits per channel) all match; Scale and Tempo are excluded because they are sub-perceptual, and including them would let the number read 'unique' while two spells look identical. Groups the LIVE rosters (SkillCatalog magic + ClassSkillCatalog), so it measures what the player can actually cast. Read-only — resolves looks and prints, spawns nothing.")]
    public bool EnableLookAudit = true;
    [Tooltip("QA (1ic): key that runs the per-spell look-collision audit. F4: F1 is a skill hotkey, F3 is the 1hy corner/void audit, and the F2/F4 lanes 1hx removed were not restored.")]
    public Key LookAuditKey = Key.F4;
    private string _lookAuditText;
    private bool _lookAuditRun;

    private WorldNpcPlacer _npcPlacer;
    private bool _spawned;
    private bool _toolKitSpawned;
    private bool _pendingPlayerGrants;
    private bool _arenaReady;
    private GameObject _testGroundRoot;
    private readonly List<WeaponRackStand> _rackStands = new List<WeaponRackStand>();
    private ContextPromptUI _contextPrompt;
    private PlayerController _playerController;

    // Perf readout (1ea): one screen-space overlay refreshed on a 0.25s coroutine, never per frame.
    private Canvas _fpsCanvas;
    private TMPro.TextMeshProUGUI _fpsText;
    private float _fpsAccum;
    private int _fpsSamples;

    /// <summary>True once the independent floating platform has been built (safe to place the player on).</summary>
    public bool IsArenaReady => _arenaReady && PlatformTopY != float.MinValue;

    /// <summary>Top-surface height of the independent test platform (world Y).</summary>
    public static float PlatformTopY { get; private set; } = float.MinValue;

    private void Awake()
    {
        if (CreatePlatform)
            BuildTestGround();

        _npcPlacer = Object.FindAnyObjectByType<WorldNpcPlacer>();
        if (_npcPlacer == null)
        {
            var go = new GameObject("TestNpcPlacer");
            _npcPlacer = go.AddComponent<WorldNpcPlacer>();
            _npcPlacer.AutoPlaceOnStart = false;
        }

        // Voxel experiment (1et): flip the streamer ON before its first stream poll so the world
        // builds as stepped columns from the very first chunk. The streamer is a scene object found
        // in Awake; the flag must be set before the first Update-poll, which every Awake runs before.
        if (EnableVoxelTerrain)
        {
            var streamer = Object.FindAnyObjectByType<WorldStreamer>();
            if (streamer != null)
                streamer.VoxelTerrainEnabled = true;
            else
                Debug.LogWarning("[NewWorldTestGround] EnableVoxelTerrain: WorldStreamer not found yet — " +
                    "flip the toggle on the streamer object in the scene instead.");
        }

        // Low-poly facet look (1hi): same pre-first-poll contract as the voxel toggle above — the
        // lane sets the streamer's knob to this lane's value (and the far shell + near build paths
        // read it before any far cell or chunk is built). Note: named differently from the voxel
        // block's local above — C# forbids shadowing an enclosing-scope local.
        var polyStreamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (polyStreamer != null)
        {
            polyStreamer.LowPolyFacets = EnableLowPolyTerrain;
            // 1hi.1: the lane also drives the near-chunk facet step (see the field tooltip).
            polyStreamer.LowPolyStep = EnableLowPolyTerrain ? LowPolyStep : 0;
        }
        else
            Debug.LogWarning("[NewWorldTestGround] EnableLowPolyTerrain: WorldStreamer not found yet — " +
                "flip the toggle on the streamer object in the scene instead.");

        if (AutoSpawnOnStart)
            StartCoroutine(RunBenchSpawn());
    }

    /// <summary>
    /// Spawns the test bench one lane group per frame instead of all at once in Awake, so the
    /// heavy setup (weapon models, NPCs, enemies, grants) no longer blocks the first frames. The
    /// independent platform was already built in <see cref="Awake"/> (so it exists before any
    /// caller asks to place the player), and every lane sits flat on its top surface — no world
    /// terrain is read or written. Every lane runs isolated — a failure in one (e.g. one enemy
    /// spawn) logs an error instead of aborting the bench and stranding the player.
    /// </summary>
    private System.Collections.IEnumerator RunBenchSpawn()
    {
        if (_spawned) yield break;
        _spawned = true;

        // The platform is a solid collider built in Awake, so the player can be placed on it
        // immediately — there is no streaming gate and no void to race. This is the DEFAULT spawn
        // now (1dn): the player appears on the test ground unless AutoTeleportPlayerOnStart is off.
        if (CreatePlatform && AutoTeleportPlayerOnStart)
            RunSafely("player placement", PlacePlayerOnArena);

        if (EnableTools) { RunSafely("tool pickups", SpawnToolKit); yield return null; }
        if (EnableFarming) { RunSafely("farming plot", SpawnFarmingPlot); yield return null; }
        if (EnableLivestock) { RunSafely("livestock", SpawnLivestock); yield return null; }
        if (EnableEnemies) { RunSafely("enemies", SpawnEnemies); yield return null; }
        if (EnableBuildings) { RunSafely("buildings", SpawnBuildings); yield return null; }
        if (EnableNpcs) { RunSafely("npcs", SpawnNpcs); yield return null; }
        if (EnableReligion) { RunSafely("religion", SpawnReligion); yield return null; }
        if (EnablePoiHub) { RunSafely("POI hub", RegisterPoiHub); yield return null; }
        if (EnableWeapons)
        {
            RunSafely("weapons", SpawnAllWeapons);
            yield return null;
            RunSafely("weapon rack", SpawnWeaponRack);
            yield return null;
        }
        if (EnableSkills) { RunSafely("skills", GrantAllSkills); yield return null; }
        if (EnableMagicModels) { RunSafely("magic models", SpawnMagicModels); yield return null; }
        if (EnableGear) { RunSafely("gear", GrantStarterGear); yield return null; }
        if (EnableRaces) { RunSafely("races", GrantRaceAccess); yield return null; }
        if (EnableTerrainSlabDemo) { RunSafely("terrain shapes demo", SpawnTerrainSlabDemo); yield return null; }
        if (EnableDigLayersDemo) { RunSafely("dig layers demo", SpawnDigLayersDemo); yield return null; }
        if (EnableResetTerrainSaves) { RunSafely("terrain saves reset", ResetTerrainSaves); yield return null; }
        if (EnableStatusEffectsDemo) { RunSafely("status effects demo", SpawnStatusEffectsDemo); yield return null; }
        if (EnableVoxelSculptDemo) { RunSafely("voxel sculpt demo", SpawnVoxelSculptDemo); yield return null; }
        if (EnableFpsStats) { RunSafely("fps stats", SpawnFpsStats); yield return null; }
        if (EnableSpeedDecoupleRender) { RunSafely("speed-decoupled renderer", ApplySpeedDecoupleSettings); yield return null; }
        RunSafely("player grants", TryDeferPlayerGrants);

        // Safety net: if the platform wasn't ready when the bench started (e.g. built later or
        // CreatePlatform toggled) AND an auto-teleport was requested, pull the player onto it at
        // the very end. PlacePlayerOnArena self-guards on IsArenaReady, so this can never teleport
        // onto missing ground.
        if (AutoTeleportPlayerOnStart && !IsArenaReady)
            RunSafely("player placement (fallback)", PlacePlayerOnArena);
    }

    /// <summary>A failing lane (one bench system) must never abort the whole bench coroutine and
    /// strand the player away from the arena — log it and keep going.</summary>
    private void RunSafely(string lane, System.Action spawn)
    {
        try
        {
            spawn?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NewWorldTestGround] Lane \"{lane}\" failed — detail below. Anything already placed stays.\n{ex}");
        }
    }

    /// <summary>Teleport the player onto the independent platform (2 m above its top surface).
    /// Self-guards on <see cref="IsArenaReady"/> — never moves the player onto missing ground.</summary>
    private void PlacePlayerOnArena()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        if (!IsArenaReady)
        {
            Debug.LogWarning("[NewWorldTestGround] Skipped arena teleport — the test platform isn't built.");
            return;
        }
        player.TeleportTo(GetSpawnPoint());
    }

    /// <summary>
    /// QA for the status strip under the HUD bars (1cu): self-apply every combat status plus the
    /// food/drink stamina modifier to the local player so the strip is visible immediately. Wet is
    /// applied first (Burn taken afterwards would be instantly doused by the soak), then Chill twice
    /// — water conduction grants +2 stacks each, stopping at 4/5 so the gauge displays without the
    /// wet "freeze" path destroying the component. All durations refresh on re-run (idempotent).
    /// </summary>
    private void SpawnStatusEffectsDemo()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var root = player.transform.root.gameObject;
        WetStatus.Apply(root, 4f);
        SpellDoT.Apply(root, 0.5f, 6f, 0.5f, DamageType.Fire);
        ChillStatus.Apply(root);
        ChillStatus.Apply(root);
        BlindStatus.Apply(root, 4f);
        player.ApplyStaminaRegenModifier(1.2f, 120f);
    }

    /// <summary>
    /// Opt-in demo for Earth terrain shapes via <see cref="TerrainDeformer"/> (1cj): casts an
    /// Earth Wall (smooth ridge, capped at noise + lift), casts it again to show repeat casts
    /// DON'T stack any higher, then a Pillar (smooth column) and a Crater (smooth dish dent).
    /// This deforms REAL streamed terrain (permanent per-chunk saves), so it is off by default
    /// and aimed clear of the platform footprint and the legacy WorldBuilder village.
    /// </summary>
    private void SpawnTerrainSlabDemo()
    {
        float baseX = PlatformCenter.x - PlatformSize * 0.5f - 18f;
        float baseZ = PlatformCenter.z;

        TerrainDeformer.Apply(new Vector3(baseX, 0f, baseZ), 2f, TerrainShape.Wall, Vector3.forward);
        TerrainDeformer.Apply(new Vector3(baseX, 0f, baseZ), 2f, TerrainShape.Wall, Vector3.forward);
        TerrainDeformer.Apply(new Vector3(baseX - 7f, 0f, baseZ), 1.8f, TerrainShape.Pillar, default);
        TerrainDeformer.Apply(new Vector3(baseX + 7f, 0f, baseZ), 2.2f, TerrainShape.Crater, default);
    }

    /// <summary>
    /// QA lane for the layered strata (grass -> dirt -> stone): excavates two craters on the
    /// streamed terrain just off the platform east edge by REPEATING the shared crater dig (each
    /// cast ratchets the floor one CraterStep deeper — exactly the shovel/pickaxe excavation path).
    /// The shallow pit bottoms out in the dirt band (2 casts), the deep one digs through into the
    /// stone band (4 casts), so their side walls show the banding: a grass ring, a dirt band, then
    /// stone. Deforms REAL streamed terrain (permanent per-chunk saves) — off by default, clear of
    /// the platform footprint and the legacy WorldBuilder village.
    /// </summary>
    private void SpawnDigLayersDemo()
    {
        float baseX = PlatformCenter.x + PlatformSize * 0.5f + 20f;
        float baseZ = PlatformCenter.z + 22f;

        // Shallow pit — well into the dirt band (2 * ~1.1 m step ~ 2.2 m deep).
        for (int i = 0; i < 2; i++)
            TerrainDeformer.Apply(new Vector3(baseX, 0f, baseZ), 2.4f, TerrainShape.Crater, default);

        // Deep pit — through the dirt/stone blend into solid stone (4 casts ~ 4.4 m).
        for (int i = 0; i < 4; i++)
            TerrainDeformer.Apply(new Vector3(baseX + 10f, 0f, baseZ), 2.4f, TerrainShape.Crater, default);
    }

    /// <summary>
    /// Opt-in QA lane: permanently wipe the current world's terrain chunk saves and reload every
    /// loaded chunk so the ground regenerates pristine from noise (no leftover slabs, no holes).
    /// Operates on the streamed world only — never the platform or the legacy WorldBuilder village.
    /// </summary>
    private void ResetTerrainSaves()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
        {
            Debug.LogWarning("[NewWorldTestGround] ResetTerrainSaves skipped — no WorldStreamer found.");
            return;
        }
        streamer.ResetTerrainSaves();
    }

    /// <summary>
    /// QA lane for the voxel sculpt API (1eu): runs the three public operations against REAL
    /// streamed terrain just off the platform's west edge:
    ///   (1) a directed crater — the toolbar dig with a cast direction clips the dent into a
    ///       slope-front scoop (only the tiles ahead of the digger are hollowed);
    ///   (2) a SculptVoxelCave under a ridge (an overburden shelf keeps a roof above the void);
    ///   (3) a SculptVoxelRaise pushing a pillar up out of the ground.
    /// Each is placed a short walk off the platform, on streamed terrain that is loaded and
    /// voxel-meshed when the lane runs (SculptVoxel* only touches loaded chunks). Needs the voxel
    /// terrain enabled — on smooth terrain the operations have nothing to edit, so the lane logs a
    /// warning and stays put. Off by default; never touches the platform or legacy WorldBuilder
    /// village.
    /// </summary>
    private void SpawnVoxelSculptDemo()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
        {
            Debug.LogWarning("[NewWorldTestGround] Voxel sculpt demo skipped — no WorldStreamer found.");
            return;
        }
        if (!streamer.VoxelTerrainEnabled)
        {
            Debug.LogWarning("[NewWorldTestGround] Voxel sculpt demo skipped — the voxel terrain is OFF. " +
                "Tick the voxel terrain toggle to QA the sculpt API on real chunks.");
            return;
        }

        float baseX = PlatformCenter.x - PlatformSize * 0.5f - 24f;
        float baseZ = PlatformCenter.z;

        // (2) Cave shelf first so the directed dig below doesn't collide with the ridge.
        float caveX = baseX;
        float caveZ = baseZ - 14f;
        streamer.SculptVoxelCave(new Vector3(caveX, 0f, caveZ), 6f, 3f, 4f);

        // (1) Directed crater: cast due east, so the scoop opens toward the platform.
        TerrainDeformer.Dig(new Vector3(baseX + 4f, 0f, baseZ), 3f, Vector3.right);

        // (3) Raise a pillar clear of the other two features.
        float raiseX = baseX;
        float raiseZ = baseZ + 14f;
        streamer.SculptVoxelRaise(new Vector3(raiseX, 0f, raiseZ), 4f, 8f);

        // Stone marker pegs so each feature is findable from the platform edge.
        SpawnSculptMarker("SculptCave", new Vector3(caveX, PlatformTopY, caveZ));
        SpawnSculptMarker("SculptDig", new Vector3(baseX + 4f, PlatformTopY, baseZ));
        SpawnSculptMarker("SculptRaise", new Vector3(raiseX, PlatformTopY, raiseZ));
    }

    /// <summary>One small stone peg on the platform top marking a sculpt demo feature.</summary>
    private void SpawnSculptMarker(string name, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos + new Vector3(0f, 0.6f, 0f);
        go.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.sharedMaterial = SolidMaterial(new Color(0.5f, 0.5f, 0.5f));
    }

    /// <summary>
    /// Spawn the whole test bench through the budgeted coroutine (safe to call repeatedly). The
    /// platform is built in Awake, so a sync caller just starts the async lane path.
    /// </summary>
    public void SpawnBench()
    {
        if (_spawned) return;
        StartCoroutine(RunBenchSpawn());
    }

    /// <summary>
    /// The player-dependent grants (skills/gear/races) bail out when the player doesn't exist yet
    /// (GameManager.Player is wired by GameBootstrap / PlayerController.Start). Defer them to a
    /// one-shot poll so boot order can never silently drop the whole skill/gear/race bench.
    /// </summary>
    private void TryDeferPlayerGrants()
    {
        if (_pendingPlayerGrants) return;
        if (GameManager.Instance?.Player != null) return;
        if (EnableSkills || EnableGear || EnableRaces) _pendingPlayerGrants = true;
    }

    private void RunPendingPlayerGrants()
    {
        if (!_pendingPlayerGrants) return;
        if (GameManager.Instance?.Player == null) return;
        _pendingPlayerGrants = false;
        if (EnableSkills) GrantAllSkills();
        if (EnableGear) GrantStarterGear();
        if (EnableRaces) GrantRaceAccess();
    }

    /// <summary>
    /// Re-apply the bench grants (every catalog weapon, skill budget, starter gear, race access)
    /// after a new game clears the ToolManager inventory. At bootstrap <see cref="SpawnBench"/>
    /// runs during Awake, but <see cref="GameManager.StartNewGame"/> clears the bag right after,
    /// so the grants above alone would never be visible — call this from the new-game entry points
    /// post-clear. The tool/food kit is NOT here: it lives as world pickups, which the inventory
    /// clear cannot touch — those are placed once by the deferred bench lane on the loaded terrain.
    /// </summary>
    public void GrantBenchBag()
    {
        if (EnableWeapons) SpawnAllWeapons();
        if (EnableRaces) GrantRaceAccess();
        if (EnableSkills) GrantAllSkills();
        if (EnableGear) GrantStarterGear();
        TryDeferPlayerGrants();
    }

    /// <summary>
    /// Builds the INDEPENDENT floating test platform: a self-contained mesh + collider raised clear
    /// of the local terrain, so the world's procedural ground is never edited (no carve, no flatten,
    /// no persistence writes). The platform's top is a flat surface at <see cref="PlatformTopY"/>;
    /// every bench lane keys its placement off it. The world seed is only read to coarse-sample the
    /// same 5-octave noise the streamed terrain uses, so the platform floats just above the local
    /// high point instead of intersecting it.
    /// </summary>
    private void BuildTestGround()
    {
        if (_testGroundRoot != null)
            return;

        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        long seed = streamer != null ? streamer.Seed : 1337;

        // Coarse-sample the local terrain (the surface is smooth, so a few samples per side is
        // enough) and float the platform clear of the highest point so terrain and trees never poke
        // through. This only READS the noise — the terrain itself is never modified.
        float half = PlatformSize * 0.5f;
        float maxGround = float.MinValue;
        const int steps = 8;
        for (int i = 0; i <= steps; i++)
        {
            for (int j = 0; j <= steps; j++)
            {
                float sx = PlatformCenter.x - half + PlatformSize * i / steps;
                float sz = PlatformCenter.z - half + PlatformSize * j / steps;
                float h = TerrainNoiseGenerator.GetHeight(seed, sx, sz);
                if (h > maxGround)
                    maxGround = h;
            }
        }
        if (maxGround == float.MinValue)
            maxGround = PlatformCenter.y;

        float topY = maxGround + PlatformClearance;

        _testGroundRoot = new GameObject("TestGroundRoot");
        _testGroundRoot.transform.SetParent(null);
        _testGroundRoot.transform.SetPositionAndRotation(
            new Vector3(PlatformCenter.x, topY, PlatformCenter.z), Quaternion.identity);

        // Solid slab — its BoxCollider top face (local 0) is the standing surface at topY.
        var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "TestGroundSlab";
        slab.transform.SetParent(_testGroundRoot.transform, false);
        slab.transform.localScale = new Vector3(PlatformSize, PlatformThickness, PlatformSize);
        slab.transform.localPosition = new Vector3(0f, -PlatformThickness * 0.5f, 0f);
        var slabRenderer = slab.GetComponent<MeshRenderer>();
        if (slabRenderer != null)
            slabRenderer.sharedMaterial = PlatformMaterial(streamer);

        // Four corner legs hang BELOW the slab so the platform reads as an independent floating
        // structure (its top stays one flat level for the bench lanes).
        float post = Mathf.Max(0.8f, PlatformSize * 0.012f);
        float legHeight = PlatformThickness * 7f;
        for (int i = 0; i < 4; i++)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.name = "TestGroundLeg" + i;
            pole.transform.SetParent(_testGroundRoot.transform, false);
            pole.transform.localScale = new Vector3(post, legHeight, post);
            pole.transform.localPosition = new Vector3(
                (i % 2 == 0 ? -1f : 1f) * (half - post),
                -PlatformThickness - legHeight * 0.5f,
                (i < 2 ? -1f : 1f) * (half - post));
            var poleRenderer = pole.GetComponent<MeshRenderer>();
            if (poleRenderer != null)
                poleRenderer.sharedMaterial = SolidMaterial(new Color(0.24f, 0.2f, 0.17f));
        }

        PlatformCenter.y = topY;
        PlatformTopY = topY;
        _arenaReady = true;
    }

    /// <summary>
    /// Material for the platform top: a plain URP Lit grass material, NOT the streamer's layered
    /// terrain material. The slab is a stock cube with no vertex colors, so the vertex-color
    /// TerrainLayered shader would resolve its albedo to black; the platform is a test bench and
    /// has no strata bands to show anyway.
    /// </summary>
    private static Material PlatformMaterial(WorldStreamer streamer)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { color = ColorPalette.GrassGreen };
    }

    public Vector3 GetSpawnPoint()
    {
        float groundY = PlatformTopY != float.MinValue ? PlatformTopY : PlatformCenter.y;
        return new Vector3(PlatformCenter.x, groundY + 2f, PlatformCenter.z + PlatformSize * 0.45f);
    }

    private void SpawnFarmingPlot()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;

        float startX = PlatformCenter.x - PlatformSize * 0.34f;
        float z = PlatformCenter.z + PlatformSize * 0.18f;

        string[] seeds = { "wheat_seed", "corn_seed", "tomato_seed", "rice_seed" };
        for (int i = 0; i < seeds.Length; i++)
        {
            float x = startX + i * 3.5f;
            Vector3 pos = new Vector3(x, PlatformTopY + 0.1f, z);
            var field = wb.TillGround(pos, PlatformTopY);
            if (field == null) continue;
            wb.PlantCrop(field, seeds[i].Replace("_seed", ""));
            if (i % 2 == 0) wb.WaterField(pos);
            if (i % 3 == 0) wb.FertilizeField(pos);
            if (i % 4 == 0) wb.BoostFieldGrowth(pos);
        }
    }

    private void SpawnLivestock()
    {
        Livestock.AnimalType[] all =
        {
            Livestock.AnimalType.Cow, Livestock.AnimalType.Pig, Livestock.AnimalType.Sheep, Livestock.AnimalType.Goat,
            Livestock.AnimalType.Chicken, Livestock.AnimalType.Duck, Livestock.AnimalType.Turkey
        };
        float startX = PlatformCenter.x + PlatformSize * 0.2f;
        float z = PlatformCenter.z + PlatformSize * 0.3f;
        for (int i = 0; i < all.Length; i++)
        {
            var go = new GameObject("Test_" + all[i]);
            float x = startX + i * 4f;
            go.transform.position = new Vector3(x, PlatformTopY + 0.1f, z);
            var live = go.AddComponent<Livestock>();
            live.Type = all[i];
            Livestock.BuildModelInto(go.transform, all[i]);
        }
    }

    private void SpawnEnemies()
    {
        // Every enemy type the model builder knows, in two rows (all 20 in the roster).
        string[] rowA =
        {
            "slime", "wolf", "goblin", "bandit", "treant", "golem", "drake", "undead", "slug", "scorpion"
        };
        string[] rowB =
        {
            "mummy", "yeti", "ice_wolf", "fire_elemental", "dragon", "demon", "mimic", "sea_creature", "skeleton", "bat"
        };
        // Extra copies so the arena has a denser horde to clear.
        string[] horde = { "slime", "slime", "goblin", "goblin", "bat", "skeleton" };

        float startX = PlatformCenter.x - 27f;
        SpawnEnemyRow(rowA, startX, PlatformCenter.z - 40f);
        SpawnEnemyRow(rowB, startX, PlatformCenter.z - 32f);
        SpawnEnemyRow(horde, startX, PlatformCenter.z - 24f);

        // Two indestructible training dummies — damage testing (plain vs 50% damage reduction).
        // Moved clear of the enemy rows so the grid layout above owns the south arena.
        SpawnDummy("TestDummy_Plain", PlatformCenter.x - 4f, PlatformCenter.z - 18f, 0f);
        SpawnDummy("TestDummy_Armored", PlatformCenter.x + 4f, PlatformCenter.z - 18f, 0.5f);

        if (IncludeBoss)
            SpawnBoss();
    }

    /// <summary>One row of enemies from <paramref name="ids"/> at 6-unit spacing.</summary>
    private void SpawnEnemyRow(string[] ids, float startX, float z)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            var go = new GameObject("TestEnemy_" + ids[i]);
            float eX = startX + i * 6f;
            go.transform.position = new Vector3(eX, PlatformTopY + 0.05f, z);
            go.AddComponent<SphereCollider>();
            EnemyCatalog.AddEnemyComponent(go, ids[i]);
        }
    }

    private void SpawnDummy(string name, float x, float z, float damageReduction)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(x, PlatformTopY + 0.05f, z);
        var col = go.AddComponent<SphereCollider>();
        col.radius = 1f;
        col.center = new Vector3(0f, 0.85f, 0f);

        // DummyEnemy carries the immortal/0-damage/regen defaults (1do); only the flat damage
        // reduction variant and aggro ranges differ between the two dummies.
        var ec = go.AddComponent<DummyEnemy>();
        ec.DamageReduction = damageReduction;
    }

    private void SpawnBoss()
    {
        float bz = PlatformCenter.z - PlatformSize * 0.42f;
        Vector3 pos = new Vector3(PlatformCenter.x, PlatformTopY + 0.1f, bz);
        var go = new GameObject("TestBoss");
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().size = new Vector3(2.4f, 3f, 1.6f);
        if (!go.GetComponent<BossController>())
        {
            var boss = go.AddComponent<BossController>();
            boss.BossId = "test_golem";
        }
    }

    private void SpawnBuildings()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;

        string[] types = { "table", "chair", "chest" };
        float z = PlatformCenter.z + PlatformSize * 0.3f;
        float startX = PlatformCenter.x - 20f;
        for (int i = 0; i < types.Length; i++)
        {
            float bx = startX + i * 8f;
            wb.SpawnBuildingDirect(types[i], new Vector3(bx, PlatformTopY, z), 0);
        }
    }

    /// <summary>
    /// Build the three holy places — taoist shrine, church, pagoda — plus their worship NPCs
    /// (taoist / priest / monk) so the Faith system can be exercised on the bench. Mirrors the
    /// legacy <c>CreateWorld</c> wiring (<see cref="WorldBuilder.BuildShrine"/>/<c>BuildChurch</c>/
    /// <c>BuildPagoda</c> + <c>MapBuilder.Build*Npc</c> + the matching NPC component); each site is
    /// placed on the platform clear of the other bench lanes.
    /// </summary>
    private void SpawnReligion()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        wb.EnsureWorldRoot();
        if (PlatformTopY == float.MinValue)
        {
            Debug.LogWarning("[NewWorldTestGround] Skipped religion lane — the test platform isn't built.");
            return;
        }

        float baseY = PlatformTopY;
        float cx = PlatformCenter.x;
        float cz = PlatformCenter.z;

        // The three NPCs stand on the platform, not on a structure: the taoist 2.9 m
        // clear of the shrine's stair foot (the rebuilt shrine's treads run to
        // z = -9.6 on site) and the monk 9.5 m west of the pagoda's centre, outside
        // its 14 m podium, which he was previously standing inside.
        wb.BuildShrine(new Vector3(cx - 30f, baseY, cz - 38f));
        var taoist = MapBuilder.BuildTaoistNpc(wb.WorldRoot.transform, new Vector3(cx - 30f, baseY, cz - 50.5f), Quaternion.identity);
        StandOnGround(taoist, baseY);
        taoist.AddComponent<TaoistPriestNPC>();

        wb.BuildChurch(new Vector3(cx + 30f, baseY, cz - 35f));
        var priest = MapBuilder.BuildPriestNpc(wb.WorldRoot.transform, new Vector3(cx + 17.5f, baseY, cz - 35f), Quaternion.Euler(0f, 90f, 0f));
        StandOnGround(priest, baseY);
        priest.AddComponent<PriestNPC>();

        wb.BuildPagoda(new Vector3(cx - 30f, baseY, cz + 44f));
        var monk = MapBuilder.BuildMonkNpc(wb.WorldRoot.transform, new Vector3(cx - 39.5f, baseY, cz + 44f), Quaternion.Euler(0f, 90f, 0f));
        StandOnGround(monk, baseY);
        monk.AddComponent<PagodaMonkNPC>();
    }

    /// <summary>
    /// Drop a block-built NPC so its FEET rest on <paramref name="groundY"/>. The rigs are
    /// authored around a body origin - head at local +0.52, shoes at local -0.88 - so a root
    /// placed at the ground plane sinks the figure to the knees: 0.915 m for all three faith
    /// NPCs. Measured, never hardcoded: only the rig knows where its own lowest block is, and a
    /// literal 0.915 here would silently go stale the next time a shoe or a hem moves. Renderer
    /// bounds are read after the hierarchy is built, so this also survives a rig that later gains
    /// a skirt or a longer robe.
    /// </summary>
    private static void StandOnGround(GameObject npc, float groundY)
    {
        if (npc == null) return;
        var renderers = npc.GetComponentsInChildren<Renderer>();
        float minY = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            minY = Mathf.Min(minY, renderers[i].bounds.min.y);
        }
        if (minY == float.MaxValue) return;
        npc.transform.position += Vector3.up * (groundY - minY);
    }

    private void SpawnNpcs()
    {
        if (_npcPlacer == null) return;

        float z = PlatformCenter.z - PlatformSize * 0.18f;
        float startX = PlatformCenter.x + 6f;
        _npcPlacer.Place("test_vendor", "QA Merchant", NpcRoleKind.Vendor, NpcShopMode.Tools, new Vector3(startX, PlatformTopY, z), "fishshop");
        _npcPlacer.Place("test_quest", "QA Hermit", NpcRoleKind.QuestGiver, NpcShopMode.Vendor, new Vector3(startX + 4f, PlatformTopY, z));
        _npcPlacer.Place("test_follower", "QA Companion", NpcRoleKind.Follower, NpcShopMode.Vendor, new Vector3(startX + 8f, PlatformTopY, z));
        _npcPlacer.Place("test_grocer", "QA Grocer", NpcRoleKind.Vendor, NpcShopMode.Grocery, new Vector3(startX + 12f, PlatformTopY, z));
        _npcPlacer.Place("test_cafe", "QA Cafe", NpcRoleKind.Vendor, NpcShopMode.Cafe, new Vector3(startX + 16f, PlatformTopY, z));
    }

    private void RegisterPoiHub()
    {
        var def = ScriptableObject.CreateInstance<POIDefinition>();
        def.name = "POI_test_hub";
        def.Id = "test_hub";
        def.DisplayName = "QA Hub";
        def.Kind = PoiKind.Town;
        def.Biome = BiomeType.Plains;
        def.LocalPosition = PlatformCenter;
        def.Radius = PlatformSize * 0.5f;
        def.IsFastTravelPoint = true;
        POIRegistry.Register(def);
    }

    /// <summary>
    /// Lay the tool/food discovery kit out along the platform's east edge as real world pickups
    /// (the west edge hosts the weapon pedestals) — press E on one to add it to the inventory
    /// (see <see cref="ToolManager.TryPickupNearby"/>). Runs once from the deferred bench lane so
    /// the platform is already built; the guard keeps a re-entry from duplicating pickups.
    /// </summary>
    private void SpawnToolKit()
    {
        if (_toolKitSpawned) return;
        _toolKitSpawned = true;

        var wb = WorldBuilder.Instance;
        if (wb == null) return;

        // The 10-slot tool rack — a representative discovery kit.
        string[] kit =
        {
            "axe", "pickaxe", "hoe", "hammer", "scythe", "watering_can",
            "fertilizer", "club", "rosary", "fishing_rod"
        };
        // Foods round out the kit (no seeds by request).
        string[] extras = { "banh_mi", "com_tam", "nuoc_dau", "mi_chinh", "xap_phong" };

        float x = PlatformCenter.x + PlatformSize * 0.42f;
        float startZ = PlatformCenter.z - 26f;
        int total = kit.Length + extras.Length;
        float step = 52f / Mathf.Max(1, total - 1);

        int i = 0;
        foreach (var type in kit)
        {
            float iz = startZ + i * step;
            SpawnToolPickup(wb, type, new Vector3(x, PlatformTopY + 0.15f, iz), 1);
            i++;
        }
        foreach (var type in extras)
        {
            float iz = startZ + i * step;
            SpawnToolPickup(wb, type, new Vector3(x, PlatformTopY + 0.15f, iz), 5);
            i++;
        }
    }

    /// <summary>One <c>Pickup_&lt;itemId&gt;</c> world drop that grants <paramref name="amount"/>
    /// of the item when pressed E on (see <see cref="ToolManager.TryPickupTool"/>).</summary>
    private void SpawnToolPickup(WorldBuilder wb, string itemId, Vector3 pos, int amount)
    {
        var pickup = wb.SpawnPickup(itemId, pos);
        if (pickup == null) return;
        pickup.AddComponent<PickupAmount>().Amount = amount;
    }

    /// <summary>
    /// Ensure the player has the Phase 10 combat/skill stack, wire the weapon inventory (owned
    /// weapons feed the Character Info > Inventory list / Equipment hand slots), and equip the
    /// starter weapon.
    /// </summary>
    private void SpawnAllWeapons()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        WeaponCatalog.EnsureBuilt();
        WeaponRigBuilder.EnsureCombatStack(player.gameObject);

        var inv = player.GetComponent<WeaponInventory>();
        if (inv == null)
            inv = player.gameObject.AddComponent<WeaponInventory>();

        // Grant every catalog weapon into the player's inventory (owned list + bag copies) so
        // each can be dragged onto the L. Hand / R. Hand slots from the Character Info sheet.
        var tm = ToolManager.Instance;
        foreach (var weapon in WeaponCatalog.All)
        {
            if (weapon == null) continue;
            inv.EnsureOwned(weapon.id);
            tm?.AddItem(weapon.id, 1);
        }

        var starter = WeaponCatalog.Find(WeaponCatalog.StarterWeaponId);
        if (starter != null)
        {
            WeaponRigBuilder.EquipInto(player.gameObject, starter);
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
                // Casual at boot → the starter weapon stays stowed on the body; it only draws
                // while fighting (reverted 1cr rule).
                pc.ReApplyWeaponPose(instant: true);
            else
                WeaponRigBuilder.ApplyPose(player.gameObject, draw: false, instant: true);
        }
    }

    /// <summary>
    /// Place one pedestal stand per catalog weapon along the platform's west edge so every weapon
    /// is visible on the test ground. Pressing E on a stand adds it to the player's inventory
    /// (see <see cref="PlayerController.HandleInteractionKeys"/>).
    /// </summary>
    private void SpawnWeaponRack()
    {
        WeaponCatalog.EnsureBuilt();
        _rackStands.Clear();

        var all = WeaponCatalog.All;
        if (all == null || all.Count == 0) return;

        float x = PlatformCenter.x - PlatformSize * 0.42f;
        float startZ = PlatformCenter.z - 26f;
        float step = 52f / Mathf.Max(1, all.Count - 1);

        for (int i = 0; i < all.Count; i++)
        {
            var weapon = all[i];
            if (weapon == null) continue;

            var stand = new GameObject("WeaponRack_" + weapon.id);
            var rt = stand.AddComponent<WeaponRackStand>();
            rt.WeaponId = weapon.id;
            float iz = startZ + i * step;
            stand.transform.position = new Vector3(x, PlatformTopY + 0.15f, iz);
            stand.transform.rotation = Quaternion.identity;

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(stand.transform, false);
            pedestal.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            var pmr = pedestal.GetComponent<MeshRenderer>();
            if (pmr != null)
                pmr.sharedMaterial = SolidMaterial(new Color(0.24f, 0.2f, 0.17f));

            var site = new GameObject("ModelSite");
            site.transform.SetParent(stand.transform, false);
            site.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            WeaponModelBuilder.Build(weapon.id, site.transform);

            var box = stand.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.2f, 0f);
            box.size = new Vector3(1f, 2.6f, 1f);

            _rackStands.Add(rt);
        }
    }

    /// <summary>Cache the player's controller ref (resolved once per player object).</summary>
    private PlayerController PlayerControllerCached(PlayerController player)
    {
        if (_playerController == null || _playerController.gameObject != player.gameObject)
            _playerController = player;
        return _playerController;
    }

    private void Update()
    {
        // (1hy) Rendered-corner + void audit: one key, one frame, no side effects. Polled FIRST
        // because the weapon-rack logic below returns early on its own conditions, and a lane that
        // could be skipped by an unrelated early return would report the wrong frame.
        if (EnableCornerAudit)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[CornerAuditKey] != null && kb[CornerAuditKey].wasPressedThisFrame)
                RunCornerAudit();
        }

        // 1ic: the look-collision audit. Polled next, and for the same reason: this Update has early
        // returns below, so a lane after them could report nothing at all.
        if (EnableLookAudit)
        {
            Keyboard kb2 = Keyboard.current;
            if (kb2 != null && kb2[LookAuditKey] != null && kb2[LookAuditKey].wasPressedThisFrame)
                RunLookAudit();
        }

        RunPendingPlayerGrants();
        if (!EnableWeapons || _rackStands.Count == 0) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null || gm.GamePaused) return;
        var playerController = PlayerControllerCached(gm.Player);
        if (playerController != null && playerController.FightingMode) return;

        var prompt = _contextPrompt;
        if (prompt == null)
            _contextPrompt = prompt = Object.FindAnyObjectByType<ContextPromptUI>();
        if (prompt == null) return;
        Vector3 playerPos = gm.Player.transform.position;
        float nearest = float.MaxValue;
        WeaponRackStand nearStand = null;
        for (int i = 0; i < _rackStands.Count; i++)
        {
            var stand = _rackStands[i];
            if (stand == null || stand.Collected) continue;
            float d = Vector3.Distance(playerPos, stand.transform.position);
            if (d < nearest && d <= 3.2f)
            {
                nearest = d;
                nearStand = stand;
            }
        }

        if (nearStand != null && prompt != null)
        {
            var weapon = WeaponCatalog.Find(nearStand.WeaponId);
            string name = weapon != null && !string.IsNullOrEmpty(weapon.displayName) ? weapon.displayName : nearStand.WeaponId;
            prompt.ShowPrompt(Localization.F("E - {0}", name), 0.2f);
        }
    }

    /// <summary>
    /// QA (1hy): run the WorldStreamer's read-only rendered-corner + void audit and keep it on the
    /// HUD. The 1gh chunk-diagnostics line above reads ONE chunk's lifecycle state; this reads the
    /// drawn mesh layer of the WHOLE resident set, which is the layer that the report needs and the
    /// one no lattice comparison can stand in for (rule 8: a green lattice is not evidence about what
    /// is drawn). One key, one report — no rebuild, no re-stamp and no forced poll, so the numbers
    /// describe the frame the key was pressed on (rule 7).
    /// </summary>
    private void RunCornerAudit()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
        {
            _cornerAuditText = "corner audit: no WorldStreamer in the scene";
            Debug.LogWarning("[NewWorldTestGround] " + _cornerAuditText);
            return;
        }

        _cornerAuditText = streamer.RenderedCornerAudit();
        Debug.Log("[NewWorldTestGround] " + _cornerAuditText);
    }

    /// <summary>
    /// QA (1ic): the per-spell look-collision audit. Resolves every spell the player can actually
    /// cast and reports how many DISTINGUISHABLE identities came out.
    ///
    /// <para><b>Why the axes are named in the output.</b> "172 unique" is only meaningful next to
    /// the definition of unique, and the definition is the part that can quietly flatter the result.
    /// Two spells are the same identity here when impact family, cast family, body shape and core
    /// RGB all match at 8 bits per channel. Scale and Tempo are deliberately left OUT: they are
    /// sub-perceptual, so counting them would let a number read unique while two spells look the
    /// same on screen (the rule-7 "gate a classifier on the width of its own test" habit, applied to
    /// an identity metric instead of a spatial one).</para>
    ///
    /// <para><b>Why it groups by the axes and not by <c>SpellLook.Fingerprint</c>.</b> The
    /// fingerprint is a 32-bit hash of those same axes. Grouping on it would report a genuine
    /// 32-bit hash collision as "two spells look the same", which is a different claim about a
    /// different thing. The key below packs the real axes, so a reported collision is a real
    /// identity collision.</para>
    ///
    /// <para><b>Scope names its owners.</b> It walks the two LIVE rosters — SkillCatalog's magic
    /// castables and ClassSkillCatalog's spell effects — because those are what a player can cast.
    /// The dead <c>RaceSkillCatalog.MakeSpell</c> twin builds no reachable spell and is excluded;
    /// it is kept in signature parity with the live one, not in the audit's denominator.</para>
    ///
    /// Read-only: it resolves looks into a local dictionary, prints, and places nothing.
    /// </summary>
    private void RunLookAudit()
    {
        SkillCatalog.EnsureBuilt();
        ClassSkillCatalog.EnsureBuilt();

        var groups = new Dictionary<ulong, List<string>>();
        var byImpact = new Dictionary<SpellImpactStyle, int>();
        var byCast = new Dictionary<SpellCastStyle, int>();
        var byShape = new Dictionary<ProjectileShape, int>();
        int total = 0;
        int authored = 0;
        var seen = new HashSet<string>();

        void Add(SpellData spell, string owner)
        {
            if (spell == null) return;
            // Guard against double counting a SpellData reachable from two rosters: the denominator
            // is SPELLS, and a roster bug that lists one spell twice must not inflate the count.
            if (spell.id != null && !seen.Add(spell.id)) return;
            total++;
            var look = SpellLook.Resolve(spell);
            if (look.Authored) authored++;
            Bump(byImpact, look.Impact);
            Bump(byCast, look.Cast);
            Bump(byShape, look.DisplayShape);
            ulong key = LookKey(look);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<string>();
                groups[key] = list;
            }
            list.Add((owner + "|" + spell.id) + " " + Describe(look));
        }

        foreach (var skill in SkillCatalog.OfType(SkillType.Magic))
        {
            if (skill == null || skill.IsPassive) continue;
            if (skill.Effect is SpellCastEffect cast && cast.Spell != null)
                Add(cast.Spell, "skill");
        }
        if (ClassSkillCatalog.All != null)
        {
            foreach (var cs in ClassSkillCatalog.All)
            {
                if (cs == null || cs.IsPassive || cs.Effects == null) continue;
                foreach (var eff in cs.Effects)
                    if (eff is ClassSpellEffect ce && ce.Spell != null)
                        Add(ce.Spell, "class");
            }
        }

        int colliding = 0;
        int worst = 1;
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            colliding++;
            if (kv.Value.Count > worst) worst = kv.Value.Count;
        }

        // A clean run must not print "worst 1" - that reads like a finding when it is the
        // absence of one, and a number that flatters the result is worse than no number.
        string worstText = colliding > 0 ? worst.ToString() : "none";

        var sb = new System.Text.StringBuilder(512);
        sb.Append("look audit: ").Append(total).Append(" spells, ").Append(groups.Count)
          .Append(" distinct identities, ").Append(colliding).Append(" colliding groups")
          .Append(" (worst ").Append(worstText).Append("), ").Append(authored)
          .Append(" authored profiles. axes = impact+cast+shape+coreRGB@8bit (scale/tempo excluded)");
        _lookAuditText = sb.ToString();
        _lookAuditRun = true;
        Debug.Log("[NewWorldTestGround] " + _lookAuditText);

        sb.Append('\n').Append("  impact families: ").Append(CountLine(byImpact));
        sb.Append('\n').Append("  cast families:   ").Append(CountLine(byCast));
        sb.Append('\n').Append("  body shapes:     ").Append(CountLine(byShape));
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            sb.Append("\n  COLLISION x").Append(kv.Value.Count).Append(':');
            foreach (string member in kv.Value) sb.Append("\n    ").Append(member);
        }
        Debug.Log("[NewWorldTestGround] " + sb.ToString());
    }

    /// <summary>Packs the perceptual axes into one key. 34 bits: 3+3+4 for the enums, 8 per channel.</summary>
    private static ulong LookKey(in SpellLook look)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(look.Core.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(look.Core.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(look.Core.b * 255f), 0, 255);
        return ((ulong)(int)look.Impact << 31)
             | ((ulong)(int)look.Cast << 28)
             | ((ulong)(int)look.DisplayShape << 24)
             | ((ulong)r << 16) | ((ulong)g << 8) | (ulong)b;
    }

    private static string Describe(in SpellLook look)
        => "(" + look.Impact + "/" + look.Cast + "/" + look.DisplayShape + "/"
           + look.Core.r.ToString("F2") + "," + look.Core.g.ToString("F2") + "," + look.Core.b.ToString("F2")
           + (look.Authored ? " AUTHORED)" : ")");

    private static void Bump<TKey>(Dictionary<TKey, int> map, TKey key) where TKey : notnull
    {
        map.TryGetValue(key, out int n);
        map[key] = n + 1;
    }

    private static string CountLine<TKey>(Dictionary<TKey, int> map) where TKey : notnull
    {
        var parts = new List<string>(map.Count);
        foreach (var kv in map) parts.Add(kv.Key + "=" + kv.Value);
        parts.Sort();
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Wire the skill profile + hotkey bindings on the player and grant every skill (testing).
    /// Passives apply their stat effects immediately; castables become hotkey-executable.
    /// </summary>
    private void GrantAllSkills()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        if (player.GetComponent<SkillProfile>() == null)
            player.gameObject.AddComponent<SkillProfile>();
        if (player.GetComponent<SkillBindings>() == null)
            player.gameObject.AddComponent<SkillBindings>();

        // Ensure the combat stack exists so passive stat effects can apply (PlayerStats)
        // and castable skills can spend resources (SpellCaster / StaminaSystem) immediately.
        WeaponRigBuilder.EnsureCombatStack(player.gameObject);

        SkillCatalog.EnsureBuilt();
        var profile = player.GetComponent<SkillProfile>();
        if (profile == null) return;

        // Testing: grant a fat point budget and auto-learn EVERY skill so the whole roster is
        // usable immediately. Prereq gating is satisfied by sweeping the catalog until a full
        // pass learns nothing new (no dependency order needed).
        profile.Points += 999;
        bool learnedAny;
        do
        {
            learnedAny = false;
            var all = SkillCatalog.All;
            if (all == null) break;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || profile.HasLearned(all[i].id)) continue;
                if (profile.Learn(all[i]))
                    learnedAny = true;
            }
        } while (learnedAny);
    }

    /// <summary>
    /// Place EVERY castable magic spell down on the platform's middle band as a static,
    /// school-colored projectile-style model on a pedestal + in-game label, so each spell's magic
    /// model can be looked at and edited on the test ground (1dk). The roster mirrors the
    /// MagicTestMatrix grid: every <see cref="SkillType.Magic"/> skill that isn't passive and casts
    /// a real <see cref="SpellData"/>, grouped by school then display name. Each display reuses the
    /// exact live-cast body builders via <see cref="SpellCaster.CreateProjectileDisplay"/> — Comet /
    /// Earth Meteor show the rough rock body (SummonFallingRock), explicit shapes (Ice Lance, Shadow
    /// Spear, Arcane Missiles...) show their real body, and zone/beam/vortex/storm/summon/instant
    /// spells show their school-colored default icon (those deliveries have no static projectile).
    /// The label uses the world-TMP pattern from the legacy building signs. Pure visuals — pedestals
    /// and bodies get no collider, so the grid stays walkable and nothing is interactable.
    /// </summary>
    private void SpawnMagicModels()
    {
        SkillCatalog.EnsureBuilt();

        var castables = new List<(Skill skill, SpellData spell)>();
        foreach (var skill in SkillCatalog.OfType(SkillType.Magic))
        {
            if (skill == null || skill.IsPassive) continue;
            if (skill.Effect is not SpellCastEffect cast || cast.Spell == null) continue;
            castables.Add((skill, cast.Spell));
        }
        if (castables.Count == 0) return;
        castables.Sort((a, b) =>
        {
            int bySchool = ((int)a.skill.DamageKind).CompareTo((int)b.skill.DamageKind);
            return bySchool != 0 ? bySchool
                : string.CompareOrdinal(a.skill.displayName ?? a.skill.id, b.skill.displayName ?? b.skill.id);
        });

        // Grid across the platform's middle band — clear of the tool kit east / weapon rack west
        // lines (x ±0.42·PlatformSize), the enemy rows + dummies south of z-18, the farming/
        // livestock/buildings band at z+18+, and the NPC row at z-0.18·PlatformSize.
        float spacing = 3f;
        float usable = PlatformSize * 0.78f;
        int columns = Mathf.Clamp(Mathf.FloorToInt(usable / spacing) + 1, 1, Mathf.Max(1, castables.Count));
        int rows = Mathf.CeilToInt(castables.Count / (float)columns);
        float startX = PlatformCenter.x - (columns - 1) * spacing * 0.5f;
        float startZ = PlatformCenter.z + (rows - 1) * spacing * 0.5f;

        for (int i = 0; i < castables.Count; i++)
        {
            Skill skill = castables[i].skill;
            SpellData spell = castables[i].spell;
            int col = i % columns;
            int row = i / columns;

            var cell = new GameObject("MagicModel_" + skill.id);
            cell.transform.position = new Vector3(startX + col * spacing, PlatformTopY + 0.1f,
                startZ - row * spacing);
            Vector3 look = new Vector3(PlatformCenter.x - cell.transform.position.x, 0f,
                PlatformCenter.z - cell.transform.position.z);
            if (look.sqrMagnitude > 0.0001f)
                cell.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Pedestal";
            Collider pcol = pedestal.GetComponent<Collider>();
            if (pcol != null) Destroy(pcol);
            pedestal.transform.SetParent(cell.transform, false);
            pedestal.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            pedestal.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            var pmr = pedestal.GetComponent<MeshRenderer>();
            if (pmr != null)
                pmr.sharedMaterial = SolidMaterial(new Color(0.24f, 0.2f, 0.17f));

            var modelRoot = new GameObject("Model");
            modelRoot.transform.SetParent(cell.transform, false);
            modelRoot.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            // 1ij: hand it the SpellData, not (skill.DamageKind, spell.Shape). The two-arg overload
            // resolves through the identity-less school fallback, so every Fire spell on this bench
            // showed the same body regardless of its own DisplayShape - which is precisely the thing
            // the per-spell bench exists to let you compare. This overload is now the only caller
            // that needs it, which is what makes the deleted DamageType/ProjectileShape version
            // dead. rockBody is read from the spell itself, so the argument is redundant here.
            SpellCaster.CreateProjectileDisplay(spell)
                .transform.SetParent(modelRoot.transform, false);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(cell.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            var tmp = labelGo.AddComponent<TMPro.TextMeshPro>();
            tmp.text = skill.displayName ?? skill.id;
            tmp.fontSize = 1.6f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            // 1ij: the label states which spell you are looking at, so it wears that spell's OWN
            // core colour. The school colour made every spell of a school share a label tint, so the
            // labels could not tell you which identity you were actually looking at - and the body
            // above is now per-spell, so a label that disagrees with its body is worse than useless.
            tmp.color = SpellLook.Resolve(spell).Core;
            tmp.outlineWidth = 0.1f;
            tmp.outlineColor = Color.black;
            tmp.rectTransform.sizeDelta = new Vector3(3f, 0.6f);
        }
    }

    /// <summary>
    /// Equip a representative starter set into the player's 21-slot equipment system (testing),
///     so the humanoid Equipment tab has something to show. Also wires up the ClassUnlocker for
    ///     the Class tab — under its exclusive single-choice model the Wanderer baseline is active
    ///     until the player picks a class.
    /// </summary>
    private void GrantStarterGear()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var equip = player.GetComponent<EquipmentSystem>();
        if (equip == null)
            equip = player.gameObject.AddComponent<EquipmentSystem>();

        GearCatalog.EnsureBuilt();
        string[] kit =
        {
            "leather_helmet", "leather_armor", "leather_gloves", "leather_leggings", "leather_boots",
            "copper_ring", "iron_ring", "silver_ring", "gold_ring", "sapphire_ring",
            "ruby_ring", "emerald_ring", "amethyst_ring", "topaz_ring", "diamond_ring",
            "bronze_necklace", "earring_copper", "earring_silver", "leather_belt"
        };
        foreach (var id in kit)
            equip.Equip(id);

        if (player.GetComponent<ClassUnlocker>() == null)
            player.gameObject.AddComponent<ClassUnlocker>();
    }

    /// <summary>
    /// Wire the player's active-race manager (testing). Game-design §3.5 keeps race choice to ONE
    /// active race, so this grants no unlock-everything roster and no Ritual Stones — the player
    /// starts Human and may change only to Human or an actually-discovered race (via the Character
    /// Info Race tab / RaceDiscoveryPoint), consuming a Ritual Stone when offered.
    /// </summary>
    private void GrantRaceAccess()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var mgr = player.GetComponent<RaceChangeManager>();
        if (mgr == null)
            player.gameObject.AddComponent<RaceChangeManager>();
    }

    /// <summary>
    /// Perf readout (1ea): builds a small screen-space overlay and starts a 0.25s coroutine that
    /// refreshes avg FPS + frame ms + the streamer's loaded chunk count + active collider count.
    /// Deliberately independent of the new-world HUD (opt-in QA toggle) so optimization passes can
    /// be compared in the Editor without a profiler. Read-only; never touches the world or platform.
    /// </summary>
    private void SpawnFpsStats()
    {
        var canvas = HudCanvas.CreateOverlay("BenchStatsCanvas");
        var root = HudCanvas.CreateBackdrop(canvas.transform, "Stats",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(100f, -28f), new Vector2(300f, 126f));
        var label = new GameObject("Label");
        label.transform.SetParent(root, false);
        var rect = label.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 3f);
        rect.offsetMax = new Vector2(-5f, -3f);
        var tmp = label.AddComponent<TMPro.TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = 13f;
        tmp.color = new Color(1f, 1f, 0.8f);
        tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
        tmp.text = "FPS -";
        _fpsText = tmp;
        _fpsCanvas = canvas;
        _fpsAccum = 0f;
        _fpsSamples = 0;
        StartCoroutine(UpdateFpsStats());
    }

    private System.Collections.IEnumerator UpdateFpsStats()
    {
        var wait = new WaitForSecondsRealtime(0.25f);
        while (true)
        {
            yield return wait;

            float fps = 1f / Mathf.Max(Time.deltaTime, 0.0001f);
            _fpsAccum += fps;
            _fpsSamples++;
            float avgFps = _fpsAccum / _fpsSamples;
            if (_fpsSamples >= 40)
            {
                // Slide the rolling average forward instead of growing without bound.
                _fpsAccum = avgFps;
                _fpsSamples = 1;
            }

            int chunks = 0;
            int dormant = 0;
            int colliders = 0;
            int farSectors = 0;
            int rebuilds = 0;
            var streamer = Object.FindAnyObjectByType<WorldStreamer>();
            if (streamer != null)
            {
                var loaded = streamer.LoadedChunks;
                chunks = loaded.Count;
                dormant = streamer.DormantChunkCount;
                farSectors = streamer.FarSectorCount;
                rebuilds = streamer.RebuildPendingCount;
                foreach (var kv in loaded)
                    if (kv.Value != null && kv.Value.HasCollider)
                        colliders++;
            }

            if (_fpsText != null)
            {
                string stats = string.Format("FPS {0:0}  ({1:0.0} ms)\nchunks {2}  dormant {3}  colliders {4}\nfar cells {5}\nrebuilds {6}",
                    avgFps, 1000f / avgFps, chunks, dormant, colliders, farSectors, rebuilds);

                // (1gf) per-stage poll split: which stage eats a sprint crossing. Read the rolling
                // peaks + heavy-poll count, then reset the window so the next refresh shows only the
                // polls since this one. The LOD band sweep runs on the gameplay frame (outside the
                // streamer coroutine), so it is reported separately as its own worst-case.
                if (EnablePollStageStats)
                {
                    var last = streamer != null ? streamer.LastPollStats : WorldStreamer.PollStageStats.Zero;
                    var peak = streamer != null ? streamer.PeakPollStats : WorldStreamer.PollStageStats.Zero;
                    stats += string.Format(
                        "\nlast poll {0:0.00} ms  heavy {1}\n  near {2:0.00}  final {3:0.00}  coll {4:0.00}  props {5:0.00}\n  farScan {6:0.00}  farFinal {7:0.00}  rebrd {8:0.00}  disp {9:0.00}\npeaks {10:0.00} ms  far {11:0.00}  final {12:0.00}",
                        last.TotalMs, streamer != null ? streamer.HeavyPollsSinceLastRead : 0,
                        last.StreamAroundMs, last.FinalizeMs, last.CollidersMs, last.PropsMs,
                        last.FarScanMs, last.FarFinalizeMs, last.RebuildDrainMs, last.DispatchMs,
                        peak.TotalMs, peak.FarMs, peak.FinalizeMs);
                    var lod = Object.FindAnyObjectByType<ChunkLodManager>();
                    if (lod != null)
                        stats += string.Format("\nlod sweep {0:0.00} / {1:0.00} ms", lod.LastSweepMs, lod.PeakSweepMs);
                    streamer?.ResetPollStagePeaks();
                }

                // (1gh) chunk diagnostics: for any "chunk X is invisible for no reason" report this
                // single line says whether it is missing, retained-dormant, hidden (root/inactive),
                // mesh-less, at a LOD band, or under a dead far cell — one screenshot resolves it.
                if (EnableChunkDiagnostics && streamer != null)
                {
                    var tc = new TerrainChunkCoord(ChunkInspectX, ChunkInspectZ);
                    string diag = "\n" + streamer.ChunkDiagnostics(tc);
                    if (streamer.LoadedChunks.TryGetValue(tc, out var co) && co != null)
                    {
                        diag += co.gameObject.activeSelf ? "  active" : "  **[hidden root]**";
                        var mr = co.GetComponent<MeshRenderer>();
                        var mf = co.GetComponent<MeshFilter>();
                        diag += (mr != null && mr.enabled) ? "  ren" : "  renOFF";
                        diag += (mf != null && mf.sharedMesh != null) ? "  mesh+Vtx" : "  noMesh";
                        Transform lod1 = co.transform.Find("Lod1");
                        Transform lod2 = co.transform.Find("Lod2");
                        diag += lod1 != null ? "  lod1:" + (lod1.gameObject.activeSelf ? "on" : "off") : "  lod1:null";
                        diag += lod2 != null ? "  lod2:" + (lod2.gameObject.activeSelf ? "on" : "off") : "  lod2:null";
                        var lod = Object.FindAnyObjectByType<ChunkLodManager>();
                        if (lod != null)
                            diag += "  band " + lod.BandIndexOf(co.gameObject);
                        diag += "  coll " + co.HasCollider + "  dorm " + co.Dormant;
                    }
                    stats += diag;
                }

                // (1hy) The rendered-corner + void report stays up until the next press, so a
                // screenshot taken after walking up to the gap the report named still shows the
                // numbers for the frame the key was pressed on.
                if (EnableCornerAudit && !string.IsNullOrEmpty(_cornerAuditText))
                    stats += "\n" + _cornerAuditText;

                // 1ic: same persistence — the look-collision headline stays up so a screenshot of
                // the number is evidence for the frame the key was pressed on.
                if (EnableLookAudit && !string.IsNullOrEmpty(_lookAuditText))
                    stats += "\n" + _lookAuditText;

                // 1id: SpellImpactFx's per-frame budget REFUSES flashes past PerFrameBudget in one
                // frame and counts the refusals. That counter was write-only until here — the budget
                // is a cap, and a cap cannot be judged from taste (rule 7). Read LIVE every frame,
                // unlike the frozen audit headline above, and on its own line so a rising count is
                // legible against the frame instead of reading as part of the identity number.
                // _lookAuditRun gates it: the audit must have been pressed once for the lane to arm.
                if (EnableLookAudit && _lookAuditRun)
                    stats += "\n" + SpellImpactFx.DroppedSinceLaunch + " impact flashes dropped"
                        + " (budget " + SpellImpactFx.PerFrameBudget + "/frame)";

                _fpsText.text = stats;
            }
        }
    }

    /// <summary>
    /// QA/perf lane (1gd): speed-decoupled renderer. Forces the scene's WorldStreamer onto the
    /// decoupled clock (StreamInUpdate on + DecoupleRenderFromGameplay on) and confirms the streamer
    /// found. Combined with the FPS stats readout (which now also shows the seam-rebuild back-queue)
    /// this is the direct A/B for the "immense lag at high player speed" report: with the toggle on,
    /// moving fast should no longer drag the gameplay frame, and the rebuild count should pulse
    /// through the edited-terrain ring instead of hitches. Config-only — no world placement.
    /// </summary>
    private void ApplySpeedDecoupleSettings()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
        {
            Debug.LogWarning("[NewWorldTestGround] EnableSpeedDecoupleRender: WorldStreamer not found — nothing to configure.");
            return;
        }
        bool wasEnabled = streamer.StreamInUpdate;
        bool wasDecoupled = streamer.DecoupleRenderFromGameplay;
        streamer.StreamInUpdate = true;
        streamer.DecoupleRenderFromGameplay = true;
        Debug.Log($"\n[NewWorldTestGround] Speed-decoupled renderer (1gd) applied: " +
            $"StreamHz={streamer.StreamHz}, DecoupleRenderFromGameplay: {wasDecoupled} -> true, " +
            $"StreamInUpdate: {wasEnabled} -> true. Seam rebuilds now run in the background " +
            "(RebuildPendingCount visible in the FPS readout).");
    }

    private static Material SolidMaterial(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { color = c };
        return mat;
    }
}