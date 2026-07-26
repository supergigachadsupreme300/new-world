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

        float distance = Vector3.Distance(transform.position, _player.position);

        _isAttacking = false;
        if (distance <= ChaseRange)
        {
            if (distance <= AttackRange)
            {
                _isAttacking = true;
                Attack();
            }
            else
            {
                FollowPlayer();
            }
        }
        else
        {
            Patrol();
        }

        AnimateModel();
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
        transform.position = Vector3.MoveTowards(transform.position, _player.position, MoveSpeed * Time.deltaTime);
        transform.LookAt(_player);
        _isMoving = true;
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
        transform.position = _origin + Random.insideUnitSphere * 1.5f;
        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        BuildModel();
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    private Vector3 GetRandomPatrolPoint()
    {
        Vector3 point = _origin + Random.insideUnitSphere * PatrolRange;
        point.y = 0f;
        return point;
    }
}
