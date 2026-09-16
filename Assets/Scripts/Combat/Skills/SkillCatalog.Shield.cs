using System.Collections.Generic;

public static partial class SkillCatalog
{
    private static void RegisterShieldDesign(DesignBank bank)
    {
        /* ──────────────── L1 (5 slots — 1 root × 5 children) ──────────────── */

        // Root: shield_bash (active, Stamina 14, Bash 22 Physical). Every Shield skill plays the
        // equipped shield's own bash (ShieldBashEffect) — the shield face, not a hand-held blade —
        // so the whole category shares the bash/guard/counter identity and needs a shield in hand.
        bank.L1["shield_bash"] = new BranchSlot[]
        {
            S("shield_slam", "Shield Slam", Bash(24f, DamageType.Physical), "A deafening full-body shield slam.", Stamina(14f)),
            S("shield_spikewall", "Spiked Wall", Bash(20f, DamageType.Physical), "A bristling shield line that lashes out.", Stamina(16f)),
            S("shield_flash", "Sunwall", Bash(24f, DamageType.Holy), "A gleaming shield flare of holy light.", Stamina(18f), DamageType.Holy, true),
            S("shield_riposte", "Iron Riposte", Bash(22f, DamageType.Physical), "Brace and punish an enemy that hit you.", Stamina(14f)),
            S("shield_earthwarden", "Earthwarden", Bash(22f, DamageType.Earth), "Strike the ground, sending rubble against foes.", Stamina(18f), DamageType.Earth, true),
        };

        /* ──────────────── L2 (25 slots — 5 L1 parents × 5 children each) ──────────────── */

        /* shield_slam children */
        bank.L2["shield_slam"] = new BranchSlot[]
        {
            S("shield_slam_aftershock", "Aftershock Slam", Bash(28f, DamageType.Physical), "A slam that sends aftershocks through the ground.", Stamina(20f)),
            S("shield_slam_flame", "Flame Slam", Bash(28f, DamageType.Fire), "A fiery shield slam that scorches on impact.", Stamina(20f), DamageType.Fire, true),
            S("shield_slam_frost", "Frost Slam", Bash(28f, DamageType.Ice), "A freezing shield slam that chills all nearby.", Stamina(20f), DamageType.Ice, true),
            S("shield_slam_thunder", "Thunder Slam", Bash(30f, DamageType.Lightning), "A thunderous slam that shocks enemies.", Stamina(22f), DamageType.Lightning, true),
            S("shield_slam_earth", "Earth Slam", Bash(30f, DamageType.Earth), "A ground-shattering slam of earthen force.", Stamina(22f), DamageType.Earth, true),
        };

        /* shield_spikewall children */
        bank.L2["shield_spikewall"] = new BranchSlot[]
        {
            S("shield_wallspike_bristle", "Bristle Wall", Bash(24f, DamageType.Physical), "A wall of bristling spikes that damages on contact.", Stamina(18f)),
            S("shield_wallspike_blazing", "Blazing Wall", Bash(26f, DamageType.Fire), "A fiery wall of spikes that burns nearby foes.", Stamina(20f), DamageType.Fire, true),
            S("shield_wallspike_frost", "Frost Wall", Bash(26f, DamageType.Ice), "An ice-covered spike wall that chills.", Stamina(20f), DamageType.Ice, true),
            S("shield_wallspike_stone", "Stone Wall", Bash(28f, DamageType.Earth), "A stone spike wall that crushes on contact.", Stamina(22f), DamageType.Earth, true),
            S("shield_wallspike_gale", "Gale Wall", Bash(26f, DamageType.Wind), "A wind-blasted spike wall that knocks back.", Stamina(20f), DamageType.Wind, true),
        };

        /* shield_flash children */
        bank.L2["shield_flash"] = new BranchSlot[]
        {
            S("shield_flash_radiant", "Radiant Wall", Bash(28f, DamageType.Holy), "A blinding wall of holy radiance.", Stamina(20f), DamageType.Holy, true),
            S("shield_flash_blessed", "Blessed Slam", Bash(26f, DamageType.Holy), "A blessed shield slam that purifies foes.", Stamina(18f), DamageType.Holy, true),
            S("shield_flash_hymn", "Hymn of Light", Bash(30f, DamageType.Holy), "A sacred hymn that radiates holy power.", Stamina(22f), DamageType.Holy, true),
            S("shield_flash_dawn", "Dawn's Shield", Bash(32f, DamageType.Holy), "A dawn-bright shield flare that banishes darkness.", Stamina(24f), DamageType.Holy, true),
            S("shield_flash_purify", "Purifying Light", Bash(28f, DamageType.Holy), "A purifying light that burns the unholy.", Stamina(20f), DamageType.Holy, true),
        };

        /* shield_riposte children */
        bank.L2["shield_riposte"] = new BranchSlot[]
        {
            S("shield_riposte_rebound", "Rebound", Bash(26f, DamageType.Physical), "A riposte that rebounds enemy force.", Stamina(16f)),
            S("shield_riposte_retribution", "Retribution", Bash(28f, DamageType.Holy), "A holy retribution strike.", Stamina(18f), DamageType.Holy, true),
            S("shield_riposte_vengeance", "Vengeance", Bash(30f, DamageType.Dark), "A dark vengeance that feeds on pain.", Stamina(20f), DamageType.Dark, true),
            S("shield_riposte_reflect", "Reflect", Bash(24f, DamageType.Physical), "A riposte that reflects damage back.", Stamina(16f)),
            S("shield_riposte_guardian", "Guardian's Riposte", Bash(28f, DamageType.Holy), "A guardian's counter blessed by light.", Stamina(18f), DamageType.Holy, true),
        };

        /* shield_earthwarden children */
        bank.L2["shield_earthwarden"] = new BranchSlot[]
        {
            S("shield_earthwarden_tremor", "Tremor Stomp", Bash(28f, DamageType.Earth), "A ground-shaking stomp that stuns.", Stamina(20f), DamageType.Earth, true),
            S("shield_earthwarden_lava", "Lava Burst", Bash(30f, DamageType.Fire), "A molten burst from the earth.", Stamina(22f), DamageType.Fire, true),
            S("shield_earthwarden_frozen", "Frozen Earth", Bash(30f, DamageType.Ice), "Frozen ground that chills all who stand on it.", Stamina(22f), DamageType.Ice, true),
            S("shield_earthwarden_boulder", "Boulder Hurl", Bash(26f, DamageType.Physical), "A massive boulder hurled at enemies.", Stamina(18f)),
            S("shield_earthwarden_ore", "Ore Slam", Bash(30f, DamageType.Earth), "A slam of raw mineral force.", Stamina(22f), DamageType.Earth, true),
        };
    }
}