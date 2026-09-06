using UnityEngine;

/// <summary>
/// Procedural blocky walk/run/idle animation for the MapBuilder player model.
///
/// Sits on the "PlayerModel" root (built by <see cref="MapBuilder.BuildPlayerModel"/>).
/// The standing model is authored with shoulder -> elbow and hip -> knee pivots
/// (ShoulderL/R, ElbowL/R, HipL/R, KneeL/R) and a "Body" block; this component swings
/// those pivots sinusoidally while the player moves.
/// Amplitude and cadence scale with horizontal speed so walking differs from running.
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
    private Vector3 _bodyBasePos;

    /// <summary>Left shoulder pivot (null when the model has no arms). Used by WeaponAnimator.</summary>
    public Transform ShoulderL => _shoulderL;

    /// <summary>Right shoulder pivot (null when the model has no arms). Used by WeaponAnimator.</summary>
    public Transform ShoulderR => _shoulderR;

    private Vector3 _lastRootPos;
    private float _phase;
    private float _time;

    /// <summary>
    /// When true, the shoulder/arm pivots are left alone — a <see cref="WeaponAnimator"/> is
    /// driving them during an attack. Set while attacking, cleared on recovery.
    /// </summary>
    public bool SuppressArms;

    private void OnEnable()
    {
        _pc = GetComponentInParent<PlayerController>();

        Transform FindChild(string name)
        {
            var t = transform.Find(name);
            return t;
        }

        _shoulderL = FindChild("ShoulderL");
        _shoulderR = FindChild("ShoulderR");
        _elbowL = _shoulderL != null ? _shoulderL.Find("ElbowL") : null;
        _elbowR = _shoulderR != null ? _shoulderR.Find("ElbowR") : null;
        _hipL = FindChild("HipL");
        _hipR = FindChild("HipR");
        _kneeL = _hipL != null ? _hipL.Find("KneeL") : null;
        _kneeR = _hipR != null ? _hipR.Find("KneeR") : null;
        _body = FindChild("Body");
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

        float cadence = 1.8f + norm * 1.8f; // Hz
        _phase += cadence * Mathf.PI * 2f * Time.deltaTime;

        float legAmp = (0.32f + norm * 0.3f) * Mathf.Rad2Deg;
        float armAmp = (0.3f + norm * 0.35f) * Mathf.Rad2Deg;

        float legL = Mathf.Sin(_phase) * legAmp;
        float legR = Mathf.Sin(_phase + Mathf.PI) * legAmp;
        float armR = Mathf.Sin(_phase + Mathf.PI + 0.35f) * armAmp;
        float armL = Mathf.Sin(_phase + 0.35f) * armAmp;

        // Knee juts when the thigh swings forward (natural gait) and more at speed.
        float kneeBend = (0.42f + norm * 0.5f) * Mathf.Rad2Deg;
        float kneeL = Mathf.Max(0f, legL) * (kneeBend / Mathf.Max(0.01f, legAmp));
        float kneeR = Mathf.Max(0f, legR) * (kneeBend / Mathf.Max(0.01f, legAmp));

        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legL, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(legR, 0f, 0f);
        if (_kneeL != null) _kneeL.localRotation = Quaternion.Euler(kneeL, 0f, 0f);
        if (_kneeR != null) _kneeR.localRotation = Quaternion.Euler(kneeR, 0f, 0f);

        // Elbow stays flexed while in motion and curls a bit more as the arm swings forward.
        float elbowBase = (0.35f + norm * 0.2f) * Mathf.Rad2Deg;
        float elbowAmp = armAmp * 0.6f;
        float elbowL = elbowBase + Mathf.Max(0f, armL) * (elbowAmp / Mathf.Max(0.01f, armAmp));
        float elbowR = elbowBase + Mathf.Max(0f, armR) * (elbowAmp / Mathf.Max(0.01f, armAmp));

        if (!SuppressArms)
        {
            if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(armR, 0f, 0f);
            if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(armL, 0f, 0f);
            if (_elbowR != null) _elbowR.localRotation = Quaternion.Euler(elbowR, 0f, 0f);
            if (_elbowL != null) _elbowL.localRotation = Quaternion.Euler(elbowL, 0f, 0f);
        }
        else
        {
            // Attack: keep the arm straight so the weapon swing reads from the shoulder only.
            if (_elbowR != null) _elbowR.localRotation = Quaternion.identity;
            if (_elbowL != null) _elbowL.localRotation = Quaternion.identity;
        }

        if (_body != null && _bodyBasePos != default)
            _body.localPosition = _bodyBasePos;
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
        if (_body != null && _bodyBasePos != default)
            _body.localPosition = Vector3.Lerp(_body.localPosition, _bodyBasePos, 0.2f);
    }

    private static void SetLerped(Transform t, Vector3 rot, float blend)
    {
        if (t == null) return;
        t.localRotation = Quaternion.Euler(rot);
    }
}