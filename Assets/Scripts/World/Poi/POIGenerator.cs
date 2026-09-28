using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places all points of interest on the world map (planning Task 5.3, game-design §7.2).
/// Reads the <see cref="POIRegistry"/> roster and:
///   • towns      → <see cref="Town"/> (NPC/shop/crafting markers + chest)
///   • dungeons   → <see cref="DungeonSystem"/> (rooms, enemies, boss, loot)
///   • boss arenas→ <see cref="BossController"/> on a platform
///   • fishing    → a water marker
///   • fast travel→ <see cref="FastTravelNode"/> (bonfire/sign)
///   • caves/books→ a <see cref="LootContainer"/> treasure
/// 1hz removed <c>FastTravelSign</c> and <c>FastTravelMenu</c>, so no POI carries a travel index
/// any more: <see cref="FastTravelNode"/> survives purely as a world-map marker for
/// <see cref="PoiKind.FastTravel"/>, and the per-POI sign props are gone.
/// </summary>
public class POIGenerator : MonoSingleton<POIGenerator>
{
    [Tooltip("Where POIs are placed relative to; usually the world origin (0,0,0).")]
    public Vector3 Anchor = Vector3.zero;

    private readonly List<GameObject> _placed = new List<GameObject>();

    /// <summary>Generate all POIs from the registry. Idempotent for a spawned generator.</summary>
    public void Generate()
    {
        foreach (var placed in _placed)
        {
            if (placed != null) Destroy(placed);
        }
        _placed.Clear();

        foreach (var poi in POIRegistry.All)
        {
            Vector3 at = Anchor + poi.LocalPosition;
            switch (poi.Kind)
            {
                case PoiKind.Town:
                    PlaceTown(poi);
                    break;
                case PoiKind.Dungeon:
                    PlaceDungeon(poi);
                    break;
                case PoiKind.BossArena:
                    PlaceBossArena(poi, at);
                    break;
                case PoiKind.Fishing:
                    PlaceFishing(poi, at);
                    break;
                case PoiKind.FastTravel:
                    PlaceFastTravel(poi);
                    break;
                case PoiKind.HiddenCave:
                case PoiKind.SkillBook:
                    PlaceTreasure(poi, at);
                    break;
            }
        }
    }

    private void PlaceTown(POIDefinition poi)
    {
        var town = Town.Build(transform, Anchor + poi.LocalPosition, poi);
        _placed.Add(town.gameObject);
    }

    private void PlaceDungeon(POIDefinition poi)
    {
        var dungeon = DungeonSystem.Build(transform, Anchor + poi.LocalPosition, poi);
        _placed.Add(dungeon.gameObject);
    }

    private void PlaceBossArena(POIDefinition poi, Vector3 at)
    {
        var root = new GameObject("BossArena_" + poi.Id);
        root.transform.SetParent(transform);
        root.transform.position = at;
        _placed.Add(root);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "ArenaFloor";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localScale = new Vector3(poi.Radius * 2f, 0.15f, poi.Radius * 2f);
        floor.GetComponent<MeshRenderer>().material.color = new Color(0.5f, 0.36f, 0.36f);
        Destroy(floor.GetComponent<Collider>());

        var bossGo = new GameObject("Boss_" + (string.IsNullOrEmpty(poi.BossId) ? poi.Id : poi.BossId));
        bossGo.transform.SetParent(root.transform);
        bossGo.transform.position = at + Vector3.zero;
        _placed.Add(bossGo);
        bossGo.SetActive(false); // Configure before Awake so MaxHealth drives CurrentHealth.
        var boss = bossGo.AddComponent<BossController>();
        boss.BossId = string.IsNullOrEmpty(poi.BossId) ? poi.Id : poi.BossId;
        boss.DisplayName = poi.DisplayName;
        boss.MaxHealth = poi.BossMaxHealth > 0 ? poi.BossMaxHealth : 600;
        var col = bossGo.AddComponent<BoxCollider>();
        col.size = new Vector3(2.5f, 2.5f, 2.5f);
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) boss.SetTarget(player.transform);
        bossGo.SetActive(true);
    }

    private void PlaceFishing(POIDefinition poi, Vector3 at)
    {
        var root = new GameObject("Fishing_" + poi.Id);
        root.transform.SetParent(transform);
        root.transform.position = at;
        _placed.Add(root);

        var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Pond";
        water.transform.SetParent(root.transform, false);
        water.transform.localScale = new Vector3(poi.Radius * 2f, 0.12f, poi.Radius * 2f);
        water.GetComponent<MeshRenderer>().material.color = new Color(0.2f, 0.55f, 0.85f);
        var wcol = water.GetComponent<BoxCollider>();
        wcol.isTrigger = true;
        water.AddComponent<WaterVolume>();
    }

    private void PlaceFastTravel(POIDefinition poi)
    {
        var node = FastTravelNode.Build(transform, poi);
        _placed.Add(node.gameObject);
    }

    private void PlaceTreasure(POIDefinition poi, Vector3 at)
    {
        var chestGo = new GameObject("Treasure_" + poi.Id);
        chestGo.transform.SetParent(transform);
        chestGo.transform.position = at;
        _placed.Add(chestGo);
        var chest = chestGo.AddComponent<LootContainer>();
        chest.GuaranteedItemId = poi.Kind == PoiKind.SkillBook ? "skill_book_heal" : "healing_potion";
        chest.GuaranteedCount = 1;
        chest.RequiresInteract = false;
    }

    private void OnDrawGizmosSelected()
    {
        foreach (var poi in POIRegistry.All)
        {
            Vector3 at = Anchor + poi.LocalPosition;
            Gizmos.color = poi.Kind == PoiKind.Dungeon || poi.Kind == PoiKind.BossArena
                ? new Color(1f, 0.3f, 0.3f)
                : poi.Kind == PoiKind.Town ? new Color(0.4f, 0.8f, 1f)
                : new Color(0.6f, 0.9f, 0.6f);
            Gizmos.DrawWireSphere(at, poi.Radius);
        }
    }
}