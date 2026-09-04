using UnityEngine;
using UnityEngine.EventSystems;

public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public string action;
    public bool holdable;

    public void OnPointerDown(PointerEventData eventData)
    {
        MobileInputController.SetActionPressed(action, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        MobileInputController.SetActionPressed(action, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (holdable)
            MobileInputController.SetActionPressed(action, false);
    }
}
