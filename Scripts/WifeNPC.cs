using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WifeNPC : MonoBehaviour
{
    public static WifeNPC Instance { get; private set; }

    public enum WifeState { NotMet, Greeting, Married }

    public WifeState State = WifeState.NotMet;
    public bool Married;

    private GameObject _dialogPanel;
    private TMP_Text _nameText;
    private TMP_Text _dialogText;
    private TMP_Text _promptText;
    private Canvas _canvas;

    private readonly Queue<string> _dialogQueue = new Queue<string>();
    private bool _dialogActive;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        CreateDialogPanel();
    }

    public void Interact()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame)
            return;

        if (_dialogActive)
        {
            AdvanceDialog();
            return;
        }

        var lines = GetDialogLines();
        if (lines == null || lines.Length == 0)
            return;

        foreach (var line in lines)
            _dialogQueue.Enqueue(line);

        ShowNextDialog();
    }

    public void SaveState()
    {
        var data = new WifeSaveData
        {
            state = (int)State,
            married = Married
        };
        PlayerPrefs.SetString("WifeNPC", JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public void LoadState()
    {
        var json = PlayerPrefs.GetString("WifeNPC", "");
        if (string.IsNullOrEmpty(json))
            return;

        var data = JsonUtility.FromJson<WifeSaveData>(json);
        if (data != null)
        {
            State = (WifeState)data.state;
            Married = data.married;
        }
    }

    private string[] GetDialogLines()
    {
        if (Married)
        {
            return new string[]
            {
                "Chồng yêu ơi! Hôm nay mình hạnh phúc lắm nhé.",
                "Em luôn ở bên anh, dù nông trại có bận rộn đến đâu.",
                "Cảm ơn anh đã xây dựng dinh thự cho mình. Em yêu anh!"
            };
        }

        bool mansionComplete = IsMansionComplete();

        if (mansionComplete)
        {
            State = WifeState.Married;
            Married = true;
            SaveState();
            return new string[]
            {
                "Jessica: Anh ơi... dinh thự đã hoàn thành rồi!",
                "Jessica: Em rất hạnh phúc. Em không ngờ anh làm được đến vậy.",
                "Jessica: Nếu anh muốn... mình có thể kết hôn. Em đồng ý!",
                "Jessica: Từ giờ, em sẽ mãi bên anh. Cảm ơn anh nhé!"
            };
        }

        switch (State)
        {
            case WifeState.NotMet:
                State = WifeState.Greeting;
                SaveState();
                return new string[]
                {
                    "Jessica: Chào anh! Em là Jessica, cô gái hàng xóm.",
                    "Jessica: Nghe nói anh về nông thôn sống... Hy vọng mình sẽ là hàng xóm tốt nhé!",
                    "Jessica: Nhà em ở bên kia, anh cứ qua chơi bất cứ lúc nào."
                };

            case WifeState.Greeting:
                return new string[]
                {
                    "Jessica: Chào anh! Hôm nay trông anh có vẻ tốt lắm.",
                    "Jessica: Em luôn ở đây nếu anh cần gì nhé.",
                    "Jessica: À, dinh thự phía bắc vẫn chưa xong đâu. Anh cố gắng lên nhé!"
                };

            default:
                return new string[]
                {
                    "Jessica: Chào anh!"
                };
        }
    }

    private bool IsMansionComplete()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return false;
        return wb.GetMansionCompletedParts() >= 25;
    }

    private void ShowNextDialog()
    {
        if (_dialogQueue.Count == 0)
        {
            HideDialog();
            return;
        }

        _dialogActive = true;
        _dialogPanel.SetActive(true);

        string line = _dialogQueue.Dequeue();

        if (line.StartsWith("Jessica: "))
        {
            _nameText.text = "Jessica";
            _dialogText.text = line.Substring("Jessica: ".Length);
        }
        else
        {
            _nameText.text = "";
            _dialogText.text = line;
        }

        _promptText.text = _dialogQueue.Count > 0 ? "Nhấn E để tiếp tục" : "Nhấn E để đóng";
    }

    private void AdvanceDialog()
    {
        if (_dialogQueue.Count > 0)
        {
            ShowNextDialog();
        }
        else
        {
            HideDialog();
        }
    }

    private void HideDialog()
    {
        _dialogActive = false;
        _dialogPanel.SetActive(false);
    }

    private void CreateDialogPanel()
    {
        if (_canvas == null)
            _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null) return;

        float sw = Screen.width;
        float sh = Screen.height;

        _dialogPanel = new GameObject("WifeDialogPanel");
        _dialogPanel.transform.SetParent(_canvas.transform, false);

        var panelRt = _dialogPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, sh * 0.15f);
        panelRt.sizeDelta = new Vector2(sw * 0.7f, sh * 0.28f);

        var panelImg = _dialogPanel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.8f);

        float panelW = sw * 0.7f;
        float panelH = sh * 0.28f;

        _nameText = CreateDialogText("WifeDialogName", panelRt,
            new Vector2(0f, panelH * 0.38f), "Jessica", 24,
            new Color(0.9f, 0.6f, 0.8f), TextAlignmentOptions.Left,
            new Vector2(panelW - 40f, 35f));

        _dialogText = CreateDialogText("WifeDialogText", panelRt,
            new Vector2(0f, -panelH * 0.02f), "", 20,
            Color.white, TextAlignmentOptions.Left,
            new Vector2(panelW - 40f, panelH * 0.55f));

        _promptText = CreateDialogText("WifeDialogPrompt", panelRt,
            new Vector2(0f, -panelH * 0.38f), "Nhấn E để tiếp tục", 16,
            new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Right,
            new Vector2(panelW - 40f, 25f));

        _dialogPanel.SetActive(false);
    }

    private TMP_Text CreateDialogText(string name, RectTransform parent,
        Vector2 position, string text, int fontSize, Color color,
        TextAlignmentOptions alignment, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        return tmp;
    }

    public static GameObject BuildWifeNpc(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("WifeNpc");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;
        root.transform.localScale = Vector3.one * scale;

        Color skinC     = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color bootC     = new Color(0.15f, 0.08f, 0.2f);
        Color bootTrimC = new Color(0.35f, 0.18f, 0.42f);
        Color skirtC    = new Color(0.14f, 0.11f, 0.28f);
        Color topC      = new Color(0.92f, 0.9f, 0.95f);
        Color hairC     = new Color(0.95f, 0.85f, 0.55f);
        Color eyeC      = new Color(0.3f, 0.6f, 1f);
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);

        // ═══ LEGS ROOT ═══
        var legsRoot = new GameObject("LegsRoot");
        legsRoot.transform.SetParent(root.transform);
        legsRoot.transform.localPosition = Vector3.zero;
        legsRoot.transform.localRotation = Quaternion.identity;
        legsRoot.transform.localScale = Vector3.one;

        // Left leg
        var legL = new GameObject("LegL");
        legL.transform.SetParent(legsRoot.transform);
        legL.transform.localPosition = new Vector3(-0.12f, -0.3f, 0f);
        MakeBlock("UpperLegL", legL.transform, new Vector3(0.1f, 0.18f, 0.1f), new Vector3(0f, 0.09f, 0f), skinC, true);
        var lowerLegL = new GameObject("LowerLegL");
        lowerLegL.transform.SetParent(legL.transform);
        lowerLegL.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        MakeBlock("LowerLegBlockL", lowerLegL.transform, new Vector3(0.1f, 0.03f, 0.1f), new Vector3(0f, 0.015f, 0f), skinC, true);
        MakeBlock("CuffL",  lowerLegL.transform, new Vector3(0.16f, 0.05f, 0.22f), new Vector3(0f, -0.04f, 0f), bootTrimC, true);
        MakeBlock("BootL",  lowerLegL.transform, new Vector3(0.14f, 0.38f, 0.2f),  new Vector3(0f, -0.25f, 0f), bootC, true);
        MakeBlock("HeelL",  lowerLegL.transform, new Vector3(0.06f, 0.08f, 0.1f),  new Vector3(0f, -0.48f, 0.04f), bootC, true);
        MakeBlock("SoleL",  lowerLegL.transform, new Vector3(0.15f, 0.04f, 0.24f), new Vector3(0f, -0.46f, 0f), bootC, true);

        // Right leg
        var legR = new GameObject("LegR");
        legR.transform.SetParent(legsRoot.transform);
        legR.transform.localPosition = new Vector3(0.12f, -0.3f, 0f);
        MakeBlock("UpperLegR", legR.transform, new Vector3(0.1f, 0.18f, 0.1f), new Vector3(0f, 0.09f, 0f), skinC, true);
        var lowerLegR = new GameObject("LowerLegR");
        lowerLegR.transform.SetParent(legR.transform);
        lowerLegR.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        MakeBlock("LowerLegBlockR", lowerLegR.transform, new Vector3(0.1f, 0.03f, 0.1f), new Vector3(0f, 0.015f, 0f), skinC, true);
        MakeBlock("CuffR",  lowerLegR.transform, new Vector3(0.16f, 0.05f, 0.22f), new Vector3(0f, -0.04f, 0f), bootTrimC, true);
        MakeBlock("BootR",  lowerLegR.transform, new Vector3(0.14f, 0.38f, 0.2f),  new Vector3(0f, -0.25f, 0f), bootC, true);
        MakeBlock("HeelR",  lowerLegR.transform, new Vector3(0.06f, 0.08f, 0.1f),  new Vector3(0f, -0.48f, 0.04f), bootC, true);
        MakeBlock("SoleR",  lowerLegR.transform, new Vector3(0.15f, 0.04f, 0.24f), new Vector3(0f, -0.46f, 0f), bootC, true);

        // ═══ BODY ROOT ═══
        var bodyRoot = new GameObject("BodyRoot");
        bodyRoot.transform.SetParent(root.transform);
        bodyRoot.transform.localPosition = Vector3.zero;
        bodyRoot.transform.localRotation = Quaternion.identity;
        bodyRoot.transform.localScale = Vector3.one;

        MakeBlock("Skirt",     bodyRoot.transform, new Vector3(0.48f, 0.26f, 0.32f), new Vector3(0f, -0.07f, 0f), skirtC, true);
        MakeBlock("SkirtHem",  bodyRoot.transform, new Vector3(0.56f, 0.05f, 0.38f), new Vector3(0f, -0.21f, 0f), skirtC, true);
        MakeBlock("SkirtBelt", bodyRoot.transform, new Vector3(0.4f, 0.04f, 0.28f),  new Vector3(0f, 0.07f, 0f), bootTrimC, true);
        MakeBlock("Topwaist",  bodyRoot.transform, new Vector3(0.28f, 0.12f, 0.16f), new Vector3(0f, 0.1f, 0f), topC, true);
        MakeBlock("TopLower",  bodyRoot.transform, new Vector3(0.34f, 0.12f, 0.22f), new Vector3(0f, 0.2f, 0f), topC, true);
        MakeBlock("TopUpper",  bodyRoot.transform, new Vector3(0.42f, 0.18f, 0.28f), new Vector3(0f, 0.35f, 0f), topC, true);
        MakeBlock("TopCollar", bodyRoot.transform, new Vector3(0.18f, 0.05f, 0.14f), new Vector3(0f, 0.47f, -0.02f), bootTrimC, true);
        MakeBlock("Neck", bodyRoot.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.5f, 0f), skinC, true);
        MakeBlock("Head", bodyRoot.transform, new Vector3(0.34f, 0.3f, 0.32f), new Vector3(0f, 0.7f, 0f), skinC, true);
        MakeBlock("HairTop",  bodyRoot.transform, new Vector3(0.38f, 0.08f, 0.36f), new Vector3(0f, 0.86f, 0f), hairC, true);
        MakeBlock("HairBack", bodyRoot.transform, new Vector3(0.3f, 0.4f, 0.12f),  new Vector3(0f, 0.65f, 0.26f), hairC, true);
        MakeBlock("HairL",    bodyRoot.transform, new Vector3(0.42f, 0.375f, 0.12f), new Vector3(-0.22f, 0.65f, 0f), hairC, true, Quaternion.Euler(0f, 90f, 0f));
        MakeBlock("HairR",    bodyRoot.transform, new Vector3(0.42f, 0.375f, 0.12f), new Vector3(0.22f, 0.65f, 0f), hairC, true, Quaternion.Euler(0f, 90f, 0f));
        MakeBlock("Braid1",   bodyRoot.transform, new Vector3(0.24f, 0.14f, 0.12f), new Vector3(0f,       0.40f, 0.26f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid2",   bodyRoot.transform, new Vector3(0.20f, 0.12f, 0.1f),  new Vector3(0.02f,   0.32f, 0.27f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid3",   bodyRoot.transform, new Vector3(0.17f, 0.1f,  0.09f), new Vector3(-0.02f,  0.24f, 0.28f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid4",   bodyRoot.transform, new Vector3(0.14f, 0.09f, 0.07f), new Vector3(0.015f,  0.16f, 0.28f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid5",   bodyRoot.transform, new Vector3(0.12f, 0.07f, 0.06f), new Vector3(-0.01f,  0.08f, 0.27f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("Braid6",   bodyRoot.transform, new Vector3(0.09f, 0.06f, 0.05f), new Vector3(0.005f,  0.02f, 0.26f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("BraidEnd", bodyRoot.transform, new Vector3(0.07f, 0.05f, 0.04f), new Vector3(0f,     -0.03f, 0.26f), hairC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeWhiteL", bodyRoot.transform, new Vector3(0.1f, 0.08f, 0.03f), new Vector3(-0.09f, 0.74f, -0.165f), eyeWhiteC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeWhiteR", bodyRoot.transform, new Vector3(0.1f, 0.08f, 0.03f), new Vector3(0.09f, 0.74f, -0.165f), eyeWhiteC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeIrisL",  bodyRoot.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(-0.09f, 0.73f, -0.178f), eyeC, true, Quaternion.Euler(0f, 0f, 0f));
        MakeBlock("EyeIrisR",  bodyRoot.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0.09f, 0.73f, -0.178f), eyeC, true, Quaternion.Euler(0f, 0f, 0f));

        // ═══ LEFT ARM ROOT ═══
        var leftArmRoot = new GameObject("LeftArmRoot");
        leftArmRoot.transform.SetParent(root.transform);
        leftArmRoot.transform.localPosition = new Vector3(-0.29f, 0.33f, 0f);
        leftArmRoot.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
        leftArmRoot.transform.localScale = Vector3.one;

        MakeBlock("UpperArmL",   leftArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, 0f, 0f), skinC, true);
        MakeBlock("SleeveL",     leftArmRoot.transform, new Vector3(0.13f, 0.26f, 0.13f), new Vector3(0f, -0.18f, 0f), topC, true);
        MakeBlock("SleeveTrimL", leftArmRoot.transform, new Vector3(0.15f, 0.04f, 0.15f), new Vector3(0f, -0.32f, 0f), bootTrimC, true);
        MakeBlock("HandL",       leftArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, -0.39f, 0f), skinC, true);

        // ═══ RIGHT ARM ROOT ═══
        var rightArmRoot = new GameObject("RightArmRoot");
        rightArmRoot.transform.SetParent(root.transform);
        rightArmRoot.transform.localPosition = new Vector3(0.29f, 0.33f, 0f);
        rightArmRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        rightArmRoot.transform.localScale = Vector3.one;

        MakeBlock("UpperArmR",   rightArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, 0f, 0f), skinC, true);
        MakeBlock("SleeveR",     rightArmRoot.transform, new Vector3(0.13f, 0.26f, 0.13f), new Vector3(0f, -0.18f, 0f), topC, true);
        MakeBlock("SleeveTrimR", rightArmRoot.transform, new Vector3(0.15f, 0.04f, 0.15f), new Vector3(0f, -0.32f, 0f), bootTrimC, true);
        MakeBlock("HandR",       rightArmRoot.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0f, -0.39f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.8f, 1.7f, 0.6f);
        col.center = new Vector3(0f, 0.25f, 0f);
        col.isTrigger = true;

        return root;
    }

    private static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false, Quaternion rotation = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        if (rotation != default) go.transform.localRotation = rotation;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        if (removeCollider)
            Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    [System.Serializable]
    public class WifeSaveData
    {
        public int state;
        public bool married;
    }
}
