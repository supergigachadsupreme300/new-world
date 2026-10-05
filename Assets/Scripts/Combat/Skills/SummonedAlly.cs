using UnityEngine;

/// <summary>
/// Procedural combat ally (class skills §3.2.1 — Necromancer skeletons, Alchemist golems).
/// A prefab-free minion: chases the nearest enemy within its range, attacks on cooldown, is
/// damaged by enemies (tagged "Companion", implements <see cref="IDamageable"/>), follows the
/// player when idle, and despawns after its lifetime. Movement mirrors EnemyController
/// (MoveTowards, no NavMesh dependency).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class SummonedAlly : MonoBehaviour, IDamageable
{
    private const float MoveSpeed = 4f;
    private const float AggroRange = 7f;
    private const float FollowDistance = 1.6f;
    private const float DespawnGrace = 0.5f;

    private int _maxHealth = 60;
    private int _health = 60;
    private float _damage = 12f;
    private float _attackRange = 1.4f;
    private float _attackCooldown = 1.1f;

    private Transform _owner;
    private float _lifetime;
    private float _age;
    private float _attackTimer;
    private Transform _target;
    private Renderer[] _renderers;
    private static readonly Collider[] _scanBuffer = new Collider[16];

    /// <summary>Ally tint. Pale blue with 0.9 alpha - the colour this summon has always spawned
    /// with, kept as a named constant so the bench lane and the model cannot drift apart.</summary>
    public static readonly Color AllyColor = new Color(0.72f, 0.82f, 0.95f, 0.9f);

    /// <summary>Spawn a summon at the owner's side.</summary>
    public static SummonedAlly Spawn(Transform owner, float power, float duration, float followRange)
    {
        // 1je: the root is a bare GameObject, not a primitive. The body is
        // SummonModelBuilder.BuildAlly - a levitating construct, because this component has no
        // animator and moves by MoveTowards, so a bipedal rig would slide. Colliders: the cube's own
        // collider used to be destroyed here and nothing replaced it, so the ally still has NO
        // collider at all. The class-level [RequireComponent(typeof(SphereCollider))] is added by
        // AddComponent and is never removed - a pre-existing contradiction, recorded in PROGRESS 1je
        // and left alone, because making the ally targetable is a gameplay change, not this task.
        var go = new GameObject("SummonedAlly");
        go.tag = "Companion";

        var ally = go.AddComponent<SummonedAlly>();
        ally._owner = owner;
        ally._lifetime = Mathf.Max(duration, 3f);
        ally._damage = Mathf.Max(4f, power);
        ally._maxHealth = Mathf.RoundToInt(50f + power * 3f);
        ally._health = ally._maxHealth;

        Vector3 side = owner != null ? owner.right * 1.2f : Vector3.right;
        go.transform.position = (owner != null ? owner.position : Vector3.zero) + side;

        SummonModelBuilder.AllyBody body = SummonModelBuilder.BuildAlly(go.transform, AllyColor);
        ally._renderers = body.Renderers;

        go.transform.localScale = Vector3.one * 0.8f;
        return ally;
    }

    private void Update()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        if (_age >= _lifetime + DespawnGrace)
        {
            // 1je: every part, not one. With a single cube body there was one renderer to fade; the
            // six-part body needs the whole set, or the head and pods stay opaque while the shell
            // vanishes. Alpha only - the trim/core colours are preserved relative to each other.
            SetAlpha(0f);
            Destroy(gameObject, 0.4f);
            return;
        }

        TickTarget();
        if (_target != null)
        {
            float d = Vector3.Distance(transform.position, _target.position);
            if (d > _attackRange)
            {
                MoveToward(_target.position, MoveSpeed);
                Face(_target.position);
            }
            else
            {
                Face(_target.position);
                _attackTimer += Time.deltaTime;
                if (_attackTimer >= _attackCooldown)
                {
                    _attackTimer = 0f;
                    Strike(_target);
                }
            }
        }
        else
        {
            Vector3 follow = _owner.position - _owner.forward * FollowDistance + _owner.right * 1.2f;
            if (Vector3.Distance(transform.position, follow) > 1.5f)
                MoveToward(follow, MoveSpeed);
        }
    }

    private void TickTarget()
    {
        _target = null;
        float bestD = float.MaxValue;
        int n = Physics.OverlapSphereNonAlloc(transform.position, AggroRange, _scanBuffer);
        for (int i = 0; i < n; i++)
        {
            Collider col = _scanBuffer[i];
            if (col == null || col.transform == null) continue;
            if (col.transform.root == _owner.root) continue;
            if (!col.TryGetComponent<EnemyController>(out _)) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < bestD) { bestD = d; _target = col.transform; }
        }
    }

    private void Strike(Transform target)
    {
        if (target.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(Mathf.RoundToInt(_damage));
            DamageNumber.Spawn(target.position, _damage, DamageType.Physical);
        }
        SkillFx.SlashFlash(transform.position, transform.forward, 1.2f, 0.12f, new Color(0.8f, 0.85f, 1f, 0.9f));
    }

    private void MoveToward(Vector3 dest, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, dest, speed * Time.deltaTime);
    }

    private void Face(Vector3 point)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 10f);
    }

    /// <summary>IDamageable — enemies deal damage to the summon through the standard route.</summary>
    public int TakeDamage(int amount)
    {
        if (_health <= 0) return 0;
        _health -= Mathf.Max(0, amount);
        if (_health <= 0)
        {
            _health = 0;
            // 1je: all six parts, for the same reason as the despawn above - disabling one renderer
            // on a multi-part body leaves the rest of it standing.
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null)
                        _renderers[i].enabled = false;
            }
            Destroy(gameObject, 0.2f);
        }
        return _health;
    }

    /// <summary>Set the same alpha on every part of the body.</summary>
    private void SetAlpha(float alpha)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            Color c = _renderers[i].material.color;
            c.a = alpha;
            _renderers[i].material.color = c;
        }
    }
}