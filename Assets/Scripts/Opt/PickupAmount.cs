using UnityEngine;

/// <summary>
/// Optional amount tag for a world <c>Pickup_&lt;itemType&gt;</c> drop: when present,
/// <see cref="ToolManager.TryPickupTool"/> grants <see cref="Amount"/> of the item instead of 1.
/// The item id itself is still encoded in the pickup's GameObject name.
/// </summary>
public sealed class PickupAmount : MonoBehaviour
{
    public int Amount = 1;
}