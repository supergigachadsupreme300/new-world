using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class WifeNPC : MonoSingleton<WifeNPC>
{

    public enum WifeState { NotMet, Greeting, Married }

    public WifeState State = WifeState.NotMet;
    public bool Married;

    private GameObject _dialogPanel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private Canvas _canvas;
    private UIManager _uiManager;

    private readonly Queue<string> _dialogQueue = new Queue<string>();
    private bool _dialogActive;

    private float _affection;
    private int _lastWifeQuestDay;
    private TMP_Text _proposeText;
    private Image _loveFill;
    private TMP_Text _loveLabelText;
    private TMP_Text _loveValueText;
    private int _lastTalkDay = 1;
    private int _lastNeglectWarnDay = -1;
    private bool _ntrWarnShown;
    private const int NTR_DAYS = 3;
    private const float NTR_AFFECTION_THRESHOLD = 40f;

    private List<string> _wifeQuestNames = new List<string>();
    private List<string> _wifeQuestTargets = new List<string>();
    private List<int> _wifeQuestCounts = new List<int>();

    private static readonly string[][] WifeQuestPool = new string[][]
    {
        new string[] { "Ném Lúa Mì Cho Jessica", "donate_wheat", "5", "100" },
        new string[] { "Ném Cà Rốt Cho Jessica", "donate_carrot", "5", "90" },
        new string[] { "Ném Gỗ Cho Jessica", "donate_wood", "5", "110" },
        new string[] { "Ném Đá Cho Jessica", "donate_stone", "5", "110" },
        new string[] { "Ném Lồng Thú Cho Jessica", "donate_animal", "2", "150" },
        new string[] { "Tưới Nước Cho Cây", "water", "15", "70" },
        new string[] { "Câu Cá", "fish_catch", "3", "120" }
    };

    private Transform _npcTransform;
    private Transform _playerTransform;
    private bool _hasProposed;
    private bool _pendingHappyEnding;
    private bool _pendingFatedEnding;

    private enum HouseVisitState { None, WalkingToHouse, AtHome, Leaving }
    private HouseVisitState _visitState;
    private Vector3 _originalPos;
    private Quaternion _originalRot = Quaternion.identity;
    private readonly Vector3 _homePos = new Vector3(2.5f, 1f, 0f);
    private float _leaveTimer;
    private const float WALK_SPEED = 3f;
    private const float STAY_DURATION = 30f;
    private TMP_Text _inviteText;
    private TMP_Text _nightText;
    private int _chainStep;
    private bool _rosaryGranted;
    private bool _fishingBonusGranted;
    private int _lastRosaryGrantDay = -1;
    private Coroutine _walkRoutine;
    private GameObject _proposeRow;
    private GameObject _inviteRow;
    private GameObject _nightRow;
    private RectTransform _panelRt;

    private Transform _legL;
    private Transform _legR;
    private Transform _lowerLegL;
    private Transform _lowerLegR;
    private Transform _armL;
    private Transform _armR;
    private readonly Quaternion _armLBase = Quaternion.Euler(0f, 0f, -15f);
    private readonly Quaternion _armRBase = Quaternion.Euler(0f, 0f, 15f);
    private float _walkCycle;

    private Transform _bodyRoot;
    private Transform _irisL;
    private Transform _irisR;
    private Vector3 _irisLBase;
    private Vector3 _irisRBase;
    private Coroutine _eyeRoutine;
    private Coroutine _idleRoutine;
    private Coroutine _activityRoutine;
    private GameObject _heldProp;

    protected override void Awake()
    {
        base.Awake();
        _uiManager = GameManager.Instance?.UIManager;
        var npcGo = GameObject.Find("WifeNpc");
        if (npcGo != null) _npcTransform = npcGo.transform;
        if (_npcTransform != null)
            _originalPos = _npcTransform.position;
        else
            _originalPos = transform.position;
    }

    void Start()
    {
        RefreshWifeRefs();
        var playerGo = GameObject.Find("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
        if (_npcTransform != null)
        {
            _originalPos = _npcTransform.position;
            _originalRot = _npcTransform.rotation;
        }
        if (_eyeRoutine == null)
            _eyeRoutine = StartCoroutine(IdleEyeAnimation());
        if (_idleRoutine == null)
            _idleRoutine = StartCoroutine(WorldIdleRoutine());
        if (_activityRoutine == null)
            _activityRoutine = StartCoroutine(HomeActivityRoutine());
    }

    public void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        CreateDialogPanel();
    }

    public void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GamePaused)
            return;

        switch (_visitState)
        {
            case HouseVisitState.WalkingToHouse:
            {
                var npcPos = _npcTransform != null ? _npcTransform : transform;
                var step = WALK_SPEED * Time.deltaTime;
                npcPos.position = Vector3.MoveTowards(npcPos.position, _homePos, step);
                Vector3 walkDir = _homePos - npcPos.position;
                walkDir.y = 0f;
                if (walkDir.sqrMagnitude > 0.001f)
                    npcPos.rotation = Quaternion.LookRotation(-walkDir);
                if (Vector3.Distance(npcPos.position, _homePos) < 0.05f)
                {
                    npcPos.position = _homePos;
                    _visitState = HouseVisitState.AtHome;
                    if (_npcTransform != null) _npcTransform.rotation = _originalRot;
                    ResetPose();
                    _leaveTimer = STAY_DURATION;
                    if (!_dialogActive)
                    {
                        _dialogQueue.Clear();
                        _dialogQueue.Enqueue("Jessica: Em đến rồi! Nhà anh thật ấm cúng.");
                        _dialogQueue.Enqueue("Jessica: Em có thể ở lại một lát không?");
                        ShowNextDialog();
                    }
                }
                break;
            }
            case HouseVisitState.AtHome:
            {
                _leaveTimer -= Time.deltaTime;
                if (_leaveTimer <= 0f)
                {
                    _visitState = HouseVisitState.Leaving;
                    ResetPose();
                    if (!_dialogActive)
                    {
                        _dialogQueue.Clear();
                        _dialogQueue.Enqueue("Jessica: Em chán quá!");
                        ShowNextDialog();
                    }
                }
                break;
            }
            case HouseVisitState.Leaving:
            {
                var npcPos = _npcTransform != null ? _npcTransform : transform;
                var step = WALK_SPEED * Time.deltaTime;
                npcPos.position = Vector3.MoveTowards(npcPos.position, _originalPos, step);
                Vector3 walkDir = _originalPos - npcPos.position;
                walkDir.y = 0f;
                if (walkDir.sqrMagnitude > 0.001f)
                    npcPos.rotation = Quaternion.LookRotation(-walkDir);
                if (Vector3.Distance(npcPos.position, _originalPos) < 0.05f)
                {
                    npcPos.position = _originalPos;
                    _visitState = HouseVisitState.None;
                    if (_npcTransform != null) _npcTransform.rotation = _originalRot;
                    ResetPose();
                }
                break;
            }
        }

        if (_dialogActive &&
            ((Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) ||
             MobileInputController.Consume("propose")))
        {
            TryPropose();
        }

        if (_dialogActive &&
            ((Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
             MobileInputController.Consume("interact")))
        {
            if (_visitState == HouseVisitState.AtHome)
                _leaveTimer = STAY_DURATION;
            AdvanceDialog();
        }

        if (_dialogActive &&
            ((Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame) ||
             MobileInputController.Consume("night")))
        {
            HandleNightRequest();
        }
    }

    private void TryPropose()
    {
        bool showPropose = _affection >= 70f && !Married && State == WifeState.Greeting
            && (QuestManager.Instance?.IsComplete("greet") ?? false);
        if (showPropose && !_hasProposed)
        {
            _hasProposed = true;
            HideDialog();
            QuestManager.Instance?.AddStoryQuest(
                "Xây Dựng Dinh Thự Cho Jessica",
                "mansion", 25, 5000,
                "Jessica đồng ý lời tỏ tình! Hãy xây dinh thự để làm lễ cưới.");
            int mansionParts = WorldBuilder.Instance != null ? WorldBuilder.Instance.GetMansionCompletedParts() : 0;
            if (mansionParts > 0)
                QuestManager.Instance?.AddProgress("mansion", mansionParts);
            _dialogQueue.Clear();
            _dialogQueue.Enqueue("Jessica: Anh ơi... em thực sự rất bất ngờ!");
            _dialogQueue.Enqueue("Jessica: Em cũng có tình cảm với anh từ lâu rồi.");
            _dialogQueue.Enqueue("Jessica: Nếu anh xây xong dinh thự, chúng ta sẽ kết hôn!");
            _dialogQueue.Enqueue("Jessica: Em tin anh sẽ làm được. Yêu anh!");
            ShowNextDialog();
        }
    }

    private void HandleNightRequest()
    {
        bool greetDone = (QuestManager.Instance?.IsComplete("greet") ?? false);
        if (greetDone && State == WifeState.Greeting && IsRosaryAvailable())
        {
            _lastRosaryGrantDay = GameManager.Instance.CurrentDay;
            bool firstTime = !_rosaryGranted;
            _rosaryGranted = true;
            if (firstTime)
            {
                if (_chainStep <= 2)
                    _chainStep = 3;
                QuestManager.Instance?.AddStoryQuest("Trừ Tà Giúp Làng", "enemies", 5, 150,
                    "Dùng Tràng Hạt tiêu diệt 5 con quỷ để bảo vệ làng.");
                _dialogQueue.Clear();
                _dialogQueue.Enqueue("Jessica: Đêm nay ư? Anh có nghe tiếng lũ quỷ vào ban đêm chứ?");
                _dialogQueue.Enqueue("Jessica: Ở làng này, mỗi khi trời tối (18h \u2013 6h), lũ quỷ lại xuất hiện. Chúng phá hoại công trình và tấn công dân làng.");
                _dialogQueue.Enqueue("Jessica: Tràng Hạt là cách trừ tà tốt nhất \u2014 quả cầu thánh hạ gục kẻ thù chỉ một đòn.");
                _dialogQueue.Enqueue("Jessica: Nhớ đóng cửa khi trời tối để cản bước chúng nhé. Em tặng anh chiếc tràng hạt này!");
            }
            else
            {
                _dialogQueue.Clear();
                _dialogQueue.Enqueue("Jessica: Anh lại cần tràng hạt à? Em tặng anh thêm một chiếc nhé!");
            }
            HideDialog();
            ToolManager.Instance?.AddItem("rosary", 1);
            SaveState();
            ShowNextDialog();
        }
    }

    public bool IsDialogActive => _dialogActive;

    public Transform NpcTransform => _npcTransform;

    public void ApplyAffectionChange(float delta)
    {
        if (Married)
            return;
        _affection = Mathf.Clamp(_affection + delta, 0f, 100f);
        SaveState();
    }

    public void OnDayChanged()
    {
        if (Married || State == WifeState.NotMet)
            return;

        var gm = GameManager.Instance;
        if (gm == null)
            return;
        int today = gm.CurrentDay;
        int neglected = today - _lastTalkDay;

        if (neglected <= 0)
        {
            _ntrWarnShown = false;
            return;
        }

        _affection = Mathf.Max(0f, _affection - 3f * neglected);
        SaveState();

        if (_lastNeglectWarnDay == today)
            return;
        _lastNeglectWarnDay = today;

        if (neglected == 1)
        {
            gm.UIManager?.ShowMessage(Localization.T("Jessica: Anh dạo này bận quá... em nhớ anh."), 3f);
        }
        else if (neglected == 2)
        {
            gm.UIManager?.ShowMessage(Localization.T("Jessica: Em nghe nói ông chú giàu có kia cứ quanh quẩn gần nhà..."), 3f);
        }
        else if (neglected >= NTR_DAYS)
        {
            if (_affection <= NTR_AFFECTION_THRESHOLD)
            {
                gm.RequestNtrEnding();
            }
            else if (!_ntrWarnShown)
            {
                _ntrWarnShown = true;
                gm.UIManager?.ShowMessage(Localization.T("Jessica: Anh không còn quan tâm em nữa sao? Ông ta đã ngỏ lời mời em đi..."), 3f);
            }
        }
    }

    private void RefreshWifeRefs()
    {
        var npcGo = GameObject.Find("WifeNpc");
        if (npcGo == null)
            return;
        _npcTransform = npcGo.transform;
        _legL = _npcTransform.Find("LegsRoot/LegL");
        _legR = _npcTransform.Find("LegsRoot/LegR");
        _lowerLegL = _npcTransform.Find("LegsRoot/LegL/LowerLegL");
        _lowerLegR = _npcTransform.Find("LegsRoot/LegR/LowerLegR");
        _armL = _npcTransform.Find("LeftArmRoot");
        _armR = _npcTransform.Find("RightArmRoot");
        _bodyRoot = _npcTransform.Find("BodyRoot");
        _irisL = _npcTransform.Find("BodyRoot/EyeIrisL");
        _irisR = _npcTransform.Find("BodyRoot/EyeIrisR");
        if (_irisL != null) _irisLBase = _irisL.localPosition;
        if (_irisR != null) _irisRBase = _irisR.localPosition;
    }

    private IEnumerator WalkAnimation()
    {
        while (_visitState == HouseVisitState.WalkingToHouse || _visitState == HouseVisitState.Leaving)
        {
            _walkCycle += Time.deltaTime * 12f;
            ApplyWalkPose(_walkCycle);
            yield return null;
        }
    }

    private void ApplyWalkPose(float cycle)
    {
        float swing = Mathf.Sin(cycle) * 30f;
        if (_legL != null) _legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (_legR != null) _legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (_lowerLegL != null) _lowerLegL.localRotation = Quaternion.Euler(-swing * 0.5f, 0f, 0f);
        if (_lowerLegR != null) _lowerLegR.localRotation = Quaternion.Euler(swing * 0.5f, 0f, 0f);
        if (_armL != null) _armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f) * _armLBase;
        if (_armR != null) _armR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f) * _armRBase;
    }

    private void StopWalkAnimation()
    {
        if (_walkRoutine != null)
        {
            StopCoroutine(_walkRoutine);
            _walkRoutine = null;
        }
    }

    private void ResetPose()
    {
        StopWalkAnimation();
        DestroyHeldProp();
        if (_legL == null)
            return;
        _legL.localRotation = Quaternion.identity;
        _legR.localRotation = Quaternion.identity;
        _lowerLegL.localRotation = Quaternion.identity;
        _lowerLegR.localRotation = Quaternion.identity;
        if (_armL != null) _armL.localRotation = _armLBase;
        if (_armR != null) _armR.localRotation = _armRBase;
        if (_bodyRoot != null) _bodyRoot.localRotation = Quaternion.identity;
    }

    private IEnumerator IdleEyeAnimation()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(3f, 7f));
            if (GameManager.Instance == null || !GameManager.Instance.InGame)
                continue;
            if (_irisL == null || _irisR == null)
                continue;
            var offset = new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.012f, 0.012f));
            yield return StartCoroutine(MoveIris(offset, 0.25f));
            yield return new WaitForSeconds(Random.Range(0.3f, 1.1f));
            yield return StartCoroutine(MoveIris(Vector2.zero, 0.3f));
        }
    }

    private IEnumerator MoveIris(Vector2 offset, float duration)
    {
        if (_irisL == null || _irisR == null)
            yield break;
        float t = 0f;
        Vector3 fromL = _irisL.localPosition;
        Vector3 fromR = _irisR.localPosition;
        Vector3 toL = _irisLBase + new Vector3(offset.x, offset.y, 0f);
        Vector3 toR = _irisRBase + new Vector3(offset.x, offset.y, 0f);
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (_irisL != null) _irisL.localPosition = Vector3.Lerp(fromL, toL, k);
            if (_irisR != null) _irisR.localPosition = Vector3.Lerp(fromR, toR, k);
            yield return null;
        }
        if (_irisL != null) _irisL.localPosition = toL;
        if (_irisR != null) _irisR.localPosition = toR;
    }

    private IEnumerator WorldIdleRoutine()
    {
        while (true)
        {
            yield return null;
            if (_visitState != HouseVisitState.None)
                continue;
            if (GameManager.Instance == null || !GameManager.Instance.InGame)
                continue;
            if (_armL == null)
                continue;
            int pick = Random.Range(0, 3);
            if (pick == 0)
                yield return StartCoroutine(IdleStretch());
            else if (pick == 1)
                yield return StartCoroutine(IdleLookAround());
            else
                yield return StartCoroutine(IdleWeightShift());
            yield return new WaitForSeconds(Random.Range(6f, 12f));
        }
    }

    private IEnumerator IdleStretch()
    {
        float t = 0f, dur = 1.6f;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.None)
                yield break;
            t += Time.deltaTime;
            float k = Mathf.PingPong(t / dur * 2f, 1f);
            if (_armL != null) _armL.localRotation = Quaternion.Euler(0f, 0f, -150f * k) * _armLBase;
            if (_armR != null) _armR.localRotation = Quaternion.Euler(0f, 0f, 150f * k) * _armRBase;
            yield return null;
        }
        ResetPose();
    }

    private IEnumerator IdleLookAround()
    {
        if (_bodyRoot == null)
            yield break;
        float t = 0f, dur = 1.4f;
        float start = _bodyRoot.localRotation.eulerAngles.y;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.None)
                yield break;
            t += Time.deltaTime;
            float ang = Mathf.Sin(t / dur * Mathf.PI * 2f) * 20f;
            _bodyRoot.localRotation = Quaternion.Euler(0f, start + ang, 0f);
            yield return null;
        }
        if (_bodyRoot != null) _bodyRoot.localRotation = Quaternion.Euler(0f, start, 0f);
    }

    private IEnumerator IdleWeightShift()
    {
        if (_bodyRoot == null)
            yield break;
        float t = 0f, dur = 1.8f;
        float startY = _bodyRoot.localRotation.eulerAngles.y;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.None)
                yield break;
            t += Time.deltaTime;
            float sway = Mathf.Sin(t / dur * Mathf.PI * 2f) * 4f;
            _bodyRoot.localRotation = Quaternion.Euler(0f, startY + sway * 0.3f, sway);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(0f, 0f, 15f + sway * 3f);
            yield return null;
        }
        if (_bodyRoot != null) _bodyRoot.localRotation = Quaternion.Euler(0f, startY, 0f);
        if (_armR != null) _armR.localRotation = _armRBase;
    }

    private IEnumerator HomeActivityRoutine()
    {
        while (true)
        {
            yield return null;
            if (_visitState != HouseVisitState.AtHome)
                continue;
            if (GameManager.Instance == null || !GameManager.Instance.InGame)
                continue;
            if (_armL == null)
                continue;
            int pick = Random.Range(0, 10);
            if (pick < 5)
            {
                switch (pick)
                {
                    case 0: yield return StartCoroutine(ActivitySweep()); break;
                    case 1: yield return StartCoroutine(ActivityWipe()); break;
                    case 2: yield return StartCoroutine(ActivityRead()); break;
                    case 3: yield return StartCoroutine(ActivityDust()); break;
                    default: yield return StartCoroutine(ActivityLookAround()); break;
                }
            }
            else
            {
                yield return StartCoroutine(WanderAroundHouse());
            }
            yield return new WaitForSeconds(Random.Range(2f, 4f));
        }
    }

    private IEnumerator WanderAroundHouse()
    {
        var npc = _npcTransform != null ? _npcTransform : transform;
        if (npc == null)
            yield break;
        var waypoints = new Vector3[]
        {
            new Vector3(-2f, 1f, 1.5f),
            new Vector3(2.5f, 1f, 3.2f),
            new Vector3(-3f, 1f, 4f),
            new Vector3(4.2f, 1f, -2.5f),
            new Vector3(0f, 1f, -2f),
            new Vector3(0.5f, 1f, 4f),
        };
        int idx = Random.Range(0, waypoints.Length);
        int steps = Random.Range(1, 4);
        for (int s = 0; s < steps; s++)
        {
            if (_visitState != HouseVisitState.AtHome)
                yield break;
            Vector3 target = waypoints[idx];
            idx = (idx + Random.Range(1, waypoints.Length)) % waypoints.Length;
            while (Vector3.Distance(npc.position, target) > 0.05f)
            {
                if (_visitState != HouseVisitState.AtHome)
                    yield break;
                npc.position = Vector3.MoveTowards(npc.position, target, WALK_SPEED * Time.deltaTime);
                Vector3 dir = target - npc.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    npc.rotation = Quaternion.LookRotation(-dir);
                _walkCycle += Time.deltaTime * 12f;
                ApplyWalkPose(_walkCycle);
                yield return null;
            }
        }
        ResetPose();
        if (_visitState == HouseVisitState.AtHome && npc != null)
            npc.rotation = _originalRot;
    }

    private IEnumerator ActivitySweep()
    {
        SetHeldProp("Broom", new Vector3(0.05f, 0.75f, 0.05f), new Vector3(0f, -0.5f, 0.08f), _armR);
        float t = 0f, dur = 6f;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.AtHome)
                break;
            t += Time.deltaTime;
            float sweep = Mathf.Sin(t * 4.2f) * 32f;
            if (_armR != null) _armR.localRotation = Quaternion.Euler(0f, sweep, 10f);
            if (_armL != null) _armL.localRotation = Quaternion.Euler(0f, 0f, 40f);
            if (_bodyRoot != null) _bodyRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 4.2f) * 3f);
            yield return null;
        }
        DestroyHeldProp();
        ResetPose();
    }

    private IEnumerator ActivityWipe()
    {
        float t = 0f, dur = 5f;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.AtHome)
                break;
            t += Time.deltaTime;
            float a = t * 3.6f;
            if (_armR != null) _armR.localRotation = Quaternion.Euler(Mathf.Sin(a) * 25f, 20f, 40f + Mathf.Cos(a) * 20f);
            if (_armL != null) _armL.localRotation = Quaternion.Euler(0f, 0f, 35f);
            if (_bodyRoot != null) _bodyRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(a) * 2f);
            yield return null;
        }
        ResetPose();
    }

    private IEnumerator ActivityRead()
    {
        SetHeldProp("Book", new Vector3(0.14f, 0.02f, 0.1f), new Vector3(0f, -0.42f, 0.02f), _armL);
        float t = 0f, dur = 6f;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.AtHome)
                break;
            t += Time.deltaTime;
            float sway = Mathf.Sin(t * 1.6f) * 3f;
            if (_armL != null) _armL.localRotation = Quaternion.Euler(0f, 40f, -10f + sway);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(0f, -40f, 10f - sway);
            yield return null;
        }
        DestroyHeldProp();
        ResetPose();
    }

    private IEnumerator ActivityDust()
    {
        float t = 0f, dur = 5f;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.AtHome)
                break;
            t += Time.deltaTime;
            float up = Mathf.Clamp01(Mathf.Sin(t * 2.2f));
            if (_armL != null) _armL.localRotation = Quaternion.Euler(-90f * up, 30f, -15f);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(0f, 0f, 40f);
            if (_bodyRoot != null) _bodyRoot.localRotation = Quaternion.Euler(0f, 0f, -5f * up);
            yield return null;
        }
        ResetPose();
    }

    private IEnumerator ActivityLookAround()
    {
        if (_bodyRoot == null)
            yield break;
        float t = 0f, dur = 3.4f;
        float startY = _bodyRoot.localRotation.eulerAngles.y;
        while (t < dur)
        {
            if (_visitState != HouseVisitState.AtHome)
                break;
            t += Time.deltaTime;
            float ang = Mathf.Sin(t / dur * Mathf.PI * 2f) * 22f;
            _bodyRoot.localRotation = Quaternion.Euler(0f, startY + ang, 0f);
            yield return null;
        }
        if (_bodyRoot != null) _bodyRoot.localRotation = Quaternion.Euler(0f, startY, 0f);
        ResetPose();
    }

    private void SetHeldProp(string name, Vector3 scale, Vector3 pos, Transform parent)
    {
        DestroyHeldProp();
        _heldProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _heldProp.name = name;
        _heldProp.transform.SetParent(parent);
        _heldProp.transform.localPosition = pos;
        _heldProp.transform.localScale = scale;
        Destroy(_heldProp.GetComponent<Collider>());
    }

    private void DestroyHeldProp()
    {
        if (_heldProp != null)
        {
            Destroy(_heldProp);
            _heldProp = null;
        }
    }

    public void Interact()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame)
            return;

        _lastTalkDay = GameManager.Instance.CurrentDay;
        _ntrWarnShown = false;

        CheckExpiredQuests();

        if (_dialogActive)
        {
            if (_visitState == HouseVisitState.AtHome)
            {
                _leaveTimer = STAY_DURATION;
            }
            AdvanceDialog();
            return;
        }

        if (_visitState != HouseVisitState.None)
        {
            _dialogQueue.Clear();
            _dialogQueue.Enqueue("Jessica: Anh gọi em à?");
            _dialogQueue.Enqueue("Jessica: Em thích ở bên anh thế này.");
            ShowNextDialog();
            return;
        }

        var lines = GetDialogLines();
        if (lines == null || lines.Length == 0)
            return;

        foreach (var line in lines)
            _dialogQueue.Enqueue(line);

        ShowNextDialog();
    }

    public void SaveState()
    {
        PlayerPrefs.SetString("WifeNPC", SerializeState());
        PlayerPrefs.Save();
    }

    public string SerializeState()
    {
        var data = new WifeSaveData
        {
            state = (int)State,
            married = Married,
            affection = _affection,
            lastWifeQuestDay = _lastWifeQuestDay,
            hasProposed = _hasProposed,
            chainStep = _chainStep,
            rosaryGranted = _rosaryGranted,
            fishingBonusGranted = _fishingBonusGranted,
            lastRosaryGrantDay = _lastRosaryGrantDay,
            lastTalkDay = _lastTalkDay
        };
        data.wifeQuestNames = _wifeQuestNames;
        data.wifeQuestTargets = _wifeQuestTargets;
        data.wifeQuestCounts = _wifeQuestCounts;
        return JsonUtility.ToJson(data);
    }

    public void LoadState()
    {
        DeserializeState(PlayerPrefs.GetString("WifeNPC", ""));
    }

    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        var data = JsonUtility.FromJson<WifeSaveData>(json);
        if (data != null)
        {
            State = (WifeState)data.state;
            Married = data.married;
            _affection = data.affection;
            _lastWifeQuestDay = data.lastWifeQuestDay;
            _hasProposed = data.hasProposed;
            _chainStep = data.chainStep;
            _rosaryGranted = data.rosaryGranted;
            _fishingBonusGranted = data.fishingBonusGranted;
            _lastRosaryGrantDay = data.lastRosaryGrantDay;
            _lastTalkDay = data.lastTalkDay > 0 ? data.lastTalkDay : 1;
            if (!_rosaryGranted)
                _lastRosaryGrantDay = -1;
            if (data.wifeQuestNames != null)
                _wifeQuestNames = data.wifeQuestNames;
            if (data.wifeQuestTargets != null)
                _wifeQuestTargets = data.wifeQuestTargets;
            if (data.wifeQuestCounts != null)
                _wifeQuestCounts = data.wifeQuestCounts;
            if (_wifeQuestTargets.Count == 0)
                _lastWifeQuestDay = 0;
        }
    }

    public void InviteToHouse()
    {
        if (_visitState != HouseVisitState.None || State == WifeState.NotMet)
            return;

        RefreshWifeRefs();
        _visitState = HouseVisitState.WalkingToHouse;
        _walkCycle = 0f;
        if (_walkRoutine == null)
            _walkRoutine = StartCoroutine(WalkAnimation());
        _dialogQueue.Clear();
        _dialogQueue.Enqueue("Jessica: Ok con dê!");
        ShowNextDialog();
    }

    private string[] GetDialogLines()
    {
        if (Married)
        {
            return new string[]
            {
                "Chồng yêu ơi! Hôm nay mình hạnh phúc lắm nhé.",
                "Em luôn ở bên anh, dù nông trại có bận rộn đến đâu.",
                "Cảm ơn anh đã xây dựng dinh thự cho mình. Em yêu anh!"
            };
        }

        bool mansionComplete = IsMansionComplete();

        if (mansionComplete)
        {
            State = WifeState.Married;
            Married = true;
            SaveState();
            var qm = QuestManager.Instance;
            bool fated = qm != null && !qm.IsComplete("boss_kill") && !qm.IsComplete("mansion_secret");
            if (fated)
                _pendingFatedEnding = true;
            else
                _pendingHappyEnding = true;
            if (RichManNPC.Instance != null)
                RichManNPC.Instance.Retire();
            return new string[]
            {
                "Jessica: Anh ơi... dinh thự đã hoàn thành rồi!",
                "Jessica: Em rất hạnh phúc. Em không ngờ anh làm được đến vậy.",
                "Jessica: Nếu anh muốn... mình có thể kết hôn. Em đồng ý!",
                "Jessica: Từ giờ, em sẽ mãi bên anh. Cảm ơn anh nhé!"
            };
        }

        switch (State)
        {
            case WifeState.NotMet:
                State = WifeState.Greeting;
                SaveState();
                return new string[]
                {
                    "Jessica: Chào anh! Em là Jessica, cô gái hàng xóm.",
                    "Jessica: Nghe nói anh về nông thôn sống... Hy vọng mình sẽ là hàng xóm tốt nhé!",
                    "Jessica: Nhà em ở bên kia, anh cứ qua chơi bất cứ lúc nào."
                };

            case WifeState.Greeting:
            {
                var qm = QuestManager.Instance;
                bool greetDone = qm != null && qm.IsComplete("greet");

                if (!greetDone)
                {
                    return new string[]
                    {
                        "Jessica: Chào anh! Hôm nay trông anh có vẻ tốt lắm.",
                        "Jessica: Em luôn ở đây nếu anh cần gì nhé."
                    };
                }

                UpdateChain(qm);

                if (_wifeQuestTargets.Count == 0 && NeedGenerateWifeQuests())
                    GenerateWifeQuests();

                if (_wifeQuestTargets.Count > 0)
                {
                    CheckWifeQuests(qm);
                }

                var lines = new List<string>();

                if (_wifeQuestTargets.Count > 0)
                {
                    lines.Add("Jessica: Lại đây anh ơi! Em có vài việc nhờ anh giúp.");
                    lines.AddRange(GetWifeQuestDescriptions());
                    lines.Add("Jessica: Giúp em xong em cảm ơn nhiều lắm!");
                    return lines.ToArray();
                }

                if (lines.Count > 0)
                    return lines.ToArray();

                return new string[]
                {
                    "Jessica: Chào anh! Hôm nay anh có khỏe không?",
                    "Jessica: Em cảm ơn anh đã luôn quan tâm nhé!"
                };
            }

            default:
                return new string[]
                {
                    "Jessica: Chào anh!"
                };
        }
    }

    private bool IsMansionComplete()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return false;
        return wb.GetMansionCompletedParts() >= 25;
    }

    private bool NeedGenerateWifeQuests()
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        int today = gm.CurrentDay;
        return today != _lastWifeQuestDay;
    }

    private void GenerateWifeQuests()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        _lastWifeQuestDay = gm.CurrentDay;

        _wifeQuestNames.Clear();
        _wifeQuestTargets.Clear();
        _wifeQuestCounts.Clear();

        int pick = Random.Range(0, WifeQuestPool.Length);
        var entry = WifeQuestPool[pick];
        string dayLabel = $" [Ngày {_lastWifeQuestDay}]";
        string questName = entry[0] + dayLabel;

        _wifeQuestNames.Add(questName);
        _wifeQuestTargets.Add(entry[1]);
        _wifeQuestCounts.Add(int.Parse(entry[2]));
        int reward = int.Parse(entry[3]);

        QuestManager.Instance?.AddStoryQuest(questName, entry[1], int.Parse(entry[2]), reward,
            "Nhiệm vụ hàng ngày từ Jessica. Hoàn thành trước 6h sáng mai!");
    }

    private void UpdateChain(QuestManager qm)
    {
        if (qm == null)
            return;

        if (_chainStep == 3 && qm.IsNamedQuestComplete("Trừ Tà Giúp Làng"))
        {
            _chainStep = 4;
            _affection = Mathf.Min(100f, _affection + 10f);
            KarmaManager.Instance?.AddMaxKarma(1f);
            _uiManager?.ShowMessage(Localization.T("Hoàn thành Trừ Tà Giúp Làng! +10 độ thân mật, +1 Max Karma"), 3f);
            SaveState();
        }
    }

    private void CheckWifeQuests(QuestManager qm)
    {
        for (int i = _wifeQuestTargets.Count - 1; i >= 0; i--)
        {
            if (qm.IsNamedQuestComplete(_wifeQuestNames[i]))
            {
                _affection = Mathf.Min(100f, _affection + 10f);
                KarmaManager.Instance?.AddMaxKarma(1f);
                _uiManager?.ShowMessage(Localization.T("Hoàn thành nhiệm vụ từ Jessica! +1 Max Karma"), 3f);
                _wifeQuestNames.RemoveAt(i);
                _wifeQuestTargets.RemoveAt(i);
                _wifeQuestCounts.RemoveAt(i);
                SaveState();
            }
        }
    }

    public string GetActiveMaterialName()
    {
        for (int i = 0; i < _wifeQuestTargets.Count; i++)
        {
            if (_wifeQuestTargets[i].StartsWith("donate_"))
                return _wifeQuestTargets[i].Substring("donate_".Length);
        }
        return null;
    }

    public bool TryDepositMaterial(string material, out int progress, out int count)
    {
        progress = 0;
        count = 0;
        if (string.IsNullOrEmpty(material))
            return false;

        string expected = "donate_" + material;
        var qm = QuestManager.Instance;
        if (qm == null)
            return false;

        for (int i = 0; i < _wifeQuestTargets.Count; i++)
        {
            if (_wifeQuestTargets[i] != expected)
                continue;
            qm.AddProgress(expected, 1);
            progress = qm.GetNamedQuestProgress(_wifeQuestNames[i]);
            count = _wifeQuestCounts[i];
            return true;
        }
        return false;
    }

    public void ResetForNewGame()
    {
        State = WifeState.NotMet;
        Married = false;
        _affection = 0f;
        _lastWifeQuestDay = 0;
        _hasProposed = false;
        _chainStep = 0;
        _rosaryGranted = false;
        _fishingBonusGranted = false;
        _lastRosaryGrantDay = -1;
        _lastTalkDay = 1;
        _wifeQuestNames.Clear();
        _wifeQuestTargets.Clear();
        _wifeQuestCounts.Clear();
    }

    private List<string> GetWifeQuestDescriptions()
    {
        var lines = new List<string>();
        for (int i = 0; i < _wifeQuestTargets.Count; i++)
        {
            var qm = QuestManager.Instance;
            int prog = qm != null ? qm.GetNamedQuestProgress(_wifeQuestNames[i]) : 0;
            int need = _wifeQuestCounts[i];
            lines.Add("- " + Localization.QuestName(_wifeQuestNames[i]) + ": " + prog + "/" + need);
        }
        return lines;
    }

    private void CheckExpiredQuests()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int today = gm.CurrentDay;
        if (today != _lastWifeQuestDay && _wifeQuestTargets.Count > 0)
        {
            _affection = Mathf.Max(0f, _affection - 5f * _wifeQuestTargets.Count);
            for (int i = 0; i < _wifeQuestNames.Count; i++)
                QuestManager.Instance?.RemoveStoryQuest(_wifeQuestNames[i]);
            _wifeQuestNames.Clear();
            _wifeQuestTargets.Clear();
            _wifeQuestCounts.Clear();
            SaveState();
        }
    }

    private void ShowNextDialog()
    {
        if (_dialogQueue.Count == 0)
        {
            HideDialog();
            return;
        }

        _dialogActive = true;
        _dialogPanel.SetActive(true);

        FacePlayer();

        string line = _dialogQueue.Dequeue();

        if (line.StartsWith("Jessica: "))
        {
            _nameText.text = "Jessica";
            _dialogText.text = Localization.T(line.Substring("Jessica: ".Length));
        }
        else
        {
            _nameText.text = "";
            _dialogText.text = Localization.T(line);
        }

        UpdateLoveMeter();

        bool mobile = GameInput.IsMobile;
        _promptText.text = _dialogQueue.Count > 0
            ? (mobile ? Localization.T("Chạm để tiếp tục") : Localization.T("Nhấn E để tiếp tục"))
            : (mobile ? Localization.T("Chạm để đóng") : Localization.T("Nhấn E để đóng"));

        bool showPropose = _affection >= 70f && !Married && State == WifeState.Greeting
            && (QuestManager.Instance?.IsComplete("greet") ?? false);
        _proposeText.text = showPropose ? (mobile ? Localization.T("[Tỏ Tình] (Chạm)") : Localization.T("[Tỏ Tình] Nhấn T")) : "";
        if (_proposeRow != null) _proposeRow.SetActive(showPropose);

        bool showInvite = _visitState == HouseVisitState.None && State != WifeState.NotMet
            && (QuestManager.Instance?.IsComplete("greet") ?? false);
        bool showInviteFinal = showInvite && !showPropose;
        _inviteText.text = showInviteFinal ? (mobile ? Localization.T("[Mời Về Nhà] (Chạm)") : Localization.T("[Mời Về Nhà] Nhấn G")) : "";
        if (_inviteRow != null) _inviteRow.SetActive(showInviteFinal);

        bool showNight = State == WifeState.Greeting && IsRosaryAvailable()
            && (QuestManager.Instance?.IsComplete("greet") ?? false);
        _nightText.text = showNight ? (mobile ? Localization.T("[Hỏi Về Đêm] (Chạm)") : Localization.T("[Hỏi Về Đêm] Nhấn V")) : "";
        if (_nightRow != null) _nightRow.SetActive(showNight);

        bool anyOption = showPropose || showInviteFinal || showNight;
        if (_promptText != null) _promptText.gameObject.SetActive(!anyOption);

        LayoutOptionRows();
    }

    private void UpdateLoveMeter()
    {
        if (_loveFill == null)
            return;

        float display = Married ? 100f : Mathf.Clamp(_affection, 0f, 100f);
        float fraction = Mathf.Clamp01(display / 100f);
        var fillRt = _loveFill.rectTransform;
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, fraction);
        fillRt.sizeDelta = Vector2.zero;

        if (_loveLabelText != null)
            _loveLabelText.text = Localization.T("Độ Thân Mật");

        if (_loveValueText != null)
            _loveValueText.text = Mathf.RoundToInt(display).ToString() + "/100";
    }

    private void LayoutOptionRows()
    {
        if (_panelRt == null)
            return;

        var active = new List<GameObject>();
        if (_proposeRow != null && _proposeRow.activeSelf) active.Add(_proposeRow);
        if (_inviteRow != null && _inviteRow.activeSelf) active.Add(_inviteRow);
        if (_nightRow != null && _nightRow.activeSelf) active.Add(_nightRow);

        float panelH = _panelRt.rect.height;
        const float topPad = 6f;
        const float step = 38f;
        for (int i = 0; i < active.Count; i++)
        {
            var rowRt = active[i].GetComponent<RectTransform>();
            if (rowRt == null) continue;
            rowRt.anchoredPosition = new Vector2(-20f, panelH - topPad - (i + 1) * step);
        }
    }

    private bool IsRosaryAvailable()
    {
        var gm = GameManager.Instance;
        return gm != null && _lastRosaryGrantDay != gm.CurrentDay;
    }

    private void AdvanceDialog()
    {
        if (_dialogQueue.Count > 0)
        {
            ShowNextDialog();
        }
        else
        {
            HideDialog();
        }
    }

    public void HideDialog(bool resetQueue = false)
    {
        _dialogActive = false;
        if (resetQueue)
            _dialogQueue.Clear();
        _dialogPanel.SetActive(false);
        var npcPos = _npcTransform != null ? _npcTransform : transform;
        if (npcPos != null)
            npcPos.rotation = _originalRot;
        if (_pendingFatedEnding)
        {
            _pendingFatedEnding = false;
            var cm = FindFirstObjectByType<CutsceneManager>();
            if (cm != null) cm.RequestFatedEnding();
        }
        else if (_pendingHappyEnding)
        {
            _pendingHappyEnding = false;
            var cm = FindFirstObjectByType<CutsceneManager>();
            if (cm != null) cm.RequestHappyEnding();
        }
    }

    private void FacePlayer()
    {
        var npcPos = _npcTransform != null ? _npcTransform : transform;
        if (npcPos == null || _playerTransform == null)
            return;
        Vector3 to = npcPos.position - _playerTransform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            npcPos.rotation = Quaternion.LookRotation(to.normalized);
    }

    private void CreateDialogPanel()
    {
        if (_canvas == null)
            _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null) return;

        float sw = Screen.width;
        float sh = Screen.height;

        _dialogPanel = new GameObject("WifeDialogPanel");
        _dialogPanel.transform.SetParent(_canvas.transform, false);

        var panelRt = _dialogPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        panelRt.sizeDelta = new Vector2(sw * 0.7f, sh * 0.28f);

        var panelImg = _dialogPanel.AddComponent<Image>();
        panelImg.color = ColorPalette.UIBackdrop;

        var panelBtn = _dialogPanel.AddComponent<Button>();
        panelBtn.targetGraphic = panelImg;
        panelBtn.onClick.AddListener(AdvanceDialog);

        float panelW = sw * 0.7f;
        float panelH = sh * 0.28f;
        _panelRt = panelRt;

        _nameText = CreateDialogText("WifeDialogName", panelRt,
            new Vector2(70f, panelH * 0.38f), "Jessica", 24,
            new Color(0.9f, 0.6f, 0.8f), TextAlignmentOptions.Left,
            new Vector2(panelW - 160f, 35f));

        CreateLoveMeter(panelRt, panelW);

        _dialogText = CreateDialogText("WifeDialogText", panelRt,
            new Vector2(70f, -panelH * 0.02f), "", 20,
            Color.white, TextAlignmentOptions.Left,
            new Vector2(panelW - 160f, panelH * 0.55f));

        _promptText = CreateDialogText("WifeDialogPrompt", panelRt,
            new Vector2(70f, -panelH * 0.38f), Localization.T("Nhấn E để tiếp tục"), 16,
            new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Right,
            new Vector2(panelW - 160f, 25f));

        _proposeText = CreateDialogOptionRow(panelRt, "WifeProposeRow", "WifeProposeText",
            58f, new Color(1f, 0.3f, 0.3f), 22, out _proposeRow, TryPropose);

        _inviteText = CreateDialogOptionRow(panelRt, "WifeInviteRow", "WifeInviteText",
            10f, new Color(0.3f, 1f, 0.6f), 20, out _inviteRow, InviteToHouse);

        _nightText = CreateDialogOptionRow(panelRt, "WifeNightRow", "WifeNightText",
            -38f, new Color(1f, 0.85f, 0.3f), 20, out _nightRow, HandleNightRequest);

        _dialogPanel.SetActive(false);
    }

    private void CreateLoveMeter(RectTransform parent, float panelW)
    {
        float panelH = parent.rect.height;
        float barW = 24f;
        float barH = Mathf.Clamp(panelH * 0.6f, 90f, 170f);
        float sideW = 84f;

        var rowRt = new GameObject("WifeLoveRow").AddComponent<RectTransform>();
        rowRt.transform.SetParent(parent, false);
        rowRt.anchorMin = new Vector2(0f, 0.5f);
        rowRt.anchorMax = new Vector2(0f, 0.5f);
        rowRt.pivot = new Vector2(0f, 0.5f);
        rowRt.anchoredPosition = new Vector2(8f, 4f);
        rowRt.sizeDelta = new Vector2(barW + 12f + sideW, barH);

        var rowBg = rowRt.gameObject.AddComponent<Image>();
        rowBg.color = ColorPalette.UIBackdrop;
        rowBg.raycastTarget = false;

        var barRt = new GameObject("WifeLoveBar").AddComponent<RectTransform>();
        barRt.transform.SetParent(rowRt, false);
        barRt.anchorMin = new Vector2(0f, 0.5f);
        barRt.anchorMax = new Vector2(0f, 0.5f);
        barRt.pivot = new Vector2(0f, 0.5f);
        barRt.anchoredPosition = new Vector2(4f, 0f);
        barRt.sizeDelta = new Vector2(barW, barH);

        var bg = barRt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        bg.raycastTarget = false;

        var fillGo = new GameObject("WifeLoveFill");
        fillGo.transform.SetParent(barRt, false);
        _loveFill = fillGo.AddComponent<Image>();
        _loveFill.color = new Color(1f, 0.42f, 0.54f, 1f);
        _loveFill.raycastTarget = false;
        var fillRt = _loveFill.rectTransform;
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 0f);
        fillRt.sizeDelta = Vector2.zero;

        _loveLabelText = CreateDialogText("WifeLoveLabel", rowRt,
            new Vector2(barW + 16f, barH * 0.26f), Localization.T("Độ Thân Mật"), 13,
            new Color(1f, 0.42f, 0.54f), TextAlignmentOptions.Left,
            new Vector2(sideW, 18f));

        _loveValueText = CreateDialogText("WifeLoveValue", rowRt,
            new Vector2(barW + 16f, -barH * 0.26f), "0/100", 13,
            new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.Left,
            new Vector2(sideW, 18f));
    }

    private TMP_Text CreateDialogOptionRow(RectTransform parent, string rowName, string textName,
        float yOffset, Color textColor, int fontSize, out GameObject row,
        UnityEngine.Events.UnityAction onClick = null)
    {
        row = new GameObject(rowName);
        row.transform.SetParent(parent, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(1f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(1f, 0f);
        rowRt.anchoredPosition = new Vector2(-20f, yOffset);
        rowRt.sizeDelta = new Vector2(300f, 40f);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = ColorPalette.UIBackdrop;
        rowImg.raycastTarget = true;

        if (onClick != null)
        {
            var rowBtn = row.AddComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            rowBtn.onClick.AddListener(onClick);
        }

        var text = CreateDialogText(textName, rowRt,
            new Vector2(0f, 0f), "", fontSize, textColor,
            TextAlignmentOptions.Left, new Vector2(272f, 36f));

        row.SetActive(false);
        return text;
    }

    private TMP_Text CreateDialogText(string name, RectTransform parent,
        Vector2 position, string text, int fontSize, Color color,
        TextAlignmentOptions alignment, Vector2 size)
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
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        return tmp;
    }

    public static GameObject BuildWifeNpc(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("WifeNpc");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;
        root.transform.localScale = Vector3.one * scale;

        Color skinC     = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color bootC     = new Color(0.15f, 0.08f, 0.2f);
        Color bootTrimC = new Color(0.35f, 0.18f, 0.42f);
        Color skirtC    = new Color(0.14f, 0.11f, 0.28f);
        Color topC      = new Color(0.92f, 0.9f, 0.95f);
        Color hairC     = new Color(0.95f, 0.85f, 0.55f);
        Color eyeC      = new Color(0.3f, 0.6f, 1f);
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);

        // ═══ LEGS ROOT ═══
        var legsRoot = new GameObject("LegsRoot");
        legsRoot.transform.SetParent(root.transform);
        legsRoot.transform.localPosition = Vector3.zero;
        legsRoot.transform.localRotation = Quaternion.identity;
        legsRoot.transform.localScale = Vector3.one;

        // Left leg
        var legL = new GameObject("LegL");
        legL.transform.SetParent(legsRoot.transform);
        legL.transform.localPosition = new Vector3(-0.12f, -0.3f, 0f);
        MakeBlock("UpperLegL", legL.transform, new Vector3(0.1f, 0.18f, 0.1f), new Vector3(0f, 0.05f, 0f), skinC, true);
        var lowerLegL = new GameObject("LowerLegL");
        lowerLegL.transform.SetParent(legL.transform);
        lowerLegL.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        MakeBlock("LowerLegBlockL", lowerLegL.transform, new Vector3(0.1f, 0.05f, 0.1f), new Vector3(0f, 0.015f, 0f), skinC, true);
        MakeBlock("CuffL",  lowerLegL.transform, new Vector3(0.16f, 0.05f, 0.22f), new Vector3(0f, -0.04f, 0f), bootTrimC, true);
        MakeBlock("BootL",  lowerLegL.transform, new Vector3(0.14f, 0.38f, 0.2f),  new Vector3(0f, -0.25f, 0f), bootC, true);
        MakeBlock("HeelL",  lowerLegL.transform, new Vector3(0.06f, 0.08f, 0.1f),  new Vector3(0f, -0.48f, 0.04f), bootC, true);
        MakeBlock("SoleL",  lowerLegL.transform, new Vector3(0.15f, 0.04f, 0.24f), new Vector3(0f, -0.46f, 0f), bootC, true);

        // Right leg
        var legR = new GameObject("LegR");
        legR.transform.SetParent(legsRoot.transform);
        legR.transform.localPosition = new Vector3(0.12f, -0.3f, 0f);
        MakeBlock("UpperLegR", legR.transform, new Vector3(0.1f, 0.18f, 0.1f), new Vector3(0f, 0.05f, 0f), skinC, true);
        var lowerLegR = new GameObject("LowerLegR");
        lowerLegR.transform.SetParent(legR.transform);
        lowerLegR.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        MakeBlock("LowerLegBlockR", lowerLegR.transform, new Vector3(0.1f, 0.05f, 0.1f), new Vector3(0f, 0.015f, 0f), skinC, true);
        MakeBlock("CuffR",  lowerLegR.transform, new Vector3(0.16f, 0.05f, 0.22f), new Vector3(0f, -0.04f, 0f), bootTrimC, true);
        MakeBlock("BootR",  lowerLegR.transform, new Vector3(0.14f, 0.38f, 0.2f),  new Vector3(0f, -0.25f, 0f), bootC, true);
        MakeBlock("HeelR",  lowerLegR.transform, new Vector3(0.06f, 0.08f, 0.1f),  new Vector3(0f, -0.48f, 0.04f), bootC, true);
        MakeBlock("SoleR",  lowerLegR.transform, new Vector3(0.15f, 0.04f, 0.24f), new Vector3(0f, -0.46f, 0f), bootC, true);

        // ═══ BODY ROOT ═══
        var bodyRoot = new GameObject("BodyRoot");
        bodyRoot.transform.SetParent(root.transform);
        bodyRoot.transform.localPosition = Vector3.zero;
        bodyRoot.transform.localRotation = Quaternion.identity;
        bodyRoot.transform.localScale = Vector3.one;

        MakeBlock("Skirt",     bodyRoot.transform, new Vector3(0.48f, 0.26f, 0.32f), new Vector3(0f, -0.07f, 0f), skirtC, true);
        MakeBlock("SkirtHem",  bodyRoot.transform, new Vector3(0.56f, 0.05f, 0.38f), new Vector3(0f, -0.21f, 0f), skirtC, true);
        MakeBlock("SkirtBelt", bodyRoot.transform, new Vector3(0.4f, 0.04f, 0.28f),  new Vector3(0f, 0.07f, 0f), bootTrimC, true);
        MakeBlock("Topwaist",  bodyRoot.transform, new Vector3(0.28f, 0.12f, 0.16f), new Vector3(0f, 0.1f, 0f), topC, true);
        MakeBlock("TopLower",  bodyRoot.transform, new Vector3(0.34f, 0.12f, 0.22f), new Vector3(0f, 0.2f, 0f), topC, true);
        MakeBlock("TopUpper",  bodyRoot.transform, new Vector3(0.42f, 0.18f, 0.28f), new Vector3(0f, 0.35f, 0f), topC, true);
        MakeBlock("TopCollar", bodyRoot.transform, new Vector3(0.18f, 0.05f, 0.14f), new Vector3(0f, 0.47f, -0.02f), bootTrimC, true);
        MakeBlock("Neck", bodyRoot.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.5f, 0f), skinC, true);
        MakeBlock("Head", bodyRoot.transform, new Vector3(0.34f, 0.3f, 0.32f), new Vector3(0f, 0.7f, 0f), skinC, true);
        MakeBlock("HairTop",  bodyRoot.transform, new Vector3(0.38f, 0.08f, 0.36f), new Vector3(0f, 0.86f, 0f), hairC, true);
        MakeBlock("HairBack", bodyRoot.transform, new Vector3(0.3f, 0.4f, 0.12f),  new Vector3(0f, 0.65f, 0.26f), hairC, true);
        MakeBlock("HairL",    bodyRoot.transform, new Vector3(0.42f, 0.375f, 0.12f), new Vector3(-0.22f, 0.65f, 0f), hairC, true, Quaternion.Euler(0f, 90f, 0f));
        MakeBlock("HairR",    bodyRoot.transform, new Vector3(0.42f, 0.375f, 0.12f), new Vector3(0.22f, 0.65f, 0f), hairC, true, Quaternion.Euler(0f, 90f, 0f));
        MakeBlock("Braid1",   bodyRoot.transform, new Vector3(0.24f, 0.14f, 0.12f), new Vector3(0f,       0.40f, 0.26f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid2",   bodyRoot.transform, new Vector3(0.20f, 0.12f, 0.1f),  new Vector3(0.02f,   0.32f, 0.27f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid3",   bodyRoot.transform, new Vector3(0.17f, 0.1f,  0.09f), new Vector3(-0.02f,  0.24f, 0.28f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid4",   bodyRoot.transform, new Vector3(0.14f, 0.09f, 0.07f), new Vector3(0.015f,  0.16f, 0.28f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid5",   bodyRoot.transform, new Vector3(0.12f, 0.07f, 0.06f), new Vector3(-0.01f,  0.08f, 0.27f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid6",   bodyRoot.transform, new Vector3(0.09f, 0.06f, 0.05f), new Vector3(0.005f,  0.02f, 0.26f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("BraidEnd", bodyRoot.transform, new Vector3(0.07f, 0.05f, 0.04f), new Vector3(0f,     -0.03f, 0.26f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeWhiteL", bodyRoot.transform, new Vector3(0.1f, 0.08f, 0.03f), new Vector3(-0.09f, 0.74f, -0.165f), eyeWhiteC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeWhiteR", bodyRoot.transform, new Vector3(0.1f, 0.08f, 0.03f), new Vector3(0.09f, 0.74f, -0.165f), eyeWhiteC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeIrisL",  bodyRoot.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(-0.09f, 0.73f, -0.178f), eyeC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeIrisR",  bodyRoot.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0.09f, 0.73f, -0.178f), eyeC, true, Quaternion.Euler(0f, 0f, 0f));

        // ═══ LEFT ARM ROOT ═══
        var leftArmRoot = new GameObject("LeftArmRoot");
        leftArmRoot.transform.SetParent(root.transform);
        leftArmRoot.transform.localPosition = new Vector3(-0.29f, 0.33f, 0f);
        leftArmRoot.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
        leftArmRoot.transform.localScale = Vector3.one;

        MakeBlock("UpperArmL",   leftArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, 0f, 0f), skinC, true);
        MakeBlock("SleeveL",     leftArmRoot.transform, new Vector3(0.13f, 0.26f, 0.13f), new Vector3(0f, -0.18f, 0f), topC, true);
        MakeBlock("SleeveTrimL", leftArmRoot.transform, new Vector3(0.15f, 0.04f, 0.15f), new Vector3(0f, -0.32f, 0f), bootTrimC, true);
        MakeBlock("HandL",       leftArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, -0.39f, 0f), skinC, true);

        // ═══ RIGHT ARM ROOT ═══
        var rightArmRoot = new GameObject("RightArmRoot");
        rightArmRoot.transform.SetParent(root.transform);
        rightArmRoot.transform.localPosition = new Vector3(0.29f, 0.33f, 0f);
        rightArmRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        rightArmRoot.transform.localScale = Vector3.one;

        MakeBlock("UpperArmR",   rightArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, 0f, 0f), skinC, true);
        MakeBlock("SleeveR",     rightArmRoot.transform, new Vector3(0.13f, 0.26f, 0.13f), new Vector3(0f, -0.18f, 0f), topC, true);
        MakeBlock("SleeveTrimR", rightArmRoot.transform, new Vector3(0.15f, 0.04f, 0.15f), new Vector3(0f, -0.32f, 0f), bootTrimC, true);
        MakeBlock("HandR",       rightArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, -0.39f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.8f, 1.7f, 0.6f);
        col.center = new Vector3(0f, 0.25f, 0f);
        col.isTrigger = true;

        return root;
    }

    private static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false, Quaternion rotation = default)
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

    [System.Serializable]
    public class WifeSaveData
    {
        public int state;
        public bool married;
        public float affection;
        public int lastWifeQuestDay;
        public bool hasProposed;
        public int chainStep;
        public bool rosaryGranted;
        public bool fishingBonusGranted;
        public int lastRosaryGrantDay;
        public int lastTalkDay;
        public List<string> wifeQuestNames = new List<string>();
        public List<string> wifeQuestTargets = new List<string>();
        public List<int> wifeQuestCounts = new List<int>();
    }
}
