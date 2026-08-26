using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager : MonoBehaviour
{
    private IEnumerator BossBadEndingRoutine(System.Action onComplete = null)
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

            // Player standing on the road before the mansion, facing the oncoming darkness
            float playerZ = 46f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }
            var playerModel = MapBuilder.BuildPlayerModel(null);
            playerModel.transform.position = new Vector3(RoadX, 0.86f, playerZ);
            playerModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in playerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(playerModel);

            // The Demon King rises from the shadows ahead
            float bossZ = playerZ + 22f;
            var bossRoot = BossModelBuilder.BuildBoss(null);
            bossRoot.position = new Vector3(RoadX, 0f, bossZ);
            bossRoot.rotation = Quaternion.Euler(0f, 180f, 0f);
            bossRoot.localScale = Vector3.one * 1.05f;
            foreach (var r in bossRoot.GetComponentsInChildren<Renderer>())
            {
                r.gameObject.layer = 0;
                r.material.color = new Color(0.05f, 0.02f, 0.03f);
            }
            RegisterSpawned(bossRoot.gameObject);

            // Glowing ember eyes
            var eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeL.name = "DemonEyeL";
            Object.Destroy(eyeL.GetComponent<Collider>());
            eyeL.transform.SetParent(bossRoot, false);
            eyeL.transform.localPosition = new Vector3(-0.17f, 1.56f, 0.21f);
            RegisterSpawned(eyeL);
            var eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeR.name = "DemonEyeR";
            Object.Destroy(eyeR.GetComponent<Collider>());
            eyeR.transform.SetParent(bossRoot, false);
            eyeR.transform.localPosition = new Vector3(0.17f, 1.56f, 0.21f);
            RegisterSpawned(eyeR);
            foreach (var e in new[] { eyeL, eyeR })
            {
                e.transform.localScale = Vector3.one * 0.18f;
                var er = e.GetComponent<Renderer>();
                if (er != null)
                    er.material.color = new Color(0.9f, 0.05f, 0.03f);
            }

            // ── PHASE 1: OPENING SHOT ──
            Vector3 camStart = new Vector3(RoadX, 1.6f, playerZ - 3f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(RoadX, 2.2f, bossZ));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2.2f);

            // ── PHASE 2: THE RISE ──
            float riseDur = 3.5f;
            float riseTimer = 0f;
            while (riseTimer < riseDur)
            {
                riseTimer += Time.deltaTime;
                float p = Mathf.Min(riseTimer / riseDur, 1f);
                bossRoot.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.65f, p);
                bossRoot.position = new Vector3(RoadX, 0f, Mathf.Lerp(bossZ, bossZ - 3f, p));
                Color eyeGlow = new Color(Mathf.Lerp(0.9f, 1f, p), Mathf.Lerp(0.05f, 0.3f, p), 0.03f);
                foreach (var e in new[] { eyeL, eyeR })
                {
                    var er = e.GetComponent<Renderer>();
                    if (er != null) er.material.color = eyeGlow;
                }
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX, 2.2f, playerZ - 1.5f), p);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 2.4f, bossZ));
                }
                yield return null;
            }
            yield return new WaitForSeconds(1f);

            // ── PHASE 3: THE LUNGE ──
            float lungeDur = 2.2f;
            float lungeTimer = 0f;
            Vector3 bossStart = bossRoot.position;
            Vector3 bossEnd = new Vector3(RoadX, 0f, playerZ + 2f);
            while (lungeTimer < lungeDur)
            {
                lungeTimer += Time.deltaTime;
                float p = Mathf.Min(lungeTimer / lungeDur, 1f);
                bossRoot.position = Vector3.Lerp(bossStart, bossEnd, p);
                float shake = 0.4f - 0.35f * p;
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = new Vector3(
                        RoadX + Random.Range(-shake, shake),
                        2.2f + Random.Range(-shake * 0.5f, shake * 0.5f),
                        playerZ - 1.5f);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 2f, bossRoot.position.z));
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            // ── PHASE 4: BLACKOUT ──
            yield return StartCoroutine(FadeOverlay(1, 1.2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            _previewOnComplete = null;
            if (onComplete != null)
            {
                RestoreAfterPreview();
                onComplete();
            }
            else
            {
                if (_uiManager == null)
                    _uiManager = Object.FindAnyObjectByType<UIManager>();
                if (_uiManager != null)
                    _uiManager.ShowBossEndScreen(
                        Localization.T("RƠI VÀO BÓNG TỐI"),
                        Localization.T("Quỷ Vương đã quật ngã con.\nBóng tối nuốt chửng ngôi làng.\n\nSố phận của con dừng lại tại đây...\nHãy quay về nơi lưu gần nhất và đối mặt với nó lần nữa."));
            }
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }
}
