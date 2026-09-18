using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the player's single chosen class (game-design §3.2.1). The class system is
/// EXCLUSIVE — the player has exactly ONE class at a time (<see cref="ActiveClassId"/>), and
/// <see cref="UnlockedClassIds"/> always holds that one id. Changing class (via the Class tab /
/// <see cref="SetActiveClass"/>) replaces the choice; there is no accumulating roster and no
/// "unlock everything when requirements are met" pass. Fires
/// <see cref="OnActiveClassChanged"/> / <see cref="OnClassUnlocked"/> so
/// <see cref="ClassPassiveManager"/> re-aggregates the active class's kit. Attach to the player root.
/// </summary>
[DisallowMultipleComponent]
public class ClassUnlocker : MonoBehaviour
{
    public List<ClassData> Classes = new List<ClassData>();
    [Tooltip("The single chosen class (the only id in the unlock set).")]
    public List<string> UnlockedClassIds = new List<string>();
    [Tooltip("The class the player currently identifies with (always the one chosen class).")]
    public string ActiveClassId = "wanderer";

    /// <summary>True after a save restore populated the choice — Start() skips re-deriving it.</summary>
    private bool _restoredFromSave;

    /// <summary>Fires when a class becomes the chosen class.</summary>
    public event System.Action<ClassData> OnClassUnlocked;

    /// <summary>Fires when the active class changes.</summary>
    public event System.Action<ClassData> OnActiveClassChanged;

    private void Awake()
    {
        if (Classes.Count == 0)
            Classes = BuildDefaultClasses();
        if (string.IsNullOrEmpty(ActiveClassId) || !IsKnown(ActiveClassId))
            ActiveClassId = "wanderer";
        // Single-choice model: collapse any legacy multi-class roster to the one active class.
        if (!IsUnlocked(ActiveClassId))
            SetActiveClass(ActiveClassId);
    }

    private void Start()
    {
        // A save restore is authoritative — the restored class is already chosen.
        if (!_restoredFromSave)
            EnsureChosenClass();
        // Ensure the passive manager is present so active-class modifiers are live
        // (mirrors RaceChangeManager auto-adding RacePassiveManager).
        if (GetComponent<ClassPassiveManager>() == null)
            gameObject.AddComponent<ClassPassiveManager>();
    }

    /// <summary>
    /// Restore the single chosen class from a save (used by <see cref="SaveManager"/> on load).
    /// Any legacy multi-class roster collapses to the saved active class (Wanderer baseline if
    /// unknown). Fires <see cref="OnActiveClassChanged"/> when the restored active differs from the
    /// current so <see cref="ClassPassiveManager"/> re-aggregates the modifier set.
    /// </summary>
    public void RestoreUnlocks(IEnumerable<string> unlockedIds, string savedActiveClassId)
    {
        _restoredFromSave = true;
        string target = GetKnownOrDefault(savedActiveClassId);
        if (!string.Equals(ActiveClassId, target, System.StringComparison.OrdinalIgnoreCase))
        {
            ActiveClassId = target;
            OnActiveClassChanged?.Invoke(ActiveClass);
        }
        if (!IsUnlocked(target))
            SetActiveClass(target);
    }

    /// <summary>
    /// Single-choice model: there is no auto-unlock roster. This call only guarantees a valid
    /// chosen class exists (Wanderer baseline) so UI/combat code can rely on <see cref="ActiveClass"/>
    /// being non-null.
    /// </summary>
    public void EvaluateAll()
    {
        EnsureChosenClass();
    }

    private void EnsureChosenClass()
    {
        if (!IsKnown(ActiveClassId)) ActiveClassId = "wanderer";
        if (!IsUnlocked(ActiveClassId))
            SetActiveClass(ActiveClassId);
    }

    /// <summary>True only for the single currently-chosen class.</summary>
    public bool IsUnlocked(string classId)
    {
        if (string.IsNullOrEmpty(classId)) return false;
        return UnlockedClassIds != null && UnlockedClassIds.Count == 1
            && string.Equals(UnlockedClassIds[0], classId, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The currently-read <see cref="ClassData"/> for <see cref="ActiveClassId"/>.</summary>
    public ClassData ActiveClass
    {
        get
        {
            if (Classes == null) return null;
            foreach (var c in Classes)
                if (c != null && string.Equals(c.classId, ActiveClassId, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
        }
    }

    public ClassData FindClass(string classId)
    {
        if (string.IsNullOrEmpty(classId) || Classes == null) return null;
        foreach (var c in Classes)
            if (c != null && string.Equals(c.classId, classId, System.StringComparison.OrdinalIgnoreCase))
                return c;
        return null;
    }

    /// <summary>
    /// Choose the active class. Selecting any known class REPLACES the current choice — the unlock
    /// set always contains exactly one id. Returns false only when the id is unknown.
    /// </summary>
    public bool SetActiveClass(string classId)
    {
        var target = FindClass(classId);
        if (target == null) return false;

        bool firstSelection = !IsUnlocked(target.classId);
        if (!string.Equals(ActiveClassId, target.classId, System.StringComparison.OrdinalIgnoreCase))
        {
            ActiveClassId = target.classId;
            OnActiveClassChanged?.Invoke(target);
        }
        UnlockedClassIds.Clear();
        UnlockedClassIds.Add(target.classId);
        if (firstSelection)
            OnClassUnlocked?.Invoke(target);
        return true;
    }

    private bool IsKnown(string classId) => FindClass(classId) != null;

    private string GetKnownOrDefault(string classId) => IsKnown(classId) ? classId : "wanderer";

    /// <summary>
    /// Programmatic 17-class roster (§3.2). Wanderer is the free baseline; the other 16 are
    /// stat / skill-threshold based. Exactly one class is chosen at a time (exclusive model).
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

        list.Add(Make("taoist", "Taoist",
            none,
            new[] { new CombinedReq { First = StatType.Wisdom, Second = StatType.Intelligence, MinimumTotal = 35 } },
            0f, noneS,
            "Qi manipulation: enhanced spell cooldowns & stamina regen; demon damage bonus."));

        list.Add(Make("monk", "Monk",
            none,
            new[] { new CombinedReq { First = StatType.Faith, Second = StatType.Endurance, MinimumTotal = 35 } },
            0f, noneS,
            "Inner peace: meditation heals HP; reduced stagger, +defense while unarmed."));

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