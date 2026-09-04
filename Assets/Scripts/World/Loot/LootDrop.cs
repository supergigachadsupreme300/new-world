using System;
using UnityEngine;

/// <summary>
/// A spawned world pickup representing one dropped item (planning Task 5.2). Rotates and
/// bobs; on player contact hands the <see cref="ItemData"/> to the active inventory via the
/// <see cref="IItemCollection"/> seam (the Phase 4/6 inventory wires in without coupling
/// this script to a concrete InventorySystem).
/// </summary>
public class LootDrop : MonoBehaviour
{
    public ItemData Item;
    public int Count = 1;

    [Tooltip("Seconds before the drop despawns (0 = never).")]
    public float Lifetime = 30f;
    [Tooltip("Visual spin speed for generated cubes.")]
    public float SpinSpeed = 90f;
    [Tooltip("Bob amplitude for generated pickups.")]
    public float BobAmount = 0.12f;

    private float _lifeTimer;
    private float _bobPhase;
    private Vector3 _basePos;

    private void Awake()
    {
        _bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        _basePos = transform.position;
        BuildVisual();
        var col = GetComponent<Collider>();
        if (col == null)
            col = gameObject.AddComponent<SphereCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        transform.Rotate(0f, SpinSpeed * Time.deltaTime, 0f);
        transform.position = _basePos + Vector3.up * (Mathf.Sin(_bobPhase + Time.time) * BobAmount);

        _lifeTimer += Time.deltaTime;
        if (Lifetime > 0f && _lifeTimer >= Lifetime)
            Destroy(gameObject);
    }

    /// <summary>Build a simple generated cube visual if no item prefab is set.</summary>
    private void BuildVisual()
    {
        if (Item != null && Item.PickupPrefab != null)
            return;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Loot_" + (Item != null ? Item.id : "unknown");
        cube.transform.SetParent(transform, false);
        cube.transform.localScale = Vector3.one * 0.35f;
        UnityEngine.Object.Destroy(cube.GetComponent<Collider>());
        var r = cube.GetComponent<Renderer>();
        if (r != null) r.material.color = Item != null ? Item.Tint : Color.magenta;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Accept anything tagged Player; the collection seam decides what can hold it.
        if (other == null || !other.CompareTag("Player")) return;
        if (Item == null) { CollectAndDestroy(null); return; }

        if (TryGetComponent<IItemCollection>(out var collection))
        {
            collection.AddItem(Item, Count);
            CollectAndDestroy(true);
        }
        else
        {
            // No inventory wired yet — log the pickup for debugging and despawn.
            Debug.Log($"[LootDrop] collected {Count}x {(Item.displayName ?? Item.id)} (no inventory connected)");
            CollectAndDestroy(true);
        }
    }

    private void CollectAndDestroy(bool? granted)
    {
        Destroy(gameObject);
    }
}

/// <summary>
/// Seam so loot can hand items to whatever inventory/equipment system is live
/// (the Player/Inventory block wires this up without LootDrop depending on it).
/// </summary>
public interface IItemCollection
{
    bool AddItem(ItemData item, int count);
}

/// <summary>
/// Seam exposing the player's luck-driven loot quality multiplier; the loot system reads it
/// through this so it never depends on the concrete <see cref="PlayerStats"/>. Higher values
/// (Goblin/Gnome passives, Luck stat) improve drop payout chances.
/// </summary>
public interface ILootLuckProvider
{
    float GetLootQuality();
}