using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared static helpers for the Phase 8 new-world HUD components. Provides a screen-space
/// overlay canvas factory and a compact fill-bar builder so the player/enemy/skill HUDs stay
/// consistent without duplicating UI construction. Depends only on UGUI (no TMPro bar work).
/// </summary>
public static class HudCanvas
{
    /// <summary>Create a screen-space overlay canvas named <paramref name="name"/> (sortingOrder 20).</summary>
    public static Canvas CreateOverlay(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Build a horizontal fill bar returning the fill <see cref="Image"/> (fillAmount 0..1 mapped in fillRect).</summary>
    public static Image CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 pos, Vector2 size, Color bgColor, Color fillColor)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.pivot = pivot;
        rootRect.anchoredPosition = pos;
        rootRect.sizeDelta = size;

        var bg = new GameObject("Bg");
        bg.transform.SetParent(root.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = bgColor;
        bgImg.raycastTarget = false;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(root.transform, false);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.02f, 0.5f);
        fillRect.anchorMax = new Vector2(0.98f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = new Vector2(Mathf.Max(2f, size.x - 6f), Mathf.Max(2f, size.y - 6f));
        var fillImg = fill.AddComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        fillImg.color = fillColor;
        fillImg.raycastTarget = false;

        return fillImg;
    }

    /// <summary>Create a small background panel anchored to a rect.</summary>
    public static RectTransform CreateBackdrop(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rect;
    }
}