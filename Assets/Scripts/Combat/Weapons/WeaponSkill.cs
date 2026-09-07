using System;
using UnityEngine;

/// <summary>
/// A per-weapon unique skill (§5.4), cast from the weapon and costing FP.
///
/// Data asset defining the skill's identity, FP cost, cooldown, damage, and optional
/// status effect. Actual triggering is handled by the equipped weapon's behavior
/// (it spends FP and invokes the associated effect/visual).
/// </summary>
[CreateAssetMenu(fileName = "WeaponSkill", menuName = "New World/Combat/Weapon Skill", order = 1)]
public class WeaponSkill : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Tooltip("Custom skill kind handled by the WeaponSkill executor; 'none' means no skill.")]
    public string Kind = "none";

    [Header("Costs")]
    public float FpCost = 20f;
    public float Cooldown = 3f;

    [Header("Offense")]
    [Tooltip("Damage type dealt by the skill (§3.7). Falls back to the weapon's weapon type if None.")]
    public DamageType Type = DamageType.Physical;
    public float BaseDamage = 25f;
    public float Knockback = 5f;
    [Tooltip("Forward reach of the skill's strike in world units.")]
    public float Range = 2.5f;
    [Tooltip("Radius of the skill's strike sphere.")]
    public float Radius = 1f;

    [Header("Status (optional, §3.7)")]
    public bool AppliesStatus;
    public StatusEffectType StatusEffect;
    public float StatusProcChance;

    /// <summary>The skill can be cast when the caster has enough FP to pay FpCost.</summary>
    public bool IsUsable(SpellCaster caster) =>
        caster != null && caster.HasFocusPoints(FpCost);
}