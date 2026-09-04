using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A dungeon point of interest (planning Task 5.3, game-design §7.2 "Dungeons (combat, loot)").
/// Builds a chain of rooms (procedural stand-ins), spawns biome-standard enemies per room via
/// <see cref="EnemySpawner"/>, optionally seeds a <see cref="BossController"/> in the final room,
/// drops a <see cref="LootContainer"/> on the boss, and registers a <see cref="FastTravelSign"/>
/// so the fast-travel menu can reach the entrance.
/// </summary>
public class DungeonSystem : MonoBehaviour
{
    [Header("Identity")]
    public POIDefinition Definition;
    public BiomeType DungeonBiome = BiomeType.Deep;

    [Header("Layout")]
    [Min(1)] public int RoomCount = 3;
    public float RoomSpacing = 12f;
    [Min(1)] public int EnemiesPerRoom = 2;
    [Min(50)] public int BossMaxHealth = 600;
    public bool HasBoss = true;
    [Tooltip("Guaranteed dungeon chest loot item id.")]
    public string BossLootItem = "dark_crystal";

    public BossController Boss { get; private set; }
    public LootContainer BossChest { get; private set; }
    public FastTravelSign TravelSign { get; private set; }

    public static DungeonSystem Build(Transform parent, Vector3 worldPosition, POIDefinition poi)
    {
        var root = new GameObject("Dungeon_" + poi.Id);
        root.transform.SetParent(parent);
        root.transform.position = worldPosition;
        var dungeon = root.AddComponent<DungeonSystem>();
        dungeon.Definition = poi;
        dungeon.DungeonBiome = poi.Biome;
        dungeon.RoomCount = Mathf.Max(1, poi.RoomCount);
        dungeon.HasBoss = poi.HasBoss;
        dungeon.BossMaxHealth = poi.BossMaxHealth;
        dungeon.BuildLayout();
        return dungeon;
    }

    private void BuildLayout()
    {
        Vector3 entry = transform.position;
        Vector3 direction = new Vector3(0f, 0f, 1f);
        Vector3 doorDir = new Vector3(1f, 0f, 0f);

        // Entrance fast-travel sign.
        var signGo = new GameObject("DungeonSign");
        signGo.transform.SetParent(transform);
        signGo.transform.position = entry + doorDir * Definition.Radius * 0.9f;
        TravelSign = signGo.AddComponent<FastTravelSign>();
        TravelSign.Label = Definition.DisplayName;
        var scol = signGo.AddComponent<BoxCollider>();
        scol.isTrigger = true;
        scol.size = new Vector3(1.2f, 1.6f, 0.5f);
        BuildRoomFloor(transform, "DungeonEntry", entry, Definition.Radius, new Color(0.42f, 0.42f, 0.46f));

        var spawner = EnemySpawner.Instance;
        for (int r = 0; r < RoomCount; r++)
        {
            Vector3 center = entry + direction * (Definition.Radius + r * RoomSpacing);
            BuildRoomFloor(transform, "DungeonRoom" + r, center, Definition.Radius,
                r == RoomCount - 1 ? new Color(0.5f, 0.3f, 0.3f) : new Color(0.4f, 0.42f, 0.44f));

            bool isBossRoom = r == RoomCount - 1;

            if (isBossRoom && HasBoss && spawner != null)
            {
                Vector3 bossPos = center + direction * 2f;
                Boss = SpawnBoss(transform, bossPos);
            }
            else
            {
                for (int e = 0; e < EnemiesPerRoom; e++)
                {
                    if (spawner == null) break;
                    Vector3 off = doorDir * UnityEngine.Random.Range(-Definition.Radius * 0.5f, Definition.Radius * 0.5f)
                               + direction * UnityEngine.Random.Range(-Definition.Radius * 0.4f, Definition.Radius * 0.4f);
                    spawner.SpawnAt(DungeonBiome, center + off, 1f);
                }
            }
        }

        // Boss chest loot in the final room.
        if (Boss != null)
        {
            var chestGo = new GameObject("DungeonBossChest");
            chestGo.transform.SetParent(transform);
            chestGo.transform.position = Boss.transform.position + direction * Definition.Radius * 0.8f;
            BossChest = chestGo.AddComponent<LootContainer>();
            BossChest.GuaranteedItemId = BossLootItem;
            BossChest.GuaranteedCount = 1;
            BossChest.RequiresInteract = false;
            Boss.OnBossDefeated += OnBossDefeated;
        }
    }

    private BossController SpawnBoss(Transform parent, Vector3 position)
    {
        var bossGo = new GameObject("DungeonBoss");
        bossGo.transform.SetParent(parent);
        bossGo.transform.position = position;
        // Keep inactive until configured so BossController.Awake initializes health from MaxHealth.
        bool wasActive = bossGo.activeSelf;
        bossGo.SetActive(false);
        var boss = bossGo.AddComponent<BossController>();
        boss.BossId = string.IsNullOrEmpty(Definition.BossId) ? "dungeon_boss" : Definition.BossId;
        boss.DisplayName = Definition.DisplayName + " Boss";
        boss.MaxHealth = BossMaxHealth;
        var col = bossGo.AddComponent<BoxCollider>();
        col.isTrigger = false;
        col.size = new Vector3(2f, 2.2f, 2f);
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            boss.SetTarget(player.transform);
        bossGo.SetActive(wasActive);
        return boss;
    }

    private void OnBossDefeated(BossController boss)
    {
        // Chest opens automatically once the boss falls; future: unlock door / reward.
        if (BossChest != null)
        {
            BossChest.Loot = BuildBossLoot();
            BossChest.Open();
        }
    }

    private LootTable BuildBossLoot()
    {
        var table = new LootTable();
        table.Entries = new List<LootEntry>();
        table.Entries.Add(new LootEntry
        {
            Item = ItemDatabase.Get(BossLootItem),
            Weight = 1f,
            MinCount = 2,
            MaxCount = 4,
            Chance = 1f
        });
        table.Entries.Add(new LootEntry
        {
            Item = ItemDatabase.Get("healing_potion"),
            Weight = 0.8f,
            MinCount = 1,
            MaxCount = 3,
            Chance = 0.6f
        });
        return table;
    }

    private static void BuildRoomFloor(Transform parent, string name, Vector3 position, float radius, Color color)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = name;
        floor.transform.SetParent(parent, false);
        floor.transform.localScale = new Vector3(radius * 2f, 0.15f, radius * 2f);
        floor.transform.position = position + Vector3.up * 0f;
        floor.GetComponent<MeshRenderer>().material.color = color;
        Destroy(floor.GetComponent<Collider>());
    }
}