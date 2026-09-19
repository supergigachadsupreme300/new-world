# THINKING — Working Reasoning Log

This file stores the **raw reasoning trail** behind tough investigations: every hypothesis
considered, the evidence for/against it, dead ends, and why the surviving candidates survived. It is
deliberately more verbose and tentative than the other docs.

Authority order (do not confuse these):
- `game-design.md` — the durable design of record (what the game IS).
- `PROGRESS.md` — the per-task handoff summary (what was done + status + play-test checklist).
- `THINKING.md` — **this file**: the messy "how I reasoned it out", including guesses that turned out
  wrong. Never cite a hypothesis here as if it were implemented behavior.

When an investigation closes, keep its section but mark the verdict (confirmed / rejected / fixed by
`1xx`), so the same wrong paths aren't walked twice.

---


## 1dd — "enemies explode like in the old game" + "add more enemy" (SHIPPED in `1dd`)

User: "add more enemy and every time the enemy die they explode like in the old game". Clarified via
questions: test platform only; both new types + more numbers; visual-only explosion.

### Step 1 — what was "the old game" explosion?
- Hypothesis A: the project already had a death-explosion somewhere live — rejected; grep for
  Explosion/explode/burst across `Assets\Scripts` found only spell/skill FX, no enemy death burst.
- Hypothesis B: the archived legacy runtime had it — **confirmed**. `_Archived\Enemies\
  EnemyController.cs:980 Die()` calls `ExplodeModel()` (line 1057): every model block detaches,
  gets `BoxCollider` + `Rigidbody` (mass 0.3), `AddForce(dir*8 + up*6, Impulse)` with
  `dir = (blockPos - center).normalized; dir.y += 0.5`, random torque, destroyed after 5 s. That is
  the exact "old game" feel.

### Step 2 — mirror it into the new EnemyController (live) vs. new FX file
- Considered a new `EnemyDeathFx` particle/chunk helper. Decided against inventing a new look — the
  user asked for "like the old game", so replicate `ExplodeModel` directly inside
  `EnemyController.Die()` (prefixed same method names). Verdict: mirror, keep the 0.2 s destroy tail.
  Added `using System.Collections` + `System.Collections.Generic` for the coroutine/list (the file
  previously fully-qualified `System.Collections.Generic.List` — the new using is additive, no clash).
- Perf sanity: a few blocks per enemy, 5 s lifetime, bounded by world budget (`EnemySpawner
  MaxLiveEnemies = 60`) and the platform's ~26 enemies. No pooling needed (matches old game, spell
  debris does the same).

### Step 3 — "more enemy" scope
- `NewWorldTestGround.SpawnEnemies` had 5 types. Roster (EnemyModelBuilder) has 20. Laid out all 20
  in two rows of 10 (6-unit spacing from `x = center-27`) + a 6-copy horde row for density.
- Geometry check (platform 120², center (0,50,0), half 60): rows span x −27..+27, z −40/−32/−24 —
  inside bounds. Dummies moved from `z=center-36` (would collide with new rows) to `z=center-18`;
  boss at `z=center-50.4` clear of row A (−40). Confirmed.

### Step 4 — doc references
- `game-design.md` §3.7 is "Damage & Status Types", NOT loot — avoided a wrong §3.7 citation for the
  loot-before-burst note (draft had it, removed).

---


## 1dc — "Church + taoist roofs upside down" (SHIPPED in `1dc`)

User: "you kinda got the roofs of church and taoist upsidedown". The gable/hip panels of the four
new roof blocks (church nave gable, church spire pitch, shrine hip, shrine tier-2 hip) read as an
inverted V — ridge low, eaves high.

### Step 1 — establish the ground truth (which roof was correct?)
- `Pagoda_Roof1` (Blueprints.cs:1144-1147) had been play-tested and looked right: the roof forms a
  **peak** — panels slope DOWN from the ridge (top center) toward each eave. So it is the reference
  for the sign convention.

### Step 2 — derive the convention from the reference
- Z-axis panel at **+z** (`Euler(+14,0,0)`): R_x(θ) moves a point's height by `sinθ * zOffset` — a
  panel centered ahead of the ridge needs +θ so its far (+z) edge dips LOW (eave) and its near edge
  stays up (ridge). Z-axis panel at **−z** mirrors it (`Euler(−14,0,0)`).
- X-axis panel at **+x** (`Euler(0,0,−14)`): R_z(θ) moves height by `sinθ * xOffset`: +x panel needs
  −θ so its far (+x) edge dips low. X-axis panel at **−x** mirrors (`Euler(0,0,+14)`).
- So: `z=+ → Euler(+θ)`, `z=− → Euler(−θ)`, `x=+ → Euler(0,0,−θ)`, `x=− → Euler(0,0,+θ)`.

### Step 3 — compare each rewritten block against the convention
- `Church_Roof` (1500-1501): both Z panels inverted (z=+ had −24, z=− had +24). **CONFIRMED** → swap.
- `Church_SpireRoof` (1554-1557): Z pitch panels inverted (z=− had +38, z=+ had −38) — swap; the X
  panels (1556-1557, `x=− → +38`, `x=+ → −38`) already matched. **CONFIRMED** → swap Z only.
- `Shrine_Roof` (1707-1710): Z panels already matched (z=−14/z=+14); X panels inverted (x=− had −14,
  x=+ had +14) — swap X only. **CONFIRMED**.
- `Shrine_Roof2` (1750-1753): same pattern — Z fine, X swapped (±18) — swap X only. **CONFIRMED**.

Total: 8 sign flips across the four blocks.

### Hypotheses considered
- **H1 — only the church is wrong.** REJECTED: user said "church and taoist", and both shrine hip
  roofs (tiers 1+2) had the same inverted X-panel signs.
- **H2 — all four panels per roof are wrong.** PARTIAL: each roof mixed one correct axis with one
  inverted axis; only the offending axis per block was flipped (Z for church nave+gable, X for
  shrine hips; spire Z). This is why per-axis comparison against the reference was the right call.
- **H3 — geometries changed as well.** REJECTED: only the `Quaternion.Euler` rotation arguments
  changed; positions/sizes untouched (save-compatible since part names/offsets are unchanged).

### Open/risk notes
- Rotation signs are baked into blueprint visuals; building-damage/state persistence only stores
  part transforms, not Euler args — no save/load impact expected.
- Other roof blocks (pagoda 1144-1147, well/house hips 1220-1223, 1284-1287, 1358-1361) were all
  re-checked against the convention and are correct — no collateral fixes.

## 1db — "Raise the tab button in tab menu height" (SHIPPED in `1db`)

Ask clarified to the Character Info top tab strip at ~1.3x.

### Why two size writes had to change (the trap)
- `BuildTopButtons` writes `sizeDelta.y = 64f * S` at build time (CharacterInfoUI.cs:365), but the
  aspect-fit pass `OnLayoutFitted` (line 478) re-writes it to `56f * S` — and that pass runs during
  `BuildPanels` via `RegisterFit → ApplyFits`, i.e. AFTER the buttons are built.
- Hypothesis: changing only the 64 in the builder would be enough. REJECTED on reread: the first
  RegisterFit immediately overwrites the height to 56, and every window resize after that re-applies
  56 too. To be effective at runtime, BOTH writes must match. Confirmed by grepping both call sites
  to the same `84f * S`.
- Height target: 64 × 1.3 ≈ 84, chosen over a round 80 so the "1.3x" ask is met honestly.

### Why buttons grow UP, not down
- `BodyRow` top edge sits 60 u below the panel top (`offsetMax.y = -60`, MenuPanelBase.cs:142);
  tabs (children of `PanelRect`, top-anchored) must not cover it. Tab bottom is currently at −50
  (`y=6, h=56`) / −48 (`y=16, h=64`), leaving ~10-12px clearance.
- Naive height bump (keep y) would push the bottom down to −78 → overlap content by ~18px.
  Solution: raise `anchoredPosition.y` by the same delta as the height (+20 / +28) so the bar grows
  UPWARD and the bottom edge stays at its old spot. Verified position arithmetic on reread.

### Open/risk notes
- Label uses anchorMin/Max 0..1 with ±8f offsets → stretches with the taller button. No other code
  reads the tab bar geometry (`_tabButtonRects` is only used inside these two methods).
- On a very short window the fit clamps scale ≥ 0.25 but the tab bar derives its width from
  `availW * 0.8f` and keeps 84 height design units — pre-existing behavior, unchanged.

## 1da — "Ground AoE can't be placed far away" (SHIPPED in `1da`)

User report (paraphrased): the outdoor AoE spells can only be placed within their short spell
`Range`, so you can't drop a zone/summon/storm far across the world — the open-world game wants
long-distance placement.

### Root-cause trace (what actually capped the ground target)
- `SpellCaster.Execute` (SpellCaster.cs:264) builds the aim direction: `aim = cam.position +
  cam.forward * Mathf.Max(spell.Range, 5f)` — so the aim RAY was pinned to a point only
  `spell.Range` out. Ground deliveries then received `spell.Range * mods.RangeMult` as their delivery
  `range` (old lines 281/283/287/289 → now 296/298/302/304).
