using UnityEngine;

/// <summary>
/// Carries a <see cref="WeaponData"/> asset reference on a rigged weapon GameObject (Phase 10).
/// WeaponData is a data-only ScriptableObject (not a Component, §3.6 Layer 1), so it cannot be
/// AddComponent'd. The rig attaches this host so UIs and behaviors can read the weapon's data
/// off the hand GameObject by component instead of the (invalid) GetComponent&lt;WeaponData&gt;.
/// </summary>
public sealed class WeaponRigHost : MonoBehaviour
{
    [Tooltip("The weapon data asset represented by this rigged weapon GameObject.")]
    public WeaponData Data;
}