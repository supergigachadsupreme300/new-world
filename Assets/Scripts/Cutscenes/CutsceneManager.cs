using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(-100)]
public partial class CutsceneManager : MonoSingleton<CutsceneManager>
{
    /// <summary>
    /// Genre change: the game is now an open-world action RPG with no narrative
    /// endings. When true (default) all ending entry points are disabled and
    /// only log, so the original ending routines remain preserved in
    /// Scripts/_Archived/CutsceneManager for any future re-implementation.
    /// </summary>
    public bool RemoveEndings = true;

    /// <summary>
    /// When endings are removed this is called instead of playing a cutscene.
    /// Keeps every existing call site compiling while guaranteeing no ending
    /// can ever trigger during normal play.
    /// </summary>
    public void EndingsRemoved(System.Action onComplete)
    {
        Debug.Log("[CutsceneManager] Ending cutscenes are disabled for the open-world RPG genre.");
        onComplete?.Invoke();
    }

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
    private bool _fatedPending;
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
    private GameObject _subtitleGO;
    private GameObject _skipButton;
    private Coroutine _walkAnimRoutine;
    private GameObject _demonUI;
    private TextMeshProUGUI _demonCaption;

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
        _uiManager?.ApplyDefaultFont(tmp);
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
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(SadEndingRoutine(onComplete));
    }

    public void PlayBossBadEnding(System.Action onComplete = null)
    {
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(BossBadEndingRoutine(onComplete));
    }

    public void PlayJusticeEnding(System.Action onComplete = null)
    {
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
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
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
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
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(DemonEndingRoutine(onComplete));
    }

    public void PlayHappyEnding(System.Action onComplete = null)
    {
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(HappyEndingRoutine(onComplete));
    }

    public void PlayNtrEnding(System.Action onComplete = null)
    {
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(NtrRoutine(onComplete));
    }

    public void RequestHappyEnding()
    {
        if (RemoveEndings) { EndingsRemoved(null); return; }
        if (IsActive || _happyPending) return;
        _happyPending = true;
    }

    public void RequestNtrEnding()
    {
        if (RemoveEndings) { EndingsRemoved(null); return; }
        if (IsActive || _ntrPending) return;
        _ntrPending = true;
    }

    public void PlayFatedEnding(System.Action onComplete = null)
    {
        if (RemoveEndings) { EndingsRemoved(onComplete); return; }
        if (IsActive) return;
        IsActive = true;
        _previewOnComplete = onComplete;
        _cutsceneStartTime = Time.time;
        _cutsceneRoutine = StartCoroutine(FatedEndingRoutine(onComplete));
    }

    public void RequestFatedEnding()
    {
        if (RemoveEndings) { EndingsRemoved(null); return; }
        if (IsActive || _fatedPending) return;
        _fatedPending = true;
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
        else if (GameManager.Instance != null)
            GameManager.Instance.ShowMainMenu(true);
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
            if (_fatedPending && !IsActive)
            {
                _fatedPending = false;
                IsActive = true;
                _cutsceneStartTime = Time.time;
                _cutsceneRoutine = StartCoroutine(FatedEndingRoutine());
            }
            else if (_ntrPending && !IsActive)
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

    private void RestoreAfterPreview()
    {
        RestorePlayerControl();
        if (GameManager.Instance != null && GameManager.Instance.InGame)
            ShowHUD();
        else if (GameManager.Instance != null)
            GameManager.Instance.ShowMainMenu(true);
    }

    private void FinishEndingScene(System.Action onComplete, string endTitle, string endContent)
    {
        _previewOnComplete = null;
        if (onComplete != null)
        {
            RestoreAfterPreview();
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
        DestroySubtitle();
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
}
