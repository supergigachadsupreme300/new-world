using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enables ragdoll physics on death (Task 3.3).
///
/// Assumes an authored rig: a CharacterController (or rigidbody root) for the live body,
/// and a set of rigidbody/collider joints configured but disabled under a "Ragdoll" child
/// (or the explicit <see cref="ragdollParts"/> list). On EnableRagdoll the live body is
/// turned off and the ragdoll bodies wake with an optional impulse.
/// </summary>
public class RagdollEnabler : MonoBehaviour
{
    [Tooltip("Live body components to disable on ragdoll (e.g. Animator, CharacterController).")]
    public List<Behaviour> LiveBody = new List<Behaviour>();

    [Tooltip("Optional impulse applied to the ragdoll root on enable.")]
    public Vector3 Impulse;
    [Tooltip("Auto-collect rigidbody parts from a 'Ragdoll' child if left empty.")]
    public List<Rigidbody> ragdollParts = new List<Rigidbody>();

    private bool _enabled;

    private void Awake()
    {
        if (ragdollParts.Count == 0)
        {
            Transform ragdoll = transform.Find("Ragdoll");
            if (ragdoll != null)
                ragdollParts.AddRange(ragdoll.GetComponentsInChildren<Rigidbody>());
        }
        // Disable ragdoll bodies until death.
        foreach (var rb in ragdollParts)
        {
            rb.isKinematic = true;
            rb.GetComponent<Collider>().enabled = false;
        }
    }

    /// <summary>Turn the corpse into a ragdoll, optionally imparting momentum.</summary>
    public void EnableRagdoll(Vector3 velocity)
    {
        if (_enabled) return;
        _enabled = true;

        foreach (var b in LiveBody)
            if (b != null)
                b.enabled = false;

        foreach (var rb in ragdollParts)
        {
            rb.isKinematic = false;
            var col = rb.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            rb.linearVelocity = velocity;
            rb.AddForce(Impulse, ForceMode.Impulse);
        }
    }

    /// <summary>Convenience wrapper that enables a ragdoll without extra velocity.</summary>
    public void EnableRagdoll()
    {
        EnableRagdoll(Vector3.zero);
    }

    /// <summary>Whether the ragdoll has already been enabled.</summary>
    public bool IsRagdoll { get => _enabled; }
}
