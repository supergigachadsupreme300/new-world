using UnityEngine;

/// <summary>
/// Per-weapon attack animation ("animation pack lives on the weapon").
///
/// Mounted on each weapon rig by <see cref="WeaponRigBuilder"/>; drives the weapon's own local
/// transform (relative to the hand it is parented to) through a windup → strike → recover cycle
/// that matches <see cref="CombatController"/>'s attack timing, so the blocky model visibly
/// swings/fires/casts depending on which weapon is equipped.
///
/// Block weapons are authored +Y-up with the grip at the base, so a swing rotates the blade that
/// extends upward from the fist. Style is chosen from the weapon id (see <see cref="StyleFor"/>).
/// </summary>
public sealed class WeaponAnimator : MonoBehaviour
{
    private enum AttackStyle
    {
        Slash,      // one-hand sword/katana/dagger — horizontal sweep
        Overhead,   // greatsword / greataxe / warhammer — overhead chop
        Thrust,     // lance — forward lunge
        Dual,       // gauntlets — alternating quick jabs (off-hand staggered)
        Draw,       // longbow — draw then loose
        Fling,      // throwing_hammer — overhand fling
        Cast        // staff / wand / orb / lute / holy book — raise + arc pulse
    }

    private WeaponRigHost _host;
    private AttackStyle _style = AttackStyle.Slash;
    private bool _offHand;

    private Vector3 _basePos;
    private Vector3 _baseEuler;
    private Vector3 _baseScale;

    private bool _active;
    private bool _heavy;
    private float _t;
    private float _duration;

    private void OnEnable()
    {
        _host = GetComponent<WeaponRigHost>();
        var data = _host != null ? _host.Data : null;
        _style = StyleFor(data);

        _basePos = transform.localPosition;
        _baseEuler = transform.localRotation.eulerAngles;
        _baseScale = transform.localScale;

        // Left-hand mounts (dual-wield clones and left-handed single rigs) drive the off-hand.
        if (transform.parent != null)
            _offHand = transform.parent.name == "HandL";
    }

    /// <summary>Kick off an attack visual. Duration matches CombatController's attack timing.</summary>
    public void PlayAttack(bool heavy)
    {
        _heavy = heavy;
        _duration = heavy ? CombatController.DefaultHeavyAttackDuration : CombatController.DefaultLightAttackDuration;
        if (_duration <= 0f) _duration = heavy ? 0.45f : 0.25f;
        _t = 0f;
        _active = true;
    }

    private void Update()
    {
        if (!_active) return;

        _t += Time.deltaTime;
        float t = Mathf.Clamp01(_t / Mathf.Max(0.001f, _duration));

        Evaluate(_style, t, _heavy, _offHand, out Vector3 euler, out Vector3 pos, out float scale);

        transform.localRotation = Quaternion.Euler(_baseEuler + euler);
        transform.localPosition = _basePos + pos;
        transform.localScale = _baseScale * scale;

        if (_t >= _duration)
        {
            _active = false;
            transform.localRotation = Quaternion.Euler(_baseEuler);
            transform.localPosition = _basePos;
            transform.localScale = _baseScale;
        }
    }

