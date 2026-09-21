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

---

## 1e8 — Play-test of 1e7: "some faces not loading" + "shoulder joint too narrow" (SHIPPED in `1e8`)

Two user reports right after 1e7 (first real render of the shouldered torso). Two separate root
causes, both confirmed by reread + silhouette math, no build.

### H1 — "some faces of the torso is not loading" → CONFIRMED: the crown-cone band is never emitted
Looking for the hole in `BuildTorso`'s emission, not the lighting/winding first. Structure:
`bands = 8` → 9 rows (lat 0..8), side bands loop `for (int b = 1; b < bands; b++)` → 7 iterations,
band b connects rows (b−1, b) → bands (0,1)…(6,7). Row 8 (the t=1.0 crown ring, W 0.20) connects to
nothing below: the cap loop only fans it to the crown CENTRE (closing lid) while row 7 (t=0.875,
W 0.74) connects down to row 6. So the crown cone — the actual "dome slope" in 1e4's wording — is a
hole ring around the top; the crown lid floats. That's the visible see-through. The ellipsoid
`Generate` loop `b < Rings` (1..6) has no such hole because its lat=0/Rings rows are degenerate poles.
- FIX: `b <= bands` (emits rows 7–8 cone). Indices: corners length = 9×12 = 108, band 8 reads rows
  7–8 → max index 107 ✓. This restores the intended dome slope geometrically. OPEN (visual, not
  logical): whether `EmitQuad`'s single-flat-normal cells on the STEEP crown cone render clean — they
  should (nearly-planar 30° frustum cells; centroid-flip consistent) but is in the play-test list as
  a contingency (escalate band 7–8 to per-triangle emission if glitchy).

### H2 — "shoulder joint too narrow, overlap with torso" → CONFIRMED: ball buried by the 1e7 dome
Numbers: standing pivot (±0.28, 0.35) → part-local y 0.22 → t = 0.22/0.8 + 0.5 = 0.775 (NOT 0.90 —
that was a slip dividing by height wrong; t = localY/sizeY + 0.5 = 0.275+0.5 = 0.775). W(0.775) on the
old rows (0.80, 0.74) ≈ 0.788 → world 0.44·0.79 = 0.347 (male) / 0.40·0.79 = 0.315 (female). Ball:
size 0.14 → radius 0.07, center 0.28 → outer edge 0.35. Male surface 0.347 ≥ outer 0.35−ε → ball
flush/invisible; female shows a 3.5 cm sliver. Matches "too narrow / overlapping the torso".
- Hypothesis options (asked the user): (A) bigger balls + outward pivots, (B) bigger balls only,
  (C) narrow the dome so the SAME balls poke out. USER CHOSE C — keep joint sizes/pivots as authored,
  reduce the silhouette so the ball reads as a cap.
- FIX (Body): rows t=0.75 0.80→0.70 and t=0.875 0.74→0.60 (D: 0.48→0.44, 0.44→0.38), crown 0.20
  untouched. New W(0.775) = lerp(0.70, 0.60, 0.2) = 0.68 → world 0.299 male / 0.268 female → poke
  0.35−0.299 = 0.051 / 0.082. Chest (0.72) stays the widest upper point — taper, not inverted.
- FIX (Chest for the sit model): sit pivots at t = (0.40−0.42)/0.28 + 0.5 = 0.429; old W ≈ 0.71 →
  world 0.39·0.71 = 0.277 vs ball outer 0.25+0.065 = 0.315 → 3.8 cm sliver. New mid rows (0.375→0.66,
  0.5→0.62, 0.625→0.60): W(0.429) ≈ 0.643 → world 0.250 → poke ~6 cm ✓.
- Seated Body (pivot t = 0.22/0.6 + 0.5 = 0.867) inherits the Body rows: W ≈ 0.607 → world 0.34·0.61
  = 0.206 → ball clearly outside (−0.24 pivot, radius 0.065) ✓.
- OPEN (until play-test): cap readability — the ball is shirt-colored on a shirt-colored dome, so it
  reads through SILHOUETTE + facet normals, not color; if it still reads weak, the knobs are the wB/dB
  top rows or (revisited) ball size, not the pivots.

## 1e7 — "The shape of torso is still the same" — why the shouldered torso never changed (SHIPPED in `1e7`)

User report after 1e4 shipped: the torso STILL looks the same. Every prior silhouette change
(1e2 shoulders, 1e4 dome/crown) appeared to do nothing in play. Hypothesis investigation below.

### H1 — "the user just didn't notice / the dome is too subtle" → REJECTED
The dome slope + crown spans only the top ~12% of the part height above a 0.80-wide shoulder shelf —
conceivable as "too subtle". BUT the report says the torso looks the SAME as before, not "slightly
different". The stronger prior: the shape change never reached the renderer.

### H2 — "a stale cached mesh / another model path wins" → REJECTED (dead end, right symptom wrong cause)
Checked the player-model builders (`MapBuilder.PlayerModels`): the torso parts go through
`MakePart(name, parent, size, pos, color, profileId)` with profile ids `"Body"`, `"SitTorso"`,
`"Chest"`. NPCs/enemies use `MakeBlock` (cube) — not the same path. Stale mesh caching is
impossible (meshes live in an in-memory static cache; a recompile is a fresh domain). So the id path
itself is the suspect.

### H3 → CONFIRMED — `BuildTorso` was unreachable dead code; every torso part rendered as a plain ellipsoid
`BuildEllipsoid(profileId)` opens with `if (empty || !_profiles.ContainsKey(profileId)) profileId = "HairBand";`
then `Generate(profileId)` — and `Generate` only branches to `BuildTorso` when the id is exactly
`"Body"/"SitTorso"/"Chest"`. The three torso ids are intentionally NOT in `_profiles` (they aren't
dent-sculpted ellipsoids; they're the separate silhouette builder), so *every* call with a torso id
was remapped to `"HairBand"` first and the `BuildTorso` branch was unreachable. The torso ≥ 1dw was
always the plain unsculpted ellipsoid → identical silhouette through 1e2/1e3/1e4. The 1e2/1e4 "taller
Body so pivots sit on the dome" sizing was sized against a mesh that never drew.

Evidence trail (all grep/read, no build):
- `_profiles` keys (read): Skirt, SkirtHem, Head, Neck, UpperArm, Forearm, Hand, Thigh, Shin, Shoe,
  Hair, HairSide, HairBack, HairBand, Cylinder, Ponytail, EyeWhite, EyeIris, Joint — no Body/SitTorso/Chest.
- `BuildEllipsoid` fallback = "HairBand"; `Generate("HairBand")` skips the BuildTorso branch; HairBand
  has empty dents → plain ellipsoid. → confirmed reachability failure.
- Only caller of `BuildEllipsoid` is `MapBuilder.MakePart` (grep) which passes the raw profile id.

### FIX — route torso ids before the fallback (no shape change)
`BuildEllipsoid` now short-circuits `"Body"/"SitTorso"/"Chest"` to a cached `BuildTorso` build (same
`_cache` + `HideAndDontSave` pattern as the ellipsoid path). The silhouettes themselves were already
sized correctly — no values touched. Post-fix pivot-vs-silhouette sanity (reread math):
- Standing `Body` (size.x 0.44/0.40, centre root y 0.13): shoulder pivot local y = 0.35−0.13 = 0.22 →
  t = 0.72 → W ≈ 0.78 → world 0.44·0.78 = 0.344 (female 0.312) ≥ pivot ±0.28 → joints sit ON the dome band.
- Sit `Chest` (centre y 0.42): pivot local y = 0.40−0.42 = −0.02 → t = 0.48 → W ≈ 0.70 → world
  0.39·0.70 = 0.275 ≥ pivots ±0.25 → shoulders carried by the Chest plateau as designed.
- Seated-car `Body` (size.x 0.34, centre y 0.25): pivot local y = 0.47−0.25 = 0.22 → t = 0.72 → world
  0.34·0.78 = 0.266 ≥ ±0.24.
- `SitTorso` sits under the `Chest` (top W 0.46 tucks under Chest bottom W ≈ 0.70) → reads as layered,
  no seam.

OPEN (until play-test): visual fit/"reads" of the newly-rendered shoulders vs the joint balls and the
neck crown — if the silhouette reads wrong on screen, tune the dome/shelf arrays, don't re-plumb.

## 1e6 — Structural render/physics: is the LOD/pooling layer worth wiring? (SHIPPED in `1e6`)

The second half of the 1e5 audit (render/physics budget + the Phase 9 managers that were never
consumed). Verdict: the LOD layer was a genuine no-op bug and the pooler was pure dead code — both
worth wiring; the rest of the "audit suggestions" were rejected with evidence.

### H1 — "ChunkLodManager already saves triangles" → REJECTED (it saved nothing)
Read: `RegisterChunk` only indexes children named `Lod*`; nothing in the codebase ever CREATES a
`Lod1`/`Lod2` child (grep across Assets: only `RegisterChunk`). So every chunk had an empty Details
dictionary, `ApplyBand` fell to the `useDetail == false` branch forever, and — worse — the branch
that DID run only did `root.GetComponent<MeshRenderer>().enabled = true` (already true), never
disabling the root when a detail *was* present. Net: bands 30/60/120 existed on paper, zero triangle
saving, ~3.7k chunks at full ~1800 tris each. → CONFIRMED dead; fixed by building real LOD children.

### H2 — "decimate the merged mesh with a simple vertex stride" → REJECTED (layout is not a grid)
The merged mesh is per-tile quads (4 verts/tile, fixed offset `(lz*cs+lx)*4`) followed by side-wall
verts — NOT a uniform vertex grid. A naive stride would sample across quad boundaries and tear the
surface. Also, the top-block corners are DUPLICATED per tile (each tile stores its own 4 corners), so
a stride over raw vertices is meaningless. FIX: sample the *tile-corner grid* — grid point (gx,gz)
maps to a specific tile's SW/NE/NW/SE corner via `WorldCornerIndex`, and the decimated mesh shares
one vertex per grid point → watertight inside itself, exactly on the chunk edges.
- Sub-bug caught in review: step 4 does NOT divide the 30-tile chunk (0,4,...,28) → Lod2 stopped 2 m
  short of the edge and would leave a seam against the neighbour. Switched Lod2 to step 3 (0,3,...,30)
  and documented the "step must divide cs" contract.

### H3 — "build LOD children eagerly in ApplyMerged" → REJECTED (cost for chunks that never use them)
A radius-30 ring is ~3.7k chunks; most sit in band 0 and never show a detail. Building 2 extra meshes
per chunk on every apply would ADD work for the majority. FIX: lazy build, `_lodDirty` set by every
apply/patch, `RefreshLodMeshes()` called from `ApplyBand` only when a detail band is first selected.
This also solves deformation: a pit dug while near (band 0) marks the LOD stale; walking away past
60 m triggers the refresh so the far view shows the pit, not a closed-over surface.

### H4 — "ObjectPooler is broken (EntityId keying)" → OPEN-but-safe, left as-is
`GetEntityId()`/`EntityId` are not defined anywhere under `Assets` (grep) — they resolve from a
project-wide source the pooler already relied on. If a clone's id differs from its prefab's, pooling
silently degrades to `CreateNew` every Get (correct, just no reuse); if they match (the pooler's
authoring assumption), pooling works. Either way the wiring is safe; not worth chasing an external
type for a perf-only fallback. NOTED as an assumption, not a bug.

### H5 — "pool enemy death debris and loot drops" → REJECTED
Enemy `ExplodeModel` debris is the enemy's OWN rendered model parts (each `Renderer.gameObject`
detached + given a Rigidbody), not clones of a prefab — pooling them would mean returning model parts
to a pool and re-parenting them into the model factory, a structural rewrite for a per-death-only
burst. Loot drops (`DropDrop`) are persistent (parented to the world, consumed by pickup logic, not
stateless on wake). Both are low-frequency vs. spell impacts/digs. Skipped.

