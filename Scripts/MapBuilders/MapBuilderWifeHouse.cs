using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  WIFE HOUSE  (14 x 9 x 14, 2-storey, balcony, staircase)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildWifeHouse(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("WifeHouse");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC   = new Color(0.522f, 0.337f, 0.18f);
        Color roofC   = new Color(0.404f, 0.204f, 0.114f);
        Color ridgeC  = new Color(0.345f, 0.11f, 0.039f);
        Color floorC  = new Color(0.447f, 0.263f, 0.157f);
        Color frameC  = new Color(0.165f, 0.094f, 0.031f);
        Color winC    = new Color(0.549f, 0.784f, 0.863f);
        Color stoneC  = new Color(0.439f, 0.4f, 0.361f);
        Color balcC   = new Color(0.325f, 0.208f, 0.114f);
        Color stairC  = new Color(0.455f, 0.353f, 0.2f);
        Color sofaC   = new Color(0.416f, 0.612f, 0.69f);
        Color tableC  = new Color(0.361f, 0.259f, 0.145f);
        Color goldC   = new Color(0.9f, 0.68f, 0.16f);
        Color brickC  = new Color(0.62f, 0.3f, 0.17f);
        Color leafC   = new Color(0.2f, 0.45f, 0.18f);
        Color flowerC = new Color(0.85f, 0.3f, 0.4f);
        Color amberC  = new Color(1f, 0.72f, 0.35f);
        Color rugC    = new Color(0.62f, 0.16f, 0.16f);
        Color bedC    = new Color(0.95f, 0.93f, 0.88f);
        Color stoveC  = new Color(0.12f, 0.12f, 0.12f);
        Color metalC  = new Color(0.55f, 0.55f, 0.57f);

        float h1 = 5f, h2 = 4f;

        // ── 1st floor walls (segmented with window openings) ──
        float w1 = 1.6f;                       // 1F window width (y 1.2..2.8)
        float sillY1 = 0.6f, sillH1 = 1.2f;    // below window
        float headY1 = 3.9f, headH1 = 2.2f;    // above window
        float w2 = 1.6f;                       // 2F window width (y 5.95..7.35)
        float sillY2 = h1 + 0.25f + 0.35f, sillH2 = 0.7f;
        float headY2 = h1 + 0.25f + 4f - 0.95f, headH2 = 1.9f;
        float y1 = h1 / 2f;                    // 1F wall center
        float y2 = h1 + 0.25f + h2 / 2f;       // 2F wall center

        // -Z face, 1F (windows x=±4)
        MakeBlock("Wall1F_Zneg_L", root.transform, new Vector3(2.2f, h1, 0.5f), new Vector3(-5.9f, y1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_M", root.transform, new Vector3(6.4f, h1, 0.5f), new Vector3(0f, y1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_R", root.transform, new Vector3(2.2f, h1, 0.5f), new Vector3(5.9f, y1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W1S", root.transform, new Vector3(w1, sillH1, 0.5f), new Vector3(4f, sillY1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W1H", root.transform, new Vector3(w1, headH1, 0.5f), new Vector3(4f, headY1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W2S", root.transform, new Vector3(w1, sillH1, 0.5f), new Vector3(-4f, sillY1, -7f), wallC);
        MakeBlock("Wall1F_Zneg_W2H", root.transform, new Vector3(w1, headH1, 0.5f), new Vector3(-4f, headY1, -7f), wallC);

        // +Z face, 1F (window x=4)
        MakeBlock("Wall1F_Zpos_L", root.transform, new Vector3(10.2f, h1, 0.5f), new Vector3(-1.9f, y1, 7f), wallC);
        MakeBlock("Wall1F_Zpos_R", root.transform, new Vector3(2.2f, h1, 0.5f), new Vector3(5.9f, y1, 7f), wallC);
        MakeBlock("Wall1F_Zpos_W1S", root.transform, new Vector3(w1, sillH1, 0.5f), new Vector3(4f, sillY1, 7f), wallC);
        MakeBlock("Wall1F_Zpos_W1H", root.transform, new Vector3(w1, headH1, 0.5f), new Vector3(4f, headY1, 7f), wallC);

        // +X face, 1F (window z=-4)
        MakeBlock("Wall1F_Xpos_Back", root.transform, new Vector3(0.5f, h1, 2.2f), new Vector3(7f, y1, -5.9f), wallC);
        MakeBlock("Wall1F_Xpos_Front", root.transform, new Vector3(0.5f, h1, 10.2f), new Vector3(7f, y1, 1.9f), wallC);
        MakeBlock("Wall1F_Xpos_W1S", root.transform, new Vector3(0.5f, sillH1, w1), new Vector3(7f, sillY1, -4f), wallC);
        MakeBlock("Wall1F_Xpos_W1H", root.transform, new Vector3(0.5f, headH1, w1), new Vector3(7f, headY1, -4f), wallC);

        MakeBlock("Floor", root.transform, new Vector3(14f, 0.5f, 14f), Vector3.zero, floorC);

        // ── 2nd floor walls (segmented with window openings) ──
        // -Z face, 2F (windows x=±4)
        MakeBlock("Wall2F_Zneg_L", root.transform, new Vector3(2.2f, h2, 0.5f), new Vector3(-5.9f, y2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_M", root.transform, new Vector3(6.4f, h2, 0.5f), new Vector3(0f, y2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_R", root.transform, new Vector3(2.2f, h2, 0.5f), new Vector3(5.9f, y2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W1S", root.transform, new Vector3(w2, sillH2, 0.5f), new Vector3(4f, sillY2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W1H", root.transform, new Vector3(w2, headH2, 0.5f), new Vector3(4f, headY2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W2S", root.transform, new Vector3(w2, sillH2, 0.5f), new Vector3(-4f, sillY2, -7f), wallC);
        MakeBlock("Wall2F_Zneg_W2H", root.transform, new Vector3(w2, headH2, 0.5f), new Vector3(-4f, headY2, -7f), wallC);

        // +Z face, 2F (window x=4)
        MakeBlock("Wall2F_Zpos_L", root.transform, new Vector3(10.2f, h2, 0.5f), new Vector3(-1.9f, y2, 7f), wallC);
        MakeBlock("Wall2F_Zpos_R", root.transform, new Vector3(2.2f, h2, 0.5f), new Vector3(5.9f, y2, 7f), wallC);
        MakeBlock("Wall2F_Zpos_W1S", root.transform, new Vector3(w2, sillH2, 0.5f), new Vector3(4f, sillY2, 7f), wallC);
        MakeBlock("Wall2F_Zpos_W1H", root.transform, new Vector3(w2, headH2, 0.5f), new Vector3(4f, headY2, 7f), wallC);

        // +X face, 2F (window z=3)
        MakeBlock("Wall2F_Xpos_Back", root.transform, new Vector3(0.5f, h2, 9.2f), new Vector3(7f, y2, -2.4f), wallC);
        MakeBlock("Wall2F_Xpos_Front", root.transform, new Vector3(0.5f, h2, 3.2f), new Vector3(7f, y2, 5.4f), wallC);
        MakeBlock("Wall2F_Xpos_W1S", root.transform, new Vector3(0.5f, sillH2, w2), new Vector3(7f, sillY2, 3f), wallC);
        MakeBlock("Wall2F_Xpos_W1H", root.transform, new Vector3(0.5f, headH2, w2), new Vector3(7f, headY2, 3f), wallC);
        MakeBlock("Floor2F_B", root.transform, new Vector3(10.5f, 0.5f, 14f),
            new Vector3(-1.75f, h1 + 0.25f, 0f), floorC);
        MakeBlock("Floor2F_E", root.transform, new Vector3(1.5f, 0.5f, 14f),
            new Vector3(6.25f, h1 + 0.25f, 0f), floorC);
        MakeBlock("Floor2F_NS", root.transform, new Vector3(2.2f, 0.5f, 8.2f),
            new Vector3(4.5f, h1 + 0.25f, 2.9f), floorC);
        MakeBlock("Floor2F_SS", root.transform, new Vector3(2.2f, 0.5f, 0.6f),
            new Vector3(4.5f, h1 + 0.25f, -6.7f), floorC);
        MakeBlock("Ceiling", root.transform, new Vector3(14f, 0.3f, 14f),
            new Vector3(0f, h1 + h2 + 0.4f, 0f), floorC);

        // ── Open side (-X) with entrance gap ──
        float wallH = h1 + h2 + 0.5f;
        float wallY = wallH / 2f;
        MakeBlock("WallSideL", root.transform, new Vector3(0.5f, wallH, 5.5f),
            new Vector3(-7f, wallY, -4.25f), wallC);
        MakeBlock("WallSideR", root.transform, new Vector3(0.5f, wallH, 5.5f),
            new Vector3(-7f, wallY, 4.25f), wallC);

        // ── Gabled roof ──
        float rise = 3.5f;
        float halfW = 7f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.8f;
        float roofZ = 14f + overhang * 2f;
        float roofY = h1 + h2 + 0.55f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(-halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.7f, 0.35f, roofZ + 0.2f),
            new Vector3(0f, roofY + rise + 0.1f, 0f), ridgeC);

        foreach (float gz in new[] { -7f, 7f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 7; i++)
            {
                float t = (i + 0.5f) / 7f;
                float sw = 14f * (1f - t) + 0.2f;
                float sy = roofY + (i + 0.5f) * rise / 7f;
                float sh = rise / 7f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Roof details: finials, fascia, chimney ──
        MakeBlock("RidgeFinial", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(0f, roofY + rise + 0.55f, 8.95f), goldC);
        MakeBlock("RidgeFinial", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(0f, roofY + rise + 0.55f, -8.95f), goldC);
        MakeBlock("EaveFascia", root.transform, new Vector3(0.25f, 1f, 17.6f),
            new Vector3(7.2f, roofY + 0.15f, 0f), roofC);
        MakeBlock("EaveFascia", root.transform, new Vector3(0.25f, 1f, 17.6f),
            new Vector3(-7.2f, roofY + 0.15f, 0f), roofC);
        MakeBlock("ChimneyCollar", root.transform, new Vector3(2.1f, 0.25f, 2.1f),
            new Vector3(3.5f, 11.7f, 3f), roofC);
        MakeBlock("Chimney", root.transform, new Vector3(1.4f, 1.9f, 1.4f),
            new Vector3(3.5f, 12.15f, 3f), brickC);
        MakeBlock("ChimneyCap", root.transform, new Vector3(1.7f, 0.15f, 1.7f),
            new Vector3(3.5f, 13.3f, 3f), ridgeC);

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(15.5f, 0.5f, 15.5f),
            new Vector3(0f, -0.27f, 0f), stoneC);
        foreach (float cx in new[] { -7.45f, 7.45f })
            foreach (float cz in new[] { -7.45f, 7.45f })
                MakeBlock("FoundationCorner", root.transform, new Vector3(0.6f, 0.25f, 0.6f),
                    new Vector3(cx, -0.05f, cz), stoneC);

        // ── Windows ──
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.6f, 1.6f, 0.14f),
                new Vector3(wx, 2f, -7.03f), winC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.6f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC, true);
            MakeBlock("WinFrame", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC, true);
            AddWindowTrim(root.transform, new Vector3(wx, 2f, -7.03f), 1.6f, 1.6f,
                new Vector3(0f, 0f, -1f), frameC, stoneC, "B1");
        }
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass2F", root.transform, new Vector3(1.6f, 1.4f, 0.14f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), winC, true);
            MakeBlock("WinFrame2F", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC, true);
            MakeBlock("WinFrame2F", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC, true);
            AddWindowTrim(root.transform, new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), 1.6f, 1.4f,
                new Vector3(0f, 0f, -1f), frameC, stoneC, "B2");
        }

        // ── Windows on +Z face ──
        MakeBlock("WinGlassZ1", root.transform, new Vector3(1.6f, 1.6f, 0.14f),
            new Vector3(4f, 2f, 7.03f), winC, true);
        MakeBlock("WinFrameZ1", root.transform, new Vector3(0.1f, 1.6f, 0.16f),
            new Vector3(4f, 2f, 7.03f), frameC, true);
        MakeBlock("WinFrameZ1", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
            new Vector3(4f, 2f, 7.03f), frameC, true);
        AddWindowTrim(root.transform, new Vector3(4f, 2f, 7.03f), 1.6f, 1.6f,
            new Vector3(0f, 0f, 1f), frameC, stoneC, "F1");
        MakeBlock("WinGlassZ2", root.transform, new Vector3(1.6f, 1.4f, 0.14f),
            new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), winC, true);
        MakeBlock("WinFrameZ2", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
            new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), frameC, true);
        MakeBlock("WinFrameZ2", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
            new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), frameC, true);
        AddWindowTrim(root.transform, new Vector3(4f, h1 + 0.25f + 1.4f, 7.03f), 1.6f, 1.4f,
            new Vector3(0f, 0f, 1f), frameC, stoneC, "F2");

        // ── Windows on +X face ──
        MakeBlock("WinGlassX1", root.transform, new Vector3(0.14f, 1.6f, 1.6f),
            new Vector3(7.03f, 2f, -4f), winC, true);
        MakeBlock("WinFrameX1", root.transform, new Vector3(0.16f, 1.6f, 0.1f),
            new Vector3(7.03f, 2f, -4f), frameC, true);
        MakeBlock("WinFrameX1", root.transform, new Vector3(0.16f, 0.1f, 1.6f),
            new Vector3(7.03f, 2f, -4f), frameC, true);
        MakeBlock("WinSillX1", root.transform, new Vector3(0.35f, 0.12f, 2.1f),
            new Vector3(7.33f, 1.14f, -4f), stoneC, true);
        MakeBlock("WinHeadX1", root.transform, new Vector3(0.35f, 0.16f, 2.1f),
            new Vector3(7.33f, 2.88f, -4f), stoneC, true);
        MakeBlock("ShutterLX1", root.transform, new Vector3(0.08f, 1.6f, 0.28f),
            new Vector3(7.23f, 2f, -4.96f), frameC, true);
        MakeBlock("ShutterRX1", root.transform, new Vector3(0.08f, 1.6f, 0.28f),
            new Vector3(7.23f, 2f, -3.04f), frameC, true);
        MakeBlock("WinGlassX2", root.transform, new Vector3(0.14f, 1.4f, 1.6f),
            new Vector3(7.03f, h1 + 0.25f + 1.4f, 3f), winC, true);
        MakeBlock("WinFrameX2", root.transform, new Vector3(0.16f, 1.4f, 0.1f),
            new Vector3(7.03f, h1 + 0.25f + 1.4f, 3f), frameC, true);
        MakeBlock("WinFrameX2", root.transform, new Vector3(0.16f, 0.1f, 1.6f),
            new Vector3(7.03f, h1 + 0.25f + 1.4f, 3f), frameC, true);
        MakeBlock("WinSillX2", root.transform, new Vector3(0.35f, 0.12f, 2.1f),
            new Vector3(7.33f, h1 + 0.25f + 1.4f - (1.4f / 2f + 0.06f), 3f), stoneC, true);
        MakeBlock("WinHeadX2", root.transform, new Vector3(0.35f, 0.16f, 2.1f),
            new Vector3(7.33f, h1 + 0.25f + 1.4f + (1.4f / 2f + 0.08f), 3f), stoneC, true);
        MakeBlock("ShutterLX2", root.transform, new Vector3(0.08f, 1.4f, 0.28f),
            new Vector3(7.23f, h1 + 0.25f + 1.4f, 2.04f), frameC, true);
        MakeBlock("ShutterRX2", root.transform, new Vector3(0.08f, 1.4f, 0.28f),
            new Vector3(7.23f, h1 + 0.25f + 1.4f, 3.96f), frameC, true);

        // ── Entrance on -X side (facing road) ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, -1.6f), frameC, true);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, 1.6f), frameC, true);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.3f, 0.35f, 3.5f),
            new Vector3(-7.04f, 4.75f, 0f), frameC, true);

        // ── Swinging door ──
        var wifeDoorPivot = new GameObject("Door");
        wifeDoorPivot.transform.SetParent(root.transform);
        wifeDoorPivot.transform.localPosition = new Vector3(-7.04f, 2.25f, -1.6f);
        var wifeDoorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wifeDoorPanel.name = "DoorPanel";
        wifeDoorPanel.transform.SetParent(wifeDoorPivot.transform);
        wifeDoorPanel.transform.localPosition = new Vector3(1.5f, 0f, 0f);
        wifeDoorPanel.transform.localScale = new Vector3(3f, 4.5f, 0.3f);
        wifeDoorPanel.GetComponent<MeshRenderer>().material.color = wallC;
        wifeDoorPanel.AddComponent<BoxCollider>();

        // ── Entrance yard ──
        MakeBlock("ThresholdStep", root.transform, new Vector3(1.8f, 0.1f, 3.2f),
            new Vector3(-7.2f, 0.05f, 0f), stoneC);
        MakeBlock("WelcomeMat", root.transform, new Vector3(0.5f, 0.06f, 2.4f),
            new Vector3(-7.15f, 0.11f, 0f), rugC);
        MakeBlock("LintelCrown", root.transform, new Vector3(0.3f, 0.15f, 3.4f),
            new Vector3(-7.04f, 5f, 0f), goldC);
        MakeBlock("Lantern", root.transform, new Vector3(0.32f, 0.5f, 0.32f),
            new Vector3(-7.3f, 3.4f, 2.2f), goldC);
        MakeBlock("LanternGlow", root.transform, new Vector3(0.18f, 0.28f, 0.18f),
            new Vector3(-7.3f, 3.4f, 2.22f), amberC);
        foreach (Vector3 pt in new[]
        {
            new Vector3(-8.2f, 0.02f, 0f),
            new Vector3(-8.8f, 0.02f, 0.35f),
            new Vector3(-9.4f, 0.02f, -0.35f),
            new Vector3(-10f, 0.02f, 0.15f),
            new Vector3(-10.6f, 0.02f, -0.25f),
        })
            MakeBlock("PathTile", root.transform, new Vector3(0.9f, 0.04f, 0.55f), pt, stoneC);
        foreach (float px in new[] { -4f, 4f })
        {
            MakeBlock("Planter", root.transform, new Vector3(1.6f, 0.8f, 0.7f),
                new Vector3(px, 0.55f, -7.5f), frameC);
            foreach (float lx in new[] { -0.6f, 0f, 0.6f })
                MakeBlock("PlanterLeaf", root.transform, new Vector3(0.5f, 0.6f, 0.5f),
                    new Vector3(px + lx, 1.25f, -7.5f), leafC);
            MakeBlock("PlanterFlower", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(px - 0.5f, 1.55f, -7.4f), flowerC);
            MakeBlock("PlanterFlower", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(px + 0.5f, 1.55f, -7.6f), flowerC);
        }

        // ── Balcony (at -X, 2F) ──
        float balcY = h1 + 0.25f;
        MakeBlock("BalconyDeck", root.transform, new Vector3(4f, 0.2f, 8f),
            new Vector3(-8.5f, balcY, 0f), balcC, true);
        for (float bz = -3.5f; bz <= 3.5f; bz += 1f)
        {
            MakeBlock("BalconyRail", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(-8.5f, balcY + 0.7f, bz), frameC, true);
        }
        MakeBlock("BalconyRailing", root.transform, new Vector3(0.08f, 1.2f, 7.6f),
            new Vector3(-9.52f, balcY + 0.7f, 0f), frameC, true);
        MakeBlock("BalconyHandrail", root.transform, new Vector3(0.08f, 0.12f, 8f),
            new Vector3(-8.5f, balcY + 1.2f, 0f), frameC, true);

        // ── Balcony furniture ──
        MakeBlock("BalconyTable", root.transform, new Vector3(0.8f, 0.5f, 0.8f),
            new Vector3(-7.5f, balcY + 0.35f, 2.5f), tableC);
        MakeBlock("BalconyChair", root.transform, new Vector3(0.7f, 0.6f, 0.7f),
            new Vector3(-7.95f, balcY + 0.25f, 2.9f), wallC);
        MakeBlock("BalconyPlantPot", root.transform, new Vector3(0.4f, 0.4f, 0.4f),
            new Vector3(-7.4f, balcY + 0.3f, -2f), frameC);
        MakeBlock("BalconyPlantLeaf", root.transform, new Vector3(0.6f, 0.6f, 0.5f),
            new Vector3(-7.4f, balcY + 0.75f, -2f), leafC);
        MakeBlock("BalconyPlantPot", root.transform, new Vector3(0.4f, 0.4f, 0.4f),
            new Vector3(-9.2f, balcY + 0.3f, 2.8f), frameC);
        MakeBlock("BalconyPlantLeaf", root.transform, new Vector3(0.6f, 0.6f, 0.5f),
            new Vector3(-9.2f, balcY + 0.75f, 2.8f), leafC);

        // ── Staircase (interior, +X side) — straight, auto-walkable (0.3 rise/run) ──
        for (int i = 0; i < 17; i++)
        {
            float sy = 0.25f + (i + 1) * 0.3f - 0.05f;
            float sz = -1.4f - i * 0.3f;
            float th = 0.1f;
            if (i == 0)
            {
                th = 0.3f;
                sy = 0.4f;
            }
            MakeBlock("Stair", root.transform, new Vector3(2f, th, 0.55f),
                new Vector3(4.5f, sy, sz), stairC, false);
        }

        // ── 2F door to balcony ──
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, -1.5f), frameC, true);
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, 1.5f), frameC, true);
        MakeBlock("DoorLintel2F", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(-7.04f, balcY + 3.65f, 0f), frameC, true);

        // ── 1F living set (SW corner, near the entrance) ──
        MakeBlock("Rug", root.transform, new Vector3(3.6f, 0.06f, 2.6f),
            new Vector3(-2.4f, 0.28f, -5.2f), rugC);
        MakeBlock("Sofa", root.transform, new Vector3(3f, 0.35f, 1.1f),
            new Vector3(-3f, 0.425f, -5.9f), sofaC);
        MakeBlock("SofaBack", root.transform, new Vector3(3f, 0.75f, 0.18f),
            new Vector3(-3f, 0.85f, -6.35f), sofaC);
        MakeBlock("SofaArmL", root.transform, new Vector3(0.25f, 0.55f, 1.1f),
            new Vector3(-4.375f, 0.625f, -5.9f), sofaC);
        MakeBlock("SofaArmR", root.transform, new Vector3(0.25f, 0.55f, 1.1f),
            new Vector3(-1.625f, 0.625f, -5.9f), sofaC);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.15f, 0.12f, 0.85f),
            new Vector3(-3.55f, 0.66f, -5.9f), new Color(0.55f, 0.7f, 0.78f), true);
        MakeBlock("SofaCushion", root.transform, new Vector3(1.15f, 0.12f, 0.85f),
            new Vector3(-2.45f, 0.66f, -5.9f), new Color(0.5f, 0.66f, 0.75f), true);
        MakeBlock("TableTop", root.transform, new Vector3(1.3f, 0.08f, 0.75f),
            new Vector3(-3f, 0.58f, -4.1f), tableC);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-3.59f, 0.28f, -4.39f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-2.41f, 0.28f, -4.39f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-3.59f, 0.28f, -3.81f), frameC, true);
        MakeBlock("TableLeg", root.transform, new Vector3(0.08f, 0.45f, 0.08f),
            new Vector3(-2.41f, 0.28f, -3.81f), frameC, true);
        MakeBlock("Chair", root.transform, new Vector3(0.9f, 0.35f, 0.9f),
            new Vector3(-0.9f, 0.425f, -4.4f), wallC);
        MakeBlock("ChairBack", root.transform, new Vector3(0.9f, 0.7f, 0.16f),
            new Vector3(-0.9f, 0.8f, -5.2f), wallC);
        MakeBlock("LampPole", root.transform, new Vector3(0.06f, 1.4f, 0.06f),
            new Vector3(-5.3f, 0.75f, -4.4f), frameC, true);
        MakeBlock("LampBase", root.transform, new Vector3(0.3f, 0.08f, 0.3f),
            new Vector3(-5.3f, 0.29f, -4.4f), frameC, true);
        MakeBlock("LampShade", root.transform, new Vector3(0.35f, 0.25f, 0.35f),
            new Vector3(-5.3f, 1.6f, -4.4f), goldC, true);
        MakeBlock("LampGlow", root.transform, new Vector3(0.2f, 0.15f, 0.2f),
            new Vector3(-5.3f, 1.43f, -4.4f), amberC, true);
        MakeBlock("WallShelf", root.transform, new Vector3(2f, 0.12f, 0.6f),
            new Vector3(-3f, 3.2f, -6.45f), frameC, true);
        MakeBlock("PlantPot", root.transform, new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-5.8f, 0.5f, -6f), frameC);
        MakeBlock("PlantLeaf", root.transform, new Vector3(0.9f, 0.8f, 0.9f),
            new Vector3(-5.8f, 1.1f, -6f), leafC, true);
        MakeBlock("PlantFlower", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
            new Vector3(-5.8f, 1.5f, -5.8f), flowerC, true);

        // ── 1F kitchen + dining (east side) ──
        MakeBlock("KitchenCounter", root.transform, new Vector3(3.4f, 0.9f, 0.9f),
            new Vector3(1.5f, 0.7f, 5.7f), frameC);
        MakeBlock("CounterTop", root.transform, new Vector3(3.5f, 0.08f, 1f),
            new Vector3(1.5f, 1.2f, 5.7f), new Color(0.75f, 0.62f, 0.45f), true);
        MakeBlock("Sink", root.transform, new Vector3(0.8f, 0.12f, 0.55f),
            new Vector3(1.5f, 1.28f, 5.7f), metalC, true);
        MakeBlock("Stove", root.transform, new Vector3(1.5f, 0.9f, 0.9f),
            new Vector3(4.6f, 0.7f, 5.7f), stoveC);
        MakeBlock("StoveBurner", root.transform, new Vector3(0.45f, 0.06f, 0.45f),
            new Vector3(4.15f, 1.2f, 5.4f), metalC, true);
        MakeBlock("StoveBurner", root.transform, new Vector3(0.45f, 0.06f, 0.45f),
            new Vector3(4.15f, 1.2f, 6f), metalC, true);
        MakeBlock("StoveDoor", root.transform, new Vector3(1.1f, 0.35f, 0.06f),
            new Vector3(4.6f, 0.55f, 6.17f), new Color(0.18f, 0.18f, 0.2f), true);
        MakeBlock("KitchenShelf", root.transform, new Vector3(2.4f, 0.12f, 0.6f),
            new Vector3(2f, 3.2f, 6.45f), frameC, true);
        MakeBlock("DiningTable", root.transform, new Vector3(1.8f, 0.08f, 1.1f),
            new Vector3(1.5f, 0.6f, 2.6f), tableC);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(0.68f, 0.35f, 2.12f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(2.32f, 0.35f, 2.12f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(0.68f, 0.35f, 3.08f), frameC, true);
        MakeBlock("DiningTableLeg", root.transform, new Vector3(0.09f, 0.4f, 0.09f),
            new Vector3(2.32f, 0.35f, 3.08f), frameC, true);
        MakeBlock("DiningBench", root.transform, new Vector3(1.7f, 0.3f, 0.5f),
            new Vector3(1.5f, 0.4f, 1.9f), tableC);
        MakeBlock("DiningBench", root.transform, new Vector3(1.7f, 0.3f, 0.5f),
            new Vector3(1.5f, 0.4f, 3.3f), tableC);
        MakeBlock("LampCord", root.transform, new Vector3(0.06f, 0.6f, 0.06f),
            new Vector3(0f, 4.9f, 0.5f), frameC, true);
        MakeBlock("LampShade", root.transform, new Vector3(0.35f, 0.25f, 0.35f),
            new Vector3(0f, 4.6f, 0.5f), goldC, true);
        MakeBlock("LampGlow", root.transform, new Vector3(0.2f, 0.15f, 0.2f),
            new Vector3(0f, 4.42f, 0.5f), amberC, true);

        // ── 2F bedroom (matching stairwell opening on the NE) ──
        MakeBlock("BedFrame", root.transform, new Vector3(3f, 0.3f, 2.2f),
            new Vector3(-1f, 5.65f, 5.55f), frameC);
        MakeBlock("BedMattress", root.transform, new Vector3(2.8f, 0.3f, 2f),
            new Vector3(-1f, 5.95f, 5.55f), bedC);
        MakeBlock("BedBlanket", root.transform, new Vector3(2.8f, 0.1f, 1.6f),
            new Vector3(-1f, 6.2f, 5.25f), new Color(0.65f, 0.3f, 0.45f), true);
        MakeBlock("BedPillow", root.transform, new Vector3(1.1f, 0.18f, 0.45f),
            new Vector3(-1.5f, 6.2f, 6.3f), bedC, true);
        MakeBlock("BedPillow", root.transform, new Vector3(1.1f, 0.18f, 0.45f),
            new Vector3(-0.5f, 6.2f, 6.3f), bedC, true);
        MakeBlock("BedHeadboard", root.transform, new Vector3(3.2f, 1.2f, 0.15f),
            new Vector3(-1f, 6.4f, 6.62f), frameC);
        MakeBlock("BedFootboard", root.transform, new Vector3(2.4f, 0.6f, 0.12f),
            new Vector3(-1f, 6f, 4.42f), frameC, true);
        MakeBlock("Nightstand", root.transform, new Vector3(0.7f, 0.7f, 0.55f),
            new Vector3(-2.6f, 5.85f, 6.3f), tableC);
        MakeBlock("Nightstand", root.transform, new Vector3(0.7f, 0.7f, 0.55f),
            new Vector3(0.6f, 5.85f, 6.3f), tableC);
        MakeBlock("NightLamp", root.transform, new Vector3(0.28f, 0.4f, 0.28f),
            new Vector3(-2.6f, 6.4f, 6.3f), goldC, true);
        MakeBlock("NightLampGlow", root.transform, new Vector3(0.18f, 0.2f, 0.18f),
            new Vector3(-2.6f, 6.45f, 6.3f), amberC, true);
        MakeBlock("NightLamp", root.transform, new Vector3(0.28f, 0.4f, 0.28f),
            new Vector3(0.6f, 6.4f, 6.3f), goldC, true);
        MakeBlock("NightLampGlow", root.transform, new Vector3(0.18f, 0.2f, 0.18f),
            new Vector3(0.6f, 6.45f, 6.3f), amberC, true);
        MakeBlock("Wardrobe", root.transform, new Vector3(2.2f, 2.4f, 0.7f),
            new Vector3(-5.5f, 6.7f, -6.55f), frameC);
        MakeBlock("WardrobeDoor", root.transform, new Vector3(0.9f, 2.2f, 0.06f),
            new Vector3(-5.85f, 6.7f, -6.2f), new Color(0.45f, 0.3f, 0.15f), true);
        MakeBlock("WardrobeDoor", root.transform, new Vector3(0.9f, 2.2f, 0.06f),
            new Vector3(-5.15f, 6.7f, -6.2f), new Color(0.45f, 0.3f, 0.15f), true);
        MakeBlock("ReadingChair", root.transform, new Vector3(0.9f, 0.35f, 0.9f),
            new Vector3(4.8f, 5.85f, 2.5f), sofaC);
        MakeBlock("ReadingChairBack", root.transform, new Vector3(0.16f, 0.7f, 0.9f),
            new Vector3(5.42f, 6.2f, 2.5f), sofaC, true);
        MakeBlock("SideTable", root.transform, new Vector3(0.5f, 0.08f, 0.5f),
            new Vector3(4.2f, 6.2f, 3.1f), tableC, true);
        MakeBlock("SideTableLeg", root.transform, new Vector3(0.06f, 0.4f, 0.06f),
            new Vector3(4.2f, 5.97f, 3.1f), frameC, true);
        MakeBlock("Rug2F", root.transform, new Vector3(3.5f, 0.06f, 2.5f),
            new Vector3(-1f, 5.53f, 4f), rugC);
        MakeBlock("LampCord2F", root.transform, new Vector3(0.06f, 0.6f, 0.06f),
            new Vector3(0f, 8.95f, 0f), frameC, true);
        MakeBlock("LampShade2F", root.transform, new Vector3(0.4f, 0.25f, 0.4f),
            new Vector3(0f, 8.55f, 0f), goldC, true);
        MakeBlock("LampGlow2F", root.transform, new Vector3(0.22f, 0.16f, 0.22f),
            new Vector3(0f, 8.4f, 0f), amberC, true);

        return root;
    }

}
