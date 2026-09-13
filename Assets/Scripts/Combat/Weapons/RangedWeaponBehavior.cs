using System;
using UnityEngine;

/// <summary>
/// Ranged weapon behavior (§3.6 Layer 3). Fires projectiles (or raycasts), consumes
/// ammo (arrows/bolts) via IAmmoProvider, applies accuracy from Dexterity, and deals
/// damage from weapon.base (weapon ceiling — NOT stat-scaled, §3.4).
///
/// Attach to the weapon/root; assign Muzzle (fire origin) and optional Projectile prefab.
/// If no projectile prefab is set, a generated arrow projectile is built at runtime.
/// </summary>
public class RangedWeaponBehavior : MonoBehaviour, IWeaponBehavior
{
    [Header("Wiring")]
    public WeaponData Data;
    public Transform Muzzle;
    public GameObject ProjectilePrefab;

    [Header("Runtime")]
    public DamageType ShotType = DamageType.Physical;

    /// <summary>Ammo source; defaults to InfiniteAmmo until Inventory exists.</summary>
    public IAmmoProvider Ammo = InfiniteAmmo.Instance;

    /// <summary>Optional stat accessor supplying Dexterity for accuracy (wired Phase 4).</summary>
    public IStatProvider Stats;

    [Header("Projectile")]
    public float ProjectileSpeed = 30f;

    /// <summary>Flight time of a fully-uncharged shot (draw extends it).</summary>
    public float BaseLifetime = 4f;

    private bool _attacking;

    public event Action Completed;
    public bool IsAttacking => _attacking;

    public void BeginAttack(AttackCommand cmd)
    {
        if (_attacking) return;
        if (Data != null && Data.AmmoItemId != null && Ammo != null)
        {
            int count = Ammo.Count(Data.AmmoItemId);
            if (count == 0) return; // no ammo
        }

        _attacking = true;

        // Charge/draw scales damage, projectile speed and flight distance: a full draw
        // (~charge 0..1) roughly doubles damage and reach while the bolt flies ~1.5x faster.
        float charge = Mathf.Clamp01(cmd.ChargeLevel);

        // Damage comes from weapon.base (weapon ceiling, not stat-scaled).
        float damage = Data != null ? Data.BaseDamage : 10f;
        damage *= Mathf.Lerp(1f, 2.5f, charge);
        ShotType = Data != null ? Data.Type : ShotType;

        Vector3 origin = Muzzle != null ? Muzzle.position : cmd.Origin != null ? cmd.Origin.position : transform.position;
        Vector3 dir = cmd.Direction.sqrMagnitude > 0.0001f
            ? cmd.Direction.normalized
            : transform.forward;

        // Accuracy from Dexterity — spread angle decreases as accuracy rises.
        float accuracy = 1f;
        if (Stats != null && Data != null)
            accuracy = 1f + Stats.GetStat(WeaponScalingStat.Dexterity) * Data.AccuracyFromDex;
        Vector3 aimed = ApplySpread(dir, Mathf.Clamp01(1f / Mathf.Max(accuracy, 0.01f)));

        // Draw extends the flight envelope: speed rises and the projectile destroys later so a
        // full draw carries roughly 2.4x the base distance (speed x lifetime).
        float speed = ProjectileSpeed * Mathf.Lerp(1f, 1.5f, charge);
        float lifetime = BaseLifetime * Mathf.Lerp(1f, 2f, charge);
        float reach = Data != null ? Data.Reach * Mathf.Lerp(1f, 2f, charge) : 60f;

        // Consume ammo.
        if (Data != null && Data.AmmoItemId != null)
            Ammo?.Consume(Data.AmmoItemId);

        FireProjectile(damage, origin, aimed, cmd, speed, lifetime, reach);

        // Ranged attacks complete immediately (projectile carries the damage).
        _attacking = false;
        Completed?.Invoke();
    }

    public void ActiveFrame()
    {
    }

    public void Cancel()
    {
        _attacking = false;
    }

    private Vector3 ApplySpread(Vector3 dir, float spread)
    {
        if (spread <= 0f) return dir;
        return (dir + UnityEngine.Random.insideUnitSphere * spread * 0.15f).normalized;
    }

    private void FireProjectile(float damage, Vector3 origin, Vector3 dir, AttackCommand cmd, float speed, float lifetime, float reach)
    {
        if (ProjectilePrefab != null)
        {
            GameObject go = Instantiate(ProjectilePrefab, origin, Quaternion.LookRotation(dir));
            var proj = go.GetComponent<RangedProjectile>();
            if (proj != null)
            {
                proj.Lifetime = lifetime;
                proj.Launch(dir, speed, damage, ShotType, cmd.Origin);
            }
            else if (go.TryGetComponent<Rigidbody>(out var rb))
                rb.linearVelocity = dir * speed;
        }
        else
        {
            // No authored prefab: build a generated projectil at runtime so shots read as real
            // projectiles (not hit-scan) — an arrow for bows, a spinning hammer head for the
            // throwing hammer.
            Vector3 spawn = origin + dir * 0.3f;
            var go = BuildDefaultProjectileVisual();
            go.transform.position = spawn;
            go.transform.rotation = Quaternion.LookRotation(dir);
            var proj = go.AddComponent<RangedProjectile>();
            proj.Lifetime = lifetime;
            proj.Launch(dir, speed, damage, ShotType, cmd.Origin);
        }
    }

