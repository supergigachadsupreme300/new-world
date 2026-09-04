using UnityEngine;
using UnityEngine.InputSystem;

public class FishingController : MonoSingleton<FishingController>
{
    public enum FishState { Idle, Casting, Waiting, FishApproaching, HookShaking, Reeling, Success, Fail }

    public static bool IsFishingActive => Instance != null && Instance.State != FishState.Idle;

    public FishState State = FishState.Idle;
    public float CastArcSpeed = 25f;
    public float ShakeDuration = 1.5f;
    public float FishApproachSpeed = 2f;
    public float FlopChance = 0.4f;

    private float[] _effectiveWeights;

    private const float CastGravity = 4f;

    private GameObject _hook;
    private Rigidbody _hookRb;
    private float _castStartTime;
    private float _castTimeout;
    private Vector3 _hookTarget;
    private GameObject _fishShadow;
    private float _timer;
    private float _shakeIntensity = 0.2f;
    private FishingUI _fishingUI;
    private Canvas _canvas;
    private UIManager _uiManager;
    private bool _wheelGrabbed;
    private bool _wheelWasGrabbed;
    private float _lastWheelAngle;

    private int _currentFishIndex = -1;
    private float _currentFlop;
    private int _baitUsed; // 0 none, 1 bait, 2 chum

    private static readonly string[] FishTypes = { "fish_carp", "fish_salmon", "fish_tuna", "fish_pufferfish" };
    private static readonly string[] FishLabels = { "Cá Chép", "Cá Hồi", "Cá Ngừ", "Cá Nóc" };

    // Per-species reeling difficulty (Phase 3A). Higher tier = harder.
    private struct FishData
    {
        public float Weight;
        public float Flop;
        public float ReelMul;
        public float ZoneMul;
    }
    private static readonly FishData[] FishStats =
    {
        new FishData { Weight = 40f, Flop = 0.30f, ReelMul = 1.00f, ZoneMul = 1.00f }, // carp - easy
        new FishData { Weight = 30f, Flop = 0.40f, ReelMul = 0.92f, ZoneMul = 1.10f }, // salmon
        new FishData { Weight = 20f, Flop = 0.52f, ReelMul = 0.82f, ZoneMul = 1.28f }, // tuna
        new FishData { Weight = 10f, Flop = 0.62f, ReelMul = 0.72f, ZoneMul = 1.45f }  // pufferfish - hardest
    };

