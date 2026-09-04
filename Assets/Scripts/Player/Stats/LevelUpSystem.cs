using System;
using UnityEngine;

/// <summary>
/// Character leveling (game-design §3.4 + planning Task 4.1). Tracks total XP, advances
/// level on progressive thresholds, and grants stat points each level. Every XP source is
/// multiplied by the active race's all-source XP bonus (Human +15%, etc.), applied here.
/// </summary>
[DisallowMultipleComponent]
public class LevelUpSystem : MonoBehaviour
{
    [Header("Leveling")]
    [Tooltip("Current character level.")]
    public int Level = 1;

    [Tooltip("Current total XP (after racial multiplier).")]
    public float Xp;

    [Tooltip("Stat points granted per level-up.")]
    public int PointsPerLevel = 3;

    [Tooltip("Base XP required to reach level 2.")]
    public float BaseXpForLevel = 100f;

    [Tooltip("Multiplier applied to each level's threshold (exponential curve).")]
    public float LevelCurve = 1.5f;

    [Tooltip("Maximum attainable level.")]
    public int MaxLevel = 99;

    private PlayerStats _stats;

    /// <summary>Fires on level-up with the new level; afterwards Stats.Points increase.</summary>
    public event Action<int> OnLevelUp;

    [Header("Available Points")]
    public int AvailablePoints;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
    }

    /// <summary>XP required to advance from the given level to the next.</summary>
    public float XpForLevel(int level)
    {
        return BaseXpForLevel * Mathf.Pow(LevelCurve, level - 1);
    }

    /// <summary>Current level-up threshold (XP needed for the next level).</summary>
    public float XpToNextLevel => XpForLevel(Level);

    /// <summary>Add XP, applying the active race's all-source XP bonus, and level up as needed.</summary>
    public void AddXp(float amount)
    {
        if (amount <= 0f) return;
        float racialBonus = _stats != null && _stats.Race != null ? _stats.Race.XpBonusAll : 0f;
        Xp += amount * (1f + racialBonus / 100f);

        while (Level < MaxLevel && Xp >= XpForLevel(Level))
        {
            Xp -= XpForLevel(Level);
            Level++;
            AvailablePoints += PointsPerLevel;
            OnLevelUp?.Invoke(Level);
        }
        if (Level >= MaxLevel)
            Xp = XpForLevel(MaxLevel); // clamp displayed XP at cap
    }

    /// <summary>Spend one available point into a stat. Returns true if spent.</summary>
    public bool SpendPoint(StatType stat)
    {
        if (AvailablePoints <= 0 || _stats == null) return false;
        AvailablePoints--;
        _stats.AddStatPoints(stat, 1f);
        return true;
    }
}