### H6 — "add a prop-collider distance band" → REJECTED
The 1di prop ring already restricts live props to a radius-4 Chebyshev ring (~600 m) — roughly 80
chunks × ~2 props ≈ **~160** live prop GOs/colliders near the focus, not the ~450k broadphase bodies
of the old full stream. A further band would buy almost nothing while making distant trees
walk-through and letting spell rays pass through them (contradicting 1di's "everything inside the
ring keeps its colliders", and the tool ray is only 10 m — `ToolManager.UseRayDistance`). Skipped.

### H7 — "throttle the whole SpellEffect Update to 30 Hz" → REJECTED as stated, narrowed
Movement + the two detonation probes must stay per-frame or a fast bolt tunnels/overlaps past a
target between steps. What IS safely throttleable is the homing guidance re-lock scan (RaycastAll +
OverlapSphereNonAlloc, `UpdateMissileTargeting`) — a soft "who do I chase" read. Throttled that to
1/3 frames, steering still per-frame. CONFIRMED narrower fix.

---

## 1e5 — Performance deep-dive: is there real mileage left after Phases 0-5? (SHIPPED in `1e5`, structural work deferred to `1e6`)

Three parallel audits (boot timeline, per-frame hot-path grep, render/physics budget) produced a mix
of real wins and false alarms. The adjust-the-ratios verdict was: startup had genuine leftover cost
(an actual triple-build), the per-frame field was mostly already clean, and the render/physics layer
holds the remaining big items (→ 1e6).

### H1 — "Boot is slow because of ~24 FindAnyObjectByType scans" → REVISED
Evidence: GameBootstrap.cs:17-54 does ~24 `FindAnyObjectByType<T>()` sweeps. But each sweep is ~0.1ms
on a sparse early scene — the whole scan block is ~1-3ms, small v. the init bodies. VERDICT: the
scans are NOT the main cost; the main cost is that `InitializeUI` (120-180 GOs + 8 textures, 15-35ms),
`ToolManager.Initialize` (53 tool models) and `SoundManager.LoadSoundClips` (8 sync Resources.Load,
No-alloc — each may be 0.5x-5x ms) run MULTIPLE times: UIManager's own `Start()` (UIManager.cs:236),
`GameBootstrap` (line 65/66), AND `GameManager.Start`→`AutoResolveReferences` (line 272/273). Three
UI builds. The registry (#17 documented approach) is still the right mechanism to remove the sweeps,
but the dedup guards are the real seconds-fraction win. → CONFIRMED-by-read, shipped.

### H2 — "Adding an `_initialized` guard to AutoResolveReferences is safe" → PARTIAL-CONFIRMED with a catch
The catch found by re-reading Start(): `AutoResolveReferences` is the ONLY place that fills `Pets` from
the scene (`FindObjectsByType<PetController>`) before `SpawnDefaultPets()` checks `Pets.Count`. An
early-return guard would stop a pre-placed scene pet from being counted → `SpawnDefaultPets` would
spawn an extra default pet that never existed before. FIX: guard only the side-effecting init cluster
(menu/UI/tool/cutscene/randomEvent), leave field resolution + Pets scan per-pass. → confirmed.

### H3 — "Deferring the non-critical boot inits to a BootInitDeferrer is safe" → CONFIRMED with exclusions
Evidence read: `QuestManager.InitializeQuests` is self-guarded (`_quests.Count > 0`), and
`GameManager.Start`→`StartNewGame` already calls `ResetQuests()`+`InitializeQuests()` + `Karma`/`Religion`
Initialize on frame 1 → re-queueing those from bootstrap would DOUBLE them. Excluded. `WifeNPC.Initialize`
only builds the dialog canvas; `WifeNPC.Start` only starts coroutines, and `ResetForNewGame` only resets
fields → delering Initialize/LoadState to frame 2 is invisible. `CutsceneManager.Start` only starts a
coroutine, `Initialize` prebuilds driving assets when enabled (deferred OK). → confirmed by read.

### H4 — Audit's per-frame "SkillBarHUD RefreshEntries Clear+AddRange+Sort per frame" → REJECTED as noise
Re-read: SkillBarHUD.Update only runs while the skill bar is visible, `RefreshEntries` writes into
pre-allocated lists (AddRange onto a zero-capacity list reallocates once then reuses; Sort on ≤10
items), and label repaint is already change-guarded (`_entriesDirty`). Not worth touching.

### H5 — Audit's "InteractionPrompt LateUpdate GetComponent<Collider> + FindNearest each frame" → REJECTED as already-mitigated
Re-read `UI/InteractionPrompt.cs`: the crosshair raycast already runs every 3rd frame with a cached
last-hit; text writes are change-guarded; the only true-every-frame cost is `ResolveEKeyLocKey`'s
`SittableSeat.FindNearest` when pointing at empty terrain (small linear scan) and one
`GetComponent<Collider>` guarded by Unity's per-type cache. Left untouched.

### H6 — "EnemyHealthBarHUD runs 24 WorldToScreenPoint/frame" → CONFIRMED, throttled
The 0.5s full-scene `FindObjectsByType<EnemyController>` rescan is intentional (`1dr`); the per-frame
cost was the screen-space projection for up to 24 bars. Throttled projections to 30Hz (position
stale for ≤1 frame only) + skipped bars for enemies > 60m + fill writes stay change-guarded.
CONFIRMED the previous narrowing (PlayerBars/MagicWheel already throttled by 1dr).

### Dead end recorded (so it isn't re-walked)
`SittableSeat.FindNearest` per-frame was hypothesized as the "interaction prompt" cost — the actual
per-frame prompt already caches it behind the raycast gate. Camera per-frame items were real but small.
The big render/physics findings (LOD meshes never created — `ChunkLodManager` band children don't exist;
~3,700 chunk draw calls; per-cast collider re-cook + save flush; ObjectPooler with zero consumers;
per-projectile `new Material`) are the structured second-half work → **1e6**, separate commit.

---

## 1e4 — shoulder-dome torso: kill the flat-collar look WITHOUT stranding the shoulder pivots (SHIPPED in `1e4`)

Motivation: the 1e2 flat top plateau read as a collar ring / hat brim around the neck base. 1e4
replaces it with a sloped shoulder dome + small crown. The SELF-REVIEW trap: the first 1e4 cut shipped
only "silhouette math looks right in the doc comment" — it did NOT recompute the shoulder pivots, which
turned out to sit exactly on the crown row.

### Step 1 — H1 "the crown covers the pivots": REJECTED by recomputation
- Standing shoulder pivot: torso-local (±0.28, 0.35) → root (±0.28, 0.40). Standing `Body` is
  size (0.44, 0.6, 0.25) @ root 0.05 → center root 0.10, top root 0.40. So the pivot is EXACTLY at the
  part top → t = 1.0 → the crown row (W 0.20) → world 0.44·0.20 = **0.088** vs pivot ±0.28 → freed.
- Seated: body (0.34, 0.5, 0.28) @ root 0.25 → spans [0, 0.50]; pivot (±0.24, 0.47) → unit y 0.44 →
  t 0.94 → silhouette ~0.46 → world 0.156 vs ±0.24 → freed.
- Sit: `Chest` (0.39, 0.28, 0.26) @ root 0.42 → spans [0.28, 0.56]; pivot (±0.25, 0.40) → t 0.43 →
  W ≈ 0.71 → world 0.277 ≥ ±0.25 ✓ COVERED (only this one was fine).
- Lesson already written in 1e2's log: the flat plateau W 0.80 held through t=1 WAS load-bearing for
  the pivots (1e2 Step 2). The 1e4 dome narrows to 0.20 at t=1 → any silhouette-only change was
  doomed to regress 1e0/1e2. The WIP doc comment even claimed coverage with a half-worked fragment
  ("0.65·0.44=0.286 ≥ ±0.13") that was never true arithmetic.

### Step 2 — H2 "just keep the top wide": viable but fails the mission
- Constraint: SHARED `"Body"` silhouette must satisfy BOTH standing (needs ≥ 0.28/0.44 = 0.636 at
  t 1.0) and seated (needs ≥ 0.24/0.34 = 0.706 at t 0.94 — the binding one). A dome that stays
  ≥ 0.72 at the very top covers both BUT the crown is gone → reads like the old plateau again. So a
  true small crown REQUIRES moving the pivot down the silhouette (taller body OR lower pivot).

### Step 3 — H3/H4 "taller body" vs "lower pivot": chose taller body (H3)
- H3 — build `"Body"` taller so pivots land on the dome band (t 0.78–0.87) instead of the crown row.
  Standing body center moves 0.05→0.13 (TORSO-LOCAL; the Torso pivot sits at root 0.05, so root
  center 0.18) and size.y 0.6→0.8 → spans torso-local [−0.27, 0.53]: pivot torso-local (±0.28, 0.35)
  → unit y (0.35−0.13)/0.8 = 0.275 → t 0.775 → W 0.79 → world 0.44·0.79 = 0.347 ≥ 0.28 (F 0.315 ✓);
  crown 0.088 tucks under the neck+head base. Seated body 0.5→0.6 (root center 0.25, spans
  [−0.05, 0.55]): pivot (±0.24, 0.47) → t 0.867 → W 0.744 → world 0.252 ≥ 0.24 ✓; crown flush under
  [0.50, 0.60]. Effects verified by inspection: hip pivots (standing −0.25, seated 0.06) stay inside
  the taller bodies; female skirt top (−0.22/−0.13 …) still overlaps the waist; `ApplyRaceLook` scales
  root + silhouettes equally → coverage invariant. CONFIRMED.
- H4 — drop the pivots 10–15 cm (standing 0.35→0.20, seated 0.47→0.36): best margins but moves every
  arm/hand/weapon chain and re-risks the driving cutscene reach. REJECTED (kept as the C-option).
- Verdict: SHIPPED in `1e4` (H3). Silhouette arrays unchanged from the 1e4 WIP — the fix was purely
  the two `MakePart("Body"…)` size/pos lines + the corrected doc text. No CLI build (rule 3); user
  compiles in Unity. Play-test: shoulder seams stand/seated, longer-torso waist/shirt-hem look, race
  spread > 1.2 coverage, skirt seam, ponytail clearance.

---

## 1e3 — hair refit: scalp cap that hugs the skull, not floating slabs (SHIPPED in `1e3`)

User: "need a way to fix the hair since its not fit the head at all". Chose "cap that hugs the
skull (Recommended)". During implementation I found the RECOMMENDED nesting-under-Head variant has a
hidden trap; resolved by position-fitting instead (same hug, zero hierarchy risk) — recorded here.

### Step 1 — measured the float, disproved the nesting shortcut
- Standing head centre (0.65, r 0.15, crown ~0.81 with cranium dent); the old crown slab sat at y
  0.88 → floated 0.04–0.07 above the scalp. Sides (±0.19) and nape (−0.16z) also missed the shell.
- H1 — nest hair under Head with head-local offsets. FOUND BROKEN: parenting under Head multiplies
  every child localScale by Head's `localScale` (0.3 for the standing head, and per-race `headScale`
  later) → the same world look would need local sizes ≥1.0 (0.32/0.3 ≈ 1.07), ugly and fragile, and
  a solid ellipsoid "cap" can NEVER hug a sphere from above without either floating above the crown
  or covering the eyes (a shell cap with a face window needs a cutout our solid parts can't make;
  the crescent between eye-line y 0.76 and crown y 0.81 is only 0.05 tall). REJECTED nesting; kept
  parents (torso/root).
- H2 — the "scalp cap" IS best built as a **thin oblate lens**: sized so its widest band sits on the
  crown at the PLATEAU (skull top pierces the lens's lower half, upper rim rises ~0.05 — attached,
  no float), with front rim ry around 0.10 at the eye line (rz ≈ 0.10 < eye z 0.155 → face clear).
  That's just an ellipsoid with the RIGHT size/position. ACCEPTED: values tuned per head (standing/
  sit crown 0.80–0.92, seated 0.87) from actual head hull numbers, not guesses.

### Step 2 — measured-value table (standing shown; seated/sit scaled for their heads)
- `Hair` lens 0.36×0.12×0.30 @ (0,0.80,0): rim at y 0.74, half-x 0.18 → at y 0.755 (eye top) the
  lens radius is ~0.12x/0.10z << eye Z 0.155 → eyes clear; at the crown it covers to rx 0.177 ≥
  cranium 0.162 → crown buried with margin. Verdict: the "cap hugs" claim is now arithmetic, not vibes.
- `HairL/R` ±0.18 @ y 0.70: inner face 0.14 < skull side 0.141 → 1 mm buried; outer 0.22 = visible
  hair mass. `HairBack` @ (0,0.66,−0.16) w 0.11: inner face buried 0.05 into the back shell, outer
  protrudes 0.05 = nape volume. `Band`/tail re-anchored on the same numbers.

### Step 3 — verification
- Grep + reread (rule 3): 18 hair MakePart lines updated; no consumer outside the builders touches
  `Hair*`/`Ponytail*` names except the `headScale` prefix check (unsafe to change) and NPC hairdos
  (MakeBlock, separate). No hierarchy change → zero contract risk.
