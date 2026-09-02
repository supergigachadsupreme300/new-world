using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class SleepManager : MonoSingleton<SleepManager>
{
public static bool IsSleeping { get; private set; }

    private bool _initialized;
    private bool _wasPausedBeforeOpen;
    private Canvas _canvas;
    private UIManager _uiManager;
    private GameObject _panel;

    private GameObject _sleepPrompt;
    private TMP_Text _sleepPromptText;

    private bool _sleeping;
    private Vector3 _savedPosition;
    private Quaternion _savedRotation;
    private float _savedTimeSpeed;
    private float _sleepTargetHours;
    private float _sleepAccumulated;
    private float _nextRestoreHour;
    private PlayerController _player;

    private const float SleepSpeedMultiplier = 100f;
    private const float StaminaPerHour = 150f;
    private const float HpPerHour = 12f;
    private const float RestoreCheckInterval = 0.25f;
    private const float MorningHour = 6f;
    private const float MaxSleepHours = 12f;

    void Start()
    {
        if (!_initialized)
            Initialize();
    }

    void Update()
    {
        if (_sleeping)
        {
            HandleSleepUpdate();
            return;
        }

        if (_panel != null && _panel.activeSelf && Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.xKey.wasPressedThisFrame)
                Close();
        }
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
        float panelW = Mathf.Min(sw * 0.4f, 380f);
        float panelH = Mathf.Min(sh * 0.3f, 200f);
        float fontS = Mathf.Max(14f, sh / 40f);
        float btnH = sh * 0.055f;

        _panel = new GameObject("SleepPanel");
        _panel.transform.SetParent(_canvas.transform, false);
        var rect = _panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(panelW, panelH);
        var img = _panel.AddComponent<Image>();
        img.color = new Color(0.18f, 0.2f, 0.27f, 0.95f);
        img.raycastTarget = false;

        MakeText("SleepTitle", _panel.transform, Localization.T("Giấc Ngủ"),
            new Vector2(0f, panelH * 0.28f), new Vector2(panelW - 40f, fontS * 1.8f),
            (int)(fontS * 1.4f), TextAlignmentOptions.Center);

        MakeText("SleepHint", _panel.transform, Localization.T("Bạn có muốn ngủ để qua thời gian?"),
            new Vector2(0f, panelH * 0.08f), new Vector2(panelW - 40f, fontS * 1.2f),
            (int)fontS, TextAlignmentOptions.Center);

        MakeButton("SleepConfirm", _panel.transform, Localization.T("Ngủ"),
            new Vector2(-panelW * 0.14f, -panelH * 0.28f), new Vector2(panelW * 0.26f, btnH),
            (int)fontS, new Color(0.3f, 0.6f, 0.42f), OnSleepConfirm);

        MakeButton("SleepCancel", _panel.transform, Localization.T("Hủy"),
            new Vector2(panelW * 0.14f, -panelH * 0.28f), new Vector2(panelW * 0.26f, btnH),
            (int)fontS, new Color(0.75f, 0.38f, 0.41f), Close);

        _panel.SetActive(false);
        CreateSleepPrompt();
    }
    private void CreateSleepPrompt()
    {
        _sleepPrompt = new GameObject("SleepWakePrompt");
        _sleepPrompt.transform.SetParent(_canvas.transform, false);
        var rt = _sleepPrompt.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, Screen.height * 0.08f);
        rt.sizeDelta = new Vector2(400f, 40f);

        var bg = _sleepPrompt.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        bg.raycastTarget = false;

        _sleepPromptText = MakeText("SleepWakeText", _sleepPrompt.transform,
            "", new Vector2(0f, 0f), new Vector2(380f, 36f),
            Mathf.Max(14, Screen.height / 45), TextAlignmentOptions.Center);
        _sleepPromptText.transform.SetParent(_sleepPrompt.transform, false);

        _sleepPrompt.SetActive(false);
    }
    public void Open()
    {
        if (!_initialized)
            Initialize();
        if (_panel == null)
            return;

        _wasPausedBeforeOpen = GameManager.Instance != null && GameManager.Instance.GamePaused;
        _panel.SetActive(true);
        RefreshLabels();

        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(true);
        if (_uiManager != null)
            _uiManager.ShowPauseMenu(false);
    }
    public void Close()
    {
        if (_panel != null)
            _panel.SetActive(false);

        if (_wasPausedBeforeOpen)
        {
            if (_uiManager != null)
                _uiManager.ShowPauseMenu(true);
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(false);
    }
    private void RefreshLabels()
    {
        if (_panel == null)
            return;
        SetChildText("SleepTitle", Localization.T("Giấc Ngủ"));
        SetChildText("SleepHint", Localization.T("Bạn có muốn ngủ để qua thời gian?"));
        SetChildText("SleepConfirm", Localization.T("Ngủ"));
        SetChildText("SleepCancel", Localization.T("Hủy"));
    }
    private void SetChildText(string name, string text)
    {
        var child = _panel.transform.Find(name);
        if (child == null)
            return;
        var txt = child.GetComponentInChildren<TMP_Text>();
        if (txt != null)
            txt.text = text;
    }
    private void OnSleepConfirm()
    {
        float currentHour = GameManager.Instance != null ? GameManager.Instance.TimeOfDay : 8f;

        if (currentHour < MorningHour)
            _sleepTargetHours = MorningHour - currentHour;
        else
            _sleepTargetHours = (24f - currentHour) + MorningHour;
        _sleepTargetHours = Mathf.Min(_sleepTargetHours, MaxSleepHours);

        _sleepAccumulated = 0f;
        _nextRestoreHour = RestoreCheckInterval;

        _player = GameManager.Instance?.Player;
        if (_player == null)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.SetTimeOfDay(MorningHour);
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Bạn đã ngủ qua đêm."), 2f);
            Close();
            return;
        }

        _savedPosition = _player.transform.position;
        _savedRotation = _player.transform.rotation;
        _savedTimeSpeed = GameManager.Instance.TimeSpeed;

        if (_panel != null)
            _panel.SetActive(false);
        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause(false);

        Transform bed = FindBedTransform();
        Vector3 bedPos = bed != null ? bed.position : _savedPosition;

        _player.transform.position = bedPos + Vector3.up * 0.35f;
        _player.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _player.EnableInput(false);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.SetParent(null);
            cam.transform.position = _player.transform.position + new Vector3(0f, 2.2f, -1.8f);
            cam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
        }

        GameManager.Instance.TimeSpeed = _savedTimeSpeed * SleepSpeedMultiplier;

        _sleeping = true;
        IsSleeping = true;

        if (_sleepPrompt != null)
        {
            _sleepPromptText.text = GameInput.IsMobile
                ? Localization.T("Chạm màn hình để dậy")
                : Localization.T("Nhấn Shift để dậy");
            _sleepPrompt.SetActive(true);
        }
    }
    private void HandleSleepUpdate()
    {
        if (GameManager.Instance == null)
            return;

        float deltaHours = GameManager.Instance.TimeSpeed * Time.deltaTime;
        _sleepAccumulated += deltaHours;

        while (_nextRestoreHour <= _sleepAccumulated && _nextRestoreHour <= _sleepTargetHours)
        {
            RestoreStats(RestoreCheckInterval);
            _nextRestoreHour += RestoreCheckInterval;
        }

        if (_sleepAccumulated >= _sleepTargetHours)
        {
            WakeUp();
            return;
        }

        bool shiftPressed = Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame;
        bool mobileTap = GameInput.IsMobile && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        if (shiftPressed || mobileTap)
            WakeUp();
    }
    private void RestoreStats(float hours)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        player.Stamina = Mathf.Min(player.MaxStamina, player.Stamina + hours * StaminaPerHour);
        player.HP = Mathf.Min(player.MaxHP, player.HP + Mathf.RoundToInt(hours * HpPerHour));
        GameManager.Instance?.UIManager?.UpdatePlayerHud(player.HP, player.MaxHP, player.Stamina, player.MaxStamina, player.Money);
    }
    private void WakeUp()
    {
        int roundedHours = Mathf.Max(1, Mathf.RoundToInt(_sleepAccumulated));

        if (_player != null)
        {
            _player.transform.position = _savedPosition;
            _player.transform.rotation = _savedRotation;
            _player.EnableInput(true);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetParent(_player.transform);
                cam.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                cam.transform.localRotation = Quaternion.identity;
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TimeSpeed = _savedTimeSpeed;
            GameManager.Instance.SetTimeOfDay(GameManager.Instance.TimeOfDay);
        }

        _sleeping = false;
        IsSleeping = false;

        if (_sleepPrompt != null)
            _sleepPrompt.SetActive(false);

        GameManager.Instance?.UIManager?.ShowMessage(Localization.F("Bạn đã ngủ {0} tiếng.", roundedHours), 3f);
        Close();
    }
    private Transform FindBedTransform()
    {
        var bedGO = GameObject.Find("Bed");
        return bedGO != null ? bedGO.transform : null;
    }
    private TMP_Text MakeText(string name, Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions align)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, pos, text, fontSize, Color.white, size, false, false, align, false);
    private Button MakeButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, Color color, UnityEngine.Events.UnityAction callback)
        => CountryLife.Helpers.UIHelper.MakeButton(name, parent, label, pos, size, fontSize, color, callback);
}
