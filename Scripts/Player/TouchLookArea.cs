using UnityEngine;
using UnityEngine.EventSystems;

public class TouchLookArea : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        MobileInputController.AddLookDelta(eventData.delta * SettingsManager.TouchSensitivity);
    }
}
