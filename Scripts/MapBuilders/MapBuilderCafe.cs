using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
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
        MakeBlock("Foundation", root.transform, new Vector3(halfW * 2f + 1.5f, 0.4f, depth + 1.5f), new Vector3(0f, -0.2f, 0f), stoneC);

        // ── Flat roof ──
        MakeBlock("Roof", root.transform, new Vector3(halfW * 2f + 1f, 0.35f, depth + 1f), new Vector3(0f, 4.15f, 0f), roofC);
        MakeBlock("RoofTrim", root.transform, new Vector3(halfW * 2f + 1.4f, 0.12f, 0.5f), new Vector3(0f, 4.32f, depth / 2f + 0.6f), trimC, true);

        // ── Sign above the door ──
        MakeBlock("Sign", root.transform, new Vector3(3.6f, 0.7f, 0.2f), new Vector3(0f, 3.2f, depth / 2f + 0.1f), signC, true);
        var signLabel = new GameObject("CafeSignLabel");
        signLabel.transform.SetParent(root.transform);
        signLabel.transform.localPosition = new Vector3(0f, 3.2f, depth / 2f + 0.32f);
        signLabel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var signTmp = signLabel.AddComponent<TMPro.TextMeshPro>();
        signTmp.text = "QUÁN CÀ PHÊ";
        signTmp.fontSize = 0.75f;
        signTmp.alignment = TMPro.TextAlignmentOptions.Center;
        signTmp.color = new Color(0.3f, 0.15f, 0.05f);
        signTmp.outlineWidth = 0.12f;
        signTmp.outlineColor = Color.white;
        signTmp.rectTransform.sizeDelta = new Vector3(3.2f, 0.6f);

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
            MakeBlock("Stool", root.transform, new Vector3(0.32f, 0.5f, 0.32f), new Vector3(tx, 0.25f, 2.15f), trimC, true);
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

}
