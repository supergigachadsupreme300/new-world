using System;
using UnityEngine;

public static partial class MapBuilder
{
    // ==================== MapBuilderPlayerModel.cs ====================
    // ═══════════════════════════════════════════════════════════════
    //  PLAYER MODEL  (smooth ellipsoid character, 1dw)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// The race the given parent player (or <c>null</c> host, e.g. cutscenes) should look like.
    /// Reads the live <see cref="RaceChangeManager"/> on the host; falls back to Human so cutscene
    /// models keep the classic look.
    /// </summary>
    private static RaceData ResolvePlayerRace(Transform parent)
    {
        if (parent != null)
        {
            var mgr = parent.GetComponent<RaceChangeManager>();
            if (mgr != null && mgr.ActiveRace != null)
                return mgr.ActiveRace;
        }
        var roster = RaceDatabase.DefaultRoster;
        if (roster == null) return null;
        for (int i = 0; i < roster.Count; i++)
            if (roster[i] != null && string.Equals(roster[i].raceId, "human", StringComparison.OrdinalIgnoreCase))
                return roster[i];
        return null;
    }

    private static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k);

    /// <summary>Whole-model size bump (1dy): +8% on top of any caller scale so the character with the
    /// slimmer torso reads bigger. One constant so the car-cutscene/chair fits are a single-line
    /// revert if they clip.</summary>
    private const float PlayerModelScale = 1.08f;

    /// <summary>Faceted ball joint at a limb pivot (1dy): a plain "Joint"-profile sphere at the pivot
    /// transform's origin, so it rotates with the limb and scales with how race ratios size the pivot.
    /// Color matches the adjacent part (arm sleeve / pants leg).</summary>
    private static void AddJoint(string name, Transform pivot, Vector3 size, Color color)
    {
        MakePart(name, pivot, size, Vector3.zero, color, "Joint");
    }

    /// <summary>
    /// Apply a race's body-ratio knobs to a freshly built player model (§3.5 Race Visuals).
    /// Height/Bulk stretch the whole root; the head, neck, eyes and hair are counter-scaled to read
    /// at the race's Head size; shoulder pivots get the arm-length + spread, hip pivots the leg length.
    /// All-1 values (Human) are a strict no-op. Ratios only ever move Transforms — the part meshes are
    /// unit-space ellipsoids (1dw) so they scale with them exactly like the old cubes did.
    /// </summary>
    private static void ApplyRaceLook(Transform modelRoot, RaceData race, bool calibrateFeet)
    {
        if (modelRoot == null || race == null) return;

        float h = Mathf.Max(0.6f, race.BodyHeight);
        float b = Mathf.Max(0.6f, race.BodyBulk);
        float hd = Mathf.Max(0.6f, race.BodyHead);
        float sw = Mathf.Max(0.6f, race.BodyShoulderWidth);
        float arm = Mathf.Max(0.6f, race.BodyArm);
        float leg = Mathf.Max(0.6f, race.BodyLeg);

        if (h == 1f && b == 1f && hd == 1f && sw == 1f && arm == 1f && leg == 1f)
            return;

        var s = modelRoot.localScale;
        modelRoot.localScale = new Vector3(s.x * b, s.y * h, s.z * b);

        var headScale = new Vector3(hd / b, hd / h, hd / b);
        ApplyRaceRatioRecurse(modelRoot, headScale, sw, arm, leg);

        // Standing pose: re-plant the feet on the ground after height/leg changes (derived from the
        // standing leg chain: hip -0.25, thigh+shin+shoe ≈ -0.62 below it). PlayerModelScale keeps the
        // re-plant consistent with the +8% whole-model bump (1dy).
        if (calibrateFeet)
        {
            var p = modelRoot.localPosition;
            modelRoot.localPosition = new Vector3(p.x, PlayerModelScale * h * (0.25f + 0.62f * leg), p.z);
        }
    }

    private static void ApplyRaceRatioRecurse(Transform t, Vector3 headScale, float sw, float arm, float leg)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (c == null) continue;
            string n = c.name;
            if (n == "Head" || n == "Neck" || n.StartsWith("Eye") || n.StartsWith("Hair") || n.StartsWith("Ponytail"))
            {
                c.localScale = Vector3.Scale(c.localScale, headScale);
            }
            else if (n == "ShoulderL" || n == "ShoulderR")
            {
                c.localScale = new Vector3(c.localScale.x, c.localScale.y * arm, c.localScale.z);
                var sp = c.localPosition;
                c.localPosition = new Vector3(sp.x * sw, sp.y, sp.z);
            }
            else if (n == "HipL" || n == "HipR")
            {
                c.localScale = new Vector3(c.localScale.x, c.localScale.y * leg, c.localScale.z);
            }
            ApplyRaceRatioRecurse(c, headScale, sw, arm, leg);
        }
    }

    public static GameObject BuildPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("PlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = new Vector3(0f, 0.86f * PlayerModelScale, 0f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * (scale * PlayerModelScale);

        // ── Torso pivot (waist centre): holds everything above the hips so the animator can
        // lean/bounce the upper body while the legs stay planted on the root. ──
        var torso = new GameObject("Torso");
        torso.transform.SetParent(root.transform, false);
        torso.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        torso.transform.localRotation = Quaternion.identity;

        bool female = ActiveGender == PlayerGender.Female;
        var race = ResolvePlayerRace(parent);

        Color skinC = race != null ? race.SkinColor : new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = race != null ? race.ClothColor : new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = race != null ? race.PantsColor : new Color(0.25f, 0.25f, 0.35f);
        Color hairC = race != null ? race.HairColor : new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = race != null ? race.EyeColor : new Color(0.05f, 0.03f, 0.01f);
        Color shoeC = race != null ? race.ShoeColor : new Color(0.2f, 0.2f, 0.2f);
        Color dressC = shirtC;

        MakePart("Body", torso.transform, new Vector3(female ? 0.40f : 0.44f, 0.6f, 0.25f), new Vector3(0f, 0.05f, 0f), shirtC, "Body");
        if (female)
        {
            MakePart("Skirt", torso.transform, new Vector3(0.52f, 0.28f, 0.3f), new Vector3(0f, -0.27f, 0f), dressC, "Skirt");
            MakePart("SkirtHem", torso.transform, new Vector3(0.56f, 0.06f, 0.34f), new Vector3(0f, -0.42f, 0f), Darken(dressC, 0.6f), "SkirtHem");
        }
        MakePart("Head", torso.transform, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0f, 0.65f, 0f), skinC, "Head");
        MakePart("Neck", torso.transform, new Vector3(0.15f, 0.16f, 0.15f), new Vector3(0f, 0.4f, 0f), skinC, "Cylinder");

        // ── Shoulder pivots (rotate from shoulder joint) ──
        // Arm chain: Shoulder -> Elbow -> Forearm + Hand (upper/lower arm split).
        var shoulderL = new GameObject("ShoulderL");
        shoulderL.transform.SetParent(torso.transform);
        shoulderL.transform.localPosition = new Vector3(-0.28f, 0.35f, 0f);
        shoulderL.transform.localRotation = Quaternion.identity;

        var shoulderR = new GameObject("ShoulderR");
        shoulderR.transform.SetParent(torso.transform);
        shoulderR.transform.localPosition = new Vector3(0.28f, 0.35f, 0f);
        shoulderR.transform.localRotation = Quaternion.identity;

        AddJoint("JShoulderL", shoulderL.transform, new Vector3(0.14f, 0.14f, 0.14f), shirtC);
        AddJoint("JShoulderR", shoulderR.transform, new Vector3(0.14f, 0.14f, 0.14f), shirtC);

        MakePart("UpperArmL", shoulderL.transform, new Vector3(0.12f, 0.26f, 0.12f), new Vector3(0f, -0.13f, 0f), shirtC, "UpperArm");
        var elbowL = new GameObject("ElbowL");
        elbowL.transform.SetParent(shoulderL.transform);
        elbowL.transform.localPosition = new Vector3(0f, -0.26f, 0f);
        elbowL.transform.localRotation = Quaternion.identity;
        AddJoint("JElbowL", elbowL.transform, new Vector3(0.11f, 0.11f, 0.11f), shirtC);
        MakePart("ForearmL", elbowL.transform, new Vector3(0.12f, 0.22f, 0.12f), new Vector3(0f, -0.12f, 0f), shirtC, "Forearm");
        MakePart("HandL", elbowL.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(0f, -0.25f, 0f), skinC, "Hand");

        MakePart("UpperArmR", shoulderR.transform, new Vector3(0.12f, 0.26f, 0.12f), new Vector3(0f, -0.13f, 0f), shirtC, "UpperArm");
        var elbowR = new GameObject("ElbowR");
        elbowR.transform.SetParent(shoulderR.transform);
        elbowR.transform.localPosition = new Vector3(0f, -0.26f, 0f);
        elbowR.transform.localRotation = Quaternion.identity;
        AddJoint("JElbowR", elbowR.transform, new Vector3(0.11f, 0.11f, 0.11f), shirtC);
        MakePart("ForearmR", elbowR.transform, new Vector3(0.12f, 0.22f, 0.12f), new Vector3(0f, -0.12f, 0f), shirtC, "Forearm");
        MakePart("HandR", elbowR.transform, new Vector3(0.12f, 0.08f, 0.12f), new Vector3(0f, -0.25f, 0f), skinC, "Hand");

        // ── Hip pivots (rotate from hip joint) ──
        var hipL = new GameObject("HipL");
        hipL.transform.SetParent(root.transform);
        hipL.transform.localPosition = new Vector3(-0.12f, -0.25f, 0f);
        hipL.transform.localRotation = Quaternion.identity;
        AddJoint("JHipL", hipL.transform, new Vector3(0.13f, 0.13f, 0.13f), pantsC);

        var hipR = new GameObject("HipR");
        hipR.transform.SetParent(root.transform);
        hipR.transform.localPosition = new Vector3(0.12f, -0.25f, 0f);
        hipR.transform.localRotation = Quaternion.identity;
        AddJoint("JHipR", hipR.transform, new Vector3(0.13f, 0.13f, 0.13f), pantsC);

        // Leg chain: Hip -> Knee -> Shin + Shoe (upper/lower leg split).
        MakePart("ThighL", hipL.transform, new Vector3(0.14f, 0.3f, 0.14f), new Vector3(0f, -0.15f, 0f), pantsC, "Thigh");
        var kneeL = new GameObject("KneeL");
        kneeL.transform.SetParent(hipL.transform);
        kneeL.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        kneeL.transform.localRotation = Quaternion.identity;
        AddJoint("JKneeL", kneeL.transform, new Vector3(0.12f, 0.12f, 0.12f), pantsC);
        MakePart("ShinL", kneeL.transform, new Vector3(0.14f, 0.24f, 0.14f), new Vector3(0f, -0.12f, 0f), pantsC, "Shin");
        MakePart("ShoeL", kneeL.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(0f, -0.27f, 0.02f), shoeC, "Shoe");

        MakePart("ThighR", hipR.transform, new Vector3(0.14f, 0.3f, 0.14f), new Vector3(0f, -0.15f, 0f), pantsC, "Thigh");
        var kneeR = new GameObject("KneeR");
        kneeR.transform.SetParent(hipR.transform);
        kneeR.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        kneeR.transform.localRotation = Quaternion.identity;
        AddJoint("JKneeR", kneeR.transform, new Vector3(0.12f, 0.12f, 0.12f), pantsC);
        MakePart("ShinR", kneeR.transform, new Vector3(0.14f, 0.24f, 0.14f), new Vector3(0f, -0.12f, 0f), pantsC, "Shin");
        MakePart("ShoeR", kneeR.transform, new Vector3(0.16f, 0.08f, 0.22f), new Vector3(0f, -0.27f, 0.02f), shoeC, "Shoe");

        MakePart("Hair", torso.transform, new Vector3(0.32f, 0.08f, 0.3f), new Vector3(0f, 0.88f, 0f), hairC, "Hair");
        MakePart("HairL", torso.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(-0.19f, 0.69f, 0f), hairC, "HairSide");
        MakePart("HairR", torso.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(0.19f, 0.69f, 0f), hairC, "HairSide");
        if (female)
        {
            MakePart("HairBack", torso.transform, new Vector3(0.3f, 0.3f, 0.1f), new Vector3(0f, 0.62f, -0.16f), hairC, "HairBack");
            MakePart("HairBand", torso.transform, new Vector3(0.34f, 0.05f, 0.32f), new Vector3(0f, 0.8f, 0f), new Color(0.1f, 0.34f, 0.56f), "HairBand");
            MakePart("Ponytail1", torso.transform, new Vector3(0.18f, 0.24f, 0.14f), new Vector3(0f, 0.7f, -0.23f), hairC, "Ponytail");
            MakePart("Ponytail2", torso.transform, new Vector3(0.15f, 0.22f, 0.13f), new Vector3(0f, 0.5f, -0.27f), hairC, "Ponytail");
            MakePart("Ponytail3", torso.transform, new Vector3(0.12f, 0.2f, 0.12f), new Vector3(0f, 0.3f, -0.29f), hairC, "Ponytail");
        }
        else
        {
            MakePart("HairBack", torso.transform, new Vector3(0.3f, 0.26f, 0.1f), new Vector3(0f, 0.62f, -0.16f), hairC, "HairBack");
        }
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);
        MakePart("EyeWhiteL", torso.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(-0.08f, 0.72f, 0.155f), eyeWhiteC, "EyeWhite");
        MakePart("EyeWhiteR", torso.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(0.08f, 0.72f, 0.155f), eyeWhiteC, "EyeWhite");
        MakePart("EyeIrisL", torso.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(-0.08f, 0.72f, 0.165f), eyeC, "EyeIris");
        MakePart("EyeIrisR", torso.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(0.08f, 0.72f, 0.165f), eyeC, "EyeIris");

        ApplyRaceLook(root.transform, race, calibrateFeet: true);

        return root;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SEATED PLAYER MODEL  (for inside car — arms reaching forward)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildSeatedPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("SeatedPlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = new Vector3(-0.35f * PlayerModelScale, 0.65f * PlayerModelScale, -0.1f * PlayerModelScale);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * (scale * PlayerModelScale);

        bool female = ActiveGender == PlayerGender.Female;
        var race = ResolvePlayerRace(parent);

        Color skinC = race != null ? race.SkinColor : new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = race != null ? race.ClothColor : new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = race != null ? race.PantsColor : new Color(0.25f, 0.25f, 0.35f);
        Color hairC = race != null ? race.HairColor : (female ? new Color(0.16f, 0.1f, 0.08f) : new Color(0.2f, 0.12f, 0.05f));
        Color eyeC = race != null ? race.EyeColor : new Color(0.05f, 0.03f, 0.01f);
        Color dressC = shirtC;

        // ── Torso (seated, upright) ──
        MakePart("Body", root.transform, new Vector3(0.34f, 0.5f, 0.28f), new Vector3(0f, 0.25f, 0f), shirtC, "Body");
        if (female)
        {
            MakePart("Skirt", root.transform, new Vector3(0.42f, 0.2f, 0.32f), new Vector3(0f, -0.02f, 0f), dressC, "Skirt");
            MakePart("SkirtHem", root.transform, new Vector3(0.46f, 0.05f, 0.36f), new Vector3(0f, -0.13f, 0f), Darken(dressC, 0.6f), "SkirtHem");
        }
        // ── Head ──
        MakePart("Head", root.transform, new Vector3(0.28f, 0.28f, 0.28f), new Vector3(0f, 0.74f, 0f), skinC, "Head");
        MakePart("Neck", root.transform, new Vector3(0.13f, 0.10f, 0.13f), new Vector3(0f, 0.55f, 0f), skinC, "Cylinder");
        // ── Hair ──
        MakePart("Hair", root.transform, new Vector3(0.3f, 0.07f, 0.28f), new Vector3(0f, 0.94f, 0f), hairC, "Hair");
        MakePart("HairL", root.transform, new Vector3(0.08f, 0.3f, 0.12f), new Vector3(-0.18f, 0.72f, 0f), hairC, "HairSide");
        MakePart("HairR", root.transform, new Vector3(0.08f, 0.3f, 0.12f), new Vector3(0.18f, 0.72f, 0f), hairC, "HairSide");
        if (female)
        {
            MakePart("HairBack", root.transform, new Vector3(0.28f, 0.26f, 0.08f), new Vector3(0f, 0.72f, -0.14f), hairC, "HairBack");
            MakePart("HairBand", root.transform, new Vector3(0.3f, 0.05f, 0.3f), new Vector3(0f, 0.9f, 0f), new Color(0.1f, 0.34f, 0.56f), "HairBand");
            MakePart("Ponytail1", root.transform, new Vector3(0.16f, 0.22f, 0.12f), new Vector3(0f, 0.76f, -0.19f), hairC, "Ponytail");
            MakePart("Ponytail2", root.transform, new Vector3(0.13f, 0.2f, 0.11f), new Vector3(0f, 0.58f, -0.23f), hairC, "Ponytail");
            MakePart("Ponytail3", root.transform, new Vector3(0.11f, 0.18f, 0.1f), new Vector3(0f, 0.4f, -0.25f), hairC, "Ponytail");
        }
        // ── Eyes ──
        Color eyeWhiteC = new Color(0.95f, 0.95f, 0.97f);
        MakePart("EyeWhiteL", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(-0.07f, 0.77f, 0.145f), eyeWhiteC, "EyeWhite");
        MakePart("EyeWhiteR", root.transform, new Vector3(0.08f, 0.06f, 0.03f), new Vector3(0.07f, 0.77f, 0.145f), eyeWhiteC, "EyeWhite");
        MakePart("EyeIrisL", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(-0.07f, 0.77f, 0.155f), eyeC, "EyeIris");
        MakePart("EyeIrisR", root.transform, new Vector3(0.05f, 0.05f, 0.04f), new Vector3(0.07f, 0.77f, 0.155f), eyeC, "EyeIris");
        // ── Arms (shoulder -> elbow chain, reaching forward to the steering wheel) ──
        // Shoulder carries the reach (-60°), elbow flexes the forearm up toward the wheel so
        // the hands land on it. The driving cutscene rocks these pivots to simulate steering.
        var shoulderL = new GameObject("ShoulderL");
        shoulderL.transform.SetParent(root.transform);
        shoulderL.transform.localPosition = new Vector3(-0.24f, 0.47f, 0.01f);
        shoulderL.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);

        var shoulderR = new GameObject("ShoulderR");
        shoulderR.transform.SetParent(root.transform);
        shoulderR.transform.localPosition = new Vector3(0.24f, 0.47f, 0.01f);
        shoulderR.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);

        AddJoint("JShoulderL", shoulderL.transform, new Vector3(0.13f, 0.13f, 0.13f), shirtC);
        AddJoint("JShoulderR", shoulderR.transform, new Vector3(0.13f, 0.13f, 0.13f), shirtC);

        MakePart("UpperArmL", shoulderL.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(0f, -0.1f, 0f), shirtC, "UpperArm");
        var elbowL = new GameObject("ElbowL");
        elbowL.transform.SetParent(shoulderL.transform);
        elbowL.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        elbowL.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        AddJoint("JElbowL", elbowL.transform, new Vector3(0.10f, 0.10f, 0.10f), shirtC);
        MakePart("ForearmL", elbowL.transform, new Vector3(0.09f, 0.2f, 0.09f), new Vector3(0f, -0.1f, 0f), shirtC, "Forearm");
        MakePart("HandL", elbowL.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0f, -0.2f, 0f), skinC, "Hand");

        MakePart("UpperArmR", shoulderR.transform, new Vector3(0.1f, 0.2f, 0.1f), new Vector3(0f, -0.1f, 0f), shirtC, "UpperArm");
        var elbowR = new GameObject("ElbowR");
        elbowR.transform.SetParent(shoulderR.transform);
        elbowR.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        elbowR.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        AddJoint("JElbowR", elbowR.transform, new Vector3(0.10f, 0.10f, 0.10f), shirtC);
        MakePart("ForearmR", elbowR.transform, new Vector3(0.09f, 0.2f, 0.09f), new Vector3(0f, -0.1f, 0f), shirtC, "Forearm");
        MakePart("HandR", elbowR.transform, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0f, -0.2f, 0f), skinC, "Hand");

        // ── Legs (hip -> knee chain, thighs forward and shins down in the footwell) ──
        var hipL = new GameObject("HipL");
        hipL.transform.SetParent(root.transform);
        hipL.transform.localPosition = new Vector3(-0.12f, 0.06f, 0f);
        hipL.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        AddJoint("JHipL", hipL.transform, new Vector3(0.12f, 0.12f, 0.12f), pantsC);

        var hipR = new GameObject("HipR");
        hipR.transform.SetParent(root.transform);
        hipR.transform.localPosition = new Vector3(0.12f, 0.06f, 0f);
        hipR.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        AddJoint("JHipR", hipR.transform, new Vector3(0.12f, 0.12f, 0.12f), pantsC);

        MakePart("ThighL", hipL.transform, new Vector3(0.13f, 0.3f, 0.13f), new Vector3(0f, -0.15f, 0f), pantsC, "Thigh");
        var kneeL = new GameObject("KneeL");
        kneeL.transform.SetParent(hipL.transform);
        kneeL.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        kneeL.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        AddJoint("JKneeL", kneeL.transform, new Vector3(0.11f, 0.11f, 0.11f), pantsC);
        MakePart("ShinL", kneeL.transform, new Vector3(0.11f, 0.26f, 0.11f), new Vector3(0f, -0.13f, 0f), pantsC, "Shin");

        MakePart("ThighR", hipR.transform, new Vector3(0.13f, 0.3f, 0.13f), new Vector3(0f, -0.15f, 0f), pantsC, "Thigh");
        var kneeR = new GameObject("KneeR");
        kneeR.transform.SetParent(hipR.transform);
        kneeR.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        kneeR.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        AddJoint("JKneeR", kneeR.transform, new Vector3(0.11f, 0.11f, 0.11f), pantsC);
        MakePart("ShinR", kneeR.transform, new Vector3(0.11f, 0.26f, 0.11f), new Vector3(0f, -0.13f, 0f), pantsC, "Shin");

        ApplyRaceLook(root.transform, race, calibrateFeet: false);

        return root;
    }
    // ═══════════════════════════════════════════════════════════════
    //  SITTING PLAYER MODEL  (neutral pose — hips on the seat, arms on lap)
    // ═══════════════════════════════════════════════════════════════

    public static GameObject BuildSitPlayerModel(Transform parent, float scale = 1f)
    {
        var root = new GameObject("SitPlayerModel");
        root.transform.SetParent(parent);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * (scale * PlayerModelScale);

        bool female = ActiveGender == PlayerGender.Female;
        var race = ResolvePlayerRace(parent);

        Color skinC = race != null ? race.SkinColor : new Color(220f / 255f, 178f / 255f, 132f / 255f);
        Color shirtC = race != null ? race.ClothColor : new Color(0.2f, 0.6f, 0.9f);
        Color pantsC = race != null ? race.PantsColor : new Color(0.25f, 0.25f, 0.35f);
        Color hairC = race != null ? race.HairColor : new Color(0.2f, 0.12f, 0.05f);
        Color eyeC = race != null ? race.EyeColor : new Color(0.05f, 0.03f, 0.01f);
        Color shoeC = race != null ? race.ShoeColor : new Color(0.2f, 0.2f, 0.2f);
        Color dressC = shirtC;

        // ── Legs (hip -> knee chain, thighs forward, shins down, feet on floor) ──
        var hipL = new GameObject("HipL");
        hipL.transform.SetParent(root.transform);
        hipL.transform.localPosition = new Vector3(-0.13f, -0.03f, 0.16f);
        hipL.transform.localRotation = Quaternion.identity;
        AddJoint("JHipL", hipL.transform, new Vector3(0.13f, 0.13f, 0.13f), pantsC);

        var hipR = new GameObject("HipR");
        hipR.transform.SetParent(root.transform);
        hipR.transform.localPosition = new Vector3(0.13f, -0.03f, 0.16f);
        hipR.transform.localRotation = Quaternion.identity;
        AddJoint("JHipR", hipR.transform, new Vector3(0.13f, 0.13f, 0.13f), pantsC);

        MakePart("ThighL", hipL.transform, new Vector3(0.14f, 0.26f, 0.14f), Vector3.zero, pantsC, "Thigh", Quaternion.Euler(-75f, 0f, 0f));
        MakePart("ThighR", hipR.transform, new Vector3(0.14f, 0.26f, 0.14f), Vector3.zero, pantsC, "Thigh", Quaternion.Euler(-75f, 0f, 0f));

        var kneeL = new GameObject("KneeL");
        kneeL.transform.SetParent(hipL.transform);
        kneeL.transform.localPosition = new Vector3(0f, -0.13f, 0.08f);
        kneeL.transform.localRotation = Quaternion.identity;
        AddJoint("JKneeL", kneeL.transform, new Vector3(0.12f, 0.12f, 0.12f), pantsC);
        MakePart("ShinL", kneeL.transform, new Vector3(0.12f, 0.3f, 0.12f), new Vector3(0f, -0.14f, 0f), pantsC, "Shin");
        MakePart("ShoeL", kneeL.transform, new Vector3(0.16f, 0.08f, 0.24f), new Vector3(0f, -0.31f, 0.02f), shoeC, "Shoe");

        var kneeR = new GameObject("KneeR");
        kneeR.transform.SetParent(hipR.transform);
        kneeR.transform.localPosition = new Vector3(0f, -0.13f, 0.08f);
        kneeR.transform.localRotation = Quaternion.identity;
        AddJoint("JKneeR", kneeR.transform, new Vector3(0.12f, 0.12f, 0.12f), pantsC);
        MakePart("ShinR", kneeR.transform, new Vector3(0.12f, 0.3f, 0.12f), new Vector3(0f, -0.14f, 0f), pantsC, "Shin");
        MakePart("ShoeR", kneeR.transform, new Vector3(0.16f, 0.08f, 0.24f), new Vector3(0f, -0.31f, 0.02f), shoeC, "Shoe");

        // ── Torso (hips on the seat) ──
        MakePart("Torso", root.transform, new Vector3(female ? 0.37f : 0.40f, 0.36f, 0.28f), new Vector3(0f, 0.18f, 0f), shirtC, "SitTorso");
        if (female)
        {
            MakePart("Skirt", root.transform, new Vector3(0.48f, 0.2f, 0.32f), new Vector3(0f, 0.02f, 0f), dressC, "Skirt");
            MakePart("SkirtHem", root.transform, new Vector3(0.52f, 0.05f, 0.35f), new Vector3(0f, -0.08f, 0f), Darken(dressC, 0.6f), "SkirtHem");
        }
        MakePart("Chest", root.transform, new Vector3(0.39f, 0.28f, 0.26f), new Vector3(0f, 0.42f, 0f), shirtC, "Chest");

        // ── Neck + head ──
        MakePart("Neck", root.transform, new Vector3(0.14f, 0.12f, 0.14f), new Vector3(0f, 0.62f, 0f), skinC, "Cylinder");
        MakePart("Head", root.transform, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0f, 0.78f, 0f), skinC, "Head");
        MakePart("EyeWhiteL", root.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(-0.08f, 0.85f, 0.155f), new Color(0.95f, 0.95f, 0.97f), "EyeWhite");
        MakePart("EyeWhiteR", root.transform, new Vector3(0.09f, 0.07f, 0.03f), new Vector3(0.08f, 0.85f, 0.155f), new Color(0.95f, 0.95f, 0.97f), "EyeWhite");
        MakePart("EyeIrisL", root.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(-0.08f, 0.85f, 0.165f), eyeC, "EyeIris");
        MakePart("EyeIrisR", root.transform, new Vector3(0.055f, 0.055f, 0.04f), new Vector3(0.08f, 0.85f, 0.165f), eyeC, "EyeIris");
        MakePart("Hair", root.transform, new Vector3(0.32f, 0.08f, 0.3f), new Vector3(0f, 1f, 0f), hairC, "Hair");
        MakePart("HairL", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(-0.19f, 0.82f, 0f), hairC, "HairSide");
        MakePart("HairR", root.transform, new Vector3(0.08f, 0.32f, 0.26f), new Vector3(0.19f, 0.82f, 0f), hairC, "HairSide");
        if (female)
        {
            MakePart("HairBack", root.transform, new Vector3(0.3f, 0.3f, 0.1f), new Vector3(0f, 0.74f, -0.16f), hairC, "HairBack");
            MakePart("HairBand", root.transform, new Vector3(0.34f, 0.05f, 0.32f), new Vector3(0f, 0.93f, 0f), new Color(0.1f, 0.34f, 0.56f), "HairBand");
        }
        else
        {
            MakePart("HairBack", root.transform, new Vector3(0.3f, 0.26f, 0.1f), new Vector3(0f, 0.74f, -0.16f), hairC, "HairBack");
        }

        // ── Arms (shoulder -> elbow chain, hands resting on the lap) ──
        var shoulderL = new GameObject("ShoulderL");
        shoulderL.transform.SetParent(root.transform);
        shoulderL.transform.localPosition = new Vector3(-0.25f, 0.40f, 0f);
        shoulderL.transform.localRotation = Quaternion.identity;

        var shoulderR = new GameObject("ShoulderR");
        shoulderR.transform.SetParent(root.transform);
        shoulderR.transform.localPosition = new Vector3(0.25f, 0.40f, 0f);
        shoulderR.transform.localRotation = Quaternion.identity;
        AddJoint("JShoulderL", shoulderL.transform, new Vector3(0.13f, 0.13f, 0.13f), shirtC);
        AddJoint("JShoulderR", shoulderR.transform, new Vector3(0.13f, 0.13f, 0.13f), shirtC);

        MakePart("UpperArmL", shoulderL.transform, new Vector3(0.12f, 0.34f, 0.12f), new Vector3(0f, -0.12f, 0.06f), shirtC, "UpperArm", Quaternion.Euler(-60f, 0f, 0f));
        MakePart("UpperArmR", shoulderR.transform, new Vector3(0.12f, 0.34f, 0.12f), new Vector3(0f, -0.12f, 0.06f), shirtC, "UpperArm", Quaternion.Euler(-60f, 0f, 0f));

        var elbowL = new GameObject("ElbowL");
        elbowL.transform.SetParent(shoulderL.transform);
        elbowL.transform.localPosition = new Vector3(0f, -0.085f, 0.147f);
        elbowL.transform.localRotation = Quaternion.identity;
        AddJoint("JElbowL", elbowL.transform, new Vector3(0.11f, 0.11f, 0.11f), shirtC);
        MakePart("ForearmL", elbowL.transform, new Vector3(0.11f, 0.16f, 0.11f), new Vector3(0f, -0.08f, -0.007f), shirtC, "Forearm");
        MakePart("HandL", elbowL.transform, new Vector3(0.11f, 0.09f, 0.11f), new Vector3(0f, -0.16f, -0.007f), skinC, "Hand");

        var elbowR = new GameObject("ElbowR");
        elbowR.transform.SetParent(shoulderR.transform);
        elbowR.transform.localPosition = new Vector3(0f, -0.085f, 0.147f);
        elbowR.transform.localRotation = Quaternion.identity;
        AddJoint("JElbowR", elbowR.transform, new Vector3(0.11f, 0.11f, 0.11f), shirtC);
        MakePart("ForearmR", elbowR.transform, new Vector3(0.11f, 0.16f, 0.11f), new Vector3(0f, -0.08f, -0.007f), shirtC, "Forearm");
        MakePart("HandR", elbowR.transform, new Vector3(0.11f, 0.09f, 0.11f), new Vector3(0f, -0.16f, -0.007f), skinC, "Hand");

        ApplyRaceLook(root.transform, race, calibrateFeet: false);

        return root;
    }
    // ==================== MapBuilderShop.cs ====================
}