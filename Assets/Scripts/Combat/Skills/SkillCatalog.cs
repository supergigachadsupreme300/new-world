using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of the 60 default skills (Phase 10) — 10 per <see cref="SkillType"/>.
/// Each skill composes shared effects (composition model): passive skills use a
/// <see cref="StatBuffEffect"/> with a zero <see cref="Cost"/>; castables use
/// <see cref="DamageZoneEffect"/> / <see cref="SpellCastEffect"/> / <see cref="WeaponArtEffect"/>.
/// Skills are built in code (no .asset files) and carry their <see cref="DamageKind"/> element.
/// </summary>
public static class SkillCatalog
{
    /// <summary>The built roster. <see cref="EnsureBuilt"/> populates it once.</summary>
    public static List<Skill> All { get; private set; }

    private static bool _built;

    /// <summary>Build the 60-skill roster on first access (idempotent).</summary>
    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
    }

    /// <summary>Look up a skill by id, or null.</summary>
    public static Skill Find(string id)
    {
        EnsureBuilt();
        if (All == null) return null;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].id == id)
                return All[i];
        return null;
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

        return list;
    }

    private static void Add(List<Skill> list, string id, string name, SkillType type, bool passive,
        Cost cost, bool isMagical, DamageType kind, IEffect effect, string[] prereqs, string desc)
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
        list.Add(s);
    }

    private static Cost Focus(float amount) => new Cost { Resource = ResourceKind.Focus, Amount = amount, CastTime = 0.4f, Cooldown = 2f };
    private static Cost Stamina(float amount) => new Cost { Resource = ResourceKind.Stamina, Amount = amount, Cooldown = 1.2f };
    private static Cost None() => default;
    private static string[] P(params string[] ids) => ids;

    private static SpellCastEffect Spell(string spellId, string spellName, DamageType type,
        float basePower, float fpCost, SpellDelivery delivery, float cooldown)
    {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.name = spellId;
        spell.id = spellId;
        spell.displayName = spellName;
        spell.Type = type;
        spell.BasePower = basePower;
        spell.FpCost = fpCost;
        spell.CastTime = 0.5f;
        spell.Cooldown = cooldown;
        spell.Delivery = delivery;
        return new SpellCastEffect { Spell = spell };
    }

    private static StatBuffEffect Buff(StatType stat, float amount) => new StatBuffEffect { Stat = stat, Amount = amount };
    private static DamageZoneEffect Slash(float power, DamageType kind) => new DamageZoneEffect { Radius = 1.6f, BasePower = power, Type = kind };
    private static DamageZoneEffect Zone(float radius, float power, DamageType kind) => new DamageZoneEffect { Radius = radius, BasePower = power, Type = kind };

    private static void BuildMelee(List<Skill> list)
    {
        /* Passives (Stats) */
        Add(list, "melee_heavy_mastery", "Heavy Mastery", SkillType.Melee, true, None(), false, DamageType.Physical,
            Buff(StatType.Strength, 3f), null, "Permanent +3 Strength.");
        Add(list, "melee_finesse", "Finesse", SkillType.Melee, true, None(), false, DamageType.Physical,
            Buff(StatType.Dexterity, 3f), null, "Permanent +3 Dexterity.");
        Add(list, "melee_tough", "Tough Knuckles", SkillType.Melee, true, None(), false, DamageType.Physical,
            Buff(StatType.Defense, 2f), null, "Permanent +2 Defense.");

        /* Castables (Weapon arts / strike zones) */
        Add(list, "melee_cleave", "Cleave", SkillType.Melee, false, Stamina(10f), false, DamageType.Physical,
            Slash(18f, DamageType.Physical), null, "A wide physical slash in front of you.");
        Add(list, "melee_lunge", "Lunge", SkillType.Melee, false, Stamina(12f), false, DamageType.Physical,
            new WeaponArtEffect(), null, "A forward thrust weapon art (equipped weapon art).");
        Add(list, "melee_whirlwind", "Whirlwind", SkillType.Melee, false, Stamina(18f), false, DamageType.Wind,
            Zone(2.2f, 20f, DamageType.Wind), null, "Spin, striking all nearby foes with wind force.");
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
            Buff(StatType.Luck, 2f), null, "Permanent +2 Luck (critical hits).");
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
            Buff(StatType.Intelligence, 2f), null, "Permanent +2 Intelligence (regen/FP).");

        Add(list, "magic_fireball", "Fireball", SkillType.Magic, false, Focus(15f), true, DamageType.Fire,
            Spell("magic_fireball_spell", "Fireball", DamageType.Fire, 25f, 15f, SpellDelivery.Projectile, 4f),
            null, "Launch a fireball.");
        Add(list, "magic_frostbolt", "Frost Bolt", SkillType.Magic, false, Focus(13f), true, DamageType.Ice,
            Spell("magic_frostbolt_spell", "Frost Bolt", DamageType.Ice, 22f, 13f, SpellDelivery.Projectile, 4f),
            null, "Launch a freezing bolt.");
        Add(list, "magic_chain", "Chain Lightning", SkillType.Magic, false, Focus(20f), true, DamageType.Lightning,
            Spell("magic_chain_spell", "Chain Lightning", DamageType.Lightning, 28f, 20f, SpellDelivery.Projectile, 5f),
            P("magic_fireball"), "Electric blast (requires Fireball).");
        Add(list, "magic_heal", "Lesser Heal", SkillType.Magic, false, Focus(10f), false, DamageType.Holy,
            Spell("magic_heal_spell", "Lesser Heal", DamageType.Holy, 15f, 10f, SpellDelivery.Instant, 0f),
            null, "Restore health with a holy miracle.");
        Add(list, "magic_ward", "Arcane Ward", SkillType.Magic, false, Focus(12f), true, DamageType.Arcane,
            Zone(2f, 14f, DamageType.Arcane), P("magic_arcane"), "A protective arcane wave.");
        Add(list, "magic_dark", "Dark Bolt", SkillType.Magic, false, Focus(14f), true, DamageType.Dark,
            Spell("magic_dark_spell", "Dark Bolt", DamageType.Dark, 24f, 14f, SpellDelivery.Projectile, 4f),
            null, "Fire a shadow bolt.");
        Add(list, "magic_blizzard", "Blizzard", SkillType.Magic, false, Focus(28f), true, DamageType.Ice,
            Zone(3.4f, 26f, DamageType.Ice), P("magic_chain", "magic_frostbolt"), "A great frozen storm.");
    }

    private static void BuildStealth(List<Skill> list)
    {
        Add(list, "stealth_sneak", "Silent Steps", SkillType.Stealth, true, None(), false, DamageType.Physical,
            Buff(StatType.Dexterity, 3f), null, "Permanent +3 Dexterity.");
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
            Buff(StatType.Intelligence, 2f), null, "Permanent +2 Intelligence.");

        Add(list, "craft_repair", "Field Repair", SkillType.Crafting, false, Stamina(8f), false, DamageType.Physical,
            Zone(1f, 8f, DamageType.Physical), null, "A repair pulse (restores durability).");
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
            Buff(StatType.Health, 4f), null, "Permanent +4 Health.");
        Add(list, "fort_armor", "Iron Flesh", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Defense, 4f), null, "Permanent +4 Defense.");
        Add(list, "fort_stamina", "Relentless", SkillType.Fortitude, true, None(), false, DamageType.Physical,
            Buff(StatType.Endurance, 4f), null, "Permanent +4 Endurance.");
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
}