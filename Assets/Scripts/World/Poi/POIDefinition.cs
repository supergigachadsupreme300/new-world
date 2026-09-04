using System;
using UnityEngine;

/// <summary>
/// Kinds of point of interest placed by <see cref="POIGenerator"/> (planning Task 5.3,
/// game-design §7.2). Towns host NPCs/shops/crafting; dungeons hold rooms/enemies/boss/loot;
/// boss arenas isolate a boss; fishing spots sit on water; fast-travel nodes become signs.
/// </summary>
public enum PoiKind
{
    Town = 0,
    Dungeon = 1,
    BossArena = 2,
    Fishing = 3,
    FastTravel = 4,
    Farming = 5,
    HiddenCave = 6,
    SkillBook = 7
}

/// <summary>
/// A single point of interest definition (planning Task 5.3). Data-only so the generator can
/// place it without hero-coding; <see cref="POIGenerator"/> turns each definition into a world
/// object (fast-travel signs, town/shop/craft stations markers, dungeon spawns, boss, loot).
/// </summary>
[CreateAssetMenu(fileName = "POI", menuName = "New World/World/Point of Interest", order = 71)]
public class POIDefinition : ScriptableObject
{
    [Tooltip("Stable id (e.g. 'plains_1', 'dungeon_deep_1').")]
    public string Id;
    public string DisplayName;
    public PoiKind Kind;

    [Header("Placement")]
    public BiomeType Biome = BiomeType.Plains;
    [Tooltip("Local offset from the generator anchor where this POI sits.")]
    public Vector3 LocalPosition;
    [Tooltip("World-space footprint (radius / half-extent) used for gating and radius checks.")]
    public float Radius = 8f;

    [Header("Dungeon / Arena")]
    [Tooltip("For Dungeon/Arena: number of linked rooms (dungeon) or 1 (arena).")]
    [Min(1)] public int RoomCount = 1;
    [Tooltip("Enemy id list spawned inside a dungeon/arena; parents to rooms.")]
    public string[] EnemyIds;
    public string BossId;
    [Min(50)] public int BossMaxHealth = 500;
    public bool HasBoss;

    [Header("Fast Travel")]
    [Tooltip("Register a FastTravelSign here so the fast-travel menu can list this POI.")]
    public bool IsFastTravelPoint = true;

    [Header("Content")]
    [Tooltip("Loot scene id wired to a LootTable/chest when the generator builds a container.")]
    public string LootTableId;
}