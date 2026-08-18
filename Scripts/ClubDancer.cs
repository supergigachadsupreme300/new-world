using UnityEngine;

public class ClubDancer : MonoBehaviour
{
    public bool IsDancing;
    public float Phase;
    public int DanceStyle;

    private Transform _body, _head;
    private Transform _hipL, _hipR;
    private Transform _shoulderL, _shoulderR;
    private Transform _eyeL, _eyeR, _mouth, _hair;
    private Transform _browL, _browR;

    private Quaternion _body0, _head0, _hipL0, _hipR0, _shoulderL0, _shoulderR0;
    private Vector3 _body0Pos;
    private Color _mouthDefault;

    void Awake()
    {
        _body = transform.Find("Body");
        if (_body != null)
        {
            _head = _body.Find("Head");
            _hipL = _body.Find("HipL");
            _hipR = _body.Find("HipR");
            _shoulderL = _body.Find("ShoulderL");
            _shoulderR = _body.Find("ShoulderR");

            if (_head != null)
            {
                _eyeL = _head.Find("EyeL");
                _eyeR = _head.Find("EyeR");
                _mouth = _head.Find("Mouth");
                _hair = _head.Find("Hair");
                _browL = _head.Find("BrowL");
                _browR = _head.Find("BrowR");
            }
        }

        _body0 = _body != null ? _body.localRotation : Quaternion.identity;
        _body0Pos = _body != null ? _body.localPosition : Vector3.zero;
        _head0 = _head != null ? _head.localRotation : Quaternion.identity;
        _hipL0 = _hipL != null ? _hipL.localRotation : Quaternion.identity;
        _hipR0 = _hipR != null ? _hipR.localRotation : Quaternion.identity;
        _shoulderL0 = _shoulderL != null ? _shoulderL.localRotation : Quaternion.identity;
        _shoulderR0 = _shoulderR != null ? _shoulderR.localRotation : Quaternion.identity;

        if (_mouth != null)
        {
            var mr = _mouth.GetComponent<MeshRenderer>();
            if (mr != null) _mouthDefault = mr.material.color;
            else _mouthDefault = new Color(0.6f, 0.25f, 0.25f);
        }

        if (DanceStyle == 0)
            DanceStyle = Random.Range(1, 5);
    }

    void LateUpdate()
    {
        if (!IsDancing) { ResetPose(); return; }

        switch (DanceStyle)
        {
            case 1: DanceBounce(); break;
            case 2: DanceWave(); break;
            case 3: DanceTwist(); break;
            case 4: DanceGroove(); break;
            default: DanceBounce(); break;
        }
    }

    private void ResetPose()
    {
        if (_body != null) { _body.localRotation = _body0; _body.localPosition = _body0Pos; }
        if (_head != null) _head.localRotation = _head0;
        if (_hipL != null) _hipL.localRotation = _hipL0;
        if (_hipR != null) _hipR.localRotation = _hipR0;
        if (_shoulderL != null) _shoulderL.localRotation = _shoulderL0;
        if (_shoulderR != null) _shoulderR.localRotation = _shoulderR0;
        ResetFace();
    }

    private void ResetFace()
    {
        if (_mouth != null)
        {
            var mr = _mouth.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = _mouthDefault;
            _mouth.localScale = new Vector3(0.06f, 0.015f, 0.01f);
        }
        if (_eyeL != null) _eyeL.localScale = new Vector3(0.04f, 0.04f, 0.01f);
        if (_eyeR != null) _eyeR.localScale = new Vector3(0.04f, 0.04f, 0.01f);
        if (_browL != null) _browL.localRotation = Quaternion.identity;
        if (_browR != null) _browR.localRotation = Quaternion.identity;
    }

    private void SetMouthOpen(float amount)
    {
        if (_mouth == null) return;
        _mouth.localScale = new Vector3(0.06f, 0.015f + amount * 0.03f, 0.01f + amount * 0.005f);
        var mr = _mouth.GetComponent<MeshRenderer>();
        if (mr != null) mr.material.color = Color.Lerp(_mouthDefault, new Color(0.35f, 0.1f, 0.1f), amount);
    }

    private void SetEyeWide(float amount)
    {
        float w = 0.04f + amount * 0.015f;
        float h = 0.04f + amount * 0.02f;
        if (_eyeL != null) _eyeL.localScale = new Vector3(w, h, 0.01f);
        if (_eyeR != null) _eyeR.localScale = new Vector3(w, h, 0.01f);
    }

    private void SetBrowEmotion(float innerUp, float outerUp)
    {
        if (_browL != null) _browL.localRotation = Quaternion.Euler(0f, 0f, innerUp * 15f);
        if (_browR != null) _browR.localRotation = Quaternion.Euler(0f, 0f, -innerUp * 15f);
    }

