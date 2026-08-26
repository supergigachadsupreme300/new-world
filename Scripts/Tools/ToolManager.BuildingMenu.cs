using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class ToolManager
{
    private void ToggleBuildingMenu()
    {
        if (_buildingMenuOpen)
        {
            CloseBuildingMenu();
            return;
        }

        if (_buildingMenuPanel == null)
            CreateBuildingMenu();

        _buildingMenuOpen = true;
        _buildingMenuPanel.SetActive(true);
        RefreshBuildingMenuLabels();
        GameManager.Instance?.TogglePause(true);
        GameManager.Instance?.UIManager?.ShowPauseMenu(false);
        GameInput.SetCursorLocked(false);
    }

    private void CloseBuildingMenu()
    {
        _buildingMenuOpen = false;
        if (_buildingMenuPanel != null)
            _buildingMenuPanel.SetActive(false);
        GameManager.Instance?.TogglePause(false);
        GameInput.SetCursorLocked(true);
        UpdateBuildingPreviewVisibility();
    }

    private void CreateBuildingMenu()
    {
        var canvasGo = GameObject.Find("HUD_Canvas");
        var canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        float sw = Screen.width;
        float sh = Screen.height;
        float panelW = Mathf.Min(sw * 0.50f, 500f);
        float panelH = Mathf.Min(sh * 0.65f, 420f);
        float fontS = Mathf.Max(14f, sh / 42f);
        float btnH = sh * 0.065f;
        float padding = sh * 0.015f;

        _buildingMenuPanel = new GameObject("BuildingMenu");
        _buildingMenuPanel.transform.SetParent(canvas.transform, false);
        var panelRect = _buildingMenuPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelW, panelH);
        var panelImg = _buildingMenuPanel.AddComponent<Image>();
        panelImg.color = new Color(0.18f, 0.2f, 0.27f, 0.95f);

        var title = MakeBMText("BuildTitle", _buildingMenuPanel.transform, Localization.T("Xây Dựng"),
            new Vector2(0f, panelH * 0.42f), new Vector2(panelW - 80, fontS * 1.6f), (int)(fontS * 1.3f));

        MakeBMButton("BuildClose", _buildingMenuPanel.transform, "X",
            new Vector2(panelW * 0.44f, panelH * 0.42f), new Vector2(btnH, btnH),
            (int)fontS, new Color(0.75f, 0.38f, 0.41f), CloseBuildingMenu);

        float headerH = panelH * 0.18f;
        float viewportH = panelH - headerH - padding * 2;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(_buildingMenuPanel.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(0.5f, 0.5f);
        vpRect.anchorMax = new Vector2(0.5f, 0.5f);
        vpRect.pivot = new Vector2(0.5f, 0.5f);
        vpRect.anchoredPosition = new Vector2(0f, -padding);
        vpRect.sizeDelta = new Vector2(panelW - padding * 2, viewportH);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(0.12f, 0.13f, 0.18f, 1f);
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = padding;
        layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);

        var scrollRect = _buildingMenuPanel.AddComponent<ScrollRect>();
        scrollRect.viewport = vpRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;

        var wb = WorldBuilder.Instance;
        int count = wb != null ? wb.BuildingCount : 0;

        for (int i = 0; i < count; i++)
        {
            var def = wb.GetBuildingByIndex(i);
            int index = i;
            bool unlocked = wb.IsBlueprintUnlocked(def.Name);

            string costLabel = "";
            if (def.WoodCost > 0) costLabel += def.WoodCost + "\U0001FAB5 ";
            if (def.StoneCost > 0) costLabel += def.StoneCost + "\U0001FAA8";
            costLabel = costLabel.Trim();
            string btnLabel = Localization.BuildingName(def.Name) + "    " + costLabel;
            if (!unlocked) btnLabel += "   \U0001F512";

            var btn = MakeBMButton("BuildBtn_" + i, content.transform, btnLabel,
                Vector2.zero, new Vector2(panelW - padding * 4, btnH),
                (int)fontS, new Color(0.26f, 0.3f, 0.37f),
                () => SelectBuilding(index));

            if (!unlocked)
            {
                btn.interactable = false;
                var targetImg = btn.targetGraphic as Graphic;
                if (targetImg != null) targetImg.color = new Color(0.18f, 0.2f, 0.26f);
                var btnText = btn.GetComponentInChildren<TMP_Text>();
                if (btnText != null) btnText.color = new Color(0.55f, 0.55f, 0.55f);
            }

            var le = btn.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = btnH;
        }

        _buildingMenuPanel.SetActive(false);
    }

    private void SelectBuilding(int index)
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        var def = wb.GetBuildingByIndex(index);
        if (!wb.IsBlueprintUnlocked(def.Name))
        {
            _uiManager?.ShowMessage(Localization.T("Bản thiết kế này bị khóa. Hãy đến Thư Viện tìm hiểu thêm!"), 2f);
            return;
        }
        wb.CurrentBuildingIndex = index;
        _buildingChosen = true;
        CloseBuildingMenu();
        _uiManager?.ShowMessage(Localization.F("Đã chọn: {0}. Nhấp để đặt.", Localization.BuildingName(def.Name)), 2f);
    }

    private void RefreshBuildingMenuLabels()
    {
        if (_buildingMenuPanel == null) return;
        var titleTf = _buildingMenuPanel.transform.Find("BuildTitle");
        if (titleTf != null)
        {
            var t = titleTf.GetComponent<TMP_Text>();
            if (t != null) t.text = Localization.T("Xây Dựng");
        }
        var wb = WorldBuilder.Instance;
        int count = wb != null ? wb.BuildingCount : 0;
        for (int i = 0; i < count; i++)
        {
            var def = wb.GetBuildingByIndex(i);
            bool unlocked = wb.IsBlueprintUnlocked(def.Name);
            string costLabel = "";
            if (def.WoodCost > 0) costLabel += def.WoodCost + "\U0001FAB5 ";
            if (def.StoneCost > 0) costLabel += def.StoneCost + "\U0001FAA8";
            costLabel = costLabel.Trim();
            string label = Localization.BuildingName(def.Name) + "    " + costLabel;
            if (!unlocked) label += "   \U0001F512";
            var btnGo = _buildingMenuPanel.transform.Find("Viewport/Content/BuildBtn_" + i);
            if (btnGo == null) continue;
            var t = btnGo.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = label;
            var btn = btnGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = unlocked;
                var targetImg = btn.targetGraphic as Graphic;
                if (targetImg != null) targetImg.color = unlocked ? new Color(0.26f, 0.3f, 0.37f) : new Color(0.18f, 0.2f, 0.26f);
                if (t != null) t.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            }
        }
    }

    private string GetVietnameseBuildingName(string name)
    {
        return Localization.BuildingName(name);
    }

    private string GetMansionPartVietnameseName(string typeName)
    {
        return Localization.MansionPartName(typeName);
    }

    private TMP_Text MakeBMText(string name, Transform parent, string text,
        Vector2 pos, Vector2 size, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            tmp.font = _uiManager.defaultTmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private Button MakeBMButton(string name, Transform parent, string label,
        Vector2 pos, Vector2 size, int fontSize, Color color,
        UnityEngine.Events.UnityAction callback)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(callback);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var tr = textGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            tmp.font = _uiManager.defaultTmpFont;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }
}
