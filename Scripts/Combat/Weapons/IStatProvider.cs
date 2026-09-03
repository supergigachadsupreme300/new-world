using UnityEngine;

/// <summary>
/// Supplies current stat values for weapon/spell scaling.
///
/// The full 11-stat system lands in Phase 4; this interface lets weapon behaviors
/// (MeleeWeaponBehavior, RangedWeaponBehavior, SpellCaster) query stats without a
/// hard dependency. Phase 4's stat runtime implements this.
/// </summary>
public interface IStatProvider
{
    /// <summary>Current value of the given scaling stat.</summary>
    float GetStat(WeaponScalingStat stat);

    /// <summary>Current effective magic attack power (Wisdom-scaled).</summary>
    float MagicAttackPower { get; }

    /// <summary>Current max focus points (Intelligence-derived).</summary>
    float MaxFocusPoints { get; }

    /// <summary>Luck-driven status proc chance (%) — see StatusProcLuck, §3.4.</summary>
    float StatusProcLuck { get; }
}
