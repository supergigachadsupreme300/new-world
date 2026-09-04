using System;
using UnityEngine;

/// <summary>
/// Magic weapon behavior (§3.6 Layer 3). Does not deliver its own attack — magic weapons
/// are equipped gear that scales/alters spells (§3.6 Notes). It routes casts to the shared
/// SpellCaster (§3.8), applying the staff/wand/book magic-mods (MagicDamageMult,
/// CastTimeMod, CooldownMod) and pointing the cast at the spell currently bound to the weapon.
///
/// Attach to the weapon/root. Assign Data (WeaponData, category Magic) and Caster
/// (the owner's SpellCaster). The active spell to cast is Data.Art-driven or the bound Spell.
/// </summary>
public class MagicWeaponBehavior : MonoBehaviour, IWeaponBehavior
{
    [Header("Wiring")]
    public WeaponData Data;
    public SpellCaster Caster;
    public Transform CastOrigin;

    [Header("Bound Spell (§3.8)")]
    [Tooltip("Spell to cast. If null, falls back to any spell granted via active skills.")]
    public SpellData BoundSpell;

    private bool _attacking;

    public event Action Completed;
    public bool IsAttacking => _attacking;

    private void Awake()
    {
        if (Caster == null)
            Caster = GetComponentInParent<SpellCaster>();
        if (CastOrigin == null)
            CastOrigin = transform;
    }

    public void BeginAttack(AttackCommand cmd)
    {
        if (_attacking) return;
        if (Caster == null) { Completed?.Invoke(); return; }

        SpellData spell = BoundSpell;
        if (spell == null)
        {
            // No bound spell — nothing to cast.
            Completed?.Invoke();
            return;
        }

        _attacking = true;
        var mods = MagicWeaponMods.FromWeapon(Data);
        bool started = Caster.BeginCast(spell, CastOrigin, mods);
        _attacking = false;
        if (started)
            OnCast?.Invoke(spell);
        Completed?.Invoke();
    }

    public void ActiveFrame()
    {
    }

    public void Cancel()
    {
        _attacking = false;
    }

    /// <summary>Fires when a spell successfully begins casting through this weapon.</summary>
    public event Action<SpellData> OnCast;
}
