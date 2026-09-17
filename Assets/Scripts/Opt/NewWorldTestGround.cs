using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Testing ground for the open world. Drop this ONE component on a GameObject and it lays a
/// deterministic test bench on the REAL procedural terrain, which is left untouched: the bench
/// samples the world's own 5-octave noise (the same <see cref="WorldStreamer"/> terrain the world
/// is built from) and places every prop on the natural ground height — no artificial floor and no
/// carve/flatten, so the terrain at the arena coordinate is never edited. Contents: the player
/// tool/seed kit, a farming plot, livestock pens, an enemy arena, a building row, an NPC row, and
/// a crafted fast-travel POI hub. It composes existing public APIs only (no rewrites of live
/// contracts), so every Phase 4-8/9 system can be exercised from a single area.
///
/// Opt-in lanes via serialized toggles; spawn once (idempotent) on <see cref="AutoSpawnOnStart"/>.
/// </summary>
public sealed class NewWorldTestGround : MonoBehaviour
{
    [Header("Arena")]
    [Tooltip("Footprint of the test arena (X/Z world units). It marks the bench layout area; the terrain itself is left untouched.")]
    public float PlatformSize = 120f;
    [Tooltip("Centre of the arena in world space. Its Y is snapped to the sampled ground height.")]
    public Vector3 PlatformCenter = new Vector3(0f, 50f, 0f);
    [Tooltip("Wait for the arena's terrain chunks to stream in, then place the player onto the natural ground (no terrain is edited).")]
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
    private bool _groundSampled;
    private bool _spawnGroundReady;
    private long _worldSeed;
    private WorldStreamer _streamer;
    private readonly List<WeaponRackStand> _rackStands = new List<WeaponRackStand>();
    private ContextPromptUI _contextPrompt;
    private PlayerController _playerController;

    /// <summary>True when the arena's terrain chunks are in place (never a void to spawn into).</summary>
    public bool IsArenaReady => _arenaReady && PlatformTopY != float.MinValue;

    /// <summary>Natural ground height at the arena centre, sampled from the world's own noise.</summary>
    public static float PlatformTopY { get; private set; } = float.MinValue;

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
    /// the arena is enabled it first waits for the terrain under the arena spawn point to stream
    /// in (HARD gate — the player is never pulled over unloaded ground), then PULLS THE PLAYER
    /// ONTO THE NATURAL GROUND before spawning any lane, so the player is never left in the void
    /// ("ground first, then player"). The terrain itself is never edited — every lane just samples
    /// the ground height with <see cref="GroundAt"/> and sits on it. Every lane runs isolated — a
    /// failure in one (e.g. one enemy spawn) logs an error instead of aborting the bench and
    /// stranding the player.
    /// </summary>
    private System.Collections.IEnumerator RunBenchSpawn()
    {
        if (_spawned) yield break;
        _spawned = true;

        if (CreatePlatform)
        {
            // "Ground first, then player": first a HARD gate — never pull the player over terrain
            // that isn't loaded (the chunk under the arena spawn point streams among the first, so
            // this is fast and simply removes the void-fall race). Then a SOFT wait for the whole
            // footprint so tile-dependent lanes (farming tills real soil, the NPC placer, buildings)
            // have their terrain; it gives up after a deadline rather than blocking boot on the far
            // corners. The player is placed once the spawn ground is assured.
            yield return StartCoroutine(WaitForSpawnGround());
            yield return StartCoroutine(WaitForArenaTerrain());
            if (_spawnGroundReady)
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

        // Safety net: if the arena returned partial (e.g. the footprint wait timed out), pull the player
        // onto the bench point at the very end. PlacePlayerOnArena self-guards — it only teleports
        // over a chunk that is actually loaded (mesh + collider), so a slow streamer can never drop
        // the player into the void; they simply stay on the solid boot chunk.
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

    /// <summary>Teleport the player onto the arena (2 m above the sampled ground). Never fires over
    /// unloaded terrain — the chunk under the spawn point must actually be loaded (mesh + collider
    /// applied) before the player is moved, otherwise they stay where they are.</summary>
    private void PlacePlayerOnArena()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null || !streamer.LoadedChunks.ContainsKey(SpawnGroundChunk()))
        {
            Debug.LogWarning("[NewWorldTestGround] Skipped arena teleport — spawn ground not loaded yet.");
            return;
        }
        player.transform.position = GetSpawnPoint();
    }

