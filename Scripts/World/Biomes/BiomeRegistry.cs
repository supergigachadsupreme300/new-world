using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of the nine biomes (game-design §7.1, planning Task 5.1). Builds the
/// programmatic roster (matching the race-database pattern) so enemy spawns can resolve a
/// biome's enemy table, night factor (§7.3), and tier without asset wiring. Individual
/// .asset overrides still take precedence when the player assigns them in the Inspector.
/// </summary>
public static class BiomeRegistry
{
    private static readonly Dictionary<BiomeType, BiomeData> _byType =
        new Dictionary<BiomeType, BiomeData>();

    private static bool _built;

    private static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        Register(BuildAll());
    }

    private static void Register(IEnumerable<BiomeData> all)
    {
        foreach (var b in all)
            if (b != null && !_byType.ContainsKey(b.Type))
                _byType.Add(b.Type, b);
    }

    /// <summary>All nine biome definitions, built in order.</summary>
    public static List<BiomeData> All
    {
        get
        {
            EnsureBuilt();
            return new List<BiomeData>(_byType.Values);
        }
    }

    /// <summary>Get a biome definition, or null if unknown.</summary>
    public static BiomeData Get(BiomeType type)
    {
        EnsureBuilt();
        return _byType.TryGetValue(type, out var b) ? b : null;
    }

    /// <summary>Night time-of-day factor (§7.3): enemies stronger at night.</summary>
    public const float NightFactor = 1.15f;

    // ---------------------------------------------------------------
    //  PROGRAMMATIC ROSTER (game-design §7.1 enemy sets)
    // ---------------------------------------------------------------

    private static BiomeData Make(BiomeType type, string display, string[] ids, float[] weights,
        float density, Color tint, float nightChance = 0.25f)
    {
        var b = ScriptableObject.CreateInstance<BiomeData>();
        b.name = "Biome_" + type;
        b.Type = type;
        b.displayName = display;
        b.SpawnEnemyIds = ids;
        b.SpawnWeights = weights;
        b.BaseDensity = density;
        b.Tint = tint;
        b.NightVariantChance = nightChance;
        return b;
    }

    private static BiomeData[] BuildAll()
    {
        return new[]
        {
            Make(BiomeType.Plains, "Plains", new[] { "slime", "wolf" }, new[] { 60f, 40f }, 4f, new Color(0.6f, 0.85f, 0.5f)),
            Make(BiomeType.Forest, "Forest", new[] { "bandit", "treant" }, new[] { 55f, 45f }, 5f, new Color(0.3f, 0.6f, 0.3f)),
            Make(BiomeType.Mountains, "Mountains", new[] { "golem", "drake" }, new[] { 60f, 40f }, 3f, new Color(0.6f, 0.6f, 0.7f)),
            Make(BiomeType.Swamp, "Swamp", new[] { "undead", "slug" }, new[] { 55f, 45f }, 5f, new Color(0.4f, 0.5f, 0.3f)),
            Make(BiomeType.Desert, "Desert", new[] { "scorpion", "mummy" }, new[] { 60f, 40f }, 4f, new Color(0.9f, 0.8f, 0.5f)),
            Make(BiomeType.Tundra, "Tundra", new[] { "yeti", "ice_wolf" }, new[] { 55f, 45f }, 4f, new Color(0.8f, 0.9f, 1.0f)),
            Make(BiomeType.Volcanic, "Volcanic", new[] { "fire_elemental", "dragon" }, new[] { 65f, 35f }, 3f, new Color(0.9f, 0.4f, 0.2f), 0.1f),
            Make(BiomeType.Deep, "Deep", new[] { "demon", "mimic" }, new[] { 55f, 45f }, 4f, new Color(0.45f, 0.35f, 0.5f), 0.0f),
            Make(BiomeType.Ocean, "Ocean", new[] { "sea_creature" }, new[] { 100f }, 3f, new Color(0.3f, 0.5f, 0.9f)),
        };
    }

    /// <summary>Density multiplier applied for night/heavier spawns. Reserved for difficulty scaling.</summary>
    public static float DifficultyDensityMultiplier()
    {
#if UNITY_EDITOR
        return 1f;
#else
        return 1f;
#endif
    }
}