using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PagodaMonkNPC : MonoBehaviour
{
    public static PagodaMonkNPC Instance { get; private set; }

    private Transform _myTransform;
    private Transform _playerTransform;

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
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

    public bool IsDialogActive => _dialogActive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _myTransform = transform;
    }

    void Start()
    {
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        if (_blessedDay != gm.CurrentDay)
        {
            _blessedDay = -1;
            if (gm.Player != null)
                gm.Player.StaminaRegenMultiplier = 1f;
        }
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

        FacePlayer();
        _dialogActive = true;
        _panel.SetActive(true);
        _nameText.text = Localization.T("Nhà Sư");
        _dialogQueue.Clear();
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
                "Con đã nhận phước lành hôm nay rồi. Hãy quay lại vào ngày mai nếu muốn dâng gạo tiếp.");
        }
        Advance();
    }

    public void Advance()
    {
        if (_panel == null)
            return;
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
        _dialogText.text = Localization.T(_dialogQueue.Dequeue());
        bool offeringShown = _waitingOffering && _dialogText.text == Localization.T(_offerLine);
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : offeringShown
                ? (GameInput.IsMobile ? Localization.T("Chạm để dâng gạo") : Localization.T("Nhấn E để dâng gạo"))
                : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
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
        if (_panel != null)
            _panel.SetActive(false);
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
        img.color = new Color(0f, 0f, 0f, 0.8f);

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

        _panel.SetActive(false);
    }

    private TMP_Text MakeText(string name, RectTransform parent, Vector2 position, string text,
        int fontSize, Color color, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (GameManager.Instance?.UIManager?.defaultTmpFont != null)
            tmp.font = GameManager.Instance.UIManager.defaultTmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        return tmp;
    }
}
