using System;
using UnityEngine;

/// <summary>
/// The nine open-world biomes (game-design §7.1, planning Task 5.1). Each drives terrain
/// flavor, resource distribution, and the set of enemies that spawn in it.
/// </summary>
public enum BiomeType
{
    Plains = 0,
    Forest = 1,
    Mountains = 2,
    Swamp = 3,
    Desert = 4,
    Tundra = 5,
    Volcanic = 6,
    Deep = 7,
    Ocean = 8
}

/// <summary>
/// Data-only definition of a biome (game-design §7.1). Owns the enemy roster that spawns
/// here, per-game-design enemy sets: Plains → Slimes/Wolves; Forest → Bandits/Treants;
/// Mountains → Golems/Drakes; Swamp → Undead/Slugs; Desert → Scorpions/Mummies;
/// Tundra → Yetis/Ice Wolves; Volcanic → Fire elementals/Dragons; Deep → Demons/Mimics;
/// Ocean → Sea creatures.
/// </summary>
[CreateAssetMenu(fileName = "Biome", menuName = "New World/Biomes/Biome", order = 60)]
public class BiomeData : ScriptableObject
{
    public const int BiomeCount = 9;

    [Tooltip("Enum id that other systems use to reference this biome.")]
    public BiomeType Type;

    [Header("Identity")]
    public string displayName;
    [TextArea] public string description;

    [Header("Terrain (palette / tuning)")]
    [Tooltip("Ambient hue tint for this biome.")]
    public Color Tint = Color.white;
    [Tooltip("Elevation bias used by terrain/noise; reserved for generation.")]
    public float BaseHeight;
    [Tooltip("Aggregate terrain slope factor; reserved for generation.")]
    public float Roughness;

    [Header("Spawn Rules")]
    [Tooltip("Enemy roster in order of weight; paired with SpawnWeights.")]
    public string[] SpawnEnemyIds;
    [Tooltip("Relative spawn weight per entry (same index as SpawnEnemyIds).")]
    public float[] SpawnWeights;
    [Tooltip("Base number of enemies per spawn-chunk (scaled by difficulty).")]
    public float BaseDensity = 3f;
    [Tooltip("Chance (0..1) that a night-only enemy variant replaces a day one (§7.3).")]
    [Range(0f, 1f)] public float NightVariantChance = 0.25f;

    /// <summary>Roll an enemy id honoring the weight table.</summary>
    public string RollEnemyId()
    {
        if (SpawnEnemyIds == null || SpawnEnemyIds.Length == 0)
            return null;
        if (SpawnWeights == null || SpawnWeights.Length != SpawnEnemyIds.Length)
            return SpawnEnemyIds[UnityEngine.Random.Range(0, SpawnEnemyIds.Length)];

        float total = 0f;
        foreach (var w in SpawnWeights) total += Mathf.Max(0f, w);
        if (total <= 0f) return SpawnEnemyIds[UnityEngine.Random.Range(0, SpawnEnemyIds.Length)];

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < SpawnEnemyIds.Length; i++)
        {
            roll -= Mathf.Max(0f, SpawnWeights[i]);
            if (roll <= 0f)
                return SpawnEnemyIds[i];
        }
        return SpawnEnemyIds[SpawnEnemyIds.Length - 1];
    }

    /// <summary>Registry-static friendly lookup helper.</summary>
    public static string DisplayNameOf(BiomeType type)
    {
        switch (type)
        {
            case BiomeType.Plains: return "Plains";
            case BiomeType.Forest: return "Forest";
            case BiomeType.Mountains: return "Mountains";
            case BiomeType.Swamp: return "Swamp";
            case BiomeType.Desert: return "Desert";
            case BiomeType.Tundra: return "Tundra";
            case BiomeType.Volcanic: return "Volcanic";
            case BiomeType.Deep: return "Deep";
            case BiomeType.Ocean: return "Ocean";
            default: return type.ToString();
        }
    }
}