using UnityEngine;

/// <summary>
/// World altar / ritual site that unlocks a race and allows a mid-play transform
/// (game-design §3.5, planning Task 4.3). Interacting unlocks the race permanently
/// (RaceUnlockManager) and, if the player has a Ritual Stone (or the race is Human/free),
/// switches the active race immediately — refreshing PlayerStats modifiers, the RaceRig,
/// and the passive kit.
/// </summary>
public class RaceDiscoveryPoint : MonoBehaviour
{
    [Tooltip("Race this altar reveals. Null/empty on the divine altar sessions handled in-editor.")]
    public RaceData Race;

    [Tooltip("Interaction radius.")]
    public float Radius = 2f;

    [Header("Transform Cost")]
    [Tooltip("Consumed on mid-play race change (Human is always free).")]
    public bool RequiresRitualStone = true;

    public bool Interactable = true;

    [Tooltip("Layer mask for the interacting player.")]
    public LayerMask PlayerMask = ~0;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }

    /// <summary>Try to reveal + transform. Returns true if the race was switched.</summary>
    public bool Interact(PlayerStats player)
    {
        if (!Interactable || player == null || Race == null) return false;

        var rm = RaceUnlockManager.Instance;
        if (rm == null) return false;

        bool isHuman = string.Equals(Race.raceId, "human", System.StringComparison.OrdinalIgnoreCase);
        bool hasStone = !RequiresRitualStone || isHuman
            || (ToolManager.Instance != null && ToolManager.Instance.CountItem(RaceChangeManager.RitualStoneItemId) > 0);

        if (!hasStone)
        {
            // Unlock the race regardless; the player can return with a stone to transform.
            if (!rm.IsUnlocked(Race))
                rm.UnlockRace(Race.raceId);
            return false;
        }

        // Consent to the altar's transform cost (Human is always free).
        bool changed = ApplyRace(player);
        if (!changed && !isHuman && RequiresRitualStone)
        {
            // Unlock the race even when the transform was refused for another reason.
            if (!rm.IsUnlocked(Race))
                rm.UnlockRace(Race.raceId);
        }
        return changed;
    }

    private bool ApplyRace(PlayerStats player)
    {
        var mgr = player.GetComponent<RaceChangeManager>();
        if (mgr == null) mgr = player.gameObject.AddComponent<RaceChangeManager>();
        return mgr.SetActiveRace(Race, requireStone: RequiresRitualStone, unlockIfNeeded: true);
    }
}