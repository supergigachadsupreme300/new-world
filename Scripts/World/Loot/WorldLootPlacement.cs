using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seeds world loot — chests and hidden drops — around the map (planning Task 5.2 "world
/// loot placement"). A simple programmatic bucket so items appear even before the POI system
/// places them; designers can also author containers by hand and skip this.
/// </summary>
public class WorldLootPlacement : MonoBehaviour
{
    [Header("Chest Seeding")]
    public BiomeType [] SeedBiomes = { BiomeType.Forest, BiomeType.Swamp, BiomeType.Deep };
    [Tooltip("Guaranteed item id handed out by seeded chests in tight spots.")]
    public string HiddenDropItem = "healing_potion";
    [Min(1)] public int HiddenDropCount = 2;

    private readonly List<LootContainer> _containers = new List<LootContainer>();

    /// <summary>Place a chest at a world position with a given table (linked to a biome).</summary>
    public LootContainer PlaceChest(BiomeType biome, Vector3 position, LootTable table)
    {
        var go = new GameObject("Chest_" + biome + "_" + _containers.Count);
        go.transform.SetParent(transform);
        go.transform.position = position;
        var container = go.AddComponent<LootContainer>();
        container.Loot = table;
        container.GuaranteedItemId = HiddenDropItem;
        container.GuaranteedCount = HiddenDropCount;
        _containers.Add(container);
        return container;
    }

    /// <summary>Simple demonstration seeding around an anchor (used at load / debug).</summary>
    public void SeedDemoChests(Vector3 anchor, float radius)
    {
        foreach (var biome in SeedBiomes)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * radius;
            offset.y = 0f;
            var table = BuildBiomeChestTable(biome);
            PlaceChest(biome, anchor + offset, table);
        }
    }

    private static LootTable BuildBiomeChestTable(BiomeType biome)
    {
        var table = new LootTable();
        table.Entries = new List<LootEntry>();

        var material = ResourceOf(biome);
        if (material != null)
        {
            table.Entries.Add(new LootEntry
            {
                Item = material,
                Weight = 1f,
                MinCount = 1,
                MaxCount = 3,
                Chance = 0.9f
            });
        }
        table.Entries.Add(new LootEntry
        {
            Item = ItemDatabase.Get("healing_potion"),
            Weight = 0.6f,
            MinCount = 1,
            MaxCount = 2,
            Chance = 0.5f
        });
        return table;
    }

    private static ItemData ResourceOf(BiomeType biome)
    {
        switch (biome)
        {
            case BiomeType.Plains: return ItemDatabase.Get("herb");
            case BiomeType.Forest: return ItemDatabase.Get("wood");
            case BiomeType.Mountains: return ItemDatabase.Get("ore");
            case BiomeType.Swamp: return ItemDatabase.Get("poison");
            case BiomeType.Desert: return ItemDatabase.Get("ancient_relic");
            case BiomeType.Tundra: return ItemDatabase.Get("frost_crystal");
            case BiomeType.Volcanic: return ItemDatabase.Get("obsidian");
            case BiomeType.Deep: return ItemDatabase.Get("dark_crystal");
            case BiomeType.Ocean: return ItemDatabase.Get("pearl");
            default: return null;
        }
    }
}