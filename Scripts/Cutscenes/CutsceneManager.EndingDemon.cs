using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager 
{
    private IEnumerator DemonEndingRoutine(System.Action onComplete = null)
    {
        GameObject realWife = WifeNPC.Instance != null ? WifeNPC.Instance.gameObject : null;
        bool wifeWasActive = realWife != null && realWife.activeInHierarchy;
        GameObject staticWife = WorldBuilder.Instance != null ? WorldBuilder.Instance.StaticWifeModel : null;
        bool staticWifeWasActive = staticWife != null && staticWife.activeInHierarchy;
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

            // Player standing alone at the road turn where the Demon King fell
            float playerZ = 78f;
            if (_player != null)
            {
                _player.transform.position = new Vector3(RoadX, 0f, playerZ);
                _player.transform.rotation = Quaternion.identity;
                var realModel = _player.transform.Find("PlayerModel");
                if (realModel != null)
                    realModel.gameObject.SetActive(false);
            }
            var heroModel = MapBuilder.BuildPlayerModel(null);
            heroModel.transform.position = new Vector3(RoadX, 0.82f, playerZ);
            heroModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in heroModel.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(heroModel);

            // Scorched ground where the Demon King was slain â€” at the road turn junction
            float bossZ = 90f;
            var scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            scorch.name = "DemonScorch";
            scorch.transform.position = new Vector3(RoadX, 0.01f, bossZ);
            scorch.transform.localScale = new Vector3(4f, 0.02f, 4f);
            var scorchR = scorch.GetComponent<Renderer>();
            if (scorchR != null) scorchR.material.color = new Color(0.12f, 0.04f, 0.05f);
            Object.Destroy(scorch.GetComponent<Collider>());
            RegisterSpawned(scorch);

            // The fallen Demon King lying where it was slain
            var fallenKing = BossModelBuilder.BuildBoss(null);
            fallenKing.position = new Vector3(RoadX, 0.18f, bossZ);
            fallenKing.rotation = Quaternion.Euler(-90f, 180f, 0f);
            foreach (var r in fallenKing.GetComponentsInChildren<Renderer>())
            {
                r.gameObject.layer = 0;
                r.material.color = r.material.color * new Color(0.35f, 0.3f, 0.3f);
            }
            RegisterSpawned(fallenKing.gameObject);

            // â”€â”€ PHASE 1: OPENING SHOT â”€â”€
            Vector3 camStart = new Vector3(RoadX, 2.2f, playerZ - 4f);
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = camStart;
                _mainCamera.transform.LookAt(new Vector3(RoadX, 1.2f, playerZ + 2f));
            }
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(2.2f);

            // â”€â”€ PHASE 2: THE FALLEN KING'S EMBER â”€â”€
            var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ember.name = "DemonEmber";
            ember.transform.position = new Vector3(RoadX, 1f, bossZ);
            ember.transform.localScale = Vector3.one * 0.5f;
            var emberR = ember.GetComponent<Renderer>();
            if (emberR != null) emberR.material.color = new Color(1f, 0.4f, 0.1f);
            Object.Destroy(ember.GetComponent<Collider>());
            RegisterSpawned(ember);

            float glowDur = 3f;
            float glowTimer = 0f;
            while (glowTimer < glowDur)
            {
                glowTimer += Time.deltaTime;
                float p = Mathf.Min(glowTimer / glowDur, 1f);
                ember.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.2f, p);
                if (emberR != null)
                    emberR.material.color = new Color(1f, 0.4f - 0.2f * p, 0.1f, 1f);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(camStart, new Vector3(RoadX - 2f, 2.8f, bossZ + 4f), p);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 1f, bossZ));
                }
                yield return null;
            }
            ember.SetActive(false);
            yield return new WaitForSeconds(1f);

            // â”€â”€ PHASE 3: SMOKE RISES FROM THE REMAINS â”€â”€
            var smokeGO = new GameObject("DemonSmoke");
            smokeGO.transform.position = new Vector3(RoadX, 0.3f, bossZ);
            var ps = smokeGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 3f;
            main.startSpeed = 0.5f;
            main.startSize = 0.8f;
            main.startColor = new Color(0.08f, 0.06f, 0.07f);
            main.maxParticles = 30;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 8f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.5f;
            var renderer = smokeGO.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
                renderer.material.color = new Color(0.08f, 0.06f, 0.07f, 0.6f);
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
            RegisterSpawned(smokeGO);
            yield return new WaitForSeconds(2f);

            // â”€â”€ PHASE 4: CAMERA TURNS TOWARD THE VILLAGE â”€â”€
            float panDur = 4f;
            float panTimer = 0f;
            while (panTimer < panDur)
            {
                panTimer += Time.deltaTime;
                float p = Mathf.Min(panTimer / panDur, 1f);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = new Vector3(RoadX - 1f, 2.4f, playerZ + 2f);
                    Vector3 lookTarget = Vector3.Lerp(new Vector3(RoadX, 1f, playerZ + 8f), new Vector3(RoadX, 1f, playerZ - 20f), p);
                    _mainCamera.transform.LookAt(lookTarget);
                }
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);

            // â”€â”€ PHASE 5: THE HERO WALKS AWAY â”€â”€
            _walkAnimRoutine = StartCoroutine(WalkAnimation(heroModel, 2.5f));

            float walkZ = playerZ;
            float walkEndZ = playerZ - 34f;
            while (walkZ > walkEndZ)
            {
                walkZ -= 2.5f * Time.deltaTime;
                heroModel.transform.position = new Vector3(RoadX, 0.82f, walkZ);
                heroModel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = new Vector3(RoadX - 1.2f, 2f, walkZ + 4f);
                    _mainCamera.transform.LookAt(new Vector3(RoadX, 1.1f, walkZ));
                }
                yield return null;
            }
            StopWalkAnimation();
            ResetLimbRotations(heroModel);
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(FadeOverlay(1, 2f));

            // â”€â”€ PHASE 6: THE VILLAGE â€” A QUIET TRAGEDY â”€â”€
            if (GameManager.Instance != null) GameManager.Instance.SetTimeOfDay(8f);
            if (realWife != null) realWife.SetActive(false);
            if (staticWife != null) staticWife.SetActive(false);

            // Jessica lies dead on the open ground before her house (house wall x=26..40 behind her)
            Vector3 corpsePos = new Vector3(24.2f, 0.12f, -0.8f);
            var corpse = WifeNPC.BuildWifeNpc(null, corpsePos, 1f, Quaternion.Euler(-90f, 180f, 0f));
            foreach (var r in corpse.GetComponentsInChildren<Renderer>())
            {
                r.gameObject.layer = 0;
                r.material.color = r.material.color * new Color(0.55f, 0.45f, 0.45f);
            }
            foreach (var c in corpse.GetComponentsInChildren<Collider>())
                Object.Destroy(c);
            RegisterSpawned(corpse);

            var blood = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blood.name = "BloodPool";
            blood.transform.position = new Vector3(corpsePos.x, 0.025f, corpsePos.z);
            blood.transform.localScale = new Vector3(1.6f, 0.02f, 1.1f);
            var bloodR = blood.GetComponent<Renderer>();
            if (bloodR != null) bloodR.material.color = new Color(0.42f, 0.02f, 0.02f);
            Object.Destroy(blood.GetComponent<Collider>());
            RegisterSpawned(blood);

            // The addict, hunched over the body
            var addict = MapBuilder.BuildAddictNpc(null, new Vector3(24.8f, 0f, 0.9f));
            addict.transform.rotation = Quaternion.LookRotation((corpsePos - addict.transform.position).normalized) * Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in addict.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(addict);

            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(19.8f, 2f, -4.5f);
                _mainCamera.transform.LookAt(corpsePos + Vector3.up * 0.6f);
            }
            ShowDemonCaption(Localization.T("Jessica Ä‘Ã£ bá»‹ háº¡ sÃ¡t ngay trÆ°á»›c hiÃªn nhÃ ."));
            yield return StartCoroutine(FadeOverlay(0, 2f));
            yield return new WaitForSeconds(3.2f);

            // â”€â”€ PHASE 7: THE ADDICT â€” HUNCHED OVER THE BODY â”€â”€
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(23f, 1.3f, 0.6f);
                _mainCamera.transform.LookAt(addict.transform.position + Vector3.up * 0.8f);
            }
            ShowDemonCaption(Localization.T("KhÃ´ng pháº£i tÃ´i... tÃ´i khÃ´ng kiá»ƒm soÃ¡t Ä‘Æ°á»£c ná»¯a..."));
            yield return StartCoroutine(FadeOverlay(1, 1f));
            yield return StartCoroutine(FadeOverlay(0, 1f));
            yield return new WaitForSeconds(3f);

            // â”€â”€ PHASE 8: POLICE ARRIVE AT THE HOUSE â”€â”€
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(16.5f, 2.4f, 3.5f);
                _mainCamera.transform.LookAt(new Vector3(22f, 1f, -0.5f));
            }
            ShowDemonCaption(Localization.T("Cáº£nh sÃ¡t nhanh chÃ³ng cÃ³ máº·t."));
            yield return StartCoroutine(FadeOverlay(1, 1f));
            yield return StartCoroutine(FadeOverlay(0, 1f));

            var officerA = MapBuilder.BuildPoliceOfficer(null, new Vector3(12.8f, 0.93f, 8f), Quaternion.identity);
            foreach (var r in officerA.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(officerA);
            var officerB = MapBuilder.BuildPoliceOfficer(null, new Vector3(15.2f, 0.93f, 8f), Quaternion.identity);
            foreach (var r in officerB.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            RegisterSpawned(officerB);

            Vector3 officerAStart = officerA.transform.position;
            Vector3 officerBStart = officerB.transform.position;
            Vector3 officerAEnd = new Vector3(25.4f, 0.93f, -1.2f);
            Vector3 officerBEnd = new Vector3(24.6f, 0.93f, 0.5f);
            float walkDur = 4f;
            float walkTimer = 0f;
            while (walkTimer < walkDur)
            {
                walkTimer += Time.deltaTime;
                float p = Mathf.Min(walkTimer / walkDur, 1f);
                officerA.transform.position = Vector3.Lerp(officerAStart, officerAEnd, p);
                officerB.transform.position = Vector3.Lerp(officerBStart, officerBEnd, p);
                FaceMoveDirection(officerA.transform, officerAEnd - officerAStart);
                FaceMoveDirection(officerB.transform, officerBEnd - officerBStart);
                if (_mainCamera != null)
                {
                    _mainCamera.transform.position = Vector3.Lerp(new Vector3(16.5f, 2.4f, 3.5f), new Vector3(19f, 2.2f, -1.5f), p);
                    _mainCamera.transform.LookAt(new Vector3(23f, 0.9f, -0.5f));
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            ShowDemonCaption(Localization.T("Há» báº¯t giá»¯ káº» nghiá»‡n ngáº­p... nhÆ°ng káº» gÃ¢y Ã¡n chá»‰ lÃ  bá» ná»•i."));
            yield return new WaitForSeconds(2.4f);

            // â”€â”€ PHASE 9: FINAL LOOK â”€â”€
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = new Vector3(20.5f, 2f, -6f);
                _mainCamera.transform.LookAt(new Vector3(23.5f, 0.9f, -1f));
            }
            yield return StartCoroutine(FadeOverlay(1, 1f));
            yield return StartCoroutine(FadeOverlay(0, 1f));
            yield return new WaitForSeconds(3f);

            HideDemonCaption();
            DestroyDemonUI();

            // â”€â”€ FADE + END SCREEN â”€â”€
            yield return StartCoroutine(FadeOverlay(1, 2f));

            HideSkipButton();
            CleanupSpawned();
            DestroyLetterboxBars();
            DestroyOverlay();

            FinishEndingScene(onComplete,
                Localization.T("QUá»¶ VÆ¯Æ NG ÄÃƒ CHáº¾T NHÆ¯NG CÃI ÃC CHÆ¯A Háº¾T"),
                Localization.T("Quá»· VÆ°Æ¡ng Ä‘Ã£ bá»‹ Ä‘Ã¡nh báº¡i, bÃ³ng tá»‘i bá»‹ Ä‘áº©y lÃ¹i.\nNhÆ°ng khi cáº­u quay vá» lÃ ng...\nJessica Ä‘Ã£ bá»‹ má»™t káº» nghiá»‡n ngáº­p do ma tÃºy cá»§a PhÃº Ã”ng háº¡ sÃ¡t.\n\nKáº» gÃ¢y Ã¡n chá»‰ lÃ  bá» ná»•i...\nCÃ³ thá»ƒ Ä‘Ã¢y lÃ  mÆ°u Ä‘á»“ cá»§a lÅ© quá»·.\nCÃ¡i Ã¡c chÆ°a bá»‹ nhá»• táº­n gá»‘c.\nNgÃ´i lÃ ng chÆ°a thá»ƒ yÃªn bÃ¬nh."));
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
            if (realWife != null && wifeWasActive) realWife.SetActive(true);
            if (staticWife != null && staticWifeWasActive) staticWife.SetActive(true);
        }
    }

    private void ShowDemonCaption(string text)
    {
        if (_canvas == null)
            _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null) return;

        if (_demonUI == null)
        {
            _demonUI = new GameObject("DemonEndingUI");
            _demonUI.transform.SetParent(_canvas.transform, false);

            var bg = _demonUI.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.65f);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = new Vector2(0.5f, 0f);
            bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.pivot = new Vector2(0.5f, 0f);
            bgRt.anchoredPosition = new Vector2(0f, 24f);
            bgRt.sizeDelta = new Vector2(1500f, 110f);

            var cap = new GameObject("Caption");
            cap.transform.SetParent(_demonUI.transform, false);
            _demonCaption = cap.AddComponent<TextMeshProUGUI>();
            _uiManager?.ApplyDefaultFont(_demonCaption);
            _demonCaption.fontSize = 26;
            _demonCaption.color = Color.white;
            _demonCaption.alignment = TextAlignmentOptions.Center;
            _demonCaption.enableWordWrapping = true;
            var capRt = cap.GetComponent<RectTransform>();
            capRt.anchorMin = Vector2.zero;
            capRt.anchorMax = Vector2.one;
            capRt.offsetMin = new Vector2(40f, 10f);
            capRt.offsetMax = new Vector2(-40f, -10f);
            capRt.pivot = new Vector2(0.5f, 0.5f);
        }

        _demonCaption.text = text;
        _demonUI.SetActive(true);
    }

    private void HideDemonCaption()
    {
        if (_demonUI != null)
            _demonUI.SetActive(false);
    }

    private void DestroyDemonUI()
    {
        if (_demonUI != null)
        {
            Destroy(_demonUI);
            _demonUI = null;
        }
        _demonCaption = null;
    }
}
