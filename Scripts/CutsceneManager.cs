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

    private bool _happyPending;
    private GameObject _tetoRoot;
    private Transform _tetoBody;
    private float _happyElapsed;
    private int _happyPhase;
    private float _happyPhaseTimer;
    private int _happyJumpCount;
    private readonly List<GameObject> _hearts = new List<GameObject>();
    private GameObject _happyUI;
    private GameObject _skipButton;

    private const float RoadX = 14f;
    private const float IntroStartZ = -55f;
    private const float IntroEndZ = -5f;
    private const float SadStartZ = 25f;
    private const float SadEndZ = -35f;
    private const float HappyStartZ = -18f;
    private const float HappyEndZ = 30f;
    private const float WalkSpeed = 4.5f;
    private const float SwingSpeed = 2.8f;
    private const float LateralSwing = 1.6f;
    private const float JumpHeight = 0.55f;
    private const float JumpSpeed = 9f;

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
        tmp.text = "B\x1ecf qua [ESC]";
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Right;
        var rt = _skipButton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -20);
        rt.sizeDelta = new Vector2(200, 40);
        _skipButton.SetActive(false);
    }

    private void ShowSkipButton()
    {
        if (_canvas == null)
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
        _cutsceneRoutine = StartCoroutine(IntroRoutine(onComplete));
    }

    public void PlaySadEnding()
    {
        if (IsActive) return;
        IsActive = true;
        _cutsceneRoutine = StartCoroutine(SadEndingRoutine());
    }

    public void RequestHappyEnding()
    {
        if (IsActive || _happyPending) return;
        _happyPending = true;
    }

    public void CancelCutscene()
    {
        if (_cutsceneRoutine != null)
        {
            StopCoroutine(_cutsceneRoutine);
            _cutsceneRoutine = null;
        }
        CleanupAll();
        RestorePlayerControl();
        IsActive = false;
    }

    private IEnumerator PendingCheckLoop()
    {
        while (true)
        {
            if (_happyPending && !IsActive)
            {
                _happyPending = false;
                IsActive = true;
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
        float rideDur = 4.5f;
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        DisablePlayerControl();
        DetachCamera();
        HideHUD();

        if (_mainCamera != null)
        {
            _mainCamera.transform.position = new Vector3(RoadX - 12, 2.2f, IntroEndZ);
            _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, IntroStartZ + 15));
        }

        yield return StartCoroutine(CreateFadeOverlay());
        ShowSkipButton();
        yield return StartCoroutine(FadeOverlay(0, 1.5f));

        float deltaZ = IntroEndZ - IntroStartZ;

        var car = CreateBlock(new Vector3(2f, 0.6f, 4f), new Vector3(RoadX, 0.3f, IntroStartZ), new Color(0.2f, 0.6f, 1f));
        CreateBlock(car.transform, new Vector3(1.6f, 0.4f, 2f), new Vector3(0, 0.5f, -0.2f), new Color(0.3f, 0.3f, 0.8f));
        foreach (var ox in new[] { -0.9f, 0.9f })
        {
            CreateBlock(car.transform, new Vector3(0.2f, 0.4f, 0.4f), new Vector3(ox, -0.2f, -1.5f), Color.black);
            CreateBlock(car.transform, new Vector3(0.2f, 0.4f, 0.4f), new Vector3(ox, -0.2f, 1.5f), Color.black);
        }
        RegisterSpawned(car);

        float t = 0;
        Vector3 camStart = new Vector3(RoadX - 12, 2.2f, IntroEndZ);
        Vector3 lookStart = new Vector3(RoadX, 1f, IntroStartZ + 15);
        Vector3 lookEnd = new Vector3(RoadX, 1f, IntroStartZ + 3);

        while (t < rideDur)
        {
            t += Time.deltaTime;
            float p = t / rideDur;
            if (car != null)
                car.transform.position = new Vector3(RoadX, 0.3f, IntroStartZ + deltaZ * p);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX - 9, 2.5f, IntroEndZ - 3), p);
                _mainCamera.transform.LookAt(Vector3.Lerp(lookStart, lookEnd, p));
            }
            yield return null;
        }

        if (car != null)
            car.transform.position = new Vector3(RoadX, 0.3f, IntroEndZ);

        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(FadeOverlay(1, 1.5f));
        DestroyOverlay();

        HideSkipButton();
        CleanupSpawned();
        ShowHUD();
        RestorePlayerControl();
        IsActive = false;
        _cutsceneRoutine = null;

        onComplete?.Invoke();

        if (onComplete == null && _uiManager != null && GameManager.Instance != null && !GameManager.Instance.InGame)
            _uiManager.ShowMainMenu(true);
    }

    // ═══════════════════════════════════════════════
    //  SAD ENDING
    // ═══════════════════════════════════════════════

    private IEnumerator SadEndingRoutine()
    {
        float rideDur = 8f;
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        DisablePlayerControl();
        DetachCamera();
        HideHUD();

        yield return StartCoroutine(CreateFadeOverlay());
        CreateLetterboxBars();
        ShowSkipButton();
        yield return StartCoroutine(FadeOverlay(0, 1.5f));

        if (_mainCamera != null)
        {
            _mainCamera.transform.position = new Vector3(RoadX + 5, 2.8f, SadStartZ + 3);
            _mainCamera.transform.LookAt(new Vector3(RoadX, 1.4f, SadStartZ));
        }

        float wx = RoadX, wz = SadStartZ;
        float deltaZ = SadEndZ - SadStartZ;

        var wagonParts = CreateWagon(wx, wz);
        var groom = CreateGroom(wx + 0.35f, 1.05f, wz);
        var wife = MapBuilder.BuildWifeNpc(null, new Vector3(wx - 0.3f, 1.05f, wz), 1f, Quaternion.identity);
        RegisterSpawned(wife);

        float t = 0;
        float wallTimeout = rideDur * 3f;
        while (t < rideDur && wallTimeout > 0)
        {
            t += Time.deltaTime;
            wallTimeout -= Time.deltaTime;
            float p = Mathf.Min(t / rideDur, 1f);
            float z = wz + deltaZ * p;

            foreach (var part in wagonParts)
            {
                if (part != null)
                {
                    var pos = part.transform.position;
                    part.transform.position = new Vector3(pos.x, pos.y, z + (pos.z - wz));
                }
            }
            if (groom.body != null)
            {
                var gp = groom.body.transform.position;
                groom.body.transform.position = new Vector3(gp.x, gp.y, z + (gp.z - wz));
            }
            if (groom.head != null)
            {
                var hp = groom.head.transform.position;
                groom.head.transform.position = new Vector3(hp.x, hp.y, z + (hp.z - wz));
            }
            if (wife != null)
            {
                var wp = wife.transform.position;
                wife.transform.position = new Vector3(wp.x, wp.y, z + (wp.z - wz));
            }

            if (_mainCamera != null)
            {
                float lagZ = 3f + 22f * p;
                float camY = 2.8f + 0.4f * p;
                float lookY = 1.4f - 0.4f * p;
                _mainCamera.transform.position = new Vector3(RoadX + 5, camY, z + lagZ);
                _mainCamera.transform.LookAt(new Vector3(RoadX, lookY, z));
            }

            yield return null;
        }

        yield return StartCoroutine(FadeOverlay(1, 2f));

        HideSkipButton();
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();

        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();
        if (_uiManager != null)
            _uiManager.ShowEndScreen("KẾT THÚC ĐAU BUỒN", "\"Skibidi.\ndop dop.\"");

        IsActive = false;
        _cutsceneRoutine = null;
    }

    // ═══════════════════════════════════════════════
    //  HAPPY ENDING
    // ═══════════════════════════════════════════════

    private IEnumerator HappyEndingRoutine()
    {
        _player = GameManager.Instance?.Player;
        if (_player == null)
        {
            IsActive = false;
            _cutsceneRoutine = null;
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

        _tetoRoot = CreateTeto(new Vector3(RoadX + 0.8f, 1f, HappyStartZ - 1.5f));
        _tetoBody = _tetoRoot?.transform.Find("TetoBody");

        _happyPhase = 0;
        _happyElapsed = 0;

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

                float side = Mathf.Sin(_happyElapsed * SwingSpeed);
                float tz = _player.transform.position.z - 1.2f + Mathf.Cos(_happyElapsed * SwingSpeed) * 0.3f;
                float tx = RoadX + side * LateralSwing;
                if (_tetoRoot != null)
                {
                    _tetoRoot.transform.position = new Vector3(tx, 1f, tz);
                    _tetoRoot.transform.rotation = Quaternion.Euler(0, side >= 0 ? 10 : -10, 0);
                }
                if (_tetoBody != null)
                {
                    float jump = Mathf.Pow(Mathf.Max(0, Mathf.Sin(_happyElapsed * JumpSpeed)), 2) * JumpHeight;
                    _tetoBody.localPosition = new Vector3(0, 0.85f + jump, 0);
                }
            }
            else
            {
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

        while (_happyPhaseTimer < 2.4f)
        {
            _happyPhaseTimer += Time.deltaTime;

            if (_tetoBody != null)
            {
                float jh = Mathf.Pow(Mathf.Max(0, Mathf.Sin(_happyPhaseTimer * 8f)), 2) * 0.7f;
                _tetoBody.localPosition = new Vector3(0, 0.85f + jh, 0);
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

        if (_tetoBody != null)
            _tetoBody.localPosition = new Vector3(0, 0.85f, 0);

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
        }
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();
        RestorePlayerControl();
        ShowHUD();
        IsActive = false;
        _cutsceneRoutine = null;

        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();
        if (_uiManager != null)
            _uiManager.ShowMessage("Tiếp tục cuộc phiêu lưu!", 2);
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

    private void RestorePlayerControl()
    {
        if (_player != null)
            _player.EnableInput(true);
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

    private void AttachCamera()
    {
        if (_mainCamera != null && _cameraFollow != null)
            _cameraFollow.enabled = true;
    }

    private void HideHUD()
    {
        if (_uiManager != null) _uiManager.ShowAllGameUI(false);
    }

    private void ShowHUD()
    {
        if (_uiManager != null) _uiManager.ShowAllGameUI(true);
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
        var go = new GameObject(name);
        go.transform.SetParent(_canvas.transform, false);
        var img = go.AddComponent<Image>();
        img.color = Color.black;
        var rt = go.AddComponent<RectTransform>();
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
        if (_canvas == null) return;

        _happyUI = new GameObject("HappyEndingUI");
        _happyUI.transform.SetParent(_canvas.transform, false);

        var bg = _happyUI.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        var rt = _happyUI.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var title = MakeUIText("HappyTitle", "HAPPY ENDING", 48, new Color(1f, 0.863f, 0.314f), new Vector2(0, 80));
        var sub = MakeUIText("HappySubtitle", "Bạn và Teto đã cùng nhau đến cuối con đường!", 24, Color.white, new Vector2(0, 20));
        var hint = MakeUIText("HappyHint", "Nhấn Enter để tiếp tục chơi", 18, Color.gray, new Vector2(0, -30));
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
        StartCoroutine(AnimateHeart(heartGO));
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
        CleanupSpawned();
        DestroyOverlay();
        DestroyLetterboxBars();
        DestroyHappyEndingUI();
        CleanupHearts();
        if (_tetoRoot != null)
        {
            Destroy(_tetoRoot);
            _tetoRoot = null;
            _tetoBody = null;
        }
    }

    // ── Wagon (sad ending) ──

    private List<GameObject> CreateWagon(float wx, float wz)
    {
        var parts = new List<GameObject>();
        float wy = 0;
        Color brn = new Color(85f / 255f, 52f / 255f, 22f / 255f);
        Color tan = new Color(210f / 255f, 195f / 255f, 160f / 255f);
        Color dk = new Color(70f / 255f, 42f / 255f, 16f / 255f);
        Color wblk = new Color(45f / 255f, 35f / 255f, 20f / 255f);

        parts.Add(CreateBlock(new Vector3(1.8f, 0.3f, 3.6f), new Vector3(wx, wy + 0.65f, wz), brn));
        parts.Add(CreateBlock(new Vector3(1.6f, 0.08f, 3.4f), new Vector3(wx, wy + 1.4f, wz), tan));
        parts.Add(CreateBlock(new Vector3(1.8f, 0.7f, 0.18f), new Vector3(wx, wy + 1f, wz - 1.65f), dk));
        parts.Add(CreateBlock(new Vector3(1.8f, 0.7f, 0.18f), new Vector3(wx, wy + 1f, wz + 1.65f), dk));

        float[] oxs = { -0.9f, 0.9f, -0.9f, 0.9f };
        float[] ozs = { -1.5f, -1.5f, 1.5f, 1.5f };
        for (int wi = 0; wi < 4; wi++)
            parts.Add(CreateBlock(new Vector3(0.2f, 0.8f, 0.8f), new Vector3(wx + oxs[wi], wy + 0.4f, wz + ozs[wi]), wblk));

        foreach (var p in parts) RegisterSpawned(p);
        return parts;
    }

    private (GameObject body, GameObject head) CreateGroom(float x, float y, float z)
    {
        var body = CreateBlock(new Vector3(0.48f, 0.82f, 0.32f), new Vector3(x, y, z), new Color(28f / 255f, 30f / 255f, 65f / 255f));
        var head = CreateBlock(new Vector3(0.4f, 0.4f, 0.4f), new Vector3(x, y + 0.6f, z), new Color(220f / 255f, 178f / 255f, 132f / 255f));
        RegisterSpawned(body);
        RegisterSpawned(head);
        return (body, head);
    }

    // ── Teto (happy ending) ──

    private GameObject CreateTeto(Vector3 position)
    {
        var root = new GameObject("Teto");
        root.transform.position = position;

        var body = CreateBlock(root.transform, new Vector3(0.85f, 1.7f, 0.85f), new Vector3(0, 0.85f, 0), new Color(0.863f, 0.314f, 0.471f));
        body.name = "TetoBody";
        CreateBlock(root.transform, new Vector3(0.75f, 0.75f, 0.75f), new Vector3(0, 2.15f, 0), new Color(0.863f, 0.698f, 0.518f));
        CreateBlock(root.transform, new Vector3(0.85f, 0.2f, 0.85f), new Vector3(0, 2.55f, 0), new Color(0.1f, 0.1f, 0.12f));

        RegisterSpawned(root);
        return root;
    }

    private void FaceEachOther()
    {
        if (_player == null || _tetoRoot == null) return;
        float z = _player.transform.position.z;
        _player.transform.position = new Vector3(RoadX - 0.9f, _player.transform.position.y, z);
        _player.transform.rotation = Quaternion.Euler(0, 90, 0);
        _tetoRoot.transform.position = new Vector3(RoadX + 0.9f, _tetoRoot.transform.position.y, z);
        _tetoRoot.transform.rotation = Quaternion.Euler(0, -90, 0);
    }
}
