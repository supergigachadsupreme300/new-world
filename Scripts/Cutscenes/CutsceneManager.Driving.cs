using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════
    //  INTRO CUTSCENE
    // ═══════════════════════════════════════════════

    private IEnumerator IntroRoutine(System.Action onComplete)
    {
        bool completedNormally = false;
        try
        {
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        DisablePlayerControl();
        DetachCamera();
        HideHUD();
        ShowSkipButton();

        if (_prebuilt)
        {
            _introCar.SetActive(true);
            _introCar.transform.SetParent(null);
            _introCar.transform.position = new Vector3(RoadX, 0f, IntroStartZ);
            _introPlayer.SetActive(true);
        }
        else
        {
            InitDrivingMaterials();

            if (_introCar == null)
            {
                _introCar = MapBuilder.BuildCar(null, new Vector3(RoadX, 0f, IntroStartZ));
                RegisterSpawned(_introCar);

                _introPlayer = MapBuilder.BuildSeatedPlayerModel(_introCar.transform);
                RegisterSpawned(_introPlayer);

                if (_introCar != null)
                {
                    _introSteeringWheel = _introCar.transform.Find("SteeringWheel");
                    _introWheels.Clear();
                    foreach (string wn in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
                    {
                        var wt = _introCar.transform.Find(wn);
                        if (wt != null) _introWheels.Add(wt);
                    }
                }
            }
            else
            {
                _introCar.transform.SetParent(null);
            }
        }

        _steeringAnimRoutine = StartCoroutine(AnimateSteering());

        Quaternion camRot = Quaternion.Euler(13f, 130f, -2.5f);
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = new Vector3(RoadX + CamOffsetX, CamOffsetY, IntroStartZ + CamOffsetZ);
            _mainCamera.transform.rotation = camRot;
        }

        Vector3 camFixedPos = _mainCamera != null ? _mainCamera.transform.position : Vector3.zero;

        float driveZ = IntroStartZ;
        SpawnIntroRoadOnce();
        while (driveZ < IntroEndZ)
        {
            driveZ += DrivingSpeed * Time.deltaTime;
            if (_introCar != null)
                _introCar.transform.position = new Vector3(RoadX, 0f, driveZ);
            if (_mainCamera != null)
            {
                Vector3 lookTarget = new Vector3(RoadX, 1f, driveZ);
                _mainCamera.transform.LookAt(lookTarget);
            }
            yield return null;
        }

        StopSteeringAnim();
        yield return new WaitForSeconds(0.5f);

        DestroyDrivingSegments();
        HideSkipButton();
        if (!_prebuilt)
        {
            CleanupSpawned();
            _introCar = null;
            _introPlayer = null;
        }
        else
        {
            _introCar.SetActive(false);
            _introPlayer.SetActive(false);
        }
        _introSteeringWheel = null;
        _introWheels.Clear();
        ShowHUD();
        RestorePlayerControl();

        if (WorldBuilder.Instance != null)
            WorldBuilder.Instance.CloseBorderGap();

        completedNormally = true;
        onComplete?.Invoke();

        if (onComplete == null && _uiManager != null && GameManager.Instance != null && !GameManager.Instance.InGame)
            _uiManager.ShowMainMenu(true);
        }
        finally
        {
            HideSkipButton();
            if (!completedNormally)
                RestorePlayerControl();
            if (GameManager.Instance != null && GameManager.Instance.InGame)
                ShowHUD();
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    // ═══════════════════════════════════════════════
    //  DRIVING GROUND SEGMENTS
    // ═══════════════════════════════════════════════

    private void InitDrivingMaterials()
    {
        if (_drivingRoadMat != null) return;
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        var shader = urp != null ? urp : Shader.Find("Standard");
        _drivingRoadMat = new Material(shader);
        _drivingRoadMat.color = new Color(0.235f, 0.243f, 0.275f);
        _drivingGrassMat = new Material(shader);
        var tex = Resources.Load<Texture2D>("texture/grass_blade");
        if (tex != null)
        {
            _drivingGrassMat.mainTexture = tex;
            _drivingGrassMat.mainTextureScale = new Vector2(4f, SegmentLength / 5f);
        }
        else
        {
            _drivingGrassMat.color = new Color(0.3f, 0.6f, 0.25f);
        }
        _drivingKerbMat = new Material(shader);
        _drivingKerbMat.color = new Color(0.46f, 0.45f, 0.42f);
    }

    // ═══════════════════════════════════════════════
    //  PREBUILD: materials + car + segments at load
    // ═══════════════════════════════════════════════

    private void PrebuildDrivingAssets()
    {
        if (_prebuilt) return;

        InitDrivingMaterials();

        _introCar = MapBuilder.BuildCar(null, Vector3.zero);
        _introCar.SetActive(false);
        _introPlayer = MapBuilder.BuildSeatedPlayerModel(_introCar.transform);
        _introPlayer.SetActive(false);

        if (_introCar != null)
        {
            _introSteeringWheel = _introCar.transform.Find("SteeringWheel");
            _introWheels.Clear();
            foreach (string wn in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
            {
                var wt = _introCar.transform.Find(wn);
                if (wt != null) _introWheels.Add(wt);
            }
        }

        int poolSize = Mathf.CeilToInt((SegmentSpawnAhead + SegmentDespawnBehind) / SegmentLength) + 3;
        if (poolSize < MaxActiveSegments * 2 + 3) poolSize = MaxActiveSegments * 2 + 3;
        for (int i = 0; i < poolSize; i++)
        {
            var seg = SpawnDrivingSegmentRaw(0f);
            seg.SetActive(false);
            _segmentPool.Add(seg);
        }

        _prebuilt = true;
    }

    private GameObject GetPooledSegment()
    {
        for (int i = _segmentPool.Count - 1; i >= 0; i--)
        {
            var seg = _segmentPool[i];
            if (seg != null)
            {
                _segmentPool.RemoveAt(i);
                seg.SetActive(true);
                return seg;
            }
        }
        return SpawnDrivingSegmentRaw(0f);
    }

    private void ReturnToPool(GameObject seg)
    {
        if (seg == null) return;
        seg.SetActive(false);
        _segmentPool.Add(seg);
    }

    private void UpdateDrivingSegments(float baseZ, float groundOffset, float scrollSpeed, bool despawn)
    {
        if (despawn)
        {
            for (int i = _drivingSegments.Count - 1; i >= 0; i--)
            {
                if (_drivingSegments[i] == null) { _drivingSegments.RemoveAt(i); continue; }
                float segZ = _drivingSegments[i].transform.position.z;
                if (segZ < baseZ - SegmentDespawnBehind)
                {
                    if (_prebuilt)
                        ReturnToPool(_drivingSegments[i]);
                    else
                        Destroy(_drivingSegments[i]);
                    _drivingSegments.RemoveAt(i);
                }
            }
        }

        float farZ = float.MinValue;
        float nearZ = float.MaxValue;
        foreach (var seg in _drivingSegments)
        {
            if (seg == null) continue;
            float sz = seg.transform.position.z;
            if (sz > farZ) farZ = sz;
            if (sz < nearZ) nearZ = sz;
        }

        float carZ = baseZ + groundOffset;
        float needed = carZ + SegmentSpawnAhead;
        float needBehind = carZ - SegmentDespawnBehind;

        if (_drivingSegments.Count == 0)
        {
            float startZ = needBehind;
            int spawned = 0;
            while (startZ < needed && spawned < MaxSegmentsPerFrame && _drivingSegments.Count < MaxActiveSegments)
            {
                startZ += SegmentLength;
                _drivingSegments.Add(SpawnDrivingSegment(startZ));
                spawned++;
            }
        }
        else
        {
            int fwdSpawned = 0;
            while (farZ < needed && fwdSpawned < MaxSegmentsPerFrame && _drivingSegments.Count < MaxActiveSegments)
            {
                farZ += SegmentLength;
                _drivingSegments.Add(SpawnDrivingSegment(farZ));
                fwdSpawned++;
            }

            int bwdSpawned = 0;
            while (nearZ > needBehind && bwdSpawned < MaxSegmentsPerFrame && _drivingSegments.Count < MaxActiveSegments)
            {
                nearZ -= SegmentLength;
                _drivingSegments.Add(SpawnDrivingSegment(nearZ));
                bwdSpawned++;
            }
        }

        if (scrollSpeed > 0f)
        {
            float move = scrollSpeed * Time.deltaTime;
            foreach (var seg in _drivingSegments)
                if (seg != null)
                    seg.transform.position += new Vector3(0f, 0f, -move);
        }
    }

    private GameObject SpawnDrivingSegment(float centerZ)
    {
        var seg = _prebuilt ? GetPooledSegment() : SpawnDrivingSegmentRaw(centerZ);
        if (_prebuilt)
            seg.transform.position = new Vector3(0f, 0f, centerZ);
        return seg;
    }

    private GameObject SpawnDrivingSegmentRaw(float centerZ)
    {
        var seg = new GameObject("DrivingSeg");

        // Road
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "Road";
        road.transform.SetParent(seg.transform);
        road.transform.localScale = new Vector3(7.6f, 0.06f, SegmentLength);
        road.transform.localPosition = new Vector3(RoadX, 0.03f, 0f);
        road.GetComponent<Renderer>().sharedMaterial = _drivingRoadMat;
        Destroy(road.GetComponent<Collider>());

        // Kerbs
        foreach (int side in new[] { -1, 1 })
        {
            var kerb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kerb.name = "Kerb";
            kerb.transform.SetParent(seg.transform);
            kerb.transform.localScale = new Vector3(0.55f, 0.22f, SegmentLength);
            kerb.transform.localPosition = new Vector3(RoadX + side * 4.07f, 0.11f, 0f);
            kerb.GetComponent<Renderer>().sharedMaterial = _drivingKerbMat;
            Destroy(kerb.GetComponent<Collider>());
        }

        // Grass left
        float roadLeft = RoadX - 3.8f;
        var grassL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grassL.name = "GrassL";
        grassL.transform.SetParent(seg.transform);
        grassL.transform.localScale = new Vector3(roadLeft + 150f, 0.05f, SegmentLength);
        grassL.transform.localPosition = new Vector3((roadLeft - 150f) / 2f, 0f, 0f);
        grassL.GetComponent<Renderer>().sharedMaterial = _drivingGrassMat;
        Destroy(grassL.GetComponent<Collider>());

        // Grass right
        float roadRight = RoadX + 3.8f;
        var grassR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grassR.name = "GrassR";
        grassR.transform.SetParent(seg.transform);
        grassR.transform.localScale = new Vector3(200f - roadRight, 0.05f, SegmentLength);
        grassR.transform.localPosition = new Vector3((roadRight + 200f) / 2f, 0f, 0f);
        grassR.GetComponent<Renderer>().sharedMaterial = _drivingGrassMat;
        Destroy(grassR.GetComponent<Collider>());

        // Trees scattered across grass
        SpawnScatteredTrees(seg, SegmentLength);

        seg.transform.position = new Vector3(0f, 0f, centerZ);
        return seg;
    }

    private void SpawnScatteredTrees(GameObject parent, float segLen)
    {
        float roadLeft = RoadX - 3.8f;
        float roadRight = RoadX + 3.8f;
        int count = Mathf.FloorToInt(segLen / 4f);

        for (int i = 0; i < count; i++)
        {
            float z = -segLen / 2f + Random.Range(0f, segLen);

            float x;
            if (Random.value < 0.5f)
                x = Random.Range(-150f, roadLeft - 2f);
            else
                x = Random.Range(roadRight + 2f, 200f);

            float treeScale = Random.Range(0.6f, 1.2f);
            var tree = MapBuilder.BuildTree(parent.transform, new Vector3(x, 0f, z), treeScale);
            if (tree != null)
                tree.name = "ScatteredTree";
        }
    }

    private void DestroyDrivingSegments()
    {
        foreach (var seg in _drivingSegments)
        {
            if (seg == null) continue;
            if (_prebuilt)
                ReturnToPool(seg);
            else
                Destroy(seg);
        }
        _drivingSegments.Clear();
    }

    // Spawns the intro road once as a static strip, from behind the starting
    // camera up to the edge of the real world road (which already exists at
    // z >= -300). The road is NOT recycled during the drive, so it no longer
    // vanishes behind the car; DestroyDrivingSegments() returns it to the
    // pool after the intro.
    private void SpawnIntroRoadOnce()
    {
        float introRoadStart = IntroStartZ - SegmentDespawnBehind;
        float introRoadEnd = -300f;
        int spawned = 0;
        for (float c = introRoadStart + SegmentLength / 2f;
             c >= introRoadEnd + SegmentLength / 2f && spawned < MaxActiveSegments;
             c -= SegmentLength)
        {
            _drivingSegments.Add(SpawnDrivingSegment(c));
            spawned++;
        }
    }

    // ═══════════════════════════════════════════════
    //  STEERING WHEEL + HANDS ANIMATION
    // ═══════════════════════════════════════════════

    private IEnumerator AnimateSteering()
    {
        Vector3 restHandL = new Vector3(-0.26f, 0.42f, 0.38f);
        Vector3 restHandR = new Vector3(0.26f, 0.42f, 0.38f);
        Vector3 restArmL = new Vector3(-0.26f, 0.35f, 0.15f);
        Vector3 restArmR = new Vector3(0.26f, 0.35f, 0.15f);
        float wheelSpin = 0f;
        while (true)
        {
            if (_introSteeringWheel != null)
            {
                float angle = -Mathf.Sin(Time.time * 2f) * 15f;
                _introSteeringWheel.localRotation = Quaternion.Euler(60f, 0f, angle);
            }
            if (_introPlayer != null)
            {
                float push = Mathf.Sin(Time.time * 2f) * 0.06f;

                var handL = _introPlayer.transform.Find("HandL");
                var handR = _introPlayer.transform.Find("HandR");
                if (handL != null) handL.localPosition = restHandL + new Vector3(0f, 0f, push);
                if (handR != null) handR.localPosition = restHandR + new Vector3(0f, 0f, -push);

                var armL = _introPlayer.transform.Find("UpperArmL");
                var armR = _introPlayer.transform.Find("UpperArmR");
                if (armL != null) armL.localPosition = restArmL + new Vector3(0f, 0f, push);
                if (armR != null) armR.localPosition = restArmR + new Vector3(0f, 0f, -push);
            }
            wheelSpin += Time.deltaTime * 300f;
            foreach (var w in _introWheels)
                if (w != null) w.localRotation = Quaternion.Euler(wheelSpin, 0f, 90f);
            yield return null;
        }
    }

    private void StopSteeringAnim()
    {
        if (_steeringAnimRoutine != null)
        {
            StopCoroutine(_steeringAnimRoutine);
            _steeringAnimRoutine = null;
        }
    }

    // ═══════════════════════════════════════════════
    //  MAIN MENU VISUAL  (close-up of player driving)
    // ═══════════════════════════════════════════════

    public void PlayMainMenuVisual()
    {
        if (_menuVisualRoutine != null) return;
        StopMainMenuVisual();
        _menuVisualRoutine = StartCoroutine(MenuVisualRoutine());
    }

    public void StopIntroIfActive()
    {
        if (!IsActive) return;
        CancelCutscene();
    }

    public void StopMainMenuVisual(bool keepSegments = false)
    {
        if (_menuVisualRoutine != null)
        {
            StopCoroutine(_menuVisualRoutine);
            _menuVisualRoutine = null;
            AttachCamera();
        }
        StopSteeringAnim();
        if (!keepSegments)
            DestroyDrivingSegments();
        if (_prebuilt && _introCar != null)
            _introCar.SetActive(false);
    }

    private IEnumerator MenuVisualRoutine()
    {
        DetachCamera();
        DisablePlayerControl();

        if (_prebuilt)
        {
            _introCar.SetActive(true);
            _introCar.transform.position = new Vector3(RoadX, 0f, IntroStartZ);
            _introPlayer.SetActive(true);
        }
        else
        {
            InitDrivingMaterials();

            _introCar = MapBuilder.BuildCar(null, new Vector3(RoadX, 0f, IntroStartZ));
            RegisterSpawned(_introCar);
            _introPlayer = MapBuilder.BuildSeatedPlayerModel(_introCar.transform);
            RegisterSpawned(_introPlayer);

            if (_introCar != null)
            {
                _introSteeringWheel = _introCar.transform.Find("SteeringWheel");
                _introWheels.Clear();
                foreach (string wn in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
                {
                    var wt = _introCar.transform.Find(wn);
                    if (wt != null) _introWheels.Add(wt);
                }
            }
        }

        _steeringAnimRoutine = StartCoroutine(AnimateSteering());

        if (_mainCamera != null)
        {
            _mainCamera.transform.position = new Vector3(RoadX + CamOffsetX, CamOffsetY, IntroStartZ + CamOffsetZ);
            _mainCamera.transform.rotation = Quaternion.Euler(13f, 130f, -2.5f);
        }

        float groundOffset = 0f;
        while (true)
        {
            groundOffset += DrivingSpeed * 0.5f * Time.deltaTime;
            UpdateDrivingSegments(IntroStartZ, groundOffset, DrivingSpeed, true);
            yield return null;
        }
    }
}
