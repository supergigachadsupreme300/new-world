using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catalog of RaceData assets (game-design §3.5, planning Task 4.3). Provides lookup by id
/// and a weighted random roll for character creation (Human ≈ 50%, each other ≈ 2.38%).
///
/// <see cref="Races"/> can be authored in-editor; if empty, <see cref="BuildDefaultRoster"/>
/// synthesizes the full 22-race lineup so the game runs without hand-authored assets.
/// </summary>
[CreateAssetMenu(fileName = "RaceDatabase", menuName = "New World/Races/Race Database", order = 51)]
public class RaceDatabase : ScriptableObject
{
    public List<RaceData> Races = new List<RaceData>();

    private static RaceData _human;
    private static readonly Dictionary<string, RaceData> _byId =
        new Dictionary<string, RaceData>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The default Human baseline race (always unlocked, +15% all XP).</summary>
    public static RaceData Human => _human;

    private void OnEnable()
    {
        if (Races.Count == 0)
            Races = BuildDefaultRoster();
    }

    public void RebuildIndex()
    {
        _byId.Clear();
        _human = null;
        if (Races == null) return;
        foreach (var r in Races)
        {
            if (r == null) continue;
            _byId[r.raceId] = r;
            if (string.Equals(r.raceId, "human", StringComparison.OrdinalIgnoreCase))
                _human = r;
        }
    }

    /// <summary>Look a race up by id, or null if not present.</summary>
    public RaceData GetRace(string id)
    {
        RebuildIndex();
        return id != null && _byId.TryGetValue(id, out RaceData r) ? r : null;
    }

    /// <summary>Weighted random roll per §3.5: Human ≈ 50%, others ≈ 2.38% each.</summary>
    public RaceData Roll()
    {
        RebuildIndex();
        if (Races == null || Races.Count == 0) return null;

        float total = 0f;
        for (int i = 0; i < Races.Count; i++)
            total += Mathf.Max(0f, Races[i] != null ? Races[i].Weight : 0f);

        float pick = UnityEngine.Random.value * total;
        float acc = 0f;
        foreach (var r in Races)
        {
            if (r == null) continue;
            acc += Mathf.Max(0f, r.Weight);
            if (pick <= acc) return r;
        }
        return Races[Races.Count - 1];
    }

