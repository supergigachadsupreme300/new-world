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
    /// Rig <paramref name="weapon"/> onto <paramref name="playerRoot"/> and assign it to the
    /// combat controller's hands with the appropriate wielding state. Returns the handed-over
    /// weapon GameObject (or null on failure).
    /// </summary>
    public static GameObject EquipInto(GameObject playerRoot, WeaponData weapon)
    {
        if (playerRoot == null || weapon == null) return null;

        EnsureCombatStack(playerRoot);
        var combat = playerRoot.GetComponent<CombatController>();
        var caster = playerRoot.GetComponent<SpellCaster>();
        var stats = playerRoot.GetComponent<PlayerStats>();
        if (combat == null) return null;

        var weaponGo = BuildRig(playerRoot, weapon, caster, stats, out var behavior);
        if (weaponGo == null || behavior == null) return null;

        var wielding = WieldingFor(weapon);
        switch (wielding)
        {
            case CombatController.WieldingState.Dual:
                combat.LeftHand = weaponGo;
                combat.RightHand = CloneRig(playerRoot, weapon, caster, stats);
                break;
            case CombatController.WieldingState.TwoHand:
                combat.RightHand = weaponGo;
                combat.LeftHand = null;
                break;
            case CombatController.WieldingState.Single:
            default:
                combat.RightHand = weaponGo;
                combat.LeftHand = null;
                break;
        }
        combat.Wielding = wielding;
        return weaponGo;
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
                AddProxy(go, PrimitiveType.Cylinder, new Vector3(0.12f, 0.55f, 0.12f), new Color(0.7f, 0.7f, 0.72f));
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
                AddProxy(go, PrimitiveType.Cylinder, new Vector3(0.06f, 0.7f, 0.06f), new Color(0.45f, 0.3f, 0.2f));
                behavior = ranged;
                break;

            case WeaponCategory.Magic:
                var magic = go.AddComponent<MagicWeaponBehavior>();
                magic.Data = weapon;
                magic.Caster = caster;
                magic.CastOrigin = go.transform;
                AddProxy(go, PrimitiveType.Sphere, new Vector3(0.4f, 0.4f, 0.4f), new Color(0.3f, 0.5f, 0.9f));
                behavior = magic;
                break;

            default:
                Object.Destroy(go);
                return null;
        }

        var art = go.AddComponent<WeaponArtExecutor>();
        art.Data = weapon;
        art.Caster = caster;

        return go;
    }

    private static GameObject CloneRig(GameObject playerRoot, WeaponData weapon,
        SpellCaster caster, PlayerStats stats)
    {
        var go = BuildRig(playerRoot, weapon, caster, stats, out var ignored);
        return go;
    }

    private static void AddProxy(GameObject parent, PrimitiveType shape, Vector3 localScale, Color color)
    {
        var proxy = GameObject.CreatePrimitive(shape);
        proxy.name = "Proxy";
        proxy.transform.SetParent(parent.transform, false);
        proxy.transform.localScale = localScale;
        proxy.transform.localPosition = new Vector3(0f, localScale.y * 0.5f, 0f);
        var mr = proxy.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mr.sharedMaterial = new Material(shader) { color = color };
        }
        var proxyCol = proxy.GetComponent<Collider>();
        if (proxyCol != null)
            Object.Destroy(proxyCol);
    }
}