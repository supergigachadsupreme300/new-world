using UnityEngine;

public class ClubDJAnimator : MonoBehaviour
{
    public bool IsPlaying;
    public float Phase;
    public int Style;

    private Transform _body, _head;
    private Transform _hipL, _hipR;
    private Transform _shoulderL, _shoulderR;

    private Quaternion _body0, _head0, _hipL0, _hipR0, _shoulderL0, _shoulderR0;
    private Vector3 _body0Pos;

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
        }

        _body0 = _body != null ? _body.localRotation : Quaternion.identity;
        _body0Pos = _body != null ? _body.localPosition : Vector3.zero;
        _head0 = _head != null ? _head.localRotation : Quaternion.identity;
        _hipL0 = _hipL != null ? _hipL.localRotation : Quaternion.identity;
        _hipR0 = _hipR != null ? _hipR.localRotation : Quaternion.identity;
        _shoulderL0 = _shoulderL != null ? _shoulderL.localRotation : Quaternion.identity;
        _shoulderR0 = _shoulderR != null ? _shoulderR.localRotation : Quaternion.identity;

        if (Style == 0)
            Style = Random.Range(1, 5);
    }

    void LateUpdate()
    {
        if (!IsPlaying) { ResetPose(); return; }

        switch (Style)
        {
            case 1: DJMix(); break;
            case 2: DJHype(); break;
            case 3: DJGroove(); break;
            case 4: DJScratch(); break;
            default: DJMix(); break;
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
    }

    // ── Style 1: Mixing — left hand on turntable, right hand on mixer ──
    private void DJMix()
    {
        float t = Time.time * 5f + Phase;
        float bob = Mathf.Sin(t * 1.5f) * 3f;
        float headNod = Mathf.Sin(t * 2f) * 5f;
        // Left arm extended forward-down onto turntable
        float lArmX = -8f + Mathf.Sin(t * 0.8f) * 4f;
        float lArmZ = 20f + Mathf.Sin(t * 1.2f) * 5f;
        // Right arm reaching toward mixer center
        float rArmX = -25f + Mathf.Sin(t * 0.6f) * 8f;
        float rArmY = -5f + Mathf.Sin(t * 1.8f) * 3f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, Mathf.Abs(Mathf.Sin(t)) * 0.01f, 0f);
            _body.localRotation = Quaternion.Euler(0f, bob, 0f);
        }
        if (_head != null) _head.localRotation = Quaternion.Euler(headNod, 0f, 0f);
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 5f, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 0.7f) * 5f, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(lArmX, 0f, lArmZ);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(rArmX, rArmY, -15f);
    }

    // ── Style 2: Hype — both hands up, body sway ──
    private void DJHype()
    {
        float t = Time.time * 4f + Phase;
        float sway = Mathf.Sin(t) * 8f;
        float armRaise = Mathf.Sin(t * 0.8f) * 30f + 40f;
        float armWave = Mathf.Sin(t * 1.5f) * 20f;
        float headBob = Mathf.Sin(t * 2f) * 8f;
        float bounce = Mathf.Abs(Mathf.Sin(t * 2f)) * 0.03f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, bounce, 0f);
            _body.localRotation = Quaternion.Euler(0f, sway, Mathf.Sin(t * 0.5f) * 4f);
        }
        if (_head != null) _head.localRotation = Quaternion.Euler(headBob, sway * 0.3f, 0f);
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(Mathf.Sin(t) * 8f, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 8f, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-armRaise, 0f, armWave * 0.5f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(-armRaise * 0.9f, 0f, -armWave * 0.5f);
    }

    // ── Style 3: Groove — slow lean, one hand on turntable ──
    private void DJGroove()
    {
        float t = Time.time * 3f + Phase;
        float lean = Mathf.Sin(t) * 5f;
        float headTilt = Mathf.Sin(t * 0.7f) * 6f;
        // Left hand steady on turntable, right hand adjusts knobs
        float rArmX = -30f + Mathf.Sin(t * 1.3f) * 10f;
        float rArmZ = -5f + Mathf.Sin(t * 0.9f) * 8f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 1.5f)) * 0.015f, 0f);
            _body.localRotation = Quaternion.Euler(lean, 0f, Mathf.Sin(t * 0.4f) * 3f);
        }
        if (_head != null) _head.localRotation = Quaternion.Euler(0f, headTilt, Mathf.Sin(t * 0.6f) * 4f);
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.5f) * 4f, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 0.5f) * 4f, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(-10f, 0f, 25f);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(rArmX, rArmZ, -10f);
    }

    // ── Style 4: Scratch — one arm scratching, body twist ──
    private void DJScratch()
    {
        float t = Time.time * 6f + Phase;
        float twist = Mathf.Sin(t) * 10f;
        float scratch = Mathf.Sin(t * 2f) * 15f;
        float headBop = Mathf.Sin(t * 3f) * 6f;
        // Left arm scratch motion (back and forth)
        float lArmZ = 30f + scratch;
        float lArmX = -5f + Mathf.Sin(t * 1.5f) * 8f;
        // Right arm on mixer
        float rArmX = -20f + Mathf.Sin(t * 0.8f) * 6f;

        if (_body != null)
        {
            _body.localPosition = _body0Pos + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 2f)) * 0.02f, 0f);
            _body.localRotation = Quaternion.Euler(0f, twist, 0f);
        }
        if (_head != null) _head.localRotation = Quaternion.Euler(headBop, -twist * 0.5f, 0f);
        if (_hipL != null) _hipL.localRotation = Quaternion.Euler(Mathf.Sin(t * 2f) * 6f, 0f, 0f);
        if (_hipR != null) _hipR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 2f) * 6f, 0f, 0f);
        if (_shoulderL != null) _shoulderL.localRotation = Quaternion.Euler(lArmX, 0f, lArmZ);
        if (_shoulderR != null) _shoulderR.localRotation = Quaternion.Euler(rArmX, 0f, -12f);
    }
}
