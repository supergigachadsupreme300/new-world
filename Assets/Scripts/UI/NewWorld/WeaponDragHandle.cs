using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Makes an inventory weapon entry draggable onto a hand-slot <see cref="WeaponDropTarget"/>.
/// Holds the dragged weapon id in <see cref="DraggingWeaponId"/> while the ghost follows the
/// pointer; the target's <see cref="WeaponDropTarget.OnDrop"/> performs the equip.
/// </summary>
public sealed class WeaponDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string WeaponId;

    /// <summary>Weapon id currently being dragged, or null when idle.</summary>
    public static string DraggingWeaponId;

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
        DraggingWeaponId = WeaponId;
        var canvas = FindTopCanvas();
        if (canvas == null) return;

        var weapon = WeaponCatalog.Find(WeaponId);
        string name = weapon != null && !string.IsNullOrEmpty(weapon.displayName) ? weapon.displayName : WeaponId;

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
        CharacterInfoUI.Instance?.OnDragDropEnded();
    }
}