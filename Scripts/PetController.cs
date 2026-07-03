using UnityEngine;

public class PetController : MonoBehaviour
{
    public float FollowSpeed = 2.5f;
    public float FollowDistance = 2.2f;
    public float AttackRange = 4f;
    public int Damage = 8;
    public float AttackCooldown = 1.2f;

    private Transform _player;
    private float _attackTimer;

    private void Awake()
    {
        _player = FindObjectOfType<PlayerController>()?.transform;
    }

    private void Update()
    {
        if (_player == null)
            return;

        Vector3 targetPos = _player.position - _player.forward * FollowDistance;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, FollowSpeed * Time.deltaTime);

        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance <= AttackRange)
        {
            _attackTimer += Time.deltaTime;
            if (_attackTimer >= AttackCooldown)
            {
                _attackTimer = 0f;
                AttackNearby();
            }
        }
    }

    private void AttackNearby()
    {
        var enemies = FindObjectsOfType<EnemyController>();
        foreach (var enemy in enemies)
        {
            if (enemy == null)
                continue;
            if (Vector3.Distance(transform.position, enemy.transform.position) <= AttackRange)
            {
                enemy.TakeDamage(Damage);
                Debug.Log("Pet attacked enemy");
            }
        }
    }
}
