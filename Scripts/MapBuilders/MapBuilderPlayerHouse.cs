using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  PLAYER HOUSE  (10 x 5 x 10, gabled roof, chimney, porch, bed)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildPlayerHouse(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("PlayerHouse");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color woodC    = new Color(0.63f, 0.39f, 0.18f);
        Color roofC    = new Color(0.635f, 0.243f, 0.149f);
        Color ridgeC   = new Color(0.345f, 0.11f, 0.039f);
        Color eaveC    = new Color(0.569f, 0.345f, 0.157f);
        Color stoneC   = new Color(0.439f, 0.4f, 0.361f);
        Color chimneyC = new Color(0.384f, 0.333f, 0.29f);
        Color winC     = new Color(0.549f, 0.784f, 0.863f);
        Color frameC   = new Color(0.165f, 0.094f, 0.031f);
        Color shuttC   = new Color(0.227f, 0.376f, 0.173f);
        Color porchC   = new Color(0.58f, 0.361f, 0.165f);

        // ── Walls + floor ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, -5f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, 5f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 10f), new Vector3(-5f, 2.5f, 0f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 3.5f), new Vector3(5f, 2.5f, -3.25f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 3.5f), new Vector3(5f, 2.5f, 3.25f), woodC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1f, 3f), new Vector3(5f, 4.5f, 0f), woodC);
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.5f, 10f), Vector3.zero, woodC);

        // ── Gabled roof ──
        float rise = 3f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.6f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.65f, 10f + overhang * 2f),
            new Vector3(halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.65f, 10f + overhang * 2f),
            new Vector3(-halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.68f, 0.38f, 10f + overhang * 2f + 0.2f),
            new Vector3(0f, 5f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.55f, 0.32f, 10f + overhang * 2f + 0.2f),
            new Vector3(halfW, 5.05f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.55f, 0.32f, 10f + overhang * 2f + 0.2f),
            new Vector3(-halfW, 5.05f, 0f), eaveC);

        // Gable end fill
        foreach (float gz in new[] { -5f, 5f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 6; i++)
            {
                float t = (i + 0.5f) / 6f;
                float sw = 10f * (1f - t) + 0.2f;
                float sy = 5f + (i + 0.5f) * rise / 6f;
                float sh = rise / 6f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), woodC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.5f, 11.5f),
            new Vector3(0f, -0.27f, 0f), stoneC);
        MakeBlock("Foundation", root.transform, new Vector3(10.8f, 0.22f, 10.8f),
            new Vector3(0f, -0.52f, 0f), stoneC);

        // ── Chimney ──
        float chX = 2.8f, chZ = 2f;
        float chBot = 5f - 0.8f;
        float chTop = 5f + rise + 1.2f;
        float chH = chTop - chBot;
        MakeBlock("Chimney", root.transform, new Vector3(1.3f, chH, 1.3f),
            new Vector3(chX, (chBot + chTop) / 2f, chZ), chimneyC);
        MakeBlock("ChimneyCap", root.transform, new Vector3(1.65f, 0.44f, 1.65f),
            new Vector3(chX, chTop + 0.22f, chZ), new Color(0.259f, 0.212f, 0.18f));

        // ── Front wall windows ──
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.4f, 0.14f),
                new Vector3(wx, 2.8f, -5.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
                new Vector3(wx, 2.8f, -5.03f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.1f, 0.16f),
                new Vector3(wx, 2.8f, -5.03f), frameC, true);
            MakeBlock("Shutter", root.transform, new Vector3(0.22f, 1.4f, 0.12f),
                new Vector3(wx - 0.88f, 2.8f, -5.03f), shuttC, true);
            MakeBlock("Shutter", root.transform, new Vector3(0.22f, 1.4f, 0.12f),
                new Vector3(wx + 0.88f, 2.8f, -5.03f), shuttC, true);
        }

        // ── Back wall window ──
        MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.4f, 0.14f),
            new Vector3(0f, 2.8f, 5.03f), winC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
            new Vector3(0f, 2.8f, 5.03f), frameC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.1f, 0.16f),
            new Vector3(0f, 2.8f, 5.03f), frameC, true);

        // ── Left wall window ──
        MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.4f, 1.4f),
            new Vector3(-5.03f, 2.8f, 0f), winC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.1f, 1.4f),
            new Vector3(-5.03f, 2.8f, 0f), frameC, true);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.4f, 0.1f),
            new Vector3(-5.03f, 2.8f, 0f), frameC, true);

        // ── Right side entrance ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.32f, 4.2f, 0.32f),
            new Vector3(5.03f, 2.1f, -1.55f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.32f, 4.2f, 0.32f),
            new Vector3(5.03f, 2.1f, 1.55f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.32f, 0.35f, 3.42f),
            new Vector3(5.03f, 4.35f, 0f), frameC, true);
        MakeBlock("Porch", root.transform, new Vector3(1.2f, 0.3f, 4.2f),
            new Vector3(5.62f, 4.05f, 0f), porchC, true);
        MakeBlock("PorchColumn", root.transform, new Vector3(0.24f, 4.05f, 0.24f),
            new Vector3(6.12f, 2f, -1.8f), frameC, true);
        MakeBlock("PorchColumn", root.transform, new Vector3(0.24f, 4.05f, 0.24f),
            new Vector3(6.12f, 2f, 1.8f), frameC, true);

        // ── Swinging door ──
        var doorPivot = new GameObject("Door");
        doorPivot.transform.SetParent(root.transform);
        doorPivot.transform.localPosition = new Vector3(5.03f, 2f, -1.55f);
        var doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorPanel.name = "DoorPanel";
        doorPanel.transform.SetParent(doorPivot.transform);
        doorPanel.transform.localPosition = new Vector3(1.5f, 0f, 0f);
        doorPanel.transform.localScale = new Vector3(3f, 4f, 0.3f);
        doorPanel.GetComponent<MeshRenderer>().material.color = frameC;
        doorPanel.AddComponent<BoxCollider>();

        // ── Furniture ──
        var sofaC  = new Color(0.4f, 0.55f, 0.35f);
        var tableC = new Color(0.361f, 0.259f, 0.145f);
        var shelfC = new Color(0.455f, 0.275f, 0.157f);
        var rugC   = new Color(0.6f, 0.25f, 0.22f);
        var clothC = new Color(0.55f, 0.7f, 0.78f);
        var metalC = new Color(0.72f, 0.75f, 0.78f);
        var goldC  = new Color(0.886f, 0.753f, 0.098f);
        var amberC = new Color(0.95f, 0.72f, 0.25f);
        var leafC  = new Color(0.227f, 0.55f, 0.3f);
        var bookC  = new Color(0.65f, 0.3f, 0.3f);
        var doorC  = new Color(0.45f, 0.3f, 0.15f);

        // ── Living room (rug, sofa, coffee table, chair, lamp) ──
        MakeBlock("Rug", root.transform, new Vector3(3.6f, 0.06f, 2.6f),
            new Vector3(-2f, 0.28f, 0.5f), rugC, true);
        MakeBlock("Sofa", root.transform, new Vector3(3f, 0.35f, 1.1f),
            new Vector3(-2f, 0.425f, 2f), sofaC, true);
        MakeBlock("SofaBack", root.transform, new Vector3(3f, 0.75f, 0.18f),
            new Vector3(-2f, 0.85f, 2.45f), sofaC, true);
        MakeBlock("SofaArmL", root.transform, new Vector3(0.25f, 0.55f, 1.1f),
            new Vector3(-3.375f, 0.625f, 2f), sofaC, true);
        MakeBlock("SofaArmR", root.transform, new Vector3(0.25f, 0.55f, 1.1f),
            new Vector3(-0.625f, 0.625f, 2f), sofaC, true);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.15f, 0.12f, 0.85f),
            new Vector3(-2.55f, 0.66f, 2f), clothC, true);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.15f, 0.12f, 0.85f),
            new Vector3(-1.45f, 0.66f, 2f), clothC, true);
        MakeBlock("TableTop", root.transform, new Vector3(1.3f, 0.08f, 0.75f),
            new Vector3(-2f, 0.62f, 0.2f), tableC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.3f, 0.08f),
            new Vector3(-2.59f, 0.4f, -0.13f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.3f, 0.08f),
            new Vector3(-1.41f, 0.4f, -0.13f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.3f, 0.08f),
            new Vector3(-2.59f, 0.4f, 0.53f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.3f, 0.08f),
            new Vector3(-1.41f, 0.4f, 0.53f), frameC, true);
        MakeBlock("Chair", root.transform, new Vector3(0.9f, 0.35f, 0.9f),
            new Vector3(-3.3f, 0.425f, -2.2f), frameC, true);
        MakeBlock("ChairBack", root.transform, new Vector3(0.9f, 0.7f, 0.16f),
            new Vector3(-3.3f, 0.8f, -3.1f), frameC, true);
        MakeBlock("LampPole", root.transform, new Vector3(0.06f, 1.4f, 0.06f),
            new Vector3(-4.2f, 0.75f, 1.8f), frameC, true);
        MakeBlock("LampBase", root.transform, new Vector3(0.3f, 0.08f, 0.3f),
            new Vector3(-4.2f, 0.29f, 1.8f), frameC, true);
        MakeBlock("LampShade", root.transform, new Vector3(0.35f, 0.25f, 0.35f),
            new Vector3(-4.2f, 1.6f, 1.8f), goldC, true);
        MakeBlock("LampGlow", root.transform, new Vector3(0.2f, 0.15f, 0.2f),
            new Vector3(-4.2f, 1.43f, 1.8f), amberC, true);

        // ── Kitchen (back-right wall) ──
        MakeBlock("KitchenCounter", root.transform, new Vector3(3f, 0.9f, 0.9f),
            new Vector3(2.5f, 0.7f, 4.5f), frameC, true);
        MakeBlock("CounterTop", root.transform, new Vector3(3.1f, 0.08f, 1f),
            new Vector3(2.5f, 1.2f, 4.5f), new Color(0.75f, 0.62f, 0.45f), true);
        MakeBlock("Sink", root.transform, new Vector3(0.8f, 0.12f, 0.55f),
            new Vector3(2.5f, 1.28f, 4.5f), metalC, true);
        MakeBlock("KitchenPot", root.transform, new Vector3(0.3f, 0.25f, 0.3f),
            new Vector3(1.8f, 1.34f, 4.35f), goldC, true);
        MakeBlock("KitchenJar", root.transform, new Vector3(0.25f, 0.3f, 0.25f),
            new Vector3(3.2f, 1.34f, 4.6f), clothC, true);

        // ── Dining (center-back) ──
        MakeBlock("DiningTable", root.transform, new Vector3(1.8f, 0.08f, 1.1f),
            new Vector3(2.8f, 0.6f, 2.6f), tableC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.35f, 0.09f),
            new Vector3(1.98f, 0.425f, 2.12f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.35f, 0.09f),
            new Vector3(3.62f, 0.425f, 2.12f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.35f, 0.09f),
            new Vector3(1.98f, 0.425f, 3.08f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.35f, 0.09f),
            new Vector3(3.62f, 0.425f, 3.08f), frameC, true);
        MakeBlock("DiningBench", root.transform, new Vector3(1.7f, 0.3f, 0.5f),
            new Vector3(2.8f, 0.4f, 1.9f), tableC, true);
        MakeBlock("DiningBench", root.transform, new Vector3(1.7f, 0.3f, 0.5f),
            new Vector3(2.8f, 0.4f, 3.3f), tableC, true);

        // ── Bookshelf (back-left) ──
        MakeBlock("Bookshelf", root.transform, new Vector3(1.2f, 2.8f, 0.5f),
            new Vector3(-4.3f, 1.65f, 4.5f), frameC, true);
        MakeBlock("BookshelfBoard", root.transform, new Vector3(1.05f, 0.08f, 0.4f),
            new Vector3(-4.3f, 0.7f, 4.5f), shelfC, true);
        MakeBlock("BookshelfBoard", root.transform, new Vector3(1.05f, 0.08f, 0.4f),
            new Vector3(-4.3f, 1.5f, 4.5f), shelfC, true);
        MakeBlock("BookshelfBoard", root.transform, new Vector3(1.05f, 0.08f, 0.4f),
            new Vector3(-4.3f, 2.3f, 4.5f), shelfC, true);
        MakeBlock("Book", root.transform, new Vector3(0.16f, 0.35f, 0.25f),
            new Vector3(-4.75f, 0.92f, 4.5f), bookC, true);
        MakeBlock("Book", root.transform, new Vector3(0.16f, 0.35f, 0.25f),
            new Vector3(-4.55f, 0.92f, 4.5f), goldC, true);
        MakeBlock("Book", root.transform, new Vector3(0.16f, 0.3f, 0.25f),
            new Vector3(-4.75f, 1.72f, 4.5f), tableC, true);
        MakeBlock("Book", root.transform, new Vector3(0.16f, 0.3f, 0.25f),
            new Vector3(-4.55f, 1.72f, 4.5f), clothC, true);

        // ── Wardrobe (front-right) ──
        MakeBlock("Wardrobe", root.transform, new Vector3(0.7f, 2.4f, 2.2f),
            new Vector3(4.45f, 1.6f, -3.9f), frameC, true);
        MakeBlock("WardrobeDoor", root.transform, new Vector3(0.06f, 2.2f, 0.9f),
            new Vector3(4.78f, 1.6f, -4.35f), doorC, true);
        MakeBlock("WardrobeDoor", root.transform, new Vector3(0.06f, 2.2f, 0.9f),
            new Vector3(4.78f, 1.6f, -3.45f), doorC, true);

        // ── Plant (front-left corner) ──
        MakeBlock("PlantPot", root.transform, new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-4.3f, 0.5f, -4.3f), frameC, true);
        MakeBlock("PlantLeaf", root.transform, new Vector3(0.9f, 0.8f, 0.9f),
            new Vector3(-4.3f, 1.1f, -4.3f), leafC, true);
        MakeBlock("PlantFlower", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
            new Vector3(-4.3f, 1.5f, -4.1f), goldC, true);

        // ── Bed ──
        var bed = MakeBlock("Bed", root.transform, new Vector3(2.8f, 0.5f, 1.8f),
            new Vector3(1.2f, 0.5f, -1.8f), new Color(0.608f, 0.216f, 0.216f), true);
        var bedTrigger = bed.AddComponent<BoxCollider>();
        bedTrigger.isTrigger = true;
        bedTrigger.size = new Vector3(1f, 1f, 1f);
        MakeBlock("BedPillow", bed.transform, new Vector3(0.2f, 0.5f, 0.4f),
            new Vector3(-0.4f, 0.7f, 0f), Color.white, true);
        MakeBlock("Headboard", bed.transform, new Vector3(0.1f, 2.2f, 1f),
            new Vector3(-0.55f, 0.5f, 0f), new Color(0.345f, 0.196f, 0.07f), true);

        return root;
    }

}
