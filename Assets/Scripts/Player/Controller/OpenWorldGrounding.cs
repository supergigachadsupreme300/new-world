using UnityEngine;

/// <summary>
/// Keeps a transform grounded on the streamed chunk terrain.
///
/// The open world's ground is a procedural mesh (not a static plane), so any
/// object that should stand on it must raycast down to find the exact mesh
/// surface. This component performs that downward raycast each frame and snaps
/// the owner's Y to the terrain height (plus an optional stand radius).
///
/// It is non-intrusive: it only modifies the transform's position.Y and is safe
/// to attach to the player root, spawned enemies, pets, or placeds.
/// </summary>
public class OpenWorldGrounding : MonoBehaviour
{
    [Tooltip("Terrain layers to land on.")]
    public LayerMask GroundMask = ~0;

    [Tooltip("Extra stand height above the ground surface.")]
    public float StandOffset = 0f;

    [Tooltip("How far up to cast from (should exceed the tallest terrain step).")]
    public float CastHeight = 5f;

    [Tooltip("How far down to cast from the top of the cast range.")]
    public float CastDepth = 100f;

    [Tooltip("If true the object keeps its own X/Z and only the height is snapped.")]
    public bool OnlyAdjustHeight = true;

    private void LateUpdate()
    {
        SnapToGround();
    }

    /// <summary>Snap the transform to the terrain surface below it.</summary>
    public float SnapToGround()
    {
        Vector3 origin = transform.position;
        if (!OnlyAdjustHeight)
        {
            // Cast straight down from slightly above.
            Vector3 down = new Vector3(transform.position.x, transform.position.y + CastHeight, transform.position.z);
            if (Physics.Raycast(down, Vector3.down, out RaycastHit hit, CastDepth, GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + StandOffset, transform.position.z);
                return hit.point.y;
            }
            return transform.position.y;
        }

        origin.y += CastHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit2, CastDepth, GroundMask,
                QueryTriggerInteraction.Ignore))
        {
            transform.position = new Vector3(transform.position.x, hit2.point.y + StandOffset, transform.position.z);
            return hit2.point.y;
        }
        return transform.position.y;
    }
}