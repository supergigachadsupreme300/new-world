using UnityEngine;

public enum ControlMode
{
    PC,
    Mobile
}

public static class GameInput
{
    public static ControlMode Mode = ControlMode.PC;

    public static bool IsMobile => Mode == ControlMode.Mobile;

    public static void SetCursorLocked(bool locked)
    {
        if (IsMobile) return;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
