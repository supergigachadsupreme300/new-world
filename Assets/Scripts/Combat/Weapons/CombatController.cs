using UnityEngine;

/// <summary>
/// Core combat state machine for the player (and potentially enemies via an override
/// mode). Drives attacks, dodges, blocks, and parries based on input events, feeds
/// StaminaSystem, and routes attacks to the equipped weapon's IWeaponBehavior (§3.6).
///
/// Per §3.6, this controller talks ONLY to IWeaponBehavior — it never knows whether a
/// weapon is melee, ranged, or magic.
///
/// Attach to any GameObject alongside StaminaSystem. Assign the hand-slot weapon objects
/// (or WeaponData infra) so attacks resolve through the correct behavior.
/// </summary>
[RequireComponent(typeof(StaminaSystem))]
public class CombatController : MonoBehaviour
{
    [Header("Action Costs")]
    public float LightAttackCost = 10f;
    public float HeavyAttackCost = 25f;
    public float DodgeCost = 20f;
    public float BlockDrainPerHit = 15f;

    [Header("Action Timing")]
    public float LightAttackDuration = 0.25f;
    public float HeavyAttackDuration = 0.45f;
    public float DodgeDuration = 0.35f;
    public float ParryWindowDuration = 0.2f;
    public float PostActionBuffer = 0.15f;

    /// <summary>Defaults used by <see cref="WeaponAnimator"/> for the swing visual timing.</summary>
    public static readonly float DefaultLightAttackDuration = 0.25f;
    public static readonly float DefaultHeavyAttackDuration = 0.45f;

    [Header("Hands / Wielding (§5.4)")]
    [Tooltip("Right-hand weapon GameObject carrying a WeaponData + IWeaponBehavior.")]
    public GameObject RightHand;
    [Tooltip("Left-hand weapon GameObject carrying a WeaponData + IWeaponBehavior.")]
    public GameObject LeftHand;
    [Tooltip("Current wielding state, derived from the hands. Dual = one weapon per hand; TwoHand = a single held weapon gripped with both hands for a buff.")]
    public WieldingState Wielding = WieldingState.Single;

    /// <summary>Player's intent to two-hand a single held weapon (applies when exactly one hand holds a weapon).</summary>
    private bool _twoHandIntent;

    [Header("Defense")]
    public bool IsBlocking;
    public bool ParryActive;

    // ── Public state ────────────────────────────────────────────────────────
    public CombatState CurrentState { get; private set; } = CombatState.Idle;
    public bool CanAct => CurrentState == CombatState.Idle;

    private StaminaSystem _stamina;
    private float _actionTimer;
    private float _bufferTimer;
    private bool _parryWindowOpen;
    private int _comboCount;
    private float _lastAttackEndTime = float.MinValue;
    private const int MaxCombo = 3;

    /// <summary>Per-hand swing progress for the independent dual scheme and combo chains (§5.16).</summary>
    private struct HandSwing
    {
        /// <summary>Absolute time the swing completes (0 when the hand is free).</summary>
        public float EndAt;
        /// <summary>Absolute time the previous swing completed (per-hand combo reset).</summary>
        public float LastEnd;
        /// <summary>Per-hand combo stage (0..MaxCombo), driving the swing variant.</summary>
        public int Combo;
    }

    private HandSwing _swingR;
    private HandSwing _swingL;

    /// <summary>Pause (seconds) without a light attack that resets the combo chain to swing one.</summary>
    public float ComboResetTime = 0.8f;

    public enum CombatState
    {
        Idle,
        LightAttack,
        HeavyAttack,
        Dodge,
        PostAction
    }

    /// <summary>Wielding state governing hand usage (§5.4).</summary>
    public enum WieldingState
    {
        /// <summary>A single held weapon used one-handed.</summary>
        Single = 0,

        /// <summary>One (different) weapon per hand — attacks alternate between hands.</summary>
        Dual = 1,

        /// <summary>A single held weapon gripped with both hands for a buff.</summary>
        TwoHand = 2
    }

