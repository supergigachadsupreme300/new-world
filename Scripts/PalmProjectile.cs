using UnityEngine;

public class PalmProjectile : MonoBehaviour
{
    private float _lifetime = 3f;

    private void Update()
    {
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f)
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<PlayerController>() != null)
            return;

        var enemy = collision.collider.GetComponentInParent<EnemyController>();
        if (enemy == null)
            enemy = collision.collider.GetComponent<EnemyController>();

        if (enemy != null)
            enemy.TakeDamage(Mathf.Max(50, enemy.MaxHealth));

        Destroy(gameObject);
    }
}
