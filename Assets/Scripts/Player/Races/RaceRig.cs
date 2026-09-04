using UnityEngine;

/// <summary>
/// Swaps / scales the player's body for the active race (game-design §3.5, planning Task 4.3).
/// Applies scale + offset + material tint to a placeholder body now; when real per-race models
/// exist they can be dropped into RaceData.RigPrefab without code changes. The parameter is
/// applied via <see cref="ApplyRace"/> at spawn, race change, and rig re-init.
/// </summary>
[DisallowMultipleComponent]
public class RaceRig : MonoBehaviour
{
    [Tooltip("Root body transform that gets scaled/offset. If unset, uses this object.")]
    public Transform Body;

    [Tooltip("Renderer(s) tinted by the race's RigTint. Auto-collected if empty.")]
    public Renderer[] TintRenderers;

    private void Awake()
    {
        if (Body == null) Body = transform;
        if (TintRenderers == null || TintRenderers.Length == 0)
            TintRenderers = GetComponentsInChildren<Renderer>();
    }

    /// <summary>Apply a race's rig parameters (scale/offset/tint) and optional body prefab.</summary>
    public void ApplyRace(RaceData race)
    {
        if (race == null) return;
        if (Body == null) Body = transform;

        // Swap in a dedicated prefab if one is authored; otherwise scale/tint the placeholder.
        if (race.RigPrefab != null)
        {
            var existing = Body.gameObject;
            Vector3 pos = existing.transform.position;
            Quaternion rot = existing.transform.rotation;
            var pooled = Instantiate(race.RigPrefab, pos, rot, transform);
            Destroy(existing);
            Body = pooled.transform;
        }

        Body.localScale = Vector3.one * race.RigScale;

        foreach (var r in TintRenderers)
        {
            if (r == null) continue;
            var mats = r.materials;
            foreach (var m in mats)
                if (m.HasProperty("_Color"))
                {
                    Color c = m.color;
                    c = new Color(race.RigTint.r, race.RigTint.g, race.RigTint.b, c.a);
                    m.color = c;
                }
        }
    }
}