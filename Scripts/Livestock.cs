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
    private float _fightCooldown;
    private bool _isFighting;
    private bool _isFleeing;
    private float _spawnAnimTimer;
    private bool _spawned = true;
    private float _flashTimer;
    private Renderer[] _renderers;
    private Color[] _originalColors;
    private Coroutine _flashCoroutine;
    private Transform[] _upperLegs;
    private Transform[] _lowerLegs;
    private float _walkCycle;

    private void Awake()
    {
        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = true;

        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = 0.4f;
        col.center = new Vector3(0f, 0.4f, 0f);
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

        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
            PickWanderTarget();

        Vector3 toTarget = _wanderTarget - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > 0.3f)
        {
            Vector3 dir = toTarget.normalized;
            transform.position += dir * _moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
            AnimateLegs(true);
        }
        else
        {
            PickWanderTarget();
            AnimateLegs(false);
        }

        if (_behavior == BehaviorMode.Flee)
            CheckFleeTrigger();
        if (_behavior == BehaviorMode.Fight)
            CheckFightTrigger();
    }

    private void CheckFleeTrigger()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist < 8f)
        {
            _isFleeing = true;
        }
    }

    private void UpdateFlee()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) { _isFleeing = false; return; }
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > 15f) { _isFleeing = false; return; }

        Vector3 awayDir = (transform.position - player.transform.position).normalized;
        awayDir.y = 0f;
        transform.position += awayDir * _moveSpeed * 2f * Time.deltaTime;
        if (awayDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(awayDir), Time.deltaTime * 5f);
        AnimateLegs(true);
    }

    private void CheckFightTrigger()
    {
        if (_isFighting || _fightCooldown > 0f) return;
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist < 5f)
        {
            _isFighting = true;
        }
    }

    private void UpdateFight()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) { ResetFight(); return; }

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > 20f) { ResetFight(); return; }

        Vector3 dir = (player.transform.position - transform.position).normalized;
        dir.y = 0f;
        transform.position += dir * _moveSpeed * 3f * Time.deltaTime;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
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
            Knockout();
    }

    private void Knockout()
    {
        IsKnockedOut = true;
        _knockoutTimer = 15f;
        _isFighting = false;
        _isFleeing = false;
        transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        AnimateLegs(false);
    }

    private void Recover()
    {
        IsKnockedOut = false;
        Health = MaxHealth;
        transform.localRotation = Quaternion.identity;
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
        var upper = new System.Collections.Generic.List<Transform>();
        var lower = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in _modelRoot.transform)
        {
            string n = child.name;
            if (n.StartsWith("Leg") && n.Contains("Upper"))
                upper.Add(child);
            else if (n.StartsWith("Leg") && n.Contains("Lower"))
                lower.Add(child);
        }
        _upperLegs = upper.ToArray();
        _lowerLegs = lower.ToArray();
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
        MakeBlock(parent, "LegFL_Upper", new Vector3(0.14f, 0.28f, 0.14f), new Vector3(-0.25f, 0.56f, 0.35f), legUpper);
        MakeBlock(parent, "LegFL_Lower", new Vector3(0.1f, 0.26f, 0.1f), new Vector3(-0.25f, 0.3f, 0.35f), legLower);
        MakeBlock(parent, "LegFL_Hoof", new Vector3(0.11f, 0.06f, 0.13f), new Vector3(-0.25f, 0.15f, 0.35f), hoof);
        MakeBlock(parent, "LegFR_Upper", new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0.25f, 0.56f, 0.35f), legUpper);
        MakeBlock(parent, "LegFR_Lower", new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0.25f, 0.3f, 0.35f), legLower);
        MakeBlock(parent, "LegFR_Hoof", new Vector3(0.11f, 0.06f, 0.13f), new Vector3(0.25f, 0.15f, 0.35f), hoof);
        MakeBlock(parent, "LegBL_Upper", new Vector3(0.14f, 0.28f, 0.14f), new Vector3(-0.25f, 0.56f, -0.35f), legUpper);
        MakeBlock(parent, "LegBL_Lower", new Vector3(0.1f, 0.26f, 0.1f), new Vector3(-0.25f, 0.3f, -0.35f), legLower);
        MakeBlock(parent, "LegBL_Hoof", new Vector3(0.11f, 0.06f, 0.13f), new Vector3(-0.25f, 0.15f, -0.35f), hoof);
        MakeBlock(parent, "LegBR_Upper", new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0.25f, 0.56f, -0.35f), legUpper);
        MakeBlock(parent, "LegBR_Lower", new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0.25f, 0.3f, -0.35f), legLower);
        MakeBlock(parent, "LegBR_Hoof", new Vector3(0.11f, 0.06f, 0.13f), new Vector3(0.25f, 0.15f, -0.35f), hoof);
    }

    private static void BuildPig(Transform parent)
    {
        Color pink = new Color(0.95f, 0.65f, 0.6f);
        Color darkPink = new Color(0.8f, 0.45f, 0.4f);
        Color nose = new Color(0.85f, 0.5f, 0.5f);
        Color legUp = new Color(0.85f, 0.55f, 0.5f);
        Color legLo = new Color(0.78f, 0.48f, 0.43f);
        Color hoof = new Color(0.35f, 0.2f, 0.15f);

        MakeSphere(parent, "Body", new Vector3(0f, 0.55f, 0f), 0.9f, pink);
        MakeSphere(parent, "Head", new Vector3(0f, 0.6f, 0.45f), 0.5f, pink);
        MakeSphere(parent, "Snout", new Vector3(0f, 0.55f, 0.7f), 0.25f, nose);
        MakeBlock(parent, "NostrilL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.04f, 0.55f, 0.8f), darkPink);
        MakeBlock(parent, "NostrilR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.04f, 0.55f, 0.8f), darkPink);
        MakeBlock(parent, "EarL", new Vector3(0.12f, 0.1f, 0.06f), new Vector3(-0.18f, 0.82f, 0.4f), darkPink);
        MakeBlock(parent, "EarR", new Vector3(0.12f, 0.1f, 0.06f), new Vector3(0.18f, 0.82f, 0.4f), darkPink);
        MakeBlock(parent, "EyeL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.1f, 0.68f, 0.6f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.1f, 0.68f, 0.6f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.03f, 0.15f, 0.03f), new Vector3(0f, 0.7f, -0.5f), darkPink);
        MakeBlock(parent, "LegFL_Upper", new Vector3(0.1f, 0.16f, 0.1f), new Vector3(-0.2f, 0.47f, 0.25f), legUp);
        MakeBlock(parent, "LegFL_Lower", new Vector3(0.08f, 0.14f, 0.08f), new Vector3(-0.2f, 0.32f, 0.25f), legLo);
        MakeBlock(parent, "LegFL_Hoof", new Vector3(0.09f, 0.04f, 0.1f), new Vector3(-0.2f, 0.23f, 0.25f), hoof);
        MakeBlock(parent, "LegFR_Upper", new Vector3(0.1f, 0.16f, 0.1f), new Vector3(0.2f, 0.47f, 0.25f), legUp);
        MakeBlock(parent, "LegFR_Lower", new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0.2f, 0.32f, 0.25f), legLo);
        MakeBlock(parent, "LegFR_Hoof", new Vector3(0.09f, 0.04f, 0.1f), new Vector3(0.2f, 0.23f, 0.25f), hoof);
        MakeBlock(parent, "LegBL_Upper", new Vector3(0.1f, 0.16f, 0.1f), new Vector3(-0.2f, 0.47f, -0.25f), legUp);
        MakeBlock(parent, "LegBL_Lower", new Vector3(0.08f, 0.14f, 0.08f), new Vector3(-0.2f, 0.32f, -0.25f), legLo);
        MakeBlock(parent, "LegBL_Hoof", new Vector3(0.09f, 0.04f, 0.1f), new Vector3(-0.2f, 0.23f, -0.25f), hoof);
        MakeBlock(parent, "LegBR_Upper", new Vector3(0.1f, 0.16f, 0.1f), new Vector3(0.2f, 0.47f, -0.25f), legUp);
        MakeBlock(parent, "LegBR_Lower", new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0.2f, 0.32f, -0.25f), legLo);
        MakeBlock(parent, "LegBR_Hoof", new Vector3(0.09f, 0.04f, 0.1f), new Vector3(0.2f, 0.23f, -0.25f), hoof);
    }

    private static void BuildSheep(Transform parent)
    {
        Color wool = new Color(0.95f, 0.93f, 0.88f);
        Color dark = new Color(0.4f, 0.35f, 0.3f);
        Color eyeC = new Color(0.05f, 0.05f, 0.05f);
        Color legUp = new Color(0.45f, 0.4f, 0.35f);
        Color legLo = new Color(0.38f, 0.32f, 0.28f);
        Color hoof = new Color(0.25f, 0.18f, 0.1f);

        for (int i = 0; i < 6; i++)
        {
            float x = Random.Range(-0.25f, 0.25f);
            float y = 0.55f + Random.Range(-0.1f, 0.1f);
            float z = Random.Range(-0.35f, 0.35f);
            MakeSphere(parent, "Wool" + i, new Vector3(x, y, z), Random.Range(0.35f, 0.5f), wool);
        }
        MakeBlock(parent, "Head", new Vector3(0.22f, 0.25f, 0.25f), new Vector3(0f, 0.7f, 0.55f), dark);
        MakeBlock(parent, "EyeL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.08f, 0.75f, 0.67f), eyeC);
        MakeBlock(parent, "EyeR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.08f, 0.75f, 0.67f), eyeC);
        MakeBlock(parent, "EarL", new Vector3(0.08f, 0.04f, 0.05f), new Vector3(-0.15f, 0.78f, 0.5f), dark);
        MakeBlock(parent, "EarR", new Vector3(0.08f, 0.04f, 0.05f), new Vector3(0.15f, 0.78f, 0.5f), dark);
        MakeBlock(parent, "LegFL_Upper", new Vector3(0.09f, 0.18f, 0.09f), new Vector3(-0.18f, 0.52f, 0.25f), legUp);
        MakeBlock(parent, "LegFL_Lower", new Vector3(0.07f, 0.18f, 0.07f), new Vector3(-0.18f, 0.34f, 0.25f), legLo);
        MakeBlock(parent, "LegFL_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(-0.18f, 0.23f, 0.25f), hoof);
        MakeBlock(parent, "LegFR_Upper", new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0.18f, 0.52f, 0.25f), legUp);
        MakeBlock(parent, "LegFR_Lower", new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0.18f, 0.34f, 0.25f), legLo);
        MakeBlock(parent, "LegFR_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(0.18f, 0.23f, 0.25f), hoof);
        MakeBlock(parent, "LegBL_Upper", new Vector3(0.09f, 0.18f, 0.09f), new Vector3(-0.18f, 0.52f, -0.25f), legUp);
        MakeBlock(parent, "LegBL_Lower", new Vector3(0.07f, 0.18f, 0.07f), new Vector3(-0.18f, 0.34f, -0.25f), legLo);
        MakeBlock(parent, "LegBL_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(-0.18f, 0.23f, -0.25f), hoof);
        MakeBlock(parent, "LegBR_Upper", new Vector3(0.09f, 0.18f, 0.09f), new Vector3(0.18f, 0.52f, -0.25f), legUp);
        MakeBlock(parent, "LegBR_Lower", new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0.18f, 0.34f, -0.25f), legLo);
        MakeBlock(parent, "LegBR_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(0.18f, 0.23f, -0.25f), hoof);
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
        MakeBlock(parent, "LegFL_Upper", new Vector3(0.09f, 0.19f, 0.09f), new Vector3(-0.15f, 0.52f, 0.3f), legUp);
        MakeBlock(parent, "LegFL_Lower", new Vector3(0.07f, 0.19f, 0.07f), new Vector3(-0.15f, 0.33f, 0.3f), legLo);
        MakeBlock(parent, "LegFL_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(-0.15f, 0.22f, 0.3f), hoof);
        MakeBlock(parent, "LegFR_Upper", new Vector3(0.09f, 0.19f, 0.09f), new Vector3(0.15f, 0.52f, 0.3f), legUp);
        MakeBlock(parent, "LegFR_Lower", new Vector3(0.07f, 0.19f, 0.07f), new Vector3(0.15f, 0.33f, 0.3f), legLo);
        MakeBlock(parent, "LegFR_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(0.15f, 0.22f, 0.3f), hoof);
        MakeBlock(parent, "LegBL_Upper", new Vector3(0.09f, 0.19f, 0.09f), new Vector3(-0.15f, 0.52f, -0.3f), legUp);
        MakeBlock(parent, "LegBL_Lower", new Vector3(0.07f, 0.19f, 0.07f), new Vector3(-0.15f, 0.33f, -0.3f), legLo);
        MakeBlock(parent, "LegBL_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(-0.15f, 0.22f, -0.3f), hoof);
        MakeBlock(parent, "LegBR_Upper", new Vector3(0.09f, 0.19f, 0.09f), new Vector3(0.15f, 0.52f, -0.3f), legUp);
        MakeBlock(parent, "LegBR_Lower", new Vector3(0.07f, 0.19f, 0.07f), new Vector3(0.15f, 0.33f, -0.3f), legLo);
        MakeBlock(parent, "LegBR_Hoof", new Vector3(0.08f, 0.04f, 0.09f), new Vector3(0.15f, 0.22f, -0.3f), hoof);
    }

    private static void BuildChicken(Transform parent)
    {
        Color white = new Color(0.95f, 0.93f, 0.88f);
        Color red = new Color(0.85f, 0.15f, 0.1f);
        Color yellow = new Color(0.95f, 0.85f, 0.2f);
        Color legUp = new Color(0.9f, 0.8f, 0.18f);
        Color legLo = new Color(0.85f, 0.75f, 0.15f);
        Color foot = new Color(0.8f, 0.7f, 0.12f);
        Color wingUp = new Color(0.92f, 0.9f, 0.85f);
        Color wingLo = new Color(0.88f, 0.86f, 0.82f);

        MakeSphere(parent, "Body", new Vector3(0f, 0.45f, 0f), 0.5f, white);
        MakeBlock(parent, "Breast", new Vector3(0.25f, 0.3f, 0.2f), new Vector3(0f, 0.42f, 0.15f), white);
        MakeBlock(parent, "Head", new Vector3(0.15f, 0.18f, 0.15f), new Vector3(0f, 0.65f, 0.2f), white);
        MakeBlock(parent, "Comb", new Vector3(0.04f, 0.12f, 0.08f), new Vector3(0f, 0.78f, 0.18f), red);
        MakeBlock(parent, "Beak", new Vector3(0.06f, 0.04f, 0.08f), new Vector3(0f, 0.62f, 0.32f), yellow);
        MakeBlock(parent, "Wattle", new Vector3(0.04f, 0.06f, 0.04f), new Vector3(0f, 0.55f, 0.28f), red);
        MakeBlock(parent, "EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.06f, 0.68f, 0.28f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.06f, 0.68f, 0.28f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.06f, 0.15f, 0.15f), new Vector3(0f, 0.55f, -0.25f), white);
        MakeBlock(parent, "TailTip", new Vector3(0.04f, 0.1f, 0.1f), new Vector3(0f, 0.65f, -0.35f), white);
        MakeBlock(parent, "WingL_Upper", new Vector3(0.04f, 0.18f, 0.2f), new Vector3(-0.22f, 0.5f, 0f), wingUp);
        MakeBlock(parent, "WingL_Lower", new Vector3(0.03f, 0.14f, 0.15f), new Vector3(-0.28f, 0.42f, -0.05f), wingLo);
        MakeBlock(parent, "WingR_Upper", new Vector3(0.04f, 0.18f, 0.2f), new Vector3(0.22f, 0.5f, 0f), wingUp);
        MakeBlock(parent, "WingR_Lower", new Vector3(0.03f, 0.14f, 0.15f), new Vector3(0.28f, 0.42f, -0.05f), wingLo);
        MakeBlock(parent, "LegL_Upper", new Vector3(0.035f, 0.12f, 0.035f), new Vector3(-0.06f, 0.33f, 0f), legUp);
        MakeBlock(parent, "LegL_Lower", new Vector3(0.025f, 0.12f, 0.025f), new Vector3(-0.06f, 0.21f, 0.02f), legLo);
        MakeBlock(parent, "FootL", new Vector3(0.08f, 0.02f, 0.1f), new Vector3(-0.06f, 0.14f, 0.03f), foot);
        MakeBlock(parent, "LegR_Upper", new Vector3(0.035f, 0.12f, 0.035f), new Vector3(0.06f, 0.33f, 0f), legUp);
        MakeBlock(parent, "LegR_Lower", new Vector3(0.025f, 0.12f, 0.025f), new Vector3(0.06f, 0.21f, 0.02f), legLo);
        MakeBlock(parent, "FootR", new Vector3(0.08f, 0.02f, 0.1f), new Vector3(0.06f, 0.14f, 0.03f), foot);
    }

    private static void BuildDuck(Transform parent)
    {
        Color white = new Color(0.92f, 0.9f, 0.85f);
        Color orange = new Color(0.95f, 0.6f, 0.15f);
        Color legUp = new Color(0.92f, 0.58f, 0.13f);
        Color legLo = new Color(0.88f, 0.55f, 0.1f);
        Color foot = new Color(0.85f, 0.52f, 0.08f);
        Color wingUp = new Color(0.9f, 0.88f, 0.83f);
        Color wingLo = new Color(0.86f, 0.84f, 0.8f);

        MakeSphere(parent, "Body", new Vector3(0f, 0.42f, 0f), 0.55f, white);
        MakeBlock(parent, "Breast", new Vector3(0.28f, 0.28f, 0.2f), new Vector3(0f, 0.4f, 0.12f), white);
        MakeBlock(parent, "Head", new Vector3(0.2f, 0.22f, 0.2f), new Vector3(0f, 0.62f, 0.22f), white);
        MakeBlock(parent, "Bill", new Vector3(0.12f, 0.04f, 0.15f), new Vector3(0f, 0.58f, 0.38f), orange);
        MakeBlock(parent, "EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.08f, 0.66f, 0.32f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.08f, 0.66f, 0.32f), Color.black);
        MakeBlock(parent, "Tail", new Vector3(0.06f, 0.1f, 0.12f), new Vector3(0f, 0.5f, -0.28f), white);
        MakeBlock(parent, "WingL_Upper", new Vector3(0.04f, 0.16f, 0.22f), new Vector3(-0.24f, 0.48f, 0f), wingUp);
        MakeBlock(parent, "WingL_Lower", new Vector3(0.03f, 0.12f, 0.16f), new Vector3(-0.3f, 0.4f, -0.04f), wingLo);
        MakeBlock(parent, "WingR_Upper", new Vector3(0.04f, 0.16f, 0.22f), new Vector3(0.24f, 0.48f, 0f), wingUp);
        MakeBlock(parent, "WingR_Lower", new Vector3(0.03f, 0.12f, 0.16f), new Vector3(0.3f, 0.4f, -0.04f), wingLo);
        MakeBlock(parent, "LegL_Upper", new Vector3(0.04f, 0.1f, 0.04f), new Vector3(-0.08f, 0.32f, 0f), legUp);
        MakeBlock(parent, "LegL_Lower", new Vector3(0.03f, 0.1f, 0.03f), new Vector3(-0.08f, 0.22f, 0.02f), legLo);
        MakeBlock(parent, "FootL", new Vector3(0.1f, 0.02f, 0.12f), new Vector3(-0.08f, 0.16f, 0.03f), foot);
        MakeBlock(parent, "LegR_Upper", new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0.08f, 0.32f, 0f), legUp);
        MakeBlock(parent, "LegR_Lower", new Vector3(0.03f, 0.1f, 0.03f), new Vector3(0.08f, 0.22f, 0.02f), legLo);
        MakeBlock(parent, "FootR", new Vector3(0.1f, 0.02f, 0.12f), new Vector3(0.08f, 0.16f, 0.03f), foot);
    }

    private static void BuildTurkey(Transform parent)
    {
        Color brown = new Color(0.5f, 0.3f, 0.15f);
        Color darkBrown = new Color(0.35f, 0.2f, 0.1f);
        Color red = new Color(0.8f, 0.15f, 0.1f);
        Color legUp = new Color(0.48f, 0.28f, 0.13f);
        Color legLo = new Color(0.42f, 0.24f, 0.1f);
        Color foot = new Color(0.38f, 0.22f, 0.08f);
        Color wingUp = new Color(0.48f, 0.28f, 0.13f);
        Color wingLo = new Color(0.42f, 0.24f, 0.1f);

        MakeSphere(parent, "Body", new Vector3(0f, 0.5f, 0f), 0.7f, brown);
        MakeBlock(parent, "Breast", new Vector3(0.35f, 0.35f, 0.2f), new Vector3(0f, 0.45f, 0.2f), brown);
        MakeBlock(parent, "Head", new Vector3(0.15f, 0.18f, 0.15f), new Vector3(0f, 0.72f, 0.3f), brown);
        MakeBlock(parent, "Wattle", new Vector3(0.05f, 0.1f, 0.03f), new Vector3(0f, 0.62f, 0.38f), red);
        MakeBlock(parent, "Beak", new Vector3(0.05f, 0.03f, 0.08f), new Vector3(0f, 0.7f, 0.4f), new Color(0.8f, 0.7f, 0.3f));
        MakeBlock(parent, "EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.06f, 0.75f, 0.38f), Color.black);
        MakeBlock(parent, "EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.06f, 0.75f, 0.38f), Color.black);

        for (int i = 0; i < 5; i++)
        {
            float angle = -30f + i * 15f;
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.35f;
            float y = 0.6f + Mathf.Cos(angle * Mathf.Deg2Rad) * 0.1f;
            MakeBlock(parent, "Fan" + i, new Vector3(0.04f, 0.2f, 0.02f), new Vector3(x, y, -0.3f), darkBrown);
        }

        MakeBlock(parent, "WingL_Upper", new Vector3(0.04f, 0.2f, 0.25f), new Vector3(-0.32f, 0.55f, 0f), wingUp);
        MakeBlock(parent, "WingL_Lower", new Vector3(0.03f, 0.15f, 0.18f), new Vector3(-0.38f, 0.45f, -0.05f), wingLo);
        MakeBlock(parent, "WingR_Upper", new Vector3(0.04f, 0.2f, 0.25f), new Vector3(0.32f, 0.55f, 0f), wingUp);
        MakeBlock(parent, "WingR_Lower", new Vector3(0.03f, 0.15f, 0.18f), new Vector3(0.38f, 0.45f, -0.05f), wingLo);
        MakeBlock(parent, "LegL_Upper", new Vector3(0.045f, 0.14f, 0.045f), new Vector3(-0.1f, 0.35f, 0f), legUp);
        MakeBlock(parent, "LegL_Lower", new Vector3(0.035f, 0.14f, 0.035f), new Vector3(-0.1f, 0.21f, 0.02f), legLo);
        MakeBlock(parent, "FootL", new Vector3(0.1f, 0.02f, 0.12f), new Vector3(-0.1f, 0.13f, 0.03f), foot);
        MakeBlock(parent, "LegR_Upper", new Vector3(0.045f, 0.14f, 0.045f), new Vector3(0.1f, 0.35f, 0f), legUp);
        MakeBlock(parent, "LegR_Lower", new Vector3(0.035f, 0.14f, 0.035f), new Vector3(0.1f, 0.21f, 0.02f), legLo);
        MakeBlock(parent, "FootR", new Vector3(0.1f, 0.02f, 0.12f), new Vector3(0.1f, 0.13f, 0.03f), foot);
    }
}
