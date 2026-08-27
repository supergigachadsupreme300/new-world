using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MobileInputController : MonoSingleton<MobileInputController>
{
    private GameObject _canvasGo;
    private Canvas _canvas;
    private VirtualJoystick _joystick;
    private GameObject _buildGroup;
    private bool _visible;

    private readonly Dictionary<string, bool> _held = new Dictionary<string, bool>();
    private readonly Dictionary<string, int> _pressedFrame = new Dictionary<string, int>();
    private readonly List<string> _pressedFrameKeys = new List<string>();
    private Vector2 _lookDelta;

    public static Vector2 MoveAxis =>
        Instance != null && Instance._joystick != null ? Instance._joystick.Value : Vector2.zero;

    public static bool IsHeld(string action)
    {
        if (Instance == null) return false;
        return Instance._held.TryGetValue(action, out bool value) && value;
    }

    public static bool Consume(string action)
    {
        if (Instance == null) return false;
        return Instance.ConsumeInternal(action);
    }

    public static void SetActionPressed(string action, bool isDown)
    {
        if (Instance == null) return;
        if (isDown)
        {
            Instance._held[action] = true;
            Instance._pressedFrame[action] = Time.frameCount;
        }
        else
        {
            Instance._held[action] = false;
        }
    }

    public static void AddLookDelta(Vector2 delta)
    {
        if (Instance != null)
            Instance._lookDelta += delta;
    }

    public static Vector2 TakeLookDelta()
    {
        if (Instance == null) return Vector2.zero;
        var delta = Instance._lookDelta;
        Instance._lookDelta = Vector2.zero;
        return delta;
    }

    private bool ConsumeInternal(string action)
    {
        if (_pressedFrame.TryGetValue(action, out int frame))
        {
            _pressedFrame.Remove(action);
            return Time.frameCount - frame <= 1;
        }
        return false;
    }

    protected override void Awake()
    {
        base.Awake();
        BuildUI();
        _canvasGo.SetActive(false);
        UpdateVisibility();
    }

    private void Update()
    {
        UpdateVisibility();

        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (!_visible || player == null || player.IgnoreInput)
            _lookDelta = Vector2.zero;

        if (_pressedFrame.Count > 0)
        {
            _pressedFrameKeys.Clear();
            _pressedFrameKeys.AddRange(_pressedFrame.Keys);
            for (int i = _pressedFrameKeys.Count - 1; i >= 0; i--)
            {
                if (Time.frameCount - _pressedFrame[_pressedFrameKeys[i]] > 2)
                    _pressedFrame.Remove(_pressedFrameKeys[i]);
            }
        }

        UpdateBuildButtons();
    }

    private bool IsVisible()
    {
        if (!GameInput.IsMobile) return false;
        var gm = GameManager.Instance;
        if (gm == null || !gm.InGame || gm.GamePaused) return false;
        if (CutsceneManager.Instance != null && CutsceneManager.Instance.IsActive) return false;
        return true;
    }

    private void UpdateVisibility()
    {
        bool shouldShow = IsVisible();
        if (shouldShow == _visible) return;
        _visible = shouldShow;
        if (_canvasGo != null)
            _canvasGo.SetActive(shouldShow);
        if (!shouldShow)
        {
            _held.Clear();
            _pressedFrame.Clear();
            _lookDelta = Vector2.zero;
        }
    }

    private void UpdateBuildButtons()
    {
        if (_buildGroup == null) return;
        bool hammer = ToolManager.Instance != null &&
                      ToolManager.Instance.GetSelectedItemType() == "hammer";
        if (_buildGroup.activeSelf != hammer)
            _buildGroup.SetActive(hammer);
    }

    private void BuildUI()
    {
        _canvasGo = new GameObject("Mobile_Canvas");
        _canvasGo.layer = LayerMask.NameToLayer("UI");
        _canvas = _canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 950;
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();

        float w = Screen.width;
        float h = Screen.height;
        float baseFloor = h * 0.14f;
        int btnFont = Mathf.Max(12, (int)(h * 0.02f));
        int smFont = Mathf.Max(11, (int)(h * 0.016f));
        float sPrimary = Mathf.Min(h * 0.085f, w * 0.15f);
        float sMedium = Mathf.Min(h * 0.07f, w * 0.125f);
        float sSmall = Mathf.Min(h * 0.06f, w * 0.11f);

        CreateLookArea();

        float joySize = Mathf.Min(w * 0.18f, h * 0.15f);
        var joyGo = new GameObject("MoveJoystick");
        joyGo.transform.SetParent(_canvasGo.transform, false);
        var joyRt = joyGo.AddComponent<RectTransform>();
        joyRt.anchorMin = new Vector2(0f, 0f);
        joyRt.anchorMax = new Vector2(0f, 0f);
        joyRt.pivot = new Vector2(0.5f, 0.5f);
        joyRt.anchoredPosition = new Vector2(w * 0.13f, baseFloor + joySize * 0.5f + h * 0.03f);
        joyRt.sizeDelta = new Vector2(joySize, joySize);
        var joyImg = joyGo.AddComponent<Image>();
        joyImg.color = new Color(1f, 1f, 1f, 0.14f);
        var joy = joyGo.AddComponent<VirtualJoystick>();
        joy.radius = joySize * 0.5f;

        var knobGo = new GameObject("Knob");
        knobGo.transform.SetParent(joyGo.transform, false);
        var knobRt = knobGo.AddComponent<RectTransform>();
        knobRt.anchorMin = new Vector2(0.5f, 0.5f);
        knobRt.anchorMax = new Vector2(0.5f, 0.5f);
        knobRt.pivot = new Vector2(0.5f, 0.5f);
        knobRt.sizeDelta = new Vector2(joySize * 0.45f, joySize * 0.45f);
        var knobImg = knobGo.AddComponent<Image>();
        knobImg.color = new Color(1f, 1f, 1f, 0.4f);
        joy.knob = knobRt;
        _joystick = joy;

        CreateActionButton("UseButton", Localization.T("DÙNG"),
            new Vector2(-w * 0.13f, baseFloor + h * 0.105f), new Vector2(sPrimary, sPrimary),
            "use", false, btnFont);
        CreateActionButton("InteractButton", "E",
            new Vector2(-w * 0.13f, baseFloor + h * 0.36f), new Vector2(sMedium, sMedium),
            "interact", false, smFont);
        CreateActionButton("DropButton", "Q",
            new Vector2(-w * 0.035f, baseFloor + h * 0.02f), new Vector2(sSmall, sSmall),
            "drop", false, smFont);
        CreateActionButton("SprintButton", Localization.T("CHẠY"),
            new Vector2(-w * 0.035f, baseFloor + h * 0.19f), new Vector2(sMedium, sMedium),
            "sprint", true, smFont);
        CreateActionButton("JumpButton", Localization.T("NHẢY"),
            new Vector2(-w * 0.035f, baseFloor + h * 0.275f), new Vector2(sMedium, sMedium),
            "jump", false, btnFont);
        CreateActionButton("PauseButton", "II",
            new Vector2(-w * 0.02f, -h * 0.02f), new Vector2(h * 0.05f, h * 0.05f),
            "pause", false, smFont, topRight: true);

        _buildGroup = new GameObject("BuildGroup");
        _buildGroup.transform.SetParent(_canvasGo.transform, false);
        CreateActionButton("BuildButton", Localization.T("XÂY"),
            new Vector2(-w * 0.13f, baseFloor + h * 0.53f), new Vector2(sMedium, sMedium),
            "build", false, btnFont, parent: _buildGroup.transform);
        CreateActionButton("RotateButton", Localization.T("XOAY"),
            new Vector2(-w * 0.035f, baseFloor + h * 0.445f), new Vector2(sSmall, sSmall),
            "rotate", false, smFont, parent: _buildGroup.transform);
        _buildGroup.SetActive(false);
    }

    private void CreateLookArea()
    {
        var lookGo = new GameObject("LookArea");
        lookGo.transform.SetParent(_canvasGo.transform, false);
        var lookRt = lookGo.AddComponent<RectTransform>();
        lookRt.anchorMin = new Vector2(0.5f, 0f);
        lookRt.anchorMax = new Vector2(1f, 1f);
        lookRt.offsetMin = Vector2.zero;
        lookRt.offsetMax = Vector2.zero;
        var lookImg = lookGo.AddComponent<Image>();
        lookImg.color = new Color(0f, 0f, 0f, 0f);
        lookGo.AddComponent<TouchLookArea>();
    }

    private void CreateActionButton(string name, string label, Vector2 pos, Vector2 size,
        string action, bool holdable, int fontSize, Transform parent = null, bool topRight = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : _canvasGo.transform, false);
        var rt = go.AddComponent<RectTransform>();
        if (topRight)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.5f);

        var btn = go.AddComponent<MobileActionButton>();
        btn.action = action;
        btn.holdable = holdable;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var uiManager = Object.FindAnyObjectByType<UIManager>();
        uiManager?.ApplyDefaultFont(tmp);
    }
}
