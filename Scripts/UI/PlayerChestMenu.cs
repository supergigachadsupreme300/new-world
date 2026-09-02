using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Two-way player chest panel: store items from the bag into the chest and
/// take items back out. Tabs switch between the chest and the player's bag.
/// Pauses the game while open, like the goblin chest.
/// </summary>
public class PlayerChestMenu : MonoBehaviour
{
    public static PlayerChestMenu Instance { get; private set; }
    public bool IsOpen { get; private set; }

    private Vector3 _chestPos;
    private bool _showingBag;
    private int _openFrame = -10;
    private bool _wasPausedBeforeOpen;
    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _titleText;
    private TMP_Text _statusText;
    private TMP_Text _closeText;
    private GameObject _content;
    private readonly List<GameObject> _tabButtons = new List<GameObject>();
    private readonly List<GameObject> _rows = new List<GameObject>();

    public static PlayerChestMenu Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("PlayerChestMenu");
            Instance = go.AddComponent<PlayerChestMenu>();
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool closePressed = (kb != null && kb.escapeKey.wasPressedThisFrame)
            || (((kb != null && kb.eKey.wasPressedThisFrame) || MobileInputController.Consume("interact"))
                && Time.frameCount > _openFrame);
        if (closePressed)
            Close();
    }

    public void OpenAt(Vector3 chestPos)
    {
        EnsurePanel();
        if (_panel == null)
            return;
        _chestPos = chestPos;
        _showingBag = false;
        _panel.SetActive(true);
        IsOpen = true;
        _openFrame = Time.frameCount;
        _wasPausedBeforeOpen = GameManager.Instance != null && GameManager.Instance.GamePaused;
        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(true);
        GameManager.Instance?.UIManager?.ShowPauseMenu(false);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        if (_panel != null)
            _panel.SetActive(false);
        _chestPos = Vector3.zero;
        if (_wasPausedBeforeOpen)
            GameManager.Instance?.UIManager?.ShowPauseMenu(true);
        else
            GameManager.Instance?.TogglePause(false);
    }

    private void ShowChest()
    {
        _showingBag = false;
        UpdateTabHighlights();
        Refresh();
    }

    private void ShowBag()
    {
        _showingBag = true;
        UpdateTabHighlights();
        Refresh();
    }

    private void TakeFromChest(string itemType)
    {
        var storage = ChestStorageManager.Instance;
        var tm = ToolManager.Instance;
        if (storage == null || tm == null || string.IsNullOrEmpty(itemType))
            return;
        if (!tm.CanHoldItem(itemType))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
            return;
        }
        int count = storage.CountItem(_chestPos, itemType);
        int taken = storage.TakeItem(_chestPos, itemType, count);
        if (taken > 0 && tm.AddItem(itemType, taken))
            GameManager.Instance?.UIManager?.ShowMessage(
                Localization.F("Đã lấy {0} từ rương.", Localization.ItemName(itemType)), 1.5f);
        Refresh();
    }

    private void StoreToChest(string itemType)
    {
        var storage = ChestStorageManager.Instance;
        var tm = ToolManager.Instance;
        if (storage == null || tm == null || string.IsNullOrEmpty(itemType))
            return;
        int count = tm.CountItem(itemType);
        if (count <= 0)
            return;
        if (storage.IsFull(_chestPos, itemType))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Rương đầy."), 1.5f);
            return;
        }
        if (storage.StoreItem(_chestPos, itemType, count) && tm.RemoveItemAmount(itemType, count))
            GameManager.Instance?.UIManager?.ShowMessage(
                Localization.F("Đã cất {0} vào rương.", Localization.ItemName(itemType)), 1.5f);
        Refresh();
    }

    private void Refresh()
    {
        ClearRows();
        bool mobile = GameInput.IsMobile;
        if (_titleText != null)
            _titleText.text = Localization.T("Rương Đồ");
        if (_statusText != null)
            _statusText.text = StatusText();
        if (_closeText != null)
            _closeText.text = mobile ? Localization.T("[Đóng] (Chạm)") : Localization.T("[Đóng] Ấn E");

        float y = -4f;
        if (_showingBag)
        {
            var tm = ToolManager.Instance;
            if (tm == null)
                return;
            for (int i = 0; i < 10; i++)
            {
                var slot = tm.PeekSlot(i);
                if (slot == null)
                    continue;
                string label = string.Format("{0} x{1}", Localization.ItemName(slot.Type), slot.Count);
                CreateRow("StoreRow", label, new Color(0.8f, 0.9f, 0.85f), y, Localization.T("Cất"),
                    () => StoreToChest(slot.Type));
                y -= 46f;
            }
        }
        else
        {
            var storage = ChestStorageManager.Instance;
            if (storage == null)
                return;
            var list = new List<KeyValuePair<string, int>>(storage.GetContents(_chestPos));
            list.Sort((a, b) =>
                string.CompareOrdinal(Localization.ItemName(a.Key), Localization.ItemName(b.Key)));
            foreach (var kv in list)
            {
                string label = string.Format("{0} x{1}", Localization.ItemName(kv.Key), kv.Value);
                CreateRow("TakeRow", label, new Color(0.85f, 0.9f, 0.75f), y, Localization.T("Lấy"),
                    () => TakeFromChest(kv.Key));
                y -= 46f;
            }
        }
        RefreshTabLabels();
    }

    private string StatusText()
    {
        var storage = ChestStorageManager.Instance;
        int used = storage != null ? storage.GetContents(_chestPos).Count : 0;
        return Localization.F("Trong rương: {0}/{1} loại — {2}", used,
            ChestStorageManager.MaxDistinctTypes, _showingBag ? Localization.T("Túi đồ") : Localization.T("Chọn Lấy hoặc Cất"));
    }

    private void UpdateTabHighlights()
    {
        if (_tabButtons.Count < 2)
            return;
        SetTabColor(_tabButtons[0], !_showingBag);
        SetTabColor(_tabButtons[1], _showingBag);
    }

    private static void SetTabColor(GameObject tab, bool active)
    {
        var img = tab.GetComponentInChildren<Image>();
        if (img != null)
            img.color = active ? new Color(0.8f, 0.62f, 0.3f) : new Color(0.35f, 0.33f, 0.3f);
    }

    private void RefreshTabLabels()
    {
        string[] labels = { Localization.T("Rương"), Localization.T("Túi") };
        for (int i = 0; i < _tabButtons.Count && i < labels.Length; i++)
        {
            var txt = _tabButtons[i].GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = labels[i];
        }
    }

    private void CreateRow(string rowName, string label, Color color, float y, string buttonText,
        UnityEngine.Events.UnityAction onClick)
    {
        var row = new GameObject(rowName);
        row.transform.SetParent(_content.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, y);
        rowRt.sizeDelta = new Vector2(0f, 40f);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = ColorPalette.UIBackdrop;

        var labelText = CountryLife.Helpers.UIHelper.MakeText(rowName + "_Txt", rowRt,
            new Vector2(-60f, 0f), label, 17, color, new Vector2(240f, 34f), true, true,
            TextAlignmentOptions.Left, false);
        labelText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        labelText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        labelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelText.rectTransform.anchoredPosition = new Vector2(-35f, 0f);

        var actionGo = new GameObject(rowName + "_Btn");
        actionGo.transform.SetParent(rowRt, false);
        var actionRt = actionGo.AddComponent<RectTransform>();
        actionRt.anchorMin = new Vector2(1f, 0f);
        actionRt.anchorMax = new Vector2(1f, 1f);
        actionRt.pivot = new Vector2(1f, 0.5f);
        actionRt.anchoredPosition = new Vector2(-8f, 0f);
        actionRt.sizeDelta = new Vector2(88f, 32f);

        var actionImg = actionGo.AddComponent<Image>();
        actionImg.color = new Color(0.8f, 0.62f, 0.3f);
        var actionBtn = actionGo.AddComponent<Button>();
        actionBtn.targetGraphic = actionImg;
        actionBtn.onClick.AddListener(onClick);

        CountryLife.Helpers.UIHelper.MakeText(rowName + "_BtnTxt", actionRt,
            Vector2.zero, buttonText, 16, Color.white, new Vector2(80f, 28f), true, true,
            TextAlignmentOptions.Center, false);

        _rows.Add(row);
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            if (row != null)
                Destroy(row);
        }
        _rows.Clear();
    }

    private void CreateTab(string name, string label, RectTransform parent, float x, bool active,
        UnityEngine.Events.UnityAction onClick)
    {
        var tab = new GameObject(name);
        tab.transform.SetParent(parent, false);
        var tabRt = tab.AddComponent<RectTransform>();
        tabRt.anchorMin = new Vector2(0.5f, 1f);
        tabRt.anchorMax = new Vector2(0.5f, 1f);
        tabRt.pivot = new Vector2(0.5f, 1f);
        tabRt.anchoredPosition = new Vector2(x, 0f);
        tabRt.sizeDelta = new Vector2(150f, 36f);

        var img = tab.AddComponent<Image>();
        img.color = active ? new Color(0.8f, 0.62f, 0.3f) : new Color(0.35f, 0.33f, 0.3f);
        var btn = tab.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        CountryLife.Helpers.UIHelper.MakeText(name + "_Txt", tabRt,
            Vector2.zero, label, 18, Color.white, new Vector2(140f, 30f), true, true,
            TextAlignmentOptions.Center, false);

        _tabButtons.Add(tab);
    }

    private void EnsurePanel()
    {
        if (_panel != null)
            return;
        var hud = GameObject.Find("HUD_Canvas");
        _canvas = hud != null ? hud.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;

        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("PlayerChestPanel");
        _panel.transform.SetParent(_canvas.transform, false);
        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(sw * 0.55f, sh * 0.7f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        float panelW = rt.sizeDelta.x;
        float panelH = rt.sizeDelta.y;

        _titleText = MakeText("PlayerChestTitle", rt, new Vector2(0f, panelH * 0.43f),
            "", 24, new Color(0.95f, 0.8f, 0.5f), new Vector2(panelW - 40f, 34f));
        CreateTab("ChestTab", Localization.T("Rương"), rt, -90f, true, ShowChest);
        CreateTab("BagTab", Localization.T("Túi"), rt, 90f, false, ShowBag);
        _statusText = MakeText("PlayerChestStatus", rt, new Vector2(0f, panelH * 0.3f),
            "", 17, new Color(0.85f, 0.85f, 0.9f), new Vector2(panelW - 40f, 30f));

        _content = new GameObject("PlayerChestContent");
        _content.transform.SetParent(_panel.transform, false);
        var contentRt = _content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = new Vector2(0f, -panelH * 0.02f);
        contentRt.sizeDelta = new Vector2(panelW - 40f, panelH * 0.52f);

        _closeText = MakeText("PlayerChestClose", rt, new Vector2(0f, -panelH * 0.43f),
            "", 16, new Color(0.9f, 0.9f, 0.9f), new Vector2(panelW - 40f, 26f));

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}