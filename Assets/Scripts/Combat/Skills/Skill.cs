using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A composable skill (Phase 10). Every skill is made of two interchangeable attributes:
/// Attribute 1 — <see cref="Cost"/> ("what it costs to use") and Attribute 2 — <see cref="Effect"/>
/// ("what it does"), plus its <see cref="SkillType"/> source category and <see cref="DamageKind"/>
/// (the element when the skill is offensive). Spells count as skills: a magic castable skill's
/// effect is a <see cref="SpellCastEffect"/> over a <see cref="SpellData"/>. Passive skills use a
/// <see cref="StatBuffEffect"/> and a zero <see cref="Cost"/>.
///
/// Data-only ScriptableObject (built at runtime by <c>SkillCatalog</c>), consistent with the
/// data-driven design. Execution is delegated to the composed effect — this class never branches.
/// </summary>
[CreateAssetMenu(fileName = "Skill", menuName = "New World/Skills/Skill", order = 70)]
public class Skill : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [Tooltip("Which progression category this skill belongs to (§3.3).")]
    public SkillType Type;
    [TextArea] public string description;

    [Header("Attribute 1 — Cost")]
    public Cost SkillCost;

    [Header("Attribute 2 — What it does")]
    [Tooltip("Offensive skill = the element/damage kind. Physical=false magic; else one of the elemental types (§3.7).")]
    public bool IsMagical;
    public DamageType DamageKind = DamageType.Physical;
    [Tooltip("The pluggable behavior this skill executes.")]
    public IEffect Effect;

    [Header("Progression")]
    [Tooltip("Skills that must be learned before this one can be purchased.")]
    public string[] PrereqSkillIds = System.Array.Empty<string>();
    [Tooltip("True = passive (always-on once learned) rather than a castable.")]
    public bool IsPassive;

    /// <summary>True if the user can currently pay the skill's cost.</summary>
    public bool CanAfford(SkillContext ctx) => SkillCost.CanAfford(ctx);

    /// <summary>Spend the cost; returns false if it could not be paid.</summary>
    public bool TrySpend(SkillContext ctx) => SkillCost.Spend(ctx);

    /// <summary>Cooldown key for this skill on the shared caster.</summary>
    public string CooldownKey => "skill_" + (id ?? name);

    /// <summary>Remaining cooldown seconds (0 = ready).</summary>
    public float RemainingCooldown(SkillContext ctx)
    {
        if (ctx == null || ctx.Caster == null) return 0f;
        return ctx.Caster.CooldownRemaining(CooldownKey);
    }

    /// <summary>True if all prerequisite skills are in <paramref name="learned"/>.</summary>
    public bool PrereqsMet(HashSet<string> learned)
    {
        if (PrereqSkillIds == null || PrereqSkillIds.Length == 0) return true;
        for (int i = 0; i < PrereqSkillIds.Length; i++)
            if (string.IsNullOrEmpty(PrereqSkillIds[i]) || learned == null || !learned.Contains(PrereqSkillIds[i]))
                return false;
        return true;
    }
}