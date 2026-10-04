using System;
using UnityEngine;

/// <summary>
/// The stat(s) a weapon's damage scales with and the per-point coefficient.
/// Str scales heavy/melee; Dex scales light/one-handed (see §3.4).
/// </summary>
public enum WeaponScalingStat
{
    None = 0,
    Strength = 1,
    Dexterity = 2,
    Intelligence = 3,
    Wisdom = 4
}

/// <summary>
/// Weapon data asset (§3.6 Layer 1). Data-only ScriptableObject; the actual combat
/// behavior is delegated to the matching IWeaponBehavior (Melee/Ranged/Magic).
///
/// Magic weapons additionally carry magic mods (MagicDamageMult, CastTimeMod, CooldownMod)
/// which scale spells cast through them (see §3.8 and MagicWeaponBehavior).
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "New World/Combat/Weapon", order = 0)]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Category & Delivery")]
    public WeaponCategory Category = WeaponCategory.Melee;
    public DamageType Type = DamageType.Physical;

    [Header("Wielding (§5.4)")]
    [Tooltip("Weight class driving the Str requirement: light / medium / heavy.")]
    public float Weight = 1f;
    public float StrengthRequirement = 1f;

    [Header("Combat Numbers")]
    [Tooltip("Base damage. Ranged uses this as the weapon's damage ceiling (weapon.base), not stat-scaled.")]
    public float BaseDamage = 10f;
    public float Speed = 1f;
    [Tooltip("Attack reach in world units (melee arc / ranged max distance).")]
    public float Reach = 1f;

    [Header("Scaling")]
    public WeaponScalingStat ScalingStat = WeaponScalingStat.None;
    public float ScalingCoefficient = 0f;

    [Header("Per-Category (Melee)")]
    [Tooltip("Stagger power applied by melee hits.")]
    public float StaggerPower = 0f;

    [Header("Per-Category (Ranged)")]
    [Tooltip("Ammo item id consumed per shot (arrows/bolts). Empty = requires ammo none.")]
    public string AmmoItemId;
    [Tooltip("Accuracy bonus from Dexterity (%) — ranged precision.")]
    public float AccuracyFromDex = 0f;

    [Header("Per-Category (Magic, §3.8)")]
    [Tooltip("Multiplier applied to spell base power when casting through this weapon.")]
    public float MagicDamageMult = 1f;
    [Tooltip("Multiplier applied to spell cast time (<1 = faster).")]
    public float CastTimeMod = 1f;
    [Tooltip("Multiplier applied to spell cooldown (<1 = shorter).")]
    public float CooldownMod = 1f;

    [Header("Per-Category (Shield)")]
    [Tooltip("Fraction of incoming damage absorbed while blocking with this shield. 0.8 = 20% gets through (parity with the bare-hand guard); 0.95 = only 5%.")]
    public float BlockAbsorbPercent = 0.8f;
    [Tooltip("Multiplier on stamina drained per absorbed hit while blocking (<1 = cheaper guard).")]
    public float BlockStaminaDrainMult = 1f;

    [Header("Weapon Skill (§5.4)")]
    public WeaponSkill Skill;
}
