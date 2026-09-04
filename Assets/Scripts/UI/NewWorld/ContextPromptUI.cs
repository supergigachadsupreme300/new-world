using UnityEngine;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.3): context-sensitive interaction prompt. A reusable overlay that shows a
/// hint ("Press E to <action>") centered near the bottom. It composes the existing
/// <see cref="UIManager"/> info pipeline when available but stays self-contained so it can be
/// dropped on any interactable. Adapted from the shipped E-key prompt rather than rewriting it.
/// </summary>
public sealed class ContextPromptUI : MonoBehaviour
{
    public string ActionKey = "Press E";

    private Canvas _canvas;
    private TMP_Text _text;
    private string _action;
    private float _visibleTime;

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("ContextPromptCanvas");
        var root = HudCanvas.CreateBackdrop(_canvas.transform, "Prompt",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -Screen.height * 0.28f),
            new Vector2(420f, 42f));
        var label = new GameObject("Label");
        label.transform.SetParent(root, false);
        var rect = label.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var tmp = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.fontSize = Mathf.Max(15f, Screen.height / 40f);
        tmp.color = new Color(1f, 1f, 0.85f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = "";
        _text = tmp;
        _canvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_canvas == null) return;
        bool show = _visibleTime > 0f;
        if (_canvas.gameObject.activeSelf != show)
            _canvas.gameObject.SetActive(show);
        if (show)
            _visibleTime -= Time.deltaTime;
    }

    /// <summary>Set the prompt action and show it until <paramref name="duration"/> elapses.</summary>
    public void ShowPrompt(string action, float duration)
    {
        _action = action;
        _visibleTime = duration;
        if (_text != null)
            _text.text = ActionKey + " " + action;
    }

    /// <summary>Hide the prompt immediately.</summary>
    public void HidePrompt()
    {
        _visibleTime = 0f;
    }

    /// <summary>Return the active action text (for tools/debug).</summary>
    public string CurrentAction => _action;
}