using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CountryLife.Helpers;

/// <summary>
/// A world crafting station (planning Task 6.3). Placed in towns and player homes; opened on
/// interaction to craft weapon / armor / potion / food recipes. Recipes marked
/// <c>RequiresDiscovery</c> are hidden until <see cref="RecipeDiscovery"/> unlocks them via a
/// skill book (§5.3). Consumes <see cref="ToolManager"/> ingredients and grants the result to
/// the tool inventory, flowing through the shared item economy.
/// </summary>
public class CraftingStation : MonoBehaviour
{
    [Header("Station")]
    public string StationId = "crafting_station";
    public RecipeKind Kind = RecipeKind.Armor;
    [Tooltip("Result-inventory id (used to resolve where crafted goods land).")]
    public string CategoryName = "Crafting";

    private GameObject _panel;
    private RectTransform _listParent;
    private bool _open;

    public static bool AnyOpen { get; private set; }

    private void Awake()
    {
        var rd = RecipeDiscovery.LoadOrCreate();
        if (rd == null) return;
    }

    private void OnDestroy()
    {
        if (_open) AnyOpen = false;
    }

    /// <summary>Called by player interaction; toggles the station panel.</summary>
    public void Interact()
    {
        if (_open) { Close(); return; }
        Open();
    }

    private void Open()
    {
        _open = true;
        AnyOpen = true;
        RecipeDiscovery.LoadOrCreate();
        GameInput.SetCursorLocked(false);
        if (GameManager.Instance != null)
            GameManager.Instance.UIManager?.SetCrosshairVisible(false);

        var hudGo = GameObject.Find("HUD_Canvas");
        var canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) { Close(); return; }

        if (_panel == null)
            CreatePanel(canvas);
        else
            RebuildList();
        _panel.SetActive(true);
    }

    public void Close()
    {
        _open = false;
        AnyOpen = false;
        if (_panel != null) _panel.SetActive(false);
        GameInput.SetCursorLocked(true);
        if (GameManager.Instance != null)
            GameManager.Instance.UIManager?.SetCrosshairVisible(true);
    }

    private void Update()
    {
        if (!_open) return;
        if ((Keyboard0Esc || MobileUseInteract()))
            Close();
    }

    private bool Keyboard0Esc
    {
        get
        {
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
    }

    private static bool MobileUseInteract()
    {
        return GameInput.IsMobile && MobileInputController.Consume("interact");
    }

    private void CreatePanel(Canvas canvas)
    {
        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("CraftingStation_" + StringKey(StationId));
        _panel.transform.SetParent(canvas.transform, false);
        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(sw * 0.62f, sh * 0.7f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        float pw = sw * 0.62f;
        float ph = sh * 0.7f;

        UIHelper.MakeText("TitleText", rt, new Vector2(0f, ph * 0.44f),
            CategoryName, 26, new Color(1f, 0.85f, 0.45f), new Vector2(pw - 40f, 40f));

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
            new Vector2(0f, -ph * 0.44f), new Vector2(150f, 44f), 22,
            new Color(0.6f, 0.25f, 0.25f), Close);

        RebuildList();
        _panel.SetActive(false);
    }

    private void RebuildList()
    {
        if (_listParent == null) return;
        for (int i = _listParent.childCount - 1; i >= 0; i--)
            Destroy(_listParent.GetChild(i).gameObject);

        var recipes = RecipeRegistry.OfKind(Kind);
        recipes.RemoveAll(r => r.RequiresDiscovery &&
            (RecipeDiscovery.Instance == null || !RecipeDiscovery.Instance.IsDiscovered(r.Id)));

        float rowH = 56f;
        float startY = (recipes.Count - 1) * rowH * 0.5f;

        for (int i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            int index = i;
            float y = startY - i * rowH;

            string name = recipe.DisplayName;
            UIHelper.MakeText("RecipeName" + index, _listParent,
                new Vector2(-(_listParent.sizeDelta.x * 0.24f), y), name, 20, Color.white,
                new Vector2(_listParent.sizeDelta.x * 0.42f, 40f));

            var costText = UIHelper.MakeText("RecipeCost" + index, _listParent,
                new Vector2(_listParent.sizeDelta.x * 0.08f, y), CostString(recipe), 15,
                new Color(0.8f, 0.8f, 0.8f), new Vector2(_listParent.sizeDelta.x * 0.36f, 40f));

            var btn = UIHelper.MakeButton("CraftBtn" + index, _listParent,
                Localization.T("Chế Tạo"), new Vector2(_listParent.sizeDelta.x * 0.36f, y),
                new Vector2(110f, 40f), 18, new Color(0.3f, 0.5f, 0.32f), () => Craft(recipe));

            UpdateButtonState(btn, costText, recipe);
        }
    }

    private string CostString(RecipeData recipe)
    {
        if (recipe.Ingredients == null) return "";
        var parts = new List<string>();
        foreach (var ing in recipe.Ingredients)
        {
            int have = ToolManager.Instance != null ? ToolManager.Instance.CountItem(ing.ItemId) : 0;
            var item = ItemDatabase.Get(ing.ItemId);
            string iname = item != null ? item.displayName : ing.ItemId;
            parts.Add(have + "/" + ing.Count + " " + iname);
        }
        return string.Join("\n", parts.ToArray());
    }

    private void UpdateButtonState(Button btn, TMP_Text costText, RecipeData recipe)
    {
        if (btn == null) return;
        bool can = HasIngredients(recipe);
        var colors = btn.colors;
        colors.normalColor = can ? new Color(0.3f, 0.5f, 0.32f) : new Color(0.35f, 0.35f, 0.35f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f);
        btn.colors = colors;
        btn.interactable = can;
    }

    private bool HasIngredients(RecipeData recipe)
    {
        if (recipe.Ingredients == null || ToolManager.Instance == null) return false;
        foreach (var ing in recipe.Ingredients)
            if (ToolManager.Instance.CountItem(ing.ItemId) < ing.Count) return false;
        return true;
    }

    private void Craft(RecipeData recipe)
    {
        bool hasIng = HasIngredients(recipe);
        if (ToolManager.Instance == null || !hasIng)
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Thiếu nguyên liệu."), 1.5f);
            return;
        }
        if (!ToolManager.Instance.CanHoldItem(recipe.ResultItemId))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
            return;
        }

        foreach (var ing in recipe.Ingredients)
            ToolManager.Instance.RemoveItemAmount(ing.ItemId, ing.Count);
        ToolManager.Instance.AddItem(recipe.ResultItemId, recipe.ResultCount);

        var item = ItemDatabase.Get(recipe.ResultItemId);
        string label = item != null ? item.displayName : recipe.ResultItemId;
        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.T("Đã chế tạo: ") + label, 1.5f);

        RebuildList();
    }

    private static string StringKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return "0";
        int h = 0;
        foreach (char c in s) h = (h * 31 + c) & 0x7fffffff;
        return h.ToString();
    }
}