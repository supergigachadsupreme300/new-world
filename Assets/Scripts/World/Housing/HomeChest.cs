using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A player-home chest (planning Task 6.5, game-design §5.3 "chest storage"). Provides
/// deposit/withdraw to/from the player's <see cref="ToolManager"/> tool inventory, so stored
/// goods are re-usable by crafting and the vendor economy. A simple generated cube stands in
/// until storage art arrives. Interact toggles the open state; <c>RequiresInteract</c> lets a
/// trigger open it automatically.
/// </summary>
public class HomeChest : MonoBehaviour
{
    public string ChestId = "home_chest";
    [Tooltip("Open automatically on player contact.")]
    public bool AutoOpen = true;

    private bool _open;
    private GameObject _visual;

    private void Awake()
    {
        BuildVisual();
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1.0f, 0.8f, 0.9f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!AutoOpen) return;
        if (other == null || !other.CompareTag("Player")) return;
        Open();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!AutoOpen) return;
        if (other == null || !other.CompareTag("Player")) return;
        Close();
    }

    /// <summary>Toggle storage. Public so the owner/plot can be interacted with directly.</summary>
    public void Interact()
    {
        if (_open) Close(); else Open();
    }

    public void Open()
    {
        _open = true;
        GameManager.Instance?.UIManager?.ShowMessage("Chest storage open.", 1.2f);
    }

    public void Close()
    {
        _open = false;
    }

    public bool IsOpen => _open;

    /// <summary>Move one copy of an item from the player's tool inventory into the chest.</summary>
    public bool Deposit(string itemId)
    {
        var tm = ToolManager.Instance;
        if (tm == null || tm.CountItem(itemId) <= 0) return false;
        tm.RemoveItemAmount(itemId, 1);
        GameManager.Instance?.UIManager?.ShowMessage("Stored: " + ItemName(itemId), 1.2f);
        return true;
    }

    /// <summary>Move one copy of an item from the chest back to the player (best effort).</summary>
    public bool Withdraw(string itemId)
    {
        var tm = ToolManager.Instance;
        if (tm == null) return false;
        if (!tm.CanHoldItem(itemId)) return false;
        tm.AddItem(itemId, 1);
        GameManager.Instance?.UIManager?.ShowMessage("Withdrew: " + ItemName(itemId), 1.2f);
        return true;
    }

    private static string ItemName(string itemId)
    {
        var item = ItemDatabase.Get(itemId);
        return item != null ? item.displayName : itemId;
    }

    private void BuildVisual()
    {
        if (_visual != null) return;
        _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _visual.name = "HomeChest_" + ChestId;
        _visual.transform.SetParent(transform, false);
        _visual.transform.localScale = new Vector3(1.0f, 0.8f, 0.9f);
        Destroy(_visual.GetComponent<Collider>());
        var r = _visual.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.5f, 0.62f, 0.3f);
    }

    private void OnDestroy()
    {
        if (_visual != null) Destroy(_visual);
    }
}