using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
    private GameObject _libraryLearnPanel;
    private Transform _libraryLearnContent;
    private TMP_Text _libraryLearnTitle;
    private int _libraryRowCounter;

    public bool IsLibraryLearnShown => _libraryLearnPanel != null && _libraryLearnPanel.activeSelf;

    public void ShowLibraryLearnPanel(bool show)
    {
        if (!show)
        {
            if (_libraryLearnPanel != null)
                _libraryLearnPanel.SetActive(false);
            return;
        }
        if (_libraryLearnPanel == null)
            CreateLibraryLearnPanel();
        if (_libraryLearnPanel == null)
            return;
        RefreshLibraryLearnChrome();
        PopulateLibraryLearnRows();
        _libraryLearnPanel.SetActive(true);
    }

    public void RefreshLibraryLearnPanel()
    {
        if (_libraryLearnPanel == null || !_libraryLearnPanel.activeSelf)
            return;
        RefreshLibraryLearnChrome();
        PopulateLibraryLearnRows();
    }

    private void CreateLibraryLearnPanel()
    {
        float panelW = Mathf.Min(Screen.width * 0.62f, 620f);
        float panelH = Mathf.Min(Screen.height * 0.6f, 540f);
        float pad = 20f;

        _libraryLearnPanel = CreateMenuPanel("LibraryLearnPanel", Vector2.zero, new Vector2(panelW, panelH));

        _libraryLearnTitle = EnsureText("LibraryLearnTitle", new Vector2(0f, panelH * 0.36f),
            Localization.T("CON MUỐN HỌC BẢN THIẾT KẾ NÀO?"), (int)(panelH * 0.06f),
            _libraryLearnPanel.transform, TextAlignmentOptions.Center, true,
            new Vector2(panelW - pad * 4, panelH * 0.07f));

        float contentW = panelW - pad * 4 - 8f;
        float scrollbarW = 12f;
        float viewportH = panelH * 0.56f;
        float viewportY = panelH * 0.03f;

        var viewportGo = new GameObject("LibraryLearnViewport");
        viewportGo.transform.SetParent(_libraryLearnPanel.transform, false);
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRt.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRt.pivot = new Vector2(0.5f, 0.5f);
        viewportRt.anchoredPosition = new Vector2(0f, viewportY);
        viewportRt.sizeDelta = new Vector2(contentW, viewportH);
        viewportGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = new GameObject("LibraryLearnContent");
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        _libraryLearnContent = contentGo.transform;

        var scrollbarGo = new GameObject("LibraryLearnScrollbar");
        scrollbarGo.transform.SetParent(_libraryLearnPanel.transform, false);
        var scrollbarRt = scrollbarGo.AddComponent<RectTransform>();
        scrollbarRt.anchorMin = new Vector2(0.5f, 0.5f);
        scrollbarRt.anchorMax = new Vector2(0.5f, 0.5f);
        scrollbarRt.pivot = new Vector2(0.5f, 0.5f);
        scrollbarRt.anchoredPosition = new Vector2(contentW * 0.5f - scrollbarW * 0.5f - 2f, viewportY);
        scrollbarRt.sizeDelta = new Vector2(scrollbarW, viewportH);
        var scrollbarImg = scrollbarGo.AddComponent<Image>();
        scrollbarImg.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        var handleArea = new GameObject("SlidingArea");
        handleArea.transform.SetParent(scrollbarGo.transform, false);
        var haRt = handleArea.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(2f, 4f);
        haRt.offsetMax = new Vector2(-2f, -4f);
        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var hRt = handle.AddComponent<RectTransform>();
        hRt.anchorMin = Vector2.zero;
        hRt.anchorMax = new Vector2(1f, 0.3f);
        hRt.offsetMin = Vector2.zero;
        hRt.offsetMax = Vector2.zero;
        var hImg = handle.AddComponent<Image>();
        hImg.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        scrollbar.handleRect = hRt;
        scrollbar.targetGraphic = hImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var scrollRect = viewportGo.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 0f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        CreateButton("LibraryLearnBackButton", _libraryLearnPanel.transform, Localization.T("Quay Lại"),
            new Vector2(0f, -panelH * 0.44f), () => ShowLibraryLearnPanel(false));

        _libraryLearnPanel.SetActive(false);
    }

    private void RefreshLibraryLearnChrome()
    {
        if (_libraryLearnPanel == null)
            return;
        if (_libraryLearnTitle != null)
            _libraryLearnTitle.text = Localization.T("CON MUỐN HỌC BẢN THIẾT KẾ NÀO?");
        var back = _libraryLearnPanel.transform.Find("LibraryLearnBackButton");
        if (back != null)
        {
            var txt = back.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = Localization.T("Quay Lại");
        }
    }

    private void PopulateLibraryLearnRows()
    {
        if (_libraryLearnContent == null)
            return;
        for (int i = _libraryLearnContent.childCount - 1; i >= 0; i--)
            Object.Destroy(_libraryLearnContent.GetChild(i).gameObject);

        var wb = WorldBuilder.Instance;
        var list = wb != null ? wb.GetResearchableBlueprints() : new List<(string Name, int Cost)>();
        float rowH = Mathf.Max(26f, Screen.height * 0.045f);
        long money = 0;
        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player != null)
            money = player.Money;

        if (list.Count == 0)
        {
            var empty = new GameObject("LibraryLearnEmpty");
            empty.transform.SetParent(_libraryLearnContent, false);
            var er = empty.AddComponent<RectTransform>();
            er.sizeDelta = new Vector2(0f, rowH * 2f);
            var le = empty.AddComponent<LayoutElement>();
            le.preferredHeight = rowH * 2f;
            le.flexibleHeight = 0f;
            var txt = empty.AddComponent<TextMeshProUGUI>();
            if (defaultTmpFont != null)
                txt.font = defaultTmpFont;
            txt.text = Localization.T("Con đã nắm được mọi tri thức ở thư viện này rồi. Hãy truyền lại cho thế hệ sau nhé.");
            txt.fontSize = (int)(rowH * 0.55f);
            txt.color = new Color(0.9f, 0.9f, 0.9f);
            txt.alignment = TextAlignmentOptions.Center;
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            bool affordable = money >= list[i].Cost;
            int captured = i;

            var btn = CreateButton("LibraryLearnRow_" + (_libraryRowCounter++), _libraryLearnContent,
                string.Format("{0} — {1}🪙", Localization.BuildingName(list[i].Name), list[i].Cost),
                Vector2.zero, () => LibrarianNPC.Instance?.ChooseResearch(captured),
                new Vector2(viewportMaxWidth(), rowH));

            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(viewportMaxWidth(), rowH);
            var le = btn.GetComponent<LayoutElement>();
            if (le == null)
                le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = viewportMaxWidth();
            le.preferredHeight = rowH;
            le.flexibleWidth = 1f;
            le.flexibleHeight = 0f;

            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = affordable ? new Color(0.16f, 0.38f, 0.2f, 1f) : new Color(0.3f, 0.3f, 0.34f, 1f);
            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = string.Format("{0} — {1}🪙", Localization.BuildingName(list[i].Name), list[i].Cost);
                txt.fontSize = Mathf.Max(11, (int)(rowH * 0.5f));
                txt.alignment = TextAlignmentOptions.Left;
                txt.verticalAlignment = VerticalAlignmentOptions.Middle;
                txt.color = affordable ? Color.white : new Color(0.75f, 0.75f, 0.75f);
            }
        }
    }

    private float viewportMaxWidth()
    {
        float panelW = Mathf.Min(Screen.width * 0.62f, 620f);
        return panelW - 96f;
    }
}