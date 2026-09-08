using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drop target on an inventory slot (backpack storage grid or hotbar mirror row). A stack dragged
/// from another slot (see <see cref="ItemDragHandle"/>) is moved here on release via
/// <see cref="ToolManager.MoveSlot"/>.
/// </summary>
public sealed class ItemDropTarget : MonoBehaviour, IDropHandler
{
    public int Slot;

    public void OnDrop(PointerEventData eventData)
    {
        // An equipped weapon dragged off a hand slot (see WeaponDragHandle) → unequip it back
        // into the bag. Dragged armed weapons have no ToolManager slot, so handle them first.
        string unequipId = WeaponDragHandle.DraggingWeaponId;
        if (!string.IsNullOrEmpty(unequipId))
        {
            CharacterInfoUI.Instance?.UnequipWeapon(unequipId, Slot);
            return;
        }

        int from = ItemDragHandle.DraggingSlot;
        if (from < 0 || from == Slot) return;

        var tm = ToolManager.Instance;
        if (tm == null) return;
        tm.MoveSlot(from, Slot);
        CharacterInfoUI.Instance?.RefreshInventoryUi();
    }
}