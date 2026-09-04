using UnityEngine;

/// <summary>
/// Idle bob + sway for background club patrons so they read as alive
/// instead of mannequins standing at the door.
/// </summary>
public class ClubPatronAnimator : MonoBehaviour
{
    private Transform _body;
    private float _phase;
    private float _rate;

    void Start()
    {
        _body = transform.Find("Body");
        _phase = Random.value * 6.28f;
        _rate = 1.2f + Random.value * 1.2f;
    }

    void Update()
    {
        if (_body == null)
            return;
        float bob = Mathf.Sin(Time.time * _rate + _phase) * 0.02f;
        _body.localPosition = new Vector3(0f, 0.35f + bob, 0f);
        _body.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.6f + _phase) * 8f, 0f);
    }
}