using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using CountryLife.Helpers;

public class CraftingManager : MonoBehaviour
{
    public class Ingredient
    {
        public string ItemType;
        public int Count;
        public Ingredient(string itemType, int count) { ItemType = itemType; Count = count; }
    }

    public class Recipe
    {
        public string Id;
        public string Category;
        public string ResultType;
        public int ResultCount;
        public List<Ingredient> Ingredients = new List<Ingredient>();

        public Recipe(string id, string category, string resultType, int resultCount, params Ingredient[] ingredients)
        {
            Id = id;
            Category = category;
            ResultType = resultType;
            ResultCount = resultCount;
            Ingredients.AddRange(ingredients);
        }
    }

    public static readonly string[] StationCategories =
    {
        "crafting_stove",
        "preserve_jar",
        "brewing_kettle"
    };

    public static readonly Dictionary<string, string> CategoryNames = new Dictionary<string, string>
    {
        { "crafting_stove", "Bếp Nấu" },
        { "preserve_jar", "Lọ Ngâm" },
        { "brewing_kettle", "Nồi Ủ" }
    };

    public static readonly List<Recipe> Recipes = new List<Recipe>
    {
        // === Cooking (crafting_stove) ===
        new Recipe("com_trang", "crafting_stove", "com_trang", 1,
            new Ingredient("rice", 1)),
        new Recipe("com_ga", "crafting_stove", "com_ga", 1,
            new Ingredient("rice", 1), new Ingredient("meat", 1)),
        new Recipe("com_chieu", "crafting_stove", "com_chieu", 1,
            new Ingredient("com_trang", 1), new Ingredient("egg", 1)),
        new Recipe("xoi_gac", "crafting_stove", "xoi_gac", 1,
            new Ingredient("rice", 1), new Ingredient("pumpkin", 1)),
        new Recipe("sup_bi_ngo", "crafting_stove", "sup_bi_ngo", 1,
            new Ingredient("pumpkin", 1), new Ingredient("salt", 1)),

        // === Preserves (preserve_jar) ===
        new Recipe("mut_ca_rot", "preserve_jar", "mut_ca_rot", 1,
            new Ingredient("carrot", 1), new Ingredient("sugar", 2)),
        new Recipe("trai_cay_kho", "preserve_jar", "trai_cay_kho", 1,
            new Ingredient("strawberry", 1), new Ingredient("sugar", 1)),
        new Recipe("dua_chua", "preserve_jar", "dua_chua", 1,
            new Ingredient("carrot", 1), new Ingredient("onion", 1), new Ingredient("salt", 1)),

        // === Brewing (brewing_kettle) ===
        new Recipe("ruou_gao", "brewing_kettle", "ruou_gao", 1,
            new Ingredient("rice", 3), new Ingredient("sugar", 1)),
        new Recipe("tuong_ot", "brewing_kettle", "tuong_ot", 1,
            new Ingredient("tomato", 2), new Ingredient("salt", 1), new Ingredient("onion", 1)),
        new Recipe("ruou_tang", "brewing_kettle", "ruou_tang", 1,
            new Ingredient("demon_horn", 1), new Ingredient("sugar", 2)),
        new Recipe("tinh_duoc", "brewing_kettle", "tinh_duoc", 1,
            new Ingredient("dark_essence", 1), new Ingredient("honey", 1))
    };

    public static CraftingManager Instance { get; private set; }

    public static CraftingManager Ensure()
    {
        if (Instance != null)
            return Instance;
        var go = new GameObject("CraftingManager");
        return go.AddComponent<CraftingManager>();
    }

    private GameObject _panel;
    private RectTransform _listParent;
    private string _activeCategory;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static string ResolveStationCategory(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;
        Transform t = hitCollider.transform;
        while (t != null)
        {
            foreach (string cat in StationCategories)
            {
                if (t.name == cat)
                    return cat;
            }
            t = t.parent;
        }
        return null;
    }

    public void InteractStation(Collider hitCollider)
    {
        string category = ResolveStationCategory(hitCollider);
        if (category == null)
            return;
        if (IsOpen)
        {
            Close();
            return;
        }
        OpenForCategory(category);
    }

    public void OpenForCategory(string category)
    {
        _activeCategory = category;
        if (_panel == null)
            CreatePanel();
        else
            RebuildList();
        _panel.SetActive(true);
        GameInput.SetCursorLocked(false);
        if (GameManager.Instance != null)
            GameManager.Instance.UIManager?.SetCrosshairVisible(false);

        string title = Localization.T("Chế Tạo");
        if (CategoryNames.TryGetValue(category, out string catName))
            title += " - " + Localization.T(catName);
        var titleText = _panel.transform.Find("TitleText");
        if (titleText != null)
            titleText.GetComponent<TMP_Text>().text = title;
    }

    public void Close()
    {
        if (_panel != null)
            _panel.SetActive(false);
        GameInput.SetCursorLocked(true);
        if (GameManager.Instance != null)
            GameManager.Instance.UIManager?.SetCrosshairVisible(true);
    }

