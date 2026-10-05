using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager 
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FATED ENDING
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        // Build a temp mansion for the scene if the real one isn't built yet; cleaned up at scene end
        bool mansionBuilt = WorldBuilder.Instance != null && WorldBuilder.Instance.HasPlayerMansion();
        if (!mansionBuilt && WorldBuilder.Instance != null)
        {
            var tempMansion = MapBuilder.BuildRichManMansion(null, WorldBuilder.MansionBasePos, 1f,
                Quaternion.Euler(0f, -90f, 0f));
            foreach (var r in tempMansion.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            foreach (var c in tempMansion.GetComponentsInChildren<Collider>())
                Object.Destroy(c);
            RegisterSpawned(tempMansion);
        }
        Vector3 mb = WorldBuilder.Instance?.GetMansionPosition() ?? WorldBuilder.MansionBasePos;

        // Local (x = offset from center, z = toward facade) mapped onto the -90-rotated mansion:
        // facade faces -x, so local +z -> world -x, local +x -> world +z
        Vector3 P(Vector3 local) => new Vector3(mb.x - local.z, local.y, mb.z + local.x);

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

        // â”€â”€ Set: the real mansion (front = -x, facade-left living room) â”€â”€

        // â”€â”€ Police car parked on the grass in front of the mansion â”€â”€
        var policeCar = MapBuilder.BuildPoliceCar(null, P(new Vector3(-4f, 0f, 13.5f)));
        policeCar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        RegisterSpawned(policeCar);

        // â”€â”€ Dead bodies inside the living room â”€â”€
        var deadPlayer = PlayerModelBuilder.BuildPlayerModel(null);
        deadPlayer.transform.position = P(new Vector3(-8.2f, 0.69f, 6.4f));
        deadPlayer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        foreach (var r in deadPlayer.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(deadPlayer);

        var deadWife = WifeNPC.BuildWifeNpc(null,
            P(new Vector3(-5.5f, 0.82f, 5.4f)), 1f, Quaternion.Euler(90f, 0f, 0f));
        foreach (var r in deadWife.GetComponentsInChildren<Renderer>())
            r.gameObject.layer = 0;
        RegisterSpawned(deadWife);

        CreateBloodPool(P(new Vector3(-8.2f, 0.53f, 6.4f)));
        CreateBloodPool(P(new Vector3(-5.5f, 0.53f, 5.4f)));

        // â”€â”€ Robbery / addiction clue props â”€â”€
        BuildRobberyClues(P(new Vector3(-8.5f, 0.50f, 6.2f)));

        // â”€â”€ Police officers (inside the living room, near the front wall) â”€â”€
        var officerA = MapBuilder.BuildPoliceOfficer(null,
            P(new Vector3(-9f, 1.43f, 7.6f)), Quaternion.Euler(0f, 180f, 0f));
        RegisterSpawned(officerA);
        var officerB = MapBuilder.BuildPoliceOfficer(null,
            P(new Vector3(-4.6f, 1.43f, 7.6f)), Quaternion.Euler(0f, 180f, 0f));
        RegisterSpawned(officerB);

        // â”€â”€ Demons lurking at the room edges (camera border only) â”€â”€
        var demons = new List<Transform>();
        for (int g = 0; g < 5; g++)
        {
            var demonGO = new GameObject("DemonPlaceholder");
            var demon = demonGO.transform;
            foreach (var r in demon.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = 0;
            foreach (var c in demon.GetComponentsInChildren<Collider>())
                Object.Destroy(c);
            demons.Add(demon);
        }
        Vector3[] demonPos =
        {
            P(new Vector3(-9.4f, 0.65f, 8f)),
            P(new Vector3(-4.6f, 0.65f, 8f)),
            P(new Vector3(-9.4f, 0.65f, 6.2f)),
            P(new Vector3(-4.8f, 0.65f, 5.8f)),
            P(new Vector3(-5.8f, 0.65f, 2.4f))
        };
        Vector3 lookCenter = P(new Vector3(-7f, 0.65f, 5.5f));
        for (int i = 0; i < demons.Count; i++)
        {
            demons[i].position = demonPos[i];
            demons[i].localScale = Vector3.one * 1f;
            demons[i].rotation = Quaternion.LookRotation(lookCenter - demonPos[i]);
            RegisterSpawned(demons[i].gameObject);
            StartCoroutine(IdleBob(demons[i], 0.15f));
        }

        // â”€â”€ PHASE 1: exterior daytime, police car at the mansion front (4s) â”€â”€
        Vector3 camExt = P(new Vector3(-6f, 2.6f, 16f));
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = camExt;
            _mainCamera.transform.LookAt(P(new Vector3(0f, 1.8f, 8.5f)));
        }
        yield return StartCoroutine(FadeOverlay(0, 2f));
        yield return StartCoroutine(ShowSubtitle("Cá»­a dinh thá»± má»Ÿ toang... cÃ²n chiáº¿c xe cáº£nh sÃ¡t Ä‘áº­u bÃªn ngoÃ i.", 3f));
        yield return new WaitForSeconds(1f);

        // â”€â”€ PHASE 2: cut inside the living room, reveal bodies (6s) â”€â”€
        yield return StartCoroutine(FadeOverlay(1f, 0.6f));
        Vector3 camIn = P(new Vector3(-6.5f, 4.5f, 7.4f));
        Vector3 lookBodies = P(new Vector3(-6.9f, 0.85f, 5.9f));
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = camIn;
            _mainCamera.transform.LookAt(lookBodies);
        }
        yield return StartCoroutine(FadeOverlay(0f, 0.6f));
        yield return StartCoroutine(PanCamera(camIn, P(new Vector3(-6.8f, 4.3f, 7.2f)), lookBodies, 2.5f));
        yield return StartCoroutine(ShowSubtitle("Trong phÃ²ng... hai thi thá»ƒ náº±m báº¥t Ä‘á»™ng.", 3.5f));

        // â”€â”€ PHASE 3: officers walk over, discover (7s) â”€â”€
        yield return StartCoroutine(WalkStraight(officerA.transform,
            P(new Vector3(-9f, 1.43f, 7.6f)),
            P(new Vector3(-8.2f, 1.43f, 6.4f)), 3.5f));
        yield return StartCoroutine(WalkStraight(officerB.transform,
            P(new Vector3(-4.6f, 1.43f, 7.6f)),
            P(new Vector3(-5.6f, 1.43f, 5.8f)), 3.5f));
        yield return StartCoroutine(ShowSubtitle("Cá»­a bá»‹ phÃ¡. Äá»“ Ä‘áº¡c vÆ°Æ¡ng vÃ£i kháº¯p nÆ¡i.", 3.5f));
        yield return new WaitForSeconds(0.5f);

        // â”€â”€ PHASE 4: the clue (13s) â”€â”€
        Vector3 camClue = P(new Vector3(-8.6f, 1.75f, 7.6f));
        Vector3 lookClue = P(new Vector3(-8.5f, 0.65f, 6.2f));
        yield return StartCoroutine(PanCamera(camIn, camClue, lookClue, 2.5f));
        yield return StartCoroutine(ShowSubtitle("Má»™t vá»¥ trá»™m... nhÆ°ng chá»‰ máº¥t vÃ i Ä‘á»“ng vÃ ng vá»¥n.", 3f));
        yield return StartCoroutine(ShowSubtitle("Khoan Ä‘Ã£... bÆ¡m kim tiÃªm. Dáº¥u váº¿t nghiá»‡n ngáº­p.", 3f));
        yield return StartCoroutine(ShowSubtitle("Káº» nghiá»‡n nÃ y... cÃ³ váº» liÃªn quan Ä‘áº¿n gia tá»™c giÃ u cÃ³.", 3.5f));

        // â”€â”€ PHASE 5: lights dim, the demons at the border (6s) â”€â”€
        yield return StartCoroutine(FadeOverlay(0.7f, 2.5f));
        yield return StartCoroutine(ShowSubtitle("VÃ  lÅ© quá»·... váº«n Ä‘á»©ng im ngay rÃ¬a bÃ³ng tá»‘i. KhÃ´ng ai nhÃ¬n tháº¥y chÃºng.", 3.5f));
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(FadeOverlay(1f, 2f));

        // â”€â”€ PHASE 6: end screen â”€â”€
        HideSkipButton();
        DestroySubtitle();
        CleanupSpawned();
        DestroyLetterboxBars();
        DestroyOverlay();

        FinishEndingScene(onComplete,
            Localization.T("Káº¾T THÃšC Äá»ŠNH Má»†NH"),
            Localization.T("Báº¡n vÃ  Jessica Ä‘Ã£ xÃ¢y xong dinh thá»±... nhÆ°ng khÃ´ng bao giá» diá»‡t Quá»· VÆ°Æ¡ng,\nkhÃ´ng láº­t táº©y bÃ­ máº­t cá»§a PhÃº Ã”ng.\n\nMá»™t Ä‘Ãªm, káº» nghiá»‡n ngáº­p do ma tÃºy cá»§a PhÃº Ã”ng Ä‘Ã£ Ä‘á»™t nháº­p.\nCáº£nh sÃ¡t tÃ¬m tháº¥y hai thi thá»ƒ trong chÃ­nh ngÃ´i nhÃ  báº¡n xÃ¢y nÃªn.\nDáº¥u váº¿t: má»™t vá»¥ trá»™m... do nghiá»‡n ngáº­p.\n\nVÃ  lÅ© quá»· váº«n Ä‘á»©ng im á»Ÿ rÃ¬a mÃ n Ä‘Ãªm,\nkhÃ´ng má»™t ai nhÃ¬n tháº¥y chÃºng.\n\nÄá»‹nh má»‡nh cá»§a báº¡n Ä‘Ã£ káº¿t thÃºc ngay trong nhÃ  mÃ¬nh."));
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
