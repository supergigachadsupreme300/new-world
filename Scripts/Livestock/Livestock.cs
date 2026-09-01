using UnityEngine;
using System.Collections;

public class Livestock : MonoBehaviour
{
    public enum AnimalType { Cow, Pig, Sheep, Goat, Chicken, Duck, Turkey }
    public enum BehaviorMode { Passive, Fight, Flee }

    public AnimalType Type;
    public int Health = 5;
    public int MaxHealth = 5;
    public bool IsKnockedOut;

    private float _moveSpeed;
    private float _wanderRange;
    private BehaviorMode _behavior;
    private Vector3 _origin;
    private Vector3 _wanderTarget;
    private float _wanderTimer;
    private GameObject _modelRoot;
    private Rigidbody _rb;
    private float _knockoutTimer;
    private float _preKnockoutYaw;
    private float _fightCooldown;
    private bool _isFighting;
    private bool _isFleeing;
    private float _spawnAnimTimer;
    private bool _spawned = true;
    private bool _grounded;
    private float _flashTimer;
    private Renderer[] _renderers;
    private Color[] _originalColors;
    private Coroutine _flashCoroutine;
    private Transform[] _upperLegs;
    private Transform[] _lowerLegs;
    private float _walkCycle;
    private Vector3 _wantedVel;

    private void Awake()
    {
        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = 0.4f;
        col.center = new Vector3(0f, 0.4f, 0f);
        col.material = new PhysicsMaterial { dynamicFriction = 0.1f, staticFriction = 0.1f, bounciness = 0f };
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;
        var vel = _rb.linearVelocity;
        if (!_spawned || (GameManager.Instance != null && GameManager.Instance.GamePaused))
        {
            _rb.linearVelocity = new Vector3(0f, vel.y, 0f);
            return;
        }
        _rb.linearVelocity = new Vector3(_wantedVel.x, vel.y, _wantedVel.z);
    }

    private void Start()
    {
        _origin = transform.position;
        ConfigureByType();
        BuildModel();
        PickWanderTarget();
    }

    private void ConfigureByType()
    {
        switch (Type)
        {
            case AnimalType.Cow:
                MaxHealth = 5; Health = 5; _moveSpeed = 1.5f; _wanderRange = 8f; _behavior = BehaviorMode.Passive;
                break;
            case AnimalType.Pig:
                MaxHealth = 5; Health = 5; _moveSpeed = 2f; _wanderRange = 6f; _behavior = BehaviorMode.Fight;
                break;
            case AnimalType.Sheep:
                MaxHealth = 5; Health = 5; _moveSpeed = 1.5f; _wanderRange = 7f; _behavior = BehaviorMode.Passive;
                break;
            case AnimalType.Goat:
                MaxHealth = 5; Health = 5; _moveSpeed = 2f; _wanderRange = 7f; _behavior = BehaviorMode.Fight;
                break;
            case AnimalType.Chicken:
                MaxHealth = 2; Health = 2; _moveSpeed = 3f; _wanderRange = 5f; _behavior = BehaviorMode.Flee;
                break;
            case AnimalType.Duck:
                MaxHealth = 2; Health = 2; _moveSpeed = 3f; _wanderRange = 5f; _behavior = BehaviorMode.Flee;
                break;
            case AnimalType.Turkey:
                MaxHealth = 2; Health = 2; _moveSpeed = 2.5f; _wanderRange = 5f; _behavior = BehaviorMode.Flee;
                break;
        }
    }

