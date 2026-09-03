using System;
using UnityEngine;

/// <summary>
/// Per-category skill XP (game-design §3.3, planning Task 4.2). Six categories each track
/// their own XP and level; leveling grants flat tier rewards at 5 / 10 / 15 / 20 / 25.
/// <see cref="SkillXpTracker.AddXp"/> applies the active race's per-category XP bonus.
///
/// The 15-class system consults category levels (Blacksmith requires Crafting ≥ 10).
/// </summary>
[DisallowMultipleComponent]
public class SkillXpTracker : MonoBehaviour
{
    [Serializable]
    public class CategoryState
    {
        public int Level;
        public float Xp;
        public int UnlockedTier; // highest tier reward granted so far
    }

    public const int MaxLevel = 25;
    public const int TierStep = 5;

    public CategoryState[] Categories = new CategoryState[CategoryCount];
    public const int CategoryCount = 6;

    public float BaseXpForLevel = 100f;
    public float LevelCurve = 1.3f;

    private PlayerStats _stats;

    /// <summary>Fires on any category level-up with the category and its new level.</summary>
    public event Action<SkillType, int> OnSkillLevelUp;

    [Header("Tier Rewards (flat bonuses per category level milestone)")]
    [Tooltip("Reward granted to a category each time it hits a 5/10/15/20/25 milestone.")]
    public string[] TierRewardNames =
    {
        "Melee damage +5%", "Ranged damage +5%", "Spell power +5%",
        "Sneak/Action noise -10%", "Crafting yield +5%", "Max HP +5%"
    };

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        EnsureCategories(CategoryCount);
    }

    private void EnsureCategories(int count)
    {
        if (Categories == null || Categories.Length != count)
        {
            Categories = new CategoryState[count];
            for (int i = 0; i < count; i++) Categories[i] = new CategoryState();
        }
    }

    public int GetLevel(SkillType skill) => Categories[(int)skill].Level;

    public float GetXp(SkillType skill) => Categories[(int)skill].Xp;

    public float XpToNextLevel(SkillType skill) => XpToNextLevelFor(Categories[(int)skill]);

    /// <summary>Add category XP applying the race's per-category XP bonus; level up + grant tier rewards.</summary>
    public void AddXp(SkillType skill, float amount)
    {
        if (amount <= 0f) return;
        EnsureCategories(CategoryCount);

        float bonus = _stats != null && _stats.Race != null ? _stats.Race.GetXpBonus(skill) : 0f;
        var c = Categories[(int)skill];
        c.Xp += amount * (1f + bonus / 100f);

        while (c.Level < MaxLevel && c.Xp >= XpToNextLevelFor(c))
        {
            c.Xp -= XpToNextLevelFor(c);
            c.Level++;
            GrantTierIfDue(c, (int)skill);
            OnSkillLevelUp?.Invoke(skill, c.Level);
        }
        if (c.Level >= MaxLevel)
            c.Xp = XpToNextLevelFor(c);
    }

    private float XpToNextLevelFor(CategoryState c) =>
        BaseXpForLevel * Mathf.Pow(LevelCurve, c.Level - 1);

    private void GrantTierIfDue(CategoryState c, int index)
    {
        // Grant reward for each milestone crossed (tier level % 5 == 0).
        if (c.Level % TierStep != 0) return;
        c.UnlockedTier = c.Level;
        // Reward application hook: subclasses/consumers read TierRewardNames[index].
    }

    /// <summary>The tier reward title earned at a given category level, or null if none.</summary>
    public string TierRewardFor(int index) =>
        index >= 0 && index < TierRewardNames.Length ? TierRewardNames[index] : null;
}