- Left OPEN for play-test: lens thickness reads "thick hair" not "helmet"; side/nape panels blend at
  the skull seams; big-head races (headScale) keep the relative fit (numbers scale with Head because
  both head and hair get the same ratio recursion scaling).
- Verdict: SHIPPED in `1e3`. No CLI build (rule 3); user compiles in Unity.

---

## 1e2 — shouldered torso silhouette: torso/chest stop being ellipsoids (SHIPPED in `1e2`)

After 1e1, the user reported "the torso still has gaps, change the shape" and separately that the
hair "doesn't fit the head at all". 1e2 = torso; 1e3 = hair.

### Step 1 — why 1e0's dents couldn't close the remaining gaps
- H1 — the shelf/flare dents just need to be stronger. COMPUTATION splits the hypothesis: dents push
  a corner along its RADIAL by ≤ `Strength` (0.15 unit). The standing shoulder pivot in unit space is
  (±0.28/0.44, (0.36−0.05)/0.6) = (±0.64, 0.52) → radius ≈ 0.82 against a 0.5 sphere. Even a corner
  at the shoulder cap (radial 0.5) can only reach 0.65. CONFIRMED: dents can never bridge the shell.
  REJECTED as the fix.
- H2 — tuck pivots further in. Rejected: shoulder x was already tucked 0.33→0.28; going to ≤0.15
  breaks arm/body proportions and misplaces the whole arm/weapon reach.
- H3 — REPLACE the ellipsoid with a shouldered torso silhouette whose shoulder band physically
  reaches ±0.8 unit (world 0.35 at size.x 0.44). ACCEPTED (user: "change the shape"; chose
  "moderate athletic"). Because world half-width = `size.x·W`, keeping size vectors means the reach
  scales automatically for all variants (reasoned through each: standing pivot needs ≥0.64·ux,
  seated 0.71, sit-chest 0.64, sit-torso clamped top 0.70 → one 0.80 Body curve + 0.72 Chest +
  0.70 SitTorso covers all).

### Step 2 — the shape
- Kept the 1dx visual language: 12 segs × 7 flat bands, same jitter seeds → watertight mosaic
  identical in feel to the limbs. Reused the mosaic emission block verbatim from `Generate`.
- TOP: instead of a pole, a flat **shoulder plateau** disc (W 0.80 held to t=1). The `"Cylinder"`
  neck passes through its middle; the plateau annulus reads as the collar ring, and the neck base is
  half-buried so no see-through sneaks around the pirce point (the neck cylinder occludes the cap's
  centre triangles). Collar-vs-hat rim flagged for play-test.
- BOTTOM: closed cap at the waist/hip ring (female skirt + male pants cover it anyway).
- Height still spans y ±0.5 → `size.y` scaling unchanged; the torso is the ONE mesh that breaks the
  [-0.5,0.5] half-cube doc contract (shoulders 0.8) BY DESIGN — updated in game-design.md and the
  class doc rather than hiding the exception.
- Sit model: two stacked parts (Torso + Chest). Chest gets a mid-band 0.72 plateau because the sit
  shoulder pivot (0.25, 0.40) lives ~at the Chest's vertical centre, not near its top — verified the
  pivot's unit height (0.40−0.42)/0.28 = −0.07 → t≈0.43 → Chest silhouette 0.72 → world 0.28 ≥ 0.25.
  SitTorso's top 0.70 keeps the hip band covering sit hips (±0.13, bolted by hip ball).

### Step 3 — joint balls + pivot pucks
- Shoulder/hip balls shrank ~15% so they sit EMBEDDED in the plateau/hip band instead of poking free
  (the plateau skin now reaches past their centres). Standing JShoulder 0.16→0.14, JHip 0.15→0.13;
  seated 0.14→0.13, 0.13→0.12; sit 0.15→0.13, 0.14→0.13.
- Shoulders dropped ≤1 cm so the ball's top sits under the plateau: standing 0.36→0.35 (unit height
  0.5 = plateau row), sit 0.41→0.40; seated stayed 0.47 (already inside, unit 0.44 → t≈0.94). The
  ~1 cm drop is arm-chain-following, reach-length-neutral; flagged for play-test.

### Step 4 — verification
- Grep + reread (rule 3): three profiles gone from `_profiles`; no other caller references them
  (`MakePart` strings only live in the three PlayerModels builders); `BuildTorso` routed for exactly
  Body/SitTorso/Chest; all tucks/ball sizes single-edit verified; the Emit doc-header fix confirmed.
- Left OPEN: plateau collar read, extreme race spread (>1.3) shoulder float, skirt/waist cap seam.
- Verdict: SHIPPED in `1e2`. No CLI build (rule 3).

---

## 1e1 — neck switched from pillar to a cylinder (SHIPPED in `1e1`)

Follow-up to 1dz. User: "change the neck into cylinder". This REPLACES the 1dz square pillar
(removed) rather than layering on top of it — no clarifications needed: a cylinder is a round column.

