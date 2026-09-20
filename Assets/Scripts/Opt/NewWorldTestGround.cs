using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("Automatically teleport the player onto the platform when the bench spawns. Disabled by default: the game starts the player on the world's boot chunk near (0, terrain, -10) and the platform is left for the player to walk to.")]
    public bool AutoTeleportPlayerOnStart = false;

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
    public bool EnableMagicModels = true;
    [Tooltip("Cast Earth-shape terrain demos (Wall smooth ridge, Pillar, Crater smooth dent) onto the streamed terrain just off the platform. The Wall is cast twice to show repeat casts are CAPPED (smooth feathered deforms, no slab stacking — 1cj). Deforms REAL terrain — permanent chunk saves — so it is off by default and never touches the platform or legacy village.")]
    public bool EnableTerrainSlabDemo = false;
    [Tooltip("QA the layered strata (grass -> dirt -> stone): two craters excavated on the streamed terrain just off the platform by repeating the shared crater digs (each cast ratchets the floor a step deeper, like the shovel/pickaxe path). One pit reaches the dirt band, the other digs through into stone. Deforms REAL terrain — permanent chunk saves — so it is off by default and never touches the platform or legacy village.")]
    public bool EnableDigLayersDemo = false;
    [Tooltip("QA: delete this world's terrain chunk saves and regenerate every loaded chunk from noise — a deliberate clean map (no leftover slabs, closed mesh). Permanently discards ALL terrain edits for the current seed, so it is off by default and never touches the platform or legacy village.")]
    public bool EnableResetTerrainSaves = false;
    [Tooltip("QA: self-apply every combat status (Burn DoT, Wet, Blind, two wet-conducted Chill stacks) plus the food/drink stamina buff to the local player, so the status strip under the HUD bars can be play-tested. Applies to the player directly — no world placement.")]
    public bool EnableStatusEffectsDemo = false;

    private WorldNpcPlacer _npcPlacer;
    private bool _spawned;
    private bool _toolKitSpawned;
    private bool _pendingPlayerGrants;
    private bool _arenaReady;
    private GameObject _testGroundRoot;
    private readonly List<WeaponRackStand> _rackStands = new List<WeaponRackStand>();
    private ContextPromptUI _contextPrompt;
    private PlayerController _playerController;

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
        // immediately — there is no streaming gate and no void to race. This is opt-in only
        // (AutoTeleportPlayerOnStart, default off): the player starts on the world's boot chunk
        // and walks to the platform when they want the bench.
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
            go.AddComponent<EnemyController>().ApplyEnemyId(ids[i]);
        }
    }

    private void SpawnDummy(string name, float x, float z, float damageReduction)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(x, PlatformTopY + 0.05f, z);
        var col = go.AddComponent<SphereCollider>();
        col.radius = 1f;
        col.center = new Vector3(0f, 0.85f, 0f);

        var ec = go.AddComponent<EnemyController>();
        ec.ApplyEnemyId("dummy");
        ec.SetMaxHealth(1000);
        ec.Damage = 0;
        ec.Armor = 0;
        ec.DamageReduction = damageReduction;
        ec.AttackRange = 0f;
        ec.AttackCooldown = 1.2f;
        ec.ChaseRange = 30f;
        ec.AlertRange = 30f;
        ec.LeashRange = 40f;
        ec.MoveSpeed = 0f;
        ec.PatrolSpeed = 0f;
        ec.CanFlee = false;
        ec.Immortal = true;
        ec.RegenPerSecond = Mathf.RoundToInt(ec.MaxHealth * 0.15f);
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

        wb.BuildShrine(new Vector3(cx - 30f, baseY, cz - 38f));
        var taoist = MapBuilder.BuildTaoistNpc(wb.WorldRoot.transform, new Vector3(cx - 30f, baseY, cz - 46.6f), Quaternion.identity);
        taoist.AddComponent<TaoistPriestNPC>();

        wb.BuildChurch(new Vector3(cx + 30f, baseY, cz - 35f));
        var priest = MapBuilder.BuildPriestNpc(wb.WorldRoot.transform, new Vector3(cx + 17.5f, baseY, cz - 35f), Quaternion.Euler(0f, 90f, 0f));
        priest.AddComponent<PriestNPC>();

        wb.BuildPagoda(new Vector3(cx - 30f, baseY, cz + 44f));
        var monk = MapBuilder.BuildMonkNpc(wb.WorldRoot.transform, new Vector3(cx - 32f, baseY, cz + 46f), Quaternion.Euler(0f, -90f, 0f));
        monk.AddComponent<PagodaMonkNPC>();
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
            SpellCaster.CreateProjectileDisplay(skill.DamageKind, spell.Shape, spell.SummonFallingRock)
                .transform.SetParent(modelRoot.transform, false);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(cell.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            var tmp = labelGo.AddComponent<TMPro.TextMeshPro>();
            tmp.text = skill.displayName ?? skill.id;
            tmp.fontSize = 1.6f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = DamageNumber.ColorFor(skill.DamageKind);
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

    private static Material SolidMaterial(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { color = c };
        return mat;
    }
}