    /// <summary>
    /// Recompute the wield state from the current hand contents. Two weapons → <see cref="WieldingState.Dual"/>;
    /// one weapon + <see cref="TwoHandIntent"/> → <see cref="WieldingState.TwoHand"/>; otherwise single.
    /// Call after equipping/unequipping.
    /// </summary>
    public void RecomputeWielding()
    {
        if (RightHand != null && LeftHand != null)
            Wielding = WieldingState.Dual;
        else if ((RightHand != null || LeftHand != null) && _twoHandIntent)
            Wielding = WieldingState.TwoHand;
        else
            Wielding = WieldingState.Single;
    }

    /// <summary>Whether the player intends to two-hand a single held weapon (ignored while two weapons are held).</summary>
    public bool TwoHandIntent => _twoHandIntent;

    /// <summary>Set the two-hand grip intent for a single held weapon and recompute the wield state.</summary>
    public void SetTwoHand(bool intent)
    {
        _twoHandIntent = intent;
        RecomputeWielding();
    }

    // ── Public event hooks ──────────────────────────────────────────────────
    public event System.Action<CombatState> OnStateChanged;
    public event System.Action<IWeaponBehavior> OnAttackStarted;

    private void Awake()
    {
        _stamina = GetComponent<StaminaSystem>();
    }

    // ── Hand/behavior resolution ────────────────────────────────────────────
    private bool _useOffHand; // alternates primary hand when dual-wielding

    /// <summary>The weapon behavior driving attacks, honoring wielding state (§5.4).</summary>
    public IWeaponBehavior ActiveBehavior
    {
        get
        {
            switch (Wielding)
            {
                case WieldingState.TwoHand:
                    return ResolveBehavior(TwoHandWeapon);
                case WieldingState.Dual:
                    // Alternate which of the two hands leads each attack while dual-wielding.
                    GameObject dedicated = _useOffHand ? LeftHand : RightHand;
                    _useOffHand = !_useOffHand;
                    return ResolveBehavior(FirstValid(dedicated, _useOffHand ? RightHand : LeftHand));
                case WieldingState.Single:
                default:
                    return ResolveBehavior(RightHand ?? LeftHand);
            }
        }
    }

    /// <summary>The single weapon used for a two-hand grip (right hand preferred).</summary>
    private GameObject TwoHandWeapon => RightHand != null ? RightHand : LeftHand;

    private GameObject FirstValid(GameObject a, GameObject b)
    {
        if (a != null && a.GetComponent<IWeaponBehavior>() != null) return a;
        return b != null && b.GetComponent<IWeaponBehavior>() != null ? b : a;
    }

    private IWeaponBehavior ResolveBehavior(GameObject hand)
    {
        if (hand == null) return null;
        return WeaponDatabase.ResolveBehavior(hand, CategoryOf(hand));
    }

    private WeaponCategory CategoryOf(GameObject hand)
    {
        // WeaponData is a ScriptableObject (not a Component) — read it from the rig's WeaponRigHost.
        var host = hand != null ? hand.GetComponent<WeaponRigHost>() : null;
        return host != null && host.Data != null ? host.Data.Category : WeaponCategory.Melee;
    }

    /// <summary>Category of the weapon rigged into a specific hand slot.</summary>
    public WeaponCategory CategoryOfHand(GameObject hand) => CategoryOf(hand);

    /// <summary>
    /// True when both hands hold a real (non-fist) weapon — the per-hand dual-wield scheme. Fists
    /// never count, so a bare-fisted (or single-weapon) fighter keeps the standard single-hand controls.
    /// </summary>
    public bool HasLoadedDual
    {
        get
        {
            if (RightHand == null || LeftHand == null) return false;
            return !WeaponRigBuilder.IsFist(RightHand) && !WeaponRigBuilder.IsFist(LeftHand);
        }
    }

    /// <summary>True when both loaded hands hold magic weapons (dual-magic keeps the standard magic flow).</summary>
    public bool BothHandsMagic =>
        HasLoadedDual &&
        CategoryOf(RightHand) == WeaponCategory.Magic &&
        CategoryOf(LeftHand) == WeaponCategory.Magic;

    /// <summary>True when a loaded dual loadout contains a ranged weapon (mouse buttons cross hands).</summary>
    public bool HasRangedDual =>
        HasLoadedDual &&
        (CategoryOf(RightHand) == WeaponCategory.Ranged || CategoryOf(LeftHand) == WeaponCategory.Ranged);

