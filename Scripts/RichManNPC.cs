using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RichManNPC : MonoBehaviour
{
    public static RichManNPC Instance { get; private set; }

    private Transform _myTransform;
    private Transform _wifeTransform;
    private Transform _playerTransform;
    private Quaternion _originalRotation = Quaternion.identity;

    private const float HOME_PATROL_RADIUS = 6f;
    private const float WALK_SPEED = 1.8f;
    private const float FEAR_DISTANCE = 4f;
    private const float AFFECTION_STEAL = 2f;
    private const float VISIT_DURATION = 6f;
    private const float DEAL_RANGE = 6f;
    private const int DEAL_START_DAY = 3;

    private enum VisitState { AtHome, WalkingToWife, Visiting, WalkingHome }
    private VisitState _visitState = VisitState.AtHome;
    private Vector3 _homePosition;
    private Vector3 _wifeDoorPosition;
    private float _visitTimer;
    private Vector3 _patrolOrigin;
    private Vector3 _target;
    private bool _hasPatrolTarget;
    private float _patrolPause;
    private int _lastStealDay = -1;

    private enum DealState { None, WalkingToMeeting, Meeting, Leaving }
    private DealState _dealState = DealState.None;
    private GameObject _dealer;
    private readonly Vector3 _meetingSpot = new Vector3(58f, 0f, 50f);
    private readonly Vector3 _dealerSpawn = new Vector3(62f, 0.97f, 70f);
    private readonly Vector3 _dealerLeave = new Vector3(70f, 0.97f, 66f);
    private float _meetingTimer;
    private int _lastDealDay = -1;
    private bool _richAtMeeting;

    private GameObject _leaveRow;
    private GameObject _bribeRow;
    private TMP_Text _leaveText;
    private TMP_Text _bribeText;
    private bool _endingChoiceAvailable;
    private bool _endingChoiceShown;

    public bool Discovered { get; private set; }
    public bool IsEndingChoiceShown => _endingChoiceShown;

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private bool _dialogActive;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    public bool IsDialogActive => _dialogActive;
    public Transform NpcTransform => _myTransform;

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
        _homePosition = _myTransform != null ? _myTransform.position : Vector3.zero;
        _patrolOrigin = _homePosition;
        var wifeGo = GameObject.Find("WifeNpc");
        if (wifeGo != null) _wifeTransform = wifeGo.transform;
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
        if (_myTransform != null)
            _originalRotation = _myTransform.rotation;

        if (WifeNPC.Instance != null && WifeNPC.Instance.Married)
        {
            Retire();
            return;
        }
        InitializeDialog();
    }

    public void Retire()
    {
        if (_panel != null) _panel.SetActive(false);
        _dialogActive = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null)
            return;
        if (GameManager.Instance.GamePaused)
            return;
        if (CutsceneManager.Instance != null && CutsceneManager.Instance.IsActive)
            return;

        if (_myTransform == null)
            return;

        if (WifeNPC.Instance != null && WifeNPC.Instance.Married)
        {
            Retire();
            return;
        }

        if (_wifeTransform == null)
        {
            var wifeGo = GameObject.Find("WifeNpc");
            if (wifeGo != null) _wifeTransform = wifeGo.transform;
        }

        if (_dialogActive)
            return;

        float hour = GameManager.Instance.TimeOfDay;
        bool night = hour >= 18f || hour < 6f;

        if (_playerTransform != null &&
            Vector3.Distance(_playerTransform.position, _myTransform.position) < FEAR_DISTANCE)
        {
            MoveAwayFromPlayer();
            return;
        }

        if (_wifeTransform == null)
        {
            Patrol();
            return;
        }

        if (_dealState != DealState.None)
        {
            UpdateDeal();
            return;
        }

        switch (_visitState)
        {
            case VisitState.AtHome:
            {
                bool stoleToday = _lastStealDay == GameManager.Instance.CurrentDay;
                if (TryStartDeal())
                {
                    // nightly drug deal has begun
                }
                else if (night && !stoleToday && !Discovered)
                {
                    _visitState = VisitState.WalkingToWife;
                    _hasPatrolTarget = false;
                }
                else
                {
                    Patrol();
                }
                break;
            }
            case VisitState.WalkingToWife:
            {
                _wifeDoorPosition = _wifeTransform.position + _wifeTransform.right * 2.4f;
                if (MoveTowards(_wifeDoorPosition, WALK_SPEED * 0.9f))
                {
                    _visitState = VisitState.Visiting;
                    _visitTimer = VISIT_DURATION;
                    TryStealAffection();
                }
                break;
            }
            case VisitState.Visiting:
            {
                _visitTimer -= Time.deltaTime;
                if (_visitTimer <= 0f)
                    _visitState = VisitState.WalkingHome;
                break;
            }
            case VisitState.WalkingHome:
            {
                if (MoveTowards(_homePosition, WALK_SPEED))
                {
                    _visitState = VisitState.AtHome;
                    _hasPatrolTarget = false;
                }
                break;
            }
        }
    }

    private void TryStealAffection()
    {
        int today = GameManager.Instance.CurrentDay;
        if (_lastStealDay == today)
            return;
        _lastStealDay = today;

        if (WifeNPC.Instance != null)
            WifeNPC.Instance.ApplyAffectionChange(-AFFECTION_STEAL);
        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.T("Ông chú giàu có lại sang nhà Jessica giữa đêm..."), 3f);
    }

    private bool TryStartDeal()
    {
        if (_dealState != DealState.None) return false;
        if (Discovered) return false;
        if (WifeNPC.Instance != null && WifeNPC.Instance.Married) return false;
        int today = GameManager.Instance.CurrentDay;
        if (today < DEAL_START_DAY) return false;
        if (_lastDealDay == today) return false;
        if (GameManager.Instance.TimeOfDay < 21f) return false;
        _lastDealDay = today;
        _dealState = DealState.WalkingToMeeting;
        _richAtMeeting = false;
        _hasPatrolTarget = false;
        return true;
    }

    private void UpdateDeal()
    {
        switch (_dealState)
        {
            case DealState.WalkingToMeeting:
            {
                if (_dealer == null)
                    _dealer = BuildDealerNpc(null, _dealerSpawn);
                if (!_richAtMeeting && MoveTowards(_meetingSpot, WALK_SPEED * 1.1f))
                    _richAtMeeting = true;
                if (_dealer != null)
                {
                    MoveDealerTowards(_dealer, _meetingSpot, 2.2f);
                    if (_richAtMeeting && Vector3.Distance(_dealer.transform.position, _meetingSpot) < 1.2f)
                    {
                        _dealState = DealState.Meeting;
                        _meetingTimer = 5f;
                    }
                }
                break;
            }
            case DealState.Meeting:
            {
                _meetingTimer -= Time.deltaTime;
                if (_meetingTimer <= 0f)
                {
                    _dealState = DealState.Leaving;
                    _richAtMeeting = false;
                }
                break;
            }
            case DealState.Leaving:
            {
                if (_dealer != null)
                {
                    MoveDealerTowards(_dealer, _dealerLeave, 2.5f);
                    if (Vector3.Distance(_dealer.transform.position, _dealerLeave) < 1f)
                    {
                        Object.Destroy(_dealer);
                        _dealer = null;
                    }
                }
                if (!_richAtMeeting && MoveTowards(_homePosition, WALK_SPEED))
                {
                    _dealState = DealState.None;
                    _richAtMeeting = false;
                    _hasPatrolTarget = false;
                }
                break;
            }
        }
    }

    private void MoveDealerTowards(GameObject dealer, Vector3 dest, float speed)
    {
        if (dealer == null) return;
        dest.y = dealer.transform.position.y;
        dealer.transform.position = Vector3.MoveTowards(dealer.transform.position, dest, speed * Time.deltaTime);
        Vector3 to = dest - dealer.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            dealer.transform.rotation = Quaternion.LookRotation(-to.normalized);
    }

    public bool TryEavesdropDeal(Vector3 playerPos)
    {
        if (Discovered) return false;
        if (_dealState == DealState.None) return false;
        if (_dealer == null) return false;
        if (Vector3.Distance(playerPos, _meetingSpot) > DEAL_RANGE) return false;
        DiscoverSecret();
        return true;
    }

    private void DiscoverSecret()
    {
        Discovered = true;
        if (_dealer != null)
        {
            Object.Destroy(_dealer);
            _dealer = null;
        }
        _dealState = DealState.None;
        _richAtMeeting = false;
        _visitState = VisitState.WalkingHome;
        _hasPatrolTarget = false;
        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.T("Bạn đã phát hiện hoạt động phi pháp của Phú Ông!"), 4f);
        QuestManager.Instance?.AddProgress("mansion_secret", 1);
    }

    public void SetDiscovered(bool value)
    {
        Discovered = value;
    }

    public void ChooseLeave()
    {
        if (!_endingChoiceShown) return;
        HideEndingChoice();
        Hide();
    }

    public void ChooseBribe()
    {
        if (!_endingChoiceShown) return;
        HideEndingChoice();
        Hide();
        GameManager.Instance?.RequestBlackmailEnding();
    }

    private void Patrol()
    {
        if (_patrolPause > 0f)
        {
            _patrolPause -= Time.deltaTime;
            return;
        }

        if (!_hasPatrolTarget)
        {
            _target = _patrolOrigin + new Vector3(
                Random.Range(-HOME_PATROL_RADIUS, HOME_PATROL_RADIUS), 0f,
                Random.Range(-HOME_PATROL_RADIUS, HOME_PATROL_RADIUS));
            _hasPatrolTarget = true;
        }

        if (MoveTowards(_target, WALK_SPEED))
        {
            _hasPatrolTarget = false;
            _patrolPause = Random.Range(1.5f, 3.5f);
        }
    }

    private bool MoveTowards(Vector3 dest, float speed)
    {
        if (_myTransform == null)
            return true;
        dest.y = _myTransform.position.y;
        Vector3 to = dest - _myTransform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.001f)
            return true;

        _myTransform.position = Vector3.MoveTowards(_myTransform.position, dest, speed * Time.deltaTime);
        _myTransform.rotation = Quaternion.LookRotation(-to.normalized);
        return Vector3.Distance(_myTransform.position, dest) < 0.1f;
    }

    private void MoveAwayFromPlayer()
    {
        if (_myTransform == null || _playerTransform == null)
            return;
        Vector3 away = _myTransform.position - _playerTransform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f)
            away = Vector3.forward;
        away.Normalize();

        _myTransform.position += away * (WALK_SPEED * 1.7f * Time.deltaTime);
        _myTransform.rotation = Quaternion.LookRotation(-away);
    }

    public void Interact()
    {
        InitializeDialog();
        if (_panel == null)
            return;

        _dialogActive = true;
        _panel.SetActive(true);
        FacePlayer();
        _nameText.text = Localization.T("Phú Ông");
        _dialogQueue.Clear();
        if (Discovered)
        {
            _dialogQueue.Enqueue("Cậu... đã nhìn thấy chuyện đêm qua rồi sao?");
            _dialogQueue.Enqueue("Được thôi. Cậu là người thông minh. Chúng ta có thể... thỏa thuận.");
            _dialogQueue.Enqueue("Im lặng, và cậu sẽ có một món tiền cậu không thể từ chối.");
        }
        else
        {
            _dialogQueue.Enqueue("Hừ. Một kẻ làm ruộng như cậu mà cũng dám bắt chuyện với ta?");
            _dialogQueue.Enqueue("Ta có vàng, có đất, cả nửa dãy phố. Còn cậu? Một mảnh ruộng và vài con gà.");
            _dialogQueue.Enqueue("Đừng làm ta mất thời gian. Về lo đám lúa của cậu đi.");
        }
        _endingChoiceAvailable = Discovered;
        _endingChoiceShown = false;
        HideEndingChoice();
        Advance();
    }

    public void Advance()
    {
        if (_dialogQueue.Count == 0)
        {
            if (_endingChoiceAvailable)
            {
                _endingChoiceAvailable = false;
                ShowEndingChoice();
                return;
            }
            Hide();
            return;
        }
        _dialogText.text = Localization.T(_dialogQueue.Dequeue());
        _promptText.text = _dialogQueue.Count > 0
            ? (GameInput.IsMobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : (GameInput.IsMobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));
    }

    private void ShowEndingChoice()
    {
        _endingChoiceShown = true;
        bool mobile = GameInput.IsMobile;
        if (_leaveText != null)
            _leaveText.text = mobile ? Localization.T("[Bỏ Đi] (Chạm)") : Localization.T("[Bỏ Đi] Ấn 1");
        if (_bribeText != null)
            _bribeText.text = mobile ? Localization.T("[Nhận Hối Lộ] (Chạm)") : Localization.T("[Nhận Hối Lộ] Ấn 2");
        if (_leaveRow != null) _leaveRow.SetActive(true);
        if (_bribeRow != null) _bribeRow.SetActive(true);
        if (_promptText != null)
            _promptText.text = Localization.T("Hãy lựa chọn...");
    }

    private void HideEndingChoice()
    {
        if (_leaveRow != null) _leaveRow.SetActive(false);
        if (_bribeRow != null) _bribeRow.SetActive(false);
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

        _panel = new GameObject("RichManDialogPanel");
        _panel.transform.SetParent(_canvas.transform, false);

        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        rt.sizeDelta = new Vector2(sw * 0.6f, sh * 0.22f);

        var img = _panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.8f);

        var btn = _panel.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Advance);

        float panelW = sw * 0.6f;
        float panelH = sh * 0.22f;

        _nameText = MakeText("RichManDialogName", rt, new Vector2(0f, panelH * 0.38f),
            Localization.T("Phú Ông"), 24, new Color(0.9f, 0.78f, 0.35f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("RichManDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("RichManDialogPrompt", rt, new Vector2(0f, -panelH * 0.38f),
            "", 16, new Color(0.7f, 0.7f, 0.7f), new Vector2(panelW - 40f, 25f));

        CreateOptionRow("RichManLeaveRow", "RichManLeaveText", -6f,
            new Color(0.8f, 0.8f, 0.85f), out _leaveRow, out _leaveText, OnLeaveChoice);
        CreateOptionRow("RichManBribeRow", "RichManBribeText", -54f,
            new Color(1f, 0.85f, 0.3f), out _bribeRow, out _bribeText, OnBribeChoice);

        _panel.SetActive(false);
    }

    private void OnLeaveChoice()
    {
        HideEndingChoice();
        Hide();
    }

    private void OnBribeChoice()
    {
        HideEndingChoice();
        Hide();
        GameManager.Instance?.RequestBlackmailEnding();
    }

    private void CreateOptionRow(string rowName, string textName, float yOffset,
        Color textColor, out GameObject row, out TMP_Text text,
        UnityEngine.Events.UnityAction onClick)
    {
        row = new GameObject(rowName);
        row.transform.SetParent(_panel.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(1f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(1f, 1f);
        rowRt.anchoredPosition = new Vector2(-20f, yOffset);
        rowRt.sizeDelta = new Vector2(300f, 40f);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = new Color(0f, 0f, 0f, 0.8f);
        rowImg.raycastTarget = true;

        var rowBtn = row.AddComponent<Button>();
        rowBtn.targetGraphic = rowImg;
        rowBtn.onClick.AddListener(onClick);

        text = MakeText(textName, rowRt, new Vector2(0f, 0f), "", 18, textColor, new Vector2(272f, 36f));
        text.alignment = TextAlignmentOptions.Left;

        row.SetActive(false);
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

    public static GameObject BuildRichManNpc(Transform parent, Vector3 position, float scale = 1f,
        Quaternion rotation = default, bool registerInstance = true)
    {
        var root = new GameObject("RichManNpc");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation != default ? rotation : Quaternion.identity;
        root.transform.localScale = Vector3.one * scale;

        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color noseC = new Color(0.87f, 0.73f, 0.6f);
        Color suitC = new Color(0.16f, 0.18f, 0.30f);
        Color vestC = new Color(0.45f, 0.1f, 0.12f);
        Color goldC = new Color(0.92f, 0.78f, 0.3f);
        Color shirtC = new Color(1f, 1f, 1f);
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);
        Color eyeDarkC = new Color(0.1f, 0.05f, 0.05f);
        Color hatC = new Color(0.12f, 0.12f, 0.14f);
        Color tieC = new Color(0.62f, 0.15f, 0.18f);
        Color beltC = new Color(0.25f, 0.16f, 0.08f);
        Color shoeC = new Color(0.15f, 0.12f, 0.1f);
        Color caneC = new Color(0.2f, 0.12f, 0.06f);

        var legsRoot = new GameObject("LegsRoot");
        legsRoot.transform.SetParent(root.transform);
        legsRoot.transform.localPosition = Vector3.zero;

        MakeBlock("ThighL", legsRoot.transform, new Vector3(0.2f, 0.3f, 0.2f), new Vector3(-0.16f, -0.28f, 0f), suitC, true);
        MakeBlock("ThighR", legsRoot.transform, new Vector3(0.2f, 0.3f, 0.2f), new Vector3(0.16f, -0.28f, 0f), suitC, true);
        MakeBlock("ShinL", legsRoot.transform, new Vector3(0.17f, 0.3f, 0.17f), new Vector3(-0.16f, -0.58f, 0f), suitC, true);
        MakeBlock("ShinR", legsRoot.transform, new Vector3(0.17f, 0.3f, 0.17f), new Vector3(0.16f, -0.58f, 0f), suitC, true);
        MakeBlock("ShoeL", legsRoot.transform, new Vector3(0.2f, 0.08f, 0.32f), new Vector3(-0.16f, -0.82f, -0.03f), shoeC, true);
        MakeBlock("ShoeR", legsRoot.transform, new Vector3(0.2f, 0.08f, 0.32f), new Vector3(0.16f, -0.82f, -0.03f), shoeC, true);

        var bodyRoot = new GameObject("BodyRoot");
        bodyRoot.transform.SetParent(root.transform);
        bodyRoot.transform.localPosition = Vector3.zero;

        MakeBlock("Belly", bodyRoot.transform, new Vector3(0.58f, 0.48f, 0.5f), new Vector3(0f, 0.05f, -0.08f), suitC, true);
        MakeBlock("Chest", bodyRoot.transform, new Vector3(0.62f, 0.34f, 0.34f), new Vector3(0f, 0.38f, 0f), suitC, true);
        MakeBlock("BellyGold", bodyRoot.transform, new Vector3(0.12f, 0.08f, 0.06f), new Vector3(0f, 0.06f, -0.33f), goldC, true);
        MakeBlock("Tie", bodyRoot.transform, new Vector3(0.14f, 0.3f, 0.06f), new Vector3(0f, 0.22f, -0.18f), tieC, true);
        MakeBlock("ShirtCollar", bodyRoot.transform, new Vector3(0.15f, 0.05f, 0.05f), new Vector3(0f, 0.545f, -0.175f), shirtC, true);
        MakeBlock("Vest", bodyRoot.transform, new Vector3(0.26f, 0.26f, 0.05f), new Vector3(0f, 0.27f, -0.175f), vestC, true);
        MakeBlock("VestButton1", bodyRoot.transform, new Vector3(0.032f, 0.032f, 0.03f), new Vector3(0f, 0.345f, -0.204f), goldC, true);
        MakeBlock("VestButton2", bodyRoot.transform, new Vector3(0.032f, 0.032f, 0.03f), new Vector3(0f, 0.28f, -0.204f), goldC, true);
        MakeBlock("VestButton3", bodyRoot.transform, new Vector3(0.032f, 0.032f, 0.03f), new Vector3(0f, 0.215f, -0.204f), goldC, true);
        MakeBlock("PocketSquare", bodyRoot.transform, new Vector3(0.07f, 0.08f, 0.02f), new Vector3(0.065f, 0.49f, -0.176f), shirtC, true);
        MakeBlock("Belt", bodyRoot.transform, new Vector3(0.36f, 0.06f, 0.03f), new Vector3(0f, 0.13f, -0.31f), beltC, true);
        MakeBlock("BeltBuckle", bodyRoot.transform, new Vector3(0.09f, 0.05f, 0.02f), new Vector3(0f, 0.13f, -0.33f), goldC, true);
        MakeBlock("PocketChain", bodyRoot.transform, new Vector3(0.015f, 0.07f, 0.015f), new Vector3(0.16f, 0.11f, -0.30f), goldC, true, Quaternion.Euler(0f, 0f, 15f));
        MakeBlock("Neck", bodyRoot.transform, new Vector3(0.16f, 0.12f, 0.16f), new Vector3(0f, 0.58f, 0f), skinC, true);

        MakeBlock("Head", bodyRoot.transform, new Vector3(0.36f, 0.32f, 0.34f), new Vector3(0f, 0.74f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", bodyRoot.transform, new Vector3(0.09f, 0.06f, 0.03f), new Vector3(-0.09f, 0.76f, -0.165f), eyeWhiteC, true);
        MakeBlock("EyeWhiteR", bodyRoot.transform, new Vector3(0.09f, 0.06f, 0.03f), new Vector3(0.09f, 0.76f, -0.165f), eyeWhiteC, true);
        MakeBlock("EyeIrisL", bodyRoot.transform, new Vector3(0.05f, 0.05f, 0.03f), new Vector3(-0.09f, 0.755f, -0.178f), eyeDarkC, true);
        MakeBlock("EyeIrisR", bodyRoot.transform, new Vector3(0.05f, 0.05f, 0.03f), new Vector3(0.09f, 0.755f, -0.178f), eyeDarkC, true);
        MakeBlock("EyebrowL", bodyRoot.transform, new Vector3(0.1f, 0.022f, 0.02f), new Vector3(-0.09f, 0.815f, -0.17f), hatC, true);
        MakeBlock("EyebrowR", bodyRoot.transform, new Vector3(0.1f, 0.022f, 0.02f), new Vector3(0.09f, 0.815f, -0.17f), hatC, true);
        MakeBlock("Nose", bodyRoot.transform, new Vector3(0.08f, 0.07f, 0.05f), new Vector3(0f, 0.735f, -0.19f), noseC, true);
        MakeBlock("JowlL", bodyRoot.transform, new Vector3(0.08f, 0.1f, 0.1f), new Vector3(-0.17f, 0.68f, -0.08f), skinC, true);
        MakeBlock("JowlR", bodyRoot.transform, new Vector3(0.08f, 0.1f, 0.1f), new Vector3(0.17f, 0.68f, -0.08f), skinC, true);
        MakeBlock("MustacheMain", bodyRoot.transform, new Vector3(0.22f, 0.05f, 0.055f), new Vector3(0f, 0.70f, -0.19f), hatC, true);
        MakeBlock("MustacheTipL", bodyRoot.transform, new Vector3(0.075f, 0.035f, 0.05f), new Vector3(-0.115f, 0.685f, -0.185f), hatC, true);
        MakeBlock("MustacheTipR", bodyRoot.transform, new Vector3(0.075f, 0.035f, 0.05f), new Vector3(0.115f, 0.685f, -0.185f), hatC, true);
        MakeBlock("Goatee", bodyRoot.transform, new Vector3(0.08f, 0.045f, 0.045f), new Vector3(0f, 0.655f, -0.188f), hatC, true);
        MakeBlock("Smirk", bodyRoot.transform, new Vector3(0.13f, 0.018f, 0.018f), new Vector3(0f, 0.62f, -0.19f), hatC, true);
        MakeBlock("HairSideL", bodyRoot.transform, new Vector3(0.4f, 0.12f, 0.1f), new Vector3(-0.18f, 0.82f, 0f), hatC, true, Quaternion.Euler(0f, 90f, 0f));
        MakeBlock("HairSideR", bodyRoot.transform, new Vector3(0.4f, 0.12f, 0.1f), new Vector3(0.18f, 0.82f, 0f), hatC, true, Quaternion.Euler(0f, 90f, 0f));
        MakeBlock("HatBand", bodyRoot.transform, new Vector3(0.36f, 0.03f, 0.34f), new Vector3(0f, 0.965f, 0f), goldC, true);
        MakeBlock("HatBrim", bodyRoot.transform, new Vector3(0.5f, 0.05f, 0.48f), new Vector3(0f, 0.94f, 0f), hatC, true);
        MakeBlock("HatTop", bodyRoot.transform, new Vector3(0.34f, 0.16f, 0.32f), new Vector3(0f, 1.02f, 0f), hatC, true);

        MakeBlock("ArmL", bodyRoot.transform, new Vector3(0.16f, 0.42f, 0.16f), new Vector3(-0.4f, 0.28f, 0f), suitC, true);
        MakeBlock("ArmR", bodyRoot.transform, new Vector3(0.16f, 0.42f, 0.16f), new Vector3(0.4f, 0.28f, 0f), suitC, true);
        MakeBlock("HandL", bodyRoot.transform, new Vector3(0.12f, 0.12f, 0.12f), new Vector3(-0.4f, 0.04f, 0f), skinC, true);
        MakeBlock("HandR", bodyRoot.transform, new Vector3(0.12f, 0.12f, 0.12f), new Vector3(0.4f, 0.04f, 0f), skinC, true);
        MakeBlock("WatchBandL", bodyRoot.transform, new Vector3(0.05f, 0.06f, 0.05f), new Vector3(-0.4f, 0.10f, -0.05f), goldC, true);
        MakeBlock("WatchFaceL", bodyRoot.transform, new Vector3(0.035f, 0.04f, 0.02f), new Vector3(-0.4f, 0.10f, -0.062f), shirtC, true);
        MakeBlock("RingR", bodyRoot.transform, new Vector3(0.08f, 0.06f, 0.08f), new Vector3(0.4f, -0.05f, 0f), goldC, true);

        MakeBlock("CaneShaft", bodyRoot.transform, new Vector3(0.035f, 0.88f, 0.035f), new Vector3(0.40f, -0.42f, -0.10f), caneC, true);
        MakeBlock("CaneTip", bodyRoot.transform, new Vector3(0.045f, 0.05f, 0.045f), new Vector3(0.40f, -0.89f, -0.10f), goldC, true);
        MakeBlock("CaneHandle", bodyRoot.transform, new Vector3(0.055f, 0.05f, 0.055f), new Vector3(0.40f, 0.07f, -0.10f), goldC, true);
        MakeBlock("Coin1", bodyRoot.transform, new Vector3(0.06f, 0.015f, 0.06f), new Vector3(-0.40f, 0.10f, -0.05f), goldC, true);
        MakeBlock("Coin2", bodyRoot.transform, new Vector3(0.055f, 0.015f, 0.055f), new Vector3(-0.40f, 0.115f, -0.045f), goldC, true);
        MakeBlock("Coin3", bodyRoot.transform, new Vector3(0.05f, 0.015f, 0.05f), new Vector3(-0.40f, 0.13f, -0.05f), goldC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(1.0f, 1.9f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        if (registerInstance)
            root.AddComponent<RichManNPC>();

        return root;
    }

    private static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position,
        Color color, bool removeCollider = false, Quaternion rotation = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        if (rotation != default) go.transform.localRotation = rotation;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        if (removeCollider)
            Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    public static GameObject BuildDealerNpc(Transform parent, Vector3 position)
    {
        var root = new GameObject("DealerNpc");
        root.transform.SetParent(parent);
        root.transform.position = position;

        Color cloakC = new Color(0.13f, 0.09f, 0.1f);
        Color hoodC = new Color(0.09f, 0.06f, 0.07f);
        Color scarfC = new Color(0.55f, 0.05f, 0.05f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color sackC = new Color(0.5f, 0.4f, 0.25f);
        Color bootC = new Color(0.15f, 0.12f, 0.1f);
        Color goldC = new Color(0.9f, 0.8f, 0.3f);

        MakeBlock("LegL", root.transform, new Vector3(0.17f, 0.55f, 0.17f), new Vector3(-0.15f, -0.62f, 0f), cloakC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.55f, 0.17f), new Vector3(0.15f, -0.62f, 0f), cloakC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.2f, 0.1f, 0.3f), new Vector3(-0.15f, -0.92f, 0f), bootC, true);
        MakeBlock("BootR", root.transform, new Vector3(0.2f, 0.1f, 0.3f), new Vector3(0.15f, -0.92f, 0f), bootC, true);

        MakeBlock("Cloak", root.transform, new Vector3(0.55f, 0.75f, 0.45f), new Vector3(0f, -0.15f, 0f), cloakC, true);
        MakeBlock("CloakTrim", root.transform, new Vector3(0.58f, 0.06f, 0.1f), new Vector3(0f, -0.5f, -0.24f), scarfC, true);
        MakeBlock("Belt", root.transform, new Vector3(0.58f, 0.07f, 0.05f), new Vector3(0f, -0.05f, -0.24f), bootC, true);
        MakeBlock("BeltBuckle", root.transform, new Vector3(0.12f, 0.09f, 0.05f), new Vector3(0f, -0.05f, -0.27f), goldC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.15f, 0.12f, 0.15f), new Vector3(0f, 0.42f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.58f, 0f), skinC, true);
        MakeBlock("HoodBack", root.transform, new Vector3(0.38f, 0.42f, 0.42f), new Vector3(0f, 0.66f, 0.02f), hoodC, true);
        MakeBlock("HoodTop", root.transform, new Vector3(0.34f, 0.2f, 0.34f), new Vector3(0f, 0.88f, 0f), hoodC, true);
        MakeBlock("Scarf", root.transform, new Vector3(0.34f, 0.09f, 0.08f), new Vector3(0f, 0.42f, -0.17f), scarfC, true);
        MakeBlock("ScarfTail", root.transform, new Vector3(0.06f, 0.18f, 0.06f), new Vector3(-0.12f, 0.38f, -0.2f), scarfC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.07f, 0.05f), new Vector3(0f, 0.58f, -0.17f), new Color(0.6f, 0.5f, 0.42f), true);

        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.36f, 0.12f, 0f), cloakC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.36f, 0.12f, 0f), cloakC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.11f, 0.11f, 0.11f), new Vector3(-0.36f, -0.12f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.11f, 0.11f, 0.11f), new Vector3(0.36f, -0.12f, 0f), skinC, true);

        MakeBlock("Sack", root.transform, new Vector3(0.3f, 0.35f, 0.28f), new Vector3(0.34f, -0.3f, 0.12f), sackC, true);
        MakeBlock("SackTie", root.transform, new Vector3(0.08f, 0.08f, 0.08f), new Vector3(0.34f, -0.1f, 0.12f), scarfC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.35f, 0f);
        col.isTrigger = true;

        return root;
    }
}