- The ground-target resolvers honor that `range` as the probe length: `ResolveZone` →
  `TerrainDeformer.ResolveGroundTarget(pos, fwd, range)` (line 48), `ResolveSummon`/`ResolveStorm` →
  `GroundTarget(pos, fwd, range)` (line 328), `SpawnVortex` → its own raycast (line 394). So even if
  the player points far away, the landing point was clamped to the spell's short range.
- The preview mirrored the same: `PlayerController.TryAoeTarget` (PlayerController.cs:1711) projected
  the landing ring at `Mathf.Max(spell.Range, 5f)` and raycast `Mathf.Max(spell.Range, 0.1f)` —
  preview and landing agreed, both short. That agreement is why the bug read as "can't aim far"
  rather than "preview lies".

### Hypotheses & decision
- **H1 — keep the aim pinned to spell.Range but scale it up.** REJECTED: spell `Range` is a delivery
  property (≈12-30 on the earth/sky zones); multiplying it is a hack with no principled value.
- **H2 — unbounded (= float.MaxValue) probe.** REJECTED for the fallback branch: `aimHit.point`
  misses → `pos + fwd * range` would hand positions at literal float infinity to the resolvers /
  preview ring. `float.MaxValue` math invites NaN/inf edge cases in squares/distance checks later.
- **H3 — a large fixed practical cap (1200) for GROUND deliveries only. [ADOPTED]** One constant
  `SpellCaster.GroundAimMax = 1200f`, used for the aim direction AND the four ground resolvers'
  range; preview mirrors the constant. Finite, safe in squared-distance math, and > any visible
  world distance (map ~20-chunk render ≈ 600 m). Projectile/instant/beam deliveries keep
  `Mathf.Max(spell.Range, 5f)` and `spell.Range * mods.RangeMult` untouched.
- Scope guard: only the four GROUND deliveries (Zone/Vortex/Summon/Storm) — `FireProjectile`,
  `ResolveDirect` (instant), `ResolveBeam` and `SpellBeam.Length` still use spell.Range, and
  `SpellEffect` flight caps are untouched. Projectile spells keep their normal shot range; only AoE
  ground placement is opened up.

### Open/risk notes
- `TerrainDeformer.ResolveGroundTarget` walks up to 4 skip iterations over RAISED terrain while
  consuming `remaining`; with a 1200 probe the rest of the algorithm is unchanged (same skip loop,
  now just more ray budget). No new failure mode observed on reread.
- The ground-down probe in all paths stays fixed at 30 u (`Vector3.up * 0.1f` then down 30 u); a
  target on a very steep far face could land slightly below the hit surface, but that pre-existed and
  is unchanged.
- Verification is grep + reread only (rule 3 — no build). Confirm by searching the two call sites.

### Play-test checklist (user verifies in Unity)
Try aiming a Zone/Storm/Summon/Vortex well past the old spell Range; the preview ring should land far
away and the delivery should resolve there; confirm projectiles/instants/beams still behave exactly
as before.

## 1cz — "make the church and shrine as big and detailed as the pagoda" (SHIPPED in `1cz`)

### Context
User asked for the church and taoist shrine to match the pagoda in size and detail. Measured gap
before the change: pagoda = 15 parts, 14×14, ~21 tall, ~430-line builder; church = 7 parts, 13×9,
~9.6 tall, ~65-line builder; shrine = 6 parts, 10×8.5, ~6 tall, ~65-line builder.

### Scope decisions (asked user; answers recorded)
1. Match pagoda scale (~14-16 wide, ~20 tall)? → CHOSE match-pagoda scale.
2. Steeple style → CHOSE a single tall front steeple (gothic, not a twin-tower or a separate topknot).
3. Shrine design → CHOSE a two-tier pagoda-style hall (main tiled roof + an upper tier roof) topped
   by a jewelled gold spire.

### Design hypotheses
- **H1 - keep part names vs rename.** CHOSE keep every existing part name and add new ones. Save/load
  restores `structure_part_*` by name generically (`LoadBuildingsFromSave`); no gameplay code matches
  part names except the two builders' switch. Renaming would break old saves for no gain.
- **H2 - part granularity.** CHOSE 13 church / 12 shrine parts. Enough to separate the vertical stack
  (foundation → interior → roof → steeple) so each cube-stack reads clearly, while staying in the
  same order of magnitude as the pagoda's 15 so the structure loop cost doesn't blow up.
- **H3 - NPC anchor orientation.** CONFIRMED by rotation math: `BuildChurch` rotates every part +90°
  about Y, so the new church's local -Z front (steps/tower door) faces world **-X (west)**; shrine
  rotation is 0, so its front faces world **-Z (south)**. The priest therefore moved from south of the
  church to **west** in front of the steps (rot 90→facing +X), and the taoist moved further south
  (-40,-33.4) in front of the widened steps and now faces the shrine (rot 0). Follow-up correction
  while landing `1cz`: after shifting the church's foundation from a 9-deep to a 13-deep slab and
  adding a full-width front porch + 4 descending steps (apron z -9.8..-6.6, landing to z≈-12), the
  priest anchor moved out to (27.5,-30) so he stands at the foot of the steps, not on the apron cube
  (the original 31.6 point landed inside the new porch volume). Test platform mirrored: priest
  `(cx+17.5, cz-35)` at the foot of the west steps (church center cx+30).
- **H4 - window/roof idiom.** Reused the pagoda's rotated-cube idiom (`CreatePartCubeRotated`) for
  eaves; used the same rotation trick at ±35° to build the gothic pointed arch heads (chevron), which
  is the one non-pagoda motif the design needed.
- **H5 - pruning radius.** `ChurchExcludeHalf`/`ShrineExcludeHalf` raised to 15/14 so the tree/rock
  pruning box clears the grown footprints (16×13 and 14×12 → half-extents ~8 and ~7-7.2).

### Evidence / verification (grep, no build — rule 3)
- Every new part name present in BOTH `_churchSubBuildings`/`_shrineSubBuildings` AND its switch case
  (25/25 via grep; see PROGRESS.md `1cz`). No legacy name dropped, so save-restore stays intact.
- New part names are only referenced in WorldBuilder.cs (array) + WorldBuilder.Blueprints.cs (switch);
  no other consumer strings (CharacterInfoUI, save/restore) refer to them.
- `IsFloorType` only matches `wood_floor`/`stone_floor`, so the new props can't pollute floor spawns.

### Dead ends / decisions NOT taken
- Did NOT add a side "village clock tower" or twin towers to the church (user picked single steeple).
- Did NOT make the shrine a literal 2nd pagoda — kept the deity statue + altar + censer interior so it
  stays a worship hall rather than a clone.
- Did NOT touch the pagoda or the legacy village layout.

### Open items
- Play-test: visual overlap of newly widened steps vs NPC box-collider walks; confirm pruning radius
  covers the grown footprints (esp. church east flank at x=48 now inside the 15 box) and the west
  priest placement reads as "in front of the door".

