using UnityEngine;

/// <summary>
/// Impact-flash family for a spell (1ib). Every style is built from primitives by
/// <see cref="SkillFx"/>, so these stay prefab-free like the rest of the VFX layer.
/// <see cref="Inherit"/> is the sentinel that lets an authored profile decline to override — Unity
/// does not serialize <c>Nullable&lt;T&gt;</c>, so the sentinel is the enum's zero member.
/// </summary>
public enum SpellImpactStyle
{
    /// <summary>No authored opinion — take the school family's deterministic pick.</summary>
    Inherit = 0,
    /// <summary>Small cluster of shards thrown outward from the hit point.</summary>
    Burst = 1,
    /// <summary>Flat ground ring. The house default — what most spells used before 1ib.</summary>
    Ring = 2,
    /// <summary>Exploding translucent sphere (the pre-1ib projectile impact).</summary>
    Sphere = 3,
    /// <summary>Two crossed flat blades on the ground plane.</summary>
    Cross = 4,
    /// <summary>Several small angular shards radiating across the ground.</summary>
    Shards = 5,
    /// <summary>Layered soft spheres, brightest at the core.</summary>
    Bloom = 6,
    /// <summary>Vertical column flash at the hit point.</summary>
    Pillar = 7
}

/// <summary>
/// Casting-halo family for a spell (1ib) — the ring/field drawn at the caster's hand while a cast
/// charges and on release. Built once by <c>CastingCircle</c> and toggled by style, never rebuilt
/// per frame (rule 10: a metric two code paths must agree on is one named constant).
/// </summary>
public enum SpellCastStyle
{
    /// <summary>No authored opinion — take the school family's deterministic pick.</summary>
    Inherit = 0,
    /// <summary>Filled disc plus two rings — the pre-1ib casting circle.</summary>
    Circle = 1,
    /// <summary>Ring with radial tick marks.</summary>
    Rune = 2,
    /// <summary>Hexagon outline.</summary>
    HexRing = 3,
    /// <summary>Crosshair spokes.</summary>
    Cross = 4,
    /// <summary>Sweeping partial arc.</summary>
    Arc = 5,
    /// <summary>Concentric expanding waves.</summary>
    Wave = 6,
    /// <summary>Soft filled disc with a bright rim.</summary>
    Halo = 7
}

/// <summary>
/// Hand-authored per-spell nudges on top of the deterministic look (1ib). Every field is a
/// *multiplier or a sentinel*, never an absolute colour: a profile can shift a spell within its
/// school's family but cannot repaint it out of it, which is what keeps a Fire spell reading as fire.
/// Null on <see cref="SpellData"/> means "fully deterministic".
/// </summary>
[System.Serializable]
public sealed class SpellLookProfile
{
    [Tooltip("Additive hue rotation in turns (-0.5..0.5). 0 = no shift.")]
    public float HueShift;
    [Tooltip("Multiplier on the school colour's value. 1 = unchanged.")]
    public float ValueScale = 1f;
    [Tooltip("Multiplier on the school colour's saturation. 1 = unchanged.")]
    public float SaturationScale = 1f;
    [Tooltip("Multiplier on the visual size of impact/cast/projectile bodies. 1 = unchanged.")]
    public float Scale = 1f;
    [Tooltip("Multiplier on flicker/pulse rate. 1 = unchanged.")]
    public float Tempo = 1f;
    [Tooltip("Impact flash family. Inherit = deterministic pick.")]
    public SpellImpactStyle Impact = SpellImpactStyle.Inherit;
    [Tooltip("Casting halo family. Inherit = deterministic pick.")]
    public SpellCastStyle Cast = SpellCastStyle.Inherit;
    [Tooltip("Projectile body shape. Auto = inherit (never Missile — see SpellLook.DisplayShape).")]
    public ProjectileShape DisplayShape = ProjectileShape.Auto;
}

