using System;
using System.Collections;
using UnityEngine;

public class FlyingCrane : MonoBehaviour
{
    private Livestock.AnimalType _animalType;
    private Vector3 _dropTarget;
    private Action<Livestock> _onLanded;
    private Transform _body;
    private Transform _wingL;
    private Transform _wingR;
    private Transform _wingFarL;
    private Transform _wingFarR;
    private Transform _cagePivot;
    private Transform _cageModel;
    private float _flySpeed = 20f;
    private float _flyHeight = 45f;
    private int _phase;
    private float _spawnTime;
    private bool _cageDropped;

    public void Setup(Livestock.AnimalType type, Vector3 dropTarget, Action<Livestock> onLanded)
    {
        _animalType = type;
        _dropTarget = dropTarget;
        _onLanded = onLanded;
        _phase = 0;
        _cageDropped = false;
        _spawnTime = Time.time;
        transform.localScale = Vector3.one;

        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.enabled = true;
            var c = r.material.color;
            c.a = 1f;
            r.material.color = c;
        }

        BuildModel();

        Vector3 entryDir = (_dropTarget - new Vector3(0f, 0f, 0f)).normalized;
        entryDir.y = 0f;
        entryDir = -entryDir;
        transform.position = _dropTarget + entryDir * 280f + Vector3.up * _flyHeight;
        transform.rotation = Quaternion.LookRotation(-entryDir);

