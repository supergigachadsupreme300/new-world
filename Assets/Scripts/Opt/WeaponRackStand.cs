using UnityEngine;

/// <summary>
/// A single weapon pedestal on the NewWorld test ground. Carries the weapon id; pressing E on it
/// adds the weapon to the player's <see cref="WeaponInventory"/> and removes the pedestal world.
/// Named <c>WeaponRack_&lt;id&gt;</c> so <see cref="PlayerController.HandleInteractionKeys"/> can
/// find it via the E-raycast.
/// </summary>
public sealed class WeaponRackStand : MonoBehaviour
{
    public string WeaponId;

    /// <summary>True once the weapon has been collected (stand removed).</summary>
    public bool Collected { get; private set; }

    /// <summary>Mark collected and remove the pedestal + model from the world.</summary>
    public void Collect()
    {
        if (Collected) return;
        Collected = true;
        Destroy(gameObject);
    }
}