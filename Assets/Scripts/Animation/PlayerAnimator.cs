using UnityEngine;

/// <summary>
/// Procedural blocky walk/run/idle animation for the MapBuilder player model.
///
/// Sits on the "PlayerModel" root (built by <see cref="PlayerModelBuilder.BuildPlayerModel"/>).
/// The standing model is authored with shoulder -> elbow and hip -> knee pivots
/// (ShoulderL/R, ElbowL/R, HipL/R, KneeL/R) plus a "Torso" pivot that holds the upper body;
/// this component swings those pivots sinusoidally while the player moves.
/// Two gaits blend by speed: a calm natural walk and a distinct exaggerated cartoon run
/// (high knees, wide pumping arms, forward torso lean) that takes over at sprint speed.
///
/// Skipped while sitting or riding. Rest pose is local identity, which keeps any weapon
/// rigged to the hand bones at its equipped pose when idle.
/// </summary>
public sealed class PlayerAnimator : MonoBehaviour
{
    private PlayerController _pc;
    private Transform _shoulderL;
    private Transform _shoulderR;
    private Transform _elbowL;
    private Transform _elbowR;
    private Transform _hipL;
    private Transform _hipR;
    private Transform _kneeL;
    private Transform _kneeR;
    private Transform _body;
    private Transform _torso;
    private Transform _head;
    private Vector3 _bodyBasePos;

    /// <summary>Left shoulder pivot (null when the model has no arms). Used by WeaponAnimator.</summary>
    public Transform ShoulderL => _shoulderL;

    /// <summary>Right shoulder pivot (null when the model has no arms). Used by WeaponAnimator.</summary>
    public Transform ShoulderR => _shoulderR;

    /// <summary>Left elbow pivot (null when the model has no arms). Used by WeaponAnimator.</summary>
    public Transform ElbowL => _elbowL;

    /// <summary>Right elbow pivot (null when the model has no arms). Used by WeaponAnimator.</summary>
    public Transform ElbowR => _elbowR;

    private Vector3 _lastRootPos;
    private float _phase;
    private float _time;

    /// <summary>
    /// Low-pass rate (1/s) for the measured planar speed. Cadence is a division by
    /// <see cref="StrideLength"/>, so an unsmoothed one-frame spike would otherwise be amplified
    /// straight into the <c>_phase</c> integrator and leave a permanent phase error behind.
    /// </summary>
    private const float SpeedSmoothing = 12f;

    /// <summary>
    /// Any measured planar speed above this is a teleport/respawn, not locomotion, and is
    /// discarded rather than smoothed. Comfortably above sprint (10 m/s) and a stacked
    /// MoveSpeed build, comfortably below a teleport.
    /// </summary>
    private const float TeleportSpeed = 30f;

    /// <summary>Smoothed planar speed (m/s) of the character root. See <see cref="SpeedSmoothing"/>.</summary>
    private float _speedH;

    /// <summary>
    /// When true, the arm pivots (shoulders AND elbows) are left entirely to a
    /// <see cref="WeaponAnimator"/>, which drives them through its attack pose track / ready sway
    /// while SuppressArms is set. Managed by <see cref="AcquireArms"/> / <see cref="ReleaseArms"/>
    /// so several weapon rigs (dual wield, or an attack over a ready sway) can hold the arms at once.
    /// </summary>
    public bool SuppressArms;

    private int _armOwners;

    /// <summary>Last frame a WeaponAnimator wrote the arm pivots (PingArms). Feeds the watchdog.</summary>
    private float _lastArmWrite;

    private bool _armWatchdogLogged;

    [Tooltip("How much the upper body pitches with the camera look (0 = none, 1 = full camera pitch). "
        + "Looking down bends the torso forward, looking up leans it back.")]
    public float TorsoLookBlend = 0.5f;

