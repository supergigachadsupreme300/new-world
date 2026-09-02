using UnityEngine;

/// <summary>
/// Manages stamina for a combat entity (player or enemy).
///
/// Stamina is the core resource governing action availability: every combat
/// action (attack, dodge, block) costs stamina. It regenerates over time but
/// pauses briefly after any drain event ("post-action window").
///
/// Attach to any GameObject that needs stamina. The existing PlayerController
/// has its own stamina fields; this system can replace or coexist with them
/// via the public API.
/// </summary>
public class StaminaSystem : MonoBehaviour
{
    [Header("Capacity")]
    public float MaxStamina = 100f;

    [Header("Regeneration")]
    public float RegenRate = 15f;
    public float RegenDelay = 0.4f;
    public float RegenMultiplier = 1f;

    [Header("Status")]
    public float Stamina;
    public bool Empty => Stamina <= 0f;

    private float _timer;

    /// <summary>Fires whenever stamina is consumed.</summary>
    public event System.Action<float> OnDrained;

    /// <summary>Fires when stamina hits zero.</summary>
    public event System.Action OnDepleted;

    private void Awake()
    {
        Stamina = MaxStamina;
    }

    /// <summary>Attempt to spend stamina. Returns true if the full amount was paid.</summary>
    public bool TrySpend(float cost)
    {
        if (Stamina < cost)
            return false;
        Stamina -= cost;
        _timer = RegenDelay;
        OnDrained?.Invoke(cost);
        if (Stamina <= 0f)
        {
            Stamina = 0f;
            OnDepleted?.Invoke();
        }
        return true;
    }

    /// <summary>Forcefully drain stamina (e.g. guard damage leak).</summary>
    public void Drain(float amount)
    {
        Stamina = Mathf.Max(0f, Stamina - amount);
        _timer = RegenDelay;
        if (Stamina <= 0f)
            OnDepleted?.Invoke();
    }

    /// <summary>Refill stamina instantly (e.g. rest at bonfire).</summary>
    public void RestoreAll()
    {
        Stamina = MaxStamina;
    }

    private void Update()
    {
        if (Stamina >= MaxStamina)
            return;

        _timer -= Time.deltaTime;
        if (_timer > 0f)
            return;

        // Regen scaled by time to remain framerate-independent.
        Stamina = Mathf.Min(Stamina + RegenRate * RegenMultiplier * Time.deltaTime, MaxStamina);
    }
}