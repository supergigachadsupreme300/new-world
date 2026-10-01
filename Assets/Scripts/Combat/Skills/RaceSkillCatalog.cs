using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of race skills. Each of the 22 races owns a radial tree (hub + 3 paths)
/// that focuses purely on enhancing racial identity — stat bonuses, passive amplifiers, and
/// active racial abilities. Built in code (no .asset files), mirroring
/// <see cref="ClassSkillCatalog"/>. Skills are auto-granted for testing; passives apply while
/// the race is active (aggregated by <see cref="RaceSkillPassiveManager"/>).
/// </summary>
public static class RaceSkillCatalog
{
    public static List<RaceSkill> All { get; private set; }

    private static bool _built;
    private static Dictionary<string, RaceSkill> _cache;
    private static Dictionary<string, List<RaceSkill>> _byRace;

    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
        _cache = new Dictionary<string, RaceSkill>(All.Count);
        _byRace = new Dictionary<string, List<RaceSkill>>();
        foreach (var s in All)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            _cache[s.id] = s;

            string raceId = ExtractRaceId(s.id);
            if (!_byRace.TryGetValue(raceId, out var list))
            {
                list = new List<RaceSkill>();
                _byRace[raceId] = list;
            }
            list.Add(s);
        }
    }

    public static RaceSkill Find(string id)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(id)) return null;
        _cache.TryGetValue(id, out var skill);
        return skill;
    }

    public static IEnumerable<RaceSkill> ForRace(string raceId)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(raceId)) yield break;
        if (_byRace != null && _byRace.TryGetValue(raceId, out var list))
            for (int i = 0; i < list.Count; i++)
                yield return list[i];
    }

    public static bool IsRaceSkill(string skillId) => Find(skillId) != null;

    private static string ExtractRaceId(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        int dot = id.IndexOf('.');
        if (dot < 0) return string.Empty;
        string body = id.Substring(0, dot);
        return body.StartsWith("rac.") ? body.Substring("rac.".Length) : body;
    }

    // ── Build all 22 race trees ─────────────────────────────────────────────

    private static List<RaceSkill> BuildDefault()
    {
        var list = new List<RaceSkill>();
        BuildHuman(list);
        BuildFireGiant(list);
        BuildSerpentKin(list);
        BuildDraconic(list);
        BuildGolem(list);
        BuildCelestial(list);
        BuildWraith(list);
        BuildUndead(list);
        BuildSkeleton(list);
        BuildWerewolf(list);
        BuildGoblin(list);
        BuildOrc(list);
        BuildIceGiant(list);
        BuildVampire(list);
        BuildDemonkin(list);
        BuildAngel(list);
        BuildSuccubus(list);
        BuildFishmen(list);
        BuildHarpy(list);
        BuildDwarf(list);
        BuildGnome(list);
        BuildElf(list);
        return list;
    }

    // ── Authoring helpers ───────────────────────────────────────────────────

    private const string NP = "rac.";

    private static RaceSkill Make(List<RaceSkill> list, string raceId, string node, string name,
        int layer, bool passive, Cost cost, string[] prereqs, string desc,
        IRaceEffect effect = null, params RaceMod[] mods)
    {
        var s = ScriptableObject.CreateInstance<RaceSkill>();
        string id = NP + raceId + "." + node;
        s.name = id;
        s.id = id;
        s.displayName = name;
        s.Layer = layer;
        s.IsPassive = passive;
        s.SkillCost = cost;
        s.PrereqSkillIds = prereqs ?? new string[0];
        s.description = desc;
        s.Effects = effect != null ? new[] { effect } : new IRaceEffect[0];
        s.Mods = mods ?? new RaceMod[0];
        list.Add(s);
        return s;
    }

    private static Cost Focus(float amount) => new Cost { Resource = ResourceKind.Focus, Amount = amount, CastTime = 0.4f, Cooldown = 2f };
    private static Cost Stamina(float amount) => new Cost { Resource = ResourceKind.Stamina, Amount = amount, Cooldown = 1.2f };
    private static Cost None() => default;
    private static string[] P(params string[] ids) => ids;
    private static string HubId(string raceId) => NP + raceId + ".hub";
    private static string N(string raceId, string node) => NP + raceId + "." + node;
    private static string[] Chain(string raceId) => new[] { HubId(raceId) };

    private static RaceMod M(RaceModType kind, float amount) => new RaceMod { kind = kind, amount = amount };

    private static SpellData MakeSpell(string raceId, string name, DamageType type, float power,
        float fp, SpellDelivery delivery, float cooldown, float range = 10f, float radius = 1.5f,
        ProjectileShape shape = ProjectileShape.Auto, SpellLookProfile look = null)
    {
        var sd = ScriptableObject.CreateInstance<SpellData>();
        sd.name = "rac_" + raceId + "_" + name.ToLower().Replace(" ", "_");
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
        // 1ii: kept in lockstep with ClassSkillCatalog.MakeSpell, which has the identical body and
        // the identical live signature. This twin has zero call sites today, so nothing would have
        // complained if the two drifted — a rule-12 second spelling, pre-existing, fixed here.
        sd.Look = look;
        return sd;
    }

    // ── Human ───────────────────────────────────────────────────────────────
    // Balanced + XP focused. The baseline race.

    private static void BuildHuman(List<RaceSkill> list)
    {
        Make(list, "human", "hub", "Human Heritage", 0, true, None(), null,
            "Adaptable and determined — the human spirit excels at everything.");

        // Adaptability — balanced stats + XP.
        Make(list, "human", "ad1", "Quick Learner", 1, true, None(), Chain("human"),
            "+5% XP bonus from all sources.",
            mods: M(RaceModType.XpBonusMul, 0.05f));
        Make(list, "human", "ad2", "Versatile", 2, true, None(), P(N("human", "ad1")),
            "+3% to all core stats (Health, Strength, Dexterity, Endurance).",
            null, M(RaceModType.HealthBonus, 3f), M(RaceModType.StrengthBonus, 3f),
            M(RaceModType.DexterityBonus, 3f), M(RaceModType.EnduranceBonus, 3f));
        Make(list, "human", "ad3", "Inspire", 2, false, Focus(10f), P(N("human", "ad1")),
            "Rally nearby allies: +10% attack speed and +5% damage for 15s.",
            new RaceAuraEffect { Seconds = 15f, DamageReduction = 0f, HpRegenPerSecond = 0.003f });

        // Resilience — defense + sustain.
        Make(list, "human", "re1", "Iron Will", 1, true, None(), Chain("human"),
            "+5% stagger resistance.",
            mods: M(RaceModType.StaggerResistMul, 0.05f));
        Make(list, "human", "re2", "Second Wind", 2, true, None(), P(N("human", "re1")),
            "+0.3% max HP/s regeneration.",
            mods: M(RaceModType.HpRegenPerSecond, 0.003f));
        Make(list, "human", "re3", "Endure", 2, false, Stamina(12f), P(N("human", "re1")),
            "Gain a temporary shield: -10% damage taken for 10s.",
            new RaceAuraEffect { Seconds = 10f, DamageReduction = 0.10f, HpRegenPerSecond = 0f });

        // Determination — offense.
        Make(list, "human", "de1", "Human Spirit", 1, true, None(), Chain("human"),
            "+5% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.05f));
        Make(list, "human", "de2", "Focus", 2, true, None(), P(N("human", "de1")),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "human", "de3", "Rallying Cry", 2, false, Stamina(10f), P(N("human", "de1")),
            "Taunt nearby enemies for 4s.",
            new RaceTauntEffect { Radius = 6f, Duration = 4f });
    }

    // ── Fire Giant ──────────────────────────────────────────────────────────
    // Fire resistant, tanky, fire damage.

    private static void BuildFireGiant(List<RaceSkill> list)
    {
        Make(list, "fire_giant", "hub", "Fire Giant Blood", 0, true, None(), null,
            "Born of volcanoes — fire heals, steel breaks against your hide.");

        // Molten Core — fire resistance + HP.
        Make(list, "fire_giant", "mc1", "Molten Core", 1, true, None(), Chain("fire_giant"),
            "+5% health bonus. The fires within burn strong.",
            mods: M(RaceModType.HealthBonus, 5f));
        Make(list, "fire_giant", "mc2", "Lava Blood", 2, true, None(), P(N("fire_giant", "mc1")),
            "+0.5% max HP/s regeneration. Your blood runs hot.",
            mods: M(RaceModType.HpRegenPerSecond, 0.005f));
        Make(list, "fire_giant", "mc3", "Eruption", 2, false, Stamina(18f), P(N("fire_giant", "mc1")),
            "Erupt molten rock around you, dealing fire damage.",
            new RaceStrikeEffect { Radius = 3f, BasePower = 40f, Type = DamageType.Fire });

        // Lava Walk — defense + speed.
        Make(list, "fire_giant", "lw1", "Tectonic", 1, true, None(), Chain("fire_giant"),
            "+5% melee defense.",
            mods: M(RaceModType.DefenseBonus, 5f));
        Make(list, "fire_giant", "lw2", "Unyielding", 2, true, None(), P(N("fire_giant", "lw1")),
            "+10% stagger resistance.",
            mods: M(RaceModType.StaggerResistMul, 0.10f));
        Make(list, "fire_giant", "lw3", "Seismic Slam", 2, false, Stamina(20f), P(N("fire_giant", "lw1")),
            "Slam the ground, staggering all nearby foes.",
            new RaceRoarEffect { Radius = 4f, Duration = 2f, StaggerAmount = 25f, Type = DamageType.Fire });

        // Inferno — fire damage.
        Make(list, "fire_giant", "if1", "Flame Aura", 1, true, None(), Chain("fire_giant"),
            "+5% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.05f));
        Make(list, "fire_giant", "if2", "Heat Wave", 2, true, None(), P(N("fire_giant", "if1")),
            "+5% strength bonus.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "fire_giant", "if3", "Fire Breath", 2, false, Focus(16f), P(N("fire_giant", "if1")),
            "Breathe fire in a cone, dealing heavy fire damage.",
            new RaceStrikeEffect { Radius = 4f, BasePower = 50f, Type = DamageType.Fire });
    }

    // ── Serpent-kin ─────────────────────────────────────────────────────────
    // Venom, dexterity, poison.

    private static void BuildSerpentKin(List<RaceSkill> list)
    {
        Make(list, "serpent_kin", "hub", "Serpent Blood", 0, true, None(), null,
            "Venom courses through your veins — every strike poisons.");

        // Venomous — venom damage + Dex.
        Make(list, "serpent_kin", "ve1", "Venom Glands", 1, true, None(), Chain("serpent_kin"),
            "+5% dexterity bonus.",
            mods: M(RaceModType.DexterityBonus, 5f));
        Make(list, "serpent_kin", "ve2", "Toxic Blood", 2, true, None(), P(N("serpent_kin", "ve1")),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "serpent_kin", "ve3", "Venom Strike", 2, false, Stamina(14f), P(N("serpent_kin", "ve1")),
            "A venomous strike that deals damage over time.",
            new RaceStrikeEffect { Radius = 2f, BasePower = 28f, Type = DamageType.Dark });

        // Slither — speed + dodge.
        Make(list, "serpent_kin", "sl1", "Slither", 1, true, None(), Chain("serpent_kin"),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "serpent_kin", "sl2", "Evasive", 2, true, None(), P(N("serpent_kin", "sl1")),
            "+5% parry window.",
            mods: M(RaceModType.ParryWindowMul, 0.05f));
        Make(list, "serpent_kin", "sl3", "Constrict", 2, false, Stamina(12f), P(N("serpent_kin", "sl1")),
            "Entangle and slow a foe for 5s.",
            new RaceCcZoneEffect { Radius = 3f, Duration = 5f, SlowFactor = 0.4f });

        // Constrict — CC.
        Make(list, "serpent_kin", "co1", "Hissing", 1, true, None(), Chain("serpent_kin"),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));
        Make(list, "serpent_kin", "co2", "Fangs", 2, true, None(), P(N("serpent_kin", "co1")),
            "+5% lifesteal on hit.",
            mods: M(RaceModType.LifestealBonus, 0.05f));
        Make(list, "serpent_kin", "co3", "Poison Cloud", 2, false, Focus(14f), P(N("serpent_kin", "co1")),
            "Release a cloud of toxic gas.",
            new RaceCcZoneEffect { Radius = 4f, Duration = 4f, SlowFactor = 0.5f });
    }

    // ── Draconic ────────────────────────────────────────────────────────────
    // Fire resistant, strong, dragon abilities.

    private static void BuildDraconic(List<RaceSkill> list)
    {
        Make(list, "draconic", "hub", "Dragon Blood", 0, true, None(), null,
            "Ancient draconic power — fire resistance and devastating breath.");

        // Dragon Scales — defense + fire res.
        Make(list, "draconic", "ds1", "Dragon Scales", 1, true, None(), Chain("draconic"),
            "+5% defense bonus. Scales harden against blows.",
            mods: M(RaceModType.DefenseBonus, 5f));
        Make(list, "draconic", "ds2", "Scale Mail", 2, true, None(), P(N("draconic", "ds1")),
            "+10% stagger resistance.",
            mods: M(RaceModType.StaggerResistMul, 0.10f));
        Make(list, "draconic", "ds3", "Dragon Hide", 2, true, None(), P(N("draconic", "ds1")),
            "+5% health bonus.",
            mods: M(RaceModType.HealthBonus, 5f));

        // Dragon Strength — offense.
        Make(list, "draconic", "dg1", "Dragon Strength", 1, true, None(), Chain("draconic"),
            "+5% strength bonus. Raw draconic power.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "draconic", "dg2", "Wyrm's Might", 2, true, None(), P(N("draconic", "dg1")),
            "+8% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.08f));
        Make(list, "draconic", "dg3", "Dragon Claw", 2, false, Stamina(18f), P(N("draconic", "dg1")),
            "Rake with draconic claws.",
            new RaceStrikeEffect { Radius = 2.5f, BasePower = 45f, Type = DamageType.Fire });

        // Dragon Breath — fire damage.
        Make(list, "draconic", "db1", "Inner Flame", 1, true, None(), Chain("draconic"),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "draconic", "db2", "Heat Aura", 2, true, None(), P(N("draconic", "db1")),
            "+0.3% max HP/s regeneration.",
            mods: M(RaceModType.HpRegenPerSecond, 0.003f));
        Make(list, "draconic", "db3", "Dragon Roar", 2, false, Focus(14f), P(N("draconic", "db1")),
            "Unleash a terrifying roar — stagger and damage all nearby foes.",
            new RaceRoarEffect { Radius = 6f, Duration = 3f, StaggerAmount = 20f, Type = DamageType.Fire });
    }

    // ── Golem ───────────────────────────────────────────────────────────────
    // Stone skin, slow, tanky.

    private static void BuildGolem(List<RaceSkill> list)
    {
        Make(list, "golem", "hub", "Stone Form", 0, true, None(), null,
            "Living stone — nearly unbreakable, but slow.");

        // Stone Skin — defense.
        Make(list, "golem", "ss1", "Thick Stone", 1, true, None(), Chain("golem"),
            "+8% defense bonus. Stone hardens.",
            mods: M(RaceModType.DefenseBonus, 8f));
        Make(list, "golem", "ss2", "Granite", 2, true, None(), P(N("golem", "ss1")),
            "+10% stagger resistance.",
            mods: M(RaceModType.StaggerResistMul, 0.10f));
        Make(list, "golem", "ss3", "Stone Slam", 2, false, Stamina(22f), P(N("golem", "ss1")),
            "Slam the ground with stone fists.",
            new RaceStrikeEffect { Radius = 3f, BasePower = 50f });

        // Iron Body — HP + sustain.
        Make(list, "golem", "ib1", "Iron Body", 1, true, None(), Chain("golem"),
            "+8% health bonus. Massive stone form.",
            mods: M(RaceModType.HealthBonus, 8f));
        Make(list, "golem", "ib2", "Regeneration", 2, true, None(), P(N("golem", "ib1")),
            "+0.5% max HP/s regeneration.",
            mods: M(RaceModType.HpRegenPerSecond, 0.005f));
        Make(list, "golem", "ib3", "Fortify", 2, false, Focus(12f), P(N("golem", "ib1")),
            "Harden your form: -15% damage taken for 12s.",
            new RaceAuraEffect { Seconds = 12f, DamageReduction = 0.15f, HpRegenPerSecond = 0f });

        // Earthquake — CC.
        Make(list, "golem", "eq1", "Tremor", 1, true, None(), Chain("golem"),
            "+5% melee power. Raw stone force.",
            mods: M(RaceModType.MeleePowerMul, 0.05f));
        Make(list, "golem", "eq2", "Seismic", 2, true, None(), P(N("golem", "eq1")),
            "+5% blocking effectiveness.",
            mods: M(RaceModType.BlockingMul, 0.05f));
        Make(list, "golem", "eq3", "Earthquake", 2, false, Focus(18f), P(N("golem", "eq1")),
            "Shake the earth, stunning nearby foes.",
            new RaceCcZoneEffect { Radius = 5f, Duration = 3f, SlowFactor = 1f, Stun = true });
    }

    // ── Celestial ───────────────────────────────────────────────────────────
    // Healing, faith, holy.

    private static void BuildCelestial(List<RaceSkill> list)
    {
        Make(list, "celestial", "hub", "Celestial Grace", 0, true, None(), null,
            "Touched by the divine — healing miracles and holy light.");

        // Divine Light — heal + faith.
        Make(list, "celestial", "dl1", "Divine Light", 1, true, None(), Chain("celestial"),
            "+5% faith bonus.",
            mods: M(RaceModType.FaithBonus, 5f));
        Make(list, "celestial", "dl2", "Healing Touch", 2, true, None(), P(N("celestial", "dl1")),
            "+8% healing power.",
            mods: M(RaceModType.HealPowerMul, 0.08f));
        Make(list, "celestial", "dl3", "Restoration", 2, false, Focus(14f), P(N("celestial", "dl1")),
            "Heal 60 HP (scales with healing power).",
            new RaceHealEffect { Amount = 60f });

        // Celestial Grace — speed + dodge.
        Make(list, "celestial", "cg1", "Graceful", 1, true, None(), Chain("celestial"),
            "+5% movement speed. Divine swiftness.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "celestial", "cg2", "Evasion", 2, true, None(), P(N("celestial", "cg1")),
            "+5% parry window.",
            mods: M(RaceModType.ParryWindowMul, 0.05f));
        Make(list, "celestial", "cg3", "Radiant Burst", 2, false, Focus(16f), P(N("celestial", "cg1")),
            "Emit a burst of holy light, healing allies and damaging foes.",
            new RaceStrikeEffect { Radius = 4f, BasePower = 35f, Type = DamageType.Holy });

        // Holy Smite — offense.
        Make(list, "celestial", "hs1", "Holy Wrath", 1, true, None(), Chain("celestial"),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "celestial", "hs2", "Sanctified", 2, true, None(), P(N("celestial", "hs1")),
            "+5% health bonus.",
            mods: M(RaceModType.HealthBonus, 5f));
        Make(list, "celestial", "hs3", "Holy Smite", 2, false, Focus(18f), P(N("celestial", "hs1")),
            "Smite a foe with divine power.",
            new RaceStrikeEffect { Radius = 2.5f, BasePower = 45f, Type = DamageType.Holy });
    }

    // ── Wraith ──────────────────────────────────────────────────────────────
    // Immaterial, magic, wisdom.

    private static void BuildWraith(List<RaceSkill> list)
    {
        Make(list, "wraith", "hub", "Spirit Form", 0, true, None(), null,
            "Between worlds — ethereal power and shadow magic.");

        // Ethereal — spell power + wisdom.
        Make(list, "wraith", "et1", "Ethereal", 1, true, None(), Chain("wraith"),
            "+5% wisdom bonus.",
            mods: M(RaceModType.WisdomBonus, 5f));
        Make(list, "wraith", "et2", "Spiritual", 2, true, None(), P(N("wraith", "et1")),
            "+8% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.08f));
        Make(list, "wraith", "et3", "Soul Drain", 2, false, Focus(14f), P(N("wraith", "et1")),
            "Drain life force from a foe, restoring HP.",
            new RaceLifestealStrikeEffect { Radius = 2f, BasePower = 28f, LifestealFraction = 0.4f, Type = DamageType.Dark });

        // Shadow Step — speed + stealth.
        Make(list, "wraith", "ss1", "Shadow Step", 1, true, None(), Chain("wraith"),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "wraith", "ss2", "Fade", 2, true, None(), P(N("wraith", "ss1")),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));
        Make(list, "wraith", "ss3", "Phase Shift", 2, false, Stamina(12f), P(N("wraith", "ss1")),
            "Become intangible for 6s — pass through enemies.",
            new RaceStealthEffect { Seconds = 6f });

        // Shadow — CC.
        Make(list, "wraith", "sh1", "Dark Aura", 1, true, None(), Chain("wraith"),
            "+5% cooldown reduction.",
            mods: M(RaceModType.CooldownMul, 0.05f));
        Make(list, "wraith", "sh2", "Wither", 2, true, None(), P(N("wraith", "sh1")),
            "+5% intelligence bonus.",
            mods: M(RaceModType.IntelligenceBonus, 5f));
        Make(list, "wraith", "sh3", "Dark Nova", 2, false, Focus(16f), P(N("wraith", "sh1")),
            "Release a nova of dark energy.",
            new RaceStrikeEffect { Radius = 3.5f, BasePower = 35f, Type = DamageType.Dark });
    }

    // ── Undead ──────────────────────────────────────────────────────────────
    // Infinite stamina, fire/holy vulnerable.

    private static void BuildUndead(List<RaceSkill> list)
    {
        Make(list, "undead", "hub", "Undying Will", 0, true, None(), null,
            "Death cannot hold you — infinite stamina and relentless endurance.");

        // Unending — stamina.
        Make(list, "undead", "un1", "Unending", 1, true, None(), Chain("undead"),
            "+8% stamina regen. Death-fueled endurance.",
            mods: M(RaceModType.StaminaRegenMul, 0.08f));
        Make(list, "undead", "un2", "Endless", 2, true, None(), P(N("undead", "un1")),
            "+5% endurance bonus.",
            mods: M(RaceModType.EnduranceBonus, 5f));
        Make(list, "undead", "un3", "Death's Embrace", 2, false, Stamina(16f), P(N("undead", "un1")),
            "Unleash necrotic energy, damaging and slowing foes.",
            new RaceCcZoneEffect { Radius = 3.5f, Duration = 4f, SlowFactor = 0.5f });

        // Death's Grip — offense.
        Make(list, "undead", "dg1", "Death's Grip", 1, true, None(), Chain("undead"),
            "+5% melee power. Undead strength.",
            mods: M(RaceModType.MeleePowerMul, 0.05f));
        Make(list, "undead", "dg2", "Grasp", 2, true, None(), P(N("undead", "dg1")),
            "+5% strength bonus.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "undead", "dg3", "Life Tap", 2, false, Focus(12f), P(N("undead", "dg1")),
            "Steal life from a foe.",
            new RaceLifestealStrikeEffect { Radius = 2f, BasePower = 25f, LifestealFraction = 0.35f, Type = DamageType.Dark });

        // Undead Resilience — sustain.
        Make(list, "undead", "ur1", "Deathless", 1, true, None(), Chain("undead"),
            "+0.3% max HP/s regeneration.",
            mods: M(RaceModType.HpRegenPerSecond, 0.003f));
        Make(list, "undead", "ur2", "Hardened", 2, true, None(), P(N("undead", "ur1")),
            "+5% health bonus.",
            mods: M(RaceModType.HealthBonus, 5f));
        Make(list, "undead", "ur3", "Undying", 2, true, None(), P(N("undead", "ur1")),
            "+5% stagger resistance.",
            mods: M(RaceModType.StaggerResistMul, 0.05f));
    }

    // ── Skeleton ────────────────────────────────────────────────────────────
    // Fast, infinite stamina, bleed immune.

    private static void BuildSkeleton(List<RaceSkill> list)
    {
        Make(list, "skeleton", "hub", "Bone Frame", 0, true, None(), null,
            "Light and swift — nothing weighs you down.");

        // Bone White — speed + Dex.
        Make(list, "skeleton", "bw1", "Swift Bones", 1, true, None(), Chain("skeleton"),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "skeleton", "bw2", "Nimble", 2, true, None(), P(N("skeleton", "bw1")),
            "+5% dexterity bonus.",
            mods: M(RaceModType.DexterityBonus, 5f));
        Make(list, "skeleton", "bw3", "Bone Storm", 2, false, Stamina(14f), P(N("skeleton", "bw1")),
            "Whirl in a storm of bones, striking all nearby.",
            new RaceStrikeEffect { Radius = 2.5f, BasePower = 35f });

        // Brittle Strength — offense.
        Make(list, "skeleton", "bs1", "Brittle Strength", 1, true, None(), Chain("skeleton"),
            "+5% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.05f));
        Make(list, "skeleton", "bs2", "Sharp", 2, true, None(), P(N("skeleton", "bs1")),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));
        Make(list, "skeleton", "bs3", "Skeletal Rush", 2, false, Stamina(12f), P(N("skeleton", "bs1")),
            "Dash forward with bone speed, striking foes.",
            new RaceStrikeEffect { Radius = 2f, BasePower = 30f });

        // Skeletal Rush — utility.
        Make(list, "skeleton", "sr1", "Light Frame", 1, true, None(), Chain("skeleton"),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "skeleton", "sr2", "Hollow", 2, true, None(), P(N("skeleton", "sr1")),
            "+5% parry window.",
            mods: M(RaceModType.ParryWindowMul, 0.05f));
        Make(list, "skeleton", "sr3", "Rattle", 2, true, None(), P(N("skeleton", "sr1")),
            "+5% cooldown reduction.",
            mods: M(RaceModType.CooldownMul, 0.05f));
    }

    // ── Werewolf ────────────────────────────────────────────────────────────
    // Night bonuses, bleed, fast.

    private static void BuildWerewolf(List<RaceSkill> list)
    {
        Make(list, "werewolf", "hub", "Lunar Blood", 0, true, None(), null,
            "The moon calls — primal fury and savage speed.");

        // Lunar Strength — offense.
        Make(list, "werewolf", "ls1", "Lunar Strength", 1, true, None(), Chain("werewolf"),
            "+5% strength bonus. Primal power.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "werewolf", "ls2", "Feral", 2, true, None(), P(N("werewolf", "ls1")),
            "+8% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.08f));
        Make(list, "werewolf", "ls3", "Feral Transformation", 2, false, Stamina(20f), P(N("werewolf", "ls1")),
            "Transform: +10 Strength, +5% move speed for 15s.",
            new RaceStatBuffEffect { Seconds = 15f, Stat = StatType.Strength, Amount = 10f, Radius = 4f });

        // Pack Instinct — speed + AS.
        Make(list, "werewolf", "pi1", "Pack Instinct", 1, true, None(), Chain("werewolf"),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "werewolf", "pi2", "Hunt", 2, true, None(), P(N("werewolf", "pi1")),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "werewolf", "pi3", "Predator", 2, true, None(), P(N("werewolf", "pi1")),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));

        // Primal Fury — lifesteal.
        Make(list, "werewolf", "pf1", "Blood Scent", 1, true, None(), Chain("werewolf"),
            "+3% lifesteal on hit.",
            mods: M(RaceModType.LifestealBonus, 0.03f));
        Make(list, "werewolf", "pf2", "Devour", 2, true, None(), P(N("werewolf", "pf1")),
            "+0.5% max HP/s regeneration.",
            mods: M(RaceModType.HpRegenPerSecond, 0.005f));
        Make(list, "werewolf", "pf3", "Claw Swipe", 2, false, Stamina(16f), P(N("werewolf", "pf1")),
            "Rake with savage claws.",
            new RaceLifestealStrikeEffect { Radius = 2.5f, BasePower = 32f, LifestealFraction = 0.3f });
    }

    // ── Goblin ──────────────────────────────────────────────────────────────
    // Small hitbox, loot, lucky.

    private static void BuildGoblin(List<RaceSkill> list)
    {
        Make(list, "goblin", "hub", "Goblin Cunning", 0, true, None(), null,
            "Small but clever — lucky finds and quick feet.");

        // Lucky Find — loot + Luck.
        Make(list, "goblin", "lf1", "Lucky Find", 1, true, None(), Chain("goblin"),
            "+5% luck bonus.",
            mods: M(RaceModType.LuckBonus, 5f));
        Make(list, "goblin", "lf2", "Scavenge", 2, true, None(), P(N("goblin", "lf1")),
            "+5% consumable potency.",
            mods: M(RaceModType.ConsumablePotencyMul, 0.05f));
        Make(list, "goblin", "lf3", "Goblin Grenade", 2, false, Focus(10f), P(N("goblin", "lf1")),
            "Throw an explosive device.",
            new RaceStrikeEffect { Radius = 3f, BasePower = 30f, Type = DamageType.Fire });

        // Nimble — speed + AS.
        Make(list, "goblin", "nb1", "Nimble", 1, true, None(), Chain("goblin"),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "goblin", "nb2", "Quick Feet", 2, true, None(), P(N("goblin", "nb1")),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "goblin", "nb3", "Dodge Roll", 2, false, Stamina(8f), P(N("goblin", "nb1")),
            "Roll away from danger, gaining brief invulnerability.",
            new RaceStealthEffect { Seconds = 2f });

        // Goblin Tactics — utility.
        Make(list, "goblin", "gt1", "Goblin Tactics", 1, true, None(), Chain("goblin"),
            "+5% parry window.",
            mods: M(RaceModType.ParryWindowMul, 0.05f));
        Make(list, "goblin", "gt2", "Tricky", 2, true, None(), P(N("goblin", "gt1")),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));
        Make(list, "goblin", "gt3", "Sneak Attack", 2, false, Stamina(12f), P(N("goblin", "gt1")),
            "A devastating sneak attack from stealth.",
            new RaceStrikeEffect { Radius = 2f, BasePower = 40f, Type = DamageType.Dark });
    }

    // ── Orc ─────────────────────────────────────────────────────────────────
    // Strong, stagger, HP regen.

    private static void BuildOrc(List<RaceSkill> list)
    {
        Make(list, "orc", "hub", "Orcish Heritage", 0, true, None(), null,
            "Born for war — raw strength and unbreakable will.");

        // Brute Force — offense.
        Make(list, "orc", "bf1", "Iron Bones", 1, true, None(), Chain("orc"),
            "+5% strength bonus. Massive orcish power.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "orc", "bf2", "Crushing Blows", 2, true, None(), P(N("orc", "bf1")),
            "+8% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.08f));
        Make(list, "orc", "bf3", "Warcry", 2, false, Stamina(14f), P(N("orc", "bf1")),
            "Unleash a terrifying warcry — taunt and stagger foes.",
            new RaceRoarEffect { Radius = 6f, Duration = 4f, StaggerAmount = 20f });

        // Resilience — defense + sustain.
        Make(list, "orc", "re1", "Thick Hide", 1, true, None(), Chain("orc"),
            "+5% defense bonus.",
            mods: new[] { M(RaceModType.DefenseBonus, 5f), M(RaceModType.DefenseMeleeMul, 0.04f) });
        Make(list, "orc", "re2", "Endurance", 2, true, None(), P(N("orc", "re1")),
            "+8% stamina regen.",
            mods: M(RaceModType.StaminaRegenMul, 0.08f));
        Make(list, "orc", "re3", "Blood Pact", 2, false, Stamina(12f), P(N("orc", "re1")),
            "Sacrifice blood for power: -15% damage taken for 10s.",
            new RaceAuraEffect { Seconds = 10f, DamageReduction = 0.15f, HpRegenPerSecond = 0f });

        // Savage Instinct — utility.
        Make(list, "orc", "si1", "Quick Learner", 1, true, None(), Chain("orc"),
            "+5% XP bonus.",
            mods: M(RaceModType.XpBonusMul, 0.05f));
        Make(list, "orc", "si2", "Feral Speed", 2, true, None(), P(N("orc", "si1")),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "orc", "si3", "Berserker Rage", 2, false, Stamina(16f), P(N("orc", "si1")),
            "Enter a rage: +10% attack speed and +5% melee power for 12s.",
            new RaceStatBuffEffect { Seconds = 12f, Stat = StatType.AttackSpeed, Amount = 10f, Radius = 3f });
    }

    // ── Ice Giant ───────────────────────────────────────────────────────────
    // Cold immune, freeze aura, tanky.

    private static void BuildIceGiant(List<RaceSkill> list)
    {
        Make(list, "ice_giant", "hub", "Frozen Blood", 0, true, None(), null,
            "Frozen heart — cold immune and devastatingly strong.");

        // Frozen Core — cold res + HP.
        Make(list, "ice_giant", "fc1", "Frozen Core", 1, true, None(), Chain("ice_giant"),
            "+5% health bonus. icy endurance.",
            mods: M(RaceModType.HealthBonus, 5f));
        Make(list, "ice_giant", "fc2", "Glacial", 2, true, None(), P(N("ice_giant", "fc1")),
            "+5% defense bonus.",
            mods: M(RaceModType.DefenseBonus, 5f));
        Make(list, "ice_giant", "fc3", "Ice Breath", 2, false, Focus(14f), P(N("ice_giant", "fc1")),
            "Breathe freezing air, slowing and damaging foes.",
            new RaceCcZoneEffect { Radius = 4f, Duration = 4f, SlowFactor = 0.5f });

        // Glacial Speed — offense.
        Make(list, "ice_giant", "gs1", "Glacial Strength", 1, true, None(), Chain("ice_giant"),
            "+5% strength bonus.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "ice_giant", "gs2", "Heavy", 2, true, None(), P(N("ice_giant", "gs1")),
            "+8% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.08f));
        Make(list, "ice_giant", "gs3", "Avalanche", 2, false, Stamina(20f), P(N("ice_giant", "gs1")),
            "Charge forward, crushing all in your path.",
            new RaceStrikeEffect { Radius = 3f, BasePower = 45f });

        // Blizzard — CC.
        Make(list, "ice_giant", "bl1", "Frost Aura", 1, true, None(), Chain("ice_giant"),
            "+10% stagger resistance.",
            mods: M(RaceModType.StaggerResistMul, 0.10f));
        Make(list, "ice_giant", "bl2", "Chill", 2, true, None(), P(N("ice_giant", "bl1")),
            "+5% cooldown reduction.",
            mods: M(RaceModType.CooldownMul, 0.05f));
        Make(list, "ice_giant", "bl3", "Blizzard", 2, false, Focus(18f), P(N("ice_giant", "bl1")),
            "Summon a blizzard, freezing all nearby.",
            new RaceCcZoneEffect { Radius = 5f, Duration = 5f, SlowFactor = 0.3f });
    }

    // ── Vampire ─────────────────────────────────────────────────────────────
    // Lifesteal, fast, sunlight weak.

    private static void BuildVampire(List<RaceSkill> list)
    {
        Make(list, "vampire", "hub", "Bloodline", 0, true, None(), null,
            "The thirst defines you — drain life and move like shadows.");

        // Sanguine — lifesteal + sustain.
        Make(list, "vampire", "sa1", "Blood Taste", 1, true, None(), Chain("vampire"),
            "+5% lifesteal on hit. The hunger grows.",
            mods: M(RaceModType.LifestealBonus, 0.05f));
        Make(list, "vampire", "sa2", "Vital Siphon", 2, true, None(), P(N("vampire", "sa1")),
            "+0.5% max HP/s regeneration.",
            mods: M(RaceModType.HpRegenPerSecond, 0.005f));
        Make(list, "vampire", "sa3", "Bat Swarm", 2, false, Focus(14f), P(N("vampire", "sa1")),
            "Unleash a swarm of bats that damage and lifesteal.",
            new RaceLifestealStrikeEffect { Radius = 3.5f, BasePower = 25f, LifestealFraction = 0.4f, Type = DamageType.Dark });

        // Shadow — speed + stealth.
        Make(list, "vampire", "sh1", "Night Stalker", 1, true, None(), Chain("vampire"),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "vampire", "sh2", "Cloak", 2, true, None(), P(N("vampire", "sh1")),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));
        Make(list, "vampire", "sh3", "Mist Form", 2, false, Stamina(10f), P(N("vampire", "sh1")),
            "Become mist — intangible for 5s.",
            new RaceStealthEffect { Seconds = 5f });

        // Charm — spell power.
        Make(list, "vampire", "ch1", "Alluring", 1, true, None(), Chain("vampire"),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "vampire", "ch2", "Mesmerize", 2, true, None(), P(N("vampire", "ch1")),
            "+5% wisdom bonus.",
            mods: M(RaceModType.WisdomBonus, 5f));
        Make(list, "vampire", "ch3", "Charm Gaze", 2, false, Focus(12f), P(N("vampire", "ch1")),
            "Charm a foe, confusing them for 6s.",
            new RaceCcZoneEffect { Radius = 3f, Duration = 6f, SlowFactor = 0.8f });
    }

    // ── Demonkin ────────────────────────────────────────────────────────────
    // Fire resistant, fire aura, strong.

    private static void BuildDemonkin(List<RaceSkill> list)
    {
        Make(list, "demonkin", "hub", "Hellborn", 0, true, None(), null,
            "Born of hellfire — burn everything.");

        // Hellfire — fire resistance + offense.
        Make(list, "demonkin", "hf1", "Hellfire", 1, true, None(), Chain("demonkin"),
            "+5% strength bonus.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "demonkin", "hf2", "Infernal", 2, true, None(), P(N("demonkin", "hf1")),
            "+8% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.08f));
        Make(list, "demonkin", "hf3", "Hellfire Pulse", 2, false, Focus(16f), P(N("demonkin", "hf1")),
            "Emit a pulse of hellfire around you.",
            new RaceStrikeEffect { Radius = 3.5f, BasePower = 40f, Type = DamageType.Fire });

        // Demonic Strength — raw power.
        Make(list, "demonkin", "ds1", "Demonic Strength", 1, true, None(), Chain("demonkin"),
            "+5% strength bonus.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "demonkin", "ds2", "Rage", 2, true, None(), P(N("demonkin", "ds1")),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "demonkin", "ds3", "Demon Rush", 2, false, Stamina(18f), P(N("demonkin", "ds1")),
            "Dash forward with demonic speed, striking foes.",
            new RaceStrikeEffect { Radius = 2.5f, BasePower = 38f, Type = DamageType.Fire });

        // Shadowflame — DoT.
        Make(list, "demonkin", "sf1", "Shadowflame", 1, true, None(), Chain("demonkin"),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "demonkin", "sf2", "Corruption", 2, true, None(), P(N("demonkin", "sf1")),
            "+5% intelligence bonus.",
            mods: M(RaceModType.IntelligenceBonus, 5f));
        Make(list, "demonkin", "sf3", "Hellfire Aura", 2, true, None(), P(N("demonkin", "sf1")),
            "+0.3% max HP/s regeneration.",
            mods: M(RaceModType.HpRegenPerSecond, 0.003f));
    }

    // ── Angel ───────────────────────────────────────────────────────────────
    // Elemental resist, faith, holy.

    private static void BuildAngel(List<RaceSkill> list)
    {
        Make(list, "angel", "hub", "Divine Wings", 0, true, None(), null,
            "Heaven's chosen — holy light and divine protection.");

        // Divine Grace — faith + heal.
        Make(list, "angel", "dg1", "Divine Grace", 1, true, None(), Chain("angel"),
            "+5% faith bonus.",
            mods: M(RaceModType.FaithBonus, 5f));
        Make(list, "angel", "dg2", "Blessed", 2, true, None(), P(N("angel", "dg1")),
            "+8% healing power.",
            mods: M(RaceModType.HealPowerMul, 0.08f));
        Make(list, "angel", "dg3", "Holy Light", 2, false, Focus(14f), P(N("angel", "dg1")),
            "Heal 50 HP and cleanse debuffs.",
            new RaceHealEffect { Amount = 50f });

        // Angelic Flight — speed + utility.
        Make(list, "angel", "af1", "Angelic Flight", 1, true, None(), Chain("angel"),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "angel", "af2", "Graceful", 2, true, None(), P(N("angel", "af1")),
            "+5% parry window.",
            mods: M(RaceModType.ParryWindowMul, 0.05f));
        Make(list, "angel", "af3", "Divine Smite", 2, false, Focus(18f), P(N("angel", "af1")),
            "Smite foes with divine power.",
            new RaceStrikeEffect { Radius = 3f, BasePower = 42f, Type = DamageType.Holy });

        // Holy Light — offense.
        Make(list, "angel", "hl1", "Holy Wrath", 1, true, None(), Chain("angel"),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "angel", "hl2", "Sanctuary", 2, true, None(), P(N("angel", "hl1")),
            "+5% health bonus.",
            mods: M(RaceModType.HealthBonus, 5f));
        Make(list, "angel", "hl3", "Heavenly Shield", 2, true, None(), P(N("angel", "hl1")),
            "+5% blocking effectiveness.",
            mods: M(RaceModType.BlockingMul, 0.05f));
    }

    // ── Succubus ────────────────────────────────────────────────────────────
    // Charm, dexterity, spell power.

    private static void BuildSuccubus(List<RaceSkill> list)
    {
        Make(list, "succubus", "hub", "Seductive Power", 0, true, None(), null,
            "Charm and beguile — magic through desire.");

        // Allure — spell power.
        Make(list, "succubus", "al1", "Allure", 1, true, None(), Chain("succubus"),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "succubus", "al2", "Fascinate", 2, true, None(), P(N("succubus", "al1")),
            "+5% wisdom bonus.",
            mods: M(RaceModType.WisdomBonus, 5f));
        Make(list, "succubus", "al3", "Charm Wave", 2, false, Focus(14f), P(N("succubus", "al1")),
            "Release a wave of charm, confusing nearby foes.",
            new RaceCcZoneEffect { Radius = 4f, Duration = 5f, SlowFactor = 0.7f });

        // Seduction — speed + Dex.
        Make(list, "succubus", "se1", "Seduction", 1, true, None(), Chain("succubus"),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "succubus", "se2", "Bewitch", 2, true, None(), P(N("succubus", "se1")),
            "+5% dexterity bonus.",
            mods: M(RaceModType.DexterityBonus, 5f));
        Make(list, "succubus", "se3", "Life Drain", 2, false, Focus(12f), P(N("succubus", "se1")),
            "Drain life from a foe.",
            new RaceLifestealStrikeEffect { Radius = 2.5f, BasePower = 25f, LifestealFraction = 0.4f, Type = DamageType.Dark });

        // Charm — CC.
        Make(list, "succubus", "ch1", "Beguiling", 1, true, None(), Chain("succubus"),
            "+5% cooldown reduction.",
            mods: M(RaceModType.CooldownMul, 0.05f));
        Make(list, "succubus", "ch2", "Enthrall", 2, true, None(), P(N("succubus", "ch1")),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "succubus", "ch3", "Desire's Embrace", 2, true, None(), P(N("succubus", "ch1")),
            "+5% lifesteal on hit.",
            mods: M(RaceModType.LifestealBonus, 0.05f));
    }

    // ── Fishmen ─────────────────────────────────────────────────────────────
    // Swim speed, underwater, endurance.

    private static void BuildFishmen(List<RaceSkill> list)
    {
        Make(list, "fishmen", "hub", "Tidal Blood", 0, true, None(), null,
            "Masters of the deep — water is your domain.");

        // Aquatic — water affinity.
        Make(list, "fishmen", "aq1", "Aquatic", 1, true, None(), Chain("fishmen"),
            "+5% endurance bonus.",
            mods: M(RaceModType.EnduranceBonus, 5f));
        Make(list, "fishmen", "aq2", "Tide Walk", 2, true, None(), P(N("fishmen", "aq1")),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "fishmen", "aq3", "Water Blast", 2, false, Focus(14f), P(N("fishmen", "aq1")),
            "Unleash a torrent of water.",
            new RaceStrikeEffect { Radius = 3.5f, BasePower = 35f, Type = DamageType.Water });

        // Tide's Strength — offense.
        Make(list, "fishmen", "ts1", "Tide's Strength", 1, true, None(), Chain("fishmen"),
            "+5% strength bonus.",
            mods: M(RaceModType.StrengthBonus, 5f));
        Make(list, "fishmen", "ts2", "Crushing", 2, true, None(), P(N("fishmen", "ts1")),
            "+8% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.08f));
        Make(list, "fishmen", "ts3", "Tidal Wave", 2, false, Stamina(18f), P(N("fishmen", "ts1")),
            "Summon a tidal wave that knocks back foes.",
            new RaceCcZoneEffect { Radius = 4f, Duration = 3f, SlowFactor = 0.6f });

        // Deep Endurance — sustain.
        Make(list, "fishmen", "de1", "Deep Endurance", 1, true, None(), Chain("fishmen"),
            "+8% stamina regen.",
            mods: M(RaceModType.StaminaRegenMul, 0.08f));
        Make(list, "fishmen", "de2", "Resilient", 2, true, None(), P(N("fishmen", "de1")),
            "+5% health bonus.",
            mods: M(RaceModType.HealthBonus, 5f));
        Make(list, "fishmen", "de3", "Abyssal", 2, true, None(), P(N("fishmen", "de1")),
            "+5% defense bonus.",
            mods: M(RaceModType.DefenseBonus, 5f));
    }

    // ── Harpy ───────────────────────────────────────────────────────────────
    // Glide, jump, dexterity.

    private static void BuildHarpy(List<RaceSkill> list)
    {
        Make(list, "harpy", "hub", "Wind Wings", 0, true, None(), null,
            "Born of the sky — wind is your ally.");

        // Wing Power — Dex + AS.
        Make(list, "harpy", "wp1", "Wing Power", 1, true, None(), Chain("harpy"),
            "+5% dexterity bonus.",
            mods: M(RaceModType.DexterityBonus, 5f));
        Make(list, "harpy", "wp2", "Feathered", 2, true, None(), P(N("harpy", "wp1")),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "harpy", "wp3", "Talon Dive", 2, false, Stamina(14f), P(N("harpy", "wp1")),
            "Dive from above, striking all below.",
            new RaceStrikeEffect { Radius = 3f, BasePower = 40f, Type = DamageType.Wind });

        // Skyborne — movement.
        Make(list, "harpy", "sb1", "Skyborne", 1, true, None(), Chain("harpy"),
            "+8% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.08f));
        Make(list, "harpy", "sb2", "Glide", 2, true, None(), P(N("harpy", "sb1")),
            "+5% parry window.",
            mods: M(RaceModType.ParryWindowMul, 0.05f));
        Make(list, "harpy", "sb3", "Wind Step", 2, false, Stamina(10f), P(N("harpy", "sb1")),
            "Dash through the air with wind speed.",
            new RaceStealthEffect { Seconds = 3f });

        // Ranged — offense.
        Make(list, "harpy", "ra1", "Sharp Eyes", 1, true, None(), Chain("harpy"),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));
        Make(list, "harpy", "ra2", "Keen", 2, true, None(), P(N("harpy", "ra1")),
            "+5% luck bonus.",
            mods: M(RaceModType.LuckBonus, 5f));
        Make(list, "harpy", "ra3", "Gust", 2, false, Focus(10f), P(N("harpy", "ra1")),
            "Create a wind gust that pushes foes away.",
            new RaceCcZoneEffect { Radius = 3f, Duration = 2f, SlowFactor = 0.8f });
    }

    // ── Dwarf ───────────────────────────────────────────────────────────────
    // Crafting, endurance, forge.

    private static void BuildDwarf(List<RaceSkill> list)
    {
        Make(list, "dwarf", "hub", "Mountain Root", 0, true, None(), null,
            "Stone and steel — master craftsmen and unyielding fighters.");

        // Master Smith — crafting.
        Make(list, "dwarf", "ms1", "Master Smith", 1, true, None(), Chain("dwarf"),
            "+8% consumable potency.",
            mods: M(RaceModType.ConsumablePotencyMul, 0.08f));
        Make(list, "dwarf", "ms2", "Forge Master", 2, true, None(), P(N("dwarf", "ms1")),
            "+5% luck bonus.",
            mods: M(RaceModType.LuckBonus, 5f));
        Make(list, "dwarf", "ms3", "Hammer Slam", 2, false, Stamina(18f), P(N("dwarf", "ms1")),
            "Slam with a forge hammer.",
            new RaceStrikeEffect { Radius = 2.5f, BasePower = 42f });

        // Stout Body — HP + defense.
        Make(list, "dwarf", "sb1", "Stout Body", 1, true, None(), Chain("dwarf"),
            "+5% health bonus.",
            mods: M(RaceModType.HealthBonus, 5f));
        Make(list, "dwarf", "sb2", "Thick Skin", 2, true, None(), P(N("dwarf", "sb1")),
            "+5% defense bonus.",
            mods: new[] { M(RaceModType.DefenseBonus, 5f), M(RaceModType.DefenseMeleeMul, 0.04f) });
        Make(list, "dwarf", "sb3", "Stone Wall", 2, false, Focus(12f), P(N("dwarf", "sb1")),
            "Harden yourself: -12% damage taken for 12s.",
            new RaceAuraEffect { Seconds = 12f, DamageReduction = 0.12f, HpRegenPerSecond = 0f });

        // Forge Power — offense.
        Make(list, "dwarf", "fp1", "Forge Power", 1, true, None(), Chain("dwarf"),
            "+5% melee power.",
            mods: M(RaceModType.MeleePowerMul, 0.05f));
        Make(list, "dwarf", "fp2", "Endurance", 2, true, None(), P(N("dwarf", "fp1")),
            "+5% endurance bonus.",
            mods: M(RaceModType.EnduranceBonus, 5f));
        Make(list, "dwarf", "fp3", "Sturdy", 2, true, None(), P(N("dwarf", "fp1")),
            "+10% stagger resistance.",
            mods: M(RaceModType.StaggerResistMul, 0.10f));
    }

    // ── Gnome ───────────────────────────────────────────────────────────────
    // Lucky, small, loot.

    private static void BuildGnome(List<RaceSkill> list)
    {
        Make(list, "gnome", "hub", "Gnome Wit", 0, true, None(), null,
            "Small in size, huge in luck — trinkets and tricks.");

        // Tinker — craft + luck.
        Make(list, "gnome", "tk1", "Tinker", 1, true, None(), Chain("gnome"),
            "+8% consumable potency.",
            mods: M(RaceModType.ConsumablePotencyMul, 0.08f));
        Make(list, "gnome", "tk2", "Inventor", 2, true, None(), P(N("gnome", "tk1")),
            "+5% luck bonus.",
            mods: M(RaceModType.LuckBonus, 5f));
        Make(list, "gnome", "tk3", "Deploy Turret", 2, false, Focus(14f), P(N("gnome", "tk1")),
            "Deploy a small turret that fires at enemies.",
            new RaceSummonEffect { Power = 10f, Duration = 20f, Range = 4f });

        // Quick Feet — speed.
        Make(list, "gnome", "qf1", "Quick Feet", 1, true, None(), Chain("gnome"),
            "+5% attack speed.",
            mods: M(RaceModType.AttackSpeedBonus, 5f));
        Make(list, "gnome", "qf2", "Nimble", 2, true, None(), P(N("gnome", "qf1")),
            "+5% movement speed.",
            mods: M(RaceModType.MoveSpeedMul, 0.05f));
        Make(list, "gnome", "qf3", "Vanish", 2, false, Stamina(8f), P(N("gnome", "qf1")),
            "Disappear briefly.",
            new RaceStealthEffect { Seconds = 4f });

        // Gizmo — utility.
        Make(list, "gnome", "gz1", "Gizmo", 1, true, None(), Chain("gnome"),
            "+5% parry window.",
            mods: M(RaceModType.ParryWindowMul, 0.05f));
        Make(list, "gnome", "gz2", "Trick", 2, true, None(), P(N("gnome", "gz1")),
            "+5% backstab damage.",
            mods: M(RaceModType.BackstabMul, 0.05f));
        Make(list, "gnome", "gz3", "Smoke Bomb", 2, false, Focus(10f), P(N("gnome", "gz1")),
            "Throw a smoke bomb, blinding nearby foes.",
            new RaceCcZoneEffect { Radius = 3f, Duration = 4f, SlowFactor = 0.6f });
    }

    // ── Elf ─────────────────────────────────────────────────────────────────
    // Dexterity, XP, perception.

    private static void BuildElf(List<RaceSkill> list)
    {
        Make(list, "elf", "hub", "Elven Grace", 0, true, None(), null,
            "Ancient grace — precision, knowledge, and natural harmony.");

        // Elven Grace — Dex + AS.
        Make(list, "elf", "eg1", "Elven Grace", 1, true, None(), Chain("elf"),
            "+5% dexterity bonus.",
            mods: M(RaceModType.DexterityBonus, 5f));
        Make(list, "elf", "eg2", "Swift Blade", 2, true, None(), P(N("elf", "eg1")),
            "+5% attack speed.",
            mods: new[] { M(RaceModType.AttackSpeedBonus, 5f), M(RaceModType.AttackSpeedMul, 0.03f) });
        Make(list, "elf", "eg3", "Wind Step", 2, false, Stamina(10f), P(N("elf", "eg1")),
            "Dash forward with elven speed.",
            new RaceStealthEffect { Seconds = 3f });

        // Arcane Knowledge — spell power.
        Make(list, "elf", "ak1", "Arcane Knowledge", 1, true, None(), Chain("elf"),
            "+5% intelligence bonus.",
            mods: M(RaceModType.IntelligenceBonus, 5f));
        Make(list, "elf", "ak2", "Attuned", 2, true, None(), P(N("elf", "ak1")),
            "+5% spell power.",
            mods: M(RaceModType.SpellPowerMul, 0.05f));
        Make(list, "elf", "ak3", "Arcane Burst", 2, false, Focus(14f), P(N("elf", "ak1")),
            "Release a burst of arcane energy.",
            new RaceStrikeEffect { Radius = 3f, BasePower = 38f, Type = DamageType.Arcane });

        // Nature's Blessing — XP + sustain.
        Make(list, "elf", "nb1", "Nature's Blessing", 1, true, None(), Chain("elf"),
            "+8% XP bonus.",
            mods: M(RaceModType.XpBonusMul, 0.08f));
        Make(list, "elf", "nb2", "Keen Eye", 2, true, None(), P(N("elf", "nb1")),
            "+5% luck bonus.",
            mods: M(RaceModType.LuckBonus, 5f));
        Make(list, "elf", "nb3", "Nature's Shield", 2, false, Focus(12f), P(N("elf", "nb1")),
            "A shield of nature absorbs damage.",
            new RaceHealEffect { Amount = 45f });
    }
}