### Step 1 — mesh design
- H1 — taper/reuse the pillar's box-scale silhouette, just round the shaft in 8–12 facets.
  ACCEPTED (12 facets = same band count as the body's mosaic → consistent flat-shading scale).
- H2 — soft-shaded lathe (many segments, smooth normals). REJECTED: fights the flat-facet look
  established in 1dx; the pre-1dz round neck was already rejected as a plain sphere, a smooth
  cylinder would read as a leftover primitive.
- Winding: sides + caps all go through `EmitQuad`/`EmitTriangle` per-panel centroid flips — no global
  orientation assumptions (same reasoning as 1dx/1dz). Verified by hand for the top cap (normal +Y)
  and bottom cap (normal −Y) before relying on the flip logic.
- Top/bottom caps use the pole point duplicated per triangle (fan) — watertight, no polar cap gap.

### Step 2 — cleanup + why removal is safe
- `BuildPillar` + `BuildBox` + the `"Pillar"` profile were only reachable from the neck (grep showed
  the only `"Pillar"` consumers were the three `Neck` MakePart calls + docs). Swapped the profile key,
  deleted the dead generator + box helper → no orphan code, no fallback risk. Documented the 1dz
  removal as a supersede note rather than deleting the PROGRESS entry (record authority).
- Sizes kept identical to the pillar (standing 0.15×0.16, seated 0.13×0.10, sit 0.14×0.12): the unit
  cylinder's width = size.x (it spans ±0.5 in xz), so the column now has radius size.x/2 (0.075) —
  slightly slimmer than the pillar's 0.09 shaft but reads as a proper neck; head/torso overlap
  unchanged (same height envelopes as verified in 1dz).

### Step 3 — verification
- Grep + reread (rule 3): zero remaining `Pillar`/`BuildPillar`/`BuildBox` references under
  `Assets\Scripts`; `"Cylinder"` profile + `BuildCylinder` + Generate branch present; three `Neck`
  MakePart calls use `"Cylinder"`. No API/name/collider changes; the `Neck` GameObject name kept →
  race-ratio counter-scale + all consumers untouched.
- Left OPEN for Unity play-test: 12 facets on a ~0.1-scale neck reads "cylindrical" vs "octagon";
  cap shading vs head shadow; overlap flushness under the head.
- Verdict: SHIPPED in `1e1`. No CLI build (rule 3) — verified by grep + reread; the user compiles in
  Unity.

---

## 1e0 — seal the torso↔limb gaps (SHIPPED in `1e0`)

Follow-up to 1dy. User: "the torso and limbs has gaps either change the shape of torso". Chose:
all junctions (shoulders + hips; elbows/knees flush already verified), reshape + tuck pivots.

### Step 1 — why the gap appeared
- The 1dw ellipsoid swap rounded the torso corners; the old shared cube's sharp top/side corners
  physically covered the arm-attach region, masking the shoulder pivot offset. The dent mosaic kept
  the rounded silhouette → surface receded from the pivot.
- Quantified (by reread of sizes/positions, standing): Body ellipsoid radii (0.22, 0.30, 0.125) at
  centre (0, 0.05); shoulder pivot (±0.33, 0.37). Radius along the pivot direction u=(0.67,0.74):
  1/sqrt((0.67/0.22)²+(0.74/0.30)²) = 0.255; pivot distance 0.418 → gap ≈ 0.16 world units; the
  0.13 (r 0.065) JShoulder ball covered < half → visible pinch. 1dy's narrower torso worsened it
  (0.25→0.22 half-extent).

### Step 2 — fix design decision
- H1 — enlarge the joint balls enough to bridge everything. REJECTED as the sole fix: would need
  balls much bigger than the limbs → bloated knobs, not seams.
- H2 — only reshape the torso (bulge skins out to the pivots). REJECTED as the sole fix: dent push
  is bounded (radial, ≤ ~strength), physically can't reach the pivots that sit past the surface.
- H3 — three-part stack: (a) torso **shelf dents** bulging the skin toward the pivots, (b) tuck the
  pivots in a few cm so they land inside the (bulged) surface + ball radius, (c) enlarge the
  shoulder/hip balls slightly so they lap the seam. ACCEPTED — each part is modest, together they
  overlap rather than leave a gap. Proportions kept ball ≈ limb cross-section (shoulder 0.16 vs
  upper arm 0.12/0.26 tall — reads as a shoulder ball in a socket).
- Dent placement care: shelf anchors at unit ±(0.34, 0.34) with radius (0.28,0.22,0.26) → influence
  at chest centre ≈ 0 (offset magnitude beyond radius) — verified by reread of the Dent math, so the
  chest/waist profiles don't bloat. Hip flare anchors ±(0.26,-0.36) keep the pelvis subtle for the
  female skirt + male trunk.

### Step 3 — pivot tuck safety
- Tucked only the spawn POSITIONS of Shoulder/Hip pivots; names, rotations, and all child chain
  offsets untouched. PlayerAnimator does rotation-driven DoF — the ~2–4 cm origin shift is within
  the swing envelope. WeaponRigBuilder measures shoulder→hand reach from these pivots; the shoulder
  tuck shifts reach by < 4 cm (arm length chained off the pivot) — acceptable, flagged for play-test.
- Race ratios: `ApplyRaceRatioRecurse` sets pivot `localScale`/`localPosition` (spread, arm, leg)
  AFTER build — it multiplies into whatever base we baked, so the tuck and ratio spread compose.
- The shelf bulge is on the SHARED `"Body"` mesh: female/seated benefit too; ratio-scaled bodies
  scale the same unit mesh → proportionally the shelves belong to the same silhouette family.

### Step 4 — verification
- Grep + reread (rule 3): shelf+flare dents only on `"Body"` + `"SitTorso"`; shoulder position values
  per builder distinct + tucks applied (0.33→0.28 etc.); JShoulder/JHip sizes bumped per plan (0.13/0.16,
  0.12/0.14, 0.13/0.15; hips 0.14/0.15, 0.12/0.13, 0.13/0.14); no other profiles touched; no API/
  name/collider change. One near-miss walked: an earlier JHipL edit's oldString matched two builders
  (sit 0.13 and standing-on… actually stood 0.14 / seated 0.12 / sit 0.13 — the sit one collided only
  with the seated 0.13 after seated edit? no — it matched once, DISAMBIGUATED by position context;
  the "multiple matches" error surfaced and I anchored with the hip localPosition line). Caught by
  the tool — good example of why edits want surrounding context.
- Left OPEN for Unity play-test: whether the shelf bulge reads as deliberate breadth vs a hump at
  small races; ball override on the forward-reached seated arms; first-person arm visibility at the
  shoulder.
- Verdict: SHIPPED in `1e0`. No CLI build (rule 3) — verified by grep + reread; user compiles in
  Unity.

---

## 1dz — neck switched to a pillar shape (SHIPPED in `1dz`)

Follow-up to 1dy. User: "the neck switch to pillar shape". No clarifying questions asked — "pillar"
in this block-style game reads as a straight square masonry column, and switching just the neck's
mesh keeps every contract intact.

### Step 1 — what shape is a "pillar"
- H1 — a plain box (the old pre-1dw cube) for the neck. REJECTED as the sole option: identical to the
  legacy cube, reads as a regression rather than a deliberate column.
- H2 — a round cylinder column (lathe). REJECTED: "pillar" here implies square masonry (the world's
  pillars are box/square); a cylinder is "column", and it'd fight the flat-shaded block teshetic.
- H3 — a **square masonry pillar**: foot slab + straight shaft + cap/abacus, all flat-faced.
  ACCEPTED: unmistakably a pillar, still occupies the [-0.5, 0.5] unit cube → `localScale` = size
  vector contract unchanged, cache still keyed by profile, race ratios transform-only.
- Taper idea (entasis): a true frustum needs non-axis-aligned faces — extra generator code, no real
  gain at neck scale. Dropped; straight shaft between the slabs already reads as a pillar.

### Step 2 — mesh build
- Reuse `EmitQuad` for each of the 6 faces of three stacked axis-aligned boxes. EmitQuad already does
  the per-panel outward winding check against the panel centroid — for a box face the centroid dot
  its outward face normal is positive, so orientation is automatically correct with NO new winding
  logic (verified by reasoning, not by build — rule 3).
- Interior faces between slabs (base-top/shaft-bottom, shaft-top/cap-bottom) are emitted but buried
  inside solid geometry: backface-culled and invisible, so the mesh is fully closed and watertight
  with zero special-casing.
- Profiling: 18 quads ≈ 72 verts — trivial; cached once under `"Pillar"` like every other profile.

### Step 3 — builders
- Only the `Neck` `MakePart` calls change (profile `"Pillar"`, sizes bumped so the column reads:
  standing 0.15×0.16, seated 0.13×0.10, sit 0.14×0.12). GameObject NAME stays `"Neck"` — the name is
  what `ApplyRaceRatioRecurse` matches for head/neck counter-scaling and what `PlayerAnimator`-adjacent
  lookups use; changing it would break the race ratios. The old `"Neck"` dent profile stays in the
  dict (harmless, documented unused).
- Overlap checks (neck top vs head base, computed by reread): standing neck spans y [0.32, 0.48],
  head base 0.50 → clean 0.02 gap; seated spans [0.50, 0.60], head base 0.60 → flush;
  sit spans [0.56, 0.68], head base 0.63 → 0.05 tuck (invisible under the head). Acceptable.

### Step 4 — verification
- Grep + reread (rule 3): `"Pillar"` profile present; `Generate` routes to `BuildPillar`;
  `BuildBox` call sites = 3; the three `Neck` MakePart calls use `"Pillar"`. No public API change;
  `BuildEllipsoid`/cache/fallback untouched; the pillar is a separate cached mesh keyed by profile.
- Left OPEN for Unity play-test: whether foot/cap slab proportions read at neck scale (~0.1 world
  height) or want slightly thicker slabs; race-ratio counter-scale of a box neck (visual only).
- Verdict: SHIPPED in `1dz`. No CLI build (rule 3) — verified by grep + reread; the user compiles in
  Unity.

---

## 1dy — slimmer torso, +8% overall size, faceted ball joints at limb pivots (SHIPPED in `1dy`)

Follow-up to 1dx. User: "reduce the torso width abit then increase the total size, add sphere with
similar skin generate to between parts as joints". Clarified: moderate width cut (−12%) + +8% whole
model, joints colored to match the adjacent part, limb joints only (shoulder/elbow/hip/knee), applied
to standing + seated + sit variants.

### Step 1 — how to make the model bigger without breaking the grounded feet / car-fit
- H1 — raise each part's size vector by the factor by hand. REJECTED: ~40 MakePart call sites across
  three builders, easy to miss; feet re-plant (ApplyRaceLook) and the car/chair fits would drift in a
  piecemeal way.
- H2 — a single root `localScale` multiplier (a module const `PlayerModelScale`) applied to every
  root's scale AND position, plus the same factor in the standing `calibrateFeet` re-plant. ACCEPTED:
  scale cascades to every child offset + part size exactly like the old unit-cube contract; one
  constant to revert is the car-cutscene clipping fallback; human (K=1) is the old no-op shape.
  Caveat logged: `ApplyRaceLook` reads `modelRoot.localScale` (already includes K) for Height/Bulk so
  ratios compose correctly; only the y re-plant needed the K factor.
- H3 — change the `scale` default/arg of the builders. REJECTED (mostly): all callers pass scale=1;
  scaling a position/root would still need the calibrateFeet factor, and cutscene parents would
  silently drift. K baked in the builders matches the "make the model bigger everywhere" intent.

### Step 2 — torso width
- Standing male `Body` x 0.50→0.44, female 0.46→0.40 (−12%). Kept shoulders at ±0.33 → arms sit a
  touch wider than the torso; the shoulder JOINT balls (1dy) land exactly there, which is what makes
  the wider-than-torso arms read as articulated shoulders rather than a bug. Seated `Body` 0.38→0.34
  and sit `Torso` 0.42/0.46→0.37/0.40 + `Chest` 0.44→0.39 proportion the same cut. No neck-joint
  sphere (user: limb joints only) — the Neck part already bridges head/torso.

### Step 3 — the joint spheres: placement, naming, layer routing
- Placement: `AddJoint(...)` parents a `"Joint"` profile part at the pivot transform's ORIGIN
  (localPosition zero) — zero pose work, rotates with the pivot in every animator/cutscene pose.
  Sizes tuned per joint around the arm/leg cross-section (shoulder 0.13, elbow 0.11, hip 0.14,
  knee 0.12; slightly smaller in the seated car model so the profile stays dainty in the car).
- Naming: `JShoulder/JElbow/JHip/JKnee` — the `J` prefix guarantees no collision with
  `PlayerAnimator` (`Torso/ShoulderL`, `ElbowL` under shoulder), `WeaponAnimator.FindOwnerShoulder`,
  `WeaponRigBuilder`, or `ApplyRaceRatioRecurse`'s Head/Neck/Eye/Hair/Ponytail/Shoulder/Hip patterns.
  A `StartsWith("Shoulder")` joint would be safe as a name but I chose `J…` anyway — belt and braces.
- Layer routing: `IsArmUnderShoulder` walks UP from a renderer to a `Shoulder*`/`Elbow*` ancestor;
  a `"Joint"` part on `ShoulderL` has parent `ShoulderL` → layer 7 (visible first-person) exactly
  like the arm parts. Hip/knee joints on `HipL`/`KneeL` stay layer 6 (not arm chains). Confirmed by
  rereading `PlayerController.Animation.cs` (arm-chain walk is ancestor-based, so child name is
  irrelevant).
- Race ratios: `ApplyRaceRatioRecurse` scales `ShoulderL/R` (y·arm, x·sw) and `HipL/R` (y·leg) —
  joints are CHILDREN of those pivots (not matched themselves), so they inherit the scaled pivot and
  stay proportional for Dwarf/Gnome/Fire Giant etc. Good.
- "Similar skin generate": reuse the SAME faceted mosaic via a new `{ "Joint", new Dent[0] }`
  profile (plain faceted sphere, no dents — joints are mechanical balls, not knobbly flesh).

### Step 4 — traps hit / caught during the pass
- First drafted the sit-builder root edit as oldString that ALSO swallowed the `female/race/color`
  declarations, replacing them with just the scaled-root lines → would have been a compile error
  (`race`/`shirtC` undefined downstream). Caught on the follow-up read, restored the block verbatim in
  the same pass. Lesson: when widening an edit's oldString, re-check the following lines still get
  their declarations.
- game-design edit briefly left a stray `----- "still reads" marker -----` line and a duplicated
  "Part renderers" bullet; both removed in the same pass. (No leftover markers/dups verified on
  final reread.)

### Step 5 — verification
- Grep + reread (rule 3): `PlayerModelScale` used in all three builders + calibrateFeet only;
  `AddJoint` defined once, used for all 8 joints × 3 builders; `"Joint"` profile exists in
  `PlayerPartMesher`; no public API/name/contract changes; no new colliders. Two method-of-record
  greps (`BuildPlayerModel(|BuildSeatedPlayerModel(|BuildSitPlayerModel(`) still resolve at their
  unchanged call sites.
- Left OPEN for Unity play-test: joint sphere sizes vs their parts (clip/float), whether +8% clips the
  car cutscene or chair seats (one-line revert via `PlayerModelScale`), and how the narrower torso +
  ball shoulders read on the small races.
- Verdict: SHIPPED in `1dy`. No CLI build (rule 3) — verified by grep + reread; the user compiles in
  Unity.

---

## 1dx — faceted low-poly player skin: chunky triangle/square mosaic with deterministic jitter (SHIPPED in `1dx`)

Follow-up to 1dw. User: the model "is currently only plain original shape" — wants "multiple surface
triangle, square shape generate all over the skin". Clarified: chunky low-poly facets, slight
hand-cut jitter (deterministic), all parts.

### Step 1 — what "triangle and square shapes all over" means technically
- H1 — flat-shading only: drop `RecalculateNormals()` so the existing smooth 9×16 grid shows hard
  edges. REJECTED: the grid is a plain lat/long mesh — every band cell is already 2 thin triangles
  pointing at the poles; flat shading would read as slivers, not "triangles and squares".
- H2 — re-topology into a panel mosaic: chunkier lattice, each cell becomes a QUAD (square) panel or
  two TRIANGLE panels, all flat-shaded via duplicated vertices with face normals. ACCEPTED: gives
  exactly the requested mixed mosaic and a stylized low-poly read; keeps the ellipsoid silhouette
  and the 1dw dents (sculpt happens on the lattice corners first).
- H3 — jitter per panel with independent corners (cracked/shattered gem look). REJECTED: single-sided
  solid meshes — any gap between panels shows the backfaces-culled hole straight through to the
  background/collider. Instead jitter the SHARED lattice corners: boundaries warp organically, mesh
  stays watertight, and it's trivially deterministic.

### Step 2 — panel winding, correctness traps
- Old code had ONE global winding check on the first face. New emission is per-panel: compute the
  face normal from the (pre-swap) triangle, compare against the panel centroid's outward radial,
  swap two indices + negate the normal when inward. This is safe for the pole fans too (south fan is
  emitted in the same loop direction as north — the per-panel flip absorbs the handedness), and for
  quads the single averaged normal is oriented consistently with the diagonal-triangle order used for
  the index buffer.
- Quad "square" read: both triangles share one flat normal, so the light seam that would split a
  quad into two shaded triangles disappears — it renders as one square. Coplanarity is only
  approximate on a curved ellipsoid cell (they're not planar), but at chunky scale with a shared
  normal this is invisible.
- Determinism: `Hash01(x, seed, salt)` (integer hash → [0,1)) with `AnchorSeed(profileId)` means the
  facet layout is byte-identical across sessions/processes — no `UnityEngine.Random` (results would
  change every play session and every rebuild). Cache stays keyed by profile only.
- CS0136-class shadowing audit (learned from 1dv): the method uses `s` (seg counter), `s1`, `b`,
  `lat`, `k`, `n`, `sm`, `h1/h2`, `t1/t2`, `iv`, `nn` — no redeclaration of an outer variable in the
  same block; the split branch redeclares nothing that collides. `0x5EEDF`/`0xCAFE` are plain int
  salts — fine.
- Latency: per-panel vertex duplication ≈ 400 verts / ~500 tris per part (quads ~47×4 + split tris
  ~50×3 + caps 24×3), built once per profile and cached — trivial.

### Step 3 — verification
- Grep + reread (rule 3): `MakePart`/`BuildEllipsoid`/cache/profiles untouched; `RecalculateNormals`
  gone from `PlayerPartMesher`; the three `MapBuilder` builders + all part/pivot names unchanged;
  parts still span [-0.5, 0.5] so `localScale` sizing (race ratios, weapon hand-scale) is intact.
- Left OPEN for Unity play-test: whether 6 bands × 12 cells reads "chunky" enough at every part size
  (the Head at 0.3 has the densest-eye region — hang looser than chunky on small parts), and whether
  35% triangle split vs squares feels right in-game.
- Verdict: SHIPPED in `1dx`. No CLI build (rule 3) — verified by grep + reread; the user compiles in
  Unity.

---

## 1dw — smooth player model: ellipsoid part surfaces with dent sculpt (SHIPPED in `1dw`; generator superseded by `1dx`)

Request: player model is "too blocky" — keep the existing spine/pivots for animation, generate the
visual surface of each part as an ellipsoid shape, and sculpt each part with multiple dents like the
terrain dents. Follow-up: MUST account for race-ratio differences.

### Step 1 — how to scope it without breaking the animated rig
- Constraint A (user): keep the spine/bone pivots so `PlayerAnimator`/`WeaponRigBuilder`/cutscenes
  don't move; only the visible surface changes.
- Constraint B: race ratios (`ApplyRaceLook`/`ApplyRaceRatioRecurse`) size parts through
  `Transform.localScale`; `RaceRig` scales the whole root; `WeaponRigBuilder.ScaleForHandScale`
  compensates hand size for draw/stow. If the mesh baked a fixed radius, every one of these breaks.
- H1 — replace each cube's MeshFilter with a pre-sized ellipsoid mesh (radius = size vector) at build
  time. REJECTED: bakes sizes into the mesh, so race/ratio/gender/weapon scale changes would need a
  mesh rebuild per part and the cache becomes size-dependent; also the older "ratios only move
  Transforms" invariant (1co) would be violated, and weapon-hand compensation targets a scale that
  would now be double-applied (mesh radius + localScale).
- H2 — unit-space ellipsoid (occupies the same [-0.5, 0.5] cube) + keeping the exact old size vectors
  as `localScale`. ACCEPTED: `localScale = old size` reproduces identical world dimensions, so every
  consumer that multiplies/averages part scales keeps working with zero change. Cache key becomes pure
  profile id; gender/race/model variants share one mesh per part.
- H3 — sculpt = hard boolean carve. REJECTED: adds per-vertex inside/outside tests, risky winding, no
  analogue in the codebase. Generalizing the existing terrain dent (`WorldStreamer.DeformAt`) was the
  natural fit (same codebase language, same smoothstep, only the falloff goes ellipsoid in 3D instead
  of a heightfield crater) and keeps meshes manifold.

### Step 2 — sculpt math = the DeformAt carve, generalized
- DeformAt: for a tile, influence from normalized distance to a pit center, `s = t²(3−2t)`
  smoothstep, then height is lowered along the terrain normal. Here: per-vertex influence from
  normalized ellipsoid distance `n = sqrt((dx/rx)² + (dy/ry)² + (dz/rz)²)`, `t = 1 − clamp01(n)`,
  and the vertex is pushed along its ORIGINAL radial (so positive strength bulges out, negative
  carves in — the same sign convention as height up/down). Anchors/radii live in unit space and scale
  linearly with the part (visual check: eyes seated in the Head's dents at ±(0.13, 0.02, 0.16) align
  with the EyeWhite/EyeIris positions at z≈0.155–0.165 of a 0.3-wide head — they sink consistently).
- Winding: sphere-grid generation could come out inside-out depending on Unity's handedness reading.
  Cheap guarantee: compute the first triangle's normal, dot against the first vertex position; flip
  the whole index list if it points toward the origin. Confirmed correct against Unity's
  counter-clockwise-front convention and cheap enough to leave always-on.
- After sculpting, `RecalculateNormals()` (the DeformAt path already does the same) so the smoothed
  surface gets correct normals; `RecalculateBounds`.
- Eye parts: old "EyeWhite" was a flat box (0.09 x 0.07 x 0.03) on the head front. As an ellipsoid
  it would stick through the dents, so EyeWhite/EyeIris get a front-bulge dent only (thin disc look);
  the head's sockets accept them.

### Step 3 — caching and threading
- Meshes are Deterministic/static; built lazily on first `MakePart`. H4 — cache per profile in a
  static Dictionary with hideFlags HideAndDontSave (like `SharedCubeMesh`, which the player no longer
  uses). ACCEPTED: N profiles → ≤ N meshes ever exist; no per-part allocations beyond transforms.
  Main thread only (model builds are already main-thread).
- Mesh selection: `PlayerPartMesher.BuildEllipsoid(profileId)` returns the shared instance; each part
  GameObject is just a MeshFilter/MeshRenderer + Transform. This mirrors how `SharedCubeMesh` +
  `MakeBlock` worked, so `MakePart` is a drop-in sibling with an extra profile arg and no collider.

### Step 4 — verification / dead ends
- Grep: no `MakeBlock` remains in `MapBuilder.PlayerModels.cs` (all player parts → `MakePart`);
  part names/pivots/sizes/rotations preserved verbatim (PlayerAnimator shoulder/elbow/hip/knee
  chains, WeaponRigBuilder hand lookup, WeaponAnimator.FindOwnerShoulder, layer 6/7 culling,
  ApplyRaceRatioRecurse name matching Head/Neck/Eye/Hair/Ponytail and Shoulder/Hip) — all still
  resolve. Race ratios remain transform-only; nothing bakes a size-derived radius.
- Dead end walked: considering a `Dent` as a reusable struct array with per-profile static fields was
  fine but Dictionary-of-arrays is terser and grep-checkable; no GC impact (static init once).
- Left OPEN for Unity play-test: proportion tuning (dent strengths/radii are hand-tuned, one pass),
  the eye-dent alignment on non-Human `BodyHead` ratios (head counter-scale scales dents too — expect
  them to stay proportional), and overlap/Z-fighting where hair, skirt and hands sit on bodies.
- Verdict: SHIPPED in `1dw`. No CLI build (rule 3) — verified by grep + reread; the user compiles in
  Unity.

---

## 1dv — chunk mesh pooling: one Mesh per chunk + capped freed-mesh pool (SHIPPED in `1dv`)

Second half of the 1dq deferral ("deferred to 1dt (noise memo) + 1dv (mesh pooling)"). Scope B per
user choice (persistent mesh + reload pool; NOT reusing the merged CPU arrays too).

### Step 1 — where the allocation churn actually is
- `ApplyMerged` was the single mesh constructor: `new Mesh` + `UploadMeshData(false)`, then
  `Destroy(previous)`. Callers: boot sync chunk, `CreateChunkGameObject` (every chunk fill/reload),
  `FullRebuildChunk` (whole-chunk rebuilds: slab chunks, seam/border reconciles, deform fallback
  when a chunk holds flat tiles or a patch nearly covers it).
- The deform FAST path (`PatchRegion`) already mutates `_mf.sharedMesh` + the retained `_merged` CPU
  arrays in place — it never allocated. So the honest churn is FullRebuildChunk rebuilds + chunk
  unload→reload (walk far). Volume is modest: this is a GC/GPU-fragment micro-opt, not a lag fix
  (1di/1dq already took the real physics cost).
- H1 — size-keyed pool: REJECTED. Every chunk mesh is the same shape (~961 verts / ~1800 tris; slab
  walls may add verts), so ONE unordered pool suffices — no buckets.
- H2 — per-chunk persistent Mesh + return to a capped pool on Release. ACCEPTED: covers rebuild churn
  (same instance re-uploaded, no Destroy) AND reload churn (freed mesh reused by the next chunk). Cap
  48 bounds retained GPU memory; overflow destroys.

### Step 2 — pitfalls found while writing it
- **Overwrite-only pooling is safe** because every upload re-specifies ALL arrays (UploadMerged runs
  the full setter sequence) — a reused buffer is never left with stale index/vertex counts. A slab
  wall chunk uploads more verts → buffer grows; a later plain chunk uploads fewer → array length
  truncates; Unity retains the larger GPU allocation (memory retention, no corruption).
  - **PARTIALLY REJECTED by runtime evidence (1dv follow-up fix):** Unity's typed channel setters do
    NOT let the VERTEX COUNT shrink via re-specification alone — the mesh retains its previous larger
    vertex count, so a pooled mesh that held more verts (slab walls) than the incoming upload threw
    `SetNormals/SetUVs/SetColors "is out of bounds"` on the first smaller re-upload. The fix keeps
    overwrite-only semantics but adds `if (mesh.vertexCount != md.Vertices.Length) mesh.Clear();` at
    the top of `UploadMerged` — Clear resets every channel buffer to zero so the setters grow them
    fresh to md's size; the same-count hot path stays fully allocation-free. Memory-retention note is
    still true (Clear may drop/rebuild the GPU allocation on downsize — the old transient double-buffer
    cost, but only when counts change, not on the common identical rebuild).
- **COLLIDER RE-COOK TRAP (the important one):** old code swapped `sharedMesh` to a NEW instance each
  apply, implicitly re-cooking the MeshCollider. With a persistent shared instance the reference never
  changes, and a MeshCollider does NOT republish its baked physics mesh on vertex mutation → a
  FullRebuildChunk on a collider-active chunk would update visuals but keep stale physics (player
  falls through a rebuilt wall). Fixed with the explicit null→assign used by `PatchRegion`
  (`_mc.sharedMesh = null; _mc.sharedMesh = _mesh;`).
- **Threading:** the pool is main-thread only by construction (ApplyMerged + Release both run on the
  main thread); documented, no lock.
- Verdict: H1 rejected, H2 confirmed (with the vertex-count shrink correction above). Shipped in `1dv`
  plus a follow-up fix commit. No behavior change expected — play-test that rebuilt collider terrain
  matches visuals (the catch above), a slab-walled chunk deforming flat handles the downsize, and
  F12/reset loops stream cleanly.

## 1du — prop-ring keep-alive + shared dent-debris cube (SHIPPED in `1du`)

Follow-up micro-opt from the 1di/1dq/1dt sweep. Two independent findings, two one-file fixes.

### H1 — prop ring destroy/respawn churn at ring equilibrium
- Walking the ring edge toggles chunks in/out constantly. Old behavior: leave → `ReleaseProps` destroyed
  the spawned GOs; re-enter → `BeginProps` re-rolled the full deterministic stream (~2 destroy + a
  900-tile re-roll per chunk toggle). That churn was the last per-frame hiccup left at the edge.
- CONFIRMED + fixed: keep-alive. `ReleaseProps` deactivates (`SetActive(false)`), `BeginProps` reactivates
  the same GameObjects; the RNG/tiles/cursor survive so no re-roll. `_propActive` becomes the single
  "props visible" flag, `PropsOn` mirrors it, `PropsPending` additionally requires a live stream, and
  `Release()` (chunk unload) still destroys outright.
- Behavior delta vs the `1di` doc: a chopped prop is a destroyed GO leaving a null `_props` slot; the
  preserved stream cursor is already past that tile, so re-entry does NOT respawn it (1di: documented
  deterministic respawn). Kept — "the world stays as I left it" is the friendlier read. Play-test item.
- Caller audit (grep): the five members are touched only by `SyncPropRing`/`StepChunkProps`
  (WorldStreamer.Props.cs) and `ChunkObject` itself. No wider surface to keep old semantics for.

### H2 — CreatePrimitive dent debris per piece
- `SpawnCraterDebris` called `GameObject.CreatePrimitive(PrimitiveType.Cube)` 3–5× per dig/cast.
  Fact-check for the draft comment: CreatePrimitive does NOT allocate a fresh Mesh (Unity's built-in
  cube mesh is shared), so the original "allocates a fresh cube Mesh per piece" claim was WRONG. The real
  saving is skipping per-piece GO + MeshFilter/MeshRenderer initialization — modest, harmless.
- CONFIRMED + fixed: one shared inactive template (`SharedDebrisCube`) + `Instantiate`.
- REGRESSION caught in review: the template strips the BoxCollider, so clones (Rigidbody, no collider)
  **tunnel through the terrain** instead of landing in the crater. OLD debris had per-piece BoxColliders
  and thud-crumbled in the pit (the 1de "explode like pickaxe" look). User chose "collider-less is fine":
  accepted — brief up-burst, sink below-ground, `Destroy` at 2.5 s. Documented honestly in PROGRESS.
- Verdict: H1 + H2 confirmed; both shipped in `1du`. H2 carries an accepted visual trade-off.

Follow-up to 1dq (which left background mesh/noise work untouched by design). Second pass over the
costliest CPU: `BuildMergedMeshData`.

- Count: chunk = 900 tiles, 4 verts each → **3,600 `TerrainBandColor` calls per chunk**, each
  re-running `GetHeight` (5 octaves → ~18,000 `PerlinNoise`), plus pass1/pass3 `EdgeHeights` noise
  fallbacks for out-of-chunk borders, plus separate 961 corner-grid samples upstream in the chunk
  build.
- Observation: colors key on the **pristine noise surface** at world corner coords; those corners are
  pure functions of (seed, x, z). A chunk owns at most 31×31 = 961 distinct corners. => memoize.
- H1 — reuse `tile.Data.Heights` (corner grid) instead of re-sampling noise: REJECTED. Heights on a
  deformed tile are the SAVED height (== vertexY), so colors would read depth ≈ 0 everywhere → all
  grass. The code deliberately compares against the pristine surface so a dug tile shows strata.
  Getting this wrong would silently kill strata colors on all carved terrain.
- H2 — thread a per-build dictionary through the band + border paths. CONFIRMED. Cost: one
  Dictionary with ≤961 entries per chunk build, thread-local, GC-pausing nothing (allocated once per
  build). Determinism preserved (same seed/coords → same value; memo never changes the outcome).
- H3 — share the memo with the corner-grid sampling in WorldStreamer.ChunkBuild: possible but couples
  two call paths; skipped for now (961 samples × 5 octaves stays, the band sampling collapses).
- Verdict: 3,600 → ≤961 GetHeight/chunk (~3.7×), byte-identical colors, no public-signature break
  (PatchRegion/terrain-aim use the kept 4-arg overload).

## 1dr — per-frame component lookups (SHIPPED in `1dr`)

Hotspot scan prompted by "need to optimize the game even more": profile-by-reading the per-frame Update
loops (no profiler on this machine; rule 3 = read + grep).

- **PlayerController** was the champion: 14 raw `GetComponent` calls per frame across Movement/Combat/Stamina
  (MaxHP, MaxStamina, HandleMovement, HandleStamina, TakeDamage, aim paths). All target permanent
  components on the player root. Decided: lazy accessors whose null case re-scans (safe vs.
  CombatController added late by WeaponRigBuilder), because caching a null forever would silently break
  late-rigged combat.
- H1 — cache once in Start: REJECTED for combat-specific refs (WeaponRigBuilder adds CombatController
  after Start on first rig). Lazy accessor chosen instead.
- H2 — cache CombatController in Awake/Start era: same flaw. [decide: lazy]
- **Wheel/skill-bar**: per-frame `GetComponent<SpellCaster>`/`GetComponent<CombatController>`/
  `GetComponent<SkillBindings>` — same fix pattern (cache against player swap, MagicWheelUI already
  had that idiom for `_caster`).
- **PlayerBarsHUD status strip**: `getComponent`×4 + `SetActive`×10 every frame. SetActive guard fixes
  the pointless native toggles; a 3-frame poll is invisible (values are whole-second countdowns) and
  cuts the scan by 2/3. Rejected gate-by-status-change because statuses are added/removed by other
  systems with no notification channel.
- **AudioManager**: `Camera.main` per frame → cached field; `Crossfade` wrote both volumes every frame
  even at full settle → settle guards `> 0.0001f`.
- **ChunkLodManager**: unguarded `gameObject.SetActive(cond)` per chunk per pass → activeSelf guard.
- Left alone (each already gated or event-driven): EnemyHealthBarHUD (0.5 s scan), InteractionPrompt
  (1/3-frame raycast), SittableSeat.FindNearest (bounded list), menu/modal builders (on-open).
- Verdict path: each change is semantics-preserving (null-fallback identical); the remaining greps
  confirm the one-time/event-driven frames.

## 1dq — "load only those with neccessity" → collider-on-demand (SHIPPED in `1dq`)

User: "currently the entire everything in 30 radius is loading at the same time, so would there be a
way to load only those with neccessity without reduceing the range"; clarification: "only load the
collider of those near the player and and magic".

- **Frame of the ask:** the WHOLE radius-30 square visibly streamed in at once (3,721 chunks: 900-tile
  meshes + merged colliders + props). The user explicitly does NOT want the render range cut. So the
  "necessity" axis must be *what physics needs*, not what renders.
- H0 — only mesh-upload nearby, keep all 900-tile per-tile NO builds at full radius: equivalent cost to
  today at boot; H0 rejected as not satisfying "optimize".
- H1 — shrink the streamed square but keep radius 30: rejected by the user (would reduce the render
  range they can see / walk to).
- H2 — collider on demand only (1dq): stream everything, gate the merged MeshCollider. Evidence it's
  the right axis: chunk creation passes a `buildCollider` flag to the SAME path that already renders
  meshes; collider cooks (~7,200 tri/chunk) + broadphase bodies are exactly the expensive per-chunk
  physics objects, and every gameplay raycast probe (player, SpellCaster ≤40 m, NavGrid, Tornado,
  ToolManager, Fishing) uses ranges ≪ 240 m — so radius 8 covers all of it.
- **Component risk — long-range spells:** a firebolt's ground probe needs the terrain beneath it to
  NOT fly clean through the far world. => SpellEffect registers the chunk it currently flies over in
  a static registry; reconcile expands by 1 chunk. Design constraint: only track ONE chunk per bolt
  (release old on each boundary crossing) so the registry can't accumulate stale coords.
- **Component risk — ring jump (death/new game/F12):** interior chunks of a teleported ring could sit
  collider-less => the player falls through before the ring re-promotes them. Full-map reconcile each
  poll with the `HasCollider != want` guard closes this: toggles are zero while idle, and a jump just
  flips a square of colliders once (each cooked once).
- **Component risk — rebuilds:** deformation/arena-lane code path re-cooks colliders via
  `FullRebuildChunk`/`PatchRegion`; both must respect the flag or far chunks get incinerated colliders
  back. => `ApplyMerged` stores the collider intent (one authoritative `_colliderActive`), rebuild paths
  read it (`buildCollider: obj.HasCollider`, `&& _colliderActive`), `Release()` clears it.
- **Boot risk:** the placeholder spawn needs a collider BEFORE the first reconcile poll. =>
  `GenerateChunkSync` keeps `buildCollider:true` (single boot chunk, one cook).
- Chosen scope: 1dq = collider gating only. Mesh uploads/noise still cost per chunk at radius 30 —
  deferred to 1dt (noise memo) + 1dv (mesh pooling); props already limited by `PropRingRadius` (1di).
  NOTE these are separate knobs; a future per-chunk draw budget is out of scope (rendering is GPU-bound,
  the collider change targets CPU physics).

## 1dp — "some magic keep switch between big and small continuously" (SHIPPED in `1dp`)

User: "some magic keep switch between big and small continuously which really fuck up the visual".

- **Symptom:** on the magic-model bench, parts of the grid visibly breathe/scale up and down — not
  all models, and not at a constant speed (some flicker fast, some slow-pulse).
- Evidence scan: the bench uses `SpellCaster.CreateProjectileDisplay`, which routes through the SAME
  `AttachDefaultProjectileVisual` builders as live casts. Those builders attach `OrbFx` on every
  shape (Orb/Shard/Debris/Bolt/Lance/Spear/Blade/Splash/Comet/Missile/Dart).
- H1 — models carry a `SpellEffect` scaling them? REJECTED: the display explicitly adds none; no
  collider/launch.
- H2 — pedestal/timing hack animates them? REJECTED: nothing else in NewWorldTestGround touches the
  bench models post-build (they're plain children of a labelled group).
- H3 — `OrbFx` is the animator. CONFIRMED: `OrbFx.Update()` recomputes `localScale = base * pulse`
  every frame with per-mode amplitudes; `Bolt` (±0.22, ~24 Hz × 7 Hz beat) is the worst "big/small"
  offender, `Ember` (±0.22) solid second; `Plain`/`Shard` amplitudes (0.04–0.06) are the subtle ones
  the user wouldn't complain about. Spins (Shard/Swirl/Tumble) add wobble.
- Options: (a) thread an `animate=false` param through 12+ private builders to skip `AddComponent<OrbFx>`
  — cleanest signature-wise but a large mechanical diff touching every shape; (b) strip the components
  inside `CreateProjectileDisplay` after building (`GetComponentsInChildren<OrbFx>` + Destroy). Chose
  **(b)**: one 3-line change fully inside the display path, live casts untouched (they never call
  CreateProjectileDisplay — single caller is the bench). OrbFx is a private nested class, so the strip
  must stay inside SpellCaster — it does.
- Kept the comet-exhaust ParticleSystems on pedestal models (they don't rescale the body). If the user
  later wants a dead-calm gallery, gate `AttachProjectileParticles` too.
- Open: none. Verified by reread + grep (single `CreateProjectileDisplay` caller; `OrbFx` added only
  in `SpellCaster.Projectiles.cs`, destroyed for the display before any child Update runs).

---

## 1do — "there is no enemy in enemy folders, split each enemy race into a folder of each own" (SHIPPED in `1do`)

User: "there is no enemy in enemy folders, split each enemy race into a folder of each own, that
folder would contain the script of enemy from that race".

- **Symptom:** zero enemy folders exist anywhere in Assets. Grep found NO `EnemyType` enum and NO
  `enemy race` concept: every race was a bare string `EnemyId` driving ONE shared `EnemyController`
  FSM + `EnemyModelBuilder.BuildEnemy(id)` for looks. Identical default stats (50 HP / 10 dmg /
  2.5 speed) on ALL 21 races. So "the script of that race" had to be CREATED per race, not found.
- H1 — just MOVE the shared `EnemyController` into each race folder? REJECTED: one shared brain
  cannot also be "the script of each race" without duplication or lying folder contents; subclasses
  are the honest mapping (each folder owns a real, editable script that governs that race).
- H2 — keep spawners on the generic `AddComponent<EnemyController>` and ship the folders
  reference-only? User explicitly chose "Wire spawners to per-race" — otherwise the per-race
  scripts would be dead code. CONFIRMED wiring via new `EnemyCatalog`.
- H3 — where does model-building go? Kept centralized in `EnemyModelBuilder` (each race's script
  sets id; base builds model in Awake). Full per-race model split would duplicate `MakeBlock`
  plumbing and explode the diff; not what "the script of enemy from that race" needs. DEFERRED.
- Stat presets: user chose "Distinct stat presets now". Table designed along archetype lines
  (slime tanky/no-flee, golem armor/DR, dragon/demon elite, mimic stationary ambush with tiny
  aggro, bat fastest + wide perception, skeleton = pristine baseline). Numbers tunable; recorded as
  the authoritative table in game-design §7.1.0.
- Stat application timing: base `Awake` is `private` on the base — subclasses can't override it
  safely, so added `protected virtual ApplyRaceConfig()` called at the TOP of `Awake` AND in
  `ApplyEnemyId`. Field-initializer stats were an alternative (no hook) but serialized Inspector
  defaults on the base would fight them; runtime stamping is unambiguous given the project has no
  enemy prefabs/enemy scene instances (all runtime `AddComponent`).
- `EnemySpawner` trap: existing `if (EnemyPrefab == null) return null;` would have silently killed
  ALL world spawns once the brain moved to the catalog (no prefab assigned anywhere). Removed;
  prefab is now only an optional visual template with its stale brain destroyed.
- File moves preserve Unity GUIDs via `git mv` of `.meta` (EnemyController + BossController); the
  new per-race files get fresh GUIDs (no existing asset references). `Assembly-CSharp.csproj` is
  git-ignored (Unity regenerates) — its stale `Assets\Scripts\EnemyController.cs` entries are
  legacy leftovers, not tracked.
- Dummy regression risk: `SpawnDummy` used to override 9 fields; `DummyEnemy` now carries all of
  them, and the armored variant still needs only `DamageReduction` + aggro ranges — kept identical
  values (cross-checked line-by-line).

## 1dn — "change the player spawn point to be on the test ground" (SHIPPED in `1dn`)

User: "change the player spawn point to be on the test ground".

- H1 — flip `AutoTeleportPlayerOnStart` to default on. The pull-onto-pad in `RunBenchSpawn` runs one
  frame in (coroutines started in Awake don't tick before the first frame), so the player would flash
  on the boot chunk for a frame then teleport. It also self-gates on `IsArenaReady`, and more
  importantly it's a teleport, not "the spawn point". PARTIAL — used as belt-and-braces, not the main
  mechanism.
- H2 — move the actual spawn: `GameBootstrap` creates the test ground at the END of boot (after the
  player teleport), so I reordered it BEFORE the player placement and spawn on `GetSpawnPoint()` when
  `CreatePlatform && IsArenaReady`. Is the deck safe to stand on at that instant? YES — the platform
  mesh/collider is built synchronously in `BuildTestGround` (Awake), no streaming gate like terrain.
  Boot-chunk placement stays as fallback (platform off). CONFIRMED as the core change.
- H3 — `ResetPlayer` (new game + death respawn) only re-homed to the pad if the player had already
  REACHED it (`IsOnOrNearArena`: XZ within 0.6×platform, Y within 6 m). With the pad as default spawn,
  a brand-new game would immediately bounce back to the boot chunk — inconsistent. Dropped the gate:
  teleport to `GetSpawnPoint()` whenever `IsArenaReady`, boot chunk otherwise; deleted
  `IsOnOrNearArena` (only caller was the gate). CONFIRMED.
- Spawn rotation: `ResetPlayer` sets `Quaternion.identity` (facing +Z); `GetSpawnPoint` is at
  `PlatformCenter.z + 0.45·PlatformSize` (south edge), so the player faces AWAY from the deck centre
  at spawn. Accepted — mouse turns the camera anyway; not changing rotation (matches old pad-spawn
  behaviour).
- Double-teleport check: with the toggle on, `RunBenchSpawn.PlacePlayerOnArena` fires on frame 1 to
  the SAME coordinate GameBootstrap already placed — idempotent, no snap-back (records safe pos via
  `TeleportTo`). Fallback guard at the end of `RunBenchSpawn` also still fires only when
  `!IsArenaReady`. No conflict.

## 1dm — "reduce tree and stone spawn ratio to 1/5" (SHIPPED in `1dm`)

User: "reduce tree and stone spawn ratio to 1/5".

- H1 — the ratio is in `ChunkObject.StepProps` (streamed world): each tile rolls `_propRng.Next(200)`
  once for a tree and once for a rock → 1/200 each. "reduce … to 1/5" reads as "× 1/5" (an absolute
  "1/5" would be 40× DENSER than today, so that can't be the intent) → 1/200 ÷ 5 = **1/1000**.
- H2 — did the legacy `WorldBuilder.SpawnTrees(150)/SpawnRocks(75)` also need the cut? REJECTED: that
  path only runs under `EnableLegacyGeneration` (default false — the comment "streaming world: all
  blueprints usable" and Start/GenerateWorld gates confirm legacy generation is off). Only the streamed
  path drives what the player sees.
- Implementation: single const `ChunkObject.PropSpawnOdds = 1000`, both rolls use it (deterministic
  per-chunk RNG unchanged, so chunk-to-chunk placement is still deterministic — just sparser).
- Expected counts (chunk = 30×30 = 900 tiles, `TerrainChunkCoord.ChunkSize`): old ≈ 900×(1/200×2) =
  ~9 props/chunk; new ≈ 900×(1/1000×2) = **~1.8 props/chunk** (poisson, most chunks 0-2, rare tail to
  ~5). A full ring-4 (81 chunks) holds ~150 props instead of ~730.
  WORTHY of a play-test check: at ring 4 the world may now read fairly empty in spots.

### Verdict
- CONFIRMED H1 (1-in-1000), H2 rejected. Implemented as `1dm`. OPEN until the user play-tests the new
  density.


## 1dl — stop spawning enemies, keep the code (SHIPPED in `1dl`)

User: "stop spawning enemy for now but keep the enemy code, i'll test them later".

- H1 — comment out the `SpawnEnemies` lane. REJECTED — the toggles are THE documented off-switch for
  lanes; deleting/commenting invites the code to rot and blocks a simple re-enable.
- H2 — flip `EnableEnemies` to `false`. ACCEPTED. Checked for scene instances first: the only
  serialized copy is `Assets\_Recovery\0 (10).unity` (a backup, `EnableEnemies: 1`); the live game
  adds the component at runtime (`GameBootstrap` → `root.AddComponent<NewWorldTestGround>()`), so the
  C# default governs and enemies stop immediately. The `RunBenchSpawn` guard already keeps the whole
  `SpawnEnemies*` code path intact for later.

### Verdict
- CONFIRMED H2 (default flip is sufficient; no scene edit needed). Implemented as `1dl`.


## 1dk — "place down every magic on the test ground" (SHIPPED in `1dk`)

User: "place down every magic on the test ground so i can look and edit the magic model".

### Step 1 — what IS "the magic model"
- The game has no `MagicModelBuilder`; every magic spell's visual is either a projectile body built at
  runtime by `SpellCaster.Projectiles.cs` (`BuildProjectileBody` switch: Orb-Sphere, Bolt, Shard,
  Lance, Spear, Blade, Splash, Comet, Missile, Dart, Debris + the summonFallingRock boulder) or an
  animated delivery object (SpellZone ring/column, SpellBeam line, SpellStorm strikes, SpellTornado
  funnel, SpellSummon totem/effigy, transient SkillFx flashes).
- User picked scope C: **one display per castable magic spell** (the 90+ MagicTestMatrix roster) + a
  world-TMP label above each. So I re-read the cast path (`SpellCaster.Cast.cs`) to ground how each
  delivery looks before deciding what to display.

### Step 2 — how to display a spell statically
- Projectile spells: reuse the exact live-cast visuals by making the (instance-but-stateless)
  `AttachDefaultProjectileVisual` **static** and adding `public static CreateProjectileDisplay(type,
  shape, rockBody)` — same body + comet-exhaust particles, no `SpellEffect`/collider/launch.
- Non-projectile deliveries (Zone/Vortex/Beam/Storm/Summon/Instant) have **no static projectile** —
  their visuals are runtime-animated and would need live casts. Decision: show their school-colored
  default icon body + the spell name label, so the bench still lists every magic spell with a
  recognizable id to edit. Flagged to the user before shipping; folding in animated delivery models
  is a possible follow-up.

### Step 3 — where on the platform (spatial reasoning)
- Layout constraints read from `NewWorldTestGround.cs`: tool kit at x +0.42·PlatformSize (z −26..+22),
  weapon rack at x −0.42·PlatformSize, enemy rows z −24..−40 (x ±27), dummies z −18 (x ±4), NPC row
  z −0.18·PlatformSize (x +6..+22), farm/livestock/buildings z ≥ +18. Player spawns at z +0.45·PlatformSize.
- Free middle band: x ±0.40·PlatformSize, z −14..+10. Placed the grid there (default 120 platform:
  32 columns × 3 spacer-3u rows ≈ 93u wide × 6u deep). 3u pitch keeps labels apart and rows walkable;
  pedestals/bodies get **no collider** so the grid never blocks movement or E-interaction.

### Step 4 — labels
- World TMP pattern copied from the legacy building signs (`MapBuilder.Nightclub` neon label:
  text/fontSize/alignment/color/outline/sizeDelta, no explicit font assignment) — consistent with the
  codebase; relies on the project's configured TMP default font like the signs do.

### Verdict
- CONFIRMED: the reusable body builders (Step 2) are all static → the static-visual approach needs
  no live caster. Implemented as `1dk`. OPEN until the user play-tests (walk the grid: every magic
  spell visible + labeled; each shape matches the cast; grid clear of other lanes; big icons not busy)
  or asks for the animated delivery models as a follow-up.


## 1dj — enemy HP bar "does not stay on their head" (SHIPPED in `1dj`)

User: "the hp bar of enemy does not stay on their head, fix".

### Step 1 — what anchors the bar, and why does it float?
- H1: bar lags behind a moving enemy. REJECTED — the HUD repositions every frame from the enemy root
  transform (`WorldToScreenPoint` in Update), so it tracks position exactly.
- H2: enemies bob/hover so the root height wobbles. REJECTED — grep of `Combat/AI` shows no
  hover/bob/sin code; enemy models are static cube children of the root built at local zero.
- H3 (CONFIRMED): the bar height is a FIXED 2.2u above the root, but models top out at very different
  heights (model top: slime ~0.43u, wolf ~0.8u, bandit 0.95u, golem ~1.2u, dummy ~1.9u). So bars
  float far above small enemies — reads as "not on their head".

### Step 2 — fix options
- A. Add a dedicated `Head`/top anchor transform to all ~20 model builders and anchor the HUD to it.
  REJECTED — large surface area for no gain: models are static, so a model's top never moves relative
  to the root; a measured bounds top is equivalent at attach time.
- B. Measure the model's renderer-bounds top once per enemy (on pool rebind) and cache the offset;
  keep following the root every frame. ACCEPTED — one-file change; `GetComponentsInChildren` only on
  target-change, never per-frame; fallback 2.2u if the model/renderers are missing.
- Margin `0.25` so the bar sits just above the head rather than inside it. Dragon/demon do have wide
  wings/tail, but the head/horns are still the highest point on those builds here, so the absolute-top
  rule is good enough without special-casing.

### Verdict
- CONFIRMED H3; implemented as B in `1dj` (`EnemyHealthBarHUD.ComputeHeadOffset`). OPEN until the user
  play-tests (damage a spread of enemy sizes and check the bars hug each head).


## 1di — "game too lag" at radius 30: root-cause + prop ring + burst fix (SHIPPED in `1di`)

User reported lag after `1dg` (radius 30). The `1dh` file refactor went first per their choice; this
task is the actual perf work.

### Step 1 — where does the lag actually come from at radius 30?
- H1: draw calls / overdraw from terrain meshes. WEAK — each chunk is ONE merged mesh (~900 quads,
  11k tris), so 3,721 chunks ≈ 3.7-4k draw calls worst case. URP handles that; not the "lag" feel.
- H2: memory allocation / GC from streaming. PARTLY — chunk finalize allocates a mesh + merges arrays
  per chunk, but that's incremental and bounded per tick.
- H3 (STRONG, CONFIRMED): **prop GameObject + BoxCollider count at full load.** Each tagged tile spawns
  a tree AND/OR rock prop as separate cube-based GameObjects with colliders (~450k BoxColliders across
  the ring), because (a) `MapBuilder.MakeBlock` defaults `removeCollider=false` and (b) tree trunks/
  branches are created with colliders. Props are never LOD'd or culled (`ChunkLodManager` explicitly
  skips props; chunk-root culling by distance never engages while inside the render radius). 33k props ×
  (Transform sync + collider) + broadphase cost = sustained drag and spawn burst hitches.
- H4: the per-tick **finalize burst** hitching frames during the initial fill (12-16 chunk mesh uploads +
  MeshCollider cooking inside one poll tick at 20 ticks/s). SEPARATE from H3 (happens during load, H3
  during play), but reinforces the "lag" complaint right after boot.

### Step 2 — user scoping (questions answered)
- Chose: **prop ring** + **only the burst smoothing**. Explicitly NOT chosen: full collider band
  (~120-180 m), prop mesh-merge + instancing.
- Requires: **player + targets stay hit-able**; goal **steady 60 fps on default settings**.

### Step 3 — design reasoning
- Prop ring reuses the existing streaming pipeline (deterministic per-chunk RNG) instead of a new LOD/
  instancing system. Ring must be Chebyshev (matching `StreamAround` square) so "inside/outside" is
  unambiguous at chunk boundaries — props pop at the square edge, exactly like terrain streams in.
- Prop cleanup must NOT touch the chunk mesh/collider (`ChunkObject.ReleaseProps` keeps them), else the
  terrain would flicker out whenever the player turns slightly — the mesh is authoritative for physics.
- Who owns BeginProps? Moved OUT of `CreateChunkGameObject` into `SyncPropRing`, otherwise far chunks
  would queue 900 pending tiles even though nothing ever steps them (StepChunkProps would stream them
  because it only checked PropsPending, not position). `PropsPending` now requires `PropsOn`.
- Spawn chunk safety: the player's own chunk is always within the ring (ring is measured from exactly
  that focus), so boot ground keeps its trees immediately — no "no props at spawn".
- Burst smoothing: instead of hand-tuning constants again (1dg already raised them), made the budget
  ADAPTIVE to the previous frame (`AdaptiveBudgetMs` = base 6 ms × clamp(target/delta, 0.35, 1.2)). A
  hitch, once, self-shrinks the next tick's chunk finalize work; a smooth frame spends the full budget,
  so average load speed is preserved while the per-frame spike is gone. Same pattern (wall-clock 3 ms)
  on prop spawning.
- Rejected: pooling the prop GameObjects for the pop-in/pop-out churn — adds a pool + lifecycle state
  for ~800 props at ring 4; the pop cost (~a few dozen destroy/create) is inside the 3 ms prop budget
  and churn only happens while the player actively walks the ring edge. Cheap enough not to justify it.
  REOPEN if ring is raised a lot or the user wants the collider band.

### Verdict
- H3 + H4 confirmed; H1 weak; H2 partial. Fix shipped in `1di`: prop ring (default 4) + adaptive
  finalize/prop budgets. Deferred by user choice: collider band, mesh merge/instancing.

---


## 1dh — "game too lag, first optimize the files structure" (SHIPPED in `1dh`)

User reported lag right after `1dg` (radius 30). Clarified via question: "optimize the files structure"
= reorganize the C# code files FIRST (their answer also included the game-object structure fix, but as
the explicit "Refactor first, perf after" ordering — so `1dh` is refactor, `1di` is the lag fix).

### Step 1 — what "structure" really meant here
- The folder tree was already sane (`Combat/*`, `World/*`, `NPCs`, `UI/NewWorld`, ...). The real
  structural debt is 25 `.cs` files over ~600 lines (god files: CharacterInfoUI 3321, MapBuilder 2030,
  WorldBuilder 1973, PlayerController 1779, UIManager 1609, SpellCaster 1165, WorldStreamer 1096,
  ToolManager 1076). The repo already has the right convention: `public partial class X` split by
  domain (WorldBuilder.*, MapBuilder.*). So "file structure" = extend that to the gods.
- H1: rename/refolder files → would break Unity MonoBehaviourt GUID/meta references. REJECTED — a
  pure rename invalidates script references. Strategy: KEEP original filenames as the partial cores,
  ADD new `X.Group.cs` files (Unity auto-meta). Verified no duplicate meta GUIDs.

### Step 2 — the split rules that made it mechanical + safe
- Fields NEVER move (serialized layout untouched — Unity serializes by name regardless of file).
- Methods/properties/whole #regions move verbatim; base-class/interfaces/attributes stay on the core
  part; `sealed`/`static` repeated on every part (C# requires matching modifiers);
  `public static partial class MapBuilder` parts — static keyword required on ALL parts.
- New parts copy the original's FULL using block (superset) — unused usings are warnings, never
  compile breaks; this dodges "missing using" entirely.
- Trailing helper classes (WorldBuilder.Fields/Building/ThrownItem..., ToolManager.InventorySlot...)
  stay put.
- Partial classes: a MonoBehaviour's asset reference follows the file whose name matches the class —
  the cores keep those names, so scene/prefab references survive the split.

### Step 3 — verification reality-check (elsewhere I caught a fabrication risk)
- `git diff -U0` on every original: all hunks are pure deletions (+ `partial` keyword). Small "+"
  counts that appeared got scrutinized against `git show HEAD:<file>`:
  - CharacterInfoUI "+50": TreePan pan/zoom class, RaceNode colors, BuildRaceTree helpers — all
    PRESENT at HEAD, so the "+" is diff repositioning after big deletions, not invented code.
  - PlayerController "+3": IgnoreInput property present at HEAD (it was genuinely un-indented at
    line 23 originally — agent kept it verbatim, causing a repositioning hunk).
  - Per-family line totals are preserved (delta ≤ 64 = new-file header comments + using supersets).
- Grep: `partial class X` file counts match the created files exactly; moved member names unique
  across Assets/Scripts (the few "dupes" — ApplyHit/BuildCafe/ResetTerrainSaves/TakeDamage — were
  same-named members on DIFFERENT classes, verified per-hit).

### Verdict
- H1 rejected; the mechanical partial-split convention is confirmed as the low-risk path. Shipped in
  `1dh`. No compile (rule 3); user play-tests. Lag fix pending as `1di` (collider band + prop ring +
  prop mesh merge/instancing to cut ~450k colliders / ~33k renderers at radius 30).

---

## 1dg — "increase terrain render range and need to increase the loading speed even more" (SHIPPED in `1dg`)

User asked for (a) more render range and (b) faster loading. Clarified via questions: radius **30**, an
**aggressive** burst-load profile, keep the distance fog.

### Step 1 — where does loading speed actually sit?
- H1: mesh generation on threads is too slow. PARTLY — each chunk builds 900 tiles + a merged mesh on
  a ThreadPool thread (~961 noise calls), but that work was already backgrounded; the pipeline was not
  throttled by raw CPU but by pacing.
- H2 (CONFIRMED): the pacing constants cap throughput. `PollInterval = 0.1` s means only ~10 ticks/s;
  `FinalizeChunks` caps at `min(ChunksPerFrame, 8)` chunks AND a 6 ms wall clock — ≈ 80 chunks/s max;
  `MaxInFlight = 8` limits how far ahead the background runs. At radius 20 (1,681 chunks) the full fill
  is ~21 s, so the `GameBootstrap` comment "fills the render radius over ~1.5 s" was stale — it predated
  the bigger radius and nothing accelerated the pipeline with it.
- Evidence for the pacing being the binding constraint: main-thread apply per chunk is tiny (a
  961-vertex/1800-tri flag upload + single collider cook) vs the 6 ms budget and 8-chunk cap. So the
  cheap work was artificially rationed.

### Step 2 — the speed levers (all in WorldStreamer.cs)
- PollInterval 0.1 → 0.05 (2x ticks/frame budget). Each tick also re-runs StreamAround (unload sweep +
  ring enqueue) and DispatchPending (distance sort) — cheap at ~4k chunks.
- FinalizeChunks: hard cap `8 → 16`, wall-clock `6 → 12 ms`. Kept the cap so a pathological frame can't
  overrun; kept the wall clock so slow cooks (collider bake) don't blow the frame.
- MaxInFlight `8 → 24`: more background backlog so the faster finalize never waits on emptiness.
- PropTilesPerTick `40 → 120`: props exist to dress the ground; at the old 40 tiles/tick the ~1/200
  tree + ~1/200 rock density would take minutes to dribble in across 3,721 chunks' 3.3 M tiles.
- Net: ≈ 320 chunks/s (4x today) → radius-30 fill ~10–15 s.

### Step 3 — range knobs checked for side effects
- `ChunkLodManager.EffectiveCullDistance()` = `max(CullDistance, (Radius+1)*ChunkSize)` — auto-scales,
  no edit needed. At radius 30 that is 930 m; camera far plane default ≈1000 m just covers the axis
  (diagonal spurs may flirt with the clip → play-test item, see PROGRESS).
- Memory/objects grow quadratically (1,681 → 3,721 chunks); each chunk = 1 GameObject (mesh + collider)
  + ~9 props (~150 cubes) on average. Verified nothing else hard-codes the old radius/pacing
  (grep `Radius = 20`, `PollInterval`, `MaxInFlight`, `ChunksPerFrame`, `PropTilesPerTick`).

### Verdict
- H1 rejected, H2 confirmed. Changes shipped in `1dg`; play-test checklist in PROGRESS `1dg-status`.

User: "the animals dont have damage interaction so they basicly invincible right now". Clarified via
questions: at 0 HP → "explode and part flung everywhere" (the old-game voxel burst); club stays
non-lethal so cage capture keeps working.

### Step 1 — why were they invincible?
- H1: no collider → hits never connect. REJECTED — `Livestock.Awake` adds a root `SphereCollider`
  (radius 0.4) and the club's raycast hits it fine.
- H2: no health system. Mostly NOT the issue — `TakeDamage` already exists (red flash, flee/fight,
  knockout → capture, 15 s recover), reachable today via the club.
- H3 (CONFIRMED): the combat pipeline only routes damage to `IDamageable` — every sink greps to
  `target.TryGetComponent<IDamageable>` (HitboxSystem, RangedProjectile, SpellCaster, SpellZone,
  SpellStorm, SpellBeam, SpellTornado, SpellDoT, IEffect/ClassEffect/RaceEffect, SummonedAlly).
  `Livestock` (or its root) never implements it, so every non-club attack no-ops. Played weird: the
  spell pipeline computes full damage numbers and pops them above the animal even though nothing is
  applied — hits visibly "connect" while HP never drops, exactly the reported "basically invincible".

### Step 2 — 0-HP behavior (design fork, user answered)
- Existing knockout → capture was the only outcome; user wanted the old-game explosion. Kept BOTH by
  splitting the sources: lethal damage (any combat hit reaching 0 HP) → `ExplodeAnimal()` (mirrors
  `EnemyController.ExplodeModel`, 1dd — detach parts, BoxCollider + Rigidbody mass 0.3,
  `AddForce(dir*8+up*6)` torque, cleaned after 5 s) then destroy the animal; the club gets a new
  non-lethal `KnockDown()` (stun, no HP loss) so cages/capture still function.
- Return type: `IDamageable.TakeDamage` is `int` (remaining health). Club callers previously ignored
  the return; changed signature to match — verified the only other `Livestock.TakeDamage` caller is
  the club (grep) and it now calls `KnockDown()`.
- Statuses: burning/chill DoTs attach now that animals are damageable; knocked-out animals early-return
  from `TakeDamage`, so a DoT can't kill a downed animal mid-capture. Knockback shoves the animal
  rigidbody — acceptable flavor of "damage interaction".

### Step 3 — latent debris-cleanup bug caught during build (fixed here)
- Both the shipped `EnemyController.ExplodeModel` (1dd) and my first `ExplodeAnimal` draft scheduled
  cleanup with `StartCoroutine(DestroyDebris)` on the controller, which `Destroy(gameObject, 0.2f)`
  destroys right after — Unity kills coroutines when their component dies, so the ~5 s cleanup never
  ran and debris would accumulate forever (the 1dd play-test item never confirmed this).
  Fixed by scheduling `Destroy(part, DebrisLifetime)` on EACH part (live objects), dropping the
  coroutine + list. Also trimmed `EnemyController`'s now-unused `System.Collections*` usings (its
  `_targets` field is fully-qualified `System.Collections.Generic.List`, so removal is safe).
- Noted for future bursts: never put a cleanup coroutine on an object that is being destroyed in the
  same breath.

### Step 4 — no-loot decision
- User only asked for the explosion; deaths grant nothing. Exploded animals are removed and the
  spawner (`LivestockSpawner`, 45–80 s trickle, Max 20) replaces them. If hunting loot is wanted it
  is a separate task.

---

## 1de — "dirt explode or rock debris when creating a dent, depends on the layer" (SHIPPED in `1de`)

User: "when creating dent, make a dirtblock explode as well or rock debris depends on the layer,
the explosion would much like when stone got destroyed by pickaxe".

### Step 1 — what IS a "dent", and where are they created?
- "Dent" = the crater excavation (`TerrainShape.Crater`), the ground disturbance shared by the tools
  (shovel/pickaxe via `TerrainDeformer.Dig`) and Earth magic (zone casts, Storm strikes, the Stone
  Shard projectile impact). Hypothesized the right hook is `WorldStreamer.DeformAt`, the single
  funnel every crater goes through.
- **Confirmed by grep**: every crater call site routes through `TerrainDeformer.Apply` →
  `WorldStreamer.DeformAt` — `SpellCaster.ResolveZone`, `SpellCaster` charged casts,
  `SpellStorm.DeformGround`, `SpellEffect.ResolveProjectileImpact`, `ToolManager` shovel/pickaxe
  `Dig`, and the QA strata lane on `NewWorldTestGround`. One hook covers all of them.

### Step 2 — "depends on the layer": where does the layer color come from?
- The strata system from `1cs` already paints the pit walls: `ChunkMeshGenerator.TerrainBandColor
  (seed, x, z, vertexY)` returns grass green ≤ 0.35 m depth, dirt-brown to ~2.3 m, stone-grey ≥ 2.7 m
  (`StoneBandEnd`). Bonus: `vertexY` can be the floor height at the crater center
  (`WorldStreamer.CurrentHeightOf`), so the debris exactly matches the stratum the dig just reached.
- Rejected alternatives (dead ends): (a) hard-coding `DirtBrown` for tools / `StoneGray` for spells —
  spells and the pickaxe both cross bands, so a fixed tint would lie half the time; (b) spawning from
  `ToolManager` only — leaves spell dents (the biggest craters) bare. Verdict: single `DeformAt` hook
  + `TerrainBandColor`.

### Step 3 — the "like stone destroyed by pickaxe" look
- `WorldBuilder.SpawnRockDebris` (WorldBuilder.RockMining.cs:178): 3–6 cubes sized from volume,
  `Color.Lerp(gray, black, rand*0.5)`, mass = volume·1000, up-bias velocity (4–8 up), spin. Its
  pieces persist forever (by design — they're smashable again). For dents that would litter across
  repeated digs, so the new `DentDebris` copies the physics but `Destroy`s after 2.5 s (the
  prior-impact-burst timeout).
- **Hypothesis H6 (from `1cx` re-checked):** do freshly-carved debris chunks fall through the chunk
  collider while it rebuilds mid-frame? The previous investigation concluded the crater floor is
  solid walkable terrain and chunks fall freely on it — same reasoning holds; debris spawns at floor
  + 0.08 m. No special handling.
- **Double-burst risk (confirmed, fixed):** `SpellEffect` already threw its own grey
  `SpawnImpactDebris` (3–5 grey cubes) on every Earth projectile impact. With the DeformAt hook that
  would fire twice at the same point. Decision (user-backed): delete `SpawnImpactDebris`/its call,
  let DeformAt own ALL crater debris. Grep after removal: the only remaining`SpawnImpactDebris`
  symbol is `RandomEventManager`'s unrelated `(Vector3, Transform)` method — untouched.
- `SpawnDigPuff` (the tools' quick 1 s shard poof) deliberately kept — it is the stroke accent, the
  new debris is the excavation chunk ("explode ... as well").

### Step 4 — edge cases
- Unloaded terrain: `DeformAt` early-returns when `newHeights.Count == 0`, so no debris without an
  actual edit. Raised shapes guarded out (`shape == TerrainShape.Crater` check).
- Massive spell radii: debris count/size fixed small (3–5 cubes, 0.08–0.16) regardless of radius —
  authored look, no per-cast allocation spike.

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
