using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 9 (Task: Occlusion culling): budget-friendly occlusion culler. Tracks a set of candidate
/// scene objects and, each refresh, raycasts from the camera to each candidate. Objects blocked by
/// an opaque occluder (<see cref="OccluderLayer"/>) are deactivated (their renderers disabled);
/// unobstructed ones are reactivated. Waiting a few frames is recommended so the toggle is not
/// per-frame. Composes existing scene objects without assuming a chunk structure.
/// </summary>
public sealed class CullManager : MonoBehaviour
{
    [Tooltip("Layer(bits) treated as opaque for occlusion raycasts.")]
    public int OccluderLayer = 1;
    [Tooltip("Evaluate after this many frames.")]
    public int RefreshEveryFrames = 3;
    [Tooltip("Max distance to test (further objects are always reactivated).")]
    public float MaxOcclusionDistance = 90f;

    private readonly List<Transform> _candidates = new List<Transform>();
    private readonly Dictionary<Transform, bool> _state = new Dictionary<Transform, bool>();
    private int _frame;

    public void AddCandidate(GameObject go)
    {
        if (go != null && !_candidates.Contains(go.transform))
            _candidates.Add(go.transform);
    }

    public void RemoveCandidate(GameObject go)
    {
        if (go == null) return;
        if (_candidates.Remove(go.transform))
            _state.Remove(go.transform);
    }

    private void Update()
    {
        _frame++;
        if (RefreshEveryFrames > 0 && _frame % RefreshEveryFrames != 0)
            return;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 origin = cam.transform.position;

        for (int i = _candidates.Count - 1; i >= 0; i--)
        {
            var t = _candidates[i];
            if (t == null)
            {
                _candidates.RemoveAt(i);
                continue;
            }
            bool visible = Evaluate(origin, t);
            bool prev = _state.TryGetValue(t, out var had) ? had : false;
            if (visible != prev)
            {
                _state[t] = visible;
                SetEnabled(t, visible);
            }
        }
    }

    private bool Evaluate(Vector3 origin, Transform target)
    {
        float dist = Vector3.Distance(origin, target.position);
        if (dist >= MaxOcclusionDistance) return true;
        Vector3 dir = (target.position - origin).normalized;
        var hit = new RaycastHit();
        bool blocked = Physics.Raycast(origin, dir, out hit, dist, OccluderLayer, QueryTriggerInteraction.Collide);
        if (!blocked) return true;
        // Not occluded if the ray only tagged the candidate's own collider.
        return hit.collider == null || hit.collider.gameObject == target.gameObject;
    }

    private void SetEnabled(Transform t, bool enabled)
    {
        var mr = t.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = enabled;
    }

    /// <summary>True while a candidate is currently rendered (not occluded).</summary>
    public bool IsVisible(GameObject go)
    {
        if (go == null) return true;
        return _state.TryGetValue(go.transform, out var v) ? v : true;
    }
}