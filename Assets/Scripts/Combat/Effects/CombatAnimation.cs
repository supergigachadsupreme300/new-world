using UnityEngine;

/// <summary>
/// Bridges CombatController state to an Animator (Task 3.3).
///
/// Listens to OnStateChanged and sets the matching Animator trigger for the current
/// combat state (attack, dodge, etc.), plus an idle bool when no action is active.
/// Attach to a GameObject with an Animator and an assignable CombatController.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CombatAnimation : MonoBehaviour
{
    [Header("Wiring")]
    public CombatController Combat;
    public Animator Anim;

    [Header("Animator Parameters")]
    public string IdleParam = "Idle";
    public string LightAttackParam = "LightAttack";
    public string HeavyAttackParam = "HeavyAttack";
    public string DodgeParam = "Dodge";
    public string BlockParam = "Blocking";

    private void Awake()
    {
        if (Anim == null)
            Anim = GetComponent<Animator>();
        if (Combat == null)
            Combat = GetComponent<CombatController>();
    }

    private void OnEnable()
    {
        if (Combat != null)
            Combat.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (Combat != null)
            Combat.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(CombatController.CombatState state)
    {
        if (Anim == null) return;

        ResetTriggers();

        switch (state)
        {
            case CombatController.CombatState.LightAttack:
                SetTrigger(LightAttackParam);
                Anim.SetBool(IdleParam, false);
                break;
            case CombatController.CombatState.HeavyAttack:
                SetTrigger(HeavyAttackParam);
                Anim.SetBool(IdleParam, false);
                break;
            case CombatController.CombatState.Dodge:
                SetTrigger(DodgeParam);
                Anim.SetBool(IdleParam, false);
                break;
            default:
                Anim.SetBool(IdleParam, true);
                break;
        }
    }

    private void Update()
    {
        if (Anim == null || Combat == null) return;
        Anim.SetBool(BlockParam, Combat.IsBlocking);
    }

    private void SetTrigger(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        Anim.SetTrigger(name);
    }

    private void ResetTriggers()
    {
        if (!string.IsNullOrEmpty(LightAttackParam)) Anim.ResetTrigger(LightAttackParam);
        if (!string.IsNullOrEmpty(HeavyAttackParam)) Anim.ResetTrigger(HeavyAttackParam);
        if (!string.IsNullOrEmpty(DodgeParam)) Anim.ResetTrigger(DodgeParam);
    }
}
