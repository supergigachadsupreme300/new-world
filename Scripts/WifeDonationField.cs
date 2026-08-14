using System.Collections.Generic;
using UnityEngine;

public class WifeDonationField : MonoBehaviour
{
    public static WifeDonationField Instance { get; private set; }

    private BoxCollider _triggerCol;
    private readonly HashSet<GameObject> _consumed = new HashSet<GameObject>();
    private bool _hintShown;
    private bool _shown = true;
    private Transform _arrowRoot;
    private float _arrowBaseY = 2.8f;
    private TMPro.TextMeshPro _label;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        RefreshVisibility();

        if (GameManager.Instance == null || !GameManager.Instance.InGame || GameManager.Instance.GamePaused)
            return;

        var player = GameManager.Instance.Player;
        if (player == null)
            return;

        float dist = Vector3.Distance(player.transform.position, transform.position);
        if (dist < 4f && !_hintShown)
        {
            string material = WifeNPC.Instance != null ? WifeNPC.Instance.GetActiveMaterialName() : null;
            if (!string.IsNullOrEmpty(material))
            {
                _hintShown = true;
                GameManager.Instance.UIManager?.ShowMessage(
                    Localization.F("Nhấn Q để ném {0} vào rổ của Jessica!", DisplayName(material)), 3f);
            }
        }
        else if (dist >= 5f)
        {
            _hintShown = false;
        }

