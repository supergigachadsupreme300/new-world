using UnityEngine;

public class FlappingFish : MonoBehaviour
{
    public string FishType = "fish_carp";
    public string FishLabel = "Cá";
    public float CalmTime = 15f;

    public bool IsPickable { get; private set; }
    public bool IsStunned { get; private set; }

    private enum FState { Flapping, Stunned, Calm }
    private FState _state = FState.Flapping;
    private float _timeAlive;
    private float _baseY;
    private Vector3 _spawnPos;
    private Vector3 _hopVel;
    private float _hopTimer;

    private const float Gravity = 6f;

    public void Initialize(string fishType, string fishLabel, Vector3 spawnPos)
    {
        FishType = fishType;
        FishLabel = fishLabel;
        _spawnPos = spawnPos;
        _baseY = spawnPos.y;
        _timeAlive = 0f;
        _state = FState.Flapping;
    }

    public void KnockOut()
    {
        if (IsStunned || IsPickable)
            return;

        IsStunned = true;
        _state = FState.Stunned;
        _hopVel = Vector3.zero;
        MakePickable();
        GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Đã gõ cá xỉu! Nhặt lên thôi."), 2f);
    }

    private void Update()
    {
        switch (_state)
        {
            case FState.Flapping: UpdateFlapping(); break;
            case FState.Stunned: UpdateStunned(); break;
            case FState.Calm: UpdateCalm(); break;
        }
    }

    private void UpdateFlapping()
    {
        _timeAlive += Time.deltaTime;
        if (_timeAlive >= CalmTime)
        {
            _state = FState.Calm;
            _hopVel = Vector3.zero;
            MakePickable();
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Cá đã nằm im, nhặt lên thôi!"), 2f);
            return;
        }

        _hopVel.y -= Gravity * Time.deltaTime;
        _hopTimer -= Time.deltaTime;
        if (_hopTimer <= 0f)
        {
            _hopTimer = Random.Range(0.25f, 0.5f);
            _hopVel.y = Random.Range(2.5f, 3.5f);
            _hopVel.x = Random.Range(-0.6f, 0.6f);
            _hopVel.z = Random.Range(-0.6f, 0.6f);
        }

        var p = transform.position + _hopVel * Time.deltaTime;

        Vector3 flat = p - _spawnPos;
        flat.y = 0f;
        if (flat.magnitude > 1.2f)
        {
            flat = flat.normalized * 1.2f;
            p = _spawnPos + new Vector3(flat.x, p.y, flat.z);
            _hopVel.x *= -0.5f;
            _hopVel.z *= -0.5f;
        }

        if (p.y <= _baseY)
        {
            p.y = _baseY;
            _hopVel.y = 0f;
        }

        transform.position = p;
        transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 20f) * 30f, 0f, Mathf.Cos(Time.time * 16f) * 20f);
    }

    private void UpdateStunned()
    {
        transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void UpdateCalm()
    {
        transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void MakePickable()
    {
        if (IsPickable)
            return;
        IsPickable = true;
        gameObject.name = "Pickup_" + FishType;
        transform.position = new Vector3(transform.position.x, _baseY, transform.position.z);
    }
}
