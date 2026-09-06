using UnityEngine;

/// <summary>
/// Per-weapon attack animation — a unique visible "using" motion for every one of the 15
/// weapons ("animation pack lives on the weapon", Phase 10).
///
/// Mounted on each weapon rig by <see cref="WeaponRigBuilder"/>. Drives two things through a
/// windup → strike → recover cycle that matches <see cref="CombatController"/>'s attack timing:
///   • the weapon's own local transform (relative to the hand it is parented to), and
///   • the owning shoulder pivot on the player model (so the blocky arm visibly swings, punches,
///     raises for a cast, draws a bow, overhead chops, thrusts, etc.).
///
/// Each weapon id maps to a <see cref="MotionProfile"/> (motion kind + amplitude + flavor), so
/// e.g. a katana sweeps wider than an iron sword, a warhammer chops bigger than a greatsword,
/// and every magic focus raises the arm with its own bob/arc/pulse.
///
/// The shoulder is fully owned while attacking: this component sets
/// <see cref="PlayerAnimator.SuppressArms"/> so the walk/idle animator can't overwrite the swing
/// mid-attack, then restores the captured base pose on recovery. The weapon rest pose is
/// re-captured at the start of every attack, so re-parenting onto a hand (WeaponRigBuilder's
/// ReparentToHands) never breaks the animation.
///
/// Block weapons are authored +Y-up with the grip at the base; swings rotate the blade upward
/// from the fist.
/// </summary>
public sealed class WeaponAnimator : MonoBehaviour
{
    // Motion kinds. Each produces a mechanically distinct arm + weapon motion.
    private const int K_Slash = 0;      // iron_sword / katana — horizontal sweep
    private const int K_Overhead = 1;   // greatsword / greataxe / warhammer — chop down
    private const int K_Thrust = 2;     // lance — two-handed lunge
    private const int K_Dual = 3;       // gauntlets — alternating quick punches
    private const int K_Bow = 4;        // longbow — raise, draw, loose
    private const int K_Fling = 5;      // throwing_hammer — overhand fling
    private const int K_Cast = 6;       // staff / holy_book / bone_wand / control_orb / lute
    private const int K_Stab = 7;       // dagger — double jab

    private struct MotionProfile
    {
        public readonly int Kind;
        public readonly float Amp;
        public readonly int Flavor;

