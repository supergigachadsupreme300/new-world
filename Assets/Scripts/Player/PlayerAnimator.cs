using UnityEngine;

/// <summary>
/// Procedural blocky walk/run/idle animation for the MapBuilder player model.
///
/// Sits on the "PlayerModel" root (built by <see cref="MapBuilder.BuildPlayerModel"/>).
/// The standing model is authored with shoulder + hip pivots (ShoulderL/R, HipL/R) and a
/// "Body" block; this component swings those pivots sinusoidally while the player moves.
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
    private Transform _hipL;
    private Transform _hipR;
    private Transform _body;
    private Vector3 _bodyBasePos;

    private Vector3 _lastRootPos;
    private float _phase;
    private float _time;

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
        _hipL = FindChild("HipL");
        _hipR = FindChild("HipR");
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

        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legL, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(legR, 0f, 0f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(armR, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(armL, 0f, 0f);

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
        SetLerped(_shoulderL, idle, blend);
        SetLerped(_shoulderR, idle, blend);
        SetLerped(_hipL, idle, blend);
        SetLerped(_hipR, idle, blend);
        if (_body != null && _bodyBasePos != default)
            _body.localPosition = Vector3.Lerp(_body.localPosition, _bodyBasePos, 0.2f);
    }

    private static void SetLerped(Transform t, Vector3 rot, float blend)
    {
        if (t == null) return;
        t.localRotation = Quaternion.Euler(rot);
    }
}