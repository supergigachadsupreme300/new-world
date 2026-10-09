using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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
    [Tooltip("1je: mount all THREE summon bodies side by side on the platform's south-west apron - the following-familiar circle, the standing totem, and the SummonedAlly combat construct. Exists because SummonedAlly.Spawn is reachable only from ClassEffect and RaceEffect (2 call sites, both live combat), so without this lane the ally's model has no readout at all: the one visual 1je changed could not be looked at without casting a class skill at an enemy. Mounts the BUILDERS, not proxies (the 1ij/1jd precedent).")]
    public bool EnableSummonModels = false;
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
    [Tooltip("QA/perf (1gf): extend the FPS overlay with the WorldStreamer poll's per-stage ms split (near/finalize/colliders/farManage/farFinalize/props/rebuild drain) + the rolling worst-poll peaks and heavy-poll count, plus the ChunkDistanceCull sweep ms (last + peak). Read-only; needs EnableFpsStats on to display. Lets a long-sprint run show WHICH stage actually eats the gameplay frame instead of guessing. On by default so the baseline is visible; flip off to hide the extra lines.")]
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
    [Tooltip("QA (1gh): extend the FPS overlay with a chunk-diagnostics line for ChunkInspectX/Z — that chunk's real load state (loaded/dormant/absent), root GameObject active, renderer+mesh present, collider, and which far cell owns it (and whether that cell is LIVE or MISSING). For any 'chunk invisible for no reason' report: one screenshot answers whether it is missing, hidden by the distance cull, or under a dead far cell. Read-only; needs EnableFpsStats on to display.")]
    public bool EnableChunkDiagnostics = true;
    [Tooltip("QA (1gh): chunk coords inspected by the diagnostics line (the coords of the reported monster/relic chunk −8_3 in chunk-space, X −8, Z 3).")]
    public int ChunkInspectX = -8;
    [Tooltip("QA (1gh): chunk coords inspected by the diagnostics line (the coords of the reported monster/relic chunk −8_3 in chunk-space, X −8, Z 3).")]
    public int ChunkInspectZ = 3;
    [Tooltip("QA (1hy): press CornerAuditKey for a read-only audit of the terrain the player is actually LOOKING at, for the 'in every chunk corner the edge will not match — the player can see the void through that gap' report. Three sections, asked in the order the questions depend on each other. A fingerprint: more than one (facet step, vertex count) bucket means the resident world was built by two versions of the generator, which voids the other two as evidence. B void: any chunk footprint in the fully-owned ring with neither a visible real chunk nor a live far cell — the only failure mode that opens a real hole in an otherwise watertight chunk set, and the direct test of a visible 'gap'. C corners: does every loaded chunk place a rendered vertex AT each of its four corners, do the chunks meeting at a node agree on the corner height, and does each rendered corner match that chunk's own lattice. The VERDICT line names the first failure and the world XZ to walk to. Read-only — no rebuild, no re-stamp, no forced poll, so it describes the frame the key was pressed on. Needs EnableFpsStats on to display.")]
    public bool EnableCornerAudit = true;
    [Tooltip("QA (1hy): key that runs the rendered-corner + void audit. F3 because F5-F12 are editor cutscene shortcuts and F1 is the combat-mode toggle (not a skill hotkey).")]
    public Key CornerAuditKey = Key.F3;
    private string _cornerAuditText;

    [Tooltip("QA (1ic): press LookAuditKey for a read-only audit of the per-spell visual identity — how many of the spell roster's resolved looks are actually distinguishable. Two spells count as the SAME identity when their impact family, cast family, projectile body shape and core colour (quantised to 8 bits per channel) all match; Scale and Tempo are excluded because they are sub-perceptual, and including them would let the number read 'unique' while two spells look identical. Groups the LIVE rosters (SkillCatalog magic + ClassSkillCatalog), so it measures what the player can actually cast. Read-only — resolves looks and prints, spawns nothing.")]
    public bool EnableLookAudit = true;
    [Tooltip("QA (1ic): key that runs the per-spell look-collision audit. F4: F1 is the combat-mode toggle, F3 is the 1hy corner/void audit, and the F2/F4 lanes 1hx removed were not restored.")]
    public Key LookAuditKey = Key.F4;
    private string _lookAuditText;
    private bool _lookAuditRun;

    [Tooltip("QA/perf (1ik): press FrameBudgetKey for a read-only attribution of WHERE the frame is going - CPU main thread vs render thread vs GPU - plus the draw/batch/triangle counts the render side is paying for, and whether the frame is vsync-pinned (which means the reported FPS is a multiple of the refresh interval rather than a measurement of the work). Sampled continuously and cheaply (a few recorder reads per frame, no allocation, nothing touched); the key snapshots the frames that ran UP TO the press and prints them. Read-only by rule 7: no rebuild, no patch, no forced poll, no setting changed. A timing source that never produced a sample prints 'n/a' rather than 0, because 0 reads as 'that side is free'. Press standing still, read; then walk and press again - two numbers, two conditions, no mode switch.")]
    public bool EnableFrameBudgetAudit = true;
    [Tooltip("QA/perf (1ik): key that snapshots the frame-budget window. F2, and the choice is grep-verified rather than inherited: F2 has zero references anywhere in Assets\\Scripts and was vacated when 1hx removed its lane without restoring it; F3 is the 1hy corner/void audit, F4 is the 1ic look audit, F5 is the CameraModeSwitch camera toggle (Player\\CameraModeSwitch.cs:120), and F6-F12 are editor cutscene shortcuts. The old claim 'F1 is a skill hotkey' was incorrect: F1 is the combat-mode toggle (PlayerController.Interactions.cs:521).")]
    public Key FrameBudgetKey = Key.F2;
    private string _frameBudgetText;

    [Tooltip("QA (1in): press CraterAuditKey for a read-only measurement of a terrain dent - what the crater is actually made of, asked premise-first. A fingerprint: if the resident world was built by two versions of the generator, the sections below describe a world that is not on screen. B resolution: for the deepest dished corner near you, how deep it is, how many 1 m corners it spans, and the render path's worst-case node gap (rule 12) - this is the number that decides whether the dent exists at all on screen. C profile: the radial depth profile ring by ring, so a monotone cone and a bowl-with-rim are distinguishable as numbers rather than by eye. D expressibility: the largest corner spread against the adaptive-refinement trigger, how many corners rose ABOVE pristine (a rim), and whether any whole-metre discontinuity exists (side walls) - i.e. whether the shape could exist in the data model at all. Search band and its radius are printed; corners with no loaded tile are excluded, so a zero means 'not measured here', never 'no crater'. Read-only by rule 7: no rebuild, no re-stamp, no forced poll. Needs EnableFpsStats on to display.")]
    public bool EnableCraterAudit = true;
    [Tooltip("QA (1io): key that runs the crater/deform audit. F13, and the choice is CHECKED rather than grep-inherited. 1in shipped this lane on F1 after grepping for 'Key.F1' and 'KeyCode.F1', found nothing, and concluded the key was free. It was not: the combat-mode toggle binds it as 'Keyboard.current.f1Key' (Player\\PlayerController.Interactions.cs:521), which is the property-name spelling and matches neither pattern. So pressing F1 ran the audit AND toggled fighting mode - weapons drew and ToolManager reset selection mid-measurement. The 'F1 is a skill hotkey' claim in the F3/F4/F2 tooltips was simply wrong; F1 is the combat toggle. F13 has zero bindings in either spelling (see tools\\StaticChecks.ps1 check 8, which now enforces this for every lane key). F2 is the 1ik frame-budget lane, F3 the 1hy corner/void audit, F4 the 1ic look audit, F5 the CameraModeSwitch camera toggle, F6-F12 the editor cutscene shortcuts; 1jq CORRECTED this tooltip's 'Numpad0-9 are free apart from numpadEnter' claim: it had silently stopped being true when 1je took Numpad1 for the summon-model lane, so it was asserting a free key that was already bound. The measured map as of 1jq: Numpad1 is 1je's summon-model lane, numpadEnter is the ending cutscene (CutsceneManager.EndingHappy.cs), and the free Numpads are 0 and 2-9, of which 1jq took Numpad8.")]
    public Key CraterAuditKey = Key.F13;
    private string _craterAuditText;

    [Tooltip("QA (1jq): key that spawns the summon-model lane. Numpad1, and the choice is CHECKED the same way F13's was: 'Key.Numpad1', '.numpad1Key' and '[Key.Numpad1]' all read 0 in Assets\\Scripts, with F1 as the positive control proving the property-name spelling is actually being searched. Note this lane then TOOK Numpad1, so the F13 tooltip's 'Numpad0-9 are free' below is stale - 1jq corrected it in place.")]
    public Key SummonModelKey = Key.Numpad1;
    private readonly List<GameObject> _summonModelCells = new List<GameObject>();

    [Tooltip("QA/perf (1jq): press TrailAuditKey for a read-only count of the in-flight projectile trail strips - how many exist, how many of those were actually RENDERED, and their segment/vertex/triangle totals. This is the acceptance readout for the voxel -> strip change: the proposal was 'this costs less', and no other lane in this file reports component or draw counts (1ik's F2 reports CPU/GPU milliseconds, not counts). Strips-1-with-visible-0 names the mechanism - a strip that built but did not draw is a mesh-bounds or missing-camera fault, not a colour problem. Prints a known-zero control beside every count, because a count never shown able to report 0 is not a count. Read-only by rule 7: counts what is in the scene on the frame the key was pressed, spawns nothing and changes nothing. It CANNOT prove the strip is flush with the ground or how the tail looks - drawn and flush are separate properties, so the tail read is a play-test item, not a number. Needs EnableFpsStats on to display.")]
    public bool EnableTrailAudit = true;
    [Tooltip("QA/perf (1jq): key that snapshots the trail-strip counts. Numpad8, chosen the way F13's and Numpad1's were - by grepping all three Input System spellings ('Key.Numpad8', '.numpad8Key', '[Key.Numpad8]') across Assets\\Scripts and confirming zero hits, with F1 as the positive control proving the property-name spelling really is searched. The full map as of 1jq: F1 is the combat-mode toggle (PlayerController.Interactions.cs:521), F2 is this file's frame-budget lane, F3 the 1hy corner/void audit, F4 the 1ic look audit, F5 the CameraModeSwitch toggle, F6-F12 editor cutscene shortcuts (GameManager.cs), F13 the 1io crater audit, Numpad1 the 1je summon-model lane, numpadEnter the ending cutscene. The free Numpads as of 1jv are 0 and 3-9: 1je took Numpad1, 1jq took Numpad8, 1jv took Numpad2 (1jv corrected this sentence's older 'Numpad2-Numpad9 are free' in place). See tools\\StaticChecks.ps1 check 8, which enforces the no-double-binding half of this for every lane key.")]
    public Key TrailAuditKey = Key.Numpad8;
    private string _trailAuditText;

    // ---------------------------------------------------------------------------------------------
    // (1jv) Camera-boom audit: is the camera's distance from the player changing, and what drives it.
    //
    // WHY A LANE AND NOT A FIX: the report was "the camera is continuously bugging when moving" plus
    // "snaps in and out / zooms, everywhere even on flat open ground". Two mechanisms were derivable
    // from the code and they need different fixes, and the first hypothesis (the collision clamp)
    // was FALSIFIED by the answer - the boom rises 1.1 m over 6.65 m, so it cannot hit flat ground.
    // The survivors are the position smoother changing the boom's EFFECTIVE length with movement
    // direction, and the boom swinging with mouse yaw. Both make the distance move, so the number
    // alone cannot tell them apart - the CONTROLS do. Hence rule 7's measure-first.
    //
    // WHAT MAKES THE VERDICT TRUSTWORTHY: the sections are ordered by whether their PREMISE holds.
    // In first person this camera snaps to the pivot and cannot zoom at all, so every distance below
    // would be describing a camera that is not the boom - the mode check runs FIRST and the rest of
    // the report is void without it. The window and its length are printed beside every number, so a
    // zero means "not measured over these frames", never "did not happen".
    //
    // READ-ONLY (rule 7): it samples transforms and two numbers CameraModeSwitch publishes. It does
    // not rebuild, re-stamp, force a poll, teleport the player, or write anything the camera reads.
    [Tooltip("QA (1jv, extended 1ks): press BoomAuditKey for a read-only measurement of the third-person camera's boom - is the camera's distance from the player actually changing while moving, and what drives it. Reported as 'the camera is continuously bugging when moving' and 'snaps in and out / zooms, everywhere even on flat open ground'. Section A checks the PREMISE (third person + this camera), because in first person the camera snaps to the pivot and every later number would describe a camera that cannot zoom. Section B is the control: the player's speed and mouse-yaw total over the window, because a distance that moves only while the player is turning is the boom swinging, which is expected, not a defect. Section C is the measurement: measured camera-to-pivot distance against the boom's own two published lengths (rest and post-collision) - a shorter applied length is the collision clamp, a held length with a growing distance is the position smoother trailing past the boom's end. Section C-hitters (1ks) NAMES the collider each clamped frame was clamped by, from CameraModeSwitch's own cast, with the nearest distance - a name whose nearest distance sits at the ~0.8 m pad floor is a start-overlap (geometry glued to the player's back), which is the 'zoom up to behind the head' signature; a name at several metres is a real obstruction behind the player. Read-only by rule 7: samples transforms and reads published values, spawns nothing, changes nothing, and reports the window that ended on the frame the key was pressed. Needs EnableFpsStats on to display.")]
    public bool EnableBoomAudit = true;
    [Tooltip("QA (1jv): key that reports the third-person camera boom readout. Numpad2, chosen the way F13's / Numpad1's / Numpad8's were - by grepping all three Input System spellings ('Key.Numpad2', '.numpad2Key', '[Key.Numpad2]') across Assets\\Scripts and confirming zero hits, with F1 as the positive control proving the property-name spelling really is searched. The full map as of 1jv: F1 is the combat-mode toggle (PlayerController.Interactions.cs:521), F2 the 1ik frame-budget lane, F3 the 1hy corner/void audit, F4 the 1ic look audit, F5 the CameraModeSwitch toggle, F6-F12 editor cutscene shortcuts (GameManager.cs), F13 the 1io crater audit, Numpad1 the 1je summon-model lane, Numpad2 is this lane, Numpad8 the 1jq trail lane, numpadEnter the ending cutscene. Numpad0 and Numpad3-Numpad9 are the free Numpads (this lane took Numpad2; the trail tooltip's older 'Numpad2-Numpad9 are free' was corrected in place in the same commit). See tools\\StaticChecks.ps1 check 8, which enforces the no-double-binding half of this for every lane key.")]
    public Key BoomAuditKey = Key.Numpad2;
    private string _boomAuditText;

    // Trailing window for the boom readout. A single frame cannot catch a transient, and 'snaps in
    // and out' IS a transient, so the trackers are fed every frame and the key only READS the
    // trailing window - the report describes the frames that ran UP TO the press (same convention
    // as the 1ik frame-budget lane). 120 frames is ~2 s at 60 fps: long enough to contain a couple of
    // direction changes while running, short enough that the player need not keep running.
    private const int BoomWindow = 120;
    private readonly float[] _boomDist = new float[BoomWindow];
    private readonly float[] _boomSpeed = new float[BoomWindow];
    private readonly float[] _boomRest = new float[BoomWindow];
    private readonly float[] _boomApplied = new float[BoomWindow];
    private readonly bool[] _boomClamped = new bool[BoomWindow];
    private readonly float[] _boomYaw = new float[BoomWindow];
    // 1ks: the hitter recorded per frame, from CameraModeSwitch's own cast (never re-derived here).
    // Only meaningful on a clamped frame; the snapshot tallies these to name the clamp's mechanism.
    private readonly string[] _boomHitName = new string[BoomWindow];
    private readonly int[] _boomHitLayer = new int[BoomWindow];
    private readonly float[] _boomHitDist = new float[BoomWindow];
    private Vector3 _boomLastPivot;
    private bool _boomSeen;
    private PlayerController _boomOwner;
    private CameraModeSwitch _boomMode;
    private int _boomHead;
    private int _boomFilled;

    // ---------------------------------------------------------------------------------------------
    // (1ik) Frame-budget attribution lane: a continuous passive sampler plus one snapshot key.
    //
    // WHY A WINDOW AND NOT THE SINGLE FRAME: a frame time over one frame is noise, and a CPU/GPU
    // split is only meaningful as a distribution. So the sampler runs every frame and the KEY is the
    // snapshot boundary - the report describes the frames that ran UP TO the press, which is what
    // keeps rule 7's "describe the frame the key was pressed on" intact without pretending one frame
    // is a measurement.
    //
    // WHY THE WINDOW RESETS ON PRESS rather than being a true ring buffer: a ring buffer needs every
    // field copied out per frame; resetting gives the same comparison for the workflow that matters
    // (stand, read; walk, read) at a fraction of the cost. The frame count is printed with the
    // result, because a reader who does not know the window's width cannot read the average.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Frames in the snapshot window. At the 15-60 fps this lane exists to diagnose, 90
    /// frames is 1.5-6 s: long enough to stop chasing a single hitch, short enough that one walk
    /// between two presses still fits inside a window.</summary>
    private const int FrameBudgetWindow = 90;

    /// <summary>A side must own at least this share of the WORK before the verdict names it.
    /// Deliberately conservative: the test compares two recorder readings whose accuracy is unknown
    /// in the Editor, so a threshold near 0.5 would name a mechanism for arithmetic noise (rule 7 -
    /// gate a classifier on the width of its own test). Below this the verdict says "no single side"
    /// instead of picking the larger of two close numbers.</summary>
    private const float FbDominantShare = 0.6f;

    private int _fbFrames;
    private float _fbFrameSum, _fbFrameMax;
    private float _fbCpuSum, _fbMainSum, _fbRenderSum, _fbGpuSum, _fbGpuFtSum;
    private int _fbCpuN, _fbMainN, _fbRenderN, _fbGpuN, _fbGpuFtN;
    private float _fbDrawSum, _fbBatchSum, _fbSetPassSum, _fbTriSum;
    private bool _fbCountedDraws;

    private ProfilerRecorder _fbTotalCpu, _fbMainThread, _fbRenderThread;
    private ProfilerRecorder _fbGpuTime, _fbDrawCalls, _fbBatches, _fbSetPass, _fbTriangles;
    private readonly FrameTiming[] _fbFrameTimings = new FrameTiming[1];

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

        // (1ik) Frame-budget sampler's recorders. Opened here so the window that F5 later prints
        // covers real play, not the keypress. StartNew returns an INVALID recorder when the marker
        // does not exist in this build/platform (there is no "Render Thread" marker under
        // single-threaded player settings, and "GPU Frame Time" needs GPU profiling support, which
        // the Editor does not always provide). That is not an error to throw on: the lane's whole
        // job is to say which sides it can and cannot see, so an invalid recorder is carried through
        // to the report as "n/a" and counted, never as a zero.
        if (EnableFrameBudgetAudit)
        {
            _fbTotalCpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Total CPU", FrameBudgetWindow);
            _fbMainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", FrameBudgetWindow);
            _fbRenderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", FrameBudgetWindow);
            _fbGpuTime = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", FrameBudgetWindow);
            _fbDrawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", FrameBudgetWindow);
            _fbBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", FrameBudgetWindow);
            _fbSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", FrameBudgetWindow);
            _fbTriangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", FrameBudgetWindow);
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
        if (EnableSummonModels) { RunSafely("summon models", SpawnSummonModels); yield return null; }
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

    /// <summary>
    /// (1ik) Read one probe value in MILLISECONDS from a profiler recorder, or -1 when the recorder
    /// could not see it. Counters (draw calls, triangles) are returned as their raw value, so the
    /// unit is decided by the marker's own declared unit rather than by which field is being read —
    /// hard-coding "divide by 1e6" per call site would be six independent spellings of one fact
    /// (rule 8), and the six would drift the first time a marker changed units.
    /// </summary>
    private static double FbProbeMs(ProfilerRecorder rec)
    {
        if (!rec.Valid) return -1d;
        long last = rec.LastValue;
        if (rec.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds)
        {
            // A VALID recorder that has never been filled still returns 0, and 0 ms of CPU or GPU work
            // is not a measurement — it is a source that produced no sample. Reporting it as 0 would
            // print "this side costs nothing" for the side the Editor cannot see, which is the exact
            // misreading that sends the next reader to the wrong place (rule 7: a positive result has
            // to mean something). Time sources therefore treat 0 as n/a.
            if (last <= 0) return -1d;
            return last * 1e-6;
        }
        // COUNTS are the opposite case: 0 draw calls in a frame is a real, meaningful number, so a
        // zero is passed through untouched. Treating it as "unavailable" would delete the one frame
        // that proves nothing is being drawn.
        return last;
    }

    /// <summary>
    /// (1ik) Accumulate one frame into the snapshot window. Read-only and allocation-free: recorder
    /// reads, a handful of adds, no scene walk, no allocation, nothing touched. Deliberately does NOT
    /// enumerate renderers to count them — <c>FindObjects</c>-style walks cost real milliseconds on a
    /// world with ~380 chunks and ~1224 far cells, and a probe that perturbs the frame it measures
    /// cannot be quoted as a frame cost. Draw/batch/triangle counts come from the render profiler's
    /// own per-frame counters instead, which is both cheaper and the number Unity actually bills.
    /// </summary>
    private void SampleFrameBudget()
    {
        // Capture first, read after: FrameTimingManager fills the ring during the frame, so this is
        // the only place in the method where ordering matters.
        FrameTimingManager.CaptureFrameTimings();

        float frameMs = Time.unscaledDeltaTime * 1000f;
        _fbFrames++;
        _fbFrameSum += frameMs;
        if (frameMs > _fbFrameMax) _fbFrameMax = frameMs;

        double cpu = FbProbeMs(_fbTotalCpu);
        if (cpu >= 0d) { _fbCpuSum += (float)cpu; _fbCpuN++; }
        double main = FbProbeMs(_fbMainThread);
        if (main >= 0d) { _fbMainSum += (float)main; _fbMainN++; }
        double render = FbProbeMs(_fbRenderThread);
        if (render >= 0d) { _fbRenderSum += (float)render; _fbRenderN++; }

        // Two independent GPU sources on purpose. "GPU Frame Time" is a profiler marker that needs
        // GPU profiling support (commonly absent in the Editor), while FrameTimingManager reports the
        // GPU's own submission timestamp. Printing both means a disagreement is visible instead of
        // silently averaging into a number nobody can source.
        double gpu = FbProbeMs(_fbGpuTime);
        if (gpu >= 0d) { _fbGpuSum += (float)gpu; _fbGpuN++; }
        if (FrameTimingManager.GetLatestTimings(1, _fbFrameTimings) > 0)
        {
            float gpuFt = (float)_fbFrameTimings[0].gpuFrameTime;
            if (gpuFt > 0f) { _fbGpuFtSum += gpuFt; _fbGpuFtN++; }
        }

        // Render-work counters. Counted together so the printed row cannot mix a triangle average
        // from one window with a draw-call average from another.
        double draws = FbProbeMs(_fbDrawCalls);
        if (draws >= 0d)
        {
            _fbCountedDraws = true;
            // Each counter is gated on its OWN validity, not on draws being visible: the four markers
            // can be independently absent (e.g. setpass/batches stripped in some players), and
            // accumulating -1 for a missing marker would silently drag the averages negative.
            _fbDrawSum += (float)draws;
            double batches = FbProbeMs(_fbBatches);
            if (batches >= 0d) _fbBatchSum += (float)batches;
            double setPass = FbProbeMs(_fbSetPass);
            if (setPass >= 0d) _fbSetPassSum += (float)setPass;
            double tris = FbProbeMs(_fbTriangles);
            if (tris >= 0d) _fbTriSum += (float)tris;
        }
    }

    /// <summary>
    /// (1ik) Print the window, then clear it. Section order follows rule 7 — cheapest check first,
    /// and PREMISE before measurement:
    /// <list type="number">
    /// <item>A. the clamp: is the reported frame time a multiple of the refresh interval? If so the
    /// headline FPS is a presentation quantum, not a measurement, and every share below it is
    /// meaningless — a frame can only ever be N presents long.</item>
    /// <item>B. the fork: which side owns the work (CPU main / CPU render / GPU), with an explicit
    /// n/a per source so "could not measure" is never printed as 0.</item>
    /// <item>C. the owners: what the render side is being billed for, plus the render settings that
    /// govern it — so a fix has a lever to move, not just a number to admire.</item>
    /// <item>VERDICT: one line naming the side, or saying that no single side owns the frame.</item>
    /// </list>
    /// </summary>
    private void SnapshotFrameBudget()
    {
        int frames = _fbFrames;
        if (frames <= 0)
        {
            _frameBudgetText = "budget: no frames sampled yet — press again after the world has run";
            return;
        }

        float inv = 1f / frames;
        float frameAvg = _fbFrameSum * inv;
        float cpuAvg = _fbCpuN > 0 ? _fbCpuSum / _fbCpuN : -1f;
        float mainAvg = _fbMainN > 0 ? _fbMainSum / _fbMainN : -1f;
        float renderAvg = _fbRenderN > 0 ? _fbRenderSum / _fbRenderN : -1f;
        float gpuAvg = _fbGpuN > 0 ? _fbGpuSum / _fbGpuN : -1f;
        float gpuFtAvg = _fbGpuFtN > 0 ? _fbGpuFtSum / _fbGpuFtN : -1f;

        var sb = new System.Text.StringBuilder(256);
        sb.Append("budget: ").Append(frames).Append(" frames  avg ").Append(frameAvg.ToString("0.0"))
          .Append(" ms  max ").Append(_fbFrameMax.ToString("0.0"))
          .Append("  (fps ").Append(frameAvg > 0.01f ? (1000f / frameAvg).ToString("0") : "-").Append(")");

        // --- A. The clamp. A vsync'd frame can only be a whole number of presents long, so a frame
        // sitting at 2x or 3x the refresh interval is not "33 ms of work" — it is "more than one
        // present interval of work, quantised". The bracket below is the honest reading: the work is
        // somewhere in (one interval, this frame], and the slack is up to one interval wide.
        float refresh = (float)Screen.currentResolution.refreshRateRatio.value;
        int vsync = QualitySettings.vSyncCount;
        int targetFps = Application.targetFrameRate;
        float interval = refresh > 1f ? 1000f / refresh : 0f;
        sb.Append("\nclamp: vsync ").Append(vsync).Append(" @ ").Append(refresh.ToString("0"))
          .Append(" Hz  targetFps ").Append(targetFps);
        if (vsync > 0 && interval > 0.01f)
        {
            float presents = frameAvg / interval;
            sb.Append("  -> ").Append(presents.ToString("0.0")).Append(" presents/frame");
            sb.Append(presents > 1.05f
                ? "  WORK IS NOT this number: true cost is in (" + (presents - 1f).ToString("0.0") + "x interval, " + frameAvg.ToString("0.0") + "]"
                : "  (on the refresh beat)");
        }

        // --- B. The fork. "n/a" is spelled out for every source, with the sample count, because a
        // bare 0 in a timing column is indistinguishable from "that side costs nothing" — the exact
        // reading that would send the next reader looking in the wrong place.
        sb.Append("\n  cpu  main ").Append(Fb(mainAvg)).Append("  render ").Append(Fb(renderAvg))
          .Append("  total ").Append(Fb(cpuAvg))
          .Append("   [n ").Append(_fbMainN).Append('/').Append(_fbRenderN).Append('/').Append(_fbCpuN).Append("]");
        sb.Append("\n  gpu  profiler ").Append(Fb(gpuAvg)).Append("  frameTiming ").Append(Fb(gpuFtAvg))
          .Append("   [n ").Append(_fbGpuN).Append('/').Append(_fbGpuFtN).Append("]");

        // --- C. The owners the render side is billed for, and the settings that govern them. A
        // number with no lever attached cannot be acted on, so shadow distance / resolution scale /
        // MSAA travel with the counts.
        if (_fbCountedDraws)
        {
            sb.Append("\n  draw ").Append((_fbDrawSum * inv).ToString("0"))
              .Append("  batches ").Append(Fb(_fbBatchSum * inv))
              .Append("  setpass ").Append(Fb(_fbSetPassSum * inv))
              .Append("  tris ").Append(Fb(_fbTriSum * inv / 1000f)).Append('k');
        }
        bool haveScale = FbRenderScale(out float renderScale);
        sb.Append("\n  settings  shadowDist ").Append(QualitySettings.shadowDistance.ToString("0"))
          .Append("  shadowRes ").Append(QualitySettings.shadowResolution)
          .Append("  aa ").Append(QualitySettings.antiAliasing)
          .Append(haveScale
              ? "  renderScale " + renderScale.ToString("0.00")
              : "  renderScale n/a (active pipeline is not URP / asset unreadable)");

        // --- The verdict. The clamp state from section A DECIDES which comparison is legitimate,
        // because the two questions are different numbers:
        //   - unclamped: a share of the frame is a share of the work, so work/frame is meaningful.
        //   - clamped:   the frame is quantised, so work/frame UNDERSTATES the cause. 20 ms of work
        //                behind a 33.3 ms frame is a full extra present, yet reads as only 0.60 —
        //                and a 0.5-threshold on it would refuse to name the side that did it.
        // So dominance is measured between the SIDES (which needs no reference frame and so is
        // clamp-proof), and the residual is interpreted rather than absorbed: under a clamp, up to
        // one interval of slack is expected and must NOT be reported as unexplained work, while
        // unclamped it is a real mystery and stays visibly unexplained.
        float cpuSide = cpuAvg > 0f ? cpuAvg : 0f;
        float gpuSide = gpuAvg > 0f ? gpuAvg : gpuFtAvg > 0f ? gpuFtAvg : 0f;
        float sideSum = cpuSide + gpuSide;
        float work = sideSum > 0f ? (cpuSide > gpuSide ? cpuSide : gpuSide) : -1f;
        bool gpuOwner = gpuSide > cpuSide;
        bool clamped = vsync > 0 && interval > 0.01f && frameAvg > interval * 1.05f;

        if (work > 0f)
        {
            float shareOfWork = sideSum > 0f ? work / sideSum : 0f;
            float residual = frameAvg - work;
            sb.Append("\n  work ").Append(work.ToString("0.0")).Append(" ms  residual ").Append(residual.ToString("0.0")).Append(" ms");
            sb.Append(clamped
                ? "  (residual is CLAMP SLACK, not mystery work: one interval = " + interval.ToString("0.0") + " ms)"
                : "  (unclamped: residual is unexplained work)");

            sb.Append("\nVERDICT: ");
            if (shareOfWork < FbDominantShare)
            {
                sb.Append("BOTH SIDES COMPARABLE (cpu ").Append(cpuSide.ToString("0.0"))
                  .Append(" vs gpu ").Append(gpuSide.ToString("0.0"))
                  .Append(") — the split is the story, not one side.");
            }
            else if (clamped)
            {
                // The dominant side is named for exceeding ONE interval, which is the mechanism a
                // clamped frame actually has. 20 ms behind a 33.3 ms frame is a whole lost present,
                // and this is the sentence that says so instead of reporting 0.60 of a quantised total.
                sb.Append(gpuOwner ? "GPU" : "CPU").Append(" work (").Append(work.ToString("0.0"))
                  .Append(" ms) alone exceeds the ").Append(interval.ToString("0.0"))
                  .Append(" ms interval, so the frame is a clamped multiple");
                if (!gpuOwner)
                {
                    if (mainAvg > 0f && renderAvg > 0f && mainAvg >= (mainAvg + renderAvg) * FbDominantShare)
                        sb.Append(" — driven by the MAIN THREAD (").Append(mainAvg.ToString("0.0")).Append(" ms)");
                    else if (mainAvg > 0f && renderAvg > 0f)
                        sb.Append(" — main ").Append(mainAvg.ToString("0.0")).Append(" ms vs render ")
                          .Append(renderAvg.ToString("0.0")).Append(" ms, split between both");
                }
                sb.Append(". That is what misses the present.");
            }
            else if (gpuOwner)
            {
                sb.Append("GPU owns the frame (").Append(work.ToString("0.0")).Append(" ms of ")
                  .Append(frameAvg.ToString("0.0")).Append(").");
            }
            else
            {
                sb.Append("CPU owns the frame (").Append(work.ToString("0.0")).Append(" ms of ")
                  .Append(frameAvg.ToString("0.0")).Append(")");
                // Name WHICH cpu: a render-thread-dominant frame is a completely different problem
                // from a main-thread one, and a bare "CPU" would hide that distinction.
                if (mainAvg > 0f && renderAvg > 0f)
                {
                    float mainShare = mainAvg / (mainAvg + renderAvg);
                    if (mainShare >= FbDominantShare)
                        sb.Append(" — MAIN THREAD ").Append(mainAvg.ToString("0.0")).Append(" ms.");
                    else if (mainShare <= 1f - FbDominantShare)
                        sb.Append(" — RENDER THREAD ").Append(renderAvg.ToString("0.0"))
                          .Append(" ms vs main ").Append(mainAvg.ToString("0.0")).Append(" ms.");
                    else
                        sb.Append(" — main ").Append(mainAvg.ToString("0.0")).Append(" ms vs render ")
                          .Append(renderAvg.ToString("0.0")).Append(" ms, split between both.");
                }
                else
                    sb.Append(".");
            }
        }
        else
        {
            sb.Append("\nVERDICT: CANNOT SPLIT — no CPU or GPU source produced a sample in this session. ")
              .Append("This is a measurement gap, not a fast frame: the counters the Editor exposes did not report. ")
              .Append("Do NOT read the absent columns as 0 ms.");
        }

        _frameBudgetText = sb.ToString();
        Debug.Log("[NewWorldTestGround] " + _frameBudgetText.Replace("\n", " | "));

        // Clear for the next window: press, walk, press.
        _fbFrames = 0;
        _fbFrameSum = 0f; _fbFrameMax = 0f;
        _fbCpuSum = _fbMainSum = _fbRenderSum = _fbGpuSum = _fbGpuFtSum = 0f;
        _fbCpuN = _fbMainN = _fbRenderN = _fbGpuN = _fbGpuFtN = 0;
        _fbDrawSum = _fbBatchSum = _fbSetPassSum = _fbTriSum = 0f;
        _fbCountedDraws = false;
    }

    /// <summary>Format a millisecond probe for the report: a real average, or an explicit "n/a" when
    /// the source was never visible. Never prints a bare 0 for a missing source.</summary>
    private static string Fb(float ms) => ms >= 0f ? ms.ToString("0.0") : "n/a";

    /// <summary>
    /// (1ik) Render scale, read from the ACTIVE pipeline asset rather than from QualitySettings.
    /// <para>
    /// Unity has no <c>QualitySettings.renderScale</c> — that is a CS0117 the first build of this lane
    /// caught. Render scale lives on the render-pipeline asset: URP's
    /// <c>UniversalRenderPipelineAsset.renderScale</c>. Reading it off QualitySettings is not just a
    /// wrong member, it is the wrong OBJECT: this project ships two quality levels with two different
    /// pipeline assets (PC = 1.0, Mobile = 0.8), so a single hard-coded number would report the wrong
    /// one on half the quality levels. Reading it from the live asset means the lane reports the value
    /// that is actually in force.
    /// </para>
    /// <para>
    /// The URP type is reached by REFLECTION, deliberately, for two reasons. First, this file is a QA
    /// lane and URP is not otherwise referenced anywhere in the project (grep: zero hits before this
    /// line), so a direct type reference would add an assembly dependency to the test platform that
    /// exists only to print one diagnostic number. Second, reflection degrades honestly: if the
    /// pipeline is not URP, or the asset cannot be read, the probe returns false and the report prints
    /// "n/a" rather than a fabricated value — the same absent-vs-zero discipline the probe uses
    /// everywhere else (rule 7).
    /// </para>
    /// </summary>
    private static bool FbRenderScale(out float value)
    {
        value = -1f;
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null) return false;
        // Walk to the BASE asset: a quality level may point at a renderer data sub-asset rather than
        // the pipeline asset itself, and only the pipeline asset carries renderScale.
        object asset = rp;
        while (asset != null)
        {
            var t = asset.GetType();
            var prop = t.GetProperty("renderScale");
            if (prop != null && prop.PropertyType == typeof(float))
            {
                value = (float)prop.GetValue(asset);
                return value > 0f;
            }
            var baseProp = t.GetProperty("baseScriptableObject");
            if (baseProp == null) return false;
            asset = baseProp.GetValue(asset);
        }
        return false;
    }

    private void OnDisable()
    {
        // (1ik) ProfilerRecorder is an IDisposable holding native counters; letting the play-mode
        // teardown drop it without Dispose leaks the recorder for the lifetime of the domain. Each is
        // gated on Valid so a lane that never opened them (toggle off) still tears down cleanly.
        FbDispose(ref _fbTotalCpu);
        FbDispose(ref _fbMainThread);
        FbDispose(ref _fbRenderThread);
        FbDispose(ref _fbGpuTime);
        FbDispose(ref _fbDrawCalls);
        FbDispose(ref _fbBatches);
        FbDispose(ref _fbSetPass);
        FbDispose(ref _fbTriangles);
    }

    private static void FbDispose(ref ProfilerRecorder rec)
    {
        if (rec.Valid) rec.Dispose();
    }

    private void Update()
    {
        // (1ik) Sampled FIRST, before any of the early returns below, because a passive sampler that
        // an unrelated `return` can skip is not a sampler — it is a counter that quietly stops, and a
        // stopped counter reports an average over the frames it happened to catch.
        if (EnableFrameBudgetAudit)
        {
            SampleFrameBudget();
            Keyboard kbBudget = Keyboard.current;
            if (kbBudget != null && kbBudget[FrameBudgetKey] != null && kbBudget[FrameBudgetKey].wasPressedThisFrame)
                SnapshotFrameBudget();
        }

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

        // (1in) The crater/deform audit. Polled here for the same reason as the lanes above: this
        // Update returns early on weapon-rack and GamePaused conditions, so a lane placed after
        // them could silently report nothing. Stand next to a dent and press.
        if (EnableCraterAudit)
        {
            Keyboard kbCrater = Keyboard.current;
            if (kbCrater != null && kbCrater[CraterAuditKey] != null
                && kbCrater[CraterAuditKey].wasPressedThisFrame)
                RunCraterAudit();
        }

        // 1je: summon-model lane. Polled here, above the weapon-rack / GamePaused early returns,
        // for the same reason as the lanes above - a lane placed after them could report nothing.
        if (EnableSummonModels)
        {
            Keyboard kbSummon = Keyboard.current;
            if (kbSummon != null && kbSummon[SummonModelKey] != null
                && kbSummon[SummonModelKey].wasPressedThisFrame)
                SpawnSummonModels();
        }

        // 1jq: the trail-strip count lane. Polled with the others, above the early returns, for the
        // same reason - a lane after them could report nothing. Goes through RunSafely like every
        // other lane so a fault in the audit surfaces as a named failure instead of a dead frame.
        if (EnableTrailAudit)
        {
            Keyboard kbTrail = Keyboard.current;
            if (kbTrail != null && kbTrail[TrailAuditKey] != null
                && kbTrail[TrailAuditKey].wasPressedThisFrame)
                RunSafely("trail audit", SnapshotTrailAudit);
        }

        // 1jv: the camera-boom lane. The TRACKER runs every frame (the key only reads its window) and
        // both sit above the early returns, with the other lanes, for the same reason - a lane below
        // them could report nothing. RunSafely, like every other lane here.
        if (EnableBoomAudit)
        {
            TrackBoomFrame();
            Keyboard kbBoom = Keyboard.current;
            if (kbBoom != null && kbBoom[BoomAuditKey] != null
                && kbBoom[BoomAuditKey].wasPressedThisFrame)
                RunSafely("boom audit", SnapshotBoomAudit);
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
    /// QA (1in): run the crater/deform audit and cache it on the HUD.
    ///
    /// <para>The report is multi-line, so it goes to the console verbatim (that is where a table of
    /// per-ring numbers can be read) and a one-line headline is cached for the HUD, matching how the
    /// other multi-section lanes behave. It is read-only inside the streamer - nothing is rebuilt,
    /// re-stamped or polled - so the numbers describe exactly the frame the key was pressed on.</para>
    /// </summary>
    private void RunCraterAudit()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
        {
            _craterAuditText = "crater audit: no WorldStreamer in the scene";
            Debug.LogWarning("[NewWorldTestGround] " + _craterAuditText);
            return;
        }

        string report = streamer.CraterAudit();
        // HUD gets the headline only: the full report is a per-ring table that would bury every other
        // line on the diagnostics strip. The console keeps all of it.
        string headline = report;
        int nl = report.IndexOf('\n');
        if (nl >= 0) headline = report.Substring(0, nl);
        int verdict = report.IndexOf("VERDICT ");
        if (verdict >= 0)
        {
            string v = report.Substring(verdict);
            int vnl = v.IndexOf('\n');
            if (vnl > 0) v = v.Substring(0, vnl);
            headline = headline + "\n" + v.TrimEnd();
        }
        _craterAuditText = headline;
        Debug.Log("[NewWorldTestGround] " + report);
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
        // 1f7 added this when the axis had two live values (Boulder/Swarm); 1ir deleted Swarm with its
        // last user, so the split is no longer interesting on its own — the axis stays counted
        // because SkyRock still occupies 2 bits of the fingerprint below, and an axis that is packed
        // into the key but not reported is one you cannot tell you moved.
        var bySkyRock = new Dictionary<SkyRockStyle, int>();
        // 1jt adds this alongside the 2 key bits. Unlike SkyRock (constant under determinism), the
        // anchor really is two-valued - Summon deliveries plus the two authored Meteors resolve to
        // Feet and everything else to Front - so this count is the visible proof the axis moved.
        // (1ka: the armed halo no longer reads it - it is flat at the weapon - but the axis is still
        // resolved and packed here, so the count stays; deleting it is a separate task.)
        var byAnchor = new Dictionary<SpellCastAnchor, int>();
        // 1kc: the zone-body axis. Like SkyRock it is resolved to a constant (Funnel) for every
        // non-authored spell, so the count is a singleton until a spell authors VortexCircle —
        // which exactly one spell does (Conflagration). Kept because an axis packed into the key but
        // not reported is one you cannot tell you moved.
        var byZone = new Dictionary<ZoneBody, int>();
        // 1kf: the storm-body axis, the same contract with the opposite default — None for every
        // spell until a profile grants Funnel, which exactly one does (Firestorm). Reported for the
        // same reason as byZone: it is packed into LookKey below.
        var byStorm = new Dictionary<StormBody, int>();
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
            Bump(bySkyRock, look.SkyRock);
            Bump(byAnchor, look.CastAnchor);
            Bump(byZone, look.ZoneModel);
            Bump(byStorm, look.StormBodyModel);
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
          .Append(" authored profiles. axes = impact+cast+shape+skyrock+anchor+zone+stormbody+coreRGB@8bit (scale/tempo excluded)");
        _lookAuditText = sb.ToString();
        _lookAuditRun = true;
        Debug.Log("[NewWorldTestGround] " + _lookAuditText);

        sb.Append('\n').Append("  impact families: ").Append(CountLine(byImpact));
        sb.Append('\n').Append("  cast families:   ").Append(CountLine(byCast));
        sb.Append('\n').Append("  body shapes:     ").Append(CountLine(byShape));
        sb.Append('\n').Append("  sky rocks:       ").Append(CountLine(bySkyRock))
          .Append("   (only SummonFallingRock spells draw one; the rest resolve to Boulder)");
        sb.Append('\n').Append("  cast anchors:   ").Append(CountLine(byAnchor))
          .Append("   (1jt: Feet = Summon deliveries + the two authored Meteors; front = everything else; 1ka: resolved for the audit only, placement no longer reads it)");
        sb.Append('\n').Append("  zone bodies:    ").Append(CountLine(byZone))
          .Append("   (1kc: Funnel for every non-authored zone; VortexCircle = Conflagration's own body)");
        sb.Append('\n').Append("  storm bodies:   ").Append(CountLine(byStorm))
          .Append("   (1kf: None for every spell but Firestorm; Funnel = the pre-1kc conflagration tornado)");
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            sb.Append("\n  COLLISION x").Append(kv.Value.Count).Append(':');
            foreach (string member in kv.Value) sb.Append("\n    ").Append(member);
        }
        Debug.Log("[NewWorldTestGround] " + sb.ToString());
    }

    /// <summary>Packs the perceptual axes into one key. 42 bits: 3+3+4+2+2+2+2 for the enums, 8 per channel.
    /// 1f7 added SkyRock's 2 bits. Because determinism always resolves that axis to
    /// <see cref="SkyRockStyle.Boulder"/> (see the enum remarks), every non-authored spell gets the
    /// SAME two bits, so no existing collision verdict moved - the axis only separates the spells
    /// that actually wear a different falling body, which is exactly the pair 1f7 authored.
    /// <para><b>1kf adds StormBody's 2 bits at 40</b>, under the same contract as 1kc's below: the
    /// axis resolves to <see cref="StormBody.None"/> for every spell but Firestorm, so no existing
    /// key moves and `M`/`colliding` are untouched - the ONLY key that changes is Firestorm's, which
    /// flips its storm bits to <see cref="StormBody.Funnel"/>. Predicted deltas: authored 26→27;
    /// `storm bodies:` reads `Funnel=1` with `None` taking the rest. Firestorm's key moving can only
    /// SPLIT it from a former group (the Funnel bits are unique to it), so `colliding` may drop by
    /// one and never rise - the same one-way ratchet 1f7 described.</para>
    /// <para><b>1kc adds ZoneBody's 2 bits at 38</b>, under the same contract as 1f7's: the axis is
    /// resolved to <see cref="ZoneBody.Funnel"/> for every non-authored spell, so no existing key
    /// moves and `M`/`colliding` are untouched — the ONLY key that changes is Conflagration's, which
    /// flips its zone bits to <see cref="ZoneBody.VortexCircle"/>. If Conflagration previously shared
    /// a group, `colliding` drops by one; otherwise nothing but that one key moves. Predicted deltas:
    /// authored 25→26 (the F4 counter's whole-roster; the magic-catalog-only count 21→22 in the
    /// SkillCatalog files is the same roster minus ClassSkillCatalog's five), zone count
    /// Funnel→everyone-but-Conflagration and VortexCircle=1.
    /// <para><b>1jt adds CastAnchor's 2 bits at 36</b>, for a different reason than 1f7's: 1f7's axis
    /// was resolved to a constant by determinism, whereas the anchor is genuinely two-valued among
    /// spells that are otherwise identical - a halo at the feet and a halo 1.8 m in front are two
    /// different pictures of the same fireball. So this axis is not a no-op like SkyRock was: expect
    /// the distinct count to RISE and `colliding`/`worst` to FALL. That direction is the point, and it
    /// is a one-way ratchet - a new axis can only split a group, never merge two, so these three
    /// numbers moving the wrong way would mean the key is broken rather than the world improved.
    /// <c>Authored</c> is deliberately NOT packed: it means "a profile exists", not "the spell looks
    /// different", so packing it would split groups that are genuinely identical.
    /// <para><b>1ka: the axis is kept packed even though placement no longer reads it</b> (the armed
    /// halo is flat at the weapon now). Removing the axis would shave 2 bits AND change every existing
    /// key, which moves `M`/`colliding` the way a real change would - indistinguishable from a broken
    /// key (1jt's ratchet, reversed). Keeping it packed means the 1ka commit changes the key NOT AT
    /// ALL, so any movement in `M` after it is meaningful.</para></summary>
    private static ulong LookKey(in SpellLook look)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(look.Core.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(look.Core.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(look.Core.b * 255f), 0, 255);
            return ((ulong)(int)look.Impact << 33)
                 | ((ulong)(int)look.Cast << 30)
                 | ((ulong)(int)look.DisplayShape << 26)
                 | ((ulong)(int)look.SkyRock << 24)
                 | ((ulong)(int)look.CastAnchor << 36)
                 | ((ulong)(int)look.ZoneModel << 38)
                 | ((ulong)(int)look.StormBodyModel << 40)
                 | ((ulong)r << 16) | ((ulong)g << 8) | (ulong)b;
        }

