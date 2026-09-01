using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
    private void CreateEventTestPanel(float panelWidth, float panelHeight, float padding)
    {
        var rem = RandomEventManager.Instance;
        int eventCount = rem != null ? rem.EventCount : 0;
        if (eventCount == 0) return;

        float evPanelW = Mathf.Min(panelWidth * 1.4f, 680f);
        float evPanelH = Mathf.Min(panelHeight * 0.9f, 560f);
        _eventTestPanel = CreateMenuPanel("EventTestPanel", Vector2.zero, new Vector2(evPanelW, evPanelH));

        EnsureText("EventTestTitle", new Vector2(0f, evPanelH * 0.38f), Localization.T("SỰ KIỆN TEST"),
            (int)(evPanelH * 0.06f), _eventTestPanel.transform, TextAlignmentOptions.Center, true,
            new Vector2(evPanelW - padding * 4, evPanelH * 0.07f));

        float tierLabelH = evPanelH * 0.05f;
        float btnH = Mathf.Max(28f, Screen.height * 0.035f);
        float rowSpacing = 3f;
        float tierSpacing = 6f;
        int cols = 3;
        string[] tierLabels = { "Tier 0 — Cơ Bản", "Tier 1 — Nâng Cao", "Tier 2 — Quý Hiếm" };
        Color[] tierBg = { new Color(0.15f, 0.35f, 0.15f, 0.6f), new Color(0.15f, 0.25f, 0.5f, 0.6f), new Color(0.45f, 0.35f, 0.1f, 0.6f) };

        // Group events by tier
        var tierEvents = new List<(string name, Color color, int index)>[3];
        for (int t = 0; t < 3; t++) tierEvents[t] = new List<(string, Color, int)>();
        for (int i = 0; i < eventCount; i++)
        {
            int tier = rem.GetEventTier(i);
            if (tier >= 0 && tier < 3)
                tierEvents[tier].Add((rem.GetEventName(i), rem.GetEventColor(i), i));
        }

        // Panel layout
        float contentW = evPanelW - padding * 4 - 8f;
        float scrollbarW = 12f;
        float viewportW = contentW - scrollbarW;
        float viewportH = evPanelH * 0.65f;
        float viewportY = evPanelH * 0.01f;

        // Viewport
        var viewportObject = new GameObject("EventTestViewport");
        viewportObject.transform.SetParent(_eventTestPanel.transform, false);
        var viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, viewportY);
        viewportRect.sizeDelta = new Vector2(contentW, viewportH);
        viewportObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewportObject.AddComponent<RectMask2D>();

        // Content
        var contentObject = new GameObject("EventTestContent");
        contentObject.transform.SetParent(viewportObject.transform, false);
        var contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        var contentVLG = contentObject.AddComponent<VerticalLayoutGroup>();
        contentVLG.spacing = 0f;
        contentVLG.padding = new RectOffset(4, 4, 4, 4);
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = false;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;

        // Scrollbar
        var scrollbarGo = new GameObject("EventTestScrollbar");
        scrollbarGo.transform.SetParent(_eventTestPanel.transform, false);
        var scrollbarRt = scrollbarGo.AddComponent<RectTransform>();
        scrollbarRt.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarRt.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarRt.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRt.anchoredPosition = new Vector2(contentW * 0.5f - scrollbarW * 0.5f - 2f, viewportY);
        scrollbarRt.sizeDelta = new Vector2(scrollbarW, viewportH);
        var scrollbarImg = scrollbarGo.AddComponent<Image>();
        scrollbarImg.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();

        var scrollbarHandleArea = new GameObject("SlidingArea");
        scrollbarHandleArea.transform.SetParent(scrollbarGo.transform, false);
        var handleAreaRt = scrollbarHandleArea.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(2f, 4f);
        handleAreaRt.offsetMax = new Vector2(-2f, -4f);

        var scrollbarHandle = new GameObject("Handle");
        scrollbarHandle.transform.SetParent(scrollbarHandleArea.transform, false);
        var handleRt = scrollbarHandle.AddComponent<RectTransform>();
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = new Vector2(1f, 0.3f);
        handleRt.offsetMin = Vector2.zero;
        handleRt.offsetMax = Vector2.zero;
        var handleImg = scrollbarHandle.AddComponent<Image>();
        handleImg.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // ScrollRect
        var scrollRect = viewportObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 0f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Build 3-column grid per tier
        float totalContentH = 4f; // top padding
        for (int t = 0; t < 3; t++)
        {
            if (tierEvents[t].Count == 0) continue;

            // Tier header (full width)
            string tierLabel = t < tierLabels.Length ? tierLabels[t] : "Tier " + t;
            var tierGo = new GameObject("TierHeader_" + t);
            tierGo.transform.SetParent(contentObject.transform, false);
            var tierRt = tierGo.AddComponent<RectTransform>();
            tierRt.sizeDelta = new Vector2(contentW, tierLabelH);
            var tierLE = tierGo.AddComponent<LayoutElement>();
            tierLE.preferredWidth = contentW;
            tierLE.preferredHeight = tierLabelH;
            tierLE.flexibleWidth = 1f;
            var tierImg = tierGo.AddComponent<Image>();
            tierImg.color = tierBg[Mathf.Min(t, 2)];
            tierImg.raycastTarget = false;
            var tierTextGo = new GameObject("TierLabel_" + t);
            tierTextGo.transform.SetParent(tierGo.transform, false);
            var tierTextRt = tierTextGo.AddComponent<RectTransform>();
            tierTextRt.anchorMin = Vector2.zero;
            tierTextRt.anchorMax = Vector2.one;
            tierTextRt.offsetMin = Vector2.zero;
            tierTextRt.offsetMax = Vector2.zero;
            var tierTmp = tierTextGo.AddComponent<TextMeshProUGUI>();
            if (defaultTmpFont != null) tierTmp.font = defaultTmpFont;
            tierTmp.text = Localization.T(tierLabel);
            tierTmp.fontSize = (int)(tierLabelH * 0.65f);
            tierTmp.color = Color.white;
            tierTmp.alignment = TextAlignmentOptions.Center;
            tierTmp.raycastTarget = false;
            totalContentH += tierLabelH + tierSpacing;

            // Rows of 3 columns
            var events = tierEvents[t];
            int rowCount = Mathf.CeilToInt((float)events.Count / cols);
            float rowW = contentW - 8f;
            float colW = (rowW - (cols - 1) * rowSpacing) / cols;

            for (int r = 0; r < rowCount; r++)
            {
                var rowGo = new GameObject("EventRow_" + t + "_" + r);
                rowGo.transform.SetParent(contentObject.transform, false);
                var rowRt = rowGo.AddComponent<RectTransform>();
                rowRt.sizeDelta = new Vector2(rowW, btnH);
                var rowLE = rowGo.AddComponent<LayoutElement>();
                rowLE.preferredWidth = rowW;
                rowLE.preferredHeight = btnH;
                rowLE.flexibleWidth = 1f;
                var rowHLG = rowGo.AddComponent<HorizontalLayoutGroup>();
                rowHLG.spacing = rowSpacing;
                rowHLG.childAlignment = TextAnchor.MiddleCenter;
                rowHLG.childControlWidth = false;
                rowHLG.childControlHeight = false;
                rowHLG.childForceExpandWidth = false;
                rowHLG.childForceExpandHeight = false;

                for (int c = 0; c < cols; c++)
                {
                    int flatIdx = r * cols + c;
                    if (flatIdx >= events.Count) break;

                    var ev = events[flatIdx];
                    int capturedIndex = ev.index;
                    Color evColor = ev.color;

                    var btn = CreateButton("EventBtn_" + capturedIndex, rowGo.transform, ev.name,
                        Vector2.zero, () =>
                        {
                            rem.ForceEventByIndex(capturedIndex);
                            ShowEventTestPanel(false);
                            _settingsPanel?.SetActive(false);
                            _pauseMenuPanel?.SetActive(false);
                            if (GameManager.Instance != null && GameManager.Instance.GamePaused)
                                GameManager.Instance.TogglePause(false);
                        }, new Vector2(colW, btnH));

                    var btnLE = btn.GetComponent<LayoutElement>();
                    if (btnLE == null) btnLE = btn.gameObject.AddComponent<LayoutElement>();
                    btnLE.preferredWidth = colW;
                    btnLE.preferredHeight = btnH;

                    var btnImage = btn.GetComponent<Image>();
                    if (btnImage != null)
                    {
                        Color bg = evColor * 0.3f;
                        bg.a = 1f;
                        btnImage.color = bg;
                    }
                    var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnText != null)
                    {
                        btnText.color = evColor;
                        btnText.fontSize = Mathf.Max(10, (int)(colW * 0.07f));
                    }
                }
                totalContentH += btnH + rowSpacing;
            }
            totalContentH += tierSpacing;
        }
        totalContentH += 4f; // bottom padding

        // Set content height without ContentSizeFitter (smooth scroll)
        contentRect.sizeDelta = new Vector2(0f, totalContentH);

        CreateButton("EventTestBackButton", _eventTestPanel.transform, Localization.T("Quay Lại"),
            new Vector2(0f, -evPanelH * 0.38f), () => ShowEventTestPanel(false));
        _eventTestPanel.SetActive(false);
    }

    public void ShowEventTestPanel(bool show)
    {
        if (_eventTestPanel != null)
            _eventTestPanel.SetActive(show);
    }
}
