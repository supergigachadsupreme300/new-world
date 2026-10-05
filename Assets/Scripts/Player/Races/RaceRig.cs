using UnityEngine;

/// <summary>
/// Swaps / scales the player's body for the active race (game-design Â§3.5, planning Task 4.3).
/// Applies the uniform scale + offset and, when a dedicated body prefab exists, drops it in and
/// tints it with the race's <see cref="RaceData.RigTint"/>. The procedural block player model is
/// colored and proportioned by the race's own palette/ratio at build time
/// (<see cref="PlayerModelBuilder.BuildPlayerModel"/>), so this rig deliberately does NOT flat-tint it â€”
/// it only applies the hitbox-affecting uniform scale. The parameter is applied via
/// <see cref="ApplyRace"/> at spawn, race change, and rig re-init.
/// </summary>
[DisallowMultipleComponent]
public class RaceRig : MonoBehaviour
{
    [Tooltip("Root body transform that gets scaled/offset. If unset, uses this object.")]
    public Transform Body;

    private void Awake()
    {
        if (Body == null) Body = transform;
    }

    /// <summary>Apply a race's rig parameters (scale/offset), optional body prefab, and prefab tint.</summary>
    public void ApplyRace(RaceData race)
    {
        if (race == null) return;
        if (Body == null) Body = transform;

        // Swap in a dedicated prefab if one is authored; otherwise scale the procedural block model.
        if (race.RigPrefab != null)
        {
            var existing = Body.gameObject;
            Vector3 pos = existing.transform.position;
            Quaternion rot = existing.transform.rotation;
            var pooled = Instantiate(race.RigPrefab, pos, rot, transform);
            if (Body != null)
            {
                foreach (UnityEngine.Renderer r in pooled.GetComponentsInChildren<UnityEngine.Renderer>())
                {
                    if (r == null) continue;
                    var mats = r.materials;
                    foreach (var m in mats)
                        if (m.HasProperty("_Color"))
                            m.color = new Color(race.RigTint.r, race.RigTint.g, race.RigTint.b, m.color.a);
                }
            }
            Destroy(existing);
            Body = pooled.transform;
        }

        Body.localScale = Vector3.one * race.RigScale;
    }
}
