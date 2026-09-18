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