private static string Describe(in SpellLook look)
    // 1js: the trailing "trail r,g,b" is the resolved exhaust-trail colour. The matrix band cannot
    // SHOW a trail - it draws every spell on a motionless pedestal and TrailStrip only spawns for a
    // LAUNCHED projectile (SpellEffect.Update returns early on !_launched) - so this text readout is
    // the only way to confirm the resolution rule itself (each school keeps its own hue, Physical is
    // white) without firing one projectile per school. In-flight appearance stays a play-test item.
    // LookKey is deliberately NOT extended for TrailColor: the trail is a pure function of DamageType,
    // so that axis is strictly COARSER than the Core RGB already packed - it could only split a group
    // in the coincidence where two schools Tint to the same Core, which is not a collision the 1ic
    // audit is measuring. (The older version of this comment gave "the key is bit-full at 34 bits" as
    // the reason; that arithmetic was wrong - it is 38 bits of a 64-bit ulong. Space was never the
    // reason Trail is excluded. 1jt.)
    // 1jt DOES add CastAnchor, because the anchor duplicates nothing already packed: the same
    // fireball with its halo at the feet and 1.8 m in front is two different pictures.
    // See AGENTS.md rule 13's corollary.
    // (1ka: the per-spell halo no longer reads the anchor - it is flat at the weapon now - so the
    // value is resolution-only, kept because LookKey packs it.)
    // 1jt: "feet"/"front" is the resolved cast anchor. The matrix band cannot SHOW it - it renders the
    // CAST EFFECT and the halo is drawn by the PLAYER path, not the bench - so this text is the only
    // readout for the resolution rule. Placement itself is a play-test item (1ka: flat, at the weapon).
    => "(" + look.Impact + "/" + look.Cast + "/" + look.DisplayShape + "/" + look.SkyRock + "/"
    + (look.CastAnchor == SpellCastAnchor.Feet ? "feet" : "front") + "/" + look.ZoneModel + "/"
    + look.StormBodyModel + "/"
    + look.Core.r.ToString("F2") + "," + look.Core.g.ToString("F2") + "," + look.Core.b.ToString("F2")
    + " trail " + look.Trail.r.ToString("F2") + "," + look.Trail.g.ToString("F2") + "," + look.Trail.b.ToString("F2")
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
    /// exact live-cast body builders — Comet / explicit shapes (Ice Lance, Shadow Spear, Arcane
    /// Missiles...) show their real projectile body, sky spells show their real falling-rock body
    /// (1f7), and beam/vortex/storm/summon/instant spells show their school-colored default icon
    /// (those deliveries have no static projectile). The label uses the world-TMP pattern from the
    /// legacy building signs. Pure visuals — pedestals and bodies get no collider, so the grid stays
    /// walkable and nothing is interactable.
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
            //
            // 1ir: a sky spell shows its FALLING ROCK instead, via the same live builders
            // FallRock uses — otherwise a Zone spell has no projectile display to fall back on and
            // would show the generic orb.
            if (spell.SummonFallingRock)
            {
                var rockLook = SpellLook.Resolve(spell);
                SkillFx.BuildRockBody(modelRoot.transform, RockBodyBenchScale(spell),
                    rockLook.Core, rockLook.SkyRock);
            }
            // 1kc: Conflagration's own body — mounts the SAME builder the runtime SpellZone runs,
            // for the same rule-13 reason as the beam branch below: a visual that exists only inside
            // a live cast has no acceptance readout. The VortexRiser embers animate on the pedestal,
            // so the rising vortex reads as well as it does in a live cast.
            else if (SpellLook.Resolve(spell).ZoneModel == ZoneBody.VortexCircle)
            {
                var cl = SpellLook.Resolve(spell);
                SpellZoneModelBuilder.BuildConflagration(modelRoot.transform, spell.Radius,
                    cl.Core, SpellLook.HotCore(cl.Core));
            }
            // 1kf: Firestorm's authored storm body — the pre-1kc conflagration funnel — mounted via
            // the SAME builder SpellStorm runs, for the same rule-13 reason as the branches above: a
            // visual that exists only inside a live cast has no acceptance readout. Full spell
            // radius, exactly as the runtime builds it (charge scaling is a live-cast concern).
            else if (SpellLook.Resolve(spell).StormBodyModel == StormBody.Funnel)
            {
                var sl = SpellLook.Resolve(spell);
                SpellZoneModelBuilder.BuildFunnel(modelRoot.transform, spell.Radius,
                    SkillFx.SharedSpriteMaterial(sl.Core));
            }
            // 1ir: the two deliveries that have NO projectile body at all. Without these two branches
            // a Beam and a Summon both fall through to CreateProjectileDisplay, which draws the generic
            // orb — so the bench would report "unchanged" for Flamethrower and Continuous Fireball
            // forever while the live spells drew a cone and a ground circle. That is 1f7's exact
            // failure (a visual that exists only inside a live cast has no acceptance readout), and
            // these mount the SAME builders the runtime uses rather than a proxy that could drift:
            // SpellBeamModelBuilder.BuildFunnelVisual for the swept wedge's funnel body (1is), and SpellSummon's
            // own circle below.
            else if (SpellBeam.ConeFullAngleDegrees(spell) > 0f)
            {
                // 1is: mounts SpellBeamModelBuilder.BuildFunnelVisual — the Great Tornado silhouette in
                // yellow/orange — which is what the live beam now draws. This bench MUST mount the
                // real builder rather than a proxy: 1ij's lesson is that a visual living only inside
                // a live cast has no acceptance readout, and a proxy cone here would have gone on
                // reporting the OLD ray fan as correct forever after the funnel replaced it.
                var funnelLook = SpellLook.Resolve(spell);
                SpellBeamModelBuilder.BuildFunnelVisual(modelRoot.transform,
                    spell.Range,                                  // deliveryRange = beam length
                    spell.Radius * SpellBeam.ConeMouthFraction,  // mouth = a fraction of the tip
                    spell.Radius,                                 // tip = deliveryRadius
                    funnelLook.Core,                              // orange body
                    SpellLook.HotCore(funnelLook.Core));           // yellow where the fire is born
            }
            else if (spell.CasterAnchored)
            {
                // The following familiar's own read: a flat ground circle at the live targeting
                // radius plus the low orb it fires from. Sized from the spell, not hardcoded, so a
                // retuned radius moves the bench model with it.
                Shader benchShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                if (benchShader != null)
                {
                    Color core = SpellLook.Resolve(spell).Core;
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    disc.name = "FollowCircle";
                    Collider dcol = disc.GetComponent<Collider>();
                    if (dcol != null) Destroy(dcol);
                    disc.transform.SetParent(modelRoot.transform, false);
                    disc.transform.localPosition = new Vector3(0f, -0.95f, 0f);
                    disc.transform.localScale = new Vector3(spell.Radius * 2f, 0.05f, spell.Radius * 2f);
                    disc.GetComponent<MeshRenderer>().sharedMaterial = SolidMaterial(core);

                    var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    orb.name = "FollowCore";
                    Collider ocol = orb.GetComponent<Collider>();
                    if (ocol != null) Destroy(ocol);
                    orb.transform.SetParent(modelRoot.transform, false);
                    orb.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                    orb.transform.localScale = Vector3.one * 0.6f;
                    orb.GetComponent<MeshRenderer>().sharedMaterial = SolidMaterial(core);
                }
            }
            else
            {
                MagicProjectileModelBuilder.CreateProjectileDisplay(spell)
                    .transform.SetParent(modelRoot.transform, false);
            }

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
    /// 1je: the summon-model lane. Mounts all THREE summon bodies side by side on pedestals, so
    /// "summon model" is finally a thing you can look at without casting at something.
    /// <para><b>Why this lane exists at all.</b> <c>SummonedAlly</c> is created by exactly two call
    /// sites, <c>SummonEffect.Execute</c> (via ClassEffect) and <c>RaceEffect</c>, both inside live
    /// combat, so before 1je the ally's body could only be seen by fighting, which is the same
    /// discoverability hole 1iz describes for the magic models, one layer out. The familiar circle
    /// and the totem ride along because they had no readout either and they are the two other shapes
    /// <c>SpellSummon</c> can build.</para>
    /// <para><b>Band choice, stated.</b> z = centre + 11f: south of the magic-model grid (which spans
    /// z +/- 1.5 at 3 m spacing with 2 rows) and 7 m north of the farming/livestock/buildings band at
    /// z + 18+, clear of the dummy row (z - 18), the NPC row (z - 0.18*Size) and the +/-0.42*Size
    /// weapon-rack / tool-kit lines. Pedestals rather than ground-standing so the 1.65 m ally reads
    /// against the platform and not against a clump of grass.</para>
    /// <para><b>Colour honesty.</b> The ally cell wears <c>SummonedAlly.AllyColor</c>, which is its
    /// REAL spawn colour, because <c>SummonEffect</c> passes no spell and the component hard-codes it
    /// so the bench is not allowed to dress it in a <c>SpellLook</c> it never resolves. The other two
    /// cells wear the same colour for SHAPE comparison only; at runtime <c>SpellSummon</c> colours
    /// them <c>SpellLook.Resolve(spell).Core</c>.</para>
    /// <para>Re-pressable and idempotent: each run clears the previous row rather than stacking a
    /// second one, so the key is safe to hit while iterating on the model.</para>
    /// </summary>
    private void SpawnSummonModels()
    {
        if (_summonModelCells.Count > 0)
        {
            for (int i = 0; i < _summonModelCells.Count; i++)
                if (_summonModelCells[i] != null)
                    Destroy(_summonModelCells[i]);
        }
        float z = PlatformCenter.z + 11f;
        float spacing = 4.5f;
        Color tint = SummonedAlly.AllyColor;

        MountSummonModelCell("FamiliarCircle", z - spacing,
            "Familiar circle (shape only - real colour is SpellLook.Core)", tint,
            parent => SummonModelBuilder.BuildFamiliarCircle(parent, 3f, tint));
        MountSummonModelCell("StandingTotem", z,
            "Standing totem (shape only - real colour is SpellLook.Core)", tint,
            parent => SummonModelBuilder.BuildTotem(parent, tint));
        MountSummonModelCell("SummonedAlly", z + spacing,
            "SummonedAlly - combat construct", tint,
            parent => SummonModelBuilder.BuildAlly(parent, tint));

        Debug.Log("[NewWorldTestGround] summon-model lane: 3 bodies on pedestals at z=" + z
            + " (ally colour #" + ColorUtility.ToHtmlStringRGB(tint) + ").");
    }

      /// <summary>1jv: feed the boom lane's trailing window. Runs EVERY frame - the key only reads the
      /// window, because "snaps in and out" is a transient and a single frame cannot catch one. Strictly
      /// read-only: reads two transforms and two published floats, writes only its own arrays.</summary>
      private void TrackBoomFrame()
      {
          var gm = GameManager.Instance;
          var player = gm != null ? gm.Player : null;
          PlayerController ctrl = player != null ? PlayerControllerCached(player) : null;
          Camera cam = Camera.main;

          // Resolve the switcher only when the controller CHANGES - one GetComponent per owner rather
          // than per frame - and drop the window with it: a respawn that swaps the controller must not
          // leave samples from the OLD player's camera mixed into one report.
          if (!ReferenceEquals(ctrl, _boomOwner))
          {
              _boomOwner = ctrl;
              _boomMode = ctrl != null ? ctrl.GetComponent<CameraModeSwitch>() : null;
              _boomHead = 0;
              _boomFilled = 0;
              _boomSeen = false;
          }
          Transform pivot = ctrl != null ? ctrl.PlayerCameraPivot : null;
          CameraModeSwitch mode = _boomMode;

          // PREMISE gate. In first person this camera snaps to the pivot every frame and cannot zoom
          // at all, so a distance sampled then describes a camera that is not the boom. Reset the
          // window instead of recording a number that cannot mean anything - a lane that reports a
          // confident figure about the wrong camera is worse than one that reports nothing.
          if (ctrl == null || cam == null || pivot == null || mode == null || mode.IsFirstPerson)
          {
              _boomHead = 0;
              _boomFilled = 0;
              return;
          }

          Vector3 pp = pivot.position;
          // Speed from the pivot's own motion rather than CharacterController.velocity: one less
          // component to resolve, and it measures what the CAMERA is actually being asked to follow.
          float speed = _boomSeen ? Vector3.Distance(pp, _boomLastPivot) / Mathf.Max(Time.deltaTime, 1e-5f) : 0f;
          _boomLastPivot = pp;
          _boomSeen = true;

          _boomDist[_boomHead] = Vector3.Distance(cam.transform.position, pp);
          _boomSpeed[_boomHead] = speed;
          _boomYaw[_boomHead] = pivot.eulerAngles.y; // task 4 - camera boom yaw from pivot
          _boomRest[_boomHead] = mode.BoomRestLength;
          _boomApplied[_boomHead] = mode.BoomAppliedLength;
          // Clamped = the boom itself was shortened by the collision SphereCast, by more than float
          // noise. Compared against the boom's OWN published rest length, not a magic constant.
          _boomClamped[_boomHead] = mode.BoomAppliedLength < mode.BoomRestLength - 0.01f;
          _boomHitName[_boomHead] = mode.BoomLastHitName;
          _boomHitLayer[_boomHead] = mode.BoomLastHitLayer;
          _boomHitDist[_boomHead] = mode.BoomLastHitDistance;
          _boomHead = (_boomHead + 1) % BoomWindow;
          if (_boomFilled < BoomWindow) _boomFilled++;
      }

      /// <summary>1jv: report the boom readout for the window that ended on this frame. Sections are
      /// ordered by whether their PREMISE holds (A first), the window and its length are printed so a
      /// zero reads as "not measured", and the verdict names a MECHANISM off the controls rather than
      /// off the size of the movement.</summary>
      private void SnapshotBoomAudit()
      {
          var sb = new System.Text.StringBuilder(384);
          // Read the switcher TrackBoomFrame resolved THIS frame, rather than reaching for the
          // component again: one place decides what the premise gate saw, so the gate in the tracker
          // and the gate in the report can never disagree about which camera they mean.
          CameraModeSwitch mode = _boomMode;
          bool third = mode != null && !mode.IsFirstPerson;

          sb.Append("boom audit: ");
          sb.Append("A premise: ").Append(third ? "THIRD person" : "FIRST person / switch absent");
          sb.Append(", window=").Append(_boomFilled).Append('/').Append(BoomWindow).Append(" frames");

          if (!third)
          {
              sb.Append("\n  D verdict: VOID - in first person this camera snaps to the pivot and has no");
              sb.Append("\n    boom, so no distance here can answer the report. Switch to third person (F5)");
              sb.Append("\n    and press the key while moving.");
              _boomAuditText = sb.ToString();
              Debug.Log("[NewWorldTestGround] " + _boomAuditText.Replace("\n", " | "));
              return;
          }
          if (_boomFilled == 0)
          {
              sb.Append("\n  D verdict: NOT MEASURED - the window is empty, so this says nothing about");
              sb.Append("\n    the camera. Move around in third person first, then press the key.");
              _boomAuditText = sb.ToString();
              Debug.Log("[NewWorldTestGround] " + _boomAuditText.Replace("\n", " | "));
              return;
          }

          // Walk the ring in chronological order (oldest first) so the yaw sum is a real total.
          int start = (_boomHead - _boomFilled + BoomWindow * 2) % BoomWindow;
          float dMin = float.MaxValue, dMax = 0f, peakStep = 0f;
          float restMin = float.MaxValue, restMax = 0f;
          float appMin = float.MaxValue, appMax = 0f;
          float peakSpeed = 0f, movingFrames = 0f;
          int clampedFrames = 0, restMoving = 0;
          float prevD = 0f, prevYaw = 0f, yawTotal = 0f;
          bool havePrev = false;

          for (int n = 0; n < _boomFilled; n++)
          {
              int i = (start + n) % BoomWindow;
              float d = _boomDist[i], r = _boomRest[i], a = _boomApplied[i];
              if (d < dMin) dMin = d;
              if (d > dMax) dMax = d;
              if (r < restMin) restMin = r;
              if (r > restMax) restMax = r;
              if (a < appMin) appMin = a;
              if (a > appMax) appMax = a;
              if (_boomClamped[i]) clampedFrames++;
              // A frame counts as "moving" on a threshold well above transform noise, so a standing
              // player cannot manufacture a finding (rule 7: gate the classifier on its own width).
              if (_boomSpeed[i] > 0.5f) { movingFrames++; if (_boomSpeed[i] > peakSpeed) peakSpeed = _boomSpeed[i]; }
              if (havePrev)
              {
                  float step = Mathf.Abs(d - prevD);
                  if (step > peakStep) peakStep = step;
                  float dy = Mathf.Abs(Mathf.DeltaAngle(prevYaw, _boomYaw[i]));
                  yawTotal += dy;
                  if (dy < 0.5f) restMoving++;
              }
              prevD = d; prevYaw = _boomYaw[i]; havePrev = true;
          }

          float distRange = dMax - dMin;
          sb.Append("\n  B controls: peak speed ").Append(peakSpeed.ToString("0.0")).Append(" m/s, ")
            .Append(movingFrames.ToString("0")).Append('/').Append(_boomFilled).Append(" frames above 0.5 m/s")
            .Append(", mouse yaw total ").Append(yawTotal.ToString("0")).Append(" deg (")
            .Append(restMoving.ToString("0")).Append(" frames under 0.5 deg)");
          sb.Append("\n  C measure: camera-to-pivot ").Append(dMin.ToString("0.00")).Append(" .. ")
            .Append(dMax.ToString("0.00")).Append(" m (range ").Append(distRange.ToString("0.00"))
            .Append(", peak frame step ").Append(peakStep.ToString("0.000")).Append(" m)");
          sb.Append("\n            boom rest ").Append(restMin.ToString("0.00")).Append(" .. ")
            .Append(restMax.ToString("0.00")).Append(" m, applied ").Append(appMin.ToString("0.00"))
            .Append(" .. ").Append(appMax.ToString("0.00")).Append(" m, clamped on ")
            .Append(clampedFrames).Append('/').Append(_boomFilled).Append(" frames");

          // 1ks: NAME the clamp's mechanism. A clamped frame's _cachedFinalDist came from a cast whose
          // hit CameraModeSwitch published; tally those hits so the clamp count becomes a who, not a
          // how many. Read-only: values are the source's own, recorded by TrackBoomFrame, never
          // re-derived here. Printed as "name (layer) xN", most frequent first, up to four distinct
          // hitters; a start-overlap shows a distance at the pad floor, the "behind the head" pinch.
          if (clampedFrames > 0)
          {
              var hitCount = new Dictionary<string, int>();
              var hitLayer = new Dictionary<string, int>();
              var hitNear = new Dictionary<string, float>();
              for (int n = 0; n < _boomFilled; n++)
              {
                  int i = (start + n) % BoomWindow;
                  if (!_boomClamped[i]) continue;
                  string nm = _boomHitName[i] ?? "(no hit recorded)";
                  hitCount.TryGetValue(nm, out int c);
                  hitCount[nm] = c + 1;
                  hitLayer[nm] = _boomHitLayer[i];
                  float d = _boomHitDist[i];
                  if (!hitNear.TryGetValue(nm, out float dn) || d < dn) hitNear[nm] = d;
              }
              var ordered = new List<KeyValuePair<string, int>>(hitCount);
              ordered.Sort((a, b) => b.Value.CompareTo(a.Value));
              sb.Append("\n  C-hitters (clamped frames): ");
              int shown = 0;
              foreach (var kv in ordered)
              {
                  if (shown == 4) { sb.Append(", ..."); break; }
                  if (shown > 0) sb.Append(", ");
                  sb.Append('"').Append(kv.Key).Append("\" L").Append(hitLayer[kv.Key])
                    .Append(" x").Append(kv.Value).Append(" nearest ")
                    .Append(hitNear[kv.Key].ToString("0.00")).Append(" m");
                  shown++;
              }
          }

          // The verdict. Each branch states the EVIDENCE it fired on, not a vibe about the numbers.
          if (movingFrames < _boomFilled * 0.25f)
          {
              sb.Append("\n  D verdict: NOT REPRODUCED - under a quarter of the window had the player");
              sb.Append("\n    moving, so a distance that held proves nothing. Run and turn, then re-press.");
          }
          else if (clampedFrames > _boomFilled * 0.10f)
          {
              sb.Append("\n  D verdict: COLLISION CLAMP. The boom was shortened by the SphereCast on ")
                .Append(clampedFrames).Append(" frames, so the camera really is being pulled in and");
              sb.Append("\n    pushed out by terrain or props - not the smoother. 1ju makes this visible in the");
              sb.Append("\n    AIM on the same frame, because the aim now reads the unclamped boom position.");
              sb.Append("\n    See C-hitters for the OBJECT that fires it (1ks): a name there with a");
              sb.Append("\n    nearest distance at ~0.8 m is the start-overlap pinch, not a real wall.");
          }
          else if (distRange > 0.35f && yawTotal < 5f)
          {
              sb.Append("\n  D verdict: POSITION SMOOTHER. The boom held at ").Append(restMax.ToString("0.00"))
                .Append(" m and was never clamped, yet the measured distance moved ").Append(distRange.ToString("0.00"));
              sb.Append("\n    m while the player turned only ").Append(yawTotal.ToString("0")).Append(" deg. So it is neither");
              sb.Append("\n    the boom swinging nor the clamp: the camera is trailing PAST the boom's end, and");
              sb.Append("\n    that lag is being added to the effective boom length. Speed-dependent by");
              sb.Append("\n    construction - lag is v * SmoothTime (0.15 s).");
          }
          else if (yawTotal >= 5f)
          {
              sb.Append("\n  D verdict: INCONCLUSIVE - the player turned ").Append(yawTotal.ToString("0"))
                .Append(" deg in this window, so the");
              sb.Append("\n    boom swung and a distance change is expected. Hold the mouse still while moving");
              sb.Append("\n    and press again; the next window will separate the two.");
          }
          else
          {
              sb.Append("\n  D verdict: STEADY. Distance held within ").Append(distRange.ToString("0.00"))
                .Append(" m, boom never clamped, player");
              sb.Append("\n    turning under 5 deg - this window does not reproduce the report.");
          }

          _boomAuditText = sb.ToString();
          Debug.Log("[NewWorldTestGround] " + _boomAuditText.Replace("\n", " | "));
      }

    /// <summary>1jq: read-only snapshot of the in-flight projectile trail strips. This is the
    /// acceptance readout for the voxel -> strip rewrite: the proposal was entirely about COST, and no
    /// other lane in this file reports component or draw COUNTS (1ik's frame-budget lane reports
    /// milliseconds, which under vsync is quantised by the present interval and cannot separate
    /// "cheaper" from "slightly cheaper").
    /// <para>
    /// <b>Scoping, which is the whole difficulty (rule 7).</b> "Live strips that were not drawn" is the
    /// finding this lane exists to catch — bad mesh bounds or a missing camera are invisible to a pure
    /// component count. But it is only a POSITIVE result where a draw is possible at all: a bolt's first
    /// 0.3 m of flight has a strip with zero segments and therefore nothing to draw, and counting that
    /// as a fault would train the reader to ignore the verdict. So the not-drawn count is split into
    /// <c>drawn</c> / <c>undrawn</c> over strips that HAVE geometry, and strips with no geometry are
    /// reported separately as <c>young</c>. Only <c>undrawn-with-geometry</c> is a fault, and it is the
    /// number the headline names.
    /// </para>
    /// <para>
    /// The collider count is the deliberate known-zero control. It is zero BY DESIGN (a trail is
    /// cosmetic), so a run that reports strips-with-colliders &gt; 0 means the count is lying, and a
    /// count never shown able to report non-zero is not a count. It is also a real invariant rather than
    /// a filler number: a strip that acquired a Collider would cost physics time it never should.
    /// </para>
    /// </summary>
      private void SnapshotTrailAudit()
    {
        TrailStrip[] strips = Object.FindObjectsByType<TrailStrip>(FindObjectsSortMode.None);

        int live = 0, drawn = 0, young = 0, undrawnWithGeometry = 0;
        int segs = 0, verts = 0, tris = 0;
        int minSegs = int.MaxValue, maxSegs = 0, withCollider = 0;

        for (int i = 0; i < strips.Length; i++)
        {
            TrailStrip s = strips[i];
            if (s == null) continue;
            live++;
            segs += s.SegmentCount;
            verts += s.VertexCount;
            tris += s.TriangleCount;
            if (s.SegmentCount < minSegs) minSegs = s.SegmentCount;
            if (s.SegmentCount > maxSegs) maxSegs = s.SegmentCount;
            if (s.GetComponent<Collider>() != null) withCollider++;

            if (s.WasDrawn) { drawn++; continue; }
            if (s.SegmentCount == 0) young++; else undrawnWithGeometry++;
        }

        if (minSegs == int.MaxValue) minSegs = 0;

        var sb = new System.Text.StringBuilder(256);
        sb.Append("trail audit: ");
        if (live == 0)
        {
            // A zero here is ambiguous on its own — no bolt in flight, or the trail is gone. Say so
            // rather than printing a clean 0 that reads as "the trail costs nothing".
            sb.Append("0 strips live - no projectile in flight (cast one and press again)");
            _trailAuditText = sb.ToString();
            Debug.Log("[NewWorldTestGround] " + _trailAuditText);
            return;
        }

        sb.Append(live).Append(" live, ").Append(drawn).Append(" drawn, ").Append(young)
            .Append(" young (no geometry yet)");
        sb.Append("\nundrawn WITH geometry: ").Append(undrawnWithGeometry)
            .Append(undrawnWithGeometry == 0 ? "  <- clean" : "  <- FAULT: bounds or camera");
        sb.Append("\ntotals: ").Append(segs).Append(" segments / ").Append(verts)
            .Append(" verts / ").Append(tris).Append(" tris");
        if (segs > 0) sb.Append("  (per strip ").Append(minSegs).Append('-').Append(maxSegs).Append(')');
        sb.Append("\ncontrol: ").Append(withCollider).Append(" strips with a Collider (expect 0)");
        sb.Append("\ncaps: ").Append(TrailStrip.MaxPoints).Append(" points = ")
            .Append(TrailStrip.MaxPoints - 1).Append(" segments max, step ").Append(TrailStrip.Step)
            .Append(" m, life ").Append(TrailStrip.Life).Append(" s");

        _trailAuditText = sb.ToString();
        Debug.Log("[NewWorldTestGround] " + _trailAuditText.Replace("\n", " | "));
    }

    /// <summary>One pedestal + one mounted summon body + one label, parented to the lane root.
    /// <paramref name="mount"/> takes the mount point and runs the real builder, so the bench cannot
    /// drift from the live model (1ij/1jd's "mount the builder, not a proxy" rule).</summary>
    private void MountSummonModelCell(string name, float z, string label, Color tint,
        System.Action<Transform> mount)
    {
        var cell = new GameObject("SummonModel_" + name);
        _summonModelCells.Add(cell);
        cell.transform.position = new Vector3(PlatformCenter.x, PlatformTopY + 0.1f, z);
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
        if (pmr != null) pmr.sharedMaterial = SolidMaterial(new Color(0.24f, 0.2f, 0.17f));

        var modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(cell.transform, false);
        modelRoot.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        // 1je: the ally body is authored with its head top at 2.06 and the component spawns it at
        // localScale 0.8 (so 1.65 m live). This cell mounts the raw builder output at 1.0 to show
        // the authored proportions; the label says so, because a bench that silently rescales is
        // how "the model looks wrong in game" becomes a two-session argument.
        modelRoot.transform.localScale = Vector3.one;
        mount(modelRoot.transform);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(cell.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 2.9f, 0f);
        var tmp = labelGo.AddComponent<TMPro.TextMeshPro>();
        tmp.text = name + "\n" + label;
        tmp.fontSize = 0.9f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = tint;
        tmp.outlineWidth = 0.1f;
        tmp.outlineColor = Color.black;
        tmp.rectTransform.sizeDelta = new Vector3(4f, 1.2f);
    }

