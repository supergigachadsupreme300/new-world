using UnityEngine;
using static WeaponModelBuilder;

/// <summary>
/// Procedural cube-based models for the four MAGIC WEAPONS that carry a rotation accent:
/// staff, holy book, bone wand, control orb. Split out of WeaponModelBuilder by 1ja (1iz is what
/// found them) so the magic models are findable by name - previously they were four anonymous methods
/// numbered 11-14 inside a 379-line file shared with fourteen other weapons, and nothing in the repo
/// was called a "magic model".
///
/// Two things about this file that look like mistakes and are not:
///
/// 1. `using static WeaponModelBuilder` is deliberate. The block helper and 11 of that class's 15
///    palette colours are SHARED with the fourteen non-magic weapons, so they cannot move here without
///    either duplicating them (a second spelling that rots) or editing every other weapon. They stay
///    put and are imported. Nothing is overridden - the fourteen Build* methods it also imports are
///    never called from here.
///
/// 2. The `// 11.` - `// 14.` section numbers are the weapon's index in WeaponCatalog registration
///    order (11=staff, 12=holy_book, 13=bone_wand, 14=control_orb), NOT a counter within this file.
///    Renumbering them to 1-4 would silently break that correspondence. WeaponModelBuilder keeps its
///    own 1-10 and 15-18 for the same reason.
///
/// NOT in this file: BuildLute. It is the fifth WeaponCategory.Magic weapon in WeaponCatalog, and it
/// deliberately stays with the non-magic builders - it is a musical instrument and, unlike these four,
/// it is scale-only (no rotation accent), which is the asymmetry 1im's rest-pose diagnosis turned on.
/// If you are looking for "the magic models" and expect five, this is why there are four.
///
/// Each builder returns the root Transform so the caller can parent it. All colliders are destroyed.
/// </summary>
public static class MagicWeaponModelBuilder
{
    // ──────────────────────────────────────────────────────────
    //  11. MAGE'S STAFF
    // ──────────────────────────────────────────────────────────
    public static Transform BuildStaff(Transform parent)
    {
        var root = new GameObject("Staff").transform;
        root.SetParent(parent, false);
        MakeBlock("LowerShaft", root, new Vector3(0.04f, 0.50f, 0.04f), new Vector3(0f, 0.30f, 0f), DarkWood);
        MakeBlock("UpperShaft", root, new Vector3(0.035f, 0.50f, 0.035f), new Vector3(0f, 0.80f, 0f), DarkWood);
        MakeBlock("WrapLow", root, new Vector3(0.05f, 0.02f, 0.05f), new Vector3(0f, 0.40f, 0f), Gold);
        MakeBlock("WrapHigh", root, new Vector3(0.045f, 0.02f, 0.045f), new Vector3(0f, 0.70f, 0f), Gold);
        MakeBlock("OrbBase", root, new Vector3(0.06f, 0.03f, 0.06f), new Vector3(0f, 1.05f, 0f), Gold);
        MakeBlock("OrbCore", root, new Vector3(0.08f, 0.08f, 0.08f), new Vector3(0f, 1.12f, 0f), BlueCrystal);
        MakeBlock("OrbGlow1", root, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.04f, 1.15f, 0f), PaleGold);
        MakeBlock("OrbGlow2", root, new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.03f, 1.10f, 0.02f), PaleGold);
        MakeBlock("Pommel", root, new Vector3(0.05f, 0.03f, 0.05f), new Vector3(0f, 0.02f, 0f), Bronze);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  12. HOLY BOOK
    // ──────────────────────────────────────────────────────────
    public static Transform BuildHolyBook(Transform parent)
    {
        var root = new GameObject("HolyBook").transform;
        root.SetParent(parent, false);
        MakeBlock("Cover", root, new Vector3(0.14f, 0.18f, 0.04f), new Vector3(0f, 0.15f, 0f), Cream);
        MakeBlock("Spine", root, new Vector3(0.02f, 0.18f, 0.04f), new Vector3(-0.07f, 0.15f, 0f), LeatherBrown);
        MakeBlock("ClaspTop", root, new Vector3(0.02f, 0.02f, 0.05f), new Vector3(0.06f, 0.23f, 0f), Gold);
        MakeBlock("ClaspBot", root, new Vector3(0.02f, 0.02f, 0.05f), new Vector3(0.06f, 0.07f, 0f), Gold);
        MakeBlock("Cross", root, new Vector3(0.02f, 0.10f, 0.01f), new Vector3(0f, 0.15f, 0.025f), Gold);
        MakeBlock("CrossBar", root, new Vector3(0.08f, 0.02f, 0.01f), new Vector3(0f, 0.18f, 0.025f), Gold);
        MakeBlock("GlowWisp1", root, new Vector3(0.025f, 0.025f, 0.025f), new Vector3(0.05f, 0.22f, 0.03f), PaleGold);
        MakeBlock("GlowWisp2", root, new Vector3(0.02f, 0.02f, 0.02f), new Vector3(-0.04f, 0.10f, 0.03f), PaleGold);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  13. BONE WAND
    // ──────────────────────────────────────────────────────────
    public static Transform BuildBoneWand(Transform parent)
    {
        var root = new GameObject("BoneWand").transform;
        root.SetParent(parent, false);
        MakeBlock("LowerShaft", root, new Vector3(0.035f, 0.35f, 0.035f), new Vector3(0f, 0.25f, 0f), Ivory);
        MakeBlock("UpperShaft", root, new Vector3(0.03f, 0.30f, 0.03f), new Vector3(0f, 0.60f, 0f), Ivory);
        MakeBlock("JointLow", root, new Vector3(0.045f, 0.03f, 0.045f), new Vector3(0f, 0.42f, 0f), Ivory);
        MakeBlock("JointHigh", root, new Vector3(0.04f, 0.025f, 0.04f), new Vector3(0f, 0.72f, 0f), Ivory);
        MakeBlock("Skull", root, new Vector3(0.06f, 0.06f, 0.05f), new Vector3(0f, 0.85f, 0f), Ivory);
        MakeBlock("SkullJaw", root, new Vector3(0.04f, 0.02f, 0.04f), new Vector3(0f, 0.81f, 0.01f), Ivory);
        MakeBlock("EyeSocket1", root, new Vector3(0.015f, 0.015f, 0.01f), new Vector3(-0.015f, 0.86f, 0.025f), DarkPurple);
        MakeBlock("EyeSocket2", root, new Vector3(0.015f, 0.015f, 0.01f), new Vector3(0.015f, 0.86f, 0.025f), DarkPurple);
        MakeBlock("RuneBand", root, new Vector3(0.045f, 0.03f, 0.045f), new Vector3(0f, 0.45f, 0f), DarkPurple);
        MakeBlock("Base", root, new Vector3(0.04f, 0.03f, 0.04f), new Vector3(0f, 0.02f, 0f), DarkSteel);
        return root;
    }

    // ──────────────────────────────────────────────────────────
    //  14. CONTROL ORB
    // ──────────────────────────────────────────────────────────
    public static Transform BuildControlOrb(Transform parent)
    {
        var root = new GameObject("ControlOrb").transform;
        root.SetParent(parent, false);
        MakeBlock("RingBase", root, new Vector3(0.18f, 0.02f, 0.18f), new Vector3(0f, 0.15f, 0f), Gold);
        MakeBlock("ProngL", root, new Vector3(0.025f, 0.12f, 0.025f), new Vector3(-0.06f, 0.22f, 0f), Bronze);
        MakeBlock("ProngR", root, new Vector3(0.025f, 0.12f, 0.025f), new Vector3(0.06f, 0.22f, 0f), Bronze);
        MakeBlock("ProngF", root, new Vector3(0.025f, 0.10f, 0.025f), new Vector3(0f, 0.21f, 0.06f), Bronze);
        MakeBlock("Cradle", root, new Vector3(0.10f, 0.03f, 0.10f), new Vector3(0f, 0.28f, 0f), Gold);
        MakeBlock("OrbFront", root, new Vector3(0.09f, 0.09f, 0.09f), new Vector3(0f, 0.35f, 0f), Cyan);
        MakeBlock("OrbCore", root, new Vector3(0.06f, 0.06f, 0.06f), new Vector3(0f, 0.35f, 0f), BlueCrystal);
        MakeBlock("WindWisp1", root, new Vector3(0.03f, 0.015f, 0.03f), new Vector3(0.05f, 0.38f, 0.02f), PaleGold);
        MakeBlock("WindWisp2", root, new Vector3(0.025f, 0.012f, 0.025f), new Vector3(-0.04f, 0.33f, -0.02f), PaleGold);
        MakeBlock("WindWisp3", root, new Vector3(0.02f, 0.01f, 0.02f), new Vector3(0.01f, 0.40f, -0.03f), PaleGold);
        return root;
    }
}
