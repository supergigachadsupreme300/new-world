using System;
using UnityEngine;

/// <summary>A single-stat unlock threshold for a class.</summary>
[Serializable]
public struct StatReq
{
    public StatType Stat;
    public float Minimum;
}

/// <summary>A skill-level unlock threshold for a class (e.g. Blacksmith needs Crafting ≥ 10).</summary>
[Serializable]
public struct SkillReq
{
    public SkillType Skill;
    public int Level;
}

/// <summary>A combined two-stat unlock threshold (e.g. Str + End ≥ 35).</summary>
[Serializable]
public struct CombinedReq
{
    public StatType First;
    public StatType Second;
    public float MinimumTotal;
}

/// <summary>
/// A data-only class definition (game-design §3.2). Each class declares a list of
/// requirements evaluated by <see cref="ClassUnlocker"/> — single-stat, combined-stat, and
/// skill-level. See the §3.2 table for the 15 classes (Wanderer baseline + 14 unlockable).
/// </summary>
[CreateAssetMenu(fileName = "Class", menuName = "New World/Classes/Class", order = 60)]
public class ClassData : ScriptableObject
{
    [Header("Identity")]
    public string classId;
    public string displayName;
    [TextArea] public string description;

    [Header("Unlock Requirements (§3.2)")]
    public StatReq[] StatRequirements = Array.Empty<StatReq>();
    [Tooltip("Optional check that the SUM of two stats meets a threshold (e.g. Str + End ≥ 35).")]
    public CombinedReq[] CombinedRequirements = Array.Empty<CombinedReq>();
    [Tooltip("At least two different stats must each be ≥ this value (e.g. Alchemist: any 2 ≥ 18). 0 = no check.")]
    public float MinAnyTwoStats = 0f;
    public SkillReq[] SkillRequirements = Array.Empty<SkillReq>();

    [Header("Mechanic")]
    [Tooltip("Human-readable summary of the class's unique mechanic.")]
    public string UniqueMechanic;
}