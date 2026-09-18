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

## 1cm — Earth Wall repeat cast "makes the entire chunk moving" (OPEN)

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

**H1 — heights stack/grind on repeat (dirty heights).** REJECTED.
`DeformAt` raises then clamps each corner to `ceiling = GetHeight(Seed, cx, cz) + lift`
(`WorldStreamer.cs:811-824`). After a first cast the corners sit at/below that ceiling; a repeat cast
recomputes the same value and clamps back. It is a provable no-op for the Wall (and Crater clamps to
`floorY` the same way). This is the single most useful fact in the whole investigation: **it means the
bug is not in the terrain values.**

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

**H7 — the second cast's aim ray hits the first wall's collider. OPEN (leading candidate).**
`ResolveZone` does a plain `Physics.Raycast(pos, fwd, range)` with no layer filtering. After cast #1
there is a 2.6 m ridge in the aim path, so cast #2's `aimHit` lands on the wall, and the follow-up
down-probe can leave `center` on/near the wall instead of the intended ground point. This changes
where the deform + **collider recook** happen, even though the heights clamp. Needs a decision on
whether the aim probe should ignore the already-deformed terrain collider (but NOT ignore it for the
down-probe, which legitimately wants the ground).

**H8 — null-collider physics frame during recook. OPEN.**
`PatchRegion`/`ApplyMerged` set `_mc.sharedMesh = null` then `= mesh`. Between those statements there
is a physics step with no collider under anything standing on that chunk → CharacterController drop /
re-depenetration pop, which reads as the world jerking. Cast #1 may land far from the feet; the
repeat cast (same spot, standing close) could expose it. Fix shape: cook the new collider/asset first
and swap `sharedMesh` once, never leaving it null.

**H9 — full-chunk re-mesh re-emits side bands around the smooth ridge. OPEN.**
Any route into `FullRebuildChunk` rebuilds the whole 30×30 mesh + collider and re-runs `EdgeIsRaised`.
If the first wall's ridge edges now emit side bands (or vert/tri counts change), the entire chunk
visibly re-meshes at once. Need to read `EdgeIsRaised`/`EdgeHeights` and the collapse threshold, and
establish which route cast #1 vs cast #2 actually take. H5/H6 argued against the *default* route
flip, so this is only live if something else forces the full rebuild (e.g. the wall crossing into a
neighbour chunk that does contain a flat tile).

**H10 — background-generate race. OPEN (fallback).**
`BackgroundGenerateChunk` runs `BuildOrLoadChunk` (reads `_loadedData` + save files) on a ThreadPool
thread while `ApplyHeightEdits`/`FlushDirtyChunk` mutate them on the main thread. A chunk the first
wall crossed could finish generating from a stale file mid-edit and snap its mesh. The user's
deterministic repro argues against a race, so this is last until H7-H9 are ruled out.

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
- Repeat-cast height math is a clamped no-op (H1).
- Coordinates/origins are consistent (H2).
- Merged mesh is tops-first; `PatchRegion` offsets are safe (H3).
- Nothing translates a `ChunkObject` at runtime (H4).
- `IsFlatTile` and the 75% threshold do NOT flip for a plain repeat Wall (H5/H6).

### Next session plan
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

### Open questions I still want answered
- Was the first cast the DEFAULT radius or heavily charged? (changes H6/H9 exposure)
- Does the "move" happen at the instant of the cast, or a frame or two later (physics step → H8)?
- Does a non-Wall repeat cast (Crater at the same spot) also do it? If no, H7 (aim hitting the raised
  collider) is a much stronger candidate than the generic recook/rebuild paths (H8/H9).
