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
/// </summary>
public sealed class ChargedProjectilePreview : MonoBehaviour
{
    private static ChargedProjectilePreview _instance;

    private GameObject _body;   // projectile-root equivalent: position, rotation, charge scale
    private SpellData _spell;   // the body currently built — rebuilt when the armed spell changes
    private bool _active;

    // 1l6x: the held body spins about its travel axis exactly like the bolt it becomes. The rate is
    // read from SpellEffect.SpinRate (rule 13 - one source), never re-spelled here; _spin is the
    // accumulated angle (deg) applied by Show's rotation so the per-frame path stays a transform write.
    private float _spinRate;
    private float _spin;

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
            MagicProjectileModelBuilder.AttachDefaultProjectileVisual(_body, SpellLook.Resolve(spell),
                spell.SummonFallingRock);
            _spell = spell;
            // Same rate the released bolt will spin at (rule 13), and start the hold aligned to a
            // fresh launch (its bolt starts at phase 0).
            _spinRate = SpellEffect.SpinRate(spell.Shape == ProjectileShape.Wave);
            _spin = 0f;
        }

        Vector3 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        float s = Mathf.Max(0.05f, chargeScale);
        // Push the body further along the aim as it grows, so a big charged body's trailing voxels
        // do not sink into the player's torso/arms (the body's tail extends BACK from the muzzle).
        // 1l6n: the ratio was doubled (0.25 -> 0.5 m of push per +1.0 of charge scale) and the cap
        // raised (0.5 -> 1.0 m) because a full overcharge reaches s ~= 2.2 and beyond, where the old
        // cap bound before the body had cleared the player. Play-test knob: raise the 0.5 if a large
        // hold still overlaps, or the 1.0 cap to let extreme overcharges push further.
        float push = Mathf.Clamp(0.5f * (s - 1f), 0f, 1f);
        // LookRotation makes local +Z the travel axis; the trailing local-Z spin is exactly the
        // launched bolt's `transform.Rotate(0, 0, rate*dt)`, so the hold and the flight agree.
        Quaternion look = Quaternion.LookRotation(d);
        _body.transform.SetPositionAndRotation(pos + d * push,
            look * Quaternion.AngleAxis(_spin, Vector3.forward));
        _body.transform.localScale = Vector3.one * s;
        _body.SetActive(true);
        _active = true;
    }

    /// <summary>Hide the held body immediately (aim cancelled, shot released, nothing to aim).</summary>
    public void Hide()
    {
        _active = false;
        _spin = 0f;   // a new hold starts aligned with a fresh launch
        if (_body != null) _body.SetActive(false);
    }
}
