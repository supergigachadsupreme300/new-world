using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent control zone (class skills §3.2.1): slows (and optionally stuns) enemies inside.
/// Re-applies a short slow/stun window on every tick so the effect reads continuously while an
/// enemy stands inside and expires naturally shortly after it leaves. Prefab-free (procedural
/// ring visual mirroring <see cref="SkillFx.RingFlash"/>).
/// </summary>
public class CCZone : MonoBehaviour
{
    private float _radius = 4f;
    private float _duration = 5f;
    private float _slowFactor = 0.5f;
    private bool _stun;
    private GameObject _owner;
    private float _age;

    private readonly List<EnemyController> _affected = new List<EnemyController>();
    private static readonly Collider[] _scanBuffer = new Collider[32];

    /// <summary>Spawn a control zone at the given position.</summary>
    public static CCZone Spawn(Vector3 pos, float radius, float duration, float slowFactor, bool stun, GameObject owner)
    {
        var go = new GameObject("CCZone");
        go.transform.position = pos;
        var zone = go.AddComponent<CCZone>();
        zone.Init(radius, duration, slowFactor, stun, owner);
        return zone;
    }

    public void Init(float radius, float duration, float slowFactor, bool stun, GameObject owner)
    {
        _radius = Mathf.Max(radius, 0.5f);
        _duration = Mathf.Max(duration, 0.5f);
        _slowFactor = Mathf.Clamp01(slowFactor);
        _stun = stun;
        _owner = owner;
        BuildVisual();
    }

    private void BuildVisual()
    {
        // 1jd: the ring moved to CcZoneFxModelBuilder (Models/Magic). The pale blue stays HERE — it
        // marks a crowd-control zone rather than a school, so it is deliberately not a SpellLook
        // colour (rule 13) and the builder takes it as an argument rather than choosing one.
        CcZoneFxModelBuilder.BuildCcZoneRing(transform, _radius, new Color(0.38f, 0.75f, 0.96f, 0.4f));
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= _duration)
        {
            // Release any enemy still under our effect.
            foreach (var e in _affected)
                if (e != null && !_stun) e.RestoreSlow();
            _affected.Clear();
            Destroy(gameObject);
            return;
        }

        _affected.Clear();
        int n = Physics.OverlapSphereNonAlloc(transform.position, _radius, _scanBuffer);
        for (int i = 0; i < n; i++)
        {
            Collider col = _scanBuffer[i];
            if (col == null || col.transform == null) continue;
            if (_owner != null && col.transform.root == _owner.transform.root) continue;
            if (col.TryGetComponent<EnemyController>(out var enemy))
            {
                _affected.Add(enemy);
                if (_stun)
                    enemy.ApplyStun(0.25f);
                else if (_slowFactor < 1f)
                    enemy.ApplySlow(_slowFactor, 0.25f);
            }
        }
    }
}