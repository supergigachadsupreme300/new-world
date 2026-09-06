using UnityEngine;

/// <summary>
/// Builds a live, combat-active weapon GameObject from a <see cref="WeaponData"/> and
/// equips it onto the player's <see cref="CombatController"/> hands (Phase 10).
///
/// Rigging steps per category:
///   • Melee  → <see cref="MeleeWeaponBehavior"/> + required <see cref="HitboxSystem"/>.
///   • Ranged → <see cref="RangedWeaponBehavior"/> + muzzle point (hitscan fallback, no prefab).
///   • Magic  → <see cref="MagicWeaponBehavior"/> + the owner's <see cref="SpellCaster"/>.
/// Every rig also gets a <see cref="WeaponArtExecutor"/> (for WeaponArt-based skills) and a
/// primitive visual proxy (the repo has no weapon mesh assets).
///
/// The builder ensures the player root carries the combat stack (StaminaSystem +
/// CombatController + SpellCaster + PlayerStats as the IStatProvider) since nothing else
/// wires it today. Composes existing contracts only — no rewrites.
/// </summary>
public static class WeaponRigBuilder
{
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
        return weaponGo;
    }

    private static void ClearHand(GameObject rig)
    {
        if (rig != null) Object.Destroy(rig);
    }

    /// <summary>
    /// Parent a built rig to the player model's shoulder hand (right by default, left when
    /// <paramref name="isLeft"/>). The block weapons are authored +Y-up with the grip/pommel at
    /// the base, so the root sits in the fist with a slight forward lean. Falls back to the
    /// player root when no hand bone exists (seated/sitting models have no arm pivots).
    /// </summary>
    private static void AttachToHand(GameObject playerRoot, GameObject weaponGo, bool isLeft)
    {
        if (weaponGo == null) return;
        var t = weaponGo.transform;
        var hand = FindHand(playerRoot?.transform, isLeft);
        if (hand == null || hand == playerRoot.transform)
        {
            t.SetParent(playerRoot.transform, false);
            return;
        }
        t.SetParent(hand, false);
        // Nudge the grip outward (X) so the blade clears the torso; keep a slight forward lean.
        t.localPosition = new Vector3(isLeft ? -0.02f : 0.02f, -0.05f, 0f);
        t.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        t.localScale = Vector3.one;
    }

    /// <summary>Resolve the standing player model's hand transform (null when unavailable).</summary>
    private static Transform FindHand(Transform playerRoot, bool isLeft)
    {
        if (playerRoot == null) return null;
        var model = playerRoot.Find("PlayerModel");
        if (model == null) return null;
        var shoulder = model.Find(isLeft ? "ShoulderL" : "ShoulderR");
        if (shoulder == null) return null;
        return shoulder.Find(isLeft ? "HandL" : "HandR");
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

        var art = go.AddComponent<WeaponArtExecutor>();
        art.Data = weapon;
        art.Caster = caster;

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