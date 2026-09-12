/// <summary>
/// Receives healing from the magic/support pipeline. Implemented by entities with a
/// health pool that can be restored (the player and future allies/companions). Kept
/// separate from <see cref="IDamageable"/> so enemies are never unintentionally healed.
/// </summary>
public interface IHealable
{
    /// <summary>Restore up to the entity's max health by the given amount.</summary>
    void Heal(int amount);
}