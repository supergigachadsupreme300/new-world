using System.Collections.Generic;
using UnityEngine;

public class WifeDonationField : MonoBehaviour
{
    public static WifeDonationField Instance { get; private set; }

    private BoxCollider _triggerCol;
    private readonly HashSet<GameObject> _consumed = new HashSet<GameObject>();
    private bool _hintShown;

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

        MakeBox("Base", transform, new Vector3(2.0f, 0.12f, 2.0f), new Vector3(0f, 0.06f, 0f), woodC);
        MakeBox("SideN", transform, new Vector3(2.0f, 0.7f, 0.12f), new Vector3(0f, 0.47f, 0.94f), woodC);
        MakeBox("SideS", transform, new Vector3(2.0f, 0.7f, 0.12f), new Vector3(0f, 0.47f, -0.94f), woodC);
        MakeBox("SideE", transform, new Vector3(0.12f, 0.7f, 2.0f), new Vector3(0.94f, 0.47f, 0f), woodC);
        MakeBox("SideW", transform, new Vector3(0.12f, 0.7f, 2.0f), new Vector3(-0.94f, 0.47f, 0f), woodC);
        MakeBox("RimN", transform, new Vector3(2.2f, 0.1f, 0.14f), new Vector3(0f, 0.9f, 1.03f), rimC);
        MakeBox("RimS", transform, new Vector3(2.2f, 0.1f, 0.14f), new Vector3(0f, 0.9f, -1.03f), rimC);
        MakeBox("RimE", transform, new Vector3(0.14f, 0.1f, 2.2f), new Vector3(1.03f, 0.9f, 0f), rimC);
        MakeBox("RimW", transform, new Vector3(0.14f, 0.1f, 2.2f), new Vector3(-1.03f, 0.9f, 0f), rimC);
    }

    private void BuildTrigger()
    {
        _triggerCol = gameObject.AddComponent<BoxCollider>();
        _triggerCol.isTrigger = true;
        _triggerCol.center = new Vector3(0f, 0.85f, 0f);
        _triggerCol.size = new Vector3(2.2f, 1.4f, 2.2f);
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
        if (Instance == null || cage == null || Instance._triggerCol == null)
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
