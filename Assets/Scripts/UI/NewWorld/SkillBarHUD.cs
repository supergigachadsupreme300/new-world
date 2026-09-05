using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.1): skill bar (6 slots). Shows the tracked <see cref="SkillManager"/> skills
/// (Farming, Fishing) plus bindable action slots. Each slot renders an icon colour, a binding
/// hint, and for skill tracks the current level and XP fraction. Extensible: game-mode/action
/// skills can be registered via <see cref="BindAction"/> to light up additional slots.
/// </summary>
public sealed class SkillBarHUD : MonoBehaviour
{
    public const int SlotCount = 6;
    public bool ShowOnInGame = true;

    private Canvas _canvas;
    private readonly Image[] _slotImages = new Image[SlotCount];
    private readonly TMP_Text[] _slotTexts = new TMP_Text[SlotCount];
    private readonly Dictionary<int, string> _actions = new Dictionary<int, string>();

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("SkillBarCanvas");
        float h = Mathf.Max(Screen.height, 1);
        float slotSize = h * 0.075f;
        float spacing = slotSize * 1.2f;
        float total = (SlotCount - 1) * spacing;
        float startX = -total * 0.5f;

        for (int i = 0; i < SlotCount; i++)
        {
            var slot = HudCanvas.CreateBackdrop(_canvas.transform, "Slot_" + i,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(startX + i * spacing, slotSize), new Vector2(slotSize, slotSize));
            _slotImages[i] = HudCanvas.CreateBar(slot, "Progress", Vector2.zero, Vector2.one,
                new Vector2(0f, 0f), Vector2.zero, new Vector2(slotSize, slotSize),
                new Color(0f, 0f, 0f, 0.6f), DefaultColor(i));

            var label = new GameObject("Label");
            label.transform.SetParent(slot, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = Mathf.Max(14f, h / 64f);
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            _slotTexts[i] = tmp;
        }
    }

    private static Color DefaultColor(int slot)
    {
        switch (slot)
        {
            case 0: return new Color(0.85f, 0.62f, 0.28f); // Farming
            case 1: return new Color(0.3f, 0.7f, 0.9f);    // Fishing
            default: return new Color(0.35f, 0.35f, 0.4f);
        }
    }

    /// <summary>Register a named action on a slot (0-based).</summary>
    public void BindAction(int slot, string actionName)
    {
        if (slot < 0 || slot >= SlotCount || string.IsNullOrEmpty(actionName)) return;
        _actions[slot] = actionName;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool inGame = gm != null && gm.InGame;
        if (_canvas != null)
            _canvas.gameObject.SetActive(ShowOnInGame ? inGame : true);
        if (!inGame) return;

        var sm = SkillManager.Instance;

        // Slot 0: Farming — show level + XP fraction.
        if (sm != null)
        {
            SetSlot(0, "Farming", "G", sm.Level(SkillManager.Track.Farming), sm.XPNormalized(SkillManager.Track.Farming));
            SetSlot(1, "Fishing", "C", sm.Level(SkillManager.Track.Fishing), sm.XPNormalized(SkillManager.Track.Fishing));
        }

        // Remaining slots: bound actions or empty.
        for (int i = 2; i < SlotCount; i++)
        {
            if (_actions.TryGetValue(i, out var act))
                SetSlot(i, act, "", 0, 0f);
            else if (_slotTexts[i] != null && string.IsNullOrEmpty(_slotTexts[i].text))
                _slotTexts[i].text = "";
        }
    }

    private void SetSlot(int slot, string name, string hint, int level, float frac)
    {
        var img = _slotImages[slot];
        var txt = _slotTexts[slot];
        if (img != null)
            img.fillAmount = Mathf.Clamp01(frac);
        if (txt != null)
        {
            string line = (level > 0 ? name + " " + level : name);
            txt.text = string.IsNullOrEmpty(hint) ? line : line + "\n" + hint;
        }
    }
}