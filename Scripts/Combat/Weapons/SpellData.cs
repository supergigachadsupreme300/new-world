using System;
using UnityEngine;

/// <summary>
/// How a spell delivers its effect (§3.8).
/// </summary>
public enum SpellDelivery
{
    Instant = 0,
    Projectile = 1,
    Zone = 2
}

/// <summary>
/// Data asset defining a spell (§3.8). Spells are cast through Magic weapons
/// (staff / wand / book) or equippable active skills, routed via SpellCaster.
/// </summary>
[CreateAssetMenu(fileName = "SpellData", menuName = "New World/Combat/Spell", order = 2)]
public class SpellData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Damage (§3.7)")]
    public DamageType Type = DamageType.Arcane;
    [Tooltip("Base spell power, scaled by Wisdom (IStatProvider.MagicAttackPower). Set 0 for pure utility/heal.")]
    public float BasePower = 20f;

    [Header("Costs")]
    public float FpCost = 15f;
    public float CastTime = 0.5f;
    public float Cooldown = 2f;

    [Header("Delivery (§3.8)")]
    public SpellDelivery Delivery = SpellDelivery.Instant;
    [Tooltip("Range for projectile/zone (world units).")]
    public float Range = 10f;
    [Tooltip("Zone radius (Zone delivery) or projectile explosion radius.")]
    public float Radius = 1f;
    public float ProjectileSpeed = 20f;

    [Header("Status (optional, §3.7)")]
    public bool AppliesStatus;
    public StatusEffectType StatusEffect;
    public float StatusProcChance;

    [Header("Presentation")]
    public GameObject CastEffectPrefab;
    public GameObject ImpactEffectPrefab;
}
