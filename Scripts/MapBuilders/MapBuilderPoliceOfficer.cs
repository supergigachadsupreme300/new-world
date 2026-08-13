using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  POLICE OFFICER
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildPoliceOfficer(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("PoliceOfficer");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = rotation;

        Color uniC = new Color(0.42f, 0.47f, 0.28f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color hatC = new Color(0.42f, 0.47f, 0.28f);
        Color goldC = new Color(0.92f, 0.78f, 0.3f);
        Color pantsC = new Color(0.34f, 0.39f, 0.22f);
        Color bootC = new Color(0.1f, 0.1f, 0.1f);
        Color visorC = new Color(0.08f, 0.08f, 0.08f);
        Color redC = new Color(0.72f, 0.12f, 0.12f);

        MakeBlock("LegL", root.transform, new Vector3(0.17f, 0.65f, 0.17f), new Vector3(-0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.65f, 0.17f), new Vector3(0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(-0.15f, -0.88f, 0f), bootC, true);
        MakeBlock("BootR", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0.15f, -0.88f, 0f), bootC, true);

        MakeBlock("Body", root.transform, new Vector3(0.5f, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), uniC, true);
        MakeBlock("Belt", root.transform, new Vector3(0.52f, 0.07f, 0.06f), new Vector3(0f, 0.12f, -0.16f), new Color(0.1f, 0.1f, 0.1f), true);
        MakeBlock("BeltBuckle", root.transform, new Vector3(0.1f, 0.06f, 0.05f), new Vector3(0f, 0.12f, -0.19f), goldC, true);
        MakeBlock("Badge", root.transform, new Vector3(0.12f, 0.1f, 0.04f), new Vector3(0f, 0.35f, -0.16f), goldC, true);
        MakeBlock("Collar", root.transform, new Vector3(0.2f, 0.08f, 0.06f), new Vector3(0f, 0.34f, -0.15f), uniC, true);
        MakeBlock("CollarTabL", root.transform, new Vector3(0.09f, 0.1f, 0.05f), new Vector3(-0.07f, 0.3f, -0.16f), redC, true);
        MakeBlock("CollarTabR", root.transform, new Vector3(0.09f, 0.1f, 0.05f), new Vector3(0.07f, 0.3f, -0.16f), redC, true);
        MakeBlock("CollarStarL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.07f, 0.31f, -0.19f), goldC, true);
        MakeBlock("CollarStarR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.07f, 0.31f, -0.19f), goldC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);

        MakeBlock("HatCrown", root.transform, new Vector3(0.34f, 0.12f, 0.34f), new Vector3(0f, 0.68f, 0f), hatC, true);
        MakeBlock("HatBrim", root.transform, new Vector3(0.44f, 0.05f, 0.28f), new Vector3(0f, 0.64f, 0f), visorC, true);
        MakeBlock("HatBand", root.transform, new Vector3(0.36f, 0.04f, 0.36f), new Vector3(0f, 0.645f, 0f), redC, true);
        MakeBlock("HatBadgeBack", root.transform, new Vector3(0.1f, 0.04f, 0.04f), new Vector3(0f, 0.67f, -0.18f), redC, true);
        MakeBlock("HatBadge", root.transform, new Vector3(0.08f, 0.05f, 0.04f), new Vector3(0f, 0.665f, -0.2f), goldC, true);

        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.34f, 0.1f, 0f), uniC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.34f, 0.1f, 0f), uniC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.34f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.34f, -0.14f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }

}