    private void Update()
    {
        if (!_spawned) return;
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return;

        if (!_grounded)
        {
            _grounded = true;
            GroundModel();
        }

        if (_flashTimer > 0f)
            _flashTimer -= Time.deltaTime;

        if (_fightCooldown > 0f)
            _fightCooldown -= Time.deltaTime;

        if (IsKnockedOut)
        {
            _knockoutTimer -= Time.deltaTime;
            if (_knockoutTimer <= 0f)
                Recover();
            return;
        }

        if (_isFighting)
        {
            UpdateFight();
            return;
        }

        if (_isFleeing)
        {
            UpdateFlee();
            return;
        }

        if (_behavior == BehaviorMode.Fight && !_isFighting && _fightCooldown <= 0f)
        {
            var fightPlayer = GameManager.Instance?.Player;
            if (fightPlayer != null && Vector3.Distance(transform.position, fightPlayer.transform.position) <= 20f)
            {
                _isFighting = true;
                UpdateFight();
                return;
            }
        }

        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
            PickWanderTarget();

        Vector3 toTarget = _wanderTarget - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > 0.3f)
        {
            Vector3 dir = toTarget.normalized;
            _wantedVel = dir * _moveSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 14f);
            AnimateLegs(true);
        }
        else
        {
            _wantedVel = Vector3.zero;
            PickWanderTarget();
            AnimateLegs(false);
        }

    }

    private void UpdateFlee()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) { _isFleeing = false; return; }
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > 15f) { _isFleeing = false; _wantedVel = Vector3.zero; return; }

        Vector3 awayDir = (transform.position - player.transform.position).normalized;
        awayDir.y = 0f;
        if (awayDir.sqrMagnitude > 0.01f)
        {
            _wantedVel = awayDir * _moveSpeed * 2f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(awayDir), Time.deltaTime * 16f);
        }
        else
        {
            _wantedVel = Vector3.zero;
        }
        AnimateLegs(true);
    }

    private void UpdateFight()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) { ResetFight(); return; }

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > 20f) { ResetFight(); return; }

        Vector3 dir = (player.transform.position - transform.position).normalized;
        dir.y = 0f;
        _wantedVel = dir * _moveSpeed * 3f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 16f);
        AnimateLegs(true);

        if (dist < 1.5f)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
                pc.TakeDamage(5);
            ResetFight();
            _fightCooldown = 5f;
        }
    }

    private void ResetFight()
    {
        _isFighting = false;
        _wantedVel = Vector3.zero;
    }

    private void AnimateLegs(bool moving)
    {
        if (_upperLegs == null || _upperLegs.Length == 0) return;

        int count = _upperLegs.Length;
        if (moving)
        {
            _walkCycle += Time.deltaTime * _moveSpeed * 5f;
            for (int i = 0; i < count; i++)
            {
                float phase = (i % 2 == 0) ? _walkCycle : _walkCycle + Mathf.PI;
                float swing = Mathf.Sin(phase) * 20f;
                if (_upperLegs[i] != null)
                    _upperLegs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
                if (i < _lowerLegs.Length && _lowerLegs[i] != null)
                    _lowerLegs[i].localRotation = Quaternion.Euler(-swing * 0.6f, 0f, 0f);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                if (_upperLegs[i] != null)
                    _upperLegs[i].localRotation = Quaternion.identity;
                if (i < _lowerLegs.Length && _lowerLegs[i] != null)
                    _lowerLegs[i].localRotation = Quaternion.identity;
            }
        }
    }

    private void PickWanderTarget()
    {
        _wanderTimer = Random.Range(2f, 5f);
        Vector2 r = Random.insideUnitCircle * _wanderRange;
        _wanderTarget = _origin + new Vector3(r.x, 0f, r.y);
    }

    public void TakeDamage(int amount)
    {
        if (IsKnockedOut) return;
        Health -= amount;
        StartFlash();
        if (Health <= 0)
        {
            Knockout();
            return;
        }
        if (_behavior == BehaviorMode.Flee)
            _isFleeing = true;
        else if (_behavior == BehaviorMode.Fight && !_isFighting)
            _isFighting = true;
    }

    private void Knockout()
    {
        IsKnockedOut = true;
        _knockoutTimer = 15f;
        _isFighting = false;
        _isFleeing = false;
        _wantedVel = Vector3.zero;
        _preKnockoutYaw = transform.eulerAngles.y;
        transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        AnimateLegs(false);
    }

    private void Recover()
    {
        IsKnockedOut = false;
        Health = MaxHealth;
        _wantedVel = Vector3.zero;
        transform.localRotation = Quaternion.Euler(0f, _preKnockoutYaw, 0f);
        AnimateLegs(false);
    }

    public bool TryCapture()
    {
        if (!IsKnockedOut) return false;
        return true;
    }

    private void StartFlash()
    {
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRed());
    }

    private IEnumerator FlashRed()
    {
        if (_renderers == null)
            _renderers = GetComponentsInChildren<Renderer>();
        if (_originalColors == null)
        {
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].material.color;
        }

        for (int i = 0; i < _renderers.Length; i++)
            _renderers[i].material.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _renderers[i].material.color = _originalColors[i];

        _flashTimer = 0f;
    }

    public void StartSpawnAnimation()
    {
        _spawned = false;
        transform.localScale = Vector3.zero;
        _spawnAnimTimer = 0f;
        StartCoroutine(SpawnAnimation());
    }

    private IEnumerator SpawnAnimation()
    {
        float duration = 0.5f;
        _spawnAnimTimer = 0f;

        while (_spawnAnimTimer < duration)
        {
            _spawnAnimTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_spawnAnimTimer / duration);
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * eased;
            yield return null;
        }

        transform.localScale = Vector3.one;
        _spawned = true;
    }

    private void GroundModel()
    {
        if (_modelRoot == null) return;
        float lowest = 0f;
        foreach (var r in _modelRoot.GetComponentsInChildren<Renderer>())
        {
            if (r == null) continue;
            lowest = Mathf.Min(lowest, r.bounds.min.y - transform.position.y);
        }
        if (lowest > 0.01f)
            _modelRoot.transform.localPosition = new Vector3(0f, -lowest, 0f);
    }

    // ═══════════════════════════════════════
    //  PROCEDURAL MODELS
    // ═══════════════════════════════════════

    private void BuildModel()
    {
        _modelRoot = new GameObject("Model");
        _modelRoot.transform.SetParent(transform, false);
        BuildModelInto(_modelRoot.transform, Type);
        CaptureLegs();
    }

    private void CaptureLegs()
    {
        var uppers = new System.Collections.Generic.List<Transform>();
        var lowers = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in _modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == _modelRoot.transform) continue;
            string n = child.name;
            if (n.StartsWith("Leg") && n.Contains("_Upper"))
            {
                var pivot = child.parent;
                if (pivot != null && pivot != _modelRoot.transform)
                    uppers.Add(pivot);
                Transform lower = null;
                foreach (Transform c2 in pivot)
                    if (c2.name.Contains("_Lower")) { lower = c2; break; }
                if (lower != null) lowers.Add(lower);
            }
        }
        _upperLegs = uppers.ToArray();
        _lowerLegs = lowers.ToArray();
    }

    public static void BuildModelInto(Transform parent, AnimalType type)
    {
        var root = new GameObject("Model");
        root.transform.SetParent(parent, false);
        switch (type)
        {
            case AnimalType.Cow: BuildCow(root.transform); break;
            case AnimalType.Pig: BuildPig(root.transform); break;
            case AnimalType.Sheep: BuildSheep(root.transform); break;
            case AnimalType.Goat: BuildGoat(root.transform); break;
            case AnimalType.Chicken: BuildChicken(root.transform); break;
            case AnimalType.Duck: BuildDuck(root.transform); break;
            case AnimalType.Turkey: BuildTurkey(root.transform); break;
        }
    }

    private static GameObject MakeBlock(Transform parent, string name, Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        go.GetComponent<Renderer>().material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject MakeSphere(Transform parent, string name, Vector3 position, float diameter, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(diameter, diameter, diameter);
        go.GetComponent<Renderer>().material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private static void MakeLeg(Transform parent, string prefix, Vector3 hipPos, Vector3 upperScale, Vector3 lowerScale, Vector3 hoofScale, Color cUpper, Color cLower, Color cHoof)
    {
        var pivot = new GameObject(prefix);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = hipPos;
        var upper = MakeBlock(pivot.transform, prefix + "_Upper", upperScale, new Vector3(0f, -upperScale.y * 0.5f, 0f), cUpper);
        var lower = MakeBlock(upper.transform, prefix + "_Lower", lowerScale, new Vector3(0f, -upperScale.y * 0.5f - lowerScale.y * 0.5f, 0f), cLower);
        MakeBlock(lower.transform, prefix + "_Hoof", hoofScale, new Vector3(0f, -lowerScale.y * 0.5f - hoofScale.y * 0.5f, 0f), cHoof);
    }

    private static void MakeLegWithFoot(Transform parent, string prefix, Vector3 hipPos, Vector3 upperScale, Vector3 lowerScale, Vector3 footScale, Color cUpper, Color cLower, Color cFoot)
    {
        var pivot = new GameObject(prefix);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = hipPos;
        var upper = MakeBlock(pivot.transform, prefix + "_Upper", upperScale, new Vector3(0f, -upperScale.y * 0.5f, 0f), cUpper);
        var lower = MakeBlock(upper.transform, prefix + "_Lower", lowerScale, new Vector3(0f, -upperScale.y * 0.5f - lowerScale.y * 0.5f, 0f), cLower);
        MakeBlock(lower.transform, prefix + "_Foot", footScale, new Vector3(0f, -lowerScale.y * 0.5f - 0.01f, footScale.z * 0.3f), cFoot);
    }

    private static void BuildCow(Transform parent)
    {
        Color white = new Color(0.95f, 0.95f, 0.95f);
        Color black = new Color(0.1f, 0.1f, 0.1f);
        Color pink = new Color(0.9f, 0.6f, 0.6f);
        Color horn = new Color(0.8f, 0.75f, 0.6f);
        Color legUpper = new Color(0.88f, 0.88f, 0.88f);
        Color legLower = new Color(0.82f, 0.82f, 0.82f);
        Color hoof = new Color(0.3f, 0.2f, 0.1f);

        MakeBlock(parent, "Body", new Vector3(0.8f, 0.6f, 1.2f), new Vector3(0f, 0.7f, 0f), white);
        MakeBlock(parent, "Head", new Vector3(0.4f, 0.35f, 0.35f), new Vector3(0f, 0.8f, 0.7f), white);
        MakeBlock(parent, "Snout", new Vector3(0.25f, 0.15f, 0.1f), new Vector3(0f, 0.7f, 0.9f), pink);
        MakeBlock(parent, "Patch1", new Vector3(0.5f, 0.3f, 0.4f), new Vector3(0.2f, 0.8f, 0.1f), black);
        MakeBlock(parent, "Patch2", new Vector3(0.3f, 0.25f, 0.5f), new Vector3(-0.15f, 0.65f, -0.2f), black);
        MakeBlock(parent, "EarL", new Vector3(0.1f, 0.05f, 0.08f), new Vector3(-0.25f, 0.95f, 0.65f), pink);
        MakeBlock(parent, "EarR", new Vector3(0.1f, 0.05f, 0.08f), new Vector3(0.25f, 0.95f, 0.65f), pink);
        MakeBlock(parent, "HornL", new Vector3(0.06f, 0.15f, 0.06f), new Vector3(-0.15f, 1.05f, 0.65f), horn);
        MakeBlock(parent, "HornR", new Vector3(0.06f, 0.15f, 0.06f), new Vector3(0.15f, 1.05f, 0.65f), horn);
        MakeBlock(parent, "EyeL", new Vector3(0.05f, 0.05f, 0.05f), new Vector3(-0.12f, 0.88f, 0.82f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0.12f, 0.88f, 0.82f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.04f, 0.04f, 0.3f), new Vector3(0f, 0.8f, -0.7f), pink);
        MakeBlock(parent, "TailTip", new Vector3(0.06f, 0.06f, 0.08f), new Vector3(0f, 0.8f, -0.88f), black);

        MakeLeg(parent, "LegFL", new Vector3(-0.25f, 0.56f, 0.35f), new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0.11f, 0.06f, 0.13f), legUpper, legLower, hoof);
        MakeLeg(parent, "LegFR", new Vector3(0.25f, 0.56f, 0.35f), new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0.11f, 0.06f, 0.13f), legUpper, legLower, hoof);
        MakeLeg(parent, "LegBL", new Vector3(-0.25f, 0.56f, -0.35f), new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0.11f, 0.06f, 0.13f), legUpper, legLower, hoof);
        MakeLeg(parent, "LegBR", new Vector3(0.25f, 0.56f, -0.35f), new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0.11f, 0.06f, 0.13f), legUpper, legLower, hoof);
    }

    private static void BuildPig(Transform parent)
    {
        Color pink = new Color(0.95f, 0.65f, 0.6f);
        Color darkPink = new Color(0.8f, 0.45f, 0.4f);
        Color nose = new Color(0.85f, 0.5f, 0.5f);
        Color eyeWhite = new Color(0.95f, 0.95f, 0.95f);
        Color legUp = new Color(0.85f, 0.55f, 0.5f);
        Color legLo = new Color(0.78f, 0.48f, 0.43f);
        Color hoof = new Color(0.35f, 0.2f, 0.15f);

        MakeBlock(parent, "Body", new Vector3(0.7f, 0.5f, 0.85f), new Vector3(0f, 0.55f, 0f), pink);
        MakeBlock(parent, "Head", new Vector3(0.4f, 0.38f, 0.35f), new Vector3(0f, 0.65f, 0.45f), pink);
        MakeBlock(parent, "Snout", new Vector3(0.22f, 0.14f, 0.08f), new Vector3(0f, 0.6f, 0.65f), nose);
        MakeBlock(parent, "NostrilL", new Vector3(0.04f, 0.03f, 0.03f), new Vector3(-0.04f, 0.6f, 0.7f), darkPink);
        MakeBlock(parent, "NostrilR", new Vector3(0.04f, 0.03f, 0.03f), new Vector3(0.04f, 0.6f, 0.7f), darkPink);
        MakeBlock(parent, "EarL", new Vector3(0.12f, 0.1f, 0.06f), new Vector3(-0.18f, 0.82f, 0.4f), darkPink);
        MakeBlock(parent, "EarR", new Vector3(0.12f, 0.1f, 0.06f), new Vector3(0.18f, 0.82f, 0.4f), darkPink);
        MakeBlock(parent, "EyeL", new Vector3(0.06f, 0.06f, 0.04f), new Vector3(-0.12f, 0.72f, 0.58f), eyeWhite);
        MakeBlock(parent, "EyeR", new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0.12f, 0.72f, 0.58f), eyeWhite);
        MakeBlock(parent, "PupilL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.12f, 0.72f, 0.6f), Color.black);
        MakeBlock(parent, "PupilR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.12f, 0.72f, 0.6f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.03f, 0.18f, 0.03f), new Vector3(0f, 0.7f, -0.5f), darkPink);

        MakeLeg(parent, "LegFL", new Vector3(-0.2f, 0.35f, 0.25f), new Vector3(0.1f, 0.16f, 0.1f), new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0.09f, 0.04f, 0.1f), legUp, legLo, hoof);
        MakeLeg(parent, "LegFR", new Vector3(0.2f, 0.35f, 0.25f), new Vector3(0.1f, 0.16f, 0.1f), new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0.09f, 0.04f, 0.1f), legUp, legLo, hoof);
        MakeLeg(parent, "LegBL", new Vector3(-0.2f, 0.35f, -0.25f), new Vector3(0.1f, 0.16f, 0.1f), new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0.09f, 0.04f, 0.1f), legUp, legLo, hoof);
        MakeLeg(parent, "LegBR", new Vector3(0.2f, 0.35f, -0.25f), new Vector3(0.1f, 0.16f, 0.1f), new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0.09f, 0.04f, 0.1f), legUp, legLo, hoof);
    }

    private static void BuildSheep(Transform parent)
    {
        Color wool = new Color(0.95f, 0.93f, 0.88f);
        Color darkWool = new Color(0.88f, 0.86f, 0.82f);
        Color dark = new Color(0.4f, 0.35f, 0.3f);
        Color eyeC = new Color(0.05f, 0.05f, 0.05f);
        Color legUp = new Color(0.45f, 0.4f, 0.35f);
        Color legLo = new Color(0.38f, 0.32f, 0.28f);
        Color hoof = new Color(0.25f, 0.18f, 0.1f);

        MakeBlock(parent, "Body", new Vector3(0.6f, 0.5f, 0.8f), new Vector3(0f, 0.6f, 0f), wool);
        MakeBlock(parent, "WoolTop", new Vector3(0.5f, 0.2f, 0.65f), new Vector3(0f, 0.85f, 0f), wool);
        MakeBlock(parent, "WoolSideL", new Vector3(0.12f, 0.35f, 0.55f), new Vector3(-0.32f, 0.6f, 0f), darkWool);
        MakeBlock(parent, "WoolSideR", new Vector3(0.12f, 0.35f, 0.55f), new Vector3(0.32f, 0.6f, 0f), darkWool);
        MakeBlock(parent, "WoolBack", new Vector3(0.5f, 0.4f, 0.15f), new Vector3(0f, 0.6f, -0.42f), wool);
        MakeBlock(parent, "Head", new Vector3(0.22f, 0.25f, 0.25f), new Vector3(0f, 0.7f, 0.55f), dark);
        MakeBlock(parent, "Snout", new Vector3(0.12f, 0.08f, 0.06f), new Vector3(0f, 0.62f, 0.68f), new Color(0.5f, 0.4f, 0.35f));
        MakeBlock(parent, "EyeL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.08f, 0.75f, 0.67f), eyeC);
        MakeBlock(parent, "EyeR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.08f, 0.75f, 0.67f), eyeC);
        MakeBlock(parent, "EarL", new Vector3(0.08f, 0.04f, 0.05f), new Vector3(-0.15f, 0.78f, 0.5f), dark);
        MakeBlock(parent, "EarR", new Vector3(0.08f, 0.04f, 0.05f), new Vector3(0.15f, 0.78f, 0.5f), dark);

        MakeLeg(parent, "LegFL", new Vector3(-0.18f, 0.4f, 0.25f), new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
        MakeLeg(parent, "LegFR", new Vector3(0.18f, 0.4f, 0.25f), new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
        MakeLeg(parent, "LegBL", new Vector3(-0.18f, 0.4f, -0.25f), new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
        MakeLeg(parent, "LegBR", new Vector3(0.18f, 0.4f, -0.25f), new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
    }

    private static void BuildGoat(Transform parent)
    {
        Color brown = new Color(0.6f, 0.45f, 0.3f);
        Color darkBrown = new Color(0.4f, 0.3f, 0.2f);
        Color horn = new Color(0.75f, 0.7f, 0.55f);
        Color legUp = new Color(0.55f, 0.4f, 0.28f);
        Color legLo = new Color(0.45f, 0.33f, 0.22f);
        Color hoof = new Color(0.25f, 0.18f, 0.1f);

        MakeBlock(parent, "Body", new Vector3(0.5f, 0.45f, 0.9f), new Vector3(0f, 0.6f, 0f), brown);
        MakeBlock(parent, "Head", new Vector3(0.25f, 0.3f, 0.3f), new Vector3(0f, 0.75f, 0.55f), brown);
        MakeBlock(parent, "Beard", new Vector3(0.06f, 0.15f, 0.06f), new Vector3(0f, 0.55f, 0.65f), darkBrown);
        MakeBlock(parent, "HornL", new Vector3(0.05f, 0.2f, 0.05f), new Vector3(-0.12f, 0.95f, 0.5f), horn);
        MakeBlock(parent, "HornR", new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0.12f, 0.95f, 0.5f), horn);
        MakeBlock(parent, "EyeL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.1f, 0.8f, 0.7f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.1f, 0.8f, 0.7f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0f, 0.7f, -0.5f), darkBrown);

        MakeLeg(parent, "LegFL", new Vector3(-0.15f, 0.42f, 0.3f), new Vector3(0.09f, 0.19f, 0.09f), new Vector3(0.07f, 0.19f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
        MakeLeg(parent, "LegFR", new Vector3(0.15f, 0.42f, 0.3f), new Vector3(0.09f, 0.19f, 0.09f), new Vector3(0.07f, 0.19f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
        MakeLeg(parent, "LegBL", new Vector3(-0.15f, 0.42f, -0.3f), new Vector3(0.09f, 0.19f, 0.09f), new Vector3(0.07f, 0.19f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
        MakeLeg(parent, "LegBR", new Vector3(0.15f, 0.42f, -0.3f), new Vector3(0.09f, 0.19f, 0.09f), new Vector3(0.07f, 0.19f, 0.07f), new Vector3(0.08f, 0.04f, 0.09f), legUp, legLo, hoof);
    }

    private static void BuildChicken(Transform parent)
    {
        Color white = new Color(0.95f, 0.93f, 0.88f);
        Color cream = new Color(0.92f, 0.9f, 0.85f);
        Color red = new Color(0.85f, 0.15f, 0.1f);
        Color yellow = new Color(0.95f, 0.85f, 0.2f);
        Color legUp = new Color(0.9f, 0.8f, 0.18f);
        Color legLo = new Color(0.85f, 0.75f, 0.15f);
        Color foot = new Color(0.8f, 0.7f, 0.12f);
        Color wingUp = new Color(0.92f, 0.9f, 0.85f);
        Color wingLo = new Color(0.88f, 0.86f, 0.82f);

        MakeBlock(parent, "Body", new Vector3(0.32f, 0.3f, 0.38f), new Vector3(0f, 0.4f, 0f), white);
        MakeBlock(parent, "Breast", new Vector3(0.25f, 0.22f, 0.15f), new Vector3(0f, 0.38f, 0.12f), cream);
        MakeBlock(parent, "Head", new Vector3(0.15f, 0.18f, 0.15f), new Vector3(0f, 0.62f, 0.18f), white);
        MakeBlock(parent, "Comb", new Vector3(0.04f, 0.12f, 0.08f), new Vector3(0f, 0.74f, 0.16f), red);
        MakeBlock(parent, "Beak", new Vector3(0.06f, 0.04f, 0.08f), new Vector3(0f, 0.6f, 0.3f), yellow);
        MakeBlock(parent, "Wattle", new Vector3(0.04f, 0.06f, 0.04f), new Vector3(0f, 0.53f, 0.26f), red);
        MakeBlock(parent, "EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.06f, 0.65f, 0.26f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.06f, 0.65f, 0.26f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.06f, 0.12f, 0.12f), new Vector3(0f, 0.5f, -0.22f), white);
        MakeBlock(parent, "TailTip", new Vector3(0.04f, 0.1f, 0.1f), new Vector3(0f, 0.58f, -0.32f), cream);
        MakeBlock(parent, "WingL", new Vector3(0.04f, 0.18f, 0.2f), new Vector3(-0.2f, 0.45f, 0f), wingUp);
        MakeBlock(parent, "WingR", new Vector3(0.04f, 0.18f, 0.2f), new Vector3(0.2f, 0.45f, 0f), wingUp);

        MakeLegWithFoot(parent, "LegL", new Vector3(-0.06f, 0.3f, 0f), new Vector3(0.035f, 0.12f, 0.035f), new Vector3(0.025f, 0.12f, 0.025f), new Vector3(0.08f, 0.02f, 0.1f), legUp, legLo, foot);
        MakeLegWithFoot(parent, "LegR", new Vector3(0.06f, 0.3f, 0f), new Vector3(0.035f, 0.12f, 0.035f), new Vector3(0.025f, 0.12f, 0.025f), new Vector3(0.08f, 0.02f, 0.1f), legUp, legLo, foot);
    }

    private static void BuildDuck(Transform parent)
    {
        Color white = new Color(0.92f, 0.9f, 0.85f);
        Color cream = new Color(0.88f, 0.86f, 0.8f);
        Color orange = new Color(0.95f, 0.6f, 0.15f);
        Color legUp = new Color(0.92f, 0.58f, 0.13f);
        Color legLo = new Color(0.88f, 0.55f, 0.1f);
        Color foot = new Color(0.85f, 0.52f, 0.08f);
        Color wingUp = new Color(0.9f, 0.88f, 0.83f);
        Color wingLo = new Color(0.86f, 0.84f, 0.8f);

        MakeBlock(parent, "Body", new Vector3(0.35f, 0.28f, 0.42f), new Vector3(0f, 0.38f, 0f), white);
        MakeBlock(parent, "Breast", new Vector3(0.28f, 0.22f, 0.15f), new Vector3(0f, 0.36f, 0.12f), cream);
        MakeBlock(parent, "Head", new Vector3(0.2f, 0.22f, 0.2f), new Vector3(0f, 0.58f, 0.2f), white);
        MakeBlock(parent, "Bill", new Vector3(0.12f, 0.04f, 0.15f), new Vector3(0f, 0.54f, 0.35f), orange);
        MakeBlock(parent, "EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.08f, 0.62f, 0.3f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.08f, 0.62f, 0.3f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.06f, 0.1f, 0.12f), new Vector3(0f, 0.46f, -0.25f), white);
        MakeBlock(parent, "WingL", new Vector3(0.04f, 0.16f, 0.22f), new Vector3(-0.22f, 0.44f, 0f), wingUp);
        MakeBlock(parent, "WingR", new Vector3(0.04f, 0.16f, 0.22f), new Vector3(0.22f, 0.44f, 0f), wingUp);

        MakeLegWithFoot(parent, "LegL", new Vector3(-0.08f, 0.26f, 0f), new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0.03f, 0.1f, 0.03f), new Vector3(0.1f, 0.02f, 0.12f), legUp, legLo, foot);
        MakeLegWithFoot(parent, "LegR", new Vector3(0.08f, 0.26f, 0f), new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0.03f, 0.1f, 0.03f), new Vector3(0.1f, 0.02f, 0.12f), legUp, legLo, foot);
    }

    private static void BuildTurkey(Transform parent)
    {
        Color brown = new Color(0.5f, 0.3f, 0.15f);
        Color darkBrown = new Color(0.35f, 0.2f, 0.1f);
        Color lightBrown = new Color(0.58f, 0.38f, 0.2f);
        Color red = new Color(0.8f, 0.15f, 0.1f);
        Color beak = new Color(0.8f, 0.7f, 0.3f);
        Color legUp = new Color(0.48f, 0.28f, 0.13f);
        Color legLo = new Color(0.42f, 0.24f, 0.1f);
        Color foot = new Color(0.38f, 0.22f, 0.08f);
        Color wingUp = new Color(0.48f, 0.28f, 0.13f);
        Color wingLo = new Color(0.42f, 0.24f, 0.1f);

        MakeBlock(parent, "Body", new Vector3(0.5f, 0.42f, 0.55f), new Vector3(0f, 0.48f, 0f), brown);
        MakeBlock(parent, "Chest", new Vector3(0.4f, 0.35f, 0.2f), new Vector3(0f, 0.44f, 0.18f), lightBrown);
        MakeBlock(parent, "Head", new Vector3(0.15f, 0.18f, 0.15f), new Vector3(0f, 0.7f, 0.28f), brown);
        MakeBlock(parent, "Neck", new Vector3(0.08f, 0.15f, 0.08f), new Vector3(0f, 0.6f, 0.22f), brown);
        MakeBlock(parent, "Wattle", new Vector3(0.05f, 0.1f, 0.03f), new Vector3(0f, 0.6f, 0.35f), red);
        MakeBlock(parent, "Snood", new Vector3(0.03f, 0.06f, 0.02f), new Vector3(0f, 0.78f, 0.32f), red);
        MakeBlock(parent, "Beak", new Vector3(0.05f, 0.03f, 0.08f), new Vector3(0f, 0.68f, 0.38f), beak);
        MakeBlock(parent, "EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.06f, 0.73f, 0.36f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.06f, 0.73f, 0.36f), Color.black);

        MakeBlock(parent, "FanBase", new Vector3(0.4f, 0.08f, 0.08f), new Vector3(0f, 0.52f, -0.28f), darkBrown);
        for (int i = 0; i < 7; i++)
        {
            float angle = -45f + i * 15f;
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.35f;
            float y = 0.55f + Mathf.Cos(angle * Mathf.Deg2Rad) * 0.15f;
            MakeBlock(parent, "Fan" + i, new Vector3(0.03f, 0.18f, 0.015f), new Vector3(x, y, -0.32f), darkBrown);
        }

        MakeBlock(parent, "WingL", new Vector3(0.04f, 0.2f, 0.25f), new Vector3(-0.3f, 0.52f, 0f), wingUp);
        MakeBlock(parent, "WingR", new Vector3(0.04f, 0.2f, 0.25f), new Vector3(0.3f, 0.52f, 0f), wingUp);

        MakeLegWithFoot(parent, "LegL", new Vector3(-0.1f, 0.3f, 0f), new Vector3(0.045f, 0.14f, 0.045f), new Vector3(0.035f, 0.14f, 0.035f), new Vector3(0.1f, 0.02f, 0.12f), legUp, legLo, foot);
        MakeLegWithFoot(parent, "LegR", new Vector3(0.1f, 0.3f, 0f), new Vector3(0.045f, 0.14f, 0.045f), new Vector3(0.035f, 0.14f, 0.035f), new Vector3(0.1f, 0.02f, 0.12f), legUp, legLo, foot);
    }
}
