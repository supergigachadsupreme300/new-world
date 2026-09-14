using System;
using UnityEngine;

/// <summary>
/// How a spell delivers its effect (§3.8).
/// </summary>
public enum SpellDelivery
{
    Instant = 0,
    Projectile = 1,
    Zone = 2,
    Vortex = 3,
    Beam = 4,
    Summon = 5,
    Storm = 6
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
    [Tooltip("Lifetime of a persistent zone delivery in seconds. 0 = zone resolves instantly.")]
    public float Duration = 0f;
    [Tooltip("Seconds between damage ticks for Beam / Summon / Storm deliveries. 0 = default (0.5s).")]
    public float TickInterval = 0.5f;
    [Tooltip("Focus points drained per second while a Beam channel is held alive (0 = no upkeep).")]
    public float ChannelDrainPerSecond = 0f;

    [Header("Status (optional, §3.7)")]
    public bool AppliesStatus;
    public StatusEffectType StatusEffect;
    [Tooltip("Chance (0..1) the status applies on a hit. Default 1 when AppliesStatus is set.")]
    public float StatusProcChance = 1f;

    [Header("Presentation")]
    public GameObject CastEffectPrefab;
    public GameObject ImpactEffectPrefab;

    [Header("Mechanics")]
    [Tooltip("Restores health to friendly (Player/companion) targets instead of damaging them. Zone healing also heals allies while still damaging enemies.")]
    public bool Heals;
    [Tooltip("Outward shove (world units) applied to the target's root on hit. Positive pushes away from the caster.")]
    public float Knockback;
    [Tooltip("Self-buff: the Instant delivery grants a timed effect on the caster (e.g. flight) instead of a damage/heal raycast. Duration is the buff length in seconds.")]
    public bool SelfBuff;
}
