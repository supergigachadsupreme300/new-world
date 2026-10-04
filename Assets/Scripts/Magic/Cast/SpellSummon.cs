using UnityEngine;

/// <summary>
/// Summoned object (SpellDelivery.Summon): a persistent construct at the goal point that acts
/// until its lifetime ends. Damage summons become turrets that fire small bolts at the nearest
/// enemy inside their radius each tick (reusing the shared projectile flight); healing summons
/// become a persistent heal aura for allies inside the radius.
/// </summary>
public class SpellSummon : MonoBehaviour
{
    public float Radius = 6f;
    public float Lifetime = 6f;
    public float TickInterval = 0.5f;
    public float BoltPowerMultiplier = 0.6f;

    private SpellCaster _caster;
    private SpellData _spell;
    private float _power;
    private Transform _casterRoot;
    private float _age;
    private float _tick;
    private Transform _head;
    private Color _color;
    private Vector3 _headBaseScale;
    private readonly Collider[] _hitBuffer = new Collider[32];
    private readonly RaycastHit[] _groundBuffer = new RaycastHit[8];

    /// <summary>1ir: this summon belongs to the caster — it is created at the caster and follows it
    /// for its whole life, drawing a ground circle. Read from the spell, not passed in, so the three
    /// coupled behaviours (spawn here / follow / circle) cannot be half-enabled.</summary>
    private bool _follow;

    /// <summary>1is: this familiar sprays FORWARD along the caster's aim instead of picking the
    /// nearest enemy in its radius. Deliberately a SEPARATE flag from <see cref="_follow"/>: sharing
    /// one would silently give any future caster-anchored turret "fire regardless of targets" for
    /// free, which is a balance change nobody would be looking for. One flag, one meaning.</summary>
    private bool _sprayForward;

    /// <summary>1is: how far BEHIND the caster the circle sits, in metres. Applied at spawn
    /// (SpellCaster.ResolveSummon) and again every follow frame, from the same constant, or the
    /// familiar would visibly jump forward on its first tick.
    /// <para>Flattened against Y at the call site: an aim pointed at the ground must not bury the
    /// circle, and one pointed at the sky must not launch it.</para></summary>
    public const float BackOffset = 1.8f;

    /// <summary>1is: constant upward lead on a forward-sprayed bolt. The old nearest-target path
    /// computed its rise from the height DIFFERENCE to a target; with no target there is nothing to
    /// differ from, so this is a fixed nudge that clears the lip of ground the circle sits on.</summary>
    public const float ForwardSprayRise = 0.18f;

    /// <summary>1is: cached camera for the shared aim derivation — this runs every frame the familiar
    /// is alive, and Camera.main is a tag lookup (same reason SpellBeam caches one).</summary>
    private Camera _mainCam;

    /// <summary>1ir: ground probe for the follow. Starts high enough to clear the caster's own
    /// capsule but must SKIP the caster's colliders outright — a ray started above a standing player
    /// hits their capsule top before the terrain, which would park the circle at chest height.</summary>
    private const float GroundProbeUp = 4f;
    private const float GroundProbeDown = 40f;

    /// <summary>1ie: the summon's resolved look, cached at Initialize.</summary>
    private SpellLook _look;

