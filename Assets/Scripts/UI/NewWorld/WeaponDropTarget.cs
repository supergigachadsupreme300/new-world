using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drop target on the Equipment tab's L. Hand / R. Hand slots. A weapon dragged from the
/// Inventory tab (see <see cref="WeaponDragHandle"/>) is equipped into this slot on release.
/// </summary>
public sealed class WeaponDropTarget : MonoBehaviour, IDropHandler
{
    public EquipSlot Slot;

    public void OnDrop(PointerEventData eventData)
    {
        string dragging = WeaponDragHandle.DraggingWeaponId;
        if (string.IsNullOrEmpty(dragging)) return;
        var ui = CharacterInfoUI.Instance;
        if (ui != null)
            ui.EquipWeaponFromDrop(dragging, Slot);
    }
}