    [Tooltip("Metres the body travels per FULL gait cycle (two steps: left forward, right forward). "
        + "The cadence is speed / stride, so one cycle advancing the body by exactly this much is what "
        + "keeps the feet planted instead of sliding. Bigger stride = fewer, longer steps. "
        + "The default 5.6 puts a 5 m/s walk at ~0.89 Hz and a 10 m/s sprint at ~1.79 Hz.")]
    public float StrideLength = 5.6f;

    [Tooltip("Hard ceiling on the gait cycle rate (Hz), so a very fast build cannot turn the run "
        + "into a blur. Above the ceiling the feet DO slide, because the animation can no longer "
        + "express the real speed - raise this before raising StrideLength if the legs skate. "
        + "1jp: the two knobs trade against each other, and StrideLength is the one that reads as "
        + "'slower animation'. Raising it lowers the rate but lengthens the ground covered per cycle, "
        + "so a slower gait skates more unless the leg swing amplitude goes up with it. At the 5.6 "
        + "default this ceiling first binds at ~17.9 m/s, far above the 10 m/s sprint, so it is not "
        + "what is setting the on-screen rate.")]
    public float MaxCadence = 3.2f;

    /// <summary>Arm gait scale per held weapon (0 = tight, 1 = full pump).</summary>
    private struct ArmGait
    {
        public float SwingScale;
        public float ElbowScale;
        public float CrossScale;
    }

    private readonly System.Collections.Generic.Dictionary<string, ArmGait> _armGaits =
        new System.Collections.Generic.Dictionary<string, ArmGait>
        {
            { "iron_sword",       new ArmGait { SwingScale = 0.95f, ElbowScale = 0.90f, CrossScale = 0.95f } },
            { "dagger",           new ArmGait { SwingScale = 1.00f, ElbowScale = 0.95f, CrossScale = 1.00f } },
            { "greatsword",       new ArmGait { SwingScale = 0.60f, ElbowScale = 0.55f, CrossScale = 0.50f } },
            { "greataxe",         new ArmGait { SwingScale = 0.55f, ElbowScale = 0.50f, CrossScale = 0.45f } },
            { "warhammer",        new ArmGait { SwingScale = 0.50f, ElbowScale = 0.45f, CrossScale = 0.40f } },
            { "katana",           new ArmGait { SwingScale = 0.70f, ElbowScale = 0.60f, CrossScale = 0.60f } },
            { "lance",            new ArmGait { SwingScale = 0.40f, ElbowScale = 0.35f, CrossScale = 0.30f } },
            { "longbow",          new ArmGait { SwingScale = 0.80f, ElbowScale = 0.75f, CrossScale = 0.80f } },
            { "staff",            new ArmGait { SwingScale = 0.85f, ElbowScale = 0.80f, CrossScale = 0.85f } },
            { "holy_book",        new ArmGait { SwingScale = 0.85f, ElbowScale = 0.80f, CrossScale = 0.85f } },
            { "bone_wand",        new ArmGait { SwingScale = 0.85f, ElbowScale = 0.80f, CrossScale = 0.85f } },
            { "control_orb",      new ArmGait { SwingScale = 0.85f, ElbowScale = 0.80f, CrossScale = 0.85f } },
            { "lute",             new ArmGait { SwingScale = 0.85f, ElbowScale = 0.80f, CrossScale = 0.85f } },
            { "buckler",          new ArmGait { SwingScale = 0.30f, ElbowScale = 0.30f, CrossScale = 0.30f } },
            { "round_shield",     new ArmGait { SwingScale = 0.25f, ElbowScale = 0.25f, CrossScale = 0.25f } },
            { "tower_shield",     new ArmGait { SwingScale = 0.20f, ElbowScale = 0.20f, CrossScale = 0.20f } },
            { "fist",             new ArmGait { SwingScale = 1.00f, ElbowScale = 0.95f, CrossScale = 1.00f } },
            { "gauntlets",        new ArmGait { SwingScale = 1.00f, ElbowScale = 0.95f, CrossScale = 1.00f } },
            { "throwing_hammer",  new ArmGait { SwingScale = 0.90f, ElbowScale = 0.85f, CrossScale = 0.90f } },
        };
    private readonly ArmGait _defaultArmGait = new ArmGait { SwingScale = 0.9f, ElbowScale = 0.85f, CrossScale = 0.9f };
    private CombatController _cc;
    private WeaponStowAnimator _wstow;

