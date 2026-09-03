using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of points of interest (planning Task 5.3). Programmatic roster mirrors the
/// biome/item-database pattern: <see cref="POIGenerator"/> consumes <see cref="All"/> to place
/// towns, dungeons, boss arenas, fishing spots, and fast-travel nodes per biome. Individual
/// .asset overrides take precedence when assigned in the Inspector.
/// </summary>
public static class POIRegistry
{
    private static readonly List<POIDefinition> _all = new List<POIDefinition>();
    private static readonly Dictionary<string, POIDefinition> _byId = new Dictionary<string, POIDefinition>();
    private static bool _built;

    private static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        Register(BuildAll());
    }

    private static void Register(IEnumerable<POIDefinition> pois)
    {
        foreach (var p in pois)
        {
            if (p == null || string.IsNullOrEmpty(p.Id)) continue;
            if (!_byId.ContainsKey(p.Id))
            {
                _byId.Add(p.Id, p);
                _all.Add(p);
            }
        }
    }

    public static List<POIDefinition> All
    {
        get { EnsureBuilt(); return new List<POIDefinition>(_all); }
    }

    /// <summary>All POIs of a given kind across biomes.</summary>
    public static List<POIDefinition> OfKind(PoiKind kind)
    {
        EnsureBuilt();
        var result = new List<POIDefinition>();
        foreach (var p in _all)
            if (p.Kind == kind) result.Add(p);
        return result;
    }

    /// <summary>All POIs matching a biome.</summary>
    public static List<POIDefinition> OfBiome(BiomeType biome)
    {
        EnsureBuilt();
        var result = new List<POIDefinition>();
        foreach (var p in _all)
            if (p.Biome == biome) result.Add(p);
        return result;
    }

    public static POIDefinition Get(string id)
    {
        EnsureBuilt();
        return string.IsNullOrEmpty(id) ? null : (_byId.TryGetValue(id, out var p) ? p : null);
    }

    // ---------------------------------------------------------------
    //  PROGRAMMATIC ROSTER (game-design §7.2 POIs, towns/dungeons/etc.)
    // ---------------------------------------------------------------

    private static POIDefinition Make(string id, string display, PoiKind kind, BiomeType biome,
        Vector3 local, float radius, bool fastTravel = true)
    {
        var p = ScriptableObject.CreateInstance<POIDefinition>();
        p.name = "POI_" + id;
        p.Id = id;
        p.DisplayName = display;
        p.Kind = kind;
        p.Biome = biome;
        p.LocalPosition = local;
        p.Radius = radius;
        p.IsFastTravelPoint = fastTravel;

        if (kind == PoiKind.Dungeon)
        {
            p.HasBoss = true;
            p.RoomCount = 4;
            p.BossMaxHealth = 800;
        }
        else if (kind == PoiKind.BossArena)
        {
            p.HasBoss = true;
            p.RoomCount = 1;
            p.BossMaxHealth = 1200;
        }
        return p;
    }

    private static POIDefinition[] BuildAll()
    {
        return new[]
        {
            // Towns (one per "civilized" biome; NPCs/shops/crafting live here).
            Make("town_plains", "Plains Town" , PoiKind.Town,       BiomeType.Plains,    new Vector3( 90f, 0f, 120f), 24f),
            Make("town_forest", "Forest Post" , PoiKind.Town,       BiomeType.Forest,    new Vector3(150f, 0f,  40f), 18f),
            Make("town_desert", "Desert Camp" , PoiKind.Town,       BiomeType.Desert,    new Vector3(-160f, 0f, 140f), 18f),
            Make("town_tundra", "Tundra Hold" , PoiKind.Town,       BiomeType.Tundra,    new Vector3( 40f, 0f, -200f), 18f),

            // Dungeons (combat + loot): one per danger biome.
            Make("dungeon_forest", "Mossy Warren", PoiKind.Dungeon,  BiomeType.Forest,   new Vector3( 60f, 0f,  90f), 14f),
            Make("dungeon_deep",   "Abyssal Crypt", PoiKind.Dungeon, BiomeType.Deep,     new Vector3(-40f, 0f, -90f), 16f),
            Make("dungeon_volcanic","Ember Vault",  PoiKind.Dungeon, BiomeType.Volcanic, new Vector3(-140f, 0f, -60f), 16f),

            // Boss arenas (isolated fights).
            Make("boss_plains"    , "Giant Arena"    , PoiKind.BossArena, BiomeType.Plains,    new Vector3( 0f, 0f, 220f), 14f),
            Make("boss_mountains" , "Golem Lair"     , PoiKind.BossArena, BiomeType.Mountains, new Vector3(180f, 0f,  -40f), 14f),
            Make("boss_swamp"     , "Witch Mire"     , PoiKind.BossArena, BiomeType.Swamp,     new Vector3(-90f, 0f,  20f), 14f),
            Make("boss_volcanic"  , "Dragon Crown"   , PoiKind.BossArena, BiomeType.Volcanic,  new Vector3(-220f, 0f,  10f), 18f),

            // Fishing spots (game-design §7.2; sit near/on water).
            Make("fish_plains"    , "Mill Pond"   , PoiKind.Fishing, BiomeType.Plains, new Vector3( 60f, 0f, 180f), 8f),
            Make("fish_swamp"     , "Blackwater"  , PoiKind.Fishing, BiomeType.Swamp,  new Vector3(-60f, 0f,  70f), 8f),
            Make("fish_tundra"    , "Frozen Hole" , PoiKind.Fishing, BiomeType.Tundra, new Vector3(120f, 0f,-220f), 8f),
            Make("fish_ocean"     , "Coral Shallows", PoiKind.Fishing, BiomeType.Ocean, new Vector3(-120f, 0f,-180f), 10f),

            // Hidden caves / secrets / skill books.
            Make("cave_mountains" , "Hidden Grotto", PoiKind.HiddenCave, BiomeType.Mountains, new Vector3( 200f, 0f,  120f), 10f),
            Make("skill_swamp"    , "Lost Tome"   , PoiKind.SkillBook,  BiomeType.Swamp,     new Vector3(-150f, 0f,   60f), 6f),
        };
    }
}