        public MotionProfile(int kind, float amp, int flavor)
        {
            Kind = kind;
            Amp = amp;
            Flavor = flavor;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, MotionProfile> Profiles =
        new System.Collections.Generic.Dictionary<string, MotionProfile>
        {
            { "iron_sword",       new MotionProfile(K_Slash, 1.00f, 0) },
            { "katana",           new MotionProfile(K_Slash, 1.50f, 0) },
            { "greatsword",       new MotionProfile(K_Overhead, 1.00f, 0) },
            { "greataxe",         new MotionProfile(K_Overhead, 1.25f, 1) },
            { "warhammer",        new MotionProfile(K_Overhead, 1.10f, 2) },
            { "lance",            new MotionProfile(K_Thrust, 1.00f, 0) },
            { "gauntlets",        new MotionProfile(K_Dual, 1.00f, 0) },
            { "longbow",          new MotionProfile(K_Bow, 1.00f, 0) },
            { "throwing_hammer",  new MotionProfile(K_Fling, 1.00f, 0) },
            { "dagger",           new MotionProfile(K_Stab, 1.00f, 0) },
            { "staff",            new MotionProfile(K_Cast, 1.00f, 0) },
            { "holy_book",        new MotionProfile(K_Cast, 1.10f, 1) },
            { "bone_wand",        new MotionProfile(K_Cast, 0.90f, 2) },
            { "control_orb",      new MotionProfile(K_Cast, 1.20f, 3) },
            { "lute",             new MotionProfile(K_Cast, 0.85f, 4) },
        };

    private static MotionProfile FallbackProfile = new MotionProfile(K_Slash, 1f, 0);

    private string _weaponId;
    private MotionProfile _profile;

    // Rest pose snapshots, re-captured each attack (re-parent safe).
    private Vector3 _basePos;
    private Vector3 _baseEuler;
    private Vector3 _baseScale;
    private Quaternion _ownerBaseRot;
    private Quaternion _otherBaseRot;

    private bool _active;
    private bool _heavy;
    private float _t;
    private float _duration;

    private PlayerAnimator _playerAnim;
    private Transform _ownerShoulder;
    private Transform _otherShoulder;
    private bool _mirrorOther;

    private void OnEnable()
    {
        var host = GetComponent<WeaponRigHost>();
        var data = host != null ? host.Data : null;
        _weaponId = data != null ? data.id : null;
        _profile = _weaponId != null && Profiles.TryGetValue(_weaponId, out var p) ? p : FallbackProfile;

        _playerAnim = GetComponentInParent<PlayerAnimator>();
    }

    /// <summary>Kick off an attack visual. Duration matches CombatController's attack timing.</summary>
    public void PlayAttack(bool heavy)
    {
        _heavy = heavy;
        _duration = heavy ? CombatController.DefaultHeavyAttackDuration : CombatController.DefaultLightAttackDuration;
        if (_duration <= 0f) _duration = heavy ? 0.45f : 0.25f;
        _t = 0f;
        _active = true;

        // Rest pose freshly captured so re-parenting onto a hand (ReparentToHands) is harmless.
        _basePos = transform.localPosition;
        _baseEuler = transform.localRotation.eulerAngles;
        _baseScale = transform.localScale;

        // Resolve the owning shoulder (rig -> HandR/L -> ElbowR/L -> ShoulderR/L) fresh each attack.
        var parent = transform.parent;
        bool offHand = parent != null && parent.name == "HandL";
        _ownerShoulder = FindOwnerShoulder(parent);
        _ownerBaseRot = _ownerShoulder != null ? _ownerShoulder.localRotation : Quaternion.identity;

        _mirrorOther = !offHand && (_profile.Kind == K_Overhead || _profile.Kind == K_Thrust ||
            _profile.Kind == K_Bow || _profile.Kind == K_Cast);
        if (_playerAnim == null) _playerAnim = GetComponentInParent<PlayerAnimator>();
        _otherShoulder = null;
        if (_mirrorOther && _playerAnim != null)
        {
            _otherShoulder = offHand ? _playerAnim.ShoulderR : _playerAnim.ShoulderL;
            _otherBaseRot = _otherShoulder != null ? _otherShoulder.localRotation : Quaternion.identity;
        }

        if (_playerAnim != null) _playerAnim.SuppressArms = true;
    }

    private void Update()
    {
        if (!_active) return;

        _t += Time.deltaTime;
        float t = Mathf.Clamp01(_t / Mathf.Max(0.001f, _duration));
        bool offHand = transform.parent != null && transform.parent.name == "HandL";

        EvaluateWeapon(_profile, t, _heavy, offHand, out Vector3 euler, out Vector3 pos, out float scale);
        EvaluateShoulder(_profile, t, _heavy, out Vector3 ownerOff);

        transform.localRotation = Quaternion.Euler(_baseEuler + euler);
        transform.localPosition = _basePos + pos;
        transform.localScale = _baseScale * scale;

        if (_ownerShoulder != null)
            _ownerShoulder.localRotation = _ownerBaseRot * Quaternion.Euler(ownerOff);

        if (_otherShoulder != null)
        {
            // Supporting hand follows a softened mirror of the owner's motion.
            var otherOff = ownerOff * (K_Bow == _profile.Kind || K_Cast == _profile.Kind ? 0.85f : 0.5f);
            _otherShoulder.localRotation = _otherBaseRot * Quaternion.Euler(otherOff);
        }

        if (_t >= _duration)
            End();
    }

    private void End()
    {
        _active = false;
        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
        if (_ownerShoulder != null) _ownerShoulder.localRotation = _ownerBaseRot;
        if (_otherShoulder != null) _otherShoulder.localRotation = _otherBaseRot;
        if (_playerAnim != null) _playerAnim.SuppressArms = false;
    }

    /// <summary>
    /// From a hand-pivoted weapon rig, climb to the arm's shoulder pivot
    /// (rig -> HandR/L -> ElbowR/L -> ShoulderR/L) regardless of how deep the arm chain is.
    /// </summary>
    private static Transform FindOwnerShoulder(Transform from)
    {
        var p = from;
        while (p != null)
        {
            if (p.name.StartsWith("Shoulder")) return p;
            p = p.parent;
        }
        return null;
    }

    private void OnDisable()
    {
        if (_playerAnim != null) _playerAnim.SuppressArms = false;
    }

    // ──────────────────────────────────────────────────────────
    //  Weapon-local motion (relative to the hand it's parented to)
    // ──────────────────────────────────────────────────────────

    private static void EvaluateWeapon(MotionProfile p, float t, bool heavy, bool offHand,
        out Vector3 euler, out Vector3 pos, out float scale)
    {
        euler = Vector3.zero;
        pos = Vector3.zero;
        scale = 1f;
        float amp = p.Amp;
        float h = heavy ? 1.2f : 1f;

        switch (p.Kind)
        {
            case K_Slash:
            {
                // Wind up back, sweep across, recover. Higher amp = wider sweep (katana).
                float y;
                float x = 0f;
                if (t < 0.3f) y = Mathf.Lerp(0f, 70f, Ease(Seg(t, 0f, 0.3f)));
                else if (t < 0.62f)
                {
                    float k = Ease(Seg(t, 0.3f, 0.62f));
                    y = Mathf.Lerp(70f, -115f, k) * amp;
                    x = -35f * k * amp;
                }
                else
                {
                    float k = Ease(Seg(t, 0.62f, 1f));
                    y = Mathf.Lerp(-115f * amp, 0f, k);
                    x = Mathf.Lerp(-35f * amp, 0f, k);
                }
                euler = new Vector3(x, y, 0f);
                break;
            }

            case K_Overhead:
            {
                float x;
                float s = 1f;
                int flavor = p.Flavor;
                if (t < 0.35f)
                {
                    x = Mathf.Lerp(0f, -125f, Ease(Seg(t, 0f, 0.35f))) * amp;
                    if (flavor == 2) x *= 1.2f; // warhammer: slower, deeper windup (amp + lag)
                }
                else if (t < 0.7f)
                {
                    float k = Ease(Seg(t, 0.35f, 0.7f));
                    x = Mathf.Lerp(-125f * amp, 45f, k);
                    s = 1f + 0.1f * Mathf.Sin(Mathf.PI * k); // crunch on the strike
                }
                else
                {
                    x = Mathf.Lerp(45f, 0f, Ease(Seg(t, 0.7f, 1f)));
                }
                euler = new Vector3(x, 0f, 0f) * h;
                pos = new Vector3(0f, 0f, flavor == 1 ? 0.05f * Mathf.Sin(Mathf.PI * Seg(t, 0.35f, 0.7f)) : 0f);
                scale = s;
                break;
            }

            case K_Thrust:
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
                break;
            }

            case K_Dual:
            {
                int strikes = heavy ? 3 : 2;
                // Off hand cuts half a phase so the punches alternate.
                float phase = Mathf.PI * t * strikes + (offHand ? Mathf.PI : 0f);
                float depth = 0.38f;
                pos = new Vector3(0f, 0f, -0.1f + depth * (0.5f + 0.5f * Mathf.Sin(phase)));
                if (!heavy && offHand) pos.z *= 0.7f;
                break;
            }

            case K_Bow:
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
                    float k = Ease(Seg(t, 0.42f, 0.6f));
                    y = Mathf.Lerp(78f, 6f, k);
                    z = Mathf.Lerp(22f, -8f, k);
                }
                else
                {
                    float k = Ease(Seg(t, 0.6f, 1f));
                    y = Mathf.Lerp(6f, 0f, k);
                    z = Mathf.Lerp(-8f, 0f, k);
                }
                euler = new Vector3(0f, y, z);
                pos = new Vector3(0f, 0f, 0.06f * Mathf.Sin(Mathf.PI * Seg(t, 0.42f, 0.75f)));
                break;
            }

