using UnityEngine;

public class CloudBehavior : MonoBehaviour
{
    public float DriftSpeed = 0.5f;
    public float Lifetime = 90f;

    private Vector3 _driftDir;
    private float _dirTimer;
    private float _age;

    void Start()
    {
        _dirTimer = Random.Range(8f, 12f);
        PickNewDirection();
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        _dirTimer -= Time.deltaTime;
        if (_dirTimer <= 0f)
        {
            PickNewDirection();
            _dirTimer = Random.Range(8f, 12f);
        }

        transform.position += _driftDir * DriftSpeed * Time.deltaTime;
    }

    private void PickNewDirection()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        _driftDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }
}
