using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a live, combat-active weapon GameObject from a <see cref="WeaponData"/> and
/// equips it onto the player's <see cref="CombatController"/> hands (Phase 10).
///
/// Rigging steps per category:
///   • Melee  → <see cref="MeleeWeaponBehavior"/> + required <see cref="HitboxSystem"/>.
///   • Ranged → <see cref="RangedWeaponBehavior"/> + muzzle point (hitscan fallback, no prefab).
///   • Magic  → <see cref="MagicWeaponBehavior"/> + the owner's <see cref="SpellCaster"/>.
/// Every rig also gets a <see cref="WeaponSkillExecutor"/> (for WeaponSkill-based skills) and a
/// primitive visual proxy (the repo has no weapon mesh assets).
///
/// The builder ensures the player root carries the combat stack (StaminaSystem +
/// CombatController + SpellCaster + PlayerStats as the IStatProvider) since nothing else
/// wires it today. Composes existing contracts only — no rewrites.
/// </summary>
public static class WeaponRigBuilder
{
    /// <summary>Set true to print equip/attach diagnostics to the console (debug aid).</summary>
    public static bool LogRigging = true;

    /// <summary>
    /// Target WORLD scale of a drawn (in-hand) weapon. The hand blocks the rig parents to are not
    /// identity-scaled (<c>HandL/R</c> are ~0.12, 0.08, 0.12), so the rig's local scale must be
    /// computed per parent via <see cref="ScaleForWorld"/> — treating it as uniform local (the old
    /// <c>EquipScale</c>) silently shrank the held weapon ~10× and distorted it. Equals
    /// <see cref="StowScale"/> so a weapon reads exactly the same size drawn and stowed.
    /// </summary>
    public const float DrawScale = 0.8f;

    private static readonly HashSet<string> _logged = new HashSet<string>();

    private static void LogOnce(string key, string message)
    {
        if (LogRigging && _logged.Add(key)) Debug.Log("[WeaponRig] " + message);
    }

    /// <summary>
    /// Ensure the player root has the combat stack needed to drive an equipped weapon.
    /// </summary>
    public static void EnsureCombatStack(GameObject playerRoot)
    {
        if (playerRoot == null) return;
        if (playerRoot.GetComponent<PlayerStats>() == null)
            playerRoot.AddComponent<PlayerStats>();
        if (playerRoot.GetComponent<SpellCaster>() == null)
            playerRoot.AddComponent<SpellCaster>();
        if (playerRoot.GetComponent<CombatController>() == null)
            playerRoot.AddComponent<CombatController>();
    }

    /// <summary>
    /// Rig <paramref name="weapon"/> onto <paramref name="playerRoot"/>, attach its visual to
    /// the matching player-model hand, and assign it to the combat controller's hand
    /// (<paramref name="toLeftHand"/> selects left vs right). Equipping replaces ONLY the targeted
    /// hand slot — the weapon in the other hand is left untouched, so the player can hold a
    /// different one-handed weapon in each hand (dual-wield). Returns the handed-over weapon
    /// GameObject (or null on failure).
    /// </summary>
    public static GameObject EquipInto(GameObject playerRoot, WeaponData weapon, bool toLeftHand = false)
    {
        if (playerRoot == null || weapon == null) return null;

        EnsureCombatStack(playerRoot);
        var combat = playerRoot.GetComponent<CombatController>();
        var caster = playerRoot.GetComponent<SpellCaster>();
        var stats = playerRoot.GetComponent<PlayerStats>();
        if (combat == null) return null;

        var weaponGo = BuildRig(playerRoot, weapon, caster, stats, out var behavior);
        if (weaponGo == null || behavior == null)
        {
            if (weaponGo != null) Object.Destroy(weaponGo);
            return null;
        }

        // Replace only the targeted hand slot so the other hand's weapon is preserved.
        if (toLeftHand)
            ClearHand(combat.LeftHand);
        else
            ClearHand(combat.RightHand);

        AttachToHand(playerRoot, weaponGo, toLeftHand);
        if (toLeftHand)
            combat.LeftHand = weaponGo;
        else
            combat.RightHand = weaponGo;

        // Wielding (single / dual / two-hand-grip) derives from what is actually in the hands.
        combat.RecomputeWielding();
        var wielding = combat.Wielding;
        LogOnce("equip-" + weapon.id,
            "equipped '" + weapon.id + "' -> RightHand=" +
            (combat.RightHand != null ? combat.RightHand.transform.parent != null ? combat.RightHand.transform.parent.name : "?" : "null") +
            ", LeftHand=" +
            (combat.LeftHand != null ? combat.LeftHand.transform.parent != null ? combat.LeftHand.transform.parent.name : "?" : "null"));
        return weaponGo;
    }

