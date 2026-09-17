using System.Collections.Generic;

public static partial class SkillCatalog
{
    private static void RegisterCraftingDesign(DesignBank bank)
    {
        /* ──────────────── L1 (25 slots — 5 roots × 5 children each) ──────────────── */

        // Root: craft_hands (passive, Luck+3)
        bank.L1["craft_hands"] = new BranchSlot[]
        {
            A("craft_purity"),
            S("craft_hands_knife", "Craft Knife", Slash(16f, DamageType.Physical), "A precise crafting cut.", Stamina(10f)),
            S("craft_hands_steady", "Steady Craft", Perk(PassivePerkType.LootLuckPercent, 4f), "Steady hands court fortune; loot luck rises 4%.", passive: true),
            S("craft_hands_dex", "Dexterous Hands", Perk(PassivePerkType.AttackSpeedPercent, 3f), "Swift fingers speed every tool and strike by 3%.", passive: true),
            S("craft_hands_quality", "Adept Hands", Perk(PassivePerkType.LootLuckPercent, 4f), "Four percent more luck settles into adept palms.", passive: true),
        };

        // Root: craft_knowledge (passive, Intelligence+3)
        bank.L1["craft_knowledge"] = new BranchSlot[]
        {
            A("craft_refine"),
            A("craft_repair"),
            S("craft_knowledge_sage", "Sage Lore", Perk(PassivePerkType.FocusMaxPercent, 6f), "Deep lore swells focus reserves by 6%.", passive: true),
            S("craft_knowledge_scribe", "Scribe", Perk(PassivePerkType.FocusRegenPercent, 5f), "Scribbled wisdom restores focus 5% faster.", passive: true),
            S("craft_knowledge_alchemy", "Alchemy", Zone(1.6f, 14f, DamageType.Arcane), "A reactive alchemical pulse.", Focus(12f), DamageType.Arcane, true),
        };

        // Root: craft_focus (passive, Wisdom+2)
        bank.L1["craft_focus"] = new BranchSlot[]
        {
            S("craft_focus_consume", "Consume", Perk(PassivePerkType.FocusRegenPercent, 5f), "What is consumed feeds focus back 5% faster.", passive: true),
            S("craft_focus_block", "Block Mind", Perk(PassivePerkType.BlockEfficiencyPercent, 8f), "A walled mind holds block 8% more efficiently.", passive: true),
            S("craft_focus_magician", "Magician", Perk(PassivePerkType.SpellDamagePercent, 6f), "Arcane craft ignites spell power by 6%.", passive: true),
            S("craft_focus_mana", "Mana Whorl", Zone(1.8f, 14f, DamageType.Arcane), "A whorl of gathered mana.", Focus(12f), DamageType.Arcane, true),
            S("craft_focus_peace", "Peaceful Mind", Perk(PassivePerkType.CooldownReductionPercent, 3f), "Stillness trims every cooldown by 3%.", passive: true),
        };

        // Root: craft_endurance (passive, Endurance+3)
        bank.L1["craft_endurance"] = new BranchSlot[]
        {
            S("craft_endurance_tireless", "Tireless", Perk(PassivePerkType.StaminaRegenPercent, 10f), "Exhaustion flees; stamina returns 10% faster.", passive: true),
            S("craft_endurance_marathon", "Marathon", Perk(PassivePerkType.StaminaMaxPercent, 6f), "Unflagging lungs widen the stamina pool 6%.", passive: true),
            S("craft_endurance_longwork", "Long Work", Perk(PassivePerkType.StaminaMaxPercent, 3f), "The long shift bulks stamina reserves by 3%.", passive: true),
            S("craft_endurance_hold", "Hold Ground", Perk(PassivePerkType.StaggerResistPercent, 10f), "Rooted feet shrug off 10% more stagger.", passive: true),
            S("craft_endurance_shock", "Shock Endurance", Slash(16f, DamageType.Physical), "A shoulder-driven shock slam.", Stamina(10f)),
        };

        // Root: craft_efficiency (passive, AttackSpeed+2)
        bank.L1["craft_efficiency"] = new BranchSlot[]
        {
            S("craft_efficiency_quickswap", "Quick Swap", Perk(PassivePerkType.CooldownReductionPercent, 3f), "Swapped grips cool down 3% faster.", passive: true),
            S("craft_efficiency_optimize", "Optimize", Perk(PassivePerkType.CooldownReductionPercent, 4f), "Optimized motion cuts cooldowns by 4%.", passive: true),
            S("craft_efficiency_streamline", "Streamline", Perk(PassivePerkType.AttackSpeedPercent, 3f), "Trimmed form lets swings land 3% faster.", passive: true),
            S("craft_efficiency_workfast", "Work Fast", Perk(PassivePerkType.AttackSpeedPercent, 4f), "Bench-haste grants 4% attack speed.", passive: true),
            S("craft_efficiency_pulse", "Efficiency Pulse", Zone(1.6f, 12f, DamageType.Physical), "A well-timed pulse of force.", Stamina(10f)),
        };

        /* ──────────────── L2 (125 slots — 25 L1 parents × 5 children each) ──────────────── */

        /* craft_purity children */
        bank.L2["craft_purity"] = new BranchSlot[]
        {
            S("craft_purity_refinedluck", "Refined Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_purity_cleansing", "Cleansing Touch", Buff(StatType.Luck, 4f), "Permanent +4 Luck.", passive: true),
            S("craft_purity_golden", "Golden Touch", Slash(18f, DamageType.Arcane), "A touch that transmutes matter to gold.", Focus(14f), DamageType.Arcane, true),
            S("craft_purity_essence", "Pure Essence", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_purity_craft", "Pure Craft", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
        };

        /* craft_refine children (all passive) */
        bank.L2["craft_refine"] = new BranchSlot[]
        {
            S("craft_refine_polish", "Polish", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("craft_refine_perfectcut", "Perfect Cut", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_refine_finework", "Fine Work", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_refine_masterhand", "Masterful Hand", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_refine_core", "Refined Core", Buff(StatType.Intelligence, 4f), "Permanent +4 Intelligence.", passive: true),
        };

        /* craft_repair children */
        bank.L2["craft_repair"] = new BranchSlot[]
        {
            S("craft_repair_reinforce", "Reinforce", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("craft_repair_mend", "Mend", Zone(1.6f, 14f, DamageType.Physical), "A repairing pulse of force.", Stamina(12f)),
            S("craft_repair_fortify", "Fortify", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_repair_restore", "Restore", Zone(1.8f, 16f, DamageType.Arcane), "An arcane restoration wave.", Focus(14f), DamageType.Arcane, true),
            S("craft_repair_enduring", "Enduring Fix", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
        };

        /* craft_hands_knife children */
        bank.L2["craft_hands_knife"] = new BranchSlot[]
        {
            S("craft_hands_knife_precision", "Precision Cut", Slash(18f, DamageType.Physical), "A cut made with surgical precision.", Stamina(12f)),
            S("craft_hands_knife_ember", "Ember Cut", Slash(20f, DamageType.Fire), "A cut along a seam of ember.", Stamina(14f), DamageType.Fire, true),
            S("craft_hands_knife_frost", "Frost Cut", Slash(20f, DamageType.Ice), "A cut that shears through cold.", Stamina(14f), DamageType.Ice, true),
            S("craft_hands_knife_quick", "Quick Cut", Slash(18f, DamageType.Physical), "A fast, efficient cut.", Stamina(12f)),
            S("craft_hands_knife_void", "Void Cut", Slash(22f, DamageType.Dark), "A cut that parts dark matter.", Stamina(16f), DamageType.Dark, true),
        };

        /* craft_hands_steady children (all passive) */
        bank.L2["craft_hands_steady"] = new BranchSlot[]
        {
            S("craft_hands_steady_dex", "Steady Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_hands_steady_luck", "Steady Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_hands_steady_attackspeed", "Steady Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_hands_steady_speed", "Steady Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_hands_steady_wisdom", "Steady Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
        };

        /* craft_hands_dex children (all passive) */
        bank.L2["craft_hands_dex"] = new BranchSlot[]
        {
            S("craft_hands_dex_attackspeed", "Dex Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_hands_dex_dex", "Dexterous Core", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_hands_dex_speed", "Dex Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_hands_dex_luck", "Deft Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_hands_dex_intelligence", "Deft Mind", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
        };

        /* craft_hands_quality children (all passive) */
        bank.L2["craft_hands_quality"] = new BranchSlot[]
        {
            S("craft_hands_quality_luck", "Quality Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_hands_quality_wisdom", "Quality Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_hands_quality_dex", "Quality Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_hands_quality_attackspeed", "Quality Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_hands_quality_health", "Quality Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
        };

        /* craft_knowledge_sage children (all passive) */
        bank.L2["craft_knowledge_sage"] = new BranchSlot[]
        {
            S("craft_knowledge_sage_intelligence", "Sage Intelligence", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("craft_knowledge_sage_wisdom", "Sage Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_knowledge_sage_faith", "Sage Faith", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("craft_knowledge_sage_luck", "Sage Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_knowledge_sage_dex", "Sage Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
        };

        /* craft_knowledge_scribe children (all passive) */
        bank.L2["craft_knowledge_scribe"] = new BranchSlot[]
        {
            S("craft_knowledge_scribe_wisdom", "Scribe Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_knowledge_scribe_intelligence", "Scribe Intelligence", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("craft_knowledge_scribe_faith", "Scribe Faith", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("craft_knowledge_scribe_luck", "Scribe Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_knowledge_scribe_speed", "Scribe Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };

        /* craft_knowledge_alchemy children */
        bank.L2["craft_knowledge_alchemy"] = new BranchSlot[]
        {
            S("craft_knowledge_alchemy_alembic", "Alembic", Zone(2f, 16f, DamageType.Arcane), "A bubbling arcane alembic burst.", Focus(14f), DamageType.Arcane, true),
            S("craft_knowledge_alchemy_potion", "Potion", Zone(2f, 18f, DamageType.Water), "A reactive potion splashes into force.", Focus(16f), DamageType.Water, true),
            S("craft_knowledge_alchemy_transmute", "Transmute", Zone(2.2f, 18f, DamageType.Arcane), "Matter reorganized into violence.", Focus(16f), DamageType.Arcane, true),
            S("craft_knowledge_alchemy_catalyst", "Catalyst", Slash(20f, DamageType.Fire), "A catalyst-touched strike.", Stamina(16f), DamageType.Fire, true),
            S("craft_knowledge_alchemy_filament", "Filament", Slash(20f, DamageType.Dark), "A dark filament drawn from a beaker.", Stamina(16f), DamageType.Dark, true),
        };

        /* craft_focus_consume children (all passive) */
        bank.L2["craft_focus_consume"] = new BranchSlot[]
        {
            S("craft_focus_consume_wisdom", "Consumed Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_focus_consume_intelligence", "Consumed Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("craft_focus_consume_health", "Consumed Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_focus_consume_endurance", "Consumed Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_focus_consume_faith", "Consumed Faith", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
        };

        /* craft_focus_block children (all passive) */
        bank.L2["craft_focus_block"] = new BranchSlot[]
        {
            S("craft_focus_block_intelligence", "Blocked Mind", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("craft_focus_block_wisdom", "Blocked Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_focus_block_defense", "Blocked Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("craft_focus_block_health", "Blocked Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_focus_block_endurance", "Blocked Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
        };

        /* craft_focus_magician children (all passive) */
        bank.L2["craft_focus_magician"] = new BranchSlot[]
        {
            S("craft_focus_magician_wisdom", "Magician Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_focus_magician_faith", "Magician Faith", Buff(StatType.Faith, 5f), "Permanent +5 Faith.", passive: true),
            S("craft_focus_magician_intelligence", "Magician Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("craft_focus_magician_luck", "Magician Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_focus_magician_speed", "Magician Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
        };

        /* craft_focus_mana children */
        bank.L2["craft_focus_mana"] = new BranchSlot[]
        {
            S("craft_focus_mana_surge", "Mana Surge", Zone(2.2f, 18f, DamageType.Arcane), "A surging wave of mana.", Focus(16f), DamageType.Arcane, true),
            S("craft_focus_mana_spike", "Mana Spike", Slash(20f, DamageType.Arcane), "A crystalline mana spike.", Stamina(16f), DamageType.Arcane, true),
            S("craft_focus_mana_whirlpool", "Whirlpool", Zone(2.4f, 18f, DamageType.Water), "A sucking whorl of water and mana.", Focus(16f), DamageType.Water, true),
            S("craft_focus_mana_vortex", "Mana Vortex", Zone(2.2f, 18f, DamageType.Wind), "A spinning vortex of channeled mana.", Focus(16f), DamageType.Wind, true),
            S("craft_focus_mana_void", "Void Core", Zone(2.2f, 20f, DamageType.Dark), "A dark core of spent mana.", Focus(18f), DamageType.Dark, true),
        };

        /* craft_focus_peace children (all passive) */
        bank.L2["craft_focus_peace"] = new BranchSlot[]
        {
            S("craft_focus_peace_health", "Peaceful Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_focus_peace_endurance", "Peaceful Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_focus_peace_wisdom", "Peaceful Wisdom", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("craft_focus_peace_defense", "Peaceful Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("craft_focus_peace_luck", "Peaceful Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* craft_endurance_tireless children (all passive) */
        bank.L2["craft_endurance_tireless"] = new BranchSlot[]
        {
            S("craft_endurance_tireless_endurance", "Tireless Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_endurance_tireless_health", "Tireless Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_endurance_tireless_speed", "Tireless Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_endurance_tireless_attackspeed", "Tireless Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_endurance_tireless_strength", "Tireless Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
        };

        /* craft_endurance_marathon children (all passive) */
        bank.L2["craft_endurance_marathon"] = new BranchSlot[]
        {
            S("craft_endurance_marathon_speed", "Marathon Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_endurance_marathon_endurance", "Marathon Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_endurance_marathon_health", "Marathon Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_endurance_marathon_dex", "Marathon Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_endurance_marathon_luck", "Marathon Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* craft_endurance_longwork children (all passive) */
        bank.L2["craft_endurance_longwork"] = new BranchSlot[]
        {
            S("craft_endurance_longwork_health", "Long Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_endurance_longwork_endurance", "Long Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_endurance_longwork_defense", "Long Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("craft_endurance_longwork_strength", "Long Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("craft_endurance_longwork_luck", "Long Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* craft_endurance_hold children (all passive) */
        bank.L2["craft_endurance_hold"] = new BranchSlot[]
        {
            S("craft_endurance_hold_defense", "Held Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("craft_endurance_hold_endurance", "Held Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_endurance_hold_health", "Held Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_endurance_hold_strength", "Held Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("craft_endurance_hold_luck", "Held Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* craft_endurance_shock children */
        bank.L2["craft_endurance_shock"] = new BranchSlot[]
        {
            S("craft_endurance_shock_pulse", "Shock Pulse", Slash(20f, DamageType.Physical), "A body-shock pulse.", Stamina(14f)),
            S("craft_endurance_shock_thunder", "Thunder Shock", Slash(22f, DamageType.Lightning), "A jaw-rattling thunder shock.", Stamina(16f), DamageType.Lightning, true),
            S("craft_endurance_shock_ground", "Ground Shock", Zone(2f, 20f, DamageType.Earth), "A shockwave driven through the ground.", Stamina(16f), DamageType.Earth, true),
            S("craft_endurance_shock_ice", "Ice Shock", Slash(22f, DamageType.Ice), "A cold shock that seizes muscles.", Stamina(16f), DamageType.Ice, true),
            S("craft_endurance_shock_chain", "Chain Shock", Slash(24f, DamageType.Lightning), "A shock that chains between foes.", Stamina(18f), DamageType.Lightning, true),
        };

        /* craft_efficiency_quickswap children (all passive) */
        bank.L2["craft_efficiency_quickswap"] = new BranchSlot[]
        {
            S("craft_efficiency_quickswap_attackspeed", "Swap Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_efficiency_quickswap_dex", "Swap Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_efficiency_quickswap_speed", "Swap Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_efficiency_quickswap_luck", "Swap Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("craft_efficiency_quickswap_endurance", "Swap Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
        };

        /* craft_efficiency_optimize children (all passive) */
        bank.L2["craft_efficiency_optimize"] = new BranchSlot[]
        {
            S("craft_efficiency_optimize_dex", "Optimized Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_efficiency_optimize_intelligence", "Optimized Intellect", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
            S("craft_efficiency_optimize_speed", "Optimized Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_efficiency_optimize_attackspeed", "Optimized Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_efficiency_optimize_luck", "Optimized Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* craft_efficiency_streamline children (all passive) */
        bank.L2["craft_efficiency_streamline"] = new BranchSlot[]
        {
            S("craft_efficiency_streamline_speed", "Streamlined Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_efficiency_streamline_attackspeed", "Streamlined Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_efficiency_streamline_dex", "Streamlined Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_efficiency_streamline_endurance", "Streamlined Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("craft_efficiency_streamline_luck", "Streamlined Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* craft_efficiency_workfast children (all passive) */
        bank.L2["craft_efficiency_workfast"] = new BranchSlot[]
        {
            S("craft_efficiency_workfast_attackspeed", "Work Speed", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("craft_efficiency_workfast_dex", "Work Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("craft_efficiency_workfast_speed", "Fast Style", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("craft_efficiency_workfast_health", "Work Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("craft_efficiency_workfast_luck", "Work Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* craft_efficiency_pulse children */
        bank.L2["craft_efficiency_pulse"] = new BranchSlot[]
        {
            S("craft_efficiency_pulse_quick", "Quick Pulse", Slash(18f, DamageType.Physical), "A quick, efficient pulse.", Stamina(12f)),
            S("craft_efficiency_pulse_force", "Force Pulse", Slash(20f, DamageType.Physical), "A heavier pulse of force.", Stamina(14f)),
            S("craft_efficiency_pulse_arc", "Arc Pulse", Zone(2f, 18f, DamageType.Arcane), "An arcane pulse that resonates.", Focus(14f), DamageType.Arcane, true),
            S("craft_efficiency_pulse_fire", "Fire Pulse", Zone(2f, 18f, DamageType.Fire), "A pulse of flickering fire.", Stamina(14f), DamageType.Fire, true),
            S("craft_efficiency_pulse_frost", "Frost Pulse", Zone(2f, 18f, DamageType.Ice), "A pulse of crackling frost.", Stamina(14f), DamageType.Ice, true),
        };
    }
}