### Context
Player read the sky-spell descriptions ("a burning meteor falls from the sky", "a colossal mass of
burning rock that levels everything") and noticed the cast only flashed a ring on the ground — no rock
was ever summoned. Wanted the effect to match the tooltip.

### Scope decision (asked user)
- Asked: which spells → "All sky/rock spells (Recommended)": Fire Meteor, Asteroid, Earth Meteor
  (Zone); Comet (Projectile); Meteor Rain + Rockfall (Storm).
- Asked: damage timing → "Delay damage until landing": the rock should visibly fall and the burst
  resolves on impact, not instantly (a cast-time-driven beam of drama for the meteor family).

### Design hypotheses
- **H1 - data-driven flag vs hard-coded skill ids.** CHOSE the flag (`SpellData.SummonFallingRock`).
  The catalog already builds the whole spell via the `Spell(...)` factory; a new optional
  `summonFallingRock:` arg is the established pattern (`terrainShape:`/`projectileShape:`) and avoids
  scattering skill-id strings through damage paths. Scorch explicitly NOT flagged (jet of light, not a
  rock) even though it shares the Comet projectile shape.
- **H2 - defer via coroutine in SpellCaster vs a self-driving FX component.** CHOSE the component.
  `ResolveZone` is a pure method called from `Execute` (synchronous); making it start a coroutine
  breaks the return-value contract. Instead: `SkillFx.FallRock` owns a `RockDrop` MonoBehaviour that
  animates the fall and fires the `onImpact` callback. `ResolveZone` extracted the burst body into
  `ResolveZoneImpact` which the normal path calls synchronously and the sky path calls from the
  landing callback (guarded by `if (this == null) return`). Return-value check: `Execute`'s result
  only feeds `OnCastComplete`, which has **zero subscribers** — returning an early DamageResult for
  sky zones is harmless (verified by grep).
- **H3 - storm cadence change risk.** `SpellStorm.StrikeDelayed` already delays each strike by a
  random 0-0.35 s; adding the rock's fall only shifts the flash/damage/deform to landing time. Rocks
  are spawned per strike (~2/tick × ticks), each short-lived and self-destroying, so no leak like the
  old lightning-bolt leak (which the comment explicitly warns about). No pooled allocation needed at
  this scale (a storm spawns single-digit rocks per tick; `StrikeFlash` is already pooled).
- **H4 - rock must never have a collider.** CONFIRMED constraint: the whole point of 1cx was that
  collider-grazing + knockback teleported the shared Terrain root. `FallRock` cubes strip their
  collider (`CubeChild`), like every other FX primitive in the codebase. Damage/knockback/deform are
  ONLY produced by the spell's own overlap/raycast pipeline inside the landing callback.

### Verified read-only before committing (per rule 3)
- `OnCastComplete` — only the declaration (SpellCaster.cs:62) + invoke (246); **no subscribers**.
- `Execute` switch routes Zone → `ResolveZone` (line 281); Projectile → `FireProjectile` (279) which
  calls `AttachDefaultProjectileVisual(go, type, shape)` (484) — threaded `summonFallingRock` through.
- `SpellStorm.StrikeDelayed` (SpellStorm.cs:78) → `SpawnStrikeFx`/`ResolveStrike`/`DeformGround`;
  `RandomStrikePoint` returns world points (enemy pos or ±radius random), so `FallRock` lands there.
- `Comet()` builder + `BuildProjectileBody` switch on shape — added `rockBody` without touching
  Scorch/Burn/Frost Bite (they keep the light core).
- SkillFx already has `RingFader`/`SlashFader` self-contained faders; `FallRock` mirrors them.

### Play-test checklist (user verifies in Unity)
Cast all six flagged spells; confirm the rock drops and the burst lands with it, Scorch still streaks
as light, and no terrain moves (rock has no collider).


## 1cx — The "whole terrain moves" bug was NEVER the deformer: knockback teleports the shared Terrain root (FIXED in `1cx`)

Player (Earth Wall report, verbatim): "when using earth wall it cause the same error, investigate the
root cause of this". Clarified symptom: "when im using skill, the scene camera litteraly see the
entire terrain move". This forced a rethink — the 1cv/1cw deform-width theory did NOT fit.

### Hypotheses & evidence
- **H1 - the deformer width was still leaking (1cv/1cw follow-up).** REJECTED as THE cause. Three
  facts broke the width theory: (a) the width is now capped for every shape (SpellCaster.cs:999-1001),
  (b) the very FIRST report was **Asteroid** (`magic_fireball_meteor_astroid`, SkillCatalog.Magic.cs:
  277), which carries **no `terrainShape:` arg** so `TerrainDeformer.Apply` no-ops — it cannot have
  moved terrain, yet the user saw it move, and (c) 1cv/1cw shipped and the user still saw the bug with
  Earth Wall. The width fixes corrected a genuine independent bug (charged shapes repaint a huge dish)
  but were a red herring for THIS symptom.
- **H2 - the camera does the moving (shake/FOV/dolly).** INVESTIGATED, REJECTED. `ScreenShake`
  (MaxAmplitude 0.15, child "CameraShakeRig" targeting), `CombatFeedback` (0.5 shake, HitStop),
  `ThirdPersonCamera`/`CameraModeSwitch`/`CameraFollow` (smooth damp footsteps, no cast-time snap).
  None can translate the whole visible world by meters. The shake note even documents that follow
  cameras overwrite the root — so shake is a tiny child-rig jitter, not terrain motion.
- **H3 - the CHAR cast pushes the player (depenetration from re-cooked chunk colliders / overlap).
  PARTIAL. `PlayerController.EnforcePhysicsSanity` only reverts launches ≥150 m, so a moderate
  physics depenetration could yank the player (and therefore the pivoted camera). BUT the symptom
  ALSO showed for Asteroid with zero chunk recook, and the camera pivot rides the player — a player
  launch alone doesn't make "the scene camera see the terrain move". Kept as an open edge case, not
  the driver.
- **H4 - ApplyKnockback teleports the shared Terrain root [THE WINNER].** CONFIRMED. `ResolveZone`
  (SpellCaster.cs:1017) queries `Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer)` with
  the default mask and calls `ApplyHit` on each collider. `ApplyHit` feeds `ApplyKnockback`
  (SpellCaster.cs:1205-1213): `Transform root = target.transform.root; root.position += dir.normalized
  * spell.Knockback`. Every streamed terrain chunk is parented under ONE `"Terrain"` root
  (`Terrain` → `Chunks` → `TerrainChunk_x_z`, WorldStreamer.cs:447-471), so ALL chunks share that root.
  A zone spell overlapping chunk MeshColliders moves the entire world's root by `Knockback` per chunk
  caught (charge-scaled radius → several chunks → big one-frame lurch) — the exact "entire terrain
  moved" read from a player-pivoted camera. Explains Every report: Asteroid (knockback 3, no deform),
  Earth Wall (knockback 3.5), Meteor (knockback 4). Real victims are `IDamageable` (EnemyController /
  BossController / SummonedAlly); terrain/props are not.
- **H5 - the fix could live in ApplyKnockback (guard root).** Option DESIGNED, merged into H4 fix:
  guard earlier in `ApplyHit` so ALL spell damage machinery (DamageNumber, status, knockback) skips
  non-`IDamageable` targets — a single point covering ResolveZone, `ResolveDirect`, and every
  `ResolveHitAt` caller. The other delivery components already pre-filter (`SpellZone`/`SpellStorm`/
  `SpellTornado`/`SpellBeam` loop-check `IDamageable`; `SpellEffect` uses `HitLayers`), so nothing
  legitimate is blocked — grep confirmed.

### Verdict
CONFIRMED (H4 + H1-rejected). Fix (`SpellCaster.cs:1076-1081`): at the top of `ApplyHit`, return an
empty `DamageResult` unless `target` or `target.transform.root` implements `IDamageable`. Overcharge
size/radius mechanic untouched (player wants no caps). Same-pass: PROGRESS.md `1cx`. Verified by
grep+reread only (rule 3); Unity play-test should confirm Asteroid/Earth Wall/Meteor no longer lurch
the world while enemies still take knockback. Legacy 1cv/1cw marks this trail's earlier theory as
rejected (regarding THIS symptom) — that history stays in the `1cv`/`1cw` sections.

---

## 1cv  - Crater dishes stay a LOCAL bowl: width bounded, depth UNBOUNDED (SHIPPED in `1cv`)

Player (play-test): "when i use asteroid skill the entire chunk moving for some reason, or could be
the entire terrain." Follow-up (verbatim): "i want no limit on my game / no limit."

### Hypotheses & evidence
- **H1 - Earth Meteor's crater fed the FULL blast splash straight into the deformer, so one cast
  lowered every corner inside the whole 30-tile ChunkSize dish at the same time.** CONFIRMED.
  `SpellCaster.ResolveZone` built `radius = spell.Radius * sizeScale` (Earth Meteor `Radius` 9 m,
  up to ~13+ tiles with charge `sizeScale`) and handed that whole radius to
  `TerrainDeformer.Apply(center, radius, Crater, fwd)` (SpellCaster.cs:986). `TerrainDeformer`
  lowers **every** corner inside `reach = radius + feather`, so a radius of 9-13+ tiles dropped the
  whole chunk at once - read as "the entire chunk / the entire terrain moved." This is the driver.
- **H2 - the whole chunk streams/reloads or the chunk teleports.** REJECTED. Each tile's height
  lives in the mutable terrain stream state (WorldStreamer) and is only addressed by fixed chunk
  coords; the heights themselves are the thing deformed. Nothing relocates a chunk.
- **H3 - the player wanted a depth/global cap (games usually clamp).** REJECTED - the player's "i
  want no limit on my game / no limit" is explicit, so DEPTH must stay unbounded (`CraterStep`
  ratchets ~1.1 m deeper per cast with NO floor, per 1cs) and the fix must live on WIDTH.

### Verdict
CONFIRMED (H1). Fix (`SpellCaster.cs:995-998`): bound the crater **WIDTH** to the spell's small
local delivery dish (`DeliveryRadius * 0.5`, ~2 m for Earth Meteor, floor-clamped to 0.5 m) instead
of the blast splash; **DEPTH stays unbounded** - each cast still ratchets `CraterStep` deeper with
no floor cap (per the player's "no limit"). Same-pass: game-design.md 3.8 (width bounded / depth
keeps no floor) + PROGRESS.md `1cv` + THINKING.md. No new QA lane (existing crater deform lane on
NewWorldTestGround covers the shape).

---

## 1cw — AoE *still* reshaped the whole chunk: raised shapes + projectiles fed the charge-scaled blast radius (SHIPPED in `1cw`)

Player (follow-up, verbatim): "still that bug where if player using an aoe skill it would move the
entire terrain." This arrived right after `1cv` fixed the Meteor crater, so the same whole-chunk
symptom survived on a DIFFERENT path.

