using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of skills — 64 hand-authored base skills expanded via true-prereq-depth branching
/// into ~1002 total (Layer 0 roots → Layer 1: exactly 5 branches each → Layer 2: 5 children per
/// branch + authored 2-hop locks → Layer 3: authored 3-hop locks like Tornado).
/// EVERY node is individually designed: the branch/cluster tables in SkillCatalog.*.cs
/// (partials of this class) spell out each skill's name, description and effect, so no tree slot
/// is a generated placeholder. Each skill composes shared effects (composition model): passive
/// skills use a <see cref="PassivePerkEffect"/> with a zero <see cref="Cost"/>; castables use
/// <see cref="DamageZoneEffect"/> / <see cref="SpellCastEffect"/> / <see cref="WeaponSkillEffect"/>.
/// Skills are built in code (no .asset files) and carry their <see cref="DamageKind"/> element.
/// </summary>
public static partial class SkillCatalog
{
    /// <summary>The built roster. <see cref="EnsureBuilt"/> populates it once.</summary>
    public static List<Skill> All { get; private set; }

    private static bool _built;
    private static Dictionary<string, Skill> _cache;

    /// <summary>Build the skill roster on first access (idempotent).</summary>
    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
        _cache = new Dictionary<string, Skill>(All.Count);
        foreach (var s in All)
            if (s != null && !string.IsNullOrEmpty(s.id))
                _cache[s.id] = s;
    }

    /// <summary>Look up a skill by id, or null.</summary>
    public static Skill Find(string id)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(id)) return null;
        _cache.TryGetValue(id, out var skill);
        return skill;
    }

    /// <summary>All skills in a given category.</summary>
    public static IEnumerable<Skill> OfType(SkillType type)
    {
        EnsureBuilt();
        if (All == null) yield break;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].Type == type)
                yield return All[i];
    }

    private static List<Skill> BuildDefault()
    {
        var list = new List<Skill>();

        BuildMelee(list);
        BuildRanged(list);
        BuildMagic(list);
        BuildStealth(list);
        BuildCrafting(list);
        BuildFortitude(list);
        BuildShield(list);

        ExpandTree(list);

        return list;
    }

    private static DesignBank _design;

    /// <summary>
    /// Every tree branch slot is spelled out in the partial content files. <see cref="EnsureBuilt"/>
    /// is called before the tree expands, so the bank is available to <see cref="ExpandTree"/>.
    /// </summary>
    private static DesignBank Design
    {
        get
        {
            if (_design == null) _design = BuildDesignBank();
            return _design;
        }
    }

    private static DesignBank BuildDesignBank()
    {
        var bank = new DesignBank();
        RegisterMeleeDesign(bank);
        RegisterRangedDesign(bank);
        RegisterMagicDesign(bank);
        RegisterStealthDesign(bank);
        RegisterCraftingDesign(bank);
        RegisterFortitudeDesign(bank);
        RegisterShieldDesign(bank);
        return bank;
    }

    private static void Add(List<Skill> list, string id, string name, SkillType type, bool passive,
        Cost cost, bool isMagical, DamageType kind, IEffect effect, string[] prereqs, string desc,
        int layer = 0)
    {
        var s = ScriptableObject.CreateInstance<Skill>();
        s.name = id;
        s.id = id;
        s.displayName = name;
        s.Type = type;
        s.IsPassive = passive;
        s.SkillCost = cost;
        s.IsMagical = isMagical;
        s.DamageKind = kind;
        s.Effect = effect;
        s.PrereqSkillIds = prereqs;
        s.description = desc;
        s.Layer = layer;
        list.Add(s);
    }

    private static Cost Focus(float amount) => new Cost { Resource = ResourceKind.Focus, Amount = amount, CastTime = 0.4f, Cooldown = 2f };
    private static Cost Stamina(float amount) => new Cost { Resource = ResourceKind.Stamina, Amount = amount, Cooldown = 1.2f };
    private static Cost None() => default;
    private static string[] P(params string[] ids) => ids;

    /// <summary>
    /// 1ii: the one hand-authored look profile factory. Every field is a *multiplier or a
    /// sentinel*, so a profile can only move a spell inside its school's family — it can never
    /// repaint a Fire spell purple, and it can never grant a delivery the spell does not have.
    /// 1f7 adds <paramref name="skyRock"/>, the falling-body shape for a sky spell.
    /// </summary>
    private static SpellLookProfile Look(SpellImpactStyle impact, SpellCastStyle cast,
        float scale = 1f, float tempo = 1f, float hueShift = 0f, float value = 1f, float sat = 1f,
        ProjectileShape shape = ProjectileShape.Auto, SkyRockStyle skyRock = SkyRockStyle.Inherit)
        => new SpellLookProfile
        {
            Impact = impact,
            Cast = cast,
            Scale = scale,
            Tempo = tempo,
            HueShift = hueShift,
            ValueScale = value,
            SaturationScale = sat,
            DisplayShape = shape,
            SkyRock = skyRock
        };

    private static SpellCastEffect Spell(string spellId, string spellName, DamageType type,
        float basePower, float fpCost, SpellDelivery delivery, float cooldown,
        float deliveryRange = 10f, float deliveryRadius = 1f, float castTime = 0.5f,
        bool heals = false, float knockback = 0f, float duration = 0f,
        StatusEffectType? statusEffect = null, float projectileSpeed = 20f,
        float tickInterval = 0.5f, float channelDrainPerSecond = 0f, bool selfBuff = false,
        TerrainShape terrainShape = TerrainShape.None, ProjectileShape projectileShape = ProjectileShape.Auto,
        bool summonFallingRock = false, SpellLookProfile look = null)
    {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.name = spellId;
        spell.id = spellId;
        spell.displayName = spellName;
        spell.Type = type;
        spell.BasePower = basePower;
        spell.FpCost = fpCost;
        spell.CastTime = castTime;
        spell.Cooldown = cooldown;
        spell.Delivery = delivery;
        spell.Range = deliveryRange;
        spell.Radius = deliveryRadius;
        spell.Duration = duration;
        spell.ProjectileSpeed = projectileSpeed;
        spell.TickInterval = tickInterval;
        spell.ChannelDrainPerSecond = channelDrainPerSecond;
        spell.Heals = heals;
        spell.Knockback = knockback;
        spell.SelfBuff = selfBuff;
        spell.TerrainShape = terrainShape;
        spell.Shape = projectileShape;
        spell.AppliesStatus = statusEffect.HasValue;
        spell.StatusEffect = statusEffect ?? default;
        spell.SummonFallingRock = summonFallingRock;
        // 1ii: null (the default) means "fully deterministic from spell.id" — the resolver treats a
        // profile that merely EXISTS as authored, so these 21 must be a deliberate list and not a
        // blanket default. Assigning a null here is the same as leaving the field at its default.
        spell.Look = look;
        return new SpellCastEffect { Spell = spell };
    }

    private static PassivePerkEffect Perk(PassivePerkType perk, float amount) => new PassivePerkEffect { Perk = perk, Amount = amount };
    private static DamageZoneEffect Slash(float power, DamageType kind) => new DamageZoneEffect { Radius = 2.0f, BasePower = power, Type = kind };
    private static DamageZoneEffect Zone(float radius, float power, DamageType kind) => new DamageZoneEffect { Radius = radius, BasePower = power, Type = kind };
    private static ShieldBashEffect Bash(float power, DamageType kind, float knockback = 4f) => new ShieldBashEffect { BasePower = power, Type = kind, KnockbackForce = knockback };

    private static void BuildMelee(List<Skill> list)
    {
        /* Passives (Perks) */
        Add(list, "melee_heavy_mastery", "Heavy Mastery", SkillType.Melee, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.AttackPowerPercent, 5f), null, "Increases physical attack power by 5%.");
        Add(list, "melee_finesse", "Finesse", SkillType.Melee, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.AttackSpeedPercent, 5f), null, "Increases attack speed by 5%.");
        Add(list, "melee_tough", "Tough Knuckles", SkillType.Melee, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.DamageReductionFlat, 0.02f), P("melee_heavy_mastery"), "Adds 2% damage reduction (requires Heavy Mastery).");

        /* Castables (Weapon arts / strike zones) */
        Add(list, "melee_cleave", "Cleave", SkillType.Melee, false, Stamina(10f), false, DamageType.Physical,
            Slash(18f, DamageType.Physical), null, "A wide physical slash in front of you.");
        Add(list, "melee_lunge", "Lunge", SkillType.Melee, false, Stamina(12f), false, DamageType.Physical,
            new WeaponSkillEffect(), null, "A forward thrust weapon skill (equipped weapon skill).");
        Add(list, "melee_whirlwind", "Whirlwind", SkillType.Melee, false, Stamina(18f), false, DamageType.Wind,
            Zone(2.2f, 20f, DamageType.Wind), P("melee_cleave"), "Spin, striking all nearby foes with wind force (requires Cleave).");
        Add(list, "melee_berserk", "Berserk Slash", SkillType.Melee, false, Stamina(20f), true, DamageType.Fire,
            Slash(26f, DamageType.Fire), P("melee_cleave"), "A furious flaming slash (requires Cleave).");
        Add(list, "melee_couter", "Counter Strike", SkillType.Melee, false, Stamina(16f), false, DamageType.Physical,
            Slash(24f, DamageType.Physical), P("melee_finesse"), "A precise counter blow (requires Finesse).");
        Add(list, "melee_execute", "Execute", SkillType.Melee, false, Stamina(25f), true, DamageType.Dark,
            Slash(30f, DamageType.Dark), P("melee_berserk", "melee_tough"), "A devastating dark finishing blow.");
    }

    private static void BuildRanged(List<Skill> list)
    {
        Add(list, "ranged_marksman", "Marksman", SkillType.Ranged, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.CritChanceFlat, 5f), null, "Adds 5% critical-hit chance (physical attacks).");
        Add(list, "ranged_steady", "Steady Hands", SkillType.Ranged, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.CritDamagePercent, 10f), P("ranged_marksman"), "Critical hits deal 10% extra damage (requires Marksman).");
        Add(list, "ranged_carry", "Swift Quiver", SkillType.Ranged, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.AttackSpeedPercent, 5f), null, "Increases attack speed by 5%.");

        Add(list, "ranged_pierce", "Piercing Shot", SkillType.Ranged, false, Stamina(12f), false, DamageType.Physical,
            Zone(1f, 20f, DamageType.Physical), null, "A precise piercing shot.");
        Add(list, "ranged_multishot", "Multishot", SkillType.Ranged, false, Stamina(16f), false, DamageType.Physical,
            Zone(2f, 16f, DamageType.Physical), P("ranged_pierce"), "Fire a fan of arrows (requires Piercing Shot).");
        Add(list, "ranged_arrowrain", "Arrow Rain", SkillType.Ranged, false, Stamina(24f), true, DamageType.Wind,
            Zone(3f, 22f, DamageType.Wind), P("ranged_multishot"), "Rain arrows over a wide area.");
        Add(list, "ranged_quickshot", "Quick Shot", SkillType.Ranged, false, Stamina(8f), false, DamageType.Physical,
            Slash(14f, DamageType.Physical), null, "A rapid low-damage shot.");
        Add(list, "ranged_flamearrow", "Flame Arrow", SkillType.Ranged, false, Stamina(14f), true, DamageType.Fire,
            Zone(1.4f, 18f, DamageType.Fire), null, "A fire-tipped arrow.");
        Add(list, "ranged_iceshot", "Ice Shot", SkillType.Ranged, false, Stamina(14f), true, DamageType.Ice,
            Zone(1.4f, 18f, DamageType.Ice), P("ranged_flamearrow"), "A frost arrow (requires Flame Arrow).");
        Add(list, "ranged_execute", "Heart-Seeker", SkillType.Ranged, false, Stamina(26f), true, DamageType.Arcane,
            Zone(1.6f, 28f, DamageType.Arcane), P("ranged_arrowrain"), "A lethal arcane shot.");
    }

    private static void BuildMagic(List<Skill> list)
    {
        Add(list, "magic_focus", "Focal Mind", SkillType.Magic, true, None(), false, DamageType.Arcane,
            Perk(PassivePerkType.FocusMaxPercent, 6f), null, "Increases maximum focus points by 6%.");
        Add(list, "magic_arcane", "Arcane Study", SkillType.Magic, true, None(), false, DamageType.Arcane,
            Perk(PassivePerkType.SpellDamagePercent, 6f), null, "Increases magic attack power by 6%.");
        Add(list, "magic_manaflow", "Mana Flow", SkillType.Magic, true, None(), false, DamageType.Arcane,
            Perk(PassivePerkType.FocusMaxPercent, 4f), P("magic_arcane"), "Increases maximum focus points by 4% (requires Arcane Study).");

        Add(list, "magic_fireball", "Fireball", SkillType.Magic, false, Focus(15f), true, DamageType.Fire,
            Spell("magic_fireball_spell", "Fireball", DamageType.Fire, 25f, 15f, SpellDelivery.Projectile, 4f,
                statusEffect: StatusEffectType.Burn,
                look: Look(SpellImpactStyle.Burst, SpellCastStyle.Wave, tempo: 1.15f)),
            null, "Launch a fireball that burns the target.");
        Add(list, "magic_frostbolt", "Frost Bolt", SkillType.Magic, false, Focus(13f), true, DamageType.Ice,
            Spell("magic_frostbolt_spell", "Frost Bolt", DamageType.Ice, 22f, 13f, SpellDelivery.Projectile, 4f,
                statusEffect: StatusEffectType.Chill, projectileShape: ProjectileShape.Bolt,
                look: Look(SpellImpactStyle.Shards, SpellCastStyle.Cross, value: 0.95f)),
            null, "Launch a freezing bolt that chills the target.");
        // Lightning line (§4.8): Storm Focus roots the lightning school as its OWN element now.
        // Chain Lightning is no longer a Fireball offshoot — it hangs from a dedicated lightning root.
        Add(list, "magic_lightning", "Storm Focus", SkillType.Magic, true, None(), false, DamageType.Lightning,
            Perk(PassivePerkType.CooldownReductionPercent, 6f), null, "Spells ready 6% faster, enfolding the storm.");
        Add(list, "magic_chain", "Chain Lightning", SkillType.Magic, false, Focus(20f), true, DamageType.Lightning,
            Spell("magic_chain_spell", "Chain Lightning", DamageType.Lightning, 28f, 20f, SpellDelivery.Projectile, 5f,
                statusEffect: StatusEffectType.Stagger, projectileShape: ProjectileShape.Bolt,
                look: Look(SpellImpactStyle.Cross, SpellCastStyle.Arc, tempo: 1.3f, value: 1.1f)),
            P("magic_lightning"), "Electric blast that staggers foes (requires Storm Focus).");
        Add(list, "magic_heal", "Lesser Heal", SkillType.Magic, false, Focus(10f), false, DamageType.Holy,
            Spell("magic_heal_spell", "Lesser Heal", DamageType.Holy, 15f, 10f, SpellDelivery.Instant, 0f,
                heals: true,
                look: Look(SpellImpactStyle.Bloom, SpellCastStyle.Halo, value: 1.08f, sat: 0.8f)),
            P("magic_focus"), "Restore health with a holy miracle (requires Focal Mind).");
        Add(list, "magic_ward", "Arcane Ward", SkillType.Magic, false, Focus(12f), true, DamageType.Arcane,
            Spell("magic_ward_spell", "Arcane Ward", DamageType.Arcane, 14f, 12f, SpellDelivery.Zone, 3f,
                deliveryRange: 8f, deliveryRadius: 2f, knockback: 1.5f,
                look: Look(SpellImpactStyle.Cross, SpellCastStyle.Rune)),
            P("magic_arcane"), "A protective arcane wave that shoves foes back.");
        Add(list, "magic_dark", "Dark Bolt", SkillType.Magic, false, Focus(14f), true, DamageType.Dark,
            Spell("magic_dark_spell", "Dark Bolt", DamageType.Dark, 24f, 14f, SpellDelivery.Projectile, 4f,
                statusEffect: StatusEffectType.Blind, projectileShape: ProjectileShape.Bolt,
                look: Look(SpellImpactStyle.Bloom, SpellCastStyle.HexRing, value: 0.8f, sat: 1.15f)),
            null, "Fire a shadow bolt that blinds the target.");
        Add(list, "magic_blizzard", "Blizzard", SkillType.Magic, false, Focus(28f), true, DamageType.Ice,
            Spell("magic_blizzard_spell", "Blizzard", DamageType.Ice, 22f, 28f, SpellDelivery.Storm, 6f,
                deliveryRange: 8f, deliveryRadius: 3.2f, duration: 2.5f, statusEffect: StatusEffectType.Chill,
                look: Look(SpellImpactStyle.Shards, SpellCastStyle.HexRing, scale: 1.15f, value: 0.95f)),
            P("magic_chain", "magic_frostbolt"), "A frozen storm that repeatedly chills all inside with light frost.");

        // Wind line (§3.7 Wind): Gust → Wind Blade → Gale Force → Tornado / Wind Walk. Tornado uses the
        // Vortex delivery and resolves the Great Tornado: the old environmental tornado model
        // (BuildTornado + TornadoBehavior) scaled to the spell — spins, tows and carries creatures —
        // plus Wind damage ticks + enemy pull. Other Vortex spells keep the SpellZone funnel.
        // Wind Walk is an Instant self-buff that grants timed flight (PlayerController.BeginFlight).
        Add(list, "magic_gust", "Wind Gust", SkillType.Magic, false, Focus(12f), true, DamageType.Wind,
            Spell("magic_gust_spell", "Wind Gust", DamageType.Wind, 14f, 12f, SpellDelivery.Zone, 3f,
                deliveryRadius: 2.5f, knockback: 2.5f,
                look: Look(SpellImpactStyle.Ring, SpellCastStyle.Arc, tempo: 1.2f)),
            null, "A blast of wind that scatters nearby foes.");
        Add(list, "magic_windblade", "Wind Blade", SkillType.Magic, false, Focus(15f), true, DamageType.Wind,
            Spell("magic_windblade_spell", "Wind Blade", DamageType.Wind, 18f, 15f, SpellDelivery.Projectile, 4f,
                deliveryRadius: 1.2f, knockback: 1f, projectileShape: ProjectileShape.Blade,
                look: Look(SpellImpactStyle.Cross, SpellCastStyle.Cross, tempo: 1.25f)),
            P("magic_gust"), "Hurl a razor-sharp blade of wind (requires Wind Gust).");
        Add(list, "magic_gale", "Gale Force", SkillType.Magic, false, Focus(22f), true, DamageType.Wind,
            Spell("magic_gale_spell", "Gale Force", DamageType.Wind, 24f, 22f, SpellDelivery.Zone, 6f,
                deliveryRadius: 3.4f, knockback: 2f,
                look: Look(SpellImpactStyle.Shards, SpellCastStyle.Wave, scale: 1.15f, tempo: 1.3f)),
            P("magic_windblade"), "Summon a towering storm of razor wind that drives foes back (requires Wind Blade).");
        Add(list, "magic_tornado", "Tornado", SkillType.Magic, false, Focus(28f), true, DamageType.Wind,
            Spell("magic_tornado_spell", "Tornado", DamageType.Wind, 16f, 28f, SpellDelivery.Vortex, 10f,
                deliveryRange: 12f, deliveryRadius: 3f, castTime: 0.8f,
                look: Look(SpellImpactStyle.Ring, SpellCastStyle.Wave, scale: 1.2f, tempo: 1.35f)),
            P("magic_gale"), "Summon a ravenous tornado that pulls foes in and shreds them (requires Gale Force).");
        Add(list, "magic_flight", "Wind Walk", SkillType.Magic, false, Focus(20f), true, DamageType.Wind,
            Spell("magic_flight_spell", "Wind Walk", DamageType.Wind, 0f, 18f, SpellDelivery.Instant, 25f,
                duration: 10f, selfBuff: true,
                look: Look(SpellImpactStyle.Bloom, SpellCastStyle.Arc, scale: 1.1f, sat: 1.15f)),
            P("magic_gale"), "Ride the wind and take flight for 10 seconds (requires Gale Force).");

        // Water school (§3.7): Water Bolt roots the school; water spells soak targets with the
        // Wet status (WetStatus) — Wet slows slightly and conducts, so Ice/Lightning spells deal
        // bonus damage against soaked foes. Deliveries favour flow: beam tide, vortex whirlpool,
        // mist zone, and healing spring (water as life). No terrain reshaping — that is Earth's.
        Add(list, "magic_water", "Water Bolt", SkillType.Magic, false, Focus(14f), true, DamageType.Water,
            Spell("magic_water_spell", "Water Bolt", DamageType.Water, 22f, 13f, SpellDelivery.Projectile, 4f,
                statusEffect: StatusEffectType.Wet, projectileShape: ProjectileShape.Bolt,
                look: Look(SpellImpactStyle.Sphere, SpellCastStyle.Wave, tempo: 1.1f)),
            null, "Launch a splash that soaks and slows the target.");

        // Earth school (§3.7): Stone Shard roots the school. Earth spells carry NO status effect —
        // they hit like falling rock (heavy knockback) and reshape the ground itself
        // (TerrainShape, via TerrainDeformer): rings that circle the impact, spikes that erupt,
        // walls/pillars that rear up (walls ACROSS the cast axis, 1ga — a left-right barricade), and the root Stone Shard projectile
        // carves its crater where the shard strikes (never at the caster's footing); the deep
        // Meteor and Earth Wall skills crater / wall the ground where they land.
        Add(list, "magic_earth", "Stone Shard", SkillType.Magic, false, Focus(15f), true, DamageType.Earth,
            Spell("magic_earth_spell", "Stone Shard", DamageType.Earth, 26f, 15f, SpellDelivery.Projectile, 4f,
                projectileShape: ProjectileShape.Debris, terrainShape: TerrainShape.Crater,
                look: Look(SpellImpactStyle.Shards, SpellCastStyle.HexRing, value: 0.95f)),
            null, "Hurl a fistful of living rock that carves a crater where it strikes.");

        // Meteor — the Earth school's sky-event, gated behind Boulder Crash (the falling-rock line).
        // A rock from above strikes the aim point hard and carves one of the school's permanent
        // craters there (a smooth shallow dish — always a solid walkable floor, never a void). Zone
        // delivery so the crater
        // resolves on the ground at impact; the heavy knockback reads like a meteor landing.
        Add(list, "magic_earth_meteor", "Meteor", SkillType.Magic, false, Focus(28f), true, DamageType.Earth,
            Spell("magic_earth_meteor_spell", "Meteor", DamageType.Earth, 40f, 28f, SpellDelivery.Zone, 9f,
                deliveryRange: 12f, deliveryRadius: 4f, knockback: 4f, terrainShape: TerrainShape.Crater,
                summonFallingRock: true,
                look: Look(SpellImpactStyle.Pillar, SpellCastStyle.Rune, scale: 1.25f, value: 1.05f)),
            P("magic_earth_boulder"), "A meteor plunges from the sky, carving a crater into the ground.");

        // Earth Wall — the wall-line's deep skill, gated behind Landslide (the Wall-shape branch
        // of the Boulder Crash line). Zone delivery rears a taller stone ridge across the cast axis
        // at the aim point (§3.8, TerrainShape.Wall oriented by `fwd`); the solid ridge also blocks
        // movement and projectiles. Landslide (34/24/7) → Earth Wall (36/26/8) reads as the
        // escalating wall family.
        Add(list, "magic_earth_wall", "Earth Wall", SkillType.Magic, false, Focus(26f), true, DamageType.Earth,
            Spell("magic_earth_wall_spell", "Earth Wall", DamageType.Earth, 36f, 26f, SpellDelivery.Zone, 8f,
                deliveryRange: 10f, deliveryRadius: 3.6f, knockback: 3.5f, terrainShape: TerrainShape.Wall,
                look: Look(SpellImpactStyle.Pillar, SpellCastStyle.Cross, scale: 1.2f, value: 0.95f)),
            P("magic_earth_boulder_landslide"), "A towering wall of stone rears up across the cast and crashes down on foes.");
    }

    private static void BuildStealth(List<Skill> list)
    {
        Add(list, "stealth_sneak", "Silent Steps", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.BackstabPercent, 10f), P("stealth_reflexes"), "Backstabs deal 10% extra damage (requires Quick Reflexes).");
        Add(list, "stealth_shadow", "Shadow-Touched", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.MovementSpeedPercent, 4f), null, "Increases movement speed by 4%.");
        Add(list, "stealth_reflexes", "Quick Reflexes", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.CritChanceFlat, 4f), null, "Adds 4% critical-hit chance (physical attacks).");
        Add(list, "stealth_fox", "Sly Fox", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.LootLuckPercent, 10f), null, "Increases loot quality by 10%.");
        Add(list, "stealth_nimble", "Nimble", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.AttackSpeedPercent, 4f), null, "Increases attack speed by 4%.");
        Add(list, "stealth_veil", "Veil of Night", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.DamageReductionFlat, 0.03f), P("stealth_shadow"), "The dark softens blows — adds 3% damage reduction (requires Shadow-Touched).");

        Add(list, "stealth_shadowstep", "Shadow Step", SkillType.Stealth, false, Stamina(12f), true, DamageType.Dark,
            Zone(3f, 16f, DamageType.Dark), P("stealth_veil"), "Strike from the shadows.");
        Add(list, "stealth_backstab", "Backstab", SkillType.Stealth, false, Stamina(18f), true, DamageType.Physical,
            Slash(26f, DamageType.Physical), null, "A vicious strike from behind.");
        Add(list, "stealth_cloak", "Smoke Cloud", SkillType.Stealth, false, Stamina(10f), true, DamageType.Wind,
            Zone(1.8f, 12f, DamageType.Wind), P("stealth_nimble"), "A smokescreen of wind force.");
        Add(list, "stealth_assassinate", "Assassinate", SkillType.Stealth, false, Stamina(28f), true, DamageType.Dark,
            Slash(32f, DamageType.Dark), P("stealth_backstab", "stealth_fox"), "A lethal dark finisher.");
    }

    private static void BuildCrafting(List<Skill> list)
    {
        Add(list, "craft_hands", "Steady Hands", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.LootLuckPercent, 8f), null, "Increases loot quality by 8% (gear and materials).");
        Add(list, "craft_knowledge", "Crafter's Knowledge", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.FocusMaxPercent, 8f), null, "Increases maximum focus points by 8%.");
        Add(list, "craft_focus", "Deep Focus", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.FocusRegenPercent, 10f), null, "Increases focus regeneration rate by 10%.");
        Add(list, "craft_endurance", "Endless Bending", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.StaminaMaxPercent, 8f), null, "Increases maximum stamina by 8%.");
        Add(list, "craft_efficiency", "Efficient Work", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.AttackSpeedPercent, 5f), null, "Increases attack speed by 5%.");
        Add(list, "craft_purity", "Pure Materials", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.LootLuckPercent, 6f), P("craft_hands"), "Increases loot quality by 6% (requires Steady Hands).");
        Add(list, "craft_refine", "Refinement", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.FocusMaxPercent, 5f), P("craft_knowledge"), "Increases maximum focus points by 5% (requires Crafter's Knowledge).");

        Add(list, "craft_repair", "Field Repair", SkillType.Crafting, false, Stamina(8f), false, DamageType.Physical,
            Zone(1f, 8f, DamageType.Physical), P("craft_knowledge"), "A repair pulse (restores durability, requires Crafter's Knowledge).");
        Add(list, "craft_transmute", "Transmute", SkillType.Crafting, false, Focus(12f), true, DamageType.Arcane,
            Zone(1.6f, 14f, DamageType.Arcane), P("craft_purity"), "Transmutes materials into force.");
        Add(list, "craft_forge", "Masterwork", SkillType.Crafting, false, Focus(18f), true, DamageType.Fire,
            Zone(2f, 18f, DamageType.Fire), P("craft_transmute"), "A forging inferno.");
    }

    private static void BuildFortitude(List<Skill> list)
    {
        Add(list, "fort_health", "Tough Body", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.MaxHealthPercent, 8f), null, "Increases maximum health by 8%.");
        Add(list, "fort_vitality", "Vitality", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.MaxHealthPercent, 4f), P("fort_health"), "Increases maximum health by 4% (requires Tough Body).");
        Add(list, "fort_armor", "Iron Flesh", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.DamageReductionFlat, 0.04f), null, "Adds 4% damage reduction.");
        Add(list, "fort_stamina", "Relentless", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.StaminaMaxPercent, 8f), P("fort_armor"), "Increases maximum stamina by 8% (requires Iron Flesh).");
        Add(list, "fort_recovery", "Fast Recovery", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.HealthRegenPerSecond, 0.003f), null, "Regenerates 0.3% of max health per second.");
        Add(list, "fort_steadfast", "Steadfast", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.StaggerResistPercent, 10f), P("fort_armor"), "Increases stagger resistance by 10% (requires Iron Flesh).");
        Add(list, "fort_bulwark", "Bulwark", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Perk(PassivePerkType.MaxHealthPercent, 6f), null, "Increases maximum health by 6%.");

        Add(list, "fort_stoneskin", "Stoneskin", SkillType.Fortitude, false, Focus(12f), true, DamageType.Earth,
            Zone(2f, 14f, DamageType.Earth), null, "Harden your body; smash nearby ground.");
        Add(list, "fort_guro", "Grit", SkillType.Fortitude, false, Stamina(10f), false, DamageType.Physical,
            Slash(14f, DamageType.Physical), null, "A bull-headed shoulder slam.");
        Add(list, "fort_wall", "Grim Wall", SkillType.Fortitude, false, Focus(20f), true, DamageType.Earth,
            Zone(2.8f, 20f, DamageType.Earth), P("fort_steadfast", "fort_stoneskin"), "Erupt the earth in defense.");
    }

    private static void BuildShield(List<Skill> list)
    {
        // Shield category root (§3.3): the bash line grounds every shield skill. Castables use the
        // shield's OWN bash (ShieldBashEffect) — hitting with the face of the equipped shield, not
        // with a hand-held blade — so a shield skill is meaningless without a shield in hand.
        Add(list, "shield_bash", "Shield Bash", SkillType.Shield, false, Stamina(14f), false, DamageType.Physical,
            Bash(22f, DamageType.Physical), null, "A heavy blunt bash with the face of your equipped shield.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  TREE EXPANSION — Layer 1 (5 branches per Layer 0) + Layer 2 (5 per L1 read
    //  from the hand-designed branch tables in the SkillCatalog.{Category}.cs partials).
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One designed tree-branch slot. <see cref="IsAuthored"/> slots reference a skill that
    /// <c>Build*</c> already created; the rest carry a fully hand-written skill definition.
    /// </summary>
    internal sealed class BranchSlot
    {
        public bool IsAuthored;
        public string Id;
        public string Name;
        public bool IsPassive;
        public Cost Cost;
        public bool IsMagical;
        public DamageType Kind;
        public IEffect Effect;
        public string Desc;
    }

    /// <summary>Authored-slot shorthand: references an existing skill from the <c>Build*</c> pass.</summary>
    private static BranchSlot A(string authoredId) =>
        new BranchSlot { IsAuthored = true, Id = authoredId };

    /// <summary>Designed-slot shorthand: a brand-new, hand-written skill.</summary>
    private static BranchSlot S(string id, string name, IEffect effect, string desc,
        Cost cost = default, DamageType kind = DamageType.Physical, bool magical = false,
        bool passive = false) =>
        new BranchSlot { Id = id, Name = name, Effect = effect, Desc = desc,
            Cost = cost, Kind = kind, IsMagical = magical, IsPassive = passive };

    /// <summary>Aggregates every category's designed L1/L2 tables.</summary>
    internal sealed class DesignBank
    {
        public readonly Dictionary<string, BranchSlot[]> L1 = new Dictionary<string, BranchSlot[]>();
        public readonly Dictionary<string, BranchSlot[]> L2 = new Dictionary<string, BranchSlot[]>();
    }

    private static void ExpandTree(List<Skill> list)
    {
        // Resolve each hand-authored lock to its TRUE prereq-chain depth: a root has no prereqs
        // (Layer 0), a branch off a root is Layer 1, and skills that chain 2-3 deep (Tornado,
        // Masterwork, Heart-Seeker) own Layers 2-3 instead of being flattened onto Layer 2 —
        // otherwise the L2 band overfills and depth-3 skills read as a phantom layer inside it.
        var authored = new List<Skill>(list);
        var baseDepth = new Dictionary<string, int>();
        foreach (var s in authored)
        {
            if (s.PrereqSkillIds == null || s.PrereqSkillIds.Length == 0)
                baseDepth[s.id] = 0;
            else
            {
                int md = 0;
                foreach (var pid in s.PrereqSkillIds)
                {
                    int dv;
                    if (baseDepth.TryGetValue(pid, out dv)) md = Mathf.Max(md, dv);
                }
                baseDepth[s.id] = md + 1;
            }
            s.Layer = baseDepth[s.id];
        }

        var layer0 = new List<Skill>();
        foreach (var s in authored)
            if (s.Layer == 0) layer0.Add(s);

        var layer1All = new List<Skill>();
        var layer1Ids = new HashSet<string>();

        foreach (var parent in layer0)
        {
            // The branch table (SkillCatalog.*.cs) lists the ordered 5 slots for this root.
            // Authored entries (A(...)) reference a skill Build* already created; new entries
            // (S(...)) are added here with their hand-written identity + effect.
            BranchSlot[] slots;
            if (!Design.L1.TryGetValue(parent.id, out slots))
            {
                slots = new BranchSlot[0];
                Debug.LogWarning("[SkillCatalog] No L1 design table for root: " + parent.id);
            }
            for (int ci = 0; ci < 5 && ci < slots.Length; ci++)
            {
                var slot = slots[ci];
                Skill branch;
                if (slot.IsAuthored)
                {
                    // Resolve the existing skill created by Build*. Layer is set by the
                    // depth walk that runs before this loop.
                    branch = authored.Find(s => s.id == slot.Id);
                    if (branch == null)
                    {
                        Debug.LogError("[SkillCatalog] Authored L1 slot " + slot.Id + " not found in build list");
                        continue;
                    }
                }
                else
                {
                    Add(list, slot.Id, slot.Name, parent.Type, slot.IsPassive,
                        slot.Cost, slot.IsMagical, slot.Kind, slot.Effect,
                        P(parent.id), slot.Desc, 1);
                    branch = list[list.Count - 1];
                }
                // Multi-root authored locks (e.g. Assassinate = Backstab + Sly Fox) appear
                // in multiple root clusters but must expand only once.
                if (layer1Ids.Add(branch.id))
                    layer1All.Add(branch);
            }
        }

        foreach (var parent in layer1All)
        {
            // Layer-2 children are every L1 branch's 5 individually designed grandchildren.
            BranchSlot[] kids;
            if (!Design.L2.TryGetValue(parent.id, out kids))
            {
                kids = new BranchSlot[0];
                Debug.LogWarning("[SkillCatalog] No L2 design table for L1 branch: " + parent.id);
            }
            for (int ci = 0; ci < 5 && ci < kids.Length; ci++)
            {
                var slot = kids[ci];
                Add(list, slot.Id, slot.Name, parent.Type, slot.IsPassive,
                    slot.Cost, slot.IsMagical, slot.Kind, slot.Effect,
                    P(parent.id), slot.Desc, 2);
            }
        }
    }
}