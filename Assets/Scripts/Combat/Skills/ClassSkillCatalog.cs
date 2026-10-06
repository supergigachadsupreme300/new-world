using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of class skills (game-design §3.2.1). Each of the 18 classes owns a small
/// radial tree — one hub + 3 thematic paths of passive modifiers and castable abilities
/// (~10 skills × 18 = ~180 total). Built in code (no .asset files), mirroring
/// <see cref="SkillCatalog"/>. Skills are auto-granted at class unlock; passives apply only
/// while that class is ACTIVE (aggregated by <see cref="ClassPassiveManager"/>).
///
/// Each class's 3 paths are themed around different racial archetypes (§3.5), so the player's
/// race naturally synergizes with one path more than the others.
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

    /// <summary>1ii: class-spell look profiles, same all-multiplier contract as SkillCatalog.Look.</summary>
    /// <para><b>1jt adds <paramref name="castAnchor"/></b> so this factory can express the same
    /// profile as the one in <c>SkillCatalog.cs</c>. It has no <c>skyrock</c> parameter and still did
    /// not need one — no class spell is a <c>SummonFallingRock</c> sky spell — but a factory that
    /// silently cannot express a field is the kind of gap that gets rediscovered as a bug.</para>
    /// <para><b>Overload trap, stated because it is real:</b> this and
    /// <c>SkillCatalog.Look</c> are two overloads in the same partial class, and THIS one substitutes
    /// fewer optional defaults — so a call that does not name <c>skyrock</c> or <c>castAnchor</c>
    /// binds here, not there. That is harmless while both factories produce the same profile for the
    /// fields they share, and it is exactly why <c>castAnchor</c> was added to both rather than one.</para>
    /// </summary>
    private static SpellLookProfile Look(SpellImpactStyle impact, SpellCastStyle cast,
        float scale = 1f, float tempo = 1f, float hueShift = 0f, float value = 1f, float sat = 1f,
        ProjectileShape shape = ProjectileShape.Auto, SpellCastAnchor castAnchor = SpellCastAnchor.Inherit)
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
            CastAnchor = castAnchor
        };

    private static SpellData MakeSpell(string classId, string name, DamageType type, float power,
        float fp, SpellDelivery delivery, float cooldown, float range = 10f, float radius = 1.5f,
        ProjectileShape shape = ProjectileShape.Auto, SpellLookProfile look = null)
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
        sd.Shape = shape;
        // 1ii: same contract as SkillCatalog.Spell — null means "fully deterministic from sd.id".
        sd.Look = look;
        return sd;
    }

    // ── Wanderer (baseline) ────────────────────────────────────────────────
    // Paths: Survival / Focus / Combat — Universal; Human (all), Dwarf (Focus), Orc (Combat)

    private static void BuildWanderer(List<ClassSkill> list)
    {
        Make(list, "wanderer", "hub", "Wanderer", 0, true, None(), null,
            "Balanced basics — a jack of all trades. Works with any race.");

        // Survival — universal survivability.
        Make(list, "wanderer", "surv1", "Field Toughness", 1, true, None(), Chain("wanderer"),
            "+8% stamina regen. Synergizes with Endurance-focused races (Undead, Fire Giant).",
            mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "wanderer", "surv2", "Natural Mender", 2, false, None(), P(N("wanderer", "surv1")),
            "+8% healing power; +0.5% max HP/s regen.",
            null, M(ClassModType.HealPowerMul, 0.08f), M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "wanderer", "surv3", "Bandaged Wounds", 2, false, Focus(8f), P(N("wanderer", "surv1")),
            "Restore 40 HP (scales with healing power).", new HealEffect { Amount = 40f },
            M(ClassModType.HpRegenPerSecond, 0.002f));

        // Focus — crafting & consumables. Best with Dwarf (+20% craft yield), Goblin, Gnome.
        Make(list, "wanderer", "foc1", "Handy Hands", 1, true, None(), Chain("wanderer"),
            "+8% consumable potency. Dwarf/Goblin/Gnome gain the most from this path.",
            mods: M(ClassModType.ConsumablePotencyMul, 0.08f));
        Make(list, "wanderer", "foc2", "Scavenger", 2, true, None(), P(N("wanderer", "foc1")),
            "+4% craft success, +5% repair.", null, M(ClassModType.CraftSuccessMul, 0.04f), M(ClassModType.RepairMul, 0.05f));
        Make(list, "wanderer", "foc3", "Tonic Focus", 2, false, Focus(6f), P(N("wanderer", "foc1")),
            "A restoring tonic: +1% max HP/s for a time.", new AuraEffect { Seconds = 20f, DamageReduction = 0f, HpRegenPerSecond = 0.01f });

        // Combat — melee basics. Best with Orc (+25% Str), Werewolf, Draconic.
        Make(list, "wanderer", "cbt1", "First Knocks", 1, true, None(), Chain("wanderer"),
            "+6% melee power. Strength-focused races (Orc, Werewolf) scale this hardest.",
            mods: M(ClassModType.MeleePowerMul, 0.06f));
        Make(list, "wanderer", "cbt2", "Rough Hide", 2, true, None(), P(N("wanderer", "cbt1")),
            "+4% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.04f));
        Make(list, "wanderer", "cbt3", "First Steps", 2, false, Stamina(10f), P(N("wanderer", "cbt1")),
            "A practiced basic strike.", new ClassStrikeEffect { Radius = 1.8f, BasePower = 20f });
    }

    // ── Warrior ─────────────────────────────────────────────────────────────
    // Paths: Brute / Bulwark / Duelist — Orc/Golem (Brute+Bulwark), Fire Giant (Bulwark), Werewolf/Skeleton (Duelist)

    private static void BuildWarrior(List<ClassSkill> list)
    {
        Make(list, "warrior", "hub", "Warrior", 0, true, None(), null,
            "Frontline melee damage and battlefield control.");

        // Brute — raw power. Orc (+25% Str, +15% stagger), Fire Giant, Golem.
        Make(list, "warrior", "pow1", "Iron Grip", 1, true, None(), Chain("warrior"),
            "+8% melee power. Orc's +25% Str scales this hardest.",
            mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "warrior", "pow2", "Bone Crusher", 2, true, None(), P(N("warrior", "pow1")),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "warrior", "pow3", "Titan Swing", 2, false, Stamina(22f), P(N("warrior", "pow1")),
            "A massive two-handed swing dealing heavy damage.",
            new ClassStrikeEffect { Radius = 2.6f, BasePower = 55f }, M(ClassModType.MeleePowerMul, 0.08f));

        // Bulwark — defense & taunt. Golem (Stone Skin stacks), Fire/Ice Giant (tanky).
        Make(list, "warrior", "war1", "Ironhide", 1, true, None(), Chain("warrior"),
            "+4% melee defense. Golem's Stone Skin stacks with this for extreme DR.",
            mods: M(ClassModType.DefenseMeleeMul, 0.04f));
        Make(list, "warrior", "war2", "Brace", 2, true, None(), P(N("warrior", "war1")),
            "+8% melee power, +5% melee defense.", null, M(ClassModType.MeleePowerMul, 0.08f), M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "warrior", "war3", "War Cry", 2, false, Focus(12f), P(N("warrior", "war1")),
            "Force nearby enemies to focus you for 4s.",
            new TauntEffect { Radius = 6f, Duration = 4f }, M(ClassModType.DefenseMeleeMul, 0.04f));

        // Duelist — speed & precision. Werewolf (+10% AS, night +25% move), Skeleton (infinite stamina, +20% move).
        Make(list, "warrior", "dual1", "Quick Hands", 1, true, None(), Chain("warrior"),
            "+4% attack speed. Werewolf's +10% AS and Skeleton's infinite stamina fuel this path.",
            mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "warrior", "dual2", "Relentless", 2, true, None(), P(N("warrior", "dual1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "warrior", "dual3", "Whirlwind", 2, false, Stamina(18f), P(N("warrior", "dual1")),
            "Spin, striking all nearby foes with wind force.",
            new ClassStrikeEffect { Radius = 3f, BasePower = 40f, Type = DamageType.Wind });
    }

    // ── Mage ────────────────────────────────────────────────────────────────
    // Paths: Fire / Frost / Arcane — Draconic (Fire), Ice Giant (Frost), Wraith/Elf (Arcane)

    private static void BuildMage(List<ClassSkill> list)
    {
        Make(list, "mage", "hub", "Mage", 0, true, None(), null,
            "Elemental destruction and battlefield control.");

        // Fire — raw damage. Draconic (fire res 40%, Str+20 for fallback melee), Demonkin (fire aura).
        Make(list, "mage", "fire1", "Kindling", 1, true, None(), Chain("mage"),
            "+8% spell power. Draconic's fire resistance lets them fight in their own flames.",
            mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "fire2", "Burning Focus", 2, true, None(), P(N("mage", "fire1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "fire3", "Fireball", 2, false, Focus(15f), P(N("mage", "fire1")),
            "Launch a fireball.",
            new ClassSpellEffect { Spell = MakeSpell("mage", "Fireball", DamageType.Fire, 30f, 15f, SpellDelivery.Projectile, 4f,
                look: Look(SpellImpactStyle.Bloom, SpellCastStyle.Wave, scale: 1.12f, tempo: 1.15f)) },
            M(ClassModType.SpellPowerMul, 0.08f));

        // Frost — control. Ice Giant (cold immune, freeze aura stacks with Frost Nova).
        Make(list, "mage", "cd1", "Arcane Haste", 1, true, None(), Chain("mage"),
            "+5% cooldown reduction. Ice Giant's freeze aura + Frost Nova = double crowd control.",
            mods: M(ClassModType.CooldownMul, 0.05f));
        Make(list, "mage", "cd2", "Frost Study", 2, true, None(), P(N("mage", "cd1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "cd3", "Frost Nova", 2, false, Focus(14f), P(N("mage", "cd1")),
            "Freeze nearby enemies, slowing them harshly.",
            new CcZoneEffect { Radius = 4f, Duration = 4f, SlowFactor = 0.45f });

        // Arcane — versatility. Wraith (Immaterial = pure caster, +25 Wisdom), Elf (+8% XP, +10% AS).
        Make(list, "mage", "arc1", "Astral Mind", 1, true, None(), Chain("mage"),
            "+8% spell power. Wraith's Immaterial makes them the ultimate arcane caster.",
            mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "mage", "arc2", "Channeled", 2, true, None(), P(N("mage", "arc1")),
            "+4% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.04f));
        Make(list, "mage", "arc3", "Arcane Bolt", 2, false, Focus(12f), P(N("mage", "arc1")),
            "Hurl a bolt of raw arcane energy.",
            new ClassSpellEffect { Spell = MakeSpell("mage", "Arcane Bolt", DamageType.Arcane, 26f, 12f, SpellDelivery.Projectile, 3f,
                shape: ProjectileShape.Bolt, look: Look(SpellImpactStyle.Burst, SpellCastStyle.HexRing, sat: 1.15f)) });
    }

    // ── Rogue ───────────────────────────────────────────────────────────────
    // Paths: Shadow / Vampiric / Dagger — Vampire (Vampiric), Serpent-kin (Shadow), Harpy/Goblin (Dagger)

    private static void BuildRogue(List<ClassSkill> list)
    {
        Make(list, "rogue", "hub", "Rogue", 0, true, None(), null,
            "Stealth, backstab, and assassination.");

        // Shadow — backstab. Serpent-kin (venom blade DoT + backstab), Vampire (lifesteal + move).
        Make(list, "rogue", "sh1", "Shadow Step", 1, true, None(), Chain("rogue"),
            "+10% backstab damage. Serpent-kin's Venom Blade DoT stacks with backstab burst.",
            mods: M(ClassModType.BackstabMul, 0.10f));
        Make(list, "rogue", "sh2", "Night Fang", 2, true, None(), P(N("rogue", "sh1")),
            "+10% backstab damage.", mods: M(ClassModType.BackstabMul, 0.10f));
        Make(list, "rogue", "sh3", "Vanish", 2, false, Stamina(12f), P(N("rogue", "sh1")),
            "Become invisible to enemies for 8s.",
            new StealthEffect { Seconds = 8f });

        // Vampiric — lifesteal. Vampire (5% base lifesteal stacks), Wraith (Immaterial = safe draining).
        Make(list, "rogue", "tr1", "Vampiric Dagger", 1, false, Stamina(14f), Chain("rogue"),
            "A draining strike that restores 35% of damage dealt as HP. Vampire's 5% lifesteal stacks.",
            new LifestealStrikeEffect { Radius = 2.2f, BasePower = 24f, LifestealFraction = 0.35f });
        Make(list, "rogue", "tr2", "Poisons", 2, true, None(), P(N("rogue", "tr1")),
            "+6% consumable potency, +4% attack speed.",
            null, M(ClassModType.ConsumablePotencyMul, 0.06f), M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "rogue", "tr3", "Phantom Blade", 2, false, Stamina(16f), P(N("rogue", "tr1")),
            "Slash with shadow energy.",
            new LifestealStrikeEffect { Radius = 2.2f, BasePower = 30f, LifestealFraction = 0.4f, Type = DamageType.Dark },
            M(ClassModType.BackstabMul, 0.10f));

        // Dagger — speed & precision. Harpy (+25% Dex, +10% AS), Goblin (+20% loot, 15% smaller hitbox).
        Make(list, "rogue", "dag1", "Flurry", 1, true, None(), Chain("rogue"),
            "+4% attack speed. Harpy's +25% Dex and +10% AS make this path devastating.",
            mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "rogue", "dag2", "Killer Instinct", 2, true, None(), P(N("rogue", "dag1")),
            "+12% backstab damage.", mods: M(ClassModType.BackstabMul, 0.12f));
        Make(list, "rogue", "dag3", "Assassinate", 2, false, Stamina(18f), P(N("rogue", "dag1")),
            "A lethal dark finisher.",
            new ClassStrikeEffect { Radius = 1.8f, BasePower = 40f, Type = DamageType.Dark },
            M(ClassModType.BackstabMul, 0.05f));
    }

    // ── Cleric ──────────────────────────────────────────────────────────────
    // Paths: Light / Guardian / Restoration — Celestial/Angel (Light), Angel (Guardian), Dwarf (Restoration)

    private static void BuildCleric(List<ClassSkill> list)
    {
        Make(list, "cleric", "hub", "Cleric", 0, true, None(), null,
            "Healing miracles and divine protection.");

        // Light — healing. Celestial (healing miracles +20%), Angel (+25% Faith).
        Make(list, "cleric", "l1", "Radiant Faith", 1, true, None(), Chain("cleric"),
            "+8% healing power. Celestial's +20% miracle bonus stacks multiplicatively.",
            mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "cleric", "l2", "Guiding Light", 2, true, None(), P(N("cleric", "l1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "cleric", "l3", "Cure Wounds", 2, false, Focus(12f), P(N("cleric", "l1")),
            "Restore 60 HP (scales with healing power).", new HealEffect { Amount = 60f });

        // Guardian — defense & aura. Angel (elemental res 20%), Celestial (Faith scaling).
        Make(list, "cleric", "g1", "Hardened Soul", 1, true, None(), Chain("cleric"),
            "+5% blocking effectiveness. Angel's elemental resist makes them an unbreakable guardian.",
            mods: M(ClassModType.BlockingMul, 0.05f));
        Make(list, "cleric", "g2", "Fortitude", 2, true, None(), P(N("cleric", "g1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "cleric", "g3", "Sanctuary", 2, false, Focus(16f), P(N("cleric", "g1")),
            "A blessed aura: -8% damage taken, +0.2% max HP/s for 20s.",
            new AuraEffect { Seconds = 20f, DamageReduction = 0.08f, HpRegenPerSecond = 0.002f });

        // Restoration — HP regen. Dwarf (End+25, tanky healer), Fishmen (Health+15).
        Make(list, "cleric", "r1", "Renewal", 1, true, None(), Chain("cleric"),
            "+0.5% max HP/s regeneration. Dwarf's End+25 makes this scaling very effective.",
            mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "cleric", "r2", "Blessed", 2, true, None(), P(N("cleric", "r1")),
            "+10% healing power.", mods: M(ClassModType.HealPowerMul, 0.10f));
        Make(list, "cleric", "r3", "Restoration", 2, false, Focus(18f), P(N("cleric", "r1")),
            "A powerful restore, healing 90 HP.", new HealEffect { Amount = 90f });
    }

    // ── Berserker ───────────────────────────────────────────────────────────
    // Paths: Rage / Frenzy / Might — Orc/Demonkin (Rage), Werewolf (Frenzy), Orc/Fire Giant (Might)

    private static void BuildBerserker(List<ClassSkill> list)
    {
        Make(list, "berserker", "hub", "Berserker", 0, true, None(), null,
            "Damage increases as HP drops. Risk-for-reward combat.");

        // Rage — berserk scaling. Orc (1% HP/s regen sustains low HP), Demonkin (fire aura).
        Make(list, "berserker", "ra1", "Blood Rage", 1, true, None(), Chain("berserker"),
            "Melee power rises as your HP falls. Orc's 1% HP/s regen lets you ride the edge safely.",
            mods: M(ClassModType.BerserkScale, 0.10f));
        Make(list, "berserker", "ra2", "Thrill of Battle", 2, true, None(), P(N("berserker", "ra1")),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "berserker", "ra3", "Bloodlust", 2, false, Stamina(16f), P(N("berserker", "ra1")),
            "A reckless strike that heals 40% of damage dealt.",
            new LifestealStrikeEffect { Radius = 2.4f, BasePower = 30f, LifestealFraction = 0.4f },
            M(ClassModType.BerserkScale, 0.10f));

        // Frenzy — speed. Werewolf (bleed claws + night +25% move +10% AS), Skeleton (infinite stamina).
        Make(list, "berserker", "fr1", "Frenzied", 1, true, None(), Chain("berserker"),
            "+5% attack speed. Werewolf's night bonuses and bleed claws make this lethal.",
            mods: M(ClassModType.AttackSpeedMul, 0.05f));
        Make(list, "berserker", "fr2", "Manic", 2, true, None(), P(N("berserker", "fr1")),
            "Berserk scaling deepens.",
            mods: M(ClassModType.BerserkScale, 0.10f));
        Make(list, "berserker", "fr3", "Rampage", 2, false, Stamina(18f), P(N("berserker", "fr1")),
            "Fury incarnate — a wide brutal swing.",
            new ClassStrikeEffect { Radius = 2.6f, BasePower = 40f, Type = DamageType.Fire });

        // Might — raw power. Orc (+25% Str, +15% stagger), Fire Giant (Str+20, tanky).
        Make(list, "berserker", "mi1", "Unforgiving", 1, true, None(), Chain("berserker"),
            "+10% melee power. Orc's +25% Str and +15% stagger damage maximize this path.",
            mods: M(ClassModType.MeleePowerMul, 0.10f));
        Make(list, "berserker", "mi2", "Scarred", 2, true, None(), P(N("berserker", "mi1")),
            "+4% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.04f));
        Make(list, "berserker", "mi3", "Roar", 2, false, Stamina(12f), P(N("berserker", "mi1")),
            "A taunting bellow that draws foes to you for 3s.",
            new TauntEffect { Radius = 5f, Duration = 3f });
    }

    // ── Necromancer ─────────────────────────────────────────────────────────
    // Paths: Undead / Blood / Shadow — Wraith/Undead (Undead), Vampire (Blood), Elf/Wraith (Shadow)

    private static void BuildNecromancer(List<ClassSkill> list)
    {
        Make(list, "necromancer", "hub", "Necromancer", 0, true, None(), null,
            "Undead summoning and blood magic.");

        // Undead — summons. Wraith (Immaterial = safe behind summons), Undead (infinite stamina).
        Make(list, "necromancer", "de1", "Raise Skeleton", 1, false, Focus(16f), Chain("necromancer"),
            "Summon a skeleton ally for 30s. Wraith's Immaterial lets them summon from safety.",
            new SummonEffect { Power = 12f, Duration = 30f, Range = 5f });
        Make(list, "necromancer", "de2", "Bone Armor", 2, true, None(), P(N("necromancer", "de1")),
            "+5% melee defense (graveyard fortitude).", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "necromancer", "de3", "Raise Brute", 2, false, Focus(26f), P(N("necromancer", "de1")),
            "Summon a powerful undead brute for 40s.",
            new SummonEffect { Power = 24f, Duration = 40f, Range = 5f }, M(ClassModType.DefenseMeleeMul, 0.04f));

        // Blood — lifesteal. Vampire (5% base + class lifesteal), Demonkin (fire aura sustains).
        Make(list, "necromancer", "bl1", "Blood Siphon", 1, false, Focus(12f), Chain("necromancer"),
            "Strike with blood magic, restoring HP. Vampire's lifesteal stacks for massive sustain.",
            new LifestealStrikeEffect { Radius = 2f, BasePower = 22f, LifestealFraction = 0.3f, Type = DamageType.Dark });
        Make(list, "necromancer", "bl2", "Pallid", 2, true, None(), P(N("necromancer", "bl1")),
            "+0.5% max HP/s regeneration.", mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "necromancer", "bl3", "Blood Rite", 2, false, Focus(16f), P(N("necromancer", "bl1")),
            "A brutal rite that heals half of damage dealt.",
            new LifestealStrikeEffect { Radius = 2.2f, BasePower = 30f, LifestealFraction = 0.5f, Type = DamageType.Dark });

        // Shadow — cooldown/spells. Wraith (+25 Wisdom → spell power), Elf (+8% XP, +10% AS).
        Make(list, "necromancer", "sd1", "Grave Call", 1, true, None(), Chain("necromancer"),
            "+6% cooldown reduction. Wraith's Wisdom+25 fuels spell rotation speed.",
            mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "necromancer", "sd2", "Wither", 2, true, None(), P(N("necromancer", "sd1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "necromancer", "sd3", "Bone Spear", 2, false, Focus(14f), P(N("necromancer", "sd1")),
            "Hurl a spear of bone and shadow.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 30f, Type = DamageType.Dark });
    }

    // ── Samurai ─────────────────────────────────────────────────────────────
    // Paths: Blade / Bushido / Precision — Elf (Blade), Human (Bushido), Skeleton/Serpent-kin (Precision)

    private static void BuildSamurai(List<ClassSkill> list)
    {
        Make(list, "samurai", "hub", "Samurai", 0, true, None(), null,
            "Precision parry-and-riposte combat.");

        // Blade — parry/counter. Elf (+20% Dex, +10% AS for draw speed), Harpy (+25% Dex).
        Make(list, "samurai", "bl1", "Flow State", 1, true, None(), Chain("samurai"),
            "+10% parry window. Elf's +20% Dex scales parry timing perfectly.",
            mods: M(ClassModType.ParryWindowMul, 0.10f));
        Make(list, "samurai", "bl2", "Draw Speed", 2, true, None(), P(N("samurai", "bl1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "samurai", "bl3", "Iaijutsu", 2, false, Stamina(16f), P(N("samurai", "bl1")),
            "A lightning-fast draw cut.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 45f },
            M(ClassModType.ParryWindowMul, 0.05f));

        // Bushido — balance/heal. Human (+15% XP, balanced stats), Celestial (Faith scaling).
        Make(list, "samurai", "bu1", "Bushido Code", 1, true, None(), Chain("samurai"),
            "+10% parry window. Human's balanced stats suit this disciplined path.",
            mods: M(ClassModType.ParryWindowMul, 0.10f));
        Make(list, "samurai", "bu2", "Clear Mind", 2, true, None(), P(N("samurai", "bu1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "samurai", "bu3", "Moment of Clarity", 2, false, Focus(8f), P(N("samurai", "bu1")),
            "Breathe and center yourself, healing 50 HP.", new HealEffect { Amount = 50f });

        // Precision — backstab/counter. Skeleton (bleed immune, +20% move), Serpent-kin (Venom Blade).
        Make(list, "samurai", "pr1", "Precision", 1, true, None(), Chain("samurai"),
            "+10% backstab damage (ripostes hit harder). Skeleton's speed closes distance fast.",
            mods: M(ClassModType.BackstabMul, 0.10f));
        Make(list, "samurai", "pr2", "Keen Edge", 2, true, None(), P(N("samurai", "pr1")),
            "+12% parry window, +5% backstab.", null, M(ClassModType.ParryWindowMul, 0.12f), M(ClassModType.BackstabMul, 0.05f));
        Make(list, "samurai", "pr3", "Riposte", 2, false, Stamina(14f), P(N("samurai", "pr1")),
            "A devastating counter-cut.",
            new ClassStrikeEffect { Radius = 1.8f, BasePower = 38f },
            M(ClassModType.ParryWindowMul, 0.05f));
    }

    // ── Alchemist ───────────────────────────────────────────────────────────
    // Paths: Potion / Toxin / Forge — Gnome/Goblin (Potion), Serpent-kin (Toxin), Dwarf (Forge)

    private static void BuildAlchemist(List<ClassSkill> list)
    {
        Make(list, "alchemist", "hub", "Alchemist", 0, true, None(), null,
            "Consumable mastery and chemical warfare.");

        // Potion — consumables. Gnome (+40% loot, +35 Luck), Goblin (+20% loot).
        Make(list, "alchemist", "po1", "Distiller", 1, true, None(), Chain("alchemist"),
            "+8% consumable potency. Gnome's Lucky Find (+40% loot) floods this path with reagents.",
            mods: M(ClassModType.ConsumablePotencyMul, 0.08f));
        Make(list, "alchemist", "po2", "Master Alchemy", 2, true, None(), P(N("alchemist", "po1")),
            "+8% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.08f));
        Make(list, "alchemist", "po3", "Philosopher's Stone", 2, false, Focus(10f), P(N("alchemist", "po1")),
            "Distilled life: heal 50 HP and gain slow HP regen.",
            new HealEffect { Amount = 50f }, M(ClassModType.HpRegenPerSecond, 0.005f));

        // Toxin — CC/debuff. Serpent-kin (Venom Blade DoT stacks), Vampire (lifesteal + toxin).
        Make(list, "alchemist", "go1", "Acid Cloud", 1, false, Focus(14f), Chain("alchemist"),
            "Spawn a corrosive cloud that slows enemies. Serpent-kin's Venom Blade stacks with this.",
            new CcZoneEffect { Radius = 3.5f, Duration = 4f, SlowFactor = 0.5f });
        Make(list, "alchemist", "go2", "Caustic", 2, true, None(), P(N("alchemist", "go1")),
            "+6% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.06f));
        Make(list, "alchemist", "go3", "Sleep Dart", 2, false, Focus(12f), P(N("alchemist", "go1")),
            "A sleepy cloud that stuns enemies inside.",
            new CcZoneEffect { Radius = 2.2f, Duration = 3f, SlowFactor = 1f, Stun = true });

        // Forge — craft/repair. Dwarf (+20% yield, forge discounts, End+25).
        Make(list, "alchemist", "si1", "Tiny Forge", 1, true, None(), Chain("alchemist"),
            "+5% repair efficiency. Dwarf's forge discounts and craft yield make this path shine.",
            mods: M(ClassModType.RepairMul, 0.05f));
        Make(list, "alchemist", "si2", "Goodsmith", 2, true, None(), P(N("alchemist", "si1")),
            "+5% craft success.", mods: M(ClassModType.CraftSuccessMul, 0.05f));
        Make(list, "alchemist", "si3", "Purification", 2, true, None(), P(N("alchemist", "si1")),
            "+5% melee defense (purified body).", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
    }

    // ── Knight ──────────────────────────────────────────────────────────────
    // Paths: Iron / Wall / Crusader — Golem/Fire Giant (Iron), Orc (Wall), Draconic/Angel (Crusader)

    private static void BuildKnight(List<ClassSkill> list)
    {
        Make(list, "knight", "hub", "Knight", 0, true, None(), null,
            "Heavy armor defense and party protection.");

        // Iron — defense/equip. Golem (Stone Skin + equip load), Fire/Ice Giant (tanky).
        Make(list, "knight", "ir1", "Plate Training", 1, true, None(), Chain("knight"),
            "+4% melee defense, +10 equip load. Golem's Stone Skin stacks for extreme DR.",
            null, M(ClassModType.DefenseMeleeMul, 0.04f), M(ClassModType.EquipLoadBonus, 10f));
        Make(list, "knight", "ir2", "Bearer's Might", 2, true, None(), P(N("knight", "ir1")),
            "+5% melee defense, +10 equip load.", null, M(ClassModType.DefenseMeleeMul, 0.05f), M(ClassModType.EquipLoadBonus, 10f));
        Make(list, "knight", "ir3", "Bulwark", 2, false, Focus(16f), P(N("knight", "ir1")),
            "Fortify yourself: -10% damage taken, +HP regen for 25s.",
            new AuraEffect { Seconds = 25f, DamageReduction = 0.10f, HpRegenPerSecond = 0.002f },
            M(ClassModType.EquipLoadBonus, 10f));

        // Wall — blocking/taunt. Orc (+15% stagger dmg, +1% HP/s), Undead (infinite stamina).
        Make(list, "knight", "wa1", "Stable Guard", 1, true, None(), Chain("knight"),
            "+6% blocking effectiveness. Orc's stagger bonus makes blocks devastating.",
            mods: M(ClassModType.BlockingMul, 0.06f));
        Make(list, "knight", "wa2", "Iron Wall", 2, true, None(), P(N("knight", "wa1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "knight", "wa3", "Challenge", 2, false, Stamina(10f), P(N("knight", "wa1")),
            "Taunt nearby enemies for 5s.",
            new TauntEffect { Radius = 6f, Duration = 5f });

        // Crusader — holy strike. Draconic (Str+20, fire res), Angel (Faith+25, elemental res).
        Make(list, "knight", "ho1", "Warden", 1, true, None(), Chain("knight"),
            "+0.5% max HP/s regeneration. Angel's Faith+25 and elemental res suit this holy path.",
            mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "knight", "ho2", "Heavy Crusader", 2, true, None(), P(N("knight", "ho1")),
            "+4% melee defense, +10 equip load.", null, M(ClassModType.DefenseMeleeMul, 0.04f), M(ClassModType.EquipLoadBonus, 10f));
        Make(list, "knight", "ho3", "Crusader Strike", 2, false, Stamina(16f), P(N("knight", "ho1")),
            "A holy-imbued smash.",
            new ClassStrikeEffect { Radius = 2.2f, BasePower = 40f, Type = DamageType.Holy });
    }

    // ── Archer ──────────────────────────────────────────────────────────────
    // Paths: Marksman / Wind / Trapper — Harpy/Elf (Marksman), Elf (Wind), Goblin (Trapper)

    private static void BuildArcher(List<ClassSkill> list)
    {
        Make(list, "archer", "hub", "Archer", 0, true, None(), null,
            "Ranged precision and battlefield control.");

        // Marksman — ranged power. Harpy (+25% Dex, +10% AS, glide), Elf (+20% Dex, +8% XP).
        Make(list, "archer", "ma1", "Steady Aim", 1, true, None(), Chain("archer"),
            "+5% ranged handling. Harpy's +25% Dex and glide mobility make this deadly.",
            mods: M(ClassModType.RangedHandlingMul, 0.05f));
        Make(list, "archer", "ma2", "Hawkeye", 2, true, None(), P(N("archer", "ma1")),
            "+6% ranged handling.", mods: M(ClassModType.RangedHandlingMul, 0.06f));
        Make(list, "archer", "ma3", "Sniper Shot", 2, false, Stamina(18f), P(N("archer", "ma1")),
            "A long-range precision shot.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 50f });

        // Wind — speed/utility. Elf (+10% AS, +8% XP, +20% perception).
        Make(list, "archer", "wi1", "Quick Nock", 1, true, None(), Chain("archer"),
            "+5% attack speed. Elf's +10% AS and perception bonuses fuel rapid volleys.",
            mods: M(ClassModType.AttackSpeedMul, 0.05f));
        Make(list, "archer", "wi2", "Wind Guide", 2, true, None(), P(N("archer", "wi1")),
            "+5% ranged handling.", mods: M(ClassModType.RangedHandlingMul, 0.05f));
        Make(list, "archer", "wi3", "Wind Shot", 2, false, Focus(8f), P(N("archer", "wi1")),
            "An arrow guided by wind force.",
            new ClassSpellEffect { Spell = MakeSpell("archer", "Wind Shot", DamageType.Wind, 18f, 8f, SpellDelivery.Projectile, 4f,
                shape: ProjectileShape.Dart, look: Look(SpellImpactStyle.Cross, SpellCastStyle.Arc, tempo: 1.2f)) });

        // Trapper — CC/zone. Goblin (+20% loot, 15% smaller hitbox), Fishmen (End+15, water theme).
        Make(list, "archer", "ho1", "Concussive Arrow", 1, false, Stamina(10f), Chain("archer"),
            "A shocking arrow that slows enemies. Goblin's small hitbox lets them kite safely.",
            new CcZoneEffect { Radius = 2.2f, Duration = 3f, SlowFactor = 0.5f });
        Make(list, "archer", "ho2", "Field Craft", 2, true, None(), P(N("archer", "ho1")),
            "+5% consumable potency.", mods: M(ClassModType.ConsumablePotencyMul, 0.05f));
        Make(list, "archer", "ho3", "Volley", 2, false, Stamina(22f), P(N("archer", "ho1")),
            "Rain arrows over a wide area.",
            new ClassStrikeEffect { Radius = 3f, BasePower = 32f });
    }

    // ── Enchanter ───────────────────────────────────────────────────────────
    // Paths: Time / Frost / Charm — Elf (Time), Ice Giant (Frost), Succubus/Angel (Charm)

    private static void BuildEnchanter(List<ClassSkill> list)
    {
        Make(list, "enchanter", "hub", "Enchanter", 0, true, None(), null,
            "Area control, debuffs, and party support.");

        // Time — cooldown. Elf (+10% AS, +8% XP, +6% Wisdom), Celestial (Faith+25).
        Make(list, "enchanter", "ti1", "Bend Time", 1, true, None(), Chain("enchanter"),
            "+6% cooldown reduction. Elf's XP bonus and Wisdom fuel rapid spell rotation.",
            mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "enchanter", "ti2", "Rewind", 2, true, None(), P(N("enchanter", "ti1")),
            "+6% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "enchanter", "ti3", "Time Slow", 2, false, Focus(16f), P(N("enchanter", "ti1")),
            "Bend time in an area, slowing enemies to a crawl.",
            new CcZoneEffect { Radius = 5f, Duration = 4f, SlowFactor = 0.35f });

        // Frost — CC/zone. Ice Giant (cold immune, freeze aura stacks with Rime/Gravity Well).
        Make(list, "enchanter", "fr1", "Rime", 1, false, Focus(12f), Chain("enchanter"),
            "A frost field that slows enemies. Ice Giant's freeze aura stacks for total lockdown.",
            new CcZoneEffect { Radius = 3.5f, Duration = 3f, SlowFactor = 0.4f });
        Make(list, "enchanter", "fr2", "Bitter Cold", 2, true, None(), P(N("enchanter", "fr1")),
            "+5% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.05f));
        Make(list, "enchanter", "fr3", "Gravity Well", 2, false, Focus(18f), P(N("enchanter", "fr1")),
            "A crushing well that nearly roots and stuns.",
            new CcZoneEffect { Radius = 3f, Duration = 3f, SlowFactor = 0.25f, Stun = true });

        // Charm — aura/support. Succubus (Charm Gaze), Angel (Faith+25, elemental res).
        Make(list, "enchanter", "ch1", "Magnetic Charm", 1, true, None(), Chain("enchanter"),
            "+6% spell power. Succubus's Charm Gaze stacks with this for mass crowd control.",
            mods: M(ClassModType.SpellPowerMul, 0.06f));
        Make(list, "enchanter", "ch2", "Aura Twisting", 2, true, None(), P(N("enchanter", "ch1")),
            "+15% aura strength.", mods: M(ClassModType.AuraStrength, 0.15f));
        Make(list, "enchanter", "ch3", "Mind Shield", 2, false, Focus(14f), P(N("enchanter", "ch1")),
            "A shimmering ward: -6% damage taken for 20s.",
            new AuraEffect { Seconds = 20f, DamageReduction = 0.06f, HpRegenPerSecond = 0.001f });
    }

    // ── Brawler ─────────────────────────────────────────────────────────────
    // Paths: Fist / Grapple / Shout — Orc/Werewolf (Fist), Demonkin (Grapple), Golem (Shout)

    private static void BuildBrawler(List<ClassSkill> list)
    {
        Make(list, "brawler", "hub", "Brawler", 0, true, None(), null,
            "Unarmed combat and crowd control.");

        // Fist — raw power. Orc (+25% Str, +15% stagger), Werewolf (bleed claws + night bonuses).
        Make(list, "brawler", "fi1", "Calloused", 1, true, None(), Chain("brawler"),
            "+8% melee power. Orc's +25% Str makes fists devastating.",
            mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "brawler", "fi2", "Badass", 2, true, None(), P(N("brawler", "fi1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "brawler", "fi3", "Haymaker", 2, false, Stamina(16f), P(N("brawler", "fi1")),
            "A devastating telegraphed punch.",
            new ClassStrikeEffect { Radius = 2f, BasePower = 45f });

        // Grapple — CC/taunt. Demonkin (fire aura + Str+20), Fishmen (End+15, water theme).
        Make(list, "brawler", "gr1", "Bone Throw", 1, false, Stamina(10f), Chain("brawler"),
            "Hurl debris that stuns enemies in the zone. Demonkin's fire aura ignites the chaos.",
            new CcZoneEffect { Radius = 2f, Duration = 3f, SlowFactor = 1f, Stun = true });
        Make(list, "brawler", "gr2", "Shove", 2, true, None(), P(N("brawler", "gr1")),
            "+8% melee power.", mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "brawler", "gr3", "Bellow", 2, false, Stamina(10f), P(N("brawler", "gr1")),
            "A booming shout that draws foes in for 3s.",
            new TauntEffect { Radius = 4f, Duration = 3f });

        // Shout — stagger/defense. Golem (Stone Skin + stagger), Undead (infinite stamina).
        Make(list, "brawler", "st1", "Iron Chin", 1, true, None(), Chain("brawler"),
            "+10% stagger resistance. Golem's Stone Skin + Iron Chin = nearly unbreakable.",
            mods: M(ClassModType.StaggerResistMul, 0.10f));
        Make(list, "brawler", "st2", "Bulldozer", 2, true, None(), P(N("brawler", "st1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "brawler", "st3", "Whirlwind Fist", 2, false, Stamina(14f), P(N("brawler", "st1")),
            "Spin with fists and feet flailing.",
            new ClassStrikeEffect { Radius = 2.4f, BasePower = 35f },
            M(ClassModType.StaggerResistMul, 0.05f));
    }

    // ── Paladin ─────────────────────────────────────────────────────────────
    // Paths: Oath / Guard / Smite — Celestial/Angel (Oath), Golem (Guard), Draconic/Angel (Smite)

    private static void BuildPaladin(List<ClassSkill> list)
    {
        Make(list, "paladin", "hub", "Paladin", 0, true, None(), null,
            "Holy warrior — heal, tank, and smite.");

        // Oath — heal+def. Celestial (healing miracles +20%), Angel (+25% Faith).
        Make(list, "paladin", "oa1", "Oathkeeper", 1, true, None(), Chain("paladin"),
            "+8% healing power. Celestial's +20% miracle bonus makes healing powerful.",
            mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "paladin", "oa2", "Righteous", 2, true, None(), P(N("paladin", "oa1")),
            "+4% melee defense, +8% healing power.", null, M(ClassModType.DefenseMeleeMul, 0.04f), M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "paladin", "oa3", "Lay on Hands", 2, false, Focus(14f), P(N("paladin", "oa1")),
            "Restore 70 HP (scales with healing power).", new HealEffect { Amount = 70f });

        // Guard — blocking/taunt. Golem (Stone Skin stacks), Angel (elemental res).
        Make(list, "paladin", "gu1", "Aegis Training", 1, true, None(), Chain("paladin"),
            "+6% blocking effectiveness. Golem's Stone Skin + blocking = mobile fortress.",
            mods: M(ClassModType.BlockingMul, 0.06f));
        Make(list, "paladin", "gu2", "Holy Provocation", 2, false, Stamina(8f), P(N("paladin", "gu1")),
            "Taunts enemies for 4s.", new TauntEffect { Radius = 6f, Duration = 4f });
        Make(list, "paladin", "gu3", "Aegis", 2, false, Focus(18f), P(N("paladin", "gu1")),
            "A radiant aura: -10% damage taken, +0.3% max HP/s for 24s.",
            new AuraEffect { Seconds = 24f, DamageReduction = 0.10f, HpRegenPerSecond = 0.003f });

        // Smite — holy damage. Draconic (Str+20, fire res), Angel (Faith+25).
        Make(list, "paladin", "sm1", "Holy Fire", 1, true, None(), Chain("paladin"),
            "+8% spell power. Draconic's Str+20 + Faith+5% dual-scales this path.",
            mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "paladin", "sm2", "Divine", 2, true, None(), P(N("paladin", "sm1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "paladin", "sm3", "Smite", 2, false, Focus(16f), P(N("paladin", "sm1")),
            "Smite a foe with holy light.",
            new ClassSpellEffect { Spell = MakeSpell("paladin", "Smite", DamageType.Holy, 34f, 16f, SpellDelivery.Instant, 6f,
                look: Look(SpellImpactStyle.Pillar, SpellCastStyle.Rune, scale: 1.15f, value: 1.1f, sat: 0.8f)) });
    }

    // ── Bard ────────────────────────────────────────────────────────────────
    // Paths: Song / Dissonance / Drums — Celestial (Song), Succubus (Dissonance), Orc/Elf (Drums)

    private static void BuildBard(List<ClassSkill> list)
    {
        Make(list, "bard", "hub", "Bard", 0, true, None(), null,
            "Party-wide buffs, auras, and crowd control.");

        // Song — aura/heal. Celestial (healing miracles +20%), Angel (Faith+25).
        Make(list, "bard", "st1", "Encore", 1, true, None(), Chain("bard"),
            "+15% aura strength. Celestial's healing bonus amplifies the entire party.",
            mods: M(ClassModType.AuraStrength, 0.15f));
        Make(list, "bard", "st2", "Lullaby", 2, true, None(), P(N("bard", "st1")),
            "+8% healing power.", mods: M(ClassModType.HealPowerMul, 0.08f));
        Make(list, "bard", "st3", "Battle Hymn", 2, false, Focus(12f), P(N("bard", "st1")),
            "An inspiring song: -8% damage taken, +HP regen for 22s.",
            new AuraEffect { Seconds = 22f, DamageReduction = 0.08f, HpRegenPerSecond = 0.002f });

        // Dissonance — CC. Succubus (Charm Gaze stacks with Dissonance), Elf (Wisdom+15).
        Make(list, "bard", "wi1", "Dissonance", 1, false, Focus(14f), Chain("bard"),
            "A jarring tune that slows enemies. Succubus's Charm Gaze + Dissonance = crowd control king.",
            new CcZoneEffect { Radius = 3.5f, Duration = 4f, SlowFactor = 0.45f });
        Make(list, "bard", "wi2", "Harmony", 2, true, None(), P(N("bard", "wi1")),
            "+15% aura strength.", mods: M(ClassModType.AuraStrength, 0.15f));
        Make(list, "bard", "wi3", "Rousing Tune", 2, false, Focus(10f), P(N("bard", "wi1")),
            "Renewing song: heal 40 HP, +8% stamina regen.",
            new HealEffect { Amount = 40f }, M(ClassModType.StaminaRegenMul, 0.08f));

        // Drums — speed/stamina. Orc (+15% stagger, End+10), Werewolf (night +25% move), Elf (+10% AS).
        Make(list, "bard", "dr1", "Drums of War", 1, true, None(), Chain("bard"),
            "+8% stamina regen. Orc's End+10 and Werewolf's night speed fuel the war drums.",
            mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "bard", "dr2", "Tempo", 2, true, None(), P(N("bard", "dr1")),
            "+4% attack speed.", mods: M(ClassModType.AttackSpeedMul, 0.04f));
        Make(list, "bard", "dr3", "Taunting Jig", 2, false, Focus(12f), P(N("bard", "dr1")),
            "A defiant jig that taunts foes and steels allies.",
            new TauntEffect { Radius = 5f, Duration = 4f }, M(ClassModType.DefenseMeleeMul, 0.04f));
    }

    // ── Taoist ──────────────────────────────────────────────────────────────
    // Paths: Qi / Symbol / Flow — Undead/Elf (Qi), Celestial (Symbol), Elf/Fishmen (Flow)

    private static void BuildTaoist(List<ClassSkill> list)
    {
        Make(list, "taoist", "hub", "Taoist", 0, true, None(), null,
            "Qi manipulation — enhanced cooldowns, stamina regen, and spiritual power.");

        // Qi — stamina/cooldown. Undead (infinite stamina + Qi), Elf (+20% Dex, +8% XP).
        Make(list, "taoist", "qi1", "Qi Flow", 1, true, None(), Chain("taoist"),
            "+8% stamina regen. Undead's infinite stamina lets them channel Qi without rest.",
            mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "taoist", "qi2", "Qi Coalescence", 2, true, None(), P(N("taoist", "qi1")),
            "+6% cooldown reduction.", mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "taoist", "qi3", "Talisman", 2, false, Focus(14f), P(N("taoist", "qi1")),
            "Channel qi into a sealing talisman bolt.",
            new ClassSpellEffect { Spell = MakeSpell("taoist", "Talisman", DamageType.Arcane, 26f, 14f, SpellDelivery.Projectile, 6f,
                shape: ProjectileShape.Dart, look: Look(SpellImpactStyle.Ring, SpellCastStyle.Rune, hueShift: 0.03f, value: 0.9f)) });

        // Symbol — spell/heal. Celestial (Faith+25, healing miracles +20%), Elf (+8% XP).
        Make(list, "taoist", "sy1", "Ba Gua Symbols", 1, true, None(), Chain("taoist"),
            "+8% spell power. Celestial's Faith+25 and healing bonus amplify the symbols.",
            mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "taoist", "sy2", "Meditative", 2, true, None(), P(N("taoist", "sy1")),
            "+8% stamina regen, +0.3% max HP/s.", null, M(ClassModType.StaminaRegenMul, 0.08f), M(ClassModType.HpRegenPerSecond, 0.003f));
        Make(list, "taoist", "sy3", "Yi Symbol", 2, false, Focus(14f), P(N("taoist", "sy1")),
            "A yin-yang ward: -6% damage taken, +HP regen for 20s.",
            new AuraEffect { Seconds = 20f, DamageReduction = 0.06f, HpRegenPerSecond = 0.003f });

        // Flow — CC/zone. Elf (+10% AS, +8% XP), Fishmen (water theme, swim speed).
        Make(list, "taoist", "fo1", "Flowing Form", 1, true, None(), Chain("taoist"),
            "+6% cooldown reduction. Elf's versatility and Fishmen's water affinity suit this path.",
            mods: M(ClassModType.CooldownMul, 0.06f));
        Make(list, "taoist", "fo2", "Water & Wood", 2, true, None(), P(N("taoist", "fo1")),
            "+8% spell power.", mods: M(ClassModType.SpellPowerMul, 0.08f));
        Make(list, "taoist", "fo3", "Flowing Water", 2, false, Focus(12f), P(N("taoist", "fo1")),
            "Qi slows all who enter: a flowing-water field.",
            new CcZoneEffect { Radius = 4f, Duration = 4f, SlowFactor = 0.4f });
    }

    // ── Monk ────────────────────────────────────────────────────────────────
    // Paths: Body / Mind / Fist — Golem (Body), Celestial (Mind), Orc/Werewolf (Fist)

    private static void BuildMonk(List<ClassSkill> list)
    {
        Make(list, "monk", "hub", "Monk", 0, true, None(), null,
            "Inner peace — meditation heals HP; reduced stagger, defense while unarmed.");

        // Body — defense/block. Golem (Stone Skin stacks with Iron Flesh), Dwarf (End+25).
        Make(list, "monk", "bo1", "Discipline", 1, true, None(), Chain("monk"),
            "+8% stamina regen. Golem's Stone Skin + Discipline = nearly unbreakable body.",
            mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "monk", "bo2", "Iron Flesh", 2, true, None(), P(N("monk", "bo1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "monk", "bo3", "Iron Body", 2, true, None(), P(N("monk", "bo1")),
            "+6% blocking effectiveness (unyielding body).", mods: M(ClassModType.BlockingMul, 0.06f));

        // Mind — heal/stamina. Celestial (healing miracles +20%), Elf (+8% XP, Wisdom+15).
        Make(list, "monk", "mi1", "Zen", 1, true, None(), Chain("monk"),
            "+0.5% max HP/s regeneration. Celestial's healing bonus amplifies meditation.",
            mods: M(ClassModType.HpRegenPerSecond, 0.005f));
        Make(list, "monk", "mi2", "Calm Breath", 2, true, None(), P(N("monk", "mi1")),
            "+8% stamina regen.", mods: M(ClassModType.StaminaRegenMul, 0.08f));
        Make(list, "monk", "mi3", "Meditation", 2, false, Focus(6f), P(N("monk", "mi1")),
            "Sit in stillness and heal 60 HP.", new HealEffect { Amount = 60f });

        // Fist — melee/stagger. Orc (+25% Str, +15% stagger), Werewolf (bleed claws, +20% move).
        Make(list, "monk", "fi1", "Centered", 1, true, None(), Chain("monk"),
            "+8% melee power. Orc's +25% Str makes monk strikes devastating.",
            mods: M(ClassModType.MeleePowerMul, 0.08f));
        Make(list, "monk", "fi2", "Unyielding", 2, true, None(), P(N("monk", "fi1")),
            "+10% stagger resistance.", mods: M(ClassModType.StaggerResistMul, 0.10f));
        Make(list, "monk", "fi3", "Chi Wave", 2, false, Stamina(14f), P(N("monk", "fi1")),
            "Release a wave of chi that strikes and slows.",
            new ClassStrikeEffect { Radius = 2.2f, BasePower = 38f, Type = DamageType.Wind },
            M(ClassModType.StaggerResistMul, 0.05f));
    }

    // ── Blacksmith ──────────────────────────────────────────────────────────
    // Paths: Forge / Anvil / Ember — Dwarf (Forge), Golem (Anvil), Draconic/Fire Giant (Ember)

    private static void BuildBlacksmith(List<ClassSkill> list)
    {
        Make(list, "blacksmith", "hub", "Blacksmith", 0, true, None(), null,
            "Crafting mastery and forged resilience.");

        // Forge — craft success. Dwarf (+20% yield, forge discounts), Goblin (+20% loot).
        Make(list, "blacksmith", "fo1", "Apprentice Forge", 1, true, None(), Chain("blacksmith"),
            "+6% craft success. Dwarf's +20% yield and forge discounts make this path essential.",
            mods: M(ClassModType.CraftSuccessMul, 0.06f));
        Make(list, "blacksmith", "fo2", "Kept Coal", 2, true, None(), P(N("blacksmith", "fo1")),
            "+8% craft success.", mods: M(ClassModType.CraftSuccessMul, 0.08f));
        Make(list, "blacksmith", "fo3", "Masterwork", 2, true, None(), P(N("blacksmith", "fo1")),
            "+8% craft success, +6% repair.", null, M(ClassModType.CraftSuccessMul, 0.08f), M(ClassModType.RepairMul, 0.06f));

        // Anvil — repair/defense. Golem (Stone Skin stacks with Tempered Steel), Orc (End+10).
        Make(list, "blacksmith", "an1", "Field Repair", 1, true, None(), Chain("blacksmith"),
            "+5% repair efficiency. Golem's Stone Skin + Tempered Steel = mobile forge-fortress.",
            mods: M(ClassModType.RepairMul, 0.05f));
        Make(list, "blacksmith", "an2", "Sturdy Fixes", 2, true, None(), P(N("blacksmith", "an1")),
            "+6% repair efficiency.", mods: M(ClassModType.RepairMul, 0.06f));
        Make(list, "blacksmith", "an3", "Tempered Steel", 2, true, None(), P(N("blacksmith", "an1")),
            "+5% melee defense (forged-hardened).", mods: M(ClassModType.DefenseMeleeMul, 0.05f));

        // Ember — craft/block. Draconic (fire res 40%, Str+20), Fire Giant (lava walk, fire res 50%).
        Make(list, "blacksmith", "em1", "Ember Study", 1, true, None(), Chain("blacksmith"),
            "+5% craft success. Draconic's fire resistance lets them work the hottest forges.",
            mods: M(ClassModType.CraftSuccessMul, 0.05f));
        Make(list, "blacksmith", "em2", "Quenching", 2, true, None(), P(N("blacksmith", "em1")),
            "+5% melee defense.", mods: M(ClassModType.DefenseMeleeMul, 0.05f));
        Make(list, "blacksmith", "em3", "Forging Spirit", 2, true, None(), P(N("blacksmith", "em1")),
            "+6% blocking effectiveness, +10% stagger resistance.",
            null, M(ClassModType.BlockingMul, 0.06f), M(ClassModType.StaggerResistMul, 0.10f));
    }
}
