using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  PAGODA MONK  (dialog-only tip giver)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildMonkNpc(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("PagodaMonkNpc");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;

        Color robeC = new Color(0.95f, 0.58f, 0.22f);
        Color robeDarkC = new Color(0.82f, 0.46f, 0.16f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color sandalC = new Color(0.45f, 0.3f, 0.15f);

        MakeBlock("LegL", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(-0.15f, -0.6f, 0f), robeDarkC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0.15f, -0.6f, 0f), robeDarkC, true);
        MakeBlock("SandalL", root.transform, new Vector3(0.22f, 0.07f, 0.34f), new Vector3(-0.15f, -0.88f, 0f), sandalC, true);
        MakeBlock("SandalR", root.transform, new Vector3(0.22f, 0.07f, 0.34f), new Vector3(0.15f, -0.88f, 0f), sandalC, true);

        MakeBlock("Robe", root.transform, new Vector3(0.58f, 0.6f, 0.36f), new Vector3(0f, 0f, 0f), robeC, true);
        MakeBlock("RobeDrape", root.transform, new Vector3(0.2f, 0.5f, 0.1f), new Vector3(-0.2f, 0.05f, 0.05f), robeDarkC, true);
        MakeBlock("Sash", root.transform, new Vector3(0.6f, 0.09f, 0.08f), new Vector3(0f, 0.1f, -0.17f), robeDarkC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.28f, 0.3f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("BrowL", root.transform, new Vector3(0.09f, 0.03f, 0.03f), new Vector3(-0.09f, 0.59f, -0.16f), robeDarkC, true);
        MakeBlock("BrowR", root.transform, new Vector3(0.09f, 0.03f, 0.03f), new Vector3(0.09f, 0.59f, -0.16f), robeDarkC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);
        MakeBlock("EarLobeL", root.transform, new Vector3(0.05f, 0.08f, 0.03f), new Vector3(-0.17f, 0.52f, 0f), skinC, true);
        MakeBlock("EarLobeR", root.transform, new Vector3(0.05f, 0.08f, 0.03f), new Vector3(0.17f, 0.52f, 0f), skinC, true);

        MakeBlock("ArmL", root.transform, new Vector3(0.15f, 0.45f, 0.15f), new Vector3(-0.37f, 0.1f, 0f), robeC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.15f, 0.45f, 0.15f), new Vector3(0.37f, 0.1f, 0f), robeC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.37f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.37f, -0.14f, 0f), skinC, true);

        MakeBlock("Beads", root.transform, new Vector3(0.06f, 0.4f, 0.03f), new Vector3(0.16f, -0.02f, 0.12f), new Color(0.6f, 0.35f, 0.15f), true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }

}
