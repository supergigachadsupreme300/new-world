using UnityEngine;

/// <summary>
/// The spell's OWN projectile body, held at the muzzle of the magic weapon while a
/// <see cref="SpellDelivery.Projectile"/> spell charges — the exact body the player is about to fire,
/// growing with the charge level and placed at the same muzzle the path preview's ray starts from, so
/// the hold reads as "this is the shot" rather than a generic charge hint.
///
/// <para><b>Shown, not fired.</b> This is a render-only stand-in with no <see cref="SpellEffect"/> and
/// no collider: the real projectile is still created by <c>SpellCaster.FireProjectile</c> on release.
/// The caller hides this body before launching, so the two never coexist — the real bolt appears where
/// this one was, at the charge size this one already showed.</para>
///
/// <para><b>It nudges ahead of the muzzle as it grows.</b> The muzzle is a fixed 0.5 m point, but the
/// body's trailing voxels extend BACK from its origin, so a big charged body would sink into the
/// player's torso/arms. The body is pushed further along the aim the larger the charge (see
/// <see cref="Show"/>'s <c>push</c>), so a full overcharge floats a metre clear of the weapon. The
/// launch does not carry this offset (it spawns at the fixed muzzle), so a released bolt appears
/// slightly behind the hold's center — negligible at flight speed, and a deliberate under-report in
/// favour of keeping the spawn point a fixed, wall-safe distance from the caster.</para>
///
/// <para><b>1l1: why the body comes from <see cref="MagicProjectileModelBuilder"/> and not its own
/// art.</b> The whole point is that the hold IS the shot, so it resolves the shape and colour the same
/// way a live cast does (<c>SpellLook.Resolve</c> — rule 13's one resolver) and asks the same builder
/// for the same geometry. A hand-drawn stand-in here would be a second spelling of "what does this
/// spell look like" that rots the moment a shape changes. It is a new caller of that builder (the
/// others: a projectile in flight, the QA bench pedestals, the turret bolt); the builder owns the
/// SHAPE and nothing time-dependent, and this component adds no per-frame effect to it, so it does not
/// repeat 1jg's "trail on a motionless pedestal" mistake (rule 19).</para>
///
/// <para><b>Scale is the caster's charge ladder, read not re-derived (rule 8).</b> The charge scale is
/// passed in by the caller as <c>SpellCaster.SizeScale(charge)</c>; the body's own <c>look.Scale</c> is
/// applied by the builder to the shape child. Setting this root's local scale therefore multiplies the
/// two exactly as <c>FireProjectile</c>'s <c>go.transform.localScale *= sizeScale</c> does — same
/// structure (root carries position/rotation/charge-scale, child carries the look scale), so the hold
/// and the launch cannot disagree about <i>charge</i> size. The launch's second factor, the skill-level
/// <c>RadiusMult</c> the wheel embeds in the weapon mods (<c>SkillContext.SizeScale</c>,
/// <c>1 + (level-1)*0.02</c>), is NOT mirrored: it is a per-skill constant rather than anything the
/// charge animates, and re-deriving it here would be a second spelling of that ladder. The effect is
/// that at high skill levels the released bolt is slightly larger than the held preview — the same
/// honest under-report the beam path preview documents for its reach.</para>
///
/// <para><b>1l92: the hold draws its own inward draft.</b> Twelve small cubes ride a spiral from a
/// rim around the body into its centre, so a charging shot visibly gathers rather than sitting
/// inert. Like the spin, this is per-frame work that belongs to the loop already gated on a live
/// hold (<see cref="Update"/>), not to the shared shape builder (rule 19); the motes' colour is the
/// spell's resolved <c>look.Core</c>, whitened, read from the same <see cref="SpellLook"/> the body
/// uses rather than a second palette (rule 13). They hide with the body on release, so the launched
/// bolt is the only thing on screen the instant the shot fires.</para>
/// </summary>
public sealed class ChargedProjectilePreview : MonoBehaviour
{
    private static ChargedProjectilePreview _instance;

    private GameObject _body;   // projectile-root equivalent: position, rotation, charge scale
    private SpellData _spell;   // the body currently built — rebuilt when the armed spell changes
    private bool _active;

    // 1l6x: the held body spins about its travel axis exactly like the bolt it becomes. The rate is
    // read from SpellEffect.SpinRate through SpellEffect.RollsInFlight (rule 13 - one source), never
    // re-spelled here; _spin is the accumulated angle (deg) applied by Show's rotation so the per-frame
    // path stays a transform write. 1l101: a shape that holds its pose in flight rolls 0 here too.
    private float _spinRate;
    private float _spin;

