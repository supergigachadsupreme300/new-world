using UnityEngine;

/// <summary>
/// Set of magic modifiers carried by a staff/wand/book (§3.6 Layer 1) that modulate
/// spells cast through them (§3.8). Extracted from the weapon's WeaponData rather than
/// sharing the whole weapon with the caster.
/// </summary>
public struct MagicWeaponMods
{
    /// <summary>Multiplier applied to spell base power (MagicDamageMult).</summary>
    public float DamageMult;

    /// <summary>Multiplier applied to spell cast time (CastTimeMod).</summary>
    public float CastTimeMult;

    /// <summary>Multiplier applied to spell cooldown (CooldownMod).</summary>
    public float CooldownMult;

    /// <summary>Multiplier applied to spell FP cost.</summary>
    public float FpCostMult;

    public static MagicWeaponMods FromWeapon(WeaponData data)
    {
        if (data == null)
            return new MagicWeaponMods
            {
                DamageMult = 1f,
                CastTimeMult = 1f,
                CooldownMult = 1f,
                FpCostMult = 1f
            };
        return new MagicWeaponMods
        {
            DamageMult = Mathf.Max(data.MagicDamageMult, 0f),
            CastTimeMult = Mathf.Max(data.CastTimeMod, 0.05f),
            CooldownMult = Mathf.Max(data.CooldownMod, 0.05f),
            FpCostMult = 1f
        };
    }
}