    /// <summary>Configure the summon. <paramref name="durationMult"/> scales the lifetime only
    /// (1ir: the caster's DurationScale ladder); radius comes from <paramref name="radiusMult"/>.</summary>
    public void Initialize(SpellCaster caster, SpellData spell, float power,
        float radiusMult = 1f, float durationMult = 1f)
    {
        _caster = caster;
        _spell = spell;
        _power = power;
        _casterRoot = caster != null ? caster.transform.root : null;
        radiusMult = Mathf.Max(radiusMult, 0.01f);
        durationMult = Mathf.Max(durationMult, 0.01f);
        // 1ie: resolved OUTSIDE the spell != null guard — BuildVisual and the RingFlash below both
        // read _look (Scale, and the impact style it hands to SpellImpactFx). Assigned only inside
        // the guard, a spell-less summon drew at default(SpellLook).Scale == 0 and, worse, asked
        // SpellImpactFx for the Inherit style, which Spawn returns on — no flash at all. Same
        // fallback Zone/Beam/Tornado seed.
        _look = spell != null ? SpellLook.Resolve(spell) : SpellLook.Resolve(DamageType.Arcane, ProjectileShape.Auto);
        if (spell != null)
        {
            Radius = Mathf.Max(spell.Radius * radiusMult, 1f);
            // 1is: for a FORWARD-SPRAYING familiar, Radius is display-only. The spray has no target
            // gate at all, so this number no longer bounds what the spell can reach — it only sizes
            // the drawn circle. Stated here because "radius 5" reads like a range, and the next
            // reader would otherwise assume the spell still stops at 5 m. (For every other summon it
            // is still the live targeting reach read by NearestEnemy.)
            // 1ir: durationMult applies to CASTER-ANCHORED summons only, and this is the line that
            // says so. SpellCaster.DurationScale hands it to every summon, so scaling it here would
            // have quietly lengthened Ember Effigy (6s -> 7.2s at full charge) — an unrequested
            // rebalance of a shipped spell, invisible in the diff of the new one. The charge ladder
            // was bought for the spell that has a stream to lengthen, not for every turret.
            if (spell.Duration > 0f) Lifetime = Mathf.Max(spell.Duration * (_follow ? durationMult : 1f), 0.1f);
            if (spell.TickInterval > 0f) TickInterval = spell.TickInterval;
            _color = _look.Core;
            _follow = spell.CasterAnchored;
            _sprayForward = spell.SummonFiresForward;
        }

        BuildVisual();
        SkillFx.RingFlash(transform.position, Vector3.up, _color, Radius * 0.8f, 0.45f, _look.Scale);
    }

