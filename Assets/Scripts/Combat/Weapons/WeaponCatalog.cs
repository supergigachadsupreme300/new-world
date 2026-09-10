using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime catalog of the 15 default weapons (Phase 10). Builds <see cref="WeaponData"/>
/// instances in code — no .asset files needed — mirroring the <c>ClassUnlocker.BuildDefaultClasses</c>
/// pattern. Each weapon is authored to represent a class archetype's distinct fighting style
/// (category, wielding, damage type, base damage/speed/reach/weight, scaling) so the archetypes
/// feel different, but NO weapon is class-locked: any player can equip/use any of them.
///
/// This is data-only. To make a weapon combat-active, hand it to <c>WeaponRigBuilder</c>
/// which builds the live weapon GameObject (behavior + hitbox + art executor + visual proxy)
/// and equips it onto the player's <c>CombatController</c> hands.
/// </summary>
public static class WeaponCatalog
{
    /// <summary>The built roster. <see cref="EnsureBuilt"/> populates it once.</summary>
    public static List<WeaponData> All { get; private set; }

    private static bool _built;

    /// <summary>Build the 15-weapon roster on first access (idempotent, append-only).</summary>
    public static void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        All = BuildDefault();
    }

    /// <summary>Look up a weapon by id, or null if not present.</summary>
    public static WeaponData Find(string id)
    {
        EnsureBuilt();
        if (All == null) return null;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].id == id)
                return All[i];
        return null;
    }

    /// <summary>Convenience default starter weapon id (Wanderer's Iron Sword).</summary>
    public const string StarterWeaponId = "iron_sword";

    /// <summary>Base weapon id for the innate bare-fist rigs (not an inventory item).</summary>
    public const string FistWeaponId = "fist";

    /// <summary>
    /// Data for the bare-fist combat mode used when fighting with NO weapon equipped — both hand
    /// slots hold an invisible fist rig driven by the gauntlets boxing animation. Kept OUT of
    /// <see cref="All"/> so it can never be granted as an inventory item or listed in the gear sheet.
    /// </summary>
    public static WeaponData Fists
    {
        get
        {
            if (_fists == null) _fists = BuildFists();
            return _fists;
        }
    }
    private static WeaponData _fists;

    private static WeaponData BuildFists()
    {
        return Make("fist", "Fists", WeaponCategory.Melee, DamageType.Physical, 0f, 4f, 1.7f, 0.6f, 0.7f,
            WeaponScalingStat.Dexterity, 0.06f, 0f, null,
            Skill("wskill_fist", "Iron Fist", "strike", DamageType.Physical, 6f, 1.4f, 0.8f, 2f, 3f));
    }

    /// <summary>
    /// Display name for an inventory item slot. Weapons use their authored <c>displayName</c>
    /// (their ids are English tokens), everything else falls back to <see cref="Localization"/>.
    /// </summary>
    public static string DisplayName(string id)
    {
        WeaponData w = Find(id);
        if (w != null && !string.IsNullOrEmpty(w.displayName)) return w.displayName;
        return Localization.ItemName(id);
    }

    private static List<WeaponData> BuildDefault()
    {
        var list = new List<WeaponData>();

        // ── Melee ──────────────────────────────────────────────────────────
        list.Add(Make("iron_sword", "Iron Sword", WeaponCategory.Melee, DamageType.Physical, 10f, 12f, 1f, 1.2f, 1f, WeaponScalingStat.Dexterity, 0.06f, 5f, null,
            Skill("wskill_iron_sword", "Blade Arc", "strike", DamageType.Physical, 10f, 2.0f, 1.0f, 3f, 6f)));
        list.Add(Make("greatsword", "Greatsword", WeaponCategory.Melee, DamageType.Physical, 18f, 26f, 0.8f, 1.6f, 1.6f, WeaponScalingStat.Strength, 0.10f, 10f, null,
            Skill("wskill_greatsword", "Mighty Cleave", "strike", DamageType.Physical, 16f, 2.4f, 1.4f, 4f, 10f)));
        list.Add(Make("dagger", "Dagger", WeaponCategory.Melee, DamageType.Physical, 5f, 8f, 1.4f, 0.9f, 0.8f, WeaponScalingStat.Dexterity, 0.08f, 2f, null,
            Skill("wskill_dagger", "Quick Stab", "thrust", DamageType.Physical, 8f, 1.8f, 0.6f, 2.5f, 3f)));
        list.Add(Make("katana", "Katana", WeaponCategory.Melee, DamageType.Physical, 13f, 18f, 1.1f, 1.5f, 1.4f, WeaponScalingStat.Dexterity, 0.09f, 6f, null,
            Skill("wskill_katana", "Iaido Slash", "strike", DamageType.Physical, 13f, 2.2f, 1.0f, 3.5f, 6f)));
        list.Add(Make("greataxe", "Greataxe", WeaponCategory.Melee, DamageType.Physical, 22f, 34f, 0.6f, 1.7f, 1.7f, WeaponScalingStat.Strength, 0.11f, 14f, null,
            Skill("wskill_greataxe", "Executioner Swing", "strike", DamageType.Physical, 18f, 2.4f, 1.5f, 4f, 12f)));
        list.Add(Make("lance", "Knight's Lance", WeaponCategory.Melee, DamageType.Physical, 16f, 20f, 0.9f, 2.4f, 1.5f, WeaponScalingStat.Strength, 0.08f, 8f, null,
            Skill("wskill_lance", "Charging Thrust", "thrust", DamageType.Physical, 14f, 3.0f, 0.7f, 4f, 8f)));
        list.Add(Make("gauntlets", "Gauntlets", WeaponCategory.Melee, DamageType.Physical, 4f, 6f, 1.7f, 0.6f, 0.7f, WeaponScalingStat.Dexterity, 0.06f, 3f, null,
            Skill("wskill_gauntlets", "Iron Fist", "strike", DamageType.Physical, 6f, 1.4f, 0.8f, 2f, 3f)));
        list.Add(Make("warhammer", "Warhammer", WeaponCategory.Melee, DamageType.Holy, 17f, 24f, 0.8f, 1.4f, 1.5f, WeaponScalingStat.Strength, 0.09f, 12f, null,
            Skill("wskill_warhammer", "Judgment Smash", "strike", DamageType.Holy, 15f, 2.0f, 1.2f, 4f, 10f)));

        // ── Ranged ─────────────────────────────────────────────────────────
        list.Add(Make("longbow", "Longbow", WeaponCategory.Ranged, DamageType.Physical, 8f, 16f, 1f, 18f, 2f, WeaponScalingStat.Dexterity, 0f, 3f, "arrow",
            Skill("wskill_longbow", "Piercing Arrow", "thrust", DamageType.Physical, 12f, 4.0f, 0.4f, 3.5f, 6f)));
        list.Add(Make("throwing_hammer", "Throwing Hammer", WeaponCategory.Ranged, DamageType.Physical, 10f, 18f, 0.95f, 12f, 1.8f, WeaponScalingStat.Strength, 0f, 6f, null,
            Skill("wskill_throwing_hammer", "Power Throw", "thrust", DamageType.Physical, 13f, 3.5f, 0.5f, 3f, 8f)));

        // ── Magic ──────────────────────────────────────────────────────────
        list.Add(MakeMagic("staff", "Mage's Staff", WeaponCategory.Magic, DamageType.Arcane, 8f, 14f, 0.9f, 6f, 1.1f, WeaponScalingStat.Wisdom, 0.08f, 2f,
            1.2f, 1f, 1f, Skill("wskill_staff", "Arcane Burst", "strike", DamageType.Arcane, 12f, 2.6f, 1.2f, 4f, 6f)));
        list.Add(MakeMagic("holy_book", "Holy Book", WeaponCategory.Magic, DamageType.Holy, 7f, 12f, 0.95f, 5f, 1f, WeaponScalingStat.Wisdom, 0.07f, 2f,
            1.1f, 1f, 1f, Skill("wskill_holy_book", "Holy Smite", "strike", DamageType.Holy, 10f, 2.4f, 1.1f, 4f, 6f)));
        list.Add(MakeMagic("bone_wand", "Bone Wand", WeaponCategory.Magic, DamageType.Dark, 8f, 15f, 0.9f, 7f, 1.1f, WeaponScalingStat.Wisdom, 0.09f, 2f,
            1.25f, 1f, 1f, Skill("wskill_bone_wand", "Dark Lash", "strike", DamageType.Dark, 12f, 2.6f, 1.1f, 3.5f, 8f)));
        list.Add(MakeMagic("control_orb", "Control Orb", WeaponCategory.Magic, DamageType.Wind, 9f, 13f, 0.85f, 8f, 1.2f, WeaponScalingStat.Intelligence, 0.09f, 2f,
            1.15f, 0.9f, 1f, Skill("wskill_control_orb", "Wind Burst", "strike", DamageType.Wind, 12f, 2.6f, 1.2f, 3.5f, 8f)));
        list.Add(MakeMagic("lute", "Bard's Lute", WeaponCategory.Magic, DamageType.Physical, 5f, 8f, 1f, 4f, 1f, WeaponScalingStat.Wisdom, 0.06f, 1f,
            1f, 1.1f, 1f, Skill("wskill_lute", "Sonic Wave", "strike", DamageType.Physical, 8f, 2.4f, 1.4f, 3.5f, 6f)));

        return list;
    }

    private static WeaponData Make(string id, string displayName, WeaponCategory category, DamageType type,
        float weight, float baseDamage, float speed, float reach, float coefficient,
        WeaponScalingStat scaling, float stagger, float strengthRequirement, string ammoId, WeaponSkill skill)
    {
        var w = ScriptableObject.CreateInstance<WeaponData>();
        w.name = id;
        w.id = id;
        w.displayName = displayName;
        w.Category = category;
        w.Type = type;
        w.Weight = weight;
        w.BaseDamage = baseDamage;
        w.Speed = speed;
        w.Reach = reach;
        w.ScalingStat = scaling;
        w.ScalingCoefficient = coefficient;
        w.StaggerPower = stagger;
        w.StrengthRequirement = strengthRequirement;
        w.AmmoItemId = ammoId;
        w.Skill = skill;
        return w;
    }

    private static WeaponData MakeMagic(string id, string displayName, WeaponCategory category, DamageType type,
        float weight, float baseDamage, float speed, float reach, float coefficient,
        WeaponScalingStat scaling, float stagger, float strengthRequirement,
        float damageMult, float castTimeMod, float cooldownMod, WeaponSkill skill)
    {
        var w = Make(id, displayName, category, type, weight, baseDamage, speed, reach, coefficient, scaling, stagger, strengthRequirement, null, skill);
        w.MagicDamageMult = damageMult;
        w.CastTimeMod = castTimeMod;
        w.CooldownMod = cooldownMod;
        return w;
    }

    private static WeaponSkill Skill(string id, string displayName, string kind, DamageType type,
        float baseDamage, float range, float radius, float cooldown, float knockback)
    {
        var s = ScriptableObject.CreateInstance<WeaponSkill>();
        s.name = id;
        s.id = id;
        s.displayName = displayName;
        s.Kind = kind;
        s.Type = type;
        s.BaseDamage = baseDamage;
        s.FpCost = 0f;
        s.Range = range;
        s.Radius = radius;
        s.Cooldown = cooldown;
        s.Knockback = knockback;
        return s;
    }
}