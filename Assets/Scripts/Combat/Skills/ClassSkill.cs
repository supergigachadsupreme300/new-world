using UnityEngine;

/// <summary>
/// Kinds of passive modifiers a class skill can grant (game-design §3.2.1). Modifiers are
/// live only while the owning class is ACTIVE — switching classes swaps the modifier set in/out.
/// Amount semantics (all positive, added):
///   * *Mul kinds  → final multiplier = 1 + Σamount  (e.g. MeleePowerMul 0.26 → +26% melee power)
///   * EquipLoadBonus / HpRegenPerSecond → flat add (HpRegen is fraction of max HP per second)
///   * BerserkScale → 1 + Σamount × (1 − HP/MaxHP), folded into the melee multiplier live
/// </summary>
public enum ClassModType
{
    MeleePowerMul,
    SpellPowerMul,
    CooldownMul,
    AttackSpeedMul,
    BackstabMul,
    HealPowerMul,
    BerserkScale,
    ParryWindowMul,
    ConsumablePotencyMul,
    DefenseMeleeMul,
    EquipLoadBonus,
    RangedHandlingMul,
    AuraStrength,
    BlockingMul,
    StaggerResistMul,
    CraftSuccessMul,
    RepairMul,
    StaminaRegenMul,
    HpRegenPerSecond,
}

/// <summary>A single passive modifier carried by a <see cref="ClassSkill"/>.</summary>
[System.Serializable]
public struct ClassMod
{
    public ClassModType kind;
    public float amount;
}

/// <summary>
/// A class skill (game-design §3.2.1). Each of the 17 classes owns a small radial tree
/// (hub + 3 paths). Skills are auto-granted at class unlock — their effects/modifiers are
/// live only while that class is the active class. Composed behaviors mirror the normal
/// <see cref="Skill"/> model: passive modifiers via <see cref="Mods"/>, actives via
/// <see cref="Effects"/> (IClassEffect), a shared <see cref="Cost"/> + cooldown key.
/// </summary>
[CreateAssetMenu(fileName = "ClassSkill", menuName = "New World/Skills/ClassSkill", order = 71)]
public class ClassSkill : ScriptableObject
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
    public IClassEffect[] Effects = new IClassEffect[0];

    [Header("Passive modifiers")]
    [Tooltip("Modifiers granted while the owning class is the active class (additive).")]
    public ClassMod[] Mods = new ClassMod[0];

    [Header("Tree / Progression")]
    [Tooltip("Radial layer: 0 = class hub, 1 = path parent, 2 = leaves/capstone.")]
    public int Layer;
    [Tooltip("Parent nodes in the radial layout (cosmetic — everything is auto-granted on unlock).")]
    public string[] PrereqSkillIds = System.Array.Empty<string>();
    [Tooltip("True = passive (no cast), false = castable via hotkey.")]
    public bool IsPassive;

    /// <summary>True if a modifier of the given kind is present on this skill.</summary>
    public bool HasMod(ClassModType kind)
    {
        if (Mods == null) return false;
        for (int i = 0; i < Mods.Length; i++)
            if (Mods[i].kind == kind) return true;
        return false;
    }

    /// <summary>Total amount of the given modifier kind across this skill's Mods.</summary>
    public float ModValue(ClassModType kind)
    {
        if (Mods == null) return 0f;
        float sum = 0f;
        for (int i = 0; i < Mods.Length; i++)
            if (Mods[i].kind == kind) sum += Mods[i].amount;
        return sum;
    }

    /// <summary>Cooldown key for this skill on the shared caster (per-class namespace).</summary>
    public string CooldownKey => "class_" + id;

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