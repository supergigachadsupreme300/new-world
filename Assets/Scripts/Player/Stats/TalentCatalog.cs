using System.Collections.Generic;

/// <summary>
/// What a talent's ranks improve.
/// </summary>
public enum TalentKind
{
    /// <summary>Boosts all character XP (<see cref="LevelUpSystem.AddXp"/>).</summary>
    PlayerXp,

    /// <summary>Boosts a skill type's per-skill leveling XP and its category bar.</summary>
    SkillTypeXp,

    /// <summary>Adds flat points to a core stat (<see cref="StatType"/>).</summary>
    Stat,

    /// <summary>Adds flat critical-hit chance % (additive with tree crit perks).</summary>
    CritChance,

    /// <summary>Adds critical-hit damage % (stacks with tree crit-damage perks).</summary>
    CritDamage,

    /// <summary>Adds backstab damage %.</summary>
    Backstab,

    /// <summary>Adds block stamina-efficiency % (less stamina drained per blocked hit).</summary>
    BlockEfficiency,

    /// <summary>Adds stagger/knockback resistance %.</summary>
    StaggerResist,

    /// <summary>Adds stamina regeneration %.</summary>
    StaminaRegen,

    /// <summary>Adds focus (FP) regeneration %.</summary>
    FocusRegen
}

/// <summary>
/// Definition of a talent — a rankable perk. Every rank costs one talent point and stacks
/// additively. The first talent is granted at random on game creation; further talents are
/// unlocked with points earned per character level-up.
/// </summary>
[System.Serializable]
public sealed class Talent
{
    public string Id;
    public string DisplayName;
    public int MaxRanks = 3;
    public TalentKind Kind;

    /// <summary>Category scoped by <see cref="TalentKind.SkillTypeXp"/> talents.</summary>
    public SkillType Scope;

    /// <summary>Stat boosted by <see cref="TalentKind.Stat"/> talents.</summary>
    public StatType Stat;

    /// <summary>Effect per rank: +% XP (PlayerXp / SkillTypeXp), +% combat/regen (CritChance … FocusRegen),
    /// or +flat stat points (Stat).</summary>
    public float PerRank;

    /// <summary>Human-readable effect for one rank: "+X% XP", "+X% combat/regen", or "+X stat".</summary>
    public string EffectPerRank()
    {
        if (Kind == TalentKind.PlayerXp) return "+" + PerRank.ToString("0") + "% character XP";
        if (Kind == TalentKind.SkillTypeXp) return "+" + PerRank.ToString("0") + "% " + DisplayName + " XP";
        if (Kind == TalentKind.CritChance) return "+" + PerRank.ToString("0") + "% critical hit chance";
        if (Kind == TalentKind.CritDamage) return "+" + PerRank.ToString("0") + "% crit damage";
        if (Kind == TalentKind.Backstab) return "+" + PerRank.ToString("0") + "% backstab damage";
        if (Kind == TalentKind.BlockEfficiency) return "+" + PerRank.ToString("0") + "% block efficiency";
        if (Kind == TalentKind.StaggerResist) return "+" + PerRank.ToString("0") + "% stagger resistance";
        if (Kind == TalentKind.StaminaRegen) return "+" + PerRank.ToString("0") + "% stamina regen";
        if (Kind == TalentKind.FocusRegen) return "+" + PerRank.ToString("0") + "% focus regen";
        return "+" + PerRank.ToString("0") + " " + Stat;
    }
}

/// <summary>
/// Runtime catalog of talents (built in code, no .asset files). Backs the random first grant,
    /// the free rank-up flow, and the XP/stat bonus reads on <see cref="TalentTracker"/>.
/// </summary>
public static class TalentCatalog
{
    /// <summary>The built roster. <see cref="EnsureBuilt"/> populates it once.</summary>
    public static List<Talent> All { get; private set; }

    private static bool _built;
    private static Dictionary<string, Talent> _cache;

