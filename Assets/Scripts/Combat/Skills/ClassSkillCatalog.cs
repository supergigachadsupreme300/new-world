using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of class skills (game-design §3.2.1). Each of the 17 classes owns a small
/// radial tree — one hub + 3 thematic paths of passive modifiers and castable abilities
/// (~14 skills × 17 = ~240 total). Built in code (no .asset files), mirroring
/// <see cref="SkillCatalog"/>. Skills are auto-granted at class unlock; passives apply only
/// while that class is ACTIVE (aggregated by <see cref="ClassPassiveManager"/>).
/// </summary>
public static class ClassSkillCatalog
{
    /// <summary>The built roster. <see cref="EnsureBuilt"/> populates it once.</summary>
    public static List<ClassSkill> All { get; private set; }

    private static bool _built;
    private static Dictionary<string, ClassSkill> _cache;
    private static Dictionary<string, List<ClassSkill>> _byClass;

    /// <summary>Build the roster on first access (idempotent).</summary>
    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
        _cache = new Dictionary<string, ClassSkill>(All.Count);
        _byClass = new Dictionary<string, List<ClassSkill>>();
        foreach (var s in All)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            _cache[s.id] = s;

            string classId = s.id.Substring(0, s.id.IndexOf('.')).Replace("cls.", "");
            if (!_byClass.TryGetValue(classId, out var list))
            {
                list = new List<ClassSkill>();
                _byClass[classId] = list;
            }
            list.Add(s);
        }
    }

    /// <summary>Look up a class skill by id, or null.</summary>
    public static ClassSkill Find(string id)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(id)) return null;
        _cache.TryGetValue(id, out var skill);
        return skill;
    }

    /// <summary>All skills belonging to a class (in build order: hub, path parents, leaves).</summary>
    public static IEnumerable<ClassSkill> ForClass(string classId)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(classId)) yield break;
        if (_byClass != null && _byClass.TryGetValue(classId, out var list))
            for (int i = 0; i < list.Count; i++)
                yield return list[i];
    }

    /// <summary>True if <paramref name="skillId"/> belongs to a class's tree (findable via fallback).</summary>
    public static bool IsClassSkill(string skillId) => Find(skillId) != null;

    private static List<ClassSkill> BuildDefault()
    {
        var list = new List<ClassSkill>();
        BuildWanderer(list);
        BuildWarrior(list);
        BuildMage(list);
        BuildRogue(list);
        BuildCleric(list);
        BuildBerserker(list);
        BuildNecromancer(list);
        BuildSamurai(list);
        BuildAlchemist(list);
        BuildKnight(list);
        BuildArcher(list);
        BuildEnchanter(list);
        BuildBrawler(list);
        BuildPaladin(list);
        BuildBard(list);
        BuildTaoist(list);
        BuildMonk(list);
        BuildBlacksmith(list);
        return list;
    }

    // ── Authoring helpers ───────────────────────────────────────────────────

    private const string NP = "cls.";

    private static ClassSkill Make(List<ClassSkill> list, string classId, string node, string name,
        int layer, bool passive, Cost cost, string[] prereqs, string desc,
        IClassEffect effect = null, params ClassMod[] mods)
    {
        var s = ScriptableObject.CreateInstance<ClassSkill>();
        string id = NP + classId + "." + node;
        s.name = id;
        s.id = id;
        s.displayName = name;
        s.Layer = layer;
        s.IsPassive = passive;
        s.SkillCost = cost;
        s.PrereqSkillIds = prereqs ?? new string[0];
        s.description = desc;
        s.Effects = effect != null ? new[] { effect } : new IClassEffect[0];
        s.Mods = mods ?? new ClassMod[0];
        list.Add(s);
        return s;
    }

    private static Cost Focus(float amount) => new Cost { Resource = ResourceKind.Focus, Amount = amount, CastTime = 0.4f, Cooldown = 2f };
    private static Cost Stamina(float amount) => new Cost { Resource = ResourceKind.Stamina, Amount = amount, Cooldown = 1.2f };
    private static Cost None() => default;
    private static string[] P(params string[] ids) => ids;
    private static string HubId(string classId) => NP + classId + ".hub";
    private static string N(string classId, string node) => NP + classId + "." + node;
    private static string[] Chain(string classId, params string[] nodes) => new[] { HubId(classId) };

    private static ClassMod M(ClassModType kind, float amount) => new ClassMod { kind = kind, amount = amount };

    private static SpellData MakeSpell(string classId, string name, DamageType type, float power,
        float fp, SpellDelivery delivery, float cooldown, float range = 10f, float radius = 1.5f)
    {
        var sd = ScriptableObject.CreateInstance<SpellData>();
        sd.name = "cls_" + classId + "_" + name.ToLower().Replace(" ", "_");
        sd.id = sd.name;
        sd.displayName = name;
        sd.Type = type;
        sd.BasePower = power;
        sd.FpCost = fp;
        sd.CastTime = 0.4f;
        sd.Cooldown = cooldown;
        sd.Delivery = delivery;
        sd.Range = range;
        sd.Radius = radius;
        return sd;
    }

    // ── Wanderer ────────────────────────────────────────────────────────────

    private static void BuildWanderer(List<ClassSkill> list)
    {
        Make(list, "wanderer", "hub", "Wanderer", 0, true, None(), null,
            "Balanced basics — a jack of all trades.");

        // Survival: stamina & healing.
        Make(list, "wanderer", "surv1", "Field Toughness", 1, true, None(), Chain("wanderer"),
            "+8% stamina regen.", mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "wanderer", "surv2", "Natural Mender", 2, false, None(), P(N("wanderer", "surv1")),
            "+8% healing power; +0.5% max HP/s regen.",
            null, M(ClassModType.HealPowerMul, 0.08f), M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "wanderer", "surv3", "Bandaged Wounds", 2, false, Focus(8f), P(N("wanderer", "surv1")),
            "Restore 40 HP (scales with healing power).", new HealEffect { Amount = 40f },
            M(ClassModType.HpRegenPerSecond, 0.002f));

        // Focus: consumables & craftsmanship.
        Make(list, "wanderer", "foc1", "Handy Hands", 1, true, None(), Chain("wanderer"),
            "+8% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.08f));
        Make(list, "wanderer", "foc2", "Scavenger", 2, true, None(), P(N("wanderer", "foc1")),
            "+4% craft success, +5% repair.", null, M(ClassModType.CraftSuccessMul, 0.04f), M(ClassModType.RepairMul, 0.05f));
        Make(list, "wanderer", "foc3", "Tonic Focus", 2, false, Focus(6f), P(N("wanderer", "foc1")),
            "A restoring tonic: +1% max HP/s for a time.", new AuraEffect { Seconds = 20f, DamageReduction = 0f, HpRegenPerSecond = 0.01f });

        // Combat basics.
        Make(list, "wanderer", "cbt1", "First Knocks", 1, true, None(), Chain("wanderer"),
            "+6% melee power.", mods: M(ClassModType.MeleePowerMul, 0.06f));
        Make(list, "wanderer", "cbt2", "Rough Hide", 2, true, None(), P(N("wanderer", "cbt1")),
            "+4% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.04f));
        Make(list, "wanderer", "cbt3", "First Steps", 2, false, Stamina(10f), P(N("wanderer", "cbt1")),
            "A practiced basic strike.", new ClassStrikeEffect { Radius = 1.8f, BasePower = 20f });
    }

    // ── Warrior ─────────────────────────────────────────────────────────────

    private static void BuildWarrior(List<ClassSkill> list)
    {
        Make(list, "warrior", "hub", "Warrior", 0, true, None(), null,
            "Weapon Arts enhanced, stance breaking.");

        // Power.
        Make(list, "warrior", "pow1", "Mighty Grip", 1, true, None(), Chain("warrior"),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "warrior", "pow2", "Watchman Cut", 2, true, None(), P(N("warrior", "pow1")),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "warrior", "pow3", "Titan Swing", 2, false, Stamina(22f), P(N("warrior", "pow1")),
            "A massive two-handed swing dealing heavy damage.",
            new ClassStrikeEffect { Radius = 2.6f, BasePower = 55f }, M(ClassModType.MeleePowerMul, 0.08f));

        // War.
        Make(list, "warrior", "war1", "Battledress", 1, true, None(), Chain("warrior"),
            "+4% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.04f));
        Make(list, "warrior", "war2", "Mettle", 2, true, None(), P(N("warrior", "war1")),
            "+8% melee power, +5% melee defense.", null, M(ClassModType.MeleePowerMul, 0.08f), M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "warrior", "war3", "War Cry", 2, false, Focus(12f), P(N("warrior", "war1")),
            "Force nearby enemies to focus you for 4s.",
            new TauntEffect { Radius = 6f, Duration = 4f }, M(ClassModType.DefenseMeleeMul, 0.04f));

        // Dual.
        Make(list, "warrior", "dual1", "Quick Hands", 1, true, None(), Chain("warrior"),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "warrior", "dual2", "Relentless", 2, true, None(), P(N("warrior", "dual1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "warrior", "dual3", "Whirlwind", 2, false, Stamina(18f), P(N("warrior", "dual1")),
            "Spin, striking all nearby foes with wind force.",
            new ClassStrikeEffect { Radius = 3f, BasePower = 40f, Type = DamageType.Wind });
    }

    // ── Mage ────────────────────────────────────────────────────────────────

    private static void BuildMage(List<ClassSkill> list)
    {
        Make(list, "mage", "hub", "Mage", 0, true, None(), null,
            "Spell casting, magic damage.");

        // Fire.
        Make(list, "mage", "fire1", "Kindling", 1, true, None(), Chain("mage"),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "fire2", "Burning Focus", 2, true, None(), P(N("mage", "fire1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "fire3", "Fireball", 2, false, Focus(15f), P(N("mage", "fire1")),
            "Launch a fireball.",
            new ClassSpellEffect { Spell = MakeSpell("mage", "Fireball", DamageType.Fire, 30f, 15f, SpellDelivery.Projectile, 4f) },
            M(ClassModType.SpellPowerMul, 0.08f));

        // Cooldown / control.
        Make(list, "mage", "cd1", "Arcane Haste", 1, true, None(), Chain("mage"),
            "+5% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.05f));
        Make(list, "mage", "cd2", "Frost Study", 2, true, None(), P(N("mage", "cd1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "cd3", "Frost Nova", 2, false, Focus(14f), P(N("mage", "cd1")),
            "Freeze nearby enemies, slowing them harshly.",
            new CcZoneEffect { Radius = 4f, Duration = 4f, SlowFactor = 0.45f });

        // Arcane.
        Make(list, "mage", "arc1", "Astral Mind", 1, true, None(), Chain("mage"),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "arc2", "Channeled", 2, true, None(), P(N("mage", "arc1")),
            "+4% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.04f));
        Make(list, "mage", "arc3", "Arcane Bolt", 2, false, Focus(12f), P(N("mage", "arc1")),
            "Hurl a bolt of raw arcane energy.",
            new ClassSpellEffect { Spell = MakeSpell("mage", "Arcane Bolt", DamageType.Arcane, 26f, 12f, SpellDelivery.Projectile, 3f) });
    }

    // ── Rogue ───────────────────────────────────────────────────────────────

    private static void BuildRogue(List<ClassSkill> list)
    {
        Make(list, "rogue", "hub", "Rogue", 0, true, None(), null,
            "Backstab bonus, stealth attacks.");

        // Shadow.
        Make(list, "rogue", "sh1", "Shadow Step", 1, true, None(), Chain("rogue"),
            "+10% backstab damage.", mods: M(ClassModType.BackstabMul, 0.10f));
        Make(list, "rogue", "sh2", "Night Fang", 2, true, None(), P(N("rogue", "sh1")),
            "+10% backstab damage.", mods: M(ClassModType.BackstabMul, 0.10f));
        Make(list, "rogue", "sh3", "Vanish", 2, false, Stamina(12f), P(N("rogue", "sh1")),
            "Become invisible to enemies for 8s.",
            new StealthEffect { Seconds = 8f });

        // Trick.
        Make(list, "rogue", "tr1", "Vampiric Dagger", 1, false, Stamina(14f), Chain("rogue"),
            "A draining strike that restores 35% of damage dealt as HP.",
            new LifestealStrikeEffect { Radius = 2.2f, BasePower = 24f, LifestealFraction = 0.35f });
        Make(list, "rogue", "tr2", "Poisons", 2, true, None(), P(N("rogue", "tr1")),
            "+6% consumable potency, +4% attack speed.",
            null, M(ClassModType.ConsumablePotencyMul, 0.06f), M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "rogue", "tr3", "Phantom Blade", 2, false, Stamina(16f), P(N("rogue", "tr1")),
            "Slash with shadow energy.",
            new LifestealStrikeEffect { Radius = 2.2f, BasePower = 30f, LifestealFraction = 0.4f, Type = DamageType.Dark },
            M(ClassModType.BackstabMul, 0.10f));

        // Dagger.
        Make(list, "rogue", "dag1", "Flurry", 1, true, None(), Chain("rogue"),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "rogue", "dag2", "Killer Instinct", 2, true, None(), P(N("rogue", "dag1")),
            "+12% backstab damage.", mods: M(ClassModType.BackstabMul, 0.12f));
        Make(list, "rogue", "dag3", "Assassinate", 2, false, Stamina(18f), P(N("rogue", "dag1")),
            "A lethal dark finisher.",
            new ClassStrikeEffect { Radius = 1.8f, BasePower = 40f, Type = DamageType.Dark },
            M(ClassModType.BackstabMul, 0.05f));
    }

    // ── Cleric ──────────────────────────────────────────────────────────────

    private static void BuildCleric(List<ClassSkill> list)
    {
        Make(list, "cleric", "hub", "Cleric", 0, true, None(), null,
            "Healing miracles, buffs.");

        // Light.
        Make(list, "cleric", "l1", "Radiant Faith", 1, true, None(), Chain("cleric"),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "cleric", "l2", "Guiding Light", 2, true, None(), P(N("cleric", "l1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "cleric", "l3", "Cure Wounds", 2, false, Focus(12f), P(N("cleric", "l1")),
            "Restore 60 HP (scales with healing power).", new HealEffect { Amount = 60f });

        // Guard.
        Make(list, "cleric", "g1", "Hardened Soul", 1, true, None(), Chain("cleric"),
            "+5% blocking effectiveness.", mods: M(ClassModType.BlockingMul, 0.05f));
        Make(list, "cleric", "g2", "Fortitude", 2, true, None(), P(N("cleric", "g1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "cleric", "g3", "Sanctuary", 2, false, Focus(16f), P(N("cleric", "g1")),
            "A blessed aura: -8% damage taken, +0.2% max HP/s for 20s.",
            new AuraEffect { Seconds = 20f, DamageReduction = 0.08f, HpRegenPerSecond = 0.002f });

        // Restore.
        Make(list, "cleric", "r1", "Renewal", 1, true, None(), Chain("cleric"),
            "+0.5% max HP/s regeneration.", mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "cleric", "r2", "Blessed", 2, true, None(), P(N("cleric", "r1")),
            "+10% healing power.", mods: M(ClassModType.HealPowerMul, 0.10f));
        Make(list, "cleric", "r3", "Restoration", 2, false, Focus(18f), P(N("cleric", "r1")),
            "A powerful restore, healing 90 HP.", new HealEffect { Amount = 90f });
    }

    // ── Berserker ───────────────────────────────────────────────────────────

    private static void BuildBerserker(List<ClassSkill> list)
    {
        Make(list, "berserker", "hub", "Berserker", 0, true, None(), null,
            "Damage increases as HP drops.");

        // Rage.
        Make(list, "berserker", "ra1", "Blood Rage", 1, true, None(), Chain("berserker"),
            "Melee power rises as your HP falls.",
            mods: M(ClassModType.BerserkScale, 0.10f));
        Make(list, "berserker", "ra2", "Thrill of Battle", 2, true, None(), P(N("berserker", "ra1")),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "berserker", "ra3", "Bloodlust", 2, false, Stamina(16f), P(N("berserker", "ra1")),
            "A reckless strike that heals 40% of damage dealt.",
            new LifestealStrikeEffect { Radius = 2.4f, BasePower = 30f, LifestealFraction = 0.4f },
            M(ClassModType.BerserkScale, 0.10f));

        // Frenzy.
        Make(list, "berserker", "fr1", "Frenzied", 1, true, None(), Chain("berserker"),
            "+5% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.05f));
        Make(list, "berserker", "fr2", "Manic", 2, true, None(), P(N("berserker", "fr1")),
            "Berserk scaling deepens.",
            mods: M(ClassModType.BerserkScale, 0.10f));
        Make(list, "berserker", "fr3", "Rampage", 2, false, Stamina(18f), P(N("berserker", "fr1")),
            "Fury incarnate — a wide brutal swing.",
            new ClassStrikeEffect { Radius = 2.6f, BasePower = 40f, Type = DamageType.Fire });

        // Might.
        Make(list, "berserker", "mi1", "Unforgiving", 1, true, None(), Chain("berserker"),
            "+10% melee power.", mods: M(ClassModType.MeleePowerMul, 0.10f));
        Make(list, "berserker", "mi2", "Scarred", 2, true, None(), P(N("berserker", "mi1")),
            "+4% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.04f));
        Make(list, "berserker", "mi3", "Roar", 2, false, Stamina(12f), P(N("berserker", "mi1")),
            "A taunting bellow that draws foes to you for 3s.",
            new TauntEffect { Radius = 5f, Duration = 3f });
    }

    // ── Necromancer ─────────────────────────────────────────────────────────

    private static void BuildNecromancer(List<ClassSkill> list)
    {
        Make(list, "necromancer", "hub", "Necromancer", 0, true, None(), null,
            "Summon undead allies.");

        // Dead.
        Make(list, "necromancer", "de1", "Raise Skeleton", 1, false, Focus(16f), Chain("necromancer"),
            "Summon a skeleton ally for 30s.",
            new SummonEffect { Power = 12f, Duration = 30f, Range = 5f });
        Make(list, "necromancer", "de2", "Bone Armor", 2, true, None(), P(N("necromancer", "de1")),
            "+5% melee defense (graveyard fortitude).", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "necromancer", "de3", "Raise Brute", 2, false, Focus(26f), P(N("necromancer", "de1")),
            "Summon a powerful undead brute for 40s.",
            new SummonEffect { Power = 24f, Duration = 40f, Range = 5f }, M(ClassModType.DefenseMeleeMul, 0.04f));

        // Blood.
        Make(list, "necromancer", "bl1", "Blood Siphon", 1, false, Focus(12f), Chain("necromancer"),
            "Strike with blood magic, restoring HP.",
            new LifestealStrikeEffect { Radius = 2f, BasePower = 22f, LifestealFraction = 0.3f, Type = DamageType.Dark });
        Make(list, "necromancer", "bl2", "Pallid", 2, true, None(), P(N("necromancer", "bl1")),
            "+0.5% max HP/s regeneration.", mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "necromancer", "bl3", "Blood Rite", 2, false, Focus(16f), P(N("necromancer", "bl1")),
            "A brutal rite that heals half of damage dealt.",
            new LifestealStrikeEffect { Radius = 2.2f, BasePower = 30f, LifestealFraction = 0.5f, Type = DamageType.Dark });

        // Shadow.
        Make(list, "necromancer", "sd1", "Grave Call", 1, true, None(), Chain("necromancer"),
            "+6% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "necromancer", "sd2", "Wither", 2, true, None(), P(N("necromancer", "sd1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "necromancer", "sd3", "Bone Spear", 2, false, Focus(14f), P(N("necromancer", "sd1")),
            "Hurl a spear of bone and shadow.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 30f, Type = DamageType.Dark });
    }

    // ── Samurai ─────────────────────────────────────────────────────────────

    private static void BuildSamurai(List<ClassSkill> list)
    {
        Make(list, "samurai", "hub", "Samurai", 0, true, None(), null,
            "Perfect parry window extended.");

        // Blade.
        Make(list, "samurai", "bl1", "Flow State", 1, true, None(), Chain("samurai"),
            "+10% parry window.", mods: M(ClassModType.ParryWindowMul, 0.10f));
        Make(list, "samurai", "bl2", "Draw Speed", 2, true, None(), P(N("samurai", "bl1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "samurai", "bl3", "Iaijutsu", 2, false, Stamina(16f), P(N("samurai", "bl1")),
            "A lightning-fast draw cut.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 45f },
            M(ClassModType.ParryWindowMul, 0.05f));

        // Bushido.
        Make(list, "samurai", "bu1", "Bushido Code", 1, true, None(), Chain("samurai"),
            "+10% parry window.", mods: M(ClassModType.ParryWindowMul, 0.10f));
        Make(list, "samurai", "bu2", "Clear Mind", 2, true, None(), P(N("samurai", "bu1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "samurai", "bu3", "Moment of Clarity", 2, false, Focus(8f), P(N("samurai", "bu1")),
            "Breathe and center yourself, healing 50 HP.", new HealEffect { Amount = 50f });

        // Precision.
        Make(list, "samurai", "pr1", "Precision", 1, true, None(), Chain("samurai"),
            "+10% backstab damage (ripostes hit harder).", mods: M(ClassModType.BackstabMul, 0.10f));
        Make(list, "samurai", "pr2", "Keen Edge", 2, true, None(), P(N("samurai", "pr1")),
            "+12% parry window, +5% backstab.", null, M(ClassModType.ParryWindowMul, 0.12f), M(ClassModType.BackstabMul, 0.05f));
        Make(list, "samurai", "pr3", "Riposte", 2, false, Stamina(14f), P(N("samurai", "pr1")),
            "A devastating counter-cut.",
            new ClassStrikeEffect { Radius = 1.8f, BasePower = 38f },
            M(ClassModType.ParryWindowMul, 0.05f));
    }

    // ── Alchemist ───────────────────────────────────────────────────────────

    private static void BuildAlchemist(List<ClassSkill> list)
    {
        Make(list, "alchemist", "hub", "Alchemist", 0, true, None(), null,
            "Enhanced consumable effects.");

        // Potion.
        Make(list, "alchemist", "po1", "Distiller", 1, true, None(), Chain("alchemist"),
            "+8% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.08f));
        Make(list, "alchemist", "po2", "Master Alchemy", 2, true, None(), P(N("alchemist", "po1")),
            "+8% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.08f));
        Make(list, "alchemist", "po3", "Philosopher's Stone", 2, false, Focus(10f), P(N("alchemist", "po1")),
            "Distilled life: heal 50 HP and gain slow HP regen.",
            new HealEffect { Amount = 50f }, M(ClassModType.HpRegenPerSecond, 0.005f));

        // Goo.
        Make(list, "alchemist", "go1", "Acid Cloud", 1, false, Focus(14f), Chain("alchemist"),
            "Spawn a corrosive cloud that slows enemies.",
            new CcZoneEffect { Radius = 3.5f, Duration = 4f, SlowFactor = 0.5f });
        Make(list, "alchemist", "go2", "Caustic", 2, true, None(), P(N("alchemist", "go1")),
            "+6% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.06f));
        Make(list, "alchemist", "go3", "Sleep Dart", 2, false, Focus(12f), P(N("alchemist", "go1")),
            "A sleepy cloud that stuns enemies inside.",
            new CcZoneEffect { Radius = 2.2f, Duration = 3f, SlowFactor = 1f, Stun = true });

        // Silver.
        Make(list, "alchemist", "si1", "Tiny Forge", 1, true, None(), Chain("alchemist"),
            "+5% repair efficiency.", mods: M(ClassModType.RepairMul, 0.05f));
        Make(list, "alchemist", "si2", "Goodsmith", 2, true, None(), P(N("alchemist", "si1")),
            "+5% craft success.", mods: M(ClassModType.CraftSuccessMul, 0.05f));
        Make(list, "alchemist", "si3", "Purification", 2, true, None(), P(N("alchemist", "si1")),
            "+5% melee defense (purified body).", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
    }

    // ── Knight ──────────────────────────────────────────────────────────────

    private static void BuildKnight(List<ClassSkill> list)
    {
        Make(list, "knight", "hub", "Knight", 0, true, None(), null,
            "Buffs defense & melee; increases equip-load carry.");

        // Iron.
        Make(list, "knight", "ir1", "Plate Training", 1, true, None(), Chain("knight"),
            "+4% melee defense, +10 equip load.", null, M(ClassModType.DefenseMeleeMul, 0.04f), M(ClassModType.EquipLoadBonus, 10f));
        Make(list, "knight", "ir2", "Bearer's Might", 2, true, None(), P(N("knight", "ir1")),
            "+5% melee defense, +10 equip load.", null, M(ClassModType.DefenseMeleeMul, 0.05f), M(ClassModType.EquipLoadBonus, 10f));
        Make(list, "knight", "ir3", "Bulwark", 2, false, Focus(16f), P(N("knight", "ir1")),
            "Fortify yourself: -10% damage taken, +HP regen for 25s.",
            new AuraEffect { Seconds = 25f, DamageReduction = 0.10f, HpRegenPerSecond = 0.002f },
            M(ClassModType.EquipLoadBonus, 10f));

        // Wall.
        Make(list, "knight", "wa1", "Stable Guard", 1, true, None(), Chain("knight"),
            "+6% blocking effectiveness.", mods: M(ClassModType.BlockingMul, 0.06f));
        Make(list, "knight", "wa2", "Iron Wall", 2, true, None(), P(N("knight", "wa1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "knight", "wa3", "Challenge", 2, false, Stamina(10f), P(N("knight", "wa1")),
            "Taunt nearby enemies for 5s.",
            new TauntEffect { Radius = 6f, Duration = 5f });

        // Holy.
        Make(list, "knight", "ho1", "Warden", 1, true, None(), Chain("knight"),
            "+0.5% max HP/s regeneration.", mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "knight", "ho2", "Heavy Crusader", 2, true, None(), P(N("knight", "ho1")),
            "+4% melee defense, +10 equip load.", null, M(ClassModType.DefenseMeleeMul, 0.04f), M(ClassModType.EquipLoadBonus, 10f));
        Make(list, "knight", "ho3", "Crusader Strike", 2, false, Stamina(16f), P(N("knight", "ho1")),
            "A holy-imbued smash.",
            new ClassStrikeEffect { Radius = 2.2f, BasePower = 40f, Type = DamageType.Holy });
    }

    // ── Archer ──────────────────────────────────────────────────────────────

    private static void BuildArcher(List<ClassSkill> list)
    {
        Make(list, "archer", "hub", "Archer", 0, true, None(), null,
            "Ranged accuracy & handling.");

        // Marksman.
        Make(list, "archer", "ma1", "Steady Aim", 1, true, None(), Chain("archer"),
            "+5% ranged handling.", mods: M(ClassModType.RangedHandlingMul, 0.05f));
        Make(list, "archer", "ma2", "Hawkeye", 2, true, None(), P(N("archer", "ma1")),
            "+6% ranged handling.", mods: M(ClassModType.RangedHandlingMul, 0.06f));
        Make(list, "archer", "ma3", "Sniper Shot", 2, false, Stamina(18f), P(N("archer", "ma1")),
            "A long-range precision shot.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 50f });

        // Wind.
        Make(list, "archer", "wi1", "Quick Nock", 1, true, None(), Chain("archer"),
            "+5% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.05f));
        Make(list, "archer", "wi2", "Wind Guide", 2, true, None(), P(N("archer", "wi1")),
            "+5% ranged handling.", mods: M(ClassModType.RangedHandlingMul, 0.05f));
        Make(list, "archer", "wi3", "Wind Shot", 2, false, Focus(8f), P(N("archer", "wi1")),
            "An arrow guided by wind force.",
            new ClassSpellEffect { Spell = MakeSpell("archer", "Wind Shot", DamageType.Wind, 18f, 8f, SpellDelivery.Projectile, 4f) });

        // Hound.
        Make(list, "archer", "ho1", "Concussive Arrow", 1, false, Stamina(10f), Chain("archer"),
            "A shocking arrow that slows enemies.",
            new CcZoneEffect { Radius = 2.2f, Duration = 3f, SlowFactor = 0.5f });
        Make(list, "archer", "ho2", "Field Craft", 2, true, None(), P(N("archer", "ho1")),
            "+5% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.05f));
        Make(list, "archer", "ho3", "Volley", 2, false, Stamina(22f), P(N("archer", "ho1")),
            "Rain arrows over a wide area.",
            new ClassStrikeEffect { Radius = 3f, BasePower = 32f });
    }

    // ── Enchanter ───────────────────────────────────────────────────────────

    private static void BuildEnchanter(List<ClassSkill> list)
    {
        Make(list, "enchanter", "hub", "Enchanter", 0, true, None(), null,
            "Control/zone mage (slow, roots, area denial).");

        // Time.
        Make(list, "enchanter", "ti1", "Bend Time", 1, true, None(), Chain("enchanter"),
            "+6% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "enchanter", "ti2", "Rewind", 2, true, None(), P(N("enchanter", "ti1")),
            "+6% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "enchanter", "ti3", "Time Slow", 2, false, Focus(16f), P(N("enchanter", "ti1")),
            "Bend time in an area, slowing enemies to a crawl.",
            new CcZoneEffect { Radius = 5f, Duration = 4f, SlowFactor = 0.35f });

        // Frost.
        Make(list, "enchanter", "fr1", "Rime", 1, false, Focus(12f), Chain("enchanter"),
            "A frost field that slows enemies.",
            new CcZoneEffect { Radius = 3.5f, Duration = 3f, SlowFactor = 0.4f });
        Make(list, "enchanter", "fr2", "Bitter Cold", 2, true, None(), P(N("enchanter", "fr1")),
            "+5% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.05f));
        Make(list, "enchanter", "fr3", "Gravity Well", 2, false, Focus(18f), P(N("enchanter", "fr1")),
            "A crushing well that nearly roots and stuns.",
            new CcZoneEffect { Radius = 3f, Duration = 3f, SlowFactor = 0.25f, Stun = true });

        // Charm.
        Make(list, "enchanter", "ch1", "Magnetic Charm", 1, true, None(), Chain("enchanter"),
            "+6% spell power.", mods: M(ClassModType.SpellPowerMul, 0.06f));
        Make(list, "enchanter", "ch2", "Aura Twisting", 2, true, None(), P(N("enchanter", "ch1")),
            "+15% aura strength.", mods: M(ClassModType.AuraStrength, 0.15f));
        Make(list, "enchanter", "ch3", "Mind Shield", 2, false, Focus(14f), P(N("enchanter", "ch1")),
            "A shimmering ward: -6% damage taken for 20s.",
            new AuraEffect { Seconds = 20f, DamageReduction = 0.06f, HpRegenPerSecond = 0.001f });
    }

    // ── Brawler ─────────────────────────────────────────────────────────────

    private static void BuildBrawler(List<ClassSkill> list)
    {
        Make(list, "brawler", "hub", "Brawler", 0, true, None(), null,
            "Unarmed/grapple crowd control.");

        // Fist.
        Make(list, "brawler", "fi1", "Calloused", 1, true, None(), Chain("brawler"),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "brawler", "fi2", "Badass", 2, true, None(), P(N("brawler", "fi1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "brawler", "fi3", "Haymaker", 2, false, Stamina(16f), P(N("brawler", "fi1")),
            "A devastating telegraphed punch.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 45f });

        // Grapple.
        Make(list, "brawler", "gr1", "Bone Throw", 1, false, Stamina(10f), Chain("brawler"),
            "Hurl debris that stuns enemies in the zone.",
            new CcZoneEffect { Radius = 2f, Duration = 3f, SlowFactor = 1f, Stun = true });
        Make(list, "brawler", "gr2", "Shove", 2, true, None(), P(N("brawler", "gr1")),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "brawler", "gr3", "Bellow", 2, false, Stamina(10f), P(N("brawler", "gr1")),
            "A booming shout that draws foes in for 3s.",
            new TauntEffect { Radius = 4f, Duration = 3f });

        // Shout.
        Make(list, "brawler", "st1", "Iron Chin", 1, true, None(), Chain("brawler"),
            "+10% stagger resistance.", mods: M(ClassModType.StaggerResistMul, 0.10f));
        Make(list, "brawler", "st2", "Bulldozer", 2, true, None(), P(N("brawler", "st1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "brawler", "st3", "Whirlwind Fist", 2, false, Stamina(14f), P(N("brawler", "st1")),
            "Spin with fists and feet flailing.",
            new ClassStrikeEffect { Radius = 2.4f, BasePower = 35f },
            M(ClassModType.StaggerResistMul, 0.05f));
    }

    // ── Paladin ─────────────────────────────────────────────────────────────

    private static void BuildPaladin(List<ClassSkill> list)
    {
        Make(list, "paladin", "hub", "Paladin", 0, true, None(), null,
            "Holy tank/support (taunt, guard allies).");

        // Oath.
        Make(list, "paladin", "oa1", "Oathkeeper", 1, true, None(), Chain("paladin"),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "paladin", "oa2", "Righteous", 2, true, None(), P(N("paladin", "oa1")),
            "+4% melee defense, +8% healing power.", null, M(ClassModType.DefenseMeleeMul, 0.04f), M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "paladin", "oa3", "Lay on Hands", 2, false, Focus(14f), P(N("paladin", "oa1")),
            "Restore 70 HP (scales with healing power).", new HealEffect { Amount = 70f });

        // Guard.
        Make(list, "paladin", "gu1", "Aegis Training", 1, true, None(), Chain("paladin"),
            "+6% blocking effectiveness.", mods: M(ClassModType.BlockingMul, 0.06f));
        Make(list, "paladin", "gu2", "Holy Provocation", 2, false, Stamina(8f), P(N("paladin", "gu1")),
            "Taunts enemies for 4s.", new TauntEffect { Radius = 6f, Duration = 4f });
        Make(list, "paladin", "gu3", "Aegis", 2, false, Focus(18f), P(N("paladin", "gu1")),
            "A radiant aura: -10% damage taken, +0.3% max HP/s for 24s.",
            new AuraEffect { Seconds = 24f, DamageReduction = 0.10f, HpRegenPerSecond = 0.003f });

        // Smite.
        Make(list, "paladin", "sm1", "Holy Fire", 1, true, None(), Chain("paladin"),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "paladin", "sm2", "Divine", 2, true, None(), P(N("paladin", "sm1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "paladin", "sm3", "Smite", 2, false, Focus(16f), P(N("paladin", "sm1")),
            "Smite a foe with holy light.",
            new ClassSpellEffect { Spell = MakeSpell("paladin", "Smite", DamageType.Holy, 34f, 16f, SpellDelivery.Instant, 6f) });
    }

    // ── Bard ────────────────────────────────────────────────────────────────

    private static void BuildBard(List<ClassSkill> list)
    {
        Make(list, "bard", "hub", "Bard", 0, true, None(), null,
            "Party-wide buffs/auras.");

        // String.
        Make(list, "bard", "st1", "Encore", 1, true, None(), Chain("bard"),
            "+15% aura strength.", mods: M(ClassModType.AuraStrength, 0.15f));
        Make(list, "bard", "st2", "Lullaby", 2, true, None(), P(N("bard", "st1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "bard", "st3", "Battle Hymn", 2, false, Focus(12f), P(N("bard", "st1")),
            "An inspiring song: -8% damage taken, +HP regen for 22s.",
            new AuraEffect { Seconds = 22f, DamageReduction = 0.08f, HpRegenPerSecond = 0.002f });

        // Wind.
        Make(list, "bard", "wi1", "Dissonance", 1, false, Focus(14f), Chain("bard"),
            "A jarring tune that slows enemies.",
            new CcZoneEffect { Radius = 3.5f, Duration = 4f, SlowFactor = 0.45f });
        Make(list, "bard", "wi2", "Harmony", 2, true, None(), P(N("bard", "wi1")),
            "+15% aura strength.", mods: M(ClassModType.AuraStrength, 0.15f));
        Make(list, "bard", "wi3", "Rousing Tune", 2, false, Focus(10f), P(N("bard", "wi1")),
            "Renewing song: heal 40 HP, +8% stamina regen.",
            new HealEffect { Amount = 40f }, M(ClassModType.StaminaRegenMul, 0.08f));

        // Drum.
        Make(list, "bard", "dr1", "Drums of War", 1, true, None(), Chain("bard"),
            "+8% stamina regen.", mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "bard", "dr2", "Tempo", 2, true, None(), P(N("bard", "dr1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "bard", "dr3", "Taunting Jig", 2, false, Focus(12f), P(N("bard", "dr1")),
            "A defiant jig that taunts foes and steels allies.",
            new TauntEffect { Radius = 5f, Duration = 4f }, M(ClassModType.DefenseMeleeMul, 0.04f));
    }

    // ── Taoist ──────────────────────────────────────────────────────────────

    private static void BuildTaoist(List<ClassSkill> list)
    {
        Make(list, "taoist", "hub", "Taoist", 0, true, None(), null,
            "Qi manipulation: enhanced cooldowns & stamina regen; demon damage bonus.");

        // Qi.
        Make(list, "taoist", "qi1", "Qi Flow", 1, true, None(), Chain("taoist"),
            "+8% stamina regen.", mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "taoist", "qi2", "Qi Coalescence", 2, true, None(), P(N("taoist", "qi1")),
            "+6% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "taoist", "qi3", "Talisman", 2, false, Focus(14f), P(N("taoist", "qi1")),
            "Channel qi into a sealing talisman bolt.",
            new ClassSpellEffect { Spell = MakeSpell("taoist", "Talisman", DamageType.Arcane, 26f, 14f, SpellDelivery.Projectile, 6f) });

        // Symbol.
        Make(list, "taoist", "sy1", "Ba Gua Symbols", 1, true, None(), Chain("taoist"),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "taoist", "sy2", "Meditative", 2, true, None(), P(N("taoist", "sy1")),
            "+8% stamina regen, +0.3% max HP/s.", null, M(ClassModType.StaminaRegenMul, 0.08f), M(ClassModType.HpRegenPerSecond, 0.003f));
        Make(list, "taoist", "sy3", "Yi Symbol", 2, false, Focus(14f), P(N("taoist", "sy1")),
            "A yin-yang ward: -6% damage taken, +HP regen for 20s.",
            new AuraEffect { Seconds = 20f, DamageReduction = 0.06f, HpRegenPerSecond = 0.003f });

        // Form.
        Make(list, "taoist", "fo1", "Flowing Form", 1, true, None(), Chain("taoist"),
            "+6% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "taoist", "fo2", "Water & Wood", 2, true, None(), P(N("taoist", "fo1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "taoist", "fo3", "Flowing Water", 2, false, Focus(12f), P(N("taoist", "fo1")),
            "Qi slows all who enter: a flowing-water field.",
            new CcZoneEffect { Radius = 4f, Duration = 4f, SlowFactor = 0.4f });
    }

    // ── Monk ────────────────────────────────────────────────────────────────

    private static void BuildMonk(List<ClassSkill> list)
    {
        Make(list, "monk", "hub", "Monk", 0, true, None(), null,
            "Inner peace: meditation heals HP; reduced stagger, +defense while unarmed.");

        // Body.
        Make(list, "monk", "bo1", "Discipline", 1, true, None(), Chain("monk"),
            "+8% stamina regen.", mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "monk", "bo2", "Iron Flesh", 2, true, None(), P(N("monk", "bo1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "monk", "bo3", "Iron Body", 2, true, None(), P(N("monk", "bo1")),
            "+6% blocking effectiveness (unyielding body).", mods: M(ClassModType.BlockingMul, 0.06f));

        // Mind.
        Make(list, "monk", "mi1", "Zen", 1, true, None(), Chain("monk"),
            "+0.5% max HP/s regeneration.", mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "monk", "mi2", "Calm Breath", 2, true, None(), P(N("monk", "mi1")),
            "+8% stamina regen.", mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "monk", "mi3", "Meditation", 2, false, Focus(6f), P(N("monk", "mi1")),
            "Sit in stillness and heal 60 HP.", new HealEffect { Amount = 60f });

        // Fist.
        Make(list, "monk", "fi1", "Centered", 1, true, None(), Chain("monk"),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "monk", "fi2", "Unyielding", 2, true, None(), P(N("monk", "fi1")),
            "+10% stagger resistance.", mods: M(ClassModType.StaggerResistMul, 0.10f));
        Make(list, "monk", "fi3", "Chi Wave", 2, false, Stamina(14f), P(N("monk", "fi1")),
            "Release a wave of chi that strikes and slows.",
            new ClassStrikeEffect { Radius = 2.2f, BasePower = 38f, Type = DamageType.Wind },
            M(ClassModType.StaggerResistMul, 0.05f));
    }

    // ── Blacksmith ──────────────────────────────────────────────────────────

    private static void BuildBlacksmith(List<ClassSkill> list)
    {
        Make(list, "blacksmith", "hub", "Blacksmith", 0, true, None(), null,
            "Crafting/forge support: gear upgrade success, repair, forgiving workmanship.");

        // Forge.
        Make(list, "blacksmith", "fo1", "Apprentice Forge", 1, true, None(), Chain("blacksmith"),
            "+6% craft success.", mods: M(ClassModType.CraftSuccessMul, 0.06f));
        Make(list, "blacksmith", "fo2", "Kept Coal", 2, true, None(), P(N("blacksmith", "fo1")),
            "+8% craft success.", mods: M(ClassModType.CraftSuccessMul, 0.08f));
        Make(list, "blacksmith", "fo3", "Masterwork", 2, true, None(), P(N("blacksmith", "fo1")),
            "+8% craft success, +6% repair.", null, M(ClassModType.CraftSuccessMul, 0.08f), M(ClassModType.RepairMul, 0.06f));

        // Anvil.
        Make(list, "blacksmith", "an1", "Field Repair", 1, true, None(), Chain("blacksmith"),
            "+5% repair efficiency.", mods: M(ClassModType.RepairMul, 0.05f));
        Make(list, "blacksmith", "an2", "Sturdy Fixes", 2, true, None(), P(N("blacksmith", "an1")),
            "+6% repair efficiency.", mods: M(ClassModType.RepairMul, 0.06f));
        Make(list, "blacksmith", "an3", "Tempered Steel", 2, true, None(), P(N("blacksmith", "an1")),
            "+5% melee defense (forged-hardened).", mods: M(ClassModType.DefenseMeleeMul, 0.05f));

        // Ember.
        Make(list, "blacksmith", "em1", "Ember Study", 1, true, None(), Chain("blacksmith"),
            "+5% craft success.", mods: M(ClassModType.CraftSuccessMul, 0.05f));
        Make(list, "blacksmith", "em2", "Quenching", 2, true, None(), P(N("blacksmith", "em1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "blacksmith", "em3", "Forging Spirit", 2, true, None(), P(N("blacksmith", "em1")),
            "+6% blocking effectiveness, +10% stagger resistance.",
            null, M(ClassModType.BlockingMul, 0.06f), M(ClassModType.StaggerResistMul, 0.10f));
    }
}