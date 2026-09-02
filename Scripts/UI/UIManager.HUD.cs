using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class UIManager
{
    public void ShowAllGameUI(bool show)
    {
        if (show)
            _platformPanel?.SetActive(false);
        _timeText?.gameObject.SetActive(show);
        _hpText?.gameObject.SetActive(show);
        _staminaText?.gameObject.SetActive(show);
        _moneyText?.gameObject.SetActive(show);
        _questText?.gameObject.SetActive(show);
        for (int i = 0; i < InventorySlotCount; i++)
            if (_inventorySlots[i] != null) _inventorySlots[i].SetActive(show);
        _messageCanvas?.gameObject.SetActive(show);
        _mobSpawnerText?.gameObject.SetActive(show);
        _crosshairText?.gameObject.SetActive(show);
        _infoText?.gameObject.SetActive(show);
        if (!show)
        {
            HideSkillPanel();
            HideFriendPanel();
        }
    }

    public void SetCrosshairVisible(bool visible)
    {
        if (_crosshairText != null)
            _crosshairText.gameObject.SetActive(visible);
    }

    public void SetInfoText(string text)
    {
        if (_infoText != null)
        {
            _infoText.text = text ?? "";
            _infoText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    public void SetStatsBgVisible(bool visible)
    {
        if (_statsBg != null)
            _statsBg.gameObject.SetActive(visible);
    }

    public void ShowBossBar(string bossName, int currentHp, int maxHp)
    {
        if (_bossBarRoot == null)
            CreateBossBar();
        if (_bossBarRoot == null)
            return;

        _bossBarRoot.SetActive(true);
        if (_bossBarName != null)
            _bossBarName.text = bossName;
        SetBossBar(currentHp, maxHp);
    }

    public void SetBossBar(int currentHp, int maxHp)
    {
        if (_bossBarFill == null)
            return;
        float ratio = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
        _bossBarFill.fillAmount = ratio;
        var color = ratio > 0.4f ? new Color(0.9f, 0.25f, 0.2f) : new Color(1f, 0.55f, 0.1f);
        _bossBarFill.color = color;
    }

    public void HideBossBar()
    {
        if (_bossBarRoot != null)
            _bossBarRoot.SetActive(false);
    }

    private void CreateBossBar()
    {
        if (_canvas == null)
            return;
        float sw = Screen.width;
        float sh = Screen.height;

        _bossBarRoot = new GameObject("BossBarRoot");
        _bossBarRoot.transform.SetParent(_canvas.transform, false);

        var rootRect = _bossBarRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -sh * 0.08f);
        rootRect.sizeDelta = new Vector2(sw * 0.5f, 34f);

        var bg = new GameObject("BossBarBg");
        bg.transform.SetParent(_bossBarRoot.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(sw * 0.5f, 20f);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.75f);
        bgImg.raycastTarget = false;

        var fill = new GameObject("BossBarFill");
        fill.transform.SetParent(_bossBarRoot.transform, false);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = new Vector2(sw * 0.5f - 6f, 14f);
        var fillImg = fill.AddComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        fillImg.color = new Color(0.9f, 0.25f, 0.2f);
        fillImg.raycastTarget = false;
        _bossBarFill = fillImg;

        _bossBarName = EnsureText("BossBarName", new Vector2(0f, 20f), "", 16, _bossBarRoot.transform,
            TextAlignmentOptions.Center, false, new Vector2(sw * 0.5f, 20f));

        _bossBarRoot.SetActive(false);
    }

    private void CreateKarmaBar()
    {
        if (_canvas == null) return;
        float sw = Screen.width;
        float sh = Screen.height;

        _karmaBarRoot = new GameObject("KarmaBarRoot");
        _karmaBarRoot.transform.SetParent(_canvas.transform, false);

        var rootRect = _karmaBarRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, sh * 0.06f + sw * 0.065f + 10f);
        rootRect.sizeDelta = new Vector2(sw * 0.35f, 28f);

        var bg = new GameObject("KarmaBarBg");
        bg.transform.SetParent(_karmaBarRoot.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(sw * 0.35f, 16f);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.75f);
        bgImg.raycastTarget = false;

        var fill = new GameObject("KarmaBarFill");
        fill.transform.SetParent(_karmaBarRoot.transform, false);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = new Vector2(sw * 0.35f - 6f, 10f);
        var fillImg = fill.AddComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        fillImg.color = new Color(1f, 0.84f, 0f);
        fillImg.raycastTarget = false;
        _karmaBarFill = fillImg;

        _karmaBarText = EnsureText("KarmaBarText", new Vector2(0f, 18f), "", 14, _karmaBarRoot.transform,
            TextAlignmentOptions.Center, false, new Vector2(sw * 0.35f, 18f));

        _karmaBarRoot.SetActive(false);
    }

    public void ShowKarmaBar(float current, float max)
    {
        if (_karmaBarRoot == null)
            CreateKarmaBar();
        if (_karmaBarRoot == null) return;

        if (!_karmaBarVisible)
        {
            _karmaBarVisible = true;
            _karmaBarRoot.SetActive(true);
        }
        if (_karmaBarFill != null)
            _karmaBarFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        int k = Mathf.FloorToInt(current);
        int km = Mathf.FloorToInt(max);
        if (_karmaBarText != null && (k != _lastKarma || km != _lastKarmaMax))
        {
            _lastKarma = k;
            _lastKarmaMax = km;
            _karmaBarText.text = Localization.T("Phước Đức") + ": " + k + "/" + km;
        }
    }

    public void HideKarmaBar()
    {
        if (_karmaBarVisible)
        {
            _karmaBarVisible = false;
            if (_karmaBarRoot != null)
                _karmaBarRoot.SetActive(false);
        }
    }

    public void UpdateTimeText(int day, float hour)
    {
        if (_timeText == null) return;
        if (day == _lastTimeDay && Mathf.Approximately(hour, _lastTimeHour)) return;
        _lastTimeDay = day;
        _lastTimeHour = hour;
        _timeText.text = Localization.F("Ngày {0} - {1}", day, hour.ToString("00.00"));
    }

    public void UpdatePlayerHud(int hp, int maxHp, float stamina, float maxStamina, long money)
    {
        if (_hpText != null && (hp != _lastHp || maxHp != _lastMaxHp))
        {
            _lastHp = hp;
            _lastMaxHp = maxHp;
            _hpText.text = $"HP: {hp}/{maxHp}";
        }
        if (_staminaText != null && ((int)stamina != (int)_lastStamina || (int)maxStamina != (int)_lastMaxStamina))
        {
            _lastStamina = stamina;
            _lastMaxStamina = maxStamina;
            _staminaText.text = Localization.F("Thể Lực: {0}/{1}", (int)stamina, (int)maxStamina);
        }
        if (_moneyText != null && money != _lastMoney)
        {
            _lastMoney = money;
            _moneyText.text = Localization.F("Tiền: {0}", money);
        }

        var tm = ToolManager.Instance;
        bool holdingRosary = tm != null && tm.GetSelectedItemType() == "rosary";
        var km = KarmaManager.Instance;
        if (holdingRosary && km != null)
            ShowKarmaBar(km.CurrentKarma, km.MaxKarma);
        else
            HideKarmaBar();
    }

    public void UpdateInventoryText(ToolManager.InventorySlot[] slots, int selectedSlot)
    {
        for (int i = 0; i < InventorySlotCount; i++)
        {
            if (i >= slots.Length || _inventorySlotTexts[i] == null) continue;

            var item = slots[i];
            string label = item == null ? "" : (item.Count > 1 ? $"{Localization.ItemName(item.Type)}x{item.Count}" : Localization.ItemName(item.Type));
            _inventorySlotTexts[i].text = $"{i + 1}: {label}";

            bool isSelected = (i == selectedSlot);
            if (_inventorySlotImages[i] != null)
                _inventorySlotImages[i].color = isSelected
                    ? new Color(0.35f, 0.55f, 0.75f, 0.95f)
                    : new Color(0.3f, 0.3f, 0.35f, 0.85f);
            if (_inventorySlotTexts[i] != null)
                _inventorySlotTexts[i].color = isSelected ? Color.yellow : Color.white;
        }
    }

    public void UpdateQuestHud(string text)
    {
        if (_questText != null)
        {
            _questText.text = text;
            ResizeStatsBg();
        }
    }

    private void ResizeStatsBg()
    {
        if (_statsBg == null || _questText == null)
            return;
        _questText.ForceMeshUpdate();
        float needed = Mathf.Max(_questText.preferredHeight, 24f);
        _questText.rectTransform.sizeDelta = new Vector2(300f * _statsScale, needed);
        _statsBg.sizeDelta = new Vector2(320f * _statsScale, (175f + needed + 30f) * _statsScale);
    }

    public void UpdateQuestPanelText(string text)
    {
        if (_questLinesText != null)
            _questLinesText.text = text;
    }

    private string BuildRecordLines(long wheat, long enemies, long earned, long stolen)
    {
        return Localization.F("Lúa đã thu hoạch: {0}", wheat) + "\n"
             + Localization.F("Kẻ thù đã diệt: {0}", enemies) + "\n"
             + Localization.F("Tiền đã kiếm: {0}", earned) + "\n"
             + Localization.F("Tiền bị cướp: {0}", stolen);
    }
}
