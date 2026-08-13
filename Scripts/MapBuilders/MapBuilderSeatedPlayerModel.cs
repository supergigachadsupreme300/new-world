using System.Collections.Generic;
using UnityEngine;

public static partial class MapBuilder
{
    // ═══════════════════════════════════════════════════════════════
    //  SEATED PLAYER MODEL  (for inside car — arms reaching forward)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildSeatedPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("SeatedPlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = new Vector3(-0.35f, 0.65f, -0.1f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * scale;

        bool female = ActiveGender == PlayerGender.Female;

        Color skinC = new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = new Color(0.25f, 0.25f, 0.35f);
        Color hairC = female ? new Color(0.16f, 0.1f, 0.08f) : new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = new Color(0.05f, 0.03f, 0.01f);
        Color dressC = new Color(0.14f, 0.44f, 0.72f);

        // ── Torso (seated, upright) ──
        MakeBlock("Body", root.transform, new Vector3(0.38f, 0.5f, 0.28f), new Vector3(0f, 0.25f, 0f), shirtC, true);
        if (female)
        {
            MakeBlock("Skirt", root.transform, new Vector3(0.42f, 0.2f, 0.32f), new Vector3(0f, -0.02f, 0f), dressC, true);
            MakeBlock("SkirtHem", root.transform, new Vector3(0.46f, 0.05f, 0.36f), new Vector3(0f, -0.13f, 0f), new Color(0.09f, 0.3f, 0.52f), true);
        }
        // ── Head ──
        MakeBlock("Head", root.transform, new Vector3(0.28f, 0.28f, 0.28f), new Vector3(0f, 0.74f, 0f), skinC, true);
        MakeBlock("Neck", root.transform, new Vector3(0.1f, 0.08f, 0.1f), new Vector3(0f, 0.55f, 0f), skinC, true);
        // ── Hair ──
        MakeBlock("Hair", root.transform, new Vector3(0.3f, 0.07f, 0.28f), new Vector3(0f, 0.9f, 0f), hairC, true);
        MakeBlock("HairL", root.transform, new Vector3(0.08f, 0.3f, 0.12f), new Vector3(-0.18f, 0.72f, 0f), hairC, true);
        MakeBlock("HairR", root.transform, new Vector3(0.08f, 0.3f, 0.12f), new Vector3(0.18f, 0.72f, 0f), hairC, true);
        if (female)
        {
            MakeBlock("HairBack", root.transform, new Vector3(0.28f, 0.26f, 0.08f), new Vector3(0f, 0.8f, -0.14f), hairC, true);
            MakeBlock("HairBand", root.transform, new Vector3(0.3f, 0.05f, 0.3f), new Vector3(0f, 0.9f, 0f), new Color(0.1f, 0.34f, 0.56f), true);
        }
        // ── Eyes ──
        MakeBlock("EyeL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.07f, 0.77f, 0.14f), eyeC, true);
        MakeBlock("EyeR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.07f, 0.77f, 0.14f), eyeC, true);
        // ── Upper arms (reaching forward to steering wheel) ──
        MakeBlock("UpperArmL", root.transform, new Vector3(0.1f, 0.28f, 0.1f), new Vector3(-0.26f, 0.35f, 0.15f), shirtC, true).transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
        MakeBlock("UpperArmR", root.transform, new Vector3(0.1f, 0.28f, 0.1f), new Vector3(0.26f, 0.35f, 0.15f), shirtC, true).transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
        // ── Hands (at steering wheel height) ──
        MakeBlock("HandL", root.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(-0.26f, 0.42f, 0.38f), skinC, true);
        MakeBlock("HandR", root.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0.26f, 0.42f, 0.38f), skinC, true);
        // ── Legs (seated, bent forward) ──
        MakeBlock("ThighL", root.transform, new Vector3(0.13f, 0.3f, 0.13f), new Vector3(-0.12f, 0.05f, 0.08f), pantsC, true).transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        MakeBlock("ThighR", root.transform, new Vector3(0.13f, 0.3f, 0.13f), new Vector3(0.12f, 0.05f, 0.08f), pantsC, true).transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        MakeBlock("ShinL", root.transform, new Vector3(0.11f, 0.28f, 0.11f), new Vector3(-0.12f, -0.15f, 0.22f), pantsC, true).transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
        MakeBlock("ShinR", root.transform, new Vector3(0.11f, 0.28f, 0.11f), new Vector3(0.12f, -0.15f, 0.22f), pantsC, true).transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

        return root;
    }

}
