using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Makes a weapon draggable. Two uses:
///   • Weapon rack entries — set <see cref="WeaponId"/> and drop onto a hand-slot
///     <see cref="WeaponDropTarget"/> to equip.
///   • Equipped hand slots — leave <see cref="WeaponId"/> empty and set <see cref="Slot"/>; the id is
///     resolved live from the rig on the hand. Dropping it anywhere (e.g. a storage slot, see
///     <see cref="ItemDropTarget"/>) unequips it back into the bag.
/// Holds the dragged weapon id in <see cref="DraggingWeaponId"/> while the ghost follows the
/// pointer; the drop target performs the equip/unequip.
/// </summary>
public sealed class WeaponDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string WeaponId;

    /// <summary>When set, the dragged id is read from the equipped rig on this hand slot.</summary>
    public EquipSlot Slot = (EquipSlot)(-1);

    /// <summary>Weapon id currently being dragged, or null when idle.</summary>
    public static string DraggingWeaponId;

    /// <summary>The hand slot a dragged equipped weapon was pulled from, or <c>(EquipSlot)(-1)</c>.</summary>
    public static EquipSlot DraggingHandSlot = (EquipSlot)(-1);

    private GameObject _ghost;
    private RectTransform _ghostRect;

    /// <summary>
    /// Prefer the explicit <see cref="WeaponId"/>; otherwise read the weapon actually equipped on
    /// this hand slot (so an equipped weapon can be dragged back out), or null when nothing readable.
    /// </summary>
    private string ResolveWeaponId()
    {
        if (!string.IsNullOrEmpty(WeaponId)) return WeaponId;
        if ((int)Slot < 0) return null;

        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        var combat = player != null ? player.GetComponent<CombatController>() : null;
        if (combat == null) return null;

        var hand = Slot == EquipSlot.LeftHand ? combat.LeftHand
            : Slot == EquipSlot.RightHand ? combat.RightHand : null;
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null ? host.Data.id : null;
    }

    private Canvas FindTopCanvas()
    {
        var canvases = GetComponentsInParent<Canvas>(true);
        if (canvases == null || canvases.Length == 0) return null;
        Canvas top = canvases[0];
        for (int i = 1; i < canvases.Length; i++)
            if (canvases[i].sortingOrder > top.sortingOrder)
                top = canvases[i];
        return top;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        string id = ResolveWeaponId();
        if (string.IsNullOrEmpty(id)) return;

        DraggingWeaponId = id;
        DraggingHandSlot = Slot;
        var canvas = FindTopCanvas();
        if (canvas == null) return;

        var weapon = WeaponCatalog.Find(id);
        string name = weapon != null && !string.IsNullOrEmpty(weapon.displayName) ? weapon.displayName : id;

        _ghost = new GameObject("DragGhost");
        _ghost.transform.SetParent(canvas.transform, false);
        _ghostRect = _ghost.AddComponent<RectTransform>();
        _ghostRect.sizeDelta = new Vector2(170f, 36f);
        var img = _ghost.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 0.9f, 0.6f);
        var cg = _ghost.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var label = new GameObject("Label");
        label.transform.SetParent(_ghost.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var tmp = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.text = name;
        tmp.fontSize = Mathf.Max(13f, Screen.height / 56f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        _ghostRect.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_ghostRect != null)
            _ghostRect.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_ghost != null)
            Destroy(_ghost);
        _ghost = null;
        _ghostRect = null;
        DraggingWeaponId = null;
        DraggingHandSlot = (EquipSlot)(-1);
        CharacterInfoUI.Instance?.OnDragDropEnded();
    }
}