using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager 
{
    // ═══════════════════════════════════════════════
    //  HAPPY ENDING
    // ═══════════════════════════════════════════════

    private IEnumerator HappyEndingRoutine(System.Action onComplete = null)
    {
        _savedTimeSpeed = GameManager.Instance != null ? GameManager.Instance.TimeSpeed : 0.01f;
        try
        {
        _player = GameManager.Instance?.Player;
        if (_player == null)
        {
            yield break;
        }
        if (_uiManager == null)
            _uiManager = Object.FindAnyObjectByType<UIManager>();

        if (_uiManager != null)
            _uiManager.ShowMainMenu(false);

        _player.transform.position = new Vector3(RoadX - 0.8f, 1f, HappyStartZ);
        _player.transform.rotation = Quaternion.identity;

        DetachCamera();
        DisablePlayerControl();
        HideHUD();
        ShowSkipButton();

        // Hide real player model (layer 6, invisible to camera) and spawn visible cutscene model
        var realModel = _player.transform.Find("PlayerModel");
        if (realModel != null)
            realModel.gameObject.SetActive(false);

        _happyPlayerModel = MapBuilder.BuildPlayerModel(null);
        _happyPlayerModel.transform.position = new Vector3(RoadX - 0.8f, 0.82f, HappyStartZ);
        _happyPlayerModel.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        foreach (var r in _happyPlayerModel.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(_happyPlayerModel);
        _walkAnimRoutine = StartCoroutine(WalkAnimation(_happyPlayerModel, WalkSpeed));

        _tetoRoot = CreateTeto(new Vector3(RoadX + 0.8f, 1f, HappyStartZ - 1.5f));
        _tetoBody = _tetoRoot?.transform.Find("BodyRoot");
        _tetoLeftArm = _tetoRoot?.transform.Find("LeftArmRoot");
        _tetoRightArm = _tetoRoot?.transform.Find("RightArmRoot");
        var legsRoot = _tetoRoot?.transform.Find("LegsRoot");
        _tetoLegL = legsRoot?.Find("LegL");
        _tetoLegR = legsRoot?.Find("LegR");
        _tetoLowerLegL = _tetoLegL?.Find("LowerLegL");
        _tetoLowerLegR = _tetoLegR?.Find("LowerLegR");

        _happyPhase = 0;
        _happyElapsed = 0;

        GameManager.Instance?.SetTimeOfDay(5.5f);
        _savedTimeSpeed = GameManager.Instance != null ? GameManager.Instance.TimeSpeed : 0.01f;
        if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = 0;

        while (_happyPhase == 0)
        {
            _happyElapsed += Time.deltaTime;

            if (_player.transform.position.z < HappyEndZ)
            {
                Vector3 pos = _player.transform.position;
                pos.z += WalkSpeed * Time.deltaTime;
                pos.x = RoadX - 1f;
                _player.transform.position = pos;
                _player.transform.rotation = Quaternion.identity;

                if (_happyPlayerModel != null)
                {
                    _happyPlayerModel.transform.position = new Vector3(pos.x, 0.82f, pos.z);
                    _happyPlayerModel.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                }

                float side = Mathf.Sin(_happyElapsed * SwingSpeed);
                float tz = _player.transform.position.z - 1.2f + Mathf.Cos(_happyElapsed * SwingSpeed) * 0.3f;
                float tx = RoadX + side * LateralSwing;
                if (_tetoRoot != null)
                {
                    float skip = Mathf.Sin(_happyElapsed * SkipSpeed);
                    float bounce = Mathf.Pow(Mathf.Max(0, skip), 2) * SkipHeight;
                    float tilt = skip * 4f;
                    float armSwing = skip * ArmSwingAngle;
                    _tetoRoot.transform.position = new Vector3(tx, 1f + bounce, tz);
                    _tetoRoot.transform.rotation = Quaternion.Euler(0, (side >= 0 ? 10 : -10) + 180, tilt);
                    if (_tetoLeftArm != null)
                        _tetoLeftArm.localRotation = Quaternion.Euler(armSwing, 0f, -15f);
                    if (_tetoRightArm != null)
                        _tetoRightArm.localRotation = Quaternion.Euler(-armSwing, 0f, 15f);
                    float legPhase = Mathf.Sin(_happyElapsed * SkipSpeed * 0.5f);
                    float kickAngle = 35f;
                    if (_tetoLegL != null)
                        _tetoLegL.localRotation = Quaternion.identity;
                    if (_tetoLegR != null)
                        _tetoLegR.localRotation = Quaternion.identity;
                    if (_tetoLowerLegL != null)
                        _tetoLowerLegL.localRotation = Quaternion.Euler(-Mathf.Max(0, legPhase) * kickAngle, 0f, 0f);
                    if (_tetoLowerLegR != null)
                        _tetoLowerLegR.localRotation = Quaternion.Euler(-Mathf.Max(0, -legPhase) * kickAngle, 0f, 0f);
                }
            }
            else
            {
                StopWalkAnimation();
                ResetLimbRotations(_happyPlayerModel);
                if (_tetoLegL != null) _tetoLegL.localRotation = Quaternion.identity;
                if (_tetoLegR != null) _tetoLegR.localRotation = Quaternion.identity;
                if (_tetoLowerLegL != null) _tetoLowerLegL.localRotation = Quaternion.identity;
                if (_tetoLowerLegR != null) _tetoLowerLegR.localRotation = Quaternion.identity;
                _player.transform.position = new Vector3(RoadX - 1f, 1f, HappyEndZ);
                _happyPhase = 1;
                _happyPhaseTimer = 0;
            }

            Vector3 refPos = _tetoRoot != null ? _tetoRoot.transform.position : _player.transform.position;
            Vector3 mid = (_player.transform.position + refPos) * 0.5f;
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = mid + new Vector3(-6, 4.5f, -9);
                _mainCamera.transform.LookAt(mid + Vector3.up * 1.8f);
            }

            yield return null;
        }

        FaceEachOther();
        yield return new WaitForSeconds(0.8f);

        _happyPhase = 2;
        _happyPhaseTimer = 0;
        _happyJumpCount = 0;

        var wb = WorldBuilder.Instance;
        if (wb != null)
        {
            Vector3 fwCenter = _tetoRoot != null
                ? (_player.transform.position + _tetoRoot.transform.position) * 0.5f + new Vector3(0f, 4f, -8f)
                : _player.transform.position + new Vector3(0f, 4f, -8f);
            RandomEventManager.Instance?.PlayFireworks(fwCenter, wb.WorldRoot?.transform, 8);

            var fwLight = new GameObject("FireworkLight");
            fwLight.transform.position = fwCenter;
            var light = fwLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 30f;
            light.intensity = 4f;
            light.color = new Color(1f, 0.85f, 0.4f);
            RegisterSpawned(fwLight);
        }

        while (_happyPhaseTimer < 2.4f)
        {
            _happyPhaseTimer += Time.deltaTime;

            if (_tetoRoot != null)
            {
                float skip = Mathf.Sin(_happyPhaseTimer * 10f);
                float bounce = Mathf.Pow(Mathf.Max(0, skip), 2) * 0.25f;
                float armSwing = skip * ArmSwingAngle;
                _tetoRoot.transform.position = new Vector3(_tetoRoot.transform.position.x, 1f + bounce, _tetoRoot.transform.position.z);
                if (_tetoLeftArm != null)
                    _tetoLeftArm.localRotation = Quaternion.Euler(armSwing, 0f, -15f);
                if (_tetoRightArm != null)
                    _tetoRightArm.localRotation = Quaternion.Euler(-armSwing, 0f, 15f);
            }

            if (_happyJumpCount < 2 && _happyPhaseTimer >= 0.35f && _happyPhaseTimer - Time.deltaTime < 0.35f)
            {
                SpawnHeart(_tetoRoot != null ? _tetoRoot.transform.position : Vector3.zero);
                _happyJumpCount++;
            }
            if (_happyJumpCount < 2 && _happyPhaseTimer >= 1.15f && _happyPhaseTimer - Time.deltaTime < 1.15f)
            {
                SpawnHeart(_tetoRoot != null ? _tetoRoot.transform.position : Vector3.zero);
                _happyJumpCount++;
            }

            Vector3 refPos2 = _tetoRoot != null ? _tetoRoot.transform.position : _player.transform.position;
            if (_mainCamera != null)
            {
                Vector3 mid2 = (_player.transform.position + refPos2) * 0.5f;
                _mainCamera.transform.position = new Vector3(RoadX, mid2.y + 1.6f, mid2.z - 4f);
                _mainCamera.transform.rotation = Quaternion.identity;
            }

            yield return null;
        }

        if (_tetoLeftArm != null)
            _tetoLeftArm.localRotation = Quaternion.Euler(0f, 0f, -15f);
        if (_tetoRightArm != null)
            _tetoRightArm.localRotation = Quaternion.Euler(0f, 0f, 15f);

        _happyPhase = 3;
        ShowHappyEndingUI();

        float enterWait = 0;
        while (enterWait < 60f)
        {
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                break;

            Vector3 refPos3 = _tetoRoot != null ? _tetoRoot.transform.position : _player.transform.position;
            if (_mainCamera != null)
            {
                Vector3 mid3 = (_player.transform.position + refPos3) * 0.5f;
                _mainCamera.transform.position = new Vector3(RoadX, mid3.y + 1.6f, mid3.z - 4f);
                _mainCamera.transform.rotation = Quaternion.identity;
            }

            enterWait += Time.deltaTime;
            yield return null;
        }

        HideSkipButton();
        DestroyHappyEndingUI();
        CleanupHearts();
        if (_tetoRoot != null)
        {
            Destroy(_tetoRoot);
            _tetoRoot = null;
            _tetoBody = null;
            _tetoLeftArm = null;
            _tetoRightArm = null;
            _tetoLegL = null;
            _tetoLegR = null;
            _tetoLowerLegL = null;
            _tetoLowerLegR = null;
        }
        StopWalkAnimation();
        ResetLimbRotations(_happyPlayerModel);
        _happyPlayerModel = null;
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();
        RestoreAfterPreview();

        if (onComplete != null)
        {
            _previewOnComplete = null;
            onComplete();
        }
        else
        {
            if (_uiManager == null)
                _uiManager = Object.FindAnyObjectByType<UIManager>();
            if (_uiManager != null)
                _uiManager.ShowMessage(Localization.T("Tiếp tục cuộc phiêu lưu!"), 2);
        }
        }
        finally
        {
            IsActive = false;
            _cutsceneRoutine = null;
            if (GameManager.Instance != null) GameManager.Instance.TimeSpeed = _savedTimeSpeed;
        }
    }

    // ── Happy Ending UI ──

    private void ShowHappyEndingUI()
    {
        if (_happyUI != null) return;
        if (_canvas == null)
            _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null) return;

        _happyUI = new GameObject("HappyEndingUI");
        _happyUI.transform.SetParent(_canvas.transform, false);

        var bg = _happyUI.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        var rt = _happyUI.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var title = MakeUIText("HappyTitle", Localization.T("KẾT THÚC HẠNH PHÚC"), 48, new Color(1f, 0.863f, 0.314f), new Vector2(0, 80));
        var sub = MakeUIText("HappySubtitle", Localization.T("Bạn và Jessica đã đi đến cuối con đường cùng nhau!"), 24, Color.white, new Vector2(0, 20));
        var hint = MakeUIText("HappyHint", Localization.T("Nhấn Enter để tiếp tục chơi"), 18, Color.gray, new Vector2(0, -30));
    }

    private GameObject MakeUIText(string name, string text, int fontSize, Color color, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_happyUI.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        _uiManager?.ApplyDefaultFont(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(600, 60);
        return go;
    }

    private void DestroyHappyEndingUI()
    {
        if (_happyUI != null) { Destroy(_happyUI); _happyUI = null; }
    }

    // ── Hearts ──

    private void SpawnHeart(Vector3 position)
    {
        if (_canvas == null) return;

        var heartGO = new GameObject("Heart");
        heartGO.transform.SetParent(_canvas.transform, false);
        var heart = heartGO.AddComponent<TextMeshProUGUI>();
        _uiManager?.ApplyDefaultFont(heart);
        heart.text = "♥";
        heart.fontSize = 48;
        heart.color = new Color(1f, 0.314f, 0.471f);
        heart.alignment = TextAlignmentOptions.Center;

        var rt = heartGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(60, 60);

        _hearts.Add(heartGO);
        _heartRoutine = StartCoroutine(AnimateHeart(heartGO));
    }

    private IEnumerator AnimateHeart(GameObject heart)
    {
        float dur = 1f;
        float elapsed = 0;
        Vector3 startScl = Vector3.one;
        Vector3 endScl = Vector3.one * 2f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / dur;
            heart.transform.localScale = Vector3.Lerp(startScl, endScl, p);
            var rt = heart.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0, 200), 1 - Mathf.Pow(1 - p, 2));
            yield return null;
        }

        _hearts.Remove(heart);
        if (heart != null) Destroy(heart);
    }

    private void CleanupHearts()
    {
        foreach (var h in _hearts)
        {
            if (h != null) Destroy(h);
        }
        _hearts.Clear();
    }

    // ── Wife NPC (happy ending) ──

    private GameObject CreateTeto(Vector3 position)
    {
        var npc = WifeNPC.BuildWifeNpc(null, position, 1f, Quaternion.identity);
        npc.name = "Jessica";
        RegisterSpawned(npc);
        return npc;
    }

    private void FaceEachOther()
    {
        if (_player == null || _tetoRoot == null) return;
        float z = _player.transform.position.z;
        _player.transform.position = new Vector3(RoadX - 0.9f, _player.transform.position.y, z);
        _player.transform.rotation = Quaternion.Euler(0, 90, 0);
        if (_happyPlayerModel != null)
        {
            _happyPlayerModel.transform.position = new Vector3(RoadX - 0.9f, 0.82f, z);
            _happyPlayerModel.transform.rotation = Quaternion.Euler(0, 90, 0);
        }
        _tetoRoot.transform.position = new Vector3(RoadX + 0.9f, _tetoRoot.transform.position.y, z);
        _tetoRoot.transform.rotation = Quaternion.Euler(0, 90, 0);
    }
}
