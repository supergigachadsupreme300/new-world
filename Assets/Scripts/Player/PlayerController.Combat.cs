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
    /// magic on radial shapes, so projectile/instant spells, ranged weapons, and directional
    /// wall spells get no marker.
    /// </summary>
    private void UpdateAoePreview(float charge)
    {
        var spell = ArmedSpell();
        // 1ga: no circular disc for directional wall-shaped spells — a round footprint is
        // meaningless for a ridge that rears across the cast, so walls get no ground preview
        // (Crater/Ring/Spikes/Pillar are radial and keep their disc).
        if (spell != null && spell.TerrainShape != TerrainShape.Wall
            && (spell.Delivery == SpellDelivery.Zone || spell.Delivery == SpellDelivery.Vortex
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
        // 1if: with a spell behind it, the halo takes the full per-spell look (colour, family,
        // size, tempo) instead of just the school colour. Without one — the preview and ranged-draw
        // paths — it keeps the plain colour overload, which draws the Circle family.
        if (spell != null)
        {
            var look = SpellLook.Resolve(spell);
            Casting().Show(hand.transform, charge, look);
            return;
        }
        var skill = SkillCatalog.Find(MagicWheelUI.ArmedSkillId);
        Color color = skill != null ? DamageNumber.ColorFor(skill.DamageKind) : Color.white;
        Casting().Show(hand.transform, charge, color);
    }

    /// <summary>One-shot expansion ring at the magic weapon on cast release.</summary>
    private void BurstCastingCircle(float charge)
    {
        var combat = CombatCached;
        var hand = MagicHand(combat);
        if (hand == null) return;
        var spell = ArmedSpell();
        if (spell != null)
        {
            // 1if: the release burst takes the same look, so the ring that leaves the weapon is the
            // spell's own colour and size.
            var look = SpellLook.Resolve(spell);
            float r = spell.Radius * (0.6f + charge * 0.5f);
            Casting().Burst(r, look.Core, hand.transform.up, look.Scale);
            HideCastingCircle();
            return;
        }
        // No spell armed: this is the plain weapon-release burst, not a spell, so it keeps the plain
        // white it had before per-spell looks. Resolving it through SpellLook would hand it Arcane's
        // pink, which reads as "an Arcane spell was cast" — a lie about an identity-less release.
        // (1if changed this line; restoring it. The sibling burst above stays look-derived.)
        float radius = 1.5f;
        Casting().Burst(radius * (0.6f + charge * 0.5f), Color.white, hand.transform.up);
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
    /// Show/refresh the projectile flight-path preview each aim frame.
    ///
    /// <para><b>1iq: the CONE is the BOW's alone.</b> The cone is a spread fan - it claims "your aim
    /// is this wide, and holding narrows it". Only one weapon in the roster earns that claim, so only
    /// one weapon draws it; everything else gets the straight trajectory ray alone. Two independent
    /// reasons, and it is worth keeping them apart because they are not the same statement:
    /// <list type="bullet">
    /// <item><b>Projectile magic has no spread at all</b> - nothing in the spell path ever offsets the
    /// fire direction (no <c>insideUnitSphere</c> anywhere under <c>Spell*.cs</c>), so a cone there was
    /// drawing a fan of outcomes the game does not have. The straight ray is the truthful readout and it
    /// is kept: aim feedback is not the thing being removed.</item>
    /// <item><b>The throwing hammer is not drawn</b> - it is thrown, so it gets the ray too.</item>
    /// </list></para>
    ///
    /// <para><b>1ir adds the ONE legitimate second cone, and it does not contradict 1iq.</b> 1iq's
    /// reasoning was "projectile magic has no spread" - true of every projectile spell, and still true.
    /// Flamethrower is not a projectile: it is a Beam whose damage genuinely sweeps an area, so a cone
    /// there is not an overstatement, it is the hit area itself. The distinction 1iq drew is between a
    /// weapon that is <i>ranged</i> and a flight that is actually <i>spread</i>; a beam cone is the
    /// second kind. Its branch below keys on <c>SpellBeam.ConeFullAngleDegrees &gt; 0</c> rather than on
    /// "is a beam", so a LINE beam (Searing Ray) still gets the plain ray and cannot regress into 1iq's
    /// overstating fan.</para>
    ///
    /// <para><b>Spread comes from the drawn flight, not from the weapon being ranged.</b> The gate is
    /// <c>AmmoItemId != null</c> (arrows today, i.e. exactly the longbow) rather than a hardcoded
    /// <c>"longbow"</c> string, so a future crossbow inherits the cone without a second edit. The
    /// coupling to state: that gate means "consumes ammo", so a future ammunition firearm would inherit
    /// a <i>charge-narrowing</i> cone it has no mechanic for. If one is ever added, this wants a real
    /// <c>IsDrawnProjectile</c> flag on <see cref="WeaponData"/> instead.</para>
    ///
    /// <para><b>Known defect, deliberately NOT fixed here.</b> The cone narrows with charge
    /// (<c>spread * (1f - c)</c>) but <see cref="RangedWeaponBehavior.BeginAttack"/> applies the same
    /// Dexterity spread at every charge level - <c>charge</c> is not in that expression. Worse,
    /// <c>AccuracyFromDex</c> is 0 on both ranged weapons (nothing in <c>Make()</c> sets it), so the
    /// cone both starts at maximum spread and narrows to zero while the shot never tightens. Scoping
    /// the visual is what was asked for; making the claim true is a gameplay change and is left as a
    /// reported finding rather than done silently.</para>
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

            // reach is SpellEffect's flight envelope and is NOT charge-scaled here: this preview
            // mirrors what the spell actually flies, and re-deriving that ladder here would be a
            // second spelling of it (rule 8). Only the cone changed in 1iq.
            float reach = Mathf.Max(armedSpell.ProjectileSpeed, 1f) * 4f; // SpellEffect flight envelope
            PathPreview().Show(pos + fwd * 0.5f, fwd, reach,
                0f, SpellLook.Resolve(armedSpell).Core, transform);
            return;
        }

        // 1ir: a cone BEAM is the one magic delivery whose spread is real. A beam has no flight
        // envelope (SpellCaster.ResolveBeam passes deliveryRange as the beam's length, not a
        // speed*time), so it gets its own branch rather than borrowing the projectile one above.
        // The angle comes from SpellBeam.ConeFullAngleDegrees — the same helper the runtime uses —
        // so the readout cannot drift from the hit area, and reach comes from the caster's own
        // charge ladder rather than a fourth copy of it.
        if (armedSpell != null && armedSpell.Delivery == SpellDelivery.Beam
            && SpellBeam.ConeFullAngleDegrees(armedSpell) > 0f)
        {
            var beamCombat = CombatCached;
            var beamHand = MagicHand(beamCombat);
            var beamCam = MainCam;
            var beamCaster = SpellCasterRef;
            // beamCaster is in this guard because the reach below READS its charge ladder. With no
            // caster there is no ladder to read, and the alternatives are both worse: inventing a
            // fallback constant here would be a second spelling of ChargeSizeBonus (rule 8), which is
            // exactly the defect TryAoeTarget above already papers over with its own 0.8f fallback.
            // Hiding the preview is the honest answer — it reports nothing rather than a wrong reach.
            if (beamHand == null || beamCam == null || beamCaster == null) { HidePathPreview(); return; }

            Vector3 bpos = beamHand.transform.position;
            Vector3 bfwd = beamCam.transform.position + beamCam.transform.forward * Mathf.Max(armedSpell.Range, 5f) - bpos;
            if (bfwd.sqrMagnitude < 0.0001f) bfwd = beamHand.transform.forward; else bfwd = bfwd.normalized;

            // The beam's length is Range * sizeScale (SpellCaster.Channels.cs passes sizeScale as
            // lengthMult), and sizeScale is the caster's own SizeScale ladder — called, not restated.
            // This preview omits the weapon-stat RadiusMult that the caster also folds in, because it
            // has no stats context here — the same honest under-report the flight branch documents.
            float beamReach = armedSpell.Range * beamCaster.SizeScale(charge);
            PathPreview().Show(bpos + bfwd * 0.5f, bfwd, beamReach,
                SpellBeam.ConeFullAngleDegrees(armedSpell), SpellLook.Resolve(armedSpell).Core, transform);
            return;
        }

        var rangedCombat = CombatCached;
        var hand2 = rangedCombat != null ? (rangedCombat.RightHand ?? rangedCombat.LeftHand) : null;
        var ranged = hand2 != null ? hand2.GetComponent<RangedWeaponBehavior>() : null;
        if (ranged == null) { HidePathPreview(); return; }
        ShowRangedPathPreview(ranged, hand2, charge);
    }

    /// <summary>
    /// Bounded ranged-weapon preview (regular aim or dual per-hand draw). 1iq: the spread fan is the
    /// drawn flight's alone (see <see cref="UpdatePathPreview"/> for why the gate is ammo, and for the
    /// charge/spread defect this visual currently overstates); every other ranged weapon draws the
    /// straight ray, and the drawn one's cone still narrows with the draw.
    /// </summary>
    private void ShowRangedPathPreview(RangedWeaponBehavior ranged, GameObject hand, float charge)
    {
        if (ranged == null) { HidePathPreview(); return; }

        float c = Mathf.Clamp01(charge);
        float speed = ranged.ProjectileSpeed * Mathf.Lerp(1f, 1.5f, c);
        float lifetime = ranged.BaseLifetime * Mathf.Lerp(1f, 2f, c);
        Vector3 origin = ranged.Muzzle != null ? ranged.Muzzle.position
            : hand != null ? hand.transform.position : transform.position;

        // 1iq: cone only for a drawn projectile (consumes ammo). 0f collapses every ring to zero
        // width in ProjectilePathPreview.Apply and leaves the trajectory ray at full opacity, so the
        // non-drawn weapons keep their aim readout without gaining a fan they have no spread for.
        float spread = 0f;
        if (ranged.Data != null && !string.IsNullOrEmpty(ranged.Data.AmmoItemId)
            && ranged.Stats != null)
        {
            float accuracy = 1f + ranged.Stats.GetStat(WeaponScalingStat.Dexterity) * ranged.Data.AccuracyFromDex;
            spread = Mathf.Atan2(0.15f / Mathf.Max(accuracy, 0.01f), 1f) * Mathf.Rad2Deg * (1f - c);
        }

        PathPreview().Show(origin, transform.forward, speed * lifetime,
            spread, DamageNumber.ColorFor(ranged.ShotType), transform);
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
        color = SpellLook.Resolve(spell).Core;
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
