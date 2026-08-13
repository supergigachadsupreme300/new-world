using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  RICH MAN MANSION
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildRichManMansion(Transform parent, Vector3 position, float scale = 1f, Quaternion rotation = default)
    {
        var root = new GameObject("RichManMansion");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;
        root.transform.localScale = Vector3.one * scale;

        Color wallC    = new Color(0.94f, 0.9f, 0.82f);
        Color trimC    = new Color(0.28f, 0.18f, 0.1f);
        Color roofC    = new Color(0.22f, 0.22f, 0.26f);
        Color goldC    = new Color(0.9f, 0.68f, 0.16f);
        Color stoneC   = new Color(0.65f, 0.62f, 0.58f);
        Color winC     = new Color(0.55f, 0.78f, 0.86f);
        Color leafC    = new Color(0.2f, 0.45f, 0.18f);
        Color waterC   = new Color(0.3f, 0.6f, 0.9f);

        float h1 = 5f, h2 = 4f;
        float halfW = 9f, halfD = 8f;
        float y1 = h1 / 2f;
        float y2 = h1 + 0.25f + h2 / 2f;

        // ── Foundation & floors ──
        MakeBlock("MansionFoundation", root.transform, new Vector3(halfW * 2f + 1.4f, 0.55f, halfD * 2f + 1.4f),
            new Vector3(0f, -0.28f, 0f), stoneC);
        MakeBlock("Floor1F", root.transform, new Vector3(halfW * 2f, 0.5f, halfD * 2f), Vector3.zero, stoneC);
        MakeBlock("Floor2F", root.transform, new Vector3(halfW * 2f, 0.5f, halfD * 2f),
            new Vector3(0f, h1 + 0.25f, 0f), stoneC);
        MakeBlock("Ceiling", root.transform, new Vector3(halfW * 2f, 0.3f, halfD * 2f),
            new Vector3(0f, h1 + h2 + 0.4f, 0f), stoneC);

        // ── 1F walls ──
        MakeBlock("Wall1F_Zneg", root.transform, new Vector3(halfW * 2f, h1, 0.5f), new Vector3(0f, y1, -halfD), wallC);
        MakeBlock("Wall1F_Zpos", root.transform, new Vector3(halfW * 2f, h1, 0.5f), new Vector3(0f, y1, halfD), wallC);
        MakeBlock("Wall1F_Xpos", root.transform, new Vector3(0.5f, h1, halfD * 2f), new Vector3(halfW, y1, 0f), wallC);
        MakeBlock("Wall1F_Xneg_N", root.transform, new Vector3(0.5f, h1, 5.8f), new Vector3(-halfW, y1, -5.1f), wallC);
        MakeBlock("Wall1F_Xneg_S", root.transform, new Vector3(0.5f, h1, 5.8f), new Vector3(-halfW, y1, 5.1f), wallC);

        // 1F windows (-Z, +Z, +X faces)
        foreach (float wx in new[] { -5f, 5f })
        {
            MakeBlock("Win1F_Zneg", root.transform, new Vector3(1.8f, 1.8f, 0.14f), new Vector3(wx, 2f, -halfD - 0.03f), winC, true);
            MakeBlock("WinFrame1F_Zneg", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 1.12f, -halfD - 0.03f), trimC, true);
            MakeBlock("WinFrame1F_Zneg", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 2.94f, -halfD - 0.03f), trimC, true);
            MakeBlock("Win1F_Zpos", root.transform, new Vector3(1.8f, 1.8f, 0.14f), new Vector3(wx, 2f, halfD + 0.03f), winC, true);
            MakeBlock("WinFrame1F_Zpos", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 1.12f, halfD + 0.03f), trimC, true);
            MakeBlock("WinFrame1F_Zpos", root.transform, new Vector3(2f, 0.12f, 0.16f), new Vector3(wx, 2.94f, halfD + 0.03f), trimC, true);
        }
        foreach (float wz in new[] { -4f, 4f })
        {
            MakeBlock("Win1F_Xpos", root.transform, new Vector3(0.14f, 1.8f, 1.8f), new Vector3(halfW + 0.03f, 2f, wz), winC, true);
            MakeBlock("WinFrame1F_Xpos", root.transform, new Vector3(0.16f, 0.12f, 2f), new Vector3(halfW + 0.03f, 1.12f, wz), trimC, true);
            MakeBlock("WinFrame1F_Xpos", root.transform, new Vector3(0.16f, 0.12f, 2f), new Vector3(halfW + 0.03f, 2.94f, wz), trimC, true);
            MakeBlock("Win1F_Xneg", root.transform, new Vector3(0.14f, 1.5f, 1.5f), new Vector3(-halfW - 0.03f, 2f, wz), winC, true);
            MakeBlock("WinFrame1F_Xneg", root.transform, new Vector3(0.16f, 0.12f, 1.7f), new Vector3(-halfW - 0.03f, 1.24f, wz), trimC, true);
            MakeBlock("WinFrame1F_Xneg", root.transform, new Vector3(0.16f, 0.12f, 1.7f), new Vector3(-halfW - 0.03f, 2.81f, wz), trimC, true);
        }

        // ── Cornice band at the 1F/2F junction (all four sides) ──
        MakeBlock("CorniceZN", root.transform, new Vector3(19.2f, 0.3f, 0.55f), new Vector3(0f, 5.55f, -8.18f), stoneC);
        MakeBlock("CorniceZNGold", root.transform, new Vector3(19.2f, 0.1f, 0.6f), new Vector3(0f, 5.72f, -8.18f), goldC, true);
        MakeBlock("CorniceZP", root.transform, new Vector3(19.2f, 0.3f, 0.55f), new Vector3(0f, 5.55f, 8.18f), stoneC);
        MakeBlock("CorniceZPGold", root.transform, new Vector3(19.2f, 0.1f, 0.6f), new Vector3(0f, 5.72f, 8.18f), goldC, true);
        MakeBlock("CorniceXN", root.transform, new Vector3(0.55f, 0.3f, 16.4f), new Vector3(-8.95f, 5.55f, 0f), stoneC);
        MakeBlock("CorniceXNGold", root.transform, new Vector3(0.6f, 0.1f, 16.4f), new Vector3(-8.95f, 5.72f, 0f), goldC, true);
        MakeBlock("CorniceXP", root.transform, new Vector3(0.55f, 0.3f, 16.4f), new Vector3(8.95f, 5.55f, 0f), stoneC);
        MakeBlock("CorniceXPGold", root.transform, new Vector3(0.6f, 0.1f, 16.4f), new Vector3(8.95f, 5.72f, 0f), goldC, true);

        // ── Entrance on -X side (double door + gold frame) ──
        MakeBlock("DoorFrameL", root.transform, new Vector3(0.4f, 4.5f, 0.4f), new Vector3(-halfW - 0.1f, 2.25f, -1.9f), goldC);
        MakeBlock("DoorFrameR", root.transform, new Vector3(0.4f, 4.5f, 0.4f), new Vector3(-halfW - 0.1f, 2.25f, 1.9f), goldC);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.4f, 0.5f, 4.4f), new Vector3(-halfW - 0.1f, 4.75f, 0f), goldC);
        MakeBlock("DoorPanelL", root.transform, new Vector3(0.14f, 3.6f, 1.7f), new Vector3(-halfW - 0.2f, 2.25f, -0.85f), goldC, true);
        MakeBlock("DoorPanelR", root.transform, new Vector3(0.14f, 3.6f, 1.7f), new Vector3(-halfW - 0.2f, 2.25f, 0.85f), goldC, true);
        MakeBlock("DoorKnobL", root.transform, new Vector3(0.16f, 0.16f, 0.16f), new Vector3(-halfW - 0.28f, 2.2f, -0.55f), trimC, true);
        MakeBlock("DoorKnobR", root.transform, new Vector3(0.16f, 0.16f, 0.16f), new Vector3(-halfW - 0.28f, 2.2f, 0.55f), trimC, true);
        MakeBlock("DoorStep", root.transform, new Vector3(7f, 0.3f, 1f), new Vector3(-halfW - 0.5f, 0.15f, 0f), stoneC);
        MakeBlock("DoorStepGold", root.transform, new Vector3(7f, 0.08f, 0.14f), new Vector3(-halfW - 0.52f, 0.42f, -0.5f), goldC, true);
        MakeBlock("DoorStep2", root.transform, new Vector3(5f, 0.25f, 0.65f), new Vector3(-halfW - 1f, 0.125f, 0f), stoneC);
        MakeBlock("DoorStep3", root.transform, new Vector3(3.4f, 0.2f, 0.45f), new Vector3(-halfW - 1.45f, 0.1f, 0f), stoneC);

        // ── 2F walls ──
        MakeBlock("Wall2F_Zneg", root.transform, new Vector3(halfW * 2f, h2, 0.5f), new Vector3(0f, y2, -halfD), wallC);
        MakeBlock("Wall2F_Zpos", root.transform, new Vector3(halfW * 2f, h2, 0.5f), new Vector3(0f, y2, halfD), wallC);
        MakeBlock("Wall2F_Xpos", root.transform, new Vector3(0.5f, h2, halfD * 2f), new Vector3(halfW, y2, 0f), wallC);
        MakeBlock("Wall2F_Xneg", root.transform, new Vector3(0.5f, h2, halfD * 2f), new Vector3(-halfW, y2, 0f), wallC);

        // 2F windows
        foreach (float wx in new[] { -5f, 5f })
        {
            MakeBlock("Win2F_Zneg", root.transform, new Vector3(1.8f, 1.4f, 0.14f), new Vector3(wx, y2, -halfD - 0.03f), winC, true);
            MakeBlock("Win2F_Zpos", root.transform, new Vector3(1.8f, 1.4f, 0.14f), new Vector3(wx, y2, halfD + 0.03f), winC, true);
        }
        foreach (float wz in new[] { -4f, 4f })
        {
            MakeBlock("Win2F_Xpos", root.transform, new Vector3(0.14f, 1.4f, 1.8f), new Vector3(halfW + 0.03f, y2, wz), winC, true);
            MakeBlock("Win2F_Xneg", root.transform, new Vector3(0.14f, 1.4f, 1.8f), new Vector3(-halfW - 0.03f, y2, wz), winC, true);
        }

        // 2F window trims: sill/header (reuse AddWindowTrim for +/-Z, manual for +/-X)
        foreach (float wx in new[] { -5f, 5f })
        {
            AddWindowTrim(root.transform, new Vector3(wx, y2, -halfD - 0.05f), 1.8f, 1.4f, -Vector3.forward, trimC, stoneC, "2F_ZN_" + (wx < 0 ? "A" : "B"));
            AddWindowTrim(root.transform, new Vector3(wx, y2, halfD + 0.05f), 1.8f, 1.4f, Vector3.forward, trimC, stoneC, "2F_ZP_" + (wx < 0 ? "A" : "B"));
        }
        foreach (float wz in new[] { -4f, 4f })
        {
            string tag = wz < 0 ? "A" : "B";
            MakeBlock("WinSill2F_XP" + tag, root.transform, new Vector3(0.45f, 0.12f, 2f), new Vector3(halfW + 0.02f, y2 - 0.76f, wz), stoneC, true);
            MakeBlock("WinHead2F_XP" + tag, root.transform, new Vector3(0.45f, 0.16f, 2f), new Vector3(halfW + 0.02f, y2 + 0.78f, wz), stoneC, true);
            MakeBlock("Shutter2F_XPL" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(halfW + 0.15f, y2, wz - 1.06f), trimC, true);
            MakeBlock("Shutter2F_XPR" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(halfW + 0.15f, y2, wz + 1.06f), trimC, true);
            MakeBlock("WinSill2F_XN" + tag, root.transform, new Vector3(0.45f, 0.12f, 2f), new Vector3(-halfW - 0.02f, y2 - 0.76f, wz), stoneC, true);
            MakeBlock("WinHead2F_XN" + tag, root.transform, new Vector3(0.45f, 0.16f, 2f), new Vector3(-halfW - 0.02f, y2 + 0.78f, wz), stoneC, true);
            MakeBlock("Shutter2F_XNL" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(-halfW - 0.15f, y2, wz - 1.06f), trimC, true);
            MakeBlock("Shutter2F_XNR" + tag, root.transform, new Vector3(0.12f, 1.4f, 0.28f), new Vector3(-halfW - 0.15f, y2, wz + 1.06f), trimC, true);
        }

        // ── Entrance-face 2F window (above the double door, gold frame + pediment) ──
        MakeBlock("Win2F_Entr", root.transform, new Vector3(0.14f, 2.6f, 1.9f), new Vector3(-halfW - 0.27f, 7.2f, 0f), winC, true);
        MakeBlock("Win2F_EntrJambL", root.transform, new Vector3(0.18f, 3f, 0.28f), new Vector3(-halfW - 0.15f, 7.2f, -1f), goldC);
        MakeBlock("Win2F_EntrJambR", root.transform, new Vector3(0.18f, 3f, 0.28f), new Vector3(-halfW - 0.15f, 7.2f, 1f), goldC);
        MakeBlock("Win2F_EntrHead", root.transform, new Vector3(0.18f, 0.28f, 2.4f), new Vector3(-halfW - 0.15f, 8.72f, 0f), goldC);
        MakeBlock("Win2F_EntrSill", root.transform, new Vector3(0.34f, 0.22f, 2.3f), new Vector3(-halfW - 0.3f, 5.6f, 0f), goldC);
        MakeBlock("Win2F_EntrPedBase", root.transform, new Vector3(0.2f, 0.22f, 2.7f), new Vector3(-halfW - 0.12f, 8.95f, 0f), goldC);
        MakeBlock("Win2F_EntrPed1", root.transform, new Vector3(0.2f, 0.3f, 2f), new Vector3(-halfW - 0.14f, 9.15f, 0f), goldC);
        MakeBlock("Win2F_EntrPed2", root.transform, new Vector3(0.2f, 0.3f, 1.3f), new Vector3(-halfW - 0.14f, 9.5f, 0f), goldC);
        MakeBlock("Win2F_EntrPed3", root.transform, new Vector3(0.2f, 0.3f, 0.6f), new Vector3(-halfW - 0.14f, 9.85f, 0f), goldC);
        MakeBlock("Win2F_EntrFinial", root.transform, new Vector3(0.22f, 0.25f, 0.22f), new Vector3(-halfW - 0.14f, 10.12f, 0f), goldC, true);

        // ── Balcony balustrade on the entrance face (posts + rails between the porch columns) ──
        MakeBlock("BalconySlab", root.transform, new Vector3(0.6f, 0.2f, 5f), new Vector3(-halfW - 0.32f, 5.85f, 0f), stoneC);
        MakeBlock("BalconyBase", root.transform, new Vector3(0.34f, 0.1f, 5f), new Vector3(-halfW - 0.4f, 5.72f, 0f), stoneC);
        MakeBlock("BalconyRailBot", root.transform, new Vector3(0.16f, 0.12f, 4.8f), new Vector3(-halfW - 0.48f, 6.02f, 0f), trimC, true);
        MakeBlock("BalconyRailTop", root.transform, new Vector3(0.16f, 0.14f, 4.8f), new Vector3(-halfW - 0.5f, 6.66f, 0f), goldC, true);
        MakeBlock("BalconyRailGold", root.transform, new Vector3(0.2f, 0.08f, 4.8f), new Vector3(-halfW - 0.52f, 6.76f, 0f), goldC, true);
        for (int b = 0; b < 7; b++)
        {
            float bz = -2.25f + b * 0.75f;
            MakeBlock("Baluster" + b, root.transform, new Vector3(0.12f, 0.72f, 0.12f), new Vector3(-halfW - 0.5f, 6.36f, bz), trimC, true);
        }

        MakeBlock("GoldTrimLine", root.transform, new Vector3(halfW * 2f + 0.1f, 0.18f, 0.3f), new Vector3(0f, h1 + 0.25f, -halfD - 0.28f), goldC, true);
        MakeBlock("GoldTrimLine", root.transform, new Vector3(halfW * 2f + 0.1f, 0.18f, 0.3f), new Vector3(0f, h1 + 0.25f, halfD + 0.28f), goldC, true);

        // ── Portico (columns + pediment) on -X side ──
        MakeBlock("PorchSlab", root.transform, new Vector3(7f, 0.3f, 9f), new Vector3(-halfW - 1.2f, 0.15f, 0f), stoneC);
        MakeBlock("PorchColumnL", root.transform, new Vector3(0.55f, h1 + h2 + 0.9f, 0.55f), new Vector3(-halfW - 1.6f, (h1 + h2 + 0.9f) / 2f, -3f), wallC);
        MakeBlock("PorchColumnR", root.transform, new Vector3(0.55f, h1 + h2 + 0.9f, 0.55f), new Vector3(-halfW - 1.6f, (h1 + h2 + 0.9f) / 2f, 3f), wallC);
        MakeBlock("ColumnCapL", root.transform, new Vector3(0.85f, 0.25f, 0.85f), new Vector3(-halfW - 1.6f, h1 + h2 + 0.9f, -3f), goldC, true);
        MakeBlock("ColumnCapR", root.transform, new Vector3(0.85f, 0.25f, 0.85f), new Vector3(-halfW - 1.6f, h1 + h2 + 0.9f, 3f), goldC, true);
        MakeBlock("PorchRoof", root.transform, new Vector3(6.5f, 0.35f, 8.5f), new Vector3(-halfW - 1.6f, h1 + h2 + 1.1f, 0f), roofC);
        MakeBlock("PorchRoofGold", root.transform, new Vector3(6.9f, 0.2f, 8.9f), new Vector3(-halfW - 1.6f, h1 + h2 + 1.28f, 0f), goldC, true);

        // ── Gabled roof (ridge along Z, gold ridge) ──
        float rise = 3.5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float roofZ = halfD * 2f + 3.6f;
        float roofY = h1 + h2 + 0.55f;

        MakeBlock("MansionRoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("MansionRoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(-halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("MansionRidge", root.transform, new Vector3(0.7f, 0.35f, roofZ + 0.2f),
            new Vector3(0f, roofY + rise + 0.1f, 0f), goldC);
        MakeBlock("RidgeFinialN", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(0f, roofY + rise + 0.55f, halfD + 1.8f), goldC, true);
        MakeBlock("RidgeFinialS", root.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(0f, roofY + rise + 0.55f, -halfD - 1.8f), goldC, true);
        MakeBlock("MansionRidgeTrim", root.transform, new Vector3(0.55f, 0.3f, roofZ + 0.4f),
            new Vector3(0f, roofY + rise + 0.35f, 0f), goldC, true);
        MakeBlock("FasciaXP", root.transform, new Vector3(0.35f, 0.3f, roofZ + 0.4f),
            new Vector3(halfW + 0.03f, roofY + 0.07f, 0f), goldC, true);
        MakeBlock("FasciaXN", root.transform, new Vector3(0.35f, 0.3f, roofZ + 0.4f),
            new Vector3(-halfW - 0.03f, roofY + 0.07f, 0f), goldC, true);

        foreach (float gz in new[] { -halfD, halfD })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 7; i++)
            {
                float t = (i + 0.5f) / 7f;
                float sw = halfW * 2f * (1f - t) + 0.2f;
                float sy = roofY + (i + 0.5f) * rise / 7f;
                float sh = rise / 7f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Chimney ──
        MakeBlock("ChimneyCollar", root.transform, new Vector3(2.1f, 0.25f, 2.1f), new Vector3(4f, 11.7f, 4f), roofC);
        MakeBlock("Chimney", root.transform, new Vector3(1.4f, 1.9f, 1.4f), new Vector3(4f, 12.15f, 4f), trimC);
        MakeBlock("ChimneyCap", root.transform, new Vector3(1.7f, 0.15f, 1.7f), new Vector3(4f, 13.3f, 4f), stoneC);

        // ── Front yard: hedges, statues, straight walk ──
        MakeBlock("HedgeL", root.transform, new Vector3(2.4f, 1f, 1.3f), new Vector3(-halfW - 2.6f, 0.5f, -5.2f), leafC);
        MakeBlock("HedgeR", root.transform, new Vector3(2.4f, 1f, 1.3f), new Vector3(-halfW - 2.6f, 0.5f, 5.2f), leafC);
        MakeBlock("GoldStatueL", root.transform, new Vector3(0.5f, 1.3f, 0.5f), new Vector3(-halfW - 1.7f, 0.65f, -4.6f), goldC, true);
        MakeBlock("GoldStatueR", root.transform, new Vector3(0.5f, 1.3f, 0.5f), new Vector3(-halfW - 1.7f, 0.65f, 4.6f), goldC, true);
        MakeBlock("MansionWalk", root.transform, new Vector3(13.8f, 0.12f, 3.2f), new Vector3(-halfW - 11.1f, 0.06f, 0f), stoneC);

        // ── Big fountain on a stone pad (north of the walk) ──
        MakeBlock("FountainPad", root.transform, new Vector3(6.4f, 0.15f, 6.4f), new Vector3(-halfW - 8.5f, 0.075f, -5.6f), stoneC);
        MakeBlock("FountainBase", root.transform, new Vector3(3.8f, 0.45f, 3.8f), new Vector3(-halfW - 8.5f, 0.225f, -5.6f), stoneC);
        MakeBlock("FountainRim", root.transform, new Vector3(4.4f, 0.3f, 4.4f), new Vector3(-halfW - 8.5f, 0.6f, -5.6f), stoneC, true);
        MakeBlock("FountainRimTop", root.transform, new Vector3(4.4f, 0.12f, 4.4f), new Vector3(-halfW - 8.5f, 0.78f, -5.6f), stoneC, true);
        MakeBlock("FountainWater", root.transform, new Vector3(3.7f, 0.1f, 3.7f), new Vector3(-halfW - 8.5f, 0.76f, -5.6f), waterC, true);
        MakeBlock("FountainPedestal", root.transform, new Vector3(1f, 1.3f, 1f), new Vector3(-halfW - 8.5f, 1.35f, -5.6f), stoneC, true);
        MakeBlock("FountainJet", root.transform, new Vector3(0.3f, 0.7f, 0.3f), new Vector3(-halfW - 8.5f, 2.05f, -5.6f), waterC, true);
        MakeBlock("FountainStatue", root.transform, new Vector3(0.5f, 0.7f, 0.5f), new Vector3(-halfW - 8.5f, 2.35f, -5.6f), goldC, true);
        foreach (int cs in new[] { -1, 1 })
        {
            foreach (int cz in new[] { -1, 1 })
            {
                MakeBlock("FountainFinial" + (cs < 0 ? "A" : "B") + (cz < 0 ? "A" : "B"),
                    root.transform, new Vector3(0.16f, 0.16f, 0.16f),
                    new Vector3(-halfW - 8.5f + cs * 1.9f, 0.86f, -5.6f + cz * 1.9f), goldC, true);
            }
        }

        // ── Lampposts flanking the walk near the porch ──
        foreach (int side in new[] { -1, 1 })
        {
            string tag = side < 0 ? "L" : "R";
            MakeBlock("LampPost" + tag, root.transform, new Vector3(0.16f, 3.4f, 0.16f), new Vector3(-halfW - 4.5f, 1.7f, side * 3.1f), trimC, true);
            MakeBlock("LampHead" + tag, root.transform, new Vector3(0.55f, 0.35f, 0.55f), new Vector3(-halfW - 4.5f, 3.4f, side * 3.1f), goldC, true);
            MakeBlock("LampGlow" + tag, root.transform, new Vector3(0.4f, 0.22f, 0.4f), new Vector3(-halfW - 4.5f, 3.45f, side * 3.1f), new Color(1f, 0.95f, 0.7f), true);
        }

        // ── Cypress trees: gate flankers, fence corners, behind-house pair ──
        BuildCypress(root.transform, new Vector3(-halfW - 17f, 0f, -3.6f), 0.9f, "NW2", leafC, trimC);
        BuildCypress(root.transform, new Vector3(-halfW - 17f, 0f, 3.6f), 0.9f, "SW2", leafC, trimC);
        BuildCypress(root.transform, new Vector3(-halfW - 17.4f, 0f, -9f), 1f, "NW", leafC, trimC);
        BuildCypress(root.transform, new Vector3(-halfW - 17.4f, 0f, 9f), 1f, "SW", leafC, trimC);
        BuildCypress(root.transform, new Vector3(halfW + 2.6f, 0f, -5.5f), 1f, "NE", leafC, trimC);
        BuildCypress(root.transform, new Vector3(halfW + 2.6f, 0f, 5.5f), 1f, "SE", leafC, trimC);

        // ── Flower beds flanking the walk ──
        Color[] flowerC = { new Color(0.85f, 0.2f, 0.2f), new Color(0.95f, 0.85f, 0.15f), new Color(0.6f, 0.3f, 0.8f) };
        int fi = 0;
        foreach (float fz in new[] { -2.4f, 2.4f })
        {
            foreach (float fx in new[] { -halfW - 5f, -halfW - 9f, -halfW - 13f })
            {
                MakeBlock("FlowerBed", root.transform, new Vector3(0.8f, 0.25f, 0.8f), new Vector3(fx, 0.125f, fz), leafC);
                MakeBlock("Flower", root.transform, new Vector3(0.35f, 0.28f, 0.35f), new Vector3(fx, 0.28f, fz), flowerC[fi++ % flowerC.Length], true);
            }
        }

        // ── Garden fence along the west edge, split for the gate ──
        Color fenceC = new Color(0.18f, 0.32f, 0.15f);
        foreach (int side in new[] { -1, 1 })
        {
            string tag = side < 0 ? "S" : "N";
            float zc = side * 5.65f;
            MakeBlock("FenceTopRail" + tag, root.transform, new Vector3(0.18f, 0.12f, 7.7f), new Vector3(-halfW - 18f, 1.15f, zc), fenceC, true);
            MakeBlock("FenceBottomRail" + tag, root.transform, new Vector3(0.18f, 0.1f, 7.7f), new Vector3(-halfW - 18f, 0.5f, zc), fenceC, true);
            for (int p = 0; p < 5; p++)
            {
                float pz = side * (2.6f + p * 1.8f);
                MakeBlock("FencePost" + tag + p, root.transform, new Vector3(0.22f, 1.5f, 0.22f), new Vector3(-halfW - 18.05f, 0.75f, pz), fenceC, true);
            }
        }
        MakeBlock("GatePostL", root.transform, new Vector3(0.3f, 2.2f, 0.3f), new Vector3(-halfW - 18f, 1.1f, -1.9f), fenceC, true);
        MakeBlock("GatePostR", root.transform, new Vector3(0.3f, 2.2f, 0.3f), new Vector3(-halfW - 18f, 1.1f, 1.9f), fenceC, true);
        MakeBlock("GateCapL", root.transform, new Vector3(0.38f, 0.14f, 0.38f), new Vector3(-halfW - 18f, 2.22f, -1.9f), goldC, true);
        MakeBlock("GateCapR", root.transform, new Vector3(0.38f, 0.14f, 0.38f), new Vector3(-halfW - 18f, 2.22f, 1.9f), goldC, true);
        MakeBlock("GateLintel", root.transform, new Vector3(0.18f, 0.2f, 3.8f), new Vector3(-halfW - 18.1f, 2.05f, 0f), fenceC, true);
        MakeBlock("GateApron", root.transform, new Vector3(4.4f, 0.1f, 4.6f), new Vector3(-halfW - 17.7f, 0.05f, 0f), stoneC);

        return root;
    }

}
