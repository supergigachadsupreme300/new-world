using UnityEngine;

public class TornadoBehavior : MonoBehaviour
{
    public float DriftSpeed = 2f;
    public float DirectionChangeInterval = 5f;
    public float BaseRotateSpeed = 60f;
    public float RotateSpeedVariation = 45f;

    private Vector3 _driftDir;
    private float _dirTimer;
    private Transform[] _blocks;
    private float[] _rotateSpeeds;

    void Start()
    {
        int count = transform.childCount;
        _blocks = new Transform[count];
        _rotateSpeeds = new float[count];
        for (int i = 0; i < count; i++)
        {
            _blocks[i] = transform.GetChild(i);
            float t = count > 1 ? (float)i / (count - 1) : 0f;
            _rotateSpeeds[i] = BaseRotateSpeed + t * RotateSpeedVariation;
        }
        PickNewDirection();
    }

    void Update()
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            if (_blocks[i] != null)
                _blocks[i].Rotate(Vector3.up, _rotateSpeeds[i] * Time.deltaTime);
        }

        _dirTimer -= Time.deltaTime;
        if (_dirTimer <= 0f) PickNewDirection();

        transform.position += _driftDir * DriftSpeed * Time.deltaTime;
    }

    private void PickNewDirection()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        _driftDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        _dirTimer = DirectionChangeInterval + Random.Range(-1f, 1f);
    }
}
