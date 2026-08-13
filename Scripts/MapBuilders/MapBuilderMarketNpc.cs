using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  MARKET NPC  (blocky villager for tool shop & grocery stall)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildMarketNpc(Transform parent, string npcName, Vector3 localPosition, Quaternion rotation)
    {
        var root = new GameObject(npcName);
        root.transform.SetParent(parent);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = rotation;

        bool isTool = npcName == "ToolShopNPC";
        Color apronC = isTool ? new Color(0.45f, 0.48f, 0.52f) : new Color(0.3f, 0.55f, 0.3f);
        Color apronDarkC = isTool ? new Color(0.3f, 0.32f, 0.36f) : new Color(0.2f, 0.42f, 0.2f);
        Color shirtC = new Color(0.75f, 0.6f, 0.3f);
        Color skinC = new Color(230f / 255f, 200f / 255f, 175f / 255f);
        Color pantsC = new Color(0.16f, 0.16f, 0.16f);
        Color bootC = new Color(0.1f, 0.1f, 0.1f);

        MakeBlock("LegL", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(-0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.5f, 0.17f), new Vector3(0.15f, -0.6f, 0f), pantsC, true);
        MakeBlock("BootL", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(-0.15f, -0.88f, 0f), bootC, true);
        MakeBlock("BootR", root.transform, new Vector3(0.2f, 0.09f, 0.3f), new Vector3(0.15f, -0.88f, 0f), bootC, true);

        MakeBlock("Body", root.transform, new Vector3(0.52f, 0.55f, 0.3f), new Vector3(0f, 0f, 0f), shirtC, true);
        MakeBlock("Apron", root.transform, new Vector3(0.5f, 0.42f, 0.06f), new Vector3(0f, -0.05f, 0.11f), apronC, true);
        MakeBlock("ApronStrap", root.transform, new Vector3(0.06f, 0.45f, 0.05f), new Vector3(-0.16f, 0.05f, 0.12f), apronDarkC, true);
        MakeBlock("ApronStrap", root.transform, new Vector3(0.06f, 0.45f, 0.05f), new Vector3(0.16f, 0.05f, 0.12f), apronDarkC, true);
        MakeBlock("Belt", root.transform, new Vector3(0.54f, 0.07f, 0.06f), new Vector3(0f, 0.12f, -0.16f), pantsC, true);

        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.36f, 0f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.32f, 0.3f, 0.32f), new Vector3(0f, 0.52f, 0f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.09f, 0.55f, -0.16f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.06f, 0.05f), new Vector3(0f, 0.51f, -0.17f), skinC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.34f, 0.1f, 0.34f), new Vector3(0f, 0.7f, 0f), new Color(0.15f, 0.1f, 0.07f), true);

        MakeBlock("ArmL", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(-0.36f, 0.1f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.14f, 0.45f, 0.14f), new Vector3(0.36f, 0.1f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(-0.36f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.36f, -0.14f, 0f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.9f, 1.7f, 0.7f);
        col.center = new Vector3(0f, 0.45f, 0f);
        col.isTrigger = true;

        return root;
    }

}