    private static void ClearHand(GameObject rig)
    {
        if (rig != null) Object.Destroy(rig);
    }

    /// <summary>
    /// Parent a built rig to the player model's shoulder hand (right by default, left when
    /// <paramref name="isLeft"/>). The block weapons are authored +Y-up with the grip/pommel at
    /// the base, so the root sits in the fist with the blade leaning forward (see
    /// <see cref="ApplyHandPose"/>). Falls back to the player root when no hand bone exists
    /// (seated/sitting models have no arm pivots).
    /// </summary>
    private static void AttachToHand(GameObject playerRoot, GameObject weaponGo, bool isLeft)
    {
        if (weaponGo == null) return;
        var t = weaponGo.transform;
        var hand = FindHand(playerRoot?.transform, isLeft);
        if (hand == null || hand == playerRoot.transform)
        {
            // No model hand bone yet — park the weapon upright in front of the chest (same
            // blade-up + forward lean as ApplyHandPose) so attack swings read correctly there.
            // ReparentToHands migrates it onto the bone when it exists.
            t.SetParent(playerRoot.transform, false);
            t.localPosition = new Vector3(isLeft ? -0.33f : 0.33f, 0.9f, 0.9f);
            t.localRotation = Quaternion.Euler(WeaponHoldForwardLean, 0f, 0f);
            t.localScale = ScaleForWorld(t.parent, DrawScale);
            LogOnce("attach-fallback-" + weaponGo.name + "-" + isLeft,
                "hand bone missing for " + (isLeft ? "left" : "right") + "; parked at chest-front pose on '" + playerRoot.name + "'");
            return;
        }
        t.SetParent(hand, false);
        ApplyHandPose(t, isLeft);
        LogOnce("attach-hand-" + weaponGo.name + "-" + isLeft,
            weaponGo.name + " attached to '" + hand.name + "'");
    }

    /// <summary>
    /// Forward tilt of the held weapon's blade. The block weapons are authored +Y-up (blade up,
    /// grip at base); WeaponAnimator's swings are authored against that convention (they rotate
    /// about the blade-up axes), so the rest must keep the blade roughly vertical. This positive
    /// pitch leans the up-blade FORWARD (+Z) so the weapon reads as held in front of the player
    /// rather than up the torso/back, while keeping the attack-swing axes valid. ~20-30° balances
    /// the "in front" look against the long tip clipping the first-person view / surroundings.
    /// </summary>
    public const float WeaponHoldForwardLean = 25f;

    /// <summary>
    /// Uniform scale applied while a weapon is stowed on the body (waist/back). Stow anchors hang
    /// under an identity-scaled node, so local == world here. Equals <see cref="DrawScale"/> so the
    /// weapon keeps one consistent size across modes. Tune against the model in-editor.
    /// </summary>
    public const float StowScale = 0.8f;

    /// <summary>
    /// Local scale that yields a uniform world shape of <paramref name="world"/> units for a rig
    /// parented to <paramref name="parent"/>, compensating for any (non-uniform) parent scale such
    /// as the <c>HandL/R</c> visual blocks. Falls back to uniform <paramref name="world"/> when the
    /// parent is null.
    /// </summary>
    public static Vector3 ScaleForWorld(Transform parent, float world)
    {
        if (parent == null)
            return Vector3.one * world;
        Vector3 p = parent.lossyScale;
        return new Vector3(
            world / Mathf.Max(p.x, 1e-4f),
            world / Mathf.Max(p.y, 1e-4f),
            world / Mathf.Max(p.z, 1e-4f));
    }

    private static void ApplyHandPose(Transform t, bool isLeft)
    {
        // The block weapons are authored +Y-up with the grip at the base. At the large in-hand
        // scale the grip centre sits ~1 unit above the root, so sink the root below the fist so
        // the visible weapon is held (grip ≈ hand) instead of floating above it.
        //
        // Orientation: blade grows up from the grip (WeaponAnimator swings depend on this frame)
        // and leans forward by WeaponHoldForwardLean so it sits in front of the body, not on the
        // back (the old -12° leaned it backward) and not sideways (the removed horizontal aim).
        t.localPosition = new Vector3(isLeft ? -0.1f : 0.1f, -1.0f, 0f);
        t.localRotation = Quaternion.Euler(WeaponHoldForwardLean, 0f, 0f);
        t.localScale = ScaleForWorld(t.parent, DrawScale);
    }

