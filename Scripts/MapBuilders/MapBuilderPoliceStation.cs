using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
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
        MakeBlock("StationFrontWallL", root.transform, new Vector3(3.2f, 3.4f, 0.3f), new Vector3(-2.9f, 2f, 3.25f), wallC);
        MakeBlock("StationFrontWallR", root.transform, new Vector3(3.2f, 3.4f, 0.3f), new Vector3(2.9f, 2f, 3.25f), wallC);

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
        signLabel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var tmp = signLabel.AddComponent<TMPro.TextMeshPro>();
        tmp.text = "POLICE";
        tmp.fontSize = 1.1f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = Color.black;
        tmp.rectTransform.sizeDelta = new Vector3(5.6f, 0.8f);

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

}
