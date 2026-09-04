using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager 
{
    private IEnumerator SadEndingRoutine(System.Action onComplete = null)
    {
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

        // ── Reposition player on road behind wagon spawn ──
        float playerSadZ = SadStartZ + 10f;
        if (_player != null)
        {
            _player.transform.position = new Vector3(RoadX, 0f, playerSadZ);
            _player.transform.rotation = Quaternion.identity;

            // Hide real player model (on layer 6, not visible to camera)
            var realModel = _player.transform.Find("PlayerModel");
            if (realModel != null)
                realModel.gameObject.SetActive(false);
        }

        // Spawn a standalone player model on default layer (visible to camera)
        var sadPlayerModel = MapBuilder.BuildPlayerModel(null);
        sadPlayerModel.transform.position = new Vector3(RoadX, 0.82f, playerSadZ);
        sadPlayerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        foreach (var r in sadPlayerModel.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(sadPlayerModel);

        // ── PHASE 1: THE PLAYER WATCHES (5s) ──
        // Camera in front of player, looking at his face
        Vector3 camStart = new Vector3(RoadX, 1.8f, playerSadZ - 3f);
        Vector3 lookAtPlayer = new Vector3(RoadX, 1.2f, playerSadZ);
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = camStart;
            _mainCamera.transform.LookAt(lookAtPlayer);
        }
        yield return StartCoroutine(FadeOverlay(0, 2f));
        yield return new WaitForSeconds(3f);

        // ── PHASE 2: THE WAGON APPEARS (6s) ──
        float wagonStartZ = SadStartZ;
        float wagonEndZ = SadEndZ;
        float deltaZ = wagonEndZ - wagonStartZ;

        var wagonRoot = CreateWagon(RoadX, wagonStartZ);

        var horse = HorseModelBuilder.BuildHorse(wagonRoot);
        horse.localPosition = new Vector3(0f, 0f, -3.2f);
        RegisterSpawned(horse.gameObject);

        var enemy = EnemyModelBuilder.BuildRegularEnemy(wagonRoot);
        enemy.localPosition = new Vector3(0.35f, 0.56f, -0.6f);
        RegisterSpawned(enemy.gameObject);

        var wife = WifeNPC.BuildWifeNpc(wagonRoot,
            new Vector3(-0.3f, 1.42f, 0.3f), 1f,
            Quaternion.Euler(0, 180, 0));
        RegisterSpawned(wife);

        // Pan camera from player's face to the departing wagon
        float panDur = 6f;
        float panTimer = 0f;
        Vector3 panEnd = new Vector3(RoadX - 3f, 3f, wagonStartZ + 6f);

        while (panTimer < panDur)
        {
            panTimer += Time.deltaTime;
            float pt = Mathf.SmoothStep(0f, 1f, panTimer / panDur);
            _mainCamera.transform.position = Vector3.Lerp(camStart, panEnd, pt);
            _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, wagonStartZ));
            yield return null;
        }

        // ── PHASE 3: THE JOURNEY (16s) ──
        float rideDur = 16f;
        float rideTimer = 0f;
        bool wifeLookedBack = false;

        while (rideTimer < rideDur)
        {
            rideTimer += Time.deltaTime;
            float p = Mathf.Min(rideTimer / rideDur, 1f);
            float z = wagonStartZ + deltaZ * p;

            wagonRoot.position = new Vector3(RoadX, 0f, z);

            if (!wifeLookedBack && p >= 0.65f && wife != null)
            {
                wifeLookedBack = true;
                _wifeLookBackRoutine = StartCoroutine(WifeLookBack(wife.transform));
            }

            float camZ = z + 10f;
            _mainCamera.transform.position = new Vector3(RoadX - 2f, 3.5f, camZ);
            _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, z));

            yield return null;
        }

        // ── PHASE 4: FADING AWAY (5s) ──
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(FadeOverlay(1, 2f));

        // ── PHASE 5: END SCREEN ──
        HideSkipButton();
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();

        FinishEndingScene(onComplete,
            Localization.T("KẾT THÚC BUỒN"),
            Localization.T("Bạn đã đến quá muộn.\nTrong khi bạn đi tìm kiếm giàu sang,\nbạn đã quên đi điều thực sự quan trọng.\n\nCô ấy đợi...\ncho đến khi không thể đợi nữa."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    // ── Wagon (sad ending) ──

    private Transform CreateWagon(float wx, float wz)
    {
        Color brn = new Color(85f / 255f, 52f / 255f, 22f / 255f);
        Color tan = new Color(210f / 255f, 195f / 255f, 160f / 255f);
        Color dk = new Color(70f / 255f, 42f / 255f, 16f / 255f);
        Color wblk = new Color(45f / 255f, 35f / 255f, 20f / 255f);

        var root = new GameObject("WagonRoot").transform;
        root.position = new Vector3(wx, 0f, wz);

        CreateBlock(root, new Vector3(1.6f, 0.12f, 3.2f), new Vector3(0f, 0.5f, 0f), brn);
        CreateBlock(root, new Vector3(0.08f, 0.5f, 3.2f), new Vector3(-0.8f, 0.8f, 0f), dk);
        CreateBlock(root, new Vector3(0.08f, 0.5f, 3.2f), new Vector3(0.8f, 0.8f, 0f), dk);
        CreateBlock(root, new Vector3(1.6f, 0.6f, 0.08f), new Vector3(0f, 0.85f, -1.6f), dk);
        CreateBlock(root, new Vector3(1.2f, 0.4f, 0.08f), new Vector3(0f, 0.75f, 1.6f), dk);
        CreateBlock(root, new Vector3(1.0f, 0.08f, 0.4f), new Vector3(0f, 0.85f, -0.6f), brn);
        CreateBlock(root, new Vector3(0.06f, 0.35f, 0.06f), new Vector3(-0.4f, 0.67f, -0.6f), brn);
        CreateBlock(root, new Vector3(0.06f, 0.35f, 0.06f), new Vector3(0.4f, 0.67f, -0.6f), brn);
        CreateBlock(root, new Vector3(0.08f, 0.08f, 1.4f), new Vector3(0f, 0.4f, -2.3f), dk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(-0.85f, 0.35f, -1.3f), wblk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(0.85f, 0.35f, -1.3f), wblk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(-0.85f, 0.35f, 1.3f), wblk);
        CreateBlock(root, new Vector3(0.15f, 0.7f, 0.7f), new Vector3(0.85f, 0.35f, 1.3f), wblk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.8f), new Vector3(0f, 0.25f, -1.3f), dk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.8f), new Vector3(0f, 0.25f, 1.3f), dk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.6f), new Vector3(0f, 0.38f, -0.4f), dk);
        CreateBlock(root, new Vector3(0.06f, 0.06f, 1.6f), new Vector3(0f, 0.38f, 0.4f), dk);

        RegisterSpawned(root.gameObject);
        return root;
    }
}
