using UnityEngine;

/// <summary>
/// Per-weapon attack animation — a unique visible "using" motion for every one of the 15
/// weapons ("animation pack lives on the weapon", Phase 10).
///
/// Mounted on each weapon rig by <see cref="WeaponRigBuilder"/>. Plays a windup → strike/charge
/// → recover limb pose-track that matches <see cref="CombatController"/>'s attack timing, and
/// reports the actual duration it will run so the controller's action lock stays in sync.
///
/// The arm is the animation: each weapon is a keyframed pose track driving the owning shoulder +
/// elbow (and, for two-hand grips, the supporting arm) so the blade swings WITH the arm — no more
/// blade-only "wiggle". The weapon rides the hand at its equipped rest pose; only magic focuses
/// keep small local accents (orb arc, scale pulse, strum) on top.
///
/// The arms are fully owned while attacking: this component sets
/// <see cref="PlayerAnimator.SuppressArms"/> so the walk/idle animator can't overwrite the swing
/// mid-attack, then restores the captured base pose on recovery. Rest poses are re-captured at the
/// start of every attack, so re-parenting onto a hand (WeaponRigBuilder's ReparentToHands) never
/// breaks the animation.
///
/// Block weapons are authored +Y-up with the grip at the base; swings rotate the arm (and therefore
/// the blade) from the shoulder while the elbow flexes for punches, draws, stabs and thrusts.
/// </summary>
public sealed class WeaponAnimator : MonoBehaviour
{
    private const int K_None = -1;   // no local weapon accent (arm carries the motion)
    private const int K_Staff = 0;   // staff — steady raise + arc pulse
    private const int K_Book = 1;    // holy_book — two-hand raise + chant sway
    private const int K_Wand = 2;    // bone_wand — quick raise + size pulse
    private const int K_Orb = 3;     // control_orb — raised arm + wide sweeping arc
    private const int K_Lute = 4;    // lute — held at the side, string strum
    private const int K_Dual = 5;    // gauntlets — alternate hands off-phase

    private enum OffArm
    {
        None,    // weapon in one hand only (sword, dagger, hammer, casters, gauntlets each hand)
        Mirror,  // two-hand grip — support arm copies the swing (greatsword, greataxe, warhammer, lance, katana)
        Asym     // the two arms play different tracks (longbow: bow arm vs draw arm)
    }

    /// <summary>One keyframe of the owner arm's pose track (angles in degrees, additive).</summary>
    private struct PoseKey
    {
        public float t;                 // normalized time 0..1
        public float shX, shY, shZ;     // owner shoulder pitch / yaw / roll (additive)
        public float elX;               // owner elbow flex (additive)

        public PoseKey(float t, float shX, float shY, float shZ, float elX)
        {
            this.t = t;
            this.shX = shX; this.shY = shY; this.shZ = shZ;
            this.elX = elX;
        }
    }

    private struct WeaponAnimDef
    {
        public OffArm Mode;
        public PoseKey[] Owner;   // throwing-arm track
        public PoseKey[] Other;   // support-arm track (Mode == Asym only)
        public int Accent;        // K_* accent kind
        public float TimeLight;   // light-attack duration
        public float TimeHeavy;   // heavy-attack duration

        public WeaponAnimDef(OffArm mode, PoseKey[] owner, PoseKey[] other, int accent,
            float light, float heavy)
        {
            Mode = mode;
            Owner = owner;
            Other = other;
            Accent = accent;
            TimeLight = light;
            TimeHeavy = heavy;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, WeaponAnimDef> Defs =
        new System.Collections.Generic.Dictionary<string, WeaponAnimDef>
        {
            { "iron_sword", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.30f, -75f, 55f, 0f, -15f),     // raise arm + wind right/back
                new PoseKey(0.62f, -80f, -45f, 0f, -5f),     // sweep across the chest
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.30f, 0.50f) },

            { "greatsword", new WeaponAnimDef(OffArm.Mirror, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, -150f, 0f, 0f, 8f),       // both arms raise overhead
                new PoseKey(0.70f, -55f, 0f, 0f, 14f),       // slam down in front
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.45f, 0.65f) },

