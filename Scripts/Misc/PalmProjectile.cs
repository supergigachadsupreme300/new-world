using UnityEngine;

public class PalmProjectile : MonoBehaviour
{
    private float _lifetime = 3f;
    public int BossDamagePerHit = 15;

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.Player != null && gm.Player.GetComponent<Collider>() != null)
            Physics.IgnoreCollision(col, gm.Player.GetComponent<Collider>());

        if (GoblinPet.Instance != null && GoblinPet.Instance.GetComponent<Collider>() != null)
            Physics.IgnoreCollision(col, GoblinPet.Instance.GetComponent<Collider>());

        foreach (var pet in gm.Pets)
        {
            if (pet != null && pet.GetComponent<Collider>() != null)
                Physics.IgnoreCollision(col, pet.GetComponent<Collider>());
        }
    }

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
            enemy.TakeDamage(enemy.IsBoss ? BossDamagePerHit : Mathf.Max(50, enemy.MaxHealth));

        Destroy(gameObject);
    }
}
