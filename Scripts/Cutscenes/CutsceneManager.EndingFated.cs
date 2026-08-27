using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager 
{
    // ═══════════════════════════════════════════════
    //  FATED ENDING
    // ═══════════════════════════════════════════════

    private IEnumerator FatedEndingRoutine(System.Action onComplete = null)
    {
        _savedTimeSpeed = GameManager.Instance != null ? GameManager.Instance.TimeSpeed : 0.01f;
        try
        {
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        DisablePlayerControl();
        DetachCamera();
        HideHUD();

        // Ensure the real mansion exists and is fully built before we stage on it
        WorldBuilder.Instance?.CompleteMansionImmediately();
        Vector3 mb = WorldBuilder.Instance?.GetMansionPosition() ?? new Vector3(-22f, 0f, 0f);

        yield return StartCoroutine(CreateFadeOverlay());
        CreateLetterboxBars();
        ShowSkipButton();

        if (_player != null)
        {
            _player.transform.position = new Vector3(RoadX, 0f, 45f);
            _player.transform.rotation = Quaternion.identity;
            var realModel = _player.transform.Find("PlayerModel");
            if (realModel != null)
                realModel.gameObject.SetActive(false);
        }

        GameManager.Instance?.SetTimeOfDay(12f);
        if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = 0;

        // ── Set: the real mansion (front = +z, front-left living room) ──
        float houseX = mb.x;
        float houseZ = mb.z;

        // ── Police car parked on the grass in front of the mansion ──
        var policeCar = MapBuilder.BuildPoliceCar(null, new Vector3(houseX - 4f, 0f, houseZ + 13.5f));
        policeCar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        RegisterSpawned(policeCar);

        // ── Dead bodies inside the living room ──
        var deadPlayer = MapBuilder.BuildPlayerModel(null);
        deadPlayer.transform.position = new Vector3(houseX - 8.2f, 0.69f, houseZ + 6.4f);
        deadPlayer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        foreach (var r in deadPlayer.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(deadPlayer);

        var deadWife = WifeNPC.BuildWifeNpc(null,
            new Vector3(houseX - 5.5f, 0.82f, houseZ + 5.4f), 1f, Quaternion.Euler(90f, 0f, 0f));
        foreach (var r in deadWife.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(deadWife);

        CreateBloodPool(new Vector3(houseX - 8.2f, 0.53f, houseZ + 6.4f));
        CreateBloodPool(new Vector3(houseX - 5.5f, 0.53f, houseZ + 5.4f));

        // ── Robbery / addiction clue props ──
        BuildRobberyClues(new Vector3(houseX - 8.5f, 0.50f, houseZ + 6.2f));

        // ── Police officers (inside the living room, near the front wall) ──
        var officerA = MapBuilder.BuildPoliceOfficer(null,
            new Vector3(houseX - 9f, 1.43f, houseZ + 7.6f), Quaternion.Euler(0f, 180f, 0f));
        RegisterSpawned(officerA);
        var officerB = MapBuilder.BuildPoliceOfficer(null,
            new Vector3(houseX - 4.6f, 1.43f, houseZ + 7.6f), Quaternion.Euler(0f, 180f, 0f));
        RegisterSpawned(officerB);

        // ── Demons lurking at the room edges (camera border only) ──
        var demons = new List<Transform>();
        for (int g = 0; g < 5; g++)
        {
            var demon = EnemyModelBuilder.BuildRegularEnemy(null);
            foreach (var r in demon.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            foreach (var c in demon.GetComponentsInChildren<Collider>())
                Object.Destroy(c);
            demons.Add(demon);
        }
        Vector3[] demonPos =
        {
            new Vector3(houseX - 9.4f, 0.65f, houseZ + 8f),
            new Vector3(houseX - 4.6f, 0.65f, houseZ + 8f),
            new Vector3(houseX - 9.4f, 0.65f, houseZ + 6.2f),
            new Vector3(houseX - 4.8f, 0.65f, houseZ + 5.8f),
            new Vector3(houseX - 5.8f, 0.65f, houseZ + 2.4f)
        };
        Vector3 lookCenter = new Vector3(houseX - 7f, 0.65f, houseZ + 5.5f);
        for (int i = 0; i < demons.Count; i++)
        {
            demons[i].position = demonPos[i];
            demons[i].localScale = Vector3.one * 1f;
            demons[i].rotation = Quaternion.LookRotation(lookCenter - demonPos[i]);
            RegisterSpawned(demons[i].gameObject);
            StartCoroutine(IdleBob(demons[i], 0.15f));
        }

        // ── PHASE 1: exterior daytime, police car at the mansion front (4s) ──
        Vector3 camExt = new Vector3(houseX - 6f, 2.6f, houseZ + 16f);
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = camExt;
            _mainCamera.transform.LookAt(new Vector3(houseX, 1.8f, houseZ + 8.5f));
        }
        yield return StartCoroutine(FadeOverlay(0, 2f));
        yield return StartCoroutine(ShowSubtitle("Cửa dinh thự mở toang... còn chiếc xe cảnh sát đậu bên ngoài.", 3f));
        yield return new WaitForSeconds(1f);

        // ── PHASE 2: cut inside the living room, reveal bodies (6s) ──
        yield return StartCoroutine(FadeOverlay(1f, 0.6f));
        Vector3 camIn = new Vector3(houseX - 6.5f, 4.5f, houseZ + 7.4f);
        Vector3 lookBodies = new Vector3(houseX - 6.9f, 0.85f, houseZ + 5.9f);
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = camIn;
            _mainCamera.transform.LookAt(lookBodies);
        }
        yield return StartCoroutine(FadeOverlay(0f, 0.6f));
        yield return StartCoroutine(PanCamera(camIn, new Vector3(houseX - 6.8f, 4.3f, houseZ + 7.2f), lookBodies, 2.5f));
        yield return StartCoroutine(ShowSubtitle("Trong phòng... hai thi thể nằm bất động.", 3.5f));

        // ── PHASE 3: officers walk over, discover (7s) ──
        yield return StartCoroutine(WalkStraight(officerA.transform,
            new Vector3(houseX - 9f, 1.43f, houseZ + 7.6f),
            new Vector3(houseX - 8.2f, 1.43f, houseZ + 6.4f), 3.5f));
        yield return StartCoroutine(WalkStraight(officerB.transform,
            new Vector3(houseX - 4.6f, 1.43f, houseZ + 7.6f),
            new Vector3(houseX - 5.6f, 1.43f, houseZ + 5.8f), 3.5f));
        yield return StartCoroutine(ShowSubtitle("Cửa bị phá. Đồ đạc vương vãi khắp nơi.", 3.5f));
        yield return new WaitForSeconds(0.5f);

        // ── PHASE 4: the clue (13s) ──
        Vector3 camClue = new Vector3(houseX - 8.6f, 1.75f, houseZ + 7.6f);
        Vector3 lookClue = new Vector3(houseX - 8.5f, 0.65f, houseZ + 6.2f);
        yield return StartCoroutine(PanCamera(camIn, camClue, lookClue, 2.5f));
        yield return StartCoroutine(ShowSubtitle("Một vụ trộm... nhưng chỉ mất vài đồng vàng vụn.", 3f));
        yield return StartCoroutine(ShowSubtitle("Khoan đã... bơm kim tiêm. Dấu vết nghiện ngập.", 3f));
        yield return StartCoroutine(ShowSubtitle("Kẻ nghiện này... có vẻ liên quan đến gia tộc giàu có.", 3.5f));

        // ── PHASE 5: lights dim, the demons at the border (6s) ──
        yield return StartCoroutine(FadeOverlay(0.7f, 2.5f));
        yield return StartCoroutine(ShowSubtitle("Và lũ quỷ... vẫn đứng im ngay rìa bóng tối. Không ai nhìn thấy chúng.", 3.5f));
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(FadeOverlay(1f, 2f));

        // ── PHASE 6: end screen ──
        HideSkipButton();
        DestroySubtitle();
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();

        FinishEndingScene(onComplete,
            Localization.T("KẾT THÚC ĐỊNH MỆNH"),
            Localization.T("Bạn và Jessica đã xây xong dinh thự... nhưng không bao giờ diệt Quỷ Vương,\nkhông lật tẩy bí mật của Phú Ông.\n\nMột đêm, kẻ nghiện ngập do ma túy của Phú Ông đã đột nhập.\nCảnh sát tìm thấy hai thi thể trong chính ngôi nhà bạn xây nên.\nDấu vết: một vụ trộm... do nghiện ngập.\n\nVà lũ quỷ vẫn đứng im ở rìa màn đêm,\nkhông một ai nhìn thấy chúng.\n\nĐịnh mệnh của bạn đã kết thúc ngay trong nhà mình."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
            if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = _savedTimeSpeed;
        }
    }

    private void CreateBloodPool(Vector3 position)
    {
        var blood = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        blood.transform.position = position;
        blood.transform.localScale = new Vector3(1.4f, 0.03f, 1.1f);
        var r = blood.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.5f, 0.03f, 0.02f);
        Object.Destroy(blood.GetComponent<Collider>());
        RegisterSpawned(blood);
    }

    private void BuildRobberyClues(Vector3 position)
    {
        Color goldC = new Color(0.85f, 0.72f, 0.2f);
        Color darkC = new Color(0.25f, 0.2f, 0.14f);
        Color glassC = new Color(0.7f, 0.75f, 0.8f);

        CreateBlock(position + new Vector3(0f, 0.05f, 0f), new Vector3(0.25f, 0.08f, 0.25f), goldC);
        CreateBlock(position + new Vector3(-0.4f, 0.05f, 0.2f), new Vector3(0.18f, 0.1f, 0.18f), goldC);
        CreateBlock(position + new Vector3(0.3f, 0.05f, -0.3f), new Vector3(0.22f, 0.06f, 0.22f), goldC);
        CreateBlock(position + new Vector3(-0.15f, 0.12f, -0.15f), new Vector3(0.06f, 0.22f, 0.06f), glassC);
        CreateBlock(position + new Vector3(0.5f, 0.04f, 0.4f), new Vector3(0.5f, 0.05f, 0.35f), darkC);
    }
}