            { "dagger", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.20f, -58f, 6f, 0f, -28f),      // jab extend
                new PoseKey(0.34f, 12f, 0f, 0f, 18f),        // re-cock
                new PoseKey(0.55f, -58f, 6f, 0f, -28f),      // jab extend again
                new PoseKey(0.70f, 12f, 0f, 0f, 18f),
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.24f, 0.34f) },

            { "katana", new WeaponAnimDef(OffArm.Mirror, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.25f, -70f, 70f, 0f, -40f),     // deep draw, elbow curled
                new PoseKey(0.88f, -60f, -200f, 0f, -10f),   // full-arm 360° sweep
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.45f, 0.60f) },

            { "greataxe", new WeaponAnimDef(OffArm.Mirror, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, -45f, -60f, 0f, 10f),     // wind back low, opposite side
                new PoseKey(0.75f, -75f, 60f, 0f, 6f),       // cleave across the body
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.40f, 0.60f) },

            { "lance", new WeaponAnimDef(OffArm.Mirror, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, 20f, 0f, 0f, 38f),        // pull back, elbows flexed out
                new PoseKey(0.75f, -65f, 0f, 0f, -10f),      // both arms drive forward into the lunge
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.32f, 0.48f) },

            { "gauntlets", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.30f, -72f, 0f, 0f, -24f),      // punch extend
                new PoseKey(0.48f, -24f, 0f, 0f, 34f),       // re-cock
                new PoseKey(0.72f, -72f, 0f, 0f, -24f),      // second punch
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_Dual, 0.26f, 0.36f) },

            { "longbow", new WeaponAnimDef(OffArm.Asym,
                // Owner (right) = the draw hand.
                Track(
                    new PoseKey(0f, 0f, 0f, 0f, 0f),
                    new PoseKey(0.35f, -25f, -20f, 0f, -120f), // pull string to the cheek
                    new PoseKey(0.65f, -25f, -20f, 0f, -120f), // hold the draw (charge)
                    new PoseKey(0.80f, -55f, 0f, 0f, -15f),    // loose — snap forward
                    new PoseKey(1f, 0f, 0f, 0f, 0f)),
                // Other (left) = the bow arm, extended toward the target.
                Track(
                    new PoseKey(0f, 0f, 0f, 0f, 0f),
                    new PoseKey(0.35f, -85f, 0f, 0f, -6f),
                    new PoseKey(0.65f, -85f, 0f, 0f, -6f),
                    new PoseKey(0.80f, -82f, 0f, 0f, -4f),
                    new PoseKey(1f, 0f, 0f, 0f, 0f)),
                K_None, 0.50f, 0.80f) },

            { "throwing_hammer", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.32f, -145f, 0f, 0f, -80f),     // wind up overhead, elbow cocked
                new PoseKey(0.70f, -50f, 0f, 0f, -5f),       // whip forward, extend
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.32f, 0.45f) },

            { "warhammer", new WeaponAnimDef(OffArm.Mirror, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.40f, -155f, 0f, 0f, 6f),       // slow telegraphed raise
                new PoseKey(0.55f, -155f, 0f, 0f, 6f),       // hold at the apex
                new PoseKey(0.72f, -65f, 0f, 0f, 16f),       // crushing slam
                new PoseKey(0.86f, -72f, 0f, 0f, 22f),       // impact bounce
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.55f, 0.75f) },

            { "staff", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, -60f, 0f, 0f, -18f),      // raise the focus
                new PoseKey(0.70f, -60f, 0f, 0f, -18f),      // channel
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_Staff, 0.42f, 0.62f) },

            { "holy_book", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, -45f, 0f, 0f, -26f),      // raise the tome
                new PoseKey(0.70f, -45f, 0f, 0f, -26f),
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_Book, 0.44f, 0.64f) },

            { "bone_wand", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, -70f, 0f, 0f, -14f),
                new PoseKey(0.70f, -70f, 0f, 0f, -14f),
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_Wand, 0.38f, 0.56f) },

            { "control_orb", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, -80f, 10f, 0f, -12f),
                new PoseKey(0.70f, -80f, 10f, 0f, -12f),
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_Orb, 0.46f, 0.66f) },

            { "lute", new WeaponAnimDef(OffArm.None, Track(
                new PoseKey(0f, 0f, 0f, 0f, 0f),
                new PoseKey(0.35f, -30f, 15f, 0f, -34f),     // hold the lute at the side
                new PoseKey(0.70f, -30f, 15f, 0f, -34f),
                new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_Lute, 0.42f, 0.60f) },
        };

    private static WeaponAnimDef FallbackDef = new WeaponAnimDef(OffArm.None,
        Track(new PoseKey(0f, 0f, 0f, 0f, 0f),
              new PoseKey(0.30f, -75f, 55f, 0f, -15f),
              new PoseKey(0.62f, -80f, -45f, 0f, -5f),
              new PoseKey(1f, 0f, 0f, 0f, 0f)), null, K_None, 0.30f, 0.50f);

    private string _weaponId;
    private WeaponAnimDef _def;

    // Rest pose snapshots, re-captured each attack (re-parent safe).
    private Vector3 _basePos;
    private Vector3 _baseEuler;
    private Vector3 _baseScale;
    private Quaternion _ownerShBase;
    private Quaternion _ownerElBase;
    private Quaternion _otherShBase;
    private Quaternion _otherElBase;

    private bool _active;
    private bool _heavy;
    private float _t;
    private float _duration;
    private bool _offHand;

    private PlayerAnimator _playerAnim;
    private Transform _ownerShoulder;
    private Transform _ownerElbow;
    private Transform _otherShoulder;
    private Transform _otherElbow;

    private void OnEnable()
    {
        var host = GetComponent<WeaponRigHost>();
        var data = host != null ? host.Data : null;
        _weaponId = data != null ? data.id : null;
        _def = _weaponId != null && Defs.TryGetValue(_weaponId, out var d) ? d : FallbackDef;

        _playerAnim = GetComponentInParent<PlayerAnimator>();
    }

    /// <summary>
    /// Kick off an attack visual. Returns the duration <see cref="CombatController"/> should keep
    /// the player locked for, so slower weapons (bow draw, warhammer windup) stay in sync.
    /// </summary>
    public float PlayAttack(bool heavy)
    {
        _heavy = heavy;
        _duration = Mathf.Max(0.001f, heavy ? _def.TimeHeavy : _def.TimeLight);
        _t = 0f;
        _active = true;

        // Rest poses freshly captured so re-parenting onto a hand (ReparentToHands) is harmless.
        _basePos = transform.localPosition;
        _baseEuler = transform.localRotation.eulerAngles;
        _baseScale = transform.localScale;

        // Resolve the arm chain (rig -> HandL/R -> ElbowL/R -> ShoulderL/R) fresh each attack.
        var parent = transform.parent;
        _offHand = parent != null && parent.name == "HandL";
        _ownerShoulder = FindOwnerShoulder(parent);
        _ownerElbow = FindOwnerElbow(parent);
        if (_playerAnim == null) _playerAnim = GetComponentInParent<PlayerAnimator>();
        _otherShoulder = _playerAnim != null ? (_offHand ? _playerAnim.ShoulderR : _playerAnim.ShoulderL) : null;
        _otherElbow = _playerAnim != null ? (_offHand ? _playerAnim.ElbowR : _playerAnim.ElbowL) : null;

        _ownerShBase = _ownerShoulder != null ? _ownerShoulder.localRotation : Quaternion.identity;
        _ownerElBase = _ownerElbow != null ? _ownerElbow.localRotation : Quaternion.identity;
        _otherShBase = _otherShoulder != null ? _otherShoulder.localRotation : Quaternion.identity;
        _otherElBase = _otherElbow != null ? _otherElbow.localRotation : Quaternion.identity;

        if (_playerAnim != null) _playerAnim.SuppressArms = true;
        return _duration;
    }

    private void Update()
    {
        if (!_active) return;

        _t += Time.deltaTime;
        float t = Mathf.Clamp01(_t / _duration);

        // Dual-wield gauntlets alternate hands by half a phase so the punches land one-two.
        if (_def.Accent == K_Dual && _offHand) t = Mathf.Repeat(t + 0.5f, 1f);

        float h = _heavy ? 1.15f : 1f;

        PoseKey k = Sample(_def.Owner, t);
        Vector3 sh = new Vector3(k.shX, k.shY, k.shZ) * h;
        float el = k.elX * h;

        if (_ownerShoulder != null)
            _ownerShoulder.localRotation = _ownerShBase * Quaternion.Euler(sh);
        if (_ownerElbow != null)
            _ownerElbow.localRotation = _ownerElBase * Quaternion.Euler(el, 0f, 0f);

        switch (_def.Mode)
        {
            case OffArm.Mirror:
                // Two-hand grip: the support arm mirrors the swing (yaw flipped side-to-side).
                if (_otherShoulder != null)
                    _otherShoulder.localRotation = _otherShBase * Quaternion.Euler(k.shX * h, -k.shY * h, k.shZ * h);
                if (_otherElbow != null)
                    _otherElbow.localRotation = _otherElBase * Quaternion.Euler(el, 0f, 0f);
                break;

            case OffArm.Asym:
                PoseKey ok = Sample(_def.Other, t);
                if (_otherShoulder != null)
                    _otherShoulder.localRotation = _otherShBase * Quaternion.Euler(ok.shX * h, ok.shY * h, ok.shZ * h);
                if (_otherElbow != null)
                    _otherElbow.localRotation = _otherElBase * Quaternion.Euler(ok.elX * h, 0f, 0f);
                break;
        }

        // The weapon rides the hand; only magic focuses add a small local accent.
        ApplyAccent(_def.Accent, t, out Vector3 aEuler, out Vector3 aPos, out float aScale);
        transform.localRotation = Quaternion.Euler(_baseEuler + aEuler);
        transform.localPosition = _basePos + aPos;
        transform.localScale = _baseScale * aScale;

        if (_t >= _duration)
            End();
    }

    private void End()
    {
        _active = false;
        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
        if (_ownerShoulder != null) _ownerShoulder.localRotation = _ownerShBase;
        if (_ownerElbow != null) _ownerElbow.localRotation = _ownerElBase;
        if (_otherShoulder != null) _otherShoulder.localRotation = _otherShBase;
        if (_otherElbow != null) _otherElbow.localRotation = _otherElBase;
        if (_playerAnim != null) _playerAnim.SuppressArms = false;
    }

    /// <summary>
    /// From a hand-pivoted weapon rig, climb to the arm's shoulder pivot (rig -> HandR/L ->
    /// ElbowR/L -> ShoulderR/L) regardless of how deep the arm chain is.
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

    /// <summary>Climb to the arm's elbow pivot the same way as <see cref="FindOwnerShoulder"/>.</summary>
    private static Transform FindOwnerElbow(Transform from)
    {
        var p = from;
        while (p != null)
        {
            if (p.name.StartsWith("Elbow")) return p;
            p = p.parent;
        }
        return null;
    }

    private void OnDisable()
    {
        if (_playerAnim != null) _playerAnim.SuppressArms = false;
    }

    // ──────────────────────────────────────────────────────────
    //  Pose-track playback
    // ──────────────────────────────────────────────────────────

    private static PoseKey[] Track(params PoseKey[] keys) => keys;

    private static PoseKey Sample(PoseKey[] track, float t)
    {
        if (track == null || track.Length == 0) return new PoseKey(0f, 0f, 0f, 0f, 0f);
        if (track.Length == 1) return track[0];
        if (t <= track[0].t) return track[0];
        if (t >= track[track.Length - 1].t) return track[track.Length - 1];

        for (int i = 0; i < track.Length - 1; i++)
        {
            if (t < track[i + 1].t)
            {
                float u = Ease(Seg(t, track[i].t, track[i + 1].t));
                return new PoseKey(
                    t,
                    Mathf.Lerp(track[i].shX, track[i + 1].shX, u),
                    Mathf.Lerp(track[i].shY, track[i + 1].shY, u),
                    Mathf.Lerp(track[i].shZ, track[i + 1].shZ, u),
                    Mathf.Lerp(track[i].elX, track[i + 1].elX, u));
            }
        }
        return track[track.Length - 1];
    }

    /// <summary>
    /// Small weapon-local accent per magic weapon on top of the arm choreography: an arc (staff /
    /// orb), a chant sway (book), a size pulse (wand) or a string strum (lute). Melee returns 0.
    /// </summary>
    private static void ApplyAccent(int accent, float t, out Vector3 euler, out Vector3 pos, out float scale)
    {
        euler = Vector3.zero;
        pos = Vector3.zero;
        scale = 1f;
        if (accent == K_None) return;

        float pulse = Mathf.Sin(Mathf.PI * Seg(t, 0.35f, 0.70f));

        switch (accent)
        {
            case K_Staff:
                euler = new Vector3(0f, 0f, 14f * pulse);
                pos = new Vector3(0f, 0.02f * Seg(t, 0.35f, 0.70f), 0f);
                break;
            case K_Book:
                euler = new Vector3(0f, 16f * pulse, 0f);
                pos = new Vector3(0f, 0.10f * Seg(t, 0.35f, 0.70f), 0f);
                break;
            case K_Wand:
                euler = new Vector3(0f, 0f, 10f * pulse);
                scale = 1f + 0.26f * pulse;
                break;
            case K_Orb:
                euler = new Vector3(0f, 0f, 30f * pulse);
                pos = new Vector3(0f, 0.16f * Seg(t, 0.35f, 0.70f), 0f);
                break;
            case K_Lute:
                scale = 1f + 0.08f * Mathf.Sin(Mathf.PI * 4f * Seg(t, 0f, 1f));
                break;
        }
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private static float Seg(float t, float a, float b) => Mathf.InverseLerp(a, b, t);
}