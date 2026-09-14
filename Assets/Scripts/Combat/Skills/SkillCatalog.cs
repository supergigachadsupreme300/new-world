using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of skills — 64 hand-authored base skills expanded via true-prereq-depth branching
/// into ~1002 total (Layer 0 roots → Layer 1: exactly 5 branches each → Layer 2: 5 children per
/// branch + authored 2-hop locks → Layer 3: authored 3-hop locks like Tornado).
/// EVERY node is individually designed: the branch/cluster tables in SkillCatalog.*.cs
/// (partials of this class) spell out each skill's name, description and effect, so no tree slot
/// is a generated placeholder. Each skill composes shared effects (composition model): passive
/// skills use a <see cref="StatBuffEffect"/> with a zero <see cref="Cost"/>; castables use
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

    private static SpellCastEffect Spell(string spellId, string spellName, DamageType type,
        float basePower, float fpCost, SpellDelivery delivery, float cooldown,
        float deliveryRange = 10f, float deliveryRadius = 1f, float castTime = 0.5f,
        bool heals = false, float knockback = 0f, float duration = 0f,
        StatusEffectType? statusEffect = null, float projectileSpeed = 20f,
        float tickInterval = 0.5f, float channelDrainPerSecond = 0f, bool selfBuff = false)
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
        spell.AppliesStatus = statusEffect.HasValue;
        spell.StatusEffect = statusEffect ?? default;
        return new SpellCastEffect { Spell = spell };
    }

    private static StatBuffEffect Buff(StatType stat, float amount) => new StatBuffEffect { Stat = stat, Amount = amount };
    private static DamageZoneEffect Slash(float power, DamageType kind) => new DamageZoneEffect { Radius = 2.0f, BasePower = power, Type = kind };
    private static DamageZoneEffect Zone(float radius, float power, DamageType kind) => new DamageZoneEffect { Radius = radius, BasePower = power, Type = kind };

    private static void BuildMelee(List<Skill> list)
    {
        /* Passives (Stats) */
        Add(list, "melee_heavy_mastery", "Heavy Mastery", SkillType.Melee, true, None(), false, DamageType.Physical,
            Buff(StatType.Strength, 3f), null, "Permanent +3 Strength.");
        Add(list, "melee_finesse", "Finesse", SkillType.Melee, true, None(), false, DamageType.Physical,
            Buff(StatType.Dexterity, 3f), null, "Permanent +3 Dexterity.");
        Add(list, "melee_tough", "Tough Knuckles", SkillType.Melee, true, None(), false, DamageType.Physical,
            Buff(StatType.Defense, 2f), P("melee_heavy_mastery"), "Permanent +2 Defense (requires Heavy Mastery).");

        /* Castables (Weapon arts / strike zones) */
        Add(list, "melee_cleave", "Cleave", SkillType.Melee, false, Stamina(10f), false, DamageType.Physical,
            Slash(18f, DamageType.Physical), null, "A wide physical slash in front of you.");
        Add(list, "melee_lunge", "Lunge", SkillType.Melee, false, Stamina(12f), false, DamageType.Physical,
            new WeaponSkillEffect(), null, "A forward thrust weapon skill (equipped weapon skill).");
        Add(list, "melee_whirlwind", "Whirlwind", SkillType.Melee, false, Stamina(18f), false, DamageType.Wind,
            Zone(2.2f, 20f, DamageType.Wind), P("melee_cleave"), "Spin, striking all nearby foes with wind force (requires Cleave).");
        Add(list, "melee_shieldbash", "Shield Bash", SkillType.Melee, false, Stamina(14f), false, DamageType.Physical,
            Slash(22f, DamageType.Physical), null, "A heavy blunt shield strike.");
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
            Buff(StatType.Dexterity, 4f), null, "Permanent +4 Dexterity (accuracy).");
        Add(list, "ranged_steady", "Steady Hands", SkillType.Ranged, true, None(), false, DamageType.Physical,
            Buff(StatType.Luck, 2f), P("ranged_marksman"), "Permanent +2 Luck (requires Marksman).");
        Add(list, "ranged_carry", "Swift Quiver", SkillType.Ranged, true, None(), false, DamageType.Physical,
            Buff(StatType.AttackSpeed, 2f), null, "Permanent +2 Attack Speed.");

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
            Buff(StatType.Intelligence, 3f), null, "Permanent +3 Intelligence (max FP).");
        Add(list, "magic_arcane", "Arcane Study", SkillType.Magic, true, None(), false, DamageType.Arcane,
            Buff(StatType.Wisdom, 3f), null, "Permanent +3 Wisdom (spell power).");
        Add(list, "magic_manaflow", "Mana Flow", SkillType.Magic, true, None(), false, DamageType.Arcane,
            Buff(StatType.Intelligence, 2f), P("magic_arcane"), "Permanent +2 Intelligence (regen/FP, requires Arcane Study).");

        Add(list, "magic_fireball", "Fireball", SkillType.Magic, false, Focus(15f), true, DamageType.Fire,
            Spell("magic_fireball_spell", "Fireball", DamageType.Fire, 25f, 15f, SpellDelivery.Projectile, 4f,
                statusEffect: StatusEffectType.Burn),
            null, "Launch a fireball that burns the target.");
        Add(list, "magic_frostbolt", "Frost Bolt", SkillType.Magic, false, Focus(13f), true, DamageType.Ice,
            Spell("magic_frostbolt_spell", "Frost Bolt", DamageType.Ice, 22f, 13f, SpellDelivery.Projectile, 4f,
                statusEffect: StatusEffectType.Frost),
            null, "Launch a freezing bolt that chills the target.");
        // Lightning line (§4.8): Storm Focus roots the lightning school as its OWN element now.
        // Chain Lightning is no longer a Fireball offshoot — it hangs from a dedicated lightning root.
        Add(list, "magic_lightning", "Storm Focus", SkillType.Magic, true, None(), false, DamageType.Lightning,
            Buff(StatType.Intelligence, 3f), null, "Permanent +3 Intelligence (max FP), enfolding the storm.");
        Add(list, "magic_chain", "Chain Lightning", SkillType.Magic, false, Focus(20f), true, DamageType.Lightning,
            Spell("magic_chain_spell", "Chain Lightning", DamageType.Lightning, 28f, 20f, SpellDelivery.Projectile, 5f,
                statusEffect: StatusEffectType.Stagger),
            P("magic_lightning"), "Electric blast that staggers foes (requires Storm Focus).");
        Add(list, "magic_heal", "Lesser Heal", SkillType.Magic, false, Focus(10f), false, DamageType.Holy,
            Spell("magic_heal_spell", "Lesser Heal", DamageType.Holy, 15f, 10f, SpellDelivery.Instant, 0f,
                heals: true),
            P("magic_focus"), "Restore health with a holy miracle (requires Focal Mind).");
        Add(list, "magic_ward", "Arcane Ward", SkillType.Magic, false, Focus(12f), true, DamageType.Arcane,
            Spell("magic_ward_spell", "Arcane Ward", DamageType.Arcane, 14f, 12f, SpellDelivery.Zone, 3f,
                deliveryRange: 8f, deliveryRadius: 2f, knockback: 1.5f),
            P("magic_arcane"), "A protective arcane wave that shoves foes back.");
        Add(list, "magic_dark", "Dark Bolt", SkillType.Magic, false, Focus(14f), true, DamageType.Dark,
            Spell("magic_dark_spell", "Dark Bolt", DamageType.Dark, 24f, 14f, SpellDelivery.Projectile, 4f,
                statusEffect: StatusEffectType.Rot),
            null, "Fire a shadow bolt that rots the target.");
        Add(list, "magic_blizzard", "Blizzard", SkillType.Magic, false, Focus(28f), true, DamageType.Ice,
            Spell("magic_blizzard_spell", "Blizzard", DamageType.Ice, 22f, 28f, SpellDelivery.Storm, 6f,
                deliveryRange: 8f, deliveryRadius: 3.2f, duration: 2.5f, statusEffect: StatusEffectType.Frost),
            P("magic_chain", "magic_frostbolt"), "A frozen storm that repeatedly strikes all inside with frost.");

        // Wind line (§3.7 Wind): Gust → Wind Blade → Gale Force → Tornado / Wind Walk. Tornado uses the
        // Vortex delivery and resolves the Great Tornado: the old environmental tornado model
        // (BuildTornado + TornadoBehavior) scaled to the spell — spins, tows and carries creatures —
        // plus Wind damage ticks + enemy pull. Other Vortex spells keep the SpellZone funnel.
        // Wind Walk is an Instant self-buff that grants timed flight (PlayerController.BeginFlight).
        Add(list, "magic_gust", "Wind Gust", SkillType.Magic, false, Focus(12f), true, DamageType.Wind,
            Spell("magic_gust_spell", "Wind Gust", DamageType.Wind, 14f, 12f, SpellDelivery.Zone, 3f,
                deliveryRadius: 2.5f, knockback: 2.5f),
            null, "A blast of wind that scatters nearby foes.");
        Add(list, "magic_windblade", "Wind Blade", SkillType.Magic, false, Focus(15f), true, DamageType.Wind,
            Spell("magic_windblade_spell", "Wind Blade", DamageType.Wind, 18f, 15f, SpellDelivery.Projectile, 4f,
                deliveryRadius: 1.2f, knockback: 1f),
            P("magic_gust"), "Hurl a razor-sharp blade of wind (requires Wind Gust).");
        Add(list, "magic_gale", "Gale Force", SkillType.Magic, false, Focus(22f), true, DamageType.Wind,
            Spell("magic_gale_spell", "Gale Force", DamageType.Wind, 24f, 22f, SpellDelivery.Zone, 6f,
                deliveryRadius: 3.4f, knockback: 2f),
            P("magic_windblade"), "Summon a towering storm of razor wind that drives foes back (requires Wind Blade).");
        Add(list, "magic_tornado", "Tornado", SkillType.Magic, false, Focus(28f), true, DamageType.Wind,
            Spell("magic_tornado_spell", "Tornado", DamageType.Wind, 16f, 28f, SpellDelivery.Vortex, 10f,
                deliveryRange: 12f, deliveryRadius: 3f, castTime: 0.8f),
            P("magic_gale"), "Summon a ravenous tornado that pulls foes in and shreds them (requires Gale Force).");
        Add(list, "magic_flight", "Wind Walk", SkillType.Magic, false, Focus(20f), true, DamageType.Wind,
            Spell("magic_flight_spell", "Wind Walk", DamageType.Wind, 0f, 18f, SpellDelivery.Instant, 25f,
                duration: 10f, selfBuff: true),
            P("magic_gale"), "Ride the wind and take flight for 10 seconds (requires Gale Force).");
    }

    private static void BuildStealth(List<Skill> list)
    {
        Add(list, "stealth_sneak", "Silent Steps", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Buff(StatType.Dexterity, 3f), P("stealth_reflexes"), "Permanent +3 Dexterity (requires Quick Reflexes).");
        Add(list, "stealth_shadow", "Shadow-Touched", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Buff(StatType.Speed, 2f), null, "Permanent +2 Speed.");
        Add(list, "stealth_reflexes", "Quick Reflexes", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Buff(StatType.Dexterity, 2f), null, "Permanent +2 Dexterity.");
        Add(list, "stealth_fox", "Sly Fox", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Buff(StatType.Luck, 3f), null, "Permanent +3 Luck.");
        Add(list, "stealth_nimble", "Nimble", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Buff(StatType.AttackSpeed, 2f), null, "Permanent +2 Attack Speed.");
        Add(list, "stealth_veil", "Veil of Night", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Buff(StatType.Dexterity, 4f), P("stealth_shadow"), "Deepens the darkness around you (requires Shadow-Touched).");

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
            Buff(StatType.Luck, 3f), null, "Permanent +3 Luck (crafting quality).");
        Add(list, "craft_knowledge", "Crafter's Knowledge", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Buff(StatType.Intelligence, 3f), null, "Permanent +3 Intelligence.");
        Add(list, "craft_focus", "Deep Focus", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Buff(StatType.Wisdom, 2f), null, "Permanent +2 Wisdom.");
        Add(list, "craft_endurance", "Endless Bending", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Buff(StatType.Endurance, 3f), null, "Permanent +3 Endurance.");
        Add(list, "craft_efficiency", "Efficient Work", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Buff(StatType.AttackSpeed, 2f), null, "Permanent +2 Attack Speed.");
        Add(list, "craft_purity", "Pure Materials", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Buff(StatType.Luck, 3f), P("craft_hands"), "Permanent +3 Luck (requires Steady Hands).");
        Add(list, "craft_refine", "Refinement", SkillType.Crafting, true, None(), false, DamageType.Physical,
            Buff(StatType.Intelligence, 2f), P("craft_knowledge"), "Permanent +2 Intelligence (requires Crafter's Knowledge).");

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
            Buff(StatType.Health, 4f), null, "Permanent +4 Health.");
        Add(list, "fort_vitality", "Vitality", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Health, 4f), P("fort_health"), "Permanent +4 Health (requires Tough Body).");
        Add(list, "fort_armor", "Iron Flesh", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Defense, 4f), null, "Permanent +4 Defense.");
        Add(list, "fort_stamina", "Relentless", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Endurance, 4f), P("fort_armor"), "Permanent +4 Endurance (requires Iron Flesh).");
        Add(list, "fort_recovery", "Fast Recovery", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Health, 2f), null, "Permanent +2 Health (regen).");
        Add(list, "fort_steadfast", "Steadfast", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Defense, 3f), P("fort_armor"), "Permanent +3 Defense (requires Iron Flesh).");
        Add(list, "fort_bulwark", "Bulwark", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Health, 3f), null, "Permanent +3 Health.");

        Add(list, "fort_stoneskin", "Stoneskin", SkillType.Fortitude, false, Focus(12f), true, DamageType.Earth,
            Zone(2f, 14f, DamageType.Earth), null, "Harden your body; smash nearby ground.");
        Add(list, "fort_guro", "Grit", SkillType.Fortitude, false, Stamina(10f), false, DamageType.Physical,
            Slash(14f, DamageType.Physical), null, "A bull-headed shoulder slam.");
        Add(list, "fort_wall", "Grim Wall", SkillType.Fortitude, false, Focus(20f), true, DamageType.Earth,
            Zone(2.8f, 20f, DamageType.Earth), P("fort_steadfast", "fort_stoneskin"), "Erupt the earth in defense.");
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