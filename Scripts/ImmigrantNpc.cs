using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ImmigrantNpc : MonoBehaviour
{
    public static ImmigrantNpc Instance { get; private set; }

    public static void ClearInstance()
    {
        Instance = null;
    }

    private const string _questBaseName = "Xây Nhà Cho Người Di Cư";
    private const long RentPerHouse = 500;

    private string CurrentQuestName()
    {
        var wb = WorldBuilder.Instance;
        int idx = wb != null ? wb.GetImmigrantNextIndex() : 0;
        return _questBaseName + " " + idx;
    }

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

    private int _lastRentDay = -1;

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
        if (_myTransform != null)
            _originalRotation = _myTransform.rotation;
    }

    private bool IsQuestComplete()
    {
        var qm = QuestManager.Instance;
        return qm != null && qm.IsNamedQuestComplete(CurrentQuestName());
    }

    private bool HasActiveQuest()
    {
        var qm = QuestManager.Instance;
        string qn = CurrentQuestName();
        return qm != null && qm.HasQuest(qn) && !qm.IsNamedQuestComplete(qn);
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
        _nameText.text = Localization.T("Người Di Cư");
        _dialogQueue.Clear();
        AddQuestDialogLines();
        Advance();
    }

    private void AddQuestDialogLines()
    {
        var qm = QuestManager.Instance;
        var wb = WorldBuilder.Instance;
        if (qm == null || wb == null)
            return;

        if (wb.AllImmigrantHousesBuilt)
        {
            _dialogQueue.Enqueue("Ông chủ đã cho chúng tôi một cuộc sống mới! Mỗi sáng chúng tôi sẽ trả tiền thuê nhà cho ông chủ.");
            _dialogQueue.Enqueue("Từ nay khu vực này là một phần của làng. Chúng tôi sẽ luôn biết ơn ông chủ.");
            return;
        }

        if (IsQuestComplete())
        {
            _dialogQueue.Enqueue("Cảm ơn ông chủ! Gia đình tôi đã chuyển vào nhà mới.");
            _dialogQueue.Enqueue("Ông chủ cứ yên tâm, chúng tôi sẽ chăm chỉ làm việc.");
            _dialogQueue.Enqueue("Khi nào có người di cư khác đến, ông chủ giúp họ nhé!");
            return;
        }

        if (HasActiveQuest())
        {
            int progress = qm.GetNamedQuestProgress(CurrentQuestName());
            if (progress <= 0)
                _dialogQueue.Enqueue("Ngôi nhà của tôi vẫn chưa xong. Ông chủ giúp tôi dựng nhà nhé!");
            else
                _dialogQueue.Enqueue("Cảm ơn ông chủ! Nhà tôi sắp xong rồi.");
            return;
        }

        _dialogQueue.Enqueue("Xin chào ông chủ! Tôi vừa rời làng cũ, nơi ấy đã chẳng còn chỗ cho chúng tôi nữa.");
        _dialogQueue.Enqueue("Nghe nói vùng đất này còn nhiều chỗ trống. Ông chủ giúp tôi một việc được không?");
        _dialogQueue.Enqueue("Xin hãy dựng một căn nhà nhỏ cho gia đình tôi. Chúng tôi chỉ cần một mái che thôi.");

        int nextIdx = wb.GetImmigrantNextIndex();
        int total = wb.MaxImmigrantHouses;
        string questDesc = Localization.F("Xây một ngôi nhà nhỏ cho người di cư ({0}/{1}).", nextIdx + 1, total);
        qm.AddStoryQuest(CurrentQuestName(), "immigrant_house", 1, 500, questDesc);
        wb.PlaceNextImmigrantBlueprint();

        if (GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.ShowMessage(
                Localization.T("Blueprint đã được đặt tại vị trí quy hoạch! Hãy thu thập gỗ & đá."), 5f);
    }

    public void Advance()
    {
        if (_panel == null)
            return;
        if (_dialogQueue.Count == 0)
        {
            Hide();
            return;
        }
        _dialogText.text = Localization.T(_dialogQueue.Dequeue());
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
    }

    public void OnDayChanged()
    {
        var gm = GameManager.Instance;
        var wb = WorldBuilder.Instance;
        if (gm == null || wb == null || gm.Player == null)
            return;
        if (!IsQuestComplete() && !wb.AllImmigrantHousesBuilt)
        {
            _lastRentDay = gm.CurrentDay;
            return;
        }
        if (_lastRentDay < 0 || _lastRentDay >= gm.CurrentDay)
        {
            _lastRentDay = gm.CurrentDay;
            return;
        }
        int days = gm.CurrentDay - _lastRentDay;
        _lastRentDay = gm.CurrentDay;
        int houses = wb.ImmigrantHousesBuilt;
        if (houses <= 0 || days <= 0)
            return;
        long rent = (long)houses * RentPerHouse * days;
        gm.Player.Money += rent;
        GameStats.AddMoneyEarned(rent);
        if (gm.UIManager != null)
        {
            gm.UIManager.UpdatePlayerHud(gm.Player.HP, gm.Player.MaxHP, gm.Player.Stamina, gm.Player.MaxStamina, gm.Player.Money);
            gm.UIManager.ShowMessage(Localization.F("Nhận {0} đồng tiền thuê nhà từ khu người di cư!", rent), 3f);
        }
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

        _panel = new GameObject("ImmigrantDialogPanel");
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

        _nameText = MakeText("ImmigrantDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Người Di Cư"), 24, new Color(1f, 0.78f, 0.3f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("ImmigrantDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("ImmigrantDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
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