    /// <summary>Pick the runtime projectile visual for the equipped ranged weapon (no prefab case).</summary>
    private GameObject BuildDefaultProjectileVisual()
    {
        if (Data != null && Data.id == "throwing_hammer")
            return BuildHammerVisual();
        return BuildArrowVisual();
    }

    /// <summary>Build a generated arrow visual (used when no projectile prefab is assigned).
    /// Renderer-only: the root keeps no collider so <see cref="RangedProjectile"/>'s flight
    /// raycast never self-hits.</summary>
    private static GameObject BuildArrowVisual()
    {
        var go = new GameObject("ArrowProjectile");
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return go;
        var wood = new Color(0.45f, 0.32f, 0.18f, 1f);
        var steel = new Color(0.82f, 0.82f, 0.85f, 1f);

        // Shaft along +Z (cylinder defaults to the Y axis).
        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        DestroyCollider(shaft);
        shaft.transform.SetParent(go.transform, false);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        shaft.transform.localScale = new Vector3(0.03f, 0.25f, 0.03f);
        shaft.GetComponent<MeshRenderer>().material = new Material(shader) { color = wood };

        // Pointed head at the front.
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        DestroyCollider(head);
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        head.transform.localScale = new Vector3(0.1f, 0.1f, 0.12f);
        head.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

        // Two thin crossed fletching blades near the tail.
        var fletchA = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fletchA.name = "FletchA";
        DestroyCollider(fletchA);
        fletchA.transform.SetParent(go.transform, false);
        fletchA.transform.localPosition = new Vector3(0f, 0f, -0.24f);
        fletchA.transform.localScale = new Vector3(0.06f, 0.09f, 0.01f);
        fletchA.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

        var fletchB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fletchB.name = "FletchB";
        DestroyCollider(fletchB);
        fletchB.transform.SetParent(go.transform, false);
        fletchB.transform.localPosition = new Vector3(0f, 0f, -0.24f);
        fletchB.transform.localScale = new Vector3(0.01f, 0.06f, 0.09f);
        fletchB.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

        return go;
    }

    /// <summary>Generated spinning hammer-head projectile for the throwing hammer. Flies head-first
    /// along +Z (matching the projectile travel axis) and tumbles around that axis in flight.
    /// Renderer-only — no collider, so <see cref="RangedProjectile"/>'s raycast never self-hits.</summary>
    private static GameObject BuildHammerVisual()
    {
        var go = new GameObject("HammerProjectile");
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            var wood = new Color(0.45f, 0.32f, 0.18f, 1f);
            var steel = new Color(0.82f, 0.82f, 0.85f, 1f);
            var darkSteel = new Color(0.30f, 0.30f, 0.34f, 1f);

            // Handle along +Z (cylinder defaults to Y, rotate 90° about X).
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Handle";
            DestroyCollider(handle);
            handle.transform.SetParent(go.transform, false);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localPosition = new Vector3(0f, 0f, -0.28f);
            handle.transform.localScale = new Vector3(0.07f, 0.32f, 0.07f);
            handle.GetComponent<MeshRenderer>().material = new Material(shader) { color = wood };

            // Handle grip wrap.
            var wrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wrap.name = "Wrap";
            DestroyCollider(wrap);
            wrap.transform.SetParent(go.transform, false);
            wrap.transform.localPosition = new Vector3(0f, 0f, -0.44f);
            wrap.transform.localScale = new Vector3(0.09f, 0.09f, 0.16f);
            wrap.GetComponent<MeshRenderer>().material = new Material(shader) { color = darkSteel };

            // Hammer head at the front.
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            DestroyCollider(head);
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            head.transform.localScale = new Vector3(0.30f, 0.22f, 0.20f);
            head.GetComponent<MeshRenderer>().material = new Material(shader) { color = steel };

            // Head rim + striking spike.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "HeadRim";
            DestroyCollider(rim);
            rim.transform.SetParent(go.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0f, 0.32f);
            rim.transform.localScale = new Vector3(0.26f, 0.18f, 0.08f);
            rim.GetComponent<MeshRenderer>().material = new Material(shader) { color = darkSteel };

            var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spike.name = "Spike";
            DestroyCollider(spike);
            spike.transform.SetParent(go.transform, false);
            spike.transform.localPosition = new Vector3(0f, 0f, 0.36f);
            spike.transform.localScale = new Vector3(0.08f, 0.08f, 0.14f);
            spike.GetComponent<MeshRenderer>().material = new Material(shader) { color = darkSteel };
        }

        go.AddComponent<TumbleSpin>();
        return go;
    }

    private static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    /// <summary>Slow tumble around the travel axis so a thrown hammer reads as spinning mid-air.</summary>
    private sealed class TumbleSpin : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(360f * Time.deltaTime, 0f, 0f, Space.Self);
        }
    }
}
