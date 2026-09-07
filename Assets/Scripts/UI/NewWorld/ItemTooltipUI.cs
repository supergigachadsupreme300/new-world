using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A cursor-following item info tooltip (hover feedback). Self-contained: builds its own
/// screen-space overlay canvas (see <see cref="HudCanvas.CreateOverlay"/>) and populates
/// itself from an item id resolved across the item catalogs:
/// weapons (<see cref="WeaponCatalog"/>), gear (<see cref="GearCatalog"/>), loot
/// (<see cref="ItemDatabase"/>), and a <see cref="Localization.ItemName"/> fallback.
///
/// Designed to be re-used by both world-hover (curve the crosshair at weapon racks / loot
/// drops) and UI-slot hover. Call <see cref="Show(string, Vector2)"/> / <see cref="Hide"/>.
/// </summary>
public sealed class ItemTooltipUI : MonoBehaviour
{
    public const int SortingOrder = 20;

    private static ItemTooltipUI _instance;

    private RectTransform _root;
    private TMP_Text _title;
    private TMP_Text _body;
    private Canvas _canvas;
    private bool _shown;

    /// <summary>Ensure the singleton exists and returns it (builds on first access).</summary>
    public static ItemTooltipUI Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ItemTooltip");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<ItemTooltipUI>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        if (_canvas == null) Build();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Show the tooltip for <paramref name="itemId"/> near a screen position (pixels).</summary>
    public static void Show(string itemId, Vector2 screenPos)
    {
        var self = Instance;
        self.Populate(itemId);
        self.PositionAt(screenPos);
        self._root.gameObject.SetActive(true);
        self._shown = true;
    }

    /// <summary>Hide the tooltip if it is visible.</summary>
    public static void Hide()
    {
        var self = _instance;
        if (self == null || !self._shown) return;
        self._root.gameObject.SetActive(false);
        self._shown = false;
    }

    /// <summary>True if the tooltip is currently visible.</summary>
    public static bool IsShown => _instance != null && _instance._shown;

    private void Build()
    {
        _canvas = HudCanvas.CreateOverlay("ItemTooltipCanvas");
        _canvas.sortingOrder = SortingOrder;

        var rootGo = new GameObject("Tooltip");
        rootGo.transform.SetParent(_canvas.transform, false);
        _root = rootGo.AddComponent<RectTransform>();
        _root.pivot = new Vector2(0f, 1f);
        var img = rootGo.AddComponent<Image>();
        img.color = new Color(0.08f, 0.09f, 0.12f, 0.97f);
        img.raycastTarget = false;

        // Keep the box sized to the text once we set an initial size (ContentSizeFitter
        // would fight the anchored-position following, so we grow manually on populate).
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(rootGo.transform, false);
        var tr = titleGo.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(10f, -28f);
        tr.offsetMax = new Vector2(-10f, -4f);
        _title = titleGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(_title);
        _title.raycastTarget = false;
        _title.fontStyle = FontStyles.Bold;
        _title.fontSize = Mathf.Max(16f, Screen.height / 44f);
        _title.color = new Color(1f, 0.92f, 0.6f, 1f);
        _title.enableWordWrapping = true;

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(rootGo.transform, false);
        var brt = bodyGo.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.pivot = new Vector2(0.5f, 1f);
        brt.offsetMin = new Vector2(10f, 8f);
        brt.offsetMax = new Vector2(-10f, -30f);
        _body = bodyGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(_body);
        _body.raycastTarget = false;
        _body.fontSize = Mathf.Max(13f, Screen.height / 54f);
        _body.color = Color.white;
        _body.enableWordWrapping = true;

        _root.gameObject.SetActive(false);
    }

    private void ApplyFont(TMP_Text t)
    {
        GameManager.Instance?.UIManager?.ApplyDefaultFont(t);
    }

    private void Populate(string itemId)
    {
        var weapon = WeaponCatalog.Find(itemId);
        if (weapon != null) { Fill(weapon); return; }

        var gear = GearCatalog.Find(itemId);
        if (gear != null) { Fill(gear); return; }

        var loot = ItemDatabase.Get(itemId);
        if (loot != null) { Fill(loot); return; }

        // Fallback: any other tool/material id via localization.
        _title.text = Localization.ItemName(itemId);
        _body.text = Localization.T("Material / resource");
        SizeToContent();
    }

