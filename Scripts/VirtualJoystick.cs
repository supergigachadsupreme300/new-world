using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public RectTransform knob;
    public float radius = 60f;
    public float deadZone = 0.08f;

    public Vector2 Value { get; private set; }

    private Vector2 _baseCenter;

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateKnob(eventData.position);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        UpdateKnob(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateKnob(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Reset();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Reset();
    }

    private void UpdateKnob(Vector2 screenPos)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out var local);
        Vector2 dir = local;

        float mag = dir.magnitude;
        if (mag > radius)
            dir = dir * (radius / mag);

        if (knob != null)
            knob.anchoredPosition = dir;

        Vector2 value = mag > 0.0001f ? dir / radius : Vector2.zero;
        value = Vector2.ClampMagnitude(value, 1f);
        if (value.magnitude < deadZone)
            value = Vector2.zero;
        Value = value;
    }

    private void Reset()
    {
        Value = Vector2.zero;
        if (knob != null)
            knob.anchoredPosition = Vector2.zero;
    }
}
