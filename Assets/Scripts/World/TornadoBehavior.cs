using System.Collections.Generic;
using UnityEngine;

public class TornadoBehavior : MonoBehaviour
{
    public float DriftSpeed = 2f;
    public float DirectionChangeInterval = 5f;
    public float BaseRotateSpeed = 8f;
    public float RotateSpeedVariation = 4f;
    public float PullRadius = 30f;
    public float PullForce = 8f;
    public float MaxPullSpeed = 20f;
    public float OrbitHeight = 80f;
    public float OrbitSpeed = 10f;

    private Vector3 _driftDir;
    private float _dirTimer;
    private Transform[] _blocks;
    private float[] _rotateSpeeds;

    private class OrbitingDebris
    {
        public GameObject Block;
        public float Angle;
        public float Radius;
        public float HeightOffset;
        public float OrbitSpeed;
        public float FloatSpeed;
        public float FloatAmplitude;
    }

    private class PulledObject
    {
        public Rigidbody Rb;
        public float Angle;
        public float Radius;
        public float HeightOffset;
        public float OrbitSpeed;
        public float FloatSpeed;
        public float FloatAmplitude;
        public float PullTimer;
    }

    private readonly List<OrbitingDebris> _debris = new List<OrbitingDebris>();
    private readonly List<PulledObject> _pulled = new List<PulledObject>();
    private static readonly Collider[] _overlapBuffer = new Collider[64];

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

        for (int i = _debris.Count - 1; i >= 0; i--)
        {
            var d = _debris[i];
            if (d.Block == null)
            {
                _debris.RemoveAt(i);
                continue;
            }
            d.Angle += d.OrbitSpeed * Time.deltaTime;
            float x = Mathf.Cos(d.Angle) * d.Radius;
            float z = Mathf.Sin(d.Angle) * d.Radius;
            float y = d.HeightOffset + Mathf.Sin(Time.time * d.FloatSpeed) * d.FloatAmplitude;
            d.Block.transform.localPosition = new Vector3(x, y, z);
        }

        for (int i = _pulled.Count - 1; i >= 0; i--)
        {
            var p = _pulled[i];
            if (p.Rb == null)
            {
                _pulled.RemoveAt(i);
                continue;
            }
            p.PullTimer -= Time.deltaTime;
            if (p.PullTimer <= 0f)
            {
                p.Rb.linearVelocity = Vector3.zero;
                p.Rb.useGravity = true;
                _pulled.RemoveAt(i);
                continue;
            }
            p.Angle += p.OrbitSpeed * Time.deltaTime;
            float x = Mathf.Cos(p.Angle) * p.Radius;
            float z = Mathf.Sin(p.Angle) * p.Radius;
            float y = p.HeightOffset + Mathf.Sin(Time.time * p.FloatSpeed) * p.FloatAmplitude;
            Vector3 targetPos = transform.position + new Vector3(x, y, z);
            Vector3 vel = (targetPos - p.Rb.position) * PullForce;
            if (vel.sqrMagnitude > MaxPullSpeed * MaxPullSpeed)
                vel = vel.normalized * MaxPullSpeed;
            p.Rb.linearVelocity = vel;
        }

        PullNearbyObjects();

        _dirTimer -= Time.deltaTime;
        if (_dirTimer <= 0f) PickNewDirection();

        transform.position += _driftDir * DriftSpeed * Time.deltaTime;
    }

    private void PullNearbyObjects()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, PullRadius, _overlapBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var rb = _overlapBuffer[i].attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;

            bool alreadyPulled = false;
            for (int j = 0; j < _pulled.Count; j++)
            {
                if (_pulled[j].Rb == rb) { alreadyPulled = true; break; }
            }
            if (alreadyPulled) continue;

            rb.useGravity = false;
            _pulled.Add(new PulledObject
            {
                Rb = rb,
                Angle = Random.Range(0f, Mathf.PI * 2f),
                Radius = Random.Range(2f, PullRadius * 0.6f),
                HeightOffset = Random.Range(2f, OrbitHeight),
                OrbitSpeed = Random.Range(BaseRotateSpeed, BaseRotateSpeed + RotateSpeedVariation),
                FloatSpeed = Random.Range(1f, 3f),
                FloatAmplitude = Random.Range(0.5f, 1.5f),
                PullTimer = 8f
            });
        }
    }

    public void AddDebrisBlock(Vector3 scale, Color color)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "TornadoDebris";
        block.transform.SetParent(transform);
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().material.color = color;
        Object.Destroy(block.GetComponent<Collider>());

        _debris.Add(new OrbitingDebris
        {
            Block = block,
            Angle = Random.Range(0f, Mathf.PI * 2f),
            Radius = Random.Range(3f, 8f),
            HeightOffset = Random.Range(0f, 80f),
            OrbitSpeed = Random.Range(BaseRotateSpeed, BaseRotateSpeed + RotateSpeedVariation),
            FloatSpeed = Random.Range(1f, 3f),
            FloatAmplitude = Random.Range(0.5f, 2f)
        });
    }

    public void AddBuildingPartDebris(Vector3 localPos, Quaternion localRot, Vector3 localScale, Color color)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "BuildingDebris";
        block.transform.SetParent(transform);
        block.transform.localPosition = localPos;
        block.transform.localRotation = localRot;
        block.transform.localScale = localScale;
        block.GetComponent<Renderer>().material.color = color;
        Object.Destroy(block.GetComponent<Collider>());

        _debris.Add(new OrbitingDebris
        {
            Block = block,
            Angle = Random.Range(0f, Mathf.PI * 2f),
            Radius = Random.Range(3f, 8f),
            HeightOffset = Random.Range(2f, 40f),
            OrbitSpeed = Random.Range(BaseRotateSpeed, BaseRotateSpeed + RotateSpeedVariation),
            FloatSpeed = Random.Range(1f, 3f),
            FloatAmplitude = Random.Range(0.5f, 2f)
        });
    }

    void OnDestroy()
    {
        for (int i = 0; i < _pulled.Count; i++)
        {
            if (_pulled[i].Rb != null)
            {
                _pulled[i].Rb.linearVelocity = Vector3.zero;
                _pulled[i].Rb.useGravity = true;
            }
        }
        _pulled.Clear();
    }

    private void PickNewDirection()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        _driftDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        _dirTimer = DirectionChangeInterval + Random.Range(-1f, 1f);
    }
}