    /// <summary>Build the roster on first access (idempotent).</summary>
    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
        _cache = new Dictionary<string, Talent>(All.Count);
        foreach (var t in All)
            if (t != null && !string.IsNullOrEmpty(t.Id))
                _cache[t.Id] = t;
    }

    /// <summary>Look up a talent by id, or null.</summary>
    public static Talent Find(string id)
    {
        EnsureBuilt();
        if (string.IsNullOrEmpty(id)) return null;
        _cache.TryGetValue(id, out var talent);
        return talent;
    }

    private static List<Talent> BuildDefault()
    {
        var list = new List<Talent>();

        list.Add(new Talent
        {
            Id = "t.fast_learner", DisplayName = "Fast Learner",
            MaxRanks = 3, Kind = TalentKind.PlayerXp, PerRank = 5f
        });

        AddType(list, SkillType.Melee, "t.melee", "Melee Training");
        AddType(list, SkillType.Ranged, "t.ranged", "Ranged Drills");
        AddType(list, SkillType.Magic, "t.magic", "Arcane Study");
        AddType(list, SkillType.Stealth, "t.stealth", "Shadow Arts");
        AddType(list, SkillType.Crafting, "t.crafting", "Craftsmanship");
        AddType(list, SkillType.Fortitude, "t.fortitude", "Fortitude");
        AddType(list, SkillType.Shield, "t.shield", "Shield Work");

        AddStat(list, StatType.Health, "t.health", "Vitality");
        AddStat(list, StatType.Speed, "t.speed", "Fleet");
        AddStat(list, StatType.Endurance, "t.endurance", "Conditioning");
        AddStat(list, StatType.Strength, "t.strength", "Might");
        AddStat(list, StatType.Dexterity, "t.dexterity", "Finesse");
        AddStat(list, StatType.AttackSpeed, "t.attackspeed", "Celerity");
        AddStat(list, StatType.Defense, "t.defense", "Armored");
        AddStat(list, StatType.Intelligence, "t.intelligence", "Focused Mind");
        AddStat(list, StatType.Wisdom, "t.wisdom", "Sage");
        AddStat(list, StatType.Faith, "t.faith", "Devoted");
        AddStat(list, StatType.Luck, "t.luck", "Lucky");

        AddPercent(list, "t.crit_chance", "Critical Eye", TalentKind.CritChance, 2f);
        AddPercent(list, "t.crit_damage", "Executioner", TalentKind.CritDamage, 15f);
        AddPercent(list, "t.backstab", "Ambush", TalentKind.Backstab, 10f);
        AddPercent(list, "t.block_efficiency", "Bulwark", TalentKind.BlockEfficiency, 10f);
        AddPercent(list, "t.stagger_resist", "Grounded", TalentKind.StaggerResist, 10f);
        AddPercent(list, "t.stamina_regen", "Second Wind", TalentKind.StaminaRegen, 10f);
        AddPercent(list, "t.focus_regen", "Arcane Spring", TalentKind.FocusRegen, 10f);

        return list;
    }

    private static void AddType(List<Talent> list, SkillType type, string id, string name)
    {
        list.Add(new Talent
        {
            Id = id, DisplayName = name,
            MaxRanks = 3, Kind = TalentKind.SkillTypeXp, Scope = type, PerRank = 6f
        });
    }

    private static void AddStat(List<Talent> list, StatType stat, string id, string name)
    {
        list.Add(new Talent
        {
            Id = id, DisplayName = name,
            MaxRanks = 3, Kind = TalentKind.Stat, Stat = stat, PerRank = 1f
        });
    }

    private static void AddPercent(List<Talent> list, string id, string name, TalentKind kind, float perRank)
    {
        list.Add(new Talent
        {
            Id = id, DisplayName = name,
            MaxRanks = 3, Kind = kind, PerRank = perRank
        });
    }
}