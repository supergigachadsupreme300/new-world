using UnityEngine;

/// <summary>
/// Dispatches the active race's passive kit (game-design §3.5, planning Task 4.3). Reads
/// PlayerStats.Race.PassiveId and exposes the modifiers combat/utility code queries each
/// frame. Frame-independent passives (regen, auras, resistances) are resolved here; hit
/// hooks (venom/blood/lifesteal) feed into the combat pipeline via <see cref="OnMeleeHit"/>.
/// </summary>
[DisallowMultipleComponent]
public class RacePassiveManager : MonoBehaviour
{
    private PlayerStats _stats;
    private string _lastPassiveId = string.Empty;
    private float _cachedMoveMult = 1f;
    private float _cachedDamageReduction = 0f;
    private bool _cachedInfiniteStamina;
    private float _cachedHpRegenPerSecond;
    private float _cachedLifesteal;

    /// <summary>Fires when the player lands a melee hit, for hit-proc passives.</summary>
    public event System.Action OnMeleeHit;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (_stats == null || _stats.Race == null)
        {
            if (!string.IsNullOrEmpty(_lastPassiveId)) Reset();
            return;
        }

        string pid = _stats.Race.PassiveId ?? string.Empty;
        if (pid == _lastPassiveId) return;
        _lastPassiveId = pid;
        Refresh(pid);
    }

    private void Reset()
    {
        _lastPassiveId = string.Empty;
        _cachedMoveMult = 1f;
        _cachedDamageReduction = 0f;
        _cachedInfiniteStamina = false;
        _cachedHpRegenPerSecond = 0f;
        _cachedLifesteal = 0f;
    }

    private void Refresh(string pid)
    {
        _cachedMoveMult = 1f;
        _cachedDamageReduction = 0f;
        _cachedInfiniteStamina = false;
        _cachedHpRegenPerSecond = 0f;
        _cachedLifesteal = 0f;

        switch (pid)
        {
            case "stone_skin":
                _cachedMoveMult = 0.8f;            // -20% move speed (Golem)
                _cachedDamageReduction = 0.25f;     // 25% physical + magic reduction
                break;
            case "undead":
                _cachedInfiniteStamina = true;
                break;
            case "skeleton":
                _cachedInfiniteStamina = true;
                _cachedMoveMult = 1.2f;            // +20% move speed
                break;
            case "orc":
                _cachedHpRegenPerSecond = 0.01f;   // 1% max HP/s
                break;
            case "werewolf":
                _cachedHpRegenPerSecond = 0.02f;   // only at night, approximated
                break;
            case "vampire":
                _cachedLifesteal = 0.05f;          // 5% on hit
                break;
            default:
                break; // passive handled by combat/aura hooks or purely narrative
        }
    }

    /// <summary>Multiplier applied to the player's movement speed (Golem −20%, Skeleton +20%).</summary>
    public float MoveSpeedMultiplier => _cachedMoveMult;

    /// <summary>Extra flat damage reduction % from the race (Stone Skin 25%).</summary>
    public float DamageReductionBonus => _cachedDamageReduction;

    /// <summary>Stamina never depletes (Undead, Skeleton).</summary>
    public bool InfiniteStamina => _cachedInfiniteStamina;

    /// <summary>Passive HP regen as a fraction of max HP per second.</summary>
    public float HpRegenPerSecond => _cachedHpRegenPerSecond;

    /// <summary>Lifesteal fraction of melee damage restored to HP (Vampire 5%).</summary>
    public float Lifesteal => _cachedLifesteal;

    /// <summary>Notify hit-proc passives (venom/bleed/lifesteal) that a melee hit landed.</summary>
    public void NotifyMeleeHit()
    {
        OnMeleeHit?.Invoke();
    }
}