/// <summary>Partial player controller — combat cluster: damage/heal, class buffs (stealth, aura,
/// flight), combat-mode toggle, dual-wield and aim/charge input, weapon pose, and the AoE/cast
/// previews. Mechanically split from PlayerController.cs; no behavior or signature changes.</summary>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    public bool FightingMode { get; private set; }

    /// <summary>Max health (HP cap). Reads the current <see cref="PlayerStats"/> maximum
    /// (Health-scaled + tree perks §3.3) when available; falls back to 100 before stats are rigged.</summary>
    public int MaxHP
    {
        get
        {
            var stats = StatsCached;
            return stats != null ? Mathf.Max(1, Mathf.RoundToInt(stats.MaxHP)) : 100;
        }
    }

    public void TakeDamage(int amount)
    {
        if (HP <= 0) return;
        if (Time.time < _invulnerableUntil) return;
        // Aura buff: flat damage reduction from class aura skills (clamped to sane bounds).
        if (_classBuffDamageReduction > 0f && Time.time < _classBuffUntil)
            amount = Mathf.RoundToInt(amount * (1f - Mathf.Min(_classBuffDamageReduction, 0.5f)));
        // Skill-tree perks: flat damage reduction stacks on top (§3.3).
        var pStats = StatsCached;
        if (pStats != null)
            amount -= Mathf.RoundToInt(amount * Mathf.Min(pStats.DamageReductionPerkFlat, 0.45f));
        // Melee guard: blocking absorbs 80% of the hit while stamina holds; if stamina runs out
        // the guard breaks and the full hit lands.
        var combat = CombatCached;
        if (combat != null && combat.IsBlocking)
        {
            if (combat.OnBlockedHit(amount))
            {
                amount = Mathf.RoundToInt(amount * combat.BlockTakenMultiplier);
                if (amount <= 0) return;
            }
        }
        HP -= amount;
        if (HP <= 0)
        {
            HP = 0;
            Debug.Log("Player died");
            GameManager.Instance?.TriggerPlayerDeath();
        }
        GameManager.Instance?.UIManager?.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

    /// <summary>Restore HP up to max (class heal skills §3.2.1, consumables, miracles).</summary>
    public void Heal(int amount)
    {
        if (HP <= 0 || amount <= 0) return;
        HP = Mathf.Min(MaxHP, HP + amount);
        GameManager.Instance?.UIManager?.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
    }

    /// <summary>
    /// Stealth state — enemies ignore the player while active (Rogue "Vanish").
    /// </summary>
    public bool IsInvisible => Time.time < _stealthUntil;

    /// <summary>Enter stealth for the given duration (hides renderers; enemies skip targeting).</summary>
    public void ActivateStealth(float seconds)
    {
        if (seconds <= 0f) return;
        if (_stealthRenderers == null || _stealthRenderers.Length == 0)
            _stealthRenderers = GetComponentsInChildren<Renderer>(true);
        if (Time.time < _stealthUntil) return; // already hidden
        _stealthUntil = Time.time + seconds;
        foreach (var r in _stealthRenderers)
            if (r != null) r.enabled = false;
    }

    /// <summary>Grant the class aura buff (damage reduction + passive HP regen) for the duration.</summary>
    public void ApplyClassBuff(float seconds, float damageReduction, float hpRegenPerSecond)
    {
        _classBuffUntil = Mathf.Max(_classBuffUntil, Time.time + Mathf.Max(seconds, 0f));
        _classBuffDamageReduction = Mathf.Max(_classBuffDamageReduction, damageReduction);
        _classBuffHpRegenPerSecond = Mathf.Max(_classBuffHpRegenPerSecond, hpRegenPerSecond);
    }

    /// <summary>Start flying for the given duration (free vertical movement, no gravity).</summary>
    public void BeginFlight(float seconds)
    {
        if (seconds <= 0f) return;
        _flightUntil = Mathf.Max(_flightUntil, Time.time + seconds);
    }

    /// <summary>End flight immediately; gravity resumes and the player falls/lands normally.</summary>
    public void EndFlight()
    {
        _flightUntil = 0f;
    }

    /// <summary>Tick stealth expiry + aura buff lifetime once per frame.</summary>
    private void UpdateClassState()
    {
        // Stealth expiry un-hides renderers.
        if (_stealthUntil > 0f && Time.time >= _stealthUntil)
        {
            _stealthUntil = 0f;
            if (_stealthRenderers != null)
                foreach (var r in _stealthRenderers)
                    if (r != null) r.enabled = true;
        }
        // Aura buff expiry resets both components together.
        if (_classBuffUntil > 0f && Time.time >= _classBuffUntil)
        {
            _classBuffUntil = 0f;
            _classBuffDamageReduction = 0f;
            _classBuffHpRegenPerSecond = 0f;
        }
    }

    private void ToggleCombatMode()
    {
        if (FightingMode)
        {
            FightingMode = false;
            GameManager.Instance?.UIManager?.SetHotbarVisible(true);
            var skillBar = Object.FindAnyObjectByType<SkillBarHUD>();
            if (skillBar != null) skillBar.SetVisible(false);
            if (_cachedFightSlot >= 0)
                ToolManager.Instance?.SelectSlot(_cachedFightSlot);
            _cachedFightSlot = -1;
            ShowPrompt(Localization.T("Casual mode."));
            // Sheathe the equipped weapon onto the body (waist/back) — casual mode never holds a
            // fighting pose, in any camera view.
            ReApplyWeaponPose(instant: false);
        }
        else
        {
            FightingMode = true;
            var tm = ToolManager.Instance;
            _cachedFightSlot = tm != null ? tm.SelectedSlotIndex : -1;
            ToolManager.Instance?.ResetSelection();
            GameManager.Instance?.UIManager?.SetHotbarVisible(false);
            var skillBar = Object.FindAnyObjectByType<SkillBarHUD>();
            if (skillBar != null) skillBar.SetVisible(true);
            TryAutoRigWeapon();
            // Draw the weapon from its stow point into the hand.
            ReApplyWeaponPose(instant: false);
        }
    }

    /// <summary>True while the equipped weapon is mid draw/stow transition (attacks gated).</summary>
    private bool WeaponTransitionBusy()
    {
        var animator = GetComponent<WeaponStowAnimator>();
        return animator != null && animator.IsBusy;
    }

    /// <summary>True when the equipped hand(s) hold a melee weapon (incl. bare fists).</summary>
    private bool IsMeleeEquipped(CombatController combat)
    {
        var hand = combat.RightHand ?? combat.LeftHand;
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null && host.Data.Category == WeaponCategory.Melee;
    }

    /// <summary>True when a shield weapon is held in either hand (enables RMB block + strict guard).</summary>
    private bool IsShieldEquipped(CombatController combat)
    {
        return combat != null && combat.HasShield;
    }

    /// <summary>
    /// Per-hand dual-wield input (§5.16). Both hands carry real weapons (not both magic — that keeps
    /// the normal magic flow). LMB drives one hand and RMB the other: same-side when no ranged weapon
    /// is present, CROSSED (LMB→right hand, RMB→left hand) whenever a bow/throwing hammer is among the
    /// two so the ranged hand keeps its hold-to-charge/release-to-fire draw. Melee swings on press,
    /// shields guard while held, magic fires the armed spell uncharged on press (loses its charge),
    /// ranged charges on hold and fires on release. Only a shield hand can raise a guard (the dual
    /// weapon trade-off).
    /// </summary>
    private void HandleDualModeCombat(CombatController combat, bool inputAvailable)
    {
        if (Mouse.current == null) return;

        bool crossed = combat.HasRangedDual;
        GameObject lmbHand = crossed ? combat.RightHand : combat.LeftHand;
        GameObject rmbHand = crossed ? combat.LeftHand : combat.RightHand;

        var lmb = Mouse.current.leftButton;
        var rmb = Mouse.current.rightButton;

        bool blockRequested = false;
        HandleDualHand(combat, lmbHand, lmb.isPressed, lmb.wasPressedThisFrame,
            lmb.wasReleasedThisFrame || !lmb.isPressed, inputAvailable,
            ref _dualChargeL, ref _dualChargeActiveL, ref blockRequested);
        HandleDualHand(combat, rmbHand, rmb.isPressed, rmb.wasPressedThisFrame,
            rmb.wasReleasedThisFrame || !rmb.isPressed, inputAvailable,
            ref _dualChargeR, ref _dualChargeActiveR, ref blockRequested);

        combat.SetBlocking(blockRequested);
    }

    /// <summary>Drive a single hand's weapon from its mapped mouse button in dual mode.</summary>
    private void HandleDualHand(CombatController combat, GameObject hand, bool held, bool pressed,
        bool released, bool inputAvailable, ref float charge, ref bool charging, ref bool blockRequested)
    {
        if (hand == null) return;

        switch (combat.CategoryOfHand(hand))
        {
            case WeaponCategory.Melee:
                // Swing on press. A raised guard (other hand's shield) drops first so the swing lands.
                if (pressed && inputAvailable && !WeaponTransitionBusy())
                {
                    if (combat.IsBlocking) combat.SetBlocking(false);
                    combat.LightAttackWith(hand);
                }
                break;

            case WeaponCategory.Shield:
                // Hold to guard, release to unguard. The caller re-applies SetBlocking after both
                // hands so a same-frame attack click takes the guard down again.
                if (held && inputAvailable) blockRequested = true;
                break;

            case WeaponCategory.Magic:
                // Magic loses its charge in a mixed dual: a tap fires the armed spell uncharged.
                if (pressed && inputAvailable && !WeaponTransitionBusy())
                {
                    if (combat.IsBlocking) combat.SetBlocking(false);
                    MagicWheelUI.EnsureArmedMagic();
                    MagicWheelUI.ReleaseArmedCast(0f);
                }
                break;

            case WeaponCategory.Ranged:
                // Ranged keeps its charge in dual: hold to draw, release to fire at that level.
                if (pressed && inputAvailable)
                {
                    charging = true;
                    charge = 0f;
                }
                if (!charging) break;
                if (held)
                {
                    charge = Mathf.Min(charge + Time.deltaTime, MagicChargeMaxTime);
                    ShowRangedPathPreview(hand.GetComponent<RangedWeaponBehavior>(), hand, charge);
                }
                combat.SetChargeLevel(MagicChargeLevel(charge));
                if (released)
                {
                    float level = MagicChargeLevel(charge);
                    charging = false;
                    charge = 0f;
                    HidePathPreview();
                    if (inputAvailable && !WeaponTransitionBusy())
                        combat.FireRangedWith(hand, level);
                    combat.EndCharge(level > 0f);
                }
                break;
        }
    }

    /// <summary>True when the equipped hand(s) hold a ranged weapon (bow / throwing hammer).</summary>
    private bool IsRangedEquipped(CombatController combat)
    {
        var hand = combat != null ? (combat.RightHand ?? combat.LeftHand) : null;
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null && host.Data.Category == WeaponCategory.Ranged;
    }

    /// <summary>True while an aim/charge is in progress (drives the HUD charge bar visibility).</summary>
    public bool IsCharging => _aiming && (_chargeRmbHeld || _chargeAccum > 0f);

    /// <summary>Charge progress for the HUD. Magic overcharges past 100% (level 1+ keeps climbing);
    /// ranged picks only the capped draw level.</summary>
    public float MagicChargeProgress => IsCharging
        ? (ArmedSpell() != null ? SpellChargeLevel(_chargeAccum) : MagicChargeLevel(_chargeAccum))
        : 0f;

    /// <summary>Charge level (0..1) for a held cast; taps under the threshold cast uncharged.</summary>
    private static float MagicChargeLevel(float holdTime)
    {
        float t = (holdTime - MagicChargeTapThreshold) / (MagicChargeMaxTime - MagicChargeTapThreshold);
        return Mathf.Clamp01(t);
    }

    /// <summary>
    /// Uncapped charge level for held magic casts: same ramp as <see cref="MagicChargeLevel"/> but
    /// with no upper limit, so overcharging past full grows power/size/cost and real-time FP drain
    /// becomes the only ceiling. Taps under the threshold sit at 0 (cast uncharged).
    /// </summary>
    private static float SpellChargeLevel(float holdTime)
    {
        float t = (holdTime - MagicChargeTapThreshold) / (MagicChargeFullTime - MagicChargeTapThreshold);
        return Mathf.Max(t, 0f);
    }

    /// <summary>
    /// Show/refresh the AoE landing preview each aim frame — but only for armed zone/vortex
    /// magic, so projectile/instant spells and ranged weapons get no marker.
    /// </summary>
    private void UpdateAoePreview(float charge)
    {
        var spell = ArmedSpell();
        if (spell != null && (spell.Delivery == SpellDelivery.Zone || spell.Delivery == SpellDelivery.Vortex
            || spell.Delivery == SpellDelivery.Summon || spell.Delivery == SpellDelivery.Storm))
        {
            if (TryAoeTarget(spell, charge, out var center, out var radius, out var color))
                AoePreview().Show(center, radius, color);
        }
        else
        {
            HideAoePreview();
        }
    }

    private void HideAoePreview()
    {
        if (_aoePreview != null)
            _aoePreview.Hide();
    }

    private AoeAimPreview AoePreview()
    {
        if (_aoePreview == null)
            _aoePreview = AoeAimPreview.Instance;
        return _aoePreview;
    }

    /// <summary>
    /// Show/refresh the halo casting circle around the held magic weapon each aim frame. Only
    /// armed magic gets the halo — ranged draws show their own weapon accent instead.
    /// </summary>
    private void UpdateCastingCircle(float charge)
    {
        if (!MagicWheelUI.HasArmedMagic())
        {
            HideCastingCircle();
            return;
        }
        var combat = CombatCached;
        var hand = MagicHand(combat);
        if (hand == null)
        {
            HideCastingCircle();
            return;
        }
        var spell = ArmedSpell();
        Color color;
        if (spell != null) color = DamageNumber.ColorFor(spell.Type);
        else
        {
            var skill = SkillCatalog.Find(MagicWheelUI.ArmedSkillId);
            color = skill != null ? DamageNumber.ColorFor(skill.DamageKind) : Color.white;
        }
        Casting().Show(hand.transform, charge, color);
    }

    /// <summary>One-shot expansion ring at the magic weapon on cast release.</summary>
    private void BurstCastingCircle(float charge)
    {
        var combat = CombatCached;
        var hand = MagicHand(combat);
        if (hand == null) return;
        var spell = ArmedSpell();
        Color color = spell != null ? DamageNumber.ColorFor(spell.Type) : Color.white;
        float radius = spell != null ? spell.Radius : 1.5f;
        Casting().Burst(radius * (0.6f + charge * 0.5f), color, hand.transform.up);
        HideCastingCircle();
    }

    private void HideCastingCircle()
    {
        if (_castingCircle != null)
            _castingCircle.Hide();
    }

    private CastingCircle Casting()
    {
        if (_castingCircle == null)
            _castingCircle = CastingCircle.Instance;
        return _castingCircle;
    }

    /// <summary>
    /// Show/refresh the projectile flight-path preview each aim frame — a wide cone over the
    /// possible spread that narrows into a precision ray as the draw/charge builds. Covers the
    /// bow &amp; throwing hammer (true Dexterity spread) and projectile magic (straight laser,
    /// shrinking cone is focus feedback).
    /// </summary>
    private void UpdatePathPreview(float charge, SpellData armedSpell)
    {
        if (armedSpell != null && armedSpell.Delivery == SpellDelivery.Projectile)
        {
            var combat = CombatCached;
            var hand = MagicHand(combat);
            var cam = MainCam;
            if (hand == null || cam == null) { HidePathPreview(); return; }

            // Mirror caster aim (SpellCaster.Execute): from the hand toward the camera line.
            Vector3 pos = hand.transform.position;
            Vector3 fwd = cam.transform.position + cam.transform.forward * Mathf.Max(armedSpell.Range, 5f) - pos;
            if (fwd.sqrMagnitude < 0.0001f) fwd = hand.transform.forward; else fwd = fwd.normalized;

            float c = Mathf.Clamp01(charge);
            float reach = Mathf.Max(armedSpell.ProjectileSpeed, 1f) * 4f; // SpellEffect flight envelope
            PathPreview().Show(pos + fwd * 0.5f, fwd, reach,
                8f * (1f - c), DamageNumber.ColorFor(armedSpell.Type), transform);
            return;
        }

        var rangedCombat = CombatCached;
        var hand2 = rangedCombat != null ? (rangedCombat.RightHand ?? rangedCombat.LeftHand) : null;
        var ranged = hand2 != null ? hand2.GetComponent<RangedWeaponBehavior>() : null;
        if (ranged == null) { HidePathPreview(); return; }
        ShowRangedPathPreview(ranged, hand2, charge);
    }

    /// <summary>Bounded ranged-weapon preview (regular aim or dual per-hand draw).</summary>
    private void ShowRangedPathPreview(RangedWeaponBehavior ranged, GameObject hand, float charge)
    {
        if (ranged == null) { HidePathPreview(); return; }

        float accuracy = 1f;
        if (ranged.Stats != null && ranged.Data != null)
            accuracy = 1f + ranged.Stats.GetStat(WeaponScalingStat.Dexterity) * ranged.Data.AccuracyFromDex;
        float spread = Mathf.Atan2(0.15f / Mathf.Max(accuracy, 0.01f), 1f) * Mathf.Rad2Deg;

        float c = Mathf.Clamp01(charge);
        float speed = ranged.ProjectileSpeed * Mathf.Lerp(1f, 1.5f, c);
        float lifetime = ranged.BaseLifetime * Mathf.Lerp(1f, 2f, c);
        Vector3 origin = ranged.Muzzle != null ? ranged.Muzzle.position
            : hand != null ? hand.transform.position : transform.position;
        PathPreview().Show(origin, transform.forward, speed * lifetime,
            spread * (1f - c), DamageNumber.ColorFor(ranged.ShotType), transform);
    }

    private void HidePathPreview()
    {
        if (_pathPreview != null)
            _pathPreview.Hide();
    }

    private ProjectilePathPreview PathPreview()
    {
        if (_pathPreview == null)
            _pathPreview = ProjectilePathPreview.Instance;
        return _pathPreview;
    }

    /// <summary>Returns the equipped hand holding a magic weapon, or null.</summary>
    private GameObject MagicHand(CombatController combat)
    {
        if (combat == null) return null;
        if (combat.RightHand != null && HandIsMagic(combat.RightHand)) return combat.RightHand;
        if (combat.LeftHand != null && HandIsMagic(combat.LeftHand)) return combat.LeftHand;
        return null;
    }

    private static bool HandIsMagic(GameObject hand)
    {
        if (hand == null) return false;
        var host = hand.GetComponent<WeaponRigHost>();
        return host != null && host.Data != null && host.Data.Category == WeaponCategory.Magic;
    }

    /// <summary>True while a beam channel is active (LMB is sustaining the beam, so it must not re-aim/attack).</summary>
    private bool BeamChanneling()
    {
        var caster = SpellCasterRef;
        return caster != null && caster.IsChanneling;
    }

    /// <summary>SpellData of the armed magic skill (SpellCastEffect), or null when none is previewable.</summary>
    private SpellData ArmedSpell()
    {
        string id = MagicWheelUI.ArmedSkillId;
        if (string.IsNullOrEmpty(id)) return null;
        var skill = SkillCatalog.Find(id);
        if (skill == null || skill.Effect is not SpellCastEffect cast || cast.Spell == null)
            return null;
        return cast.Spell;
    }

    /// <summary>
    /// Project the ground target for an AoE spell — mirrors <see cref="SpellCaster"/> zone/vortex
    /// placement (ray along the camera forward to the spell range, then dropped to the ground).
    /// The radius grows with the charge level using the caster's charge-size bonus.
    /// </summary>
    private bool TryAoeTarget(SpellData spell, float charge, out Vector3 center, out float radius, out Color color)
    {
        center = transform.position;
        radius = 1f;
        color = Color.white;
        if (spell == null) return false;

        var cam = MainCam;
        if (cam == null) return false;

        Vector3 pos = cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        // Ground deliveries land where the camera points — out to the practical GroundAimMax cap,
        // mirroring SpellCaster.Execute's unbounded aim (matches `RunBenchSpawn`-style far placement).
        Vector3 at = pos + fwd * SpellCaster.GroundAimMax;
        if (Physics.Raycast(pos, fwd, out RaycastHit aimHit, SpellCaster.GroundAimMax))
            at = aimHit.point;
        center = at;
        if (Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 30f))
            center = groundHit.point;

        var caster = SpellCasterRef;
        float sizeBonus = caster != null ? caster.ChargeSizeBonus : 0.8f;
        radius = spell.Radius * (1f + charge * sizeBonus);
        color = DamageNumber.ColorFor(spell.Type);
        return true;
    }

    /// <summary>True when an in-progress aim/charge should be dropped without firing.</summary>
    private bool ShouldCancelCharge()
    {
        if (GameInput.IsMobile || Mouse.current == null) return true;
        if (!FightingMode || WeaponTransitionBusy()) return true;
        if (MagicWheelUI.IsOpen) return true;
        if (MagicTestMatrix.IsOpen) return true;
        var combat = CombatCached;
        return !MagicWheelUI.HasArmedMagic() && !IsRangedEquipped(combat);
    }

    private void TryAutoRigWeapon()
    {
        WeaponCatalog.EnsureBuilt();
        // If the weapon is out of combat (stowed on the body), leave it there — the draw
        // transition animates it into the hand afterward. Only re-seat onto the hands when the
        // weapon is already drawn (or has no animator yet, e.g. first equip / parked rig).
        var animator = GetComponent<WeaponStowAnimator>();
        bool stowed = animator != null && !animator.IsDrawn;
        if (!stowed)
            WeaponRigBuilder.ReparentToHands(gameObject);
        var combat = CombatCached;
        if (combat != null && (combat.RightHand != null || combat.LeftHand != null)) return;
        // No weapon equipped — fight with the innate bare fists instead of auto-equipping an
        // owned or starter weapon. The player chooses real weapons via the gear sheet.
        WeaponRigBuilder.EnsureFists(gameObject);
    }

    /// <summary>
    /// Re-apply the current draw/stow pose for all equipped weapons based on
    /// <see cref="WeaponsDrawn"/> (combat mode changed; camera switches are harmless no-ops now
    /// that casual always sheathes). No-op until the combat
    /// stack/hands exist so a camera toggle during Awake is safe.
    /// </summary>
    public void ReApplyWeaponPose(bool instant = true)
    {
        var combat = CombatCached;
        if (combat == null) return;
        WeaponRigBuilder.ApplyPose(gameObject, draw: WeaponsDrawn, instant);
    }
}
