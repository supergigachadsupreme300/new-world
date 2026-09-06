using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drop target on the Equipment tab's L. Hand / R. Hand slots. Weapons dragged from any
/// inventory slot (backpack grid / hotbar mirror, see <see cref="ItemDragHandle"/>) — or from
/// the legacy weapon rack (see <see cref="WeaponDragHandle"/>) — are equipped on release.
/// </summary>
public sealed class WeaponDropTarget : MonoBehaviour, IDropHandler
{
    public EquipSlot Slot;

    public void OnDrop(PointerEventData eventData)
    {
        int from = ItemDragHandle.DraggingSlot;
        var tm = ToolManager.Instance;
        if (from >= 0 && tm != null)
        {
            var slot = tm.PeekSlot(from);
            if (slot != null && !string.IsNullOrEmpty(slot.Type) && WeaponCatalog.Find(slot.Type) != null)
            {
                var ui = CharacterInfoUI.Instance;
                if (ui != null)
                    ui.EquipWeaponFromDrop(slot.Type, Slot);
                return;
            }
        }

        string dragging = WeaponDragHandle.DraggingWeaponId;
        if (string.IsNullOrEmpty(dragging)) return;
        var ui2 = CharacterInfoUI.Instance;
        if (ui2 != null)
            ui2.EquipWeaponFromDrop(dragging, Slot);
    }
}