using UnityEngine;

/// <summary>
/// Kinds of passive modifiers a race skill can grant. Race modifiers enhance what makes
/// each race unique — stat bonuses that stack with the base racial stat modifiers, passive
/// amplifiers that scale existing racial passives, and combat/utility bonuses.
/// Amount semantics (all positive, added):
///   * Bonus kinds → flat add to the race's base stat modifier (e.g. StrengthBonus 5 → +5% Strength)
///   * Mul kinds → final multiplier = 1 + Σamount (e.g. MeleePowerMul 0.08 → +8% melee power)
///   * PerSecond kinds → flat add per second (HpRegenPerSecond 0.005 → +0.5% max HP/s)
///   * Flat kinds → flat add (EquipLoadBonus 10 → +10 equip load)
/// </summary>
public enum RaceModType
{
    // ── Stat Bonuses (flat add to race's stat % modifier) ────────────────────
    HealthBonus,
    SpeedBonus,
    EnduranceBonus,
    StrengthBonus,
    DexterityBonus,
    AttackSpeedBonus,
    DefenseBonus,
    IntelligenceBonus,
    WisdomBonus,
    FaithBonus,
    LuckBonus,

    // ── Passive Amplifiers (extend existing racial passive traits) ────────────
    MoveSpeedMul,
    DamageReductionBonus,
    HpRegenPerSecond,
    LifestealBonus,
    StaminaRegenMul,

    // ── Combat Modifiers ─────────────────────────────────────────────────────
    MeleePowerMul,
    SpellPowerMul,
    BackstabMul,
    HealPowerMul,
    CooldownMul,
    ParryWindowMul,
    BlockingMul,
    StaggerResistMul,
    AttackSpeedMul,
    DefenseMeleeMul,

    // ── Utility ──────────────────────────────────────────────────────────────
    XpBonusMul,
    EquipLoadBonus,
    ConsumablePotencyMul,
}

/// <summary>A single passive modifier carried by a <see cref="RaceSkill"/>.</summary>
[System.Serializable]
public struct RaceMod
{
    public RaceModType kind;
    public float amount;
}

/// <summary>
/// A race skill. Each of the 22 races owns a radial tree (hub + 3 paths) that focuses
/// purely on enhancing racial identity — stat bonuses, passive amplifiers, and active
/// racial abilities. Skills are auto-granted for testing (no point economy). Passives
/// apply while the race is active (aggregated by <see cref="RaceSkillPassiveManager"/>).
/// Active abilities route through <see cref="RaceSkillCaster"/> via the hotkey system.
/// </summary>
[CreateAssetMenu(fileName = "RaceSkill", menuName = "New World/Skills/RaceSkill", order = 72)]
public class RaceSkill : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea] public string description;

    [Header("Attribute 1 — Cost")]
    [Tooltip("Resource/amount/cooldown for castables. None for passives.")]
    public Cost SkillCost;

    [Header("Attribute 2 — What it does")]
    [Tooltip("Active behaviors executed when cast (empty for pure-passive skills).")]
    public IRaceEffect[] Effects = new IRaceEffect[0];

    [Header("Passive modifiers")]
    [Tooltip("Modifiers granted while the owning race is active (additive).")]
    public RaceMod[] Mods = new RaceMod[0];

    [Header("Tree / Progression")]
    [Tooltip("Radial layer: 0 = race hub, 1 = path parent, 2 = leaves/capstone.")]
    public int Layer;
    [Tooltip("Parent nodes in the radial layout (cosmetic — everything is auto-granted).")]
    public string[] PrereqSkillIds = System.Array.Empty<string>();
    [Tooltip("True = passive (no cast), false = castable via hotkey.")]
    public bool IsPassive;

    /// <summary>True if a modifier of the given kind is present on this skill.</summary>
    public bool HasMod(RaceModType kind)
    {
        if (Mods == null) return false;
        for (int i = 0; i < Mods.Length; i++)
            if (Mods[i].kind == kind) return true;
        return false;
    }

    /// <summary>Total amount of the given modifier kind across this skill's Mods.</summary>
    public float ModValue(RaceModType kind)
    {
        if (Mods == null) return 0f;
        float sum = 0f;
        for (int i = 0; i < Mods.Length; i++)
            if (Mods[i].kind == kind) sum += Mods[i].amount;
        return sum;
    }

    /// <summary>Cooldown key for this skill on the shared caster (per-race namespace).</summary>
    public string CooldownKey => "race_" + id;

    /// <summary>True if all prerequisite ids are present in <paramref name="owned"/>.</summary>
    public bool PrereqsMet(System.Collections.Generic.HashSet<string> owned)
    {
        if (PrereqSkillIds == null || PrereqSkillIds.Length == 0) return true;
        for (int i = 0; i < PrereqSkillIds.Length; i++)
            if (string.IsNullOrEmpty(PrereqSkillIds[i]) || owned == null || !owned.Contains(PrereqSkillIds[i]))
                return false;
        return true;
    }
}
