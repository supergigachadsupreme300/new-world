using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour
{
    public int MaxHealth = 50;
    public int Damage = 10;
    public float MoveSpeed = 2.5f;
    public float ChaseRange = 8f;
    public float AttackRange = 1.5f;
    public float AttackCooldown = 1.2f;
    public float PatrolRange = 6f;
    public float PatrolSpeed = 1.5f;
    public bool IsGiant;
    public bool IsBoss;

    public static bool BossFightActive;

    public int CurrentHealth => _health;

    private int _health;
    [SerializeField] private Transform _player;
    private PlayerController _playerController;
    private Vector3 _origin;
    private Vector3 _patrolTarget;
    private float _attackTimer;
    private bool _isDead;
    private float _respawnTimer;

    public bool IsDead => _isDead;

    private Transform _modelRoot;
    private Transform _upperTorso;
    private Transform _midTorso;
    private Transform _lowerTorso;
    private Transform _armL;
    private Transform _armR;
    private Transform _armL2;
    private Transform _armR2;
    private Transform _legL;
    private Transform _legR;
    private Transform _kneeL;
    private Transform _kneeR;
    private float _walkCycle;
    private float _bobTimer;
    private bool _isMoving;
    private bool _isAttacking;
    private int _followTick;

    private GameObject _structureTarget;
    private float _stuckTimer;
    private float _structureAttackTimer;
    private float _structureCheckTimer;
    private Vector3 _lastChasePos;
    private bool _hasChasePos;
    private bool _knockOut;
    private static readonly Collider[] _structureSearchBuffer = new Collider[64];

    private bool _bossSkillActive;
    private float _bossSkillTimer;
    private bool _enraged;
    private Coroutine _bossSkillRoutine;
    private readonly List<GameObject> _activeProjectiles = new List<GameObject>();

    private const float STUCK_THRESHOLD = 4f;
    private const float STRUCTURE_ATTACK_COOLDOWN = 2f;
    private const float STRUCTURE_SEARCH_RANGE = 5f;
    private const float STUCK_MOVEMENT_FACTOR = 0.3f;

    private void Awake()
    {
        _origin = transform.position;
        _patrolTarget = GetRandomPatrolPoint();
        if (_player == null)
            _player = Object.FindAnyObjectByType<PlayerController>()?.transform;
        if (_player != null && _playerController == null)
            _playerController = _player.GetComponent<PlayerController>();
    }

    private void Start()
    {
        _health = MaxHealth;
        BuildModel();
        if (IsBoss)
        {
            BossFightActive = true;
            _bossSkillTimer = 2f;
            GameManager.Instance?.UIManager?.ShowBossBar(Localization.T("Quỷ Vương"), CurrentHealth, MaxHealth);
        }
    }

    private void BuildModel()
    {
        if (IsBoss)
            _modelRoot = BossModelBuilder.BuildBoss(transform);
        else if (IsGiant)
            _modelRoot = EnemyModelBuilder.BuildGiantEnemy(transform);
        else
            _modelRoot = EnemyModelBuilder.BuildRegularEnemy(transform);

        CaptureJoints();
        ConfigureCollider();
    }

    private void CaptureJoints()
    {
        if (_modelRoot == null) return;

        _upperTorso = FindChild(_modelRoot, "UpperTorso");
        _midTorso = FindChild(_modelRoot, "MidTorso");
        _lowerTorso = FindChild(_modelRoot, "LowerTorso");
        _armL = FindChild(_modelRoot, "ArmL");
        _armR = FindChild(_modelRoot, "ArmR");
        _armL2 = FindChild(_modelRoot, "ArmL2");
        _armR2 = FindChild(_modelRoot, "ArmR2");
        _legL = FindChild(_modelRoot, "LegL");
        _legR = FindChild(_modelRoot, "LegR");
        _kneeL = FindChild(_modelRoot, "KneeL");
        _kneeR = FindChild(_modelRoot, "KneeR");
    }

    private Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name)
                return child;
            var found = FindChild(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void ConfigureCollider()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider>();

        if (IsBoss)
        {
            col.size = new Vector3(1.25f, 3.2f, 0.8f);
            col.center = new Vector3(0f, 1.6f, 0f);
        }
        else if (IsGiant)
        {
            col.size = new Vector3(0.8f, 2.8f, 0.4f);
            col.center = new Vector3(0f, 1.4f, 0f);
        }
        else
        {
            col.size = new Vector3(0.6f, 1.8f, 0.3f);
            col.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    private void Update()
    {
        if (_isDead)
        {
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
                Respawn();
            return;
        }
        if (_playerController == null)
            _playerController = GameManager.Instance?.Player;
        if (_player == null && _playerController != null)
            _player = _playerController.transform;
        if (_player == null)
            _player = Object.FindAnyObjectByType<PlayerController>()?.transform;
        if (_player != null && _playerController == null)
            _playerController = _player.GetComponent<PlayerController>();
        if (_player == null)
            return;
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPlayerDead) return;

        if (!IsBoss && EnforceSacredZone()) return;

        float distance = Vector3.Distance(transform.position, _player.position);

        if (IsBoss && distance <= 45f)
            BossFightActive = true;

        if (IsBoss)
        {
            HandleBossSkills(distance);
            if (_bossSkillActive)
                return;
        }

        Transform target = _player;
        float targetDist = distance;
        var goblin = GoblinPet.Instance;
        if (goblin != null && !goblin.IsDead && !goblin.IsHiddenInHut)
        {
            float goblinDist = Vector3.Distance(transform.position, goblin.transform.position);
            if (goblinDist < targetDist)
            {
                target = goblin.transform;
                targetDist = goblinDist;
            }
        }

        _isAttacking = false;
        _isMoving = false;

        if (_structureTarget != null)
        {
            HandleStructureAttack(distance);
        }
        else if (targetDist <= ChaseRange)
        {
            if (targetDist <= AttackRange)
            {
                _stuckTimer = 0f;
                _hasChasePos = false;
                _isAttacking = true;
                Attack(target);
            }
            else
            {
                FollowTarget(target);
                TrackStuck();
            }
        }
        else
        {
            Patrol();
            _stuckTimer = 0f;
            _hasChasePos = false;
        }

        AnimateModel();
    }

    private const float SacredZoneRadius = 7.5f;
    private const float SacredPushSpeed = 14f;
    private const float PagodaHalfSize = 7f;

    private bool EnforceSacredZone()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return false;

        Vector3 center = wb.PagodaPosition;
        center.y = 0f;
        Vector3 flat = transform.position;
        flat.y = 0f;

        Vector3 away = flat - center;
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.forward;
        else
            away.Normalize();

        if (Vector3.Distance(flat, center) < SacredZoneRadius)
        {
            _knockOut = true;
            _structureTarget = null;
            _stuckTimer = 0f;
            _hasChasePos = false;

            Vector3 originFlat = _origin;
            originFlat.y = 0f;
            if (Vector3.Distance(originFlat, center) < SacredZoneRadius)
            {
                Vector3 originAway = originFlat - center;
                if (originAway.sqrMagnitude < 0.0001f)
                    originAway = Vector3.forward;
                else
                    originAway.Normalize();
                _origin = center + originAway * (SacredZoneRadius + 1.5f);
                _origin.y = 0f;
                _patrolTarget = GetRandomPatrolPoint();
            }
        }

        if (!_knockOut) return false;

        Vector3 target = center + away * (SacredZoneRadius + 1.5f);
        if (Vector3.Distance(flat, center) < PagodaHalfSize)
            transform.position = target;
        else
            transform.position = Vector3.MoveTowards(transform.position, target, SacredPushSpeed * Time.deltaTime);
        transform.LookAt(target);
        _isMoving = true;
        _isAttacking = false;

        if ((transform.position - target).sqrMagnitude < 0.04f)
            _knockOut = false;
        return true;
    }

    private void AnimateModel()
    {
        if (_modelRoot == null) return;
        if (_bossSkillActive)
            return;

        if (_isAttacking)
        {
            _bobTimer += Time.deltaTime * 8f;
            float shakeX = Mathf.Sin(_bobTimer * 3f) * 0.03f;
            float shakeZ = Mathf.Cos(_bobTimer * 4f) * 0.02f;
            _modelRoot.localPosition = new Vector3(shakeX, 0f, shakeZ);
            _modelRoot.localRotation = Quaternion.Euler(Mathf.Sin(_bobTimer * 5f) * 8f, 0f, 0f);
            ResetJoints();
        }
        else if (_isMoving)
        {
            RunAnimation();
        }
        else
        {
            IdleAnimation();
        }
    }

    private void RunAnimation()
    {
        float speed = IsGiant ? MoveSpeed * 0.8f : MoveSpeed;
        _walkCycle += Time.deltaTime * speed * 7f;
        float sin = Mathf.Sin(_walkCycle);
        float cos = Mathf.Cos(_walkCycle);

        // Legs swing at hips
        float hipSwing = sin * 35f;
        if (_legL != null) _legL.localRotation = Quaternion.Euler(hipSwing, 0f, 0f);
        if (_legR != null) _legR.localRotation = Quaternion.Euler(-hipSwing, 0f, 0f);

        // Knees bend (bend when leg swings back, straighten when forward)
        float kneeBendL = Mathf.Max(0f, -sin) * 40f;
        float kneeBendR = Mathf.Max(0f, sin) * 40f;
        if (_kneeL != null) _kneeL.localRotation = Quaternion.Euler(-kneeBendL, 0f, 0f);
        if (_kneeR != null) _kneeR.localRotation = Quaternion.Euler(-kneeBendR, 0f, 0f);

        // Arms swing opposite to legs
        float armSwing = sin * 25f;
        if (_armL != null) _armL.localRotation = Quaternion.Euler(-armSwing, 0f, 0f);
        if (_armR != null) _armR.localRotation = Quaternion.Euler(armSwing, 0f, 0f);

        // Second pair of arms (boss) swing in the opposite phase
        float armSwing2 = armSwing * 0.8f;
        if (_armL2 != null) _armL2.localRotation = Quaternion.Euler(armSwing2, 0f, 0f);
        if (_armR2 != null) _armR2.localRotation = Quaternion.Euler(-armSwing2, 0f, 0f);

        // Torso forward lean + subtle bob (3 segments with base tilts)
        _bobTimer += Time.deltaTime * speed * 5f;
        float bob = Mathf.Sin(_bobTimer * 2f) * 0.015f;
        if (_upperTorso != null)
            _upperTorso.localRotation = Quaternion.Euler(8f, 0f, 0f);
        if (_midTorso != null)
            _midTorso.localRotation = Quaternion.Euler(5f, 0f, 0f);
        if (_lowerTorso != null)
            _lowerTorso.localRotation = Quaternion.Euler(3f, 0f, 0f);
        _modelRoot.localPosition = new Vector3(0f, bob, 0f);
        _modelRoot.localRotation = Quaternion.identity;
    }

    private void IdleAnimation()
    {
        _bobTimer += Time.deltaTime * 1.5f;
        float bob = Mathf.Sin(_bobTimer) * 0.008f;

        // Subtle breathing on torso segments
        float breath = Mathf.Sin(_bobTimer * 0.8f) * 1.5f;
        if (_upperTorso != null)
            _upperTorso.localRotation = Quaternion.Euler(breath, 0f, 0f);
        if (_midTorso != null)
            _midTorso.localRotation = Quaternion.Euler(breath * 0.6f, 0f, 0f);
        if (_lowerTorso != null)
            _lowerTorso.localRotation = Quaternion.Euler(breath * 0.3f, 0f, 0f);

        _modelRoot.localPosition = new Vector3(0f, bob, 0f);
        _modelRoot.localRotation = Quaternion.identity;

        ResetJoints();
    }

    private void ResetJoints()
    {
        if (_legL != null) _legL.localRotation = Quaternion.identity;
        if (_legR != null) _legR.localRotation = Quaternion.identity;
        if (_kneeL != null) _kneeL.localRotation = Quaternion.identity;
        if (_kneeR != null) _kneeR.localRotation = Quaternion.identity;
        if (_armL != null) _armL.localRotation = Quaternion.identity;
        if (_armR != null) _armR.localRotation = Quaternion.identity;
        if (_armL2 != null) _armL2.localRotation = Quaternion.identity;
        if (_armR2 != null) _armR2.localRotation = Quaternion.identity;
    }

    public void TakeDamage(int amount)
    {
        if (_isDead)
            return;

        _health -= amount;
        if (IsBoss && GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.SetBossBar(Mathf.Max(0, _health), MaxHealth);
        if (_health <= 0)
        {
            Die();
        }
    }

    private void Patrol()
    {
        if (Vector3.Distance(transform.position, _patrolTarget) < 0.2f)
            _patrolTarget = GetRandomPatrolPoint();

        transform.position = Vector3.MoveTowards(transform.position, _patrolTarget, PatrolSpeed * Time.deltaTime);
        transform.LookAt(_patrolTarget);
        _isMoving = true;
    }

    private void FollowTarget(Transform target)
    {
        Vector3 dir = (target.position - transform.position).normalized;
        float step = MoveSpeed * Time.deltaTime;
        Vector3 origin = transform.position + Vector3.up * 0.9f;

        if (Physics.Raycast(origin, dir, out var hit, step + 0.5f))
        {
            if (IsDoorCollider(hit.collider))
            {
                transform.position = Vector3.MoveTowards(transform.position, target.position, step);
                transform.LookAt(target);
                _isMoving = true;
                return;
            }

            _followTick++;
            if ((_followTick & 7) == 0)
            {
                var wb = WorldBuilder.Instance;
                if (wb != null && wb.FindBuilding(hit.collider.gameObject) != null)
                {
                    _isMoving = false;
                    return;
                }
            }
        }

        transform.position = Vector3.MoveTowards(transform.position, target.position, step);
        transform.LookAt(target);
        _isMoving = true;
    }

    private bool IsDoorCollider(Collider col)
    {
        if (col == null)
            return false;
        Transform t = col.transform;
        while (t != null)
        {
            if (t.name == "Door")
                return true;
            t = t.parent;
        }
        return false;
    }

    private void Attack(Transform target)
    {
        _attackTimer += Time.deltaTime;
        if (_attackTimer < AttackCooldown)
            return;

        _attackTimer = 0f;

        var goblin = GoblinPet.Instance;
        if (target != null && goblin != null && target == goblin.transform)
        {
            goblin.TakeDamage(Damage);
            Debug.Log($"Enemy hit goblin for {Damage}");
            return;
        }

        var player = _playerController;
        if (player != null)
        {
            player.TakeDamage(Damage);
            Debug.Log($"Enemy hit player for {Damage}");
        }
    }

    private void TrackStuck()
    {
        if (_hasChasePos)
        {
            float moved = Vector3.Distance(transform.position, _lastChasePos);
            float expected = MoveSpeed * Time.deltaTime;
            if (moved < expected * STUCK_MOVEMENT_FACTOR)
                _stuckTimer += Time.deltaTime;
            else
                _stuckTimer = Mathf.Max(0f, _stuckTimer - Time.deltaTime * 0.5f);
        }
        _lastChasePos = transform.position;
        _hasChasePos = true;

        if (_stuckTimer >= STUCK_THRESHOLD)
        {
            FindNearestStructure();
        }
    }

    private void HandleStructureAttack(float playerDistance)
    {
        _structureCheckTimer += Time.deltaTime;
        if (_structureCheckTimer >= 2f)
        {
            _structureCheckTimer = 0f;
            if (playerDistance <= ChaseRange)
            {
                _structureTarget = null;
                _stuckTimer = 0f;
                _hasChasePos = false;
                return;
            }
        }

        if (_structureTarget == null)
        {
            FindNearestStructure();
            return;
        }

        float structDist = Vector3.Distance(transform.position, _structureTarget.transform.position);

        if (structDist <= AttackRange)
        {
            _structureAttackTimer += Time.deltaTime;
            if (_structureAttackTimer >= STRUCTURE_ATTACK_COOLDOWN)
            {
                AttackStructure();
                _structureAttackTimer = 0f;
            }
            _isAttacking = true;
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, _structureTarget.transform.position, MoveSpeed * Time.deltaTime);
            transform.LookAt(_structureTarget.transform.position);
            _isMoving = true;
        }
    }

    private void FindNearestStructure()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, STRUCTURE_SEARCH_RANGE, _structureSearchBuffer);
        GameObject closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var col = _structureSearchBuffer[i];
            var building = wb.FindBuilding(col.gameObject);
            if (building == null) continue;
            if (building.PartStates == null || building.PartStates.Count == 0) continue;
            if (building.Type != null && building.Type.StartsWith("structure_part_Pagoda_")) continue;

            // Find the specific part entity for this collider
            GameObject partEntity = null;
            foreach (var ps in building.PartStates)
            {
                if (ps.Entity == null) continue;
                if (col.gameObject == ps.Entity || (col.transform.parent != null && col.transform.parent.gameObject == ps.Entity))
                {
                    partEntity = ps.Entity;
                    break;
                }
            }
            if (partEntity == null) continue;

            float d = Vector3.Distance(transform.position, partEntity.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = partEntity;
            }
        }

        _structureTarget = closest;
        _stuckTimer = 0f;
        _structureAttackTimer = 0f;
        _structureCheckTimer = 0f;
    }

    private void AttackStructure()
    {
        if (_structureTarget == null) return;
        var wb = WorldBuilder.Instance;
        if (wb == null) return;

        wb.DamageBuilding(_structureTarget);
        Debug.Log($"Enemy attacked structure: {_structureTarget.name}");

        // If the target was destroyed, find a new one
        var building = wb.FindBuilding(_structureTarget);
        if (building == null || building.DestroyedParts >= building.TotalParts)
        {
            _structureTarget = null;
        }
        else
        {
            // Check if the specific part still exists
            bool stillAlive = false;
            foreach (var ps in building.PartStates)
            {
                if (ps.Entity == _structureTarget)
                {
                    stillAlive = true;
                    break;
                }
            }
            if (!stillAlive)
                _structureTarget = null;
        }
    }

    private void HandleBossSkills(float playerDist)
    {
        if (_isDead)
            return;

        if (!_enraged && _health <= MaxHealth * 0.5f)
            StartEnrage();

        _bossSkillTimer -= Time.deltaTime;
        if (_bossSkillTimer > 0f)
            return;
        if (playerDist > 20f)
            return;
        if (_player == null)
            return;

        if (playerDist <= 4.5f)
        {
            if (playerDist > 3f && Random.value < 0.2f)
                StartBossSkill(BossCharge());
            else
                StartBossSkill(BossSlam());
        }
        else if (playerDist > 8f)
        {
            StartBossSkill(BossBarrage());
        }
        else
        {
            if (Random.value < 0.5f)
                StartBossSkill(BossCharge());
            else
                StartBossSkill(BossBarrage());
        }
    }

    private void StartBossSkill(IEnumerator routine)
    {
        if (_bossSkillActive)
            return;
        _bossSkillActive = true;
        _bossSkillRoutine = StartCoroutine(routine);
    }

    private void StartEnrage()
    {
        _enraged = true;
        MoveSpeed = 2.3f;
        AttackCooldown = 0.6f;
        SoundManager.Instance?.Play("bonk", 1f);

        var eyeL = FindChild(_modelRoot, "EyeL");
        var eyeR = FindChild(_modelRoot, "EyeR");
        var glow = new Color(1f, 0.15f, 0.04f);
        if (eyeL != null)
        {
            var r = eyeL.GetComponent<Renderer>();
            if (r != null) r.material.color = glow;
        }
        if (eyeR != null)
        {
            var r = eyeR.GetComponent<Renderer>();
            if (r != null) r.material.color = glow;
        }
        _bossSkillTimer = Mathf.Min(_bossSkillTimer, 1f);
    }

    private void SetUpperArms(Quaternion rot)
    {
        if (_armL != null) _armL.localRotation = rot;
        if (_armR != null) _armR.localRotation = rot;
    }

    private void SetLowerArms(Quaternion rot)
    {
        if (_armL2 != null) _armL2.localRotation = rot;
        if (_armR2 != null) _armR2.localRotation = rot;
    }

    private void ResetBossArms()
    {
        SetUpperArms(Quaternion.identity);
        SetLowerArms(Quaternion.identity);
    }

    private IEnumerator BossSlam()
    {
        if (_player != null)
            transform.LookAt(_player.position);

        float t = 0f;
        while (t < 0.9f)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / 0.9f);
            SetUpperArms(Quaternion.Euler(-70f * p, 0f, 0f));
            SetLowerArms(Quaternion.Euler(-40f * p, 0f, 0f));
            yield return null;
        }

        t = 0f;
        while (t < 0.22f)
        {
            t += Time.deltaTime;
            float p = t / 0.22f;
            SetUpperArms(Quaternion.Euler(75f * p, 0f, 0f));
            SetLowerArms(Quaternion.Euler(50f * p, 0f, 0f));
            yield return null;
        }

        SoundManager.Instance?.Play("bonk", 0.9f);
        if (_player != null && Vector3.Distance(transform.position, _player.position) <= 3.5f)
            _playerController?.TakeDamage(14);
        SpawnSlamCracks();

        t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            if (_modelRoot != null)
                _modelRoot.localPosition = new Vector3(Random.Range(-0.06f, 0.06f), 0f, Random.Range(-0.06f, 0.06f));
            yield return null;
        }
        if (_modelRoot != null)
            _modelRoot.localPosition = Vector3.zero;

        ResetJoints();
        ResetBossArms();
        _bossSkillActive = false;
        _bossSkillTimer = _enraged ? 5f : 6f;
    }

    private IEnumerator BossBarrage()
    {
        if (_player == null)
        {
            _bossSkillActive = false;
            yield break;
        }
        transform.LookAt(_player.position);

        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / 0.8f);
            SetUpperArms(Quaternion.Euler(-60f * p, 0f, 0f));
            SetLowerArms(Quaternion.Euler(-45f * p, 0f, 0f));
            yield return null;
        }

        Vector3 chest = transform.position + Vector3.up * 1.5f;
        var wait = new WaitForSeconds(0.16f);
        for (int i = 0; i < 5; i++)
        {
            if (_player == null)
                break;
            Vector3 playerChest = _player.position + Vector3.up * 0.8f;
            Vector3 dir = (playerChest - chest).normalized;
            dir = Quaternion.Euler(0f, Random.Range(-7f, 7f), 0f) * dir;
            float maxDist = Vector3.Distance(chest, playerChest) + 4f;
            SpawnBossProjectile(chest, dir, maxDist);
            SoundManager.Instance?.Play("bonk", 0.35f);
            yield return wait;
        }

        yield return new WaitForSeconds(0.4f);
        ResetJoints();
        ResetBossArms();
        _bossSkillActive = false;
        _bossSkillTimer = _enraged ? 6f : 7f;
    }

    private void SpawnBossProjectile(Vector3 origin, Vector3 dir, float maxDist)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BossBone";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * 0.22f;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.85f, 0.8f, 0.72f);
        _activeProjectiles.Add(go);
        StartCoroutine(BossProjectileFlight(go, dir, maxDist));
    }

    private IEnumerator BossProjectileFlight(GameObject proj, Vector3 dir, float maxDist)
    {
        float travelled = 0f;
        while (proj != null && travelled < maxDist)
        {
            float step = 9f * Time.deltaTime;
            travelled += step;
            proj.transform.position += dir * step;

            if (_player != null)
            {
                Vector3 toPlayer = _player.position + Vector3.up * 0.8f - proj.transform.position;
                if (toPlayer.magnitude < 0.9f)
                {
                    _playerController?.TakeDamage(8);
                    SoundManager.Instance?.Play("bonk", 0.6f);
                    _activeProjectiles.Remove(proj);
                    Destroy(proj);
                    yield break;
                }
            }
            yield return null;
        }
        if (proj != null)
        {
            _activeProjectiles.Remove(proj);
            Destroy(proj);
        }
    }

    private IEnumerator BossCharge()
    {
        if (_player == null)
        {
            _bossSkillActive = false;
            yield break;
        }
        Vector3 locked = _player.position;
        locked.y = 0f;

        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            Vector3 flat = transform.position;
            flat.y = 0f;
            Vector3 toLocked = locked - flat;
            if (toLocked.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toLocked.normalized), Time.deltaTime * 6f);
            SetUpperArms(Quaternion.Euler(-50f * p, 0f, 0f));
            SetLowerArms(Quaternion.Euler(-30f * p, 0f, 0f));
            yield return null;
        }

        float speed = MoveSpeed * 3f;
        t = 0f;
        bool hit = false;
        while (t < 0.9f)
        {
            t += Time.deltaTime;
            Vector3 toLocked = locked - new Vector3(transform.position.x, 0f, transform.position.z);
            if (toLocked.magnitude < 0.3f)
                break;
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(locked.x, transform.position.y, locked.z), speed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(toLocked.normalized);
            SetUpperArms(Quaternion.Euler(70f, 0f, 0f));
            SetLowerArms(Quaternion.Euler(45f, 0f, 0f));
            if (!hit && _player != null && Vector3.Distance(transform.position, _player.position) <= 1.8f)
            {
                hit = true;
                _playerController?.TakeDamage(12);
                SoundManager.Instance?.Play("bonk", 0.8f);
            }
            yield return null;
        }

        t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float p = t / 0.8f;
            SetUpperArms(Quaternion.Lerp(Quaternion.Euler(70f, 0f, 0f), Quaternion.identity, p));
            SetLowerArms(Quaternion.Lerp(Quaternion.Euler(45f, 0f, 0f), Quaternion.identity, p));
            yield return null;
        }

        ResetJoints();
        ResetBossArms();
        _bossSkillActive = false;
        _bossSkillTimer = _enraged ? 7f : 8f;
    }

    private void SpawnSlamCracks()
    {
        for (int i = 0; i < 5; i++)
        {
            var crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crack.name = "SlamCrack";
            Object.Destroy(crack.GetComponent<Collider>());
            float a = (i / 5f) * Mathf.PI * 2f + Random.Range(0f, 0.5f);
            float dist = Random.Range(1.2f, 3.2f);
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist);
            pos.y = transform.position.y + 0.02f;
            crack.transform.position = pos;
            crack.transform.localScale = new Vector3(Random.Range(0.5f, 0.9f), 0.04f, Random.Range(0.4f, 0.7f));
            var r = crack.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.1f, 0.05f, 0.05f);
            _activeProjectiles.Add(crack);
            StartCoroutine(DestroyAfter(crack, 2f));
        }
    }

    private IEnumerator DestroyAfter(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null)
        {
            _activeProjectiles.Remove(go);
            Destroy(go);
        }
    }

    private void DestroyActiveProjectiles()
    {
        foreach (var go in _activeProjectiles)
        {
            if (go != null)
                Destroy(go);
        }
        _activeProjectiles.Clear();
    }

    private void Die()
    {
        _isDead = true;
        GameStats.AddEnemy();
        if (IsBoss)
        {
            QuestManager.Instance?.AddProgress("boss_kill", 1);
            BossFightActive = false;
            GameManager.Instance?.UIManager?.HideBossBar();
            if (QuestManager.Instance != null && !QuestManager.Instance.IsComplete("mansion_secret"))
                GameManager.Instance?.RequestDemonEnding();
            if (_bossSkillRoutine != null)
            {
                StopCoroutine(_bossSkillRoutine);
                _bossSkillRoutine = null;
            }
            _bossSkillActive = false;
            DestroyActiveProjectiles();
            ExplodeModel();
            DropMaterials(true);
            var bossCol = GetComponent<Collider>();
            if (bossCol != null) bossCol.enabled = false;
            Destroy(gameObject, 3f);
            return;
        }

        _respawnTimer = 5f;
        QuestManager.Instance?.AddProgress("enemies", 1);
        ExplodeModel();
        DropMaterials(false);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    private void DropMaterials(bool boss)
    {
        var wb = WorldBuilder.Instance;
        if (wb == null)
            return;
        Vector3 pos = transform.position + Vector3.up * 0.8f;
        if (boss)
        {
            for (int i = 0; i < 3; i++)
                wb.ThrowPickup("demon_horn", pos, RandomDropVelocity(i));
            for (int i = 0; i < 2; i++)
                wb.ThrowPickup("dark_essence", pos, RandomDropVelocity(i));
            return;
        }
        if (IsGiant)
        {
            wb.ThrowPickup("bone", pos, RandomDropVelocity(0));
            wb.ThrowPickup("dark_essence", pos, RandomDropVelocity(1));
            return;
        }
        if (Random.value < 0.5f)
            wb.ThrowPickup("bone", pos, RandomDropVelocity(0));
        else
            wb.ThrowPickup("dark_essence", pos, RandomDropVelocity(0));
    }

    private Vector3 RandomDropVelocity(int index)
    {
        float angle = (index * 1.5f + Random.Range(0f, 0.6f)) * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle) * Random.Range(2f, 3.5f), 5f, Mathf.Sin(angle) * Random.Range(2f, 3.5f));
    }

    private void ExplodeModel()
    {
        if (_modelRoot == null) return;

        Vector3 center = transform.position + Vector3.up * 0.9f;
        var renderers = _modelRoot.GetComponentsInChildren<Renderer>();
        var debris = new List<GameObject>();

        foreach (var r in renderers)
        {
            var block = r.gameObject;
            Vector3 worldPos = block.transform.position;
            Quaternion worldRot = block.transform.rotation;

            block.transform.SetParent(null);
            block.transform.position = worldPos;
            block.transform.rotation = worldRot;

            block.AddComponent<BoxCollider>();
            var rb = block.AddComponent<Rigidbody>();
            rb.mass = 0.3f;

            Vector3 dir = (worldPos - center).normalized;
            dir.y += 0.5f;
            rb.AddForce(dir * 8f + Vector3.up * 6f, ForceMode.Impulse);
            rb.AddTorque(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f), ForceMode.Impulse);

            debris.Add(block);
        }

        _modelRoot = null;
        StartCoroutine(DestroyDebris(debris, 5f));
    }

    private IEnumerator DestroyDebris(List<GameObject> debris, float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (var go in debris)
        {
            if (go != null) Destroy(go);
        }
    }

    private void Respawn()
    {
        _health = MaxHealth;
        _isDead = false;
        Vector3 pos = _origin;
        var wb = WorldBuilder.Instance;
        if (wb != null)
        {
            Vector3 center = wb.PagodaPosition;
            center.y = 0f;
            Vector3 flat = pos;
            flat.y = 0f;
            if (Vector3.Distance(flat, center) < SacredZoneRadius)
            {
                Vector3 away = flat - center;
                if (away.sqrMagnitude < 0.0001f)
                    away = Vector3.forward;
                pos = center + away.normalized * (SacredZoneRadius + 1.5f);
            }
        }
        pos += Random.insideUnitSphere * 1.5f;
        pos.y = 0f;
        transform.position = pos;
        BuildModel();
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    private Vector3 GetRandomPatrolPoint()
    {
        Vector3 point = _origin;
        var wb = WorldBuilder.Instance;
        Vector3 center = wb != null ? wb.PagodaPosition : Vector3.zero;
        center.y = 0f;
        int attempts = 0;
        while (attempts < 5)
        {
            point = _origin + Random.insideUnitSphere * PatrolRange;
            point.y = 0f;
            if (Vector3.Distance(point, center) >= SacredZoneRadius)
                break;
            attempts++;
        }
        return point;
    }
}
