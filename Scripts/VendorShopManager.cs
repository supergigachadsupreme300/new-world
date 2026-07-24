using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class VendorShopManager : MonoBehaviour
{
    private bool _initialized;
    private Canvas _canvas;

    void Update()
    {
        if (_shopPanel != null && _shopPanel.activeSelf && Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.xKey.wasPressedThisFrame)
                Close();
        }
    }

    private GameObject _shopPanel;
    private Button _tabBuy;
    private Button _tabSell;
    private Button _closeBtn;
    private Button _prevBtn;
    private Button _nextBtn;
    private Button _sellAllBtn;
    private TMP_Text _pageLabel;
    private TMP_Text _titleText;
    private TMP_Text _tabBuyText;
    private TMP_Text _tabSellText;
    private List<ShopSlot> _slots = new List<ShopSlot>();

    private string _activeTab = "buy";
    private int _page = 1;
    private const int ItemsPerPage = 6;
    private const int Cols = 2;

    private class ShopItem
    {
        public string Type;
        public string Label;
        public int Price;
    }

    private List<ShopItem> _buyItems = new List<ShopItem>
    {
        new ShopItem { Type = "axe", Label = "Axe", Price = 25 },
        new ShopItem { Type = "pickaxe", Label = "Pickaxe", Price = 25 },
        new ShopItem { Type = "hoe", Label = "Hoe", Price = 20 },
        new ShopItem { Type = "mi_hao_hao", Label = "Instant Noodles", Price = 10 },
        new ShopItem { Type = "wheat_seed", Label = "Wheat Seed", Price = 3 },
        new ShopItem { Type = "corn_seed", Label = "Corn Seed", Price = 4 },
        new ShopItem { Type = "carrot_seed", Label = "Carrot Seed", Price = 3 },
        new ShopItem { Type = "tomato_seed", Label = "Tomato Seed", Price = 4 },
        new ShopItem { Type = "strawberry_seed", Label = "Strawberry Seed", Price = 5 },
        new ShopItem { Type = "pumpkin_seed", Label = "Pumpkin Seed", Price = 4 },
        new ShopItem { Type = "onion_seed", Label = "Onion Seed", Price = 3 },
        new ShopItem { Type = "sugarcane_seed", Label = "Sugarcane Seed", Price = 4 },
        new ShopItem { Type = "rice_seed", Label = "Rice Seed", Price = 3 },
        new ShopItem { Type = "fertilizer", Label = "Fertilizer", Price = 8 },
        new ShopItem { Type = "watering_can", Label = "Watering Can", Price = 6 },
        new ShopItem { Type = "peashooter_seed", Label = "Peashooter Seed", Price = 10 },
    };

    private List<ShopItem> _sellItems = new List<ShopItem>
    {
        new ShopItem { Type = "wheat", Label = "Wheat", Price = 10 },
        new ShopItem { Type = "damaged_wheat", Label = "Damaged Wheat", Price = 3 },
        new ShopItem { Type = "corn", Label = "Corn", Price = 12 },
        new ShopItem { Type = "damaged_corn", Label = "Damaged Corn", Price = 4 },
        new ShopItem { Type = "potato", Label = "Potato", Price = 11 },
        new ShopItem { Type = "damaged_potato", Label = "Damaged Potato", Price = 3 },
        new ShopItem { Type = "carrot", Label = "Carrot", Price = 9 },
        new ShopItem { Type = "damaged_carrot", Label = "Damaged Carrot", Price = 2 },
        new ShopItem { Type = "tomato", Label = "Tomato", Price = 13 },
        new ShopItem { Type = "damaged_tomato", Label = "Damaged Tomato", Price = 3 },
        new ShopItem { Type = "strawberry", Label = "Strawberry", Price = 15 },
        new ShopItem { Type = "damaged_strawberry", Label = "Damaged Strawberry", Price = 4 },
        new ShopItem { Type = "pumpkin", Label = "Pumpkin", Price = 14 },
        new ShopItem { Type = "damaged_pumpkin", Label = "Damaged Pumpkin", Price = 3 },
        new ShopItem { Type = "onion", Label = "Onion", Price = 10 },
        new ShopItem { Type = "damaged_onion", Label = "Damaged Onion", Price = 2 },
        new ShopItem { Type = "sugarcane", Label = "Sugarcane", Price = 11 },
        new ShopItem { Type = "damaged_sugarcane", Label = "Damaged Sugarcane", Price = 3 },
        new ShopItem { Type = "rice", Label = "Rice", Price = 12 },
        new ShopItem { Type = "damaged_rice", Label = "Damaged Rice", Price = 3 },
    };

    private List<ShopItem> _currentItems => _activeTab == "buy" ? _buyItems : _sellItems;
    private int _totalPages => Mathf.Max(1, (_currentItems.Count + ItemsPerPage - 1) / ItemsPerPage);

    private class ShopSlot
    {
        public GameObject Root;
        public Button Button;
        public TMP_Text Label;
        public ShopItem Item;
    }

    void Start()
    {
        if (!_initialized)
            Initialize();
    }

    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;
        _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;

        float sw = Screen.width;
        float sh = Screen.height;
        float panelW = Mathf.Min(sw * 0.55f, 640f);
        float panelH = Mathf.Min(sh * 0.75f, 560f);
        float fontS = Mathf.Max(14f, sh / 40f);
        float btnH = sh * 0.065f;
        float padding = sh * 0.02f;

        _shopPanel = new GameObject("VendorShop");
        _shopPanel.transform.SetParent(_canvas.transform, false);
        var rect = _shopPanel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(panelW, panelH);
        var img = _shopPanel.AddComponent<Image>();
        img.color = new Color(0.18f, 0.2f, 0.27f, 0.95f);
        img.raycastTarget = false;

        _titleText = MakeText("ShopTitle", _shopPanel.transform, "Vendor Shop",
            new Vector2(0f, panelH * 0.42f), new Vector2(panelW - padding * 4, fontS * 1.8f),
            (int)(fontS * 1.4f), TextAlignmentOptions.Center);

        _closeBtn = MakeButton("ShopClose", _shopPanel.transform, "X",
            new Vector2(panelW * 0.45f, panelH * 0.42f), new Vector2(btnH, btnH),
            (int)fontS, new Color(0.75f, 0.38f, 0.41f), Close);

        float tabY = panelH * 0.3f;
        float tabW = panelW * 0.3f;
        _tabBuy = MakeButton("TabBuy", _shopPanel.transform, "Buy",
            new Vector2(-tabW * 0.5f, tabY), new Vector2(tabW, btnH),
            (int)fontS, new Color(0.37f, 0.51f, 0.68f), () => SwitchTab("buy"));
        _tabSell = MakeButton("TabSell", _shopPanel.transform, "Sell",
            new Vector2(tabW * 0.5f, tabY), new Vector2(tabW, btnH),
            (int)fontS, new Color(0.3f, 0.34f, 0.42f), () => SwitchTab("sell"));

        float startX = -panelW * 0.22f;
        float startY = panelH * 0.14f;
        float spacingX = panelW * 0.44f;
        float spacingY = panelH * 0.16f;

        _slots.Clear();
        for (int i = 0; i < ItemsPerPage; i++)
        {
            int col = i % Cols;
            int row = i / Cols;
            float x = startX + col * spacingX;
            float y = startY - row * spacingY;

            var slot = new ShopSlot();
            slot.Root = new GameObject("ShopSlot_" + i);
            slot.Root.transform.SetParent(_shopPanel.transform, false);
            var sr = slot.Root.AddComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.5f, 0.5f);
            sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(x, y);
            sr.sizeDelta = new Vector2(panelW * 0.38f, btnH * 1.2f);

            var si = slot.Root.AddComponent<Image>();
            si.color = new Color(0.26f, 0.3f, 0.37f);
            slot.Button = slot.Root.AddComponent<Button>();
            slot.Button.targetGraphic = si;

            slot.Label = MakeText("SlotLabel_" + i, slot.Root.transform, "",
                Vector2.zero, sr.sizeDelta, (int)fontS, TextAlignmentOptions.Center);

            _slots.Add(slot);
        }

        float navY = -panelH * 0.36f;
        _prevBtn = MakeButton("ShopPrev", _shopPanel.transform, "<",
            new Vector2(-panelW * 0.28f, navY), new Vector2(btnH * 1.5f, btnH),
            (int)fontS, new Color(0.3f, 0.34f, 0.42f), () => ChangePage(_page - 1));
        _nextBtn = MakeButton("ShopNext", _shopPanel.transform, ">",
            new Vector2(panelW * 0.28f, navY), new Vector2(btnH * 1.5f, btnH),
            (int)fontS, new Color(0.3f, 0.34f, 0.42f), () => ChangePage(_page + 1));

        _pageLabel = MakeText("ShopPage", _shopPanel.transform, "",
            new Vector2(0f, navY), new Vector2(panelW * 0.4f, btnH),
            (int)fontS, TextAlignmentOptions.Center);

        _sellAllBtn = MakeButton("SellAll", _shopPanel.transform, "Sell All",
            new Vector2(0f, -panelH * 0.44f), new Vector2(panelW * 0.35f, btnH * 0.85f),
            (int)(fontS * 0.85f), new Color(0.75f, 0.38f, 0.41f), SellAll);

        _shopPanel.SetActive(false);
        UpdatePage();
    }

    private bool _wasPausedBeforeOpen;

    public bool IsOpen()
    {
        return _shopPanel != null && _shopPanel.activeSelf;
    }

    public void Open()
    {
        if (_shopPanel == null)
        {
            Debug.LogError("VendorShopManager: _shopPanel is null. Initialize() may have failed.");
            return;
        }
        _page = 1;
        _activeTab = "buy";
        _shopPanel.SetActive(true);
        _wasPausedBeforeOpen = GameManager.Instance != null && GameManager.Instance.GamePaused;

        if (GameManager.Instance?.UIManager != null)
        {
            GameManager.Instance.UIManager.ShowInstructions(false);
            GameManager.Instance.UIManager.ShowStatsPanel(false);
            GameManager.Instance.UIManager.ShowQuestPanel(false);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(true);

        if (GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.ShowPauseMenu(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SwitchTab("buy");
    }

    public void Close()
    {
        if (_shopPanel != null)
            _shopPanel.SetActive(false);

        if (!_wasPausedBeforeOpen)
        {
            var player = GameManager.Instance?.Player;
            if (player != null)
                player.EnableInput(true);

            if (GameManager.Instance != null)
                GameManager.Instance.TogglePause(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (GameManager.Instance?.UIManager != null)
                GameManager.Instance.UIManager.ShowPauseMenu(true);
        }
    }

    private void SwitchTab(string tab)
    {
        _activeTab = tab;
        _page = 1;

        var buyColors = tab == "buy" ? new Color(0.37f, 0.51f, 0.68f) : new Color(0.3f, 0.34f, 0.42f);
        var sellColors = tab == "sell" ? new Color(0.37f, 0.51f, 0.68f) : new Color(0.3f, 0.34f, 0.42f);
        SetButtonColor(_tabBuy, buyColors);
        SetButtonColor(_tabSell, sellColors);

        UpdatePage();
    }

    private void ChangePage(int newPage)
    {
        _page = Mathf.Clamp(newPage, 1, _totalPages);
        UpdatePage();
    }

    private void UpdatePage()
    {
        var items = _currentItems;
        int total = _totalPages;
        _page = Mathf.Clamp(_page, 1, total);
        int start = (_page - 1) * ItemsPerPage;

        for (int i = 0; i < _slots.Count; i++)
        {
            int idx = start + i;
            if (idx < items.Count)
            {
                var item = items[idx];
                _slots[i].Item = item;
                _slots[i].Root.SetActive(true);
                _slots[i].Button.onClick.RemoveAllListeners();

                if (_activeTab == "buy")
                {
                    _slots[i].Label.text = $"{item.Label}\n{item.Price}g";
                    _slots[i].Button.onClick.AddListener(() => BuyItem(item));
                }
                else
                {
                    int owned = ToolManager.Instance != null ? ToolManager.Instance.CountItem(item.Type) : 0;
                    _slots[i].Label.text = $"{item.Label}\n{owned}x · {item.Price}g";
                    _slots[i].Button.onClick.AddListener(() => SellItem(item));
                }
            }
            else
            {
                _slots[i].Item = null;
                _slots[i].Root.SetActive(false);
                _slots[i].Label.text = "";
                _slots[i].Button.onClick.RemoveAllListeners();
            }
        }

        string tabLabel = _activeTab == "buy" ? "Buy" : "Sell";
        _pageLabel.text = $"{tabLabel} · Page {_page}/{total}";
        _prevBtn.interactable = _page > 1;
        _nextBtn.interactable = _page < total;
    }

    private void BuyItem(ShopItem item)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        if (player.Money < item.Price)
        {
            ShowMessage("Not enough money");
            return;
        }

        var tm = ToolManager.Instance;
        if (tm == null) return;

        int slot = tm.FindEmptySlot();
        if (slot < 0)
        {
            ShowMessage("Inventory full");
            return;
        }

        tm.AddItem(item.Type, 1);
        player.Money -= item.Price;
        ShowMessage($"Bought {item.Label}");
    }

    private void SellItem(ShopItem item)
    {
        var tm = ToolManager.Instance;
        var player = GameManager.Instance?.Player;
        if (tm == null || player == null) return;

        int owned = tm.CountItem(item.Type);
        if (owned <= 0)
        {
            ShowMessage($"No {item.Label} to sell");
            return;
        }

        tm.RemoveAllItems(item.Type);
        int earned = owned * item.Price;
        player.Money += earned;
        QuestManager.Instance?.AddProgress("money_earned", earned);
        ShowMessage($"Sold {owned} {item.Label} (+{earned}g)");
        UpdatePage();
    }

    private void SellAll()
    {
        var tm = ToolManager.Instance;
        var player = GameManager.Instance?.Player;
        if (tm == null || player == null) return;

        int totalEarned = 0;
        foreach (var item in _sellItems)
        {
            int owned = tm.CountItem(item.Type);
            if (owned > 0)
            {
                tm.RemoveAllItems(item.Type);
                totalEarned += owned * item.Price;
            }
        }

        if (totalEarned > 0)
        {
            player.Money += totalEarned;
            QuestManager.Instance?.AddProgress("money_earned", totalEarned);
            ShowMessage($"Sold all (+{totalEarned}g)");
            UpdatePage();
        }
        else
        {
            ShowMessage("Nothing to sell");
        }
    }

    private void ShowMessage(string text)
    {
        GameManager.Instance?.UIManager?.ShowMessage(text, 1.5f);
    }

    private TMP_Text MakeText(string name, Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions align)
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
        if (GameManager.Instance?.UIManager?.defaultTmpFont != null)
            tmp.font = GameManager.Instance.UIManager.defaultTmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = align;
        return tmp;
    }

    private Button MakeButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, Color color, UnityEngine.Events.UnityAction callback)
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
        if (GameManager.Instance?.UIManager?.defaultTmpFont != null)
            tmp.font = GameManager.Instance.UIManager.defaultTmpFont;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private void SetButtonColor(Button btn, Color color)
    {
        var img = btn?.GetComponent<Image>();
        if (img != null) img.color = color;
    }
}