/// <summary>
/// The single resolved visual identity of a spell (1ib, §3.8).
///
/// <para><b>Why this type exists.</b> Before 1ib a spell's whole on-screen identity came from three
/// shared things: <c>DamageNumber.ColorFor(Type)</c> (10 colours for 172 spells), one
/// <c>SkillFx.ImpactSphere</c> call (deleted in 1ig, now <c>SpellImpactFx</c>), and one
/// <c>CastingCircle</c>. 172 spells could not look
/// different. This resolves each spell's look once, in one place, so no consumer re-derives a colour
/// or a shape — the rule-12 "second spelling that rots" failure, in a codebase that already had two
/// drifting DamageType palettes.</para>
///
/// <para><b>Precedence, and it is exactly three steps.</b> Authored <see cref="SpellLookProfile"/>
/// → deterministic derivation from <c>spell.id</c> → school default. Nothing else.</para>
///
/// <para><b>Family resemblance is the point.</b> Determinism varies value, saturation, scale, tempo
/// and the impact/cast family — but hue only within a tight band around the school hue, and the
/// impact/cast/shape families are per-school. Two spells that both say "fire" stay recognisably
/// fire; they just stop being the same fire.</para>
///
/// <para><b>DisplayShape is display-only, deliberately.</b> <c>SpellData.Shape == ProjectileShape.Missile</c>
/// sets <c>SpellEffect._homing = true</c> (SpellEffect.cs:75-76) — so <c>Shape</c> is partly
/// <i>behaviour</i>. A deterministic picker that wrote back to <c>spell.Shape</c> would silently
/// switch homing on for spells that never asked for it. This type therefore carries its own
/// <see cref="DisplayShape"/> and <see cref="Resolve(SpellData)"/> never touches
/// <c>spell.Shape</c>; an explicitly authored shape always wins.</para>
///
/// <para><b>Threading.</b> Every resolver here is pure and allocation-light, but the static
/// <see cref="SchoolColor"/> table and the hash are only ever read on the main thread, from cast and
/// tick paths in <c>Update</c> — not from any background generation thread.</para>
/// </summary>
public readonly struct SpellLook
{
    /// <summary>Primary colour: the school hue with this spell's value/saturation jitter.</summary>
    public readonly Color Core;
    /// <summary>Secondary colour for two-tone FX (rim, trails, shards). Derived from <see cref="Core"/>.</summary>
    public readonly Color Edge;
    /// <summary>Multiplier on impact/cast/projectile visual size. ~0.88..1.12 deterministically.</summary>
    public readonly float Scale;
    /// <summary>Multiplier on flicker/pulse rate. ~0.88..1.14 deterministically.</summary>
    public readonly float Tempo;
    /// <summary>Which impact flash this spell plays.</summary>
    public readonly SpellImpactStyle Impact;
    /// <summary>Which casting halo this spell draws.</summary>
    public readonly SpellCastStyle Cast;
    /// <summary>Projectile body shape. Authored shapes always win; deterministic picks exclude
    /// <see cref="ProjectileShape.Missile"/> because that value means "homing" (see type remarks).</summary>
    public readonly ProjectileShape DisplayShape;
    /// <summary>True when an authored <see cref="SpellLookProfile"/> supplied at least one field.</summary>
    public readonly bool Authored;

    private SpellLook(Color core, Color edge, float scale, float tempo,
        SpellImpactStyle impact, SpellCastStyle cast, ProjectileShape displayShape, bool authored)
    {
        Core = core;
        Edge = edge;
        Scale = scale;
        Tempo = tempo;
        Impact = impact;
        Cast = cast;
        DisplayShape = displayShape;
        Authored = authored;
    }

    /// <summary>
    /// The canonical DamageType palette — the ONE place a school's base colour is spelled (1ib).
    /// <c>DamageNumber.ColorFor</c> delegates here and <c>MagicTestMatrix</c>'s private
    /// <c>SchoolColor</c> was deleted in favour of this, because the project had two independently
    /// spelled tables that had already drifted (Dark was (0.70,0.55,1) in one and (0.85,0.45,1) in
    /// the other). Adding a third would have made it worse.
    /// </summary>
    public static Color SchoolColor(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire: return new Color(1f, 0.5f, 0.2f);
            case DamageType.Ice: return new Color(0.5f, 0.85f, 1f);
            case DamageType.Lightning: return new Color(1f, 0.95f, 0.4f);
            case DamageType.Holy: return new Color(1f, 0.95f, 0.7f);
            case DamageType.Dark: return new Color(0.85f, 0.45f, 1f);
            case DamageType.Wind: return new Color(0.7f, 1f, 0.95f);
            case DamageType.Earth: return new Color(0.78f, 0.62f, 0.42f);
            case DamageType.Water: return new Color(0.4f, 0.65f, 1f);
            case DamageType.Arcane: return new Color(1f, 0.5f, 1f);
            // Physical keeps the old ColorFor `default` gold verbatim. It looks wrong for arrows and
            // bolts, but 1ib's contract is "add a model, change nothing on screen", so restyling a
            // school does not belong here — it is a finding for the visual tasks with a stated
            // before/after. See THINKING.md 1ib H40.
            case DamageType.Physical: return new Color(1f, 0.9f, 0.3f);
            default: return new Color(1f, 0.9f, 0.3f);
        }
    }

    /// <summary>
    /// Resolve a spell's full look. Authored profile → deterministic-from-id → school default.
    /// Null-safe: a null spell yields the Arcane school default.
    /// </summary>
    public static SpellLook Resolve(SpellData spell)
    {
        if (spell == null)
            return Resolve(DamageType.Arcane, ProjectileShape.Auto);

        DamageType type = spell.Type;
        SpellLookProfile p = spell.Look;

        // --- deterministic layer, seeded from the STABLE id (never displayName: 1hz's lesson) ---
        // Five INDEPENDENT values. This matters: my first cut derived them as h, h*2654435761,
        // h*40503 and h^0x85EBCA6B, then took the low 16 bits of each — but XOR-ing with a constant
        // and taking the low bits is a *bijection* of the low bits, so two of them were the same
        // number rearranged, and the collision audit would have been measuring one axis five times.
        // Mix() is a murmur3 fmix32 (full avalanche), so Mix(h ^ k) for distinct k are independent.
        uint h = Fnv1a(spell.id);
        float rA = Unit(Mix(h ^ 0x9E3779B9u));
        float rB = Unit(Mix(h ^ 0x85EBCA6Bu));
        float rC = Unit(Mix(h ^ 0xC2B2AE35u));
        float rD = Unit(Mix(h ^ 0x27D4EB2Fu));
        float rE = Unit(Mix(h ^ 0x165667B1u));

        Families fam = FamiliesFor(type);

        // Hue moves only inside a tight band around the school hue (±0.04 turns ≈ ±14°) so siblings
        // stay siblings; everything else is free to vary.
        float hueShift = (rA - 0.5f) * 0.08f;
        float valueScale = 0.86f + rB * 0.30f;
        float satScale = 0.86f + rC * 0.30f;
        float scale = 0.88f + rA * 0.24f;
        float tempo = 0.88f + rB * 0.26f;

        SpellImpactStyle impact = Pick(fam.Impact, rC);
        SpellCastStyle cast = Pick(fam.Cast, rD);

        // Shape: an authored shape is real design work and wins outright. Determinism only fills the
        // Auto gap, and its families never contain Missile (that value also means "homing").
        // rE, not rD: the cast-style pick already consumed rD, and two components fed by the same
        // random value are correlated, which quietly costs uniqueness in the 1ic audit.
        ProjectileShape display = spell.Shape != ProjectileShape.Auto
            ? spell.Shape
            : Pick(fam.Shapes, rE);

        // A profile that merely exists is an authored decision even if every field is at its default.
        bool authored = p != null;
        if (p != null)
        {
            if (p.HueShift != 0f) hueShift += p.HueShift;
            if (p.ValueScale != 1f) valueScale *= p.ValueScale;
            if (p.SaturationScale != 1f) satScale *= p.SaturationScale;
            if (p.Scale != 1f) scale *= p.Scale;
            if (p.Tempo != 1f) tempo *= p.Tempo;
            if (p.Impact != SpellImpactStyle.Inherit) impact = p.Impact;
            if (p.Cast != SpellCastStyle.Inherit) cast = p.Cast;
            // Missile is honoured here but ONLY here: by 1ii the profile is hand-authored per
            // headline spell, so an author asking for Missile means it. The deterministic path
            // above may never pick it, because a look layer must not grant homing on its own.
            if (p.DisplayShape != ProjectileShape.Auto) display = p.DisplayShape;
        }

        Color baseColor = SchoolColor(type);
        Color core = Tint(baseColor, hueShift, satScale, valueScale);
        Color edge = EdgeFor(core, rC);
        return new SpellLook(core, edge, Mathf.Clamp(scale, 0.55f, 1.7f), Mathf.Clamp(tempo, 0.6f, 1.6f),
            impact, cast, display, authored);
    }

    /// <summary>
    /// Identity-less fallback for callers that have a DamageType and a shape but no
    /// <see cref="SpellData"/> — <c>SpellCaster.CreateProjectileDisplay(DamageType,
    /// ProjectileShape)</c> and any other spell-less caller. These deliberately get
    /// the <b>school default</b> with no id hash, so every such projectile looks like its school
    /// rather than pretending to be a specific spell. This is a named fallback, not a fourth
    /// precedence step: the whole point of the resolver is that there is only one rule, and callers
    /// with no spell fall off the end of it explicitly.
    /// </summary>
    public static SpellLook Resolve(DamageType type, ProjectileShape shape)
    {
        Families fam = FamiliesFor(type);
        Color baseColor = SchoolColor(type);
        ProjectileShape display = shape != ProjectileShape.Auto ? shape : Pick(fam.Shapes, 0.5f);
        return new SpellLook(baseColor, EdgeFor(baseColor, 0.5f), 1f, 1f,
            fam.Impact[0], fam.Cast[0], display, false);
    }

    // (1ig: `Fingerprint` was deleted here. 1ib built it as the 1ic audit's measuring instrument
    //  and 1ic refused to use it — grouping by a 32-bit hash reports a bucket-full collision as
    //  "two spells look the same", a claim about the instrument rather than the world. The audit
    //  packs the real axes into 34 bits instead (NewWorldTestGround.LookKey). This is also the
    //  admission that 1ib's plan named the wrong measuring device; THINKING.md 1ic H40.)

    // ------------------------------------------------------------------ families

    /// <summary>Which impact/cast/shape bodies a school is allowed to wear (1ib).</summary>
    private readonly struct Families
    {
        public readonly SpellImpactStyle[] Impact;
        public readonly SpellCastStyle[] Cast;
        public readonly ProjectileShape[] Shapes;

        public Families(SpellImpactStyle[] impact, SpellCastStyle[] cast, ProjectileShape[] shapes)
        {
            Impact = impact;
            Cast = cast;
            Shapes = shapes;
        }
    }

    // Fire is explosive and vertical, Ice is flat and crystalline, Earth is heavy and grounded, and
    // so on. None of these families contain ProjectileShape.Missile on purpose: that value also means
    // "homing" (SpellEffect.cs:75-76), and a look layer must never grant a behaviour.
    private static readonly SpellImpactStyle[] Impact_Physical = { SpellImpactStyle.Ring, SpellImpactStyle.Cross, SpellImpactStyle.Burst, SpellImpactStyle.Shards };
    private static readonly SpellImpactStyle[] Impact_Fire = { SpellImpactStyle.Burst, SpellImpactStyle.Sphere, SpellImpactStyle.Bloom, SpellImpactStyle.Pillar };
    private static readonly SpellImpactStyle[] Impact_Ice = { SpellImpactStyle.Cross, SpellImpactStyle.Shards, SpellImpactStyle.Ring, SpellImpactStyle.Bloom };
    private static readonly SpellImpactStyle[] Impact_Lightning = { SpellImpactStyle.Burst, SpellImpactStyle.Cross, SpellImpactStyle.Pillar, SpellImpactStyle.Sphere };
    private static readonly SpellImpactStyle[] Impact_Holy = { SpellImpactStyle.Bloom, SpellImpactStyle.Ring, SpellImpactStyle.Sphere, SpellImpactStyle.Pillar };
    private static readonly SpellImpactStyle[] Impact_Dark = { SpellImpactStyle.Bloom, SpellImpactStyle.Sphere, SpellImpactStyle.Shards, SpellImpactStyle.Ring };
    private static readonly SpellImpactStyle[] Impact_Wind = { SpellImpactStyle.Ring, SpellImpactStyle.Cross, SpellImpactStyle.Burst, SpellImpactStyle.Shards };
    private static readonly SpellImpactStyle[] Impact_Earth = { SpellImpactStyle.Ring, SpellImpactStyle.Pillar, SpellImpactStyle.Burst, SpellImpactStyle.Sphere };
    private static readonly SpellImpactStyle[] Impact_Water = { SpellImpactStyle.Ring, SpellImpactStyle.Bloom, SpellImpactStyle.Shards, SpellImpactStyle.Sphere };
    private static readonly SpellImpactStyle[] Impact_Arcane = { SpellImpactStyle.Burst, SpellImpactStyle.Sphere, SpellImpactStyle.Cross, SpellImpactStyle.Bloom };

    private static readonly SpellCastStyle[] Cast_Physical = { SpellCastStyle.Circle, SpellCastStyle.Cross, SpellCastStyle.Rune };
    private static readonly SpellCastStyle[] Cast_Fire = { SpellCastStyle.Circle, SpellCastStyle.Wave, SpellCastStyle.Arc, SpellCastStyle.Halo };
    private static readonly SpellCastStyle[] Cast_Ice = { SpellCastStyle.Circle, SpellCastStyle.HexRing, SpellCastStyle.Rune, SpellCastStyle.Cross };
    private static readonly SpellCastStyle[] Cast_Lightning = { SpellCastStyle.Cross, SpellCastStyle.Arc, SpellCastStyle.Circle, SpellCastStyle.HexRing };
    private static readonly SpellCastStyle[] Cast_Holy = { SpellCastStyle.Halo, SpellCastStyle.Rune, SpellCastStyle.Circle, SpellCastStyle.HexRing };
    private static readonly SpellCastStyle[] Cast_Dark = { SpellCastStyle.HexRing, SpellCastStyle.Arc, SpellCastStyle.Circle, SpellCastStyle.Rune };
    private static readonly SpellCastStyle[] Cast_Wind = { SpellCastStyle.Arc, SpellCastStyle.Wave, SpellCastStyle.Circle, SpellCastStyle.Cross };
    private static readonly SpellCastStyle[] Cast_Earth = { SpellCastStyle.Rune, SpellCastStyle.HexRing, SpellCastStyle.Circle, SpellCastStyle.Wave };
    private static readonly SpellCastStyle[] Cast_Water = { SpellCastStyle.Wave, SpellCastStyle.Circle, SpellCastStyle.Arc, SpellCastStyle.Halo };
    private static readonly SpellCastStyle[] Cast_Arcane = { SpellCastStyle.HexRing, SpellCastStyle.Rune, SpellCastStyle.Circle, SpellCastStyle.Arc };

    private static readonly ProjectileShape[] Shape_Physical = { ProjectileShape.Dart, ProjectileShape.Lance, ProjectileShape.Bolt };
    private static readonly ProjectileShape[] Shape_Fire = { ProjectileShape.Sphere, ProjectileShape.Comet, ProjectileShape.Shard };
    private static readonly ProjectileShape[] Shape_Ice = { ProjectileShape.Shard, ProjectileShape.Lance, ProjectileShape.Sphere };
    private static readonly ProjectileShape[] Shape_Lightning = { ProjectileShape.Bolt, ProjectileShape.Dart, ProjectileShape.Shard };
    private static readonly ProjectileShape[] Shape_Holy = { ProjectileShape.Sphere, ProjectileShape.Shard, ProjectileShape.Dart };
    private static readonly ProjectileShape[] Shape_Dark = { ProjectileShape.Sphere, ProjectileShape.Shard, ProjectileShape.Bolt };
    private static readonly ProjectileShape[] Shape_Wind = { ProjectileShape.Blade, ProjectileShape.Dart, ProjectileShape.Comet };
    private static readonly ProjectileShape[] Shape_Earth = { ProjectileShape.Debris, ProjectileShape.Shard, ProjectileShape.Sphere };
    private static readonly ProjectileShape[] Shape_Water = { ProjectileShape.Splash, ProjectileShape.Sphere, ProjectileShape.Shard };
    private static readonly ProjectileShape[] Shape_Arcane = { ProjectileShape.Sphere, ProjectileShape.Shard, ProjectileShape.Bolt };

    private static Families FamiliesFor(DamageType type)
    {
        switch (type)
        {
            case DamageType.Physical:
                return new Families(Impact_Physical, Cast_Physical, Shape_Physical);
            case DamageType.Fire:
                return new Families(Impact_Fire, Cast_Fire, Shape_Fire);
            case DamageType.Ice:
                return new Families(Impact_Ice, Cast_Ice, Shape_Ice);
            case DamageType.Lightning:
                return new Families(Impact_Lightning, Cast_Lightning, Shape_Lightning);
            case DamageType.Holy:
                return new Families(Impact_Holy, Cast_Holy, Shape_Holy);
            case DamageType.Dark:
                return new Families(Impact_Dark, Cast_Dark, Shape_Dark);
            case DamageType.Wind:
                return new Families(Impact_Wind, Cast_Wind, Shape_Wind);
            case DamageType.Earth:
                return new Families(Impact_Earth, Cast_Earth, Shape_Earth);
            case DamageType.Water:
                return new Families(Impact_Water, Cast_Water, Shape_Water);
            case DamageType.Arcane:
                return new Families(Impact_Arcane, Cast_Arcane, Shape_Arcane);
            default:
                return new Families(Impact_Arcane, Cast_Arcane, Shape_Arcane);
        }
    }

    private static T Pick<T>(T[] family, float r)
    {
        if (family == null || family.Length == 0) return default;
        int i = (int)(r * family.Length);
        if (i < 0) i = 0;
        if (i >= family.Length) i = family.Length - 1;
        return family[i];
    }

    // ------------------------------------------------------------------ colour + hash

    private static Color Tint(Color baseColor, float hueShift, float satScale, float valueScale)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        h = Mathf.Repeat(h + hueShift, 1f);
        s = Mathf.Clamp01(s * satScale);
        v = Mathf.Clamp01(v * valueScale);
        Color outColor = Color.HSVToRGB(h, s, v);
        outColor.a = baseColor.a;
        return outColor;
    }

    /// <summary>Secondary colour: the core hue pushed brighter and cooler, for two-tone FX.</summary>
    private static Color EdgeFor(Color core, float r)
    {
        Color.RGBToHSV(core, out float h, out float s, out float v);
        h = Mathf.Repeat(h + (r - 0.5f) * 0.06f, 1f);
        s = Mathf.Clamp01(s * 0.55f);
        v = Mathf.Clamp01(v * 1.15f + 0.12f);
        Color outColor = Color.HSVToRGB(h, s, v);
        outColor.a = core.a;
        return outColor;
    }

    /// <summary>FNV-1a over the spell id. Deterministic across runs and platforms, unlike a
    /// <c>System.Random</c> seeded by the clock — the look must not change between play sessions.</summary>
    private static uint Fnv1a(string s)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(s)) return hash;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }

    private static float Unit(uint h) => (h & 0xFFFFu) / 65535f;

    /// <summary>
    /// murmur3 32-bit finaliser — full avalanche, so every input bit reaches every output bit.
    /// Without it, values sliced out of one hash are algebraically related to each other and the
    /// per-spell variation collapses onto a single axis (see the resolver's comment).
    /// </summary>
    private static uint Mix(uint x)
    {
        unchecked
        {
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;
            return x;
        }
    }
}
