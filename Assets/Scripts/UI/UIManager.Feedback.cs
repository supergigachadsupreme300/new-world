/// <summary>
/// Feedback partial of UIManager: the scroll-banner message notification (typewriter banner
/// with spreading scroll caps), its dedicated message canvas, and helper builders.
/// </summary>
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
    private void CreateMessageCanvas(float screenWidth, float screenHeight, float lineHeight, float largefontSize)
    {
        if (_messageCanvas != null && _messageText != null) return;

        var canvasGO = new GameObject("MessageCanvas");
        canvasGO.layer = LayerMask.NameToLayer("UI");
        _messageCanvas = canvasGO.AddComponent<Canvas>();
        _messageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _messageCanvas.sortingOrder = 1100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.SetActive(true);

        // Banner container: anchored at top-center, slides down from above on show.
        var bannerGO = new GameObject("MessageBanner");
        bannerGO.transform.SetParent(canvasGO.transform, false);
        var banner = bannerGO.AddComponent<RectTransform>();
        banner.anchorMin = new Vector2(0.5f, 1f);
        banner.anchorMax = new Vector2(0.5f, 1f);
        banner.pivot = new Vector2(0.5f, 1f);
        _bannerWidth = screenWidth * 0.78f;
        _bannerHeight = lineHeight * 4.0f;
        banner.anchoredPosition = new Vector2(0f, -screenHeight * 0.08f);
        banner.sizeDelta = new Vector2(_bannerWidth, _bannerHeight);
        _messageBannerGroup = bannerGO.AddComponent<CanvasGroup>();
        _messageBannerGroup.interactable = false;
        _messageBannerGroup.blocksRaycasts = false;
        _messageBanner = banner;
        bannerGO.SetActive(false);
        _messageScrollSprite = LoadUiSprite("scroll");
        _messageInsideSprite = LoadUiSprite("scroll_inside");

        // Middle panel — widens as the scrolls spread apart.
        var insideGO = new GameObject("MessageInside");
        insideGO.transform.SetParent(banner, false);
        var inside = insideGO.AddComponent<RectTransform>();
        inside.anchorMin = new Vector2(0.5f, 0.5f);
        inside.anchorMax = new Vector2(0.5f, 0.5f);
        inside.pivot = new Vector2(0.5f, 0.5f);
        inside.sizeDelta = new Vector2(0f, 0f);
        var insideImg = insideGO.AddComponent<Image>();
        insideImg.sprite = _messageInsideSprite;
        insideImg.preserveAspect = false;
        insideImg.color = _messageInsideSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.75f);
        insideImg.raycastTarget = false;
        _messageInside = inside;

        // Message text types out inside the panel.
        var textGO = new GameObject("MessageText");
        textGO.transform.SetParent(insideGO.transform, false);
        var rect = textGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(16f, 8f);
        rect.offsetMax = new Vector2(-16f, -8f);

        _messageText = textGO.AddComponent<TextMeshProUGUI>();
        if (defaultTmpFont != null)
            _messageText.font = defaultTmpFont;
        _messageText.text = "";
        _messageText.fontSize = (int)(largefontSize * 1.8f);
        _messageText.color = Color.white;
        _messageText.alignment = TextAlignmentOptions.Center;
        _messageText.textWrappingMode = TextWrappingModes.Normal;
        _messageText.overflowMode = TextOverflowModes.Overflow;
        _messageText.raycastTarget = false;
        _messageText.margin = new Vector4(20f, 10f, 20f, 10f);

        // Left/right scroll caps — slide down together, then drift apart.
        if (_messageScrollSprite != null)
        {
            float cap = _bannerHeight * 0.9f;
            _messageBannerL = MakeScrollCap(bannerGO.transform, "ScrollL", cap).rectTransform;
            _messageBannerR = MakeScrollCap(bannerGO.transform, "ScrollR", cap).rectTransform;
        }
    }

    private Sprite LoadUiSprite(string name)
    {
        try
        {
            var tex = Resources.Load<Texture2D>(name);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[UI] Failed to load sprite '" + name + "': " + e.Message);
        }
        return null;
    }

    private Image MakeScrollCap(Transform parent, string name, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = _messageScrollSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    public void ShowMessage(string text, float duration)
    {
        if (_messageText == null || _messageCanvas == null)
        {
            Debug.LogWarning("[UI] ShowMessage: _messageText or _messageCanvas is null!");
            return;
        }
        if (!_messageCanvas.gameObject.activeSelf)
            _messageCanvas.gameObject.SetActive(true);
        if (_messageBanner != null && !_messageBanner.gameObject.activeSelf)
            _messageBanner.gameObject.SetActive(true);
        if (_messageBannerGroup != null)
            _messageBannerGroup.alpha = 1f;
        ResetBannerToHidden();
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(ScrollMessageSequence(text, duration));
    }

    private void ResetBannerToHidden()
    {
        if (_messageBanner == null) return;
        float overshoot = _bannerHeight > 0f ? _bannerHeight * 2.2f : 200f;
        _messageBanner.anchoredPosition = new Vector2(0f, -Screen.height * 0.08f + overshoot);
        _messageInside.sizeDelta = new Vector2(0f, 0f);
        float cap = _bannerHeight * 0.9f;
        if (_messageBannerL != null) _messageBannerL.anchoredPosition = new Vector2(-cap * 0.6f, 0f);
        if (_messageBannerR != null) _messageBannerR.anchoredPosition = new Vector2(cap * 0.6f, 0f);
        _messageText.text = "";
        _messageText.alpha = 1f;
    }

    private IEnumerator ScrollMessageSequence(string fullText, float duration)
    {
        if (_messageBanner == null)
            yield break;
        _messageBanner.gameObject.SetActive(true);
        _messageText.text = "";

        // 1. Two scrolls slide down from above together.
        float restY = -Screen.height * 0.08f;
        float startY = _messageBanner.anchoredPosition.y;
        float cap = _bannerHeight * 0.9f;
        float slideT = 0f;
        while (slideT < 1f)
        {
            slideT += Time.deltaTime / 0.3f;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slideT));
            _messageBanner.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, restY, t));
            yield return null;
        }
        _messageBanner.anchoredPosition = new Vector2(0f, restY);

        // 2. Scrolls spread apart while the middle panel widens.
        WaitForSeconds tick = new WaitForSeconds(0.02f);
        float spreadT = 0f;
        while (spreadT < 1f)
        {
            spreadT += Time.deltaTime / 0.35f;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(spreadT), 3f); // ease-out cubic
            float half = _bannerWidth * 0.5f * t;
            if (_messageBannerL != null) _messageBannerL.anchoredPosition = new Vector2(-half - cap * 0.3f, 0f);
            if (_messageBannerR != null) _messageBannerR.anchoredPosition = new Vector2(half + cap * 0.3f, 0f);
            _messageInside.sizeDelta = new Vector2(half * 2f, _bannerHeight * 0.8f);
            yield return null;
        }
        _messageInside.sizeDelta = new Vector2(_bannerWidth, _bannerHeight * 0.8f);

        // 3. Typewriter the text into the panel.
        for (int i = 0; i <= fullText.Length; i++)
        {
            _messageText.text = fullText.Substring(0, i);
            _messageText.SetVerticesDirty();
            yield return tick;
        }
        _messageText.ForceMeshUpdate();

        // 4. Hold, then fade the whole banner out.
        yield return new WaitForSeconds(duration);
        if (_messageBannerGroup != null)
        {
            float fadeT = 0f;
            while (fadeT < 1f)
            {
                fadeT += Time.deltaTime / 0.35f;
                _messageBannerGroup.alpha = 1f - Mathf.Clamp01(fadeT);
                yield return null;
            }
        }

        _messageText.text = string.Empty;
        if (_messageBannerGroup != null)
            _messageBannerGroup.alpha = 1f;
        if (_messageBanner != null)
            _messageBanner.gameObject.SetActive(false);
        _typewriterCoroutine = null;
    }
}