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

    private int _health;
    private Transform _player;
    private Vector3 _origin;
    private Vector3 _patrolTarget;
    private float _attackTimer;
    private bool _isDead;
    private float _respawnTimer;

    private Transform _modelRoot;
    private Transform _upperTorso;
    private Transform _midTorso;
    private Transform _lowerTorso;
    private Transform _armL;
    private Transform _armR;
    private Transform _legL;
    private Transform _legR;
    private Transform _kneeL;
    private Transform _kneeR;
    private float _walkCycle;
    private float _bobTimer;
    private bool _isMoving;
    private bool _isAttacking;

    private GameObject _structureTarget;
    private float _stuckTimer;
    private float _structureAttackTimer;
    private float _structureCheckTimer;
    private Vector3 _lastChasePos;
    private bool _hasChasePos;
    private bool _knockOut;

    private const float STUCK_THRESHOLD = 4f;
    private const float STRUCTURE_ATTACK_COOLDOWN = 2f;
    private const float STRUCTURE_SEARCH_RANGE = 5f;
    private const float STUCK_MOVEMENT_FACTOR = 0.3f;

    private void Awake()
    {
        _health = MaxHealth;
        _origin = transform.position;
        _patrolTarget = GetRandomPatrolPoint();
        _player = Object.FindAnyObjectByType<PlayerController>()?.transform;
    }

    private void Start()
    {
        BuildModel();
    }

    private void BuildModel()
    {
        if (IsGiant)
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

        if (IsGiant)
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
        if (_player == null)
            return;
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPlayerDead) return;

        if (EnforceSacredZone()) return;

        float distance = Vector3.Distance(transform.position, _player.position);

        _isAttacking = false;
        _isMoving = false;

        if (_structureTarget != null)
        {
            HandleStructureAttack(distance);
        }
        else if (distance <= ChaseRange)
        {
            if (distance <= AttackRange)
            {
                _stuckTimer = 0f;
                _hasChasePos = false;
                _isAttacking = true;
                Attack();
            }
            else
            {
                FollowPlayer();
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
    }

    public void TakeDamage(int amount)
    {
        if (_isDead)
            return;

        _health -= amount;
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

    private void FollowPlayer()
    {
        Vector3 dir = (_player.position - transform.position).normalized;
        float step = MoveSpeed * Time.deltaTime;
        Vector3 origin = transform.position + Vector3.up * 0.9f;

        if (Physics.Raycast(origin, dir, out var hit, step + 0.5f))
        {
            if (IsDoorCollider(hit.collider))
            {
                transform.position = Vector3.MoveTowards(transform.position, _player.position, step);
                transform.LookAt(_player);
                _isMoving = true;
                return;
            }

            var wb = WorldBuilder.Instance;
            if (wb != null && wb.FindBuilding(hit.collider.gameObject) != null)
            {
                _isMoving = false;
                return;
            }
        }

        transform.position = Vector3.MoveTowards(transform.position, _player.position, step);
        transform.LookAt(_player);
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

    private void Attack()
    {
        _attackTimer += Time.deltaTime;
        if (_attackTimer < AttackCooldown)
            return;

        _attackTimer = 0f;
        var player = _player.GetComponent<PlayerController>();
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

        Collider[] hits = Physics.OverlapSphere(transform.position, STRUCTURE_SEARCH_RANGE);
        GameObject closest = null;
        float closestDist = float.MaxValue;

        foreach (var col in hits)
        {
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

    private void Die()
    {
        _isDead = true;
        _respawnTimer = 5f;
        QuestManager.Instance?.AddProgress("enemies", 1);
        ExplodeModel();
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
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