    /// <summary>
    /// True when the per-hand dual scheme (§5.16) is active: both hands hold real weapons but not
    /// both magic. Hand attacks in this mode run on per-hand timers so the two hands swing independently.
    /// </summary>
    private bool PerHandScheme => HasLoadedDual && !BothHandsMagic;

    /// <summary>Category of the weapon the next attack would resolve (defaults to melee when empty).</summary>
    private WeaponCategory ActiveCategory()
    {
        GameObject hand;
        switch (Wielding)
        {
            case WieldingState.TwoHand:
                hand = TwoHandWeapon;
                break;
            case WieldingState.Dual:
                hand = _useOffHand ? LeftHand : RightHand;
                break;
            case WieldingState.Single:
            default:
                hand = RightHand ?? LeftHand;
                break;
        }
        return CategoryOf(hand);
    }

    // ── Input API ───────────────────────────────────────────────────────────

    /// <summary>The hand rig currently holding <paramref name="behavior"/>, or null.</summary>
    private GameObject HandOf(IWeaponBehavior behavior)
    {
        if (RightHand != null && RightHand.GetComponent<IWeaponBehavior>() == behavior) return RightHand;
        if (LeftHand != null && LeftHand.GetComponent<IWeaponBehavior>() == behavior) return LeftHand;
        return null;
    }

    /// <summary>Per-hand swing state for a given hand slot (right preferred).</summary>
    private ref HandSwing SwingOf(GameObject hand)
    {
        if (hand == RightHand) return ref _swingR;
        return ref _swingL;
    }

    /// <summary>Trigger a light attack (tap attack button) on the live hand's weapon.</summary>
    public void LightAttack() => LightAttackWith(HandOf(ActiveBehavior));

    /// <summary>
    /// Trigger a light attack specifically from the weapon rigged in <paramref name="hand"/>
    /// (per-hand dual-wield scheme, §5.16). The acting hand drives the swing animation only.
    /// </summary>
    public void LightAttackWith(GameObject hand)
    {
        if (IsBlocking) return;

        // No weapon in that hand — never consume stamina or lock an attack state.
        var behavior = ResolveBehavior(hand);
        if (behavior == null) return;

        // Per-hand dual (§5.16): each hand swings on its OWN timer, so the two hands never share a
        // wait. A global action (roll/heavy/parry) still gates every hand.
        if (PerHandScheme)
        {
            ref var swing = ref SwingOf(hand);
            if (Time.time < swing.EndAt) return;
            if (CurrentState != CombatState.Idle) return;

            if (!_stamina.TrySpend(LightAttackCost)) return;

            // Per-hand combo chain: same pause-to-reset rule as the shared chain.
            if (Time.time - swing.LastEnd > ComboResetTime) swing.Combo = 0;

            float swingAnim = NotifyWeaponAnimator(hand, false, swing.Combo);
            float swingSpeed = AttackSpeedScale();
            swing.EndAt = Time.time + Mathf.Max(LightAttackDuration / swingSpeed, swingAnim);

            var swingCmd = new AttackCommand
            {
                IsHeavy = false,
                Direction = transform.forward,
                Origin = transform
            };
            behavior.BeginAttack(swingCmd);
            OnAttackStarted?.Invoke(behavior);
            return;
        }

        if (!CanAct) return;

        if (!_stamina.TrySpend(LightAttackCost)) return;

        // Combo chain: a pause longer than ComboResetTime restarts at swing one. Measured from the
        // END of the previous swing (not its start) so long swing locks don't kill the chain.
        if (Time.time - _lastAttackEndTime > ComboResetTime) _comboCount = 0;

        CurrentState = CombatState.LightAttack;
        float anim = NotifyWeaponAnimator(hand, false, _comboCount);
        float speed = AttackSpeedScale();
        _actionTimer = Mathf.Max(LightAttackDuration / speed, anim);
        _bufferTimer = PostActionBuffer;
        OnStateChanged?.Invoke(CurrentState);

        var cmd = new AttackCommand
        {
            IsHeavy = false,
            Direction = transform.forward,
            Origin = transform
        };
        behavior.BeginAttack(cmd);
        OnAttackStarted?.Invoke(behavior);
    }