    // 1l92: motes that stream IN to the held body while the charge builds - a ring of small cubes
    // spiralling from a rim the size of the (charge-scaled) body toward its centre. The per-frame work
    // lives HERE, in the loop that already runs only while a hold is up, not in the shared
    // MagicProjectileModelBuilder (rule 19: the builder owns shape, the preview owns time - the same
    // split 1l71 made for the hold's spin). The colour is the spell's resolved look.Core, whitened, so
    // the motes read as the spell's identity without a new palette (rule 13).
    private Transform _moteRoot;    // container, toggled with the hold
    private Transform[] _mote;      // the motes (children of _moteRoot)
    private float[] _motePhase;     // 0 = rim, 1 = absorbed
    private float[] _moteAngle;     // start angle around the aim axis (radians)
    private float[] _moteSweep;     // radians turned while travelling in (signed)
    private float[] _moteRate;      // phase per second (1 / travel seconds)
    private float[] _moteAlong;     // start offset along the aim (metres)
    private Material _moteMat;
    private Color _moteColor = Color.white;
    private float _moteScale = 1f;  // the caster's charge scale, read not re-derived (rule 8)

    private const int MoteCount = 12;
    private const float MoteSpawnRadius = 0.5f;   // rim radius at charge scale 1 (metres)
    private const float MoteSize = 0.07f;         // cube edge at charge scale 1 (metres)
    private const float MoteAlongSpread = 0.3f;   // +/- along-aim spawn offset (metres)
    private const float MoteTravelMin = 0.45f;    // seconds rim -> centre
    private const float MoteTravelMax = 0.8f;

