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

    private const float PATROL_RADIUS = 2.5f;
    private const float WALK_SPEED = 1.8f;
    private const float DOOR_APPROACH_RADIUS = 1.6f;
    private const float FEAR_DISTANCE = 4f;
    private const float AFFECTION_STEAL = 2f;
    private Vector3 _patrolOrigin;
    private Vector3 _target;
    private bool _hasPatrolTarget;
    private float _patrolPause;
    private int _lastStealDay = -1;

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
        _patrolOrigin = _myTransform != null ? _myTransform.position : Vector3.zero;
        var wifeGo = GameObject.Find("WifeNpc");
        if (wifeGo != null) _wifeTransform = wifeGo.transform;
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;

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

        if (night && _wifeTransform != null)
        {
            Vector3 door = _wifeTransform.position + _wifeTransform.right * 2.4f;
            MoveTowards(door, WALK_SPEED * 0.9f);
            if (Vector3.Distance(_myTransform.position, door) < DOOR_APPROACH_RADIUS)
                TryStealAffection();
        }
        else
        {
            Patrol();
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
            Localization.T("Ông chú giàu có đang lảng vảng quanh nhà Jessica..."), 3f);
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
                Random.Range(-PATROL_RADIUS, PATROL_RADIUS), 0f,
                Random.Range(-PATROL_RADIUS, PATROL_RADIUS));
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
        _nameText.text = Localization.T("Phú Ông");
        _dialogQueue.Clear();
        _dialogQueue.Enqueue("Jessica của cậu dạo này trông cô đơn lắm đấy.");
        _dialogQueue.Enqueue("Nếu cậu cứ mải làm nông, tôi sẽ đưa cô ấy đi cho xem.");
        _dialogQueue.Enqueue("Tôi giàu có, còn cậu thì sao? Cô ấy xứng đáng cuộc sống tốt hơn.");
        Advance();
    }

    public void Advance()
    {
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

    public void Hide()
    {
        _dialogActive = false;
        if (_panel != null)
            _panel.SetActive(false);
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
}