            case K_Fling:
            {
                float x;
                float z = 0f;
                if (t < 0.32f)
                    x = Mathf.Lerp(0f, -150f, Ease(Seg(t, 0f, 0.32f)));
                else if (t < 0.7f)
                {
                    float k = Ease(Seg(t, 0.32f, 0.7f));
                    x = Mathf.Lerp(-150f, 65f, k);
                    z = 0.2f * k;
                }
                else
                {
                    x = Mathf.Lerp(65f, 0f, Ease(Seg(t, 0.7f, 1f)));
                }
                euler = new Vector3(x, 0f, 0f) * h;
                pos = new Vector3(0f, 0f, z);
                break;
            }

            case K_Stab:
            {
                // Two quick jabs: jab at ~0.33 and ~0.62.
                float jab = Mathf.Max(Strike(Seg(t, 0.18f, 0.36f)), Strike(Seg(t, 0.5f, 0.68f))) * h;
                pos = new Vector3(0f, 0f, -0.26f * jab);
                euler = new Vector3(-14f * jab, 0f, 0f);
                break;
            }

            case K_Cast:
            {
                float x;
                float yOff;
                float arc;
                float s = 1f;
                int flavor = p.Flavor;
                float pulse = Mathf.Sin(Mathf.PI * Seg(t, 0.35f, 0.68f));

                // Each focus pulses differently: staff (0) arcs up; holy book (1) sways;
                // bone_wand (2) pulses its size; control_orb (3) spins its arc; lute (4) strums.
                switch (flavor)
                {
                    default:
                    case 0: arc = 18f * pulse; break;
                    case 1: arc = 12f * pulse; yOff = 0.16f * Seg(t, 0.35f, 0.68f); break;
                    case 2: arc = 14f * pulse; s = 1f + 0.28f * pulse; break;
                    case 3: arc = 34f * pulse; yOff = 0.2f * Seg(t, 0.35f, 0.68f); break;
                    case 4: arc = 16f * pulse; s = 1f + 0.06f * Mathf.Sin(Mathf.PI * 4f * Seg(t, 0f, 1f)); break;
                }

                if (t < 0.35f)
                {
                    float k = Ease(Seg(t, 0f, 0.35f));
                    x = -58f * k;
                    yOff = 0.14f * k;
                }
                else if (t < 0.68f)
                {
                    x = -58f;
                    yOff = 0.14f;
                }
                else
                {
                    float k = Ease(Seg(t, 0.68f, 1f));
                    x = Mathf.Lerp(-58f, 0f, k);
                    yOff = Mathf.Lerp(0.14f, 0f, k);
                }

                euler = new Vector3(x, 0f, arc) * amp;
                pos = new Vector3(0f, yOff, 0f);
                scale = s;
                break;
            }
        }
    }

    /// <summary>Sharp 0→1→0 spike used for stabs/flashes.</summary>
    private static float Strike(float u)
    {
        u = Mathf.Clamp01(u);
        return Mathf.Sin(Mathf.PI * u);
    }

    // ──────────────────────────────────────────────────────────
    //  Shoulder (arm) motion — composed on top of the captured base pose
    // ──────────────────────────────────────────────────────────

    private static void EvaluateShoulder(MotionProfile p, float t, bool heavy, out Vector3 off)
    {
        off = Vector3.zero;
        float h = heavy ? 1.25f : 1f;

        switch (p.Kind)
        {
            case K_Slash:
            {
                // Wind the shoulder back, sweep the arm across, recover.
                float y;
                float x = 0f;
                if (t < 0.25f) y = Mathf.Lerp(0f, 70f, Ease(Seg(t, 0f, 0.25f)));
                else if (t < 0.6f)
                {
                    float k = Ease(Seg(t, 0.25f, 0.6f));
                    y = Mathf.Lerp(70f, -105f, k);
                    x = -18f * k;
                }
                else
                {
                    float k = Ease(Seg(t, 0.6f, 1f));
                    y = Mathf.Lerp(-105f, 0f, k);
                    x = Mathf.Lerp(-18f, 0f, k);
                }
                off = new Vector3(x, y, 0f) * p.Amp;
                break;
            }

            case K_Overhead:
            {
                // Throw the arm fully up, slam it down, recover.
                float x;
                if (t < 0.35f) x = Mathf.Lerp(0f, -150f, Ease(Seg(t, 0f, 0.35f)));
                else if (t < 0.7f) x = Mathf.Lerp(-150f, 32f, Ease(Seg(t, 0.35f, 0.7f)));
                else x = Mathf.Lerp(32f, 0f, Ease(Seg(t, 0.7f, 1f)));
                off = new Vector3(x, 0f, 0f) * h * p.Amp;
                break;
            }

            case K_Thrust:
            {
                // Both hands drive the lance forward.
                float push = Mathf.Clamp01(Mathf.Max(Ease(Seg(t, 0f, 0.3f)), 1f - Ease(Seg(t, 0.75f, 1f))));
                off = new Vector3(70f * push, 0f, 0f) * h;
                break;
            }

            case K_Dual:
            {
                // Alternating punches: shoulder drives forward in sync with the jab.
                int strikes = heavy ? 3 : 2;
                float phase = Mathf.PI * t * strikes;
                off = new Vector3(62f * (0.5f + 0.5f * Mathf.Sin(phase)), 0f, 0f);
                break;
            }

            case K_Bow:
            {
                // Raise both arms, draw, then loose.
                float x;
                if (t < 0.45f) x = Mathf.Lerp(0f, -120f, Ease(Seg(t, 0f, 0.45f)));
                else if (t < 0.68f) x = Mathf.Lerp(-120f, -20f, Ease(Seg(t, 0.45f, 0.68f)));
                else x = Mathf.Lerp(-20f, 0f, Ease(Seg(t, 0.68f, 1f)));
                off = new Vector3(x, 0f, 0f);
                break;
            }

            case K_Fling:
            {
                float x;
                float y = 0f;
                if (t < 0.35f) x = Mathf.Lerp(0f, -140f, Ease(Seg(t, 0f, 0.35f)));
                else if (t < 0.75f)
                {
                    float k = Ease(Seg(t, 0.35f, 0.75f));
                    x = Mathf.Lerp(-140f, 58f, k);
                    y = 26f * k;
                }
                else
                {
                    float k = Ease(Seg(t, 0.75f, 1f));
                    x = Mathf.Lerp(58f, 0f, k);
                    y = Mathf.Lerp(26f, 0f, k);
                }
                off = new Vector3(x, y, 0f) * h;
                break;
            }

            case K_Stab:
            {
                // Quick forward snaps at each jab.
                float jab = Mathf.Max(Strike(Seg(t, 0.18f, 0.36f)), Strike(Seg(t, 0.5f, 0.68f))) * h;
                off = new Vector3(34f * jab, 0f, 0f);
                break;
            }

            case K_Cast:
            {
                // Raise the arm, pulse while channeling, lower.
                int flavor = p.Flavor;
                float pulse = Mathf.Sin(Mathf.PI * Seg(t, 0.3f, 0.7f));
                float raise;
                if (t < 0.32f) raise = Ease(Seg(t, 0f, 0.32f));
                else if (t < 0.7f) raise = 1f;
                else raise = 1f - Ease(Seg(t, 0.7f, 1f));
                raise = Mathf.Clamp01(raise);

                float yaw = 0f;
                switch (flavor)
                {
                    case 0: yaw = 8f * pulse; break;       // staff: steady raise
                    case 1: yaw = 18f * pulse; break;      // holy book: sway while channeling
                    case 2: yaw = 10f * pulse; break;      // bone_wand: tight flicker
                    case 3: yaw = 24f * pulse; break;      // control_orb: wide sweep
                    case 4: yaw = -16f * pulse; break;     // lute: opposite strum
                }
                off = new Vector3(-85f * raise, yaw * p.Amp, 0f);
                break;
            }
        }
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private static float Seg(float t, float a, float b) => Mathf.InverseLerp(a, b, t);
}