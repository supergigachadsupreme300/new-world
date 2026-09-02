using UnityEngine;

/// <summary>
/// Core combat state machine for the player (and potentially enemies via
/// an override mode). Drives attacks, dodges, blocks, and parries based
/// on input events and feeds StaminaSystem.
///
/// Attach to any GameObject alongside StaminaSystem and HitboxSystem to
/// enable a full Elden Ring-style action combat loop.
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

    [Header("Weapon")]
    public HitboxSystem ActiveHitbox;
    public float LightDamage = 10f;
    public float HeavyDamage = 22f;

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

    // ── Public event hooks ──────────────────────────────────────────────────
    public event System.Action<CombatState> OnStateChanged;

    private void Awake()
    {
        _stamina = GetComponent<StaminaSystem>();
        if (ActiveHitbox == null)
            ActiveHitbox = GetComponentInChildren<HitboxSystem>();
    }

    // ── Input API ───────────────────────────────────────────────────────────

    /// <summary>Trigger a light attack (tap attack button).</summary>
    public void LightAttack()
    {
        if (!CanAct) return;
        if (!_stamina.TrySpend(LightAttackCost)) return;

        CurrentState = CombatState.LightAttack;
        _actionTimer = LightAttackDuration;
        _bufferTimer = PostActionBuffer;
        OnStateChanged?.Invoke(CurrentState);

        if (ActiveHitbox != null)
            ActiveHitbox.AttackPower = LightDamage + _comboCount * 3f; // combo scaling
        ActiveHitbox?.BeginSwing(transform, LightDamage);
    }

    /// <summary>Trigger a heavy attack (hold attack button).</summary>
    public void HeavyAttack()
    {
        if (!CanAct) return;
        if (!_stamina.TrySpend(HeavyAttackCost)) return;

        CurrentState = CombatState.HeavyAttack;
        _actionTimer = HeavyAttackDuration;
        _bufferTimer = PostActionBuffer;
        _comboCount = 0; // heavy resets combo
        OnStateChanged?.Invoke(CurrentState);

        ActiveHitbox?.BeginSwing(transform, HeavyDamage);
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

        // i-frames are signalled to the damage listener; the CharacterController
        // or enemy AI checks IsInvulnerable before dealing damage.
    }

    /// <summary>Attempt a parry. Returns true if the parry window is open.</summary>
    public bool TryParry()
    {
        if (CurrentState != CombatState.Idle)
            return false;

        _parryWindowOpen = true;
        _actionTimer = ParryWindowDuration;
        // Briefly set state to PostAction so no other actions overlap.
        CurrentState = CombatState.PostAction;
        OnStateChanged?.Invoke(CurrentState);
        return true;
    }

    /// <summary>Toggle blocking on/off (called by shield button hold/release).</summary>
    public void SetBlocking(bool blocking)
    {
        if (CurrentState != CombatState.Idle && !blocking)
        {
            // Not allowed to start blocking while mid-action; allowed to release.
        }
        IsBlocking = blocking;
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
    }
}