    private void Update()
    {
        if (_spell == null)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        if (_age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_head != null)
            _head.localScale = _headBaseScale * (1f + 0.18f * Mathf.Sin(Time.time * 3.2f));

        // 1ir: follow BEFORE the tick, so this frame's bolts are aimed from where the circle now is.
        if (_follow && _casterRoot != null) FollowCaster();

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval > 0f ? TickInterval : 0.5f;
            Tick();
        }
    }

    /// <summary>1ir/1is: keep the circle BEHIND the caster, snapped to the ground so it does not hang in
    /// the air on a slope or float when they jump. Skips the caster's own colliders — see
    /// GroundProbeUp.
    /// <para>1is: the offset is applied BEFORE the probe, not after. Probing at the caster and then
    /// moving the result 1.8 m back would keep the circle at the player's own floor height while it
    /// sits behind a step or the lip of a slope — the exact "circles at your feet, not where it is"
    /// read that offsetting was supposed to fix.</para>
    /// <para>Takes the NEAREST hit, not the first. <c>RaycastNonAlloc</c> returns hits in an
    /// unspecified order, so walking past a building would sometimes park the circle on its roof and
    /// sometimes on the street, depending on buffer layout. A downward ray's nearest hit is the
    /// lowest <c>point.y</c>, which is the ground under the circle.</para>
    /// <para><b>No wall avoidance</b> — the circle tracks the caster's XZ outright, so it can pass
    /// through a wall with them. Stated here because "follows you" reads like it stops at
    /// obstacles. Making it a bound spirit that clips is the cheaper, more readable behaviour than a
    /// sliding solver, and this is the honest description of what ships.</para></summary>
    private void FollowCaster()
    {
        // Same aim derivation the beam uses, so the circle always sits behind the line it sprays
        // along — one fact, one place (SpellCaster.CurrentAimDirection).
        if (_mainCam == null) _mainCam = Camera.main;
        Vector3 aim = SpellCaster.CurrentAimDirection(_casterRoot.position, BackOffset * 3f,
            _casterRoot.forward, _mainCam);
        Vector3 back = new Vector3(aim.x, 0f, aim.z);
        if (back.sqrMagnitude > 0.0001f)
            back = back.normalized * -BackOffset;

        Vector3 p = _casterRoot.position + back;
        int n = Physics.RaycastNonAlloc(p + Vector3.up * GroundProbeUp, Vector3.down,
            _groundBuffer, GroundProbeDown);
        float best = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = _groundBuffer[i];
            if (h.collider == null) continue;
            if (h.collider.transform.root == _casterRoot) continue;
            if (h.point.y >= best) continue;
            best = h.point.y;
        }
        // No ground found (mid-air, or over a gap the probe missed): keep the caster's own Y rather
        // than snapping to the last frame's ground or to 0.
        if (!float.IsPositiveInfinity(best)) p.y = best + 0.02f;
        transform.position = p;
    }

    private void Tick()
    {
        if (_caster == null || _spell == null) return;

        if (_spell.Heals)
        {
            AuraHeal();
            return;
        }

        // 1is: three disjoint kinds of summon, chosen by two independent flags. A forward-spraying
        // familiar has no target concept at all; every other damage summon is still the 1ir turret.
        if (_sprayForward) FireForward();
        else FireAtNearest();
    }

    private void AuraHeal()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _hitBuffer);
        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == _casterRoot) continue;
            if (!root.TryGetComponent<IHealable>(out _)) continue;
            _caster.ResolveHeal(_spell, _power, col.gameObject);
        }
    }

    /// <summary>1is: spray straight forward along the caster's aim, unconditionally — no target test.
    /// This is what makes the familiar a stream rather than a turret, and it is why
    /// <see cref="Radius"/> no longer bounds anything for this spell (see Initialize).
    /// <para>The muzzle is the circle's own orb, so the bolts visibly leave the familiar.</para></summary>
    private void FireForward()
    {
        if (_mainCam == null) _mainCam = Camera.main;
        Vector3 from = _head != null ? _head.position : transform.position + Vector3.up * 1.6f;
        Vector3 aim = SpellCaster.CurrentAimDirection(from, Mathf.Max(_spell.Range, 5f),
            _spell != null && _casterRoot != null ? _casterRoot.forward : Vector3.forward, _mainCam);
        Vector3 flat = new Vector3(aim.x, 0f, aim.z);
        Vector3 dir = flat.sqrMagnitude > 0.0001f
            ? (flat.normalized + Vector3.up * ForwardSprayRise).normalized
            : aim;
        SpawnBolt(from, dir);
    }

    private void FireAtNearest()
    {
        Transform target = NearestEnemy();
        if (target == null) return;

        Vector3 muzzle = _head != null ? _head.position : transform.position + Vector3.up * 1.6f;
        Vector3 to = target.position - muzzle;
        float h = Mathf.Max(0f, muzzle.y - target.position.y);
        Vector3 flat = to;
        flat.y = 0f;
        Vector3 dir = flat.sqrMagnitude > 0.0001f
            ? (flat.normalized + Vector3.up * Mathf.Clamp(h * 0.35f, 0f, 0.8f)).normalized
            : Vector3.up;

        SpawnBolt(muzzle, dir);
    }

    /// <summary>1is: the one place a summon bolt is created, shared by the forward spray and the
    /// nearest-target turret. Splitting it out is deliberate — the two paths differ only in how they
    /// choose a direction, and a copy of this block would let the bolt's power, size or launch speed
    /// drift between a turret and a familiar without anything failing.</summary>
    private void SpawnBolt(Vector3 muzzle, Vector3 dir)
    {
        var go = new GameObject("SummonBolt");
        go.transform.position = muzzle;
        go.transform.rotation = Quaternion.LookRotation(dir);
        _caster.DecorateProjectile(go, _spell);
        float speed = _spell.ProjectileSpeed > 0f ? _spell.ProjectileSpeed : 18f;
        // The trailing 1f is the projectile's radiusMult and is deliberately NOT the summon's size
        // scale: a familiar grows when you charge it, but its bolts keep their authored burst so the
        // detonation stays smaller than the area the familiar scans. Passing sizeScale here would
        // also widen every existing summon-turret bolt (Ember Effigy), which is a balance change
        // outside 1ir. SpellEffect.SplashRadius reads this same field, so the two agree by default.
        var fx = go.AddComponent<SpellEffect>().Initialize(_spell, _power * BoltPowerMultiplier, dir, _caster, 1f);
        fx.Launch(speed);
    }

    /// <summary>Nearest hostable enemy root inside the radius (EnemyController / BossController).</summary>
    private Transform NearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _hitBuffer);
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (root == _casterRoot) continue;
            if (root.CompareTag("Player") || root.CompareTag("Companion")) continue;
            if (!root.TryGetComponent<EnemyController>(out _) &&
                !root.TryGetComponent<BossController>(out _)) continue;

            float dSqr = (root.position - transform.position).sqrMagnitude;
            if (dSqr < bestSqr)
            {
                bestSqr = dSqr;
                best = root;
            }
        }
        return best;
    }

    /// <summary>Totem: base disc + tapered pillar + pulsing head crystal + orbiting shards.</summary>
    private void BuildVisual()
    {
        if (_follow) { BuildCircleVisual(); return; }
        BuildTotemVisual();
    }

    /// <summary>1ir: the following-familiar read — a flat ground circle the size of the real
    /// targeting radius, plus a low orb to fire from. Deliberately NOT SkillFx.RingFlash, which
    /// self-destructs and would give a one-frame flash instead of a persistent circle, and NOT the
    /// totem below: a pillar-and-shards totem that walks behind you reads as a carried statue, not
    /// as an area you are standing in. Radius is the live targeting value, so the drawn circle is
    /// exactly the area FireAtNearest/NearestEnemy actually scan.</summary>
    private void BuildCircleVisual()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "FollowCircle";
        DestroyCollider(disc.transform);
        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        disc.transform.localScale = new Vector3(Radius * 2f, 0.05f, Radius * 2f);
        SetMaterial(disc.transform, shader, _color);

        _head = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        _head.name = "FollowCore";
        DestroyCollider(_head);
        _head.SetParent(transform, false);
        _head.localPosition = new Vector3(0f, 0.9f, 0f);
        _head.localScale = Vector3.one * 0.6f;
        SetMaterial(_head, shader, _color);
        _headBaseScale = _head.localScale;
    }

    private void BuildTotemVisual()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) return;

        var baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseDisc.name = "SummonBase";
        DestroyCollider(baseDisc.transform);
        baseDisc.transform.SetParent(transform, false);
        baseDisc.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        baseDisc.transform.localScale = new Vector3(0.9f, 0.07f, 0.9f);
        SetMaterial(baseDisc.transform, shader, _color);

        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "SummonPillar";
        DestroyCollider(pillar.transform);
        pillar.transform.SetParent(transform, false);
        pillar.transform.localPosition = new Vector3(0f, 1f, 0f);
        pillar.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
        SetMaterial(pillar.transform, shader, _color);

        _head = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        _head.name = "SummonHead";
        DestroyCollider(_head);
        _head.SetParent(transform, false);
        _head.localPosition = new Vector3(0f, 2.1f, 0f);
        _head.localScale = Vector3.one * 0.5f;
        SetMaterial(_head, shader, _color);
        _headBaseScale = _head.localScale;

        for (int i = 0; i < 3; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "SummonOrbit_" + i;
            DestroyCollider(shard.transform);
            shard.transform.SetParent(transform, false);
            float ang = i * 120f;
            Vector2 c = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            shard.transform.localPosition = new Vector3(c.x * 0.75f, 1.2f, c.y * 0.75f);
            shard.transform.localScale = Vector3.one * 0.18f;
            SetMaterial(shard.transform, shader, _color);
        }
    }

    private static void DestroyCollider(Transform t)
    {
        Collider col = t.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    private static void SetMaterial(Transform t, Shader shader, Color color)
    {
        var r = t.GetComponent<MeshRenderer>();
        if (r != null) r.material = new Material(shader) { color = color };
    }
}