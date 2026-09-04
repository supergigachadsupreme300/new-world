using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager 
{
    private IEnumerator JusticeEndingRoutine(System.Action onComplete = null)
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

            yield return StartCoroutine(CreateFadeOverlay());
            CreateLetterboxBars();
            ShowSkipButton();

            // Player watching from the road shoulder (off the car's path)
            float playerZ = 92f;
            float playerX = RoadX + 4.5f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(playerX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }
            var justicePlayerModel = MapBuilder.BuildPlayerModel(null);
            justicePlayerModel.transform.position = new Vector3(playerX, 0.82f, playerZ);
            justicePlayerModel.transform.rotation = Quaternion.identity;
            foreach (var r in justicePlayerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(justicePlayerModel);

            // Rich man standing at the mansion front, facing the road
            var richModel = RichManNPC.BuildRichManNpc(null,
                new Vector3(60.5f, 0.86f, 100f), 1f, Quaternion.Euler(0f, 90f, 0f), false);
            foreach (var r in richModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(richModel);

            // ── PHASE 1: OPENING SHOT ──
            Vector3 camStart = new Vector3(RoadX, 2.2f, playerZ - 3f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(60f, 1.2f, 100f));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2f);

            // ── PHASE 2: POLICE CAR ARRIVES ──
            float carStopZ = 104f;
            var policeCar = MapBuilder.BuildPoliceCar(null, new Vector3(RoadX, 0f, 116f));
            policeCar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            RegisterSpawned(policeCar);
            float driveDur = 4f;
            float driveTimer = 0f;
            while (driveTimer < driveDur)
            {
                driveTimer += Time.deltaTime;
                float p = Mathf.Min(driveTimer / driveDur, 1f);
                policeCar.transform.position = new Vector3(RoadX, 0f, Mathf.Lerp(116f, carStopZ, p));
                yield return null;
            }

            var officer = MapBuilder.BuildPoliceOfficer(null,
                new Vector3(RoadX - 1.2f, 0.93f, carStopZ), Quaternion.Euler(0f, -90f, 0f));
            foreach (var r in officer.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(officer);

            // ── PHASE 3: THE ARREST ──
            Vector3 officerStart = officer.transform.position;
            Vector3 richPos = richModel.transform.position;
            Vector3 arrestTarget = Vector3.MoveTowards(officerStart, richPos, Mathf.Max(0f, Vector3.Distance(officerStart, richPos) - 1.5f));
            float arrestDur = 4f;
            float arrestTimer = 0f;
            while (arrestTimer < arrestDur)
            {
                arrestTimer += Time.deltaTime;
                float p = Mathf.Min(arrestTimer / arrestDur, 1f);
                officer.transform.position = Vector3.Lerp(officerStart, arrestTarget, p);
                FaceMoveDirection(officer.transform, richPos - officer.transform.position);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX - 1f, 2.6f, carStopZ + 4f), p);
                    _mainCamera.transform.LookAt(new Vector3(60f, 1.2f, 100f));
                }
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 4: TAKEN AWAY (walk west to the car, then back toward the doors) ──
            Vector3 richStart = richModel.transform.position;
            Vector3 officerPhase4Start = officer.transform.position;
            Vector3 carDoor = new Vector3(RoadX - 0.6f, 0.86f, carStopZ - 0.4f);
            Vector3 officerDoor = new Vector3(RoadX + 0.6f, 0.86f, carStopZ - 0.4f);
            Vector3 richMid = new Vector3(carDoor.x, richStart.y, richStart.z);
            Vector3 officerMid = new Vector3(officerDoor.x, officerPhase4Start.y, officerPhase4Start.z);
            Vector3 camPhase4Pos = new Vector3(RoadX - 3f, 3.5f, (richStart.z + carStopZ) * 0.5f + 2f);
            float takeLegDur = 2f;
            var legWestRich = StartCoroutine(WalkStraight(richModel.transform, richStart, richMid, takeLegDur));
            var legWestOfficer = StartCoroutine(WalkStraight(officer.transform, officerPhase4Start, officerMid, takeLegDur));
            float cam4Timer = 0f;
            while (cam4Timer < takeLegDur)
            {
                cam4Timer += Time.deltaTime;
                if (_mainCamera != null)
                {
                    float cp = Mathf.SmoothStep(0f, 1f, Mathf.Min(cam4Timer / takeLegDur, 1f));
                    Vector3 midLook = (richStart + carDoor) * 0.5f + Vector3.up * 0.8f;
                    _mainCamera.transform.position = Vector3.Lerp(
                        new Vector3(RoadX - 1f, 2.6f, carStopZ + 4f), camPhase4Pos, cp);
                    _mainCamera.transform.LookAt(midLook);
                }
                yield return null;
            }
            yield return legWestRich;
            yield return legWestOfficer;
            var legSouthRich = StartCoroutine(WalkStraight(richModel.transform, richMid, carDoor, takeLegDur));
            var legSouthOfficer = StartCoroutine(WalkStraight(officer.transform, officerMid, officerDoor, takeLegDur));
            cam4Timer = 0f;
            Vector3 camPhase4bPos = new Vector3(RoadX - 4f, 3f, carStopZ + 2f);
            while (cam4Timer < takeLegDur)
            {
                cam4Timer += Time.deltaTime;
                if (_mainCamera != null)
                {
                    float cp = Mathf.SmoothStep(0f, 1f, Mathf.Min(cam4Timer / takeLegDur, 1f));
                    _mainCamera.transform.position = Vector3.Lerp(camPhase4Pos, camPhase4bPos, cp);
                    _mainCamera.transform.LookAt(carDoor + Vector3.up * 0.5f);
                }
                yield return null;
            }
            yield return legSouthRich;
            yield return legSouthOfficer;

            richModel.transform.SetParent(policeCar.transform);
            richModel.transform.localPosition = new Vector3(-0.35f, 0.62f, -0.2f);
            richModel.transform.localRotation = Quaternion.identity;
            officer.transform.SetParent(policeCar.transform);
            officer.transform.localPosition = new Vector3(0.35f, 0.62f, -0.2f);
            officer.transform.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(1f);

            // ── PHASE 5: DEPARTURE (car drives off-screen with easing) ──
            float departDur = 6f;
            float departTimer = 0f;
            float departZ = -180f;
            bool fadeStarted = false;
            while (departTimer < departDur)
            {
                departTimer += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Min(departTimer / departDur, 1f));
                float carZ = Mathf.Lerp(carStopZ, departZ, p);
                policeCar.transform.position = new Vector3(RoadX, 0f, carZ);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = new Vector3(RoadX - 2f, 3.2f, carZ + 10f);
                    _mainCamera.transform.LookAt(policeCar.transform.position + Vector3.up * 0.5f);
                }
                if (p > 0.6f && !fadeStarted)
                {
                    fadeStarted = true;
                    StartCoroutine(FadeOverlay(1, 1.5f));
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);

            // Freeze the clock at a quiet night hour
            if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = 0;
            GameManager.Instance?.SetTimeOfDay(23.5f);

            // Stage at the wife's bedroom (reuse the live house's bed if intact)
            GameObject wifeHouse = GameObject.Find("WifeHouse");
            Transform bedTransform = wifeHouse != null ? FindChildRecursive(wifeHouse.transform, "BedMattress") : null;
            GameObject bedroomRoot = null;
            Vector3 bedPos;
            if (bedTransform != null)
            {
                bedPos = bedTransform.position;
            }
            else
            {
                bedroomRoot = BuildBedroomSet(new Vector3(33f, 0f, 0f));
                bedTransform = bedroomRoot != null ? FindChildRecursive(bedroomRoot.transform, "BedMattress") : null;
                bedPos = bedTransform != null ? bedTransform.position : new Vector3(32f, 0.85f, 5.55f);
            }

            // Wife asleep on the near side, player on the far side (heads on the pillows)
            var nightWife = WifeNPC.BuildWifeNpc(null, bedPos + new Vector3(0.55f, 0.35f, 0f), 1f, Quaternion.Euler(90f, 0f, 0f));
            foreach (var r in nightWife.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            foreach (var c in nightWife.GetComponentsInChildren<Collider>())
                Object.Destroy(c);
            RegisterSpawned(nightWife);

            var nightPlayer = MapBuilder.BuildPlayerModel(null);
            nightPlayer.transform.position = bedPos + new Vector3(-0.55f, 0.35f, 0f);
            nightPlayer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (var r in nightPlayer.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            foreach (var c in nightPlayer.GetComponentsInChildren<Collider>())
                Object.Destroy(c);
            RegisterSpawned(nightPlayer);

            // Warm lamp light for the night
            var lamp = new GameObject("BedroomLamp");
            var lampLight = lamp.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.range = 9f;
            lampLight.intensity = 2.2f;
            lampLight.color = new Color(1f, 0.65f, 0.3f);
            lamp.transform.position = bedPos + new Vector3(0f, 1.4f, 0f);
            RegisterSpawned(lamp);

            yield return StartCoroutine(FadeOverlay(0, 1.5f));

            // Wide shot of the bedroom at night
            Vector3 camWide = bedPos + new Vector3(2.2f, 1.1f, 1.2f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camWide;
                _mainCamera.transform.LookAt(bedPos + new Vector3(0f, 0.1f, 0.4f));
            }
            yield return new WaitForSeconds(2.8f);

            // Close-up on the wife's face — her eyes snap open
            Vector3 camFace = bedPos + new Vector3(0.55f, 1.35f, 0.55f);
            yield return StartCoroutine(PanCamera(camWide, camFace, bedPos + new Vector3(0.55f, 0.35f, 0.55f), 1.5f));
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 7: THE DEMON AT THE BEDSIDE ──
            yield return StartCoroutine(FadeOverlay(1, 0.8f));

            float enemyY = bedPos.y - 0.7f;
            var nightEnemyGO = new GameObject("NightEnemyPlaceholder");
            var nightEnemy = nightEnemyGO.transform;
            nightEnemy.position = new Vector3(bedPos.x + 1.8f, enemyY, bedPos.z + 0.2f);
            nightEnemy.rotation = Quaternion.Euler(0f, -90f, 0f);
            nightEnemy.localScale = Vector3.one * 1.1f;
            foreach (var r in nightEnemy.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            foreach (var c in nightEnemy.GetComponentsInChildren<Collider>())
                Object.Destroy(c);
            RegisterSpawned(nightEnemy.gameObject);

            if (_mainCamera != null)
            {
                _mainCamera.transform.position = bedPos + new Vector3(0.8f, 1.5f, 1.1f);
                _mainCamera.transform.LookAt(nightEnemy.position + Vector3.up * 1f);
            }
            yield return StartCoroutine(FadeOverlay(0, 0.8f));
            yield return new WaitForSeconds(2.2f);

            // ── PHASE 8: MORNING — SHE IS GONE ──
            yield return StartCoroutine(FadeOverlay(1, 1.5f));
            GameManager.Instance?.SetTimeOfDay(8f);
            nightWife.SetActive(false);
            nightEnemy.gameObject.SetActive(false);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camWide;
                _mainCamera.transform.LookAt(bedPos + new Vector3(0f, 0.1f, 0.4f));
            }
            yield return StartCoroutine(FadeOverlay(0, 1.5f));
            yield return new WaitForSeconds(2.8f);

            // ── PHASE 9: MORNING — THE POLICE INVESTIGATE THE WIFE'S HOUSE ──
            yield return StartCoroutine(FadeOverlay(1, 1.5f));
            GameManager.Instance?.SetTimeOfDay(10f);
            if (bedroomRoot != null)
                bedroomRoot.SetActive(false);

            var invCar = MapBuilder.BuildPoliceCar(null, new Vector3(19.5f, 0f, 2.5f));
            invCar.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            RegisterSpawned(invCar);

            var invOfficer1 = MapBuilder.BuildPoliceOfficer(null, new Vector3(25.6f, 0.93f, -1.2f), Quaternion.Euler(0f, 90f, 0f));
            foreach (var r in invOfficer1.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(invOfficer1);

            var invOfficer2 = MapBuilder.BuildPoliceOfficer(null, new Vector3(23f, 0.93f, -5.5f), Quaternion.Euler(0f, 90f, 0f));
            foreach (var r in invOfficer2.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(invOfficer2);

            // The player reports and watches from a distance
            justicePlayerModel.transform.position = new Vector3(17.5f, 0.82f, -2f);
            justicePlayerModel.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var patrol = StartCoroutine(PolicePatrol(invOfficer2.transform,
                new Vector3(23f, 0.93f, -5.5f), new Vector3(21f, 0.93f, -3f)));

            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(17f, 2.3f, -5f);
                _mainCamera.transform.LookAt(new Vector3(26f, 1f, -1.5f));
            }
            yield return StartCoroutine(FadeOverlay(0, 1.5f));
            yield return new WaitForSeconds(3f);

            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(24f, 1.9f, -3.2f);
                _mainCamera.transform.LookAt(new Vector3(26f, 1f, -1f));
            }
            yield return new WaitForSeconds(2.5f);
            StopCoroutine(patrol);

            // ── PHASE 10: THE MONK EXPLAINS AT THE PAGODA ──
            yield return StartCoroutine(FadeOverlay(1, 1.5f));
            GameManager.Instance?.SetTimeOfDay(11f);
            invCar.SetActive(false);
            invOfficer1.SetActive(false);
            invOfficer2.SetActive(false);

            var liveMonk = Object.FindAnyObjectByType<PagodaMonkNPC>();
            if (liveMonk != null)
                liveMonk.gameObject.SetActive(false);

            var explainMonk = MapBuilder.BuildMonkNpc(null, new Vector3(26f, 0.86f, 17.5f), Quaternion.identity);
            foreach (var r in explainMonk.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(explainMonk);

            justicePlayerModel.transform.position = new Vector3(26f, 0.82f, 13.5f);
            justicePlayerModel.transform.rotation = Quaternion.identity;

            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(31f, 1.7f, 15.5f);
                _mainCamera.transform.LookAt(new Vector3(26f, 1f, 15.5f));
            }
            yield return StartCoroutine(FadeOverlay(0, 1.5f));
            yield return new WaitForSeconds(3f);

            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(25.6f, 1.9f, 14.2f);
                _mainCamera.transform.LookAt(new Vector3(26.2f, 1.2f, 17.6f));
            }
            yield return new WaitForSeconds(2.5f);

            // ── PHASE 11: FADE + END SCREEN ──
            yield return StartCoroutine(FadeOverlay(1, 2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("CÔNG LÝ ĐƯỢC THỰC THI NHƯNG HIỂM HỌA CHƯA QUA"),
                Localization.T("Cậu đã lật tẩy bộ mặt thật của Phú Ông.\nCảnh sát đã đến, và hắn bị bắt ngay trước dinh thự của chính mình.\n\nĐêm ấy, cậu và Jessica trở về nhà, ngủ say.\nGiữa đêm, cô chợt mở mắt...\nmột con quỷ đang nhìn cô chằm chằm.\n\nSáng hôm sau... Jessica đã biến mất.\nCảnh sát kéo đến điều tra căn nhà, nhưng không tìm được dấu vết nào.\n\nCậu chạy lên chùa tìm thầy. Thầy trầm ngâm:\n\"Jessica không bị người bắt... thứ bước vào đêm ấy là quỷ.\nHãy tìm cô ấy trước khi màn đêm buông xuống.\"\nHiểm họa thật sự vẫn chưa qua."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
            if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = _savedTimeSpeed;
        }
    }

    private GameObject BuildBedroomSet(Vector3 position)
    {
        var root = new GameObject("BedroomSet");
        root.transform.position = position;

        Color floorC = new Color(0.42f, 0.34f, 0.26f);
        Color frameC = new Color(0.38f, 0.28f, 0.16f);
        Color bedC = new Color(0.78f, 0.72f, 0.6f);
        Color coverC = new Color(0.65f, 0.3f, 0.45f);
        Color woodC = new Color(0.45f, 0.3f, 0.15f);

        CreateBlock(root.transform, new Vector3(6f, 0.2f, 6f), new Vector3(0f, 0.1f, 0f), floorC);
        CreateBlock(root.transform, new Vector3(3f, 0.3f, 2.2f), new Vector3(0f, 0.5f, 0f), frameC);
        CreateBlock(root.transform, new Vector3(2.8f, 0.3f, 2f), new Vector3(0f, 0.8f, 0f), bedC);
        CreateBlock(root.transform, new Vector3(2.8f, 0.1f, 1.6f), new Vector3(0f, 1.05f, -0.3f), coverC);
        CreateBlock(root.transform, new Vector3(1.1f, 0.18f, 0.45f), new Vector3(-0.5f, 1.05f, 0.75f), bedC);
        CreateBlock(root.transform, new Vector3(1.1f, 0.18f, 0.45f), new Vector3(0.5f, 1.05f, 0.75f), bedC);
        CreateBlock(root.transform, new Vector3(3.2f, 1.2f, 0.15f), new Vector3(0f, 1.25f, 1.07f), frameC);
        CreateBlock(root.transform, new Vector3(2.4f, 0.6f, 0.12f), new Vector3(0f, 0.85f, -1.13f), frameC);
        CreateBlock(root.transform, new Vector3(0.7f, 0.7f, 0.55f), new Vector3(-1.6f, 0.7f, 0.75f), woodC);
        CreateBlock(root.transform, new Vector3(0.7f, 0.7f, 0.55f), new Vector3(1.6f, 0.7f, 0.75f), woodC);

        return root;
    }
}
