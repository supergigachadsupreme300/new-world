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
    /// <summary>1l6p: every launch orb the body owns. The familiar carries the whole ring (each tick's
    /// bolt fires from a random one — see <see cref="Muzzle"/>); the totem carries just its head.
    /// Replaces the single head orb the flat circle used.</summary>
    private Transform[] _cores;
    private Color _color;
    private Vector3 _coreBaseScale;
    private readonly Collider[] _hitBuffer = new Collider[32];
    /// <summary>1ir: ground probe for the follow. 1jt: the constants and the probe itself moved to
    /// <see cref="SpellCaster.GroundUnder"/> — the casting halo needs byte-identical behaviour, and
    /// two private raycasts is rule 8's second-spelling rot (see that method's remarks). This buffer
    /// stays here because the buffer is per-caller mutable state.</summary>
    private readonly RaycastHit[] _groundBuffer = new RaycastHit[8];

    /// <summary>1ir: this summon belongs to the caster — it is created at the caster and follows it
    /// for its whole life, drawing a ring behind it (1l6p: a vertical halo, no longer a ground
    /// circle). Read from the spell, not passed in, so the three coupled behaviours (spawn here /
    /// follow / drawn ring) cannot be half-enabled.</summary>
    private bool _follow;

    /// <summary>1is: this familiar sprays FORWARD along the caster's aim instead of picking the
    /// nearest enemy in its radius. Deliberately a SEPARATE flag from <see cref="_follow"/>: sharing
    /// one would silently give any future caster-anchored turret "fire regardless of targets" for
    /// free, which is a balance change nobody would be looking for. One flag, one meaning.</summary>
    private bool _sprayForward;

    /// <summary>1is: how far BEHIND the caster the ring sits, in metres. Applied by
    /// <see cref="FollowCaster"/> — the ONE placement, run at birth (from Initialize) and every follow
    /// frame — so spawn and follow cannot drift (1l6r). ResolveSummon no longer applies it.
    /// <para>Flattened against Y inside <see cref="FollowCaster"/>: an aim pointed at the ground must
    /// not bury the ring, and one pointed at the sky must not launch it.</para>
    /// <para><b>1jt: this is an alias of <see cref="SpellLook.CastFrontOffset"/>, not a second
    /// spelling of the same number.</b> The front casting halo and this rear familiar are meant to sit
    /// symmetrically about the player, so they are one value by construction: changing one without the
    /// other would silently break the symmetry, and nothing else in the game would notice.</para></summary>
    public const float BackOffset = SpellLook.CastFrontOffset;

    /// <summary>1is: cached camera for the shared aim derivation — this runs every frame the familiar
    /// is alive, and Camera.main is a tag lookup (same reason SpellBeam caches one).</summary>
    private Camera _mainCam;

    /// <summary>1l6q: cached look pivot for the spray's aim. The bolts now use the DIRECTION-based
    /// aim (<see cref="SpellCaster.StraightFlightDirection"/>), whose preferred source is the player
    /// camera pivot — current-frame, so the stream does not trail a frame behind (1jo). Read once at
    /// Initialize from the caster root's <see cref="PlayerController"/>; null when there is none, and
    /// the helper then falls back to the camera and finally to the caster's own forward.</summary>
    private Transform _lookPivot;

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
        PlayerController owner = _casterRoot != null ? _casterRoot.GetComponent<PlayerController>() : null;
        _lookPivot = owner != null ? owner.PlayerCameraPivot : null;
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
            // 1is/1l6p: for a FORWARD-SPRAYING familiar, Radius is NEITHER a range NOR a size. The
            // spray has no target gate at all, so this number does not bound what the spell reaches
            // (the bolts' own flight envelope decides that); and since 1l6p the drawn ring is a FIXED
            // authored halo (SummonModelBuilder.FamiliarRingRadius), not a readout of this radius — a
            // vertical ring sized by 5 m would be a 10 m portal. Stated because "radius 5" reads like a
            // range. (For every other summon it is still the live reach read by NearestEnemy.)
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

        // 1l6r: place the follower through the SAME function that will place it every follow frame,
        // BEFORE the body is built and the birth flash is drawn. The flash lives 0.45 s and is spawned
        // at transform.position, so when the station was first set only in Update, the whole birth
        // effect (and the first rendered frame) appeared wherever the spawn seed was rather than behind
        // the player. Birth and follow now share one placement.
        if (_follow && _casterRoot != null) FollowCaster();

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

        if (_cores != null)
        {
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 3.2f);
            for (int i = 0; i < _cores.Length; i++)
                if (_cores[i] != null) _cores[i].localScale = _coreBaseScale * pulse;
        }

        // 1ir: follow BEFORE the tick, so this frame's bolts are aimed from where the circle now is.
        if (_follow && _casterRoot != null) FollowCaster();

        _tick -= Time.deltaTime;
        if (_tick <= 0f)
        {
            _tick = TickInterval > 0f ? TickInterval : 0.5f;
            Tick();
        }
    }

    /// <summary>1ir/1is: keep the ring BEHIND the caster, snapped to the ground so its anchor does not
    /// hang in the air on a slope or float when they jump (1l6p: the ring itself rides
    /// <c>FamiliarRingHeight</c> above that anchor). Skips the caster's own colliders — see
    /// <see cref="SpellCaster.GroundProbeUp"/>.
    /// <para>1l6r: this is ALSO the BIRTH placement. <see cref="Initialize"/> calls it once, before the
    /// body is built and the birth flash is drawn, so the familiar is at its final station from the
    /// first rendered frame — and, because birth and follow are the same function, they cannot
    /// disagree (1jx's "agree by construction", made literal).</para>
    /// <para>1is: the offset is applied BEFORE the probe, not after. Probing at the caster and then
    /// moving the result 1.8 m back would keep the ring at the player's own floor height while it
    /// sits behind a step or the lip of a slope — the exact "circles at your feet, not where it is"
    /// read that offsetting was supposed to fix.</para>
    /// <para>Takes the NEAREST hit, not the first. <c>RaycastNonAlloc</c> returns hits in an
    /// unspecified order, so walking past a building would sometimes park the ring on its roof and
    /// sometimes on the street, depending on buffer layout. A downward ray's nearest hit is the
    /// lowest <c>point.y</c>, which is the ground under the ring.</para>
    /// <para><b>No wall avoidance</b> — the ring tracks the caster's XZ outright, so it can pass
    /// through a wall with them. Stated here because "follows you" reads like it stops at
    /// obstacles. Making it a bound spirit that clips is the cheaper, more readable behaviour than a
    /// sliding solver, and this is the honest description of what ships.</para></summary>
    private void FollowCaster()
    {
        // 1jx: stationed on the CASTER BODY, not the camera aim line. The player's body rotation is
        // yaw-only (PlayerController.HandleMouseLook writes transform.rotation = Euler(0, _yaw, 0)),
        // so _casterRoot.forward IS the upper-body yaw: the ring sits exactly behind the player and
        // rotates with their body as the camera moves. The old 1is derivation reused CurrentAimDirection
        // (the camera-point aim the beam uses), which pulled the ring off-axis by the
        // third-person camera's 1jl shoulder offset and collapsed it onto the player's feet when aiming
        // steeply down. The spray (FireForward) uses the look DIRECTION (StraightFlightDirection, 1l6q)
        // — "pours bolts down your aim line" — so the station basis and the fire basis are deliberately
        // DIFFERENT, and that is the point: the ring follows the body, the fire follows the aim.
        Vector3 flat = new Vector3(_casterRoot.forward.x, 0f, _casterRoot.forward.z);
        Vector3 back = flat.sqrMagnitude > 0.0001f
            ? flat.normalized * -BackOffset
            : Vector3.zero;

        Vector3 p = _casterRoot.position + back;
        transform.position = SpellCaster.GroundUnder(p, _casterRoot, _groundBuffer);
        Vector3 flatForward = flat.sqrMagnitude > 0.0001f ? flat.normalized : _casterRoot.forward;
        transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
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

    /// <summary>1is: spray forward along the caster's aim, unconditionally — no target test.
    /// This is what makes the familiar a stream rather than a turret, and it is why
    /// <see cref="Radius"/> no longer bounds anything for this spell (see Initialize).
    /// <para>The muzzle is a random orb on the ring (1l6p — see <see cref="Muzzle"/>), so the bolts
    /// visibly leave the familiar rather than a single fixed point.</para>
    /// <para><b>1l6q: the bolts fly PARALLEL to the look, not converging on the reticle.</b> 1l6o
    /// aimed each bolt with <see cref="SpellCaster.CurrentAimDirection"/> — the POINT-based aim, i.e.
    /// the direction from the muzzle to the crosshair's world point — so every bolt angled in toward
    /// that point. Because the muzzle is offset (behind the caster, chest height), that convergence
    /// made the whole stream read as "launching at the cursor" (the report) rather than straight ahead.
    /// It now uses <see cref="SpellCaster.StraightFlightDirection"/> — the DIRECTION-based aim, the
    /// SAME one a normal projectile (<c>FireProjectile</c>) uses — so each orb fires parallel to where
    /// you are looking (yaw AND pitch, so 1l6o's "adapt shooting angle when the player turns" still
    /// holds) and the stream no longer skews toward the reticle. The pivot is preferred over the
    /// camera so the aim is current-frame (1jo).</para>
    /// <para><b>1l6p: the muzzle is a random orb on the ring</b> (<see cref="Muzzle"/>), so consecutive
    /// bolts leave from different points and the stream reads as fire thrown from the ring rather than
    /// spat from one spot.</para></summary>
    private void FireForward()
    {
        if (_mainCam == null) _mainCam = Camera.main;
        Vector3 from = Muzzle();
        Vector3 dir = SpellCaster.StraightFlightDirection(_mainCam, _lookPivot,
            _casterRoot != null ? _casterRoot.forward : Vector3.forward);
        SpawnBolt(from, dir);
    }

    /// <summary>1l6p: where a summon's next bolt is born. The familiar's ring has one orb per
    /// <c>FamiliarCoreCount</c>, chosen at random so shots fan across the rim; every other body has a
    /// single orb. Falls back to a point above the summon if the body failed to build (no shader), so
    /// a model fault cannot silence the spell's damage.</summary>
    private Vector3 Muzzle()
    {
        if (_cores != null && _cores.Length > 0)
        {
            Transform core = _cores[Random.Range(0, _cores.Length)];
            if (core != null) return core.position;
        }
        return transform.position + Vector3.up * 1.6f;
    }

    private void FireAtNearest()
    {
        Transform target = NearestEnemy();
        if (target == null) return;

        Vector3 muzzle = Muzzle();
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
    /// crystal + orbiting shards), or the familiar's ring when the summon is caster-anchored (1l6p: a
    /// vertical ring of launch orbs, not the old ground circle).
    /// <para><b>1lv1 adds an authored third body.</b> A summon whose look grants
    /// <see cref="SummonBody.TeslaCoil"/> draws the stationary coil spire instead (Static Coil), so
    /// this one node owns all three shapes and the flag can only ever pick one — the drawn body and
    /// the flag cannot half-enable. Every other summon still resolves to <see cref="SummonBody.Default"/>
    /// and takes the follow/totem branch unchanged.</para>
    /// <para>1jd: the geometry now lives in <see cref="SummonModelBuilder"/> (under
    /// <c>Models/Magic/</c>), so "summon model" is findable by name and by folder. The shapes, the
    /// names and the pulse are unchanged; the builder returns the pulsing orbs and the scale they
    /// pulse around as ONE record, so the two cannot drift apart at this boundary (1l6p: an array —
    /// the familiar fires each bolt from a random rim orb).</para></summary>
    private void BuildVisual()
    {
        SummonModelBuilder.Body body = _look.SummonBodyModel == SummonBody.TeslaCoil
            ? SummonModelBuilder.BuildTeslaCoil(transform, _color)
            : _follow
                ? SummonModelBuilder.BuildFamiliarCircle(transform, _color)
                : SummonModelBuilder.BuildTotem(transform, _color);
        _cores = body.Cores;
        _coreBaseScale = body.CoreBaseScale;
    }
}