    /// <summary>Trigger a heavy attack (hold attack button). Melee weapons have no heavy attack — their
    /// RMB is the block — so only ranged/magic weapons can perform one.</summary>
    public void HeavyAttack()
    {
        if (!CanAct) return;
        if (IsBlocking) return;

        IWeaponBehavior behavior = ActiveBehavior;
        if (behavior == null) return;

        if (ActiveCategory() == WeaponCategory.Melee) return;

        if (!_stamina.TrySpend(HeavyAttackCost)) return;

        CurrentState = CombatState.HeavyAttack;
        float anim = NotifyWeaponAnimators(true, 3); // heavy always plays the finisher swing
        float speed = AttackSpeedScale();
        _actionTimer = Mathf.Max(HeavyAttackDuration / speed, anim);
        _bufferTimer = PostActionBuffer;
        _comboCount = 0; // heavy resets combo
        OnStateChanged?.Invoke(CurrentState);

        var cmd = new AttackCommand
        {
            IsHeavy = true,
            Direction = transform.forward,
            Origin = transform
        };
        behavior.BeginAttack(cmd);
        OnAttackStarted?.Invoke(behavior);
    }

    /// <summary>Trigger a dodge roll.</summary>
    public void Dodge()
    {
        if (!CanAct) return;
        if (IsBlocking) return;
        if (!_stamina.TrySpend(DodgeCost)) return;

        // Cancel any in-flight per-hand swing so the roll reads cleanly (§5.16).
        _swingR.EndAt = 0f;
        _swingL.EndAt = 0f;

        CurrentState = CombatState.Dodge;
        _actionTimer = DodgeDuration;
        _bufferTimer = PostActionBuffer;
        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>Attempt a parry. Returns true if the parry window is open.</summary>
    public bool TryParry()
    {
        if (CurrentState != CombatState.Idle)
            return false;

        // Window width comes from PlayerStats (BaseParrySeconds + Dex, × class ParryWindowMul).
        var stats = GetComponent<PlayerStats>();
        float window = stats != null ? stats.ParryWindow : ParryWindowDuration;
        if (window <= 0f) window = ParryWindowDuration;

        _parryWindowOpen = true;
        _actionTimer = window;
        CurrentState = CombatState.PostAction;
        OnStateChanged?.Invoke(CurrentState);
        return true;
    }

    /// <summary>Toggle blocking on/off (called by shield button hold/release). Drives every equipped
    /// rig's defense guard animation only on the state edge, so spam/ressert churn never hits the
    /// animators frame-to-frame.</summary>
    public void SetBlocking(bool blocking)
    {
        bool want = blocking && CurrentState == CombatState.Idle;
        if (want && !IsBlocking)
        {
            IsBlocking = true;
            PlayGuard();
        }
        else if (!want && IsBlocking)
        {
            IsBlocking = false;
            EndGuard();
        }
    }

    /// <summary>Blocking only stays up while the loadout is drawn and idle — a draw/stow transition
    /// or a sheathed weapon drops the guard so the guard pose never fights the stow idle.</summary>
    public bool CanKeepBlocking()
    {
        if (CurrentState != CombatState.Idle) return false;
        var stow = GetComponent<WeaponStowAnimator>();
        return stow == null || (stow.IsDrawn && !stow.IsBusy);
    }

    /// <summary>Raise every equipped rig's defense guard pose (the "defense" animation set).</summary>
    public void PlayGuard()
    {
        foreach (var a in AllAnimators)
            a.PlayGuard();
    }

    /// <summary>Drop every equipped rig's defense guard pose.</summary>
    public void EndGuard()
    {
        foreach (var a in AllAnimators)
            a.EndGuard();
    }

    /// <summary>Rig holding a shield weapon on either hand, or null.</summary>
    public WeaponRigHost EquippedShield
    {
        get
        {
            var right = RightHand != null ? RightHand.GetComponent<WeaponRigHost>() : null;
            if (right != null && right.Data != null && right.Data.Category == WeaponCategory.Shield)
                return right;
            var left = LeftHand != null ? LeftHand.GetComponent<WeaponRigHost>() : null;
            if (left != null && left.Data != null && left.Data.Category == WeaponCategory.Shield)
                return left;
            return null;
        }
    }

    /// <summary>True when either hand holds a shield weapon (enables the strict shield guard).</summary>
    public bool HasShield => EquippedShield != null;

    /// <summary>
    /// Fraction of a blocked hit that still lands: 1 − the equipped shield's BlockAbsorbPercent,
    /// falling back to 0.2 (the bare-hand guard absorbs 80%) when no shield is held.
    /// </summary>
    public float BlockTakenMultiplier
    {
        get
        {
            var shield = EquippedShield;
            return shield != null && shield.Data != null
                ? 1f - shield.Data.BlockAbsorbPercent
                : 0.2f;
        }
    }

    /// <summary>Stamina-drain multiplier applied per absorbed hit: the equipped shield's
    /// BlockStaminaDrainMult (1 = bare-hand guard).</summary>
    public float BlockDrainMultiplier
    {
        get
        {
            var shield = EquippedShield;
            return shield != null && shield.Data != null
                ? Mathf.Clamp(shield.Data.BlockStaminaDrainMult, 0.1f, 5f)
                : 1f;
        }
    }

    /// <summary>Receive stamina drain from an incoming blocked hit. True while the block holds;
    /// when stamina can't cover the cost the guard breaks (block released) and false is returned.
    /// Class BlockingMul (§3.2.1) and the tree block-efficiency perk (§3.3) reduce the drain
    /// (stronger guard, less stamina eaten per hit); an equipped shield's BlockStaminaDrainMult
    /// multiplies it again (cheaper guard).</summary>
    public bool OnBlockedHit(float incomingDamage)
    {
        if (!IsBlocking) return false;
        var passives = GetComponent<ClassPassiveManager>();
        float blocking = passives != null ? Mathf.Max(passives.BlockingMul, 0.1f) : 1f;
        var stats = GetComponent<PlayerStats>();
        if (stats != null) blocking *= Mathf.Max(stats.TreeBlockEfficiencyMul, 0.1f);
        float drain = (BlockDrainPerHit + incomingDamage * 0.2f) / blocking * BlockDrainMultiplier;
        if (_stamina == null || !_stamina.TrySpend(drain))
        {
            SetBlocking(false);
            return false;
        }
        return true;
    }

    /// <summary>Check if a parry is currently active (for enemy knockbacks).</summary>
    public bool IsParryWindowOpen => _parryWindowOpen;

    public void ResetCombo()
    {
        _comboCount = 0;
        _swingR.Combo = 0;
        _swingL.Combo = 0;
    }

    /// <summary>Attack-speed scale applied to both the swing visuals and the action lock.</summary>
    private float AttackSpeedScale()
    {
        var stats = GetComponent<PlayerStats>();
        return stats != null ? stats.AttackSpeedScale : 1f;
    }

    /// <summary>Drive the per-weapon swing visuals on ANY equipped rigs (broadcast — used by the
    /// single/magic flows). Returns the longest attack duration reported so the lock stays in sync.</summary>
    private float NotifyWeaponAnimators(bool heavy, int variant)
    {
        float duration = 0f;
        if (RightHand != null)
            foreach (var a in RightHand.GetComponentsInChildren<WeaponAnimator>(true))
                duration = Mathf.Max(duration, a.PlayAttack(heavy, variant));
        if (LeftHand != null)
            foreach (var a in LeftHand.GetComponentsInChildren<WeaponAnimator>(true))
                duration = Mathf.Max(duration, a.PlayAttack(heavy, variant));
        return duration;
    }

    /// <summary>Drive the swing visuals on a SINGLE hand's rig only (per-hand dual attacks).</summary>
    private float NotifyWeaponAnimator(GameObject hand, bool heavy, int variant)
    {
        float duration = 0f;
        if (hand == null) return 0f;
        foreach (var a in hand.GetComponentsInChildren<WeaponAnimator>(true))
            duration = Mathf.Max(duration, a.PlayAttack(heavy, variant));
        return duration;
    }

    /// <summary>Ask every equipped rig to enter its charge-hold pose (aim/draw).</summary>
    public void PlayCharge()
    {
        foreach (var a in AllAnimators)
            a.PlayCharge();
    }

    /// <summary>Push the live charge level (0..1) to every equipped rig's charge-hold accent.</summary>
    public void SetChargeLevel(float level)
    {
        foreach (var a in AllAnimators)
            a.SetChargeLevel(level);
    }

    /// <summary>End the charge-hold on every equipped rig — fire resumes the release tail, else settles.</summary>
    public void EndCharge(bool fire)
    {
        foreach (var a in AllAnimators)
            a.EndCharge(fire);
    }

    private System.Collections.Generic.IEnumerable<WeaponAnimator> AllAnimators
    {
        get
        {
            if (RightHand != null)
                foreach (var a in RightHand.GetComponentsInChildren<WeaponAnimator>(true))
                    yield return a;
            if (LeftHand != null)
                foreach (var a in LeftHand.GetComponentsInChildren<WeaponAnimator>(true))
                    yield return a;
        }
    }

    /// <summary>Fire the equipped ranged weapon at a released charge/draw level (0..1): the shot's
    /// damage, projectile speed and flight distance scale with the draw. Not usable while blocking.</summary>
    public void FireRanged(float charge) => FireRangedWith(HandOf(ActiveBehavior), charge);

    /// <summary>
    /// Fire a specific hand's ranged weapon at a released charge/draw level (0..1) — the per-hand
    /// dual-wield variant of <see cref="FireRanged"/>. Launches from that hand's muzzle.
    /// </summary>
    public void FireRangedWith(GameObject hand, float charge)
    {
        if (!CanAct) return;
        if (IsBlocking) return;

        var behavior = ResolveBehavior(hand);
        if (behavior == null) return;

        charge = Mathf.Clamp01(charge);
        float cost = Mathf.Lerp(LightAttackCost, HeavyAttackCost, charge);
        if (!_stamina.TrySpend(cost)) return;

        var cmd = new AttackCommand
        {
            IsHeavy = charge >= 0.5f,
            ChargeLevel = charge,
            Direction = transform.forward,
            Origin = transform
        };
        behavior.BeginAttack(cmd);
        OnAttackStarted?.Invoke(behavior);
    }

    // ── Frame update ────────────────────────────────────────────────────────

    private void Update()
    {
        _actionTimer -= Time.deltaTime;
        _bufferTimer -= Time.deltaTime;

        TickHand(ref _swingR);
        TickHand(ref _swingL);

        switch (CurrentState)
        {
            case CombatState.LightAttack:
                if (_actionTimer <= 0f)
                {
                    CurrentState = CombatState.Idle;
                    _lastAttackEndTime = Time.time;
                    _comboCount++;
                    if (_comboCount > MaxCombo) _comboCount = 0;
                    OnStateChanged?.Invoke(CurrentState);
                }
                break;

            case CombatState.HeavyAttack:
            case CombatState.Dodge:
                if (_actionTimer <= 0f)
                {
                    CurrentState = CombatState.Idle;
                    OnStateChanged?.Invoke(CurrentState);
                }
                break;

            case CombatState.PostAction:
                if (_actionTimer <= 0f)
                {
                    _parryWindowOpen = false;
                    CurrentState = CombatState.Idle;
                    OnStateChanged?.Invoke(CurrentState);
                }
                break;

            case CombatState.Idle:
                break;
        }

        // A sheathed loadout or in-flight stow transition can't hold a block — drop the guard so
        // the guard animation never persists onto the stow idle.
        if (IsBlocking && !CanKeepBlocking())
            SetBlocking(false);
    }

    /// <summary>Advance one hand's independent swing: on completion, free the hand and bump its combo chain.</summary>
    private static void TickHand(ref HandSwing swing)
    {
        if (swing.EndAt > 0f && Time.time >= swing.EndAt)
        {
            swing.LastEnd = swing.EndAt;
            swing.EndAt = 0f;
            swing.Combo = swing.Combo + 1 > MaxCombo ? 0 : swing.Combo + 1;
        }
    }

    private void OnDisable()
    {
        CurrentState = CombatState.Idle;
        _actionTimer = 0f;
        _bufferTimer = 0f;
        _swingR = default;
        _swingL = default;
    }
}
