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
        }

        Vector3 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        float s = Mathf.Max(0.05f, chargeScale);
        // Push forward slightly so larger preview doesn't engulf player
        float push = Mathf.Clamp(0.25f * (s - 1f), 0f, 0.5f);
        _body.transform.SetPositionAndRotation(pos + d * push, Quaternion.LookRotation(d));
        _body.transform.localScale = Vector3.one * s;
        _body.SetActive(true);
        _active = true;
    }

    /// <summary>Hide the held body immediately (aim cancelled, shot released, nothing to aim).</summary>
    public void Hide()
    {
        _active = false;
        if (_body != null) _body.SetActive(false);
    }
}
