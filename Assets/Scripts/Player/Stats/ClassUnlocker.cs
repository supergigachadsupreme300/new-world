using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evaluates class unlock conditions against the player's current stats / skill levels
/// (game-design §3.2, planning Task 4.5). Classes are classless and non-exclusive — a player
/// can unlock many and mix abilities. Fires <see cref="OnClassUnlocked"/> when a new one is
/// earned. Attach to the player root and register the pool of ClassData to evaluate.
/// </summary>
[DisallowMultipleComponent]
public class ClassUnlocker : MonoBehaviour
{
    public List<ClassData> Classes = new List<ClassData>();
    public List<string> UnlockedClassIds = new List<string>();

    private readonly HashSet<string> _unlocked = new HashSet<string>();
    private PlayerStats _stats;
    private SkillXpTracker _skills;

    /// <summary>Fires when a class becomes unlocked.</summary>
    public event System.Action<ClassData> OnClassUnlocked;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _skills = GetComponent<SkillXpTracker>();
        foreach (var id in UnlockedClassIds) _unlocked.Add(id);
        if (Classes.Count == 0)
            Classes = BuildDefaultClasses();
    }

    /// <summary>Re-evaluate all classes; unlocks any newly satisfied (Wanderer baseline is always free).</summary>
    public void EvaluateAll()
    {
        if (Classes == null) return;
        foreach (var c in Classes)
        {
            if (c == null) continue;
            if (string.Equals(c.classId, "wanderer", System.StringComparison.OrdinalIgnoreCase))
                UnlockIfAbsent(c);
            else if (MeetsRequirements(c))
                UnlockIfAbsent(c);
        }
    }

    private void UnlockIfAbsent(ClassData c)
    {
        if (_unlocked.Add(c.classId))
        {
            UnlockedClassIds.Add(c.classId);
            OnClassUnlocked?.Invoke(c);
        }
    }

    public bool IsUnlocked(string classId) => _unlocked.Contains(classId);

    private bool MeetsRequirements(ClassData c)
    {
        if (c.StatRequirements != null)
        {
            foreach (var req in c.StatRequirements)
            {
                float current = _stats != null ? _stats.GetTotal(req.Stat) : 0f;
                if (current < req.Minimum) return false;
            }
        }
        if (c.CombinedRequirements != null)
        {
            foreach (var req in c.CombinedRequirements)
            {
                float sum = _stats != null
                    ? _stats.GetTotal(req.First) + _stats.GetTotal(req.Second)
                    : 0f;
                if (sum < req.MinimumTotal) return false;
            }
        }
        if (c.MinAnyTwoStats > 0f)
        {
            int met = 0;
            for (int i = 0; i < (int)StatType.Luck + 1 && _stats != null; i++)
                if (_stats.GetTotal((StatType)i) >= c.MinAnyTwoStats) met++;
            if (met < 2) return false;
        }
        if (c.SkillRequirements != null && _skills != null)
        {
            foreach (var req in c.SkillRequirements)
                if (_skills.GetLevel(req.Skill) < req.Level) return false;
        }
        return true;
    }

    /// <summary>
    /// Programmatic 15-class roster (§3.2). Wanderer is the free baseline; the other 14 are
    /// stat / skill-threshold based and non-exclusive.
    /// </summary>
    public static List<ClassData> BuildDefaultClasses()
    {
        var list = new List<ClassData>();
        var none = new StatReq[0];
        var noneC = new CombinedReq[0];
        var noneS = new SkillReq[0];

        list.Add(Make("wanderer", "Wanderer", none, noneC, 0f, noneS,
            "Balanced starting stats, no special abilities."));

        list.Add(Make("warrior", "Warrior",
            new[] { new StatReq { Stat = StatType.Strength, Minimum = 20 } }, noneC, 0f, noneS,
            "Weapon Arts enhanced, stance breaking."));

        list.Add(Make("mage", "Mage",
            new[] { new StatReq { Stat = StatType.Wisdom, Minimum = 20 } }, noneC, 0f, noneS,
            "Spell casting, magic damage."));

        list.Add(Make("rogue", "Rogue",
            new[] { new StatReq { Stat = StatType.Dexterity, Minimum = 20 } }, noneC, 0f, noneS,
            "Backstab bonus, stealth attacks."));

        list.Add(Make("cleric", "Cleric",
            new[] { new StatReq { Stat = StatType.Faith, Minimum = 20 } }, noneC, 0f, noneS,
            "Healing miracles, buffs."));

        list.Add(Make("berserker", "Berserker",
            none,
            new[] { new CombinedReq { First = StatType.Strength, Second = StatType.Endurance, MinimumTotal = 35 } },
            0f, noneS,
            "Damage increases as HP drops."));

        list.Add(Make("necromancer", "Necromancer",
            none,
            new[] { new CombinedReq { First = StatType.Wisdom, Second = StatType.Faith, MinimumTotal = 35 } },
            0f, noneS,
            "Summon undead allies."));

        list.Add(Make("samurai", "Samurai",
            none,
            new[] { new CombinedReq { First = StatType.Dexterity, Second = StatType.Endurance, MinimumTotal = 35 } },
            0f, noneS,
            "Perfect parry window extended."));

        list.Add(Make("alchemist", "Alchemist",
            none, noneC, 18f, noneS,
            "Enhanced consumable effects (any 2 stats ≥ 18)."));

        list.Add(Make("knight", "Knight",
            none,
            new[] { new CombinedReq { First = StatType.Defense, Second = StatType.Strength, MinimumTotal = 35 } },
            0f, noneS,
            "Buffs defense & melee; increases equip-load carry."));

        list.Add(Make("archer", "Archer",
            new[] { new StatReq { Stat = StatType.Dexterity, Minimum = 20 } }, noneC, 0f, noneS,
            "Ranged accuracy & handling."));

        list.Add(Make("enchanter", "Enchanter",
            none,
            new[] { new CombinedReq { First = StatType.Intelligence, Second = StatType.Wisdom, MinimumTotal = 35 } },
            0f, noneS,
            "Control/zone mage (slow, roots, area denial)."));

        list.Add(Make("brawler", "Brawler",
            new[] { new StatReq { Stat = StatType.Strength, Minimum = 20 } }, noneC, 0f, noneS,
            "Unarmed/grapple crowd control."));

        list.Add(Make("paladin", "Paladin",
            none,
            new[] { new CombinedReq { First = StatType.Faith, Second = StatType.Endurance, MinimumTotal = 35 } },
            0f, noneS,
            "Holy tank/support (taunt, guard allies)."));

        list.Add(Make("bard", "Bard",
            none,
            new[] { new CombinedReq { First = StatType.Faith, Second = StatType.Intelligence, MinimumTotal = 35 } },
            0f, noneS,
            "Party-wide buffs/auras."));

        list.Add(Make("blacksmith", "Blacksmith",
            none, noneC, 0f,
            new[] { new SkillReq { Skill = SkillType.Crafting, Level = 10 } },
            "Crafting/forge support: gear upgrade success, repair, forging bonuses."));

        return list;
    }

    private static ClassData Make(string id, string name, StatReq[] stats, CombinedReq[] combined,
        float minAnyTwo, SkillReq[] skills, string mechanic)
    {
        var c = ScriptableObject.CreateInstance<ClassData>();
        c.classId = id;
        c.displayName = name;
        c.StatRequirements = stats;
        c.CombinedRequirements = combined;
        c.MinAnyTwoStats = minAnyTwo;
        c.SkillRequirements = skills;
        c.UniqueMechanic = mechanic;
        return c;
    }
}