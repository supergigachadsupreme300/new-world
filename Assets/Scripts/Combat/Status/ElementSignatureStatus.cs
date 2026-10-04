using UnityEngine;

/// <summary>
/// Per-element signature status (§3.7). Every magic attack of an element with a signature
/// applies it automatically on hit — even when the skill doesn't declare an explicit
/// <c>statusEffect:</c> (skills that DO declare one keep their explicit choice as the override).
///
/// Mapping: Fire→Burn, Ice→Chill (the deeper Frost is the heavy freeze), Lightning→Stagger
/// (stun), Dark→Blind, Water→Wet, Arcane→no status. Wind keeps its knockback, Earth reshapes
/// terrain instead of a status, Holy heals, and Physical is not a magic element — all return
/// null (no automatic status).
/// </summary>
public static class ElementSignatureStatus
{
    /// <summary>The signature status for a damage type, or null when the element has none.</summary>
    public static StatusEffectType? For(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire:
                return StatusEffectType.Burn;
            case DamageType.Ice:
                return StatusEffectType.Chill;
            case DamageType.Lightning:
                return StatusEffectType.Stagger;
            case DamageType.Dark:
                return StatusEffectType.Blind;
            case DamageType.Water:
                return StatusEffectType.Wet;
            default:
                return null;
        }
    }
}