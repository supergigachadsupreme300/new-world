using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Church priest (Batch 20b). Worshipping here means donating 50 coins to the parish;
/// the offering is routed through <see cref="ReligionManager.Worship"/> (joins the Catholic
/// faith / converts the player) and grants the daily holy-water blessing: an instant full heal,
/// with the blessed heals-exalted condition tracked by <see cref="ReligionManager.ChurchHolyWaterBlessed"/>.
/// </summary>
public class PriestNPC : MonoSingleton<PriestNPC>
{
    private const int DonationCost = 50;

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
        "Chúa ban phước lành cho con. Nhà thờ làng luôn rộng mở đón con.",
        "Con hãy sống lương thiện, giúp đỡ người nghèo khó trong làng.",
        "Làng này là nơi duy nhất còn giữ Đức Tin. Hãy bảo vệ nó.",
        "Cầu nguyện mỗi ngày giúp tâm hồn con được thanh thản.",
        "Người nông dân chăm chỉ lao động chính là phụng sự Đấng Tối Cao.",
        "Nước thánh có thể chữa lành thương tích. Hãy dâng cúng để nhận phước lành.",
        "Lòng tin vững vàng hơn mọi bức tường. Đừng để quỷ dữ lay động con.",
        "Mỗi ngày bố thí một chút, Chúa sẽ đưa con vượt qua khó khăn."
    };

    private const string _offerLine =
        "Con hãy góp 50 bạc để giữ gìn nhà thờ. Ta sẽ ban phước lành nước thánh, chữa lành mọi thương tích của con hôm nay.";

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
        bool alreadyBlessed = rm != null && rm.ChurchHolyWaterBlessed;

        FriendshipManager.Instance?.GrantTalk("priest");
        FacePlayer();
        _dialogActive = true;
        _panel.SetActive(true);
        _nameText.text = Localization.T("Cha Xứ");
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
            _dialogQueue.Enqueue("Con đã nhận phước lành nước thánh hôm nay rồi.");
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
                ? (GameInput.IsMobile ? Localization.T("Chạm để bố thí 50 bạc") : Localization.T("Nhấn E để bố thí 50 bạc"))
                : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
        if (_promptText != null)
            _promptText.gameObject.SetActive(true);
    }

    private void PerformOffering()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        var player = gm.Player;
        var rm = gm.ReligionManager;
        if (player == null)
            return;
        if (player.Money < DonationCost)
        {
            _dialogQueue.Enqueue("Con chưa đủ 50 bạc. Hãy quay lại khi có tiền nhé.");
            return;
        }
        bool blessed = rm.Worship(ReligionManager.ReligionFaith.Church, out bool switched);
        if (!blessed)
        {
            _dialogQueue.Enqueue(rm.CanSwitchToday()
                ? "Con đã dâng cúng hôm nay rồi. Hãy quay lại vào ngày mai."
                : "Con đã đổi tín ngưỡng hôm nay rồi. Hãy quay lại vào ngày mai.");
            return;
        }
        player.Money -= DonationCost;
        player.HP = player.MaxHP;
        _dialogQueue.Enqueue(switched
            ? "Chấp nhận Đức Tin Công Giáo. Ta ban phước lành nước thánh: thương tích của con đã lành hẳn cả ngày hôm nay!"
            : "Ta ban phước lành nước thánh: thương tích của con đã lành hẳn cả ngày hôm nay!");
        if (gm.UIManager != null)
            gm.UIManager.ShowMessage(Localization.T("Phước lành nước thánh: máu đã hồi đầy!"), 2f);
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

        _panel = new GameObject("PriestDialogPanel");
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

        _nameText = MakeText("PriestDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Cha Xứ"), 24, new Color(0.35f, 0.35f, 0.9f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("PriestDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("PriestDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
            "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}