using UnityEngine;

/// <summary>
/// Procedural blocky walk/run/idle animation for the MapBuilder player model.
///
/// Sits on the "PlayerModel" root (built by <see cref="MapBuilder.BuildPlayerModel"/>).
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
    /// When true, the arm pivots (shoulders AND elbows) are left entirely to a
    /// <see cref="WeaponAnimator"/>, which drives them through its attack pose track / ready sway
    /// while SuppressArms is set. Managed by <see cref="AcquireArms"/> / <see cref="ReleaseArms"/>
    /// so several weapon rigs (dual wield, or an attack over a ready sway) can hold the arms at once.
    /// </summary>
    public bool SuppressArms;

    private int _armOwners;

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
    }

    private void LateUpdate()
    {
        _time += Time.deltaTime;

        if (_pc == null)
        {
            _pc = GetComponentInParent<PlayerController>();
            if (_pc == null) return;
        }

        bool skip = _pc.IsSitting || _pc.IsRiding;
        if (skip)
        {
            RestoreIdle(0f);
            return;
        }

        Vector3 rootPos = _pc.transform.position;
        Vector3 delta = rootPos - _lastRootPos;
        _lastRootPos = rootPos;
        if (Time.deltaTime > 0f)
            delta /= Time.deltaTime;

        float speedH = new Vector2(delta.x, delta.z).magnitude;

        if (speedH < 0.35f)
        {
            Breathe();
            RestoreIdle(0f);
            return;
        }

        // norm 0 (walk) .. 1 (sprint): scale cadence and swing amplitude with speed.
        float runSpeed = _pc.MoveSpeed * _pc.SprintMultiplier * 0.9f;
        float norm = Mathf.Clamp01((speedH - 0.4f) / Mathf.Max(0.1f, runSpeed));

        // Two gaits blended by speed: a calm natural walk and, past ~45% speed, a distinct
        // exaggerated cartoon run (full at sprint) — high knees, wide pumping arms, forward
        // lean and a bouncy bobble.
        float runBlend = Mathf.SmoothStep(0.45f, 0.8f, norm);

        float cadence = 1.8f + norm * 2.0f; // Hz
        _phase += cadence * Mathf.PI * 2f * Time.deltaTime;

        // ── Walk pose (natural gait) ──
        float wLegAmp = (0.32f + norm * 0.3f) * Mathf.Rad2Deg;
        float wArmAmp = (0.3f + norm * 0.35f) * Mathf.Rad2Deg;
        float wLegL = Mathf.Sin(_phase) * wLegAmp;
        float wLegR = Mathf.Sin(_phase + Mathf.PI) * wLegAmp;
        float wArmR = Mathf.Sin(_phase + Mathf.PI + 0.35f) * wArmAmp;
        float wArmL = Mathf.Sin(_phase + 0.35f) * wArmAmp;
        float wKneeBend = (0.42f + norm * 0.5f) * Mathf.Rad2Deg;
        float wKneeL = Mathf.Max(0f, wLegL) * (wKneeBend / Mathf.Max(0.01f, wLegAmp));
        float wKneeR = Mathf.Max(0f, wLegR) * (wKneeBend / Mathf.Max(0.01f, wLegAmp));
        float wElbowBase = (0.35f + norm * 0.2f) * Mathf.Rad2Deg;
        float wElbowAmp = wArmAmp * 0.6f;
        float wElbowL = wElbowBase + Mathf.Max(0f, wArmL) * (wElbowAmp / Mathf.Max(0.01f, wArmAmp));
        float wElbowR = wElbowBase + Mathf.Max(0f, wArmR) * (wElbowAmp / Mathf.Max(0.01f, wArmAmp));

        // ── Run pose (exaggerated cartoon: big strides, high knees, wide pumping arms) ──
        float rEase = Mathf.Clamp01((norm - 0.45f) / 0.35f); // 0 at run start .. 1 at sprint
        float rLegAmp = (0.5f + rEase * 0.4f) * Mathf.Rad2Deg;
        float rArmAmp = (0.55f + rEase * 0.3f) * Mathf.Rad2Deg;
        float rLegL = Mathf.Sin(_phase) * rLegAmp;
        float rLegR = Mathf.Sin(_phase + Mathf.PI) * rLegAmp;
        float rArmR = Mathf.Sin(_phase + Mathf.PI + 0.2f) * rArmAmp;
        float rArmL = Mathf.Sin(_phase + 0.2f) * rArmAmp;
        float rKneeBend = (0.9f + rEase * 0.25f) * Mathf.Rad2Deg;
        float rKneeL = Mathf.Max(0f, rLegL) * (rKneeBend / Mathf.Max(0.01f, rLegAmp));
        float rKneeR = Mathf.Max(0f, rLegR) * (rKneeBend / Mathf.Max(0.01f, rLegAmp));
        float rElbowBase = (1.05f + rEase * 0.15f) * Mathf.Rad2Deg;
        float rElbowAmp = rArmAmp * 0.7f;
        float rElbowL = rElbowBase + Mathf.Max(0f, rArmL) * (rElbowAmp / Mathf.Max(0.01f, rArmAmp));
        float rElbowR = rElbowBase + Mathf.Max(0f, rArmR) * (rElbowAmp / Mathf.Max(0.01f, rArmAmp));

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
            if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(armR, 0f, 0f);
            if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(armL, 0f, 0f);
            if (_elbowR != null) _elbowR.localRotation = Quaternion.Euler(elbowR, 0f, 0f);
            if (_elbowL != null) _elbowL.localRotation = Quaternion.Euler(elbowL, 0f, 0f);
        }
        // When SuppressArms is set, a WeaponAnimator fully owns the shoulders AND elbows
        // (windup/strike/charge pose tracks), so nothing is written here mid-attack.

        // Cartoon run top body: forward lean, bounce on the torso and bobble the head.
        if (_torso != null)
            _torso.localRotation = Quaternion.Euler(-12f * runBlend, 0f, 0f);
        if (_head != null)
            _head.localRotation = Quaternion.Euler(-(3f + 5f * runBlend) * Mathf.Sin(_phase * 2f) - 4f * runBlend, 0f, 0f);
        if (_body != null && _bodyBasePos != default)
        {
            float bob = Mathf.Sin(_phase * 2f) * (0.03f * runBlend);
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