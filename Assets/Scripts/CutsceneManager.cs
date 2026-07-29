using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }
    private UIManager _uiManager;
    private Coroutine _cutsceneCoroutine;
    private GameObject _overlay;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Initialize(UIManager uiManager)
    {
        _uiManager = uiManager;
    }

    public void PlaySadEnding()
    {
        if (_cutsceneCoroutine != null)
            return;

        _cutsceneCoroutine = StartCoroutine(PlaySadEndingRoutine());
    }

    public void CancelCutscene()
    {
        if (_cutsceneCoroutine != null)
        {
            StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = null;
        }
        if (_overlay != null)
            Destroy(_overlay);
    }

    private IEnumerator PlaySadEndingRoutine()
    {
        GameManager.Instance?.TogglePause(true);
        _uiManager?.ShowAllGameUI(false);
        _uiManager?.ShowMainMenu(false);

        _overlay = new GameObject("CutsceneOverlay");
        _overlay.transform.SetParent(_uiManager != null ? _uiManager.gameObject.transform : null, false);
        var image = _overlay.AddComponent<Image>();
        image.color = Color.black;
        var rect = _overlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.canvasRenderer.SetAlpha(1f);
        yield return new WaitForSeconds(0.5f);
        var cameraTransform = Camera.main?.transform;
        var startPos = cameraTransform != null ? cameraTransform.position : new Vector3(20f, 5f, 30f);
        var endPos = startPos + new Vector3(0f, 1f, -40f);
        var timer = 0f;
        var duration = 8f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            var t = timer / duration;
            if (cameraTransform != null)
                cameraTransform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        _uiManager?.ShowEndScreen("KẾT THÚC ĐAU BUỒN", "\"Skibidi.\ndop dop.\"");
        if (_overlay != null)
            Destroy(_overlay);
        _cutsceneCoroutine = null;
    }
}