    /// <summary>
    /// Spawn the whole test bench through the budgeted coroutine (safe to call repeatedly). The
    /// arena chunk-wait runs async, so a sync caller just starts the async path.
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
    /// Reads the natural ground height at the arena centre from the world's own noise function.
    /// The terrain is left completely untouched — no carve, no flatten, no persistence writes —
    /// <see cref="PlatformTopY"/> is just the sampled height the bench lanes key their placement
    /// off. Also caches the world seed so every lane can sample <see cref="GroundAt"/> at its own
    /// anchor for per-item ground placement.
    /// </summary>
    private void PrepareArenaGround()
    {
        _streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (_streamer == null)
            return;

        _worldSeed = _streamer.Seed;
        _groundSampled = true;
        PlatformCenter.y = TerrainNoiseGenerator.GetHeight(_worldSeed, PlatformCenter.x, PlatformCenter.z);
        PlatformTopY = PlatformCenter.y;
    }

    /// <summary>Natural ground height at a world position. Prefers the LIVE terrain (the loaded tile's
    /// own corner heights, so it honours saved deformations from earlier runs and Earth-spell edits)
    /// and falls back to the world's 5-octave noise for tiles that aren't loaded yet, then to the
    /// configured centre height when the world seed was never captured.</summary>
    private float GroundAt(float x, float z)
    {
        if (!_groundSampled) return PlatformCenter.y;

        var tile = new ChunkCoord(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
        if (_streamer != null && _streamer.TryGetData(tile, out ChunkData data) && data.IsValid)
        {
            // Bilinear sample of the loaded quad's 4 corners (NW/NE/SE/SW).
            float tx = Mathf.Clamp01(x - tile.X);
            float tz = Mathf.Clamp01(z - tile.Z);
            float top = Mathf.Lerp(data.Heights[0], data.Heights[1], tx);
            float bottom = Mathf.Lerp(data.Heights[3], data.Heights[2], tx);
            return Mathf.Lerp(bottom, top, tz);
        }
        return TerrainNoiseGenerator.GetHeight(_worldSeed, x, z);
    }

    /// <summary>Terrain chunk directly under the arena spawn point — the only ground the player
    /// absolutely needs before being pulled onto the bench.</summary>
    private TerrainChunkCoord SpawnGroundChunk()
    {
        return TerrainChunkCoord.FromWorld(new Vector3(PlatformCenter.x, 0f, PlatformCenter.z + PlatformSize * 0.45f));
    }

    /// <summary>
    /// HARD gate for "ground first, then player": waits until the chunk under the arena spawn point
    /// is loaded (mesh + collider applied), so the player is NEVER teleported over unloaded terrain.
    /// That chunk lies near the boot focus so it streams among the first; this wait just makes the
    /// guarantee load-independent instead of racing the footprint vote timer. If streaming never
    /// delivers it (30s) the player simply stays grounded on the boot chunk — no void fall.
    /// </summary>
    private System.Collections.IEnumerator WaitForSpawnGround()
    {
        TerrainChunkCoord target = SpawnGroundChunk();
        float deadline = Time.time + 30f;
        while (Time.time < deadline)
        {
            var streamer = Object.FindAnyObjectByType<WorldStreamer>();
            if (streamer != null && streamer.LoadedChunks.ContainsKey(target))
            {
                _spawnGroundReady = true;
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
        _spawnGroundReady = false;
        Debug.LogError("[NewWorldTestGround] Arena spawn ground never streamed in (30s). Player stays on the boot chunk — bench lanes still spawn.");
    }

    /// <summary>
    /// SOFT gate: waits until every streamed terrain chunk under the arena footprint is loaded so
    /// the tile-dependent lanes (farming tills real soil, the NPC placer, buildings) have their
    /// terrain. Unlike <see cref="WaitForSpawnGround"/> this is not a correctness requirement and
    /// gives up after 15s (warning) rather than blocking boot on the far corners; the player ground
    /// is already assured by the hard gate, and lanes soft-fail via <see cref="RunSafely"/>.
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
        Debug.LogWarning("[NewWorldTestGround] Arena chunks didn't load in time; placing the bench on whatever terrain exists.");
    }

    /// <summary>Terrain chunks overlapping the arena footprint (placement uses real tile heights).</summary>
    private List<TerrainChunkCoord> ArenaChunkCoords()
    {
        float margin = PlatformSize * 0.5f + 1f;
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

    public Vector3 GetSpawnPoint()
    {
        // Sample the natural ground at the player's XZ anchor so the +2 offset always lands on real
        // terrain; before that, fall back to the configured center height so callers never get a
        // pit the arena didn't actually carve.
        float groundY = PlatformTopY != float.MinValue
            ? GroundAt(PlatformCenter.x, PlatformCenter.z + PlatformSize * 0.45f)
            : PlatformCenter.y;
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
            Vector3 pos = new Vector3(x, GroundAt(x, z) + 0.1f, z);
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
            float x = startX + i * 4f;
            go.transform.position = new Vector3(x, GroundAt(x, z) + 0.1f, z);
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
            float eX = startX + i * 6f;
            go.transform.position = new Vector3(eX, GroundAt(eX, z) + 0.05f, z);
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
        go.transform.position = new Vector3(x, GroundAt(x, z) + 0.05f, z);
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
        Vector3 pos = new Vector3(PlatformCenter.x, GroundAt(PlatformCenter.x, bz) + 0.1f, bz);
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
            wb.SpawnBuildingDirect(types[i], new Vector3(bx, GroundAt(bx, z), z), 0);
        }
    }

    private void SpawnNpcs()
    {
        if (_npcPlacer == null) return;

        float z = PlatformCenter.z - PlatformSize * 0.18f;
        float startX = PlatformCenter.x + 6f;
        _npcPlacer.Place("test_vendor", "QA Merchant", NpcRoleKind.Vendor, NpcShopMode.Tools, new Vector3(startX, GroundAt(startX, z), z), "fishshop");
        _npcPlacer.Place("test_quest", "QA Hermit", NpcRoleKind.QuestGiver, NpcShopMode.Vendor, new Vector3(startX + 4f, GroundAt(startX + 4f, z), z));
        _npcPlacer.Place("test_follower", "QA Companion", NpcRoleKind.Follower, NpcShopMode.Vendor, new Vector3(startX + 8f, GroundAt(startX + 8f, z), z));
        _npcPlacer.Place("test_grocer", "QA Grocer", NpcRoleKind.Vendor, NpcShopMode.Grocery, new Vector3(startX + 12f, GroundAt(startX + 12f, z), z));
        _npcPlacer.Place("test_cafe", "QA Cafe", NpcRoleKind.Vendor, NpcShopMode.Cafe, new Vector3(startX + 16f, GroundAt(startX + 16f, z), z));
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

        float x = PlatformCenter.x + PlatformSize * 0.42f;
        float startZ = PlatformCenter.z - 26f;
        int total = kit.Length + extras.Length;
        float step = 52f / Mathf.Max(1, total - 1);

        int i = 0;
        foreach (var type in kit)
        {
            float iz = startZ + i * step;
            SpawnToolPickup(wb, type, new Vector3(x, GroundAt(x, iz) + 0.15f, iz), 1);
            i++;
        }
        foreach (var type in extras)
        {
            float iz = startZ + i * step;
            SpawnToolPickup(wb, type, new Vector3(x, GroundAt(x, iz) + 0.15f, iz), 5);
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
            float iz = startZ + i * step;
            stand.transform.position = new Vector3(x, GroundAt(x, iz) + 0.15f, iz);
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