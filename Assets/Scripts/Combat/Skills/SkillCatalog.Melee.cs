using System.Collections.Generic;

public static partial class SkillCatalog
{
    private static void RegisterMeleeDesign(DesignBank bank)
    {
        /* ──────────────── L1 (25 slots — 5 roots × 5 children each) ──────────────── */

        // Root: melee_heavy_mastery (passive, Strength+3)
        bank.L1["melee_heavy_mastery"] = new BranchSlot[]
        {
            A("melee_tough"),
            S("melee_heavy_sunder", "Sunder", Slash(24f, DamageType.Physical), "A blow that tears through armor.", Stamina(14f)),
            S("melee_heavy_crag", "Crag Breaker", Slash(26f, DamageType.Earth), "A downward smash that cracks the ground.", Stamina(16f), DamageType.Earth, true),
            S("melee_heavy_goliath", "Goliath Stance", Buff(StatType.Endurance, 3f), "Permanent +3 Endurance.", passive: true),
            S("melee_heavy_skullcrush", "Skullcrush", Zone(2f, 22f, DamageType.Physical), "A devastating overhead strike.", Stamina(16f)),
        };

        // Root: melee_finesse (passive, Dexterity+3)
        bank.L1["melee_finesse"] = new BranchSlot[]
        {
            A("melee_couter"),
            S("melee_finesse_expose", "Expose Weakness", Slash(22f, DamageType.Physical), "A surgical strike that finds the weak seam.", Stamina(12f)),
            S("melee_finesse_flick", "Lightning Flick", Slash(26f, DamageType.Lightning), "A blade flicker as fast as lightning.", Stamina(14f), DamageType.Lightning, true),
            S("melee_finesse_mirage", "Mirage Blade", Slash(24f, DamageType.Dark), "A feint that cuts from a shadow after-image.", Stamina(16f), DamageType.Dark, true),
            S("melee_finesse_rhythm", "Blade Rhythm", Buff(StatType.Dexterity, 3f), "Permanent +3 Dexterity.", passive: true),
        };

        // Root: melee_cleave (active, Stamina 10, Slash 18 Physical)
        bank.L1["melee_cleave"] = new BranchSlot[]
        {
            A("melee_whirlwind"),
            A("melee_berserk"),
            S("melee_cleave_rending", "Rending Cleave", Slash(26f, DamageType.Physical), "A cleave that bites deep and tears.", Stamina(16f)),
            S("melee_cleave_ember", "Ember Sweep", Slash(28f, DamageType.Fire), "A cleave trailing a curtain of embers.", Stamina(18f), DamageType.Fire, true),
            S("melee_cleave_tempest", "Tempest Cut", Zone(1.8f, 22f, DamageType.Wind), "A sweeping cut that carries a storm.", Stamina(18f), DamageType.Wind, true),
        };

        // Root: melee_lunge (active, Stamina 12, WeaponSkillEffect)
        bank.L1["melee_lunge"] = new BranchSlot[]
        {
            S("melee_lunge_piercer", "Piercer", Slash(20f, DamageType.Physical), "A single lunging thrust aimed at vitals.", Stamina(10f)),
            S("melee_lunge_bullrush", "Bull Rush", Slash(22f, DamageType.Physical), "A lowered-shoulder lunge that bowls foes over.", Stamina(14f)),
            S("melee_lunge_hotsteel", "Hot Steel", Slash(24f, DamageType.Fire), "A lunge searing the wound as it enters.", Stamina(16f), DamageType.Fire, true),
            S("melee_lunge_shockjab", "Jab of Static", Slash(24f, DamageType.Lightning), "A quick lunge crackling with static.", Stamina(14f), DamageType.Lightning, true),
            S("melee_lunge_longarm", "Long Arm", Slash(28f, DamageType.Ice), "An impossibly extended lunge chilling the target.", Stamina(18f), DamageType.Ice, true),
        };

        // Root: melee_shieldbash (active, Stamina 14, Slash 22 Physical)
        bank.L1["melee_shieldbash"] = new BranchSlot[]
        {
            S("melee_shield_slam", "Shield Slam", Slash(24f, DamageType.Physical), "A deafening full-body shield slam.", Stamina(14f)),
            S("melee_shield_wallspike", "Spiked Wall", Zone(2f, 20f, DamageType.Physical), "A bristling shield line that lashes out.", Stamina(16f)),
            S("melee_shield_sunwall", "Sunwall", Zone(2.2f, 24f, DamageType.Holy), "A gleaming shield flare of holy light.", Stamina(18f), DamageType.Holy, true),
            S("melee_shield_ironrip", "Iron Riposte", Slash(22f, DamageType.Physical), "Brace and punish an enemy that hit you.", Stamina(14f)),
            S("melee_shield_earthwarden", "Earthwarden", Zone(2f, 22f, DamageType.Earth), "Strike the ground, sending rubble against foes.", Stamina(18f), DamageType.Earth, true),
        };

        /* ──────────────── L2 (125 slots — 25 L1 parents × 5 children each) ──────────────── */

        /* melee_tough children (all passive) */
        bank.L2["melee_tough"] = new BranchSlot[]
        {
            S("melee_tough_resolute", "Resolute Guard", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("melee_tough_siege", "Siegebreaker", Buff(StatType.Health, 5f), "Permanent +5 HP.", passive: true),
            S("melee_tough_titan", "Titan Plate", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("melee_tough_ironclad", "Ironclad", Buff(StatType.Defense, 6f), "Permanent +6 Defense.", passive: true),
            S("melee_tough_fortress", "Fortress Core", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
        };

        /* melee_heavy_sunder children */
        bank.L2["melee_heavy_sunder"] = new BranchSlot[]
        {
            S("melee_sunder_razor", "Razor Sunder", Slash(28f, DamageType.Physical), "A sundering blow that shears clean.", Stamina(18f)),
            S("melee_sunder_blazing", "Blazing Sunder", Slash(30f, DamageType.Fire), "Fire erupts from the sundered wound.", Stamina(20f), DamageType.Fire, true),
            S("melee_sunder_frost", "Frostbite Sunder", Slash(30f, DamageType.Ice), "A sunder that freezes as it tears.", Stamina(20f), DamageType.Ice, true),
            S("melee_sunder_rending", "Rending Sunder", Zone(2.2f, 28f, DamageType.Physical), "A sunder that rips through a wide arc.", Stamina(20f)),
            S("melee_sunder_abyssal", "Abyssal Sunder", Slash(34f, DamageType.Dark), "Dark energy tears the target apart.", Stamina(22f), DamageType.Dark, true),
        };

        /* melee_heavy_crag children */
        bank.L2["melee_heavy_crag"] = new BranchSlot[]
        {
            S("melee_crag_fissure", "Fissure Strike", Slash(30f, DamageType.Earth), "A smash that splits the earth.", Stamina(20f), DamageType.Earth, true),
            S("melee_crag_magma", "Magma Crag", Zone(2.4f, 28f, DamageType.Fire), "Molten rock erupts from the impact.", Stamina(22f), DamageType.Fire, true),
            S("melee_crag_tremor", "Tremor Slam", Zone(2.6f, 26f, DamageType.Earth), "A ground-shaking slam that staggers foes.", Stamina(20f), DamageType.Earth, true),
            S("melee_crag_obsidian", "Obsidian Edge", Slash(32f, DamageType.Dark), "A jagged strike of dark volcanic glass.", Stamina(22f), DamageType.Dark, true),
            S("melee_crag_boulder", "Boulder Crush", Slash(30f, DamageType.Physical), "A boulder-heavy overhead blow.", Stamina(20f)),
        };

        /* melee_heavy_goliath children (all passive) */
        bank.L2["melee_heavy_goliath"] = new BranchSlot[]
        {
            S("melee_goliath_resilience", "Resilience of Stone", Buff(StatType.Health, 5f), "Permanent +5 HP.", passive: true),
            S("melee_goliath_fortress", "Living Fortress", Buff(StatType.Defense, 5f), "Permanent +5 Defense.", passive: true),
            S("melee_goliath_molten", "Molten Core", Buff(StatType.Strength, 5f), "Permanent +5 Strength.", passive: true),
            S("melee_goliath_will", "Iron Will", Buff(StatType.Endurance, 5f), "Permanent +5 Endurance.", passive: true),
            S("melee_goliath_unbroken", "Unbroken", Buff(StatType.Health, 6f), "Permanent +6 HP.", passive: true),
        };

        /* melee_heavy_skullcrush children */
        bank.L2["melee_heavy_skullcrush"] = new BranchSlot[]
        {
            S("melee_skull_maul", "Skull Maul", Zone(2.2f, 26f, DamageType.Physical), "A bone-crushing overhead maul.", Stamina(20f)),
            S("melee_skull_volcanic", "Volcanic Crash", Zone(2.4f, 28f, DamageType.Fire), "A molten-impact crash that scorches the area.", Stamina(22f), DamageType.Fire, true),
            S("melee_skull_quake", "Quake Strike", Zone(2.6f, 28f, DamageType.Earth), "A seismic smash that sends shockwaves outward.", Stamina(22f), DamageType.Earth, true),
            S("melee_skull_darkcrush", "Dark Crush", Zone(2.2f, 30f, DamageType.Dark), "A void-infused crush that devours light.", Stamina(24f), DamageType.Dark, true),
            S("melee_skull_boneshatter", "Boneshatter", Slash(32f, DamageType.Physical), "A devastating blow that shatters bone.", Stamina(22f)),
        };

        /* melee_couter children */
        bank.L2["melee_couter"] = new BranchSlot[]
        {
            S("melee_couter_flurry", "Counter Flurry", Slash(28f, DamageType.Physical), "A rapid series of counter-blows.", Stamina(18f)),
            S("melee_couter_arcane", "Arcane Riposte", Slash(30f, DamageType.Arcane), "A magical counter that bends reality.", Stamina(20f), DamageType.Arcane, true),
            S("melee_couter_thunder", "Thunder Counter", Slash(30f, DamageType.Lightning), "A crackling counter that stuns on contact.", Stamina(20f), DamageType.Lightning, true),
            S("melee_couter_viper", "Viper Riposte", Slash(30f, DamageType.Dark), "A venomous counter that lingers.", Stamina(20f), DamageType.Dark, true),
            S("melee_couter_shadow", "Shadow Counter", Slash(34f, DamageType.Dark), "A counter strike from the shadows themselves.", Stamina(22f), DamageType.Dark, true),
        };

        /* melee_finesse_expose children */
        bank.L2["melee_finesse_expose"] = new BranchSlot[]
        {
            S("melee_expose_sever", "Sever Weakness", Slash(26f, DamageType.Physical), "A cut that opens a deep gash.", Stamina(16f)),
            S("melee_expose_ember", "Ember Expose", Slash(28f, DamageType.Fire), "A searing strike that reveals hidden veins.", Stamina(18f), DamageType.Fire, true),
            S("melee_expose_venom", "Venom Expose", Slash(28f, DamageType.Dark), "A venomous strike that seeps into the wound.", Stamina(18f), DamageType.Dark, true),
            S("melee_expose_rend", "Rend Open", Slash(30f, DamageType.Physical), "A brutal rending that tears armor aside.", Stamina(20f)),
            S("melee_expose_void", "Void Slice", Slash(32f, DamageType.Arcane), "A cut that shears through the very fabric of defenses.", Stamina(22f), DamageType.Arcane, true),
        };

        /* melee_finesse_flick children */
        bank.L2["melee_finesse_flick"] = new BranchSlot[]
        {
            S("melee_flick_spark", "Spark Flick", Slash(30f, DamageType.Lightning), "A lightning-fast blade flick.", Stamina(18f), DamageType.Lightning, true),
            S("melee_flick_blur", "Blur Strike", Slash(28f, DamageType.Wind), "A blur of motion that cuts before the eye registers.", Stamina(16f), DamageType.Wind, true),
            S("melee_flick_tempest", "Tempest Flick", Slash(32f, DamageType.Wind), "A flick that carries gale-force behind it.", Stamina(20f), DamageType.Wind, true),
            S("melee_flick_frost", "Frost Flick", Slash(32f, DamageType.Ice), "A freezing blade flick that numbs on contact.", Stamina(20f), DamageType.Ice, true),
            S("melee_flick_shadow", "Shadow Flick", Slash(34f, DamageType.Dark), "A blade that flickers through shadows.", Stamina(22f), DamageType.Dark, true),
        };

        /* melee_finesse_mirage children */
        bank.L2["melee_finesse_mirage"] = new BranchSlot[]
        {
            S("melee_mirage_phantom", "Phantom Strike", Slash(30f, DamageType.Dark), "An after-image that strikes independently.", Stamina(20f), DamageType.Dark, true),
            S("melee_mirage_echo", "Echo Blade", Slash(28f, DamageType.Physical), "A blade that echoes its cut twice.", Stamina(18f)),
            S("melee_mirage_doppel", "Doppelganger", Slash(30f, DamageType.Arcane), "A mirror-copy feint that strikes from behind.", Stamina(20f), DamageType.Arcane, true),
            S("melee_mirage_shade", "Shade Cut", Slash(32f, DamageType.Dark), "A cut delivered from a pool of darkness.", Stamina(22f), DamageType.Dark, true),
            S("melee_mirage_mist", "Mist Veil", Zone(2.2f, 28f, DamageType.Wind), "A sweeping mirage that disorients and cuts.", Stamina(20f), DamageType.Wind, true),
        };

        /* melee_finesse_rhythm children (all passive) */
        bank.L2["melee_finesse_rhythm"] = new BranchSlot[]
        {
            S("melee_rhythm_tempo", "Blade Tempo", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("melee_rhythm_grace", "Combat Grace", Buff(StatType.Speed, 5f), "Permanent +5 Speed.", passive: true),
            S("melee_rhythm_reflex", "Refined Reflex", Buff(StatType.Dexterity, 5f), "Permanent +5 Dexterity.", passive: true),
            S("melee_rhythm_fluid", "Fluid Motion", Buff(StatType.AttackSpeed, 5f), "Permanent +5 Attack Speed.", passive: true),
            S("melee_rhythm_precision", "Absolute Precision", Buff(StatType.Luck, 5f), "Permanent +5 Luck.", passive: true),
        };

        /* melee_whirlwind children */
        bank.L2["melee_whirlwind"] = new BranchSlot[]
        {
            S("melee_whirl_fervor", "Fervor Spin", Zone(2.4f, 24f, DamageType.Wind), "A furious spin powered by adrenaline.", Stamina(18f), DamageType.Wind, true),
            S("melee_whirl_flame", "Flame Vortex", Zone(2.4f, 26f, DamageType.Fire), "A spinning trail of fire.", Stamina(20f), DamageType.Fire, true),
            S("melee_whirl_frost", "Frost Cyclone", Zone(2.4f, 26f, DamageType.Ice), "A freezing whirlwind that chills all nearby.", Stamina(20f), DamageType.Ice, true),
            S("melee_whirl_razor", "Razor Vortex", Zone(2.2f, 22f, DamageType.Physical), "A tighter, sharper spin that cuts deeper.", Stamina(18f)),
            S("melee_whirl_void", "Void Cyclone", Zone(2.6f, 28f, DamageType.Dark), "A dark maelstrom that devours all in its path.", Stamina(22f), DamageType.Dark, true),
        };

        /* melee_berserk children */
        bank.L2["melee_berserk"] = new BranchSlot[]
        {
            S("melee_berserk_reckless", "Reckless Fury", Slash(30f, DamageType.Fire), "A wild, uncontrolled slash of pure rage.", Stamina(22f), DamageType.Fire, true),
            S("melee_berserk_blood", "Blood Frenzy", Slash(28f, DamageType.Physical), "A frenzy that feeds on the scent of blood.", Stamina(20f)),
            S("melee_berserk_searing", "Searing Burn", Zone(2.2f, 26f, DamageType.Fire), "A burning berserk strike that scorches the area.", Stamina(20f), DamageType.Fire, true),
            S("melee_berserk_rage", "Berserker Rage", Slash(34f, DamageType.Dark), "Pure dark fury that ignores all pain.", Stamina(24f), DamageType.Dark, true),
            S("melee_berserk_storm", "Berserker Storm", Zone(2.4f, 28f, DamageType.Wind), "A storm of berserk strikes that buffet foes.", Stamina(22f), DamageType.Wind, true),
        };

        /* melee_cleave_rending children */
        bank.L2["melee_cleave_rending"] = new BranchSlot[]
        {
            S("melee_rending_deep", "Deep Rending", Slash(30f, DamageType.Physical), "A rending cleave that opens a gaping wound.", Stamina(20f)),
            S("melee_rending_flame", "Flame Rend", Slash(32f, DamageType.Fire), "Fire tears through the target.", Stamina(22f), DamageType.Fire, true),
            S("melee_rending_ice", "Ice Rend", Slash(32f, DamageType.Ice), "A rending cleave that freezes as it cuts.", Stamina(22f), DamageType.Ice, true),
            S("melee_rending_storm", "Storm Rend", Zone(2.2f, 28f, DamageType.Wind), "Wind-infused rending that lashes outward.", Stamina(20f), DamageType.Wind, true),
            S("melee_rending_void", "Void Rend", Slash(36f, DamageType.Dark), "A rending blow that tears through the void.", Stamina(24f), DamageType.Dark, true),
        };

        /* melee_cleave_ember children */
        bank.L2["melee_cleave_ember"] = new BranchSlot[]
        {
            S("melee_ember_burst", "Ember Burst", Zone(2.4f, 28f, DamageType.Fire), "A burst of embers that scorch the area.", Stamina(20f), DamageType.Fire, true),
            S("melee_ember_magma", "Magma Sweep", Zone(2.6f, 30f, DamageType.Fire), "A sweeping arc of molten rock.", Stamina(22f), DamageType.Fire, true),
            S("melee_ember_cinder", "Cinder Cleave", Slash(30f, DamageType.Fire), "A cleave trailing smoldering cinders.", Stamina(20f), DamageType.Fire, true),
            S("melee_ember_inferno", "Inferno Arc", Zone(2.4f, 32f, DamageType.Fire), "An all-consuming inferno arc.", Stamina(24f), DamageType.Fire, true),
            S("melee_ember_vapor", "Vapor Sweep", Zone(2.2f, 28f, DamageType.Water), "A steam-laced sweep that scalds on contact.", Stamina(20f), DamageType.Water, true),
        };

        /* melee_cleave_tempest children */
        bank.L2["melee_cleave_tempest"] = new BranchSlot[]
        {
            S("melee_tempest_gale", "Gale Cleave", Zone(2.4f, 26f, DamageType.Wind), "A wind-driven cleave that scatters foes.", Stamina(20f), DamageType.Wind, true),
            S("melee_tempest_squall", "Squall Strike", Zone(2.2f, 24f, DamageType.Wind), "A sudden, violent wind strike.", Stamina(18f), DamageType.Wind, true),
            S("melee_tempest_hurricane", "Hurricane Arc", Zone(2.6f, 30f, DamageType.Wind), "A sweeping arc of hurricane force.", Stamina(22f), DamageType.Wind, true),
            S("melee_tempest_thunder", "Thunder Sweep", Zone(2.4f, 28f, DamageType.Lightning), "A thunder-infused cleave that shocks.", Stamina(22f), DamageType.Lightning, true),
            S("melee_tempest_frost", "Frost Sweep", Zone(2.4f, 28f, DamageType.Ice), "A freezing wind sweep that chills all it touches.", Stamina(22f), DamageType.Ice, true),
        };

        /* melee_lunge_piercer children */
        bank.L2["melee_lunge_piercer"] = new BranchSlot[]
        {
            S("melee_piercer_deep", "Deep Pierce", Slash(24f, DamageType.Physical), "A piercing thrust that reaches vitals.", Stamina(14f)),
            S("melee_piercer_flame", "Flame Thrust", Slash(26f, DamageType.Fire), "A fiery lunge that sears as it pierces.", Stamina(16f), DamageType.Fire, true),
            S("melee_piercer_frost", "Frost Thrust", Slash(26f, DamageType.Ice), "A chilling thrust that freezes the wound.", Stamina(16f), DamageType.Ice, true),
            S("melee_piercer_static", "Static Pierce", Slash(28f, DamageType.Lightning), "An electrified thrust that arcs on contact.", Stamina(18f), DamageType.Lightning, true),
            S("melee_piercer_void", "Void Pierce", Slash(30f, DamageType.Dark), "A thrust that tears through dimensional barriers.", Stamina(20f), DamageType.Dark, true),
        };

        /* melee_lunge_bullrush children */
        bank.L2["melee_lunge_bullrush"] = new BranchSlot[]
        {
            S("melee_bullrush_tackle", "Tackle", Slash(26f, DamageType.Physical), "A blunt-force charging tackle.", Stamina(16f)),
            S("melee_bullrush_charging", "Charging Bull", Slash(28f, DamageType.Earth), "An earth-shaking charging slam.", Stamina(18f), DamageType.Earth, true),
            S("melee_bullrush_blazing", "Blazing Charge", Slash(30f, DamageType.Fire), "A fire-wreathed charging rush.", Stamina(20f), DamageType.Fire, true),
            S("melee_bullrush_frost", "Frost Charge", Slash(30f, DamageType.Ice), "A freezing charge that leaves ice in its wake.", Stamina(20f), DamageType.Ice, true),
            S("melee_bullrush_thunder", "Thunder Rush", Slash(32f, DamageType.Lightning), "A crackling charge that stuns on impact.", Stamina(22f), DamageType.Lightning, true),
        };

        /* melee_lunge_hotsteel children */
        bank.L2["melee_lunge_hotsteel"] = new BranchSlot[]
        {
            S("melee_hotsteel_smolder", "Smoldering Steel", Slash(28f, DamageType.Fire), "A searing lunge that leaves embers in the wound.", Stamina(18f), DamageType.Fire, true),
            S("melee_hotsteel_infernal", "Infernal Lunge", Slash(30f, DamageType.Fire), "An inferno-powered thrust.", Stamina(20f), DamageType.Fire, true),
            S("melee_hotsteel_molten", "Molten Jab", Slash(30f, DamageType.Fire), "A jab of molten steel that burns deep.", Stamina(20f), DamageType.Fire, true),
            S("melee_hotsteel_volcanic", "Volcanic Thrust", Slash(32f, DamageType.Earth), "A volcanic thrust that spews molten earth.", Stamina(22f), DamageType.Earth, true),
            S("melee_hotsteel_searing", "Searing Thrust", Slash(30f, DamageType.Fire), "A thrust that sears through flesh and armor.", Stamina(20f), DamageType.Fire, true),
        };

        /* melee_lunge_shockjab children */
        bank.L2["melee_lunge_shockjab"] = new BranchSlot[]
        {
            S("melee_shockjab_spark", "Spark Jab", Slash(28f, DamageType.Lightning), "A quick jab sparking with static.", Stamina(18f), DamageType.Lightning, true),
            S("melee_shockjab_bolt", "Bolt Lunge", Slash(30f, DamageType.Lightning), "A bolt-fast lunge that shocks on contact.", Stamina(20f), DamageType.Lightning, true),
            S("melee_shockjab_arc", "Arc Strike", Slash(30f, DamageType.Lightning), "A lunge that arcs electricity between targets.", Stamina(20f), DamageType.Lightning, true),
            S("melee_shockjab_storm", "Storm Jab", Slash(32f, DamageType.Wind), "A wind-charged jab that howls on impact.", Stamina(22f), DamageType.Wind, true),
            S("melee_shockjab_thunder", "Thunder Lunge", Slash(34f, DamageType.Lightning), "A thunderous lunge that shakes the ground.", Stamina(24f), DamageType.Lightning, true),
        };

        /* melee_lunge_longarm children */
        bank.L2["melee_lunge_longarm"] = new BranchSlot[]
        {
            S("melee_longarm_glacial", "Glacial Reach", Slash(30f, DamageType.Ice), "An extended lunge that trails glacial frost.", Stamina(20f), DamageType.Ice, true),
            S("melee_longarm_frost", "Frost Lance", Slash(28f, DamageType.Ice), "A frost-tipped lance that slows on hit.", Stamina(18f), DamageType.Ice, true),
            S("melee_longarm_abyssal", "Abyssal Reach", Slash(32f, DamageType.Dark), "A reach that extends through the abyss.", Stamina(22f), DamageType.Dark, true),
            S("melee_longarm_void", "Void Reach", Slash(34f, DamageType.Dark), "A lunge that pierces through dimensional voids.", Stamina(24f), DamageType.Dark, true),
            S("melee_longarm_static", "Static Reach", Slash(30f, DamageType.Lightning), "An electrified reach that chains on impact.", Stamina(20f), DamageType.Lightning, true),
        };

        /* melee_shield_slam children */
        bank.L2["melee_shield_slam"] = new BranchSlot[]
        {
            S("melee_slam_aftershock", "Aftershock Slam", Zone(2.4f, 28f, DamageType.Physical), "A slam that sends aftershocks through the ground.", Stamina(20f)),
            S("melee_slam_flame", "Flame Slam", Zone(2.4f, 28f, DamageType.Fire), "A fiery shield slam that scorches on impact.", Stamina(20f), DamageType.Fire, true),
            S("melee_slam_frost", "Frost Slam", Zone(2.4f, 28f, DamageType.Ice), "A freezing shield slam that chills all nearby.", Stamina(20f), DamageType.Ice, true),
            S("melee_slam_thunder", "Thunder Slam", Zone(2.6f, 30f, DamageType.Lightning), "A thunderous slam that shocks enemies.", Stamina(22f), DamageType.Lightning, true),
            S("melee_slam_earth", "Earth Slam", Zone(2.6f, 30f, DamageType.Earth), "A ground-shattering slam of earthen force.", Stamina(22f), DamageType.Earth, true),
        };

        /* melee_shield_wallspike children */
        bank.L2["melee_shield_wallspike"] = new BranchSlot[]
        {
            S("melee_wallspike_bristle", "Bristle Wall", Zone(2.2f, 24f, DamageType.Physical), "A wall of bristling spikes that damages on contact.", Stamina(18f)),
            S("melee_wallspike_blazing", "Blazing Wall", Zone(2.4f, 26f, DamageType.Fire), "A fiery wall of spikes that burns nearby foes.", Stamina(20f), DamageType.Fire, true),
            S("melee_wallspike_frost", "Frost Wall", Zone(2.4f, 26f, DamageType.Ice), "An ice-covered spike wall that chills.", Stamina(20f), DamageType.Ice, true),
            S("melee_wallspike_stone", "Stone Wall", Zone(2.6f, 28f, DamageType.Earth), "A stone spike wall that crushes on contact.", Stamina(22f), DamageType.Earth, true),
            S("melee_wallspike_gale", "Gale Wall", Zone(2.4f, 26f, DamageType.Wind), "A wind-blasted spike wall that knocks back.", Stamina(20f), DamageType.Wind, true),
        };

        /* melee_shield_sunwall children */
        bank.L2["melee_shield_sunwall"] = new BranchSlot[]
        {
            S("melee_sunwall_radiant", "Radiant Wall", Zone(2.4f, 28f, DamageType.Holy), "A blinding wall of holy radiance.", Stamina(20f), DamageType.Holy, true),
            S("melee_sunwall_blessed", "Blessed Slam", Zone(2.2f, 26f, DamageType.Holy), "A blessed shield slam that purifies foes.", Stamina(18f), DamageType.Holy, true),
            S("melee_sunwall_hymn", "Hymn of Light", Zone(2.6f, 30f, DamageType.Holy), "A sacred hymn that radiates holy power.", Stamina(22f), DamageType.Holy, true),
            S("melee_sunwall_dawn", "Dawn's Shield", Zone(2.8f, 32f, DamageType.Holy), "A dawn-bright shield flare that banishes darkness.", Stamina(24f), DamageType.Holy, true),
            S("melee_sunwall_purify", "Purifying Light", Zone(2.4f, 28f, DamageType.Holy), "A purifying light that burns the unholy.", Stamina(20f), DamageType.Holy, true),
        };

        /* melee_shield_ironrip children */
        bank.L2["melee_shield_ironrip"] = new BranchSlot[]
        {
            S("melee_ironrip_rebound", "Rebound", Slash(26f, DamageType.Physical), "A riposte that rebounds enemy force.", Stamina(16f)),
            S("melee_ironrip_retribution", "Retribution", Slash(28f, DamageType.Holy), "A holy retribution strike.", Stamina(18f), DamageType.Holy, true),
            S("melee_ironrip_vengeance", "Vengeance", Slash(30f, DamageType.Dark), "A dark vengeance that feeds on pain.", Stamina(20f), DamageType.Dark, true),
            S("melee_ironrip_reflect", "Reflect", Zone(2f, 24f, DamageType.Physical), "A riposte that reflects damage back.", Stamina(16f)),
            S("melee_ironrip_guardian", "Guardian's Riposte", Slash(28f, DamageType.Holy), "A guardian's counter blessed by light.", Stamina(18f), DamageType.Holy, true),
        };

        /* melee_shield_earthwarden children */
        bank.L2["melee_shield_earthwarden"] = new BranchSlot[]
        {
            S("melee_earthwarden_tremor", "Tremor Stomp", Zone(2.6f, 28f, DamageType.Earth), "A ground-shaking stomp that stuns.", Stamina(20f), DamageType.Earth, true),
            S("melee_earthwarden_lava", "Lava Burst", Zone(2.4f, 30f, DamageType.Fire), "A molten burst from the earth.", Stamina(22f), DamageType.Fire, true),
            S("melee_earthwarden_frozen", "Frozen Earth", Zone(2.4f, 30f, DamageType.Ice), "Frozen ground that chills all who stand on it.", Stamina(22f), DamageType.Ice, true),
            S("melee_earthwarden_boulder", "Boulder Hurl", Zone(2.2f, 26f, DamageType.Physical), "A massive boulder hurled at enemies.", Stamina(18f)),
            S("melee_earthwarden_ore", "Ore Slam", Zone(2.6f, 30f, DamageType.Earth), "A slam of raw mineral force.", Stamina(22f), DamageType.Earth, true),
        };
    }
}
