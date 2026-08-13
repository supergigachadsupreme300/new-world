using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  RAGGED ADDICT  (junkie hunched over a victim, demon ending)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildAddictNpc(Transform parent, Vector3 position = default, Quaternion rotation = default)
    {
        var root = new GameObject("RaggedAddict");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = (rotation == default) ? Quaternion.identity : rotation;

        Color ragC = new Color(0.38f, 0.34f, 0.29f);
        Color ragDarkC = new Color(0.22f, 0.19f, 0.16f);
        Color skinC = new Color(0.62f, 0.5f, 0.42f);
        Color skinDarkC = new Color(0.32f, 0.25f, 0.2f);
        Color hairC = new Color(0.16f, 0.13f, 0.09f);

        // Bent legs — crouching over the body
        MakeBlock("LegL", root.transform, new Vector3(0.15f, 0.32f, 0.15f), new Vector3(-0.15f, 0.14f, 0.12f), ragDarkC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.17f, 0.2f, 0.45f), new Vector3(0.15f, 0.13f, -0.18f), ragDarkC, true);
        MakeBlock("FootL", root.transform, new Vector3(0.16f, 0.08f, 0.24f), new Vector3(-0.15f, 0.04f, 0.28f), ragDarkC, true);
        MakeBlock("FootR", root.transform, new Vector3(0.16f, 0.08f, 0.26f), new Vector3(0.15f, 0.04f, -0.02f), ragDarkC, true);

        // Hunched torso, leaning toward the victim
        MakeBlock("Hip", root.transform, new Vector3(0.48f, 0.18f, 0.3f), new Vector3(0f, 0.24f, -0.04f), ragDarkC, true);
        MakeBlock("Body", root.transform, new Vector3(0.5f, 0.42f, 0.34f), new Vector3(0f, 0.42f, -0.08f), ragC, true);
        MakeBlock("RagWrap", root.transform, new Vector3(0.5f, 0.1f, 0.3f), new Vector3(0f, 0.28f, 0.05f), ragDarkC, true);
        MakeBlock("PatchL", root.transform, new Vector3(0.18f, 0.16f, 0.05f), new Vector3(-0.22f, 0.5f, -0.22f), ragDarkC, true);
        MakeBlock("PatchR", root.transform, new Vector3(0.16f, 0.14f, 0.05f), new Vector3(0.21f, 0.34f, -0.22f), ragDarkC, true);
        MakeBlock("Shawl", root.transform, new Vector3(0.56f, 0.14f, 0.34f), new Vector3(0f, 0.6f, -0.1f), ragDarkC, true);

        // Neck + head, low and forward
        MakeBlock("Neck", root.transform, new Vector3(0.14f, 0.1f, 0.14f), new Vector3(0f, 0.58f, -0.14f), skinC, true);
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.28f, 0.3f), new Vector3(0f, 0.68f, -0.24f), skinC, true);
        MakeBlock("EarL", root.transform, new Vector3(0.04f, 0.07f, 0.03f), new Vector3(-0.17f, 0.7f, -0.24f), skinC, true);
        MakeBlock("EarR", root.transform, new Vector3(0.04f, 0.07f, 0.03f), new Vector3(0.17f, 0.7f, -0.24f), skinC, true);
        MakeBlock("EyeWhiteL", root.transform, new Vector3(0.08f, 0.05f, 0.03f), new Vector3(-0.09f, 0.71f, -0.4f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("EyeWhiteR", root.transform, new Vector3(0.08f, 0.05f, 0.03f), new Vector3(0.09f, 0.71f, -0.4f), new Color(0.95f, 0.95f, 0.97f), true);
        MakeBlock("PupilL", root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(-0.09f, 0.71f, -0.42f), skinDarkC, true);
        MakeBlock("PupilR", root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(0.09f, 0.71f, -0.42f), skinDarkC, true);
        MakeBlock("BrowL", root.transform, new Vector3(0.09f, 0.03f, 0.02f), new Vector3(-0.09f, 0.75f, -0.4f), hairC, true);
        MakeBlock("BrowR", root.transform, new Vector3(0.09f, 0.03f, 0.02f), new Vector3(0.09f, 0.75f, -0.4f), hairC, true);
        MakeBlock("Nose", root.transform, new Vector3(0.07f, 0.05f, 0.05f), new Vector3(0f, 0.69f, -0.42f), skinC, true);
        MakeBlock("Stubble", root.transform, new Vector3(0.24f, 0.06f, 0.08f), new Vector3(0f, 0.63f, -0.38f), skinDarkC, true);

        // Messy hair
        MakeBlock("Hair", root.transform, new Vector3(0.36f, 0.14f, 0.36f), new Vector3(0f, 0.84f, -0.24f), hairC, true);
        MakeBlock("HairTuftL", root.transform, new Vector3(0.08f, 0.12f, 0.08f), new Vector3(-0.15f, 0.92f, -0.27f), hairC, true);
        MakeBlock("HairTuftR", root.transform, new Vector3(0.08f, 0.1f, 0.08f), new Vector3(0.15f, 0.9f, -0.22f), hairC, true);

        // Arms reaching down toward the victim
        MakeBlock("ArmL", root.transform, new Vector3(0.13f, 0.38f, 0.13f), new Vector3(-0.34f, 0.44f, -0.05f), ragC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.13f, 0.38f, 0.13f), new Vector3(0.34f, 0.44f, -0.05f), ragC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.11f, 0.11f, 0.11f), new Vector3(-0.34f, 0.22f, -0.18f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.11f, 0.11f, 0.11f), new Vector3(0.34f, 0.22f, -0.18f), skinC, true);

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.8f, 1f, 0.8f);
        col.center = new Vector3(0f, 0.42f, 0f);
        col.isTrigger = true;

        return root;
    }

}