    /// <summary>
    /// Re-seat any equipped rigs onto the player model's hands. The model is built/rebound
    /// independently of weapon equipping, so an equip that ran before the limbs existed (or after a
    /// model reload) can leave the rig parented to the player root and hidden inside the body.
    /// </summary>
    public static void ReparentToHands(GameObject playerRoot)
    {
        if (playerRoot == null) return;
        var combat = playerRoot.GetComponent<CombatController>();
        if (combat == null) return;
        ReparentHand(playerRoot, combat.RightHand, false);
        ReparentHand(playerRoot, combat.LeftHand, true);
    }

    private static void ReparentHand(GameObject playerRoot, GameObject rig, bool isLeft)
    {
        if (rig == null) return;
        var t = rig.transform;
        var hand = FindHand(playerRoot?.transform, isLeft);
        if (hand == null || hand == playerRoot.transform) return;
        if (t.parent == hand) return;
        t.SetParent(hand, false);
        ApplyHandPose(t, isLeft);
        LogOnce("reparent-" + rig.name + "-" + isLeft,
            rig.name + " re-parented onto '" + hand.name + "'");
    }

    // ────────────────────────────────────────────────────────────────────
    //  DRAW / STOW  (out-of-combat sheathing)
    //  When the player is not in fighting mode the equipped weapon moves out
    //  of the hand to a visible body anchor (waist scabbard / back carry),
    //  animated through a WeaponStowAnimator. This drives that pose swap.
    // ────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, Transform> _stowAnchorCache = new Dictionary<string, Transform>();

    /// <summary>
    /// Ensure a <see cref="WeaponStowAnimator"/> exists on the player and place all equipped
    /// rigs in the requested pose. <paramref name="draw"/> = true moves rigs into the hands
    /// (combat), false moves them to their stow anchor (out of combat). <paramref name="instant"/>
    /// snaps immediately (model rebuild) instead of animating.
    /// </summary>
    public static void ApplyPose(GameObject playerRoot, bool draw, bool instant = false)
    {
        if (playerRoot == null) return;
        var combat = playerRoot.GetComponent<CombatController>();
        if (combat == null) return;

        var animator = playerRoot.GetComponent<WeaponStowAnimator>();
        if (animator == null)
            animator = playerRoot.AddComponent<WeaponStowAnimator>();

        var anchorParent = StowAnchorParent(playerRoot);
        animator.AnchorParent = anchorParent;
        animator.Prune();

        if (combat.RightHand != null)
            RegisterStow(animator, playerRoot, combat.RightHand, false);
        if (combat.LeftHand != null)
            RegisterStow(animator, playerRoot, combat.LeftHand, true);

        if (instant)
            animator.Snap(draw);
        else
            animator.SetPose(draw);
    }

    private static void RegisterStow(WeaponStowAnimator animator, GameObject playerRoot, GameObject rig, bool isLeft)
    {
        if (rig == null) return;
        var host = rig.GetComponent<WeaponRigHost>();
        if (host == null || host.Data == null) return;

        var hand = FindHand(playerRoot?.transform, isLeft);
        if (hand == null || hand == playerRoot.transform) return;

        bool waist = IsWaistStow(host.Data);
        var stow = GetStowAnchor(playerRoot, waist);
        var stowDef = StowPoseFor(host.Data, waist, isLeft);

        animator.Register(rig.transform, hand, DrawPos(isLeft), DrawRot(isLeft),
            stow, stowDef.pos, stowDef.rot, DrawScale, StowScale);
    }

    private static Vector3 DrawPos(bool isLeft) => new Vector3(isLeft ? -0.1f : 0.1f, -1.0f, 0f);
    private static Quaternion DrawRot(bool isLeft) => Quaternion.Euler(WeaponHoldForwardLean, 0f, 0f);

    /// <summary>Whether a weapon sheaths at the waist (hip) rather than on the back.</summary>
    private static bool IsWaistStow(WeaponData w)
    {
        switch (w.id)
        {
            case "iron_sword":
            case "dagger":
            case "katana":
            case "holy_book":
            case "gauntlets":
                return true;
            default:
                return false;
        }
    }

