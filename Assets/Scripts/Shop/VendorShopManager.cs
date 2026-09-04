using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class VendorShopManager : MonoBehaviour
{
    private bool _initialized;
    private Canvas _canvas;
    private UIManager _uiManager;
    private Dictionary<ShopItem, int> _originalSellPrices = new Dictionary<ShopItem, int>();
    private Dictionary<ShopItem, int> _originalBuyPrices = new Dictionary<ShopItem, int>();

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
    private string _mode = "vendor";
    private float _buyDiscountMul = 1f;
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
        new ShopItem { Type = "wheat_seed", Label = "Hạt Lúa Mì", Price = 3 },
        new ShopItem { Type = "corn_seed", Label = "Hạt Ngô", Price = 4 },
        new ShopItem { Type = "carrot_seed", Label = "Hạt Cà Rốt", Price = 3 },
        new ShopItem { Type = "tomato_seed", Label = "Hạt Cà Chua", Price = 4 },
        new ShopItem { Type = "strawberry_seed", Label = "Hạt Dâu Tây", Price = 5 },
        new ShopItem { Type = "pumpkin_seed", Label = "Hạt Bí Ngòi", Price = 4 },
        new ShopItem { Type = "onion_seed", Label = "Hạt Hành Tây", Price = 3 },
        new ShopItem { Type = "sugarcane_seed", Label = "Hạt Mía", Price = 4 },
        new ShopItem { Type = "rice_seed", Label = "Hạt Gạo", Price = 3 },
        new ShopItem { Type = "fertilizer", Label = "Phân Bón", Price = 8 },
        new ShopItem { Type = "watering_can", Label = "Bình Tưới", Price = 6 },
    };

    private List<ShopItem> _toolsBuyItems = new List<ShopItem>
    {
        new ShopItem { Type = "axe", Label = "Rìu", Price = 25 },
        new ShopItem { Type = "pickaxe", Label = "Cuốc Chim", Price = 25 },
        new ShopItem { Type = "hoe", Label = "Cuốc", Price = 20 },
        new ShopItem { Type = "scythe", Label = "Lưỡi Hái", Price = 20 },
        new ShopItem { Type = "hammer", Label = "Búa", Price = 25 },
        new ShopItem { Type = "club", Label = "Gậy", Price = 80 },
        new ShopItem { Type = "fishing_rod", Label = "Cần Câu", Price = 50 },
        new ShopItem { Type = "rosary", Label = "Tràng Hạt", Price = 100 },
    };

    private List<ShopItem> _convenienceBuyItems = new List<ShopItem>
    {
        new ShopItem { Type = "mi_hao_hao", Label = "Mì Hảo Hảo", Price = 10 },
        new ShopItem { Type = "banh_mi", Label = "Bánh Mì", Price = 12 },
        new ShopItem { Type = "banh_tet", Label = "Bánh Tét", Price = 18 },
        new ShopItem { Type = "nuoc_dau", Label = "Nước Dừa", Price = 8 },
        new ShopItem { Type = "tra_da", Label = "Trà Đá", Price = 5 },
        new ShopItem { Type = "soda", Label = "Soda", Price = 6 },
        new ShopItem { Type = "keo", Label = "Kẹo", Price = 3 },
    };

    private List<ShopItem> _groceryBuyItems = new List<ShopItem>
    {
        new ShopItem { Type = "tu_gao", Label = "Túi Gạo", Price = 15 },
        new ShopItem { Type = "duong", Label = "Đường", Price = 6 },
        new ShopItem { Type = "muoi", Label = "Muối", Price = 5 },
        new ShopItem { Type = "xap_phong", Label = "Xà Phòng", Price = 8 },
        new ShopItem { Type = "mi_chinh", Label = "Mì Chính", Price = 9 },
    };

    private List<ShopItem> _storeBuyItems;

    private List<ShopItem> _restaurantBuyItems = new List<ShopItem>
    {
        new ShopItem { Type = "com_trang", Label = "Cơm Trắng", Price = 15 },
        new ShopItem { Type = "com_tam", Label = "Cơm Tấm", Price = 30 },
        new ShopItem { Type = "com_ga", Label = "Cơm Gà", Price = 45 },
        new ShopItem { Type = "com_chieu", Label = "Cơm Chiên", Price = 70 },
    };

    private List<ShopItem> _cafeBuyItems = new List<ShopItem>
    {
        new ShopItem { Type = "cafe_den", Label = "Cà Phê Đen", Price = 40 },
        new ShopItem { Type = "banh_mi", Label = "Bánh Mì", Price = 12 },
        new ShopItem { Type = "tra_da", Label = "Trà Đá", Price = 5 },
    };

    private List<ShopItem> _fishingBuyItems = new List<ShopItem>
    {
        new ShopItem { Type = "fishing_rod", Label = "Cần Câu", Price = 50 },
        new ShopItem { Type = "rod_upgrade_1", Label = "Cần Câu Cấp 1", Price = 40 },
        new ShopItem { Type = "rod_upgrade_2", Label = "Cần Câu Cấp 2", Price = 80 },
        new ShopItem { Type = "rod_upgrade_3", Label = "Cần Câu Cấp 3", Price = 200 },
        new ShopItem { Type = "fishing_bait", Label = "Mồi Câu", Price = 15 },
        new ShopItem { Type = "fishing_chum", Label = "Mồi Bả", Price = 25 },
        new ShopItem { Type = "fish_carp", Label = "Cá Chép", Price = 20 },
        new ShopItem { Type = "fish_salmon", Label = "Cá Hồi", Price = 35 },
        new ShopItem { Type = "fish_tuna", Label = "Cá Ngừ", Price = 55 },
        new ShopItem { Type = "fish_pufferfish", Label = "Cá Nóc", Price = 80 },
    };

    private List<ShopItem> _sellItems = new List<ShopItem>
    {
        new ShopItem { Type = "wheat", Label = "Lúa Mì", Price = 10 },
        new ShopItem { Type = "damaged_wheat", Label = "Lúa Mì Hư", Price = 3 },
        new ShopItem { Type = "corn", Label = "Ngô", Price = 12 },
        new ShopItem { Type = "damaged_corn", Label = "Ngô Hư", Price = 4 },
        new ShopItem { Type = "potato", Label = "Khoai Tây", Price = 11 },
        new ShopItem { Type = "damaged_potato", Label = "Khoai Tây Hư", Price = 3 },
        new ShopItem { Type = "carrot", Label = "Cà Rốt", Price = 9 },
        new ShopItem { Type = "damaged_carrot", Label = "Cà Rốt Hư", Price = 2 },
        new ShopItem { Type = "tomato", Label = "Cà Chua", Price = 13 },
        new ShopItem { Type = "damaged_tomato", Label = "Cà Chua Hư", Price = 3 },
        new ShopItem { Type = "strawberry", Label = "Dâu Tây", Price = 15 },
        new ShopItem { Type = "damaged_strawberry", Label = "Dâu Tây Hư", Price = 4 },
        new ShopItem { Type = "pumpkin", Label = "Bí Ngòi", Price = 14 },
        new ShopItem { Type = "damaged_pumpkin", Label = "Bí Ngòi Hư", Price = 3 },
        new ShopItem { Type = "onion", Label = "Hành Tây", Price = 10 },
        new ShopItem { Type = "damaged_onion", Label = "Hành Tây Hư", Price = 2 },
        new ShopItem { Type = "sugarcane", Label = "Mía", Price = 11 },
        new ShopItem { Type = "damaged_sugarcane", Label = "Mía Hư", Price = 3 },
        new ShopItem { Type = "rice", Label = "Gạo", Price = 12 },
        new ShopItem { Type = "damaged_rice", Label = "Gạo Hư", Price = 3 },
        new ShopItem { Type = "fish_carp", Label = "Cá Chép", Price = 15 },
        new ShopItem { Type = "fish_salmon", Label = "Cá Hồi", Price = 25 },
        new ShopItem { Type = "fish_tuna", Label = "Cá Ngừ", Price = 40 },
        new ShopItem { Type = "fish_pufferfish", Label = "Cá Nóc", Price = 60 },

        // Crafted goods
        new ShopItem { Type = "xoi_gac", Label = "Xôi Gấc", Price = 22 },
        new ShopItem { Type = "sup_bi_ngo", Label = "Súp Bí Ngòi", Price = 24 },
        new ShopItem { Type = "mut_ca_rot", Label = "Mứt Cà Rốt", Price = 26 },
        new ShopItem { Type = "trai_cay_kho", Label = "Trái Cây Khô", Price = 28 },
        new ShopItem { Type = "dua_chua", Label = "Dưa Chua", Price = 30 },
        new ShopItem { Type = "ruou_gao", Label = "Rượu Gạo", Price = 45 },
        new ShopItem { Type = "tuong_ot", Label = "Tương Ớt", Price = 32 },
        new ShopItem { Type = "ruou_tang", Label = "Rượu Thuốc", Price = 60 },
        new ShopItem { Type = "tinh_duoc", Label = "Tinh Dược", Price = 75 },

        // Enemy materials
        new ShopItem { Type = "demon_horn", Label = "Sừng Quỷ", Price = 25 },
        new ShopItem { Type = "dark_essence", Label = "Tinh Chất Bóng Tối", Price = 30 },
        new ShopItem { Type = "bone", Label = "Xương Quái Vật", Price = 12 },
    };

    private List<ShopItem> _currentItems
    {
        get
        {
            if (_activeTab == "sell")
                return _sellItems;
            switch (_mode)
            {
                case "restaurant": return _restaurantBuyItems;
                case "cafe": return _cafeBuyItems;
                case "tools": return _toolsBuyItems;
                case "convenience":
                    if (_storeBuyItems == null)
                    {
                        _storeBuyItems = new List<ShopItem>();
                        _storeBuyItems.AddRange(_toolsBuyItems);
                        _storeBuyItems.AddRange(_convenienceBuyItems);
                        _storeBuyItems.AddRange(_groceryBuyItems);
                    }
                    return _storeBuyItems;
                case "grocery": return _groceryBuyItems;
                case "fishing": return _fishingBuyItems;
                default: return _buyItems;
            }
        }
    }
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
        var hudGo = GameObject.Find("HUD_Canvas");
        _canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        _uiManager = GameManager.Instance?.UIManager;
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

        _titleText = MakeText("ShopTitle", _shopPanel.transform, Localization.T("Cửa Hàng Bà Tân"),
            new Vector2(0f, panelH * 0.42f), new Vector2(panelW - padding * 4, fontS * 1.8f),
            (int)(fontS * 1.4f), TextAlignmentOptions.Center);

        _closeBtn = MakeButton("ShopClose", _shopPanel.transform, "X",
            new Vector2(panelW * 0.45f, panelH * 0.42f), new Vector2(btnH, btnH),
            (int)fontS, new Color(0.75f, 0.38f, 0.41f), Close);

        float tabY = panelH * 0.3f;
        float tabW = panelW * 0.3f;
        _tabBuy = MakeButton("TabBuy", _shopPanel.transform, Localization.T("Mua"),
            new Vector2(-tabW * 0.5f, tabY), new Vector2(tabW, btnH),
            (int)fontS, new Color(0.37f, 0.51f, 0.68f), () => SwitchTab("buy"));
        _tabSell = MakeButton("TabSell", _shopPanel.transform, Localization.T("Bán"),
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

        _sellAllBtn = MakeButton("SellAll", _shopPanel.transform, Localization.T("Bán Tất Cả"),
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
        OpenShop("Cửa Hàng Bà Tân", "vendor");
    }

    public void OpenRestaurant()
    {
        OpenShop("Nhà Hàng", "restaurant");
    }

    public void OpenCafe()
    {
        OpenShop("Quán Cà Phê", "cafe");
    }

    public void OpenTools()
    {
        OpenShop("Cửa Hàng Nông Cụ", "tools");
    }

    public void OpenConvenience()
    {
        OpenShop("Cửa Hàng Tiện Lợi", "convenience");
    }

    public void OpenGrocery()
    {
        OpenShop("Cửa Hàng Tạp Hóa", "grocery");
    }

    public void OpenFishing()
    {
        OpenShop("Cửa Hàng Câu Cá", "fishing");
    }

    private void OpenShop(string titleKey, string mode)
    {
        if (_shopPanel == null)
        {
            Debug.LogError("VendorShopManager: _shopPanel is null. Initialize() may have failed.");
            return;
        }
        _page = 1;
        _activeTab = "buy";
        _mode = mode;
        _buyDiscountMul = mode == "restaurant"
            ? (FriendshipManager.Instance != null ? FriendshipManager.Instance.ShopDiscountFor("chef") : 1f)
            : mode == "fishing"
                ? (FriendshipManager.Instance != null ? FriendshipManager.Instance.ShopDiscountFor("fishshop") : 1f)
                : 1f;
        _shopPanel.SetActive(true);
        _wasPausedBeforeOpen = GameManager.Instance != null && GameManager.Instance.GamePaused;

        if (BuffaloDialog.Instance != null && BuffaloDialog.Instance.IsDialogActive)
            BuffaloDialog.Instance.Hide();

        if (_uiManager != null)
        {
            _uiManager.ShowTutorial(false);
            _uiManager.ShowRecordPanel(false);
            _uiManager.ShowQuestPanel(false);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(true);

        if (_uiManager != null)
            _uiManager.ShowPauseMenu(false);
        GameInput.SetCursorLocked(false);

        if (_titleText != null) _titleText.text = Localization.T(titleKey);
        SetButtonLabel(_tabBuy, Localization.T("Mua"));
        SetButtonLabel(_tabSell, Localization.T("Bán"));
        SetButtonLabel(_sellAllBtn, Localization.T("Bán Tất Cả"));
        bool hasSellTab = mode == "vendor";
        if (_tabSell != null) _tabSell.gameObject.SetActive(hasSellTab);
        if (_sellAllBtn != null) _sellAllBtn.gameObject.SetActive(hasSellTab);

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
            GameInput.SetCursorLocked(true);
        }
        else
        {
            if (_uiManager != null)
                _uiManager.ShowPauseMenu(true);
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
                    _slots[i].Label.text = $"{Localization.T(item.Label)}\n{BuyPrice(item)}g";
                    _slots[i].Button.onClick.AddListener(() => BuyItem(item));
                }
                else
                {
                    int owned = ToolManager.Instance != null ? ToolManager.Instance.CountItem(item.Type) : 0;
                    _slots[i].Label.text = $"{Localization.T(item.Label)}\n{owned}x · {item.Price}g";
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

        string tabLabel = _activeTab == "buy" ? Localization.T("Mua") : Localization.T("Bán");
        _pageLabel.text = Localization.F("{0} · Trang {1}/{2}", tabLabel, _page, total);
        _prevBtn.interactable = _page > 1;
        _nextBtn.interactable = _page < total;
    }

    private int BuyPrice(ShopItem item)
    {
        if (_buyDiscountMul <= 0f) return item.Price;
        return Mathf.RoundToInt(item.Price * _buyDiscountMul);
    }

    private void BuyItem(ShopItem item)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        int price = BuyPrice(item);
        if (player.Money < price)
        {
            ShowMessage(Localization.T("Không đủ tiền"));
            return;
        }

        if (item.Type == "rod_upgrade_1" || item.Type == "rod_upgrade_2" || item.Type == "rod_upgrade_3")
        {
            var fp = FishingProgression.Instance;
            int target = item.Type == "rod_upgrade_3" ? 3 : (item.Type == "rod_upgrade_2" ? 2 : 1);
            if (fp == null || fp.RodLevel + 1 != target)
            {
                ShowMessage(Localization.T("Bạn cần nâng cấp cần câu đúng thứ tự."));
                return;
            }
            string result = fp.TryUpgrade(target);
            player.Money -= price;
            ShowMessage(result);
            return;
        }

        var tm = ToolManager.Instance;
        if (tm == null) return;

        if (!tm.CanHoldItem(item.Type))
        {
            ShowMessage(Localization.T("Túi đồ đầy"));
            return;
        }

        tm.AddItem(item.Type, 1);
        player.Money -= price;
        ShowMessage(Localization.F("Đã mua {0}", Localization.T(item.Label)));
    }

    private void SellItem(ShopItem item)
    {
        var tm = ToolManager.Instance;
        var player = GameManager.Instance?.Player;
        if (tm == null || player == null) return;

        int owned = tm.CountItem(item.Type);
        if (owned <= 0)
        {
            ShowMessage(Localization.F("Không có {0} để bán", Localization.T(item.Label)));
            return;
        }

        tm.RemoveAllItems(item.Type);
        int earned = owned * item.Price;
        player.Money += earned;
        GameStats.AddMoneyEarned(earned);
        QuestManager.Instance?.AddProgress("money_earned", earned);
        ShowMessage(Localization.F("Đã bán {0} {1} (+{2}g)", owned, Localization.T(item.Label), earned));
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
            GameStats.AddMoneyEarned(totalEarned);
            QuestManager.Instance?.AddProgress("money_earned", totalEarned);
            ShowMessage(Localization.F("Đã bán tất cả (+{0}g)", totalEarned));
            UpdatePage();
        }
        else
        {
            ShowMessage(Localization.T("Không có gì để bán"));
        }
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        var t = button.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = label;
    }

    private void ShowMessage(string text)
    {
        GameManager.Instance?.UIManager?.ShowMessage(text, 1.5f);
    }

    private TMP_Text MakeText(string name, Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions align)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, pos, text, fontSize, Color.white, size, false, false, align, false);

    private Button MakeButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, Color color, UnityEngine.Events.UnityAction callback)
        => CountryLife.Helpers.UIHelper.MakeButton(name, parent, label, pos, size, fontSize, color, callback);

    private void SetButtonColor(Button btn, Color color)
    {
        var img = btn?.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    public void ApplyPriceMultiplier(float multiplier)
    {
        foreach (var item in _sellItems)
        {
            if (!_originalSellPrices.ContainsKey(item))
                _originalSellPrices[item] = item.Price;
            item.Price = Mathf.Max(1, Mathf.RoundToInt(_originalSellPrices[item] * multiplier));
        }
    }

    public void ApplyBuyPriceMultiplier(float multiplier)
    {
        foreach (var item in _buyItems)
        {
            if (!_originalBuyPrices.ContainsKey(item))
                _originalBuyPrices[item] = item.Price;
            item.Price = Mathf.Max(1, Mathf.RoundToInt(_originalBuyPrices[item] * multiplier));
        }
    }

    public void ResetSellPrices()
    {
        foreach (var item in _sellItems)
            if (_originalSellPrices.TryGetValue(item, out int orig))
                item.Price = orig;
    }

    public void ResetBuyPrices()
    {
        foreach (var item in _buyItems)
            if (_originalBuyPrices.TryGetValue(item, out int orig))
                item.Price = orig;
    }
}
