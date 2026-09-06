using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.1): skill bar that shows one slot per skill bound through
/// <see cref="SkillBindings"/> (Phase 10). Each slot renders the skill display name plus the key
/// it is bound to (e.g. "G"). The bar is dynamic: a slot is added for every newly bound skill (no
/// fixed 4-slot layout) so players can bind as many skills as they like. Shown only in fighting
/// mode (driven by <see cref="SetVisible"/> from the combat-mode toggle).
/// </summary>
public sealed class SkillBarHUD : MonoBehaviour
{
    /// <summary>Hard upper bound on rendered slots (keys beyond this stay bound, just not shown).</summary>
    public const int MaxSlots = 12;

    /// <summary>Cap for the hotkey bar layout (slots are centered across this many slot positions).</summary>
    public const int LayoutSlots = 10;

    public bool ShowOnInGame = true;

    private Canvas _canvas;
    private readonly List<GameObject> _slots = new List<GameObject>();
    private readonly List<Image> _fills = new List<Image>();
    private readonly List<TMP_Text> _labels = new List<TMP_Text>();
    private bool _visible = true;

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("SkillBarCanvas");
    }

    private static Color DefaultColor(int slot)
    {
        return new Color(0.35f, 0.35f, 0.4f);
    }

    /// <summary>Legacy API kept for external compatibility; the bar is now binding-driven.</summary>
    public void BindAction(int slot, string actionName)
    {
        // No-op: the bar renders skill bindings straight from SkillBindings.
    }

    /// <summary>Force the whole skill bar visible/hidden (e.g. fighting mode).</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_canvas != null)
            _canvas.gameObject.SetActive(visible);
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool inGame = gm != null && gm.InGame;
        bool fighting = false;
        if (gm != null && gm.Player != null)
        {
            var pc = gm.Player.GetComponent<PlayerController>();
            fighting = pc != null && pc.FightingMode;
        }
        bool wantCanvas = inGame && fighting && _visible;
        if (_canvas == null)
            return;
        if (_canvas.gameObject.activeSelf != wantCanvas)
            _canvas.gameObject.SetActive(wantCanvas);
        if (!wantCanvas)
            return;

        var player = gm != null ? gm.Player : null;
        var bindings = player != null ? player.GetComponent<SkillBindings>() : null;

        var entries = new List<KeyValuePair<Key, string>>();
        if (bindings != null) entries.AddRange(bindings.Bindings);
        // Sort by key for a stable layout across frames.
        entries.Sort((a, b) => ((int)a.Key).CompareTo((int)b.Key));

        int count = Mathf.Clamp(entries.Count, 1, MaxSlots);
        EnsureSlots(count);

        for (int i = 0; i < _labels.Count; i++)
        {
            if (i < entries.Count)
            {
                var kv = entries[i];
                var skill = SkillCatalog.Find(kv.Value);
                string name = skill != null ? skill.displayName : kv.Value;
                _labels[i].text = name + "\n" + KeyLabel(kv.Key);
                if (_fills[i] != null)
                    _fills[i].fillAmount = 1f;
            }
            else
            {
                _labels[i].text = "";
            }
        }
    }

    /// <summary>Human-readable label for a bound key ("Digit1" -> "1", "G" -> "G").</summary>
    public static string KeyLabel(Key key)
    {
        if (key == Key.None) return "—";
        string s = key.ToString();
        if (s.StartsWith("Digit", System.StringComparison.Ordinal))
            s = s.Substring(5);
        return s;
    }

    private void EnsureSlots(int target)
    {
        while (_slots.Count > target)
        {
            int last = _slots.Count - 1;
            if (_slots[last] != null)
                Destroy(_slots[last]);
            _slots.RemoveAt(last);
            _fills.RemoveAt(last);
            _labels.RemoveAt(last);
        }
        while (_slots.Count < target)
            CreateSlot(_slots.Count);
    }

    private void CreateSlot(int index)
    {
        float w = Mathf.Max(Screen.width, 1f);
        float h = Mathf.Max(Screen.height, 1);
        float slotSize = h * 0.055f;
        float spacing = slotSize * 1.35f;
        float total = (LayoutSlots - 1) * spacing;
        float startX = -total * 0.5f;

        // Sit the skill bar above the restored 10-slot inventory bar (legacy layout:
        // bottom-centre, top edge ≈ 6% screen height + one slot width). Convert that
        // screen-pixel clearance into this overlay's canvas units (UiScale-aware).
        float canvasScale = w / (1280f / MenuPanelBase.UiScale);
        float invTopPx = h * 0.06f + w * 0.065f + 12f;
        float posY = invTopPx / canvasScale + slotSize * 0.5f;

        var slot = HudCanvas.CreateBackdrop(_canvas.transform, "Slot_" + index,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(startX + index * spacing, posY), new Vector2(slotSize, slotSize));
        _slots.Add(slot.gameObject);
        _fills.Add(HudCanvas.CreateBar(slot, "Progress", Vector2.zero, Vector2.one,
            new Vector2(0f, 0f), Vector2.zero, new Vector2(slotSize, slotSize),
            new Color(0f, 0f, 0f, 0.6f), DefaultColor(index)));

        var label = new GameObject("Label");
        label.transform.SetParent(slot, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Max(12f, h / 90f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        _labels.Add(tmp);
    }
}