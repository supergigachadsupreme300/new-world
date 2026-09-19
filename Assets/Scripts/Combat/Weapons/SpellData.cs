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
/// Ground deformation a spell triggers when it resolves on terrain (§3.8 — the Earth
/// school's signature; Earth spells carry no status effect and reshape the ground instead).
/// </summary>
public enum TerrainShape
{
    None = 0,

    /// <summary>Raise an annulus (stone ring) around the impact center — the "circle around"
    /// earth move. The center stays level so nothing is buried at the strike point.</summary>
    Ring = 1,

    /// <summary>Raise a cluster of rock spikes across the impact area.</summary>
    Spikes = 2,

    /// <summary>Raise an elongated stone ridge (an earth wall) along the cast direction.</summary>
    Wall = 3,

    /// <summary>Raise a tall flat-topped column (an earth pillar) at the impact center.</summary>
    Pillar = 4,

    /// <summary>Excavate a wide shallow smooth dish (an earth projectile carves its crater
    /// here). The pit has a smooth feathered rim and always keeps a solid walkable floor —
    /// clamped to never grind deeper than one excavation — never a bottomless void.</summary>
    Crater = 5
}

/// <summary>
/// Visual shape of a Projectile-delivery spell (§3.8). The shape follows the spell's
/// name ("Frost Bolt" is a jagged Bolt, "Ice Lance" a Lance, "Stone Shard" a Shard...),
/// not the element. `Auto` picks the element's default shape (see SpellCaster).
/// </summary>
public enum ProjectileShape
{
    Auto = 0,     // element default (Fire→Sphere, Ice→Shard, Lightning→Bolt, Wind→Blade, ...)

    /// <summary>Jagged segmented streak built like the thunder-storm lightning bolt
    /// (RandomEventManager.SpawnJaggedBolt), colored by the element.</summary>
    Bolt = 1,

    /// <summary>Round orb (classic fireballs, plain magic balls).</summary>
    Sphere = 2,

    /// <summary>Diamond crystal that tumbles/drills (stone &amp; frost chips).</summary>
    Shard = 3,

    /// <summary>Long straight pointed ice spike.</summary>
    Lance = 4,

    /// <summary>Tapered spear with a broad head and a trailing shaft.</summary>
    Spear = 5,

    /// <summary>Flat spinning slashing disc (wind blades / crossing blades).</summary>
    Blade = 6,

    /// <summary>Water droplet with a splash trail.</summary>
    Splash = 7,

    /// <summary>Streaking fire/energy tail (burning comet line).</summary>
    Comet = 8,

    /// <summary>Cluster of small darts (arcane missiles).</summary>
    Missile = 9,

    /// <summary>Small sleek fast bolt-line (quick ranged shots / talisman darts).</summary>
    Dart = 10,

    /// <summary>Tumbling cluster of rock chunks (the Earth school / stone shards). Dressed like the
    /// world's breakable-rock debris (grey <c>Color.Lerp(Color.gray, Color.black, rand)</c> cubes).</summary>
    Debris = 11
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

    [Header("Terrain (§3.8, Earth school)")]
    [Tooltip("Earth spells reshape the ground: Ring raises a stone circle around the impact point, Spikes raise rock spikes across the area, Wall rears an earth ridge along the cast direction, Pillar raises a tall column, and Crater excavates a shallow solid-floored dish (its floor clamps, so it never grinds into a void). None for all other schools.")]
    public TerrainShape TerrainShape;

    [Header("Presentation")]
    public GameObject CastEffectPrefab;
    public GameObject ImpactEffectPrefab;
    [Tooltip("Projectile-delivery visual shape. Auto = element default (SpellCaster).")]
    public ProjectileShape Shape = ProjectileShape.Auto;

    [Header("Mechanics")]
    [Tooltip("Restores health to friendly (Player/companion) targets instead of damaging them. Zone healing also heals allies while still damaging enemies.")]
    public bool Heals;
    [Tooltip("Outward shove (world units) applied to the target's root on hit. Positive pushes away from the caster.")]
    public float Knockback;
    [Tooltip("Self-buff: the Instant delivery grants a timed effect on the caster (e.g. flight) instead of a damage/heal raycast. Duration is the buff length in seconds.")]
    public bool SelfBuff;
    [Tooltip("Sky spell: summon a big rock that falls from above the target and reads as the spell landing. Zone and Storm deliveries defer their damage/terrain resolve to the moment the rock hits the ground. Pure visual rock — no collider.")]
    public bool SummonFallingRock;
}
