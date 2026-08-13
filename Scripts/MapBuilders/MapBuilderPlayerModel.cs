using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  PLAYER MODEL  (blocky farmer character)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("PlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = new Vector3(0f, 0.86f, 0f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * scale;

        bool female = ActiveGender == PlayerGender.Female;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = new Color(0.25f, 0.25f, 0.35f);
        Color hairC = new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = new Color(0.05f, 0.03f, 0.01f);
        Color shoeC = new Color(0.2f, 0.2f, 0.2f);
        Color dressC = new Color(0.14f, 0.44f, 0.72f);

        MakeBlock("Body", root.transform, new Vector3(female ? 0.46f : 0.5f, 0.6f, 0.25f), new Vector3(0f, 0.05f, 0f), shirtC, true);
        if (female)
        {
            MakeBlock("Skirt", root.transform, new Vector3(0.52f, 0.28f, 0.3f), new Vector3(0f, -0.27f, 0f), dressC, true);
            MakeBlock("SkirtHem", root.transform, new Vector3(0.56f, 0.06f, 0.34f), new Vector3(0f, -0.42f, 0f), new Color(0.09f, 0.3f, 0.52f), true);
        }
        MakeBlock("Head", root.transform, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0f, 0.65f, 0f), skinC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0f, 0.4f, 0f), skinC, true);
        MakeBlock("ArmL", root.transform, new Vector3(0.12f, 0.5f, 0.12f), new Vector3(-0.33f, 0.12f, 0f), shirtC, true);
        MakeBlock("ArmR", root.transform, new Vector3(0.12f, 0.5f, 0.12f), new Vector3(0.33f, 0.12f, 0f), shirtC, true);
        MakeBlock("HandL", root.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(-0.33f, -0.14f, 0f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(0.33f, -0.14f, 0f), skinC, true);
        MakeBlock("LegL", root.transform, new Vector3(0.14f, 0.5f, 0.14f), new Vector3(-0.13f, -0.5f, 0f), pantsC, true);
        MakeBlock("LegR", root.transform, new Vector3(0.14f, 0.5f, 0.14f), new Vector3(0.13f, -0.5f, 0f), pantsC, true);
        MakeBlock("ShoeL", root.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(-0.13f, -0.82f, 0f), shoeC, true);
        MakeBlock("ShoeR", root.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(0.13f, -0.82f, 0f), shoeC, true);
        MakeBlock("Hair", root.transform, new Vector3(0.32f, 0.08f, 0.3f), new Vector3(0f, 0.82f, 0f), hairC, true);
        MakeBlock("HairL", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(-0.19f, 0.69f, 0f), hairC, true);
        MakeBlock("HairR", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(0.19f, 0.69f, 0f), hairC, true);
        if (female)
        {
            MakeBlock("HairBack", root.transform, new Vector3(0.3f, 0.3f, 0.1f), new Vector3(0f, 0.7f, -0.16f), hairC, true);
            MakeBlock("HairBand", root.transform, new Vector3(0.34f, 0.05f, 0.32f), new Vector3(0f, 0.8f, 0f), new Color(0.1f, 0.34f, 0.56f), true);
        }
        MakeBlock("EyeL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.08f, 0.72f, 0.15f), eyeC, true);
        MakeBlock("EyeR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.08f, 0.72f, 0.15f), eyeC, true);

        return root;
    }

}
