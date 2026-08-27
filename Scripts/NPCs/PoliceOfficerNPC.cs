using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PoliceOfficerNPC : MonoSingleton<PoliceOfficerNPC>
{
private Transform _myTransform;
    private Transform _playerTransform;
    private Quaternion _originalRotation = Quaternion.identity;

    private const float WALK_SPEED = 1.4f;

    private Vector3 _patrolOrigin;
    private readonly List<Vector3> _patrolWaypoints = new List<Vector3>();
    private int _waypointIndex;
    private Vector3 _target;
    private bool _hasPatrolTarget;
    private float _patrolPause;

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private bool _dialogActive;
    private readonly Queue<string> _dialogQueue = new Queue<string>();

    private Transform _hipL, _hipR, _shoulderL, _shoulderR;
    private float _walkCycle;
    private bool _isWalking;

    public bool IsDialogActive => _dialogActive;

    void Start()
    {
        _patrolOrigin = _myTransform != null ? _myTransform.position : Vector3.zero;
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
        if (_myTransform != null)
            _originalRotation = _myTransform.rotation;

        _hipL = _myTransform?.Find("HipL");
        _hipR = _myTransform?.Find("HipR");
        _shoulderL = _myTransform?.Find("ShoulderL");
        _shoulderR = _myTransform?.Find("ShoulderR");

        _patrolWaypoints.Add(_patrolOrigin + new Vector3(-2f, 0f, 0f));
        _patrolWaypoints.Add(_patrolOrigin + new Vector3(2.2f, 0f, 0.8f));
        _patrolWaypoints.Add(_patrolOrigin + new Vector3(1.2f, 0f, 2.6f));
        _patrolWaypoints.Add(_patrolOrigin + new Vector3(3f, 0f, -0.5f));
        _patrolWaypoints.Add(_patrolOrigin + new Vector3(0f, 0f, -2.2f));
    }

    void Update()
    {
        if (GameManager.Instance == null)
            return;
        if (GameManager.Instance.GamePaused)
        {
            _isWalking = false;
            return;
        }
        if (CutsceneManager.Instance != null && CutsceneManager.Instance.IsActive)
        {
            _isWalking = false;
            return;
        }
        if (_myTransform == null)
            return;
        if (_dialogActive)
        {
            _isWalking = false;
            return;
        }

        if (_playerTransform == null)
        {
            var playerGo = GameObject.Find("Player");
            if (playerGo != null) _playerTransform = playerGo.transform;
        }

        Patrol();
        AnimateWalk();
    }
    private void AnimateWalk()
    {
        if (!_isWalking)
        {
            _walkCycle = 0f;
            ResetPivots();
            return;
        }
        _walkCycle += Time.deltaTime * 10f;
        float swing = Mathf.Sin(_walkCycle) * 28f;
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-swing * 0.7f, 0f, 0f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(swing * 0.7f, 0f, 0f);
    }
    private void ResetPivots()
    {
        if (_hipL != null) _hipL.localRotation = Quaternion.identity;
        if (_hipR != null) _hipR.localRotation = Quaternion.identity;
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.identity;
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.identity;
    }
    private void Patrol()
    {
        if (_patrolPause > 0f)
        {
            _patrolPause -= Time.deltaTime;
            _isWalking = false;
            return;
        }

        if (!_hasPatrolTarget)
        {
            if (_patrolWaypoints.Count == 0)
                return;
            _target = _patrolWaypoints[_waypointIndex % _patrolWaypoints.Count];
            _hasPatrolTarget = true;
        }

        if (MoveTowards(_target, WALK_SPEED))
        {
            _hasPatrolTarget = false;
            _waypointIndex++;
            _patrolPause = Random.Range(1.2f, 3.2f);
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
        {
            _isWalking = false;
            return true;
        }

        _isWalking = true;
        _myTransform.position = Vector3.MoveTowards(_myTransform.position, dest, speed * Time.deltaTime);
        _myTransform.rotation = Quaternion.LookRotation(-to.normalized);
        return Vector3.Distance(_myTransform.position, dest) < 0.1f;
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
        _nameText.text = Localization.T("Cảnh Sát");
        _dialogQueue.Clear();
        bool discovered = RichManNPC.Instance != null && RichManNPC.Instance.Discovered;
        if (discovered)
        {
            _dialogQueue.Enqueue("Cậu tới đúng lúc. Chúng tôi đã nghi ngờ hắn từ lâu.");
            _dialogQueue.Enqueue("Những giao dịch ban đêm của hắn không lọt khỏi mắt chúng tôi.");
            _dialogQueue.Enqueue("Cảm ơn cậu. Đồng chí, vào việc thôi!");
        }
        else
        {
            _dialogQueue.Enqueue("Chào cậu. Công việc của tôi là giữ bình yên cho thôn này.");
            _dialogQueue.Enqueue("Nghe nói đêm đêm quanh dinh thự Phú Ông có kẻ lạ ra vào bí mật...");
            _dialogQueue.Enqueue("Nếu cậu thấy gì bất thường, hãy đến báo ngay cho đồn.");
        }
        Advance();
    }
    public void Advance()
    {
        if (_panel == null)
            return;
        if (_dialogQueue.Count == 0)
        {
            if (RichManNPC.Instance != null && RichManNPC.Instance.Discovered)
            {
                _panel.SetActive(false);
                _dialogActive = false;
                GameManager.Instance?.RequestJusticeEnding();
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
    public void Hide()
    {
        _dialogActive = false;
        if (_panel != null)
            _panel.SetActive(false);
        if (_myTransform != null)
            _myTransform.rotation = _originalRotation;
    }
    public void Retire()
    {
        Hide();
        if (gameObject != null)
            gameObject.SetActive(false);
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

        _panel = new GameObject("PoliceDialogPanel");
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

        _nameText = MakeText("PoliceDialogName", rt, new Vector2(0f, panelH * 0.36f),
            Localization.T("Cảnh Sát"), 24, new Color(0.45f, 0.75f, 1f),
            new Vector2(panelW - 40f, 34f));

        _dialogText = MakeText("PoliceDialogText", rt, new Vector2(0f, -panelH * 0.02f),
            "", 20, Color.white, new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = MakeText("PoliceDialogPrompt", rt, new Vector2(0f, -panelH * 0.36f),
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
GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
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
