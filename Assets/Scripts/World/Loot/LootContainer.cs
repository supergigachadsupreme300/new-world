using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A world chest/hidden-drop container (planning Task 5.2 "world loot placement").
/// Holds a <see cref="LootTable"/> that is rolled once when the player interacts with it,
/// spawning <see cref="LootDrop"/> pickups. A cheap generated cube visual stands in until
/// art/prefabs drop in.
/// </summary>
public class LootContainer : MonoBehaviour
{
    [Tooltip("Drop table rolled on open.")]
    public LootTable Loot;
    [Tooltip("Item id this container guarantees regardless of table (optional).")]
    public string GuaranteedItemId;
    [Min(1)] public int GuaranteedCount = 1;

    [Header("Behavior")]
    [Tooltip("Only the first interaction pays out.")]
    public bool OneTime = true;
    [Tooltip("Require a jump/key press interaction to open (not just contact).")]
    public bool RequiresInteract = true;

    private bool _opened;
    private GameObject _visual;

    private void Awake()
    {
        BuildVisual();
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.9f, 0.7f, 0.9f);
        }
    }

    /// <summary>Roll the table and spawn drops at this container. Call on interaction.</summary>
    public void Open()
    {
        if (_opened) return;
        if (OneTime) _opened = true;

        float luck = 0f;
        var receiver = FindFirstLuckProvider();
        if (receiver != null)
            luck = Mathf.Max(0f, receiver.GetLootQuality() - 1f);

        int index = 0;
        if (Loot != null)
        {
            foreach (var pair in Loot.Roll(luck))
                SpawnDrop(pair.Key, pair.Value, index++);
        }

        if (!string.IsNullOrEmpty(GuaranteedItemId))
        {
            var item = ItemDatabase.Get(GuaranteedItemId);
            if (item != null)
                SpawnDrop(item, GuaranteedCount, index++);
        }
    }

    private void SpawnDrop(ItemData item, int count, int index)
    {
        var go = new GameObject("LootDrop_" + index);
        go.transform.SetParent(transform.parent);
        go.transform.position = transform.position + Vector3.up * 0.4f + new Vector3(
            UnityEngine.Random.Range(-0.3f, 0.3f), 0f,
            UnityEngine.Random.Range(-0.3f, 0.3f));
        var drop = go.AddComponent<LootDrop>();
        drop.Item = item;
        drop.Count = count;
    }

    private ILootLuckProvider FindFirstLuckProvider()
    {
        // A full integration reads the interacting player; for now locate the live player stats.
        var ps = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
        return ps as ILootLuckProvider;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        if (RequiresInteract) return;
        Open();
    }

    private void BuildVisual()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "LootChest_" + name;
        cube.transform.SetParent(transform, false);
        cube.transform.localScale = Vector3.one;
        UnityEngine.Object.Destroy(cube.GetComponent<Collider>());
        _visual = cube;
        var r = cube.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.55f, 0.4f, 0.2f);
    }

    private void OnDestroy()
    {
        if (_visual != null) Destroy(_visual);
    }
}