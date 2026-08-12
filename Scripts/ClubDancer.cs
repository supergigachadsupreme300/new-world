using UnityEngine;

public class ClubDancer : MonoBehaviour
{
    public bool IsDancing;
    public float Phase;

    private Transform _legL;
    private Transform _legR;
    private Transform _armL;
    private Transform _armR;
    private Transform _body;
    private Quaternion _legL0;
    private Quaternion _legR0;
    private Quaternion _armL0;
    private Quaternion _armR0;
    private Quaternion _body0;

    void Awake()
    {
        _legL = transform.Find("LegL");
        _legR = transform.Find("LegR");
        _armL = transform.Find("ArmL");
        _armR = transform.Find("ArmR");
        _body = transform.Find("Body");
        _legL0 = _legL != null ? _legL.localRotation : Quaternion.identity;
        _legR0 = _legR != null ? _legR.localRotation : Quaternion.identity;
        _armL0 = _armL != null ? _armL.localRotation : Quaternion.identity;
        _armR0 = _armR != null ? _armR.localRotation : Quaternion.identity;
        _body0 = _body != null ? _body.localRotation : Quaternion.identity;
    }

    void LateUpdate()
    {
        if (!IsDancing)
        {
            if (_legL != null) _legL.localRotation = _legL0;
            if (_legR != null) _legR.localRotation = _legR0;
            if (_armL != null) _armL.localRotation = _armL0;
            if (_armR != null) _armR.localRotation = _armR0;
            if (_body != null) _body.localRotation = _body0;
            return;
        }

        float t = Time.time * 5f + Phase;
        float leg = Mathf.Sin(t) * 22f;
        float arm = Mathf.Sin(t + Mathf.PI) * 35f;
        float hip = Mathf.Sin(t * 0.5f) * 10f;
        float roll = Mathf.Sin(t * 2f) * 4f;

        if (_legL != null) _legL.localRotation = Quaternion.Euler(leg, 0f, 0f);
        if (_legR != null) _legR.localRotation = Quaternion.Euler(-leg, 0f, 0f);
        if (_armL != null) _armL.localRotation = Quaternion.Euler(arm, 0f, 0f);
        if (_armR != null) _armR.localRotation = Quaternion.Euler(-arm, 0f, 0f);
        if (_body != null) _body.localRotation = Quaternion.Euler(0f, hip, roll);
    }
}
