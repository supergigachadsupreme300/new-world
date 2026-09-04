using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.1): multiplayer indicator. A small HUD badge that reports the networking
/// state from Phase 7: whether a dedicated server is running, how many sessions are connected,
/// and the inbound chat activity counter. Purely presentational — polls <see cref="NetServerHost"/>
/// if present; safe to add to singleplayer builds (shows "Offline").
/// </summary>
public sealed class MultiplayerIndicatorHUD : MonoBehaviour
{
    public bool ShowOnInGame = true;

    private Canvas _canvas;
    private TMP_Text _label;

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("MultiplayerIndicatorCanvas");
        var root = HudCanvas.CreateBackdrop(_canvas.transform, "MPBadge",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-120f, -120f), new Vector2(220f, 36f));
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root, false);
        var lr = labelGo.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Max(12f, Screen.height / 60f);
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        _label = tmp;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool inGame = gm != null && gm.InGame;
        if (_canvas != null)
            _canvas.gameObject.SetActive(ShowOnInGame ? inGame : true);
        if (!inGame || _label == null) return;

        var host = Object.FindAnyObjectByType<NetServerHost>();
        string text;
        if (host != null && host.IsRunning)
        {
            var server = host.Server;
            int sessions = server != null ? server.Sessions.Count : 0;
            text = "Server: " + sessions + " online";
        }
        else
        {
            text = "Offline (Solo)";
        }
        _label.text = text;
    }
}