using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Testing ground for the open world. Drop this ONE component on a GameObject and it carves a flat
/// arena out of the real procedural terrain (the world's own generator + <see cref="WorldStreamer"/>
/// chunk pipeline — same 5-octave noise the terrain is built from — rather than an artificial
/// floating floor) and lays out a deterministic test bench on it: the player tool/seed kit, a
/// farming plot, livestock pens, an enemy arena, a building row, an NPC row, and a crafted
/// fast-travel POI hub. It composes existing public APIs only (no rewrites of live contracts), so
/// every Phase 4-8/9 system can be exercised from a single area.
///
/// Opt-in lanes via serialized toggles; spawn once (idempotent) on <see cref="AutoSpawnOnStart"/>.
/// </summary>
public sealed class NewWorldTestGround : MonoBehaviour
{
    [Header("Arena")]
    [Tooltip("Size of the flat test arena (X/Z world units).")]
    public float PlatformSize = 120f;
    [Tooltip("Centre of the arena in world space. Its Y is snapped to the sampled ground height.")]
    public Vector3 PlatformCenter = new Vector3(0f, 50f, 0f);
    [Tooltip("Carve a flat arena out of the procedural terrain (WorldStreamer.FlattenAt).")]
    public bool CreatePlatform = true;

    [Header("Spawning")]
    [Tooltip("Spawn the bench automatically on Awake.")]
    public bool AutoSpawnOnStart = true;

    [Header("Lanes")]
    [Tooltip("Lay the tool/food discovery kit along the platform's east edge as world pickups to grab with E.")]
    public bool EnableTools = true;
    public bool EnableFarming = true;
    public bool EnableLivestock = true;
    public bool EnableEnemies = true;
    public bool EnableBuildings = true;
    public bool EnableNpcs = true;
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

    private WorldNpcPlacer _npcPlacer;
    private bool _spawned;
    private bool _toolKitSpawned;
    private bool _pendingPlayerGrants;
    private bool _arenaReady;
    private float _flattenFeather = 3f;
    private readonly List<WeaponRackStand> _rackStands = new List<WeaponRackStand>();
    private ContextPromptUI _contextPrompt;
    private PlayerController _playerController;

    /// <summary>True when the flat test arena has been carved (never a void to spawn into).</summary>
    public bool IsArenaReady => _arenaReady && PlatformTopY != float.MinValue;

    /// <summary>XZ bounds + ground height of the flat arena carved out of the procedural terrain.</summary>
    public static float PlatformTopY { get; private set; } = float.MinValue;
    public static float PlatformMinX { get; private set; }
    public static float PlatformMaxX { get; private set; }
    public static float PlatformMinZ { get; private set; }
    public static float PlatformMaxZ { get; private set; }

    /// <summary>True when a world position lies inside the flat test arena footprint.</summary>
    public static bool IsInsidePlatform(float x, float z)
    {
        return PlatformTopY != float.MinValue
            && x >= PlatformMinX && x <= PlatformMaxX
            && z >= PlatformMinZ && z <= PlatformMaxZ;
    }

