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
        bool hasStone = !RequiresRitualStone || isHuman; // Ritual Stone check hooked by inventory

        if (!rm.IsUnlocked(Race))
            rm.UnlockRace(Race.raceId);

        if (!hasStone)
        {
            // No stone — we at least unlocked the race, but can't transform yet.
            return false;
        }

        ApplyRace(player);
        return true;
    }

    private void ApplyRace(PlayerStats player)
    {
        player.Race = Race;
        player.Refresh();

        var pm = player.GetComponent<RacePassiveManager>();
        if (pm != null) pm.enabled = true; // triggers refresh next Update

        var rig = player.GetComponent<RaceRig>();
        if (rig != null) rig.ApplyRace(Race);
    }
}