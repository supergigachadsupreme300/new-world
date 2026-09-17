using System.Collections.Generic;

public static partial class SkillCatalog
{
    private static void RegisterRangedDesign(DesignBank bank)
    {
        /* ──────────────── L1 (25 slots — 5 roots × 5 children each) ──────────────── */

        // Root: ranged_marksman (passive, Dexterity+4)
        bank.L1["ranged_marksman"] = new BranchSlot[]
        {
            A("ranged_steady"),
            S("ranged_marksman_snipe", "Sniper Shot", Slash(24f, DamageType.Physical), "A long-aimed shot of lethal precision.", Stamina(16f)),
            S("ranged_marksman_windshot", "Wind Shot", Zone(1.2f, 22f, DamageType.Wind), "An arrow that rides the wind.", Stamina(16f), DamageType.Wind, true),
            S("ranged_marksman_holyarrow", "Holy Arrow", Zone(1.2f, 22f, DamageType.Holy), "A blessed arrow of radiant light.", Stamina(18f), DamageType.Holy, true),
            S("ranged_marksman_frostaim", "Frost Aim", Slash(24f, DamageType.Ice), "A steady aim that chills the target.", Stamina(16f), DamageType.Ice, true),
        };

        // Root: ranged_carry (passive, AttackSpeed+2)
        bank.L1["ranged_carry"] = new BranchSlot[]
        {
            S("ranged_carry_rapid", "Rapid Fire", Slash(18f, DamageType.Physical), "A stream of fast, light arrows.", Stamina(10f)),
            S("ranged_carry_burst", "Burst Volley", Zone(1.6f, 18f, DamageType.Physical), "A short burst of arrows at close range.", Stamina(14f)),
            S("ranged_carry_litstorm", "Storm Quiver", Zone(2f, 22f, DamageType.Lightning), "A volley crackling with lightning.", Stamina(18f), DamageType.Lightning, true),
            S("ranged_carry_volley", "Broad Volley", Zone(1.8f, 20f, DamageType.Physical), "A wide-spread volley of arrows.", Stamina(14f)),
            S("ranged_carry_emerge", "Stone Shot", Slash(20f, DamageType.Earth), "An arrow tipped with hardened stone.", Stamina(14f), DamageType.Earth, true),
        };

        // Root: ranged_pierce (active, Stamina 12, Zone(1,20,Physical))
        bank.L1["ranged_pierce"] = new BranchSlot[]
        {
            A("ranged_multishot"),
            S("ranged_pierce_armor", "Armor Piercer", Slash(24f, DamageType.Physical), "A shot designed to punch through armor.", Stamina(14f)),
            S("ranged_pierce_chain", "Chain Pierce", Slash(24f, DamageType.Lightning), "A bolt that arcs between targets.", Stamina(16f), DamageType.Lightning, true),
            S("ranged_pierce_void", "Void Pierce", Slash(28f, DamageType.Dark), "A shot that tears through dark matter.", Stamina(18f), DamageType.Dark, true),
            S("ranged_pierce_deepwater", "Deep Water", Slash(24f, DamageType.Water), "A pressure-laden arrow of deep water.", Stamina(16f), DamageType.Water, true),
        };

        // Root: ranged_quickshot (active, Stamina 8, Slash(14,Physical))
        bank.L1["ranged_quickshot"] = new BranchSlot[]
        {
            S("ranged_quickshot_hasted", "Hasted Shot", Slash(18f, DamageType.Physical), "A shot loosed in a heartbeat.", Stamina(10f)),
            S("ranged_quickshot_flurry", "Arrow Flurry", Slash(22f, DamageType.Physical), "A sudden flurry of quick arrows.", Stamina(14f)),
            S("ranged_quickshot_ember", "Ember Quickshot", Slash(22f, DamageType.Fire), "A fast shot trailing embers.", Stamina(14f), DamageType.Fire, true),
            S("ranged_quickshot_chiller", "Chiller", Slash(22f, DamageType.Ice), "A quickest shot that freezes the air.", Stamina(14f), DamageType.Ice, true),
            S("ranged_quickshot_shadow", "Shadow Quickshot", Slash(26f, DamageType.Dark), "A swift shot from the dark.", Stamina(16f), DamageType.Dark, true),
        };

        // Root: ranged_flamearrow (active, Stamina 14, Zone(1.4,18,Fire))
        bank.L1["ranged_flamearrow"] = new BranchSlot[]
        {
            A("ranged_iceshot"),
            S("ranged_flamearrow_burst", "Explosive Arrow", Zone(2.2f, 22f, DamageType.Fire), "An arrow that detonates on impact.", Stamina(18f), DamageType.Fire, true),
            S("ranged_flamearrow_homing", "Homing Spark", Slash(24f, DamageType.Lightning), "A spark-charged arrow that seeks its mark.", Stamina(16f), DamageType.Lightning, true),
            S("ranged_flamearrow_wildfire", "Wildfire", Zone(2.4f, 24f, DamageType.Fire), "An arrow that spreads consuming fire.", Stamina(20f), DamageType.Fire, true),
            S("ranged_flamearrow_holyshot", "Blessed Arrow", Zone(1.6f, 22f, DamageType.Holy), "A blessed shot that casts light.", Stamina(18f), DamageType.Holy, true),
        };

        /* ──────────────── L2 (125 slots — 25 L1 parents × 5 children each) ──────────────── */

        /* ranged_steady children (all passive) */
        bank.L2["ranged_steady"] = new BranchSlot[]
        {
            S("ranged_steady_eagle", "Eagle Eye", Perk(PassivePerkType.LootLuckPercent, 6f), "The eagle's gaze uncovers 6% more spoils.", passive: true),
            S("ranged_steady_hawk", "Hawk Sight", Perk(PassivePerkType.CritChanceFlat, 3f), "A hawk's sharper sight grants 3% crit chance.", passive: true),
            S("ranged_steady_deadeye", "Dead Eye", Perk(PassivePerkType.CritDamagePercent, 10f), "Cold, dead eyes deliver 10% deadlier criticals.", passive: true),
            S("ranged_steady_surefoot", "Sure Footed", Perk(PassivePerkType.MovementSpeedPercent, 3f), "Steady footing lends 3% swifter movement.", passive: true),
            S("ranged_steady_farsight", "Farsight", Perk(PassivePerkType.FocusRegenPercent, 7f), "Far-seeing focus regenerates 7% faster.", passive: true),
        };

        /* ranged_multishot children */
        bank.L2["ranged_multishot"] = new BranchSlot[]
        {
            S("ranged_multishot_scatter", "Scatter Shot", Zone(2.2f, 20f, DamageType.Physical), "A wide fan of scattershot arrows.", Stamina(18f)),
            S("ranged_multishot_chain", "Chain Volley", Zone(2f, 22f, DamageType.Lightning), "A volley that arcs between foes.", Stamina(20f), DamageType.Lightning, true),
            S("ranged_multishot_frost", "Frost Fan", Zone(2f, 22f, DamageType.Ice), "A frost-laced fan of arrows.", Stamina(20f), DamageType.Ice, true),
            S("ranged_multishot_wind", "Windburst", Zone(2.2f, 22f, DamageType.Wind), "A wind-driving burst of arrows.", Stamina(20f), DamageType.Wind, true),
            S("ranged_multishot_shadow", "Shadow Barrage", Zone(2.2f, 24f, DamageType.Dark), "A barrage of shadow-wreathed arrows.", Stamina(22f), DamageType.Dark, true),
        };

        /* ranged_iceshot children */
        bank.L2["ranged_iceshot"] = new BranchSlot[]
        {
            S("ranged_iceshot_crystal", "Crystal Arrow", Slash(24f, DamageType.Ice), "An arrow of pure crystal frost.", Stamina(16f), DamageType.Ice, true),
            S("ranged_iceshot_lance", "Frozen Lance", Slash(22f, DamageType.Ice), "A lance of ice that impales.", Stamina(16f), DamageType.Ice, true),
            S("ranged_iceshot_blizzard", "Blizzard Shot", Zone(2f, 24f, DamageType.Ice), "A shot that summons a personal blizzard.", Stamina(18f), DamageType.Ice, true),
            S("ranged_iceshot_voidchill", "Void Chill", Slash(26f, DamageType.Dark), "A shot of unnatural, cold darkness.", Stamina(18f), DamageType.Dark, true),
            S("ranged_iceshot_arcanefrost", "Arcane Frost", Slash(24f, DamageType.Arcane), "An arcane-frost shot that baffles magics.", Stamina(16f), DamageType.Arcane, true),
        };

        /* ranged_marksman_snipe children */
        bank.L2["ranged_marksman_snipe"] = new BranchSlot[]
        {
            S("ranged_marksman_snipe_headshot", "Headshot", Slash(28f, DamageType.Physical), "A surgical head shot.", Stamina(18f)),
            S("ranged_marksman_snipe_wind", "Piercing Wind", Slash(26f, DamageType.Wind), "A wind-charged snipe that pierces.", Stamina(18f), DamageType.Wind, true),
            S("ranged_marksman_snipe_frost", "Frost Shot", Slash(26f, DamageType.Ice), "A snipe that takes the target's heat.", Stamina(18f), DamageType.Ice, true),
            S("ranged_marksman_snipe_holy", "Holy Pierce", Slash(28f, DamageType.Holy), "A radiant snipe against dark forces.", Stamina(20f), DamageType.Holy, true),
            S("ranged_marksman_snipe_dark", "Dark Snipe", Slash(30f, DamageType.Dark), "A hidden snipe from the shadows.", Stamina(20f), DamageType.Dark, true),
        };

        /* ranged_marksman_windshot children */
        bank.L2["ranged_marksman_windshot"] = new BranchSlot[]
        {
            S("ranged_marksman_windshot_gust", "Gust Arrow", Slash(24f, DamageType.Wind), "An arrow towing a gust of wind.", Stamina(16f), DamageType.Wind, true),
            S("ranged_marksman_windshot_storm", "Storm Bolt", Slash(26f, DamageType.Wind), "A bolt of wind that howls.", Stamina(18f), DamageType.Wind, true),
            S("ranged_marksman_windshot_razor", "Razor Wind", Slash(24f, DamageType.Wind), "A blade-sharp wind arrow.", Stamina(16f), DamageType.Wind, true),
            S("ranged_marksman_windshot_gale", "Gale Shot", Slash(28f, DamageType.Wind), "A shot backed by gale force.", Stamina(20f), DamageType.Wind, true),
            S("ranged_marksman_windshot_tornado", "Tornado Arrow", Zone(2f, 24f, DamageType.Wind), "An arrow that spawns a mini tornado.", Stamina(18f), DamageType.Wind, true),
        };

        /* ranged_marksman_holyarrow children */
        bank.L2["ranged_marksman_holyarrow"] = new BranchSlot[]
        {
            S("ranged_marksman_holyarrow_radiance", "Radiance", Zone(2.2f, 24f, DamageType.Holy), "A radiant burst upon impact.", Stamina(18f), DamageType.Holy, true),
            S("ranged_marksman_holyarrow_purge", "Purge", Slash(24f, DamageType.Holy), "A purging holy strike.", Stamina(16f), DamageType.Holy, true),
            S("ranged_marksman_holyarrow_shine", "Shine Shot", Slash(24f, DamageType.Holy), "An arrow that shines with holy light.", Stamina(16f), DamageType.Holy, true),
            S("ranged_marksman_holyarrow_dawn", "Dawn Piercer", Slash(26f, DamageType.Holy), "A first-light pierce.", Stamina(18f), DamageType.Holy, true),
            S("ranged_marksman_holyarrow_volley", "Light Volley", Zone(2.2f, 22f, DamageType.Holy), "A volley on shafts of light.", Stamina(18f), DamageType.Holy, true),
        };

        /* ranged_marksman_frostaim children */
        bank.L2["ranged_marksman_frostaim"] = new BranchSlot[]
        {
            S("ranged_marksman_frostaim_heart", "Frozen Heart", Slash(26f, DamageType.Ice), "A shot that stops the heart with cold.", Stamina(18f), DamageType.Ice, true),
            S("ranged_marksman_frostaim_glacier", "Glacial Aim", Slash(26f, DamageType.Ice), "A glacial-precision shot.", Stamina(18f), DamageType.Ice, true),
            S("ranged_marksman_frostaim_frostwolf", "Frostwolf", Slash(22f, DamageType.Ice), "A fast, wild ice arrow.", Stamina(16f), DamageType.Ice, true),
            S("ranged_marksman_frostaim_snow", "Snow Spike", Slash(24f, DamageType.Ice), "A spike of compacted snow ice.", Stamina(16f), DamageType.Ice, true),
            S("ranged_marksman_frostaim_winter", "Winter Arrow", Zone(2f, 24f, DamageType.Ice), "An arrow that brings winter's chill.", Stamina(18f), DamageType.Ice, true),
        };

        /* ranged_carry_rapid children */
        bank.L2["ranged_carry_rapid"] = new BranchSlot[]
        {
            S("ranged_carry_rapid_machine", "Machine Fire", Slash(20f, DamageType.Physical), "Unrelenting machine-like fire.", Stamina(14f)),
            S("ranged_carry_rapid_storm", "Storm Fire", Slash(22f, DamageType.Lightning), "Rapid storm-tinged shots.", Stamina(16f), DamageType.Lightning, true),
            S("ranged_carry_rapid_burning", "Burning Fire", Slash(22f, DamageType.Fire), "Rapid shots that ignite the air.", Stamina(16f), DamageType.Fire, true),
            S("ranged_carry_rapid_frost", "Frost Fire", Slash(22f, DamageType.Ice), "Rapid shots that leave frost.", Stamina(16f), DamageType.Ice, true),
            S("ranged_carry_rapid_void", "Void Fire", Slash(24f, DamageType.Dark), "Rapid shots devoured by darkness.", Stamina(18f), DamageType.Dark, true),
        };

        /* ranged_carry_burst children */
        bank.L2["ranged_carry_burst"] = new BranchSlot[]
        {
            S("ranged_carry_burst_tuft", "Tuft Burst", Slash(22f, DamageType.Physical), "A quick burst with physical force.", Stamina(16f)),
            S("ranged_carry_burst_flame", "Flame Burst", Zone(2f, 24f, DamageType.Fire), "A burst that blossoms into flame.", Stamina(18f), DamageType.Fire, true),
            S("ranged_carry_burst_frost", "Frost Burst", Zone(2f, 24f, DamageType.Ice), "A burst that shatters like ice.", Stamina(18f), DamageType.Ice, true),
            S("ranged_carry_burst_lightning", "Lightning Burst", Zone(2f, 24f, DamageType.Lightning), "A crackling burst of lightning.", Stamina(18f), DamageType.Lightning, true),
            S("ranged_carry_burst_earth", "Earth Burst", Zone(2.2f, 26f, DamageType.Earth), "A burst that erupts stone.", Stamina(20f), DamageType.Earth, true),
        };

        /* ranged_carry_litstorm children */
        bank.L2["ranged_carry_litstorm"] = new BranchSlot[]
        {
            S("ranged_carry_litstorm_spark", "Spark Storm", Slash(24f, DamageType.Lightning), "A storm of small sparks.", Stamina(16f), DamageType.Lightning, true),
            S("ranged_carry_litstorm_rain", "Thunder Rain", Zone(2.2f, 26f, DamageType.Lightning), "Arrows fall like thunderous rain.", Stamina(20f), DamageType.Lightning, true),
            S("ranged_carry_litstorm_tempest", "Arc Tempest", Slash(26f, DamageType.Lightning), "A tempest of arcing bolts.", Stamina(18f), DamageType.Lightning, true),
            S("ranged_carry_litstorm_quiver", "Storm Quiver", Slash(24f, DamageType.Lightning), "Arrows drawn from a storm quiver.", Stamina(16f), DamageType.Lightning, true),
            S("ranged_carry_litstorm_chain", "Chain Lightning Arrow", Slash(28f, DamageType.Lightning), "A chain of lightning in flight.", Stamina(20f), DamageType.Lightning, true),
        };

        /* ranged_carry_volley children */
        bank.L2["ranged_carry_volley"] = new BranchSlot[]
        {
            S("ranged_carry_volley_broad", "Broad Volley", Zone(2.2f, 22f, DamageType.Physical), "A broader spread of arrows.", Stamina(18f)),
            S("ranged_carry_volley_split", "Split Volley", Zone(2f, 24f, DamageType.Physical), "Arrows that split mid-flight.", Stamina(18f)),
            S("ranged_carry_volley_rail", "Rail Volley", Slash(26f, DamageType.Physical), "A volley shot like rails.", Stamina(18f)),
            S("ranged_carry_volley_ember", "Ember Volley", Zone(2f, 24f, DamageType.Fire), "A volley that leaves a trail of embers.", Stamina(18f), DamageType.Fire, true),
            S("ranged_carry_volley_frost", "Frost Volley", Zone(2f, 24f, DamageType.Ice), "A volley that cools the battlefield.", Stamina(18f), DamageType.Ice, true),
        };

        /* ranged_carry_emerge children */
        bank.L2["ranged_carry_emerge"] = new BranchSlot[]
        {
            S("ranged_carry_emerge_stone", "Stone Shot", Slash(24f, DamageType.Earth), "A heavier stone-tipped shot.", Stamina(16f), DamageType.Earth, true),
            S("ranged_carry_emerge_terra", "Terra Arrow", Zone(2f, 26f, DamageType.Earth), "An arrow that calls up earth.", Stamina(18f), DamageType.Earth, true),
            S("ranged_carry_emerge_crystal", "Crystal Shard", Slash(26f, DamageType.Earth), "A shard of crystalline rock.", Stamina(18f), DamageType.Earth, true),
            S("ranged_carry_emerge_rock", "Rock Rain", Zone(2.2f, 24f, DamageType.Earth), "A rain of pummeling rock.", Stamina(18f), DamageType.Earth, true),
            S("ranged_carry_emerge_gem", "Gem Pierce", Slash(26f, DamageType.Arcane), "A gem-faceted arrow of arcane edge.", Stamina(18f), DamageType.Arcane, true),
        };

        /* ranged_pierce_armor children */
        bank.L2["ranged_pierce_armor"] = new BranchSlot[]
        {
            S("ranged_pierce_armor_shred", "Armor Shred", Slash(28f, DamageType.Physical), "A shot that shreds platemail.", Stamina(18f)),
            S("ranged_pierce_armor_iron", "Iron Spike", Slash(26f, DamageType.Physical), "A heavy iron-bodkin spike.", Stamina(18f)),
            S("ranged_pierce_armor_shield", "Shield Breaker", Slash(26f, DamageType.Physical), "A shot meant to shatter shields.", Stamina(18f)),
            S("ranged_pierce_armor_impale", "Deep Impale", Slash(30f, DamageType.Physical), "A shot that buries itself deep.", Stamina(20f)),
            S("ranged_pierce_armor_sunder", "Sunder Shot", Slash(28f, DamageType.Earth), "A sundering shot of rock force.", Stamina(20f), DamageType.Earth, true),
        };

        /* ranged_pierce_chain children */
        bank.L2["ranged_pierce_chain"] = new BranchSlot[]
        {
            S("ranged_pierce_chain_arc", "Chain Arc", Slash(26f, DamageType.Lightning), "An arc that leaps from foe to foe.", Stamina(18f), DamageType.Lightning, true),
            S("ranged_pierce_chain_volt", "Volt Pierce", Slash(28f, DamageType.Lightning), "A pierce charged to volt capacity.", Stamina(20f), DamageType.Lightning, true),
            S("ranged_pierce_chain_storm", "Storm Impale", Slash(28f, DamageType.Wind), "A pinning shot of storm force.", Stamina(20f), DamageType.Wind, true),
            S("ranged_pierce_chain_dissolve", "Dissolve", Slash(28f, DamageType.Water), "A shot that dissolves on contact.", Stamina(20f), DamageType.Water, true),
            S("ranged_pierce_chain_wire", "Wire Shot", Slash(26f, DamageType.Lightning), "A shot that binds like wire.", Stamina(18f), DamageType.Lightning, true),
        };

        /* ranged_pierce_void children */
        bank.L2["ranged_pierce_void"] = new BranchSlot[]
        {
            S("ranged_pierce_void_impale", "Void Impale", Slash(30f, DamageType.Dark), "A void-shot that impales the soul.", Stamina(20f), DamageType.Dark, true),
            S("ranged_pierce_void_abyssal", "Abyssal Shot", Slash(32f, DamageType.Dark), "A shot drawn from the abyss.", Stamina(22f), DamageType.Dark, true),
            S("ranged_pierce_void_eye", "Void Eye", Slash(28f, DamageType.Dark), "A shot guided by the void's eye.", Stamina(20f), DamageType.Dark, true),
            S("ranged_pierce_void_lance", "Shadow Lance", Slash(28f, DamageType.Dark), "A lance of coalesced shadow.", Stamina(20f), DamageType.Dark, true),
            S("ranged_pierce_void_null", "Null Pierce", Slash(30f, DamageType.Arcane), "A pierce that nulls defenses.", Stamina(20f), DamageType.Arcane, true),
        };

        /* ranged_pierce_deepwater children */
        bank.L2["ranged_pierce_deepwater"] = new BranchSlot[]
        {
            S("ranged_pierce_deepwater_tide", "Tideline", Slash(26f, DamageType.Water), "A shot that rides the tide.", Stamina(18f), DamageType.Water, true),
            S("ranged_pierce_deepwater_abyss", "Abyss Shot", Slash(28f, DamageType.Water), "A shot from the abyssal deep.", Stamina(20f), DamageType.Water, true),
            S("ranged_pierce_deepwater_vortex", "Vortex Pierce", Zone(2f, 26f, DamageType.Water), "A spinning water vortex pierce.", Stamina(18f), DamageType.Water, true),
            S("ranged_pierce_deepwater_steam", "Steam Shot", Slash(26f, DamageType.Water), "A superheated steam arrow.", Stamina(18f), DamageType.Water, true),
            S("ranged_pierce_deepwater_trench", "Trench", Zone(2.2f, 28f, DamageType.Water), "A shot that carves a water trench.", Stamina(20f), DamageType.Water, true),
        };

        /* ranged_quickshot_hasted children */
        bank.L2["ranged_quickshot_hasted"] = new BranchSlot[]
        {
            S("ranged_quickshot_hasted_speed", "Speed Burst", Slash(20f, DamageType.Physical), "A burst of ungodly speed.", Stamina(14f)),
            S("ranged_quickshot_hasted_dart", "Storm Dart", Slash(22f, DamageType.Lightning), "A dart swift as a storm bolt.", Stamina(16f), DamageType.Lightning, true),
            S("ranged_quickshot_hasted_fire", "Fire Dart", Slash(22f, DamageType.Fire), "A dart that scorches in passing.", Stamina(16f), DamageType.Fire, true),
            S("ranged_quickshot_hasted_frost", "Frost Dart", Slash(22f, DamageType.Ice), "A dart cold to the touch.", Stamina(16f), DamageType.Ice, true),
            S("ranged_quickshot_hasted_venom", "Venom Dart", Slash(22f, DamageType.Dark), "A dart fouled with dark venom.", Stamina(16f), DamageType.Dark, true),
        };

        /* ranged_quickshot_flurry children */
        bank.L2["ranged_quickshot_flurry"] = new BranchSlot[]
        {
            S("ranged_quickshot_flurry_arrow", "Arrow Storm", Slash(24f, DamageType.Physical), "A storm of arrows at point blank.", Stamina(18f)),
            S("ranged_quickshot_flurry_gale", "Flurry Gale", Slash(24f, DamageType.Wind), "A flurry carried on a gale.", Stamina(18f), DamageType.Wind, true),
            S("ranged_quickshot_flurry_ember", "Ember Flurry", Slash(24f, DamageType.Fire), "A flurry trailing burning embers.", Stamina(18f), DamageType.Fire, true),
            S("ranged_quickshot_flurry_frost", "Frost Flurry", Slash(24f, DamageType.Ice), "A flurry that chills mid-volley.", Stamina(18f), DamageType.Ice, true),
            S("ranged_quickshot_flurry_shadow", "Shadow Flurry", Slash(26f, DamageType.Dark), "A flurry of shadowy shots.", Stamina(20f), DamageType.Dark, true),
        };

        /* ranged_quickshot_ember children */
        bank.L2["ranged_quickshot_ember"] = new BranchSlot[]
        {
            S("ranged_quickshot_ember_bolt", "Ember Bolt", Slash(24f, DamageType.Fire), "A bolt glowing like an ember.", Stamina(16f), DamageType.Fire, true),
            S("ranged_quickshot_ember_scorch", "Scorch Dart", Slash(26f, DamageType.Fire), "A dart that scorches its path.", Stamina(18f), DamageType.Fire, true),
            S("ranged_quickshot_ember_fly", "Fireflies", Zone(2f, 24f, DamageType.Fire), "A swarm of burning firefly sparks.", Stamina(18f), DamageType.Fire, true),
            S("ranged_quickshot_ember_ignite", "Ignite Shot", Slash(24f, DamageType.Fire), "A shot that catches flame on impact.", Stamina(16f), DamageType.Fire, true),
            S("ranged_quickshot_ember_singe", "Singe", Slash(26f, DamageType.Fire), "A searing singe of a shot.", Stamina(18f), DamageType.Fire, true),
        };

        /* ranged_quickshot_chiller children */
        bank.L2["ranged_quickshot_chiller"] = new BranchSlot[]
        {
            S("ranged_quickshot_chiller_dart", "Ice Dart", Slash(24f, DamageType.Ice), "A dart of sharpened ice.", Stamina(16f), DamageType.Ice, true),
            S("ranged_quickshot_chiller_bite", "Frostbite", Slash(26f, DamageType.Ice), "A shot that bites with cold.", Stamina(18f), DamageType.Ice, true),
            S("ranged_quickshot_chiller_snap", "Cold Snap", Zone(2f, 24f, DamageType.Ice), "A sudden snap of freezing cold.", Stamina(18f), DamageType.Ice, true),
            S("ranged_quickshot_chiller_glacial", "Glacial Dart", Slash(26f, DamageType.Ice), "A dart of ancient ice.", Stamina(18f), DamageType.Ice, true),
            S("ranged_quickshot_chiller_rain", "Chilling Rain", Zone(2.2f, 22f, DamageType.Ice), "A rain that freezes the ground.", Stamina(18f), DamageType.Ice, true),
        };

        /* ranged_quickshot_shadow children */
        bank.L2["ranged_quickshot_shadow"] = new BranchSlot[]
        {
            S("ranged_quickshot_shadow_bolt", "Shadow Bolt", Slash(26f, DamageType.Dark), "A bolt of living shadow.", Stamina(18f), DamageType.Dark, true),
            S("ranged_quickshot_shadow_night", "Night Arrow", Slash(28f, DamageType.Dark), "An arrow steeped in night.", Stamina(20f), DamageType.Dark, true),
            S("ranged_quickshot_shadow_fan", "Shadow Fan", Zone(2f, 24f, DamageType.Dark), "A fan of shadow projectiles.", Stamina(18f), DamageType.Dark, true),
            S("ranged_quickshot_shadow_consume", "Consume", Slash(26f, DamageType.Dark), "A shot that consumes light.", Stamina(18f), DamageType.Dark, true),
            S("ranged_quickshot_shadow_depths", "Depths Shot", Slash(28f, DamageType.Dark), "A shot rising from the depths.", Stamina(20f), DamageType.Dark, true),
        };

        /* ranged_flamearrow_burst children */
        bank.L2["ranged_flamearrow_burst"] = new BranchSlot[]
        {
            S("ranged_flamearrow_burst_bomb", "Firebomb", Zone(2.4f, 26f, DamageType.Fire), "A firebomb arrow that detonates.", Stamina(20f), DamageType.Fire, true),
            S("ranged_flamearrow_burst_mortar", "Mortar", Zone(2.6f, 28f, DamageType.Fire), "A high-arcing mortar shot.", Stamina(22f), DamageType.Fire, true),
            S("ranged_flamearrow_burst_cracker", "Firecracker", Slash(24f, DamageType.Fire), "A loud, explosive cracker shot.", Stamina(18f), DamageType.Fire, true),
            S("ranged_flamearrow_burst_shell", "Burst Shell", Zone(2.2f, 24f, DamageType.Fire), "A shell that bursts into flame.", Stamina(20f), DamageType.Fire, true),
            S("ranged_flamearrow_burst_cluster", "Cluster Bomb", Slash(28f, DamageType.Fire), "A cluster of small detonations.", Stamina(22f), DamageType.Fire, true),
        };

        /* ranged_flamearrow_homing children */
        bank.L2["ranged_flamearrow_homing"] = new BranchSlot[]
        {
            S("ranged_flamearrow_homing_seek", "Seeking Spark", Slash(26f, DamageType.Lightning), "A spark that homes in on its prey.", Stamina(18f), DamageType.Lightning, true),
            S("ranged_flamearrow_homing_chase", "Chase Bolt", Slash(26f, DamageType.Lightning), "A bolt that chases its target.", Stamina(18f), DamageType.Lightning, true),
            S("ranged_flamearrow_homing_guided", "Guided Arrow", Slash(28f, DamageType.Arcane), "An arcane-guided perfect shot.", Stamina(20f), DamageType.Arcane, true),
            S("ranged_flamearrow_homing_falcon", "Falcon Shot", Slash(26f, DamageType.Wind), "A shot swift as a falcon's dive.", Stamina(18f), DamageType.Wind, true),
            S("ranged_flamearrow_homing_mark", "Hunter's Mark", Slash(28f, DamageType.Dark), "A marked shot from the unseen.", Stamina(20f), DamageType.Dark, true),
        };

        /* ranged_flamearrow_wildfire children */
        bank.L2["ranged_flamearrow_wildfire"] = new BranchSlot[]
        {
            S("ranged_flamearrow_wildfire_arc", "Wildfire Arc", Zone(2.4f, 28f, DamageType.Fire), "An arc of spreading wildfire.", Stamina(22f), DamageType.Fire, true),
            S("ranged_flamearrow_wildfire_conflagration", "Conflagration", Slash(30f, DamageType.Fire), "A shot that becomes a conflagration.", Stamina(22f), DamageType.Fire, true),
            S("ranged_flamearrow_wildfire_cinder", "Cinder Storm", Zone(2.4f, 26f, DamageType.Fire), "A storm of burning cinders.", Stamina(20f), DamageType.Fire, true),
            S("ranged_flamearrow_wildfire_ground", "Burning Ground", Zone(2.6f, 24f, DamageType.Fire), "Ground set ablaze by the arrow.", Stamina(20f), DamageType.Fire, true),
            S("ranged_flamearrow_wildfire_emberwhirl", "Ember Whirl", Zone(2.4f, 26f, DamageType.Fire), "A whirl of dancing embers.", Stamina(20f), DamageType.Fire, true),
        };

        /* ranged_flamearrow_holyshot children */
        bank.L2["ranged_flamearrow_holyshot"] = new BranchSlot[]
        {
            S("ranged_flamearrow_holyshot_sacred", "Sacred Arrow", Slash(26f, DamageType.Holy), "A sacred shaft of pure light.", Stamina(18f), DamageType.Holy, true),
            S("ranged_flamearrow_holyshot_heavenly", "Heavenly Shot", Slash(28f, DamageType.Holy), "A shot blessed from on high.", Stamina(20f), DamageType.Holy, true),
            S("ranged_flamearrow_holyshot_volley", "Purifying Volley", Zone(2.2f, 24f, DamageType.Holy), "A volley that purifies all it touches.", Stamina(18f), DamageType.Holy, true),
            S("ranged_flamearrow_holyshot_beacon", "Beacon", Slash(24f, DamageType.Holy), "A shot that shines as a beacon.", Stamina(16f), DamageType.Holy, true),
            S("ranged_flamearrow_holyshot_sun", "Sun Arrow", Slash(28f, DamageType.Holy), "An arrow bright as the sun.", Stamina(20f), DamageType.Holy, true),
        };
    }
}