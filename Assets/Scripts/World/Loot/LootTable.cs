using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One weighted drop possibility in a <see cref="LootTable"/> (planning Task 5.2).
/// </summary>
[Serializable]
public class LootEntry
{
    public ItemData Item;
    [Tooltip("Relative roll weight against other entries.")]
    public float Weight = 1f;
    [Min(1)] public int MinCount = 1;
    [Min(1)] public int MaxCount = 1;
    [Range(0f, 1f)] public float Chance = 1f;
    [Tooltip("If true, ignores the luck quality gate and always rolls.")]
    public bool Always;
}

/// <summary>
/// Weighted drop table for an enemy or container (planning Task 5.2). Rolls a set of items
/// honoring weights and a luck-based quality multiplier (game-design §7.1 / Goblin & Gnome
/// passives). Returns a list of (item, count) pairs; callers spawn <see cref="LootDrop"/>.
/// </summary>
[Serializable]
public class LootTable
{
    [Tooltip("Entries are rolled in order; later entries can gate on luck quality higher up.")]
    public List<LootEntry> Entries = new List<LootEntry>();

    /// <summary>
    /// Roll the table. <paramref name="luckMultiplier"/> is the player's luck contribution
    /// (0 = no bonus, &gt;0 raises the best-roll chance), passed in by the caller so the
    /// table never couples to a concrete player type.
    /// </summary>
    public List<KeyValuePair<ItemData, int>> Roll(float luckMultiplier = 0f)
    {
        var result = new List<KeyValuePair<ItemData, int>>();
        if (Entries == null) return result;

        // Build a weight pool so a single uniform draw picks entries by weight.
        float total = 0f;
        foreach (var e in Entries)
            if (e != null && e.Item != null && e.Weight > 0f)
                total += e.Weight;
        if (total <= 0f) return result;

        float luckBonus = Mathf.Max(0f, luckMultiplier);
        foreach (var e in Entries)
        {
            if (e == null || e.Item == null || e.Weight <= 0f) continue;
            if (!e.Always && UnityEngine.Random.value > e.Chance + luckBonus * 0.05f)
                continue;
            if (!e.Always && !RollQualityGate(e, luckBonus))
                continue;

            int count = UnityEngine.Random.Range(e.MinCount, e.MaxCount + 1);
            if (count > 0)
                result.Add(new KeyValuePair<ItemData, int>(e.Item, count));
        }
        return result;
    }

    /// <summary>Luck raises the chance that a gated entry actually pays out.</summary>
    private static bool RollQualityGate(LootEntry e, float luckBonus)
    {
        // Entries with low weight are treated as "rarer" — luck helps.
        float gate = Mathf.Clamp01(e.Weight * 0.15f + 0.3f);
        return UnityEngine.Random.value <= gate + luckBonus * 0.1f;
    }
}