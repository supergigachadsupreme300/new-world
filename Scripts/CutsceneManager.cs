using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(-100)]
public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }
    public bool IsActive { get; private set; }
    public bool JustCancelledCutscene { get; private set; }

    private UIManager _uiManager;
    private Camera _mainCamera;
    private CameraFollow _cameraFollow;
    private PlayerController _player;
    private Canvas _canvas;

    private GameObject _overlay;
    private Image _overlayImage;
    private GameObject _letterTop;
    private GameObject _letterBottom;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Coroutine _cutsceneRoutine;
    private Coroutine _pendingCheckRoutine;
    private Coroutine _menuVisualRoutine;
    private float _cutsceneStartTime;

    private bool _happyPending;
    private bool _ntrPending;
    private System.Action _previewOnComplete;
    private Coroutine _wifeLookBackRoutine;
    private Coroutine _heartRoutine;
    private GameObject _tetoRoot;
    private Transform _tetoBody;
    private Transform _tetoLeftArm;
    private Transform _tetoRightArm;
    private Transform _tetoLegL;
    private Transform _tetoLegR;
    private Transform _tetoLowerLegL;
    private Transform _tetoLowerLegR;
    private GameObject _happyPlayerModel;
    private float _happyElapsed;
    private int _happyPhase;
    private float _happyPhaseTimer;
    private int _happyJumpCount;
    private float _savedTimeSpeed;
    private readonly List<GameObject> _hearts = new List<GameObject>();
    private GameObject _happyUI;
    private GameObject _skipButton;
    private Coroutine _walkAnimRoutine;

    private GameObject _introCar;
    private GameObject _introPlayer;
    private Transform _introSteeringWheel;
    private readonly List<Transform> _introWheels = new List<Transform>();
    private Coroutine _steeringAnimRoutine;

    private readonly List<GameObject> _drivingSegments = new List<GameObject>();
    private readonly List<GameObject> _segmentPool = new List<GameObject>();
    private bool _prebuilt;
    private Material _drivingRoadMat;
    private Material _drivingGrassMat;
    private Material _drivingKerbMat;

    private const float RoadX = 14f;
    private const float IntroStartZ = -500f;
    private const float IntroEndZ = -5f;
    private const float DrivingSpeed = 64f;
    private const float SegmentLength = 10f;
    private const float SegmentWidth = 60f;
    private const float SegmentSpawnAhead = 200f;
    private const float SegmentDespawnBehind = 120f;
    private const int MaxSegmentsPerFrame = 4;
    private const int MaxActiveSegments = 40;
    private const float TransitionTime = 4f;
    private const float CamOffsetX = -3.5f;
    private const float CamOffsetY = 2.5f;
    private const float CamOffsetZ = 7f;
    private const float SadStartZ = 5f;
    private const float SadEndZ = -50f;
    private const float HappyStartZ = -18f;
    private const float HappyEndZ = 30f;
    private const float WalkSpeed = 4.5f;
    private const float SwingSpeed = 2.8f;
    private const float LateralSwing = 0f;
    private const float SkipHeight = 0.25f;
    private const float SkipSpeed = 7f;
    private const float ArmSwingAngle = 25f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        _pendingCheckRoutine = StartCoroutine(PendingCheckLoop());
    }

    void Update()
    {
        JustCancelledCutscene = false;
        if (IsActive && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            JustCancelledCutscene = true;
            CancelCutscene();
        }

        if (IsActive && Time.time - _cutsceneStartTime > 120f)
        {
            CancelCutscene();
        }
    }

    void OnDestroy()
    {
        if (_pendingCheckRoutine != null)
            StopCoroutine(_pendingCheckRoutine);
    }

    public void Initialize(UIManager uiManager)
    {
        _uiManager = uiManager;
        _canvas = Object.FindAnyObjectByType<Canvas>();
        PrebuildDrivingAssets();
    }

    // ── Skip Button ──

    private void CreateSkipButton()
    {
        if (_skipButton != null) return;
        if (_canvas == null) return;

        _skipButton = new GameObject("SkipButton");
        _skipButton.transform.SetParent(_canvas.transform, false);
        var tmp = _skipButton.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            tmp.font = _uiManager.defaultTmpFont;
        tmp.text = GameInput.IsMobile ? Localization.T("Bỏ Qua") : Localization.T("Bỏ Qua [ESC]");
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Right;
        tmp.raycastTarget = true;
        var rt = _skipButton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -20);
        rt.sizeDelta = new Vector2(200, 40);
        var skipBtn = _skipButton.AddComponent<Button>();
        skipBtn.targetGraphic = tmp;
        skipBtn.onClick.AddListener(CancelCutscene);
        _skipButton.SetActive(false);
    }

    private void ShowSkipButton()
    {
        var hudGo = GameObject.Find("HUD_Canvas");
        if (hudGo != null)
        {
            var hudCanvas = hudGo.GetComponent<Canvas>();
            if (hudCanvas != null && hudCanvas.gameObject.activeInHierarchy)
                _canvas = hudCanvas;
        }
        if (_canvas == null || !_canvas.gameObject.activeInHierarchy)
            _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null) return;
        CreateSkipButton();
        if (_skipButton != null)
            _skipButton.SetActive(true);
    }

    private void HideSkipButton()
    {
        if (_skipButton != null)
            _skipButton.SetActive(false);
    }

    // ── Public API ──

    public void PlayIntroCutscene(System.Action onComplete = null)
    {
        if (IsActive) return;
        IsActive = true;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(IntroRoutine(onComplete));
    }

    public void PlaySadEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(SadEndingRoutine(onComplete));
    }

    public void PlayBossBadEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(BossBadEndingRoutine(onComplete));
    }

    private IEnumerator BossBadEndingRoutine(System.Action onComplete = null)
    {
        try
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_uiManager != null)
                _uiManager.ShowMainMenu(false);

            DisablePlayerControl();
            DetachCamera();
            HideHUD();

            yield return StartCoroutine(CreateFadeOverlay());
            CreateLetterboxBars();
            ShowSkipButton();

            // Player standing on the road before the mansion, facing the oncoming darkness
            float playerZ = 46f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }
            var playerModel = MapBuilder.BuildPlayerModel(null);
            playerModel.transform.position = new Vector3(RoadX, 0.86f, playerZ);
            playerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in playerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(playerModel);

            // The Demon King rises from the shadows ahead
            float bossZ = playerZ + 22f;
            var bossRoot = BossModelBuilder.BuildBoss(null);
            bossRoot.position = new Vector3(RoadX, 0f, bossZ);
            bossRoot.rotation = Quaternion.Euler(0f, 180f, 0f);
            bossRoot.localScale = Vector3.one * 1.05f;
            foreach (var r in bossRoot.GetComponentsInChildren<Renderer>())
            {
                r.gameObject.layer = 0;
                r.material.color = new Color(0.05f, 0.02f, 0.03f);
            }
            RegisterSpawned(bossRoot.gameObject);

            // Glowing ember eyes
            var eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeL.name = "DemonEyeL";
            Object.Destroy(eyeL.GetComponent<Collider>());
            eyeL.transform.SetParent(bossRoot, false);
            eyeL.transform.localPosition = new Vector3(-0.17f, 1.56f, 0.21f);
            RegisterSpawned(eyeL);
            var eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeR.name = "DemonEyeR";
            Object.Destroy(eyeR.GetComponent<Collider>());
            eyeR.transform.SetParent(bossRoot, false);
            eyeR.transform.localPosition = new Vector3(0.17f, 1.56f, 0.21f);
            RegisterSpawned(eyeR);
            foreach (var e in new[] { eyeL, eyeR })
            {
                e.transform.localScale = Vector3.one * 0.18f;
                var er = e.GetComponent<Renderer>();
                if (er != null)
                    er.material.color = new Color(0.9f, 0.05f, 0.03f);
            }

            // ── PHASE 1: OPENING SHOT ──
            Vector3 camStart = new Vector3(RoadX, 1.6f, playerZ - 3f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(RoadX, 2.2f, bossZ));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2.2f);

            // ── PHASE 2: THE RISE ──
            float riseDur = 3.5f;
            float riseTimer = 0f;
            while (riseTimer < riseDur)
            {
                riseTimer += Time.deltaTime;
                float p = Mathf.Min(riseTimer / riseDur, 1f);
                bossRoot.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.65f, p);
                bossRoot.position = new Vector3(RoadX, 0f, Mathf.Lerp(bossZ, bossZ - 3f, p));
                Color eyeGlow = new Color(Mathf.Lerp(0.9f, 1f, p), Mathf.Lerp(0.05f, 0.3f, p), 0.03f);
                foreach (var e in new[] { eyeL, eyeR })
                {
                    var er = e.GetComponent<Renderer>();
                    if (er != null) er.material.color = eyeGlow;
                }
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX, 2.2f, playerZ - 1.5f), p);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 2.4f, bossZ));
                }
                yield return null;
            }
            yield return new WaitForSeconds(1f);

            // ── PHASE 3: THE LUNGE ──
            float lungeDur = 2.2f;
            float lungeTimer = 0f;
            Vector3 bossStart = bossRoot.position;
            Vector3 bossEnd = new Vector3(RoadX, 0f, playerZ + 2f);
            while (lungeTimer < lungeDur)
            {
                lungeTimer += Time.deltaTime;
                float p = Mathf.Min(lungeTimer / lungeDur, 1f);
                bossRoot.position = Vector3.Lerp(bossStart, bossEnd, p);
                float shake = 0.4f - 0.35f * p;
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = new Vector3(
                        RoadX + Random.Range(-shake, shake),
                        2.2f + Random.Range(-shake * 0.5f, shake * 0.5f),
                        playerZ - 1.5f);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 2f, bossRoot.position.z));
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            // ── PHASE 4: BLACKOUT ──
            yield return StartCoroutine(FadeOverlay(1, 1.2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            _previewOnComplete = null;
            if (onComplete != null)
            {
                RestorePlayerControl();
                ShowHUD();
                onComplete();
            }
            else
            {
                if (_uiManager == null)
                    _uiManager = Object.FindAnyObjectByType<UIManager>();
                if (_uiManager != null)
                    _uiManager.ShowBossEndScreen(
                        Localization.T("RƠI VÀO BÓNG TỐI"),
                        Localization.T("Quỷ Vương đã quật ngã con.\nBóng tối nuốt chửng ngôi làng.\n\nSố phận của con dừng lại tại đây...\nHãy quay về nơi lưu gần nhất và đối mặt với nó lần nữa."));
            }
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    public void PlayJusticeEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        if (onComplete == null)
        {
            if (RichManNPC.Instance != null) RichManNPC.Instance.Retire();
            if (PoliceOfficerNPC.Instance != null) PoliceOfficerNPC.Instance.Retire();
        }
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(JusticeEndingRoutine(onComplete));
    }

    public void PlayBlackmailEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        if (onComplete == null)
        {
            if (RichManNPC.Instance != null) RichManNPC.Instance.Retire();
            if (PoliceOfficerNPC.Instance != null) PoliceOfficerNPC.Instance.Retire();
        }
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(BlackmailEndingRoutine(onComplete));
    }

    public void PlayDemonEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(DemonEndingRoutine(onComplete));
    }

    public void PlayTrueEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        if (onComplete == null)
        {
            if (RichManNPC.Instance != null) RichManNPC.Instance.Retire();
            if (PoliceOfficerNPC.Instance != null) PoliceOfficerNPC.Instance.Retire();
        }
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(TrueEndingRoutine(onComplete));
    }

    public void PlayHappyEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(HappyEndingRoutine(onComplete));
    }

    public void PlayNtrEnding(System.Action onComplete = null)
    {
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(NtrRoutine(onComplete));
    }

    public void RequestHappyEnding()
    {
        if (IsActive || _happyPending) return;
        _happyPending = true;
    }

    public void RequestNtrEnding()
    {
        if (IsActive || _ntrPending) return;
        _ntrPending = true;
    }

    public void CancelCutscene()
    {
        if (_cutsceneRoutine != null)
        {
            StopCoroutine(_cutsceneRoutine);
            _cutsceneRoutine = null;
        }
        if (_wifeLookBackRoutine != null)
        {
            StopCoroutine(_wifeLookBackRoutine);
            _wifeLookBackRoutine = null;
        }
        if (_heartRoutine != null)
        {
            StopCoroutine(_heartRoutine);
            _heartRoutine = null;
        }
        if (GameManager.Instance != null && GameManager.Instance.TimeSpeed == 0f && _savedTimeSpeed > 0f)
            GameManager.Instance.TimeSpeed = _savedTimeSpeed;
        CleanupAll();
        RestorePlayerControl();
        if (GameManager.Instance != null && GameManager.Instance.InGame)
            ShowHUD();
        else if (_uiManager != null)
            _uiManager.ShowMainMenu(true);
        IsActive = false;

        if (_previewOnComplete != null)
        {
            var cb = _previewOnComplete;
            _previewOnComplete = null;
            cb();
        }
    }

    private IEnumerator PendingCheckLoop()
    {
        while (true)
        {
            if (_ntrPending && !IsActive)
            {
                _ntrPending = false;
                IsActive = true;
                _cutsceneStartTime = Time.time;
                _cutsceneRoutine = StartCoroutine(NtrRoutine());
            }
            else if (_happyPending && !IsActive)
            {
                _happyPending = false;
                IsActive = true;
                _cutsceneStartTime = Time.time;
                _cutsceneRoutine = StartCoroutine(HappyEndingRoutine());
            }
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════
    //  INTRO CUTSCENE
    // ═══════════════════════════════════════════════

    private IEnumerator IntroRoutine(System.Action onComplete)
    {
        try
        {
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        DisablePlayerControl();
        DetachCamera();
        HideHUD();
        ShowSkipButton();

        if (_prebuilt)
        {
            _introCar.SetActive(true);
            _introCar.transform.SetParent(null);
            _introCar.transform.position = new Vector3(RoadX, 0f, IntroStartZ);
            _introPlayer.SetActive(true);
        }
        else
        {
            InitDrivingMaterials();

            if (_introCar == null)
            {
                _introCar = MapBuilder.BuildCar(null, new Vector3(RoadX, 0f, IntroStartZ));
                RegisterSpawned(_introCar);

                _introPlayer = MapBuilder.BuildSeatedPlayerModel(_introCar.transform);
                RegisterSpawned(_introPlayer);

                if (_introCar != null)
                {
                    _introSteeringWheel = _introCar.transform.Find("SteeringWheel");
                    _introWheels.Clear();
                    foreach (string wn in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
                    {
                        var wt = _introCar.transform.Find(wn);
                        if (wt != null) _introWheels.Add(wt);
                    }
                }
            }
            else
            {
                _introCar.transform.SetParent(null);
            }
        }

        _steeringAnimRoutine = StartCoroutine(AnimateSteering());

        Quaternion camRot = Quaternion.Euler(13f, 130f, -2.5f);
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = new Vector3(RoadX + CamOffsetX, CamOffsetY, IntroStartZ + CamOffsetZ);
            _mainCamera.transform.rotation = camRot;
        }

        Vector3 camFixedPos = _mainCamera != null ? _mainCamera.transform.position : Vector3.zero;

        float driveZ = IntroStartZ;
        UpdateDrivingSegments(driveZ, 0f, 0f, false);
        while (driveZ < IntroEndZ)
        {
            driveZ += DrivingSpeed * Time.deltaTime;
            if (_introCar != null)
                _introCar.transform.position = new Vector3(RoadX, 0f, driveZ);
            if (_mainCamera != null)
            {
                Vector3 lookTarget = new Vector3(RoadX, 1f, driveZ);
                _mainCamera.transform.LookAt(lookTarget);
            }
            UpdateDrivingSegments(driveZ, 0f, 0f, false);
            yield return null;
        }

        StopSteeringAnim();
        yield return new WaitForSeconds(0.5f);

        DestroyDrivingSegments();
        HideSkipButton();
        if (!_prebuilt)
        {
            CleanupSpawned();
            _introCar = null;
            _introPlayer = null;
        }
        else
        {
            _introCar.SetActive(false);
            _introPlayer.SetActive(false);
        }
        _introSteeringWheel = null;
        _introWheels.Clear();
        ShowHUD();
        RestorePlayerControl();

        if (WorldBuilder.Instance != null)
            WorldBuilder.Instance.CloseBorderGap();

        onComplete?.Invoke();

        if (onComplete == null && _uiManager != null && GameManager.Instance != null && !GameManager.Instance.InGame)
            _uiManager.ShowMainMenu(true);
        }
        finally
        {
            HideSkipButton();
            RestorePlayerControl();
            if (GameManager.Instance != null && GameManager.Instance.InGame)
                ShowHUD();
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    // ═══════════════════════════════════════════════
    //  DRIVING GROUND SEGMENTS
    // ═══════════════════════════════════════════════

    private void InitDrivingMaterials()
    {
        if (_drivingRoadMat != null) return;
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        var shader = urp != null ? urp : Shader.Find("Standard");
        _drivingRoadMat = new Material(shader);
        _drivingRoadMat.color = new Color(0.235f, 0.243f, 0.275f);
        _drivingGrassMat = new Material(shader);
        var tex = Resources.Load<Texture2D>("texture/grass_blade");
        if (tex != null)
        {
            _drivingGrassMat.mainTexture = tex;
            _drivingGrassMat.mainTextureScale = new Vector2(4f, SegmentLength / 5f);
        }
        else
        {
            _drivingGrassMat.color = new Color(0.3f, 0.6f, 0.25f);
        }
        _drivingKerbMat = new Material(shader);
        _drivingKerbMat.color = new Color(0.46f, 0.45f, 0.42f);
    }

    // ═══════════════════════════════════════════════
    //  PREBUILD: materials + car + segments at load
    // ═══════════════════════════════════════════════

    private void PrebuildDrivingAssets()
    {
        if (_prebuilt) return;

        InitDrivingMaterials();

        _introCar = MapBuilder.BuildCar(null, Vector3.zero);
        _introCar.SetActive(false);
        _introPlayer = MapBuilder.BuildSeatedPlayerModel(_introCar.transform);
        _introPlayer.SetActive(false);

        if (_introCar != null)
        {
            _introSteeringWheel = _introCar.transform.Find("SteeringWheel");
            _introWheels.Clear();
            foreach (string wn in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
            {
                var wt = _introCar.transform.Find(wn);
                if (wt != null) _introWheels.Add(wt);
            }
        }

        int poolSize = Mathf.CeilToInt((SegmentSpawnAhead + SegmentDespawnBehind) / SegmentLength) + 3;
        for (int i = 0; i < poolSize; i++)
        {
            var seg = SpawnDrivingSegmentRaw(0f);
            seg.SetActive(false);
            _segmentPool.Add(seg);
        }

        _prebuilt = true;
    }

    private GameObject GetPooledSegment()
    {
        for (int i = _segmentPool.Count - 1; i >= 0; i--)
        {
            var seg = _segmentPool[i];
            if (seg != null)
            {
                _segmentPool.RemoveAt(i);
                seg.SetActive(true);
                return seg;
            }
        }
        return SpawnDrivingSegmentRaw(0f);
    }

    private void ReturnToPool(GameObject seg)
    {
        if (seg == null) return;
        seg.SetActive(false);
        _segmentPool.Add(seg);
    }

    private void UpdateDrivingSegments(float baseZ, float groundOffset, float scrollSpeed, bool despawn)
    {
        if (despawn)
        {
            for (int i = _drivingSegments.Count - 1; i >= 0; i--)
            {
                if (_drivingSegments[i] == null) { _drivingSegments.RemoveAt(i); continue; }
                float segZ = _drivingSegments[i].transform.position.z;
                if (segZ < baseZ - SegmentDespawnBehind)
                {
                    if (_prebuilt)
                        ReturnToPool(_drivingSegments[i]);
                    else
                        Destroy(_drivingSegments[i]);
                    _drivingSegments.RemoveAt(i);
                }
            }
        }

        float farZ = float.MinValue;
        float nearZ = float.MaxValue;
        foreach (var seg in _drivingSegments)
        {
            if (seg == null) continue;
            float sz = seg.transform.position.z;
            if (sz > farZ) farZ = sz;
            if (sz < nearZ) nearZ = sz;
        }

        float carZ = baseZ + groundOffset;
        float needed = carZ + SegmentSpawnAhead;
        float needBehind = carZ - SegmentDespawnBehind;

        if (_drivingSegments.Count == 0)
        {
            float startZ = needBehind;
            int spawned = 0;
            while (startZ < needed && spawned < MaxSegmentsPerFrame && _drivingSegments.Count < MaxActiveSegments)
            {
                startZ += SegmentLength;
                _drivingSegments.Add(SpawnDrivingSegment(startZ));
                spawned++;
            }
        }
        else
        {
            int fwdSpawned = 0;
            while (farZ < needed && fwdSpawned < MaxSegmentsPerFrame && _drivingSegments.Count < MaxActiveSegments)
            {
                farZ += SegmentLength;
                _drivingSegments.Add(SpawnDrivingSegment(farZ));
                fwdSpawned++;
            }

            int bwdSpawned = 0;
            while (nearZ > needBehind && bwdSpawned < MaxSegmentsPerFrame && _drivingSegments.Count < MaxActiveSegments)
            {
                nearZ -= SegmentLength;
                _drivingSegments.Add(SpawnDrivingSegment(nearZ));
                bwdSpawned++;
            }
        }

        if (scrollSpeed > 0f)
        {
            float move = scrollSpeed * Time.deltaTime;
            foreach (var seg in _drivingSegments)
                if (seg != null)
                    seg.transform.position += new Vector3(0f, 0f, -move);
        }
    }

    private GameObject SpawnDrivingSegment(float centerZ)
    {
        var seg = _prebuilt ? GetPooledSegment() : SpawnDrivingSegmentRaw(centerZ);
        if (_prebuilt)
            seg.transform.position = new Vector3(0f, 0f, centerZ);
        return seg;
    }

    private GameObject SpawnDrivingSegmentRaw(float centerZ)
    {
        var seg = new GameObject("DrivingSeg");

        // Road
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "Road";
        road.transform.SetParent(seg.transform);
        road.transform.localScale = new Vector3(7.6f, 0.06f, SegmentLength);
        road.transform.localPosition = new Vector3(RoadX, 0.03f, 0f);
        road.GetComponent<Renderer>().material = _drivingRoadMat;
        Destroy(road.GetComponent<Collider>());

        // Kerbs
        foreach (int side in new[] { -1, 1 })
        {
            var kerb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kerb.name = "Kerb";
            kerb.transform.SetParent(seg.transform);
            kerb.transform.localScale = new Vector3(0.55f, 0.22f, SegmentLength);
            kerb.transform.localPosition = new Vector3(RoadX + side * 4.07f, 0.11f, 0f);
            kerb.GetComponent<Renderer>().material = _drivingKerbMat;
            Destroy(kerb.GetComponent<Collider>());
        }

        // Grass left
        float roadLeft = RoadX - 3.8f;
        var grassL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grassL.name = "GrassL";
        grassL.transform.SetParent(seg.transform);
        grassL.transform.localScale = new Vector3(roadLeft + 150f, 0.05f, SegmentLength);
        grassL.transform.localPosition = new Vector3((roadLeft - 150f) / 2f, 0f, 0f);
        grassL.GetComponent<Renderer>().material = _drivingGrassMat;
        Destroy(grassL.GetComponent<Collider>());

        // Grass right
        float roadRight = RoadX + 3.8f;
        var grassR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grassR.name = "GrassR";
        grassR.transform.SetParent(seg.transform);
        grassR.transform.localScale = new Vector3(200f - roadRight, 0.05f, SegmentLength);
        grassR.transform.localPosition = new Vector3((roadRight + 200f) / 2f, 0f, 0f);
        grassR.GetComponent<Renderer>().material = _drivingGrassMat;
        Destroy(grassR.GetComponent<Collider>());

        // Trees scattered across grass
        SpawnScatteredTrees(seg, SegmentLength);

        seg.transform.position = new Vector3(0f, 0f, centerZ);
        return seg;
    }

    private void SpawnScatteredTrees(GameObject parent, float segLen)
    {
        float roadLeft = RoadX - 3.8f;
        float roadRight = RoadX + 3.8f;
        int count = Mathf.FloorToInt(segLen / 4f);

        for (int i = 0; i < count; i++)
        {
            float z = -segLen / 2f + Random.Range(0f, segLen);

            float x;
            if (Random.value < 0.5f)
                x = Random.Range(-150f, roadLeft - 2f);
            else
                x = Random.Range(roadRight + 2f, 200f);

            float treeScale = Random.Range(0.6f, 1.2f);
            var tree = MapBuilder.BuildTree(parent.transform, new Vector3(x, 0f, z), treeScale);
            if (tree != null)
                tree.name = "ScatteredTree";
        }
    }

    private void DestroyDrivingSegments()
    {
        foreach (var seg in _drivingSegments)
        {
            if (seg == null) continue;
            if (_prebuilt)
                ReturnToPool(seg);
            else
                Destroy(seg);
        }
        _drivingSegments.Clear();
    }

    // ═══════════════════════════════════════════════
    //  STEERING WHEEL + HANDS ANIMATION
    // ═══════════════════════════════════════════════

    private IEnumerator AnimateSteering()
    {
        Vector3 restHandL = new Vector3(-0.26f, 0.42f, 0.38f);
        Vector3 restHandR = new Vector3(0.26f, 0.42f, 0.38f);
        Vector3 restArmL = new Vector3(-0.26f, 0.35f, 0.15f);
        Vector3 restArmR = new Vector3(0.26f, 0.35f, 0.15f);
        float wheelSpin = 0f;
        while (true)
        {
            if (_introSteeringWheel != null)
            {
                float angle = -Mathf.Sin(Time.time * 2f) * 15f;
                _introSteeringWheel.localRotation = Quaternion.Euler(60f, 0f, angle);
            }
            if (_introPlayer != null)
            {
                float push = Mathf.Sin(Time.time * 2f) * 0.06f;

                var handL = _introPlayer.transform.Find("HandL");
                var handR = _introPlayer.transform.Find("HandR");
                if (handL != null) handL.localPosition = restHandL + new Vector3(0f, 0f, push);
                if (handR != null) handR.localPosition = restHandR + new Vector3(0f, 0f, -push);

                var armL = _introPlayer.transform.Find("UpperArmL");
                var armR = _introPlayer.transform.Find("UpperArmR");
                if (armL != null) armL.localPosition = restArmL + new Vector3(0f, 0f, push);
                if (armR != null) armR.localPosition = restArmR + new Vector3(0f, 0f, -push);
            }
            wheelSpin += Time.deltaTime * 120f;
            foreach (var w in _introWheels)
                if (w != null) w.localRotation = Quaternion.Euler(wheelSpin, 0f, 90f);
            yield return null;
        }
    }

    private void StopSteeringAnim()
    {
        if (_steeringAnimRoutine != null)
        {
            StopCoroutine(_steeringAnimRoutine);
            _steeringAnimRoutine = null;
        }
    }

    // ═══════════════════════════════════════════════
    //  MAIN MENU VISUAL  (close-up of player driving)
    // ═══════════════════════════════════════════════

    public void PlayMainMenuVisual()
    {
        if (_menuVisualRoutine != null) return;
        StopMainMenuVisual();
        _menuVisualRoutine = StartCoroutine(MenuVisualRoutine());
    }

    public void StopMainMenuVisual(bool keepSegments = false)
    {
        if (_menuVisualRoutine != null)
        {
            StopCoroutine(_menuVisualRoutine);
            _menuVisualRoutine = null;
            AttachCamera();
        }
        StopSteeringAnim();
        if (!keepSegments)
            DestroyDrivingSegments();
        if (_prebuilt && _introCar != null)
            _introCar.SetActive(false);
    }

    private IEnumerator MenuVisualRoutine()
    {
        DetachCamera();
        DisablePlayerControl();

        if (_prebuilt)
        {
            _introCar.SetActive(true);
            _introCar.transform.position = new Vector3(RoadX, 0f, IntroStartZ);
            _introPlayer.SetActive(true);
        }
        else
        {
            InitDrivingMaterials();

            _introCar = MapBuilder.BuildCar(null, new Vector3(RoadX, 0f, IntroStartZ));
            RegisterSpawned(_introCar);
            _introPlayer = MapBuilder.BuildSeatedPlayerModel(_introCar.transform);
            RegisterSpawned(_introPlayer);

            if (_introCar != null)
            {
                _introSteeringWheel = _introCar.transform.Find("SteeringWheel");
                _introWheels.Clear();
                foreach (string wn in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
                {
                    var wt = _introCar.transform.Find(wn);
                    if (wt != null) _introWheels.Add(wt);
                }
            }
        }

        _steeringAnimRoutine = StartCoroutine(AnimateSteering());

        if (_mainCamera != null)
        {
            _mainCamera.transform.position = new Vector3(RoadX + CamOffsetX, CamOffsetY, IntroStartZ + CamOffsetZ);
            _mainCamera.transform.rotation = Quaternion.Euler(13f, 130f, -2.5f);
        }

        float groundOffset = 0f;
        while (true)
        {
            groundOffset += DrivingSpeed * 0.5f * Time.deltaTime;
            UpdateDrivingSegments(IntroStartZ, groundOffset, DrivingSpeed, true);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════
    //  SAD ENDING
    // ═══════════════════════════════════════════════

    private IEnumerator SadEndingRoutine(System.Action onComplete = null)
    {
        try
        {
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        DisablePlayerControl();
        DetachCamera();
        HideHUD();

        yield return StartCoroutine(CreateFadeOverlay());
        CreateLetterboxBars();
        ShowSkipButton();

        // ── Reposition player on road behind wagon spawn ──
        float playerSadZ = SadStartZ + 10f;
        if (_player != null)
        {
            _player.transform.position = new Vector3(RoadX, 0f, playerSadZ);
            _player.transform.rotation = Quaternion.identity;

            // Hide real player model (on layer 6, not visible to camera)
            var realModel = _player.transform.Find("PlayerModel");
            if (realModel != null)
                realModel.gameObject.SetActive(false);
        }

        // Spawn a standalone player model on default layer (visible to camera)
        var sadPlayerModel = MapBuilder.BuildPlayerModel(null);
        sadPlayerModel.transform.position = new Vector3(RoadX, 0.82f, playerSadZ);
        sadPlayerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        foreach (var r in sadPlayerModel.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(sadPlayerModel);

        // ── PHASE 1: THE PLAYER WATCHES (5s) ──
        // Camera in front of player, looking at his face
        Vector3 camStart = new Vector3(RoadX, 1.8f, playerSadZ - 3f);
        Vector3 lookAtPlayer = new Vector3(RoadX, 1.2f, playerSadZ);
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = camStart;
            _mainCamera.transform.LookAt(lookAtPlayer);
        }
        yield return StartCoroutine(FadeOverlay(0, 2f));
        yield return new WaitForSeconds(3f);

        // ── PHASE 2: THE WAGON APPEARS (6s) ──
        float wagonStartZ = SadStartZ;
        float wagonEndZ = SadEndZ;
        float deltaZ = wagonEndZ - wagonStartZ;

        var wagonRoot = CreateWagon(RoadX, wagonStartZ);

        var horse = HorseModelBuilder.BuildHorse(wagonRoot);
        horse.localPosition = new Vector3(0f, 0f, -3.2f);
        RegisterSpawned(horse.gameObject);

        var enemy = EnemyModelBuilder.BuildRegularEnemy(wagonRoot);
        enemy.localPosition = new Vector3(0.35f, 0.56f, -0.6f);
        RegisterSpawned(enemy.gameObject);

        var wife = WifeNPC.BuildWifeNpc(wagonRoot,
            new Vector3(-0.3f, 1.42f, 0.3f), 1f,
            Quaternion.Euler(0, 180, 0));
        RegisterSpawned(wife);

        // Pan camera from player's face to the departing wagon
        float panDur = 6f;
        float panTimer = 0f;
        Vector3 panEnd = new Vector3(RoadX - 3f, 3f, wagonStartZ + 6f);

        while (panTimer < panDur)
        {
            panTimer += Time.deltaTime;
            float pt = Mathf.SmoothStep(0f, 1f, panTimer / panDur);
            _mainCamera.transform.position = Vector3.Lerp(camStart, panEnd, pt);
            _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, wagonStartZ));
            yield return null;
        }

        // ── PHASE 3: THE JOURNEY (16s) ──
        float rideDur = 16f;
        float rideTimer = 0f;
        bool wifeLookedBack = false;

        while (rideTimer < rideDur)
        {
            rideTimer += Time.deltaTime;
            float p = Mathf.Min(rideTimer / rideDur, 1f);
            float z = wagonStartZ + deltaZ * p;

            wagonRoot.position = new Vector3(RoadX, 0f, z);

            if (!wifeLookedBack && p >= 0.65f && wife != null)
            {
                wifeLookedBack = true;
                _wifeLookBackRoutine = StartCoroutine(WifeLookBack(wife.transform));
            }

            float camZ = z + 10f;
            _mainCamera.transform.position = new Vector3(RoadX - 2f, 3.5f, camZ);
            _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, z));

            yield return null;
        }

        // ── PHASE 4: FADING AWAY (5s) ──
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(FadeOverlay(1, 2f));

        // ── PHASE 5: END SCREEN ──
        HideSkipButton();
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();

        FinishEndingScene(onComplete,
            Localization.T("KẾT THÚC BUỒN"),
            Localization.T("Bạn đã đến quá muộn.\nTrong khi bạn đi tìm kiếm giàu sang,\nbạn đã quên đi điều thực sự quan trọng.\n\nCô ấy đợi...\ncho đến khi không thể đợi nữa."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    private IEnumerator NtrRoutine(System.Action onComplete = null)
    {
        try
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_uiManager != null)
                _uiManager.ShowMainMenu(false);

            DisablePlayerControl();
            DetachCamera();
            HideHUD();

            yield return StartCoroutine(CreateFadeOverlay());
            CreateLetterboxBars();
            ShowSkipButton();

            // ── Reposition player on road behind the pickup spot ──
            float playerZ = SadStartZ + 10f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;

                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }

            var ntrPlayerModel = MapBuilder.BuildPlayerModel(null);
            ntrPlayerModel.transform.position = new Vector3(RoadX, 0.82f, playerZ);
            ntrPlayerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in ntrPlayerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(ntrPlayerModel);

            // ── PHASE 1: THE PLAYER WATCHES (2.5s) ──
            Vector3 camStart = new Vector3(RoadX, 1.8f, playerZ - 3f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(RoadX, 1.2f, playerZ));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2.5f);

            // ── PHASE 2: THE GOLD CAR ARRIVES ──
            float stopZ = -6f;
            var carRoot = MapBuilder.BuildCar(null,
                new Vector3(RoadX, 0f, stopZ), new Color(0.92f, 0.78f, 0.25f));
            RegisterSpawned(carRoot);

            var wifeModel = WifeNPC.BuildWifeNpc(null,
                new Vector3(RoadX, 0.86f, -1.5f), 1f, Quaternion.Euler(0f, 180f, 0f));
            foreach (var r in wifeModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(wifeModel);

            var richModel = RichManNPC.BuildRichManNpc(null,
                new Vector3(RoadX - 1.8f, 0.86f, -4.2f), 1f, Quaternion.Euler(0f, 180f, 0f), false);
            foreach (var r in richModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(richModel);

            // Pan camera from player face to the trio
            Vector3 camPickup = new Vector3(RoadX - 2f, 2.6f, stopZ + 9f);
            yield return StartCoroutine(PanCamera(camStart, camPickup, new Vector3(RoadX, 1f, stopZ + 2f), 3f));

            // ── PHASE 3: HE TAKES HER AWAY (walk to car) ──
            Vector3 wifeStart = wifeModel.transform.position;
            Vector3 richStart = richModel.transform.position;
            Vector3 wifeDoor = new Vector3(RoadX + 0.6f, 0.86f, stopZ - 1f);
            Vector3 richDoor = new Vector3(RoadX - 0.6f, 0.86f, stopZ - 1f);

            float walkDur = 4f;
            float wt = 0f;
            while (wt < walkDur)
            {
                wt += Time.deltaTime;
                float p = Mathf.Min(wt / walkDur, 1f);
                wifeModel.transform.position = Vector3.Lerp(wifeStart, wifeDoor, p);
                richModel.transform.position = Vector3.Lerp(richStart, richDoor, p);
                FaceMoveDirection(wifeModel.transform, wifeDoor - wifeModel.transform.position);
                FaceMoveDirection(richModel.transform, richDoor - richModel.transform.position);
                yield return null;
            }

            // Board the car
            wifeModel.transform.SetParent(carRoot.transform);
            wifeModel.transform.localPosition = new Vector3(0.35f, 0.62f, -0.2f);
            wifeModel.transform.localRotation = Quaternion.identity;
            richModel.transform.SetParent(carRoot.transform);
            richModel.transform.localPosition = new Vector3(-0.35f, 0.62f, -0.2f);
            richModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            yield return new WaitForSeconds(1.5f);

            // ── PHASE 4: THE JOURNEY (12s) ──
            float rideDur = 12f;
            float rideTimer = 0f;
            float deltaZ = SadEndZ - stopZ;
            bool wifeLookedBack = false;

            while (rideTimer < rideDur)
            {
                rideTimer += Time.deltaTime;
                float p = Mathf.Min(rideTimer / rideDur, 1f);
                float z = stopZ + deltaZ * p;
                carRoot.transform.position = new Vector3(RoadX, 0f, z);

                if (!wifeLookedBack && p >= 0.6f)
                {
                    wifeLookedBack = true;
                    _wifeLookBackRoutine = StartCoroutine(WifeLookBack(wifeModel.transform));
                }

                if (_mainCamera != null)
                {
                    float camZ = z + 10f;
                    _mainCamera.transform.position = new Vector3(RoadX - 2f, 3.5f, camZ);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, z));
                }
                yield return null;
            }

            // ── PHASE 5: FADING AWAY (5s) ──
            yield return new WaitForSeconds(3f);
            yield return StartCoroutine(FadeOverlay(1, 2f));

            // ── PHASE 6: END SCREEN ──
            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("KẾT THÚC NTR"),
                Localization.T("Trong lúc cậu mải làm nông, ông chú giàu có đã lặng lẽ đến gần cô ấy.\n\nKhi cậu quay lại...\nJessica đã không còn đợi cậu nữa.\n\nCậu đã để cô ấy ra đi, mãi mãi."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    private IEnumerator JusticeEndingRoutine(System.Action onComplete = null)
    {
        try
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_uiManager != null)
                _uiManager.ShowMainMenu(false);

            DisablePlayerControl();
            DetachCamera();
            HideHUD();

            yield return StartCoroutine(CreateFadeOverlay());
            CreateLetterboxBars();
            ShowSkipButton();

            // Player on the road, watching from the south
            float playerZ = 38f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }
            var justicePlayerModel = MapBuilder.BuildPlayerModel(null);
            justicePlayerModel.transform.position = new Vector3(RoadX, 0.82f, playerZ);
            justicePlayerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in justicePlayerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(justicePlayerModel);

            // Rich man standing at the mansion front
            var richModel = RichManNPC.BuildRichManNpc(null,
                new Vector3(20.5f, 0.86f, 46f), 1f, Quaternion.Euler(0f, 90f, 0f), false);
            foreach (var r in richModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(richModel);

            // ── PHASE 1: OPENING SHOT ──
            Vector3 camStart = new Vector3(RoadX, 2.2f, playerZ - 3f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(20f, 1.2f, 46f));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2f);

            // ── PHASE 2: POLICE CAR ARRIVES ──
            float carStopZ = 52f;
            var policeCar = MapBuilder.BuildPoliceCar(null, new Vector3(RoadX, 0f, 64f));
            RegisterSpawned(policeCar);
            float driveDur = 4f;
            float driveTimer = 0f;
            while (driveTimer < driveDur)
            {
                driveTimer += Time.deltaTime;
                float p = Mathf.Min(driveTimer / driveDur, 1f);
                policeCar.transform.position = new Vector3(RoadX, 0f, Mathf.Lerp(64f, carStopZ, p));
                yield return null;
            }

            var officer = MapBuilder.BuildPoliceOfficer(null,
                new Vector3(RoadX - 1.2f, 0.93f, carStopZ), Quaternion.Euler(0f, -90f, 0f));
            foreach (var r in officer.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(officer);

            // ── PHASE 3: THE ARREST ──
            Vector3 officerStart = officer.transform.position;
            Vector3 richPos = richModel.transform.position;
            float arrestDur = 4f;
            float arrestTimer = 0f;
            while (arrestTimer < arrestDur)
            {
                arrestTimer += Time.deltaTime;
                float p = Mathf.Min(arrestTimer / arrestDur, 1f);
                officer.transform.position = Vector3.Lerp(officerStart, richPos, p);
                FaceMoveDirection(officer.transform, richPos - officer.transform.position);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX - 1f, 2.6f, carStopZ + 4f), p);
                    _mainCamera.transform.LookAt(new Vector3(20f, 1.2f, 46f));
                }
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 4: TAKEN AWAY ──
            Vector3 richStart = richModel.transform.position;
            Vector3 carDoor = new Vector3(RoadX - 0.6f, 0.86f, carStopZ - 0.4f);
            Vector3 officerDoor = new Vector3(RoadX + 0.6f, 0.86f, carStopZ - 0.4f);
            float takeDur = 4f;
            float takeTimer = 0f;
            while (takeTimer < takeDur)
            {
                takeTimer += Time.deltaTime;
                float p = Mathf.Min(takeTimer / takeDur, 1f);
                richModel.transform.position = Vector3.Lerp(richStart, carDoor, p);
                officer.transform.position = Vector3.Lerp(richPos, officerDoor, p);
                FaceMoveDirection(richModel.transform, carDoor - richModel.transform.position);
                FaceMoveDirection(officer.transform, officerDoor - officer.transform.position);
                yield return null;
            }

            richModel.transform.SetParent(policeCar.transform);
            richModel.transform.localPosition = new Vector3(-0.35f, 0.62f, -0.2f);
            richModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            officer.transform.SetParent(policeCar.transform);
            officer.transform.localPosition = new Vector3(0.35f, 0.62f, -0.2f);
            officer.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            yield return new WaitForSeconds(1f);

            // ── PHASE 5: DEPARTURE ──
            float departDur = 5f;
            float departTimer = 0f;
            float departZ = -30f;
            while (departTimer < departDur)
            {
                departTimer += Time.deltaTime;
                float p = Mathf.Min(departTimer / departDur, 1f);
                policeCar.transform.position = new Vector3(RoadX, 0f, Mathf.Lerp(carStopZ, departZ, p));
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = new Vector3(RoadX - 2f, 3.2f, Mathf.Lerp(carStopZ, departZ, p) + 10f);
                    _mainCamera.transform.LookAt(policeCar.transform.position);
                }
                yield return null;
            }

            // ── PHASE 6: FADE + END SCREEN ──
            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(FadeOverlay(1, 2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("CÔNG LÝ ĐƯỢC THỰC THI NHƯNG HIỂM HỌA CHƯA QUA"),
                Localization.T("Cậu đã lật tẩy bộ mặt thật của Phú Ông.\nCảnh sát đã đến, và hắn bị bắt ngay trước dinh thự của chính mình.\n\nJessica được an toàn.\nNhưng phía đông, tiếng gầm của Quỷ Vương vẫn còn vang vọng...\nHiểm họa thật sự vẫn chưa qua."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    private IEnumerator BlackmailEndingRoutine(System.Action onComplete = null)
    {
        try
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_uiManager != null)
                _uiManager.ShowMainMenu(false);

            DisablePlayerControl();
            DetachCamera();
            HideHUD();

            yield return StartCoroutine(CreateFadeOverlay());
            CreateLetterboxBars();
            ShowSkipButton();

            if (_player != null)
            {
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }

            // Player model at the mansion front
            var playerModel = MapBuilder.BuildPlayerModel(null);
            playerModel.transform.position = new Vector3(28f, 0.86f, 46f);
            playerModel.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            foreach (var r in playerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(playerModel);

            // Rich man facing the player
            var richModel = RichManNPC.BuildRichManNpc(null,
                new Vector3(31.5f, 0.86f, 46f), 1f, Quaternion.Euler(0f, 90f, 0f), false);
            foreach (var r in richModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(richModel);

            // Bribe sack on the ground between them
            var sack = BuildBribeSack(new Vector3(29.8f, 0.45f, 46f));
            RegisterSpawned(sack);

            // ── PHASE 1: OPENING SHOT ──
            Vector3 camStart = new Vector3(28f, 2.4f, 41f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(29.8f, 1.1f, 46f));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2f);

            // ── PHASE 2: PAN TO THE SACK ──
            yield return StartCoroutine(PanCamera(camStart, new Vector3(29.8f, 2.2f, 44f), new Vector3(29.8f, 1f, 46f), 2f));
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 3: THE PLAYER TAKES THE BRIBE ──
            Vector3 playerStart = playerModel.transform.position;
            Vector3 sackGrab = new Vector3(29.8f, 0.86f, 46f);
            float walkDur = 2.5f;
            float walkTimer = 0f;
            while (walkTimer < walkDur)
            {
                walkTimer += Time.deltaTime;
                float p = Mathf.Min(walkTimer / walkDur, 1f);
                playerModel.transform.position = Vector3.Lerp(playerStart, sackGrab, p);
                FaceMoveDirection(playerModel.transform, sackGrab - playerModel.transform.position);
                yield return null;
            }
            sack.transform.SetParent(playerModel.transform);
            sack.transform.localPosition = new Vector3(0.35f, 0.35f, 0f);

            yield return new WaitForSeconds(2f);

            // ── PHASE 4: FADE + END SCREEN ──
            yield return StartCoroutine(FadeOverlay(1, 2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("KẾT THÚC ĐỒI BẠI"),
                Localization.T("Cậu đã im lặng. Và cậu đã được trả một cái giá rất hậu hĩnh.\n\nNhưng đêm xuống, những chiếc xe vẫn nối đuôi nhau đến dinh thự.\nJessica vẫn đang trong tầm ngắm của hắn...\n\nVà giờ, cậu là một phần của câu chuyện đó."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    private IEnumerator DemonEndingRoutine(System.Action onComplete = null)
    {
        try
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_uiManager != null)
                _uiManager.ShowMainMenu(false);

            DisablePlayerControl();
            DetachCamera();
            HideHUD();

            yield return StartCoroutine(CreateFadeOverlay());
            CreateLetterboxBars();
            ShowSkipButton();

            // Player standing alone at the eastern road where the Demon King fell
            float playerZ = 96f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }
            var heroModel = MapBuilder.BuildPlayerModel(null);
            heroModel.transform.position = new Vector3(RoadX, 0.82f, playerZ);
            heroModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in heroModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(heroModel);

            // Scorched ground where the Demon King was slain
            float bossZ = playerZ + 14f;
            var scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            scorch.name = "DemonScorch";
            scorch.transform.position = new Vector3(RoadX, 0.01f, bossZ);
            scorch.transform.localScale = new Vector3(4f, 0.02f, 4f);
            var scorchR = scorch.GetComponent<Renderer>();
            if (scorchR != null) scorchR.material.color = new Color(0.12f, 0.04f, 0.05f);
            Object.Destroy(scorch.GetComponent<Collider>());
            RegisterSpawned(scorch);

            // ── PHASE 1: OPENING SHOT ──
            Vector3 camStart = new Vector3(RoadX, 2.2f, playerZ - 4f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(RoadX, 1.2f, playerZ + 2f));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2.2f);

            // ── PHASE 2: THE FALLEN KING'S EMBER ──
            var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ember.name = "DemonEmber";
            ember.transform.position = new Vector3(RoadX, 1f, bossZ);
            ember.transform.localScale = Vector3.one * 0.5f;
            var emberR = ember.GetComponent<Renderer>();
            if (emberR != null) emberR.material.color = new Color(1f, 0.4f, 0.1f);
            Object.Destroy(ember.GetComponent<Collider>());
            RegisterSpawned(ember);

            float glowDur = 3f;
            float glowTimer = 0f;
            while (glowTimer < glowDur)
            {
                glowTimer += Time.deltaTime;
                float p = Mathf.Min(glowTimer / glowDur, 1f);
                ember.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.2f, p);
                if (emberR != null)
                    emberR.material.color = new Color(1f, 0.4f - 0.2f * p, 0.1f, 1f);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX - 2f, 2.8f, bossZ + 4f), p);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, bossZ));
                }
                yield return null;
            }
            ember.SetActive(false);
            yield return new WaitForSeconds(1f);

            // ── PHASE 3: SMOKE RISES FROM THE REMAINS ──
            for (int i = 0; i < 5; i++)
            {
                var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                smoke.name = "DemonSmoke";
                smoke.transform.position = new Vector3(RoadX + Random.Range(-1f, 1f), 0.6f, bossZ + Random.Range(-1f, 1f));
                smoke.transform.localScale = Vector3.one * Random.Range(0.5f, 0.9f);
                var sr = smoke.GetComponent<Renderer>();
                if (sr != null) sr.material.color = new Color(0.08f, 0.06f, 0.07f);
                Object.Destroy(smoke.GetComponent<Collider>());
                RegisterSpawned(smoke);
                yield return null;
            }
            yield return new WaitForSeconds(2f);

            // ── PHASE 4: CAMERA TURNS TOWARD THE VILLAGE, DEALS GO ON ──
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(RoadX - 1f, 2.4f, playerZ + 2f);
                _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, playerZ - 20f));
            }
            var dealCar = MapBuilder.BuildCar(null, new Vector3(RoadX, 0f, playerZ - 8f));
            RegisterSpawned(dealCar);
            float carDur = 3.5f;
            float carTimer = 0f;
            while (carTimer < carDur)
            {
                carTimer += Time.deltaTime;
                float p = Mathf.Min(carTimer / carDur, 1f);
                dealCar.transform.position = new Vector3(RoadX, 0f, Mathf.Lerp(playerZ - 8f, playerZ - 46f, p));
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 5: FADE + END SCREEN ──
            yield return StartCoroutine(FadeOverlay(1, 2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("QUỶ VƯƠNG ĐÃ CHẾT NHƯNG CÁI ÁC CHƯA HẾT"),
                Localization.T("Quỷ Vương đã bị đánh bại, bóng tối bị đẩy lùi.\nNhưng trong bóng đêm, những giao dịch bẩn của Phú Ông vẫn tiếp diễn...\n\nJessica vẫn còn trong tầm ngắm của hắn.\nCái ác chưa bị nhổ tận gốc.\nNgôi làng chưa thể yên bình."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    private IEnumerator TrueEndingRoutine(System.Action onComplete = null)
    {
        try
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_uiManager != null)
                _uiManager.ShowMainMenu(false);

            DisablePlayerControl();
            DetachCamera();
            HideHUD();

            yield return StartCoroutine(CreateFadeOverlay());
            CreateLetterboxBars();
            ShowSkipButton();

            // Player on the road, watching from the south
            float playerZ = 38f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }
            var truePlayerModel = MapBuilder.BuildPlayerModel(null);
            truePlayerModel.transform.position = new Vector3(RoadX, 0.82f, playerZ);
            truePlayerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in truePlayerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(truePlayerModel);

            // Rich man standing at the mansion front
            var richModel = RichManNPC.BuildRichManNpc(null,
                new Vector3(20.5f, 0.86f, 46f), 1f, Quaternion.Euler(0f, 90f, 0f), false);
            foreach (var r in richModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(richModel);

            // ── PHASE 1: DAWN — DEMON SLAIN, LIGHT SPREADS ──
            Vector3 camStart = new Vector3(RoadX, 2.2f, playerZ - 3f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(20f, 1.2f, 46f));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(1.5f);

            // Rosary light burst overhead — the Demon King has been vanquished
            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "TrueLightBurst";
            burst.transform.position = new Vector3(RoadX, 8f, 40f);
            burst.transform.localScale = Vector3.one * 0.4f;
            var burstR = burst.GetComponent<Renderer>();
            if (burstR != null) burstR.material.color = new Color(1f, 0.9f, 0.55f);
            Object.Destroy(burst.GetComponent<Collider>());
            RegisterSpawned(burst);

            float burstDur = 2.5f;
            float burstTimer = 0f;
            while (burstTimer < burstDur)
            {
                burstTimer += Time.deltaTime;
                float p = Mathf.Min(burstTimer / burstDur, 1f);
                burst.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 3.2f, p);
                yield return null;
            }
            burst.SetActive(false);
            yield return new WaitForSeconds(1f);

            // ── PHASE 2: POLICE CAR ARRIVES ──
            float carStopZ = 52f;
            var policeCar = MapBuilder.BuildPoliceCar(null, new Vector3(RoadX, 0f, 64f));
            RegisterSpawned(policeCar);
            float driveDur = 4f;
            float driveTimer = 0f;
            while (driveTimer < driveDur)
            {
                driveTimer += Time.deltaTime;
                float p = Mathf.Min(driveTimer / driveDur, 1f);
                policeCar.transform.position = new Vector3(RoadX, 0f, Mathf.Lerp(64f, carStopZ, p));
                yield return null;
            }

            var officer = MapBuilder.BuildPoliceOfficer(null,
                new Vector3(RoadX - 1.2f, 0.93f, carStopZ), Quaternion.Euler(0f, -90f, 0f));
            foreach (var r in officer.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(officer);

            // ── PHASE 3: THE ARREST ──
            Vector3 officerStart = officer.transform.position;
            Vector3 richPos = richModel.transform.position;
            float arrestDur = 4f;
            float arrestTimer = 0f;
            while (arrestTimer < arrestDur)
            {
                arrestTimer += Time.deltaTime;
                float p = Mathf.Min(arrestTimer / arrestDur, 1f);
                officer.transform.position = Vector3.Lerp(officerStart, richPos, p);
                FaceMoveDirection(officer.transform, richPos - officer.transform.position);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX - 1f, 2.6f, carStopZ + 4f), p);
                    _mainCamera.transform.LookAt(new Vector3(20f, 1.2f, 46f));
                }
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 4: TAKEN AWAY ──
            Vector3 richStart = richModel.transform.position;
            Vector3 carDoor = new Vector3(RoadX - 0.6f, 0.86f, carStopZ - 0.4f);
            Vector3 officerDoor = new Vector3(RoadX + 0.6f, 0.86f, carStopZ - 0.4f);
            float takeDur = 4f;
            float takeTimer = 0f;
            while (takeTimer < takeDur)
            {
                takeTimer += Time.deltaTime;
                float p = Mathf.Min(takeTimer / takeDur, 1f);
                richModel.transform.position = Vector3.Lerp(richStart, carDoor, p);
                officer.transform.position = Vector3.Lerp(richPos, officerDoor, p);
                FaceMoveDirection(richModel.transform, carDoor - richModel.transform.position);
                FaceMoveDirection(officer.transform, officerDoor - officer.transform.position);
                yield return null;
            }

            richModel.transform.SetParent(policeCar.transform);
            richModel.transform.localPosition = new Vector3(-0.35f, 0.62f, -0.2f);
            richModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            officer.transform.SetParent(policeCar.transform);
            officer.transform.localPosition = new Vector3(0.35f, 0.62f, -0.2f);
            officer.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            yield return new WaitForSeconds(1f);

            // ── PHASE 5: DEPARTURE ──
            float departDur = 5f;
            float departTimer = 0f;
            float departZ = -30f;
            while (departTimer < departDur)
            {
                departTimer += Time.deltaTime;
                float p = Mathf.Min(departTimer / departDur, 1f);
                policeCar.transform.position = new Vector3(RoadX, 0f, Mathf.Lerp(carStopZ, departZ, p));
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = new Vector3(RoadX - 2f, 3.2f, Mathf.Lerp(carStopZ, departZ, p) + 10f);
                    _mainCamera.transform.LookAt(policeCar.transform.position);
                }
                yield return null;
            }

            // ── PHASE 6: SUNRISE OVER THE PEACEFUL VILLAGE ──
            yield return new WaitForSeconds(1.5f);
            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "PeaceSun";
            sun.transform.position = new Vector3(RoadX + 18f, 14f, 10f);
            sun.transform.localScale = Vector3.one * 3f;
            var sunR = sun.GetComponent<Renderer>();
            if (sunR != null) sunR.material.color = new Color(1f, 0.85f, 0.45f);
            Object.Destroy(sun.GetComponent<Collider>());
            RegisterSpawned(sun);

            yield return StartCoroutine(PanCamera(
                new Vector3(RoadX - 2f, 3.2f, 44f),
                new Vector3(RoadX - 6f, 4.5f, 6f),
                new Vector3(RoadX, 1.5f, 20f), 4f));
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 7: FADE + END SCREEN ──
            yield return StartCoroutine(FadeOverlay(1, 2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("KẾT THÚC THẬT SỰ"),
                Localization.T("Quỷ Vương đã bị trấn áp.\nPhú Ông đã bị cảnh sát dẫn đi ngay trước dinh thự của hắn.\n\nHai hiểm họa đã bị nhổ tận gốc.\nNgôi làng cuối cùng cũng được yên bình.\nMặt trời mọc lên, và một tương lai tươi sáng đang chờ đón."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    private GameObject BuildBribeSack(Vector3 position)
    {
        var sack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sack.name = "BribeSack";
        sack.transform.position = position;
        sack.transform.localScale = new Vector3(0.7f, 0.55f, 0.55f);
        var r = sack.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.62f, 0.5f, 0.2f);
        Object.Destroy(sack.GetComponent<Collider>());
        return sack;
    }

    private IEnumerator PanCamera(Vector3 startPos, Vector3 endPos, Vector3 lookTarget, float duration)
    {
        if (_mainCamera == null)
            yield break;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / duration);
            _mainCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            _mainCamera.transform.LookAt(lookTarget);
            yield return null;
        }
    }

    private void FaceMoveDirection(Transform t, Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return;
        t.rotation = Quaternion.LookRotation(-dir.normalized);
    }

    private IEnumerator WifeLookBack(Transform wife)
    {
        Quaternion startRot = wife.transform.rotation;
        Quaternion lookBack = Quaternion.Euler(0, 0, 0);
        float dur = 1.5f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            wife.transform.rotation = Quaternion.Slerp(startRot, lookBack, t);
            yield return null;
        }
        yield return new WaitForSeconds(3f);

        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            wife.transform.rotation = Quaternion.Slerp(lookBack, startRot, t);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════
    //  HAPPY ENDING
    // ═══════════════════════════════════════════════

    private IEnumerator HappyEndingRoutine(System.Action onComplete = null)
    {
        _savedTimeSpeed = GameManager.Instance != null ? GameManager.Instance.TimeSpeed : 0.01f;
        try
        {
        _player = GameManager.Instance?.Player;
        if (_player == null)
        {
            yield break;
        }
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        _player.transform.position = new Vector3(RoadX - 0.8f, 1f, HappyStartZ);
        _player.transform.rotation = Quaternion.identity;

        DetachCamera();
        DisablePlayerControl();
        HideHUD();
        ShowSkipButton();

        // Hide real player model (layer 6, invisible to camera) and spawn visible cutscene model
        var realModel = _player.transform.Find("PlayerModel");
        if (realModel != null)
            realModel.gameObject.SetActive(false);

        _happyPlayerModel = MapBuilder.BuildPlayerModel(null);
        _happyPlayerModel.transform.position = new Vector3(RoadX - 0.8f, 0.82f, HappyStartZ);
        _happyPlayerModel.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        foreach (var r in _happyPlayerModel.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(_happyPlayerModel);
        _walkAnimRoutine = StartCoroutine(WalkAnimation(_happyPlayerModel, WalkSpeed));

        _tetoRoot = CreateTeto(new Vector3(RoadX + 0.8f, 1f, HappyStartZ - 1.5f));
        _tetoBody = _tetoRoot?.transform.Find("BodyRoot");
        _tetoLeftArm = _tetoRoot?.transform.Find("LeftArmRoot");
        _tetoRightArm = _tetoRoot?.transform.Find("RightArmRoot");
        var legsRoot = _tetoRoot?.transform.Find("LegsRoot");
        _tetoLegL = legsRoot?.Find("LegL");
        _tetoLegR = legsRoot?.Find("LegR");
        _tetoLowerLegL = _tetoLegL?.Find("LowerLegL");
        _tetoLowerLegR = _tetoLegR?.Find("LowerLegR");

        _happyPhase = 0;
        _happyElapsed = 0;

        GameManager.Instance?.SetTimeOfDay(12f);
        _savedTimeSpeed = GameManager.Instance != null ? GameManager.Instance.TimeSpeed : 0.01f;
        if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = 0;

        while (_happyPhase == 0)
        {
            _happyElapsed += Time.deltaTime;

            if (_player.transform.position.z < HappyEndZ)
            {
                Vector3 pos = _player.transform.position;
                pos.z += WalkSpeed * Time.deltaTime;
                pos.x = RoadX - 1f;
                _player.transform.position = pos;
                _player.transform.rotation = Quaternion.identity;

                if (_happyPlayerModel != null)
                {
                    _happyPlayerModel.transform.position = new Vector3(pos.x, 0.82f, pos.z);
                    _happyPlayerModel.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                }

                float side = Mathf.Sin(_happyElapsed * SwingSpeed);
                float tz = _player.transform.position.z - 1.2f + Mathf.Cos(_happyElapsed * SwingSpeed) * 0.3f;
                float tx = RoadX + side * LateralSwing;
                if (_tetoRoot != null)
                {
                    float skip = Mathf.Sin(_happyElapsed * SkipSpeed);
                    float bounce = Mathf.Pow(Mathf.Max(0, skip), 2) * SkipHeight;
                    float tilt = skip * 4f;
                    float armSwing = skip * ArmSwingAngle;
                    _tetoRoot.transform.position = new Vector3(tx, 1f + bounce, tz);
                    _tetoRoot.transform.rotation = Quaternion.Euler(0, (side >= 0 ? 10 : -10) + 180, tilt);
                    if (_tetoLeftArm != null)
                        _tetoLeftArm.localRotation = Quaternion.Euler(armSwing, 0f, -15f);
                    if (_tetoRightArm != null)
                        _tetoRightArm.localRotation = Quaternion.Euler(-armSwing, 0f, 15f);
                    float legPhase = Mathf.Sin(_happyElapsed * SkipSpeed * 0.5f);
                    float kickAngle = 35f;
                    if (_tetoLegL != null)
                        _tetoLegL.localRotation = Quaternion.identity;
                    if (_tetoLegR != null)
                        _tetoLegR.localRotation = Quaternion.identity;
                    if (_tetoLowerLegL != null)
                        _tetoLowerLegL.localRotation = Quaternion.Euler(-Mathf.Max(0, legPhase) * kickAngle, 0f, 0f);
                    if (_tetoLowerLegR != null)
                        _tetoLowerLegR.localRotation = Quaternion.Euler(-Mathf.Max(0, -legPhase) * kickAngle, 0f, 0f);
                }
            }
            else
            {
                StopWalkAnimation();
                ResetLimbRotations(_happyPlayerModel);
                if (_tetoLegL != null) _tetoLegL.localRotation = Quaternion.identity;
                if (_tetoLegR != null) _tetoLegR.localRotation = Quaternion.identity;
                if (_tetoLowerLegL != null) _tetoLowerLegL.localRotation = Quaternion.identity;
                if (_tetoLowerLegR != null) _tetoLowerLegR.localRotation = Quaternion.identity;
                _player.transform.position = new Vector3(RoadX - 1f, 1f, HappyEndZ);
                _happyPhase = 1;
                _happyPhaseTimer = 0;
            }

            Vector3 refPos = _tetoRoot != null ? _tetoRoot.transform.position : _player.transform.position;
            Vector3 mid = (_player.transform.position + refPos) * 0.5f;
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = mid + new Vector3(-6, 4.5f, -9);
                _mainCamera.transform.LookAt(mid + Vector3.up * 1.8f);
            }

            yield return null;
        }

        FaceEachOther();
        yield return new WaitForSeconds(0.8f);

        _happyPhase = 2;
        _happyPhaseTimer = 0;
        _happyJumpCount = 0;

        var wb = WorldBuilder.Instance;
        if (wb != null)
        {
            Vector3 fwCenter = _tetoRoot != null
                ? (_player.transform.position + _tetoRoot.transform.position) * 0.5f + Vector3.up * 4f
                : _player.transform.position + Vector3.up * 4f;
            RandomEventManager.Instance?.PlayFireworks(fwCenter, wb.WorldRoot?.transform, 8);
        }

        while (_happyPhaseTimer < 2.4f)
        {
            _happyPhaseTimer += Time.deltaTime;

            if (_tetoRoot != null)
            {
                float skip = Mathf.Sin(_happyPhaseTimer * 10f);
                float bounce = Mathf.Pow(Mathf.Max(0, skip), 2) * 0.25f;
                float armSwing = skip * ArmSwingAngle;
                _tetoRoot.transform.position = new Vector3(_tetoRoot.transform.position.x, 1f + bounce, _tetoRoot.transform.position.z);
                if (_tetoLeftArm != null)
                    _tetoLeftArm.localRotation = Quaternion.Euler(armSwing, 0f, -15f);
                if (_tetoRightArm != null)
                    _tetoRightArm.localRotation = Quaternion.Euler(-armSwing, 0f, 15f);
            }

            if (_happyJumpCount < 2 && _happyPhaseTimer >= 0.35f && _happyPhaseTimer - Time.deltaTime < 0.35f)
            {
                SpawnHeart(_tetoRoot != null ? _tetoRoot.transform.position : Vector3.zero);
                _happyJumpCount++;
            }
            if (_happyJumpCount < 2 && _happyPhaseTimer >= 1.15f && _happyPhaseTimer - Time.deltaTime < 1.15f)
            {
                SpawnHeart(_tetoRoot != null ? _tetoRoot.transform.position : Vector3.zero);
                _happyJumpCount++;
            }

            Vector3 refPos2 = _tetoRoot != null ? _tetoRoot.transform.position : _player.transform.position;
            if (_mainCamera != null)
            {
                Vector3 mid2 = (_player.transform.position + refPos2) * 0.5f;
                _mainCamera.transform.position = new Vector3(RoadX, mid2.y + 1.6f, mid2.z - 4f);
                _mainCamera.transform.rotation = Quaternion.identity;
            }

            yield return null;
        }

        if (_tetoLeftArm != null)
            _tetoLeftArm.localRotation = Quaternion.Euler(0f, 0f, -15f);
        if (_tetoRightArm != null)
            _tetoRightArm.localRotation = Quaternion.Euler(0f, 0f, 15f);

        _happyPhase = 3;
        ShowHappyEndingUI();

        float enterWait = 0;
        while (enterWait < 60f)
        {
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                break;

            Vector3 refPos3 = _tetoRoot != null ? _tetoRoot.transform.position : _player.transform.position;
            if (_mainCamera != null)
            {
                Vector3 mid3 = (_player.transform.position + refPos3) * 0.5f;
                _mainCamera.transform.position = new Vector3(RoadX, mid3.y + 1.6f, mid3.z - 4f);
                _mainCamera.transform.rotation = Quaternion.identity;
            }

            enterWait += Time.deltaTime;
            yield return null;
        }

        HideSkipButton();
        DestroyHappyEndingUI();
        CleanupHearts();
        if (_tetoRoot != null)
        {
            Destroy(_tetoRoot);
            _tetoRoot = null;
            _tetoBody = null;
            _tetoLeftArm = null;
            _tetoRightArm = null;
            _tetoLegL = null;
            _tetoLegR = null;
            _tetoLowerLegL = null;
            _tetoLowerLegR = null;
        }
        StopWalkAnimation();
        ResetLimbRotations(_happyPlayerModel);
        _happyPlayerModel = null;
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();
        RestorePlayerControl();
        ShowHUD();

        if (onComplete != null)
        {
            _previewOnComplete = null;
            onComplete();
        }
        else
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_uiManager != null)
                _uiManager.ShowMessage(Localization.T("Tiếp tục cuộc phiêu lưu!"), 2);
        }
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
            if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = _savedTimeSpeed;
        }
    }

    // ═══════════════════════════════════════════════
    //  WALK ANIMATION
    // ═══════════════════════════════════════════════

    private IEnumerator WalkAnimation(GameObject model, float walkSpeed)
    {
        if (model == null) yield break;
        var legL = model.transform.Find("LegL");
        var legR = model.transform.Find("LegR");
        var armL = model.transform.Find("ArmL");
        var armR = model.transform.Find("ArmR");

        if (legL == null && legR == null && armL == null && armR == null) yield break;

        float freq = walkSpeed * 1.8f;
        float legAngle = 25f;
        float armAngle = 15f;

        while (model != null)
        {
            float theta = Time.time * freq;
            float sinVal = Mathf.Sin(theta);

            if (legL != null) legL.localRotation = Quaternion.Euler(sinVal * legAngle, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-sinVal * legAngle, 0f, 0f);
            if (armL != null) armL.localRotation = Quaternion.Euler(-sinVal * armAngle, 0f, 0f);
            if (armR != null) armR.localRotation = Quaternion.Euler(sinVal * armAngle, 0f, 0f);

            yield return null;
        }
    }

    private void StopWalkAnimation()
    {
        if (_walkAnimRoutine != null)
        {
            StopCoroutine(_walkAnimRoutine);
            _walkAnimRoutine = null;
        }
    }

    private void ResetLimbRotations(GameObject model)
    {
        if (model == null) return;
        foreach (string name in new[] { "LegL", "LegR", "ArmL", "ArmR" })
        {
            var t = model.transform.Find(name);
            if (t != null) t.localRotation = Quaternion.identity;
        }
    }

    // ═══════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════

    private void DisablePlayerControl()
    {
        _player = GameManager.Instance?.Player;
        if (_player != null)
        {
            _player.EnableInput(false);
            _player.SetLookRotation(0, 0);
        }
    }

    public void RestorePlayerControl()
    {
        if (_player != null)
        {
            _player.EnableInput(true);
            // Restore real player model if it was hidden for cutscene
            var realModel = _player.transform.Find("PlayerModel");
            if (realModel != null)
                realModel.gameObject.SetActive(true);
        }
        AttachCamera();
    }

    private void DetachCamera()
    {
        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _cameraFollow = _mainCamera.GetComponent<CameraFollow>();
            if (_cameraFollow != null)
                _cameraFollow.enabled = false;
        }
    }

    public void AttachCamera()
    {
        if (_mainCamera != null && _cameraFollow != null)
            _cameraFollow.enabled = true;
    }

    private void HideHUD()
    {
        if (_uiManager != null)
        {
            _uiManager.ShowAllGameUI(false);
            _uiManager.SetStatsBgVisible(false);
        }
    }

    private void ShowHUD()
    {
        if (_uiManager != null)
        {
            _uiManager.ShowAllGameUI(true);
            _uiManager.SetStatsBgVisible(true);
        }
    }

    private void FinishEndingScene(System.Action onComplete, string endTitle, string endContent)
    {
        _previewOnComplete = null;
        if (onComplete != null)
        {
            RestorePlayerControl();
            ShowHUD();
            onComplete();
            return;
        }
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();
        if (_uiManager != null)
            _uiManager.ShowEndScreen(endTitle, endContent);
    }

    // ── Overlay ──

    private IEnumerator CreateFadeOverlay()
    {
        if (_overlay != null) yield break;
        if (_canvas == null)
            _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
        {
            var canvasGO = new GameObject("CutsceneCanvas");
            canvasGO.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            _canvas = canvasGO.GetComponent<Canvas>();
        }
        _overlay = new GameObject("CutsceneOverlay");
        _overlay.transform.SetParent(_canvas.transform, false);
        _overlayImage = _overlay.AddComponent<Image>();
        _overlayImage.color = new Color(0, 0, 0, 1);
        var rt = _overlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _overlay.SetActive(true);
        yield return null;
    }

    private IEnumerator FadeOverlay(float targetAlpha, float duration)
    {
        if (_overlayImage == null) yield break;
        _overlay.SetActive(true);
        float startA = _overlayImage.color.a;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(startA, targetAlpha, elapsed / duration);
            _overlayImage.color = new Color(0, 0, 0, a);
            yield return null;
        }
        _overlayImage.color = new Color(0, 0, 0, targetAlpha);
        if (targetAlpha <= 0)
            _overlay.SetActive(false);
    }

    private void DestroyOverlay()
    {
        if (_overlay != null) { Destroy(_overlay); _overlay = null; _overlayImage = null; }
    }

    // ── Letterbox bars ──

    private void CreateLetterboxBars()
    {
        if (_canvas == null)
            _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null) return;
        DestroyLetterboxBars();
        _letterTop = CreateLetterbar("LetterboxTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        _letterBottom = CreateLetterbar("LetterboxBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
    }

    private GameObject CreateLetterbar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var img = go.AddComponent<Image>();
        img.color = Color.black;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(0, Screen.height * 0.12f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    private void DestroyLetterboxBars()
    {
        if (_letterTop != null) { Destroy(_letterTop); _letterTop = null; }
        if (_letterBottom != null) { Destroy(_letterBottom); _letterBottom = null; }
    }

    // ── Happy Ending UI ──

    private void ShowHappyEndingUI()
    {
        if (_happyUI != null) return;
        if (_canvas == null)
            _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null) return;

        _happyUI = new GameObject("HappyEndingUI");
        _happyUI.transform.SetParent(_canvas.transform, false);

        var bg = _happyUI.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        var rt = _happyUI.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var title = MakeUIText("HappyTitle", Localization.T("KẾT THÚC HẠNH PHÚC"), 48, new Color(1f, 0.863f, 0.314f), new Vector2(0, 80));
        var sub = MakeUIText("HappySubtitle", Localization.T("Bạn và Jessica đã đi đến cuối con đường cùng nhau!"), 24, Color.white, new Vector2(0, 20));
        var hint = MakeUIText("HappyHint", Localization.T("Nhấn Enter để tiếp tục chơi"), 18, Color.gray, new Vector2(0, -30));
    }

    private GameObject MakeUIText(string name, string text, int fontSize, Color color, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_happyUI.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            tmp.font = _uiManager.defaultTmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(600, 60);
        return go;
    }

    private void DestroyHappyEndingUI()
    {
        if (_happyUI != null) { Destroy(_happyUI); _happyUI = null; }
    }

    // ── Hearts ──

    private void SpawnHeart(Vector3 position)
    {
        if (_canvas == null) return;

        var heartGO = new GameObject("Heart");
        heartGO.transform.SetParent(_canvas.transform, false);
        var heart = heartGO.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            heart.font = _uiManager.defaultTmpFont;
        heart.text = "♥";
        heart.fontSize = 48;
        heart.color = new Color(1f, 0.314f, 0.471f);
        heart.alignment = TextAlignmentOptions.Center;

        var rt = heartGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(60, 60);

        _hearts.Add(heartGO);
        _heartRoutine = StartCoroutine(AnimateHeart(heartGO));
    }

    private IEnumerator AnimateHeart(GameObject heart)
    {
        float dur = 1f;
        float elapsed = 0;
        Vector3 startScl = Vector3.one;
        Vector3 endScl = Vector3.one * 2f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / dur;
            heart.transform.localScale = Vector3.Lerp(startScl, endScl, p);
            var rt = heart.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0, 200), 1 - Mathf.Pow(1 - p, 2));
            yield return null;
        }

        _hearts.Remove(heart);
        if (heart != null) Destroy(heart);
    }

    private void CleanupHearts()
    {
        foreach (var h in _hearts)
        {
            if (h != null) Destroy(h);
        }
        _hearts.Clear();
    }

    // ── Block spawning ──

    private GameObject CreateBlock(Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = scale;
        go.transform.position = position;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private GameObject CreateBlock(Transform parent, Vector3 scale, Vector3 localPos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = localPos;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private void RegisterSpawned(GameObject go)
    {
        _spawned.Add(go);
    }

    private void CleanupSpawned()
    {
        foreach (var go in _spawned)
        {
            if (go != null) Destroy(go);
        }
        _spawned.Clear();
    }

    private void CleanupAll()
    {
        HideSkipButton();
        StopSteeringAnim();
        StopMainMenuVisual();
        CleanupSpawned();
        DestroyDrivingSegments();
        DestroyOverlay();
        DestroyLetterboxBars();
        DestroyHappyEndingUI();
        CleanupHearts();
        if (_prebuilt)
        {
            if (_introCar != null) _introCar.SetActive(false);
        }
        else
        {
            _introCar = null;
            _introPlayer = null;
        }
        _introSteeringWheel = null;
        _introWheels.Clear();
        if (_tetoRoot != null)
        {
            Destroy(_tetoRoot);
            _tetoRoot = null;
            _tetoBody = null;
            _tetoLeftArm = null;
            _tetoRightArm = null;
        }
    }

    // ── Wagon (sad ending) ──

    private Transform CreateWagon(float wx, float wz)
    {
        Color brn = new Color(85f / 255f, 52f / 255f, 22f / 255f);
        Color tan = new Color(210f / 255f, 195f / 255f, 160f / 255f);
        Color dk = new Color(70f / 255f, 42f / 255f, 16f / 255f);
        Color wblk = new Color(45f / 255f, 35f / 255f, 20f / 255f);

        var root = new GameObject("WagonRoot").transform;
        root.position = new Vector3(wx, 0f, wz);

        CreateBlock(root, new Vector3(1.6f, 0.12f, 3.2f), new Vector3(0f, 0.5f, 0f), brn);
        CreateBlock(root, new Vector3(0.08f, 0.5f, 3.2f), new Vector3(-0.8f, 0.8f, 0f), dk);
        CreateBlock(root, new Vector3(0.08f, 0.5f, 3.2f), new Vector3(0.8f, 0.8f, 0f), dk);
        CreateBlock(root, new Vector3(1.6f, 0.6f, 0.08f), new Vector3(0f, 0.85f, -1.6f), dk);
        CreateBlock(root, new Vector3(1.2f, 0.4f, 0.08f), new Vector3(0f, 0.75f, 1.6f), dk);
        CreateBlock(root, new Vector3(1.0f, 0.08f, 0.4f), new Vector3(0f, 0.85f, -0.6f), brn);
        CreateBlock(root, new Vector3(0.06f, 0.35f, 0.06f), new Vector3(-0.4f, 0.67f, -0.6f), brn);
        CreateBlock(root, new Vector3(0.06f, 0.35f, 0.06f), new Vector3(0.4f, 0.67f, -0.6f), brn);
        CreateBlock(root, new Vector3(0.08f, 0.08f, 1.4f), new Vector3(0f, 0.4f, -2.3f), dk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(-0.85f, 0.35f, -1.3f), wblk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(0.85f, 0.35f, -1.3f), wblk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(-0.85f, 0.35f, 1.3f), wblk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(0.85f, 0.35f, 1.3f), wblk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.8f), new Vector3(0f, 0.25f, -1.3f), dk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.8f), new Vector3(0f, 0.25f, 1.3f), dk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.6f), new Vector3(0f, 0.38f, -0.4f), dk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.6f), new Vector3(0f, 0.38f, 0.4f), dk);

        RegisterSpawned(root.gameObject);
        return root;
    }

    // ── Wife NPC (happy ending) ──

    private GameObject CreateTeto(Vector3 position)
    {
        var npc = WifeNPC.BuildWifeNpc(null, position, 1f, Quaternion.identity);
        npc.name = "Jessica";
        RegisterSpawned(npc);
        return npc;
    }

    private void FaceEachOther()
    {
        if (_player == null || _tetoRoot == null) return;
        float z = _player.transform.position.z;
        _player.transform.position = new Vector3(RoadX - 0.9f, _player.transform.position.y, z);
        _player.transform.rotation = Quaternion.Euler(0, 90, 0);
        if (_happyPlayerModel != null)
        {
            _happyPlayerModel.transform.position = new Vector3(RoadX - 0.9f, 0.82f, z);
            _happyPlayerModel.transform.rotation = Quaternion.Euler(0, 90, 0);
        }
        _tetoRoot.transform.position = new Vector3(RoadX + 0.9f, _tetoRoot.transform.position.y, z);
        _tetoRoot.transform.rotation = Quaternion.Euler(0, 90, 0);
    }
}