    private static (Vector3 pos, Quaternion rot) StowPoseFor(WeaponData w, bool waist, bool isLeft)
    {
        // Waist scabbard: grip at the hip, blade angled up-and-back behind the shoulder (keeps the
        // oversized blade off the floor and reads as a sheathed one-hander).
        if (waist)
        {
            float side = isLeft ? -1f : 1f;
            switch (w.id)
            {
                case "holy_book":
                    // Flat book at the side, cover facing forward.
                    return (new Vector3(side * 0.18f, -0.08f, -0.1f), Quaternion.Euler(0f, 0f, -16f * side));
                case "gauntlets":
                    // Fists hang at the hip.
                    return (new Vector3(side * 0.16f, -0.16f, -0.08f), Quaternion.Euler(-20f, 0f, 20f * side));
                default:
                    // Blades: grip near the hip, tip riding up over the shoulder.
                    return (new Vector3(side * 0.2f, -0.12f, -0.12f), Quaternion.Euler(-38f, 0f, 12f * side));
            }
        }

        // Back carry: grip behind the shoulder, blade/staff up with a slight backward tilt.
        return (new Vector3(0f, 0.32f, -0.2f), Quaternion.Euler(-8f, 0f, 0f));
    }

    /// <summary>The node carrying the weapon stow anchors (Torso, so they follow waist/back).</summary>
    private static Transform StowAnchorParent(GameObject playerRoot)
    {
        if (playerRoot == null) return null;
        var model = playerRoot.transform.Find("PlayerModel");
        var torso = model != null ? model.Find("Torso") : null;
        return torso != null ? torso : (model != null ? model : playerRoot.transform);
    }

    private static Transform GetStowAnchor(GameObject playerRoot, bool waist)
    {
        var parent = StowAnchorParent(playerRoot);
        string name = waist ? "StowWaist" : "StowBack";
        var key = parent != null ? parent.name + "/" + name : name;
        if (_stowAnchorCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var go = new GameObject(name);
        if (parent != null)
            go.transform.SetParent(parent, false);
        var t = go.transform;
        _stowAnchorCache[key] = t;
        return t;
    }

    /// <summary>Resolve the standing player model's hand transform (null when unavailable).</summary>
    private static Transform FindHand(Transform playerRoot, bool isLeft)
    {
        if (playerRoot == null) return null;
        var model = playerRoot.Find("PlayerModel");
        if (model == null) return null;
        var shoulder = model.Find(isLeft ? "Torso/ShoulderL" : "Torso/ShoulderR")
            ?? model.Find(isLeft ? "ShoulderL" : "ShoulderR");
        if (shoulder == null) return null;
        // The hand hangs below Shoulder -> Elbow, so search the whole arm chain.
        return FindDescendant(shoulder, isLeft ? "HandL" : "HandR");
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var t = FindDescendant(root.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    private static GameObject BuildRig(GameObject playerRoot, WeaponData weapon,
        SpellCaster caster, PlayerStats stats, out IWeaponBehavior behavior)
    {
        behavior = null;
        var go = new GameObject("Wpn_" + weapon.id);
        go.transform.SetParent(playerRoot.transform, false);

        // WeaponData is a ScriptableObject asset (data-only, §3.6 Layer 1) — not a Component.
        // Carry it by reference on a lightweight host so the UI/display and behaviors can read it.
        var host = go.AddComponent<WeaponRigHost>();
        host.Data = weapon;

        switch (weapon.Category)
        {
            case WeaponCategory.Melee:
                var melee = go.AddComponent<MeleeWeaponBehavior>();
                melee.Data = weapon;
                melee.AttackDamage = weapon.BaseDamage;
                melee.Hitbox = go.GetComponent<HitboxSystem>();
                melee.Stats = stats;
                WeaponModelBuilder.Build(weapon.id, go.transform);
                behavior = melee;
                break;

            case WeaponCategory.Ranged:
                var ranged = go.AddComponent<RangedWeaponBehavior>();
                ranged.Data = weapon;
                ranged.Stats = stats;
                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(go.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 0.1f, 1f);
                ranged.Muzzle = muzzle.transform;
                WeaponModelBuilder.Build(weapon.id, go.transform);
                behavior = ranged;
                break;

            case WeaponCategory.Magic:
                var magic = go.AddComponent<MagicWeaponBehavior>();
                magic.Data = weapon;
                magic.Caster = caster;
                magic.CastOrigin = go.transform;
                WeaponModelBuilder.Build(weapon.id, go.transform);
                behavior = magic;
                break;

            default:
                Object.Destroy(go);
                return null;
        }

        var skillExec = go.AddComponent<WeaponSkillExecutor>();
        skillExec.Data = weapon;
        skillExec.Caster = caster;

        // Per-weapon attack animation — lives on the rig so the model animates by equipped weapon.
        go.AddComponent<WeaponAnimator>();

        return go;
    }

}