    private void Update()
    {
        if (!IsOpen)
            return;
        bool closePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                            (GameInput.IsMobile && MobileInputController.Consume("interact"));
        if (closePressed)
            Close();
    }

    private void CreatePanel()
    {
        var hudGo = GameObject.Find("HUD_Canvas");
        var canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("CraftingPanel");
        _panel.transform.SetParent(canvas.transform, false);
        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(sw * 0.7f, sh * 0.78f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        float pw = sw * 0.7f;
        float ph = sh * 0.78f;

        UIHelper.MakeText("TitleText", rt, new Vector2(0f, ph * 0.44f),
            Localization.T("Chế Tạo"), 28, new Color(1f, 0.85f, 0.45f),
            new Vector2(pw - 40f, 40f));

        var listGo = new GameObject("RecipeList");
        listGo.transform.SetParent(rt, false);
        var listRt = listGo.AddComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0.5f, 0.5f);
        listRt.anchorMax = new Vector2(0.5f, 0.5f);
        listRt.pivot = new Vector2(0.5f, 0.5f);
        listRt.anchoredPosition = new Vector2(0f, -ph * 0.02f);
        listRt.sizeDelta = new Vector2(pw - 60f, ph * 0.8f);
        _listParent = listRt;

        UIHelper.MakeButton("CloseBtn", rt, Localization.T("Đóng"),
            new Vector2(0f, -ph * 0.44f), new Vector2(160f, 44f), 22,
            new Color(0.6f, 0.25f, 0.25f), Close);

        RebuildList();
        _panel.SetActive(false);
    }

    private void ClearList()
    {
        if (_listParent == null)
            return;
        for (int i = _listParent.childCount - 1; i >= 0; i--)
            Destroy(_listParent.GetChild(i).gameObject);
    }

    private void RebuildList()
    {
        if (_listParent == null)
            return;
        ClearList();

        var recipes = Recipes.FindAll(r => r.Category == _activeCategory);
        float rowH = 56f;
        float startY = (recipes.Count - 1) * rowH * 0.5f;

        for (int i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            int index = i;
            float y = startY - i * rowH;

            string name = Localization.ItemName(recipe.ResultType);
            string cost = RecipeCostString(recipe);

            UIHelper.MakeText("RecipeName" + index, _listParent,
                new Vector2(-(_listParent.sizeDelta.x * 0.24f), y), name, 20, Color.white,
                new Vector2(_listParent.sizeDelta.x * 0.45f, 40f));

            var costText = UIHelper.MakeText("RecipeCost" + index, _listParent,
                new Vector2(_listParent.sizeDelta.x * 0.08f, y), cost, 15, new Color(0.8f, 0.8f, 0.8f),
                new Vector2(_listParent.sizeDelta.x * 0.4f, 40f));

            var btn = UIHelper.MakeButton("CraftBtn" + index, _listParent,
                Localization.T("Chế Tạo"), new Vector2(_listParent.sizeDelta.x * 0.36f, y),
                new Vector2(110f, 40f), 18, new Color(0.3f, 0.5f, 0.32f), () => Craft(recipe, index));

            UpdateCraftButtonState(btn, costText, recipe);
        }
    }

    private string RecipeCostString(Recipe recipe)
    {
        var parts = new List<string>();
        foreach (var ing in recipe.Ingredients)
        {
            int have = ToolManager.Instance != null ? ToolManager.Instance.CountItem(ing.ItemType) : 0;
            string iname = Localization.ItemName(ing.ItemType);
            parts.Add(have + "/" + ing.Count + " " + iname);
        }
        return string.Join("\n", parts.ToArray());
    }

    private void UpdateCraftButtonState(Button btn, TMP_Text costText, Recipe recipe)
    {
        if (btn == null)
            return;
        bool can = ToolManager.Instance != null && HasIngredients(recipe);
        var colors = btn.colors;
        colors.normalColor = can ? new Color(0.3f, 0.5f, 0.32f) : new Color(0.35f, 0.35f, 0.35f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f);
        btn.colors = colors;
        btn.interactable = can;
    }

    private bool HasIngredients(Recipe recipe)
    {
        if (ToolManager.Instance == null)
            return false;
        foreach (var ing in recipe.Ingredients)
        {
            if (ToolManager.Instance.CountItem(ing.ItemType) < ing.Count)
                return false;
        }
        return true;
    }

    private void Craft(Recipe recipe, int index)
    {
        if (ToolManager.Instance == null)
            return;
        if (!HasIngredients(recipe))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Thiếu nguyên liệu."), 1.5f);
            return;
        }
        if (!ToolManager.Instance.CanHoldItem(recipe.ResultType))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
            return;
        }

        foreach (var ing in recipe.Ingredients)
            ToolManager.Instance.RemoveItemAmount(ing.ItemType, ing.Count);

        ToolManager.Instance.AddItem(recipe.ResultType, recipe.ResultCount);
        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.T("Đã chế tạo: ") + Localization.ItemName(recipe.ResultType), 1.5f);

        RebuildList();
    }

    public void ClosePanelIfOpen()
    {
        if (IsOpen)
            Close();
    }
}
