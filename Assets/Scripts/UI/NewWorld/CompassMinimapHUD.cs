using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 8 (Task 8.1): compass strip + minimap in the upper-right corner.
/// The minimap is a north-up top-down circle centred on the player with a subtle
/// crosshair, a north tick, and a player dot. The compass (top-centre) shows the
/// current heading cardinal.
/// </summary>
public sealed class CompassMinimapHUD : MonoBehaviour
{
    [Header("Layout")]
    public bool ShowOnInGame = true;
    [Range(0.08f, 0.3f)] public float MinimapRadiusFraction = 0.16f;

    /// <summary>World metres shown across the minimap diameter.</summary>
    public float ViewSize = 40f;

    private Canvas _canvas;
    private TMP_Text _compassLabel;
    private RectTransform _minimapRoot;
    private float _radius;

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("CompassMinimapCanvas");
        float w = Mathf.Max(Screen.width, 1f);
        float h = Mathf.Max(Screen.height, 1f);

        // Compass strip (top-centre).
        var compassRoot = HudCanvas.CreateBackdrop(_canvas.transform, "CompassStrip",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(w * 0.5f, 34f));
        compassRoot.pivot = new Vector2(0.5f, 1f);
        _compassLabel = MakeLabel(compassRoot, "N", Vector2.zero, Color.white);

        // Minimap circle, upper-right corner.
        _radius = Mathf.Min(w, h) * MinimapRadiusFraction;
        float d = _radius * 2f;
        _minimapRoot = HudCanvas.CreateBackdrop(_canvas.transform, "Minimap",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-_radius - 14f, -_radius - 14f), new Vector2(d, d));
        var discImg = _minimapRoot.GetComponent<Image>();
        discImg.sprite = MakeCircleSprite();
        discImg.type = Image.Type.Simple;
        discImg.color = new Color(0.06f, 0.08f, 0.1f, 0.82f);

        // North tick.
        var north = new GameObject("North");
        north.transform.SetParent(_minimapRoot, false);
        var nrt = north.AddComponent<RectTransform>();
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.pivot = new Vector2(0.5f, 0.5f);
        nrt.anchoredPosition = new Vector2(0f, _radius - 15f);
        nrt.sizeDelta = new Vector2(20f, 14f);
        var northText = north.AddComponent<TextMeshProUGUI>();
        northText.text = "N";
        northText.fontSize = Mathf.Max(12f, Screen.height / 68f);
        northText.color = new Color(0.85f, 0.9f, 1f, 0.95f);
        northText.alignment = TextAlignmentOptions.Top;
        northText.raycastTarget = false;

        // Crosshair (horizontal + vertical hairlines).
        MakeHairline(_minimapRoot, "CrossH", new Vector2(0.5f, 0.5f), d * 0.6f, 1.5f);
        MakeHairline(_minimapRoot, "CrossV", new Vector2(0.5f, 0.5f), 1.5f, d * 0.6f);

        // Player dot (dedicated, clearly visible).
        var dot = new GameObject("PlayerDot");
        dot.transform.SetParent(_minimapRoot, false);
        var drt = dot.AddComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.pivot = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = Vector2.zero;
        drt.sizeDelta = new Vector2(Mathf.Max(7f, _radius * 0.22f), Mathf.Max(7f, _radius * 0.22f));
        var dotImg = dot.AddComponent<Image>();
        dotImg.sprite = MakeCircleSprite();
        dotImg.type = Image.Type.Simple;
        dotImg.color = new Color(0.55f, 0.9f, 1f, 1f);
        dotImg.raycastTarget = false;
    }

    private RectTransform MakeHairline(RectTransform parent, string name, Vector2 anchor, float sizeX, float sizeY)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(sizeX, sizeY);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.22f);
        img.raycastTarget = false;
        return rt;
    }

    private static Sprite _circleSprite;
    private static Sprite MakeCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "MinimapCircle";
        var pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        float r = c - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                pixels[y * size + x] = (dx * dx + dy * dy) <= r * r
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }

    private TMP_Text MakeLabel(RectTransform parent, string text, Vector2 pos, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(112f, 32f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Max(17f, Screen.height / 48f);
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = text;
        return tmp;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool inGame = gm != null && gm.InGame;
        if (_canvas != null)
            _canvas.gameObject.SetActive(ShowOnInGame ? inGame : true);
        if (!inGame) return;

        Transform focus = gm.Player != null ? gm.Player.transform : null;
        if (focus == null) return;

        float yaw;
        var cam = Camera.main;
        yaw = cam != null ? cam.transform.eulerAngles.y : focus.eulerAngles.y;
        if (_compassLabel != null)
            _compassLabel.text = Cardinal(yaw);
    }

    private static string Cardinal(float yaw)
    {
        yaw = ((yaw % 360f) + 360f) % 360f;
        if (yaw < 22.5f || yaw >= 337.5f) return "N";
        if (yaw < 67.5f) return "NE";
        if (yaw < 112.5f) return "E";
        if (yaw < 157.5f) return "SE";
        if (yaw < 202.5f) return "S";
        if (yaw < 247.5f) return "SW";
        if (yaw < 292.5f) return "W";
        return "NW";
    }
}