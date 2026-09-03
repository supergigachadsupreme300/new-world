using UnityEngine;

/// <summary>
/// Receives damage from the combat pipeline. Implemented by any entity with a health pool
/// (enemies, breakables, the player). The new damage system resolves DamageCalculator results
/// and routes them here; legacy health receivers can adapt to this interface.
/// </summary>
public interface IDamageable
{
    /// <summary>Apply the given amount of damage. Returns remaining health.</summary>
    int TakeDamage(int amount);
}
