using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager : MonoBehaviour
{
    private IEnumerator BlackmailEndingRoutine(System.Action onComplete = null)
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

            if (_player != null)
            {
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }

            // Player model at the mansion front
            var playerModel = MapBuilder.BuildPlayerModel(null);
            playerModel.transform.position = new Vector3(68f, 0.86f, 100f);
            playerModel.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            foreach (var r in playerModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(playerModel);

            // Rich man facing the player
            var richModel = RichManNPC.BuildRichManNpc(null,
                new Vector3(71.5f, 0.86f, 100f), 1f, Quaternion.Euler(0f, 90f, 0f), false);
            foreach (var r in richModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(richModel);

            // Bribe sack on the ground between them
            var sack = BuildBribeSack(new Vector3(69.8f, 0.45f, 100f));
            RegisterSpawned(sack);

            // ── PHASE 1: OPENING SHOT ──
            Vector3 camStart = new Vector3(68f, 2.4f, 95f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(69.8f, 1.1f, 100f));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2f);

            // ── PHASE 2: PAN TO THE SACK ──
            yield return StartCoroutine(PanCamera(camStart, new Vector3(69.8f, 2.2f, 96f), new Vector3(69.8f, 1f, 100f), 2f));
            yield return new WaitForSeconds(1.5f);

            // ── PHASE 3: THE PLAYER TAKES THE BRIBE ──
            Vector3 playerStart = playerModel.transform.position;
            Vector3 sackGrab = new Vector3(69.8f, 0.86f, 100f);
            float walkDur = 2.5f;
            float walkTimer = 0f;
            while (walkTimer < walkDur)
            {
                walkTimer += Time.deltaTime;
                float p = Mathf.Min(walkTimer / walkDur, 1f);
                playerModel.transform.position = Vector3.Lerp(playerStart, sackGrab, p);
                playerModel.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                yield return null;
            }
            sack.transform.SetParent(playerModel.transform);
            sack.transform.localPosition = new Vector3(0.35f, 0.35f, 0f);

            yield return new WaitForSeconds(2f);

            // ── PHASE 4: FADE + END SCREEN ──
            yield return StartCoroutine(FadeOverlay(1, 2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("KẾT THÚC ĐỒI BẠI"),
                Localization.T("Cậu đã im lặng. Và cậu đã được trả một cái giá rất hậu hĩnh.\n\nNhưng đêm xuống, những chiếc xe vẫn nối đuôi nhau đến dinh thự.\nJessica vẫn đang trong tầm ngắm của hắn...\n\nVà giờ, cậu là một phần của câu chuyện đó."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
        }
    }

    private GameObject BuildBribeSack(Vector3 position)
    {
        var sack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sack.name = "BribeSack";
        sack.transform.position = position;
        sack.transform.localScale = new Vector3(0.7f, 0.55f, 0.55f);
        var r = sack.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.62f, 0.5f, 0.2f);
        Object.Destroy(sack.GetComponent<Collider>());
        return sack;
    }
}
