using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
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
        float wheelW = 0.3f;
        float xOff = 0.95f;
        float zFront = 1.3f;
        float zRear = -1.3f;
        MakeBlock("WheelFL", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(-xOff, wheelY, zFront), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelFR", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(xOff, wheelY, zFront), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelRL", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(-xOff, wheelY, zRear), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        MakeBlock("WheelRR", root.transform, new Vector3(wheelW, wheelH, wheelD), new Vector3(xOff, wheelY, zRear), wheelC, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
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
        MakeBlock("LightRed", root.transform, new Vector3(0.16f, 0.09f, 0.34f), new Vector3(0f, 1.84f, -0.35f), redC, true);
        MakeBlock("LightBlue", root.transform, new Vector3(0.16f, 0.09f, 0.34f), new Vector3(0f, 1.84f, -0.05f), blueC, true);

        return root;
    }

}
