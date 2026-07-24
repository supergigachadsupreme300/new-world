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
        }
        else
        {
            PickWanderTarget();
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
    }

    private void Recover()
    {
        IsKnockedOut = false;
        Health = MaxHealth;
        transform.localRotation = Quaternion.identity;
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
        var pos = transform.position;
        pos.y = -2f;
        transform.position = pos;
        _spawnAnimTimer = 0f;
        StartCoroutine(SpawnAnimation());
    }

    private IEnumerator SpawnAnimation()
    {
        float duration = 2f;
        float startY = -2f;
        float endY = _origin.y;
        _spawnAnimTimer = 0f;

        while (_spawnAnimTimer < duration)
        {
            _spawnAnimTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_spawnAnimTimer / duration);
            float eased = t * t * (3f - 2f * t);
            var pos = transform.position;
            pos.y = Mathf.Lerp(startY, endY, eased);
            transform.position = pos;
            yield return null;
        }

        var finalPos = transform.position;
        finalPos.y = endY;
        transform.position = finalPos;
        _origin = finalPos;
        _spawned = true;
    }

    // ═══════════════════════════════════════
    //  PROCEDURAL MODELS
    // ═══════════════════════════════════════

    private void BuildModel()
    {
        _modelRoot = new GameObject("Model");
        _modelRoot.transform.SetParent(transform, false);

        switch (Type)
        {
            case AnimalType.Cow: BuildCow(); break;
            case AnimalType.Pig: BuildPig(); break;
            case AnimalType.Sheep: BuildSheep(); break;
            case AnimalType.Goat: BuildGoat(); break;
            case AnimalType.Chicken: BuildChicken(); break;
            case AnimalType.Duck: BuildDuck(); break;
            case AnimalType.Turkey: BuildTurkey(); break;
        }
    }

    private GameObject MakeBlock(string name, Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_modelRoot.transform, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        go.GetComponent<Renderer>().material.color = color;
        Destroy(go.GetComponent<Collider>());
        return go;
    }

    private GameObject MakeSphere(string name, Vector3 position, float diameter, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(_modelRoot.transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(diameter, diameter, diameter);
        go.GetComponent<Renderer>().material.color = color;
        Destroy(go.GetComponent<Collider>());
        return go;
    }

    private void BuildCow()
    {
        Color white = new Color(0.95f, 0.95f, 0.95f);
        Color black = new Color(0.1f, 0.1f, 0.1f);
        Color pink = new Color(0.9f, 0.6f, 0.6f);
        Color horn = new Color(0.8f, 0.75f, 0.6f);

        MakeBlock("Body", new Vector3(0.8f, 0.6f, 1.2f), new Vector3(0f, 0.6f, 0f), white);
        MakeBlock("Head", new Vector3(0.4f, 0.35f, 0.35f), new Vector3(0f, 0.7f, 0.7f), white);
        MakeBlock("Snout", new Vector3(0.25f, 0.15f, 0.1f), new Vector3(0f, 0.6f, 0.9f), pink);
        MakeBlock("Patch1", new Vector3(0.5f, 0.3f, 0.4f), new Vector3(0.2f, 0.7f, 0.1f), black);
        MakeBlock("Patch2", new Vector3(0.3f, 0.25f, 0.5f), new Vector3(-0.15f, 0.55f, -0.2f), black);
        MakeBlock("EarL", new Vector3(0.1f, 0.05f, 0.08f), new Vector3(-0.25f, 0.85f, 0.65f), pink);
        MakeBlock("EarR", new Vector3(0.1f, 0.05f, 0.08f), new Vector3(0.25f, 0.85f, 0.65f), pink);
        MakeBlock("HornL", new Vector3(0.06f, 0.15f, 0.06f), new Vector3(-0.15f, 0.95f, 0.65f), horn);
        MakeBlock("HornR", new Vector3(0.06f, 0.15f, 0.06f), new Vector3(0.15f, 0.95f, 0.65f), horn);
        MakeBlock("EyeL", new Vector3(0.05f, 0.05f, 0.05f), new Vector3(-0.12f, 0.78f, 0.82f), Color.black);
        MakeBlock("EyeR", new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0.12f, 0.78f, 0.82f), Color.black);
        MakeBlock("Tail", new Vector3(0.04f, 0.04f, 0.3f), new Vector3(0f, 0.7f, -0.7f), pink);
        Color legC = new Color(0.85f, 0.85f, 0.85f);
        MakeBlock("LegFL", new Vector3(0.12f, 0.35f, 0.12f), new Vector3(-0.25f, 0.18f, 0.35f), legC);
        MakeBlock("LegFR", new Vector3(0.12f, 0.35f, 0.12f), new Vector3(0.25f, 0.18f, 0.35f), legC);
        MakeBlock("LegBL", new Vector3(0.12f, 0.35f, 0.12f), new Vector3(-0.25f, 0.18f, -0.35f), legC);
        MakeBlock("LegBR", new Vector3(0.12f, 0.35f, 0.12f), new Vector3(0.25f, 0.18f, -0.35f), legC);
    }

    private void BuildPig()
    {
        Color pink = new Color(0.95f, 0.65f, 0.6f);
        Color darkPink = new Color(0.8f, 0.45f, 0.4f);
        Color nose = new Color(0.85f, 0.5f, 0.5f);

        MakeSphere("Body", new Vector3(0f, 0.45f, 0f), 0.9f, pink);
        MakeSphere("Head", new Vector3(0f, 0.5f, 0.45f), 0.5f, pink);
        MakeSphere("Snout", new Vector3(0f, 0.45f, 0.7f), 0.25f, nose);
        MakeBlock("NostrilL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.04f, 0.45f, 0.8f), darkPink);
        MakeBlock("NostrilR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.04f, 0.45f, 0.8f), darkPink);
        MakeBlock("EarL", new Vector3(0.12f, 0.1f, 0.06f), new Vector3(-0.18f, 0.72f, 0.4f), darkPink);
        MakeBlock("EarR", new Vector3(0.12f, 0.1f, 0.06f), new Vector3(0.18f, 0.72f, 0.4f), darkPink);
        MakeBlock("EyeL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.1f, 0.58f, 0.6f), Color.black);
        MakeBlock("EyeR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.1f, 0.58f, 0.6f), Color.black);
        MakeBlock("Tail", new Vector3(0.03f, 0.15f, 0.03f), new Vector3(0f, 0.6f, -0.5f), darkPink);
        MakeBlock("LegFL", new Vector3(0.1f, 0.22f, 0.1f), new Vector3(-0.2f, 0.11f, 0.25f), darkPink);
        MakeBlock("LegFR", new Vector3(0.1f, 0.22f, 0.1f), new Vector3(0.2f, 0.11f, 0.25f), darkPink);
        MakeBlock("LegBL", new Vector3(0.1f, 0.22f, 0.1f), new Vector3(-0.2f, 0.11f, -0.25f), darkPink);
        MakeBlock("LegBR", new Vector3(0.1f, 0.22f, 0.1f), new Vector3(0.2f, 0.11f, -0.25f), darkPink);
    }

    private void BuildSheep()
    {
        Color wool = new Color(0.95f, 0.93f, 0.88f);
        Color dark = new Color(0.4f, 0.35f, 0.3f);
        Color eyeC = new Color(0.05f, 0.05f, 0.05f);

        for (int i = 0; i < 6; i++)
        {
            float x = Random.Range(-0.25f, 0.25f);
            float y = 0.45f + Random.Range(-0.1f, 0.1f);
            float z = Random.Range(-0.35f, 0.35f);
            MakeSphere("Wool" + i, new Vector3(x, y, z), Random.Range(0.35f, 0.5f), wool);
        }
        MakeBlock("Head", new Vector3(0.22f, 0.25f, 0.25f), new Vector3(0f, 0.6f, 0.55f), dark);
        MakeBlock("EyeL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.08f, 0.65f, 0.67f), eyeC);
        MakeBlock("EyeR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.08f, 0.65f, 0.67f), eyeC);
        MakeBlock("LegFL", new Vector3(0.08f, 0.28f, 0.08f), new Vector3(-0.18f, 0.14f, 0.25f), dark);
        MakeBlock("LegFR", new Vector3(0.08f, 0.28f, 0.08f), new Vector3(0.18f, 0.14f, 0.25f), dark);
        MakeBlock("LegBL", new Vector3(0.08f, 0.28f, 0.08f), new Vector3(-0.18f, 0.14f, -0.25f), dark);
        MakeBlock("LegBR", new Vector3(0.08f, 0.28f, 0.08f), new Vector3(0.18f, 0.14f, -0.25f), dark);
    }

    private void BuildGoat()
    {
        Color brown = new Color(0.6f, 0.45f, 0.3f);
        Color darkBrown = new Color(0.4f, 0.3f, 0.2f);
        Color horn = new Color(0.75f, 0.7f, 0.55f);

        MakeBlock("Body", new Vector3(0.5f, 0.45f, 0.9f), new Vector3(0f, 0.5f, 0f), brown);
        MakeBlock("Head", new Vector3(0.25f, 0.3f, 0.3f), new Vector3(0f, 0.65f, 0.55f), brown);
        MakeBlock("Beard", new Vector3(0.06f, 0.15f, 0.06f), new Vector3(0f, 0.45f, 0.65f), darkBrown);
        MakeBlock("HornL", new Vector3(0.05f, 0.2f, 0.05f), new Vector3(-0.12f, 0.85f, 0.5f), horn);
        MakeBlock("HornR", new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0.12f, 0.85f, 0.5f), horn);
        MakeBlock("EyeL", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.1f, 0.7f, 0.7f), Color.black);
        MakeBlock("EyeR", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.1f, 0.7f, 0.7f), Color.black);
        MakeBlock("Tail", new Vector3(0.04f, 0.1f, 0.04f), new Vector3(0f, 0.6f, -0.5f), darkBrown);
        MakeBlock("LegFL", new Vector3(0.08f, 0.3f, 0.08f), new Vector3(-0.15f, 0.15f, 0.3f), darkBrown);
        MakeBlock("LegFR", new Vector3(0.08f, 0.3f, 0.08f), new Vector3(0.15f, 0.15f, 0.3f), darkBrown);
        MakeBlock("LegBL", new Vector3(0.08f, 0.3f, 0.08f), new Vector3(-0.15f, 0.15f, -0.3f), darkBrown);
        MakeBlock("LegBR", new Vector3(0.08f, 0.3f, 0.08f), new Vector3(0.15f, 0.15f, -0.3f), darkBrown);
    }

    private void BuildChicken()
    {
        Color white = new Color(0.95f, 0.93f, 0.88f);
        Color red = new Color(0.85f, 0.15f, 0.1f);
        Color yellow = new Color(0.95f, 0.85f, 0.2f);

        MakeSphere("Body", new Vector3(0f, 0.35f, 0f), 0.5f, white);
        MakeBlock("Head", new Vector3(0.15f, 0.18f, 0.15f), new Vector3(0f, 0.55f, 0.2f), white);
        MakeBlock("Comb", new Vector3(0.04f, 0.12f, 0.08f), new Vector3(0f, 0.68f, 0.18f), red);
        MakeBlock("Beak", new Vector3(0.06f, 0.04f, 0.08f), new Vector3(0f, 0.52f, 0.32f), yellow);
        MakeBlock("Wattle", new Vector3(0.04f, 0.06f, 0.04f), new Vector3(0f, 0.45f, 0.28f), red);
        MakeBlock("EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.06f, 0.58f, 0.28f), Color.black);
        MakeBlock("EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.06f, 0.58f, 0.28f), Color.black);
        MakeBlock("LegL", new Vector3(0.03f, 0.2f, 0.03f), new Vector3(-0.06f, 0.1f, 0f), yellow);
        MakeBlock("LegR", new Vector3(0.03f, 0.2f, 0.03f), new Vector3(0.06f, 0.1f, 0f), yellow);
        MakeBlock("Tail", new Vector3(0.06f, 0.15f, 0.15f), new Vector3(0f, 0.45f, -0.25f), white);
    }

    private void BuildDuck()
    {
        Color white = new Color(0.92f, 0.9f, 0.85f);
        Color orange = new Color(0.95f, 0.6f, 0.15f);

        MakeSphere("Body", new Vector3(0f, 0.32f, 0f), 0.55f, white);
        MakeBlock("Head", new Vector3(0.2f, 0.22f, 0.2f), new Vector3(0f, 0.52f, 0.22f), white);
        MakeBlock("Bill", new Vector3(0.12f, 0.04f, 0.15f), new Vector3(0f, 0.48f, 0.38f), orange);
        MakeBlock("EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.08f, 0.56f, 0.32f), Color.black);
        MakeBlock("EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.08f, 0.56f, 0.32f), Color.black);
        MakeBlock("LegL", new Vector3(0.04f, 0.18f, 0.06f), new Vector3(-0.08f, 0.09f, 0f), orange);
        MakeBlock("LegR", new Vector3(0.04f, 0.18f, 0.06f), new Vector3(0.08f, 0.09f, 0f), orange);
        MakeBlock("Tail", new Vector3(0.06f, 0.1f, 0.12f), new Vector3(0f, 0.4f, -0.28f), white);
    }

    private void BuildTurkey()
    {
        Color brown = new Color(0.5f, 0.3f, 0.15f);
        Color darkBrown = new Color(0.35f, 0.2f, 0.1f);
        Color red = new Color(0.8f, 0.15f, 0.1f);

        MakeSphere("Body", new Vector3(0f, 0.4f, 0f), 0.7f, brown);
        MakeBlock("Head", new Vector3(0.15f, 0.18f, 0.15f), new Vector3(0f, 0.62f, 0.3f), brown);
        MakeBlock("Wattle", new Vector3(0.05f, 0.1f, 0.03f), new Vector3(0f, 0.52f, 0.38f), red);
        MakeBlock("Beak", new Vector3(0.05f, 0.03f, 0.08f), new Vector3(0f, 0.6f, 0.4f), new Color(0.8f, 0.7f, 0.3f));
        MakeBlock("EyeL", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.06f, 0.65f, 0.38f), Color.black);
        MakeBlock("EyeR", new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.06f, 0.65f, 0.38f), Color.black);

        for (int i = 0; i < 5; i++)
        {
            float angle = -30f + i * 15f;
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.35f;
            float y = 0.5f + Mathf.Cos(angle * Mathf.Deg2Rad) * 0.1f;
            MakeBlock("Fan" + i, new Vector3(0.04f, 0.2f, 0.02f), new Vector3(x, y, -0.3f), darkBrown);
        }

        MakeBlock("LegL", new Vector3(0.04f, 0.22f, 0.04f), new Vector3(-0.1f, 0.11f, 0f), darkBrown);
        MakeBlock("LegR", new Vector3(0.04f, 0.22f, 0.04f), new Vector3(0.1f, 0.11f, 0f), darkBrown);
    }
}