/// <summary>
    /// 1f7: the <c>scale</c> a falling rock is drawn at on the magic-model bench.
    ///
    /// <para>A live rock body's <c>scale</c> is the spell's BLAST RADIUS (3 for Meteor, 4 for
    /// Asteroid), so drawing it at that value puts a 7-9 m wide rock in a 3 m grid cell — it would
    /// swamp its neighbours, which are the whole point of a comparison bench. This is a STATED
    /// fraction of the real value rather than a second opinion of it, so the bench still shows the
    /// real body's proportions and only its size differs.</para>
    ///
    /// <para>Note the 0.35 here is NOT the 0.5 the Storm sky path applies
    /// (<c>SpellStorm.StrikeDelayed</c>: <c>Radius * 0.5f</c>, a per-strike gameplay decision about
    /// how big each strike's rock reads). This one exists only so the bench grid stays legible, so it
    /// is a bench constant and not a second gameplay spelling.</para>
    /// </summary>
    private static float RockBodyBenchScale(SpellData spell)
        => spell == null ? 1f : Mathf.Max(spell.Radius * 0.35f, 0.8f);

    /// <summary>
    /// Equip a representative starter set into the player's 21-slot equipment system (testing),
/// so the humanoid Equipment tab has something to show. Also wires up the ClassUnlocker for
/// the Class tab — under its exclusive single-choice model the Wanderer baseline is active
/// until the player picks a class.
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
                // polls since this one. The chunk distance-cull sweep runs on the gameplay frame
                // (outside the streamer coroutine), so it is reported separately as its own worst-case.
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
                    var cull = Object.FindAnyObjectByType<ChunkDistanceCull>();
                    if (cull != null)
                        stats += string.Format("\ncull sweep {0:0.00} / {1:0.00} ms", cull.LastCullMs, cull.PeakCullMs);
                    streamer?.ResetPollStagePeaks();
                }

                // (1gh) chunk diagnostics: for any "chunk X is invisible for no reason" report this
                // single line says whether it is missing, retained-dormant, hidden (root/inactive),
                // mesh-less, or under a dead far cell — one screenshot resolves it. 1f6 removed the
                // lod1/lod2 and band fields with the LOD children they described: the root mesh is the
                // chunk's only render surface, so there is no second thing to be covering it.
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

                // (1in) Same persistence rule as the two audits above: the crater headline stays up
                // until the next press, so a screenshot taken after walking up to the dent the
                // report named still shows the numbers for the frame the key was pressed on.
                if (EnableCraterAudit && !string.IsNullOrEmpty(_craterAuditText))
                    stats += "\n" + _craterAuditText;

                // 1id: SpellImpactFx's per-frame budget REFUSES flashes past PerFrameBudget in one
                // frame and counts the refusals. That counter was write-only until here — the budget
                // is a cap, and a cap cannot be judged from taste (rule 7). Read LIVE every frame,
                // unlike the frozen audit headline above, and on its own line so a rising count is
                // legible against the frame instead of reading as part of the identity number.
                // _lookAuditRun gates it: the audit must have been pressed once for the lane to arm.
                if (EnableLookAudit && _lookAuditRun)
                    stats += "\n" + SpellImpactFx.DroppedSinceLaunch + " impact flashes dropped"
                        + " (budget " + SpellImpactFx.PerFrameBudget + "/frame)";

                // (1ik) Frame-budget snapshot. It stays up until the NEXT press replaces it, not
                // forever like the corner/look headlines above: a stale standing-still number sitting
                // under a walking frame is worse than no number, because it reads as current. The
                // window's frame count is printed with the averages so the reader knows whether this
                // is 90 frames or 12.
                if (!string.IsNullOrEmpty(_frameBudgetText))
                    stats += "\n" + _frameBudgetText;

                // 1jq: the trail-strip snapshot. Same persistence rule as the other audits - it stays
                // up until the next press replaces it - because a strip count is only meaningful next
                // to the frame it was taken on (a bolt's life is 0.35s, so a later frame reads 0 and
                // would read as "the trail is broken" rather than "the bolt already finished").
                if (EnableTrailAudit && !string.IsNullOrEmpty(_trailAuditText))
                    stats += "\n" + _trailAuditText;

                // 1jv: the camera-boom snapshot. Same persistence rule, so the readout stays on screen
                // until the next press replaces it - the report IS the window that ended at the press.
                if (EnableBoomAudit && !string.IsNullOrEmpty(_boomAuditText))
                    stats += "\n" + _boomAuditText;

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