    private static float[] FishWeights
    {
        get
        {
            var w = new float[FishStats.Length];
            for (int i = 0; i < w.Length; i++) w[i] = FishStats[i].Weight;
            return w;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _uiManager = GameManager.Instance?.UIManager;
        var hudCanvasGo = GameObject.Find("HUD_Canvas");
        _canvas = hudCanvasGo != null ? hudCanvasGo.GetComponent<Canvas>() : FindObjectOfType<Canvas>();
        if (_canvas == null)
        {
            var canvasGo = new GameObject("FishingCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        var uiGo = new GameObject("FishingUI");
        uiGo.transform.SetParent(transform);
        _fishingUI = uiGo.AddComponent<FishingUI>();
        _fishingUI.Create(_canvas);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return;

        switch (State)
        {
            case FishState.Casting: UpdateCasting(); break;
            case FishState.Waiting: UpdateWaiting(); break;
            case FishState.FishApproaching: UpdateFishApproaching(); break;
            case FishState.HookShaking: UpdateHookShaking(); break;
            case FishState.Reeling: UpdateReeling(); break;
        }
    }

    private void FixedUpdate()
    {
        if (State != FishState.Casting || _hookRb == null)
            return;

        _hookRb.AddForce(Vector3.down * (CastGravity * _hookRb.mass), ForceMode.Force);
    }

    public void TryStartFishing()
    {
        if (State != FishState.Idle) return;

        var player = GameManager.Instance?.Player;
        if (player == null || player.InWater) return;

        var cam = Camera.main;
        if (cam == null) return;

        if (player.transform.position.x > -140f)
        {
            _uiManager?.ShowMessage(Localization.T("Cần đến gần biển để câu cá!"), 2f);
            return;
        }

        float t = (0.1f - cam.transform.position.y) / cam.transform.forward.y;
        if (t <= 0f)
        {
            _uiManager?.ShowMessage(Localization.T("Ngắm xuống mặt nước!"), 2f);
            return;
        }

        Vector3 waterPoint = cam.transform.position + cam.transform.forward * t;
        if (waterPoint.x > -210f)
        {
            _uiManager?.ShowMessage(Localization.T("Ngắm ra xa hơn về phía biển!"), 2f);
            return;
        }

        _hookTarget = waterPoint;
        _hookTarget.y = 0.1f;

        var tm = ToolManager.Instance;
        _baitUsed = 0;
        _effectiveWeights = FishWeights;
        if (tm != null)
        {
            if (tm.CountItem("fishing_chum") > 0)
            {
                tm.RemoveItemAmount("fishing_chum", 1);
                _baitUsed = 2;
                _effectiveWeights[3] *= 2f;
                _uiManager?.ShowMessage(Localization.T("Đã dùng Mồi Bả!"), 1.5f);
            }
            else if (tm.CountItem("fishing_bait") > 0)
            {
                tm.RemoveItemAmount("fishing_bait", 1);
                _baitUsed = 1;
                _uiManager?.ShowMessage(Localization.T("Đã dùng Mồi Câu!"), 1.5f);
            }
        }

        _hook = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _hook.name = "FishingHook";
        _hook.transform.localScale = Vector3.one * 0.2f;
        _hook.GetComponent<Renderer>().material.color = new Color(0.7f, 0.7f, 0.7f);

        var hookCollider = _hook.GetComponent<SphereCollider>();
        hookCollider.isTrigger = true;

        _hookRb = _hook.AddComponent<Rigidbody>();
        _hookRb.useGravity = false;
        _hookRb.isKinematic = false;
        _hookRb.interpolation = RigidbodyInterpolation.Interpolate;
        _hookRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _hookRb.linearDamping = 0f;
        _hookRb.mass = 0.1f;

        var hookOrigin = cam.transform.position + cam.transform.forward * 1.5f;
        hookOrigin.y = 0.5f;
        _hook.transform.position = hookOrigin;

        Vector3 flatTarget = _hookTarget - hookOrigin;
        flatTarget.y = 0f;
        float flightTime = Mathf.Clamp(flatTarget.magnitude / CastArcSpeed, 1f, 4f);
        Vector3 launchVelocity = flatTarget / flightTime;
        launchVelocity.y = (_hookTarget.y - hookOrigin.y + 0.5f * CastGravity * flightTime * flightTime) / flightTime;
        _hookRb.linearVelocity = launchVelocity;
        _castStartTime = Time.time;
        _castTimeout = flightTime + 1.5f;

        player.EnableInput(false);
        State = FishState.Casting;
    }

    private void UpdateCasting()
    {
        if (_hook == null) { CancelFishing(); return; }

        if (_hook.transform.position.y <= 0.1f || Time.time - _castStartTime > _castTimeout)
        {
            _hook.transform.position = _hookTarget;
            _hookRb.linearVelocity = Vector3.zero;
            _hookRb.isKinematic = true;
            State = FishState.Waiting;
            _timer = Random.Range(3f, 6f);
        }
    }

    private void UpdateWaiting()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            var player = GameManager.Instance?.Player;
            if (player == null) { CancelFishing(); return; }

            _fishShadow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _fishShadow.name = "FishShadow";
            _fishShadow.transform.localScale = new Vector3(0.3f, 0.05f, 0.5f);
            _fishShadow.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.15f, 0.4f);
            Object.Destroy(_fishShadow.GetComponent<Collider>());

            Vector3 approachDir = Random.insideUnitSphere.normalized;
            approachDir.y = 0f;
            float dist = Random.Range(3f, 6f);
            _fishShadow.transform.position = _hookTarget + approachDir * dist;
            _fishShadow.transform.position = new Vector3(_fishShadow.transform.position.x, 0.05f, _fishShadow.transform.position.z);

            State = FishState.FishApproaching;
        }
    }

