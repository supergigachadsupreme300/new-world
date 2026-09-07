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
    /// Uniform scale-up applied to a weapon when equipped in-hand. The weapon models are authored
    /// rack-scale (fitted to the pedestal), which reads too small held against the ~1.5-unit-tall
    /// blocky player; this bump makes blades/staves/bows look proper relative to the body.
    /// 10× (was 1.25) per design request so held weapons are boldly oversized vs. the hand.
    /// </summary>
    public const float EquipScale = 12.5f;

    private static readonly HashSet<string> _logged = new HashSet<string>();

    private static void LogOnce(string key, string message)
    {
        if (LogRigging && _logged.Add(key)) Debug.Log("[WeaponRig] " + message);
    }

    /// <summary>Default wielding per weapon archetype (guessed from the weapon id).</summary>
    public static CombatController.WieldingState WieldingFor(WeaponData weapon)
    {
        if (weapon == null) return CombatController.WieldingState.Single;
        switch (weapon.id)
        {
            case "greatsword":
            case "greataxe":
            case "katana":
            case "lance":
            case "warhammer":
            case "longbow":
                return CombatController.WieldingState.TwoHand;
            case "gauntlets":
                return CombatController.WieldingState.Dual;
            default:
                return CombatController.WieldingState.Single;
        }
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
    /// the matching player-model hand, and assign it to the combat controller's hands with the
    /// appropriate wielding state. Returns the handed-over weapon GameObject (or null on failure).
    /// </summary>
    public static GameObject EquipInto(GameObject playerRoot, WeaponData weapon, bool toLeftHand = false)
    {
        if (playerRoot == null || weapon == null) return null;

        EnsureCombatStack(playerRoot);
        var combat = playerRoot.GetComponent<CombatController>();
        var caster = playerRoot.GetComponent<SpellCaster>();
        var stats = playerRoot.GetComponent<PlayerStats>();
        if (combat == null) return null;

        var wielding = WieldingFor(weapon);

        var weaponGo = BuildRig(playerRoot, weapon, caster, stats, out var behavior);
        if (weaponGo == null || behavior == null)
        {
            if (weaponGo != null) Object.Destroy(weaponGo);
            return null;
        }

        // Drop the previous rigs so old Wpn_ proxies never pile up on the hands.
        ClearHand(combat.LeftHand);
        ClearHand(combat.RightHand);

        switch (wielding)
        {
            case CombatController.WieldingState.Dual:
                AttachToHand(playerRoot, weaponGo, false);
                var clone = CloneRig(playerRoot, weapon, caster, stats, true);
                if (clone == null)
                {
                    Object.Destroy(weaponGo);
                    return null;
                }
                combat.RightHand = weaponGo;
                combat.LeftHand = clone;
                break;
            case CombatController.WieldingState.TwoHand:
                AttachToHand(playerRoot, weaponGo, false);
                combat.RightHand = weaponGo;
                combat.LeftHand = null;
                break;
            case CombatController.WieldingState.Single:
            default:
                AttachToHand(playerRoot, weaponGo, toLeftHand);
                if (toLeftHand)
                {
                    combat.LeftHand = weaponGo;
                    combat.RightHand = null;
                }
                else
                {
                    combat.RightHand = weaponGo;
                    combat.LeftHand = null;
                }
                break;
        }
        combat.Wielding = wielding;
        LogOnce("equip-" + weapon.id,
            "equipped '" + weapon.id + "' (" + wielding + ") -> RightHand=" +
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
            t.localScale = Vector3.one * EquipScale;
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
        t.localScale = Vector3.one * EquipScale;
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

    private static GameObject CloneRig(GameObject playerRoot, WeaponData weapon,
        SpellCaster caster, PlayerStats stats, bool toLeftHand = false)
    {
        var go = BuildRig(playerRoot, weapon, caster, stats, out var ignored);
        if (go != null)
            AttachToHand(playerRoot, go, toLeftHand);
        return go;
    }

}