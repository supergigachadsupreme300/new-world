using UnityEngine;

/// <summary>
/// Simple infinite ammo provider used before the Inventory system exists.
/// Always returns -1 (infinite / unspecified) and consumes nothing.
/// </summary>
public sealed class InfiniteAmmo : IAmmoProvider
{
    public static readonly InfiniteAmmo Instance = new InfiniteAmmo();

    public int Count(string ammoItemId) => -1;

    public void Consume(string ammoItemId)
    {
    }
}