    private void UpdateFishApproaching()
    {
        if (_fishShadow == null) { CancelFishing(); return; }

        _fishShadow.transform.position = Vector3.MoveTowards(
            _fishShadow.transform.position,
            new Vector3(_hookTarget.x, 0.05f, _hookTarget.z),
            FishApproachSpeed * Time.deltaTime);

        if (Vector3.Distance(_fishShadow.transform.position, new Vector3(_hookTarget.x, 0.05f, _hookTarget.z)) < 0.3f)
        {
            Object.Destroy(_fishShadow);
            _fishShadow = null;
            State = FishState.HookShaking;
            _timer = ShakeDuration;
        }
    }

    private void UpdateHookShaking()
    {
        if (_hook == null) { CancelFishing(); return; }

        float offset = Mathf.Sin(Time.time * 30f) * _shakeIntensity;
        _hook.transform.localPosition = new Vector3(
            _hookTarget.x + offset,
            _hookTarget.y + Mathf.Sin(Time.time * 25f) * _shakeIntensity * 0.5f,
            _hookTarget.z + Mathf.Cos(Time.time * 28f) * _shakeIntensity * 0.5f);

        _timer -= Time.deltaTime;

        if ((!GameInput.IsMobile && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            MobileInputController.Consume("use"))
        {
            StartReeling();
            return;
        }

        if (_timer <= 0f)
        {
            _uiManager?.ShowMessage(Localization.T("Cá thoát rồi!"), 2f);
            CancelFishing();
            return;
        }
    }

    private void StartReeling()
    {
        State = FishState.Reeling;
        _wheelGrabbed = false;
        _wheelWasGrabbed = false;
        _lastWheelAngle = 0f;

        _currentFishIndex = PickFishType();

        float flop = FishStats[_currentFishIndex].Flop;
        if (SkillManager.Instance != null)
            flop *= SkillManager.Instance.FishingFlopMultiplier();
        if (FishingProgression.Instance != null)
            flop *= FishingProgression.Instance.RodFlopMultiplier();
        if (_baitUsed == 2) flop = 0.1f;
        else if (_baitUsed == 1) flop = 0.2f;
        _currentFlop = Mathf.Clamp01(flop);

        float skillReel = SkillManager.Instance != null ? SkillManager.Instance.FishingReelMultiplier() : 1f;
        float rodReel = FishingProgression.Instance != null ? FishingProgression.Instance.RodReelMultiplier() : 1f;
        float speciesReel = FishStats[_currentFishIndex].ReelMul;
        float zoneMul = FishStats[_currentFishIndex].ZoneMul;
        _fishingUI.SetReelParameters(skillReel * rodReel * speciesReel, zoneMul);

        _fishingUI.Show();
    }

    private void UpdateReeling()
    {
        var pointer = Pointer.current;
        if (pointer == null)
        {
            _fishingUI.UpdateReeling(Time.deltaTime, 0f);
            return;
        }

        if (!GameInput.IsMobile && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelFishing();
            return;
        }

        bool isPressed = pointer.press.isPressed;
        if (!isPressed)
        {
            _wheelGrabbed = false;
            _wheelWasGrabbed = false;
        }
        else if (isPressed && _fishingUI.IsMouseOverWheel())
        {
            _wheelGrabbed = true;
        }

        float wheelDelta = 0f;
        if (_wheelGrabbed)
        {
            var pointerPos = pointer.position.ReadValue();
            var center = _fishingUI.GetWheelCenterScreen();
            float angle = Mathf.Atan2(pointerPos.y - center.y, pointerPos.x - center.x) * Mathf.Rad2Deg;
            if (!_wheelWasGrabbed)
            {
                _lastWheelAngle = angle;
            }
            else
            {
                float delta = angle - _lastWheelAngle;
                if (delta > 180f) delta -= 360f;
                if (delta < -180f) delta += 360f;
                _lastWheelAngle = angle;
                wheelDelta = -delta;
            }
            _wheelWasGrabbed = true;
        }

        _fishingUI.UpdateReeling(Time.deltaTime, wheelDelta);

        if (_fishingUI.Progress >= 1f)
        {
            State = FishState.Success;
            OnCatchFish();
        }
        else if (_fishingUI.Progress <= 0f)
        {
            State = FishState.Fail;
            _uiManager?.ShowMessage(Localization.T("Cá thoát rồi!"), 2f);
            CancelFishing();
        }
    }

    private void OnCatchFish()
    {
        int idx = _currentFishIndex >= 0 ? _currentFishIndex : PickFishType();
        string fishType = FishTypes[idx];
        string fishLabel = FishLabels[idx];

        var player = GameManager.Instance?.Player;

        if (Random.value < _currentFlop)
        {
            SpawnFlappingFish(player, fishType, fishLabel);
            _uiManager?.ShowMessage(Localization.F("Bắt được {0}! Nó quẫy trên bờ — dùng gậy gõ cho xỉu!", Localization.T(fishLabel)), 3f);
        }
        else
        {
            var tm = ToolManager.Instance;
            if (tm != null)
                tm.AddItem(fishType, 1);

            _uiManager?.ShowMessage(Localization.F("Bắt được {0}!", Localization.T(fishLabel)), 3f);

            var sm = SkillManager.Instance;
            if (sm != null && sm.DoubleCatchChance() > 0f && Random.value < sm.DoubleCatchChance())
            {
                tm.AddItem(fishType, 1);
                _uiManager?.ShowMessage(Localization.F("Mẻ kép! +1 {0} nhờ kỹ năng Câu Cá.", Localization.T(fishLabel)), 2f);
            }
        }

        QuestManager.Instance?.AddProgress("fish_catch", 1);
        SkillManager.Instance?.AddXP(SkillManager.Track.Fishing, SkillManager.FishingXpPerCatch);

        CancelFishing();
    }

    private void SpawnFlappingFish(PlayerController player, string fishType, string fishLabel)
    {
        Vector3 spawnPos = player != null
            ? player.transform.position + player.transform.forward * 1.5f
            : Vector3.zero;

        Vector3 rayOrigin = spawnPos + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var ground, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            spawnPos.y = ground.point.y;
        else if (player != null)
            spawnPos.y = player.transform.position.y;

        var root = new GameObject("LiveFish_" + fishType);
        root.transform.position = spawnPos;
        root.transform.localRotation = Quaternion.identity;

        var worldRoot = GameObject.Find("WorldRoot");
        if (worldRoot != null)
            root.transform.SetParent(worldRoot.transform, true);

        ItemBuilder.BuildItem(root.transform, fishType);

        var flap = root.AddComponent<FlappingFish>();
        flap.Initialize(fishType, fishLabel, spawnPos);
    }

    private int PickFishType()
    {
        float total = 0f;
        var weights = _effectiveWeights != null ? _effectiveWeights : FishWeights;
        foreach (var w in weights) total += w;
        float roll = Random.Range(0f, total);
        float cum = 0f;
        for (int i = 0; i < FishTypes.Length; i++)
        {
            cum += weights[i];
            if (roll <= cum) return i;
        }
        return 0;
    }

    private void CancelFishing()
    {
        if (_hook != null) { Object.Destroy(_hook); _hook = null; }
        if (_fishShadow != null) { Object.Destroy(_fishShadow); _fishShadow = null; }
        _currentFishIndex = -1;
        _fishingUI.Hide();
        State = FishState.Idle;
        GameManager.Instance?.Player?.EnableInput(true);
    }
}