    /// <summary>Claim ownership of the arm pivots (attack or ready sway). Calls SuppressArms on.</summary>
    public void AcquireArms()
    {
        _armOwners++;
        SuppressArms = _armOwners > 0;
    }

    /// <summary>Release one claim on the arm pivots. Calls SuppressArms off when none are left.</summary>
    public void ReleaseArms()
    {
        _armOwners = Mathf.Max(0, _armOwners - 1);
        SuppressArms = _armOwners > 0;
    }

    /// <summary>WeaponAnimator pings each frame it actively drives the arm pivots, keeping the
    /// watchdog (see LateUpdate) from force-releasing a healthy attack/charge/guard hold.</summary>
    public void PingArms() => _lastArmWrite = Time.time;

    /// <summary>The player controller this model jumps with (null until resolved).</summary>
    public PlayerController Controller => _pc;

    private void OnEnable()
    {
        _pc = GetComponentInParent<PlayerController>();

        Transform FindChild(string name)
        {
            var t = transform.Find(name);
            return t;
        }

        _shoulderL = FindChild("Torso/ShoulderL");
        _shoulderR = FindChild("Torso/ShoulderR");
        _elbowL = _shoulderL != null ? _shoulderL.Find("ElbowL") : null;
        _elbowR = _shoulderR != null ? _shoulderR.Find("ElbowR") : null;
        _hipL = FindChild("HipL");
        _hipR = FindChild("HipR");
        _kneeL = _hipL != null ? _hipL.Find("KneeL") : null;
        _kneeR = _hipR != null ? _hipR.Find("KneeR") : null;
        _torso = FindChild("Torso");
        _body = _torso != null ? _torso.Find("Body") : FindChild("Body");
        _head = _torso != null ? _torso.Find("Head") : FindChild("Head");
        if (_body != null) _bodyBasePos = _body.localPosition;

        // Seed the speed tracker from the CURRENT position. Without this the first LateUpdate
        // measures the player against a default(Vector3) - i.e. its distance from the world
        // origin - and reports a huge speed. The old clamped `norm` only showed it as one frame
        // of run pose, but cadence is a DIVISION (see StrideLength) and would fire a multi-Hz
        // burst into the _phase integrator, so the seed is what keeps frame 1 honest.
        _lastRootPos = transform.position;
        _speedH = 0f;
        _cc = GetComponentInParent<CombatController>();
        _wstow = GetComponentInParent<WeaponStowAnimator>();
    }

