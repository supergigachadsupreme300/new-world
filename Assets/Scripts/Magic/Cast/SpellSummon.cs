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
    /// <summary>1ir: ground probe for the follow. 1jt: the constants and the probe itself moved to
    /// <see cref="SpellCaster.GroundUnder"/> — the casting halo needs byte-identical behaviour, and
    /// two private raycasts is rule 8's second-spelling rot (see that method's remarks). This buffer
    /// stays here because the buffer is per-caller mutable state.</summary>
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
    /// circle, and one pointed at the sky must not launch it.</para>
    /// <para><b>1jt: this is an alias of <see cref="SpellLook.CastFrontOffset"/>, not a second
    /// spelling of the same number.</b> The front casting halo and this rear familiar are meant to sit
    /// symmetrically about the player, so they are one value by construction: changing one without the
    /// other would silently break the symmetry, and nothing else in the game would notice.</para></summary>
    public const float BackOffset = SpellLook.CastFrontOffset;

    /// <summary>1is: constant upward lead on a forward-sprayed bolt. The old nearest-target path
    /// computed its rise from the height DIFFERENCE to a target; with no target there is nothing to
    /// differ from, so this is a fixed nudge that clears the lip of ground the circle sits on.</summary>
    public const float ForwardSprayRise = 0.18f;

    /// <summary>1is: cached camera for the shared aim derivation — this runs every frame the familiar
    /// is alive, and Camera.main is a tag lookup (same reason SpellBeam caches one).</summary>
    private Camera _mainCam;

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
    /// <see cref="SpellCaster.GroundProbeUp"/>.
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
        // 1jt: the probe itself is SpellCaster's now, so the halo under/in front of the player and
        // this circle behind them can never disagree about what "the ground" is.
        transform.position = SpellCaster.GroundUnder(p, _casterRoot, _groundBuffer);
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

    /// <summary>Builds the construct's body: the totem (base disc + tapered pillar + pulsing head
    /// crystal + orbiting shards), or the familiar's ground circle when the summon is caster-anchored.
    /// <para>1jd: the geometry now lives in <see cref="SummonModelBuilder"/> (under
    /// <c>Models/Magic/</c>), so "summon model" is findable by name and by folder. The shapes, the
    /// names and the pulse are unchanged; the builder returns the head and the scale it pulses
    /// around as ONE record, so the two cannot drift apart at this boundary.</para></summary>
    private void BuildVisual()
    {
        SummonModelBuilder.Body body = _follow
            ? SummonModelBuilder.BuildFamiliarCircle(transform, Radius, _color)
            : SummonModelBuilder.BuildTotem(transform, _color);
        _head = body.Head;
        _headBaseScale = body.HeadBaseScale;
    }
}