### Hypotheses & evidence
- **H1 - `1cv` only bounded `Crater`; every RAISED shape still fed `radius = spell.Radius * sizeScale`
  into the deformer, and `sizeScale` is unbounded (overcharge).** CONFIRMED. In
  `SpellCaster.ResolveZone` the fix was `deformRadius = shape == Crater ? dish*0.5 : radius`, so
  Tremor (Ring), Spire Field (Spikes), Earth Wall (Wall), Landslide (Wall), Stone Pillars (Pillar)
  still passed the charge-scaled blast radius (SpellCaster.cs:997). `SpellChargeLevel` has no upper
  clamp (PlayerController.cs:1501 returns `(t-0.15)/(1.2-0.15)` unbounded), so a player who holds
  RMB can drive `sizeScale = 1 + charge*1.2` to 3.4+ before FP drains - a 3 m-delivery raise becomes
  a ~10 m+ raise spanning a whole chunk. Death-march match: any AoE skill (not just crater).
- **H2 - the projectile dent also scaled by charge.** CONFIRMED. `SpellEffect.ResolveProjectileImpact`
  did `dentRadius * _radiusMult` where `_radiusMult = sizeScale`; charged Stone Shard (Earth, Crater
  dent `max(1.2, Radius)` = 1.2 m) carves `1.2*3.4 ≈ 4 m+`. Same width-leak family.
- **H3 - Storm/Summon were also leaking.** REJECTED. `SpellStorm.DeformGround` uses
  `max(Radius*0.55, 1.2)` (no sizeScale) and Summon uses `min(Radius*0.4, 2.5)` — both already
  dish-capped. Grep confirmed no other `TerrainDeformer.Apply` call sites (ToolManager Dig, test
  lanes fixed radii).

### Verdict
CONFIRMED (H1+H2). Fix: **width-capped for EVERY shape**, charge still scales the damage splash
(`radius`/`zoneRadius` untouched) but never the ground edit.
- `SpellCaster.cs:999-1001`: `dish = max(Radius>0 ? Radius : 1.6f, 0.5f)`;
  `deformRadius = Crater ? dish*0.5f : dish`.
- `SpellEffect.cs:262-270`: dent `max(1.2, Radius)` (Earth) / 1.4 m (others), `_radiusMult` dropped —
  also restores the doc's "fixed ~1.4 m non-Earth dent" to literal truth.
Crater depth remains unbounded (player's no-limit rule). Same-pass: game-design.md §3.8 (width bounded
for every shape incl. raised, `1cw`) + PROGRESS.md `1cw` + THINKING.md. Verify by grep+reread only
(rule 3); Unity play-test should confirm a **charged** raise/AoE stays a local dish.

---

## 1cu — Player HUD status strip under the bars (SHIPPED in `1cu`)

User asked: "add a show status function that show player the status effect that they're having under
their bars." Raw trail:

### Hypotheses & evidence
- **H1 — there is a dedicated player status tracker/manager to hook into.** REJECTED. Exploring
  `Assets/Scripts/Combat/Effects/` showed statuses are **stapled to the target's root**:
  `SpellDoT` (Burn/Poison/Rot/Bleed DoT), `WetStatus`, `BlindStatus`, `ChillStatus` (build gauge).
  No registry, no manager — each is a root `MonoBehaviour` with its own timer. So the HUD must
  poll `player.transform.root` for the components; no event bus exists or is needed.
- **H2 — remaining-time is uniformly readable.** MOSTLY REJECTED: only `SpellDoT.Remaining` was
  public. `WetStatus`/`BlindStatus` hide `_expiresAt` behind a private field and `Update()` destroys
  on expiry. Decision: add a tiny additive `Remaining` property to each (reads `_expiresAt -
  Time.time`), keeping behaviour identical. `ChillStatus` has NO timer at all — it's a decaying
  integer gauge decaying 1/sec that either converts to a Frost slow or self-destroys; a full freeze
  (`IsFrozen`) flips `_cold=0; Destroy(this)` on the next `Update`, so "FROZEN" literally cannot be
  displayed from this component. Display the gauge (`CHILL n/5`) instead.
- **H3 — the food/drink stamina buff is displayable from existing fields.** The user picked
  "combat statuses + food/drink buff". `ToolManager` calls
  `PlayerController.ApplyStaminaRegenModifier(0.5f | 1.2f, 120f)`; the expiry was already stored as
  `_staminaRegenModifierUntil` (used in `HandleStamina`). Added read-only `StaminaBuffRemaining` +
  `HasStaminaBuff` — pure exposure, no gameplay change. Sign/pct derived from the public
  `StaminaRegenModifier` field.
- **H4 — one bulletproof rendering style.** User chose "colored square + text" over text-only chips.
  No icon assets exist anywhere, so a plain `Image` colored per status + centered `TextMeshProUGUI`
  is the honest minimum; a `GridLayoutGroup` (4 fixed columns, 82×24 cells) wraps to a second row and
  the panel height is resized to fit the row count. Pool of 10 chips is enough (max ~6 concurrent
  statuses today); chips update only when color/text actually change (mirrors the bars' label
  caching), so the strip never allocates per frame in steady state.
- **H5 — QA lane ordering matters.** `WetStatus.Apply` actively *douses* an active Burn
  (`SpellDoT.RemoveType(Fire)`), so the demo applies Wet FIRST, then Burn — otherwise the DoT is
  cancelled instantly. Chill twice while wet gives +2+2=4 stacks (below the 5-threshold the "freeze"
  path would destroy the component); a third stack would throw it into the invisible frozen/self-
  destroy state, so the demo stays at 4/5 to keep the gauge visible while it decays. Placement is
  direct-to-player (no `PlatformTopY` geometry, so the rule-4 lane convention is trivially obeyed).
- **Verdict:** H1 rejected (no tracker — poll components), H2 fix (add `Remaining`), H3 confirmed
  (buff already timed), H4 style as above, H5 ordering confirmed. Feature shipped in `1cu`;
  verification by source review (rule 3), play-test listed in `PROGRESS.md ## 1cu-status`.

---

## 1ct — Play-test fixes: pink terrain (1cs shader) + casual fighting pose (1cr revert) (SHIPPED in `1ct`)

### VERDICT
Two play-test bugs, two root causes, both confirmed by source review (no build, rule 3):

1. **Pink terrain** = `TerrainLayered.shader` got **rejected entirely**: its ForwardLit vertex called
   `GetVertexNormalInputs(input.normalOS, input.normalOS)` — URP 17.5 has no `(float3, float3)`
   overload, only `(float3)` and `(float3, float4 tangentOS)` (`ShaderVariablesFunctions.hlsl:22,31`)
   → HLSL compile error → subshader fails → Unity SRP shows magenta (the shader's URP-Lit `FallBack`
   is **not** used under URP). Trees/rocks looked fine because they keep their own working URP Lit
   materials — only the ground material was broken. Fix: single-arg `GetVertexNormalInputs(input.normalOS)`.
2. **Fighting pose in normal mode** = `1cr`'s `WeaponsDrawn => FightingMode || firstPerson`
   (`PlayerController.cs:1861`) kept the weapon drawn at port arms in every first-person frame,
   casual included. Fix (confirmed with user: "draw only while fighting"): `WeaponsDrawn => FightingMode`.

### Hypotheses & evidence
- **H1 — pink terrain is a shader *name/path* problem (e.g. material lost the shader on reload).**
  REJECTED. `GameBootstrap` assigns the layered shader to the ground material by name at boot; a
  missing/dropped shader reference would pink EVERYTHING on the ground renderer, which it did — but
  the giveaway was that trees/rocks (plain URP Lit) were unaffected, so the ground's **own material**
  was at fault, i.e. its shader failing to compile (not a shared/global shader that all materials
  reference). Compile failure under SRP => magenta.
- **H2 — the pink is a missing *keyword* (fog / shadows) tricking URP into an error branch.**
  REJECTED after reading `Core.hlsl`/`Lighting.hlsl` include chain: keywords select variants, they
  don't produce magenta. Magenta requires the shader itself to be uncompiled/rejected.
- **H3 — the exact HLSL error is the `GetVertexNormalInputs(f3,f3)` call.** CONFIRMED (compile error
  in the strict sense — no such overload). Audited every other API call against the 17.5 package:
  `UniversalFragmentPBR(InputData, SurfaceData)` exists exactly at `Lighting.hlsl:302` with matching
  field names; `TransformWorldToShadowCoord` lives in `Shadows.hlsl:356` (reachable via
  `Lighting→RealtimeLights→Shadows` and `Lighting→GlobalIllumination→SphericalHarmonics→Shadows`);
  `MixFog`/`ComputeFogFactor`/`GetVertexPositionInputs` fine; ShadowCaster's `_LightDirection`,
  `_LightPosition`, `ApplyShadowBias`, `_CASTING_PUNCTUAL_LIGHT_SHADOW`, `UNITY_REVERSED_Z` all match
  the URP 17.5 `ShadowCasterPass.hlsl`. Only the overload was wrong. The tangent overload needed a
  `float4` tangent — the terrain has no tangent stream and no normal map, so the single-`float3`
  overload is exactly right.
  - Side-check: shader references `half4 _Color; float _UseVertexColor;` at file scope without a
    `CBUFFER UnityPerMaterial` — legal (plain uniforms), just not SRP-batcher friendly; not the bug.
