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

    private int _prevStyle;
    private float _switchTimer;
    private float _switchInterval;
    private float _transitionT;
    private const float TransitionDuration = 0.3f;

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
            DanceStyle = Random.Range(1, 9);

        _prevStyle = DanceStyle;
        _switchInterval = Random.Range(15f, 25f);
        _switchTimer = _switchInterval;
        _transitionT = 1f;
    }

    void LateUpdate()
    {
        if (!IsDancing) { ResetPose(); return; }

        _switchTimer -= Time.deltaTime;
        if (_switchTimer <= 0f)
        {
            _prevStyle = DanceStyle;
            int newStyle;
            do { newStyle = Random.Range(1, 9); } while (newStyle == DanceStyle);
            DanceStyle = newStyle;
            _transitionT = 0f;
            _switchTimer = Random.Range(15f, 25f);
        }

        if (_transitionT < 1f)
        {
            _transitionT += Time.deltaTime / TransitionDuration;
            if (_transitionT > 1f) _transitionT = 1f;
        }

        float blend = Mathf.SmoothStep(0f, 1f, _transitionT);
        if (blend < 1f)
        {
            ApplyPose(_prevStyle);
            BlendPose(DanceStyle, 1f - blend);
        }
        else
        {
            ApplyPose(DanceStyle);
        }
    }

    private void ApplyPose(int style)
    {
        switch (style)
        {
            case 1: DanceBounce(); break;
            case 2: DanceWave(); break;
            case 3: DanceTwist(); break;
            case 4: DanceGroove(); break;
            case 5: DanceShuffle(); break;
            case 6: DanceRobot(); break;
            case 7: DanceHeadbang(); break;
            case 8: DanceSalsa(); break;
            default: DanceBounce(); break;
        }
    }

    private void BlendPose(int targetStyle, float t)
    {
        float t1 = Time.time * 5f + Phase;
        float t2 = Time.time * 5f + Phase + 100f;
        Vector3 bodyPos = new Vector3(0f, Mathf.Lerp(GetBounce(targetStyle, t1), GetBounce(DanceStyle, t2), t), 0f);
        Quaternion bodyRot = Quaternion.Slerp(GetBodyRot(targetStyle, t1), GetBodyRot(DanceStyle, t2), t);
        Quaternion headRot = Quaternion.Slerp(GetHeadRot(targetStyle, t1), GetHeadRot(DanceStyle, t2), t);
        Quaternion hipLRot = Quaternion.Slerp(GetHipLRot(targetStyle, t1), GetHipLRot(DanceStyle, t2), t);
        Quaternion hipRRot = Quaternion.Slerp(GetHipRRot(targetStyle, t1), GetHipRRot(DanceStyle, t2), t);
        Quaternion shoulderLRot = Quaternion.Slerp(GetShoulderLRot(targetStyle, t1), GetShoulderLRot(DanceStyle, t2), t);
        Quaternion shoulderRRot = Quaternion.Slerp(GetShoulderRRot(targetStyle, t1), GetShoulderRRot(DanceStyle, t2), t);

        if (_body != null) { _body.localPosition = _body0Pos + bodyPos; _body.localRotation = bodyRot; }
        if (_head != null) _head.localRotation = headRot;
        if (_hipL != null) _hipL.localRotation = hipLRot;
        if (_hipR != null) _hipR.localRotation = hipRRot;
        if (_shoulderL != null) _shoulderL.localRotation = shoulderLRot;
        if (_shoulderR != null) _shoulderR.localRotation = shoulderRRot;

        SetMouthOpen(Mathf.Lerp(GetMouthOpen(targetStyle, t1), GetMouthOpen(DanceStyle, t2), t));
        SetEyeWide(Mathf.Lerp(GetEyeWide(targetStyle, t1), GetEyeWide(DanceStyle, t2), t));
        SetBrowEmotion(Mathf.Lerp(GetBrowVal(targetStyle, t1), GetBrowVal(DanceStyle, t2), t), 0f);
    }

    private float GetBounce(int s, float t)
    {
        switch (s)
        {
            case 1: return Mathf.Abs(Mathf.Sin(t * 7f)) * 0.12f;
            case 2: return Mathf.Abs(Mathf.Sin(t * 4.5f)) * 0.03f;
            case 3: return Mathf.Abs(Mathf.Sin(t * 18f)) * 0.06f;
            case 4: return Mathf.Abs(Mathf.Sin(t * 3.75f)) * 0.05f;
            case 5: return Mathf.Abs(Mathf.Sin(t * 8f)) * 0.04f;
            case 6: return (Mathf.PingPong(t * 4f, 1f) > 0.9f ? 0.06f : 0f);
            case 7: return Mathf.Abs(Mathf.Sin(t * 6f)) * 0.08f;
            case 8: return Mathf.Abs(Mathf.Sin(t * 5f)) * 0.04f;
            default: return 0f;
        }
    }

    private Quaternion GetBodyRot(int s, float t)
    {
        switch (s)
        {
            case 1: return Quaternion.Euler(0f, Mathf.Sin(t * 7f) * 15f, 0f);
            case 2: return Quaternion.Euler(Mathf.Sin(t * 1.8f) * 8f, Mathf.Sin(t * 3f) * 18f, Mathf.Sin(t * 1.2f) * 10f);
            case 3: return Quaternion.Euler(0f, Mathf.Sin(t * 9f) * 35f, 0f);
            case 4: return Quaternion.Euler(Mathf.Sin(t * 0.875f) * 12f, Mathf.Sin(t * 2.5f) * 10f, 0f);
            case 5: return Quaternion.Euler(0f, Mathf.Sin(t * 8f) * 20f, Mathf.Sin(t * 6f) * 8f);
            case 6: return Quaternion.Euler(0f, Mathf.RoundToInt(t * 4f) % 2 == 0 ? 25f : -25f, 0f);
            case 7: return Quaternion.Euler(Mathf.Sin(t * 6f) * 25f, 0f, Mathf.Sin(t * 4f) * 10f);
            case 8: return Quaternion.Euler(Mathf.Sin(t * 2.5f) * 6f, Mathf.Sin(t * 5f) * 15f, 0f);
            default: return Quaternion.identity;
        }
    }

    private Quaternion GetHeadRot(int s, float t)
    {
        switch (s)
        {
            case 1: return Quaternion.Euler(Mathf.Sin(t * 14f) * 14f, 0f, 0f);
            case 2: return Quaternion.Euler(Mathf.Sin(t * 1.5f) * 9f, Mathf.Sin(t * 1.5f) * 18f, 0f);
            case 3: return Quaternion.Euler(0f, -Mathf.Sin(t * 4.5f) * 17.5f, Mathf.Sin(t * 9f) * 18f);
            case 4: return Quaternion.Euler(Mathf.Sin(t * 3.75f) * 12f, Mathf.Sin(t * 2.5f) * 4f, 0f);
            case 5: return Quaternion.Euler(0f, Mathf.Sin(t * 8f) * 15f, Mathf.Sin(t * 6f) * 10f);
            case 6: return Quaternion.Euler(0f, Mathf.RoundToInt(t * 4f) % 2 == 0 ? 20f : -20f, 0f);
            case 7: return Quaternion.Euler(Mathf.Sin(t * 6f) * 30f, 0f, 0f);
            case 8: return Quaternion.Euler(Mathf.Sin(t * 2.5f) * 10f, Mathf.Sin(t * 5f) * 12f, 0f);
            default: return Quaternion.identity;
        }
    }

    private Quaternion GetHipLRot(int s, float t)
    {
        switch (s)
        {
            case 1: return Quaternion.Euler(Mathf.Sin(t * 7f) * 30f, 0f, 0f);
            case 2: return Quaternion.Euler(Mathf.Sin(t * 3f) * 8f, 0f, 0f);
            case 3: return Quaternion.Euler(Mathf.Abs(Mathf.Sin(t * 18f)) * 20f, 0f, 0f);
            case 4: return Quaternion.Euler(Mathf.Sin(t * 4.5f) * 12f, 0f, 0f);
            case 5: return Quaternion.Euler(Mathf.Sin(t * 8f) * 25f, 0f, 0f);
            case 6: return Quaternion.Euler(Mathf.RoundToInt(t * 4f) % 2 == 0 ? 15f : -10f, 0f, 0f);
            case 7: return Quaternion.Euler(Mathf.Sin(t * 6f) * 18f, 0f, 0f);
            case 8: return Quaternion.Euler(Mathf.Sin(t * 5f) * 20f, Mathf.Sin(t * 2.5f) * 10f, 0f);
            default: return Quaternion.identity;
        }
    }

    private Quaternion GetHipRRot(int s, float t)
    {
        switch (s)
        {
            case 1: return Quaternion.Euler(-Mathf.Sin(t * 7f) * 30f, 0f, 0f);
            case 2: return Quaternion.Euler(-Mathf.Sin(t * 3f) * 8f, 0f, 0f);
            case 3: return Quaternion.Euler(-Mathf.Abs(Mathf.Sin(t * 18f)) * 20f, 0f, 0f);
            case 4: return Quaternion.Euler(-Mathf.Sin(t * 4.5f) * 12f, 0f, 0f);
            case 5: return Quaternion.Euler(-Mathf.Sin(t * 8f) * 25f, 0f, 0f);
            case 6: return Quaternion.Euler(Mathf.RoundToInt(t * 4f) % 2 == 0 ? -10f : 15f, 0f, 0f);
            case 7: return Quaternion.Euler(-Mathf.Sin(t * 6f) * 18f, 0f, 0f);
            case 8: return Quaternion.Euler(-Mathf.Sin(t * 5f) * 20f, -Mathf.Sin(t * 2.5f) * 10f, 0f);
            default: return Quaternion.identity;
        }
    }

    private Quaternion GetShoulderLRot(int s, float t)
    {
        switch (s)
        {
            case 1: return Quaternion.Euler(Mathf.Sin(t * 7f + Mathf.PI) * 85f, 0f, 20f);
            case 2: return Quaternion.Euler(-Mathf.Sin(t * 2.4f) * 85f - 30f, 0f, Mathf.Cos(t * 2.4f) * 33f);
            case 3: return Quaternion.Euler(-80f + Mathf.Sin(t * 4.5f) * 40f, 0f, 80f);
            case 4: return Quaternion.Euler(-Mathf.Sin(t * 1.25f) * 50f - 80f, 0f, Mathf.Sin(t * 2.75f) * 33f);
            case 5: return Quaternion.Euler(Mathf.Sin(t * 8f) * 100f, 0f, Mathf.Sin(t * 6f) * 60f);
            case 6: return Quaternion.Euler(Mathf.RoundToInt(t * 4f) % 2 == 0 ? -130f : -20f, 0f, Mathf.RoundToInt(t * 4f) % 2 == 0 ? 60f : -40f);
            case 7: return Quaternion.Euler(Mathf.Sin(t * 6f) * 80f, 0f, 30f);
            case 8: return Quaternion.Euler(-Mathf.Sin(t * 5f) * 65f - 65f, 0f, Mathf.Sin(t * 2.5f) * 40f);
            default: return Quaternion.identity;
        }
    }

    private Quaternion GetShoulderRRot(int s, float t)
    {
        switch (s)
        {
            case 1: return Quaternion.Euler(-Mathf.Sin(t * 7f + Mathf.PI) * 85f, 0f, -20f);
            case 2: return Quaternion.Euler(-Mathf.Sin(t * 2.4f) * 59.5f - 21f, 0f, -Mathf.Cos(t * 2.4f) * 33f - 12f);
            case 3: return Quaternion.Euler(-80f - Mathf.Sin(t * 4.5f) * 40f, 0f, -80f);
            case 4: return Quaternion.Euler(-Mathf.Sin(t * 1.25f) * 30f - 48f, 0f, -Mathf.Sin(t * 2.75f) * 16.5f - 10f);
            case 5: return Quaternion.Euler(-Mathf.Sin(t * 8f) * 100f, 0f, -Mathf.Sin(t * 6f) * 60f);
            case 6: return Quaternion.Euler(Mathf.RoundToInt(t * 4f) % 2 == 0 ? -20f : -130f * 0.7f, 0f, Mathf.RoundToInt(t * 4f) % 2 == 0 ? -40f : 60f);
            case 7: return Quaternion.Euler(-Mathf.Sin(t * 6f) * 80f, 0f, -30f);
            case 8: return Quaternion.Euler(Mathf.Sin(t * 5f) * 32.5f + 32.5f, 0f, -Mathf.Sin(t * 2.5f) * 40f);
            default: return Quaternion.identity;
        }
    }

    private float GetMouthOpen(int s, float t)
    {
        switch (s)
        {
            case 1: return Mathf.Abs(Mathf.Sin(t * 14f)) * 0.8f;
            case 2: return Mathf.Max(0f, Mathf.Sin(t * 3.6f)) * 0.5f;
            case 3: return Mathf.Abs(Mathf.Sin(t * 27f)) * 0.8f;
            case 4: return Mathf.Max(0f, Mathf.Sin(t * 3f)) * 0.5f;
            case 5: return Mathf.Abs(Mathf.Sin(t * 16f)) * 0.6f;
            case 6: return Mathf.RoundToInt(t * 4f) % 3 == 0 ? 0.7f : 0.1f;
            case 7: return Mathf.Abs(Mathf.Sin(t * 12f)) * 0.9f;
            case 8: return Mathf.Max(0f, Mathf.Sin(t * 5f)) * 0.6f;
            default: return 0f;
        }
    }

    private float GetEyeWide(int s, float t)
    {
        switch (s)
        {
            case 1: return Mathf.Abs(Mathf.Sin(t * 21f)) * 0.6f;
            case 2: return Mathf.Sin(t * 2.1f) * 0.3f;
            case 3: return Mathf.PingPong(t * 9f, 2f) > 1.5f ? 0.8f : 0f;
            case 4: return Mathf.Sin(t * 2.5f) * 0.25f;
            case 5: return Mathf.Abs(Mathf.Sin(t * 12f)) * 0.5f;
            case 6: return Mathf.RoundToInt(t * 2f) % 2 == 0 ? 0.6f : 0f;
            case 7: return Mathf.Abs(Mathf.Sin(t * 10f)) * 0.7f;
            case 8: return Mathf.Sin(t * 5f) * 0.3f;
            default: return 0f;
        }
    }

    private float GetBrowVal(int s, float t)
    {
        switch (s)
        {
            case 1: return Mathf.Sin(t * 14f);
            case 2: return Mathf.Sin(t * 2.4f) * 0.5f;
            case 3: return Mathf.PingPong(t * 9f, 2f) > 1.5f ? 1f : -0.3f;
            case 4: return Mathf.Sin(t * 1.25f) * 0.4f;
            case 5: return Mathf.Sin(t * 12f) * 0.6f;
            case 6: return Mathf.RoundToInt(t * 2f) % 2 == 0 ? 0.8f : -0.2f;
            case 7: return Mathf.Sin(t * 10f) * 0.8f;
            case 8: return Mathf.Sin(t * 5f) * 0.5f;
            default: return 0f;
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

    // ── Style 1: Bounce — high-energy vertical hops, sharp arm pumps overhead, high leg kicks ──
    private void DanceBounce()
    {
        float t = Time.time * 7f + Phase;
        float bounce = Mathf.Abs(Mathf.Sin(t)) * 0.12f;
        float legKick = Mathf.Sin(t) * 30f;
        float armPump = Mathf.Sin(t + Mathf.PI) * 85f;
        float hip = Mathf.Sin(t * 0.7f) * 15f;
        float headBob = Mathf.Sin(t * 2f) * 14f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(0f, hip, 0f);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legKick, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-legKick, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(armPump, 0f, 20f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armPump, 0f, -20f);
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
        float armSweep = Mathf.Sin(t * 0.8f) * 85f + 30f;
        float armZ = Mathf.Cos(t * 0.8f) * 55f;
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
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-armSweep, 0f, armZ * 0.6f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armSweep * 0.7f, 0f, -armZ * 0.6f - 12f);
        if (_head != null) _head.localRotation = Quaternion.Euler(headRoll * 0.5f, headRoll, 0f);

        SetMouthOpen(Mathf.Max(0f, Mathf.Sin(t * 1.2f)) * 0.5f);
        SetEyeWide(Mathf.Sin(t * 0.7f) * 0.3f);
        SetBrowEmotion(Mathf.Sin(t * 0.8f) * 0.5f, 0f);
    }

    // ── Style 3: Twist — fast aggressive twist, arms in overhead T-pose, sharp knee bends ──
    private void DanceTwist()
    {
        float t = Time.time * 9f + Phase;
        float twist = Mathf.Sin(t) * 35f;
        float shoulderOpp = Mathf.Sin(t + Mathf.PI * 0.5f) * 40f;
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
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-80f + shoulderOpp, 0f, 80f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-80f - shoulderOpp, 0f, -80f);
        if (_head != null) _head.localRotation = Quaternion.Euler(0f, -twist * 0.5f, headTurn);

        SetMouthOpen(Mathf.Abs(Mathf.Sin(t * 3f)) * 0.8f);
        SetEyeWide(Mathf.PingPong(t, 2f) > 1.5f ? 0.8f : 0f);
        SetBrowEmotion(Mathf.PingPong(t, 2f) > 1.5f ? 1f : -0.3f, 0f);
    }

    // ── Style 4: Groove — slow deep lean, one arm disco point overhead, relaxed flowing ──
    private void DanceGroove()
    {
        float t = Time.time * 2.5f + Phase;
        float groove = Mathf.Sin(t) * 10f;
        float armRaise = Mathf.Sin(t * 0.5f) * 50f + 80f;
        float armSwing = Mathf.Sin(t * 1.1f) * 55f;
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

    // ── Style 5: Shuffle — fast side-to-side foot shuffle, arms crossed then snapping wide overhead ──
    private void DanceShuffle()
    {
        float t = Time.time * 8f + Phase;
        float shuffle = Mathf.Sin(t) * 20f;
        float armOpen = Mathf.Sin(t * 0.5f) > 0f ? 100f : -20f;
        float armCross = Mathf.Sin(t * 0.5f) > 0f ? 0f : 60f;
        float bounce = Mathf.Abs(Mathf.Sin(t * 2f)) * 0.04f;
        float bodyLean = Mathf.Sin(t * 2f) * 8f;
        float headTurn = Mathf.Sin(t * 4f) * 15f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(Mathf.Sin(t) * 0.03f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(0f, shuffle, bodyLean);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(Mathf.Sin(t) * 25f, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 25f, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-armOpen, 0f, armCross);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armOpen, 0f, -armCross);
        if (_head != null) _head.localRotation = Quaternion.Euler(0f, headTurn, Mathf.Sin(t * 2f) * 10f);

        SetMouthOpen(Mathf.Abs(Mathf.Sin(t * 2f)) * 0.6f);
        SetEyeWide(Mathf.Abs(Mathf.Sin(t * 4f)) * 0.5f);
        SetBrowEmotion(Mathf.Sin(t * 4f), 0f);
    }

    // ── Style 6: Robot — stiff mechanical movements, arms raised in lock positions ──
    private void DanceRobot()
    {
        float t = Time.time * 4f + Phase;
        float step = Mathf.RoundToInt(t) % 2;
        float bodyAngle = step * 25f - 12.5f;
        float armAngle = step * -130f;
        float headAngle = step * 20f - 10f;
        float bounce = step * 0.06f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(0f, bodyAngle, 0f);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(step * 15f, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-step * 10f, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(armAngle, 0f, 60f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(armAngle * 0.7f, 0f, -40f);
        if (_head != null) _head.localRotation = Quaternion.Euler(0f, headAngle, 0f);

        SetMouthOpen(step > 0 ? 0.7f : 0.1f);
        SetEyeWide(step > 0 ? 0.6f : 0f);
        SetBrowEmotion(step > 0 ? 0.8f : -0.2f, 0f);
    }

    // ── Style 7: Headbang — deep torso forward lean, aggressive head slamming, fists pumping overhead ──
    private void DanceHeadbang()
    {
        float t = Time.time * 6f + Phase;
        float headSlam = Mathf.Sin(t) * 30f;
        float torsoLean = Mathf.Abs(Mathf.Sin(t)) * 25f;
        float bounce = Mathf.Abs(Mathf.Sin(t * 2f)) * 0.08f;
        float fistPump = Mathf.Sin(t + Mathf.PI) * 80f;
        float hipSway = Mathf.Sin(t * 0.5f) * 10f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(torsoLean, hipSway, Mathf.Sin(t * 0.3f) * 10f);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(Mathf.Sin(t) * 18f, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 18f, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-fistPump, 0f, 30f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(fistPump * 0.8f, 0f, -30f);
        if (_head != null) _head.localRotation = Quaternion.Euler(headSlam, 0f, Mathf.Sin(t * 3f) * 8f);

        SetMouthOpen(Mathf.Abs(Mathf.Sin(t * 3f)) * 0.9f);
        SetEyeWide(Mathf.Abs(Mathf.Sin(t * 5f)) * 0.7f);
        SetBrowEmotion(Mathf.Sin(t * 3f), 0f);
    }

    // ── Style 8: Salsa — smooth hip circles, one arm raised overhead, quick step-touch footwork ──
    private void DanceSalsa()
    {
        float t = Time.time * 5f + Phase;
        float hipCircle = Mathf.Sin(t) * 15f;
        float hipCircleZ = Mathf.Cos(t) * 8f;
        float armRaise = Mathf.Sin(t * 0.5f) * 40f + 65f;
        float armSwing = Mathf.Sin(t * 2f) * 40f;
        float legStep = Mathf.Sin(t * 2f) * 20f;
        float bounce = Mathf.Abs(Mathf.Sin(t * 2.5f)) * 0.04f;
        float bodyLean = Mathf.Sin(t * 0.5f) * 6f;
        float headTilt = Mathf.Sin(t * 2.5f) * 10f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(bodyLean, hipCircle, hipCircleZ);
        }
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(legStep, Mathf.Sin(t) * 10f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-legStep, -Mathf.Sin(t) * 10f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-armRaise, 0f, armSwing * 0.6f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armRaise * 0.5f, 0f, -armSwing * 0.4f - 15f);
        if (_head != null) _head.localRotation = Quaternion.Euler(headTilt, Mathf.Sin(t * 2f) * 12f, 0f);

        SetMouthOpen(Mathf.Max(0f, Mathf.Sin(t * 2f)) * 0.6f);
        SetEyeWide(Mathf.Sin(t * 2.5f) * 0.3f);
        SetBrowEmotion(Mathf.Sin(t * 2f) * 0.5f, 0f);
    }
}
