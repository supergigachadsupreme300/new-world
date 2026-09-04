using UnityEngine;

/// <summary>
/// Procedural cube-based models for all enemy types.
/// </summary>
public static class EnemyModelBuilder
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

    public static Transform BuildEnemy(Transform parent, string enemyId)
    {
        switch (enemyId)
        {
            case "slime":          return BuildSlime(parent);
            case "wolf":           return BuildWolf(parent);
            case "goblin":         return GoblinModelBuilder.BuildGoblin(parent);
            case "bandit":         return BuildBandit(parent);
            case "treant":         return BuildTreant(parent);
            case "golem":          return BuildGolem(parent);
            case "drake":          return BuildDrake(parent);
            case "undead":         return BuildUndead(parent);
            case "slug":           return BuildSlug(parent);
            case "scorpion":       return BuildScorpion(parent);
            case "mummy":          return BuildMummy(parent);
            case "yeti":           return BuildYeti(parent);
            case "ice_wolf":       return BuildIceWolf(parent);
            case "fire_elemental": return BuildFireElemental(parent);
            case "dragon":         return BuildDragon(parent);
            case "demon":          return BuildDemon(parent);
            case "mimic":          return BuildMimic(parent);
            case "sea_creature":   return BuildSeaCreature(parent);
            case "skeleton":       return BuildSkeleton(parent);
            case "bat":            return BuildBat(parent);
            default:               return BuildSlime(parent);
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  1. SLIME â€” blob, no limbs, ~0.35u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildSlime(Transform parent)
    {
        Color bodyGreen = new Color(0.35f, 0.75f, 0.25f);
        Color bodyDark  = new Color(0.22f, 0.55f, 0.15f);
        Color eyeWhite  = new Color(0.95f, 0.95f, 0.9f);
        Color eyePupil  = new Color(0.05f, 0.05f, 0.05f);

        var root = new GameObject("SlimeModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body",   root.transform, new Vector3(0.5f, 0.3f, 0.5f),     new Vector3(0f, 0.15f, 0f),       bodyGreen);
        MakeBlock("Top",    root.transform, new Vector3(0.35f, 0.15f, 0.35f),   new Vector3(0f, 0.35f, 0f),       bodyGreen);
        MakeBlock("Base",   root.transform, new Vector3(0.48f, 0.08f, 0.48f),   new Vector3(0f, 0.04f, 0f),       bodyDark);
        MakeBlock("EyeL",   root.transform, new Vector3(0.08f, 0.08f, 0.02f),   new Vector3(-0.1f, 0.25f, 0.24f), eyeWhite);
        MakeBlock("EyeR",   root.transform, new Vector3(0.08f, 0.08f, 0.02f),   new Vector3(0.1f, 0.25f, 0.24f),  eyeWhite);
        MakeBlock("PupilL", root.transform, new Vector3(0.03f, 0.03f, 0.02f),   new Vector3(-0.1f, 0.25f, 0.25f), eyePupil);
        MakeBlock("PupilR", root.transform, new Vector3(0.03f, 0.03f, 0.02f),   new Vector3(0.1f, 0.25f, 0.25f),  eyePupil);
        MakeBlock("Mouth",  root.transform, new Vector3(0.12f, 0.02f, 0.02f),   new Vector3(0f, 0.15f, 0.25f),    bodyDark);

        return root.transform;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  2. WOLF â€” quadruped, ~0.5u tall
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildWolf(Transform parent)
    {
        Color fg = new Color(0.45f, 0.42f, 0.4f);
        Color fd = new Color(0.3f, 0.28f, 0.26f);
        Color fb = new Color(0.55f, 0.52f, 0.5f);
        Color ey = new Color(0.9f, 0.8f, 0.15f);
        Color nb = new Color(0.08f, 0.08f, 0.08f);
        Color tw = new Color(0.92f, 0.92f, 0.9f);

        var root = new GameObject("WolfModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body",  root.transform, new Vector3(0.25f, 0.2f, 0.55f),  new Vector3(0f, 0.45f, 0f),  fg);
        MakeBlock("Belly", root.transform, new Vector3(0.2f, 0.1f, 0.45f),   new Vector3(0f, 0.38f, 0f),  fb);

        var neck = new GameObject("Neck");
        neck.transform.SetParent(root.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.5f, 0.22f);
        neck.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        MakeBlock("Neck",  neck.transform, new Vector3(0.15f, 0.18f, 0.12f), new Vector3(0f, 0.08f, 0f),  fg);
        MakeBlock("Head",  neck.transform, new Vector3(0.18f, 0.14f, 0.2f),  new Vector3(0f, 0.2f, 0.04f), fg);
        MakeBlock("Snout", neck.transform, new Vector3(0.1f, 0.08f, 0.12f),  new Vector3(0f, 0.15f, 0.16f), fd);
        MakeBlock("Nose",  neck.transform, new Vector3(0.06f, 0.04f, 0.02f), new Vector3(0f, 0.17f, 0.22f), nb);
        MakeBlock("EyeL",  neck.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(-0.07f, 0.22f, 0.12f), ey);
        MakeBlock("EyeR",  neck.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.07f, 0.22f, 0.12f),  ey);
        MakeBlock("EarL",  neck.transform, new Vector3(0.04f, 0.1f, 0.04f),  new Vector3(-0.06f, 0.28f, 0f), fd);
        MakeBlock("EarR",  neck.transform, new Vector3(0.04f, 0.1f, 0.04f),  new Vector3(0.06f, 0.28f, 0f),  fd);
        MakeBlock("JawL",  neck.transform, new Vector3(0.08f, 0.03f, 0.1f),  new Vector3(-0.03f, 0.1f, 0.14f), tw);
        MakeBlock("JawR",  neck.transform, new Vector3(0.08f, 0.03f, 0.1f),  new Vector3(0.03f, 0.1f, 0.14f),  tw);

        var tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.5f, -0.28f);
        tail.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        MakeBlock("Tail",    tail.transform, new Vector3(0.06f, 0.06f, 0.25f), new Vector3(0f, -0.05f, -0.1f),  fg);
        MakeBlock("TailTip", tail.transform, new Vector3(0.04f, 0.04f, 0.1f),  new Vector3(0f, -0.08f, -0.22f), fd);

        WolfLeg(root.transform, -0.08f, 0.15f, fg, fd, "FL");
        WolfLeg(root.transform,  0.08f, 0.15f, fg, fd, "FR");
        WolfLeg(root.transform, -0.08f, -0.15f, fg, fd, "BL");
        WolfLeg(root.transform,  0.08f, -0.15f, fg, fd, "BR");

        return root.transform;
    }

    private static void WolfLeg(Transform root, float x, float z, Color fg, Color fd, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(root, false);
        h.transform.localPosition = new Vector3(x, 0.35f, z);
        MakeBlock("UL" + s, h.transform, new Vector3(0.07f, 0.15f, 0.07f), new Vector3(0f, -0.07f, 0f), fg);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.06f, 0.12f, 0.06f), new Vector3(0f, -0.06f, 0f), fd);
        MakeBlock("Paw" + s, k.transform, new Vector3(0.08f, 0.04f, 0.1f), new Vector3(0f, -0.14f, 0.02f), fd);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  3. GOBLIN â€” delegates to GoblinModelBuilder
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  4. BANDIT â€” humanoid rogue, ~0.95u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildBandit(Transform parent)
    {
        Color sk = new Color(0.75f, 0.58f, 0.42f);
        Color hd = new Color(0.12f, 0.1f, 0.1f);
        Color tb = new Color(0.25f, 0.18f, 0.12f);
        Color pd = new Color(0.15f, 0.13f, 0.12f);
        Color bk = new Color(0.08f, 0.06f, 0.06f);
        Color dg = new Color(0.65f, 0.65f, 0.7f);
        Color ed = new Color(0.1f, 0.06f, 0.02f);
        Color bl = new Color(0.35f, 0.22f, 0.1f);

        var root = new GameObject("BanditModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Torso", root.transform, new Vector3(0.3f, 0.22f, 0.18f), new Vector3(0f, 0.55f, 0f), tb);
        MakeBlock("Belt",  root.transform, new Vector3(0.32f, 0.06f, 0.2f), new Vector3(0f, 0.44f, 0f), bl);
        MakeBlock("Pants", root.transform, new Vector3(0.28f, 0.18f, 0.17f), new Vector3(0f, 0.35f, 0f), pd);
        MakeBlock("Neck",  root.transform, new Vector3(0.08f, 0.05f, 0.08f), new Vector3(0f, 0.68f, 0f), sk);
        MakeBlock("Head",  root.transform, new Vector3(0.22f, 0.22f, 0.22f), new Vector3(0f, 0.82f, 0f), sk);
        MakeBlock("HoodT", root.transform, new Vector3(0.26f, 0.14f, 0.28f), new Vector3(0f, 0.9f, -0.02f), hd);
        MakeBlock("HoodL", root.transform, new Vector3(0.06f, 0.18f, 0.2f),  new Vector3(-0.13f, 0.8f, 0f), hd);
        MakeBlock("HoodR", root.transform, new Vector3(0.06f, 0.18f, 0.2f),  new Vector3(0.13f, 0.8f, 0f),  hd);
        MakeBlock("Mask",  root.transform, new Vector3(0.2f, 0.08f, 0.02f),  new Vector3(0f, 0.78f, 0.11f), hd);
        MakeBlock("EyeL",  root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(-0.05f, 0.84f, 0.11f), ed);
        MakeBlock("EyeR",  root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.05f, 0.84f, 0.11f),  ed);

        var aL = new GameObject("ArmL");
        aL.transform.SetParent(root.transform, false);
        aL.transform.localPosition = new Vector3(-0.2f, 0.62f, 0f);
        MakeBlock("UAL", aL.transform, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(0f, -0.07f, 0f), tb);
        MakeBlock("LAL", aL.transform, new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0f, -0.22f, 0f), sk);
        MakeBlock("Han", aL.transform, new Vector3(0.06f, 0.06f, 0.06f), new Vector3(0f, -0.32f, 0f), sk);

        var aR = new GameObject("ArmR");
        aR.transform.SetParent(root.transform, false);
        aR.transform.localPosition = new Vector3(0.2f, 0.62f, 0f);
        MakeBlock("UAR", aR.transform, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(0f, -0.07f, 0f), tb);
        MakeBlock("LAR", aR.transform, new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0f, -0.22f, 0f), sk);
        MakeBlock("HaR", aR.transform, new Vector3(0.06f, 0.06f, 0.06f), new Vector3(0f, -0.32f, 0f), sk);
        MakeBlock("Dag", aR.transform, new Vector3(0.02f, 0.15f, 0.03f), new Vector3(0f, -0.4f, 0f),  dg);

        HumanLeg(root.transform, -0.08f, pd, bk, "L");
        HumanLeg(root.transform,  0.08f, pd, bk, "R");

        return root.transform;
    }

    private static void HumanLeg(Transform root, float x, Color p, Color b, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(root, false);
        h.transform.localPosition = new Vector3(x, 0.35f, 0f);
        MakeBlock("UL" + s, h.transform, new Vector3(0.08f, 0.18f, 0.08f), new Vector3(0f, -0.08f, 0f), p);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.07f, 0.14f, 0.07f), new Vector3(0f, -0.07f, 0f), p);
        MakeBlock("Boot" + s, k.transform, new Vector3(0.09f, 0.06f, 0.12f), new Vector3(0f, -0.16f, 0.02f), b);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  5. TREANT â€” living tree, ~1.4u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildTreant(Transform parent)
    {
        Color bk = new Color(0.35f, 0.22f, 0.1f);
        Color bd = new Color(0.22f, 0.14f, 0.06f);
        Color lg = new Color(0.2f, 0.55f, 0.15f);
        Color ld = new Color(0.12f, 0.4f, 0.1f);
        Color eg = new Color(0.8f, 0.95f, 0.3f);

        var root = new GameObject("TreantModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Trunk",   root.transform, new Vector3(0.3f, 0.5f, 0.25f),    new Vector3(0f, 0.35f, 0f),     bk);
        MakeBlock("Base",    root.transform, new Vector3(0.4f, 0.15f, 0.35f),   new Vector3(0f, 0.08f, 0f),     bd);
        MakeBlock("Knot",    root.transform, new Vector3(0.08f, 0.08f, 0.04f),  new Vector3(0.1f, 0.4f, 0.13f), bd);
        MakeBlock("CanL",    root.transform, new Vector3(0.35f, 0.25f, 0.3f),   new Vector3(-0.15f, 0.85f, 0f),  lg);
        MakeBlock("CanR",    root.transform, new Vector3(0.35f, 0.25f, 0.3f),   new Vector3(0.15f, 0.85f, 0f),   lg);
        MakeBlock("CanT",    root.transform, new Vector3(0.4f, 0.2f, 0.35f),    new Vector3(0f, 1.0f, 0f),       lg);
        MakeBlock("CanB",    root.transform, new Vector3(0.3f, 0.2f, 0.25f),    new Vector3(0f, 0.9f, -0.15f),   ld);
        MakeBlock("EyeL",    root.transform, new Vector3(0.06f, 0.06f, 0.02f),  new Vector3(-0.06f, 0.55f, 0.13f), eg);
        MakeBlock("EyeR",    root.transform, new Vector3(0.06f, 0.06f, 0.02f),  new Vector3(0.06f, 0.55f, 0.13f),  eg);
        MakeBlock("ArmLB",   root.transform, new Vector3(0.08f, 0.06f, 0.4f),   new Vector3(-0.3f, 0.6f, 0.1f),  bk);
        MakeBlock("ArmLC1",  root.transform, new Vector3(0.04f, 0.15f, 0.04f),  new Vector3(-0.5f, 0.55f, 0.2f), bd);
        MakeBlock("ArmLC2",  root.transform, new Vector3(0.04f, 0.12f, 0.04f),  new Vector3(-0.48f, 0.5f, 0.25f), bd);
        MakeBlock("ArmRB",   root.transform, new Vector3(0.08f, 0.06f, 0.4f),   new Vector3(0.3f, 0.6f, 0.1f),   bk);
        MakeBlock("ArmRC1",  root.transform, new Vector3(0.04f, 0.15f, 0.04f),  new Vector3(0.5f, 0.55f, 0.2f),  bd);
        MakeBlock("ArmRC2",  root.transform, new Vector3(0.04f, 0.12f, 0.04f),  new Vector3(0.48f, 0.5f, 0.25f),  bd);
        MakeBlock("RootL",   root.transform, new Vector3(0.1f, 0.08f, 0.2f),    new Vector3(-0.2f, 0.04f, 0.1f),  bd);
        MakeBlock("RootR",   root.transform, new Vector3(0.1f, 0.08f, 0.2f),    new Vector3(0.2f, 0.04f, 0.1f),   bd);
        MakeBlock("RootB",   root.transform, new Vector3(0.12f, 0.06f, 0.15f),  new Vector3(0f, 0.04f, -0.15f),   bd);

        return root.transform;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  6. GOLEM â€” rock humanoid, ~1.3u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildGolem(Transform parent)
    {
        Color sg = new Color(0.55f, 0.52f, 0.48f);
        Color sd = new Color(0.35f, 0.33f, 0.3f);
        Color sl = new Color(0.65f, 0.62f, 0.58f);
        Color eg = new Color(0.9f, 0.8f, 0.2f);

        var root = new GameObject("GolemModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Torso",  root.transform, new Vector3(0.4f, 0.3f, 0.25f),   new Vector3(0f, 0.7f, 0f),     sg);
        MakeBlock("Plate",  root.transform, new Vector3(0.42f, 0.1f, 0.27f),  new Vector3(0f, 0.65f, 0f),    sd);
        MakeBlock("ShoL",   root.transform, new Vector3(0.14f, 0.1f, 0.14f),  new Vector3(-0.24f, 0.82f, 0f), sl);
        MakeBlock("ShoR",   root.transform, new Vector3(0.14f, 0.1f, 0.14f),  new Vector3(0.24f, 0.82f, 0f),  sl);
        MakeBlock("Neck",   root.transform, new Vector3(0.12f, 0.06f, 0.12f), new Vector3(0f, 0.88f, 0f),   sg);
        MakeBlock("Head",   root.transform, new Vector3(0.28f, 0.24f, 0.26f), new Vector3(0f, 1.05f, 0f),   sg);
        MakeBlock("Brow",   root.transform, new Vector3(0.3f, 0.06f, 0.08f),  new Vector3(0f, 1.12f, 0.12f), sd);
        MakeBlock("EyeL",   root.transform, new Vector3(0.06f, 0.05f, 0.02f), new Vector3(-0.07f, 1.08f, 0.13f), eg);
        MakeBlock("EyeR",   root.transform, new Vector3(0.06f, 0.05f, 0.02f), new Vector3(0.07f, 1.08f, 0.13f),  eg);
        MakeBlock("Jaw",    root.transform, new Vector3(0.24f, 0.06f, 0.1f),  new Vector3(0f, 0.95f, 0.1f),   sd);

        GolemArm(root.transform, -0.28f, sg, sd, "L");
        GolemArm(root.transform,  0.28f, sg, sd, "R");
        GolemLeg(root.transform, -0.12f, sg, sd, "L");
        GolemLeg(root.transform,  0.12f, sg, sd, "R");

        return root.transform;
    }

    private static void GolemArm(Transform r, float x, Color sg, Color sd, string s)
    {
        var a = new GameObject("Arm" + s);
        a.transform.SetParent(r, false);
        a.transform.localPosition = new Vector3(x, 0.78f, 0f);
        MakeBlock("UA" + s, a.transform, new Vector3(0.12f, 0.22f, 0.12f), new Vector3(0f, -0.1f, 0f),  sg);
        MakeBlock("LA" + s, a.transform, new Vector3(0.14f, 0.2f, 0.14f),  new Vector3(0f, -0.32f, 0f), sg);
        MakeBlock("Fst" + s, a.transform, new Vector3(0.16f, 0.12f, 0.14f), new Vector3(0f, -0.46f, 0f), sd);
    }

    private static void GolemLeg(Transform r, float x, Color sg, Color sd, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(r, false);
        h.transform.localPosition = new Vector3(x, 0.5f, 0f);
        MakeBlock("UL" + s, h.transform, new Vector3(0.12f, 0.22f, 0.12f), new Vector3(0f, -0.1f, 0f), sg);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.11f, 0.2f, 0.11f),  new Vector3(0f, -0.1f, 0f),  sg);
        MakeBlock("Foot" + s, k.transform, new Vector3(0.14f, 0.06f, 0.18f), new Vector3(0f, -0.24f, 0.02f), sd);
    }


    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  7. DRAKE â€” winged lizard, ~0.6u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildDrake(Transform parent)
    {
        Color sg = new Color(0.45f, 0.42f, 0.4f);
        Color sd = new Color(0.3f, 0.28f, 0.26f);
        Color sb = new Color(0.55f, 0.5f, 0.45f);
        Color wm = new Color(0.4f, 0.25f, 0.2f);
        Color eo = new Color(0.9f, 0.5f, 0.1f);
        Color hb = new Color(0.75f, 0.7f, 0.6f);
        Color cb = new Color(0.1f, 0.08f, 0.06f);

        var root = new GameObject("DrakeModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body",  root.transform, new Vector3(0.22f, 0.18f, 0.45f), new Vector3(0f, 0.42f, 0f), sg);
        MakeBlock("Belly", root.transform, new Vector3(0.18f, 0.1f, 0.38f),  new Vector3(0f, 0.36f, 0f), sb);

        var neck = new GameObject("Neck");
        neck.transform.SetParent(root.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.48f, 0.2f);
        neck.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        MakeBlock("Nk", neck.transform, new Vector3(0.1f, 0.15f, 0.1f), new Vector3(0f, 0.08f, 0f), sg);

        var head = new GameObject("Head");
        head.transform.SetParent(neck.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.2f, 0.04f);
        MakeBlock("HB", head.transform, new Vector3(0.16f, 0.12f, 0.18f), Vector3.zero, sg);
        MakeBlock("Sn", head.transform, new Vector3(0.08f, 0.06f, 0.1f),  new Vector3(0f, 0.15f, 0.12f), sd);
        MakeBlock("El", head.transform, new Vector3(0.04f, 0.04f, 0.02f), new Vector3(-0.06f, 0.17f, 0.08f), eo);
        MakeBlock("Er", head.transform, new Vector3(0.04f, 0.04f, 0.02f), new Vector3(0.06f, 0.17f, 0.08f),  eo);
        MakeBlock("HL", head.transform, new Vector3(0.03f, 0.1f, 0.03f),  new Vector3(-0.05f, 0.22f, -0.02f), hb);
        MakeBlock("HR", head.transform, new Vector3(0.03f, 0.1f, 0.03f),  new Vector3(0.05f, 0.22f, -0.02f),  hb);

        var wL = new GameObject("WingL");
        wL.transform.SetParent(root.transform, false);
        wL.transform.localPosition = new Vector3(-0.15f, 0.55f, 0f);
        MakeBlock("WU", wL.transform, new Vector3(0.25f, 0.03f, 0.2f), new Vector3(-0.12f, 0.05f, 0f), wm);
        MakeBlock("WL", wL.transform, new Vector3(0.2f, 0.02f, 0.15f), new Vector3(-0.3f, 0.02f, 0f),  wm);

        var wR = new GameObject("WingR");
        wR.transform.SetParent(root.transform, false);
        wR.transform.localPosition = new Vector3(0.15f, 0.55f, 0f);
        MakeBlock("WU", wR.transform, new Vector3(0.25f, 0.03f, 0.2f), new Vector3(0.12f, 0.05f, 0f), wm);
        MakeBlock("WL", wR.transform, new Vector3(0.2f, 0.02f, 0.15f), new Vector3(0.3f, 0.02f, 0f),  wm);

        var tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.42f, -0.22f);
        tail.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
        MakeBlock("T1", tail.transform, new Vector3(0.08f, 0.07f, 0.2f),  new Vector3(0f, -0.02f, -0.1f),  sg);
        MakeBlock("T2", tail.transform, new Vector3(0.05f, 0.05f, 0.15f), new Vector3(0f, -0.04f, -0.25f), sd);

        DrakeLeg(root.transform, -0.08f, 0.12f, sg, sd, cb, "FL");
        DrakeLeg(root.transform,  0.08f, 0.12f, sg, sd, cb, "FR");
        DrakeLeg(root.transform, -0.08f, -0.12f, sg, sd, cb, "BL");
        DrakeLeg(root.transform,  0.08f, -0.12f, sg, sd, cb, "BR");

        return root.transform;
    }

    private static void DrakeLeg(Transform r, float x, float z, Color sg, Color sd, Color cb, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(r, false);
        h.transform.localPosition = new Vector3(x, 0.32f, z);
        MakeBlock("UL" + s, h.transform, new Vector3(0.06f, 0.12f, 0.06f), new Vector3(0f, -0.06f, 0f), sg);
        MakeBlock("LL" + s, h.transform, new Vector3(0.05f, 0.1f, 0.05f),  new Vector3(0f, -0.18f, 0f), sd);
        MakeBlock("Cl" + s, h.transform, new Vector3(0.07f, 0.03f, 0.08f), new Vector3(0f, -0.26f, 0.02f), cb);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  8. UNDEAD â€” shambling zombie, ~0.95u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildUndead(Transform parent)
    {
        Color sp = new Color(0.6f, 0.58f, 0.5f);
        Color sd = new Color(0.45f, 0.42f, 0.38f);
        Color cr = new Color(0.3f, 0.28f, 0.25f);
        Color eg = new Color(0.3f, 0.8f, 0.3f);
        Color bw = new Color(0.85f, 0.82f, 0.75f);

        var root = new GameObject("UndeadModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Torso",  root.transform, new Vector3(0.28f, 0.22f, 0.16f), new Vector3(0f, 0.55f, 0f),    sd);
        MakeBlock("Rib1",   root.transform, new Vector3(0.26f, 0.08f, 0.04f), new Vector3(0f, 0.58f, 0.08f),  bw);
        MakeBlock("Rib2",   root.transform, new Vector3(0.22f, 0.06f, 0.04f), new Vector3(0f, 0.52f, 0.08f),  bw);
        MakeBlock("Cloth",  root.transform, new Vector3(0.3f, 0.14f, 0.04f),  new Vector3(0f, 0.5f, 0.09f),   cr);
        MakeBlock("Neck",   root.transform, new Vector3(0.07f, 0.05f, 0.07f), new Vector3(0f, 0.68f, 0f),   sp);
        MakeBlock("Head",   root.transform, new Vector3(0.2f, 0.2f, 0.2f),    new Vector3(0f, 0.82f, 0f),   sd);
        MakeBlock("EyeL",   root.transform, new Vector3(0.05f, 0.05f, 0.02f), new Vector3(-0.05f, 0.84f, 0.1f), eg);
        MakeBlock("EyeR",   root.transform, new Vector3(0.05f, 0.05f, 0.02f), new Vector3(0.05f, 0.84f, 0.1f),  eg);
        MakeBlock("Mouth",  root.transform, new Vector3(0.1f, 0.04f, 0.02f),  new Vector3(0f, 0.76f, 0.1f),  cr);
        MakeBlock("Jaw",    root.transform, new Vector3(0.12f, 0.04f, 0.06f), new Vector3(0f, 0.73f, 0.06f), sd);

        var aL = new GameObject("ArmL");
        aL.transform.SetParent(root.transform, false);
        aL.transform.localPosition = new Vector3(-0.18f, 0.6f, 0f);
        MakeBlock("UA", aL.transform, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(0f, -0.07f, 0f), sd);
        MakeBlock("LA", aL.transform, new Vector3(0.05f, 0.14f, 0.05f), new Vector3(0f, -0.22f, 0f), sp);
        MakeBlock("Ha", aL.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0f, -0.32f, 0f), sd);

        var aR = new GameObject("ArmR");
        aR.transform.SetParent(root.transform, false);
        aR.transform.localPosition = new Vector3(0.18f, 0.6f, 0f);
        aR.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        MakeBlock("UA", aR.transform, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(0f, -0.07f, 0f), sd);
        MakeBlock("LA", aR.transform, new Vector3(0.05f, 0.14f, 0.05f), new Vector3(0f, -0.22f, 0f), sp);
        MakeBlock("Ha", aR.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0f, -0.32f, 0f), sd);

        HumanLeg(root.transform, -0.07f, cr, sd, "L");
        HumanLeg(root.transform,  0.07f, cr, sd, "R");

        return root.transform;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  9. SLUG â€” giant slug, no legs, ~0.3u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildSlug(Transform parent)
    {
        Color sp = new Color(0.5f, 0.3f, 0.55f);
        Color sd = new Color(0.35f, 0.2f, 0.4f);
        Color sb = new Color(0.6f, 0.45f, 0.6f);
        Color ss = new Color(0.65f, 0.55f, 0.7f);
        Color eb = new Color(0.05f, 0.05f, 0.05f);

        var root = new GameObject("SlugModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body", root.transform, new Vector3(0.3f, 0.2f, 0.55f),   new Vector3(0f, 0.12f, 0f),  sp);
        MakeBlock("Top",  root.transform, new Vector3(0.22f, 0.1f, 0.4f),   new Vector3(0f, 0.22f, 0f),  sp);
        MakeBlock("Bel",  root.transform, new Vector3(0.28f, 0.06f, 0.5f),  new Vector3(0f, 0.04f, 0f),  sb);
        MakeBlock("Slim", root.transform, new Vector3(0.18f, 0.04f, 0.3f),  new Vector3(0f, 0.26f, 0f),  ss);
        MakeBlock("Mouth", root.transform, new Vector3(0.08f, 0.03f, 0.02f), new Vector3(0f, 0.08f, 0.28f), sd);
        MakeBlock("Trail", root.transform, new Vector3(0.15f, 0.02f, 0.3f),  new Vector3(0f, 0.02f, -0.3f), ss);

        SlugStalk(root.transform, -0.06f, sp, sd, eb, "L");
        SlugStalk(root.transform,  0.06f, sp, sd, eb, "R");

        return root.transform;
    }

    private static void SlugStalk(Transform r, float x, Color sp, Color sd, Color eb, string s)
    {
        var st = new GameObject("Stk" + s);
        st.transform.SetParent(r, false);
        st.transform.localPosition = new Vector3(x, 0.25f, 0.18f);
        MakeBlock("St" + s,  st.transform, new Vector3(0.02f, 0.12f, 0.02f), new Vector3(0f, 0.06f, 0f), sd);
        MakeBlock("EB" + s,  st.transform, new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0f, 0.14f, 0f), sp);
        MakeBlock("Pu" + s,  st.transform, new Vector3(0.02f, 0.02f, 0.02f), new Vector3(0f, 0.14f, 0.03f), eb);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  10. SCORPION â€” 8 legs, pincers, stinger, ~0.35u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildScorpion(Transform parent)
    {
        Color sr = new Color(0.6f, 0.2f, 0.1f);
        Color sd = new Color(0.4f, 0.12f, 0.06f);
        Color so = new Color(0.75f, 0.35f, 0.15f);
        Color eb = new Color(0.05f, 0.05f, 0.05f);
        Color sy = new Color(0.85f, 0.75f, 0.2f);

        var root = new GameObject("ScorpionModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body",  root.transform, new Vector3(0.2f, 0.12f, 0.3f),  new Vector3(0f, 0.2f, 0f),  sr);
        MakeBlock("Plate", root.transform, new Vector3(0.22f, 0.06f, 0.28f), new Vector3(0f, 0.26f, 0f), sd);
        MakeBlock("Head",  root.transform, new Vector3(0.12f, 0.08f, 0.1f),  new Vector3(0f, 0.22f, 0.18f), sr);
        MakeBlock("EyeL",  root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(-0.04f, 0.25f, 0.23f), eb);
        MakeBlock("EyeR",  root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(0.04f, 0.25f, 0.23f),  eb);

        ScorpionClaw(root.transform, -0.14f, so, sr, "L");
        ScorpionClaw(root.transform,  0.14f, so, sr, "R");

        var tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.22f, -0.16f);
        tail.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        MakeBlock("T1", tail.transform, new Vector3(0.05f, 0.05f, 0.12f), new Vector3(0f, 0.05f, -0.05f),  sr);
        MakeBlock("T2", tail.transform, new Vector3(0.04f, 0.04f, 0.1f),  new Vector3(0f, 0.14f, -0.1f),   sd);
        MakeBlock("Stg", tail.transform, new Vector3(0.03f, 0.06f, 0.03f), new Vector3(0f, 0.2f, -0.14f),   sy);

        ScorpionLeg(root.transform, -0.14f, 0.1f,  sd, "FL");
        ScorpionLeg(root.transform,  0.14f, 0.1f,  sd, "FR");
        ScorpionLeg(root.transform, -0.14f, 0f,    sd, "ML");
        ScorpionLeg(root.transform,  0.14f, 0f,    sd, "MR");
        ScorpionLeg(root.transform, -0.14f, -0.1f, sd, "BL");
        ScorpionLeg(root.transform,  0.14f, -0.1f, sd, "BR");

        return root.transform;
    }

    private static void ScorpionClaw(Transform r, float x, Color so, Color sr, string s)
    {
        var c = new GameObject("Claw" + s);
        c.transform.SetParent(r, false);
        c.transform.localPosition = new Vector3(x, 0.22f, 0.12f);
        float dx = x > 0 ? -0.06f : 0.06f;
        float dx2 = x > 0 ? -0.1f : 0.1f;
        MakeBlock("AU" + s, c.transform, new Vector3(0.04f, 0.04f, 0.15f), new Vector3(dx, 0f, 0.06f),  so);
        MakeBlock("AL" + s, c.transform, new Vector3(0.04f, 0.04f, 0.1f),  new Vector3(dx2, 0f, 0.18f), so);
        MakeBlock("Pn" + s, c.transform, new Vector3(0.08f, 0.06f, 0.06f), new Vector3(dx2, 0f, 0.26f), sr);
    }

    private static void ScorpionLeg(Transform r, float x, float z, Color sd, string s)
    {
        MakeBlock("Leg" + s, r.transform, new Vector3(0.03f, 0.04f, 0.12f), new Vector3(x, 0.14f, z), sd);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  11. MUMMY â€” wrapped humanoid, ~0.95u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildMummy(Transform parent)
    {
        Color bt = new Color(0.75f, 0.68f, 0.5f);
        Color bd = new Color(0.6f, 0.52f, 0.38f);
        Color bl = new Color(0.55f, 0.48f, 0.35f);
        Color eg = new Color(0.9f, 0.8f, 0.15f);
        Color sd = new Color(0.4f, 0.38f, 0.3f);

        var root = new GameObject("MummyModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Torso",  root.transform, new Vector3(0.28f, 0.22f, 0.18f), new Vector3(0f, 0.55f, 0f),    bt);
        MakeBlock("WrCh",   root.transform, new Vector3(0.3f, 0.08f, 0.04f),  new Vector3(0f, 0.58f, 0.09f), bd);
        MakeBlock("WrWa",   root.transform, new Vector3(0.3f, 0.06f, 0.19f),  new Vector3(0f, 0.44f, 0f),    bl);
        MakeBlock("Neck",   root.transform, new Vector3(0.08f, 0.05f, 0.08f), new Vector3(0f, 0.68f, 0f),  bt);
        MakeBlock("Head",   root.transform, new Vector3(0.2f, 0.2f, 0.2f),    new Vector3(0f, 0.82f, 0f),  bt);
        MakeBlock("WrHd",   root.transform, new Vector3(0.22f, 0.08f, 0.22f), new Vector3(0f, 0.88f, 0f),  bd);
        MakeBlock("EyeL",   root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(-0.05f, 0.82f, 0.1f), eg);
        MakeBlock("EyeR",   root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.05f, 0.82f, 0.1f),  eg);
        MakeBlock("WrFc",   root.transform, new Vector3(0.2f, 0.04f, 0.02f),  new Vector3(0f, 0.78f, 0.1f), bd);

        var aL = new GameObject("ArmL");
        aL.transform.SetParent(root.transform, false);
        aL.transform.localPosition = new Vector3(-0.18f, 0.6f, 0f);
        aL.transform.localRotation = Quaternion.Euler(-15f, 0f, 10f);
        MakeBlock("UA", aL.transform, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(0f, -0.07f, 0f), bt);
        MakeBlock("LA", aL.transform, new Vector3(0.05f, 0.14f, 0.05f), new Vector3(0f, -0.22f, 0f), bt);
        MakeBlock("Ha", aL.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0f, -0.32f, 0f), sd);

        var aR = new GameObject("ArmR");
        aR.transform.SetParent(root.transform, false);
        aR.transform.localPosition = new Vector3(0.18f, 0.6f, 0f);
        aR.transform.localRotation = Quaternion.Euler(-15f, 0f, -10f);
        MakeBlock("UA", aR.transform, new Vector3(0.06f, 0.16f, 0.06f), new Vector3(0f, -0.07f, 0f), bt);
        MakeBlock("LA", aR.transform, new Vector3(0.05f, 0.14f, 0.05f), new Vector3(0f, -0.22f, 0f), bt);
        MakeBlock("Ha", aR.transform, new Vector3(0.06f, 0.06f, 0.04f), new Vector3(0f, -0.32f, 0f), sd);

        MummyLeg(root.transform, -0.07f, bt, bd, "L");
        MummyLeg(root.transform,  0.07f, bt, bd, "R");

        return root.transform;
    }

    private static void MummyLeg(Transform r, float x, Color bt, Color bd, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(r, false);
        h.transform.localPosition = new Vector3(x, 0.35f, 0f);
        MakeBlock("UL" + s, h.transform, new Vector3(0.07f, 0.18f, 0.07f), new Vector3(0f, -0.08f, 0f), bt);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0f, -0.07f, 0f), bt);
        MakeBlock("Ft" + s, k.transform, new Vector3(0.08f, 0.04f, 0.1f),  new Vector3(0f, -0.16f, 0.02f), bd);
    }


    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  12. YETI â€” large white ape, ~1.2u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildYeti(Transform parent)
    {
        Color fw = new Color(0.88f, 0.86f, 0.82f);
        Color fg = new Color(0.7f, 0.68f, 0.65f);
        Color sb = new Color(0.55f, 0.6f, 0.7f);
        Color ed = new Color(0.08f, 0.06f, 0.04f);
        Color nb = new Color(0.1f, 0.1f, 0.1f);
        Color cb = new Color(0.85f, 0.8f, 0.7f);

        var root = new GameObject("YetiModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Torso",  root.transform, new Vector3(0.38f, 0.28f, 0.24f), new Vector3(0f, 0.7f, 0f),    fw);
        MakeBlock("Chest",  root.transform, new Vector3(0.36f, 0.15f, 0.06f), new Vector3(0f, 0.72f, 0.12f), fg);
        MakeBlock("Neck",   root.transform, new Vector3(0.12f, 0.06f, 0.12f), new Vector3(0f, 0.88f, 0f),  fw);
        MakeBlock("Head",   root.transform, new Vector3(0.26f, 0.22f, 0.24f), new Vector3(0f, 1.02f, 0f),  fw);
        MakeBlock("Brow",   root.transform, new Vector3(0.28f, 0.06f, 0.08f), new Vector3(0f, 1.08f, 0.1f), fg);
        MakeBlock("EyeL",   root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(-0.06f, 1.04f, 0.12f), ed);
        MakeBlock("EyeR",   root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.06f, 1.04f, 0.12f),  ed);
        MakeBlock("Nose",   root.transform, new Vector3(0.06f, 0.05f, 0.04f), new Vector3(0f, 1.0f, 0.12f),   nb);
        MakeBlock("Mouth",  root.transform, new Vector3(0.1f, 0.02f, 0.02f),  new Vector3(0f, 0.95f, 0.12f),  sb);

        YetiArm(root.transform, -0.24f, fw, sb, cb, "L");
        YetiArm(root.transform,  0.24f, fw, sb, cb, "R");
        YetiLeg(root.transform, -0.12f, fw, fg, "L");
        YetiLeg(root.transform,  0.12f, fw, fg, "R");

        return root.transform;
    }

    private static void YetiArm(Transform r, float x, Color fw, Color sb, Color cb, string s)
    {
        var a = new GameObject("Arm" + s);
        a.transform.SetParent(r, false);
        a.transform.localPosition = new Vector3(x, 0.82f, 0f);
        MakeBlock("UA" + s, a.transform, new Vector3(0.1f, 0.22f, 0.1f),  new Vector3(0f, -0.1f, 0f),  fw);
        MakeBlock("LA" + s, a.transform, new Vector3(0.09f, 0.2f, 0.09f), new Vector3(0f, -0.32f, 0f), fw);
        MakeBlock("Ha" + s, a.transform, new Vector3(0.11f, 0.1f, 0.1f),  new Vector3(0f, -0.46f, 0f), sb);
        MakeBlock("C1" + s, a.transform, new Vector3(0.02f, 0.06f, 0.02f), new Vector3(-0.04f, -0.52f, 0.03f), cb);
        MakeBlock("C2" + s, a.transform, new Vector3(0.02f, 0.06f, 0.02f), new Vector3(0f, -0.52f, 0.03f),     cb);
        MakeBlock("C3" + s, a.transform, new Vector3(0.02f, 0.06f, 0.02f), new Vector3(0.04f, -0.52f, 0.03f),  cb);
    }

    private static void YetiLeg(Transform r, float x, Color fw, Color fg, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(r, false);
        h.transform.localPosition = new Vector3(x, 0.52f, 0f);
        MakeBlock("UL" + s, h.transform, new Vector3(0.12f, 0.22f, 0.12f), new Vector3(0f, -0.1f, 0f), fw);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.11f, 0.18f, 0.11f), new Vector3(0f, -0.09f, 0f), fg);
        MakeBlock("Ft" + s, k.transform, new Vector3(0.14f, 0.06f, 0.18f), new Vector3(0f, -0.22f, 0.02f), fg);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  13. ICE WOLF â€” white wolf + ice crystals, ~0.5u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildIceWolf(Transform parent)
    {
        Color fw = new Color(0.85f, 0.88f, 0.92f);
        Color fi = new Color(0.7f, 0.78f, 0.88f);
        Color fb = new Color(0.9f, 0.92f, 0.95f);
        Color eb = new Color(0.3f, 0.6f, 0.95f);
        Color nd = new Color(0.15f, 0.18f, 0.22f);
        Color tw = new Color(0.92f, 0.92f, 0.95f);
        Color ic = new Color(0.6f, 0.8f, 1f);

        var root = new GameObject("IceWolfModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body",  root.transform, new Vector3(0.25f, 0.2f, 0.55f), new Vector3(0f, 0.45f, 0f),  fw);
        MakeBlock("Belly", root.transform, new Vector3(0.2f, 0.1f, 0.45f),  new Vector3(0f, 0.38f, 0f),  fb);
        MakeBlock("CrysL", root.transform, new Vector3(0.04f, 0.12f, 0.04f), new Vector3(-0.12f, 0.6f, 0f), ic);
        MakeBlock("CrysR", root.transform, new Vector3(0.04f, 0.12f, 0.04f), new Vector3(0.12f, 0.6f, 0f),  ic);

        var neck = new GameObject("Neck");
        neck.transform.SetParent(root.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.5f, 0.22f);
        neck.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        MakeBlock("Nk", neck.transform, new Vector3(0.15f, 0.18f, 0.12f), new Vector3(0f, 0.08f, 0f), fw);
        MakeBlock("Hd", neck.transform, new Vector3(0.18f, 0.14f, 0.2f),  new Vector3(0f, 0.2f, 0.04f), fw);
        MakeBlock("Sn", neck.transform, new Vector3(0.1f, 0.08f, 0.12f),  new Vector3(0f, 0.15f, 0.16f), fi);
        MakeBlock("No", neck.transform, new Vector3(0.06f, 0.04f, 0.02f), new Vector3(0f, 0.17f, 0.22f), nd);
        MakeBlock("El", neck.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(-0.07f, 0.22f, 0.12f), eb);
        MakeBlock("Er", neck.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.07f, 0.22f, 0.12f),  eb);
        MakeBlock("EL", neck.transform, new Vector3(0.04f, 0.1f, 0.04f),  new Vector3(-0.06f, 0.28f, 0f), fi);
        MakeBlock("ER", neck.transform, new Vector3(0.04f, 0.1f, 0.04f),  new Vector3(0.06f, 0.28f, 0f),  fi);
        MakeBlock("JL", neck.transform, new Vector3(0.08f, 0.03f, 0.1f),  new Vector3(-0.03f, 0.1f, 0.14f), tw);
        MakeBlock("JR", neck.transform, new Vector3(0.08f, 0.03f, 0.1f),  new Vector3(0.03f, 0.1f, 0.14f),  tw);

        var tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.5f, -0.28f);
        tail.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        MakeBlock("T1", tail.transform, new Vector3(0.06f, 0.06f, 0.25f), new Vector3(0f, -0.05f, -0.1f),  fw);
        MakeBlock("T2", tail.transform, new Vector3(0.04f, 0.04f, 0.1f),  new Vector3(0f, -0.08f, -0.22f), fi);

        WolfLeg(root.transform, -0.08f, 0.15f, fw, fi, "FL");
        WolfLeg(root.transform,  0.08f, 0.15f, fw, fi, "FR");
        WolfLeg(root.transform, -0.08f, -0.15f, fw, fi, "BL");
        WolfLeg(root.transform,  0.08f, -0.15f, fw, fi, "BR");

        return root.transform;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  14. FIRE ELEMENTAL â€” flame humanoid, ~1.0u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildFireElemental(Transform parent)
    {
        Color fc = new Color(1f, 0.45f, 0.05f);
        Color fm = new Color(1f, 0.65f, 0.1f);
        Color fo = new Color(0.9f, 0.25f, 0.02f);
        Color eg = new Color(1f, 0.8f, 0.2f);
        Color ey = new Color(1f, 0.9f, 0.3f);

        var root = new GameObject("FireElementalModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Core",     root.transform, new Vector3(0.22f, 0.3f, 0.18f),  new Vector3(0f, 0.55f, 0f),  fc);
        MakeBlock("Glow",     root.transform, new Vector3(0.16f, 0.2f, 0.12f),  new Vector3(0f, 0.55f, 0f),  eg);
        MakeBlock("Head",     root.transform, new Vector3(0.2f, 0.22f, 0.2f),   new Vector3(0f, 0.85f, 0f),  fm);
        MakeBlock("Flm1",     root.transform, new Vector3(0.06f, 0.18f, 0.06f), new Vector3(-0.04f, 1.05f, 0f), fo);
        MakeBlock("Flm2",     root.transform, new Vector3(0.05f, 0.22f, 0.05f), new Vector3(0.04f, 1.08f, 0f),  fo);
        MakeBlock("Flm3",     root.transform, new Vector3(0.04f, 0.15f, 0.04f), new Vector3(0f, 1.1f, 0.02f),    fo);
        MakeBlock("EyeL",     root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(-0.05f, 0.88f, 0.1f), ey);
        MakeBlock("EyeR",     root.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.05f, 0.88f, 0.1f),  ey);

        var aL = new GameObject("ArmL");
        aL.transform.SetParent(root.transform, false);
        aL.transform.localPosition = new Vector3(-0.16f, 0.65f, 0f);
        MakeBlock("UA", aL.transform, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(0f, -0.07f, 0f), fm);
        MakeBlock("LA", aL.transform, new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0f, -0.22f, 0f), fc);
        MakeBlock("Ha", aL.transform, new Vector3(0.08f, 0.08f, 0.06f), new Vector3(0f, -0.32f, 0f), fo);

        var aR = new GameObject("ArmR");
        aR.transform.SetParent(root.transform, false);
        aR.transform.localPosition = new Vector3(0.16f, 0.65f, 0f);
        MakeBlock("UA", aR.transform, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(0f, -0.07f, 0f), fm);
        MakeBlock("LA", aR.transform, new Vector3(0.06f, 0.14f, 0.06f), new Vector3(0f, -0.22f, 0f), fc);
        MakeBlock("Ha", aR.transform, new Vector3(0.08f, 0.08f, 0.06f), new Vector3(0f, -0.32f, 0f), fo);

        var lL = new GameObject("LegL");
        lL.transform.SetParent(root.transform, false);
        lL.transform.localPosition = new Vector3(-0.07f, 0.38f, 0f);
        MakeBlock("UL", lL.transform, new Vector3(0.08f, 0.16f, 0.08f), new Vector3(0f, -0.07f, 0f), fm);
        MakeBlock("LL", lL.transform, new Vector3(0.07f, 0.14f, 0.07f), new Vector3(0f, -0.24f, 0f), fc);

        var lR = new GameObject("LegR");
        lR.transform.SetParent(root.transform, false);
        lR.transform.localPosition = new Vector3(0.07f, 0.38f, 0f);
        MakeBlock("UL", lR.transform, new Vector3(0.08f, 0.16f, 0.08f), new Vector3(0f, -0.07f, 0f), fm);
        MakeBlock("LL", lR.transform, new Vector3(0.07f, 0.14f, 0.07f), new Vector3(0f, -0.24f, 0f), fc);

        MakeBlock("BF1", root.transform, new Vector3(0.08f, 0.15f, 0.04f), new Vector3(0f, 0.6f, -0.12f), fo);
        MakeBlock("BF2", root.transform, new Vector3(0.06f, 0.12f, 0.04f), new Vector3(0f, 0.7f, -0.1f),  fo);

        return root.transform;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  15. DRAGON â€” large winged dragon, ~1.0u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildDragon(Transform parent)
    {
        Color sr = new Color(0.6f, 0.15f, 0.08f);
        Color sd = new Color(0.4f, 0.08f, 0.04f);
        Color sb = new Color(0.75f, 0.4f, 0.15f);
        Color wm = new Color(0.5f, 0.12f, 0.06f);
        Color ey = new Color(0.95f, 0.85f, 0.15f);
        Color hb = new Color(0.7f, 0.6f, 0.45f);
        Color cb = new Color(0.08f, 0.06f, 0.04f);

        var root = new GameObject("DragonModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body",  root.transform, new Vector3(0.3f, 0.25f, 0.5f),   new Vector3(0f, 0.6f, 0f),  sr);
        MakeBlock("Belly", root.transform, new Vector3(0.25f, 0.12f, 0.42f), new Vector3(0f, 0.52f, 0f), sb);
        MakeBlock("Ridge", root.transform, new Vector3(0.32f, 0.06f, 0.48f), new Vector3(0f, 0.72f, 0f), sd);

        var neck = new GameObject("Neck");
        neck.transform.SetParent(root.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.7f, 0.25f);
        neck.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
        MakeBlock("Nk", neck.transform, new Vector3(0.12f, 0.2f, 0.12f), new Vector3(0f, 0.1f, 0f), sr);

        var head = new GameObject("Head");
        head.transform.SetParent(neck.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.25f, 0.05f);
        MakeBlock("HB", head.transform, new Vector3(0.2f, 0.15f, 0.25f), Vector3.zero, sr);
        MakeBlock("Sn", head.transform, new Vector3(0.12f, 0.08f, 0.15f), new Vector3(0f, 0.18f, 0.16f), sd);
        MakeBlock("JL", head.transform, new Vector3(0.1f, 0.04f, 0.12f),  new Vector3(-0.03f, 0.12f, 0.18f), sd);
        MakeBlock("JR", head.transform, new Vector3(0.1f, 0.04f, 0.12f),  new Vector3(0.03f, 0.12f, 0.18f),  sd);
        MakeBlock("El", head.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(-0.08f, 0.2f, 0.1f),   ey);
        MakeBlock("Er", head.transform, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.08f, 0.2f, 0.1f),    ey);
        MakeBlock("HL", head.transform, new Vector3(0.04f, 0.15f, 0.04f), new Vector3(-0.06f, 0.28f, -0.04f), hb);
        MakeBlock("HR", head.transform, new Vector3(0.04f, 0.15f, 0.04f), new Vector3(0.06f, 0.28f, -0.04f),  hb);

        var wL = new GameObject("WingL");
        wL.transform.SetParent(root.transform, false);
        wL.transform.localPosition = new Vector3(-0.2f, 0.78f, 0f);
        MakeBlock("WU", wL.transform, new Vector3(0.35f, 0.03f, 0.3f), new Vector3(-0.18f, 0.08f, 0f), wm);
        MakeBlock("WL", wL.transform, new Vector3(0.25f, 0.02f, 0.2f), new Vector3(-0.4f, 0.04f, 0f),  wm);

        var wR = new GameObject("WingR");
        wR.transform.SetParent(root.transform, false);
        wR.transform.localPosition = new Vector3(0.2f, 0.78f, 0f);
        MakeBlock("WU", wR.transform, new Vector3(0.35f, 0.03f, 0.3f), new Vector3(0.18f, 0.08f, 0f), wm);
        MakeBlock("WL", wR.transform, new Vector3(0.25f, 0.02f, 0.2f), new Vector3(0.4f, 0.04f, 0f),  wm);

        var tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.6f, -0.25f);
        tail.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        MakeBlock("T1", tail.transform, new Vector3(0.1f, 0.1f, 0.25f),  new Vector3(0f, -0.02f, -0.12f), sr);
        MakeBlock("T2", tail.transform, new Vector3(0.07f, 0.07f, 0.2f), new Vector3(0f, -0.04f, -0.32f), sd);
        MakeBlock("TS", tail.transform, new Vector3(0.04f, 0.08f, 0.04f), new Vector3(0f, -0.05f, -0.45f), hb);

        DragonLeg(root.transform, -0.1f, 0.15f, sr, sd, cb, "FL");
        DragonLeg(root.transform,  0.1f, 0.15f, sr, sd, cb, "FR");
        DragonLeg(root.transform, -0.1f, -0.15f, sr, sd, cb, "BL");
        DragonLeg(root.transform,  0.1f, -0.15f, sr, sd, cb, "BR");

        return root.transform;
    }

    private static void DragonLeg(Transform r, float x, float z, Color sr, Color sd, Color cb, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(r, false);
        h.transform.localPosition = new Vector3(x, 0.42f, z);
        MakeBlock("UL" + s, h.transform, new Vector3(0.08f, 0.16f, 0.08f), new Vector3(0f, -0.07f, 0f), sr);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.16f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.07f, 0.14f, 0.07f), new Vector3(0f, -0.07f, 0f), sd);
        MakeBlock("Cl" + s, k.transform, new Vector3(0.1f, 0.04f, 0.1f),   new Vector3(0f, -0.18f, 0.02f), cb);
    }


    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  16. DEMON â€” horned humanoid, ~1.1u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildDemon(Transform parent)
    {
        Color sd = new Color(0.2f, 0.08f, 0.1f);
        Color sm = new Color(0.3f, 0.1f, 0.12f);
        Color hb = new Color(0.65f, 0.55f, 0.4f);
        Color er = new Color(0.95f, 0.15f, 0.05f);
        Color cb = new Color(0.08f, 0.05f, 0.04f);
        Color tw = new Color(0.9f, 0.88f, 0.85f);

        var root = new GameObject("DemonModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Torso",  root.transform, new Vector3(0.3f, 0.24f, 0.18f),  new Vector3(0f, 0.6f, 0f),   sd);
        MakeBlock("Chest",  root.transform, new Vector3(0.28f, 0.12f, 0.06f),  new Vector3(0f, 0.62f, 0.08f), sm);
        MakeBlock("Neck",   root.transform, new Vector3(0.08f, 0.05f, 0.08f),  new Vector3(0f, 0.74f, 0f), sd);
        MakeBlock("Head",   root.transform, new Vector3(0.22f, 0.2f, 0.22f),   new Vector3(0f, 0.9f, 0f),  sd);
        MakeBlock("HornL",  root.transform, new Vector3(0.04f, 0.2f, 0.04f),   new Vector3(-0.08f, 1.05f, -0.02f), hb);
        MakeBlock("HornR",  root.transform, new Vector3(0.04f, 0.2f, 0.04f),   new Vector3(0.08f, 1.05f, -0.02f),  hb);
        MakeBlock("EyeL",   root.transform, new Vector3(0.05f, 0.04f, 0.02f),  new Vector3(-0.06f, 0.92f, 0.11f), er);
        MakeBlock("EyeR",   root.transform, new Vector3(0.05f, 0.04f, 0.02f),  new Vector3(0.06f, 0.92f, 0.11f),  er);
        MakeBlock("Mouth",  root.transform, new Vector3(0.12f, 0.04f, 0.02f),  new Vector3(0f, 0.84f, 0.11f),     cb);
        MakeBlock("TthL",   root.transform, new Vector3(0.03f, 0.04f, 0.02f),  new Vector3(-0.04f, 0.82f, 0.11f), tw);
        MakeBlock("TthR",   root.transform, new Vector3(0.03f, 0.04f, 0.02f),  new Vector3(0.04f, 0.82f, 0.11f),  tw);
        MakeBlock("Brow",   root.transform, new Vector3(0.24f, 0.04f, 0.04f),  new Vector3(0f, 0.96f, 0.1f),       sm);

        DemonArm(root.transform, -0.2f, sd, sm, cb, "L");
        DemonArm(root.transform,  0.2f, sd, sm, cb, "R");
        DemonLeg(root.transform, -0.08f, sd, cb, "L");
        DemonLeg(root.transform,  0.08f, sd, cb, "R");

        var tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.45f, -0.12f);
        tail.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
        MakeBlock("T1", tail.transform, new Vector3(0.06f, 0.06f, 0.2f),  new Vector3(0f, -0.02f, -0.1f),  sd);
        MakeBlock("T2", tail.transform, new Vector3(0.04f, 0.04f, 0.15f), new Vector3(0f, -0.04f, -0.25f), sm);
        MakeBlock("TS", tail.transform, new Vector3(0.03f, 0.08f, 0.03f), new Vector3(0f, -0.05f, -0.35f), cb);

        return root.transform;
    }

    private static void DemonArm(Transform r, float x, Color sd, Color sm, Color cb, string s)
    {
        var a = new GameObject("Arm" + s);
        a.transform.SetParent(r, false);
        a.transform.localPosition = new Vector3(x, 0.68f, 0f);
        MakeBlock("UA" + s, a.transform, new Vector3(0.08f, 0.18f, 0.08f), new Vector3(0f, -0.08f, 0f), sd);
        MakeBlock("LA" + s, a.transform, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(0f, -0.26f, 0f), sd);
        MakeBlock("Ha" + s, a.transform, new Vector3(0.08f, 0.08f, 0.06f), new Vector3(0f, -0.36f, 0f), sm);
        MakeBlock("C1" + s, a.transform, new Vector3(0.02f, 0.07f, 0.02f), new Vector3(-0.03f, -0.42f, 0.02f), cb);
        MakeBlock("C2" + s, a.transform, new Vector3(0.02f, 0.07f, 0.02f), new Vector3(0f, -0.42f, 0.02f),     cb);
        MakeBlock("C3" + s, a.transform, new Vector3(0.02f, 0.07f, 0.02f), new Vector3(0.03f, -0.42f, 0.02f),  cb);
    }

    private static void DemonLeg(Transform r, float x, Color sd, Color cb, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(r, false);
        h.transform.localPosition = new Vector3(x, 0.4f, 0f);
        MakeBlock("UL" + s, h.transform, new Vector3(0.09f, 0.2f, 0.09f), new Vector3(0f, -0.09f, 0f), sd);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.08f, 0.16f, 0.08f), new Vector3(0f, -0.08f, 0f), sd);
        MakeBlock("Hf" + s, k.transform, new Vector3(0.1f, 0.05f, 0.12f),  new Vector3(0f, -0.2f, 0.02f), cb);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  17. MIMIC â€” chest monster, ~0.4u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildMimic(Transform parent)
    {
        Color wb = new Color(0.45f, 0.3f, 0.15f);
        Color wd = new Color(0.3f, 0.2f, 0.1f);
        Color mg = new Color(0.85f, 0.7f, 0.2f);
        Color md = new Color(0.5f, 0.4f, 0.15f);
        Color ir = new Color(0.6f, 0.1f, 0.08f);
        Color ey = new Color(0.95f, 0.85f, 0.15f);
        Color tw = new Color(0.9f, 0.9f, 0.88f);

        var root = new GameObject("MimicModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Base", root.transform, new Vector3(0.4f, 0.18f, 0.3f),  new Vector3(0f, 0.1f, 0f),  wb);
        MakeBlock("Rim",  root.transform, new Vector3(0.42f, 0.04f, 0.32f), new Vector3(0f, 0.2f, 0f),  mg);

        var lid = new GameObject("Lid");
        lid.transform.SetParent(root.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.2f, -0.15f);
        lid.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        MakeBlock("Lid",  lid.transform, new Vector3(0.4f, 0.1f, 0.3f),  new Vector3(0f, 0.05f, 0.15f), wb);
        MakeBlock("LidR", lid.transform, new Vector3(0.42f, 0.02f, 0.32f), new Vector3(0f, 0.1f, 0.15f), mg);
        MakeBlock("Clasp", lid.transform, new Vector3(0.06f, 0.04f, 0.02f), new Vector3(0f, 0.02f, 0.3f), md);

        MakeBlock("Intr", root.transform, new Vector3(0.36f, 0.12f, 0.26f), new Vector3(0f, 0.12f, 0f), ir);
        MakeBlock("EyeL", root.transform, new Vector3(0.06f, 0.05f, 0.02f), new Vector3(-0.1f, 0.22f, 0.15f), ey);
        MakeBlock("EyeR", root.transform, new Vector3(0.06f, 0.05f, 0.02f), new Vector3(0.1f, 0.22f, 0.15f),  ey);

        MakeBlock("Jaw",   root.transform, new Vector3(0.36f, 0.06f, 0.15f), new Vector3(0f, 0.18f, 0.15f),  wd);
        MakeBlock("Tl1",   root.transform, new Vector3(0.03f, 0.04f, 0.02f), new Vector3(-0.12f, 0.22f, 0.28f), tw);
        MakeBlock("Tl2",   root.transform, new Vector3(0.03f, 0.04f, 0.02f), new Vector3(-0.04f, 0.22f, 0.28f), tw);
        MakeBlock("Tr1",   root.transform, new Vector3(0.03f, 0.04f, 0.02f), new Vector3(0.04f, 0.22f, 0.28f),  tw);
        MakeBlock("Tr2",   root.transform, new Vector3(0.03f, 0.04f, 0.02f), new Vector3(0.12f, 0.22f, 0.28f),  tw);

        MakeBlock("LegFL", root.transform, new Vector3(0.06f, 0.08f, 0.06f), new Vector3(-0.16f, 0f, 0.1f),  md);
        MakeBlock("LegFR", root.transform, new Vector3(0.06f, 0.08f, 0.06f), new Vector3(0.16f, 0f, 0.1f),   md);
        MakeBlock("LegBL", root.transform, new Vector3(0.06f, 0.08f, 0.06f), new Vector3(-0.16f, 0f, -0.1f), md);
        MakeBlock("LegBR", root.transform, new Vector3(0.06f, 0.08f, 0.06f), new Vector3(0.16f, 0f, -0.1f),  md);

        return root.transform;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  18. SEA CREATURE â€” tentacled fish, ~0.5u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildSeaCreature(Transform parent)
    {
        Color st = new Color(0.2f, 0.5f, 0.55f);
        Color sd = new Color(0.12f, 0.35f, 0.4f);
        Color bl = new Color(0.4f, 0.65f, 0.65f);
        Color fb = new Color(0.15f, 0.4f, 0.6f);
        Color ey = new Color(0.9f, 0.85f, 0.2f);
        Color tw = new Color(0.9f, 0.92f, 0.9f);

        var root = new GameObject("SeaCreatureModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body",  root.transform, new Vector3(0.28f, 0.22f, 0.35f),  new Vector3(0f, 0.35f, 0f),  st);
        MakeBlock("Belly", root.transform, new Vector3(0.24f, 0.1f, 0.3f),    new Vector3(0f, 0.28f, 0f),  bl);
        MakeBlock("Top",   root.transform, new Vector3(0.2f, 0.06f, 0.25f),   new Vector3(0f, 0.48f, 0f),  sd);

        MakeBlock("Head",  root.transform, new Vector3(0.24f, 0.18f, 0.15f), new Vector3(0f, 0.4f, 0.2f), st);
        MakeBlock("Mouth", root.transform, new Vector3(0.18f, 0.08f, 0.04f), new Vector3(0f, 0.32f, 0.28f), sd);
        MakeBlock("Tw1",   root.transform, new Vector3(0.14f, 0.03f, 0.02f), new Vector3(-0.04f, 0.34f, 0.28f), tw);
        MakeBlock("Tw2",   root.transform, new Vector3(0.14f, 0.03f, 0.02f), new Vector3(0.04f, 0.34f, 0.28f),  tw);
        MakeBlock("EyeL",  root.transform, new Vector3(0.06f, 0.06f, 0.02f), new Vector3(-0.1f, 0.45f, 0.26f), ey);
        MakeBlock("EyeR",  root.transform, new Vector3(0.06f, 0.06f, 0.02f), new Vector3(0.1f, 0.45f, 0.26f),  ey);
        MakeBlock("PuL",   root.transform, new Vector3(0.025f, 0.025f, 0.02f), new Vector3(-0.1f, 0.45f, 0.27f), sd);
        MakeBlock("PuR",   root.transform, new Vector3(0.025f, 0.025f, 0.02f), new Vector3(0.1f, 0.45f, 0.27f),  sd);

        MakeBlock("FinT",  root.transform, new Vector3(0.04f, 0.12f, 0.2f),  new Vector3(0f, 0.55f, -0.05f), fb);
        MakeBlock("FinL",  root.transform, new Vector3(0.12f, 0.04f, 0.15f), new Vector3(-0.2f, 0.38f, 0f),   fb);
        MakeBlock("FinR",  root.transform, new Vector3(0.12f, 0.04f, 0.15f), new Vector3(0.2f, 0.38f, 0f),    fb);

        var tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.35f, -0.18f);
        tail.transform.localRotation = Quaternion.Euler(-5f, 0f, 0f);
        MakeBlock("T1",  tail.transform, new Vector3(0.08f, 0.08f, 0.15f), new Vector3(0f, 0f, -0.08f),  st);
        MakeBlock("TF",  tail.transform, new Vector3(0.02f, 0.15f, 0.12f), new Vector3(0f, 0f, -0.2f),   fb);

        MakeBlock("Tn1", root.transform, new Vector3(0.04f, 0.04f, 0.2f),  new Vector3(-0.1f, 0.18f, 0.05f),  sd);
        MakeBlock("Tn2", root.transform, new Vector3(0.04f, 0.04f, 0.2f),  new Vector3(0.1f, 0.18f, 0.05f),   sd);
        MakeBlock("Tn3", root.transform, new Vector3(0.04f, 0.04f, 0.18f), new Vector3(-0.06f, 0.18f, -0.05f), sd);
        MakeBlock("Tn4", root.transform, new Vector3(0.04f, 0.04f, 0.18f), new Vector3(0.06f, 0.18f, -0.05f),  sd);
        MakeBlock("Tn5", root.transform, new Vector3(0.03f, 0.03f, 0.15f), new Vector3(0f, 0.18f, -0.1f),      sd);

        return root.transform;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  19. SKELETON â€” bare bones, ~0.95u
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildSkeleton(Transform parent)
    {
        Color bw = new Color(0.88f, 0.85f, 0.78f);
        Color bd = new Color(0.7f, 0.67f, 0.6f);
        Color es = new Color(0.05f, 0.05f, 0.05f);
        Color eg = new Color(0.8f, 0.2f, 0.1f);

        var root = new GameObject("SkeletonModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Spine",  root.transform, new Vector3(0.06f, 0.22f, 0.06f),  new Vector3(0f, 0.55f, 0f),   bw);
        MakeBlock("Rib1",   root.transform, new Vector3(0.22f, 0.03f, 0.06f),  new Vector3(0f, 0.62f, 0.02f), bw);
        MakeBlock("Rib2",   root.transform, new Vector3(0.2f, 0.03f, 0.06f),   new Vector3(0f, 0.58f, 0.02f), bw);
        MakeBlock("Rib3",   root.transform, new Vector3(0.18f, 0.03f, 0.06f),  new Vector3(0f, 0.54f, 0.02f), bw);
        MakeBlock("Pelvis", root.transform, new Vector3(0.18f, 0.06f, 0.1f),   new Vector3(0f, 0.4f, 0f),    bd);

        MakeBlock("Neck",   root.transform, new Vector3(0.05f, 0.05f, 0.05f),  new Vector3(0f, 0.68f, 0f),  bw);
        MakeBlock("Skull",  root.transform, new Vector3(0.18f, 0.18f, 0.18f),  new Vector3(0f, 0.82f, 0f),  bw);
        MakeBlock("Jaw",    root.transform, new Vector3(0.14f, 0.04f, 0.1f),   new Vector3(0f, 0.72f, 0.04f), bd);
        MakeBlock("EsL",    root.transform, new Vector3(0.06f, 0.06f, 0.04f),  new Vector3(-0.04f, 0.84f, 0.08f), es);
        MakeBlock("EsR",    root.transform, new Vector3(0.06f, 0.06f, 0.04f),  new Vector3(0.04f, 0.84f, 0.08f),  es);
        MakeBlock("EgL",    root.transform, new Vector3(0.03f, 0.03f, 0.02f),  new Vector3(-0.04f, 0.84f, 0.1f),  eg);
        MakeBlock("EgR",    root.transform, new Vector3(0.03f, 0.03f, 0.02f),  new Vector3(0.04f, 0.84f, 0.1f),   eg);
        MakeBlock("Nose",   root.transform, new Vector3(0.04f, 0.04f, 0.02f),  new Vector3(0f, 0.8f, 0.09f),      es);
        MakeBlock("Teeth",  root.transform, new Vector3(0.1f, 0.02f, 0.02f),   new Vector3(0f, 0.74f, 0.09f),     bw);

        SkeletonArm(root.transform, -0.14f, bw, bd, "L");
        SkeletonArm(root.transform,  0.14f, bw, bd, "R");
        SkeletonLeg(root.transform, -0.06f, bw, bd, "L");
        SkeletonLeg(root.transform,  0.06f, bw, bd, "R");

        return root.transform;
    }

    private static void SkeletonArm(Transform r, float x, Color bw, Color bd, string s)
    {
        var a = new GameObject("Arm" + s);
        a.transform.SetParent(r, false);
        a.transform.localPosition = new Vector3(x, 0.64f, 0f);
        MakeBlock("UA" + s, a.transform, new Vector3(0.04f, 0.16f, 0.04f), new Vector3(0f, -0.07f, 0f), bw);
        MakeBlock("LA" + s, a.transform, new Vector3(0.03f, 0.14f, 0.03f), new Vector3(0f, -0.22f, 0f), bw);
        MakeBlock("Ha" + s, a.transform, new Vector3(0.05f, 0.06f, 0.03f), new Vector3(0f, -0.32f, 0f), bd);
    }

    private static void SkeletonLeg(Transform r, float x, Color bw, Color bd, string s)
    {
        var h = new GameObject("Leg" + s);
        h.transform.SetParent(r, false);
        h.transform.localPosition = new Vector3(x, 0.35f, 0f);
        MakeBlock("UL" + s, h.transform, new Vector3(0.05f, 0.18f, 0.05f), new Vector3(0f, -0.08f, 0f), bw);
        var k = new GameObject("Knee" + s);
        k.transform.SetParent(h.transform, false);
        k.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        MakeBlock("LL" + s, k.transform, new Vector3(0.04f, 0.14f, 0.04f), new Vector3(0f, -0.07f, 0f), bw);
        MakeBlock("Ft" + s, k.transform, new Vector3(0.06f, 0.03f, 0.1f),  new Vector3(0f, -0.16f, 0.02f), bd);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  20. BAT â€” small flyer, ~0.25u body, ~0.5u wingspan
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static Transform BuildBat(Transform parent)
    {
        Color fd = new Color(0.2f, 0.15f, 0.12f);
        Color fm = new Color(0.3f, 0.22f, 0.18f);
        Color wm = new Color(0.25f, 0.18f, 0.15f);
        Color er = new Color(0.9f, 0.2f, 0.1f);
        Color tw = new Color(0.9f, 0.9f, 0.88f);
        Color ei = new Color(0.5f, 0.35f, 0.3f);

        var root = new GameObject("BatModel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        MakeBlock("Body", root.transform, new Vector3(0.1f, 0.08f, 0.12f), new Vector3(0f, 0.2f, 0f), fd);

        MakeBlock("Head",  root.transform, new Vector3(0.1f, 0.08f, 0.1f),  new Vector3(0f, 0.26f, 0.08f), fm);
        MakeBlock("EarL",  root.transform, new Vector3(0.03f, 0.08f, 0.03f), new Vector3(-0.04f, 0.34f, 0.06f), fd);
        MakeBlock("EiL",   root.transform, new Vector3(0.015f, 0.05f, 0.03f), new Vector3(-0.04f, 0.33f, 0.065f), ei);
        MakeBlock("EarR",  root.transform, new Vector3(0.03f, 0.08f, 0.03f), new Vector3(0.04f, 0.34f, 0.06f),  fd);
        MakeBlock("EiR",   root.transform, new Vector3(0.015f, 0.05f, 0.03f), new Vector3(0.04f, 0.33f, 0.065f), ei);
        MakeBlock("EyeL",  root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(-0.03f, 0.27f, 0.13f), er);
        MakeBlock("EyeR",  root.transform, new Vector3(0.03f, 0.03f, 0.02f), new Vector3(0.03f, 0.27f, 0.13f),  er);
        MakeBlock("FangL", root.transform, new Vector3(0.015f, 0.03f, 0.015f), new Vector3(-0.02f, 0.22f, 0.13f), tw);
        MakeBlock("FangR", root.transform, new Vector3(0.015f, 0.03f, 0.015f), new Vector3(0.02f, 0.22f, 0.13f),  tw);

        var wL = new GameObject("WingL");
        wL.transform.SetParent(root.transform, false);
        wL.transform.localPosition = new Vector3(-0.08f, 0.24f, 0f);
        MakeBlock("WU", wL.transform, new Vector3(0.15f, 0.02f, 0.12f), new Vector3(-0.08f, 0.02f, 0f), wm);
        MakeBlock("WL", wL.transform, new Vector3(0.1f, 0.015f, 0.08f), new Vector3(-0.2f, 0f, 0f),    wm);

        var wR = new GameObject("WingR");
        wR.transform.SetParent(root.transform, false);
        wR.transform.localPosition = new Vector3(0.08f, 0.24f, 0f);
        MakeBlock("WU", wR.transform, new Vector3(0.15f, 0.02f, 0.12f), new Vector3(0.08f, 0.02f, 0f), wm);
        MakeBlock("WL", wR.transform, new Vector3(0.1f, 0.015f, 0.08f), new Vector3(0.2f, 0f, 0f),    wm);

        MakeBlock("LegL", root.transform, new Vector3(0.02f, 0.06f, 0.02f), new Vector3(-0.03f, 0.14f, 0f), fd);
        MakeBlock("LegR", root.transform, new Vector3(0.02f, 0.06f, 0.02f), new Vector3(0.03f, 0.14f, 0f),  fd);

        return root.transform;
    }
}

