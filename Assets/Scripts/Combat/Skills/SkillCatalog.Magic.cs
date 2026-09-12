using System.Collections.Generic;

public static partial class SkillCatalog
{
    private static void RegisterMagicDesign(DesignBank bank)
    {
        /* ──────────────── L1 (30 slots — 6 roots × 5 children each) ──────────────── */

        // Root: magic_focus (passive, Intelligence+3)
        bank.L1["magic_focus"] = new BranchSlot[]
        {
            A("magic_heal"),
            S("magic_focus_clarity", "Clarity", Buff(StatType.Intelligence, 3f), "Permanent +3 Intelligence.", passive: true),
            S("magic_focus_holylight", "Holy Light", Spell("magic_focus_holylight_spell", "Holy Light", DamageType.Holy, 24f, 14f, SpellDelivery.Zone, 4f, deliveryRadius: 2.4f, heals: true), "A radiant burst of holy light that sears enemies and mends allies.", Focus(14f), DamageType.Holy, true),
            S("magic_focus_inspire", "Inspire", Buff(StatType.Wisdom, 3f), "Permanent +3 Wisdom.", passive: true),
            S("magic_focus_renew", "Renew", Spell("magic_focus_renew_spell", "Renew", DamageType.Holy, 18f, 12f, SpellDelivery.Instant, 0f, heals: true), "A gentle holy glow that restores the body.", Focus(12f), DamageType.Holy, true),
        };

        // Root: magic_arcane (passive, Wisdom+3)
        bank.L1["magic_arcane"] = new BranchSlot[]
        {
            A("magic_manaflow"),
            A("magic_ward"),
            S("magic_arcane_bolt", "Arcane Bolt", Spell("magic_arcane_bolt_spell", "Arcane Bolt", DamageType.Arcane, 26f, 14f, SpellDelivery.Projectile, 3f), "A bolt of raw arcane energy.", Focus(14f), DamageType.Arcane, true),
            S("magic_arcane_surge", "Arcane Surge", Buff(StatType.Wisdom, 3f), "Permanent +3 Wisdom.", passive: true),
            S("magic_arcane_bind", "Arcane Bind", Spell("magic_arcane_bind_spell", "Arcane Bind", DamageType.Arcane, 24f, 12f, SpellDelivery.Zone, 5f, deliveryRadius: 2f, statusEffect: StatusEffectType.Stagger), "A binding wave of arcane force that staggers foes.", Focus(12f), DamageType.Arcane, true),
        };

        // Root: magic_fireball (active, Focus 15, projectile fire)
        bank.L1["magic_fireball"] = new BranchSlot[]
        {
            A("magic_chain"),
            S("magic_fireball_meteor", "Meteor", Spell("magic_fireball_meteor_spell", "Meteor", DamageType.Fire, 30f, 22f, SpellDelivery.Zone, 6f, deliveryRadius: 3f, knockback: 1.5f), "A burning meteor falls from the sky, scattering the blast.", Focus(22f), DamageType.Fire, true),
            S("magic_fireball_inferno", "Inferno", Spell("magic_fireball_inferno_spell", "Inferno", DamageType.Fire, 32f, 24f, SpellDelivery.Zone, 7f, deliveryRadius: 3.4f, statusEffect: StatusEffectType.Burn), "An expanding ring of fire that scorches all it touches.", Focus(24f), DamageType.Fire, true),
            S("magic_fireball_ember", "Embermind", Buff(StatType.Intelligence, 3f), "Permanent +3 Intelligence.", passive: true),
            S("magic_fireball_scorch", "Scorch", Spell("magic_fireball_scorch_spell", "Scorch", DamageType.Fire, 26f, 16f, SpellDelivery.Projectile, 4f, statusEffect: StatusEffectType.Burn), "A narrow, searing jet of fire that leaves nothing unburnt.", Focus(16f), DamageType.Fire, true),
        };

        // Root: magic_frostbolt (active, Focus 13, projectile ice)
        bank.L1["magic_frostbolt"] = new BranchSlot[]
        {
            S("magic_frostbolt_icelance", "Ice Lance", Spell("magic_frostbolt_icelance_spell", "Ice Lance", DamageType.Ice, 26f, 16f, SpellDelivery.Projectile, 4f), "A long spear of solid ice.", Focus(16f), DamageType.Ice, true),
            S("magic_frostbolt_freeze", "Freeze", Spell("magic_frostbolt_freeze_spell", "Freeze", DamageType.Ice, 28f, 18f, SpellDelivery.Zone, 5f, deliveryRadius: 2.4f, statusEffect: StatusEffectType.Frost), "A wave of freezing air that clings to all it touches.", Focus(18f), DamageType.Ice, true),
            S("magic_frostbolt_crystal", "Crystal Mind", Buff(StatType.Intelligence, 3f), "Permanent +3 Intelligence.", passive: true),
            S("magic_frostbolt_glacier", "Glacier", Spell("magic_frostbolt_glacier_spell", "Glacier", DamageType.Ice, 30f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 2.6f), "A massive wall of glacial ice.", Focus(20f), DamageType.Ice, true),
            S("magic_frostbolt_chill", "Chill Touch", Spell("magic_frostbolt_chill_spell", "Chill Touch", DamageType.Ice, 24f, 14f, SpellDelivery.Projectile, 3f, statusEffect: StatusEffectType.Frost), "A numbing cold that slows the foe.", Focus(14f), DamageType.Ice, true),
        };

        // Root: magic_dark (active, Focus 14, projectile dark)
        bank.L1["magic_dark"] = new BranchSlot[]
        {
            S("magic_dark_shadowbolt", "Shadow Bolt", Spell("magic_dark_shadowbolt_spell", "Shadow Bolt", DamageType.Dark, 28f, 16f, SpellDelivery.Projectile, 4f), "A bolt of concentrated shadow.", Focus(16f), DamageType.Dark, true),
            S("magic_dark_voidrend", "Void Rend", Spell("magic_dark_voidrend_spell", "Void Rend", DamageType.Dark, 30f, 18f, SpellDelivery.Zone, 5f, deliveryRadius: 2.2f, knockback: 1f, statusEffect: StatusEffectType.Rot), "Darkness tears through the area, rots and shoves.", Focus(18f), DamageType.Dark, true),
            S("magic_dark_curse", "Dark Pact", Buff(StatType.Faith, 3f), "Permanent +3 Faith.", passive: true),
            S("magic_dark_devour", "Devour", Spell("magic_dark_devour_spell", "Devour", DamageType.Dark, 34f, 22f, SpellDelivery.Zone, 6f, deliveryRadius: 2f, statusEffect: StatusEffectType.Rot), "Void mouths snap at all nearby foes, rotting them.", Focus(22f), DamageType.Dark, true),
            S("magic_dark_nightfall", "Nightfall", Spell("magic_dark_nightfall_spell", "Nightfall", DamageType.Dark, 30f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 3f), "A plane of unnatural darkness descends.", Focus(20f), DamageType.Dark, true),
        };

        // Root: magic_gust (active, Focus 12, zone wind)
        bank.L1["magic_gust"] = new BranchSlot[]
        {
            A("magic_windblade"),
            S("magic_gust_stormbreath", "Storm Breath", Spell("magic_gust_stormbreath_spell", "Storm Breath", DamageType.Wind, 26f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 3f, knockback: 2f), "A howling breath of storm wind that drives foes back.", Focus(16f), DamageType.Wind, true),
            S("magic_gust_cyclone", "Cyclone", Spell("magic_gust_cyclone_spell", "Cyclone", DamageType.Wind, 30f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 3f, knockback: 2.5f), "A spinning cyclone that scatters foes.", Focus(20f), DamageType.Wind, true),
            S("magic_gust_airmastery", "Air Mastery", Buff(StatType.AttackSpeed, 3f), "Permanent +3 Attack Speed.", passive: true),
            S("magic_gust_airburst", "Air Burst", Spell("magic_gust_airburst_spell", "Air Burst", DamageType.Wind, 24f, 14f, SpellDelivery.Zone, 3f, deliveryRadius: 2.4f, knockback: 2f), "A sudden detonation of air that hurls foes away.", Focus(14f), DamageType.Wind, true),
        };

        /* ──────────────── L2 (150 slots — 30 L1 parents × 5 children each) ──────────────── */

        /* magic_heal children */
        bank.L2["magic_heal"] = new BranchSlot[]
        {
            S("magic_heal_greater", "Greater Heal", Spell("magic_heal_greater_spell", "Greater Heal", DamageType.Holy, 28f, 20f, SpellDelivery.Instant, 0f, heals: true), "A powerful surge of healing.", Focus(20f), DamageType.Holy, true),
            S("magic_heal_light", "Light's Embrace", Spell("magic_heal_light_spell", "Light's Embrace", DamageType.Holy, 22f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2f, heals: true), "A warm embrace of healing light.", Focus(16f), DamageType.Holy, true),
            S("magic_heal_bless", "Blessing", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_heal_purify", "Purify", Spell("magic_heal_purify_spell", "Purify", DamageType.Holy, 24f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.2f, heals: true), "A holy wave that cleanses and mends.", Focus(16f), DamageType.Holy, true),
            S("magic_heal_mend", "Mending Light", Spell("magic_heal_mend_spell", "Mending Light", DamageType.Holy, 20f, 12f, SpellDelivery.Instant, 0f, heals: true), "A steady light that knits wounds.", Focus(12f), DamageType.Holy, true),
        };

        /* magic_manaflow children */
        bank.L2["magic_manaflow"] = new BranchSlot[]
        {
            S("magic_manaflow_pool", "Deep Pool", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_manaflow_siphon", "Siphon", Buff(StatType.Intelligence, 4f), "Permanent +4 Intelligence.", passive: true),
            S("magic_manaflow_arcane", "Arcane Font", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_manaflow_repurpose", "Repurpose", Buff(StatType.Intelligence, 3f), "Permanent +3 Intelligence.", passive: true),
            S("magic_manaflow_regen", "Rapid Regen", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
        };

        /* magic_ward children */
        bank.L2["magic_ward"] = new BranchSlot[]
        {
            S("magic_ward_aegis", "Aegis", Spell("magic_ward_aegis_spell", "Aegis", DamageType.Arcane, 20f, 16f, SpellDelivery.Zone, 5f, deliveryRadius: 2.6f), "A protective shell of arcane force.", Focus(16f), DamageType.Arcane, true),
            S("magic_ward_barrier", "Arcane Barrier", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("magic_ward_bulwark", "Ward Bulwark", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("magic_ward_reflect", "Reflect", Spell("magic_ward_reflect_spell", "Reflect", DamageType.Arcane, 22f, 14f, SpellDelivery.Zone, 4f, deliveryRadius: 2f), "An arcane shell that punishes attackers.", Focus(14f), DamageType.Arcane, true),
            S("magic_ward_shield", "Light Shield", Buff(StatType.Defense, 4f), "Permanent +4 Defense.", passive: true),
        };

        /* magic_chain children */
        bank.L2["magic_chain"] = new BranchSlot[]
        {
            S("magic_chain_fork", "Fork Bolt", Spell("magic_chain_fork_spell", "Fork Bolt", DamageType.Lightning, 32f, 20f, SpellDelivery.Projectile, 5f, statusEffect: StatusEffectType.Stagger), "A bolt that forks into many and staggers.", Focus(20f), DamageType.Lightning, true),
            S("magic_chain_arc", "Arc Storm", Spell("magic_chain_arc_spell", "Arc Storm", DamageType.Lightning, 34f, 24f, SpellDelivery.Zone, 6f, deliveryRadius: 3f, statusEffect: StatusEffectType.Stagger), "A storm of arcing electricity that shocks foes still.", Focus(24f), DamageType.Lightning, true),
            S("magic_chain_static", "Static Field", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_chain_overload", "Overload", Spell("magic_chain_overload_spell", "Overload", DamageType.Lightning, 36f, 26f, SpellDelivery.Zone, 7f, deliveryRadius: 2.8f, statusEffect: StatusEffectType.Stagger), "A burst of overcharged lightning that stuns.", Focus(26f), DamageType.Lightning, true),
            S("magic_chain_leap", "Leap Bolt", Spell("magic_chain_leap_spell", "Leap Bolt", DamageType.Lightning, 30f, 18f, SpellDelivery.Projectile, 4f, statusEffect: StatusEffectType.Stagger), "A bolt that leaps from target to target, buzzing.", Focus(18f), DamageType.Lightning, true),
        };

        /* magic_windblade children */
        bank.L2["magic_windblade"] = new BranchSlot[]
        {
            S("magic_windblade_razor", "Razor Blade", Spell("magic_windblade_razor_spell", "Razor Blade", DamageType.Wind, 24f, 14f, SpellDelivery.Projectile, 3f, deliveryRadius: 1f), "A thinner, sharper blade of wind.", Focus(14f), DamageType.Wind, true),
            S("magic_windblade_scissor", "Wind Scissor", Spell("magic_windblade_scissor_spell", "Wind Scissor", DamageType.Wind, 26f, 16f, SpellDelivery.Projectile, 4f, deliveryRadius: 1.4f), "Twin crossing blades of wind.", Focus(16f), DamageType.Wind, true),
            S("magic_windblade_boreas", "Boreas", Spell("magic_windblade_boreas_spell", "Boreas", DamageType.Wind, 28f, 18f, SpellDelivery.Zone, 5f, deliveryRadius: 2.4f), "A frigid northern gale.", Focus(18f), DamageType.Wind, true),
            S("magic_windblade_swift", "Swift Wind", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("magic_windblade_lashing", "Lashing Wind", Spell("magic_windblade_lashing_spell", "Lashing Wind", DamageType.Wind, 26f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.2f), "Wind that lashes at all sides.", Focus(16f), DamageType.Wind, true),
        };

        /* magic_focus_clarity children (all passive) */
        bank.L2["magic_focus_clarity"] = new BranchSlot[]
        {
            S("magic_focus_clarity_tranquil", "Tranquil Mind", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_focus_clarity_wisdom", "Deep Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_focus_clarity_patience", "Patience", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("magic_focus_clarity_serenity", "Serenity", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("magic_focus_clarity_faith", "Inner Light", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
        };

        /* magic_focus_holylight children */
        bank.L2["magic_focus_holylight"] = new BranchSlot[]
        {
            S("magic_focus_holylight_radiance", "Radiance", Spell("magic_focus_holylight_radiance_spell", "Radiance", DamageType.Holy, 28f, 18f, SpellDelivery.Zone, 5f, deliveryRadius: 2.8f, heals: true), "A blinding burst of holy radiance that sears foes and mends allies.", Focus(18f), DamageType.Holy, true),
            S("magic_focus_holylight_beacon", "Beacon", Spell("magic_focus_holylight_beacon_spell", "Beacon", DamageType.Holy, 26f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.4f, heals: true), "A tower of holy light that heals all beneath it.", Focus(16f), DamageType.Holy, true),
            S("magic_focus_holylight_glory", "Glory", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_focus_holylight_sunburst", "Sunburst", Spell("magic_focus_holylight_sunburst_spell", "Sunburst", DamageType.Holy, 30f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 3f, heals: true), "A sun-bright explosion that wounds the wicked and heals the faithful.", Focus(20f), DamageType.Holy, true),
            S("magic_focus_holylight_illum", "Illumination", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
        };

        /* magic_focus_inspire children (all passive) */
        bank.L2["magic_focus_inspire"] = new BranchSlot[]
        {
            S("magic_focus_inspire_wisdom", "Ancient Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_focus_inspire_faith", "Unshaken Faith", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_focus_inspire_intellect", "Grand Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_focus_inspire_endurance", "Enduring Spirit", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("magic_focus_inspire_luck", "Fortune's Favor", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* magic_focus_renew children */
        bank.L2["magic_focus_renew"] = new BranchSlot[]
        {
            S("magic_focus_renew_regrowth", "Regrowth", Spell("magic_focus_renew_regrowth_spell", "Regrowth", DamageType.Holy, 22f, 14f, SpellDelivery.Instant, 0f, heals: true), "A renewing light that mends swiftly.", Focus(14f), DamageType.Holy, true),
            S("magic_focus_renew_vigor", "Vigor", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("magic_focus_renew_restore", "Restore", Spell("magic_focus_renew_restore_spell", "Restore", DamageType.Holy, 30f, 22f, SpellDelivery.Instant, 0f, heals: true), "A major restoration of the body.", Focus(22f), DamageType.Holy, true),
            S("magic_focus_renew_health", "Peak Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("magic_focus_renew_bloom", "Bloom", Spell("magic_focus_renew_bloom_spell", "Bloom", DamageType.Holy, 24f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.2f, heals: true), "A bloom of revitalizing light.", Focus(16f), DamageType.Holy, true),
        };

        /* magic_arcane_bolt children */
        bank.L2["magic_arcane_bolt"] = new BranchSlot[]
        {
            S("magic_arcane_bolt_force", "Force Bolt", Spell("magic_arcane_bolt_force_spell", "Force Bolt", DamageType.Arcane, 30f, 18f, SpellDelivery.Projectile, 4f), "A heavy bolt of arcane force.", Focus(18f), DamageType.Arcane, true),
            S("magic_arcane_bolt_prism", "Prism Bolt", Spell("magic_arcane_bolt_prism_spell", "Prism Bolt", DamageType.Arcane, 32f, 20f, SpellDelivery.Projectile, 5f), "A bolt that splits into a prism of colors.", Focus(20f), DamageType.Arcane, true),
            S("magic_arcane_bolt_insight", "Arcane Insight", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_arcane_bolt_rupture", "Rupture", Spell("magic_arcane_bolt_rupture_spell", "Rupture", DamageType.Arcane, 34f, 22f, SpellDelivery.Zone, 5f, deliveryRadius: 2.2f), "A rupturing blast of arcane power.", Focus(22f), DamageType.Arcane, true),
            S("magic_arcane_bolt_missile", "Arcane Missiles", Spell("magic_arcane_bolt_missile_spell", "Arcane Missiles", DamageType.Arcane, 28f, 16f, SpellDelivery.Projectile, 4f), "A stream of small arcane missiles.", Focus(16f), DamageType.Arcane, true),
        };

        /* magic_arcane_surge children (all passive) */
        bank.L2["magic_arcane_surge"] = new BranchSlot[]
        {
            S("magic_arcane_surge_power", "Arcane Power", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_arcane_surge_intellect", "Arcane Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_arcane_surge_focus", "Focused Energy", Buff(StatType.Intelligence, 4f), "Permanent +4 Intelligence.", passive: true),
            S("magic_arcane_surge_spirit", "Arcane Spirit", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_arcane_surge_mastery", "Arcane Mastery", Buff(StatType.Wisdom, 6f), "Permanent +6 Wisdom.", passive: true),
        };

        /* magic_arcane_bind children */
        bank.L2["magic_arcane_bind"] = new BranchSlot[]
        {
            S("magic_arcane_bind_shackle", "Shackles", Spell("magic_arcane_bind_shackle_spell", "Shackles", DamageType.Arcane, 28f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.2f, statusEffect: StatusEffectType.Stagger), "Arcane chains that bind, burn and stagger.", Focus(16f), DamageType.Arcane, true),
            S("magic_arcane_bind_hold", "Hold", Spell("magic_arcane_bind_hold_spell", "Hold", DamageType.Arcane, 26f, 14f, SpellDelivery.Zone, 4f, deliveryRadius: 2f, statusEffect: StatusEffectType.Stagger), "An arcane grip that stops foes in their tracks.", Focus(14f), DamageType.Arcane, true),
            S("magic_arcane_bind_vine", "Vine Cage", Spell("magic_arcane_bind_vine_spell", "Vine Cage", DamageType.Arcane, 24f, 12f, SpellDelivery.Zone, 4f, deliveryRadius: 2.4f), "Arcanum vines that ensnare.", Focus(12f), DamageType.Arcane, true),
            S("magic_arcane_bind_endurance", "Bound Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("magic_arcane_bind_warding", "Warding Bind", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
        };

        /* magic_fireball_meteor children */
        bank.L2["magic_fireball_meteor"] = new BranchSlot[]
        {
            S("magic_fireball_meteor_rain", "Meteor Rain", Spell("magic_fireball_meteor_rain_spell", "Meteor Rain", DamageType.Fire, 36f, 26f, SpellDelivery.Zone, 8f, deliveryRadius: 3.6f), "A barrage of falling meteors.", Focus(26f), DamageType.Fire, true),
            S("magic_fireball_meteor_comet", "Comet", Spell("magic_fireball_meteor_comet_spell", "Comet", DamageType.Fire, 34f, 24f, SpellDelivery.Projectile, 6f), "A swift streak of burning light.", Focus(24f), DamageType.Fire, true),
            S("magic_fireball_meteor_impact", "Impact", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_fireball_meteor_astroid", "Asteroid", Spell("magic_fireball_meteor_astroid_spell", "Asteroid", DamageType.Fire, 40f, 30f, SpellDelivery.Zone, 9f, deliveryRadius: 3.6f), "A colossal mass of burning rock.", Focus(30f), DamageType.Fire, true),
            S("magic_fireball_meteor_ember", "Emberfall", Spell("magic_fireball_meteor_ember_spell", "Emberfall", DamageType.Fire, 30f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 3f), "A cascade of burning embers.", Focus(20f), DamageType.Fire, true),
        };

        /* magic_fireball_inferno children */
        bank.L2["magic_fireball_inferno"] = new BranchSlot[]
        {
            S("magic_fireball_inferno_conflagration", "Conflagration", Spell("magic_fireball_inferno_conflagration_spell", "Conflagration", DamageType.Fire, 38f, 28f, SpellDelivery.Zone, 8f, deliveryRadius: 4f), "An inferno that spreads across the ground.", Focus(28f), DamageType.Fire, true),
            S("magic_fireball_inferno_firestorm", "Firestorm", Spell("magic_fireball_inferno_firestorm_spell", "Firestorm", DamageType.Fire, 36f, 26f, SpellDelivery.Zone, 7f, deliveryRadius: 3.8f), "A storm of whirling flame.", Focus(26f), DamageType.Fire, true),
            S("magic_fireball_inferno_rage", "Infernal Rage", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("magic_fireball_inferno_peak", "Inferno Peak", Spell("magic_fireball_inferno_peak_spell", "Inferno Peak", DamageType.Fire, 40f, 30f, SpellDelivery.Zone, 9f, deliveryRadius: 4f), "A towering eruption of flame.", Focus(30f), DamageType.Fire, true),
            S("magic_fireball_inferno_wave", "Fire Wave", Spell("magic_fireball_inferno_wave_spell", "Fire Wave", DamageType.Fire, 32f, 22f, SpellDelivery.Zone, 6f, deliveryRadius: 3.4f), "A rolling wave of fire.", Focus(22f), DamageType.Fire, true),
        };

        /* magic_fireball_ember children (all passive) */
        bank.L2["magic_fireball_ember"] = new BranchSlot[]
        {
            S("magic_fireball_ember_intellect", "Ember Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_fireball_ember_wisdom", "Ember Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_fireball_ember_faith", "Ember Faith", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_fireball_ember_luck", "Luck of the Flame", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("magic_fireball_ember_speed", "Flame Reflex", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };

        /* magic_fireball_scorch children */
        bank.L2["magic_fireball_scorch"] = new BranchSlot[]
        {
            S("magic_fireball_scorch_burn", "Burn", Spell("magic_fireball_scorch_burn_spell", "Burn", DamageType.Fire, 30f, 18f, SpellDelivery.Projectile, 4f, statusEffect: StatusEffectType.Burn), "A searing burn that lingers long after impact.", Focus(18f), DamageType.Fire, true),
            S("magic_fireball_scorch_ignite", "Ignite", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_fireball_scorch_flash", "Flash Fire", Spell("magic_fireball_scorch_flash_spell", "Flash Fire", DamageType.Fire, 32f, 20f, SpellDelivery.Zone, 5f, deliveryRadius: 2.2f), "A swift flash of scorching fire.", Focus(20f), DamageType.Fire, true),
            S("magic_fireball_scorch_searing", "Searing Ray", Spell("magic_fireball_scorch_searing_spell", "Searing Ray", DamageType.Fire, 34f, 22f, SpellDelivery.Projectile, 5f), "A narrow beam of searing heat.", Focus(22f), DamageType.Fire, true),
            S("magic_fireball_scorch_heat", "Radiant Heat", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
        };

        /* magic_frostbolt_icelance children */
        bank.L2["magic_frostbolt_icelance"] = new BranchSlot[]
        {
            S("magic_frostbolt_icelance_pierce", "Frost Pierce", Spell("magic_frostbolt_icelance_pierce_spell", "Frost Pierce", DamageType.Ice, 30f, 18f, SpellDelivery.Projectile, 4f), "A lance that pierces through armor.", Focus(18f), DamageType.Ice, true),
            S("magic_frostbolt_icelance_havoc", "Ice Havoc", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_frostbolt_icelance_impale", "Glacial Impale", Spell("magic_frostbolt_icelance_impale_spell", "Glacial Impale", DamageType.Ice, 34f, 22f, SpellDelivery.Projectile, 5f), "A massive spike that impales.", Focus(22f), DamageType.Ice, true),
            S("magic_frostbolt_icelance_hail", "Hail Lance", Spell("magic_frostbolt_icelance_hail_spell", "Hail Lance", DamageType.Ice, 30f, 20f, SpellDelivery.Zone, 5f, deliveryRadius: 2f), "A volley of ice lances.", Focus(20f), DamageType.Ice, true),
            S("magic_frostbolt_icelance_bite", "Frost Bite", Spell("magic_frostbolt_icelance_bite_spell", "Frost Bite", DamageType.Ice, 28f, 16f, SpellDelivery.Projectile, 4f, statusEffect: StatusEffectType.Frost), "A biting cold that chills to the bone.", Focus(16f), DamageType.Ice, true),
        };

        /* magic_frostbolt_freeze children */
        bank.L2["magic_frostbolt_freeze"] = new BranchSlot[]
        {
            S("magic_frostbolt_freeze_deep", "Deep Freeze", Spell("magic_frostbolt_freeze_deep_spell", "Deep Freeze", DamageType.Ice, 34f, 24f, SpellDelivery.Zone, 6f, deliveryRadius: 2.6f, statusEffect: StatusEffectType.Stagger), "A paralyzing cold that freezes foes solid.", Focus(24f), DamageType.Ice, true),
            S("magic_frostbolt_freeze_snap", "Cold Snap", Spell("magic_frostbolt_freeze_snap_spell", "Cold Snap", DamageType.Ice, 32f, 20f, SpellDelivery.Zone, 5f, deliveryRadius: 2.4f), "A sudden snap-freeze of the air.", Focus(20f), DamageType.Ice, true),
            S("magic_frostbolt_freeze_chill", "Frozen Will", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_frostbolt_freeze_tundra", "Tundra", Spell("magic_frostbolt_freeze_tundra_spell", "Tundra", DamageType.Ice, 32f, 22f, SpellDelivery.Zone, 6f, deliveryRadius: 3f), "The ground becomes frozen tundra.", Focus(22f), DamageType.Ice, true),
            S("magic_frostbolt_freeze_touch", "Frozen Touch", Spell("magic_frostbolt_freeze_touch_spell", "Frozen Touch", DamageType.Ice, 28f, 18f, SpellDelivery.Zone, 4f, deliveryRadius: 2f), "An ice touch that slows and bites.", Focus(18f), DamageType.Ice, true),
        };

        /* magic_frostbolt_crystal children (all passive) */
        bank.L2["magic_frostbolt_crystal"] = new BranchSlot[]
        {
            S("magic_frostbolt_crystal_intellect", "Crystal Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_frostbolt_crystal_wisdom", "Crystal Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_frostbolt_crystal_endurance", "Crystal Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("magic_frostbolt_crystal_defense", "Crystal Armor", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("magic_frostbolt_crystal_luck", "Crystal Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* magic_frostbolt_glacier children */
        bank.L2["magic_frostbolt_glacier"] = new BranchSlot[]
        {
            S("magic_frostbolt_glacier_wall", "Glacier Wall", Spell("magic_frostbolt_glacier_wall_spell", "Glacier Wall", DamageType.Ice, 36f, 26f, SpellDelivery.Zone, 8f, deliveryRadius: 3.4f), "A colossal wall of glacier ice.", Focus(26f), DamageType.Ice, true),
            S("magic_frostbolt_glacier_surge", "Glacial Surge", Spell("magic_frostbolt_glacier_surge_spell", "Glacial Surge", DamageType.Ice, 40f, 30f, SpellDelivery.Zone, 9f, deliveryRadius: 3.8f), "A surge of suffocating cold.", Focus(30f), DamageType.Ice, true),
            S("magic_frostbolt_glacier_weight", "Glacial Weight", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("magic_frostbolt_glacier_avalanche", "Avalanche", Spell("magic_frostbolt_glacier_avalanche_spell", "Avalanche", DamageType.Ice, 38f, 28f, SpellDelivery.Zone, 8f, deliveryRadius: 3.6f), "A cascading avalanche of ice.", Focus(28f), DamageType.Ice, true),
            S("magic_frostbolt_glacier_eternal", "Eternal Cold", Buff(StatType.Wisdom, 6f), "Permanent +6 Wisdom.", passive: true),
        };

        /* magic_frostbolt_chill children */
        bank.L2["magic_frostbolt_chill"] = new BranchSlot[]
        {
            S("magic_frostbolt_chill_soul", "Chill Soul", Spell("magic_frostbolt_chill_soul_spell", "Chill Soul", DamageType.Ice, 30f, 18f, SpellDelivery.Projectile, 4f, statusEffect: StatusEffectType.Frost), "A cold that seeps into the soul and slows it.", Focus(18f), DamageType.Ice, true),
            S("magic_frostbolt_chill_curse", "Frost Curse", Spell("magic_frostbolt_chill_curse_spell", "Frost Curse", DamageType.Ice, 28f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2f), "A curse of creeping frost.", Focus(16f), DamageType.Ice, true),
            S("magic_frostbolt_chill_will", "Frost Will", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_frostbolt_chill_stare", "Cold Stare", Spell("magic_frostbolt_chill_stare_spell", "Cold Stare", DamageType.Ice, 26f, 14f, SpellDelivery.Projectile, 3f), "A gaze that freezes the heart.", Focus(14f), DamageType.Ice, true),
            S("magic_frostbolt_chill_hour", "Witching Chill", Spell("magic_frostbolt_chill_hour_spell", "Witching Chill", DamageType.Ice, 32f, 20f, SpellDelivery.Zone, 5f, deliveryRadius: 2.4f), "An unnatural hour of deep cold.", Focus(20f), DamageType.Ice, true),
        };

        /* magic_dark_shadowbolt children */
        bank.L2["magic_dark_shadowbolt"] = new BranchSlot[]
        {
            S("magic_dark_shadowbolt_doom", "Doom Bolt", Spell("magic_dark_shadowbolt_doom_spell", "Doom Bolt", DamageType.Dark, 34f, 22f, SpellDelivery.Projectile, 5f), "A bolt of impending doom.", Focus(22f), DamageType.Dark, true),
            S("magic_dark_shadowbolt_gloom", "Gloom", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_dark_shadowbolt_pool", "Shadow Pool", Spell("magic_dark_shadowbolt_pool_spell", "Shadow Pool", DamageType.Dark, 32f, 20f, SpellDelivery.Zone, 5f, deliveryRadius: 2.6f), "A pool of writhing shadow.", Focus(20f), DamageType.Dark, true),
            S("magic_dark_shadowbolt_spear", "Shadow Spear", Spell("magic_dark_shadowbolt_spear_spell", "Shadow Spear", DamageType.Dark, 36f, 24f, SpellDelivery.Projectile, 6f), "A spear of condensed darkness.", Focus(24f), DamageType.Dark, true),
            S("magic_dark_shadowbolt_tendrils", "Tendrils", Spell("magic_dark_shadowbolt_tendrils_spell", "Tendrils", DamageType.Dark, 30f, 18f, SpellDelivery.Zone, 4f, deliveryRadius: 2.4f), "Shadow tentacles lash all around.", Focus(18f), DamageType.Dark, true),
        };

        /* magic_dark_voidrend children */
        bank.L2["magic_dark_voidrend"] = new BranchSlot[]
        {
            S("magic_dark_voidrend_rip", "Void Rip", Spell("magic_dark_voidrend_rip_spell", "Void Rip", DamageType.Dark, 36f, 24f, SpellDelivery.Zone, 6f, deliveryRadius: 2.6f), "A tear in reality that rends the target.", Focus(24f), DamageType.Dark, true),
            S("magic_dark_voidrend_tear", "Tear", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_dark_voidrend_abyss", "Abyss", Spell("magic_dark_voidrend_abyss_spell", "Abyss", DamageType.Dark, 38f, 26f, SpellDelivery.Zone, 7f, deliveryRadius: 2.8f), "Darkness opens into a void abyss.", Focus(26f), DamageType.Dark, true),
            S("magic_dark_voidrend_laceration", "Laceration", Spell("magic_dark_voidrend_laceration_spell", "Laceration", DamageType.Dark, 32f, 20f, SpellDelivery.Projectile, 5f), "A void that lacerates on contact.", Focus(20f), DamageType.Dark, true),
            S("magic_dark_voidrend_rend", "Void Rend", Spell("magic_dark_voidrend_rend_spell", "Void Rend", DamageType.Dark, 34f, 22f, SpellDelivery.Projectile, 5f), "A rending projectile of void nothing.", Focus(22f), DamageType.Dark, true),
        };

        /* magic_dark_curse children (all passive) */
        bank.L2["magic_dark_curse"] = new BranchSlot[]
        {
            S("magic_dark_curse_pact", "Pact of Dark", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("magic_dark_curse_might", "Dark Might", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("magic_dark_curse_wisdom", "Unholy Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_dark_curse_intellect", "Dark Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("magic_dark_curse_endurance", "Dark Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
        };

        /* magic_dark_devour children */
        bank.L2["magic_dark_devour"] = new BranchSlot[]
        {
            S("magic_dark_devour_consume", "Consume", Spell("magic_dark_devour_consume_spell", "Consume", DamageType.Dark, 38f, 26f, SpellDelivery.Zone, 7f, deliveryRadius: 2.4f, statusEffect: StatusEffectType.Rot), "Maws of darkness consume and rot all they touch.", Focus(26f), DamageType.Dark, true),
            S("magic_dark_devour_swallow", "Swallow", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("magic_dark_devour_gullet", "Void Gullet", Spell("magic_dark_devour_gullet_spell", "Void Gullet", DamageType.Dark, 40f, 30f, SpellDelivery.Zone, 8f, deliveryRadius: 3f), "A gaping void that swallows foes.", Focus(30f), DamageType.Dark, true),
            S("magic_dark_devour_feast", "Dark Feast", Spell("magic_dark_devour_feast_spell", "Dark Feast", DamageType.Dark, 34f, 22f, SpellDelivery.Zone, 6f, deliveryRadius: 2.6f), "Darkness that feasts on the enemy.", Focus(22f), DamageType.Dark, true),
            S("magic_dark_devour_hunger", "Hunger", Spell("magic_dark_devour_hunger_spell", "Hunger", DamageType.Dark, 30f, 18f, SpellDelivery.Projectile, 4f), "A hungry beam of darkness.", Focus(18f), DamageType.Dark, true),
        };

        /* magic_dark_nightfall children */
        bank.L2["magic_dark_nightfall"] = new BranchSlot[]
        {
            S("magic_dark_nightfall_eclipse", "Eclipse", Spell("magic_dark_nightfall_eclipse_spell", "Eclipse", DamageType.Dark, 38f, 28f, SpellDelivery.Zone, 8f, deliveryRadius: 3.4f), "The sky darkens into eclipse.", Focus(28f), DamageType.Dark, true),
            S("magic_dark_nightfall_midnight", "Midnight", Buff(StatType.Faith, 6f), "Permanent +6 Faith.", passive: true),
            S("magic_dark_nightfall_enshroud", "Enshroud", Spell("magic_dark_nightfall_enshroud_spell", "Enshroud", DamageType.Dark, 34f, 24f, SpellDelivery.Zone, 6f, deliveryRadius: 3f), "Darkness enshrouds the area.", Focus(24f), DamageType.Dark, true),
            S("magic_dark_nightfall_veil", "Veil of Night", Spell("magic_dark_nightfall_veil_spell", "Veil of Night", DamageType.Dark, 30f, 20f, SpellDelivery.Zone, 5f, deliveryRadius: 2.6f), "A veil of impenetrable night.", Focus(20f), DamageType.Dark, true),
            S("magic_dark_nightfall_dark", "Permanent Dusk", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };

        /* magic_gust_stormbreath children */
        bank.L2["magic_gust_stormbreath"] = new BranchSlot[]
        {
            S("magic_gust_stormbreath_gale", "Gale Breath", Spell("magic_gust_stormbreath_gale_spell", "Gale Breath", DamageType.Wind, 30f, 18f, SpellDelivery.Zone, 5f, deliveryRadius: 3f), "A strengthened gale of breath.", Focus(18f), DamageType.Wind, true),
            S("magic_gust_stormbreath_howl", "Howl", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("magic_gust_stormbreath_blast", "Storm Blast", Spell("magic_gust_stormbreath_blast_spell", "Storm Blast", DamageType.Wind, 32f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 3.2f), "A pressurized blast of storm air.", Focus(20f), DamageType.Wind, true),
            S("magic_gust_stormbreath_shriek", "Wind Shriek", Spell("magic_gust_stormbreath_shriek_spell", "Wind Shriek", DamageType.Wind, 28f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.8f), "A piercing shriek of wind.", Focus(16f), DamageType.Wind, true),
            S("magic_gust_stormbreath_zephyr", "Zephyr", Spell("magic_gust_stormbreath_zephyr_spell", "Zephyr", DamageType.Wind, 26f, 14f, SpellDelivery.Zone, 4f, deliveryRadius: 2.6f), "A gentle but cutting breeze.", Focus(14f), DamageType.Wind, true),
        };

        /* magic_gust_cyclone children */
        bank.L2["magic_gust_cyclone"] = new BranchSlot[]
        {
            S("magic_gust_cyclone_tornado", "Mini Tornado", Spell("magic_gust_cyclone_tornado_spell", "Mini Tornado", DamageType.Wind, 34f, 22f, SpellDelivery.Vortex, 7f, deliveryRange: 8f, deliveryRadius: 3f, duration: 4f), "A compact but violent tornado that drags foes in.", Focus(22f), DamageType.Wind, true),
            S("magic_gust_cyclone_whirl", "Whirlwind", Spell("magic_gust_cyclone_whirl_spell", "Whirlwind", DamageType.Wind, 32f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 2.8f), "A chaotic whirlwind of force.", Focus(20f), DamageType.Wind, true),
            S("magic_gust_cyclone_stormeye", "Storm Eye", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("magic_gust_cyclone_windstorm", "Windstorm", Spell("magic_gust_cyclone_windstorm_spell", "Windstorm", DamageType.Wind, 36f, 24f, SpellDelivery.Zone, 7f, deliveryRadius: 3.4f), "A full-scale windstorm.", Focus(24f), DamageType.Wind, true),
            S("magic_gust_cyclone_spin", "Spin", Spell("magic_gust_cyclone_spin_spell", "Spin", DamageType.Wind, 28f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.4f), "A rapid spinning vortex.", Focus(16f), DamageType.Wind, true),
        };

        /* magic_gust_airmastery children (all passive) */
        bank.L2["magic_gust_airmastery"] = new BranchSlot[]
        {
            S("magic_gust_airmastery_speed", "Sky Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("magic_gust_airmastery_attackspeed", "Wind Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("magic_gust_airmastery_dex", "Feather Reflex", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("magic_gust_airmastery_agility", "Air Agility", Buff(StatType.Dexterity, 4f), "Permanent +4 Dexterity.", passive: true),
            S("magic_gust_airmastery_grace", "Wind Grace", Buff(StatType.Speed, 4f), "Permanent +4 Speed.", passive: true),
        };

        /* magic_gust_airburst children */
        bank.L2["magic_gust_airburst"] = new BranchSlot[]
        {
            S("magic_gust_airburst_surge", "Air Surge", Spell("magic_gust_airburst_surge_spell", "Air Surge", DamageType.Wind, 30f, 18f, SpellDelivery.Zone, 5f, deliveryRadius: 2.6f), "A surge of compressed air.", Focus(18f), DamageType.Wind, true),
            S("magic_gust_airburst_pop", "Pop", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("magic_gust_airburst_crack", "Crack", Spell("magic_gust_airburst_crack_spell", "Crack", DamageType.Wind, 28f, 16f, SpellDelivery.Zone, 4f, deliveryRadius: 2.4f, knockback: 2f), "A sonic crack of bursting air.", Focus(16f), DamageType.Wind, true),
            S("magic_gust_airburst_pressure", "Pressure", Spell("magic_gust_airburst_pressure_spell", "Pressure", DamageType.Wind, 30f, 18f, SpellDelivery.Zone, 5f, deliveryRadius: 2.6f, knockback: 2.2f), "Crushing air pressure.", Focus(18f), DamageType.Wind, true),
            S("magic_gust_airburst_shock", "Shockwave", Spell("magic_gust_airburst_shock_spell", "Shockwave", DamageType.Wind, 32f, 20f, SpellDelivery.Zone, 6f, deliveryRadius: 3f, knockback: 2.5f), "A wide shockwave of air that scatters everything.", Focus(20f), DamageType.Wind, true),
        };
    }
}