using UnityEngine;

/// <summary>
/// Dresses the night club's east-facing entrance so the "rich man at the club"
/// scene reads as a real night scene: lit canopy, red carpet, queue bollards,
/// patrons + bouncer, glowing windows, a flickering deal-corner lamp, crates and
/// a parked delivery wagon. Everything is parented under the club root so the
/// NightClubController day/night toggle, persistence and the NavGrid rebuild all
/// pick the props up automatically with no extra wiring.
/// </summary>
public static class ClubExteriorBuilder
{
    private static Vector3 ToLocal(Transform club, Vector3 worldPos)
        => club.InverseTransformPoint(worldPos);

    private static Quaternion ToLocalRot(Transform club, float worldYaw)
        => Quaternion.Inverse(club.rotation) * Quaternion.Euler(0f, worldYaw, 0f);

    private static GameObject MakeHolder(Transform club, string name, Vector3 worldPos, float worldYaw = 0f)
    {
        var h = new GameObject(name);
        h.transform.SetParent(club, false);
        h.transform.localPosition = ToLocal(club, worldPos);
        h.transform.localRotation = ToLocalRot(club, worldYaw);
        return h;
    }

    private static Light AddPointLight(Transform club, Vector3 worldPos, float range, float intensity, Color color, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(club, false);
        go.transform.localPosition = ToLocal(club, worldPos);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static void MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool noCollider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        if (noCollider)
            Object.Destroy(go.GetComponent<Collider>());
    }