    // ── Style 1: Bounce — high-energy vertical hops, sharp arm pumps, high leg kicks ──
    private void DanceBounce()
    {
        float t = Time.time * 7f + Phase;
        float bounce = Mathf.Abs(Mathf.Sin(t)) * 0.12f;
        float legKick = Mathf.Sin(t) * 30f;
        float armPump = Mathf.Sin(t + Mathf.PI) * 60f;
        float hip = Mathf.Sin(t * 0.7f) * 15f;
        float headBob = Mathf.Sin(t * 2f) * 14f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(0f, hip, 0f);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legKick, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-legKick, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(armPump, 0f, 15f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armPump, 0f, -15f);
        if (_head != null) _head.localRotation = Quaternion.Euler(headBob, 0f, 0f);

        SetMouthOpen(Mathf.Abs(Mathf.Sin(t * 2f)) * 0.8f);
        SetEyeWide(Mathf.Abs(Mathf.Sin(t * 3f)) * 0.6f);
        SetBrowEmotion(Mathf.Sin(t * 2f), 0f);
    }

    // ── Style 2: Wave — smooth wide sway, arms sweeping overhead, slow head rolls ──
    private void DanceWave()
    {
        float t = Time.time * 3f + Phase;
        float sway = Mathf.Sin(t) * 18f;
        float armSweep = Mathf.Sin(t * 0.8f) * 55f + 20f;
        float armZ = Mathf.Cos(t * 0.8f) * 35f;
        float legShift = Mathf.Sin(t) * 8f;
        float bounce = Mathf.Abs(Mathf.Sin(t * 1.5f)) * 0.03f;
        float headRoll = Mathf.Sin(t * 0.5f) * 18f;
        float bodyTilt = Mathf.Sin(t * 0.6f) * 8f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(bodyTilt, sway, Mathf.Sin(t * 0.4f) * 10f);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legShift, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-legShift, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-armSweep, 0f, armZ * 0.5f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armSweep * 0.7f, 0f, -armZ * 0.5f - 12f);
        if (_head != null) _head.localRotation = Quaternion.Euler(headRoll * 0.5f, headRoll, 0f);

        SetMouthOpen(Mathf.Max(0f, Mathf.Sin(t * 1.2f)) * 0.5f);
        SetEyeWide(Mathf.Sin(t * 0.7f) * 0.3f);
        SetBrowEmotion(Mathf.Sin(t * 0.8f) * 0.5f, 0f);
    }

    // ── Style 3: Twist — fast aggressive twist, arms in T-pose, sharp knee bends ──
    private void DanceTwist()
    {
        float t = Time.time * 9f + Phase;
        float twist = Mathf.Sin(t) * 35f;
        float shoulderOpp = Mathf.Sin(t + Mathf.PI * 0.5f) * 25f;
        float kneeBend = Mathf.Abs(Mathf.Sin(t * 2f)) * 20f;
        float bounce = Mathf.Abs(Mathf.Sin(t * 2f)) * 0.06f;
        float headTurn = Mathf.Sin(t * 0.6f) * 18f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(0f, twist, 0f);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(kneeBend, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-kneeBend, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-20f + shoulderOpp, 0f, 35f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-20f - shoulderOpp, 0f, -35f);
        if (_head != null) _head.localRotation = Quaternion.Euler(0f, -twist * 0.5f, headTurn);

        SetMouthOpen(Mathf.Abs(Mathf.Sin(t * 3f)) * 0.8f);
        SetEyeWide(Mathf.PingPong(t, 2f) > 1.5f ? 0.8f : 0f);
        SetBrowEmotion(Mathf.PingPong(t, 2f) > 1.5f ? 1f : -0.3f, 0f);
    }

    // ── Style 4: Groove — slow deep lean, one arm up disco point, relaxed flowing ──
    private void DanceGroove()
    {
        float t = Time.time * 2.5f + Phase;
        float groove = Mathf.Sin(t) * 10f;
        float armRaise = Mathf.Sin(t * 0.5f) * 30f + 40f;
        float armSwing = Mathf.Sin(t * 1.1f) * 35f;
        float legStep = Mathf.Sin(t * 1.8f) * 12f;
        float bounce = Mathf.Abs(Mathf.Sin(t * 1.5f)) * 0.05f;
        float bodyLean = Mathf.Sin(t * 0.35f) * 12f;
        float headNod = Mathf.Sin(t * 1.5f) * 12f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(bodyLean, groove, 0f);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legStep, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-legStep, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-armRaise, 0f, armSwing * 0.6f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armRaise * 0.6f, 0f, -armSwing * 0.3f - 10f);
        if (_head != null) _head.localRotation = Quaternion.Euler(headNod, groove * 0.4f, 0f);

        SetMouthOpen(Mathf.Max(0f, Mathf.Sin(t * 1.2f)) * 0.5f);
        SetEyeWide(Mathf.Sin(t) * 0.25f);
        SetBrowEmotion(Mathf.Sin(t * 0.5f) * 0.4f, 0f);
    }
}
