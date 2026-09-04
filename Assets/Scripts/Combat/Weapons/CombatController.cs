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

    [Header("Hands / Wielding (§5.4)")]
    [Tooltip("Right-hand weapon GameObject carrying a WeaponData + IWeaponBehavior.")]
    public GameObject RightHand;
    [Tooltip("Left-hand weapon GameObject carrying a WeaponData + IWeaponBehavior.")]
    public GameObject LeftHand;
    [Tooltip("Current wielding state. Two-hand = both slots for one weapon; Dual = one per hand.")]
    public WieldingState Wielding = WieldingState.Single;

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
    private const int MaxCombo = 3;

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
        /// <summary>One hand (off-hand free).</summary>
        Single = 0,

        /// <summary>One weapon per hand (needs ~2× Str).</summary>
        Dual = 1,

        /// <summary>Both hand slots for one weapon (needs ~half Str).</summary>
        TwoHand = 2
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
        var data = hand != null ? hand.GetComponent<WeaponData>() : null;
        return data != null ? data.Category : WeaponCategory.Melee;
    }

    // ── Input API ───────────────────────────────────────────────────────────

    /// <summary>Trigger a light attack (tap attack button).</summary>
    public void LightAttack()
    {
        if (!CanAct) return;

        // No weapon equipped — never consume stamina or lock an attack state.
        IWeaponBehavior behavior = ActiveBehavior;
        if (behavior == null) return;

        if (!_stamina.TrySpend(LightAttackCost)) return;

        CurrentState = CombatState.LightAttack;
        _actionTimer = LightAttackDuration;
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

    /// <summary>Trigger a heavy attack (hold attack button).</summary>
    public void HeavyAttack()
    {
        if (!CanAct) return;

        IWeaponBehavior behavior = ActiveBehavior;
        if (behavior == null) return;

        if (!_stamina.TrySpend(HeavyAttackCost)) return;

        CurrentState = CombatState.HeavyAttack;
        _actionTimer = HeavyAttackDuration;
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
        if (!_stamina.TrySpend(DodgeCost)) return;

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

        _parryWindowOpen = true;
        _actionTimer = ParryWindowDuration;
        CurrentState = CombatState.PostAction;
        OnStateChanged?.Invoke(CurrentState);
        return true;
    }

    /// <summary>Toggle blocking on/off (called by shield button hold/release).</summary>
    public void SetBlocking(bool blocking)
    {
        IsBlocking = blocking && CurrentState == CombatState.Idle;
    }

    /// <summary>Receive stamina drain from an incoming blocked hit.</summary>
    public void OnBlockedHit(float incomingDamage)
    {
        if (!IsBlocking) return;
        _stamina.Drain(BlockDrainPerHit + incomingDamage * 0.2f);
    }

    /// <summary>Check if a parry is currently active (for enemy knockbacks).</summary>
    public bool IsParryWindowOpen => _parryWindowOpen;

    public void ResetCombo() => _comboCount = 0;

    // ── Frame update ────────────────────────────────────────────────────────

    private void Update()
    {
        _actionTimer -= Time.deltaTime;
        _bufferTimer -= Time.deltaTime;

        switch (CurrentState)
        {
            case CombatState.LightAttack:
            case CombatState.HeavyAttack:
            case CombatState.Dodge:
                if (_actionTimer <= 0f)
                {
                    CurrentState = CombatState.Idle;
                    _comboCount++;
                    if (_comboCount > MaxCombo) _comboCount = 0;
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
    }

    private void OnDisable()
    {
        CurrentState = CombatState.Idle;
        _actionTimer = 0f;
        _bufferTimer = 0f;
    }
}