        if (_arrowRoot != null)
        {
            float bob = Mathf.Sin(Time.time * 2f) * 0.08f;
            _arrowRoot.localPosition = new Vector3(0f, _arrowBaseY + bob, 0f);
        }
    }

    private void RefreshVisibility()
    {
        bool show = WifeNPC.Instance != null && !string.IsNullOrEmpty(WifeNPC.Instance.GetActiveMaterialName());
        if (show == _shown)
            return;

        _shown = show;
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(_shown);
        if (_triggerCol != null)
            _triggerCol.enabled = _shown;
    }

    private void LateUpdate()
    {
        _consumed.Clear();
    }

    public static WifeDonationField Build(Transform parent, Vector3 position)
    {
        var go = new GameObject("WifeDonationField");
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;

        var field = go.AddComponent<WifeDonationField>();
        field.BuildVisual();
        field.BuildTrigger();
        return field;
    }

    private void BuildVisual()
    {
        Color woodC = new Color(0.52f, 0.33f, 0.18f);
        Color rimC = new Color(0.35f, 0.22f, 0.11f);

        MakeBox("Base", transform, new Vector3(3.0f, 0.18f, 3.0f), new Vector3(0f, 0.09f, 0f), woodC);
        MakeBox("SideN", transform, new Vector3(3.0f, 1.0f, 0.18f), new Vector3(0f, 0.68f, 1.41f), woodC);
        MakeBox("SideS", transform, new Vector3(3.0f, 1.0f, 0.18f), new Vector3(0f, 0.68f, -1.41f), woodC);
        MakeBox("SideE", transform, new Vector3(0.18f, 1.0f, 3.0f), new Vector3(1.41f, 0.68f, 0f), woodC);
        MakeBox("SideW", transform, new Vector3(0.18f, 1.0f, 3.0f), new Vector3(-1.41f, 0.68f, 0f), woodC);
        MakeBox("RimN", transform, new Vector3(3.2f, 0.14f, 0.2f), new Vector3(0f, 1.2f, 1.5f), rimC);
        MakeBox("RimS", transform, new Vector3(3.2f, 0.14f, 0.2f), new Vector3(0f, 1.2f, -1.5f), rimC);
        MakeBox("RimE", transform, new Vector3(0.2f, 0.14f, 3.2f), new Vector3(1.5f, 1.2f, 0f), rimC);
        MakeBox("RimW", transform, new Vector3(0.2f, 0.14f, 3.2f), new Vector3(-1.5f, 1.2f, 0f), rimC);

        BuildArrow();
        BuildLabel();
    }

    private void BuildArrow()
    {
        _arrowRoot = new GameObject("DonationArrow").transform;
        _arrowRoot.SetParent(transform, false);
        _arrowRoot.localPosition = new Vector3(0f, _arrowBaseY, 0f);

        Color shaftC = new Color(1f, 0.72f, 0.12f);
        Color headC = new Color(1f, 0.9f, 0.3f);

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "ArrowShaft";
        shaft.transform.SetParent(_arrowRoot, false);
        shaft.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        shaft.transform.localScale = new Vector3(0.16f, 0.9f, 0.16f);
        Destroy(shaft.GetComponent<Collider>());
        shaft.GetComponent<Renderer>().material.color = shaftC;

        MakeCone(new Vector3(0f, -0.3f, 0f), 0.25f, 0.6f, headC);
    }

    private void MakeCone(Vector3 localPos, float radius, float height, Color color)
    {
        int layers = 5;
        float layerH = height / layers;
        for (int i = 0; i < layers; i++)
        {
            float t = i / (float)(layers - 1);
            float r = Mathf.Lerp(0.02f, radius, t);
            var layer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            layer.name = "ArrowHead";
            layer.transform.SetParent(_arrowRoot, false);
            layer.transform.localPosition = localPos + Vector3.up * (t * height - height * 0.5f + layerH * 0.5f);
            layer.transform.localScale = new Vector3(r * 2f, layerH * 0.5f, r * 2f);
            Destroy(layer.GetComponent<Collider>());
            layer.GetComponent<Renderer>().material.color = color;
        }
    }

    private void BuildLabel()
    {
        var labelGO = new GameObject("DonationLabel");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 4.1f, 0f);

        _label = labelGO.AddComponent<TMPro.TextMeshPro>();
        _label.alignment = TMPro.TextAlignmentOptions.Center;
        _label.fontSize = 1.1f;
        _label.color = Color.white;
        _label.outlineWidth = 0.12f;
        _label.outlineColor = Color.black;
        _label.rectTransform.sizeDelta = new Vector3(16f, 2f);
        RefreshLabel();

        Localization.OnLanguageChanged += RefreshLabel;
    }

    private void RefreshLabel()
    {
        if (_label != null)
            _label.text = Localization.T("Bỏ vật phẩm nhiệm vụ hàng ngày vào đây");
    }

    private void OnDestroy()
    {
        Localization.OnLanguageChanged -= RefreshLabel;
    }

    private void BuildTrigger()
    {
        _triggerCol = gameObject.AddComponent<BoxCollider>();
        _triggerCol.isTrigger = true;
        _triggerCol.center = new Vector3(0f, 1.0f, 0f);
        _triggerCol.size = new Vector3(3.4f, 1.8f, 3.4f);
    }

    private void MakeBox(string name, Transform parent, Vector3 scale, Vector3 localPos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = color;
        Destroy(go.GetComponent<Collider>());
    }

    private void OnTriggerEnter(Collider other)
    {
        var root = FindThrownRoot(other.transform);
        if (root == null || _consumed.Contains(root))
            return;

        string material = ClassifyMaterial(root);
        if (material == null)
            return;

        if (material == "animal")
        {
            var proj = root.GetComponentInChildren<ThrownCageProjectile>();
            if (proj == null || !proj.HasCapturedAnimal)
                return;
        }

        if (WifeNPC.Instance != null && WifeNPC.Instance.TryDepositMaterial(material, out int progress, out int count))
        {
            _consumed.Add(root);
            Destroy(root);
            ShowDepositMessage(material, progress, count);
        }
    }

    public static bool TryDonateCage(Transform cage)
    {
        if (Instance == null || cage == null || Instance._triggerCol == null || !Instance._triggerCol.enabled)
            return false;

        var root = cage.gameObject;
        if (Instance._consumed.Contains(root))
            return false;

        if (!Instance._triggerCol.bounds.Contains(cage.position))
            return false;

        var proj = cage.GetComponent<ThrownCageProjectile>();
        if (proj != null && !proj.HasCapturedAnimal)
            return false;

        if (WifeNPC.Instance != null && WifeNPC.Instance.TryDepositMaterial("animal", out int progress, out int count))
        {
            Instance._consumed.Add(root);
            Destroy(root);
            Instance.ShowDepositMessage("animal", progress, count);
            return true;
        }
        return false;
    }

    private void ShowDepositMessage(string material, int progress, int count)
    {
        SoundManager.Instance?.Play("pop", 0.8f);
        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.F("Đã nộp {0} cho Jessica! ({1}/{2})", DisplayName(material), progress, count), 1.5f);
    }

    private static string DisplayName(string material)
    {
        return material == "animal" ? Localization.T("Lồng Thú") : Localization.ItemName(material);
    }

    private static GameObject FindThrownRoot(Transform t)
    {
        if (t == null)
            return null;
        var root = t.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;
        return root;
    }

    private static string ClassifyMaterial(GameObject root)
    {
        if (root == null)
            return null;
        string name = root.name;
        if (name.StartsWith("Pickup_"))
            return name.Substring("Pickup_".Length);
        if (name == "TreeFelled" || name == "BranchTop")
            return "wood";
        if (name == "RockDebris")
            return "stone";
        if (name == "ThrownCage" || name == "CageWithAnimal")
            return "animal";
        return null;
    }
}
