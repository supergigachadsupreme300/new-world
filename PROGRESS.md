## 1jz. Flamethrower's funnel now rotates with the player like BeamEnd (testing.md task 3)

The user reported the flamethrower funnel chunk doesn't track the player: "edit flamethrower funnel
chunk to rotate with player like beamEnd". Root cause in `SpellBeam.Update`: the beam re-set
`transform.position` and re-derived `Direction` every frame, and the LINE body re-aimed itself
(`_body.rotation = FromToRotation(up, Direction)` in `PulseVisual`) — but the CONE's funnel never
did. `BuildFunnelVisual` mounts all its discs + debris + ring in the beam parent's local +Z ONCE at
build time, and nothing ever re-oriented that parent, so the whole cone stayed frozen at the
cast-time aim while the damage tick walked the new `Direction` and the `BeamEnd` tip orb swung around
with the player. One line fixes the family: re-orient the funnel parent to the live Direction each
frame.

- `SpellBeam.Update` after `Direction` is set: `if (_funnelChunks != null && Direction.sqrMagnitude >
  0.0001f) transform.rotation = Quaternion.FromToRotation(Vector3.forward, Direction);` — +Z is the
  funnel's forward by build convention, and re-facing the PARENT re-aims every disc, the orbiting
  debris and the leading ring at once. Gated on `_funnelChunks` so the line beam (which places its
  body in world space and never reads the parent rotation) is untouched.
- The `PulseVisual` debris orbit rotates chunks in the parent's local X/Y about +Z — with the parent
  now aimed at the live direction, the orbit plane is perpendicular to the true beam axis (it was
  frozen at cast time before), so the swirl also tracks the player.

Verification: grep + reread; `transform.rotation` is written nowhere else in `SpellBeam.cs` and read by
no external consumer, so the line beam's behaviour is byte-identical. No Unity build (rule 3).
    `skills: none applied` - no installed skill governs this C# edit (rule 15).

### 1jz-status
- [x] `SpellBeam.Update`: funnel parent re-aimed to the live `Direction` each frame (cone path only).
- [x] Docs: game-design.md Beam bullet + magic-skills.md Beam row note the cone now sweeps as a whole.
- Verification: `tools\StaticChecks.ps1` => 0 candidates; no other reader of `transform.rotation`.
- [ ] **PLAY-TEST:** hold Flamethrower and turn/move the aim while channeling — the funnel (discs,
      debris, leading ring) and the bright tip orb must stay aligned in front of the player and swing
      together; Searing Ray (line beam) unchanged; QA bench still shows the funnel on its pedestal.

## 1jy. Fire Wave is now a ground-hugging forward wave projectile (testing.md task 2)

The user asked to convert Fire Wave's AoE-placement zone skill ("firewave skill is currently an aoe
placement skills") into "a projectile type but only stay to the ground and shoot out and wave
projectile forward". Fixed in `SkillCatalog.Magic.cs:322`:
`SpellDelivery.Zone` → `SpellDelivery.Projectile`, `deliveryRange: 12f`, and a new
`ProjectileShape.Wave` — the FIRST shape that is both a look AND a behaviour, where the behaviour is a
complete flight-mode rewrite in `SpellEffect`:

- **A new authored-only `ProjectileShape.Wave`.** `SpellData.cs` gains `Wave = 12`. It is behaviour,
  not a look: `SpellEffect.Launch` sets `_wave` when `Shape == Wave`, caps travel to exactly
  `Range / Speed` (~0.6 s at 12 m / 20 m/s — the 4 s default Lifetime would have run it 80 m), and
  kicks off with a `SkillFx.RingFlash` on the GROUND beneath the spawn. Because `SpellLook.Resolve`
  inherits a non-Auto `spell.Shape` into `DisplayShape`, the wave body flows to the look without any
  profile authoring; the resolve comment (`SpellLook.cs:327-331`) now says families never contain
  Missile **or** Wave.
- **The wave rides the terrain.** The new per-frame branch in `Update` flattens `_dir.y = 0`,
  re-faces the body with `LookRotation(flatDir)`, and clamps the center to
  `ground.point + 0.35` via a downward raycast — so the disc always lies flat and glued even when the
  release was aimed up or down. The normal flight probes (enemy raycast, ground-probe detonation) and
  the trail strip are all bypassed on purpose: a wave may not die on the ground it rides or on the
  first foe it touches.
- **Each foe is hit ONCE as the band passes.** `SweepWave()` ticks a `OverlapSphereNonAlloc` band at
  `SplashRadius` every `WaveTickInterval` 0.15 s, but a per-wave `HashSet<Transform> _waveHit` (root
  keyed, caster excluded) enforces a single `ResolveHitAt` + the spell's existing caster-outward
  knockback — no re-resolve/re-knock on later ticks.
- **Body.** `MagicProjectileModelBuilder.Wave(...)`: a flat disc of 16 voxel cubes at ground level
  with a brighter 7-cube forward crest arc — reads as a wave rolling outward, and the bench picks it
  up automatically via `CreateProjectileDisplay` (no F4 special-casing needed).

### 1jy-status
- [x] `SpellData.cs`: `ProjectileShape.Wave = 12` + doc comment stating it is behaviour like Missile
      and authored-only; `Shape` tooltip extended.
- [x] `SkillCatalog.Magic.cs:322`: Fire Wave → Projectile delivery, range 12, `projectileShape: Wave`
      (power 32 / FP 22 / cd 6 / radius 3.4 / knockback 2.5 unchanged).
- [x] `SpellEffect.cs`: `_wave` flag (Launch), lifetime cap `Range/Speed`, ground ring at launch,
      terrain-glued per-frame wave branch (flatten + re-face + clamp + `SweepWave` + move), spent
      wave's end burst via `SpellImpactFx`, `_waveHit` once-per-root set; `using System.Collections.Generic`.
- [x] `MagicProjectileModelBuilder.cs`: `case ProjectileShape.Wave` + `Wave(...)` body builder.
- [x] `SpellLook.cs:327-331`: comment now names Wave as behaviour (families never grant it).
- [x] Docs: §3.8.1 shape-table **Wave** row + **Ground waves** delivery bullet; magic-skills.md row
      gains `range 12, shape:Wave`.
- Verification: grep + reread. Confirmed `projectileShape:` is the `Spell(...)` factory param (34
  sites), `SplashRadius`/`_splashBuffer`/`HitLayers`/`IsGroundCollider`/`ResolveHitAt`/`SkillFx.RingFlash`
  signatures reused from existing call sites, the resolve inherit path flows Wave to display, and the
  bench draws the Wave body via `CreateProjectileDisplay`. `tools\StaticChecks.ps1` => 0 candidates.
  No Unity build (rule 3).
      `skills: none applied` - no installed skill governs these C# edits (rule 15).
- [ ] **PLAY-TEST:** craft/equip Fire Wave. (a) casting sends a ground-level ring of cubes sweeping
      forward roughly 12 m and ~0.6 s, staying flat across slopes and drops; (b) foes the band passes
      over are damaged once each and shoved forward (caster-outward knockback along the ground); (c)
      it passes over the ground without detonating and through walls/foes; (d) the wave dies with a
      small impact burst at the end of its reach; (e) the F2/F3 lane and bench list Fire Wave under
      projectile shapes, drawn as the flat wave disc.

## 1jw. Continuous Fireball: follow circle now body-anchored, bolts now homing (testing.md task 1)

Two changes the user asked for in testing.md: the follow circle must sit "behind the player" and rotate
"with the player's upper body", and the fireballs "shooting from the skill" must have the same "flypath
update" as other projectiles — in-flight homing.

- **The follow circle stations on the CASTER BODY, not the camera aim line.** The player's body rotation
  is yaw-only (PlayerController.Camera.cs writes `Quaternion.Euler(0, _yaw, 0)`), so the flattened
  `_casterRoot.forward` IS the upper-body yaw. `SpellSummon.FollowCaster` now places the circle at
  `-BackOffset` along that bearing, still on the shared `GroundUnder` probe. The old 1is derivation
  reused `SpellCaster.CurrentAimDirection` (the camera-point aim the beam and the spray use), which
  pulled the circle off-axis by the third-person camera's 1jl shoulder offset and collapsed it onto the
  player's feet when aiming steeply down. The spray (`FireForward`) deliberately still uses the camera
  aim line — "pours bolts down your aim line" — so the station basis and the fire basis are now
  different by design, and the stale "one fact, one place" comment was rewritten to say so.
- **Spawn and first follow frame agree by construction.** `ResolveSummon` derived the caster-anchored
  back offset from the flattened camera-point aim `fwd`; it now uses the player root's yaw-only forward
  (`transform.root.forward`, flattened) — the identical reference `SpellCaster` hands `SpellSummon` as
  `_casterRoot` (`caster.transform.root`) — so the circle no longer appears a few degrees off the body
  and snaps onto it on the first frame.
- **The bolts are homing.** Continuous Fireball's spell now carries `projectileShape:
  ProjectileShape.Missile` — the gameplay axis that makes `SpellEffect._homing` true, the same "flypath"
  Arcane Missiles and Chill Soul follow. Because `SpellLook.Resolve` inherits a non-Auto `spell.Shape`
  into `DisplayShape` (a bare Missile would silently re-body the bolt as the arcane-missile cone), the
  look profile re-authors `shape: ProjectileShape.Comet` — verified as exactly the body the
  deterministic Fire-family pick already resolved for this spell id (FNV-1a + murmur mix, `rE = 0.4995`
  → index 1 of `{Sphere, Comet, Shard}` = Comet), so the drawn bolt is byte-for-byte unchanged and only
  the flight path gained homing.

### 1jw-status
- [x] `SpellSummon.FollowCaster`: back offset from the flattened body forward (yaw-only); comment states
      the station (body) and fire (camera-aim) bases are deliberately different.
- [x] `SpellCaster.Cast.cs ResolveSummon`: caster-anchored spawn offset from `transform.root.forward`
      (flattened), matching `_casterRoot`, so spawn == first follow frame.
- [x] `SkillCatalog.Magic.cs` continuous-fireball line: `projectileShape: ProjectileShape.Missile` +
      `shape: ProjectileShape.Comet` in its authored Look; the comment records why both (homing vs
      display, and the inheritance hazard a bare Missile would have caused).
- Verification: grep + reread + `tools\StaticChecks.ps1` => 0 candidates. Confirmed `projectileShape:`
  is the `Spell(...)` factory's parameter name (34 existing call sites), the class-skill `Look` factory
  sets `DisplayShape = shape`, the bolt carries the summon's `_spell` into `SpellEffect.Initialize`
  (homing engages at SpellEffect.cs:106), and the display-shape seed (`0x165667B1`, SpellLook.cs:312)
  matches the hash used to pick Comet. No Unity build (rule 3).
      `skills: none applied` - no installed skill governs these C# edits (rule 15).
- [ ] **PLAY-TEST:** cast Continuous Fireball in THIRD person: (a) the circle sits directly behind the
      player and rotates with the body as the camera turns, staying glued when aiming down/up;
      (b) walk/strafe — the circle tracks behind the body, ground-snapped; (c) the auto-fired bolts now
      curve onto enemies like guided projectiles while still drawing the fireball **comet** body (no
      arcane-missile cone appears).
- [ ] Carried-over pending play-tests (unchanged): 1jt (both Meteors, Fireball on slopes, Continuous
      Fireball, no-spell/ranged paths, third-person alignment, F4 output), 1jr/1ju regression (turning
      while moving must not zoom; strafing must not yaw), and 1jv Numpad2 verdict.

## 1jv. "The camera is continuously bugging when moving" / "snaps in and out / zooms, everywhere even on flat open ground" - MEASUREMENT LANE SHIPPED, mechanism not yet chosen

Reported by the user; the fix has NOT been chosen, because rule 7 says measure before fixing what you
cannot see. Two mechanisms were derivable from the code and they need different fixes, and the first
hypothesis was **falsified before touching it**: the collision clamp. The boom rises `ThirdPersonY` -
pivot height = 2.6 - 1.5 = **1.1 m** over `ThirdPersonDistance` 6.5 m (boom length ≈ 6.65 m), and the
collision `SphereCast` sweeps along that boom from the pivot — so the ray climbs from 1.5 m up to ~2.6 m
as it travels and **never descends below the pivot's height**. Flat open ground cannot be in the sweep;
the clamp needs a wall or a slope rising ≥ ~1.5 m within the boom's reach. The report's own qualifier
("everywhere even on flat open ground") already excluded it. The surviving candidates are (a) the
**position smoother** lengthening the boom's *effective* reach as the camera trails behind the player
(`lag = v * SmoothTime`, see 1jn/1ju - 0.75 m walk / 1.5 m sprint), and (b) the **boom swinging** with
mouse yaw. Both move the distance, so the *number* cannot separate them - the *controls* (speed + yaw)
can. Hence the measurement lane.

### 1jv-status
- [x] `CameraModeSwitch` now publishes the boom's own two lengths - `BoomRestLength` (assigned from
      `targetDist`, the unclamped desired boom in `UpdateThirdPerson`) and `BoomAppliedLength` (assigned
      from `finalDist`, the post-`SphereCast` length). The lane measures against these rather than re-
      deriving the boom vector (rule 8: no second spelling; a "shorter applied than rest" is the clamp,
      "rest held but measured distance grew" is the smoother trailing past the boom's end).
- [x] `NewWorldTestGround` boom audit lane (bench key **Numpad2**): fields `EnableBoomAudit` +
      `BoomAuditKey`, a 120-frame trailing window fed every frame by `TrackBoomFrame` (distance, pivot
      speed, pivot yaw, rest/applied boom length, clamped flag), and `SnapshotBoomAudit` read on the key.
      Sections A (premise: third person + switcher present), B (controls: peak speed, moving frames, mouse
      yaw total), C (measure: camera-to-pivot min/max/range/peak step, boom rest/applied min..max, clamped
      frames), D (verdict naming a mechanism: VOID / NOT MEASURED / NOT REPRODUCED / COLLISION CLAMP /
      POSITION SMOOTHER / INCONCLUSIVE / STEADY). Strictly read-only (rule 7): samples two transforms and
      two published floats, writes only its own arrays; the report describes the window that ended on the
      frame the key was pressed.
- [x] The switcher is resolved once per controller change (cached `_boomOwner`/`_boomMode`,
      `GetComponent<CameraModeSwitch>()` only when the player object changes), and the window resets with
      it - a respawn must not mix the old camera's samples into one report.
- [x] Numpad2 verified free across all three Input System spellings (`Key.Numpad2` / `.numpad2Key` /
      `[Key.Numpad2]`), F1 as the positive control; `tools\StaticChecks.ps1` check 8 now lists it among
      the 7 lane keys. The older tooltips' stale "Numpad2-Numpad9 are free" claim was corrected in place.
- [x] The committed draft (user's `6c8c702 "camera fix in progress"`) referenced `_boomYaw` /
      `_boomLastPivot` / `_boomSeen` that it never declared (CS0103) and kept a write-only
      `_boomTrackable`; this task's commit declares the arrays/fields and the owner-cache, drops
      `_boomTrackable`, and restores the orphaned 1jq trail XML doc to sit directly above
      `SnapshotTrailAudit`.
- Verification: grep + reread + `tools\StaticChecks.ps1` => 0 candidates (NewWorldTestGround.cs braces
      193/193 parens 1270/1270; check 8 green for Numpad2). No Unity build (rule 3).
      `skills: none applied` - no installed skill governs a third-person follow-camera measurement lane;
      the Unity skills cannot run in this project (rule 15).
- [ ] **PLAY-TEST (this is the deliverable's whole point):** open in third person on flat ground, walk
      and sprint with the mouse held steady, press **Numpad2**; repeat with a burst of mouse turning, then
      against a wall so the collision clamp is genuinely active. The section-B controls and section-D
      verdict are expected to separate the smoother from the swing from the clamp. Report the readout
      verbatim (console or HUD). The verdict then names the mechanism to fix in a follow-up task.
- [ ] Carried-over pending play-tests: 1jt (both Meteors, Fireball on slopes, Continuous Fireball, the
      no-spell/ranged paths, third-person alignment, F4 output), and 1jr/1ju regression (turning while
      moving must not zoom; strafing must not yaw).

Reported by the user: "the camera turning abit when moving to the side is not needed". `CameraModeSwitch`
derived its rotation from `LookRotation(lookTarget - _camera.transform.position)`, and that position is
the output of `Vector3.SmoothDamp`. A smoother leaves the camera **trailing** the player, so the trail
was being subtracted from the look-at: **position lag steered the aim**. Forward running trails *along*
the view axis, which is only a pitch shift and reads as "fine"; **strafing** trails *sideways*, which is
a yaw. Steady-state lag is `v * SmoothTime` = 0.75 m at walk over a 6.5 m boom = **6.6 deg**, and
**13 deg** sprinting, flipping sign with the strafe direction - and 0.75-1.5 m is comparable to
`ThirdPersonSideOffset` (0.9 m), so the over-the-shoulder framing shifted during the strafe too. The fix
is **one token**: aim from `desired`, which the method already computes as the unlagged boom position.

### 1ju-status
- [ ] **Strafe left, then strafe right** - the horizon must not yaw in either direction, and the two
      must match. This is the whole report; everything else is a guard against a regression I introduced.
- [ ] **Walk forward / backward** - the view must be unchanged from before (the old pitch-shift artifact
      is also gone; a small pitch wobble on accel/decel is expected and was always there).
- [ ] **Strafe while turning the mouse** - the two must compose; no yaw should come from the strafe alone.
- [ ] **A wall beside the player** (collision pull-in) - must still pull in and yaw as it did before.
      Deliberately **unchanged** by this task; see below.
- [ ] **F5 both ways** - first person snaps to the pivot rotation and never entered the changed line, so
      it must be bit-identical.
- [ ] **1jr regression** - turning while moving must still not zoom the camera in.
- [x] Provenance: with the player's rotation written only by mouse look (grep: every `transform.rotation`
      write on the player is mouse-look, spawn-reset, or a cutscene) and `CameraFollow` correctly disabled
      in third person (`ApplyCameraFollow`), `LateUpdate` L204 was the **only** writer of the third-person
      camera's rotation and the smoothed position its only strafe-dependent input.
- [x] The change is a no-op at rest: `lookTarget - desired` contains neither `pivotPos` nor the smoothed
      position, and `desired` is the boom position, so when the camera has caught up the expression is
      byte-identical to the old one. Identical under a collision pull-in too (same `finalDist`).
- [x] `SmoothTime`'s tooltip now records that it is position-only and can no longer steer the view.
- Verification: grep + reread. `tools\StaticChecks.ps1` does **not** cover `CameraModeSwitch.cs` (not in
      its `$files`), so this edit is outside the rule-3 instrument by construction - reread, not script.
      No Unity build (rule 3).
      `skills: none applied` - no installed skill governs a follow-camera aim fix, and the Unity skills
      cannot run in this project.

### Reported, not fixed (deliberately)
- **The collision yaw.** When a wall pulls the boom in, `finalDist < targetDist` and `lookTarget` (0.9 m
  right of the pivot) stops cancelling against `toCam`'s own lateral component, so the camera yaws toward
  it - up to ~17 deg at a 2 m pull-in. It is the *same* anti-pattern but a different trigger, and fixing
  it is a visible behaviour change, so it is filed here rather than bundled onto a fix whose whole value
  is that it changes nothing at rest.
- **`Player/ThirdPersonCamera.cs`** is a second, **dead** class carrying the same anti-pattern
  (`LookRotation(pivotPos - transform.position)` at its own `RotationSmoothTime`). Zero code references;
  its only mention anywhere is a comment in `ScreenShake.cs` - the comment-mention false positive a bare
  grep cannot filter out (rule 8). Deleting it is a rule-14 task with its own sweep.
## 1jt. Casting circles drew on the held weapon - and the fix that deleted two Meteors was a one-word enum change

Requested by the user: summon casting circles belong under the player, every other delivery's belongs
in front of them. `PlayerController.UpdateCastingCircle` hung the halo off `MagicHand(combat).transform`,
which parked it at the staff - a mark that read as part of the weapon rather than as the cast. Added a
drawn-only look axis, `SpellLookProfile.CastAnchor` / `SpellCastAnchor` (`Inherit`/`Front`/`Feet`),
resolved in `SpellLook.Resolve` from `spell.Delivery == SpellDelivery.Summon ? Feet : Front` with the
authored profile overriding, so all 172 spells classify with **zero unclassified cases** (a total
predicate over a field that already exists, not a hand-listed set). Placement moves to the ground via one
shared probe, `SpellCaster.GroundUnder`, extracted from `SpellSummon.FollowCaster` so the halo and the
rear-mounted familiar cannot disagree about where "the ground" is. Front offset is
`SpellLook.CastFrontOffset` (1.8 m), and `SpellSummon.BackOffset` now **aliases** it so the halo and the
familiar stay symmetric about the player.

### 1jt-status
- [ ] **Fire Meteor and Earth Meteor** - both must draw the halo at the player's feet while keeping
      `SpellDelivery.Zone`, the falling rock, and (Earth) the Crater at the aim point. This is the whole
      read: the two spells that must look summoned and behave zone-delivered at the same time.
- [ ] **Any non-summon spell** (e.g. Fireball) - the halo and the release ring must both sit ~1.8 m in
      front, on the ground, and must sit on a **slope** rather than floating at the caster's feet height.
- [ ] **Continuous Fireball** (`SpellDelivery.Summon`, `casterAnchored`) - halo at the feet, while its
      summoned familiar keeps its own circle **behind** the player. Two circles, two places, on purpose.
- [ ] **Ranged / no-spell-armed paths** - cast preview, ranged-draw accent and the plain white release
      burst must still hang on the weapon with no ground halo and no colour change.
- [ ] **Third person** - the halo follows the *player's* forward, not the camera's, so the 1jl shoulder
      offset must not shift it sideways.
- [ ] **F4 lane** - expect the new `cast anchors:` line, `distinct identities` unchanged at 172/172/0
      (the key was already all-distinct, so an added axis is inert *by construction* - a derivation, not
      a measurement), and `authored profiles` **21 -> 22** because Fire Meteor gained a profile.
- [x] `SpellDelivery` is **unchanged** for both Meteors - confirmed by grep, `SpellDelivery.Zone` still
      present on both definitions.
- [x] `ResolveZone` retains its `SummonFallingRock` branch and its Zone `TerrainShape` application;
      `ResolveSummon` was not given either.
- [x] Front direction is `PlayerController.transform.forward` with `y` flattened, not
      `CameraModeSwitch.lookTarget` (which carries `ThirdPersonSideOffset`) and not a camera-position ray
      (1jm's coupling).
- [x] The offset is applied **before** the ground probe, and the probe skips the player's own hierarchy.
- [x] The release burst and the charge halo both call the one `GroundCastPoint` function, and the halo is
      re-placed every frame, so the two cannot drift apart mid-charge.
- [x] Fire Meteor's profile is **anchor-only**: `Impact`/`Cast` left `Inherit`, so its deterministic Fire
      picks are untouched and only the halo position changes.
- [x] `castAnchor` added to **both** `Look(...)` factories (`SkillCatalog.cs`, `ClassSkillCatalog.cs`) so
      the two same-named overloads in one partial class cannot express different subsets of a profile.
- Verification: grep + reread + `tools\StaticChecks.ps1` -> **0 candidates** (`NewWorldTestGround.cs`
      braces 178/178, parens 1126/1126). No Unity build (rule 3).
      `skills: none applied` - no installed skill governs a cast-placement edit reviewed by grep, and the
      Unity skills cannot run in this project anyway.

### Follow-ups filed, not done
- **Behind-player placement for the other caster-anchored summons is a separate task, deliberately not
  bundled here.** It is not a one-line flip: setting `casterAnchored` changes the model
  (`BuildTotem` -> `BuildFamiliarCircle`), whether the summon follows the player, and a charge-scaled
  lifetime. Nine non-healing summons are candidates; Healing Shrine is an aura and must stay excluded.
- **`CastAnchor` is in the F4 identity key, `Authored` is not.** `Authored` means "a profile exists", not
  "looks different", so packing it would split groups that are genuinely identical. Recorded because the
  two read similarly and only one belongs.
## 1js. The projectile trail did not read as its element - a trail colour that is computed, not a table

Requested by the user: "make the magic projectile trail match with the element, with physical type make
it white". The trail already received the per-spell resolved look, so it was *already* per-element in
provenance - it read `SpellLook.Edge`, which is `EdgeFor(core, r)` and drops saturation to 55%. That is
correct for a one-quad rim highlight and wrong for a wide, soft, partially-transparent ribbon: 55%
saturation on a thin additive strip reads as grey, so no school was legible as itself. Added
`SpellLook.Trail` (a real member on the look struct, resolved in `Resolve` alongside `Core`/`Edge`, so
rule 13's "derived in exactly one place" holds and `TrailStrip` still derives nothing) and pointed the
strip at it. `SpellLook.TrailColor` derives from the existing `SchoolColor` table rather than spelling a
third per-school palette, with `Physical` overridden to pure white.

### 1js-status
- [ ] **Fire, Ice, Lightning and Earth casts in flight** - the readout cannot show this. The QA matrix
      band draws every spell on a motionless pedestal and `TrailStrip.Spawn` is only reached from
      `SpellEffect.Update` *after* `if (!_launched) return`, so a non-launched matrix entry has no trail
      to look at. Verified by casting.
- [ ] **A physical spell (arrow / bolt) in flight** - the trail must be white, and the projectile *body*
      must stay gold. That second half is the part that proves the override landed in `TrailColor` and
      not in `SchoolColor`, which would have recoloured every physical projectile and impact as a side
      effect of a trail request.
- [x] Resolved values confirmed by hand, all ten schools, with two passing controls
      (`Lerp(black,white,0.45)=0.45`, `Lerp(white,white,0.45)=1`): see the `game-design.md` �3.8.3 list.
- [x] `SpellLook.SchoolColor` and `SpellImpactFx`'s `look.Edge` reader deliberately **not** changed -
      confirmed still gold and still reading `Edge` respectively.
- Verification: grep + reread + `tools\StaticChecks.ps1` -> 0 candidates; balance delta vs HEAD
      `SpellLook.cs` +1/+1 braces +7/+7 parens, `NewWorldTestGround.cs` 0/0 and +3/+3. No Unity build
      (rule 3). `skills: none applied` - no installed skill governs a look-resolution edit reviewed by
      grep, and the Unity skills cannot run here anyway.

### Follow-ups filed, not done
- **A colour formula is a piece of arithmetic that can be wrong in a way nothing reports.** The first
  version of `TrailColor` held hue in HSV and raised saturation and value ("same hue, brighter"). Hand
  checking showed it turned `Earth` (0.78,0.62,0.42) into `(1.00,0.73,0.38)` - r-g = 0.27, g-b = 0.35,
  which is a saturated **orange**, i.e. the Earth trail would have read as *Fire*, the exact confusion
  this task removes. Chroma is saturation x value, so raising value on a low-saturation school shifts its
  apparent hue; holding `h` constant does not hold the *perceived* hue constant. `Color.Lerp` toward
  white is monotone and preserves every school's channel ORDER, so the result is checkable by hand and
  cannot surprise. Recorded in `AGENTS.md` rule 13.
- **`LookKey` was deliberately NOT extended** with the new colour. The key is bit-full at 34 bits, and
  `TrailColor` is a pure function of `DamageType`, so the new axis is strictly COARSER than the `Core`
  RGB already packed into it: it can only split a group in the coincidence where two schools `Tint` to
  the same `Core`, which is not the collision the 1ic audit measures. Said out loud because rule 13's
  corollary requires it - a quiet "the numbers should not have moved" is indistinguishable from never
  re-running the audit.
## 1jr. The third-person camera zoomed in when the player turned while moving - a cached distance applied to the wrong ray

Reported by the user as "camera sometime bug and zoomin if player turn while moving". The cause is in
`CameraModeSwitch.UpdateThirdPerson`: the collision `SphereCast` runs at ~10 Hz
(`CollisionCheckInterval` = 0.1 s) and the clamped distance was reused in between **unconditionally**. A
distance is only meaningful along the ray that produced it, and `toCam` changes on every turn and
strafe - so a clamp taken "straight back" was applied to "back and to the left", worst exactly when the
direction changed fastest, which is turning. Fixed by caching the direction too (`_cachedDir`) and
re-measuring when the boom has swung more than `RecastOnTurnDegrees` = 8° since the last cast. Gated on a
clamp being active, so a clean frame still costs one query per 0.1 s.

### 1jr-status
- [ ] **Turn while moving** — the report itself. Camera should not pull in unless something is actually
      behind the player along the CURRENT boom direction.
- [ ] **Turn while backing into a wall** — a real pull-in should still happen, and releasing should not
      pop. If a jerk remains, the next candidate is the one-sided ratchet: `Mathf.Min` only ever pulls
      in, and release waits up to 0.1 s for a clean cast. That is a behaviour change, so it gets a
      measurement first (rule 7) rather than a guess.
- [ ] **Pivot-inside-own-collider trap** — `CollisionMask = ~0` with the cast starting at the pivot,
      which sits inside the player's `CharacterController`. Left unchanged on purpose: `SphereCast` does
      not report colliders the sphere already overlaps at the origin, so the player's own capsule is
      suppressed. Not verified by measurement — if the camera ever slams to 0.1 m with nothing in the
      way, that assumption is the thing to re-check, and the fix would be a mask change, not a cast change.

## 1jq. The projectile trail is one camera-facing strip, not ~23 pooled cubes

**Status: SHIPPED, unverified.** `Assets/Scripts/Magic/Fx/TrailStrip.cs` (new),
`Assets/Scripts/Magic/Cast/SpellEffect.cs`; `ProjectileTrail.cs` + `.meta` **deleted**;
`Assets/Scripts/Opt/NewWorldTestGround.cs` (+ `game-design.md`, `AGENTS.md`, `PROGRESS.md`,
`THINKING.md`). **Verified by grep + reread; no Unity build** (rule 3).
`tools\StaticChecks.ps1` -> **0 candidates**, and check 8 now lists **6** lane keys including
`Numpad8` with no second binding anywhere in `Assets\Scripts`. `NewWorldTestGround.cs` balance delta
vs a `StripNonCode`-calibrated `git show HEAD:` baseline: braces **+5/+5**, parens **+61/+61**.
**skills: `scenario-unity-vfx` loaded** - informative only, not authoritative (rule 15): it supplied the
trail/camera-facing conventions and the per-frame-allocation reasoning, and it cannot verify anything
here because rule 3 forbids driving a Unity build.

**What changed.** `ProjectileTrail` emitted one pooled 5-9 cm cube every `Step` = 0.3 m, each living
`Life` = 0.35 s. At the default 20 m/s that is **~23 live cubes per flying projectile**: 23 GameObjects,
23 `MeshRenderer`s (23 draws), 23 `MonoBehaviour`s ticking every frame - and the pooling did not make it
cheap, because `ObjectPooler.Return(go, delay)` does `AddComponent<ReturnTimer>()` on **every** emit and
`ReturnTimer.Update` then calls `Destroy(gameObject)`, so each voxel cost a native component create plus
a deferred destroy per life. `TrailStrip` is **one** GameObject, one `MeshRenderer`, one `Update`, one
pre-sized vertex buffer rewritten in place with **no per-frame allocation**. It is camera-facing
(each cross-section offset perpendicular to both the local tangent and `Camera.main.forward`), tapered
**0.10 m at the head to 0 at the oldest point**, with a matching alpha ramp, and it self-destructs when
its last point ages out.

`Step` (0.3 m) and `Life` (0.35 s) are **carried over unchanged**, and `MaxPoints` = 29 is derived from
them rather than chosen: the fastest authored magic projectile is 22 m/s, so `22 x 0.35 / 0.3 = 25.7`
segments are live at once and 29 gives headroom. On overflow the **oldest** point is dropped, so the tail
shortens and the arrays never grow. Colour is the already-resolved `SpellLook.Edge` carried in the
**mesh's vertex colours**, which is why one shared material serves every strip of every school - so the
strip adds no second `SpellLook.Resolve` and derives no colour of its own (rule 13). Emission stayed in
`SpellEffect.Update` behind its existing `if (!_launched) return;` gate, which is 1jg's placement rule
and is what keeps the static spell bench and the turret's `DecorateProjectile` bolt from hanging a
strip on a pedestal that never moves.

**Two things the rewrite did not inherit for free.** The old taper was **age, not alpha** - voxels got
their gradient for free from expiring, so a single strip must author both ramps explicitly. And because
the strip is world-space and **unparented** (as the voxels were), `SpellEffect`'s `Destroy(gameObject)`
on impact needs no hand-off at all: the strip simply keeps lying where the bolt *was* and deletes
itself. The visible cost is that the last <= `Step` of approach is not drawn; the bolt's body covers it.

**Two defects found by rereading my own new file, both fixed here** (neither could have been caught by a
grep or by `StaticChecks.ps1`):
- `Spawn` pushed the spawn point **twice**, with a comment claiming this "guarantees a legal segment on
  the first `Update`" because the too-few-points early-out "would destroy the trail 0.3 m after it was
  born". **Both halves were false** - `Update` destroys only on `_count == 0` and returns harmlessly on
  `_count < 2`, so a one-point strip survives on its own. The duplicate bought nothing *and corrupted the
  measurement*, since a zero-length first segment reported `1` segment / `2` triangles with no visible
  geometry. Removed; the comment went with it (rule 8's "a comment that documents a path that is not
  taken is worse than no comment", in its sharpest form - it asserted a control-flow fact).
- `VertexCount` returned `_count * 2` even when `_count < 2`, i.e. it reported 2 vertices for a mesh
  nothing had been written into. Now 0 unless there is a segment (1ik: an absent measurement and a
  measurement of zero are different). `Spawn` also seeds the mesh bounds at the spawn point now that a
  one-point strip lives for its first 0.3 m with an empty mesh.

### 1jq-status
- [x] **`ArgumentOutOfRangeException` on the first frame a trail drew** (found on the user's console,
      fixed in the follow-up commit) - and this one was **caused by the CS1503 fix**. Retyping the colour
      to `Color` moved the upload onto `SetColors(List<Color>)`, so the colour buffer became a
      `List<Color>` instead of an array; `new List<Color>(58)` sets a **Capacity**, so `Count` starts at 0
      and the indexer setter rejects every index. The buffer is now filled once at spawn. An array's
      `Length` is both its capacity and its last valid index + 1 - a `List`'s is not.
- [x] **CS1503 `cannot convert float to byte`** (found on the user's console, fixed in the follow-up
      commit). `SpellLook.Edge` is a `Color` (four normalised floats) and `Color32`'s *constructor* takes
      four bytes; the `Color -> Color32` conversion that does exist is an implicit **operator**, a
      different member from a constructor overload. Fixed by storing the trail colour as a `Color` end to
      end, which removed the whole float->byte ladder. That in turn put `SetColors(List<Color>)` on the
      overload the rest of the project uses - `List<Color32>` appears nowhere in the 378 files.
- [x] **CS0246 `MeshUpdateFlags` could not be found** (found on the user's console, fixed in the
      follow-up commit). The runtime mesh update used `MeshUpdateFlags` + the `(array, start, count,
      flags)` overloads; the type exists in Unity 6000.5.1f1 but `TrailStrip.cs` was the only file in
      the repo that ever mentioned it, so no local use could confirm it and **no check in this repo
      looks for it** - `StaticChecks.ps1` went green, grep went green, reread went green, and it still
      did not compile. Rewritten onto `ChunkMeshGenerator`'s proven idiom: `SetVertices(array)` /
      `SetColors(array)` / `SetTriangles(array, 0)` / `bounds` set by hand **after**, no flags. Full-array
      upload means leftover indices from a longer previous frame are live, so the index tail is now
      `Array.Clear`ed - garbage geometry, not a harmless no-op. See `AGENTS.md` rule 3.
- [ ] **Look at a trail in flight (this is the part no check can judge)** - tapewidth, whether the tail
      fades the way you want, and whether the colour reads as the spell's. It is `Sprites/Default`
      alpha-blended, **not** additive, so it will read softer than the old voxels; say if you want it
      hotter and that becomes one shader/material change, not a rewrite.
- [ ] **Press Numpad8 mid-flight** - expect `1 live, 1 drawn`, `0 undrawn WITH geometry` after the first
      0.3 m, and `0 strips with a Collider`. `undrawn WITH geometry` > 0 means the hand-set mesh bounds
      or a missing camera, and that is the number the headline is built around.
- [ ] **Confirm the trail does not linger after impact** - it should fade out on its own within ~0.35 s.
- [ ] **Fire the fastest spell (Continuous Fireball, 22 m/s)** - this is the one that fills the buffer,
      so it is where `MaxPoints` = 29 is either enough headroom or visibly truncating the tail.
- [ ] **Check a Zone, the static spell bench and a turret bolt** - all three must show **no** trail.

## 1jp. The moving animation, slower again (StrideLength 4.3 -> 5.6 m)

**Status: SHIPPED, unverified.** `Assets/Scripts/Animation/PlayerAnimator.cs` (+ `game-design.md`,
`AGENTS.md`, `PROGRESS.md`, `THINKING.md`). **Verified by grep + reread; no Unity build** (rule 3).
`tools\StaticChecks.ps1` -> 0 candidates; brace/paren deltas 0 on all five files against `git show HEAD:`
baselines. **skills: none applied** - same call as 1jn/1jo, the Unity skills target the editor/CLI and
Animator/import pipelines, and neither can verify a procedural pose.

One field: `StrideLength` 4.3 -> **5.6 m** per full cycle. `cadence = speed / StrideLength` is unchanged,
so the rate moves by the inverse - walk 5 m/s **1.16 -> 0.89 Hz (2.33 -> 1.79 steps/s)**, sprint 10 m/s
**2.33 -> 1.79 Hz (4.65 -> 3.57 steps/s)**. A further 1.23x slower than 1jn, from the same character
speeds. `MaxCadence` (3.2 Hz) left alone: at 5.6 m it first binds at ~17.9 m/s, still clear of the 10 m/s
sprint and the 12.5 m/s +25% build, so it is not what sets the on-screen rate.

**The honest part, which is in the field's own tooltip:** the knob that reads as "slower animation" is
`StrideLength`, and it is *also* the knob that lengthens the ground a single cycle must cover. A slower
cycle has to travel further per cycle to keep up with the same speed, so **slowing the gait increases foot
skate** unless the leg swing amplitude rises with it. The partner knob moves the other way: `MaxCadence`
trades skate for legibility, which is exactly why it is the one you do **not** raise to slow things down.
I shipped the rate change that was asked for and did not silently pair it with an amplitude bump - the
amplitude (`wLegAmp` / `rLegAmp`) is pose authoring, and changing it changes the silhouette, which is a
different request.

### 1jp-status
- [ ] **Walk and sprint and confirm the new rate** - walk 1.8 steps/s, sprint 3.6 steps/s. If it still
      reads too fast, tell me and I will move `StrideLength` again; do not assume another nudge lands.
- [ ] **Watch the feet at a sprint** - this is the trade 1jp makes explicit. Slower cycle, more ground per
      cycle, so any skate you see is the cost of the slower rate, not a new bug. If it bothers you, the
      honest fix is a larger leg swing, not a faster cycle.
- [ ] **Re-check the walk->sprint transition** - two rates that are now closer together in feel
      (0.89 vs 1.79 Hz rather than 1.16 vs 2.33) can make the blend read differently.

## 1jo. Camera further right (0.6 -> 0.9 m) + the path-preview ray no longer trails the camera

**Status: SHIPPED, both unverified.** `CameraModeSwitch.cs`, `SpellCaster.Cast.cs`, `PlayerController.Combat.cs`
(+ `game-design.md`, `AGENTS.md`, `PROGRESS.md`, `THINKING.md`). **Verified by grep + reread; no Unity build**
(rule 3). `tools\StaticChecks.ps1` -> 0 candidates; all three call sites confirmed 3-arg; brace/paren deltas 0.
**skills: none applied** - the Unity skills target the editor/CLI and Animator/import pipelines, and this is
a camera-framing field plus a C# aim-source change. The 1jn skill check had already established that the
installed animation skill cannot verify anything here.

**Part 1 - camera right.** `ThirdPersonSideOffset` 0.6 -> **0.9 m**. One field. It cannot bend a shot: the
projectile aim is a *direction* off the look pivot, never the camera's position, so this is framing only.
First person is untouched (it snaps to the pivot and never enters `UpdateThirdPerson`).

**Part 2 - the ray's endlag.** The mechanism, found by reading the call chain rather than guessing: the
preview is built in `PlayerController.Update` (`HandleMouseLook()` at 225 -> `HandleInteractionKeys()` at 229
-> `UpdatePathPreview`), and the look pivot is written *there*. `CameraModeSwitch` writes the **camera**
transform in `LateUpdate` (lines 137-138 first person, 180-182 third). Unity runs all `Update`s before all
`LateUpdate`s, so anything reading the camera during `Update` - which is exactly what the aim preview did -
got **last frame's** camera. One frame of lag, and it is only visible when the camera is moving fast, which
is why the report was "when moving the camera". The pivot and the camera hold the *same direction* (first
person copies the pivot's rotation outright; third person looks from a point offset along `-pivot.forward`
back to a target offset along `pivot.right`, and the lateral terms cancel), so this is not an aim change -
it is reading the same value one frame earlier.

The fix is in `StraightFlightDirection`, **not** in the preview. 1jm made the preview *call* that helper so
the readout cannot drift from the flight path; patching the preview instead would have re-introduced exactly
the second spelling rule 8 forbids, and the ray would have shown an aim the bullet no longer used. The
helper now takes the look pivot (preferred), then the camera (fallback, for a caster with no player), then
the caller's forward, then `Vector3.forward`. Both call sites pass it; the caster resolves it via the
existing `transform.root.GetComponent<PlayerController>()` idiom.

### 1jo-status
- [ ] **Whip the camera around and watch the ray** - this is the check for the endlag half. The ray should
      now sit under the crosshair immediately instead of trailing it.
- [ ] **Confirm the framing at 0.9 m** and say if you want it wider or narrower; it is one field, one drag.
- [ ] **Watch for the new one-frame relationship the other way** - the ray is now drawn from *this* frame's
      look while the camera settles to it in `LateUpdate`. If the ray and the crosshair ever disagree,
      the candidate is the camera's `SmoothTime` (0.15 s) smoothing the *position*, not the aim.
- [ ] **Re-check a projectile's flight** - the shot itself now uses the same current-frame direction, so a
      thrown bolt is one frame less stale than in 1jm. It should be indistinguishable except during a fast
      camera swing.
- [ ] **Check a beam's ray too** - deliberately NOT changed, see below. It should still show the same
      one-frame trail the projectile ray had.

### Reported, not changed (1jo)
- **The beam preview still reads the camera** (`PlayerController.Combat.cs`, the `bfwd` line). It is
  point-based (`cam.position + cam.forward * Range`, minus the hand), which carries the *identical* skew
  1jm found, and it also keeps the one-frame offset. Fixing only its lag would break the 1jm invariant that
  the preview mirrors the caster, and fixing both means changing the beam's actual aim - the thing 1jm
  deliberately deferred. One edit, but it is your call.
- **~35 other sites read `cam.transform.forward` for aim** and share the identical one-frame offset:
  `ToolManager` (throw/pickup/dig/club rays), `PlayerController.Interactions` interaction rays,
  `FishingController`, `FieldManager`/`FarmingManager`, `InteractionPrompt`. This is a property of the
  write order, not of 1jo, and most of them want the camera's *position* anyway (a ray from the eye), so
  moving the camera write into `Update` would change the camera's own feel. The clean systemic version is
  what 1jo did for one ray: aim from the look source. That is a separate, larger decision.

## 1jn. Player walk/run cycle is slower and its rate follows measured movement speed

**Status: SHIPPED, cadence unverified.** One file: `Assets/Scripts/Animation/PlayerAnimator.cs` (+ `game-design.md`,
`AGENTS.md`, `PROGRESS.md`, `THINKING.md`). **Verified by grep + reread; no Unity build** (rule 3).
**skills: `scenario-unity-animation` applied (informative).** It targets Animator Controllers and the
import pipeline, which this project does not use - `PlayerAnimator` is fully procedural - so it verified
nothing here, but its measured rule *"script speed that disagrees with the clip slides the feet (p50 0.19
vs 1.14 m/s)"* is the governing constraint and it is what the fix is built on. See 1jn's THINKING note.

The report was "the animation is too fast, the player can't really see it, and make it scale with movement
speed". Those turn out to be **one lever, not two**, which is the useful part of this task.

`PlayerAnimator` already measured the character's real planar speed (`speedH`, from the root transform delta)
and already fed it into `norm`, but the gait *rate* was thrown away and re-expressed as
`cadence = 1.8f + norm * 2.0f` Hz. A large constant floor meant a slow walk still cycled at ~2.8 Hz, and a
sprint hit 3.8 Hz - **7.6 steps a second**, since one cycle is two steps. Meanwhile the base speeds are
`MoveSpeed = 5f` and `SprintMultiplier = 2f`, so the walk is a 5 m/s jog and the sprint a 10 m/s sprint.
At 10 m/s *any* honest cadence is fast, which is why the two halves of the request were in tension: you
cannot slow the cycle at a fixed stride and also make it track speed.

The fix is the relationship rather than the numbers. One cycle covers two steps, so
**`cadence = speed / StrideLength`**, with `StrideLength` = 4.3 m per full cycle. That makes the body advance
exactly one stride per cycle, so the feet plant by construction rather than by tuning, and the rate now
scales with real speed for free - including knockback, water and perk multipliers the old ladder never saw.
The longer stride is *also* the slowdown: fewer, bigger steps instead of a slower character.

| case | speed | before | after |
|---|---|---|---|
| walk | 5 m/s | 2.82 Hz = 5.64 steps/s | **1.16 Hz = 2.33 steps/s** (2.4x slower) |
| sprint | 10 m/s | 3.8 Hz = 7.6 steps/s | **2.33 Hz = 4.65 steps/s** (1.7x slower) |

Two things that were load-bearing rather than tidy-ups, both because a **division** is a much more sensitive
consumer of its input than the clamped `norm` it replaced:
- **The speed is now smoothed and teleport-guarded.** One spiky frame (knockback, respawn) used to cost one
  frame of pose; it now lands in a divisor feeding the `_phase` integrator, so it would leave a *permanent*
  phase error. Teleports (>30 m/s) are discarded outright and the rest is low-passed at 12/s.
- **`OnEnable` now seeds `_lastRootPos`.** It was never initialised, so the first `LateUpdate` measured the
  player against `default(Vector3)` and reported its distance from the world origin as speed - invisible
  under the old clamp, a multi-Hz burst under the new one.

### 1jn-status
- [ ] **Walk and check the cycle is readable** - the check for the "too fast" half. It should now be a
      visibly unhurried walk, not a march.
- [ ] **Sprint and check it is faster but still readable** - it should land at roughly half the old rate.
- [ ] **Watch the FEET, not the rhythm** - the invariant this buys is no foot slide, so if the legs skate
      the stride/speed relationship is off. `StrideLength` is the knob for "too fast still"; `MaxCadence` is
      the knob for "skating at speed", and the tooltip says to raise the ceiling *before* the stride.
- [ ] **Check the transition out of idle** - the walk/idle threshold now reads a smoothed speed, so it should
      be less flickery; confirm it still settles promptly when you stop.
- [ ] **Check a knockback / respawn** - this is what the smoothing and the teleport guard exist for, and it
      is the one case that would show as a visible twitch in the legs if the guard is wrong.
- [ ] Note: `norm` still normalises pose blending against the raw `MoveSpeed`/`SprintMultiplier` fields and so
      does not see perk/water multipliers. Left alone (it drives pose, not rate) and recorded in
      `game-design.md`; a stacked build poses slightly short of a full run while its legs keep the right rate.

## 1jm. Projectile spells fly straight along the look direction (no camera-point convergence)

**Status: SHIPPED, aim unverified.** One new shared helper + two call sites. **Verified by grep + reread;
no Unity build** (rule 3).
**skills: none applied** - see 1jm's THINKING note.

The user asked to make player projectiles "flight straight from the player direction" so they stop having
a weird trajectory. `SpellCaster.Execute` used to shoot from the hand toward a **point** `Range` metres in
front of the camera. The error in that is the hand-to-**camera** vector scaled by `1/Range` - negligible in
first person (the camera is at the pivot) and roughly `atan(6.5 / Range)` in third person, which is the
skew the user was seeing. `Projectile` now leaves the cast origin along the camera's **forward**, taken as
a direction, via a new `SpellCaster.StraightFlightDirection(cam, fallbackForward)`.

Two things this deliberately is **not**:
- **Not `origin.forward`.** That is the literal reading of "player direction" and it is wrong: the cast
  origin is the magic hand on the weapon rig, and the body's rotation is yaw-only (`HandleMouseLook` writes
  `Euler(0f, _yaw, 0f)`, pitch lives on the camera pivot), so it is a flat horizontal shot that cannot aim
  up or down.
- **Not a change to instant/beam/ground deliveries.** Instant and beam carry the *identical* skew and are
  reported rather than changed, because the request was scoped to projectiles. Extending it is one edit
  away if you want it.

`FireProjectile` and `SpellEffect` already consume the vector directly (`pos += fwd * 0.5f`,
`Quaternion.LookRotation(fwd)`), so nothing downstream needed touching. The aiming preview
(`UpdatePathPreview`) now **calls** the same helper instead of re-deriving the formula, and no longer needs
a camera reference at all.

### 1jm-status
- [ ] **Cast a projectile in third person and check the path is straight** - this is the check for 1jm. Note
      that **1jl also landed in this session and also changed the shot's appearance** (camera moved 0.6 m
      right), so if the shot looks different than expected, the two candidates are 1jm's aim change and
      1jl's camera - a good 1jm result implicates this file, a bad one sends you to `CameraModeSwitch`.
- [ ] **Does the crosshair still line up with the flight path?** The shot now leaves the hand parallel to
      the view ray rather than converging on a point, so in third person it starts left of the crosshair and
      converges toward it with distance. If that reads as mis-aimed, the alternative is spawning the
      projectile from the camera instead - a different design, not a bug in this one.
- [ ] **Vertical aim** - confirm you can still hit something above and below you. This is the regression
      `origin.forward` would have caused and the reason it was rejected.
- [ ] **Path preview matches the flight** - arm a projectile spell and compare the previewed line to where
      the bolt actually goes. They now read one helper, so a mismatch here is a real defect, not drift.
- [ ] Wheel-cast magic (`fast`) and a skill-granted `SpellCastEffect` cast both route through the same
      `Execute`, so they take the new aim too - worth one cast each.

## 1jl. Third-person camera sits over the player's right shoulder

**Status: SHIPPED, framing unverified.** One field + one local in `CameraModeSwitch`. **Verified by grep +
reread; no Unity build** (rule 3). The framing amount is a play-test value, not a measurement.
**skills: none applied** - see 1jl's THINKING note.

The user asked to "move 3rd person camera abit to the right". `CameraModeSwitch` gained
`ThirdPersonSideOffset = 0.6f` (negative = left, `0` = the pre-1jl centred look), applied as
`pivot.right * offset` to **both** the camera position **and** the look-at point.

**The part worth knowing:** offsetting the position alone would have moved *nothing* on screen.
`UpdateThirdPerson` ends in `LookRotation(pivotPos - camera.position)`, so a position-only offset is
rotated straight back out to keep the pivot centred - a view-angle tweak wearing a framing change's
clothes. Moving the look-at too makes the view direction algebraically unchanged (the lateral term cancels
out of both sides of `lookTarget - desired`) and slides the character left of centre, which is the actual
request. First person is untouched: its branch snaps to the pivot and never enters `UpdateThirdPerson`,
so the field has exactly two readers and both are third-person.

`ThirdPersonSideOffset` is a plain initializer with **0** hits in the one live scene, and the component is
`AddComponent`ed at runtime - so `0.6f` is the shipped value, same reasoning as 1jf's `StartInFirstPerson`.

### 1jl-status
- [ ] **Is 0.6 m the right "a bit"?** It is roughly a shoulder width on this body. It is one serialized
      field on purpose - dial it in the inspector, no code edit.
- [ ] **Does a wall beside the player now pull the camera in?** `toCam` starts at the pivot and the
      lateral term is now inside the direction it sweeps, so yes by construction. Check a corridor.
- [ ] **Player model placement** - the character now sits left of centre; check it does not clip the
      left screen edge at narrow aspect ratios.
- [ ] First person still snaps to the pivot (static claim, `Update`'s branch never calls
      `UpdateThirdPerson`; worth one glance anyway).

## 1jk. Audit: no CS0117 siblings of the 1jj split - and three ways a reference sweep lies

**Status: AUDIT COMPLETE, 0 real findings. Docs corrected.** Read-only sweep; the only file changed is
`AGENTS.md`. **Verified by grep + reread; no Unity build** (rule 3).
**skills: none applied** - a static analysis + doc audit inside this repo.

`1jj` fixed 4 CS0117 sites that `75fd44d` left behind. Rule 17's new bullet says the sweep's denominator is
*every caller*, so the question this task answers is: **were there more?**

### The sweep, and its verdict
Every qualified `Owner.Member` reference in all **378** `.cs` files under `Assets\Scripts`, checked against
the names declared by that owner:

| Owner | Declared | Distinct referenced | Total refs | Unresolved |
|---|---|---|---|---|
| `MapBuilder` | 58 | 36 | 204 | 1 - a doc comment |
| `WorldBuilder` | 223 | 13 | 85 | 7 - 5 extractor, 1 inherited, 1 a comment |
| `PlayerModelBuilder` | 3 | 4 | 17 | 1 - a comment |

**0 real defects.** The `MapBuilder` hit that is not the one 1jj fixed (`NewWorldTestGround.cs:750`,
`MapBuilder.Build`) is the wildcard `MapBuilder.Build*Npc` inside an XML doc comment. So the 1jj family is
closed: there were exactly 4 broken sites, and they are all fixed.

### Four instrument defects, each of which first produced a wrong answer
This is the real content of the task. Every one of these made the output look like a clean sweep or a real
finding when it was neither, and only reading the actual file caught them.
- **The ref table never got filled.** First run printed "0 referenced, 0 unresolved" - because
  `New-Object System.Collections.Generic.ArrayList` does not exist (it is non-generic), so every `$refs[$n]`
  assignment threw and the table stayed empty. **A false zero from a broken instrument is indistinguishable
  in the output from a clean sweep.** Fixed run: 36 distinct / 204 refs, which is a number worth reading.
- **Comments are not references.** `WorldBuilder.SpawnRockDebris` read as 3 outside calls to a `private`
  method (CS0122) and is in fact 2 `//` comments and 1 `<see cref>`. It is referenced for what it *looks*
  like, not called.
- **Inherited members have no local declaration.** `WorldBuilder.Instance`, used **55** times, is declared
  in none of the twelve partials: `WorldBuilder : MonoSingleton<WorldBuilder>` inherits it from the live
  `MonoSingleton<T>`. A per-file extractor cannot see a base class.
- **A nested type with its brace on the next line defeats a one-line regex.** `public class FieldState`
  followed by a newline matches nothing expecting a trailing `(`/`{`/`=`, so 5 save-record types
  (`FieldState`, `BuildingState`, `FieldSaveData`, `BuildingSaveData`, `MansionBlueprintSaveData`) all read
  as missing. They are declared in `WorldBuilder.cs` and `WorldBuilder.Persistence.cs`.

Also worth recording: two of my own *verification* commands guessed file paths that do not exist
(`Assets\Scripts\Magic\MagicProjectileModelBuilder.cs` and `Assets\Scripts\World\Chunks\WorldStreamer.Deform.cs`;
the real paths are `Models\Magic\` and `World\Streaming\`). Those two errors produced **"Cannot find path"**
rather than a false claim, which is the benign failure mode - but the same guess reported as a hit would have
been a finding about a file that is not there.

### Rule 18's file counts were wrong, and the wrongness cancelled out
Measuring per folder: `Cutscenes/` **11** files (only **8** are `Ending*`; the rest are the base plus
`Driving` and `Helpers`), `WorldBuilder/` **12** partials, `MapBuilder/` **9** partials. Rule 18 said "the ten
`CutsceneManager.*` endings, the twelve `WorldBuilder.*` partials, the ten `MapBuilder.*` partials".
So one number is right, and the two that are wrong err in opposite directions - **10+12+10 and 11+12+9 are
both 32**, which is why the sentence survived 30-odd tasks: a reader who checked the total saw it agree.
It was also wrong at its own origin commit `aae400b`, so this was never rot, it was a bad count.
Corrected in place, with the cancellation named so nobody "fixes" one of the two back.

**No pending play-test** - this task changes no behaviour and no gameplay code.
## 1jj. `MapBuilder.BuildSeatedPlayerModel` - the call sites 75fd44d never finished, incl. 3 in Legacy

**Status: READY FOR PLAY-TEST.** Four CS0117 sites from the same class split 1ji finished. One live line
plus **3 lines inside the `Legacy/` fence, fixed on the owner's explicit authorisation** and recorded in
AGENTS.md rule 18 so a later reader files it as an exception, not a violation.
**Verified by grep + reread + `tools\StaticChecks.ps1`; no Unity build** (rule 3).
**skills: none applied** - a C# edit reviewed by a human inside this repo; the Unity skills drive an editor
or `-batchmode`, which rule 3 forbids.

| File | Line(s) | Was | Now |
|---|---|---|---|
| `Interactions/PlayerSitController.cs` | 33 | `MapBuilder.BuildSitPlayerModel` | `PlayerModelBuilder.BuildSitPlayerModel` |
| `Legacy/Cutscenes/CutsceneManager.Driving.cs` | 46, 172, 507 | `MapBuilder.BuildSeatedPlayerModel` | `PlayerModelBuilder.BuildSeatedPlayerModel` |

**This is the call side of 1ji's bug, and it was missed by the commit that caused it.** `75fd44d` moved the
player-model class out of `MapBuilder` and rewrote `MapBuilder.BuildPlayerModel` ->
`PlayerModelBuilder.BuildPlayerModel` at **10 sites in 10 files**. It *touched* `CutsceneManager.Driving.cs`
and `CutsceneManager.Helpers.cs` and changed **no `Build` line in either** - because those two hold the
*seated* variants and the sweep searched for the one name that had moved. So the fence was crossed 9 times
by that commit family and `Driving.cs` alone was left calling a member that no longer exists.

**Enumerate the moved class's public surface, not the symbol the console quoted.** `PlayerModelBuilder`
exposes exactly 3 public members, and all three had to be accounted for separately:
`BuildPlayerModel` (10 sites, done by `75fd44d`), `BuildSeatedPlayerModel` (3 sites, this task) and
`BuildSitPlayerModel` (1 site, this task). A sweep keyed on the reported name would have found 1 of 3.

**The Legacy decision, stated rather than assumed.** 3 of the 4 sites are in read-only `Legacy/`, and
rule 18 forbids editing it in either direction. I stopped and asked rather than picking, because the two
plausible fixes are opposites: qualify the 3 lines (completes the interrupted sweep, but edits frozen
old-game source) or add a `MapBuilder` forwarder (leaves Legacy byte-identical, but rule 17 forbids a
forwarder to a Legacy host and it hides the sweep gap behind a working build). The owner chose the first.
It is also the one consistent with **8 sibling files in the same folder** already calling
`PlayerModelBuilder.BuildPlayerModel`.

**Verification, with the instruments named honestly:** 0 stale `MapBuilder.Build*Model` references remain
tree-wide (the pattern was checked against a known-zero control first - an earlier version of it
`Build(Player|Seated|Sit)Model` could not match `BuildSeatedPlayerModel` and reported a false 0). The diff
is **exactly 4 lines**, prefix-only, with the Legacy hunk 3 added / 3 removed and no other content touched.
Balance delta **zero** on both files (`PlayerSitController` 12/12 braces 43/43 parens; `Driving.cs` 68/68 and
269/269, identical to HEAD). Both files kept their encoding - `Driving.cs` has a UTF-8 BOM, `PlayerSitController`
has none, and both pass a strict UTF-8 decode. **Neither file is in `StaticChecks.ps1`'s `$files` list**, so
the manual balance count is the only instrument that touched them; the script still reports 0 candidates
project-wide.

**Play-test:** the driving cutscene still seats the player in the car at all three points (intro, mid, and
the later repeat) - that is the one line count that proves the 3 Legacy edits, since a wrong signature
there would build nothing rather than error at runtime. Sitting in a chair still spawns the chair pose. The
player model and race proportions are unchanged from 1ji.
## 1ji. `MakePart` did not exist in PlayerModelBuilder - a `partial` split that git recorded as unchanged

**Status: READY FOR PLAY-TEST.** The user pasted the Unity error (`the name 'MakePart' does not exist`
in the player model builder). Fixed by qualifying two inherited references; no behaviour change.
**Verified by grep + reread + `tools\StaticChecks.ps1`; no Unity build** (rule 3), so the compile itself
is still owed - paste anything else the console reports.
**skills: none applied** - a C# edit reviewed by a human inside this repo; the Unity skills drive an
editor or `-batchmode`, which rule 3 forbids.

`Assets/Scripts/Models/Player/PlayerModelBuilder.cs`, 89 call sites, **all of it a prefix**:

| Was | Now | Sites |
|---|---|---|
| `MakePart(` | `MapBuilder.MakePart(` | 86 |
| `ActiveGender` | `MapBuilder.ActiveGender` | 3 |
| `PlayerGender` | *unchanged* | 3 |

**The cause is old and the diff hid it.** `75fd44d` moved `MapBuilder.PlayerModels.cs` to
`Models/Player/PlayerModelBuilder.cs` and changed `public static partial class MapBuilder` to
`public static class PlayerModelBuilder`. A `partial` resolves bare names against its *siblings*, so
`MakePart` - declared once in `MapBuilder.cs` - used to resolve without a prefix and stopped the instant
the class stopped being partial. Because the body genuinely did not change, **git recorded that file as
R100, byte-identical**: the one artifact rule 17 says must agree across a move reported "nothing changed"
about a file that could not compile.

**Rule 18 shapes the fix.** `MapBuilder` is quarantined read-only, so the repair is entirely on the live
side and the direction is the sanctioned one (live -> legacy). The alternative - a forwarder on the host -
is a `Legacy/` edit, and the repo already had the convention to copy: `SaveManager` and
`UIManager.MainMenu` both write `MapBuilder.ActiveGender`.

**Sweeping found two things the console did not name**, which is the transferable part:
- `ActiveGender` (3 sites) would have been the *next* compile error - Unity prints what you look at, not
  what is broken.
- Two candidates were **false positives, and both are traps worth writing down**: PowerShell's
  `-contains` is case-insensitive, so this file's own local `headScale` matched MapBuilder's NPC field
  `HeadScale`; and `PlayerGender` is a **top-level enum in the global namespace** (`public enum
  PlayerGender` one line *above* the class), which resolves unprefixed from anywhere - "declared in that
  file" is not "member of that class".

**Same bug class checked and cleared elsewhere.** `MagicImpactModelBuilder`, `MagicProjectileModelBuilder`
and `MagicWeaponModelBuilder` were split out of their hosts the same way by 1ja/1jb. My first sweep of
them reported "clean" **while reading zero files** - an inner `$_` shadowed the outer one, so every host
resolved to nothing and the scan had nothing to compare against. Re-run with a positive control (each host
resolved to 1 file and yielded 3/6/5/33 members, so a hit was possible), it reports **no bare
cross-class references**: those extractions were done correctly and needed no change.

**Balance: 22/22 braces and 509/509 parens both before and after** - zero delta, because the edit only
inserted a prefix. Reported as a delta rather than as "balanced", since that is what the evidence supports.

**Play-test:** the player model, the cutscene car/chair fits and the race proportions render exactly as
before (they are a pure rename of the call target). Confirm in the console that no further CS0103 remains
- the next error after `MakePart` was `ActiveGender`, and both are now qualified.
## 1jh. Projectile impacts had no debris - grey rock chips thrown forward, not up

**Status: READY FOR PLAY-TEST.** A new `WorldStreamer.SpawnImpactRockDebris` emitter plus a
`TerrainDeformer.ImpactRockDebris` entry point, and a latent collider bug in the shared cube template.
**Verified by grep + reread + `tools\StaticChecks.ps1` -> 0 candidates; no Unity build** (rule 3), so the
visual read is still owed.
**skills: none applied** - a C# edit reviewed by a human inside this repo; no installed skill governs
that, and the Unity skills target driving an editor or `-batchmode`, which rule 3 forbids.

Shipped as its own commit from the same request as 1jg, because the two halves are **different repairs**:
1jg restored something that was deleted, and this restores something that was only **switched off**.

**Why not just flip `emitDebris` back to true.** `1gb` (`c5d0c31`) made projectile impacts pass
`emitDebris:false` for one stated reason: the burst read as *"3 objects floating up then disappear"*.
That is a complaint about **shape and motion**, not about debris existing at impacts - so re-enabling it
would ship the thing that was complained about, and adding the chips alongside it would leave the complaint
half-answered. `SpawnCraterDebris` is also *excavation* debris: stratum-tinted by `TerrainBandColor`,
sized for a dug pit, lifted +2.5..+5 m/s. Un-suppressing it is the wrong fix twice over.

So 1jh is a separate emitter, and the shared excavation behaviour is **untouched** - `DeformAt` still
guards `if (shape == TerrainShape.Crater && emitDebris)`, so tool digs and zone/storm/summon strikes are
byte-for-byte what they were. The dent a projectile carves is unchanged; only who supplies the debris.

**The four differences, one per part of the critique:**
- **Thrown forward.** Biased along the projectile's own `_dir`, **flattened** to horizontal so a bolt
  arriving from above cannot turn the throw skyward again - that flattening is the part that would
  otherwise be missed, since "not up" is easy to state and hard to keep true for every arrival angle.
- **Short life (1.4 s vs 2.5 s).** These chunks carry no collider, so they fall *through* whatever they
  were knocked out of; the old 2.5 s spent most of its life sinking out of sight rather than reading.
- **Grey rock, not stratum colour** - `Color.Lerp(Color.gray, Color.black, …)`, matching the world's
  breakable-rock debris. A spell shattering masonry is not a shovel full of dirt.
- **Count and size scale with the impact radius** (2–8 chunks, size × `clamp(radius·0.75, 0.7, 1.6)`),
  so a charged bolt throws more and bigger rather than every impact looking identical.

**Additive to the impact sphere, deliberately.** `SpellImpactFx.Spawn` is the spell's *identity* (its own
per-school impact family, §3.7) and the chips are the *world reacting*. Rule 13's asymmetry test - do the
two adjacent lines share an identity? - says no, so both stay. Spawned at the **hit point**
(`transform.position`), not the probed ground point, so a wall strike throws chips too.

**The latent collider bug, found by the new caller rather than by looking for it.** `SharedDebrisCube`
destroyed its Collider with plain `Destroy`, which is deferred to end of frame - and the first debris
burst is normally emitted in the very frame the template is built, so that frame's `Instantiate` **cloned
a collider onto live debris**. It has been there since `1du`. Survivable for rare tool digs; not a good
bet the moment an emitter goes behind every projectile impact, which is exactly what 1jh did - so the new
caller exposed the old bug rather than merely inheriting it. Now `DestroyImmediate`, matching what 1jg did
in its own template. A convention that only ever finds new code is not yet a convention (AGENTS rule 19).

**Play-test items:** chips read as rock knocked off the impact rather than as objects floating; a
ground strike throws forward and dies in about a second; a wall strike throws chips too; a charged cast
visibly throws more/bigger than an uncharged one; the chips do not spoil the school-coloured impact
sphere (this is the one that could read as "too busy"); tool digs and zone strikes look exactly as before.
## 1jg. Magic projectiles flew through a silent, trail-less world - a pooled voxel exhaust

**Status: READY FOR PLAY-TEST.** A new `ProjectileTrail` emitter driven from the flight loop in
`SpellEffect.Update`, plus a **field rename's ghost** found on the way (`SpellLook.Secondary` does not
exist). **Verified by grep + reread + `tools\StaticChecks.ps1` -> 0 candidates; no Unity build** (rule 3),
so the visual read is still owed.
**skills: none applied** - the artifact is a C# edit reviewed by a human inside this repo; no installed
skill governs that, and the Unity skills target driving an editor or `-batchmode`, which rule 3 forbids.

The request was "spells fly with no trail, and collisions have no debris". Both halves are **restoring a
look that was deliberately deleted**, which is the one kind of visual request where the history is the
specification. Two findings from reading it:

- The exhaust trail was not removed by neglect. `1155a6e` replaced a `TrailRenderer` with a particle
  exhaust, and `1eb` (`5381e8b`) deleted it **at the user's request** - the bodies became fully static.
  `game-design.md` records this as an invariant, so 1jg had to rewrite that sentence rather than add to
  it: the honest statement is that the *body* is static and the *trail* is a separate, world-space thing.
- The debris half was never deleted, only switched off: `1gb` (`c5d0c31`) made projectile impacts pass
  `emitDebris:false`, because `SpawnCraterDebris` reads as "3 objects floating up then disappear". That
  critique is about *shape and motion*, not about debris existing, so 1jh is a new emitter rather than
  an un-suppression of the old one. 1jg and 1jh are therefore separate commits.

**The field that does not exist.** The plan called for the trail colour to be `SpellLook.Secondary`,
documented in two places as "the secondary colour for two-tone FX (rim, **trails**, shards)". It is
declared **nowhere** - the member is `SpellLook.Edge`, and both doc comments had been calling it
"Secondary" for as long as it has existed. Grepping the *comment* returned two hits and a confident
colour; grepping the *declarations* returned none. `SpellLook.Resolve` was already called in
`SpellEffect.Initialize`, so no signature changed - but the same grep would have been the only thing
between this and a CS1061. Both comments are now corrected, which is the real fix: the next reader who
greps "trails" finds `Edge`.

**Where the emission lives, and why it is not the model builder.** `MagicProjectileModelBuilder` is a
one-shot shape factory with no per-frame behaviour, and it is shared by two consumers that would both
have looked broken: `NewWorldTestGround`'s spell band draws every castable spell as a **motionless
pedestal**, and `SpellCaster.DecorateProjectile` is a summoned turret's bolt, which never flies. A trail
emitted there hangs a row of cubes on a pedestal that never moves - which is, precisely, the complaint
1gb was filed for, rebuilt in a new place. Driving the emission from `SpellEffect.Update` gets the gate
for free: a Zone resolves and destroys itself in `Launch` *before* `_launched` is set, and neither the
bench nor a turret bolt ever reaches `Update`, so only genuinely flying casts trail.

**No `Update` on the voxels.** The old `ParticleSystem` bought its taper with a particle system, and the
removed one was cut for cost. These voxels have no component: the taper is **age** (each is recycled
0.35 s after it was left, so the tail is always the part about to disappear), the spacing is
**distance**-gated (0.3 m of flight, not one per frame) so speed does not change the look, and per-emit
randomness in rotation and size keeps the stream from reading as a mechanical dotted line. Colour is
written with `Renderer.material`, which caches its per-renderer instance on the pooled object - so the
tint allocates once per pooled voxel for its whole lifetime, and there was no reason to introduce
`MaterialPropertyBlock` (which has zero uses in this project).

**Play-test items:** trail reads as exhaust rather than a dotted line at both a fast bolt and a slow one;
the `Edge` tint is visible against each school's body colour; nothing trails on the static bench or a
turret bolt; a homing missile's trail follows the curve instead of cutting the corner; no collider snag
on the player's controller while a bolt passes overhead.

## 1jf. The game opened in first person - it now opens in third person

**Status: READY FOR PLAY-TEST, with one named risk.** A **one-field default flip**
(`CameraModeSwitch.StartInFirstPerson` -> `false`), no framing change and no other code touched.
**Verified by grep + reread + `tools\StaticChecks.ps1` -> 0 candidates; no Unity build** (rule 3), so the
visual read is still owed and **one specific failure mode is what the read is for** (below).
**skills: none applied** - same reasoning as 1jd/1je: no installed skill governs a serialized-default edit
inside one repo, and the Unity skills target driving an editor or `-batchmode`, which rule 3 forbids.

The request ("change player camera angle to 3rd view") read like new work, so the first question was whether
third person *worked at all*. **It already did, and had for some time**: `CameraModeSwitch` toggles both ways
on **F5**, with distance/height/collision/smoothing all authored. So this task is not a camera system - it
is a decision about **which of two working views the game opens on**, and the only honest risk is that the
rarely-used one has an unexercised failure nobody had to look at until now.

| Finding | Evidence | Consequence |
|---|---|---|
| `CameraModeSwitch` is added at runtime, not serialized | `PlayerController.Camera.cs` `SetupPlayerCamera` does `AddComponent<CameraModeSwitch>()`; **0** occurrences of `CameraModeSwitch`/`StartInFirstPerson` in `SampleScene.unity` | the C# default **does** govern - a scene override would have made this edit a silent no-op |
| the flip is self-contained | `StartInFirstPerson` has exactly **1** reader (its own `OnEnable`); `CurrentMode`/`IsFirstPerson` have **0** external readers | the mode is render-side only; no gameplay consumer to update, and none should have been reading it |
| a *second* third-person class is dead | `Player/Controller/ThirdPersonCamera.cs`: 1 declaration, **0** code refs (only a `ScreenShake` comment mentions it), GUID in **0** assets | `CameraModeSwitch` is the camera owner. Left alone - deleting it is a rule 14 task, not a camera task |
| cutscenes are unaffected | `CameraModeSwitch.LateUpdate` writes the camera in **both** modes, so `CutsceneManager`'s one-shot writes were already losing to it | the default flip changes nothing here; not a new conflict |

**Decisions, with the reason:**

- **Framing left exactly as authored** (6.5 m back, 2.6 m up, dead-centre, no shoulder offset). The user
  asked to change the *view*, not the framing, and a framing guess is a play-test cycle I cannot verify
  without an editor. Offered as a follow-up rather than bundled in.
- **`ThirdPersonCamera.cs` reported, not deleted.** It is a genuine 1iw-shaped finding - a file whose
  folder says "camera" that no code calls - but removing it is its own task with its own sweep.
- **Fixed a wrong tooltip while in the file (rule 8).** `ThirdPersonY` was documented as "vertical offset
  **above the pivot**", which is not what the code does: `UpdateThirdPerson` adds
  `up * (ThirdPersonY - pivot.localPosition.y)` to the pivot's **world** position, so the two cancel and
  the camera lands at feet + `ThirdPersonY`. The number is right (2.6 m up a 1.8 m character reads as
  over-the-shoulder), the sentence about it was not - and the play-test below depends on the reader
  interpreting 6.5/2.6 correctly. Also stated there that raising it does **not** raise the look-at point,
  which is the natural next wrong move.
- **No comment added about the collision risk** (see `THINKING.md` §1jf): it is an unverified hypothesis,
  and a comment asserting it would be a claim nobody measured.

### 1jf-status
- **Shipped:** commit pushed to `origin/main`; files touched: `Assets/Scripts/Player/CameraModeSwitch.cs`,
  `game-design.md`, `PROGRESS.md`, `THINKING.md`, `AGENTS.md`, `TREE.md`.
- **Verified:** grep + reread. `StartInFirstPerson` 1 reader; `CurrentMode`/`IsFirstPerson` 0 external
  readers; `ThirdPersonCamera` 0 refs + 0 asset GUID refs; 0 scene occurrences of the component or field.
  Paren/brace **delta** 2/2 on `CameraModeSwitch.cs` (both pairs inside the new tooltips - baseline was
  15/15 braces, 73/73 parens). `tools\StaticChecks.ps1` -> **0 candidates**.
- **Pending play-test (the point of this task):** press play and confirm the camera is **actually behind
  the player**. The named risk is that it will not be, and the failure is silent: the third-person
  collision `SphereCast` **starts at the pivot**, which is 1.5 m up and therefore **inside your own
  `CharacterController`** (radius `>= 0.3`, height `>= 1.8`, centre `y = height/2 = 0.9`), with
  `CollisionMask = ~0` (everything). If that own collider registers, `_cachedFinalDist` becomes
  `max(hit.distance - 0.2, 0.1)` and the camera parks **0.1 m from the pivot** - i.e. third person renders
  as first person and this commit looks like it did nothing. **Decision on this: play-test first, do not
  pre-emptively exclude the player** (the user chose this explicitly). Read it as: camera at the player's
  head = the trap fired, exclude the player's own colliders and say so; camera 6.5 m back = clean.
- **Pending play-test, lower risk:** (1) the camera does not clip the ground on slopes
  (`CollisionCheckInterval = 0.1f` reuses a cached clamp, so a wall can be entered up to 0.1 s late);
  (2) the player model **is visible** (layer 6 is re-added to the culling mask on entering third, and
  arms stay visible in both modes); (3) the held weapon sheaths in casual and draws in combat, since
  `SetMode` calls `ReApplyWeaponPose`; (4) F5 still returns to first person and the model is culled again.
- **Follow-up, not in this task:** closer/tighter framing, or an over-the-shoulder lateral offset, if the
  default framing reads wrong in motion.

## 1je. The summoned ally was a bare cube - a real body, and a bench you can actually look at

**Status: READY FOR PLAY-TEST.** A deliberate **addition plus one required handle change**, not a move:
`SummonedAlly.Spawn` created `GameObject.CreatePrimitive(PrimitiveType.Cube)` and set one material on it,
so the only summon body in the game with no model file was the one you could only see by fighting.
**Verified by grep + reread + `tools\StaticChecks.ps1` -> 0 candidates** (including check 8, which read
the new lane key with no script change); **no Unity build** (rule 3), so the visual read is still owed.
**skills: none applied** - same reasoning as 1jd: no installed skill governs a C# model/component edit
inside one repo, and the Unity skills target driving an editor or `-batchmode`, which rule 3 forbids.

The request was the 1iz complaint a third time, and the shape kept repeating: **the feature existed and
had been shipping; it had no name and no home.** 1ij named the projectile models, 1iz named the weapon
models, 1jd named nine spell bodies - and `SummonedAlly` was still four lines of cube inside a
gameplay component, reachable only from `SummonEffect.Execute` (`ClassEffect`) and `RaceEffect`
(**2 call sites**, both inside live combat). A visual that exists only inside a live cast has no
acceptance readout (rule 13), so the lane was part of the task, not a follow-up.

| Change | Where | Why |
|---|---|---|
| `SummonModelBuilder.BuildAlly` (new) | `Models/Magic/SummonModelBuilder.cs` | the body, beside the totem and familiar circle it belongs with |
| `SummonModelBuilder.AllyBody` (new) | same | `Root` + `Renderer[]` - one record, not six out-params (rule 17) |
| `SummonedAlly._renderer` -> `Renderer[]` | `Combat/Skills/SummonedAlly.cs` | **forced**: despawn alpha and the death disable both went through one renderer, so a multi-part body needs the whole set or the head and pods stay standing while the shell vanishes |
| `AllyColor` (new, `public static readonly`) | `SummonedAlly` | the colour it always used, now one named constant the bench also reads, so there is no second spelling |
| `SpawnSummonModels` + `EnableSummonModels` + `SummonModelKey` | `Opt/NewWorldTestGround.cs` | the acceptance readout; rule 4's opt-in lane, re-pressable |

**Decisions, with the reason, so the next reader does not re-open them:**

- **It hovers; it does not walk.** `SummonedAlly` moves by `MoveTowards` + `Face` and has no animator,
  no rig and no walk cycle, so a bipedal skeleton would slide and read as broken. A hovering construct
  reads correctly while translating and rotating, which is all the component can do. Six parts: ground
  ring, capsule shell, sphere head, front core, two splayed shoulder pods.
- **Two colours from one input, derived in the builder.** Trim is RGB x0.55, core is RGB x1.15 clamped
  with alpha 1. A caller still passes exactly one colour, so rule 13's "resolved in one place" holds.
- **Size: head top 2.06 authored, 1.65 m live.** A Unity capsule is 2 units tall *before* scaling, so
  the shell spans 0.5-1.6 and the head caps it. The component's existing `localScale = 0.8f` is
  **untouched** - the model was sized to fit the line that was already there.
- **The bench mounts the builder at 1.0, not 0.8**, and the comment says so: a bench that silently
  rescales is how "the model looks wrong in game" becomes a two-session argument.

**Three things deliberately NOT done** (each is a gameplay or behaviour change riding on a model task):

- **No collider was added.** The cube's collider was destroyed at spawn and nothing replaced it, so the
  ally has **never** been targetable by enemies. Adding hitboxes would change combat. The class-level
  `[RequireComponent(typeof(SphereCollider))]` is added by `AddComponent` and never removed - a
  **pre-existing contradiction**, recorded here and left alone.
- **No walk cycle / no bob.** Same reason it hovers: animation is behaviour, and the component's
  `Update` is not this task's surface.
- **No ground snap.** The component `MoveTowards`es in a straight line, so **on sloped ground the ring
  will float or sink**. Pre-existing (the half-buried cube hid it; a ground-contact part does not),
  newly *visible* - rule 7's "a feature change can expose a dormant defect". Left for a movement task.

### 1je-status

- Play-test still owed in Unity (rule 3 runs no build here): the ally's silhouette and scale in-world
  (1.65 m, hovering, ring on the terrain), that despawn alpha now takes **all six** parts, and that death
  disables **all six**.
- The lane: enable `EnableSummonModels` and press **Numpad1**. Band `z = PlatformCenter.z + 11f`, three
  pedestals 4.5 m apart, clear of the magic grid (z +/- 1.5), the farming band (z + 18+), the dummies
  (z - 18), the NPC row (z - 0.18*Size) and the +/-0.42*Size rack / tool-kit lines. Re-pressable - each
  run clears the previous row.
- Key choice is **measured**, not inherited: `Key.Numpad1`, `.numpad1Key` and `[Key.Numpad1]` all read
  0 in `Assets\Scripts`, with **F1 as the positive control** proving the property-name spelling was
  actually searched (rule 7). Check 8 now reports 5 lane keys and no second binding.
- **Instrument caveat found while verifying:** `NewWorldTestGround.cs` is **not paren-clean at HEAD**
  (1216/1215 - an unbalanced `(` inside a `Debug.LogWarning` string literal), so its balance count can
  never be read as pass/fail; only the **delta** is meaningful (this task's delta: 61/61).


## 1jd. Nine spell/skill bodies had no model file — one named builder each, and the comparator that proves it

**Status: SHIPPED.** A pure move: no behaviour change, no signature change on any public API. **9** new
builders (8 in `Models/Magic/`, 1 in `Models/`) + **9** `.meta` (**377** `.cs` / **377** `.cs.meta`
parity under `Assets/Scripts` = 368 + these 9; **550** `.meta` GUIDs with **0** duplicate groups), and a
new rule-17 instrument `tools/Compare-MovedModel.ps1`.
**Verified by grep + reread + `tools\StaticChecks.ps1` -> 0 candidates + the move comparator -> 24
blocks / 24 identical / 0 unresolved; no Unity build** (rule 3). **skills: none applied** — no installed
skill governs a C# file move inside one repo; the DCC-side and `-batchmode` Unity skills were
deliberately not loaded (rule 15's "match the skill to the artifact").

The request was 1iz's complaint one layer down: *"where is the code that builds the casting circle?"*
meant reading `CastingCircle.cs`. 1jb had named the projectile and impact models and left a note that
`SkillFx` and the casting circle were "the next candidate" — **nine**, not two. What moved, and what
deliberately did not:

| Builder | Out of | Left behind on purpose |
|---|---|---|
| `SpellBeamModelBuilder` | `SpellBeam` | `PulseVisual`'s funnel flare + debris orbit |
| `SpellZoneModelBuilder` | `SpellZone` | the zone's lifetime |
| `SpellStormModelBuilder` | `SpellStorm` | strike scheduling |
| `SummonModelBuilder` | `SpellSummon` | the pulse |
| `SkillFxModelBuilder` | `SkillFx` | nothing — slash/ring are one-shot |
| `CastingCircleModelBuilder` | `CastingCircle` | per-frame pulse + rotation |
| `AoeAimPreviewModelBuilder` | `AoeAimPreview` | the pulsing |
| `CcZoneFxModelBuilder` | `CCZone` | nothing — one-shot |
| `WeaponProjectileModelBuilder` | `RangedWeaponBehavior` | aim |

**The dividing line is shape vs. lifetime.** Transforms that only need building moved; anything that
decides *where a piece is next frame* stayed, because that is behaviour, not a shape. So
`SkillFx.SlashFlash`/`RingFlash` keep their exact public signatures (22 and 8 call sites, most of them
non-spell classes) and gained a delegate — a move that renames the public entry point would have been a
second, unrelated edit riding along.

### Four return records, because a builder must not remember its own output

`LineBody`, `TipOrb`, `Circle` and `Piece` exist so the *component* keeps ownership of the handles. The
alternative — a builder remembering "the last ring I built" — is a second owner of a transform, and
1jb already had to unpick exactly that shape when `MagicImpactModelBuilder` handed back three *parallel
lists`. One record per piece makes index-alignment unrepresentable. `SummonModelBuilder.Body` is the
same idea for the totem/familiar pair. `SpellBeam`'s tip orb is shared by the cone **and** the line, so
it stayed outside the branch: folding it into the line builder would have left the cone's tip built but
never pulsed.

### Two literals that moved OUT, declared rather than quietly tolerated

`CCZone`'s pale-blue `Color` stays in the component — the colour is the caller's decision, not the
model's (rule 13's "ask what would catch the bug if that value were wrong": here the answer is the
component, so the model must not own it). And `SpellStorm`'s inline `new Vector3(0.1f, 3.2f, 0.1f)`
became the named `BoltScale`, which also fixed a latent duplication: the fader was re-deriving its start
scale from a second copy of the same three numbers. **8** literals in total are declared in the
comparator as `Carry`, so the diff reads as a decision instead of as noise.

### The comparator needed four attempts, and the failures are the point

`tools/Compare-MovedModel.ps1` compares each moved block's **literal stream** (numbers + strings, in
order) between `HEAD` and the working tree, with a `-Mutate` control. Rule 17 is why it exists at all:
with no compiler, the comparator *is* the compiler. Getting it to a trustworthy green took four passes,
and **three of them were failures of the comparator, not of the move**:

- **Sub-blocks cannot be addressed by member name.** Inside `BuildVisual`, `Build` and friends are not
  members. First attempt: `@fragment` brace-balancing, which reported 4 false "not found" for nested
  `MonoBehaviour`s (`private sealed class BoltFader` has no parameter list, so requiring `(` missed it).
- **A member name is not an unambiguous address.** `RingFlash` has **two** overloads and the first is
  expression-bodied (`=> RingFlash(..., 1f);`), so member-addressing landed on a one-line forwarder with
  no braces. Fixed with inclusive `@start|||end` line ranges.
- **The dangerous failure is a wrong address that passes.** My first `BuildLineBody` marker,
  `Vector3 mid = transform.position + Direction`, occurs **twice** in the old file — once in the
  per-frame `Animate()`, once in `BuildVisual`. It matched the animation, ran 86 lines, and reported the
  pulse maths as a difference. It failed loudly *by luck*: had the two blocks' literals matched, the
  comparator would have printed `==` for a block it never checked. Rule 7's "a check nobody has seen
  fail" — sharpened to "a check that has only ever been *seen* fail, and cannot yet be seen to *pass for
  the wrong reason*".
- **A literal stream cannot see structure**, so it needs declared `Map`s (renamed identifiers) and
  `Carry`s (hoisted or caller-side literals), or a legitimate rename reads as a difference and a
  legitimate hoist reads as a deletion.

The `-Mutate` control flips `0.18f -> 0.19f` in memory and was confirmed to go red on **2** moves with
exit 2. **A green comparator nobody has seen go red is not a check** (rule 7) — and neither is a green
one whose markers have never been checked for uniqueness.

### Also in this commit

`NewWorldTestGround`'s beam-funnel bench call was repointed at the builder (the bench mounts the real
shape, per the 1f7 `BuildRockBody` precedent, so a visual change has a readout). `SkillFx`'s
`FxFallingRock` primitive stays inline — it is not one of the moved bodies, and the broad "no
`CreatePrimitive` left" grep is expected to read **1** there. `Magic/README.md`, `ARCHITECTURE.md`,
`game-design.md` §9.4 (Source Layout) and `TREE.md` are in the same pass; the README's "still unnamed on purpose"
paragraph about `SkillFx` was **rewritten rather than deleted**, because its stated reason ("`SkillFx`
also owns `SlashFlash`/`RingFlash`") is exactly what this commit changed, and a doc that keeps a reason
which has stopped being true is worse than no doc.

**Play-test items (optional — a behaviour-preserving move should be invisible):** cast a cone beam and a
line beam and check the tip orb still pulses on both; open the casting circle and the AoE preview; throw
an arrow and a hammer; summon a totem and a familiar; stand in a CC zone and confirm the ring is still
pale blue. Anything that looks different is a real regression, not a "new look".

## 1jc. `Assets/Scripts/Legacy/` - the old game's 32 files quarantined read-only, and the tool the move silently killed

**Status: SHIPPED.** The move itself (`git mv`, `.meta` travelling along, GUIDs unchanged) was `aae400b`;
the rule was `f6b5559` but landed at the top of `AGENTS.md`, above the `# Project Rules` heading, and
numbered **17** - colliding with the existing rule 17. This commit renumbers it to **18**, moves it into
the numbered list, adds `Assets/Scripts/Legacy/README.md`, deletes the emptied
`Assets/Scripts/Cutscenes/` folder + `.meta` (GUID 104c63d6..., **0** serialized referrers), **repairs
`tools/StaticChecks.ps1`** - which the move had killed - and updates `game-design.md` (new §9.4c),
`ARCHITECTURE.md`, `TREE.md`. Measured: **32** legacy `.cs` in 3 folders; **368** `.cs` / **368** `.cs.meta`
parity; **542** `.meta` / **542** unique GUIDs (no duplicate introduced by the new `README.md.meta`).
**skills: `scenario-unity-architecture` loaded** for the GUID/`.meta`/move question - *informative, not
authoritative*: its workflow is entirely `-batchmode`/`ArchJobs`/Test-Framework driven, which rule 3
forbids here, so it verified nothing in this repo. Its one actionable pointer (`merges and moves never
break references` / `meta_audit`) was checked by hand instead: **no `.gitattributes` and no LFS**, so no
path-scoped filter could have broken on the move (`git check-attr -a` on a moved file -> empty).
**Verified by grep + reread + `tools\StaticChecks.ps1` -> 0 candidates; no Unity build** (rule 3).

### The finding that mattered: the move killed rule 3's only instrument, silently

`tools/StaticChecks.ps1` reads four files by absolute-ish path (`$blueprints`, `$persistence`, `$npcs`,
`$world`), all `Assets\Scripts\World\WorldBuilder*.cs`. The move renamed all four. Running the script
printed its pre-flight `MISSING:` lines and then **threw on `Resolve-Path` before check 1 printed a
single result** - so the one instrument that exists to catch "review is not compilation" (rule 3) had
stopped compiling-checking anything at all. **No check reported it; the only way it surfaced was running
the script.** Repaired by repointing the four paths at `Legacy\WorldBuilder\` with a comment saying *why*
they are still read: checks 2/3/6 are the part-key parity guard (rule 9 - a renamed `Church_*` key builds
nothing, silently), and **a finding inside a `Legacy` file is a report of a rule-18 violation, not a fix
queue**, because fixing it would be the violation.

### Two failed instruments, kept here so the next reader does not rebuild them

- **GUID reachability is the wrong instrument for the C# call graph.** First audit asked "which live
  scripts does nothing reference?" by counting each script's GUID inside every other script's text. C#
  references types **by name**; GUIDs appear only in serialized assets. The scan reported **326 of 336**
  live scripts as unreferenced - `ChunkObject`, `WorldStreamer`, `NewWorldSystems`, everything. The
  correct instrument for this question is a type-name grep, which is what actually shipped: **23** live
  files name `WorldBuilder`, **15** name `MapBuilder`, **11** name `CutsceneManager`. The serialized side
  is the one GUIDs *are* the right instrument for, and it answered: `Assets/Scenes/SampleScene.unity`
  (the only scene in `EditorBuildSettings`) carries exactly **2** legacy components - `WorldBuilder` and
  `CutsceneManager` - so **the old game still boots out of `Legacy/`**. That is why the rule says
  *untouched has to keep meaning still runs*.
- **A "known-zero control" that is not zero is worse than no control.** The same scan printed a
  synthetic-GUID control expecting 0 and got **13678** in the serialized set: Unity writes
  `guid: 00000000000000000000000000000000` for every null/missing reference, so all-zeros is the single
  most common 32-hex string in a scene file. Replaced with `0123456789abcdef0123456789abcdef`
  (0 / 0 / 0) and an `IndexOf` walk instead of `String.Split` (1iv's rule). Second pass: 3.66 M live
  chars, 0.82 M legacy, 3.0 M serialized.

### Docs, because a move rots the docs that name what moved (rule 17)

`game-design.md` §6.2 (`Scripts/Cutscenes/`), §9.4's `Models/MapBuilder/` claim and §9.4b's "they now
live in" all named pre-1jc paths; `ARCHITECTURE.md`'s current-state table and §2.1 heading did too.
Corrected **current-state** claims; **historical** records (1iw/1hz entries, `PROGRESS.md`, `THINKING.md`)
left as written with a forward note, because rewriting them would falsify what those tasks did.
`TREE.md` regenerated (`aae400b`, 1178 tracked) - it was two structural commits stale.

### Pending play-test (needs Unity, rule 3)
- The old game still boots: open `Assets/Scenes/SampleScene.unity` and confirm the village + a cutscene
  still behave exactly as before the move (GUIDs are unchanged, so this should be a no-op).
- The new game is unaffected: stream terrain, cast a spell, sit, ride a pet - `MapBuilder.BuildTree` /
  `BuildStone` are still called from `ChunkObject`, now out of `Legacy/`.

### 1jc-status
- Shipped. Rule 18 in place, folder fenced, instrument repaired and green. Not compiled.

## 1jb. Models/Magic/MagicProjectileModelBuilder.cs + Models/Magic/MagicImpactModelBuilder.cs - the two remaining magic spell models get their files

**Status: SHIPPED.** Two new files under Models/Magic/ (436 + 203 lines). SpellCaster.Projectiles.cs 413 -> 72, SpellImpactFx.cs 159 -> 253 (net -42). Assets/Scripts 368 .cs / 368 .cs.meta. skills: none applied - pure relocation + interface change (single Part list). **Verified by grep + reread + normalised code-line comparison against pre-move blocks; no Unity build** (rule 3). tools/StaticChecks.ps1 -> 0 candidates.

### What moved
- Projectile bodies: 18 static members moved from SpellCaster.Projectiles.cs to MagicProjectileModelBuilder. Keeps FireProjectile and DecorateProjectile only. Destroy(col) -> Object.Destroy(col).
- Impact flashes: 8 shape families moved to MagicImpactModelBuilder.Build returning List<Part> (transform/material/spin). Parallel lists replaced.

### Verification
- Static checks 0 candidates; added both files to files list.
- Parity .cs/.cs.meta 368/368. GUIDs unique.
- Call sites updated (NewWorldTestGround, SpellImpactFx, SpellCaster.Projectiles, SpellLook).

### Files
- Models/Magic/MagicProjectileModelBuilder.cs + .meta
- Models/Magic/MagicImpactModelBuilder.cs + .meta

### Pending play-test
- Magic model bench renders identically.
- Impact flashes spawn with same colours/timing.
- Live casts and turret DecorateProjectile unchanged.

### 1jb-status
- Behaviour preserved; interface change documented.

## 1ja. `Models/Magic/MagicWeaponModelBuilder.cs` - the four magic models get the file 1iz said they needed

**Status: SHIPPED.** `Models/WeaponModelBuilder.cs` 379 -> 321 lines; new
`Models/Magic/MagicWeaponModelBuilder.cs` (107 lines). `Assets/Scripts` is now 366 `.cs` / 366 `.cs.meta`.
`skills: none applied` - a pure code relocation plus doc corrections; no installed skill governs file
layout. **Verified by grep + reread + a verbatim body diff against `HEAD` - NOT compiled** (rule 3: no
Unity build runs in this project). `tools\StaticChecks.ps1` -> **0 candidates**.

### What actually moved

The four section-`// 11.`-`// 14.` bodies, **verbatim**, out of `Models/WeaponModelBuilder.cs` into
`public static class MagicWeaponModelBuilder`. A diff of the four method bodies against `HEAD` reports
**0 differences**; the comparison harness was confirmed able to fail by mutating one line and watching it
report the change.

The source file changed in exactly four ways, and none of them is gameplay:

- the four `switch` cases now call `MagicWeaponModelBuilder.BuildStaff/BuildHolyBook/BuildBoneWand/
  BuildControlOrb(parent)` - so `Build*` references are 8 total: 4 declarations + 4 qualified calls, and
  there are **no unqualified external callers** to break;
- `MakeBlock` `private static` -> `internal static` (still reaches `MapBuilder.CreateSolidMaterial`);
- the **11** palette fields the magic bodies use `private` -> `internal`, reached via
  `using static WeaponModelBuilder;`. The palette itself **did not move and was not copied**;
- the class summary, which had claimed "all 15 weapons".

### Two access changes, not a duplicated helper

The 1iz plan called the blocker "`MakeBlock` is `private`". Both halves are now widened rather than
duplicated, because there is **no `.asmdef` anywhere in the repo** (measured: 0), so `internal` +
`using static` is the same assembly and needs no forwarding wrapper. The palette is **15**
`static readonly Color` fields; the magic four use **11**; the remaining four (`SteelSilver`,
`DarkGold`, `WoodBrown`, `WarmBrown`) are used only by the non-magic builders and stay `private`.

### The section numbers were NOT renumbered

`// 11.`-`// 14.` stay as they are, and `// 15.`-`// 18.` still follow `BuildLute`. The numbers mirror
registration order in `Assets/Scripts/Combat/Weapons/WeaponCatalog.cs`, so they are documentation of
catalog order rather than a within-file sequence; renumbering to a tidy 1-14 would silently break that
correspondence.

### `BuildLute` stayed, deliberately

`WeaponCategory.Magic` contains **five** weapons, not four. `lute` is the fifth, and it is **not**
contiguous with the other four - the block ends at `BuildControlOrb`, `// 15.` is the lute. The
selection criterion was 1iz's measured one: these four are the only weapon defs carrying a **rotation**
accent, which is why 1im's rest-pose drift appeared on magic alone. `BuildLute` is scale-only. Moving it
would have meant moving a method that is not part of the reported defect.

### Numbers in the docs were wrong, and are now measured

Five documents carried 1iz's counts; several were incorrect and are corrected in this pass:
**18** weapon builders (19 `Build*` symbols counting the dispatch), **14** non-magic builders,
**15** palette colours, and the block span **L199-275** in the 379-line file (`// 11.` at L199,
`// 15.` at L276, with a rule line at each boundary). `TREE.md` regenerated from the staged tree:
tracked 1173 / non-`.meta` 539 / `.meta` 634, `Assets/Scripts` 368.

### Files

- `Assets/Scripts/Models/Magic/MagicWeaponModelBuilder.cs` + `.meta` (guid `0d168455f6404d6295e622a90091d9bb`)
- `Assets/Scripts/Models/Magic.meta` (guid `31e3cdc380f24021a0c35bd93f9475e2`)
- `Assets/Scripts/Models/WeaponModelBuilder.cs`
- `ARCHITECTURE.md`, `AGENTS.md`, `game-design.md`, `PROGRESS.md`, `THINKING.md`, `TREE.md`

### Verification performed
- verbatim body diff vs `HEAD`: 0 differences (mutation control fires)
- GUID uniqueness: 20,376 `.meta` scanned, 20,375 distinct, **0 duplicates**
- `.cs` / `.cs.meta` parity under `Assets/Scripts`: **366 / 366**
- brace + paren balance on both `.cs`; exactly one class declaration each
- `Build*` reference count = 8 (4 declarations + 4 qualified calls), 0 unqualified callers
- `TREE.md` completeness rebuilt from the rendered tree: 415 rendered + 124 collapsed = **539** =
  `git ls-files` non-`.meta`, 0 invented paths
- `tools\StaticChecks.ps1`: 0 candidates

### Pending play-test items
- Open the magic weapon rack / spawn each of staff, holy book, bone wand, control orb and confirm all
  four still build and animate. A move like this cannot fail at load time in review - only in the editor.
- Confirm the four look **identical** to before (same palette, same geometry): any difference means an
  `internal` widening was missed and a default `Color` is being substituted.
- Confirm the combat-mode toggle (`Keyboard.current.f1Key`) and weapon rig seating are unaffected.

## 1iz. `ARCHITECTURE.md` — the magic models were never missing, they were unnamed

**Status: plan written, nothing moved. New `ARCHITECTURE.md`. No code touched (365 `.cs` still 365
`.cs.meta`). `skills: none applied` — measured source-layout analysis; no installed skill governs it.
Verified by grep over an explicitly-built recursive list of all 365 `.cs`, plus rereading the extracted
source at its real line boundaries.**

Requested, in three parts: *group models and animations close together; I don't see magic models; split
the controller, the modelling and the animation apart; write down a structural project.*

### The premise failure, reported first

**"Group the models and animations together" has no single target, because `Models/` is not one
category.** `Models/MapBuilder/` is 10 partials of one class referenced by **40 files** — the most
depended-on symbol in the repo — and its public surface includes `BuildCloud`, `BuildTornado`,
`BuildCafe`, `BuildPoliceCar` and `BuildPlayerHouse` sitting beside `BuildPlayerModel`. So `Models/`
means *procedural geometry*, and only 7 of its 17 `.cs` are per-thing model builders (2–7 referrers
each). **Measured before naming any destination**; a differently-shaped pile would have been the same
mistake in a new folder.

### "I don't see magic models" — they exist, and they are four adjacent methods

`Models/WeaponModelBuilder.cs` was 379 lines with 18 weapon builders (19 `Build*` symbols, counting the dispatch). The magic four are **contiguous**,
and the boundaries were read out of the file rather than inferred:

| Method | Body | Header |
|---|---|---|
| `BuildStaff` | 201–215 | `// 11. MAGE'S STAFF` @ 199 |
| `BuildHolyBook` | 220–233 | `// 12. HOLY BOOK` @ 218 |
| `BuildBoneWand` | 238–253 | `// 13. BONE WAND` @ 236 |
| `BuildControlOrb` | 258–273 | `// 14. CONTROL ORB` @ 256 |

Nothing in the repo — no file, no folder, no symbol — says "magic model", which is exactly why a search
for one returns a folder about something else. These four are also the only weapon defs carrying a
*rotation* accent, which is why 1im's rest-pose drift appeared on magic alone.

### The extraction blocker is narrower than it looks

First draft of the plan said the shared palette and the `MakeBlock` helper "must move or become
accessible". Reading the source corrected that: the **palette cannot move** — it is 14
`private static readonly Color` fields of which the magic four use 11, and the other fourteen weapons
share the same fields. The only real blocker is `MakeBlock` being `private static` (it also reaches into
`MapBuilder.CreateSolidMaterial`, so it is not self-contained). Stage 1 is therefore "widen one
accessibility modifier and move 77 lines", not "untangle a shared helper".

### The plan

Four stages, smallest blast radius first, each one commit: **1** extract the magic weapons (fixes the
actual complaint); **2** `Models/` → `Geometry/{Actors,World,Weapons}`, pure moves; **3** extract the
92-line `PlayerController.Animation.cs` bridge into an `Avatar/PlayerAvatar` component — the only stage
that changes signatures, so it goes last and alone; **4** optional, pull `WeaponRigBuilder`/`WeaponRigHost`
out of `Combat/`.

Two invariants are stated as non-negotiable: a class's partials stay with its class (`PlayerController.*`
×6, `MapBuilder.*` ×10 are never separated), and `.cs`/`.cs.meta` parity is the real check, because with
no namespaces and no `.asmdef` a path move cannot break compilation.

## 1iy. `TREE.md` — a generated project map, and the verifier that had to be made able to fail

**Status: shipped. New `tools/Write-Tree.ps1` + generated `TREE.md`. No game code touched.
`skills: none applied` - repo documentation tooling, which no installed skill governs.
Verified by rebuilding all 537 paths out of the rendered tree and diffing against `git ls-files`.**

Requested: "write down a project file tree into a file." The obvious implementation — type the tree by
hand — is the one thing that would have made the file worthless, so this ships as a **generator plus its
output** instead: `powershell -ExecutionPolicy Bypass -File tools\Write-Tree.ps1` rebuilds `TREE.md`
from `git ls-files`, and the file's own header names the commit and timestamp it saw.

### Why generated rather than maintained

1iu shipped two hand-written navigation maps (`Magic/README.md`, `Animation/README.md`) and **every
symbol in them was wrong on the first pass** — the rest-pose pair filed under the wrong owner, a
`HolsterPoint` that does not exist, `PlayerAnimator` described as owning blends it never had. Nothing
errors when a doc is wrong; the reader just goes to the wrong file confidently. A hand-typed tree of a
1169-file repo rots on the next structural commit, and unlike C# nothing compiles it. A generated one is
at worst out of date, and it says so on its first line.

### What it shows, and what it deliberately does not

- Source of truth is `git ls-files`, so untracked and `.gitignore`d clutter (`Library/` alone is 51,903
  files) **cannot** appear even by accident.
- **`.meta` files are omitted everywhere** — 632 of them, 54% of the repo by count, and none carries
  information a reader needs. The header states the count instead of listing them.
- `Assets/Scripts` is **always expanded in full** (367 files); `_ArtSource` [38], `Assets/Resources`
  [26], `Assets/TextMesh Pro` [33] and `ProjectSettings` [27] collapse to `[N files]`.
- The file asserts **no design or process claim**, only structure, because structure is the one thing it
  can be regenerated to verify. It points at `AGENTS.md` / `game-design.md` / `PROGRESS.md` /
  `THINKING.md` rather than duplicating their claims into a fifth copy.

### Two bugs the work exposed, both in the tooling rather than the output

- **PowerShell 5.1 has no `` `u{XXXX} `` escape** (that is 6+), so the first render printed the literal
  text `u{251C}` for every branch. Glyphs are now built with `[char]0x251C`. The file is written UTF-8
  without a BOM.
- **A size heuristic must not be able to hide a subtree.** `Assets/` holds 445 files, so it collapsed on
  the threshold and `Assets/Scripts` — the one directory the force-list exists to protect — was never
  reached. `Should-Expand` now also expands any *ancestor* of a forced directory.

### Verified, and shown able to fail

Rebuilt every path from the rendered tree's indentation and diffed against `git ls-files`:
**413 rendered leaves + 124 inside the 4 collapsed directories = 537 tracked non-`.meta` files, zero
invented paths, and all 4 printed `[N files]` counts match what git holds.**

The first version of that verifier **passed vacuously at 0 == 0** because its regex swallowed the tree
glyphs into the directory names, so both sides of the comparison were empty; a second version then
reported two false failures by dropping the `Assets/` ancestor prefix. It only became trustworthy after
it was observed reporting `BAD` on a real mismatch. That is rule 7 applied to the verification rather
than the fix: **a check nobody has seen fail is not a check.**

## 1iw. `Models/MapBuilder/` exists — and it is load-bearing, contrary to the request's premise

**Status: shipped, NOT play-tested (rule 3 - no build). Cosmetic file moves + docs; no code edit.
`skills: none applied` - a folder reorganisation of one static partial class, which no installed
skill governs. Verified by caller enumeration + GUID/meta parity + `StaticChecks.ps1` 0 candidates.**

Requested: "MapBuilder's files take up too much space in `Models` while not being used in the game,
create a folder and put them in there." The premise was measured before the move and is **false**;
the move was done anyway because it is safe, and the measurement is recorded so the tidy folder is
not later read as licence to delete.

**Done:** `Assets/Scripts/Models/MapBuilder/` holds all 10 `MapBuilder.*.cs` partials (5,153 lines),
each moved with its `.meta` (GUIDs preserved). `Models/` is left with the 7 builders it is actually
about: `Boss`/`Enemy`/`Goblin`/`Horse`/`Item`/`PlayerPartMesher`/`WeaponModelBuilder`. New folder meta
`d1446c600a04472dba884fa5d7edbe7e`. **Zero callers were edited** - the class name is unchanged, C# is
folder-agnostic, and 0 path-string references exist.

**The premise: ~40 files call it, and the important ones are not village content.**

| Live consumer | Calls |
|---|---|
| `Player/Races/RaceRig.cs`, `PlayerController.Animation.cs`, `Animation/PlayerAnimator.cs` | `BuildPlayerModel` |
| `Interactions/PlayerSitController.cs` | `BuildSitPlayerModel` |
| `Magic/Cast/SpellTornado.cs`, `SpellBeam.cs`, `SpellCaster.Cast.cs` | `BuildTornado` |
| **`World/Chunks/ChunkObject.cs`** | `BuildTree`, `BuildStone` |
| `Quests/RandomEventManager.cs` | `BuildCloud`, `BuildTornado` |
| `Pets/PetController.cs` | `MakeBlock` |
| `UI/UIManager.cs` | `RefreshWorldSignTexts` |

`ChunkObject` is the one worth knowing: **the streamed terrain draws its trees and stones through
`MapBuilder`**, so the class sits in the new world's critical path despite living under `Models/`.
Deleting it would have removed the player model, sitting, a spell, pets and the terrain's own foliage.

The honest partial truth: 9 of the 10 partials (`.Houses`, `.Mansion`, `.Restaurants`, `.Stores`,
`.Police`, `.Vehicles`, `.NPCs`, and the village half of `.Nature`/`.PlayerModels`) really are only
used by `WorldBuilder` and the nine `CutsceneManager.Ending*` files, so retiring the legacy layer would
orphan them. But `PlayerModels.cs` and `Nature.cs` would have to be kept or promoted first, so the
class as a unit is not dead.

### Verified

- 10/10 partials present with paired `.meta`; 633 tracked metas hold 633 distinct GUIDs; 0 orphan metas
  under `Assets/`; `.cs`/`.cs.meta` unchanged at 365/365; 0 stale `Models/MapBuilder.cs` path refs.
- `tools/StaticChecks.ps1` does not cover `Models/` (checks 1-5 cover `WorldBuilder*.cs` +
  `NewWorldTestGround.cs`), so no `$files` update was needed.
- New `AGENTS.md` rule 7 bullet: "not used" is a claim about a call graph, and a folder that dominates
  a directory is where that claim is most tempting and least true — do the cosmetic move, but report
  the premise failure in the same pass. New `game-design.md` §9.4b records the consumer table.

## 1iv. 34 MB of source art moved out of `Assets/` — and the four files that had to stay

**Status: shipped, NOT play-tested (rule 3 - no build). File moves/deletes + docs; no code change.
`skills: scenario-unity-architecture` for the GUID-resolution rule (informative, not authoritative).
Verified by GUID-level reference scan + re-resolve after the move + `StaticChecks.ps1` 0 candidates +
`.cs`/`.cs.meta` 365/365 unchanged.**

The plan was "move `Assets/model`, `Assets/texture`, `Assets/UI component`, `Assets/xoanvnmexel` out".
**The reference scan said that would have broken the game**, and the plan was wrong in an instructive
way. Moved to repo-root `_ArtSource/` (outside `Assets/`, so Unity stops importing it; git keeps it):

| Moved | n | Size |
|---|---|---|
| `Assets/model/` (all) | 52 | 32.8 MB |
| `Assets/UI component/` (all) | 24 | 0.06 MB |
| `Assets/texture/` — 6 unreferenced textures | 12 (6 + metas) | 1.2 MB |
| `Assets/xoanvnmexel/Readme.txt` | 2 | - |

Deleted outright, not archived: 3 byte-identical duplicates whose twins already live in
`Resources/texture/` and which nothing referenced (`grass_blade`, `leaves_texture`, `wood_texture`); the
root `xoanvnmexel.zip` (25 KB, the un-extracted pack); and root `sound.meta`, a second orphan meta.
Removed from disk only: `Assets/_Recovery/` (52 untracked files incl. 27 abandoned `0 (N).unity`
scenes), gitignored since 1hz.

### The four files that stayed, and why the folder names were no evidence

| Kept in `Assets/` | Evidence |
|---|---|
| `Assets/texture/{dirt_texture, fertilize, peashooter_seed}.png` | referenced **exactly once each** by `Scenes/SampleScene.unity` - the only scene in `EditorBuildSettings` - as the named fields `FieldTexture`, `FertilizerTexture`, `PeashooterSeedTexture` |
| `Assets/xoanvnmexel/XoanVnmexelStandard.ttf` | `m_SourceFontFileGUID` in `Resources/VietPixel.asset`; `UiAssetCache` does `Resources.Load<TMP_FontAsset>("VietPixel")` and TMP's default font asset references it. This is the **live UI font**. |

`Assets/xoanvnmexel/` is an unpronounceable pack name that read as pure junk, and moving it would have
removed every glyph in the game. Re-resolved all four after the move: still 1 occurrence each, font GUID
still present in `VietPixel.asset`.

> **The first count was wrong and is retracted.** The pre-move scan reported 7717 / 5292 / 7374
> occurrences for the three textures, which is impossible in a 29,003-char scene file — `"$text".Split($g)`
> splits on each *character* of the GUID. True counts are **1 each**, confirmed by regex `Matches` and an
> `IndexOf` walk, against bogus-GUID controls reading 0. The numbers had already reached `game-design.md`,
> `AGENTS.md` and this entry before being caught; all are corrected. The scope did not change: 3 textures
> genuinely are live, each as a single named field.

### What was verified

- Collected the 59 GUIDs in the 4 folders, then grepped every `.unity/.prefab/.asset/.mat/.controller`
  for them: **10 referrers, of which 8 were the gitignored `_Recovery/` scenes** and 1 was
  `SampleScene.unity`. `.cs`/`.cs.meta` parity unchanged at 365/365 (no `.cs` was touched).
- Each moved asset's `.meta` travelled with it. The moved **folder** metas were deleted rather than moved:
  Unity cannot resolve a folder GUID for a folder outside `Assets/`. `Assets/_Recovery.meta` was also
  caught and deleted as a newly-orphaned folder meta.
- After the move: 0 tracked files left under the old paths; every tracked non-`.cs` meta under `Assets/`
  has a folder behind it; all 639 tracked metas hold 639 distinct GUIDs.
- `Assets/texture/` is now 3 files. `dirt_texture.png` is byte-identical to its `Resources/` twin and
  **both copies are live** (scene field vs `Resources.Load`), so the pair was left alone and said so
  rather than deduped on paper — merging it means hand-editing a serialized GUID in a scene file,
  unverifiable without the editor (rule 3).
- New `AGENTS.md` rule 7 bullets: a GUID only resolves inside `Assets/`, so moving an asset out turns a
  live reference into a silently-missing one — measure referrers first, ask which referrers are *live*
  rather than merely present, and never trust a `String.Split` count without a known-zero control.

## 1iu. `Magic/` and `Animation/` exist; `Combat/Effects/` is gone

**Status: shipped, NOT play-tested (rule 3 - no build). Moves + two new READMEs + docs only; zero
behaviour change. `skills: scenario-unity-architecture` (its `.meta`-travels-with-the-asset rule is
informative here, not authoritative - rule 3 means it could not have verified anything).
Verified by grep + reread + `StaticChecks.ps1` 0 candidates + `.cs`/`.cs.meta` 365/365.**

34 files moved, each with its `.meta`. Two new trees and a `Combat/Effects/` split:

| Was | Now | n |
|---|---|---|
| `Combat\Effects\SpellLook.cs` | `Magic\Look\` | 1 |
| `Combat\Effects\{SkillFx,SpellImpactFx,CastingCircle}.cs` | `Magic\Fx\` | 3 |
| `Combat\Effects\SpellDoT.cs` | `Magic\Cast\` | 1 |
| `Combat\Weapons\{SpellCaster + 4 partials,SpellData,SpellEffect,SpellZone,SpellStorm,SpellSummon,SpellTornado,SpellBeam}.cs` | `Magic\Cast\` | 12 |
| `UI\NewWorld\{MagicWheelUI,MagicTestMatrix}.cs` | `Magic\Ui\` | 2 |
| `Player\PlayerAnimator.cs`, `Combat\Weapons\{WeaponAnimator,WeaponStowAnimator}.cs` | `Animation\` | 3 |
| `Combat\Effects\{StatusEffectType,BlindStatus,ChillStatus,WetStatus,CCZone,ElementSignatureStatus}.cs` | `Combat\Status\` | 6 |
| `Combat\Effects\{DamageNumber,HitStop,ScreenShake,CombatFeedback}.cs` | `Combat\Feedback\` | 4 |
| `Combat\Effects\{SummonedAlly,StaminaSystem}.cs` | `Combat\Skills\`, `Combat\` | 2 |

`Combat\Effects` held 19 files; 1it deleted 2, so all 17 survivors went elsewhere and the folder +
its meta are gone. Combat went from two catch-all folders to five named ones.

### What deliberately did NOT move, and why

- **`Models\WeaponModelBuilder.cs` stayed put.** It builds **all twenty** weapons, four of them magic
  (`BuildStaff`/`BuildHolyBook`/`BuildBoneWand`/`BuildControlOrb`, dispatched by `Build(weaponId, …)`),
  so the magic four cannot be split out by a path move - that needs the *file* split. Same for
  `MagicWeaponBehavior` / `MagicWeaponMods`, which are a `WeaponCategory` driven by `WeaponData`, not
  part of the spell pipeline.
- **`Player\PlayerController.Animation.cs` stayed in `Player\`.** It is a `partial class
  PlayerController`. The test used: group **independent components** (all three animators stand
  alone), and leave a class's own partials with their class.
- **Spell geometry stayed split by *when* it draws** - `SpellCaster.Projectiles.cs` owns in-flight
  bodies, `SkillFx.cs` owns the falling rock. Both are mounted by `MagicTestMatrix`, so each still has
  an acceptance readout.

### Why this was low-risk, and what was checked instead

There are **no namespaces and no `.asmdef`** anywhere in the project, no reflection, no
`[SerializeReference]` and no string-keyed component lookup, so a folder is an organisational unit
and a move cannot break compilation. The things a move *can* break were checked instead:

- `.meta` travelled with every file (`.cs`/`.cs.meta` 365/365, unchanged), 8 new folder metas and 2
  README metas authored with fresh GUIDs, and every one of the 71 folders under `Assets/Scripts` has a
  meta.
- `tools\StaticChecks.ps1`: 0 candidates; its `$files` list references no moved file.
- Every path string in `Assets`, `game-design.md`, `AGENTS.md` and the tools script was scanned for
  the old locations: **zero** hits outside `PROGRESS.md`/`THINKING.md` history, which is left alone
  because rewriting a past task record falsifies what was true then.
- Found and removed an orphan: **`Assets/Scripts/Audio.meta`**, tracked but with no `Audio/` folder.
  See the 1it entry - it survived that task because "is this folder empty" cannot ask about a folder
  that is absent.

### The two new READMEs were wrong on the first pass

`Magic\README.md` and `Animation\README.md` shipped only after every symbol they name was grepped,
and **four claims had to be corrected** - all caught by grepping after writing, none by rereading:

| Claim as first written | Truth |
|---|---|
| `SyncRestFromIdle` / `RestoreAuthoredRest` owned by `PlayerAnimator` | `WeaponAnimator` (`AGENTS.md` rule 16 is about `WeaponAnimator`) |
| `WeaponStowAnimator`'s sheathe anchor is `HolsterPoint` | `HolsterPoint` **does not exist**; the field is `AnchorParent` |
| `PlayerAnimator` owns idle/attack/dodge blends and the melee swing arc | it is a *procedural* walk/run/idle component for the `MapBuilder` model; the attack/swing states live in `PlayerController.Animation.cs` / `WeaponAnimator` |
| `SpellLook` is documented at `§3.13`; `MagicTestMatrix` bench at `§2.2` | `§3.8.3` for both - and `3.13` is not a section that exists |

New `AGENTS.md` rule 8 bullet: a navigation map is a copy of the codebase, it is the one file nothing
checks, and **never write a line number** in one. New rule 7 bullet: a scan reporting absences must be
shown able to report presences - the first verification pass here reported 9 of 22 symbols MISSING,
all nine depth artefacts (`**` in a PowerShell `-Path` glob descends exactly one level).

## 1it. Dead scaffolding, vestigial folders and the stale `_Archived/` fork are gone

**Status: shipped, NOT play-tested (rule 3 - no build). Pure deletion + one doc line; no gameplay
change. `skills: scenario-unity-architecture` loaded for the `.meta`-travels-with-the-asset rule (a
Unity skill is informative here, not authoritative - rule 3 means it could not have verified
anything). Verified by grep + reread + `StaticChecks.ps1` 0 candidates.**

### Deleted: 7 unreferenced scaffolding files (373 -> 365 `.cs`)

Every one had its own declaration as the **only** occurrence of its name in the whole `Assets` tree
(scripts, scenes, prefabs, assets, JSON), with **zero** `AddComponent<T>` / `GetComponent<T>` /
`new T` producers, and no `[SerializeReference]`, no reflection and no string-based type lookup
anywhere in the project to hide behind:

| File | What it was |
|---|---|
| `Combat\Effects\CombatAnimation.cs` | planned Task 3.3 Animator bridge |
| `Combat\Effects\RagdollEnabler.cs` | planned Task 3.3 ragdoll enable |
| `Player\Controller\OpenWorldGrounding.cs` | planned Task 4.2 terrain-snapping helper |
| `Player\Creation\CharacterCreation.cs` | planned Task 4.4 creation/roll screen |
| `World\Housing\HousePlotPlacer.cs` | planned Task 6.5 plot placement |
| `World\Loot\WorldLootPlacement.cs` | planned Task 5.2 chest seeding |
| `World\Npcs\EconomyProvider.cs` | planned Task 6.4 pricing path (shops price their own) |

All seven are `GAMEPLAY_IMPROVEMENT_PLAN.txt` "planning Task N.M" reference implementations named by
**zero** docs. Also deleted: `World\WorldBuilder.Inspection.cs`, an empty
`public partial class WorldBuilder {}` shell.

### Deleted: 18 vestigial empty folders (+ their folder `.meta`s)

`Scripts\_Archived`, `Combat\AI`, `Player\Combat`, `Player\Inventory`, `Utils`, `Multiplayer` +
`Client`/`Server`/`Sync`, `SideContent` + 7 empty subfolders - residue of the 1hz multiplayer
removal and an abandoned regroup that was planned and never populated - plus `Player\Creation`,
which became empty **in this same pass** when `CharacterCreation.cs` went. `SideContent`/`Multiplayer`
were counted by an `-Recurse -File` emptiness test that found only their *subfolders' own* `.meta`
files; the `.cs` count is what says "empty".

A **nineteenth** tracked folder meta was found still live after 1it and removed in 1iu:
`Assets/Scripts/Audio.meta`, a meta whose `Audio/` folder was already gone - an orphan of an earlier
removal, invisible to any "is the folder empty" test because the folder is not there to ask about.

### Deleted: `_Archived/` at the repo root (67 tracked files)

A stale fork, and its `README.md` was making **four false claims**: `CutsceneManager` "retired, no
endings" (live: `Scripts\Cutscenes\`, 11 files), `WorldBuilder` "legacy voxel world" (live:
`Scripts\World\`), `QuestManager`/`RandomEventManager` "retired" (live: `Scripts\Quests\`), and
`EnemyController` moved to `Scripts/Combat/AI/` (that folder was **empty**; it lives at
`Scripts\Enemies\_Shared\`). Its one true claim - a `RemoveEndings` compatibility shim - describes a
live flag on a live class that has nothing to do with the archive. Only `Mob.cs` and
`WorldBuilder.FastTravel.cs` had no live counterpart, and git history keeps both.

### Two judgement calls that went the other way from "delete"

- **`RaceSpellEffect` (in `RaceEffect.cs`) is KEPT.** Unreferenced, but it is one of ten
  `IRaceEffect` implementations and the only natural slot for "a racial ability casts a spell" - a
  reader looking for that mechanism should find it. Deleting an unused member of a *live*
  polymorphic family is not the same as deleting an orphaned file.
- **`CombatController.OnStateChanged` is KEPT**, and now has **zero subscribers** in-tree
  (`CombatAnimation` was the only one) with its 7 `?.Invoke` sites intact. It is a public extension
  point and a null delegate invoke is free; stripping the event would be a second, unrelated
  behavioural edit to a live combat file riding along in a delete-dead-code diff. Recorded here
  rather than in a code comment, because "nobody subscribes" is a fact about today.

### Deletion side-effects recorded, not chased

- **`World\Housing` is a dead sub-tree, reported as the next candidate.** `HousePlotPlacer` was the
  only thing that ever did `AddComponent<HousePlot>()`, and `HomeBuilder.TryBuild(HousePlot, ...)`
  already had zero callers - so removing the placer leaves `HousePlot` with no creator at all.
  Following it would cascade into `CraftingStation`, `HomeChest` and `FarmPlot`, so 1it stops here.
- **`RaceDatabase.Roll()` loses its only caller** (`CharacterCreation`). It is a public method on a
  ScriptableObject, left in place.
- **`game-design.md:1487` corrected in the same pass.** It listed `WeaponData`'s shared fields as
  including "hand usage (single / dual / two-hand)" - a field that does not exist. The `HandUsage`
  enum that gave the claim its name is deleted (no field on `WeaponData` used it), and the doc's own
  §2208-2216 states wielding is governed by `Weight` + `StrengthRequirement`. The enum's header also
  cited §5.4, which is *Crafting*; the wielding section is §5.5.

### Verification

- **`.cs` count 365, `.cs.meta` count 365 - the GUID invariant.** Every deletion removed the `.cs`
  and its `.meta` together.
- Zero residual references to any deleted symbol across `Assets\**\*.cs|unity|asset|json`.
- `tools\StaticChecks.ps1`: **0 candidates** (run because `WorldBuilder.Inspection.cs` was a
  `WorldBuilder*.cs` file).
- **NOT compiled** (rule 3).

### Pending play-test items for the user
- Nothing to observe in game - this task deletes only unreachable code and empty folders. The one
  thing worth confirming is that Unity's console is still clean on load (no missing-script warning),
  which is the only externally visible consequence of touching 98 tracked paths.

## 1is. The Great Tornado silhouette (funnel beam) + a familiar that sprays down your aim line

**Status: shipped as found (the user's in-flight work, committed unchanged in substance), NOT
play-tested. `skills: none applied` - reviewed C# only.**

- **Beam:** `SpellBeam.BuildConeVisual` -> `BuildFunnelVisual`: 9 chunky discs stacked along the beam
  axis (`FunnelChunks`) + 3 orbiting debris chunks (`FunnelDebris`), so the 44-degree Flamethrower cone
  reads as a funnel with a twist instead of a fan of flat wedge sprites. The hot end is
  `SpellLook.HotCore(Core)`, not `Edge` - `Edge` desaturates Fire's core toward white, the opposite of
  a burning throat. `YawFor(seed)` is deterministic per index so the twist survives a rebuild.
- **Aim derivation unified:** the cone's forward vector moved to `SpellCaster.CurrentAimDirection`, so
  `SpellBeam` and `SpellSummon` cannot disagree about where "forward" is - the drift 1ir invited by
  giving the familiar its own copy.
- **`SummonFiresForward` is a NEW flag, separate from `CasterAnchored`** - one flag would have handed
  every future caster-anchored turret "fires regardless of targets" for free. With no target gate,
  `Radius` stops bounding the spell and only sizes the drawn circle; the bolts' own flight envelope
  decides reach. `SpellSummon.FireForward` + the single `SpawnBolt` site keep the spray and the
  nearest-target path from drifting on muzzle or lead.
- **Bench:** `NewWorldTestGround` mounts `BuildFunnelVisual` rather than a proxy cone (rule 7 - a
  visual that exists only inside a live cast has no acceptance readout).
- **Isolated from unrelated WIP** that was in the tree: a stale `GAME_DESCRIPTION.md` deletion and a
  half-done `Scripts\Audio` -> `Scripts\Combat\Audio` folder move (both folders are empty; they
  belonged to 1it's dead-folder sweep, where `Combat\Audio` was already gone).
- `tools\StaticChecks.ps1`: **0 candidates**. **NOT compiled** (rule 3).

### Pending play-test items for the user
- Cast Flamethrower: the cone should read as a solid funnel with a swirl, hottest at the caster.
- Continuous Fireball should take station **behind** you and pour bolts down your aim line.

## 1ir. The meteor line stops being two single-shot rocks: Flamethrower is a 44-degree cone on a channel, Continuous Fireball is a familiar that follows you

**Status: shipped, NOT play-tested (rule 3 - no build or play-test runs in this project). Real
gameplay change: two spells removed, two added, three new mechanics (swept-cone Beam, caster-anchored
Summon, charge-scaled summon lifetime), two look axes' only users deleted, and both a behaviour and a
bench branch fixed as a consequence. `skills: none applied` - the task is a reviewed C# edit, and per
AGENTS rule 15's "match the skill to the artifact" none of the installed DCC/Unity-driver skills governs
it (and rule 3 means a Unity skill could not have verified anything here anyway).**

### What changed

- **`magic_fireball_meteor_comet` (Comet) -> `magic_fireball_meteor_flamethrower`.** Beam delivery,
  power 36, FP 26, cd 6s, range 11, `deliveryRadius` 2.4 (the cone's **tip** radius), Burn, knockback
  1.5, `ChannelDrainPerSecond` 11, `BeamHalfAngle` 22. Replaces a single-shot ember streak that was
  its own only `EmberStreak` user.
- **`magic_fireball_meteor_astroid` (Asteroid) -> `magic_fireball_meteor_continuous`.** Summon
  delivery, power 36, FP 28, cd 8s, `deliveryRadius` 5 (targeting), `duration` 8s, `tickInterval` 0.4s,
  `projectileSpeed` 22, Burn, `CasterAnchored`, `BoltSplashRadius` 1.6. Replaces a Zone slam that was
  the sky-rock family's only `Swarm`.
- **Spell count is unchanged at 172** - the diff is exactly two `S(...)` lines out and two in.
- **Old ids self-canonicalise on load** (`SkillProfile.ResolveLegacyId`, called from **both** restore
  paths): `learnedSkills` entries and `skillLevelsJson` keys are rewritten in place, so a returning
  player's Comet/Asteroid become the new spells without losing a single level.

### Three new mechanics, each opt-in so nothing else moved

1. **Swept cone (`SpellData.BeamHalfAngle`, default `0`).** `0` keeps the original single-capsule line
   **byte-identically** - all ten earlier beams, including Searing Ray, are untouched. `> 0` fans
   `ConeRays` (7) rays across `±BeamHalfAngle` with `ConeSegments` (2) overlapping capsules each,
   opening from `ConeMouthFraction` (0.35) x tip radius at the mouth to the full `Width` at the tip,
   flashing **once per tick** like the line, and damaging each target root **once per tick** no matter
   how many segments overlap it. Searing Ray was given `5.625` so the two Fire beams read differently
   by shape alone - they are mutually exclusive anyway (`ResolveBeam` calls `StopChannel`).
2. **Caster-anchored summon (`SpellData.CasterAnchored`).** Spawns on the caster instead of the ground
   target, follows them with per-frame snapping to the **nearest** ground below (skipping **their own**
   colliders so it does not snap to their head, and taking the lowest `point.y` rather than the first
   buffer entry, since `RaycastNonAlloc` does not sort), and carries a persistent ground circle at its
   targeting radius. It tracks XZ outright with **no wall avoidance** — it clips through walls with
   you, which is stated in code and in the docs rather than implied by "follows you". The other nine
   summons keep ground-target placement, and only this one gets the charge-scaled lifetime below.
3. **Charge-scaled summon lifetime (`SpellCaster.DurationScale`).** A **deliberate user decision**:
   `DurationScale(charge) => SizeScale(charge)`, so charging a familiar fully buys both a longer stream
   and a bigger familiar. Full charge on Continuous Fireball is `2.2x` size / `2.6x` damage / `17.6s`,
   ~44 bolts at `0.4s`. No `ChargeDurationBonus` field exists - an earlier draft proposed one and the
   user rejected it as one knob too many. `ResolveSummon` hands the multiplier to **every** summon, so
   `SpellSummon.Initialize` applies it **only** when the summon is caster-anchored; scaling it for all
   of them would have silently turned Ember Effigy's 6 s into 7.2 s at full charge.

### Two look axes lost their only user, and the removal is the interesting part

- **`SkyRockStyle.Swarm`** existed for exactly one spell. The enum value, `SkillFx.BuildRockSwarm` and the
  `BuildRockBody` branch are **all deleted**, leaving `Inherit`/`Boulder`. I first wrote that the enum
  slot should be *kept* as a save-migration guard, then checked what a save actually holds:
  `SaveManager` persists only `learnedSkills` and `skillLevelsJson`, and a look is authored in
  `SkillCatalog`, never serialized - so no save can reference the value and there was nothing to
  migrate. Reserving it would have kept a name nothing could use. The F4 key packs the raw enum value at
  bits 24+, so dropping an **unused** value cannot split or merge a group and `172 / 172 / 0` stands.
  `SkillFx.BuildRockBody`'s `style` parameter is now **inert** and is documented as such - kept because
  it is the seam a future style grows through, which is only defensible if the comment says so.
- **`ProjectileShape.EmberStreak`** and its builder are deleted. **`ProjectileShape.Comet` stays** -
  Scorch, Burn and Frost Bite still wear it, which is why 1f7's note about "Comet only" had to be
  rewritten rather than the shape removed.

### Two bugs found while building this, both fixed in the same pass

- **The cone visual was positioned in world space**, so `BuildConeVisual` ignored its parent - the QA
  bench would have mounted every cone at the world origin instead of on its pedestal. Now built and
  pulsed in the parent's **local** frame (`InverseTransformDirection` for the live pulse).
- **The cone deduped on damage but not on healing.** `TickCollider` returned `false` for the heal
  branch, so a healing cone never marked a root seen and healed it once per overlapping capsule - up to
  `7 x 2 = 14` times per tick. `TickCollider` now returns a three-state `BeamHit` (`None` / `Healed` /
  `Damaged`): any effect consumes the root once per tick, only damage spawns the flash, and a
  non-damageable child still cannot consume its enemy. No shipped cone heals; the bug was free.

### The bench got a third branch, because 1ir is the 1f7 miss again

`NewWorldTestGround`'s magic-model grid had two cases: falling rock, else `CreateProjectileDisplay`.
**Both new deliveries have no projectile body**, so both would have fallen through to the generic orb
and the bench would have reported "unchanged" for exactly the two deliveries that changed. It now has
three: rock, **cone** (mounting the live `SpellBeam.BuildConeVisual`), and **following circle** (the
spell's own radius + core). Both mount the **real** runtime builders rather than proxies, which is the
1f7 `BuildRockBody` precedent; `SpellBeam.ConeMouthFraction` is `public` so the bench cannot restate
the ratio and rot.

### Two deliberate deviations from the agreed plan, stated rather than buried

- **The familiar's orb is at the circle's CENTRE, not on its rim.** The circle already states the radius,
  and a central wisp reads as "bound to you" where a rim dot reads as one of several identical marks.
  The plan said rim; this is the one place the code knowingly differs, and the bench mirrors it.
- **`BoltSplashRadius` is passed `radiusMult = 1f` by summons**, so Continuous Fireball's 1.6 m burst
  stays fixed while its familiar grows on a charge. The ratio "bolt bursts smaller than the area it
  scans" reads better held constant. `SpellEffect.SplashRadius` still multiplies by whatever it is
  handed, so a chargeable projectile setting the field would scale correctly. Changing the summon to
  pass its scale would also widen Ember Effigy's bolts - out of scope, deliberately not done.

### Follow-up commit: `SizeScale`/`DurationScale` cannot be static (CS0120)

Fixed in the commit immediately after `824952f`. The first version declared both ladders
`public static` while reading `ChargeSizeBonus` — a **public instance field** (`SpellCaster.cs:27`,
there to stay inspector-tunable) — which is CS0120, "an object reference is required". Unity
rejected the whole assembly on it.

The instructive half is the comment I wrote *beside* the bug: "It is static because it never read
instance state", which the very next line contradicts. Rule 8 again — a comment naming a fact about
the code is a copy of that fact, and it was wrong from the moment it was typed. What made it survive
review is that the line *looks* legal: `SizeScale` is on the same class as the field, and every
internal caller sits in an instance method (`Execute` reads `transform.position` at line 49), so
nothing local to the file reveals the mistake. Only the one **cross-class, class-qualified** caller
could have — and that call site is the thing that broke.

Fixed by making both **instance** methods (the internal callers needed no change), and by
correcting the comment to record why they must stay instance rather than re-asserting a falsehood.
The preview call site (`PlayerController.Combat.cs`) became `beamCaster.SizeScale(charge)` off a
`SpellCasterRef` it now fetches in the existing guard — deliberately **not** a fallback constant,
because a fallback here would be a second spelling of `ChargeSizeBonus` (the same defect the
pre-existing `TryAoeTarget` guard already papers over with its own `0.8f`). With no caster the
preview hides itself rather than reporting a wrong reach.

**Lesson worth keeping: a class-qualified call (`Type.Member(...)`) is the only place CS0120 on an
instance member can show up, and it is also the only place the static-ness of a member is *visible at
all.** Grep for that spelling specifically after writing anything `static` that is in the same class
as its inputs.

### Play-test items (user, in Unity)

0. **First: does the project compile at all?** Everything below assumes the CS0120 fix landed; if
   Unity still reports an error, paste it and the whole sweep repeats rather than this list.
1. **Flamethrower**: hold to spray. Must read as a 44-degree wedge opening outward (0.84 m mouth -> 2.4 m
   tip), one impact flash per 0.5s tick, and **one** enemy must not take several ticks' worth of damage
   for standing in the wide end. Focus must drain ~11/s and stop cleanly on release or on empty.
2. **Aim preview**: Flamethrower's ghost must be the 44-degree wedge, charge-scaled; Searing Ray's must
   stay the narrow ~11-degree line. Both from `1iq`'s new preview path.
3. **Continuous Fireball**: cast it and walk away from the cast point - the familiar must come to you,
   sit on the ground (not hover, not sink), and keep firing. The circle must stay under it for the whole
   life, and must sit at your feet rather than on a nearby rooftop when you walk past a building.
4. **Charged Continuous Fireball**: confirm the body and the lifetime both grow together, and that it
   ends on its own at ~17.6s rather than running forever. **The bolts must NOT grow** — they are the
   fixed 1.6m burst by design, so bolt size staying put while the body grows is the correct read, not
   a missing scale.
5. **Save migration**: with a save that has Comet/Asteroid learned, load it and confirm both are the new
   spells at their old levels, and that `skillLevelsJson` keys came across.
6. **F4 spell-identity audit**: `172 / 172 / 0` must still hold, and both new ids must appear exactly
   once each.
7. **Bench (magic-model grid)**: Flamethrower must show the wedge on its pedestal (not a generic orb),
   Continuous Fireball the circle + core.
8. **Regression**: Searing Ray, Chain Lightning and the other beams must be visually unchanged; Ember
   Effigy and the other eight summons unchanged; Meteor / Meteor Rain / Earth Meteor / Rockfall must
   still fall boulders.

## 1iq. The aiming cone is the bow's alone - projectile magic and the throwing hammer keep the ray, and the cone is reporting a spread the game does not apply

**Status: shipped, NOT play-tested (rule 3 - no build or play-test runs in this project). Behaviour
change is visual-only: no damage, spread, range, speed or projectile behaviour was touched. One
known pre-existing defect was found and deliberately NOT fixed - see "The defect this exposed".**

### What changed

- **The spread cone is drawn only for a drawn flight** (`AmmoItemId != null` - arrows today, i.e.
  exactly the longbow). Every other projectile delivery now draws the straight trajectory ray alone.
- **Projectile magic loses the cone** (`PlayerController.UpdatePathPreview` now passes `spreadDeg = 0`
  instead of `8f * (1f - c)`). Magic's flight reach is **unchanged** - the preview still mirrors
  `SpellEffect`'s envelope and is deliberately not charge-scaled.
- **The throwing hammer loses the cone** (same gate, inside `ShowRangedPathPreview`).
- **The bow keeps its cone**, narrowing with the draw as before.
- The gate lives in **one place** - inside `ShowRangedPathPreview` - so both preview callers are
  covered by it: the aim-frame path (`UpdatePathPreview`) and the per-hand dual draw
  (`PlayerController.Combat.cs:249`). Neither caller gained a private copy of the decision.

### Why `spreadDeg = 0` is the whole mechanism

`ProjectilePathPreview.Apply` derives everything from `narrow = Clamp01(1 - halfAngle / 45°)`, so a
zero angle is not "hide the cone" by special-casing - it is the existing collapse path taken to its
end: ring material alpha `Lerp(0.55, 0, 1) = 0`, ring `widthMultiplier = 0`, ray alpha
`Lerp(0.12, 0.95, 1) = 0.95`. The cone and the trajectory therefore became **two independent claims**
a caller can ask for separately, rather than one shape whose presence also asserted a spread.

### Two reasons to drop the cone from magic, kept apart

They are not the same statement, so they are not the same edit:

1. **Magic has no spread at all.** Nothing in the spell path ever offsets the fire direction - there
   is no `insideUnitSphere` anywhere under `Spell*.cs`. The old `8°` fan was drawing outcomes the game
   does not have; the straight ray is the truthful readout. Aim feedback was not the thing being
   removed.
2. **The hammer is thrown, not drawn.** It consumes no ammo and has no draw-accuracy notion.

### The gate is ammo, not a hardcoded id - and the coupling that comes with it

`"longbow"` would be a second spelling of the weapon's identity that rots on a rename (rule 8), so the
gate reads `AmmoItemId != null` - which also hands a future crossbow the cone with no second edit.
**Stated cost of that choice:** the gate means "consumes ammo", so a future ammunition firearm would
inherit a *charge-narrowing* cone it has no mechanic for. If one is ever added this wants a real
`IsDrawnProjectile` flag on `WeaponData` instead.

### The defect this exposed (reported, not fixed)

**The cone does not describe the shot, for the bow either.** Two separate reasons, both read from the
code rather than inferred:

- `RangedWeaponBehavior.BeginAttack` applies `ApplySpread(dir, Clamp01(1 / accuracy))` at **every**
  charge level - `charge` is not in that expression. It scales damage (x2.5), speed (x1.5), lifetime
  (x2) and reach (x2), but never the spread. The preview's `spread * (1f - c)` claims a tightening
  the fire path never performs.
- `AccuracyFromDex` is `0` on **both** ranged weapons: nothing in `WeaponCatalog.Make()` sets it, so
  `RangedAccuracy = 1 + Dex * 0 = 1` and the cone both *starts* at maximum spread (~8.5°) and
  narrows to zero, while the fired shot stays at maximum spread forever. Dexterity's documented
  "ranged accuracy" scaling (`game-design.md` §3.4) is therefore inert for both weapons.

So 1iq scopes a visual that overstates the truth, and scoping it does not make it true. Making it
true - feed `charge` into the spread, and give the longbow a non-zero `AccuracyFromDex` - is a
**gameplay** change with real hit-outcome consequences, so it is left as an explicit decision rather
than done silently under cover of a visual task. This is AGENTS rule 7's "drawn and flush are
separate properties" one layer out: the cone was the *drawn* claim, and nothing checked the *shot*.

### Play-test items (user, in Unity)

1. Aim the **longbow** and hold: the cone must still be present and must still narrow with the draw.
2. Aim a **projectile magic spell** (Firebolt/Frostbolt): a straight ray, **no ring fan**, tinted in
   the spell's resolved colour, clipped at the first solid hit.
3. Aim the **throwing hammer**: a straight ray, no cone.
4. Dual per-hand draw must behave the same as the single-weapon aim path (both callers share the gate).
5. Confirm the ground AoE ring on zone spells is unchanged and still colour-matched.

## 1ip. The player's base weapon is the Mage's Staff, not the Wanderer's Iron Sword

**Status: shipped, NOT play-tested (rule 3 - no build or play-test runs in this project). Data-only
change: one constant's value. No weapon's stats, model, animation or combo track was touched.**

### What changed

- **`WeaponCatalog.StarterWeaponId` is now `"staff"`** (was `"iron_sword"`). That single constant is the
  only place that decides what the player starts holding.
- **The Iron Sword is untouched** and stays in the 15-weapon roster as an ordinary equippable: same
  stats, same `BuildIronSword` model, same 4-clip combo def. Only what gets *equipped at boot* moved.
- The constant's doc comment was rewritten. It used to read "Convenience default starter weapon id
  (Wanderer's Iron Sword)" - a comment that *named* the value, so leaving it would have shipped a stale
  claim in the same edit that invalidated it.

### Why this is one line and not a feature

The staff was already fully wired for a `WeaponCategory.Magic` weapon, so nothing new was needed:
`WeaponModelBuilder.BuildStaff` (dispatched at `WeaponModelBuilder.cs:368`), a **staff-specific** hold
pose in `WeaponRigBuilder.DrawPoseFor`'s Magic branch (`:293-300`, the `weapon.id == "staff"` sub-case),
a `WeaponAnimDef` with `K_Staff` (`WeaponAnimator.cs:274`), and
`WeaponDatabase.cs:27` -> `MagicWeaponBehavior`.

### The two readers, and why I checked them

`StarterWeaponId` is consumed in exactly two places, both data-driven:

1. **`NewWorldTestGround.SpawnAllWeapons`** (`:910`) - finds it and `WeaponRigBuilder.EquipInto`s it,
   then sheathes it. The bench and the legacy world both boot this way now.
2. **`CharacterInfoUI.CycleWeapon`** (`:365-366`) - inserts the starter at index 0 when the owned list
   is empty or lacks it, so the cycle-weapon fallback has something to land on.

### What I verified beyond the id (the real risk was category, not value)

A Melee -> Magic swap moves the player into different branches of several category switches, so I
looked for code that *assumes the starter is melee* rather than for the string:

- `CombatController.BothHandsMagic` requires **both** hands loaded; one staff is single-wield, which is
  the same loading state the sword was in. Unchanged.
- `PlayerController.IsMelee` / `IsMagic` and `MagicWheelUI`'s gate read the **equipped** rig's
  category, not the starter id. They now answer differently - that is the point.

### Verification (grep + reread, no build)

- Grepped `iron_sword` across `Assets\Scripts` (8 hits, 4 files) and confirmed only
  `StarterWeaponId` decides the starting weapon; the rest are the sword's own roster entry, its model
  builder + dispatch, its anim defs, and one unrelated doc-comment example in `RecipeData.cs:13`.
- Grepped `StarterWeaponId` (4 hits) and read both call sites in full.
- Grepped `WeaponCategory.Magic` / `.Melee` (32 hits) and read every site that could branch on the
  starter's category: `CombatController.cs:177,199-200,211,328`,
  `PlayerController.Combat.cs:165,213,228,489`, `MagicWheelUI.cs:255`,
  `WeaponRigBuilder.cs:293,308,449,552,574`, `WeaponDatabase.cs:25,27`.
- `tools\StaticChecks.ps1`: **0 candidates**, all 8 checks pass (ran it anyway - the changed file is not
  in `$files`, but a green run costs nothing and this is a boot-time data path).
- Reread the edited region for declaration order (rule 3: review is not compilation). The edit is one
  `const` initializer plus a doc comment - nothing that can fail definite assignment.
- skills: none applied - the artifact is a one-constant C# edit reviewed by a human; the installed set
  is DCC-side (Blender/Maya/ZBrush/Unreal) and the Unity skills target driving a live editor over MCP,
  which rule 3 forbids. Stated per rule 15's "silence is not a verdict".

### Deviations, stated

1. **Task id is 1ip, and it was nearly not.** I first wrote this task as `1io` in the code comment, the
   `game-design.md` bullet and this entry — and `1io` is a **real shipped task** ("Crater/deform audit
   lane moved F1 -> F13"). `1im` is also taken (the WeaponAnimator drift task). Caught by grepping
   `PROGRESS.md`'s `^## 1..` headings after the edits rather than before them, which is the order that
   let it through. All four mentions are now `1ip`. **Rule 8's "a comment that names an identifier is
   a copy of it" applies to a task id too** — and the copy is written *after* the fact here, so nothing
   in the code review would have caught it.
2. **The previous task (Deep Freeze) shipped as `1il`**, which is genuinely free but sits out of order
   between `1ik` and `1im`. It is already pushed, and rule 1 forbids amending, so it stays as-is rather
   than being renumbered. Noted here so the next reader is not confused by the sequence.
3. **This is a gameplay-visible consequence the user should confirm, not something I decided:** a fresh
   character now has the **magic wheel available at boot** (`MagicWheelUI.cs:255` gates on the equipped
   category), and every spell is scaled by the staff's `MagicDamageMult` 1.2 / `CastTimeMod` 1.0 /
   `CooldownMod` 1.0. That follows from the swap rather than being authored.

### Pending play-test items (needs the user in Unity)

1. **Boot the game** - the staff must be in the right hand, and (casual mode at boot, per the 1cr rule)
   **sheathed onto the body**, not floating in the hand.
2. **Draw it** (enter combat) - the staff's forward-lean + cant hold must read correctly in the hand.
   This pose was authored against the bench rack where it was already drawn; the *sheathed* staff pose
   is a separate question nobody has looked at.
3. **Cast a spell** - it must route through `MagicWeaponBehavior` and hit with Arcane damage, drawing
   FP. Confirm the staff's 1.2x magic-damage multiplier is what you want on the base weapon.
4. **Magic wheel** - confirm it is available from a fresh boot (this is a consequence of the swap).
5. **Cycle Weapon in Character Info** - with an empty inventory the fallback must land on the staff,
   not the sword.
6. **Iron Sword still works** - equip it from the bench rack: model, 4-clip combo and stats unchanged.
7. **No save migration is claimed.** If a save already recorded the iron_sword as equipped, that save
   keeps its sword; this changes what a *fresh* character is handed, and I did not touch any save path.

## 1f7. The falling rock was the one spell visual with no per-spell hook - Comet gets an ember tail, Asteroid a swarm

**Status: shipped, NOT play-tested (rule 3 - no build or play-test runs in this project). Purely a
visual-identity change: no delivery, no damage, no timing and no deform value was touched.**

### What changed

- **Added `SkyRockStyle { Inherit, Boulder, Swarm }`** (`SpellLook.cs`) as a fourth per-spell look
  axis, resolved through the existing `SpellLookProfile.SkyRock` -> `SpellLook.SkyRock` path. Both
  `SpellLook` constructors and all three `Resolve` return sites carry it.
- **`SkillFx.BuildRockBody(root, scale, tint, style, shader = null)`** extracted from `FallRock`, so
  the body can be built without a fall. `FallRock` gained a `style` parameter and now delegates.
  The **boulder branch is byte-for-byte the 1cy body** - Meteor, Meteor Rain, Earth Meteor and
  Rockfall draw exactly what they drew before. `BuildRockSwarm` is the new one: a lead rock at 0.62x
  the radius plus a flat fan of six at 0.75x.
- **Comet moved off `ProjectileShape.Comet` onto a new `ProjectileShape.EmberStreak = 12`** with an
  `EmberStreak` body builder: a stretched bright head plus six tapering ember cubes. Reached through
  `SpellLookProfile.DisplayShape`, never `spell.Shape`. **Comet also dropped
  `summonFallingRock`**, so it left the sky-rock family entirely - that flag was what made it draw a
  boulder in the first place.
- **Asteroid authored `skyRock: SkyRockStyle.Swarm`** and kept its `summonFallingRock` flag, so its
  blast radius, damage, knockback and Zone delivery are untouched; only the falling body changed.
- **Bench (`NewWorldTestGround.SpawnMagicModels`)**: a `SummonFallingRock` spell now mounts its real
  `BuildRockBody` formation instead of calling `CreateProjectileDisplay`. `LookKey` gained SkyRock's
  2 bits (34 -> 36) and `Describe` prints it; the F4 lane also counts the split per style.

### Why the axis is authored-only

`Inherit` always resolves to `Boulder`, and there is deliberately no `Families` array and no `Pick()`
for this axis. Impact and cast families jitter between looks that are all equally valid; a sky-rock
style is a *structural* statement about how the spell reads. Jittering it would have given Meteor a
swarm half the time. Same rule that keeps `DisplayShape` from ever handing a spell `Missile`'s homing.

### Deviations from the plan, stated

1. **The tail is ~2.0 m, not the planned ~2.3 m.** Measured off the shipped literals (head front at
   z=+0.31, last ember rear at z=-1.70). The doc comments carry the measured figures, not the plan's.
2. **The bench draws the formations at 0.35x the live blast radius**, which the plan did not specify.
   At true size a radius-4 formation is 7-9 m wide in a 3 m grid cell and swamps its neighbours, which
   are the whole point of a comparison bench. It is a stated bench constant, not a second opinion of
   the live value - `RockBodyBenchScale` names the reason in its own doc comment.

### Verification (grep + reread, no build)

- `tools\StaticChecks.ps1`: **0 candidates**, all 8 checks pass.
- Grepped `Assets\Scripts` for `FallRock` (declared in `SkillFx.cs`, **2** call sites -
  `SpellCaster.Cast.cs:233` and `SpellStorm.cs:106`; the bench calls `BuildRockBody`, not `FallRock`),
  `BuildRockBody` (2 call sites - `FallRock` and the bench), `SummonFallingRock` (now **5** spells),
  `ProjectileShape` switch exhaustiveness (`EmberStreak` case added), `spell.Shape` readers (exactly
  one, `SpellEffect.cs:83`, and only for `Missile` - so Comet's move to `EmberStreak` cannot have
  changed homing), and the `look: Look(` profile count (21 -> 23).
- **Reread every edited region** for declaration order and signature agreement (rule 3: review is not
  compilation). Caught and fixed three stale claims this way: the `SkyRockStyle` doc said "five"
  spells where it was six, "two different sizes" where the two ladders are 3-4 and 1.6-1.8, and the
  `EmberStreak` doc described a `rockBody` parameter the method does not take.
- skills: none applied - the artifact is a C# edit reviewed by a human; the installed set is DCC-side
  and the Unity skills target driving a live editor, which rule 3 forbids.

### Pending play-test items (needs the user in Unity)

1. **Fire Comet** - must read as a swift streak of burning light with a long tapering ember tail, and
   must no longer drop a boulder. Its tooltip says exactly this, so the model should now agree with it.
2. **Fire Asteroid** - must land as a spread: one rock on the aim point with six around it, all
   flush with the ground. Check it on a **slope** - the fan is deliberately flat, so no rock may float
   or sink.
3. **Fire Meteor, Meteor Rain, Earth Meteor, Rockfall** - must be visually **unchanged**. Any
   difference is a regression in the extracted `BuildRockBody` boulder branch.
4. **Bench**: on the magic-model grid, Asteroid shows the swarm and Meteor the boulder, drawn at a
   stated 0.35x of live blast radius. Confirm the swarm still fits its 3 m cell.
5. **F4 look audit**: `172 / 172 / 0` should still read. The reasoning for why adding an axis cannot
   move it is in `game-design.md` §3.8.4; the lane was **not** re-run by this task.
6. **Neither spell should feel different to fight**: same range, FP, cooldown, damage, knockback.

## 1f6. The detail LOD bands are deleted - a chunk's root mesh is its only surface at every distance

**Status: shipped, NOT play-tested (rule 3 - no build or play-test runs in this project). Render-path
change: it affects every real chunk at every distance, so rule 11 applies - you must restart the
session to see it.** Reported as "Lod1/Lod2 visually cover some chunks"; **no measurement was taken
first** (the user's explicit choice, so rule 7's measure-first habit is knowingly skipped and the
mechanism below is argued from the code, not from a readout).

### What changed

- **`ChunkLodManager` → `ChunkDistanceCull`** (file `git mv`'d with its `.meta`, GUID preserved).
  The class now does exactly one thing: hide a chunk root past the distance the world promises to be
  covered. The 1gh `EffectiveCullDistance` floor and the 1ea rolling 1024-entry burst are untouched.
  Renamed with it, because a cull that still says "LOD" invites the second surface back:
  `EnableLod` → `EnableChunkDistanceCull`, `LastSweepMs`/`PeakSweepMs` → `LastCullMs`/`PeakCullMs`,
  and the HUD line `lod sweep` → `cull sweep`. (Checked for collisions: `NewWorldSystems` already has
  an unrelated `EnableCulling` + `CullManager` for POI/enemy occlusion, so the cull names had to
  stay distinct from those.)
- **Deleted from `ChunkObject`:** `RefreshLodMeshes`, `BuildLodChild`, `EnsureLodChild`,
  `BuildVoxelLodChild`, `LodDetailCurvature`, `NeedsLodDetail`, the `LodDirty` flag, the
  `_lod1Go`/`_lod2Go`/`_lod1Mf`/`_lod2Mf` fields, the child toggles in `SetVisualActive` and the child
  teardown in `Release`. `ApplyMerged`/`PatchRegion` no longer set a staleness flag - and note that
  flag was **never the bug** (1f5 said so): a passing staleness test is not evidence that a surface
  carries the feature you are looking at.
- **Deleted `ChunkCornerGrid.Normals`.** Grepped first: the array was written in `BuildCornerGrid`
  and `PatchCornerGrid` and read by nothing else once the LOD builders went, so it was pure
  per-build waste (~961 Vector3s per chunk). Its removal also removes a seam-shaped trap - a corner's
  *normal* had no single owning corner under the 1ew ownership rule, so the array could only ever
  have held a plausible-looking wrong value.
- **Deleted the QA readout fields** (`Lod1`/`Lod2` child states, `band N`) from the F3 chunk-diagnostics
  line and updated both tooltips.
- **Kept deliberately:** `MergedChunkMeshData.Corners` (the collider cook, `PatchCornerGrid`, the
  low-poly root re-emit and the F3 corner audit all still read it), `VoxelMesher`/`VoxelChunkData`,
  and the distance cull itself.

### Why deletion and not a retune

1f5 measured the right thing and still shipped the wrong answer: a **fixed decimation stride silently
drops any relief narrower than its cell**, so a decimated child of a heightfield is a resolution bug
before it is a performance win. The 0.20 m Laplacian gate was genuinely *derived* (a discrete
Laplacian is exactly zero for any planar surface at any stride, so it measured relief rather than
scale), which is what made it safe to ship unmeasured - and it was still wrong, because it made the
terrain passable at **two different shapes** instead of one. Two further problems had no threshold at
all: a coarse triangle spanning convex ground sits in front of the real surface and **covers** it
(the report), and `BuildVoxelLodChild` decimated unconditionally with no gate to fix it. When the
second surface is only honest because a derived gate holds it honest, the surface is the bug.

### The trade, stated

Draw calls are **unchanged** (one root renderer per chunk either way - the bands disabled the root
rather than adding to it), but the ~336 chunks that used to draw a decimated child now draw their
full ~1800-triangle root: roughly **+400k triangles resident**. That is the price of one surface.

### Verification

- `tools\StaticChecks.ps1`: **0 candidates**, all 8 checks pass. `ChunkDistanceCull.cs` and
  `NewWorldSystems.cs` were **added to `$files`** by this task, so checks 1/4/7 cover them.
- Grepped `Assets\Scripts` for every removed symbol - `ChunkLodManager`, `RefreshLodMeshes`,
  `BuildLodChild`, `BuildVoxelLodChild`, `EnsureLodChild`, `LodDirty`, `LodDetailCurvature`,
  `NeedsLodDetail`, `LodMeshMode`, `BandIndexOf`, `LastSweepMs`, `PeakSweepMs`, `EnableLod`,
  `Corners.Normals` - plus a case-insensitive sweep for `lod`. Every survivor is an intentional
  historical note (`ChunkDistanceCull`, `ChunkObject` and `game-design.md`).
- **Not compiled** - Unity is the compiler (rule 3), and rule 11 means even a clean console would not
  show this: chunks already resident keep their uploaded mesh, so the change is only observable after
  a restart. skills: none applied - the artifact is a C# edit reviewed by a human; the installed set is
  DCC-side (Maya/Blender/ZBrush/Unreal) and the Unity skills target driving a live editor, which
  rule 3 forbids.

### Pending play-test items (needs the user in Unity)

1. **Restart the session** before looking at anything (rule 11).
2. Stand still and walk forward 40 m. Terrain must not change shape at 30 m or 60 m any more - those
   band switches are gone.
3. Confirm no chunk is visibly "covered" by a decimated surface (the original report), especially at
   the near/far boundary and over convex ground.
4. Watch `cull sweep` on the FPS overlay: unchanged in steady state, peak still dominated by the
   initial fill (1ik).
5. **Frame budget is the risk this trade creates** - ~+400k resident triangles. Check the frame time
   standing still and while sprinting; if it is worse, that is the cost of one surface, and the answer
   is a genuinely different far surface (a real decimated far shell), not re-adding a near band.
6. The 1f5 items are obsolete: there are no longer any LOD children to inspect.

## 1f5. Lod1/Lod2 silently redrew the crater - a decimated lattice drops relief narrower than its cell

> **SUPERSEDED by 1f6.** Everything this entry describes - the band children, `RefreshLodMeshes`,
> `NeedsLodDetail`/`LodDetailCurvature`, `BuildVoxelLodChild` - was deleted in 1f6, on the grounds this
> entry itself recorded: a decimated child of a heightfield is a resolution bug before it is a
> performance win, and a derived gate that keeps a second surface honest is a mitigation, not a
> licence. Its play-test list is void. Kept for the reasoning (the stride/surface argument and the
> per-cell-refinement rejection), not as a description of the world.

**Status: shipped, NOT play-tested (rule 3 - no build or play-test runs in this project). Render-path
change: it only affects chunks that already contain sub-cell relief, i.e. chunks someone has carved.**

The user reported: the crater looked right on first load and then "got altered". The far shell, the
near-chunk border fallback and a repeated cast were each checked and ruled out (see `THINKING.md` 1f5).
The mechanism that survived is the one nobody had looked at: **the LOD band children**.

### What changed

- **`ChunkObject.RefreshLodMeshes` now picks the stride.** `BuildLodChild` was called with hard-coded
  strides 2 and 3. It now passes `1` for both when `NeedsLodDetail` says the chunk holds relief finer
  than a decimated cell can carry, so a carved chunk draws the 1 m lattice at every band.
- **`NeedsLodDetail(corners.Y, grid)` is a discrete Laplacian**: it flags a chunk when any lattice node
  departs from the average of its four 1 m neighbours by more than `LodDetailCurvature` = **0.20 m**.

### Why the threshold is derived, not chosen

`NeedsLodDetail` measures relief rather than scale because a locally planar surface has a **zero**
discrete Laplacian at any stride, so untouched ground scores 0 however far away it is. Both sides of
the 0.20 m gate come from `TerrainNoiseGenerator`'s octave table:

| | 1 m Laplacian |
|---|---|
| five-octave field, worst case over `Continental/Hills/Detail/Roughness/PivotAngle` (`A·(kd)²/4`) | **≈0.016 m** |
| 1f3's terraced crater wall, 0.475 m of rise inside one lattice cell | **≈0.24–0.95 m** |
| a smooth *un-terraced* dish (curvature ≈0.15 m) | ≈0.15 m - deliberately **below** the gate |

So the gate sits ~12x above the natural floor and ~2.4x below the authored terrace step. A smooth dish
that still looks slightly softened at 60 m is what LOD is for; the gate does not chase it.

### The decision that was not available: per-cell refinement

The obvious version subdivides only the distorting cells. **Rejected**: a cell that refines while its
neighbour stays decimated leaves the shared edge as a polyline against a straight chord - the T-junction
row this project already fought in the far shell before 1ej, and it reads as permanent thin lines
rather than as a resolution change. With no compiler available (rule 3) a mesh rewrite that can crack is
not a gamble worth taking. Refinement is therefore **whole-chunk**, which cannot crack: a uniform stride
has no transitions. The cost is honest and bounded - a carved chunk keeps the root mesh's 900 quads out
to the last band instead of 225/100. Untouched chunks are unaffected.

### Verification

- `tools\StaticChecks.ps1`: **0 candidates**, all 8 checks pass. `ChunkObject.cs` was already in `$files`
  (line 64, added by 1ex), so checks 1, 4 and 7 covered this edit rather than skipping it.
- Brace balance re-counted by hand on the edited file: 80/80.
- `BuildLodChild` has exactly one caller (updated); `TerrainChunkCoord.CornerGridSize` was already the
  lattice stride used at line 615. With `step = 1`, `axis = 31` and the node index runs to 960, which is
  the last slot of a 31x31 grid - checked, because a stride that misses the chunk rim leaves a seam.
- **Not compiled** - Unity is the compiler (rule 3). **No measurement was possible**: the user has no
  F13 key, so the read-only lane that rule 7 asks for on a geometry report could not be run or read. The
  mechanism was established by reading instead, and the threshold was derived from the octave table so
  that it does not depend on a readout. skills: none applied - the installed set is DCC-side and the
  Unity skills target driving a live editor, which rule 3 forbids.

### Pending play-test items (needs the user in Unity)

1. **Restart the session** (rule 11: render-path change, and this one is read off `_merged.Corners`).
2. Cast a crater, walk **backwards 35 m**, and confirm the crater has **not** changed shape. Repeat at
   70 m for the Lod2 band. This is the whole bug.
3. Walk back to it and confirm nothing pops.
4. Watch the `lod sweep` ms line on the FPS overlay after carving several craters - a carved chunk now
   builds a 961-vertex child instead of a 256/121-vertex one, on band switch only.
5. **Untested, recorded as the same class of defect:** `BuildVoxelLodChild` still decimates to 2/3
   unconditionally, and `VoxelTerrainEnabled` is false by default. Same coupling, one dimension down
   again; not touched here.

## 1f3. The crater is a terraced spherical cap, not a smooth cone (behaviour change)

**Status: shipped, NOT play-tested (rule 3 — no build or play-test runs in this project). This is a
behaviour change to the shared deform path: every Crater in the game is affected — projectile dents,
Earth spells/zones and tool digs all funnel through `WorldStreamer.DeformAt`.**

The 1f2 measurement shipped and the user acted on it: the crater read as a "smoothed out blanket".
That is a complaint about the profile's **continuity**, which no amount of depth or rim fixes — 1ez's
rim was already there. Two changes, both of them the ones F13 section E was built to judge.

### What changed

- **The profile is a spherical cap.** `capR = (reach² + capDepth²) / (2·capDepth)` with
  `capDepth = min(CraterStep, reach)`; depth at radius `d` is `sqrt(capR² − d²) − (capR − capDepth)`,
  which is `capDepth` at the impact point and exactly **0** at `reach`. The old cone
  (`1 − dist/reach`) was a straight line, so every node in the footprint held a distinct height and
  the 1 m lattice had no level breaks to draw. The feather to untouched ground is unchanged; only the
  shape between the centre and the rim moved.
- **The signed offset is snapped to a terrace ladder** of `clamp(reach · 0.25, 0.30, 0.80)` m before it
  is written. A fraction of reach, so one ratio reads as terracing at every size.
- **`CraterRimLift` 0.55 → 0.90 m.** Re-derived, not nudged: the cap digs deeper than the cone at
  every radius (≈0.94 m at the lip's inner edge where the cone reached ≈0.47 m), so the old lift would
  have left 1ez's rim barely proud of grade (~0.10 m) — a new profile silently regressing an old
  feature. Net rim is back to ≈0.44 m continuous / **+0.475 m** as a snapped terrace, still under
  `stepOffset` (0.5 m).

### Three decisions that are not obvious from the diff

- **The snap is on the OFFSET, not on the write.** That placement is what keeps both invariants: the
  lip still `Max`es against `current` (repeat cast → same offset → same target, idempotent) and the
  excavation still subtracts a non-negative amount (unbounded downward ratchet, 1cv). The declared
  cost: a cast moves the floor by `CraterStep` **within ± half a terrace** rather than exactly — a
  1.9 m-reach projectile digs **0.95 m** on its first cast, not 1.1 m. The deep core cannot round away
  (`CraterStep / terrace ≥ 1.375`), so the ratchet stays unbounded in practice, not just in intent.
- **Absolute-Y quantisation was rejected, and the expensive reason is `IsFlatTile`.** Snapping the
  height instead of the offset gives genuinely flat treads on a *slope* (two adjacent corners on one
  rung of a world-wide ladder), but it makes depth a function of absolute elevation, and it floods
  `ChunkMeshGenerator.IsFlatTile` → `ChunkContainsFlatTile` → **`FullRebuildChunk`**, a path sized for
  slab side walls. Offset snap keeps flat treads to ground that is genuinely flat (including the test
  platform) and leaves sloped ground on ramps-with-hard-breaks. **If the terrace read is too weak on a
  slope, that is the trade to revisit — not a smaller epsilon.**
- **The rim wall is now steep by design:** 60° at a 1.9 m reach, 73° at a 1.5 m tool dig, against the
  cone's 34°. The user does not need a walkable bowl; a walkable crater is a *different shape*, not a
  tuning of this one. Above `slopeLimit` (never assigned by this project) the player cannot climb out.

### Verification

- `tools\StaticChecks.ps1`: **0 candidates**, all 8 checks pass.
- Derived by hand and cross-checked twice (see `THINKING.md` 1f3): rim/depression crossover
  t ≈ 0.68 → **0.72**, peak net rim **+0.443 m** continuous (**+0.475 m** snapped), centre dig
  **−0.95 m** snapped, rim-wall slope 60.1° / 72.5°, and the identity `sqrt(capR² − reach²) = capR −
  capDepth` confirmed numerically for both a projectile and a tool reach. These are **derived, not
  measured** — F13 has to confirm them on the resident set.
- `Mathf.Max(0f, capR² − dist²)` guards `dist > capR`, which `loopReach` can reach when the 1i9 facet
  skirt is live; for `dist > reach` the cap is negative, so `Clamp01` yields the 0 the cone gave.
- **A dormant interaction is documented, not pre-empted:** 1i9's `CraterFacetSkirt` returns 0 unless
  `EffectiveLowPolyStep > 0`, and 1ia holds that flag false, so the skirt contributes nothing to
  `offset` today. If facets are ever re-enabled, a skirt under half a terrace would snap back to 0 and
  lose its guarantee. Recorded in the code next to the snap.
- Grepped every consumer of the crater profile (`CraterRim*`, `CraterStep`) across
  `Assets\Scripts` — no other site re-derives the profile, so there is no second spelling to update.
- **Not compiled** — Unity is the compiler (rule 3). skills: none applied — the installed set is
  DCC-side (Blender/Maya/ZBrush/Unreal/Scenario) and the installed Unity skills target driving a live
  editor or `-batchmode`, which rule 3 forbids; this change is a reviewed C# edit.

### Pending play-test items (needs the user in Unity)

1. **Restart the session** — this is a deform-path change, so it is visible immediately, but a crater
   already in the resident set keeps its old geometry until re-carved.
2. Cast a crater and press **F13**. Expect section E's ladder verdict to flip from `CONTINUOUS (no
   terrace exists)` to **`UNIFORM LADDER (quantised carve)`**, with `riserEdges > 0`, `flatTreads > 0`
   on the test platform's flat ground, and `maxRiser` ≈ one terrace (0.30–0.48 m).
3. Re-cast the **same spot** and confirm the floor still drops every cast (the ratchet) and the rim does
   **not** grow (1ez idempotency). Both are invariants this change deliberately preserved.
4. Cast a crater **on a chunk boundary** and read `bandsAtChunkRim`. It was structurally 0 for a
   depression; the snapped **raised** rim terrace is the first thing in this path that can emit a real
   slab face against the pristine-noise fallback. Non-zero here is a finding, not noise.
5. Try to walk out of a crater. Expect to fail on the 60°+ wall — that is the agreed trade; report it
   only if it is a *hard* trap (stuck with no way out) rather than a slow climb.

### Not done, deliberately

- **1f4 (the literal sub-metre facet shell) is untouched.** A shell overlay cannot survive a 3–6 m
  sampled lattice without its own poke-through problem, and a proper version needs a render-hole data
  path, a shell mesh + collider, lifecycle handling and a save-format `1 → 2` bump. That is a separate
  task, and 1f3's readout is what decides whether it is still wanted.

---

## 1f2. F13 section E — is the crater STEPPED at all? (read-only; measures the proposal, changes nothing)

**Status: shipped, NOT play-tested (rule 3 — no build or play-test runs in this project). Strictly
read-only: no chunk, lattice, mesh, collider, save or deform state is touched, no rebuild, no
re-stamp, no forced poll. Behaviour change is deliberately NOT in this commit.**

Follow-up to 1in/1io/1iz. The user wants every crater to become a scale-derived low-poly ball-cap
with a stepped, faceted surface — "facet shell over a low-poly ball-cap, no dish" — with depth and
step both scaling from the projectile, later hits re-centring on their own collision point and
carving deeper, and the 1ez rim kept and terraced too. Rule 7 requires the measurement first and the
fix in a **separate** task, so this commit only adds the lane section that will judge the fix.

### The gap sections A–D could not close

A/B/C/D all describe a *smooth* bowl in detail — deepest dig, radial profile, corner spread above the
1ew refinement trigger, above-pristine rim count. Every one of them can be clean while the shape is a
smooth cone, because **none of them asks whether the carve is stepped at all**. "Deep" and "stepped"
are separate properties, exactly as 1hy's "drawn" is separate from "flush" (rule 7). E asks the
missing question and prints a verdict line into the same VERDICT block.

### What section E measures

- **The level ladder** — distinct carved corner heights, min/max gap, and a `UNIFORM LADDER` verdict
  when the gap spread is under `CraterAuditLadderUniform` (0.1 m). Gated on the carve's own premise:
  with no dig past the threshold it prints `<no crater in band>` and classifies nothing, because a
  ladder built from untouched terrain is a positive-looking number about nothing.
- **Flat treads** — fraction of *touched* tiles whose four corners are equal.
- **Riser and boundary edges** — lattice edges with a step between two carved corners, and edges with
  one carved endpoint (the sub-step band, i.e. where quantising shrinks the effective radius).
- **Walkability** — the tallest riser as a collider slope at the 1 m lattice pitch, printed against
  the **live** `CharacterController.stepOffset` / `slopeLimit`.
- **Chunk-rim side-wall bands** — how many of the footprint's tile edges sit above the pristine-noise
  fallback that `ChunkMeshGenerator.EdgeHeights` uses for an unloaded out-of-chunk neighbour.

### Three decisions that are not obvious from the diff

- **Membership and value use different references, on purpose.** Membership is *deviation from
  pristine* (either direction — the 1ez rim is a raise, and a rim terrace is as much a step as a dish
  terrace); the value measured is the *raw* height. A carve writes `refY + offset` against ONE
  reference height, so the pristine slope underneath is overwritten rather than added to and the
  carved heights are exactly the authored offsets plus a constant — the ladder, directly. Measuring
  dig-below-pristine as the value (the obvious shortcut) folds the untouched slope back in and
  reports "CONTINUOUS" for a perfectly quantised carve.
- **A zero is reported as `UNKNOWN`, never as `0`.** If B clears the dig threshold and E finds no
  corner deviating by it, the two read the same corner grid through the same gate, so it is a
  contradiction, not a result: the band and the dig disagree. Per 1i2's retraction, the replacement
  for a retracted number is *unknown*, not zero.
- **Thresholds are read, not copied** (rule 8). This project writes `stepOffset = 0.5f` in exactly
  one place and never assigns `slopeLimit` at all, so a literal copy in the lane would be a second
  spelling that rots silently.

### Verification

- `tools\StaticChecks.ps1`: **0 candidates**, all 8 checks pass. Check 4 initially flagged the
  `top` local in the seam walk; that was a genuine false positive (every switch arm assigns it), so
  the edge table was rewritten as a switch **expression** returning corners, neighbour and Height
  slots as one value — a check that flags a false positive on the first file it covers is a check
  whose silence has stopped meaning anything (rule 3).
- `ChunkData.Size` (1 m), `ChunkData.IsValid`, `TerrainChunkCoord.FromTile` and its `==` operator
  confirmed by grep against the declaring files before use.
- **Not compiled** — Unity is the compiler (rule 3).

### Pending play-test items (needs the user in Unity)

1. Press **F13** standing next to an existing crater. Expect `E terraces` with a **CONTINUOUS** verdict
   and `flatTreads 0/N` — the current carve is a smooth cone, so this is the "before" number.
   **(Superseded by 1f3, which changed the carve: a crater cast *after* 1f3 reads `UNIFORM LADDER`
   instead. A crater that existed before the change keeps its old geometry until re-carved, so it
   still reads `CONTINUOUS` — which is exactly the rule-11 mixed-resident-set case the fingerprint
   section exists to catch.)**
2. Press F13 on untouched ground. Expect `<no crater in band: nothing to step>`, not a ladder.
3. Cast a crater **on a chunk boundary** and read the seam line; expect `bandsAtChunkRim 0` today
   (a depression never sits above the noise fallback) — that is the number that must stay 0 after the
   rim becomes terraced.
4. Report the F13 output verbatim; it is the acceptance readout for 1f3 and the baseline 1f4's facet
   section is built against.

---

## 1f1. Standing torso was a flat slab — depth raised to a volume (both genders)

**Status: shipped, NOT play-tested (rule 3 — no build or play-test runs in this project). One
`MakePart` size scalar changed; no mesh, pivot, name, hierarchy or save change.**

Follow-up to the 1e2/1e4/1e8 shouldered-torso work: the user reported the whole torso "too
slab-like/flat". Root cause is a depth scale, not the silhouette tables.

### The bug

Standing `Body` is `size = (0.44, 0.80, 0.25)` (female `0.40`), `MapBuilder.PlayerModels.cs:135`.
With `dB` peaking at `0.50`, the world half-depth never exceeds `0.50·0.25 = 0.125`, while half-width
reaches `0.72·0.44 = 0.317` — a **2.4–2.8:1 flat plate**. `dB` only ranges `0.42–0.50`, so the front
and back faces are nearly parallel and every silhouette bulge in `wB` is width-only. The seated body
(`0.34, 0.60, 0.28` → ≈1.75:1) is the rounder one, confirming the standing part is the outlier.

Width cannot be reduced to fix it: the shoulder shelf must reach the ±0.28 pivot, i.e.
`0.68·size.x ≥ 0.28` → `size.x ≥ 0.412`, so width is pinned. Depth is the only free lever.

### What changed

`Assets\Scripts\Models\MapBuilder.PlayerModels.cs:135` — standing `Body` `size.z 0.25 → 0.32`
(both genders; `size.x`/`size.y` unchanged).

| row | now (half-depth, w:d) | after (half-depth, w:d) |
|---|---|---|
| chest t=.625 | .125, 2.54 | .160, **1.98** |
| hip t=.125 | .125, 2.39 | .160, **1.87** |
| waist t=.375 | .105, 1.68 | .134, 1.31 |
| shoulder t=.775 | .110, 2.80 | .141, 2.19 |
| female chest | .125, 2.30 | .160, **1.80** |

### Why it is safe / scoped

- The part mesh is **size-independent and cached**; `MakePart` applies `localScale` per instance, so
  only the standing instance changes. Seated has its own `size.z = 0.28` line (`:258`) and `SitTorso`
  uses the `"SitTorso"`/`"Chest"` tables — untouched.
- The standing `Body` size is read only at this one `MakePart` call (grep).
- Clearances re-derived: crown row `0.20·0.32 = .064` still tucks under the neck radius `.075`;
  female skirt `.15` still laps the bottom cap `.147`; back hair still protrudes; arms/thighs
  (`.06`/`.07` half-depth) sit inside the torso.

### 1f1-status
- Implemented; verified by grep + reread (rule 3, no CLI build): the one edited line reads
  `size.z 0.32`; `wB`/`dB` arrays unchanged (9 entries each); no other consumer of the standing
  `Body` size; no pivot/joint/name/hierarchy touched. `tools\StaticChecks.ps1` does not cover
  `MapBuilder.PlayerModels.cs`, so this is grep + reread only. game-design §3.5 + PROGRESS updated
  same pass (THINKING omitted — trivial arithmetic).
- Play-test (pending, user runs Unity): standing male + female torso reads as a volume, not a plate;
  side view chest/hips depth ≈ width; shoulder/hip joint balls still read as caps; arms don't clip the
  deeper chest while swinging; neck/crown seam intact; female skirt still covers the pelvis; seated/
  sit visually unchanged.

## 1ez. Raised crater rim (the monotone cone gets its missing positive term)

**Status: shipped, NOT play-tested (rule 3 — no build or play-test runs in this project). One branch of
`DeformAt` changed; no save format, no version bump, no lattice.**

This is the shape half of the 1in report, shipped on its own after 1ex (collider). The user chose
rim-first over bundling it with the stored fine lattice (1ey) precisely so the collider cost (1ex) and
the shape change (1ez) stay separately attributable, and so the save-format migration in 1ey waits for
a measured reason.

### The bug (1in / THINKING H56)

`WorldStreamer.Deform.cs`'s crater branch was `target = current - s * CraterStep` — a **monotone
subtraction with no positive term anywhere**. A rim is a positive raise, so no amount of resolution
could produce one; the dent could only read as a smooth funnel. H55 also showed 1ew's adaptive
refinement never fires for a crater (a 1.9 m-reach, 1.1 m-deep dish lowers a tile's worst corner by
~0.76 m, far under `DefaultRefineThreshold = 2.5`), so the shape, not the geometry, was the finding.

### What changed

`Assets\Scripts\World\Streaming\WorldStreamer.Deform.cs`:
- New rim constants next to `CraterStep`: `CraterRimInner = 0.55`, `CraterRimPeak = 0.80` (fractions of
  `reach`) and `CraterRimLift = 0.55` (an **absolute** metre lift, deliberately not a fraction of
  `CraterStep`, so the net ~0.44 m rim stays under the 0.5 m `stepOffset` at every crater size). The lip is a smooth bump across the
  outer band of the footprint, **zero at both ends**.
- The crater branch now computes a **signed profile** `offset = lipBump * CraterRimLift - depression`:
  - `offset > 0` → raised lip, target `Mathf.Max(current, baseY + offset)` — **idempotent** (target
    relative to pristine noise) and bounded, exactly the raised-shape rule.
  - `offset <= 0` → the original per-cast excavation (`current + offset`, ratchets down), unchanged.
  A signed profile is used instead of `Max(dishTarget, lipTarget)` of two separate curves, because the
  two curves are far apart where the lip support begins and a hard `Max` jumps there; the signed form
  crosses zero continuously (~0.68 × reach).
- The raise is ringed by the same 0.9 m caster keep-out as the other raised shapes (the lip band starts
  at `0.55 × reach ≥ 0.935 m`, so the guard never actually fires, but it makes the invariant explicit
  if the constants move). The player lookup now runs for every shape, not only raised ones.
- XML doc updated to describe the lip.

### Why no save/lattice change is needed

The rim is written as ordinary **1 m corner heights** through the existing `ApplyHeightEdits` path, and
the collider is re-cooked from the restamped lattice, so it is automatically physical at 1ex's 1 m
resolution. The old crater dish already had this property; the lip inherits it for free.

### 1ez-status — play-test in Unity

- [ ] Fire the projectile at flat ground. The dent should now read as **bowl-with-rim**, not a smooth
      funnel: a ring of raised ground (~0.4 m) around the pit, tapering back to grade.
- [ ] **Repeat-cast the same spot.** The pit deepens (unchanged); the rim does **not** grow taller —
      that is the idempotency of the raise branch.
- [ ] **F13 (1in/1io lane), section D**: corners **above** pristine should now be non-zero in the
      crater footprint. Before 1ez section D was the "is a rim authored" answer, and it read zero.
- [ ] Cast a crater with the player standing inside the footprint; confirm no violent depenetration
      (the keep-out ring covers the raise).
- [ ] Earth Wall / Ring / Pillar / Spikes still behave (the non-crater branch is untouched).

### Verification performed

Grep + reread, no build (rule 3). `tools\StaticChecks.ps1`: **0 candidates** across all 12 files.
Hand-evaluated the profile at several radii (t = 0.55, 0.67, 0.68, 0.80, 1.0) to confirm the
signed-profile crossover is continuous (~0.68 x reach), the bowl is ~0.47 m down at the inner band,
and the rim peaks ~0.44 m above pristine at `reach = 1.9` (Radius 1.4 + feather 0.5). Corrected the
in-code figures, which had quoted a stale 0.39 m inner bowl and called `CraterRimLift` a fraction of
`CraterStep` when it is absolute. No save/version/tile-format token changed, so persistence is untouched
by construction.

`skills: none applied` — a single self-contained Unity C# numeric branch reviewed by a human, not a DCC
artifact; the `scenario-unity-*` skills drive a running editor over MCP/`-batchmode`, which rule 3 bars
here (informative at best, never authoritative).

---

## 1ex. Collider lattice 2 m -> 1 m (the player no longer walks through visible craters)

**Status: shipped, NOT play-tested (rule 3 — no build or play-test runs in this project). One constant
changed; the rest of the commit is stale-comment repair + one static-check coverage extension.**

This is the first of the craters/terrain tasks (1ex collider; then **1ez raised rim**, then the
deferred **1ey stored fine lattice**; **caves** split out to their own later task — `1ew` is **already
taken** by the adaptive stretch-split at line 3140, so caves must take a fresh id). It is deliberately
first and standalone because it fixes a bug that exists **now**, with no
dependence on the others.

### The bug

The player has **no separate ground raycast**. `PlayerController.Movement.cs:143` calls
`CharacterController.Move`, which sweeps the chunk `MeshCollider` directly. That collider was built at
**2 m** (`ChunkColliderDecimation = 2`, every 2nd node of the 31×31 corner grid → 256 verts / 450 tris),
while the *render* surface samples every 1 m node.

So the ground you stand on was a quarter of the render's resolution, and the failure is not merely
"blocky": a **1.1 m-deep crater centred on an odd x or z had no sampled collider node anywhere inside
its footprint**. The visual dish sat entirely between four collider vertices, so the player walked
straight over a pit they could see. Roughly half of all carved craters (those with an odd footprint
centre) were non-physical. This is visible today, before any fine-lattice work, and is why 1ex was
sequenced ahead of 1ey/1ez rather than bundled with them.

### What changed

`Assets\Scripts\World\Terrain\ChunkMeshGenerator.cs`:
- `ChunkColliderDecimation` **2 → 1**. At 1 m the collider is 961 verts / 1800 tris per chunk.
- The constant's doc comment now states the real premise: the player sweeps this collider directly, the
  2 m step is what let a visible crater have no collider node, and **the 1hi "~4x cheaper cook" claim is
  dead at this value** (961/1800 is render-resolution). The mechanism is kept — not inlined as a literal
  `1` — because `ColliderStep` must keep preferring the chunk's own low-poly facet step, and because it
  still drops the render mesh's side walls and refined blocks.
- The build-site and `BuildDecimatedCollider` comments updated, plus the 1 m vertical-step warning.

Stale-comment sweep, same pass (all now derive from the 1 m value):
- `ChunkObject.cs`: `RefreshCollider`, `RebuildColliderSurface`, `_colliderMesh`, the `ApplyMerged`
  (1hi) note, and the `PatchRegion` re-cook note. The last one had claimed the 2 m surface tracked the
  excavation "exactly", which was the bug restated as a feature.
- `TerrainChunkMeshData.cs`: `ColliderVertices` / `ColliderTriangles`.
- `WorldStreamer.cs`: the `ColliderRingRadius` tooltip's stale `~7k-tri` figure replaced with the real
  ring-7 number (225 bodies × 1800 tris ≈ **405k** collider triangles, was ≈101k).

`tools\StaticChecks.ps1`:
- `$files` gained `ChunkMeshGenerator.cs` and `ChunkObject.cs`. This commit only changed a constant and
  comments in them, but **1ey/1ez edit `ChunkMeshGenerator` (fine-node read, `IsRefinable`) and 1ez edits
  `Deform`**, so they belong in coverage before they carry a real edit (rule 3: a file absent from
  `$files` is silently outside checks 1/4/5/7). Added now, verified: both report balanced and produce
  **0 new candidates**, so the addition adds coverage without noise.

`game-design.md`:
- §2.5's collider bullet and the §3.x "collider lattice" bullet rewritten to the 1 m value, the dead
  `~4x` claim, and the two carried constraints (horizontal-quads-only ⇒ 45° ramp at 1 m cliffs; step must
  divide 30 ⇒ legal set {1,2,3,5,6,10,15,30}).

### Why 1 m and not something cleverer

Three options were put to the user: leave 2 m (rim/craters stay visual-only), drop to 1 m globally, or
1 m only inside the budget-exempt `MustCollideRadius` zone. The user chose **global 1 m**. The hybrid was
rejected because the collider is cooked **once per chunk** and cached; a radius-dependent step would need
a live re-cook/swap mechanism on an already-cooked `MeshCollider`, i.e. new machinery, for a 0.33 m rim
already below `stepOffset = 0.5 m`. Global 1 m is one constant and is measured on the existing F2 lane.

### 1ex-status — play-test in Unity

- [ ] **Crater on an odd-centre footprint now sinks.** Fire a magic projectile at ground whose impact
      footprint centres on an odd integer x or z; walk into the middle. Before 1ex you stand *on* the
      dish; now you drop into it. (This is the whole point of the task.)
- [ ] **F2 frame budget, before/after the constant.** The ring is 225 bodies at 1800 tris each. Confirm
      the collider cook and the per-`Move` broadphase cost are acceptable; 1ik's lane is the instrument.
- [ ] **Cliff traversal.** Climb a 1 m vertical terrain step. The collider is horizontal quads only, so
      the step is a 45° ramp — exactly the default `CharacterController.slopeLimit` (never assigned in
      this project). If cliffs now refuse to climb, that is this, not a streaming bug.
- [ ] Stream in / walk out normally around the ring-7 boundary; confirm no falling through rendered
      ground as colliders cook at 2/poll.
- [ ] Fast-travel-less sprint across a chunk boundary (ring crossing) — the deferred-cook path
      (`MaxColliderCooksPerPoll`) with the larger cook must still keep the ground under the player.

### Verification performed

Grep + reread, no build (rule 3). `tools\StaticChecks.ps1`: **0 candidates** across all 12 files,
including the two newly added. The only source-token change is `2` → `1`; every other edit is a comment.
Reread the wording of each of the 9 comment sites and the constant against the actual arithmetic
(`axis = 30/1 + 1 = 31` → 961 verts, 5400 indices = 1800 tris; max corner index 960 < `Corners.Y.Length`
961). `NewWorldTestGround` mirrors neither value, so no test-platform mirror to chase.

**Rule 3 caveat, stated: `ChunkMeshGenerator.cs`/`ChunkObject.cs` were outside `$files` until this
commit, and a balance/paren scan is not a compile.** The change is one integer literal, so the only
realistic compile risk would be a type/const mismatch, and `ChunkObject.ColliderDecimation` is a `const`
alias of the same `int` — no signature crosses the edit. Unity is still the compiler.

`skills: none applied` — this is a Unity C# constant + comment change reviewed by a human, not a DCC
artifact; the `scenario-unity-*` skills drive a running editor over MCP/`-batchmode`, which rule 3 bars
here (informative at best, never authoritative).

---

## 1io. Crater/deform audit lane moved F1 -> F13 (F1 was the combat-mode toggle) + StaticChecks check 8

**Status: shipped, NOT play-tested (rule 3 - no build or play-test runs in this project). No
terrain behaviour changed; this is a QA-key rebind plus a static check.**
The user reported that F1 is the button they use for the fighting-mode switch. They were right, and
the lane shipped on a key that was already taken.

### The bug 1in shipped

1in picked **F1** after grepping for `Key.F1` and `KeyCode.F1`, finding no references, and
documented the key as free in both the code and `HANDOFF-1in.md`. It is bound:
`Player\PlayerController.Interactions.cs:521` reads `Keyboard.current.f1Key` and calls
`ToggleCombatMode()`. That is the Input System's **property-name** spelling, which neither grep
pattern matches. Pressing F1 therefore fired **both** the audit and the combat-mode toggle, so any
readout taken that way was measured while weapons drew and `ToolManager` reset the selection
underneath the player. Any F1 readout from before this commit should be treated as suspect.

The corollary is a lie that had been propagating since 1hy: **"F1 is a skill hotkey"**, recorded as
the *reason* F2/F3/F4 were considered free. It appears in four tooltips (`NewWorldTestGround.cs`
F3/F4/F2 lanes plus prose) and in `PROGRESS.md:1511`, `:2284` and `THINKING.md:2434`. 1ik had
already flagged it as unsourced and declined to repeat it; it now has an answer and it was wrong.
F1 is the **combat toggle**. Those older entries are left as history, corrected here.

### What changed

`Assets\Scripts\Opt\NewWorldTestGround.cs`:
- `CraterAuditKey = Key.F1` -> **`Key.F13`**. F13 verified free in both spellings against the
  Input System 1.19 `Key` enum (`Library\PackageCache\...InputSystem\Devices\Keyboard.cs` lists
  `F13`-`F24`), and against every binding form in `Assets\Scripts`.
- Four tooltips corrected. The F13 tooltip now records *why* the key moved and names the binding
  that was missed, so the next reader does not re-derive it.

`tools\StaticChecks.ps1` — **new check 8**, "a QA lane key must not already be bound elsewhere
(one key, two owners)". It parses every `public Key <x>Key = Key.<Y>;` lane default out of
`$files`, then scans all of `Assets\Scripts` for any second binding of that key across all three
Input System spellings (`Keyboard.current.f1Key`, `Keyboard.current[Key.F1]`, `kb[SomeLaneKey]`),
skipping each lane's own declaration and its own indirection site.

Two false-positive rounds got it wrong first and both are recorded in the script's header:
- A bare substring search on the key name (`f4`, `Key.F4`) matched `Leaf4`, `#44FF44`,
  `Pagoda_Roof4` and four tooltip strings: **54 candidates**. Rule 7's false-positive failure,
  reached immediately. Fixed by anchoring the pattern on a `Keyboard`-typed expression.
- The anchored pattern still missed the real bug because `Keyboard.current` is two member hops,
  so the captured token was `current` rather than `f1Key`. Fixed by allowing `current.` as a
  middle segment and by normalising `f1Key` -> `F1`.

**Verified both ways, per rule 7** (a check nobody has seen fail is not a check): reverting
`CraterAuditKey` to `Key.F1` fires check 8 naming `PlayerController.Interactions.cs L521`; the
restored `Key.F13` goes quiet.

Docs: `game-design.md` §2.2 (lane key paragraph) and the QA-toggle table; `AGENTS.md` rule 7 (new
bullet on key-grep spellings and on a recorded justification outliving its task); `THINKING.md` 1io.

### Verified

`tools\StaticChecks.ps1`: **0 candidates** after restoring F13 (1 candidate while the bug was
deliberately reintroduced); `NewWorldTestGround.cs braces 163/163 parens 967/967`. Grep of
`Assets\Scripts` for `F13` / `f13Key` in both spellings: zero. **Not compiled, not play-tested.**

### Pending play-test

1. Compile; paste any console errors (fixed in a new commit, never an amend).
2. Stand next to a **fresh** dent from each of the three sources (projectile, Earth Crater, tool)
   and press **F13**; paste the whole table. Read section A before B/C/D - if it says
   `buildStamp MIXED`, restart Unity before trusting the rest.
3. Confirm **F1 still only toggles fighting mode** and no longer prints a crater report.
4. Then 1ex (interior-only stored fine nodes, tile edges left bilinear so the no-crack proof
   survives) and the bounded raised-rim profile ship as a **separate** change.

Carried over: **F2** (`e658acb`) standing/walking captures; **magic weapon rest pose**
(`10f2500`) staff/book/orb/wand, reparent-and-equip, ready sway, melee regression.

---

## 1in. Crater/deform audit lane (originally F1, now F13) - shipped as a MEASUREMENT, not a fix

**Status: measurement shipped, NO behaviour changed. Awaiting the user's Unity compile and the
crater-audit readout (rule 3 - no build or play-test runs in this project; rule 7 - measure before
fixing). 1in put this lane on F1, which was already bound - see 1io; it is now on F13, so any
readout taken on F1 is void.**
The user reports that spell and tool dents "look like the ground got pulled down" rather than like
craters, and asked for a raised rim instead of stretching the surrounding tiles, which would also
make caves easier later. This task ships **only the measurement**. The fix is 1ex and is not started.

### Why the measurement came first

The shape question has three live candidate mechanisms, and they are indistinguishable by eye:
1. the dish is written to 1 m corners the render path samples, and reads as displaced material;
2. it is written but only partly sampled, so part of it is not drawn at all (rule 12);
3. it is drawn correctly and the fault is purely that a **cone has no rim** - the shape was never
   authored, so no amount of extra resolution would produce one.
Picking one of these by looking would be a guess, so F1 measures all three and names them.

### What shipped

`Assets\Scripts\World\Streaming\WorldStreamer.CraterAudit.cs` (new partial of `WorldStreamer`,
`CraterAudit()`) - read-only: it reads tile data and the resident mesh, and rebuilds, re-stamps and
polls nothing, so every number describes the frame the key was pressed on.

- **A. fingerprint (premise first).** Distinct `(BuildStamp, MeshStep, vertexCount)` buckets across
  the loaded set, plus a loud `buildStamp MIXED` line. Rule 11: an un-restarted session can hold
  chunks from two generator versions, which would make B/C/D evidence about a world that is not on
  screen - so this section runs **before** the three that look more like tests.
- **B. resolution.** Deepest dished corner, how many corners the dish spans, and the dish radius
  against the render path's own visibility bound (`step/sqrt(2)`, not `step`). At step 0 it prints
  `nodeGap n/a (every corner drawn)` on purpose: the dish is drawn **in full**, so the limit is
  SHAPE, not resolution. Saying so stops the next reader "fixing" a carve that is already visible.
- **C. profile.** Per-ring min/mean/max dig out to the dish edge, so "monotone cone" and "bowl with a
  rim" are separable from the numbers. Untouched corners read exactly `0.000` because the chunk
  builder seeds corner `(gx,gz)` from `GetHeight(seed, cx, cz)` - the same sample the dig is measured
  against, so the zero is real and not noise.
- **D. expressibility.** Max corner spread vs the 1ew refinement trigger, corners above pristine (a
  rim is a *positive raise*, and a crater that only lowers can never have one), and adjacent-corner
  gaps. **Scoped to the crater's own footprint**, not the search band: the band finds the crater, it
  is not the crater, and the Wall/Ring/Pillar deform profiles do raise - an unrelated one nearby
  would otherwise be read as "this crater has a rim".
- **VERDICT.** Keeps "no loaded terrain in band" (an absent measurement) distinct from "no dished
  corner" (a real observation of zero), per rule 7's unit rule.

`Assets\Scripts\Opt\NewWorldTestGround.cs`: `EnableCraterAudit` (on), `CraterAuditKey` (**now F13**,
was F1 - see 1io), a cached headline on the HUD plus the full table to the console. F1 was picked
after grep confirmed no `Key.F1` binding exists - which was **wrong**: the combat-mode toggle binds
it as `Keyboard.current.f1Key` (see 1io). F2 is the frame budget, F3 the corner/void audit, F4 the
look audit, F5 the camera toggle.

### Two things the first draft got wrong that no automated check would have caught

- **A half-metre bias.** The profile sampled `FloorToInt(cx + 0.5 + cos*r)`. Corners sit *at* integer
  world coords (`ChunkData.Size == 1`; `WorldStreamer.ChunkBuild.cs:293-295` seeds corner `(gx,gz)`
  from `GetHeight(seed, tc.X*cs+gx, tc.Z*cs+gz)`), so every ring was pushed outward by half a step.
  Found by reading the seeding code rather than assuming it, because the *other* half of the codebase
  (`CurrentHeightOf`) really does sample `(cx + 0.5, cz + 0.5)` - two spellings of "the height of a
  corner" exist, and picking the wrong one is silent.
- **A use-before-declaration.** `dishSpan = span;` was written above `float span = 0f;` (CS0103).
  **`tools\StaticChecks.ps1` did not catch it** - it covers balance, arity, void-return, CS0165,
  cross-case, part-key and member-depth, but has **no declaration-order check**, so brace/paren
  balance passed at 44/44 while the file could not compile. Found by rereading, not by a check.
  Left as a known blind spot rather than adding a naive detector: a false-positive-prone
  "use before declare" scan is worse than none (rule 7).

### Verified

`tools\StaticChecks.ps1`: 0 candidates; `WorldStreamer.CraterAudit.cs braces 44/44 parens 263/263`;
`NewWorldTestGround.cs braces 163/163 parens 967/967`. Plus grep/reread of every referenced member:
`_loadedData`, `VoxelTerrainEnabled`, `EffectiveLowPolyStep`, `EffectiveRefineThreshold`,
`ChunkData.IsValid`, `TerrainNoiseGenerator.GetHeight(long,float,float)`, and the corner owner table
against `CurrentHeightOf` slot-for-slot. **Not compiled** - the user is the compiler.

### Pending play-test

1. Compile; paste any console errors (fixed in a new commit, never an amend).
2. Stand next to a **fresh** dent from each of the three sources (projectile, Earth Crater, tool) and
   press **F13** (not F1 - see 1io); paste the whole table. Read section A before B/C/D.
3. Report which look you are chasing: the missing rim, the sinking neighbours, or the walls.
Then 1ex (interior-only stored fine nodes, tile edges left bilinear so the no-crack proof survives)
and the bounded raised-rim profile ship as a **separate** change.

---

## 1im. Magic weapon permanently drifts sideways after repeated casts — shipped, NOT play-tested

**Status: shipped, not verified in Unity (rule 3 — no build or play-test runs in this project).**
The user reported the magic weapon rotating a little every time it is used and staying permanently
rotated after enough uses in a row. Root cause confirmed by reading `WeaponAnimator`: the weapon's
rest pose was **sampled from the live transform** at the start of every animation phase, while every
animated frame is written as `_baseEuler + accent`. A phase that began while the previous phase's pose
was still applied therefore captured that accent as its new permanent rest — and since `End`,
`StopSway` and `AbandonSway` all restore *to* the base, nothing ever unwound it.

**Only the magic weapons could do this, and that asymmetry is what named the mechanism.** `ApplyAccent`
is the sole source of a rotation offset, and its rotation cases are exactly `K_Staff` (14° roll),
`K_Book` (16° yaw), `K_Wand` (10° roll) and `K_Orb` (30° roll). `K_Lute` is **scale-only**, and every
melee/ranged/shield def is `K_None` or `K_Dual`, which has no case in the switch and returns zeros. So
"all magic weapons drift" would have been **wrong on the lute** — see `THINKING.md` 1im.

**Trigger.** `PlayerController.Interactions.cs:389` calls `PlayCharge()` on every LMB press, gated only
by `WeaponTransitionBusy()`, which checks the draw/stow lerp and not a charge phase in flight. The
magic release tail is 0.38–0.66 s, so clicking faster than that lands the next capture mid-tail. Input
gating was deliberately **not** added (asked; "mechanism fix only") so cast feel is unchanged.

### What changed

`Assets\Scripts\Combat\Weapons\WeaponAnimator.cs` only:
- New authored rest state: `_restValid` / `_restPos` / `_restRot` / `_restScale`.
- `SyncRestFromIdle` — the single writer; called **only** while no phase owns the transform.
- `RestoreAuthoredRest` — the single reader; restores, or no-ops if the rest is not known yet.
- `CaptureRest` — samples the live pose as the authored rest only when idle, otherwise restores the
  authored rest first, so the capture can never read its own output.
- `StartSway` — had the identical live capture; routed through the same helper.
- `OnDisable` — clears `_restValid`, since a rig can be re-parented or re-posed while disabled.

The idle guard is load-bearing, not cautious: the original live capture existed so that re-parenting
onto a hand (`ReparentToHands`) stays harmless, and that is preserved exactly — a re-parent rewrites
the local pose, and an idle rig is by definition sitting on that new pose. A drifted session also
**self-heals on the next cast** rather than needing a restart.

Ruled out by reading, not guessing: `WeaponStowAnimator` (slerps to an authored end pose, `k = 1f`, and
`SetPose` early-outs on an unchanged `draw` — self-correcting) and `CastingCircle` (writes only its own
rotation, never the hand or the rig).

### 1im-status — play-test in Unity

- [ ] **Staff**: rapid-click cast ~20× without pause. The staff's rest roll must be **identical** before
      and after every cast.
- [ ] **holy_book** (16° yaw) and **control_orb** (30° roll, the worst case) — same 20× test.
- [ ] **bone_wand** — the roll must not drift *and* the 26 % charge scale must not creep.
- [ ] **lute** — expected to be unaffected even before the fix (scale-only accent); confirm it is clean,
      and if it drifts too, the diagnosis is wrong and says so.
- [ ] Full-charge release → **immediately** re-cast, repeatedly (the tightest window on the bug).
- [ ] RMB guard entered during a magic release tail (melee/shield hand) — rest must not change.
- [ ] Re-parent path: equip → unequip → re-equip a magic weapon, and enter/leave fighting mode; the
      drawn hold pose from `DrawPoseFor` must still be correct (this is what the idle guard protects).
- [ ] Melee + gauntlets regression: swing a blade and hammer repeatedly; punch with gauntlets.
- [ ] Ready-sway regression: stand still in combat for ~10 s and confirm the lit-focus pulse still loops.

### Verification performed

Grep + reread, no build (rule 3). `tools\StaticChecks.ps1` does not cover this file, so: all 8
`transform.localRotation` writers enumerated and classified (7 read `_baseEuler` or
`_baseEuler + offset`, 1 is the new `RestoreAuthoredRest`); the only writers of `_rest*` are
`SyncRestFromIdle` and the `OnDisable` invalidation; all 3 `CaptureRest` callers checked for the flag
state they present. Balance 99/99 braces, 666/666 parens, sole depth-0 member is the class declaration.

`skills: none applied` — this is a Unity C# logic fix in one file, not a DCC artifact and not an
editor-driving task, so no installed skill governs the artifact; the Scenario Unity skills also cannot
verify anything here (rule 3 bars their MCP/CLI path). Stated deliberately rather than by omission.

# PROGRESS / Session Handoff Notes

Last updated: 2026-10-06 (1ju). Read this first in a new session; then continue with the
newest `## 1xx` entry at the top (they are ordered newest-first) and its
`### 1xx-status` play-test list. **The old game's code is READ-ONLY** - it is quarantined in
`Assets/Scripts/Legacy/` (AGENTS.md rule 18); live code may call into it, nothing may edit it.
**1f5 is superseded by 1f6** - the LOD bands and the
`NeedsLodDetail` gate it added were both deleted; read 1f6 for the current design and 1f5 only
for the reasoning it recorded. **1ju is the newest task and is not yet play-tested** - a strafe
must not yaw the view (1ju), and 1jt's casting-circle placement is still open underneath it.

## 1ik. Frame-budget attribution lane (F2) â€” shipped, NOT verified

**Status: shipped, not run. The FPS number you reported has no attribution behind it yet, and this is
the lane that produces one.** The user reports FPS "mostly falls under 20" â€” but the overlay showed 30
FPS at the moment it was screenshotted, and every counter on that overlay is a *streamer* counter. There
is no CPU/GPU instrumentation anywhere in the project (grep for `FrameTimingManager`,
`ProfilerRecorder`, `Unity.Profiling` returns **zero** hits outside this lane), so the ~30 ms that is
not streaming was unattributable. That is the gap this closes.

**Rule 7's discipline, and why the key is F2.** The lane is read-only (recorder reads and a
`StringBuilder`; no rebuild, no patch, no forced poll, no setting changed) and samples **continuously**
rather than on the keypress, because a frame time over one frame is noise and a CPU/GPU split is only
meaningful as a distribution. The key is the **snapshot boundary**, so the report describes the frames
that ran *up to* the press. F2 was chosen by grep, not inherited: it has zero references in
`Assets\Scripts` and was vacated when 1hx removed its lane without restoring it; F5 is the
`CameraModeSwitch` toggle (`Player\CameraModeSwitch.cs:120`). *(Correction, resolved in 1io: the
F3/F4 tooltips called F1 "a skill hotkey". It is not - F1 is the **combat-mode toggle**,
`Keyboard.current.f1Key` at `PlayerController.Interactions.cs:521`, the property-name spelling that
the 1ik grep did not cover.)*

### What the report says, and in what order

Sections are ordered by whether the **premise** holds before the measurement is read (rule 7), because
a share of a quantised frame is not a share of work:

- **A. The clamp.** `vsync N @ R Hz` + presents-per-frame. If the frame is a clamped multiple, the
  readout says the true cost is a *bracket* â€” "work in (1 interval, this frame]" â€” not a point
  estimate, since a vsync'd frame can only be a whole number of present intervals.
- **B. The fork.** `cpu main / render / total` and `gpu profiler / frameTiming`, each with its sample
  count `[n â€¦]`. A source that never reported prints `n/a`, never `0`.
- **C. The owners.** draw / batches / setpass / tris, plus the render settings that govern them
  (shadow distance, shadow resolution, AA, render scale) â€” a number with no lever attached cannot be
  acted on.
- **VERDICT.** Names CPU-main, CPU-render, or GPU, or says both sides are comparable, or says the
  split failed. Dominance is measured **between the sides** (clamp-proof), not against the frame.

### Two traps the lane is built to avoid (both are now `AGENTS.md` rule 7 bullets)

- **Absent â‰  zero, and the unit decides.** A *valid* `ProfilerRecorder` that has never been filled
  still returns 0, so a bare 0 in a time column would read "this side costs nothing" about the one
  side the Editor can't see. Time sources map 0 â†’ `n/a`; count sources pass 0 through (0 draw calls is
  a real observation). The unit comes from the marker's own declared `UnitType`, not from the call
  site.
- **A clamp breaks the denominator.** The first version divided work by frame time; under vsync that is
  not a share of anything (20 ms behind a 33.3 ms frame is a full lost present but reads 0.60). Fixed:
  dominance is side-vs-side, and the residual is labelled *clamp slack* vs *unexplained work* per
  regime. I also caught myself calling `sb.Clear()` mid-report, which would have wiped every earlier
  line â€” rewritten as clean branches instead.

### 1ik-status â€” NOT verified, no Unity run in this project

- [ ] Let Unity compile. **First build of this lane returned exactly one error and it is FIXED**
      (`41db254` â†’ follow-up commit): `QualitySettings.renderScale` does not exist â€” render scale lives
      on the **URP asset** (`UniversalRenderPipelineAsset.renderScale`). `tools\StaticChecks.ps1`
      reported **0 candidates on the broken file**, so this is rule 3's "review is not compilation"
      landing exactly as written: the balance check cannot see that a member is on the wrong type.
      Now read by reflection off `GraphicsSettings.currentRenderPipeline`, printing `n/a` if the
      active pipeline is not URP. **Recompile and confirm it is clean before anything else.**
- [ ] Report which **quality level** you are on (PC or Mobile) alongside the readout â€” the two ship
      different pipeline assets and different render scales, so the settings row is only meaningful
      with that context.
- [ ] Stand still on the test platform, press **F2**, screenshot the block. Then walk ~5 s, press F2
      again, screenshot. Two numbers, two conditions â€” that comparison is the whole deliverable.
- [ ] Read the **VERDICT** line first, then the bracketed clamp line. If section A says the frame is a
      clamped multiple, the headline FPS is a presentation quantum and the real cost is the bracket.
- [ ] If GPU prints `n/a` on both sources, that is a **measurement gap, not a fast frame** â€” say so,
      and the lane's answer is "this platform's Editor does not expose GPU timing", which is a
      different conclusion from "the GPU is fine".
- [ ] Report the `draw / tris` counts and the `settings` row verbatim; those decide whether the next
      task is a draw-call/geometry lever or a CPU-scripting lever.
- [ ] **Follow-up (1il, not done):** the 1ic verdict close-out. Its 4 checklist items are all resolved
      by the F4 readout (`172/172/0`, `worst none`), but 1ic is measured-not-play-tested, and I am
      deliberately **not** closing 1ic before the <20 FPS picture is understood â€” a doc that says
      "verified" while the frame budget is unexplained is the stale-text failure rule 2 warns about.
- Verification here: `tools\StaticChecks.ps1` 0 candidates (incl. brace/paren balance on
  `NewWorldTestGround.cs` at 156/156, 932/932), grep for `Key.F2` (no conflict), grep for `OnDisable`
  (none existed; added for `ProfilerRecorder.Dispose`), grep confirming no pre-existing
  `FrameTimingManager`/`ProfilerRecorder` use. **No build.**

## 1ij. Bench fidelity + the last three colour consumers â€” shipped, NOT verified

**Status: code + docs complete.** This is the last of the three commits the 1ic split produced, and
it is the one that closes the "no consumer re-derives a colour" claim: after it, **zero spell-facing
sites** call `DamageNumber.ColorFor`.

### The three sites, and why each was still wrong

All three drew a colour that the player's spell has already had an opinion about, so they were the
last places where a Fire spell could show a non-Fire preview, or where two spells of one school could
look identical *while aiming at each other*:

1. **Flight-path cone** (`PlayerController.Combat.cs`) â€” the charge preview was tinted by school
   colour, so two different Fire spells aimed identically.
2. **Ground AoE preview ring** (`PlayerController.Combat.cs`) â€” same.
3. **Falling-rock tint** (`SpellCaster.Cast.cs`) â€” Meteor and Comet both dropped a rock tinted by
   school colour, losing the per-spell body colour the rock itself already got in 1ih.

A fourth was found and fixed in passing: **`SpellZone.BuildVisual` took a `DamageType type` argument
purely to re-derive a colour on the null-spell path** â€” while `Initialize` had *already* resolved the
identity-less fallback into `_look`. Two sources of identity where one was enough, able to disagree
with itself. The parameter is gone; `BuildVisual()` reads `_look.Core` once.

### The bench now shows the real body (`SpawnMagicModels`)

It called `CreateProjectileDisplay(skill.DamageKind, spell.Shape, â€¦)`, i.e. the **identity-less
school fallback** â€” so every Fire spell on the bench showed the same body no matter what its own
`DisplayShape` said, on the one screen whose entire job is comparing per-spell bodies. It now passes
the `SpellData`. Labels were tinted by `skill.DamageKind` for the same reason and now use
`SpellLook.Resolve(spell).Core`, so a label cannot disagree with the body above it.

**Two knock-ons worth naming.** `CreateProjectileDisplay(SpellData)` had **zero callers** until this â€”
it was written in 1ig and never wired up, which is why the *old* two-arg overload was still live.
And `CreateProjectileDisplay(DamageType, ProjectileShape, bool)` now has no callers at all; it is
retained because it is the named identity-less fallback's public entry point and `MagicTestMatrix` is
the natural future caller, but if a grep confirms zero at the next cleanup it should go.

**skills: none applied â€” read the task as C# code review, not as an engine or DCC artifact.** The
installed set is Scenario's Blender/Maya/ZBrush/Unreal/Unity/image-video-audio skills. The nearest
candidates (`scenario-unity-expert`, `scenario-unity-architecture`, `scenario-unity-gameplay`) all drive
a running Unity 6.3 editor over MCP or in `-batchmode`, which rule 3 forbids here, so they could have
been informative about engine APIs but could not have *verified* anything â€” exactly the
informative/authoritative split rule 11 draws. The deliverable here was 20 files of reviewed C# plus
docs, with the repo's own `tools/StaticChecks.ps1` as the mechanised check. Recorded in `AGENTS.md`
rule 15, added after the user asked; see `THINKING.md` Â§1ij for the same note.

### 1ij-status â€” NOT verified

- [ ] **Press F4 first.** Same gate as 1idâ€“1ii.
- [ ] On the bench, confirm two spells of the **same school** now show **different bodies**, and that
      each label matches the body above it (a Fire sphere next to a Fire shard, two different labels).
- [ ] Charge a Fire spell and an Ice spell: the flight cone and the ground AoE ring must differ in
      colour *before* release, not only after.
- [ ] Cast Meteor and Comet: the falling rock must wear each spell's own core colour, not one Earth
      orange.
- [ ] Cast a zone with no spell behind it (the fallback path) and confirm it still draws â€” the
      `BuildVisual` signature change is a compile-level edit, and the null-spell branch is the one the
      play-test is for.
- Verification here: `tools\StaticChecks.ps1` 0 candidates, string-stripped balance on all 6 touched
  files, full `DamageNumber.ColorFor` sweep reviewed (remaining 20 sites are all **non-spell**:
  class/race skill slash flashes, the ranged-shot cone, the skill-tree node tint, the Earth debris
  accent, and the UI node legend â€” none has a `SpellData`), grep for `CreateProjectileDisplay` and
  `BuildVisual` call sites. **No build.**

## 1idâ€“1ii. Per-spell impact / cast / body families â€” shipped, NOT verified (1ic's number unmeasured)

**Status: code + docs complete, verification OPEN.** These six tasks were written as one batch in the
working tree (they interleave inside the same eight files, so hunk-splitting them would be
guesswork), and they ship as one commit. **Nothing here is verified until F4 reports
`distinct identities == spells`.** That is not a formality â€” 1ib's commit message claimed "172/172
distinct fingerprints" from a code reading, and 1ic's whole existence is that no such number had ever
been *measured*. Do not report this work as done.

### What shipped

| Task | Change |
|---|---|
| **1id** | `SpellImpactFx` â€” a pooled per-family impact-flash dispatcher replacing `SkillFx.ImpactSphere`. 8 `SpellImpactStyle` families, pool cap 96 idle per family, `PerFrameBudget = 24` drops (never queues) the excess. Owns its materials so the in-place fade cannot corrupt a sibling flash |
| **1ie** | Zone / Beam / Vortex / Storm / Summon on-hit flashes + per-spell body colour and spawn-ring colour from the resolved look |
| **1if** | `CastingCircle` â€” 7 `SpellCastStyle` families driving which halo parts exist (Disc / outer Halo / Rune / HexRing / inner segments), plus per-spell halo tint and `Scale`/`Tempo` |
| **1ig** | Projectile body shape comes from `SpellLook.DisplayShape` instead of the deleted `AutoShapeFor`; the `Shape` (gameplay/homing) vs `DisplayShape` (drawn) split |
| **1ih** | `spell.Shape` never written; `SummonFallingRock` + per-spell rock tint; 1 authored `SpellLookProfile` per named-family spell |
| **1ii** | **21** authored `look:` profiles â€” 16 in `SkillCatalog.cs`, 5 in `ClassSkillCatalog.cs`. The other 151 spells resolve deterministically from their school family |

Also: `SpellLook.cs.meta` committed (1ib shipped the `.cs` without it), `SpellImpactFx.cs` + `.meta`
added, and `SpellLook.Fingerprint` deleted (see below).

### Repairs made in this pass â€” three of them were real defects, not tidy-ups

- **`CastingCircle` threw every frame with a stripped shader.** `Build()` assigns `_runeTicks`
  unconditionally but creates the 8 tick cubes only inside `if (shader != null)`, so when both
  `Shader.Find` calls return null the transform is **empty** and `Apply()`'s
  `_runeTicks.GetChild(i)` raises `ArgumentOutOfRangeException` per frame â€” the halo draws nothing and
  the console fills. The pre-existing `_runeTicks != null` check passed *while the ticks were
  missing*. Fixed by guarding on `childCount >= RuneTicks`: the real invariant is "as many children as
  I am about to index", not "the reference is non-null".
- **`SpellStorm` and `SpellSummon` left `_look` at `default`.** Both resolved it only inside
  `if (spell != null)` and then read `_look.Scale` unconditionally after the block â€” `Scale` is `0` on
  a default `SpellLook`, so a spell-less storm drew a 5Ã—-too-small spawn ring, and a spell-less summon
  passed the `Inherit` impact style to `SpellImpactFx.Spawn`, which returns immediately (no flash at
  all). Both now seed the same identity-less fallback `SpellZone`/`SpellBeam`/`SpellTornado` use.
- **An undeclared behaviour regression.** 1if had changed the identity-less release burst from
  `Color.white` to `DamageNumber.ColorFor(DamageType.Arcane)`. Restored to white: a plain weapon
  release now reads as "no spell was cast", where pink reads as "an Arcane spell was cast". The
  sibling burst, which *is* spell-backed, stays look-derived.
- `SpellImpactFx`'s constants block was re-indented 8 â†’ 4 spaces.

### `SpellLook.Fingerprint` deleted

1ib built it as 1ic's measuring instrument and **1ic refused to use it** (THINKING.md 1ic, H40): a
32-bit hash collision is a claim about the instrument, not about two spells looking alike. Zero callers
remained. This is also the admission that 1ib's plan named the wrong measuring device, and it is why
1ib's commit-message claim is not repeated here â€” `PROGRESS.md` records the *expected* denominator
(172) and the gate, not a number that has not been read.

### `MagicTestMatrix`'s school swatch keeps its own palette

1ib folded this QA header colour into `SpellLook.SchoolColor`, silently recolouring four of nine
schools. Restored to the pre-1ib literals behind a comment saying why: it is a **swatch, not a
readout**. When the swatch and the thing being judged are the same colour, a mis-coloured spell is
invisible on the very screen built to catch it. This is the single sanctioned exception to "no consumer
re-derives a colour"; it is debug-only and save-invisible, so it needs no parity check.

### 1idâ€“1ii-status â€” NOT verified

- [ ] **Press F4 first.** Everything below is meaningless until `M == N` (see 1ic).
- [ ] If `M < N`: retune the per-school family tables in `SpellLook` (widen the member lists) or the
      jitter band. Paste the console's `COLLISION x<n>:` blocks.
- [ ] Fire one spell per school on the bench: halo family, impact family and body shape must differ
      from each other, and a Fire spell must never draw a crystalline impact.
- [ ] Watch the **impact flashes dropped** HUD line while casting several Blizzards + a storm at
      once. A non-zero climbing number means `PerFrameBudget = 24` is below the real peak rate â€” that
      is what the counter is for.
- [ ] Cast Blizzard and Tornado, and count flashes: **one per tick**, not one per victim.
- [ ] Cast on a stripped-shader build (or temporarily force `Shader.Find` to null) and confirm the
      Rune halo no longer spams exceptions.
- Verification here: `tools\StaticChecks.ps1` 0 candidates, string-stripped `(){}[]` balance on all 17
  touched files, grep sweep for all three deleted members across `.cs`/`.asset`/`.prefab`/`.unity`,
  21 `look:` sites confirmed, 172 = 167 + 5 re-counted. **No build.**

## 1ic. Read-only per-spell look-collision audit (F4) â€” the measurement 1ibâ€“1ij is judged by

**Status: shipped, NOT run. The number does not exist yet â€” pressing F4 is what produces it.** This
is deliberate and is the whole reason it is its own commit: 1ib's plan ordered the audit *before* the
visual work "so the jitter is tuned against a measurement rather than taste", and the working tree had
already merged 1ic with 1idâ€“1ii. Splitting it out keeps the readout that judges the other six tasks in
history where rule 7 can find it.

### What it measures

Press **F4** on the test platform. It resolves the look of every spell the player can actually cast
and reports `N spells / M distinct identities / C colliding groups`, plus a full list of colliding
pairs in the console. The HUD keeps the headline line so a screenshot of the number is evidence for
the frame the key was pressed on.

**Two spells are the same identity when impact family + cast family + body shape + core RGB all match
at 8 bits per channel.** `Scale` and `Tempo` are deliberately excluded â€” they are sub-perceptual, and
counting them would let the number read "unique" while the two spells look identical on screen. That
is rule 7's "gate a classifier on the width of its own test", applied to an identity metric instead of
a spatial one.

**The gate: `M` must equal `N`.** Expected `N` is **172** (167 magic `Spell(...)` call sites â€” 16 in
`SkillCatalog.cs` + 151 in `SkillCatalog.Magic.cs` â€” plus 5 live `ClassSkillCatalog.MakeSpell` sites).
`N` below 172 means the audit's walk missed reachable spells, not that spells collided; the readout
prints `N` precisely so that distinction is visible. `M < N` means the family tables or the jitter
band are under-tuned and 1idâ€“1ii are **not** done.

### Three things it deliberately does not do

- **It does not group by `SpellLook.Fingerprint`.** That property is a 32-bit hash of the same axes;
  grouping on it would report a genuine hash collision as "two spells look the same", which is a
  different claim about a different thing. `LookKey` packs the real axes instead (34 bits), so a
  reported collision is a real identity collision. **`SpellLook.Fingerprint` is now dead** â€” it was
  built in 1ib as the measuring instrument and the audit refused it. It is deleted in 1ig.
- **It excludes the dead `RaceSkillCatalog.MakeSpell` twin.** That twin builds no reachable spell. It
  is kept in signature parity with the live `ClassSkillCatalog.MakeSpell`, not in the denominator.
- **It spawns nothing.** It resolves looks into a local dictionary and prints.

### The StaticChecks false positive this commit had to fix first

The new readout's summary line ends `.Append("), ")` â€” a `)` inside a string literal â€” and
`tools\StaticChecks.ps1` check 1 counted raw characters, so it reported the file as
`parens 840/841`. **That was a false positive on the very first file the fix was run against**, which
is the situation rule 7 calls out: a check that cries wolf on its first file has a silence no reader
can trust any more. Check 1 now counts braces and parens with comments, string literals and char
literals stripped, character-wise (a regex cannot tell an escaped quote from a closing one, and
`@"..."` verbatim strings need their own rule).

**Verified both directions**, because a green check nobody has seen fail is not a check: an extra `(`
injected into `WorldStreamer.Deform.cs:38` fires the check (`parens 278/277`), and the restored file
goes quiet (`277/277`). The whole tree is now **0 candidates**.

### 1ic-status â€” NOT verified, no Unity run in this project

- [ ] Launch the test platform and press **F4**. Report the whole headline line verbatim.
- [ ] If `distinct identities` < `spells`: paste the console's `COLLISION x<n>:` blocks. Those are the
      spell pairs to retune â€” widen the per-school family tables or the jitter band in `SpellLook`,
      not the audit.
- [ ] If `spells` < 172: the walk is missing reachable spells; check whether `ExpandTree` fails to
      materialise every `DesignBank` slot.
- [ ] Confirm a clean run prints `(worst none)` rather than `(worst 1)` â€” the old code seeded the
      worst-group counter at 1, so a run with zero collisions printed "worst 1", which reads like a
      finding when it is the absence of one.
- Verification performed here: `tools\StaticChecks.ps1` (0 candidates, after being watched fire on an
  injected imbalance), string-stripped `(){}[]` balance on every touched file, grep for the audit's
  roster symbols. **No build.**

## 1ibâ€“1ij. Per-spell visual identity for all 172 spells â€” PLAN (executing)

**Status: plan of record, written down before implementation.** Full reasoning trail, including the
claim that turned out to be false, is in `THINKING.md` Â§1ib (H34â€“H39). Summary of the shape:

- **New file `Assets\Scripts\Combat\Effects\SpellLook.cs`** â€” a `SpellLook` (core colour, scale, tempo,
  impact style, cast style, display shape) resolved in exactly three steps: **authored â†’ deterministic
  (FNV-1a over `spell.id`) â†’ school default**, with a per-school family table so a Fire spell never
  draws a crystalline shatter. It becomes the **single** place a spell's colour or shape is derived.
- **Why deterministic, not 172 hand-authored rows:** a hand-maintained 172-row table is precisely
  rule 12's "second spelling that rots" â€” rename a spell and it desyncs. Determinism can never
  drift; the ~21 hand-authored overrides (16 base-pass spells + 5 class spells) sit on top for the
  spells whose look is a design decision.
- **~121 spells, not 39, is the win.** `SkillFx.RingFlash` is the dominant shared strike visual
  (13 call sites across 8 files); `ImpactSphere` is one call site reached only by Projectile spells.
- **Also fixing a feedback gap:** Beam (11), Zone (77), Vortex (8) and Storm (14) play **no per-tick
  on-hit flash today** â€” only a damage number. 1ih adds per-spell tick flashes.

### Two invariants the implementation must hold

1. **`DisplayShape` never writes back to `spell.Shape`.** `Shape == ProjectileShape.Missile` sets
   `_homing = true` (`SpellEffect.cs:75-76`), so `Shape` is partly **behaviour**. A shape picker
   that wrote to it would silently switch homing on â€” a gameplay change disguised as a look change.
   The 36 explicitly authored shapes win outright; determinism only fills the `Auto` gap (3 spells).
2. **Key off `spell.id`, never `displayName`** â€” rename-safe (1hz's lesson).

### The number that decides whether this worked

`172 spells â†’ N distinct (impact, cast, display-shape, core-colour) tuples`, with colliding pairs
printed. If N â‰  172 the jitter is under-tuned and 1ib is not done. That readout is task **1ic** and
ships *before* the visual work, so the jitter is tuned against a measurement rather than taste.

### Task order

| # | Task |
| --- | --- |
| 1ib | `SpellLook` + resolver + school family table; fold `MagicTestMatrix.SchoolColor` in |
| 1ic | read-only collision audit lane on the test platform |
| 1id | pooled tick/impact FX with per-frame spawn budget + raised cap |
| 1ie | `RingFlash` seam (~121 spells) |
| 1if | `ImpactSphere` + `CastingCircle` seams |
| 1ig | projectile `DisplayShape` + identity-less fallbacks |
| 1ih | per-tick flashes on Zone / Beam / Vortex |
| 1ii | 21 authored overrides; `RaceSkillCatalog.MakeSpell` twin parity |
| 1ij | bench shows full identity + docs sync |

Nothing here changes delivery, damage, cost or school for any spell, and no prefab, asset, shader or
`SpellData` serialization is introduced. Unlike 1ia's terrain revert, **no restart is needed** â€” FX are
built fresh per cast.

## 1ia. Terrain render algorithm reverted to pre-1hi (the facet look is now OFF by default)

The user asked for the terrain's rendering algorithm to be reverted to what it was **before the first
low-poly request** (1hi), as the only change â€” a test of the corner void gap, not a cleanup. I read
"revert the algorithm" as "the world must render exactly as it did pre-1hi", and implemented it as a
**default flip rather than a deletion**: no low-poly code was removed, so `LowPolyFacets = true`
still restores the facet look exactly, with the F3 audit and all 1i0-1i9 work untouched.

### The change (three values, plus their test-platform mirrors)

| Value | Was | Now |
| --- | --- | --- |
| `WorldStreamer.LowPolyFacets` | `true` | **`false`** |
| `WorldStreamer.LowPolyStep` | `6` | **`3`** (pre-1hx) |
| `WorldStreamer.FarSectorStep()` | `6` | **`3`** |
| `NewWorldTestGround.EnableLowPolyTerrain` | `true` | **`false`** |
| `NewWorldTestGround.LowPolyStep` | `6` | **`3`** |

`FarSectorStep` is **not** gated on the flag, so the far shell samples 3 m whether or not the look is
on â€” a one-value revert would have left the shell describing a surface nobody draws. And
`NewWorldTestGround` pushes both mirrored values onto the streamer in `Awake`, before the first stream
poll, so leaving them at the old values would have silently re-enabled the look on the test platform
every session.

### What the world renders now

- Far shell: smooth central-difference normals, `flatFacets = false`, 3 m sampling. `Cull Off`
  material unchanged.
- Near chunks: the full **1 m per-tile** merged surface **with the per-tile side walls** the coarse
  root omits. `EffectiveLowPolyStep` is `0`, so `BuildLowPolyMerged` is unreachable.
- 1ew stretch-split: back on (`EffectiveRefineThreshold` returns `RefineThreshold` again).
- Lod1/Lod2 children build again; `PatchRegion` takes its per-tile skim.
- Colliders are **unchanged** and deliberately not part of the revert: smooth chunks still cook the
  decimated 2 m lattice from 1hi. It is a physics/budget feature with no visual effect.

### What this does and does not settle

This is a **hypothesis test for the corner void**, not a fix. 1i1-1i8 located the void at the near/far
boundary (the outer x/z corner of the loaded square); one candidate contributor is that the coarse
root emits no side walls, so a height disagreement between a loaded chunk and a far cell is
see-through rather than filled, and the 3 m step is a second candidate. The revert is the cheapest way
to tell the mechanism apart from the data layer â€” **F3 after a restart is the readout that decides it.**

### Terrain save cache â€” WIPED at the user's request (edits are gone)

The user authorised discarding all terrain edits so the world comes up pristine. Deleted **13**
`tc_*.dat` files (7 616 bytes) from
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\country life\worlds\1337\` â€” the exact pattern
`ChunkSaveManager.ResetWorldSaves(seed)` deletes. No Unity process was running, so nothing could
write them back from a live session's in-memory dirty state.

**Consequences to remember:**
- **All terrain edits for seed 1337 are permanently gone** â€” every dig, tool crater, zone/storm/summon
  strike and projectile dent since the seed was created. Do not go looking for them, and do not treat a
  missing crater as a regression.
- `savegame.json` was **deliberately left alone** (separate player save: `time, player, inventory,
  gunAmmo, fields, buildings, quests` â€” verified to carry no heights, so it cannot resurrect edits).
- The `worlds\1337\` directory itself was kept, exactly as `ResetWorldSaves` keeps it.
- Deleting files from disk also removes the need for the in-session resident drop: with Unity closed
  there is no `_loadedChunks`/`_dormantChunks`/far shell to drop, so **the next launch is already a
  clean, fully-regenerated, pre-1hi-render world.** If a reset is ever wanted mid-session instead,
  the in-game path is `NewWorldTestGround.EnableResetTerrainSaves` (default off, runs in `Awake`).

### 1ia-status â€” NOT verified, no Unity run in this project

- [x] Terrain save cache wiped (above) â€” the world will regenerate pristine from noise.
- [ ] **Start a fresh play session** before looking at anything. Per rule 11 a restart is the only
      remedy for a render change: `_loadedChunks`, `_dormantChunks` and the far shell all hold geometry
      built by the old generator, and `EnqueueChunkIfNeeded` *wakes* a dormant chunk in place. A
      session that was already running keeps the facet world. This is satisfied simply because Unity
      was closed for the cache wipe.
- [ ] After restart, the world should read as the **pre-1hi smooth** look: 1 m ground, Lod bands back,
      1ew splitting on steep slopes.
- [ ] Walk out to the near/far rim and press **F3**. Report all sections, especially the corner/void
      counts at the outer x/z corner tile, and compare against the 1i1-1i8 numbers.
- [ ] Confirm the projectile dent is back (1i9 is moot at step 1, but the dent should be plainly
      visible) and that craters still ratchet on repeat casts.
- [ ] Confirm no cracks along chunk edges and none at the near/far rim (3 must divide 30/90/180 â€” it
      does).
- [ ] If the corner void is GONE: the facet path's missing side walls (or the 6 m step) were the
      cause, and the fix belongs on the low-poly side â€” re-enable the look and close it there. If it
      is STILL THERE: the cause is in the data/ownership layer, the render algorithm is exonerated, and
      the next step is to instrument that seam rather than keep re-rendering it.
- Verification performed here: grep + reread, plus `tools\StaticChecks.ps1` (0 candidates). No build.

## 1i9. The dent was never deleted â€” 1hx deleted the resolution it was drawn at

The user reported "the dent function is gone now", clarified as the dent a projectile leaves in the
terrain. This one I could reason to a proof rather than to a measurement, because the renderer's
decimation is arithmetic.

### What I nearly shipped wrong, and the check that killed it

My first mechanism was the directed-dig clip at `Deform.cs:144` â€” `SpellEffect.cs:306` passes the
projectile's *flight direction* `_dir`, and that clip multiplies a crater's influence by
`Clamp01(along / (radius*0.5) + 0.15)`, which zeroes everything behind the cast plane. Real code,
and it would turn a projectile crater into a forward-facing sliver about 6.7 cm deep at its centre.

**It is gated on `VoxelTerrainEnabled`, which is `false` in normal play** â€” the only assignment in
the project is `NewWorldTestGround.cs:138`, the opt-in test-ground lane, and no `.unity`/`.prefab`
serialises it. So the clip never ran. **A mechanism that cannot be live is not a cause**, and the
gate was one grep of a boolean away from shipping as "the fix". Worth remembering next to 1i4's
construct-in-the-wrong-place: this time the code was fine and the *premise* was false.

### The actual cause

`LowPolyStep = 6`, and `EmitLowPolySurface` (`ChunkMeshGenerator.cs:757`) builds the visible
surface from every `step`-th node of the 31Ã—31 lattice at world positions `gx*step, gz*step` â€” so
`axis = 30/6 + 1 = 6`, **25 quads / 100 vertices**. That `verts 100` was in the user's own F3
readout, which is what made this a proof instead of a guess: the render carries a vertex only every
6 m in x and z.

The projectile dent's reach is `radius + feather = 1.4 + 0.5 = 1.9 m` (`Deform.cs:55`). A carve
only changes the render if a sampled node falls within that reach, and the worst case is
`3âˆš2 â‰ˆ 4.24 m`. So **~69% of impacts moved no rendered vertex at all**, and the rest produced a
partial dish.

**And this is why it worked "before".** At `step = 3` the worst case was `1.5âˆš2 = 2.12 m`, so a
1.9 m reach essentially always contained a sampled node and the dent rendered. 1hx moved the step
3 â†’ 6 for facet-shading contrast, and put the dent below the floor. This is rule 12's own sentence â€”
*"the edit granularity is the step"* â€” applied to a deform that was never revisited when the step
moved. The dent was deleted by a **rendering** decision, in a **deform** file, with no error and
perfectly correct data underneath.

### The fix

`CraterFacetSkirt` + widened write bounds in `DeformAt`:

- When low-poly is on, find the nearest node the renderer samples (both coords multiples of the
  step) and its distance `toSampledNode`. If the authored reach contains none, the carve is
  provably invisible, so additionally dip that node with a skirt that dies one step out (squared).
- Combined by `Mathf.Max` with the authored influence, and `Max` against `CurrentHeightOf` per
  cast as before, so the authored radius, the 1.1 m depth and the per-cast ratchet are untouched and
  a crater that already reaches a sampled node is bit-for-bit unchanged.
- **The loop's write bounds widen to `toSampledNode + step`** â€” the guarantee was computed outside
  the region the loop writes, and a guarantee outside the written region writes nothing.
- The skirt measures the corner as world node `(cx, cz)`, not `cx + 0.5`. The deform loop samples
  influence at tile centres (pre-existing half-metre quirk), but the corner it *writes* is a
  lattice point and the facets sit at exactly those integer multiples â€” measuring in the target's
  own space is what makes the peak land on the node it is aimed at.
- Scoped to `Crater` alone. The raised shapes are not silently re-footprinted; see rule 12.

Deliberately **rejected**: widening `dentRadius` (also widens the gameplay crater, and a Crater
ratchets `CraterStep` per cast, so a "small universal dent" becomes a deep pit) and reverting
`LowPolyStep` to 3 (that is the visual decision 1hx was made to get).

### 1i9-status
- [ ] **Play-test: fire any non-Earth bolt at the ground â€” a dent must now appear.** Repeat at a
      spot roughly midway between two facet lines (the ~69% case) and at one near a facet line
      (the ~31% case): both must show a mark, and the near-line case must be unchanged.
- [ ] Confirm the dent still ratchets deeper on repeat casts, and that the shovel/pickaxe dig is
      still a tight bowl (it now also gets a skirt when its 1.05 m reach catches no sampled node).
- [ ] `tools\StaticChecks.ps1`: **`WorldStreamer.Deform.cs` added to `$files`** (it was unchecked,
      so the run above covered none of this edit), braces 60/60, parens 366/366, 0 candidates;
      `influence` is the pre-existing declare-then-assign-in-if-chain pattern. No Unity build was
      run â€” verification is code review + grep + the static script.
- [ ] Note for the running session: this is a **deform** change, not a render-algorithm change, so
      it applies to carves made *after* the recompile. Existing resident chunks keep their meshes,
      but no resident drop is required because nothing about the already-built geometry changed.

## 1i8. Reading the saves, so the next fix is chosen by data instead of by my next guess

1i7's two additions did their job in a single readout and immediately narrowed the field:

- `buildStamp 1 x243 (uniform: resident set matches the code now running)` â€” **you did restart, and
  1i6's code is genuinely running.** The "the new code never ran" explanation, which I could not
  rule out by reading, is now closed by measurement rather than assumed.
- `[1i7 split: stamp-clobbered 9 â€¦ no-owner 220]` â€” **my short-circuit theory was wrong.** I claimed
  `!float.IsNaN(corners[fx, fz])` was bypassing the seam walk, and it accounts for 4% of the fault.
  So the 1i5/1i6 resolver is running and declining to fix 96% of the nodes it is aimed at.

The user's own observation is the other half of the narrowing: the gap is at **every chunk corner
within 300 m**, which is a *systematic* failure, not an occasional one. A systematic failure has very
few candidate causes, so I checked the cheap ones by reading and eliminated them without a round-trip:

| candidate | result |
|---|---|
| walk never runs (ordering) | cleared â€” the walk is at `ChunkBuild.cs:264`, the noise fill at `:290` |
| walk reads a different world | cleared â€” `seed = Seed` is captured at dispatch (`Streaming.cs:505`) |
| `IsSaneHeight` silently rejects a dig | cleared â€” the band is Â±200 m (`WorldStreamer.cs:294`) |
| unit mismatch in the pristine test | cleared â€” `ChunkData.Size = 1f`, so tile == metre |
| wrong owner arithmetic | cleared â€” verified 16/16 against a worked example in 1i6 |

Which leaves a genuine contradiction, and it is the reason this is a measurement rather than a fix:
on a uniform-stamp fresh restart, a chunk's **rendered** edited height *is* its **saved** height,
because a fresh build reads nothing else. So the edited side's save provably holds a non-pristine
height at a tile touching the node, the walk reads that save, examines exactly those four tiles, and
prefers any non-pristine value. It should have adopted it. On 220 nodes it did not, and every check I
have passes. That is the same shape as 1i4, where a check passed on a member in the wrong place â€” so
the check is what's untrustworthy, not the reasoning.

### What this adds

1. **The save probe** (`ProbeSeamSaves`) â€” for every disagreeing node, read the mods of the four
   tiles that share it **straight out of each owning chunk's save file**, and count how many hold a
   value differing from pristine by more than `SeamPristineTol`. It uses the *resolver's own*
   tolerance, so it answers "would the walk have copied this?" rather than "do these two surfaces
   differ?". Memoised per chunk, so ~1 file read per loaded chunk rather than 4 per node.
   - `[1i8 saves: in-save N â€¦ not-in-save M â€¦]` â€” the two outcomes need **opposite** fixes.
2. **Which of the four slots held the edit** (`NE/NW/SE/SW` histogram). If the build's addressing is
   wrong, the edit piles up in a slot the resolver does not consult first â€” which is directly
   checkable now, against reality rather than against my own arithmetic.
3. **Up to `RenderedCornerMaxListed` worked examples**, worst cracks first, each printing all four
   sides: slot, value, `EDITED`/`pristine`/`no mod`, and the owning chunk. This is the part that ends
   the guessing.
4. **B/D stop reporting known-absent things.** That run had `far cells 0` and B still printed
   `claimed-dead 3858` plus `VERDICT B: â€¦ which is a true hole`. B's "claimed" test is the
   *ownership* predicate (`FarCellForChunk` â€” a cell belongs here), not a test that a cell *exists*;
   the two come apart whenever the shell has not filled. B now returns `B-unfilled` when
   `_farSectors.Count == 0` rather than sending the player to walk to ring 6, and D's line is tagged
   `[VACUOUS: â€¦ dY 0 means nothing here]` for the same reason. Rule 7 again: a measurement that
   reports things which are known-absent is not conservative, it is unreadable â€” and that verdict is
   what I have been chasing for two rounds.

### 1i8-status
- [ ] **Press F3 and paste the `C` line plus the `seam-save probe` lines.** The verdict depends on
      which of `in-save` / `not-in-save` dominates.
- [ ] If `not-in-save` dominates, **1i9 is not a resolver fix.** It is the live-edit channel:
      `ApplyHeightEdits` rebuilds only chunks owning a touched tile (`Deform.cs:448`) and
      `ReconcileModifiedBorders` is gated on the neighbour being in `_modifiedChunks` (`:361`) and is
      called *only* from the chunk-load path (`Mesh.cs:153`) â€” so an unmodified neighbour is never
      reconciled, ever. Seam correctness would have to move to the edit path, which is a larger
      change than a resolver.
- [ ] Verification: code review + `tools\StaticChecks.ps1` (**0 candidates**), braces 67/67 and parens
      525/525 on the audit file. `FloorDiv(int,int)` signature confirmed by reading, and inline `out`
      declarations confirmed as existing project style. No Unity build was run.

## 1i6. The 1i4 fix resolved the seam ONE owner deep, and one edge short

Found by walking 1i4's addressing instead of trusting its comment. A world node is a corner of **four
tiles in up to four chunks**, and 1i4 asked only one of them.

For a node on this chunk's west edge the four touching tiles split 2+2:

| slot | tile | lives in |
|---|---|---|
| NE `Heights[1]` | (wx-1, wz-1) | **west neighbour** |
| NW `Heights[0]` | (wx, wz-1) | west neighbour |
| SE `Heights[2]` | (wx-1, wz) | this chunk |
| SW `Heights[3]` | (wx, wz) | this chunk |

1i4 read the NE tile only. So:

- **Half the neighbour's contributors were never asked.** A dig that lands on the NW or SE tile was
  invisible to the resolver.
- **The east and north edges were never scanned at all** (`if (fx != 0 && fx != gridSize-1 && ...)`).
  The west-east case only worked *by accident*: the eastern chunk scans its own west edge and finds
  this chunk. The reverse â€” edit in the east, western neighbour still pristine â€” was never asked, and
  a local dig produces exactly that.

So the seam propagated in one direction and not the other. This is rule 7's "a scope is a claim about
the mechanism, so name which owner(s) the walk admits": 1i4 admitted one owner and called the pass a
resolution.

### What changed

- All four edges are scanned; every NaN boundary node calls `TryResolveSeamCorner`.
- That walks the four touching tiles, reading two from `mods` and two from cached neighbour **saves**
  (max three save reads per boundary chunk, memoised in `foreignMods`).
- **Preference order is a real edit over an untouched corner.** A mod stores all four corners of its
  tile, including ones the edit never reached, so "this tile has a sane value here" â‰  "this tile
  edited here". The node's pristine height is sampled once and any value differing by more than
  `SeamPristineTol` (0.01) wins outright. The audit measured exactly this shape â€” 345 of 400 nodes
  one side edited against pristine â€” so the rule reads the measured fault, it does not guess.
- If nothing was edited, the first sane value is used rather than letting each chunk roll its own
  noise sample, which keeps the four chunks bit-identical at unedited seams too.
- Tie-break order is the table above, fixed, so two tiles edited to different heights still converge
  deterministically. The canonical (wx-1,wz-1) tile still wins ties, so this is a strict
  generalisation of 1i4, not a redefinition.

### Verification of the addressing

Rule 8 asks for a worked example at a 4-chunk node, because mid-edge checks miss the wrap. Checked
all four tiles at four nodes â€” the 4-chunk corner (30,30), a west edge (30,15), a south edge (45,30)
and an interior node (17,17) â€” recomputing each tile's stored corner and confirming it lands on the
node: 16/16 correct. Two harness bugs surfaced first (PowerShell's case-insensitive `$SLOT`/`$slot`
collapsing into one variable, then comparing a *local* corner to a *world* node); both were harness
faults, and the same shape of slip is exactly what the worked example is for.

### 1i6-status
- [ ] **Confirm it compiles**, then **restart** and press F3. `C corners cross-chunk dY` should drop
      from `0.2859 (345 nodes)` toward 0, with `[cause: one-sided edit N]` emptying.
- [ ] **Watch the seam load cost.** Up to three extra save reads per boundary chunk. If streaming
      stalls at chunk borders, memoise `ForeignTileMods` across the build instead of per call site.
- [ ] Verification: code review + `tools\StaticChecks.ps1` (**0 candidates**) + the 16/16 worked
      example above. No Unity build was run.

## 1i5. Two errors 1i4 shipped: a member outside the class, and a data race on the build thread

Unity reported `CS0106: The modifier 'private' is not valid for this item` at
`WorldStreamer.ChunkBuild.cs(12,5)`. Reading it, there were two problems, and the compile error was
the *smaller* one.

### 1. The helper landed outside the class (CS0106)

The method was inserted by anchoring on the first `/// <summary>` in the file â€” which belonged to the
**partial class header**, not to a method. The result sat between the `using` block and
`public partial class WorldStreamer`, so it was a top-level member. Three things failed to catch it:

- **Grep** found `ForeignTileMods` at its call site and confirmed the definition existed.
- **Brace/paren balance was 25/25 and 101/101** â€” the method body balanced *and* the class body
  balanced, because they were two separate, independently well-formed constructs.
- **My read of the diff** went past it, because a method is a familiar shape and the anchor I had
  used to insert it felt verified.

CS0106 does not even name the class, which is the tell: the parser found a member where no type was
open. This is now **check 7** in `tools/StaticChecks.ps1` â€” a member declared at brace depth 0. It has
no false-positive shape, because there is no valid C# that trips it, and it is verified by
reintroducing the exact bug and confirming the check fires.

`ChunkBuild.cs` and `FarShell.cs` are also added to the script's `$files`, so checks 1 and 4 now cover
the two files 1i3/1i4 changed. That immediately surfaced a **pre-existing** imbalance in
`FarShell.cs`: a doc comment at the top of `EmitVoxelFarTopRun` wrote the half-open interval
`[x0..xEnd)`, which has a `)` and no `(`. It had been invisible because the file was never checked.
Rewritten as prose.

### 2. The real bug: a `Dictionary` read from the build thread

`BuildChunkMeshData` is reached through `BackgroundGenerateChunk`, which runs on a **ThreadPool
thread** while the main thread builds, unloads and demotes chunks. The 1i4 fix preferred a *live*
`_loadedChunks` lookup for the owning chunk. `Dictionary<TKey,TValue>` is not safe to read during a
write â€” a concurrent read can throw or hand back a torn entry. The audit is main-thread-only and read
`_loadedChunks` freely, so the pattern looked safe; it is safe *there* and not here, which is the
second rule-7 shape this session: **a safe idiom in one thread is not evidence it is safe in another.**

Removed. The owner is now read from its **save file only**, which is not a downgrade:

- the whole build path is already disk-driven â€” `BuildChunkMeshData` builds *this* chunk from
  `ChunkSaveManager.TryLoadChunk`, so reading a neighbour from disk is the same source the owner chunk
  itself was built from;
- it removes the race entirely, and the helper now touches nothing but `ChunkSaveManager` and its own
  locals;
- the residue is an edit in memory and not yet flushed. The seam keeps its old value until the owner's
  save lands â€” the same bounded, self-correcting staleness 1i3 documents for the far shell.

The fix's correctness does not depend on the live path: the owner chunk is the one that *has* the mod
on disk, and a chunk that has the mod on disk is the chunk the edit reached. The save flush, not the
lookup, is the clock.

### 1i5-status
- [ ] **Confirm it compiles** â€” the CS0106 is fixed; the only outstanding risk is a signature the
      static checks cannot see. Paste anything else Unity reports and it gets its own commit.
- [ ] Then **restart** and press F3. `C corners cross-chunk dY` should drop from `0.2859 (345 nodes)`
      toward `0` and the `[cause: one-sided edit N]` bracket should empty. `D boundary` should stay at
      `0` from 1i3.
- [ ] Verification: code review + `tools\StaticChecks.ps1` (**0 candidates**, including the new
      check 7, which was verified by reintroducing the bug and watching it fire). No Unity build was
      run.

## 1i4. The actual fix â€” and a misreading that cost three measurement rounds

The player answered the scoping question with one line that overturned the framing:

> every corner tile of real chunk

That is what the **original** 1hy report already said â€” *"in every chunk corner the edge will not
match"*. I read "outer z/x corner tile" as the corner of the loaded chunk square, built 1i1 and 1i3
against the near/far seam, and explicitly deferred the 345 interior nodes as "a step the player has
not reported seeing". **They were reporting it from the start.** The deferral was a mistake, and it is
worth recording as one: 1i1's question was not wrong, it was *leading* â€” it offered the loaded
square's corner and the far rim as the only two readings, and the player picked the one nearest the
truth without either being right. The right move on a location I could not resolve was to ask what
the artifact looks like, not to enumerate locations and let the answer pick my hypothesis.

**The fix.** A corner on a chunk seam is a corner of up to four chunks, and each was filling its own
copy of that height from its **own** tiles. `ApplyHeightEdits` writes every *loaded* tile at a world
coordinate, so two chunks that were both loaded when an edit landed agree â€” which is exactly why the
fault hides in a world that mostly looks right. Where one side was not loaded, it keeps pristine
noise **forever**, because nothing reconciles it: `ReconcileModifiedBorders` repairs slab-wall
bottoms, not corner heights. The audit's 345 of 400 says this is the *normal* state of a seam, not an
edge case.

`BuildChunkMeshData` now resolves an unwritten **boundary** corner from the world rather than from
this chunk's own tiles:

- **Resident owner first.** If the chunk that owns the node is loaded, adopt its live
  `LatticeY` â€” the same grid it renders, strictly better than a save that may predate the session.
- **Otherwise its save**, loaded once per neighbour and filtered by the same rules the build path
  already uses.
- **Interior nodes and locally-written corners are never touched.** A non-NaN corner is this chunk's
  own tile's value and wins; `gx>0 && gz>0` is interior and can never be foreign, because the
  east and north edges are owned by this chunk's own tile.

**Addressing, worked through (rule 8 â€” the copy's arithmetic *is* the seam contract).** The stamp
above it is `corners[LocalX+1, LocalZ+1] = Heights[1]`, so the tile that owns a world node is always
the tile one metre back in each axis, always as its NE slot. Owner chunk = chunk of tile
`(wx-1, wz-1)`; value = that tile's `Heights[1]`. Worked example at world `(30,30)`, where four
chunks meet: owner is chunk `(0,0)`, owning tile is its local `(29,29)`, value is `Heights[1]`. The
first draft of this fix hardcoded the owning tile as `(29,29)`; working a **west edge** through the
same rule gave `(29, gz-1)`, so the local tile coords are derived from the node rather than assumed.
That is the mistake rule 8 exists to catch, and it was caught before the commit rather than in
Unity.

Only ~61 boundary nodes per chunk can be foreign, touching at most four neighbours, so the cost is a
handful of save loads per chunk build.

### 1i4-status
- [ ] **Restart the play session.** Chunk corner grids are built once and cached; a resident world
      will not show this either way.
- [ ] Press F3. `C corners cross-chunk dY` should drop from `0.2859 (345 nodes)` toward `0`, and the
      `[cause: one-sided edit N]` bracket should empty. Confirm with the number rather than the eye.
- [ ] `D boundary real-vs-far dY 0` from 1i3 should stay at 0 â€” the two fixes are independent and
      both should hold.
- [ ] Watch for a one-time hitch while chunks build: a chunk build may now load up to four neighbour
      saves. If it is noticeable, the follow-up is to cache those per stream pass, not to remove the
      lookup.
- [ ] Verification: code review + `tools\StaticChecks.ps1` (0 candidates), braces 25/25, parens
      101/101. No Unity build was run â€” confirm the project compiles and the world still builds.

## 1i3. The fix: a far cell must read the LIVE grid for the chunks it overlaps

Third F3 run, and it is the first trustworthy one:

```
A fingerprint  buckets 1  [step 6 verts 100 x361]  stepDrift 0/361 (want step 6)
B void  ring 0..32  footprints 4225  real+far 0  real only 361  far only 3864  NOT DRAWN 0
C corners  nodes 400  rendered 1444/1444  no-vertex nodes 0  cross-chunk dY 0.2859 (345 nodes)
              own-lattice dY 0
D boundary  nodes 76  no-far-surface 0  real-vs-far dY 0.2084  (under 2cm noise floor 1)
    worst at (-240,330)  [cause: one-sided edit 16 (first at (300,0)) NO side edited 19]
VERDICT D-R1: the near/far boundary steps by 0.2084 m at (-240,330)
```

- **`B` is clean â€” `NOT DRAWN 0`, `far only 3864`.** The 2965 was entirely 1i2's `FarShadowedByCoarse`
  bug; a corrected walk over the identical band finds every footprint covered. The band is sound.
- **The 19 "no side edited" nodes are still arithmetic.** `under 2cm noise floor 1` means only one node
  fell below the 2 cm gate and nineteen sat just above it â€” that is float rounding on a pair of values
  each within 1 cm of pristine, i.e. a ~2 cm disagreement. The gate sits in the rounding regime; the
  count is not evidence about the lattice. Read them as noise.
- **`C` and `D` agree**: 345 of 400 interior shared corners and 16 of 76 boundary nodes are one-sided
  edits, and the boundary steps by `0.2084 m`. The mechanism is confirmed on both seams.

**The fix.** The gap is at the seam where a far cell's domain **overlaps** the loaded ring â€” a span-6
cell spans six chunks, so the ring it borders lives inside the same block. For those chunks
`BuildFarChunkCorners` was reading `ChunkTileMod`s out of the **save files**, while the real chunk
renders its **live** corner grid (`ApplyHeightEdits` writes that all session). Two sources, one world
node, and neither surface has side walls in low-poly â€” so the difference is a see-through crack at
exactly the corner the player reported.

`BuildFarChunkCorners` now prefers `ChunkObject.LatticeY` â€” the live `_merged.Corners` grid, the same
one the real chunk renders from â€” for a loaded chunk's **boundary** corners. Those are the only nodes
it shares with another surface, so that is the whole of the defect. Interior nodes are deliberately
left on the disk path: they sit behind the real chunk, and their lattice indices can differ on a
refined tile, which is a risk this fix does not need to take.

**Known limitation, stated rather than hidden.** A far cell is built once and cached, so an edit made
to a rim chunk *after* the cell was built still leaves the seam stale until that cell is rebuilt. This
is the same class of staleness as the rest of the renderer (rule 11) and is strictly better than the
old behaviour, where the source was a save file that was stale by construction.

**Second, separate defect â€” not fixed here.** The 345 interior one-sided edits are between two *real*
chunks and are untouched by this change. `ApplyHeightEdits` writes every loaded tile at a world
coordinate, so both sides should have been written together; the one-sided cases are where one side
was not yet loaded. That needs a load-time reconciliation (an extension of
`ReconcileModifiedBorders` to corner heights), and it is a `0.2859 m` step the player has not
reported seeing. Deliberately left for 1i4 rather than folded in here.

### 1i3-status
- [ ] **Restart the play session before looking.** This is a change to how the far shell is built, so
      per rule 11 nothing already resident is rebuilt â€” a cell already in `_farSectors` keeps the
      heights it was built with. Restart, walk to the near/far boundary of the loaded ring (about
      270â€“300 m out), and check the corner tiles.
- [ ] Expect `D boundary  real-vs-far dY 0` and `cause of those N node(s)` to disappear from the
      verdict. Press F3 to confirm the number rather than trusting the eye.
- [ ] `C` will still report `cross-chunk dY 0.2859 (345 nodes)` â€” that is the separate interior
      defect described above and is expected to remain.
- [ ] Watch for a lighting crease along far-cell shared edges: a cell that uses live data for a
      loaded chunk can now differ from its neighbour cell, which uses the disk path. This is a real
      trade-off of the fix; if it shows, it is a narrower follow-up, not a regression to roll back.
- [ ] Verification: code review + `tools\StaticChecks.ps1` (0 candidates). No Unity build was run â€”
      confirm the project compiles and that the far shell still builds.

## 1i2. The 1i1 readout's two loudest results were both the audit's own bugs

```
D boundary  nodes 76  no-far-surface 0  real-vs-far dY 0.2084
    worst at (-240,330)  [cause of those 36 node(s): one-sided edit 17 (first at (300,0))
                          NO side edited 19 (not an edit - look at the lattice/seed)]
VERDICT B: 2965 footprint(s) inside the fully-owned ring ... nothing draws there
```

The `VERDICT` and the `NO side edited` line were both **defects in the measurement**, and both had
to be fixed before the gap could be worked on. What survives is narrower and more useful.

### Defect 1 â€” B demanded `!FarShadowedByCoarse`, which is a hole in the renderer's own design

`FarShadowedByCoarse(cell)` is true whenever a **live coarser cell already covers the same ground**
(1eq/1er demote handoff) â€” it means "this cell is a reserved shadow and something else is drawing
here", not "nothing is here". B required it to be *false* for a footprint to count as covered, so
every reserved shadow was reported as a missing surface. All 2965 were this.

This is **1hy's rule-7 mistake one level in**. 1hy's walk admitted one family of owner (chunks with
a live mesh) and called everything else absent; 1i1's walk admitted one family of *far owner* (the
cell the ownership predicate named) and called everything else absent. A coverage check has to admit
every owner that can draw. `AnyFarCellDrawsAt` now asks the direct question â€” is any of the three
cell levels that can geometrically cover this chunk switched on â€” enumerating the candidates rather
than re-deriving the renderer's shadow rule.

**The 2965 is retracted, not resolved.** How much of that band is genuinely covered is unknown until
the corrected walk runs; only that the previous number was meaningless.

### Defect 2 â€” the label was inverted, and it conflated two different failures

`(of which no far cell claims 2965)` was printed from the counter for footprints that **do** have an
owner. Voids are now split:

- **claimed-dead** â€” a far cell claims this ground and nothing is drawing it. A true hole.
- **unowned** â€” no far cell claims it; the horizon stops short of the promised `keep` radius. Not a
  missing surface, just a shorter one.

These get separate `VERDICT` lines. One alarming number that mixes "broken" with "smaller than
promised" is worse than two honest ones.

### Defect 3 â€” the pristine classification has a width, and it swallowed the noise floor

Two values each within `RenderedCornerTolerance` (0.01 m) of the same pristine value can differ by up
to **2 Ã— tol**. So a node could be "stepped" and "both sides pristine" simultaneously â€” and 19 nodes
1â€“2 cm apart were filed under `not an edit - look at the lattice/seed`, which reads like a finding
and is arithmetic: they are the noise floor of two facets rounded to the same lattice. Classification
and the `D-R1` verdict now both require `> 2 Ã— tol`, and the sub-2 cm count prints as a noise floor.

### What actually survives the readout

- **A: clean** â€” one bucket, no step drift. Premise holds.
- **C is the real finding, and it is large: 345 of 400 shared corner nodes are one-sided edits**, worst
  `0.2859 m`, first at `(30,60)`. Not a handful of seams â€” essentially every interior seam in the
  loaded square.
- **D confirms the same mechanism one level out: 17 of 76 boundary nodes are one-sided edits**, worst
  `0.2084 m`, and `no-far-surface 0` â€” the shell does reach the loaded ring, it just arrives at a
  different height. The 19 "lattice/seed" nodes are retracted per defect 3.

So the mechanism proposed in 1i0/1i1 is **confirmed on both seams**: a corner is written on one side
of a seam and its neighbour keeps the pristine value. `BuildChunkMeshData` fills a chunk's corner grid
from that chunk's own `ChunkTileMod`s, `ApplyHeightEdits` reaches only loaded tiles, and
`ReconcileModifiedBorders` repairs slab walls rather than corner heights. Nothing reconciles the pair.

### 1i2-status
- [ ] **Press F3 again.** Three things to read: (a) does `B` now show `claimed-dead 0`; (b) `D`'s
      `under 2cm noise floor` count, which tells us how much of the old 36 was arithmetic; (c) `D`'s
      cause bracket with the corrected threshold.
- [ ] Then implement the reconciliation as **1i3**. It is the confirmed mechanism, so this is the
      first point at which a fix is justified â€” and it must still be its own commit.
- [ ] Nothing in the world's behaviour has changed across 1hy/1i0/1i1/1i2. The gap is still there and
      is expected to persist until 1i3.
- [ ] Verification: code review + `tools\StaticChecks.ps1` (0 candidates), braces 46/46, parens 406/406.
      No Unity build was run â€” confirm F3 still compiles and runs.

## 1i1. The gap is at the near/far boundary â€” a seam section C was structurally unable to see

The player localised it, and that answer was worth more than the next column of 1i0:

> the gap still happen, and to be precise the gap only appear at the outer z/x corner tile

"Outer x/z corner tile" is the **corner of the loaded square**, which is precisely where the loaded
set stops and the far shell takes over. And that is a seam 1hy's lane never measured. Section C walks
`_loadedChunks` and compares real chunks against each other, so it is blind there *by construction*:
the loaded set's outer ring meets the far shell, and section C has no opinion about that meeting.

This is the lesson worth keeping, and it is a rule-7 lesson rather than a bug: **a walk's scope is a
hypothesis, not just a radius.** 1hy was careful to scope section B to the band where a void is even
*possible* â€” and then scoped section C to the loaded set, which silently encoded "the defect is
between two real chunks" as an assumption nobody had checked. The premise-first ordering of rule 7
was right about *staleness* and was blind to this, because a walk can be correctly scoped for a
cause and still be scoped to the wrong **family of owner**.

**What shipped (measurement only, still no fix).** Section D:

- Walks every corner node where the loaded set does not occupy all four quadrants
  (`LoadedChunksTouchingCorner < 4`) â€” the whole perimeter of the loaded square, including all four
  outer corner tiles.
- Locates the far side with the renderer's own `FarCellForChunk` and reads the **far cell's own
  uploaded mesh** at that world point, taking the topmost vertex for the same reason section C does.
  A far cell is placed at `cell.X*30` with a local-origin mesh (per `CreateFarSector`), so the read
  is `world - cell.X*30` and no second spelling of the placement is introduced.
- Reports `real-vs-far dY` â€” the step between a loaded chunk's corner and the shell's grid on the
  same world point. **Neither surface has side walls in low-poly (1hi.1), so any step here is a
  see-through crack, not a terrace.**
- Reuses 1i0's pristine test across the seam instead of within it, which is a sharper test than in C:
  the far band has no save mods of its own (a dig cannot reach past the near ring), so a far surface
  sitting exactly on pristine noise against an edited real corner is a corner whose edit never
  reached the shell.

The mechanism this is hunting is already visible in the code and is the same one-sided-edit family as
1i0, one level out: `BuildFarChunkCorners` builds a far cell's grids from `ChunkTileMod`s read out of
the **save files**, while a real chunk renders from its **live** tile data. At an outer corner tile the
real side is loaded and edited and the far side â€” chunk (10,10) and its neighbours â€” is past the
loaded ring, so it is pristine noise, and nothing reconciles the pair. Section D measures that rather
than asserting it.

**Deliberately not done.** No fix. The two candidate causes (one-sided edit at the seam vs. a lattice
error) imply completely different fixes, and the magnitude will say which: a step of centimetres is an
edit; a step of metres is structural.

### 1i1-status
- [ ] **Press F3 again and paste the two new lines:** `D boundary` and its `worst at â€¦` line. The
      `VERDICT` will now be `D-R1` if the boundary is stepped, and will carry the pristine
      classification in brackets.
- [ ] If `D-R1` reports a large delta (metres), the cause is structural â€” the corner lattice, not
      saves. If it is centimetres, it is the one-sided edit and the fix belongs in the seam
      reconciliation.
- [ ] `1i0`'s section-C classification bracket should come back in the same readout; it is still
      valid evidence about the interior seams and is independent of section D.
- [ ] 1hy/1i0/1i1 are all read-only. Nothing about the world's behaviour has changed yet, so the
      gap is expected to persist until the fix lands.
- [ ] Verification: code review + `tools\StaticChecks.ps1` (0 candidates). No Unity build was run â€”
      confirm F3 still compiles and runs.

## 1i0. The F3 readout answered *where*; this adds the one column that answers *why*

1hy's lane ran and returned a clean three-section report, and it eliminated most of the field:

```
A fingerprint  buckets 1  [step 6 verts 100 x361]  stepDrift 0/361 (want step 6)
B void  ring 0..32  footprints 4225  real+far 0  real only 361  far only 3864  NOT DRAWN 0
C corners  nodes 400  rendered 1444/1444  no-vertex nodes 0
              cross-chunk dY 0.2859 (345 nodes)  own-lattice dY 0
    worst cross-chunk at (60,-30)
VERDICT C-R2: rendered corner heights disagree ACROSS chunks by 0.2859 m at (60,-30)
```

- **A is clean** (one bucket, no drift) â€” the mixed-resident-set premise fails, so B and C are real
  evidence rather than evidence about a world that is not on screen. This is exactly what the
  premise-first ordering was for.
- **B is clean** â€” 0 voids across all 4225 footprints in the owned band. **There is no hole.** The
  whole lifecycle hypothesis (a footprint with nothing drawing it) is dead, and with it the idea
  that the fix belongs in the streaming/dormant path.
- **C-R1 is clean** â€” `rendered 1444/1444` is exactly 361 chunks x 4 corners, so every chunk places
  a rendered vertex at all four of its corners, and 400 nodes is the correct corner count for a
  19x19 chunk square.
- **`own-lattice dY 0` with `cross-chunk dY 0.2859` is the whole finding.** Each chunk's rendered
  corner matches its own lattice *exactly*, and adjacent chunks disagree at shared corners. So the
  render path is not the culprit: each 31x31 lattice faithfully reproduces its own tile data, and
  the two tiles that own the same world corner hold different heights for it.

**What shipped (measurement only, no fix).** `cross-chunk dY` said *where* but not *why*, and the
cause is not a single thing, so section C now classifies every disagreeing node by where its
contributors sit relative to pristine world noise â€” read straight off the data, not inferred:

- **one-sided edit** â€” one contributor exactly on untouched `GetHeight(seed, nx, nz)`, another off
  it. This can only mean the corner was written on ONE side and nothing propagated across the seam.
- **all sides edited apart** â€” no contributor is pristine; both were written and they differ.
- **no side edited** â€” every contributor sits exactly on pristine noise yet they disagree, which
  would rule the edit path out entirely and point at the lattice or the seed instead.

The pristine test is exact, not a proxy: it calls the same 3-argument `GetHeight` overload
(`baseHeight = 0f`) that `BuildChunkMeshData` uses to fill its corner grid, so "pristine" means the
identical value the build would have produced. A `_modifiedChunks` membership count is collected
alongside as a coarser corroborating signal.

**Why this is where I stopped.** Code review does point at a mechanism, and it is in the save/edit
path rather than the mesh path: `WorldStreamer.ChunkBuild.cs` assembles its `corners` grid from
**this chunk's own** `ChunkTileMod`s at chunk-local indices, so a world corner on a chunk boundary
is stamped by a modified tile in the edited chunk while the neighbouring chunk fills the same world
corner from its own grid. `ApplyHeightEdits` *does* propagate across chunk boundaries â€” it iterates
world tile coords, not chunks â€” but its own doc says "**Unloaded tiles are ignored**", and
`ReconcileModifiedBorders` only fixes side-wall bottoms, not corner heights. So an edit applied
where the far side was not yet loaded leaves a one-sided corner forever, because nothing reconciles
it after the fact. 0.2859 m is the right magnitude for that: a `FlattenAt` rim edit, whose feather
drives the per-corner delta to a few centimetres, and far too small for a seed or slot error (those
would be metres).

That is a strong inference, not a measurement, and the two candidate fixes are very different in
scope â€” a per-corner reconciliation against neighbouring chunks' saves is a save-format-adjacent
change. So the classifier ships alone, and the fix waits for its readout.

### 1i0-status
- **Verified by grep + reread only** (rule 3 â€” no CLI/Unity build). `tools\StaticChecks.ps1` ->
  **0 candidates**; `WorldStreamer.CornerAudit.cs` braces 36/36, parens 296/296. The 3-argument
  `GetHeight(long, float, float, float baseHeight = 0f)` overload confirmed present, and `int` ->
  `float` narrows it at the call. `CrossCause` declared once, called twice (printed line + verdict).
  `Seed` confirmed `public long Seed`, `_modifiedChunks` a readable `HashSet<TerrainChunkCoord>`.
  The new `RenderedCornerNode` fields are written in the first pass and read in a second read-only
  pass, so no enumerator is mutated mid-walk.
- **Pending play-test:** press **F3** again anywhere. Only the `C corners` line changes; it now ends
  with a `[cause of those N node(s): ...]` bracket.
  - `one-sided edit N` dominant -> confirmed. The fix is the cross-chunk corner reconciliation, and
    the `first at (x,z)` coordinate is a corner to walk to and look down.
  - `NO side edited N` dominant -> the edit path is innocent and the lattice/seed is the real
    suspect; nothing else in this entry's analysis applies.
- **Not done, deliberately:** the fix, per rule 7.

## 1hy. Measurement first: the read-only rendered-corner + void audit, for "in every chunk corner the edge will not match â€” the player can see the void through that gap"

The report survived a clean play-session restart, which killed the staleness hypothesis (rule 11) and
left a static pass that found **no defect**: `BuildCornerGrid`'s owner/slot arithmetic is correct in
all four branches, the low-poly root covers local `0..30` inclusive (max lattice index 960), the four
chunks meeting at a 2x2 junction all emit a *coincident* vertex at the same world point, and
`GetHeight` is sampled from world coordinates so the lattice is shared by construction. Rule 8 is
explicit that this is exactly the situation where a proof is not enough: the four-chunk node cannot be
a hole **conditional on the drawn mesh being the step-6 facet root**, and nothing had measured whether
it is. So per rule 7 this task ships the measurement *only* â€” no behaviour change.

**What shipped**

- **`WorldStreamer.CornerAudit.cs` (new partial), `RenderedCornerAudit()`, bench key F3.** The 1hv
  F3 lane, restored and extended. Three sections, asked in the order the questions depend on each
  other, and a `VERDICT` line naming the first failure with the world XZ to walk to.
  - **A fingerprint** â€” distinct `(MeshStep, vertexCount)` buckets across the loaded set, plus
    step-drift against `EffectiveLowPolyStep`. **This runs first because it is the premise test:**
    more than one bucket means two versions of the generator built this world, which makes B and C
    evidence about a world that is not on screen.
  - **B void** â€” the direct test of "a gap", and the only failure mode that can open a hole in an
    otherwise watertight chunk set. Walks every chunk footprint in the band the streamer and far
    shell jointly own and asks one question per footprint: does *anything* draw it? Real chunk =
    in `_loadedChunks` with an active hierarchy; far cell = the owning cell exists, is active, and is
    not shadowed by a live coarser cell (1er retention). Neither => a void, listed nearest-first.
  - **C corners** â€” R1: does every loaded chunk place a rendered vertex AT each of its four corners
    (coverage, vs the number of loaded chunks that *should* meet at the node). R2: do the chunks at a
    node agree on the corner height, and does each rendered corner match **its own** `LatticeY` â€” the
    rule 8 copy contract, measured rather than assumed.
- **The void walk is scoped to rings `0 .. view + FarOuterKeep`, and that is the point.** It is
  exactly the union of what the two owners promise (real chunks to `view + 1`, far cells from
  `near + 1` to `view + FarOuterKeep`), so the band is covered by construction and a hole in it means
  something. Past it, real chunks are DORMANT and hidden and no far cell owns them â€” a ring of
  "voids" at 630 m that is correct, expected, and not what anyone is looking at. The band is printed
  so the number cannot be misread. It deliberately does **not** re-derive
  `ChunkLodManager.EffectiveCullDistance`: that formula is private, and a second spelling here would
  be a copy that rots when the LOD side changes (rule 8 again).
- **Two read-side judgement calls, both recorded in the file.** A disabled root `MeshRenderer` is
  counted as its own `rootHidden` signal and is *not* treated as a void, because a LOD detail band
  deliberately disables the root and draws a child instead. And the corner match takes the **topmost**
  vertex at the corner, not the min, because a smooth root carries side walls hanging off the same
  edge â€” the 1hv lesson, kept.
- **`tools\StaticChecks.ps1` now covers the new file, and its check 4 was wrong.** Adding the file to
  `$files` immediately reported 4 CS0165 candidates, all false: the check could not see that an `out`
  parameter is assigned by the callee's contract, nor that a bare local written only through an `out`
  call argument (the `TryGetValue` pattern) is assigned. A check that flags every `out` param trains
  its reader to ignore the section, so both shapes are now recognised. Note the reported lines are the
  *caller's* locals in `RenderedCornerAudit()`, not the callee's parameters â€” both are now correct.

### 1hy-status
- **Verified by grep + reread only** (rule 3 â€” no CLI/Unity build; Unity is the compiler).
  `tools\StaticChecks.ps1` â†’ **0 candidates** (after the check-4 fix). Brace/paren balance on
  `WorldStreamer.CornerAudit.cs` 33/33 and 271/271. Every referenced symbol confirmed present and
  accessible: `RenderDistance`, `NearRingRadius`, `VoxelTerrainEnabled`, `EffectiveLowPolyStep`,
  `_focus`, `_loadedChunks`, `_dormantChunks`, `_farSectors`, `FarOuterKeep` (const), `FarCellForChunk`,
  `FarShadowedByCoarse`, and on `ChunkObject` `RootMesh` / `MeshStep` / `LatticeY` / `VoxelMesh`. All
  11 new `WorldStreamer` symbols are declared exactly once and confined to the new file; all 4 bench
  symbols are confined to `NewWorldTestGround.cs`. `LatticeY(30,30)` -> index 960 of 961, in range.
  `WorldStreamer.SeamAudit.cs` is confirmed still deleted (1hx), and the new file references nothing
  from it.
- **Pending play-test (user side):** press **F3** standing next to a visible gap, screenshot the
  report, then walk to the XZ it names.
  - If `A fingerprint` shows **more than one bucket**, the resident world is mixed-version and B/C
    describe nothing â€” restart again and re-press.
  - If `B void` shows `NOT DRAWN > 0` inside the near ring, that is the hole; the listed chunk coords
    and world XZ are the place to look.
  - If both are clean, the gap is in the corner layer only, and section C's numbers say which of R1
    (a corner the surface never reaches) or R2 (a corner height that disagrees) it is.
  - Still worth one look, because it decides where the search goes next: does the gap sit where
    **four** chunk borders radiate from it, or only **one**?
- **Not done, deliberately:** no fix. Rule 7 requires the measurement and the fix to ship as separate
  tasks so the readout that justified the fix stays in history. The fix is 1hz+1, chosen by the
  `VERDICT`.

## 1hz follow-up. The behavioural orphan: `RichManNPC`'s night-club hangout is gone, the 21:00 deal is not

The 1hz pass deleted the night club and then handed me three greps. All three came back clean,
and the tree still shipped a **behavioural orphan** â€” the third residue class of the new rule 13:
every symbol resolved, the file compiled, the code ran, and it was just an NPC walking an empty lot.
`RichManNPC` had carried a complete nightly routine aimed at the deleted building: a 19:00â€“21:00
window (`CLUB_WINDOW_START` / `CLUB_WINDOW_END`), a path out to the club entrance, a
`ClubHangState` â†’ `ClubIdleState` machine with watch/pace spots, and a toast saying he was at the
bar. The *type* was gone, so the grep for the type said there was nothing left to do.

**What shipped**

- **The routine is deleted, not renamed**: `ClubHangState`, `ClubIdleState`, `_clubHangState`,
  `_clubIdleState`, `_clubIdleTimer`, `_clubPaceSpots`, `_clubPaceIndex`, `_clubStandSpot`,
  `_clubHintedDay`, `HandleClubHangout()` and `FaceToward()` are all gone, along with the
  19:00â€“21:00 window and the two `_clubHangState = ClubHangState.None` resets. This is the
  rule 13 corollary in action: deleting the block took its locals with it, so the sweep grepped the
  *removed block's* identifiers (`CLUB_WINDOW_END`, `IsPlayerInClub`) for readers elsewhere in the
  file rather than assuming the block was self-contained.
- **The deal is untouched, and its hour is now one constant.** `TryStartDeal` had a bare
  `TimeOfDay < 21f` and `ForceStartDealForWatch` had `SetTimeOfDay(CLUB_WINDOW_END)` â€” the rule 10
  shape: a metric two code paths must agree on, written twice. Both now read `DEAL_HOUR` (21:00), so
  the hint window, the start gate and the watch harness cannot drift apart. The hint's own `19:00`
  is written `DEAL_HOUR - 2f` so it is derived rather than a third literal.
- **Vocabulary renamed to match the surviving thing**: `IsPlayerInClub` â†’ `IsPlayerAtDealSite`,
  `ClubCenter` â†’ `DealSiteCenter`. Same coordinates, same Â±12 Ã— Â±8 m box â€” the plot is the deal
  site now, not a club entrance, and a method that says `InClub` over a lot with no building on it
  is the kind of lie that survives three more features.
- **The breadcrumb is location-agnostic.** Removing the hangout also removed the quest's *only*
  in-game lead ("he is at the barâ€¦"), and the player has no other way to find `(0, 0, 95)`. The
  replacement is one message per evening from 19:00, day 3+, undiscovered:
  *"PhÃº Ã”ng hay ra ngoÃ i vÃ o ban Ä‘Ãªm... hÃ£y tÃ¬m háº¯n."* / *"The rich man slips out at night... find
  him."* â€” no building named, because none is there. The deal camera's own toast was reworded from
  "sneaks toward the bar" to "slips to the meeting spot", and the old bar key is deleted.

### 1hz follow-up-status
- **Verified by grep + reread only** (rule 3 â€” no CLI/Unity build; Unity is the compiler).
  `tools\StaticChecks.ps1` â†’ **0 candidates**. Brace/paren balance re-checked: `RichManNPC.cs`
  123/123 and 710/710, `Localization.cs` 1223/1223 and 136/136. An exact scan for all fifteen removed
  identifiers returns zero hits, and `VisitState.WalkingHome` was confirmed to still have a writer
  and a reader after the block that used to set it went away.
- **Localization was checked on both sides of the runtime lookup** (rule 13's last corollary): the
  reworded key and the new key are each present in the dictionary *and* referenced from exactly one
  `T()` call site, and a `-SimpleMatch` sweep for `quÃ¡n bar` across every `.cs` returns nothing.
- **Not a runtime check.** Nothing here has been seen on screen. Play-test:
  1. Reach day 3 undiscovered and watch the clock pass 19:00 â€” the hint should appear **once**, with
     no NPC walking anywhere, and not again until the next day.
  2. At 21:00 he should still walk to `(13, 0, 95)` and start the deal; use the bench's
     `Watch Deal Scene (Test)` to skip the wait. The camera, eavesdrop, bribe and leave rows must
     all still work, and the camera toast should no longer mention a bar.
  3. Stand in the Â±12 Ã— Â±8 m box around `(0, 0, 95)` during the walk and confirm he does not stall
     (the deal is handled before the proximity check precisely so this cannot happen).
  4. Confirm nothing in the console mentions a removed symbol â€” Unity's compiler is the only real
     check for that.

## 1hz. The 1hz feature-removal pass, finished â€” multiplayer, night club, fast travel, horse, and the `Recovery` scene dump all gone; the tree compiles again

The removal itself was already staged in the working tree (29 scripts staged-deleted) when the session
started; what was missing was the half that makes it *ship*. Deleting a type and grepping its **name**
finds the call sites but not the code that existed only to **feed** the deleted system, so the tree
did not compile: four POI files were still constructing `FastTravelSign` objects for a
`FastTravelMenu` that no longer existed. There was also a **silent** bug, not a loud one â€” see the
second bullet under "The two things grep could not tell me".

**What shipped in this pass**

- **POI sign producers stripped** (`Town.cs`, `DungeonSystem.cs`, `FastTravelNode.cs`,
  `POIGenerator.cs`): the `TravelSign` properties, the sign GameObjects + trigger colliders +
  `SignPost`/`Board` cubes, `POIGenerator.AssignTravel`, the `_nextTravelIndex` counter and its four
  mutations, and the now-unused `int index` parameter on `FastTravelNode.Build`. All stale
  `<see cref="FastTravelSign"/>` / `<see cref="FastTravelMenu"/>` XML doc references are gone too.
  `FastTravelNode` and `PoiKind.FastTravel` **deliberately stay** â€” `POIGenerator.PlaceFastTravel`
  and the streaming cull sweep (`NewWorldSystems.cs:166`) still reach them; they simply have no
  registry entry, so no bonfire spawns. `POIDefinition`'s tooltip was corrected too: it claimed a
  `"?"` marker, but `WorldMapUI.Refresh:52` appends `âœˆ`.
- **The save-key trap closed.** `WorldBuilder.Persistence.LoadBuildingsFromSave` still listed
  `"NightClub"` in the essential-building restore branch while its `RebuildEssentialBuilding` case
  was deleted. That branch rebuilds the building, then stamps
  `_buildings[_buildings.Count - 1]` with the saved health / maxHealth / part healths / door state â€”
  so a pre-1hz save stamped the **night club's** condition onto the **previous** building in the
  list. Not a compile error; a save that loads "successfully" and corrupts a building. The key is
  dropped, so an old `NightClub` entry falls through to the generic path, where
  `CreateBuildingEntity` returns `null` for an unknown type (`WorldBuilder.Blueprints.cs:429-430`) â†’
  `SpawnBuildingDirect` returns `false` (`:551`) â†’ the entry is skipped untouched.
- **14 dead localization keys removed** (`Localization.cs`, the `// Horse & fast travel (Phase 3D)`
  block). Every key was checked unreferenced by exact `-SimpleMatch` across all `.cs` files, and
  `Localization.T` is a one-way Vietnameseâ†’English lookup, so the second dictionary column is not
  independently reachable. No `.json`/`.asset` data file supplies them either. This is what took
  `"Há»™p ÄÃªm & NhÃ  HÃ ng"` ("Night Club & Restaurant") out of the build.
- **Orphan folder metas removed**: `Assets/Scripts/{Networking,NightClub,Vehicles}.meta` were still
  tracked and clean in `git status` while their folders were already gone.
- **`Assets/_Recovery/` gitignored** and its 50 tracked files (`0.unity` â€¦ `0 (24).unity` + metas)
  untracked via `git rm -r --cached`. All 52 files remain on disk â€” nothing was deleted from the
  user's machine. This also silently cleaned up two loose ends: the stray edit to
  `0 (24).unity.meta` and the untracked `0 (25).unity` pair.
- **`tools/StaticChecks.ps1` now covers the two `WorldBuilder*.cs` files this task edited**
  (`WorldBuilder.Persistence.cs`, `WorldBuilder.NPCs.cs`). Per rule 3, a `WorldBuilder*.cs` that
  is not in `$files` silently falls out of checks 1, 4 and 5.

**The two things grep could not tell me** â€” both are now the new `AGENTS.md` rule 13:

1. *Producers outlive their consumer.* `FastTravelSign` was deleted along with its menu, and the
   grep for `FastTravelSign` "succeeded" â€” it found only the four producers, which is exactly what
   needed deleting. There was no signal that anything was left over.
2. *A save key outlives its builder.* `RebuildEssentialBuilding` has no `NightClub` case and no
   compiler complains, because a missing `case` in a `switch` over a string is legal. The corruption
   is a runtime data bug visible only in an old save file.

**Left for a follow-up commit** (shipped separately, see the `1hz follow-up` entry above): `RichManNPC`'s
club hangout â€” a whole state machine (`ClubHangState`/`ClubIdleState`, pacing spots,
`HandleClubHangout`) pacing a building 1hz deleted. The 21:00 dealer story and its camera gate were
kept. That is a *behavioural* orphan, invisible to both greps above.

### 1hz-status
- **Verified by grep + reread only** (rule 3 â€” no CLI/Unity build in this project; Unity is the
  compiler). `tools\StaticChecks.ps1` â†’ **0 candidates** across all five files, including the two
  newly added. Brace/paren balance re-checked by hand on the 6 POI files, `RichManNPC.cs` and
  `WorldBuilder.Persistence.cs` (e.g. `RichManNPC` 140/140 braces, 739/739 parens).
- **A real bug caught during the edit**: `DungeonSystem.BuildLayout`'s `doorDir` local is declared
  next to the sign block that was removed but *read* 20 lines later at `:71` (`enemy spawn offset`).
  Deleting the block's `Vector3 doorDir = ...` would have compiled only as a definite-assignment
  error â€” exactly the class of thing rule 3 exists for, found here by grepping the removed block's
  identifiers for other readers rather than trusting the delete.
- **GUID scan clean**: every deleted `.meta` GUID was searched across all 41 `.unity` / `.prefab` /
  `.asset` files in `Assets`; zero references. The scan itself was probe-tested (it does find
  `WorldBuilder.cs.meta`'s 10 real references), so a zero is a real zero and not a broken search.
  One stale serialized field remains in a now-gitignored file (`Assets/_Recovery/0 (8).unity`
  `RideSpeed: 13`), which is harmless and untracked now.
- **Not a runtime check.** Nothing here has been seen on screen. Play-test:
  1. **Load a pre-1hz save that contains a night club** and walk the village â€” every building's
     walls and door should be intact, and specifically the building *before* the old club site must
     not carry the club's health. This is the only way to observe the save-key fix.
  2. Spawn a town / dungeon / boss-arena / fishing / treasure POI and confirm it still builds with
     no console errors and no missing-component warnings, and that `Town`/`DungeonSystem` no longer
     report a null `TravelSign`.
  3. Open the world map: POI rows should still show the `âœˆ` marker and `FT`/`Town`/etc. kind names.
  4. Confirm the fast-travel menu and the multiplayer menu are genuinely gone from the main menu.
  5. `Assets/_Recovery/` should be absent from `git status` but the files should still be in the
     folder on disk.
- **Left for the follow-up commit** (now shipped â€” see the `1hz follow-up` entry above): the `RichManNPC`
  club hangout removal + the `DEAL_HOUR` constant, and the two remaining "bar" localization strings.
- **Left deliberately in place**: `Packages/manifest.json` still lists
  `com.unity.multiplayer.center: 1.0.1`. Nothing references it (verified), but removing a package
  is a separate call from removing code, so it was not touched here.

## 1hy. Weapon combo-track pass (iron_sword / dagger / fist / gauntlets) â€” committed as found, three spots flagged for the play-test

An animation edit that was sitting uncommitted in the working tree through 1hv/1hw/1hx. The user
asked for it to be folded in and committed rather than discarded, so this entry exists to say exactly
what is in the file now and which three places read like a copy-paste slip rather than tuning. **No
keyframe was changed by this task** â€” the values are shipped as found.

Pose keys are `K(t, shX, shY, shZ, elX[, wrX, wrY])` (`WeaponAnimator.cs:114-115`) and each weapon
def is `WeaponAnimDef(mode, ownerTracks, otherTracks, accent, timeLight, timeHeavy, slashLead)`
(`:97`). What changed:

- **`iron_sword`** â€” clips 1 and 2: the strike key's wrist X `95f -> 125f` (a harder wrist roll into
  the cut). Clips 3 and 4 **swapped**: the overhead chop and the forward thrust exchanged places in
  the combo order. `SlashLead` stays `true`.
- **`dagger`** â€” all four combo variants replaced with **byte-identical copies of `iron_sword`'s four
  tracks**, comments included. The dagger's own jabs (low jab / high jab / quick double / lunging
  stab, with its faster `0.24f/0.34f` timings) are gone, while `SlashLead` is left `false` and the
  timings are unchanged â€” so the def now carries slashing poses with non-slash lead behaviour.
- **`fist`** â€” clips 1/3 keep the jab (a double jab repeating the jab was already true). Clip 2 is
  now a side cross driven by **shoulder Z** (`shZ 90f`, then `shY -90f`/`shZ 45f` on the follow
  through). Clip 4's wind-up key is now `K(0.01f, 360f, ...)` â€” a full **360Â° shoulder-X rotation at
  t=0.01** unwinding to rest by t=0.99, where it used to be a `-140f` uppercut cock.
- **`gauntlets`** â€” all four variants are now effectively the **same pose pair** (`-72f/0f/0f/-90f`
  then `-72f/0f/0f/0f`); variant 2 differs only by a missing space in the source. Jab, cross, double
  and uppercut are visually indistinguishable.
- Cosmetic only: `lance` variant 1 re-indented to column 0, `longbow`'s two `V(T(...))` collapsed
  from two lines to one.

### 1hy-status
- COMMITTED AS FOUND at the user's request; **not** independently reviewed as an animation change.
  Verified by grep + reread only (rule 3 â€” no CLI/Unity build): `WeaponAnimator.cs` balances
  97/97 braces, 656/656 parens; the diff touches only pose-track literals and two reformats inside
  the `Catalog` table, no signature, no call site, no serialized field. `WeaponAnimator` is **not** in
  `tools\StaticChecks.ps1`'s file list and this task adds nothing to `WorldBuilder*.cs` or
  `NewWorldTestGround.cs`, so the script was not re-run for it.
- Nothing outside this file references these tracks: the defs are a private static table keyed by
  weapon id, and `WeaponRigBuilder` mounts the animator. No GUID, save key or part key is involved.
- **Flagged, not fixed** (each is a judgement call about intent that only the play-test can settle):
  1. `dagger` playing `iron_sword`'s slash tracks with `SlashLead = false` â€” either the clips were
     pasted in by mistake, or the dagger is meant to become a slashing weapon and `SlashLead` was
     missed.
  2. `gauntlets` â€” four variants, one pose pair. The boxer chain (jab, cross, double, uppercut) is
     not readable.
  3. `fist` clip 4 â€” a 360Â° shoulder spin as an uppercut wind-up, versus the `-140f` cock it
     replaced.
  Also worth an eye: `iron_sword`'s chop/thrust order swap changes which swing the **third** combo
  hit plays.
- Play-test: equip an `iron_sword`, a `dagger`, `fist` and `gauntlets` on the bench rack
  (`EnableWeapons` lane) and swing the full light combo on each. Confirm (a) the sword's third hit is
  the one you want, (b) the dagger reads as a dagger and not a short sword, (c) the fist finisher does
  not corkscrew the shoulder, (d) the gauntlets show four distinguishable swings. If any fails, it is
  a one-line keyframe fix in a follow-up commit (never an amend).

## 1hx. Coarser facet step (3 m -> 6 m) and the removal of the F2/F3/F4 measurement lanes

Two changes in one pass, because they are the same decision seen from two sides: **the world should
read as low-poly, and the instruments that measured the old look are gone at the user's request.**

**The facet step moved 3 -> 6, in lockstep in the two places it lives.** `WorldStreamer.LowPolyStep`
(near chunks) and `FarSectorStep` (`WorldStreamer.FarShell.cs`) are one constant wearing two hats, so
only one may move: `WorldStreamer.cs` `LowPolyStep = 3 -> 6`, `NewWorldTestGround.cs` (the bench copy
applied in `Awake`) `3 -> 6`, `FarSectorStep` `return 3 -> 6`. 6 is legal because it divides all three
spans â€” 30 (chunk side), 90 and 180 (the far cells' span-3/span-6 boxes) â€” so every grid row still
lands exactly on a chunk boundary. Cell vertex counts follow: a rim cell 6x6 (was 11x11), a band-B
cell 16x16 (was 31x31), a band-C cell 31x31 (was 61x61); a near chunk root is 25 quads / 36 verts
(was ~121 quads). The cost is the collider, which rides the same step, so footing is lumpier and prop
heights (still sampled on the 1 m lattice) float/sink by up to the facet error.

**Why 6 and not "more triangles":** facet shading contrast scales with `curvature x span`, and on
this 5-octave field (base octave amplitude 55 m at frequency 0.0012) 3 m facets came out
near-coplanar â€” adjacent flat normals barely differed, so the world still read as smooth haze. The
span was the lever, not the triangle count. That is now **AGENTS rule 12**, written so the next
edit cannot change one file and invert the world's density.

**Removed, at the user's request** (they are still in git history, with their measurement findings):
- `Assets\Scripts\World\Streaming\WorldStreamer.SeamAudit.cs` (1hj, F2) â€” lattice cross-chunk
  comparison, hidden roots, interior holes, far/real rim step.
- `Assets\Scripts\World\Streaming\WorldStreamer.CornerAudit.cs` (1hv, F3) â€” the rendered-mesh
  corner audit plus the resident-staleness fingerprint.
- `WorldStreamer.DropResidentTerrainKeepSaves()` (1hw, F4) â€” the one-key resident drop that kept the
  saves, and the whole `EnableResidentDrop` / `ResidentDropKey` / `RunResidentDrop` bench lane.
- Their `NewWorldTestGround.cs` wiring: 3 toggles, 3 keys, 3 cached strings, 3 HUD lines, and the
  `Update()` blocks. The `ChunkObject` QA accessors those audits read **stay** â€” they are the only
  safe way to ask those questions, and nothing else uses them.

**The consequence, stated where it will be read:** rule 11's remedy is now a **play-session restart**.
`ResetTerrainSaves()` would do the same drop in the right order but first calls
`ChunkSaveManager.ResetWorldSaves(Seed)`, which permanently deletes the player's terrain edits, and
the non-destructive wrapper is what 1hx removed. So a render-algorithm change is not observable until
every chunk has been rebuilt from scratch â€” and two things make that worse: an inspector tweak to
`LowPolyStep` / `VoxelTerrainEnabled` mid-session silently does nothing (the value is only read at
dispatch), and a chunk **mid-build** captured its mesh mode at dispatch, so it lands after any unload
with the old settings.

### 1hx-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). `tools\StaticChecks.ps1`
  reports **0 candidates** (it covers `NewWorldTestGround.cs`, the only edited file in its list;
  balance 143/143 braces, 695/695 parens). The three deleted `WorldStreamer` partials plus the
  `DropResidentTerrainKeepSaves` removal were checked by hand: **all 15 removed symbols
  (`SeamAudit`, `RenderedCornerAudit`, `DropResidentTerrainKeepSaves`, `EnableSeamAudit`,
  `SeamAuditKey`, `EnableCornerAudit`, `CornerAuditKey`, `EnableResidentDrop`, `ResidentDropKey`,
  `_seamAuditText`, `_cornerAuditText`, `_residentDropText`, `RunSeamAudit`, `RunCornerAudit`,
  `RunResidentDrop`) now have 0 hits** over `git ls-files "*.cs"` â€” the whole-repo form, because
  `Assets\Scripts\**\*.cs` does not reach `World\Streaming\` in PowerShell (1hw's lesson).
  `WorldStreamer.Streaming.cs` balance was hand-checked after the 51-line excision.
- **No scene or serialized-asset reference was touched**: neither deleted partial nor the removed
  method was GUID-referenced by any serialized asset (the only scripts that are are the 12 in
  `SampleScene.unity`, none of them in this change).
- Docs in the same pass: `AGENTS.md` rule 11 (restart-only remedy + the mid-dispatch and
  inspector-tick traps) and **new rule 12** (the facet size is one decision in two files; it must
  divide 30/90/180; a coarser step is not free); `game-design.md` Â§2.2 keeps the four-cause gap
  taxonomy and the "a check only speaks for the layer it reads" lesson, and records that the lanes
  that measured it are gone.
- Play-test (nothing about this is visible without a **restarted** session):
  1. Enter play, let the near ring and the far shell build, and look at the horizon and at your feet.
     The facets should read as flat plates with clear normal breaks, not smooth haze.
  2. Walk to a chunk boundary and across it: no density break, no crack â€” 6 divides 30.
  3. Check footing on a slope (lumpier by design) and that trees/rocks sit on the ground rather than
     floating (a 6 m facet can differ from the 1 m lattice by more than a 3 m one did).
  4. Press **F1-F4** in the bench: F1 is the skill hotkey, F2/F3/F4 are now unbound â€” that is the
     removal, not a bug.
- Not done, deliberately: no automatic re-drop, no generation counter for in-flight builds, and the
  audit findings themselves are not re-litigated here â€” 1hj/1hv/1hw measured a corner-gap report and
  the F3 staleness fingerprint was never read back. A future gap fix should expect to **rebuild** a
  measurement rather than read cause A off an existing validator (rule 8).

## 1hw. Resident-terrain drop key (F4) + public `DropResidentTerrainKeepSaves()` â€” the executable form of AGENTS rule 11

Chosen while 1hv's measurement was waiting on the user's readout: the drop was, until now, a manual
"restart the play session", which destroys the A/B it is supposed to enable (a restart rebuilds
everything at once, so "the corner gap is gone" proves nothing about *which* chunk was stale).

**Shipped** â€” `WorldStreamer.DropResidentTerrainKeepSaves()` (new, in `WorldStreamer.Streaming.cs`
beside `ResetTerrainSaves`) + bench lane `EnableResidentDrop` (default **off**, like every other
world-mutating lane) / `ResidentDropKey` (**F4**) / `RunResidentDrop()` / HUD line.

The method is deliberately `ResetTerrainSaves()` **minus the destructive middle**: `ClearFarShell()`,
then a snapshot of loaded **and** dormant keys, then `UnloadChunk` + `EnqueueChunkIfNeeded` per chunk
(the `ForceRebuildChunk` pair, private, now reachable from outside). Nothing else: no
`ResetWorldSaves`, no `FlushPendingSaves`, no `_dirtyTiles.Clear()`. `UnloadChunk` *persists* pending
edits on its way out, so a sculpted world comes back sculpted â€” the save files are the contract, and
this call cannot touch them.

Two things that are easy to get wrong and are handled explicitly:
- **Dormant chunks must be unloaded, not just re-queued.** `EnqueueChunkIfNeeded` WAKES a dormant
  chunk instead of re-dispatching it (`WakeChunk`: same GameObject, same pooled mesh, same tile
  data), so a loaded-only loop would leave every dormant chunk holding the old geometry until the
  player walked back into that region. `UnloadChunk`'s dormant branch removes it from the dictionary
  and destroys the object, which is what makes the re-queue a real rebuild.
- **A chunk mid-build cannot be caught, and the code says so.** `DispatchPending` snapshots
  `VoxelTerrainEnabled` + `EffectiveLowPolyStep` at *dispatch* time and hands them to the worker, so
  an in-flight chunk is in neither dictionary yet (nothing to unload), and `EnqueueChunkIfNeeded`
  refuses to re-dispatch it. Re-dispatching it anyway would be a coin flip â€” `FinalizeChunks` keeps
  whichever result arrives first and discards the other (`if (_loadedChunks.ContainsKey(...)) continue;`).
  Making that deterministic needs a per-chunk generation counter in the streaming core, which is not
  a bench-key's business. So the method logs the straggler count and asks for a second press once a
  readout shows `inflight 0` â€” both audits already print it.

### 1hw-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). `tools\StaticChecks.ps1`
  reports **0 candidates** (it covers `NewWorldTestGround.cs`). `WorldStreamer.Streaming.cs` is not
  in the script's file list, so its edit was hand-checked: braces 69/69, parens 334/334, every local
  declared before use, and the `UnloadChunk(keys[i])` overload is unambiguous (`TerrainChunkCoord`
  argument, exactly as `ResetTerrainSaves` calls it two methods above).
- One check came back **misleading and was redone**: `Select-String -Path Assets\Scripts\**\*.cs`
  only reaches two directory levels in PowerShell, so it never saw `World\Streaming\` at all and
  reported `DropResidentTerrainKeepSaves` as declared zero times when the call existed. Redone over
  `git ls-files "*.cs"` (431 files): the symbol has exactly 2 hits (definition + call),
  `RunResidentDrop` 2, `EnableResidentDrop` 3, `ResidentDropKey` 3. A second bug in the same pass:
  combining `-Pattern ([regex]::Escape($s))` with `-SimpleMatch` made `Key.F4` (the dot) search for a
  literal `Key\.F4` and report 0 hits; re-run as a regex, `Key.F4` is used exactly once in the project
  â€” the new field â€” and F1/F2/F3 are the skill hotkey and the two audits, so F4 was genuinely free.
- Contract: one call, no saves touched, returns the re-queued count. The bench HUD line carries that
  count plus the collider warning, so a screenshot after the rebuild says how much was thrown away.
- **Hazard, documented in the tooltip and on the HUD rather than hidden**: the drop removes every
  chunk collider until the rebuild lands (~1â€“2 s at the per-poll budget), so pressing it while
  standing on streamed terrain means falling through the world. Press it from the bench platform or
  in flight. This is why the toggle defaults **off** while the two audits default on.
- Play-test:
  1. Tick `EnableResidentDrop` on the bench, stand on the platform, press **F4**. HUD shows
     `resident drop: N chunk(s) requeued, saves kept`.
  2. Press **F3**. Its fingerprint should collapse to a single `(step, verts)` bucket and
     `stepDrift 0` â€” that is what "the drop worked" looks like. Then walk to the corner gap and press
     F3 again: gaps gone â‡’ staleness confirmed; gaps remain â‡’ the render path (1hv's other branches).
  3. Optional: sculpt or dig something on the terrain, then F4 â€” the edit must still be there. That
     is the "keeps the saves" half of the claim, and it is the half `ResetTerrainSaves` gets wrong.
- Not done, deliberately: no generation counter for in-flight builds, and no automatic re-press. Both
  would change streaming behaviour for every player to serve a QA key.

## 1hv. Rendered-corner audit lane (F3) â€” the layer the seam audit cannot read, shipped BEFORE any fix for the corner-gap report

User report: "in every chunk corner it wont match the edge so player can see the void through that
gap." Rule 7 applies â€” a report I cannot see gets a read-only measurement lane FIRST, and the fix is a
separate task. Reasoning trail: `THINKING.md` Â§1hv.

**Why the existing seam audit was not enough.** `WorldStreamer.SeamAudit()` (1hj, F2) already
measures "gaps between terrain chunks", and its section A already covers the 4-chunk corner node
transitively (it walks both the east and the north edge of every chunk). But A compares
`ChunkObject.LatticeY` â€” the **lattice**, i.e. the DATA layer. A renderer that draws its corner
vertex from somewhere other than the lattice it stamps reports "worst dY 0 OK" and still parts at the
corner. Section B catches a short/hidden root (active state, vertex count, X/Z bounds) and cannot see
whether the four vertices meeting at a corner agree. This is rule 8's "never let a validator stand in
for a layer it does not read" in a new costume.

**What shipped** â€” `Assets\Scripts\World\Streaming\WorldStreamer.CornerAudit.cs` (new partial) +
`NewWorldTestGround.cs` (`EnableCornerAudit` default on, `CornerAuditKey` default **F3**,
`RunCornerAudit()`, HUD line that persists like the seam audit's). `RenderedCornerAudit()` walks the
**uploaded chunk meshes** and reports:

- **R1 coverage** â€” for every loaded chunk, whether a rendered vertex sits AT each of its four
  corners (mesh-local 0/30 within 0.05 m). A node's *expected* contributor count is the number of
  loaded chunks touching it (up to four: `n/30` and `n/30 âˆ’ 1` per axis), so a chunk that is loaded
  with no mesh is counted as missing rather than quietly skipped.
- **R2 cross-chunk spread** â€” at each node, how far apart the corner heights of the chunks meeting
  there are. "The render path is not sharing one corner height."
- **R2 own-lattice delta** â€” each rendered corner against **its own** chunk's `LatticeY`. "The render
  path copies the corner from the wrong place" (rule 8's copy contract, measured).
- **Staleness fingerprint** â€” distinct `(facet step, vertex count)` buckets across the loaded set plus
  a step-drift count against the current `EffectiveLowPolyStep`. Buckets > 1 = a mixed resident set;
  drift > 0 = a uniformly old one. Both are the rule-11 failure mode and are invisible by eye.

A `VERDICT` line names the first thing that failed, so one screenshot answers the question.

**Two things the measurement had to get right, both found by deriving rather than assuming:**
- **The corner height is the TOPMOST vertex, not the min.** A *smooth* root also carries side walls
  hanging down from the same edge, so the min at a corner is the bottom of a wall and would have
  reported a false own-lattice mismatch on every chunk. The low-poly root has no walls, so this would
  have looked fine on one setting and lied on the other.
- **The address arithmetic** (rule 8): a chunk root sits at
  `(tc.X*30*ChunkData.Size, 0, tc.Z*30*ChunkData.Size)` and `ChunkData.Size == 1`, so mesh-local Y ==
  world Y â€” which is what makes "rendered corner vs `LatticeY`" a legal comparison, the same
  assumption the seam audit's bounds check already makes.

### 1hv-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Braces/parens balance in both
  edited files (`WorldStreamer.CornerAudit.cs` 23/23 and 173/173, `NewWorldTestGround.cs` 149/149 and
  737/737); all seven new symbols are declared exactly once across the `WorldStreamer*` partials (no
  collision with `SeamAudit`); `ChunkObject.VoxelMesh` and `WorldStreamer.VoxelTerrainEnabled` were
  grep-confirmed to exist before being referenced. `tools\StaticChecks.ps1` reports **0 candidates**
  (it does cover `NewWorldTestGround.cs`, so that edit is inside its checks).
- Cost/contract: one `Mesh.GetVertices` per loaded chunk through a single reused list; strictly
  read-only â€” no rebuild, no re-stamp, no forced poll â€” so the numbers describe the frame the key was
  pressed on. The second dictionary pass does not write at all, so no enumerator can be invalidated.
- **NO fix ships in this task, by design.** Which branch fires decides where the fix goes: `R1` â‡’ the
  surface emitter never reaches the corner; `R2` â‡’ corner-derivation arithmetic in the render path;
  `R-stale` â‡’ rule 11's missing public "re-render everything, keep the saves" API. The readout picks
  the branch; guessing would have picked all three.
- Play-test (needs `EnableFpsStats` on, which is default):
  1. Stand near a corner where the void shows, press **F3**, screenshot the HUD. The `VERDICT` line
     names the mechanism; paste it back.
  2. Press **F2** as well â€” if A reports clean while F3 reports R1/R2, that pair is the proof the
     lattice is fine and the render path is not.
  3. Zero-code A/B: restart the play session (every chunk then rebuilds through `BuildOrLoadChunk`)
     and press F3 again. **Gaps gone â‡’ staleness (rule 11). Gaps remain â‡’ the render path.**
- Left alone deliberately: LOD children (`Lod1`/`Lod2`) and the far shell are NOT in this pass. A
  corner gap at the near/far rim is section D of the seam audit; a gap that appears only when a LOD
  band is active is a third question, and neither should be folded in before the first readout.
- Housekeeping: the new partial also got its `WorldStreamer.CornerAudit.cs.meta` (new unique GUID),
  because every other script in `Assets\Scripts` is tracked with one and a missing one would leave
  untracked churn the first time the editor imports it. The class is only ever referenced by name
  (never serialized into a scene or prefab), so no existing GUID reference is affected.

## 1hu. AGENTS rule 11 â€” editing the terrain RENDER algorithm must drop the resident terrain (loaded + dormant + far shell), keeping the saves

User request: "whenever edit the map rendering algorithm then clear the cached map so it would create
anew" (clarified: terrain, not the world-map menu). Reasoning trail: `THINKING.md` Â§1hu.

**Docs-only task** â€” no code change. It is a rule because the *sequence* was missing, and the
missing part is not the obvious one.

- **What "the cached map" actually is**: not a map cache. `WorldMapUI` is a POI text list,
  `CompassMinimapHUD` is a compass + dot that never samples terrain, and `MapBuilder.*` is the legacy
  village prop builder. The holders of terrain render output are the streaming chunks themselves.
- **Three of them, and each fails differently.** (1) `_loadedChunks` â€” every `ChunkObject` owns an
  uploaded `RootMesh`; the generator only runs inside `BuildOrLoadChunk`, which a loaded chunk never
  re-enters. (2) `_dormantChunks` â€” a demoted chunk is re-shown *in place* ("same GameObject, same
  pooled mesh, same tile data") and `EnqueueChunkIfNeeded` **wakes** a dormant chunk instead of
  re-dispatching it, so it is a landmine that pops the stale mesh back when the player returns.
  (3) the far shell â€” its cells are sampled from the real chunks' surfaces, so a drop that omits
  `ClearFarShell()` moves the seam out to the ring boundary instead of removing it.
- **The consequence, written into the rule**: a mid-session render edit leaves a world where
  resident chunks are OLD-algorithm and newly streamed chunks are NEW-algorithm, and the two part
  along their shared edges.
- **`ResetTerrainSaves()` is named as the wrong tool for this job**: its sequence is exactly right
  (`ClearFarShell()` â†’ unload + requeue every loaded *and* dormant chunk) but it first calls
  `ChunkSaveManager.ResetWorldSaves(Seed)`, deleting the `tc_*.dat` files â€” which hold **heights**,
  i.e. data, not render output. It stays correct for its own job (the opt-in
  `EnableResetTerrainSaves` clean-map lane). For a renderer change the rule names
  `UnloadChunk(tc)` + `EnqueueChunkIfNeeded(tc)` per chunk and records that this pair exists only
  privately (`ForceRebuildChunk`), with `ForceRebuildArenaLane` as the public precedent.
- **The mesh pool is explicitly excluded**, because it is the intuitive answer and it is wrong:
  `UploadMerged` re-specifies every channel and `Mesh.Clear()`s on a vertex-count change, so a
  pooled `Mesh` is a buffer, not a stale cache.
- Recorded as a **known gap**: there is no public "re-render everything, keep the saves" entry
  point, so the drop stays a manual step. If a future task wants one, `ForceRebuildChunk` is the
  private pair to promote.

### 1hu-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Docs-only: `AGENTS.md` (new
  rule 11), `game-design.md` Â§2.3 (the re-render bullet beside the existing force-rebuild API entry),
  `PROGRESS.md`, `THINKING.md`. No `.cs` file was touched, so nothing was compiled and nothing can
  have broken.
- All the code claims in the rule were read out of the files before being written down:
  `WorldStreamer.Streaming.cs` (`WakeChunk`, `EnqueueChunkIfNeeded`, `UnloadChunk`,
  `ForceRebuildChunk`, `ResetTerrainSaves`, the wake-pass comment), `ChunkMeshGenerator.cs`
  (`AcquireChunkMesh` / `ReleaseChunkMesh` / `UploadMerged`).
- **Open and deliberately unresolved**: the user's corner-gap report in the same message. A
  mid-session render edit is the one mechanism this repo can create on demand that matches it, but
  that is a hypothesis (see `THINKING.md` Â§1hu H5). Rule 7 applies â€” the measurement is task 1hv and
  no fix ships until its readout names the mechanism.
- Play-test: none for this task (nothing to see). The user should do the zero-code A/B from 1hv:
  press the corner lane, then restart the play session and press it again.

## 1ht. Character Info tab band 40@10 â†’ 32@8, full button art on every button, and framed value fields

User request: reduce the Info / Skills / Inventory / Map / Faith button height further, make Change
Class and Change Race use the same PNG as the other buttons, and put a border on the text fields that
hold race / class / stats. Reasoning trail: `THINKING.md` A1ht.

**What changed**

- `TabBarHeight` 40 â†’ **32**, `TabBarTopY` 10 â†’ **8**. Band is now y 260â€“292 (16 units shorter, its
  centre 10 units higher). `TabLabelInsetY` stays 8, so the label box is 16 tall and the clamped font
  is `min(0.95 Ã— (32 âˆ’ 8), max(24, h/44))` = 22.8 at 1080p. The Skills header row at 236 and the tree
  viewport top at 200 are untouched â€” 1hs already moved the rows into the 200â€“250 corridor, and the
  new corridor is simply 10 units taller at the top, so nothing had to move again.
- **Every `MakeButton` call site now applies the full frame.** The audit found exactly three that
  did not: Change Class, Change Race and the Faith panel's Switch Faith. All three were still wearing
  the short "stats menu button" art that `MakeButton` lays down by default, which is why the two
  buttons the user named looked different from their neighbours. The three now call
  `ApplyFullButtonSprite(...)` like the other six, so there is one rule at the eight call sites rather
  than an exception list.
- **Value fields are framed** (11 stat inputs + the class and race rows): a 1.5-unit border drawn
  *inside* the field's own rect as four flat `Image` strips (`AddBorderStrip` / `AddFieldBorder`),
  with the field's text inset 4 so the border never touches the first character. The stat inputs keep
  their dark fill â€” the border is a child of the *field's* rect, not a second `Image`, because that
  rect is the `TMP_InputField`'s `targetGraphic` and has to stay the clickable fill.
- **Class / race rows moved and grew**: âˆ’96 / âˆ’130 â†’ **âˆ’80 / âˆ’118**, and 30 â†’ **34** tall. Both
  follow from the same arithmetic. The border has to be *inside* the box (the rows are 4 apart and
  the last is 2 from the buttons below, so an outside border lands on a neighbour), which costs 4 of
  inset on every edge, which leaves a 22-tall text box on a 30-tall row â€” too short for a 22.5pt
  line, whose descent would then cross the border. 34 leaves 26. `ValueLineFontSize(rowHeight)` caps
  the font against the *inset* box, and both call sites use it, so this cannot drift.
- Rejected along the way, with reasons in `THINKING.md`: a 9-sliced border sprite (Unity scales a
  slice by the drawn rect's own dimension â€” ~2 units on a 64-wide field, ~58 on a 700-wide row), and
  padding the frame *outside* the class/race label (the top border then lands exactly on the first
  line of glyphs, because a pivot-top rect grows downward, not upward).

### 1ht-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Braces and parens balance in
  all three edited partials (`CharacterInfoUI.cs` 75/75 and 580/580, `CharacterInfoUI.Stats.cs` 32/32
  and 218/218, `CharacterInfoUI.Faith.cs` 61/61 and 241/241), no `ApplyFieldFrame` call survives
  anywhere, all 9 `MakeButton` call sites are followed by `ApplyFullButtonSprite`, and the new helpers
  (`AddBorderStrip` / `AddFieldBorder` / `MakeFieldFrame` / `InsetBoxPos` / `InsetBoxSize` /
  `ValueLineFontSize`) are each declared exactly once across the `CharacterInfoUI*` partials.
  `tools\StaticChecks.ps1` reports **0 candidates** (it does not cover these files â€” a guard that
  nothing else broke, not a check of this change).
- Play-test: open Character Info and confirm (a) the 5 tabs read correctly with no glyph clipped at
  1080p and at 1440p+, (b) Change Class / Change Race / Switch Faith look like the other buttons,
  (c) the stat fields, class row and race row are visibly framed and the digits/text do not touch
  the frame, (d) typing in a stat field still selects/edits normally â€” the border strips are
  `raycastTarget = false` specifically so they cannot steal the click.
- Left alone deliberately: the stat *name* / *value* labels and the "+" allocators are not framed â€”
  only the fields are, since the request was about the fields.

## 1hs. Character Info tab bar â€” 84 units tall at 36 â†’ 40 at 10, and the rows it was covering

User request: "reduce the tab button in the tab menu height and put their center higher". The two
numbers were a 2-minute edit; the *reason* the band was 84 tall is that nothing constrained it, and
it was covering live content. Reasoning trail: `THINKING.md` Â§1hs.

**Measured first (all figures design units; the design space is 1066Ã—600, top edge y +300, and
`Body` is inset 60 top / 60 bottom so panel content shares the canvas centre):**

| element | y extent before | drawn |
|---|---|---|
| tab band (5 buttons) | 180â€¦264 | over everything below |
| Skills General/Class/Race sub-tabs (`P(-160, 250)`, pivot top, 30 tall) | 220â€¦250 | **behind the band** |
| Skills Skill Points / Learned (`P(âˆ“, 222)`, pivot top-left, 28 tall) | 194â€¦222 | **behind the band** |
| Faith title / status (`P(0, 238)` / `P(0, 206)`) | 204â€¦238 / 180â€¦206 | **behind the band** |
| Info level readout (`P(-330, 210)`, 64 tall) | 146â€¦210 | top 30 units behind the band |
| Inventory "Equipment" heading + storage header | 178â€¦222 / 210â€¦238 | top of each behind the band |

The band is created by `BuildTopButtons` *after* `Build()` made the body row, so it is the later
sibling and Unity draws it **on top** â€” the Skills sub-buttons and both top readouts were simply
not visible, and the Faith title/status were not either. The panels were authored blind to the
band because nothing in their coordinate system mentions it.

**What changed**

- `TabBarHeight` 84 â†’ **40**, `TabBarTopY` 36 â†’ **10** (34 in the fit pass). Band is now y 250â€¦290:
  **40 units shorter, its centre 48 units higher** (30 below the screen top, was 78).
- Those two numbers are now **constants read by both write sites**. They were duplicated â€” and
  already disagreed (36 at build, 34 on the first resize), so the bar used to change height position
  the first time the window was resized.
- **Skills header row** â†’ one named `SkillsHeaderY = 236` for all five widgets. The sub-tabs
  (250) and the two readouts (222) are horizontally disjoint â€” x âˆ’450â€¦âˆ’250 and 250â€¦470 against the
  buttons' âˆ’220â€¦160 â€” so they now **share one row** in the corridor between the band's bottom edge
  (250) and the tree viewport's top edge (200): 14 clear of the band, 6 clear of the viewport.
  `TreeViewport` is a `RectMask2D` built *after* this row, so any overlap would clip the buttons.
- **Faith status** 206 â†’ **192**. The title's glyphs grow *down* from y 238, so at 206 the two ink
  blocks overlapped by ~6 units. That was invisible while the band covered the pair; raising the
  band would have turned a hidden collision into a visible one, so it is fixed here.
- **Tab label font is clamped** to its own box: `min(0.95 Ã— (height âˆ’ inset), max(24, h/44))`. At
  1080p/1440p this is the old value unchanged (24.5 / 32.7 â†’ 30.4 capped); without it a 40-tall
  button would be outgrown by the raw `Screen.height / 44` term on a 1440p+ window.

### 1hs-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Braces balance in both
  edited files (`CharacterInfoUI.cs` 71/71, `CharacterInfoUI.Faith.cs` 61/61), no remaining literal
  `84f` / `36f` / `34f` tab geometry anywhere in the `CharacterInfoUI*` partials, and
  `tools\StaticChecks.ps1` still reports **0 candidates** (it does not cover this file â€” it is a
  guard that nothing else broke, not a check of this change).
- Revealed by raising the band, deliberately left alone: the Info level readout, the Inventory
  "Equipment" heading and the storage header were partly hidden and are now fully visible. All
  three are horizontally or vertically clear of what sits under them (checked: level ink ends ~24
  above the XP bar; the equipment heading spans x âˆ’450â€¦30 while the storage grid starts at x 120).
- Not touched: the panel `TabDesignBox` rects, which are declared independently of the band and are
  already looser than the content they cover.
- PENDING PLAY-TEST (open the Character Info panel, each tab): the 5 top tabs are a **shorter row
  hugging the top of the screen**, all five labels centred in their buttons and none clipped; on
  **Skills** the General/Class/Race sub-buttons and the Skill Points / Learned readouts are now
  **visible and clickable** (they were behind the band) with clear air above and below the row, and
  switching sub-tabs still repaints; on **Faith** the title and status read as two separate lines;
  on **Info** and **Inventory** the top readouts are fully visible; resize the window (aspect
  change) and confirm the bar does **not** jump â€” that is the duplicated-literal bug this removes.

## 1hr. Static compile-risk sweep + faith-lane placement audit, and `tools/StaticChecks.ps1`

Unity is the only compiler this project has (rule 3), and 1hq proved that a session's
hand-written grep can miss a whole error class. So the checks were mechanised into
**`tools/StaticChecks.ps1`** (outside `Assets/`, so Unity never compiles it) and run over
every file this thread touched. **It is not a compiler** â€” it reports candidates, Unity
decides. Current result: **0 candidates.**

- **Run it:** `powershell -ExecutionPolicy Bypass -File tools\StaticChecks.ps1` from the repo
  root. Six checks:
  1. brace/paren balance in `WorldBuilder.Blueprints.cs`, `WorldBuilder.cs`, `NewWorldTestGround.cs`;
  2. **arity** â€” every call of the six structure helpers must match *some* declared overload
     (this is the check that would have caught the CS0029, and it is overload-aware:
     `CreatePartBoxOn` legitimately has a 5-arg and a 6-arg form);
  3. `return <void helper>(...)` anywhere in the file (the CS0029 class);
  4. locals declared with no initializer, each reported with whether it is assigned later in
     the block (the CS0165 heuristic â€” both current hits, `partColor` and `learnedAny`, are
     assigned before use, so they are *not* errors);
  5. **cross-case local reads and duplicate case-scope names in every switch** of the three
     files (the CS0165 that 1hq fixed, generalised from one method to all switches);
  6. **part-key parity** â€” every `Shrine_*`/`Church_*`/`Pagoda_*` key in the `_shrineSubBuildings`
     -style tables must have a `case`, and every `case` must be in a table. This is the
     silent no-build failure of rule 9, and it now covers all three structures, not just the
     shrine: **Shrine 12/12, Church 13/13, Pagoda 14/14**.
- **1hp placement audit (rule 4's "measure before fixing", applied to my own fix).** `PlatformSize`
  is 120, so the platform spans `cx Â± 60`, `cz Â± 60`. Checked the three new NPC positions against
  every other lane rather than assuming they were clear:
  - taoist `(cx âˆ’ 30, cz âˆ’ 50.5)` â€” the rebuilt shrine's foundation ends at `cz âˆ’ 47.6` and its
    stair foot at site `z = âˆ’9.6`, so 2.9 m clear of the stair; the boss (`SpawnBoss`, box
    2.4 Ã— 1.6 m) is at `(cx, cz âˆ’ 50.4)`, i.e. the **same Z** but 30 m east, so no overlap; the
    NPC lane sits at `cz âˆ’ 21.6`, far north.
  - monk `(cx âˆ’ 39.5, cz + 44)` â€” the pagoda's 14 m podium ends at `cx âˆ’ 37`, so 2.5 m clear;
    nothing else is placed at `cz + 44` (the buildings and spell lanes are at `cz + 36`).
  - priest unchanged at `(cx + 17.5, cz âˆ’ 35)`.
  - 11 m and 20.5 m of platform margin remain on the far axes, so nothing was moved off the slab.
- **Verification:** the script (0 candidates) plus a re-read of `StandOnGround` â€” it measures
  world-space `Renderer.bounds.min.y` and writes `transform.position`, so it is correct whether or
  not the rig is parented under an offset root. No Unity build; play-test still pending.
- **Follow-up for the next session:** add the script's file list to itself when a new
  `WorldBuilder`/`TestGround` file appears, otherwise check 1â€“5 silently stop covering it.

## 1hq. Fix the two compile errors Unity found in 1hm/1hn (follow-up to 1ho/1hp)

Unity was the first compiler to run over `1hm`â€“`1hp`; it reported two errors in
`WorldBuilder.Blueprints.cs`, both shipped by earlier commits in this thread.
**Fixed in `ebad1c9`** (a new commit, not an amend, per rule 1).

- **CS0029 â€” `CreatePartPanelBetween` returned a `void`.** Line 1005 does
  `return CreatePartCubeRotated(...)`, but `CreatePartCubeRotated` was declared `void` in 1hm â€” the
  caller and the callee were written in the same commit and never compiled together. **Fix:** it now
  returns the `GameObject` it creates, matching `CreatePartCube`. This broke *every* roof panel, ramp
  and stair in all three holy places, so nothing in 1hmâ€“1hp could have run.
- **CS0165 â€” `Church_Spire` read a case-local of `Church_SpireRoof`.** A switch section's locals are
  scoped to the whole switch, so `float apexY` in the roof case was readable from the spire case â€”
  but definite assignment fails on a path that enters `Church_Spire` directly. **Fix:**
  `sprHalf / sprThick / sprEaveY / sprApexY` are now method-scope consts beside `sprTan`/`sprVert`, so
  both cases read the same named numbers and the spire cannot drift off its own roof's apex.
  `sprApexY = 10.45 + 2.72Â·0.7813 = 12.575` â€” **geometry unchanged**.
- **Class swept, not just the two:** scanned all three structure builders (`BuildPagodaPart`,
  `BuildChurchPart`, `BuildShrinePart`) for a local declared in one `case` and read in another, and
  for a name declared twice across cases. One instance (the one above), no duplicates, no other
  `return <void helper>(...)` anywhere in the file.
- **Why review missed it, recorded so it isn't missed again:** grep confirms a symbol *exists*, not
  that a signature *matches*; and a per-case local is a hidden dependency between cases that no
  geometry reading can see. Brace balance, part-name matching and hand-derived arithmetic are not
  compilation. Unity's console is the only compiler this project has â€” **paste its errors into the
  next session's handoff instead of fixing them silently.**

## 1hp. Test lane - the three faith NPCs stood on the platform with their feet 0.915 m under it

The last item of the church/shrine rebuild (`1hmâ€“1hp`). Reasoning trail: `THINKING.md` Â§1hmâ€“1hp.

**What was wrong** (`NewWorldTestGround.SpawnReligion`): all three worship NPCs were placed with
their root at bare `PlatformTopY`, but the `MapBuilder` rigs are authored around a **body origin** â€”
the taoist's shoes are at local `y âˆ’0.88` with height 0.07, so the rig's lowest point is **0.915 m
below its root**. Every one of the three stood sunk to the knees in the platform. Two further
placement faults in the same lane, both consequences of the structures' real footprints:
- the **taoist at 8.6 m south of the shrine** stood **on the shrine's new stair** (1ho's treads run out
  to `z = âˆ’9.6` on site);
- the **monk at 2 m west of the pagoda's centre** stood **inside the pagoda's 14 m podium**, and faced
  `Euler(0, âˆ’90, 0)` â€” i.e. **away** from the pagoda.

### 1hp-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). `NewWorldTestGround.cs`
  braces 146/146, `StandOnGround` defined once and called 3 times, and no other lane places a faith NPC.
- `StandOnGround(npc, groundY)` **measures** the rig's lowest renderer bound and drops the root so the
  feet land on the ground â€” deliberately **not** a hardcoded `0.915`, because only the rig knows where
  its own lowest block is and a literal would silently go stale the next time a shoe or hem moves. It
  also survives a rig that later gains a skirt or a longer robe.
- Moved: taoist to `z = âˆ’50.5` (2.9 m clear of the stair foot, still facing the shrine), monk to
  `x = cx âˆ’ 39.5` at the pagoda's own `z` and turned to `Euler(0, 90, 0)` so he faces the pagoda. The
  priest was already clear of the church and already facing it â€” unchanged apart from the height.
- **Out of scope, recorded not fixed (rule 4 â€” the legacy world is not this project's test surface):**
  `WorldBuilder.CreateWorld` places the same three NPCs at `y 0.93 / 0.93 / 1.815` against
  `ShrineBasePos`/`ChurchBasePos`/`PagodaBasePos` at `y 0`, so if the legacy ground under those spots
  is at those values the legacy NPCs are sunk by the same 0.915 m. Not verified (needs a legacy-world
  play-test) and deliberately untouched.
- PENDING PLAY-TEST (fresh test platform, `EnableReligion`): all three faith NPCs stand **on** the
  platform with their shoes touching it (previously knee-deep); the taoist is on flat ground south of
  the shrine's stair, not on a tread; the monk is off the pagoda's podium and **facing** the pagoda;
  each NPC's interaction trigger still reaches the player (the rigs' trigger boxes are authored in the
  same body-origin frame, so they move up with the figure â€” check the priest and monk can be talked to
  without standing on top of them).

## 1ho. Shrine rebuild - single-storey hall, Three Pure Ones, ridge lantern (task 3 of the church/shrine rebuild)

Finishes the rebuild the user asked for ("redo the taoist temple", "Taoism has 3 gods, so 3 statues
not 1"). Reasoning trail, including the three defects this draft introduced and the rejected
alternatives: `THINKING.md` Â§1hmâ€“1hp.

**What was actually wrong with the old shrine** (from its own declared numbers, before any gap): the
tier-1 roof was `14.4 Ã— 0.5 Ã— 5.2` â€” a **2.6 m deep eave per side on a 10.8 m interior**, so it did
not cover the hall; the tier-2 floor at `y 5.9` left a **3.1 m ceiling** over the deity, i.e. statues
under a low lid; and every part root sat at its own offset, so no two blocks shared a readable datum.
Fixed by making it a **single-storey hall with a ridge lantern**.

**Changes â€” `WorldBuilder.Blueprints.cs` (`BuildShrinePart`, all 12 cases, + new
`CreateShrineSanqingFigure`) and `WorldBuilder.cs` (`_shrineSubBuildings`):**
- One datum ladder, y = 0 at the platform top: `podium 0.45 â†’ cap 0.60 â†’ floor 0.75 â†’ column top 4.20
  â†’ architrave 4.69 â†’ eave underside 4.05 â†’ ridge underside 6.194 â†’ ridge top 6.679 â†’ lantern plate
  6.599 â†’ lantern deck 6.899 â†’ lantern eave 7.879 â†’ lantern ridge 8.607 â†’ spire base 8.791` (top 12.67).
- **The four tier-2 part keys are repurposed, not renamed and not deleted** â€” `Shrine_Tier2Floor` is
  now the lantern's sole plate, `Shrine_Tier2Walls` its four walls, `Shrine_Roof2` its gable roof and
  `Shrine_Spire` the spire on that roof's own apex. Renaming a part would make it fall through
  `SpawnStructurePart`'s dispatch and build nothing, silently (AGENTS rule 9).
- **Roof**: 18Â° gable, two `CreatePartPanelBetween` panels from an eave underside **64 cm below** the
  architrave's top at `|x| = 6.60` to the ridge underside at `x = 0`, so the rake crosses the plate's
  top plane at `4.63` and the panel is buried 0â€“51 cm across **1.57 m of bearing** on beams that run
  out to 6.20. A 14 cm drop (the church's cornice) would move that crossing to 6.17, i.e. a 1 cm graze
  at the tip of a beam. Both gable ends are closed with `CreatePartGableSteps` at 6.00 (back wall) and
  6.10 (facade) wide.
- **Facade**: two piers either side of a 3.0 m doorway, a lintel whose ends run 20 cm into them,
  pilasters 5 cm proud, a gold threshold, and a gable board over the whole front. The old colonnade's
  front-centre column position **is** that doorway (7 columns now, not 8).
- **Three Pure Ones** on one dais: Yuanshi in gold (ruyi + fan), Lingbao in jade (pearl), Daode in white
  over purple (whisk + beard), each with a lotus throne, mantle, sash, eyes and diadem, told apart by a
  `variant` argument.
- `_shrineSubBuildings`: all 12 offsets now `Vector3.zero`, sizes are the rebuilt truth.

### 1ho-status
- IMPLEMENTED; verified by grep + reread + hand-derived arithmetic (rule 3 â€” no CLI/Unity build).
  Brace balance of the file: 188/188. No duplicate declarations in `BuildShrinePart`. All 12 table
  part names compared against the 12 `case "Shrine_*"` labels: **agree**. `CreatePartGableSteps` is
  called in 5 places (1 church, 2 shrine gables, 2 lantern gables) and all 5 pass both half-spans.
- **Four defects this draft introduced, found on reread:** (a) the front gable board was 6.60 wide
  under a 6.60 eave while the facade piers end at 6.10 â€” the board overhung the wall it stands on;
  (b) the censer's 2.0 m bowl at `z = âˆ’3.60` ran **through** the new facade â€” moved to `âˆ’2.60`;
  (c) the dais at `z = 3.60` put the middle figure **inside the rear centre column** (its 0.95 base
  occupies `z 3.425..4.375`) â€” dais and figures moved to `2.20`, with the altar/step/mat re-spaced to
  `âˆ’0.20 / âˆ’1.30 / +0.70`; (d) **the gable helper emitted inverted bands here**: it sized every band
  from `wallTop`, but with the eave 64 cm under the architrave the roof's underside is already *below*
  the wall top out at `|x| > 4.815`, so the outer band came out **0.13â€“0.16 m tall the wrong way** â€” a
  mirrored cube with an inside-out collider. Fixed in the helper (clamp the run to where the underside
  meets `wallTop`, re-band inside it, skip any band that still inverts). The church's gable is affected
  only cosmetically: it now stops at 6.019 instead of 6.15, and that 0.13 m of board was inside the
  roof slab either way. The lantern's gable is unchanged (its triangle reaches the wall's full width).
  The lantern plate was also moved off "45 cm into the ridge": that put its bottom 3.5 cm above the
  roof's own ceiling at the crown, a slot along the ridge visible from inside.
- **Generalised the gable helper twice over**: it now takes the wall's plane point + normal and a
  **separate `gableHalfSpan`**, because the shrine's gables (6.0/6.1) are narrower than its eave (6.60)
  and the old single-number signature traced the roof's line instead of the wall's; and it now clamps
  the band run to where the roof's underside meets the wall's top (see defect (d)). The church's two
  half-spans are equal, so its pitch is unchanged.
- **Verified a latent ship-stopper before committing:** zero offsets put all 12 part roots on the same
  point, so the footprint test would have failed if a part tested against its siblings. It does not â€”
  `PlaceStructureBlueprint` collects every sub-plan and only then adds them, so siblings never see each
  other. (`WorldBuilder.Blueprints.cs:88-105`.)
- Corrected a false claim carried from 1hn in two places: the part tables' `Size` is **not**
  "documentation only" â€” `CanPlaceBuilding` and both blueprint ghosts read it. There are no damage
  volumes.
- Docs: `game-design.md` Â§5.7 (shrine structure line + a "Shrine rebuild (1ho)" block); `AGENTS.md`
  unchanged (rule 9 already covers the convention); `THINKING.md` Â§1hmâ€“1hp; this file.
- **Save caveat:** unchanged from 1hn â€” parts are stored with absolute world positions, so an **old
  save** re-loads the shrine's parts at their pre-rebuild offsets (up to 5.9 m out) and shows a mix of
  old and new geometry. Nothing migrates old saves.
- PENDING PLAY-TEST (fresh test platform, `EnableReligion`): approach the shrine from the south and
  confirm the 4 treads step down to the platform and the 5.0 m gold inlay sits on the ground; walk
  through the doorway and confirm it is **open** (no column in the middle of the front row) with a
  threshold under your feet; look up inside and confirm the roof bears on the architrave with no
  daylight slot along either rake or at the ridge; look at the front and back from outside and confirm
  the gable triangles are closed and sit on the facade/wall; check the ridge lantern straddles the
  ridge with no gap under its plate and no gap between the roof and the lantern walls; confirm the
  spire rises out of the lantern roof; check the three statues stand clear of the rear column and are
  visible from the doorway; confirm the censer is inside the door line and the altar is between the
  censer and the dais; look for coplanar z-fighting on the yin-yang disc, the gold bands and the two
  threshold strips.

## 1hn. Church rebuild - every structural gap closed on one datum ladder (task 2 of the church/shrine rebuild)

Continues 1hm (helpers, no call sites). This commit is the first **call site** for all three helpers, and
the proof that stating geometry by its support makes the gaps unauthorable rather than merely fixed.
Reasoning trail and the hand-derived defect list: `THINKING.md` Â§1hmâ€“1hp.

**What was actually broken** (re-derived by hand from the authored coordinates, not guessed): the nave
roof covered only `z Â±4.02` of a nave whose walls are at `z Â±6.15` (â‰ˆ4.3 m open at each end); the spire
roof was centred near `z = 0` while the belfry and spire stood at `z = âˆ’6.2` (the spire had **no** roof
over it and a 1.00 m vertical gap); the ridge beam floated ~0.45 m above the panels and the eave bars
~2.46 m outside them; both side walls had a 0.50 m slot under them and ~0.425Ã—0.475 m holes at all four
corners; 12 pillar bases, 6 pews, the altar (1.02 m), the buttresses (0.50 m), the rear buttresses (off
the podium entirely), the nave floor (0.08 m), the censer-equivalent internals and the spire stages
(0.15/0.05 m) all floated; and the stained glass was **buried inside** the solid wall slabs, so six
windows showed nothing. A prior sub-agent's claim that the roof panels interpenetrate by 4.43 m was
checked by hand and **rejected** â€” the panels met correctly at the ridge; that item was not "fixed".

**Changes â€” `WorldBuilder.Blueprints.cs` `BuildChurchPart` (all 13 cases) + `WorldBuilder.cs`
`_churchSubBuildings`:**
- One datum ladder, named as consts at the top of the method, y = 0 at the platform top:
  `slabTop 0.35 â†’ capTop 0.40 â†’ floorTop 0.50 â†’ plinthTop 1.10 â†’ wallTop 4.30 â†’ corniceTop 4.60
  (the roof's bearing) â†’ ridge underside 7.35`. Every block is now a `CreatePartBoxOn` call, so its
  support is the previous line's named top.
- All 13 `_churchSubBuildings` offsets are now `Vector3.zero` and the geometry is authored in **site
  coordinates**; the declared `Size` fields are the rebuilt truth and drive the blueprint ghost and
  `CanPlaceBuilding`'s footprint test (corrected in 1ho â€” they are not "documentation only").
- **Roof**: two `CreatePartPanelBetween` panels, each from an eave underside buried 6 cm in the cornice
  at `|z| = 6.15` up to the ridge underside at `z = 0` (pitch 24.56Â°, `tan` = 0.4571, vertical depth
  0.550 = `0.50Â·âˆš(1+tanÂ²)`) â€” the panel now spans the full nave, and the pitch cannot be written
  backwards. A ridge cap is laid along each panel's own top surface (same helper, sunk 5 cm) instead of a
  horizontal beam, because a horizontal beam on a peaked roof only touches along the peak line and
  opens a gap at its own ends. Both side walls get `CreatePartGableSteps` infill built from **the same
  eave/ridge numbers the panels were built from**, so the steps cannot drift off the real pitch.
- **One axis**: `towerZ = âˆ’6.20` is a const shared by the tower, belfry, spire roof and spire, and the
  spire is stacked on the spire roof's own apex (`apexY = eaveY + sprHalfÂ·tan38Â°`, apex top
  `= apexY + 0.45Â·âˆš(1+tanÂ²)`), every stage overlapping the one below by 0.10.
- **Windows**: the side walls are a sill band (1.10â€“1.75), a lintel band (3.45â€“4.30) and seven piers,
  which leaves **six real openings**; the glass sits in the hole with gold sill/head/mullions and a
  projecting stone sill. The front/back walls are solid bands, so their lancets are *applied* proud of
  the face (5 cm proud, 30 cm deep â‡’ 10 cm buried) â€” stated as a rule in the code so it cannot drift.
- **Contacts**: wall corners are closed by overlapping panels + pilasters + returns; the stair treads are
  solid blocks from âˆ’0.05 (not thin slabs on air); the podium was extended to `z Â±7.4` so the rear
  buttresses bear on it; the buttress stages are pulled inboard far enough to stay buried in the stage
  below; the front buttresses bear on the tower apron (widened to 7.8 for them) instead of beside it.
- **Interior**: altar, pulpit and its two steps (the step nearest the pulpit is the taller one, so the
  climb rises toward the platform), 12 pillars, 6 pews and 2 candles are all seated on `floorTop`.

### 1hn-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Reread case by case against the
  ladder and found and fixed four contact defects the first draft introduced: (a) the **front buttresses
  were off the 7.0-wide apron** and floating over the podium; (b) the tower's **applied door, rose window
  and side lancets were centred on the face plane** and therefore buried in the solid tower; (c) the
  **spire's third gold ring** sat 0.9 m above the shaft; (d) the **belfry cornice and the spire panels
  had coplanar edges** (z-fighting) until the cornice was widened to 5.60 against the panels' 5.44.
- **Rejected on re-derivation, deliberately not "fixed":** the old roof panels' apparent 4.43 m
  interpenetration (they met correctly); `Euler(+24)` on the +Z nave panel (a **correct** hip for a +Z
  rise under Unity's X rotation, unlike the pagoda's `Euler(+14)`).
- Brace-balance and duplicate-local scan of the whole file: depth 0, no duplicate declarations at method
  or switch-section scope (the only repeats are `for`-scoped loop variables). All `CreatePartBoxOn` calls
  use the 5-arg float overload; `CreatePartPanelBetween` is called with 7 args in 4 places (2 nave
  panels, 2 ridge caps) and 8 for the spire pyramid's 4 panels.
- Docs: `game-design.md` Â§5.7 (church rebuild + the gable-helper correction); `AGENTS.md` rule 9 already
  covers the convention; `THINKING.md` Â§1hmâ€“1hp (1hn marked resolved, two new findings recorded); this
  file.
- **Save caveat:** parts are stored with absolute world positions, so an **old save** re-loads these parts
  at their pre-rebuild offsets (up to 0.5 m out) and shows a mix of old and new geometry. A fresh test
  platform run is unaffected. Nothing migrates old saves.
- PENDING PLAY-TEST (fresh test platform, `EnableReligion`): walk up the front steps and through the door;
  stand inside the nave and look up at **both** roof ends â€” no daylight gap at `z Â±6`; look at the tower
  from the front and confirm the spire rises out of the spire roof on the same axis (previously it stood
  beside it); check the six side windows show glass, not blank wall; check the four corners of the nave
  at ground level; check the rear buttresses touch the podium; walk the aisle and confirm no floating
  furniture; look into both gable ends and confirm the corbel steps close the triangle.
- **Queued, in order:** ~~1ho shrine rebuild + three Sanqing statues~~ **SHIPPED (see the 1ho entry at the
  top)**; then 1hp, the three faith NPCs sunk 0.915 m into the test platform.
- **STILL OPEN (deferred by the user's choice):** the pagoda's four roofs are authored with an inverted
  pitch (`Euler(+14)` on the +Z panel raises the outer eave) and `Pagoda_Roof1`'s centre cap floats
  0.57â€“1.44 m above its panels. Same fix as 1hn, re-author the panels through `CreatePartPanelBetween`.
  Details in THINKING Â§1hmâ€“1hp.
- **STILL OPEN (separate, earlier):** 1hk needs a post-restart F2 check for `worst dY 0`; the terrain rim
  discrepancy (`worst dY 9.6813`) is unaddressed and is the reserved "candidate 1hl".

## 1hm. Bottom-referenced geometry helpers for the hand-authored structures (task 1 of the church/shrine rebuild)

User request (one of three threads): **redo the taoist temple and the church** â€” the church "has gaps in
structure" and the Taoist temple should have **three statues, not one** (the user remembered the Taoist
trinity; confirmed as the **Sanqing / Three Pure Ones**). The user chose: the Taoist temple is the
**shrine** (not the Buddhist pagoda), **no** measurement lane (the gaps are provable from the authored
coordinates), and the **pagoda's** separate roof defect is **not** fixed in this pass.

Reasoning trail + the full hand-derived defect list: `THINKING.md` Â§1hmâ€“1hp.

**Root cause of the gaps: two authoring conventions, not two bugs.** Every gap reduces to a block placed
by its **centre** Y (so its bottom face must be re-derived by hand and misses its support by a fraction
of a metre) or a roof panel placed by a **tilt sign** (which hides an inverted pitch). Both are invisible
in the source and obvious in the world, and both survive review because the reader has to redo the
arithmetic. Same shape as AGENTS rule 8 one layer up: the copied value is the bottom face.

**Changes â€” `WorldBuilder.Blueprints.cs`, three new helpers next to `CreatePartCube`:**
- `CreatePartBoxOn(root, xz | x, bottomY, z, size, colour)` â€” a block placed by its **bottom** face, so
  a support's top and the block resting on it are written on adjacent lines and the gap between them is
  visible in the source. Two overloads (Vector3 xz, float x/z).
- `CreatePartPanelBetween(root, a, b, halfWidth, thickness, across, colour)` â€” a slab whose **underside
  runs from a to b**; `across` is the horizontal width direction, `thickness` is measured perpendicular to
  the slope, and the rotation comes from `Quaternion.LookRotation(slope, width)`. Roofs, ramps and stairs
  are now stated as their two contact points, so an inverted pitch is **no longer expressible**.
- `CreatePartGableSteps(root, xCentre, thickness, wallTop, eaveY, ridgeY, halfSpan, courses, colour)` â€”
  a corbel-stepped gable closure; every step's top overshoots **6 cm** into the roof underside, because
  a 6 cm intersection is invisible and a 6 cm gap is a slit.
- No call sites yet, so this commit changes nothing in the world â€” it exists so the church and shrine
  rebuilds (1hn/1ho) are reviewable line-by-line.

### 1hm-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). `CreatePartCubeRotated` takes
  `(Transform, Vector3 localPos, Vector3 scale, Color, Quaternion)` and returns `void`, so the panel
  helper passes `rot` straight through; `CreatePartCube` returns `GameObject` and `CreatePartBoxOn`
  forwards it. `Vector3.ProjectOnPlane` + `Quaternion.LookRotation(forward, upwards)` are both
  `UnityEngine` members already in scope (`using UnityEngine;` at the top of the partial). Unity's
  `LookRotation` requires `upwards` not parallel to `forward`; `across` is projected onto the plane
  normal to the slope first, so the two are perpendicular by construction.
- Worked example checked by hand (the rotation identity the helper relies on): for a panel rising toward
  +Z, `LookRotation((0, sinÎ¸, cosÎ¸), (1,0,0))` = `Euler(âˆ’Î¸,0,0)`, and `R_x(âˆ’Î¸)` puts the +Z end **up** â€”
  so a hip roof's +Z panel takes a **negative** authored angle. The pagoda's `Euler(+14)` (`:1144`) is
  therefore inverted (butterfly roof), while the church's `Euler(+24)` and the shrine's `Euler(+14)`
  are correct hips.
- Hand-verified the gable helper against the church numbers it was used with (halfSpan 6.15, rise
  2.811, courses 10 â‡’ band 0.615). **Correction landed in 1hn:** the centred-step formula shipped here
  was wrong â€” a centred step's *inner* edge is `z = 0`, so its top must reach `ridgeY` to cover the
  roof, which makes every step as tall as the peak; the helper now emits uncentred bands mirrored about
  `z = 0`, each reaching the roof underside at its own inner edge. See 1hn.
- Docs: `AGENTS.md` **new rule 9** (state hand-authored geometry by its support; the three helpers; the
  site-origin convention; never rename a structure part type because the string is the save/load key);
  `game-design.md` Â§5.7 (the convention, with the pagoda tilt-sign bug as the worked example);
  `THINKING.md` Â§1hmâ€“1hp (trail, still OPEN for 1hp); this file.
- PENDING PLAY-TEST: **nothing to check yet** â€” no visual change in this commit. The commits that carry
  the play-test list are 1hn (church), 1ho (shrine) and 1hp (test-lane NPC heights).
- **Queued, in order:** ~~1hn church rebuild~~ shipped; ~~1ho shrine rebuild + three Sanqing statues~~ 
  shipped; then 1hp, the three faith NPCs sunk 0.915 m into the test platform.
- **STILL OPEN (deferred by the user's choice, recorded so it is not lost):** the pagoda's four roofs are
  authored with an inverted pitch â€” `Euler(+14)` on the +Z panel raises the outer eave, so all four read
  as butterfly roofs â€” and `Pagoda_Roof1`'s centre cap (`:1163`) floats 0.57â€“1.44 m above its own
  panels. `Roof2/3/4`'s caps plug their valleys, so only Roof1 visibly floats. Same fix as 1hn: re-author
  the panels through `CreatePartPanelBetween` from eave underside to ridge underside. Details in THINKING
  Â§1hmâ€“1hp.
- Task numbering note: the terrain rim follow-up already referred to itself as "candidate 1hl" in the
  1hk entry, so this rebuild is numbered **1hmâ€“1hp** to leave 1hl free for it.

## 1hk. Fix the east-column corner ownership â€” the measured cause of the see-through seams

Follow-up to the 1hj audit. The user pressed F2 inside the loaded ring and sent the readout; **section
A failed** and named the mechanism:

```
A real<->real  pairs 684  nodes 21204  WORST dY 0.5693 m at TChunk(1,-2)-NTChunk(1,-1) node 30
B roots  checked 361  bad 0  OK        C holes  interior 0  frontier 4  OK
D rim  rimChunks 72  cells 76  missing 0  noFarCell 0  compared 758  worst dY 9.6813
VERDICT A: real<->real corner mismatch (worst 0.5693 m at TChunk(1,-2)-NTChunk(1,-1) node 30)
```

684 pairs Ã— 31 nodes = 21204 compared, **zero skipped** â€” the whole loaded ring was measured, so this
is not a localised artefact. B and C are structurally clean (every loaded root draws its full 30 m,
nothing is missing inside the ring), which is why the fix is in the lattice and not in streaming.

**Root cause (one wrong slot in the ownership rule).** `BuildCornerGrid` copies each lattice node out
of the tile that **carries** the node's world corner, and the east column read the wrong corner of the
right tile: node (cs, gz) stands on world corner (Ox+cs, Oz+gz), which is tile (cs-1, gz)'s **SE**
corner, but the code took its **NE** corner â€” one metre further north. So chunk A's east column
carried `h(Ox+cs, Oz+gz+1)` while its east neighbour's west column (`(0, gz)` = tile (0, gz) SW)
carried `h(Ox+cs, Oz+gz)`: same lattice position in space, two different world corners, on **every
east seam of every chunk, in a pristine world** (no dig, no save, no loading order required). The
magnitude is the surface's 1 m height gradient â€” the 0.5693 m measured, decimetres typically, metres
on steep ground. North seams were already exact, which is why the symptom read as a *lattice* bug with
no obvious culprit.

The audit's own location confirms it: the worst node was index 30 of a **north** edge, i.e. the 4-chunk
corner (30,30). Three of that corner's four owners stamp it correctly; the fourth â€” the east column's
`gz == 0` row â€” was reading `h(60, -59)` instead of `h(60, -60)`. The fix therefore shows up on the
north edge of the chunk BELOW the broken column, exactly where the readout pointed.

**Why it was a see-through hole and not just a step.** The low-poly root (default, `LowPolyFacets`) is
built from this lattice and emits **no side walls**, so a sub-metre corner disagreement is an open
slit between two surfaces rather than a covered crack â€” and `BuildBorderCorners`' neighbour data
reaches only the side-wall pass, which the low-poly path drops entirely. Section D's step is the same
defect seen from the far side: the far grid samples `GetHeight(seed, Ox+gx, Oz+gz)` (correct), the
real chunk's east column did not.

**Changes:**
- `ChunkMeshGenerator.BuildCornerGrid` â€” east branch `slot = 1` (NE) â†’ **`slot = 2` (SE)**, the corner
  the node actually stands on. Doc comment updated to state the rule as "reads the corner the node
  stands on" rather than "mirrors the retired WorldCornerIndex".
- `ChunkMeshGenerator.PatchCornerGrid` â€” the identical change, because the patch path mirrors the
  build ownership; leaving it at NE would make a patched chunk drift from a freshly built one by
  exactly the 1 m the build rule was off by.
- Band colours self-correct: they are derived from the owner tile's `slot` (`:939-941`, `:1040-1042`),
  so the east column is now coloured at the corner it renders.
- No save migration: **tile** data was always exact (tiles are cut from the same corner grid), only
  the lattice copy was wrong. Only the derived surfaces change â€” low-poly root, decimated collider
  (`BuildDecimatedCollider`), LOD children, and the rim step.
- `WorldStreamer.SeamAudit` corrections found while reading the readout: section D addressed the real
  lattice with **cell**-local coords (safe only because every gathered cell is span-1 â€” a coarser cell
  would read a shifted node and be skipped as NaN, i.e. report a false "clean") now derived from the
  world node; section D now compares **top-surface vertices only** (`n.y <= 0` = wall geometry, which
  can sit on a node position at a base-level Y), inert while `VoxelTerrainEnabled` is off; the
  VERDICT notes voxel far cells. Read-only throughout â€” no behaviour change.
- Docs: `game-design.md` Â§2.2 (the seam contract now states the real rule â€” the lattice copies the
  owning tile's corner, so correctness rests on the ownership rule, not on re-deriving noise â€” plus a
  note that `ChunkValidator` checks tiles and therefore cannot see a lattice bug); this file;
  `THINKING.md` Â§1hk.

### 1hk-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build): the ownership rule exists at
  exactly two sites (`ChunkMeshGenerator.cs:906-909` build, `:1006-1009` patch) and both now read SE;
  every lattice consumer is **position-based** (`EmitLowPolySurface:783-786`,
  `BuildDecimatedCollider:958-961`, `ChunkObject.BuildLodChild:551-552`, `ResampleLowPolySurface`),
  i.e. places node (gx,gz) at local (gxÂ·step, gzÂ·step), so correcting the stamp corrects all of them
  at once with no consumer change; the owner tiles for boundary nodes are never refined
  (`IsRefinable:215-220` confines refinement to interior tiles 1..cs-2), so slot 2 always exists on
  the 4-vertex block it is read from; no other copy of the rule exists anywhere in `Assets\Scripts`.
- PENDING PLAY-TEST: (1) **restart Play mode** (or walk far away and back) so chunks re-stream and pick
  up the corrected lattice â€” chunks already in memory keep the old stamp until they are rebuilt, since
  nothing invalidates a live lattice on a code change; (2) press **F2** and confirm
  `A real<->real ... worst dY 0` â€” that is the single number that proves the fix, and the north-seam
  pairs must read 0 too; (3) walk to ground level and confirm the see-through slits along eastâ€“west
  chunk seams are gone (this was the reported symptom); (4) the player still stands on the ground and
  no crack/hole appears at a seam from a low angle; (5) dig/cut a corner and confirm the facets +
  collider still track it (the patch path changed too); (6) section D should DROP to 0 or near 0 for
  the east-column part of its 9.68 m â€” see the open item below.
- **STILL OPEN (measured, not this task's cause):** D's `worst dY 9.6813` at the rim is a *real*
  far/real surface disagreement, ~30Ã— too large for a 1 m gradient, and the non-voxel far builder
  emits tops only (no wall vertices to blame). The surviving mechanisms are (i) a far cell built
  before a dig was flushed (staleness â€” the code's own comment calls this expected), or (ii)
  `RelaxLegacySlabTile` (`WorldStreamer.cs:319-342`) relaxing a legacy flat-modified tile's corners
  in memory **only**, which the real lattice inherits while the far grid re-reads the unrelaxed save
  â‡’ dY = 0.5Â·|slab âˆ’ noise|, implying a ~19.4 m legacy slab step at a chunk border. Candidate 1hl,
  after this fix is confirmed.
- **Also open from the 1hj trace** (proven code defects, edit-dependent, NOT measured in this world):
  `ApplyHeightEdits` not writing corner owners at `cx-1`/`cz-1` on the low edge of its own bbox;
  a seam-straddling dig persisting only in the loaded side's save; `CurrentHeightOf`'s tile-centre
  noise fallback (`Deform.cs:566`). Details in THINKING Â§1hj/Â§1hk.

## 1hj. Seam audit â€” measure the "gaps between terrain chunks" report before fixing it

User report: **permanent see-through slits/holes at ground level between terrain chunks** (seen up
close while walking, not only on a horizon). No dig, no far rim, no LOD band was named as the
trigger â€” the symptom is near ground, all the time. The user approved the audit-first plan: measure,
then pick the fix from the readout.

**Why an audit and not a fix.** This codebase's seam design is watertight *by construction* (canonical
noise re-derived per corner, exact block origins, node spacing that divides 30), so the classic
causes are already ruled out by reading the code â€” which is exactly why guessing is dangerous here. A
fix chosen without a number can be correct and still change nothing, leaving the real cause still
unmeasured. Per new AGENTS rule 7, measurement ships as its own task so the readout that justifies the
fix stays in history.

**The four mechanisms measured** (a see-through slit is identical from the player's side in all four):
- **A realâ†”real corner divergence** â€” the 31 shared lattice nodes of every loaded chunk pair compared
  node-by-node. Pristine â‡’ exactly 0. Any max â‡’ one side was re-stamped by an edit its neighbour
  never received.
- **B loaded-but-not-drawing root** â€” root inactive, no mesh on the filter, vertex count â‰  the count
  the step implies, or X/Z bounds that no longer reach the chunk's 0..30 m footprint.
- **C interior hole** â€” a chunk position inside the loaded ring that is uncovered while â‰¥3 of its
  orthogonal neighbours are LOADED. The streaming frontier (0 loaded neighbours) is counted
  separately, and a non-empty ready queue is flagged as a transient caveat, so normal loading never
  reads as a hole.
- **D far/real rim step** â€” every far cell meeting the last real ring (plus the ring past it, which is
  what catches the diagonal corner cells) has its lattice vertices compared against the real chunks
  they meet; outer-ring chunks with no far cell beyond them are listed.

**Changes:**
- `WorldStreamer.SeamAudit()` + helpers (`AuditRealToReal`, `CompareSharedEdge`, `AuditRoots`,
  `AuditInteriorHoles`, `AuditFarRim`) in the NEW `Assets\Scripts\World\Streaming\
  WorldStreamer.SeamAudit.cs` (partial class, + .meta). Returns a 5-line report (header + Aâ€“D +
  VERDICT) where VERDICT names the first section that failed. Strictly read-only â€” no rebuild, no
  patch, no re-stamp, no forced poll.
- `ChunkObject`: four read-only QA accessors â€” `MeshStep`, `HasLattice`, `LatticeY(gx, gz)`
  (chunk-local 0..30, NaN when absent), `RootMesh`.
- Test ground (rule 4): `NewWorldTestGround.EnableSeamAudit` (serialized, default true) +
  `SeamAuditKey` (serialized, **F2** â€” F5â€“F12 are the editor cutscene shortcuts in `GameManager`,
  F8 included, and F1 is a skill hotkey), polled at the top of `Update()` so it works regardless of
  the other lanes' config; the report is cached in `_seamAuditText`, shown on the bench overlay
  (needs `EnableFpsStats`) and logged to the Console. Uses the new Input System
  (`Keyboard.current`, `UnityEngine.InputSystem`) â€” the project has no legacy `Input.*` calls at all.
- Docs: `game-design.md` Â§2.2 now carries the **seam contract** (one owner rule per corner + the
  four-cause table) and points at the audit; `AGENTS.md` rule 7 (measure before fixing a visual
  report); this file; `THINKING.md` Â§1hj (still OPEN â€” no readout yet).

### 1hj-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build): every member the audit
  touches exists with a matching signature (`_loadedChunks`/`_dormantChunks`/`_pendingChunks`/
  `_chunksInFlight`/`_readyChunks`/`_farSectors`/`_focus`, `EffectiveLowPolyStep`, `NearRingRadius`,
  `RenderDistance`, `FarCellForChunk`, `FarCellRings`, `FarSectorStep`, `FloorDiv`, `ChunkData.Size`,
  `FarCell`); `FarCellForChunk` used as-is (no duplicate lookup helper added); far-mesh node step read
  from `FarSectorStep` rather than hard-coded; world offsets use `ChunkData.Size`; the real ring
  extent (0..`keep = NearRingRadius+1`) confirmed from `StreamAround` (`int keep = radius + 1`) so the
  hole scan cannot flag the far shell's own frontier.
- **ROOT CAUSE STILL UNKNOWN** â€” this task ships the measurement only. The fix (1hk) is chosen from
  the user's readout, per the audit-first plan.
- PENDING PLAY-TEST: stand near a reported gap (inside the loaded ring) and press **F2**; screenshot
  the bench overlay. Expected on a healthy world: `A worst dY 0`, `B bad 0`, `C interior 0`,
  `D missing 0 / noFarCell 0 / worst dY 0` and a `VERDICT clean` line. Which of Aâ€“D is non-zero
  names the mechanism for 1hk. (Also confirm F2 does not collide with a gameplay binding; F2 is free
  in the codebase today, and `SeamAuditKey` is repointable in the inspector.)

## 1hi.2. Regression fix: the low-poly root, LOD children and decimated collider were wound BACKWARDS

User: "the chunk around player is upside down, invisible from the top but visible from below, and player
fall right through." Post-1hi.1 the real chunks' root + collider became the lattice surface, and it
rendered only from below + let the player fall â€” the lattice-family mesh winding was upside down.

**Root cause (THINKING 1hi.2 for the trail):** the decimated lattice family (far shell flat AND smooth
grid, `BuildLodChild`, `BuildDecimatedCollider`, and the 1hi.1 root's `EmitLowPolyIndices`) all emit
p00/first-corner-first â€” quad corners SW, SE, NE, NW with triangles (00,10,11)/(00,11,01). The smooth 1 m
tile family emits the opposite ordering â€” NW, NE, SE, SW with (0,1,2)/(0,2,3) (`BuildMeshData`,
ChunkMeshGenerator.cs:152-164) â€” the project's proven up-facing winding (the one-sided `GroundMaterial`
surface the player has always stood on). The far shell masked its own copy of the inversion with the
**double-sided** `FarGroundMaterial` (1ei: "renders the decimated far terrain ... regardless of mesh
winding/culling artifacts that once hid it from the upper face", GameBootstrap.cs:106-112), so real chunks
kept Cull Back and culled the upper face.

**Changes (all three real-chunk lattice emitters now use the smooth-tile up-facing order; normals already
+Y and untouched):**
- `ChunkMeshGenerator.EmitLowPolyIndices` (:846-847) â†’ each lattice quad emitted NW, NE, SE, SW with
  `BuildMeshData`'s exact (0,1,2)/(0,2,3) pattern: `(v+3, v+2, v+1)` / `(v+3, v+1, v+0)`. Fixes the
  upside-down 1hi.1 root render.
- `ChunkMeshGenerator.BuildDecimatedCollider` (:966-967) â†’ `(i01, i11, i10)` / `(i01, i10, i00)`. Fixes
  the fall-through for BOTH the 3 m low-poly collider and the 2 m smooth-mode collider
  (ChunkMeshGenerator.cs:586 â€” never play-tested since 1hi).
- `ChunkObject.BuildLodChild` (:538-543) â†’ `(i01, i11, i10)` / `(i01, i10, i00)`, so smooth-mode
  Lod1/Lod2 (back-facing since 1e6, historically masked by the double-sided far shell) finally face up.
- Far shell left UNTOUCHED (its double-sided material already renders it correctly from above).

### 1hi.2-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build): the three real-chunk lattice
  emitters now match `BuildMeshData`'s up-facing order; every `BuildDecimatedCollider` call site routes
  through the fixed function (ChunkMeshGenerator.cs:586/:731; ChunkObject.cs:108/:149/:430); voxel path
  and far shell untouched; docs updated in the same pass (this file + THINKING + game-design Â§2.5).
- PENDING PLAY-TEST: (1) the near ground renders from above again as crisp 3 m facets (not upside down);
  (2) the player STANDS on the 3 m collider â€” and on smooth mode's 2 m collider (check a near chunk after
  a dig/refit too); (3) smooth mode (`LowPolyFacets` OFF, then let the stream reload): the Lod1/Lod2 band
  ground at 30-270 m now renders ON TOP â€” it may have read invisible/holey before; confirm the midband is
  ground, not a hole; (4) dig / corner-edit still re-stamps facets + collider correctly.

## 1hi.1. Coarse near-ring facets â€” real chunks now render the 3 m lattice surface (follow-up to 1hi "too smooth" report)

User: "the surface is currently too smooth to be called low poly terrain." 1hi only flat-shaded the
existing **1 m** mesh + far shell; 1 m flat quads over a continuous heightfield still read smooth. Real
low-poly = COARSER rendered faces, so this task makes the real chunks' ROOT mesh the lattice-facet
surface itself.

**Design (approved):** facet step **3 m** (matches the far shell exactly â€” the whole world reads ONE
uniform 3 m facet language with the near/far seam sharing exact world corners); Lod1/Lod2 children
**skipped** in low-poly mode (the root already exceeds their density; `ChunkLodManager.ApplyBand`
falls back to `rootMr.enabled = true` when the named detail is missing â€” verified at ChunkLodManager.
cs:259-263).

**Changes:**
- `ChunkMeshGenerator.BuildMergedMeshData(tiles, border, seed, int lowPolyStep = 0)`: step > 0
  short-circuits to `BuildLowPolyMerged` â€” `BuildCornerGrid` (same 31x31 lattice) â†’ flat per-quad
  facet emission (`EmitLowPolySurface`: 4 verts/quad, +Y-dominant cross normal, UV + strata colors
  sampled from the lattice itself, no side walls, bounds) + `EmitLowPolyIndices` (shared winding) +
  `BuildDecimatedCollider(corners, step)` so the collider rides the SAME step. Patch tables are null
  (no per-tile blocks). `ResampleLowPolySurface(md, step)` (returns the updated struct â€” value type)
  re-emits the tiny root from a re-stamped lattice for `PatchRegion`.
- `WorldStreamer.LowPolyStep` (serialized, default 3) + `EffectiveLowPolyStep` (0 = full-res root);
  routed through all three merged-build sites: Streaming.cs:195 (seam-rebuild job â€” new
  `ChunkRebuildJob.LowPolyStep` snapshot, mirrored after the voxel snapshot pattern at line 505-509),
  ChunkBuild.cs:145 (dispatch worker â€” `BuildOrLoadChunk(tc, seed, lowPolyStep)`), Deform.cs:272
  (FullRebuildChunk) + :268. `GenerateChunkSync` passes `EffectiveLowPolyStep`.
- `ChunkObject`: `_meshStep` from `md.LowPolyStep` on apply; `ColliderStep` (root step when coarse,
  else 2 m) drives `RefreshCollider`/`RebuildColliderSurface`/`PatchRegion` collider builds;
  `PatchRegion` low-poly branch â€” skips the per-tile skim, re-stamps the lattice via `PatchCornerGrid`
  as before, then re-samples the whole root (`_merged = ResampleLowPolySurface(...)`); `RefreshLodMeshes`
  early-outs when `_meshStep > 0`.
- Test ground (rule 4): `NewWorldTestGround.LowPolyStep` (serialized, default 3) applied with the
  existing `EnableLowPolyTerrain` block in Awake.
- `MergedChunkMeshData.LowPolyStep` field (0 = full-res 1 m root).

**Trade-offs (accepted):** a 1 m corner edit only VISIBLY moves a facet vertex when the edited corner
lands on the 3 m grid (heights still save/restore the 1 m lattice); collider surface coarsens to match
the render; no side walls on the coarse root (consistent with the far shell's flat bands).

### 1hi.1-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). New symbols:
  `LowPolyStep` (WorldStreamer + test ground), `EffectiveLowPolyStep` (private),
  `MergedChunkMeshData.LowPolyStep`, `BuildLowPolyMerged`/`EmitLowPolySurface`/`EmitLowPolyIndices`/
  `ResampleLowPolySurface` (ChunkMeshGenerator), `ChunkRebuildJob.LowPolyStep`,
  `ChunkObject._meshStep`/`ColliderStep`. Grep confirms: all 3 `BuildMergedMeshData` call sites + both
  `BuildOrLoadChunk` call sites thread the step; `ColliderDecimation` survives only as the smooth-mode
  fallback inside `ColliderStep`; `_merged.Vertices` readers in PatchRegion are low-poly-safe (null
  table â†’ `IsTileRefined` false â†’ no refinedness mismatch at Deform.cs:525-533); `IsTileRefined`
  null-guards (ChunkObject.cs:643-646); `UploadMerged` clears on vertex-count change so the pooled
  mesh handles root-mode flips (ChunkMeshGenerator.cs:1230); `ChunkLodManager` band sweep tolerates
  missing Lod children (ApplyBand fallback). Far shell untouched; voxel path untouched (never calls
  the merged builder with a step; `_meshStep` stays 0 there).
- PENDING PLAY-TEST: (1) the near ground reads as crisp 3 m facets everywhere (not smooth), matching
  the horizon; (2) walk over the near/far seam (ring 9-10) â€” the facet language and sizes match, no
  seam/hole; (3) dig / corner-edit â€” the facet under the edit moves when the edited corner lands on the
  3 m grid and you STAND on the new surface (collider step = 3); (4) walk a chunk border â€” watertight,
  no side-wall gaps at cliffs between low-poly chunks (expected look: slanted facets, no vertical
  walls); (5) flip `EnableLowPolyTerrain` OFF and rebuild â€” the full 1 m surface + 2 m collider
  returns; (6) voxel mode unchanged.
- Docs synced same pass: this PROGRESS 1hi.1, THINKING 1hi.1, game-design Â§2.5 (coarse near-ring
  facets + decimated-collider step note) + Â§2.10 (coarse root facets).

## 1hi. Low-poly facet terrain (look) + decimated 2 m colliders (perf) â€” far-shell flat facets, refine knockout, cheap PhysX cooks

Requested: "make the terrain more low poly vibe" â€” asks whether that also wins performance. Honest
answer first: GPU rasterization is already trivial (~150k tris total), so low-poly does NOT cut draw
calls; the REAL per-triangle costs are the synchronous **collider cooks** (full merged mesh, 2/poll
cap), the **finalize / LOD-sweep mesh uploads**, and background generation. So the task shipped BOTH:
a low-poly LOOK (A) and a real perf win (B), as one opt-in QA lane with the look defaulting ON.

**A â€” Low-poly look (`WorldStreamer.LowPolyFacets`, default true):**
- Far shell (`WorldStreamer.FarShell.cs BuildFarSector(cell, seed, maxRing, bool flatFacets)`) emits
  **flat per-quad normals** instead of the smooth central-difference haze: 4 corner vertices per 3 m
  quad sharing one +Y-dominant flat normal (cross of the +X/+Z edges, flipped up, degenerate-safe).
  Triangles unchanged; vertices 4x BUT a far cell uploads once per cell lifetime (finalize), never per
  frame â€” the budgeted per-frame render stages are untouched. Seam-proof by construction: boundary
  quads are built from the SAME world corners on both sides of a shared edge (SampleHeight/WorldHeight
  resolve exact world corners), so adjacent cells compute byte-identical boundary quads â€” simpler than
  the 1ek cross-cell pull (which stays in the smooth path, now the `else` branch).
- Near band: `EffectiveRefineThreshold = LowPolyFacets ? 0 : RefineThreshold` (new private property)
  routes through ALL four build paths (Streaming.cs:172 `job.Refine`, ChunkBuild.cs:137,
  Deform.cs:255, Deform.cs:513) so the 1ew adaptive stretch-split never fires â€” steep near slopes
  keep ONE flat 1 m quad per tile, matching the far facets. `BuildMeshData`(threshold 0) is the
  documented disable mode (4-vert quads, per-tile flat normals) â€” verified clean, no degenerate path.

**B â€” Decimated 2 m colliders (`ChunkMeshGenerator.BuildDecimatedCollider`):**
- Physical surface = every 2nd node of the SAME 31x31 world-corner grid the LOD children use â†’
  256 verts / 450 tris vs. up to ~1800+ tris of the full merged render mesh â†’ the per-enable PhysX
  cook on the gameplay frame (capped 2/poll) drops ~4x. Seam-proof across chunks by construction
  (shared lattice), and tracks deformation: `PatchRegion` re-derives it from the patch-re-stamped
  lattice.
- Thread-safety: collider arrays (`MergedChunkMeshData.ColliderVertices/Triangles`) are built on the
  worker thread inside `BuildMergedMeshData` right after `BuildCornerGrid`; `ChunkObject` uploads them
  into a SECOND pooled `Mesh _colliderMesh` (same acquire/release discipline as the render mesh,
  release in `Release()`; lazily allocated â€” collider-less chunks own nothing).
- `ChunkObject` routing: `SetColliderActive`/`ApplyMerged`/`PatchRegion` all go through new
  `RefreshCollider(bool active, MergedChunkMeshData md = default)` + `UploadCollider` +
  `RebuildColliderSurface`; apply path always re-uploads when fresh arrays are present (a rebuild on
  an already-collider-enabled chunk must not keep a stale cooked surface); voxel mode unchanged (its
  chunky 1 m render columns stay the collider, guarded by `VoxelMesh`).

Test ground (rule 4): `NewWorldTestGround.EnableLowPolyTerrain` (default **on**) sets the streamer
knob in Awake before the first stream poll, mirroring the `EnableVoxelTerrain` wiring â€” off restores
the old smooth haze.

### 1hi-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). New symbols:
  `LowPolyFacets` (WorldStreamer), `EffectiveRefineThreshold` (private), `BuildFarSector(cell, seed,
  maxRing, flatFacets)` (only call site updated â€” FarShell.cs:714, matches the VoxelTerrainEnabled
  read-per-cell pattern), `ChunkMeshGenerator.BuildDecimatedCollider` (public),
  `ChunkColliderDecimation` (public const 2), `ColliderVertices/ColliderTriangles` (merged data,
  only set by the smooth builder â€” FarShell's coarse cells & VoxelMesher never set them, so nothing
  else reads these), `ChunkObject.RefreshCollider/UploadCollider/RebuildColliderSurface/_colliderMesh/
  ColliderDecimation`. Grep confirms: `RefineThreshold` now only remains as the serialized field +
  the property; all 4 build-site consumers route through `EffectiveRefineThreshold`; every
  `_mc.sharedMesh` write site in ChunkObject is inside the three methods + Release; far-cell producers
  never touch collider arrays. The `Streaming.cs:172 job.Refine` executes on the dispatch (main) thread.
- PENDING PLAY-TEST: (1) run a long sprint at max speed â€” the look reads as crisp flat facets on the
  horizon AND on steep near slopes, with no frame hitch from the collider ring (the ~4x cook cut should
  be visible in the 1gf per-stage poll split, `colliders` ms); (2) walk across chunk boundaries and dig/
  corner-edit right at a seam â€” the ground surface stays watertight and you stand on it everywhere
  (collider tracks the excavation); (3) flip `EnableLowPolyTerrain` OFF and re-run â€” the old smooth
  central-difference haze returns on a fresh far shell (A/B check); (4) voxel mode (`EnableVoxelTerrain`)
  still stands/collides identically (render-mesh collider guard); (5) the far-shell flat bands show no
  lighting seams along cell edges or at the rim/real junction.
- Docs synced same pass: PROGRESS 1hi, THINKING 1hi, game-design Â§2.5 (low-poly look + decimated
  collider bullets) + Â§2.10 (knockout note).

## 1gh. Report: "chunk -8_3 is invisible for no reason" â€” LOD cull hid retained terrain + chunk diagnostics (fixed)

Report: the chunk `-8_3` (world x â‰ˆ -240..-210, z â‰ˆ 90..120, Chebyshev ring 8 from the platform) is
invisible "for no reason". User confirmed the GameObject `TerrainChunk_-8_3` EXISTS in the hierarchy
but the ground isn't shown â€” so this is a render-state bug, not a missing chunk.

Root cause traced to `ChunkLodManager`:

- `EffectiveCullDistance` = `max(CullDistance, (RenderDistance.Radius + 1) * 30)`: with a small
  render-distance asset the render term alone dips BELOW the real-chunk streaming extent
  (`NearRingRadius` 9 + hysteresis keep ring 10). E.g. radius 7 â†’ cull 240 m, while `-8_3` sits at
  ring 8 (~250 m from the platform) and IS loaded/retained by the streamer.
- The LOD sweep then `SetActive(false)`s the whole root over the cullâ€¦
- â€¦but **far cells only exist BEYOND the near ring** (`FarCellForChunk` ring â‰¥ near+1), so nothing
  covers that cell â€” an invisible hole. `SetActive(false)` also silently removes the chunk's collider,
  matching the earlier fall-through at the same frontier (1gg).

Fix â€” the invariant (ChunkLodManager.cs `EffectiveCullDistance`, 1gh):

- The cull now floors at the ENTIRE retained-chunk extent:
  `max(CullDistance, (Radius+1)*30, (NearRingRadius + 1 + DormantRingDepth)*30)`. The sole legitimate
  cover for a real chunk is its far cell, which only exists beyond the near ring â€” so LOD can never
  hide ground the streamer is the only owner of. With a full render-distance asset the value is
  unchanged (930 m+); with a small one the frontier hole (and its collider) is repaired. Dormant
  chunks are skipped by the sweep regardless, and beyond near+dormant the far cell is the cover.

QA diagnostics (rule 4 â€” opt-in readout):

- `NewWorldTestGround.EnableChunkDiagnostics` (default **on**) + `ChunkInspectX/Z` (default âˆ’8/3)
  append one line to the FPS overlay: `WorldStreamer.ChunkDiagnostics(tc)` â€” real loaded/dormant/
  absent, collider, dormant flag, voxel flag, and the owning far cell (span + live/MISSING) â€” plus
  the root's `activeSelf` / renderer+mesh presence / `Lod1`+`Lod2` on-off / LOD band
  (`ChunkLodManager.BandIndexOf`, new) / collider. One screenshot resolves any future
  "chunk invisible" report (missing vs LOD-hidden vs far-cell-dead) without a guessing session.

### 1gh-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). New symbols:
  `ChunkDiagnostics` (WorldStreamer public), `BandIndexOf` (ChunkLodManager public),
  `EnableChunkDiagnostics` + `ChunkInspectX/Z` (test ground) â€” all fresh and collision-free;
  `FarCellForChunk` call signature matches (`x, z, centre, near, keep`); `LoadedChunks`/
  `DormantChunks`/`DormantChunkCount`/'FarSectorCount'/`RebuildPendingCount` are already public;
  `ChunkObject.HasCollider/Dormant/VoxelMesh` confirmed; global namespace throughout (no using
  needed). Docs synced same pass: PROGRESS 1gh, THINKING 1gh, game-design Â§2.2 cull invariant +
  Â§2.7 diagnostics.
- PENDING PLAY-TEST: (1) look/show the west edge at ring 8-9 with a SMALL render distance â€” chunk
  -8_3's ground returns (and its collider, no fall-through there); (2) with a full render distance the
  fix must be a no-op â€” no visual change; (3) if any chunk STILL looks missing, walk near it and send
  the new `chunk -8_3:` HUD line (says loaded vs dormant vs absent, root active/renderer/mesh/lod
  band/collider, far live/MISSING) â€” that pinpoints it.

## 1gg. Player falls through the ground â€” collider "not loading" (fixed: player-floor guarantee + void-fall rescue)

Report: while sprinting, the player can fall right through the ground â€” the chunk under them is
rendered but has **no MeshCollider**. Root cause traced to the collider-on-demand queue
(WorldStreamer.cs `ReconcileCollidersIfChanged`, 1dg/1dq/1es/1ge):

- Every streaming chunk is created **collider-less** (`CreateChunkGameObject` buildCollider=false);
  `ReconcileCollidersIfChanged` is the only place that later assigns the MeshCollider, once the chunk
  enters the `ColliderRingRadius` (7) ring or sits under an active spell request.
- Since the shared stream budget (`_streamCapped`) started gating the walk (1es, tightened 1ge), every
  enable was deferred whenever earlier stages (finalize/far/props) had drained the pool â€” which is
  **almost every poll during a heavy sprint**. The ring-fill also capped at `MaxColliderCooksPerPoll`
  = 2, and the walk ran in dictionary order with **no spatial priority**.
- Net effect: a chunk entering ring 7 could have its first cook deferred for the whole duration of a
  budget-starved crossing; when the player then stepped onto it (it was the centre chunk by then, or a
  freshly-finalized-at-feet chunk), it still had no collider â†’ the CharacterController passed through
  the visible mesh and the player fell. Far cells have no collider and the PlayerController fail-net
  (150 m blast / NaN) can't catch a *gradual* fall, so nothing stopped the descent.

Fix (two layers):

- **A. Player-floor collider guarantee (WorldStreamer.cs):** the collider walk now treats the focus
  cell + its 2-chunk Chebyshev floor as **COLLISION-CRITICAL** â€” those enables are budget-EXEMPT
  (bounded at 6/poll, charged to the budget afterwards for honest accounting) and cook unconditionally
  even while `_streamCapped`. Additionally, whenever any enable deferred, the walk cooks the single
  **closest** pending chunk at the end â€” so the fill front advances â‰¥1 cell per poll and the centre
  cell (cheb 0) can never stay collider-less across polls. Everything beyond the floor keeps the
  existing budgeted fill; disables stay instant. Result: the ground under / just ahead of a sprinting
  player can never lack a collider for more than one poll, no matter how loaded the stream is.
- **B. Void-fall rescue (PlayerController.cs):** `EnforcePhysicsSanity` now also reverts the player to
  `_lastSafePosition` when Y drops below `VoidFallFloor` = **-300 m** â€” beneath the Â±200 m sanitized
  terrain band (noise Â±82.6 m + deform headroom), so only a true missing-collider void reaches it.
  Backstop for any residual hole (e.g. a genuinely missing chunk) instead of an infinite fall; doesn't
  disturb legitimate platform falls onto the terrain below.

### 1gg-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). New symbols collision-free:
  `MustCollideRadius`, `MustCollideCap`, `closestPending`, `closestCheb`, `deferredAny`,
  `VoidFallFloor` all fresh in their scopes; `ReconcileCollidersIfChanged` is the only cook-gating
  walk (single call site in StreamOnce); `SpendStreamBudget` reuse is unchanged; the PlayerController
  const+check sit inside `EnforcePhysicsSanity` (runs every Update pre-input) with no false-positive
  path for legit terrain (floor -300 < sanitized -200). Docs synced same pass: PROGRESS 1gg, THINKING
  1gg, game-design Â§2.5 collider-on-demand + Â§2.8 physics rails.
- PENDING PLAY-TEST: (1) hard sprint through a heavy stream + the dormant band â€” no fall-through, no
  new per-poll hitch at the collider front; (2) sprint then turn around immediately and run back â€” a
  trailing-edge chunk that lost its collider re-enables by the time you reach it; (3) walk onto a
  region while the stream is visibly catching up â€” ground you see is ground you stand on; (4) if a
  fall-through still happens, expect the console `Fell below the world floor` warning and report the
  coordinates â€” that helps find the residual hole.

## 1gf. Per-stage poll ms instrument â€” the "still the lag" A/B baseline

After 1gd (decoupled renderer clock + async seam rebuilds) and 1ge (demote gate + heavy-only
cool-down + budgeted collider cooks + monotonic stamps), play-test still reported the sprint/fast
crossing lag ("still the lag"). Next step in the trail: STOP guessing which stage eats the frame and
MEASURE it. This task adds a QA-only read-only instrument (rule 4 â€” the bench overlay; no legacy
world changes, and NO behavior change: every stage's timing is pure wall-clock capture around the
existing calls).

- **Per-poll stage split (WorldStreamer.cs):** new nested `public readonly struct PollStageStats`
  (allocation-free value snapshot) with one float per stage â€” StreamAround, Dispatch (+ rebuild
  dispatch), Finalize (chunk GameObjects + mesh uploads), RebuildDrain (async seam-rebuild apply),
  FarScan (far shadow-sync / removal-scan / ring-walk / pre-warm / dispatch), FarFinalize (far sector
  creation + uploads), Colliders (ring walk + PhysX cooks), PropSync, Props. `StreamOnce` wraps every
  stage call with `Time.realtimeSinceStartup` boundaries, records `LastPollStats`, and folds each
  stage into a rolling `PeakPollStats` (per-stage max since last read) + `HeavyPollsSinceLastRead`.
  `ResetPollStagePeaks()` clears the window (the HUD calls it each refresh so each visible window
  covers just the current run).
- **Far pass split (WorldStreamer.FarShell.cs):** `FarShellTick` now takes `out float scanMs, out
  float finalMs` (single call site, updated) â€” the scan/management half (up to the dispatch charge)
  vs the finalize half are reported separately, so the HUD can distinguish "always-on full-width far
  scan overhead" from "per-crossing cell creation cost".
- **LOD sweep readout (ChunkLodManager.cs):** the band sweep still runs in `Update()` on the
  GAMEPLAY frame (not the decoupled coroutine) and rebuilds decimated LOD meshes synchronously on
  band change. Exposes `LastSweepMs`/`PeakSweepMs` (rolling peak per manager) so a sprint's LOD
  rebuild burst is visible on the HUD.
- **Bench overlay (NewWorldTestGround.cs):** `EnablePollStageStats` (default **on**, needs
  `EnableFpsStats`) appends the last-poll split, the worst-poll peaks + heavy-poll count, and the LOD
  sweep peak to the 4 Hz FPS readout, then resets the peak window.

### 1gf-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Grepped every new symbol
  (`PollStageStats`/`Zero`/`Max`, `LastPollStats`, `PeakPollStats`, `HeavyPollsSinceLastRead`,
  `ResetPollStagePeaks`, the `FarShellTick` out params, `LastSweepMs`/`PeakSweepMs`,
  `EnablePollStageStats`): the ONLY `FarShellTick` call site is `StreamOnce` (updated to the new
  signature; no stale 2-arg call remains); the ChunkLodManager `sweepStart` is frame-local (no clash
  with the StreamAround deep-unload `sweepStart` in Streaming.cs); the struct is declared inside the
  `WorldStreamer` class so the fields referencing it compile regardless of member order.
- PENDING PLAY-TEST: with `EnableFpsStats` + `EnablePollStageStats` on, long-sprint across the
  dormant band and through edited terrain; read which line peaks under a hitch â€” expected candidates
  `farScan` (full-width far scan overhead every poll), `final`/`farFinal` (per-crossing creation) or
  `lod sweep` (gameplay-frame LOD rebuilds). Bring the numbers back; 1gg then targets the winner.

## 1ge. 1gd play-test follow-up â€” demote-cover gate, heavy-poll cool-down, budgeted collider cooks, monotonic rebuild stamps

Play-test after 1gd (user reported): the speed lag PERSISTED ("still the lag") and a NEW regression
appeared â€” "a group of chunks goes invisible for no reason". Session Q&A confirmed: running SMOOTH
terrain (so the 1gd async seam path IS live), the loss reads as a whole GROUP of chunks going
invisible, HUD was not watched, and `EnableSpeedDecoupleRender` had not been A/B'd.

- **Root cause (mesh loss, confirmed by code):** `DemoteChunk` hides a real chunk the moment it passes
  `keep` with NO cover check (StreamAround â†’ DemoteChunk, Streaming.cs), while the far shell's OWN
  tenant demotes ARE guarded (`FarCoverageReady`, FarShell.cs). The demote removes the chunk from
  `_loadedChunks`; `FarShellTick`'s active-shadow sync then activates the span-1 cell under it the same
  poll â€” but ONLY if that cell already exists in `_farSectors`. When the async far build for the
  position hasn't landed yet (a ~50-400 ms window, widened by the 1gd cadence cut), the hidden chunk
  has no cover at all â†’ a whole trailing group reads as invisible until the far finalize catches up.
- **Fix A â€” demote gate (1xd):** the StreamAround demote pass resolves each chunk's required far cell
  via `FarCellForDemote()` (same near/keep bounds `FarShellTick` uses) and demotes only once the cell
  is already live in `_farSectors` (inactive is fine â€” the sync flips it in the same poll). A skipped
  chunk stays visible and the new `_demoteBacklog` flag (joins the idle gate in WorldStreamer.cs) keeps
  the loop polling until the cover lands. Null cover = the position is outside the far annulus (small
  render distance) â†’ demote proceeds under legacy behavior â€” no deadlock.
- **Fix B â€” restore loading cadence (1xd):** `StreamOnce()` now returns HEAVY (`_streamCapped`, the
  shared stream budget was exhausted) instead of "did any work"; `StreamLoop` cool-downs only after a
  heavy poll. Light-busy polls (dispatch-only, small finalizes, gate re-checks) keep the full
  `StreamHz` beat â€” undoing the ~15-30% throughput cut that let the player outrun the far fill.
- **Fix C â€” budget the collider cooks (1xd):** `ReconcileCollidersIfChanged` defers a cook
  (`_collidersDirty = true`) when `_streamCapped`, instead of cooking up to `MaxColliderCooksPerPoll`
  (2) unbudgeted on top of a heavy poll â€” PhysX cooks can no longer stack ~2-6 ms onto arbitrary
  gameplay frames at speed.
- **Fix D â€” rebuild drain hardening (1xd):** `ChunkObject.MeshRebuildStamp` now takes values from a
  GLOBAL monotonic counter (`NextMeshRebuildStamp`) instead of a per-object `++` (per-object restarted
  at 0 on every chunk lifecycle, so a reload could re-issue a stamp a stale in-flight result already
  held). `DrainRebuildResults` additionally rejects results whose chunk is `Dormant`.
- **Docs (same pass):** game-design Â§2.5 (speed-decoupled bullet updated to heavy-only + new demote
  gate / collider budget / monotonic stamp bullets); THINKING Â§1ge; this entry. Tooltip on
  `DecoupleRenderFromGameplay` corrected (heavy-only) in the same pass.

### 1ge-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Grepped every new/changed
  symbol (`_demoteBacklog`, `FarCellForDemote`, `NextMeshRebuildStamp`/`_meshRebuildStampCounter`, the
  `StreamOnce` heavy-return + `StreamLoop` cadence, the collider `_streamCapped` gate, the
  `DrainRebuildResults` `obj.Dormant` guard): all call sites + signatures confirmed; no
  `MeshRebuildStamp++` remains. Extent check: far ring walk is `near+1 .. view+FarOuterKeep` (no static
  coverage gap over the dormant band at default) â€” the invisible group is a build-lag hole, closed by
  the demote gate.
- PENDING PLAY-TEST: sprint across the dormant band with AND without `EnableSpeedDecoupleRender`;
  confirm no group of chunks goes invisible either way; watch the FPS HUD at the moment a group used to
  vanish (is the span-1 cell under a hidden chunk live? do `far cells` stay ahead of `dormant`?); confirm
  a wide crossing no longer spikes a gameplay frame (collider cooks deferred on heavy polls).

## 1gd. "Immense lag at higher player speed" â€” speed-decoupled renderer clock + async edited-terrain seam rebuilds

User reports: the game is smooth while walking but lags immensely when you move faster (player speed).
Clarified the target is the WorldStreamer 3D-terrain renderer, NOT the UI minimap (no RenderTexture /
second camera exists; `CompassMinimapHUD` + `WorldMapUI` are UGUI, cheap). User directive: do NOT limit
the loading speed â€” decouple rendering from gameplay another way.

- **Root cause (confirmed):** every streaming/render poll ran inside the gameplay `Update()` at 20 Hz.
  Walking into edited terrain ran **synchronous** 900-tile rebuilt merged chunks on the main thread â€”
  `ReconcileNewlyLoadedChunk` (WorldStreamer.Mesh.cs:141) called `FullRebuildChunk` (= re-emit 900
  tiles + `BuildMergedMeshData` + full mesh/collider upload, Deform.cs:231) for every modified chunk
  entering the ring, and `ReconcileModifiedBorders` (Deform.cs:347) chained up to four MORE per
  reconcile. At speed, each poll crossing new chunks triggered these bursts; stacked multi-ms the
  gameplay frame dragged. Budgets (1es, 4 ms) did not cover them because they were uncounted
  synchronous rebuilds, not finalize-stage work.
- **Fix A â€” renderer on its own clock:** `Update()` â†’ `StartCoroutine(StreamLoop())` in `OnEnable`.
  The loop ticks on `WaitForSecondsRealtime(1/StreamHz)` (`StreamHz` default 20) and checks
  `StreamInUpdate` itself (runtime toggle still works). After any BUSY poll it `yield return null`
  (cool-down frame) when `DecoupleRenderFromGameplay` is on, so a heavy map-render beat can never
  stack onto the next gameplay frame. Poll body moved to `StreamOnce()`; the `_timer`/`PollInterval`
  accumulator was removed.
- **Fix B â€” edited-terrain seam rebuilds are async:** new pipeline in WorldStreamer.Streaming.cs:
  `_rebuildPending` (HashSet) â†’ `DispatchRebuilds` (main-thread snapshot of raw `ChunkData` refs +
  real border corners via `BuildBorderCorners`, then `ThreadPool.QueueUserWorkItem`) â†’
  `BackgroundChunkRebuild` (worker re-emits `BuildMeshData` + `BuildMergedMeshData`) â†’
  `_readyRebuilds` (ConcurrentQueue) â†’ `DrainRebuildResults` under the shared `SpendStreamBudget`
  accounting (caps `MaxRebuildInFlight` 8, `MaxRebuildFinalizePerPoll` 3). `ReconcileNewlyLoadedChunk`
  + `ReconcileModifiedBorders` now call `RequestChunkRebuild(tc)` instead of `FullRebuildChunk`.
  Stale-result guard: `ChunkObject.MeshRebuildStamp` bumps in `ApplyMerged` + `PatchRegion`; a result
  applies only if its snapshot stamp still matches (a chunk that unloaded+reloaded, or that got
  edited meanwhile, discards the old result). Dedupe against pending/in-flight. Voxel-mode chunks
  keep the synchronous path (`FullRebuildChunk`) â€” experimental model unchanged. Thread discipline
  unchanged: the worker never touches `_loadedData`/`_loadedChunks`/ChunkObject members.
- **Fix C â€” hard slices:** deep-unload sweep gets a wall-clock interrupter (`DeepUnloadSliceMs` 1.2 ms,
  checked per iteration) â€” a single `UnloadChunk` can run past 1 ms and six stacked; the backlog flag
  already carried the drain over polls (`MaxChunkUnloadsPerPoll` 6 kept).
- **Fix D â€” collider reconcile:** already tracks the player's exact chunk (centre == player chunk, no
  anchor lag) â€” satisfied by design; no code change.
- **Fix E â€” allocations:** pooled per-poll temp lists (`_tempWake`/`_tempDemote`/`_tempUnload`/
  `_tempRebuildScan` in Streaming.cs; `_tempFarStale`/`_tempFarHandoffs` in FarShell.cs) replace the
  lazy `new List<>()` in the wake/demote/unload scans and the far removal scan.
- **QA (rule 4, test platform only):** `NewWorldTestGround.EnableSpeedDecoupleRender` (default OFF) +
  `RunSafely("speed-decoupled renderer", ApplySpeedDecoupleSettings)` lane â€” config-only, forces
  `StreamInUpdate` + `DecoupleRenderFromGameplay` on any WorldStreamer found and logs the settings.
  The FPS readout (`EnableFpsStats`, default ON) now also prints the rebuild back-queue
  (`RebuildPendingCount`) and its backdrop grew to fit the 4th line.
- **Docs (same pass):** game-design Â§2.5 (new speed-decoupled-clock + async-seam bullets; deep-unload
  slice note; 1ee "Update" phrasing â†’ decoupled clock; temp-list pooling note); THINKING Â§1gd.

### 1gd-status
- IMPLEMENTED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Grepped all new symbols
  (`RequestChunkRebuild`, `DispatchRebuilds`, `DrainRebuildResults`, `RebuildPendingCount`,
  `DecoupleRenderFromGameplay`, `StreamHz`, `MeshRebuildStamp`, pooled `_temp*` fields): every call
  site + signature confirmed (WorldStreamer.cs StreamOnce/StreamLoop, Streaming.cs pipeline,
  Mesh.cs/Deform.cs reconcile, ChunkObject.ApplyMerged/PatchRegion, NewWorldTestGround QA + FPS
  readout). No `Update()`/`_timer` references remain in the streamer partials; `FullRebuildChunk`
  still exists (edit paths + voxel + thread-pool-refused fallback).
- Commit: this one.
- Pending play-test: (a) toggle `EnableSpeedDecoupleRender` on + FPS stats on, sprint near/far on the
  test ground (and ideally a fast vehicle across real edited terrain) â€” gameplay frames must stay
  smooth where they used to hitch, while the `rebuilds` counter pulses in the readout instead of
  frame spikes; (b) repeat WITHOUT the toggle to confirm the old hitching path returns (A/B); (c) dig/
  edit then walk across the seam â€” slab walls still seal (no phantom walls / gaps), rebuild results
  still applied; (d) quick out-and-back through the dormant band rotates chunks without a burst (the
  1.2 ms slice); (e) voxel mode still rebuilds synchronously and looks unchanged; (f) `StreamInUpdate`
  off â†’ no streaming at all (master switch intact).

## 1gc. Chunks "disappear then generated right back" at the close-range edge â€” dormant keep-ring stops the destroy/regenerate churn

User report: while moving, at the edge of the close range the chunks would disappear then be
generated right back; asked for "can't it just remove or add the mesh directly".

- **Root cause:** real chunks stream only to ring `NearRingRadius` 9 and stay loaded through the
  +1 hysteresis ring (`keep = near+1` = ring 10); the instant a chunk passes ring 10 the
  `StreamAround` sweep called `UnloadChunk` â†’ `obj.Release()` (props destroyed, pooled mesh
  returned, LOD children destroyed) + `Destroy(gameObject)` + 900-tile bookkeeping dropped
  (WorldStreamer.Streaming.cs:228). Walking back across the boundary re-dispatched the coord â†’
  background regenerate â†’ new GameObject + mesh upload. Sequential destroy + full regenerate on
  every boundary crossing â‰¡ "disappear then generated right back". The collider ring (7, 1dq) only
  toggles `MeshCollider.sharedMesh` on the same object (no visual change, no destroy) â€” a separate
  ring, NOT the source. Asked-in-plan: user chose the **dormant keep-ring** variant (hide the chunk,
  never destroy it) over a near-ring bump or a full-render-collider-only option.
- **Fix:** dormant keep-ring (`DormantRingDepth`, default 2 â†’ rings 11-12 â‰ˆ 270-390 m).
  `StreamAround`'s passes are now wake â†’ demote â†’ deep-unload:
  - **Demote** â€” chunk passing `keep` moves `_loadedChunks â†’ _dormantChunks`, visuals off
    (`ChunkObject.SetVisualActive(false)`), props `ReleaseProps()`, collider off, `Dormant` flag set.
    Tile data / pooled mesh / GameObject / VoxelStore retained. Leaving `_loadedChunks` makes every
    "does the real chunk cover this?" check (far active-shadow, prop ring, collider reconcile, LOD
    registration) treat it as absent â†’ the coarse far cell covers it the SAME poll, zero far-shell
    code change.
  - **Wake** â€” dormant chunk re-entering ring â‰¤ `keep` is re-added to `_loadedChunks` + visuals on,
    in place; collider/props/LOD return through their own ring passes the same poll. Never re-dispatched.
  - **Deep-unload** â€” only a dormant chunk past `keep + DormantRingDepth` is truly `UnloadChunk`ed,
    via the existing 1es capped sweep (6/poll + `_chunkUnloadBacklog`). `UnloadChunk` now also reads
    the dormant set; `ResetTerrainSaves` unions both sets.
- **LOD gap guard (caught in implementation):** `NewWorldSystems`' LOD registration delta-diff runs
  on a timer (`RegSyncInterval`), so between demote and unregister the `ChunkLodManager` band sweep
  could re-enable a dormant chunk's visuals and leave them visible over the far cell (the z-fight
  the shadow rule prevents). Guarded with `ChunkObject.Dormant` + a one-bool skip in the sweep.
- **Docs (same pass):** game-design Â§2.5 (new dormant keep-ring bullet; capped-sweep + active-shadow
  notes updated); THINKING Â§1gc; the QA FPS HUD (`NewWorldTestGround.EnableFpsStats`) now also shows
  a `dormant` chunk count.

### 1gc-status
- FIXED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Re-grepped every `_loadedChunks`
  consumer (Voxel :83/170/245/265, Props :52/77, Mesh :54, FarShell :389/438/1242, Deform
  :233/358/448, Reconcile :316, NewWorldSystems :116, QA HUD :1105): dormant chunks are excluded by
  construction and each site reads "absent" â€” the intended behavior, with no far-shell / prop /
  collider edits needed. `_dormantChunks` / `Dormant` / `SetVisualActive` / `DemoteChunk` /
  `WakeChunk` / `DormantRingDepth` / `DormantChunkCount` are fresh symbols (no collisions in
  Assets\Scripts).
- Commit: this one.
- Pending play-test: (a) walk straight out past ~270 m to ~390 m and back â€” the close-range-edge
  chunks must no longer disappear-then-regenerate; the swap to the coarse far cell is smooth and the
  return is instant; (b) zig-zag across the boundary (fast facing) â€” no blank frames, no
  double-draw/z-fight flicker; (c) stand still at the boundary a few seconds â€” nothing changes (idle
  gate holds); (d) QA FPS HUD `dormant` climbs while you leave and falls on return, bounded well
  under ~184 at depth 2; (e) a one-way sprint past ~390 m deep-unloads without a hitch (capped
  6/poll); (f) F5 reset-terrain / F12 flow with dormant chunks in memory still works.

## 1gb. Magic projectile impact "3 objects floating up then disappear" â€” replaced with an exploding, fading sphere

User report: "currently when magic projectile hit something it will generate an effect of 3 object
floating up then disappear, change that effect into an exploding sphere then disappear, as it
explode the transparency will increase".

- **Root cause:** the "3 objects floating up" was NOT the `spell.ImpactEffectPrefab` impact-prefab
  path (SpellEffect.cs:280 â€” dead: grep finds no assignment of `ImpactEffectPrefab` anywhere, and
  the project has zero `.prefab` files). It was `WorldStreamer.SpawnCraterDebris` (Deform.cs:584),
  fired from every Crater excavation at `DeformAt` â€” which every magic projectile triggers by
  carving its impact dent. It spawns `Random.Range(3, 6)` up-biased rigidbody cubes ("floating up")
  destroyed after ~2.5 s ("then disappear"). Tool digs and zone/storm/summon strikes share the path.
- **Fix:** new `SkillFx.ImpactSphere(worldPos, color, radius, lifetime = 0.45f)` + nested
  `ImpactSphereFader`: a collider-stripped `PrimitiveType.Sphere` (script-built, prefab-free like
  all combat FX) grows from 0.25Ã— to full radius ("explodes") while its per-instance material
  alpha ramps 0.9 â†’ 0 ("as it explodes, transparency increases"), then destroys itself. Hooked in
  `SpellEffect.ResolveProjectileImpact`: the impact dent is KEPT (gameplay carve + Earth signature)
  but now passes `emitDebris:false`, and the sphere spawns at the impact point tinted
  `DamageNumber.ColorFor(_spell.Type)` and scaled to `Mathf.Max(0.8f, _spell.Radius)`.
- **Plumbing (zero-breakage, rule 5):** `WorldStreamer.DeformAt(..., bool emitDebris = true)` gates
  `SpawnCraterDebris`; `TerrainDeformer.Apply(..., bool emitDebris = true)` forwards it. All other
  callers (tools `Dig`, zone/summon/storm casts, test-ground lanes) keep the default â€” the cube
  burst remains for digs and zone/storm/summon strikes only.
- Asked-in-plan choices: sphere color = spell element color; dent kept (sphere replaces the cubes,
  not additive); size scales with the spell's radius.
- **Docs (same pass):** game-design Â§3.7 (new impact-sphere paragraph after the projectile-dent
  description), Â§3.8 crater-debris note + Debris shape table row, Â§2.5 pooling bullet;
  `SpellEffect.cs` / `WorldStreamer.Deform.cs` / `TerrainDeformer.cs` comments; THINKING Â§1gb.

### 1gb-status
- FIXED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Grep `DeformAt(` /
  `TerrainDeformer.Apply(`: only SpellEffect passes `emitDebris:false`; the other 11 call sites use
  the default. `ImpactSphere` / `ImpactSphereFader` / `emitDebris` are fresh symbols â€” no collisions
  in Assets\Scripts.
- Commit: this one.
- Pending play-test: (a) cast any projectile (fireball, frost bolt, Stone Shard, lightning, wind
  bladeâ€¦) at the ground near you â€” a school-colored sphere explodes outward at the hit point and
  fades to fully transparent instead of 3-6 floating cubes; (b) the terrain dent still carves (walk
  over the pit after); (c) hit a standing enemy â€” sphere plays at the impact point and the ground
  below still dents; (d) shovel/pickaxe swings still pop the stratum-tinted cubes (tools keep
  debris); (e) zone/storm/summon casts (Meteor, Rockfall, golem, Earth Wall) still throw debris.

## 1ga. Earth Wall renders "vertical on the player's view" + spurious circular aim preview â€” perpendicular ridge, wall preview suppressed

User report: "the earth wall create walls with vertcal on player view, and have circle preview for
some reason, fix it".

- **Root cause A (orientation):** `WorldStreamer.DeformAt` built the Wall ridge ALONG the projected
  cast direction â€” looking at the aim point puts the ridge edge-on, so it reads as a thin
  screen-vertical slab receding with the view instead of a wall. This matched the old spec
  (game-design Â§3.8 "along the cast direction"), so it was a spec/UX disagreement, not a code fault;
  the user was asked along-vs-across and chose ACROSS. Fix: the Wall influence branch now derives
  the spine as `ridge = (-wallDir.z, 0, wallDir.x)` (rotated 90Â°) â€” a left-right barricade across
  the cast â€” while `wallDir` itself still feeds the voxel crater directed-dig clip untouched.
  Aim point, ~2.6 m height cap, keep-out ring, width bounds all preserved.
- **Root cause B (circle preview):** `PlayerController.UpdateAoePreview` showed the circular
  `AoeAimPreview` disc for every Zone/Vortex/Summon/Storm spell, including directional
  `TerrainShape.Wall` â€” a round footprint is meaningless for a ridge. Fix: skip the disc when
  `spell.TerrainShape == TerrainShape.Wall`; Crater/Ring/Spikes/Pillar are radial and keep theirs.
  (`Hide`/`Lock` already guard the inactive state.) The hand `CastingCircle` halo and the impact
  `SpawnZoneRing` flash are separate visuals and unchanged.
- **Docs (same pass):** game-design Â§3.8 (ridge across the cast, 1ga note), `SpellData.cs` enum doc
  + tooltip, `SkillCatalog.cs` earth-school comment + Earth Wall/Landslide tooltips,
  `SkillCatalog.Magic.cs` Landslide tooltip + comment, `SpellCaster.Cast.cs` orient comment,
  `magic-skills.md` both Wall rows, `THINKING.md` Â§1ga. Historical PROGRESS entries describing the
  old "along the cast" behavior are left as-is (they record what shipped then).

### 1ga-status
- FIXED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Grep `along the cast` after the
  edits leaves only non-wall wording (beam aim, vortex placement) and historical entries; `ridge` is
  local to the Wall branch; `wallDir` remains the sole input of the voxel crater clip.
- Commit: this one.
- Pending play-test: (a) cast Earth Wall / Landslide on flat ground and slopes â€” ridge runs
  left-right ACROSS the view, reads as a barricade, blocks movement/knockback; (b) no circular disc
  while aiming the wall; (c) regressions: Crater/Ring/Spikes/Pillar aim discs unchanged, repeat wall
  still caps ~2.6 m, voxel directed-dig (tool swing into a slope) unchanged, hand casting-circle
  halo still shows for all armed magic.

## 1fz. Magic deformation leaves a see-through hole at the cast site (intermittent, persists across reload) â€” stale far-band LOD hardening shipped

User report: casting an earth spell (Wall/Pillar/etc.) occasionally leaves an uncovered,
see-through area in the terrain at the cast site; walking closer makes it disappear; it persists
after unload/reload; intermittent ("sometime", "close up").

- **Full audit verdict (see THINKING Â§1fz for the trail):** the entire pipeline
  `DeformAt` â†’ `ApplyHeightEdits` â†’ `RebuildChunkRegion` â†’ `PatchRegion` â†’ `PatchCornerGrid`/
  `BuildCornerGrid` â†’ `BuildMergedMeshData` â†’ `BuildOrLoadChunk` â†’ `FlushDirtyChunk` â†’
  `BuildLodChild` was line-by-line verified clean â€” including the original suspect (refined-lattice
  corner-slot mapping), which was FALSIFIED: `IsRefinable` (ChunkMeshGenerator.cs:220) restricts
  refinement to strictly interior tiles, so border tiles are always coarse and interior lattice nodes
  only ever read slot 3 (= SW corner, exact in both layouts). Worker race ruled out (`_loadedData`
  is main-thread-only); unload destroys chunk GOs (no pooling reuse).
- **The one provable defect found & fixed:** `ChunkLodManager.Update` refreshed LOD children only
  when the distance band *changed* (`ApplyBand`) â€” a chunk already showing a detail band (Lod1
  30-60 m, Lod2 60 m+) that got deformed kept a **stale pre-deform surface** until the player
  crossed a band boundary, contradicting the invariant documented at ChunkObject.cs:58-59 /
  ChunkLodManager.cs:178-180 / game-design Â§2.2 ("a far band never renders a pre-excavation hole").
  **Fix:** expose `ChunkObject.LodDirty` (reads the existing `_lodDirty` flag set by every
  `ApplyMerged`/`PatchRegion`) and, in the manager's per-scan `else` branch, call
  `RefreshLodMeshes()` for any `band > 0` chunk whose flag is set â€” one bool read per scan tick
  for clean chunks; `RefreshLodMeshes` itself early-outs when the flag is clear.
- **Hypothesis status:** shipped, UNCONFIRMED against the "persists after reload" symptom (reload
  builds fresh chunk GOs, so a stale child alone doesn't obviously survive it). Alternates still
  open if play-test shows the hole persists: (a) legacy/corrupt pre-1fx save tile heights
  regenerated as noise inside deformed regions (deterministic â†’ persists); (b) sub-LOD-step thin
  deform features vanishing in decimated far-band sampling; (c) `RegisterChunk` snapshot timing
  showing stale children (`entry.Details` captured before lazy child build).
- **Docs:** `game-design.md` Â§2.2 LOD bullet (band-switch-only wording â†’ also polls `LodDirty`
  per scan tick, 1fz); `THINKING.md` Â§1fz (audit trail, falsified hypothesis, alternates).
- Out-of-scope observations noted but NOT changed: `RegisterChunk` snapshot timing; FarShell far
  cells never invalidated by deform (live â‰¥~300 m, contradicts "close up").

### 1fz-status
- SHIPPED (hypothesis pending validation); verified by grep + reread (rule 3 â€” no CLI/Unity build).
  Grep confirms `LodDirty` has exactly two references (declaration + the new poll site);
  `RefreshLodMeshes` clears `_lodDirty` on every path (ChunkObject.cs:272/275/282) so the new
  branch cannot spin per-frame; diff of both files inspected byte-level (em-dashes intact, no BOM
  corruption from an aborted PowerShell edit â€” that file was `git checkout`-restored and re-edited).
- Diagnostic/fix commit: this one (new commit, not amended).
- Pending play-test (rule 3 = no build): (a) cast Wall/Pillar/Spikes/Ring/Crater at 30-120 m â€”
  no see-through hole at the cast site when viewed from range, close up, and after
  unload/reload; (b) cross a band boundary (walk from 60 m+ toward the cast site) â€” no stale
  pre-deform surface flash before the refresh; (c) regressions: normal LOD banding still
  simplifies distant chunks, collider behavior unchanged; (d) if the hole PERSISTS in play-test,
  investigate save integrity next (ChunkTileMod NaN/garbage vs `IsSaneHeight` noise-fallback â€”
  the "persists after reload" lead).

## 1fx. World-load regression ("map under the player no longer loads") â€” root cause found + fixed: BuildCornerGrid far-corner index out of bounds (1ew regression)

User report right after 1ew shipped, in three zooms: (1) "the map under the player no longer loads",
(2) "only chunks near the player are missing", (3) after the diagnostic instrumentation commit, the
smoking gun: a ~300Ã—300 m hole around the spawn with no invisible ground, plus the Console line:

```
[WorldStreamer] Background chunk generation failed for TChunk(-1,3); it will retry.
System.IndexOutOfRangeException: Index was outside the bounds of the array.
  at ChunkMeshGenerator.BuildCornerGrid â€¦ ChunkMeshGenerator.cs:712
  at ChunkMeshGenerator.BuildMergedMeshData â€¦ at WorldStreamer.BuildOrLoadChunk â€¦
```

- **ROOT CAUSE (1ew regression): `BuildCornerGrid`'s corner-ownership branch order.** The lattice loop
  visits all 31Ã—31 nodes including the far corner (gx=cs, gz=cs)=(30,30). The shipped order
  `if (gx<cs && gz<cs) â€¦ else if (gz == cs) â€¦` matched (30,30) with the **`gz == cs` branch first** and
  computed `ownerIdx = (cs-1)*cs + gx = 29*30 + 30 = 900` on the 900-length tiles array â†’
  `IndexOutOfRangeException` on **EVERY** chunk build â†’ every real chunk failed to materialize â†’ the
  whole near real-chunk ring (`NearRingRadius=9`, ~300 m) was a hole while the INDEPENDENT far shell
  kept rendering past ~300 m (it only skips cells whose real chunk is pending/in-flight â€”
  FarShell.cs:468-472 â€” so the hole was bounded exactly at the ring cut, hence "300Ã—300"). The worker
  catch had logged only `ex.Message` (stack discarded) pre-diagnostic, which is why the first pass
  didn't surface it.
- **Fix (ChunkMeshGenerator.cs):** boundary branches made mutually exclusive â€” `else if (gx < cs)` =
  north edge (owner tile (cs-1, gx) NW), `else if (gz < cs)` = east edge (owner tile (gx, cs-1) NE),
  final `else` = far corner â†’ owner tile (cs-1, cs-1) SE slot 2 â€” exactly the retired
  `WorldCornerIndex` result, so LOD surfaces stay bit-identical to pre-1ew. The SAME exact-corner
  ownership applied to `PatchCornerGrid`, whose pre-fix `gz == cs` branch claimed (30,30) for tile
  (30,29); the region-bounds check then skipped it, silently never re-stamping the chunk's NE lattice
  node after a patch (stale far-LOD corner) â€” same root, no throw.
- **Kept from the diagnostic pass:** full `ex.ToString()` in the worker catch, main-thread
  try/catch+`Debug.LogException` around `FinalizeChunks` and the boot `GenerateChunkSync` â€” safety nets
  that never fired for H2/H3 but stay for future regressions.
- **Docs:** `THINKING.md` Â§1fx (H1 CONFIRMED, H2/H3 not-the-cause, H4 never reached; far shell /
  Deform sampling / idle-gate dead ends recorded).

### 1fx-status
- FIXED; verified by grep + reread (rule 3 â€” no CLI/Unity build). Grep confirms no other
  `(cs-1)*cs` / `== cs` owner-index site remains in `Assets\Scripts` (the pattern existed only in the two
  fixed methods); `WorldCornerIndex` still only appears in doc comments (symbol remains removed);
  branch coverage of both fixed 4-way lattices re-checked exhaustively (all nodes in [0..cs]Â² map
  exactly once; (30,30) â†’ tile (29,29) slot 2).
- Diagnostic commit: `2369f4c` (1fx instrumentation). Fix commit: this one.
- Pending play-test (rule 3 = no build): (a) near ring around spawn FILLS with real terrain (no
  300 m hole) on a fresh Play + also after re-enqueue; (b) the whole radius renders smoothly to the
  horizon â€” real chunks near, far shell beyond, no seam/step at the ring cut; (c) dig/corner-edit a
  tile near a chunk's NE edge, then check the far-band LOD re-stamps (no stale corner) once the patch
  applies; (d) no `[WorldStreamer] Background chunk generation failed` warnings in Console during a
  few minutes of walking; (e) 1ew checks still valid: steep slopes show split small faces, revisit
  restores exactly, voxel opt-in unaffected.

## 1ew. Smooth terrain adaptive stretch-split (Phase A: refinement data + rendering) â€” no more single stretched faces on steep slopes

User report (prior problem this supersedes): after the 1ev smooth revert, a steep slope renders a
1x1 tile as ONE hugely stretched quad â€” the corner-grab editor grabs only world-corner keys, so an
interior face has no vertex to bite ("one surface only") and a cliff reads as un-editable. Fix chosen
by user: model A adaptive stretch-split (split exactly the over-stretched tiles), NOT a uniform finer
grid, NOT the blocky voxel mode. This task (1ew) is Phase A: derived refinement + merged block table +
LOD-from-lattice; Phase B (1ex: fine-lattice editor writes + v4 saves) is queued; 1ey (normals shading
smoothing + budget sanity) after that.

- **Refinement rule (`ChunkMeshGenerator`):** a tile whose 4 corners differ by more than
  `WorldStreamer.RefineThreshold` (serialized, default `ChunkMeshGenerator.DefaultRefineThreshold` =
  2.5 m; 0 = off) emits a **2x2 sub-quad block** (16 verts / 8 tris, row-major, non-welded like legacy)
  instead of the coarse 4-corner quad. Interior fine heights are **bilinear from the 4 coarse corners**
  (edge midpoints = linear in the shared corners â‡’ zero cracks against coarse neighbours). Interior-of-
  chunk only (local index 1..28) so the 1 m border ring keeps the cross-chunk shared-corner contract.
- **DERIVED, never stored in 1ew:** pristine corners re-derive from noise and edits still store coarse
  4-corner heights only (DeformAt/FlattenAt/GetDigDepth unchanged) â€” the save format is byte-identical
  to pre-1ew. The planned v4 fine-lattice save section MOVED to 1ex per the phased plan.
- **Merged block table:** `MergedChunkMeshData.TileVertexBase/TileVertexCount` (per 900 tiles, built
  after the defensive fill so fallback tiles count correctly) replace the fixed `(tileIndex * 4)`
  stride; `ChunkObject.PatchRegion` re-skims the region through the table + re-stamps the LOD lattice
  via `ChunkMeshGenerator.PatchCornerGrid`.
- **LOD from lattice:** `MergedChunkMeshData.Corners` (31x31 `ChunkCornerGrid` of
  Y/Normals/UV/Colors; ownership per corner mirrors the retired `WorldCornerIndex` rule) is built by
  `BuildCornerGrid` and re-stamped per patch; `BuildLodChild` decimates from it (`WorldCornerIndex`
  removed â€” refined blocks broke the fixed merged stride).
- **Edit flips split state â‡’ full rebuild:** `RebuildChunkRegion` compares fresh
  `ChunkMeshGenerator.IsRefined(tile)` vs `obj.IsTileRefined(lx,lz)`; any flip â†’ `FullRebuildChunk`
  (block table cannot resize in place).
- **No wall conflicts:** `EdgeIsRaised` only fires on flat slabs; slabs are flat (deltaâ‰ˆ0 â‡’ never
  refined) and refined steep tiles have equal shared corners (no walls) â€” verified by reasoning + grep.
- **Docs:** `game-design.md` Â§2.2 rewritten (stale 5-vertex/center-vertex chunk â†’ real 900-tile /
  31x31-corner model + merged mesh + LOD-from-lattice) and new Â§2.10 (adaptive stretch-split);
  `THINKING.md` Â§1ew.

### 1ew-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI/Unity build). Grep confirms: `RefineThreshold`
  defined (WorldStreamer.cs:57) and passed at all three BuildMeshData sites (ChunkBuild.cs:133,
  Deform.cs:246 + FullRebuildChunk, Deform.cs:499 RebuildChunkRegion); `TileVertexBase`/`TileVertexCount`
  set in BuildMergedMeshData and consumed only by PatchRegion/IsTileRefined (voxel builders leave them
  null and every reader null-guards); `ChunkCornerGrid` built by BuildCornerGrid, re-stamped by
  PatchCornerGrid (called from PatchRegion), decimated by BuildLodChild; `WorldCornerIndex`,
  `topVertsPerTile`, `ChunkMeshData.DefaultY` removed with no remaining references.
- Pending play-test (rule 3 = no build): steep slopes show multiple small faces (no blocky steps, no
  holes/seams at splits), corner edits still move coarse corners, revisiting an area restores it
  exactly; try digging a 2.5 m+ surface and see the 4â†’16 split flip; a voxel-opt-in run still
  streams stepped meshes (smooth path untouched).

## 1ev. Voxel un-defaulted â€” smooth heightfield is the world's default terrain again; the stepped voxel model is back to opt-in

User report after 1eu shipped: the world now "looks somewhat like Minecraft, terrain made of blocks" â€”
and that's not wanted. Root cause: 1eu flipped `WorldStreamer.VoxelTerrainEnabled` to **default ON**,
so every chunk rendered as a 1-metre stepped column world (flat column tops + terrace walls). The
smooth height-field path was untouched and is cleanly gated on that one bool (chunk build/rebuild/flush
in `WorldStreamer.Streaming.cs`, LOD in `WorldStreamer.Mesh.cs`, far shell in `WorldStreamer.FarShell.cs`,
deformation + sculpt in `WorldStreamer.Deform.cs`), so the fix is a single-flag revert. User approved
"Revert default, keep opt-in" â€” voxel XOR deleted (the column store, v3 saves, sculpt API and voxel LOD/
far-shell work stay for experiments; the 1eu entry below is now historical).

- **Changed:** `WorldStreamer.cs` `VoxelTerrainEnabled = false` (+ tooltip rewritten: smooth is the
  default again, voxel is the opt-in preview). No voxel code was removed.
- **Docs:** `game-design.md` Â§2.9 header/intro corrected (1ev un-defaults voxel); `THINKING.md` Â§1ev.
- **Save-format note (documented 1et limitation, unchanged):** the smooth reader rejects voxel v2/v3
  chunk saves, so any chunk previously saved under voxel mode regenerates from noise under smooth. If
  the map looks odd after the flip, start a New Game or tick `NewWorldTestGround.EnableResetTerrainSaves`
  once.
- **QA lanes unaffected:** `EnableVoxelTerrain` + `EnableVoxelSculptDemo` still work as opt-in to
  preview/iterate the experimental stepped world.

### 1ev-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). Confirmed: `VoxelTerrainEnabled`
  has exactly one definition at `WorldStreamer.cs:53`, now `= false`; every guarded call site still
  routes on it (Streaming.cs:157/194/317, Mesh.cs:106, FarShell.cs:668, Deform.cs:137/229/462,
  Voxel.cs:265, TestGround.cs:118/310) so the smooth path is active and voxel stays reachable via the
  toggle; grep of `game-design.md`/intro shows no remaining "voxel default" claims.
- Pending play-test (rule 3 = no build): fresh Play renders smooth rolling terrain everywhere (near
  chunks, LOD, far shell) with no 1 m steps/terrace walls; smooth deformation (crater digs, shovel/
  pickaxe strata banding, Earth spells) works as before 1eu; a chunk saved in a voxel v3 file
  regenerates cleanly (or run the reset toggle); optional: tick `EnableVoxelTerrain` once to confirm
  the stepped experimental world still streams.

## 1eu. Voxel terrain Phases 2-4 â€” multi-run columns, sculpt API, directed dig, v3 saves, voxel LOD children + far shell, voxel-on default (voxel is now the default terrain of the world)

P2/P3/P4 of the voxel program (P1 shipped in `1et`), all in one pass. Turns the P1 single-run
column world into a **true volumetric carve world** and makes it the world's default terrain.

- **Multi-run column store (`VoxelChunkData.cs`, rewritten):** columns are now sorted run lists
  `(YBot, YTop)` instead of a single `[ColumnBaseY .. Top]` run. New ops: `RemoveSolid`/`AddSolid`
  (boolean volume insert/remove with run merge + pristine-prune â€” a column that ends equal to its
  noise top is dropped and `_modifiedCount` decremented, keeping saves sparse), `SetColumnRuns`
  (v3 save load), `VisitColumns` (v3 save write), `RunsAt` (per-column runs for cave meshing),
  `SetSurfaceTop(x, z, top)` â€” an **overburden-only surface shave/add that preserves buried caves**
  (diff against the current top run, not a full column replace; used by the dirty-tile overlay in
  rebuild/flush so live edits never collapse a sculpted chamber).
- **v3 save format (`ChunkSaveManager.cs`):** `VoxelSaveVersion = 3`; writes each edited column as
  `idx, runCount, (YBot, YTop)*` via `VisitColumns`; reader accepts v3, migrates v2 (single-run) and
  v1 (legacy height-field) on read. Smooth path untouched (`CurrentVersion` still 1, its reader still
  rejects voxel versions).
- **Chunk pipeline (`TerrainChunkMeshData.cs` + `ChunkObject.cs`):** `TerrainChunkMeshData.Voxel`
  carries the store through the build; `ChunkObject.VoxelStore` is attached at CreateChunkGameObject
  and cleared in Release. `WorldStreamer.Voxel.cs` rewritten around the store: `BuildVoxelChunk`
  ships the store, `FullRebuildVoxelChunk` = live store + dirty-tile `SetSurfaceTop` overlay + real
  borders (`BuildVoxelBorderTops`), `FlushVoxelChunk` = store + dirty overlay then saves
  (`SaveVoxelChunk`) or **deletes the file** when the chunk is pristine again.
- **Sculpt API (`WorldStreamer.Voxel.cs`):** public `SculptVoxelCave(center, radius, roofThickness,
  chamberHeight)` and `SculptVoxelRaise(center, radius, height)` â†’ `SculptVoxelVolume` (sphere, per
  column AddSolid/RemoveSolid on loaded chunks only, then rebuild + `FlushDirtyChunk` per touched
  chunk). A cave keeps an untouched roof shelf above the void (chamber real).
- **Directed dig (`WorldStreamer.Deform.cs` + `TerrainDeformer.cs` + `ToolManager.cs`):** Crater
  branch in voxel mode clips the influence sphere to the tiles ahead of the cast direction
  (`influence *= Clamp01(along / max(0.25, radius*0.5) + 0.15)`), so a shovel/pickaxe swing is a
  half-space scoop into the slope. New `TerrainDeformer.Dig(center, radius, Vector3 dir)` overload;
  `ToolManager` passes `player.transform.forward` for both dig tools.
- **Cave rendering (`VoxelMesher.cs` Pass 1b/1c):** buried runs now render: every non-topmost run
  emits its top (chamber floor), any run whose bottom sits above the column floor emits a downward
  **ceiling** quad via new `EmitRunCeiling`. Documented P2 limit: interior cavity side walls are NOT
  meshed (the wall pass reads only the topmost run) â€” the rim of a carve reads as a slot into the
  void until per-run side-wall meshing lands.
- **Voxel LOD children (`ChunkObject.cs`):** `RefreshLodMeshes` gained a voxel branch +
  `BuildVoxelLodChild` (decimated 2Ã—2 blocks, rounded-mean top assembled into a coarse full-size
  `VoxelChunkData` â†’ `VoxelMesher.Build` â†’ pooled LOD mesh) and a shared `EnsureLodChild` with the
  smooth path. Gate fix: `if (!_lodDirty || VoxelStore == null)`.
- **Voxel far shell (`WorldStreamer.FarShell.cs`):** new `BuildVoxelFarSector(cell, seed, maxRing)` â€”
  stepped twin of `BuildFarSector` sampling the same deterministic integer column tops
  (`VoxelChunkData.RoundNoiseTop`) on the 3 m far grid with merged row-run tops + terracing walls
  (`EmitVoxelFarTopRun`/`EmitVoxelFarWallStrip`); `BackgroundGenerateFarCell` routes on
  `VoxelTerrainEnabled`. The far disc now steps like the near world â€” no smooth seam at the rim.
- **Default ON (`WorldStreamer.cs`):** `VoxelTerrainEnabled = true` (voxel is the default terrain;
  smooth is the opt-out). QA toggle `NewWorldTestGround.EnableVoxelTerrain` unchanged; new QA lane
  `EnableVoxelSculptDemo` (directed dig + cave + raise with stone markers off the platform's west
  edge, no-op with a warning if voxel is off).

### 1eu-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). Confirmed in the tree:
  `VoxelChunkData` has all of Create/RoundNoiseTop/ColumnTop/RunsAt/PristineRun/VisitColumns/
  SetColumnRuns/SetColumnTop/SetSurfaceTop/RemoveSolid/AddSolid plus `struct VoxelRun`
  (VoxelChunkData.cs:326); `ChunkSaveManager.VoxelSaveVersion = 3` with v3-write `WriteVoxelChunk`
  (L442) and v1/v2/v3 reader guard (L179); `ChunkObject.BuildVoxelLodChild` (L376) + `EnsureLodChild`
  (L351) + voxel `RefreshLodMeshes` branch (L254-268); `WorldStreamer.Voxel.cs` store-backed
  build/rebuild/flush + `SculptVoxelVolume`/`SculptVoxelCave`/`SculptVoxelRaise` (L205-224);
  `TerrainDeformer.Dig` both overloads; `WorldStreamer.Deform.cs` Crater dir clip; `ToolManager`
  passes `player.transform.forward`; `BuildVoxelFarSector` (FarShell L871) routed at L668;
  `WorldStreamer.cs` default ON; `NewWorldTestGround.EnableVoxelSculptDemo` lane + marker spawner.
  Grep of removed symbols: no remaining `ForEachColumnTop`-style overlay callers on the rebuild path;
  `SaveVoxelChunk`/`VisitColumns`/`SetSurfaceTop` call sites match signatures.
- Pending play-test (rule 3 = no build): P2 gate = carve a cliff/ridgeline into a slope with the
  shovel or `SculptVoxelCave` and verify the chamber floor + ceiling read correctly (known slot-rim
  artifact until per-run walls land); P3 gate = sprint across >10 chunk crossings with voxel ON and
  no ~30 m spike (LOD + far shell are now voxel-meshing too); P4 = the `EnableVoxelSculptDemo` lane
  directed-dig scoop clips to the cast direction on a slope; restore a carve exactly to noise and
  confirm the file deletes; legacy smooth world via the OFF toggle still stream/saves/edits.

## 1et. Voxel terrain Phase 1 â€” the height-field-stretch fix behind a stepped 1 m world (render + persistence + adapter, toggle OFF), world unchanged for legacy mode

User problem (picked up after the 1es lag work): the heightfield's single-quad model makes steep
adjacent terrain an uneditable stretched face; asked whether to flatten or go volumetric, the user
chose **full volumetric/voxel terrain** and approved Phase 1. 1et ships the P1 slice behind
`WorldStreamer.VoxelTerrainEnabled` (serialized bool, **default OFF**): every real chunk renders as a
**1-metre stepped voxel world** (flat column tops + terrace walls) while keeping the whole streaming /
pooling / budgets / deformation API / save-file pipeline's public contracts intact. The legacy
height-field path is untouched and remains the default until play-test reads the voxel world right.

- Render decision: 1 m columns, so the game's voxelate-style visuals (1dx) and the 
  steep-terrain shape match the horizon idea. Material **derived** from dig depth below the pristine
  noise surface (`TerrainBandColor`) â€” never stored, so a column run is two integers.
- Storage decision: **sparse column-run store** (`VoxelChunkData`), one run
  `[ColumnBaseY=-1000 .. Top]` per edited column, pristine columns = null â†’ regenerate from the same
  deterministic noise rounding. Infinite dig via the huge vertical clamp (column floor at -1000,
  tops clamped Â±200 like the heightfield) â€” a pit is solid as deep as gameplay can reach.
- `Assets\Scripts\World\Terrain\Voxel\VoxelChunkData.cs` (new): `SetColumnTop` (pristine-value write
  clears the column back to untouched â€” keeps saves sparse), `ColumnTop` (stored else noise),
  `ForEachColumnTop`, `HasModifications` (O(1) counter), static `RoundNoiseTop(seed, wx, wz)` so every
  chunk touching a world column derives the SAME value (untouched seams always level), `VoxelRun`.
- `Assets\Scripts\World\Terrain\Voxel\VoxelMesher.cs` (new): stepped mesh builder â€” merged row-run
  top quads (one quad per equal-height run, per-metre UV tiling), terrace walls on every
  higher/lower column pair (one 1 m band per metre of drop; drops > 32 m quantize to 16 bands so deep
  pits never explode the tri budget), boundary planes run-merged into wall strips, cross-product
  winding matching the merged builder, memoized band colours, bounds from the sampled y-range.
- `Assets\Scripts\World\Streaming\WorldStreamer.Voxel.cs` (new partial): the background
  `BuildVoxelChunk` (column save load â†’ 900 flat 4-corner adapter tiles â†’ stepped mesh),
  `FullRebuildVoxelChunk` (authoritative tiles â†’ column store â†’ mesh with real neighbour border),
  `VoxelTopFromTile` (rounded avg of sane corners), `BuildVoxelBorderTops`/`VoxelBorderIfLoaded`
  (loaded-only ring, falls back to noise for missing neighbours â€” no phantom walls), `FlushVoxelChunk`
  (sparse full-chunk v2 snapshot; restores-to-pristine **deletes** the file).
- Wiring â€” voxel flag captured ON THE MAIN THREAD at dispatch so a chunk never changes shape
  mid-build: `WorldStreamer.ChunkBuild.cs` `BackgroundGenerateChunk(tc, seed, bool voxel)` â†’
  `BuildVoxelChunk`/`BuildOrLoadChunk`; `WorldStreamer.Streaming.cs` dispatch + `GenerateChunkSync` +
  `FlushDirtyChunk` branches; `WorldStreamer.Deform.cs` `FullRebuildChunk`/`RebuildChunkRegion` route
  every edit to a full voxel rebuild; `WorldStreamer.Mesh.cs` sets `obj.VoxelMesh =
  VoxelTerrainEnabled`. LOD gate: `ChunkObject.VoxelMesh` â†’ `RefreshLodMeshes()` no-ops (stepped mesh
  has no TOPS-FIRST grid to decimate) and the root mesh always renders; the far shell intentionally
  stays smooth in P1.
- `Assets\Scripts\World\Chunks\ChunkSaveManager.cs`: **v2 column-run format** on the same chunk files
  (`NWTC | int 2 | seed | cx | cz | colCount | per col: idx=localZ*30+localX, top`), written by
  `WriteVoxelChunk` (atomic tmp+swap, same worker + `_saveWriteLock`); `SaveWork` carries an optional
  `VoxelChunkData` (ownership transferred like `ChunkSaveData`); `SaveChunkNow`/`DrainSaveQueue`/
  `FlushPendingSaves` route voxel payloads. **Migration:** `TryLoadVoxelChunk` accepts v2 AND v1 â€”
  a legacy height-field save converts on read (per-tile average of sane 4-corner heights â†’ rounded
  column top) and stays in place until an edit rewrites it as v2. `CurrentVersion` stays 1 for the
  smooth path (its reader still rejects v2 â€” documented limitation).
- QA surface (rule 4): `NewWorldTestGround.EnableVoxelTerrain` serialized toggle, applied in Awake
  (`streamer.VoxelTerrainEnabled = true`) before the streamer's first poll â€” flip on to play-test the
  voxel world on the test platform without touching the inspector.

### 1et-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). All branch points confirmed:
  dispatch captures `voxel` at Streaming.cs:157 before queueing, `GenerateChunkSync` branches
  (Streaming.cs:194), `FlushDirtyChunk` â†’ `FlushVoxelChunk` (Streaming.cs:317), `FullRebuildChunk` +
  `RebuildChunkRegion` â†’ `FullRebuildVoxelChunk` (Deform.cs:217/450), `Mesh.cs` sets `obj.VoxelMesh`.
  Save manager re-read clean: `SaveChunk`/`SaveChunkNow` default-compatible for existing callers
  (`ChunkTileMod`/`ChunkSaveData` untouched), `SaveWork` ctor updated call sites â€” grep confirms no
  stale `BackgroundGenerateChunk(tc, seed)` 2-arg callers, `SaveChunkNow(seed, tc, data)` callers
  still compile via the optional `voxel = null`. Two issues caught in this pass and fixed:
  `VoxelMesher` wall band lerp had a dead `bandH` variable (removed) and one `TrackY` call missing its
  `ref` modifier (compile error) â€” both fixed. ChunkObject gate + far-shell LOD behavior re-read.
- Memory/CPU notes for P1: calm terrain â‰ˆ merged top runs + 1-band walls (~2-4 k tris/chunk, similar
  to smooth); the mesher is pure lists (worker-thread safe); bounds sized to the sampled y-range;
  border maps built per rebuild only.
- Known trade (accepted): LOD children + far shell stay smooth in voxel mode (decimated grid is
  TOPS-FIRST only); the 1 m step pattern intentionally replaces the smooth slope look â€” verify the
  test-platform lanes (flat ground â‡’ single merged quad) hold their footing.
- Play-test (pending, Unity): enable `NewWorldTestGround.EnableVoxelTerrain` (or flip the streamer
  toggle before play) â€” world builds as stepped 1 m terrain with **no lag/collider corruption**;
  deform (dig/flatten/spells) still edits and the chunk is the **only** sphere re-rendering (no
  neighbour seams, no phantom walls on untouched borders); carve a cliff wall then leave the chunk
  and return â€” the stepped edit **restores from save**; save + reload the world â€” edits persist;
  restore a carve exactly to flat noise then leave â€” the chunk's save file is deleted (no ghost edit);
  sprint across >10 crossings â€” no 1es regression; bench lanes (tilled soil, enclosures, pedestals)
  sit ON the voxel ground (tools/spells keep working unchanged). Then report: if the stepped world
  looks right, Phase 2 (multi-run column sculpt + directed carve) is next; legacy height-field
  deletion happens in the final phase.

## 1es. Crossing-spike smoothing â€” one shared main-thread stream budget + off-thread chunk saves (periodic spike at each ~30 m chunk crossing gone)

User report: "player lag when travel the world", "game slow/freeze at every ~30 m chunk boundary".
Asked-and-answered first: is the spike periodic (every chunk) or random? Answer: **periodic, every
chunk (~30 m)** â†’ the offender is the boundary-crossing STORM of work, not one random system. Trade
decision: user chose **smoothness wins over fill speed** â€” the fill may trail slightly when sprinting,
but no hitch. 1es removes the spike by (a) pooling every main-thread streaming step against ONE shared
`StreamBudgetMs` (4 ms) poll-wide budget so a crossing spill cannot leak into the frame, and
(b) moving chunk save-file writes off the main thread so "edit terrain â†’ walk out â†’ unload flush" no
longer freezes the frame.

- `Assets\Scripts\World\Streaming\WorldStreamer.cs`:
  - **Shared stream budget (1es core):** `StreamBudgetMs = 4f` + `_streamBudgetRemaining` +
    `_streamCapped`. `SpendStreamBudget(float ms)` charges elapsed wall time to the pool and flips
    `_streamCapped` once it is dry. Reset to `AdaptiveBudgetMs(StreamBudgetMs)` (the self-shrinking
    adaptive factor from 1di) each poll, right before streaming runs, in `Update`.
  - Idle gate now folds the unload drain into `working`: `_chunkUnloadBacklog` keeps the spread-out
    unload sweep from being skipped by the "nothing dirty, skip work" idle check.
  - `OnDestroy` calls `ChunkSaveManager.FlushPendingSaves()` after clearing dirty tiles â€” shutdown
    still lands every pending save before teardown. The idle-throttle work-skips (`_worldDirty`,
    `BuildOrLoadChunk`/`FinalizeChunks` guards) stay untouched, so standing still is still zero-cost.
- `WorldStreamer.Streaming.cs`:
  - The **unload burst** (the incoming half of a crossing â€” the trailing arc of out-of-range chunks was
    destroyed ALL in one poll) is now capped at `MaxChunkUnloadsPerPoll = 6`; the poll that can't finish
    sets `_chunkUnloadBacklog`, the sweep continues next polls and the idle gate stays busy until it
    drains. The entry cap comment on the "capped unload sweep" block updated. Worst-case ~60-chunk turn
    drains over ~10 polls instead of one 60-destroy spike.
  - `ResetTerrainSaves` (New Game) now calls `ChunkSaveManager.FlushPendingSaves()` BEFORE
    `ResetWorldSaves()` wipes the discarded-file bookkeeping â€” pending writes for the old world finish
    on their own files, not re-targeted at the reset files.
- `WorldStreamer.Mesh.cs`: `FinalizeChunks` (real-chunk mesh upload) is now gated by `_streamCapped`
  and spends per-chunk into the shared pool (the old `ChunkFinalizeBaseMs = 6f` const is deleted â€” 1es
  replaced the fixed per-pass slice with the poll-wide pool). `AdaptiveBudgetMs` doc updated: it both
  sizes its own burst AND the shared `StreamBudgetMs` pool.
- `WorldStreamer.FarShell.cs`:
  - Step (1a) active-shadow sync (per cell elapsing the retained inactive set) restricted to
    `cell.Span == 1` â€” the span-3/6 cells span multiple rings and their sync was walking the whole
    retained set every poll; span-1 is the only layer that owns the loaded/unloaded shadow.
  - The far scan phase Charges its own elapsed wall time into the shared pool BEFORE step (4) finalize,
    so a heavier scan thins the drag on the crossing that poll.
  - The far finalize loop (mesh uploads) runs against `!_streamCapped` + `SpendStreamBudget` per
    iteration â€” a crossing that needs several far-to-real handoffs spills across polls instead of one
    spike.
- `WorldStreamer.Props.cs`: `StepChunkProps` gets the same entry guard (`_streamCapped`) +
  per-chunk spend + `|| _streamCapped` in its break condition (budget mostly floors its time anyway to
  ~2 ms â€” `PropBudgetMs` lowered 3 â†’ 2 as the shared pool is now the real ceiling).
- `Assets\Scripts\World\Terrain\ChunkMeshGenerator.cs`: `UploadMerged(MergedChunkMeshData md, Mesh
  mesh)` â€” unchanged public signature; meshes upload READABLE (`UploadMeshData(false)`) always. The
  1es attempt to free far-cell CPU buffers via `markNoLongerReadable` was REVERTED in the follow-up
  `1es-fix` â€” far-cell meshes share the capped pooled-mesh cache with real chunks, and a real chunk
  re-specifies its pooled mesh on load/deform, which throws "Not allowed to access normals/vertices"
  on a non-readable mesh (play-test hit exactly that). Pre-1es behavior for real chunks unchanged.
- `Assets\Scripts\World\Chunks\ChunkSaveManager.cs`: **chunk saves moved off the main thread** while
  the LINQ/serialization stays on it (per Unity-6 IO rules, the path string stays main-thread-captured
  via `Warmup`):
  - `SaveChunk` (public signature UNCHANGED: `(long, TerrainChunkCoord, ChunkSaveData)`) now enqueues a
    `SaveWork` (path + serialized chunk bytes) into `_saveQueue` (ConcurrentQueue), then starts ONE
    background drain worker via `Interlocked.CompareExchange(ref _saveWorkerRunning, 1, 0)` +
    `ThreadPool.QueueUserWorkItem(DrainSaveQueue)`.
  - `DrainSaveQueue` drains until empty (with a near-empty re-check after the drain loop, since enqueues
    may race the loop). Vertex/serialization cost stays on the caller (main thread);
    only `File.WriteAllBytes` moves to the worker.
  - `FlushPendingSaves()` (new public) drains the queue synchronously on the current thread â€” called at
    shutdown (`WorldStreamer.OnDestroy`), at New Game (`ResetTerrainSaves`), and available for the
    legacy save path. The worker's own writes serialise with the flush thread through `_saveWriteLock`.
  - `SaveChunkNow` is now an internal step that performs the actual locked file write; `SynchronousWrites`
    toggle doc updated (still exists for the gated legacy full-flush-on-unload path). Concurrency note:
    a flush and a worker can never write the same chunk file concurrently (lock + the flush drains the
    queue first). Each await-return path re-checks the queue because `DrainSaveQueue` may have claimed
    the worker flag between the check and the start call (the loop-end re-check pattern).

### 1es-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). Symbols check out: `SpendStreamBudget`
  1 def + 5 call sites (Mesh/Props/FarShell scan/FarShell finalize + reset), `_streamCapped` read at all
  four gated entry points, `MaxChunkUnloadsPerPoll`/`_chunkUnloadBacklog` feed both the sweep and the
  idle gate, `FlushPendingSaves` 1 def + 2 main calls (OnDestroy, ResetTerrainSaves), `SaveChunk`/`UploadMerged`
  public signatures default-compatible for existing callers (`CreateMeshFromMerged` + `ChunkObject.ApplyMerged`
  call `UploadMerged(md, mesh)`; far cells upload READABLE after the `1es-fix` revert). Removed
  `ChunkFinalizeBaseMs` + `StreamFull` â€” grep confirms no remaining references. Collider/streaming width
  unchanged (colliders stay capped at 2/poll from 1ei).
- **1es-fix (follow-up):** the play-test threw `Not allowed to access normals on mesh ... isReadable is
  false` from `ChunkObject.ApplyMerged` â€” a pooled mesh that a far cell had uploaded with
  `UploadMeshData(true)` was re-acquired by a real chunk and re-specified (`SetNormals`/`SetVertices`
  throw on a non-readable Mesh). Root cause: far-cell meshes share the SAME `_chunkMeshPool` (cap 48,
  `ReleaseChunkMesh` on far destroy + real-chunk release), and pooled meshes MUST stay readable for 1dv's
  in-place re-upload. REVERTED in `1es-fix` (commit 6dâ€¦): the `markNoLongerReadable` parameter is gone
  from `UploadMerged`, far cells upload `UploadMeshData(false)` like real chunks, and the `true` upload is
  removed â€” pre-1es pooled behavior restored 1:1. No pool migration needed: the pool is a static in-memory
  queue, so restarting Play mode clears any already-poisoned mesh.
- Budget arithmetic reviewed: worst crossing step now contributes finalize (real + far) + collider cooks
  (â‰¤2) + props + unload (â‰¤6) + scan charges â€” all against ONE 4 ms (`AdaptiveBudgetMs`-scaled) pool, so a
  busy crossing poll ends when the pool dries and DEFERS the rest to the next poll rather than finishing
  them in-frame. Because `_streamBudgetRemaining` resets every frame and only the pieces that actually
  RAN spend, several crosses in a row cannot starve progress to zero (each poll refills; each poll makes
  at least the entry-level progress of every step's first unit).
- Known trade (accepted per user choice): the ring FILL is allowed to lag slightly when sprinting â€” the
  first stream of a new crossing may appear a fraction of a second later than pre-1es, in exchange for
  pattern, the spike (every ~30 m) is gone.
- Play-test (pending, Unity): sprint/walk straight for >10 crossings â€” **no periodic ~30 m frame spike**;
  goround corners / 180Â° turn â€” the trailing unload storms ("until the sweep catches up" is fine) with no
  one-frame freeze; edit terrain then walk out of its chunk immediately â€” no freeze at the unload flush
  (the write happens on the background worker; `FlushPendingSaves` only runs at shutdown/New Game);
  standing still stays zero-cost (idle gate untouched); far cells still fill without a blink (1eq/1er
  behaviors unchanged); the debug `frame time`/`cycle times` overlay's max-cycle line should sit flat with
  no ~30 m teeth. The 1es `UploadMeshData(true)` experiment is REVERTED (see the 1es-fix note above) â€”
  since far cells upload readable again, restart Play mode (the static mesh pool re-inits fresh) and
  recheck the world renders + deforms with NO "not allowed to access normals/vertices" errors.

## 1er. Swap-band pre-warm + shadow retention â€” far-shell "new ground while moving" no longer lags

User report after 1eq: "it is causing lag to render new ground when player moving, cant you use async
or smth" (confirmed: mid-far band ~330-480 m â€” the 1eq swap band around rings 13-16). Generation was
ALREADY async: the hitches were the swap band REBUILDING on the crossing â€” moving one chunk step flips
a band of boxes, the ring walk re-discovers ~90-135 brand-new span-1 cells that had been suppressed
under their span-3 boxes, dispatches up to 96 workers at once, then drains them through the main thread
(16 cells / 2.5 ms per poll for ~6-8 polls) right on top of the real-chunk finalize + collider cooks +
prop spawns. Unity mesh uploads must stay on the main thread, so "more async" can't move the last hop â€”
1er ERASES the on-crossing build instead: build the swap band AHEAD of the cut, and never destroy the
fine cells a live box replaces.

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs`:
  - **Swap-band pre-warm** (`PreWarmFarShadowCells`, new step 2b after the ring walk): every live
    span-3/6 cell whose farthest corner is within `FarPrebuildAhead` (=2) rings of its demote ring
    (`FarBandBMin+2`/`FarBandCMin+2`) appends its finer children to `_farPending` marked in
    `_farReserved`. They generate over the polls BEFORE the crossing, so `CompleteFarHandoff` at demote
    time finds every replacement already live-hidden â€” a pure SetActive swap at the ring cut. Ring 32+
    fringe cells still use the normal ring walk.
  - **Reserved payload**: `FarMeshData.Reserved` carries the flag from dispatch to finalize (not re-read
    from shared state, so it survives the in-flight period). `BackgroundGenerateFarCell(..., bool
    reserved)`; finalize accepts reserved cells even when not (yet) required and creates them via
    `CreateFarSector(cell, merged, reserved)` INACTIVE (`go.SetActive(false)`). A reserved cell that
    became genuinely required while building activates via the very next poll's active-shadow sync.
  - **Shadow retention** (removal scan, covers ALL spans now): a cell under a LIVE coarser owner
    (`owner.Span > cell.Span` with the owner present in `_farSectors`) is RETAINED as an inactive
    shadow instead of destroyed â€” promoted-away rim cells and pre-warmed children keep their meshes, so
    trailing demotes and turn-arounds reactivate the SAME bytes. Bounded: retention only exists inside
    live boxes (a subset of the shell); cells beyond keep still destroy wholesale. (The previous
    keep-alive sat inside the `cell.Span >= 3` branch only, so span-1-under-span-3 still got destroyed â€”
    restructured to generic `coveredByLiveCoarse` retain above the span check.)
  - **Dispatch pacing**: `MaxFarDispatchPerPoll` (36) on-demand + `MaxFarPrebuildPerPoll` (12) reserved
    per poll, shared `MaxFarInFlight` (96). On-demand cells precede reserved ones in `_farPending`
    (ring walk first, pre-warm appends last), so a ring cut never spawns a 96-job storm and the prefill
    ramp can never starve the near void. `_farReserved` is cleared every poll with `_farPending`/
    `_farVisited` and by `ClearFarShell`.
  - All 1eq guarantees preserved: promote retain, demote tenant retain while coverage builds, atomic
    `CompleteFarHandoff`, one live owner per region â€” no hole, no z-fight. Idle behavior unchanged
    (idle gate skips the whole tick).

### 1er-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build): pre-warm dedupe path walked
  (`_farSectors`/`_farInFlight`/`RequiredFarCell`/`_farVisited`), dispatch-order invariant (ring-walk
  on-demand cells strictly precede pre-warm reserved cells, so the `break` after
  `MaxFarDispatchPerPoll` is safe), reserved flag survives the async period via `FarMeshData`, finalize
  accepts reserved-not-required cells and creates them inactive, and every swap path re-derived:
  demote-with-retained-children (coverage ready â†’ instant handoff), demote-before-prewarm-finishes
  (tenant retains until coverage, children finalize inactive then sync activates), turn-away waste
  (reserved in-flight cells create inactive, then the next removal scan destroys them as stale â€” small,
  self-cleaning, bounded by the 2-ring prewarm window). New/changed symbols have single definitions and
  matching call sites (`PreWarmFarShadowCells` 1 def + 1 call, `CreateFarSector` 1 def + 1 call,
  `BackgroundGenerateFarCell` 1 def + 1 call); `CompleteFarHandoff`/`HideFinerChildren`/
  `FarCoverageReady` untouched. No public API/signature change; real-ring path untouched.
- Cost note: steady-state `_farSectors` grows slightly (retained inactive shadows inside the live-box
  region, ~hundreds of small 11x11-vert GOs) and step-1a sync + step-1b scan iterate them â€” bounded by
  the shell geography, never the distance walked. Trade: swap-band movement cost drops from bursty
  rebuild + dispatch + finalize storms (~90-135 cells per crossing) to a steady pre-warm trickle
  (â‰¤12 reserved dispatches/poll) + SetActive toggles at each cut. If the retained-shadow count ever
  shows in the profiler, prefer hiding the box's own shadow rather than retaining (kept for now so
  trailing swaps never rebuild).
- Play-test (pending, Unity): sprint a long straight line on the test platform across â‰¥10 chunk
  boundaries â€” the far shell at ~330-480 m must fill continuously with NO hitch/frame spike as each
  crossing's demote/promote fires (ground no longer "renders with lag" â€” it should be already there as
  a pre-warmed shadow and merely swap in); no new blank/blink or z-fight anywhere in the swap band;
  the `far cells` overlay should stay roughly steady while walking; turn around 180Â° and walk back â€” no
  rebuild hitch on re-approach (retained shadows reactivate); idle must be unchanged.

## 1eq. Far-shell ownership swaps never blink â€” promote/demote around the span-1â†”span-3 ring boundary keep one live cell per region ("chunks in range disappear and render right back" while moving)

User report: "when player move, chunks that in the range disappear and rendered right back". Dialed in
to the far shell's span-1â†”span-3 ownership swaps around rings 13-16 (~400-480 m â€” well inside the 900 m
view): requiredness is a hard Chebyshev ring cut relative to the (integer-chunk) focus, so EVERY 30 m
chunk step the player crosses flips a band of cells. On the approach side span-1 rim cells PROMOTE into
span-3 boxes; on the trailing side span-3 boxes DEMOTE into their 9 span-1 children. Replacements are
generated asynchronously (~50-400 ms), and the removal scan destroyed the outgoing cell in the SAME poll
its replacement was only enqueued â€” so BOTH swap directions left the region blank for the whole rebuild
window, then it popped back as the replacements materialized: exactly the "blink away and back" while
moving.

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs` â€” ownership handoff (1eq) so every region
  always has exactly one live owner and swaps expose neither a hole nor double-drawn ground:
  - **Promote retain** (removal scan): a fine cell whose footprint flips to a coarser cell that is
    required but NOT yet live keeps rendering as the tenant (same ownership predicate as the ring walk),
    so the region never goes blank while the coarse replacement builds.
  - **Promote handoff** (`CreateFarSector`): the newly live coarser cell takes over the same poll it is
    created â€” `HideFinerChildren` deactivates the fine cells still registered inside its footprint; the
    next removal scan destroys them (stale, already hidden, region covered).
  - **Demote tenant retain** (removal scan â€” THE fix): a demoted span-3 (or span-6) cell whose finer
    replacements are not all generated yet is RETAINED, not destroyed. Previously it fell through to the
    stale list and was destroyed the poll the ring-cut flipped â€” the exact hole above.
  - **Demote handoff** (`CompleteFarHandoff`): once EVERY required finer replacement is live
    (`FarCoverageReady`), the coarse tenant is removed (so `FarShadowedByCoarse` turns false that exact
    poll) and ownership is handed to the previously-hidden children atomically in one poll. A stale cell
    under a LIVE coarser owner stays on the plain-destroy path (already hidden, ground covered by the
    coarser mesh); a box that slid past keep handoffs away vacuously (no children required â†’ nothing
    renders â€” correct).
  - **Active-shadow sync** now also hides any cell under a live coarser owner (`FarShadowedByCoarse`),
    and `CreateFarSector` spawns replacement cells hidden when a covering owner is live (reserved
    shadow) â€” a later swap never exposes the ground.

### 1eq-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build): removal-scan flow re-derived for every
  state â€” required (keep), promote-retain (not-live coarser, keep rendering), demote-retain (finer
  replacements building, keep rendering), demote-handoff (all replacements live, atomic swap), stale
  under live coarser (destroy, covered), beyond-keep (vacuous coverage-ready â†’ handoff â†’ destroy).
  `CompleteFarHandoff` order re-read (tenant removed from `_farSectors` FIRST so the children's
  `FarShadowedByCoarse` reads false the exact poll they activate; loaded-real-chunk shadow still
  respected for span-1 children). `HideFinerChildren` runs synchronously inside `CreateFarSector` before
  any frame renders, so no frame can double-draw a promote pair. No public API/signature change; the
  real-ring path is untouched (hysteresis keep ring 10 + the 1em loaded-guard + the span-1
  real-chunk-pending keep already prevent real-chunk churn â€” re-read StreamAround/DispatchPending â€” so
  this pass is far-shell only). New helpers each have their documented single owner (grep above: 1
  definition, â‰¤2 call sites each, matching the flow).
- Cost note: promote/demote around rings 13-16 still rebuild cells as the focus crosses boundaries (the
  ring-cut is absolute), but the swaps are now COVERED so the churn is invisible. The demote tenant can
  stay live up to ~0.4 s while its 9 children build (one coarse mesh â†’ 9 fine meshes per boundary, all
  background). If the swap-band churn ever shows in the profiler, caching retired fine cells instead of
  destroying them is the lever â€” not needed for the correctness fix.
- Play-test (pending, Unity): walk a long straight line on the test platform across several chunk
  boundaries â€” the ground around rings 13-16 (~400-480 m, both sides of the player) must NOT blank out
  and pop back at any point (no "far chunks disappear and render right back" while moving); no z-fight
  or double-drawn band where a promoted cell just took over; the `far cells` overlay counter stays
  roughly steady while moving (only the leading/trailing edge cells churn); standing still unchanged
  (idle gate intact).

## 1ep. Player body faces become irregular sizes â€” non-uniform but covering lattice (Â±20%), keeping the mosaic watertight

User report: "the faces that make up the player body will be different in sizes but in the final
still cover all the area, instead of an orderly same-size grid like current." So: an irregular
hand-cut stone mosaic, verified options via clarifying questions â†’ **all parts** (ellipsoid parts,
torso silhouette, neck cylinder, hair/eyes) and **subtle Â±20% variance**.

- The player part meshes (`Assets\Scripts\Models\PlayerPartMesher.cs`) were a strictly uniform
  lattice: Rings 7 Ã— Segs 12, every non-pole cell the same 30Â° Ã— 30Â° patch (only the 1dx jitter
  0.03 unit-space + 35% squareâ†’triangle split broke it up). Implemented non-uniform spacing by
  re-emitting the lattice from two deterministic schedules derived from the existing
  `Hash01`/`AnchorSeed` (so every profile stays byte-identical per build and the cache is unchanged):
  - `Steps(count, seed, salt, irregularity)` â€” normalized step weights `1 Â± irregularity`
    (clamped â‰¥ 0.4, no collapsed slivers); `Positions(weights, total)` â€” cumulative endpoints,
    total exact.
  - **phi/rows** salt `0x1E0F01` (Â±20% band heights; poles at Ï† 0/Ï€ and torso `t = 0`/`t = 1` â€”
    the hip row + crown disc + neck/pivot/dome contracts are untouched), **theta** salt `0x1E0F02`
    (Â±20% segment widths, ONE shared schedule per part so cells stay in aligned azimuth planes and
    quads stay near-planar).
  - Applied in `Generate` (ellipsoid), `BuildTorso` (rows on the moved `t`, silhouette still
    piecewise-linear via `Silhouette`), and `BuildCylinder` (facet widths + cap fans).
  - `CornerUV` now takes the schedule fraction arrays (`Î¸/2Ï€`, `1âˆ’Ï†/Ï€`) so texel density follows
    panel size â€” invisible today (solid-color parts), texture-ready later.
  - Watertight by construction: every interior corner is a single shared position; only the spacing
    changes, so each cell still covers its area exactly â€” the "still covers all the area" requirement.
- Assets\Scripts\Models\PlayerPartMesher.cs â€” `BandIrregularity = 0.20f`, `SegIrregularity =
  0.20f`, `StepWeight`/`Steps`/`Positions` helpers, weighted lattices in `Generate`/`BuildTorso`/
  `BuildCylinder`, schedule-based `CornerUV`, updated class docblock + constants comments (1ep).

### 1ep-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). Every `CornerUV` call site now
  passes the schedule arrays (grep: `CornerUV(lat, s, uf, vf)` in both the ellipsoid and torso band
  loops + the three fans/caps); no leftover uniform `lat * PI / Rings` / `s * 2PI / Segs` text; the
  new salts (0x1E0F01/0x1E0F02) are unique vs the existing jitter/split salts; `MapBuilder.MakePart`
  is still the only consumer (shared cached mesh, sized via localScale â€” untouched); `MakeBlock`
  (creatures/vehicles/props) is NOT affected.
- Play-test (pending, Unity): fresh Play â€” all body parts read as a hand-cut mosaic with panel sizes
  visibly varying between cells, yet the surface is fully covered (no see-through/cracks at seams);
  shoulder dome, crown, hair-over-head and neck-under-crown still overlap exactly as before; pattern
  is identical on a second Play (deterministic). If the look is wanted stronger, `BandIrregularity` /
  `SegIrregularity` are single-constant knobs.

## 1eo. Shrink the loaded range to 900 m (radius 30) and correlate the terrain generation (noise octaves rebalanced) so adjacent tiles track each other

Two requested changes, implemented in one task:

1. **Range: 67 (~2,010 m) -> 30 (~900 m), and the max clamp now equals the default.** The
   user asked to "reduce the max range of terrain loaded" and picked **30 chunks (900 m)**. The
   only live setter was `GameBootstrap.cs` (`rd.Radius = 67 / rd.MaxRadius = 160`). Now: `Radius =
   MaxRadius = 30`. `RenderDistanceController`'s class default/max and `[Range]` attributes follow
   (30), so neither a settings slider nor a future scene asset can re-widen the shell. Real near ring
   (NearRingRadius 9 / keep 10 / prop ring floor `near+1`) is untouched; the far shell now covers
   rings 10-32 (~900-1,000 cells, down from ~1,400 at 67); span-6 far cells (ring >= 36) never appear
   at the default. LOD cull auto-matches (~930 m). Camera far plane stays 2200 m (clears the shell
   with margin), and the shader's horizon tonal lift (starts ~1600 m) is now beyond the loaded world.
2. **Generation correlation ("stat" rebalance):** heights come from 5 Perlin octaves
   (`TerrainNoiseGenerator.DefaultLayers`). Detail (0.02/5) and Roughness (0.08/1.5) ran at
   wavelengths below a chunk's 30 m, so a raised tile sat next to a flat tile (little Â±1 m bumps).
   Re-weighted so the mass sits in the long-wavelength layers â€” Continental 0.001/40 -> **0.0012/55**,
   Hills 0.005/15 -> **0.004/22**, Detail 0.02/5 -> **0.012/3.5**, Roughness 0.08/1.5 -> **0.03/0.6**,
   PivotAngle 0.01/2 -> **0.008/1.5** â€” cutting the sub-chunk slope ~7x. Net relief similar; noise max
   ~Â±63.5 m -> **Â±82.6 m** (still well inside the Â±200 m `IsSaneHeight` band). This is a **noise
   rebalance only** (no deformation-blend pass; digs/craters/`DeformHeights` untouched).

- Assets\Scripts\Core\GameBootstrap.cs â€” `rd.Radius = 30; rd.MaxRadius = 30;` + comment (1eo).
- Assets\Scripts\World\Streaming\RenderDistanceController.cs â€” defaults `Radius/MaxRadius = 30`,
  `[Range(1, 30)]` (was `[Range(1, 160)]` / 160 max).
- Assets\Scripts\World\Terrain\TerrainNoiseGenerator.cs â€” `DefaultLayers` rebalanced (values above) +
  correlation rationale in the doc comment.
- Assets\Scripts\World\Streaming\WorldStreamer.cs â€” `MaxTerrainHeight` doc: noise max Â±63.5 -> Â±82.6.
- Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs â€” class + band-C docs: ~900 m view, ~1,000-
  cell fill, ~1,000 shadow-less cells, span-6 unreachable below radius 36.
- Assets\Scripts\Player\PlayerController.Camera.cs â€” far-plane comment updated (default 960 m now).

### 1eo-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build): every stale `67`/`160`/`2 km`/
  `1,400`/`Â±63.5`/`span-6` reference in `Assets\Scripts` and `game-design.md` was updated or marked
  "was ..." history; grep confirms the only live Radius setters are `GameBootstrap` (30/30) and the
  `RenderDistanceController` class defaults (30/30) â€” no scene/.asset override exists; `DefaultLayers`
  is code-only and feeds the shared corner grid (real chunks + far shell + saves) via `GetHeight`.
- Play-test (pending, Unity): horizon at ~900 m with the far rim cells around rings 10-32; near-fill
  unchanged; no crack/hole at the far edge; boot far fill ~1.5-4 s. World look: rolling coherent
  terrain, a raised tile sits among raised tiles, no Â±1 m checkerboard; Earth-spell craters still
  feather in. **Run `ResetTerrainSaves` once** â€” every unmodified generated corner shifts (saved edits
  persist). Perf: fewer far cells (~1,000 vs ~17.5k cells at the old 67 radius) â€” if the real-ring
  fill pacing ever matters again, the boot-burst-finalize lever (option A from the pacing analysis)
  is still available.

## 1en. Props match the chunk stream â€” prop ring floored to the real ring (0-330 m) + 15x faster spawn budget (120 -> 1800 tiles/tick)

User report: "the tree and stone generation range is not matching the chunk generation range". Two
distinct defects, both about the prop stream trailing the terrain stream:

1. **Range:** `PropRingRadius` defaulted to 4 (â‰ˆ120 m) and nothing ever raised it at runtime â€” a
   scene-serialized 4 silently restricted props while the near ring streamed real chunks to ring 9
   and the far shell covered rings 10-69. Trees/stones stopped at ~120 m while terrain rendered to
   300 m (real) and beyond (coarse far shell). Fix: `SyncPropRing` now takes `near` and enforces a
   **floor of `near + 1`** (rings 0..10, â‰ˆ 0-330 m) â€” the full set of real chunks that actually
   render full-fidelity geometry incl. the hysteresis keep ring â€” and the serialized value can only
   push the ring wider. This also covers the new "mining the ring-8-10 trees" distance, so prop
   colliders (which mine/axe raycasts hit) reach the same keep-ring chunks the player can now chop.
2. **Pace:** `PropTilesPerTick` was 120 tiles/tick (20 Hz => ~2.7 chunks/s => a 441-chunk ring would
   take **~135 s** to fill; even the old ring 4 took ~30 s). That budget was sized for the pre-1dm
   1/200 density; after the odds fell 5x to 1/1000 the tile scan itself is nano-cheap (two
   Random.Next per tile) and 120 tiles roll only ~0.24 expected props. Fix: `120 -> 1800` (15x) =>
   ~26.7 chunks/s => the **whole ring fills in ~11 s**, trailing the ~6 s terrain pace as the docs
   intend; ~3.6 expected spawns/tick (~1 ms tree/rock building) stays under `PropBudgetMs` 3.

Deliberately NOT done (Part 4 of the plan): tying prop BoxColliders to the terrain collider ring.
At the 1/1000 density the extend-from-ring-4->ring-10 adds only ~5-8k static bodies (~1% of the
~450k old worst case that 1di removed), and keeping every spawned prop collidable preserves
chopping/mining across the whole ring it now reaches.

- Assets\Scripts\World\Streaming\WorldStreamer.Props.cs â€” `SyncPropRing(centre, near)` ring floor
  `Mathf.Max(PropRingRadius, near + 1)`; `PropTilesPerTick 120 -> 1800`; doc blocks updated.
- Assets\Scripts\World\Streaming\WorldStreamer.cs â€” call site `SyncPropRing(centre, near)`;
  `PropRingRadius` default 4 -> 9 + tooltip (floor semantics).

### 1en-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). `SyncPropRing` has one call site
  (WorldStreamer.cs) and it now passes `near`; the effective ring is `max(9, 9+1) = 10` = 441
  chunks; chunk-op count unchanged (forest pop-in boundary now sits exactly on the real/far ring
  seam at 300-330 m where terrain turns into decimated far cells â€” the last visible pop-in edge).
  Collider behavior untouched (props keep full colliders, so chop/rock targets stay hit-able at the
  new range).
- Play-test (pending, Unity): fresh Play on the test platform â€” trees/stones must fill the WHOLE
  0-300 m disc (not just the old 120 m), arriving a few seconds behind the terrain fill (not ~2
  minutes), tree+rock ~1-in-1000 density visibly unchanged when close, and props on the ring
  8-10 keep chunks appear/disappear with chunk streaming (no props on the far rim cells beyond). No
  new hitching during the ~11 s fill; axe/pickaxe still hits props out to the ring edge. If the
  fill still looks slow, the next lever is `PropTilesPerTick` (each tick remains ~1 ms) â€” or Part 4
  (prop colliders follow the terrain collider ring) if the body count ever matters.

## 1em. Real-ring dispatch starvation â€” finalized chunks re-dispatched forever, locking the near ring at a ~24-chunk bubble ("no chunks within 300 m except the closest")

New user signature: "within 300 from player spawnpoint there were no chunk spawn beside the one
closest to player". After 1el fixed the far shell, this is the last hole: the far shell fills 300 mâ†’2
km, but the real chunk ring inside 300 m never materialized beyond a small patch around the player.
Diagnosed by grep + reread (rule 3, no build):

- The far shell only covers rings >= 10 (near 9 + 1), so inside 300 m the ground is 100% real
  `ChunkObject`s. The far shell rendered fine and there were NO background-generation warnings (user
  confirmed), which isolated the failure to the real-chunk DISPATCH, not generation.
- Root cause in `DispatchPending` (`Assets\Scripts\World\Streaming\WorldStreamer.Streaming.cs`, code
  ancient since the 90afbbb/1ea era): the dispatch loop checked only `_chunksInFlight`, never
  `_loadedChunks`, and the trailing cleanup dropped an entry only when it was BOTH
  `_pendingChunks.Contains(c)` AND `_loadedChunks.ContainsKey(c)`. But `FinalizeChunks` removes the
  pending mark at finalize (`WorldStreamer.Mesh.cs`), so a finalized chunk is `loaded AND NOT pending`
  â€” the cleanup could never fire, the chunk stayed in `_chunkDispatchOrder`, and dispatch re-queued
  its regeneration every poll. Because dispatch is nearest-first and `MaxInFlight` (24) saturated
  with the nearest chunks before the scan passed index ~24, the freed slots were ALWAYS refilled by
  the same nearest chunks re-generating (work `FinalizeChunks` then discarded via its
  already-loaded `continue`), so the OUTER rings never got a slot â€” a permanent ~24-chunk bubble.
- The far shell was immune (its `FarConsiderCell` skips `_farSectors.ContainsKey` completed cells),
  which is why 300 mâ†’2 km rendered while the 0-300 m disc stayed empty. This also re-explains the
  persistent 1efâ†’1ek "holes / square empty ring / not fully loaded" family: those passes polished
  the far shell while the real-ring dispatch flaw sat underneath, and the docs' "fills the full near
  ring" claim was never achieved live.

- `Assets\Scripts\World\Streaming\WorldStreamer.Streaming.cs` â€” `DispatchPending` only:
  - dispatch loop: `if (_loadedChunks.ContainsKey(tc)) continue;` (never re-generate a materialized
    chunk);
  - trailing cleanup now drops ALL loaded chunks from `_chunkDispatchOrder` (plus their pending
    marks) instead of only pending-and-loaded.

### 1em-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). Flow re-derived: poll 1 dispatches
  the nearest 24 in flight; each finalize frees slots that the next poll's scan now passes outward to
  the still-unloaded rings (loaded/ in-flight candidates skipped, order shrinks via the cleanup), so
  the ring fills to completion nearest-first and ends with `_chunkDispatchOrder == 0`, in-flight 0,
  ready empty â€” the 1ee idle gate then truly idles (previously the perpetual re-generation churn kept
  `working` true forever, silently burning CPU every poll). No public API/signature change; far shell,
  `EnqueueChunkIfNeeded`, `StreamAround` hysteresis, budgets untouched. `FinalizeChunks`'s
  already-loaded `continue` remains as a cheap belt-and-braces for any straggler.
- Cost note: total generated chunks unchanged (still 361 in the near ring + far cells); the waste of
  endlessly regenerating the same ~24 chunks is eliminated.
- Play-test (pending, Unity): fresh Play on the test platform â€” the real ground must now fill the
  WHOLE 0-300 m disc (361 chunks, FPS overlay `chunks` should climb to ~361 then settle; `far cells`
  ~1,400), seamless into the far shell at ring 10 (300 m) with no gap and no z-fight (active-shadow
  unchanged), and the loaded-chunk count should STOP near the ring + keep (nothing regenerating in
  place, idle cost ~0 as the docs intend). If `chunks` still stalls below ~360, re-check the
  finalize budget under editor load (THINKING Â§1em H4).

## 1el. Far-shell block-coordinate fix â€” span-3/6 cells were rendered 3x/6x further out, leaving a permanent empty ring past the rim

User play-test after 1ek: "the chunk ring have an offset of 300 x/z there is no chunks loadede in
there". Clarified by reread (rule 3, no CLI build): the terrain ring visible at ~300 m is the CORRECT
span-1 rim band (rings 10-14), and everything beyond it was a permanent void â€” the far shell
never filled the mid-ground. This is the same "holes / not fully loading / empty interior" family of
reports behind 1efâ†’1ek; those passes fixed the fill order / step ladder / starvation, but the root
cause below was never caught.

Root cause: **`FarCell.X/Z` changed meaning between call sites.** The owning-cell math
(`FarCellForChunk`, `RequiredFarCell`, `FarCellRings`) treats `FarCell.X/Z` as the block's **min chunk
coordinate** (chunk units â€” the parents are built as `FloorDiv(x,6)*6`, `FloorDiv(x,3)*3`, and the
span-1 rim uses the raw chunk coord, which is provably placed correctly at xÂ·30). But the build +
placement treated the same value as a **block index** and multiplied by the span again:
`BuildFarSector` sampled `cell.X * span + cx` (and `cellTileOriginX = cell.X * span * cs`) while
`CreateFarSector` placed the GO at `cell.X * cell.Span * ChunkData.Size`. Since BOTH the sampled
chunks and the placement used the same over-scale, each misplaced cell rendered *self-consistent
correct terrain* â€” just at the wrong place: every span-3 cell at 3Ã— its true block and every span-6
cell at 6Ã—, so the shell kept a watertight look wherever a cell landed while leaving the intervening
rings empty.

Verified arithmetic (grep + reread): a span-3 block owning chunks 15-17 (world 450 m) was sampled +
drawn at chunk 45 (1,350 m); the nearest visible span-3 strips sat at chunks 45/54/63 (1,350/1,620/
1,890 m â€” the "some loaded from far away" of the 1eg report), span-6 cells landed at ring ~216
(off-view), and the whole 450â†’~1,300 m band stayed **permanently empty**. The rim band (300-450 m)
was the only correct far geometry, matching the user's "ring 300 m offset, nothing beyond".

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs` â€” removed the over-scale at all three
  sites, keeping `cell.X/Z` = block min chunk everywhere:
  - `grids[cz, cx] = BuildFarChunkCorners(new TerrainChunkCoord(cell.X + cx, cell.Z + cz), seed)` (was
    `cell.X * span + cx`).
  - `cellTileOriginX/Z = cell.X/Z * cs` (was `* span * cs`) â€” the cross-seam/boundary world samples now
    match the corrected chunk mapping.
  - `CreateFarSector` position = `cell.X/Z * ChunkSize * Size` (was `cell.X/Z * Span * â€¦`). Span-1 rim
    cells are arithmetically unchanged (Span == 1 makes both formulas identical), so the active-shadow
    band, `RequiredFarCell`/`FarCellForChunk`/`FarCellRings`, the ring walk, budgets, and dispatch
    order need no edits (they were already chunk-min based).

### 1el-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build): every `FarCell` construction now
  follows the chunk-min convention â€” `FarCellRings` (min X = cell.X, max X + Span - 1), the
  suppression parents (`FloorDiv(x,6)*6` / `FloorDiv(x,3)*3`), `FarCellForChunk`, the rim active-shadow
  (`TerrainChunkCoord(cell.X, cell.Z)`), and now the build (`cell.X + cx`), tile origin (`cell.X * cs`)
  and placement (`cell.X * ChunkSize * Size`). Sample-height cache (`SampleHeight`'s `cxi/lx` clamp),
  `BuildFarChunkCorners`, bounds, winding, cross-seam `WorldHeight`, and budgets unchanged. No public
  API/signature change. Watertight re-derivation: adjacent block-min cells Ã— adjacent corners land on
  shared world coords â†’ heights coincide on the uniform 3 m lattice (sibling watertightness reasoning
  of Â§1ej still holds; band B/C suppression boundaries unchanged: span-3 â‰¤ ring 35, span-6 â‰¥ 36).
- Cost note: unchanged â€” same cell count (~1,400) and per-cell vertex counts; the fill now actually
  covers the whole visible disc instead of scattering strips.
- Play-test (pending, Unity): enter Play fresh (full recompile) on the test platform â€” the far ground
  must read as ONE continuous surface from the real ring edge (~270 m) out to the ~2 km horizon with
  **no empty band at any offset**: rim (300-450 m), span-3 mid-band (450â†’~1,100 m), span-6 outer
  (â†’~2 km) all present; no thin lines/cracks at cell or band boundaries; the `far cells` overlay
  counter should settle near ~1,400; standing/moving FPS unchanged. If any residual hole remains after
  this pass, the geometry is provably band-complete, so re-check the rim active-shadow interaction with
  the always-loaded ring-10 hysteresis real chunks (THINKING Â§1el H3).

## 1ej. Far shell â€” permanent "thin lines along every chunk edge" (step-ladder T-junction cracks) â†’ uniform 3 m lattice

User play-test after 1ei reported the far ground beyond the real ring with "spaces" around the loaded
chunks, and cells even further out with "gaps with every chunks". Clarified: the gaps are **permanent**
(standing still 10-15 s never fills them) and look like **thin lines/cracks along every 30 m chunk-edge**
â€” a systematic boundary artifact, not fill latency. Verified by grep + reread (rule 3, no CLI build);
Unity play-test pending.

Root cause: the far shell's decimation **step ladder** (`FarSectorStep`, WorldStreamer.FarShell.cs)
picked each cell's step from its own far-ring distance (span-3 â†’ 3/6/9 by rings â‰¤21/â‰¤27, span-6 â†’
12/15 by â‰¤47). Wherever adjacent cells used DIFFERENT steps, their shared 30 m-aligned edge was a
**T-junction**: the coarse cell's edge-chord skipped the fine cell's intermediate vertices, whose noise
heights sit off the chord â†’ a permanent open V-crack along the whole row. Same-step siblings were proven
watertight (identical world-anchored corner grids, byte-identical slotâ†’world math to the real chunk
builder), so the ladder itself was the only seam source â€” Â§1ef H5 had accepted these as "sub-pixel",
now REOPENED by the report.

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs` â€” `FarSectorStep(span, maxRing)` now
  returns the uniform **3 m** step for every cell (span/maxRing kept for call-site stability). Every
  cell of every span lives on the one shared world-aligned lattice (rim 11Ã—11, span-3 31Ã—31, span-6
  61Ã—61), so adjacent cells of every span share exact coincident edge rows â€” no T-junction cracks by
  construction; the rim/real Lod2 step-3 seam stays exact. Header + method docs updated (ladder wording,
  verts/axis figures 31/16/11 and 16/13 removed). Budgets, horizon-first dispatch, active-shadow ring,
  bake switch (disabled) untouched.

### 1ej-status
- Implemented; verified by grep + reread: no remaining pre-1ej ladder values anywhere in
  `WorldStreamer.FarShell.cs` (3/6/9/12/15 only appear inside the "what was removed" doc text);
  `FarSectorStep` has exactly one caller (`BuildFarSector`, FarShell.cs:409) and a constant body â€”
  no caller signature change; `span`/`maxRing` still thread-safe captured values so the worker path is
  unchanged; no public members touched.
- Cost note: uniform step 3 raises the far-shell vertex total to ~2.2 M (span-6 cells 16/13 â†’ 61Ã—61
  verts) â€” negligible vs the ~1,400 draw calls that dominate; background fill time rises a little
  (more verts/cell), still horizon-first over the 2.5 ms/poll finalize budget.
- Play-test (pending, Unity): enter Play fresh (full recompile), walk the far ground â€” the shell must
  read as ONE continuous surface with **no thin lines/cracks anywhere** from the rim junction (270 m)
  out to the horizon; the ring band around the player must show no gaps; confirm fill still closes
  horizon-first and standing/moving FPS is unchanged. If thin lines STILL appear after this pass, they
  cannot be far-shell internal seams (geometry is provably watertight) â€” next check is per-chunk
  save/noise divergence between neighboring corner grids, per THINKING Â§1ej H3.

## 1ek. Far shell "square ring, inner never fills" = horizon-first dispatch starving the near cells â†’ near-first + 96 in-flight + cross-seam normals

User play-test after 1ej reported the far shell "loads as a **square ring**, not loading the inner
except for the chunks around the player spot", with "some squares stay **empty forever**"; the ring is
the boundary at ~270 m around the player. Clarified: these are **missing cells** (not the crease lines
1ej addressed â€” those also persist as "still split into grid"). Verified by grep + reread (rule 3, no
CLI build); Unity play-test pending.

Root cause: `FarShellTick` walks the far rings into `_farPending` closest-first but dispatched it in
**reverse (horizon-first)**, capped at 48 in-flight â€” so the farthest, and after 1ej the heaviest,
outer span-6 cells (36 chunk corner grids + 3,721 verts each) grabbed every flight slot every poll,
while the near rim/span-3 cells at the front of the list were only dispatched last. Under movement (and
the Editor) they starved **permanently** â†’ the square outer ring renders while the interior/rim never
appears. Same mechanism behind the long-running "not fully loading" complaints; 1ej's uniform 3 m step
(8-14Ã— heavier cells) made it the dominant symptom. Also remains from before: far-cell edge **normals**
were one-sided (clamped at the cell edge), so the two coincident vertices of every neighbor pair got
different normals â†’ a permanent lighting crease ("split into grid / thin lines at every chunk edge",
non-geometric, so 1ej couldn't remove it).

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs`:
  - **Near-first dispatch** â€” the step-(3) loop now iterates `_farPending` FORWARD (closest-first), so
    the rim/near cells around the player close immediately and the distant fringe fills a moment later
    (reverses 1eg's horizon-first order; the pending walk was already closest-first). Header + dispatch
    comments updated.
  - **`MaxFarInFlight` 48 â†’ 96** â€” parallelizes the heavy worker builds; finalize stays 2.5 ms/16 cap
    (no main-thread change).
  - **Cross-seam normals** â€” in `BuildFarSector`, when a slope neighbor falls outside the cell
    (`gx == 0/axis-1`, `gz == 0/axis-1`), the missing side is sampled directly from
    `TerrainNoiseGenerator.GetHeight(seed, wx Â± step, wz)`. The far band has no save mods (collider
    ring 8 < rim 10), so direct noise equals the neighbor cell's grid value exactly; both cells now
    compute byte-identical edge normals â†’ no crease at far-cell boundaries or the rim/real junction.
    Interiors keep the grid read (only boundary rows/cols pay â‰¤2 extra noise calls).

### 1ek-status
- Implemented; verified by grep + reread: dispatch loop is the only forward iteration (no other
  reverse-order caller); `MaxFarInFlight` referenced only by the dispatch loop; `WorldHeight` helper is
  local to `BuildFarSector` and used only for the out-of-cell seam samples (grid read kept for
  interiors); no public members touched, no caller signature change; docs in the same pass
  (game-design Â§2.5, PROGRESS Â§1ek, THINKING Â§1ek H1-H4).
- Design notes: near-first is the deliberate 1eg reversal â€” surroundings now fill instantly and the
  distant fringe shows a transient, out-of-view edge gap on fresh load/teleport (strictly better than a
  permanent void around the player); 96 worker slots don't touch the main thread's 2.5 ms finalize cap.
- Play-test (pending, Unity): enter Play fresh (full recompile) â€” the far shell must fill OUT from the
  player with **no empty ring/interior**: the rim at 270 m and the mid band appear within the first
  second, distant fringe a moment later; confirm no cell-boundary fine lines remain (cross-seam
  normals) and standing/moving FPS is unchanged. If a void still reads "empty interior" after
  near-first, next check is the 2.5 ms finalize budget starving creation in the Editor or a
  `FarCellForChunk` mapping gap (THINKING Â§1ek verdict).

## 1ei. Far shell "only visible from below" + moving-load hitches

User play-test of the 1eh bake-disable fix **still** reported the far chunks beyond the real ring as
"can only see from the surface below, can't see from the upper face", plus "chunks loading when the
player moves is too laggy". Verified by grep + reread (rule 3, no CLI build); Unity play-test pending.

Root-cause work: the far-cell winding (`BuildFarSector`, WorldStreamer.FarShell.cs) is **byte-identical**
to the real-chunk LOD meshes (`BuildLodChild`, ChunkObject.cs) and the far cells share the real
`GroundMaterial` â€” so the shape is provably correct and the missing-upper-face symptom is a rendering/
culling artifact (or stale Editor compile), not the mesh geometry. The bake disable had already restored
the dynamic per-cell meshes, so this pass makes the far cells immune to winding/culling by construction
and cuts the move-time load.

- `Assets\Shaders\TerrainLayered.shader` â€” new `_Cull` property (0=Off/1=Front/2=Back, default Back);
  all three passes (ForwardLit/ShadowCaster/DepthOnly) now `Cull [_Cull]` so one material instance can
  flip to double-sided without duplicating passes.
- `Assets\Scripts\Core\GameBootstrap.cs` â€” after creating `GroundMaterial`, a sibling
  `FarGroundMaterial` is built (copies GroundMaterial, `_Cull` = 0) and assigned to the streamer.
- `Assets\Scripts\World\Streaming\WorldStreamer.cs` â€” new `public Material FarGroundMaterial;`;
  `MaxColliderCooksPerPoll` 4 â†’ 2 (synchronous PhysX cooks now spread over an extra poll via the
  existing `_collidersDirty` resume).
- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs` â€” `CreateFarSector` assigns
  `FarGroundMaterial ?? GroundMaterial` and `shadowCastingMode = ShadowCastingMode.Off` (the ~1,400 far
  cells stop drawing into the sun's shadow map â€” a large per-frame cut, no gameplay value beyond 270 m).

### 1ei-status
- Implemented; verified by grep + reread: `_Cull` defaults 2 and is referenced by all three passes
  (`Cull [_Cull]` Ã—3); `FarGroundMaterial` declared once on WorldStreamer, assigned once in GameBootstrap
  (fallback `?? GroundMaterial` keeps far cells rendering even if never assigned), consumed once in
  `CreateFarSector`; `ShadowCastingMode.Off` only on far cells (real chunks via ChunkObject/LOD still
  cast); `MaxColliderCooksPerPoll = 2` used only by `ReconcileCollidersIfChanged`. Public API adds one
  optional field â€” no consumer signature break.
- Design acceptances (watch): far cells now draw backfaces too (double-sided) â€” a small GPU vertex cost
  on decimated meshes, no z-fighting since backfaces sit behind the correct front faces; far cells no
  longer cast shadows, so distant terrain is lit but casts no shadow onto itself (already true of far
  ground â€” no shadow receiver elsewhere beyond the real ring); collider ring takes ~2Ã— polls to fully
  cook after a large jump.
- Play-test (pending, Unity): enter Play fresh so the Editor fully recompiles, then walk out â€” the far
  ground must be visible **from above** from the rim junction (270 m) to the ~2 km horizon; standing FPS
  and moving hitches should improve (far shadows off + collider cooks spread); confirm real chunks still
  cast shadows and look unchanged (Cull Back kept), and the far shell fills horizon-first as before.
  If the far cells STILL hide from above after this pass, pause in Play and inspect a `FarCell_*`
  MeshRenderer (enabled? material Cull value?) to distinguish "not rendering" vs "culled".

## 1eh. Performance pass â€” far-finalize time budget, far-shell static bake, collider ring 8â†’7, platform + physics trims

User play-test after 1ef/1eg: "too lag to play" â€” slow even standing still, hitches while moving
(Editor session, no build). Verified by grep + reread (rule 3, no CLI build); Unity play-test pending.
Diagnosis: (a) 1eg raised far-cell finalize to 16 GameObjects + mesh uploads/poll on the main thread â€”
a per-poll hitch while the shell fills and while walking; (b) steady-state cost in physics bodies
(289 chunk MeshColliders + the per-branch tree colliders the user chose to keep as-is) and ~1,400
far-shell draw calls; (c) platform QA lane (~60 magic-pedestal TMP labels) + 7 livestock on CCD.

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs`:
  - **Finalize is now time-budgeted** (`FarFinalizeBudgetMs` = 2.5 ms/poll, hard cap `MaxFarFinalizePerPoll`
    16 kept): the 1eg throughput stays, but a single poll never spikes the main thread on
    GameObject/mesh creation (mirrors the real-chunk adaptive budget). Fill now ~3-8 s (was 1.5-5).
  - **Static bake** (`TryBakeFarShell`): once the shell is fully settled for `FarSettlePollsBeforeBake`
    (20) polls, all span-3/6 cells are moved under one root and merged via
    `StaticBatchingUtility.Combine` â€” the ~1,400 per-cell renderers collapse to a handful of batched
    sub-meshes (Unity auto-splits at 65k). Span-1 rim cells stay dynamic (they own the active shadow
    and must be able to hide under a loaded real chunk). Baked cells are skipped by the removal scan
    (never torn out of the combined mesh â€” `StaticBatchingUtility` cannot re-bake the same GOs); they
    are retained on shrink/radius-change until `ClearFarShell` wipes the batch wholesale (and does NOT
    return the combined mesh to the pooled-mesh cache â€” a pooled combined mesh would corrupt chunk
    reuse). New cells after the bake stay dynamic.
    **DISABLED (see 1eh-status below):** the combined batch rendered far meshes only from below, so
    every far cell is back on its own dynamic mesh.
- `Assets\Scripts\World\Streaming\WorldStreamer.cs` â€” `ColliderRingRadius` **8 â†’ 7**: 289 â†’ 225 chunk
  MeshColliders swept by every `CharacterController.Move`; still below `NearRingRadius` 9 (collider-on-
  real-chunk constraint intact) and larger than every gameplay probe range.
- `Assets\Scripts\Opt\NewWorldTestGround.cs` â€” `EnableMagicModels` default **false** (QA lane off:
  ~60 world TMP labels + pedestal cubes gone from the Editor session; flip on to inspect).
- `Assets\Scripts\Livestock\Livestock.cs` â€” livestock Rigidbody CCD `ContinuousDynamic` â†’ `Discrete`
  (7 bodies; low-speed animals don't need CCD).

### 1eh-status
- **Static bake disabled (follow-up fix):** user play-test of `c551239`: after the shell settled ~1-2 s,
  the whole far shell (~250 m out) was visible **only from below the surface** â€” the combined
  `StaticBatchingUtility.Combine` batch had broken far-mesh rendering. Fix: `FarBakeEnabled = false`
  (const switch) â€” `TryBakeFarShell` and the settle-hook are now gated, so every far cell again renders
  as its own dynamic mesh (exactly the 1ef/1eg state the user saw as complete from above). All other
  1eh wins (finalize budget, collider ring 7, pedestals off, CCD) kept. Root cause of the combined-mesh
  artifact is NOT yet found â€” re-enable the switch only after a Unity-side experiment (see THINKING Â§1eh).
- Implemented; verified by grep + reread (rule 3 â€” no CLI build): `ColliderRingRadius` used only in
  `ReconcileCollidersIfChanged` (want = ring box test; 7 < near 9 âœ“); `EnableMagicModels` gate at
  `NewWorldTestGround` coroutine; livestock has no other CCD usage (FlyingCrane keeps CCD â€” separate
  system); far-finalize budget + bake re-read end-to-end (removal-scan skip, ClearFarShell order:
  baked batch first, no pool release of combined meshes, then dynamic sectors; epoch/reset paths
  reset `_farBaked/_farBakedCells/_farIdlePolls`). New symbols have no duplicates in the partial class.
  Bake-off guards re-read: `FarBakeEnabled` early-return in `TryBakeFarShell` + the (5) poll hook gate
  sits at the end of the poll method (nothing after it is skipped).
- Design acceptances (watch): the fill takes a few seconds (~3-8 s); with the bake **disabled the far
  shell is back at ~1,400 draw calls** (the standing-still/perf win of the combined mesh is deferred);
  the finish still settles then nothing further happens (no bake spike).
- Play-test (pending, Unity): with the FPS overlay â€” standing-still FPS and per-frame ms after the
  shell has filled; walking a long line for hitches (finalize budget should smooth the fill
  and leading-edge); confirm the far ground is visible **from above** from the rim junction out to the
  ~2 km horizon, no void ring around the player and no hole away from a walking path; confirm the far
  cells counter still reaches ~1,400 then settles, and the horizon band is complete. Raise/lower Render
  Distance â€” shell grows/shrinks dynamically (no baked interior to retain). Confirm no compile error in
  Unity (rule 3).

## 1eg. Far shell fills fast, horizon-first + horizon tint softened (follow-up to 1ef)

User play-test of 1ef: "the map visual is not fully loading, i can see some loaded from far away but
they're not complete". Root cause: the far shell filled at **3 finalized cells/poll (12 in flight)** â‰ˆ
60 cells/s â†’ the ~1,400-cell shell took ~20-25 s, and because cells are ring-walked + dispatched
closest-first, the **farthest (horizon) cells were created LAST** â€” exactly the patchy distant view
reported. It was a 1ef design acceptance, now rejected. Verified by grep + reread (rule 3, no CLI
build); Unity play-test is pending.

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs` â€” budgets: `MaxFarInFlight` 12â†’48,
  `MaxFarFinalizePerPoll` 3â†’16 (â‰ˆ320-960 cell meshes/s at 60 fps â†’ initial ~1,400-cell fill ~1.5-5 s),
  `MaxFarUnloadsPerPoll` 24â†’32. Dispatch (step (3)) now iterates the pending list **in reverse** so the
  farthest rings finalize FIRST (horizon-first fill); the near rings follow within seconds and are
  covered meanwhile by the real chunks' hysteresis ring 10 â€” no hole at the player's feet. Header text
  updated to the new fill figures.
- `Assets\Shaders\TerrainLayered.shader` â€” horizon tonal lift narrowed + weakened so the far band no
  longer reads as "missing geometry": `_HorizonStart` 1400â†’1600, `_HorizonEnd` 2100â†’2050 (removes the
  full-tint plateau â€” the shell edge is ~2,070 m), peak blend Ã—0.6. The terrain material is built at
  runtime from shader defaults (`GameBootstrap.cs:99` `Shader.Find("NewWorld/TerrainLayered")`), so no
  serialized material overrides exist to chase.
- Docs (same pass, rule 2): `game-design.md` Â§2.5 budgets/fill-time + horizon band; `THINKING.md` Â§1eg
  â€” 1ef's "slow fill is acceptable" story reopened (H1 confirmed) + horizon wash (H2), real-coverage
  hole REJECTED (H3) and re-checkable via the `far cells` counter.

### 1eg-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build). Budget constants have no other call
  sites; the reversed dispatch loop was re-read (in-flight mark added before `QueueUserWorkItem`,
  `_farPending` rebuilt each poll so reverse order is safe, epoch/seed still captured by value); shader
  defaults edited at their only property definitions; grep confirms no serialized `_HorizonStart` /
  `_HorizonEnd` / `_HorizonColor` overrides in assets.
- Play-test (pending, Unity): boot on the test platform with `EnableFpsStats` â€” the overlay's `far
  cells` counter should climb to ~1,400 in ~1.5-4 s (not ~20-25 s); glance toward the horizon while it
  fills â€” the distant band closes first and a complete ring is visible within seconds; the far ground
  should read as terrain under a soft lift, not washed-out/sky; walk a long straight line â€” no holes at
  the leading edge or the real-ring (9) â†” shell (10) junction; raise/lower Render Distance in settings
  â€” shell grows/shrinks without blanks. If `far cells` ever freezes well below ~1,400, that is a REAL
  coverage hole â€” reopen THINKING Â§1eg H3.

## 1ef. Far shell render â€” deep 2 km view (real near ring + background coarse sectors) + horizon tint

The big-crisp view pass: real full-fidelity chunks only to a near ring (9), the camera far plane to
2200 m, and a background-generated **far shell** of coarse cell meshes extending the ground out to the
67-chunk render radius (~2 km) â€” with no fog, so the near/mid terrain stays fully crisp. Verified by
grep + reread (rule 3, no CLI build); Unity play-test is pending.

- `Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs` â€” **new**, the whole far shell:
  - `FarCell {X,Z,Span}` (span-1 rim / span-3 band B / span-6 band C) + `FarMeshData` thread handoff.
  - `RequiredFarCell` â€” the single generate AND retain predicate: a coarser required parent suppresses
    its finer children (hierarchical suppression), so every annulus chunk ring 10..69 belongs to
    exactly one cell. Chunk-coord positive/negative indexing via `FloorDiv` so the grid stays aligned.
  - `FarCellForChunk` maps one chunk to its owning cell (span-6 parent â†’ span-3 parent â†’ rim cell).
    Deliberately NO loaded-check here: rim cells also spawn inactive under loaded ring-10 real chunks.
  - `FarShellTick` per poll: (1) active-shadow sync â€” span-1 cells toggle `active = !loadedChunk`, so
    the real ring unload hands straight to the shell in the SAME poll (zero hole, zero z-fight) â€” plus
    a removal scan that keeps a rim cell alive while its real chunk is queued/in-flight (no
    approach-edge hole), capped at 24/poll with a `_farUnloadBacklog` flag; (2) ring walk near+1..keep
    into a deduped pending list; (3) dispatch to the ThreadPool (`MaxFarInFlight` 12) with seed/epoch/
    maxRing captured by value; (4) finalize â‰¤3/poll, dropping stale epochs and no-longer-required
    cells before creating the GameObject.
  - `BuildFarSector` â€” decimated grid mesh from the SAME per-chunk corner grids the real chunks use
    (`BuildFarChunkCorners`: `ChunkSaveManager.TryLoadChunk` stamps + NaN-seeded noise fill, exact
    duplication of `BuildOrLoadChunk` because that builder couples the grid to the 900-tile pass);
    step by span/maxRing (rim 3; B 3/6/9; C 12/15 â†’ 11/31/16/11/16/13 verts/axis); central-difference
    slope normals; memoized `TerrainBandColor` so far terrain keeps the strata read.
  - `CreateFarSector` (static GO + pooled mesh + `GroundMaterial`; span-1 starts inactive under a
    loaded real chunk) / `DestroyFarSector` (pooled-mesh release) / `EnsureFarRoot` /
    `ClearFarShell` (epoch bump + full wipe, called from world/save reset).
  - `public int FarSectorCount` â€” powers the perf readout's new `far cells` line.
- `Assets\Scripts\World\Streaming\WorldStreamer.cs` â€” `NearRingRadius = 9` field (`[Header("Far Shell")]`,
  tooltip: keep â‰¥ `ColliderRingRadius` 8); `_lastStreamCentre` sentinel (`int.MinValue` so the first
  poll always runs even at the world origin); `Update` derives `view` (render radius) + `near` and
  passes them to `StreamAround(centre, near)` then `FarShellTick(centre, view, near)`; the 1ee idle
  gate's `working` flag now also covers the far queues so an initial fill/shrink keeps the poll alive
  only until it settles.
- `Assets\Scripts\World\Streaming\WorldStreamer.Streaming.cs` â€” `StreamAround` summary now documents
  that `radius` is the NEAR real ring (the far shell owns the ground beyond); `ResetTerrainSaves`
  calls `ClearFarShell()` first (the shell's cells were sampled from the old saves).
- `Assets\Scripts\Core\GameBootstrap.cs` â€” `rd.Radius = 67` (2,010 m; real chunks only to ring 9;
  `MaxRadius` 160 kept).
- `Assets\Scripts\Player\PlayerController.Camera.cs` â€” `CameraFarPlane = 2200f` applied in
  `CreateCamera` + `SetupPlayerCamera` (clips clean past radius-73 shell edge).
- `Assets\Shaders\TerrainLayered.shader` â€” **horizon tonal lift without fog**: `_HorizonColor`
  (0.78,0.83,0.90), `_HorizonStart` 1400, `_HorizonEnd` 2100; frag lerps toward the tint by horizontal
  distance from the camera BEFORE `MixFog`, so the outermost shell reads as atmosphere while near/mid
  terrain stays fully crisp.
- `Assets\Scripts\Opt\NewWorldTestGround.cs` â€” perf readout adds `far cells {n}` (via
  `streamer.FarSectorCount`).

### 1ef-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build): grep `FarShellTick`/`FarSectorCount`/
  `NearRingRadius`/`ClearFarShell`/`StreamAround` â†’ all call sites live and signatures match; the
  `WorldStreamer.Update` pipeline re-read (view/near, far `working` flags, `FarShellTick` after
  `FinalizeChunks`); `StreamAround` kept at 1 signature (radius = near) with `ResetTerrainSaves` â†’
  `ClearFarShell()`; `BuildFarChunkCorners` matches `BuildOrLoadChunk` corner semantics (NaN seed,
  IsSaneHeight-gated stamps, world-coord noise fill); new structs/methods have no duplicate symbols in
  the partial class; Camera/shader/bootstrap/test-ground edits re-read. No build/compile run.
- Design acceptances (play-test should watch for): the initial far fill takes ~20-25 s at 3
  finalize/poll (12 in flight) while the player moves; micro-seams/T-junctions between adjacent
  different-step cells sit at â‰¥600 m (sub-pixel); the horizon tint band intentionally softens the
  outermost ~700 m only.
- Play-test (pending, Unity): boot on the test platform with `EnableFpsStats` â€” per-frame ms should
  settle flat after the shell fills; the overlay now shows `far cells` climbing to ~1,400 then
  settling; walk toward any direction and check NO hole/z-fight where the real ring (ring 9) meets the
  shell (ring 10); run off the real ring's edge â€” the shell mesh should already be there (no blank);
  look toward the horizon â€” terrain extends ~2 km with a soft sky-blue lift and no fog on close
  terrain; raise/lower Render Distance in settings and confirm the shell grows/shrinks without holes;
  dig with pickaxe near the real-ring edge â€” deformation still works and never touches the shell.
  Confirm no compile error in Unity (rule 3).
- Follow-up fix (new commit after `ac5bb2d`): that commit's `grids` declaration
  `float[span, span][,]` was itself **invalid C#** (array size expressions are only legal in the
  `new` expression, never in a declaration type â€” Unity would fail to compile the file again). The
  correct container is `float[,][,]` â€” a rank-2 array of `float[,]` corner grids
  (`WorldStreamer.FarShell.cs:363`, matching `grids[cz, cx]` writes and `grids[czi, cxi][lx, lz]`
  reads). Grep + reread confirmed the fix is the only diff; no behavior change; no build (rule 3).

## 1ee. CPU baseline cleanups â€” idle streaming zero-cost + player caches + HUD repaint fix

The standing-still cost sweep after 1ea/1e6: kill the remaining per-frame CPU work on the test platform
without changing any gameplay. Verified by grep + reread (rule 3, no CLI build).

- `Assets\Scripts\World\Streaming\WorldStreamer.cs` â€” **idle-poll gate**: `Update` keeps its 0.05 s poll
  beat but the whole pipeline (`StreamAround` / `DispatchPending` / `FinalizeChunks` /
  `ReconcileCollidersIfChanged` / `SyncPropRing` / `StepChunkProps`) now early-outs when the focus is in
  the same chunk centre as the last poll, nothing re-armed `_worldDirty`, and no chunk is queued /
  in-flight / ready-to-finalize. `SetFocus` re-arms the flag. After the initial fill, an idle player pays
  a timer check + a few comparisons per poll; walking still streams normally the moment the focus crosses
  a chunk boundary (a 30 m chunk box).
- `Assets\Scripts\UI\UIManager.HUD.cs` â€” **UpdateTimeText repaint fix**: the guard
  `Mathf.Approximately(hour, _lastTimeHour)` could never match (hour advances every frame), so TMP
  rebuilt + repainted the day/time label every frame. Now quantizes `hour` to its displayed 0.01 h step
  (`Mathf.Round(hour*100)/100`) before storing + comparing â†’ the label repaints only when the shown text
  actually changes.
- Player root component-cache sweep (1dr convention): every `GetComponent<CombatController>()` on the
  player now routes through the existing lazy `CombatCached` property (`PlayerController.cs:41`) â€”
  `PlayerController.Interactions.cs` (pending-rig, fists, dual-mode, cancel-charge, auto-arm, RMB block,
  two-hand X), `PlayerController.Combat.cs` (EnsureFists guard, ReApplyWeaponPose), `PlayerController.
  Animation.cs` (model-reload rig capture). All were on the SAME player root the cache already covers.
  `WeaponRigBuilder` / UI / skill / AI `GetComponent` calls are on other objects and stay untouched.
- `PlayerController.cs` + `PlayerController.Interactions.cs` â€” Tab open/close no longer runs a per-press
  `Object.FindAnyObjectByType<CharacterInfoUI>()`; the cached `CharacterInfoRef` property re-finds itself
  only if the UI object was destroyed (Unity `==` null on destroyed objects covers it).
- `Assets\Scripts\Opt\NewWorldTestGround.cs` â€” `EnableFpsStats` defaults **true** so the 1ea/1ee perf
  baselines are readable on the test platform without a manual tick; flippable off in the Inspector.

### 1ee-status
- Implemented; verified by grep + reread (rule 3 â€” no CLI build): grep `GetComponent<CombatController>` in
  `Assets\Scripts\Player` â†’ exactly ONE hit (the `CombatCached` initializer itself â€” every call site
  converted); grep `FindAnyObjectByType<CharacterInfoUI>` â†’ ONE hit (the `CharacterInfoRef` initializer);
  `WorldStreamer.Update` re-read (idle gate, `_worldDirty` on `SetFocus`, `working` includes
  queued/in-flight/ready); `UpdateTimeText` re-read (quantize guard). No public API/signature changed.
- Play-test (pending, Unity): stand still on the test platform with `EnableFpsStats` on â€” per-frame ms
  floor should be flat (no terrain-poll load while idle); the HUD clock should not rebuild text every
  frame; Tab still opens/closes Character Info; fists / dual-wield / RMB block / magic aim / X two-hand
  all still work after the cache swap. Confirm no compile error in Unity (rule 3).

## 1ed. Fix CS0236 compile error in WorldStreamer (field init referencing instance method)

Follow-up fix to the 1ea alloc-free sort: `_dispatchSort = CompareDispatchDistance;` was a method-group
field initializer referencing an instance method â€” illegal C# (error CS0236 on `WorldStreamer.cs:67`,
so Unity could not compile). First attempt replaced it with a lambda in the initializer, but a lambda
that captures `this` in a field initializer is equally illegal (same CS0236). Final fix: declare the
`readonly` field bare and assign it in the constructor (`WorldStreamer() { _dispatchSort =
CompareDispatchDistance; }`) â€” legal, binds the delegate exactly once per instance, still
allocation-free per poll, keeps the per-instance `_dispatchFocus` feed. No behavior change.

### 1ed-status
- Implemented; verified by read-back (`WorldStreamer.cs:64-68` and the `DispatchPending` sort at
  `WorldStreamer.Streaming.cs:104-107` unchanged, so the comparer semantics and the 1ea zero-alloc
  claim hold). Rule 3 still applies â€” no CLI build; this restores Unity compilation.
- Play-test (pending): none â€” compile-only fix; world streaming continues to load closest-chunks-first
  with the idle zero-cost early-out. Re-enter Play mode in Unity to confirm the error is gone.

## 1ec. Rework magic projectiles into voxel cube-clusters (visual revamp, still static)

Follow-on to 1eb. User: "rework the magic projectile model, for example fire ball would be multiple
cube with smaller on stack on the back." Scoped with the user first (all three locked): (1) **all**
shapes get the cube-cluster treatment â€” a **front-leading cube in the school color** with progressively
**smaller, darker cubes stacked behind it** (-Z, bright core fading into a tapering square tail);
(2) built once and **fully static** â€” no per-frame animation, so the 1eb perf win is preserved; (3) the
cluster idea targets the *magic-ball* silhouettes, not the rock summons (the meteor-line Comet keeps its
burning boulder). All inside `SpellCaster.Projectiles.cs` â€” no public API/signature change, so the bench
(`CreateProjectileDisplay`, `NewWorldTestGround.EnableMagicModels`) and turret summons (`DecorateProjectile`)
inherit the new bodies for free. No build/CLI run (rule 3) â€” verified by grep + reread.

- `Assets\Scripts\Combat\Weapons\SpellCaster.Projectiles.cs` â€”
  - New `Cluster(name, shader, color, lead, count, spacing, jitter, fade=0.75, minCube=0.05)` helper:
    leader cube at full school color + `count-1` cubes stacked back at `-i*spacing`, size tapering
    quadratically `Lerp(lead, minCube, t*t)`, each `Lerp(color, black, t*fade)` dark, jittered and
    Z-spun. The old `Orb` helper died â€” the default "fireball/orb" case now returns
    `Cluster("Orb", â€¦, 0.24, 5, 0.10, 0.03)` (0.24 lead â†’ ~0.05 tail, dark through the stack).
  - New `AddTrailingFlecks(root, â€¦)` â€” 2-3 small darker cubes behind any elongated body.
  - Shape-by-shape (all cubes, **no `Sphere` primitives remain on projectiles**):
    - **Sphere/orb** â†’ hot voxel Cluster (above). **Shard** â†’ translucent glass lead chip (45Â° diamond)
      + 2 dimmer glass chips trailing (keeps the frost = translucent glass read).
    - **Splash** â†’ water drop cube + 3 smaller darker cube drops. **Comet** (non-rock) â†’ 3-cube mini
      Cluster core (`0.2, 3, 0.1, 0.02, fade 0.6`) + streak tail; **rockBody** Comet unchanged
      (boulder+chunks+tail â€” deliberately a rock, not a ball). **Missile** â†’ three 2-cube mini
      dart-stacks (`0.12 + 0.07 tail`).
    - **Lance/Spear/Blade/Dart** â†’ existing silhouette + trailing flecks behind the tail.
    - **Bolt/Debris** â†’ structural no-change (already cube chains â€” Bolt tapers 0.17â†’0.05); stale
      "Tumbling"/"spins" doc wording fixed to "Clustered"/static.
  - Grep-verified: `Orb(` has zero remaining call sites; `PrimitiveType.Sphere` no longer appears in
    this file (remaining spheres in Assets/Scripts are bobbers, world props, cutscene eyes, beam/summon
    head, storm FX â€” separate systems, kept).

### 1ec-status
- Implemented; verified by grep + reread (rule 3, no CLI build): grep for `Orb(` â†’ gone; `Cluster(` /
  `AddTrailingFlecks(` call sites all within `SpellCaster.Projectiles.cs`; `PrimitiveType.Sphere` â†’ 0
  hits in the projectiles file, 12 hits elsewhere (all non-projectile and legit). Full file re-read
  after edits (380 â†’ 434 lines): all 11 shape cases present, braces intact, no orphan builders, no
  signature changes (public API untouched). game-design Â§3.8 table rewritten same pass; PROGRESS +
  THINKING updated same pass.
- Play-test (pending, user runs Unity): cast every school from a staff/wand/book â€” Fireball = hot 5-cube
  stack fading dark; frost chip = 3 glass cubes; Stone Shard = rock clump; Water Bolt = drop + 3 cubes;
  catch a Meteor/Comet (rock form untouched) and the light Comet; a bolt, Ice Lance, Shadow Spear, Wind
  Blade, Arcane Missiles, a physical Dart â†’ each reads as its name with the new "bright front stack,
  darker back" silhouette and **still no particles / no animation / no FPS cost**. Magic-model bench
  (`EnableMagicModels`) shows the new bodies; impacts unchanged; the 1ea/1eb FPS gains hold.

## 1eb. Remove magic particles + static projectile bodies (magic FX cost cut)

Follow-on to 1ea. User: "remove the particle effect of magic and make the projectile detail." Scoped
with the user first: the ONLY real magic `ParticleSystem` in the project is the runtime projectile
**exhaust** trail (`SpellCaster.AttachProjectileParticles` â€” cone billboard, up to 700 particles on a
Fireball). Beams/zones/storms/summons/rings are made of primitives (not particles), and the impact
"poof" is pooled cube debris + a terrain dent (not particles) â€” the user chose to **keep** the impact
debris. On projectile detail the user chose: **remove the particles AND kill the per-frame `OrbFx`
scale-pulse/spin on every projectile child, but keep the per-shape bodies** (bolt/shard/debris/comet/
â€¦) so spells stay element-identifiable. All FX helpers were confined to `SpellCaster.Projectiles.cs`
(grep-verified) â†’ a clean deletion. No build/CLI run (rule 3) â€” verified by grep + reread.

- `Assets\Scripts\Combat\Weapons\SpellCaster.Projectiles.cs` â€” deleted `AttachProjectileParticles`
  plus its `EmissionRate`/`StartLifetime`/`StartSpeed`/`StartSize`/`MaxParticles` switches and the
  nested `OrbFx` class + `Mode` enum. `AttachDefaultProjectileVisual` no longer spawns the exhaust
  child; `Orb()` dropped its `mode` parameter and all 13 `AddComponent<OrbFx>()` sites across the
  shape builders (Shard, Debris chunks+root, Bolt, Lance, Spear, Blade, Splash, Comet, Missile, Dart,
  and the default fireball Orb) are gone. Bodies are now **fully static** render-only (no collider, no
  per-frame component) â€” flight costs only the `SpellEffect`.
- `CreateProjectileDisplay` (the test-ground magic-model bench, `NewWorldTestGround`) dropped its
  OrbFx-strip loop â€” the bench and live casts now share the same static body by construction.
- Untouched (audited, see THINKING 1eb): `RangedWeaponBehavior`/`RangedProjectile` arrows (not magic,
  own visual path), cutscene demon smoke (not magic combat), aim previewers (targeting aid), impact
  crater-debris + dent (kept per user).

### 1eb-status
- Implemented; verified by grep + reread (rule 3, no CLI build): grep for `OrbFx`/`AttachProjectileParticles`/
  `EmissionRate(`/`MaxParticles(`/`Particles/Additive` â†’ the only remaining `ParticleSystem` uses are the
  cutscene smoke (`CutsceneManager.EndingDemon.cs`) and the `ObjectPooler` replay guard â€” both legit.
  Full file re-read after edits (581 â†’ 380 lines): no dangling `mode` args, no orphan builders, file
  braces intact. Public API unchanged (deletions were all private/static within the file).
- Play-test (pending, user runs Unity): cast a Fireball, frost chip, lightning bolt, Stone Shard
  debris, a Meteor/Comet, and Arcane Missiles â†’ every projectile still reads as its element but with
  **no exhaust trail and no flicker/bob/spin** in flight; the magic-model bench still shows one
  distinct body per spell; impacts still kick the pooled cube debris + dent; ending demon smoke still
  plays. Expect a small uptick in the bench FPS readout (`EnableFpsStats`) during volleys.

## 1ea. Performance pass â€” render config, streaming maintenance, bench stats (the lag sweep)

User: "it still is too laggy. Can you do more?" A follow-on to the 1e5/1e6 optimization phases. Scoped
with the user first: lag is "everywhere, all the time"; the target profile is **PC / Unity Editor Play
mode**; and on the visual tradeoff question the user chose **"take the FPS"** (SSAO/MSAA/opaque-copy off,
shadows trimmed). Four parts, all verified by grep + reread (rule 3, no CLI build), play-tested via the
new bench overlay. game-design Â§9.2a + PROGRESS + THINKING updated same pass.

### Part A â€” URP render configuration (`Assets\Settings` + `ProjectSettings\QualitySettings.asset`)
The ACTIVE PC config was confirmed: QualitySettings level 1 â†’ `m_CurrentQuality: 1` â†’
`PC_RPAsset.asset` guid `4b83569d` with `PC_Renderer.asset` SSAO ON at full res (Downsample 0).
- `PC_Renderer.asset` â€” SSAO renderer feature `m_Active: 1 â†’ 0`.
- `PC_RPAsset.asset` â€” `m_RequireDepthTexture 1â†’0`, `m_RequireOpaqueTexture 1â†’0` (grep: nothing in the
  project samples `_CameraOpaqueTexture`/`_CameraDepthTexture`, so nothing turns black),
  `m_MSAA 1â†’0`, `m_MainLightShadowmapResolution 2048â†’1024`, `m_ShadowCascadeCount 4â†’2`,
  `m_SoftShadowQuality 3â†’0`, `m_AdditionalLightShadowsSupported 1â†’0`,
  `m_AdditionalLightsShadowmapResolution 2048â†’512`. HDR kept ON (known-good fallback).
- `ProjectSettings\QualitySettings.asset` (PC level) â€” `shadowDistance 40â†’32`.
- Net: no MSAA resolve, no full-res SSAO pass, no opaque copy, half the shadow-atlas work. Stylized
  look intact; distant sun shadows resolve earlier (the accepted trade).

### Part B â€” terrain-streaming CPU (the standing-still costs)
- **B1+B4 â€” collider ring maintenance is now change-driven** (`WorldStreamer.cs`). Previously
  `ReconcileColliders` walked the FULL `_loadedChunks` map every Update regardless of motion. Now
  `ReconcileCollidersIfChanged(centre)` early-outs unless the focus crossed a chunk boundary, a collider
  request changed (`ColliderRequestRegistry.Version` bumped in `Request`/`Release`), or a chunk was
  finalized/unloaded (`NoteChunkSetChanged` wired into `CreateChunkGameObject` + `UnloadChunk`). On a
  real ring crossing a per-poll cook budget (`MaxColliderCooksPerPoll = 4` then; now 2 since 1ei)
  spreads PhysX mesh cooks â€”
  disables apply instantly, excess enables re-flag `_collidersDirty` so the walk resumes next poll.
  An idle, fully-streamed world now pays ZERO per-frame collider maintenance.
- **B2 â€” allocation-free dispatch** (`WorldStreamer.Streaming.cs`). `DispatchPending` gained an early-out
  for an empty queue, replaced the per-poll closure `Sort` with a cached `_dispatchSort` comparer reading
  a `_dispatchFocus` field, and swapped the closure `RemoveAll` for an indexed backward-loop removal.
- **B3 â€” modified-tile lookup is an O(1) set, not a per-load chunk-materialising query** (`Deform.cs`,
  `WorldStreamer.Mesh.cs`, `TerrainChunkMeshData.cs`, `ChunkBuild.cs`). Old `ChunkHasModifiedTiles`
  materialised the full local tile list of every neighbour on every newly-loaded chunk â€” O(chunksÂ²) on a
  stream-in. New: `TerrainChunkMeshData.HadLoadedMods` (set by `ChunkBuild` only when save mods loaded),
  `ReconcileNewlyLoadedChunk(tc, hadLoadedMods)` seeds `_modifiedChunks`, `ApplyHeightEdits` does
  `UnionWith(rebuiltChunks)`, `UnloadChunk` removes, `ResetTerrainSaves` clears, and
  `ChunkHasModifiedTiles(tc)` is now `_modifiedChunks.Contains(tc)`. Un-modded worlds touch nothing extra
  on chunk load.
- **B5 â€” LOD band audit is a rolling burst** (`ChunkLodManager.cs`). Evaluates at most
  `ScanBudget = 1024` chunks per refresh (`_scanCursor` wrap; null roots removed in place); squared
  distances end-to-end (`BandForSq`, cull compare) and the root `MeshRenderer` cached on `ChunkEntry`
  instead of a per-switch `GetComponent`. Band-switch still calls `RefreshLodMeshes()` before showing a
  far band (1e6 deformation-correct behaviour preserved).

### Part C â€” POI cull-candidate scan throttled (`NewWorldSystems.cs`)
The `FindObjectsByType<PointOfInterest>` block (3Ã— per type, only under `IncludePoisAsCullCandidates`)
now runs at most once per `PoiScanInterval = 2` s. Deliberately NOT changed: `EnemyHealthBarHUD`'s 0.5 s
scene sweep (already throttled; fixing it properly is a registry refactor â€” parked in THINKING 1ea H8).

### Part D â€” bench overlay on the test platform (`NewWorldTestGround.cs`)
New `public bool EnableFpsStats` (default off) â†’ `SpawnFpsStats()` builds a TMPro overlay (avg FPS,
frame ms, loaded chunk count, active collider count) refreshed at 4 Hz by `UpdateFpsStats()`, wired into
`RunBenchSpawn`. A/B this pass with a running number.

### 1ea-status
- Implemented; verified by grep + reread (rule 3, no CLI build): every new symbol grepped and call sites
  re-checked (`ReconcileCollidersIfChanged` single Update caller; `ReconcileNewlyLoadedChunk(tc, bool)`
  both callers â€” `FinalizeChunks` + `GenerateChunkSync`; `NoteChunkSetChanged` at `CreateChunkGameObject`
  + `UnloadChunk`; `_modifiedChunks` set ops incl. `ResetTerrainSaves`; `HadLoadedMods` set only in
  `ChunkBuild`; `_dispatchSort`/`_dispatchFocus`; `ScanBudget`/`_scanCursor` wrap incl. null-drop;
  `BandForSq`); no leftover old-signature `ChunkHasModifiedTiles` callers; final asset fields re-read
  (RequireDepth/Opaque 0, MSAA 0, SSAO 0); no shader references `_CameraOpaqueTexture`/
  `_CameraDepthTexture`; `EnableFpsStats` uses the existing `HudCanvas`/`ApplyDefaultFont` patterns.
- Play-test (pending, user runs Unity in Editor Play mode â€” see CONTEXT note in THINKING 1ea about
  editor-side overhead being irreducible):
  - Tick `EnableFpsStats` on NewWorldTestGround; read avg FPS while standing still, sprinting, and
    digging, and compare a fully-streamed vs freshly-loaded world.
  - Walk across a chunk boundary â†’ colliders re-enable gradually (4/poll) but never missing on arrival;
    no one-frame cook hitch.
  - Reload a world with terraformed saves â†’ seam re-stitch still correct around edited chunks.
  - Distant sun shadows resolve earlier (cracks at range = expected 1024/2-cascade trade); picking/
    overlays/terrain colours otherwise unchanged.
  - LOD bands simplify distant chunks on the same rules as before (only the scan cadence changed).

## 1e9. PlayerAnimator â€” upper-body look-pitch direction was inverted

User report: "the upper body bending when moving cursor up and down is reversed". Root cause: a sign
inversion in `PlayerAnimator`. `PlayerController.LookPitch` is documented positive = looking down
(PlayerController.Camera.cs), and on the Torso pivot a positive X rotation = lean forward (toward the
model's facing). The look tilt negated the pitch, so looking down pitched the torso BACKWARD and
looking up pitched it forward. The sprint run-lean (`-12f * runBlend`) shared the same inverted
convention. Verified by grep + reread (rule 3, no CLI build); `LookPitch` has no other consumers.

- `PlayerAnimator.cs:146` â€” dropped the leading minus:
  `lookTilt = Mathf.Clamp(_pc.LookPitch, -60f, 60f) * TorsoLookBlend` â†’ looking down leans the torso
  forward, looking up leans it back (applied in both idle line 161 and the moving pose).
- `PlayerAnimator.cs:239` â€” run lean flipped `-12` â†’ `+12` so the sprint genuinely leans the torso
  forward per the "cartoon run forward lean" comment (previously a backward arch). The head-bob
  baseline (line 241) was left untouched â€” it reads coherent with the corrected torso.

### 1e9-status
- Implemented; verified by grep + reread (rule 3, no CLI build): `LookPitch`/`TorsoLookBlend`/
  `lookTilt` consumers are confined to PlayerAnimator (1660-style grep); sign math checked against the
  pitch doc (+ = down) and the Torso pivot's identity local rotation on the +Z-facing model
  (+X = forward lean). game-design Â§3.5 + PROGRESS + THINKING updated same pass.
- Play-test (pending, user runs Unity): moving the cursor up/down while idle and moving bends the
  upper body the correct way (down = forward lean, up = lean back); sprinting leans forward instead
  of arching back; aiming down while sprinting blends smoothly.

## 1e8. Torso follow-up from play-test â€” close the crown band hole, reveal the shoulder joints

User play-tested 1e7 and reported two things: some torso faces "not loading", and the shoulder joints
too narrow / overlapping the torso. Both fixed in one pass (no build/CLI run, rule 3 â€” verified by
grep + reread; user re-tests in Unity).

- **Crown-cone band was never emitted ("faces not loading").** `BuildTorso`'s band loop was
  `for (int b = 1; b < bands; b++)` (bands = 8) â†’ it emitted the 7 bands (0,1)â€¦(6,7) and skipped the
  8th â€” the steep crown cone between the dome row (t=0.875) and the crown row (t=1.0). The crown disc
  floated as a disconnected lid and the torso top had an open see-through ring around the neck.
  Bound is now `b <= bands` so the crown cone is emitted (watertight, same mosaic). The ellipsoid
  `Generate` loop keeps `b < Rings` (its lat=0/Rings rows are degenerate poles â€” no such hole).
- **Top of the dome pulled in so the shoulder joints read (user chose "narrow the dome").** After 1e7
  the standing dome surface at the shoulder-pivot band was Wâ‰ˆ0.79 â†’ world **0.347** (male) â€” the
  existing Â±0.28 `JShoulder` ball (radius 0.07) sat flush/buried â†’ "too narrow, overlap with torso".
  1e8 narrows the top rows so the SAME balls poke out as caps; joints and pivots untouched:
  - `Body`: shoulder shelf row 0.80â†’0.70, dome row 0.74â†’0.60 (crown 0.20 unchanged); depth rows
    0.48â†’0.44 and 0.44â†’0.38. New reach at pivots (tâ‰ˆ0.78): world 0.299 (male) / 0.268 (female) â†’ the
    ball pokes ~5 cm / ~8 cm. Chest 0.72 stays the widest upper point.
  - `Chest` (sit model): mid rows 0.72â†’0.66, 0.70â†’0.62, 0.66â†’0.60 â†’ sit pivots (tâ‰ˆ0.43, W 0.64 â†’
    world 0.250) poke ~6 cm. `SitTorso` untouched. Seated-in-car `Body` inherits `Body` automatically.

### 1e8-status
- Implemented; verified by grep + reread (rule 3, no CLI build): band-loop indices for `b = bands`
  read rows 7â€“8 (corners length = rowsÃ—Segs = 108 â†’ max index 107, valid); silhouette math re-derived
  for all three models (standing/seated/sit pokes above); no other consumer of `BuildTorso`/torso ids;
  `EmitQuad`/`EmitTriangle` untouched (shared with ellipsoid parts). game-design + PROGRESS + THINKING
  updated same pass.
- Silhouette rows are the tunable knobs if the caps don't read right after play-test (`wB`/`dB` top
  rows in `PlayerPartMesher.BuildTorso`).
- Play-test (pending, user runs Unity): torso top fully closed â€” no see-through ring around the neck
  in standing/sit/seated poses (also check the crown cone isn't glitchy under the faceted lighting);
  shoulder joint balls read as small round caps poking out of the dome; sit/seated models consistent.

## 1e7. Torso routing fix â€” the shouldered silhouette actually renders now

The 1e2/1e4 shouldered-torso work (V-taper, pinched waist, shoulder dome tucking under the neck
crown) **never rendered**: `PlayerPartMesher.BuildEllipsoid` fell back any profile id not present in
`_profiles`, and the torso ids (`"Body"`, `"SitTorso"`, `"Chest"`) are deliberately kept OUT of
`_profiles` (they are not dent-sculpted ellipsoids but the flat-facet silhouette builder
`BuildTorso`). So the fallback remap to `"HairBand"` sent every torso part to the plain unsculpted
ellipsoid â€” `Generate`'s `BuildTorso` branch (PlayerPartMesher.cs:155) was unreachable, which is why
the torso looked "the same" through 1e2â†’1e4. Fix: `BuildEllipsoid` now routes those three ids to a
cached `BuildTorso` build (same cache/HideAndDontSave pattern) before the fallback. No shape-change
was made â€” the dome/shelf/crown silhouettes were already sized correctly (verified: standing `Body`
shoulder pivot lands at tâ‰ˆ0.72 â†’ W 0.78 â†’ world half-width 0.344 at size.x 0.44 â‰¥ pivot Â±0.28;
female 0.40Â·0.78 = 0.312 â‰¥ 0.28; sit `Chest` carry tâ‰ˆ0.48 â†’ 0.275 â‰¥ pivots Â±0.25). NPC/enemy models
use `MakeBlock` (cube) paths, unaffected.

### 1e7-status
- Implemented; verified by grep + reread (rule 3, no CLI build): only `MapBuilder.MakePart`
  (MapBuilder.cs:134) calls `BuildEllipsoid`, and it passes the raw profile id, so the new torso
  branch is reached by the player model builders and nothing else regresses; `PlayerAnimator`,
  `ClubPatronAnimator`/`ClubDJAnimator`/`ClubDancer` only `Find("Body")` by name (unchanged); no code
  keys off the torso mesh identity. game-design + PROGRESS + THINKING updated same pass.
- Play-test (pending, user runs Unity): standing player torso shows the flat-facet SHOULDERED
  silhouette for the first time (shoulders the widest point, pinched waist, shoulder joints sitting
  ON the dome band, small crown under the neck â€” no capsule); same check for the sitting
  (`SitTorso`+`Chest` layered) and seated-in-car models; arms hang from the shoulders without a gap.

## 1e6. Optimization Phase 6b â€” real chunk LOD, live object pooling, missile-scan throttle

The structural half of the optimization re-audit (the boot/per-frame half shipped in `1e5`). No
build/CLI run (rule 3) â€” verified by grep + reread; user play-tests in Unity.

- **Chunk LOD actually does something now.** `ChunkObject` builds two decimated child meshes per
  chunk â€” `Lod1` (every 2nd tile corner, ~1/4 tris) and `Lod2` (every 3rd tile, ~1/9) â€” sampled from
  its own merged top-terrain block as a watertight regular grid (shared grid vertices, exact edge
  coverage; `step` must divide the 30-tile chunk). They are built **lazily** and marked stale by
  `ApplyMerged`/`PatchRegion`, so `ChunkLodManager`'s band switch calls `RefreshLodMeshes()` before
  showing a far band â€” deformation never renders a pre-excavation hole and near chunks never pay for
  LOD. `ChunkLodManager.ApplyBand` now **disables the root `MeshRenderer` while a detail band is
  active** (before, the root stayed enabled and distant chunks drew the full ~1800-tri mesh PLUS the
  detail â€” the bands were a no-op for triangle count). Physics untouched: the collider stays on the
  root/full mesh.
- **`ObjectPooler` is wired (it had zero consumers since Phase 9).** Created on the GameRoot at boot;
  `ObjectPooler.SpawnTransient(prefab, pos, rot, lifetime)` pools when available and falls back to
  plain `Instantiate`+`Destroy` otherwise. Wired the two spell **impact-VFX** sites
  (`SpellEffect.ResolveProjectileImpact`, `SpellCaster.ApplyHit`) and the per-dig **excavation
  debris** burst (`WorldStreamer.SpawnCraterDebris`). `Get` now replays a pooled `ParticleSystem`
  (`Clear`+`Play`) so reused VFX look fresh. Debris keeps its Rigidbody/scale/colour rewrite per use.
- **Missile guidance throttle:** `SpellEffect`'s homing re-lock scan (`RaycastAll` +
  `OverlapSphereNonAlloc`) runs every 3rd frame; the per-frame detonation probes and steering are
  untouched (no collision-continuity change).
- **Deliberately NOT pooled / not banded (audited + rejected, see THINKING `1e6`):** enemy death
  debris (the model parts themselves â€” pooling would restructure the model factory) and loot drops
  (persistent, pickup-state-bound); a further prop-collider distance band (the 1di prop ring already
  caps live props to ~160 GOs near the focus, and stripping far colliders would make distant trees
  walk-through and pass spells through).

### 1e6-status
- Implemented; verified by grep + reread (rule 3, no CLI build): new `ChunkObject` members are
  private except `RefreshLodMeshes()` (only caller `ChunkLodManager.ApplyBand`, guarded by
  `Chunk != null`); `ObjectPooler.SpawnTransient` is a new static helper with an unpooled fallback,
  and both former `Instantiate(...)` impact sites were re-grepped to confirm no other callers; the
  debris fallback keeps the explicit `SetActive(true)` the inactive template requires. game-design
  Â§2.5 + PROGRESS + THINKING updated same pass.
- Play-test (pending, user runs Unity): distant terrain visibly simplifies past ~30 m/60 m (no
  double-drawn full mesh) and re-fills when approached; dig a crater then back away past 60 m â€” the
  far LOD must show the pit, not a closed-over surface; chunk seams at LOD distance are clean (no
  gaps); spell impact VFX still play (and replay correctly on rapid repeated casts); digging/magic
  debris still scatters and disappears after ~2.5 s without accumulating.

## 1e5. Optimization Phase 6a â€” boot path + per-frame hotspots

Closed the last open optimization item: **Phase 6 / startup (#17, #18)** (the documented boot-path
work left over from the Phase 0-5 sweep), plus a set of safe per-frame hot-path fixes found by the
deep performance re-audit (boot timeline, per-frame HUD scans, render/physics budget). No build/CLI
run (rule 3) â€” verified by grep + reread; the render/physics structural work is 1e6.

- **Boot: ComponentRegistry** (`Assets\Scripts\Opt\ComponentRegistry.cs`) â€” one shared scene sweep
  per type instead of ~24 `FindAnyObjectByType` scans in `GameBootstrap`; freshly-`AddComponent`'d
  singletons are cached so later resolve passes stay sweepless.
- **Boot: idempotency guards** â€” `GameManager.AutoResolveReferences` no longer re-runs the UI/tool/
  menu build (side-effecting cluster guarded by `_referencesResolved`; field resolution + the Pets
  scan still run every pass so pre-placed scene pets are picked up), `UIManager.InitializeUI` and
  `SoundManager.LoadSoundClips` guard themselves (each was running 2-3x at boot â€” duplicate panel
  layout + duplicate 8Ã— `Resources.Load`). `ToolManager.Initialize` already had its own guard.
- **Boot: gated sync spawn chunk** â€” `NewWorldTestGround` is now resolved BEFORE the decision;
  `GenerateChunkSync((0,-10))` only runs for the non-platform fallback spawn (the platform IS the
  default spawn, so the 5-20ms synchronous chunk was building ground never seen).
- **Boot: `BootInitDeferrer`** (`Assets\Scripts\Opt\BootInitDeferrer.cs`) â€” non-critical manager
  setup (main menu, save system, quest init, cutscene + random-event wiring, then wife NPC + skill/
  friendship/fishing/chest) now runs one batch per frame after the first rendered frame; original
  dependency order preserved. quest/karma/religion are deliberately excluded â€” `StartNewGame`
  already re-initializes them on frame 1.
- **Per-frame:** `MultiplayerIndicatorHUD` caches the `NetServerHost` and refreshes its label at
  ~4Hz (was: re-find + text write every frame); `EnemyHealthBarHUD` re-projects bar positions at
  ~30Hz instead of every frame and skips enemies > 60m (the 0.5s scene scan is unchanged);
  `SpellBeam`/`BlindStatus` cache the main camera; `FlickerLight` caches its `Light`. (Interaction
  prompt audited â€” already 3-frame raycast-gated with text-change guards, left untouched.)

### 1e5-status
- Implemented; verified by grep + reread (rule 3, no CLI build): every changed member is private or
  signature-preserving (`InitializeUI`/`LoadSoundClips`/`AutoResolveReferences` callers re-checked â€”
  UIManager.cs:236/GameBootstrap/GameManager.Start only), the deferred lambda captures only
  bootstrap locals, `BootInitDeferrer` runs its queue exactly once then self-destroys, and
  `QuestManager.InitializeQuests` was confirmed self-guarded before deferral. game-design Â§8.6
  (boot + opt) + PROGRESS + THINKING updated same pass.
- Play-test (pending, user runs Unity): boot-to-playable noticeably snappier; every menu/settings/
  tutorial/ending/save-slot panel still opens (single-pass UI init must not drop any panel); world
  spawn with `CreatePlatform` off still lands on ground (sync-chunk fallback path); enemy bars track
  at 30Hz with no visible lag; server indicator still updates on connect/disconnect; channeled
  beams/fog follow the camera when the camera is moved at runtime.

## 1e4. Shoulder-dome torso â€” remove the flat collar, keep the pivots covered

The 1e2 flat top plateau read as a collar ring / hat brim around the neck base. Replaced with a
**sloped shoulder dome** that tucks a small crown under the neck â€” but the FIRST cut of 1e4 (selftuned
silhouette only) left the standing/seated shoulder pivots floating: the pivots sit on the very top row
of the part silhouette (standing root (Â±0.28, 0.40) = unit t 1.0 = the crown row W 0.20 â†’ world 0.088;
seated (Â±0.24, 0.47) = tâ‰ˆ0.94 â†’ world ~0.156), i.e. the 1e2 plateau's W 0.80 through t=1 had been
LOAD-BEARING. Fixed by building the `"Body"` parts TALLER so the pivots land on the dome band.

- **`PlayerPartMesher.cs`** â€” `BuildTorso` (8-band / 9-row lattice, closed bottom cap + CROWN disc Wâ‰ˆ
  0.20 â‰ˆ neck radius): deltoid W 0.80 (shelf) â†’ dome band 0.74â€“0.76 â†’ crown 0.20; `SitTorso`/`Chest`
  keep their tucking roles. Doc comment updated with the verified coverage arithmetic.
- **`MapBuilder.PlayerModels.cs`** â€” standing `Body` (line ~135): size.y **0.6 â†’ 0.8**, torso-local
  center **0.05 â†’ 0.13** (spans torso-local [âˆ’0.27, 0.53]; crown tucks under the neck+head base);
  seated `Body` (line ~258): size.y **0.5 â†’ 0.6** (root center 0.25, spans [âˆ’0.05, 0.55], crown flush
  under neck [0.50, 0.60]). Sit model untouched â€” `Chest` already carries its pivots (tâ‰ˆ0.43, world
  0.28 â‰¥ Â±0.25).
- Coverage now (world half-width at pivot height â‰¥ pivot offset): standing male 0.44Â·0.79 = **0.347 â‰¥
  Â±0.28**, female 0.40Â·0.79 = **0.315 â‰¥ Â±0.28**; seated 0.34Â·0.74 = **0.252 â‰¥ Â±0.24**.

### 1e4-status
- Implemented; verified by grep + reread (rule 3, no CLI build): both `MakePart("Body"...)` size/pos
  lines updated once each; `Body`/`SitTorso`/`Chest` silhouette arrays intact and 9 entries each;
  no other consumer of the `"Body"` part size (grep of `MakePart`/`"Body"` call sites); skirt/waist +
  seated hip pivots still inside the taller bodies by inspection; game-design Â§3.5 (1e2/1e4/1e3
  bullets cleaned of the leftover half-worked text) + PROGRESS + THINKING updated same pass.
- Play-test (pending, user runs Unity): male+female standing â€” shoulder seam flush, no joint-ball
  poke, no crown ring at the neck, waist/shirt-hem look with the taller torso; race shoulder-spread
  s/b > 1.2 still covers the far joint ball; seated-in-car â€” crown under the neck, torso height in
  the seat, arm/steering pivot unchanged; sit-on-chair â€” `Chest` unchanged, `SitTorso` top tucks;
  skirt/waist cap seam; ponytail/back-hair clearance against the taller torso top.

## 1e3. Hair refit â€” scalp cap that hugs the head

"the hair isn't fit to the head at all": the hair was 4â€“6 free-floating slabs placed against an
ideal sphere â€” the crown slab hovered **4 cm above the scalp**, the side panels drifted off the
skull laterally, the nape panel floated behind the head shell. Retuned all `Hair`/`HairSide`/
`HairBack`/`HairBand`/`Ponytail` parts to hug the ACTUAL head hull in every variant.

- **`MapBuilder.PlayerModels.cs`** (all three builders):
  - `Hair` crown: now a thin **cap lens** (oblate ellipsoid, e.g. standing 0.36Ã—0.12Ã—0.30 at y
    0.80) whose widest band sits ON the crown â€” top of the skull pierces its lower half while the
    upper rim rises ~0.05 above (no float, no gap), and its front rim stays behind the eye line
    (rz â‰ˆ0.10 at eye height < eye z 0.155 â†’ face clear). Same lens profile for seated (0.34Ã—0.12Ã—
    0.28 @ 0.87) and sit (0.36Ã—0.12Ã—0.30 @ 0.92), matching each head's crown height.
  - `HairL/R` sides: pulled in and down (standing Â±0.18 @ y 0.70, 0.08Ã—0.26Ã—0.28) so the inner face
    sits ~1 cm INSIDE the skull side (attached, not floating).
  - `HairBack` nape: thickened slightly and lapping the back shell (standing female 0.30Ã—0.36Ã—0.11 @
    (0,0.66,âˆ’0.16); male 0.30Ã—0.30Ã—0.11 @ (0,0.68,âˆ’0.16)) so outward protrusion reads as nape volume
    while the inner face is buried.
  - `HairBand` (female): placed across the cap's brow arc (0.36Ã—0.05Ã—0.34 @ (0,0.80,0.02)); ponytail
    chain re-anchored (t1 @ (0,0.63,âˆ’0.21)) to overlap the nape panel instead of floating behind.
  - No hierarchy/parent/name changes â†’ `ApplyRaceRatioRecurse` (`Hair*`/`Ponytail*` â†’ `headScale`)
    and all name lookups behave exactly as before.

### 1e3-status
- Implemented; verified by grep + reread (rule 3, no CLI build): all 18 hair MakePart lines updated
  across the three builders with the new sizes/positions (grep of `"Hair"`/`"HairSide"`/
  `"HairBack"`/`"HairBand"`/`"Ponytail"` call sites); no other consumer of the player hair parts
  exists (NPC/restaurant hairdos are `MakeBlock`-based and untouched). game-design Â§3.5 (1e3 bullet
  written in the 1e2 pass) + PROGRESS + THINKING updated same pass.
- Play-test (pending, user runs Unity): no scalp float on standing/seated/sit male+female; crown cap
  reads as hair thickness not a pancake; side panels don't cover the eyes at any race head scale;
  nape/band/tail attached without free-floating shards; race `BodyHead` ratios still grow hair with
  the head.

## 1e2. Shouldered torso silhouette â€” torso/chest no longer ellipsoids

"the torso still has gaps, change the shape" after 1e0. Root cause: the shoulder/hip pivots sit
OUTSIDE the ellipsoid (pivot radii ~0.8â€“1.6 unit vs the 0.5 lattice radius), so dent pushes along
the radial (bounded by `Strength` â‰ˆ0.15) could never bridge the shell gap. The torso/chest parts
are now a dedicated **flat-facet torso silhouette** that physically reaches the pivots.

- **`PlayerPartMesher.cs`**: the `"Body"`, `"SitTorso"`, `"Chest"` profile entries (ellipsoid +
  dents) were REMOVED; `Generate` now routes those ids to a new `BuildTorso(profileId)`: 12-seg Ã—
  7-band flat-facet mosaic (reusing the deterministic shared-corner jitter and square/triangle
  emission â€” watertight), a closed bottom cap, and a flat **top shoulder plateau** disc the neck
  cylinder passes through (reads as the collar). Silhouette reach per band (unit, world half-width =
  `size.xÂ·W`; height still `size.y`):
  - `Body`: hip flare 0.55 â†’ waist 0.46 â†’ chest 0.58 â†’ **shoulders 0.80** (world 0.35 standing,
    covering pivot Â±0.28), held through the top plateau.
  - `Chest`: mid-band plateau **0.72** (carries the sit shoulders).
  - `SitTorso`: **0.70** at its top.
- **`MapBuilder.PlayerModels.cs`**: shoulder pivots tucked ~1 cm DOWN so they sit inside the band
  (standing 0.36â†’0.35, sit 0.41â†’0.40; seated unchanged at 0.47); joint balls shrank to sit embedded-
  but-visible (standing `JShoulder` 0.16â†’0.14, `JHip` 0.15â†’0.13; seated 0.14â†’0.13, 0.13â†’0.12;
  sit 0.15â†’0.13, 0.14â†’0.13). Pivot names/rotations and the `Torso`/`Shoulder*`/`Hip*` hierarchy
  untouched â†’ animator, weapon rig (`Torso/ShoulderL`), race-ratio logic unaffected.

### 1e2-status
- Implemented; verified by grep + reread (rule 3, no CLI build): removed `"Body"`/`"SitTorso"`/
  `"Chest"` from `PlayerPartMesher._profiles`; `Generate` special-cases â†’ `BuildTorso`; silhouette
  tables present; all `AddJoint`/shoulder-position edits present once per builder; no other consumer
  references the removed profiles; the EmitTriangle doc header (damaged mid-insert) restored.
  game-design Â§3.5 + PROGRESS + THINKING updated same pass.
- Play-test (pending, user runs Unity): no visible shell gap at shoulders/hips on standing, seated,
  sit (male+female); shoulder plateau reads as a collar ring around the neck base, not a hat brim;
  arms hang attached (upper-arm inner edge buried ~2 cm); race ratios up to shoulder-spread â‰ˆ1.3
  still covered (beyond that the far joint ball starts to float â€” flagged); seated/sit cutscene and
  car-fit unchanged (shoulder tucks â‰¤1 cm); `PlayerModelScale` remains the one-line bulk revert.

## 1e1. Neck switched from pillar to a cylinder

Follow-up to 1dz: "change the neck into cylinder". The `Neck` part is now a **round cylinder
column** (`"Cylinder"` profile); the square masonry pillar of 1dz is removed.

- **`PlayerPartMesher.cs`**: `"Pillar"` profile, `BuildPillar()` and `BuildBox()` removed; new
  `BuildCylinder()` emits 12 flat side facets (matching the faceted-band count) + closed top/bottom
  caps, all via the existing `EmitQuad`/`EmitTriangle` (per-panel outward winding checks). Same
  [-0.5, 0.5] half-extent contract â†’ `localScale` = size vector still reproduces dimensions; size
  independent and cached under `"Cylinder"`.
- **`MapBuilder.PlayerModels.cs`**: the three `Neck` `MakePart` calls switched profile `"Pillar"` â†’
  `"Cylinder"`, sizes unchanged (standing 0.15Ã—0.16, seated 0.13Ã—0.10, sit 0.14Ã—0.12); GameObject
  name stays `"Neck"` (race-ratio counter-scaling and consumers untouched).

### 1e1-status
- Implemented; verified by grep + reread (no CLI build, rule 3): no `Pillar`/`BuildPillar`/`BuildBox`
  references remain in `Assets\Scripts`; `"Cylinder"` profile + `BuildCylinder` present; the three
  `Neck` MakePart calls use `"Cylinder"`. No API/name/collider changes. game-design Â§3.5 + PROGRESS +
  THINKING updated same pass.
  Play-test (pending, user runs Unity): neck reads as a round column (12 flat facets + flat caps) on
  standing, seated and sit; sits flush under the head and above the torso shoulder-shelf; race-ratio
  counter-scale still proportioned; capsule silhouette reads cylindrical from every angle (not the
  square pillar of 1dz).

## 1e0. Seal the torsoâ†”limb gaps â€” reshape torso + tuck pivots + bigger joint balls

Follow-up to 1dy: "the torso and limbs has gaps either change the shape of torso". Choices taken:
**all junctions** (shoulders + hips; elbows/knees were already flush), **reshape + tuck pivots**.
Clarified reasoning path: the old cube torso hid the attach points under sharp corners; the faceted
ellipsoid rounds them, and 1dy tightened the torso â€” so shoulder/hip pivots floated up to ~0.16
world-units off the skin. Dents alone can't reach the pivots, hence the three-part stack.

- **`PlayerPartMesher.cs`** â€” reshape the shared profiles:
  - `"Body"` gains symmetric **shoulder-shelf dents** (anchor Â±(0.34, 0.34, 0), radius (0.28, 0.22,
    0.26), strength +0.15 â€” bulges the upper-torso skin out/up toward the shoulder pivots; zero
    influence at chest centre/waist by design) and gentle **hip-flare dents** (Â±(0.26, -0.36, 0),
    radius (0.22, 0.16, 0.20), +0.05) so the lower torso keeps a hint of pelvis breadth instead of a
    bare taper point.
  - `"SitTorso"` gains the same shoulder-shelf dents (sit model). Seated reuses shared `"Body"`.
  - Meshes stay size-independent/cached â€” one Body mesh serves male/female/seated.
- **`MapBuilder.PlayerModels.cs`** â€” tuck pivots + enlarge balls:
  - Standing: shoulders (Â±0.33, 0.37)â†’(Â±0.28, 0.36), `JShoulder` 0.13â†’**0.16**; hips
    (Â±0.13, -0.25)â†’(Â±0.12, -0.25), `JHip` 0.14â†’**0.15**.
  - Seated: shoulders (Â±0.26, 0.485)â†’(Â±0.24, 0.47), `JShoulder` 0.12â†’**0.14**; `JHip` 0.12â†’**0.13**.
  - Sit: shoulders (Â±0.27, 0.42)â†’(Â±0.25, 0.41), `JShoulder` 0.13â†’**0.15**; `JHip` 0.13â†’**0.14**.
  - Pivot NAMES + rotations untouched â†’ `PlayerAnimator`/`WeaponRigBuilder`/`ApplyRaceRatioRecurse`
    unaffected; the ~2â€“4 cm tuck is visual-scale only.

### 1e0-status
- Implemented; verified by grep + reread (no CLI build, rule 3): shelf/flare dents present on
  `"Body"` + `"SitTorso"` only; every shoulder/hip pivot position + joint size updated per builder
  (grep `JShoulder/JHip` + position lines); no other profiles touched; no API/name/collider changes.
  game-design Â§3.5 + PROGRESS + THINKING updated same pass.
  Play-test (pending, user runs Unity): no pinch gap between torso and shoulder/hip balls on
  standing/seated/sit, male+female, and the small/large race ratios (ratio-scaled pivots carry the
  joints); the shoulder shelf reads as natural breadth, not a hump, and doesn't collide with the
  neck/head; arm swing + weapon reach unchanged; joint balls lap the seam from every camera angle.

## 1dz. Neck switched to a pillar shape

> Superseded by **1e1** below (the neck is now the `"Cylinder"` profile; the pillar mesh, `BuildPillar`
> and `BuildBox` were removed in 1e1). Keep this entry for the record only.

Follow-up to 1dx/1dy: "the neck switch to pillar shape". The `Neck` part is no longer the round
faceted ellipsoid â€” it is now a **unit-space square masonry column**.

- **`PlayerPartMesher.cs`**: new `BuildPillar()` + `BuildBox()` helpers emit a flat-faced stack of
  three boxes (foot slab full width â†’ straight shaft â†’ cap/abacus), each face a flat-shaded quad via
  the existing `EmitQuad` (per-face outward winding check against the face centroid). Covered by the
  same [-0.5, 0.5] half-extent contract, so `localScale` = size vector still reproduces dimensions
  and the mesh stays size-independent/cached. New `"Pillar"` profile key; `Generate` special-cases it.
- **`MapBuilder.PlayerModels.cs`**: all three `Neck` `MakePart` calls switched to the `"Pillar"`
  profile, slightly wider/taller so it reads as a column under the head â€” standing
  (0.15, 0.16, 0.15) at (0, 0.4, 0); seated (0.13, 0.10, 0.13) at (0, 0.55, 0); sit
  (0.14, 0.12, 0.14) at (0, 0.62, 0). GameObject name stays `"Neck"` so race-ratio counter-scaling
  (head/neck) and every consumer are untouched. The old `"Neck"` dent profile is retained but unused.

### 1dz-status
- Implemented; verified by grep + reread (no CLI build, rule 3): `BuildPillar` referenced only from
  `Generate`; `BuildBox` called 3Ã— per pillar; `"Pillar"` profile present and `MakePart` calls use it
  (grep `profileId "Pillar"` / `"Pillar"`); the `Neck` GameObjects remain named `"Neck"`;
  `ApplyRaceRatioRecurse`/`PlayerAnimator`/weapon contracts untouched. game-design Â§3.5 + PROGRESS +
  THINKING updated same pass.
  Play-test (pending, user runs Unity): neck reads as a straight square column (foot/shaft/cap) on
  standing, seated and sit; no gap overlapping the head chin / torso; race-ratio counter-scale of the
  head/neck still looks fine (column scales with ratios); pillar catches light with its own flat
  faces (no faceted round read).

## 1dy. Slimmer torso, +8% overall size, faceted ball joints at limb pivots

Follow-up to 1dx: "reduce the torso width abit then increase the total size, add sphere with similar
skin generate to between parts as joints" (plural = faceted with the same low-poly mosaic). Choices
taken: **moderate** (-12% torso width, whole model +8%), **match adjacent part** joint color,
**limb joints only** (shoulder/elbow/hip/knee â€” no neck joint), applied to all three model variants.

- **`MapBuilder.PlayerModels.cs`**:
  - Torso slimmer: standing `Body` x 0.50â†’0.44 (male) / 0.46â†’0.40 (female); seated `Body` 0.38â†’0.34;
    sit `Torso` 0.42/0.46â†’0.37/0.40, `Chest` 0.44â†’0.39. Skirt/shoulders/limbs untouched.
  - Whole-model size: new `PlayerModelScale = 1.08f` const applied to every root's `localScale`
    (`one * scale * K`) and root `localPosition` (standing `0.86Â·K`, seated `(-0.35,0.65,-0.1)Â·K`,
    sit stays zero). `ApplyRaceLook.calibrateFeet` now multiplies by `K` so feet stay planted. One
    constant = single-line revert if the car cutscene / chair fits clip.
  - Faceted ball joints: new `AddJoint(name, pivot, size, color)` helper â†’ `MakePart(..., "Joint")`
    at the pivot origin (rotates with the pivot, inherits race-ratio pivot scaling). Added after each
    shoulder (`JShoulderL/R` ~0.13 `shirtC`), elbow (`JElbowL/R` ~0.11 `shirtC`), hip (`JHipL/R`
    ~0.14 `pantsC`) and knee (`JKneeL/R` ~0.12 `pantsC`) in all three builders (slightly smaller in
    the seated car model). `Jâ€¦` names never collide with animator/weapon lookups; `IsArmUnderShoulder`
    still routes arm joints to layer 7 via their `Shoulder`/`Elbow` ancestors.
- **`PlayerPartMesher.cs`**: new `{ "Joint", new Dent[0] }` profile â€” a plain faceted sphere (no dents,
  same mosaic) reused by every joint part.
- **Contracts kept** (rule 5 audit): no public API changes; part/pivot names used by `PlayerAnimator`,
  `WeaponRigBuilder`, `WeaponAnimator`, `ApplyRaceRatioRecurse` untouched; no new colliders (hitbox
  unchanged â€” CharacterController/RaceRig own collision, the +8% is visual only).

### 1dy-status
- Implemented; verified by grep + reread (no CLI build, rule 3): `PlayerModelScale`, `AddJoint` and
  the `"Joint"` profile are referenced only where intended; the accidental deletion/re-add of the sit
  color declarations was caught and corrected during the same pass; `calibrateFeet` math re-read.
  game-design Â§3.5 + PROGRESS + THINKING updated same pass.
  Play-test (pending, user runs Unity): torso reads slimmer on male/female standing, seated and sit;
  whole model ~8% bigger with feet still grounded and no hitbox change; joints facet-match the 1dx
  mosaic, sit at the limb pivots, rotate with walk/run/jump, and don't clip into limbs (may need size
  tweaks); race-ratio variants still proportional (joints inherit pivot scale, incl. the 1.35 etc.);
  first-person arms + joints + weapons visible on layer 7; car cutscene / chair fits don't clip
  (revert `PlayerModelScale` to 1 if they do).

## 1dx. Faceted low-poly skin â€” chunky triangle/square mosaic with deterministic jitter

User feedback after 1dw: the player model "is currently only plain original shape" â€” they want
"multiple surface triangle, square shape generate all over the skin". Choices taken: **chunky
low-poly** facets (fewer, larger panels), slight **deterministic jitter** for a hand-cut organic look,
applied to **all parts** (body, limbs, head, hair, skirt, shoes, eyes).

- **`PlayerPartMesher.Generate` rewritten** (only code change; `MakePart`, the three builders, every
  pivot/layer/weapon contract and the cache are untouched): the smooth Rings 9 Ã— Segs 16 shared-vertex
  sphere is replaced by a **corner lattice + panel emission** pipeline:
  1. Sculpt the corner lattice (Rings 7 â†’ 6 bands Ã— Segs 12) exactly as 1dw did â€” the dent silhouette
     (waist/chest/sockets/knee etc.) is preserved on the bones of the mosaic.
  2. Deterministic tangent-plane jitter (~0.03 unit-space) per SHARED corner (hash of corner index +
     profile seed) so panel boundaries read hand-cut while staying watertight â€” no cracks to see
     through, identical every build.
  3. Emit panels with **duplicated vertices and flat face normals** (no `RecalculateNormals`): each
     band cell becomes a SQUARE panel (4 verts, 2 coplanar triangles, one shared normal â†’ reads as a
     square) or a pair of TRIANGLE panels (split along a hash-chosen diagonal, ~35% of cells);
     pole fans are always triangles. Each panel is wound outward by checking its normal against the
     panel centroid â€” no global winding assumption.
- **Invariants kept** (rule 5 audit): mesh still spans [-0.5, 0.5] â†’ `localScale` = old size vector
  reproduces dimensions; race ratios/gender/weapon hand-scale stay transform-only; cache keyed by
  profile id; `HideAndDontSave`; no collider. All part names/pivots/sizes/rotations untouched.

### 1dx-status
- Implemented; verified by grep + reread (no CLI build, rule 3): `Generate`/`EmitTriangle`/
  `EmitQuad`/`Hash01`/`AnchorSeed` self-consistent; no remaining `RecalculateNormals` in
  `PlayerPartMesher`; public surface (`BuildEllipsoid`) unchanged, so `MapBuilder.MakePart` compiles
  as-is; variable shadowing checked (`s1`-vs-seg counter, `n`-vs-`inf` in prior 1dv CS0136 class of
  bug â€” none in this file). game-design Â§3.5 + PROGRESS + THINKING updated same pass.
  Play-test (pending, user runs Unity): every part shows the chunky triangle+square mosaic (not a
  smooth ball); squares AND triangles both readable; dent silhouettes still visible under the facets;
  no cracks/see-through from any camera angle; race-ratio/gender/cutscene variants scale identically;
  eyes still sit in the head sockets and read as faceted discs; hands/weapons alignment unchanged
  (hand parts are just smaller faceted ellipsoids).

## 1dw. Smooth player model â€” ellipsoid part surfaces with terrain-style dent sculpt

> Superseded visually by **1dx** below (the generator is now a Rings 7 Ã— Segs 12 CHUNKY FACETED
> mosaic; sculpt, cache and transform sizing all still work exactly as documented here).

The player model is no longer a boxy doll. Every body part now gets a **tessellated unit-space
ellipsoid mesh** (`PlayerPartMesher`), generated once per part profile and sculpted with the same
crater-dent math the terrain deform uses; the spine/pivot hierarchy and all animation/weapon contracts
are untouched.

- **`PlayerPartMesher` (new, `Assets\Scripts\Models\PlayerPartMesher.cs`)**: builds an ellipsoid per
  profile as a Rings 9 Ã— Segs 16 sphere grid spanning the same half-extent cube [-0.5, 0.5] the old
  shared unit cube spanned, so `localScale` = old size vector reproduces the exact dimensions. Each
  profile carries dent ops (anchor + ellipsoid radius + strength): normalized ellipsoid-distance
  influence, the same `s = tÂ²(3âˆ’2t)` smoothstep as `WorldStreamer.DeformAt`, vertex pushed along its
  original radial. Winding is auto-checked (first-face normal dot vs origin) and `RecalculateNormals`
  runs after sculpting. Meshes are static + cached `Dictionary<string, Mesh>` (HideAndDontSave),
  shared across gender/race/model variants.
- **`MakePart` (`MapBuilder`)**: player-part builder over the cached mesh â€” same transform/color
  contract as `MakeBlock` plus a profile id, no collider (CharacterController owns collision).
  `MakeBlock` is untouched (still used by creatures/vehicles/props/NPCs).
- **`MapBuilder.PlayerModels.cs` (rewritten)**: all three builders (`BuildPlayerModel`,
  `BuildSeatedPlayerModel`, `BuildSitPlayerModel`) call `MakePart` with profile ids; every part name,
  pivot, size, position and rotation (sit/seat poses keep their elbow/shoulder `Quaternion.Euler`
  args) is preserved verbatim, so `PlayerAnimator`, `WeaponRigBuilder`, `WeaponAnimator`,
  `PlayerController.Animation` (layer 6/7 first-person culling) and `IsArmUnderShoulder` still
  resolve.
- **Profiles**: Body, Skirt, SkirtHem, Head (eye-socket dents at Â±(0.13, 0.02, 0.16), nose, chin,
  jaw taper), Neck, UpperArm, Forearm, Hand, Thigh, Shin, Shoe, Hair, HairSide, HairBack, HairBand,
  Ponytail, EyeWhite, EyeIris, SitTorso, Chest. Unknown/empty ids fall back to the plain ellipsoid.
- **Race ratios are transform-only (hard invariant)**: unit-space meshes mean `ApplyRaceLook` /
  `ApplyRaceRatioRecurse` / `ApplyRaceLook`'s foot replant / `RaceRig.RigScale` /
  `WeaponRigBuilder.ScaleForHandScale` all work unchanged â€” the mesh is never baking a size-derived
  radius and the cache key is size-independent.

### 1dw-status
- Implemented; verified by grep + reread (no CLI build, rule 3): zero `MakeBlock` left in
  `MapBuilder.PlayerModels.cs`; all part names match the animator/weapon/race-ratio consumers;
  `MakePart` defined in `MapBuilder`, `BuildEllipsoid` defined in `PlayerPartMesher`; sit/seat pose
  rotation args carried through. game-design Â§3.5 + PROGRESS + THINKING updated same pass.
  Play-test (pending, user runs Unity): model look in standing/sit/seat + cutscenes â€” all parts read
  smooth, no Z-fighting where hair/skirt/hands sit on bodies; gender variants (skirt + ponytail vs
  male); every race ratio â€” `BodyHeight`/`BodyBulk` stretch, `BodyHead` counter-scale keeps head/eyes
  proportioned (eye dents scale with the head), `BodyShoulderWidth` spread, `BodyArm`/`BodyLeg`
  lengths with feet replanted on the ground; 1st person arms unchanged; weapon draw/stow/stance and
  hands still aligned (Hand parts keep the scale `ScaleForHandScale` compensates for); walk/run/jump
  bob and body-lean still animate the same pivots; and after
  the race change rebuild the skeletonâ†’muscle look still reads correctly.

## 1dv. Chunk mesh pooling â€” one persistent Mesh per chunk + a capped freed-mesh pool

The mesh-allocation half of the 1dq deferral ("mesh uploads â€¦ deferred to 1dt (noise memo) + 1dv
(mesh pooling)"):

- **Where allocation lived:** `ChunkObject.ApplyMerged` did `new Mesh` + `UploadMeshData(false)` on
  EVERY call and `Destroy`ed the previous one â€” so `FullRebuildChunk` (whole-chunk deform rebuilds,
  slab chunks, seam/border reconciles) and every chunk (re)creation on fill/unload/reload spun a
  fresh Mesh + GPU upload each time and carried a transient double GPU buffer until the delayed
  Destroy ran. The deform fast path (`PatchRegion`) already reused the chunk's mesh + `_merged` CPU
  arrays in place and was untouched.
- **Now:** a chunk owns ONE `Mesh` for its whole life (`ChunkObject._mesh`): the first `ApplyMerged`
  acquires it from a small capped pool (`ChunkMeshGenerator` `_chunkMeshPool`, cap **48**); every
  rebuild re-uploads into the SAME instance (`UploadMerged` = the trimmed setter sequence + one
  `UploadMeshData`); `Release()` returns the mesh to the pool instead of destroying it, so a later
  chunk reuses the same GPU buffer. `CreateMeshFromMerged` is kept as a convenience factory over the
  same path (no remaining callers). Chunk meshes share a uniform ~961-vert / ~1800-tri size, so a
  pooled buffer never reallocates once warm (slab side walls only ever grow it; every upload fully
  re-specifies the arrays, so it can never corrupt).
- **Correctness catch in review:** the collider can no longer rely on the `sharedMesh` reference
  change to re-cook (same pooled instance across rebuilds) â€” `ApplyMerged` now explicitly
  nullâ†’assigns `sharedMesh` for collider-active chunks (the pattern `PatchRegion` already used), so
  a rebuilt chunk's physics stays in sync with its visuals after a deform.
- game-design Â§2.7 (mesh-pooling bullet) + PROGRESS + THINKING updated same pass. Verification
  (no CLI build, rule 3): grep â€” `CreateMeshFromMerged` cited only by its own definition (retained
  public factory); `AcquireChunkMesh`/`ReleaseChunkMesh`/`UploadMerged` referenced only by
  `ChunkObject` + the factory; `ApplyMerged` call sites (boot sync chunk, `CreateChunkGameObject`,
  `FullRebuildChunk`) all still thread `buildCollider:` unchanged; the pool is main-thread only.

### 1dv-status
- Implemented; verified by grep + reread (no CLI build, rule 3). Follow-up fixes: (1) the user's Unity
  compile surfaced CS0136 in `BeginProps` (the 1du keep-alive branch's `for (int i â€¦)` collided with
  the method-block `int i = 0;` tile fill counter) â€” renamed the fill counter to `tileIdx`. (2) Unity
  runtime surfaced `Mesh.normals/uv/colors is out of bounds` from `UploadMerged` on a POOLED mesh
  whose previous upload held more vertices than the incoming one (e.g. a slab chunk's side walls) â€”
  Unity Mesh buffers only ever GROW through the typed setters, so a smaller re-upload wrote channels
  against the stale larger buffer; `UploadMerged` now `Clear()`s the mesh whenever
  `mesh.vertexCount != md.Vertices.Length` (hot same-size path stays allocation-free). The
  overwrite-only-is-safe claim in the 1dv notes was wrong in that direction and is corrected here +
  in THINKING + game-design Â§2.7. Play-test: dig/cast Earth terrain
  spells near chunk seams â€” the ground visuals AND walkable physics must both update (collider
  re-cook intact after rebuilds); deform a tall cliff drop so a chunk gains slab side walls, then
  flatten it back (pooled mesh must handle the downsize cleanly); walk far so chunks
  unloadâ†’reload â€” terrain identical, no stutter
  from mesh realloc; F12/new-game/`ResetTerrainSaves` loop still streams cleanly; no memory warnings
  from the pool.

## 1du. Prop-ring keep-alive + shared dent-debris cube template (micro-opt)

Two allocation/behaviour nits from the 1di/1dq/1dt follow-up sweep:

- **Prop-ring keep-alive (ChunkObject):** before, a chunk leaving the prop ring had its spawned props
  `Destroy`ed and re-entering re-rolled the whole deterministic stream â€” walking the ring edge kept
  killing + re-creating every tree/rock (~2 GOs + a 900-tile re-roll per chunk toggled). Now
  `ReleaseProps` merely `SetActive(false)`s them and `BeginProps` on re-entry reactivates the SAME
  GameObjects (the RNG/tiles/cursor survive, so a partially-streamed chunk resumes exactly where it
  stopped) â€” the edge costs a hide/show toggle instead of destroy/respawn churn. `ChunkObject.Release()`
  (chunk unload) still destroys props outright. Behavior change vs `1di`: a chopped tree/rock now stays
  chopped when the ring leaves then returns (it was a documented deterministic respawn before).
- **Shared dent-debris cube (WorldStreamer.Deform):** `SpawnCraterDebris` instantiates each piece from
  one shared inactive cube template instead of `GameObject.CreatePrimitive` per piece (mesh/material
  resolved once, per-piece GO/component init skipped). The clones drop the BoxCollider â€” debris is
  cosmetic rigidbody scatter (3â€“5 pieces, 2.5 s life) and, per user choice, now flies up then **sinks
  through the terrain** rather than landing in the pit (accepted trade-off for lighter debris physics).
- Also committed the untracked `ColliderRequestRegistry.cs.meta` left over from `1dq`.
- game-design Â§2.7 (prop-ring bullet â†’ keep-alive semantics) + PROGRESS + THINKING updated same pass.
  Verification (no CLI build, rule 3): grep â€” `PropsOn`/`BeginProps`/`ReleaseProps`/`PropsPending`/
  `StepProps` confined to `ChunkObject` + `WorldStreamer.Props.cs`; `SpawnCraterDebris`/`SharedDebrisCube`
  each cited once; no other consumer of the old destroy-on-release semantics.

### 1du-status
- Implemented; verified by grep + reread (no CLI build, rule 3). Play-test: walk the prop-ring edge â€”
  trees/rocks hide/show at ~600 m with no create/destroy hitch; chop a tree then leave + re-enter the
  ring â€” it stays chopped; dig or cast a crater â€” debris bursts up briefly, sinks into the ground, and
  vanishes at ~2.5 s; chunk unload (walk far) still tears props down cleanly.

## 1dq. Collider-on-demand â€” physics ring + magic requests (terrain MeshColliders)

User: "currently the entire everything in 30 radius is loading at the same time, so would there be a
way to load only those with neccessity without reduceing the range"; then: "only load the collider of
those near the player and and magic". **Render radius stays 30** (user rejected shrinking it).

- **Before:** every loaded radius-30 chunk carried a MeshCollider â€” ~3,721 cooks on first pass, ~2.2M
  triangles sitting in the broadphase even far from the player, every raycast/overlap (player ground
  probe, SpellCaster â‰¤40 m, NavGrid, TornadoBehavior, ToolManager, Fishing, debris) walked a huge set.
- **Now:** a chunk streams in **collider-less** (`CreateChunkGameObject` default `buildCollider:false`);
  a per-poll `ReconcileColliders(centre)` assigns the collider exactly when it is needed and drops it
  when it stops being needed (cheap state guard â€” idle polls toggle nothing):
  - **Player ring:** `WorldStreamer.ColliderRingRadius = 8` (~9Ã—9 chunks = 240 m) covers every gameplay
    raycast distance (SpellCaster â‰¤40 m, NavGrid, Tornado, ToolManager, Fishing). Ring jump on death/
    new game/F12 is safe: reconcile scans the full loaded map every poll, so interior chunks lost by a
    teleport get re-enabled before the player lands.
  - **Magic requests:** `ColliderRequestRegistry` â€” `SpellEffect` keeps its current flight chunk
    requested (moves chunk-by-chunk, each crossing releases the old so no stale requests accumulate),
    releases on `OnDestroy`; reconcile expands each request by `ColliderRequestExpand = 1` so a bolt
    grazing a seam still stops on terrain. Long-range fireballs still detonate on far hills; other far
    chunks render meshes but cost zero physics.
  - **Rebuild paths preserve state:** `ChunkObject.ApplyMerged` now takes the collider flag through to
    the MeshCollider and tracks it; `FullRebuildChunk` passes `buildCollider: obj.HasCollider`;
    `PatchRegion` re-cooks only live colliders; `Release()` resets the flag. The synchronous boot chunk
    (`GenerateChunkSync`) still builds its collider so the player can land before the first poll.
- Estimate (no measurements, rule 3): at radius 8 of 30 (~6.4% of the square), steady-state cooked
  collider triangles drop to ~140k vs ~2.2M (~94% less); prop ring (1di) already covers the visuals the
  player sees, this closes the physics gap for the rest.
- game-design Â§2.7 (new bullet) + THINKING + PROGRESS updated same pass. Verification (no CLI build,
  rule 3): grep â€” 24 refs consistent across ChunkObject / WorldStreamer cs files / ColliderRequestRegistry /
  SpellEffect; `ApplyMerged` call sites preserved (buildCollider param threaded, boot path explicit).

### 1dq-status
- Implemented; verified by grep + reread (no CLI build). Play-test: run the build â€” visuals identical
  at radius 30; **stand still** and walk out â€” you should encounter a hard, walkable ground edge ~240 m
  from center (collider ring), not a void; fire a long-range firebolt far past the ring â€” it must still
  explode on the terrain it reaches (magic request); chips/carves/waves still dent the ground near the
  player; F12/death/new-game respawn on the pad with no fall through; no physics hitch on promo (a chunk
  entering the ring cooks its collider once).

## 1dr. Per-frame caching sweep (no behavior change)

- Killed the residual every-frame component lookups flagged during the 1di/1dq investigation
  (per-frame CPU already dominates the frame budget at radius 30; these were pure overhead):
  - **PlayerController** (partials): added lazy null-cached accessors `StatsCached`, `CombatCached`,
    `ClassPassivesCached`, `SpellCasterRef`, `MainCam` (PlayerController.cs) and switched the hot
    paths off raw `GetComponent`: `MaxHP`/`MaxStamina`, `HandleMovement`, `HandleStamina` (regen
    reads), `TakeDamage`, and the aim-frame reads in `UpdateCastingCircle`/`BurstCastingCircle`/
    `UpdatePathPreview`/`BeamChanneling`/`TryAoeTarget`/`ShouldCancelCharge`. The accessors are lazy
    and never cache a null, so late-rigged components (CombatController added by WeaponRigBuilder)
    still resolve â€” old per-frame lookups were ~14/frame before, now ~0.
  - **MagicWheelUI**: cached `_combat` (swap-agnostic like the existing `_caster`) so the per-frame
    `HoldingMagicWeapon` no longer GetComponent-scans.
  - **SkillBarHUD**: cached `PlayerController` + `SkillBindings` against player swap (2 fewer
    GetComponents/frame).
  - **PlayerBarsHUD**: status-chip `SetActive` calls now compare before toggling, and the strip
    poll is gated to every 3rd frame (statuses only change per whole second â€” invisible, âˆ’2/3 of
    the GetComponent scan + preserves).
  - **AudioManager**: cached `_cam` (one `Camera.main`/frame) and wrapped the two steady-state
    `Crossfade` volume writes in settle guards so a settled audio mix stops hammering native
    property sets.
  - **ChunkLodManager**: `SetActive` guarded per chunk per pass (no more re-calling SetActive on
    ~every registered chunk each refresh).
- Left as-is: event-driven GetComponents (interaction-key presses, combat toggles, model reloads,
  menu builders), `EnemyHealthBarHUD` (already 0.5 s scan-gated), `InteractionPrompt`
  (already 1/3-frame raycast + seat scan), `SittableSeat.FindNearest` (bounded list, gated).
- Also committed the 44 Unity-generated `Assets\Scripts\Enemies\**\*.meta` files that 1do left
  untracked (GUID-stable folders committed in the same pass so the race folders stop swallowing
  later `git add -A`).
- Verification (no CLI build, rule 3): grep â€” remaining `GetComponent`/`Camera.main` in
  `PlayerController*` are one-time (Awake/model-load/menu) or inside key-press branches; the new
  accessors are the only per-frame lookups and all live behind a null guard. No behavior change:
  each replacement preserves its null-fallback semantics.

### 1dr-status
- Implemented; verified by grep + reread (no CLI build). Play-test: everything should feel/behave
  EXACTLY identical (this pass only deleted redundant lookups) â€” walk + sprint + regen, equip/unequip
  + cast aim, skill bar slots + cooldown fills, status chips appear/expire under the bars, ambient/
  music crossfades, distant-chunk render culling. Watch the console for warnings (none expected).

## 1dt. Terrain noise memoization in the merged-mesh builder

- **Hotspot:** `ChunkMeshGenerator.BuildMergedMeshData` re-sampled the pristine 5-octave noise
  surface **per vertex** for strata coloring â€” ~3,600 `TerrainNoiseGenerator.GetHeight` calls per
  chunk (~18,000 `Mathf.PerlinNoise` evaluations), even though a chunk only has 31Ã—31 = 961 distinct
  world corners and every corner is a pure function of (seed, x, z).
- **Fix:** one thread-local memo per build call, `Dictionary<long,float>` keyed by
  `((long)wx << 32) | (uint)wz` (exact same keying the border-corner map already uses):
  - new `TerrainBandColor(seed, wx, wz, vertexY, memo)` overload â†’ each band sample reads the memo
    instead of re-running GetHeight; the public 4-arg overload is kept (PatchRegion in
    `ChunkObject`, terrain-aim `WorldStreamer.Deform`) with identical behavior.
  - `EdgeIsRaised`/`EdgeHeights`/`CornerHeight` thread the same memo, so the out-of-chunk seam
    noise fallback is also sampled once (was: once in Pass 1 and again in Pass 3).
  - Colors are byte-for-byte the same (the memo returns the identical deterministic value).
- Effect: band-color noise per chunk drops ~3,600 â†’ â‰¤961 GetHeight calls (~3.7Ã—); no behavior change,
  terrain is deterministically identical, memo is confined to one (background) chunk build.
- game-design unchanged (implementation detail under the Â§2.7 streaming bullet); PROGRESS + THINKING
  updated same pass. Verification (no CLI build, rule 3): grep â€” all `TerrainBandColor`/edge/corner
  call sites resolve to the kept 4-arg overload or the threaded internal paths; `MemoizedHeight` is
  the only new noise entry point in the builder.

### 1dt-status
- Implemented; verified by grep + reread (no CLI build). Play-test: identical terrain qua every
  frontier (colors, strata on carved pits and walls already saved) â€” full-radius boot should feel
  slightly snappier since the background chunk build spends less CPU on noise; watch burst
  hitches at boot/streaming edges.

## 1dp. Magic model bench â€” strip OrbFx so pedestal models are static

User: "some magic keep switch between big and small continuously which really fuck up the visual".

- **Root cause:** the bench's `SpellCaster.CreateProjectileDisplay` builds each model through the same
  `AttachDefaultProjectileVisual` path as live casts, which always attaches `OrbFx`. That component's
  `Update()` rescales + spins every model continuously (`Bolt` pulses Â±0.22 at ~24 Hz, `Ember` up to
  Â±0.22, spins on Shard/Swirl/Tumble). On a flying projectile that flicker is the intent; sitting on a
  pedestal it reads as "switching between big and small".
- **Fix:** inside `CreateProjectileDisplay`, after building the visual, destroy all child `OrbFx`
  components â€” the pedestal models are now fully static. Live-cast visuals are untouched (every
  `AddComponent<OrbFx>` lives in the projectile builders that the display strips after the fact).
- Grep: `CreateProjectileDisplay` has a single caller (NewWorldTestGround magic-model grid); `OrbFx`
  is only added inside `SpellCaster.Projectiles.cs`. The comet-exhaust particle streams on the pedestal
  models are kept (they don't change size/shape).
- game-design Â§2.7 magic-model grid bullet updated in the same pass.
- Verification (no CLI build, rule 3): reread `CreateProjectileDisplay` + `OrbFx` â€” strip loop runs
  before any child `Update`, destroys the only animation source on the display.

### 1dp-status
- Implemented; verified by grep + reread (no CLI build). Play-test: enable the magic-model grid â€” the
  pedestal models should now sit perfectly still (no more big/small pulsing or wobble); casting any
  spell still flickers/crackles/spins in flight as before.

## 1do. Per-race enemy folders â€” one script per race (distinct stats)

User: "there is no enemy in enemy folders, split each enemy race into a folder of each own, that folder
would contain the script of enemy from that race".

- **Before:** enemies were fully data-driven â€” one generic `EnemyController` FSM shared by all 21
  races (a race was just a string `EnemyId`), identical default stats everywhere, no per-race files.
  Now every race owns a folder + script under **`Assets\Scripts\Enemies\<Race>\<Race>Enemy.cs`**:
  - `Enemies\_Shared\EnemyController.cs` (moved from `Combat\AI`, .meta GUID preserved) â€” the FSM
    base; added `protected virtual ApplyRaceConfig()` called from `Awake` + `ApplyEnemyId` so each
    subclass stamps its `EnemyId` + stats.
  - 21 race classes (`SlimeEnemy â€¦ BatEnemy`, `DummyEnemy`), each with a **distinct stat profile**
    (HP 30â€“140, dmg 7â€“20, etc. â€” full table in game-design Â§7.1.0); `DummyEnemy` is the immortal
    1000-HP training target (0 dmg, 15%/s regen â€” the test ground only overrides DamageReduction +
    aggro ranges for the armored variant).
  - `Enemies\_Shared\EnemyCatalog.cs` â€” maps race id â†’ per-race component type (unknown ids fall
    back to the generic controller).
  - `Enemies\Boss\BossController.cs` â€” moved unchanged (3 call sites keep working: POI, Dungeon,
    test ground).
- **Spawners wired to per-race brains:** `EnemySpawner.SpawnAt` no longer early-outs without a
  prefab â€” it clones the prefab only as a visual template (stale brain swapped out) or builds a
  plain GO, then `EnemyCatalog` adds the race's component; `NewWorldTestGround.SpawnEnemyRow` and
  `SpawnDummy` use the catalog / `DummyEnemy`. So editing a folder's script affects every spawn.
- `EnemyModelBuilder.BuildEnemy(id)` still supplies all models (kept centralized). `BossController`
  still data-driven by `BossId`.
- game-design Â§7.1.0 (race stat table + folder layout) + Â§2.7 and these docs updated same pass.
- Verification (no CLI build, rule 3): grep â€” `ApplyEnemyId`/`AddComponent<EnemyController>` remain
  only as (a) the base definition, (b) the intended catalog fallback; `_Archived\*` legacy refs are
  outside Assets (not compiled); `BossController`/`EnemyController` consumers (HitboxSystem,
  Spell*, CC/statuses, EnemyHealthBarHUD, NewWorldSystems, EnemyStateSync, RaceEffect/ClassEffect,
  DungeonSystem, POIGenerator) resolve via the class name, path moves are GUID-stable (.meta
  git-mv'd). Stat names compile against the base's public fields.

### 1do-status
- Implemented; verified by grep + reread (no CLI build). Play-test: enable `EnableEnemies` on the
  test ground â€” each row should now show noticeably race-specific survivability/speed (hit a golem
  vs a bat; dps check the dragon/demon); the armored dummy should still be ~50% DR and immortally
  regenerate; world spawns (`EnemySpawner`) behave with no prefab assigned. Tune any race by editing
  its folder's `ApplyRaceConfig()`.

## 1dn. Player spawn point moved to the test ground

User: "change the player spawn point to be on the test ground".

- The test platform is now the **default spawn** across all three placement paths:
  - `GameBootstrap` creates `NewWorldTestGround` BEFORE the player teleport and lands the player on
    `GetSpawnPoint()` (pad top + 2 m) whenever the platform is built (`CreatePlatform` +
    `IsArenaReady`) â€” the deck is built synchronously in `Awake`, so there is never a void to fall
    through; the boot chunk `(0, terrain+2, -10)` stays as the fallback when the platform is off.
    Removed the duplicate `testGround` add at the end of the boot.
  - `PlayerController.ResetPlayer` (new game / death respawn) now re-homes to `GetSpawnPoint()`
    whenever `IsArenaReady`, instead of only when the player had already reached the pad; the
    proximity gate `IsOnOrNearArena` was deleted (its only caller).
  - `NewWorldTestGround.AutoTeleportPlayerOnStart` default flipped `false â†’ true` â€” the
    `RunBenchSpawn` pull-onto-pad is now belt-and-braces for the same spot (harmless: it teleports to
    the identical coordinate on frame 1).
- Boot order stays "ground first, then player": the spawn chunk is still generated synchronously (the
  boot chunk remains the sync ground), the pad surface exists before placement, and `TeleportTo`
  stamps a safe position so the physics fail-net never trips.
- game-design Â§2.7 (rewrote the old "No auto-teleport at boot (1bz)" bullet) + these docs updated in
  the same pass.
- Verification (no CLI build, rule 3): grep â€” `IsOnOrNearArena` 0 matches; `AutoTeleportPlayerOnStart`
  cited by the field + the `RunBenchSpawn`/fallback guards only; the legacy `BootSpawnPosition()` and
  boot-chunk fallback remain reachable.

### 1dn-status
- Implemented; verified by grep + reread (no CLI build â€” rule 3). Play-test: boot into play mode â€”
  the player should stand on the test platform (south edge, facing away from center; mouse turns
  around), bench lanes spawn as before, and no physics snap-back or fall through the deck; press F12
  / start a new game â†’ same pad spawn; disable `AutoTeleportPlayerOnStart` (or `CreatePlatform`) â†’
  boot-chunk start near `(0, ~terrain+2, -10)` with the pad walk-to as before.

## 1dm. Nature props (trees + rocks) cut to 1/5 density

User: "reduce tree and stone spawn ratio to 1/5".

- The streamed world's nature props spawn in `ChunkObject.StepProps`: one 1-in-200 roll per tile for
  trees and one for rocks. Both were cut to **1-in-1000 per tile** (= 1/5 of the original 1/200):
  new const `ChunkObject.PropSpawnOdds = 1000` drives both `SpawnTree`/`SpawnRock` calls. A chunk
  (~900 tiles) now averages ~2 cube-heavy props instead of ~9 (a full ring-4 = 81 chunks â‰ˆ 150 props
  vs ~730). The ring keeps everything else: pop-in/out at the `PropRingRadius` edge, deterministic
  per-chunk stream, colliders for chopping/mining.
- Only the STREAMED world was changed. The legacy `WorldBuilder.SpawnTrees(150)`/`SpawnRocks(75)`
  spawn path is legacy-mode only (`EnableLegacyGeneration = false`) and untouched.
- Comments in `ChunkObject` + `WorldStreamer.Props` and game-design Â§2.6/Â§2.7 updated in the same pass.
- Verification (no CLI build, rule 3): grep â€” `Next(200)` no longer exists (0 matches);
  `PropSpawnOdds` cited only by both `StepProps` rolls; `SpawnTree`/`SpawnRock` callers unchanged;
  legacy `SpawnTrees(SpawnRocks` still only run under `EnableLegacyGeneration`.

### 1dm-status
- Implemented; verified by grep + reread. Play-test: walk the streamed world â€” noticeably fewer trees
  and rocks (~1/5 the previous density), still in sparse clusters; chop/mining targets near the player
  still respawn deterministically; perf is even lighter.

## 1dl. Enemy arena off by default on the test platform (code kept)

User: "stop spawning enemy for now but keep the enemy code, i'll test them later".

- `NewWorldTestGround.EnableEnemies` default flipped `true â†’ false`; the whole `SpawnEnemies`/
  `SpawnEnemyRow`/`SpawnDummy`/`SpawnBoss` lane and its `RunBenchSpawn` wiring are UNTOUCHED. Since the
  live game adds the component at runtime via `GameBootstrap` (`AddComponent`, no scene instance â€” the
  only serialized scene copy is a `_Recovery` backup), the C# default governs: no enemies/dummies spawn,
  the magic model bench and every other lane run as before. Tick `EnableEnemies` back on to test later.
- game-design.md Â§2.7 + these docs updated in the same pass.
- Verification (no CLI build, rule 3): grep â€” `EnableEnemies` cited only by the field + the
  `RunBenchSpawn` guard; `SpawnEnemies*` methods still present and referenced only by that guard.

### 1dl-status
- Implemented; verified by grep + reread. Play-test: load the scene â†’ the south arena stays empty
  (enemy rows/dummies/boss absent), magic grid + all other lanes normal; re-tick `EnableEnemies` to
  bring the arena back.

## 1dk. Magic model bench: every castable magic spell placed on the test platform

User: "place down every magic on the test ground so i can look and edit the magic model".

- **Scope (user-chosen):** ONE display per castable magic spell (the full 90+ `MagicTestMatrix`
  roster â€” `SkillCatalog.OfType(SkillType.Magic)` minus passives, gated on
  `skill.Effect is SpellCastEffect cast && cast.Spell != null`, sorted by school then display name)
  + an in-game world-TMP label above each (user-chosen).
- **Display:** each spell gets a pedestal + a school-colored projectile-style body placed on a grid
  across the platform's clear middle band (x Â±0.78Â·PlatformSize at 3u pitch â†’ 32 columns Ã— 3 rows on
  the default 120 platform), oriented to face the platform center. Bodies reuse the EXACT live-cast
  visuals via the new `SpellCaster.CreateProjectileDisplay(type, shape, rockBody)` static â€” Comet /
  Earth Meteor / Asteroid show the summonFallingRock boulder, explicit shapes (Ice Lance, Shadow
  Spear, Arcane Missiles, Wind Scissor...) show their real body, and zone/beam/vortex/storm/summon/
  instant spells show their school-colored default icon (those deliveries are runtime-animated, no
  static projectile â€” flagged to the user; animated delivery models are a possible follow-up).
- **Plumbing:** `SpellCaster.AttachDefaultProjectileVisual` made `static` (it only called statics;
  the instance `DecorateProjectile` wrapper for turrets is unchanged). New opt-in lane:
  `NewWorldTestGround.EnableMagicModels` (default on, after the skills lane in `RunBenchSpawn`) â†’
  `SpawnMagicModels()`. Pedestals and bodies get **no collider** (grid stays walkable). Labels copy
  the world-TMP pattern of the legacy building signs (text/fontSize/alignment/color/outline/sizeDelta),
  colored by `DamageNumber.ColorFor(school)`.
- Verification (no CLI/Unity build, rule 3): grep â€” `AttachDefaultProjectileVisual` callers
  (`FireProjectile`, `DecorateProjectile`) unchanged; `CreateProjectileDisplay` cited only by
  `SpawnMagicModels`; `SkillCatalog.OfType`, `UiAssetCache.DefaultFont` (not used now â€” label follows
  the sign pattern), `SkillCastEffect.Spell` accessor all verified present. Reread of both files
  confirms balanced braces/flow. `game-design.md` Â§2.7 and these docs updated in the same pass.

### 1dk-status
- Implemented; verified by grep + reread only (rule 3 â€” no compile). User play-tests in Unity.
- Play-test pending: walk the platform's center band â€” every magic spell should be present with a
  readable school-colored label, each projectile shape should match what that spell casts, the grid
  should not overlap the enemy/dummy/village lanes, and the bench stays walkable (open the spell's
  ProjectileShape-related code and re-run to see the edit).

## 1dj. Enemy health bars anchor to each enemy's model head (not a fixed offset)

User: "the hp bar of enemy does not stay on their head, fix".

- **Bug:** `EnemyHealthBarHUD` positioned every bar at `enemy.transform.position + (0, 2.2, 0)` â€” a fixed
  height above the enemy ROOT (feet). Enemy models vary a lot (model top â‰ˆ slime/slug/mimic/bat
  0.3â€“0.45u, wolf/drake/ice_wolf 0.6u, scorpion 0.35u, bandit/undead/mummy/skeleton 0.95u, fire
  elemental/yeti/golem/treant 1.1â€“1.2u, dummy 1.9u), so the constant 2.2u floated far above small
  enemies and only roughly matched big ones â€” bars never sat on heads.
- **Fix:** `ComputeHeadOffset(enemy)` (new, in `EnemyHealthBarHUD`) measures the model's highest
  renderer bounds (`ModelRoot.GetComponentsInChildren<Renderer>().bounds.max.y` minus the root's y)
  and adds a `0.25` margin; it runs only on bar target-change (pool rebind), never per frame, so up to
  24 bars have zero steady-state cost. Fallback stays 2.2u when the model is missing. The bar still
  tracks the root transform every frame, so it stays glued to the head while the enemy walks; the
  cached offset is stable because models are static children of the root (no hover/bob/animation in
  `EnemyController`).
- Verification (no CLI/Unity build, rule 3): grep â€” `ModelRoot` is public on `EnemyController`
  (`EnemyController.cs:53`, its only external consumer is this HUD); `ComputeHeadOffset` is new and
  cited once; no other enemy HP-bar path exists (legacy WorldBuilder has none; `NewWorldSystems`
  routes to this HUD). Reread of the HUD confirms balanced braces and the stale `TrackDamageable`
  class doc-comment was corrected (that symbol never existed in code).

### 1dj-status
- Implemented; verified by grep + reread only (rule 3 â€” no compile). The user play-tests in Unity.
- Play-test pending: damage a slime, a wolf, a bandit, a dummy, a bat, and a tall one (golem/undead) â€”
  each bar should sit just above that model's head, stay glued while the enemy chases, and never float
  high above small enemies.

## 1di. Lag fix: prop ring + adaptive burst smoothing (steady 60 trails the stream)

User: "game too lag" (after `1dg` radius 30). After the `1dh` file refactor, scope was clarified via
questions: implement **prop ring** AND **only the burst-hitch smoothing** (no collider band, no prop
mesh-merge/instancing); keep **player + targets hit-able**; target **steady 60 on default settings**.

- **Prop ring (new behavior):** `WorldStreamer.PropRingRadius` (serialized, default **4** chunks â‰ˆ 600 m,
  Chebyshev) â€” trees/rocks stream only within the ring of the focus. `ChunkObject` gained a prop-state
  lifecycle: `PropsOn` (stream queued) + `ReleaseProps()` (destroys spawned props, keeps terrain mesh +
  collider). `SyncPropRing(centre)` (new, per tick) begins the deterministic stream for chunks entering
  the ring and releases it for chunks leaving it; `CreateChunkGameObject` no longer queues props â€” the
  ring owns that (spawn chunk is always inside the ring, so boot ground keeps its trees/rocks instantly).
  Cut: the distant radius-30 ring no longer holds ~33k prop GameObjects / ~450k prop BoxColliders; the
  ring alone (~81 chunks at default 4) keeps ~800 props. Hit-ability preserved: everything inside the
  ring keeps colliders; props pop in/out only at ~600 m (chunk-boundary pop reads as normal streaming).
  Behavior note: props are regenerable/deterministic per chunk â€” re-entering the ring after chopping
  respawns the same tree/rock, matching the existing chunk-unload/reload behavior.
- **Burst smoothing:** `FinalizeChunks` cap dropped 16 â†’ 12 chunks/tick and wall-clock budget 12 ms â†’
  base 6 ms via new `AdaptiveBudgetMs(baseMs)` (scales by previous frame length â€” a hitchy frame shrinks
  the next tick's budget toward ~2 ms; a smooth one spends the full budget; never zero). `StepChunkProps`
  gained `PropsOn` guard + `PropBudgetMs = 3f` wall-clock ceiling. Loading speed barely changes (the
  background pipeline was not the bottleneck); per-frame spikes from chunk mesh+MeshCollider cooking and
  prop GameObjects no longer extend frames.
- Verification (no CLI/Unity build, rule 3): grep â€” `BeginProps` now called only from `SyncPropRing`;
  `ReleaseProps`/`PropsOn` cited only by `SyncPropRing`/`StepChunkProps`/`Release`; no legacy WorldBuilder/
  test-ground path touches chunk props (NewWorldTestGround only reads `WorldStreamer.Seed`/`Radius`/
  `ResetTerrainSaves`). Reread of ChunkObject + all 6 WorldStreamer partials confirms balanced braces,
  no stale references to the removed `obj.BeginProps` call in the finalize path.

### 1di-status
- Implemented; verified by grep + reread only (rule 3 â€” no compile). The user play-tests in Unity.
- Play-test pending: boot â€” spawn chunk has trees/rocks immediately; walk outward â€” trees/rocks stream
  in around the player, pop out ~600 m behind, terrain stays solid for the whole ring; the radius-30
  fill holds ~60 fps (the frame-timer no longer green-lines during the fill); chop/mine targets within
  the ring still hit; a chopped tree that leaves the ring then re-enters respawns (expected, deterministic).
- Deferred (explicitly not chosen this pass): full collider band (~120â€“180 m), per-chunk prop mesh
  merge/GPU instancing â€” those remain candidates if the ring alone isn't enough at high radii.

## 1dh. Refactor pass: split the top 8 monolith files into partial classes (no behavior change)

User: "game too lag, first optimize the files structure" â€” clarified: do the code/file refactor FIRST
(this task), then the game-object structure lag fix as a separate task (planned as `1di`, next).

- Pure **mechanical** split: methods/properties/whole #regions moved out of each monolith into new
  same-class partial files, following the repo's existing convention (`WorldBuilder.*`, `MapBuilder.*`).
  **No logic, member name, signature, field, or serialized layout changed.** Fields and
  `Awake/Update/Start/OnDestroy` and any trailing helper classes stayed in each original file.
- 8 monoliths split (original â†’ kept + new files):
  - `CharacterInfoUI.cs` 3321 â†’ core + Stats / Equipment / Faith / Inventory / Map / Skills (7 files).
  - `MapBuilder.cs` 2276 â†’ core + Stores / Restaurants / Nightclub / Vehicles / Police (11 files total).
  - `WorldBuilder.cs` core â†’ core + Environment (sky/weather/fog) + Lights (street lights) (14 files total).
  - `PlayerController.cs` â†’ core + Movement / Camera / Animation / Combat / Interactions (6 files).
  - `UIManager.cs` â†’ core + Settings / Menus / Feedback (3 new parts; 13 files total).
  - `SpellCaster.cs` â†’ core + Cast / Channels / Projectiles / ApplyHit (5 files).
  - `WorldStreamer.cs` â†’ core + Streaming / ChunkBuild / Mesh / Props / Deform (6 files).
  - `ToolManager.cs` â†’ core + Dig / Weapons (2 new parts; 8 files total).
- **Verification (no CLI/Unity build, rule 3):** `git diff` on every original shows only deletions (+
  `partial` keyword / repositioning): WorldBuilder 0 add/142 del, MapBuilder 0/2027, ToolManager 0/70,
  UIManager 0/325, WorldStreamer 1/1042, SpellCaster 2/1061, PlayerController 3/1549,
  CharacterInfoUI 50/2774 (the `+` lines were content repositioned within the family â€” TreePan,
  RaceNode colors, IgnoreInput all verified present at HEAD). Per-family line totals preserved
  (delta â‰¤ 64 = new-file headers/usings). `partial class X` file counts: CharacterInfoUI 7, MapBuilder
  11, WorldBuilder 14, PlayerController 6, UIManager 13, SpellCaster 5, WorldStreamer 6, ToolManager 8.
  Moved-member uniqueness re-checked by grep; new-file subtrees have balanced braces/#regions; no
  duplicate script GUIDs; per-part usings are supersets of the original (warnings at worst, never
  missing types).
- Docs: `game-design.md` unchanged (pure file-move, no design/behavior delta). `PROGRESS.md` `1dh`,
  `THINKING.md` `1dh`.

### 1dh-status
- Implemented; verified by grep + reread + git-diff only (rule 3 â€” no compile). The user play-tests
  in Unity.
- Play-test pending: boot still works (WorldBuilder boot path intact), character/stats/map UI opens
  and populates (CharacterInfoUI partials), skills/race-trees render, shop/nightclub/restaurant/
  police-station buildings still build at their spots (MapBuilder partials), player movement/combat/
  sit/interact all respond (PlayerController partials), spells cast + projectiles + channels work
  (SpellCaster partials), tools swing/dig (ToolManager partials), chunk streaming still fills the
  radius-30 ring (WorldStreamer partials), settings/menus/feedback UI still open (UIManager partials).
- NEXT task (per user): `1di` â€” done above (prop ring + burst smoothing; collider band / mesh-merge
  explicitly deferred if the ring alone isn't enough at high radii).

## 1dg. Render radius raised to 30; streaming burst sped up ~4x (poll 2x + bigger budgets)

User: "increase terrain render range and need to increase the loading speed even more". Clarified:
radius **30** (~900 m half-width), **aggressive** burst loading, keep fog as-is.

- The fill rate was paced by constants in `WorldStreamer.cs`, not CPU: a poll tick every 0.1 s, a
  finalize cap of `min(ChunksPerFrame, 8)` chunks + a 6 ms wall clock (â‰¤ 8 chunks/tick â‰ˆ 80 chunks/s),
  and only 8 background generations in flight. At that rate even the old radius-20 (1,681 chunks) took
  ~21 s to fill â€” the bootstrap comment claiming "~1.5 s" was stale (it predated the big radius).
- **Render range** â€” `RenderDistanceController.cs` default `Radius = 20 â†’ 30` (61Ã—61 = 3,721 chunks);
  `GameBootstrap.cs` sets `rd.Radius = 30`. `ChunkLodManager.EffectiveCullDistance` already
  auto-scales to `(Radius + 1) * ChunkSize` (~930 m at radius 30), and the camera far plane default
  (~1000 m) just covers it.
- **Burst loading** â€” `PollInterval` `0.1 â†’ 0.05` s (2x ticks); `ChunksPerFrame` `8 â†’ 16` and finalize
  cap `8 â†’ 16` with wall-clock `6 â†’ 12` ms (main-thread apply is cheap: 961-vert/1800-tri mesh upload +
  one collider cook per chunk); `MaxInFlight` `8 â†’ 24` so the background backlog never starves the
  main thread. Net â‰ˆ 320 chunks/s â†’ radius 30 fills in ~10â€“15 s vs ~46 s on the old pacing (if it had
  been used at this radius).
- `PropTilesPerTick` `40 â†’ 120` â€” props (~1/200 trees + 1/200 rocks per tile) trail the terrain fill
  by only a few seconds instead of minutes across the bigger ring, still globally budgeted per tick.
- Fog untouched (user choice): the far ring stays hazy at density up to 0.015.
- Docs updated in one pass: `game-design.md` Â§2.5 (30 chunks / 3,721 / ~900 m + burst-fill note),
  `GameBootstrap` stale-comment fix, `PROGRESS.md` `1dg`, `THINKING.md` `1dg`.

### 1dg-status
- Implemented; no CLI/Unity build (rule 3) â€” verification by grep + reread: `Radius = 30` only in
  `RenderDistanceController.cs` (default) and `GameBootstrap.cs` (boot injection); `PollInterval`
  0.05, `MaxInFlight` 24, `ChunksPerFrame` 16, finalize cap 16 + 12 ms, `PropTilesPerTick` 120 only in
  `WorldStreamer.cs`; no other consumer hard-codes the old radius/pacing; ChunkLodManager cull
  formula still matches the new radius.
- Play-test pending: at boot the ground ring around the spawn chunk reaches the horizon; the fill
  completes in roughly 10â€“15 s without a visible single-frame hitch; walk at speed and confirm the
  leading edge keeps up with no holes; farthest ring corners are NOT clipped by e.g. camera far plane
  (< 1000 m) â€” if the diagonal reaches the clip, bump the camera far plane to ~1500 and note in the
  follow-up; trees/rocks stream in shortly after the terrain; no obvious frame-rate drop from ~5,600+
  chunk objects + props at once.

## 1df. Animals are damageable â€” 0 HP explodes them old-game style; club stays the non-lethal capture tool

User: "the animals dont have damage interaction so they basicly invincible right now". Clarified via
questions: at 0 HP "explode and part flung everywhere"; club stays non-lethal (capture).

- **Root cause.** The combat pipeline (melee `HitboxSystem`, weapon skills, `SpellCaster.ApplyHit`
  + zones/tornado/beam/storm, `SpellDoT`, race/class effects, summons) damages ONLY targets that
  implement `IDamageable`. `Livestock` had a full `TakeDamage` (red flash â†’ flee/fight â†’ knockout
  â†’ capture) but never implemented the interface â€” so every weapon/spell hit silently no-oped and
  only the club reached it directly (`GetComponentInParent<Livestock>().TakeDamage(20)`,
  `ToolManager.cs`), leaving animals invincible to everything else.
- **`Livestock.cs`** â€” now `MonoBehaviour, ITornadoCarried, IDamageable`; `public int TakeDamage(int)`
  (interface contract; returns remaining Health): subtract/clamp, `StartFlash()`, at 0 â†’
  `ExplodeAnimal()`, else the existing flee/fight triggers. `ExplodeAnimal()` mirrors
  `EnemyController.ExplodeModel` (1dd): every model part detaches, gains `BoxCollider` + `Rigidbody`
  (mass 0.3), `AddForce(dir*8 + up*6, Impulse)` + torque, each part `Destroy(part, 5f)`; the animal
  root dies 0.2 s later. New `KnockDown()` â€” non-lethal: `Knockout()` (15 s recover), no HP loss,
  capture loop intact.
- **`ToolManager.cs` (club)** â€” `target.TakeDamage(20)` â†’ `target.KnockDown()`: the club remains the
  capture tool, never kills.
- **Follow-up safety fix riding along.** Both `EnemyController.ExplodeModel` (shipped `1dd`) and the
  new `ExplodeAnimal` initially scheduled debris cleanup via `StartCoroutine(DestroyDebris)` on the
  controller â€” but `Destroy(gameObject, 0.2f)` kills the coroutine with the component, so debris
  never vanished. Both now `Destroy(part, DebrisLifetime)` per part (no coroutine indirection);
  `EnemyController`'s now-unused `System.Collections*` usings trimmed.
- Behavior notes: pig/goat fight, chicken/duck/turkey flee, cow/sheep passive â€” those triggers were
  already coded but unreachable, now fire on any hit. Knocked-out animals are immune
  (`TakeDamage` early-return) so DoTs can't finish them mid-capture. No loot/meat by choice; the
  spawner trickles replacements (45â€“80 s) as exploded animals are cleaned from `_activeAnimals`.

### 1df-status
- Implemented; no CLI/Unity build (rule 3) â€” verification by grep + reread: `Livestock` implements
  `IDamageable` with `int TakeDamage(int)` matching the interface; no other `Livestock.TakeDamage`
  callers (lone caller `ToolManager.cs:752` switched to `KnockDown`); `ExplodeAnimal`/`KnockDown`/
  `_exploded` no name collisions; per-part debris cleanup in both `Livestock` and `EnemyController`;
  unused usings removed from `EnemyController` without touching its fully-qualified `_targets`
  list.
- Docs updated in one pass: `game-design.md` Â§5.9 (damageable livestock + explosion + club capture)
  and the Â§5.1 club tool row, `PROGRESS.md` `1df`, `THINKING.md` `1df`.
- Play-test pending: hit each of the 7 species with melee and a spell â€” expect red flash + the
  right reaction (pig/goat charge, chicken/duck/turkey flee); lethal damage â†’ the animal bursts into
  flung voxel parts that fall and vanish after ~5 s; club â†’ animal falls over with NO explosion and
  can still be caged (cage pickup still works); tornado/DoT still damage animals; no debris remains
  after ~5 s (enemy burst cleanup fix).

## 1de. Creating a dent throws layer-tinted debris (dirt blocks or rock, like pickaxe stone)

User: "when creating dent, make a dirtblock explode as well or rock debris depends on the layer,
the explosion would much like when stone got destroyed by pickaxe."

- **Single chokepoint for every dent.** All crater excavation funnels through
  `WorldStreamer.DeformAt` (`TerrainDeformer.Dig` for the tools, `SpellCaster.ResolveZone`/
  `SpellEffect.ResolveProjectileImpact`/`SpellStorm` for Earth magic), so the new debris spawns there
  once: `if (shape == TerrainShape.Crater) SpawnCraterDebris(center)` after the height edits apply.
  Raised shapes (Wall/Ring/Pillar/Spikes) and no-op digs (unloaded tiles â†’ 0 edits) never throw
  debris.
- **Layer = the strata wall color.** `SpawnCraterDebris` samples the floor at the crater center and
  tints the 3â€“5 cubes with `ChunkMeshGenerator.TerrainBandColor(seed, x, z, floorY)` â€” dirt-brown
  blocks while the dig is in grass/dirt, grey rock once the pit reaches the stone band (â‰¥ ~2.7 m).
  The physics look mirrors pickaxe rock destruction (`WorldBuilder.SpawnRockDebris`): mass by
  volume, up-biased velocity, spin; cubes are `Destroy`d after ~2.5 s so repeated digs never litter.
- **Doubled grey burst removed.** `SpellEffect.SpawnImpactDebris` (the Earth projectile's own grey
  "fistful of rock" burst) was deleted â€” DeformAt now throws the layer-tinted debris for projectile
  dents too, so Earth impacts no longer double up. The tool `SpawnDigPuff` (quick 1 s shard poof)
  stays as the tool-stroke accent.

### 1de-status
- Implemented; no CLI/Unity build (rule 3) â€” verification by grep + reread: `SpawnCraterDebris` +
  `DentDebris` exist only in `WorldStreamer.cs`, `SpawnImpactDebris`/`SpellImpactDebris` gone from
  `SpellEffect.cs` (the remaining `RandomEventManager.SpawnImpactDebris` is an unrelated
  `(pos, parent)` method), `DeformAt`'s crater guard placed after the `newHeights.Count == 0` early
  return, comments in `SpellEffect` rewritten.
- Docs updated in one pass: `game-design.md` Â§3.8 (crater debris bullet + shovel/pickaxe tool rows),
  `PROGRESS.md` `1de`, `THINKING.md` `1de`.
- Play-test pending: dig with the shovel (expect dirt-brown blocks), dig past the stone band with the
  pickaxe (expect grey rock chunks), cast a Crater Earth spell / Stone Shard projectile (layer-tinted
  debris, no doubled burst), confirm raised shapes throw nothing and debris vanishes after ~2.5 s.

## 1dd. Enemies explode into voxel debris on death + test platform hosts all 20 enemy types

User: "add more enemy and every time the enemy die they explode like in the old game."

- **Death explosion (old-game burst restored).** The legacy enemy runtime had `ExplodeModel`
  (`_Archived\Enemies\EnemyController.cs:1057`): every model block detaches, gains a collider +
  rigidbody, and is blasted outward/upward with impulse + torque, cleaned up after ~5 s. The new
  open-world `EnemyController` never had it. Mirrored it into `EnemyController.Die()` â†’
  `ExplodeModel()` + `DestroyDebris()` coroutine (EnemyController.cs). Kept the existing 0.2 s
  `Destroy(gameObject)` tail. Purely visual â€” no damage/knockback/chain. Loot still drops first.
  Training dummies are `Immortal` and cannot die, so they never burst.
- **More enemies on the QA platform.** `NewWorldTestGround.SpawnEnemies` previously placed 5 types
  (slime/wolf/goblin/skeleton/bat) + 2 dummies in one row. Now it lays out **all 20 roster types**
  in two 10-wide rows (south arena, `z = center-40` and `center-32`) plus a 6-enemy horde row
  (`center-24`, repeat slime/goblin/bat/skeleton) â†’ ~26 enemies vs 5 before. The two training
  dummies moved to `z = center-18` (`x = centerÂ±4`), clear of the rows; boss spot untouched. New
  rows reuse the existing `SphereCollider` + `ApplyEnemyId(id)` pattern via a shared
  `SpawnEnemyRow(ids, startX, z)` helper.

### 1dd-status
- Implemented; no CLI/Unity build (rule 3) â€” verification by grep + reread: `ExplodeModel`/
  `DestroyDebris` added to `EnemyController` with no name collisions, `SpawnEnemies` callers and the
  `SpawnEnemyRow` seam reviewed, dummies/boss positions clear of the new grid.
- Docs updated in one pass: `game-design.md` Â§7.1.1 (Enemy Death Explosion), `PROGRESS.md` `1dd`,
  `THINKING.md` `1dd`.
- Play-test pending: on the platform, kill each row of enemies and confirm every one bursts into its
  own colored voxel blocks that fall with physics and vanish after ~5 s, loot drops before the burst,
  dummies never burst, and the boss (if `IncludeBoss`) is untouched.

## 1dc. Church + shrine roofs slope the right way (eave low, ridge high)

User: "you kinda got the roofs of church and taoist upsidedown" â€” the gable/hip panels of
`Church_Roof`, `Church_SpireRoof`, `Shrine_Roof` and `Shrine_Roof2` were rotated with the wrong
sign, so the ridge sat LOW and the eaves rode high (an inverted "V"). The pagoda's
`Pagoda_Roof1` was correct the whole time.

Convention (verified against `Pagoda_Roof1`, WorldBuilder.Blueprints.cs:1144-1147):
- Z-axis panels â€” a panel at **+z** slopes down toward that +z eave with `Euler(+Î¸, 0, 0)`,
  a panel at **âˆ’z** with `Euler(âˆ’Î¸, 0, 0)` âœ“
- X-axis panels â€” a panel at **+x** slopes toward that +x eave with `Euler(0, 0, âˆ’Î¸)`,
  a panel at **âˆ’x** with `Euler(0, 0, +Î¸)` âœ“

Fixed (8 rotation signs flipped in WorldBuilder.Blueprints.cs):
- `Church_Roof` (1500-1501): Z gable panels were swapped (Â±24) â€” now `+24` at z=+0.9,
  `âˆ’24` at z=âˆ’0.9.
- `Church_SpireRoof` (1554-1555): Z pitch panels were swapped (Â±38) â€” now `âˆ’38` at z=âˆ’2.2,
  `+38` at z=+2.2. The X panels (1556-1557) were already correct.
- `Shrine_Roof` (1709-1710): X hip panels were swapped (Â±14 on Z-rot) â€” now `+14` at x=âˆ’3.2,
  `âˆ’14` at x=+3.2. The Z panels (1707-1708) were already correct.
- `Shrine_Roof2` (1752-1753): X hip panels were swapped (Â±18 on Z-rot) â€” now `+18` at x=âˆ’2.4,
  `âˆ’18` at x=+2.4. The Z panels (1750-1751) were already correct.

Every other roof block in the file already matched the pagoda convention (checked all `Euler(`
calls in the four `case` blocks and the pagoda/wellhouse/mini-pagoda references).

### 1dc-status
- Implemented; no CLI/Unity build (rule 3) â€” verification was grep + reread: the four fixed blocks
  now match `Pagoda_Roof1`'s sign convention; no other rotated roof part was inverted.
- Docs updated in one pass: `PROGRESS.md` `1dc` + `THINKING.md` `1dc`. `game-design.md` has no
  roof-geometry section (no change needed).
- Play-test pending: walk both the church and the taoist shrine â€” roofs should form a proper
  ridge at top center with eaves low (not a valley), for the main roof, the two-tiered shrine
  roofs, and the church spire roof. Existing saves reuse the same part names (no save impact).

## 1db. Character Info tab buttons ~1.3x taller (grow upward, content gap kept)

User: "raise the tab button in tab menu height" â€” clarified to the Character Info top tab strip
(Info / Skills / Inventory / Map / Faith) at ~1.3x.

- `BuildTopButtons` (CharacterInfoUI.cs:364-365): tab height `64f` â†’ `84f` (~1.3x); `anchoredPosition.y`
  16 â†’ 36 so the bar grows UPWARD â€” the button bottom edge stays where it was (âˆ’48), preserving the
  ~10px gap to `BodyRow` (top at âˆ’60) so content never overlaps.
- `OnLayoutFitted` (CharacterInfoUI.cs:477-478): height `56f` â†’ `84f` and `y` 6 â†’ 34 (bottom edge kept
  at âˆ’50). Both heights must change because the aspect-fit pass re-applies `56f * S` after build,
  otherwise the taller buttons would snap back to 56 on the first fit / window resize.
- Tab label (lines 374-386) is anchored to the full button rect (`offsetMin/Max Â±8f`), so it
  auto-stretches with the taller button â€” no label change needed.

### 1db-status
- Implemented; no CLI/Unity build (rule 3) â€” grep verification: both sizeDelta writes are now `84f * S`
  and both anchoredPosition writes are updated; no other reader of the tab height/number exists
  (`_tabButtonRects` only touched in `BuildTopButtons`/`OnLayoutFitted`).
- `game-design.md`: no change â€” no section documents tab-button pixel dimensions.
- Play-test pending: open Character Info at 16:9 and non-16:9 windows â€” tabs ~1.3x taller, still flush
  above the content panels with no overlap, labels centered, resize re-fits cleanly.

## 1da. Ground AoE placement is now unbounded â€” cast anywhere, not just within spell Range

User: the outdoor AoE spells can't be placed far away â€” the landing point is capped at the spell's
own short `Range`, so you can't drop a zone/summon/storm on something across the map.

Now the four **ground deliveries (Zone / Vortex / Summon / Storm)** land wherever the camera points,
out to a practical `SpellCaster.GroundAimMax` cap of **1200 units** â€” open-world placement instead
of the old spell-`Range` (typically ~12-30) clamp:

- `SpellCaster.Execute` (SpellCaster.cs:264) computes the aim direction from the camera for ground
  deliveries at `GroundAimMax` (vs `Mathf.Max(spell.Range, 5f)` for projectile/instant/beam), and
  passes `GroundAimMax` as the delivery range to `ResolveZone` / `SpawnVortex` / `ResolveSummon` /
  `ResolveStorm`. The resolvers' probe directions scale accordingly (`TerrainDeformer
  .ResolveGroundTarget`, `GroundTarget`, vortex raycast) â€” no other resolver logic changed.
- Preview stays in sync: `PlayerController.TryAoeTarget` (PlayerController.cs:1711) projects the
  landing ring along the camera to `SpellCaster.GroundAimMax` too (it's only called for the four
  ground deliveries via `UpdateAoePreview`).
- Projectile / instant / beam keep their spell-`Range` cap â€” only ground placement is opened up;
  1200 was chosen over `float.MaxValue` to keep the aim math safe from infinity edge cases.

### 1da-status
- Implemented + docs updated in one pass (`game-design.md` Â§3.8.1 "Ground placement is unbounded").
- Verification: no CLI/Unity build (rule 3) â€” grep + reread only (see THINKING.md `1da`): all four
  ground-delivery call sites in `Execute` now pass `GroundAimMax`; aim-direction branch only differs
  for the four ground deliveries; preview mirrors with the same constant; no other `spell.Range`
  usage in the aim path changed (SpellBeam, SpellEffect flight caps untouched).
- Play-test pending: aim a Zone/Storm/Summon/Vortex at objects far across the world (well past the
  old Range) â€” confirm the preview ring lands there, the delivery happens there, and projectile
  spells/beams still aim/behave exactly as before (unchanged shot range).

## 1cz. Church & Taoist Shrine rebuilt to pagoda scale and detail

User: make the church and shrine as big and detailed as the pagoda. Both were small boxes
(church 13Ã—9Ã—~9.6 in 7 parts; shrine 10Ã—8.5Ã—~6 in 6 parts) next to the ~21-tall, 15-part pagoda.

Now both structures match the pagoda's size and richness:

- **Church 16Ã—13, ~21 tall, 13 parts** (`_churchSubBuildings` WorldBuilder.cs:405 + `BuildChurchPart`
  WorldBuilder.Blueprints.cs:1408). Kept all 7 original part names (save-compatible) and added 6:
  `Church_Pillars` (2 arcade rings, gilded capitals), `Church_Belfry` (louvered bell stage),
  `Church_SpireRoof` (4 steep dark panels + gold trim + corner finials), `Church_Spire` (gold rings
  + ball + cross, ~20 u), `Church_Buttresses` (stepped along both sides + fronts), `Church_Interior`
  (pulpit, pews, altar + candlesticks + inner cross). Upgraded originals: widened nave w/ central
  aisle, **gothic arched windows** (glass + gold jambs + pointed arch) on both side walls, front wall
  flanks with gabled rose-window arch, apse (chamfered end walls) + big gold cross, wider gabled
  roof + gable fascia, and a **tall front steeple** (6.2 sq tower w/ door + rose window replacing the
  old 3-wide offset box).
- **Shrine 14Ã—12, ~13 tall, 12 parts** (`_shrineSubBuildings` WorldBuilder.cs:425 + `BuildShrinePart`
  Blueprints.cs:1645). All 6 original names kept; added `Shrine_Tier2Floor`, `Shrine_Tier2Walls`
  (lanterns + parapet), `Shrine_Roof2`, `Shrine_Spire` (jewelled gold spire, ~13 u tall),
  `Shrine_Deity` (robed taoist statue + golden crown + staff), `Shrine_Altar` (offerings table +
  candles + urns). Upgraded originals: bigger foundation + gold bottom step, full 8-column ring
  (+ mids) with gold brackets + ridge lintels, wall-mounted **yin-yang emblem** (gold ring, black/white
  swirl + dots) replacing the old hanging disks, larger 4-slope tile roof, and a **big tripod incense
  censer** (bronze legs/bowl/lid + smoke wisp) at the entrance.
- **NPCs follow the new footprints**: `ChurchExcludeHalf` 13â†’15, `ShrineExcludeHalf` 12â†’14
  (WorldBuilder.cs:50/54); priest moved to (27.5, -30) at the foot of the west-facing front steps
  (facing +X), taoist moved to (-40, -33.4) south of the shrine (WorldBuilder.cs:513/516). Test
  platform anchors updated the same way (NewWorldTestGround.cs:545-551); church/taoist anchors are
  grounded on the new full-width porch + front steps.

### 1cz-status
- Implemented + docs updated in one pass (`game-design.md` Â§5.7; arrays switched same-pass).
- Verification: no CLI/Unity build (rule 3) â€” grep + reread only: every new part name exists in BOTH
  the part array and its `Build*Part` switch (13 church + 12 shrine = 25/25, see THINKING.md `1cz`);
  no legacy part name was dropped (save-restore keeps working); no gameplay code references part
  names by string outside these two files.
- Play-test pending: walk the village to see the new church (west-facing steeple + priest at the foot
  of its new full-width front steps) and shrine (south-facing steps + taoist); confirm the front
  steps/doors align with the NPCs, tree/rock pruning radius now clears around the bigger footprints,
  and the test-ground religion lane places all three sites upright with their NPCs.

## 1cy. Comet / Meteor / Asteroid (etc.) now summon a big falling rock on cast

User: "the comet, meteor, ...etc skills are suppose to have effect of summon a big rock". Previously
the whole meteor/boulder line read as an instant ground flash â€” no rock anywhere, even though the
tooltips say "a burning meteor falls from the sky" / "a colossal mass of burning rock".

Now the sky/rock family visibly summons a **rock from the sky** that reads as the spell landing:

- **New `SpellData.SummonFallingRock`** flag + `summonFallingRock:` arg in the `Spell(...)` factory
  (SkillCatalog.cs:124). Set on **6 spells**: Fire Meteor, Asteroid, Earth Meteor (Zone), Comet
  (Projectile), Meteor Rain + Rockfall (Storm). Scorch stays a light-streak (it's a searing jet, not
  a rock).
- **New `SkillFx.FallRock(groundTarget, scale, tint, onImpact)`** (SkillFx.cs:153) â€” a ragged
  collider-less boulder (grey cubes, warm-tinted for fire) spawns ~30+ u above the target, falls with
  gravity, and on landing fires the `onImpact` callback + shards + ring flash, then shrinks away
  (self-contained `RockDrop` + `ShardFader` components, mirroring the existing RingFader pattern).
  Purely visual â€” **no collider** so it can never re-trigger the 1cx terrain-root knockback bug or
  ragdoll anyone.
- **Zone sky spells**: `SpellCaster.ResolveZone` (SpellCaster.cs:1000) â€” the existing burst body was
  extracted to `ResolveZoneImpact`; sky spells spawn `FallRock` and **defer damage/knockback/deform
  until the rock lands** (~0.6-0.8 s drop), so the cast reads "a meteor fell here".
- **Storm sky spells**: `SpellStorm.StrikeDelayed` (SpellStorm.cs:78) drops a smaller rock per strike;
  the strike's flash/damage/deform fire from the landing callback (storm cadence otherwise unchanged).
- **Comet projectile**: `Comet(...)` builder (SpellCaster.cs:748) now takes `rockBody`; the meteor-line
  Comet flies as a rough burning boulder + tail instead of a plain light streak.

### 1cy-status
- Implemented + doc updates in one pass (`game-design.md` Â§3.8/Â§3.8.1 + magic-skills.md rows/tables).
- Verification: no CLI/Unity build (rule 3) â€” grep + reread only (see THINKING.md `1cy`).
  `OnCastComplete` has zero subscribers, so deferring the return DamageResult harms nothing.
- Play-test pending: cast **Meteor (fire), Asteroid, Earth Meteor, Comet, Meteor Rain, Rockfall** â€”
  confirm a big rock drops from the sky and the damage/knockback/crater resolves on landing; confirm
  Scorch still flies as a light streak; confirm no terrain lurch (the rock has no collider).

## 1cx. TRUE root cause of "cast an AoE â†’ the entire terrain moves": knockback teleports the shared "Terrain" root

The 1cv/1cw width fixes were real but **not** the reported bug. Earth Wall still "moved the terrain",
and the original report was **Asteroid** â€” which has **no `terrainShape`** (SkillCatalog.Magic.cs:277),
so `TerrainDeformer` never runs for it. The actual mechanism, now CONFIRMED:

`SpellCaster.ResolveZone` does `Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer)` with the
**default layer mask** (SpellCaster.cs:1017) and calls `ApplyHit` on every collider it catches
(line 1042) â€” no `IDamageable`/layer guard, unlike `SpellZone`/`SpellStorm`/`SpellTornado`/
`SpellBeam`/`HitboxSystem`, which all filter. `ApplyHit` â†’ `ApplyKnockback` (SpellCaster.cs:1205-1213)
does `root.position += dir.normalized * spell.Knockback` where `root = target.transform.root` â€” and
**every streamed terrain chunk is a child of the single shared `"Terrain"` root**
(WorldStreamer.cs:447-451). So one zone cast shoves the WHOLE world's root by `Knockback` **per
overlapping chunk collider** (charge-scaled radius catches several chunks â†’ a large one-frame lurch),
seen from the player-pivoted camera as "the entire terrain moved". Enemies (`EnemyController`,
`BossController`, `SummonedAlly`) are the only `IDamageable`s, so real victims are unaffected.

Fix (single point): a guard at the top of `SpellCaster.ApplyHit` (SpellCaster.cs:1076-1081) bails out
with an empty `DamageResult` unless the target **or its transform.root** implements `IDamageable`.
This covers ResolveZone, `ResolveDirect` raycasts, and every `ResolveHitAt` caller (Beam/Storm/
Tornado/Zone/Effect â€” which already pre-filter, so the guard is defense-in-depth). It also stops
`DamageNumber`/status/knockback spam on terrain and props. Overcharge width/radius mechanic untouched
(deliberately â€” user wants no caps).

- **Verification**: no CLI/Unity build (rule 3) â€” grep (`ApplyHit`/`ResolveHitAt` call sites:
  SpellCaster.cs:451/1042/1131, SpellBeam.cs:181, SpellStorm.cs:153, SpellEffect.cs:243/246/334,
  SpellTornado.cs:95, SpellZone.cs:91; `IDamageable` implementers = EnemyController/BossController/
  SummonedAlly) + reread only. Unity play-test: cast **Asteroid, Earth Wall, Meteor** (charged and
  tap) and confirm the scene camera no longer sees the terrain lurch; enemies still take damage and
  get knocked back.

## 1cw. AoE *still* reshaped the whole chunk â€” raised shapes + projectiles fed the charge-scaled blast radius

After the 1cv fix, the crater (Meteor) carves only its local bowl, but the user reported the same
symptom ("still that bug where if the player uses an AoE skill it would move the entire terrain").
Root cause: `SpellCaster.ResolveZone` bounded ONLY the `Crater` shape â€” **every other shape
(`Ring`/`Spikes`/`Wall`/`Pillar`: Tremor, Spire Field, Earth Wall/Landslide, Stone Pillars) still
passed `deformRadius = radius = spell.Radius * sizeScale`** (SpellCaster.cs:997), and
`SpellChargeLevel` is deliberately **unbounded** (hold-to-overcharge, PlayerController.cs:1501),
so a charged raise rears every corner of a 30-tile chunk at once â€” the same "whole terrain moved"
read. Projectile path had the same leak: `SpellEffect.ResolveProjectileImpact` dented with
`dentRadius * _radiusMult` (charge sizeScale), so a charged Stone Shard carved a giant dent.

Fix (both paths): **terrain WIDTH is capped to the spell's authored delivery dish for EVERY shape â€”
`spell.Radius` (crater `* 0.5`), never the charge-scaled blast radius**:
- SpellCaster.cs:999-1001 â€” `dish = max(spell.Radius > 0 ? spell.Radius : 1.6, 0.5)`;
  `deformRadius = crater ? dish * 0.5 : dish`.
- SpellEffect.cs:262-270 â€” dent is `max(1.2, spell.Radius)` for Earth, fixed 1.4 m otherwise; no
  `* _radiusMult`. (This also makes the existing doc line "every non-Earth bolt leaves a fixed
  ~1.4 m dent" literally true again.)

Charge still enlarges the **damage** splash and zone/ring visuals (`radius` is unchanged); only the
*ground edit* is dish-capped. Storm (SpellStorm.cs:98 `max(Radius*0.55, 1.2)`) and Summon
(SpellCaster.cs:352 `min(Radius*0.4, 2.5)`) were already bounded â€” verified by grep+reread.
Crater **depth stays unbounded** (the 1cv ratchet, no floor â€” player's no-limit rule).

- **Verification**: no CLI/Unity build (rule 3) â€” grep (no `sizeScale`/`radiusMult` reaching any
  `TerrainDeformer.Apply`; call sites are SpellCaster.cs:352/1001, SpellEffect.cs:270,
  SpellStorm.cs:99, ToolManager Dig, test lanes) + reread only. Unity play-test should re-confirm
  that a **charged** raise/crater/projectile AoE carves only its local dish, never a whole chunk.

## 1cv follow-up fix: crater dish width reads `spell.Radius` â€” `1cv` shipped against a nonexistent field

The `1cv` commit compiled in review but not in Unity: `SpellCaster.ResolveZone`
(SpellCaster.cs:995-997) referenced `spell.DeliveryRadius`, yet `SpellData` has **no** such field
(the compile error reported by the user). The catalog's `deliveryRadius:` factory arg maps to
`spell.Radius` (SkillCatalog.cs:143), so the crater's local-dish width is now
`spell.Radius > 0 ? spell.Radius * 0.5f : 1.6f` (Meteor radius 4 â†’ ~2 m, matching the doc's
"~2 m for Earth Meteor"). No behavior change â€” same dish width as intended, verified by grep
(no `DeliveryRadius` left in `Assets\Scripts`) + reread. Code comment updated to say "delivery
Radius (the `deliveryRadius:` catalog arg Ã— 0.5)".

- **Verification**: no CLI/Unity build (rule 3) â€” grep + reread only. Unity play-test should
  re-confirm a Meteor/crater cast carves only the small ~2 m local bowl, not the whole chunk.

## 1cv. Crater dishes stay a LOCAL bowl â€” width bounded, depth UNBOUNDED (player: "no limit")

Earth **Meteor** (Meteor/asteroid-style) was feeding the full blast splash (`spell.Radius` Ã—
charge sizeScale, up to ~13+ tiles) straight into `TerrainDeformer.Apply` (SpellCaster.cs:
ResolveZone), lowering **every corner inside the whole 30-tile ChunkSize dish at once** â€” read in
play as "the entire chunk / the entire terrain moved." Fix (SpellCaster.cs:995-998): the crater's
**deform WIDTH** is the small local delivery dish (`DeliveryRadius Ã— 0.5`, ~2 m for Earth Meteor),
never the blast splash â€” one cast carves a bounded shallow bowl, verified idempotent-ish (repeat
cast re-carves only that same local bowl; the ratchet below stays). **DEPTH stays deliberately
unbounded with NO floor cap** â€” each cast ratchets `CraterStep` (~1.1 m) deeper with no limit, per
the player's "i want no limit on my game." Crater multi-cast deeply ratchets; raised shapes stay
idempotent/capped. Docs note updated in game-design.md Â§3.8 (width bounded, depth keeps no floor).
No new QA lane â€” the existing crater deform lane on NewWorldTestGround covers the shape.

HUD **status strip** under the HP/FP/Stamina bars â€” every active status polled off the player root
each frame: combat DoT/CC (Burn/DoT, Wet, Chill gauge, Blind) as colored square chips with seconds
left, plus the food/drink stamina-regen modifier (+20%/âˆ’50% STAM); opt-in `EnableStatusEffectsDemo`
QA lane self-applies all of them on the test platform), `1ct` (play-test
fixes for 1cs/1cr: the layered-terrain shader's two-`float3` `GetVertexNormalInputs` call was an
URP 17.5 compile error â†’ magenta "pink" terrain (now single-arg), and the weapon **draws only
while fighting again** â€” casual mode always sheathes, in any view â€” reverting 1cr's keep-drawn-in-
first-person rule which read as a fighting pose in normal mode), `1cs` (digging
now goes **infinitely deep and shows real strata**: craters/shovel/pickaxe excavate via one shared
`CraterStep` ratchet with no depth cap, and the terrain is vertex-colored at build time into
grass â†’ dirt â†’ stone bands revealed by depth â€” shovel stops at stone, pickaxe breaks it; opt-in
`EnableDigLayersDemo` QA lane), `1cr` (equipped
weapons stay **drawn in the hand in first person even out of combat** â€” third-person casual is the
only view that sheathes them, so holding a weapon in an inventory hand slot now visibly puts it on
your hand), `1cq` (Wind/Ice
projectile visuals are now translucent â€” the frost chip is a glassy alpha-0.5 diamond, the wind
blade an ethereal alpha-0.4 cross â€” and the fireball body flickers in `Ember` mode with a much
denser/ larger ember exhaust), `1cp` (the Earth
school's Stone Shard projectile is now a **tumbling cluster of grey rock debris** styled on the
world's breakable-rock chunks, and the carved crater throws up a short debris burst at impact),
`1co` (every race
gets its own look on the blocky player model â€” full palette skin/hair/eyes/clothes/pants/shoes plus
body ratios Height/Bulk/Head/ShoulderWidth/Arm/Leg; giants read bigger via raised global RigScale;
a race change rebuilds the model), `1cn` (player hair â€”
top cap raised clear of the skull, back hair lowered, on the standing/sitting/car player models),
`1cm` (Earth Wall
repeat cast no longer "moves the entire chunk" â€” deforms are now idempotent so a repeat cast
reproduces the exact same dish/ridge instead of stacking it higher; the zone aim probe skips raised
terrain; the chunk mesh/collider swap is atomic), `1cl` (dents carve
smooth per-corner dishes/ridges instead of flat walled step-pits â€” the "tile disappears instead of
changing shape" and "world still shrinking" fix; any flat tile relaxes on load so old carves read
as smooth terrain; merged mesh keeps tops-first so region patches can't corrupt a tile), `1ck` (terrain
persistence hardening on top of 1cj: legacy slab relaxation gated to whole-metre flats so smooth
Earth carves load back exactly as cast instead of "re-generating fresh"; merged chunk mesh made
hole-proof so a chunk rebuild can never drop a tile; opt-in world-save-reset QA lane), `1cj` (terrain
deformation reverted from 1cg's flat-slab blocks back to smooth feathered per-corner edits with
stacking/grinding caps restored â€” "world shrinking down" fixed; legacy slab saves re-smooth on
load), `1ci` (frost/ice
school remixed to distinct deliveries â€” lingering frost fields, hailstorm/avalanche storm strikes,
ice vortices, homing soul-chill, instant snap-freeze â€” following the fire remix), `1ch` (fire school
skills remixed to distinct deliveries â€” vortex/storm/instant/lingering-burn vs. slam/knockback â€” and
channeled **beam** magic now sweeps with the player's aim), `1cg` (earth
terrain deforms were made flat-topped 1x1x1 m slab stacks â€” since reverted by `1cj`), `1cf` (Alt magic grid keeps its scroll
position across close/reopen), `1ce` (Alt magic grid
click no longer casts â€” arm-only + attack-input suppression), `1cd` (100x percent-perk
multiplier bug + additive MoveSpeed â€” the real "still very fast" cause), `1cc` (super-speed root
cause + speed-aware fail-net), `1cb` (class + race locked to ONE choice), `1ca` (physics integrity
guard rails â€” no more one-step 5 km teleport), `1bz`
(no boot auto-teleport; spawn on the boot chunk), `1by` (build fixes), `1bx` (eight new talents), `1bw` (religion
structures + worship NPCs on the test ground), `1bv` (talents moved to the Info tab, talent-point
currency removed). The **optimization sweep** ran Phases 0-5
(`1ag`-`1al` below); the sweep's planning doc (`OPTIMIZATION.md`) was retired once Phases 0-5 shipped â€”
only **Phase 6 / startup** (#17, #18) remains open, recorded under OPEN TASKS â€” and that shipped in
**1e5** (registry + split init + gated sync boot chunk; see the top entry). Legacy working plans
(`PLAN.md`, `PLAN-class-skill-trees.md`, `planning.md`) were deleted; `game-design.md` is the single
durable design reference.

Companion docs: `game-design.md` (design), `GAME_DESCRIPTION.md` (player pitch).

---
## # OPEN TASKS

- **Axe/pickaxe bug** â€” the CODE fix shipped 2026-09-11 (see "Fix applied 2026-09-11" further down:
  lazy tree/rock registration + chunk-parent walk-up for the infinite-world spawn path). The only
  thing left is the **Unity play-test** (chop a tree, mine a rock). No known code gap.
- ~~**Performance sweep (1ea)**~~ â€” **SHIPPED** (entry at top): render config (SSAO/MSAA/opaque-copy off,
  1024Ã—2-cascade shadows), change-driven collider reconcile + cook budget, alloc-free dispatch, O(1)
  modified-tile set, rolling LOD burst, POI-scan gate, bench overlay. Open follow-ups: **play-test the
  A/B via `EnableFpsStats`**; HDR-off/render-scale and the enemy-bar registry refactor are parked
  (THINKING 1ea).
- ~~**Optimization Phase 6 â€” startup (#17, #18)**: #17 registry + split init (Core/GameBootstrap.cs
  scans, `OPTIMIZATION.md` legacy)~~ â€” **SHIPPED in 1e5** (see entry at top). #18 â€” boot spawn-chunk
  is now synchronous only for the non-platform fallback spawn (the default test-platform spawn
  streams its chunks like every other).

---

## 1cs. Infinite digging depth + layered terrain strata (grass â†’ dirt â†’ stone)

User: "Deeper we dig, the lower the ground gets. Can we add terrain layers (grass â†’ dirt â†’ stone)?"
â†’ scoped with the user: **discrete strata bands** (small blends at the cuts, not continuous
gradients), **infinitely deep** excavation (bounded only by the existing Â±200 m mesh-sanity band),
and **shovel digs the soft bands, pickaxe breaks the stone** (its rock-prop logic stays intact).
Key architectural choice: the band color is derived at mesh-build time from `pristine noise height
at the corner âˆ’ current vertex Y`, so **nothing is added to the save format** â€” untouched ground and
raised terrain render pure grass, and pits just show deeper bands every reload. Craters are
**deliberately no longer idempotent**: each cast/swing ratchets the floor one `CraterStep` down
(~1.1 m at full influence, feathered rim), reversing the `1cm` "crater floor clamp" for excavation
only â€” raised shapes keep their `Max`-cap idempotency.

### Changes
- `ColorPalette.cs` â€” new `DirtBrown` (0.45, 0.33, 0.21).
- `TerrainChunkMeshData.cs` â€” `MergedChunkMeshData.Colors` (Color[]).
- `ChunkMeshGenerator.cs` â€” public band constants (`DirtBandStart 0.35`, `DirtBandEnd 0.65`,
  `StoneBandStart 2.3`, `StoneBandEnd 2.7`); new `TerrainBandColor(seed, worldX, worldZ, vertexY)`
  (grass â‰¤0.35, grassâ†’dirt blend, dirt, dirtâ†’stone blend, stone â‰¥2.7); `BuildMergedMeshData` fills
  colors on every top quad corner (world coords from `tile.Coord`) and every side-wall band vertex
  (`tiles[0].Coord` + local edge coords, i.e. the chunk's min-tile world corner); `CreateMeshFromMerged`
  uploads `mesh.SetColors` when present.
- `ChunkObject.cs` â€” `PatchRegion(..., long seed)` recomputes the region's top-vertex colors and
  lazily back-fills a grass default if the CPU copy had none; re-upload includes `SetColors`, collider
  re-cook unchanged.
- `WorldStreamer.cs` â€” `PatchRegion` call site passes `Seed`; crater branch rewritten to
  `target = current âˆ’ sÂ·CraterStep` (unbounded excavation; `CraterMaxDepth` const removed); new
  `GetDigDepth(worldX, worldZ)` = pristine noise height âˆ’ `CurrentHeightOf` (the tools' gate).
- `TerrainDeformer.cs` â€” `Dig(center, radius)` (shares the Crater shape) + `DigDepthAt(point)`.
- `Assets/Shaders/TerrainLayered.shader` â€” new URP lit shader: ForwardLit (vertex color Ã— `_Color`,
  URP lighting + fog) + ShadowCaster + DepthOnly; fallback `Universal Render Pipeline/Lit`.
- `GameBootstrap.cs` â€” `GroundMaterial` now uses the layered shader with a white base (vertex colors
  carry the look); falls back to URP Lit.
- `ToolManager.cs` â€” **shovel** branch digs terrain (radius 0.55) but refuses once
  `DigDepthAt â‰¥ StoneBandEnd` ("ÄÃ¡ cá»©ng â€” dÃ¹ng cuá»‘c chim!"); **pickaxe** branch excavates terrain at
  ANY depth (radius 0.5) before its unchanged rock-prop handling; both spend tool stamina, play their
  sound, and pop a small tinted `SpawnDigPuff` (dirt-brown / stone-gray shards, ~1 s lifetime).
- `NewWorldTestGround.cs` â€” opt-in `EnableDigLayersDemo` lane casts 2Ã— and 4Ã— craters on the streamed
  terrain east of the platform (through dirt, then into stone) to reveal the banding.
- Docs: `game-design.md` Â§3.7 Earth bullet + Â§3.8 terrain-shape bullet + idempotency paragraph
  (crater carve-out), Â§5.1 tools table (shovel/pickaxe excavation), Â§2.6 unchanged (no save-format
  change); `magic-skills.md` delivery legend; `PROGRESS.md` this entry; `THINKING.md` `## 1cs`.

### 1cs-status
- Source-compile verified by review (rule 3; no CLI/Unity build â€” user play-tests): greped every
  `BuildMergedMeshData`/`CreateMeshFromMerged`/`PatchRegion` call site (both `PatchRegion` refs match
  the new 6-arg signature), no lingering `CraterMaxDepth`/`DirtBlendStart` refs, band consts renamed
  consistently, `Colors` allocated once per merged mesh, wall-pass origin = `tiles[0].Coord`,
  `SetColors` in both build and patch paths, terrain shader present at `Assets/Shaders`.
  **The one thing review can't prove is the shader compiling under URP 17.5** â€” if the terrain turns
  pink/mra in play-test, that's the first thing to check (fallback is URP Lit).
- Play-test checklist: (1) cast a Crater twice â†’ the pit visibly deepens; repeat casts keep digging
  through dirt into stone, memorable pit walls show grass ring â†’ dirt band â†’ stone face; untouched and
  raised ground stays grass-green; (2) shovel on grass digs; shovel at rock-hard stone depth is
  refused with the hint; pickaxe then excavates the pit further; (3) reopen the world â†’ bands persist
  exactly (save format untouched); (4) walls/ridges still capped at first cast height; shadows +
  lighting still render on the carved terrain.
- Follow-ups noted: `SpawnDigPuff` shards are unparented (harmless, self-destruct ~1 s); if the
  per-swing puff proves noisy in play-test, gate it behind the tool sound or drop to 2 shards.

---

## 1cu. Player HUD status strip under the bars (show active status effects)

The player's HUD now lists every active status directly under the HP/FP/Stamina bars as a row of
**colored square chips with text** (wraps past 4). User's ask: "show status effect that they're
having under their bars". Chosen scope/style: combat statuses **plus** the food/drink stamina buff,
rendered as **colored square + text**.

### Changes
- `Assets/Scripts/Combat/Effects/WetStatus.cs` + `BlindStatus.cs`: added a public
  `Remaining => Mathf.Max(0f, _expiresAt - Time.time)` read for the HUD (behaviour unchanged).
- `Assets/Scripts/Player/PlayerController.cs`: exposed `StaminaBuffRemaining` / `HasStaminaBuff`
  from the existing `_staminaRegenModifierUntil` timer set by `ApplyStaminaRegenModifier`
  (ToolManager food/drink call it â€” +20% / âˆ’50% for 120 s). No gameplay change.
- `Assets/Scripts/UI/NewWorld/PlayerBarsHUD.cs`: new "StatusPanel" under the charge-bar slot
  (`top - BarSpacing*4`) with a `GridLayoutGroup` (4 columns) of 10 pooled chips. Each frame polls
  the player root: `SpellDoT` (Fire â†’ "BURN ns", other â†’ "DOT ns"), `WetStatus` ("WET ns"),
  `ChillStatus` ("CHILL n/5" while the gauge is > 0 â€” a full freeze self-destroys the component so
  it can't be displayed), `BlindStatus` ("BLIND ns"), and the stamina buff ("+20% STAM ns" /
  "âˆ’50% STAM ns"). Chips enable/disable and re-color/re-text only on change; panel height grows to
  fit wrapped rows. Canvas (`ShowOnInGame`) already gates the whole thing with the bars.
- `Assets/Scripts/Opt/NewWorldTestGround.cs`: opt-in `EnableStatusEffectsDemo` lane
  (`SpawnStatusEffectsDemo`, via `RunSafely`) that self-applies to the player root: Wet (4 s),
  then Burn DoT (`SpellDoT.Apply` Fire, 6 s â€” after Wet so the douse doesn't cancel it), two
  wet-conducted Chill stacks (+2 each = 4/5, keeps the gauge visible without the freeze path),
  Blind (4 s), and the food stamina buff (1.2Ã—, 120 s). No world placement â€” applies to the player
  directly. All applications refresh on re-run.

### 1cu-status
- Source-review verified (rule 3; no CLI/Unity build â€” user play-tests): greped every caller of the
  changed members â€” `WetStatus.Remaining`/`BlindStatus.Remaining` are new additive reads (no other
  consumers to break), `PlayerController` gained only two read-only properties, `PlayerBarsHUD`
  reads only components that already exist on the player root, and `NewWorldTestGround` uses only
  existing static `Apply` APIs + the public `ApplyStaminaRegenModifier`.
- Play-test checklist: (1) tick `EnableStatusEffectsDemo` on the test platform â†’ the strip under the
  bars shows BURN/WET/CHILL/BLIND chips with counting-down seconds ("ns") and the green "+20% STAM";
  (2) chips wrap to a second row when many; (3) eating food / drinking in the world shows the stamina
  chip with the correct sign and seconds; (4) when nothing is active the strip is empty/hidden.

---

## 1ct. Play-test fixes: pink terrain shader (1cs) + weapon drawing in casual mode (1cr revert)

Two play-test fixes after 1cs shipped â€” one for each reported bug.

### Changes (pink terrain)
- Root cause: the new `TerrainLayered.shader` ForwardLit vertex called
  `GetVertexNormalInputs(input.normalOS, input.normalOS)`. URP 17.5 only defines the overloads
  `GetVertexNormalInputs(float3)` and `GetVertexNormalInputs(float3, float4 tangentOS)`
  (`ShaderLibrary/ShaderVariablesFunctions.hlsl:22,31`), so the two-`float3` call is an HLSL
  compile error â†’ the whole subshader is rejected â†’ SRP renders the material **magenta** (FallBack
  is not used under URP) â†’ "pink terrain" while trees/rocks (their own working URP Lit materials)
  looked fine. Every other API the shader uses was verified against the 17.5 package cache
  (`UniversalFragmentPBR(InputData, SurfaceData)` at `Lighting.hlsl:302`, `TransformWorldToShadowCoord`
  via `RealtimeLightsâ†’Shadows`, shadow-caster `_LightDirection`/`_LightPosition`/`ApplyShadowBias`,
  `MixFog`/`ComputeFogFactor`) â€” the overload was the only error.
- Fix: `Assets/Shaders/TerrainLayered.shader` â€” `GetVertexNormalInputs(input.normalOS)` (single-arg;
  no tangent needed since the terrain has no normal mapping). The test platform top uses the
  streamer's `GroundMaterial`, so it un-pinks automatically.

### Changes (weapon pose revert, per user choice "draw only while fighting")
- Root cause: `1cr` set `PlayerController.WeaponsDrawn => FightingMode || firstPerson`, so the
  default first-person view always showed the equipped weapon at port arms â€” a "fighting pose" even
  in normal/casual mode, incl. at boot via the test-ground's `SpawnAllWeapons` â†’ `ReApplyWeaponPose`.
- Fix: `PlayerController.cs` â€” `WeaponsDrawn => FightingMode` (+ doc/comments on the property, the
  casual-mode toggle, `ReApplyWeaponPose`, and `LoadPlayerModel` re-rig). All downstream paths flow
  through that single property: casual toggle sheath, `CameraModeSwitch.SetMode` re-pose (now a
  harmless no-op in casual), `CharacterInfoUI` equip/cycle previews, respawn/model-rebuild re-poses.
  `NewWorldTestGround.SpawnAllWeapons` now sheathes the starter weapon at boot.
- Docs: `game-design.md` Â§3.6 draw-vs-stow bullet + Â§5.5 Hand States draw/stow â†’ fight-only rule
  with the 1cr exemption removed (noted as reverted in 1ct); `PROGRESS.md` this entry;
  `THINKING.md` `## 1ct`.

### 1ct-status
- Follow-up fix (same tag, fourth commit): terrain AND test-ground platform still rendered black
  after the shadow-coord change. New evidence: (1) the platform top is a stock `PrimitiveType.Cube`
  sharing `GroundMaterial` â€” a cube has NO vertex colors, so the layered shader's
  `_UseVertexColor=1` resolved its albedo to black; (2) the terrain mesh genuinely carries green
  vertex colors (`BuildMergedMeshData` -> `Colors` -> `mesh.SetColors`), so its blackness is the
  PBR plumbing, not the albedo. Decision (user: "fuck the shadow"): the ForwardLit pass no longer
  samples shadows or uses `UniversalFragmentPBR`/`InputData`/`SurfaceData` at all. It now shades
  with an explicit, source-verified Lambert + sky ambient (`GetMainLight()` RealtimeLights.hlsl:89,
  `LightingLambert` Lighting.hlsl:32, `SampleSHVertex` GlobalIllumination.hlsl:45, reachable via
  the Lighting.hlsl include chain) + fog. Direct light is always positive while a main directional
  sun exists, so the terrain cannot silently go black. The terrain still CASTS (ShadowCaster pass
  kept); it just doesn't receive realtime shadow maps. Also: `NewWorldTestGround.PlatformMaterial`
  now returns a plain URP Lit grass material instead of reusing the streamer's layered `GroundMaterial`
  (the cube slab has no strata bands to show and would stay black otherwise).
- Follow-up fix (same tag, third commit): after the two compile fixes the shader compiled but the
  terrain rendered **blue by day, black at night** (direct sun was missing; only sky/ambient lit it).
  Root cause: my ForwardLit computed `lightingInput.shadowCoord = TransformWorldToShadowCoord(...)`
  in the **fragment**, but URP's screen-space shadow variant (`_MAIN_LIGHT_SHADOWS_SCREEN`, on in
  the current URP asset) needs the coord from `GetShadowCoord(vertexInput)` â€” which returns
  `ComputeScreenPos(positionCS)` under that keyword (Shadows.hlsl:529-536). Feeding it shadow-atlas
  coords sampled garbage -> shadowAttenuation ~ 0 -> `UniversalFragmentPBR`'s
  `GetMainLight(inputData,...)` returned no direct light. URP Lit trees/rocks use `GetShadowCoord`
  correctly, which is why only the terrain broke. Fix: compute `output.shadowCoord = GetShadowCoord(posInputs)`
  in the vertex (interpolated to the fragment) exactly like URP Lit, plus
  `lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS)`.
  (Later superseded by the fourth commit's shadow-free Lambert rewrite.)
- Follow-up fix (same tag, second commit): the ShadowCaster pass reported
  `undeclared identifier '_LightDirection'` at `TerrainLayered.shader(145)` on d3d11. URP 17.5
  declares `float3 _LightDirection; float3 _LightPosition;` inside its own utility file
  `Shaders/ShadowCasterPass.hlsl:13-14` â€” not in `Shadows.hlsl`/`Lighting.hlsl` â€” so including
  `Shadows.hlsl` alone never put them in scope. Fixed by declaring the two `float3` globals in the
  caster's `HLSLPROGRAM` block (matching URP's own declarations exactly).
- Source-compile verified by review (rule 3; no CLI/Unity build â€” user play-tests): shader overload
  fixed and the rest of `TerrainLayered.shader` re-read against URP 17.5 APIs; greped every
  `GetVertexNormalInputs` (no other misuse exists), `WeaponsDrawn`/`ReApplyWeaponPose` refs flow
  through the single property, no stale "drawn in first person" comments/docs remain.
- Play-test checklist: (1) terrain renders green/tan lit (not magenta) and the platform top matches;
  shadows + fog still work; (2) spawn in first person casual â†’ weapon is sheathed (no port-arm pose);
  fight â†’ draws on the hand; F5 third person â†’ still sheathed in casual; equip/cycle a weapon from
  Character Info â†’ stowed while casual.

---

## 1cr. Weapon drawn on hands in first person even out of combat

User: "weapon not visible on hand when held in inventory slots" â†’ clarified as **invisible only when
not fighting**. Root cause: out-of-combat weapons are stowed onto body anchors (`StowBack`/
`StowWaist` under Torso); in first person (the default view) those anchors sit behind the camera, so
the equipped weapon could never be seen until Fighting Mode (draw) or switching to third person.

### Changes
- `PlayerController.cs` â€” new `WeaponsDrawn => FightingMode || (_cameraMode?.IsFirstPerson ?? false)`
  and `ReApplyWeaponPose(instant)` (no-op until the combat stack exists). `ToggleCombatMode` casual
  and fighting branches, `LoadPlayerModel`, and the post-reload re-rig block all route through it now.
  `SetupPlayerCamera` caches the `CameraModeSwitch` ref.
- `CameraModeSwitch.cs` â€” `SetMode` calls `_player.ReApplyWeaponPose()` so an F5 view cut re-poses
  weapons (first person â‡’ draw, third-person casual â‡’ stow).
- `CharacterInfoUI.cs` â€” both equip call sites (`EquipOwnedWeapon`, `CycleWeapon`) use `WeaponsDrawn`
  instead of `FightingMode`, so equipping a weapon from the gear sheet shows it in hand immediately
  when first person.
- `NewWorldTestGround.cs` â€” test-platform boot starter now poses via `ReApplyWeaponPose` (first person
  at boot â‡’ starter weapon visible on hand) with the old sheathed fallback if no controller.
- Behavior: fighting â‡’ always drawn; first-person (fighting or casual) â‡’ drawn; only **third-person
  casual** sheathes to the back/waist. Combat toggles keep the animated draw/stow transition.
- Docs: `game-design.md` Â§3.6 Notes (draw-vs-stow rule) + Â§5.5 Hand States (draw/stow by view);
  `PROGRESS.md` this entry; `THINKING.md` `## 1cr`.

### 1cr-status
- Source-compile verified by review: greped every `ApplyPose(`/`WeaponsDrawn`/`ReApplyWeaponPose`
  call site and reread `ToggleCombatMode`, `LoadPlayerModel`, `CameraModeSwitch.SetMode`. No CLI/Unity
  build (rule 3) â€” user play-tests.
- Play-test checklist: equip a weapon into a hand slot while casually in first person â‡’ visible on
  hand; F5 to third person while casual â‡’ sheathes on back/waist; F5 back â‡’ drawn; toggling combat in
  third person â‡’ animated draw/stow; race/gender change in second â†’ weapon stays re-rigged in view.

---

## 1cq. Translucent Wind/Ice + denser Fire projectiles

Request: **"wind, ice magic projectile should be transparent, cover fire magic projectile with more
particle to be more fiery."** All changes are in the procedural projectile-visual pipeline
(`SpellCaster.cs`); no new assets. The `"Sprites/Default"` shader already supports alpha blending
via `material.color.a`, so no shader or pipeline work was needed.

### In-flight changes (`SpellCaster.cs`)
- **Wind Blade** (`Blade` builder): both cross-cube primitives now get a translucent color
  `Color(r, g, b, 0.4f)` â€” the "Sprites/Default" shader blends with the alpha channel, making
  the blade read as a ghost of air rather than a solid painted prop.
- **Ice Shard** (`Shard` builder): the diamond-cube's material now gets alpha 0.5
  (`new Color(r, g, b, 0.5f)`) â€” semi-transparent glassy crystal, so the frost chip reads as ice
  rather than plastic.
- **Fireball Sphere** (`Orb` via the `default` case): `OrbFx.Mode.Plain` â†’ `Mode.Ember` â€” the
  fireball body now flickers with a fast warm two-sine irregular pulse (same animation as the
  Comet shape), instead of the previous gentle breathe.

### Exhaust particle changes (per-element particle tuning methods)
- `EmissionRate(Fire)`: 90 â†’ 150 particles/s (already the densest non-Lightning stream).
- `MaxParticles(Fire)`: 400 â†’ 700 (sustains the dense exhaust during longer flights).
- `StartSize(Fire)`: 0.09 â†’ 0.12 (noticeably larger ember motes).

### 1cq-status
- **`SpellCaster.cs`** â€” `Blade(...)` translucent alpha 0.4; `Shard(...)` translucent alpha 0.5;
  `BuildProjectileBody` default sphere â†’ `Mode.Ember`; `EmissionRate(Fire)` 90â†’150,
  `MaxParticles(Fire)` 400â†’700, `StartSize(Fire)` 0.09â†’0.12. Signature changes: none public â€”
  all are private static local to the visual builder.
- **`game-design.md`** â€” Â§3.8 Projectile Shapes table: **Sphere** row updated to flicker (Ember) +
  dense exhaust; **Shard** row = translucent glass; **Blade** row = translucent (alpha ~0.4);
  paragraph below the table notes translucency depends on `"Sprites/Default"` shader.
- **`magic-skills.md`** â€” matching shape-table rows updated.
- **Verification**: no CLI/Unity build per project rule 3 â€” confirmed that `"Sprites/Default"` honours
  alpha via existing `CCZone`/`CastingCircle` precedent; grepped the full pipeline to ensure no
  other material site on Wind/Ice shapes would override alpha; confirmed Comet/Scorch/Burn use
  the dedicated `Comet(...)` builder (untouched).
- **Open questions**: none. *Play-test note*: cast Wind Blade + Ice Lance / Chill Touch / Frost Bolt
  to confirm the translucent bodies are visible and readable in-flight (not too transparent);
  cast Fireball, Scorch, and Comet to confirm the denser, larger ember exhaust and the flickering
  fireball body.

### Play-test (pending, user)

---

## 1cp. Earth magic projectile = rock debris (in-flight cluster + impact burst)

Request: **"use rock debris as earth magic projectile."** The Earth school's only projectile spell,
**Stone Shard**, previously flew as a single polished diamond `Shard` (a shape it shared with frost
chips via Ice's `Auto`). Confirmed with the user: rock debris **in flight** and a **short debris
burst at impact** (when the crater is carved).

### In-flight (`SpellCaster.cs`, `SpellData.cs`)
- New `ProjectileShape.Debris` (renamed enum entry after `Dart`).
- New `Debris(...)` builder â€” 5-7 collider-stripped cubes of mixed scale (0.05-0.11 m, one ~0.14 m
  leading chunk), random rotations, clustered with the leader ahead and the tail trailing along the
  flight line. Colored exactly like the world's breakable-rock debris
  (`Color.Lerp(Color.gray, Color.black, Random.value * 0.5f)`, mirroring
  `WorldBuilder.RockMining.SpawnRockDebris`), with two chunks dusted in the Earth accent
  (`DamageNumber.ColorFor(Earth)` = 0.78/0.62/0.42) so it reads as magic, not terrain.
- `OrbFx` gains `Mode.Tumble` â€” each chunk captures `Random.onUnitSphere` as its spin axis in `Start`
  and rotates around it at ~120Â°/s (existing modes still spin the old Y-axis path, untouched).
- `AutoShapeFor(Earth)` â†’ `Debris`, so summoned-turret Earth shots (`SpellSummon` â†’
  `DecorateProjectile`) and any future Auto-shape Earth spell pick the same look.

### Impact burst (`SpellEffect.cs`)
- `ResolveProjectileImpact`: in the `TerrainShape.Crater` branch, after the carve resolves, spawn
  3-5 grey rock cubes (0.08-0.16 m, `Lerp(gray, black, rand*0.5)`), `Rigidbody` mass â‰ˆ `sÂ³Â·1000`,
  up-biased outward scatter (up 2-4, lateral Â±2.5), `Random.insideUnitSphere*6` tumble, named
  `SpellImpactDebris`, destroyed after 2.5 s (no litter, no pickup interference).

### Skill wiring (`SkillCatalog.cs`)
- `magic_earth` "Stone Shard": `projectileShape: ProjectileShape.Shard` â†’ `ProjectileShape.Debris`
  (the only Earth projectile skill; Meteor / Earth Wall are Zone delivery). `Shard` stays reserved
  for frost via Ice's `Auto`.

### 1cp-status
- **`SpellData.cs`** â€” `ProjectileShape.Debris = 11`.
- **`SpellCaster.cs`** â€” `Debris` shape â†’ `BuildProjectileBody`; new `Debris` builder; `OrbFx.Mode.Tumble`
  (+ per-object random spin axis). Signature changes: none public â€” enum gained an entry only.
- **`SkillCatalog.cs`** â€” Stone Shard line flipped to `Debris`; desc now "a fistful of living rock".
- **`SpellEffect.cs`** â€” `SpawnImpactDebris(pos)` helper + call in the crater branch.
- **`game-design.md`** â€” Â§3.8 Crater signature line (debris burst at impact) + Projectile Shapes table
  (new **Debris** row, **Shard** row narrowed to frost chips, Auto Earthâ†’Debris, OrbFx mode list).
- **`magic-skills.md`** â€” shape table (Debris row, Shard row), Auto-resolve line, Stone Shard row.
- **Verification**: no CLI/Unity build per project rule 3 â€” greps confirmed the only Earth projectile
  is Stone Shard, `ProjectileShape.Shard` explicit usage reduced to none in spell tables (Ice still
  routes via Autoâ†’Shard), and no other callers reference the changed paths; full re-read of the four
  touched methods done.
- **Open questions**: none. *Play-test note*: cast Stone Shard charged and uncharged (cluster should
  scale with charge), confirm chunks tumble (not drill), confirm the impact burst pops from the
  crater, and confirm an ice Auto spell (Chill Touch) still shows the old diamond shard.

### Play-test (pending, user)

---

## 1co. Race looks on the block player model (palette + body ratios, race-aware)

Request: **"take current player model as human base, and color according to race, change body ratio to
fit the race description."** Every race now colors and proportions the shared blocky player model.
Decisions confirmed with the user: full palette (clothes/pants/shoes also change), six ratio knobs,
race-aware wherever the model is built (cutscenes with no player parent stay Human), and **raise** the
giant races via a bigger global scale.

### Data (`RaceData.cs`, `RaceDatabase.cs`)
- `RaceData` gains a **Model Palette** group â€” `SkinColor, HairColor, EyeColor, ClothColor, PantsColor,
  ShoeColor` (defaults = original Human colors) â€” and a **Body Ratio** group â€” `BodyHeight, BodyBulk,
  BodyHead, BodyShoulderWidth, BodyArm, BodyLeg` (all 1).
- `RaceDatabase.Make(...)` now also fills the look; every one of the 22 roster entries got values
  (Human = exact original palette, all ratios 1 â†’ strict visual no-op).
- **Global scale raised**: Fire Giant 1.25â†’**1.35**, Ice Giant 1.3â†’**1.4**, Golem 1.3â†’**1.4**,
  Draconic 1.15â†’**1.2**. Small races keep their contract (Goblin 0.8, Gnome 0.7 â†’ small hitbox).
- `RaceDatabase.DefaultRoster` is now a **cached** build â€” `RaceChangeManager.ActiveRace` reuses it
  instead of re-instantiating 22 ScriptableObjects on every get.

### Rendering (`MapBuilder.PlayerModels.cs`, `RaceRig.cs`, `PlayerController.cs`)
- New `MapBuilder.ResolvePlayerRace(parent)` reads `RaceChangeManager` on the parent (Human fallback
  for `null`), and `ApplyRaceLook(root, race, calibrateFeet)` applies Height/Bulk to the root,
  head-scale to Head/Neck/Eye*/Hair*/Ponytail* (recursive â€” they live under the Torso pivot in the
  standing model), Arm+spread to the shoulder pivots, Leg to the hip pivots; the standing builder
  re-plants the feet (`localPosition.y = h*(0.25 + 0.62*leg)`). All-1 ratios exit immediately.
- All three builders (standing / car-seated / sitting) swap the hardcoded colors for the race
  palette; skirt = cloth color with a ~0.6Ã— darkened hem. Human output is pixel-identical.
- `RaceRig.ApplyRace` keeps the uniform `Body.localScale = RigScale` + prefab tint, but **no longer
  flat-tints the block model** (its colors come from the race now).
- `PlayerController.LoadPlayerModel` subscribes **once** to `RaceChangeManager.OnActiveRaceChanged`
  (`_raceSubscribed` guard) â†’ a mid-play race change rebuilds the model with the new look and
  re-seats held weapons. (Race still isn't saved/restored â€” on load the player is Human; existing
  behavior, unchanged.)

### 1co-status
- **`RaceData.cs`** â€” 12 new serialized fields (6-color palette + 6 ratio knobs), defaults = Human.
- **`RaceDatabase.cs`** â€” cached `DefaultRoster`; `Look` struct; all 22 races populated; 4 giant
  RigScales raised; `Make` signature extended (internal, one restore path â€” no asset/consumer edits).
- **`RaceChangeManager.cs`** â€” `ActiveRace` uses the cached roster.
- **`RaceRig.cs`** â€” block-model flat-tint removed; scale + prefab tint kept; TintRenderers field dropped.
- **`MapBuilder.PlayerModels.cs`** â€” `ResolvePlayerRace` / `ApplyRaceLook` / `ApplyRaceRatioRecurse` /
  `Darken` added; 3 builders race-aware (null parent â†’ Human). No signature changes to the public
  builders, so all existing call sites (spawn, gender, cutscenes) are unaffected.
- **`PlayerController.cs`** â€” `_raceSubscribed` + idempotent `OnActiveRaceChanged` â†’ `LoadPlayerModel`.
- **`game-design.md`** â€” Â§3.5 Race Visuals rewritten (palette + ratios + raise + race-aware), change
  flow line updated to "model rebuilds".
- **Verification**: no CLI/Unity build per project rule 3 â€” greps + full re-reads confirmed no stray
  reference to the removed `RaceRig.TintRenderers` and no other hardcoded player-part colors outside
  the roster; existing call sites of the three public builders are untouched.
- **Open questions**: none. *Play-test note*: the giants, Dwarf/Gnome, Elf/Harpy and the car-cutscene
  human are the high-signal checks; confirm mid-play race change rebuilds visuals and held weapons
  re-seat on the fresh hands.

### Play-test (pending, user)

---

## 1cn. Player hair: top cap raised clear of the head, back hair lowered

Request: **"the player top hair is overlap with head, raise top hair higher, lower backhair."** The
blocky player model's top "Hair" cap was clipping into the skull and the "HairBack" slab rose past
the crown.

Head geometry (standing model): 0.3 m cube centred at y 0.65 â†’ head spans y `[0.50, 0.80]`. The top
cap (centre 0.82, height 0.08) spanned `[0.78, 0.86]` â€” intersecting the head top by 0.02 m. The
back-hair slab (centre 0.70/0.72, height 0.26â€“0.30) topped out at y 0.85, above the crown.

### Fix (`MapBuilder.PlayerModels.cs`, all three player-model builders for consistency)
- **Standing (`BuildPlayerModel`)** â€” top cap 0.82 â†’ **0.88** (spans `[0.84, 0.92]`, clear of the 0.80
  crown); HairBack 0.70/0.72 â†’ **0.62** (spans `[0.47, 0.77]`: hangs from the lower back of the head
  down to the neck, below the crown).
- **Driving cutscene (`BuildSeatedPlayerModel`)** â€” top cap 0.90 â†’ **0.94**; HairBack 0.80 â†’ **0.72**.
- **Sitting (`BuildSitPlayerModel`)** â€” top cap 0.95 â†’ **1.0**; HairBack 0.83/0.85 â†’ **0.74**.

The female hair band, side hair and ponytails were left untouched â€” the band sits under the raised cap
and the ponytail still reads as attached to the lowered back hair.

### 1cn-status
- **`MapBuilder.PlayerModels.cs`** â€” top "Hair" cap raised (0.88 / 0.94 / 1.0) above the head cube in
  the standing, seated and sitting builders; "HairBack" lowered (0.62 / 0.72 / 0.74) to hang below the
  crown. Band, side hair and ponytails unchanged. No signature/consumer changes (position-only edit).
- **Verification**: no CLI/Unity build per project rule 3 â€” grep + full re-read confirmed only the
  intended block positions changed and no other call sites reference these hair offsets.

### Play-test (pending, user)
In Unity, check the player model standing, sitting, and in the car cutscene: the top hair reads as
sitting on the skull with no clipping, and the back hair hangs off the lower back of the head/neck
instead of poking past the crown.

---

## 1cm. Earth Wall repeat cast: deforms are now idempotent ("entire chunk moving" on cast 2+)

Request (after 1cl shipped): **"when cast earthwall it only work the first time and the next time it
make the entire chunk moving."** The first pass was a docs-only investigation (raw trail in
`THINKING.md â†’ ## 1cm`); this pass found and fixed the root cause.

### Root cause
`WorldStreamer.DeformAt` built the new height **additively from the current height**, so it was not
idempotent â€” raise was `value = current + s*lift` (then clamped to `noise + lift`), crater was
`max(current âˆ’ s*1.8, noise âˆ’ 1.8)`. Because `current` already contains the previous cast's raise, a
repeat cast added the raise **again**, lifting the whole influence footprint toward the cap on every
cast (the wide low-influence flanks included) â€” so the ground visibly rose across the chunk on cast
#2+ ("the entire chunk moving"). Craters compounded the same way (deeper each cast). `1bo`'s absolute
cap bounded the final height but did **not** stop the compounding. (The earlier docs/`1cl` comment
claimed repeats were a no-op â€” that was wrong; the cap only limits how far it compounds.)

### Fix (all three scopes chosen by the user)
1. **Idempotent `DeformAt`** (`WorldStreamer.cs`): raised shapes raise toward `noise(corner) + s*lift`
   applied with `Mathf.Max(current, target)`; craters dig toward `noise(corner) âˆ’ s*1.8` applied with
   `Mathf.Min(current, target)`. A repeat cast recomputes the same target â†’ **nothing changes**. Spikes
   keep their deterministic bonus, re-clamped to `noise + lift`; `Max` also means a deform can never
   *lower* terrain above the target. Dead `floorY`/`ceiling` locals removed; XML doc + comments updated.
2. **Aim probe skips raised terrain** (`TerrainDeformer.ResolveGroundTarget`, used by
   `SpellCaster.ResolveZone`): the zone aim ray steps past a hit that is the chunk's OWN
   `ChunkObject` terrain collider **and** sits above pristine noise + 0.25 m (a wall/ring/pillar the
   spells reared), so a repeat cast targets the ground the player is aiming at, not the wall face.
   Normal ground, craters, entities, buildings and props are never skipped; triggers ignored; falls
   back to the ground under the aim point at max range.
3. **Atomic collider/mesh swap** (`ChunkObject.ApplyMerged`): filter + collider are re-pointed at the
   new mesh **before** the old mesh is destroyed, so nothing references a destroyed mesh across a
   physics step. `PatchRegion`'s `null â†’ assign` recook is kept (single synchronous call, never
   observed by physics) with a clarifying comment.

### Verification
Read + grep (rule 3, no build): no remaining `current Â± s` deform site; the only other height write,
`FlattenAt`, already targets a fixed height (pad corners fully `Lerp` to it, rim corners converge
toward it without overshoot â€” stable, never compounds); `floorY`/`ceiling` gone; the
`GetHeight(long,float,float)` overload exists; `ResolveGroundTarget` only skips same-GameObject
`ChunkObject` colliders (props are children, so trees/rocks still stop the probe);
`WorldStreamer.Seed` is public.

### Play-test (pending, user)
Cast Earth Wall 3-4Ã— at the same spot (standing close and at range) â†’ every cast must land the SAME
ridge (~2.6 m), with no ground rise across the chunk on cast 2+ and no player push; cast a Crater
twice â†’ no deepening; a repeat cast aimed over the first wall â†’ targets the far side, not the wall
face; walk away and back / reopen â†’ identical. No CLI/Unity build was run (rule 3).



---

## 1cl. Terrain deformation: per-corner caps (smooth dishes/ridges) + tops-first mesh layout

Request: after 1ck, "dent still causes a tile to disappear instead of changing shape, and the
world still shrinks." Investigation found both symptoms have one source plus one latent bug:

- **Root cause A â€” cap/floor sampled at the tile CENTRE.** `DeformAt` clamped Crater floors to
  `GetHeight(Seed, wx, wz) âˆ’ 1.8` and raised ceilings to `GetHeight(Seed, wx, wz) + lift`, where
  `(wx, wz)` is the TILE'S CENTRE (`cx + 0.5, cz + 0.5`). All 4 corners of a tile therefore clamped
  to one constant â†’ the interior collapsed into a flat plate (crater = flat-bottomed step pit;
  Wall/Pillar/Ring = flat-topped slabs). A flat tile re-armed the legacy path: `IsFlatTile` â†’
  `ChunkContainsFlatTile` â†’ full rebuild, and neighbours emitted vertical side-wall bands down to
  the pit's floor â†’ the dent read as a torn-out/blocky tile. The flat floor + hard walls also made
  the rebuilt collider jagged under/near the player â†’ CharacterController depenetration â†’ the
  "world shrinking" launch.
- **Root cause B â€” wall bands interleaved into the merged vertex buffer.** `BuildMergedMeshData`
  emitted each tile's walls right after its top quads, so `PatchRegion`'s fixed quad offsets
  `(lz Â· cs + lx) Â· 4` were only valid when a chunk had NO walls â€” a chunk mixing flat tiles
  (walls) with smooth re-carves had region patches write heights into the wrong vertex slots
  (latent tile-vanish).

Fixes (`WorldStreamer.cs` / `ChunkMeshGenerator.cs`):

- **Per-corner caps (A).** `DeformAt` samples the noise at the CORNER coords `(cx, cz)` for both
  the Crater floor (`noise âˆ’ 1.8`) and the raise ceiling (`noise + lift`). Every corner keeps its
  own slope â†’ interiors are genuine smooth dishes and ridges, never flat; no walls, no full-rebuild
  churn, no jagged collider steps. Bounds still hold â€” repeat casts can't grind deeper or stack
  higher.
- **Any-flat relaxation (1ck gate reversed, deliberate).** `RelaxLegacySlabTile` relaxes ANY flat
  modified tile (whole-metre slabs AND fractional carve plateaus) deterministically on load, so the
  user's existing flat craters read as smooth rounded dents, identical every reopen; new shapes are
  never flat, so they never re-blend.
- **Tops-first merged layout (B).** `BuildMergedMeshData` emits all top quads first, then all wall
  bands â€” `PatchRegion`'s fixed offsets stay valid in every chunk. Output geometry unchanged.

Status: verified by grep + reread (rule 3; no build run) â€” `DeformAt` clamp sites,
`RelaxLegacySlabTile` gate removal, and the two-pass `BuildMergedMeshData` layout all re-read
consistent; no other code depends on the interleaved wall order (`PatchRegion`/`ApplyMerged` use
fixed top offsets). Play-test check: cast a Crater at a fresh spot â€” it must read as a smooth
concave dish with NO flat floor/step; cast a Wall twice â€” second cast must cap at ~2.6 m with a
rounded crest, no slab top; walk into a cast crater â€” no launch/shrink; reopen the world â€” carved
dishes and walls return smooth and identical; with `EnableResetTerrainSaves` on, the world
regenerates pristine once.

---

## 1ck. Terrain persistence hardening: relaxation gate + hole-proof mesh + save-reset lane

Request/Root cause: two reports â€” (a) "each time the game closes and reopens the map reset new"
and (b) "there is a literal hole with no tile" (a fall-through into the void). Investigation
confirmed the save/load pipeline is healthy: seed `1337` constant, noise deterministic
(`TerrainNoiseGenerator` offset cache), `ChunkSaveManager` files present + valid in
`worlds/1337/tc_*.dat` with no load/save errors, chunk folder keyed stably. The real defects:

- **(a) carves appear to "re-generate fresh" each open.** `RelaxLegacySlabTile` (1cj) fired on
  EVERY load for any flat mod tile. 1cj's own shapes are flat plateaus (Wall/Pillar/Ring caps
  clamp to one constant `noise + lift`; Crater floors clamp to `noise âˆ’ 1.8`), so each reopen the
  player's carvings got re-blended 50% toward noise â€” the map morphed and read as "new".
- **(b) literal mesh holes.** `ChunkMeshGenerator.BuildMergedMeshData` skipped tiles whose
  `Vertices == null` (no quad emitted), and `FullRebuildChunk` / `RebuildChunkRegion` left null
  slots for any chunk tile missing from `_loadedData` (unload/reload races at the streaming edge,
  the boot-time `ForceRebuildArenaLane`). A skipped quad = a real gap you can fall through.

Fixes (all in `WorldStreamer.cs` / `ChunkMeshGenerator.cs` / `NewWorldTestGround.cs`):

- **Whole-metre gate (a):** `RelaxLegacySlabTile` now relaxes ONLY legacy whole-metre flat tiles
  (`Mathf.Abs(flat âˆ’ Mathf.Round(flat)) > 0.01f â†’ return`). Smooth inside-metre carve plateaus
  load back exactly as cast â€” the map is stable across sessions, forever.
- **Hole-proof mesh (b):** `BuildMergedMeshData` defensively fills any null tile with the SAME
  deterministic noise corner heights the pristine grid uses (`BuildFallbackTile`/`BuildFallbackTileData`)
  so a quad is always emitted; `FullRebuildChunk` falls back to the whole-chunk `BuildOrLoadChunk`
  builder when any loaded-chunk tile is missing; `RebuildChunkRegion`'s region patch fills missing
  tiles from noise instead of leaving a default slot. `BuildOrLoadChunk` also now passes its `seed`
  through to `BuildMergedMeshData` so cross-chunk seam walls agree with the real noise.
- **QA reset lane:** `WorldStreamer.ResetTerrainSaves()` (public) + `ChunkSaveManager.ResetWorldSaves(seed)`
  delete every `tc_*.dat` for the world, clear dirty marks, and reload the loaded chunks from
  noise â€” a deliberate clean map. Opt-in via `NewWorldTestGround.EnableResetTerrainSaves` (default
  off, never touches the platform or legacy village).

Status: verified by grep + reread (rule 3; no build run) â€” new symbols referenced/defined
consistently (`BuildFallbackTileData`, `BuildFallbackTile`, `ResetWorldSaves`, `ResetTerrainSaves`,
`EnableResetTerrainSaves`), signatures match, comments/code consistent.  Play-test check: cast a
Wall/Pillar/Crater, close the game and reopen â€” the carved shapes must return EXACTLY (no morph);
walk the older whole-metre farm/village slabs â€” they smooth once and stay; with
`EnableResetTerrainSaves` on, the world regenerates pristine once.

---

## 1cj. Terrain deformation: revert slabs â†’ smooth, restore caps, relax old saves

Request/Root cause: 1cg turned every Earth terrain shape into flat-topped 1x1x1 m "slab" stacks
(offset from the aim point, vertical side-wall bands) and removed the stacking caps (`9e5bbaa`/
`fc08738` predecessors) â€” the world visibly **"shrank down"**: craters ground deeper with every
overlapping cast (no floor clamp), raises stacked until the capsule got embedded and the Character
Controller's depenetration launched the player (and repeated raises on fractional terrain snapped
whole tiles DOWN via `Mathf.Round`). Fix in `WorldStreamer.cs` (1cj):

- **`DeformAt` rewritten to smooth per-corner edits** (restores the proven pre-1cg `c990e58`
  algorithm): heights are continuous per-corner elevations smoothstep-blended at the rim â€” no
  quantized maths, no forced whole-metre steps, so the deform centers on the aim point and reads as
  genuine terrain. Raised shapes keep the ~0.9 m caster keep-out ring + `SanitizeHeight`.
- **Bounded again (caps restored)**: Crater clamps at `floorY = baseNoise âˆ’ 1.8` (never grinds
  deeper or carves a void); raised shapes (Ring/Spikes/Wall/Pillar) cap at `noise + lift` (repeat Wall
  stays ~2.6 m â€” never stacks, never embeds the capsule). Spikes' deterministic peaks are added
  *before* the ceiling clamp so they keep their jagged shape but stay bounded.
- **Deleted the 1cg-only dead code**: `TileTopAt`, `ApplyFlatEdits` (grep-verified â€” only `DeformAt`
  called them). `ChunkMeshGenerator` slab side-wall/`IsFlatTile` infra is KEPT â€” it still serves
  legacy flat saves; smooth deforms never emit walls (shared corners stay equal).
- **Legacy 1cg slabs relax on load**: `BuildOrLoadChunk` re-smooths each loaded flat-slab tile
  (`RelaxLegacySlabTile`) toward its own 4 corner-noise heights (`OldSlabRelaxKeep 0.5`, lives>0.15 m
  deviation) BEFORE the mesh is built. In-memory only â€” deterministic/idempotent; the file keeps the
  slab until the next player deformation persists the smooth values naturally.
- Demo + docs: `NewWorldTestGround` terrain demo kept (off by default) but re-demos smooth shapes +
  capped repeat Wall; comments reworded in `SpellData.cs`, `SpellEffect.cs`, `SkillCatalog.cs`;
  `game-design.md` Â§3.8 + `magic-skills.md` header describe smooth feathered edits + caps.

Status: verified by grep + reread (rule 3; no build run) â€” `TileTopAt`/`ApplyFlatEdits` gone, call
sites/signatures consistent, clamp math reviewed. Play-test check: cast Wall twice at the same spot
(test late, `baseX+0`) â€” second cast must NOT raise higher; cast Crater repeatedly â€” must NOT grind
deeper; cast on a pre-1cj save â€” old slabs must blend into smooth rounded shapes.

---

## 1ci. Ice school remix (distinct deliveries)

Request: "avalanche, cold snap, deep freeze, freeze, frost curse, frozen touch, glacial surge,
glacier, hail lance, tundra, witching chill" (the AoE list) and "chill soul, chill touch, frost
bite" (the projectile list) in the ice element are basically the same â€” differentiate them. Followed
the fire remix ("full delivery remix") so each pick reads distinctly: most AoEs were near-identical
**Zone** casts that only bumped power/radius, and the shard projectiles were recolors.

Ice remix (all in `SkillCatalog.Magic.cs`):
- **Freeze** â€” stays Zone but becomes a **lingering frost field**: + `duration 3f` (SpellZone tick),
  Frost kept.
- **Deep Freeze** â€” Zone linger: `duration 3.5f`, Frost kept (heavy paralyzing field).
- **Cold Snap** â€” Zone â†’ **Instant** (crosshair snap-freeze, Frost; no travel, no AoE marker).
  Mirrors Flash Fire.
- **Tundra** â€” Zone linger: `duration 3.5f`, **Chill** (wide creeping-slow field).
- **Frozen Touch** â€” Zone â†’ **Vortex** (ice whirl that drags + chills): `deliveryRange 8f,
  deliveryRadius 2.2f, duration 2.5f`, Chill. Mirrors the Whirlpool vortex archetype.
- **Glacier** â€” the crush slam: Zone + knockback 2, no status ("crushes and shoves foes").
- **Glacial Surge** â€” crowning frost field: radius â†’ 3.8, `duration 4f`, Frost.
- **Avalanche** â€” Zone â†’ **Storm** (hail crashes down over the area): `duration 3.5f`, Frost. Mirrors
  Thunderstorm/Meteor Rain archetypes.
- **Hail Lance** â€” Zone â†’ **Storm** (a storm of hail lances batters the area): `duration 3.5f`, Chill.
- **Frost Bite** â€” projectile shape Shard (Auto) â†’ **Comet** (streaking flash of biting cold).
- **Chill Soul** â€” projectile shape Shard (Auto) â†’ **Missile** (homing soul-chill; reuses
  `SpellEffect.UpdateMissileTargeting`, the Arcane Missiles precedent).
- **Frost Curse** â€” Zone â†’ **Vortex** (spiraling curse of creeping cold): `deliveryRange 8f,
  deliveryRadius 2f, duration 3f`, Chill.
- **Witching Chill** â€” Zone linger: `duration 3f`, Frost.
- Unchanged: Ice Lance, Frost Pierce, Glacial Impale (linear lances), Cold Stare (beam), Frost
  Obelisk (summon), Chill Touch (already a distinct shard projectile, kept as the cheap chill).

### 1ci-status
- **`SkillCatalog.Magic.cs`** â€” frost L1 (Freeze, Glacier) + L2 blocks remixed as above; tooltips
  reworded to match each new identity. Signature verified against the `Spell(...)` factory
  (`SkillCatalog.cs:124-130`): the positional float after `SpellDelivery.X` is the **cooldown**, then
  named `deliveryRange:`/`deliveryRadius:`/`duration:`/`statusEffect:` (defaults 10 / 1 / 0 / none).
  Cooldowns preserved from originals (Hail Lance 5, Cold Snap 5, Frost Bite 4, Chill Soul 4, ...). Same
  pass fixed two fire leftovers: Conflagration cd 8â†’**8** (was accidentally 7) and Flash Fire cd 6â†’**5**
  (was accidentally 6) â€” Flash Fire is Instant with the default range, so the old "range 6" note was
  wrong on both counts.
- **Docs** â€” `magic-skills.md` frost rows updated (deliveries/stats/tooltips) plus the stale Flash Fire
  row fixed; `game-design.md` Â§3.8.1 Storm examples and the Projectile Shapes table synced (Frost Bite
  â†’ Comet, Chill Soul â†’ Missile, Shard back to Stone Shard / Chill Touch). PROGRESS intro refreshed.
- **Verification**: no CLI/Unity build per project rule 3 â€” grep + full re-read. All 13 touched frost
  spell ids appear only in `SkillCatalog.Magic.cs` (no stale callers). Delivery behavior re-checked:
  Zone linger = `SpellZone` tick, Vortex = `SpawnVortex`, Storm = `SpellStorm`, Instant =
  `ResolveDirect` (status applies via `ApplyHit` â†’ `ApplyStatus`, `SpellCaster.cs:1036-1037`),
  Missile/Comet shapes resolve in `SpellCaster.BuildProjectileBody`. `UpdateAoePreview`
  (`PlayerController.cs:1505`) still rings Zone/Vortex/Storm â€” Cold Snap (Instant) correctly shows no
  marker.
- **Play-test (pending)**: cast each remixed frost skill â€” Freeze / Deep Freeze / Tundra / Witching
  Chill / Glacial Surge should leave lingering frost/chill fields; Hail Lance and Avalanche strike
  repeatedly over the area (storm); Frozen Touch and Frost Curse whirl and pull foes; Glacier slams and
  shoves; Cold Snap hits instantly along the crosshair; Chill Soul's dart bends to chase its prey; Frost
  Bite streaks in as a comet.

---

## 1ch. Fire school remix (distinct deliveries) + beam magic sweeps with player aim

Request: "asteroid, conflagration, firewave, firestorm, flash fire, inferno, inferno peak, meteor
skill in fire element are basically the same â€” differentiate them; beam magic should turn when the
player turns." All eight were near-identical **Zone** casts that only bumped power/radius. Remixed to
distinct deliveries per pick, keeping the tree's family logic, and the channeled **Beam** delivery now
re-aims every frame.

Fire remix (all in `SkillCatalog.Magic.cs`):
- **Meteor** â€” stays Zone, the instant impact slam: knockback 1.5 â†’ **2**.
- **Inferno** â€” Zone becomes a **lingering burn field**: + `duration 3.5f` (SpellZone tick), Burn kept.
- **Asteroid** â€” the payload payoff: radius 3.6 â†’ **4**, knockback â†’ **3** (no burn, pure slam).
- **Conflagration** â€” Zone â†’ **Vortex** (fire whirl that drags + burns): `duration 3f`, Burn. Mirrors
  the Whirlpool vortex archetype.
- **Firestorm** â€” Zone â†’ **Storm** (embers rain down over the area): `duration 3.5f`, Burn. Mirrors
  Thunderstorm/Meteor Rain archetypes.
- **Inferno Peak** â€” crowning burn zone: radius â†’ 4.2, `duration 3.5f`, Burn, knockback 1.
- **Fire Wave** â€” the push wave: knockback â†’ **2.5**, no burn ("sweeps foes across the field").
- **Flash Fire** â€” Zone â†’ **Instant** (crosshair flash strike, Burn; no travel, no AoE marker;
  cooldown stays 5s). Unchanged: Scorch, Meteor Rain, Comet, Burn, Searing Ray (already distinct).
- All new entries copy established delivery signatures (Whirlpool `duration`/`deliveryRadius`/Burn,
  Stormcall `deliveryRange`/`deliveryRadius`/`duration`/status, Instant range/Burn) â€” no per-skill
  special-casing added; `ResolveDirect`/`SpellZone`/`SpellStorm`/`SpawnVortex` already apply status
  and knockback on these paths (verified in `ApplyHit` at `SpellCaster.cs:1036-1037`, Zone linger at
  `:943-951`, Stormcall/Whirlpool precedents).

### 1ch-status
- **`SpellBeam.cs`** â€” `Update()` re-derives `Direction` every frame from the camera aim point
  (`cam.position + cam.forward * Length`, caster `transform.forward` fallback), mirroring
  `SpellCaster.Execute`'s aim math at `SpellCaster.cs:265-272`. The beam body, end orb, and tick
  capsule all derive from `Direction`, so the whole effect sweeps with the player's turn while
  channeling; LMB sustain/focus upkeep/grace logic unchanged.
- **`SkillCatalog.Magic.cs`** â€” fire L1 (Meteor, Inferno) + L2 blocks (Asteroid, Conflagration,
  Firestorm, Inferno Peak, Fire Wave, Flash Fire) retuned as above; tooltips reworded to match.
- **Docs** â€” `magic-skills.md` fire rows updated (deliveries/stats/tooltips); `game-design.md` Â§3.8.1
  Beam bullet rewritten ("sweeps with the caster's aim while channeling"; removes stale
  "can't be re-aimed"). PROGRESS intro refreshed.
- **Verification**: no CLI/Unity build per project rule 3 â€” grep + full re-read. All 8 touched
  spell ids appear only in `SkillCatalog.Magic.cs` (no stale callers). `ApplyHit` applies
  `ApplyStatus` + `ApplyKnockback` for Instant (Flash Fire); `resolve Zone duration` linger is
  `SpellZone`; Vortex/Storm status verified by Whirlpool/Stormcall precedents. Flash Fire's change
  Zoneâ†’Instant simply means `PlayerController.UpdateAoePreview` (`.cs:1505`) no longer shows an AoE
  marker for it â€” intended (its comment explicitly excludes projectile/instant spells).
- **Play-test (pending)**: (1) learn/arm each remixed fire skill and cast â€” Conflagration should
  visibly whirl and pull, Firestorm rain embers over the zone, Inferno/Peak leave the ground burning
  for ~3.5 s, Fire Wave shove foes, Flash Fire hit instantly along the crosshair; (2) channel Searing
  Ray / Arc Storm / Tidal Stream and turn the camera â€” the beam should sweep across enemies and keep
  damaging the new line.

---

## 1cg. Earth terrain deforms = flat-topped 1x1x1 m slab stacks (uncapped, vertical side walls)

Request: "slab the stretched ground surface; a slab can be smaller but there is a max size â€” and
generate another slab to cover the space." Old behavior stretched one terrain quad and capped the
lift/depth. New behavior: every touched TILE (1Ã—1 m column) is set to **one whole-metre level â€” all
four corners equal** â€” so it becomes a flat-topped slab; neighbours at different heights render
**vertical 1 m side-wall bands** built into the merged chunk mesh + collider (stacked-slab look);
repeating a cast **stacks another slab uncapped** up to the Â±200 m `ChunkMeshGenerator.MaxTerrainHeight`
safety band. The keep-out ring inject around the caster's feet is kept (never embed the capsule),
and the crater is a stepped flat-bottomed pit â€” never a void.

### 1cg-status
- **`ChunkMeshGenerator.cs`**: `BuildMergedMeshData` now takes optional `border`/`seed` params and, in
  a first sizing pass, counts side-wall bands (4 verts + 6 tris each) before emitting the unchanged
  top quads, then emits vertical wall bands across every edge where this tile is the higher owner.
  New helpers: `IsFlatTile`, `SideBandCount` (ceil of drop, clamped 1..256), `EdgeHeights`
  (in-chunk tiles â†’ border dict â†’ corner noise), `EdgeIsRaised` (owned by the higher tile;
  identical pristine corners never raise), `CornerHeight`, `EdgeEnds`, `EdgeOutward`. Wall normals face
  outward and UVs tile once per metre per band (slab read). Winding flips per edge so walls render
  from inside and outside.
- **`WorldStreamer.cs`**: `DeformAt` rewritten â€” per-TILE flat levels keyed by `EncodeCorner(cx,cz)`,
  quantization `Mathf.Round(target)` with a forced Â±1 slab step where the rim influence `s >= 0.6`,
  Spikes keep deterministic extra peaks, ring crater floors sampled with `TileTopAt(cx,cz,max)` (max
  for raises, min for craters). New `ApplyFlatEdits` writes all 4 corners per footprint tile, marks
  dirty, rebuilds + flushes save files, then `ReconcileModifiedBorders`. New `FullRebuildChunk`
  (all 900 tiles via `BuildBorderCorners`), `BuildBorderCorners`/`CornerIfLoaded` (border map built
  from **loaded** tiles only â€” unloaded seams fall back to noise, no phantom walls), and
  `ReconcileNewlyLoadedChunk` wired into `FinalizeChunks`/`GenerateChunkSync` so cross-chunk seam
  walls appear once both sides load. `RebuildChunkRegion` dispatches to `FullRebuildChunk` whenever
  the chunk contains a flat tile (or the region covers â‰¥75% of the chunk); `PatchRegion` is retained
  for smooth flatten-only deforms. Per-shape height caps and `CraterMaxDepth` were **removed**.
- **`NewWorldTestGround.cs`**: new default-**off** toggle `EnableTerrainSlabDemo` â†’ opt-in
  `RunSafely("terrain slab demo", SpawnTerrainSlabDemo)` lane casting Wall (twice, to demo the stack)
  + Pillar + Crater off the platform's west edge via `TerrainDeformer` (deforms REAL terrain â€”
  permanent saves â€” hence off by default; never touches the platform or legacy village).
- **Docs**: `game-design.md` Â§3.8 terrain-shape bullet rewritten (slab decomposition, vertical wall
  bands, uncapped stacking, keep-out retained, stepped crater, no cap sentinel). PROGRESS intro
  refreshed.
- **Verification**: no CLI/Unity build per project rule 3 â€” grep + full re-read of both edited
  regions; call sites of `BuildMergedMeshData` (background load + full rebuild) and `RebuildChunkRegion`
  match the new signatures; no lingering `CraterMaxDepth` references.
- **Follow-up tidy** (new commit after `1cg`): `magic-skills.md` delivery table now covers the full
  `TerrainShape` set (Crater/Ring/Spikes/Wall/Pillar) + a slab-rendering note; stale "depth-clamped"
  comments in `SpellEffect.cs` / `SpellData.cs` / `SkillCatalog.cs` reworded to "flat slab-steps with
  a walkable floor" (behavior never had a void â€” the clamp label was the outdated part).
- **Play-test (pending)**: cast an Earth Wall â†’ flat-topped blocky ridge with vertical sides;
  cast again on the same spot â†’ ridge grows taller (uncapped); Crater â†’ stepped flat-bottomed pit;
  reload the game â†’ slabs persist; deform a wall directly on a chunk seam â†’ seam wall renders
  (both chunks loaded).

---

## 1cf. Alt magic grid keeps scroll position across close/reopen

Play report: "when close and open the alt tab it reset the top, change it." Cause: `MagicTestMatrix.Open()`
set `_scroll.verticalNormalizedPosition = 1f` on every open, snapping the list back to the top.

### 1cf-status
- **`MagicTestMatrix.cs`**: removed the per-open reset; the position is now set to the top once in
  `EnsureBuilt()` after `PopulateRows()`, so the first open starts at the top and later opens retain
  whatever position the player scrolled to (the canvas is only deactivated, the `ScrollRect` state is
  kept).
- **Verification**: no CLI/Unity build per project rule 3 â€” code reread only.
- **Play-test (pending)**: open Alt grid, scroll down, close, reopen â†’ the list stays where it was; a
  fresh session starts at the top.

---

## 1ce. Alt magic grid â€” click arms only, never casts (attack input suppressed while open)

Play report: **"when clicking to choose magic in the alt menu it shouldn't shoot the magic out."** The
Alt "menu" is the dev/test **MagicTestMatrix** (Alt grid). Two things fired a spell on one click:

- **By design:** `Button.onClick â†’ CastId(id)` called `profile.ExecuteCharged(id, 0f, 0f)` â€” an instant
  fast-cast â€” so selecting a row literally cast it.
- **Input leak:** the matrix is not a `MenuPanelBase`, so `PlayerController.Update` ran its full input
  pass. The combat press guard (`PlayerController.cs:1131`) only checked `!MagicWheelUI.IsOpen` (the
  retired wheel, always false), so LMB press auto-armed a spell (`EnsureArmedMagic`) and the release
  fired it (`ReleaseArmedCast`) â€” a second cast from the same click.

User chose **arm only** (no learn/top-up, no cast).

### 1ce-status
- **`MagicTestMatrix.cs`**: `CastId` â†’ `SelectId` (`:345`), now just `MagicWheelUI.ForceArmMagic(id)` +
  status "Armed  <name>" â€” removed `ExecuteCharged`, the `HasLearned/TestGrant` grant, and
  `TopUpFocus`. Row listener (`:317`), class docstring, and hint text updated. Removed the now-unused
  `Profile()` helper. Unlearned spells can still be armed (chip updates) but will not cast (arm-only).
- **`PlayerController.cs`**: added `if (MagicTestMatrix.IsOpen) return true;` to `ShouldCancelCharge()`
  (`:1722`) so an aim in progress is cancelled when the grid opens; added `&& !MagicTestMatrix.IsOpen`
  to the dual-mode availability (`:1024`), the fighting press/aim-start guard (`:1131`), and the RMB
  block/charge guard (`:1154`). Movement/mouse-look unaffected. Now a row click cannot leak into an
  aim/charge/fire.
- **Docs**: `game-design.md` Â§5.16 Alt bullet rewritten (click only arms; attack input suppressed).
  PROGRESS intro refreshed.
- **Verification**: no CLI/Unity build per project rule 3 â€” code review + grep only. `CastId` has zero
  remaining references; the four `MagicTestMatrix.IsOpen` guards reread; `TestGrant`/`TopUpFocus`/
  `ExecuteCharged` remain used elsewhere so no dead-symbol issues.
- **Play-test (pending)**: Alt â†’ click a spell â†’ it arms ("Armed: X" chip) and does **not** fire; close
  the grid and hold LMB/RMB â†’ release to cast the armed spell normally.

---

## 1cd. 100x percent-perk multiplier bug + additive MoveSpeed (the real "still very fast" cause)

Play report after `1cc`: **"still very fast."** `1cc` only removed the dev stat floor; it left the actual
arithmetic bug. Investigated with grep + reread (no Unity build):

- **Root cause (100Ã— bug):** `PassivePerkManager.Mul` returned `1f + Sum(kind)`, but skill-tree perks are
  authored as **integer percents** (`Perk(PassivePerkType.MovementSpeedPercent, 3f)` = +3%; `PassivePerkEffect`
  passes the raw value, catalog doc says "5 = +5%"). So every percent perk was applied ~100Ã— too large.
  The ~40 `MovementSpeedPercent` nodes sum to **+158** â†’ `Mul = 159` instead of `2.58`.
  `PlayerController` then did `MoveSpeed Ã— (1 + SpeedÃ—0.5) Ã— 159` â†’ Speed 10 â†’ Ã—954 â†’ walk â‰ˆ **4,770 m/s**.
  The class/race `*Mul` managers are correct because their catalogs pass **fractions** (`0.08f` = +8%,
  `ClassSkill.cs:7`); the tree perks were the odd one out. It also inflated **every** percent stat (HP,
  damage, cooldowns, regen, loot luck) ~100Ã— â€” the player was effectively unkillable.
- **Secondary:** `PlayerStats.MaxMoveSpeed` used the multiplicative `BaseMoveSpeed Ã— (1 + SpeedÃ—k_mov)` and
  `PlayerController` re-scaled by `MoveSpeed/BaseMoveSpeed`, so Speed applied with a ~5Ã— steeper coefficient
  than design (Â§3.4 `base + SpeedÂ·k_mov`).

User direction: "both" â€” fix the bug **and** align the movement formula (kept the controller's
`MoveSpeed Ã— MaxMoveSpeed/BaseMoveSpeed` ratio wiring per user preference). No speed cap.

### 1cd-status
- **Percent aggregation fixed** (`PassivePerkManager.cs`): `Mul(kind) => 1f + Sum(kind) / 100f`; doc comment
  updated (integer percents; `Î£ 5+3 â†’ 1.08`). `Sum`/`AddPerk` unchanged so flat kinds still accumulate raw
  (`HealthRegenPerSecond` 0.003 = +0.3%/s, `DamageReductionFlat` 0.02). Class/race modifier managers
  (fractional convention) untouched.
- **MoveSpeed made additive** (`PlayerStats.cs`): `MaxMoveSpeed => (BaseMoveSpeed + GetTotal(Speed)Â·K_Move) Ã—
  TreeMul(MovementSpeedPercent)` â€” matches Â§3.4 and applies Speed once; the controller's ratio wiring is
  unchanged, so walk = `MoveSpeed Ã— (BaseMoveSpeed + SpeedÂ·K_Move)/BaseMoveSpeed Ã— TreeMul`.
- **Over-max clamp** (`PlayerController.Update`): a stored/legacy `HP`/`Stamina` above the now-lower maxima
  snaps down each frame, so the HUD never shows over-max after the rebalance.
- **Docs**: `game-design.md` Â§3.3 perk-aggregation convention note + Â§3.4 implementation paragraph rewritten
  (additive MoveSpeed, integer-percent aggregation, 1cc/1cd history). PROGRESS intro refreshed.
- **Verification**: no CLI/Unity build per project rule 3 â€” code review + grep only. Grep confirmed no
  percent perk passes a fraction (`Percent, 0.` â†’ no matches), and `.Mul(`/`TreeMul` consumers all multiply
  multiplicatively (no caller assumed the old integer scale). Flats still read via `TreeSum`.
- **Expected** (with the controller ratio wiring): Speed 10, no perks â†’ walk â‰ˆ **11 m/s** / sprint â‰ˆ 22;
  Speed 10, all tree movement perks (+158% â†’ Ã—2.58) â†’ walk â‰ˆ **29 m/s** / sprint â‰ˆ 58. Down from ~4,770 m/s.
- **Play-test (pending)**: (1) fresh + loaded characters move sanely; (2) HP/damage/cooldown now reflect
  intended magnitudes (combat will feel much lower than the inflated 100Ã— build â€” expected); (3) current
  HP/Stamina clamp cleanly to the new lower maxima on the first tick.
- **Follow-up (parked)**: fully-perked top speed (~58 m/s sprint) may still be tuned via `K_Move` /
  `MovementSpeedPercent` values; class/race `MoveSpeedMul` still unconsumed by `MaxMoveSpeed`.

---

## 1cc. Super-speed root cause + speed-aware fail-net (fixes "continuously pulled back")

Play report: "player have like super speed and being continuously pulled back." Investigated the
movement pipeline (grep + reread, no Unity build). Two effects, one chain:

- **Super speed cause:** `PlayerStats.DevMaxAllStats = true` floored every stat to 100, so Speed=100
  â†’ `moveSpeedPerkMult = (1 + 100Ã—0.5) Ã— TreeMul(MovementSpeedPercent)`. The test-ground all-perk
  grant adds ~47 `MovementSpeedPercent` nodes â‰ˆ +184% â†’ `TreeMul â‰ˆ 2.84`. Net multiplier â‰ˆ 145Ã— on
  `MoveSpeed` â†’ walk â‰ˆ **724 m/s**, sprint â‰ˆ **1,448 m/s**.
- **"Continuously pulled back" cause:** NOT death/respawn (`ResetPlayer` only runs at `Start`). It was
  the `1ca` fail-net doing its job against the monster speed: `EnforcePhysicsSanity` reverts when one
  frame moves > 150 m. At 1,448 m/s you cross a 30 m chunk in ~21 ms, so chunk-stream/build hitches
  (>~104 ms) exceeded 150 m every time â†’ repeated snap-back to the previous frame's position.

User direction: **no speed cap** â€” find the actual cause; and **set `DevMaxAllStats=false`**.

### 1cc-status
- **Root cause removed** (`PlayerStats.cs`): deleted the `DevMaxAllStats` / `DevMaxAllStatValue` consts,
  `MaxOutAllStats()`, and the `Start()` hook that called them (grep-verified no other call sites). Speed
  now reflects real allocation (creation seeds all stats to 10) â†’ `moveSpeedPerkMult â‰ˆ (1+10Ã—0.5)Ã—2.84
  â‰ˆ 17Ã—`, walk â‰ˆ 85 m/s, sprint â‰ˆ 170 m/s. No cap added (per direction); the deeper "too fast while
  fully-perked" balance question stays parked.
- **Fail-net made speed-aware** (`PlayerController`): cached `_lastEffectiveSpeed` from
  `HandleMovement`; `EnforcePhysicsSanity` now uses `threshold = max(150, _lastEffectiveSpeed Ã— 1.5)`
  instead of a flat 150 m, and `LogSanityBlast(blastPos, threshold)` probes with that radius. Real
  corruption blasts (thousands of metres) still revert; legit fast movement during a ~1 s hitch cannot
  false-trigger. This directly kills the "continuously pulled back" symptom even at the new top speed.
- **Docs**: `game-design.md` Â§2.8 fail-net bullet rewritten (speed-aware tolerance); Â§3.4 derived-formula
  block gains the implementation note (walk/sprint multiplier + dev floor removed). PROGRESS intro refreshed.
- **Verification**: no CLI/Unity build per project rule â€” code review + grep only. Grep confirms
  `DevMaxAllStats` / `MaxOutAllStats` have zero remaining references; `EnforcePhysicsSanity` and
  `LogSanityBlast` call sites both updated to the new signature.
- **Play-test (pending)**: (1) walk/sprint feel normal on a fresh + loaded character (no 1,500 m/s
  blur); (2) no repeated yank-back while sprinting across loading chunks; (3) if a genuine
  corruption blast ever occurs, the log still reports it (threshold floor 150 m unchanged).
- **Follow-up (parked)**: the all-perks test grant still yields ~85/170 m/s movement; if that reads as
  too fast once stats are real, tune `K_Move` / the `MovementSpeedPercent` perk values (separate task).

---

## 1cb. Class + race locked to ONE choice â€” exclusive single-class / single-race model

User rule: **"the player can only have 1 class, 1 race at a time."** Prior build was explicitly
non-exclusive â€” `ClassUnlocker` mass-unlocked every eligible class at Start (`EvaluateAll`), held a
growing roster, and the test ground granted **all 22 races + 3 Ritual Stones** (`GrantRaceAccess`);
the Change Race tab listed every race and switching auto-unlocked the target for free
(`SetActiveRace(..., unlockIfNeeded:true)`). User chose the **"Lock class+race to one choice"**
ladder: generic SkillType skills/talents stay GLOBAL (separate base-tree axis â€” untouched), but the
class/race system is strictly exclusive; the test ground may no longer unlock everything.

### 1cb-status
- **`ClassUnlocker` rewritten for single-choice exclusivity** (`classUnlocker.cs`): `UnlockedClassIds`
  always holds exactly the one chosen id. `Awake` collapses any legacy multi-class roster to
  ActiveClassId; `Start` no longer runs a mass-unlock pass (only `EnsureChosenClass`, Wanderer
  baseline); `EvaluateAll` is now "guarantee a valid chosen class exists" (callers like
  `CharacterInfoUI.CurrentClassLine` unchanged); `SetActiveClass` REPLACES the roster + fires
  `OnActiveClassChanged`/`OnClassUnlocked` (idempotent for the same pick); `RestoreUnlocks` collapses
  old saves to the one saved active class (Wanderer fallback) â€” old multi-class saves migrate
  gracefully. Removed the stat/skill-requirement auto-unlock machinery (`MeetsRequirements`,
  `UnlockIfAbsent`, `_unlocked` set, `_skills`).
- **Test ground no longer grants roster/stones** (`NewWorldTestGround.GrantRaceAccess`): now wires the
  `RaceChangeManager` only â€” no unlock-all-22 loop, no 3-Ritual-Stone boost; player starts Human.
- **Change Race dialog gated** (`CharacterInfoUI.BuildRaceOptions`): lists ONLY Human + actually
  discovered races (new `RaceChangeManager.CanSelectRace` wrapper over private `IsSelectable`), and
  `ApplyPendingChange` race branch now calls `SetActiveRace(race, requireStone: true,
  unlockIfNeeded: false)` â€” non-Human changes consume a Ritual Stone, never auto-unlock; failure shows
  the in-dialog hint + stays open.
- **Untouched/kept** (per scope): `CharacterCreationUI:132` / `CharacterCreation.cs:49` free creation
  race pick (it is the ONE chosen starter race), `RaceDiscoveryPoint` still grants a race you actually
  discover (unlockIfNeeded:true is the legit earn path), `RaceChangeManager` human-free + stone-gated
  logic, `SaveManager` save/restore of `unlockedClassIds`+`activeClassId`, and the managers that already
  scope modifiers to the active class/race (`ClassPassiveManager`, `RaceSkillPassiveManager`) plus the
  GLOBAL `PassivePerkManager` skill axis.
- **Docs**: `game-design.md` (Â§3.2 classes-exclusive paragraph + persistence bullet; Â§3.5 discover/
  change â†’ single-active + stone-gated) updated; `ClassUnlocker` class doc rewritten.
- **Verification**: no CLI/Unity build per project rule â€” code review + grep only. Grep-confirmed:
  removed symbols (`MeetsRequirements`, `UnlockIfAbsent`) have no call sites; `IsUnlocked` call sites
  are all legitimate (ClassSkillCaster gates casts, RaceUnlockManager/RaceDiscoveryPoint/CharacterCreation
  on the race side); every `SetActiveRace` caller re-read (creation = free pick, discovery = earn,
  UI = stone-gated no-unlock).
- **Play-test (pending)**: (1) open Character Info â†’ Class tab â€” exactly one class "current", picking
  another swaps it (no accumulation); (2) Race tab â€” only Human + discovered races shown; re-pick Human
  free, non-Human change consumes a Ritual Stone and shows the hint when you have none; (3) new game +
  reload an OLD save â€” class roster collapses to the saved single active class; (4) confirm no
  startup log floods from the removed unlock pass.

---

## 1ca. Physics integrity guard rails â€” no more one-step 5 km teleport

The `1bz` report persisted after the boot-teleport removal: "i take one step and teleported to -671.5826
5163.997". User confirmed those two numbers were **(x, z) â€” a horizontal blast ~5.2 km along +Z with a
âˆ’672 X drift**, not vertical. Investigation (grep + reread, no Unity build): **no script** can place the
player there (audited every position set; endings/intro cutscenes disabled; `OpenWorldGrounding` and
`ClearSpawnOverlap` only ever move Y, never X/Z â€” and this blast moved X and Z, so both are exonerated).
The only X/Z relocator is the CharacterController's own one-step depenetration: a single garbage/NaN
vertex anywhere in a streamed chunk poisons that chunk's `MeshCollider.bounds` â†’ broadphase corruption â†’
the CC is ejected toward the nearest boundary of the corrupted AABB on the first `Move`. `1bz` alone
did NOT fix it. User chose: **full defense-in-depth** (sanitize at source + player fail-net +
diagnostics).

### 1ca-status
- **Height sanitization at source (`WorldStreamer.BuildOrLoadChunk`)**: new `IsSaneHeight` (finite + inside
  Â±200 m band; const `MaxTerrainHeight`) replaces "stamp whatever the save says". A garbage corner slot is
  left as NaN â†’ regenerates from noise; a garbage height slot on a mod tile falls back to the already
  sanitized/regenerated corner grid, so corrupt values never enter a chunk mesh.
- **Final mesh backstop (`ChunkMeshGenerator`)**: new `SanitizeHeight` (non-finite â†’ 0, out-of-band â†’
  clamp to Â±200) applied in `BuildMeshData`, `BuildMergedMeshData`, and `ChunkObject.PatchRegion` â€” every
  vertex Y that reaches a MeshCollider is guaranteed finite and in band no matter the source.
- **Player CC fail-net (`PlayerController`)**: `_lastSafePosition` + `EnforcePhysicsSanity()` run every
  `Update` before input. Reverts (with `Debug.LogWarning`) if any coordinate is non-finite or a **single
  frame** moved the player > 150 m (max legit one-frame move â‰ˆ 14 m/s dodge â€” impossible false-positive).
  A `LogSanityBlast` sweep (`Physics.OverlapSphere` around last-safe + blast pos) reports any collider
  with non-finite/oversized `bounds` â†’ names the culprit chunk for a single targeted follow-up fix.
- **Teleport routing**: new public `PlayerController.TeleportTo(destination)` stamps the destination as
  the last-safe position. All intentional relocations now go through it so the fail-net never
  false-positives: `GameBootstrap` boot spawn (`playerController.TeleportTo`), `ResetPlayer` both
  branches, `FastTravelMenu.TravelTo`, `SleepManager` sleep/wake, `SaveManager` load-game, and
  `NewWorldTestGround.PlacePlayerOnArena`. Endings are disabled and use their own per-frame small moves.
- **Docs**: `game-design.md` **Â§2.8 "Physics Integrity Guard Rails"** added; PROGRESS intro refreshed.
- **Verification**: no CLI/Unity build per project rule â€” code review + grep only. Grep-confirmed all
  player-teleport sites route through `TeleportTo` and no other `>150 m` one-frame relocator exists.
- **Play-test (pending)**: (1) start play mode, walk ~one step on the boot chunk â€” no blast, no log
  warning; (2) if it ever recurs, the Console log names the culprit collider (bounds) in
  `[PlayerController] Physics blast restored...`; (3) fast travel, sleep, load-game, respawn and the
  opt-in pad pull all still land the player correctly (no snap-back).

---

## 1bz. No boot auto-teleport â€” player spawns on the world's boot chunk, pad is walk-to

Reported after the `1bx` play-test: "player appears on the testground, then teleports further and further"
at play-mode start (seen as "teleported very far away"). Investigation (grep + reread, no Unity build):
the new world pitches the player onto the QA pad **every** boot â€” `NewWorldTestGround.RunBenchSpawn` â†’
`PlacePlayerOnArena` â†’ `GetSpawnPoint()` `(PlatformCenter.x, PlatformTopY+2, PlatformCenter.z + 54)`, and
`PlayerController.Start`/`StartNewGame` â†’ `ResetPlayer()` re-homes to the same pad point. On a tall
platform (`PlatformTopY = maxGround + 12` over the 120 m box) that repeated <-2-frame> yank around the
high pad looked like a growing teleport. No repeated/accumulating position code exists (checked every
`Player.transform.position =` / `.position +=` site; endings + intro cutscenes are disabled), so the
symptom was the multi-path boot re-homing itself. User chose: **stop the auto-teleport; spawn on the
boot chunk near (0, terrain, -10) and let the player walk to the pad.**

### 1bz-status
- **`NewWorldTestGround.cs`**: new serialized **`AutoTeleportPlayerOnStart`** toggle (default `false`).
  The boot `PlacePlayerOnArena` lane and the end-of-spawn fallback now only run when the toggle is on;
  `GetSpawnPoint`/`PlacePlayerOnArena` are unchanged for manual/dev use.
- **`PlayerController.cs` `ResetPlayer()`**: no longer teleports to the pad whenever it is arena-ready.
  It now re-homes to `GetSpawnPoint()` **only if the player already reached the platform** (new
  `IsOnOrNearArena`: XZ within `PlatformSize * 0.6` of `PlatformCenter` **and** Y within 6 m of
  `PlatformTopY`); otherwise it uses the boot-chunk spawn `(0, terrainY + 3, -10)`. The 6 m-Y gate is
  what stops the boot spawn (â‰ˆ 9-12+ m below the pad top on flat ground) from matching.
- **Boot ground verified**: `ChunkData.Size == 1f`, so `GameBootstrap` `FromTile((0, -10))` maps to
  chunk `(0, -1)` whose world Z range is `[-30, 0]` â€” the player at `(0, y, -10)` stands on
  pre-generated ground the first frame (no void, no fall racing).
- **Docs**: `game-design.md` Â§2.7 rewritten â€” "no auto-teleport at boot (1bz)" bullet + `GetSpawnPoint`
  is only used when the player has reached the pad; `AutoTeleportPlayerOnStart` restores the old pull
  for dev sessions. PROGRESS intro refreshed.
- **Verification**: no CLI/Unity build per project rule â€” code review + grep only. Grep-confirmed only
  two callers of `PlacePlayerOnArena`/`GetSpawnPoint` (`NewWorldTestGround` internal + `ResetPlayer`),
  both behave under the new gates; no other `FindAnyObjectByType<NewWorldTestGround>` consumer depends
  on the old unconditional pull.
- **Follow-up fix (8682424):** `NewWorldTestGround.PlatformTopY` is **static** â€” the two instance
  accesses in `IsOnOrNearArena` were CS0176 compile errors; qualified with the type name.
- **Play-test (pending)**: (1) play-mode start puts the player on the ground near `(0, terrain, -10)`
  with no further teleports and the camera settles on them; (2) walking to the pad and dying/resetting
  keeps the player on the pad; (3) with `AutoTeleportPlayerOnStart=true`, the old pull-onto-pad
  behaviour still works.

---

## 1by. Build fixes: IStatProvider regen/cdr members + MakeTalentRow parent type

Unity play-test raised three pre-existing compile errors (not introduced by `1bx`, but found when the
editor compiled the talent work). Fixes verified by grep + reread; no CLI build per project rule.

- **`IStatProvider.cs`**: interface lacked `FocusRegenMul` and `CooldownReductionMult`, which
  `SpellCaster.cs:122/252` already accessed through its `IStatProvider Stats` field â†’ CS1061. Added both
  members to the interface; the sole implementer `PlayerStats` already exposes matching public getters.
- **`CharacterInfoUI.cs`**: `BuildTalentsView` passed `section.transform` (Transform) into
  `MakeTalentRow(RectTransform parent, â€¦)` â†’ CS1503. The body only uses `parent` for
  `go.transform.SetParent(parent, false)`, so the parameter type was changed to `Transform parent`
  (matching the rest of the file's `Make*` helpers). Grep-confirmed `MakeTalentRow` has a single caller.
- **Play-test (pending)**: editor compiles; talents panel + spell casting (FP regen, spell cooldowns)
  work as before.

---

## 1bx. Eight new talents: 7 distinct combat/regen kinds + Shield skill-type filler

User: "add more talents into the game, and not the same as the talents that already is". The talent
roster (18 â†’ **26** perks, Â§3.9) gained a **Shield** skill-type talent (fills the one missing category)
plus seven talents with brand-new effect kinds â€” none of which duplicate the existing XP/stat talents.

### 1bx-status
- **`TalentCatalog.cs`**: `TalentKind` extended with `CritChance`, `CritDamage`, `Backstab`,
  `BlockEfficiency`, `StaggerResist`, `StaminaRegen`, `FocusRegen`. New roster entries (all max rank 3):
  `t.shield` "Shield Work" (+6 % Shield XP/rank, same path as the other skill-type talents),
  `t.crit_chance` "Critical Eye" (+2 % crit chance), `t.crit_damage` "Executioner" (+15 % crit damage),
  `t.backstab` "Ambush" (+10 % backstab), `t.block_efficiency` "Bulwark" (+10 % block efficiency),
  `t.stagger_resist` "Grounded" (+10 % stagger resist), `t.stamina_regen` "Second Wind" (+10 % stamina
  regen), `t.focus_regen` "Arcane Spring" (+10 % focus regen). New `AddPercent` helper + `EffectPerRank`
  label strings for each new kind.
- **`TalentTracker.cs`**: one shared `SumKind(TalentKind)` helper (PerRank Ã— Ranks); `PlayerXpBonus`
  refactored onto it; new bonus reads `CritChanceBonus` (flat %) and `CritDamageBonus` / `BackstabBonus` /
  `BlockEfficiencyBonus` / `StaggerResistBonus` / `StaminaRegenBonus` / `FocusRegenBonus` (percent units).
- **`PlayerStats.cs`**: the combat/regen bonuses fold **additively** into the getters the combat pipeline
  already reads â€” `CritChance` (+ talent flat), `TreeCritDamageMul` / `TreeBackstabMul` /
  `TreeStaggerResistMul` / `TreeBlockEfficiencyMul` / `StaminaRegenMul` / `FocusRegenMul` (+ talent %/100
  onto the tree `1 + x/100` multiplier). **No consumer edits needed**: HitboxSystem, CombatController,
  PlayerController and SpellCaster all query these same getters.
- **Persistence/UI**: save/restore already filters owned ids through `TalentCatalog.Find`, so old saves
  load clean (new talents simply start at rank 0); Character Info builds its talent rows from
  `TalentCatalog.All`, so the 8 new rows appear automatically in the existing TALENTS scroll.
- **Docs**: `game-design.md` Â§3.9 updated (18 â†’ 26 total, seven skill-type talents incl. Shield,
  seven new combat/regen rows, "effect reads are live" bullet now lists the additive fold).
- **Verification**: no CLI/Unity build per project rule â€” code review + grep only. Grep-confirmed all
  eight new ids + every `TalentKind` reference resolve in `TalentCatalog`/`TalentTracker`/`PlayerStats`;
  re-read all three files and the two consumers' getter names match unchanged.
- **Play-test (pending)**: (1) Info tab shows 26 talent rows; (2) ranking Critical Eye/Executioner/Ambush/
  Bulwark/Grounded/Second Wind/Arcane Spring visibly changes crits, backstabs, block stamina drain,
  stagger knockback, stamina/focus regen; (3) ranking Shield Work makes the Shield category XP bar climb
  faster; (4) an old save loads without losing talent ranks or stat points.

---

## 1bw. All three religion structures + worship NPCs spawn on the test ground

User: "spawn the religion structure on the test ground" (+ "all three" + "include the NPCs"). The
Faith system's holy places (taoist shrine / church / pagoda) were previously only built inside the
legacy `WorldBuilder.CreateWorld()` path (`EnableLegacyGeneration = false`), so they never appeared
anywhere in the streaming world or on the QA bench. Now the test platform (`NewWorldTestGround`)
builds all three structures plus their worship NPCs.

### 1bw-status
- **`NewWorldTestGround.cs`**: new opt-in lane `EnableReligion` (default `true`) runs after the NPC
  lane; `SpawnReligion()` calls `wb.EnsureWorldRoot()` (guards `PlatformTopY == float.MinValue`),
  then mirrors the legacy wiring: `wb.BuildShrine/BuildChurch/BuildPagoda` at platform positions
  clear of the other lanes, and `MapBuilder.BuildTaoistNpc/BuildPriestNpc/BuildMonkNpc` (parented to
  `wb.WorldRoot`) + `AddComponent<TaoistPriestNPC/PriestNPC/PagodaMonkNPC>`.
- **Placement** (all at `PlatformTopY`, `PlatformSize` 120): Taoist Shrine `(cx-30, cz-38)` + taoist
  NPC `(cx-30, cz-31.6)`; Church `(cx+30, cz-35)` + priest `(cx+30, cz-41.2)`; Pagoda `(cx-30, cz+44)`
  + monk `(cx-32, cz+46)` (monk `Euler(0,-90,0)`, others identity).
- **Notes**: religion parts spawn visually only (same as legacy â€” not registered in `_buildings`,
  so not persisted); worship itself is NPC-driven, so each structure keeps its working NPC. The
  bench's tool kit already includes the **rosary** (Buddhist offering); taoist (1 wood) and church
  (50 coins) offerings may need grants in play-test.
- **Docs**: `game-design.md` Â§5.7 gained a **"The three holy places"** bullet (Faith system worship
  sites + QA test platform placement).
- **Verification**: no CLI/Unity build per project rule â€” code review + grep only; confirmed
  `BuildShrine`/`BuildChurch`/`BuildPagoda` and `MapBuilder.Build*Npc` are public, `EnsureWorldRoot`
  exists, and the three NPC classes + `MapBuilder` are in the global namespace (no usings needed).
- **Play-test (pending)**: (1) bench shows shrine NW, church NE, pagoda SW with their NPCs nearby;
  (2) press E on each worship NPC â†’ dialog offers worship; joining a faith updates Character Info >
  Faith (devotion +1/day, blessing perks); (3) toggle `EnableReligion` off â†’ no structures spawn.

---

## 1bv. Talents moved to the Info tab; talent-point currency removed (free Rank Up)

User: "talent should be show in the info tab not the skill tab" + decided to drop the talent-point
currency. The talent list (18 rankable perks, Â§3.9) now lives inside the Character Info panel as one
vertical scroll (stat/level block on top, TALENTS section below), and ranking a talent is **free** â€”
no "Talent Points" counter, no points granted per level-up, rank cap 3 unchanged.

### 1bv-status
- **`TalentTracker.cs`**: removed `Points`, `PointsPerLevel`, and the `Awake`/`OnDestroy`/`OnLevelUp`
  subscription (level-ups no longer award points). `CanSpend`/`TrySpend` replaced by free
  `CanRank(id)` (talent != null && rank < max) and `RankUp(id)` (gains one rank, returns true).
  `TalentSave` no longer carries a points field; `Serialize`/`Restore` updated. `LevelUpSystem`
  keeps firing `OnLevelUp` with no subscribers (harmless public event).
- **`CharacterInfoUI.cs`**: Info panel is now `BuildInfoTabScroll` â€” a `RectMask2D`+`ScrollRect`
  (`_infoContent`, top-anchored) holding a 480-high `StatBlock` built by the unchanged `BuildInfoTab`
  plus the `BuildTalentsView` section below it (28-high "TALENTS" header + 18 rows at 56 step, no
  inner ScrollRect, no points counter); content height sized to stat block + talent section so the
  full stack scrolls. `RefreshTalentsView` drops the counter + force-activate and sets
  `btn.interactable = tracker.CanRank(id)`; `RankUpTalent` calls `tracker.RankUp(id)`.
- **Skills tab**: `SkillSubTab.Talents` removed entirely â€” subtabs are now General / Class / Race
  only (buttons at -160/-30/100, Talents button deleted); all Talents branches removed from
  `RefreshSkillTree`, `RebuildSkillTree`, `SetSkillSubTab`, `UpdateSubTabButtons`, and `Refresh()`
  (Info case now calls `RefreshInfo()` + `RefreshTalentsView()`). Grep-confirmed zero remaining
  references to `_talentTabBtn`/`_talentPointsText`/`_talentContent`/`OnTalentsTab`/`CanSpend`/
  `TrySpend`/`SkillSubTab.Talents`.
- **Save compat**: old saves' unknown JSON "Points" field is ignored by `JsonUtility` â€” no migration
  needed; owned ranks + first-grant flag restore unchanged.
- **Docs**: `game-design.md` Â§3.9 rewritten (free ranks, list lives in the Character Info panel, no
  counter) and Â§3.2.1 subtoggle updated to "General / Class / Race".
- **Verification**: no CLI/Unity build per project rule â€” code review + grep only; pending play-test:
  (1) open Character â†’ Info tab scrolls; stat block at top, TALENTS rows below; (2) Rank Up adds a
  rank for free up to 3, button greys at cap; (3) level-up grants stat points only, no talent points;
  (4) Skills tab shows General / Class / Race only; (5) rank changes persist across save/load.

---

## 1bu. Alt magic grid fixed â€” MagicTestMatrix now actually builds/populates the list

User: "the alt magic grid didnt work" â€” pressing Alt (fighting mode, magic weapon held) showed only
an empty dark panel on the right edge. Cause: `MagicTestMatrix.cs` was a skeleton â€” `EnsureBuilt()`
created a background + three empty `RectTransform`s (no `ScrollRect` wiring, no layout, no rows), and
`AddSkillRow`/`RowIds` were dead code nothing called, so `CastId` could never run.

### 1bu-status
- **`MagicTestMatrix.cs` rewritten**: `EnsureBuilt()` now builds a real right-edge panel â€” title +
  status line, a `ScrollRect` body (viewport with `RectMask2D`, content with `VerticalLayoutGroup`
  + `ContentSizeFitter`), and one `Button` row per **castable magic skill** from
  `SkillCatalog.OfType(SkillType.Magic)` (skips passives), **grouped by school** (`Skill.DamageKind`
  in enum order) with a colored `DamageType` header per group. Rows highlight gold while armed, dim
  while on cooldown. `Update` refreshes row tints and closes on **Esc**; mouse-wheel + drag scroll;
  cursor unlocks while open.
- **Click flow** (`CastId`): top-up focus â†’ `TestGrant` if unlearned â†’ `ExecuteCharged` (chargeless)
  â†’ arm via new `MagicWheelUI.ForceArmMagic(id)` â†’ status "Cast OK/FAIL <name>". Cooldowns still apply
  (normal-cost choice); grid stays open for repeated testing.
- **`MagicWheelUI.cs`**: added `public static void ForceArmMagic(string id)` (sets `_armedSkillId` +
  `RefreshArmedChip`) so the bottom-left armed chip / LMB charged-release flow tracks the tested spell.
- **Dead code removed** from the matrix (`AddSkillRow`, `RowIds`, `_rowIds`, unused `_profileForSan`/
  `_armedId`/`AsRect`).
- **Docs**: `game-design.md` Â§5.16 Alt bullet rewritten (right-edge scrollable grid grouped by school,
  click-to-focus/learn/arm/cast) and the Â§3.8 charging-circle "Alt wheel" reference updated.
- **Verification**: no CLI/Unity build per project rule â€” code review only; grep-confirmed the removed
  symbols are gone and `SkillCatalog.OfType` / `SkillProfile.HasLearned`/`TestGrant`/`ExecuteCharged`
  / `SpellCaster.TopUpFocus`/`CooldownRemaining` signatures used all match. Note: the Alt **gate**
  (fighting mode + magic weapon held) in `MagicWheelUI.Update` is intentionally unchanged.
- **Play-test (pending)**: in fighting mode holding a magic weapon press Alt â†’ the right-edge grid
  lists spells grouped by school; a row click fast-casts + arms the chip (status line updates); Esc/
  Alt closes; scrolling works.

---

## 1bt. All skill-tree passives replaced with themed perks (446 nodes) + perk pipeline wired

User: "replace all the passive skill in the game (skill tree only, unique passive perk style)".
Every `passive: true` node in the six non-shield skill trees was converted from the flat
`Buff(StatType.X, Nf)` stat bump to a themed `Perk(PassivePerkType.Z, Vf)` Â§3.3 perk.

### 1bt-status
- **Infra** (new): `PassivePerkType.cs` (20 perks), `PassivePerkEffect.cs` (`IEffect` that registers
  into a `PassivePerkManager`), `PassivePerkManager.cs` (per-player aggregator; `Mul`/`Sum`/`Count`).
  `SkillCatalog.Perk(...)` shorthand added; the old `Buff(...)` helper was deleted.
- **Catalog rewritten** (446 nodes, zero `Buff(` remaining): Melee 17, Ranged 5, Magic 108,
  Stealth 67, Fortitude 135, Crafting 114. Each node keeps id/name/prereqs/cost; effect + tooltip
  re-theme with unique dark-fantasy text (no more "Permanent +N X").
- **Consumers wired** so every perk does something real:
  - `PlayerStats` folds â€” MaxHP (MaxHealthPercent), MaxStamina (StaminaMaxPercent), MaxFocusPoints
    (FocusMaxPercent), AttackSpeedScale/Multiplier (AttackSpeedPercent), MaxMoveSpeed
    (MovementSpeedPercent), MeleeAtkPower/LightAtkPower/AttackPower (AttackPowerPercent),
    MagicAttackPower (SpellDamagePercent), HealPowerMultiplier (HealPowerPercent), ParryWindow
    (ParryWindowPercent), DamageReduction (+DamageReductionFlat fraction, cap 80%), CooldownMultiplier
    (CooldownReductionPercent), CritChance (+CritChanceFlat), LootQuality (LootLuckPercent). New
    public getters: StaminaRegenMul, FocusRegenMul, HealthRegenPerSecondFlat, DamageReductionPerkFlat,
    CooldownReductionMult, TreeAttackPowerMul, TreeBackstabMul, TreeStaggerResistMul,
    TreeBlockEfficiencyMul, TreeCritDamageMul.
  - `PlayerController` â€” `MaxHP`/`MaxStamina` are now computed properties reading PlayerStats
    (Health + Endurance actually scale the player now, plus max-% perks); move speed includes
    `MaxMoveSpeed/BaseMoveSpeed`; stamina/HP regen include StaminaRegenMul + HealthRegenPerSecondFlat;
    `TakeDamage` applies DamageReductionPerkFlat (clamped 45%).
  - `SpellCaster` â€” focus regen Ã— FocusRegenMul; spell cooldowns Ã— CooldownReductionMult.
  - `SkillProfile` â€” weapon-skill cooldowns Ã— CooldownReductionMult.
  - `HitboxSystem` â€” NEW crit roll: physical swings crit at `PlayerStats.CritChance`% for Ã—2
    (Ã—TreeCritDamageMul); AttackPower Ã— TreeAttackPowerMul; backstab compounds TreeBackstabMul;
    knockback/force Ã· TreeStaggerResistMul.
  - `CombatController` â€” block stamina drain Ã· TreeBlockEfficiencyMul.
- **Commits**: `b55d5cd` (Melee), `f6ee70c` (Stealth â€” swept the Ranged rewrite via `git add -A`),
  `832e705` (Fortitude), `b9cf585` (docs), + this commit for Magic/Crafting/wiring/docs. Pushed to `main`.
- **Verification**: (no CLI/Unity build per project rule) grep-verified â€” `Buff(` count 0 across all
  partials, `Perk(PassivePerkType.` = 446, `passive: true` = 446; code-review of all consumer edits.
- **Play-test**: open Unity; learn passives in each tree and confirm (a) no compile errors,
  (b) tooltips show perk text, (c) crits/cooldowns/regen/move-speed/max-HP/stamina visibly reflect the
  perks, (d) save/load keeps perks applied (restore replays them via SkillProfile.RestoreState).

---

## 1bs. Shield wedge shrunk to 30Â°, standard wheel re-laid out (Fortitude re-spread)

User: "shield category spreading too wide taking up too much space so reduce the area of shield
category and recalculate the fortifide again". The standard PHYSICAL wheel (`BuildWheel`, non-compact
path) gave all 5 categories an equal 72Â° wedge (`sectorHalf = Ï€/5`), so Shield â€” the smallest
category (~31 skills) â€” spread its branches across a full-size sector. Changed the layout so **Shield
gets a fixed 30Â° wedge** and the other four (Melee / Ranged / Stealth / Fortitude) **share the
remaining 330Â° equally (~82.5Â° each)**.

- **`CharacterInfoUI.cs` `BuildWheel`** â€” replaced the fixed `categoryCenter = -90 + ciÂ·(360/5)` /
  uniform `sectorHalf` with a cumulative per-category layout: a `startAngle` accumulator begins at
  the top (-90Â°), and each category's `wedgeRad` (Shield 30Â°, others `(360âˆ’30)/(countâˆ’1)`) feeds its
  own `categoryCenter` and `sectorHalf = wedgeRad/2 âˆ’ 0.004`. The existing count-based full-arc ring
  spread (`1bq`) and `RingCapacity`-driven band allocation both consume the per-wedge `sectorHalf`,
  so **Fortitude's nodes automatically re-spread across its wider wedge** (0.716 rad vs. the old
  0.624). Compact wheels (Magic / Crafting per-school wedges) are untouched.
- **Docs** â€” `game-design.md` Â§8.2: sector list now includes Shield + describes the wedge contract
  (30Â° / ~82.5Â°). `PROGRESS.md` this entry.

### 1bs-status
No CLI/Unity build â€” verified by **code review** (project rule). Formal checks: adjacent category
bubbles (r=170, 128 px) don't collide â€” non-Shield centers are 82.5Â° apart (chord â‰ˆ 224 px), Shield's
neighbors land â‰¥82.5Â°/30Â° boundaries; Shield's ring3 (r=1150, 10 px pitch) capacity â‰ˆ 59 slots â‰¥ its
~25 L2 nodes; the four big wedges' ring3 capacity â‰ˆ 165 slots â‰¥ Fortitude's ~90 skills (~80 L2/L3).
- Play-test after this: (1) open General â†’ PHYSICAL â€” Shield occupies a visibly narrower cone and its
  branches pack tighter; (2) Fortitude's L1 / L2 / L3 nodes spread across a wider arc without
  overlap; (3) Melee / Ranged / Stealth take up the freed space and never collide with neighbor
  bubbles or run off their wedge edges.

## 1br. Render distance raised to 20 chunks (~600 m) â€” map/terrain streams much further

User: "increase the map rendering range". The game's "map" = the seamless chunk terrain; the
default streaming radius was 5 chunks (11Ã—11 = 121 chunks, ~165 m half-width). Raised to **20**:
41Ã—41 = **1,681 chunks â‰ˆ 600 m half-width**, ~5x linear / ~14x the loaded area, still comfortably
inside the 1000 m camera far clip.

- **`GameBootstrap.cs`** â€” the runtime default `rd.Radius = 5` â†’ `20` (the effective default; the
  scene derives `WorldStreamer.RenderDistance` at boot).
- **`RenderDistanceController.cs`** â€” serialized default field `Radius = 5` â†’ `20` so any
  designer-created / scene-serialized config matches; `MaxRadius = 160` untouched.
- **No other code change needed by review:** `ChunkLodManager.EffectiveCullDistance` already
  auto-matches `(radius + 1) Â· 30 = 630 m`, so no chunk is LOD-hidden while still streamed; the
  fill pipeline is time-budgeted (8 chunks/tick + ~6 ms cap, props 40 tiles/tick) so the bigger
  radius streams in over a few seconds with no hitching; scene fog is off (volume density 0).
- **Docs** â€” `game-design.md` Â§2.5: default 5 (121) â†’ **20 (1,681)**, max aligned to the code's
  hard clamp of **160**, boot note updated. Â§9.2 PC (High) target range (16-32 chunks) already
  covers 20.

### 1br-status
No CLI/Unity build â€” verified by **code review** (project rule). Budget at radius 20 â‰ˆ 1,681
chunks (~3 M terrain tris + streamed props) â€” heavier than the old 121-chunk fill but within the
Â§9.2 PC (High) 16-32-chunk target; background generation + prop streaming scale linearely and are
time-capped per tick, so no single-frame hitch is expected.
- Play-test after this: (1) open world â€” the terrain ring visibly extends to ~600 m in every
  direction (no hard world edge in view); (2) run straight â€” new chunks stream in ahead smoothly,
  old ones unload behind after the +1 hysteresis; (3) distant chunks still switch LOD correctly and
  are never hidden while in range; (4) spawn still ground-ready immediately (chunks fill over a few
  seconds, props after).

## 1bq. Fortitude branch (L1) skills spread across their wedge like the other categories

User: "the skills on fortitude layer 1 not spreading out like other category but grouped instead".
On the General skill-tree wheel, rings spread nodes by capacity then **centered** them in their
slots. Dense categories (Melee/Ranged/Stealth have ~25 L1 nodes on ring1, capacity 29) landed
`firstâ‰ˆ0` â†’ spanned ~83% of the wedge and looked spread. Fortitude's L1 has only **3** nodes
(Vitality / Relentless / Steadfast â€” `fort_vitality`, `fort_stamina`, `fort_steadfast`), so
`first=(29-3)/2=13` parked them at fractions 13.5/14.5/15.5 of the slots â€” a ~Â±0.03 rad sliver at
the wedge center where 12 px nodes at r=380 visibly stack/overlap, with empty arcs each side.

- **`CharacterInfoUI.cs` `BuildWheel`** â€” the "Spread partially filled rings" placement now spreads
  each ring's **own node count** over the wedge's **full arc** (`(used+0.5)Â·2Â·sectorHalf/count`,
  mirroring ring0's root spacing) instead of capacity-centering. Removed the now-dead
  `pitch`/`slots`/`first` locals (RingCapacity still drives the band-allocation pass above).
- **Docs** â€” `PROGRESS.md` this entry. No `game-design.md` change: Â§8.2's radial-tree description
  stays accurate; the layout detail is internal to `BuildWheel`.

### 1bq-status
No CLI/Unity build â€” verified by **code review** (project rule): the change is one angle formula;
single-node rings land exactly at the wedge center ((0+0.5)Â·2Â·half/1 = half); a ring's count can
never exceed its loader-balanced capacity, so count-based spacing (`2Â·half/count`) is always â‰¥ the
capacity pitch (`2Â·half/cap â‰ˆ nodeSize/radius`) â€” no overlap is possible where the capacity spacing
already held; `posOf`/lines/labels all consume the new positions identically. Affects every
partially-filled ring on both standard (Physical) and compact (Magic/Crafting) wheels uniformly.
- Play-test after this: open Skills â†’ General, Fortitude sector â€” Vitality / Relentless / Steadfast
  (ring1) now fan out across the wedge's full arc at ring0-style spacing, matching Melee / Ranged /
  Stealth; Melee's dense branch ring reads near-identical to before (slightly wider); single deep
  nodes stay centered; no node overlaps anywhere.

## 1bp. Every magic projectile dents the ground at impact (not just Earth's Stone Shard)

User: "also add impact dent to other magic projectile". Follow-up to `1bm` (Earth Shard carves at
impact). Previously only `TerrainShape.Crater` projectiles deformed the ground; fire / ice /
arcane / lightning / dark / wind / water bolts struck terrain with zero visual disturbance.

- **`SpellEffect.ResolveProjectileImpact`** â€” replaced the Earth-only carve with a universal one:
  every magic projectile down-probes the ground beneath its impact point and carves a **Crater**.
  Earth projectiles (`TerrainShape.Crater`) keep the full spell-scaled crater
  (`Mathf.Max(1.2, Radius)Â·radiusMult`); every other projectile leaves a small uniform **~1.4 m
  dent** (`1.4fÂ·radiusMult`). Same depth-clamp / no-void behavior, same "never at the caster's
  launch feet" guarantee; `_radiusMult` (charge) still scales both.
- **Docs** â€” `game-design.md` Â§3.7 (signature rule adds the universal projectile dent line) and
  Â§3.8 (terrain-shape bullet: Crater stays Earth's signature, others get a small dent);
  `magic-skills.md` Projectile delivery row notes the impact dent.

### 1bp-status
No CLI/Unity build â€” verified by **code review** (project rule). Reasoning: the Earth branch keeps
its exact radius formula; the new `dentRadius` default (1.4) applies to every non-Earth projectile
since no magic spell carries a non-Crater `TerrainShape` on a projectile delivery (Zone/Storm/
Summon spells never reach `ResolveProjectileImpact`); the ground `Physics.Raycast` + 
`TerrainDeformer.Apply(Crater)` matches the proven `1bm` path, so chunk rebuild/re-cook/save
(`tc_*.dat`) and Crater floor clamp all hold unchanged.
- Play-test after this: (1) cast **Fireball / Frost Bolt / Arcane Bolt / Chain Lightning / Dark
  Bolt / Wind Blade / Water Bolt** at world terrain â€” each leaves a small permanent ~1.4 m dent
  where the bolt lands; (2) **Stone Shard** still carves its bigger spell-scaled crater; (3) no
  dent ever appears at the caster's feet at cast time; (4) reload / walk away and back â€” the new
  dents persist (they ride the normal chunk-save path).

## 1bo. Earth Wall repeat casts no longer "shrink the world" â€” raised shapes are height-capped

User: "from the 2nd using onward the world got shrinking when using earth wall". Follow-up to the
`1bn` Wall fixes. Root cause in `WorldStreamer.DeformAt`: every raised shape writes
`current + s * lift` â€” it **stacks** on whatever the previous cast left there. Earth Wall at
2.6 m became 5.2 m on cast #2, 7.8 m on #3â€¦ Each taller stack embeds the player's
CharacterController deeper in the rebuilt chunk mesh; the next `Move()` depenetrates it more
violently with each cast, eventually launching the player far enough that either distant chunks
unload (streaming recenters) or the player ends up inside the raise where the terrain mesh culls
the view â€” readings as "the world got shrinking".

- **`WorldStreamer.DeformAt`** â€” raise branch now clamps `value` to **`base noise height + lift`**
  (a `ceiling`, mirroring Crater's `floorY` clamp): a wall/spire/ring/pillar reaches its intended
  height once and repeat casts can no longer stack it higher. Checks after the Spikes peak
  modifier, so even a spike tip respects the cap.
- **Docs** â€” `PROGRESS.md` this entry. No `game-design.md` change: the behavior is now "consistent
  fixed height" (matches the existing Crater depth-clamp precedent in Â§3.8).

### 1bo-status
No CLI/Unity build â€” verified by **code review** (project rule): the cap is one extra
`TerrainNoiseGenerator.GetHeight` sample (same as the Crater branch already pays, and the Spikes
branch already calls it via `current`) + a compare; placement after the Spikes modifier is
intentional so a peak can never exceed the ceiling; `ceiling` â‰¥ `current` on the first cast
(changes nothing for an unchanged tile), and on later casts it wins â€” so repetition is idempotent
in height while still re-running the mesh/re-cook + save path (a no-op visual but a correct
persist). Brace-balanced; no signature/caller changes.
- Play-test after this: (1) cast **Earth Wall** at the same spot repeatedly â€” the ridge tops out at
  ~2.6 m and **never grows taller**; (2) the player is **never launched / world never shrinks**;
  (3) Crater spells (Stone Shard / Boulder Crash) still dent and clamp exactly as before; (4) a
  wall built where a previous wall stood keeps its fixed 2.6 m height after reload (`tc_*.dat`).

## 1bn. Earth Wall fix: taller blocking wall + no more player "teleport" on repeat casts

User: "the earth wall are teleporting player the 2nd and so forth time using, and the wall that is
created is not high enough as it creates a wall that is not blocking player". Two root causes, both
in `WorldStreamer.DeformAt` (the Earth Wall skill, `1bm`, rides the existing `TerrainShape.Wall`
Zone path):

- **Wall not blocking**: the Wall `lift` was only **1.3 m** â€” below the ~2 m player capsule, so the
  ridge read as a low berm the CharacterController could walk over. Raised to **2.6 m** (full-height
  barrier; shoulders stay ~71Â° steep, above the controller's slope limit, so it cannot be climbed).
- **Player "teleport" on 2nd+ casts**: every repeat cast stacks the ridge on the previous height
  (`current + lift`), and a tall ridge rearing up under the node grows terrain into the player's
  capsule â†’ the rebuilt chunk collider intersects them â†’ the CharacterController violently
  depenetrates on the next `Move()` (a burst that reads as a teleport). Fixed with a **caster-foot
  keep-out**: raised shapes (Ring/Spikes/Wall/Pillar) now skip corners inside ~0.9 m horizontally of
  the player's feet, so terrain never grows under the capsule. Crater (excavation) is exempt.

- **`WorldStreamer.DeformAt`** â€” `Wall` lift 1.3 â†’ **2.6**; new `protectCaster` keep-out
  (`keepOutR = 0.9`, ground-sampled at the player's feet via `FindAnyObjectByType<PlayerController>`,
  only for non-Crater shapes, skipped inside the corner loop before the height write).
- **Docs** â€” `game-design.md` Â§3.8 terrain-shape bullet: Wall noted as a 2.6 m full-blocking ridge +
  caster keep-out rationale. `PROGRESS.md` this entry.
- Note: `ChunkObject.PatchRegion` momentarily nulls the chunk collider to force a re-cook; with the
  keep-out the player is guaranteed outside the raised patch, so that collider-less frame no longer
  affects them. Repeats at the same spot still stack taller, as designed.

### 1bn-status
No CLI/Unity build â€” verified by **code review** (project rule): keep-out evaluated per corner using
world-space center (`wx`,`wz`) vs. the player `transform.position` XZ, square-distance compare vs
`keepOutRÂ²` (0.81) â€” no ray, no allocations beyond the one nullable vector; placed after the
`influence <= 0` skip and before the smootherstep/height write, so non-overlapping and protected
corners both skip exactly like pre-existing early-outs; `protectCaster` is false for Crater so the
Stone Shard / boulder dent path is unchanged. Wall lift comment updated alongside the constant.
Brace-balance re-checked around the new block; no signature changes (all three callers â€”
`TerrainDeformer.Apply`, `SpellStorm.DeformGround`, `ResolveSummon` â€” are unaffected).
- Play-test after this: (1) cast the deep **Earth Wall** (needs Landslide) into open ground â€” a ~2.6 m
  ridge rears and the player **cannot walk through or over it**; (2) cast it repeatedly while
  standing next to/near the rise â€” the player is **never teleported/launched** (ground underfoot
  stays flat); (3) cast it directly on your feet spot â€” the wall simply does not grow under the
  character; (4) reload / walk away and back â€” ridges persist from the `tc_*.dat` chunks; (5)
  confirm Crater spells (Stone Shard / Boulder Crash) still dent exactly as before.

## 1bm. Stone Shard dents at impact (not the caster's feet) + new Earth Wall deep skill

User: "the terrain dent at the player feet when cast instead of impact fix it and add earth wall skill"
(placement choice: **Earth Wall gated behind Landslide**). Two changes: (1) the root **Stone Shard**
projectile no longer carves its crater at cast time just ahead of the caster â€” the dent now appears
exactly where the shard **strikes**; (2) a brand-new authored deep skill **Earth Wall** rears a taller
stone ridge along the cast.

- **`SpellCaster.FireProjectile`** â€” removed the launch-time crater carve (`pos + fwdÂ·0.7` â†’ a pit at
  the caster's feet/floor on every cast). The muzzle offset + spawn logic is unchanged.
- **`SpellEffect.ResolveProjectileImpact`** â€” when `_spell.TerrainShape == Crater`, down-probes the
  ground beneath the impact point (`impact + upÂ·0.1 â†’ downÂ·30`) and applies `TerrainDeformer.Apply`
  (`max(1.2, Radius)Â·radiusMult`, Crater) there â€” so Stone Shard dents where it lands (or under an
  enemy it hit), never at the caster's footing. Depth-clamped floor, persisted per chunk (Â§2.6).
- **`SkillCatalog.cs`** â€” new authored deep skill (Meteor pattern, no bank-slot change):
  **Earth Wall** (`magic_earth_wall`) â€” Zone, power 36, FP 26, cd 8s, range 10, radius 3.6, knockback
  3.5, `terrainShape: Wall`, prereq **Landslide** (`magic_earth_boulder_landslide`);
  `ResolveZone` deforms + orients the ridge along the cast axis (existing code, no new combat wiring);
  the solid ridge also blocks movement/projectiles. Root Stone Shard description reworded to the
  impact-carve ("â€¦carves a crater where it strikes"). Earth comment block updated.
- **`SkillCatalog.Magic.cs`** â€” Earth-school comment notes the projectile carves at impact and the
  Earth Wall deep skill rears a taller ridge.
- **Docs** â€” `game-design.md` Â§3.7 signature line (adds Earth Wall; projectile wording now "carves at
  the impact point") and Â§3.8 terrain-shape bullet (projectile reshape on strike + Earth Wall row);
  `magic-skills.md` root Stone Shard row (`terrain:Crater (impact)`) and **Earth Wall** row under
  **Landslide**, right beside Meteor; `PROGRESS.md` this entry.
- Out of scope: Zone/Storm/Summon deformations already dent at the right points (aim / per-boulder /
  summon ground-target) â€” untouched.

### 1bm-status
No CLI/Unity build â€” verified by **code review** (project rule): carved-only-on-impact â€” the new
`TerrainDeformer.Apply` call sits inside `ResolveProjectileImpact` after damage + impact-fx and before
`Destroy`, so exactly one carve per projectile that resolves; down-probe uses a 0.1 up-offset so a
ground-level impact still finds the surface; `_radiusMult` mirrors the launch-carve's `sizeScale`
scaling; `_dir` supplied for orientation (Crater ignores it) â€” no new fields/imports needed
(`TerrainDeformer` is global-namespace static, already used by this caster). Earth Wall reuses the
exact authored-skill pattern of `magic_earth_meteor` (`Add(...)` in `BuildMagic` + `P(prereq)`), so
`ExpandTree`'s prereq-depth walk resolves it the same way â€” no L1/L2 slot cap touched (the boulder
bank keeps its 5 children). All touched files brace-balanced.
- Play-test after this: (1) cast **Stone Shard** repeatedly â€” **no pit appears at your feet**; a
  shallow crater appears where each shard lands/impacts (step off the QA platform onto world terrain);
  (2) after learning **Landslide** (boulder line), check **Earth Wall** (`magic_earth_wall`) appears
  in the tree under Landslide and rears a Wall ridge along the cast, crushing with knockback;
  (3) reload / walk away and back â€” both the impact craters and the Earth Wall ridge persist from the
  `tc_*.dat` chunks; (4) confirm repeated casts never grind a void (Crater floor clamp holds).

User: "i want the earth magic to have impact on the terrain" (confirmed scope: every earth spell dents
the ground; some also raise). Earth reshaping already existed (1az/1bb) but only for Zone spells that
explicitly carried a `terrainShape` â€” Boulder Crash, Crash, Rockfall (Storm), Tectonic, Aftershock and
the whole golem/summon line hit the ground with **zero** deformation. Now **every damaging Earth spell
carries a terrain shape and all four delivery paths feed `TerrainDeformer` â†’ `WorldStreamer.DeformAt`**
(permanent, depth-clamped, persisted per chunk, Â§2.6).

- **`SkillCatalog.Magic.cs`** â€” shape assignments so the ground reacts to each spell:
  - Dent (**Crater**): **Boulder Crash**, **Crash**, **Rockfall** (Storm), **Tectonic**.
  - Raise (**Ring**): **Aftershock** (joins the quake family's rings).
  - Raise (**Spikes**): **Stone Effigy / Stone Sentinel / Stone Guardian / Colossus** (rocks erupt
    where the construct tears out of the earth). Descriptions updated to match. Earth-school comment
    now states the "every spell deforms" rule.
- **`SpellStorm.cs`** â€” new `DeformGround(at)`: each strike (Rockfall) down-rayscasts to the real
  ground and carves a small Crater (`max(RadiusÂ·0.55, 1.2)`) exactly where each boulder lands;
  gated on `_spell.TerrainShape != None` so non-earth storms stay purely visual-elements (no-op in
  `TerrainDeformer` anyway).
- **`SpellCaster.ResolveSummon`** â€” erupts a modest rock field at the ground-target point
  (`min(RadiusÂ·0.4, 2.5)`, Spikes) when the spell carries a shape â€” the golem line "rises" out of
  real ground. Zone spells need no new code â€” `ResolveZone` already calls `TerrainDeformer.Apply`
  for every shape-tagged zone. (The root **Stone Shard** projectile's launch tear-pit was later
  moved to the impact point in `1bm`.)
- **Delivery coverage now**: Zone (Crater/Ring/Spikes/Wall/Pillar) + Storm (per-strike Crater) +
  Summon (Spikes eruption) + Projectile (Stone Shard crater, carved at impact per `1bm`). Every Earth
  magic cast leaves a mark.
- **Docs** â€” `game-design.md` Â§3.7 signature line and Â§3.8 terrain-shape bullet updated ("current
  build" notes per-delivery coverage + depth-clamp everywhere); `magic-skills.md` Earth rows updated
  with `terrain:` tags.
- Out of scope (unchanged): Earth-damage skills in the **Fortitude / Melee / Shield** trees
  (Stoneskin line, Tremor Slam, Earthwarden, Grim Wallâ€¦) are physical strikes, not magic-school
  spells â€” they keep their existing no-terrain behavior.

### 1bl-status
No CLI/Unity build â€” verified by **code review** (project rule): all 9 edited `Spell(...)` factory
calls parse with the existing `terrainShape:` parameter (SkillCatalog.cs:130 default `None`);
`SpellStorm.DeformGround` gated on `_spell.TerrainShape != None`, raycast `at + upÂ·0.5 â†’ downÂ·10`
lands near the strike (strikes centre at ground-level; `_spell` null is already guarded earlier in
the component); `TerrainDeformer.Apply` is a static helper callable from both MonoBehaviour
coroutines (main thread) and SpellCaster â€” no import added (global namespace); `ResolveSummon` uses
`spell.Radius` (turret range 6) Ã— 0.4 clamped to 2.5 â†’ small bump, no wide reshape; all touched
files brace-balanced. Earth spell count / tree layout unchanged (no new skills, no retags).
- Play-test after this (step off the QA platform onto the **world terrain** â€” the floating slab is
  not the heightmap, so casts while standing on it carve invisibly below): (1) cast **Boulder Crash
  / Crash / Tectonic** â€” a wide permanent crater dents the aim point; (2) cast **Rockfall** â€” the
  whole area ends pocked with small craters under each landing boulder; (3) cast **Aftershock** â€” a
  stone ring rears up; (4) summon **Stone Effigy / Sentinel / Guardian / Colossus** â€” a small rock
  field erupts where each construct rises; (5) reload / walk away and back â€” every dent and raise
  persists from the `tc_*.dat` chunk files; (6) confirm repeated casts never grind a void (the
  Crater floor clamp holds).

---

## 1bk. NaN corner-grid bug (flat chunks) fixed in the streamer; arena-lane force-rebuild on New Game + F12

Report (after `1bi` play-test): the arena-lane terrain (bench junction `tc_-1_0/-1_1/-1_2`) intermittently rendered
**flat at height 0** even though the full-chunk flatten files were purged. Root cause was **not data** ï¿½?"
all 11 `worlds/1337/tc_*.dat` files re-validated clean (`NWTC` v1, seed 1337, coords match filenames, rolling
heights 11.7-15.5 in the sparse lanes). The bug was a **zero-vs-NaN corner sentinel bug** in
`WorldStreamer.BuildOrLoadChunk`: `new float[gridSize, gridSize]` zero-fills every corner, and an unstamped
corner then reads as "present" because `float.IsNaN(0f)` is false ï¿½?" so every corner without a saved mod
collapsed to height 0 instead of regenerating from noise. The sparsest saves (smallest stamp count) showed
the biggest flat plane.

- **Fix (2i):** the corner grid (`WorldStreamer.cs:278`) is now **NaN-prefilled** in a pre-loop, so only
  genuinely saved corners count as present and every other corner re-rolls from the 5-octave generator.
- **Fix (2ii):** new **`WorldStreamer.ForceRebuildArenaLane()`** (public) unloads the 3 arena-lane chunks and
  re-queues them through the same `UnloadChunk` + `EnqueueChunkIfNeeded` streaming path, so a stale flat
  mesh is dropped and re-streamed from noise + saves. No save files are touched.
- **Fix (2iii):** auto-called from `GameManager.StartNewGame()` (after bench respawn); plus an editor
  hotkey **F12** in `GameManager.Update` (the `#if UNITY_EDITOR` F-key block; F9 is the blackmail ending,
  so the force-rebuild took F12) to re-fire it live during play-test. `WorldStreamer` is resolved as a new
  field in `GameManager.AutoResolveReferences` (streamer is created by `GameBootstrap`, not `GameManager`).

### 1bk-status
No CLI/Unity build ï¿½?" verified by **code review** (project rule): `float.NaN` prefill sits before the mod
stamp loop and after the `IsNaN` guard contract; `ForceRebuildArenaLane` -> `ForceRebuildChunk` ->
`UnloadChunk(TerrainChunkCoord)` + `EnqueueChunkIfNeeded` (both private methods confirmed present by literal
scan); GameManager field + resolve + F12 hook + `StartNewGame` auto-call all parse inside the right methods
(confirmed by line-number context). No `using` needed ï¿½?" both classes are in the global namespace.
- Play-test after this: **(1)** New Game ï¿½?" arena-lane chunks roll with natural noise, no flat-0 patch at the
  bench junction; **(2)** press **F12** in the editor ï¿½?" the lane rebuilds instantly without touching save
  files; **(3)** deform a lane with the Earth tool, leave the area, return ï¿½?" the edited heights persist and
  the rest of the chunk is noise, not a flat plane.

---

## 1bi. Test ground = independent floating platform; legacy flatten saves purged; world terrain untouched

Supersedes the "real procedural terrain" ranges of `1bf`/`1bg` (Â§2.7). The QA bench no longer tries to
place props on the world's rolling terrain at all â€” it now builds a **self-contained floating
platform** (solid slab + collider + 4 corner posts, `BuildTestGround`) in `Awake`, floats clear of the
natural ground (coarse 9Ã—9 read-only sample of the world's own 5-octave noise + 12 m clearance), and
lays every lane flat on its **single level top** (`PlatformTopY`). The world terrain is never read for
placement and **never written** (no carve/flatten/chunk-save/prop suppression).

- **Root cause of the "still flattened map" report was DATA, not code:** the previous runs' FlattenAt
  feature had persisted full-chunk flatten saves â€” `worlds/1337/tc_-1_-1 â€¦ tc_1_0` etc. â€” 13 files
  where all 900 tiles carried the old hub's uniform height (parsed one: `NWTC` v1, seed 1337, 900 mods,
  every corner `14.988`). Current code no longer flattens, but `ChunkSaveManager.TryLoadChunk` still
  reloaded those saves, so each chunk rendered as a single flat surface (the "test field" the player
  saw). **Fix:** deleted the 13 legacy full-chunk flatten saves (sparse files â€” real Earth-spell/tool
  edits â€” kept), so the map streams back exactly as the noise generator designed it.
- **`NewWorldTestGround.cs`** â€” `PrepareArenaGround`/`GroundAt`/`WaitForSpawnGround`/
  `WaitForArenaTerrain`/`ArenaChunkCoords` (+ `_streamer`/`_groundSampled`/`_spawnGroundReady`
  fields) removed; replaced by `BuildTestGround()` (idempotent, called from `Awake` â€” platform exists
  before `PlayerController.ResetPlayer` runs in `Start`, so no void-race). All bench lanes key
  placement off `PlatformTopY` (`GetSpawnPoint` = top + 2 m). `IsArenaReady` now = platform built.
- **`WorldBuilder.Farming.cs`** â€” `TillGround` gets an optional `groundY` param; the bench passes
  `PlatformTopY` so the floating field tiles sit on the platform (world callers keep default y=0;
  road override still wins).
- Bench-spawn order (â€œplatform first, then playerâ€, then lanes deferred one-per-frame, isolated
  try/catch) and the rest of the kit/networking/WIP details unchanged from `1bg`.

### 1bi-status
No CLI/Unity build â€” verified by **code review** (project rule): no dangling refs to the removed
`GroundAt`/gates/fields; `TillGround(Vector3,float)` overload compiles clean; `BuildTestGround`
idempotent + self-guards; bench lanes only read `PlatformTopY`.
- Play-test after this: **(1)** New Game â€” no NullReferenceException; the player stands on the floating
  platform (not the terrain, not the void) at `(0, topY+2, 0+ â€¦)`; **(2)** the bench lanes (farming,
  livestock, enemies/boss/dummies, buildings, NPCs, weapon pedestals/racks, tool/food kit pickups) sit
  level on the platform top; **(3)** the world map is **rolling terrain again** â€” each chunk shows its
  natural 5-octave surface and per-tile noise, no more single-flat-surface "test field"; **(4)** prop
  collisions work on the platform (slab collider); **(5)** farming still tills/plants/water/fertilizes
  on the platform top; **(6)** NPCs/enemies behave in the bench area; **(7)** second New Game doesn't
  duplicate the bench.

---

## 1bh. New Game NullReferenceException at startup â€” tool-kit pickup path built before the world container existed

Report (after `1bg`): on `GameManager.Start` â†’ `StartNewGame` â†’ `GrantBenchBag` â†’ `SpawnToolKit`,
`WorldBuilder.CreateToolPickup` threw `NullReferenceException` at `pickup.transform.SetParent(
_worldRoot.transform)` and the tool lane never spawned.

- **Root cause**: `WorldBuilder.EnableLegacyGeneration` defaults to **false**, so `_worldRoot` is only
  created lazily by `EnsureWorldRoot()` â€” the farming / NPC / blueprint APIs call it, but the
  `SpawnPickup` / `ThrowPickup` / `ThrowCage` path never did. `GrantBenchBag()` runs synchronously
  from `GameManager.StartNewGame()` (which itself runs from `GameManager.Start()`, before any bench
  lane had created the root), so `_worldRoot` was still null and the NRE aborted the lane.
- **`WorldBuilder.cs`** â€” `CreateToolPickup()` (covers `SpawnPickup` + `ThrowPickup`) and `ThrowCage()`
  now call `EnsureWorldRoot()` before touching `_worldRoot`, matching the pattern the other WorldBuilder
  APIs already use; any early/legacy-off caller is now safe.
- **`NewWorldTestGround.cs`** â€” removed `SpawnToolKit()` from `GrantBenchBag()` (a world-placement lane
  has no business running from the synchronous bag re-grant â€” the inventory clear cannot touch world
  pickups). The kit is placed once by the deferred bench lane (`EnableTools`), i.e. after the
  `1bg` ground gates, so every drop sits on the loaded terrain instead of a boot-time noise height.
  `GrantBenchBag` now only does the actual bag grants (weapons/skills/gear/races) and no longer
  references `WorldBuilder`.

### 1bh-status
No CLI build â€” verified by code review (project rule): `WorldBuilder` derives from
`MonoSingleton<WorldBuilder>` (Instance set in Awake, so `WorldBuilder.Instance` is non-null by
`GameManager.Start`) while `_worldRoot` stays null under the default `EnableLegacyGeneration = false`
until `EnsureWorldRoot()`; the two added calls are the only `_worldRoot` uses on the pickup path;
`GrantBenchBag`'s remaining grants do not touch `WorldBuilder`; all touched files brace-balanced.
- Play-test after review: (1) New Game (and New Game from the pause menu) â€” no NullReferenceException in
  the console; (2) the tool/food kit pickups appear on the arena's east edge on the real ground and can
  be picked up with E; (3) weapons/skills/gear/races still granted on New Game; (4) start a second New
  Game â€” the kit does not duplicate.

---

## 1bf. Test ground leaves the terrain untouched â€” no more flatten/carve at the arena coordinate

Fixes the report "when testground spawn the terrain that already spawn at that coordinate get
deleted". Every boot the test ground permanently carved the arena: `WorldStreamer.FlattenAt` raised
the 120 m footprint to the pad's highest point and **persisted it to the chunk save files**, while
`ClearPropsInsidePlatform` + `ChunkObject`'s `IsInsidePlatform` check destroyed/blocked the trees and
rocks there. All of that is gone â€” the bench now spawns on the untouched procedural terrain.

- **`NewWorldTestGround.cs`** â€” removed `FlattenArenaTerrain()` (and its `FlattenAt` call) and
  `ClearPropsInsidePlatform()` / `IsTreeOrRock`; removed the `PlatformMin/MaxX/Z` statics +
  `IsInsidePlatform`; `PrepareArenaGround` now just samples the natural ground at the arena centre
  (and captures the world seed) with no 61Ã—61 scan or flatten feather. Added `GroundAt(x,z)` (world
  noise height) and switched every lane to place each prop on the natural ground at its own anchor:
  farming plots, livestock, enemies/dummies/boss, buildings, NPCs, weapon pedestals, and the tool
  pickups. `GetSpawnPoint` samples the ground at the player's actual XZ. `CreatePlatform`'s meaning
  changed from "carve the pad" to "wait for the arena's chunks, then pull the player onto the natural
  terrain first" ("ground first, then player" kept; the bench is never placed mid-void).
- **`ChunkObject.cs`** â€” `StepProps` no longer skips tiles inside the platform: trees/rocks spawn
  naturally on every tile (they sit on real ground now â€” no raised pad to poke through).
- **`game-design.md`** Â§2.7 â€” rewritten: the arena is the actual generated terrain, left completely
  untouched (no tile edits, no chunk-save writes, no prop suppression/clearing).
- **`WorldStreamer.FlattenAt`** kept as public API (unused); the Earth-spell `DeformAt` pipeline is
  untouched. Its doc comments no longer claim the test ground as a caller.

### 1bf-status
No CLI build â€” verified by code review (project rule): all removed symbols (`FlattenArenaTerrain`,
`ClearPropsInsidePlatform`, `IsTreeOrRock`, `IsInsidePlatform`, `PlatformMin/MaxX/Z`, `_flattenFeather`)
were referenced only by the test ground + `ChunkObject` (grep-clean); `PrepareArenaGround`/`GroundAt`
call the existing `TerrainNoiseGenerator.GetHeight(long, float, float)` overload; `PlatformTopY` static
kept for the remaining readers; all touched files brace-balanced.
- Note: an **existing** chunk save from an earlier session still holds the old flattened pad â€” start a
  fresh world/delete saves to see the untouched terrain.
- Play-test after review: (1) boot â€” the arena coordinate keeps the original rolling terrain (no flat
  pad, no height writes); (2) each bench prop (racks, pickups, animals, buildings, NPCs, enemies) sits
  on the natural ground and follows slopes, not floating/sunk; (3) trees/rocks now appear among the
  bench (nothing suppressed); (4) the player still lands on solid ground first and benches spawn after;
  (5) farming/Earth-spell edits near the arena still deform real terrain as before.
---

## 1bg. Player never teleported/falls into the void â€” hard spawn-ground gate replaces the loading race

Report (after `1bf`): "the terrain under player load to long result in player falling into the void".
The old flow waited for the whole 120 m footprint behind a **15s vote**: if the chunks didn't all
finish in time, the end-of-bench fallback teleported the player to the arena spawn regardless â€” over
whatever terrain was (or wasn't) loaded. Fixed by making the player's ground a correctness gate.

- **`NewWorldTestGround.cs`**:
  - New **hard gate** `WaitForSpawnGround()`: waits until the chunk directly under the arena spawn
    point (`PlatformCenter` + `0.45 Ã— size` on Z, chunk `(0,1)` for the defaults) is in
    `WorldStreamer.LoadedChunks` â€” mesh + collider are applied before a chunk registers, so this
    guarantees real standing ground. It streams among the first (near the boot focus). If it never
    streams (30s), `_spawnGroundReady` stays false, the player is kept on the solid boot chunk, and
    an error logs â€” **no void fall, ever**.
  - `PlacePlayerOnArena()` now **self-guards**: it verifies the chunk under the spawn point is in
    `LoadedChunks` (mesh + collider applied) before moving the player and otherwise logs a warning
    and stays put â€” the end-of-bench fallback stays unconditional but can never teleport over a void.
  - `WaitForArenaTerrain()` is now the **soft** footprint gate (still 15s): tile-dependent lanes
    (farming tills real soil, NPC placer, buildings) prefer the full footprint but soft-fail via
    `RunSafely` if it never arrives.
  - `GroundAt(x,z)` now prefers the **live loaded terrain** â€” bilinear-sample the loaded tile's
    4 corners via `WorldStreamer.TryGetData` (honours old flattened saves and Earth-spell edits) â€”
    and falls back to pure noise for tiles that haven't streamed. Player/bench placement therefore
    matches the actual ground height, never the pending-noise height.
- **`game-design.md`** Â§2.7 â€” boot-order bullet documents the hard vs soft gates + live-terrain sampling.

### 1bg-status
No CLI build â€” verified by code review (project rule): `LoadedChunks` is populated after
`ChunkObject.ApplyMerged(... buildCollider: true)` on the main thread, so a chunk present there has
mesh + collider; tile registration into `_loadedData` happens synchronously in the same frame as
`_loadedChunks`, so `TryGetData` never races the gate; corner order for the bilinear sample matches
`ChunkData`'s documented NW/NE/SE/SW layout and `BuildOrLoadChunk`; all touched files brace-balanced.
- Play-test after review: (1) boot (fresh) â€” player lands on the natural terrain at the arena, no
  drop, no flicker; (2) deliberately stall terrain loads (e.g. temporarily lower `ChunksPerFrame` /
  move `PlatformCenter` far away) â€” the player stays grounded on the boot chunk and never falls; the
  error logs and lanes still spawn; (3) load an old save that has the flattened pad â€” the player and
  bench land on the real (still-flattened) saved ground, not below it.

---

## 1be. Test ground spawns the tool kit as pickups â€” "spawn tools on testground for player to pickup"

The non-weapon tool/food kit (removed from the start bag in `1p`) is back as **world pickups** laid
out along the test ground's east edge â€” the player walks over and presses E on each drop to collect
it (the existing `Pickup_<id>` drop path â†’ `ToolManager.TryPickupTool`), instead of the bag being
seeded at game start.

- **`Opt/PickupAmount.cs`** (new) â€” a tiny optional tag a world `Pickup_<id>` drop can carry;
  `ToolManager.TryPickupTool` now grants `PickupAmount.Amount` (default 1) when present, so a single
  pickup can hand over a stack (foods Ã—5).
- **`NewWorldTestGround.cs`** â€” new `EnableTools` lane (default on): `SpawnToolKit()` lays the 10
  tools (axe/pickaxe/hoe/hammer/scythe/watering_can/fertilizer/club/rosary/fishing_rod) + 5 food
  stacks (banh_mi/com_tam/nuoc_dau/mi_chinh/xap_phong Ã—5) as `WorldBuilder.SpawnPickup` drops along
  the platform's east edge (mirroring the west-edge weapon pedestals). Idempotent (`_toolKitSpawned`),
  so the `GrantBenchBag` re-seed after a new game's inventory wipe can't double-spawn pickups still
  sitting on the ground.
- **`ToolManager.Pickup.cs`** â€” `TryPickupTool` honors the `PickupAmount` tag (grants the tag's count
  instead of 1).
- **`game-design.md`** Â§2.7 â€” notes the east-edge tool/food pickup row.

### 1be-status
No CLI build â€” verified by code review (project rule): `WorldBuilder.SpawnPickup` exists and builds
the item visual (`ItemBuilder` covers all 15 ids); `TryPickupNearby` raycasts triggers
(`QueryTriggerInteraction.Collide`), so the `Pickup_*` trigger BoxCollider is grabbable within the 4 m
pickup ray; the PlayerController E-raycast in `HandleInteractionKeys` falls through to
`ToolManager.TryPickupNearby` for a `Pickup_*` name (no NPC/stand/chest branch matches);
`PickupAmount` has no name clash; all touched files brace-balanced.
- Play-test after review: (1) walk the arena's east edge â€” each tool + food drop is visible and
  grabbable; pressing E adds the item to the hotbar/bag and destroys the drop (foods land as +5);
  (2) the west edge weapon racks still work unchanged; (3) start a new game (bag wipes) â€” the tool
  drops are still on the ground once, no duplicates.

---

## 1bd. Magic statuses now interact on the same target â€” wet douses fire, fire melts ice, chill builds into frost

User: "wet would stop burning, chill would stack into frost but if hit wet player then chill stack
faster and stuff like that" â†’ designed and shipped the **status-interaction gauge grid** (Â§3.7),
runtime-verified via `git diff/index` code review (no Unity build run, per project rule):

- **Chillâ†’Frost build gauge (`ChillStatus.cs`, new)** â€” Ice's signature is no longer a one-shot
  slow. Each Ice hit adds **1 cold** to the gauge (the hit target's root); at **5 cold** the gauge
  converts into a full **Frost freeze** (heavy `ApplySlow 0.5 / 3.5 s`, the literal freeze). Gauge
  self-decays; sitting at 4/5 doesn't stick forever.
- **Water conducts cold (`WetStatus.IsWet`)** â€” a **Wet** target gains **+2 cold per Ice hit** (water
  conducts), so a soaked foe freezes in 3 hits instead of 5.
- **Fire melts ice instantly (`ChillStatus.Melt`)** â€” any Fire hit on a chilled/frosted target resets
  the gauge to zero (fire-vs-ice tug of war).
- **Water douses fire (`SpellDoT.RemoveType`)** â€” applying **Wet instantly removes an active Burn
  DoT** off the target, and a **soaked target cannot be ignited** while wet (fire-vs-water: water
  always wins). Fire Burn is now gated in `SpellCaster.ApplyStatus` behind `!WetStatus.IsWet(target)`.
- Cross-package wiring: `SpellDoT` gained `RemoveType(GameObject, DamageType)` (douse helper, null-safe)
  + `TakeDamage(int, DamageType)` so Burn drops its own damage numbers; `WetStatus.Apply` now douses
  burns; `SpellCaster.ApplyStatus` eases **Chill â†’ ChillStatus.Apply**, **Burn** (signature) now
  routes to the wet-gated doT + fire-melt.

### 1bd-status
Compile verified by code review + git diff (index == worktree). No Unity build run (project rule:
no CLI/Unity build). Pending play-test items: chill 5-stack â†’ frost freeze transition, wet +2 gain,
fire-melt of a frozen foe, and wet dousing an active burn (see `game-design.md` Â§3.7 for expected
behavior).

## 1bc. Signature statuses are now guaranteed per magic element â€” Iceâ†’Chill, Darkâ†’Blind, Arcaneâ†’none

User: "add status condition for each magic element attack" â†’ then refined the mapping: **Iceâ†’Chill,
Lightning keeps Stun (Stagger), Darkâ†’Blind, Arcaneâ†’no status** (Fireâ†’Burn and Waterâ†’Wet unchanged).
Every magic attack of an element with a signature now applies it automatically on hit, even when the
skill declares no explicit status (a per-skill `statusEffect:` still overrides the default).

- **`ElementSignatureStatus.cs`** (new) â€” the single source of truth: Fireâ†’Burn, Iceâ†’Chill,
  Lightningâ†’Stagger, Darkâ†’Blind, Waterâ†’Wet, everything else (Arcane, Wind, Earth, Holy, Physical)
  â†’ null (no automatic status).
- **`StatusEffectType.cs`** â€” adds **`Chill`** (light cold, the Ice signature) and **`Blind`**
  (black-fog); `Frost` is kept as the heavier full-freeze status (the literal Freeze / Deep Freeze
  spells), `Stagger` stays the Lightning stun, `Rot` remains defined but is no longer Dark's default.
- **`BlindStatus.cs`** (new) â€” attaches to the victim's root: a semi-transparent black fog dome
  follows the character for the duration; when the victim is the local player the fog hugs the main
  camera instead, visibly cutting their field of vision (only that player sees it).
- **`SpellCaster.ApplyStatus`** â€” resolves the effective status as explicit `spell.StatusEffect`
  when the skill declares one, otherwise `ElementSignatureStatus.For(spell.Type)`; the old
  `!spell.AppliesStatus â†’ return` gate is gone so the signature flows to every delivery.
  Switch gains `Chill` (`ApplySlow(0.25, 2.5)`), `Frost` heavier (`ApplySlow(0.5, 3.5)`), and
  `Blind` (`BlindStatus.Apply`, 4s). `StatusProcChance` gate unchanged.
- **Swept the existing schools** so the new identity actually shows in-game: Ice spells
  (Frost Bolt, Blizzard, Chill Touch, Chill Soul, Frost Bite, Frost Obelisk, Cold Stare)
  Frostâ†’Chill while **Freeze** and **Deep Freeze** (was Stagger) now use the heavier **Frost**;
  Dark spells (Dark Bolt, Void Rend, Devour, Shadow Totem, Consume, Hunger, Eclipse) Rotâ†’**Blind**
  with "rot" flavor text updated; Arcane spells (Arcane Bind, Shackles, Hold) dropped their
  **Stagger** so Arcane is pure force.
- **Docs** â€” `game-design.md` Â§3.7 (status table gains Chill + Blind rows, the signature paragraph
  now states statuses are automatically applied per element with explicit overrides; Fireâ†’Burn,
  Iceâ†’Chill, Lightningâ†’Stun, Darkâ†’Blind, Waterâ†’Wet, Arcaneâ†’none, Wind=knockback, Earth=terrain,
  Holy=heals); `magic-skills.md` entries updated to the new statuses + a header note.

### 1bc-status
- No CLI build â€” verified by code review: `ElementSignatureStatus.For` covers exactly the five
  signature elements and returns null otherwise (Arcane/Wind/Earth/Holy/Physical â†’ `ApplyStatus`
  bails before the switch); explicit overrides still win (Freeze/Deep Freezeâ†’Frost, Deep Freeze
  no longer staggers); Chill/Frost/Blind cases added to an exhaustive switch; `BlindStatus` uses
  code-based Standard-shader transparency (Fade) + primitives, cleans up its fog on expiry, and the
  player-vs-world follow logic has no null refs (Camera.main guarded).
- Play-test after review: (1) cast Ice â€” target visibly slows a little (**Chill**); **Freeze** /
  **Deep Freeze** slow much heavier (**Frost**, not stun); (2) Lightning still staggers/stuns;
  (3) cast **Dark Bolt / Eclipse** at a group â€” each affected enemy is engulfed in black fog for ~4s
  (no DoT ticks now); (4) **Arcane Bind / Hold** deal pure damage, no stun; (5) Fire still burns,
  Water still wets, Earth meteor still only craters terrain, Wind still only knocks back;
  (6) if the player somehow gets blinded, the fog clings to the camera and dims vision.

---

## 1bb. Earth magic gains "Meteor" â€” the school's sky-event spell (cratering deep skill)

User: "add meteor event as a earth magic". The Earth school already owns terrain reshaping (1az:
Ring / Spikes / Wall / Pillar / Crater) but had no "big rock from the sky" capstone â€” the closest
spells were Fire-school Meteor / Meteor Rain. Added an **Earth-school Meteor** as an authored deep
skill gated behind the falling-rock line (`magic_earth_boulder`), so it renders in the tree's
auto-growing rings with no design-table changes.

- **`SkillCatalog.cs` (BuildMagic)** â€” new authored skill:
  `magic_earth_meteor` "Meteor", Earth damage, power 40, FP 28, cd 9s, **Zone** delivery at range 12,
  radius 4, knockback 4, **`terrainShape: TerrainShape.Crater`**. Zone + Crater was chosen (not a
  Storm) so the crater resolves ON the ground at the aim point â€” the "meteor event" landing â€” reusing
  the depth-clamped solid-floor carve from 1az (spamming can't grind through the floor). Prereq
  `P("magic_earth_boulder")` â†’ effective Layer 2 under Boulder Crash (branch Layer-1 slot tables are
  full at 5, so a designed/slot approach would have bumped the `ci < 5` layout cap â€” auth deployed
  instead, same pattern as Blizzard). Earth school comment updated.
- **`magic-skills.md`** â€” Meteor added under Stone Shard's Boulder Crash line; also fixed two stale
  earth tags from the 1az retag (Landslide â†’ `terrain:Wall` "an earth wall rears up..." and Stone
  Pillars â†’ `terrain:Pillar`).
- **`game-design.md`** â€” Â§3.7 signature line notes the deep Earth **Meteor** skill craters the ground
  where it lands.

### 1bb-status
- No CLI build â€” verified by code review: `Add(list, id, name, SkillType.Magic, false, Focus(...),
  true, DamageType.Earth, Spell(...), P("magic_earth_boulder"), desc)` matches the authored-skill
  signature; `magic_earth_boulder` exists in the built tree (designed L1, built before/independently
  of this add); prereq â†’ layer-2 depth keeps it inside the Earth wedge's growing rings; the spell
  factory accepts `knockback:` / `deliveryRange:` / `deliveryRadius:` / `terrainShape:`; GrantAllSkills
  (test ground) and the skill wheel read it automatically from `SkillCatalog.All`.
- Play-test after review: learn/arm **Meteor** from the Earth wedge â†’ cast at a flat area â€” a wide
  crater dish carves into the ground at the aim point (never a void), enemies near the point take 40
  power with knockback 4, and the crater persists after reload (1az pipeline); Tree doesn't overflow
  the Boulder Crash wedge (Meteor sits one ring out from the other boulder children).

---

## 1ba. Boot places the player in the void & the test arena never visibly spawns â€” fixed with "ground first, then player"

Play-test feedback after the 1ay arena carve: at boot the player appears to fall through the world into
the void, and the flat test ground isn't visibly generated. Root cause review: `PlayerController.ResetPlayer`
teleported straight to the arena spawn point on frame one, BEFORE the pad's chunks had streamed in and
before `FlattenAt` had carved â€” the player fell into unloaded terrain; and the bench coroutine ran every
lane back-to-back, so a single failing lane aborted the whole coroutine (see `_spawned` gate) and could
leave both the lanes AND the final player teleport un-executed.

- **`GameBootstrap.cs`**: the player is now physically placed on the synchronously-generated spawn chunk
  (tile `(0,-10)`) right after `GenerateChunkSync`, sampled with the streamer's own seed
  (`TerrainNoiseGenerator.GetHeight(seed, 0.5, -9.5) + 2f`) â€” never an unloaded void at frame one. Boot
  comment rewritten: "ground first, then player".
- **`PlayerController.ResetPlayer`**: only teleports to `NewWorldTestGround.GetSpawnPoint()` once
  `IsArenaReady` (pad carved); otherwise it falls back to the boot chunk `(0, terrainY+3, -10)` using the
  live streamer seed (was a hardcoded `1337`). Covers death respawns during the first few seconds too.
- **`NewWorldTestGround.cs`**:
  - New `IsArenaReady` (arena flattened AND `PlatformTopY` valid) â€” the single gate other systems query.
  - `RunBenchSpawn` reordered: wait chunks â†’ carve arena â†’ **teleport the player onto the pad FIRST** â†’
    then lay the lanes with one `yield return null` per group.
  - Every lane (farm/livestock/enemies/buildings/NPCs/POI/weapons/skills/gear/races/player-grants) now
    runs through `RunSafely` (try/catch + `Debug.LogError`), so a failing lane logs and the coroutine â€”
    and the player-placement fallback at the end â€” always completes.
  - `GetSpawnPoint` uses `PlatformTopY` when prepared (falls back to the configured `PlatformCenter.y`,
    never the raw default 50 in the void once the arena exists); `PlacePlayerOnArena` helper centralizes
    the teleport.
  - Bug fix: `PrepareArenaGround` only sampled the SOUTH-WEST quadrant of the pad (loop stepped from one
    edge by 1 m over the half-size), so the flatten target could fall lower than a taller far corner â€”
    now it strides the FULL `Â±half` footprint every 2 m (still a 61x61 grid, ~3721 noise reads).
- **`game-design.md`**: Â§2.7 documents the "ground first, then player" boot order and lane isolation.

### 1ba-status
- No CLI build â€” verified by code review: the safe spawn chunk (tile `(0,-10)` â†’ chunk `(0,-1)`) is the
  same one `GenerateChunkSync` builds, and `FromWorld(0, ~y, -10)` resolves to it; `ResetPlayer` no longer
  has a path to the un-carved arena; all `RunSafely` call sites pass method groups/fiddles matching
  `System.Action`; coroutine structure mirrors the old lane-budgeting (yields intact).
- Play-test after review: (1) boot / new game â€” the player stands on solid grass at spawn within 1-2s,
  never drops into the void, then gets pulled to the centre of the flat arena; (2) the flat pad + weapon
  racks / farms / enemies / NPCs all appear (check Console for any `Lane ... failed` error and report it â€”
  that's the exact place an old silent abort would have hidden); (3) death-respawn quickly during boot â€”
  no void (falls back to the boot chunk until the arena is ready); (4) the pad is visibly level all the
  way to its far edges (the SW-quadrant sampling fix).

---

Earth spells already persisted their ground edits forever (per-chunk save files, Â§2.6), but the roster
only RAISED terrain with two shapes (Ring / Spikes) applied by Zone spells; projectile spells never
touched the ground, and there was no wall, pillar, or excavation shape. The Earth school now does what
the design promised: castable terrain changes that last forever, at both ends of the fight.

- **`SpellData.cs`** â€” `TerrainShape` gains **`Wall`** (a stone ridge rears up along the cast axis),
  **`Pillar`** (a tall flat-topped column at the impact center), and **`Crater`** (excavates a shallow
  solid-floored dish); the terrain tooltip documents all five shapes.
- **`WorldStreamer.DeformAt(center, radius, shape, dir = default)`** â€” new `dir` projects the cast
  direction onto the XZ plane for directional shapes. Per-corner influence: **Wall** = raised band
  around the spine with rounded length caps (lift 1.3); **Pillar** = flat-top core inside `0.45Â·radius`
  with smootherstep falloff (lift 1.8, the tallest shape); **Crater** = LOWERS terrain (`current âˆ’
  smootherstepÂ·1.8m`) clamped so the floor never goes below `originalNoiseHeight âˆ’ 1.8m`. The clamp is
  the "avoid the void" guarantee â€” every pit keeps a solid, walkable bottom and spamming the cast can't
  grind it deeper than the first carve. All shapes still route through the shared `ApplyHeightEdits`
  (corner stamping, dirty-mark, chunk mesh/collider rebuild, per-chunk flush â†’ permanent).
- **`TerrainDeformer.Apply`** â€” accepts the directional `dir` and passes it through.
- **`SpellCaster.cs`** â€” `ResolveZone` passes the cast `fwd` so Walls orient along the aim;
  `FireProjectile` now carves a Crater-shaped throw-site pit (ground just ahead of the caster,
  `max(1.2, spell.Radius) Â· sizeScale`) BEFORE the projectile spawns, so an Earth projectile literally
  tears its slab loose from the ground; impact only damages.
- **`SkillCatalog.Magic.cs` / `SkillCatalog.cs`** â€” retagged the existing Earth spells that already
  read as these names: **Stone Pillars** (`magic_earth_spires_pillar`) â†’ `Pillar`, **Landslide**
  (`magic_earth_boulder_landslide`, "a wall of rock") â†’ `Wall`, and the Earth ROOT skill
  **Stone Shard** (`magic_earth_spell`, the school's projectile) â†’ `Crater`. No tree count changes.
- **`game-design.md`** â€” Â§3.7 Earth line and the Â§3.8 terrain-shape bullet now list all five shapes
  and the projectile crater carve + depth-clamp rule.

### 1az-status
- No CLI build â€” verified by code review: shape switch is exhaustive (Ring/Spikes/Wall/Pillar raise,
  Crater lowers with the `max(current âˆ’ sÂ·1.8, noise âˆ’ 1.8)` clamp â€” second carve at the same spot
  stays depth-flat); Wall/wallDir is XZ-normalized with a right-axis fallback; `TerrainDeformer.Apply`
  and `SpellCaster` call sites carry `dir`; the retagged spell factories both accept `terrainShape:`.
- Play-test after review: (1) cast **Stone Shard** repeatedly â€” a shallow crater appears ahead of the
  caster on the first cast, later casts don't deepen it, and the shard still flies/damages;
  (2) **Stone Pillars** â€” tall flat-topped columns thrust up at the aim point; (3) **Landslide** â€” a
  wall ridge rears up along the aim direction; (4) rings/spikes unchanged (Tremor, Spire Field);
  (5) reload the game / walk away and back â€” every edit is still there (persisted chunk files).

Follow-up fix: `ShieldBashEffect` logged against `SkillProfile.SkillDebug`, which is `private` in
`SkillProfile.cs` â€” CS0122. `IEffect.cs` now owns the same `private const bool SkillDebug = true;`
convention the other skill files use.

---

## 1ay. Test ground now uses the world's own terrain generation (flat procedural arena, no floating platform)

Play-test/dev feedback: the test bench sat on hand-built GameObjects (a Quad floor + thin cube collider +
corner poles) floating above the generated world, visually and physically divorced from the terrain.
The ground is now the **real procedural chunk terrain**: the test ground samples the world's own height
function (`TerrainNoiseGenerator.GetHeight`, the same 5-octave noise the chunk generator uses) over the
footprint, waits for the streamer to load every chunk under the pad, then levels it in place with a new
`WorldStreamer.FlattenAt` â€” the exact tile-edit / chunk-rebuild / per-chunk persistence pipeline the Earth
spells already use (Â§3.8) â€” so the flat arena is genuine generated terrain (mesh, collider, save files),
leveled UP to the footprint's maximum so nothing pokes through the bench.

- **`WorldStreamer.cs`**: new public `FlattenAt(Vector3 center, float halfSize, float targetHeight, float feather = 3f)`
  â€” levels a rectangular patch of the loaded heightmap with a smootherstep-feathered rim (pad interior
  fully flat; rim blends 1â†’0 into untouched terrain over `feather` units). The apply/rebuild/flush tail
  of `DeformAt` is extracted into shared `ApplyHeightEdits(...)`; both spell shapes and the flatten route
  through it, so the pad shares the gapless-corner / dirty-mark / flush-per-chunk guarantees.
- **`NewWorldTestGround.cs`**: floating-platform build (`BuildPlatform` Quad/Cube/poles) deleted;
  `SnapPlatformToTerrain` â†’ `PrepareArenaGround` (samples footprint max/min height with the world noise,
  sets `PlatformCenter.y = PlatformTopY = maxY`, XZ bounds = footprint, computes the rim feather from the
  pad's height span). `SpawnBenchBudgeted`/`SpawnBench` are folded into a `RunBenchSpawn` coroutine that
  first waits for the arena's chunk ring (`WaitForArenaTerrain` + `ArenaChunkCoords`), then flattens +
  clears any streamed-in props before laying lanes. Prop suppression in `ChunkObject.StepProps`, the
  weapon-rack placement and every lane's `PlatformCenter.y + offset` math still work unchanged â€” they key
  off the same static bounds/`PlatformTopY`, which now point at the real flattened ground.
- **`GameBootstrap.cs`**: boot comment updated â€” the player spawns directly onto procedural terrain;
  the arena carves in place once its chunks stream in (~1s), then the player is placed mid-pad.
- **`game-design.md`**: new Â§2.7 "Testing Arena â€” Real Procedural Terrain" documenting the flatten approach.

### 1ay-status
- No CLI build â€” verified by code review: `FlattenAt` box-influence + smootherstep feather math reuses the
  extracted `ApplyHeightEdits` apply path; every tile inside the flatten rect gets its 4 corners stamped
  before `RebuildChunkRegion` rebuilds the touched chunk rects (same shared-corner contract as `DeformAt`);
  the wait-for-loaded gate means no pad chunk can pop in un-flattened later; lanes/racks use
  `PlatformCenter.y`/`PlatformTopY`, which are now the flattened ground height.
- Play-test after review: (1) boot â€” the player drops onto real terrain for ~1s, then the pad settles flat
  and the player is placed mid-arena; (2) the flat pad uses the world's grass material and ramps into the
  surrounding hills at the rim (no floating quad, no corner poles); (3) no trees/rocks anywhere on the pad;
  (4) farm a plot and cast an Earth spell on the pad â€” both work on the real terrain; (5) walk off the rim,
  come back heavy-budgeted (or reload) â€” the pad is unchanged (persisted via chunk files).

---
## 1ax. Staff now grips at the sword's angle (same drawn hold pose)

Play-test feedback on the held magic staff: it hung dead-vertical off the hand while a sword in the
same hand reads side-on with a slight cant. The staff (Magic category, id `staff`) now uses the
**exact same drawn-hold rotation as the sword** â€” `Quaternion.Euler(WeaponHoldForwardLean,
DrawHoldYaw, DrawHoldCant)` (90Â° yaw so the length reads side-on to the camera + 30Â° roll cant) â€”
instead of `Euler(0, 0, 0)`. Other magic focuses (book / wand / orb / lute) keep their own natural
upright hold.

- **`WeaponRigBuilder.cs`**: new `DrawHoldCant = 30f` constant (the melee blade's off-vertical roll,
  previously an inline `30f` in the melee draw pose) so sword and staff share one angle source of
  truth. The melee branch now references `DrawHoldCant`; the staff branch returns the sword's
  rotation for its drawn pose (still at its short magic grip-height position, `y = -0.35` â€” only the
  *angle* changes).
- **`game-design.md`**: Â§3.6 Visuals gains a "drawn hold pose" note â€” the staff grips like the sword
  (same yaw + cant), other magic focuses stay upright.

### 1ax-status
- No CLI build â€” verified by code review: both the staff and melee branches now emit the identical
  `Quaternion.Euler(WeaponHoldForwardLean, DrawHoldYaw, DrawHoldCant)`, `DrawHoldCant` is referenced
  by both (no stale inline `30f`), and the staff keeps its `y = -0.35` grip near the fist.
- Play-test after review: equip the Mage's Staff and draw it â€” the length should lean/cant exactly
  like the sword's blade in the same hand, not hang vertical; book/wand/orb should still hold
  upright (unchanged branch).

---
## 1aw. Shield skill category â€” the shield tree is its own 7th skill category (bash/guard/counter)

The shield tree (root `shield_bash`, 5 branches, 25 children) was split out of Melee so the shield
reads as its own identity. Every Shield skill plays the *equipped* shield's own bash â€” the
`ShieldWeaponBehavior` bash animation + face hitbox with knockback â€” powered by the skill's damage
instead of the weapon's raw bash, and Shield skills can't fire (or spend cost/cooldown) unless a
shield is actually in hand.

- **`SkillType.cs`**: new `SkillType.Shield = 6` â€” the 7th skill-XP category (after Fortitude).
- **`SkillXpTracker.cs`**: `CategoryCount` 6 â†’ 7; `TierRewardNames` gains "Shield bash damage +5%"
  (tier index 6). Save/restore compares against the same constant, so old 6-category saves load clean.
- **`SkillCatalog.Shield.cs`** (new): `RegisterShieldDesign` â€” root `shield_bash` (active, Stamina
  14, `Bash(22f, Physical)`), L1 `shield_slam` / `shield_spikewall` / `shield_flash` /
  `shield_riposte` / `shield_earthwarden`, each with 5 L2 children (aftershock/flame/frost/thunder/
  earth, bristle/blazing/frost/stone/gale, radiant/blessed/hymn/dawn/purify,
  rebound/retribution/vengeance/reflect/guardian, tremor/lava/frozen/boulder/ore) â€” all `Bash(...)`
  castables.
- **`SkillCatalog.Melee.cs`**: the old `melee_shieldbash` layer-1 root + its 5 L2 branch tables are
  removed (the L2 comment drops to 20 L1 parents / 100 slots).
- **`SkillCatalog.cs`**: `BuildShield` + `RegisterShieldDesign` wired into the build/expand path;
  new `Bash(power, kind, knockback)` helper; `shield_bash` root added under `SkillType.Shield`.
- **`IEffect.cs`**: new `ShieldBashEffect` â€” fetches the equipped shield
  (`CombatController.EquippedShield`), plays its `WeaponAnimator` bash track, routes
  `ShieldWeaponBehavior.BeginAttack` (charge level, direction/origin from the skill context) and
  overrides the power path (`AttackDamage`, `Hitbox.AttackPower/Type/KnockbackForce` = skill values),
  plus a shield-face `SkillFx.SlashFlash` accent. Without a shield it no-ops with a debug log.
- **`SkillProfile.cs`**: `TryUse` gates `SkillType.Shield` skills on `combat.HasShield` *before* any
  cost/cooldown is spent (`[Skill] "id" needs a shield equipped`).
- **`CharacterInfoUI.cs`**: the center PHYSICAL wheel now has **5 wedges** (Melee / Ranged /
  Stealth / Fortitude / Shield); `CategoryNames` + legend + a Shield accent color.
- **`game-design.md`** synced: Â§3.3 counts 6 â†’ 7 categories (new Shield row 1/5/25 = 31; TOTAL
  67/335/1675/2077), a "Shield category (current build)" block, the XP-categories table gains the
  Shield row, and Â§3.6 reroutes `ShieldWeaponBehavior`'s bash identity to the Â§3.3 Shield category.

### 1aw-status
- No CLI build â€” verified by code review: `SkillType.Shield` / `CategoryCount` / `CategoryNames` all
  at index 6 with their new 7th entries; `ShieldBashEffect` references existing `EquippedShield` /
  `ShieldWeaponBehavior` / `WeaponAnimator.PlayAttack(false)` / `SkillFx.SlashFlash` APIs; the
  `HasShield` gate sits after the learned check and before any cost spend; grep confirms zero
  remaining `melee_shield*` / melee-shield branch references in code (only stale PROGRESS history);
  tree counts recomputed 1 + 5 + 25 = 31.
- Play-test after review: (1) with a shield equipped, learn/cast a Shield skill (e.g. Shield Bash) â€”
  it should play the shield's bash animation, hit in front with knockback, and deal the skill's
  power; (2) without a shield, the hotkey/wheel cast should fail with no FP/stamina/cooldown spent;
  (3) the center PHYSICAL wheel/legend shows 5 wedges incl. Shield; (4) old savegames still load
  (category index 6 defaulting clean).

---
## 1av. Fix MagicTestMatrix's remaining compile errors (namespace, Keyboard, LINQ)

Follow-up on `1as`: three more CS errors in `MagicTestMatrix.cs`. The file was the only one in
`Assets/Scripts/UI/NewWorld` wrapped in `namespace UI.NewWorld` â€” every sibling (incl.
`MagicWheelUI`, which calls `MagicTestMatrix.Ensure()`) lives in the global namespace, so the matrix
was invisible from the wheel (`CS0103`). It also used `Keyboard` without
`using UnityEngine.InputSystem;` (`CS0103`) and `profile.Learned.Contains(...)` on an
`IEnumerable<string>` without `using System.Linq;` (`CS1061`).

- **`MagicTestMatrix.cs`**: dropped the `namespace UI.NewWorld { }` wrapper (matrix now global like
  the rest of the folder); added `using UnityEngine.InputSystem;` for `Keyboard.current`;
  `profile.Learned.Contains(id)` â†’ `profile.HasLearned(id)` (exact-set method on `SkillProfile`, no
  LINQ needed). Still no behavior change.

### 1av-status
- No CLI build â€” verified by code review: namespace wrapper removed (grep shows zero remaining
  `UI.NewWorld` references), `Keyboard` import present, `HasLearned(string)` exists at
  `SkillProfile.cs:185`, `SkillCatalog.Find` / `HudCanvas.CreateOverlay` / `TestGrant` /
  `TopUpFocus` / `ExecuteCharged` all resolved in the global namespace. Pending play-test: Alt opens
  the matrix and a row cast works.
- No `game-design.md` change: matrix behavior unchanged.

---
## 1as. Fix MagicTestMatrix compile error (CS1106 + corrupted text)

`MagicTestMatrix.cs` did not compile: CS1106 "Extension method must be defined in a non-generic static
class" because `AsRect(this Transform t)` was an extension method inside a non-static MonoBehaviour;
the file also carried two corrupted snippets (`returnauthors;` on the mobile early-out and stray
`åœ¨å…¨` characters on `_built = true;`, plus a `Executecast` mis-capitalization).

- **`MagicTestMatrix.cs`**: `AsRect` is now a plain static helper (called as `AsRect(viewport.transform)`
  instead of `.AsRect()`); `returnauthors;` â†’ `return;`; `_built = trueåœ¨å…¨;` â†’ `_built = true;`;
  `Executecast` â†’ `ExecuteCast`. No behavior change.

### 1as-status
- No CLI build â€” verified by code review: no extension methods remain in the non-static class and the
  corrupted tokens are gone. Pending user play-test (Alt â†’ magic matrix opens/scrolls, cast a spell).
- No `game-design.md` change: matrix behavior unchanged (design doc has no matrix reference).

---
## 1ar. Redo projectiles as named shapes â€” every spell's projectile looks like its name

Follow-up on `1aq` review: user said projectiles should stop being same-colored balls â€” "bolt is the
shape of lightning, which should have generated like lightning from the thunder event". So projectile
visuals were split into **named `ProjectileShape`s** and each projectile spell's shape now matches its
name; a "Bolt"-named spell is built with the same jagged-segment technique as the thunder-storm
event's `RandomEventManager.SpawnJaggedBolt`, colored per element instead of being a sphere recolor.

- **`SpellData.cs`**: new `ProjectileShape` enum (`Auto, Bolt, Sphere, Shard, Lance, Spear, Blade,
  Splash, Comet, Missile, Dart`) + `Shape` field under a new `[Header("Presentation")]`.
- **`SpellCaster.cs`**: `DecorateProjectile(go, type, shape)`; `FireProjectile` passes `spell.Shape`;
  `AttachDefaultProjectileVisual` resolves `Auto` via `AutoShapeFor(DamageType)` (Fireâ†’Sphere,
  Iceâ†’Shard, Lightningâ†’Bolt, Windâ†’Blade, Waterâ†’Splash, Earthâ†’Shard, Physicalâ†’Dart, elseâ†’Sphere) and
  builds the body in `BuildProjectileBody`: `Bolt` (8 segment jittered cubes along +Z with taper â€”
  same look as `SpawnJaggedBolt`), `Shard` (drilling diamond), `Lance` (shaft+tip spike), `Spear`
  (dark shaft + diamond head), `Blade` (cross-blade spinning in-plane, `OrbFx.Swirl`), `Splash`
  (droplet + trailing drops), `Comet` (core + streak tail), `Missile` (clumped darts), `Dart`
  (sleek tip+body); removed the old per-type `Spark`/`Swirl` sphere builders. Added `Primitive`/
  `Materialize` helpers. `OrbFx.Mode` gained `Bolt`/`Swirl` pulse behaviors alongside Plain/Ember/
  Shard/Wisp.
- **Catalogs**: `SkillCatalog.Spell(...)` + class/race `MakeSpell(...)` gained a
  `projectileShape:`/`shape:` param. Assigned by name â€” every `*Bolt` spell (incl. Frost Bolt, Chain
  Lightning, Dark Bolt, Volt/Fork/Leap/Arc Spark/Volt Bolt, Fury Bolt, Shadow/Doom Bolt, Void Rend,
  Arcane/Force/Prism Bolt) â†’ `Bolt`; `Ice Lance`/`Frost Pierce`/`Glacial Impale` â†’ `Lance`; `Shadow
  Spear` â†’ `Spear`; `Wind Blade`/`Razor Blade`/`Wind Scissor`/`Laceration` â†’ `Blade`; `Scorch`/`Burn`/
  `Comet` â†’ `Comet`; `Arcane Missiles` â†’ `Missile`; `Tidal Surge`/`Water Bolt` â†’ `Splash`;
  `Stone Shard` â†’ `Shard`; Fireball/Chill Touch/Chill Soul/Frost Bite stay `Auto` (â†’ Sphere/Shard);
  class spells Mage Fireball (Autoâ†’Sphere), Mage Arcane Boltâ†’Bolt, Archer Wind Shotâ†’Dart, Taoist
  Talismanâ†’Dart. `SpellSummon` turret bolts pass `_spell.Shape` too.
- **`magic-skills.md`** regenerated: new **Projectile Shapes** table (each shape, how it's built,
  which spells use it) + every projectile effect line ends with `shape:<effective>`.
- **`game-design.md`** updated: `projectile shape` bullet in the SpellData list, `ProjectileShape`
  doc comment in Â§3.8.1, and a shape table describing each shape + the Auto resolution rule.

### 1ar-status
- No CLI build â€” verified by code review: 31 `projectileShape:` assignments across both catalogs,
  `BuildProjectileBody` switch covers all 10 shaped cases with a default, no stale `Spark`/`Swirl`
  builder references, `OrbFx.Mode` members all defined, generator rerun idempotent (shape counts:
  BoltÃ—16, ShardÃ—4, LanceÃ—3, BladeÃ—4, CometÃ—3, MissileÃ—1, SplashÃ—1, SpearÃ—1, SphereÃ—1 = 34 total).
- Play-test after review: cast each school's projectile (Fireball sphere, Frost Bolt jagged bolt,
  Chain Lightning jagged bolt, Wind Blade spinning cross, Ice Lance spike, Shadow Spear, Tidal
  Surge splash, Arcane Missiles cluster, class spells) â€” confirm bolts read as lightning and 2Ã— lance/
  spear tiers read distinct.

---
## 1au. Projectile trajectory now launches from the center of the casting circle

Play-test feedback on `1at` (missile homing): "the trajectory is not at the center of the magic
circle". The magic circle halo `CastingCircle` anchors to the magic rig/hand (`MagicHand`), and
`CastOrigin` is the same rig object, so the origin point already matched â€” the break was a hardcoded
**vertical lift**: `SpellCaster.FireProjectile` spawned projectiles at `pos + fwdÂ·0.5 + Vector3.upÂ·0.3`
and `PlayerController.UpdatePathPreview` drew the aim path from the same lifted point. That raised the
whole flight line ~0.3 above the halo's center plane, so the trajectory visually missed the circle's
heart.

- **`SpellCaster.FireProjectile`**: spawn is now just `pos + fwd * 0.5f` â€” on the aim line, no lift â€”
  so the trajectory passes through the casting circle's center. Self/terrain clearance stays safe via
  the `SpellEffect` caster-root skip + the small constant ground probe.
- **`PlayerController.UpdatePathPreview`**: the projectile path preview uses the same origin math
  (`pos + fwd * 0.5f`), keeping the pre-cast ray and the actual launch aligned (#1 preview-vs-fire
  drift fixed too).
- `game-design.md` "Charging & Casting Circle" notes the launch-from-center rule and the shared
  origin math.

### 1au-status
- No CLI build â€” verified by code review: rig == hand == CastOrigin == CastingCircle anchor confirmed
  in `WeaponRigBuilder` (weapon `go` becomes `combat.RightHand`, `magic.CastOrigin = go.transform`);
  both `FireProjectile` and `UpdatePathPreview` now use `pos + fwd*0.5f` (no `+Vector3.up*0.3f`).
- Play-test after review: aim a projectile spell (Arcane Missiles / Frost Bolt / Fireball) and confirm
  the path preview ray starts at the halo's center and the fired missile leaves from that same point.

---
## 1at. Missile homing follow-up: "raycast" meant the trajectory â€” prioritization is now per-frame along the flight path

User clarified `1as`: "when i said raycast i meant trajectory". The one-shot aim-line lock was the
wrong read â€” missiles should re-evaluate every frame along the path they are **bending on right
now**, and whatever foe sits on that trajectory is what they home to.

- **`SpellEffect.cs`**: `AcquireMissileTarget` (launch-time, aim-line raycast) replaced by
  `UpdateMissileTargeting` called every Update while the shape is Missile. Priority each frame:
  (1) `FirstEnemyOnTrajectory(Lookahead())` â€” a ray down the **current** `_dir` (the live
  trajectory; `Lookahead()` = spell Range or at least one second of flight), sorted by distance,
  first enemy root wins; (2) otherwise keep chasing the locked target's last known spot; (3) if
  never locked or the locked foe died, lock the nearest enemy in a 50Â° forward cone. Locked target
  re-acquires instantly whenever a foe drifts onto the trajectory. Docs reworded to "trajectory /
  flight path".

### 1at-status
- No CLI build â€” verified by code review: `FirstEnemyOnTrajectory`/`Lookahead`/`_locked` flag
  added, old launch-time lock removed, generator rerun ASCII-clean with the reworded Missile row.
- Play-test after review: cast Arcane Missiles past a row of foes â€” the missile should keep bending
  toward whichever one is on its current path and re-target if another steps into the trajectory
  mid-flight; killing the locked foe mid-flight should still finish toward the last spot.

---
## 1as. Add homing/pathfinding to the Missile projectile shape â€” it bends its way to the target

Follow-up on `1ar` review: user asked to "add pathfinder to missile, will bend its way to hit the
target, prioritize the target on the raycast". Projectile-delivery spells whose shape is
`ProjectileShape.Missile` (Arcane Missiles) now home instead of flying straight.

- **`SpellEffect.cs`**: `Launch` acquires a target when the spell's (resolved) shape is Missile â€”
  `AcquireMissileTarget` first raycasts along the aim line (`RaycastAll`, sorted by distance, skipping
  ground/caster-root/non-enemy) and locks the **first enemy on the raycast** ("prioritize the target
  on the raycast"); if none, falls back to the nearest enemy in a 50Â°-forward cone
  (`NearestEnemyInCone`, scored by distance + angle). `SteerTowardTarget` runs every update: bends
  `_dir` toward the (live or last-known) target spot at 240Â°/s via `Vector3.RotateTowards` and
  re-orients the body; the existing per-step raycast + ground-probe hitboxes then detonate on the
  chased target as usual. No target = flies straight. Target filter matches the turret's
  (`EnemyController`/`BossController` roots, excluding caster root / Player / Companion; ground
  colliders skipped).
- Docs updated: `magic-skills.md` regenerated (Missile shape row now says **homing** - locks the aim-
  raycast target and bends to chase), `game-design.md` Â§3.8.1 Missile row describes the lock priority
  + turn rate. `SpellData`/`SpellCaster` unchanged â€” homing is implicit to the Missile shape.

### 1as-status
- No CLI build â€” verified by code review: homing gated on `_spell.Shape == ProjectileShape.Missile`,
  `Array.Sort(RaycastHit[])` is valid (default distance comparer), `using System` already present,
  generator rerun ASCII-clean (fixed the em-dash mojibake in the ps1 shape row).
- Play-test after review: cast Arcane Missiles at a foe slightly off the aim line and at a foe on the
  line â€” missiles should bank toward both, prioritize the raycast target, and still detonate on
  intervening walls/ground; kill a mid-flight target and confirm the missile chases the last spot.

---
## 1aq. Add `magic-skills.md` â€” full generated list of all magic tree skills + SpellDelivery reference

User asked: "make a list of all the spell and their function, i'll tell you what need to be redo" â€”
scoped down to "only all the magic skills and put em in a file, also list me what deas delivery do".
Since the magic tree is huge (18 roots + ~400 L1/L2 branch nodes in `SkillCatalog.Magic.cs`, hand
transcription would be error-prone), a PowerShell generator script parses the skill files and emits
the listing into a new repo file `magic-skills.md`.

- `magic-skills.md` (repo root) contains: (1) **SpellDeliveries** reference â€” what each of the 7
  `SpellDelivery` values does (Instant / Projectile / Zone / Vortex / Beam / Summon / Storm), resolved
  from `SpellCaster.Execute`/`ResolveZone`; (2) **Base skills (roots)** table â€” all 18 base magic
  skills with passive/active, school, power/FP/cooldown/range/radius/status detail and description;
  (3) **Tree branches** â€” every L1 branch (and its L2 children, indented) parsed from the
  `bank.L1[...]` / `bank.L2[...]` branch tables, plus `A("id")` authoring refs to base skills.
- Generator lives at `C:\Users\antic\AppData\Local\Temp\opencode\gen-magic-list.ps1` (ASCII-safe;
  regenerable). No game code changed â€” `game-design.md` unaffected.
- Parsing verified by spot-checking generated rows against source lines (Fireball 25 power âœ“, Holy
  Light zone/heal âœ“, Storm Breath beam with drain 8 âœ“, Tremor `terrain:Ring` âœ“) and by counts (5
  authoring refs across the tree âœ“).

### 1aq-status
- No CLI build â€” generator output verified by code review (read of `magic-skills.md`, spot-checked
  against `SkillCatalog.cs`/`SkillCatalog.Magic.cs` source lines).
- Play-test after review: user reviews `magic-skills.md` and tells us which magic skills / deliveries
  to redo.

---
## 1ap. Fix: talents list overlays the General skill tree on first open (TalentsView never hidden)

Bug: the player reported "the talent appears in the general skill tree". Root cause:
`BuildTalentsView` (CharacterInfoUI.cs) creates the `TalentsView` GameObject **active by default**,
and it was only ever toggled inside `SetSkillSubTab` (i.e. on a sub-tab button click). Since the
initial Skills tab defaults to `SkillSubTab.General` and `ShowTree`/`HideOtherTreeRoots` only hide
the three tree roots (General/Class/Race â€” never the talents view), the freshly-built TalentsView
rendered **on top of** the General tree (it is a later sibling, created last). The catalog itself
contains no "talent" skill, so no wheel node was the culprit.

- **Fix** (`UI/NewWorld/CharacterInfoUI.cs`): deactivate `TalentsView` at the end of
  `BuildTalentsView`. The Talents sub-tab still shows it via `SetSkillSubTab(tab == Talents)`; the
  General/Class/Race tabs render without the talent rows overlaying the wheels.
- No gameplay/design change â€” pure visibility default fix, `game-design.md` unaffected.

### 1ap-status
- No CLI build â€” code-review verified (edit is a single `SetActive(false)` + no new braces; grep
  confirms `_talentsView.SetActive` is still only in `SetSkillSubTab`).
- Play-test: (1) open Character Info â†’ Skills with General selected: only the three wheels + legend
  (no `TalentPoints` / talent rows); (2) click Talents tab: rows appear; click back to General: rows
  disappear; (3) close character info while on Talents and reopen: panel still shows the correct tab.

---
## 1ao. Fix: map not generating â€” background chunk gen hit main-thread-only Application.persistentDataPath

Regression introduced by Phase 4 (`1ak`): `BuildOrLoadChunk` now calls `ChunkSaveManager.TryLoadChunk`
on the **background** generation threads, and `TryLoadChunk` â†’ `ChunkFilePath` â†’ `BaseDir` â†’
`Application.persistentDataPath` â€” which is **main-thread-only**. Every background chunk job threw
`get_persistentDataPath can only be called from the main thread` (caught, logged as a warning), so the
async pump produced nothing: only the synchronous main-thread boot chunk (`TerrainChunk_0_-1`) ever
spawned and the world was a void (no red errors, hence "no errors at all").

- **Diagnosed** with temporary `WorldStreamer` pump + background-job logging (commit `d970076`): the
  pump, dispatch, and finalize were healthy; the console showed the background warning with the
  `persistentDataPath` exception stack â†’ root cause locked.
- **Fix** (`World/Chunks/ChunkSaveManager.cs`): `BaseDir` is now computed from
  `Application.persistentDataPath` **once** into a cached static string; new `Warmup()` populates it.
  `WorldStreamer.Awake()` calls `ChunkSaveManager.Warmup()` on the main thread before the first
  background dispatch, so worker threads only ever read the cached path. Debug logs removed.

### 1ao-status
- No CLI build â€” code-review verified (both touched files brace-balanced; grep confirms the **only**
  remaining `Application.` access in the world path is the cached warmup; `BackgroundGenerateChunk`'s
  try/catch keeps its warning for genuine failures). The prior debug commit `d970076` was superseded by
  this fix commit (debug lines removed in this entry's files).
- Play-test items: (1) boot â†’ surrounding terrain should stream in around the player within ~1.5s
  (not just the boot chunk); (2) walk away and back â†’ chunks persist/unload/reload, deformed areas
  still restore from `tc_{x}_{z}.dat`; (3) no `[WorldStreamer] Background chunk generation failed`
  warnings in the console.

---

Follow-up housekeeping requested by the user: delete planning documents that have no remaining use now
that the work they planned is shipped, and refresh `game-design.md` so it stays the single durable
design reference.

- **Deleted** (completed/superseded working plans, per user request):
  - `OPTIMIZATION.md` â€” the 6-phase audit/tracking sheet. Phases 0-5 shipped (`1ag`-`1al`); the last
    open item, **startup Phase 6 (#17/#18)**, was first folded into `PROGRESS.md` `# OPEN TASKS`
    (above) so no actionable detail is lost.
  - `PLAN.md` â€” the UI/polish/one-giant-skill-tree batch plan; all batches shipped/pushed.
  - `PLAN-class-skill-trees.md` â€” class/race radial trees plan; shipped (Â§3.2.1 in game-design).
  - `planning.md` â€” the original 10-Phase implementation roadmap; superseded by PROGRESS.md.
- **`game-design.md` updated** to match implemented behavior:
  - **Â§2.6 Chunk Persistence** â€” rewritten for the terrain-chunk save format: per-terrain-chunk files
    `worlds/{seed}/tc_{x}_{z}.dat` (`"NWTC"` magic) storing only **locally deformed tiles**
    (`ChunkTileMod`: local coords + 4 heights), loaded via `TryLoadChunk` and re-filling deformed
    corners before noise-filling pristine ones; dirty tiles flush batched per chunk (default
    synchronous one-write-per-cast).
  - **Â§9.1 Engine** â€” Unity 2022 LTS â†’ **Unity 6 (6000.x)**, URP; notes `GetInstanceID`â†’`GetEntityId`.
  - **Â§9.3 Save System** â€” chunk files: `tc_{x}_{z}.dat` per terrain chunk, deformed-tiles only.

### 1an-status
- No CLI build â€” docs only. Verified: no remaining references to the deleted docs in `AGENTS.md` /
  `PROGRESS.md` / `game-design.md` (grep for `OPTIMIZATION.md`, `PLAN.md`, `planning.md` returns no
  roots outside stale PROGRESS history entries).
- Play-test: none needed (no gameplay code touched).

---
## 1am. Unity 6 compile fix â€” GetInstanceID â†’ GetEntityId + OverlapBoxNonAlloc arg order

Follow-up to Phase 0 (the project opened in Unity 6, 2026): `Object.GetInstanceID()` is now
`[Obsolete]` as an error (CS0619) and `Physics.OverlapBoxNonAlloc` changed its argument order, so a
build reported 5 errors across 3 files. All fixed in code review; no Unity build run.

- `Opt/ObjectPooler.cs` â€” pool key type `Dictionary<int, Queue<GameObject>>` â†’ `Dictionary<EntityId,
  Queue<GameObject>>`; `Warm`/`Get`/`ReleaseNow` now use `prefab.GetEntityId()` / `go.GetEntityId()`.
- `Combat/Weapons/HitboxSystem.cs` â€” `_hitThisSwing` is now `HashSet<EntityId>` keyed by
  `col.gameObject.GetEntityId()`; **`OverlapBoxNonAlloc` arg order fixed**: Unity 6's signature puts
  `Collider[] results` **before** `Quaternion orientation` (`position, halfExtents, _detectBuffer,
  rotation, HitLayers, TriggerInteraction`). The sphere overload was already correct.
- `Combat/AI/EnemyController.cs` â€” scan phase offset now derives from
  `GetEntityId().GetHashCode() & 0xFF` (the old `GetInstanceID() & 0xFF` bit-op doesn't exist on
  `EntityId`).

### 1am-status
- No CLI build â€” code-review verified (all three files brace-balanced; grep confirms zero
  `GetInstanceID` left in Assets; no project-local `EntityId` type that would shadow the engine
  struct; other `OverlapCapsuleNonAlloc`/`OverlapSphereNonAlloc` calls in Combat keep the unchanged
  non-rotated signatures).
- Play-test: enemy scan staggering, hitbox multi-hit detection, and object pooling continue to
  behave as before (pool reuse only observable as less GC).

## 1al. Optimization Phase 5 â€” skill-tree UI cache/pool (#15)

Audit: the skill tree destroyed+recreated ~3,000 GameObjects on every rebuild. That happened on the
Skills panel's first open **and** on every General/Class/Race sub-tab switch â€” `SetSkillSubTab` â†’
`RebuildSkillTree`/`RebuildClassSkillTree`/`RebuildRaceSkillTree` each destroyed **every** child of
`_treeContent` (`Destroy(_treeContent.GetChild(i))`) then re-instantiated the whole current-tree from
scratch. `FitTreeToViewport` also mixed the three trees' bounding boxes into one fit. Now each tree is
built **once** into its own container root under `_treeContent` and shown/hidden on switch; switching
back to an already-built tree is a repaint only (no GO churn).

**Structure (`UI/NewWorld/CharacterInfoUI.cs`):**
- New per-tree roots `_generalTreeRoot` / `_classTreeRoot` / `_raceTreeRoot`, created lazily by
  `EnsureTreeRoot(...)` (a `RectTransform` container centered under `_treeContent`). New helpers
  `ShowTree(root)` (activate the tree, hide the other two via `HideOtherTreeRoots`, reset the shared
  content pan transform) and build-id stamps `_classTreeBuildId` / `_raceTreeBuildId`.
- Each builder now starts with an "already built" fast path: build once, then on re-entry just
  `ShowTree` â†’ `FitTreeToViewport()` â†’ cheap repaint (`RefreshSkillTree`/`RefreshClassSkillTree`/
  `RefreshRaceSkillTree`). A **class or race change still forces a real rebuild** (build-id mismatch),
  so the change dialogs keep working.
- The old destroy loops that wiped ALL `_treeContent` children (killing sibling trees) now target only
  the current tree's own root. `MakeTreeNode` / `MakeTreeLine` / `MakeGeneralHeading` /
  `MakeClassTreeNode` / `MakeRaceSkillTreeNode` parent through the transient field `_treeBuildRoot`
  (set at the top of each builder) instead of hard-coded `_treeContent`.
- `FitTreeToViewport` now fits only the **current** sub-tab's node list (`switch _skillSubTab`), so a
  hidden tree's nodes can no longer inflate the other tree's fit box.
- Class/race builders no longer clear the General bookkeeping lists (`_treeNodes`/`_treeLines`/
  `_treeSkills`) â€” those GOs persist now, so returning to the General tab repaints correctly.

### 1al-status
- No CLI build â€” code-review verified (single file `CharacterInfoUI.cs` touched; brace-balanced
  369/369; grep-confirmed no remaining `SetParent(_treeContent` / `Destroy(_treeContent.GetChild`
  outside `EnsureTreeRoot`; `_treeBuildRoot` assigned in all three builders before any
  node/line/heading creation. Public API unchanged.)
- Regressions to watch on play-test: (1) **Sub-tab switching** â€” General/Class/Race should switch
  instantly with identical layout (wheel positions, class radial, race radial unchanged from before);
  (2) **class/race change dialog** â€” pick a different class/race, return to that sub-tab, confirm the
  tree actually rebuilds (build-id mismatch triggers it); (3) **per-tree fit** â€” each tree frames to
  the viewport on entry with no clipping (mixed-bounds bug fix); (4) **learned-state repaint** â€” learn
  a skill, switch away and back, confirm node colors/labels still update (persisted node lists).

## 1ak. Optimization Phase 4 â€” world streaming / terrain persistence rewrite

Audit hot spots #10, #11, #12, #13, #14 all live in the world/terrain pipeline and hit one of
three pain points: **revisit latency** (every chunk regenerated from noise on revisit even though
loads of tiny deformation files existed), **write burst** (each Earth cast wrote 250+ individual
per-tile files synchronously, each with tmp+move overhead), and **frame hitches** (full 900-tile
mesh rebuild + collider cook + prop spawn every cast/finalize). All six terrain files are touched:

**#10 â€” ChunkSaveManager rewritten for terrain-chunk granularity:**
- `World/Chunks/ChunkSaveManager.cs` â€” new binary format (`"NWTC"`, one file per terrain
  chunk under `worlds/{seed}/tc_{x}_{z}.dat`) holding only locally-deformed tiles (the old
  per-tile files are orphaned and harmless). `TryLoadChunk` returns a `ChunkSaveData` of
  `ChunkTileMod` structs (local coords + 4 heights + version stamp) which BuildOrLoadChunk uses
  to reconstruct deformed corner heights before noise-filling pristine corners.

**#10, #11a â€” Background generation now reads-or-generates:**
- `World/Streaming/WorldStreamer.cs` â€” `BackgroundGenerateChunk` and `GenerateChunkSync` both
  call the new `BuildOrLoadChunk(tc, seed)`, which tries `ChunkSaveManager.TryLoadChunk` first.
  If the file exists, deformed tiles restore their saved heights (gapless within + across chunks
  because DeformAt always deforms/Modifies/saves all co-affected neighbor tiles together);
  pristine corners remain deterministic noise. This cuts revisit CPU from ~961 octave samples
  to one small file read per chunk (only mod tiles restored from disk).

**#11a â€” Batched dirty-tile flush (250 writes â†’ 1 file per cast):**
- `WorldStreamer.MarkDirty` now only adds to `_dirtyTiles` (no per-tile sync write);
  `FlushDirtyChunk(tc)` gathers all dirty tiles inside one `TerrainChunkCoord`, clones the
  heights, and writes a single `SaveChunk` atomically (when `ChunkSaveManager.SynchronousWrites`
  is true, which is the default â€” one sync write per chunk per cast is <1ms, eliminating the old
  250+ tiny writes). An Earth cast touching 2 chunks now writes 2 files (one per chunk) total,
  not 250. `UnloadChunk` and `OnDestroy` also flush (OnDestroy groups `_dirtyTiles` by
  `TerrainChunkCoord` and flushes each). The old per-tile `ChunkSaveManager.Save` code path is
  gone.

**#11b â€” Sub-region mesh patch (only touched quads rebuilt):**
- `WorldStreamer.RebuildChunkRegion(tc, obj, minCXâ€¦maxCZ)` builds `ChunkMeshData` for only the
  local-tile rectangle affected by the deformation (not all 900 tiles). `ChunkObject.PatchRegion`
  writes the rebuilt quads' vertices/UVs/normals into the cached `_merged` arrays and re-uploads
  only those channels, then re-cooks the collider once. Falls back to the full 900-tile
  `ApplyMerged` path when the region exceeds ~75% of the chunk.

**#12 â€” Time-budgeted finalize + incremental prop streaming + shared Random:**
- `FinalizeChunks` now measures wall-clock time (`Time.realtimeSinceStartup`) and breaks at ~6ms
  per poll tick, in addition to the `ChunksPerFrame` cap.
- Props are NO LONGER spawned inside `CreateChunkGameObject`. `ChunkObject.BeginProps(seed)` now
  queues the 900 local tile indices and a single deterministic `System.Random` instance (one
  `new Random` per chunk instead of 900). `WorldStreamer.StepChunkProps` drives
  `ChunkObject.StepProps(budget)` from a global budget of 40 tiles per poll tick, spreading
  prop spawning across the next few ticks so an 8-chunk fill never spikes a single frame.
  Determinism is preserved (same seed/chunk â†’ same Random stream â†’ same prop layout), though
  placement differs from the old per-tile Random approach (noted below).

**#13 â€” Shared cube mesh + shared materials for all terrain props:**
- `Models/MapBuilder.cs` â€” new `SharedCubeMesh()` peels a unit cube from a single CreatePrimitive
  and caches it; every `MakeBlock` call (rocks, houses, NPCs, cars â€” the whole game) now adds a
  `MeshFilter.sharedMesh = SharedCubeMesh()` + `BoxCollider` (no per-call hidden mesh allocation,
  no collider destroy; identical unit-cube shape).
- `Models/MapBuilder.Nature.cs` â€” new `MakeCubeShared(...)` helper uses the shared mesh + assigns
  material via `sharedMaterial` (not `r.material`). The three functions that spawned ~2-3 cubes
  per tree/rock (`GrowBranchSegment`, `SpawnLeaves`, `GrowLeafChain`) were leaking per-renderer
  Material copies (via `r.material = mat`); they now use `MakeCubeShared` with `r.sharedMaterial`.

**#14 â€” Single mesh upload (five setters â†’ one pass):**
- `World/Terrain/ChunkMeshGenerator.cs` â€” `CreateMeshFromMerged` now calls
  `SetVertices/SetTriangles/SetNormals/SetUVs` then `mesh.UploadMeshData(false)` (one upload
  instead of five implicit per-property uploads on the old direct-setter path).

### 1ak-status
- No CLI build â€” code-review verified (all six touched files brace-balanced; no stale references to
  `SpawnProps`, `ChunkSaveManager.Save`, old `_dirty` field, or `Physics.OverlapSphere(` in Combat).
  `ChunkLodManager` and `ChunkValidator` public APIs unchanged. `DeformAt` access to locals fixed
  by parameter pass.)
- Regressions to watch on play-test: (1) **First revisit of a deformed area** â€” run an Earth
  spell, walk away, then return; verify heights persist and corners match (no gaps at chunk edges).
  (2) **Cross-chunk deformation** â€” run a large Earth cast near a chunk seam; walk away and return;
  verify both sides persisted and seam is smooth. (3) **Tree/rock placement** â€” return to a
  previously-generated area; trees should be deterministic (same positions) even after the
  single-Random refactor. (4) **Chunk unload+reload** â€” trigger a chunk unload (move far away and
  come back); verify deformed heights load from file and tree/rock placement is recreated identically
  via the determinism stream. (5) **Booting** â€” startup should feel identical (spawn-chunk props now
  stream over 2-3 ticks after terrain appears; boots under 1s total anyway).

Audit hot spots #4, #5, #7 all live inside combat: every spell tick/impact/burst ran **allocating**
`Physics.Overlap*` (a fresh `Collider[]` per call, several per second per spell), the caster rebuilt
`new List<string>(_cooldowns.Keys)` every frame, and combat FX/DamageNumber did `new GameObject` +
`new Material` per popup/strike with (in one case) a **permanent bolt leak**. Now:
- **NonAlloc buffers everywhere** â€” all remaining allocating overlaps in `Assets/Scripts/Combat`
  are gone (verified by grep â€” every call is now a `*NonAlloc` into a recycled buffer):
  - `Combat/Weapons/SpellCaster.cs` â€” `ResolveBurst` uses a shared `_overlapBuffer[128]` (#4); the
    cooldown tick iterates a reused `_cooldownKeys` list instead of `new List<>(Keys)` each frame (#5).
  - `Combat/Weapons/SpellZone.cs`, `SpellTornado.cs`, `SpellStorm.cs` â€” per-tick `_tickBuffer[128]`
    / `_strikeBuffer[128]`; `SpellStorm.RandomStrikePoint` and `ResolveStrike` share `_strikeBuffer`.
  - `Combat/Weapons/SpellEffect.cs` â€” impact + zone bursts share `_splashBuffer[128]` (its ground
    probe was already NonAlloc).
  - `Combat/Weapons/SpellBeam.cs` â€” channel tick now `OverlapCapsuleNonAlloc` into `_tickBuffer[64]`.
  - `Combat/Weapons/SpellSummon.cs` â€” `NearestEnemy` reuses its `_hitBuffer` via NonAlloc.
  - `Combat/Weapons/HitboxSystem.cs` â€” sphere/box both `NonAlloc` into instance `_detectBuffer[64]`
    (was the same allocating call twice per swing).
  - `Combat/Weapons/WeaponSkillExecutor.cs` â€” weapon-skill strikes share static `_strikeBuffer[64]`.
  - `Combat/Skills/IEffect.cs` (DamageZoneEffect), `ClassEffect.cs` (Taunt / LifestealStrike /
    ClassStrike), `RaceEffect.cs` (RaceStrike / RaceLifesteal / RaceTaunt / RaceRoar) â€” each uses a
    static `_buf[64]` (single-threaded, safe) instead of allocating `Collider[]`.
- **Pragmatic pooling (#7)**:
  - `Combat/Effects/DamageNumber.cs` â€” every hit used to spawn a new GameObject + TextMesh +
    Material. Now a static pool (cap 256): `Acquire()` reuses a released entry, alpha/scale reset on
    re-show, overflow destroys. API unchanged (`DamageNumber.Spawn` static overloads).
  - `Combat/Weapons/SpellStorm.cs` â€” `StrikeFlash` (the bright per-strike burst sphere) is pooled
    (cap 32) with one shared material; the lightning "bolt" bars reuse `SkillFx.SharedSpriteMaterial`.
    **Leak fix:** the old `SpawnStrikeFx` created 2 cubes per lightning strike that were never
    destroyed â€” now `BoltFader` shrinks them to nothing over 0.25s and destroys them.
  - `Combat/Effects/SkillFx.cs` â€” new `SharedSpriteMaterial(Color)` cache (keyed by damage-palette
    color, so the pool stays tiny); `SpellZone`/`SpellStorm` visuals share these instead of
    `new Material` per cast. Faders that animate alpha still own per-instance materials.

### 1aj-status
- No CLI build â€” code-review verified (all 13 touched files brace-balanced; every allocating
  `Physics.Overlap*` call in `Assets/Scripts/Combat` eliminated â€” grep-confirmed; no API changes
  outside internal fields; `SpellCaster` still `using System.Collections.Generic`).
- Regressions to watch on play-test: (1) **storm bolts** now fade out â€” confirm lightning strikes
  no longer leave two permanent cubes behind; (2) **DamageNumber pool** â€” many rapid hits should cap
  at ~256 simultaneous popups, alpha fully resets on reuse (watch for half-transparent numbers after
  heavy AoE); (3) spell zone/disc colors should look identical (shared palette materials â€” confirm
  heals/buffs/damage tints are still distinct); (4) bursts that used to hit >128 colliders would now
  truncate (no in-game content that dense â€” verify a big storm + 6 enemies still hits everything).

The audit's UI row all ran **every frame** with `GetComponent(InChildren)` lookups, string
formatting and TMP repaints. Now cached/dirty-checked:
- **`UI/NewWorld/PlayerBarsHUD.cs`** (#2) â€” `SpellCaster`/`PlayerStats` refs cached once per
  player object (`EnsureRefs`, re-resolved when the player root swaps) instead of
  `GetComponentInChildren` every frame; the HP/FP/Stam text labels now repaint only when the
  rounded value changes (`UpdateLabel` stores last ints) and the charge-% text only when the
  percent ticks.
- **`UI/NewWorld/SkillBarHUD.cs`** (#3) â€” the `new List + AddRange + Sort` per frame became a
  reused sorted cache (`_entries`/`_prevEntries`); label texts are repainted only when the
  binding set actually changed; the (cheap) cooldown `fillAmount` update remains per-frame.
- **`UI/NewWorld/MagicWheelUI.cs`** (#16) â€” per-slot `GetComponent<Image>` in `Paint` is a cached
  `_slotImages` list; `SpellCaster` cached against the current player; the armed chip and ring
  hint texts repaint only on change.
- **`UI/NewWorld/CompassMinimapHUD.cs`** â€” `Camera.main` cached, and the heading label only
  repaints when the yaw crosses a cardinal boundary (`CardinalIndex`).
- **`UI/NewWorld/EnemyHealthBarHUD.cs`** â€” `Camera.main` + the bar's `RectTransform` cached;
  `SetActive`/`fillAmount` are dirty-checked so steady frames skip canvas rebounds. **Also fixed a
  latent pooling bug:** a released bar re-used by a *different* enemy kept the previous enemy's Max,
  skewing its health fraction â€” Max now resets whenever the bar attaches to a new target.
- **`Opt/NewWorldTestGround.cs`** â€” the per-frame `PlayerController` lookup is now cached per
  player object.

### 1ai-status
- No CLI build â€” code-review verified (all six files brace-balanced; labels still formatted per
  call site). Play-test: damage/spend/charge bars still tick at the same thresholds; compass
  flips Nâ†’NEâ†’Eâ€¦ correctly; the armed-magic chip appears/disappears with weapon swap; enemy bars
  track fills correctly â€” verify the re-pool fix by killing the boss (or 25 enemies) and confirming
  a reused bar shows the correct fraction on the next target. Skill-bar cooldown fills unchanged.

## 1ah. Optimization Phase 1 â€” enemy scan stagger, tornado pull + camera collision throttles

Per-frame physics hot spots from the OPTIMIZATION.md audit (#1, #6, #8):
- **`Combat/AI/EnemyController.cs`** â€” the per-enemy `OverlapSphereNonAlloc` target scan ran **every
  frame** (60 live enemies = 60 broad-phase queries/frame). Now staggered to **4 Hz** with a
  per-instance phase offset (`_nextScanTime = Time + (GetInstanceID() & 0xFF) * 0.001f` in Awake;
  `TickTargets` early-outs between scans). First scan is ~immediate; taunt lock still overrides
  scans. `ClosestTarget` uses `sqrMagnitude` instead of `Vector3.Distance`.
- **`World/TornadoBehavior.cs`** â€” the 30u `OverlapSphereNonAlloc` pull query ran every frame;
  now throttled to **~4 Hz** (caught objects keep orbiting via the per-frame `_pulled` velocity
  writes, so captures stay smooth).
- **`Player/Controller/ThirdPersonCamera.cs` + `Player/CameraModeSwitch.cs`** â€” the third-person
  terrain-collision `SphereCast` ran every LateUpdate; now re-run at **~10 Hz** with the cached
  clamp distance reused between casts (per-frame position math unchanged; result identical).
- `OpenWorldGrounding` (per-body raycast) skipped â€” already confirmed a dead, unreferenced script
  (see Â§3 parked cleanup).

### 1ah-status
- No CLI build â€” code-review verified (logic + braces checked; no API changes). Play-test in Unity:
  enemies still aggro/chase/attack normally (â‰¤0.25 s scan latency at worst); spawn a wave of 40+
  enemies and compare frame time (CPU drop expected); tornado still spins, tows, swirls; third-person
  camera still avoids walls while turning/zooming. No observable feel regression expected.

Legacy commits (`c9103d9`, `77a685d`) rewrote `HitboxSystem` + `ObjectPooler` value keys from
`long`/`GetInstanceID()` to a custom `EntityId`/`GameObject.GetEntityId()` **that were never
defined** anywhere in `Assets` â€” a latent compile blocker for the whole project. Reverted both
files to plain `int` + `GetInstanceID()`:
- `Combat/Weapons/HitboxSystem.cs:40,107` â€” `HashSet<EntityId>` â†’ `HashSet<int>`;
  `col.gameObject.GetEntityId()` â†’ `GetInstanceID()`.
- `Opt/ObjectPooler.cs:14-15,33,47,88` â€” pool dictionary + keys â†’ `int`/`GetInstanceID()`.
Verified: zero `EntityId`/`GetEntityId` references remain. No behavior change (`GetInstanceID` is a
stable, unique per-object id â€” correct identity for both swing dedup and pool keying).

### 1ag-status
- No CLI build â€” code-review verified (grep clean; semantic checker 0 diagnostics). Play-test in
  Unity: game compiles and boots; melee hits still dedupe per swing; nothing else observable changes.

## 1aa. Recent completed work (2026-09-13) â€” talent system, per-skill levels, Lightning school, 3-wheel skill tree, projectile path preview, free class/race switching
Commit `125a775` ("re-organize skill trÃª, add projectile path, add diferent skill type", 2026-09-13, TVQ01) â€”
the largest single commit in the history so far (58 files, +3691/âˆ’377). Some of the beam/summon/storm
delivery code from 1m (dated 09-12) landed in this same commit.

### Talent system â€” rankable player-level perks (new)
- `Assets/Scripts/Player/Stats/TalentCatalog.cs` (+ .meta) â€” in-code roster (no .assets), `TalentKind` =
  `PlayerXp` / `SkillTypeXp` / `Stat`; every talent `MaxRanks = 3`, effects **additive per rank**:
  - `t.fast_learner` "Fast Learner" â€” +5 % **character XP**/rank.
  - 6 skill-type talents (`t.melee` â€¦ `t.fortitude`; "Arcane Study", "Craftsmanship", â€¦) â€” +6 % XP/rank
    for that skill type (applies to both the per-skill level and the category bar).
  - 11 stat talents (`t.health` â€¦ `t.luck`; "Vitality", "Fleet", "Might", "Sage", â€¦) â€” +1 **flat stat
    point**/rank.
- `TalentTracker.cs` (+ .meta) â€” const `PointsPerLevel = 1`; subscribes to `LevelUpSystem.OnLevelUp`;
  `EnsureOn(root)` auto-adds `LevelUpSystem` first so boot paths work before the character panel exists.
  - **New game**: `GameManager` grants ONE **random talent at rank 1** (`GrantRandomFirstTalent`, idempotent
    via `FirstGranted`); further points = 1 per character level-up.
  - `CanSpend`/`TrySpend` (point â†’ +1 rank), `RankOf`, bonus readers `PlayerXpBonus` / `TypeXpBonus` /
    `StatBonus` â€” all **live additive reads over owned ranks**, so restore never double-applies.
- Integration: `LevelUpSystem.AddXp` adds the character-XP talent bonus (stacks with race all-XP);
  `SkillXpTracker` adds `TypeXpBonus` to the category bar; `SkillProfile.GainUse` adds it to per-skill XP;
  `PlayerStats.GetTotal` = `base Ã— (1 + race% + race-skill%) + talent stat points + temp buffs`.
- Persistence: `SaveData.talentStateJson` (`SaveManager.cs:76` write `Points/Owned/FirstGranted`,
  restore at `SaveManager.cs:200`); blank/malformed â†’ clean slate.
- UI: `CharacterInfoUI` â†’ **Skills panel gained a "Talents" sub-tab** (`SkillSubTab.Talents`): a
  "Talent Points: N" header plus one row per talent (name, `Rank x/3`, `EffectPerRank()` text) with a
  **Rank Up** button enabled when a point exists and rank < max.

### Per-skill levels ("different skill type") â€” every learned skill levels itself
- `SkillProfile.cs` â€” each learned skill now carries its own **level** (`SkillProgress`;
  `MaxSkillLevel = 100`). Every successful use grants **per-skill XP** (`XpPerUse = 12`; the category bar
  gets `CategoryXpPerUse = 10`) scaled by race `XpBonusAll` + talent `TypeXpBonus`; level-ups follow a
  **linear curve** (`BaseXpToNext = 20`, +15 per level). New `LevelOf(id)` / `TryGetProgress(...)`.
- UI (`CharacterInfoUI`): General-tree **node labels show "Lv N"** under the name once learned; the detail
  pane adds "Lv N Â· XP x/y" for learned **active** skills (passives show just the level).

### Magic: Lightning is now its own school (7th L1 root)
`SkillCatalog.Magic.cs` â€” L1 comment becomes "7 roots; 6 full + Lightning's own school". New **`magic_lightning`
root** (passive Int+3) with L1 branches **Volt** (Projectile/Stagger), **Stormcall** (Storm, 3.5 s),
**Deep Charge** (passive Int+3), **Sky Fury** (Beam 13 m, `channelDrainPerSecond 9`) plus four full L2
subtrees (`magic_lightning_volt/_storm/_charge/_fury` â€” 20 new nodes incl. Storm Rain, Sky Beam,
Devastation, Lightning Tempest). **`magic_chain` (Chain Lightning) moved** out of `magic_fireball` to the
Lightning root. Several branches retuned onto the 1m deliveries (ids unchanged):
- **Storm Breath** (was Zone) â†’ **Beam** 10 m, drain 8/s Â· **Gust Totem** (was "Air Burst" Zone) â†’ **Summon**
  wind totem, 6 s Â· **Healing Shrine** (was "Light's Embrace" Zone) â†’ **Summon** heal aura, 8 s Â·
  **Arcane Rune** (was "Aegis" Zone) â†’ **Summon** turret, 5 s Â· **Arc Storm** (was Zone) â†’ **Beam** 14 m,
  drain 10/s Â· **Thunderstorm** (was "Overload" Zone) â†’ **Storm** 3 s.
- `SkillCatalog.cs` `Spell()` gained optional `deliveryRange`; `SpellContext` gained the ranged fields.

### Skill tree reorganization â€” three wheels on one board (CharacterInfoUI)
- The **General sub-tab now composes 3 independent tree wheels**: **Magic** (compact full-circle wheel,
  left, origin âˆ’2200,0), **Physical combat** (standard 4-wedge wheel â€” Melee/Ranged/Stealth/Fortitude,
  centre, 0,0), **Crafting** (compact full-circle, right, +2200,0); each wheel has its own hub bubble
  plus a heading ("MAGIC"/"PHYSICAL"/"CRAFTING"). Compact wheels reuse the tuned layer bands scaled for a
  single full circle (arc capacity ~6Ã— wider).
- `FitTreeToViewport` switched from centred max-radius to a **bounding-box fit** (re-centres the content
  on the box â€” off-centre wheels no longer clip); `TreePan` zoom widened to **0.05Ã—â€“20Ã—**.
- **Magic nodes tint by element**: `NodeColor` lerps learned/available/locked state color 60 % toward the
  skill's `DamageKind` color (`DamageNumber.ColorFor`) â€” fireball reads orange, frostbolt icy-blue, etc.
- `EnsureProgression` now also adds `TalentTracker` (after `LevelUpSystem`) so the skills panel always has
  the tracker.

### Projectile path preview â€” full flight-path cone while charging (new)
- `Assets/Scripts/Combat/Weapons/ProjectilePathPreview.cs` (+ .meta) â€” prefab-free singleton, world-space:
  a **cone of 10 translucent rings** (`Rings=10`, `RingSegments=18`, `MaxHalfAngleDeg=45`) from the weapon
  along the aim line that **narrows as charge builds**, collapsing to a thin centre ray of the exact
  predicted trajectory at full charge; clipped at the first solid hit.
- `PlayerController.cs` (`_pathPreview` field via `ProjectilePathPreview.Instance`):
  - **Magic projectile spells** while charging â†’ preview mirrors caster aim (spawn near the hand,
    `fwdÃ—0.5 + upÃ—0.3`, reach = `max(speed,1)Ã—4` flight envelope), tinted by the **spell element**.
  - **Ranged** (regular draw AND the per-hand dual draw) â†’ preview from the hand along aim, reach =
    `speedÃ—lifetime`, spread from ranged accuracy; half-angle shrinks as charge grows, tinted by shot type.
  - Hidden on cancel/release/weapon-switch (`HidePathPreview`). New `MagicChargeFullTime = 1.2f` (ramp for
    `SpellChargeLevel`), public `LookPitch` (torso bends with camera), `_chargeDrained` tracks the 1v
    real-time FP drain.

### Class & race switching are now free
- `ClassUnlocker.cs` `SetActiveClass`: the `freeBaseline` (Wanderer) + `IsUnlocked` gate is **removed** â€” any
  **known class id switches freely**; returns false only for unknown ids. The class-change dialog shows
  "(current)" and no requirement summary / lock dimming; falls back to `ClassUnlocker.BuildDefaultClasses()`
  when the roster is empty.
- Race dialog: same "(current)" treatment; confirm now calls `SetActiveRace(requireStone: false,
  unlockIfNeeded: true)` â€” **no Ritual Stone cost, the target race auto-unlocks** for this character.

### UiAssetCache â€” central UI asset cache (new)
`Assets/Scripts/UI/NewWorld/UiAssetCache.cs` (+ .meta) caches `MenuTexture` + `DefaultFont`
(`VietPixel`), replacing per-file `Resources.Load("menu")` / `Resources.Load<TMP_FontAsset>("VietPixel")`
in `CharacterInfoUI`, `MenuPanelBase`, `TypingMinigame`, `FishingUI`.

### Farming â€” plots + seeds plant on drop
- `ToolManager.DropThrow.cs`: dropping **seeds onto a farm Plot** now plants directly
  (`FarmingManager.GetPlotAt` â†’ `PlantPlot`, "pop" sfx) instead of only gifting the world.
- `ToolManager.cs` farming actions (till / water / fertilize / boost-growth / harvest) route through
  `FarmingManager` plots first with the legacy `_worldBuilder`-field fallback; `BuildingCount` â†’
  `BlueprintOptionCount` (`ToolManager.BuildingMenu.cs`). `FarmPlot.cs` / `FarmingManager.cs` gained the
  plot-query/harvest helpers.

### Ranged â€” runtime arrow fallback when no prefab
- `RangedWeaponBehavior.cs` (175 changed lines): `FireProjectile` with no projectile prefab now builds a
  **generated arrow** projectile at runtime (shaft + head) instead of the old hit-scan raycast tracer.
  (Throwing-hammer visual already existed from 1m.)

### World gen & boot perf
- `GameBootstrap.cs`: `FarmingManager` ensured at boot; render distance **3 â†’ 5**, `MaxRadius 160`; only the
  **spawn chunk builds synchronously** â€” the surrounding chunks stream in via the background pass (~1.5 s).
- `ChunkLodManager.cs`: `EffectiveCullDistance()` **auto-scales the LOD cull distance to the streamer's
  render radius** (`_streamer`), so culling no longer fights the bumped radius; `WorldStreamer.cs` and
  `RenderDistanceController.cs` got the matching radius plumbing.
- `NewWorldTestGround.cs`: `SpawnBenchBudgeted` coroutine spawns the test bench **one lane group per frame**
  (was all-at-once in Awake, which blocked early frames) while preserving `SpawnBench` ordering; the platform
  max-height scan samples Perlin **every 2 m (61Ã—61 vs 121Ã—121, ~4Ã— fewer noise calls)** with a +3 m clearance.

### Housekeeping
- **Accidental commit**: `Assets/_Recovery/0 (16).unity` + `0 (17).unity` (347-line unused crash scenes,
  with metas) snuck into this commit â€” they belong in the pending `_Recovery` cleanup (see Â§3).

### 1aa-status
- No CLI build available in this environment (Unity project) â€” compile/behaviour verified by code review
  only. Unity play-test pending: new game grants one random talent + Talents tab ranks it; level-ups grant
  talent points; skill detail shows Lv/XP climbing per use; Lightning root + Chain Lightning relocation;
  3-wheel General tree pans/zooms without clipping; path cone narrows on magic/ranged charge and clips at
  walls; free class/race switching; seed-drop planting on plots; render-distance bump + LOD cull;
  bench spawn no longer hitches at boot.

---

## 1ab. Recent completed work (2026-09-14) â€” Tornado spell uses the old environmental tornado model + function; Magic-wheel per-school wedges

- **`MapBuilder.BuildTornado`** gains a `widthScale` param (default 1 â€” town-tornado event unchanged).
- New **`SpellTornado.cs`** (Vortex delivery for `magic_tornado_spell`): rebuilds the old tall
  drifting debris funnel (`BuildTornado` â†’ `TornadoBehavior`) scaled to the spell radius, tunes the
  TornadoBehavior fields to spell scale (so pulled objects don't ride up to the old 80-unit orbit),
  and layers SpellZone-style Wind damage ticks + enemy pull on top; destroyed after its lifetime (5 s).
- **`SpellCaster.SpawnVortex`** routes only the Tornado spell to `SpellTornado`; all other Vortex
  spells (e.g. Mini Tornado) keep the `SpellZone` funnel unchanged.
- **`CharacterInfoUI.BuildWheel`** (compact three-wheel board): each school/category now gets its own
  wedge around the full circle (`sectorHalf = Ï€/groups âˆ’ gap`, per-wedge hub + school label) instead of
  all nodes cramming into the bottom sector. Standard single-wheel layout is untouched.
- Docs: game-design.md Â§3.8 (Vortex exception â†’ `SpellTornado`), SkillCatalog wind-line comment.

### 1ab-status
- No CLI build â€” code-review verified (braces, ids, per-wedge capacity). Play-test pending: cast
  Tornado â€” expect a big drifting debris tornado (~10 tall / ~7 wide) that pulls props via physics +
  enemies and ticks Wind damage for ~5 s; Magic wheel per-school wedges read cleanly around the circle.

---

## 1ac. Recent completed work (2026-09-14) â€” Tornado spell visibly spins and carries animals into a swirl

User: "the tornado spell currently does not spin and does not pull animals toward it, only makes
them fly up." Cause: funnel blocks were square prisms (Y-rotation invisible, no axis orbit), and
`Livestock`/pet controllers overwrote the Rigidbody X/Z every physics step while keeping Y â€” so
the orbit push was erased and only the vertical lift survived.

- **`TornadoBehavior.cs`** â€” blocks now orbit the tornado axis at their own radius + self-rotate
  (visible churn); defaults `BaseRotateSpeed 8â†’24`, `RotateSpeedVariation 4â†’16`. Pulled objects get
  a two-phase tow: ~0.8s drag into the axis at ground level, then lift + swirl. New carry flag via
  `ITornadoCarried` (set on capture, cleared on release/destroy). `AddDebrisBlock` orbit heights use
  `OrbitHeight` (was hardcoded 80) so spell-sized tornados keep debris low.
- **`MapBuilder.BuildTornado`** â€” blocks are rectangular (`width Ã— h Ã— width*1.55`) so rotation shows.
- New **`Assets/Scripts/World/ITornadoCarried.cs`**; **`Livestock.cs`** + **`PetController.cs`**
  implement it and stop writing their own velocity while carried.
- **`SpellTornado.cs`** â€” faster spin (BaseRotateSpeed 70 / Variation 55) + 13 `AddDebrisBlock`
  chunks for the old-game swirling debris look.
- Docs: game-design.md Â§3.8 Vortex note, SkillCatalog wind-line comment.

### 1ac-status
- No CLI build â€” code-review verified. Play-test pending: cast Tornado â€” the funnel should visibly
  churn; chickens/cows/pets and physics props get towed into the axis, then carried in a low swirl
  for the tornado's life (no more instant vertical pop). Town-event tornado inherits the same churn.

---

## 1ad. Recent completed work (2026-09-14) â€” Wind Walk flight spell

User: "add flight spell." A timed self-buff (Instant delivery that affects the caster) in the Wind
school after Tornado; prereq Gale Force.

- **`SpellData`** â€” new `SelfBuff` flag; **`SkillCatalog.Spell`** factory gains an optional
  `selfBuff` param. New skill **`magic_flight`** "Wind Walk": Wind, Focus 20, FP 18, Instant,
  cooldown 25 s, `duration 10` (self-buff), prereq `magic_gale` â€” baked into wind-wheel layout.
- **`SpellCaster.ResolveDirect`** â€” `SelfBuff` spells skip the hit scan and run a caster effect:
  `PlayerController.BeginFlight(Duration)` + RingFlash, then return.
- **`PlayerController`** â€” flight state (`BeginFlight`/`EndFlight`/`IsFlying`, `FlightSpeed 12`,
  `FlightVerticalSpeed 6`): flying disables sprint + dodge, moves at flight speed, and replaces the
  grounded/gravity vertical branch with free vertical movement (hold **Space** to ascend,
  **LeftCtrl** to descend; release to hover). Buffs stack by extending `_flightUntil`.
- Docs: game-design.md Â§3.8 `selfbuff` field line, SkillCatalog wind-line comment.

### 1ad-status
- No CLI build â€” code-review verified. Play-test pending: learn Wind Walk (requires Gale Force), cast
  it from the wheel or a hotkey â€” instant RingFlash, then 10 s of fly; Space/LeftCtrl move vertically,
  landing resumes normal movement. Cooldown 25 s; recast mid-flight extends the timer.

---

## 1ae. Recent completed work (2026-09-14) â€” Event tab unlocked in the pause menu (new game)

The Event Test panel (`UIManager.EventTest.cs`) existed but was dead: `CreateEventTestPanel` was never
called and nothing opened it. It is now reachable from the Pause menu.

- **`UIManager.cs`** â€” capture menu dimensions (`_menuPanelW/H/_menuPad`) in `InitializeUI` so the panel
  can be built lazily; new pause-menu button **"Sá»± Kiá»‡n"** (Events) between Quests and Settings; pause
  buttons re-spaced to a uniform 0.10Â·panelHeight pitch to fit all 7; button text re-applied on the
  localization refresh path.
- **`UIManager.EventTest.cs`** â€” `ShowEventTestPanel(true)` lazily calls `CreateEventTestPanel` on first
  open (list reflects events registered by then). The panel's event buttons already close the pause
  menu, resume the game, and `ForceEventByIndex` â€” unchanged.

### 1ae-status
- No CLI build â€” code-review verified. Play-test pending: Pause â†’ **Sá»± Kiá»‡n** â†’ pick an event (grouped
  Tier 0/1/2 grid) â€” the game resumes and that event triggers (e.g. tornado storm over the town).

---

## 1af. Recent completed work (2026-09-14) â€” Water + Earth magic schools: Wet status & terrain deformation

User: "add water and earth magic" (with the schools sharing the tree's existing depth: full L1/L2
rosters like the other schools; Water applies a **Wet** status; Earth has **no status** â€” its spells
reshape the terrain, "earth move would circle around modifying terrain"; school name on the wheel's
school bubbles).

- **Wet status** â€” `StatusEffectType.Wet = 6`. New **`WetStatus.cs`**: `SlowFactor 0.85` + `Duration 4 s`
  (re-applies `EnemyController.ApplySlow` on apply + first Update, so it stacks safely with Frost via
  `Mathf.Min`), and **conducts Ice/Lightning** â€” `IceLightningDamageBonus 1.4`. `SpellCaster.ApplyStatus`
  routes Wet â†’ `WetStatus.Apply`; `ApplyHit` boosts Ice/Lightning vs wet targets via
  `WeaknessMultiplier 1.4` (Ice/Lightning Ã— Wet = Ã—1.4).
- **`TerrainShape`** (`None/Ring/Spikes`) on `SpellData` + `Spell(...)` factory param. New
  **`TerrainDeformer.cs`** facade â†’ **`WorldStreamer.DeformAt(center, radius, shape)`**: lifts affected
  tile-corner heights (Ring = stone wall at ~0.72Â·radius, Spikes = mound + deterministic hash peaks;
  smootherstep falloff), marks the tiles modified/dirty, and rebuilds the affected chunk's merged
  mesh+collider. Hooked in `SpellCaster.ResolveZone` â€” zone Earth spells deform the ground at impact
  before damage resolves.
- **Water school** â€” root **"Water Bolt"** (magic_water, Focus 14, projectile, Wet). L1: Tidal Stream
  (beam), Whirlpool (vortex), Mist Veil (zone), Deep Mind (Int+3), Healing Spring (instant heal).
  L2 tables Ã—5 (Tsunami storm, Maelstrom, Downpour, Healing Tide, etc.) â€” wet-applying throughout.
- **Earth school** â€” root **"Stone Shard"** (magic_earth, Focus 15, projectile). L1: Boulder Crash
  (zone knockback), Tremor (**Ring** deform), Spire Field (**Spikes** deform), Earth Bulwark (Def+3),
  Stone Effigy (summon). L2 tables Ã—5 â€” Ring/Spikes deform escalates (Seismic Ring, Epicenter, Stone
  Pillars, Crystal Field, Colossus, etc.).
- **Wheel labels** â€” `CharacterInfoUI.BuildWheel`: the compact-wheel school bubble never set its label
  text; now displays the root skill's name (`slbl.text = root.displayName`) â€” Magic grows from 7 to 9
  school wedges (Water Bolt / Stone Shard included).
- Docs: game-design.md Â§3.7 (Wet row, 6â†’7 statuses, signature list Waterâ†’Wet / Earthâ†’terrain),
  Â§3.8 (`terrain shape` bullet), Â§3.3 (9 L1 roots; Magic 16/80/400/496, TOTAL 2046).

### 1af-status
- No CLI build â€” code-review verified. Play-test pending: learn/arm Water ("Wet") and Earth (Tremor /
  Spire Field deform the ground) schools from the magic wheel; verify wet-target Ice/Lightning bonus
  and that deformed chunks persist after reload.
- Compile-fix follow-up (2026-09-14, `ee3b97b`): `WorldStreamer.DeformAt`'s spike hash is now `int`
  (bitwise `&` was illegal on `float`), and the unedited-corner fallback `CornerOrBase` is a non-static
  method so it can call the instance `CurrentHeightOf`. No design change â€” pure compile fixes.

---

## 1m. Recent completed work (2026-09-12) â€” magic delivery overhaul (Beam / Summon / Storm) + throwing-hammer fix
User: duplicate-feeling spells across the magic schools should each behave distinctly. Three new spell
deliveries (Beam, Summon, Storm) added alongside projectile / instant / zone / vortex; 16 spells
reworked to use them. Spell **ids unchanged** (learned-skill data safe).
- `SpellData.cs` â€” `SpellDelivery` += `Beam=4, Summon=5, Storm=6`; new fields `TickInterval=0.5f`,
  `ChannelDrainPerSecond=0f`.
- `SkillCatalog.cs` `Spell()` factory gained optional `projectileSpeed=20f, tickInterval=0.5f,
  channelDrainPerSecond=0f`; new delivery drivers `Assets/Scripts/Combat/Weapons/SpellBeam.cs`,
  `SpellSummon.cs`, `SpellStorm.cs`.
- **Beam (user-approved controls):** the cast fires on the normal cast release (LMB-up at the frozen
  charge level); **holding LMB keeps the beam on while `ChannelDrainPerSecond` FP drains real time**
  (via `TrySpendFocus`; a rejected cast ends without killing a live beam â€” `StopChannel` runs after the
  spend). Ticks damage/heals every `TickInterval` on the capsule casterâ†’aim; 0.4 s release-grace; ~1.6 s
  fixed-sustain fallback on mobile/no-mouse. `PlayerController.cs:921` blocks re-aim/melee while
  `SpellCaster.IsChanneling` (hold keeps the beam, doesn't retrigger).
- **Summon:** ground-targeted (AoE preview, like zone/vortex). Damage summons = persistent **turrets**
  firing bolts at the nearest enemy (`BoltPowerMultiplier=0.6`, reuses bolt flight via
  `SpellCaster.DecorateProjectile`); `Heals` summons = standing heal aura for `IHealable` allies.
- **Storm:** persistent ground zone striking repeatedly (`StrikesPerTick=2`, `StrikePowerMultiplier=0.8`,
  randomized 0â€“0.35 s delays via coroutines), element-styled FX (crossed bolt bars for Lightning).
- `SpellCaster.cs` â€” 3 new switch cases + `ResolveBeam/ResolveSummon/ResolveStorm`, `GroundTarget`
  helper, `IsChanneling`, `StopChannel`, `ForgetBeam`, `DecorateProjectile`.
- 16 reworked spells (ids unchanged): **Beams** â€” Searing Ray (magic_fireball_scorch_searing, Fire/Burn),
  Arc Storm (magic_chain_arc, Lightning/Stagger), Beacon (magic_focus_holylight_beacon, Holy heal+damage),
  Hunger (magic_dark_devour_hunger, Dark/Rot), Cold Stare (magic_frostbolt_chill_stare, Ice/Frost),
  Storm Breath (magic_gust_stormbreath, Wind/Knockback); **Summons** â€” Frost Obelisk
  (magic_frostbolt_glacier_wall, ice turret/Frost), Shadow Totem (magic_dark_shadowbolt_pool, Dark/Rot),
  Arcane Rune (magic_ward_aegis, Arcane), Healing Shrine (magic_heal_light, Holy heal aura),
  Ember Effigy (magic_fireball_meteor_ember, Fire/Burn), Gust Totem (magic_gust_airburst, Wind);
  **Storms** â€” Thunderstorm (magic_chain_overload, Lightning/Stagger), Meteor Rain
  (magic_fireball_meteor_rain, Fire), Blizzard (magic_blizzard in `SkillCatalog.BuildMagic`, Ice/Frost),
  Eclipse (magic_dark_nightfall_eclipse, Dark/Rot).
- **Throwing-hammer fix:** `throwing_hammer` is `WeaponCategory.Ranged` (WeaponCatalog.cs:105) so it
  fired via `RangedWeaponBehavior.FireProjectile`, whose no-prefab fallback spawned a *generated arrow*.
  New `BuildDefaultProjectileVisual()` in `RangedWeaponBehavior.cs` picks a tumbling hammer
  (`BuildHammerVisual`: handle + head + `TumbleSpin` 360Â°/s) for `id == "throwing_hammer"`, arrows
  otherwise. Checker 0 diagnostics.
- Docs: `game-design.md` Â§3.8 (delivery behaviors) + Â§5.16 (beam sustain); `PROGRESS.md` this section.
### 1m-status
- Source-compile verified by the semantic checker (0 diagnostics, run twice). Unity play-test pending:
  beam fire-on-release + LMB-hold sustain draining FP; summon turret bolting nearest enemy / heal aura;
  storm repeated strikes; hammer tumble visual; all 16 reworked spells from staff/wand/book.

---

## 1n. Recent completed work (2026-09-12) â€” charged magic casts skip the post-release cast time
User: "the endlag on spell cast is crazy â€¦ the wait time between the explosion and the projectile is
too long." Root cause: on LMB release the player already spent the wind-up charging, but
`SpellCaster.CastRoutine` then waited another full `SpellData.CastTime` (0.5 s default, 0.8 s Tornado)
between the CastingCircle burst and the actual delivery â€” a second wind-up after the burst.
- `SpellCaster.cs` `CastRoutine`: the cast-time wait now runs **only for uncharged casts**
  (`charge <= 0f`); a charged cast (`charge > 0`) resolves immediately on release, so the burst ring
  and the projectile/delivery land on the same frame. Applies to every delivery (projectile / instant /
  zone / vortex) and every charging caller (Alt-wheel, class/race casters) since all share
  `BeginCast -> CastRoutine`. Tap-casts keep the short wind-up (0.5 s) unchanged per the user's choice.
- Docs: `PROGRESS.md` this section.
### 1n-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: charge any
  magic spell (staff fireball/wind-blade/Tornado) to ~50-100%, release â†’ burst + bolt appear together
  (no ~0.5 s / 0.8 s gap); tap-cast still shows the brief wind-up; FP cost / cooldown / charge-damage
  math unchanged.

---

## 1v. Recent completed work (2026-09-12) â€” released the charge cap + real-time mana drain while charging
User: "release the charge limit, and mana would decrease on realtime as player charge the spell." Magic
spells now overcharge past the old 2 s / level-1 ceiling while the focus pool lasts, and FP drains
continuously every aim frame instead of being spent all at once.
- **Charge cap released (magic only).** `PlayerController`: the magic aim branch grows `_chargeAccum`
  unbounded while RMB is held and FP > 0; new `SpellChargeLevel(hold)` = same ramp as the capped
  `MagicChargeLevel` but with no `Clamp01`, so level 1+ keeps scaling power/size/cost. Ranged draws keep
  the 2 s cap (`MagicChargeMaxTime`); the HUD bar and weapon anim stay clamped (represent the "readable"
  band).
- **Real-time FP drain.** Each aim frame: `drain = spell.FpCost Ã— ChargeFpCostBonus Ã— level Ã—
  FpChargeDrainRate Ã— Î”t`, capped at current FP, spent via `SpellCaster.TrySpendFocus` (also stays the
  regen delay), accumulated into `_chargeDrained` (reset on aim start / cancel / release). Charge growth
  **freezes when the pool hits 0** â€” releasing still fires at the level already paid (never a dud).
- **Prepaid settlement / single cost authority.** `SpellCaster.BeginCast(â€¦, charge, prepaidFocus)`
  spends only `max(0, fpCost âˆ’ prepaid)` and clamps the charge to what `prepaid + CurrentFp` can cover.
  To keep that coherent, the wheel-cast flat `SkillCost` spend is skipped for `SpellCastEffect` skills in
  `SkillProfile.ExecuteCharged` (drop the `Clamp01` on charge there too) â€” which also **fixes a
  pre-existing double-spend** (tap-cast cost 2Ã— `FpCost` before; now exactly `FpCost`, full charge
  1.6Ã— `FpCost` as documented). New `SkillContext.PrepaidFocus`; threaded through
  `MagicWheelUI.ReleaseArmedCast` and `SpellCastEffect.Execute`.
- Docs: `PROGRESS.md` this section.
### 1v-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending:
  - Hold RMB + LMB on an armed spell past full charge â†’ FP bar ticks down in real time, casting
    circle / AoE marker keep growing past the old cap, charge stalls at empty FP.
  - Release â†’ burst ring + delivery still land together (1n behavior kept), costs follow
    `FpCost Ã— (1 + 0.6 Ã— level)`.
  - Tap-cast (no hold) â†’ costs exactly `FpCost`, resolves instantly now (cast wind-up removed, see 1w).
  - Cancelling a charge (weapon switch / mount / UI) discards the drained FP â€” deliberate.
  - Ranged (bow/throwing hammer) draw unchanged.

---

## 1w. Recent completed work (2026-09-12) â€” magic spam casts instant, FP-only, no more ring-without-bolt
User: "when the player click to spam magic multiple time the magic effect still play but the projectile
did not spawn." Root cause: the local release FX (`BurstCastingCircle` + ring) ran in `PlayerController`
*before* `ReleaseArmedCast`, so a cast rejected downstream (per-spell `SpellData.Cooldown` 4-10 s, the
profile `SkillCost.Cooldown` 2 s gate, or an empty FP pool) still played the ring while no bolt flew â€”
`ReleaseArmedCast` also returned `true` unconditionally, hiding the reject.
- **FP-only limiter (no magic cooldown).** `SpellCaster.BeginCast(â€¦, fast: true)` skips the `IsReady`
  cooldown gate entirely and `CastRoutine` never writes `_cooldowns` on fast casts; `SkillProfile.
  ExecuteCharged` routes `SpellCastEffect` skills straight to `BeginCast(â€¦, fast: true)` (skipping the
  profile's flat cost + 2 s cooldown gates). The equipped spell's FP cost + the real-time charge drain
  are the only limiter â€” every click casts as long as mana holds. Non-magic skills, class/race spells
  and magic weapon arts keep their `fast=false` behavior (cast time + cooldown) unchanged.
- **Instant tap-casts (wind-up removed).** The `CastRoutine` wait is fully skipped for fast casts, so
  both charged releases (1n) and plain taps resolve the bolt on the same frame the click releases.
- **True cast result + no phantom FX.** `MagicWheelUI.ReleaseArmedCast` propagates `ExecuteCharged`'s
  result (now the real `BeginCast` bool for magic), and `PlayerController` only plays
  `BurstCastingCircle` / locks the AoE preview when the cast actually began; a rejected cast (empty
  pool) hides the circle/preview silently instead of fake-firing.
- Docs: `PROGRESS.md` this section.
### 1w-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending:
  - Spam-click fireball/wind-blade â†’ a bolt spawns on every click, instantly, until FP runs out; no
    ring flashes without a bolt; no wind-up delay.
  - Charged casts still land instantly on release (1n) with overcharge drain intact (1v).
  - Magic weapon Arts (staff Art), class/race spells and ranged weapons keep their cast time/cooldown.
  - Consequence to confirm: spell-cooldown stats/passives (Int `CooldownMul`, Mage/Enchanter arcane
    timing) no longer affect wheel-cast magic.
  - Charge bar now overflows past 100% during overcharge (magic only): `MagicChargeProgress` reports
    the uncapped `SpellChargeLevel` for armed magic, and `PlayerBarsHUD` scales the left-anchored fill
    by `level` so it grows past the track end; the % label climbs past 100. Ranged draw stays capped
    at 100%.

---

## 1o. Recent completed work (2026-09-12) â€” sword/shield defense animation + permanent-arm-corruption fix
User: "the sword and shield should have 2 set of animation for attack and defenseâ€¦ it only don't have
animation for that", then "if the player spam attack continuously the model might be bug and got
permanently altered". Root cause of the corruption: no single-owner phase machine â€” re-entering a
phase before it ended (spam/charge-cancel/guard) leaked `AcquireArms` claims, leaving `SuppressArms`
stuck so `PlayerAnimator` never restored the arms; plus attck/sway "rest" was captured from the live
(aesthetic-posed) bones, so `End()` re-committed a polluted pose as rest.
- `WeaponAnimator.cs`: new **defense guard** â€” the "defense" set alongside the attack swings. `PlayGuard()`
  / `EndGuard()` / `UpdateGuard()` ease the arms into a held guard pose (eased grab-in 0.18 s) while RMB
  blocking; per-weapon `GuardPoses` (t=1 hold keys): shields raise the face up in front, blades tuck a
  defensive guard, greatsword/warhammer/greataxe raise a two-hand cover, fists/gauntlets boxer guard,
  magic/ranged fall back to neutral (never block).
- **Single-owner phase fix**: `Acquire()`/`Release()` (idempotent, `_ownsArms`), `CaptureRest()` and a
  unified `End()` teardown. Every phase transition (attack/charge/guard/sway) abandons the old hold
  without releasing, so re-entrancy can't unbalance the arm-owner count; `OnDisable` does the same full
  teardown. Arm "rest" is now always **local identity** (the model's documented rest) instead of a live
  capture â€” a polluted capture can never bake an altered pose in. Weapon transform is still re-captured
  per phase (re-parent safe).
- `CombatController.cs`: `SetBlocking` drives `PlayGuard`/`EndGuard` on the state edge only, and
  `CanKeepBlocking()` (idle + loadout drawn) runs in `Update` so sheathing/stow drops the guard and the
  guard pose never fights the stow idle.
- `PlayerAnimator.cs`: watchdog â€” any rig driving the arms pings every frame (`PingArms`); if
  `SuppressArms` hangs > 0.5 s with no writer, owners are force-released + one log. Backstop if some
  unrelated flow ever leaks again.
### 1o-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending (guard pose
  angles are starting values â€” expect to tune):
  1. **No corruption**: spam LMB 30 s single + dual wield, interleave guard/attack/charge spam, walk
     + idle 5 s â†’ arms always return to rest; no frozen/stuck arms; no `[PlayerAnimator] arm-owner
     claim hung` watchdog log during normal play.
  2. **Defense animation**: sword+shield â†’ RMB hold raises the shield arm into a high guard, sword
     arm tucks (attack swings vs guard stance clearly distinct); release â†’ settles back to the ready
     sway; blocked hits keep the guard until stamina breaks (then it drops).
  3. Fists/gauntlets boxer guard; greatsword two-hand cover; guard dropped cleanly on sheathe, and
     re-raised after re-draw while RMB still held.
- **Committed + pushed** with the rest of the session's work â€” 5 commits (`0063afd..cc3e787`, origin/main):
  `b6ff2ba` feat magic redesign Â· `9c44ea7` ui magic wheel circles Â· `ea56467` ui skill tree zoom/labels Â·
  `eb9feaa` fix weapons-only start bag Â· `cc3e787` feat guard animation + arm-leak fix. Worktree clean.

---

## 1p. Recent completed work (2026-09-12) â€” removed the game-start tool/food seed
User: "remove item that is not weapon from the player inventory â€¦ just delete the code that add them
into player inventory when the game start". The non-weapons came from the test-bench seeding, so the
tool-kit spawn was simply deleted â€” no new inventory plumbing.
- `NewWorldTestGround.cs`: removed `EnableTools`, `SpawnToolKit()` (axe/pickaxe/hoe/hammer/scythe/
  watering_can/fertilizer/club/rosary/fishing_rod Ã—1 + banh_mi/com_tam/nuoc_dau/mi_chinh/xap_phong Ã—5)
  and its two call sites in `SpawnBench()` and `GrantBenchBag()`. Weapons-only grants
  (`SpawnAllWeapons`, weapon rack) unchanged.
- Non-weapons can still enter the bag mid-game via pickups/crafting/shops â€” only the start-of-game
  seeding was removed, as requested.
### 1p-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: new game /
  bench spawn starts with only the weapon grants in the bag (no axe, no food); weapon rack + all
  catalog weapons still spawn; rest of the bench lanes (farming/enemies/buildings/NPCs) unaffected.

---

## 1q. Recent completed work (2026-09-12) â€” skill tree: aggressive zoom + node labels auto-size
User: "increase the zoom ability of the skill tree and fix the bug that the text is too big compare
to the node". Scope confirmed via question: **aggressive** zoom range (Min 0.08 / Max 10) and
**auto-size** labels that keep wrapping.
- Root cause of the text bug: node boxes and label fonts were sized on unrelated scales â€” general
  nodes are 18x14 / 12x10 / 8x6 px (`MakeTreeNode`) and class/race nodes are size*2 x size (24/18/14
  tall), while labels used `Screen.height/â€¦` with `Max()` minimum clamps (8/8/6pt general, 10/8/7pt
  class/race) that dominate at 1080p+ â€” an 8pt font in a 10px box, 6pt in a 6px box, spilling over.
- `CharacterInfoUI.cs` `MakeTreeNode` / `MakeClassTreeNode` / `MakeRaceSkillTreeNode`: labels now use
  TMP auto-size bounded by the node itself â€” `enableAutoSizing`, `fontSizeMin 2f`, `fontSizeMax`
  derived from the node (`nh * 0.85` general, `size * 0.75` class/race), wrapping kept, with
  `overflowMode = Ellipsis` only as a last-resort for names that can't fit even at min size.
- `TreePan`: `MinScale 0.28 -> 0.08` (full-wheel overview), `MaxScale 3 -> 10` (close reading of tiny
  nodes), scroll step `1.2 -> 1.25` so the wider range is usable. `FitTreeToViewport` already clamps
  to these constants, so the automatic fit is unchanged.
- `FitTreeToViewport`: now also folds `_raceTreeNodes` into the max-radius fit (was omitted â€” Race
  sub-tab could under-fit).
### 1q-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: scroll-zoom
  all the way in (10x) and out (0.08x) on General/Class/Race sub-tabs; long skill names auto-shrink to
  fit their node and never spill outside it; the Race tree fits the viewport on open; no label blur/
  NRE during zoom/pan.

---

## 1r. Recent completed work (2026-09-12) â€” magic redesign: school signatures, real healing, persistent zones
User: "the magic is not very creative, most of the magic using the same thing over". Scope (confirmed):
leverage existing/dormant systems rather than add new delivery types â€” statuses/DoT, real healing,
knockback, and persistent zones. Applied to **both** the base 12 magic tree and the ~70-spell design
bank. Existing FP/cooldown/cast pipeline untouched.
- **School signatures** â€” Fireâ†’Burn, Iceâ†’Frost, Lightningâ†’Stagger, Darkâ†’Rot, Windâ†’Knockback (Tornado
  stays a pull), Holyâ†’heals, Arcaneâ†’Stagger (bind/hold). Spells that previously differed only by power
  now read distinctly on hit.
- `SpellData.cs`: added `Duration`, `Heals`, `Knockback`; status fields kept with `StatusProcChance = 1f`.
- `SkillCatalog.cs` `Spell(...)` helper: new optional `heals`, `knockback`, `duration`, `statusEffect`
  params â€” shared by both catalogs. `BuildMagic` base 12 retuned; **Ward**/*Arcane Ward* and
  **Blizzard** converted from weak `Zone` to aimed `Spell` zones (Blizzard persistent 2.5 s + Frost).
- `SkillCatalog.Magic.cs`: L1 blocks (focus/arcane/fireball/frostbolt/dark/gust) and L2 headliners
  retuned â€” chain fork/arc/overload/leapâ†’Stagger, Scorch Burn, Deep Freeze Stagger, chilled bolts
  Frost, Consume Rot, Mini Tornadoâ†’**Vortex** (8 m, pulls), airburst Crack/Pressure/Shockwave knockback,
  Arcane Shackles/Hold Stagger, and the whole heal family (Greater Heal, Light's Embrace, Purify,
  Mending Light, Radiance, Beacon, Sunburst, Regrowth, Restore, Bloom) now carry `heals: true`.
- New `IHealable.cs`; `PlayerController` implements it (uses its existing `Heal`).
- New `SpellDoT.cs` â€” Bleed/Poison/Rot/Burn ticker (per-tick = power Ã— 0.12 over 4 s; refreshes).
- New `SpellZone.cs` â€” unified persistent zone (tick damage Ã— per-delivery multiplier â€” Zone 0.4,
  Vortex 1.0 â€” optional pull, Holy heals `IHealable` allies inside per tick; lifetime expiry). Replaces
  and deletes `WindVortex.cs`.
- `SpellCaster.cs`: `ResolveZone` routes `Duration > 0` to persistent `SpellZone` and heals allies when
  `Heals`; `ApplyHit` gained heal branch + `ApplyStatus` (proc chance) + `ApplyKnockback`;
  `ResolveDirect` self-heals for Instant heal spells; `SpawnVortex` now spawns `SpellZone` (pull 3.5).
- Docs: `game-design.md` Â§3.7/Â§3.8 (signatures, healing, persistent `SpellZone`, fields).
### 1r-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: hit an
  enemy with each school and confirm the status/icons (Burn/Frost/Stagger/Rot) and Wind knockback;
  cast a Holy heal at low HP and confirm self-heal, and a Holy zone near allies; verify Tornado pulls
  and Blizzard ticks + chills; confirm projectiles still never detonate at the caster's feet.

---

## 1s. Recent completed work (2026-09-12) â€” magic projectile bolts no longer detonate on the caster/feet
User reported magic projectile spells "hit the ground way too often". Root cause: the bolt spawns
exactly at the in-hand rig root (no Muzzle offset â€” ranged uses a child Muzzle at local (0, 0.1, 1))
and SpellEffect's flight raycast hit EVERYTHING with no owner-root skip. The `~0` layer-mask ray cast
from the hand position clipped the player's own CharacterController capsule on the first frame and
detonated at the caster's feet, reading as a ground hit.
- `SpellEffect.cs` (`Update`): the flight raycast now ignores hits on the caster's own root
  (`hit.collider.transform.root != _caster.transform.root`) and keeps flying â€” mirrors
  `RangedProjectile.Update`. The splash `OverlapSphere` already skipped the caster's root.
- `SpellCaster.cs` (`FireProjectile`): spawn lifted clear of the body like the bow Muzzle â€”
  `pos += fwd * 0.5 + up * 0.3`. Applied only to projectiles; Instant/Zone/Vortex placement untouched.
### 1s-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: fire a
  projectile spell straight ahead / slightly downhill / at a close 3â€“5 m target / with an NPC beside
  the caster â€” bolt leaves the hand and only bursts on real obstacles; no puff at the feet; no
  self-damage from splash.

---

## 1t. Recent completed work (2026-09-12) â€” casting circle: two LineRenderer rings on own children
User reported a play-test crash: `NullReferenceException â€¦ CastingCircle.Build() (line 144)` on first
magic aim. Root cause (verified via Unity docs/QA): a GameObject can hold only **one** Renderer
component â€” `gameObject.AddComponent<LineRenderer>()` for the second (inner) ring returns null in
Unity 6, so `_innerRing.useWorldSpace` threw.
- `CastingCircle.cs`: each halo ring now owns its own child GameObject ("OuterRing" / "InnerRing",
  parented at local origin under the CastingCircle transform) before `AddComponent<LineRenderer>()`.
  Visuals identical â€” `useWorldSpace = false` means both rings still render in local space around the
  circle's origin, which the parent transform positions/rotates onto the weapon.
### 1t-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: hold LMB
  with armed magic â†’ halo + inner spin ring appear (no NRE), charge grows, cast-burst ring still fires.

---
User: "some of the spell in the alt magic circle i'm sure is a physical skill, i want that circle to
consist of magic skills only." Scope confirmed (via question): also align `EnsureArmedMagic` auto-arm.
- `MagicWheelUI.cs`:
  - `RebuildEntries` filter is now `!skill.IsPassive && skill.Type == SkillType.Magic` â€” melee, ranged,
    stealth, crafting & fortitude castables never enter the wheel. (`Skill.IsMagical` was NOT used: it
    flags elemental flavor and would wrongly include `melee_berserk`/`ranged_arrowrain` and wrongly
    exclude `magic_heal`; `Type` is the correct gate and is carried onto every expanded magic-tree
    branch in `SkillCatalog.ExpandTree`.)
  - `EnsureArmedMagic` aligned: the "keep currently armed" path also requires the armed skill to be
    Magic-type, and both fallback passes arm only Magic-type skills â€” auto-arm can never pick a
    physical skill as the "magic".
  - Class doc-comment updated ("learned castable magic skill â€¦ magic category only").
- Docs: `game-design.md` Â§5.16 (wheel lists learned magic-category skills only); `PROGRESS.md` this
  section.
### 1u-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: armed
  melee/ranged/stealth skills no longer appear; all magic spells still present (incl. Holy Heal);
  player with only non-magic skills sees "No spells learned yet"; auto-arm without the wheel picks
  only magic skills.

---
User: change the magic Alt quick-choose from one big ring to "multiple circle". Scope confirmed (via
questions): concentric rings; keep hold-Alt / hover / release-to-select interaction and the same skill
set (learned non-passive, cap 64).
- `MagicWheelUI.cs`:
  - Layout driven by `InnerRingCap = 6` / `MidRingCap = 18` (outer ring takes the rest): slots
    `< 6` â†’ inner, `< 24` â†’ middle, else outer. 1â€“6 spells = single inner circle; 7â€“24 = inner+middle.
  - Per-ring radii (% of canvas height): inner 0.17, middle 0.30, outer 0.42; per-ring base slot sizes
    inner 0.11 / middle 0.085 / outer 0.07, then shrunk by `(2Ï€Â·r)/(nÂ·GapRatio)` so arcs keep a gap.
    Every ring is a full circle starting at âˆ’90Â° (concentric).
  - `CreateSlot(index, ringIndex, ringCount, skill)`; new `List<float> _slotSizes` (built/cleared with
    slots) so `Paint()` hover hits each slot with its own radius (`size Â· 0.78`, nearest wins).
  - Removed single-`_slotSize` / `_ringRadius` fields. Hover/cooldown-dim/armed-colour logic unchanged.
- Docs: `game-design.md` Â§5.16 ("3 concentric circles"); `PROGRESS.md` this section.
### 1v-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: 1â€“6 learned
  spells (single inner circle), 7â€“24 (inner+middle), 25â€“64 (three circles), hover/highlight/dim/armed
  colors, release-select, armed chip, fonts across all rings.

---
User: with 2 swords equipped both hands shared the same wait time; wanted each hand to swing
independently. Scope confirmed: per-hand timing for all dual melee; a dodge cancels an in-flight swing.
- `CombatController.cs`:
  - New `HandSwing` struct (`EndAt` / `LastEnd` / `Combo`) + `_swingR/_swingL`; predicate
    `PerHandScheme = HasLoadedDual && !BothHandsMagic` (same shape as `PlayerController.dualMode`).
  - Per-hand branch in `LightAttackWith(hand)`: gates on THAT hand's own timer (not the global `CanAct`)
    plus `CurrentState == Idle` (roll/heavy/parry still gate every hand). No global state change, so the
    other hand stays free. Same stamina cost / light-attack duration / `AttackSpeedScale` math; per-hand
    combo chain (pause > `ComboResetTime` reset, cap 3). Single / two-hand / both-magic keep the stock
    global path.
  - `TickHand(ref)` in `Update()` frees each hand and bumps its combo when the swing completes.
  - `Dodge()` cancels in-flight per-hand swings (`EndAt = 0`) so the roll reads cleanly.
  - `ResetCombo()` and `OnDisable()` also clear the per-hand state.
- Docs: `PROGRESS.md` this section. (`game-design.md` Â§5.16 already specified the independence.)
- Behavior notes: a shield guard can now stay raised while the other hand swings; a dodge still blocks
  new presses; body-animator attack triggers don't fire during per-hand swings (weapon rigs drive the
  visuals via `NotifyWeaponAnimator(hand, â€¦)`).
### 1w-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: 2 swords
  alternating/spam per hand (independent cadence), sword+shield guard-drop, dodge-mid-swing cancel,
  crossed ranged+melee, both-magic/single unchanged.

---

## 1z. Recent completed work (2026-09-11) â€” per-hand dual-wield mouse scheme
User: with 2 weapons equipped, make each mouse button drive its own hand (2 swords = LMB/RMB attack
separately); trade-off = no block except via a shield hand; sword+shield = sword side attacks & shield
side guards; magic in a mixed dual loses its charge (fires uncharged); both-magic/0-or-1 weapon keep
the standard scheme; ranged keeps its charge and the buttons cross (LMBâ†’right hand's weapon, RMBâ†’left
hand's) so a bow in the right hand fires on LMB ("the first mouse clicked fires the other one").
- `CombatController.cs`:
  - `HasLoadedDual` (both hands = real non-fist rigs; fists never count), `BothHandsMagic` (both-magic
    keeps the normal aim/charge/fire flow), `HasRangedDual` (crossed-button trigger).
  - Public `CategoryOfHand(hand)` (wraps private `CategoryOf`).
  - `LightAttack()`/`FireRanged(charge)` now resolve through new `HandOf(behavior)`; added per-hand
    `LightAttackWith(hand)` and `FireRangedWith(hand, charge)` (resolve that hand's behavior â€” the
    dual scheme) that can't be used while blocking.
  - New `NotifyWeaponAnimator(hand, â€¦)` animates only the acting hand in per-hand attacks; the
    existing broadcast `NotifyWeaponAnimators` stays for the heavy/mobile paths.
- `PlayerController.cs`:
  - New `_dualChargeL/_dualChargeR` accumulator per hand; `HandleDualModeCombat` +
    `HandleDualHand` dispatch each mouse button to one hand: Melee = swing on press, Shield = guard
    while held (release unguards), Magic (mixed) = `MagicWheelUI.EnsureArmedMagic` +
    `ReleaseArmedCast(0)` on press (uncharged), Ranged = hold-to-charge (`MagicChargeLevel`) +
    release-to-fire (`FireRangedWith`).
  - `dualMode = FightingMode && !IsMobile && HasLoadedDual && !BothHandsMagic`; guards skip the old
    `_aiming` block, LMB-press block and RMB-block branch while in dual mode. Attack press auto-releases
    a raised guard before swinging. Gates: Not FightingMode (stowed weapons) or mobile â‡’ stock behavior.
- Docs: `game-design.md` Â§5.4 (Dual hand-state bullet) + Â§5.16 controls (full dual scheme table);
  `PROGRESS.md` this section.
### 1z-status
- Source-compile verified by review (Unity project â€” no CLI build, not play-tested).

---

## 1y. Recent completed work (2026-09-11) â€” shield weapons (Block category)
User: "the game have shield skill but nt a sheild, add it in." User confirmed scope: new
`WeaponCategory.Shield`, 3 shields, shields = strictly better block.
- `WeaponCategory.cs`: new `Shield = 3` (`Melee/Ranged/Magic/Shield`).
- `WeaponData.cs`: new guard mods `BlockAbsorbPercent = 0.8f` (min â€” default 80% absorb, matching
  the old bare-hand guard) + `BlockStaminaDrainMult = 1f` under `[Header("Per-Category (Shield)")]`.
- New `Assets/Scripts/Combat/Weapons/ShieldWeaponBehavior.cs` (+ `.meta`, guid
  `d118249d90894ed682054489e68be0a2`): `[RequireComponent(HitboxSystem)]`, `IWeaponBehavior` bash â€”
  mirrors `MeleeWeaponBehavior` (Str-scaled hit, `Completed` fires when the $ hitbox is not active).
  - Repo `.meta` convention (verified this session): metas are the **minimal 2-line format**
    (`fileFormatVersion: 2` + `guid: â€¦`) like `CastingCircle.cs.meta` / `MeleeWeaponBehavior.cs.meta`.
    The first shield commit wrote a full 11-line `MonoImporter` block instead, which Unity rejected
    ("YAML Parsing error â€” Parser Failure at line 11"), so the asset was ignored and the downstream
    compile died at `WeaponDatabase.cs:28` with `CS0246` for `ShieldWeaponBehavior`. Rewrote it to the
    2-line form (guid kept `d118249d90894ed682054489e68be0a2`) â€” imports and compiles in Unity.
- `WeaponCatalog.cs`: 3 shields via new `MakeShield` helper â€”
  `buckler` (wt 2, req 1, base 4, Dex scale, absorb 0.85, drain Ã—0.6, skill `wskill_buckler`),
  `round_shield` (wt 5, req 3, base 6, Str, absorb 0.9, Ã—0.7, `wskill_round_shield`),
  `tower_shield` (wt 9, req 6, base 8, Str, absorb 0.95, Ã—0.8, `wskill_tower_shield`).
- `WeaponModelBuilder.cs`: `BuildBuckler` / `BuildRoundShield` / `BuildTowerShield` proc-cube models +
  dispatcher cases.
- `WeaponRigBuilder.cs`: `Shield` BuildRig case (ShieldWeaponBehavior + databind); `DrawPoseFor`
  shield branch (flat on forearm, `(side*0.08, -0.35, 0.18)`, identity rot â€” face normal = +Z);
  `StowPoseFor` shield branch (flat on back, `(0, 0.32, -0.26)`, Euler(0,180,0)).
- `WeaponAnimator.cs`: shared `ShieldBashKeys` track (forward jab / lateral sweep / overhead slam);
  per-id defs `buckler`/`round_shield`/`tower_shield` (sd 0.28/0.32/0.38, so 0.40/0.46/0.55).
- Blocking is now shield-aware (`CombatController.cs` + `PlayerController.cs`):
  - `CombatController`: new `EquippedShield` (scans both hands for `Category==Shield`), `HasShield`,
    `BlockTakenMultiplier` (= 1 âˆ’ shield `BlockAbsorbPercent`, else 0.2) and `BlockDrainMultiplier`
    (= shield `BlockStaminaDrainMult`, clamped â‰¥0.1). `OnBlockedHit` divides drain by class
    `BlockingMul` again and multiplies by `BlockDrainMultiplier`.
  - `PlayerController.TakeDamage`: blocked hits now use `combat.BlockTakenMultiplier` (was hard-coded
    Ã—0.2 â€” updated `if (amount <= 0) return;` guard kept).
  - `PlayerController` RMB gate: `IsMeleeEquipped(combat) || IsShieldEquipped(combat)` (new helper â†’
    `combat.HasShield`) so a shield-only or sword+shield guard blocks; bow/staff + shield still
    reserves RMB for aim/charge.
- No `NewWorldTestGround.cs` change needed: `SpawnAllWeapons` already loops the whole
  `WeaponCatalog.All` list (EnsureOwned + AddItem), so the 3 shields are granted & rack-displayed
  automatically.
- Docs: `game-design.md` Â§3.6 (4-category wording, Shield row + Layer 1/2/3 + Notes bullet on the
  off-hand defense / RMB pairing), Â§5.16 controls line ("block (melee or shield)");
  `PROGRESS.md` this section.

### 1y-status
- Unity integration complete: the `.meta` rewrite (2-line format) fixed both the YAML parse error and
  the CS0246, and the shield script now imports and compiles. Blocking math verified by code review
  only â€” not play-tested (no human-in-loop fight test yet).

---

## 1x. Recent completed work (2026-09-11) â€” skill-tree link highlight, magic wheel, casting circle

### 1x-a. Skill-tree links black; clicked node lights its links white
User: "change nodes link to black, add functions that light up the link when clicking a skill."
- `CharacterInfoUI.cs`: `LineInert` and `LineActive` both now `Color.black`; new `LineHighlight =
  Color.white` (~line 141-143). `RefreshSkillTree` picked every `_treeLine` line once more after
  painting: a line goes white if `target.id == selected` OR the selected skill appears in
  `target.PrereqSkillIds` (i.e. the clicked node's direct parentâ†’child links).
- Decision (user-confirmed): the highlight algorithm lives in `CharacterInfoUI.cs`, NOT
  `SkillCatalog.Ranged.cs` / the catalog partials â€” those stay data-only.

### 1x-b. Magic wheel (Alt) â€” labels now visible + capacity 16 â†’ 64
Complaint: wheel circles had no text (only the centre hint named the spell) and 16 slots was too
small once the spell pool grew.
- `MagicWheelUI.cs`:
  - `MaxEntries` 16 â†’ 64.
  - New `_slotSize` / `_ringRadius` fields; `RebuildEntries` now sizes dynamically â€”
    `_slotSize = min(h*0.13f, (maxRadius*2Ï€)/(count*1.15f))`, `_ringRadius = min(max(h*0.28f,
    count*_slotSize*1.15f/2Ï€), maxRadius = h*0.42f)` (h = canvas height in px). Ring grows with skill
    count, slots shrink with 1.15Ã— arc gap so all 64 fit on screen.
  - Fixed latent bug: `CreateSlot` built the label but never assigned `.text`
    (`slot.label.text = skill.displayName`), font `max(7f, slotSize*0.42f)`,
    `enableWordWrapping = false`, `overflowMode = Ellipsis` â€” long names truncate, full name in the
    centre hint on hover.
  - `Paint` hover hit-radius now `_slotSize * 0.78f` (was hardcoded) so picking stays accurate on the
    small slots.

### 1x-c. Casting circle â€” halo around the magic weapon while charging (new files)
Feature: visible ring around the weapon during aim/charge so a charging cast reads clearly.
- New `Assets/Scripts/Combat/Effects/CastingCircle.cs` (+ `.meta`, guid
  `0c99a538ff784eab94de853363fa8fc8` since the repo tracks meta files). Lazy singleton
  (`Instance`), prefab-free (builds objects in code like `AoeAimPreview`):
  - Disc (solid translucent) + outer LineRenderer ring (48 seg, 0.06 width) + inner spinning ring
    (36 seg, 0.03 width, faster spin). Orientation: ring plane âŠ¥ weapon up-axis
    (`Quaternion.LookRotation(up)`).
  - `Show(anchor, charge, color)`: radius 0.35 â†’ 0.75, alpha 0.35 â†’ 1, spin `18f + charge*60f` deg/s â€”
    all lerped by charge.
  - `Burst(radius, color, upDir)` â†’ one-shot `SkillFx.RingFlash` on cast release.
  - `Hide()` sets all renderers inactive (keeps one-shot rings playing).
- `PlayerController.cs`: new `_castingCircle` field. `UpdateCastingCircle(charge)` called right after
  `UpdateAoePreview` in the magic aim path; on cancel `HideCastingCircle()`; on release
  `BurstCastingCircle(charge)` then `HideCastingCircle()`, then `MagicWheelUI.ReleaseArmedCast(charge)`.
  Helpers `Casting()`, `MagicHand(CombatController)`, `HandIsMagic(GameObject)` (~lines 1140-1210).

### 1x-status
- All three compile-level verified (git diff reviewed; no name conflicts). **Not yet visually
  confirmed in Unity** â€” next session: play with a magic weapon, Alt-wheel >64 spells, charge and
  eyeball the halo + white link highlight.

---

## 1. Recent completed work (race/class skill trees + general tree layout)

### 1a. Race skill tree system (files created this round)
- `Assets/Scripts/Combat/Skills/RaceSkill.cs` â€” `RaceSkill` + `RaceMod{kind,amount}` + `RaceModType`
  (31 enum values, including `AttackSpeedMul`, `DefenseMeleeMul`, `HpRegenPerSecond`...).
- `Assets/Scripts/Combat/Skills/RaceSkillCatalog.cs` â€” 22 race builders (`BuildHuman` â€¦ `BuildElf`),
  helper `Make(list, raceId, node, name, layer, passive, cost, prereqs, desc, IRaceEffect effect = null, params RaceMod[] mods)`.
- `Assets/Scripts/Combat/Skills/RaceEffect.cs`, `RaceSkillCaster.cs`.
- `Assets/Scripts/Player/Races/RaceSkillPassiveManager.cs`, `RaceSkillState.cs`.

Guidelines that MUST be preserved:
- Skill id convention: `rac.{raceId}.{node}`; cooldown key: `"race_" + id`.
- `RaceModType` semantics: `*Bonus` = flat add to race stat % modifier; `*Mul` = multiplier (1+Î£);
  `HpRegenPerSecond` / `EquipLoadBonus` = flat adds.
- `DamageType` has NO `Poison` â€” Serpent-kin venom uses `DamageType.Dark` (RaceSkillCatalog.cs:253).
- Multi-mod `Make(...)` calls: pass `null` for `effect` positionally, or use the named-array form
  `mods: new[] { M(...), M(...) }`. A named `mods:` followed by an unnamed arg is a compile error
  (this already burned us twice: CS8323 then CS1503).
- Current multi-mod nodes: Orc "Thick Hide" (line ~636), Dwarf "Thick Skin" (~975), Elf "Swift Blade" (~1048).
- Catalog is uniform for all races: 1 hub (L0) + 3 paths (L1) + 6 leaves (L2) = 10 nodes.

Integration (all verified to compile before this handoff):
- `PlayerStats.cs` â€” `AddTemporaryStatBuff`, `GetTotal` now adds race-skill stat bonus + temp buffs,
  many derived stats multiply by `ActiveRaceSkillMods?.XxxMul ?? 1f`. `DevMaxAllStats` stays `true`.
- `SkillProfile.cs` â€” `Execute` falls back to `RaceSkillCatalog`/`RaceSkillCaster` for `rac.*` ids.
- `RaceChangeManager.cs` â€” `ApplyRace` now always refreshes/ensures the race-skill components
  (`RaceSkillPassiveManager.Refresh()` + `RaceSkillState.SetRace(...)`).
- `CharacterInfoUI.cs` â€” new Race tab/tree/detail/hotkey-assign wiring.

### 1b. General skill tree layout recalc (JUST DONE â€” not yet visually confirmed)
Complaint: "Layer 1 has too many slots; skills that branch from layer 1 are taking those slots."
- Root cause is DATA, not layout: `SkillCatalog.ExpandTree` (SkillCatalog.cs:381) makes 5 L1 + 25 L2
  children per base skill â‡’ 2,046 nodes (66 L0 + 330 L1 + 1650 L2). L1 ring capped at ~60 slots per
  wedge so it overflowed; the 130px L1â†’L2 moat wasn't enough, so deep skills read as "still on Layer 1".
- User directive (final): "recalculate the space" â€” reduce the L1 area so L2 skills clearly land on
  Layer 2, WITHOUT reducing the catalog.
- Applied in `CharacterInfoUI.RebuildSkillTree` (constants block ~line 1010):
  - `ring0` 280 â†’ 250; `moatBase` 300 â†’ 250 (L1 ring1 r=500); `branchStep` 16 â†’ 22 (L1 ring2 r=522);
    `moatBranch` 130 â†’ **280** (L2 first ring r=802); `deepStep` 120 â†’ 180; `hubR` 180 â†’ 170.
  - `layerPitch` {20,10,12} â†’ {22,10,14}.
  - Ring-allocation math untouched; L2 is still pinned to ring 3+ (never shares an L1 ring).
- Also verified: ring1 cap now floor(500*1.04/10)=52, ring2 54 â†’ L1 band holds max 70 (Magic) fine.
- NOT yet visually confirmed in Unity â€” next session should eyeball the tree.

### 1b-update (2026-09-11): ROOT fix â€” Layer 1 is now true-roots Ã— 5, not 10 bases Ã— 5
The real bug was DATA: `SkillCatalog.ExpandTree` expanded ALL 10 raw L0 skills per category
(5 true roots + 5 hand-authored locked skills) â†’ 55 eff-L1 nodes (Magic 78), not the intended 25.
Fixed in `SkillCatalog.ExpandTree` (SkillCatalog.cs:381): a 4-line pre-pass promotes every
Layer-0 skill that has a prereq to Layer 2 (the deep band), so expansion now only branches the true
roots â†’ L1 = 25 per category (Magic/Fortitude 30, they have 6 roots), L2 = 125 (+ relabeled locks).
`CharacterInfoUI` tier-band comment updated (lines 1001-1008) to match. No layout constants changed.

### 1c-update (2026-09-11): TRUE-depth layering â€” L2 no longer overfiles, L3 owns the deepest chains
Follow-up complaint: "same problem on layer 2-3" â€” the flat "promote every lock to Layer 2" fix from
1b crowded L2 (Melee 130 / Ranged 130 / Magic 158 / Stealth 130 / Crafting 130 / Fortitude 154 against
ring caps 59/72/86) and 2-3-hop chains (Tornado, Masterwork, Heart-Seeker) read as a phantom layer 3.
Fix (`SkillCatalog.ExpandTree`, still SkillCatalog.cs:381): each hand-authored lock now gets its TRUE
prereq-chain depth (root=0, branch=1, deep=2/3) via a DAG depth walk. Depth-1 locks take the 5 slots of
the root they hang from (authored first, synthetic fill) so L1 stays exactly 25/30; a lock that spans
multiple roots (Assassinate: Backstab + Sly Fox) is deduped so it never spawns duplicate L2 children.
Depth-2 locks sit with the synthetic L2 grandkids; depth-3 locks form a real Layer 3 band (0-1 nodes).
New counts: Melee 5/25/126/-, Ranged 5/25/126/1, Magic 6/30/152/1, Stealth 5/25/126/-, Crafting
5/25/126/1, Fortitude 6/30/151/-. `CharacterInfoUI.EffLayerOf` simplified to return `s.Layer` (data is
now final); tier-band + depth comments updated. No layout constants changed.

### 1d-update (2026-09-11): L1 uses its freed room â€” bigger nodes, wider gaps, closer to the roots
With L1 now a single row of 25/30, its ring band is repurposed for readability: L1 nodes grow
8x7 â†’ 12x10 and `layerPitch` L1 10 â†’ 16 (edge gap 2px â†’ ~4px; ring1 cap 52 â†’ 30, Magic/Fortitude
fill it exactly), label font bumps 7â†’8. `moatBase` 250 â†’ 220 pulls ring1 in to r=470 so Layer 0 and
Layer 1 sit closer. Derived ring caps rechecked: r1=470(30), r3=772(57), r4=952(70), r5=1132(84) â€”
all L2 bands (126-152) and lone L3 nodes still fit.

### 1e-update (2026-09-11): L2 gets real room â€” bigger wheel, two spacious rings, no catalog cut
Complaint: "layer 2 doesn't have enough space, more than half the skills got pushed up to layer 3".
Root cause: ring3 (r=772) held only 57 L2 nodes vs 126-152, so 55-62% poured onto rings 4-5 (read as
"layer 3"). No catalog shrink (Option C chosen): `moatBranch` 280 â†’ 550 and `deepStep` 180 â†’ 260.
ring3 r=1042 (cap 77), ring4 r=1302 (cap 96) â†’ every category's L2 fits 2 well-sized rings
(Magic 152 = 77+75, ring4 ~78% full), and true L3 (Tornado/Masterwork/Heart-Seeker) sits alone on
ring5 r=1562. L1 band untouched. New derived caps: r3=1042(77), r4=1302(96), r5=1562(116), r6=1822(135).

### 1f-update (2026-09-11): SHELVED â€” "2 synthetic grandchildren per L1" (skill cut)
Cut synthetic L2 grandkids 5 â†’ 2 per L1 and pitched L2 to 16 so the band fit one ring. User REJECTED
the skill reduction: "i dont want to reduce the amount of skills, i want to keep the amount as it is".
Full catalog restored (5/L1). SUPERSEDED by 1g. Kept only `layerPitch` L2 = 16 from this attempt.

### 1g-update (2026-09-11): L2 keeps ALL skills â€” balanced 3-ring band, no ring packed to the seams
Complaint: cut is not acceptable; L2 must keep 5 grandchildren/L1 (126-152 nodes/category, ~1034 total).
Physics: that volume can't sit on one ring, so instead of greedily filling the innermost ring until it
is 100% full, `CharacterInfoUI` allocator (CharacterInfoUI.cs:1142) now SPREADS dense bands: each L2
node picks the least-loaded ring of a 3-ring band (rings 4-6 = r=1092/1372/1652), growing the band only
if capacity demands. Every ring ends up ~50-73% full with 6px gaps instead of one seam-packed ring.
Radii: `moatBranch` 550 â†’ 600 (ring3 r=1092), `deepStep` 260 â†’ 280 (r4=1372, r5=1652). L2 caps at
16px pitch: 70/89/107 (Magic 152 = ~51 per ring). True L3 (Tornado/Masterwork/Heart-Seeker) sits singly
on ring6 r=1932 (cap 167). SkillCatalog.ExpandTree back to `ci < 5`; class doc ~1034 restored.
L0 (6 roots) and L1 (exactly 25/30 on ring1) untouched.

### 1h-update (2026-09-11): L2 collapsed to a SINGLE ring â€” verified against real content files
Re-check after the six SkillCatalog.{Category}.cs content files landed (960 new skills, ExpandTree now
produces layer counts 126/126/152/126/126/151): every L2 child is `Layer=2` and max authored depth is 3
(only Tornado/Masterwork/Heart-Seeker), so no L4 and no overflow exists in DATA. What read as "pushed to
layer 3/4" was the 1g 3-ring L2 spread (rings 3-5) â€” L2 nodes visually occupied two extra rings.
User chose single-ring L2. Changes in CharacterInfoUI.cs:
- `layerPitch` L2 16 â†’ 10; L2/L3 nodes 10x7 â†’ 8x6 (2px gaps), so one ring seats all 126-152 nodes.
- `moatBranch` 600 â†’ 998: ring3 r=1092 â†’ 1490 (cap 154 @ 10px â‰¥ Magic 152). ring4 r=1770 (cap 153) = L3.
- Allocator band hard-coded 3-ring for L2 â†’ `band = 1` (single ring; still grows outward only if a layer
  ever exceeds a ring's capacity).
Simulation (real counts): L2 = exactly ring3 for all 6 categories (126/126/152/126/126/151 â‰¤ 154),
L3 = exactly ring4 (Ranged/Magic/Crafting 1 each); wheel maxR shrinks 1972 â†’ ~1810.

---

## 2. OPEN TASK â€” axe chopping / pickaxe mining is silently broken (PRIORITY)

### Symptom (confirmed by user answers)
- Game runs, hotbar visible (Casual mode), axe tool selected.
- Left-click on a tree/rock: **no chop mark, tree never progresses/falls**, and **no Console errors**.
- Player earlier reported "chop/mining animation runs" but the follow-up answers supersede that:
  no ChopMark, no progress, no errors.

### What has been ruled out (verified by reading code)
- `git status` shows only 4 modified files: `SkillProfile.cs`, `RaceChangeManager.cs`,
  `PlayerStats.cs`, `CharacterInfoUI.cs` (+ new race/class skill files). Nothing else.
- The full gather pipeline is UNCHANGED and has NO dependency on skills/races/classes/stats:
  - Input: `PlayerController.cs:873` LMB (not FightingMode) â†’ `ToolManager.UseSelectedItem()`.
  - Tool paths: `ToolManager.cs:772-856` (axe) / 838-856 (pickaxe).
  - Stamina gate: `TryUseTool`/`SpendToolStamina` (ToolManager.cs:122-146) â€” only checks `player.Stamina`.
  - World: `WorldBuilder.TreeChop.cs` (`ChopTree`/`CutTree`, ChopMark creation at line 94) and
    `WorldBuilder.RockMining.cs` (`HitRock`, cracks at `UpdateRockCracks`), 4-hit completions.
- Registration: trees/rocks ARE added to `WorldBuilder._trees/_rocks` during `CreateWorld`
  (`WorldBuilder.WorldEnv.cs:407/437`, called from `WorldBuilder.cs:476-477`).
  `FindTreeRoot` (ToolManager.Pickup.cs:203) matches any parent named `Tree*`.
- Swing animation (`PlaySwing`, ToolManager.cs:274) only fires via `SpendToolStamina` AFTER a
  successful chop/mine â€” so if nothing ever progresses, the swing shouldn't be playing either.

### Why silent no-chop (ranked hypotheses for next session)
1. **Hit collider â†’ `FindTreeRoot`/`IsRock` mismatch or unregistered root.** If the tree/rock the
   player clicks was NOT created by `WorldBuilder.CreateWorld` (e.g., a static/MapBuilder-built
   decor tree in the active test scene, or a saved-game rebuild), `_trees.Contains(treeRoot)` is
   false â‡’ `ChopTree`/`HitRock` returns false on every click: no mark, no progress, no error. Very likely.
2. **Raycast never hits the tree collider** (layer mask / collider removed by `MeshCombiner` or
   colliderless prefab leaves). Script then never enters the axe/pickaxe branch.
3. `_worldBuilder` null in `ToolManager` (would throw though â€” user reports no errors, so unlikely).
4. A runtime exception elsewhere silently swallows the click via the pre-gate at line 723
   (`TryUseTool` false â‡’ "QuÃ¡ má»‡t!" toast â€” user would see the toast; they said no errors).

### Fix applied 2026-09-11 (lazy registration + chunk parent traversal) â€” needs Unity test
Root cause CONFIRMED: the new infinite-world path (`ChunkObject.SpawnProps`, `EnableLegacyGeneration
= false` default) spawns trees/rocks under `TerrainChunk_X_Z` and does NOT add them to
`WorldBuilder._trees`/`_rocks`. The raycast works fine (trunk/branch/rock cubes keep BoxColliders);
the game logic silently rejected the hit:
- Axe: `FindTreeRoot` finds `Tree_X_Z` (prefix match OK) but `ChopTree` -> `_trees.Contains` = false.
- Pickaxe: the rock walk-up stopped only on `"WorldRoot"`, so it walked PAST `Rock_X_Z` up to the
  `ChunkObject`, then `HitRock` -> `_rocks.Contains` = false.

Changes (code, not yet verified in Editor):
- `WorldBuilder.TreeChop.cs` â€” `ChopTree` (line ~30) and `RemoveTree` (line ~8): lazy-register the
  tree into `_trees` if missing instead of returning false.
- `WorldBuilder.RockMining.cs` â€” `HitRock` (line ~8) and `RemoveRock` (line ~421): same lazy
  registration into `_rocks`.
- `ToolManager.cs` â€” rock parent walk-up (pickaxe branch, line ~840) and debris walk-up (axe
  fallback, line ~825) now also stop when the parent is a `ChunkObject` (in addition to `"WorldRoot"`).

### Next session: verify in Unity
1. Open the scene the user tests in (infinite world / chunk path). Play, select axe, chop a tree:
   expect a black ChopMark after the first swing, trunk shrinks and a TreeFelled drops on the 4th hit.
2. Select pickaxe, mine a rock: expect cracks after each hit, rock shatters into RockDebris on the
   4th hit (debris is pick-up-able / smashable).
3. Also verify legacy path still works if `EnableLegacyGeneration` is ever toggled on.
4. If trees/rocks STILL silently do nothing, instrument with `Debug.Log` probes in
   `ToolManager.UseSelectedItem` (as previously planned) â€” now the most likely remaining cause would
   be the raycast itself missing (line 770), not registration.
5. Known minor leak (pre-existing): chunk-unloaded trees/rocks stay as stale null entries in
   `_trees`/`_rocks`; harmless to chopping (null-guarded) but inflates respawn counters.

---

## 5. Skill tree content design â€” continuation plan (2026-09-11)

### What's been done
- **SkillCatalog.cs refactored** to `public static partial class SkillCatalog` with:
  - `BranchSlot` class (IsAuthored, Id, Name, IsPassive, Cost, IsMagical, Kind, Effect, Desc).
  - `A(authoredId)` â€” shortcut for referencing an existing skill from Build*.
  - `S(id, name, effect, desc, cost, kind, magical, passive)` â€” shortcut for a new hand-written skill.
  - `DesignBank` (Dictionary L1[rootId] â†’ 5 BranchSlot[], Dictionary L2[parentId] â†’ 5 BranchSlot[]).
  - `_design` field + `Design` lazy property + `BuildDesignBank()` calling `Register*Design(bank)`.
  - **ExpandTree rewritten** to read from `Design.L1` / `Design.L2` tables instead of generating
    suffix names. Authored slots (A(...)) resolve existing skills by id; new slots (S(...)) get
    Add(...). Multi-root dedupe via `layer1Ids` preserved.
  - Old synthetic machinery removed: `_suffixesByType`, `_activeElements`, `_passiveStats`,
    `ScaledCost`, `ActiveCostForLayer`, `MakeChildEffect` all deleted.
- **Content files created 2026-09-11** â€” all 6 partial files now exist:
  `SkillCatalog.Melee.cs`, `SkillCatalog.Ranged.cs`, `SkillCatalog.Magic.cs`,
  `SkillCatalog.Stealth.cs`, `SkillCatalog.Crafting.cs`, `SkillCatalog.Fortitude.cs`.
  `Register*Design(bank)` methods are resolved; `BuildDesignBank()` compiles.
  VERIFIED (static sweep, no Unity compile): per-category L1/L2 tables are exact
  bijections (every L1 child has an L2 table, every L2 key is an L1 child),
  22 authored `A()` refs + 6Ã— L0 root keys all resolve to existing `Build*` ids,
  zero duplicate skill ids, 98 unique `Spell()` ids with no collision vs the 9 base spells.

### Architecture for content files
Each category gets its own file: `Assets/Scripts/Combat/Skills/SkillCatalog.{Category}.cs`.
Each file contains:
```csharp
partial class SkillCatalog
{
    private static void Register{Category}Design(DesignBank bank) { /* populate bank.L1 + bank.L2 */ }
}
```
Content uses `A()` and `S()` helpers (private static in the main partial), plus existing effect
helpers: `Buff(stat, amt)`, `Slash(power, kind)`, `Zone(radius, power, kind)`, `Spell(...)`,
`Stamina(amt)`, `Focus(amt)`, `P(ids)`.

### Node counts per category (new = hand-written)

| Category   | L0 | Auth L1 | New L1 | Total L1 | Auth L2+L3 | New L2 | Total L2 | Auth L3 | Total |
|------------|----|---------|--------|----------|------------|--------|----------|---------|-------|
| Melee      | 5  | 4       | 21     | 25       | 1 (execute)| 125    | 126      | 0       | 156   |
| Ranged     | 5  | 3       | 22     | 25       | 1 (arrowrain)| 125  | 126      | 1 (heartseeker) | 157 |
| Magic      | 6  | 5       | 25     | 30       | 2 (blizzard, gale) | 150 | 152 | 1 (tornado) | 189 |
| Stealth    | 5  | 4       | 21     | 25       | 1 (shadowstep)| 125  | 126      | 0       | 156   |
| Crafting   | 5  | 3       | 22     | 25       | 1 (transmute)| 125  | 126      | 1 (forge)| 157   |
| Fortitude  | 6  | 3       | 27     | 30       | 1 (wall)   | 150    | 151      | 0       | 187   |
| **Total**  | 32 | 22      | 138    | 160      | 7          | 800    | 807      | 3       | 1002  |

### Authored L1 skills (22) â€” already exist in Build*, placed via A() in tables

| Skill id            | Name              | Root (prereq)  | Type    |
|---------------------|-------------------|----------------|---------|
| melee_tough         | Tough Knuckles    | heavy_mastery  | passive |
| melee_whirlwind     | Whirlwind         | cleave         | active  |
| melee_berserk       | Berserk Slash     | cleave         | active  |
| melee_couter        | Counter Strike    | finesse        | active  |
| ranged_steady       | Steady Hands      | marksman       | passive |
| ranged_multishot    | Multishot         | pierce         | active  |
| ranged_iceshot      | Ice Shot          | flamearrow     | active  |
| magic_manaflow      | Mana Flow         | arcane         | passive |
| magic_chain         | Chain Lightning   | fireball       | active  |
| magic_heal          | Lesser Heal       | focus          | active  |
| magic_ward          | Arcane Ward       | arcane         | active  |
| magic_windblade     | Wind Blade        | gust           | active  |
| stealth_sneak       | Silent Steps      | reflexes       | passive |
| stealth_veil        | Veil of Night     | shadow         | passive |
| stealth_cloak       | Smoke Cloud       | nimble         | active  |
| stealth_assassinate | Assassinate       | backstab+fox   | active  |
| craft_purity        | Pure Materials    | hands          | passive |
| craft_refine        | Refinement        | knowledge      | passive |
| craft_repair        | Field Repair      | knowledge      | active  |
| fort_vitality       | Vitality          | health         | passive |
| fort_stamina        | Relentless        | armor          | passive |
| fort_steadfast      | Steadfast         | armor          | passive |

### Authored d2/d3 locks (10) â€” already exist, no table entries needed
- d2: melee_execute, ranged_arrowrain, magic_blizzard, magic_gale, stealth_shadowstep, craft_transmute, fort_wall
- d3: ranged_execute (Heart-Seeker), magic_tornado, craft_forge (Masterwork)

### Melee L1 design (25 slots â€” 4 authored + 21 new)

Root: melee_heavy_mastery (passive, Strength+3)
1. A(melee_tough)
2. S(melee_heavy_sunder, "Sunder", Slash(24, Physical), Stamina(14)) â€” "A blow that tears through armor."
3. S(melee_heavy_crag, "Crag Breaker", Slash(26, Earth), Stamina(16), Earth, true) â€” "A downward smash that cracks the ground."
4. S(melee_heavy_goliath, "Goliath Stance", Buff(Endurance, 3), passive) â€” "Permanent +3 Endurance."
5. S(melee_heavy_skullcrush, "Skullcrush", Zone(2f, 22f, Physical), Stamina(16)) â€” "A devastating overhead strike."

Root: melee_finesse (passive, Dexterity+3)
1. A(melee_couter)
2. S(melee_finesse_expose, "Expose Weakness", Slash(22, Physical), Stamina(12)) â€” "A surgical strike that finds the weak seam."
3. S(melee_finesse_flick, "Lightning Flick", Slash(26, Lightning), Stamina(14), Lightning, true) â€” "A blade flicker as fast as lightning."
4. S(melee_finesse_mirage, "Mirage Blade", Slash(24, Dark), Stamina(16), Dark, true) â€” "A feint that cuts from a shadow after-image."
5. S(melee_finesse_rhythm, "Blade Rhythm", Buff(Dexterity, 3), passive) â€” "Permanent +3 Dexterity."

Root: melee_cleave (active, Stamina 10, Slash 18 Physical)
1. A(melee_whirlwind)
2. A(melee_berserk)
3. S(melee_cleave_rending, "Rending Cleave", Slash(26, Physical), Stamina(16)) â€” "A cleave that bites deep and tears."
4. S(melee_cleave_ember, "Ember Sweep", Slash(28, Fire), Stamina(18), Fire, true) â€” "A cleave trailing a curtain of embers."
5. S(melee_cleave_tempest, "Tempest Cut", Zone(1.8f, 22f, Wind), Stamina(18), Wind, true) â€” "A sweeping cut that carries a storm."

Root: melee_lunge (active, Stamina 12, WeaponSkillEffect)
1. S(melee_lunge_piercer, "Piercer", Slash(20, Physical), Stamina(10)) â€” "A single lunging thrust aimed at vitals."
2. S(melee_lunge_bullrush, "Bull Rush", Slash(22, Physical), Stamina(14)) â€” "A lowered-shoulder lunge that bowls foes over."
3. S(melee_lunge_hotsteel, "Hot Steel", Slash(24, Fire), Stamina(16), Fire, true) â€” "A lunge searing the wound as it enters."
4. S(melee_lunge_shockjab, "Jab of Static", Slash(24, Lightning), Stamina(14), Lightning, true) â€” "A quick lunge crackling with static."
5. S(melee_lunge_longarm, "Long Arm", Slash(28, Ice), Stamina(18), Ice, true) â€” "An impossibly extended lunge chilling the target."

Root: melee_shieldbash (active, Stamina 14, Slash 22 Physical)
1. S(melee_shield_slam, "Shield Slam", Slash(24, Physical), Stamina(14)) â€” "A deafening full-body shield slam."
2. S(melee_shield_wallspike, "Spiked Wall", Zone(2f, 20f, Physical), Stamina(16)) â€” "A bristling shield line that lashes out."
3. S(melee_shield_sunwall, "Sunwall", Zone(2.2f, 24f, Holy), Stamina(18), Holy, true) â€” "A gleaming shield flare of holy light."
4. S(melee_shield_ironrip, "Iron Riposte", Slash(22, Physical), Stamina(14)) â€” "Brace and punish an enemy that hit you."
5. S(melee_shield_earthwarden, "Earthwarden", Zone(2f, 22f, Earth), Stamina(18), Earth, true) â€” "Strike the ground, sending rubble against foes."

### Melee L2 design (125 entries â€” 5 per L1 parent)

**melee_tough** children (all passive):
- Resolute Guard (+5 Def), Siegebreaker (+5 HP), Titan Plate (+5 End), Ironclad (+6 Def), Fortress Core (+5 Str)

**melee_heavy_sunder** children:
- Razor Sunder (Phys 28), Blazing Sunder (Fire 30), Frostbite Sunder (Ice 30), Rending Sunder (Phys Zone 28), Abyssal Sunder (Dark 34)

**melee_heavy_crag** children:
- Fissure Strike (Earth 30), Magma Crag (Fire Zone 28), Tremor Slam (Earth Zone 26), Obsidian Edge (Dark 32), Boulder Crush (Phys 30)

**melee_heavy_goliath** children (all passive):
- Resilience of Stone (+5 HP), Living Fortress (+5 Def), Molten Core (+5 Str), Iron Will (+5 End), Unbroken (+6 HP)

**melee_heavy_skullcrush** children:
- Skull Maul (Phys Zone 26), Volcanic Crash (Fire Zone 28), Quake Strike (Earth Zone 28), Dark Crush (Dark Zone 30), Boneshatter (Phys 32)

**melee_couter** children:
- Counter Flurry (Phys 28), Arcane Riposte (Arcane 30), Thunder Counter (Lightning 30), Viper Riposte (Phys 30), Shadow Counter (Dark 34)

**melee_finesse_expose** children:
- Sever Weakness (Phys 26), Ember Expose (Fire 28), Venom Expose (Dark 28), Rend Open (Phys 30), Void Slice (Arcane 32)

**melee_finesse_flick** children:
- Spark Flick (Lightning 30), Blur Strike (Wind 28), Tempest Flick (Wind 32), Frost Flick (Ice 32), Shadow Flick (Dark 34)

**melee_finesse_mirage** children:
- Phantom Strike (Dark 30), Echo Blade (Phys 28), Doppelganger (Arcane 30), Shade Cut (Dark 32), Mist Veil (Wind Zone 28)

**melee_finesse_rhythm** children (all passive):
- Blade Tempo (+5 Dex), Combat Grace (+5 Speed), Refined Reflex (+5 Dex), Fluid Motion (+5 AtkSpd), Absolute Precision (+5 Luck)

**melee_whirlwind** children:
- Fervor Spin (Wind Zone 24), Flame Vortex (Fire Zone 26), Frost Cyclone (Ice Zone 26), Razor Vortex (Phys Zone 22), Void Cyclone (Dark Zone 28)

**melee_berserk** children:
- Reckless Fury (Fire 30), Blood Frenzy (Phys 28), Searing Burn (Fire Zone 26), Berserker Rage (Dark 34), Berserker Storm (Wind Zone 28)

**melee_cleave_rending** children:
- Deep Rending (Phys 30), Flame Rend (Fire 32), Ice Rend (Ice 32), Storm Rend (Wind Zone 28), Void Rend (Dark 36)

**melee_cleave_ember** children:
- Ember Burst (Fire Zone 28), Magma Sweep (Fire Zone 30), Cinder Cleave (Fire 30), Inferno Arc (Fire Zone 32), Vapor Sweep (Water Zone 28)

**melee_cleave_tempest** children:
- Gale Cleave (Wind Zone 26), Squall Strike (Wind Zone 24), Hurricane Arc (Wind Zone 30), Thunder Sweep (Lightning Zone 28), Frost Sweep (Ice Zone 28)

**melee_lunge_piercer** children:
- Deep Pierce (Phys 24), Flame Thrust (Fire 26), Frost Thrust (Ice 26), Static Pierce (Lightning 28), Void Pierce (Dark 30)

**melee_lunge_bullrush** children:
- Tackle (Phys 26), Charging Bull (Earth 28), Blazing Charge (Fire 30), Frost Charge (Ice 30), Thunder Rush (Lightning 32)

**melee_lunge_hotsteel** children:
- Smoldering Steel (Fire 28), Infernal Lunge (Fire 30), Molten Jab (Fire 30), Volcanic Thrust (Earth 32), Searing Thrust (Fire 30)

**melee_lunge_shockjab** children:
- Spark Jab (Lightning 28), Bolt Lunge (Lightning 30), Arc Strike (Lightning 30), Storm Jab (Wind 32), Thunder Lunge (Lightning 34)

**melee_lunge_longarm** children:
- Glacial Reach (Ice 30), Frost Lance (Ice 28), Abyssal Reach (Dark 32), Void Reach (Dark 34), Static Reach (Lightning 30)

**melee_shield_slam** children:
- Aftershock Slam (Phys Zone 28), Flame Slam (Fire Zone 28), Frost Slam (Ice Zone 28), Thunder Slam (Lightning Zone 30), Earth Slam (Earth Zone 30)

**melee_shield_wallspike** children:
- Bristle Wall (Phys Zone 24), Blazing Wall (Fire Zone 26), Frost Wall (Ice Zone 26), Stone Wall (Earth Zone 28), Gale Wall (Wind Zone 26)

**melee_shield_sunwall** children:
- Radiant Wall (Holy Zone 28), Blessed Slam (Holy Zone 26), Hymn of Light (Holy Zone 30), Dawn's Shield (Holy Zone 32), Purifying Light (Holy Zone 28)

**melee_shield_ironrip** children:
- Rebound (Phys 26), Retribution (Holy 28), Vengeance (Dark 30), Reflect (Phys Zone 24), Guardian's Riposte (Holy 28)

**melee_shield_earthwarden** children:
- Tremor Stomp (Earth Zone 28), Lava Burst (Fire Zone 30), Frozen Earth (Ice Zone 30), Boulder Hurl (Phys Zone 26), Ore Slam (Earth Zone 30)

### Remaining categories â€” design approach (not yet drafted)

**Ranged** (5 roots: marksman, carry, pierce, quickshot, flamearrow)
- Auth L1: steady(marksman), multishot(pierce), iceshot(flamearrow). Auth d2: arrowrain[multishot]. Auth d3: heartseeker[arrowrain].
- 25 L1 + 125 L2 to design. Theme families: accuracy (marksman), speed (carry), piercing (pierce), rapid-fire (quickshot), elemental arrows (flamearrow).

**Magic** (6 roots: focus, arcane, fireball, frostbolt, dark, gust)
- Auth L1: manaflow(arcane), chain(fireball), heal(focus), ward(arcane), windblade(gust). Auth d2: blizzard[chain+frostbolt], gale[windblade]. Auth d3: tornado[gale].
- 30 L1 + 150 L2 to design. Theme families: FP/intelligence (focus), ward/utility (arcane), fire line, ice line, dark line, wind line.

**Stealth** (5 roots: shadow, reflexes, fox, nimble, backstab)
- Auth L1: sneak(reflexes), veil(shadow), cloak(nimble), assassinate(backstab+fox). Auth d2: shadowstep[veil].
- 25 L1 + 125 L2 to design. Theme families: darkness (shadow), agility (reflexes), trickery (fox), speed (nimble), stealth attacks (backstab).

**Crafting** (5 roots: hands, knowledge, focus, endurance, efficiency)
- Auth L1: purity(hands), refine(knowledge), repair(knowledge). Auth d2: transmute[purity]. Auth d3: forge[transmute] (Masterwork).
- 25 L1 + 125 L2 to design. Theme families: quality/luck (hands), recipes (knowledge), concentration (focus), stamina (endurance), speed (efficiency).

**Fortitude** (6 roots: health, armor, recovery, bulwark, stoneskin, guro)
- Auth L1: vitality(health), stamina(armor), steadfast(armor). Auth d2: wall[steadfast+stoneskin].
- 30 L1 + 150 L2 to design. Theme families: HP (health/recovery/bulwark), defense (armor), earth (stoneskin), grit (guro).

### Conventions to follow
- **Ids**: `{category}_{root}_{name}` for L1, `{l1_id}_{name}` for L2. Semantic, not numeric.
- **Names**: short, punchy (1-3 words). Match existing tone (Whirlwind, Berserk Slash, Execute).
- **Descriptions**: one sentence, flavor + mechanical fact. E.g., "A cleave that bites deep and tears."
- **Effects**: purposeful per skill, NOT element-swapped copies. Passive = `Buff(stat, amt)`.
  Active = `Slash(power, kind)` or `Zone(radius, power, kind)`. Power scales by depth:
  L1 ~1.3Ã— root power, L2 ~1.7Ã—. Costs via `Stamina(amt)` / `Focus(amt)`.
- **Passive roots can spawn active L1 branches** (and vice versa) â€” makes the tree varied.
- **Elements**: rotate through Physical/Fire/Ice/Lightning/Holy/Dark/Wind/Earth/Water/Arcane.
  Each L1's 5 children should cover ~3-5 different elements for variety.
- **Authored d2/d3 locks** (execute, arrowrain, etc.) are NOT in L2 tables â€” they're in the
  build list already and appear alongside designed L2 children via the depth walk.

### Execution order
1. âœ… SkillCatalog.cs refactored (partial, BranchSlot, DesignBank, table-driven ExpandTree)
2. âœ… SkillCatalog.Melee.cs (25 L1 + 125 L2 = 150 entries)
3. âœ… SkillCatalog.Ranged.cs (25 L1 + 125 L2 = 150 entries)
4. âœ… SkillCatalog.Magic.cs (30 L1 + 150 L2 = 180 entries)
5. âœ… SkillCatalog.Stealth.cs (25 L1 + 125 L2 = 150 entries)
6. âœ… SkillCatalog.Crafting.cs (25 L1 + 125 L2 = 150 entries)
7. âœ… SkillCatalog.Fortitude.cs (30 L1 + 150 L2 = 180 entries)
8. âœ… Sweep: unique ids, prereq resolution, per-category counts, no orphan refs
   (static grep verified 2026-09-11 â€” see "Content files created" note above)
9. ðŸ”² Update PROGRESS.md counts (left in table form â€” current table still matches), commit with fix:/ui: prefix

### Known issues
- ~~SkillCatalog.cs class doc says ~1034 but actual count is ~1002~~ â€” FIXED (class doc now says ~1002;
  the 1002 total = 6 cats: 156/157/189/156/157/187 = L0+L1+L2+L3 per 1c-update counts).
- Dev saves referencing old `*_b1..b5` ids will lose those unlocks (acceptable â€” full content redesign).
- No Unity compile available â€” verification is static (grep for id graph) + user eyeball.
- `Assets/unused script.md` remains untracked â€” do not commit.

---

## 3. Parked / not started
- **Unused-file cleanup** (analysis delivered, waiting on user decision â€” do NOT act without one):
  - 9 dead scripts (0 refs, GUID not in any scene/asset/prefab):
    `Combat/Effects/CombatAnimation.cs`, `Combat/Effects/RagdollEnabler.cs`, `Networking/Matchmaker.cs`,
    `Player/Controller/OpenWorldGrounding.cs`, `Player/Creation/CharacterCreation.cs`,
    `Player/Races/RaceDiscoveryPoint.cs`, `World/Housing/HousePlotPlacer.cs`,
    `World/Loot/WorldLootPlacement.cs`, `World/Npcs/EconomyProvider.cs`.
  - `Assets/_Recovery/` (16 unreferenced crash scenes), `Assets/TutorialInfo/` (template leftover),
    16 empty folders under `Assets/Scripts`, root dev artifacts
    (`__azurite_db_*.json(.meta)`, `AzuriteConfig`, `_queuestorage__/`, `xoanvnmexel.zip(.meta)`,
    `obj/`, `obj.meta`, `sound.meta`).
  - **DO NOT delete `_Archived/`** (its README says preserve; excluded from Unity build/csproj).
- Race skill tree: no point economy (auto-grant all) â€” by design for testing; revisit later.

---

## 4. Environment notes
- Windows, Unity project at `D:\unity\new world\new-world`. Shell is PowerShell 5.1 (no `&&`).
- Cannot compile/run Unity from this environment â€” verification is read-only code review only.
- CRLF warnings on `git diff` are cosmetic; do not "fix" line endings wholesale.
- There is an odd stray file `Assets/unused script.md` (untracked) â€” likely a leftover, unverified.
