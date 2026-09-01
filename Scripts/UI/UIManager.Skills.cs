using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
    private GameObject _skillPanelRoot;
    private TMP_Text _skillFarmingRow;
    private TMP_Text _skillFishingRow;
    private Image _skillFarmingFill;
    private Image _skillFishingFill;
    private TMP_Text _skillPerkText;
    private bool _skillPanelVisible;

    public void ToggleSkillPanel()
    {
        if (_skillPanelVisible)
            HideSkillPanel();
        else
            ShowSkillPanel();
    }

    public void ShowSkillPanel()
    {
        if (_canvas == null) return;
        if (_skillPanelRoot == null)
            CreateSkillPanel();
        if (_skillPanelRoot == null) return;
        _skillPanelVisible = true;
        _skillPanelRoot.SetActive(true);
        UpdateSkillPanel();
    }

    public void HideSkillPanel()
    {
        if (_skillPanelVisible && _skillPanelRoot != null)
            _skillPanelRoot.SetActive(false);
        _skillPanelVisible = false;
    }

    public void UpdateSkillPanel()
    {
        if (!_skillPanelVisible || _skillPanelRoot == null) return;
        var sm = SkillManager.Instance;
        if (sm == null) return;

        string farming = Localization.T("Canh Tác");
        int flv = sm.Level(SkillManager.Track.Farming);
        float fx = sm.XP(SkillManager.Track.Farming);
        float fneed = sm.XPToNext(SkillManager.Track.Farming);
        _skillFarmingRow.text = fneed > 0f
            ? Localization.F("{0} — {1} ({2}/{3})", farming, Localization.F("Cấp {0}", flv), (int)fx, (int)fneed)
            : Localization.F("{0} — {1} ({2})", farming, Localization.F("Cấp {0}", flv), Localization.T("TỐI ĐA"));
        if (_skillFarmingFill != null)
            _skillFarmingFill.fillAmount = sm.XPNormalized(SkillManager.Track.Farming);

        string fishing = Localization.T("Câu Cá");
        int slv = sm.Level(SkillManager.Track.Fishing);
        float sx = sm.XP(SkillManager.Track.Fishing);
        float sneed = sm.XPToNext(SkillManager.Track.Fishing);
        _skillFishingRow.text = sneed > 0f
            ? Localization.F("{0} — {1} ({2}/{3})", fishing, Localization.F("Cấp {0}", slv), (int)sx, (int)sneed)
            : Localization.F("{0} — {1} ({2})", fishing, Localization.F("Cấp {0}", slv), Localization.T("TỐI ĐA"));
        if (_skillFishingFill != null)
            _skillFishingFill.fillAmount = sm.XPNormalized(SkillManager.Track.Fishing);

        string perkLine = "";
        if (sm.HasFarmingPerk2) perkLine += Localization.T("Năng suất: 25% cơ hội +1 nông sản") + "\n";
        if (sm.HasFarmingPerk4) perkLine += Localization.T("Tiết kiệm sức: toàn bộ dụng cụ -25% thể lực") + "\n";
        if (sm.HasFishingPerk2) perkLine += Localization.T("Mẻ kép: 20% cơ hội bắt đôi") + "\n";
        if (sm.HasFishingPerk4) perkLine += Localization.T("Kéo nhanh: +15% tốc độ cuốn cá") + "\n";
        if (string.IsNullOrEmpty(perkLine))
            perkLine = Localization.T("Đạt cấp 2 và 4 để mở kỹ năng đặc biệt.");
        _skillPerkText.text = perkLine;
    }

    private void CreateSkillPanel()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        float panelW = sw * 0.3f;
        float panelH = 150f;

        _skillPanelRoot = new GameObject("SkillPanelRoot");
        _skillPanelRoot.transform.SetParent(_canvas.transform, false);

        var rootRect = _skillPanelRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0.5f);
        rootRect.anchorMax = new Vector2(0f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(sw * 0.02f, 0f);
        rootRect.sizeDelta = new Vector2(panelW, panelH);

        var bg = new GameObject("SkillPanelBg");
        bg.transform.SetParent(_skillPanelRoot.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 1f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.78f);
        bgImg.raycastTarget = false;

        string title = Localization.T("Kỹ Năng");
        _skillFarmingRow = EnsureText("SkillFarmingRow", new Vector2(0f, panelH * 0.5f - 24f), title + " — ", 16,
            _skillPanelRoot.transform, TextAlignmentOptions.Center, false, new Vector2(panelW, 22f));

        var farmingBar = new GameObject("SkillFarmingBar");
        farmingBar.transform.SetParent(_skillPanelRoot.transform, false);
        var farmingBarRect = farmingBar.AddComponent<RectTransform>();
        farmingBarRect.anchorMin = new Vector2(0.5f, 0.5f);
        farmingBarRect.anchorMax = new Vector2(0.5f, 0.5f);
        farmingBarRect.sizeDelta = new Vector2(panelW - 16f, 10f);
        farmingBarRect.anchoredPosition = new Vector2(0f, 26f);
        var farmingBg = farmingBar.AddComponent<Image>();
        farmingBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        farmingBg.raycastTarget = false;

        var farmingFill = new GameObject("SkillFarmingFill");
        farmingFill.transform.SetParent(farmingBar.transform, false);
        var farmingFillRect = farmingFill.AddComponent<RectTransform>();
        farmingFillRect.anchorMin = new Vector2(0f, 0f);
        farmingFillRect.anchorMax = new Vector2(1f, 1f);
        farmingFillRect.offsetMin = Vector2.zero;
        farmingFillRect.offsetMax = Vector2.zero;
        var farmingFillImg = farmingFill.AddComponent<Image>();
        farmingFillImg.type = Image.Type.Filled;
        farmingFillImg.fillMethod = Image.FillMethod.Horizontal;
        farmingFillImg.fillAmount = 0f;
        farmingFillImg.color = new Color(0.35f, 0.85f, 0.4f);
        farmingFillImg.raycastTarget = false;
        _skillFarmingFill = farmingFillImg;

        _skillFishingRow = EnsureText("SkillFishingRow", new Vector2(0f, -4f), Localization.T("Câu Cá") + " — ", 16,
            _skillPanelRoot.transform, TextAlignmentOptions.Center, false, new Vector2(panelW, 22f));

        var fishingBar = new GameObject("SkillFishingBar");
        fishingBar.transform.SetParent(_skillPanelRoot.transform, false);
        var fishingBarRect = fishingBar.AddComponent<RectTransform>();
        fishingBarRect.anchorMin = new Vector2(0.5f, 0.5f);
        fishingBarRect.anchorMax = new Vector2(0.5f, 0.5f);
        fishingBarRect.sizeDelta = new Vector2(panelW - 16f, 10f);
        fishingBarRect.anchoredPosition = new Vector2(0f, -22f);
        var fishingBg = fishingBar.AddComponent<Image>();
        fishingBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        fishingBg.raycastTarget = false;

        var fishingFill = new GameObject("SkillFishingFill");
        fishingFill.transform.SetParent(fishingBar.transform, false);
        var fishingFillRect = fishingFill.AddComponent<RectTransform>();
        fishingFillRect.anchorMin = new Vector2(0f, 0f);
        fishingFillRect.anchorMax = new Vector2(1f, 1f);
        fishingFillRect.offsetMin = Vector2.zero;
        fishingFillRect.offsetMax = Vector2.zero;
        var fishingFillImg = fishingFill.AddComponent<Image>();
        fishingFillImg.type = Image.Type.Filled;
        fishingFillImg.fillMethod = Image.FillMethod.Horizontal;
        fishingFillImg.fillAmount = 0f;
        fishingFillImg.color = new Color(0.35f, 0.6f, 0.9f);
        fishingFillImg.raycastTarget = false;
        _skillFishingFill = fishingFillImg;

        _skillPerkText = EnsureText("SkillPerkText", new Vector2(0f, -panelH * 0.5f + 18f), "", 13,
            _skillPanelRoot.transform, TextAlignmentOptions.Center, false, new Vector2(panelW - 8f, 60f));

        _skillPanelRoot.SetActive(false);
    }
}
