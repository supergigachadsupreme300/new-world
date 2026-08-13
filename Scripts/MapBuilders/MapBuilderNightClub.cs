using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
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
        neonLabel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var neonTmp = neonLabel.AddComponent<TMPro.TextMeshPro>();
        neonTmp.text = "DANCE NIGHT";
        neonTmp.fontSize = 0.7f;
        neonTmp.alignment = TMPro.TextAlignmentOptions.Center;
        neonTmp.color = new Color(1f, 0.3f, 0.9f);
        neonTmp.outlineWidth = 0.15f;
        neonTmp.outlineColor = Color.black;
        neonTmp.rectTransform.sizeDelta = new Vector3(5.4f, 0.9f);

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
        MakeBlock("DJDesk", root.transform, new Vector3(4f, 1f, 1.1f), new Vector3(0f, 0.5f, -halfD + 1f), barC);
        MakeBlock("DJConsole", root.transform, new Vector3(3.6f, 0.12f, 0.9f), new Vector3(0f, 1.08f, -halfD + 1f), darkC, true);
        MakeBlock("Turntable", root.transform, new Vector3(0.8f, 0.06f, 0.7f), new Vector3(-1.1f, 1.15f, -halfD + 1f), new Color(0.2f, 0.2f, 0.22f), true);
        MakeBlock("Turntable", root.transform, new Vector3(0.8f, 0.06f, 0.7f), new Vector3(1.1f, 1.15f, -halfD + 1f), new Color(0.2f, 0.2f, 0.22f), true);
        MakeBlock("Mixer", root.transform, new Vector3(0.5f, 0.05f, 0.6f), new Vector3(0f, 1.16f, -halfD + 1f), new Color(0.4f, 0.2f, 0.3f), true);

        // ── Speaker stacks (corners) ──
        MakeBlock("Speaker", root.transform, new Vector3(0.9f, 1.8f, 0.8f), new Vector3(-halfW + 1.2f, 0.9f, -halfD + 1.2f), darkC, true);
        MakeBlock("Speaker", root.transform, new Vector3(0.9f, 1.8f, 0.8f), new Vector3(halfW - 1.2f, 0.9f, -halfD + 1.2f), darkC, true);

        // ── Bar along the west wall ──
        MakeBlock("Bar", root.transform, new Vector3(1.2f, 1.1f, 6.5f), new Vector3(-halfW + 1.1f, 0.55f, -1.5f), barC);
        MakeBlock("BarTop", root.transform, new Vector3(1.3f, 0.08f, 6.7f), new Vector3(-halfW + 1.1f, 1.12f, -1.5f), new Color(0.7f, 0.5f, 0.3f), true);
        for (int si = 0; si < 4; si++)
        {
            MakeBlock("BarStool", root.transform, new Vector3(0.35f, 0.6f, 0.35f), new Vector3(-halfW + 2.3f, 0.3f, -3.6f + si * 1.5f), trimC, true);
        }
        for (int bi = 0; bi < 5; bi++)
        {
            MakeBlock("Bottle", root.transform, new Vector3(0.12f, 0.4f, 0.12f), new Vector3(-halfW + 0.6f, 1.35f, -3.2f + bi * 0.7f), bi % 2 == 0 ? new Color(0.6f, 0.85f, 0.7f) : new Color(0.9f, 0.5f, 0.3f), true);
        }

        // ── VIP booth along the east wall ──
        MakeBlock("VipSofa", root.transform, new Vector3(0.5f, 0.5f, 2.4f), new Vector3(halfW - 1.1f, 0.25f, -2.5f), new Color(0.4f, 0.2f, 0.5f), true);
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

        MakeBlock("LegL", root.transform, new Vector3(0.16f, 0.5f, 0.16f), new Vector3(-0.13f, -0.25f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.16f, 0.5f, 0.16f), new Vector3(0.13f, -0.25f, 0f), pantsC, true);
        MakeBlock("Body", root.transform, new Vector3(0.5f, 0.6f, 0.3f), new Vector3(0f, 0.35f, 0f), shirtC, true);
        MakeBlock("Belt", root.transform, new Vector3(0.52f, 0.07f, 0.06f), new Vector3(0f, 0.05f, -0.14f), pantsC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.7f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.26f, 0.28f), new Vector3(0f, 0.86f, 0f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.29f, 0.1f, 0.29f), new Vector3(0f, 1.02f, 0f), new Color(0.15f, 0.1f, 0.07f), true);
        MakeBlock("ArmL", root.transform, new Vector3(0.13f, 0.5f, 0.13f), new Vector3(-0.35f, 0.4f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.13f, 0.5f, 0.13f), new Vector3(0.35f, 0.4f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(-0.35f, 0.12f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.11f, 0.1f, 0.11f), new Vector3(0.35f, 0.12f, 0f), skinC, true);

        return root;
    }

}
