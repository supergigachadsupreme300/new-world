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

    /// <summary>Raise an elongated stone ridge (an earth wall) across the cast direction.</summary>
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
    /// <para><b>1ir: this shape is NOT the meteor-line Comet skill.</b> That skill was replaced by
    /// Flamethrower and its dedicated <c>EmberStreak</c> shape deleted with it, but this
    /// <see cref="Comet"/> member predates that and is still read by Scorch and Burn. Grepping
    /// "Comet" therefore finds a live shape whose spell no longer exists - do not treat it as
    /// residue.</para></summary>
    Comet = 8,

    /// <summary>Cluster of small darts (arcane missiles).</summary>
    Missile = 9,

    /// <summary>Small sleek fast bolt-line (quick ranged shots / talisman darts).</summary>
    Dart = 10,

    /// <summary>Tumbling cluster of rock chunks (the Earth school / stone shards). Dressed like the
    /// world's breakable-rock debris (grey <c>Color.Lerp(Color.gray, Color.black, rand)</c> cubes).</summary>
    Debris = 11,

    /// <summary>1jy: a ground-hugging wave front that sweeps forward instead of flying straight.
    /// <b>This value is ALSO behaviour, like <see cref="Missile"/>:</b> it turns on the terrain-glued
    /// sweep in <c>SpellEffect</c> (flatten the aim, ride the ground at a fixed lift, hit each foe
    /// once as the band passes over it, die after the authored Range). It must therefore never appear
    /// in any <c>Shape_*</c> family — the deterministic pick would hand it to a spell as a display
    /// only, and the look layer would grant the sweep without the spell asking. Only an authored
    /// <c>spell.Shape</c> may grant it. Fire Wave is the only user.</summary>
    Wave = 12
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
    [Tooltip("1ir: Beam delivery — half-angle of the swept cone, in DEGREES. 0 (the default) is the legacy single-capsule LINE, which is how all ten other Beam spells keep working, so a cone is strictly opt-in. The beam covers the FULL 2x this angle (rays span -half..+half), so the aim preview must draw halfAngle * 2 — see SpellBeam.ConeFullAngleDegrees, which is the one place that conversion is written down.")]
    public float BeamHalfAngle = 0f;
    [Tooltip("1ir: explosion radius of a Summon/Storm turret's own bolts, independent of the targeting Radius. 0 (the default) falls back to Radius, which is why the nine existing summons are unaffected by this field. Use it when the bolt should detonate smaller than the area the turret scans.")]
    public float BoltSplashRadius = 0f;

    [Header("Status (optional, §3.7)")]
    public bool AppliesStatus;
    public StatusEffectType StatusEffect;
    [Tooltip("Chance (0..1) the status applies on a hit. Default 1 when AppliesStatus is set.")]
    public float StatusProcChance = 1f;

    [Header("Terrain (§3.8, Earth school)")]
    [Tooltip("Earth spells reshape the ground: Ring raises a stone circle around the impact point, Spikes raise rock spikes across the area, Wall rears an earth ridge across the cast direction, Pillar raises a tall column, and Crater excavates a shallow solid-floored dish (its floor clamps, so it never grinds into a void). None for all other schools.")]
    public TerrainShape TerrainShape;

    [Header("Presentation")]
    public GameObject CastEffectPrefab;
    public GameObject ImpactEffectPrefab;
    [Tooltip("Projectile-delivery visual shape. Auto = element default (SpellCaster). NOTE: this is ALSO behaviour — ProjectileShape.Missile turns on homing (SpellEffect.cs:75-76) and ProjectileShape.Wave turns on the ground-hugging forward sweep (1jy). Per-spell look variation goes in Look.DisplayShape, never here (1ib).")]
    public ProjectileShape Shape = ProjectileShape.Auto;
    [Tooltip("Hand-authored per-spell visual identity (1ib). Null = fully deterministic from the spell id. Every field is a multiplier or an Inherit sentinel, so a profile can shift a spell within its school but never repaint it out of its school.")]
    public SpellLookProfile Look;

    [Header("Mechanics")]
    [Tooltip("Restores health to friendly (Player/companion) targets instead of damaging them. Zone healing also heals allies while still damaging enemies.")]
    public bool Heals;
    [Tooltip("Outward shove (world units) applied to the target's root on hit. Positive pushes away from the caster.")]
    public float Knockback;
    [Tooltip("Self-buff: the Instant delivery grants a timed effect on the caster (e.g. flight) instead of a damage/heal raycast. Duration is the buff length in seconds.")]
    public bool SelfBuff;
    [Tooltip("Sky spell: summon a big rock that falls from above the target and reads as the spell landing. Zone and Storm deliveries defer their damage/terrain resolve to the moment the rock hits the ground. Pure visual rock — no collider.")]
    public bool SummonFallingRock;
    [Tooltip("1ir: the summon belongs to its caster — it is created AT the caster (not at the ground aim point) and then FOLLOWS the caster for its whole life, drawing its ground circle under itself. One flag for one mechanic (spawn + follow + circle): these are not separable features, they are the same 'this is your familiar' promise. False (the default) keeps the existing ground-anchored turret at the aim point.")]
    public bool CasterAnchored;
    [Tooltip("1is: this summon SPRAYS FORWARD along the caster's aim every tick, with no target test at all — it is a stream, not a turret. Separate from CasterAnchored on purpose: sharing one flag would silently hand any future caster-anchored turret 'fires regardless of targets' for free, which is a balance change nobody would be looking for. Consequence worth knowing: with no target gate, Radius stops bounding the spell and only sizes the drawn circle; the bolts' own flight envelope decides how far it reaches.")]
    public bool SummonFiresForward;
}
