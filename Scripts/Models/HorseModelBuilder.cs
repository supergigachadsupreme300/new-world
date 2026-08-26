using UnityEngine;

public static class HorseModelBuilder
{
    private static GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    public static Transform BuildHorse(Transform parent)
    {
        Color bodyBrown   = new Color(0.52f, 0.37f, 0.22f);
        Color maneDark    = new Color(0.22f, 0.14f, 0.08f);
        Color maneMid     = new Color(0.35f, 0.22f, 0.12f);
        Color tailDark    = new Color(0.20f, 0.12f, 0.06f);
        Color tailMid     = new Color(0.30f, 0.18f, 0.10f);
        Color muzzleLight = new Color(0.72f, 0.58f, 0.42f);
        Color legLower    = new Color(0.42f, 0.30f, 0.18f);
        Color hoofBlack   = new Color(0.12f, 0.10f, 0.08f);
        Color eyeBlack    = new Color(0.05f, 0.05f, 0.05f);
        Color earInner    = new Color(0.65f, 0.48f, 0.35f);
        Color nostrilDark = new Color(0.30f, 0.20f, 0.12f);

        var root = new GameObject("HorseModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        // ═══════════════════════════════════════════
        //  BODY (barrel shape, parented to root)
        // ═══════════════════════════════════════════

        MakeBlock("Barrel", root.transform, new Vector3(0.48f, 0.44f, 1.20f), new Vector3(0f, 0.65f, 0f), bodyBrown);
        MakeBlock("ChestBulge", root.transform, new Vector3(0.44f, 0.30f, 0.30f), new Vector3(0f, 0.70f, -0.42f), bodyBrown);
        MakeBlock("RumpBlock", root.transform, new Vector3(0.42f, 0.36f, 0.28f), new Vector3(0f, 0.70f, 0.42f), bodyBrown);
        MakeBlock("BellyPanel", root.transform, new Vector3(0.30f, 0.10f, 0.80f), new Vector3(0f, 0.40f, 0f), legLower);

        // ═══════════════════════════════════════════
        //  NECK + HEAD (pivot hierarchy for animation)
        // ═══════════════════════════════════════════

        var neckPivot = new GameObject("NeckPivot");
        neckPivot.transform.SetParent(root.transform, false);
        neckPivot.transform.localPosition = new Vector3(0f, 1.05f, -0.48f);
        neckPivot.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

        MakeBlock("Neck1", neckPivot.transform, new Vector3(0.14f, 0.28f, 0.14f), new Vector3(0f, 0.14f, 0f), bodyBrown);
        MakeBlock("Neck2", neckPivot.transform, new Vector3(0.12f, 0.24f, 0.12f), new Vector3(0f, 0.38f, 0f), bodyBrown);

        // Mane (dark brown blocks along crest/top of neck)
        MakeBlock("Mane1", neckPivot.transform, new Vector3(0.06f, 0.05f, 0.14f), new Vector3(0f, 0.16f, 0.0f), maneDark);
        MakeBlock("Mane2", neckPivot.transform, new Vector3(0.05f, 0.04f, 0.12f), new Vector3(0f, 0.28f, 0.0f), maneMid);
        MakeBlock("Mane3", neckPivot.transform, new Vector3(0.05f, 0.04f, 0.10f), new Vector3(0f, 0.39f, 0.0f), maneDark);
        MakeBlock("Mane4", neckPivot.transform, new Vector3(0.04f, 0.03f, 0.08f), new Vector3(0f, 0.49f, 0.0f), maneMid);

        // Head
        var head = MakeBlock("Head", neckPivot.transform, new Vector3(0.22f, 0.20f, 0.24f), new Vector3(0f, 0.58f, 0f), bodyBrown);
        MakeBlock("Muzzle", head.transform, new Vector3(0.14f, 0.12f, 0.20f), new Vector3(0f, -0.06f, 0.14f), muzzleLight);

        // Eyes (on sides of head, at X surface ±0.11)
        MakeBlock("EyeL", head.transform, new Vector3(0.02f, 0.04f, 0.04f), new Vector3(-0.12f, 0.04f, 0.04f), eyeBlack);
        MakeBlock("EyeR", head.transform, new Vector3(0.02f, 0.04f, 0.04f), new Vector3(0.12f, 0.04f, 0.04f), eyeBlack);
        MakeBlock("EyeHighlightL", head.transform, new Vector3(0.01f, 0.02f, 0.02f), new Vector3(-0.13f, 0.05f, 0.05f), Color.white);
        MakeBlock("EyeHighlightR", head.transform, new Vector3(0.01f, 0.02f, 0.02f), new Vector3(0.13f, 0.05f, 0.05f), Color.white);

        // Nostrils (on muzzle front face at Z≈0.24)
        MakeBlock("NostrilL", head.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(-0.03f, -0.04f, 0.26f), nostrilDark);
        MakeBlock("NostrilR", head.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(0.03f, -0.04f, 0.26f), nostrilDark);

        // Ears (pivot hierarchy)
        var earLPivot = new GameObject("EarLPivot");
        earLPivot.transform.SetParent(head.transform, false);
        earLPivot.transform.localPosition = new Vector3(-0.07f, 0.14f, 0f);
        earLPivot.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        MakeBlock("EarL", earLPivot.transform, new Vector3(0.04f, 0.12f, 0.03f), new Vector3(0f, 0.06f, 0f), bodyBrown);
        MakeBlock("EarInnerL", earLPivot.transform, new Vector3(0.02f, 0.08f, 0.01f), new Vector3(0f, 0.06f, 0.01f), earInner);

        var earRPivot = new GameObject("EarRPivot");
        earRPivot.transform.SetParent(head.transform, false);
        earRPivot.transform.localPosition = new Vector3(0.07f, 0.14f, 0f);
        earRPivot.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
        MakeBlock("EarR", earRPivot.transform, new Vector3(0.04f, 0.12f, 0.03f), new Vector3(0f, 0.06f, 0f), bodyBrown);
        MakeBlock("EarInnerR", earRPivot.transform, new Vector3(0.02f, 0.08f, 0.01f), new Vector3(0f, 0.06f, 0.01f), earInner);

        // ═══════════════════════════════════════════
        //  FRONT LEGS (pivot hierarchy)
        // ═══════════════════════════════════════════

        BuildLeg(root.transform, "L", new Vector3(-0.13f, 0.35f, -0.22f), bodyBrown, legLower, hoofBlack);
        BuildLeg(root.transform, "R", new Vector3(0.13f, 0.35f, -0.22f), bodyBrown, legLower, hoofBlack);

        // ═══════════════════════════════════════════
        //  HIND LEGS (pivot hierarchy)
        // ═══════════════════════════════════════════

        BuildLeg(root.transform, "BackL", new Vector3(-0.13f, 0.35f, 0.42f), bodyBrown, legLower, hoofBlack);
        BuildLeg(root.transform, "BackR", new Vector3(0.13f, 0.35f, 0.42f), bodyBrown, legLower, hoofBlack);

        // ═══════════════════════════════════════════
        //  TAIL (pivot chain)
        // ═══════════════════════════════════════════

        var tailPivot = new GameObject("TailPivot");
        tailPivot.transform.SetParent(root.transform, false);
        tailPivot.transform.localPosition = new Vector3(0f, 0.80f, 0.58f);
        tailPivot.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

        var tail1 = MakeBlock("Tail1", tailPivot.transform, new Vector3(0.06f, 0.06f, 0.30f), new Vector3(0f, -0.06f, 0.15f), tailDark);
        var tail2 = MakeBlock("Tail2", tail1.transform, new Vector3(0.05f, 0.05f, 0.25f), new Vector3(0f, -0.02f, 0.25f), tailMid);
        MakeBlock("Tail3", tail2.transform, new Vector3(0.04f, 0.04f, 0.20f), new Vector3(0f, -0.01f, 0.22f), tailDark);

        return root.transform;
    }

    private static void BuildLeg(Transform root, string suffix, Vector3 hipPos, Color upperColor, Color lowerColor, Color hoofColor)
    {
        var hipPivot = new GameObject("HipPivot" + suffix);
        hipPivot.transform.SetParent(root, false);
        hipPivot.transform.localPosition = hipPos;
        hipPivot.transform.localRotation = Quaternion.identity;

        MakeBlock("UpperLeg" + suffix, hipPivot.transform, new Vector3(0.08f, 0.32f, 0.08f), new Vector3(0f, -0.16f, 0f), upperColor);

        var kneePivot = new GameObject("KneePivot" + suffix);
        kneePivot.transform.SetParent(hipPivot.transform, false);
        kneePivot.transform.localPosition = new Vector3(0f, -0.32f, 0f);
        kneePivot.transform.localRotation = Quaternion.identity;

        MakeBlock("LowerLeg" + suffix, kneePivot.transform, new Vector3(0.07f, 0.30f, 0.07f), new Vector3(0f, -0.15f, 0f), lowerColor);
        MakeBlock("Hoof" + suffix, kneePivot.transform, new Vector3(0.10f, 0.05f, 0.12f), new Vector3(0f, -0.32f, 0f), hoofColor);
    }
}
