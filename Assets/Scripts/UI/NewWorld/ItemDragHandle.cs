using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Makes an inventory stack draggable onto an <see cref="ItemDropTarget"/> (backpack storage grid
/// or hotbar mirror row). Carries the source <see cref="ToolManager"/> slot index in
/// <see cref="DraggingSlot"/> while the ghost follows the pointer; the target's
/// <see cref="ItemDropTarget.OnDrop"/> performs the move via <see cref="ToolManager.MoveSlot"/>.
/// </summary>
public sealed class ItemDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int Slot;

    /// <summary>ToolManager slot index currently being dragged, or -1 when idle.</summary>
    public static int DraggingSlot = -1;

    private GameObject _ghost;
    private RectTransform _ghostRect;

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
        var tm = ToolManager.Instance;
        var slot = tm != null ? tm.PeekSlot(Slot) : null;
        if (slot == null || string.IsNullOrEmpty(slot.Type) || slot.Count <= 0)
            return;

        DraggingSlot = Slot;
        var canvas = FindTopCanvas();
        if (canvas == null) return;

        _ghost = new GameObject("ItemDragGhost");
        _ghost.transform.SetParent(canvas.transform, false);
        _ghostRect = _ghost.AddComponent<RectTransform>();
        _ghostRect.sizeDelta = new Vector2(120f, 30f);
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
        tmp.text = Localization.ItemName(slot.Type) + " " + slot.Count;
        tmp.fontSize = Mathf.Max(12f, Screen.height / 60f);
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
        DraggingSlot = -1;
        CharacterInfoUI.Instance?.OnItemDragEnded();
    }
}