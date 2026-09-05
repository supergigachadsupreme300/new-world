using UnityEngine;

/// <summary>
/// Testing ground for the open world. Drop this ONE component on a GameObject and it builds a
/// flat platform and lays out a deterministic test bench: the player tool/seed kit, a farming
/// plot, livestock pens, an enemy arena, a building row, an NPC row, and a crafted fast-travel
/// POI hub. It composes existing public APIs only (no rewrites of live contracts), so every
/// Phase 4-8/9 system can be exercised from a single area.
///
/// Opt-in lanes via serialized toggles; spawn once (idempotent) on <see cref="AutoSpawnOnStart"/>.
/// </summary>
public sealed class NewWorldTestGround : MonoBehaviour
{
    [Header("Platform")]
    [Tooltip("Size of the flat test platform (X/Z world units).")]
    public float PlatformSize = 120f;
    [Tooltip("Centre of the platform in world space.")]
    public Vector3 PlatformCenter = new Vector3(0f, 50f, 0f);
    public bool CreatePlatform = true;

    [Header("Spawning")]
    [Tooltip("Spawn the bench automatically on Awake.")]
    public bool AutoSpawnOnStart = true;

    [Header("Lanes")]
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
    [Tooltip("Grant the player all 60 skills (testing) and wire the skill profile + hotkey bindings.")]
    public bool EnableSkills = true;

    private WorldNpcPlacer _npcPlacer;
    private bool _spawned;
    private static readonly int TestRootLayer = 0;

    private void Awake()
    {
        if (CreatePlatform)
            BuildPlatform();

        _npcPlacer = Object.FindAnyObjectByType<WorldNpcPlacer>();
        if (_npcPlacer == null)
        {
            var go = new GameObject("TestNpcPlacer");
            _npcPlacer = go.AddComponent<WorldNpcPlacer>();
            _npcPlacer.AutoPlaceOnStart = false;
        }

        if (AutoSpawnOnStart)
            SpawnBench();
    }

    /// <summary>Build the flat ground + spawn all test lanes. Safe to call repeatedly.</summary>
    public void SpawnBench()
    {
        if (_spawned) return;
        _spawned = true;

        if (EnableTools) SpawnToolKit();
        if (EnableFarming) SpawnFarmingPlot();
        if (EnableLivestock) SpawnLivestock();
        if (EnableEnemies) SpawnEnemies();
        if (EnableBuildings) SpawnBuildings();
        if (EnableNpcs) SpawnNpcs();
        if (EnablePoiHub) RegisterPoiHub();
        if (EnableWeapons) SpawnAllWeapons();
        if (EnableSkills) GrantAllSkills();

        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            player.transform.position = PlatformCenter + new Vector3(0f, 2f, PlatformSize * 0.45f);
        }
    }

    private void BuildPlatform()
    {
        var root = new GameObject("TestPlatformRoot");
        root.transform.position = PlatformCenter;
        root.transform.rotation = Quaternion.identity;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "TestFloor";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localScale = new Vector3(PlatformSize, PlatformSize, 1f);
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localPosition = Vector3.zero;
        var mr = floor.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.sharedMaterial = SolidMaterial(ColorPalette.GrassGreen);
        Destroy(floor.GetComponent<Collider>());

        // A thin under-collider so the player physically stands on the platform.
        var solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        solid.name = "TestFloorCollider";
        solid.transform.SetParent(root.transform, false);
        solid.transform.localScale = new Vector3(PlatformSize, 0.2f, PlatformSize);
        solid.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        var smr = solid.GetComponent<MeshRenderer>();
        if (smr != null) Destroy(smr);
        solid.layer = TestRootLayer;

        // Boundary poles for orientation.
        for (int i = 0; i < 4; i++)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.name = "Corner_" + i;
            pole.transform.SetParent(root.transform, false);
            float s = PlatformSize * 0.5f;
            float signX = (i % 2 == 0) ? -1f : 1f;
            float signZ = (i < 2) ? -1f : 1f;
            pole.transform.localScale = new Vector3(0.6f, 4f, 0.6f);
            pole.transform.localPosition = new Vector3(signX * s, 2f, signZ * s);
        }
    }

    private void SpawnToolKit()
    {
        var tm = ToolManager.Instance;
        if (tm == null) return;

        // The 10-slot tool rack — a representative discovery kit.
        string[] kit = new[]
        {
            "axe", "pickaxe", "hoe", "hammer", "scythe", "watering_can",
            "fertilizer", "club", "rosary", "fishing_rod"
        };
        foreach (var type in kit)
            tm.AddItem(type, 1);

        // Seeds + foods round out the visible inventory where slots remain.
        string[] extras = new[]
        {
            "wheat_seed", "corn_seed", "tomato_seed", "rice_seed",
            "banh_mi", "com_tam", "nuoc_dau", "mi_chinh", "xap_phong"
        };
        foreach (var type in extras)
        {
            if (!tm.CanHoldItem(type)) break;
            tm.AddItem(type, 5);
        }
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

        if (IncludeBoss)
            SpawnBoss();
    }

    private void SpawnBoss()
    {
        Vector3 pos = new Vector3(PlatformCenter.x, PlatformCenter.y + 1f, PlatformCenter.z - PlatformSize * 0.42f);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "TestBoss";
        go.transform.localScale = new Vector3(3f, 3f, 3f);
        go.transform.position = pos;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.sharedMaterial = SolidMaterial(new Color(0.4f, 0.1f, 0.15f));
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

        string[] types = { "small_house", "wood_wall", "door", "table", "chair", "well", "fence", "chest" };
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
    /// Ensure the player has the Phase 10 combat/skill stack and equip the starter weapon.
    /// All 15 weapons remain available to cycle via Character Info > Equipment.
    /// </summary>
    private void SpawnAllWeapons()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        WeaponCatalog.EnsureBuilt();
        WeaponRigBuilder.EnsureCombatStack(player.gameObject);

        var starter = WeaponCatalog.Find(WeaponCatalog.StarterWeaponId);
        if (starter != null)
            WeaponRigBuilder.EquipInto(player.gameObject, starter);
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

        SkillCatalog.EnsureBuilt();
        var profile = player.GetComponent<SkillProfile>();
        if (profile == null) return;

        // Chest list (stable) so re-entry does not double-grant.
        var toLearn = SkillCatalog.All;
        if (toLearn == null) return;
        foreach (var skill in toLearn)
        {
            if (skill == null) continue;
            if (profile.HasLearned(skill.id)) continue;
            profile.Points += 999;          // testing: unlimited budget
            profile.Learn(skill);
        }
    }

    private static Material SolidMaterial(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { color = c };
        return mat;
    }
}