using System.Collections.Generic;
using UnityEngine;
using CountryLife.Helpers;

public enum PlayerGender { Male, Female }

public static partial class MapBuilder
{
    public static PlayerGender ActiveGender = PlayerGender.Male;

    private static readonly Dictionary<Color, Material> _colorMatCache = new Dictionary<Color, Material>();

    public static void ApplyBlockColor(Renderer r, Color color)
    {
        if (r == null) return;
        if (_colorMatCache.TryGetValue(color, out var cached) && cached != null)
        {
            r.sharedMaterial = cached;
            return;
        }
        var mat = new Material(r.sharedMaterial);
        mat.color = color;
        mat.name = "BlockMat_" + color;
        _colorMatCache[color] = mat;
        r.sharedMaterial = mat;
    }

    // ═══════════════════════════════════════════════════════════════
    //  LOW-LEVEL BLOCK
    // ═══════════════════════════════════════════════════════════════

    public static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false, Quaternion rotation = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        if (rotation != default) go.transform.localRotation = rotation;
        var r = go.GetComponent<Renderer>();
        ApplyBlockColor(r, color);
        if (removeCollider)
            Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    private static void SetTransparent(Renderer r, float alpha)
    {
        r.material = PickupVisualHelper.CreateTransparentMaterial(new Color(1f, 1f, 1f, alpha), 0.8f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SHARED HELPERS
    // ═══════════════════════════════════════════════════════════════

    public static Light BuildStreetLamp(Transform parent, Vector3 position)
    {
        Color lampC = new Color(0.18f, 0.16f, 0.14f);
        Color headC = new Color(0.9f, 0.35f, 0.2f);
        Color glowC = new Color(1f, 0.85f, 0.45f);
        MakeBlock("StreetLampBase", parent, new Vector3(0.36f, 0.08f, 0.36f), position + new Vector3(0f, 0.05f, 0f), lampC, true);
        MakeBlock("StreetLampPole", parent, new Vector3(0.14f, 2.9f, 0.14f), position + new Vector3(0f, 1.5f, 0f), lampC, true);
        MakeBlock("StreetLampHead", parent, new Vector3(0.36f, 0.28f, 0.36f), position + new Vector3(0f, 3.0f, 0f), headC, true);
        var glow = MakeBlock("StreetLampGlow", parent, new Vector3(0.2f, 0.16f, 0.2f), position + new Vector3(0f, 2.98f, 0f), glowC, true);
        DisableShadowCasting(glow);
        return AddGlowLight(parent, position + new Vector3(0f, 3.18f, 0f), 22f, 3.2f, new Color(1f, 0.85f, 0.45f), "StreetLight");
    }

    public static Light AddGlowLight(Transform parent, Vector3 position, float range, float intensity, Color color, string goName = "GlowPointLight")
    {
        var lampGo = new GameObject(goName);
        lampGo.transform.SetParent(parent);
        lampGo.transform.localPosition = position;
        var light = lampGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    public static void DisableShadowCasting(GameObject go)
    {
        if (go == null) return;
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void AddEntranceLight(Transform parent, Vector3 position)
    {
        var lampGo = new GameObject("RestaurantPointLight");
        lampGo.transform.SetParent(parent);
        lampGo.transform.localPosition = position;
        var light = lampGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.55f);
        light.intensity = 1.6f;
        light.range = 6f;
    }

    private static void BuildCypress(Transform parent, Vector3 position, float scale, string tag, Color leafC, Color trunkC)
    {
        var root = new GameObject("Cypress" + tag);
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localScale = Vector3.one * scale;
        MakeBlock("Trunk", root.transform, new Vector3(0.3f, 1.3f, 0.3f), new Vector3(0f, 0.65f, 0f), trunkC, true);
        MakeBlock("Leaf1", root.transform, new Vector3(1.9f, 1.3f, 1.9f), new Vector3(0f, 1.4f, 0f), leafC, true);
        MakeBlock("Leaf2", root.transform, new Vector3(1.4f, 1.2f, 1.4f), new Vector3(0f, 2.5f, 0f), leafC, true);
        MakeBlock("Leaf3", root.transform, new Vector3(0.95f, 1.2f, 0.95f), new Vector3(0f, 3.6f, 0f), leafC, true);
        MakeBlock("Leaf4", root.transform, new Vector3(0.5f, 1f, 0.5f), new Vector3(0f, 4.7f, 0f), leafC, true);
    }

    private static void AddWindowTrim(Transform parent, Vector3 center, float w, float h, Vector3 outward, Color frameC, Color stoneC, string suffix)
    {
        Vector3 basePos = center + outward * 0.3f;
        MakeBlock("WinSill" + suffix, parent, new Vector3(w + 0.5f, 0.12f, 0.35f),
            basePos + new Vector3(0f, -(h / 2f + 0.06f), 0f), stoneC, true);
        MakeBlock("WinHead" + suffix, parent, new Vector3(w + 0.5f, 0.16f, 0.35f),
            basePos + new Vector3(0f, h / 2f + 0.08f, 0f), stoneC, true);
        MakeBlock("ShutterL" + suffix, parent, new Vector3(0.28f, h, 0.08f),
            center + outward * 0.2f + new Vector3(-(w / 2f + 0.16f), 0f, 0f), frameC, true);
        MakeBlock("ShutterR" + suffix, parent, new Vector3(0.28f, h, 0.08f),
            center + outward * 0.2f + new Vector3(w / 2f + 0.16f, 0f, 0f), frameC, true);
    }

    // ==================== MapBuilderCafe.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  CAFE  (south of the shop)
    // ═══════════════════════════════════════════════════════════════
    public static GameObject BuildCafe(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("Cafe");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.84f, 0.7f, 0.52f);
        Color trimC    = new Color(0.42f, 0.27f, 0.14f);
        Color roofC    = new Color(0.55f, 0.35f, 0.2f);
        Color floorC   = new Color(0.45f, 0.32f, 0.2f);
        Color stoneC   = new Color(0.439f, 0.4f, 0.361f);
        Color counterC = new Color(0.584f, 0.294f, 0.165f);
        Color signC    = new Color(0.886f, 0.753f, 0.098f);

        float halfW = 4f;
        float depth = 7f;

        // ── Walls (door on +Z) ──
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, depth), new Vector3(-halfW, 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, depth), new Vector3(halfW, 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(halfW * 2f, 4f, 0.5f), new Vector3(0f, 2f, -depth / 2f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(2.4f, 4f, 0.5f), new Vector3(-2.8f, 2f, depth / 2f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(2.4f, 4f, 0.5f), new Vector3(2.8f, 2f, depth / 2f), wallC);

        // ── Door frame + swinging door (+Z) ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.4f, 0.25f), new Vector3(-1.6f, 1.7f, depth / 2f), trimC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.4f, 0.25f), new Vector3(1.6f, 1.7f, depth / 2f), trimC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(3.7f, 0.35f, 0.25f), new Vector3(0f, 3.7f, depth / 2f), trimC, true);
        var doorPivot = new GameObject("Door");
        doorPivot.transform.SetParent(root.transform);
        doorPivot.transform.localPosition = new Vector3(-1.6f, 2f, depth / 2f);
        var doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorPanel.name = "DoorPanel";
        doorPanel.transform.SetParent(doorPivot.transform);
        doorPanel.transform.localPosition = new Vector3(1.6f, 0f, 0f);
        doorPanel.transform.localScale = new Vector3(3f, 3.2f, 0.25f);
        doorPanel.GetComponent<MeshRenderer>().material.color = trimC;
        doorPanel.AddComponent<BoxCollider>();

        // ── Floor + foundation ──
        MakeBlock("Floor", root.transform, new Vector3(halfW * 2f, 0.5f, depth), Vector3.zero, floorC);
        MakeBlock("Foundation", root.transform, new Vector3(halfW * 2f + 1.5f, 0.4f, depth + 1.5f), new Vector3(0f, -0.25f, 0f), stoneC);

        // ── Flat roof ──
        MakeBlock("Roof", root.transform, new Vector3(halfW * 2f + 1f, 0.35f, depth + 1f), new Vector3(0f, 4.15f, 0f), roofC);
        MakeBlock("RoofTrim", root.transform, new Vector3(halfW * 2f + 1.4f, 0.12f, 0.5f), new Vector3(0f, 4.32f, depth / 2f + 0.6f), trimC, true);

        // ── Sign above the door ──
        MakeBlock("Sign", root.transform, new Vector3(3.6f, 0.7f, 0.2f), new Vector3(0f, 3.2f, depth / 2f + 0.1f), signC, true);
        var signLabel = new GameObject("CafeSignLabel");
        signLabel.transform.SetParent(root.transform);
        signLabel.transform.localPosition = new Vector3(0f, 3.2f, depth / 2f + 0.32f);
        signLabel.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        signLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var signTmp = signLabel.AddComponent<TMPro.TextMeshPro>();
        signTmp.text = Localization.T("QUÁN CÀ PHÊ");
        signTmp.fontSize = 1.5f;
        signTmp.alignment = TMPro.TextAlignmentOptions.Center;
        signTmp.color = new Color(0.3f, 0.15f, 0.05f);
        signTmp.outlineWidth = 0.12f;
        signTmp.outlineColor = Color.white;
        signTmp.rectTransform.sizeDelta = new Vector3(6.4f, 1.2f);

        // ── Counter along the north wall ──
        MakeBlock("Counter", root.transform, new Vector3(6.5f, 1f, 1.1f), new Vector3(0f, 0.5f, -1.9f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(6.7f, 0.08f, 1.2f), new Vector3(0f, 0.98f, -1.9f), new Color(0.757f, 0.62f, 0.404f), true);
        MakeBlock("EspressoMachine", root.transform, new Vector3(0.7f, 0.55f, 0.5f), new Vector3(-1.4f, 1.3f, -1.9f), new Color(0.28f, 0.28f, 0.3f), true);
        MakeBlock("Grinder", root.transform, new Vector3(0.5f, 0.45f, 0.4f), new Vector3(1.4f, 1.25f, -1.9f), new Color(0.5f, 0.4f, 0.3f), true);
        for (int ci = -2; ci <= 2; ci++)
        {
            MakeBlock("Cup", root.transform, new Vector3(0.16f, 0.16f, 0.16f),
                new Vector3(ci * 0.7f, 1.06f, -1.9f),
                ci % 2 == 0 ? new Color(0.95f, 0.95f, 0.92f) : new Color(0.65f, 0.35f, 0.2f), true);
        }

        // ── Barista behind the counter ──
        BuildCafeBaristaNpc(root.transform, new Vector3(0f, 1.13f, -3.2f), Quaternion.Euler(0f, 180f, 0f));

        // ── Interior tables + stools ──
        foreach (float tx in new[] { -2.2f, 2.2f })
        {
            MakeBlock("Table", root.transform, new Vector3(1.1f, 0.08f, 0.8f), new Vector3(tx, 0.72f, 1.2f), new Color(0.6f, 0.42f, 0.24f), true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.68f, 0.1f), new Vector3(tx - 0.4f, 0.36f, 0.9f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.68f, 0.1f), new Vector3(tx + 0.4f, 0.36f, 0.9f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.68f, 0.1f), new Vector3(tx - 0.4f, 0.36f, 1.5f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.68f, 0.1f), new Vector3(tx + 0.4f, 0.36f, 1.5f), trimC, true);
            MakeBlock("Stool", root.transform, new Vector3(0.32f, 0.5f, 0.32f), new Vector3(tx, 0.25f, 2.15f), trimC, true).AddComponent<SittableSeat>();
            MakeBlock("CoffeeCup", root.transform, new Vector3(0.16f, 0.1f, 0.16f), new Vector3(tx - 0.2f, 0.78f, 1.05f), new Color(0.95f, 0.95f, 0.92f), true);
            MakeBlock("CoffeeCup", root.transform, new Vector3(0.16f, 0.1f, 0.16f), new Vector3(tx + 0.25f, 0.78f, 1.35f), new Color(0.95f, 0.95f, 0.92f), true);
        }

        // ── Warm interior lights ──
        AddEntranceLight(root.transform, new Vector3(0f, 3.4f, 1f));
        AddEntranceLight(root.transform, new Vector3(0f, 3.4f, -1f));

        return root;
    }

    private static void BuildCafeBaristaNpc(Transform parent, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("CafeNPC");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        Color shirtC = new Color(0.45f, 0.2f, 0.12f);
        Color pantsC = new Color(0.2f, 0.2f, 0.22f);
        Color skinC  = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color apronC = new Color(0.8f, 0.78f, 0.75f);

        MakeBlock("LegL", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(-0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(-0.15f, -0.88f, 0f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("BootR", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0.15f, -0.88f, 0f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("Body", root.transform, new Vector3(0.52f, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), shirtC, true);
        MakeBlock("Apron", root.transform, new Vector3(0.5f, 0.42f, 0.06f), new Vector3(0f, -0.05f, 0.11f), apronC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.33f, 0.1f, 0.33f), new Vector3(0f, 0.66f, 0f), new Color(0.15f, 0.1f, 0.07f), true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);
        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.36f, 0.1f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.36f, 0.1f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.36f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.36f, -0.14f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        root.AddComponent<CafeBarista>();
    }
    // ==================== MapBuilderCar.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  CAR MODEL  (blocky voxel car for cutscenes / menu)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildCar(Transform parent, Vector3 position = default, Color? bodyColor = null)
    {
        var root = new GameObject("Car");
        root.transform.SetParent(parent);
        root.transform.position = position;

        Color bodyC = bodyColor ?? new Color(0.2f, 0.55f, 0.9f);
        Color cabinC = new Color(0.15f, 0.4f, 0.75f);
        Color windowC = new Color(0.6f, 0.8f, 1f, 0.7f);
        Color wheelC = new Color(0.12f, 0.12f, 0.12f);
        Color rimC = new Color(0.6f, 0.6f, 0.6f);
        Color headlightC = new Color(1f, 0.95f, 0.7f);
        Color bumperC = new Color(0.3f, 0.3f, 0.3f);
        Color seatC = new Color(0.25f, 0.15f, 0.1f);

        // ── Chassis / body ──
        MakeBlock("Body", root.transform, new Vector3(2f, 0.6f, 4.2f), new Vector3(0f, 0.55f, 0f), bodyC, true);
        // ── Hood (front) ──
        MakeBlock("Hood", root.transform, new Vector3(1.8f, 0.3f, 1f), new Vector3(0f, 1.0f, 1.4f), bodyC, true);
        // ── Trunk (rear) ──
        MakeBlock("Trunk", root.transform, new Vector3(1.8f, 0.3f, 0.8f), new Vector3(0f, 1.0f, -1.7f), bodyC, true);
        // ── Bumpers ──
        MakeBlock("BumperF", root.transform, new Vector3(2.05f, 0.15f, 0.12f), new Vector3(0f, 0.43f, 2.15f), bumperC, true);
        MakeBlock("BumperR", root.transform, new Vector3(2.05f, 0.15f, 0.12f), new Vector3(0f, 0.43f, -2.15f), bumperC, true);
        // ── Headlights ──
        MakeBlock("HeadlightL", root.transform, new Vector3(0.2f, 0.15f, 0.06f), new Vector3(-0.7f, 0.6f, 2.14f), headlightC, true);
        MakeBlock("HeadlightR", root.transform, new Vector3(0.2f, 0.15f, 0.06f), new Vector3(0.7f, 0.6f, 2.14f), headlightC, true);
        // ── Roof ──
        MakeBlock("Roof", root.transform, new Vector3(1.8f, 0.08f, 2.2f), new Vector3(0f, 1.67f, -0.2f), cabinC, true);
        // ── A-pillars (front corners) ──
        MakeBlock("PillarFL", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(-0.85f, 1.27f, 0.88f), cabinC, true);
        MakeBlock("PillarFR", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(0.85f, 1.27f, 0.88f), cabinC, true);
        // ── C-pillars (rear corners) ──
        MakeBlock("PillarRL", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(-0.85f, 1.27f, -1.28f), cabinC, true);
        MakeBlock("PillarRR", root.transform, new Vector3(0.1f, 0.75f, 0.1f), new Vector3(0.85f, 1.27f, -1.28f), cabinC, true);
        // ── Door panels (below window line) ──
        MakeBlock("DoorL", root.transform, new Vector3(0.08f, 0.35f, 2.1f), new Vector3(-0.9f, 1.0f, -0.2f), bodyC, true);
        MakeBlock("DoorR", root.transform, new Vector3(0.08f, 0.35f, 2.1f), new Vector3(0.9f, 1.0f, -0.2f), bodyC, true);
        // ── Front wall (below windshield) ──
        MakeBlock("FrontWall", root.transform, new Vector3(1.6f, 0.28f, 0.08f), new Vector3(0f, 0.97f, 0.88f), cabinC, true);
        // ── Rear wall (below rear window) ──
        MakeBlock("RearWall", root.transform, new Vector3(1.5f, 0.28f, 0.08f), new Vector3(0f, 0.97f, -1.3f), cabinC, true);
        // ── Steering wheel ──
        MakeBlock("SteeringWheel", root.transform, new Vector3(0.35f, 0.35f, 0.05f), new Vector3(-0.35f, 1.15f, 0.35f), Color.black, true).transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
        // ── Seats (base + backrest) ──
        MakeBlock("SeatBaseL", root.transform, new Vector3(0.35f, 0.12f, 0.35f), new Vector3(-0.35f, 0.65f, -0.2f), seatC, true);
        MakeBlock("SeatBackL", root.transform, new Vector3(0.35f, 0.35f, 0.08f), new Vector3(-0.35f, 0.85f, -0.38f), seatC, true);
        MakeBlock("SeatBaseR", root.transform, new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0.35f, 0.65f, -0.2f), seatC, true);
        MakeBlock("SeatBackR", root.transform, new Vector3(0.35f, 0.35f, 0.08f), new Vector3(0.35f, 0.85f, -0.38f), seatC, true);
        // ── Interior floor ──
        MakeBlock("InteriorFloor", root.transform, new Vector3(1.6f, 0.06f, 1.8f), new Vector3(0f, 0.86f, -0.2f), new Color(0.18f, 0.18f, 0.18f), true);

        // ── Wheels (4) ──
        float wheelY = 0.37f;
        float wheelH = 0.42f;
        float wheelD = 0.42f;
        float wheelW = 0.42f;
        float xOff = 0.95f;
        float zFront = 1.3f;
        float zRear = -1.3f;
        MakeBlock("WheelFL", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(-xOff, wheelY, zFront), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelFR", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(xOff, wheelY, zFront), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelRL", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(-xOff, wheelY, zRear), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelRR", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(xOff, wheelY, zRear), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // ── Rim spokes (cross pattern, equal length) ──
        float spokeLen = 0.36f;
        float spokeW = 0.04f;
        string[] wheelNames = { "WheelFL", "WheelFR", "WheelRL", "WheelRR" };
        float[] wheelX = { -xOff, xOff, -xOff, xOff };
        float[] wheelZ = { zFront, zFront, zRear, zRear };
        for (int wi = 0; wi < 4; wi++)
        {
            var wt = root.transform.Find(wheelNames[wi]);
            if (wt == null) continue;
            MakeBlock("SpokeH", wt.transform, new Vector3(0.05f, spokeLen, spokeW), new Vector3(0f, 0f, 0f), rimC, true);
            MakeBlock("SpokeV", wt.transform, new Vector3(0.05f, spokeW, spokeLen), new Vector3(0f, 0f, 0f), rimC, true);
        }
        // ── Rim caps ──
        float rimS = 0.12f;
        MakeBlock("RimFL", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(-xOff - 0.12f, wheelY, zFront), rimC, true);
        MakeBlock("RimFR", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(xOff + 0.12f, wheelY, zFront), rimC, true);
        MakeBlock("RimRL", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(-xOff - 0.12f, wheelY, zRear), rimC, true);
        MakeBlock("RimRR", root.transform, new Vector3(rimS, rimS, 0.05f), new Vector3(xOff + 0.12f, wheelY, zRear), rimC, true);

        return root;
    }

    public static GameObject BuildPoliceCar(Transform parent, Vector3 position = default)
    {
        var root = BuildCar(parent, position, new Color(0.1f, 0.12f, 0.25f));
        root.name = "PoliceCar";

        Color whiteC = new Color(0.95f, 0.95f, 0.95f);
        Color redC = new Color(0.85f, 0.1f, 0.1f);
        Color blueC = new Color(0.15f, 0.15f, 0.5f);

        MakeBlock("PoliceStripeL", root.transform, new Vector3(0.09f, 0.16f, 1.9f), new Vector3(-0.9f, 0.9f, -0.2f), whiteC, true);
        MakeBlock("PoliceStripeR", root.transform, new Vector3(0.09f, 0.16f, 1.9f), new Vector3(0.9f, 0.9f, -0.2f), whiteC, true);
        MakeBlock("LightBar", root.transform, new Vector3(0.5f, 0.12f, 0.5f), new Vector3(0f, 1.78f, -0.2f), new Color(0.2f, 0.2f, 0.25f), true);
        MakeBlock("LightRed", root.transform, new Vector3(0.34f, 0.09f, 0.16f), new Vector3(-0.17f, 1.84f, -0.2f), redC, true);
        MakeBlock("LightBlue", root.transform, new Vector3(0.34f, 0.09f, 0.16f), new Vector3(0.17f, 1.84f, -0.2f), blueC, true);

        return root;
    }
    // ==================== MapBuilderConvenienceStore.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  CONVENIENCE STORE  (small market kiosk between Shop & Restaurant)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildConvenienceStore(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("ConvenienceStore");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.9f, 0.88f, 0.84f);
        Color roofC    = new Color(0.85f, 0.24f, 0.18f);
        Color eaveC    = new Color(0.2f, 0.2f, 0.2f);
        Color floorC   = new Color(0.78f, 0.78f, 0.76f);
        Color frameC   = new Color(0.35f, 0.35f, 0.38f);
        Color counterC = new Color(0.45f, 0.55f, 0.6f);
        Color shelfC   = new Color(0.5f, 0.5f, 0.52f);
        Color signC    = new Color(0.85f, 0.24f, 0.18f);
        Color winC     = new Color(0.55f, 0.78f, 0.86f);

        float hw = 4f;
        float hd = 3.5f;
        float wallH = 4.5f;

        // ── Floor ──
        MakeBlock("Floor", root.transform, new Vector3(8f, 0.25f, 7f), new Vector3(0f, 0.125f, 0f), floorC);

        // ── Walls (entrance on +x, facing the road) ──
        MakeBlock("Wall", root.transform, new Vector3(0.5f, wallH, 7f), new Vector3(-hw, wallH / 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(8f, wallH, 0.5f), new Vector3(0f, wallH / 2f, hd), wallC);
        MakeBlock("Wall", root.transform, new Vector3(8f, wallH, 0.5f), new Vector3(0f, wallH / 2f, -hd), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, wallH, 2.7f), new Vector3(hw, wallH / 2f, -2.15f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, wallH, 2.7f), new Vector3(hw, wallH / 2f, 2.15f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1.3f, 1.6f), new Vector3(hw, wallH - 0.65f, 0f), wallC);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.2f, 3.2f, 0.2f), new Vector3(hw, 1.6f, -0.8f), frameC);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.2f, 3.2f, 0.2f), new Vector3(hw, 1.6f, 0.8f), frameC);

        // ── Flat roof with overhang ──
        MakeBlock("Roof", root.transform, new Vector3(9f, 0.5f, 8f), new Vector3(0f, wallH + 0.25f, 0f), roofC);
        MakeBlock("Eave", root.transform, new Vector3(9.4f, 0.2f, 8.4f), new Vector3(0f, wallH + 0.5f, 0f), eaveC, true);

        // ── Sign above the door ──
        MakeBlock("Sign", root.transform, new Vector3(0.25f, 0.9f, 5f), new Vector3(hw + 0.3f, 3.6f, 0f), signC, true);
        var storeSignLabel = new GameObject("StoreSignLabel");
        storeSignLabel.transform.SetParent(root.transform);
        storeSignLabel.transform.localPosition = new Vector3(hw + 0.5f, 3.6f, 0f);
        storeSignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        storeSignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var storeSignTmp = storeSignLabel.AddComponent<TMPro.TextMeshPro>();
        storeSignTmp.text = Localization.T("TIỆN LỢI");
        storeSignTmp.fontSize = 2.0f;
        storeSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        storeSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        storeSignTmp.outlineWidth = 0.18f;
        storeSignTmp.outlineColor = Color.black;
        storeSignTmp.rectTransform.sizeDelta = new Vector3(8.4f, 1.5f);

        // ── Window on the back wall ──
        MakeBlock("WinGlass", root.transform, new Vector3(0.12f, 1.4f, 2f), new Vector3(-hw - 0.02f, 2.3f, 0f), winC, true);

        // ── Counter inside (moved in from the door) ──
        MakeBlock("Counter", root.transform, new Vector3(2f, 1f, 4.5f), new Vector3(1.0f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(2f, 0.08f, 4.7f), new Vector3(1.0f, 0.98f, 0f), new Color(0.7f, 0.75f, 0.8f), true);
        MakeBlock("Register", root.transform, new Vector3(0.5f, 0.4f, 0.5f), new Vector3(1.2f, 1.22f, 0f), frameC, true);

        // ── Shelf units flanking the back wall (4 tiers each) ──
        Color[] itemColors =
        {
            new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.6f, 0.3f),
            new Color(0.95f, 0.7f, 0.15f), new Color(0.5f, 0.4f, 0.8f)
        };
        foreach (float zc in new[] { -1.9f, 1.9f })
        {
            string tag = zc < 0 ? "L" : "R";
            MakeBlock("ShelfPost" + tag, root.transform, new Vector3(0.15f, 3.8f, 0.15f), new Vector3(-2.9f, 1.9f, zc - 1.5f), frameC);
            MakeBlock("ShelfPost" + tag, root.transform, new Vector3(0.15f, 3.8f, 0.15f), new Vector3(-2.9f, 1.9f, zc + 1.5f), frameC);
            for (int i = 0; i < 4; i++)
            {
                float sy = 0.5f + i * 0.9f;
                MakeBlock("ShelfBoard" + tag, root.transform, new Vector3(0.15f, 0.08f, 3.2f), new Vector3(-2.9f, sy, zc), shelfC);
                for (int k = 0; k < 3; k++)
                {
                    float iz = zc + (k - 1) * 0.9f;
                    MakeBlock("ShelfItem" + tag, root.transform, new Vector3(0.28f, 0.28f, 0.28f), new Vector3(-2.9f, sy + 0.2f, iz), itemColors[(i + k) % itemColors.Length]);
                }
            }
        }

        // ── Shopkeeper behind the counter, facing the door ──
        BuildMarketNpc(root.transform, "ConvenienceNPC", new Vector3(-0.3f, 1.13f, 0f), Quaternion.Euler(0f, -90f, 0f));

        return root;
    }
    // ==================== MapBuilderLibrary.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  LIBRARY  (10 x 5 x 8, bookshelves, reading table, entrance on east)
    //  + LIBRARIAN NPC (knowledge vendor / blueprint research)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildLibrary(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("Library");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.93f, 0.88f, 0.79f);
        Color trimC    = new Color(0.4f, 0.27f, 0.15f);
        Color roofC    = new Color(0.24f, 0.33f, 0.45f);
        Color ridgeC   = new Color(0.16f, 0.24f, 0.35f);
        Color eaveC    = new Color(0.2f, 0.2f, 0.2f);
        Color stoneC   = new Color(0.44f, 0.4f, 0.36f);
        Color floorC   = new Color(0.62f, 0.45f, 0.28f);
        Color frameC   = new Color(0.3f, 0.18f, 0.1f);
        Color shelfC   = new Color(0.45f, 0.28f, 0.16f);
        Color signC    = new Color(0.89f, 0.75f, 0.1f);
        Color awningC  = new Color(0.2f, 0.55f, 0.55f);
        Color bookRed  = new Color(0.8f, 0.25f, 0.2f);
        Color bookGrn  = new Color(0.25f, 0.6f, 0.3f);
        Color bookBlu  = new Color(0.25f, 0.4f, 0.75f);
        Color bookGld  = new Color(0.9f, 0.7f, 0.2f);

        // ── Walls (entrance gap on the +x side) ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.4f), new Vector3(0f, 2.5f, -4f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.4f), new Vector3(0f, 2.5f, 4f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.4f, 5f, 8f), new Vector3(-5f, 2.5f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.4f, 5f, 2.5f), new Vector3(5f, 2.5f, -3.25f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.4f, 5f, 2.5f), new Vector3(5f, 2.5f, 3.25f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.4f, 1.2f, 3f), new Vector3(5f, 4.4f, 0f), wallC);
        MakeBlock("WallTrim", root.transform, new Vector3(10.2f, 0.3f, 0.45f), new Vector3(0f, 0.4f, 4f), trimC);
        MakeBlock("WallTrim", root.transform, new Vector3(10.2f, 0.3f, 0.45f), new Vector3(0f, 0.4f, -4f), trimC);

        // ── Floor + stone foundation ──
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.4f, 8f), new Vector3(0f, 0.05f, 0f), floorC);
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.4f, 9.5f), new Vector3(0f, -0.15f, 0f), stoneC);

        // ── Gabled roof ──
        float rise = 2.5f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.2f;
        float roofZ = 8f + overhang * 2f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(-halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.55f, 0.3f, roofZ + 0.2f),
            new Vector3(0f, 5f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(halfW, 5.1f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(-halfW, 5.1f, 0f), eaveC);

        // ── Gable end fillers (triangular approximation) ──
        for (int ge = 0; ge < 2; ge++)
        {
            float gz = ge == 0 ? -4f : 4f;
            for (int i = 0; i < 5; i++)
            {
                float gy = 5f + i * 0.5f;
                float gw = halfW * 2f * (1f - (float)i / 5f);
                MakeBlock("GableEnd", root.transform, new Vector3(gw, 0.5f, 0.35f),
                    new Vector3(0f, gy + 0.25f, gz), wallC);
            }
        }

        // ── Sign above entrance ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3.5f),
            new Vector3(5.08f, 4.4f, 0f), signC, true);
        var librarySignLabel = new GameObject("LibrarySignLabel");
        librarySignLabel.transform.SetParent(root.transform);
        librarySignLabel.transform.localPosition = new Vector3(5.3f, 4.4f, 0f);
        librarySignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        librarySignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var librarySignTmp = librarySignLabel.AddComponent<TMPro.TextMeshPro>();
        librarySignTmp.text = Localization.T("THƯ VIỆN");
        librarySignTmp.fontSize = 2.0f;
        librarySignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        librarySignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        librarySignTmp.outlineWidth = 0.18f;
        librarySignTmp.outlineColor = Color.black;
        librarySignTmp.rectTransform.sizeDelta = new Vector3(6.4f, 1.5f);

        // ── Entrance awning ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.15f, 3.5f),
            new Vector3(5.8f, 5f, 0f), awningC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 5f, 0.12f),
            new Vector3(6.6f, 2.5f, -1.5f), frameC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 5f, 0.12f),
            new Vector3(6.6f, 2.5f, 1.5f), frameC, true);

        // ── Door frame ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, -1.5f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, 1.5f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(5.04f, 3.65f, 0f), frameC, true);

        // ── Windows on front (+z) and west (-x) walls ──
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.2f, 0.14f),
                new Vector3(wx, 2.4f, -4.25f), new Color(0.65f, 0.8f, 0.9f), true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.2f, 0.16f),
                new Vector3(wx, 2.4f, -4.25f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.08f, 0.16f),
                new Vector3(wx, 2.4f, -4.25f), frameC, true);
        }
        MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.2f, 1.4f),
            new Vector3(-5.25f, 2.4f, 2f), new Color(0.65f, 0.8f, 0.9f), true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.2f, 0.1f),
            new Vector3(-5.25f, 2.4f, 2f), frameC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.08f, 1.4f),
            new Vector3(-5.25f, 2.4f, 2f), frameC, true);

        // ── Bookshelves along the back (-z) wall ──
        foreach (float sx in new[] { -2.6f, 2.6f })
        {
            MakeBlock("ShelfUnit", root.transform, new Vector3(2.2f, 3.2f, 0.4f),
                new Vector3(sx, 1.85f, -3.45f), shelfC);
            for (int row = 0; row < 3; row++)
            {
                float by = 0.7f + row * 0.95f;
                MakeBlock("BookShelfBoard", root.transform, new Vector3(1.9f, 0.07f, 0.32f),
                    new Vector3(sx, by - 0.31f, -3.2f), shelfC);
                for (int i = 0; i < 5; i++)
                {
                    Color bc = i % 4 == 0 ? bookRed : (i % 4 == 1 ? bookGrn : (i % 4 == 2 ? bookBlu : bookGld));
                    MakeBlock("Book", root.transform, new Vector3(0.2f, 0.55f, 0.2f),
                        new Vector3(sx - 0.8f + i * 0.4f, by, -3.2f), bc, true);
                }
            }
            MakeBlock("BookShelfBoard", root.transform, new Vector3(1.9f, 0.07f, 0.32f),
                new Vector3(sx, 2.86f, -3.2f), shelfC);
            for (int i = 0; i < 4; i++)
            {
                Color bc = i % 4 == 0 ? bookGld : (i % 4 == 1 ? bookRed : (i % 4 == 2 ? bookGrn : bookBlu));
                MakeBlock("BookTop", root.transform, new Vector3(0.18f, 0.45f, 0.18f),
                    new Vector3(sx - 0.68f + i * 0.44f, 3.15f, -3.2f), bc, true);
            }
        }
        MakeBlock("BookShelfBoard", root.transform, new Vector3(2.0f, 0.07f, 0.32f),
            new Vector3(0f, 2.53f, -3.2f), shelfC);
        MakeBlock("BookRowGld", root.transform, new Vector3(1.9f, 0.5f, 0.22f),
            new Vector3(0f, 2.85f, -3.2f), bookGld, true);

        // ── Bookshelves along the front (+z) wall ──
        foreach (float sx in new[] { -2.6f, 2.6f })
        {
            MakeBlock("ShelfUnitFront", root.transform, new Vector3(2.2f, 2.8f, 0.4f),
                new Vector3(sx, 1.65f, 3.45f), shelfC);
            for (int row = 0; row < 3; row++)
            {
                float by = 0.7f + row * 0.85f;
                MakeBlock("BookShelfBoardFront", root.transform, new Vector3(1.9f, 0.07f, 0.32f),
                    new Vector3(sx, by - 0.29f, 3.2f), shelfC);
                for (int i = 0; i < 5; i++)
                {
                    Color bc = i % 4 == 0 ? bookRed : (i % 4 == 1 ? bookGrn : (i % 4 == 2 ? bookBlu : bookGld));
                    MakeBlock("BookFront", root.transform, new Vector3(0.2f, 0.5f, 0.2f),
                        new Vector3(sx - 0.8f + i * 0.4f, by, 3.2f), bc, true);
                }
            }
        }

        // ── Shelf unit on the east wall (near the entrance) ──
        MakeBlock("ShelfEast", root.transform, new Vector3(0.5f, 2.4f, 1.6f),
            new Vector3(4.55f, 1.45f, 2.8f), shelfC);
        for (int row = 0; row < 2; row++)
        {
            float by = 0.75f + row * 0.9f;
            MakeBlock("BookShelfBoardEast", root.transform, new Vector3(0.32f, 0.07f, 1.0f),
                new Vector3(4.25f, by - 0.29f, 2.6f), shelfC);
            for (int i = 0; i < 3; i++)
            {
                Color bc = i % 3 == 0 ? bookGrn : (i % 3 == 1 ? bookBlu : bookRed);
                MakeBlock("BookEast", root.transform, new Vector3(0.2f, 0.5f, 0.2f),
                    new Vector3(4.25f, by, 2.2f + i * 0.4f), bc, true);
            }
        }

        // ── Librarian desk against the back wall (long side along X) ──
        MakeBlock("Desk", root.transform, new Vector3(2.4f, 1f, 1.6f),
            new Vector3(0f, 0.75f, -2.2f), trimC);
        MakeBlock("DeskTop", root.transform, new Vector3(2.6f, 0.08f, 1.6f),
            new Vector3(0f, 1.29f, -2.2f), floorC, true);
        MakeBlock("OpenBook", root.transform, new Vector3(0.9f, 0.06f, 1.2f),
            new Vector3(0f, 1.38f, -2.2f), new Color(0.95f, 0.93f, 0.85f), true);

        // ── Desk props ──
        MakeBlock("GlobeBase", root.transform, new Vector3(0.16f, 0.05f, 0.16f), new Vector3(-0.85f, 1.38f, -1.65f), trimC, true);
        MakeBlock("GlobeStand", root.transform, new Vector3(0.05f, 0.22f, 0.05f), new Vector3(-0.85f, 1.45f, -1.65f), ridgeC, true);
        MakeBlock("Globe", root.transform, new Vector3(0.2f, 0.2f, 0.2f), new Vector3(-0.85f, 1.65f, -1.65f), bookBlu, true);
        MakeBlock("Papers", root.transform, new Vector3(0.35f, 0.04f, 0.5f), new Vector3(0.85f, 1.39f, -2.75f), new Color(0.95f, 0.93f, 0.85f), true);
        MakeBlock("PaperTop", root.transform, new Vector3(0.3f, 0.04f, 0.45f), new Vector3(0.85f, 1.43f, -2.77f), new Color(0.9f, 0.87f, 0.78f), true);
        MakeBlock("InkPot", root.transform, new Vector3(0.12f, 0.2f, 0.12f), new Vector3(0.95f, 1.4f, -1.75f), ridgeC, true);
        MakeBlock("Quill", root.transform, new Vector3(0.04f, 0.55f, 0.04f), new Vector3(1.02f, 1.44f, -1.67f), trimC, true);
        MakeBlock("DeskCandle", root.transform, new Vector3(0.08f, 0.22f, 0.08f), new Vector3(-0.85f, 1.4f, -2.75f), new Color(0.96f, 0.92f, 0.8f), true);
        MakeBlock("DeskCandleFlame", root.transform, new Vector3(0.06f, 0.09f, 0.06f), new Vector3(-0.85f, 1.56f, -2.75f), bookGld, true);

        // ── Librarian chair behind the desk ──
        MakeBlock("LibrarianChair", root.transform, new Vector3(0.8f, 0.7f, 0.8f),
            new Vector3(0f, 0.6f, -3.38f), shelfC, true);
        MakeBlock("LibrarianChairBack", root.transform, new Vector3(0.1f, 0.6f, 0.8f),
            new Vector3(0f, 1.0f, -3.7f), trimC, true);

        // ── Wall clock on the back wall ──
        MakeBlock("Clock", root.transform, new Vector3(0.06f, 0.5f, 0.5f), new Vector3(0f, 4.15f, -3.97f), new Color(0.95f, 0.93f, 0.85f), true);
        MakeBlock("ClockRim", root.transform, new Vector3(0.07f, 0.55f, 0.55f), new Vector3(0f, 4.15f, -3.96f), trimC, true);

        // ── Window bench along the west wall ──
        MakeBlock("Bench", root.transform, new Vector3(0.45f, 0.5f, 2.6f), new Vector3(-4.6f, 0.5f, 2f), trimC, true);
        MakeBlock("BenchCushion", root.transform, new Vector3(0.47f, 0.12f, 2.62f), new Vector3(-4.6f, 0.81f, 2f), bookRed, true).AddComponent<SittableSeat>();

        // ── Potted plants in the corners ──
        MakeBlock("PlantPotSW", root.transform, new Vector3(0.34f, 0.38f, 0.34f), new Vector3(-4.55f, 0.44f, -3.35f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeavesSW", root.transform, new Vector3(0.62f, 0.5f, 0.62f), new Vector3(-4.55f, 0.85f, -3.35f), bookGrn, true);
        MakeBlock("PlantPotNE", root.transform, new Vector3(0.3f, 0.42f, 0.3f), new Vector3(4.5f, 0.46f, 3.35f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeavesNE", root.transform, new Vector3(0.55f, 0.55f, 0.55f), new Vector3(4.5f, 0.93f, 3.35f), bookGrn, true);

        // ── Lantern hanging from the ridge over the reading table ──
        MakeBlock("LanternChain", root.transform, new Vector3(0.03f, 1.6f, 0.03f), new Vector3(0f, 5.75f, 0f), ridgeC, true);
        MakeBlock("Lantern", root.transform, new Vector3(0.28f, 0.32f, 0.28f), new Vector3(0f, 4.92f, 0f), eaveC, true);
        MakeBlock("LanternGlow", root.transform, new Vector3(0.16f, 0.2f, 0.16f), new Vector3(0f, 4.92f, 0f), bookGld, true);

        // ── Exterior: welcome mat + entrance bushes ──
        MakeBlock("Mat", root.transform, new Vector3(1.4f, 0.06f, 2.2f), new Vector3(6.2f, 0.03f, 0f), bookRed, true);
        MakeBlock("BushL", root.transform, new Vector3(1.0f, 0.65f, 0.8f), new Vector3(6.9f, 0.33f, -2.8f), new Color(0.22f, 0.5f, 0.16f), true);
        MakeBlock("BushR", root.transform, new Vector3(1.0f, 0.65f, 0.8f), new Vector3(6.9f, 0.33f, 2.8f), new Color(0.22f, 0.5f, 0.16f), true);

        // ── Exterior: stepping-stone path toward the road ──
        for (int i = 0; i < 3; i++)
        {
            MakeBlock("PathStone", root.transform, new Vector3(1.0f, 0.12f, 1.4f), new Vector3(7.4f + i * 1.6f, 0.06f, 0f), stoneC, true);
        }

        return root;
    }

    public static GameObject BuildLibrarianNpc(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("LibrarianNPC");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;

        Color robeC   = new Color(0.35f, 0.22f, 0.42f);
        Color robeDark= new Color(0.27f, 0.16f, 0.33f);
        Color skinC   = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color shoeC   = new Color(0.2f, 0.14f, 0.1f);
        Color hairC   = new Color(0.85f, 0.85f, 0.85f);
        Color glassC  = new Color(0.12f, 0.12f, 0.12f);
        Color bookC   = new Color(0.8f, 0.25f, 0.2f);

        MakeBlock("LegL", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(-0.15f, -0.6f, 0f), robeDark, true);
        MakeBlock("LegR", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0.15f, -0.6f, 0f), robeDark, true);
        MakeBlock("ShoeL", root.transform, new Vector3(0.24f, 0.1f, 0.34f), new Vector3(-0.15f, -0.86f, 0f), shoeC, true);
        MakeBlock("ShoeR", root.transform, new Vector3(0.24f, 0.1f, 0.34f), new Vector3(0.15f, -0.86f, 0f), shoeC, true);

        MakeBlock("Robe", root.transform, new Vector3(0.58f, 0.6f, 0.36f), new Vector3(0f, 0f, 0f), robeC, true);
        MakeBlock("Sash", root.transform, new Vector3(0.6f, 0.09f, 0.08f), new Vector3(0f, 0.1f, -0.17f), robeDark, true);
        MakeBlock("Collar", root.transform, new Vector3(0.34f, 0.06f, 0.16f), new Vector3(0f, 0.3f, -0.16f), new Color(0.95f, 0.93f, 0.85f), true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.28f, 0.3f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.32f, 0.1f, 0.32f), new Vector3(0f, 0.67f, 0f), hairC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("GlassesL", root.transform, new Vector3(0.12f, 0.09f, 0.04f), new Vector3(-0.09f, 0.55f, -0.175f), glassC, true);
        MakeBlock("GlassesR", root.transform, new Vector3(0.12f, 0.09f, 0.04f), new Vector3(0.09f, 0.55f, -0.175f), glassC, true);
        MakeBlock("GlassesBridge", root.transform, new Vector3(0.1f, 0.03f, 0.03f), new Vector3(0f, 0.55f, -0.175f), glassC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);

        MakeBlock("ArmL", root.transform, new Vector3(0.15f, 0.45f, 0.15f), new Vector3(-0.37f, 0.1f, 0f), robeC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.15f, 0.45f, 0.15f), new Vector3(0.37f, 0.1f, 0f), robeC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.37f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.37f, -0.14f, 0f), skinC, true);
        MakeBlock("Book", root.transform, new Vector3(0.24f, 0.14f, 0.3f), new Vector3(0.37f, -0.05f, 0.1f), bookC, true);
        MakeBlock("BookPage", root.transform, new Vector3(0.24f, 0.02f, 0.3f), new Vector3(0.37f, -0.04f, 0.1f), new Color(0.95f, 0.93f, 0.85f), true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }
    // ==================== MapBuilderNightClub.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  NIGHT CLUB
    // ═══════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════
    //  NIGHTCLUB  (far north, east of the road)
    // ═══════════════════════════════════════════════════════════════
    public static GameObject BuildNightClub(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("NightClub");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC  = new Color(0.16f, 0.14f, 0.2f);
        Color trimC  = new Color(0.55f, 0.5f, 0.7f);
        Color roofC  = new Color(0.1f, 0.1f, 0.14f);
        Color floorC = new Color(0.3f, 0.28f, 0.34f);
        Color danceC = new Color(0.22f, 0.22f, 0.32f);
        Color barC   = new Color(0.5f, 0.35f, 0.22f);
        Color neonC  = new Color(0.16f, 0.13f, 0.22f);
        Color darkC  = new Color(0.08f, 0.08f, 0.1f);

        float halfW = 11.5f;
        float halfD = 9f;

        // ── Walls (entrance on +Z) ──
        MakeBlock("Wall", root.transform, new Vector3(0.6f, 5f, halfD * 2f), new Vector3(-halfW, 2.5f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.6f, 5f, halfD * 2f), new Vector3(halfW, 2.5f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(halfW * 2f, 5f, 0.6f), new Vector3(0f, 2.5f, -halfD), wallC);
        MakeBlock("Wall", root.transform, new Vector3(9.8f, 5f, 0.6f), new Vector3(-6.6f, 2.5f, halfD), wallC);
        MakeBlock("Wall", root.transform, new Vector3(9.8f, 5f, 0.6f), new Vector3(6.6f, 2.5f, halfD), wallC);
        MakeBlock("DoorLintel", root.transform, new Vector3(3.4f, 1.2f, 0.6f), new Vector3(0f, 4.4f, halfD), wallC);

        // ── Door frame + automatic club door ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.4f, 5f, 0.4f), new Vector3(-1.7f, 2.5f, halfD), trimC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.4f, 5f, 0.4f), new Vector3(1.7f, 2.5f, halfD), trimC, true);
        var doorPivot = new GameObject("ClubDoor");
        doorPivot.transform.SetParent(root.transform);
        doorPivot.transform.localPosition = new Vector3(-1.7f, 2f, halfD);
        var doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorPanel.name = "DoorPanel";
        doorPanel.transform.SetParent(doorPivot.transform);
        doorPanel.transform.localPosition = new Vector3(1.7f, 0f, 0f);
        doorPanel.transform.localScale = new Vector3(3.4f, 3.8f, 0.3f);
        doorPanel.GetComponent<MeshRenderer>().material.color = new Color(0.45f, 0.4f, 0.55f);
        doorPanel.AddComponent<BoxCollider>();

        // ── Floor + foundation ──
        MakeBlock("Floor", root.transform, new Vector3(halfW * 2f, 0.5f, halfD * 2f), Vector3.zero, floorC);
        MakeBlock("Foundation", root.transform, new Vector3(halfW * 2f + 1.5f, 0.4f, halfD * 2f + 1.5f), new Vector3(0f, -0.2f, 0f), new Color(0.35f, 0.34f, 0.32f));

        // ── Flat roof ──
        MakeBlock("Roof", root.transform, new Vector3(halfW * 2f + 1f, 0.5f, halfD * 2f + 1f), new Vector3(0f, 5.2f, 0f), roofC);

        // ── Neon sign (front facade) ──
        MakeBlock("NeonSign", root.transform, new Vector3(6f, 1.4f, 0.3f), new Vector3(0f, 3.3f, halfD + 0.1f), neonC, true);
        var neonLabel = new GameObject("ClubNeonLabel");
        neonLabel.transform.SetParent(root.transform);
        neonLabel.transform.localPosition = new Vector3(0f, 3.3f, halfD + 0.4f);
        neonLabel.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        neonLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var neonTmp = neonLabel.AddComponent<TMPro.TextMeshPro>();
        neonTmp.text = Localization.T("DANCE NIGHT");
        neonTmp.fontSize = 1.4f;
        neonTmp.alignment = TMPro.TextAlignmentOptions.Center;
        neonTmp.color = new Color(1f, 0.3f, 0.9f);
        neonTmp.outlineWidth = 0.15f;
        neonTmp.outlineColor = Color.black;
        neonTmp.rectTransform.sizeDelta = new Vector3(10.8f, 1.8f);

        // ── Disco ball ──
        var discoGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        discoGo.name = "DiscoBall";
        discoGo.transform.SetParent(root.transform);
        discoGo.transform.localPosition = new Vector3(0f, 4.7f, 0f);
        discoGo.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
        var discoMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        discoMat.color = new Color(0.9f, 0.9f, 0.95f);
        discoMat.SetFloat("_Metallic", 0.9f);
        discoMat.SetFloat("_Smoothness", 0.95f);
        discoGo.GetComponent<MeshRenderer>().material = discoMat;
        Object.Destroy(discoGo.GetComponent<Collider>());

        // ── Dance floor with checker tiles ──
        MakeBlock("DanceFloor", root.transform, new Vector3(8f, 0.3f, 8f), new Vector3(0f, 0.4f, 0.5f), danceC);
        for (int tx = -3; tx <= 3; tx += 1)
        {
            for (int tz = -3; tz <= 3; tz += 1)
            {
                MakeBlock("Tile", root.transform, new Vector3(0.95f, 0.04f, 0.95f),
                    new Vector3(tx, 0.62f, tz + 0.5f),
                    (tx + tz) % 4 == 0 ? new Color(0.9f, 0.4f, 0.8f) : new Color(0.3f, 0.8f, 0.9f), true);
            }
        }

        // ── DJ booth (north wall) ──
        MakeBlock("DJDesk", root.transform, new Vector3(4f, 1f, 1.1f), new Vector3(0f, 0.5f, -halfD + 2.5f), barC);
        MakeBlock("DJConsole", root.transform, new Vector3(3.6f, 0.12f, 0.9f), new Vector3(0f, 1.08f, -halfD + 2.5f), darkC, true);
        MakeBlock("Turntable", root.transform, new Vector3(0.8f, 0.06f, 0.7f), new Vector3(-1.1f, 1.15f, -halfD + 2.5f), new Color(0.2f, 0.2f, 0.22f), true);
        MakeBlock("Turntable", root.transform, new Vector3(0.8f, 0.06f, 0.7f), new Vector3(1.1f, 1.15f, -halfD + 2.5f), new Color(0.2f, 0.2f, 0.22f), true);
        MakeBlock("Mixer", root.transform, new Vector3(0.5f, 0.05f, 0.6f), new Vector3(0f, 1.16f, -halfD + 2.5f), new Color(0.4f, 0.2f, 0.3f), true);

        // ── DJ behind the booth ──
        BuildClubDJ(root.transform, new Vector3(0f, 1.0f, -halfD + 1f), Quaternion.Euler(0f, 0f, 0f));

        // ── Speaker stacks (corners) ──
        MakeBlock("Speaker", root.transform, new Vector3(0.9f, 1.8f, 0.8f), new Vector3(-halfW + 1.2f, 0.9f, -halfD + 1.2f), darkC, true);
        MakeBlock("Speaker", root.transform, new Vector3(0.9f, 1.8f, 0.8f), new Vector3(halfW - 1.2f, 0.9f, -halfD + 1.2f), darkC, true);

        // ── Bar along the west wall ──
        MakeBlock("Bar", root.transform, new Vector3(1.2f, 1.1f, 6.5f), new Vector3(-halfW + 1.1f, 0.55f, -1.5f), barC);
        MakeBlock("BarTop", root.transform, new Vector3(1.3f, 0.08f, 6.7f), new Vector3(-halfW + 1.1f, 1.12f, -1.5f), new Color(0.7f, 0.5f, 0.3f), true);
        for (int si = 0; si < 4; si++)
        {
            MakeBlock("BarStool", root.transform, new Vector3(0.35f, 0.6f, 0.35f), new Vector3(-halfW + 2.3f, 0.3f, -3.6f + si * 1.5f), trimC, true).AddComponent<SittableSeat>();
        }
        for (int bi = 0; bi < 5; bi++)
        {
            MakeBlock("Bottle", root.transform, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(-halfW + 0.6f, 1.35f, -3.2f + bi * 0.7f), bi % 2 == 0 ? new Color(0.6f, 0.85f, 0.7f) : new Color(0.9f, 0.5f, 0.3f), true);
        }

        // ── VIP booth along the east wall ──
        MakeBlock("VipSofa", root.transform, new Vector3(0.5f, 0.5f, 2.4f), new Vector3(halfW - 1.1f, 0.25f, -2.5f), new Color(0.4f, 0.2f, 0.5f), true).AddComponent<SittableSeat>();
        MakeBlock("VipTable", root.transform, new Vector3(0.8f, 0.08f, 0.5f), new Vector3(halfW - 1.6f, 0.55f, -2.5f), new Color(0.6f, 0.42f, 0.24f), true);

        // ── Dancers ──
        var dancerPositions = new[]
        {
            new Vector3(-3f, 1.05f, -1.8f),
            new Vector3(0f, 1.05f, -2.2f),
            new Vector3(3f, 1.05f, -1.8f),
            new Vector3(-3f, 1.05f, 1.8f),
            new Vector3(0f, 1.05f, 2.4f),
            new Vector3(3f, 1.05f, 1.8f),
            new Vector3(-1.6f, 1.05f, 0.2f),
            new Vector3(1.6f, 1.05f, 0.2f),
        };
        var shirtColors = new[]
        {
            new Color(0.9f, 0.3f, 0.4f), new Color(0.3f, 0.7f, 0.95f), new Color(0.9f, 0.6f, 0.2f),
            new Color(0.5f, 0.3f, 0.9f), new Color(0.2f, 0.8f, 0.5f), new Color(0.95f, 0.5f, 0.85f),
            new Color(0.3f, 0.9f, 0.8f), new Color(0.9f, 0.8f, 0.3f),
        };
        for (int di = 0; di < dancerPositions.Length; di++)
        {
            var dancer = BuildClubDancer(root.transform, dancerPositions[di], Quaternion.Euler(0f, 180f, 0f), shirtColors[di], new Color(0.15f, 0.15f, 0.18f), new Color(0.9f, 0.78f, 0.68f));
            var comp = dancer.AddComponent<ClubDancer>();
            comp.Phase = UnityEngine.Random.Range(0f, 6.28f);
        }

        // ── Club + disco lights ──
        var discoLightGo = new GameObject("DiscoLight");
        discoLightGo.transform.SetParent(root.transform);
        discoLightGo.transform.localPosition = new Vector3(0f, 4.2f, 0f);
        var discoLight = discoLightGo.AddComponent<Light>();
        discoLight.type = LightType.Point;
        discoLight.color = Color.white;
        discoLight.intensity = 3.5f;
        discoLight.range = 12f;

        var clubLightSpots = new[]
        {
            new Vector3(-8f, 4.7f, -4f), new Vector3(8f, 4.7f, -4f),
            new Vector3(-8f, 4.7f, 4f), new Vector3(8f, 4.7f, 4f),
            new Vector3(0f, 4.7f, -5f), new Vector3(0f, 4.7f, 5f),
        };
        foreach (var lp in clubLightSpots)
        {
            var lg = new GameObject("ClubLight");
            lg.transform.SetParent(root.transform);
            lg.transform.localPosition = lp;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.9f);
            l.intensity = 2.2f;
            l.range = 9f;
        }

        root.AddComponent<NightClubController>();
        return root;
    }

    private static GameObject BuildClubDancer(Transform parent, Vector3 position, Quaternion rotation, Color shirtC, Color pantsC, Color skinC)
    {
        var root = new GameObject("ClubDancer");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        // ── Body container (empty, scale 1,1,1 — no scale inheritance) ──
        var bodyT = new GameObject("Body").transform;
        bodyT.SetParent(root.transform);
        bodyT.localPosition = new Vector3(0f, 0.35f, 0f);
        MakeBlock("BodyMesh", bodyT, new Vector3(0.5f, 0.6f, 0.3f), Vector3.zero, shirtC, true);

        // ── Belt ──
        MakeBlock("Belt", bodyT, new Vector3(0.52f, 0.07f, 0.06f), new Vector3(0f, -0.3f, -0.14f), pantsC, true);

        // ── Head + face (children of Body container) ──
        MakeBlock("Neck", bodyT, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.35f, 0f), skinC, true);
        var headT = new GameObject("Head").transform;
        headT.SetParent(bodyT);
        headT.localPosition = new Vector3(0f, 0.51f, 0f);
        MakeBlock("HeadMesh", headT, new Vector3(0.28f, 0.26f, 0.28f), Vector3.zero, skinC, true);
        // Hair (child of Head so it follows head rotation)
        MakeBlock("Hair", headT, new Vector3(0.29f, 0.1f, 0.29f), new Vector3(0f, 0.16f, 0f), new Color(0.15f, 0.1f, 0.07f), true);
        // Eyes
        MakeBlock("EyeL", headT, new Vector3(0.04f, 0.04f, 0.01f), new Vector3(-0.05f, 0.03f, 0.145f), Color.white, true);
        MakeBlock("EyeR", headT, new Vector3(0.04f, 0.04f, 0.01f), new Vector3(0.05f, 0.03f, 0.145f), Color.white, true);
        MakeBlock("PupilL", headT, new Vector3(0.02f, 0.02f, 0.005f), new Vector3(-0.05f, 0.03f, 0.15f), new Color(0.12f, 0.08f, 0.05f), true);
        MakeBlock("PupilR", headT, new Vector3(0.02f, 0.02f, 0.005f), new Vector3(0.05f, 0.03f, 0.15f), new Color(0.12f, 0.08f, 0.05f), true);
        // Eyebrows
        MakeBlock("BrowL", headT, new Vector3(0.045f, 0.008f, 0.01f), new Vector3(-0.05f, 0.06f, 0.146f), new Color(0.15f, 0.1f, 0.07f), true);
        MakeBlock("BrowR", headT, new Vector3(0.045f, 0.008f, 0.01f), new Vector3(0.05f, 0.06f, 0.146f), new Color(0.15f, 0.1f, 0.07f), true);
        // Mouth
        MakeBlock("Mouth", headT, new Vector3(0.06f, 0.015f, 0.01f), new Vector3(0f, -0.04f, 0.145f), new Color(0.6f, 0.25f, 0.25f), true);

        // ── Hip pivots (children of Body, at bottom of torso) ──
        var hipL = new GameObject("HipL");
        hipL.transform.SetParent(bodyT);
        hipL.transform.localPosition = new Vector3(-0.13f, -0.3f, 0f);
        MakeBlock("LegL", hipL.transform, new Vector3(0.16f, 0.5f, 0.16f), new Vector3(0f, -0.25f, 0f), pantsC, true);

        var hipR = new GameObject("HipR");
        hipR.transform.SetParent(bodyT);
        hipR.transform.localPosition = new Vector3(0.13f, -0.3f, 0f);
        MakeBlock("LegR", hipR.transform, new Vector3(0.16f, 0.5f, 0.16f), new Vector3(0f, -0.25f, 0f), pantsC, true);

        // ── Shoulder pivots (children of Body) ──
        var shoulderL = new GameObject("ShoulderL");
        shoulderL.transform.SetParent(bodyT);
        shoulderL.transform.localPosition = new Vector3(-0.38f, 0.25f, 0f);
        MakeBlock("ArmL", shoulderL.transform, new Vector3(0.13f, 0.5f, 0.13f), new Vector3(0f, -0.25f, 0f), shirtC, true);
        MakeBlock("HandL", shoulderL.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(0f, -0.53f, 0f), skinC, true);

        var shoulderR = new GameObject("ShoulderR");
        shoulderR.transform.SetParent(bodyT);
        shoulderR.transform.localPosition = new Vector3(0.38f, 0.25f, 0f);
        MakeBlock("ArmR", shoulderR.transform, new Vector3(0.13f, 0.5f, 0.13f), new Vector3(0f, -0.25f, 0f), shirtC, true);
        MakeBlock("HandR", shoulderR.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(0f, -0.53f, 0f), skinC, true);

        return root;
    }

    private static GameObject BuildClubDJ(Transform parent, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("ClubDJ");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        Color shirtC = new Color(0.1f, 0.1f, 0.12f);
        Color pantsC = new Color(0.12f, 0.12f, 0.15f);
        Color skinC = new Color(0.9f, 0.78f, 0.68f);
        Color headphoneC = new Color(0.3f, 0.3f, 0.35f);

        // ── Body container (empty, scale 1,1,1 — no scale inheritance) ──
        var bodyT = new GameObject("Body").transform;
        bodyT.SetParent(root.transform);
        bodyT.localPosition = new Vector3(0f, 0.35f, 0f);
        MakeBlock("BodyMesh", bodyT, new Vector3(0.5f, 0.6f, 0.3f), Vector3.zero, shirtC, true);

        // ── Belt ──
        MakeBlock("Belt", bodyT, new Vector3(0.52f, 0.07f, 0.06f), new Vector3(0f, -0.3f, -0.14f), pantsC, true);

        // ── Head (empty container + HeadMesh) ──
        MakeBlock("Neck", bodyT, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.35f, 0f), skinC, true);
        var headT = new GameObject("Head").transform;
        headT.SetParent(bodyT);
        headT.localPosition = new Vector3(0f, 0.51f, 0f);
        MakeBlock("HeadMesh", headT, new Vector3(0.28f, 0.26f, 0.28f), Vector3.zero, skinC, true);
        MakeBlock("Hair", headT, new Vector3(0.29f, 0.1f, 0.29f), new Vector3(0f, 0.16f, 0f), new Color(0.05f, 0.05f, 0.05f), true);

        // Headphones (follow head)
        MakeBlock("HeadphoneL", headT, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(-0.18f, 0.05f, 0f), headphoneC, true);
        MakeBlock("HeadphoneR", headT, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0.18f, 0.05f, 0f), headphoneC, true);
        MakeBlock("HeadphoneBand", headT, new Vector3(0.32f, 0.04f, 0.04f), new Vector3(0f, 0.14f, 0f), headphoneC, true);

        // Sunglasses (follow head, in front of the eyes)
        MakeBlock("GlassL", headT, new Vector3(0.09f, 0.05f, 0.03f), new Vector3(-0.08f, 0.03f, 0.175f), new Color(0.05f, 0.05f, 0.1f), true);
        MakeBlock("GlassR", headT, new Vector3(0.09f, 0.05f, 0.03f), new Vector3(0.08f, 0.03f, 0.175f), new Color(0.05f, 0.05f, 0.1f), true);
        MakeBlock("GlassBridge", headT, new Vector3(0.05f, 0.025f, 0.025f), new Vector3(0f, 0.03f, 0.175f), new Color(0.05f, 0.05f, 0.1f), true);

        // Eyes + brows (positioned behind the glasses)
        MakeBlock("EyeL", headT, new Vector3(0.04f, 0.04f, 0.01f), new Vector3(-0.05f, 0.03f, 0.155f), Color.white, true);
        MakeBlock("EyeR", headT, new Vector3(0.04f, 0.04f, 0.01f), new Vector3(0.05f, 0.03f, 0.155f), Color.white, true);
        MakeBlock("BrowL", headT, new Vector3(0.045f, 0.008f, 0.01f), new Vector3(-0.05f, 0.06f, 0.156f), new Color(0.05f, 0.05f, 0.05f), true);
        MakeBlock("BrowR", headT, new Vector3(0.045f, 0.008f, 0.01f), new Vector3(0.05f, 0.06f, 0.156f), new Color(0.05f, 0.05f, 0.05f), true);
        MakeBlock("Mouth", headT, new Vector3(0.06f, 0.015f, 0.01f), new Vector3(0f, -0.04f, 0.155f), new Color(0.6f, 0.25f, 0.25f), true);

        // ── Hip pivots (children of Body) ──
        var hipL = new GameObject("HipL");
        hipL.transform.SetParent(bodyT);
        hipL.transform.localPosition = new Vector3(-0.13f, -0.3f, 0f);
        MakeBlock("LegL", hipL.transform, new Vector3(0.16f, 0.5f, 0.16f), new Vector3(0f, -0.25f, 0f), pantsC, true);

        var hipR = new GameObject("HipR");
        hipR.transform.SetParent(bodyT);
        hipR.transform.localPosition = new Vector3(0.13f, -0.3f, 0f);
        MakeBlock("LegR", hipR.transform, new Vector3(0.16f, 0.5f, 0.16f), new Vector3(0f, -0.25f, 0f), pantsC, true);

        // ── Shoulder pivots (children of Body) ──
        var shoulderL = new GameObject("ShoulderL");
        shoulderL.transform.SetParent(bodyT);
        shoulderL.transform.localPosition = new Vector3(-0.38f, 0.25f, 0f);
        MakeBlock("ArmL", shoulderL.transform, new Vector3(0.13f, 0.5f, 0.13f), new Vector3(0f, -0.25f, 0f), shirtC, true);
        MakeBlock("HandL", shoulderL.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(0f, -0.53f, 0f), skinC, true);

        var shoulderR = new GameObject("ShoulderR");
        shoulderR.transform.SetParent(bodyT);
        shoulderR.transform.localPosition = new Vector3(0.38f, 0.25f, 0f);
        MakeBlock("ArmR", shoulderR.transform, new Vector3(0.13f, 0.5f, 0.13f), new Vector3(0f, -0.25f, 0f), shirtC, true);
        MakeBlock("HandR", shoulderR.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(0f, -0.53f, 0f), skinC, true);

        // ── Add animator ──
        root.AddComponent<ClubDJAnimator>();

        return root;
    }
    // ==================== MapBuilderPoliceStation.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  POLICE STATION
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildPoliceStation(Transform parent, Vector3 position = default, Quaternion rotation = default, float scale = 1f)
    {
        var root = new GameObject("PoliceStation");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC = new Color(0.88f, 0.84f, 0.74f);
        Color trimC = new Color(0.55f, 0.4f, 0.28f);
        Color roofC = new Color(0.2f, 0.22f, 0.3f);
        Color darkC = new Color(0.16f, 0.18f, 0.24f);
        Color doorC = new Color(0.22f, 0.16f, 0.12f);
        Color winC = new Color(0.72f, 0.85f, 0.95f);
        Color signC = new Color(0.12f, 0.28f, 0.62f);
        Color whiteC = Color.white;
        Color concreteC = new Color(0.55f, 0.55f, 0.56f);
        Color flagRed = new Color(0.85f, 0.12f, 0.12f);
        Color flagYellow = new Color(0.95f, 0.85f, 0.15f);
        Color lampC = new Color(1f, 0.9f, 0.55f);
        Color bushC = new Color(0.2f, 0.5f, 0.2f);
        Color goldC = new Color(0.92f, 0.78f, 0.3f);
        Color blueC = new Color(0.2f, 0.35f, 0.85f);

        // ── Foundation ──
        MakeBlock("StationPlinth", root.transform, new Vector3(9.4f, 0.3f, 6.9f), new Vector3(0f, 0.15f, 0f), concreteC);
        MakeBlock("StationFloor", root.transform, new Vector3(9f, 0.15f, 6.5f), new Vector3(0f, 0.3f, 0f), concreteC);

        // ── Walls (3.4m tall, front faces +Z) ──
        MakeBlock("StationBackWall", root.transform, new Vector3(9f, 3.4f, 0.3f), new Vector3(0f, 2f, -3.25f), wallC);
        MakeBlock("StationLeftWall", root.transform, new Vector3(0.3f, 3.4f, 6.5f), new Vector3(-4.5f, 2f, 0f), wallC);
        MakeBlock("StationRightWall", root.transform, new Vector3(0.3f, 3.4f, 6.5f), new Vector3(4.5f, 2f, 0f), wallC);
        MakeBlock("StationFrontWallL", root.transform, new Vector3(3.55f, 3.4f, 0.3f), new Vector3(-2.725f, 2f, 3.25f), wallC);
        MakeBlock("StationFrontWallR", root.transform, new Vector3(3.55f, 3.4f, 0.3f), new Vector3(2.725f, 2f, 3.25f), wallC);

        // ── Wall-top trim band all around ──
        MakeBlock("StationTrimFront", root.transform, new Vector3(9.1f, 0.3f, 0.45f), new Vector3(0f, 3.35f, 3.32f), trimC, true);
        MakeBlock("StationTrimBack", root.transform, new Vector3(9.1f, 0.3f, 0.45f), new Vector3(0f, 3.35f, -3.32f), trimC, true);
        MakeBlock("StationTrimLeft", root.transform, new Vector3(0.45f, 0.3f, 6.6f), new Vector3(-4.52f, 3.35f, 0f), trimC, true);
        MakeBlock("StationTrimRight", root.transform, new Vector3(0.45f, 0.3f, 6.6f), new Vector3(4.52f, 3.35f, 0f), trimC, true);

        // ── Entrance door + frame + head ──
        MakeBlock("StationDoor", root.transform, new Vector3(1.8f, 2.6f, 0.22f), new Vector3(0f, 1.6f, 3.42f), doorC);
        MakeBlock("StationDoorFrameL", root.transform, new Vector3(0.15f, 2.8f, 0.24f), new Vector3(-0.95f, 1.6f, 3.4f), trimC, true);
        MakeBlock("StationDoorFrameR", root.transform, new Vector3(0.15f, 2.8f, 0.24f), new Vector3(0.95f, 1.6f, 3.4f), trimC, true);
        MakeBlock("StationDoorHead", root.transform, new Vector3(2.3f, 0.2f, 0.26f), new Vector3(0f, 3.05f, 3.4f), trimC, true);
        MakeBlock("StationDoorKnob", root.transform, new Vector3(0.12f, 0.12f, 0.14f), new Vector3(0.55f, 1.5f, 3.5f), goldC, true);

        // ── Stoop + steps ──
        MakeBlock("StationStoop", root.transform, new Vector3(3.4f, 0.24f, 1f), new Vector3(0f, 0.12f, 3.75f), concreteC);
        MakeBlock("StationStep", root.transform, new Vector3(2.4f, 0.16f, 0.55f), new Vector3(0f, 0.08f, 4.4f), concreteC);

        // ── Front windows (with trim + crossbars) ──
        foreach (float wx in new[] { -2.9f, 2.9f })
        {
            string side = wx < 0f ? "L" : "R";
            AddWindowTrim(root.transform, new Vector3(wx, 2f, 3.25f), 1.4f, 1.4f, Vector3.forward, trimC, concreteC, "F_" + side);
            MakeBlock("StationWinGlass" + side, root.transform, new Vector3(1.3f, 1.3f, 0.12f), new Vector3(wx, 2f, 3.42f), winC, true);
            MakeBlock("StationWinBarH" + side, root.transform, new Vector3(1.36f, 0.1f, 0.14f), new Vector3(wx, 2f, 3.44f), trimC, true);
            MakeBlock("StationWinBarV" + side, root.transform, new Vector3(0.1f, 1.36f, 0.14f), new Vector3(wx, 2f, 3.44f), trimC, true);
        }

        // ── Side windows (left/right walls) ──
        foreach (float sz in new[] { -1.6f, 1.6f })
        {
            string tag = sz < 0f ? "A" : "B";
            MakeBlock("StationSillL" + tag, root.transform, new Vector3(0.42f, 0.12f, 1.7f), new Vector3(-4.52f, 1.28f, sz), concreteC, true);
            MakeBlock("StationHeadL" + tag, root.transform, new Vector3(0.42f, 0.16f, 1.7f), new Vector3(-4.52f, 2.74f, sz), concreteC, true);
            MakeBlock("StationWinGlassL" + tag, root.transform, new Vector3(0.12f, 1.3f, 1.3f), new Vector3(-4.58f, 2f, sz), winC, true);
            MakeBlock("StationWinBarL" + tag, root.transform, new Vector3(0.14f, 0.1f, 1.36f), new Vector3(-4.58f, 2f, sz), trimC, true);

            MakeBlock("StationSillR" + tag, root.transform, new Vector3(0.42f, 0.12f, 1.7f), new Vector3(4.52f, 1.28f, sz), concreteC, true);
            MakeBlock("StationHeadR" + tag, root.transform, new Vector3(0.42f, 0.16f, 1.7f), new Vector3(4.52f, 2.74f, sz), concreteC, true);
            MakeBlock("StationWinGlassR" + tag, root.transform, new Vector3(0.12f, 1.3f, 1.3f), new Vector3(4.58f, 2f, sz), winC, true);
            MakeBlock("StationWinBarR" + tag, root.transform, new Vector3(0.14f, 0.1f, 1.36f), new Vector3(4.58f, 2f, sz), trimC, true);
        }

        // ── Sign band + POLICE label ──
        MakeBlock("StationSign", root.transform, new Vector3(6f, 0.8f, 0.26f), new Vector3(0f, 3.3f, 3.46f), signC, true);
        MakeBlock("StationSignBand", root.transform, new Vector3(6f, 0.16f, 0.27f), new Vector3(0f, 3.02f, 3.47f), whiteC, true);
        MakeBlock("StationSignBadge", root.transform, new Vector3(0.28f, 0.28f, 0.27f), new Vector3(0f, 3.44f, 3.47f), whiteC, true);

        var signLabel = new GameObject("PoliceSignLabel");
        signLabel.transform.SetParent(root.transform);
        signLabel.transform.localPosition = new Vector3(0f, 3.32f, 3.74f);
        signLabel.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        signLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var tmp = signLabel.AddComponent<TMPro.TextMeshPro>();
        tmp.text = Localization.T("CẢNH SÁT");
        tmp.fontSize = 2.2f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = Color.black;
        tmp.rectTransform.sizeDelta = new Vector3(11.2f, 1.6f);

        // ── Roof + parapet + raised cap ──
        MakeBlock("StationRoof", root.transform, new Vector3(9.6f, 0.25f, 7.1f), new Vector3(0f, 3.85f, 0f), roofC);
        MakeBlock("StationParapetFront", root.transform, new Vector3(9.6f, 0.6f, 0.2f), new Vector3(0f, 4.2f, 3.55f), roofC);
        MakeBlock("StationParapetBack", root.transform, new Vector3(9.6f, 0.6f, 0.2f), new Vector3(0f, 4.2f, -3.55f), roofC);
        MakeBlock("StationParapetLeft", root.transform, new Vector3(0.2f, 0.6f, 7.1f), new Vector3(-4.8f, 4.2f, 0f), roofC);
        MakeBlock("StationParapetRight", root.transform, new Vector3(0.2f, 0.6f, 7.1f), new Vector3(4.8f, 4.2f, 0f), roofC);
        MakeBlock("StationRoofCap", root.transform, new Vector3(5.2f, 0.35f, 3.6f), new Vector3(0f, 4.6f, 0f), darkC);
        MakeBlock("StationRoofCapTop", root.transform, new Vector3(3.6f, 0.15f, 2.6f), new Vector3(0f, 5.05f, 0f), roofC);

        // ── Corner watchtower with blue light ──
        MakeBlock("StationTower", root.transform, new Vector3(1.6f, 1.5f, 1.6f), new Vector3(-3.2f, 4.7f, 1.2f), wallC);
        MakeBlock("StationTowerWinFront", root.transform, new Vector3(0.14f, 0.5f, 0.5f), new Vector3(-3.2f, 5.05f, 1.98f), winC, true);
        MakeBlock("StationTowerWinSide", root.transform, new Vector3(0.5f, 0.5f, 0.14f), new Vector3(-3.98f, 5.05f, 1.2f), winC, true);
        MakeBlock("StationTowerCap", root.transform, new Vector3(1.8f, 0.18f, 1.8f), new Vector3(-3.2f, 5.62f, 1.2f), roofC);
        MakeBlock("StationTowerLightMount", root.transform, new Vector3(0.1f, 0.16f, 0.1f), new Vector3(-3.2f, 5.75f, 1.2f), darkC, true);
        MakeBlock("StationTowerLight", root.transform, new Vector3(0.34f, 0.2f, 0.34f), new Vector3(-3.2f, 5.85f, 1.2f), blueC, true);

        // ── Flag pole (front-right) ──
        MakeBlock("StationFlagPole", root.transform, new Vector3(0.1f, 3.4f, 0.1f), new Vector3(4.2f, 1.7f, 2.7f), new Color(0.55f, 0.55f, 0.58f), true);
        MakeBlock("StationFlag", root.transform, new Vector3(1f, 0.65f, 0.06f), new Vector3(4.2f, 3.3f, 2.15f), flagRed, true);
        MakeBlock("StationFlagStar", root.transform, new Vector3(0.2f, 0.2f, 0.07f), new Vector3(4.2f, 3.3f, 2.15f), flagYellow, true);

        // ── Lampposts flanking the stoop ──
        foreach (int side in new[] { -1, 1 })
        {
            string tag = side < 0 ? "L" : "R";
            MakeBlock("StationLampPost" + tag, root.transform, new Vector3(0.12f, 2.6f, 0.12f), new Vector3(side * 1.7f, 1.3f, 4.75f), darkC, true);
            MakeBlock("StationLampHead" + tag, root.transform, new Vector3(0.45f, 0.3f, 0.45f), new Vector3(side * 1.7f, 2.9f, 4.75f), lampC, true);
        }

        // ── Planters + bushes along the front ──
        foreach (int side in new[] { -1, 1 })
        {
            string tag = side < 0 ? "L" : "R";
            MakeBlock("StationPlanter" + tag, root.transform, new Vector3(1.7f, 0.4f, 0.7f), new Vector3(side * 3.7f, 0.2f, 3.7f), trimC, true);
            MakeBlock("StationBush" + tag, root.transform, new Vector3(1.5f, 0.55f, 0.6f), new Vector3(side * 3.7f, 0.55f, 3.7f), bushC, true);
            MakeBlock("StationBushEnt" + tag, root.transform, new Vector3(1f, 0.7f, 0.9f), new Vector3(side * 1.9f, 0.35f, 5.2f), bushC, true);
        }

        return root;
    }
    // ==================== MapBuilderRestaurant.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  RICE RESTAURANT  (12 x 10, gabled roof, counter, chef NPC)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildRiceRestaurant(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("RiceRestaurant");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC   = new Color(0.93f, 0.87f, 0.72f);
        Color trimC   = new Color(0.42f, 0.27f, 0.14f);
        Color roofC   = new Color(0.78f, 0.3f, 0.12f);
        Color ridgeC  = new Color(0.55f, 0.16f, 0.06f);
        Color eaveC   = new Color(0.2f, 0.18f, 0.16f);
        Color floorC  = new Color(0.45f, 0.32f, 0.2f);
        Color stoneC  = new Color(0.439f, 0.4f, 0.361f);
        Color winC    = new Color(0.549f, 0.784f, 0.863f);
        Color signC   = new Color(0.886f, 0.753f, 0.098f);
        Color awningC = new Color(0.85f, 0.25f, 0.2f);
        Color counterC = new Color(0.584f, 0.294f, 0.165f);
        Color riceC   = new Color(0.98f, 0.95f, 0.85f);

        float halfW = 6f;
        float depth = 10f;

        // ── Walls (door on +X) ──
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, depth), new Vector3(-halfW, 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(halfW * 2f, 4f, 0.5f), new Vector3(0f, 2f, -depth / 2f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(halfW * 2f, 4f, 0.5f), new Vector3(0f, 2f, depth / 2f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(halfW, 2f, -3.25f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(halfW, 2f, 3.25f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1.2f, 3f), new Vector3(halfW, 3.4f, 0f), wallC);
        MakeBlock("Floor", root.transform, new Vector3(halfW * 2f, 0.5f, depth), Vector3.zero, floorC);

        // ── Gabled roof ──
        float rise = 2.5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.2f;
        float roofZ = depth + overhang * 2f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(-halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.55f, 0.3f, roofZ + 0.2f),
            new Vector3(0f, 4f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(halfW, 4.1f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(-halfW, 4.1f, 0f), eaveC);

        foreach (float gz in new[] { -depth / 2f, depth / 2f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float sw = (halfW * 2f) * (1f - t) + 0.2f;
                float sy = 4f + (i + 0.5f) * rise / 5f;
                float sh = rise / 5f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(halfW * 2f + 1.5f, 0.4f, depth + 1.5f),
            new Vector3(0f, -0.25f, 0f), stoneC);

        // ── Sign (faces the +X road) ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3.5f),
            new Vector3(halfW + 0.08f, 3.6f, 0f), signC, true);
        var signLabel = new GameObject("RestaurantSignLabel");
        signLabel.transform.SetParent(root.transform);
        signLabel.transform.localPosition = new Vector3(halfW + 0.3f, 3.6f, 0f);
        signLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        signLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var signTmp = signLabel.AddComponent<TMPro.TextMeshPro>();
        signTmp.text = Localization.T("NHÀ HÀNG");
        signTmp.fontSize = 2.0f;
        signTmp.alignment = TMPro.TextAlignmentOptions.Center;
        signTmp.color = new Color(0.98f, 0.94f, 0.85f);
        signTmp.outlineWidth = 0.18f;
        signTmp.outlineColor = Color.black;
        signTmp.rectTransform.sizeDelta = new Vector3(6.4f, 1.5f);

        // ── Roof trim + ridge finials ──
        MakeBlock("RoofTrimN", root.transform, new Vector3(halfW * 2f + 0.2f, 0.12f, 0.55f),
            new Vector3(0f, 4.18f, -depth / 2f - 1.1f), trimC, true);
        MakeBlock("RoofTrimS", root.transform, new Vector3(halfW * 2f + 0.2f, 0.12f, 0.55f),
            new Vector3(0f, 4.18f, depth / 2f + 1.1f), trimC, true);
        MakeBlock("FinialN", root.transform, new Vector3(0.5f, 0.7f, 0.5f),
            new Vector3(0f, 4f + rise + 0.35f, -roofZ / 2f + 0.2f), ridgeC, true);
        MakeBlock("FinialS", root.transform, new Vector3(0.5f, 0.7f, 0.5f),
            new Vector3(0f, 4f + rise + 0.35f, roofZ / 2f - 0.2f), ridgeC, true);

        // ── Entrance awning (east) ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.15f, 4f),
            new Vector3(halfW + 0.8f, 3.8f, 0f), awningC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(halfW + 1.6f, 1.9f, -1.6f), trimC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(halfW + 1.6f, 1.9f, 1.6f), trimC, true);

        // ── Hanging lanterns ──
        foreach (float lz in new[] { -1.6f, 1.6f })
        {
            MakeBlock("LanternCord", root.transform, new Vector3(0.04f, 0.5f, 0.04f),
                new Vector3(halfW + 0.8f, 3.55f, lz), trimC, true);
            MakeBlock("Lantern", root.transform, new Vector3(0.28f, 0.4f, 0.28f),
                new Vector3(halfW + 0.8f, 3.1f, lz), new Color(0.9f, 0.35f, 0.2f), true);
            MakeBlock("LanternGlow", root.transform, new Vector3(0.18f, 0.22f, 0.18f),
                new Vector3(halfW + 0.8f, 3.1f, lz), new Color(1f, 0.85f, 0.45f), true);
            AddEntranceLight(root.transform, new Vector3(halfW + 0.8f, 3.0f, lz));
        }

        // ── Freestanding signpost (front, beside the path) ──
        MakeBlock("SignPost", root.transform, new Vector3(0.18f, 2.6f, 0.18f),
            new Vector3(halfW + 2.2f, 1.3f, 2.6f), trimC, true);
        MakeBlock("SignBoard", root.transform, new Vector3(0.14f, 0.9f, 2.4f),
            new Vector3(halfW + 2.2f, 2.6f, 2.6f), signC, true);
        MakeBlock("SignBoardText", root.transform, new Vector3(0.05f, 0.55f, 2.0f),
            new Vector3(halfW + 2.26f, 2.6f, 2.6f), new Color(0.98f, 0.94f, 0.85f), true);

        // ── Windows (west, north, south) ──
        foreach (float wz in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.2f, 1.2f),
                new Vector3(-halfW - 0.03f, 2.2f, wz), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.08f, 1.2f),
                new Vector3(-halfW - 0.03f, 2.2f, wz), trimC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.2f, 0.08f),
                new Vector3(-halfW - 0.03f, 2.2f, wz), trimC, true);
        }
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.2f, 0.14f),
                new Vector3(wx, 2.2f, depth / 2f + 0.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.2f, 0.16f),
                new Vector3(wx, 2.2f, depth / 2f + 0.03f), trimC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.08f, 0.16f),
                new Vector3(wx, 2.2f, depth / 2f + 0.03f), trimC, true);
        }

        // ── Door frame (east) ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(halfW + 0.04f, 1.75f, -1.5f), trimC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(halfW + 0.04f, 1.75f, 1.5f), trimC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(halfW + 0.04f, 3.65f, 0f), trimC, true);

        // ── Menu board (interior, beside door) ──
        MakeBlock("MenuBoardFrame", root.transform, new Vector3(0.14f, 1.4f, 1.2f),
            new Vector3(halfW - 0.05f, 1.9f, 2.6f), trimC, true);
        MakeBlock("MenuBoard", root.transform, new Vector3(0.12f, 1.3f, 1.1f),
            new Vector3(halfW - 0.05f, 1.9f, 2.6f), new Color(0.12f, 0.1f, 0.08f), true);
        for (int mi = 0; mi < 3; mi++)
        {
            MakeBlock("MenuChip", root.transform, new Vector3(0.06f, 0.12f, 0.8f),
                new Vector3(halfW - 0.08f, 1.85f - mi * 0.4f, 2.6f),
                mi == 1 ? new Color(1f, 0.6f, 0.35f) : new Color(0.98f, 0.95f, 0.85f), true);
        }

        // ── Counter with rice pots (west wall) ──
        MakeBlock("Counter", root.transform, new Vector3(2f, 1f, 6f),
            new Vector3(-halfW + 1f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(2f, 0.08f, 6.2f),
            new Vector3(-halfW + 1f, 0.96f, 0f), new Color(0.757f, 0.62f, 0.404f), true);
        for (int i = -2; i <= 2; i++)
        {
            MakeBlock("RicePot", root.transform, new Vector3(0.32f, 0.5f, 0.32f),
                new Vector3(-halfW + 0.45f, 1.3f, i * 1.2f), new Color(0.35f, 0.35f, 0.36f), true);
            MakeBlock("RicePotTop", root.transform, new Vector3(0.36f, 0.08f, 0.36f),
                new Vector3(-halfW + 0.45f, 1.58f, i * 1.2f), riceC, true);
        }

        // ── Kitchen detail: stir pot, chopping board, stacked bowls (on countertop) ──
        MakeBlock("StirPot", root.transform, new Vector3(0.28f, 0.32f, 0.28f),
            new Vector3(-5.15f, 1.16f, 1.6f), new Color(0.35f, 0.35f, 0.36f), true);
        MakeBlock("StirPotTop", root.transform, new Vector3(0.32f, 0.07f, 0.32f),
            new Vector3(-5.15f, 1.34f, 1.6f), new Color(0.5f, 0.35f, 0.15f), true);
        MakeBlock("ChoppingBoard", root.transform, new Vector3(0.14f, 0.05f, 0.7f),
            new Vector3(-5.25f, 1.03f, 2.55f), new Color(0.72f, 0.55f, 0.3f), true);
        MakeBlock("ChoppedItem", root.transform, new Vector3(0.05f, 0.04f, 0.05f),
            new Vector3(-5.25f, 1.09f, 2.35f), new Color(1f, 0.85f, 0.3f), true);
        MakeBlock("StackedBowl", root.transform, new Vector3(0.32f, 0.14f, 0.32f),
            new Vector3(-5.15f, 1.07f, -2.45f), riceC, true);
        MakeBlock("StackedBowl", root.transform, new Vector3(0.34f, 0.1f, 0.34f),
            new Vector3(-5.15f, 1.21f, -2.45f), riceC, true);
        MakeBlock("SmallPot", root.transform, new Vector3(0.22f, 0.24f, 0.22f),
            new Vector3(-5.1f, 1.12f, 0.4f), new Color(0.3f, 0.3f, 0.32f), true);
        MakeBlock("Ladle", root.transform, new Vector3(0.05f, 0.3f, 0.05f),
            new Vector3(-5.3f, 1.15f, -1.15f), new Color(0.55f, 0.55f, 0.58f), true);

        // ── Stove on the countertop ──
        MakeBlock("Stove", root.transform, new Vector3(0.9f, 0.45f, 0.9f),
            new Vector3(-5.2f, 1.2f, -0.6f), new Color(0.28f, 0.28f, 0.3f), true);
        MakeBlock("StoveTop", root.transform, new Vector3(0.9f, 0.05f, 0.9f),
            new Vector3(-5.2f, 1.45f, -0.6f), new Color(0.12f, 0.12f, 0.13f), true);
        MakeBlock("Burner", root.transform, new Vector3(0.34f, 0.03f, 0.34f),
            new Vector3(-5.2f, 1.48f, -0.85f), new Color(0.08f, 0.08f, 0.08f), true);
        MakeBlock("Burner", root.transform, new Vector3(0.34f, 0.03f, 0.34f),
            new Vector3(-5.2f, 1.48f, -0.35f), new Color(0.08f, 0.08f, 0.08f), true);

        // ── Wall shelf + jars behind the counter ──
        MakeBlock("ShelfBoard", root.transform, new Vector3(0.12f, 0.08f, 4.2f),
            new Vector3(-5.78f, 2.6f, 0f), trimC, true);
        for (int j = 0; j < 4; j++)
        {
            MakeBlock("Jar", root.transform, new Vector3(0.16f, 0.3f, 0.16f),
                new Vector3(-5.82f, 2.78f, -1.6f + j * 1.1f),
                j % 2 == 0 ? new Color(0.55f, 0.78f, 0.86f) : new Color(0.9f, 0.35f, 0.2f), true);
        }

        // ── Rice sacks in the corner ──
        MakeBlock("RiceSack", root.transform, new Vector3(0.5f, 0.7f, 0.5f),
            new Vector3(-5.7f, 0.35f, -3.5f), new Color(0.93f, 0.9f, 0.82f), true);
        MakeBlock("RiceSack", root.transform, new Vector3(0.45f, 0.6f, 0.45f),
            new Vector3(-5.35f, 0.3f, -3.6f), new Color(0.86f, 0.81f, 0.72f), true);

        // ── Interior dining tables (full height) ──
        foreach (float tz in new[] { -3.5f, 3.5f })
        {
            MakeBlock("DiningTable", root.transform, new Vector3(1.6f, 0.1f, 0.9f),
                new Vector3(0.5f, 0.75f, tz), new Color(0.6f, 0.42f, 0.24f), true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.1f, 0.35f, tz - 0.35f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.9f, 0.35f, tz - 0.35f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.1f, 0.35f, tz + 0.35f), trimC, true);
            MakeBlock("TableLeg", root.transform, new Vector3(0.1f, 0.7f, 0.1f),
                new Vector3(0.9f, 0.35f, tz + 0.35f), trimC, true);
            MakeBlock("Bowl", root.transform, new Vector3(0.3f, 0.08f, 0.3f),
                new Vector3(0.5f, 0.82f, tz - 0.15f), riceC, true);
            MakeBlock("Plate", root.transform, new Vector3(0.28f, 0.04f, 0.28f),
                new Vector3(0.5f, 0.81f, tz + 0.2f), new Color(0.95f, 0.93f, 0.88f), true);
        }

        // ── Interior benches + stools ──
        foreach (float bz in new[] { -3.5f, 3.5f })
        {
            MakeBlock("DiningBench", root.transform, new Vector3(0.4f, 0.12f, 1.4f),
                new Vector3(1.55f, 0.44f, bz), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.55f, 0.2f, bz - 0.55f), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.55f, 0.2f, bz + 0.55f), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.35f, 0.2f, bz - 0.55f), trimC, true);
            MakeBlock("BenchLeg", root.transform, new Vector3(0.12f, 0.4f, 0.12f),
                new Vector3(1.75f, 0.2f, bz + 0.55f), trimC, true);
            MakeBlock("DiningStool", root.transform, new Vector3(0.3f, 0.5f, 0.3f),
                new Vector3(-0.55f, 0.25f, bz), trimC, true);
        }

        // ── Interior seated diners (one per stool + one per bench) ──
        BuildSeatedCustomer(root.transform, new Vector3(-0.55f, 0.75f, -3.5f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.45f, 0.6f, 0.75f), new Color(0.35f, 0.4f, 0.5f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildSeatedCustomer(root.transform, new Vector3(1.55f, 0.75f, -3.5f),
            Quaternion.Euler(0f, 90f, 0f), new Color(0.85f, 0.55f, 0.4f), new Color(0.4f, 0.32f, 0.25f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildSeatedCustomer(root.transform, new Vector3(-0.55f, 0.75f, 3.5f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.5f, 0.65f, 0.4f), new Color(0.35f, 0.45f, 0.3f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildSeatedCustomer(root.transform, new Vector3(1.55f, 0.75f, 3.5f),
            Quaternion.Euler(0f, 90f, 0f), new Color(0.75f, 0.55f, 0.85f), new Color(0.35f, 0.3f, 0.45f),
            new Color(0.9f, 0.78f, 0.68f));

        // ── Kitchen staff: cook helper + waitress ──
        BuildStandingStaff(root.transform, new Vector3(-3.6f, 1.13f, -2.2f),
            Quaternion.Euler(0f, 90f, 0f), new Color(0.95f, 0.95f, 0.92f), new Color(0.2f, 0.2f, 0.2f),
            new Color(0.9f, 0.78f, 0.68f));
        BuildStandingStaff(root.transform, new Vector3(4.5f, 1.13f, 1.5f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.9f, 0.55f, 0.35f), new Color(0.3f, 0.35f, 0.55f),
            new Color(0.9f, 0.78f, 0.68f));

        // ── Hanging lamps over the dining tables (with Point Lights) ──
        foreach (float lz in new[] { -3.5f, 3.5f })
        {
            MakeBlock("LampCord", root.transform, new Vector3(0.04f, 0.5f, 0.04f),
                new Vector3(0.5f, 2.95f, lz), trimC, true);
            MakeBlock("Lamp", root.transform, new Vector3(0.4f, 0.35f, 0.4f),
                new Vector3(0.5f, 2.55f, lz), new Color(0.9f, 0.35f, 0.2f), true);
            var glow = MakeBlock("LampGlow", root.transform, new Vector3(0.24f, 0.2f, 0.24f),
                new Vector3(0.5f, 2.55f, lz), new Color(1f, 0.85f, 0.45f), true);
            DisableShadowCasting(glow);
            AddGlowLight(root.transform, new Vector3(0.5f, 2.35f, lz), 8f, 1.2f, new Color(1f, 0.85f, 0.45f));
        }
        // ── Interior center lamps (additional coverage) ──
        foreach (float lz in new[] { -1f, 1f })
        {
            MakeBlock("LampCordC", root.transform, new Vector3(0.04f, 0.5f, 0.04f),
                new Vector3(0.5f, 2.95f, lz), trimC, true);
            MakeBlock("LampC", root.transform, new Vector3(0.4f, 0.35f, 0.4f),
                new Vector3(0.5f, 2.55f, lz), new Color(0.9f, 0.35f, 0.2f), true);
            var glowC = MakeBlock("LampGlowC", root.transform, new Vector3(0.24f, 0.2f, 0.24f),
                new Vector3(0.5f, 2.55f, lz), new Color(1f, 0.85f, 0.45f), true);
            DisableShadowCasting(glowC);
            AddGlowLight(root.transform, new Vector3(0.5f, 2.35f, lz), 8f, 1.2f, new Color(1f, 0.85f, 0.45f));
        }

        // ── Interior planters ──
        MakeBlock("Planter", root.transform, new Vector3(0.45f, 0.55f, 0.45f),
            new Vector3(4.6f, 0.28f, -2.7f), new Color(0.62f, 0.38f, 0.2f), true);
        MakeBlock("PlantLeaves", root.transform, new Vector3(0.55f, 0.55f, 0.55f),
            new Vector3(4.6f, 0.78f, -2.7f), new Color(0.25f, 0.55f, 0.25f), true);
        MakeBlock("Planter", root.transform, new Vector3(0.4f, 0.5f, 0.4f),
            new Vector3(2.6f, 0.25f, 4.2f), new Color(0.62f, 0.38f, 0.2f), true);
        MakeBlock("PlantLeaves", root.transform, new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(2.6f, 0.72f, 4.2f), new Color(0.25f, 0.55f, 0.25f), true);

        // ── Outdoor patio tables (east of building) ──
        foreach (float tz in new[] { -2.6f, 2.6f })
        {
            MakeBlock("PatioTable", root.transform, new Vector3(1.5f, 0.08f, 0.85f),
                new Vector3(halfW + 2.3f, 0.04f, tz), new Color(0.6f, 0.42f, 0.24f), true);
            MakeBlock("PatioLeg", root.transform, new Vector3(0.1f, 0.35f, 0.1f),
                new Vector3(halfW + 2.3f, -0.1f, tz), trimC, true);
            MakeBlock("UmbrellaPole", root.transform, new Vector3(0.08f, 1.5f, 0.08f),
                new Vector3(halfW + 2.3f, 0.75f, tz), trimC, true);
            MakeBlock("UmbrellaCanopy", root.transform, new Vector3(1.4f, 0.1f, 1.4f),
                new Vector3(halfW + 2.3f, 1.55f, tz), awningC, true);
            MakeBlock("Stool", root.transform, new Vector3(0.3f, 0.5f, 0.3f),
                new Vector3(halfW + 1.3f, 0.25f, tz - 0.65f), trimC, true).AddComponent<SittableSeat>();
        }

        // ── Seated customer at north patio table ──
        MakeBlock("StoolN", root.transform, new Vector3(0.3f, 0.5f, 0.3f),
            new Vector3(halfW + 1.3f, 0.25f, 3.25f), trimC, true);
        BuildSeatedCustomer(root.transform, new Vector3(halfW + 1.3f, 0.75f, 3.25f),
            Quaternion.Euler(0f, -90f, 0f), new Color(0.85f, 0.55f, 0.4f), new Color(0.4f, 0.32f, 0.25f),
            new Color(0.9f, 0.78f, 0.68f));

        // ── Wooden fence + gate (east front, clear of the road) ──
        float fenceX = halfW + 3.6f;
        foreach (float fz in new[] { -5f, -3.2f, -1.9f, 1.9f, 3.2f, 5f })
        {
            MakeBlock("FencePost", root.transform, new Vector3(0.16f, 1.1f, 0.16f),
                new Vector3(fenceX, 0.55f, fz), trimC, true);
        }
        foreach (float secZ in new[] { -2.95f, 2.95f })
        {
            MakeBlock("FenceRailTop", root.transform, new Vector3(0.08f, 0.12f, 4.1f),
                new Vector3(fenceX + 0.16f, 0.98f, secZ), trimC, true);
            MakeBlock("FenceRailBot", root.transform, new Vector3(0.08f, 0.12f, 4.1f),
                new Vector3(fenceX + 0.16f, 0.5f, secZ), trimC, true);
        }
        MakeBlock("GatePostL", root.transform, new Vector3(0.2f, 1.2f, 0.2f),
            new Vector3(fenceX, 0.6f, -0.9f), trimC, true);
        MakeBlock("GatePostR", root.transform, new Vector3(0.2f, 1.2f, 0.2f),
            new Vector3(fenceX, 0.6f, 0.9f), trimC, true);
        MakeBlock("GateLintel", root.transform, new Vector3(0.12f, 0.14f, 1.9f),
            new Vector3(fenceX + 0.18f, 1.25f, 0f), trimC, true);

        // ── Plants, planters, shrubs ──
        MakeBlock("PlanterN", root.transform, new Vector3(0.7f, 0.32f, 0.45f),
            new Vector3(halfW + 1.3f, 0.16f, -4.9f), stoneC, true);
        MakeBlock("PlanterNFlower", root.transform, new Vector3(0.22f, 0.3f, 0.22f),
            new Vector3(halfW + 1.25f, 0.45f, -4.9f), new Color(0.9f, 0.25f, 0.35f), true);
        MakeBlock("PlanterS", root.transform, new Vector3(0.7f, 0.32f, 0.45f),
            new Vector3(halfW + 1.3f, 0.16f, 4.9f), stoneC, true);
        MakeBlock("PlanterSFlower", root.transform, new Vector3(0.22f, 0.3f, 0.22f),
            new Vector3(halfW + 1.25f, 0.45f, 4.9f), new Color(1f, 0.7f, 0.2f), true);
        MakeBlock("PlantPot", root.transform, new Vector3(0.4f, 0.35f, 0.4f),
            new Vector3(halfW + 0.9f, 0.18f, -2.6f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeaf", root.transform, new Vector3(0.65f, 0.75f, 0.65f),
            new Vector3(halfW + 0.9f, 0.65f, -2.6f), new Color(0.25f, 0.55f, 0.2f), true);
        MakeBlock("PlantPot2", root.transform, new Vector3(0.4f, 0.35f, 0.4f),
            new Vector3(halfW + 0.9f, 0.18f, 2.6f), new Color(0.55f, 0.35f, 0.2f), true);
        MakeBlock("PlantLeaf2", root.transform, new Vector3(0.65f, 0.75f, 0.65f),
            new Vector3(halfW + 0.9f, 0.65f, 2.6f), new Color(0.25f, 0.55f, 0.2f), true);
        MakeBlock("ShrubN", root.transform, new Vector3(1f, 0.55f, 0.75f),
            new Vector3(-halfW - 1.2f, 0.28f, -4.3f), new Color(0.2f, 0.45f, 0.2f), true);
        MakeBlock("ShrubS", root.transform, new Vector3(0.9f, 0.5f, 0.7f),
            new Vector3(-halfW - 1.2f, 0.25f, 4.3f), new Color(0.2f, 0.45f, 0.2f), true);

        // ── Chef behind counter ──
        BuildChefNpc(root.transform, new Vector3(-3.6f, 1.13f, 0f), Quaternion.Euler(0f, 90f, 0f));

        root.AddComponent<SteamEmitter>();

        return root;
    }

    private static void BuildChefNpc(Transform parent, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("RestaurantNPC");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        Color coatC = new Color(0.95f, 0.95f, 0.92f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color noseC = new Color(0.72f, 0.62f, 0.54f);
        Color pantsC = new Color(0.16f, 0.16f, 0.16f);
        Color bootC = new Color(0.1f, 0.1f, 0.1f);

        MakeBlock("LegL", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(-0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(-0.15f, -0.88f, 0f), bootC, true);
        MakeBlock("BootR", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0.15f, -0.88f, 0f), bootC, true);

        MakeBlock("Body", root.transform, new Vector3(0.52f, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), coatC, true);
        MakeBlock("Apron", root.transform, new Vector3(0.5f, 0.42f, 0.06f), new Vector3(0f, -0.05f, 0.11f), new Color(0.85f, 0.86f, 0.88f), true);
        MakeBlock("Belt", root.transform, new Vector3(0.54f, 0.07f, 0.06f), new Vector3(0f, 0.12f, -0.16f), pantsC, true);
        MakeBlock("Collar", root.transform, new Vector3(0.2f, 0.08f, 0.06f), new Vector3(0f, 0.34f, -0.15f), coatC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(-0.09f, 0.55f, -0.168f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(0.09f, 0.55f, -0.168f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), noseC, true);

        MakeBlock("HatBase", root.transform, new Vector3(0.36f, 0.06f, 0.36f), new Vector3(0f, 0.65f, 0f), coatC, true);
        MakeBlock("HatCrown", root.transform, new Vector3(0.3f, 0.18f, 0.3f), new Vector3(0f, 0.75f, 0f), coatC, true);

        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.36f, 0.1f, 0f), coatC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.36f, 0.1f, 0f), coatC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.36f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.36f, -0.14f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        root.AddComponent<ChefNPC>();
    }

    private static void BuildSeatedCustomer(Transform parent, Vector3 position, Quaternion rotation, Color shirtC, Color pantsC, Color skinC)
    {
        var root = new GameObject("RestaurantCustomer");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        MakeBlock("LegL", root.transform, new Vector3(0.12f, 0.12f, 0.3f), new Vector3(-0.1f, -0.18f, -0.18f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.12f, 0.12f, 0.3f), new Vector3(0.1f, -0.18f, -0.18f), pantsC, true);
        MakeBlock("Body", root.transform, new Vector3(0.42f, 0.5f, 0.3f), new Vector3(0f, 0.02f, 0f), shirtC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.32f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.26f, 0.28f), new Vector3(0f, 0.46f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.29f, 0.09f, 0.29f), new Vector3(0f, 0.6f, 0f), new Color(0.15f, 0.1f, 0.07f), true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.07f, 0.05f, 0.03f), new Vector3(-0.08f, 0.49f, -0.145f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.07f, 0.05f, 0.03f), new Vector3(0.08f, 0.49f, -0.145f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.045f, 0.045f, 0.035f), new Vector3(-0.08f, 0.49f, -0.153f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.045f, 0.045f, 0.035f), new Vector3(0.08f, 0.49f, -0.153f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.06f, 0.05f, 0.04f), new Vector3(0f, 0.455f, -0.152f), new Color(0.72f, 0.62f, 0.54f), true);
        MakeBlock("ArmL", root.transform, new Vector3(0.11f, 0.35f, 0.11f), new Vector3(-0.29f, 0.08f, 0.05f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.11f, 0.35f, 0.11f), new Vector3(0.29f, 0.08f, 0.05f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(-0.29f, -0.1f, 0.05f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(0.29f, -0.1f, 0.05f), skinC, true);
    }

    private static void BuildStandingStaff(Transform parent, Vector3 position, Quaternion rotation, Color shirtC, Color pantsC, Color skinC)
    {
        var root = new GameObject("RestaurantStaff");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        MakeBlock("LegL", root.transform, new Vector3(0.15f, 0.5f, 0.15f), new Vector3(-0.13f, -0.55f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.15f, 0.5f, 0.15f), new Vector3(0.13f, -0.55f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.18f, 0.09f, 0.28f), new Vector3(-0.13f, -0.85f, 0f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("BootR", root.transform, new Vector3(0.18f, 0.09f, 0.28f), new Vector3(0.13f, -0.85f, 0f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("Body", root.transform, new Vector3(0.44f, 0.5f, 0.28f), new Vector3(0f, 0f, 0f), shirtC, true);
        MakeBlock("Apron", root.transform, new Vector3(0.42f, 0.4f, 0.05f), new Vector3(0f, -0.04f, 0.1f), new Color(0.95f, 0.95f, 0.92f), true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.32f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.26f, 0.28f), new Vector3(0f, 0.47f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.29f, 0.09f, 0.29f), new Vector3(0f, 0.61f, 0f), new Color(0.2f, 0.13f, 0.08f), true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.07f, 0.05f, 0.03f), new Vector3(-0.08f, 0.5f, -0.145f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.07f, 0.05f, 0.03f), new Vector3(0.08f, 0.5f, -0.145f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.045f, 0.045f, 0.035f), new Vector3(-0.08f, 0.5f, -0.153f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.045f, 0.045f, 0.035f), new Vector3(0.08f, 0.5f, -0.153f), new Color(0.05f, 0.03f, 0.01f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.06f, 0.05f, 0.04f), new Vector3(0f, 0.465f, -0.152f), new Color(0.72f, 0.62f, 0.54f), true);
        MakeBlock("ArmL", root.transform, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(-0.31f, 0.08f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(0.31f, 0.08f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(-0.31f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(0.31f, -0.14f, 0f), skinC, true);
    }
    // ==================== MapBuilderShop.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  SHOP / BUFFALO SHOP  (10 x 4 x 10, counter, shelves, awning)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildShop(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("Shop");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.404f, 0.361f, 0.302f);
        Color roofC    = new Color(0.871f, 0.161f, 0.11f);
        Color ridgeC   = new Color(0.537f, 0.067f, 0.118f);
        Color eaveC    = new Color(0.18f, 0.18f, 0.18f);
        Color stoneC   = new Color(0.439f, 0.4f, 0.361f);
        Color floorC   = new Color(0.357f, 0.275f, 0.18f);
        Color frameC   = new Color(0.2f, 0.125f, 0.078f);
        Color counterC = new Color(0.584f, 0.294f, 0.165f);
        Color shelfC   = new Color(0.455f, 0.275f, 0.157f);
        Color winC     = new Color(0.549f, 0.784f, 0.863f);
        Color signC    = new Color(0.886f, 0.753f, 0.098f);
        Color awningC  = new Color(0.843f, 0.184f, 0.161f);
        Color itemC    = new Color(0.949f, 0.584f, 0.094f);

        // ── Walls ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 4f, 0.5f), new Vector3(0f, 2f, -5f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 4f, 0.5f), new Vector3(0f, 2f, 5f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 10f), new Vector3(-5f, 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(5f, 2f, -3.25f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(5f, 2f, 3.25f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1.2f, 3f), new Vector3(5f, 3.4f, 0f), wallC);
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.5f, 10f), Vector3.zero, floorC);

        // ── Gabled roof ──
        float rise = 2.5f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.2f;
        float roofZ = 10f + overhang * 2f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(-halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.55f, 0.3f, roofZ + 0.2f),
            new Vector3(0f, 4f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(halfW, 4.1f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(-halfW, 4.1f, 0f), eaveC);

        foreach (float gz in new[] { -5f, 5f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float sw = 10f * (1f - t) + 0.2f;
                float sy = 4f + (i + 0.5f) * rise / 5f;
                float sh = rise / 5f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.4f, 11.5f),
            new Vector3(0f, -0.15f, 0f), stoneC);

        // ── Sign ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3.5f),
            new Vector3(5.08f, 3.6f, 0f), signC, true);
        var shopSignLabel = new GameObject("ShopSignLabel");
        shopSignLabel.transform.SetParent(root.transform);
        shopSignLabel.transform.localPosition = new Vector3(5.3f, 3.6f, 0f);
        shopSignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        shopSignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var shopSignTmp = shopSignLabel.AddComponent<TMPro.TextMeshPro>();
        shopSignTmp.text = Localization.T("CỬA HÀNG");
        shopSignTmp.fontSize = 2.0f;
        shopSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        shopSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        shopSignTmp.outlineWidth = 0.18f;
        shopSignTmp.outlineColor = Color.black;
        shopSignTmp.rectTransform.sizeDelta = new Vector3(6.4f, 1.5f);

        // ── Entrance awning ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.15f, 3.5f),
            new Vector3(5.8f, 3.8f, 0f), awningC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(6.6f, 1.9f, -1.5f), frameC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(6.6f, 1.9f, 1.5f), frameC, true);

        // ── Windows ──
        foreach (float wz in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.2f, 1.2f),
                new Vector3(-5.03f, 2.2f, wz), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.08f, 1.2f),
                new Vector3(-5.03f, 2.2f, wz), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.2f, 0.08f),
                new Vector3(-5.03f, 2.2f, wz), frameC, true);
        }
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.2f, 0.14f),
                new Vector3(wx, 2.2f, -5.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.2f, 0.16f),
                new Vector3(wx, 2.2f, -5.03f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.08f, 0.16f),
                new Vector3(wx, 2.2f, -5.03f), frameC, true);
        }

        // ── Counter where buffalo stands ──
        MakeBlock("Counter", root.transform, new Vector3(1.8f, 1f, 4f),
            new Vector3(-0.5f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(1.8f, 0.08f, 4.2f),
            new Vector3(-0.5f, 1.04f, 0f), new Color(0.757f, 0.62f, 0.404f), true);
        MakeBlock("CounterFront", root.transform, new Vector3(0.03f, 0.8f, 4f),
            new Vector3(0.4f, 0.4f, 0f), new Color(0.624f, 0.369f, 0.192f), true);

        // ── Shelves behind counter ──
        MakeBlock("ShelfPost", root.transform, new Vector3(0.12f, 4f, 0.12f),
            new Vector3(-4.4f, 2f, -3.5f), frameC, true);
        MakeBlock("ShelfPost", root.transform, new Vector3(0.12f, 4f, 0.12f),
            new Vector3(-4.4f, 2f, 3.5f), frameC, true);
        for (int i = 0; i < 3; i++)
        {
            float sy = 0.5f + i * 1.4f;
            MakeBlock("ShelfBoard", root.transform, new Vector3(0.12f, 0.08f, 7f),
                new Vector3(-4.4f, sy, 0f), shelfC, true);
            MakeBlock("ShelfItem", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(-4.4f, sy + 0.2f, -1.5f + i * 1.5f), itemC, true);
        }

        // ── Door frame ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, -1.5f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, 1.5f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(5.04f, 3.65f, 0f), frameC, true);

        return root;
    }
    // ==================== MapBuilderFishingShop.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  FISHING TOOLS SHOP  (open-air stilted hut)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildFishingShop(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("FishingShop");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color woodC     = new Color(0.45f, 0.30f, 0.15f);
        Color woodDarkC = new Color(0.30f, 0.20f, 0.10f);
        Color thatchC   = new Color(0.72f, 0.60f, 0.35f);
        Color ropeC     = new Color(0.55f, 0.45f, 0.28f);
        Color netC      = new Color(0.80f, 0.80f, 0.75f);
        Color waterC    = new Color(0.30f, 0.55f, 0.85f);
        Color bucketC   = new Color(0.35f, 0.35f, 0.38f);
        Color iceC      = new Color(0.85f, 0.92f, 0.97f);
        Color fishOrange = new Color(1.0f, 0.65f, 0.2f);
        Color fishPink  = new Color(1.0f, 0.50f, 0.40f);
        Color signC     = new Color(0.90f, 0.68f, 0.16f);
        Color frameC    = new Color(0.28f, 0.18f, 0.10f);
        Color winC      = new Color(0.55f, 0.78f, 0.86f);
        Color awningC   = new Color(0.25f, 0.45f, 0.30f);

        float stiltH = 0.8f;
        float halfW = 5f;
        float halfD = 4.5f;

        // ── Stilts (wooden pillars raising the floor) ──
        float[][] stiltPos = new float[][] {
            new float[] { -halfW, -halfD }, new float[] { -halfW, halfD },
            new float[] { halfW, -halfD }, new float[] { halfW, halfD },
            new float[] { 0f, -halfD }, new float[] { 0f, halfD },
            new float[] { -halfW, 0f }, new float[] { halfW, 0f }
        };
        foreach (var sp in stiltPos)
        {
            MakeBlock("Stilt", root.transform, new Vector3(0.18f, stiltH, 0.18f),
                new Vector3(sp[0], stiltH / 2f, sp[1]), woodDarkC, true);
        }

        // ── Platform floor (elevated) ──
        MakeBlock("PlatformFloor", root.transform, new Vector3(halfW * 2f, 0.15f, halfD * 2f),
            new Vector3(0f, stiltH, 0f), woodC);

        // ── Platform deck planks ──
        for (float fx = -halfW + 0.5f; fx <= halfW; fx += 1f)
        {
            MakeBlock("Plank", root.transform, new Vector3(0.08f, 0.16f, halfD * 2f),
                new Vector3(fx, stiltH, 0f), woodDarkC, true);
        }

        // ── Railing around platform ──
        float railY = stiltH + 0.55f;
        // Front railing (+Z side)
        for (float rx = -halfW + 1f; rx <= halfW - 0.5f; rx += 1.5f)
        {
            MakeBlock("RailF", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(rx, railY, halfD), woodC, true);
        }
        MakeBlock("RailBarF", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY, halfD), woodC, true);
        MakeBlock("RailBarF2", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY - 0.4f, halfD), woodC, true);
        // Back railing (-Z side)
        for (float rx = -halfW + 1f; rx <= halfW - 0.5f; rx += 1.5f)
        {
            MakeBlock("RailB", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(rx, railY, -halfD), woodC, true);
        }
        MakeBlock("RailBarB", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY, -halfD), woodC, true);
        MakeBlock("RailBarB2", root.transform, new Vector3(halfW * 2f, 0.08f, 0.08f),
            new Vector3(0f, railY - 0.4f, -halfD), woodC, true);
        // Left railing (-X side)
        for (float rz = -halfD + 1f; rz <= halfD - 0.5f; rz += 1.5f)
        {
            MakeBlock("RailL", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(-halfW, railY, rz), woodC, true);
        }
        MakeBlock("RailBarL", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(-halfW, railY, 0f), woodC, true);
        MakeBlock("RailBarL2", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(-halfW, railY - 0.4f, 0f), woodC, true);
        // Right railing (+X side)
        for (float rz = -halfD + 1f; rz <= halfD - 0.5f; rz += 1.5f)
        {
            MakeBlock("RailR", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(halfW, railY, rz), woodC, true);
        }
        MakeBlock("RailBarR", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(halfW, railY, 0f), woodC, true);
        MakeBlock("RailBarR2", root.transform, new Vector3(0.08f, 0.08f, halfD * 2f),
            new Vector3(halfW, railY - 0.4f, 0f), woodC, true);

        // Corner posts (connect railing to platform at each corner)
        foreach (var cp in new float[][] { new float[] { -halfW, -halfD }, new float[] { -halfW, halfD },
            new float[] { halfW, -halfD }, new float[] { halfW, halfD } })
        {
            MakeBlock("CornerPost", root.transform, new Vector3(0.14f, railY - stiltH + 0.1f, 0.14f),
                new Vector3(cp[0], (railY + stiltH) / 2f, cp[1]), woodDarkC, true);
        }

        // ── Roof posts (4 corner + 2 center) ──
        float roofBase = stiltH + 0.15f;
        float postH = 3.2f;
        float[][] roofPosts = new float[][] {
            new float[] { -halfW, -halfD }, new float[] { -halfW, halfD },
            new float[] { halfW, -halfD }, new float[] { halfW, halfD },
            new float[] { 0f, -halfD }, new float[] { 0f, halfD }
        };
        foreach (var rp in roofPosts)
        {
            MakeBlock("RoofPost", root.transform, new Vector3(0.18f, postH, 0.18f),
                new Vector3(rp[0], roofBase + postH / 2f, rp[1]), woodDarkC, true);
        }

        // ── Thatched roof (two sloped panels) ──
        float roofH = roofBase + postH;
        float roofRise = 1.5f;
        float roofOverhang = 1.2f;
        float roofZLen = halfD * 2f + roofOverhang * 2f;
        float panelLen = Mathf.Sqrt(halfW * halfW + roofRise * roofRise) + roofOverhang * 0.5f;
        float tilt = Mathf.Atan2(roofRise, halfW) * Mathf.Rad2Deg;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.4f, roofZLen),
            new Vector3(halfW / 2f, roofH + roofRise / 2f, 0f), thatchC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.4f, roofZLen),
            new Vector3(-halfW / 2f, roofH + roofRise / 2f, 0f), thatchC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.4f, 0.25f, roofZLen + 0.3f),
            new Vector3(0f, roofH + roofRise + 0.05f, 0f), woodDarkC);

        // ── Fishing nets draped from roof edges ──
        MakeBlock("Net1", root.transform, new Vector3(0.08f, 1.5f, 3f),
            new Vector3(halfW + 0.3f, roofH + 0.5f, 1.5f), netC, true);
        MakeBlock("Net2", root.transform, new Vector3(0.08f, 1.2f, 2.5f),
            new Vector3(halfW + 0.3f, roofH + 0.6f, -1.5f), netC, true);
        MakeBlock("Net3", root.transform, new Vector3(2f, 1.5f, 0.08f),
            new Vector3(-2f, roofH + 0.5f, -halfD - 0.3f), netC, true);

        // ── Sign ──
        MakeBlock("Sign", root.transform, new Vector3(0.2f, 0.8f, 3f),
            new Vector3(halfW + 0.25f, roofH - 0.5f, 0f), signC, true);
        var fishingSignLabel = new GameObject("FishingShopSignLabel");
        fishingSignLabel.transform.SetParent(root.transform);
        fishingSignLabel.transform.localPosition = new Vector3(halfW + 0.45f, roofH - 0.5f, 0f);
        fishingSignLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        fishingSignLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        var fishingSignTmp = fishingSignLabel.AddComponent<TMPro.TextMeshPro>();
        fishingSignTmp.text = Localization.T("CỬA HÀNG CÂU CÁ");
        fishingSignTmp.fontSize = 1.8f;
        fishingSignTmp.alignment = TMPro.TextAlignmentOptions.Center;
        fishingSignTmp.color = new Color(0.98f, 0.94f, 0.85f);
        fishingSignTmp.outlineWidth = 0.18f;
        fishingSignTmp.outlineColor = Color.black;
        fishingSignTmp.rectTransform.sizeDelta = new Vector3(7.0f, 1.5f);

        // ── Counter (vendor area, +X side near entrance) ──
        MakeBlock("Counter", root.transform, new Vector3(2.2f, 0.9f, 3.5f),
            new Vector3(halfW - 1.2f, stiltH + 0.45f, 0f), woodC);
        MakeBlock("CounterTop", root.transform, new Vector3(2.2f, 0.06f, 3.7f),
            new Vector3(halfW - 1.2f, stiltH + 0.93f, 0f), new Color(0.65f, 0.52f, 0.32f), true);

        // ── Ice display on counter ──
        MakeBlock("IceBlock", root.transform, new Vector3(1.4f, 0.2f, 2.5f),
            new Vector3(halfW - 1.2f, stiltH + 1.03f, 0f), iceC, true);
        // Fish on ice
        MakeBlock("Fish1", root.transform, new Vector3(0.3f, 0.1f, 0.5f),
            new Vector3(halfW - 1.5f, stiltH + 1.18f, -0.6f), fishOrange, true);
        MakeBlock("Fish2", root.transform, new Vector3(0.3f, 0.1f, 0.5f),
            new Vector3(halfW - 1.0f, stiltH + 1.18f, 0.3f), fishPink, true);

        // ── Fishing rod display (on back wall / rack) ──
        MakeBlock("RodRack", root.transform, new Vector3(0.1f, 1.8f, 3f),
            new Vector3(-halfW + 0.2f, stiltH + 1.5f, 0f), woodDarkC, true);
        for (int i = 0; i < 3; i++)
        {
            float rz = -1f + i * 1f;
            MakeBlock("DisplayRod" + i, root.transform, new Vector3(0.04f, 1.5f, 0.04f),
                new Vector3(-halfW + 0.3f, stiltH + 1.6f, rz), new Color(0.55f, 0.30f, 0.08f), true);
        }

        // ── Buckets + barrels ──
        MakeBlock("Bucket1", root.transform, new Vector3(0.4f, 0.45f, 0.4f),
            new Vector3(-2f, stiltH + 0.23f, halfD - 0.5f), bucketC, true);
        MakeBlock("Bucket2", root.transform, new Vector3(0.35f, 0.4f, 0.35f),
            new Vector3(-3f, stiltH + 0.2f, halfD - 0.5f), new Color(0.40f, 0.30f, 0.15f), true);
        MakeBlock("Barrel", root.transform, new Vector3(0.6f, 0.8f, 0.6f),
            new Vector3(-halfW + 0.5f, stiltH + 0.4f, -halfD + 0.5f), woodDarkC, true);

        // ── Awning over entrance (+X side) ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.12f, 3f),
            new Vector3(halfW + 0.75f, roofH - 0.3f, 0f), awningC, true);
        float awningPostH = roofH - 0.3f;
        MakeBlock("AwningPost", root.transform, new Vector3(0.1f, awningPostH, 0.1f),
            new Vector3(halfW + 1.4f, awningPostH / 2f, -1.2f), woodC, true);
        MakeBlock("AwningPost", root.transform, new Vector3(0.1f, awningPostH, 0.1f),
            new Vector3(halfW + 1.4f, awningPostH / 2f, 1.2f), woodC, true);

        // ── Water trough under the platform ──
        MakeBlock("WaterTrough", root.transform, new Vector3(2.5f, 0.3f, 1.5f),
            new Vector3(0f, 0.15f, 0f), waterC, true);

        // ── Small stepping-stone path to entrance ──
        for (int i = 0; i < 3; i++)
        {
            MakeBlock("SteppingStone", root.transform, new Vector3(0.6f, 0.08f, 0.6f),
                new Vector3(halfW + 1.5f + i * 0.8f, 0.04f, 0f), new Color(0.55f, 0.52f, 0.48f), true);
        }

        return root;
    }

    public static void BuildFishingShopkeeper(Transform parent, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("FishingShopNPC");
        root.transform.SetParent(parent);
        root.transform.localPosition = position;
        root.transform.localRotation = rotation;

        Color furC = new Color(0.85f, 0.55f, 0.2f);
        Color furDark = new Color(0.7f, 0.42f, 0.15f);
        Color bellyC = new Color(0.95f, 0.85f, 0.7f);
        Color noseC = new Color(0.9f, 0.5f, 0.5f);
        Color eyeC = new Color(0.2f, 0.7f, 0.3f);

        // ── Body ──
        MakeBlock("Body", root.transform, new Vector3(0.35f, 0.28f, 0.55f), new Vector3(0f, 0.2f, 0f), furC, true);
        MakeBlock("Belly", root.transform, new Vector3(0.25f, 0.2f, 0.4f), new Vector3(0f, 0.15f, 0f), bellyC, true);

        // ── Head ──
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.26f, 0.26f), new Vector3(0f, 0.38f, 0.22f), furC, true);
        MakeBlock("Snout", root.transform, new Vector3(0.12f, 0.1f, 0.1f), new Vector3(0f, 0.34f, 0.35f), bellyC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.06f, 0.04f, 0.03f), new Vector3(0f, 0.36f, 0.4f), noseC, true);

        // ── Ears ──
        MakeBlock("EarL", root.transform, new Vector3(0.06f, 0.12f, 0.05f), new Vector3(-0.1f, 0.55f, 0.18f), furC, true);
        MakeBlock("EarR", root.transform, new Vector3(0.06f, 0.12f, 0.05f), new Vector3(0.1f, 0.55f, 0.18f), furC, true);
        MakeBlock("EarInnerL", root.transform, new Vector3(0.03f, 0.07f, 0.03f), new Vector3(-0.1f, 0.55f, 0.19f), noseC, true);
        MakeBlock("EarInnerR", root.transform, new Vector3(0.03f, 0.07f, 0.03f), new Vector3(0.1f, 0.55f, 0.19f), noseC, true);

        // ── Eyes ──
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.06f, 0.06f, 0.03f), new Vector3(-0.09f, 0.41f, 0.33f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.06f, 0.06f, 0.03f), new Vector3(0.09f, 0.41f, 0.33f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeIrisL", root.transform, new Vector3(0.04f, 0.04f, 0.03f), new Vector3(-0.09f, 0.41f, 0.34f), eyeC, true);
        MakeBlock("EyeIrisR", root.transform, new Vector3(0.04f, 0.04f, 0.03f), new Vector3(0.09f, 0.41f, 0.34f), eyeC, true);

        // ── Whiskers ──
        MakeBlock("WhiskerL1", root.transform, new Vector3(0.2f, 0.01f, 0.01f), new Vector3(-0.15f, 0.34f, 0.36f), bellyC, true);
        MakeBlock("WhiskerL2", root.transform, new Vector3(0.18f, 0.01f, 0.01f), new Vector3(-0.14f, 0.32f, 0.36f), bellyC, true);
        MakeBlock("WhiskerR1", root.transform, new Vector3(0.2f, 0.01f, 0.01f), new Vector3(0.15f, 0.34f, 0.36f), bellyC, true);
        MakeBlock("WhiskerR2", root.transform, new Vector3(0.18f, 0.01f, 0.01f), new Vector3(0.14f, 0.32f, 0.36f), bellyC, true);

        // ── Legs ──
        MakeBlock("LegFL", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(-0.12f, 0.0f, 0.16f), furDark, true);
        MakeBlock("LegFR", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(0.12f, 0.0f, 0.16f), furDark, true);
        MakeBlock("LegBL", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(-0.12f, 0.0f, -0.16f), furDark, true);
        MakeBlock("LegBR", root.transform, new Vector3(0.08f, 0.2f, 0.08f), new Vector3(0.12f, 0.0f, -0.16f), furDark, true);
        MakeBlock("PawFL", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(-0.12f, -0.1f, 0.17f), bellyC, true);
        MakeBlock("PawFR", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(0.12f, -0.1f, 0.17f), bellyC, true);
        MakeBlock("PawBL", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(-0.12f, -0.1f, -0.17f), bellyC, true);
        MakeBlock("PawBR", root.transform, new Vector3(0.09f, 0.04f, 0.1f), new Vector3(0.12f, -0.1f, -0.17f), bellyC, true);

        // ── Tail ──
        MakeBlock("TailBase", root.transform, new Vector3(0.06f, 0.06f, 0.2f), new Vector3(0f, 0.28f, -0.35f), furC, true);
        MakeBlock("TailMid", root.transform, new Vector3(0.05f, 0.05f, 0.15f), new Vector3(0f, 0.38f, -0.48f), furC, true);
        MakeBlock("TailTip", root.transform, new Vector3(0.04f, 0.04f, 0.1f), new Vector3(0f, 0.45f, -0.55f), furDark, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.4f, 0.5f, 0.7f);
        col.center = new Vector3(0f, 0.2f, 0f);
        col.isTrigger = true;
    }


    private static readonly (string goName, string locKey)[] _signDefs = {
        ("CafeSignLabel", "QUÁN CÀ PHÊ"),
        ("StoreSignLabel", "TIỆN LỢI"),
        ("LibrarySignLabel", "THƯ VIỆN"),
        ("ClubNeonLabel", "DANCE NIGHT"),
        ("PoliceSignLabel", "CẢNH SÁT"),
        ("RestaurantSignLabel", "NHÀ HÀNG"),
        ("ShopSignLabel", "CỬA HÀNG"),
        ("FishingShopSignLabel", "CỬA HÀNG CÂU CÁ"),
    };

    public static void RefreshWorldSignTexts()
    {
        foreach (var (goName, locKey) in _signDefs)
        {
            var go = GameObject.Find(goName);
            if (go == null) continue;
            var tmp = go.GetComponent<TMPro.TextMeshPro>();
            if (tmp != null)
                tmp.text = Localization.T(locKey);
        }
    }
}