    /// <summary>
    /// Programmatic full 22-race roster (§3.5 table). Weights: Human 50, others 1 (≈2.38%).
    /// Stat arrays are [Health, Speed, Endurance, Strength, Dexterity, AttackSpeed, Defense,
    /// Intelligence, Wisdom, Faith, Luck].
    /// </summary>
    public static List<RaceData> BuildDefaultRoster()
    {
        var list = new List<RaceData>();

        list.Add(Make("human", "Human",
            new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 50f,
            15f, new float[] { 15, 15, 15, 15, 15, 15 },
            "human", "+15% XP from all sources", 1f, 0f, Color.white));

        list.Add(Make("fire_giant", "Fire Giant",
            new float[] { 20, -5, 15, 20, 0, -15, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "fire_giant", "Fire resistance (50%), lava walk", 1.25f, 0f, new Color(1f, 0.5f, 0.2f)));

        list.Add(Make("serpent_kin", "Serpent-kin",
            new float[] { 0, 0, 10, -10, 15, 5, 0, 0, 0, 0, 15 }, 1f,
            0f, new float[] { 0, 0, 10, 10, 0, 0 },
            "venom_blade", "Physical attacks apply venom DoT for 8s", 0.95f, 0f, new Color(0.3f, 0.8f, 0.3f)));

        list.Add(Make("draconic", "Draconic",
            new float[] { 0, 0, 5, 20, 0, 5, 0, -10, 15, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 10, 0, 5 },
            "draconic", "Fire resistance (40%), Dragon Roar (stagger, 30s CD)", 1.15f, 0f, new Color(0.8f, 0.2f, 0.2f)));

        list.Add(Make("golem", "Golem",
            new float[] { 15, -10, 25, 25, 0, -15, 0, -20, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 20 },
            "stone_skin", "Stone Skin: 25% physical + 25% magic dmg reduction; -20% move speed", 1.3f, 0f, new Color(0.5f, 0.5f, 0.55f)));

        list.Add(Make("celestial", "Celestial",
            new float[] { 10, 0, 0, -10, 0, 5, 0, 10, 0, 25, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "celestial", "Healing miracles 20% stronger", 1.05f, 0f, new Color(1f, 0.95f, 0.7f)));

        list.Add(Make("wraith", "Wraith",
            new float[] { -15, 0, 0, 0, 0, 5, 0, 10, 25, 0, 15 }, 1f,
            0f, new float[] { 0, 0, 15, 0, 0, 0 },
            "immaterial", "Immaterial: pass through objects, immune physical, spell-caster only; no dash/run. +30% magic, +50% holy dmg", 1f, 0f, new Color(0.5f, 0.9f, 1f)));

        list.Add(Make("undead", "Undead",
            new float[] { 10, 0, 15, 0, 10, 5, 0, -10, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "undead", "Infinite stamina. +25% fire & +25% holy dmg", 1f, 0f, new Color(0.6f, 0.9f, 0.7f)));

        list.Add(Make("skeleton", "Skeleton",
            new float[] { -15, 0, 10, 10, 20, 10, 0, 0, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "skeleton", "Bleed immune, +20% move speed, infinite stamina", 0.9f, 0f, new Color(0.85f, 0.85f, 0.8f)));

        list.Add(Make("werewolf", "Werewolf",
            new float[] { 0, 0, 5, 20, 20, 10, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 15, 0, 0, 0, 0, 0 },
            "werewolf", "Night: +25% move speed + 2% HP regen/s. Claws deal bleed", 1.1f, 0f, new Color(0.6f, 0.6f, 0.7f)));

        list.Add(Make("goblin", "Goblin",
            new float[] { 0, 0, 5, -10, 20, 5, 0, 0, 0, 0, 15 }, 1f,
            0f, new float[] { 0, 0, 0, 10, 15, 0 },
            "goblin", "+20% loot quality, 15% smaller hitbox", 0.8f, 0f, new Color(0.4f, 0.9f, 0.4f)));

        list.Add(Make("orc", "Orc",
            new float[] { 15, 0, 10, 25, 0, 5, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 15, 0, 0, 0, 0, 10 },
            "orc", "+15% stagger damage, passive HP regen (1% max HP/s)", 1.2f, 0f, new Color(0.4f, 0.7f, 0.4f)));

        list.Add(Make("ice_giant", "Ice Giant",
            new float[] { 15, -5, 20, 20, 0, -15, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 10, 0, 10 },
            "ice_giant", "Cold immune, freeze aura (nearby enemies slowed 20%)", 1.3f, 0f, new Color(0.5f, 0.8f, 1f)));

        list.Add(Make("vampire", "Vampire",
            new float[] { 0, 0, 0, -10, 20, 10, 0, 5, 10, 0, 10 }, 1f,
            0f, new float[] { 0, 0, 10, 0, 0, 10 },
            "vampire", "5% lifesteal on hit, +15% move speed. Sunlight: 5% max HP burn/s", 1f, 0f, new Color(0.6f, 0.2f, 0.2f)));

        list.Add(Make("demonkin", "Demonkin",
            new float[] { 0, 0, 10, 20, 0, 5, 0, 0, 15, -15, 0 }, 1f,
            0f, new float[] { 10, 0, 10, 0, 0, 0 },
            "demonkin", "Fire resistance (40%), fire aura (1% max HP/s to nearby)", 1.1f, 0f, new Color(0.8f, 0.3f, 0.1f)));

        list.Add(Make("angel", "Angel",
            new float[] { 0, 0, 0, -10, 0, 5, 0, 15, 10, 25, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "angel", "Elemental resist (20%) via gear; weak to physical (+15%) and dark (+25%)", 1.05f, 0f, new Color(0.95f, 0.9f, 1f)));

        list.Add(Make("succubus", "Succubus/Incubus",
            new float[] { -10, 0, 0, -15, 15, 10, 0, 10, 10, 0, 10 }, 1f,
            0f, new float[] { 0, 0, 15, 0, 0, 0 },
            "charm_gaze", "Charm Gaze: 10% chance opposite-gender target confused", 0.95f, 0f, new Color(0.9f, 0.4f, 0.8f)));

        list.Add(Make("fishmen", "Fishmen",
            new float[] { 15, 0, 15, 10, 10, 5, 0, 0, 0, -10, 0 }, 1f,
            0f, new float[] { 0, 10, 0, 0, 10, 0 },
            "fishmen", "Swim speed +50%, breathe underwater, water dmg immune", 1f, 0f, new Color(0.3f, 0.7f, 0.9f)));

        list.Add(Make("harpy", "Harpy",
            new float[] { 0, 0, -20, -15, 25, 10, 0, 0, 0, 0, 15 }, 1f,
            0f, new float[] { 0, 15, 0, 0, 0, 0 },
            "harpy", "Glide (slow fall), jump height +30%", 0.9f, 0f, new Color(0.8f, 0.6f, 0.6f)));

        list.Add(Make("dwarf", "Dwarf",
            new float[] { 0, 0, 25, 15, -10, -5, 0, 0, 0, 10, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 15, 10 },
            "dwarf", "+20% crafting yield, forge discounts", 0.85f, 0f, new Color(0.9f, 0.7f, 0.4f)));

        list.Add(Make("gnome", "Gnome",
            new float[] { -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, 35 }, 1f,
            0f, new float[] { 0, 0, 15, 0, 10, 0 },
            "lucky_find", "Lucky Find: +40% loot bonus, 15% smaller hitbox", 0.7f, 0f, new Color(0.8f, 0.3f, 0.7f)));

        list.Add(Make("elf", "Elf",
            new float[] { 0, 0, 0, -10, 20, 10, 0, 0, 15, 0, 0 }, 1f,
            8f, new float[] { 0, 10, 10, 0, 0, 0 },
            "elf", "+8% all XP, enhanced perception (see hidden +20% range)", 0.95f, 0f, new Color(0.6f, 0.8f, 0.6f)));

        return list;
    }

    private static RaceData Make(string id, string name, float[] stats, float weight,
        float xpAll, float[] xpSkills, string passive, string passiveDesc,
        float scale, float offset, Color tint)
    {
        var r = ScriptableObject.CreateInstance<RaceData>();
        r.raceId = id;
        r.displayName = name;
        r.StatModifiers = stats;
        r.Weight = weight;
        r.XpBonusAll = xpAll;
        r.XpBonuses = xpSkills;
        r.PassiveId = passive;
        r.PassiveDescription = passiveDesc;
        r.RigScale = scale;
        r.RigOffset = offset;
        r.RigTint = tint;
        return r;
    }
}