using UnityEngine;

public class PetController : MonoBehaviour
{
    public float FollowSpeed = 2.5f;
    public float FollowDistance = 2.2f;
    public float LateralOffset = -1.1f;
    public float AttackRange = 4f;
    public int Damage = 8;
    public float AttackCooldown = 1.2f;

    private Transform _player;
    private float _attackTimer;
    private Transform _modelRoot;

    private void Awake()
    {
        _player = Object.FindAnyObjectByType<PlayerController>()?.transform;
        BuildModel();
        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = 0.45f;
        col.center = new Vector3(0f, 0.45f, 0f);

        var pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc != null && pc.GetComponent<CharacterController>() != null)
            Physics.IgnoreCollision(col, pc.GetComponent<CharacterController>());
    }

    private void BuildModel()
    {
        _modelRoot = new GameObject("PetModel").transform;
        _modelRoot.SetParent(transform, false);

        Color body = new Color(0.85f, 0.62f, 0.35f);
        Color belly = new Color(0.95f, 0.85f, 0.65f);
        Color dark = new Color(0.35f, 0.22f, 0.12f);

        MapBuilder.MakeBlock("PetBody", _modelRoot, new Vector3(0.36f, 0.32f, 0.55f), new Vector3(0f, 0.42f, 0f), body, true);
        MapBuilder.MakeBlock("PetChest", _modelRoot, new Vector3(0.3f, 0.26f, 0.2f), new Vector3(0f, 0.38f, 0.3f), belly, true);
        MapBuilder.MakeBlock("PetHead", _modelRoot, new Vector3(0.28f, 0.26f, 0.26f), new Vector3(0f, 0.68f, 0.28f), body, true);
        MapBuilder.MakeBlock("PetMuzzle", _modelRoot, new Vector3(0.16f, 0.1f, 0.12f), new Vector3(0f, 0.62f, 0.44f), belly, true);
        MapBuilder.MakeBlock("PetNose", _modelRoot, new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0f, 0.64f, 0.5f), dark, true);
        MapBuilder.MakeBlock("PetEarL", _modelRoot, new Vector3(0.08f, 0.18f, 0.06f), new Vector3(-0.16f, 0.84f, 0.26f), dark, true);
        MapBuilder.MakeBlock("PetEarR", _modelRoot, new Vector3(0.08f, 0.18f, 0.06f), new Vector3(0.16f, 0.84f, 0.26f), dark, true);
        MapBuilder.MakeBlock("PetEyeL", _modelRoot, new Vector3(0.05f, 0.05f, 0.03f), new Vector3(-0.1f, 0.72f, 0.38f), dark, true);
        MapBuilder.MakeBlock("PetEyeR", _modelRoot, new Vector3(0.05f, 0.05f, 0.03f), new Vector3(0.1f, 0.72f, 0.38f), dark, true);
        MapBuilder.MakeBlock("PetTail", _modelRoot, new Vector3(0.06f, 0.28f, 0.06f), new Vector3(0f, 0.5f, -0.32f), body, true);
        MapBuilder.MakeBlock("PetLegFL", _modelRoot, new Vector3(0.08f, 0.22f, 0.08f), new Vector3(-0.13f, 0.16f, 0.18f), body, true);
        MapBuilder.MakeBlock("PetLegFR", _modelRoot, new Vector3(0.08f, 0.22f, 0.08f), new Vector3(0.13f, 0.16f, 0.18f), body, true);
        MapBuilder.MakeBlock("PetLegBL", _modelRoot, new Vector3(0.08f, 0.22f, 0.08f), new Vector3(-0.13f, 0.16f, -0.18f), body, true);
        MapBuilder.MakeBlock("PetLegBR", _modelRoot, new Vector3(0.08f, 0.22f, 0.08f), new Vector3(0.13f, 0.16f, -0.18f), body, true);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return;

        if (_player == null)
            _player = Object.FindAnyObjectByType<PlayerController>()?.transform;
        if (_player == null)
            return;

        Vector3 targetPos = _player.position - _player.forward * FollowDistance + _player.right * LateralOffset;
        targetPos.y = transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, FollowSpeed * Time.deltaTime);

        if (_modelRoot != null && _player != null)
        {
            Vector3 lookDir = _player.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
                _modelRoot.rotation = Quaternion.LookRotation(lookDir.normalized);
        }

        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance <= AttackRange)
        {
            _attackTimer += Time.deltaTime;
            if (_attackTimer >= AttackCooldown)
            {
                _attackTimer = 0f;
                AttackNearest();
            }
        }
    }

    private void AttackNearest()
    {
        var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        EnemyController nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            if (d <= AttackRange && d < nearestDist)
            {
                nearest = enemy;
                nearestDist = d;
            }
        }

        if (nearest == null)
            return;

        Vector3 origin = transform.position + Vector3.up * 0.4f;
        Vector3 dir = (nearest.transform.position + Vector3.up * 0.5f) - origin;
        float dist = dir.magnitude;
        if (Physics.Raycast(origin, dir.normalized, out var hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.GetComponentInParent<EnemyController>() == nearest)
            {
                nearest.TakeDamage(Damage);
                Debug.Log("Pet attacked enemy");
            }
        }
    }
}
