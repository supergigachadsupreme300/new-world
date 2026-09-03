using System;
using UnityEngine;

/// <summary>
/// A per-weapon unique ability (§5.4), cast from the weapon and costing FP.
///
/// Data asset defining the art's identity, FP cost, cooldown, damage, and optional
/// status effect. Actual triggering is handled by the equipped weapon's behavior
/// (it spends FP and invokes the associated effect/visual).
/// </summary>
[CreateAssetMenu(fileName = "WeaponArt", menuName = "New World/Combat/Weapon Art", order = 1)]
public class WeaponArt : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Tooltip("Custom art identifier handled by the WeaponArt executor; 'none' means no art.")]
    public string ArtType = "none";

    [Header("Costs")]
    public float FpCost = 20f;
    public float Cooldown = 3f;

    [Header("Offense")]
    [Tooltip("Damage type dealt by the art (§3.7). Falls back to the weapon's weapon type if None.")]
    public DamageType Type = DamageType.Physical;
    public float BaseDamage = 25f;
    public float Knockback = 5f;
    [Tooltip("Forward reach of the art's strike in world units.")]
    public float Range = 2.5f;
    [Tooltip("Radius of the art's strike sphere.")]
    public float Radius = 1f;

    [Header("Status (optional, §3.7)")]
    public bool AppliesStatus;
    public StatusEffectType StatusEffect;
    public float StatusProcChance;

    /// <summary>The art can be cast when the caster has enough FP to pay FpCost.</summary>
    public bool IsUsable(SpellCaster caster) =>
        caster != null && caster.HasFocusPoints(FpCost);
}