    private static AttackStyle StyleFor(WeaponData weapon)
    {
        if (weapon == null) return AttackStyle.Slash;
        switch (weapon.id)
        {
            case "greatsword":
            case "greataxe":
            case "warhammer":
                return AttackStyle.Overhead;
            case "lance":
                return AttackStyle.Thrust;
            case "gauntlets":
                return AttackStyle.Dual;
            case "longbow":
                return AttackStyle.Draw;
            case "throwing_hammer":
                return AttackStyle.Fling;
            case "staff":
            case "bone_wand":
            case "control_orb":
            case "lute":
            case "holy_book":
                return AttackStyle.Cast;
            default:
                return AttackStyle.Slash;
        }
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private static float Seg(float t, float a, float b) => Mathf.InverseLerp(a, b, t);

    private static void Evaluate(AttackStyle style, float t, bool heavy, bool offHand,
        out Vector3 euler, out Vector3 pos, out float scale)
    {
        euler = Vector3.zero;
        pos = Vector3.zero;
        scale = 1f;

        switch (style)
        {
            case AttackStyle.Slash:
            {
                float y;
                float x = 0f;
                if (t < 0.3f)
                {
                    y = Mathf.Lerp(0f, 70f, Ease(Seg(t, 0f, 0.3f)));
                }
                else if (t < 0.62f)
                {
                    float p = Ease(Seg(t, 0.3f, 0.62f));
                    y = Mathf.Lerp(70f, -115f, p);
                    x = -35f * p;
                }
                else
                {
                    float p = Ease(Seg(t, 0.62f, 1f));
                    y = Mathf.Lerp(-115f, 0f, p);
                    x = Mathf.Lerp(-35f, 0f, p);
                }
                euler = new Vector3(x, y, 0f);
                break;
            }

            case AttackStyle.Overhead:
            {
                float x;
                if (t < 0.35f)
                    x = Mathf.Lerp(0f, -125f, Ease(Seg(t, 0f, 0.35f)));
                else if (t < 0.7f)
                    x = Mathf.Lerp(-125f, 40f, Ease(Seg(t, 0.35f, 0.7f)));
                else
                    x = Mathf.Lerp(40f, 0f, Ease(Seg(t, 0.7f, 1f)));
                euler = new Vector3(x, 0f, 0f);
                break;
            }

            case AttackStyle.Thrust:
            {
                float z;
                if (t < 0.4f)
                    z = Mathf.Lerp(0f, -0.28f, Ease(Seg(t, 0f, 0.4f)));
                else if (t < 0.75f)
                    z = Mathf.Lerp(-0.28f, 0.4f, Ease(Seg(t, 0.4f, 0.75f)));
                else
                    z = Mathf.Lerp(0.4f, 0f, Ease(Seg(t, 0.75f, 1f)));
                pos = new Vector3(0f, 0f, z);
                euler = new Vector3(0f, -10f * Mathf.Sin(Mathf.PI * Seg(t, 0f, 1f)), 0f);
                (pos, euler) = (pos * (heavy ? 1.25f : 1f), euler * (heavy ? 1.25f : 1f));
                break;
            }

            case AttackStyle.Dual:
            {
                int strikes = heavy ? 3 : 2;
                // Off hand cuts half a phase so the punches alternate.
                float phase = Mathf.PI * t * strikes + (offHand ? Mathf.PI : 0f);
                float depth = 0.38f;
                pos = new Vector3(0f, 0f, -0.1f + depth * (0.5f + 0.5f * Mathf.Sin(phase)));
                if (!heavy && offHand) pos.z *= 0.7f;
                break;
            }

            case AttackStyle.Draw:
            {
                float y;
                float z;
                if (t < 0.42f)
                {
                    y = Mathf.Lerp(0f, 78f, Ease(Seg(t, 0f, 0.42f)));
                    z = Mathf.Lerp(0f, 22f, Ease(Seg(t, 0f, 0.42f)));
                }
                else if (t < 0.6f)
                {
                    float p = Ease(Seg(t, 0.42f, 0.6f));
                    y = Mathf.Lerp(78f, 6f, p);
                    z = Mathf.Lerp(22f, -8f, p);
                }
                else
                {
                    float p = Ease(Seg(t, 0.6f, 1f));
                    y = Mathf.Lerp(6f, 0f, p);
                    z = Mathf.Lerp(-8f, 0f, p);
                }
                euler = new Vector3(0f, y, z);
                pos = new Vector3(0f, 0f, 0.06f * Mathf.Sin(Mathf.PI * Seg(t, 0.42f, 0.75f)));
                break;
            }

            case AttackStyle.Fling:
            {
                float x;
                float z = 0f;
                if (t < 0.32f)
                    x = Mathf.Lerp(0f, -150f, Ease(Seg(t, 0f, 0.32f)));
                else if (t < 0.7f)
                {
                    float p = Ease(Seg(t, 0.32f, 0.7f));
                    x = Mathf.Lerp(-150f, 65f, p);
                    z = 0.2f * p;
                }
                else
                {
                    x = Mathf.Lerp(65f, 0f, Ease(Seg(t, 0.7f, 1f)));
                }
                euler = new Vector3(x, 0f, 0f);
                pos = new Vector3(0f, 0f, z);
                (pos, euler) = (pos * (heavy ? 1.3f : 1f), euler * (heavy ? 1.15f : 1f));
                break;
            }

            case AttackStyle.Cast:
            {
                float x;
                float yOff;
                float arc = 0f;
                float s = 1f;
                if (t < 0.35f)
                {
                    float p = Ease(Seg(t, 0f, 0.35f));
                    x = -58f * p;
                    yOff = 0.14f * p;
                }
                else if (t < 0.68f)
                {
                    float p = Seg(t, 0.35f, 0.68f);
                    x = -58f;
                    yOff = 0.14f;
                    s = 1f + 0.2f * Mathf.Sin(Mathf.PI * p);
                    arc = 18f * Mathf.Sin(Mathf.PI * p);
                }
                else
                {
                    float p = Ease(Seg(t, 0.68f, 1f));
                    x = Mathf.Lerp(-58f, 0f, p);
                    yOff = Mathf.Lerp(0.14f, 0f, p);
                }
                euler = new Vector3(x, 0f, arc);
                pos = new Vector3(0f, yOff, 0f);
                scale = s;
                break;
            }
        }
    }
}