# Animation/ — the components that pose a transform

Three `MonoBehaviour`s that drive transforms over time, grouped by **who computes the pose** rather
than by what the thing is — a player animator and a weapon animator are the same kind of thing.

| File | Owns |
|---|---|
| `PlayerAnimator.cs` | **procedural** blocky locomotion for the `MapBuilder` player model: two gaits blended by speed (a calm walk and an exaggerated cartoon run that takes over at sprint), swinging the `ShoulderL/R`, `ElbowL/R`, `HipL/R`, `KneeL/R` and `Torso` pivots sinusoidally. Skipped while sitting or riding; its rest pose is local identity, which is what keeps a weapon rigged to the hand bones at its equipped pose while idle. Public surface: `ShoulderL/R`, `ElbowL/R`, `AcquireArms` / `ReleaseArms` / `PingArms`, `SuppressArms`, `TorsoLookBlend`. |
| `WeaponAnimator.cs` | the held weapon's swing/charge/guard accents, written as `rest + accent` — see the rest-pose rule below. Public surface: `PlayAttack`, `PlayCharge`, `SetChargeLevel`, `EndCharge`, `PlayGuard`, `EndGuard`, `VariantCount`, and the `Accent` enum on the attack request. |
| `WeaponStowAnimator.cs` | moving a rig between **held** and **stowed** poses: a smooth ease-in-out lerp of the rig's local pose, so the weapon is drawn/sheathed rather than popped. Rigs are seeded from the `CombatController` hands by `WeaponRigBuilder.ApplyPose`; while a transition plays, combat input is gated and re-triggers are ignored. Public surface: `Register` / `Forget` / `Prune`, `SetPose(drawn)`, `Snap(drawn)`, `IsBusy`, `IsDrawn`, `AnchorParent`, `Duration`. |

## The rest pose is authored, never sampled back

This is the one invariant here that neighbouring edits break quietly, and it lives in
`WeaponAnimator`:

- **`RestoreAuthoredRest()` reads** the rest pose. **`SyncRestFromIdle()` writes** it, and only while
  no phase owns the transform. Keeping them as two named methods is what stops the next phase entry
  from re-introducing the sample, and it lets an already-drifted session heal on the next cast.
- Every frame is written as `_baseEuler + accent`. So a capture that reads the **live** transform as
  the new rest captures the *previous* phase's output — and since every phase restores **to** the
  rest, nothing unwinds it. The symptom is a rig that drifts a few degrees per cast until it is
  permanently sideways.
- **A rest pose is a fact about the rig, so it is authored.** When you add a phase, capture the rest
  before the previous phase's output is applied, or just call `SyncRestFromIdle()`.
- Re-parenting rewrites the local pose, so it is exactly the case that needs the rest to be
  re-authorable — and an idle rig is when that is safe.
- The tell is an **asymmetry in the data**, not a magnitude: only the magic weapon defs carry a
  *rotation* accent (staff/book/wand/orb), while the melee, ranged and shield defs carry none, so a
  bug that shows up on magic only names its own mechanism. Diff one category's data against the
  others before theorising about timing.

## What is deliberately NOT here

`../Player/PlayerController.Animation.cs` — player model build/reload, race-change subscription,
weapon draw/stow, and the arm-chain layer checks. It is a `partial class PlayerController`, so it
stays next to the controller it *is*; splitting a class's own partials away from it costs the reader
more than the grouping saves. The test applied: group things that are **independent components**
(the three above all stand alone), and leave a class's partials with their class.

Like `../Magic/README.md`, this folder carries no namespaces and there is no `.asmdef`, so the move
cannot break compilation. What *can* break is a `.meta`: every file here came with its own, and the
check that proves it is that the `.cs` count and the `.cs.meta` count under `Assets/Scripts` are
equal.