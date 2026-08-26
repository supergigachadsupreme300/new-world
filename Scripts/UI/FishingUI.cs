using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FishingUI : MonoBehaviour
{
    private RectTransform _barBg;
    private RectTransform _greenZone;
    private RectTransform _playerLine;
    private RectTransform _progressBg;
    private RectTransform _progressFill;
    private TMP_Text _statusText;

    private RectTransform _wheelRect;
    private RectTransform _wheelIndicator;
    private float _wheelRotation;

    private float _barHalf;
    private float _zoneHalf;
    private float _greenMin;
    private float _greenMax;
    private float _greenSpeed = 120f;
    private float _greenDir = 1f;
    private float _greenPos;

    public float PlayerLinePos { get; private set; } = 0f;
    public float Progress { get; private set; } = 0.5f;
    private float _turnVelocity;

    public void Create(Canvas canvas)
    {
        float barW = 70f;
        float barH = 360f;
        float zoneH = 50f;
        float lineT = 4f;
        float progW = 260f;
        float progH = 20f;

        _barHalf = barH * 0.5f;
        _zoneHalf = zoneH * 0.5f;
        float margin = _zoneHalf + 15f;
        _greenMin = -_barHalf + margin;
        _greenMax = _barHalf - margin;

        var barGo = new GameObject("Fish_BarBg");
        barGo.transform.SetParent(canvas.transform, false);
        _barBg = barGo.AddComponent<RectTransform>();
        _barBg.sizeDelta = new Vector2(barW, barH);
        _barBg.anchorMin = new Vector2(0.5f, 0.5f);
        _barBg.anchorMax = new Vector2(0.5f, 0.5f);
        _barBg.pivot = new Vector2(0.5f, 0.5f);
        _barBg.anchoredPosition = new Vector2(200f, 0f);
        var barImg = barGo.AddComponent<Image>();
        barImg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        var gzGo = new GameObject("Fish_GreenZone");
        gzGo.transform.SetParent(_barBg, false);
        _greenZone = gzGo.AddComponent<RectTransform>();
        _greenZone.sizeDelta = new Vector2(barW - 8f, zoneH);
        _greenZone.anchorMin = new Vector2(0.5f, 0.5f);
        _greenZone.anchorMax = new Vector2(0.5f, 0.5f);
        _greenZone.pivot = new Vector2(0.5f, 0.5f);
        _greenZone.anchoredPosition = Vector2.zero;
        var gzImg = gzGo.AddComponent<Image>();
        gzImg.color = new Color(0.2f, 0.85f, 0.2f, 0.55f);

        var plGo = new GameObject("Fish_PlayerLine");
        plGo.transform.SetParent(_barBg, false);
        _playerLine = plGo.AddComponent<RectTransform>();
        _playerLine.sizeDelta = new Vector2(barW, lineT);
        _playerLine.anchorMin = new Vector2(0.5f, 0.5f);
        _playerLine.anchorMax = new Vector2(0.5f, 0.5f);
        _playerLine.pivot = new Vector2(0.5f, 0.5f);
        _playerLine.anchoredPosition = Vector2.zero;
        var plImg = plGo.AddComponent<Image>();
        plImg.color = Color.white;

        var pbGo = new GameObject("Fish_ProgressBg");
        pbGo.transform.SetParent(canvas.transform, false);
        _progressBg = pbGo.AddComponent<RectTransform>();
        _progressBg.sizeDelta = new Vector2(progW, progH);
        _progressBg.anchorMin = new Vector2(0.5f, 0f);
        _progressBg.anchorMax = new Vector2(0.5f, 0f);
        _progressBg.pivot = new Vector2(0.5f, 0.5f);
        _progressBg.anchoredPosition = new Vector2(0f, 20f);
        var pbImg = pbGo.AddComponent<Image>();
        pbImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        var pfGo = new GameObject("Fish_ProgressFill");
        pfGo.transform.SetParent(_progressBg, false);
        _progressFill = pfGo.AddComponent<RectTransform>();
        _progressFill.sizeDelta = new Vector2(progW * Progress, progH - 4f);
        _progressFill.anchorMin = new Vector2(0f, 0.5f);
        _progressFill.anchorMax = new Vector2(0f, 0.5f);
        _progressFill.pivot = new Vector2(0f, 0.5f);
        _progressFill.anchoredPosition = new Vector2(2f, 0f);
        var pfImg = pfGo.AddComponent<Image>();
        pfImg.color = new Color(0.2f, 0.6f, 1f, 0.9f);

        var stGo = new GameObject("Fish_StatusText");
        stGo.transform.SetParent(canvas.transform, false);
        var stRect = stGo.AddComponent<RectTransform>();
        stRect.sizeDelta = new Vector2(300f, 40f);
        stRect.anchorMin = new Vector2(0.5f, 0f);
        stRect.anchorMax = new Vector2(0.5f, 0f);
        stRect.pivot = new Vector2(0.5f, 0.5f);
        stRect.anchoredPosition = new Vector2(0f, barH + 50f);
        _statusText = stGo.AddComponent<TextMeshProUGUI>();
        _statusText.font = Resources.Load<TMP_FontAsset>("VietPixel");
        _statusText.fontSize = 22;
        _statusText.alignment = TextAlignmentOptions.Center;
        _statusText.color = Color.white;
        _statusText.text = "";

        float wheelSize = 120f;
        var wheelGo = new GameObject("Fish_ReelWheel");
        wheelGo.transform.SetParent(canvas.transform, false);
        _wheelRect = wheelGo.AddComponent<RectTransform>();
        _wheelRect.sizeDelta = new Vector2(wheelSize, wheelSize);
        _wheelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _wheelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _wheelRect.pivot = new Vector2(0.5f, 0.5f);
        _wheelRect.anchoredPosition = Vector2.zero;
        _wheelRect.localRotation = Quaternion.identity;
        var wheelImg = wheelGo.AddComponent<Image>();
        wheelImg.color = new Color(0.45f, 0.3f, 0.15f, 0.9f);

        var indGo = new GameObject("Fish_WheelIndicator");
        indGo.transform.SetParent(_wheelRect, false);
        _wheelIndicator = indGo.AddComponent<RectTransform>();
        _wheelIndicator.sizeDelta = new Vector2(4f, wheelSize * 0.35f);
        _wheelIndicator.anchorMin = new Vector2(0.5f, 0.5f);
        _wheelIndicator.anchorMax = new Vector2(0.5f, 0.5f);
        _wheelIndicator.pivot = new Vector2(0.5f, 1f);
        _wheelIndicator.anchoredPosition = Vector2.zero;
        var indImg = indGo.AddComponent<Image>();
        indImg.color = Color.white;

        Hide();
    }

    public void Show()
    {
        _barBg.gameObject.SetActive(true);
        _greenZone.gameObject.SetActive(true);
        _playerLine.gameObject.SetActive(true);
        _progressBg.gameObject.SetActive(true);
        _progressFill.gameObject.SetActive(true);
        _statusText.gameObject.SetActive(true);
        _wheelRect.gameObject.SetActive(true);
        _wheelIndicator.gameObject.SetActive(true);

        PlayerLinePos = 0f;
        Progress = 0.5f;
        _turnVelocity = 0f;
        _greenPos = 0f;
        _greenDir = Random.value < 0.5f ? 1f : -1f;
        _wheelRotation = 0f;
        _wheelRect.localRotation = Quaternion.identity;
        _playerLine.anchoredPosition = Vector2.zero;
        UpdateProgressFill();
        _statusText.text = Localization.T("Kéo Cá!");
        _statusText.color = Color.white;
    }

    public void Hide()
    {
        _barBg.gameObject.SetActive(false);
        _greenZone.gameObject.SetActive(false);
        _playerLine.gameObject.SetActive(false);
        _progressBg.gameObject.SetActive(false);
        _progressFill.gameObject.SetActive(false);
        _statusText.gameObject.SetActive(false);
        _wheelRect.gameObject.SetActive(false);
        _wheelIndicator.gameObject.SetActive(false);
    }

    public bool IsMouseOverWheel()
    {
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(_wheelRect, pointer.position.ReadValue(), null);
    }

    public Vector2 GetWheelCenterScreen()
    {
        return RectTransformUtility.WorldToScreenPoint(null, _wheelRect.position);
    }

    public void UpdateReeling(float deltaTime, float wheelDelta)
    {
        if (!_barBg.gameObject.activeSelf) return;

        float step = _greenSpeed * deltaTime;
        _greenPos += step * _greenDir;
        if (_greenPos > _greenMax) { _greenPos = _greenMax; _greenDir = -1f; }
        if (_greenPos < _greenMin) { _greenPos = _greenMin; _greenDir = 1f; }
        _greenZone.anchoredPosition = new Vector2(0f, _greenPos);

        float lineMove = wheelDelta * 0.4f;
        float targetPos = Mathf.Clamp(PlayerLinePos + lineMove, -_barHalf + 2f, _barHalf - 2f);
        PlayerLinePos = Mathf.SmoothDamp(PlayerLinePos, targetPos, ref _turnVelocity, 0.15f);
        _playerLine.anchoredPosition = new Vector2(0f, PlayerLinePos);

        _wheelRotation += wheelDelta;
        _wheelRect.localRotation = Quaternion.Euler(0f, 0f, _wheelRotation);

        bool inZone = Mathf.Abs(PlayerLinePos - _greenPos) <= _zoneHalf - 4f;
        float rate = 0.4f * deltaTime;
        if (inZone)
            Progress = Mathf.Min(1f, Progress + rate * 0.6f);
        else
            Progress = Mathf.Max(0f, Progress - rate * 0.125f);

        UpdateProgressFill();

        if (Progress >= 1f)
            _statusText.text = Localization.T("Bắt Được Cá!");
        else if (Progress <= 0f)
            _statusText.text = Localization.T("Cá Thoát!");
        else
            _statusText.text = Localization.T("Đang Kéo...");

        _statusText.color = Progress >= 1f ? Color.green : (Progress <= 0f ? Color.red : Color.white);
    }

    private void UpdateProgressFill()
    {
        if (_progressFill != null)
        {
            var size = _progressFill.sizeDelta;
            size.x = (_progressBg.sizeDelta.x - 4f) * Progress;
            _progressFill.sizeDelta = size;
        }
    }
}
