using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Phase 8 (Task 8.3): NPC dialogue system (simplified). A modal dialogue panel that plays a
/// data-driven instruction queue (<see cref="NpcDefinition.GreetingLines"/>) one line at a time
/// with Advance/Close, then hands off to the NPC's role interaction (vendor/quest/follower).
/// Composes <see cref="NpcController"/> without rewriting it.
/// </summary>
public sealed class NpcDialogueUI : MonoBehaviour
{
    public bool HideOnComplete = false;
    public bool AutoAdvanceAfterLines = true;

    private Canvas _canvas;
    private TMP_Text _nameText;
    private TMP_Text _lineText;
    private string[] _lines;
    private int _index;
    private NpcController _npc;

    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf && _npc != null;

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("NpcDialogueCanvas");

        var panel = HudCanvas.CreateBackdrop(_canvas.transform, "Panel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -Screen.height * 0.16f),
            new Vector2(Mathf.Min(Screen.width * 0.7f, 760f), 150f));

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(panel, false);
        var nr = nameGo.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0.5f, 1f);
        nr.anchorMax = new Vector2(0.5f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.anchoredPosition = new Vector2(0f, -6f);
        nr.sizeDelta = new Vector2(640f, 26f);
        _nameText = nameGo.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(_nameText);
        _nameText.fontSize = 17;
        _nameText.color = new Color(1f, 0.9f, 0.6f);

        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(panel, false);
        var lr = lineGo.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.5f, 0.5f);
        lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.anchoredPosition = Vector2.zero;
        lr.sizeDelta = new Vector2(720f, 80f);
        _lineText = lineGo.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(_lineText);
        _lineText.fontSize = Mathf.Max(15f, Screen.height / 36f);
        _lineText.color = Color.white;
        _lineText.alignment = TextAlignmentOptions.Center;

        // Continue hint.
        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(panel, false);
        var hr = hintGo.AddComponent<RectTransform>();
        hr.anchorMin = new Vector2(0.5f, 0f);
        hr.anchorMax = new Vector2(0.5f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.anchoredPosition = new Vector2(0f, 6f);
        hr.sizeDelta = new Vector2(400f, 20f);
        var hint = hintGo.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(hint);
        hint.fontSize = 13;
        hint.color = new Color(0.8f, 0.8f, 0.8f);
        hint.alignment = TextAlignmentOptions.Center;
        hint.text = "[ E ] " + Localization.T("Tiếp Tục");

        _canvas.gameObject.SetActive(false);
    }

    /// <summary>Open a dialogue session with an NPC's definition.</summary>
    public void Open(NpcController npc)
    {
        _npc = npc;
        if (npc == null || npc.Definition == null)
        {
            Close();
            return;
        }
        var def = npc.Definition;
        _nameText.text = def.DisplayName != null ? def.DisplayName : def.Id;
        _lines = def.GreetingLines != null && def.GreetingLines.Length > 0
            ? def.GreetingLines
            : new[] { "..." };
        _index = 0;
        _canvas.gameObject.SetActive(true);
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (_lineText != null)
            _lineText.text = _index < _lines.Length ? _lines[_index] : "";
    }

    /// <summary>Advance to the next line; after the end, close (and optionally continue the action).</summary>
    public void Advance()
    {
        if (!IsOpen) return;
        // Reflect the label as a localized action where possible.
        _index++;
        if (_index < _lines.Length)
        {
            ShowCurrent();
            return;
        }
        NpcController done = _npc;
        Close();
        if (AutoAdvanceAfterLines && done != null)
        {
            switch (done.Definition != null ? done.Definition.Role : NpcRoleKind.Vendor)
            {
                case NpcRoleKind.Vendor:
                    done.Interact(); // opens the shop via NpcController
                    break;
                default:
                    done.Interact();
                    break;
            }
        }
    }

    public void Close()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
        _npc = null;
        _lines = null;
        _index = 0;
    }
}