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
    Storm = 6,

    /// <summary>1l73: a single instant strike that ARCS from victim to victim — the primary hit
    /// lands along the aim, then the bolt jumps a fixed radius to nearby un-hit foes a limited
    /// number of times (Chain Lightning). Resolved in SpellCaster.ResolveChain; no projectile body,
    /// no zone — the drawn jagged bolts connect the victims.</summary>
    Chain = 7,

    /// <summary>1l87: a ground-hugging wave front that sweeps forward along the aim instead of flying
    /// straight. The behaviour lives in <see cref="SpellWave"/> (spawned by SpellCaster.ResolveWave):
    /// flatten the aim, ride the terrain at a fixed lift, hit each foe once as the band passes over it,
    /// and die after the authored <see cref="SpellData.Range"/>. It is deliberately NOT
    /// <see cref="Projectile"/>: a wave never detonates on contact, passes THROUGH foes, and has no
    /// homing, no held-body charge preview and no spin. Before 1l87 the sweep rode a hidden flag on
    /// <c>ProjectileShape.Wave</c> inside the projectile pipeline, which is why it read everywhere as a
    /// projectile; the shape value now carries only its drawn floor body.</summary>
    Wave = 8
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
    Auto = 0,     // element default (Fire→Sphere, Ice→Shard, Lightning→Bolt, Wind→Blade, ...), picked
                  // deterministically from the school's shape family in SpellLook (1l71)

    /// <summary>Jagged segmented streak built like the thunder-storm lightning bolt
    /// (RandomEventManager.SpawnJaggedBolt), colored by the element.</summary>
    Bolt = 1,

    /// <summary>Round orb (classic fireballs, plain magic balls).</summary>
    Sphere = 2,

    /// <summary>Round orb (classic fireballs, plain magic balls). <b>1l71: renamed from <see cref="Shard"/></b> —
    /// the diamond crystal it used to draw was the fireball body the user wanted gone; the thin-triangle
    /// chip identity moved to the new <see cref="Shard"/>.</summary>
    Ball = 3,

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

    /// <summary>1jy/1l87: the ground-hugging wave front's drawn floor body — a flat lattice of small
    /// cubes (see <c>MagicProjectileModelBuilder.Wave</c>), with the crest painted by a traveling
    /// sine (<see cref="WaveRipple"/>) while the wave moves. <b>Display-only since 1l87:</b> the sweep
    /// BEHAVIOUR this value used to also mean now lives in <see cref="SpellDelivery.Wave"/>, so a spell
    /// wears this shape purely for its body and nothing rides on it at the delivery layer. Like
    /// <see cref="Soul"/> and <see cref="Hand"/> it must never appear in a <c>Shape_*</c> family — only
    /// an authored <c>spell.Shape</c> may grant it. Fire Wave and Frozen Touch are the two users.</summary>
    Wave = 12,

    /// <summary>1l6x: a drifting spectral wisp — a cold soul that hunts (Chill Soul's drawn body).
    /// <b>Display-only:</b> unlike <see cref="Missile"/> this value carries NO behaviour — it is a body
    /// and nothing else. Like <see cref="Wave"/> and <see cref="Hand"/> it must never appear in a
    /// <c>Shape_*</c> family: the deterministic pick may only hand out school bodies, so an authored
    /// <c>SpellLookProfile.DisplayShape</c> is the one thing that grants it.</summary>
    Soul = 13,

    /// <summary>1l6y: a spectral reaching hand — fingers leading (+Z) on a flat translucent palm
    /// (Chill Touch's drawn body). <b>Display-only</b> like <see cref="Soul"/>: no behaviour rides on
    /// it, and it is authored-only — never in a <c>Shape_*</c> family.</summary>
    Hand = 14,

    /// <summary>1l71: a thin long flat triangle — the "frost &amp; stone chip" look that the old
    /// diamond (renamed <see cref="Ball"/>) carried, rebuilt as a sliver that tapers to a point along
    /// the flight line. A plain chip body with NO behaviour (not homing like <see cref="Missile"/>), so
    /// unlike Soul/Hand/Wave it MAY sit in the school <c>Shape_*</c> families —
    /// 1l71 swapped it into the seven families where the diamond used to sit; Fire keeps
    /// <see cref="Ball"/> so the fireball reads as a ball.</summary>
    Shard = 15,

    /// <summary>1l110: a compact, fast, high-contrast lightning streak — a short jagged bolt whose core
    /// is white-hot and whose segments are chunky, so a rapid-fire zap reads as a spark rather than a
    /// long forked bolt. A plain display body with NO behaviour (not homing), granted by an authored
    /// <c>spell.Shape</c> (Volt) rather than a <c>Look</c> profile. Deliberately NOT added to
    /// <c>Shape_Lightning</c>: moving the family would re-roll every un-authored Lightning spell's
    /// deterministic body, which is a change nobody asked for.</summary>
    VoltBolt = 16,

    /// <summary>1l110: a scattered swarm of tiny bright spark motes flying forward — Volt Spark's drawn
    /// body. A display-only shape with NO behaviour: the homing that makes Volt Spark seek is carried
    /// by <see cref="Missile"/> on <c>spell.Shape</c>, and this value overrides only the drawn body via
    /// an authored <c>Look.DisplayShape</c>. Authored-only, never in a <c>Shape_*</c> family (same
    /// contract as <see cref="Soul"/> / <see cref="Hand"/>).</summary>
    SparkSwarm = 17,

    /// <summary>1l110: a heavy great-bolt — a thick, long jagged streak wrapped around a white-hot core,
    /// the body Volt Bolt wears. A plain display body with NO behaviour, granted by an authored
    /// <c>spell.Shape</c>. Not in <c>Shape_Lightning</c> for the same reason as <see cref="VoltBolt"/>.</summary>
    GreatBolt = 18,

    /// <summary>1l111: a ground-hugging arc-wall of jagged lightning — a bowed floor whose crest carries
    /// a row of bolt spikes, so a <c>SpellDelivery.Wave</c> sweep reads as a lashing arc rather than
    /// Fire Wave's smooth sheet. Display-only: the sweep, the band width and the per-foe "once" set are
    /// all <see cref="SpellWave"/>'s, driven by the delivery and <c>spell.Radius</c> — never by this
    /// value. Granted through an authored <c>spell.Shape</c> (the <see cref="Wave"/> precedent) and
    /// deliberately not a member of <c>Shape_Lightning</c>, so no un-authored Lightning spell's
    /// deterministic <c>Auto</c> body moves.</summary>
    StormWave = 19,

    /// <summary>1l112: Fury Bolt's body — a long, thick forked lightning lance (a bright core rod with
    /// two short branches near the head, trailing flecks) that reads as heavier and more branching than
    /// <see cref="GreatBolt"/>'s single streak, so the fury-bank bolt does not duplicate Volt Bolt. A
    /// plain display body with NO behaviour, granted by an authored <c>spell.Shape</c>; not in
    /// <c>Shape_Lightning</c> for the same reason as <see cref="VoltBolt"/>.</summary>
    FuryLance = 20
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
    [Tooltip("Projectile-delivery visual shape. Auto = element default (SpellCaster). NOTE: ProjectileShape.Missile is ALSO behaviour — it turns on homing (SpellEffect.cs). ProjectileShape.Wave is display-ONLY (the sweep now lives in SpellDelivery.Wave, 1l87). Per-spell look variation goes in Look.DisplayShape, never here (1ib).")]
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
    [Tooltip("1ir: the summon belongs to its caster — it is created AT the caster (not at the ground aim point) and then FOLLOWS the caster for its whole life, taking up station behind them (1l6p: a fixed vertical ring, no longer a ground circle). One flag for one mechanic (spawn + follow): these are not separable features, they are the same 'this is your familiar' promise. 1l6s: because there is no aim point, a caster-anchored summon shows NO ground AoE aim preview. False (the default) keeps the existing ground-anchored turret at the aim point.")]
    public bool CasterAnchored;
    [Tooltip("1is: this summon SPRAYS FORWARD along the caster's aim every tick, with no target test at all — it is a stream, not a turret. Separate from CasterAnchored on purpose: sharing one flag would silently hand any future caster-anchored turret 'fires regardless of targets' for free, which is a balance change nobody would be looking for. Consequence worth knowing: with no target gate, Radius stops bounding the spell and only sizes the drawn circle; the bolts' own flight envelope decides how far it reaches.")]
    public bool SummonFiresForward;
    [Tooltip("1l73: a Zone delivery resolves AT THE CASTER instead of the ground aim point — the impact center snaps to the ground beneath the player and the aim preview draws its disc around the player too. One flag for one placement decision; nothing else reads it (Static Nova is the only Zone spell that must NOT reach the aim point).")]
    public bool CenterOnCaster;
    [Tooltip("1l73: an Instant delivery TELEPORTS the caster forward along the aim instead of raycasting a victim — Range is the leap distance, Radius the landing-burst radius. The leap leaves a jagged bolt connecting the old and new positions (Leap Bolt is the only user).")]
    public bool SelfTeleport;
    [Tooltip("1l73: a Zone delivery draws a single tall jagged bolt from the sky down to the impact center (Lightning Strike). Purely visual — the resolve is the normal instant zone blast.")]
    public bool SkyStrikeBolt;
    [Tooltip("1l73: a Storm delivery mounts a dark cloud over the storm center and each strike draws a long multi-segment bolt from the cloud down to its ground point instead of the crossed bars (Devastation). The cloud and every bolt ship with and die with the storm.")]
    public bool StormCloud;
    [Tooltip("1l113: a Projectile delivery ZAPS nearby foes IN FLIGHT — every TickInterval it damages each enemy within Radius (charge-scaled) for a fraction of the cast's power and draws a jagged Lightning arc from the projectile to it. An ADDITIONAL in-flight effect: the bolt still detonates on contact/ground/expiry as any projectile does, so Radius is the scan reach here while the impact burst stays BoltSplashRadius. Arc Volley is the only user — a slow ball of lightning that drifts and arcs to whatever it passes.")]
    public bool ZapsInFlight;
}
