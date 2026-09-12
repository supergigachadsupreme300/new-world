using System.Collections.Generic;

public static partial class SkillCatalog
{
    private static void RegisterStealthDesign(DesignBank bank)
    {
        /* ──────────────── L1 (25 slots — 5 roots × 5 children each) ──────────────── */

        // Root: stealth_shadow (passive, Speed+2)
        bank.L1["stealth_shadow"] = new BranchSlot[]
        {
            A("stealth_veil"),
            S("stealth_shadow_mist", "Shadow Mist", Zone(1.8f, 16f, DamageType.Dark), "A concealing mist of living shadow.", Stamina(12f), DamageType.Dark, true),
            S("stealth_shadow_night", "Night Walker", Buff(StatType.Speed, 3f), "Permanent +3 Speed.", passive: true),
            S("stealth_shadow_flit", "Shadow Flit", Slash(20f, DamageType.Dark), "A momentary dash through the dark.", Stamina(14f), DamageType.Dark, true),
            S("stealth_shadow_darkarts", "Dark Arts", Buff(StatType.Dexterity, 3f), "Permanent +3 Dexterity.", passive: true),
        };

        // Root: stealth_reflexes (passive, Dexterity+2)
        bank.L1["stealth_reflexes"] = new BranchSlot[]
        {
            A("stealth_sneak"),
            S("stealth_reflexes_cat", "Cat's Reflex", Buff(StatType.Dexterity, 3f), "Permanent +3 Dexterity.", passive: true),
            S("stealth_reflexes_dodge", "Dodge", Buff(StatType.Speed, 3f), "Permanent +3 Speed.", passive: true),
            S("stealth_reflexes_counter", "Shadow Counter", Slash(20f, DamageType.Physical), "A reflexive counter that punishes an attack.", Stamina(12f)),
            S("stealth_reflexes_blink", "Blink", Slash(22f, DamageType.Dark), "A blink-fast strike that appears from nowhere.", Stamina(14f), DamageType.Dark, true),
        };

        // Root: stealth_fox (passive, Luck+3)
        bank.L1["stealth_fox"] = new BranchSlot[]
        {
            S("stealth_fox_trick", "Trickery", Buff(StatType.Dexterity, 3f), "Permanent +3 Dexterity.", passive: true),
            S("stealth_fox_sly", "Sly Intent", Slash(22f, DamageType.Physical), "A sly strike that catches the unwary.", Stamina(12f)),
            S("stealth_fox_decoy", "Decoy", Zone(1.8f, 14f, DamageType.Wind), "A wind-born decoy that misleads.", Stamina(12f), DamageType.Wind, true),
            S("stealth_fox_bluff", "Bluff", Buff(StatType.Luck, 3f), "Permanent +3 Luck.", passive: true),
            S("stealth_fox_cheat", "Cheat Death", Buff(StatType.Endurance, 3f), "Permanent +3 Endurance.", passive: true),
        };

        // Root: stealth_nimble (passive, AttackSpeed+2)
        bank.L1["stealth_nimble"] = new BranchSlot[]
        {
            A("stealth_cloak"),
            S("stealth_nimble_flurry", "Palm Flurry", Slash(20f, DamageType.Physical), "A rapid flurry of palm-jab strikes.", Stamina(12f)),
            S("stealth_nimble_dash", "Dash", Buff(StatType.Speed, 3f), "Permanent +3 Speed.", passive: true),
            S("stealth_nimble_quickstep", "Quickstep", Buff(StatType.AttackSpeed, 3f), "Permanent +3 Attack Speed.", passive: true),
            S("stealth_nimble_flickknife", "Flick Knife", Slash(22f, DamageType.Dark), "A knife flick that cuts twice.", Stamina(14f), DamageType.Dark, true),
        };

        // Root: stealth_backstab (active, Stamina 18, Slash(26,Physical))
        bank.L1["stealth_backstab"] = new BranchSlot[]
        {
            A("stealth_assassinate"),
            S("stealth_backstab_rear", "Rear Strike", Slash(24f, DamageType.Physical), "A vicious strike to exposed backs.", Stamina(16f)),
            S("stealth_backstab_silent", "Silent Kill", Slash(24f, DamageType.Dark), "A killing blow made in absolute silence.", Stamina(18f), DamageType.Dark, true),
            S("stealth_backstab_strangle", "Strangle", Buff(StatType.Dexterity, 3f), "Permanent +3 Dexterity.", passive: true),
            S("stealth_backstab_venom", "Venom Strike", Slash(24f, DamageType.Dark), "A blade coated in searing dark venom.", Stamina(16f), DamageType.Dark, true),
        };

        /* ──────────────── L2 (125 slots — 25 L1 parents × 5 children each) ──────────────── */

        /* stealth_veil children (all passive) */
        bank.L2["stealth_veil"] = new BranchSlot[]
        {
            S("stealth_veil_umbra", "Umbra", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_veil_silhouette", "Silhouette", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_veil_darkness", "Greater Darkness", Buff(StatType.Speed, 4f), "Permanent +4 Speed.", passive: true),
            S("stealth_veil_whisper", "Whisper", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("stealth_veil_night", "Night Embrace", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
        };

        /* stealth_sneak children (all passive) */
        bank.L2["stealth_sneak"] = new BranchSlot[]
        {
            S("stealth_sneak_waylay", "Waylay", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_sneak_ghost", "Ghost Step", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_sneak_harrier", "Harrier", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("stealth_sneak_walk", "Shadow Walk", Buff(StatType.Dexterity, 4f), "Permanent +4 Dexterity.", passive: true),
            S("stealth_sneak_tracker", "Tracker", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* stealth_cloak children */
        bank.L2["stealth_cloak"] = new BranchSlot[]
        {
            S("stealth_cloak_blackout", "Blackout", Zone(2f, 18f, DamageType.Dark), "A cloud that blots out all sight.", Stamina(16f), DamageType.Dark, true),
            S("stealth_cloak_screen", "Smoke Screen", Zone(2.2f, 16f, DamageType.Wind), "A wide screen of choking smoke.", Stamina(16f), DamageType.Wind, true),
            S("stealth_cloak_fog", "Poison Fog", Zone(2f, 18f, DamageType.Dark), "A fog laced with dark venom.", Stamina(18f), DamageType.Dark, true),
            S("stealth_cloak_cloud", "Noxious Cloud", Zone(2.4f, 20f, DamageType.Dark), "A billowing cloud of toxic dark.", Stamina(20f), DamageType.Dark, true),
            S("stealth_cloak_blinding", "Blinding Smoke", Zone(2.2f, 18f, DamageType.Wind), "Thick smoke that blinds and cuts.", Stamina(18f), DamageType.Wind, true),
        };

        /* stealth_assassinate children */
        bank.L2["stealth_assassinate"] = new BranchSlot[]
        {
            S("stealth_assassinate_kill", "Kill Shot", Slash(36f, DamageType.Physical), "A lethal shot against a marked target.", Stamina(26f)),
            S("stealth_assassinate_mark", "Assassin's Mark", Slash(40f, DamageType.Dark), "A dark mark that ends life.", Stamina(30f), DamageType.Dark, true),
            S("stealth_assassinate_executioner", "Executioner", Slash(38f, DamageType.Physical), "A finely-honed execution blow.", Stamina(28f)),
            S("stealth_assassinate_deep", "Deep Assassinate", Slash(42f, DamageType.Dark), "A kill that strikes the very soul.", Stamina(32f), DamageType.Dark, true),
            S("stealth_assassinate_redirect", "Redirect", Slash(36f, DamageType.Dark), "A strike redirected through shadow.", Stamina(28f), DamageType.Dark, true),
        };

        /* stealth_shadow_mist children */
        bank.L2["stealth_shadow_mist"] = new BranchSlot[]
        {
            S("stealth_shadow_mist_pool", "Mist Pool", Zone(2f, 20f, DamageType.Dark), "A pool of dark mist that swallows.", Stamina(18f), DamageType.Dark, true),
            S("stealth_shadow_mist_fog", "Shadow Fog", Zone(2.4f, 18f, DamageType.Dark), "A creeping fog of shadow.", Stamina(18f), DamageType.Dark, true),
            S("stealth_shadow_mist_wreath", "Wreath", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_shadow_mist_haze", "Haze", Zone(2.2f, 20f, DamageType.Dark), "A disorienting haze of dark.", Stamina(20f), DamageType.Dark, true),
            S("stealth_shadow_mist_smog", "Smog", Zone(2.2f, 22f, DamageType.Dark), "A suffocating smog.", Stamina(22f), DamageType.Dark, true),
        };

        /* stealth_shadow_night children (all passive) */
        bank.L2["stealth_shadow_night"] = new BranchSlot[]
        {
            S("stealth_shadow_night_speed", "Night Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_shadow_night_dex", "Night Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_shadow_night_luck", "Night Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("stealth_shadow_night_attackspeed", "Night Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("stealth_shadow_night_health", "Night Frame", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
        };

        /* stealth_shadow_flit children */
        bank.L2["stealth_shadow_flit"] = new BranchSlot[]
        {
            S("stealth_shadow_flit_zipper", "Zipper", Slash(22f, DamageType.Dark), "A zipping slash through the dark.", Stamina(16f), DamageType.Dark, true),
            S("stealth_shadow_flit_blink", "Blink Strike", Slash(24f, DamageType.Dark), "In and out in the blink of an eye.", Stamina(18f), DamageType.Dark, true),
            S("stealth_shadow_flit_dart", "Dark Dart", Slash(24f, DamageType.Dark), "A dart of condensed shadow.", Stamina(18f), DamageType.Dark, true),
            S("stealth_shadow_flit_flit", "Flit", Slash(22f, DamageType.Physical), "A fast pass that nicks and flees.", Stamina(16f)),
            S("stealth_shadow_flit_apparition", "Apparition", Slash(28f, DamageType.Dark), "A strike from a ghostly apparition.", Stamina(20f), DamageType.Dark, true),
        };

        /* stealth_shadow_darkarts children (all passive) */
        bank.L2["stealth_shadow_darkarts"] = new BranchSlot[]
        {
            S("stealth_shadow_darkarts_dex", "Artful Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_shadow_darkarts_luck", "Artful Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("stealth_shadow_darkarts_speed", "Artful Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_shadow_darkarts_attackspeed", "Artful Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("stealth_shadow_darkarts_endurance", "Artful Body", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
        };

        /* stealth_reflexes_cat children (all passive) */
        bank.L2["stealth_reflexes_cat"] = new BranchSlot[]
        {
            S("stealth_reflexes_cat_balance", "Balance", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_reflexes_cat_tight", "Tight Coil", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("stealth_reflexes_cat_land", "Cat's Landing", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_reflexes_cat_whisker", "Whisker Sense", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("stealth_reflexes_cat_spring", "Spring", Buff(StatType.Dexterity, 4f), "Permanent +4 Dexterity.", passive: true),
        };

        /* stealth_reflexes_dodge children (all passive) */
        bank.L2["stealth_reflexes_dodge"] = new BranchSlot[]
        {
            S("stealth_reflexes_dodge_speed", "Dodge Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_reflexes_dodge_agility", "Dodge Agility", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_reflexes_dodge_feet", "Nimble Feet", Buff(StatType.Speed, 4f), "Permanent +4 Speed.", passive: true),
            S("stealth_reflexes_dodge_evasion", "Evasion", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("stealth_reflexes_dodge_prepared", "Prepared", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
        };

        /* stealth_reflexes_counter children */
        bank.L2["stealth_reflexes_counter"] = new BranchSlot[]
        {
            S("stealth_reflexes_counter_cross", "Cross Counter", Slash(24f, DamageType.Physical), "A clean cross counter.", Stamina(16f)),
            S("stealth_reflexes_counter_palm", "Palm Strike", Slash(26f, DamageType.Physical), "An open-palm counter that stuns.", Stamina(18f)),
            S("stealth_reflexes_counter_spinning", "Spinning Counter", Slash(26f, DamageType.Wind), "A spinning counter of wind force.", Stamina(18f), DamageType.Wind, true),
            S("stealth_reflexes_counter_jaw", "Jaw Breaker", Slash(26f, DamageType.Physical), "An upper-cut counter to the jaw.", Stamina(18f)),
            S("stealth_reflexes_counter_hook", "Shadow Hook", Slash(28f, DamageType.Dark), "A hook strike veiled in shadow.", Stamina(20f), DamageType.Dark, true),
        };

        /* stealth_reflexes_blink children */
        bank.L2["stealth_reflexes_blink"] = new BranchSlot[]
        {
            S("stealth_reflexes_blink_blitz", "Blitz", Slash(26f, DamageType.Dark), "A blink-blitz of shadow strikes.", Stamina(18f), DamageType.Dark, true),
            S("stealth_reflexes_blink_jitter", "Jitter", Slash(24f, DamageType.Physical), "A jittering series of fast cuts.", Stamina(16f)),
            S("stealth_reflexes_blink_ghost", "Ghost Blink", Slash(28f, DamageType.Dark), "A blink so fast you're already gone.", Stamina(20f), DamageType.Dark, true),
            S("stealth_reflexes_blink_quick", "Blink Quick", Slash(24f, DamageType.Dark), "Double-blink, double-cut.", Stamina(18f), DamageType.Dark, true),
            S("stealth_reflexes_blink_shift", "Shift", Slash(28f, DamageType.Dark), "A strike shifted between blinks.", Stamina(20f), DamageType.Dark, true),
        };

        /* stealth_fox_trick children */
        bank.L2["stealth_fox_trick"] = new BranchSlot[]
        {
            S("stealth_fox_trick_switch", "Switch", Slash(24f, DamageType.Physical), "A trick strike that switches stance.", Stamina(16f)),
            S("stealth_fox_trick_sleight", "Sleight", Slash(26f, DamageType.Dark), "Sleight-of-hand made lethal.", Stamina(18f), DamageType.Dark, true),
            S("stealth_fox_trick_foxfire", "Foxfire", Zone(2f, 20f, DamageType.Fire), "A trick flame that misleads and scorches.", Stamina(18f), DamageType.Fire, true),
            S("stealth_fox_trick_jack", "Jackal", Slash(24f, DamageType.Physical), "A fast, snapping strike.", Stamina(16f)),
            S("stealth_fox_trick_misdirect", "Misdirect", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* stealth_fox_sly children */
        bank.L2["stealth_fox_sly"] = new BranchSlot[]
        {
            S("stealth_fox_sly_snipe", "Sly Snipe", Slash(26f, DamageType.Physical), "A sly, unexpected precision strike.", Stamina(18f)),
            S("stealth_fox_sly_cheap", "Cheap Shot", Slash(24f, DamageType.Physical), "A low blow that hits where it hurts.", Stamina(16f)),
            S("stealth_fox_sly_shade", "Sly Shade", Slash(28f, DamageType.Dark), "A shade-assisted sly cut.", Stamina(20f), DamageType.Dark, true),
            S("stealth_fox_sly_pinch", "Pinch", Slash(26f, DamageType.Physical), "A pin-point strike at a weak seam.", Stamina(18f)),
            S("stealth_fox_sly_foxbit", "Foxbite", Slash(28f, DamageType.Dark), "A quick bite-like strike.", Stamina(20f), DamageType.Dark, true),
        };

        /* stealth_fox_decoy children */
        bank.L2["stealth_fox_decoy"] = new BranchSlot[]
        {
            S("stealth_fox_decoy_phantom", "Phantom", Zone(2f, 18f, DamageType.Wind), "A phantom decoy that cuts.", Stamina(16f), DamageType.Wind, true),
            S("stealth_fox_decoy_afterimage", "Afterimage", Zone(2f, 20f, DamageType.Dark), "An afterimage that strikes.", Stamina(18f), DamageType.Dark, true),
            S("stealth_fox_decoy_illusion", "Illusion", Zone(2.2f, 18f, DamageType.Wind), "An illusory wind that wounds.", Stamina(16f), DamageType.Wind, true),
            S("stealth_fox_decoy_feint", "Feint", Zone(2f, 20f, DamageType.Physical), "A feint that turns real.", Stamina(16f)),
            S("stealth_fox_decoy_shadowplay", "Shadowplay", Zone(2.4f, 22f, DamageType.Dark), "Shadows play at being killers.", Stamina(20f), DamageType.Dark, true),
        };

        /* stealth_fox_bluff children (all passive) */
        bank.L2["stealth_fox_bluff"] = new BranchSlot[]
        {
            S("stealth_fox_bluff_luck", "Bluff Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("stealth_fox_bluff_dex", "Bluff Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_fox_bluff_speed", "Bluff Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_fox_bluff_wisdom", "Sharp Wit", Buff(StatType.Wisdom, 5f), "Permanent +5 Wisdom.", passive: true),
            S("stealth_fox_bluff_intelligence", "Cunning", Buff(StatType.Intelligence, 5f), "Permanent +5 Intelligence.", passive: true),
        };

        /* stealth_fox_cheat children (all passive) */
        bank.L2["stealth_fox_cheat"] = new BranchSlot[]
        {
            S("stealth_fox_cheat_endurance", "Cheating Death", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("stealth_fox_cheat_health", "Slippery Health", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
            S("stealth_fox_cheat_second", "Second Wind", Buff(StatType.Endurance, 4f), "Permanent +4 Endurance.", passive: true),
            S("stealth_fox_cheat_defense", "Evasive Defense", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("stealth_fox_cheat_luck", "Lucky Escape", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* stealth_nimble_flurry children */
        bank.L2["stealth_nimble_flurry"] = new BranchSlot[]
        {
            S("stealth_nimble_flurry_storm", "Palm Storm", Slash(24f, DamageType.Physical), "A storm of palm strikes.", Stamina(16f)),
            S("stealth_nimble_flurry_barrage", "Flurry Barrage", Slash(24f, DamageType.Physical), "A relentless barrage.", Stamina(18f)),
            S("stealth_nimble_flurry_burning", "Burning Flurry", Slash(26f, DamageType.Fire), "A flurry wreathed in flame.", Stamina(18f), DamageType.Fire, true),
            S("stealth_nimble_flurry_frost", "Frost Flurry", Slash(26f, DamageType.Ice), "A flurry that chills the air.", Stamina(18f), DamageType.Ice, true),
            S("stealth_nimble_flurry_shadow", "Shadow Flurry", Slash(26f, DamageType.Dark), "A flurry of shadow palms.", Stamina(20f), DamageType.Dark, true),
        };

        /* stealth_nimble_dash children (all passive) */
        bank.L2["stealth_nimble_dash"] = new BranchSlot[]
        {
            S("stealth_nimble_dash_speed", "Dash Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_nimble_dash_attackspeed", "Dash Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("stealth_nimble_dash_dex", "Dash Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_nimble_dash_endurance", "Dash Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("stealth_nimble_dash_health", "Dash Hardiness", Buff(StatType.Health, 5f), "Permanent +5 Health.", passive: true),
        };

        /* stealth_nimble_quickstep children (all passive) */
        bank.L2["stealth_nimble_quickstep"] = new BranchSlot[]
        {
            S("stealth_nimble_quickstep_attackspeed", "Speed Step", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("stealth_nimble_quickstep_dex", "Step Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_nimble_quickstep_speed", "Step Speed", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("stealth_nimble_quickstep_luck", "Step Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
            S("stealth_nimble_quickstep_precision", "Step Precision", Buff(StatType.Dexterity, 4f), "Permanent +4 Dexterity.", passive: true),
        };

        /* stealth_nimble_flickknife children */
        bank.L2["stealth_nimble_flickknife"] = new BranchSlot[]
        {
            S("stealth_nimble_flickknife_double", "Double Flick", Slash(26f, DamageType.Dark), "A flick that cuts twice in one motion.", Stamina(18f), DamageType.Dark, true),
            S("stealth_nimble_flickknife_feather", "Feather Flick", Slash(24f, DamageType.Physical), "A light flick that finds the veins.", Stamina(16f)),
            S("stealth_nimble_flickknife_blade", "Blade Flick", Slash(26f, DamageType.Physical), "A flick that sends the blade spinning.", Stamina(18f)),
            S("stealth_nimble_flickknife_hot", "Hot Flick", Slash(28f, DamageType.Fire), "A searing flick of a hot blade.", Stamina(20f), DamageType.Fire, true),
            S("stealth_nimble_flickknife_venom", "Venom Flick", Slash(28f, DamageType.Dark), "A flick delivering dark venom.", Stamina(20f), DamageType.Dark, true),
        };

        /* stealth_backstab_rear children */
        bank.L2["stealth_backstab_rear"] = new BranchSlot[]
        {
            S("stealth_backstab_rear_spine", "Spine Strike", Slash(28f, DamageType.Physical), "A strike aimed at the spine.", Stamina(18f)),
            S("stealth_backstab_rear_hamstring", "Hamstring", Slash(26f, DamageType.Physical), "A rear cut that severs sinew.", Stamina(18f)),
            S("stealth_backstab_rear_kidney", "Kidney Blow", Slash(26f, DamageType.Physical), "A crippling blow to the kidneys.", Stamina(18f)),
            S("stealth_backstab_rear_burning", "Burning Rear", Slash(28f, DamageType.Fire), "A rear strike wreathed in fire.", Stamina(20f), DamageType.Fire, true),
            S("stealth_backstab_rear_chill", "Chill Rear", Slash(28f, DamageType.Ice), "A rear strike that numbs.", Stamina(20f), DamageType.Ice, true),
        };

        /* stealth_backstab_silent children */
        bank.L2["stealth_backstab_silent"] = new BranchSlot[]
        {
            S("stealth_backstab_silent_death", "Silent Death", Slash(28f, DamageType.Dark), "A death that makes no sound.", Stamina(22f), DamageType.Dark, true),
            S("stealth_backstab_silent_muffle", "Muffled Kill", Slash(26f, DamageType.Dark), "A kill muffled in shadow.", Stamina(20f), DamageType.Dark, true),
            S("stealth_backstab_silent_throat", "Throat Cut", Slash(26f, DamageType.Physical), "A silent throat cut.", Stamina(20f)),
            S("stealth_backstab_silent_darkness", "Silent Darkness", Slash(28f, DamageType.Dark), "Darkness that kills quietly.", Stamina(22f), DamageType.Dark, true),
            S("stealth_backstab_silent_echo", "Silent Echo", Slash(30f, DamageType.Dark), "A silent strike that echoes in death.", Stamina(24f), DamageType.Dark, true),
        };

        /* stealth_backstab_strangle children (all passive) */
        bank.L2["stealth_backstab_strangle"] = new BranchSlot[]
        {
            S("stealth_backstab_strangle_dex", "Strangle Dexterity", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("stealth_backstab_strangle_strength", "Strangle Strength", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("stealth_backstab_strangle_endurance", "Strangle Endurance", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("stealth_backstab_strangle_attackspeed", "Strangle Hands", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("stealth_backstab_strangle_luck", "Strangle Luck", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* stealth_backstab_venom children */
        bank.L2["stealth_backstab_venom"] = new BranchSlot[]
        {
            S("stealth_backstab_venom_pinch", "Venom Pinch", Slash(28f, DamageType.Dark), "A pinching strike laced with venom.", Stamina(20f), DamageType.Dark, true),
            S("stealth_backstab_venom_drip", "Drip", Slash(26f, DamageType.Dark), "Venom that drips from the blade.", Stamina(18f), DamageType.Dark, true),
            S("stealth_backstab_venom_fang", "Fang Strike", Slash(28f, DamageType.Dark), "A strike like a serpent's fang.", Stamina(20f), DamageType.Dark, true),
            S("stealth_backstab_venom_bite", "Venom Bite", Slash(28f, DamageType.Dark), "A bite of pure dark venom.", Stamina(20f), DamageType.Dark, true),
            S("stealth_backstab_venom_sudden", "Sudden Venom", Slash(30f, DamageType.Dark), "A sudden envenoming strike.", Stamina(22f), DamageType.Dark, true),
        };
    }
}