    private void Fill(WeaponData w)
    {
        var sb = new StringBuilder();
        _title.text = w.displayName;
        sb.Append('[').Append(Localization.T("Weapon")).Append(" · ")
          .Append(DamageKindLabel(w.Type)).AppendLine("]");
        sb.Append(Localization.F("Damage: {0}", w.BaseDamage.ToString("0")));
        sb.Append(Localization.F("  •  Speed: {0}", w.Speed.ToString("0.##")));
        sb.AppendLine();
        sb.Append(Localization.F("Reach: {0}", w.Reach.ToString("0.##")));
        sb.Append(Localization.F("  •  Weight: {0}", w.Weight.ToString("0.##")));
        if (w.StrengthRequirement > 0f)
            sb.Append(Localization.F("  •  Str: {0}", w.StrengthRequirement.ToString("0")));
        sb.AppendLine();
        sb.Append(Localization.F("Scales: {0}", ScalingLabel(w.ScalingStat)));
        if (w.Skill != null && !string.IsNullOrEmpty(w.Skill.displayName))
            sb.Append(Localization.F("  •  Skill: {0}", w.Skill.displayName));
        _body.text = sb.ToString();
        SizeToContent();
    }

    private void Fill(GearDef g)
    {
        var sb = new StringBuilder();
        _title.text = g.displayName;
        sb.Append('[').Append(GenreLabel(g.Genre)).Append(" · ")
          .Append(EquipmentSystem.SlotLabel(g.Slot)).AppendLine("]");
        sb.Append(Localization.F("Weight: {0}", g.Weight.ToString("0.##")));
        if (g.Defense > 0f)
            sb.Append(Localization.F("  •  Defense: {0}", g.Defense.ToString("0.##")));
        sb.AppendLine();
        var resistLines = new System.Collections.Generic.List<string>();
        for (int i = 0; i < g.Resist.Length; i++)
        {
            if (g.Resist[i] > 0f)
                resistLines.Add(Localization.F("{0} {1}", ((DamageType)i).ToString(), g.Resist[i].ToString("0.#")));
        }
        if (resistLines.Count > 0)
        {
            sb.Append(Localization.F("Resists: {0}", string.Join(", ", resistLines))).AppendLine();
        }
        var statLines = new System.Collections.Generic.List<string>();
        for (int i = 0; i < g.StatBonus.Length; i++)
        {
            if (g.StatBonus[i] != 0f)
                statLines.Add(Localization.F("+{0} {1}", g.StatBonus[i].ToString("0.#"), ((StatType)i).ToString()));
        }
        if (statLines.Count > 0)
            sb.Append(string.Join("  ", statLines));
        if (sb[sb.Length - 1] == '\n')
            sb.Length--;
        _body.text = sb.ToString();
        SizeToContent();
    }

    private void Fill(ItemData it)
    {
        var sb = new StringBuilder();
        _title.text = it.displayName ?? it.id;
        sb.Append('[').Append(ItemTypeLabel(it.Type)).AppendLine("]");
        sb.Append(Localization.F("Value: {0}", it.BaseValue));
        sb.Append(Localization.F("  •  Weight: {0}", it.Weight.ToString("0.##")));
        if (it.IsStackable)
            sb.Append(Localization.F("  •  Stack: {0}", it.MaxStack));
        if (!string.IsNullOrEmpty(it.description))
            sb.AppendLine().Append(it.description);
        _body.text = sb.ToString();
        SizeToContent();
    }

    /// <summary>Grow the box to fit title + body text based on the longest line.</summary>
    private void SizeToContent()
    {
        float maxW = 0f;
        if (_title != null && _title.text != null)
            foreach (var line in _title.text.Split('\n'))
                maxW = Mathf.Max(maxW, line.Length);
        if (_body != null && _body.text != null)
            foreach (var line in _body.text.Split('\n'))
                maxW = Mathf.Max(maxW, line.Length);
        float bodyLines = _body != null && _body.text != null ? _body.text.Split('\n').Length : 1f;
        float titleLines = _title != null && _title.text != null ? _title.text.Split('\n').Length : 1f;

        float w = Mathf.Clamp(maxW * 7.5f + 20f, 180f, 420f);
        float h = 30f + titleLines * 22f + bodyLines * 20f + 30f;
        _root.sizeDelta = new Vector2(w, h) / _canvas.scaleFactor;
    }

    private void PositionAt(Vector2 screenPos)
    {
        Vector2 size = _root.sizeDelta * _canvas.scaleFactor;
        float right = Screen.width, bottom = Screen.height;
        float px = Mathf.Clamp(screenPos.x + 18f, 8f, right - size.x - 8f);
        float py = Mathf.Clamp(screenPos.y - 10f, size.y + 8f, bottom - 8f);

        _root.anchoredPosition = new Vector2(px, py) / _canvas.scaleFactor;
    }

    private static string DamageKindLabel(DamageType t) => Localization.T(t.ToString());
    private static string ItemTypeLabel(ItemType t) => Localization.T(t.ToString());
    private static string GenreLabel(EquipGenre g) => Localization.T(g.ToString());
    private static string ScalingLabel(WeaponScalingStat s) => Localization.T(s.ToString());
}
