using UnityEngine;

public static class SettingsManager
{
    public const float MinMouseSensitivity = 0.5f;
    public const float MaxMouseSensitivity = 5f;
    public const float DefaultMouseSensitivity = 2.5f;

    public const float MinTouchSensitivity = 0.03f;
    public const float MaxTouchSensitivity = 0.5f;
    public const float DefaultTouchSensitivity = 0.12f;

    private static float _mouseSensitivity;
    private static float _touchSensitivity;
    private static bool _invertY;

    static SettingsManager()
    {
        _mouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat("MouseSensitivity", DefaultMouseSensitivity),
            MinMouseSensitivity, MaxMouseSensitivity);
        _touchSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat("TouchSensitivity", DefaultTouchSensitivity),
            MinTouchSensitivity, MaxTouchSensitivity);
        _invertY = PlayerPrefs.GetInt("InvertY", 0) == 1;
    }

    public static float MouseSensitivity => _mouseSensitivity;
    public static float TouchSensitivity => _touchSensitivity;
    public static bool InvertY => _invertY;

    public static void SetMouseSensitivity(float value)
    {
        _mouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        PlayerPrefs.SetFloat("MouseSensitivity", _mouseSensitivity);
        PlayerPrefs.Save();
    }

    public static void SetTouchSensitivity(float value)
    {
        _touchSensitivity = Mathf.Clamp(value, MinTouchSensitivity, MaxTouchSensitivity);
        PlayerPrefs.SetFloat("TouchSensitivity", _touchSensitivity);
        PlayerPrefs.Save();
    }

    public static void SetInvertY(bool inverted)
    {
        _invertY = inverted;
        PlayerPrefs.SetInt("InvertY", inverted ? 1 : 0);
        PlayerPrefs.Save();
    }
}
