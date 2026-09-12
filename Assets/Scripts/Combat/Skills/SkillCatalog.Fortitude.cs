using System.Collections.Generic;

public static partial class SkillCatalog
{
    private static void RegisterFortitudeDesign(DesignBank bank)
    {
        /* ──────────────── L1 (30 slots — 6 roots × 5 children each) ──────────────── */

        // Root: fort_health (passive, Health+4)
        bank.L1["fort_health"] = new BranchSlot[]
        {
            A("fort_vitality"),
            S("fort_health_meat", "Meat Shield", Buff(StatType.Health, 3f), "Permanent +3 Health.", passive: true),
            S("fort_health_brawn", "Brawn", Buff(StatType.Strength, 3f), "Permanent +3 Strength.", passive: true),
            S("fort_health_lionheart", "Lionheart", Buff(StatType.Health, 3f), "Permanent +3 Health.", passive: true),
            S("fort_health_regenerate", "Regenerate", Buff(StatType.Health, 2f), "Permanent +2 Health.", passive: true),
        };

        // Root: fort_armor (passive, Defense+4)
        bank.L1["fort_armor"] = new BranchSlot[]
        {
            A("fort_stamina"),
            A("fort_steadfast"),
            S("fort_armor_steelskin", "Steel Skin", Buff(StatType.Defense, 3f), "Permanent +3 Defense.", passive: true),
            S("fort_armor_ironwall", "Iron Wall", Buff(StatType.Defense, 3f), "Permanent +3 Defense.", passive: true),
            S("fort_armor_shield", "Shielded", Buff(StatType.Endurance, 3f), "Permanent +3 Endurance.", passive: true),
        };

        // Root: fort_recovery (passive, Health+2)
        bank.L1["fort_recovery"] = new BranchSlot[]
        {
            S("fort_recovery_regen", "Regen", Buff(StatType.Health, 3f), "Permanent +3 Health.", passive: true),
            S("fort_recovery_reclaim", "Reclaim", Buff(StatType.Endurance, 3f), "Permanent +3 Endurance.", passive: true),
            S("fort_recovery_revive", "Revive", Buff(StatType.Health, 2f), "Permanent +2 Health.", passive: true),
            S("fort_recovery_hardened", "Hardened Recovery", Buff(StatType.Endurance, 2f), "Permanent +2 Endurance.", passive: true),
            S("fort_recovery_woundmend", "Wound Mending", Buff(StatType.Health, 2f), "Permanent +2 Health.", passive: true),
        };

        // Root: fort_bulwark (passive, Health+3)
        bank.L1["fort_bulwark"] = new BranchSlot[]
        {
            S("fort_bulwark_stand", "Stand Guard", Buff(StatType.Defense, 3f), "Permanent +3 Defense.", passive: true),
            S("fort_bulwark_solid", "Solid", Buff(StatType.Health, 3f), "Permanent +3 Health.", passive: true),
            S("fort_bulwark_staunch", "Staunch", Buff(StatType.Endurance, 3f), "Permanent +3 Endurance.", passive: true),
            S("fort_bulwark_breachless", "Breachless", Buff(StatType.Defense, 3f), "Permanent +3 Defense.", passive: true),
            S("fort_bulwark_rampart", "Rampart", Buff(StatType.Health, 3f), "Permanent +3 Health.", passive: true),
        };

        // Root: fort_stoneskin (active, Focus 12, Zone(2,14,Earth))
        bank.L1["fort_stoneskin"] = new BranchSlot[]
        {
            S("fort_stoneskin_granite", "Granite", Slash(18f, DamageType.Earth), "Fists hardened to granite.", Focus(14f), DamageType.Earth, true),
            S("fort_stoneskin_boulder", "Boulder Charge", Slash(18f, DamageType.Physical), "A boulder of a shoulder charge.", Stamina(12f)),
            S("fort_stoneskin_earthcrash", "Earth Crash", Zone(2.2f, 16f, DamageType.Earth), "Crash the earth around you.", Focus(14f), DamageType.Earth, true),
            S("fort_stoneskin_mountain", "Mountain", Buff(StatType.Health, 3f), "Permanent +3 Health.", passive: true),
            S("fort_stoneskin_pebble", "Pebble Wall", Zone(2f, 14f, DamageType.Earth), "A wall of pebbles driven outward.", Focus(12f), DamageType.Earth, true),
        };

        // Root: fort_guro (active, Stamina 10, Slash(14,Physical))
        bank.L1["fort_guro"] = new BranchSlot[]
        {
            S("fort_guro_headbutt", "Headbutt", Slash(16f, DamageType.Physical), "A solid, skull-first strike.", Stamina(12f)),
            S("fort_guro_warcry", "War Cry", Zone(1.8f, 14f, DamageType.Physical), "A cry that shoves the air outward.", Stamina(12f)),
            S("fort_guro_unyielding", "Unyielding", Buff(StatType.Endurance, 3f), "Permanent +3 Endurance.", passive: true),
            S("fort_guro_ironcharge", "Iron Charge", Slash(18f, DamageType.Physical), "A pseudo-iron charge of a strike.", Stamina(14f)),
            S("fort_guro_juggernaut", "Juggernaut", Buff(StatType.Defense, 3f), "Permanent +3 Defense.", passive: true),
        };

        /* ──────────────── L2 (150 slots — 30 L1 parents × 5 children each) ──────────────── */

        /* fort_vitality children (all passive) */
        bank.L2["fort_vitality"] = new BranchSlot[]
        {
            S("fort_vitality_vim", "Vim", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_vitality_pep", "Pep", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_vitality_vigor", "Vigor", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_vitality_zest", "Zest", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("fort_vitality_flourish", "Flourish", Buff(StatType.Health, 4f), "Permanent +4 Health.", passive: true),
        };

        /* fort_stamina children (all passive) */
        bank.L2["fort_stamina"] = new BranchSlot[]
        {
            S("fort_stamina_grit", "Grit", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_stamina_last", "Lasting", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_stamina_well", "Stamina Well", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_stamina_drive", "Drive", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_stamina_plod", "Plod", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
        };

        /* fort_steadfast children (all passive) */
        bank.L2["fort_steadfast"] = new BranchSlot[]
        {
            S("fort_steadfast_rocksteady", "Rocksteady", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_steadfast_unshakable", "Unshakable", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_steadfast_resolute", "Resolute", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_steadfast_fixed", "Fixed", Buff(StatType.Defense, 4f), "Permanent +4 Defense.", passive: true),
            S("fort_steadfast_stalwart", "Stalwart", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
        };

        /* fort_health_meat children (all passive) */
        bank.L2["fort_health_meat"] = new BranchSlot[]
        {
            S("fort_health_meat_health", "Meat Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_health_meat_defense", "Meat Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_health_meat_endurance", "Meat Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_health_meat_strength", "Meat Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_health_meat_attackspeed", "Meat Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
        };

        /* fort_health_brawn children (all passive) */
        bank.L2["fort_health_brawn"] = new BranchSlot[]
        {
            S("fort_health_brawn_strength", "Heavy Brawn", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_health_brawn_health", "Brawn Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_health_brawn_defense", "Brawn Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_health_brawn_endurance", "Brawn Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_health_brawn_attackspeed", "Brawn Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
        };

        /* fort_health_lionheart children (all passive) */
        bank.L2["fort_health_lionheart"] = new BranchSlot[]
        {
            S("fort_health_lionheart_courage", "Courage", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_health_lionheart_lion", "Lion's Heart", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_health_lionheart_brave", "Brave", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_health_lionheart_roar", "Lion's Roar", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_health_lionheart_pride", "Pride", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };

        /* fort_health_regenerate children (all passive) */
        bank.L2["fort_health_regenerate"] = new BranchSlot[]
        {
            S("fort_health_regenerate_health", "Regen Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_health_regenerate_endurance", "Regen Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_health_regenerate_defense", "Regen Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_health_regenerate_speed", "Regen Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_health_regenerate_will", "Regen Will", Buff(StatType.Health, 4f), "Permanent +4 Health.", passive: true),
        };

        /* fort_armor_steelskin children (all passive) */
        bank.L2["fort_armor_steelskin"] = new BranchSlot[]
        {
            S("fort_armor_steelskin_defense", "Steel Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_armor_steelskin_endurance", "Steel Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_armor_steelskin_health", "Steel Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_armor_steelskin_strength", "Steel Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_armor_steelskin_reflex", "Steel Reflex", Buff(StatType.Defense, 4f), "Permanent +4 Defense.", passive: true),
        };

        /* fort_armor_ironwall children (all passive) */
        bank.L2["fort_armor_ironwall"] = new BranchSlot[]
        {
            S("fort_armor_ironwall_defense", "Iron Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_armor_ironwall_health", "Iron Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_armor_ironwall_endurance", "Iron Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_armor_ironwall_strength", "Iron Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_armor_ironwall_luck", "Iron Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* fort_armor_shield children (all passive) */
        bank.L2["fort_armor_shield"] = new BranchSlot[]
        {
            S("fort_armor_shield_endurance", "Shield Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_armor_shield_defense", "Shield Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_armor_shield_health", "Shield Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_armor_shield_strength", "Shield Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_armor_shield_attackspeed", "Shield Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
        };

        /* fort_recovery_regen children (all passive) */
        bank.L2["fort_recovery_regen"] = new BranchSlot[]
        {
            S("fort_recovery_regen_health", "Regen Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_recovery_regen_endurance", "Regen Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_recovery_regen_speed", "Regen Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_recovery_regen_defense", "Regen Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_recovery_regen_strength", "Regen Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
        };

        /* fort_recovery_reclaim children (all passive) */
        bank.L2["fort_recovery_reclaim"] = new BranchSlot[]
        {
            S("fort_recovery_reclaim_endurance", "Reclaimed Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_recovery_reclaim_health", "Reclaimed Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_recovery_reclaim_speed", "Reclaimed Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_recovery_reclaim_strength", "Reclaimed Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_recovery_reclaim_luck", "Reclaimed Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* fort_recovery_revive children (all passive) */
        bank.L2["fort_recovery_revive"] = new BranchSlot[]
        {
            S("fort_recovery_revive_health", "Revive Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_recovery_revive_endurance", "Revive Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_recovery_revive_defense", "Revive Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_recovery_revive_luck", "Revive Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("fort_recovery_revive_speed", "Revive Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };

        /* fort_recovery_hardened children (all passive) */
        bank.L2["fort_recovery_hardened"] = new BranchSlot[]
        {
            S("fort_recovery_hardened_endurance", "Hardened Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_recovery_hardened_health", "Hardened Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_recovery_hardened_attackspeed", "Hardened Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("fort_recovery_hardened_speed", "Hardened Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_recovery_hardened_defense", "Hardened Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
        };

        /* fort_recovery_woundmend children (all passive) */
        bank.L2["fort_recovery_woundmend"] = new BranchSlot[]
        {
            S("fort_recovery_woundmend_health", "Mend Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_recovery_woundmend_endurance", "Mend Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_recovery_woundmend_defense", "Mend Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_recovery_woundmend_speed", "Mend Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_recovery_woundmend_strength", "Mend Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
        };

        /* fort_bulwark_stand children (all passive) */
        bank.L2["fort_bulwark_stand"] = new BranchSlot[]
        {
            S("fort_bulwark_stand_defense", "Stand Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_bulwark_stand_health", "Stand Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_bulwark_stand_endurance", "Stand Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_bulwark_stand_strength", "Stand Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_bulwark_stand_luck", "Stand Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* fort_bulwark_solid children (all passive) */
        bank.L2["fort_bulwark_solid"] = new BranchSlot[]
        {
            S("fort_bulwark_solid_health", "Solid Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_bulwark_solid_defense", "Solid Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_bulwark_solid_endurance", "Solid Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_bulwark_solid_strength", "Solid Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_bulwark_solid_speed", "Solid Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };

        /* fort_bulwark_staunch children (all passive) */
        bank.L2["fort_bulwark_staunch"] = new BranchSlot[]
        {
            S("fort_bulwark_staunch_endurance", "Staunch Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_bulwark_staunch_health", "Staunch Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_bulwark_staunch_defense", "Staunch Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_bulwark_staunch_speed", "Staunch Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("fort_bulwark_staunch_strength", "Staunch Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
        };

        /* fort_bulwark_breachless children (all passive) */
        bank.L2["fort_bulwark_breachless"] = new BranchSlot[]
        {
            S("fort_bulwark_breachless_defense", "Breachless Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_bulwark_breachless_endurance", "Breachless Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_bulwark_breachless_health", "Breachless Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_bulwark_breachless_strength", "Breachless Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_bulwark_breachless_luck", "Breachless Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* fort_bulwark_rampart children (all passive) */
        bank.L2["fort_bulwark_rampart"] = new BranchSlot[]
        {
            S("fort_bulwark_rampart_health", "Rampart Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_bulwark_rampart_defense", "Rampart Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_bulwark_rampart_endurance", "Rampart Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_bulwark_rampart_strength", "Rampart Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_bulwark_rampart_attackspeed", "Rampart Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
        };

        /* fort_stoneskin_granite children */
        bank.L2["fort_stoneskin_granite"] = new BranchSlot[]
        {
            S("fort_stoneskin_granite_fist", "Granite Fist", Slash(20f, DamageType.Earth), "Fists like living granite.", Stamina(14f), DamageType.Earth, true),
            S("fort_stoneskin_granite_knuckles", "Stone Knuckles", Slash(20f, DamageType.Physical), "Knuckles armored in stone.", Stamina(14f)),
            S("fort_stoneskin_granite_slam", "Rock Slam", Zone(2.2f, 22f, DamageType.Earth), "A slam of solid rock.", Stamina(16f), DamageType.Earth, true),
            S("fort_stoneskin_granite_storm", "Pebble Storm", Zone(2f, 20f, DamageType.Earth), "A storm of flung pebbles and stone.", Stamina(14f), DamageType.Earth, true),
            S("fort_stoneskin_granite_bedrock", "Bedrock", Slash(24f, DamageType.Earth), "A strike with the weight of bedrock.", Stamina(18f), DamageType.Earth, true),
        };

        /* fort_stoneskin_boulder children */
        bank.L2["fort_stoneskin_boulder"] = new BranchSlot[]
        {
            S("fort_stoneskin_boulder_roll", "Boulder Roll", Slash(20f, DamageType.Physical), "A rolling boulder of a charge.", Stamina(14f)),
            S("fort_stoneskin_boulder_charge", "Stone Charge", Slash(20f, DamageType.Earth), "A stony headlong charge.", Stamina(14f), DamageType.Earth, true),
            S("fort_stoneskin_boulder_bear", "Bear Hug", Slash(20f, DamageType.Physical), "A crushing bear of an embrace.", Stamina(14f)),
            S("fort_stoneskin_boulder_trample", "Trample", Zone(2f, 20f, DamageType.Earth), "A trampling of stony feet.", Stamina(14f), DamageType.Earth, true),
            S("fort_stoneskin_boulder_avalanche", "Avalanche", Zone(2.4f, 24f, DamageType.Earth), "A growing avalanche of force.", Stamina(18f), DamageType.Earth, true),
        };

        /* fort_stoneskin_earthcrash children */
        bank.L2["fort_stoneskin_earthcrash"] = new BranchSlot[]
        {
            S("fort_stoneskin_earthcrash_quake", "Earthquake", Zone(2.6f, 24f, DamageType.Earth), "The earth itself quakes.", Stamina(20f), DamageType.Earth, true),
            S("fort_stoneskin_earthcrash_crash", "Rock Crash", Zone(2.2f, 22f, DamageType.Earth), "A crashing wave of rock.", Stamina(16f), DamageType.Earth, true),
            S("fort_stoneskin_earthcrash_seismic", "Seismic Slam", Zone(2.4f, 22f, DamageType.Earth), "A slam that sends seismic waves.", Stamina(18f), DamageType.Earth, true),
            S("fort_stoneskin_earthcrash_crust", "Crust Break", Slash(22f, DamageType.Earth), "A strike that fractures the crust.", Stamina(16f), DamageType.Earth, true),
            S("fort_stoneskin_earthcrash_terra", "Terracrack", Zone(2.4f, 26f, DamageType.Earth), "A cracks of terra that erupt.", Stamina(20f), DamageType.Earth, true),
        };

        /* fort_stoneskin_mountain children (all passive) */
        bank.L2["fort_stoneskin_mountain"] = new BranchSlot[]
        {
            S("fort_stoneskin_mountain_health", "Mountain Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_stoneskin_mountain_defense", "Mountain Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_stoneskin_mountain_strength", "Mountain Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_stoneskin_mountain_endurance", "Mountain Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_stoneskin_mountain_immovable", "Immovable", Buff(StatType.Health, 4f), "Permanent +4 Health.", passive: true),
        };

        /* fort_stoneskin_pebble children */
        bank.L2["fort_stoneskin_pebble"] = new BranchSlot[]
        {
            S("fort_stoneskin_pebble_shot", "Pebble Shot", Slash(18f, DamageType.Earth), "A compressed pebble shot.", Stamina(12f), DamageType.Earth, true),
            S("fort_stoneskin_pebble_blast", "Gravel Blast", Zone(2f, 20f, DamageType.Earth), "A blast of gravel.", Stamina(14f), DamageType.Earth, true),
            S("fort_stoneskin_pebble_dust", "Stone Dust", Zone(2f, 18f, DamageType.Earth), "A cloud of stinging dust.", Stamina(12f), DamageType.Earth, true),
            S("fort_stoneskin_pebble_flint", "Flint Strike", Slash(20f, DamageType.Fire), "A flint-on-stone sparking strike.", Stamina(16f), DamageType.Fire, true),
            S("fort_stoneskin_pebble_rain", "Stone Rain", Zone(2.2f, 20f, DamageType.Earth), "A rain of small stones.", Stamina(14f), DamageType.Earth, true),
        };

        /* fort_guro_headbutt children */
        bank.L2["fort_guro_headbutt"] = new BranchSlot[]
        {
            S("fort_guro_headbutt_cranial", "Cranial Smash", Slash(20f, DamageType.Physical), "A full-force cranial smash.", Stamina(14f)),
            S("fort_guro_headbutt_split", "Skull Split", Slash(22f, DamageType.Physical), "A headbutt that splits skulls.", Stamina(16f)),
            S("fort_guro_headbutt_gore", "Horn Gore", Slash(22f, DamageType.Physical), "A sharp goring strike.", Stamina(16f)),
            S("fort_guro_headbutt_burning", "Burning Head", Slash(22f, DamageType.Fire), "A headbutt wreathed in flame.", Stamina(16f), DamageType.Fire, true),
            S("fort_guro_headbutt_frozen", "Frozen Head", Slash(22f, DamageType.Ice), "A headbutt numbed by ice.", Stamina(16f), DamageType.Ice, true),
        };

        /* fort_guro_warcry children */
        bank.L2["fort_guro_warcry"] = new BranchSlot[]
        {
            S("fort_guro_warcry_blast", "War Blast", Zone(2.2f, 18f, DamageType.Physical), "A blast born of raw will.", Stamina(14f)),
            S("fort_guro_warcry_shout", "War Shout", Zone(2.4f, 20f, DamageType.Physical), "A shout that rattles the ground.", Stamina(16f)),
            S("fort_guro_warcry_rage", "Rage Cry", Zone(2.2f, 20f, DamageType.Fire), "A cry of pure burning rage.", Stamina(16f), DamageType.Fire, true),
            S("fort_guro_warcry_terrify", "Terrify", Zone(2.2f, 20f, DamageType.Dark), "A cry that chills with dark terror.", Stamina(16f), DamageType.Dark, true),
            S("fort_guro_warcry_thunder", "Thunder Cry", Zone(2.4f, 22f, DamageType.Lightning), "A cry that carries thunder.", Stamina(18f), DamageType.Lightning, true),
        };

        /* fort_guro_unyielding children (all passive) */
        bank.L2["fort_guro_unyielding"] = new BranchSlot[]
        {
            S("fort_guro_unyielding_endurance", "Unyielding Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_guro_unyielding_defense", "Unyielding Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_guro_unyielding_health", "Unyielding Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_guro_unyielding_strength", "Unyielding Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_guro_unyielding_luck", "Unyielding Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* fort_guro_ironcharge children */
        bank.L2["fort_guro_ironcharge"] = new BranchSlot[]
        {
            S("fort_guro_ironcharge_bull", "Iron Bull", Slash(20f, DamageType.Physical), "A bull-rush of iron will.", Stamina(14f)),
            S("fort_guro_ironcharge_steel", "Steel Charge", Slash(22f, DamageType.Physical), "A charge of hardened steel.", Stamina(16f)),
            S("fort_guro_ironcharge_flame", "Flame Charge", Slash(24f, DamageType.Fire), "A charge ablaze with fire.", Stamina(18f), DamageType.Fire, true),
            S("fort_guro_ironcharge_frost", "Frost Charge", Slash(24f, DamageType.Ice), "A charge trailing frost.", Stamina(18f), DamageType.Ice, true),
            S("fort_guro_ironcharge_thunder", "Thunder Charge", Slash(26f, DamageType.Lightning), "A charge rumbling with thunder.", Stamina(20f), DamageType.Lightning, true),
        };

        /* fort_guro_juggernaut children (all passive) */
        bank.L2["fort_guro_juggernaut"] = new BranchSlot[]
        {
            S("fort_guro_juggernaut_defense", "Juggernaut Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("fort_guro_juggernaut_health", "Juggernaut Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("fort_guro_juggernaut_endurance", "Juggernaut Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("fort_guro_juggernaut_strength", "Juggernaut Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("fort_guro_juggernaut_speed", "Juggernaut Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };
    }
}