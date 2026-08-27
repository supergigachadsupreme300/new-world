using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager 
{
    private IEnumerator NtrRoutine(System.Action onComplete = null)
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

            // ── Reposition player on road behind the pickup spot ──
            float playerZ = SadStartZ + 10f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;

                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }

            var ntrPlayerModel = MapBuilder.BuildPlayerModel(null);
            ntrPlayerModel.transform.position = new Vector3(RoadX, 0.82f, playerZ);
            ntrPlayerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in ntrPlayerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(ntrPlayerModel);

            // ── PHASE 1: THE PLAYER WATCHES (2.5s) ──
            Vector3 camStart = new Vector3(RoadX, 1.8f, playerZ - 3f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(RoadX, 1.2f, playerZ));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2.5f);

            // ── PHASE 2: THE GOLD CAR ARRIVES ──
            float stopZ = -6f;
            var carRoot = MapBuilder.BuildCar(null,
                new Vector3(RoadX, 0f, stopZ), new Color(0.92f, 0.78f, 0.25f));
            carRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            RegisterSpawned(carRoot);

            var wifeModel = WifeNPC.BuildWifeNpc(null,
                new Vector3(RoadX, 0.86f, -1.5f), 1f, Quaternion.Euler(0f, 180f, 0f));
            foreach (var r in wifeModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(wifeModel);

            var richModel = RichManNPC.BuildRichManNpc(null,
                new Vector3(RoadX - 1.8f, 0.86f, -4.2f), 1f, Quaternion.Euler(0f, 0f, 0f), false);
            foreach (var r in richModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(richModel);

            // Pan camera from player face to the trio
            Vector3 camPickup = new Vector3(RoadX - 2f, 2.6f, stopZ + 9f);
            yield return StartCoroutine(PanCamera(camStart, camPickup, new Vector3(RoadX, 1f, stopZ + 2f), 3f));

            // ── PHASE 3: HE TAKES HER AWAY (walk to car) ──
            Vector3 wifeStart = wifeModel.transform.position;
            Vector3 richStart = richModel.transform.position;
            Vector3 wifeDoor = new Vector3(RoadX + 0.6f, 0.86f, stopZ - 1f);
            Vector3 richDoor = new Vector3(RoadX - 0.6f, 0.86f, stopZ - 1f);

            float walkDur = 4f;
            float wt = 0f;
            while (wt < walkDur)
            {
                wt += Time.deltaTime;
                float p = Mathf.Min(wt / walkDur, 1f);
                wifeModel.transform.position = Vector3.Lerp(wifeStart, wifeDoor, p);
                richModel.transform.position = Vector3.Lerp(richStart, richDoor, p);
                FaceMoveDirection(wifeModel.transform, wifeDoor - wifeModel.transform.position);
                FaceMoveDirection(richModel.transform, richDoor - richModel.transform.position);
                yield return null;
            }

            // Board the car
            wifeModel.transform.SetParent(carRoot.transform);
            wifeModel.transform.localPosition = new Vector3(0.35f, 0.62f, -0.2f);
            wifeModel.transform.localRotation = Quaternion.identity;
            richModel.transform.SetParent(carRoot.transform);
            richModel.transform.localPosition = new Vector3(-0.35f, 0.62f, -0.2f);
            richModel.transform.localRotation = Quaternion.identity;

            yield return new WaitForSeconds(1.5f);

            // ── PHASE 4: THE JOURNEY (12s) ──
            float rideDur = 12f;
            float rideTimer = 0f;
            float deltaZ = SadEndZ - stopZ;
            bool wifeLookedBack = false;

            while (rideTimer < rideDur)
            {
                rideTimer += Time.deltaTime;
                float p = Mathf.Min(rideTimer / rideDur, 1f);
                float z = stopZ + deltaZ * p;
                carRoot.transform.position = new Vector3(RoadX, 0f, z);

                if (!wifeLookedBack && p >= 0.6f)
                {
                    wifeLookedBack = true;
                    _wifeLookBackRoutine = StartCoroutine(WifeLookBack(wifeModel.transform));
                }

                if (_mainCamera != null)
                {
                    float camZ = z + 10f;
                    _mainCamera.transform.position = new Vector3(RoadX - 2f, 3.5f, camZ);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, z));
                }
                yield return null;
            }

            // ── PHASE 5: FADING AWAY (5s) ──
            yield return new WaitForSeconds(3f);
            yield return StartCoroutine(FadeOverlay(1, 2f));

            // ── PHASE 6: END SCREEN ──
            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("KẾT THÚC NTR"),
                Localization.T("Trong lúc cậu mải làm nông, ông chú giàu có đã lặng lẽ đến gần cô ấy.\n\nKhi cậu quay lại...\nJessica đã không còn đợi cậu nữa.\n\nCậu đã để cô ấy ra đi, mãi mãi."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }
}