    private void LateUpdate()
    {
        _time += Time.deltaTime;

        // Safety net: a leaked arm-owner claim (a WeaponAnimator phase that ended without
        // releasing) leaves SuppressArms stuck, freezing the arms in a stale pose forever. Every
        // rig that actively drives the palms pings every frame; a long silence means a leak.
        if (SuppressArms && _armOwners > 0 && Time.time - _lastArmWrite > 0.5f)
        {
            _armOwners = 0;
            SuppressArms = false;
            if (!_armWatchdogLogged)
            {
                _armWatchdogLogged = true;
                Debug.LogWarning("[PlayerAnimator] arm-owner claim hung with no active writer " +
                    "- watchdog released the arms (report a weapon-animator leak).");
            }
        }

        if (_pc == null)
        {
            _pc = GetComponentInParent<PlayerController>();
            if (_pc == null) return;
        }

        bool skip = _pc.IsSitting;
        if (skip)
        {
            RestoreIdle(0f);
            return;
        }

        // Upper body pitches with the camera look: looking down bends the torso forward, looking
        // up leans it back. Applied to the torso in both the idle and moving poses.
        float lookTilt = Mathf.Clamp(_pc.LookPitch, -60f, 60f) * TorsoLookBlend;

        Vector3 rootPos = _pc.transform.position;
        Vector3 delta = rootPos - _lastRootPos;
        _lastRootPos = rootPos;
        if (Time.deltaTime > 0f)
            delta /= Time.deltaTime;

        // Planar speed, teleports rejected and low-passed. Everything below reads this smoothed
        // value, so the walk/idle threshold stops flickering on a noisy frame too.
        float rawH = Mathf.Min(new Vector2(delta.x, delta.z).magnitude, TeleportSpeed);
        _speedH = Mathf.Lerp(_speedH, rawH, 1f - Mathf.Exp(-Time.deltaTime * SpeedSmoothing));
        float speedH = _speedH;

        if (speedH < 0.35f)
        {
            Breathe();
            RestoreIdle(0f);
            if (_torso != null)
                _torso.localRotation = Quaternion.Euler(lookTilt, 0f, 0f);
            return;
        }

        // norm 0 (walk) .. 1 (sprint): drives the POSE blend below - how far the arms and knees
        // swing and how much the run leans. The gait RATE is not derived from this; it is derived
        // from the measured speed further down. Note runSpeed reads the raw MoveSpeed /
        // SprintMultiplier fields, so it does not see the perk or water multipliers that the
        // measured speed does - a stacked build therefore poses slightly short of a full run.
        float runSpeed = _pc.MoveSpeed * _pc.SprintMultiplier * 0.9f;
        float norm = Mathf.Clamp01((speedH - 0.4f) / Mathf.Max(0.1f, runSpeed));

        // Two gaits blended by speed: a calm natural walk and, past ~45% speed, a distinct
        // exaggerated cartoon run (full at sprint) â€” high knees, wide pumping arms, forward
        // lean and a bouncy bobble.
        float runBlend = Mathf.SmoothStep(0.45f, 0.8f, norm);

        // Cadence follows the character's ACTUAL measured speed, not a fixed ladder. One cycle is
        // two steps, so the honest relationship is cadence = speed / stride: the body then covers
        // exactly one StrideLength per cycle and the feet plant instead of skating. The old
        // `1.8 + norm * 2.0` had a large constant floor and reached 3.8 Hz at sprint - 7.6 steps a
        // second, far past readable. A longer stride is what slows it: it buys fewer, bigger steps
        // rather than a slower character.
        float cadence = Mathf.Clamp(speedH / Mathf.Max(StrideLength, 0.1f), 0f, MaxCadence);
        _phase += cadence * Mathf.PI * 2f * Time.deltaTime;

        // â”€â”€ Walk pose (natural gait) â”€â”€
        float wLegAmp = (0.32f + norm * 0.3f) * Mathf.Rad2Deg;
        float wArmAmp = (0.5f + norm * 0.55f) * Mathf.Rad2Deg;
        float wLegL = Mathf.Sin(_phase) * wLegAmp;
        float wLegR = Mathf.Sin(_phase + Mathf.PI) * wLegAmp;
        // Contralateral swing: each arm moves OPPOSITE its same-side leg, so the left and right
        // sides pump in alternate directions and the body visibly swings on both sides.
        float wArmR = Mathf.Sin(_phase + 0.35f) * wArmAmp;
        float wArmL = Mathf.Sin(_phase + Mathf.PI + 0.35f) * wArmAmp;
        float wKneeBend = (0.42f + norm * 0.5f) * Mathf.Rad2Deg;
        float wKneeL = Mathf.Max(0f, wLegL) * (wKneeBend / Mathf.Max(0.01f, wLegAmp));
        float wKneeR = Mathf.Max(0f, wLegR) * (wKneeBend / Mathf.Max(0.01f, wLegAmp));
        float wElbowAmp = wArmAmp;
        // Elbow mirrors the arm direction with the SAME angular in both directions: crooks back by
        // the full swing angle on the back-swing and crooks forward by the same amount on the
        // forward-swing, so the bend is perfectly symmetric about the straight arm.
        float wElbowL = (wArmL / Mathf.Max(0.01f, wArmAmp)) * wElbowAmp;
        float wElbowR = (wArmR / Mathf.Max(0.01f, wArmAmp)) * wElbowAmp;

        // â”€â”€ Run pose (exaggerated cartoon: big strides, high knees, wide pumping arms) â”€â”€
        float rEase = Mathf.Clamp01((norm - 0.45f) / 0.35f); // 0 at run start .. 1 at sprint
        float rLegAmp = (0.5f + rEase * 0.4f) * Mathf.Rad2Deg;
        float rArmAmp = (0.55f + rEase * 0.3f) * Mathf.Rad2Deg;
        float rLegL = Mathf.Sin(_phase) * rLegAmp;
        float rLegR = Mathf.Sin(_phase + Mathf.PI) * rLegAmp;
        float rArmR = Mathf.Sin(_phase + 0.2f) * rArmAmp;
        float rArmL = Mathf.Sin(_phase + Mathf.PI + 0.2f) * rArmAmp;
        float rKneeBend = (0.9f + rEase * 0.25f) * Mathf.Rad2Deg;
        float rKneeL = Mathf.Max(0f, rLegL) * (rKneeBend / Mathf.Max(0.01f, rLegAmp));
        float rKneeR = Mathf.Max(0f, rLegR) * (rKneeBend / Mathf.Max(0.01f, rLegAmp));
        float rElbowAmp = rArmAmp * 0.8f;
        float rElbowL = (rArmL / Mathf.Max(0.01f, rArmAmp)) * rElbowAmp;
        float rElbowR = (rArmR / Mathf.Max(0.01f, rArmAmp)) * rElbowAmp;

        float legL = Mathf.Lerp(wLegL, rLegL, runBlend);
        float legR = Mathf.Lerp(wLegR, rLegR, runBlend);
        float kneeL = Mathf.Lerp(wKneeL, rKneeL, runBlend);
        float kneeR = Mathf.Lerp(wKneeR, rKneeR, runBlend);
        float armL = Mathf.Lerp(wArmL, rArmL, runBlend);
        float armR = Mathf.Lerp(wArmR, rArmR, runBlend);
        float elbowL = Mathf.Lerp(wElbowL, rElbowL, runBlend);
        float elbowR = Mathf.Lerp(wElbowR, rElbowR, runBlend);

        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legL, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(legR, 0f, 0f);
        if (_kneeL != null) _kneeL.localRotation = Quaternion.Euler(kneeL, 0f, 0f);
        if (_kneeR != null) _kneeR.localRotation = Quaternion.Euler(kneeR, 0f, 0f);

        if (!SuppressArms)
        {
            // Apply per-weapon arm gaits when PlayerAnimator drives arms (walk/run). WeaponAnimator owns arms during attacks/guard/sway.
            ArmGait gL = _defaultArmGait;
            ArmGait gR = _defaultArmGait;
            string lid = null;
            string rid = null;
            if (_cc != null)
            {
                // Get weapon ids from rigs if available (read via WeaponRigHost)
                var lhost = _cc.LeftHand != null ? _cc.LeftHand.GetComponent<WeaponRigHost>() : null;
                var rhost = _cc.RightHand != null ? _cc.RightHand.GetComponent<WeaponRigHost>() : null;
                lid = lhost != null && lhost.Data != null ? lhost.Data.id : null;
                rid = rhost != null && rhost.Data != null ? rhost.Data.id : null;
                if (_cc.Wielding == CombatController.WieldingState.TwoHand)
                {
                    string twoId = rid ?? lid;
                    if (!string.IsNullOrEmpty(twoId) && _armGaits.TryGetValue(twoId, out var tg)) { gL = tg; gR = tg; }
                }
                else
                {
                    if (!string.IsNullOrEmpty(lid) && _armGaits.TryGetValue(lid, out var lg)) gL = lg;
                    if (!string.IsNullOrEmpty(rid) && _armGaits.TryGetValue(rid, out var rg)) gR = rg;
                    // tighten left if holding shield
                    if (lid == "buckler" || lid == "round_shield" || lid == "tower_shield")
                    {
                        gL.SwingScale *= 0.7f; gL.ElbowScale *= 0.7f; gL.CrossScale *= 0.7f;
                    }
                }
            }
            float cross = 0.35f;
            float armLA = Mathf.Lerp(wArmL, rArmL, runBlend) * gL.SwingScale;
            float armRA = Mathf.Lerp(wArmR, rArmR, runBlend) * gR.SwingScale;
            float elbowLA = Mathf.Lerp(wElbowL, rElbowL, runBlend) * gL.ElbowScale;
            float elbowRA = Mathf.Lerp(wElbowR, rElbowR, runBlend) * gR.ElbowScale;
            if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(armRA, 0f, 0f);
            if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(armLA, 0f, 0f);
            if (_elbowR != null) _elbowR.localRotation = Quaternion.Euler(elbowRA, 0f, 0f);
            if (_elbowL != null) _elbowL.localRotation = Quaternion.Euler(elbowLA, 0f, 0f);
        }
        // When SuppressArms is set, a WeaponAnimator fully owns the shoulders AND elbows
        // (windup/strike/charge pose tracks), so nothing is written here mid-attack.

        // Cartoon run top body: forward lean, gentle bob â€” the upper body stays stable so only the
        // arms and legs carry the motion (no wild torso/head swinging). The + lookTilt bends the
        // torso up/down with the camera's vertical aim.
        if (_torso != null)
            _torso.localRotation = Quaternion.Euler(12f * runBlend + lookTilt, 0f, 0f);
        if (_head != null)
            _head.localRotation = Quaternion.Euler(-(1f + 2f * runBlend) * Mathf.Sin(_phase * 2f) - 2f * runBlend, 0f, 0f);
        if (_body != null && _bodyBasePos != default)
        {
            float bob = Mathf.Sin(_phase * 2f) * (0.012f * runBlend);
            _body.localPosition = _bodyBasePos + new Vector3(0f, bob, 0f);
        }
    }

