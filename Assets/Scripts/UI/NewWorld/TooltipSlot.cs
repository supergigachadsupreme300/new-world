using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to a UI item slot to show the <see cref="ItemTooltipUI"/> while the cursor hovers it.
/// The id is resolved lazily via <see cref="Bind"/> on each enter, so empty slots show nothing
/// and the tooltip can't go stale after inventory changes.
/// </summary>
public sealed class TooltipSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Func<string> _provider;
    private bool _hovered;

    /// <summary>Bind the live id provider (returns the current item id or null when empty).</summary>
    public void Bind(Func<string> provider) => _provider = provider;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (_provider == null) return;
        string id = _provider();
        if (string.IsNullOrEmpty(id)) return;
        ItemTooltipUI.Show(id, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        ItemTooltipUI.Hide();
    }

    private void OnDisable()
    {
        if (_hovered)
        {
            _hovered = false;
            ItemTooltipUI.Hide();
        }
    }
}