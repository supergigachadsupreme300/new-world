/// <summary>Partial player controller — visual/animation cluster: player model build/reload,
/// race-change subscription, drawing/stowing of equipped weapons, and arm-chain layer checks.
/// Mechanically split from PlayerController.cs; no behavior or signature changes.</summary>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    /// <summary>
    /// Whether the equipped weapons should be visually drawn in the hands (vs. stowed on the body).
    /// Weapons only draw while fighting — casual mode always sheathes them onto the back/waist,
    /// regardless of camera view (first person included). Reverted from the 1cr rule ("keep drawn
    /// in first person") per play-test feedback: a weapon at port arms is a fighting pose and does
    /// not belong in normal mode.
    /// </summary>
    public bool WeaponsDrawn => FightingMode;

    public void ApplyGender()
    {
        LoadPlayerModel();
    }

    private void LoadPlayerModel()
    {
        if (_playerModelInstance != null)
            Destroy(_playerModelInstance);

        var existing = transform.Find("PlayerModel");
        if (existing != null)
            Destroy(existing.gameObject);

        // Remember what was equipped so a model reload (gender/race change) can re-rig the same
        // weapons once the fresh hands exist — but never auto-equips weapons the player unequipped.
        _pendingAutoRig.Clear();
        var combat = GetComponent<CombatController>();
        if (combat != null)
        {
            var rh = combat.RightHand != null ? combat.RightHand.GetComponent<WeaponRigHost>() : null;
            if (rh != null && rh.Data != null && rh.Data.id != WeaponCatalog.FistWeaponId)
                _pendingAutoRig.Add((rh.Data.id, false));
            var lh = combat.LeftHand != null ? combat.LeftHand.GetComponent<WeaponRigHost>() : null;
            if (lh != null && lh.Data != null && lh.Data.id != WeaponCatalog.FistWeaponId)
                _pendingAutoRig.Add((lh.Data.id, true));
        }

        _playerModelInstance = MapBuilder.BuildPlayerModel(transform);

        if (_playerModelInstance != null)
        {
            // Body renderers → layer 6 (culled in first person). Arm/hand renderers (and any
            // weapon rig parented to a hand, which hangs under the Shoulder pivots) → layer 7,
            // which CameraModeSwitch keeps visible so the player sees their own arms in 1st person.
            foreach (var r in _playerModelInstance.GetComponentsInChildren<Renderer>())
                r.gameObject.layer = IsArmUnderShoulder(r.transform) ? 7 : 6;
            _playerModelInstance.AddComponent<PlayerAnimator>();
        }

        // The rebuilt model may have appeared after an early equip parked the weapon rig on the
        // player root (hidden inside the torso); re-seat it onto the fresh hand bones.
        WeaponRigBuilder.ReparentToHands(gameObject);
        // Re-apply the current weapon pose (drawn only while fighting, stowed in casual mode) now
        // that the model's hand + body anchors exist again. Snap immediately — a fresh model has no
        // in-flight draw/stow transition to continue.
        ReApplyWeaponPose(instant: true);

        // Subscribe once: a race change rebuilds the model with the new palette/body ratios
        // (§3.5 Race Visuals). Idempotent — LoadPlayerModel runs on Awake, gender, and respawn.
        if (!_raceSubscribed)
        {
            var rcm = GetComponent<RaceChangeManager>();
            if (rcm != null)
            {
                rcm.OnActiveRaceChanged += _ => LoadPlayerModel();
                _raceSubscribed = true;
            }
        }
    }

    /// <summary>True when the renderer sits on the arm chain (Shoulder → Elbow → Hand) or a held
    /// weapon rig parented to it. Declared inline so no top-level helper is added to the class.</summary>
    private static bool IsArmUnderShoulder(Transform t)
    {
        while (t != null)
        {
            if (t.name.StartsWith("Shoulder")) return true;
            t = t.parent;
        }
        return false;
    }
}
