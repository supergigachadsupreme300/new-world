using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PagodaMonkNPC : MonoSingleton<PagodaMonkNPC>
{
private Transform _myTransform;
    private Transform _playerTransform;
    private Quaternion _originalRotation = Quaternion.identity;

    private Canvas _canvas;
    private UIManager _uiManager;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private TMP_Text _meditationText;
    private GameObject _meditationRow;
    private bool _dialogActive;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    private readonly string[] _lines =
    {
        "A di đà Phật. Con đến chùa lễ Phật à?",
        "Ngôi chùa này là cột mốc của làng, con hãy trân trọng nó.",
        "Muốn khỏe khoắn cả ngày, hãy ăn uống đầy đủ rồi mới ra đồng.",
        "Ngủ đủ giấc cũng là một cách dưỡng sức, con đừng quên.",
        "Câu cá hay thu hoạch đều cần sức. Chú ý giữ gìn sức khỏe.",
        "Bình tâm. Làng này còn lắm chuyện phải trải qua.",
        "Người nông dân giỏi là người biết tiết kiệm sức lực.",
        "Mỗi ngày dâng một bát gạo, nhà chùa sẽ phù hộ con khỏe mạnh.",
        "Ruộng lúa tốt nhờ nước, con người khỏe nhờ điều độ."
    };

    private const string _offerLine =
        "Con có gạo không? Dâng cho nhà chùa một bát gạo, ta sẽ ban phước lành sức khỏe cho con cả ngày hôm nay.";

    private int _lastLine = -1;
    private int _blessedDay = -1;
    private bool _waitingOffering;
    private bool _waitingMeditation;
    private bool _meditationOptionShown;

    public bool IsDialogActive => _dialogActive;

    void Start()
    {
        _uiManager = GameManager.Instance?.UIManager;
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
        if (_myTransform != null)
            _originalRotation = _myTransform.rotation;
    }

    const string _meditationQuestion = "Con có muốn thiền định để gia tăng giới hạn phước đức không?";

    void Update()
    {
        if (_dialogActive && _waitingMeditation)
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                StartMeditation();
                return;
            }
        }

        var gm = GameManager.Instance;
        if (gm == null)
            return;
        if (_blessedDay != gm.CurrentDay)
        {
            _blessedDay = -1;
            if (gm.Player != null)
                gm.Player.StaminaRegenMultiplier = 1f;
        }

        var qm = QuestManager.Instance;
        var wb = WorldBuilder.Instance;
        if (qm != null && wb != null && qm.HasQuest("Trấn Áp Quỷ Vương") && !wb.IsQuestBossAlive)
            wb.SpawnQuestBoss();
    }
    public bool HasBlessingToday
    {
        get { var gm = GameManager.Instance; return gm != null && _blessedDay == gm.CurrentDay; }
    }
    public void Interact()
    {
        if (gameObject == null || !gameObject.activeInHierarchy)
            return;
        InitializeDialog();
        if (_panel == null)
            return;

        FriendshipManager.Instance?.GrantTalk("monk");
        FacePlayer();
        _dialogActive = true;
        _panel.SetActive(true);
        _nameText.text = Localization.T("Nhà Sư");
        _dialogQueue.Clear();
        AddQuestDialogLines();
        for (int i = 0; i < 3; i++)
        {
            _lastLine = (_lastLine + 1) % _lines.Length;
            _dialogQueue.Enqueue(_lines[_lastLine]);
        }
        if (!HasBlessingToday)
        {
            _dialogQueue.Enqueue(_offerLine);
            _waitingOffering = true;
        }
        else
        {
            _dialogQueue.Enqueue(
                "Con đã nhận phước lành hôm nay rồi.");
        }
        _dialogQueue.Enqueue("Con có muốn thiền định để gia tăng giới hạn phước đức không?");
        _waitingMeditation = true;
        _meditationOptionShown = true;
        Advance();
    }
    private void AddQuestDialogLines()
    {
        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        if (qm.IsNamedQuestComplete("Trấn Áp Quỷ Vương"))
        {
            _dialogQueue.Enqueue("Con đã trấn áp Quỷ Vương. Làng này nợ con một ân tình lớn, Phật sẽ phù hộ con.");
            return;
        }
        if (qm.HasQuest("Trấn Áp Quỷ Vương"))
        {
            _dialogQueue.Enqueue("Quỷ Vương đang ngự trị ở cuối con đường phía đông. Hãy dùng Tràng Hạt để trừ tà!");
            return;
        }
        if (qm.IsNamedQuestComplete("Trừ Tà Quanh Chùa"))
        {
            _dialogQueue.Enqueue("Ta thấy con đã dùng Tràng Hạt trừ được quỷ dữ. Giờ ta sẽ khai mở Linh Nhãn cho con.");
            _dialogQueue.Enqueue("Con sẽ thấy những thứ người thường không thấy. Hãy nhìn về cuối con đường phía đông... Quỷ Vương đã thức tỉnh!");
            qm.AddStoryQuest("Trấn Áp Quỷ Vương", "boss_kill", 1, 1000,
                "Quỷ Vương đã thức tỉnh ở cuối con đường phía đông. Dùng Tràng Hạt tiêu diệt nó để bảo vệ làng!");
            if (WorldBuilder.Instance != null)
                WorldBuilder.Instance.SpawnQuestBoss();
            if (_uiManager != null)
                _uiManager.ShowMessage(Localization.T("QUỶ VƯƠNG ĐÃ THỨC TỈNH!"), 3f);
            return;
        }
        if (!qm.HasQuest("Trừ Tà Quanh Chùa"))
        {
            qm.AddStoryQuest("Trừ Tà Quanh Chùa", "rosary_kill", 5, 200,
                "Dùng Tràng Hạt tiêu diệt 5 con quỷ quanh chùa để bảo vệ làng.");
            _dialogQueue.Enqueue("Lũ quỷ nhỏ đang quấy phá quanh chùa. Dùng Tràng Hạt tiêu diệt 5 con để chúng khiếp sợ!");
            return;
        }
        _dialogQueue.Enqueue(Localization.F("Con hãy tiếp tục dùng Tràng Hạt. Tiến độ: {0}/5", qm.GetNamedQuestProgress("Trừ Tà Quanh Chùa")));
    }
    public void Advance()
    {
        if (_panel == null)
            return;
        if (_waitingOffering && _dialogText.text == Localization.T(_offerLine))
        {
            _waitingOffering = false;
            PerformOffering();
            _dialogText.text = Localization.T(_dialogQueue.Dequeue());
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
        if (_waitingMeditation && _dialogQueue.Count == 0)
        {
            _waitingMeditation = false;
            _meditationOptionShown = false;
            if (_meditationRow != null)
                _meditationRow.SetActive(false);
            Hide();
            return;
        }
        if (_dialogQueue.Count == 0)
        {
            Hide();
            return;
        }
        _dialogText.text = Localization.T(_dialogQueue.Dequeue());
        bool offeringShown = _waitingOffering && _dialogText.text == Localization.T(_offerLine);
        bool meditationShown = _waitingMeditation && _dialogText.text == Localization.T(_meditationQuestion);
        if (meditationShown)
            _meditationOptionShown = true;
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : offeringShown
                ? (GameInput.IsMobile ? Localization.T("Chạm để dâng gạo") : Localization.T("Nhấn E để dâng gạo"))
                : meditationShown
                    ? (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"))
                    : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
        if (_promptText != null)
            _promptText.gameObject.SetActive(true);
        if (_meditationRow != null)
            _meditationRow.SetActive(_meditationOptionShown);
        if (_meditationOptionShown && _meditationText != null)
            _meditationText.text = GameInput.IsMobile
                ? Localization.T("[Thiền Định] (Chạm)")
                : Localization.T("[Thiền Định] Nhấn T");
    }
    private void PerformOffering()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        var tm = gm.ToolManager;
        int rice = tm != null ? tm.CountItem("rice") : 0;
        int riceBag = tm != null ? tm.CountItem("tu_gao") : 0;
        if (rice >= 1 || riceBag >= 1)
        {
            if (rice >= 1)
                tm.RemoveItemAmount("rice", 1);
            else
                tm.RemoveItemAmount("tu_gao", 1);
            _blessedDay = gm.CurrentDay;
            if (gm.Player != null)
                gm.Player.StaminaRegenMultiplier = 2f;
            _dialogQueue.Enqueue(
                "Con thành tâm dâng gạo, nhà chùa xin ban phước lành. Sức lực của con sẽ hồi phục nhanh gấp đôi cả ngày hôm nay.");
            if (gm.UIManager != null)
                gm.UIManager.ShowMessage(Localization.T("Phước lành: hồi phục sức lực gấp đôi cả ngày!"), 2f);
        }
        else
        {
            _dialogQueue.Enqueue("Con chưa mang gạo theo người. Hãy quay lại khi có gạo nhé.");
        }
    }
    public void Hide()
    {
        _dialogActive = false;
        _meditationOptionShown = false;
        if (_panel != null)
            _panel.SetActive(false);
        if (_myTransform != null)
            _myTransform.rotation = _originalRotation;
    }
    private void StartMeditation()
    {
        if (!_waitingMeditation || !_dialogActive)
            return;
        _waitingMeditation = false;
        _meditationOptionShown = false;
        if (_meditationRow != null)
            _meditationRow.SetActive(false);
        if (_promptText != null)
            _promptText.gameObject.SetActive(true);
        TypingMinigame.Instance?.Open();
        Hide();
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

        _panel = new GameObject("MonkDialogPanel");
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

        _nameText = MakeText("MonkDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Nhà Sư"), 24, new Color(1f, 0.78f, 0.3f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("MonkDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("MonkDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
            "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        _meditationRow = new GameObject("MonkMeditationRow");
        _meditationRow.transform.SetParent(rt, false);
        var medRowRt = _meditationRow.AddComponent<RectTransform>();
        medRowRt.anchorMin = new Vector2(1f, 0f);
        medRowRt.anchorMax = new Vector2(1f, 0f);
        medRowRt.pivot = new Vector2(1f, 0f);
        medRowRt.anchoredPosition = new Vector2(-20f, panelH + 6f);
        medRowRt.sizeDelta = new Vector2(300f, 40f);
        var medRowImg = _meditationRow.AddComponent<Image>();
        medRowImg.color = new Color(0.4f, 0.3f, 0.6f, 0.9f);
        var medRowBtn = _meditationRow.AddComponent<Button>();
        medRowBtn.targetGraphic = medRowImg;
        medRowBtn.onClick.AddListener(StartMeditation);
        _meditationText = MakeText("MonkMeditationText", medRowRt, new Vector2(0f, 0f),
            Localization.T("[Thiền Định] Nhấn T"), 18, Color.white, new Vector2(270f, 36f));
        _meditationRow.SetActive(false);

        _panel.SetActive(false);
    }
    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
        => CountryLife.Helpers.UIHelper.MakeText(name, parent, position, text, fontSize, color, size, true, true, TextAlignmentOptions.Left, false);
}
