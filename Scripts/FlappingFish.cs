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

    private Transform _headTransform;
    private Transform[] _tailTransforms;

    public void Initialize(string fishType, string fishLabel, Vector3 spawnPos)
    {
        FishType = fishType;
        FishLabel = fishLabel;
        _spawnPos = spawnPos;
        _baseY = spawnPos.y;
        _timeAlive = 0f;
        _state = FState.Flapping;

        _headTransform = transform.childCount > 1 ? transform.GetChild(1) : null;
        _tailTransforms = new Transform[0];
        var tails = new System.Collections.Generic.List<Transform>();
        if (transform.childCount > 2) tails.Add(transform.GetChild(2));
        if (transform.childCount > 3) tails.Add(transform.GetChild(3));
        _tailTransforms = tails.ToArray();
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
        transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 8f) * 15f, 0f);

        float headWiggle = Mathf.Sin(Time.time * 12f) * 25f;
        if (_headTransform != null)
            _headTransform.localRotation = Quaternion.Euler(0f, headWiggle, 0f);
        for (int i = 0; i < _tailTransforms.Length; i++)
        {
            if (_tailTransforms[i] != null)
                _tailTransforms[i].localRotation = Quaternion.Euler(0f, -headWiggle * 0.7f, 0f);
        }
    }

    private void UpdateStunned()
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        if (_headTransform != null) _headTransform.localRotation = Quaternion.identity;
        for (int i = 0; i < _tailTransforms.Length; i++)
            if (_tailTransforms[i] != null) _tailTransforms[i].localRotation = Quaternion.identity;
    }

    private void UpdateCalm()
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        if (_headTransform != null) _headTransform.localRotation = Quaternion.identity;
        for (int i = 0; i < _tailTransforms.Length; i++)
            if (_tailTransforms[i] != null) _tailTransforms[i].localRotation = Quaternion.identity;
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
