using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places all points of interest on the world map (planning Task 5.3, game-design §7.2).
/// Reads the <see cref="POIRegistry"/> roster and:
///   • towns      → <see cref="Town"/> (NPCs/shops/crafting markers + fast travel + chest)
///   • dungeons   → <see cref="DungeonSystem"/> (rooms, enemies, boss, loot, fast travel)
///   • boss arenas→ <see cref="BossController"/> on a platform + fast travel
///   • fishing    → a water marker + fast travel
///   • fast travel→ <see cref="FastTravelNode"/> (bonfire/sign)
///   • caves/books→ a <see cref="LootContainer"/> treasure + fast travel
/// Fast-travel indices are assigned in order so <see cref="FastTravelMenu"/> lists them cleanly.
/// </summary>
public class POIGenerator : MonoSingleton<POIGenerator>
{
    [Tooltip("Where POIs are placed relative to; usually the world origin (0,0,0).")]
    public Vector3 Anchor = Vector3.zero;

    private int _nextTravelIndex = 1;

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
        AssignTravel(town.TravelSign);
        _placed.Add(town.gameObject);
    }

    private void PlaceDungeon(POIDefinition poi)
    {
        var dungeon = DungeonSystem.Build(transform, Anchor + poi.LocalPosition, poi);
        AssignTravel(dungeon.TravelSign);
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

        var signGo = new GameObject("ArenaSign");
        signGo.transform.SetParent(root.transform);
        signGo.transform.position = at + new Vector3(poi.Radius * 0.85f, 0f, poi.Radius * 0.85f);
        _placed.Add(signGo);
        var sign = signGo.AddComponent<FastTravelSign>();
        sign.Label = poi.DisplayName;
        AssignTravel(sign);
        var scol = signGo.AddComponent<BoxCollider>();
        scol.isTrigger = true;
        scol.size = new Vector3(1.2f, 1.6f, 0.5f);
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

        var signGo = new GameObject("FishingSign");
        signGo.transform.SetParent(root.transform, false);
        signGo.transform.localPosition = new Vector3(poi.Radius * 1.1f, 0.5f, 0f);
        _placed.Add(signGo);
        var sign = signGo.AddComponent<FastTravelSign>();
        sign.Label = poi.DisplayName;
        AssignTravel(sign);
        var scol = signGo.AddComponent<BoxCollider>();
        scol.isTrigger = true;
        scol.size = new Vector3(1.2f, 1.6f, 0.5f);
    }

    private void PlaceFastTravel(POIDefinition poi)
    {
        var node = FastTravelNode.Build(transform, poi, _nextTravelIndex);
        _nextTravelIndex++;
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

        var signGo = new GameObject("TreasureSign");
        signGo.transform.SetParent(transform);
        signGo.transform.position = at + new Vector3(poi.Radius, 0f, 0f);
        _placed.Add(signGo);
        var sign = signGo.AddComponent<FastTravelSign>();
        sign.Label = poi.DisplayName;
        AssignTravel(sign);
        var scol = signGo.AddComponent<BoxCollider>();
        scol.isTrigger = true;
        scol.size = new Vector3(1.2f, 1.6f, 0.5f);
    }

    private void AssignTravel(FastTravelSign sign)
    {
        if (sign == null) return;
        sign.Index = _nextTravelIndex;
        _nextTravelIndex++;
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