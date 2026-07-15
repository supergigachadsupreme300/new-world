using UnityEngine;
using System.Collections.Generic;

public class Mob : MonoBehaviour
{
    public enum MobType { Mouse, Crab }

    public MobType Type;
    public float MoveSpeed = 1f;
    public float WanderRange = 4f;
    public int Health = 10;

    private Vector3 _origin;
    private Vector3 _wanderTarget;
    private float _wanderTimer;
    private float _bobTimer;
    private GameObject _modelRoot;

    private void Start()
    {
        _origin = transform.position;
        PickWanderTarget();
        BuildModel();
    }

    private void Update()
    {
        if (_modelRoot == null) return;

        _bobTimer += Time.deltaTime * 2f;
        _modelRoot.transform.localPosition = new Vector3(0f, Mathf.Sin(_bobTimer) * 0.02f, 0f);

        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            PickWanderTarget();
        }

        Vector3 toTarget = _wanderTarget - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > 0.3f)
        {
            Vector3 dir = toTarget.normalized;
            transform.position += dir * MoveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
        }
        else
        {
            PickWanderTarget();
        }
    }

    private void PickWanderTarget()
    {
        _wanderTimer = Random.Range(2f, 5f);
        Vector2 r = Random.insideUnitCircle * WanderRange;
        _wanderTarget = _origin + new Vector3(r.x, 0f, r.y);
    }

    private void BuildModel()
    {
        _modelRoot = new GameObject("Model");
        _modelRoot.transform.SetParent(transform, false);

        switch (Type)
        {
            case MobType.Mouse: BuildMouse(); break;
            case MobType.Crab: BuildCrab(); break;
        }
    }

    private GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        go.GetComponent<Renderer>().material.color = color;
        if (removeCollider) Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private void BuildMouse()
    {
        Color furC = new Color(0.55f, 0.45f, 0.35f);
        Color darkC = new Color(0.35f, 0.28f, 0.2f);
        Color pinkC = new Color(0.9f, 0.6f, 0.6f);
        Color eyeC = new Color(0.05f, 0.05f, 0.05f);

        var body = MakeBlock("Body", _modelRoot.transform, new Vector3(0.3f, 0.18f, 0.5f), new Vector3(0f, 0.1f, 0f), furC);
        var head = MakeBlock("Head", _modelRoot.transform, new Vector3(0.2f, 0.15f, 0.15f), new Vector3(0f, 0.12f, 0.32f), furC);
        MakeBlock("Snout", _modelRoot.transform, new Vector3(0.1f, 0.08f, 0.08f), new Vector3(0f, 0.08f, 0.42f), pinkC);

        MakeBlock("EarL", _modelRoot.transform, new Vector3(0.08f, 0.02f, 0.06f), new Vector3(-0.12f, 0.2f, 0.3f), pinkC);
        MakeBlock("EarR", _modelRoot.transform, new Vector3(0.08f, 0.02f, 0.06f), new Vector3(0.12f, 0.2f, 0.3f), pinkC);

        MakeBlock("EyeL", _modelRoot.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.06f, 0.17f, 0.37f), eyeC);
        MakeBlock("EyeR", _modelRoot.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.06f, 0.17f, 0.37f), eyeC);

        MakeBlock("Tail", _modelRoot.transform, new Vector3(0.03f, 0.03f, 0.25f), new Vector3(0f, 0.05f, -0.37f), pinkC);

        for (int s = -1; s <= 1; s += 2)
        {
            MakeBlock("LegFL" + (s > 0 ? "R" : "L"), _modelRoot.transform, new Vector3(0.04f, 0.06f, 0.06f), new Vector3(s * 0.12f, 0.02f, 0.2f), darkC);
            MakeBlock("LegBL" + (s > 0 ? "R" : "L"), _modelRoot.transform, new Vector3(0.04f, 0.06f, 0.06f), new Vector3(s * 0.12f, 0.02f, -0.15f), darkC);
        }
    }

    private void BuildCrab()
    {
        Color shellC = new Color(0.8f, 0.3f, 0.15f);
        Color darkC = new Color(0.5f, 0.15f, 0.08f);
        Color eyeC = new Color(0.05f, 0.05f, 0.05f);

        var body = MakeBlock("Body", _modelRoot.transform, new Vector3(0.6f, 0.15f, 0.5f), new Vector3(0f, 0.08f, 0f), shellC);
        MakeBlock("Carapace", _modelRoot.transform, new Vector3(0.55f, 0.08f, 0.45f), new Vector3(0f, 0.16f, 0f), darkC);

        for (int s = -1; s <= 1; s += 2)
        {
            MakeBlock("Claw" + (s > 0 ? "R" : "L"), _modelRoot.transform, new Vector3(0.15f, 0.08f, 0.08f), new Vector3(s * 0.4f, 0.08f, 0.25f), shellC);
            MakeBlock("Pincer" + (s > 0 ? "R" : "L"), _modelRoot.transform, new Vector3(0.08f, 0.06f, 0.1f), new Vector3(s * 0.48f, 0.06f, 0.28f), darkC);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float s = side;
            for (int i = 0; i < 4; i++)
            {
                float z = -0.18f + i * 0.1f;
                float reach = 0.22f + i * 0.04f;
                float upperZ = z - 0.02f;
                MakeBlock("LegUpper" + (s > 0 ? "R" : "L") + i, _modelRoot.transform, new Vector3(reach, 0.05f, 0.04f),
                    new Vector3(s * (0.32f + reach * 0.5f), 0.06f, upperZ), darkC);
            }
        }

        MakeBlock("EyeStalkL", _modelRoot.transform, new Vector3(0.04f, 0.06f, 0.04f), new Vector3(-0.1f, 0.2f, 0.2f), darkC);
        MakeBlock("EyeStalkR", _modelRoot.transform, new Vector3(0.04f, 0.06f, 0.04f), new Vector3(0.1f, 0.2f, 0.2f), darkC);
        MakeBlock("EyeL", _modelRoot.transform, new Vector3(0.06f, 0.04f, 0.06f), new Vector3(-0.1f, 0.24f, 0.2f), eyeC);
        MakeBlock("EyeR", _modelRoot.transform, new Vector3(0.06f, 0.04f, 0.06f), new Vector3(0.1f, 0.24f, 0.2f), eyeC);

        var bodySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bodySphere.name = "CarapaceSphere";
        bodySphere.transform.SetParent(_modelRoot.transform, false);
        bodySphere.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        bodySphere.transform.localScale = new Vector3(0.25f, 0.12f, 0.25f);
        bodySphere.GetComponent<Renderer>().material.color = shellC;
        Object.Destroy(bodySphere.GetComponent<Collider>());
    }
}
