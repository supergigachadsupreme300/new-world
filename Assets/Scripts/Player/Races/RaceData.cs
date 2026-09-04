using System;
using UnityEngine;

/// <summary>
/// A playable race definition (game-design §3.5, planning Task 4.3). Data-only
/// ScriptableObject: stat % modifiers, passive kit, XP bonuses, roll weight, and rig
/// parameters. Adding a race = creating a new .asset — zero code changes.
/// </summary>
[CreateAssetMenu(fileName = "Race", menuName = "New World/Races/Race", order = 50)]
public class RaceData : ScriptableObject
{
    public const int StatCount = 11;

    [Header("Identity")]
    public string raceId;
    public string displayName;
    [TextArea] public string lore;

    [Header("Stat Modifiers (%)")]
    [Tooltip("Percentage applied to TOTAL stat on-the-fly (index = StatType).")]
    public float[] StatModifiers = new float[StatCount];

    /// <summary>Racial % modifier for the given stat.</summary>
    public float GetStatModifier(StatType stat) =>
        StatModifiers != null && (int)stat < StatModifiers.Length ? StatModifiers[(int)stat] : 0f;

    [Header("Passive Kit (dispatched by RacePassiveManager)")]
    [Tooltip("Passive id handled by the runtime passive manager.")]
    public string PassiveId;
    [Tooltip("Human-readable passive description for UI.")]
    public string PassiveDescription;

    [Header("XP Bonus")]
    [Tooltip("All-source XP % bonus (Human +15%).")]
    public float XpBonusAll;
    [Tooltip("Per-category XP % bonuses (index = SkillType).")]
    public float[] XpBonuses = new float[SkillCount];

    private const int SkillCount = 6;

    /// <summary>XP % bonus for a specific skill category.</summary>
    public float GetXpBonus(SkillType skill) =>
        XpBonuses != null && (int)skill < XpBonuses.Length ? XpBonuses[(int)skill] : 0f;

    [Header("Roll Weight")]
    [Tooltip("Weight for the random character-creation roll (Human=50, others≈1).")]
    public float Weight = 1f;

    [Header("Rig")]
    [Tooltip("Model scale multiplier.")]
    public float RigScale = 1f;
    [Tooltip("Vertical offset in meters.")]
    public float RigOffset = 0f;
    [Tooltip("Skin/armor material tint.")]
    public Color RigTint = Color.white;
    [Tooltip("Placeholder body prefab (real models drop in later without code changes).")]
    public GameObject RigPrefab;

    /// <summary>Net stat budget (sum of all modifiers) — for balance tiers / UI.</summary>
    public int NetStatBudget
    {
        get
        {
            float sum = 0f;
            if (StatModifiers == null) return 0;
            for (int i = 0; i < StatModifiers.Length && i < StatCount; i++) sum += StatModifiers[i];
            return Mathf.RoundToInt(sum);
        }
    }

    private void OnValidate()
    {
        if (StatModifiers == null || StatModifiers.Length != StatCount)
            Array.Resize(ref StatModifiers, StatCount);
        if (XpBonuses == null || XpBonuses.Length != SkillCount)
            Array.Resize(ref XpBonuses, SkillCount);
    }
}