    /// <summary>
    /// Remove nature props already spawned inside the arena footprint before the ground was
    /// flattened (chunk props stream in over ticks after their chunk loads), so nothing can
    /// poke its collider up through the pad and eject the player.
    /// </summary>
    private static void ClearPropsInsidePlatform()
    {
        foreach (var prop in Object.FindObjectsByType<ChunkObject>(FindObjectsSortMode.None))
        {
            Transform root = prop.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (IsTreeOrRock(child) && IsInsidePlatform(child.position.x, child.position.z))
                    Object.Destroy(child.gameObject);
            }
        }
    }

    private static bool IsTreeOrRock(Transform t)
    {
        if (t == null) return false;
        string n = t.name;
        return n.StartsWith("Tree") || n.StartsWith("Rock") || n.StartsWith("Stone");
    }

    private void Awake()
    {
        if (CreatePlatform)
            PrepareArenaGround();

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
    /// heavy setup (weapon models, NPCs, enemies, grants) no longer blocks the first frames. When
    /// the arena is enabled it first waits for the pad's terrain chunks to stream in, then carves
    /// the flat ground in place (the real procedural terrain) and PULLS THE PLAYER ONTO IT before
    /// spawning any lane, so the player is never left in the void ("ground first, then player").
    /// Every lane runs isolated — a failure in one (e.g. one enemy spawn) logs an error instead of
    /// aborting the bench and stranding the player.
    /// </summary>
    private System.Collections.IEnumerator RunBenchSpawn()
    {
        if (_spawned) yield break;
        _spawned = true;

        if (CreatePlatform)
        {
            yield return StartCoroutine(WaitForArenaTerrain());
            RunSafely("arena flatten", () =>
            {
                FlattenArenaTerrain();
                ClearPropsInsidePlatform();
            });

            // "Ground first, then player": the pad is (mostly) settled now — put the player on it
            // BEFORE laying the bench lanes, so whoever is playing never floats/fell in the void
            // while the rest of the bench builds.
            RunSafely("player placement", PlacePlayerOnArena);
        }

        if (EnableTools) { RunSafely("tool pickups", SpawnToolKit); yield return null; }
        if (EnableFarming) { RunSafely("farming plot", SpawnFarmingPlot); yield return null; }
        if (EnableLivestock) { RunSafely("livestock", SpawnLivestock); yield return null; }
        if (EnableEnemies) { RunSafely("enemies", SpawnEnemies); yield return null; }
        if (EnableBuildings) { RunSafely("buildings", SpawnBuildings); yield return null; }
        if (EnableNpcs) { RunSafely("npcs", SpawnNpcs); yield return null; }
        if (EnablePoiHub) { RunSafely("POI hub", RegisterPoiHub); yield return null; }
        if (EnableWeapons)
        {
            RunSafely("weapons", SpawnAllWeapons);
            yield return null;
            RunSafely("weapon rack", SpawnWeaponRack);
            yield return null;
        }
        if (EnableSkills) { RunSafely("skills", GrantAllSkills); yield return null; }
        if (EnableGear) { RunSafely("gear", GrantStarterGear); yield return null; }
        if (EnableRaces) { RunSafely("races", GrantRaceAccess); yield return null; }
        RunSafely("player grants", TryDeferPlayerGrants);

        // Safety net: if no platform could be carved (CreatePlatform off or flatten failed), pull
        // the player to the bench point at the very end so they never sit stranded mid-void.
        if (!IsArenaReady)
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

    /// <summary>Teleport the player onto the settled arena (2 m above the flattened pad).</summary>
    private void PlacePlayerOnArena()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        player.transform.position = GetSpawnPoint();
    }

    /// <summary>
    /// Spawn the whole test bench through the budgeted coroutine (safe to call repeatedly). The
    /// arena carve waits for streamed terrain, so a sync caller just starts the async path.
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
    /// Re-apply the bench grants (tool kit, every catalog weapon, ritual stones, skill budget,
    /// starter gear, race access) after a new game clears the ToolManager inventory. At bootstrap
    /// <see cref="SpawnBench"/> runs during Awake, but <see cref="GameManager.StartNewGame"/>
    /// clears the bag right after, so the grants above alone would never be visible — call this
    /// from the new-game entry points post-clear.
    /// </summary>
    public void GrantBenchBag()
    {
        if (EnableTools) SpawnToolKit();
        if (EnableWeapons) SpawnAllWeapons();
        if (EnableRaces) GrantRaceAccess();
        if (EnableSkills) GrantAllSkills();
        if (EnableGear) GrantStarterGear();
        TryDeferPlayerGrants();
    }

    /// <summary>
    /// Fixes the arena footprint on the REAL procedural terrain (the "similar method to generate
    /// the world" — the platform GameObjects are gone): samples the height over the whole footprint
    /// at tile resolution using the world's own noise function, and levels the pad UP to the true
    /// maximum so nothing inside can poke through the bench. The pad itself is carved in place by
    /// <see cref="WorldStreamer.FlattenAt"/> once its chunks finish streaming.
    /// </summary>
    private void PrepareArenaGround()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
            return;

        float half = PlatformSize * 0.5f;
        float cx = PlatformCenter.x;
        float cz = PlatformCenter.z;
        long seed = streamer.Seed;

        // Sample every 2m across the FULL footprint (61x61 grid instead of 121x121 — the world's
        // own generator fills the 1m tiles in between). Iterating i/j over the footprint with a
        // 2 m stride (not stepping the whole 120 m by 1 m from one edge) matters: the old loop
        // only covered half the pad, so a taller far corner could poke through the "level" arena.
        int steps = Mathf.Max(2, Mathf.RoundToInt(PlatformSize * 0.5f));
        float stepSize = PlatformSize / steps;
        float maxY = float.MinValue;
        float minY = float.MaxValue;
        for (int i = 0; i <= steps; i++)
        {
            float wx = cx - half + i * stepSize;
            for (int j = 0; j <= steps; j++)
            {
                float wz = cz - half + j * stepSize;
                float y = TerrainNoiseGenerator.GetHeight(seed, wx, wz);
                if (y > maxY) maxY = y;
                if (y < minY) minY = y;
            }
        }
        if (maxY == float.MinValue)
            maxY = TerrainNoiseGenerator.GetHeight(seed, cx, cz);
        if (minY == float.MaxValue)
            minY = maxY;

        PlatformCenter.y = maxY;
        PlatformTopY = maxY;
        PlatformMinX = cx - half;
        PlatformMaxX = cx + half;
        PlatformMinZ = cz - half;
        PlatformMaxZ = cz + half;

        // Rim blend: wider when the raw terrain inside the pad spans a bigger height range, so a
        // tall mesa ramps down into the untouched hills instead of dropping off a cliff wall.
        _flattenFeather = Mathf.Max(3f, (maxY - minY) * 0.2f);
    }

    /// <summary>
    /// Waits until every streamed terrain chunk under the arena footprint (+ rim feather margin)
    /// is loaded before flattening, so the carve always edits the real chunk tiles. Default boot
    /// puts the arena inside the render radius (the streamer streams around the player focus and
    /// fills that ring over ~1s). If the chunks never load (arena moved far from the focus) it
    /// gives up after 15s and the bench spawns on whatever terrain exists.
    /// </summary>
    private System.Collections.IEnumerator WaitForArenaTerrain()
    {
        var coords = ArenaChunkCoords();
        float deadline = Time.time + 15f;
        while (Time.time < deadline)
        {
            var streamer = Object.FindAnyObjectByType<WorldStreamer>();
            if (streamer != null)
            {
                bool allLoaded = true;
                for (int i = 0; i < coords.Count; i++)
                {
                    if (!streamer.LoadedChunks.ContainsKey(coords[i]))
                    {
                        allLoaded = false;
                        break;
                    }
                }
                if (allLoaded)
                {
                    _arenaReady = true;
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
        Debug.LogWarning("[NewWorldTestGround] Arena chunks didn't load in time; flattening what's loaded.");
    }

    /// <summary>Terrain chunks overlapping the arena footprint plus the rim blend margin.</summary>
    private List<TerrainChunkCoord> ArenaChunkCoords()
    {
        float margin = PlatformSize * 0.5f + _flattenFeather + 1f;
        int minX = Mathf.FloorToInt(PlatformCenter.x - margin);
        int maxX = Mathf.FloorToInt(PlatformCenter.x + margin);
        int minZ = Mathf.FloorToInt(PlatformCenter.z - margin);
        int maxZ = Mathf.FloorToInt(PlatformCenter.z + margin);

        var result = new List<TerrainChunkCoord>();
        for (int cx = minX; cx <= maxX; cx++)
        {
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                var tc = TerrainChunkCoord.FromTile(new ChunkCoord(cx, cz));
                if (!result.Contains(tc))
                    result.Add(tc);
            }
        }
        return result;
    }

    /// <summary>
    /// Carves the arena: levels the loaded procedural terrain across the footprint to
    /// <see cref="PlatformTopY"/> with a feathered rim, then clears any props already spawned
    /// inside the pad (new tree/rock spawns are suppressed by <see cref="ChunkObject"/>).
    /// </summary>
    private void FlattenArenaTerrain()
    {
        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
            return;

        float half = PlatformSize * 0.5f;
        streamer.FlattenAt(new Vector3(PlatformCenter.x, 0f, PlatformCenter.z), half, PlatformTopY, _flattenFeather);
        _arenaReady = true;
    }

    public Vector3 GetSpawnPoint()
    {
        // Prefer the flattened pad top once prepared; before that, fall back to the configured
        // center height so callers never get a pit the arena didn't actually carve.
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
            Vector3 pos = new Vector3(startX + i * 3.5f, PlatformCenter.y + 0.1f, z);
            var field = wb.TillGround(pos);
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
            go.transform.position = new Vector3(startX + i * 4f, PlatformCenter.y + 0.1f, z);
            var live = go.AddComponent<Livestock>();
            live.Type = all[i];
            Livestock.BuildModelInto(go.transform, all[i]);
        }
    }

    private void SpawnEnemies()
    {
        string[] ids = { "slime", "wolf", "goblin", "skeleton", "bat" };
        float z = PlatformCenter.z - PlatformSize * 0.3f;
        float startX = PlatformCenter.x - 10f;
        for (int i = 0; i < ids.Length; i++)
        {
            var go = new GameObject("TestEnemy_" + ids[i]);
            go.transform.position = new Vector3(startX + i * 6f, PlatformCenter.y + 0.05f, z);
            go.AddComponent<SphereCollider>();
            go.AddComponent<EnemyController>().ApplyEnemyId(ids[i]);
        }

        // Two indestructible training dummies — damage testing (plain vs 50% damage reduction).
        SpawnDummy("TestDummy_Plain", startX - 4f, z, 0f);
        SpawnDummy("TestDummy_Armored", startX + 2f, z, 0.5f);

        if (IncludeBoss)
            SpawnBoss();
    }

    private void SpawnDummy(string name, float x, float z, float damageReduction)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(x, PlatformCenter.y + 0.05f, z);
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
        Vector3 pos = new Vector3(PlatformCenter.x, PlatformCenter.y + 0.1f, PlatformCenter.z - PlatformSize * 0.42f);
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
            wb.SpawnBuildingDirect(types[i], new Vector3(startX + i * 8f, PlatformCenter.y, z), 0);
    }

    private void SpawnNpcs()
    {
        if (_npcPlacer == null) return;

        float z = PlatformCenter.z - PlatformSize * 0.18f;
        float startX = PlatformCenter.x + 6f;
        _npcPlacer.Place("test_vendor", "QA Merchant", NpcRoleKind.Vendor, NpcShopMode.Tools, new Vector3(startX, PlatformCenter.y, z), "fishshop");
        _npcPlacer.Place("test_quest", "QA Hermit", NpcRoleKind.QuestGiver, NpcShopMode.Vendor, new Vector3(startX + 4f, PlatformCenter.y, z));
        _npcPlacer.Place("test_follower", "QA Companion", NpcRoleKind.Follower, NpcShopMode.Vendor, new Vector3(startX + 8f, PlatformCenter.y, z));
        _npcPlacer.Place("test_grocer", "QA Grocer", NpcRoleKind.Vendor, NpcShopMode.Grocery, new Vector3(startX + 12f, PlatformCenter.y, z));
        _npcPlacer.Place("test_cafe", "QA Cafe", NpcRoleKind.Vendor, NpcShopMode.Cafe, new Vector3(startX + 16f, PlatformCenter.y, z));
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
    /// (see <see cref="ToolManager.TryPickupNearby"/>). Idempotent so a new game (which wipes the
    /// bag) can re-seed without duplicating pickups still sitting on the ground.
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

        float groundY = PlatformTopY != float.MinValue ? PlatformTopY : PlatformCenter.y;
        float x = PlatformCenter.x + PlatformSize * 0.42f;
        float startZ = PlatformCenter.z - 26f;
        int total = kit.Length + extras.Length;
        float step = 52f / Mathf.Max(1, total - 1);

        int i = 0;
        foreach (var type in kit)
            SpawnToolPickup(wb, type, new Vector3(x, groundY + 0.15f, startZ + (i++) * step), 1);
        foreach (var type in extras)
            SpawnToolPickup(wb, type, new Vector3(x, groundY + 0.15f, startZ + (i++) * step), 5);
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
            // Out of combat at boot → keep the starter weapon sheathed on the body until toggled.
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
            stand.transform.position = new Vector3(x, PlatformTopY + 0.15f, startZ + i * step);
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
    /// Equip a representative starter set into the player's 21-slot equipment system (testing),
    /// so the humanoid Equipment tab has something to show. Also wires up the ClassUnlocker for
    /// the Class tab — its Start() evaluates requirements automatically.
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
    /// Wire the player's active-race manager, unlock the full 22-race roster, and grant a few
    /// Ritual Stones (testing) so the Character Info Race tab can exercise race changes.
    /// </summary>
    private void GrantRaceAccess()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var mgr = player.GetComponent<RaceChangeManager>();
        if (mgr == null)
            mgr = player.gameObject.AddComponent<RaceChangeManager>();

        var unlocker = RaceUnlockManager.Instance;
        var roster = RaceDatabase.BuildDefaultRoster();
        if (unlocker != null && roster != null)
        {
            foreach (var r in roster)
                if (r != null && !string.IsNullOrEmpty(r.raceId))
                    unlocker.UnlockRace(r.raceId);
        }

        var tm = ToolManager.Instance;
        if (tm != null && tm.CountItem(RaceChangeManager.RitualStoneItemId) < 3)
            tm.AddItem(RaceChangeManager.RitualStoneItemId, 3 - tm.CountItem(RaceChangeManager.RitualStoneItemId));
    }

    private static Material SolidMaterial(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { color = c };
        return mat;
    }
}