        gameObject.SetActive(true);
    }

    private void BuildModel()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        Color white = new Color(0.95f, 0.96f, 0.97f);
        Color lightGray = new Color(0.85f, 0.87f, 0.89f);
        Color midGray = new Color(0.7f, 0.73f, 0.76f);
        Color darkGray = new Color(0.45f, 0.48f, 0.52f);
        Color black = new Color(0.08f, 0.08f, 0.08f);
        Color darkTip = new Color(0.15f, 0.15f, 0.15f);
        Color redCrown = new Color(0.85f, 0.1f, 0.08f);
        Color beakCol = new Color(0.2f, 0.18f, 0.1f);
        Color beakTip = new Color(0.15f, 0.13f, 0.08f);
        Color legCol = new Color(0.55f, 0.55f, 0.5f);
        Color eyeCol = new Color(0.05f, 0.05f, 0.05f);

        BuildBody(white, lightGray, midGray, darkGray, black, redCrown, beakCol, beakTip, legCol, eyeCol);
        BuildWings(white, lightGray, midGray, darkGray, darkTip);
        BuildNeckAndHead(white, lightGray, redCrown, beakCol, beakTip, eyeCol);
        BuildTailFeathers(white, lightGray, midGray, darkGray, darkTip);
        BuildLegs(legCol);
        BuildCage();
    }

    private void BuildBody(Color white, Color lightGray, Color midGray, Color darkGray, Color black, Color redCrown, Color beakCol, Color beakTip, Color legCol, Color eyeCol)
    {
        _body = MakeCube("Body", transform, new Vector3(0.55f, 0.4f, 1.4f), Vector3.zero, white);

        MakeCube("BodyTop", _body, new Vector3(0.48f, 0.08f, 1.2f), new Vector3(0f, 0.18f, 0f), lightGray);
        MakeCube("BodyBelly", _body, new Vector3(0.44f, 0.06f, 1.0f), new Vector3(0f, -0.18f, 0.05f), white);
        MakeCube("BodySideL", _body, new Vector3(0.05f, 0.2f, 1.1f), new Vector3(-0.27f, -0.02f, 0f), lightGray);
        MakeCube("BodySideR", _body, new Vector3(0.05f, 0.2f, 1.1f), new Vector3(0.27f, -0.02f, 0f), lightGray);

        MakeCube("Chest", _body, new Vector3(0.42f, 0.35f, 0.3f), new Vector3(0f, -0.05f, 0.5f), white);
        MakeCube("Rump", _body, new Vector3(0.38f, 0.3f, 0.25f), new Vector3(0f, 0.02f, -0.55f), lightGray);

        for (int i = 0; i < 5; i++)
        {
            float z = 0.35f - i * 0.18f;
            float shade = Mathf.Lerp(0.95f, 0.82f, i / 4f);
            Color c = new Color(shade, shade + 0.01f, shade + 0.02f);
            MakeCube("BodyFeather" + i, _body, new Vector3(0.5f - i * 0.02f, 0.04f, 0.15f), new Vector3(0f, 0.16f, z), c);
        }

        MakeCube("BodyFeatherBelly0", _body, new Vector3(0.36f, 0.03f, 0.2f), new Vector3(0f, -0.2f, 0.25f), white);
        MakeCube("BodyFeatherBelly1", _body, new Vector3(0.32f, 0.03f, 0.2f), new Vector3(0f, -0.2f, 0.05f), white);
        MakeCube("BodyFeatherBelly2", _body, new Vector3(0.28f, 0.03f, 0.2f), new Vector3(0f, -0.2f, -0.15f), lightGray);
    }

    private void BuildWings(Color white, Color lightGray, Color midGray, Color darkGray, Color darkTip)
    {
        Transform wingRootL = MakeCube("WingRootL", _body, new Vector3(0.08f, 0.06f, 0.4f), new Vector3(-0.3f, 0.08f, 0.05f), white);
        _wingL = MakeCube("WingInnerL", wingRootL, new Vector3(0.06f, 0.04f, 0.7f), new Vector3(-0.35f, 0.04f, -0.1f), white);
        Transform midL = MakeCube("WingMidL", _wingL, new Vector3(0.05f, 0.03f, 0.6f), new Vector3(-0.35f, 0.01f, -0.05f), lightGray);
        _wingFarL = MakeCube("WingOuterL", midL, new Vector3(0.04f, 0.02f, 0.5f), new Vector3(-0.3f, 0f, -0.05f), midGray);
        Transform tipL = MakeCube("WingTipL", _wingFarL, new Vector3(0.035f, 0.015f, 0.35f), new Vector3(-0.2f, -0.005f, -0.05f), darkGray);
        MakeCube("WingTipEndL", tipL, new Vector3(0.03f, 0.01f, 0.2f), new Vector3(-0.12f, -0.005f, -0.02f), darkTip);

        for (int i = 0; i < 4; i++)
        {
            float shade = Mathf.Lerp(0.93f, 0.78f, i / 3f);
            Color c = new Color(shade, shade + 0.01f, shade + 0.02f);
            MakeCube("FeatherL" + i, _wingL, new Vector3(0.04f, 0.008f, 0.12f), new Vector3(-0.28f - i * 0.06f, 0.02f, -0.15f - i * 0.1f), c);
        }

        Transform wingRootR = MakeCube("WingRootR", _body, new Vector3(0.08f, 0.06f, 0.4f), new Vector3(0.3f, 0.08f, 0.05f), white);
        _wingR = MakeCube("WingInnerR", wingRootR, new Vector3(0.06f, 0.04f, 0.7f), new Vector3(0.35f, 0.04f, -0.1f), white);
        Transform midR = MakeCube("WingMidR", _wingR, new Vector3(0.05f, 0.03f, 0.6f), new Vector3(0.35f, 0.01f, -0.05f), lightGray);
        _wingFarR = MakeCube("WingOuterR", midR, new Vector3(0.04f, 0.02f, 0.5f), new Vector3(0.3f, 0f, -0.05f), midGray);
        Transform tipR = MakeCube("WingTipR", _wingFarR, new Vector3(0.035f, 0.015f, 0.35f), new Vector3(0.2f, -0.005f, -0.05f), darkGray);
        MakeCube("WingTipEndR", tipR, new Vector3(0.03f, 0.01f, 0.2f), new Vector3(0.12f, -0.005f, -0.02f), darkTip);

        for (int i = 0; i < 4; i++)
        {
            float shade = Mathf.Lerp(0.93f, 0.78f, i / 3f);
            Color c = new Color(shade, shade + 0.01f, shade + 0.02f);
            MakeCube("FeatherR" + i, _wingR, new Vector3(0.04f, 0.008f, 0.12f), new Vector3(0.28f + i * 0.06f, 0.02f, -0.15f - i * 0.1f), c);
        }

        MakeCube("ShoulderL", _body, new Vector3(0.08f, 0.08f, 0.25f), new Vector3(-0.28f, 0.1f, 0.15f), white);
        MakeCube("ShoulderR", _body, new Vector3(0.08f, 0.08f, 0.25f), new Vector3(0.28f, 0.1f, 0.15f), white);
    }

    private void BuildNeckAndHead(Color white, Color lightGray, Color redCrown, Color beakCol, Color beakTip, Color eyeCol)
    {
        Transform neckBase = MakeCube("NeckBase", _body, new Vector3(0.14f, 0.14f, 0.2f), new Vector3(0f, 0.12f, 0.7f), white);
        Transform neck1 = MakeCube("Neck1", neckBase, new Vector3(0.11f, 0.11f, 0.3f), new Vector3(0f, 0.1f, 0.2f), white);
        Transform neck2 = MakeCube("Neck2", neck1, new Vector3(0.09f, 0.09f, 0.35f), new Vector3(0f, 0.08f, 0.22f), lightGray);
        Transform neck3 = MakeCube("Neck3", neck2, new Vector3(0.08f, 0.08f, 0.3f), new Vector3(0f, 0.06f, 0.2f), white);
        Transform neckTop = MakeCube("NeckTop", neck3, new Vector3(0.07f, 0.07f, 0.25f), new Vector3(0f, 0.05f, 0.18f), white);

        for (int i = 0; i < 4; i++)
        {
            float shade = Mathf.Lerp(0.92f, 0.84f, i / 3f);
            Color c = new Color(shade, shade + 0.01f, shade + 0.02f);
            MakeCube("NeckFeatherL" + i, neck1, new Vector3(0.008f, 0.06f, 0.06f), new Vector3(-0.05f - i * 0.005f, 0.02f, 0.05f + i * 0.08f), c);
            MakeCube("NeckFeatherR" + i, neck1, new Vector3(0.008f, 0.06f, 0.06f), new Vector3(0.05f + i * 0.005f, 0.02f, 0.05f + i * 0.08f), c);
        }

        Transform head = MakeCube("Head", neckTop, new Vector3(0.16f, 0.14f, 0.22f), new Vector3(0f, 0.08f, 0.2f), white);
        MakeCube("HeadTop", head, new Vector3(0.12f, 0.06f, 0.14f), new Vector3(0f, 0.07f, 0.02f), lightGray);
        MakeCube("CheekL", head, new Vector3(0.04f, 0.06f, 0.1f), new Vector3(-0.08f, -0.01f, 0.04f), lightGray);
        MakeCube("CheekR", head, new Vector3(0.04f, 0.06f, 0.1f), new Vector3(0.08f, -0.01f, 0.04f), lightGray);

        Transform crown = MakeCube("Crown", head, new Vector3(0.1f, 0.05f, 0.12f), new Vector3(0f, 0.1f, 0.02f), redCrown);
        MakeCube("CrownSpot", crown, new Vector3(0.06f, 0.03f, 0.06f), new Vector3(0f, 0.02f, 0.02f), redCrown);

        MakeCube("EyePatchL", head, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.085f, 0.04f, 0.06f), Color.black);
        MakeCube("EyePatchR", head, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.085f, 0.04f, 0.06f), Color.black);
        MakeCube("EyeL", head, new Vector3(0.025f, 0.025f, 0.025f), new Vector3(-0.085f, 0.04f, 0.08f), eyeCol);
        MakeCube("EyeR", head, new Vector3(0.025f, 0.025f, 0.025f), new Vector3(0.085f, 0.04f, 0.08f), eyeCol);
        MakeCube("EyeHighlightL", head, new Vector3(0.008f, 0.008f, 0.008f), new Vector3(-0.08f, 0.045f, 0.09f), Color.white);
        MakeCube("EyeHighlightR", head, new Vector3(0.008f, 0.008f, 0.008f), new Vector3(0.08f, 0.045f, 0.09f), Color.white);

        Transform beak = MakeCube("Beak", head, new Vector3(0.07f, 0.055f, 0.3f), new Vector3(0f, -0.01f, 0.24f), beakCol);
        MakeCube("BeakTop", beak, new Vector3(0.055f, 0.02f, 0.28f), new Vector3(0f, 0.02f, 0.01f), beakCol);
        MakeCube("BeakBottom", beak, new Vector3(0.04f, 0.015f, 0.22f), new Vector3(0f, -0.02f, 0.02f), beakCol);
        MakeCube("BeakTip", beak, new Vector3(0.03f, 0.03f, 0.08f), new Vector3(0f, 0f, 0.15f), beakTip);
        MakeCube("BeakNostrilL", beak, new Vector3(0.01f, 0.008f, 0.02f), new Vector3(-0.02f, 0.015f, 0.08f), Color.black);
        MakeCube("BeakNostrilR", beak, new Vector3(0.01f, 0.008f, 0.02f), new Vector3(0.02f, 0.015f, 0.08f), Color.black);

        _cagePivot = new GameObject("CagePivot").transform;
        _cagePivot.SetParent(beak, false);
        _cagePivot.localPosition = new Vector3(0f, -0.18f, 0.1f);
    }

    private void BuildTailFeathers(Color white, Color lightGray, Color midGray, Color darkGray, Color darkTip)
    {
        Transform tailBase = MakeCube("TailBase", _body, new Vector3(0.3f, 0.1f, 0.2f), new Vector3(0f, 0.06f, -0.7f), white);

        MakeCube("TailFan0", tailBase, new Vector3(0.08f, 0.015f, 0.6f), new Vector3(0f, 0.04f, -0.35f), lightGray);
        MakeCube("TailFanL1", tailBase, new Vector3(0.06f, 0.012f, 0.55f), new Vector3(-0.08f, 0.03f, -0.33f), midGray);
        MakeCube("TailFanR1", tailBase, new Vector3(0.06f, 0.012f, 0.55f), new Vector3(0.08f, 0.03f, -0.33f), midGray);
        MakeCube("TailFanL2", tailBase, new Vector3(0.05f, 0.01f, 0.5f), new Vector3(-0.14f, 0.02f, -0.3f), darkGray);
        MakeCube("TailFanR2", tailBase, new Vector3(0.05f, 0.01f, 0.5f), new Vector3(0.14f, 0.02f, -0.3f), darkGray);
        MakeCube("TailFanL3", tailBase, new Vector3(0.04f, 0.008f, 0.45f), new Vector3(-0.18f, 0.01f, -0.28f), darkTip);
        MakeCube("TailFanR3", tailBase, new Vector3(0.04f, 0.008f, 0.45f), new Vector3(0.18f, 0.01f, -0.28f), darkTip);

        MakeCube("TailBustleL", tailBase, new Vector3(0.1f, 0.08f, 0.12f), new Vector3(-0.12f, 0.08f, -0.05f), white);
        MakeCube("TailBustleR", tailBase, new Vector3(0.1f, 0.08f, 0.12f), new Vector3(0.12f, 0.08f, -0.05f), white);
        MakeCube("TailBustleC", tailBase, new Vector3(0.12f, 0.1f, 0.1f), new Vector3(0f, 0.1f, -0.04f), lightGray);
    }

    private void BuildLegs(Color legCol)
    {
        Color jointCol = new Color(0.5f, 0.5f, 0.45f);
        Color clawCol = new Color(0.35f, 0.32f, 0.28f);

        Transform thighL = MakeCube("ThighL", _body, new Vector3(0.06f, 0.35f, 0.06f), new Vector3(-0.1f, -0.28f, -0.15f), legCol);
        Transform shinL = MakeCube("ShinL", thighL, new Vector3(0.04f, 0.4f, 0.04f), new Vector3(0f, -0.3f, 0.02f), legCol);
        MakeCube("KneeL", thighL, new Vector3(0.055f, 0.055f, 0.055f), new Vector3(0f, -0.02f, 0f), jointCol);
        Transform ankleL = MakeCube("AnkleL", shinL, new Vector3(0.045f, 0.05f, 0.045f), new Vector3(0f, -0.22f, 0f), jointCol);
        MakeCube("TarsusL", ankleL, new Vector3(0.03f, 0.2f, 0.03f), new Vector3(0f, -0.12f, 0.02f), legCol);
        Transform footL = MakeCube("FootL", ankleL, new Vector3(0.08f, 0.015f, 0.12f), new Vector3(0f, -0.24f, 0.04f), clawCol);
        MakeCube("ToeL1", footL, new Vector3(0.02f, 0.01f, 0.06f), new Vector3(-0.025f, 0f, 0.06f), clawCol);
        MakeCube("ToeL2", footL, new Vector3(0.02f, 0.01f, 0.06f), new Vector3(0f, 0f, 0.06f), clawCol);
        MakeCube("ToeL3", footL, new Vector3(0.02f, 0.01f, 0.06f), new Vector3(0.025f, 0f, 0.06f), clawCol);
        MakeCube("ToeBackL", footL, new Vector3(0.015f, 0.01f, 0.04f), new Vector3(0f, 0f, -0.04f), clawCol);

        Transform thighR = MakeCube("ThighR", _body, new Vector3(0.06f, 0.35f, 0.06f), new Vector3(0.1f, -0.28f, -0.15f), legCol);
        Transform shinR = MakeCube("ShinR", thighR, new Vector3(0.04f, 0.4f, 0.04f), new Vector3(0f, -0.3f, 0.02f), legCol);
        MakeCube("KneeR", thighR, new Vector3(0.055f, 0.055f, 0.055f), new Vector3(0f, -0.02f, 0f), jointCol);
        Transform ankleR = MakeCube("AnkleR", shinR, new Vector3(0.045f, 0.05f, 0.045f), new Vector3(0f, -0.22f, 0f), jointCol);
        MakeCube("TarsusR", ankleR, new Vector3(0.03f, 0.2f, 0.03f), new Vector3(0f, -0.12f, 0.02f), legCol);
        Transform footR = MakeCube("FootR", ankleR, new Vector3(0.08f, 0.015f, 0.12f), new Vector3(0f, -0.24f, 0.04f), clawCol);
        MakeCube("ToeR1", footR, new Vector3(0.02f, 0.01f, 0.06f), new Vector3(-0.025f, 0f, 0.06f), clawCol);
        MakeCube("ToeR2", footR, new Vector3(0.02f, 0.01f, 0.06f), new Vector3(0f, 0f, 0.06f), clawCol);
        MakeCube("ToeR3", footR, new Vector3(0.02f, 0.01f, 0.06f), new Vector3(0.025f, 0f, 0.06f), clawCol);
        MakeCube("ToeBackR", footR, new Vector3(0.015f, 0.01f, 0.04f), new Vector3(0f, 0f, -0.04f), clawCol);
    }

    private void BuildCage()
    {
        bool isBig = _animalType == Livestock.AnimalType.Cow ||
                     _animalType == Livestock.AnimalType.Pig ||
                     _animalType == Livestock.AnimalType.Sheep ||
                     _animalType == Livestock.AnimalType.Goat;
        float cw = isBig ? 0.5f : 0.35f;
        float ch = isBig ? 0.4f : 0.3f;
        float cd = isBig ? 0.4f : 0.3f;

        _cageModel = new GameObject("CageModel").transform;
        _cageModel.SetParent(_cagePivot, false);
        ItemBuilder.BuildDetailedCage(_cageModel, cw, ch, cd);
        Livestock.BuildModelInto(_cageModel, _animalType);
    }

    private static Transform MakeCube(string name, Transform parent, Vector3 scale, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = pos;
        go.GetComponent<Renderer>().material.color = color;
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    private void Update()
    {
        if (_phase == 0)
            FlyTowardDrop();
        else if (_phase == 1)
            FlyAway();

        AnimateWings();
    }

    private void AnimateWings()
    {
        if (_wingL == null || _wingR == null) return;

        float flap = Mathf.Sin(Time.time * 4f) * 22f;
        float spread = Mathf.Sin(Time.time * 4f + 0.3f) * 3f;

        _wingL.localRotation = Quaternion.Euler(0f, spread, flap);
        _wingR.localRotation = Quaternion.Euler(0f, -spread, -flap);

        if (_wingFarL != null)
        {
            float outerFlap = Mathf.Sin(Time.time * 4f - 0.4f) * 12f;
            _wingFarL.localRotation = Quaternion.Euler(0f, 0f, outerFlap);
        }
        if (_wingFarR != null)
        {
            float outerFlap = Mathf.Sin(Time.time * 4f - 0.4f) * -12f;
            _wingFarR.localRotation = Quaternion.Euler(0f, 0f, outerFlap);
        }
    }

    private void FlyTowardDrop()
    {
        Vector3 targetPos = new Vector3(_dropTarget.x, _flyHeight, _dropTarget.z);
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;

        transform.position += (targetPos - transform.position).normalized * _flySpeed * Time.deltaTime;
        float targetY = _flyHeight + Mathf.Sin(Time.time * 1.2f) * 3f;
        var pos = transform.position;
        pos.y = targetY;
        transform.position = pos;

        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);

        float xzDist = new Vector2(transform.position.x - _dropTarget.x, transform.position.z - _dropTarget.z).magnitude;
        if (xzDist < 3f && !_cageDropped)
        {
            _cageDropped = true;
            StartCoroutine(DropCageSequence());
        }
    }

    private void FlyAway()
    {
        Vector3 dir = transform.forward;
        transform.position += dir * _flySpeed * 1.2f * Time.deltaTime;

        float distFromOrigin = transform.position.magnitude;
        if (distFromOrigin > 350f)
            Deactivate();
    }

    private IEnumerator DropCageSequence()
    {
        _phase = 2;

        if (_wingL != null) _wingL.localRotation = Quaternion.Euler(0f, 0f, 40f);
        if (_wingR != null) _wingR.localRotation = Quaternion.Euler(0f, 0f, -40f);

        yield return new WaitForSeconds(0.3f);

        if (_cagePivot != null)
        {
            _cagePivot.SetParent(null);

            var cageGo = _cagePivot.gameObject;
            cageGo.name = "DroppedCage";

            var rb = cageGo.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = cageGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.6f, 0.5f, 0.6f);
            col.center = Vector3.zero;

            StartCoroutine(WaitForCageLand(cageGo));
        }

        yield return new WaitForSeconds(0.5f);
        _phase = 1;
    }

    private IEnumerator WaitForCageLand(GameObject cageGo)
    {
        float dropY = cageGo.transform.position.y;
        float timeout = Time.time + 10f;
        bool touchedGround = false;

        yield return new WaitForSeconds(0.4f);

        while (Time.time < timeout && cageGo != null)
        {
            float cy = cageGo.transform.position.y;
            if (cy <= 1.2f)
            {
                touchedGround = true;
                break;
            }
            yield return null;
        }

        if (cageGo == null) yield break;

        yield return new WaitForSeconds(0.3f);

        if (cageGo == null) yield break;

        Vector3 landPos = cageGo.transform.position;
        landPos.y = Mathf.Max(landPos.y, 0.5f);

        var go = new GameObject("Livestock_" + _animalType);
        go.transform.SetParent(WorldBuilder.Instance?.WorldRoot?.transform);
        go.transform.position = landPos;
        var livestock = go.AddComponent<Livestock>();
        livestock.Type = _animalType;
        livestock.StartSpawnAnimation();
        _onLanded?.Invoke(livestock);

        StartCoroutine(FadeOutCage(cageGo));
    }

    private IEnumerator FadeOutCage(GameObject cageGo)
    {
        float duration = 3f;
        float elapsed = 0f;
        var renderers = cageGo.GetComponentsInChildren<Renderer>();
        Vector3 startScale = cageGo.transform.localScale;

        Rigidbody rb = cageGo.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        var cols = cageGo.GetComponents<Collider>();
        foreach (var c in cols) c.enabled = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float s = 1f - t;
            cageGo.transform.localScale = startScale * s;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var c = r.material.color;
                c.a = 1f - t;
                r.material.color = c;
            }
            yield return null;
        }

        Destroy(cageGo);
    }

    private void Deactivate()
    {
        _phase = 0;
        _cageDropped = false;
        _cagePivot = null;
        _cageModel = null;
        _wingL = null;
        _wingR = null;
        _wingFarL = null;
        _wingFarR = null;
        _body = null;

        foreach (Transform child in transform)
            Destroy(child.gameObject);

        var spawner = FindObjectOfType<LivestockSpawner>();
        if (spawner != null)
            spawner.ReturnCrane(this);
        else
            gameObject.SetActive(false);
    }
}