    /// <summary>Subtle idle breathing: bob the torso.</summary>
    private void Breathe()
    {
        if (_body == null || _bodyBasePos == default) return;
        float bob = Mathf.Sin(_time * 2.2f) * 0.006f;
        _body.localPosition = _bodyBasePos + new Vector3(0f, bob, 0f);
    }

    private void RestoreIdle(float blend)
    {
        Vector3 idle = Quaternion.identity.eulerAngles;
        if (!SuppressArms)
        {
            SetLerped(_shoulderL, idle, blend);
            SetLerped(_shoulderR, idle, blend);
            SetLerped(_elbowL, idle, blend);
            SetLerped(_elbowR, idle, blend);
        }
        SetLerped(_hipL, idle, blend);
        SetLerped(_hipR, idle, blend);
        SetLerped(_kneeL, idle, blend);
        SetLerped(_kneeR, idle, blend);
        if (_torso != null)
            _torso.localRotation = Quaternion.identity;
        if (_head != null)
            _head.localRotation = Quaternion.identity;
        if (_body != null && _bodyBasePos != default)
            _body.localPosition = Vector3.Lerp(_body.localPosition, _bodyBasePos, 0.2f);
    }

    private static void SetLerped(Transform t, Vector3 rot, float blend)
    {
        if (t == null) return;
        t.localRotation = Quaternion.Euler(rot);
    }
}
