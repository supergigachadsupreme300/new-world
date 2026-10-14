using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partial: beam channel lifecycle (spawn, sustain, stop) and channel reference tracking (§3.8).
/// Split from SpellCaster.cs - see the main partial for fields and focus/cooldown state.
/// </summary>
public partial class SpellCaster
{
    /// <summary>End the active Beam channel (if any). Returns true when one was running.</summary>
    public bool StopChannel()
    {
        if (_activeBeam == null) return false;
        _activeBeam.StopChannel();
        _activeBeam = null;
        return true;
    }

    /// <summary>Clear the caster's channel reference after the beam destroys itself.</summary>
    internal void ForgetBeam(SpellBeam beam)
    {
        if (_activeBeam == beam) _activeBeam = null;
    }

    /// <summary>
    /// Spawn a channeled beam from the cast point toward the aim. The beam lives while the caster
    /// holds the sustain input (LMB) and can afford its per-second focus upkeep; it fades out on
    /// release or when the pool runs dry. Charge widens the beam and raises its tick power.
    /// </summary>
    private DamageResult ResolveBeam(float power, SpellData spell, Vector3 pos, Vector3 fwd, float charge, float sizeScale, Transform origin)
    {
        StopChannel();
        var go = new GameObject("SpellBeam");
        // 1l83: root at the SAME muzzle a projectile spawns from (cast origin + cast-forward *
        // offset) and NO vertical lift. Before 1l83 this added +0.2 up AND SpellBeam.Update then
        // snapped the whole beam to the caster ROOT every frame, so the straight beam, the cone
        // funnel and the lightning bolt all launched from the player's feet instead of the weapon.
        go.transform.position = pos + fwd * CastMuzzleOffset;
        go.transform.rotation = Quaternion.LookRotation(fwd);
        var beam = go.AddComponent<SpellBeam>();
        beam.Initialize(this, spell, power, fwd, sizeScale, sizeScale, origin);
        _activeBeam = beam;
        return new DamageResult { HitTargets = true };
    }
}