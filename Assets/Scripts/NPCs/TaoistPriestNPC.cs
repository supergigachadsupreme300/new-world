using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Taoist priest (Batch 20c). Worshipping here means offering 1 wood (incense); the offering is
/// routed through <see cref="ReligionManager.Worship"/> (joins the Taoist faith / converts the
/// player) and grants the daily qi blessing (stamina regen x2 all day, owned by
/// <see cref="ReligionManager.RefreshBlessings"/>).
/// </summary>
public class TaoistPriestNPC : MonoSingleton<TaoistPriestNPC>
{
    private Transform _myTransform;
    private Transform _playerTransform;
    private Quaternion _originalRotation = Quaternion.identity;

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private bool _dialogActive;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    private readonly string[] _lines =
    {
        "Nhân vô vi chi cảnh, thuận theo tự nhiên. Hãy sống hòa ái với lẽ đất trời.",
        "Triu luyện khí công, lành tâm tĩnh túc. Sức khỏe là gốc rễ của mọi việc.",
        "Đạo khí lưu chuyển theo nhịp thở. Nghỉ ngơi đúng lúc, ruộng đồng mới tốt tươi.",
        "Phong thủy thuận hòa thì mùa màng bội thu. Hãy để đất thở.",
        "Lũ quỷ dữ sợ hãi bùa chú đạo gia. Giữ thân vững, tâm an thì tà khí lánh xa.",
        "Dâng một nén hương, Đạo sẽ phù hộ cho con khỏe mạnh cả ngày.",
        "Mây trên trời, nước dưới khe. Muốn sống thọ phải biết tiết chế và điều độ.",
        "Ngưỡng cửa đạo quán rộng mở cho người chân thành."
    };

    private const string _offerLine =
        "Con hãy dâng một khúc gỗ làm nén hương. Ta sẽ ban phước lành tiên khí, sức lực của con sẽ hồi phục nhanh gấp đôi cả ngày hôm nay.";

    private int _lastLine = -1;
    private bool _waitingOffering;

    public bool IsDialogActive => _dialogActive;

    void Start()
    {
        _uiManager = GameManager.Instance?.UIManager;
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
        if (_myTransform != null)
            _originalRotation = _myTransform.rotation;
    }

    private UIManager _uiManager;

    public void Interact()
    {
        if (gameObject == null || !gameObject.activeInHierarchy)
            return;
        InitializeDialog();
        if (_panel == null)
            return;

        var rm = GameManager.Instance?.ReligionManager;
        bool alreadyBlessed = rm != null && rm.HasDailyBlessingToday;

        FriendshipManager.Instance?.GrantTalk("taoist");
        FacePlayer();
        _dialogActive = true;
        _panel.SetActive(true);
        _nameText.text = Localization.T("Đạo Sĩ");
        _dialogQueue.Clear();
        for (int i = 0; i < 3; i++)
        {
            _lastLine = (_lastLine + 1) % _lines.Length;
            _dialogQueue.Enqueue(_lines[_lastLine]);
        }
        if (!alreadyBlessed)
        {
            _dialogQueue.Enqueue(_offerLine);
            _waitingOffering = true;
        }
        else
        {
            _dialogQueue.Enqueue("Con đã nhận phước lành tiên khí hôm nay rồi.");
        }
        Advance();
    }

    public void Advance()
    {
        if (_panel == null)
            return;
        if (_waitingOffering && _dialogText.text == _offerLine)
        {
            _waitingOffering = false;
            PerformOffering();
            _dialogText.text = _dialogQueue.Dequeue();
            _promptText.text = _dialogQueue.Count > 0
                ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
                : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
            return;
        }
        if (_waitingOffering && _dialogQueue.Count == 0)
        {
            _waitingOffering = false;
            PerformOffering();
        }
        if (_dialogQueue.Count == 0)
        {
            Hide();
            return;
        }
        _dialogText.text = _dialogQueue.Dequeue();
        bool offeringShown = _waitingOffering && _dialogText.text == _offerLine;
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : offeringShown
                ? (GameInput.IsMobile ? Localization.T("Chạm để dâng gỗ") : Localization.T("Nhấn E để dâng gỗ"))
                : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
        if (_promptText != null)
            _promptText.gameObject.SetActive(true);
    }

    private void PerformOffering()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        var tm = gm.ToolManager;
        var rm = gm.ReligionManager;
        if (tm == null || rm == null)
            return;
        int wood = tm.CountItem("wood");
        if (wood < 1)
        {
            _dialogQueue.Enqueue("Con chưa mang khúc gỗ nào theo người. Hãy đốn gỗ rồi quay lại nhé.");
            return;
        }
        bool blessed = rm.Worship(ReligionManager.ReligionFaith.Taoism, out bool switched);
        if (!blessed)
        {
            _dialogQueue.Enqueue(rm.CanSwitchToday()
                ? "Con đã dâng hương hôm nay rồi. Hãy quay lại vào ngày mai."
                : "Con đã đổi tín ngưỡng hôm nay rồi. Hãy quay lại vào ngày mai.");
            return;
        }
        tm.RemoveItemAmount("wood", 1);
        _dialogQueue.Enqueue(switched
            ? "Chấp nhận Đạo Giáo. Ta ban phước lành tiên khí: sức lực của con hồi phục gấp đôi cả ngày hôm nay!"
            : "Ta ban phước lành tiên khí: sức lực của con hồi phục gấp đôi cả ngày hôm nay!");
        if (gm.UIManager != null)
            gm.UIManager.ShowMessage(Localization.T("Phước lành tiên khí: hồi phục sức lực gấp đôi cả ngày!"), 2f);
    }

    public void Hide()
    {
        _dialogActive = false;
        if (_panel != null)
            _panel.SetActive(false);
        if (_myTransform != null)
            _myTransform.rotation = _originalRotation;
    }

    private void FacePlayer()
    {
        if (_myTransform == null || _playerTransform == null)
            return;
        Vector3 to = _myTransform.position - _playerTransform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            _myTransform.rotation = Quaternion.LookRotation(to.normalized);
    }

    private void InitializeDialog()
    {
        if (_canvas != null)
            return;
        var hudGo = GameObject.Find("HUD_Canvas");
        _canvas = hudGo != null ? hudGo.GetComponent<Canvas>() : Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
            return;
        CreatePanel();
    }

    private void CreatePanel()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        _panel = new GameObject("TaoistDialogPanel");
        _panel.transform.SetParent(_canvas.transform, false);

        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        rt.sizeDelta = new Vector2(sw * 0.6f, sh * 0.2f);

        var img = _panel.AddComponent<Image>();
        img.color = ColorPalette.UIBackdrop;

        var btn = _panel.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Advance);

        float panelW = sw * 0.6f;
        float panelH = sh * 0.2f;

        _nameText = MakeText("TaoistDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Đạo Sĩ"), 24, new Color(0.35f, 0.65f, 0.45f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("TaoistDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("TaoistDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
            "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}