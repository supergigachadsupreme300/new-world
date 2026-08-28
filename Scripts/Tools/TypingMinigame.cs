using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class TypingMinigame : MonoSingleton<TypingMinigame>
{
private GameObject _panel;
    private TMP_Text _paragraphText;
    private TMP_Text _inputText;
    private TMP_Text _timerText;
    private Image _progressFill;
    private bool _isActive;
    private string _currentParagraph;
    private string _playerInput;
    private float _timeRemaining;
    private float _duration = 30f;
    private Canvas _canvas;
    private UIManager _uiManager;

    private static readonly string[] Paragraphs =
    {
        "Nam Mo A Di Da Phat. Chung sinh deu co Phat tinh, chi vi cam xu bon me ma khong the nhan ra.",
        "Tu bi la suc manh lon nhat. Hay doi xu voi moi nguoi voi long tu thuong va su chiu dung.",
        "Con duong giac ngo bat dau tu moi buoc chan. Moi hanh dong thien lanh deu la buoc tien tren con do.",
        "Phat day: khong san hana, khong tham lam, khong si me. Tam yen binh la niem hanh phuc that su.",
        "Huong phap thich cau nguoi, giup do chung sinh. Do la con duong cua nguoi hieu biet.",
        "Thoi gian quy bau, hay su dung thoi gian de tu tap va lam dieu tot dep cho doi song.",
        "Moi su vua xay dung deu co nguyen nhan. Hay chap nhan voi tam thien nhu de thanh tuu.",
        "Cuoi doi la vui, thien nguyen la phuc. Hay song voi long biet on va hy vong moi ngay."
    };

    private static readonly Key[] TypingKeys =
    {
        Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
        Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
        Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
        Key.Space, Key.Period, Key.Comma, Key.Semicolon, Key.Quote,
        Key.Minus, Key.Equals, Key.Slash
    };

    public bool IsOpen => _isActive;

    public void Open(float duration = 30f)
    {
        if (_isActive) return;
        _duration = duration;
        var gm = GameManager.Instance;
        _uiManager = GameManager.Instance?.UIManager;
        if (gm == null) return;
        _canvas = gm.UIManager?.GetCanvas();
        if (_canvas == null) return;

        _isActive = true;
        _playerInput = "";
        _currentParagraph = Paragraphs[Random.Range(0, Paragraphs.Length)];
        _timeRemaining = _duration;
        gm.TogglePause(true);
        gm.UIManager?.ShowPauseMenu(false);
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
        }
        BuildUI();
        _panel.SetActive(true);
        UpdateDisplay();
    }
    public void Close()
    {
        if (!_isActive) return;
        _isActive = false;
        if (_panel != null) _panel.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
        var gm = GameManager.Instance;
        if (gm != null && gm.GamePaused)
            gm.TogglePause(false);
    }

    void Update()
    {
        if (!_isActive) return;
        _timeRemaining -= Time.unscaledDeltaTime;
        if (_timerText != null)
            _timerText.text = Mathf.CeilToInt(Mathf.Max(0f, _timeRemaining)).ToString();
        if (_timeRemaining <= 0f)
        {
            FinishMinigame();
            return;
        }
        HandleTypingInput();
    }
    private void HandleTypingInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.backspaceKey.wasPressedThisFrame)
        {
            if (_playerInput.Length > 0)
                _playerInput = _playerInput.Substring(0, _playerInput.Length - 1);
            UpdateDisplay();
            return;
        }

        if (kb.enterKey.wasPressedThisFrame)
        {
            FinishMinigame();
            return;
        }

        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        for (int i = 0; i < TypingKeys.Length; i++)
        {
            if (kb[TypingKeys[i]].wasPressedThisFrame)
            {
                string ch = KeyToChar(TypingKeys[i], shift);
                if (ch.Length > 0 && _playerInput.Length < _currentParagraph.Length)
                {
                    _playerInput += ch;
                    UpdateDisplay();
                }
                return;
            }
        }
    }
    private string KeyToChar(Key key, bool shift)
    {
        switch (key)
        {
            case Key.Space: return " ";
            case Key.Period: return shift ? ">" : ".";
            case Key.Comma: return shift ? "<" : ",";
            case Key.Semicolon: return shift ? ":" : ";";
            case Key.Quote: return shift ? "\"" : "'";
            case Key.Minus: return shift ? "_" : "-";
            case Key.Equals: return shift ? "+" : "=";
            case Key.Slash: return shift ? "?" : "/";
            default:
                string letter = key.ToString().Replace("Key", "");
                if (letter.Length == 1)
                {
                    char c = letter[0];
                    if (!shift) c = char.ToLower(c);
                    return c.ToString();
                }
                return "";
        }
    }
    private void UpdateDisplay()
    {
        if (_inputText == null || _paragraphText == null) return;
        _paragraphText.text = _currentParagraph;

        string inputDisplay = "";
        for (int i = 0; i < _playerInput.Length; i++)
        {
            bool correct = i < _currentParagraph.Length && _playerInput[i] == _currentParagraph[i];
            string col = correct ? "#44FF44" : "#FF4444";
            inputDisplay += "<color=" + col + ">" + _playerInput[i] + "</color>";
        }
        _inputText.text = inputDisplay;

        float progress = _currentParagraph.Length > 0 ? (float)_playerInput.Length / _currentParagraph.Length : 0f;
        if (_progressFill != null)
            _progressFill.fillAmount = Mathf.Clamp01(progress);
    }
    private void FinishMinigame()
    {
        int correctCount = 0;
        for (int i = 0; i < _playerInput.Length && i < _currentParagraph.Length; i++)
            if (_playerInput[i] == _currentParagraph[i]) correctCount++;

        float accuracy = _currentParagraph.Length > 0 ? (float)correctCount / _currentParagraph.Length : 0f;

        if (accuracy >= 0.8f)
        {
            KarmaManager.Instance?.AddMaxKarma(1f);
            _uiManager?.ShowMessage(Localization.T("Thanh tinh! +1 Max Karma"), 3f);
        }
        else
        {
            _uiManager?.ShowMessage(Localization.T("Hay thu lai lan sau!"), 2f);
        }
        Close();
    }
    private void BuildUI()
    {
        if (_panel != null) return;
        float sw = Screen.width;
        float sh = Screen.height;
        float panelW = Mathf.Min(sw * 0.7f, 700f);
        float panelH = Mathf.Min(sh * 0.7f, 500f);
        float fontS = Mathf.Max(16f, sh / 35f);

        _panel = new GameObject("TypingMinigamePanel");
        _panel.transform.SetParent(_canvas.transform, false);
        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelW, panelH);
        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.1f, 0.18f, 0.95f);
        panelImg.raycastTarget = true;

        MakeText("Title", _panel.transform, Localization.T("Thien Tung"),
            new Vector2(0f, panelH * 0.38f), new Vector2(panelW - 40f, fontS * 1.5f),
            (int)(fontS * 1.3f), TextAlignmentOptions.Center, Color.white);

        _timerText = MakeText("Timer", _panel.transform, "30",
            new Vector2(panelW * 0.4f, panelH * 0.38f), new Vector2(60f, fontS * 1.5f),
            (int)(fontS * 1.2f), TextAlignmentOptions.Center, new Color(1f, 0.84f, 0f));

        var progressBg = new GameObject("ProgressBg");
        progressBg.transform.SetParent(_panel.transform, false);
        var pbgRect = progressBg.AddComponent<RectTransform>();
        pbgRect.anchorMin = new Vector2(0.5f, 0.5f);
        pbgRect.anchorMax = new Vector2(0.5f, 0.5f);
        pbgRect.anchoredPosition = new Vector2(0f, panelH * 0.28f);
        pbgRect.sizeDelta = new Vector2(panelW - 60f, 14f);
        var pbgImg = progressBg.AddComponent<Image>();
        pbgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        pbgImg.raycastTarget = false;

        var fillGo = new GameObject("ProgressFill");
        fillGo.transform.SetParent(progressBg.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        _progressFill = fillGo.AddComponent<Image>();
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillAmount = 0f;
        _progressFill.color = new Color(0.9f, 0.75f, 0.1f);
        _progressFill.raycastTarget = false;

        MakeText("Instruction", _panel.transform, Localization.T("Go doan van ben duoi..."),
            new Vector2(0f, panelH * 0.16f), new Vector2(panelW - 40f, fontS * 1.2f),
            (int)(fontS * 0.8f), TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f));

        _paragraphText = MakeText("Paragraph", _panel.transform, "",
            new Vector2(0f, panelH * 0.04f), new Vector2(panelW - 40f, fontS * 2f),
            (int)fontS, TextAlignmentOptions.Center, new Color(0.6f, 0.6f, 0.6f));
        if (_paragraphText != null)
            _paragraphText.textWrappingMode = TextWrappingModes.Normal;

        _inputText = MakeText("Input", _panel.transform, "",
            new Vector2(0f, -panelH * 0.12f), new Vector2(panelW - 40f, fontS * 2f),
            (int)fontS, TextAlignmentOptions.Center, Color.white);
        if (_inputText != null)
            _inputText.textWrappingMode = TextWrappingModes.Normal;

        MakeText("Hint", _panel.transform, Localization.T("ESC de dung | Backspace de xoa"),
            new Vector2(0f, -panelH * 0.35f), new Vector2(panelW - 40f, fontS),
            (int)(fontS * 0.7f), TextAlignmentOptions.Center, new Color(0.5f, 0.5f, 0.5f));

        _panel.SetActive(false);
    }
    private TMP_Text MakeText(string name, Transform parent, string text, Vector2 pos, Vector2 size,
        int fontSize, TextAlignmentOptions align, Color color)
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
        var font = GameManager.Instance?.UIManager?.defaultTmpFont;
        if (font == null) font = Resources.Load<TMP_FontAsset>("VietPixel");
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }
}