    public static void Build(Transform club)
    {
        Color awningC = new Color(0.55f, 0.12f, 0.16f);
        Color creamC = new Color(0.92f, 0.88f, 0.8f);
        Color carpetC = new Color(0.6f, 0.08f, 0.12f);
        Color goldC = new Color(0.95f, 0.8f, 0.45f);
        Color metalC = new Color(0.16f, 0.14f, 0.16f);
        Color warmC = new Color(1f, 0.85f, 0.5f);
        Color greenC = new Color(0.2f, 0.5f, 0.2f);
        Color crateC = new Color(0.66f, 0.5f, 0.3f);
        Color barrelC = new Color(0.45f, 0.3f, 0.14f);
        Color glowMag = new Color(1f, 0.4f, 0.85f);
        Color glowCyan = new Color(0.35f, 0.85f, 1f);

        // ── Door canopy over the east entrance (door at ~(9, 95)) ──
        var canopy = MakeHolder(club, "ClubCanopy", new Vector3(8.6f, 0f, 95f));
        MakeBlock("CanopyAwning", canopy.transform, new Vector3(4.6f, 0.16f, 5.6f), new Vector3(0f, 3.4f, 0f), awningC);
        MakeBlock("CanopyAwning2", canopy.transform, new Vector3(4.7f, 0.16f, 5.7f), new Vector3(0.05f, 3.56f, 0f), creamC);
        MakeBlock("CanopyValence", canopy.transform, new Vector3(0.32f, 0.14f, 5.7f), new Vector3(1.05f, 3.18f, 0f), awningC);
        MakeBlock("CanopyPostL", canopy.transform, new Vector3(0.18f, 3.2f, 0.18f), new Vector3(0.9f, 1.6f, 2.1f), metalC, false);
        MakeBlock("CanopyPostR", canopy.transform, new Vector3(0.18f, 3.2f, 0.18f), new Vector3(0.9f, 1.6f, -2.1f), metalC, false);

        // ── Red carpet runner from the door toward the road ──
        var carpet = MakeHolder(club, "ClubCarpet", new Vector3(7.3f, 0f, 95f));
        MakeBlock("CarpetSlab", carpet.transform, new Vector3(3.2f, 0.06f, 3.2f), new Vector3(0f, 0.03f, 0f), carpetC);
        MakeBlock("CarpetTrimL", carpet.transform, new Vector3(0.12f, 0.07f, 3.2f), new Vector3(-1.62f, 0.035f, 0f), goldC);
        MakeBlock("CarpetTrimR", carpet.transform, new Vector3(0.12f, 0.07f, 3.2f), new Vector3(1.62f, 0.035f, 0f), goldC);

        // ── Lantern sconces flanking the door (flickering) ──
        for (int s = 0; s < 2; s++)
        {
            float sz = s == 0 ? 93.2f : 96.8f;
            var sconce = MakeHolder(club, "ClubSconce", new Vector3(9.42f, 0f, sz));
            MakeBlock("ClubSconceArm", sconce.transform, new Vector3(0.5f, 0.12f, 0.12f), new Vector3(0f, 2.15f, 0f), metalC);
            MakeBlock("ClubSconceGlow", sconce.transform, new Vector3(0.22f, 0.34f, 0.22f), new Vector3(0f, 2.15f, 0f), warmC);
            var scL = AddPointLight(club, new Vector3(9.42f, 2.35f, sz), 7f, 2.4f, warmC, "ClubSconceLight");
            scL.gameObject.AddComponent<FlickerLight>().Intensity = scL.intensity;
        }

        // ── Glowing strip windows on the east facade ──
        for (int w = 0; w < 2; w++)
        {
            float wz = w == 0 ? 88.8f : 101.2f;
            Color glC = w == 0 ? glowMag : glowCyan;
            var win = MakeHolder(club, "ClubStripWindow", new Vector3(9.32f, 0f, wz));
            MakeBlock("ClubWindowGlow", win.transform, new Vector3(0.22f, 1.8f, 2.6f), new Vector3(0f, 2.3f, 0f), glC);
            AddPointLight(club, new Vector3(9.32f, 2.5f, wz), 7f, 1.6f, glC, "ClubWindowLight");
        }

        // ── Queue bollards + rope framing the approach ──
        for (int q = 0; q < 2; q++)
        {
            float qz = q == 0 ? 92.55f : 97.45f;
            var queue = MakeHolder(club, "ClubQueue", new Vector3(6.9f, 0f, qz));
            MakeBlock("BollardA", queue.transform, new Vector3(0.3f, 0.7f, 0.3f), new Vector3(-1.7f, 0.35f, 0f), metalC, false);
            MakeBlock("BollardB", queue.transform, new Vector3(0.3f, 0.7f, 0.3f), new Vector3(1.7f, 0.35f, 0f), metalC, false);
            MakeBlock("QueueRope", queue.transform, new Vector3(3.4f, 0.05f, 0.08f), new Vector3(0f, 0.72f, 0f), carpetC);
        }

        // ── Planter bushes flanking the approach start ──
        for (int p = 0; p < 2; p++)
        {
            float pz = p == 0 ? 92.6f : 97.4f;
            var planter = MakeHolder(club, "ClubPlanter", new Vector3(4.4f, 0f, pz));
            MakeBlock("PlanterBox", planter.transform, new Vector3(0.75f, 0.5f, 0.75f), new Vector3(0f, 0.25f, 0f), metalC, false);
            MakeBlock("PlanterBush", planter.transform, new Vector3(0.55f, 0.55f, 0.55f), new Vector3(0f, 0.6f, 0f), greenC);
            MakeBlock("PlanterFlower", planter.transform, new Vector3(0.22f, 0.24f, 0.22f), new Vector3(0.14f, 0.62f, 0.12f), glowMag);
            MakeBlock("PlanterFlower", planter.transform, new Vector3(0.22f, 0.24f, 0.22f), new Vector3(-0.12f, 0.62f, -0.14f), glowCyan);
        }

        // ── Night patrons + bouncer silhouettes (bob lightly) ──
        var patron1 = MapBuilder.BuildClubDancer(club, ToLocal(club, new Vector3(6.4f, 0.2f, 90.8f)),
            ToLocalRot(club, 90f), new Color(0.9f, 0.5f, 0.2f), new Color(0.15f, 0.15f, 0.22f), new Color(0.85f, 0.72f, 0.62f));
        patron1.name = "ClubPatron";
        patron1.AddComponent<ClubPatronAnimator>();

        var patron2 = MapBuilder.BuildClubDancer(club, ToLocal(club, new Vector3(7.6f, 0.2f, 90.2f)),
            ToLocalRot(club, 105f), new Color(0.4f, 0.75f, 0.95f), new Color(0.15f, 0.15f, 0.22f), new Color(0.85f, 0.72f, 0.62f));
        patron2.name = "ClubPatron";
        patron2.AddComponent<ClubPatronAnimator>();

        var bouncer = MapBuilder.BuildClubDancer(club, ToLocal(club, new Vector3(8.8f, 0.2f, 92.9f)),
            ToLocalRot(club, -90f), new Color(0.12f, 0.12f, 0.16f), new Color(0.1f, 0.1f, 0.13f), new Color(0.8f, 0.68f, 0.58f));
        bouncer.name = "ClubBouncer";

        // ── Crate/barrel "delivery" cluster against the east wall ──
        var crates = MakeHolder(club, "ClubCrates", new Vector3(9.3f, 0f, 102.4f));
        MakeBlock("Crate", crates.transform, new Vector3(0.7f, 0.6f, 0.7f), new Vector3(0f, 0.3f, 0f), crateC, false);
        MakeBlock("Crate", crates.transform, new Vector3(0.7f, 0.6f, 0.7f), new Vector3(0.78f, 0.3f, 0.5f), crateC, false);
        MakeBlock("Barrel", crates.transform, new Vector3(0.55f, 0.85f, 0.55f), new Vector3(-0.2f, 0.42f, 1.0f), barrelC, false);
        MakeBlock("Barrel", crates.transform, new Vector3(0.55f, 0.85f, 0.55f), new Vector3(0.85f, 0.42f, -0.55f), barrelC, false);
        MakeBlock("Crate", crates.transform, new Vector3(0.5f, 0.45f, 0.5f), new Vector3(0.05f, 0.78f, 0.15f), crateC, false);
        MakeBlock("Tarp", crates.transform, new Vector3(0.55f, 0.08f, 0.7f), new Vector3(0.55f, 0.95f, -0.3f), new Color(0.12f, 0.12f, 0.14f));

        // ── Deal-corner lamp: a flickering pool of light where the deal happens ──
        var dealLamp = MakeHolder(club, "ClubDealLamp", new Vector3(13.2f, 0f, 98.2f));
        MakeBlock("DealLampPole", dealLamp.transform, new Vector3(0.14f, 3.6f, 0.14f), new Vector3(0f, 1.8f, 0f), metalC, false);
        MakeBlock("DealLampArm", dealLamp.transform, new Vector3(1.1f, 0.12f, 0.12f), new Vector3(0.45f, 3.45f, 0f), metalC);
        MakeBlock("DealLampHead", dealLamp.transform, new Vector3(0.4f, 0.22f, 0.4f), new Vector3(0.95f, 3.35f, 0f), warmC);
        MakeBlock("DealLampGlow", dealLamp.transform, new Vector3(0.26f, 0.2f, 0.26f), new Vector3(0.95f, 3.3f, 0f), warmC);
        var dealLight = AddPointLight(club, new Vector3(14.15f, 3.4f, 98.2f), 9f, 3f, warmC, "ClubDealLight");
        dealLight.gameObject.AddComponent<FlickerLight>().Intensity = dealLight.intensity;

        // ── Warm light spilling out of the open door ──
        AddPointLight(club, new Vector3(8.7f, 2.4f, 95f), 8f, 2f, new Color(1f, 0.65f, 0.35f), "ClubDoorLight");

        // ── Parked delivery wagon + horse (deal-set backdrop) ──
        var wagon = MakeHolder(club, "ClubWagon", new Vector3(22.5f, 0f, 104.4f), -90f);
        Color brn = new Color(85f / 255f, 52f / 255f, 22f / 255f);
        Color tan = new Color(210f / 255f, 195f / 255f, 160f / 255f);
        Color dk = new Color(70f / 255f, 42f / 255f, 16f / 255f);
        MakeBlock("WagonBed", wagon.transform, new Vector3(3.0f, 0.12f, 1.7f), new Vector3(0f, 0.5f, 0f), brn, false);
        MakeBlock("WagonSideN", wagon.transform, new Vector3(3.0f, 0.55f, 0.1f), new Vector3(0f, 0.85f, 0.78f), dk, false);
        MakeBlock("WagonSideS", wagon.transform, new Vector3(3.0f, 0.55f, 0.1f), new Vector3(0f, 0.85f, -0.78f), dk, false);
        MakeBlock("WagonEndF", wagon.transform, new Vector3(0.1f, 0.6f, 1.7f), new Vector3(1.5f, 0.85f, 0f), dk, false);
        MakeBlock("WagonEndR", wagon.transform, new Vector3(0.1f, 0.45f, 1.7f), new Vector3(-1.5f, 0.7f, 0f), dk, false);
        MakeBlock("WagonSeat", wagon.transform, new Vector3(1.2f, 0.1f, 0.5f), new Vector3(-0.3f, 0.85f, 0f), tan);
        MakeBlock("WagonWheelFL", wagon.transform, new Vector3(0.55f, 0.14f, 0.14f), new Vector3(1.0f, 0.45f, 0.72f), dk);
        MakeBlock("WagonWheelFR", wagon.transform, new Vector3(0.55f, 0.14f, 0.14f), new Vector3(1.0f, 0.45f, -0.72f), dk);
        MakeBlock("WagonWheelRL", wagon.transform, new Vector3(0.55f, 0.14f, 0.14f), new Vector3(-1.0f, 0.45f, 0.72f), dk);
        MakeBlock("WagonWheelRR", wagon.transform, new Vector3(0.55f, 0.14f, 0.14f), new Vector3(-1.0f, 0.45f, -0.72f), dk);
        MakeBlock("WagonShaftL", wagon.transform, new Vector3(2.4f, 0.07f, 0.07f), new Vector3(-1.8f, 0.6f, 0.45f), dk);
        MakeBlock("WagonShaftR", wagon.transform, new Vector3(2.4f, 0.07f, 0.07f), new Vector3(-1.8f, 0.6f, -0.45f), dk);
        MakeBlock("WagonLantern", wagon.transform, new Vector3(0.22f, 0.32f, 0.22f), new Vector3(0f, 1.5f, 0.9f), metalC);
        MakeBlock("WagonLanternGlow", wagon.transform, new Vector3(0.14f, 0.2f, 0.14f), new Vector3(0f, 1.5f, 0.9f), warmC);
        var wagonLight = AddPointLight(club, new Vector3(21.6f, 1.6f, 104.4f), 5f, 1.8f, warmC, "WagonLanternLight");
        wagonLight.gameObject.AddComponent<FlickerLight>().Intensity = wagonLight.intensity;

        var horse = HorseModelBuilder.BuildHorse(wagon.transform);
        horse.localPosition = new Vector3(-2.3f, 0f, 0.2f);
        horse.localRotation = Quaternion.identity;
    }
}