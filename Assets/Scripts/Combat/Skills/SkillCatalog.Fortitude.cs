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
            S("fort_health_meat", "Meat Shield", Perk(PassivePerkType.MaxHealthPercent, 4f), "A body armored in pure mass.", passive: true),
            S("fort_health_brawn", "Brawn", Perk(PassivePerkType.AttackPowerPercent, 3f), "Might that cracks armor with bare hands.", passive: true),
            S("fort_health_lionheart", "Lionheart", Perk(PassivePerkType.MaxHealthPercent, 3f), "A heart that refuses to stop beating.", passive: true),
            S("fort_health_regenerate", "Regenerate", Perk(PassivePerkType.HealthRegenPerSecond, 0.003f), "Flesh knits itself between blows.", passive: true),
        };

        // Root: fort_armor (passive, Defense+4)
        bank.L1["fort_armor"] = new BranchSlot[]
        {
            A("fort_stamina"),
            A("fort_steadfast"),
            S("fort_armor_steelskin", "Steel Skin", Perk(PassivePerkType.DamageReductionFlat, 0.02f), "Skin tempered like layered plate.", passive: true),
            S("fort_armor_ironwall", "Iron Wall", Perk(PassivePerkType.BlockEfficiencyPercent, 5f), "Deflects strikes with an iron resolve.", passive: true),
            S("fort_armor_shield", "Shielded", Perk(PassivePerkType.BlockEfficiencyPercent, 6f), "An invisible ward guards every opening.", passive: true),
        };

        // Root: fort_recovery (passive, Health+2)
        bank.L1["fort_recovery"] = new BranchSlot[]
        {
            S("fort_recovery_regen", "Regen", Perk(PassivePerkType.HealthRegenPerSecond, 0.003f), "Wounds close with uncanny persistence.", passive: true),
            S("fort_recovery_reclaim", "Reclaim", Perk(PassivePerkType.StaminaRegenPercent, 6f), "Takes back what the fight has taken.", passive: true),
            S("fort_recovery_revive", "Revive", Perk(PassivePerkType.MaxHealthPercent, 4f), "The body rallies from the brink.", passive: true),
            S("fort_recovery_hardened", "Hardened Recovery", Perk(PassivePerkType.DamageReductionFlat, 0.02f), "Each scar strengthens the skin beneath.", passive: true),
            S("fort_recovery_woundmend", "Wound Mending", Perk(PassivePerkType.HealthRegenPerSecond, 0.004f), "Mend what was broken, fight anew.", passive: true),
        };

        // Root: fort_bulwark (passive, Health+3)
        bank.L1["fort_bulwark"] = new BranchSlot[]
        {
            S("fort_bulwark_stand", "Stand Guard", Perk(PassivePerkType.DamageReductionFlat, 0.02f), "Immovable as the guardians of old.", passive: true),
            S("fort_bulwark_solid", "Solid", Perk(PassivePerkType.MaxHealthPercent, 5f), "Dense and unyielding as bedrock.", passive: true),
            S("fort_bulwark_staunch", "Staunch", Perk(PassivePerkType.StaminaRegenPercent, 8f), "Breath steadies, will does not waver.", passive: true),
            S("fort_bulwark_breachless", "Breachless", Perk(PassivePerkType.BlockEfficiencyPercent, 7f), "No gap, no weakness, no entry.", passive: true),
            S("fort_bulwark_rampart", "Rampart", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "A living wall against the horde.", passive: true),
        };

        // Root: fort_stoneskin (active, Focus 12, Zone(2,14,Earth))
        bank.L1["fort_stoneskin"] = new BranchSlot[]
        {
            S("fort_stoneskin_granite", "Granite", Slash(18f, DamageType.Earth), "Fists hardened to granite.", Focus(14f), DamageType.Earth, true),
            S("fort_stoneskin_boulder", "Boulder Charge", Slash(18f, DamageType.Physical), "A boulder of a shoulder charge.", Stamina(12f)),
            S("fort_stoneskin_earthcrash", "Earth Crash", Zone(2.2f, 16f, DamageType.Earth), "Crash the earth around you.", Focus(14f), DamageType.Earth, true),
            S("fort_stoneskin_mountain", "Mountain", Perk(PassivePerkType.StaggerResistPercent, 7f), "Rooted like a peak in the storm.", passive: true),
            S("fort_stoneskin_pebble", "Pebble Wall", Zone(2f, 14f, DamageType.Earth), "A wall of pebbles driven outward.", Focus(12f), DamageType.Earth, true),
        };

        // Root: fort_guro (active, Stamina 10, Slash(14,Physical))
        bank.L1["fort_guro"] = new BranchSlot[]
        {
            S("fort_guro_headbutt", "Headbutt", Slash(16f, DamageType.Physical), "A solid, skull-first strike.", Stamina(12f)),
            S("fort_guro_warcry", "War Cry", Zone(1.8f, 14f, DamageType.Physical), "A cry that shoves the air outward.", Stamina(12f)),
            S("fort_guro_unyielding", "Unyielding", Perk(PassivePerkType.StaggerResistPercent, 6f), "The spine bends for no creature.", passive: true),
            S("fort_guro_ironcharge", "Iron Charge", Slash(18f, DamageType.Physical), "A pseudo-iron charge of a strike.", Stamina(14f)),
            S("fort_guro_juggernaut", "Juggernaut", Perk(PassivePerkType.AttackPowerPercent, 4f), "Unstoppable ruin in motion.", passive: true),
        };

        /* ──────────────── L2 (150 slots — 30 L1 parents × 5 children each) ──────────────── */

        /* fort_vitality children (all passive) */
        bank.L2["fort_vitality"] = new BranchSlot[]
        {
            S("fort_vitality_vim", "Vim", Perk(PassivePerkType.MaxHealthPercent, 5f), "A roaring reservoir of vital force.", passive: true),
            S("fort_vitality_pep", "Pep", Perk(PassivePerkType.AttackSpeedPercent, 3f), "Spirit quickens every strike.", passive: true),
            S("fort_vitality_vigor", "Vigor", Perk(PassivePerkType.MaxHealthPercent, 4f), "Overflowing with raw, primal health.", passive: true),
            S("fort_vitality_zest", "Zest", Perk(PassivePerkType.LootLuckPercent, 5f), "The spirited draw fortune's gaze.", passive: true),
            S("fort_vitality_flourish", "Flourish", Perk(PassivePerkType.HealthRegenPerSecond, 0.003f), "Life thrives where will is strong.", passive: true),
        };

        /* fort_stamina children (all passive) */
        bank.L2["fort_stamina"] = new BranchSlot[]
        {
            S("fort_stamina_grit", "Grit", Perk(PassivePerkType.StaminaMaxPercent, 4f), "A deep reserve of grit-laced stamina.", passive: true),
            S("fort_stamina_last", "Lasting", Perk(PassivePerkType.StaminaMaxPercent, 5f), "Outlasts all who stand against.", passive: true),
            S("fort_stamina_well", "Stamina Well", Perk(PassivePerkType.StaminaRegenPercent, 6f), "The well of endurance never dries.", passive: true),
            S("fort_stamina_drive", "Drive", Perk(PassivePerkType.MovementSpeedPercent, 3f), "Propelled forward by relentless drive.", passive: true),
            S("fort_stamina_plod", "Plod", Perk(PassivePerkType.DamageReductionFlat, 0.02f), "Slow and unbreakable as a glacier.", passive: true),
        };

        /* fort_steadfast children (all passive) */
        bank.L2["fort_steadfast"] = new BranchSlot[]
        {
            S("fort_steadfast_rocksteady", "Rocksteady", Perk(PassivePerkType.StaggerResistPercent, 8f), "Not the faintest tremor shakes this will.", passive: true),
            S("fort_steadfast_unshakable", "Unshakable", Perk(PassivePerkType.StaggerResistPercent, 7f), "An anchor hammered into the earth.", passive: true),
            S("fort_steadfast_resolute", "Resolute", Perk(PassivePerkType.StaminaRegenPercent, 5f), "Unbending resolve feeds the fire.", passive: true),
            S("fort_steadfast_fixed", "Fixed", Perk(PassivePerkType.StaggerResistPercent, 6f), "Locked in place, unmoved by fury.", passive: true),
            S("fort_steadfast_stalwart", "Stalwart", Perk(PassivePerkType.MaxHealthPercent, 4f), "A stout fortress of living flesh.", passive: true),
        };

        /* fort_health_meat children (all passive) */
        bank.L2["fort_health_meat"] = new BranchSlot[]
        {
            S("fort_health_meat_health", "Meat Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "Built thick with primal vitality.", passive: true),
            S("fort_health_meat_defense", "Meat Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "Layers of flesh blunt the keenest blade.", passive: true),
            S("fort_health_meat_endurance", "Meat Endurance", Perk(PassivePerkType.StaminaMaxPercent, 5f), "Forged to outlast what fells the frail.", passive: true),
            S("fort_health_meat_strength", "Meat Strength", Perk(PassivePerkType.AttackPowerPercent, 4f), "Every fist carries the weight of a carcass.", passive: true),
            S("fort_health_meat_attackspeed", "Meat Hands", Perk(PassivePerkType.AttackSpeedPercent, 3f), "Blunt hands swing faster than expected.", passive: true),
        };

        /* fort_health_brawn children (all passive) */
        bank.L2["fort_health_brawn"] = new BranchSlot[]
        {
            S("fort_health_brawn_strength", "Heavy Brawn", Perk(PassivePerkType.AttackPowerPercent, 5f), "Bones crack under the force of each swing.", passive: true),
            S("fort_health_brawn_health", "Brawn Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "A titan's reservoir of stubborn life.", passive: true),
            S("fort_health_brawn_defense", "Brawn Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "Muscle woven tight becomes living armor.", passive: true),
            S("fort_health_brawn_endurance", "Brawn Endurance", Perk(PassivePerkType.StaminaRegenPercent, 7f), "Strength that refuses to ebb.", passive: true),
            S("fort_health_brawn_attackspeed", "Brawn Hands", Perk(PassivePerkType.CritDamagePercent, 8f), "When brawn meets precision, bones shatter.", passive: true),
        };

        /* fort_health_lionheart children (all passive) */
        bank.L2["fort_health_lionheart"] = new BranchSlot[]
        {
            S("fort_health_lionheart_courage", "Courage", Perk(PassivePerkType.MaxHealthPercent, 6f), "A heart ablaze with unbreakable daring.", passive: true),
            S("fort_health_lionheart_lion", "Lion's Heart", Perk(PassivePerkType.MaxHealthPercent, 5f), "Beats with the fury of a savage king.", passive: true),
            S("fort_health_lionheart_brave", "Brave", Perk(PassivePerkType.StaggerResistPercent, 8f), "Fear is a stranger to this blood.", passive: true),
            S("fort_health_lionheart_roar", "Lion's Roar", Perk(PassivePerkType.AttackPowerPercent, 4f), "A roar that shatters courage.", passive: true),
            S("fort_health_lionheart_pride", "Pride", Perk(PassivePerkType.ParryWindowPercent, 6f), "Swift and sure, pride meets the blade clean.", passive: true),
        };

        /* fort_health_regenerate children (all passive) */
        bank.L2["fort_health_regenerate"] = new BranchSlot[]
        {
            S("fort_health_regenerate_health", "Regen Health", Perk(PassivePerkType.HealthRegenPerSecond, 0.005f), "Flesh regenerates with predatory speed.", passive: true),
            S("fort_health_regenerate_endurance", "Regen Endurance", Perk(PassivePerkType.StaminaRegenPercent, 7f), "Breath returns in rhythmic surges.", passive: true),
            S("fort_health_regenerate_defense", "Regen Defense", Perk(PassivePerkType.DamageReductionFlat, 0.02f), "New skin grows tougher than old.", passive: true),
            S("fort_health_regenerate_speed", "Regen Speed", Perk(PassivePerkType.MovementSpeedPercent, 3f), "Swift renewal quickens the stride.", passive: true),
            S("fort_health_regenerate_will", "Regen Will", Perk(PassivePerkType.FocusRegenPercent, 5f), "Inner calm restores what steel has taken.", passive: true),
        };

        /* fort_armor_steelskin children (all passive) */
        bank.L2["fort_armor_steelskin"] = new BranchSlot[]
        {
            S("fort_armor_steelskin_defense", "Steel Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "Dents and gouges find no hold.", passive: true),
            S("fort_armor_steelskin_endurance", "Steel Endurance", Perk(PassivePerkType.StaminaMaxPercent, 4f), "Tempered for the long, brutal war.", passive: true),
            S("fort_armor_steelskin_health", "Steel Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "A furnace of life behind cold metal.", passive: true),
            S("fort_armor_steelskin_strength", "Steel Strength", Perk(PassivePerkType.AttackPowerPercent, 4f), "Steel-limbed strikes shatter what they meet.", passive: true),
            S("fort_armor_steelskin_reflex", "Steel Reflex", Perk(PassivePerkType.ParryWindowPercent, 5f), "Tempered nerves guide the perfect parry.", passive: true),
        };

        /* fort_armor_ironwall children (all passive) */
        bank.L2["fort_armor_ironwall"] = new BranchSlot[]
        {
            S("fort_armor_ironwall_defense", "Iron Defense", Perk(PassivePerkType.DamageReductionFlat, 0.04f), "An iron wall no blade has breached.", passive: true),
            S("fort_armor_ironwall_health", "Iron Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "Iron will, iron body, iron refusal to fall.", passive: true),
            S("fort_armor_ironwall_endurance", "Iron Endurance", Perk(PassivePerkType.StaminaRegenPercent, 6f), "The iron spirit does not tire.", passive: true),
            S("fort_armor_ironwall_strength", "Iron Strength", Perk(PassivePerkType.AttackPowerPercent, 5f), "Strikes ring like hammer on anvil.", passive: true),
            S("fort_armor_ironwall_luck", "Iron Luck", Perk(PassivePerkType.LootLuckPercent, 6f), "Iron stubbornness draws iron fortune.", passive: true),
        };

        /* fort_armor_shield children (all passive) */
        bank.L2["fort_armor_shield"] = new BranchSlot[]
        {
            S("fort_armor_shield_endurance", "Shield Endurance", Perk(PassivePerkType.StaminaMaxPercent, 5f), "A guardian's stamina stretches beyond measure.", passive: true),
            S("fort_armor_shield_defense", "Shield Defense", Perk(PassivePerkType.BlockEfficiencyPercent, 7f), "Every blow crashes against unyielding oak and iron.", passive: true),
            S("fort_armor_shield_health", "Shield Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "Life sheltered behind the warding shield.", passive: true),
            S("fort_armor_shield_strength", "Shield Strength", Perk(PassivePerkType.AttackPowerPercent, 4f), "The shield itself becomes a crushing weapon.", passive: true),
            S("fort_armor_shield_attackspeed", "Shield Hands", Perk(PassivePerkType.AttackSpeedPercent, 3f), "Quick riposte flows from the shield-arm.", passive: true),
        };

        /* fort_recovery_regen children (all passive) */
        bank.L2["fort_recovery_regen"] = new BranchSlot[]
        {
            S("fort_recovery_regen_health", "Regen Health", Perk(PassivePerkType.HealthRegenPerSecond, 0.005f), "The body mends itself in ceaseless silence.", passive: true),
            S("fort_recovery_regen_endurance", "Regen Endurance", Perk(PassivePerkType.StaminaRegenPercent, 7f), "Stamina floods back like a dark tide.", passive: true),
            S("fort_recovery_regen_speed", "Regen Speed", Perk(PassivePerkType.CooldownReductionPercent, 3f), "Quick recovery quickens the next assault.", passive: true),
            S("fort_recovery_regen_defense", "Regen Defense", Perk(PassivePerkType.DamageReductionFlat, 0.02f), "Healed flesh resists the coming wound.", passive: true),
            S("fort_recovery_regen_strength", "Regen Strength", Perk(PassivePerkType.AttackPowerPercent, 3f), "Reborn strength surges through the arms.", passive: true),
        };

        /* fort_recovery_reclaim children (all passive) */
        bank.L2["fort_recovery_reclaim"] = new BranchSlot[]
        {
            S("fort_recovery_reclaim_endurance", "Reclaimed Endurance", Perk(PassivePerkType.StaminaRegenPercent, 8f), "Snatches back what exhaustion devoured.", passive: true),
            S("fort_recovery_reclaim_health", "Reclaimed Health", Perk(PassivePerkType.HealthRegenPerSecond, 0.004f), "Life dragged back from death's threshold.", passive: true),
            S("fort_recovery_reclaim_speed", "Reclaimed Speed", Perk(PassivePerkType.MovementSpeedPercent, 3f), "Regains the ground that weakness stole.", passive: true),
            S("fort_recovery_reclaim_strength", "Reclaimed Strength", Perk(PassivePerkType.CritChanceFlat, 3f), "Recovered fury lands with sharper precision.", passive: true),
            S("fort_recovery_reclaim_luck", "Reclaimed Luck", Perk(PassivePerkType.LootLuckPercent, 5f), "Those who refuse death find its gifts.", passive: true),
        };

        /* fort_recovery_revive children (all passive) */
        bank.L2["fort_recovery_revive"] = new BranchSlot[]
        {
            S("fort_recovery_revive_health", "Revive Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "Rises from the brink with savage vitality.", passive: true),
            S("fort_recovery_revive_endurance", "Revive Endurance", Perk(PassivePerkType.StaminaMaxPercent, 5f), "Revived vigor floods every sinew.", passive: true),
            S("fort_recovery_revive_defense", "Revive Defense", Perk(PassivePerkType.DamageReductionFlat, 0.02f), "Battlescarred skin shrugs off the fatal blow.", passive: true),
            S("fort_recovery_revive_luck", "Revive Luck", Perk(PassivePerkType.LootLuckPercent, 6f), "Cheating death sharpens the senses.", passive: true),
            S("fort_recovery_revive_speed", "Revive Speed", Perk(PassivePerkType.MovementSpeedPercent, 4f), "Spring returns to the fallen step.", passive: true),
        };

        /* fort_recovery_hardened children (all passive) */
        bank.L2["fort_recovery_hardened"] = new BranchSlot[]
        {
            S("fort_recovery_hardened_endurance", "Hardened Endurance", Perk(PassivePerkType.StaminaMaxPercent, 6f), "The body schooled to endure beyond limits.", passive: true),
            S("fort_recovery_hardened_health", "Hardened Health", Perk(PassivePerkType.MaxHealthPercent, 4f), "Toughened by pain upon pain.", passive: true),
            S("fort_recovery_hardened_attackspeed", "Hardened Hands", Perk(PassivePerkType.AttackSpeedPercent, 4f), "Scarred knuckles strike without mercy.", passive: true),
            S("fort_recovery_hardened_speed", "Hardened Speed", Perk(PassivePerkType.CooldownReductionPercent, 3f), "Hardened limbs recover and lash out.", passive: true),
            S("fort_recovery_hardened_defense", "Hardened Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "Each scar a new plate of armor.", passive: true),
        };

        /* fort_recovery_woundmend children (all passive) */
        bank.L2["fort_recovery_woundmend"] = new BranchSlot[]
        {
            S("fort_recovery_woundmend_health", "Mend Health", Perk(PassivePerkType.HealthRegenPerSecond, 0.005f), "Wounds seal with unnatural swiftness.", passive: true),
            S("fort_recovery_woundmend_endurance", "Mend Endurance", Perk(PassivePerkType.StaminaRegenPercent, 6f), "Steady breath mends the fighting spirit.", passive: true),
            S("fort_recovery_woundmend_defense", "Mend Defense", Perk(PassivePerkType.StaggerResistPercent, 6f), "Mended sinew teaches the body to endure.", passive: true),
            S("fort_recovery_woundmend_speed", "Mend Speed", Perk(PassivePerkType.MovementSpeedPercent, 3f), "Healed legs carry without faltering.", passive: true),
            S("fort_recovery_woundmend_strength", "Mend Strength", Perk(PassivePerkType.BackstabPercent, 6f), "Rebuilt muscle finds the vulnerable seam.", passive: true),
        };

        /* fort_bulwark_stand children (all passive) */
        bank.L2["fort_bulwark_stand"] = new BranchSlot[]
        {
            S("fort_bulwark_stand_defense", "Stand Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "Plants deep and becomes immovable.", passive: true),
            S("fort_bulwark_stand_health", "Stand Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "The sentinel's life runs long and deep.", passive: true),
            S("fort_bulwark_stand_endurance", "Stand Endurance", Perk(PassivePerkType.StaminaRegenPercent, 7f), "A guard's breath never falters.", passive: true),
            S("fort_bulwark_stand_strength", "Stand Strength", Perk(PassivePerkType.CritDamagePercent, 7f), "A sentinel's decisive blow ends the dance.", passive: true),
            S("fort_bulwark_stand_luck", "Stand Luck", Perk(PassivePerkType.LootLuckPercent, 5f), "Patience is its own dark reward.", passive: true),
        };

        /* fort_bulwark_solid children (all passive) */
        bank.L2["fort_bulwark_solid"] = new BranchSlot[]
        {
            S("fort_bulwark_solid_health", "Solid Health", Perk(PassivePerkType.MaxHealthPercent, 6f), "Dense as the bones of the world.", passive: true),
            S("fort_bulwark_solid_defense", "Solid Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "Hard as the stone that broke the first sword.", passive: true),
            S("fort_bulwark_solid_endurance", "Solid Endurance", Perk(PassivePerkType.StaminaMaxPercent, 5f), "Solid lungs, solid will, solid refusal.", passive: true),
            S("fort_bulwark_solid_strength", "Solid Strength", Perk(PassivePerkType.AttackPowerPercent, 4f), "A granite fist behind every blow.", passive: true),
            S("fort_bulwark_solid_speed", "Solid Speed", Perk(PassivePerkType.MovementSpeedPercent, 3f), "Unhurried but absolutely relentless.", passive: true),
        };

        /* fort_bulwark_staunch children (all passive) */
        bank.L2["fort_bulwark_staunch"] = new BranchSlot[]
        {
            S("fort_bulwark_staunch_endurance", "Staunch Endurance", Perk(PassivePerkType.StaminaRegenPercent, 9f), "The wellspring of will runs deepest here.", passive: true),
            S("fort_bulwark_staunch_health", "Staunch Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "A staunch vessel brimming with dark life.", passive: true),
            S("fort_bulwark_staunch_defense", "Staunch Defense", Perk(PassivePerkType.BlockEfficiencyPercent, 6f), "Blocks with the firmness of ancient oak.", passive: true),
            S("fort_bulwark_staunch_speed", "Staunch Speed", Perk(PassivePerkType.CooldownReductionPercent, 4f), "A steadfast pace that never slows.", passive: true),
            S("fort_bulwark_staunch_strength", "Staunch Strength", Perk(PassivePerkType.AttackPowerPercent, 4f), "Unwavering force behind the unwavering fist.", passive: true),
        };

        /* fort_bulwark_breachless children (all passive) */
        bank.L2["fort_bulwark_breachless"] = new BranchSlot[]
        {
            S("fort_bulwark_breachless_defense", "Breachless Defense", Perk(PassivePerkType.BlockEfficiencyPercent, 8f), "No crack, no breach, no way inside.", passive: true),
            S("fort_bulwark_breachless_endurance", "Breachless Endurance", Perk(PassivePerkType.StaminaMaxPercent, 5f), "An inexhaustible bastion of grim will.", passive: true),
            S("fort_bulwark_breachless_health", "Breachless Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "Life held behind walls none can shatter.", passive: true),
            S("fort_bulwark_breachless_strength", "Breachless Strength", Perk(PassivePerkType.CritDamagePercent, 10f), "Break them before they break you.", passive: true),
            S("fort_bulwark_breachless_luck", "Breachless Luck", Perk(PassivePerkType.LootLuckPercent, 5f), "The unbroken find fortune at their feet.", passive: true),
        };

        /* fort_bulwark_rampart children (all passive) */
        bank.L2["fort_bulwark_rampart"] = new BranchSlot[]
        {
            S("fort_bulwark_rampart_health", "Rampart Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "A living wall of sinew, bone, and rage.", passive: true),
            S("fort_bulwark_rampart_defense", "Rampart Defense", Perk(PassivePerkType.DamageReductionFlat, 0.04f), "Stands above the fallen like a tower.", passive: true),
            S("fort_bulwark_rampart_endurance", "Rampart Endurance", Perk(PassivePerkType.StaminaRegenPercent, 7f), "Endurance poured like mortar between stones.", passive: true),
            S("fort_bulwark_rampart_strength", "Rampart Strength", Perk(PassivePerkType.AttackPowerPercent, 4f), "Strikes from the wall shake the ground.", passive: true),
            S("fort_bulwark_rampart_attackspeed", "Rampart Hands", Perk(PassivePerkType.AttackSpeedPercent, 3f), "Crenellations hide a storm of blows.", passive: true),
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
            S("fort_stoneskin_mountain_health", "Mountain Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "Life as vast as the mountain's roots.", passive: true),
            S("fort_stoneskin_mountain_defense", "Mountain Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "An avalanche of armor beneath the skin.", passive: true),
            S("fort_stoneskin_mountain_strength", "Mountain Strength", Perk(PassivePerkType.AttackPowerPercent, 5f), "The mountain's fist descends without mercy.", passive: true),
            S("fort_stoneskin_mountain_endurance", "Mountain Endurance", Perk(PassivePerkType.StaminaMaxPercent, 5f), "Breath drawn from the peak's thin air.", passive: true),
            S("fort_stoneskin_mountain_immovable", "Immovable", Perk(PassivePerkType.StaggerResistPercent, 10f), "No force in heaven or earth shifts this stone.", passive: true),
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
            S("fort_guro_unyielding_endurance", "Unyielding Endurance", Perk(PassivePerkType.StaminaRegenPercent, 8f), "Refuses to bend, refuses to falter.", passive: true),
            S("fort_guro_unyielding_defense", "Unyielding Defense", Perk(PassivePerkType.DamageReductionFlat, 0.03f), "Armor forged from pure defiance.", passive: true),
            S("fort_guro_unyielding_health", "Unyielding Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "A body that simply will not quit.", passive: true),
            S("fort_guro_unyielding_strength", "Unyielding Strength", Perk(PassivePerkType.CritChanceFlat, 3f), "Defiance sharpens every blow.", passive: true),
            S("fort_guro_unyielding_luck", "Unyielding Luck", Perk(PassivePerkType.LootLuckPercent, 6f), "Fate bows to those who never yield.", passive: true),
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
            S("fort_guro_juggernaut_defense", "Juggernaut Defense", Perk(PassivePerkType.BlockEfficiencyPercent, 7f), "Carves through resistance like siege upon stone.", passive: true),
            S("fort_guro_juggernaut_health", "Juggernaut Health", Perk(PassivePerkType.MaxHealthPercent, 5f), "The bulk of a walking fortress.", passive: true),
            S("fort_guro_juggernaut_endurance", "Juggernaut Endurance", Perk(PassivePerkType.StaminaMaxPercent, 5f), "Never stops, never rests, never breaks.", passive: true),
            S("fort_guro_juggernaut_strength", "Juggernaut Strength", Perk(PassivePerkType.AttackPowerPercent, 5f), "An oncoming doom no wall can halt.", passive: true),
            S("fort_guro_juggernaut_speed", "Juggernaut Speed", Perk(PassivePerkType.MovementSpeedPercent, 4f), "The earth trembles beneath the juggernaut's tread.", passive: true),
        };
    }
}
