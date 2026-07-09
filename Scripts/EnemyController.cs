using UnityEngine;

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

    private int _health;
    private Transform _player;
    private Vector3 _origin;
    private Vector3 _patrolTarget;
    private float _attackTimer;
    private bool _isDead;

    private void Awake()
    {
        _health = MaxHealth;
        _origin = transform.position;
        _patrolTarget = GetRandomPatrolPoint();
        _player = Object.FindAnyObjectByType<PlayerController>()?.transform;
    }

    private void Update()
    {
        if (_isDead || _player == null)
            return;

        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance <= ChaseRange)
        {
            if (distance <= AttackRange)
            {
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
    }

    private void FollowPlayer()
    {
        transform.position = Vector3.MoveTowards(transform.position, _player.position, MoveSpeed * Time.deltaTime);
        transform.LookAt(_player);
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
        QuestManager.Instance?.AddProgress("enemies", 1);
        gameObject.SetActive(false);
        Invoke(nameof(Respawn), 5f);
    }

    private void Respawn()
    {
        _health = MaxHealth;
        _isDead = false;
        transform.position = _origin + Random.insideUnitSphere * 1.5f;
        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        gameObject.SetActive(true);
    }

    private Vector3 GetRandomPatrolPoint()
    {
        Vector3 point = _origin + Random.insideUnitSphere * PatrolRange;
        point.y = 0f;
        return point;
    }
}