- **H4 — the fighting pose is a `/pose` console command or an animation state leak.**
  REJECTED. Grep found no pose command; the pose is entirely driven by `WeaponsDrawn` →
  `ReApplyWeaponPose` → `WeaponRigBuilder.ApplyPose(gameObject, draw)`. `ApplyPose` (`WeaponRigBuilder.cs:363`)
  is binary draw/stow (`WeaponStowAnimator.Snap/SetPose`) — there is NO neutral-carry pose, so the
  only ways to kill a port-arm look in casual are (a) stow (what the user chose) or (b) build a whole
  new "carry" pose API (rejected: not what the user asked for).
- **H5 — boot/test-ground drew the weapon regardless of `WeaponsDrawn`.**
  PARTLY TRUE: `NewWorldTestGround.SpawnAllWeapons` called `pc.ReApplyWeaponPose(instant: true)` at
  spawn with a comment claiming first-person visibility — under the 1cr rule that DREW the weapon at
  boot. With the revert, the same call now STOWS at boot (single code path) and the stale comment was
  rewritten. No extra toggle exists.
- **H6 — `WeaponsDrawn` revert is safe for all *hand-visibility* consumers.** CONFIRMED by grep:
  `CharacterInfoUI` (2613, 3612) only uses it to pose the preview after equip/cycle (stowed while
  casual is correct); `CameraModeSwitch.SetMode`'s `ReApplyWeaponPose` becomes a harmless no-op when
  casual; `LoadPlayerModel` re-rig + respawn re-pose flow through the same property. The arms/hands
  themselves stay visible in first person (that's `CameraModeSwitch` layer logic, untouched) — the
  player still sees their hands, just not a raised weapon.

### Dead ends & gotchas
- **FallBack is a red herring for SRP:** `FallBack "Universal Render Pipeline/Lit"` in the subshader
  is ignored under URP — don't rely on it to save a broken pass. The only fix path is a valid
  subshader.
- **`VertexNormalInputs` vs `TransformObjectToWorldNormal`:** I considered dropping the struct and
  just normalizing `TransformObjectToWorldNormal(input.normalOS)` — equivalent for a no-tangent
  surface, but the struct form is the URP-idiomatic one and shares the code path the working */
  /* URP-content objects use, so I kept `GetVertexNormalInputs(f3)`.
- **Doc drift:** `PROGRESS`'s `1cr` entry and `THINKING`'s 1cr verdict describe keep-drawn-in-first-
  person as shipped behavior — 1ct explicitly **reverses** that decision per play-test feedback;
  `game-design.md` §3.6 and §5.5 were rewritten in the same pass so no doc still claims the old rule.

### Follow-up: ShadowCaster `_LightDirection` undeclared (fixed in 1ct second commit)
- **New evidence after the first 1ct fix:** ForwardLit now compiles, but the ShadowCaster pass failed
  with `undeclared identifier '_LightDirection' at TerrainLayered.shader(145)`. My 1ct H3 audit was
  wrong in one detail: I confirmed the *names* `_LightDirection`/`_LightPosition`/`ApplyShadowBias`
  exist in URP 17.5's `Shaders/ShadowCasterPass.hlsl` — but those two `float3` globals are declared
  **inside that pass-utility file** (`ShadowCasterPass.hlsl:13-14`), NOT in `Shadows.hlsl` (which only
  defines `ApplyShadowBias`/`ApplyNormalBias` functions and shadow-matrix helpers). Since the custom
  caster includes only `Core.hlsl` + `Shadows.hlsl`, the variables were never in scope. **CONFIRMED:
  URP declares them itself only in its own inclue-able shadow-caster file `ShadowCasterPass.hlsl`.
  Fix: declare `float3 _LightDirection; float3 _LightPosition;` in the caster's `HLSLPROGRAM` block,
  byte-for-byte matching URP's own declaration** (verified again by reading `ShadowCasterPass.hlsl:13-14`
  right before the fix). Lesson: when a URP pass-support file declares its uniforms in the body of
  that file (not in a `ShaderLibrary/*.hlsl`), a custom shader must duplicate those declarations.

### Follow-up: still black after the GetShadowCoord fix → shadow-free explicit Lambert (fixed in 1ct fourth commit)
- **Symptom:** terrain AND test-ground platform both black; trees/rocks/player fine; the
  GetShadowCoord change changed nothing.
- **H1 — screen-space shadows.** REJECTED (again, harder): read `Assets/Settings/PC_Renderer.asset`
  — the Forward renderer has no screen-space-shadow feature (only SSAO, `m_ShadowTransparentReceive`),
  so `_MAIN_LIGHT_SHADOWS_SCREEN` can't be the active variant. The whole 1ct-third-commit theory
  was chasing the wrong keyword set. Also read `MainLightRealtimeShadow` (17.5 Shadows.hlsl): when
  `MAIN_LIGHT_CALCULATE_SHADOWS` is not defined it returns `half(1.0)` — full light — so a missing/
  garbage shadow coord CANNOT zero the direct term in this pipeline. Direct light was never the bug.
- **H2 — the platform is black because of vertex colors.** CONFIRMED: `NewWorldTestGround.BuildTestGround`
  builds the top as `GameObject.CreatePrimitive(PrimitiveType.Cube)` and assigns `streamer.GroundMaterial`
  — the layered shader with `_UseVertexColor = 1`. A stock cube has no COLOR channel → vertexColor
  reads (0,0,0,0) → `albedo = _Color * black = black` → black cube (URP Lit grass earlier worked
  because Lit ignores vertex colors). Fix: `PlatformMaterial` returns a plain URP Lit `GrassGreen`
  material instead (a platform has no strata bands anyway).
- **H3 — the TERRAIN is black from the same missing-color path.** REJECTED: `BuildMergedMeshData`
  fills `Colors` (ChunkMeshGenerator.cs:470) and `CreateMeshFromMerged` uploads via
  `mesh.SetColors` (line 596-597); chunk load/rebuild paths both verified to include colors. Terrain
  albedo is genuinely green on the GPU — yet output still black.
- **H4 — `UniversalFragmentPBR`/`InputData`/`SurfaceData`/BRDF plumbing outputs ~0 for this
  hand-rolled input regardless of light.** ACCEPTED as the working theory (with H2 kept for the
  platform): after three build rounds the remaining moving part is exactly that machinery, and it
  cannot be audited to zero-guarantee from the package in reasonable time. **Decision (user's call:
  "fuck the shadow"): delete it.** The ForwardLit now shades with three source-verified functions
  that are individually trivially correct — `GetMainLight()` (RealtimeLights.hlsl:89,
  shadowAttenuation = 1, no shadow data needed), `LightingLambert(half3,half3,half3)` (Lighting.hlsl:32,
  `color * saturate(dot)`), `SampleSHVertex(half3)` (GlobalIllumination.hlsl:45, sky ambient) — plus
  `ComputeFogFactor`/`MixFog`. `direct = albedo*LightingLambert(sun...);` is positive whenever the
  scene's main directional sun exists (it does: SampleScene.unity "Directional Light", intensity 2),
  so a fully black terrain becomes impossible. Cost: the terrain no longer samples realtime shadow
  maps (still casts via ShadowCaster). Re-add shadows later on a known-good base if needed.
- **Confirmed verdict:** H4 + H2. Fixed in 1ct fourth commit; `PROGRESS` notes it. Play-test
  checklist: terrain shows lit green strata (day), sun side brighter than sky side, fog at distance,
  digging exposes dirt/stone bands; platform top + legs render grass/brown respectively.

---

## 1cs — Infinite digging depth + terrain strata (grass → dirt → stone) (SHIPPED in `1cs`)

### VERDICT
Digging now goes infinitely deep (bounded only by the ±200 m mesh-sanity band) and the terrain is
vertex-colored at mesh-build time into discrete strata bands (grass → dirt → stone) derived from
`pristine noise at the corner − current vertex Y`. Craters ratchet a fixed `CraterStep` down per
cast/swing (deliberately NOT idempotent anymore), while the raised shapes keep their idempotent
`Max`-cap. No save-format or hash change — the colors are derived, never stored.

### Hypotheses & evidence
- **H1 — store a per-corner "layer" value in the save.** REJECTED. Would change `ChunkTileMod`'s
  4-float payload, the `tc_*.dat` format version/hash, `AntiCheat`/`ChunkSync` hashing and every
  save-format doc/const. Too wide a blast radius for a cosmetic read. The save already stores each
  corner's CURRENT height; depth below the *pristine* surface is the only extra datum needed, and it
  is recomputable from the seed (deterministic).
- **H2 — derive band from height alone (absolute Y).** REJECTED. Terrain rolls over ~±60 m; banding by
  absolute Y would paint whole mountains dirt/stone and river-beds grass regardless of excavation.
  Depth below the local noise surface is the only self-consistent measure ("how far am I below where
  this corner was born").
- **H3 — depth from a saved "original height" copy in memory only.** REJECTED after closer look: on
  first load after an edit, pristine height doesn't exist in memory for a loaded chunk (only the
  saved deformed corner + the noise formula). Since the noise is pure-deterministic function of the
  seed + corner coords, sampling `GetHeight(seed, cx, cz)` directly is both simpler and always
  correct, for pristine, deformed, and raised corners alike. **CONFIRMED as the implementation.**
- **H4 — idempotent "grind" is fine (revert the 1cm crater clamp, keep everything else).**
  **CONFIRMED by user.** The user explicitly wants digging to be able to go arbitrarily deep, which
  is the exact inverse of the `1cm` "crater floors clamp at noise − 1.8" idempotency. Decided to keep
  the raised shapes idempotent AND make craters compounding again (the pre-1cm behavior) — a clean
  split, and the `1cm`/`1cj` fixes that matter (no whole-chunk rise, smooth per-corner profiles,
  mesh atomicity, no flat slabs) are untouched. The old cap code (`CraterMaxDepth`) and its XML
  claims were fully removed; stale prose in `DeformAt`'s doc and `game-design.md`/`magic-skills.md`
  was rewritten in the same pass so no doc still claims craters clamp.
- **H5 — band boundaries as soft blends vs hard cuts.** User asked for "discrete strata bands" — small
  blends (0.3 m) keep the band transitions readable as layers without hard maché seams, per-corner at
  the SAME coords used for the height, so the color field is watertight across tile edges
  (neighbours share corners → identical colors — same contract that keeps the mesh gapless).
  **CONFIRMED.**

### Dead ends & gotchas
- **Rename drift:** the first implementation named the band constants `*BlendStart/BlendEnd`, then the
  public rename to `*BandStart/BandEnd` left `TerrainBandColor`'s body referencing the old names (a
  compile error caught by grep before doc pass). Fixed + grep-verified no `BlendStart`/`CraterMaxDepth`
  remain in `Assets/Scripts`.
- **Wall-pass color origin:** wall vertices are chunk-LOCAL `(ex, ez)`; their world corner is
  `tiles[0].Coord` (the chunk's min-tile world coord) + `(ex, ez)`. The defensive `tiles` fill in
  `BuildMergedMeshData` guarantees `tiles[0]` non-null before the color pass, so no NRE. Both
  `BuildMergedMeshData` call sites build the 900-array in the same (lx, lz) local order — cross-checked
  the two call sites in `WorldStreamer.cs:407` and `:879`.
- **`PatchRegion` must pass the seed too:** the newly-added seed parameter demanded a call-site update
  that the project can't compiler-check — grep-verified exactly 2 `PatchRegion` refs (definition +
  `WorldStreamer.cs:1120`), both 6-arg after the edit.
- **Crater rim "digs sideways":** repeating a cast at the same center ratchets the rim corners down a
  hair too (small `s`), slowly WIDENING the pit as it deepens. Accepted — reads as a natural bowl and
  matches "repeat casts keep digging"; flagged in the play-test checklist, not a bug.
- **Shader is the unverifiable risk:** no build is run (rule 3); `TerrainLayered.shader` is a hand-
  written URP ForwardLit (PBR + fog) + ShadowCaster + DepthOnly. If it fails to compile under URP 17.5
  the terrain goes magenta/pink in play-test and the fix is a shader compile pass, NOT the C# code.
  Fallback chain (`Shader.Find` → URP Lit → white base) is intentionally boring.
- **Tool gating uses the same math:** `shovel` gate compares `DigDepthAt(hit.point)` against
  `StoneBandEnd` (2.7); the pit floor at the hit point is interpolated across its corner heights and
  noise, and the floored integer corner sample is a faithful proxy for the band the tool is digging
  in. `Dig` shares the Crater shape so tool pits and spell craters stay one code path.

---

## 1cr — Weapon not visible on hand (held in inventory slots) (RESOLVED — shipped in `1cr`)

### VERDICT
Equipped weapons were stowed onto body anchors (`StowBack`/`StowWaist`) whenever NOT fighting; in
first person those anchors are behind the head-mounted camera, so the weapon could never appear on
screen. Fixed by making "visually drawn" = `FightingMode || firstPerson`, re-applied on combat
toggles, camera switches (F5), model rebuilds, and equip. Only third-person casual sheathes.

### Hypotheses & evidence
- **H1 — a weapon id is missing a `WeaponModelBuilder.Build` case → null visual.** REJECTED. Checked
  every catalog id vs the dispatch; all 18 weapons (incl. the 3 shields) have builders.
- **H2 — weapon renderers land on a culled layer in first person.** REJECTED. `CameraModeSwitch`
  culling only clears the body bit (layer 6); rigs stay on Default (layer 0), visible in both modes.
- **H3 — attach/hand-bone lookup fails.** REJECTED. `FindHand` resolves `PlayerModel/Torso/ShoulderX/
  …/HandX` for the standing model and `ReparentToHands` re-seats park-fallback rigs after rebuilds.
- **H4 — the weapon IS stowed, but the stow anchors are positioned behind the first-person camera.**
  **CONFIRMED.** `ToggleCombatMode` casual → `ApplyPose(draw:false)` → `WeaponStowAnimator` moves the
  rig to `StowBack`/`StowWaist` under Torso; the first-person camera looks forward from the head pivot
  and never sees behind the body. Fix: draw whenever first person.
- **H5 — stow/draw could stay per-combat-only if the user prefers.** User chose "Show in hand when
  first person" over "always in hand / drop stow" and "keep stow everywhere" — so sheathing only in
  third-person casual was the accepted target.

### Implementation notes
- Central source of truth: `PlayerController.WeaponsDrawn` + `ReApplyWeaponPose`.
- Combat toggles pass `instant:false` to keep the existing draw/stow *animation*; model rebuild /
  camera-switch equip-snap use `instant:true`. `CameraModeSwitch.SetMode` invoking the player's
  re-pose is safe during Awake because `ReApplyWeaponPose` no-ops without a `CombatController`.

---

## 1cq — Translucent Wind/Ice + denser Fire projectiles (RESOLVED — shipped in `1cq`)

### VERDICT
Wind/Ice bodies made translucent by passing a lower alpha through the existing `"Sprites/Default"`
material; Fire made "hotter" by switching the sphere to `OrbFx.Ember` flicker and roughly doubling
the ember exhaust (`EmissionRate`/`MaxParticles` up, `StartSize` up). No new shaders or assets.

### Hypotheses & evidence
- **H1 — translucency needs a shader change.** REJECTED. `Sprites/Default` (the projectile body
  shader) already blends with `SrcAlpha/OneMinusSrcAlpha`, proven by existing semi-transparent
  visuals: `CCZone` (alpha 0.4 disc), `CastingCircle`, `AoeAimPreview`, `ProjectilePathPreview` —
  all just set `material.color.a`. So the fix is purely data: give the color alpha.
- **H2 — set alpha in `DamageNumber.ColorFor`.** REJECTED — it drives ~30 call sites (bodies,
  particles, UI, skill FX across many files). Overriding alpha inside the two builders keeps Wind/
  Ice translucent without tinting damage popups or other effects.
- **H3 — the `Unlit/Color` fallback would kill transparency.** Confirmed finding, not a fix here:
  `Materialize` picks `Sprites/Default` first and only falls back to the opaque `Unlit/Color` if the
  former is missing (it never is in practice). Recorded as a doc caveat, matching how the rest of
  the codebase (RingFader, CastingCircle) already relies on this.
- **H4 — Ice/Lance share geometry?** Ice's *Lance* shape (Ice Lance/Frost Pierce, explicit `Lance`
  builder) is a separate, solid spike — kept opaque. Only the Auto `Shard` frost chip becomes
  glassy, which matches "ice magic should be transparent" for the generic chip while named lances
  keep their heft.
- **H5 — fire = bump just one number.** REJECTED — a single bump reads as a minor density change.
  Combined three levers (rate ×1.67, cap ×1.75 for sustained flight, size ×1.33) + a flickery body
  gives the obvious "more fiery" read. Kept the existing additive `Particles/Additive` exhaust and
  the gradient fade (rates seen as alpha/brightness of the glow).
- **H6 — Comet/Scorch/Burn also need the boost.** Already Ember-flickered AND have their own dense
  `Comet(...)` body; exhaust boost applies automatically (per-element tuning is shared by all fire
  delivery visuals). No per-shape change needed.

### Known limits (noted, not fixed)
- Translucency is Z-write-off sprite blending: two projectiles crossing can overdraw, but projectiles
  are transient and fast — acceptable, same as friendly/enemy cast circles today.

---

## 1cp — Earth magic projectile as rock debris (RESOLVED — shipped in `1cp`)

### VERDICT (read this first)
Confirmed approach: a **new `ProjectileShape.Debris`** (tumbling grey rock-clump, dressed like
`WorldBuilder.SpawnRockDebris`) wired to the Earth school + an **impact debris burst** out of the
crater. Trail below records the dead ends checked.

### Hypotheses & evidence
- **H1 — swap the existing `Shard` builder to rock chunks.** REJECTED. `Shard` is Ice's `Auto` shape
  too (`SpellCaster.AutoShapeFor(Ice) → Shard`), so editing it silently reskins frost chips. Instead a
  new enum value keeps Ice's diamond and gives Earth its own builder.
- **H2 — change only `AutoShapeFor(Earth)`.** INSUFFICIENT on its own. The Earth school's only
  projectile spell sets `projectileShape: ProjectileShape.Shard` **explicitly** on its `Spell(...)`
  line, so it bypasses Auto resolution. Confirmed via `SpellCastEffect Spell(...)` helper: the
  `projectileShape` param lands straight on `spell.Shape`. Fix = flip that one line AND the Auto
  default (for summoned-turret earth shots `SpellSummon.DecorateProjectile`, which pass `Auto`).
- **H3 — reuse the world debris pieces themselves.** The world chunks have `Rigidbody`/colliders and
  are interactable by the pickaxe/pickup system (`SmashDebris`, name `RockDebris`); reusing them as
  renderer-only projectile chunks risks the flight raycast self-hitting or pickups grabbing a
  fleeting chunk. The projectile keeps **renderer-only** primitives (matching the other shapes'
  "no collider" contract) but borrows the exact color formula.
- **H4 — tumbling = single Y spin (existing OrbFx path).** REJECTED for the chunks: a shared Y-spin
  looks like a drill, not debris. Added `Mode.Tumble` with a per-object `Random.onUnitSphere` axis
  captured in `Start()`; existing modes keep the old `Rotate(0, spin, 0)` branch untouched.
- **H5 — impact burst reuses the in-flight cluster builder.** REJECTED. The flight body is a static
  cluster parented to the projectile; impact needs independent physics chunks that fall out of the
  crater. Small throwaway `SpawnImpactDebris` (3-5 cubes, up-bias scatter, `Destroy` after 2.5 s),
  mirroring SpawnRockDebris' mass/velocity values.
- **H6 — could the burst chunks collide with the just-carved crater collider?** Terrain collider
  rebuilds once per deformer apply (1ck made the swap atomic) — the physics chunks spawn above the
  impact point and fall freely; the crater floor is solid walkable terrain. No special handling.

### Known limitation (noted, not fixed)
The StarEffigy/Golem **summon** spells (Stone Effigy/Sentinel/Guardian/Colossus) fire rocks via
`SpellSummon` with `Auto` shape → they now read as the same Debris cluster. Consistent, intended;
not a regression.

---

## 1co — Race look on the block player model (RESOLVED — shipped in `1co`)

### VERDICT (read this first)
Confirmed approach: **palette + body ratios on the shared block model**, race resolved from the
model's parent at build time, uniform RigScale raised for the giants, and a model rebuild on race
change. Walking list of hypotheses below is the trail.

### Hypotheses & evidence
- **H1 — tint the block model via `RaceRig.RigTint`.** REJECTED. One flat tint hits every renderer:
  eye whites go dark, hair/clothes/pants/shoes can't differ, and RigTint is inherently uniform per
  race. `RaceRig.ApplyRace` therefore keeps *only* the uniform scale and the prefab-branch tint.
- **H2 — swap a per-race prefab at build.** REJECTED for now. Needs authored `.asset` bodies; nothing
  exists yet. `RigPrefab` stays as the documented later path (drop-in, no code change).
- **H3 — per-race palette + ratio data on `RaceData`.** CONFIRMED (chosen). 6 colors + 6 knobs with
  Human defaults = original colors / 1, so Human output is a strict no-op (checked `ApplyRaceLook`
  exits when all ratios == 1 and the colors fall back to the same constexprs).
- **H4 — rebuild the model on race change.** CONFIRMED as a gap: `OnActiveRaceChanged`
  (RaceChangeManager.cs:125) had **zero subscribers**; `LoadPlayerModel` runs on Awake/ApplyGender
  only. Fix: idempotent subscription in `LoadPlayerModel` guarded by `_raceSubscribed`. Dead end I
  avoided: subscribing in Awake only — respawn/reparent paths that reload the model may drop the
  subscription; guarding inside `LoadPlayerModel` covers every reload.
- **H5 — `ActiveRace` allocates 22 ScriptableObjects per get.** CONFIRMED (calls `BuildDefaultRoster`).
  Fix: static cached `RaceDatabase.DefaultRoster`. (RebuildIndex still runs the serialized `Races` on
  OnEnable — unchanged.)
- **H6 — cutscene models.** CONFIRMED via call-site trace: `BuildSeatedPlayerModel` parents the model
  to the **car**, which has no `RaceChangeManager` → always Human. `null`-parent fallback also covers
  any other host; the seated model built off the player root (`PlayerSitController`) *is* race-aware.
- **H7 — head counter-scale.** The standing model's Hair/Eyes/Neck live under the **Torso pivot**, not
  the root, so a root-only scale would leave them embedded after Height/Bulk/RigScale → must recurse
  and counter-scale names Head/Neck/Eye*/Hair*/Ponytail* by `(Head/Bulk, Head/Height, Head/Bulk)`.
  Dead end checked: relying on RigScale only would look correct until the first Height/Bulk knob — so
  ratios are authored on top of the uniform scale, ratios decide the *look*.
- **H8 — foot calibration.** Standing leg chain: Hip at -0.25, Thigh/Shin/Shoe bottom ≈ -0.62 → foot
  bottom = `root.y − 0.25 − 0.62`; after scaling root by h and legs by leg the bottom sits at
  `root.y − 0.25*h − 0.62*leg`; setting `root.y = h*(0.25 + 0.62*leg)` re-lands it at 0 (Human ≈ 0.87
  vs 0.86 — 1 cm, absorbed by boots). Seated/sit models skip calibration (their pose isn't ground
  planted; a bottom-heavier sit for short-legged races is acceptable).
- **H9 — raise vs lengthen giants.** User chose **raise** (global `RigScale`, also enlarges the
  hitbox for "feel"). Goblin/Gnome scale unchanged (15% smaller hitbox is a real contract in §3.5).

### Known limitation (noted, not fixed)
`SaveManager` has no race field (grep of save/restore paths found none) — a saved game loads as Human
until the player re-picks a race. Pre-existing behavior; remains out of scope.

---

## 1cm — Earth Wall repeat cast "makes the entire chunk moving" (RESOLVED — fix shipped in `1cm`)

### VERDICT (read this first — the trail below is the *before* picture)
Confirmed root cause: **`DeformAt` was additive, not idempotent.** Raise was `current + s*lift`
(capped at `noise+lift`) and crater `max(current − s*1.8, noise−1.8)`. Because `current` already held
the previous cast's raise, a repeat cast added the raise **again**, lifting the whole influence
footprint toward the cap on each of the first several casts (the low-influence flanks included), so
the ground rose across a wide swath of the chunk on cast #2+ → "the entire chunk moving". Craters
compounded identically (deeper each cast).

**My "provable no-op" claim below (H1) was WRONG.** The cap bounds the *final* height but does not
make the operation idempotent; the additive form still compounds up to that cap. The tell I missed:
`DeformAt`'s own comment admitted "repeat casts (which stack the ridge on the previous height)".
`1bo` bounded the height, not the compounding.

Fix shipped (`1cm`):
- `DeformAt` uses absolute per-corner targets — raise `Mathf.Max(current, noise + s*lift)`, crater
  `Mathf.Min(current, noise − s*depth)` → idempotent.
- H7 addressed: `TerrainDeformer.ResolveGroundTarget` skips a forward hit that is the chunk's OWN
  `ChunkObject` terrain collider **and** sits above pristine noise + 0.25 m (a reared wall), so a
  repeat cast targets the intended ground rather than the wall face. (NOTE: the wall is the terrain
  `MeshCollider` itself — there is no separate wall collider/layer, which is why the "ignore a layer"
  idea below was unworkable.)
- H8 addressed: `ChunkObject.ApplyMerged` re-points filter + collider at the new mesh before
  destroying the old one. (`PatchRegion`'s `null → assign` is a single synchronous call; physics
  never observes the null, so it was left as-is with a comment.)

The hypotheses below are kept as the raw trail; see each for its final status.

### The report
> "when cast earthwall it only work the first time and the next time it make the entire chunk moving"

Repro facts gathered from the user (3 clarifying questions):
- **Deterministic** — happens on **every** cast after the first, never intermittent.
- **Same spot, standing close** — the second cast targets roughly the same spot as the first, and the
  player is standing near where the first wall came up.
- **"Moving" is not pinned down** — the user can't say for sure whether it's the player being
  shoved/sinking, the chunk's surface re-meshing, or the chunk relocating. So the investigation has
  to be robust to all three readings.

### Prior related history (from PROGRESS.md)
This is the third report in the same symptom family, so start from what earlier fixes did:
- `1bn` — Earth Wall fix: taller blocking wall + no more player "teleport" on repeat casts (added the
  caster keep-out ring, `keepOutR ≈ 0.9`, so raised shapes can't grow directly under the capsule).
- `1bo` — Earth Wall repeat casts no longer "shrink the world": raised shapes are **height-capped**
  (`newHeights = current + s*lift` then clamp), so they can't stack unbounded.
- `1cl` — per-corner caps (no flat plateaus) + tops-first merged mesh.

So the naive "it stacks taller each cast and launches you" cause was already fixed by `1bo`, and the
"flat plateau re-arms legacy walls" cause by `1cl`. This new report survives both, which is a strong
hint the remaining cause is NOT in the height math at all.

### Mental model of the cast pipeline (walked end-to-end)
1. `SpellCaster.ResolveZone` (`SpellCaster.cs:922-961`): ray from caster along aim → `aimHit`;
   then a down-probe from `aimHit.point` → `groundHit` → `center = groundHit.point`.
2. `TerrainDeformer.Apply(center, radius, Wall, fwd)` → `WorldStreamer.DeformAt`.
3. `DeformAt` (`WorldStreamer.cs:704-833`): builds the Wall ridge influence over an AABB of world
   corner coords, computes `newHeights[corner]`, calls `ApplyHeightEdits`.
4. `ApplyHeightEdits` (`1028-1066`): writes the 4 corners into every loaded tile in the AABB, bumps
   `Version`, `MarkDirty`, then per touched chunk `RebuildChunkRegion` + `FlushDirtyChunk`.
5. `RebuildChunkRegion` (`1075-1115`): if `ChunkContainsFlatTile` OR region ≥ 75% area → full rebuild;
   else build just the region and `PatchRegion` the existing merged mesh.
6. `ChunkObject.PatchRegion` (`76-135`) / `ApplyMerged` (`51-75`): `SetVertices`/`SetNormals`/UV,
   `mesh.bounds`, then recook the `MeshCollider` (`_mc.sharedMesh = null; _mc.sharedMesh = mesh`).

### Hypotheses and verdicts

**H1 — heights stack/grind on repeat (dirty heights).** **WRONG — this turned out to BE the bug.**
(Originally mislabeled "REJECTED".) `DeformAt` computed `current + s*lift` (capped at `noise + lift`)
for raises and `max(current − s*1.8, noise − 1.8)` for craters. The clamp bounds the *final* height but
does **not** make the operation idempotent: `current` already includes cast #1's raise, so each repeat
cast adds it again and compounds toward the cap — lifting the whole influence footprint (low-influence
flanks included) and reading as the entire chunk's ground rising. Craters compounded identically.
`DeformAt`'s own comment admitted "stack the ridge on the previous height"; I read past it. Fixed by
switching to absolute per-corner targets with `Mathf.Max`/`Mathf.Min`.

**H2 — coordinate/frame mismatch (corners vs tiles, chunk origin).** REJECTED.
Checked `ChunkData.Size = 1f`, `TerrainChunkCoord.ChunkSize = 30`, `FromTile` `FloorToInt`, chunk
origin `(tc.X*30, 0, tc.Z*30)`, and `DeformAt` corner ints. All agree; negative coords handled. A
mismatch here could have shifted a whole region, so it was worth ruling out, but it's clean.

**H3 — `PatchRegion` writes into wall-band vertex slots (interleaved layout).** REJECTED (fixed by 1cl).
That was exactly `1cl` root cause B. `BuildMergedMeshData` is now tops-first (all 4×900 top verts
contiguous, walls last), so `PatchRegion`'s fixed `(lz*cs + lx)*4` offsets are always valid. Confirmed
by reread (`ChunkMeshGenerator.cs:285-314` pass 2, `316-403` pass 3) and no other consumer of
`_merged.Vertices` exists.

**H4 — something literally translates the chunk transform.** REJECTED.
Grepped for `transform.position`/`SetParent` in `Assets/Scripts/World`; `CreateChunkGameObject` sets
the chunk position once at `(tc.X*cs, 0, tc.Z*cs)` and nothing moves/reparents a `ChunkObject` after.
`ChunkObject.Release` only destroys props + mesh. So "the chunk moved" cannot be a literal transform
move.

**H5 — the wall crest reads as a flat tile, flipping `ChunkContainsFlatTile` → full rebuild.**
REJECTED. `IsFlatTile` needs all 4 corners within `0.001f`. A ridge tile's corners are
`noise(corner) + 2.6` with genuine per-corner noise slope, so they are not flat. (This was the
leading theory for a while and it's attractive because it would explain "every second+ cast" — cast
#1 creates the state that flips #2 — but the math doesn't support it.)

**H6 — the repeat cast's region crosses the 75%-of-chunk threshold → full rebuild.** REJECTED for the
plain case. Default Earth Wall radius 3.6; charged max `sizeScale ≈ 1.8` → radius ≈ 6.5, reach
≈ 7 → AABB ≈ 13×13 = 169 tiles, far below `0.75 × 900 = 675`. Would only matter if heavily charged
AND near a chunk edge; user says same spot every time, so not the default explanation.

**H7 — the second cast's aim ray hits the first wall's collider. ADDRESSED (real, but a secondary
contributor).** `ResolveZone` did a plain `Physics.Raycast(pos, fwd, range)` with no layer filtering,
so after cast #1 a repeat cast's `aimHit` could land on the 2.6 m ridge and leave `center` on/near the
wall instead of the intended ground — moving the deform + collider recook. There is no separate wall
collider/layer to filter (the wall IS the chunk's terrain `MeshCollider`), so the fix is geometric:
`TerrainDeformer.ResolveGroundTarget` now skips a forward hit only when it is the chunk's OWN
`ChunkObject` collider **and** sits above pristine noise + 0.25 m (a reared shape), then re-probes;
craters/ground/entities/props are never skipped. `ResolveZone` uses it.

**H8 — null-collider physics frame during recook. ADDRESSED (defensive).**
`ApplyMerged` used to do `_mc.sharedMesh = null; … _mc.sharedMesh = mesh` (and `PatchRegion` still
does). The corrupt-order path is fixed: `ApplyMerged` now assigns the new mesh to the filter/collider
**before** destroying the old mesh. `PatchRegion`'s null→assign is one synchronous call the physics
step never observes, so it was left as-is with a clarifying comment.

**H9 — full-chunk re-mesh re-emits side bands around the smooth ridge. REJECTED / not needed.**
H5/H6 already argued the default repeat Wall doesn't flip to `FullRebuildChunk`, and once H1 (additive
compounding) explained the chunk-wide rise, no full-rebuild re-mesh was required to account for the
symptom. Not investigated further.

**H10 — background-generate race. REJECTED.**
`BackgroundGenerateChunk` does run `BuildOrLoadChunk` off-thread, but the report is fully
deterministic (every second+ cast), which a race can't explain. Superseded by H1.

### Dead ends worth remembering
- Time was spent chasing a "fill origin" bug in `BuildMergedMeshData`'s defensive null-tile fill
  (`fillOrigin = tiles[i].Coord` of the first non-null tile, then used as the origin for later null
  tiles). It IS a latent correctness smell if the first non-null tile isn't at local (0,0), but the
  clean path (`BuildOrLoadChunk` on `anyMissing`) means the fill rarely fires, and it doesn't explain
  a repeat-cast symptom. Don't re-chase it for 1cm.
- Considered "player walks up the wall then the next cast embeds them" — but the `1bn` keep-out ring
  (`keepOutR ≈ 0.9` around `casterFeet`) prevents a raise within a capsule radius of the feet, so the
  capsule can't be newly embedded by the cast itself.

### Facts to carry forward (verified by reread)
- **CORRECTED:** repeat-cast height math was **NOT** a no-op — the additive form compounded up to the
  cap (H1). Now genuinely idempotent via absolute `Max`/`Min` targets.
- Coordinates/origins are consistent (H2).
- Merged mesh is tops-first; `PatchRegion` offsets are safe (H3).
- Nothing translates a `ChunkObject` at runtime (H4).
- `IsFlatTile` and the 75% threshold do NOT flip for a plain repeat Wall (H5/H6).

### Next session plan (EXECUTED — outcome in VERDICT)
1. Instrument cast #1 vs #2 (temporary logging, or careful read): log `center`, which collider the
   aim ray hits, the route chosen (`PatchRegion` vs `FullRebuildChunk`), and the region `w,h`. This
   single step should collapse H7 vs H9.
2. Read `ChunkMeshGenerator.EdgeIsRaised` / `EdgeHeights` + collapse threshold; confirm whether a
   smooth 2.6 m ridge edge emits side bands on a full rebuild (H9).
3. Then decide the fix from evidence. Likely one or more of: (a) exclude the deformed terrain
   collider from the **aim** ray only, (b) make the collider recook atomic (no null frame), (c) keep
   repeat casts on the region-patch route.
4. Play-test: cast Earth Wall 3-4× at the same spot standing on the chunk → every cast must look and
   behave like the first (ridge ≈ 2.6 m, no whole-chunk re-mesh, no jerk); walk away and back → wall
   persists identical.

### Open questions I still wanted answered (resolved)
**Answered:** the compounding is per-cast and independent of charge/radius (the flanks compound up to
the cap regardless); the "move" is the terrain re-mesh at cast time, not a delayed physics pop (H8 is
not the cause); a Crater repeat also compounded (ground got deeper each cast), consistent with H1
rather than H7 alone.

- Was the first cast the DEFAULT radius or heavily charged? (changes H6/H9 exposure)
- Does the "move" happen at the instant of the cast, or a frame or two later (physics step → H8)?
- Does a non-Wall repeat cast (Crater at the same spot) also do it? If no, H7 (aim hitting the raised
  collider) is a much stronger candidate than the generic recook/rebuild paths (H8/H9).