    /// <summary>Ensure the singleton exists and return it (builds on first access).</summary>
    public static ChargedProjectilePreview Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ChargedProjectilePreview");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<ChargedProjectilePreview>();
            }
            return _instance;
        }
    }

    /// <summary>True while a held projectile body is currently visible.</summary>
    public bool IsActive => _active;

    private void Update()
    {
        if (!_active || _body == null) return;
        _spin = Mathf.Repeat(_spin + _spinRate * Time.deltaTime, 360f);
        UpdateMotes();
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// Show/refresh the held body at <paramref name="pos"/> facing <paramref name="dir"/>, scaled by
    /// <paramref name="chargeScale"/>. Rebuilds the body only when the armed <paramref name="spell"/>
    /// changes, so the per-frame path is a transform write and nothing else.
    /// </summary>
    public void Show(Vector3 pos, Vector3 dir, float chargeScale, SpellData spell)
    {
        if (spell == null) { Hide(); return; }

        if (_body == null || _spell != spell)
        {
            if (_body != null) Destroy(_body);
            _body = new GameObject("ChargedProjectileBody");
            _body.transform.SetParent(transform, false);
            // Same builder and resolver a live cast uses, so the held body is the one that will fly.
            // Render-only: the builder strips every primitive collider and the root keeps none.
            // Named spellLook, not look: Show already declares a Quaternion `look` in the enclosing
            // method scope (CS0136 — a nested block cannot reuse an enclosing scope's local name).
            var spellLook = SpellLook.Resolve(spell);
            MagicProjectileModelBuilder.AttachDefaultProjectileVisual(_body, spellLook, spell.SummonFallingRock);
            _spell = spell;
            // Same rate the released bolt will spin at (rule 13), and start the hold aligned to a
            // fresh launch (its bolt starts at phase 0). 1l101: read through the same predicate the
            // bolt reads, so a shape that holds its pose in flight (the Chill Touch hand) also holds
            // its pose in the hold — 0 leaves the spin a no-op at AngleAxis(0).
            _spinRate = SpellEffect.RollsInFlight(spellLook.DisplayShape) ? SpellEffect.SpinRate : 0f;
            _spin = 0f;
            // 1l92: the charge motes take the spell's own colour (whitened so they read as sparks,
            // not as the body) - resolved once here, never re-resolved per frame (rule 13).
            _moteColor = Color.Lerp(spellLook.Core, Color.white, 0.3f);
            EnsureMotes();
            ApplyMoteLook();
        }

        Vector3 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        float s = Mathf.Max(0.05f, chargeScale);
        _moteScale = s; // 1l92: the motes grow with the body, from the same charge scale the caller read
        // Push the body further along the aim as it grows, so a big charged body's trailing voxels
        // do not sink into the player's torso/arms (the body's tail extends BACK from the muzzle).
        // 1l6n: the ratio was doubled (0.25 -> 0.5 m of push per +1.0 of charge scale) and the cap
        // raised (0.5 -> 1.0 m) because a full overcharge reaches s ~= 2.2 and beyond, where the old
        // cap bound before the body had cleared the player. Play-test knob: raise the 0.5 if a large
        // hold still overlaps, or the 1.0 cap to let extreme overcharges push further.
        float push = Mathf.Clamp(0.5f * (s - 1f), 0f, 1f);
        // LookRotation makes local +Z the travel axis; the trailing local-Z spin is exactly the
        // launched bolt's `transform.Rotate(0, 0, rate*dt)`, so the hold and the flight agree (1l101:
        // both read SpellEffect.RollsInFlight, so a hand holds still in the hold as it does in flight).
        Quaternion look = Quaternion.LookRotation(d);
        _body.transform.SetPositionAndRotation(pos + d * push,
            look * Quaternion.AngleAxis(_spin, Vector3.forward));
        _body.transform.localScale = Vector3.one * s;
        _body.SetActive(true);
        _active = true;
        if (_moteRoot != null) _moteRoot.gameObject.SetActive(true);
    }

    /// <summary>Hide the held body immediately (aim cancelled, shot released, nothing to aim).</summary>
    public void Hide()
    {
        _active = false;
        _spin = 0f;   // a new hold starts aligned with a fresh launch
        if (_body != null) _body.SetActive(false);
        if (_moteRoot != null) _moteRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 1l92: build the charge-mote ring once, on the first hold, and stagger the motes' phases so the
    /// inward stream is continuous rather than one pulse per cycle. Everything the per-frame loop
    /// needs is preallocated here, so a hold allocates nothing after its first frame (the same
    /// discipline <see cref="TrailStrip"/>'s buffers use).
    /// </summary>
    private void EnsureMotes()
    {
        if (_mote != null) return;
        _moteRoot = new GameObject("ChargedProjectileMotes").transform;
        _moteRoot.SetParent(transform, false);
        _mote = new Transform[MoteCount];
        _motePhase = new float[MoteCount];
        _moteAngle = new float[MoteCount];
        _moteSweep = new float[MoteCount];
        _moteRate = new float[MoteCount];
        _moteAlong = new float[MoteCount];
        for (int i = 0; i < MoteCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ChargeMote" + i;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(_moteRoot, false);
            // Born at zero size: the container activates at the end of the Show that builds it, and if
            // this component's Update runs ahead of the caller's on that one frame the motes are never
            // positioned, so a full-size default cube must not exist to be seen.
            go.transform.localScale = Vector3.zero;
            _mote[i] = go.transform;
            RandomizeMote(i);
            // Even start phases so the twelve motes form a continuous stream, not a single wave.
            _motePhase[i] = i / (float)MoteCount;
        }
    }

    /// <summary>1l92: give mote <paramref name="i"/> fresh rim parameters. Called at build and each time
    /// it reaches the centre, so the stream never visibly repeats.</summary>
    private void RandomizeMote(int i)
    {
        _moteAngle[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        // Signed sweep of 0.6-1.6 radians: each mote turns as it is drawn in, so the ring reads as a
        // spiral draft rather than straight spokes.
        _moteSweep[i] = UnityEngine.Random.Range(0.6f, 1.6f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        _moteRate[i] = 1f / UnityEngine.Random.Range(MoteTravelMin, MoteTravelMax);
        _moteAlong[i] = UnityEngine.Random.Range(-MoteAlongSpread, MoteAlongSpread);
    }

    /// <summary>1l92: point every mote at the spell's colour. The material is the project's shared,
    /// colour-keyed sprite material, so this allocates nothing per hold and never mutates a shared
    /// material (see <c>SkillFx.SharedSpriteMaterial</c>'s warning).</summary>
    private void ApplyMoteLook()
    {
        _moteMat = SkillFx.SharedSpriteMaterial(_moteColor);
        if (_mote == null || _moteMat == null) return;
        for (int i = 0; i < MoteCount; i++)
        {
            if (_mote[i] == null) continue;
            var r = _mote[i].GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = _moteMat;
        }
    }

    /// <summary>
    /// 1l92: the per-frame charge-draft loop. Each mote rides a spiral from a rim of
    /// <see cref="MoteSpawnRadius"/> (times the charge scale) to the held body's centre, in the plane
    /// perpendicular to the aim, and a sine size curve makes it wink in and be absorbed - no material
    /// fade, so the shared material stays shared. Phase advances on TIME (a charge is a time-gated
    /// state, not a path), unlike the flight trail's distance gate (rule 19).
    /// </summary>
    private void UpdateMotes()
    {
        if (_mote == null || _moteRoot == null) return;
        Transform body = _body.transform;
        Vector3 origin = body.position;
        Vector3 fwd = body.forward;
        // The aim's perpendicular plane. The same degenerate-direction fallback CastingCircle's
        // FacingRotation uses, so a broken aim reads as a stable ring rather than NaN.
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(Vector3.right, fwd);
        side.Normalize();
        Vector3 up = Vector3.Cross(fwd, side).normalized;

        float dt = Time.deltaTime;
        for (int i = 0; i < MoteCount; i++)
        {
            float phase = _motePhase[i] + _moteRate[i] * dt;
            if (phase >= 1f)
            {
                phase -= 1f;
                RandomizeMote(i);
            }
            _motePhase[i] = phase;

            float ang = _moteAngle[i] + phase * _moteSweep[i];
            float radius = MoteSpawnRadius * _moteScale * (1f - phase);
            Vector3 offset = (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius;
            _mote[i].position = origin + offset + fwd * (_moteAlong[i] * _moteScale);
            // Sine in, sine out: 0 at the rim and at the centre, so nothing pops in or vanishes hard.
            _mote[i].localScale = Vector3.one * (MoteSize * _moteScale * Mathf.Sin(Mathf.PI * phase));
        }
    }
}
