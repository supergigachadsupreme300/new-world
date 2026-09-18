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
    private static List<RaceData> _defaultRoster;
    private static readonly Dictionary<string, RaceData> _byId =
        new Dictionary<string, RaceData>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The default Human baseline race (always unlocked, +15% all XP).</summary>
    public static RaceData Human => _human;

    /// <summary>
    /// Cached copy of the synthesized 22-race lineup. Built once and reused so runtime lookups
    /// (<see cref="RaceChangeManager.ActiveRace"/>, model coloring) never re-instantiate 22
    /// ScriptableObjects per call.
    /// </summary>
    public static List<RaceData> DefaultRoster
    {
        get
        {
            if (_defaultRoster == null)
                _defaultRoster = BuildDefaultRoster();
            return _defaultRoster;
        }
    }

    /// <summary>Block-model appearance data: full palette + body-ratio knobs (§3.5 Race Visuals).</summary>
    private struct Look
    {
        public Color Skin, Hair, Eye, Cloth, Pants, Shoes;
        public float Height, Bulk, Head, Shoulder, Arm, Leg;
    }

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
            "human", "+15% XP from all sources", 1f, 0f, Color.white,
            new Look
            {
                Skin = new Color(220f / 255f, 178f / 255f, 132f / 255f),
                Hair = new Color(0.2f, 0.12f, 0.05f),
                Eye = new Color(0.05f, 0.03f, 0.01f),
                Cloth = new Color(0.2f, 0.6f, 0.9f),
                Pants = new Color(0.25f, 0.25f, 0.35f),
                Shoes = new Color(0.2f, 0.2f, 0.2f),
                Height = 1f, Bulk = 1f, Head = 1f, Shoulder = 1f, Arm = 1f, Leg = 1f
            }));

        list.Add(Make("fire_giant", "Fire Giant",
            new float[] { 20, -5, 15, 20, 0, -15, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "fire_giant", "Fire resistance (50%), lava walk", 1.35f, 0f, new Color(1f, 0.5f, 0.2f),
            new Look
            {
                Skin = new Color(0.9f, 0.34f, 0.12f),
                Hair = new Color(1f, 0.6f, 0.12f),
                Eye = new Color(1f, 0.85f, 0.2f),
                Cloth = new Color(0.4f, 0.18f, 0.08f),
                Pants = new Color(0.32f, 0.14f, 0.08f),
                Shoes = new Color(0.25f, 0.1f, 0.06f),
                Height = 1.12f, Bulk = 1.3f, Head = 1.1f, Shoulder = 1.2f, Arm = 1.08f, Leg = 1.05f
            }));

        list.Add(Make("serpent_kin", "Serpent-kin",
            new float[] { 0, 0, 10, -10, 15, 5, 0, 0, 0, 0, 15 }, 1f,
            0f, new float[] { 0, 0, 10, 10, 0, 0 },
            "venom_blade", "Physical attacks apply venom DoT for 8s", 0.95f, 0f, new Color(0.3f, 0.8f, 0.3f),
            new Look
            {
                Skin = new Color(0.42f, 0.8f, 0.3f),
                Hair = new Color(0.08f, 0.28f, 0.12f),
                Eye = new Color(1f, 0.7f, 0.1f),
                Cloth = new Color(0.2f, 0.45f, 0.32f),
                Pants = new Color(0.18f, 0.5f, 0.38f),
                Shoes = new Color(0.32f, 0.38f, 0.22f),
                Height = 1.02f, Bulk = 0.9f, Head = 0.98f, Shoulder = 0.9f, Arm = 1.04f, Leg = 1.06f
            }));

        list.Add(Make("draconic", "Draconic",
            new float[] { 0, 0, 5, 20, 0, 5, 0, -10, 15, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 10, 0, 5 },
            "draconic", "Fire resistance (40%), Dragon Roar (stagger, 30s CD)", 1.2f, 0f, new Color(0.8f, 0.2f, 0.2f),
            new Look
            {
                Skin = new Color(0.76f, 0.22f, 0.12f),
                Hair = new Color(0.1f, 0.08f, 0.08f),
                Eye = new Color(1f, 0.6f, 0.1f),
                Cloth = new Color(0.42f, 0.14f, 0.1f),
                Pants = new Color(0.46f, 0.16f, 0.12f),
                Shoes = new Color(0.32f, 0.12f, 0.08f),
                Height = 1.1f, Bulk = 1.15f, Head = 1.12f, Shoulder = 1.15f, Arm = 1.1f, Leg = 1.06f
            }));

        list.Add(Make("golem", "Golem",
            new float[] { 15, -10, 25, 25, 0, -15, 0, -20, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 20 },
            "stone_skin", "Stone Skin: 25% physical + 25% magic dmg reduction; -20% move speed", 1.4f, 0f, new Color(0.5f, 0.5f, 0.55f),
            new Look
            {
                Skin = new Color(0.56f, 0.56f, 0.62f),
                Hair = new Color(0.36f, 0.36f, 0.42f),
                Eye = new Color(0.4f, 0.8f, 1f),
                Cloth = new Color(0.46f, 0.46f, 0.52f),
                Pants = new Color(0.52f, 0.52f, 0.58f),
                Shoes = new Color(0.44f, 0.44f, 0.5f),
                Height = 1.1f, Bulk = 1.4f, Head = 1.15f, Shoulder = 1.3f, Arm = 0.98f, Leg = 0.95f
            }));

        list.Add(Make("celestial", "Celestial",
            new float[] { 10, 0, 0, -10, 0, 5, 0, 10, 0, 25, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "celestial", "Healing miracles 20% stronger", 1.05f, 0f, new Color(1f, 0.95f, 0.7f),
            new Look
            {
                Skin = new Color(0.98f, 0.93f, 0.85f),
                Hair = new Color(1f, 0.92f, 0.55f),
                Eye = new Color(0.55f, 0.8f, 1f),
                Cloth = new Color(0.95f, 0.9f, 0.7f),
                Pants = new Color(0.6f, 0.55f, 0.8f),
                Shoes = new Color(0.9f, 0.85f, 0.65f),
                Height = 1.05f, Bulk = 1f, Head = 1f, Shoulder = 1f, Arm = 1.05f, Leg = 1.06f
            }));

        list.Add(Make("wraith", "Wraith",
            new float[] { -15, 0, 0, 0, 0, 5, 0, 10, 25, 0, 15 }, 1f,
            0f, new float[] { 0, 0, 15, 0, 0, 0 },
            "immaterial", "Immaterial: pass through objects, immune physical, spell-caster only; no dash/run. +30% magic, +50% holy dmg", 1f, 0f, new Color(0.5f, 0.9f, 1f),
            new Look
            {
                Skin = new Color(0.6f, 0.75f, 0.85f),
                Hair = new Color(0.85f, 0.95f, 1f),
                Eye = new Color(0.2f, 0.9f, 1f),
                Cloth = new Color(0.16f, 0.32f, 0.45f),
                Pants = new Color(0.1f, 0.22f, 0.32f),
                Shoes = new Color(0.06f, 0.16f, 0.26f),
                Height = 1.06f, Bulk = 0.9f, Head = 0.98f, Shoulder = 0.9f, Arm = 1.06f, Leg = 1.08f
            }));

        list.Add(Make("undead", "Undead",
            new float[] { 10, 0, 15, 0, 10, 5, 0, -10, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "undead", "Infinite stamina. +25% fire & +25% holy dmg", 1f, 0f, new Color(0.6f, 0.9f, 0.7f),
            new Look
            {
                Skin = new Color(0.56f, 0.68f, 0.42f),
                Hair = new Color(0.12f, 0.12f, 0.1f),
                Eye = new Color(0.8f, 0.85f, 0.5f),
                Cloth = new Color(0.38f, 0.32f, 0.26f),
                Pants = new Color(0.28f, 0.24f, 0.2f),
                Shoes = new Color(0.24f, 0.2f, 0.18f),
                Height = 1f, Bulk = 1.02f, Head = 1.02f, Shoulder = 1.02f, Arm = 1f, Leg = 1f
            }));

        list.Add(Make("skeleton", "Skeleton",
            new float[] { -15, 0, 10, 10, 20, 10, 0, 0, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "skeleton", "Bleed immune, +20% move speed, infinite stamina", 0.9f, 0f, new Color(0.85f, 0.85f, 0.8f),
            new Look
            {
                Skin = new Color(0.9f, 0.88f, 0.8f),
                Hair = new Color(0.62f, 0.58f, 0.52f),
                Eye = new Color(0.3f, 0.3f, 0.34f),
                Cloth = new Color(0.42f, 0.38f, 0.32f),
                Pants = new Color(0.3f, 0.28f, 0.24f),
                Shoes = new Color(0.26f, 0.24f, 0.2f),
                Height = 1f, Bulk = 0.85f, Head = 1f, Shoulder = 0.88f, Arm = 0.98f, Leg = 1.04f
            }));

        list.Add(Make("werewolf", "Werewolf",
            new float[] { 0, 0, 5, 20, 20, 10, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 15, 0, 0, 0, 0, 0 },
            "werewolf", "Night: +25% move speed + 2% HP regen/s. Claws deal bleed", 1.1f, 0f, new Color(0.6f, 0.6f, 0.7f),
            new Look
            {
                Skin = new Color(0.58f, 0.46f, 0.38f),
                Hair = new Color(0.28f, 0.2f, 0.14f),
                Eye = new Color(1f, 0.9f, 0.3f),
                Cloth = new Color(0.32f, 0.28f, 0.36f),
                Pants = new Color(0.34f, 0.3f, 0.38f),
                Shoes = new Color(0.26f, 0.2f, 0.26f),
                Height = 1.06f, Bulk = 1.25f, Head = 1.02f, Shoulder = 1.15f, Arm = 1.08f, Leg = 1.1f
            }));

        list.Add(Make("goblin", "Goblin",
            new float[] { 0, 0, 5, -10, 20, 5, 0, 0, 0, 0, 15 }, 1f,
            0f, new float[] { 0, 0, 0, 10, 15, 0 },
            "goblin", "+20% loot quality, 15% smaller hitbox", 0.8f, 0f, new Color(0.4f, 0.9f, 0.4f),
            new Look
            {
                Skin = new Color(0.45f, 0.85f, 0.35f),
                Hair = new Color(0.15f, 0.15f, 0.12f),
                Eye = new Color(0.95f, 0.22f, 0.18f),
                Cloth = new Color(0.5f, 0.42f, 0.2f),
                Pants = new Color(0.45f, 0.35f, 0.18f),
                Shoes = new Color(0.35f, 0.25f, 0.15f),
                Height = 0.88f, Bulk = 0.95f, Head = 1.25f, Shoulder = 0.95f, Arm = 0.92f, Leg = 0.88f
            }));

        list.Add(Make("orc", "Orc",
            new float[] { 15, 0, 10, 25, 0, 5, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 15, 0, 0, 0, 0, 10 },
            "orc", "+15% stagger damage, passive HP regen (1% max HP/s)", 1.2f, 0f, new Color(0.4f, 0.7f, 0.4f),
            new Look
            {
                Skin = new Color(0.45f, 0.7f, 0.42f),
                Hair = new Color(0.12f, 0.1f, 0.08f),
                Eye = new Color(1f, 0.75f, 0.2f),
                Cloth = new Color(0.32f, 0.38f, 0.32f),
                Pants = new Color(0.38f, 0.44f, 0.38f),
                Shoes = new Color(0.32f, 0.28f, 0.22f),
                Height = 1.08f, Bulk = 1.35f, Head = 1.12f, Shoulder = 1.35f, Arm = 1.1f, Leg = 1.02f
            }));

        list.Add(Make("ice_giant", "Ice Giant",
            new float[] { 15, -5, 20, 20, 0, -15, 0, -15, 0, 0, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 10, 0, 10 },
            "ice_giant", "Cold immune, freeze aura (nearby enemies slowed 20%)", 1.4f, 0f, new Color(0.5f, 0.8f, 1f),
            new Look
            {
                Skin = new Color(0.6f, 0.78f, 0.95f),
                Hair = new Color(0.82f, 0.92f, 1f),
                Eye = new Color(0.75f, 0.97f, 1f),
                Cloth = new Color(0.18f, 0.34f, 0.5f),
                Pants = new Color(0.14f, 0.28f, 0.44f),
                Shoes = new Color(0.12f, 0.22f, 0.38f),
                Height = 1.18f, Bulk = 1.35f, Head = 1.1f, Shoulder = 1.25f, Arm = 1.1f, Leg = 1.05f
            }));

        list.Add(Make("vampire", "Vampire",
            new float[] { 0, 0, 0, -10, 20, 10, 0, 5, 10, 0, 10 }, 1f,
            0f, new float[] { 0, 0, 10, 0, 0, 10 },
            "vampire", "5% lifesteal on hit, +15% move speed. Sunlight: 5% max HP burn/s", 1f, 0f, new Color(0.6f, 0.2f, 0.2f),
            new Look
            {
                Skin = new Color(0.88f, 0.7f, 0.72f),
                Hair = new Color(0.12f, 0.12f, 0.15f),
                Eye = new Color(0.9f, 0.15f, 0.12f),
                Cloth = new Color(0.42f, 0.12f, 0.14f),
                Pants = new Color(0.32f, 0.1f, 0.12f),
                Shoes = new Color(0.14f, 0.08f, 0.1f),
                Height = 1.04f, Bulk = 0.88f, Head = 0.98f, Shoulder = 0.92f, Arm = 1.05f, Leg = 1.06f
            }));

        list.Add(Make("demonkin", "Demonkin",
            new float[] { 0, 0, 10, 20, 0, 5, 0, 0, 15, -15, 0 }, 1f,
            0f, new float[] { 10, 0, 10, 0, 0, 0 },
            "demonkin", "Fire resistance (40%), fire aura (1% max HP/s to nearby)", 1.1f, 0f, new Color(0.8f, 0.3f, 0.1f),
            new Look
            {
                Skin = new Color(0.82f, 0.36f, 0.18f),
                Hair = new Color(0.1f, 0.08f, 0.08f),
                Eye = new Color(1f, 0.55f, 0.1f),
                Cloth = new Color(0.48f, 0.16f, 0.12f),
                Pants = new Color(0.38f, 0.13f, 0.1f),
                Shoes = new Color(0.32f, 0.1f, 0.08f),
                Height = 1.08f, Bulk = 1.15f, Head = 1.05f, Shoulder = 1.15f, Arm = 1.06f, Leg = 1.02f
            }));

        list.Add(Make("angel", "Angel",
            new float[] { 0, 0, 0, -10, 0, 5, 0, 15, 10, 25, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 0, 15 },
            "angel", "Elemental resist (20%) via gear; weak to physical (+15%) and dark (+25%)", 1.05f, 0f, new Color(0.95f, 0.9f, 1f),
            new Look
            {
                Skin = new Color(0.97f, 0.93f, 0.9f),
                Hair = new Color(1f, 0.9f, 0.5f),
                Eye = new Color(0.5f, 0.85f, 1f),
                Cloth = new Color(0.95f, 0.93f, 0.8f),
                Pants = new Color(0.92f, 0.88f, 0.75f),
                Shoes = new Color(0.95f, 0.92f, 0.85f),
                Height = 1.05f, Bulk = 1f, Head = 1f, Shoulder = 1f, Arm = 1.04f, Leg = 1.05f
            }));

        list.Add(Make("succubus", "Succubus/Incubus",
            new float[] { -10, 0, 0, -15, 15, 10, 0, 10, 10, 0, 10 }, 1f,
            0f, new float[] { 0, 0, 15, 0, 0, 0 },
            "charm_gaze", "Charm Gaze: 10% chance opposite-gender target confused", 0.95f, 0f, new Color(0.9f, 0.4f, 0.8f),
            new Look
            {
                Skin = new Color(0.72f, 0.45f, 0.75f),
                Hair = new Color(0.32f, 0.16f, 0.42f),
                Eye = new Color(0.85f, 0.3f, 0.9f),
                Cloth = new Color(0.38f, 0.16f, 0.44f),
                Pants = new Color(0.3f, 0.13f, 0.36f),
                Shoes = new Color(0.26f, 0.1f, 0.3f),
                Height = 1.03f, Bulk = 0.9f, Head = 0.98f, Shoulder = 0.9f, Arm = 1.05f, Leg = 1.06f
            }));

        list.Add(Make("fishmen", "Fishmen",
            new float[] { 15, 0, 15, 10, 10, 5, 0, 0, 0, -10, 0 }, 1f,
            0f, new float[] { 0, 10, 0, 0, 10, 0 },
            "fishmen", "Swim speed +50%, breathe underwater, water dmg immune", 1f, 0f, new Color(0.3f, 0.7f, 0.9f),
            new Look
            {
                Skin = new Color(0.36f, 0.72f, 0.78f),
                Hair = new Color(0.22f, 0.36f, 0.42f),
                Eye = new Color(0.2f, 0.26f, 0.36f),
                Cloth = new Color(0.32f, 0.52f, 0.55f),
                Pants = new Color(0.26f, 0.44f, 0.5f),
                Shoes = new Color(0.2f, 0.3f, 0.36f),
                Height = 1f, Bulk = 1.12f, Head = 1.22f, Shoulder = 1.05f, Arm = 0.98f, Leg = 0.95f
            }));

        list.Add(Make("harpy", "Harpy",
            new float[] { 0, 0, -20, -15, 25, 10, 0, 0, 0, 0, 15 }, 1f,
            0f, new float[] { 0, 15, 0, 0, 0, 0 },
            "harpy", "Glide (slow fall), jump height +30%", 0.9f, 0f, new Color(0.8f, 0.6f, 0.6f),
            new Look
            {
                Skin = new Color(0.85f, 0.82f, 0.78f),
                Hair = new Color(0.72f, 0.62f, 0.55f),
                Eye = new Color(0.9f, 0.55f, 0.4f),
                Cloth = new Color(0.78f, 0.62f, 0.5f),
                Pants = new Color(0.4f, 0.35f, 0.38f),
                Shoes = new Color(0.3f, 0.28f, 0.32f),
                Height = 0.96f, Bulk = 0.85f, Head = 0.98f, Shoulder = 0.8f, Arm = 0.92f, Leg = 0.88f
            }));

        list.Add(Make("dwarf", "Dwarf",
            new float[] { 0, 0, 25, 15, -10, -5, 0, 0, 0, 10, 0 }, 1f,
            0f, new float[] { 0, 0, 0, 0, 15, 10 },
            "dwarf", "+20% crafting yield, forge discounts", 0.85f, 0f, new Color(0.9f, 0.7f, 0.4f),
            new Look
            {
                Skin = new Color(0.82f, 0.55f, 0.4f),
                Hair = new Color(0.38f, 0.22f, 0.13f),
                Eye = new Color(0.42f, 0.32f, 0.22f),
                Cloth = new Color(0.58f, 0.38f, 0.26f),
                Pants = new Color(0.5f, 0.32f, 0.22f),
                Shoes = new Color(0.32f, 0.2f, 0.15f),
                Height = 0.85f, Bulk = 1.25f, Head = 1.15f, Shoulder = 1.15f, Arm = 0.85f, Leg = 0.8f
            }));

        list.Add(Make("gnome", "Gnome",
            new float[] { -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, 35 }, 1f,
            0f, new float[] { 0, 0, 15, 0, 10, 0 },
            "lucky_find", "Lucky Find: +40% loot bonus, 15% smaller hitbox", 0.7f, 0f, new Color(0.8f, 0.3f, 0.7f),
            new Look
            {
                Skin = new Color(0.93f, 0.8f, 0.65f),
                Hair = new Color(0.78f, 0.52f, 0.32f),
                Eye = new Color(0.35f, 0.55f, 0.85f),
                Cloth = new Color(0.62f, 0.32f, 0.52f),
                Pants = new Color(0.55f, 0.32f, 0.45f),
                Shoes = new Color(0.42f, 0.26f, 0.32f),
                Height = 0.85f, Bulk = 0.95f, Head = 1.3f, Shoulder = 0.95f, Arm = 0.85f, Leg = 0.8f
            }));

        list.Add(Make("elf", "Elf",
            new float[] { 0, 0, 0, -10, 20, 10, 0, 0, 15, 0, 0 }, 1f,
            8f, new float[] { 0, 10, 10, 0, 0, 0 },
            "elf", "+8% all XP, enhanced perception (see hidden +20% range)", 0.95f, 0f, new Color(0.6f, 0.8f, 0.6f),
            new Look
            {
                Skin = new Color(0.94f, 0.84f, 0.7f),
                Hair = new Color(0.92f, 0.76f, 0.42f),
                Eye = new Color(0.2f, 0.6f, 0.5f),
                Cloth = new Color(0.38f, 0.58f, 0.36f),
                Pants = new Color(0.42f, 0.54f, 0.34f),
                Shoes = new Color(0.32f, 0.38f, 0.26f),
                Height = 1.08f, Bulk = 0.88f, Head = 0.98f, Shoulder = 0.88f, Arm = 1.1f, Leg = 1.12f
            }));

        return list;
    }

    private static RaceData Make(string id, string name, float[] stats, float weight,
        float xpAll, float[] xpSkills, string passive, string passiveDesc,
        float scale, float offset, Color tint, Look look)
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
        r.SkinColor = look.Skin;
        r.HairColor = look.Hair;
        r.EyeColor = look.Eye;
        r.ClothColor = look.Cloth;
        r.PantsColor = look.Pants;
        r.ShoeColor = look.Shoes;
        r.BodyHeight = look.Height;
        r.BodyBulk = look.Bulk;
        r.BodyHead = look.Head;
        r.BodyShoulderWidth = look.Shoulder;
        r.BodyArm = look.Arm;
        r.BodyLeg = look.Leg;
        return r;
    }
}