# Game Design Document — "New World" (Working Title)

## 1. Game Overview

**Genre:** Open-World Action RPG (Elden Ring-inspired)
**Platform:** Unity (PC primary, Mobile secondary)
**Multiplayer:** None — single-player. 1hz removed the whole networking layer (see §4, §6.4).
**Core Loop:** Explore → Fight → Grow → Craft → Dominate

Seamless open-world with real-time action combat, classless progression via a **6-category skill-XP system**, an **11-stat** system, **15 unlockable classes**, and a **22-race system** (with passive-only racial kits), procedurally generated seed-based chunk terrain, and all existing CountryLife systems retained as optional side content. Combat is built on a **3-genre equipment** set (21 slots), an expandable **weapon architecture** (§3.6, Melee/Ranged/Magic), a **spell-casting pipeline** (§3.8) for magic, and **10 damage types** with **7 status effects** (§3.7).

---

## 2. World Generation System

### 2.1 Seed & Coordinate-Based World

- Every world defined by a **numeric seed** (long).
- World infinite in XZ plane, divided into **1x1 unit chunks**.
- Each chunk identified by **(chunkX, chunkZ)** integer pair.
- Same seed + coordinate always produces identical chunk (so a seed alone is enough to describe a world).

### 2.2 Chunk Structure (900 Flat Tiles, 31x31 Corner Grid)

A terrain **chunk** is a 30x30 metre area (**TerrainChunkCoord.ChunkSize = 30**) divided into
**900 tiles of 1x1 m**. Each tile is a single **flat quad** built from its **4 corner heights**
(NW/NE/SE/SW), split into 2 triangles:

```
NW ────────── NE          NW = corner (x,   z+1)
 │          ╱  │          NE = corner (x+1, z+1)
 │      T1 ╱    │          SE = corner (x+1, z)
 │        ╱      │          SW = corner (x,   z)   ← tile-local (0…1), X+S is origin
 │     ╱   T2    │
 │  ╱            │
SW ────────── SE
```

- The chunk holds a **31x31 world-corner grid** (`CornerGridSize = 31`) of heights; each interior
  corner is shared by 4 neighbouring tiles, boundary corners by 2, so adjacent tiles blend into a
  smooth heightfield with **zero gaps**.
- Pristine heights come from the 5-octave Perlin surface (§2.3) sampled per world corner; persisted
  edits (§2.6) override whole tiles (4 corners each) or whole corners across tiles.
- Each chunk is finalized as **ONE merged mesh** (single GameObject + single MeshCollider): all 900
  top quads first (contiguous per-tile blocks), then vertical side walls only where a whole-metre
  slab/older-carve discontinuity sits between neighbours. `ChunkObject.PatchRegion` re-skims a
  touched tile rectangle in place through the per-tile block table (`TileVertexBase`/`TileVertexCount`),
  so deformation never re-runs a full 900-tile rebuild.
- The **31x31 corner lattice** was built so the detail-LOD children could decimate it (a regular
  axis-aligned sample — every 2nd/3rd corner) at ~1/4 / ~1/9 of the full mesh cost. **Since 1f6 those
  children are gone** and so is their exception: a chunk now draws its ONE root mesh at every
  distance, so there is no band to hold a finer stride for. The lattice itself is retained because
  the collider cook, the patch restamp, the low-poly root re-emit and the F3 corner audit all read
  it.
- A tile whose 4 corners differ by more than the **refine threshold** (§2.10) renders as a 2x2
  sub-quad grid instead of one quad — same smooth heightfield, but steep slopes split into several
  smaller faces so the corner-grab editor (§3.8) can bite them level by level.

**Seam contract (ownership + audit).** A world corner has exactly ONE value: the canonical Perlin
surface (§2.3) plus whatever whole-corner edits are persisted for it (§2.6). Every chunk is placed at
its exact block origin (`chunk.X * 30 m`) on a lattice whose node spacing divides 30 (3 m facets when
the 1hi facet look is on — dormant since 1ia, 6 m while 1hx was default; 1 m tiles; the 2 m detail
LOD stride is gone with 1f6), so two adjacent chunks sample identical nodes at identical world
tiles. The 31x31
**corner lattice** is not re-derived from noise at mesh time — it is copied out of the *owning tile's*
stored corner, so the contract rests entirely on the **ownership rule** holding: node (gx,gz) stands
on world corner (Ox+gx, Oz+gz) and must copy the corner of exactly that point from the tile that
carries it (interior → tile (gx,gz) SW; north row → tile (gx,cs-1) NW; **east column → tile (cs-1,gz)
SE**; far corner → tile (cs-1,cs-1) SE). A rule that reads a *different* corner of the owner tile
silently shears the column and breaks the seam while every tile-vs-tile check still passes.

**Consequence: a pristine world is seam-free by construction, and any see-through gap has exactly one
of five causes:**

| # | Cause | What it means |
|---|-------|--------------|
| A | **Corner divergence** — a chunk's lattice was stamped with a height that is not the corner the node stands on (wrong owner slot, or an edit that never reached the owner tile) | the two surfaces part by a sliver along a shared boundary |
| B | **Short/hidden root** — the chunk is loaded but does not draw its whole 30 m (inactive root, missing mesh, vertex- or bounds-short rebuild) | ground is missing where a chunk is supposed to be |
| C | **Interior hole** — a chunk position inside the loaded ring holds nothing at all | not a seam: a chunk never arrived, or was dropped |
| D | **Far/real rim step** — the far-shell cell meeting the last real ring renders its own heights | a step at the near/far boundary, both meshes locally correct |
| E | **Mixed-version resident set** — the loaded chunks were not all built by the same version of the generator (AGENTS rule 11) | *not a bug in the code at all*: a stale screen. Every height in memory agrees with itself; the world on screen is simply not the world the code describes |

Cause **E** was added in 1hy. It is listed as a cause rather than a footnote because it is the one
that makes every other reading invalid: it looks exactly like A or C by eye, and no in-memory check
can detect it, because nothing in memory is wrong.

**(1hy restored the on-demand measurement of these causes — the F3 lane, and the letter collision is
deliberate.)** `WorldStreamer.RenderedCornerAudit()` (bench key **F3**,
`WorldStreamer.CornerAudit.cs`) walks the DRAWN state of the whole resident set and prints a `VERDICT`
naming the first failure plus the world XZ to walk to. Its section letters are **its own, not the
taxonomy's** — the mapping is:

| Lane section | Measures | Cause(s) it can confirm or clear |
|---|---|---|
| **A fingerprint** | distinct `(MeshStep, vertexCount)` buckets + step drift across the loaded set | **E** |
| **B void** | any chunk footprint in the fully-owned ring (`0 .. view + FarOuterKeep`) with neither a visible real chunk nor a live, unshadowed far cell | **B** and **C** |
| **C-R1 coverage** | does every loaded chunk place a rendered vertex AT each of its four corners | **B** (vertex-short root) |
| **C-R2 agreement** | do the chunks at a world node agree on the corner height, and does each rendered corner match its **own** lattice | **A** |

Since 1i0 the C-R2 line also carries a `[cause of those N node(s): ...]` classification, read off
whether each contributor's height equals **untouched world noise** at that node — the same
3-argument `GetHeight` overload the pristine corner fill uses, so "pristine" means the identical
value the build would have produced had nothing been written. That is what separates a corner
**written on one side only** from one where every side was written and differs, and — the case that
would refute the save-path explanation outright — from a node where every side sits exactly on
pristine noise and the chunks still disagree.

Worth recording as design, because it is a contract and not an implementation detail: a corner edit
writes into every **loaded** tile that touches it, and it crosses chunk seams (`ApplyHeightEdits`
iterates world tile coordinates, not chunks), so a corner is *not* per-chunk data. But it reaches
only what is loaded, and nothing reconciles the far side afterwards — `ReconcileModifiedBorders`
repairs slab-wall bottoms, not corner heights. So the pair (write) and (repair) disagree about
whose job a shared corner is, and that gap is exactly cause A showing up as a visible crack rather
than the sliver the original 1hk bug produced.

**Section D (1i1) — the near/far boundary.** The player's report that the gap appears *only* at the
outer x/z corner tile put the seam between two different **owners**: the outer ring of loaded chunks
meets a far cell, and there only one quadrant is a real chunk. Section C is blind to this by
construction — it walks `_loadedChunks` and compares loaded chunks to each other — so section D
compares the loaded ring's corners against **the far cell's own uploaded mesh** at the same world
point, and reports `real-vs-far dY`.

Note that "the far cell is live there" and "the far cell is at the same height" are different
properties. Section B tests the first and correctly passes; a see-through crack satisfies the first
and fails the second, and the low-poly root has no side walls to hide the difference. The mechanism
the design predicts is a split source of truth: a far cell's corner grids are built from
`ChunkTileMod`s read out of the **save files**, while a real chunk renders from its **live** tile
data — and at the rim the far side is past every ring a dig can reach, so it is pristine noise by
construction while the loaded side may be edited. Nothing reconciles the pair.

So the lane's "B" is the taxonomy's B *and* C, and the lane's "C" is the taxonomy's A — the letters are
positional, not semantic, and the tables are the authority. Ordering is **not** causal: the fingerprint
runs first because it is the cheapest check *and* the premise test for the rest — if the resident set
is mixed, the other two sections describe a world that is not on screen.

Two properties of the void walk that are load-bearing, not incidental. It is scoped to the band the two
owners **promise** (real chunks to `view + 1`, far cells to `view + FarOuterKeep`) rather than to the
visible radius, because past that band real chunks are dormant-and-hidden with no far cell owner and a
naive scan would report ~84 correct, expected, invisible "voids" at 630 m that drown the signal; and it
does not re-derive `ChunkDistanceCull.EffectiveCullDistance()` (private) into a second copy, because a
second spelling of a private constant is a copy that rots when the cull side changes.

**What the removed lanes established is worth keeping, because it is about *the question*, not the
tool: a check only speaks for the layer it reads.** `ChunkValidator` compares TILE heights
tile-vs-tile, which are exact by construction, so it is structurally blind to a lattice-ownership bug
(cause A); a lattice comparison is in turn blind to a renderer that draws its corner vertex from
somewhere other than the lattice it stamps — that case reported "worst dY 0 OK" and still parted at
the corner, which is why section C reads `Mesh.vertices` and not the lattice. A future gap fix should
expect to *rebuild* one of these measurements rather than to read cause A off an existing validator.

**The crater lane (1in/1io) — dents, and whether a missing rim is a resolution problem or an
unauthored shape.** `WorldStreamer.CraterAudit()` (bench key **F13**, `WorldStreamer.CraterAudit.cs`)
answers a
question the F3 lane cannot: a carve *is* in the data and *is* drawn, so every seam-cause reading is
clean while the shape is still wrong. Read-only, like F3 — no rebuild, no re-stamp, no poll — so the
numbers describe the frame the key was pressed on.

| Lane section | Measures | What it separates |
|---|---|---|
| **A fingerprint** | `(BuildStamp, MeshStep, vertexCount)` buckets across the loaded set | the premise (rule 11) for the other four |
| **B resolution** | deepest dished corner, corners spanned, dish radius vs the render path's sampling gap | "drawn" from "smaller than promised" |
| **C profile** | per-ring min/mean/max dig out to the dish edge | monotone cone from bowl-with-rim |
| **D expressibility** | corner spread vs the 1ew trigger, corners **above** pristine, adjacent gaps | "not authored" from "authored but coarse" |
| **E terraces** | level ladder + gap spread over **carved** corners, flat-tread fraction of touched tiles, riser/boundary lattice edges, tallest riser as a collider slope vs the live `CharacterController`, chunk-rim side-wall bands | "deep" from "**stepped**" — A/B/C/D all describe a smooth bowl in detail and none of them can tell it from a terraced one |

Section order is premise-first, not prettiness-first (rule 7): A runs before the shape sections because
if the resident set was built by two versions of the generator, B–E are evidence about a world that is
not on screen. E runs last of the shape sections because it is the only one that needs a well-formed
footprint to mean anything — it reads the carve's own height ladder, which is a null question when
there is no carve, so **E states its own premise and refuses to classify without it**.

**E's membership and value deliberately use different references.** A carve writes `refY + offset`
against ONE reference height taken at the impact point, so the pristine slope underneath is
*overwritten* rather than added to, and the set of carved heights is exactly the set of authored
offsets plus a constant — the ladder, directly. But E's footprint deliberately reaches half a metre
past the last dished corner, so it also contains untouched terrain sitting at arbitrary noise heights.
Measured as raw heights, those would drown the authored levels in a continuum and report
"CONTINUOUS" for a perfectly quantised carve. So membership is decided by deviation from pristine
(**either direction** — the 1ez rim is a raise and a rim terrace is as much a step as a dish terrace)
while the value measured stays the raw height. Every count is over carved corners or touched tiles,
never the raw band, and the band size is printed beside them. When B clears the dig threshold and E
finds no corner deviating by it, that is a **contradiction** reported as `UNKNOWN`, never as `0`.

E reads `stepOffset`/`slopeLimit` off the live `CharacterController` rather than against literals
(rule 8): this project writes `stepOffset = 0.5` in exactly one place and never assigns `slopeLimit`
at all, so a copy of either number in the lane would be a second spelling that rots silently. The
tallest riser is quoted as a slope at the 1 m lattice pitch because the collider is built from the
same lattice with **no vertical strip pass** — the player meets the *shape*, not a wall.

**E shipped before the change it measures, and 1f3 is the change it was measuring.** E was written
against the pre-1f3 carve: `target = current - s * CraterStep` with a smoothstepped **cone** plus 1ez's
lip, nothing quantised. The user then read that carve as a "smoothed out blanket", which is a
complaint about the profile's *continuity* that no amount of depth or rim fixes — so 1f3 changed the
two things E can tell stepped from continuous, and E is now the acceptance readout for them:
- the profile is a **spherical cap**, not a cone. `capR = (reach² + capDepth²) / (2·capDepth)` with
  `capDepth = min(CraterStep, reach)`, dug as `sqrt(capR² − d²) − (capR − capDepth)` — `capDepth` at
  the impact point and exactly 0 at `reach`, so the feather to untouched ground is unchanged and only
  the shape between them moved. `reach` is radius + 0.5 m of feather, so the smallest real dig
  (pickaxe, radius 1.0) reaches 1.5 m against a 1.1 m `CraterStep` and the `min` is defensive only.
- the signed `offset` is **snapped to a terrace ladder** of `clamp(reach · 0.25, 0.30, 0.80)` m before
  it is written — a fraction of reach so one ratio reads as terracing at every size.

Acceptance on F13: section E's ladder verdict turns from `CONTINUOUS (no terrace exists)` to
`UNIFORM LADDER (quantised carve)`, with `riserEdges > 0` and a net rim terrace of `+0.475 m` at
`t ≈ 0.80`. Numbers are derived, not measured — the readout has to confirm them on the resident set.

**The snap is applied to the OFFSET, not to the write**, which is what keeps both invariants: the lip
still `Max`es against `current` (repeat cast → same offset → same target, still idempotent) and the
excavation still subtracts a non-negative amount (still an unbounded downward ratchet, §1cv). The
cost is stated rather than hidden — a cast now moves the floor by its offset rounded to the nearest
terrace, so the per-cast depth is `CraterStep` **within ± half a terrace** (0.30–0.80 m over the clamp
range) rather than exactly 1.1 m; a 1.9 m-reach projectile's first cast digs 0.95 m, not 1.1 m. The
deep core cannot round away (`CraterStep / terrace ≥ 1.375`), so the ratchet stays unbounded in
practice, not merely in intent.

**Why the offset and not the absolute height.** Quantising the *absolute* Y would give genuinely flat
treads on a *slope* (two adjacent corners land on the same rung of a world-wide ladder). It was
rejected for two reasons, both of which are the reason this is a 1m lattice and not a data model: it
makes the crater's depth a function of the ground's absolute elevation (± half a terrace, varying
across the map), and — the expensive one — it floods `ChunkMeshGenerator.IsFlatTile`, whose
`FullRebuildChunk` path is sized for slab side walls. Offset snap keeps flat treads to ground that is
genuinely flat (including the test platform) and leaves sloped ground on ramps-with-hard-breaks.

**1f3 re-derived `CraterRimLift` (0.55 → 0.90 m).** The cap digs deeper than the cone did at every
radius (≈0.94 m at the lip's inner edge where the cone reached ≈0.47 m), so the old lift would have
left 1ez's rim barely proud of grade (~0.10 m) — a new profile silently regressing an old feature.
0.90 m restores the net ≈0.44 m the cone produced, still under `stepOffset` (0.5 m), so the lip stays
a bump you walk over rather than a wall. The lip/depression crossover moves out slightly, t ≈ 0.68 →
≈0.72. **The rim wall is now steep by design**: 60° at a 1.9 m reach, 73° at a 1.5 m tool dig, against
a cone's 34°. The user does not need a walkable bowl; a walkable crater is a *different shape*, not a
tuning of this one. Slope is quoted at the 1 m lattice pitch because the collider is built from the
same lattice with **no vertical strip pass** — the player meets the *shape*, not a wall, and above
`slopeLimit` (never assigned by this project) they cannot climb out. Cliff traversal off a crater is
a play-test item.

E's own sub-check for spurious chunk-rim side-wall bands is the same rule-7 shape:
`EdgeHeights` falls back to pristine noise outside the chunk, so a *raised* rim on a chunk-rim tile
emits a real slab face that a *depressed* one never does — dormant for a smooth carve, and now
**reachable**, because the terraced lip writes a raised terrace that lands on a chunk-rim tile.

Section D counts corners *above* pristine to identify the rim directly rather than inferring it from
the profile, because a rim is a **positive raise**. Until **1ez** the crater profile in
`WorldStreamer.Deform.cs` was a monotone smoothstep dish (`target = current - s * CraterStep`) that
could only lower — which is the reason a resolution-only fix would have been wrong: no amount of
sub-tile geometry produces a raise that is never written. 1ez added the missing positive term (a
bounded, idempotent lip across the outer band of the footprint, below), so section D's above-pristine
count is now the acceptance readout for the rim. Section D is also scoped to the crater's own footprint
rather than the search band, since the band only locates the crater — and the `Wall`/`Ring`/`Pillar`
deform profiles do raise, so an unrelated one nearby would otherwise be read as this crater's rim.

At the 1ia default (`LowPolyFacets` off, `EffectiveLowPolyStep` = 0) section B reports
`nodeGap n/a (every corner drawn)`: the dish is drawn **in full**, and the limit is shape, not
resolution. **1ez** supplied the shape (the raised, idempotent lip below); the **stored fine lattice is
1ey** — interior fine nodes only, with tile edges left bilinear so the edge-linearity no-crack proof
and cross-chunk seams are untouched — and is deferred until a measurement justifies its save-format
change (a v2 migration; it must also decouple `TryLoadVoxelChunk`'s `version < CurrentVersion` guard
so v1 voxel saves survive the bump). That is the same infrastructure the user wants for caves; **caves
are deferred**, and the existing `SculptVoxelCave` path stays dormant.

**The lane key moved F1 → F13 (1io).** 1in chose F1 by grepping for `Key.F1` and `KeyCode.F1`,
finding no references, and documenting the key as free. It was bound — the **combat-mode toggle**
binds it as `Keyboard.current.f1Key` (`Player\PlayerController.Interactions.cs:521`), the
*property-name* spelling, which neither grep pattern matches. Pressing F1 therefore ran the audit
**and** toggled fighting mode, so any readout taken that way was measured with weapons drawing and
`ToolManager` resetting selection underneath it. F13 is free in both spellings; **F1 is the combat
toggle, not a skill hotkey** — that claim sat in four tooltips and two older task entries and was
never true. `tools\StaticChecks.ps1` check 8 now derives the QA lane keys from their declarations
and fails on any second binding in `Assets\Scripts` across all three Input System spellings, so
this class of collision cannot ship silently again.

### 2.3 Perlin Noise Layers (5 octaves)

Heights generated using **multiple octaves of Perlin noise**, each layer contributing to final terrain shape:

| Layer | Purpose | Frequency | Amplitude |
|-------|---------|-----------|-----------|
| 1 - Continental | Large-scale landmass shape | 0.0012 | 55.0 |
| 2 - Hills | Rolling terrain | 0.004 | 22.0 |
| 3 - Detail | Small bumps and dips | 0.012 | 3.5 |
| 4 - Roughness | Micro-variance | 0.03 | 0.6 |
| 5 - Pivot Angle | Center vertex offset | 0.008 | 1.5 |

**Correlating the generation (1eo):** the 1eo re-weight pushes the octave **mass into the
long-wavelength layers** (Continental + Hills) and cuts the sub-chunk octaves hard — the old
Detail 0.02/5 and Roughness 0.08/1.5 ran at wavelengths below a chunk's 30 m, so a raised tile's
neighbor genuinely did not follow it (the "one tile went up next to a flat tile" checkerboard). Now
an adjacent tile tracks its neighbors: a raised tile sits among raised tiles and flat runs stay flat,
the world reads as rolling terrain, and deformation (Earth spell edits, §3.8) feathers into coherent
land instead of jagged speckle. Net relief is close to before; the ±200 m sanity band is unaffected
(noise max ≈ ±82.6 m = 55+22+3.5+0.6+1.5). **Recommended: `ResetTerrainSaves` once**, since every
unmodified generated corner changes (saved edits persist).

**Seed derivation:** Each noise layer uses `seed + layerIndex * 7919` as its seed offset to ensure different patterns per layer.

### 2.4 Neighbor-Dependent Generation

Each chunk's generation is influenced by its **4 direct neighbors** (N, S, E, W):
- Edge vertices are computed from the shared neighbor's edge (guaranteed seamless).
- Center vertex considers the heights of all 4 corners via interpolation + noise offset.
- This ensures smooth transitions and eliminates seams.

### 2.5 Chunk Loading & Render Distance

- The player controls **render distance** in chunk radius.
- **Default radius:** 30 chunks ≈ 900 m half-width (**1eo**; was 67/≈2,010 m since **1ef**, and 30/≈900 m
  before that at **1dg**). The user asked to cut the loaded range: the boot default and the maximum
  clamp both sit at 30 now (§2.5 note below), so no path — settings slider or scene — can push the
  far shell past ~900 m.
- **Maximum radius:** 30 chunks (1eo — was 160; `GameBootstrap` sets `Radius = MaxRadius = 30` and
  `RenderDistanceController`'s class default/max + inspector range follow, so a future scene asset
  can't re-widen it either).
- **Real chunk ring (near, 1ef):** only chunks inside `NearRingRadius` (default **9** ≈ 270 m) stream as
  full-fidelity `ChunkObject`s — deformable, collidable, prop-bearing, one full-detail mesh each.
  `StreamAround` receives
  the NEAR ring, not the render radius, and keeps one hysteresis ring (near+1) loaded, so the real
  chunk world is 361 chunks (was 3,721 at radius 30) and the collider/prop wins of 1dq/1di ride
  a fixed-size ring instead of scaling with the render distance. Dispatch is **nearest-first and each
  chunk never regenerates** (**1em**: the dispatch loop had no loaded-chunk guard and its cleanup only
  dropped entries that were BOTH pending AND loaded — but finalize clears the pending mark, so
  finalized chunks were re-dispatched forever, refilling the in-flight slots with the same nearest
  chunks and starving the rest of the ring into a permanent ~24-chunk bubble around the player; the
far shell was immune because it skips completed cells, which is why 300 m→~900 m rendered while the
    0-300 m disc stayed empty).
- **Dormant keep-ring (1gc):** a real chunk passing the near+1 hysteresis ring is no longer DESTROYED
  at the boundary it was just generated at. It is **demoted to a dormant state** (root mesh +
  props + collider off; 900-tile bookkeeping, pooled mesh, GameObject and VoxelStore retained) out to
  ring near+1+DormantRingDepth (**default depth 2** ≈ 270-390 m), and the coarse far cell covers the
  hidden chunk exactly as it covered the old destroyed chunk — the dormancy drops the chunk out of
  `_loadedChunks`, so the existing active-shadow rule shows the far cell over it with zero far-shell
  code change. Re-crossing the boundary **wakes the same object in place** (no dispatch, no background
  build, no mesh upload), so walking back and forth across the close-range edge no longer churns
  destroy + regenerate. Only a chunk past the dormant band is truly unloaded, via the capped sweep
  (1es, 6/poll + backlog). Cost: the retained band holds at worst ~184 chunks (rings 11-12) of cached
  mesh + tile data — tens of MB RAM while parked — while physics and render cost stay identical to the
  destroyed case (no collider, no draw; the far cell provides both covers). `DormantRingDepth` is a
  serialized knob (1-4).
- **Far shell (1ef):** from ring near+1 out to the render radius, `WorldStreamer.FarShell.cs` covers
  the ground with one coarse **cell mesh** per aligned span block — level-of-detail sectors generated on
  the ThreadPool from the SAME per-chunk corner grid the real chunks use (save stamps + noise), so the
  map stays watertight and shares the real ring's seam exactly. Cells: **span-1** rim cells (3 m step)
at rings 10-14 own the loaded/unloaded **active shadow** (inactive under a real ring-10 chunk, active
   the same poll its real chunk unloads **or goes dormant (1gc)** — zero hole, zero z-fight), **span-3** cells (rings ≥15, 90 m wide) and
  **span-6** cells (rings ≥36, 180 m wide — **1eo: unreachable at the 30-chunk default**, which only
  exposes span-1 + span-3; span-6 reappears only if the clamp is later raised above 36) cover the open ground — every cell on the SAME uniform
  **3 m step** (11/31/61 verts/axis respectively). One shared lattice means adjacent cells of every span
  carry exact coincident edge rows, so the shell has **no T-junction cracks** (1ej removed the old
  radius step ladder 3/6/9/12/15 whose different-step neighbors left thin visible lines along chunk
edges), with a
   coarser required parent suppressing its finer children so every chunk has exactly one cell. Ownership
   swaps are COVERED (**1eq**): the hard ring-cut flips ownership around the span-1↔span-3 boundary
   every 30 m chunk step (a band near rings 13-16), and naively destroying the outgoing cell in the SAME
   poll its replacement is only enqueued blanked the region for the ~50-400 ms async rebuild window
   ("chunks in range disappear and render right back" while moving). Now a fine tenant keeps rendering
   while its coarser replacement builds (promote retain), a newly live coarser cell hides its finer
   siblings the poll it is created, and a demoted coarse cell keeps rendering until EVERY finer
   replacement exists, then hands ownership to them atomically (`CompleteFarHandoff`) — one live owner
   per region at all times, no blink, no z-fight. The rebuild churn at the boundary is also **taken off
   the crossing** (**1er**): the swap band is **pre-warmed** — a live coarse cell whose farthest corner
   is within 2 rings of its demote ring generates its finer children AHEAD of the cut as **reserved
   shadows** (built on the ThreadPool over the preceding polls, created inactive), and fine cells under a
   live coarser owner are **retained** (never destroyed) while their box lives — so promote/demote while
   moving is almost always a `SetActive` toggle rather than a fresh build, and dispatches are paced
   (36 on-demand + 12 reserved per poll) so no chunk step storms the workers. Cell
   *block coordinates* `FarCell.X/Z` are the block's **min chunk coordinate in chunk units** everywhere
   (**1el** — build + placement once multiplied them by the span again, so every span-3/6 cell was
   rendered 3×/6× further out, leaving the ~450-1350 m mid-band a permanent empty ring).
   Budgets:
  **96 in flight** (1ek, was 48), 16 finalized/poll but **time-capped at ~2.5 ms/poll on the main
  thread** (1eh — the fast
  fill stays, a single poll never spikes on GameObject/mesh creation; ~120-480 cell meshes/s → initial
  fill ~1.5-4 s at the 30-chunk default, ~1,000 cells — **1eo**, down from ~1,400 at 67), 32 removals/poll with a backlog flag. Since **1es**
   every main-thread streaming step — real-chunk finalize, far finalize + the far scan charge, props, the
   real-chunk unload sweep — spends measured wall time against ONE shared **~4 ms poll budget**
   (`StreamBudgetMs`, scaled by the same adaptive factor as the boot burst): no individual bound was wrong,
   but at a ~30 m chunk crossing every step legitimately wants work in the SAME poll and the sum used to
   spike the frame (the highest-value periodic hitch the sweep found). Now a crossing DEFERS the remainder
   to the next poll instead of finishing it in-frame — the fill may trail a fraction of a second while
   sprinting (the player chose smoothness over fill speed at **1es**), the periodic per-boundary spike is
gone, and real-chunk **unloads are capped at 6/poll** (spread over a few polls with a backlog flag that
    keeps the idle gate busy; **1gc** reframes the sweep as the DEEP unload past the dormant band — the
    out-of-keep column cheaply demotes to dormant instead of tearing down), with an added **~1.2 ms
    wall-clock slice** on the deep-unload sweep (1gd — a single `UnloadChunk` can run past 1 ms and six
    of them stack; the slice bounds the sweep's effect on the poll that owns it, backlog carries over), with **chunk save files written on a background worker** (§2.6). Cells are dispatched **near-first**
  (1ek, was horizon-first): the pending walk is closest-first and dispatch iterates it forward, so the
  region around the player — where a void is most visible — and the interior close before the distant
  fringe, which fills a moment later (pre-1ek the reverse, horizon-first order let the heavy outer
  span-6 cells hog every flight slot, starving the near cells into a permanent-looking empty square
  ring). Far-cell boundary **normals are cross-seam** (1ek): at cell edges the slope's beyond-sample
  comes from the pure world heights, so neighboring cells compute byte-identical boundary normals and
  no lighting crease shows along any far-cell edge or at the rim/real junction. **Static baking is
  DISABLED** (1eh follow-up): the once-combined batch
   (`StaticBatchingUtility.Combine` via `TryBakeFarShell`, gated by the `FarBakeEnabled` switch) rendered
   the merged far meshes only from below — a back-face/combined-mesh artifact, so every far cell again
   renders as its own dynamic mesh (known-good from 1ef; **~1,000 draw calls** back at the 30-chunk
   default). The bake stays in
   code behind the switch — re-enable only after a Unity-side root cause on combined-mesh winding. Since
   **1ei** far cells render through a **double-sided (Cull Off) variant** of the ground material
   (`FarGroundMaterial`) so their tops show from above even if a mesh's winding/culling hides the upper
   face — real chunks keep Cull Back — and they **cast no shadows** (`ShadowCastingMode.Off`, 1ei): the
   ~1,000 far cells (1eo; ~1,400 at 67) no longer draw into the sun's shadow map (no gameplay value beyond 270 m). Collider
   ring-crossing cooks are capped at **2/poll** (1ei, was 4) so the synchronous PhysX cooks spread over an
   extra poll. The
   span-1 rim stays dynamic regardless (it owns the active shadow). Far
  cells have **no colliders, no props, and never re-generate** (digs stay
  inside the collider ring 7 < rim 10). The camera far plane is **2200 m** (`PlayerController.Camera.cs`)
  — sized back when the default view was ~2 km; at the **1eo** 900 m default it just clears the shell
  with huge margin, and the shader's **horizon tonal lift** (starts ~1600 m, 60% peak) is now **beyond
  the loaded world**, so it only engages if the render radius is raised above ~53 chunks. It uses no fog —
mid-view stays crisp
   so the outermost shell reads as atmosphere at the old 2 km range.
- **Low-poly facet look (1hi, DEFAULT OFF since 1ia):** `WorldStreamer.LowPolyFacets` switches the
   smooth world's chunky language on, render/geometry-read only (saves, the 1 m tile grid, props, draw
   calls and the budgeted collider pipeline untouched): (a) the far shell's cells emit **flat
   per-quad normals** — 4 corner vertices per quad sharing one +Y-dominant flat normal instead of the
   smooth central-difference haze (crisp mesas on the horizon; triangle count unchanged, vertices 4x,
   but a far cell uploads once per cell lifetime, never a per-frame cost; boundary quads are built
   from the SAME world corners on both sides of a shared edge, so flat facets stay seam-proof by
   construction — no cross-cell pull needed); (b) the near 1ew stretch-split receives a **0
   threshold** (`EffectiveRefineThreshold`), so steep near slopes keep ONE big flat quad per tile
   instead of the adaptive 2x2 sub-quads; and with 1hi.1 on, (c) the near chunks' root mesh is the
   coarse lattice surface rather than the 1 m per-tile one. The look is a QA knob like the voxel
   toggle — flip BEFORE the far shell builds for a clean read
   (`NewWorldTestGround.EnableLowPolyTerrain`, **off since 1ia**).
   **1ia reverted the whole terrain render algorithm to its pre-1hi state by flipping this default
   off** (see §2.5a). The code is untouched and the knob still works; off means: smooth
   central-difference far-shell normals, the 1ew stretch-split running at `RefineThreshold` again,
    the full 1 m per-tile near root **with side walls**, and `PatchRegion` taking its per-tile skim.
    (1f6 removed the detail LOD band entirely, so "off" no longer buys back any child meshes.)
- **Coarse near-ring facets (1hi.1, dormant since 1ia):** with `LowPolyFacets` on (**off by
   default since 1ia**, see §2.5a), the REAL chunks' root
   mesh is no longer the 1 m per-tile surface — the merged builder emits the lattice facets
   themselves: every `WorldStreamer.LowPolyStep`-th node of the 31x31 world-corner grid (default
   **3 m** = the far shell's step, so the whole world — near ring + far shell — reads ONE uniform
   3 m facet language with the near/far seam sharing exact world corners; **2 m** is the subtler
   alternative), one flattened quad per cell with a +Y-dominant cross normal, lattice UV/colors and
   no side walls. The 1 m corner grid stays the single source of truth: saves, edits, props and
   deformation are untouched (`PatchCornerGrid` restamps the lattice and the ~121-quad root re-emits
   from it), and the **collider rides the same step** (colliders below) so the player stands exactly
   on the visual. Trade-off: a 1 m corner edit only visibly moves a facet vertex when the edited
   corner lands on the coarse grid. Tune via `NewWorldTestGround.LowPolyStep`.
- **Up-facing lattice winding (1hi.2):** the lattice-family surfaces (far shell flat/smooth,
  decimated colliders, the 1hi.1 coarse roots) were originally emitted first-corner-first (SW,
    SE, NE, NW; tris (00,10,11)/(00,11,01)). The far shell masked that with its double-sided Cull Off
    material (above), but real chunks keep `GroundMaterial` (Cull Back), so the 1hi.1 root rendered only
    from below and its collider let the player drop through. 1hi.2 re-emits the two REAL-chunk lattice
    surfaces that still exist — `EmitLowPolyIndices` (coarse-root facets) and `BuildDecimatedCollider`
    — in the smooth tile's up-facing corner order
    NW, NE, SE, SW with `BuildMeshData`'s exact (0,1,2)/(0,2,3) two-triangle pattern; normals stay +Y. The
    far shell keeps its (double-sided-visible) winding. (The third, `ChunkObject.BuildLodChild`, was
    deleted with the detail LOD in 1f6.)
- **Collider lattice (1hi, step raised to 1 m by 1ex; step still follows the root per 1hi.1, wound
   up-facing 1hi.2):** smooth real chunks cook their MeshCollider from a lattice sampled every
   `ChunkColliderDecimation`-th node of the 31×31 corner grid (`ChunkMeshGenerator.BuildDecimatedCollider`)
   — **every node (1 m, 961 verts / 1800 tris) since 1ex**, or the chunk's OWN low-poly root step when
   coarse (3 m, so you stand exactly on the visible facets). The player has **no separate ground
   raycast**: `CharacterController.Move` sweeps this collider directly, so its resolution *is* the
   ground. 1ex raised it from 1hi's 2 m because the 2 m sampling let the player walk over a visible 1.1 m
   crater (a footprint centred on an odd x or z had no sampled node inside it). The pre-1ex
   `~4x cheaper cook` no longer holds — the collider is now render-resolution — but the path still omits
   the render mesh's side walls and refined blocks, which physics never uses. It shares the EXACT world
    corners the neighbour chunks use, so the physics surface is seam-proof across
   chunks by construction; `PatchRegion` re-derives it from the patch-re-stamped lattice so the collider
   tracks every excavation. Each chunk holds a second pooled Mesh (`ChunkObject._colliderMesh`, same
   acquire/release discipline as the render mesh; lazily allocated — a chunk that never enters the
   collider ring owns nothing; uploads re-specified in place, overwrite-only). Voxel mode is unchanged
   (its chunky 1 m render columns stay the collider).
   **Two notes carried with the 1 m value:** (a) the lattice is **horizontal quads only** — no vertical
   strip pass — so a 1 m vertical step is sampled as a 45° ramp, which is exactly the default
   `CharacterController.slopeLimit` (never assigned in this project), making cliff traversal a play-test
   item; (b) the step must divide 30 or the last grid row falls short of the chunk boundary, so the legal
   set is {1, 2, 3, 5, 6, 10, 15, 30}. Ring-7 is 225 bodies, so full-ring broadphase is ≈405k collider
   triangles (was ≈101k) — measure on F2.
- **Speed-decoupled renderer clock (1gd, cadence tuned 1xd):** the streaming/render loop no longer runs
  inside the gameplay `Update`. `WorldStreamer` starts a coroutine (`StreamLoop`, started in
  `OnEnable`) ticked on its OWN wall-clock beat at `StreamHz` (default **20 Hz** == the legacy
  0.05 s poll), and when a poll exhausts the shared stream budget (a HEAVY beat) it yields **one
  cool-down frame** before the next slice — a heavy map-render beat can never double-load the gameplay
  frame it lands next to. Since 1xd the cool-down no longer follows *every busy* poll (that halved the
  effective rate during catch-up and let the player outrun the far fill): light busy polls
  (dispatch-only, small finalizes) keep the full `StreamHz` cadence. `StreamInUpdate` stays the master
  switch (the coroutine checks it every iteration, so the scene toggle still works at runtime);
  `DecoupleRenderFromGameplay` gates the cool-down frame only. All per-stage budgets/slices are
  unchanged — loading speed is NOT reduced, only the render/maintenance work is decoupled from the
  gameplay frame. The per-poll temp lists are also pooled (1gd): the `StreamAround` wake/demote/
  deep-unload scans and the far-shell stale/handoff removal scan reuse module fields instead of
  allocating transient lists on every live poll.
- **Edited-terrain seam fixes are asynchronous (1gd):** walking into previously-edited terrain used to
  run **synchronous 900-tile full merged-chunk rebuilds** (`FullRebuildChunk`) on the main thread —
  `ReconcileNewlyLoadedChunk` on the loaded chunk itself PLUS up to four more from
  `ReconcileModifiedBorders`, fired every poll the stream passed an edited chunk (a `_modifiedChunks`
  O(1) set lookup). At high player speed that stacked into the "immense lag" report. 1gd moves the
  heavy re-emit + merged-mesh merge onto **ThreadPool workers**: request → main-thread snapshot job →
  worker (`BuildMeshData` + `BuildMergedMeshData`) → main-thread drain under the shared
  `StreamBudgetMs` accounting (caps `MaxRebuildInFlight` 8 / `MaxRebuildFinalizePerPoll` 3). Stale
  results are dropped via `ChunkObject.MeshRebuildStamp` (bumped on every apply/patch), so a slow
  worker can never overwrite fresher edits. Loading rate is unchanged; only the seam fix stops holding
  the gameplay frame. Voxel mode keeps its synchronous rebuild path (experimental model unchanged).
- **Chunk demote is gated on far-shell cover (1xd):** a real chunk that passes the hysteresis ring is
  hidden in place (`DemoteChunk`), and `FarShellTick`'s active-shadow sync activates the coarser cell
  under it in the SAME poll — but only if that cell already exists. If the async far build for the
  position hasn't landed yet, hiding the chunk would open a **group-sized invisible hole** for the
  ~50-400 ms build window. `StreamAround`'s demote pass therefore resolves the required far cell
  (`FarCellForDemote`, same near/keep bounds as `FarShellTick`) and only demotes once it is live in
  `_farSectors` (inactive is fine — the sync flips it). A skipped chunk stays visible and
  `_demoteBacklog` (a new idle-gate member) keeps the loop polling until the cover arrives; positions
  outside the far annulus (small render distances) have no required cell and demote un-gated.
- **MeshCollider cooks are budget-gated (1xd):** `ReconcileCollidersIfChanged` defers a cook when the
  poll's shared `StreamBudgetMs` pool is already dry (`_streamCapped`) instead of cooking up to
  `MaxColliderCooksPerPoll` (2) unbudgeted on top of a heavy beat — PhysX cooks can no longer stack
  ~2-6 ms onto arbitrary gameplay frames at speed.
- **Rebuild stamps are globally monotonic (1xd):** `ChunkObject.MeshRebuildStamp` values come from a
  shared counter (`NextMeshRebuildStamp`), not a per-object increment. A per-object counter restarted
  at 0 on every chunk lifecycle, so a reloaded chunk could re-issue a stamp a stale in-flight result
  already held; the drain check now also rejects results whose chunk is `Dormant`.
- At each frame, the system calculates which chunks are within radius of the player.
- Chunks entering radius: loaded from cache or generated.
- Chunks leaving radius: unloaded from memory (kept in cache on disk).
- **Boot (current build):** the **spawn chunk** was generated synchronously so the player is usable
  immediately; since **1e5** that sync build is gated behind the **non-platform fallback spawn** — the
  default test-platform spawn (independent floating pad, §9) streams its chunks exactly like every
  other from frame 1, so the first rendered frame no longer pays the 5-20 ms sync chunk. The rest of
  the visible ring builds in an **adaptive burst pass** (poll every 0.05 s, up to
  12 chunks / base ~6 ms finalize budget that self-shrinks while frames hitch, 24 background generations
in flight) that fills the full near ring (1ef: NearRingRadius 9 chunks — the far shell fills the
   rest) without dropping a steady 60 fps (**1di** — the earlier
   16-chunk/12 ms burst shrank the budget so chunk finalization + collider cooking stop competing with the
   frame). Since **1em** this fill actually completes: previously the dispatch loop re-generated the
   same nearest chunks forever, so the ring stalled at ~24 chunks (see the §2.5 real-ring note). The game bootstrap defaults render radius to **30** (1eo) — was **67** with a
   hard clamp of **160** (1ef) — and the chunk distance-cull distance auto-matches the current render
   radius so
   culling never fights the visible ring (far cells are static, not registered as cull candidates, so
   the cull budget still scales with the REAL near ring).
- **Boot cost (1e5):** manager lookups go through a `ComponentRegistry` (one shared scene sweep per
  type instead of ~24 `FindAnyObjectByType` scans), `UIManager.InitializeUI` / `SoundManager.
  LoadSoundClips` / `GameManager.AutoResolveReferences` idempotency guards stop the same UI layout /
  8× audio loads from running 2-3× at boot, and the non-critical manager setup (menus, save system,
  quests, cutscenes, wife NPC, skill/friendship/fishing/chest) is deferred one batch per frame by
  `BootInitDeferrer` so the first rendered frame only waits on the HUD + tool catalog.
- **Prop ring (1di + 1en):** trees/rocks stream on every chunk the real chunk stream holds — since
  **1en** `SyncPropRing` enforces a **floor of `near + 1`** (≈ **0-330 m**, the 0..near+1 rings that
  render full-fidelity chunk geometry, incl. the ring-10 hysteresis chunks), and the serialized
  `PropRingRadius` may only push the ring *wider* (a prop-only fringe), never narrower — so the prop
  range can never trail the chunk range again (before 1en a scene-serialized **4** left a 120→300 m
  band of prop-less terrain). ~441 chunks ≈ **~800 prop roots / ~5-8k cubes**, ~1% of the old 1di
  worst case. `WorldStreamer` queues the deterministic prop stream for chunks that enter the ring and
  hides their spawned props (`ChunkObject.ReleaseProps`) for chunks that leave it, while the terrain
  mesh + collider stay loaded for the whole ring. The distant radius-N ring therefore never holds the
  ~33k prop GameObjects (~450k prop BoxColliders in the physics broadphase) that made the old
  full-stream "game too lag". Everything inside the ring keeps its colliders, so chopping/mining
  targets anywhere in the ring (now incl. the 240-300 m keep ring) stay fully hit-able. Props pop
  in/out at the ring edge; leaving the ring merely **deactivates** them (keep-alive, 1du), so
  re-entering reactivates the SAME GameObjects instantly — no destroy/respawn churn at the edge — and a
  prop you already chopped stays chopped (the stream position is preserved, not re-rolled).
- **Nature props are sparse (1dm):** trees AND rocks each spawn 1-in-1000 per tile (a fifth of the
  original 1/200 ratio) — a chunk (~900 tiles) averages ~2 cube-heavy props instead of ~9, so the
  world reads sparser/cleaner while the prop ring stays light. The odds live in
  `ChunkObject.PropSpawnOdds`.
- **Collider-on-demand (1dq):** terrain **MeshColliders exist only where gameplay physics needs them**
  — chunks inside the `ColliderRingRadius` ring around the focus (default **7** ≈ 210 m, covers every
  gameplay probe: player ground ray, spell ≤40 m, NavGrid, Tornado, ToolManager, fishing) plus chunks
  under an active spell projectile (`ColliderRequestRegistry`, requested chunk-by-chunk as the bolt
  flies and expanded by one chunk). Every chunk at full render radius still looks identical — the far
  ring streams its mesh and **no collider**, so the ~2.2M pillar of cooked broadphase triangles drops
  to the ~140k the gameplay actually queries (~94% less), at the cost of a hard *walkable-physics*
  edge at the ring (the world visually continues; you just cannot walk past it). Promotion/deferral is
  a cheap state-guarded toggle per poll (no re-meshing), rebuilds (`FullRebuildChunk`/`PatchRegion`)
  and unloads preserve each chunk's collider state, and the synchronous boot chunk keeps its collider
  so the player lands before the first poll.
  - **Player-floor guarantee (1gg):** the collider walk treats the focus cell **and its 2-chunk
    floor** (Chebyshev) as collision-critical — those cooks are budget-exempt (bounded) so a
    budget-starved heavy sprint can never leave the chunk under/just-ahead of the player collider-less
    for more than one poll, and every poll additionally advances the single **closest** deferred cook
    regardless of stream load. (This closed the 1gg fall-through: stream-budget gating since `1es`/`1ge`
    could defer a chunk's first cook indefinitely during a sprint, so a player could step onto rendered
    but uncollidable ground.)
- **Chunk mesh pooling (1dv):** each chunk owns ONE `Mesh` for its entire life — acquired from a small
  capped freed-mesh pool (`ChunkMeshGenerator`, cap 48) on first stream, then **re-uploaded in place**
  on every rebuild and unload→reload instead of allocating + destroying a fresh Mesh each time
  (~961-vert / ~1800-tri GPU buffers shared across the pool). Deformation and walk cycles therefore
  stop generating Mesh/GC churn and transient double-buffer uploads; `ApplyMerged` explicitly
  null→assigns the collider mesh so a rebuilt collider-active chunk re-cooks its physics (the same
  pattern `PatchRegion` uses). Because Unity mesh buffers only ever grow, `UploadMerged` clears the
  mesh whenever the incoming vertex count differs from the retained one (slab side walls add verts;
  a later smaller re-upload must not write channels against a stale larger buffer). Purely an
  implementation detail — zero visual/behavior change.
- **Chunk distance cull (`ChunkDistanceCull`, formerly `ChunkLodManager`; detail LOD REMOVED in
  1f6):** the class does one thing — it hides a streamed chunk root once the chunk falls past the
  distance the world promises to be covered. Its 1gh floor (§ below) and the 1ea rolling 1024-entry
  burst are unchanged. What is gone is everything 1e6 put in the same class:
  - ~~**Chunk LOD (1e6):** every chunk grows two decimated **child meshes**, `Lod1` (every 2nd tile
    corner) and `Lod2` (every 3rd tile), with bands at 0-30 m / 30-60 m / 60 m+ and the root
    `MeshRenderer` disabled while a detail band was active.~~ **1f6 deleted it.** Each child was a
    *different surface* from the root — a 2 m / 3 m resample of the corner lattice drawn **instead
    of** the mesh the player was standing on — so the terrain visibly changed shape when crossing
    30 m and 60 m, and where a coarse triangle spanned convex ground the child could sit in front
    of the real surface and cover it. A chunk's root mesh is now its **only** render output at every
    distance.
  - ~~**Sub-cell relief keeps the full lattice (1f5):** the 0.20 m `LodDetailCurvature` /
    `NeedsLodDetail` gate that let a chunk holding sub-cell relief keep stride 1.~~ **1f6 deleted it
    with the thing it was protecting.** `ChunkObject.RefreshLodMeshes`, `BuildLodChild`,
    `BuildVoxelLodChild`, `EnsureLodChild`, `LodDirty` and the 31x31 `ChunkCornerGrid.Normals` copy
    (only ever read by those builders) are all gone. The **lesson it recorded still stands** and is
    the reason 1f6 was the fix rather than a retune: a fixed decimation stride silently drops any
    relief narrower than its cell, so a *fixed-stride* second surface of a heightfield is a
    resolution bug before it is a performance win. Note also that the resampling hazard 1f5 named is
    not gone from the world — it is simply no longer expressed as a *decimated child of the same
    chunk*. The decimation that remains is a **step of its own**: the 1hi.1 facet root and the
    decimated collider (§2.5), which are separate decisions with their own pairing rule.
  - The trade: draw-call count is unchanged (one root renderer per chunk either way), but the
    ~336 chunks that used to draw a decimated child now draw their full ~1800-triangle root, i.e.
    roughly **+400k triangles** resident. That is the price of one surface instead of two.
- **Cull invariant (1gh):** the sweep's `EffectiveCullDistance` can never hide a real chunk the
  streamer is the ONLY surface for. It floors at the streamed real-chunk extent —
  `max((Radius+1)*30, (NearRingRadius + 1 + DormantRingDepth)*30)` — because far cells only exist
  BEYOND the near ring (§2.3 far shell): with a small render-distance asset the render term alone
  used to dip below the near ring (e.g. radius 7 → 240 m < ring-8/9 chunks at ~250 m), so the sweep
  `SetActive(false)`-ed loaded ground that no far cell covered — a real invisible hole, with its
  collider silently gone too. A chunk at < near ring is now never culled; beyond the near ring the far
  cell is the cover, and dormant chunks are skipped by the sweep entirely.
- **Transient-object pooling (1e6):** the generic `ObjectPooler` (Phase 9, previously unused) is now
  live on the boot root and backs the high-churn cosmetic spawns — spell **impact VFX** (the
  direct-hit `ImpactEffectPrefab` path), the **excavation debris** burst from `SpawnCraterDebris`
  (tool digs and zone/storm/summon strikes; magic projectile impacts pass `emitDebris:false` and
  instead play the script-built exploding sphere below, §3.7). **1jq removed 1jg's projectile trail
  from the pooler entirely**: the trail is no longer a stream of pooled cubes but a single
  camera-facing quad strip that owns one pre-sized mesh and fades itself, so it has nothing to pool
  and no longer appears in this list.
  `ObjectPooler.SpawnTransient` uses the pool when present and falls back to plain
  `Instantiate`+`Destroy` otherwise; pooled particle effects replay from frame 0 on reuse (`Clear`+
  `Play`). Debris cubes and impact effects are fully rewritten on every use (position/scale/material/
  velocity), so pooling is invisible apart from the allocation drop. **1jh's projectile-impact rock
  chips share `SpawnCraterDebris`'s cube template and therefore its pool**, so the two burst kinds
  recycle through one queue rather than doubling the pool's object count. (The per-emit
  `Renderer.material` allocation note below applied to the trail voxels 1jq deleted and no longer
  describes anything live.)
  Enemy death debris (the model
  parts themselves) and loot drops are deliberately **not** pooled — they are structural/persistent,
  not transient clones.

### 2.5a Terrain Render Algorithm Reverted to Pre-1hi (1ia)

The world renders with the **pre-low-poly** algorithm again. 1hi added a flat-facet render path whose
default was on, and 1ia turns that default **off**, so the terrain **surface** is the pre-1hi surface
again — with one deliberate exception noted below (the 1hi decimated collider, which is not render
output). Nothing was deleted; the change is three values plus their two test-platform mirrors.

| Value | 1hx (before) | 1ia (now) | Effect while off |
| --- | --- | --- | --- |
| `WorldStreamer.LowPolyFacets` | `true` | **`false`** | the gate for the whole flat-facet path |
| `WorldStreamer.LowPolyStep` | `6` | **`3`** | only read when the gate is on |
| `WorldStreamer.FarSectorStep()` | `6` | **`3`** | far-shell sampling step (never gated — see below) |
| `NewWorldTestGround.EnableLowPolyTerrain` | `true` | **`false`** | test-platform mirror; re-enabled the look every session |
| `NewWorldTestGround.LowPolyStep` | `6` | **`3`** | test-platform mirror |
| `NewWorldTestGround.EnableCraterAudit` | n/a | **`true`** | **F13** read-only crater/deform audit (`CraterAuditKey` = `Key.F13`; moved off F1 in 1io — F1 is the combat-mode toggle) |

What the terrain renders now, with the flag off:

- **Far shell** — smooth central-difference haze normals, `flatFacets = false`, sampled every **3 m**
  on the world integer columns. The `Cull Off` material is unchanged.
- **Near chunks** — the full **1 m per-tile** merged surface **including the per-tile side walls** that
  the coarse-root path omits, built by `BuildMeshData` as before 1hi. `EffectiveLowPolyStep` is `0`, so
  `BuildMergedMeshData` never enters `BuildLowPolyMerged`.
- **1ew adaptive stretch-split** — back on: `EffectiveRefineThreshold` returns `RefineThreshold` again
  (the low-poly path passed `0`), so steep near slopes subdivide into 2x2 sub-quads again.
- **Detail LOD children** — no longer built at all, because `ChunkObject._meshStep` is `0` for every
  chunk **and 1f6 deleted the builder**. The 1ia revert note is kept for the record: at the time, the
  band children came back with it.
- **Edits** — `ChunkObject.PatchRegion` takes its per-tile skim and rebuilds bounds from the CPU vertex
  array; `ResampleLowPolySurface` is unreachable.
- **Colliders** — decoupled from the revert, and **changed again in 1ex**: smooth chunks now cook a
  **1 m** collider lattice (`ChunkColliderDecimation = 1`, up from 1hi's 2 m). The player has no
  separate ground raycast — `CharacterController.Move` sweeps the chunk `MeshCollider` directly — so
  the 2 m step meant a carve centred on an odd x or z had no sampled collider node inside its
  footprint and the player **walked through a visible crater**. At 1 m the physics surface carries the
  full render resolution (961 verts / 1800 tris per chunk, up from 256 / 450). The `~4x cheaper cook`
  claim that justified the 2 m step is **dead** at this value; the path is kept because it still drops
  the render mesh's side walls and refined blocks, and because `ColliderStep` must keep preferring the
  low-poly facet step when the look is re-enabled. Cost is real and is measured on the **F2**
  frame-budget lane: the ring-7 Chebyshev square is 225 bodies × 1800 tris ≈ **405k collider
  triangles** at full ring (was ≈101k).

Everything from 1hi is still in the source and still works — `LowPolyFacets = true` restores the
1hi.1/1hi.2 look at 3 m facets (the pre-1hx facet size, not 1hx's 6 m). Nothing was deleted, so the
revert is a **default**, not a removal.

Two things this revert does **not** change, deliberately:

- **The corner-gap defect is not fixed by this.** 1i1-1i8 located the void at the near/far boundary
  (the outer x/z corner of the loaded square). One hypothesised contributor is that the coarse-root
  path emits **no side walls**, so where a loaded chunk and a far cell disagree in height you look
  straight through the gap; the 1 m per-tile path has side walls, so the same disagreement is filled
  rather than see-through. That is a hypothesis, not a measurement, and the 3 m step is itself a
  candidate. **F3 is the instrument that settles it** — run it after a restart (§2.5) and read the
  corner/void sections.
- **The collider resolution is independent of the look**, per the row above — 1ex moved it to 1 m
  while the facet look stays off, so the two are now decided separately.

Per rule 11 this is a render-algorithm change: **nothing on screen changes until the play session is
restarted.** `_loadedChunks`, `_dormantChunks` and the far shell all hold geometry built by the old
generator, and `EnqueueChunkIfNeeded` *wakes* a dormant chunk in place rather than re-dispatching it,
so a session that was already running keeps the facet world. Toggling the inspector field mid-session
also does nothing — it is only read at chunk dispatch, and a chunk mid-build captured its mode at
dispatch time. Verification procedure: restart, walk out to the rim, then walk back and forth across
the near/far boundary so a resident chunk and a freshly built one are on screen together.

### 2.6 Chunk Persistence (File Caching)

- Deformations are saved **per terrain chunk** (`30×30` local tiles) as a single binary file:
  `worlds/{seed}/tc_{x}_{z}.dat` (magic `"NWTC"`).
- A file stores only **locally deformed tiles** (`ChunkTileMod`: local coords + 4 corner heights),
  never pristine terrain. On load (`TryLoadChunk`) deformed corners restore their saved heights
  before noise-filling pristine corners — so un-modified chunks stay fully deterministic from the
  seed and only edited areas consume disk/IO.
- **Corner sentinel is NaN, not zero (1bk):** `BuildOrLoadChunk` prefills the corner grid with
  `float.NaN`; a zero-filled grid treats `0f` as a valid saved height (`float.IsNaN(0f)` is false)
  and collapses every unstamped corner to height 0 — i.e. an un-edited chunk renders flat. NaN
  marks "no saved value", so only genuinely saved corners are restored and all others re-roll from
  the seed noise.
- **Force-rebuild API (1bk):** `WorldStreamer.ForceRebuildArenaLane()` drops the in-memory
  arena-lane chunks (`tc_-1_0/-1_1/-1_2`) and re-queues them through `UnloadChunk` +
  `EnqueueChunkIfNeeded` (the normal streaming path), so a stale flat mesh is re-streamed from
  noise + saves **without writing to any `tc_*.dat` file**. Auto-fired on New Game and bound to
  editor **F12**; `GameManager` holds the streamer reference (resolved by `AutoResolveReferences`,
  the streamer is created by `GameBootstrap`).
- **Re-rendering the whole world after a render-algorithm edit (1hu):** the resident terrain holds
  render output in three places — each loaded `ChunkObject`'s uploaded `RootMesh`, the **dormant**
  set (a demoted chunk is re-shown *in place*, same GameObject and same pooled mesh, and
  `EnqueueChunkIfNeeded` wakes a dormant chunk instead of re-dispatching it), and the **far shell**
  (its cells are sampled from the real chunks' surfaces). None of them re-runs the generator, so a
  code edit mid-session leaves old-algorithm chunks beside new-algorithm ones that part along their
  shared edges. The drop therefore covers **every loaded and every dormant chunk plus
  `ClearFarShell()`**; the chunk mesh pool needs nothing (`UploadMerged` re-specifies every channel
  and `Mesh.Clear()`s on a count change, so a pooled `Mesh` is a buffer, not a cache).
  `ResetTerrainSaves()` performs exactly that sequence but first wipes the `tc_*.dat` saves, so it
  **discards the player's terrain edits** — correct only when a pristine world is the goal. The
  non-destructive form is public as **`WorldStreamer.DropResidentTerrainKeepSaves()`** (1hw,
  returns the re-queued count): `ClearFarShell()` then `UnloadChunk` + `EnqueueChunkIfNeeded` per
  chunk over a snapshot of loaded **and** dormant keys. It deletes nothing and clears no dirty mark —
  `UnloadChunk` even *persists* pending edits on its way out, so a sculpted world returns sculpted.
  The bench exposes it as **`ResidentDropKey` (F4, `EnableResidentDrop`, off by default like every
  other world-mutating lane)**, which is the one-key A/B for any render-algorithm change: no play
  session restart, so "the terrain came back different" is an observation rather than a coincidence.
  Two honest limits: a chunk **mid-build** captured its mesh mode (voxel / facet step) at *dispatch*
  time, so it lands after the drop with the old settings and the key says so and asks for a second
  press once a readout shows `inflight 0`; and the drop removes every chunk collider until the
  rebuild lands over the next few polls, so it is meant to be pressed from a platform or in flight.
- Dirty tiles record at **mark-time** (no IO); each chunk's accumulated tiles flush **batched** into one
  file write per chunk (1es: the disk write itself runs on a background worker — the main thread keeps
  only the (cheap) vertex/serialization work, queues the bytes, and one ThreadPool worker writes the
  file, so an unload flush no longer freezes the frame; shutdown and New Game call
  `FlushPendingSaves()` to drain the queue synchronously, so no save is ever lost).
- Player modifications (terrain deformation) are delta-patched into the chunk file on flush.
- The save path is **captured once on the main thread** (`ChunkSaveManager.Warmup`, called by
  `WorldStreamer.Awake`): `Application.persistentDataPath` is main-thread-only in Unity 6, but chunk
  generation resolves the file path on background threads — they read the cached string only.
- **Voxel mode writes a second format (1et, §2.9):** with `VoxelTerrainEnabled` on, the same chunk
  files carry `version=3` multi-run column saves instead of `version=1` height-field mods; the two
  readers are mode-specific (smooth reader rejects voxel versions; voxel reader migrates v1/v2 on read).

### 2.7 Testing Arena — Independent Floating Platform (dev tool)

- The QA test bench (Opt/NewWorldTestGround) lays a **self-contained floating platform** that is fully
  INDEPENDENT of the world's procedural terrain: a solid mesh slab + collider (with 4 corner posts so
  it reads as a structure) is built in `Awake`, raised **clear of the natural ground** at the arena
  coordinate. The world's rolling terrain is **never edited in any way** — no carve, no flatten, no
  chunk-save writes, no prop clearing/suppression. The platform's top sits above a **coarse sample of
  the world's own best natural height** (same 5-octave noise the streamer uses, read-only, plus 12 m
  clearance) so terrain and trees never poke through.
- **Legacy flatten saves purged (1bi):** `worlds/1337/tc_*.dat` full-chunk flatten files (900-mod tiles
  all keyed off the old hub's flat pad) were **deleted**, so the map streams back as the generator
  designed it — each tile with its natural rolling surface and dedicated noise sample, no more
  "one-surface test field". Sparse files (real Earth-spell/tool edits, e.g. freshly tilled lanes) are
  kept untouched.
- **Corner sentinel is NaN, not zero (1bk):** `WorldStreamer.BuildOrLoadChunk` prefills the corner grid
  with `float.NaN` so an unstamped corner regenerates from noise instead of reading `0f` as a valid
  saved height (which collapsed whole chunks flat at height 0). `WorldStreamer.ForceRebuildArenaLane()`
  unloads + re-queues the arena-lane chunks; it is auto-fired on New Game and via editor **F12**, and it
  never writes to `tc_*.dat` files.
- The whole bench sits flat on the platform's **single level top** (`PlatformTopY`): every lane —
  farming plots/tilled soil, livestock, enemies/dummies/boss, buildings, NPCs, weapon pedestals/racks,
  the tool-pickup kit, and the magic-model grid — keys its placement off that one height, so nothing hugs
  a slope and every prop stands edge-to-edge level. Since the `1dl` follow-up the **enemy arena is off by
  default** (`EnableEnemies` = false — the spawn code stays, tick the toggle back on to fight the roster).
  Since `1do` each row/dummy spawns the race's OWN script (`EnemyCatalog` → `Enemies\<Race>`) carrying its
  distinct stats (§7.1.0) — the test-ground rows + dummies are the fastest way to diff every race.
- **Magic-model grid (1dk):** `NewWorldTestGround.EnableMagicModels` (default **on**) places **every
  castable magic spell** on the platform's middle band — one pedestal + school-colored projectile-style
  body (the exact live-cast visuals via `MagicProjectileModelBuilder.CreateProjectileDisplay`; the four
  `summonFallingRock` spells show their real falling formation via `SkillFx.BuildRockBody` —
  Fire Asteroid's Swarm left with 1ir, so every one of them is now the boulder, drawn at a stated
  0.35× of the live blast radius so the 3 m grid stays legible — and zone/beam/storm/summon/instant
  spells show their school-colored default icon). Since 1ir that last clause has three branches and
  not one: a **cone beam** mounts the live `SpellBeam.BuildConeVisual` wedge, a **caster-anchored
  summon** mounts its ground circle + core, and anything else shows the default icon — before 1ir both
  of the new spells fell through to `CreateProjectileDisplay`, which has no body for a Beam or a
  familiar, so the bench would have kept reporting the generic orb for exactly the two deliveries
  1ir changed) + a world-TMP label — pure visuals (no colliders/interaction) so each spell's magic model can be
  looked at and edited. Since `1dp` the pedestal models are **static** (the real casts still flicker in
  flight); since `1eb` live projectile visuals are static too — `OrbFx` pulse/spin and the exhaust
  `ParticleSystem` were removed from the builders, so the bench and live casts share the exact static body.
- **Player spawns on the test ground (1dn):** the test platform is now the **default spawn point**.
  `GameBootstrap` creates the test ground before placing the player and lands them on
  `NewWorldTestGround.GetSpawnPoint()` (pad top + 2 m) whenever the platform is built
  (`CreatePlatform` + `IsArenaReady`); `PlayerController.ResetPlayer` (new game / death respawn) does
  the same. `NewWorldTestGround.AutoTeleportPlayerOnStart` is now default **on** (the `RunBenchSpawn`
  pull-onto-pad is belt-and-braces for the same spot). The world's boot chunk near `(0, terrain, -10)`
  remains only as the fallback when the test platform is not built — flip `CreatePlatform` off or the
  toggle off to test the boot-chunk start.
- **Boot order stays "ground first, then player"** (§2.7): the platform is built in `Awake` (before any
  lane), the benchmark lanes target `PlatformTopY`, and the only sync-ground is the boot chunk — so the
  player is never teleported over a void, whether spawning on the pad or the boot chunk. The bench spawn
  is deferred (one lane group per frame); every lane runs in an isolated try/catch so one failing lane
  (e.g. one enemy spawn) logs instead of aborting the bench.
- The bench also lays the **tool/food discovery kit** along the platform's **east edge as real world
  pickups** (`Pickup_<id>` drops, `WorldBuilder.SpawnPickup`) instead of seeding the bag: the 10 tools
  (axe, pickaxe, hoe, hammer, scythe, watering_can, fertilizer, club, rosary, fishing_rod) + 5 food
  stacks (banh_mi, com_tam, nuoc_dau, mi_chinh, xap_phong ×5) — press E on a drop to collect it. The
  west edge hosts the weapon pedestals; a `PickupAmount` tag lets a single drop hand over a stack.
- **Perf readout — 4 Hz screen overlay (1ea / 1gf):** `NewWorldTestGround.EnableFpsStats` (default
  **on**) draws avg FPS + frame ms, loaded/dormant chunk + active-collider counts, the far-cell count,
  and the seam-rebuild back-queue. Since `1gf` the optional `EnablePollStageStats` (default **on**, and
  needs `EnableFpsStats`) extends it with the world streamer poll's **per-stage ms split** (near ring,
  finalize, colliders, far scan vs far finalize, props, rebuild drain) + rolling worst-poll peaks +
   heavy-poll count, and the `ChunkDistanceCull` sweep ms — so a long-sprint hitch shows on screen
  WHICH stage ate the frame. Read-only; it never touches the world, the platform, or the streamer's
  budget behavior. Since `1gh` the optional `EnableChunkDiagnostics` (default **on**) adds a single
  line for `ChunkInspectX/Z` (default the reported chunk −8/3): real load state (loaded/dormant/
   absent), root GameObject active, renderer+mesh present, collider,
   and the far cell that owns it (live or MISSING) — one screenshot resolves any
  "chunk invisible for no reason" report.

### 2.8 Physics Integrity Guard Rails

- **Problem:** one garbage/NaN vertex anywhere in the streamed terrain poisons the chunk `MeshCollider`
  (corrupted `bounds` → broken physics broadphase) and the CharacterController gets **depenetrated
  thousands of metres in a single step** ("take one step → teleported to -671, 5164").
- **Height sanitization:** `WorldStreamer` validates every height read from a `tc_*.dat` save via
  `IsSaneHeight` (finite **and** inside the ±200 m band — 5-octave noise max ≈ ±82.6 m **1eo** + deformation
  headroom). Invalid/wild values are treated as **missing corners** and regenerate from noise; a mod
  tile's garbage slot falls back to the (already-sanitized) corner grid. As a final backstop,
  `ChunkMeshGenerator.SanitizeHeight` clamps every vertex Y in `BuildMeshData`,
  `BuildMergedMeshData` and `ChunkObject.PatchRegion`, so no code path can push a corrupted height
  into a MeshCollider.
- **Player CC fail-net:** `PlayerController` records `_lastSafePosition` every sane frame.
  `EnforcePhysicsSanity` (runs each `Update` before input) reverts the player if any coordinate is
  non-finite or a **single frame** moved them beyond a **speed-aware tolerance** of at least 150 m
  (`max(last-frame effective speed × 1.5, 150)` — 1cc) — fast-but-legit movement (buffed sprint) can
  never trip it even during a ~1 s frame hitch, while every real corrupted-collider launch
  (thousands of metres) still does. On a revert it logs the blast position, the local terrain height
  there, and sweeps nearby colliders for non-finite/oversized bounds to identify the culprit chunk.
  Since `1gg` it ALSO reverts the player when Y drops **below the world floor** (`VoidFallFloor`,
  -300 m — beneath the ±200 m sanitized band, so only a true missing-collider void reaches it): a
  gradual fall-through of rendered-but-uncollidable ground is perfectly "sane" per-frame, so the
  blast tolerance alone would never catch it; the floor reverts to `_lastSafePosition` instead of
  letting the player fall forever.
- **Teleport routing:** every intentional teleport goes through `PlayerController.TeleportTo`
  (spawn/respawn, sleep, load-game, test-platform entry), which stamps the destination as
  the new "last safe" position so the fail-net never false-positives on legit relocation.

### 2.9 Voxel Terrain — Stepped-World Mode (1et experimental; smooth is the default again since 1ev)

An OPT-IN render/storage mode for the same chunk/world, added to fix the legacy heightfield's core
weakness: every tile is a 4-corner blob, so steep natural slopes become one un-editable stretched face.
It briefly became the world's default terrain in 1eu, but play-test read the 1-metre stepped columns as
too Minecraft-like/blocky, so 1ev un-defaulted it: the **smooth heightfield is the default again** and
voxel is opt-in behind `WorldStreamer.VoxelTerrainEnabled` (serialized bool, **default OFF** — flip it
before the world streams, or via the test-ground QA toggle `NewWorldTestGround.EnableVoxelTerrain`).
With it on, every real chunk re-renders as a **1-metre stepped voxel world** while keeping the whole
streaming/deformation/IO pipeline's contracts intact. It is kept (not deleted) so the stepped model can
be previewed and iterated on; the smooth world is otherwise untouched.

- **Terrain model (VoxelChunkData):** a 1 m grid of vertical columns of solid earth. Since 1eu a column
  is a **sorted run list** (a cave/overhang = 2+ runs), not a single `[ColumnBaseY .. Top]` run: each
  edited column stores only the runs that differ from pristine, and a **missing column is untouched** —
  its top re-derives deterministically from the same 5-octave world noise, so pristine chunks store zero
  data and re-roll identically on every load. `ColumnBaseY = -1000` gives 200 m+ of solid headroom under
  any reachable pit; tops are clamped to the same ±200 m mesh-safety band as the heightfield.
- **Material is derived, never stored:** grass→dirt→stone by dig depth below the pristine noise
  surface (`ChunkMeshGenerator.TerrainBandColor`), so a column run only ever needs two integers (YBot,
  YTop).
- **Rendering (VoxelMesher):** merged row-run **top quads** (one quad per equal-height run, per-metre
  UV) plus **terrace walls** — where neighbouring columns top at different heights the higher one emits
  a wall down to the lower, one band per metre (drops beyond 32 m quantize to 16 fixed bands so a deep
  pit never explodes the tri budget). Outer chunk faces use real neighbour column tops when loaded
  (cross-chunk seam walls level) and identical noise rounding when not (no phantom walls on untouched
  seams). Since 1eu every non-topmost run also renders its floor (chamber floor) and every raised run
  renders a downward **ceiling** (roof of the void beneath it), so caves/overhangs expose interiors.
- **Sculpting (1eu sculpt API):** `SculptVoxelCave(center, radius, roofThickness, chamberHeight)` and
  `SculptVoxelRaise(center, radius, height)` operate on the run lists of loaded chunks (volume sphere,
  per-column insert/remove of solid blocks) and full-rebuild + flush each touched chunk — an underground
  chamber keeps a roof shelf of untouched columns above it. The legacy 4-corner deformation API is kept
  too: voxel chunks fill their 900 tiles with flat 4-corner entries equal to the integer column top, so
  `DeformAt`/`FlattenAt`/`ApplyHeightEdits`/`GetDigDepth` and every tool/spell/shovel gate keep working;
  any edit full-rebuilds the chunk's columns + walls. The directed dig (`TerrainDeformer.Dig` with a
  direction) clips the crater's influence to the tiles ahead of the digger, so a shovel/pickaxe swing
  scoops only the slope in front of the player (crater becomes a half-space carve).
- **Persistence (extends §2.6):** the voxel path writes the same files to
  `worlds/{seed}/tc_x_y.dat` as the smooth path — as **v3 multi-run column saves**
  (`NWTC | int version=3 | seed | chunkX | chunkZ | colCount | per column: idx=localZ*30+localX,
  runCount, run pairs (YBot, YTop)`). Only edited columns are stored; restoring a carve exactly to noise
  deletes the file. **Migration:** legacy v1 height-field saves AND v2 single-run saves load in voxel
  mode (each tile's 4-corner heights reduce to a rounded-avg column top on read; the file stays until a
  voxel edit rewrites it as v3). The smooth path never writes voxel files and its reader still rejects
  them.
- **Far shell follows the mode (1eu; LOD half REMOVED in 1f6):** the far shell's `BuildFarSector`
  gains a stepped `BuildVoxelFarSector` twin that samples
  the same deterministic integer column tops on its 3 m grid — no more smooth far disc around a stepped
  world. Voxel chunks used to also build stepped **LOD children** (`BuildVoxelLodChild` — a decimated
  coarse full-size column grid feeding a pooled child mesh); **1f6 deleted that with the rest of the
  detail LOD**, so a voxel chunk now draws its own stepped mesh at every distance. Its side note —
  the 1f5 hazard that "a decimated child is a different surface from the root" is *sharpest* in
  voxel mode, where a decimated column grid is a 2x/3x coarser version of a stepped surface — is why
  the deletion is the right outcome and not a loss.
- **Known limits (documented):** the mesher's side-wall pass reads only the topmost run, so interior
  cavity side walls are NOT rendered — the rim of a carve reads as a slot into the void until per-run
  side-wall meshing lands. `ChunkSync` network sync of voxel edits is deferred; two perpendicular walls
  with different drop sizes can crack cosmetically at a 90° step corner.

### 2.10 Smooth Terrain Refinement — Adaptive Stretch-Split (1ew)

Fixes the smooth heightfield's core weakness **without** the blocky voxel look of §2.9: each tile is one
quad from 4 corner heights, so a steep natural slope turns a 1x1 tile into one huge stretched membrane —
the corner-grab deformation (§3.8) grabs only world-corner keys, so an interior face has no vertex to
bite and a cliff reads as a single un-editable surface.

- **Rule:** a tile whose 4 corner heights differ by more than `WorldStreamer.RefineThreshold`
  (serialized, default `ChunkMeshGenerator.DefaultRefineThreshold` = **2.5 m**, `0` disables) renders
  as a **2x2 sub-quad grid** (16 vertices / 8 triangles) instead of one quad. Flat tiles stay coarse
  (one quad), so only genuinely steep tiles split — face count on a cliff goes 1 → 4 while the whole
  world stays a continuous smooth heightfield at every zoom.
- **Derived, never stored (1ew):** the 3x3 fine heights are the **bilinear interpolation of the tile's
  4 coarse corners** — deterministic from the same coarse heights the save already stores. Saves are
  byte-identical to pre-1ew (no format change), collision cooks from the same refined mesh, and a
  pristine chunk still stores zero data. (1ex will persist fine lattice nodes as a v4 save section so
  an edit can move a mid-face point directly.)
- **Crack-free by construction:** every sub-quad edge lies exactly on the coarse bilinear surface — an
  edge midpoint is the linear average of the two shared corners, which is precisely what the coarse
  neighbour's straight edge passes through — so a refined tile meets a coarse neighbour (or another
  refined tile) with **zero gap**, and cross-chunk shared corner heights are untouched by refinement.
- **Interior-of-chunk only:** tiles on the 1 m border ring (local index 0 or 29) never refine, keeping
  the cross-chunk shared-corner contract exactly as it was; the fine-edit lattice of 1ex therefore stays
  strictly intra-chunk. Known limit: a cliff running exactly along a chunk edge keeps its 1 m border
  strip coarse.
- **Merged mesh is a sequence of variable-size per-tile blocks:** `MergedChunkMeshData.TileVertexBase`
  / `TileVertexCount` record each tile's block offset + length (4 or 16 verts), and
  `ChunkObject.PatchRegion` re-skims a touched rectangle through that table (the old fixed
  `tileIndex * 4` stride is gone). The **31x31 corner lattice** rides along as
  `MergedChunkMeshData.Corners` — the collider cook (§2.5) decimates from the lattice, the low-poly
  root re-emit samples it, and `PatchCornerGrid` re-stamps lattice nodes owned by a patched region
  so both track deformation. (1f6: its only other consumer, the detail-LOD children of §2.2, is
  deleted, which is why `ChunkCornerGrid.Normals` went with it — nothing read that array.)
- **Edit flips split state ⇒ full rebuild:** `RebuildChunkRegion` compares each region tile's fresh
  refinedness (`ChunkMeshGenerator.IsRefined`) against `ChunkObject.IsTileRefined`; any flip (an edit
  pushed a tile across the threshold) falls back to `FullRebuildChunk`, because the block table cannot
  be resized in place by a patch.
- **No wall conflicts:** refined steep tiles never emit side walls (shared-edge corners stay equal ⇒
  `EdgeIsRaised` is false) and slab tiles are flat (delta ≈ 0 ⇒ never refined).
- **UVs/normals:** sub-quads keep whole-tile 1 m UVs (texture density unchanged) and flat per-sub-quad
  normals (same style as the coarse quad).
- **Edit granularity in 1ew:** the split is **render + hit granularity** — `DeformAt`/`FlattenAt`/
  `GetDigDepth` still move/sample the 4 coarse corners and the fine points re-derive from them
  (corners move → the whole refined patch follows). 1ex adds the fine lattice writes so a dig can move
  a mid-face point on its own.
- **Play-test gate (1ew):** steep slopes show multiple small faces (never blocky steps like §2.9, no
   holes or seams at splits), corner edits still move terrain coarsely, and revisiting an area restores
   it exactly.
- **Low-poly knockout (1hi, dormant since 1ia):** when `WorldStreamer.LowPolyFacets` is on
   (**off by default since 1ia**, §2.5a), every build path
   (chunk dispatch, 1ea save-scan rebuilds, Deform re-skims, seam rebuilds) receives
   `EffectiveRefineThreshold = 0`, so no tile ever stretch-splits — the whole near band renders as
   one flat quad per tile (the same chunky language as the far facets, §2.5), and `IsTileRefined` is
   uniformly false. Threshold editing (DeformAt etc.) still works — coarse 1 m corner granularity.
- **Coarse root facets (1hi.1, dormant since 1ia):** when low-poly is on the ROOT is the 3 m lattice surface anyway
   (`BuildMergedMeshData(..., lowPolyStep)`: flat per-facet quads sampled from the 31x31 corner grid,
   no side walls, no per-tile blocks), so the merged patch-table pointers are null and `PatchRegion`
   skips the per-tile skim — it re-samples the whole tiny root from the re-stamped lattice
    (`ChunkMeshGenerator.ResampleLowPolySurface`). There are no detail children left to skip (1f6
    deleted them), and edits land coarsened: a 1 m corner move only visibly lifts a facet
    vertex on the 3 m grid (the 1 m heights still save/restore exactly). All of the above is dead
    while the flag is off: the 1ew split runs at `RefineThreshold`, the root is the full 1 m per-tile
    surface with side walls, and `PatchRegion` takes its per-tile skim.

---

## 3. Combat System

### 3.1 Real-Time Action Combat

Direct weapon/ability control with stamina management, dodge-rolling, blocking, and parrying. Inspired by Elden Ring's combat feel.

#### Core Mechanics

| Mechanic | Description |
|----------|-------------|
| **Light Attack** | Fast, low damage, low stamina cost |
| **Heavy Attack** | Slow, high damage, high stamina cost |
| **Dodge Roll** | i-frames during roll, costs stamina |
| **Block/Shield** | Reduces incoming damage, stamina drain on block |
| **Parry** | Frame-perfect timing for massive damage window |
| **Riposte** | Critical hit after successful parry |
| **Jump Attack** | Aerial downward strike, breaks guard |
| **Charged Attack** | Hold to charge for more damage |
| **Weapon Arts** | Unique per weapon type, costs FP (Focus Points) |

#### Damage Formula

```
Final Damage = (Attack Power x Skill Multiplier x Weakness Multiplier)
               - (Target Defense x Defense Multiplier)
               x Damage Type Modifier   # §3.7: attacker's DamageType vs
                                        #   target equipment resistance
               x Critical Modifier (if applicable)
```
The **DamageType Modifier** resolves the specific damage type (§3.7 — Physical, Fire, Ice, Lightning, Holy, Dark, Wind, Earth, Water, Arcane) of the weapon or spell against the target's per-type equipment resistance. `DamageCalculator` routes the attacker's type → the target's resistance table (see §3.6/§3.8).

#### Stamina System

- Stamina regenerates over time (pauses briefly after actions).
- Each action costs stamina.
- Stamina management is the core skill expression.

### 3.2 Classes (17 Unlockable)

The game uses a **classless unlock system**. Players start as a **Wanderer** (base class) and unlock classes by meeting stat thresholds or finding class trainers/items in the world.

#### Starting Base

- **Wanderer:** Balanced starting stats, no special abilities. Can go anywhere.

#### Unlockable Classes

| # | Class | Unlock Requirement | Unique Mechanic |
|---|-------|-------------------|-----------------|
| 1 | **Warrior** | Str >= 20 | Weapon Arts enhanced, stance breaking |
| 2 | **Mage** | Wisdom >= 20 | Spell casting, magic damage |
| 3 | **Rogue** | Dex >= 20 | Backstab bonus, stealth attacks |
| 4 | **Cleric** | Fth >= 20 | Healing miracles, buffs |
| 5 | **Berserker** | Str + End >= 35 | Damage increases as HP drops |
| 6 | **Necromancer** | Wisdom + Fth >= 35 | Summon undead allies |
| 7 | **Samurai** | Dex + End >= 35 | Perfect parry window extended |
| 8 | **Alchemist** | Any 2 stats >= 18 | Enhanced consumable effects |
| 9 | **Knight** | Defense + Str >= 35 | Buffs **defense & melee together** — stronger at each than a baseline but weaker than dedicated Paladin (defense) or Warrior (melee); increases equip-load carry (armor grants more defense) |
| 10 | **Archer** | Dex >= 20 | Ranged **accuracy & handling** (faster nock/reload, less sway) |
| 11 | **Enchanter** | Intelligence + Wisdom >= 35 | Control/zone mage (slow, roots, area denial) |
| 12 | **Brawler** | Str >= 20 | Unarmed/grapple crowd control |
| 13 | **Paladin** | Fth + End >= 35 | Holy tank/support (taunt, guard allies, sacred armor effectiveness) |
| 14 | **Bard** | Fth + Intelligence >= 35 | Party-wide buffs/auras |
| 15 | **Blacksmith** | Crafting skill >= level 10 | Crafting/forge support: gear upgrade success, repair, forging bonuses (skill-based, not stat) |
| 16 | **Taoist** | Wisdom + Intelligence >= 35 | Qi manipulation: enhanced spell cooldowns & stamina regen; demon damage bonus |
| 17 | **Monk** | Faith + Endurance >= 35 | Inner peace: meditation heals HP; reduced stagger, +defense while unarmed |

Classes are **exclusive — exactly ONE class is chosen at a time.** The player holds a single
`UnlockedClassIds` entry (the chosen class); switching via the Class tab *replaces* the choice
(no accumulating roster, no "unlock all eligible classes" pass). Any class with a highlighted
"(current)" state; the others are every-known-class options that swap the choice on confirm.
The rule *"player can only have 1 class, 1 race at a time"* is enforced in `ClassUnlocker`
(single-choice `UnlockedClassIds`, `SetActiveClass` replaces, `EvaluateAll` only guarantees a
valid baseline — Wanderer) and UI (`CharacterInfoUI.BuildClassOptions` marks the sole current).

**Switching (current build):** changing the active class is **not gated by unlock state** — any
known class may be chosen at any time and simply *becomes* the single unlocked class; the class
dialog no longer shows requirement summaries or locks. Race changes are likewise single-choice —
only Human or an actually-discovered race is selectable and a non-Human change costs a Ritual
Stone (no auto-unlock) — see §3.5.

#### 3.2.1 Class Skill Trees

Each class owns a small **radial skill tree** — one **hub** at the center plus **3 thematic paths** of
3 nodes each (path parent + 2 leaves/capstones), ~10 skills per class (~180 total across all classes).
Trees are built in code (`ClassSkillCatalog`) mirroring the main skill catalog; no asset files.

- **Auto-granted:** all 10 skills are granted the moment the class unlocks — there is no point
  economy and no per-node gating. The tree is informational (what the class grants), not a build budget.
- **Live only for the ACTIVE class:** passive modifiers are aggregated by `ClassPassiveManager` and
  swap in/out when the player switches classes — a Warrior passive stops applying the moment the
  active class changes to Mage (each has its own tree).
- **Castables** run through the shared `ClassSkillCaster` (per-class cooldown/cost keys), hotkey-bound
  via the same bindings UI as regular skills.
- **Modifier kinds (additive):** `MeleePowerMul`, `SpellPowerMul`, `CooldownMul`, `AttackSpeedMul`,
  `BackstabMul` (scales crits from behind), `HealPowerMul`, `BerserkScale` (dmg up as HP drops),
  `ParryWindowMul`, `ConsumablePotencyMul`, `DefenseMeleeMul`, `EquipLoadBonus`, `RangedHandlingMul`,
  `AuraStrength`, `BlockingMul` (÷ block stamina drain), `StaggerResistMul` (÷ knockback),
  `CraftSuccessMul`, `RepairMul`, `StaminaRegenMul`, `HpRegenPerSecond`.
- **Persistence:** the single chosen class id + the active class are saved/loaded
  (`SaveData.unlockedClassIds`, `activeClassId`); on restore any legacy multi-class roster
  collapses to the saved active class (Wanderer baseline if unknown), so old saves migrate
  gracefully and never re-gain the old "unlock everything" pass.
- **UI:** the Skills panel has a **General / Class / Race** sub-toggle. General shows the full 6-category tree;
  Class shows the active class's radial tree (hub + paths) with an auto-grant detail pane. (Talents live
  on the Character Info panel — see §3.9.)

#### Race ↔ Class Synergy

Each class's 3 paths are themed around different **racial archetypes** (§3.5), so the player's race
naturally synergizes with one path more than the others. This gives every race a "home" in multiple
classes, and every class appeals to 2-3 racial archetypes:

| Class | Path A (archetype) | Path B (archetype) | Path C (archetype) | Best Races |
|---|---|---|---|---|
| Wanderer | Survivor (all-around) | Crafter (utility) | Fighter (melee) | Human, Dwarf, Orc |
| Warrior | Brute (raw power) | Bulwark (defense/taunt) | Duelist (speed) | Orc/Golem, Fire Giant, Werewolf |
| Mage | Fire (damage) | Frost (control) | Arcane (cooldown) | Draconic, Ice Giant, Wraith/Elf |
| Rogue | Shadow (backstab) | Vampiric (lifesteal) | Dagger (speed) | Vampire, Serpent-kin, Harpy |
| Cleric | Light (healing) | Guardian (aura) | Restoration (regen) | Celestial/Angel, Angel, Dwarf |
| Berserker | Rage (berserk) | Frenzy (speed) | Might (raw) | Orc/Demonkin, Werewolf, Orc/Fire Giant |
| Necromancer | Undead (summons) | Blood (lifesteal) | Shadow (spells) | Wraith/Undead, Vampire, Elf |
| Samurai | Blade (parry) | Bushido (balance) | Precision (backstab) | Elf, Human, Skeleton |
| Alchemist | Potion (consumables) | Toxin (CC) | Forge (craft) | Gnome/Goblin, Serpent-kin, Dwarf |
| Knight | Iron (defense) | Wall (blocking) | Crusader (holy) | Golem/Fire Giant, Orc, Draconic/Angel |
| Archer | Marksman (ranged) | Wind (speed) | Trapper (CC) | Harpy/Elf, Elf, Goblin |
| Enchanter | Time (cooldown) | Frost (CC) | Charm (aura) | Elf, Ice Giant, Succubus |
| Brawler | Fist (power) | Grapple (CC) | Shout (defense) | Orc/Werewolf, Demonkin, Golem |
| Paladin | Oath (heal+def) | Guard (blocking) | Smite (holy) | Celestial/Angel, Golem, Draconic |
| Bard | Song (aura) | Dissonance (CC) | Drums (speed) | Celestial, Succubus, Orc/Elf |
| Taoist | Qi (stamina) | Symbol (spell) | Flow (CC) | Undead/Elf, Celestial, Elf/Fishmen |
| Monk | Body (defense) | Mind (heal) | Fist (melee) | Golem, Celestial, Orc |
| Blacksmith | Forge (craft) | Anvil (repair) | Ember (block) | Dwarf, Golem, Draconic |

### 3.3 Skill System (3-layer branching tree + use-based XP)

A **use-based skill progression** spans 7 categories with a **3-layer branching tree** (~2,077 skills total). No fixed class requirements — any player can advance any category based on how they play. Skills level by gaining XP in their category (with racial multipliers) and grant flat tier rewards at levels 5/10/15/20/25.

**Per-skill levels (current build):** on top of the category bar, every **learned skill also levels
itself** — each successful use grants skill-level XP (unaffected by prereqs, boosted by the race's
all-XP bonus and any matching talent, §3.9), following a linear threshold curve to a cap of level 100.
The skill detail pane shows "Lv N · XP x/y" for a learned active skill, and learned nodes in the tree
display their level. Magic's tree currently runs **9 L1 roots — the six classic schools plus Lightning,
Water, and Earth as their own schools** (Chain Lightning hangs under Lightning's root, not Fireball's;
Water soaks with Wet; Earth reshapes terrain instead of applying a status).

#### 3-Layer Branching Structure

Each category has a **3-layer tree**:

```
Layer 0 (base):     5-14 skills per category — foundational passives and core actives
                     ↓ each branches into 5
Layer 1 (branch):   25-70 skills per category — specialized variants (elemental, stat focus)
                     ↓ each branches into 5
Layer 2 (deep):     125-350 skills per category — mastery-level abilities
```

| Category | Layer 0 | Layer 1 | Layer 2 | Total |
|----------|---------|---------|---------|-------|
| Melee | 10 | 50 | 250 | 310 |
| Ranged | 10 | 50 | 250 | 310 |
| Magic | 16 | 80 | 400 | 496 |
| Stealth | 10 | 50 | 250 | 310 |
| Crafting | 10 | 50 | 250 | 310 |
| Fortitude | 10 | 50 | 250 | 310 |
| Shield | 1 | 5 | 25 | 31 |
| **TOTAL** | **67** | **335** | **1675** | **2077** |

**Prerequisites:** Each Layer 1 skill requires its parent Layer 0 skill. Each Layer 2 skill requires its parent Layer 1 skill. This creates clean branching paths — players must invest down a specific branch.

**Specialization:** Players earn ~1 skill point per category level-up (max ~25 points per category at level 25). With 310+ skills per category, players must **specialize** in 1-2 branches rather than filling the whole tree.

**Effect scaling:** Layer 1 skills are ~1.3× stronger than their parent. Layer 2 skills are ~1.7× stronger. Costs scale proportionally (1.35× per layer). **All tree passives are themed perks** (`Perk(PassivePerkType, value)`, aggregated by `PassivePerkManager` → `PlayerStats` per §3.3) — flat stat-buff passives are retired. There are **20 perk kinds** spanning offense (AttackPower / SpellDamage / CritChance / CritDamage / Backstab / AttackSpeed), resources (StaminaMax / FocusMax / StaminaRegen / FocusRegen / HealthRegen / MaxHealth), defense (DamageReduction / BlockEfficiency / StaggerResist / ParryWindow), and utility (MovementSpeed / CooldownReduction / HealPower / LootLuck). Each node chooses the kind that fits its name/branch identity and carries unique tooltip flavor (see the `SkillCatalog.*.cs` partials; 446 perk nodes across Melee / Ranged / Magic / Stealth / Fortitude / Crafting). Percent perks accumulate as **integer percents** (`Σ 5+3 → PassivePerkManager.Mul = 1.08`), flats accumulate raw via `Sum` — the class/race modifier managers instead take fractional amounts (`0.08` = +8%). Perks compound with the same class (`ClassPassiveManager`) and race (`RaceSkillPassiveManager`) multipliers where they overlap (§3.2.1, §3.2.2).

#### Skill Categories

```
   [MELEE]  [RANGED]  [MAGIC]  [SHIELD]
      \        |        /        /
      [SURVIVAL: STEALTH + CRAFTING + FORTITUDE]
```

**Shield category (current build):** the shield-tree (root `shield_bash`, 5 branches, 25 children) was
split out of the Melee tree so the shield reads as its own identity — **bash / guard / counter**, played
with the *equipped shield* (every Shield skill drives `ShieldWeaponBehavior`'s own bash animation + face
hitbox with knockback). Shield skills **require a shield in hand** — a learned Shield skill can't fire
(or spend its cost) while the player carries no shield. The wheel puts Shield under the center PHYSICAL
tree (5 wedges: Melee / Ranged / Stealth / Fortitude / Shield).

#### Skill Types

| Type | Description | Examples |
|------|-------------|---------|
| **Category Passive** | Tier reward from leveling a category | +10% stamina regen, +5% crit |
| **Active** | Equippable combat ability | Fireball, Heal, Backstab |
| **Weapon Art** | Weapon-specific unique skill | Whirlwind, Shield Bash, Arrow Rain |
| **Ultimate** | Powerful endgame ability | Meteor, Time Slow, Blood Rite |

#### Skill Book Expansion

- **Skill Books** found in the world or bought from merchants can:
  - Unlock new **Active / Weapon Art / Ultimate** skills
  - Grant bonus skill points or category XP
  - Reveal hidden ultimate paths
- The system is **expandable** — new skill books can add entirely new skills and categories post-launch.

### 3.4 Stats (11 Core)

The stat system was redesigned into **11 stats**. The **Arcane stat** was removed (its functions split into **Luck** and **Wisdom**) — note this is distinct from the **Arcane damage type** (§3.7), which remains a separate combat element. Old names were renamed for clarity: *Vigor→Health*, *Mind→Intelligence*, *Intelligence→Wisdom*. New stats added: **Speed**, **Defense**, **Luck**, **AttackSpeed**.

| Stat | Effect |
|------|--------|
| **Health** | Max HP, HP regen, resistance to status effects |
| **Speed** | Movement speed, dodge speed, **small** attack-speed bonus |
| **Endurance** | Max stamina, equip load (heavier armor/weapons) |
| **Strength** | Melee damage (heavy), stagger power |
| **Dexterity** | Light/one-handed melee damage, ranged **accuracy**, parry window, dodge i-frames, weapon-swap speed |
| **AttackSpeed** | **Primary** source of attack speed (larger per-point than Speed's bonus) |
| **Defense** | Flat **physical** damage reduction (equipment/armor-based) |
| **Intelligence** | Max FP (mana), skill cooldown reduction |
| **Wisdom** | Magic damage, spell power |
| **Faith** | Miracle/healing power, buff duration |
| **Luck** | Loot quality, crit chance, crafting luck, status-effect luck |

**Leveling:** Earn XP from combat, quests, exploration. Spend points on stats at bonfires/rest points.

**Stat splits (design notes):**
- **Speed vs AttackSpeed vs Dexterity:** Speed = raw velocity (movement velocity + dodge speed), with only a *small* effect on attack speed. AttackSpeed = the dedicated stat for **how fast you swing/attack** — a large per-point effect. Dexterity = *precision finesse*: light/one-handed melee damage, ranged accuracy, parry window, dodge i-frame quality, weapon-swap speed (it also contributes a little attack speed as finesse).
- **Strength vs Dexterity (damage):** Strength scales **heavy/melee** damage and stagger. Dexterity scales **light/one-handed melee** and finesse. Ranged **damage** scales with the **weapon itself** (bows/arrows have their own damage ceiling) — stats instead govern how well a player *uses* a ranged weapon: **Dexterity** for accuracy, **Endurance** for equip load (heavy bows), **Speed/Dexterity** for handling.
- **Defense vs Health:** Health = your HP pool and regeneration. Defense = flat reduction of incoming **physical** damage. **All resistance — physical, elemental, magic — comes from equipment (armor/gear) only, not from stats.** No stat grants damage resistance.

#### Derived Stat Formulas

Stats convert to gameplay numbers via these formulas. **Racial % modifiers apply to the stat BEFORE these formulas run**, so a racial bonus compounds (grows) as the player invests and levels that stat. Scaling coefficients marked `k_*` are **balance knobs** finalized during implementation/tuning.

```
MaxHP          = 100 + (Health × 12)
MoveSpeed      = base + (Speed × k_mov)               # movement velocity
DodgeSpeed     ×= 1 + (Speed × k_dodge)
AttackSpeed    ×= 1 + (AttackSpeed × k_as)            # PRIMARY attack-speed source (large)
AttackSpeed    ×= 1 + (Speed × k_as_speed)            # small bonus only (k_as_speed ≪ k_as)
AttackSpeed    ×= 1 + (Dexterity × k_as_dex)          # small finesse bonus (k_as_dex ≪ k_as)
MaxStamina     = 100 + (Endurance × 10)
EquipLoad      = 40  + (Endurance × 2)                # weight units of armor/weapons
MeleeAtkPower  = base + (Strength × k_str)            # heavy/melee → DamageCalculator.AttackPower
StaggerPower   = base + (Strength × k_stag)
LightAtkPower  = base + (Dexterity × k_lt)            # light/one-handed melee
RangedAccuracy = 1 + (Dexterity × k_racc)
RangedDmg      = weapon.base                          # ranged damage = weapon ceiling, NOT stats
ParryWindow    = base + (Dexterity × k_parry)
DamageReduc    = clamp(Defense × k_def, 0, 0.8)       # flat physical DR, capped
MaxFP          = 50  + (Intelligence × 10)
CooldownMult   = 1 − (Intelligence × k_cool)          # faster ability cooldowns
MagicAtkPower  = base + (Wisdom × k_mag)              # → DamageCalculator.ElementalPower
HealPower      ×= 1 + (Faith × k_heal)
BuffDuration   ×= 1 + (Faith × k_buff)
CritChance     = 5%  + (Luck × 0.15%)                 # → DamageCalculator critical
LootQuality    = base + (Luck × k_loot)
CraftLuck      = base + (Luck × k_craft)
StatusProcLuck = base + (Luck × k_status)             # poison/bleed/rot/frost procs
```

In the implementation the player's walk/sprint velocity is `(BaseMoveSpeed + Speed·k_mov) × TreeMul(MovementSpeedPercent)`, scaled by the controller's `MoveSpeed` via `MaxMoveSpeed/BaseMoveSpeed` (class/race passives layer in too), so `MoveSpeed = base + Speed·k_mov` matches the formula above. Tree perk percentages aggregate as **integer percents**: `PassivePerkManager.Mul(kind) = 1 + Σpercent/100` (e.g. the 40 movement perks sum to +158% → ×2.58), with flats accumulating raw via `Sum` (e.g. `HealthRegenPerSecond` 0.003 = +0.3%/s). A temporary **dev all-stats floor** (`DevMaxAllStats`, which forced Speed=100) was **removed in 1cc**, and a 100× multiplier-aggregation bug (`Mul = 1 + Σpercent` instead of `/100`) was **fixed in 1cd**, so velocity again reflects the character's real Speed stat and the intended perk package.

Example — a race with **Health +20%**: at base Health 30 → total 36 → MaxHP = 100 + 36×12 = **532** (vs unmodified 460). Because the bonus scales with the total stat, it represents ~16–18% more HP in the late game.

**Damage calculator wiring:** Strength/Dexterity/Wisdom/Luck feed the `DamageCalculator` context (AttackPower, LightAttackPower, ElementalPower, CriticalMultiplier). Ranged damage uses the **weapon's base damage** directly. Incoming damage is reduced by **equipment-based resistances** (armor physical DR, gear elemental/magic resist) — no stat contributes resistance. The 7 skill-XP categories (Melee, Ranged, Magic, Stealth, Crafting, Fortitude, **Shield**) are separate from the 11 stats.

### 3.5 Race System (22 Races)

Players pick a race at **character creation** (weighted-random roll that auto-commits, or manual pick) — that single race is the player's chosen race. Races are also **discoverable in the world** at altar/ritual sites, which mark them as selectable. The race system is **exclusive — exactly ONE race is active at a time** (§"player can only have 1 class, 1 race at a time"). A **mid-play change** is possible to Human (always free) or an actually-discovered race, and a non-Human change **costs a rare Ritual Stone** (the Change Race tab lists only Human + discovered races and never auto-unlocks; the test ground grants no full-roster unlock and no starter stones).

#### How Races Modify Stats

Racial stat modifiers are **percentages applied to the TOTAL stat on-the-fly**, so they **scale as the player levels**:

```
TotalStat = BaseStat × (1 + RacialPercent)
```

- Positive % (e.g. Str +20%) multiplies the whole stat, growing stronger with investment.
- Negative % (e.g. Dex -10%) is a permanent handicap the player must build around.
- Modifiers recalc immediately whenever the player levels or changes race.

#### Skill XP Categories (7)

Instead of a node-based skill tree, skills level via **use-based XP**. Each category has its own XP bar and flat tier rewards at levels 5/10/15/20/25. Races grant **XP multipliers** in categories that match their archetype, pushing builds in a natural direction.

| Category | Tracks | Example Uses |
|----------|--------|--------------|
| **Melee** | Melee proficiency | Sword/mace/axe damage, combos, stagger |
| **Ranged** | Ranged proficiency | Bow accuracy, crossbow, thrown weapons |
| **Magic** | Spell/miracle proficiency | Spell power, cast speed, FP efficiency |
| **Stealth** | Stealth/survival proficiency | Sneak damage, detection range, lockpicking |
| **Crafting** | Crafting/gathering proficiency | Potion potency, upgrade success, yield |
| **Fortitude** | Defensive proficiency | Shield stability, armor effectiveness (physical DR), perk effectiveness |
| **Shield** | Shield proficiency | Bash/slam/counter skills in the Shield tree; bash power, knockback (requires a shield equipped) |

#### The 22 Races

Stat modifiers shown as %. Weights shown for the random-roll. XP bonus = skill categories that level faster.

| # | Race | Stat Modifiers | Passive | Weight | XP Bonus |
|---|------|----------------|---------|--------|----------|
| 1 | **Human** | None (AS+0) | +15% XP from all sources | 50% | All +15% |
| 2 | **Fire Giant** | Health+20 Str+20 End+15 Speed-5 Int-15 AS-15 | Fire resistance (50%), lava walk | ~2.38% | Endurance +15% |
| 3 | **Serpent-kin** | Luck+15 Dex+15 End+10 Str-10 AS+5 | Venom Blade: physical attacks apply venom DoT for 8s | ~2.38% | Magic +10%, Stealth +10% |
| 4 | **Draconic** | Str+20 Wisdom+15 End+5 Int-10 AS+5 | Fire resistance (40%), Dragon Roar (stagger nearby, 30s CD) | ~2.38% | Strength +10%, Faith +5% |
| 5 | **Golem** | Str+25 End+25 Health+15 Speed-10 Int-20 AS-15 | **Stone Skin:** 25% physical + 25% magic dmg reduction; move speed -20% | ~2.38% | Endurance +15%, Fortitude +20% |
| 6 | **Celestial** | Faith+25 Int+10 Health+10 Str-10 AS+5 | Healing miracles 20% stronger | ~2.38% | Faith +15% |
| 7 | **Wraith** | Wisdom+25 Luck+15 Int+10 Health-15 AS+5 | **Immaterial:** pass through all physical objects, immune to physical dmg, spell-caster only; no dash/run. Takes +30% magic dmg, +50% holy dmg | ~2.38% | Magic +15% |
| 8 | **Undead** | Health+10 End+15 Dex+10 Int-10 AS+5 | Infinite stamina. Takes +25% fire dmg, +25% holy dmg | ~2.38% | Endurance +15% |
| 9 | **Skeleton** | Dex+20 Str+10 End+10 Health-15 AS+10 | Bleed immune, +20% move speed, infinite stamina | ~2.38% | Fortitude +15% |
| 10 | **Werewolf** | Str+20 Dex+20 End+5 Int-15 AS+10 | Night: +25% move speed + 2% HP regen/s. Claws deal bleed | ~2.38% | Melee +15% |
| 11 | **Goblin** | Dex+20 Luck+15 End+5 Str-10 AS+5 | +20% loot quality, 15% smaller hitbox | ~2.38% | Crafting +15%, Stealth +10% |
| 12 | **Orc** | Str+25 Health+15 End+10 Int-15 AS+5 | +15% stagger damage, passive HP regen (1% max HP/s) | ~2.38% | Melee +15%, Endurance +10% |
| 13 | **Ice Giant** | Health+15 Str+20 End+20 Speed-5 Int-15 AS-15 | Cold immune, freeze aura (nearby enemies slowed 20%) | ~2.38% | Endurance +10%, Strength +10% |
| 14 | **Vampire** | Dex+20 Luck+10 Wisdom+10 Int+5 Str-10 AS+10 | 5% lifesteal on hit, +15% move speed. Sunlight: 5% max HP burn/s | ~2.38% | Magic +10%, Stealth +10% |
| 15 | **Demonkin** | Str+20 Wisdom+15 End+10 Faith-15 AS+5 | Fire resistance (40%), fire aura (1% max HP/s to nearby) | ~2.38% | Magic +10%, Melee +10% |
| 16 | **Angel** | Faith+25 Int+15 Wisdom+10 Str-10 AS+5 | Elemental resist (20% fire/ice/lightning/magic) via gear; weak to physical (+15%) and dark (+25%) | ~2.38% | Faith +15% |
| 17 | **Succubus/Incubus** | Dex+15 Wisdom+10 Luck+10 Int+10 Str-15 Health-10 AS+10 | Charm Gaze: opposite-gender targets have 10% chance to be confused | ~2.38% | Magic +15% |
| 18 | **Fishmen** | Dex+10 End+15 Health+15 Str+10 Faith-10 AS+5 | Swim speed +50%, breathe underwater, water dmg immune | ~2.38% | Ranged +10%, Crafting +10% |
| 19 | **Harpy** | Dex+25 Luck+15 Str-15 End-20 AS+10 | Glide (slow fall), jump height +30% | ~2.38% | Ranged +15% |
| 20 | **Dwarf** | Str+15 Faith+10 End+25 Dex-10 AS-5 | +20% crafting yield, forge discounts | ~2.38% | Crafting +15%, Fortitude +10% |
| 21 | **Gnome** | Luck+35, all 10 other stats -10% | **Lucky Find:** +40% loot bonus (best loot/crit/craft/status luck in the game), 15% smaller hitbox | ~2.38% | Magic +15%, Crafting +10% |
| 22 | **Elf** | Dex+20 Wisdom+15 Str-10 AS+10 | +8% all XP, enhanced perception (see hidden at +20% range) | ~2.38% | Magic +10%, Ranged +10% |

#### Stat Balance & Tiers

Races deliberately use a **wide net-stat-budget spread**, because racial % modifiers compound with leveling (§3.4). Races with a lower stat budget are compensated with **stronger passives, utility, or XP bonuses** so every archetype stays viable — races differ in *where* their power sits as much as *how much* raw stat power they carry.

| Tier | Net Budget | Races | Compensation for the gap |
|------|-----------|-------|--------------------------|
| **Strong** | +45 | Vampire, Fishmen, Angel | Vampire: harsh sunburn (5% max HP/s in daylight); Fishmen: situational water-bias; Angel: weak to physical & dark |
| **Good** | +40 | Celestial, Wraith, Werewolf, Orc | Each has a meaningful defensive/utility weakness |
| **Fine** | +35 | Serpent, Draconic, Skeleton, Goblin, Demonkin, Dwarf, Elf | Moderate weaknesses; XP bonuses |
| **Mid** | +30 | Undead, Succubus | XP bonuses + light weakness (fire/holy, frailty) |
| **Tank** | +20 | Fire Giant, Golem, Ice Giant | Strong defensive passives: Stone Skin (physical+magic −25%), freeze aura, fire resistance — pure-tank identity |
| **Aerial** | +15 | Harpy | Mobility (glide, enhanced jump) + Ranged XP; glass-cannon utility |
| **Baseline** | 0 | Human | +15% XP from all sources; the default/no-penalty race |
| **Handicap** | −65 | Gnome | **Best loot/crit/craft/status luck in the game** via Lucky Find + Luck stat (top-tier loot) + Magic/Crafting XP — high-risk glass cannon, weak in every other stat (all 10 others −10) |

> **Note on Human vs Elf:** Human (0 stat budget, **+15% XP**) is the **safe default** (50% weight, no penalties) — the only race whose identity is raw XP gain. Elf (+35 budget, +8% XP) is a stricter min-max pick trading XP for stats. Human's identity is reliability + faster progression; Elf's is raw stat advantage.

#### Race Selection & Weighted Random

- **Human 50%** chance; **each of the other 21 races ~2.38%** (50% ÷ 21).
- The roll **auto-commits** (player keeps what they roll).
- All races remain **manually pickable** if the player prefers a specific one.
- Locked races are revealed by **world discovery points** (altars/ritual sites); interacting unlocks them for this and future characters and enables mid-play transform.

#### Race Change (Mid-Play)

- Discovered races can be swapped to at any **Race Discovery Point**.
- Cost: **1 Ritual Stone** (rare consumable). Human is always free.
  *(Current build: the change dialog calls `SetActiveRace(requireStone: false, unlockIfNeeded: true)` —
  changing race is **free and auto-unlocks the target race** for this character; the Ritual Stone cost
  applies to the world-discovery flow.)*
- On change: `PlayerStats` modifiers refresh, the player model **rebuilds** with the race's palette + body ratios (§3.5 Race Visuals), `RaceRig` applies the uniform scale, `RacePassiveManager` re-applies passives. Current HP/FP/stamina preserved as % of their new max.

#### Player Model (Faceted Low-Poly Character, 1dw + 1dx + 1dy + 1dz + 1e0 + 1e1 + 1e2 + 1e3 + 1e4 + 1e7 + 1e8 + 1ep)

- Every body part except the torso/chest silhouette (1e2/1e4 below), the neck cylinder (1e1) and the
  plain joint balls is a **unit-space faceted ellipsoid mesh** instead of a box
  (`PlayerPartMesher`): a chunkier Rings 7 × Segs 12 corner lattice (NON-UNIFORM spacing since 1ep,
  so the panels come out different sizes) generated once per part profile,
  occupying the same half-extent cube [-0.5, 0.5] as the old shared unit cube — so a part
  GameObject's `localScale` = its size vector reproduces the exact world dimensions. `MakePart`
  (`MapBuilder`) builds these on the same pivot hierarchy the animator/weapon rigs expect;
  `MakeBlock` still serves creatures, vehicles and props.
- Parts are **sculpted with terrain-style "dents"** (the `WorldStreamer.DeformAt` crater carve
  generalized to 3D): per-vertex influence from a normalized ellipsoid distance to an anchor, the
  same `s = t²(3−2t)` smoothstep, the vertex pushed along its original radial. Examples: waist pinch +
  chest raise on the torso, eye sockets + nose + chin on the head, deltoid/elbow/wrist tapers on the
  arms, knee taper + calf + quad on the legs, bell flare on the skirt; hair/hairband keep their own
  slim parts. Eyes are thin bulging discs seated into the head's eye-socket dents.
- The sculpted corners are then emitted as a **chunky low-poly mosaic (1dx)** covering the whole
  skin: every band cell becomes a flat-shaded **square** panel or a pair of flat-shaded **triangle**
  panels (deterministic per-cell hash, squares dominant, pole fans always triangles), and every
  shared corner gets a small deterministic tangent jitter so the panel boundaries read hand-cut —
  yet the corners stay shared, so the mosaic is watertight (no cracks/see-through). Each panel
  carries its own flat (face) normal, so the facets visibly catch the light; the dent silhouettes
  still read through the facets.
- **Irregular facet sizes (1ep)**: the lattice spacing itself is no longer uniform. Band heights
  (ellipsoid phi rows / torso rows) and segment widths (theta, shared by every row so cells stay in
  aligned azimuth planes) follow a deterministic **±20%** schedule per part
  (`Steps`/`Positions` in `PlayerPartMesher`, derived from the same `Hash01`/`AnchorSeed`), so the
  panels come out as **different-sized cells of a hand-cut stone mosaic** instead of an orderly
  same-size grid — while every corner stays a single shared position, so each cell still covers its
  area exactly and the part remains watertight. Poles and the torso endpoints (`t = 0` / `t = 1`,
  the hip row + crown disc) stay fixed, so silhouettes, the neck/pivot contract and the 1e4 dome
  geometry are untouched. UVs now ride the same schedule (`θ/2π`, `1−φ/π`), so a future texture's
  texel density tracks panel size (invisible today — parts are solid colors). All parts share the
  look: ellipsoids, the torso silhouette, the neck cylinder and hair/eyes.
- Meshes are **static and size-independent** — one cached mesh per profile serves every gender, race
  ratio and model variant. Sizing happens purely on `Transform.localScale`, so race ratios (§3.5
  below) and weapon hand-scale compensation (`WeaponRigBuilder.ScaleForWorld`) keep working untouched.
- **Slim torso + bigger build (1dy)**: the torso is ~12% narrower (standing `Body` 0.50→0.44 male /
  0.46→0.40 female; seated 0.38→0.34; sit `Torso` 0.42/0.46→0.37/0.40, `Chest` 0.44→0.39) and the
  whole model runs +8% (`PlayerModelScale = 1.08f`) on every root's `localScale`/position — feet are
  re-planted with the same factor in `ApplyRaceLook`. Hitbox unchanged (CharacterController / `RaceRig`
  own collision; the +8% is visual only).
- **Faceted ball joints at limb pivots (1dy)**: a plain faceted `"Joint"`-profile sphere sits at each
  shoulder / elbow / hip / knee pivot (`JShoulder`/`JElbow`/`JHip`/`JKnee`, so no animator/weapon name
  collisions). It rotates with the pivot, inherits race-ratio pivot scaling, and is colored to match
  the adjacent part (arm sleeves `shirtC`, pants legs `pantsC`).
- **Cylinder neck (1e1)**: the `Neck` part is a unit-space **round cylinder column** instead of the
  round faceted ellipsoid (and the short-lived square pillar of 1dz) — 12 flat side facets matching
  the faceted-band count, plus closed top/bottom caps (`PlayerPartMesher.BuildCylinder`, profile
  `"Cylinder"`), still spanning the [-0.5, 0.5] cube so the same size-vector/localScale contract
  holds and the mesh stays size-independent/cached. Sized like the pillar it replaced: standing
  0.15×0.16, seated 0.13×0.10, sit 0.14×0.12.
- **Shouldered torso silhouette (1e2)**: the shoulder/hip pivots sit OUTSIDE a plain ellipsoid
  (pivot radii ~0.8–1.6 unit vs the 0.5 lattice radius), so dents could never close the last gaps.
  The `"Body"` / `"SitTorso"` / `"Chest"` profiles are now a dedicated **flat-facet torso silhouette**
  (`PlayerPartMesher.BuildTorso`): 12-seg × 7-band mosaic (same watertight jitter), bottom cap + a
  flat **top shoulder plateau** that the neck cylinder passes through (reads as the collar). Reach is
  baked per band — shoulders W 0.80 (world 0.35 on the standing body, covering pivot ±0.28), waist
  pinch 0.46, hip flare 0.55; `Chest` carries the sit shoulders (0.72 mid-band), `SitTorso` plateaus
  0.70. The torso is the ONE mesh wider than the [-0.5,0.5] half-cube by design (world half-width =
  `size.x · W`; height still `size.y` exactly). *Historical (see 1e4): the flat plateau read as a
  collar ring / hat brim around the neck base — SUPERSEDED by the 1e4 sloped shoulder-dome + crown
  below; the plateau wording here is kept as history only.*
- **Fuller torso depth (1f1)**: the standing torso read as a flat slab — standing `Body` was
  `(0.44, 0.80, 0.25)`, so with `dB ≤ 0.50` the world half-depth never passed `0.125` against a
  half-width of `0.72·0.44 = 0.317` (**2.4–2.8:1**). Width is pinned by the shoulder reach
  (`0.68·size.x ≥ 0.28` → `size.x ≥ 0.412`), so the fix is depth: standing `Body` `size.z 0.25 → 0.32`
  (both genders). Ratios land at ≈1.9 chest/hips and ≈2.2 shoulder (was 2.4–2.8), matching the seated
  body's ≈1.75. One size scalar only — the mesh is size-independent, so seated/sit are untouched
  (seated keeps its own `size.z = 0.28`).
- **Shoulder-dome torso silhouette (1e4, top pulled in 1e8)**: supersedes the 1e2 flat top plateau /
  hat-brim wording. BuildTorso ends at a small CROWN disc (W ≈ 0.20 ≈ the neck radius, world 0.088,
  tucked flush under the neck base) instead of a hat-brim flat cap. The upper bands slope shoulder
  shelf W 0.70 → dome W 0.60 → crown 0.20 along the fine 9-row dome schedule, so the shoulder/collar
  seam reads as a smooth slope with no plateau ring. 1e8 pulled the top two rows in (deltoid shelf
  0.80→0.70, dome 0.74→0.60) so the existing ±0.28 shoulder JOINT BALLS poke out of the dome as
  visible round caps (the 1e4 reach of 0.347 world swallowed them flush). Because the shoulder pivots
  sit at the very TOP of the part's silhouette, the `"Body"` parts are BUILT TALLER in MapBuilder so
  the pivots land on the dome band instead:
  - standing `Body` 0.8 tall, torso-local center 0.13 (spans torso-local [−0.27, 0.53]) → pivots sit
    at t≈0.78 on W 0.68 → world **0.299** (female 0.40·0.68 = **0.268**) — the ±0.28 ball now pokes
    ~5 cm (male) / ~8 cm (female) out of the dome as a visible cap; chest 0.72 stays the widest point;
    crown world 0.088 tucks under the neck+head base;
  - seated `Body` 0.6 tall @ root center 0.25 (spans [−0.05, 0.55]) → pivots sit at t≈0.87 on W 0.60 →
    world **0.204** (ball ±0.24 → cap clearly outside the dome); crown flush under the seated neck
    base [0.50, 0.60] (radius 0.065);
  - sit model: `Chest` carries its shoulder pivots at t≈0.43 on the pulled-in mid rows (W 0.64 →
    world **0.250**, ball ±0.25 → ~6 cm cap) and `SitTorso` still tucks its 0.46 top under the Chest
    bottom.
  Waist taper + shoulder slope both ride the finer 8-band (9-row) torso lattice (`bands = 8` local to
  BuildTorso — other parts still share the 7-band ellipsoid grid). Height still spans y ±0.5 so
  `size.y` scales it exactly like the old cube/ellipsoid.
- **Torso silhouette actually renders (1e7 routing fix, 1e8 closes the crown band)**: `BuildEllipsoid`
  remapped the three torso ids (`"Body"`/`"SitTorso"`/`"Chest"`) to the plain-ellipsoid fallback
  BEFORE `Generate` could reach its `BuildTorso` branch (they are never in `_profiles`), so the 1e2/1e4
  silhouette above was dead code from 1dw onward and the torso stayed a plain capsule in-game. The ids
  now short-circuit to the cached `BuildTorso` build. On first real render the top of the torso showed
  a see-through ring: the band between the dome row (t=0.875) and the crown row (t=1.0) was never
  emitted (`for b < bands` — the 8th gap was skipped, leaving the crown disc as a floating lid), fixed
  by emitting `b <= bands`; the crown cone is now connected and watertight.
- **Upper body follows the camera pitch (1e9)**: `PlayerAnimator` pitches the Torso pivot with the
  vertical look — looking DOWN leans the torso forward, looking UP leans it back
  (`lookTilt = LookPitch · TorsoLookBlend`; `LookPitch` positive = down, and +X rotation on the Torso
  pivot = forward lean, so the patient reads correctly). The cartoon-run pose also pitches the torso
  forward with speed (+12° at sprint); the bobbing head baseline keeps its own slight counter-tilt.
  - **Gait rate follows movement speed (1jn)**: the walk/run cycle is driven by the character's **measured
    planar speed**, not a fixed ladder. The old rule was `cadence = 1.8 + norm * 2.0` Hz, whose large constant
    floor made even a slow walk cycle at ~2.8 Hz and put a sprint at **3.8 Hz = 7.6 steps a second** - far
    past the point where the cycle can be read. It is now `cadence = speed / StrideLength` (`StrideLength`
    **5.6 m** per **full** cycle, i.e. two steps, raised from 4.3 by 1jp), which is the physically honest
    relationship: one cycle advances the body by exactly one stride, so the feet plant instead of skating,
    and the rate rises and falls with real speed for free. Walk 5 m/s lands at **0.89 Hz (1.8 steps/s)**,
    sprint 10 m/s at **1.79 Hz (3.6 steps/s)**. The reason a *longer* stride is the lever, rather than a
    slower character, is that the base speeds are already a jog and a sprint
    (`MoveSpeed 5f` x `SprintMultiplier 2f`), so any honest cadence at 10 m/s is fast; 5.6 m buys fewer,
    bigger steps. **The two rate knobs trade against each other**, and this is worth stating plainly because
    1jp had to choose between them: `StrideLength` is the one that reads as "slower animation", and raising
    it lowers the cycle rate but also lengthens the ground a single cycle has to cover, so **a slower gait
    skates more** unless the leg swing amplitude is raised with it. `MaxCadence` (3.2 Hz) is the opposite
    lever - above it the feet **do** slide, because the animation can no longer express the real speed, and
    it is the knob to raise if the legs skate at a rate you are happy with. At the 5.6 m default it first
    binds at ~17.9 m/s, clear of sprint and of a +25% stacked-MoveSpeed build (12.5 m/s), so it is **not**
    what sets the on-screen rate. And `norm` (which drives the **pose** blend - arm/knee swing, the forward
    lean) still
    normalises against the raw `MoveSpeed`/`SprintMultiplier` fields, so it does not see the perk or water
    multipliers the measured speed does; a stacked build therefore poses slightly short of a full run while
    its legs keep the correct rate.
  - **Gait speed is smoothed and teleport-guarded (1jn)**: because the cadence is a **division** by
    `StrideLength`, the measured planar speed is low-passed (12/s) and any reading above 30 m/s - a respawn
    or teleport, orders of magnitude past locomotion - is discarded rather than smoothed. Without this a
    single spiky frame is amplified into the `_phase` integrator and leaves a *permanent* phase error behind.
    `OnEnable` also seeds `_lastRootPos` from the current position: it was never initialised, so the first
    `LateUpdate` measured the player against a `default(Vector3)` and reported its distance from the world
    origin as speed. That was nearly invisible under the old clamped `norm` (one frame of run pose) and is
    a multi-Hz phase burst under a divisor.
  - **Scalp-cap hair (1e3)**: hair was 4–6 floating slabs placed against an ideal sphere — the crown
  slab hovered 4 cm above the scalp and the side/back panels drifted off the skull. All `Hair`/
  `HairSide`/`HairBack`/`HairBand`/`Ponytail` parts are retuned to HUG the actual head hull (thin
  oblate cap lens resting on the crown so it neither floats nor gaps, side slabs buried ~1 cm into
  the skull, nape panel lapping the back shell), sized to the real head (0.3 standing/sit, 0.28
  seated) per variant. No hierarchy change — parent stays torso/root so `ApplyRaceRatioRecurse`
  `Hair*`/`Ponytail*` scaling (`headScale`) and all name lookups are untouched.
- **Seamless torso↔limb attachment (1e0)**: the faceted ellipsoid rounds the torso corners where the
  cube used to hide the arm/leg attach points, so the shoulder/hip pivots floated off the skin and
  left gaps. Fixed three ways that stack: (1) the `"Body"`/`"SitTorso"` profiles gained symmetric
  **shoulder-shelf dents** (+0.15 strength at the upper corners, no effect on chest centre/waist) and
  gentle **hip-flare dents** so the torso skin bulges out toward the pivots; (2) limb pivots tucked a
  little closer (standing shoulders 0.33→0.28, hips 0.13→0.12; seated shoulders 0.26→0.24; sit
  shoulders 0.27→0.25); (3) shoulder/hip joint balls enlarged (`JShoulder` up to 0.14–0.16, `JHip`
  0.13–0.15) so the balls lap over the seam. Pivot names/rotations unchanged → animator/weapon
  contracts unaffected (tucks are visual-scale only, ~2–4 cm). *Superseded by the 1e2 silhouette —
  the dents were no longer the (working) mechanism, but the pucks/tucks + joint sizes still apply.*

  Nb: the `"Body"`/`"SitTorso"`/`"Chest"` profiles carry NO dents at all (they are not in
  `_profiles`) — their shoulder/waist/hip reach is purely the baked silhouette W — so the "shoulder
  dents" wording above is history only; see 1e7 for why the silhouette is the mechanism.
- Part renderers are colored via `ApplyBlockColor` and carry **no collider** (the CharacterController
  owns collision). First/third-person camera culling is unchanged (model on layer 6).

#### Race Visuals (Palette + Body Ratios on the Shared Model)

- All 22 races share the procedural player model — it is recolored and re-proportioned per race, not
  swapped. `RaceData` carries the look, so races stay data-driven and real models can still drop into
  `RigPrefab` later without code changes.
- **Palette** (6 colors): skin, hair, eyes (whites stay white), clothes, pants, shoes. The female skirt uses the cloth color with a darkened hem. Human reproduces the original colors exactly.
- **Body ratios** (6 knobs, all default 1 = Human, clamped ≥ 0.6): `Height` / `Bulk` stretch the whole model; `Head` scales head+neck+eyes+hair; `ShoulderWidth` spreads the shoulder pivots; `Arm` / `Leg` lengthen the arm/leg chains (so players *see* correct proportions in 1st person arms and on the body). Ratios only ever move Transforms — the unit-space ellipsoid meshes scale with them, so no per-race mesh rebuild is ever needed. Representative silhouettes: Dwarf & Gnome are short and stocky (big head), Orc/Fire Giant broad-shouldered, Elf/Harpy tall and slim with long limbs, Skeleton/Harpy frail and thin.
- **Scale stays a hitbox matter**: `RaceRig` applies the race's uniform `RigScale` (Goblin 0.8 / Gnome 0.7 keep their smaller hitbox, Fire Giant 1.35 / Ice Giant 1.4 / Golem 1.4 / Draconic 1.2 read big) — it deliberately does **not** flat-tint the player model, whose colors already come from the race.
- **Race-aware builders (`MapBuilder.BuildPlayerModel` / `BuildSeatedPlayerModel` / `BuildSitPlayerModel`)**: read the live `RaceChangeManager` off the model's parent; `null` parents (car cutscene, etc.) fall back to Human so non-player models keep the default look. A race change wired to `RaceChangeManager.OnActiveRaceChanged` rebuilds the model with the new look and re-seats held weapons.

#### Expandability

- `RaceData` is a ScriptableObject — adding a race = creating a new `.asset` (zero code changes).
- New races can ship post-launch via updates / content drops.

### 3.6 Weapon Architecture (Expandable)

Weapons are built on a **4-category base — Melee, Ranged, Magic, Shield** — structured so new categories/subtypes drop in without touching existing code. The core principle: separate **what a weapon is** (data) from **how it attacks** (delivery behavior) from **how damage resolves** (damage pipeline).

#### Layers

- **Layer 1 — `WeaponData` (ScriptableObject, data-only).** Shared fields: id, display name, weight (equip-load), Str requirement (weight class, §5.5), base damage, speed, attack reach, scaling stat(s) + coefficients, `WeaponCategory`, `DamageType` (one of the 10 damage types, §3.7), and a Weapon Art reference. **Magic weapons** additionally carry magic mods — `MagicDamageMult`, `CastTimeMod`, `CooldownMod` (staff/wand/book scale spells). **Shield weapons** additionally carry guard mods — `BlockAbsorbPercent` (fraction of a blocked hit absorbed) and `BlockStaminaDrainMult` (multiplier on per-hit block stamina cost).
- **Layer 2 — `WeaponCategory` enum (expandable).** `Melee`, `Ranged`, `Magic`, `Shield`. Future values (Thrown, Summon, Hybrid, …) slot in as new enum entries + one behavior class each.
- **Layer 3 — Behavior modules via `IWeaponBehavior`.** A minimal contract: `BeginAttack(cmd)`, `ActiveFrame()`, `Cancel()`. One concrete module per category:
  - **`MeleeWeaponBehavior`** → existing `HitboxSystem` arc sweep.
  - **`RangedWeaponBehavior`** → projectile/raycast, **consumes ammo** (arrows/bolts from inventory), accuracy from Dexterity.
  - **`MagicWeaponBehavior`** → routes to the spell/skills pipeline; the equipped staff/wand/book's magic mods scale the spell (damage %, cast time, cooldown); costs FP; spell power from Wisdom.
  - **`ShieldWeaponBehavior`** → short hitbox bash on LMB (the equip's bash art, the §3.3 Shield category's bash identity) + enables the RMB guard; the shield's guard mods make blocking strictly stronger than the bare-hand guard.
  - `CombatController` talks **only** to `IWeaponBehavior` — it never knows melee vs ranged vs magic. **Adding a weapon kind = one new behavior class.**
- **Layer 4 — Damage pipeline & registry.** `DamageCalculator` (existing flexible `HitContext`) stays the single damage formula, extended to carry the weapon's `DamageType` (one of the 10 damage types, §3.7) for per-hit element/resist resolution. `WeaponDatabase` (ScriptableObject registry) holds all weapon assets and resolves each equipped weapon's category → behavior.

#### Per-Category Mechanics

| Category | Delivery | Damage Source | Key Stat | Resource |
|----------|----------|---------------|----------|----------|
| **Melee** | Hitbox arc | weapon.base + Str/Dex scaling | Str (heavy) / Dex (light) | Stamina |
| **Ranged** | Projectile / raycast | `weapon.base` (weapon ceiling) | Dex (accuracy) | **Ammo** (arrows/bolts) |
| **Magic** | Spell / skill pipeline | spell base × Wisdom, modulated by weapon magic-mods | Wisdom | FP |
| **Shield** | Short bash arc + guard | weapon.base + Str scaling | Str | **Stamina** (bash + block drain) · absorbs more / drains less than the bare-hand guard |

#### Notes

- Weapons carry a **single `DamageType`** — one of the **10 damage types** (§3.7); the damage pipeline resolves that element/type's resist/weakness.
- Dual-wield can pair **two of the same weapon type** — each hand holds one owned copy (one rig = one copy), subject to the §5.5 copy-accurate accounting rule: equipping the second hand consumes a spare bag copy, and without a spare the weapon *moves* instead of duplicating.
- **Draw vs stow (`WeaponRigBuilder.DrawScale`/`StowScale`, `WeaponStowAnimator`):** equipped weapons are **always drawn in the hands while fighting** and **sheath onto the body in casual mode** (back carry / waist scabbard) — in **any** camera view. The 1cr rule that kept them drawn in first person was reverted in `1ct`: a weapon at port arms reads as a fighting pose and does not belong in normal mode. Entering combat or leaving it drives an animated `.ApplyPose` transition; model rebuilds snap instantly.
- Shields are the **off-hand defense** (§5.5): a shield weapon equips to either hand; while held it enables RMB blocking and raises the guard's damage absorb (up to 95% on tower shields, vs. the bare-hand guard's 80%) while cutting the per-hit stamina drain to as little as 60%. Holding a shield *without* a melee weapon still blocks; with a **ranged or magic** weapon in the other hand the loadout enters the §5.16 per-hand dual scheme — the ranged (or magic) hand keeps its own draw/charge button while the shield hand guards while held (crossed-button mapping when a ranged weapon is present).
- Magic weapons are **equipped gear that scales/alters spells** rather than delivering their own attacks — distinct from melee/ranged, which deliver their own.
- **The player's base weapon is the Mage's Staff** (1ip, was the Wanderer's Iron Sword). One id spells it: `WeaponCatalog.StarterWeaponId = "staff"`, read by the bench spawn (`NewWorldTestGround.SpawnAllWeapons`) and the Character Info cycle-weapon fallback. So a fresh character starts on the **magic path** — `WeaponCategory.Magic`, Arcane damage, Wisdom scaling, the staff's `MagicDamageMult`/`CastTimeMod`/`CooldownMod` scaling every spell — and its draw pose is the staff's forward-lean hold, not the sword's upright grip. It is **single-wield by default** (one hand loaded), which is the same loading state the sword had; `BothHandsMagic` still needs both hands to hold a magic weapon. The Iron Sword itself is untouched and still in the 15-weapon roster as an ordinary equippable.
- Hand/wielding integration (§5.5): the equipped hand slots hold `WeaponData`; the categories of equipped weapons determine which behaviors are active. Wielding states modulate Str requirement as specified.
- Ranged ammo ties into the Inventory/consumables system.

#### Visuals — attack + defense animation sets

Melee and shield weapons ship **two animation sets each**: an attack swing set and a defense guard
hold. Both live in `WeaponAnimator` (mounted on each weapon rig by `WeaponRigBuilder`) as
keyframed pose tracks driving the arm pivots — "the animation pack lives on the weapon".

- **Attack set** — the windup → strike → recover limb pose-tracks per weapon (slash / jab / bash
  chains, e.g. the sword's 4-swing set, the shield's bash set). The arms are owned during the swing
  (`PlayerAnimator.SuppressArms`) and restored to rest on recovery.
- **Drawn hold pose** — the rest pose of a drawn weapon (the rotation from
  `WeaponRigBuilder.DrawPoseFor`). One-hand blades **and the staff** share one angle in the fist:
  90° yaw so the length reads side-on to the camera plus a 30° off-vertical cant, so the staff grips
  exactly like the sword rather than hanging dead-vertical. Other magic focuses (book / wand / orb /
  lute) keep their own natural upright hold at a short grip-height below the hand.
- **The rest pose is authored, never re-sampled from an animated frame** (1im). `WeaponAnimator`
  writes every animated frame as `_baseEuler + accent`, and each phase begins by capturing `_baseEuler`
  from the live transform. If that capture is allowed to run while the *previous* phase's pose is
  still on the weapon, the accent offset becomes the new permanent rest — and because `End`/`StopSway`/
  `AbandonSway` all restore *to* the base, nothing ever unwinds it. The magic weapons were the only
  ones that could drift, and the reason is structural rather than per-weapon: they are the only defs
  with a **rotation** accent (staff 14° roll, holy_book 16° yaw, bone_wand 10° roll, control_orb 30°
  roll; lute is scale-only) — every melee/ranged/shield def is `K_None` or a no-op accent. So the rest
  is now authored once (`SyncRestFromIdle`, only while no phase owns the transform) and restored
  before any capture (`RestoreAuthoredRest`), which also makes a drifted session self-heal on the
  next cast.
- **Defense set (guard)** — holding RMB (block) eases the arms into a held guard pose
  (`PlayGuard` / `EndGuard`, ~0.18 s grab-in) that stays raised while blocking:
  - **Shields** raise the shield face up in front — the cover stance.
  - **One-hand blades** (sword/dagger) tuck a defensive guard before the chest.
  - **Two-handers** (greatsword/warhammer/greataxe/katana/lance) raise the weapon in a two-hand cover.
  - **Fists/gauntlets** hold a boxer guard. The off-hand mirrors automatically.
  - **Magic/ranged** never block (their RMB is charge/draw) and fall back to neutral.
  `CombatController.SetBlocking` raises/drops the guard on the state edge only, and `CanKeepBlocking`
  drops it on sheathe/stow so the guard pose never fights the stow idle; a stamina-break on a
  blocked hit drops it too.
- **Robustness** — `WeaponAnimator` is a **single-owner phase machine** (attack / charge / guard /
  ready-sway; each phase owns the arms exactly once and releases on `End`/`OnDisable`), and the arm
  rest is always the model's local identity. Rapid attack spam, charge-cancel, and guard→attack
  juggling can never leak arm ownership or bake an altered pose into the model; a `PlayerAnimator`
  watchdog force-releases a hung arm-owner claim as a backstop. **1im** extended the same invariant
  from the arms to the weapon transform itself — see *The rest pose is authored* above, which is what
  the magic-weapon drift was made of.

### 3.7 Damage & Status Types

All damage is one of **10 damage types**. Every weapon, spell, and ability declares a **single `DamageType`** (per the single-element rule in §3.6); armor/gear provides resistance per type (equipment-only rule, §3.4). The `DamageCalculator` resolves the attacker's type against the target's resistance.

#### The 10 Damage Types

| # | Type | Description |
|---|------|-------------|
| 1 | **Physical** | Weapon/kinetic damage (blunt, slash, pierce — aggregated as one type). Reduced by Defense/armor. |
| 2 | **Fire** | Heat/burn damage. |
| 3 | **Ice** | Frost/cold damage. |
| 4 | **Lightning** | Electric damage. |
| 5 | **Holy** | Light/divine damage (strong vs undead/dark). |
| 6 | **Dark** | Shadow/void damage (strong vs holy). |
| 7 | **Wind** | Air/force damage. |
| 8 | **Earth** | Stone/ground damage. |
| 9 | **Water** | Water/fluid damage. |
| 10 | **Arcane** | Generic magic/arcane damage — the distinct "magic" damage type. |

*Physical and Arcane are themselves damage types; weapon **categories** (Melee/Ranged/Magic-delivery, §3.6) are a separate dimension — a melee weapon can deal Fire, a staff can deal Ice, etc.*

#### Status Effects (separate dimension)

Status effects are **not damage types** — they are applied **on hit** and do DoT / crowd-control, scaled by **Luck** (`StatusProcLuck`, §3.4):

| Status | Effect |
|--------|--------|
| **Bleed** | Accumulating damage-over-time on repeated hits |
| **Poison** | Damage-over-time over a duration |
| **Rot** | Strong, lingering damage-over-time |
| **Chill** | Cold **build-gauge** (`ChillStatus`): each Ice hit adds 1 cold (2 if the target is **Wet** — water conducts); at **5 cold** it converts into a full **Frost** freeze. The gauge decays on its own; **Fire melts** it instantly (§3.7 fire-vs-ice). The **Ice** signature |
| **Frost** (freeze) | Heavy freeze slow (`ApplySlow` 0.5, ~3.5 s) — delivered by crossing 5 chill stacks, or directly by literal deep-freeze spells |
| **Burn** | Fire damage-over-time + light stagger buildup |
| **Stagger** | Poise break / crowd-control (stun — the **Lightning** signature) |
| **Wet** | Soaked — slight slow (`WetStatus`: ApplySlow 0.85) + **conducts**: Ice/Lightning deal +40% vs a wet target. Applied by **Water** spells. Fog douses fire: applying **Wet instantly puts out an active Burn** (SpellDoT douse). A wet foe also **cannot be ignited** while soaked — water-vs-fire always wins. |
| **Burn** | Fire damage-over-time + light stagger buildup. Gated: **won't catch on a wet target**, and a water hit douses it outright (§3.7). While active it **melts Chill/Frost instantly** (fire-vs-ice). |
| **Blind** | Black fog (`BlindStatus`) engulfs the victim, reducing its field of vision — the **Dark** signature |

Damage-over-time statuses (Bleed/Poison/Rot/Burn) are driven by `SpellDoT.cs` (refreshes on re-apply;
per-tick = spell power × 0.12 over 4 s); Chill/Frost route to `EnemyController.ApplySlow`,
Stagger to `EnemyController.ApplyStun`, Blind to `BlindStatus`, Wet to `WetStatus`.
The player's HUD surfaces every active status as a strip of **colored square chips under the
HP/FP/Stamina bars** (`PlayerBarsHUD`): Burn/DoT with seconds left, Wet, the Chill build-gauge
(n/5), Blind, and the **food/drink stamina-regen modifier** (+X% / −X% with seconds left, from
`ApplyStaminaRegenModifier`). Status components are polled off the player root each frame;
`WetStatus`/`BlindStatus` expose `Remaining` and `PlayerController` exposes
`StaminaBuffRemaining`/`HasStaminaBuff` for the read.
Each magic school's **signature status is applied automatically to every magic attack** of that
element (an explicit per-skill `statusEffect:` overrides the default) — Fire→Burn, Ice→Chill
(deep-freeze spells use the heavier Frost), Lightning→Stagger (stun), Dark→Blind, Water→Wet,
Arcane→**no status** (pure force), Wind→Knockback, Holy→heals (§3.8), Earth→**no status — it
  reshapes terrain itself** (ring / spike / wall / pillar / crater ground deformation on the impact
  point, §3.8; the deep **Meteor** Earth skill strikes the ground and carves a permanent crater
  where it lands, and the deep **Earth Wall** (gated behind Landslide) rears a taller stone ridge
  across the cast).
  **(current build) every damaging Earth spell deforms the ground when it lands** — not just the
  tagged zones: Zone impacts dent (Crater) or rear (Ring/Spikes/Wall/Pillar) at the aim point
  (Boulder Crash, Crash and Tectonic carve craters; Aftershock rears a ring); Storm strikes
  (Rockfall) pit the ground under each boulder; Summons (the golem line) erupt a small raised rock
  field where the construct tears out of the earth; the root Stone Shard projectile carves its
  crater where the shard strikes — never at the caster's footing.
  **Beyond Earth, every magic projectile leaves a small impact dent where it strikes**
  (`SpellEffect.ResolveProjectileImpact` — fireball, frost bolt, arcane bolt, lightning, dark,
  wind blade, water bolt, etc. carve a small Crater under the impact point), so bolts visibly
  disturb the terrain; Earth's craters stay larger and depth-notable (the school's signature) —
  and a crater digs progressively deeper on repeat casts, descending through the
  grass → dirt → stone strata bands revealed in the pit walls (§3.8).
  **The dent's radius is coupled to the low-poly facet step (1i9).** With the facet look on, the
  visible surface is
  emitted from every `LowPolyStep`-th node of the 1 m corner lattice (6 m while 1hx was default; 3 m
  again since 1ia), so a carve
  narrower than half a facet writes its whole shape into nodes no triangle is built from and
  renders as nothing. The 1.4 m projectile dent (1.9 m reach) sat inside the 4.24 m worst-case
  distance to a sampled node, so the universal dent silently stopped existing when 1hx moved the
  step 3 → 6. `DeformAt` now guarantees a rendered mark: when the authored reach contains no
  sampled node it additionally dips the nearest one (and dies one step out), combined with the
  authored influence by `Max` — so the authored radius, depth and per-cast ratchet are unchanged
  and a crater that already reaches a sampled node keeps exactly its shape. Crater only: the
  raised shapes are untouched. **Dormant since 1ia:** the skirt is gated on `EffectiveLowPolyStep`,
  which is `0` while `LowPolyFacets` is off, so with the render algorithm reverted the dent renders
  at full 1 m resolution and needs no skirt at all. The guarantee stays in the source and returns
  with the facet look.
  **Every spell impact plays its spell's OWN impact family (1id), not one shared sphere (1gb).**
  1gb gave every projectile hit a solid sphere growing from a quarter to the spell's radius while
  fading out over ~0.45 s; `SkillFx.ImpactSphere` was deleted in 1ig and
  `SpellImpactFx.Spawn(worldPos, upDir, look, radius, scaleMul)` took its place, choosing one of the
  impact families in §3.8.3 from the spell's resolved look. **The per-tick load is why it had to go:**
  the old path was reached only by Projectile spells, and it allocated a fresh primitive *and a fresh
  `Material`* per impact; 1ih adds on-hit flashes to Zone (77), Beam (11) and Vortex (8) spells that
  had none, so a single blizzard alone is ~2 flashes/s and a screen with several is where one
  primitive per impact stops being affordable. Flashes are pooled per family (cap 96 idle instances
  each) and a per-frame budget of 24 drops the excess rather than queuing it — a queued flash would
  arrive after the event that caused it, which is worse than no flash. The drop counter is on the test
  ground's HUD (F4 lane), because a cap cannot be judged from taste. `SpawnCraterDebris` still only
  fires for tool digs and zone/storm/summon strikes, so a bolt reads as a clean blast, not thrown
  dirt.

### 3.8 Spell-Casting Pipeline

Spells are how the **Magic** weapon category (staff / wand / book) deals damage and casts abilities. The pipeline connects the weapon architecture (§3.6), the skill system (§3.3), the stats (§3.4 Wisdom/Intelligence), and the damage types (§3.7).

#### SpellData (ScriptableObject)

A spell is a data asset carrying:

- id, display name, icon
- `DamageType` (one of the 10 damage types, §3.7) — or **none** for pure utility/heal spells
- base power
- **FP cost**, **cast time**, **cooldown**
- range, area/radius, delivery: projectile / instant / zone / **vortex** (persistent damage-zone that pulls, e.g. the Tornado wind spell) / **beam** / **summon** / **storm** (see §3.8.1 Delivery Behaviors)
- **duration** (zone/vortex lifetime; `> 0` makes the zone **persistent**, ticked by `SpellZone.cs`)
- **selfbuff** (Instant delivery grants a timed caster effect instead of damage/heal — e.g. **Wind Walk**: `PlayerController.BeginFlight(Duration)`, free vertical movement for the buff's seconds)
- **heals** (Holy/utility spells: instant/self-heal, or an ally-heal aura when on a zone; only `IHealable` targets — the player — are ever healed, enemies still take damage)
- **knockback** (impulse applied to enemies; the Wind school signature)
- **summonFallingRock** (sky spells: a rock formation drops from the sky onto the target and the
  burst resolves on landing — which formation is the spell's own resolved `SkyRock` axis, §3.8.4;
  see §3.8.1 Delivery Behaviors "Sky spells")
- **projectile shape** (`ProjectileShape`, §3.8.1): the *visual* built for a Projectile-delivery
  spell. When a spell leaves it `Auto`, the body shape comes from the spell's resolved **display
  shape** (§3.8.3) — picked from its school's shape family by `SpellLook`, deterministically per
  spell so it never changes between casts. Every bolt/lance/blade/spear-named spell sets `Shape`
  explicitly so projectiles read as their name. **`Shape` never writes `spell.Shape`**: it is a
  gameplay flag (homing/large-projectile behaviour), while the drawn body is `SpellLook.DisplayShape`
  — two fields with different jobs, so a spell can be a homing missile and still draw as its family
  demands.
- **look profile** (`SpellLookProfile`, §3.8.3, 1ib): an optional authored override pinning this
  spell's impact family, cast family, body shape, core/edge colour and scale/tempo. 21 spells carry
  one; the other 151 resolve from their school.
- **terrain shape** (Earth school signature, §3.8): an optional `TerrainShape` reshapes the tiled
  heightmap before damage resolves — as **smooth feathered per-corner edits**, never flat blocks.
  **Ring** rears a raised annular wall around the impact, **Spikes** erupts rock spikes beneath it,
  **Wall** rears an elongated ridge across the cast direction (1ga — perpendicular to it, so the
  wall lies left-right in the player's view as a barricade; ~2.6 m on a first cast, tall enough to
  fully block the player's CharacterController), **Pillar** thrusts a tall column up at the center,
and **Crater** excavates a **terraced spherical cap** ringed by a raised, idempotent lip (1ez rim,
   1f3 cap + terraces). Heights are written as continuous per-corner
   elevations (4 corners per 1×1 m TILE, shared with neighbours — which is what keeps the
   triangulated mesh gapless), smoothstep-blended at the rim so a deform reads as genuine terrain;
  `ChunkMeshGenerator` only emits slab side-wall bands for *legacy saved flat tiles*, so smooth
  deforms build no artificial walls. Ground deform runs via `TerrainDeformer` → `WorldStreamer
  .DeformAt`, which writes the corner heights, rebuilds the affected region of the merged chunk
  mesh + collider, and persists the edit as a terrain modification (§2.6 saves them per chunk).
  Raised shapes (Ring/Spikes/Wall/Pillar) skip tiles inside a small keep-out ring (~0.9 m) around
  the player's feet so the ground never grows directly under the capsule and violently depenetrates
  it on the next physics step. Raised shapes are **bounded and idempotent**: their cap (raised
  shapes: original noise height + lift) is sampled **per-corner at each corner's own world coords**,
  so every corner keeps its own natural slope and no tile collapses to a uniform level — crests are
  smooth rounded ridges, never flat plateaus, and a repeat cast can never stack a ridge higher than
  the intended release (a repeat Wall stays ~2.6 m, never taller). In addition to being
  height-capped, **the WIDTH of every terrain shape is bounded to the spell's authored delivery
  dish — `spell.Radius` (crater: `spell.Radius * 0.5`, ~2 m for Earth Meteor), never the blast
  splash and never the (unbounded) hold-to-overcharge `sizeScale`** (1cw). Charge still enlarges the
  *damage* splash and the zone/ring visuals, but never the ground edit — so a charged Tremor,
  Spire Field, Earth Wall or Landslide rears only its own ~2.6–6 m dish, not a whole chunk.
  ***Crater rears the terrain's strata as a signature**: unlike the capped raises, an excavation
  is **WIDTH-bounded but DEPTH-unbounded** (1cv). The crater's **width** is always the spell's small
  local delivery dish (`DeliveryRadius * 0.5`, ~2 m for Earth Meteor) — never the full blast splash —
  so one cast carves a bounded local bowl in the ground and never reads as "the whole chunk / the
whole terrain moved." Its **depth** ratchets **a `CraterStep` (~1.1 m at full influence) deeper per
   cast or tool swing, with NO floor cap of its own** — repeated craters dig progressively deeper pits,
   with no limit (the player's "i want no limit on my game"); the only global backstop is
   WorldStreamer's ±200 m mesh-safety sanity band. Since **1f3** that step is applied to a
   **quantised** offset, so a cast moves the floor by `CraterStep` within ± half a terrace rather than
   exactly (see §2.2's crater lane); the ratchet itself is unchanged. Vertex colors painted at build time then reveal the dug depth
   below the pristine noise surface as discrete strata bands: **grass (surface) → dirt (~0.65–2.3 m
   down) → stone (≥ 2.7 m down)**, small blends between bands (1cs). The shovel can only dig the
   soft bands and stops at stone; the pickaxe excavates at any depth. Every crater is a genuine
   **terraced cap** — a sphere pressed into the ground, its interior quantised into level treads —
   ringed by a raised lip (1ez): the floor ratchets down per cast while the lip is
   idempotent and capped, and corners keep their own slope throughout, so no tile collapses to a slab.
   Every crater is permanent (1cs).
  **Excavation ejects debris matching the stratum it just reached (1de):** `WorldStreamer
  .SpawnCraterDebris` pops 3–5 physical cubes out of the fresh dent — dirt blocks (dirt-brown) while
  the floor digs through grass/dirt, rock (grey, the same look as pickaxe rock destruction,
  `WorldBuilder.SpawnRockDebris`) once the pit reaches the stone band — tinted by the same
  `TerrainBandColor` the pit walls render and destroyed after ~2.5 s so repeated digs never litter.
  Only a Crater throws debris; the raised shapes never do. **Projectile impacts suppress this cube
  burst (1gb)** — they carve the same dent but pass `emitDebris:false` and play their spell's own
  impact family from the pooled `SpellImpactFx` dispatcher instead (§3.7 "Every spell impact"); only
  tool digs and zone/storm/summon strikes still eject the cubes.

  **Projectile impacts throw their own grey rock chips (1jh), additively to that sphere.** 1gb's
  reason for suppressing the excavation burst was never "no debris at impacts" — it was that the burst
  reads as *three objects floating up then disappear*, which is a complaint about **shape and motion**.
  So 1jh adds a separate emitter, `WorldStreamer.SpawnImpactRockDebris`, rather than un-suppressing the
  old one, and the dent is byte-for-byte the same shape either way. Four differences from the excavation
  burst, each answering one part of that critique:
  - **Thrown forward, not up** — biased along the projectile's own travel direction, flattened so pitch
    cannot turn a downward strike into a skyward one, with only ~0.4–1.8 m/s of lift (the old burst gave
    every chunk +2.5…+5 m/s of vertical velocity, which is the "floating up").
  - **Short life — 1.4 s vs 2.5 s.** These chunks are collider-free, so they fall *through* whatever
    they were knocked out of; a long life spends most of it sinking out of sight rather than reading.
  - **Grey rock, not stratum colour** — `Color.Lerp(Color.gray, Color.black, …)`, the same material as
    the world's breakable-rock debris, because a spell shattering masonry is not a shovel full of dirt.
  - **Count and size scale with the impact radius** (2–8 chunks, size ×`clamp(radius·0.75, 0.7, 1.6)`), so
    a charged bolt throws more and throws bigger instead of every impact looking identical.

  Reached through `TerrainDeformer.ImpactRockDebris` — the same static-entry-point shape as
  `TerrainDeformer.Apply`, so the spell layer never resolves the `WorldStreamer` itself — and spawned at
  the **hit point** rather than the probed ground point, so a wall strike throws chips too. The sphere
  and the chips are both kept on purpose: the sphere is the *spell's identity* (per-school impact family,
  §3.7) and the chips are the *world reacting*, so they answer different questions.
  A Crater-shaped projectile (the root Stone Shard) carves its crater where the rock **strikes** —
  `SpellEffect.ResolveProjectileImpact` down-probes the ground at impact and deforms it there — so a
  cast never dents the caster's own feet; the pit is permanent. **Every non-Earth magic projectile
  (fire/ice/arcane/lightning/dark/wind/water) also leaves a small uniform impact dent** (a fixed
  ~1.4 m Crater where the bolt strikes) through the same path, so any bolt visibly disturbs the
  terrain — Earth retains the bigger, spell-scaled craters and the raised shapes
  (Ring/Spikes/Wall/Pillar) as its signature. All edits survive forever. Earth spells use terrain
  shapes instead of a status effect. Legacy **flat tiles saved by older builds are
  re-smoothed toward their noise corner heights when their chunk loads** (1cl): an in-memory
  relaxation — the file keeps the plateau until the player next deforms that tile, then the smooth
  values persist naturally. This covers BOTH legacy whole-metre slabs AND fractional flat plateaus
  that older carves produced (clamped crater floors / shape caps), so a carve from any build reads
  as smooth terrain, never a torn-out tile. Relaxation is deterministic, so reopening yields the
  same smooth shapes — the map never "re-randomizes". Current Earth shapes are never flat to begin
  with (per-corner caps), so they are never re-smoothed. The merged chunk mesh is **hole-proof and
  patch-safe**: any tile whose
  bookkeeping is momentarily missing (unload/reload races) is filled with the same deterministic
  noise corners, so a chunk rebuild can never drop a quad and open a fall-through; and the merged
  buffer keeps all top quads first and wall bands last, so `PatchRegion`'s fixed quad offsets can
  never write into a wall slot and corrupt a tile; and a deliberate
  clean map is available as an opt-in `EnableResetTerrainSaves` QA lane on `NewWorldTestGround`
  (deletes this seed's `tc_*.dat` chunk saves and regenerates the loaded chunks from noise).
  **(current build) every damaging Earth spell carries a terrain shape, regardless of delivery:**
  Zone impacts (Boulder Crash, Crash, Tectonic → Crater; Aftershock, the tremor ring family, Spire
  Field etc. → Ring/Spikes/Pillar/Wall; the deep Earth Wall, gated behind Landslide → Wall, rears a
  taller ridge across the cast) deform at the aim point via `ResolveZone`; Storm strikes
  (Rockfall → Crater) dent under each boulder via `SpellStorm.DeformGround`; Summons (the golem
  line → Spikes) erupt a small rock field where the construct rises via `ResolveSummon`; the
  Projectile root (Stone Shard) carves its crater at the impact point. Every raise is capped and
  idempotent — including the crater's own 1ez lip — so no shape, zone, storm, summon, or projectile,
  can ever stack unbounded, while crater floors dig as deep as the player has patience for.
- cast animation reference
- optional status-effect application with a proc chance (e.g., applies Burn/Frost/Stagger; §3.7)

**Deforms are idempotent for raised shapes (`1cm`) but craters ratchet down (`1cs`):** the per-corner
raised-shape target is an absolute profile — raised shapes aim
at (original noise height + blended lift) — and the edit applies `Max`
against the current height, so a repeat cast at the same spot reproduces
the exact same shape and changes nothing. (Earlier, the raise added `s·lift` to the *current* height
every cast, so the second+ cast kept lifting the whole influence footprint toward the cap — the
ground visibly rose across the chunk, reported as "the entire chunk moving"; craters grinded deeper
the same way.) `Max` also means a raised deform can never *lower* terrain that already sits above the
target. A **Crater**'s DISH is the deliberate inverse (see the terrain-shape bullet): each cast/swing
lowers the floor one `CraterStep` below its current height, so excavation is bounded only by the
mesh-safety sanity band. The crater's raised **lip** (1ez), by contrast, follows the raised-shape rule
exactly — it targets pristine noise + a bounded offset and is `Max`'d against the current floor — so
the lip is idempotent and can never stack higher. Two robustness fixes ride along: `SpellCaster`'s zone aim probe skips **raised terrain taller than
pristine noise** (a wall the spell itself reared) so a repeat cast targets the ground the player is
looking at rather than the wall face, and `ChunkObject` re-points the mesh filter/collider at the new
mesh **before** destroying the old one (no frame ever references a destroyed mesh).

Persistent zones are handled by the unified **`SpellZone`** (tick damage scaled by a per-delivery
multiplier — Zone ×0.4, Vortex ×1.0 — optional pull, plus Holy ally-healing of `IHealable` inside
per tick); it replaces the former one-off `WindVortex`. The **Tornado** wind spell is Vortex's one
exception: `SpellCaster` routes it to **`SpellTornado`**, which rebuilds the old environmental
tornado model + function (`MapBuilder.BuildTornado` → `TornadoBehavior`: a tall drifting funnel of
rectangular debris blocks that churns around the axis, tows caught rigidbodies into the axis first,
then carries them on a low orbit — creatures implement `ITornadoCarried` so their own controller
yields while carried; scaled down to the spell radius) and layers the same damage ticks + enemy
pull on top; all other Vortex spells keep the `SpellZone` funnel.

#### §3.8.1 Delivery Behaviors

Beyond the core projectile / instant / zone / vortex, spells use three richer deliveries so spells in
a school read distinctly instead of feeling like copies:

- **Beam** — a channeled ray from the caster to the aim point. The cast **fires on the normal cast
  release** (LMB-up at the frozen charge level, §3.8); **holding LMB keeps the beam on while
  `ChannelDrainPerSecond` FP drains in real time** (via `TrySpendFocus` — a rejected spend ends the
  beam). Each `TickInterval` (default 0.5 s) it ticks damage (or healing for `heals` spells) to
  everything inside the beam capsule caster→aim. A short release-grace (~0.4 s) lets a sloppy release
  keep the ray a moment; on mobile / no-mouse the beam auto-sustains ~1.6 s. The beam **sweeps with the
  caster's aim while channeling** — `SpellBeam` re-derives its `Direction` every frame from the camera
  aim (caster-forward fallback, mirroring `SpellCaster.Execute`), so turning sweeps the ray and its tick
  capsule across the field. While channeling, LMB is consumed by the sustain (`IsChanneling` guard) so
  the beam can't be re-cast or switched to melee.
  Examples: Searing Ray, Arc Storm, Beacon (heal), Hunger, Cold Stare, Storm Breath.
- **Summon** — ground-targeted (shows the AoE preview ring). **Damage** summons are persistent
  **turrets** that repeatedly fire bolts at the nearest enemy (`BoltPowerMultiplier` ×0.6, reusing the
  projectile flight); **`heals`** summons are standing **heal auras** mending `IHealable` allies inside
  (enemies still take damage). Examples: Frost Obelisk, Shadow Totem, Arcane Rune, Healing Shrine,
  Ember Effigy, Gust Totem.
- **Storm** — a persistent ground zone that **strikes repeatedly** while it lasts: `StrikesPerTick`
  (2) bolts per tick at `StrikePowerMultiplier` ×0.8 with randomized sub-second delays, element-styled
  visuals (e.g. crossed bolt bars on Lightning). Examples: Thunderstorm, Meteor Rain, Blizzard,
  Hail Lance, Avalanche, Eclipse.

**Ground placement is unbounded.** The four ground deliveries (Zone / Vortex / Summon / Storm) aim
where the camera actually points — out to a practical `SpellCaster.GroundAimMax` (1200 units) cap,
not the spell's own `Range` — so AoE magic can be cast anywhere in the open world (e.g. dropping a
zone on a distant ridge or a summoned turret near a far road). The landing preview
(`PlayerController.TryAoeTarget`) mirrors the same compute. Projectile / instant / beam deliveries
keep their spell `Range` cap, so only ground placement is unbounded.
- **Projectile spells fly straight along the look direction (1jm).** A `Projectile` no longer converges
  on a point `Range` metres in front of the camera. It leaves the cast origin along
  `SpellCaster.StraightFlightDirection(cam, lookPivot, fallback)` — the **look pivot's** forward, taken as a direction (1jm read the camera's forward; 1jo moved the source onto the pivot, see below).
  The old point-aim's error is the vector from the hand to the *camera* scaled by `1/Range`, so it grew
  with how far behind the camera sat: negligible in first person (the camera is at the pivot), roughly
  `atan(6.5 / Range)` in third person — the reported "weird trajectory" — and 1jl's shoulder offset added
  a lateral term on top. Taking a direction deletes the term and makes the shot independent of where the
    camera *is*, which is what keeps 1jl from introducing a skew of its own.
    **1jo: the aim reads the look PIVOT, not the camera.** The two hold the same direction (first person
    copies the pivot's rotation outright; third person looks from a point offset along `-pivot.forward`
    back to a target offset along `pivot.right`, so the lateral terms cancel), but they are written at
    different TIMES: `HandleMouseLook` sets the pivot inside `Update`, while `CameraModeSwitch` writes the
    camera transform in `LateUpdate`. The aim preview is built in `Update`, so reading the camera gave it
    **last frame's** aim and the ray trailed the crosshair by a frame whenever the camera moved - the
    reported endlag. The pivot is the look source and is current-frame, so reading it removes the lag
    without changing the direction. The camera is kept only as a fallback for a caster that has one but no
    player pivot. Both the caster and the preview call the same helper, so the ray still mirrors the shot.
  `SpellEffect`/`FireProjectile` consume that vector directly (`pos += fwd * 0.5f`,
  `Quaternion.LookRotation(fwd)`), so the fix needs no change downstream, and the aiming preview
  (`UpdatePathPreview`) now **calls** the same helper rather than re-deriving the formula — it needs no
  camera reference at all any more.
  **The cast origin's own forward is not used**: the origin is the magic hand on the weapon rig, and the
  body's rotation is yaw-only (`HandleMouseLook` writes `Euler(0, _yaw, 0)`, pitch lives on the camera
  pivot), so `origin.forward` is a flat horizontal shot that cannot aim up or down.
  Instant / beam / the four ground deliveries deliberately keep the point-aim — instant and beam carry
  the identical skew and are **reported, not changed**, because the request was scoped to projectiles.
  Every `SpellCaster` in the tree is the player's (`WeaponRigBuilder` adds it to `playerRoot`; everything
  else is `GetComponent<SpellCaster>()` on the player or the QA bench), so aiming from `Camera.main` is
  player-scoped in practice — a property this rule now depends on, which is why it is written down.

**Sky spells** (`SummonFallingRock`, the meteor/boulder family) drop a **rock formation** from high
above the ground target and read as the spell landing: the burst (damage, knockback, terrain
deform) is deferred until it hits the ground (Zone deliveries ~0.6-0.8 s drop; Storm strikes drop a
smaller formation per strike and fire their flash/damage/deform on landing). Built by
`SkillFx.FallRock` — a collider-less visual (never triggers the knockback-terrain-root bug 1cx),
self-destroying, shards + ring flash on impact, and tinted with the **spell's own resolved core
colour** (1ij) rather than the Earth school colour, so Fire and Earth rocks read differently.

**Which formation** is a per-spell look axis, `SkyRockStyle` (§3.8.4), resolved through
`SpellLookProfile.SkyRock` like every other visual axis. `scale` stays the spell's **blast radius** in
both cases — the axis decides only how that radius is spent:

| Style | Body | Spells |
| --- | --- | --- |
| **Boulder** | one ragged rock, core + 4 off-angle ridges (1cy, unchanged) | Fire Meteor, Meteor Rain, Earth Meteor, Rockfall |
| **Swarm** | 1f7: a smaller lead rock on the aim point + a flat fan of six around it, covering the blast radius instead of the middle of it | **removed in 1ir** — its last user, Fire Asteroid, became Continuous Fireball (a following familiar that casts no rock). The enum value, the `SkillFx.BuildRockSwarm` body and the `BuildRockBody` branch are all gone, leaving `SkyRockStyle` as `Inherit`/`Boulder` only. Nothing in a save can hold this value (looks are authored in `SkillCatalog`, never serialized — `SaveManager` stores only `learnedSkills` and `skillLevelsJson`), so there was no migration argument for reserving the slot; and the F4 identity key packs the raw enum value at bits 24+, so deleting an **unused** value cannot split or merge any group and its `172 / 172 / 0` verdict stands. |

The Swarm's spread was deliberately **flat (X/Z only)**: `RockDrop` lands the whole formation by
snapping the root to one ground height, so vertical scatter would have left the outer rocks floating
or sunk on a slope — seven chances to see it, against the boulder's one core.

**Spells:** Fire Meteor, Earth Meteor, Meteor Rain, Rockfall — four. (Fire Comet left this family
in 1f7; Fire Asteroid left it in 1ir, when its slot became Continuous Fireball.)

Fifth, **Projectile Shapes** — projectile visuals are split into named shapes rather than one element
color swap, so each spell looks like its name and not a recolor of the same ball. Since `1ec` every
body is a **voxel cube-cluster**: a front-leading cube in the school color with progressively
**smaller, darker cubes stacked behind it** (a bright hot core fading into a tapering square tail),
built once and fully static (no sphere meshes remain on projectiles):

| Shape | Rendered as |
|---|---|
| **Bolt** | Jagged 8-segment cube chain along the flight axis (already a cube chain tapering 0.17→0.05, the same segment technique as the thunder-storm event's `SpawnJaggedBolt`) — used by every spell with "Bolt" in the name: Frost Bolt, Chain Lightning, Dark Bolt, Volt, Fork/Leap/Arc/Volt Bolt, Fury Bolt, Shadow/Doom Bolt, Void Rend, and the class-flavored Arcane Bolt. |
| **Sphere** | Hot voxel orb: a 0.24 lead cube + 4 jittered cubes shrinking to ~0.05 behind it, each darker — the Fireball and every generic orb. (Scorch/Burn/Frost Bite use the Comet shape instead.) |
| **Shard** | Translucent glass lead chip (45° diamond) + 2 smaller, dimmer glass chips trailing — frost chips (the Ice school default; Chill Touch). |
| **Debris** | Clustered grey rock cubes (mixed sizes, random rotations, one leading chunk) — the Earth school's Stone Shard. Dressed like the world's breakable-rock debris (`Color.Lerp(gray, black, rand)` cubes) with two chunks dusted in the earthy tan accent so it reads as magic; at impact the crater plays the spell's own impact family from the pooled `SpellImpactFx` dispatcher (1id) rather than the *excavation* cube burst (that one is stratum-tinted dirt thrown upward — 1gb), and 1jh adds grey rock chips thrown forward from the hit point on top of it; the embedded model cubes only remain as the pickaxe/mining look (1de). |
| **Lance** | Long straight pointed spike (shaft + tip) with two small trailing flecks behind its tail — Ice Lance, Frost Pierce, Glacial Impale. |
| **Spear** | Tapered spear: dark shaft + broad diamond head + trailing flecks behind — Shadow Spear. |
| **Blade** | Flat translucent cross-blade (alpha ~0.4 so wind reads as a ghost of air) + two small ghost cubes trailing — Wind Blade, Razor Blade, Wind Scissor, Laceration. |
| **Splash** | Water drop cube + a trailing splash of 3 smaller, darker cube drops — Water Bolt, Tidal Surge. |
| **Comet** | Small voxel core cluster + a fading streak tail cube — Scorch, Burn, Frost Bite. The meteor-line **Comet** no longer wears this shape (1f7): it became **Ember Streak**, and in 1ir that whole spell was replaced by Flamethrower. The `rockBody` boulder variant of this shape survives for a future sky-rock projectile that wants it. |
| **Ember Streak** | 1f7, **removed in 1ir**: a stretched bright head (0.18×0.18×0.42) with a long tapering ember tail, read-only via `SpellLookProfile.DisplayShape`, never `spell.Shape`. It existed for exactly one spell — the meteor-line Comet — and that spell's slot is now Flamethrower, which is a swept Beam with no projectile body to shape. Flamethrower does **not** reuse the tail: it would have to survive in a delivery that draws nothing but a cone. |
| **Missile** | Three 2-cube mini dart-stacks; **homing** — `SpellEffect.UpdateMissileTargeting` probes the **current trajectory** every frame and prioritizes the target on the flight path (the foe it is about to fly into), otherwise keeps chasing the locked target's last spot (or locks the nearest foe ahead if never locked), steering smoothly at 240°/s so the flight bends; no target = flies straight. Arcane Missiles, Chill Soul. |
| **Dart** | Sleek thin bolt-line with a tip + small trailing fleck — physical shots (Archer Wind Shot, Taoist Talisman). |

`Auto` picks from the spell's **school family** via `SpellLook.Resolve` (§3.8.3): Fire→Sphere,
Ice→Shard, Lightning→Bolt, Wind→Blade, Water→Splash, Earth→Debris, Physical→Dart, everything
else→Sphere. Builders live in `MagicProjectileModelBuilder.BuildProjectileBody`
(cube primitives only, via the `Cluster` / `AddTrailingFlecks` helpers), colored per damage type;
**since `1eb` the body is fully static — no exhaust particles and no in-flight pulse** (the old `OrbFx`
scale-pulse/spin modes and the `AttachProjectileParticles` exhaust `ParticleSystem` were removed): the body
is one static shape that only moves. What a *flying* projectile additionally costs is **1jq's world-space
trail strip** (below) plus its `SpellEffect`; turret summons render the projectile through the same call
(`SpellSummon` passes the turret spell's shape). Translucency (Wind/Ice) is set via
`material.color.a` and relies on the `"Sprites/Default"` shader blending (the `"Unlit/Color"`
fallback would render opaque).

**In-flight trail strip (1jq, replacing 1jg's voxel trail):** every flying projectile leaves a short
exhaust behind it, as **one camera-facing quad strip** — one GameObject, one `MeshRenderer`, one draw
call, one pre-sized vertex buffer rewritten in place. `TrailStrip.Push` is driven from
`SpellEffect.Update` and gated on **distance travelled** (one point per `Step` = 0.3 m, never per
frame, so a fast bolt and a slow one lay the same spacing instead of one drawing a dotted line and
the other a solid ribbon). Each point's cross-section is offset perpendicular to both the local tangent
and the camera forward, and the tangent is the **average of the adjacent segments** so the shared
boundary vertices of neighbouring quads get the same offset and the strip cannot tear at a joint. The
strip is **0.10 m wide at the head and 0 at the oldest point**, and its alpha ramps from 0 at the
tail to the spell colour's alpha at the head, so it reads as exhaust. `Life` = 0.35 s and `Step` =
0.3 m are **carried over unchanged** from 1jg, and `MaxPoints` = 29 is *derived* from them rather
than chosen: the fastest authored magic projectile is 22 m/s, so `22 × 0.35 / 0.3 = 25.7` segments
live at once, and 29 gives headroom. When it fills, the **oldest** point is dropped — the tail
shortens, the arrays never grow and nothing is allocated per frame.

**What this replaced, and why it was worth replacing.** 1jg emitted one pooled cube per `Step`, each
living `Life` seconds, which at the default 20 m/s is **~23 live cubes per flying projectile** — 23
GameObjects, 23 `MeshRenderer`s (so 23 draws) and 23 `MonoBehaviour`s ticking `Update` every frame.
The pooling did not actually make that cheap: `ObjectPooler.Return(go, delay)` does
`AddComponent<ReturnTimer>()` on **every** emit, and `ReturnTimer.Update` then calls
`Destroy(gameObject)` when it expires, so each voxel cost a fresh native component create plus a
deferred destroy per life. The strip replaces all of that with 1 GameObject, 1 renderer, 1 `Update`
and no per-frame allocation.

Two properties that changed shape rather than merely persisting:

- **The gradient is now authored, not borrowed.** 1jg got its taper for free from voxels expiring —
  "age, not alpha". A single strip cannot borrow that, so the width ramp and the alpha ramp are both
  explicit (`TailHalfWidth`/`HeadHalfWidth` and a per-vertex alpha from 0 at the oldest point).
- **Nothing needs a hand-off at impact.** The strip is world-space and **unparented**, exactly as the
  voxels were, so `SpellEffect`'s `Destroy(gameObject)` on impact needs no work at all — the strip is
  not a child, it keeps lying where the bolt *was*, and it destroys itself when its last point ages
  out. The visible cost of that is that the last ≤`Step` of approach is not drawn; the bolt's own
  body covers it.
- **A missing camera is handled, not crashed on.** `Update` re-reads `Camera.main` while it is null
  and, if there is still none, keeps the last good shape for a frame rather than building the strip
  from a fabricated forward vector — a strip frozen for a frame is invisible, and a strip built from
  a guessed forward is wrong on screen. A sighting straight down the strip makes tangent and camera
  forward parallel and the cross product degenerate; that reuses the previous frame's side vector
  before falling back to world-up, which is why the side vector is carried on the component.

Colour is `SpellLook.Edge`, the two-tone member documented for "rim, trails, shards", already
resolved once on the projectile by `SpellEffect.Initialize`, so the trail adds no second colour lookup
and derives nothing. It travels in the **mesh's vertex colours**, not in a material, which is why a
single shared material serves every strip of every school: the strips differ per cast, the material
does not. Strip colliders are absent **by design** — the Numpad8 lane counts them as its known-zero
control, precisely because a trail that acquired a collider would cost physics it never needs.

Two placement rules, which are why the trail is **not** in the model builder, and which 1jq kept
unchanged: `MagicProjectileModelBuilder` is a one-shot shape factory with no per-frame behaviour, and
it is shared by the **static** model bench (`NewWorldTestGround`'s spell band draws every castable
spell as a motionless pedestal) and by `SpellCaster.DecorateProjectile` (a turret's bolt, which never
flies) — a trail emitted there would hang a strip in mid-air on a pedestal that never moves. So
emission lives in the flight loop, which only runs while a projectile is genuinely travelling: a Zone
resolves and destroys itself in `Launch` *before* `_launched` is set, so it never trails, and neither
the bench nor a turret bolt ever reaches `Update`.

**Acceptance readout (1jq) — bench key Numpad8.** The proposal behind this rewrite was entirely about
cost, so the lane that can judge it counts **components and geometry**, not milliseconds:
`NewWorldTestGround.EnableTrailAudit` + `SnapshotTrailAudit` reports live strips, how many were
actually **rendered** (`Renderer.isVisible`, which is the difference between "exists" and "drawn"),
per-strip and total segments / vertices / triangles, and prints `MaxPoints`-derived caps beside them.
Two deliberate details:

- **The verdict is scoped to where a positive result is *possible*.** A bolt's first `Step` of flight
  has a strip with **no segments** and therefore nothing to draw, so the lane splits not-drawn strips
  into `young (no geometry yet)` and `undrawn WITH geometry`, and only the latter can read `FAULT`.
  Counting the young ones as faults would have buried the one real signal.
- **The collider count is a known-zero control**, zero by design, so a run reporting strips-with-colliders
  > 0 proves the count is lying. A trail that acquired a Collider would cost physics it never needs.

It is read-only (rule 7): it counts what is in the scene on the frame the key was pressed, and spawns
and changes nothing. What it **cannot** report is whether the strip sits flush with the ground or how
the taper reads on screen — "drawn" and "flush" are separate properties, so the look is a play-test
item and the numbers are not a substitute for it. Numpad8 was verified free across all three Input
System spellings before use, and `tools\StaticChecks.ps1` check 8 enforces the no-double-binding half
mechanically; 1jq also corrected the crater lane's stale "Numpad0-9 are free" tooltip, which had
stopped being true when 1je took Numpad1.

Do not confuse this with the **model's** "trailing flecks" (`AddTrailingFlecks`, and the flecks named in
the table rows above): those are parented to the bolt body and travel *with* it, so they are part of the
silhouette and they show up on the static bench. The 1jq trail is left behind in the world and shows up
only in flight.

#### Casting Flow

1. Player equips a **Magic weapon** (staff/wand/book) in a hand slot.
2. The weapon's magic mods — `MagicDamageMult`, `CastTimeMod`, `CooldownMod` — modulate the spell before resolution.
3. `MagicWeaponBehavior.BeginAttack` routes the cast to `SpellCaster`.
4. `SpellCaster` validates **FP** (`MaxFP` from Intelligence) and **cooldown**; if valid, begins the **cast time**.
5. On cast completion, a `SpellEffect` spawns (projectile / instant / zone) or a persistent **zone/vortex** is summoned (`SpellZone` — e.g. Tornado/Blizzard, or a holy healing aura).
6. `DamageCalculator` resolves the spell with its `DamageType` against the target's equipment resistance; **Wisdom** scales spell power (`MagicAtkPower`), and `CooldownMult` from Intelligence shortens reuse.

#### Healing

**Holy** spells (and a few utility spells) instead carry `heals`. An **Instant** heal is applied to the
caster via `SpellCaster.ResolveDirect`; a **Zone** heal both damages enemies in radius and mends any
`IHealable` ally inside (the player), and a persistent zone heals on each tick. Healing scales with the
same Wisdom-derived spell power; only `IHealable` targets are ever healed — enemies are never healed.

#### Charging & Casting Circle

- Arming a spell from the **Alt magic grid** (§5.16) then hold **LMB** to start the aim pose (hands raise); **RMB**
  builds a **charge level** (0–100%, ~2 s, no auto-fire). **Releasing LMB** fires at the frozen level.
  Charge scales the cast: FP cost (up to ×1.6), damage (up to ×2.0), and AoE radius (up to ×1.8), so
  a deeper charge is always a gamble for more FP — never a dud.
- While aiming/charging, the held **magic weapon shows a "casting circle" halo** tinted by the armed
  spell's **resolved look** (§3.8.3). It is no longer one ring: the halo picks one of **seven cast
  families** from the spell's look, and the *parts* that family enables differ — Disc / outer Halo /
  spinning Rune (8 radial tick marks) / HexRing / inner segments. So the same Wind spell and the same
  Earth spell no longer wear the same halo, and the family is visible before the cast resolves. Radius,
  brightness and spin still ramp with charge level; `Scale`/`Tempo` from the look scale it.
  Releasing the cast pops a one-shot expanding ring **in the spell's own colour and size**. (`CastingCircle.cs`,
  driven by `PlayerController`; split aim → charge → release is used by both magic and ranged.)
  **Unarmed / no-spell-armed casts still play a plain white hand glow** — a weapon release with no
  spell behind it keeps the neutral colour rather than borrowing a school's, because a white burst
  reads as "released the weapon" and an Arcane-pink one would read as "cast an Arcane spell".
- Projectile spells launch **from the casting circle's center**: the spawn point sits on the aim line
  at the rig/hand origin (a small forward muzzle offset only, no vertical lift), so the flight
  trajectory passes through the circle's heart. The pre-cast **path preview** mirrors the exact launch
  (`SpellCaster.FireProjectile` ↔ `PlayerController.UpdatePathPreview` share the same origin math).
- Zone/vortex spells additionally show a **ground AoE preview** ring that also grows with charge,
  tinted by the spell's resolved core colour (1ij), matching the projectile **ray** colour (1iq turned
  that shared charge readout from a cone into a ray) so a charge reads in one colour from aim to release.
- Every projectile delivery (magic **projectile** spells, and ranged draws — regular and per-hand dual)
  shows a **flight-path ray** while charging: a thin centre line from the hand along the aim line, clipped
  at the first solid hit. **Magic previews are tinted by the spell's resolved core colour** (1ij —
  per-spell, so two spells of one school aim visibly differently); ranged previews are tinted by shot
  type. (`ProjectilePathPreview.cs`, driven by `PlayerController`; hidden on cancel/release/weapon
  switch.) Ranged weapons with no projectile prefab fire a runtime-generated arrow instead of a
  hit-scan tracer.
- **1iq: the spread CONE is the drawn flight's alone — only the bow draws it.** A cone claims "your aim
  is this wide, and holding narrows it", which is a claim about *accuracy*, so it is drawn only by the
  one weapon whose accuracy is drawn-dependent. Every other projectile delivery gets the ray alone.
  The gate is `AmmoItemId != null` (arrows today, i.e. exactly the longbow) rather than a hardcoded
  `"longbow"`, so a future crossbow inherits the cone without a second edit. Two consequences:
  - **Projectile magic gets no cone because magic has no spread at all** — nothing in the spell path
    ever offsets the fire direction, so the old `8° × (1 − charge)` fan was drawing outcomes the game
    does not have. (Its reach is untouched: the preview still mirrors `SpellEffect`'s flight envelope
    and is deliberately *not* charge-scaled.)
  - **The throwing hammer gets no cone because it is thrown, not drawn** — it keeps the ray.
  - Mechanically this is `spreadDeg = 0`, which collapses every ring to zero width and leaves the ray at
    full opacity, so "cone" and "trajectory" became two independent claims a caller can ask for
    separately. Note the cone is an **intent** readout, not a guarantee: nothing in
    `ProjectilePathPreview` feeds the spread the projectile is actually fired with.
- **Known defect, reported by 1iq and NOT fixed by it:** the bow's cone narrows with charge, but
  `RangedWeaponBehavior.BeginAttack` applies the same Dexterity spread at *every* charge level —
  `charge` is not in that expression. And `AccuracyFromDex` is `0` on both ranged weapons (nothing in
  `WeaponCatalog.Make()` sets it), so `RangedAccuracy = 1 + Dex × 0 = 1` and the cone both *starts* at
  maximum spread and *narrows to zero* while the fired shot never tightens. The cone is therefore a
  false promise for the bow too — the same class of lie as rule 7's "drawn and flush are separate
  properties", one layer out. Making the claim true (feed `charge` into `ApplySpread`, and give the
  longbow a non-zero `AccuracyFromDex`) is a **gameplay** change and is left as a decision, not done
  silently.

#### 3.8.3 Per-Spell Visual Identity — `SpellLook` (1ib)

**One rule, one place.** A spell's on-screen identity is resolved by `SpellLook.Resolve` and by
nothing else. There is no second spelling of "what colour is this spell" or "what shape does it draw"
anywhere in the codebase — that is the rule-12 "second spelling that rots" failure, and this codebase
had already shipped two drifting `DamageType` palettes. `DamageNumber.ColorFor(Type)` still exists
but **delegates** to `SpellLook.SchoolColor`.

**Precedence is exactly three steps** (1ib):

1. **Authored `SpellLookProfile`** on the `SpellData` (`look:`) — **25 spells** carry one (23 after
   1f7 added Comet and Asteroid, whose slots 1ir replaced). Both replacements are authored rather than
   left on a deterministic pick, for two separate reasons: Flamethrower is the only spell whose
   delivery draws a **cone** (a swept wedge opens outward from the caster, and nothing in the Fire
   family looks like that by accident), and Continuous Fireball is a steady 44-bolt stream where a
   deterministic per-cast jitter would read as a different spell. An axis only takes an authored
   profile when it is a *statement about the spell* — 1f7's rule.
2. **School family** with a per-spell deterministic pick from that family's member list — this is what
   makes 151 spells differ without 151 hand-authored profiles.
3. **`SpellLook.Resolve(DamageType, ProjectileShape)`** — the named identity-less fallback for callers
   that genuinely have no spell (a non-spell turret bolt, the magic-model bench). It is deliberately
   *not* a fourth precedence step: it is what you get by falling off the end of the rule on purpose.

**What one look carries:** `Impact` family, `Cast` family, `DisplayShape` (the body actually drawn),
`SkyRock` (the falling formation, §3.8.4), `Core` + `Edge` colours, `Scale`, `Tempo`.

- **Impact families** (`SpellImpactStyle`, 1id) drive `SpellImpactFx`'s pooled flash: Burst, Ring,
  Sphere, Cross, Shards, Bloom, Pillar. `Inherit` means "no authored opinion — take the school
  family's pick", and `SpellImpactFx.Spawn` returns immediately on it. As of **1jb** the *shapes* are
  `MagicImpactModelBuilder`'s and the *pooled lifetime* is `SpellImpactFx`'s; the family list and the
  look are unchanged, so nothing about a spell's impact appearance moved in this split.
- **Cast families** (`SpellCastStyle`, 1if) drive which parts the `CastingCircle` halo builds and
  shows: Circle, Rune, HexRing, Cross, Arc, Wave, Halo.
- **`Shape` vs `DisplayShape`** — `spell.Shape` is a *gameplay* flag (`Missile` = homing, see
  §3.8.1); `DisplayShape` is what gets drawn. They are separate fields because a spell can be a
  homing missile and still want its school's family body.

**The single sanctioned exception:** `MagicTestMatrix`'s school **header swatch** keeps its own table
rather than calling `SpellLook.SchoolColor`. It is a QA surface, not a readout — when the swatch and
the thing being judged are the same colour, a mis-coloured spell becomes invisible on the very screen
built to catch it. It is debug-only and save-invisible, so it needs no parity check.

**Shadow distance is 32 m (PC) / 40 m (Mobile), and the streamed world is ~300 m across.** That
mismatch is a standing candidate for the sub-20 FPS report and is *not* resolved by anything in the
1ik lane — 1ik measures and attributes, it does not change a render setting. Fixing it is a separate
task with its own measurement.

**How this is judged (1ic):** the test ground's **F4** lane resolves every reachable spell and reports
`N spells / M distinct identities / C colliding groups`, where identical means impact + cast + shape +
sky rock + core RGB at 8 bits. `Scale`/`Tempo` are excluded — counting them would let a number read "unique"
while two spells look identical on screen. **`M` must equal `N` (172: 167 magic + 5 class).** A session
has now read that number (`172 / 172 / 0`, `(worst none)`), so the identity tables are collision-free
and no jitter retune is needed — but `M == N` is a **static** result: the audit resolves looks into a
dictionary and spawns nothing, so 1id–1ii stay **play-test-open** until one spell per school is fired
and the halo, impact family and body shape are confirmed on screen. 1f7 added SkyRock to that key
and did **not** re-run the lane: the change is argued inert in §3.8.4, so treat the figure as
covering 1ib–1ii only and re-press F4 to confirm it.

**Where the frame time goes (1ik):** a separate read-only lane on **F2** attributes the frame to CPU
main thread, CPU render thread, or GPU, and prints the draw/batch/triangle counts and the render
settings that govern them. It samples continuously and the key is the snapshot boundary, so the report
describes the frames that ran *up to* the press — a frame time over one frame is noise. Two reading
rules are baked in because they change what the numbers mean: a source that never reported prints
`n/a` and never `0` (a valid-but-empty profiler counter returns 0, which would read as "this side is
free" about the one side the Editor cannot see), and under vsync the frame is a whole number of present
intervals, so the cost is reported as a **bracket** rather than a share of a quantised total. This
matters because the FPS overlay's own counters are all *streamer* counters — they cannot see the cost
of drawing 380 chunks and ~1,200 far cells.

#### 3.8.4 Sky-Rock Bodies — `SkyRockStyle` (1f7)

**The axis that had no per-spell hook.** Until 1f7 a spell's visual identity was carried by three
enums, a colour pair and two scalars — and the *falling rock* was not one of them, so all six
`SummonFallingRock` spells fell as the same one-boulder body on two different size ladders (a Zone
spell spends its full blast radius, 3-4; a Storm spell half of it, 1.6-1.8). The 1f7 fix added
`SkyRockStyle` as a fourth enum resolved exactly like the others, with the values in the sky-spells
table in §3.8.1.

**It is authored-only, and that is the point.** Unlike `SpellImpactStyle` and `SpellCastStyle`,
`SkyRockStyle` has **no school family and no deterministic pick**: `Inherit` always resolves to
`Boulder`. Impact and cast families jitter between looks that are all equally valid, whereas a
sky-rock style is a *structural* statement about how the spell reads — the same reason
`DisplayShape` may never hand a spell `Missile`'s homing (§3.8.1). Jittering this axis would have
given Meteor a swarm half the time.

**Three habits from it:**

- **A new visual axis is not automatically a new family.** Ask whether the value is a *choice
  between equally-valid looks* (impact, cast — jitter these) or a *statement about the spell*
  (sky rock, display shape — author these). The first kind needs a school array to make 151 spells
  differ; the second needs one author who means it.
- **An authored-only axis cannot perturb an existing collision audit.** The F4 lane's identity key
  gained SkyRock's 2 bits in 1f7. Because every non-authored spell resolves it to `Boulder`, every
  other spell got the *same* two bits — and adding a discriminating axis can only split a group,
  never merge one, so the recorded `172 / 172 / 0` still holds. State that reasoning when an audit
  key changes, because "the numbers should not have moved" is otherwise indistinguishable from
  "the numbers were not re-run".
- **A shape that only a live cast can draw is a shape with no acceptance readout.** The magic-model
  bench draws `MagicProjectileModelBuilder.CreateProjectileDisplay`, which a Zone spell has no answer for — so Fire
  Asteroid's new swarm would have been the one 1f7 visual that existed only during a cast, invisible
  on the bench built to compare it. `SkillFx.BuildRockBody` was extracted from `FallRock` so the
  bench can mount the real formation on its pedestal, and `LookKey`/`Describe` now print the axis.
- **1ir removed both new-look readouts and then deleted the visuals they described.** The lesson is
  that a per-spell look and the ability to *see* it are one feature, not two: the SkyRock axis lost
  its only second value when Continuous Fireball took Asteroid's slot, and Ember Streak lost its only
  user when Flamethrower took Comet's. Extracting the body out of the live cast (`BuildRockBody`,
  `SpellBeam.BuildConeVisual`) is what made the swarm inspectable in the first place — and it is why
  deleting a style that is *still visible elsewhere* stayed a one-file change. The bench branch has
  three cases now (rock / cone / following circle) rather than one, because a delivery with no
  projectile body needs its own mount; two new spells both falling through to the generic orb would
  have been the exact repeat of the 1f7 miss.

#### Spell Sources

- **Equipped weapon** — a staff/wand/book in a hand slot (its magic-mods apply).
- **Active skills** (§3.3) — spells granted via skills/skill books can also be cast from the skill bar; they route through the same `SpellCaster` so the pipeline is shared.

#### Expandability

Adding a spell = creating a new `SpellData` asset (zero code changes), consistent with the rest of the data-driven systems.

### 3.9 Talent System (Player-Level Perks)

A small **rankable perk layer** sitting on top of character leveling — separate from the skill trees and
the stat points you spend per level-up. It rewards long-term play and lets every build tune how it
progression-by-progression grows.

- **Earning:** ranks are granted **freely** (no talent-point currency) — click *Rank Up* on any talent,
  capped at max rank. A brand-new character is granted **one random talent at rank 1** at game creation
  so the system is immediately visible.
- **Talents (26 total, all max rank 3, effects additive per rank):**
  - *Fast Learner* — **+5 % character XP** per rank.
  - Seven **skill-type** talents (Melee/Ranged/Magic/Stealth/Crafting/Fortitude/Shield, e.g. "Arcane Study",
    "*Shield Work*"), **+6 % XP per rank** for that skill type — boosts both the per-skill levels (§3.3)
    and the category bar.
  - Eleven **stat** talents (one per core stat, e.g. "Vitality" = Health), **+1 flat stat point per rank**
    layered onto the stat total.
  - *Critical Eye* — **+2 % critical-hit chance** per rank.
  - *Executioner* — **+15 % crit damage** per rank.
  - *Ambush* — **+10 % backstab damage** per rank.
  - *Bulwark* — **+10 % block stamina efficiency** per rank.
  - *Grounded* — **+10 % stagger resistance** per rank.
  - *Second Wind* — **+10 % stamina regeneration** per rank.
  - *Arcane Spring* — **+10 % focus regeneration** per rank.
- **Effect reads are live:** XP bonuses are applied as a +% on every XP grant; stat talents add flat
  points inside `GetTotal` (base × race/race-skill % **+** the stat talents' flat add + temp buffs). The
  combat/regen talents fold **additively** into the same `PlayerStats` getters as the skill-tree perks
  (crit chance, crit/backstab/block/stagger multipliers, stamina/focus regen), so HitboxSystem,
  CombatController, PlayerController and SpellCaster pick them up with no extra plumbing. Because
  bonuses are computed from owned ranks on every read, saving/loading can never double-apply them.
- **Persistence & UI:** owned ranks + the first-grant flag are saved (`talentStateJson`); the
  **Character Info panel** lists all talents below the stat/level block (one vertical scroll), each with
  a *Rank Up* button enabled while the talent is below max rank. There is no talent-point counter.

---

## 4. Multiplayer System (Dedicated Server) — REMOVED in 1hz

**This whole section is removed code, not planned work.** 1hz deleted the networking layer outright;
see §6.4 for the exact set. The section number is kept only so the surviving `§4` cross-reference
in §5.11 (player trading) still resolves.

What went:

| Removed | Was |
|---------|-----|
| `Assets/Scripts/Networking/*` (16 scripts) | connection/session/server/anti-cheat plumbing |
| `UI/NewWorld/MultiplayerBrowserUI` | server browser, friends, party |
| `UI/NewWorld/MultiplayerIndicatorHUD` | player nameplates, health bars, cast bars |
| `Packages/manifest.json: com.unity.multiplayer.center` | still present; no code referenced it |

There is **no** multiplayer mode, co-op, invasion, arena, matchmaking or chat in this project.
"Open-world" refers to the streamed terrain (§2), not to shared worlds. Player trading is not
implemented and was never shippable without a server (§5.11).

---

## 5. Retained Side Content (from CountryLife)

All existing CountryLife systems are retained as optional side content within the open world. The
following inventory is code-confirmed against `Assets/Scripts` (see §6 for what was actually removed).

### 5.1 Tools & Inventory

The **ToolManager** drives tools and inventory: **40 item slots** — a **10-slot hotbar** (number
keys, Minecraft-style quick bar) plus a **30-slot backpack storage grid** (Character Info →
Inventory tab). Tools swap a matching **3D model** on equip (`ToolManager.ToolModels.cs`).

| Tool | Use |
|------|-----|
| **Hoe** | Till soil for planting |
| **Sickle** | Harvest crops (yields quality bonuses, skill XP, quest progress) |
| **Axe / Mattock** | Gather materials |
| **Shovel** | **Excavate terrain (1cs):** each swing digs a small bowl of soft ground (grass/dirt) one `CraterStep` deeper, revealing the strata bands as the hole descends. Stops at the **stone band** at ~2.7 m down — the pickaxe takes over there. Uses the same crater excavation path as Earth magic, so tool pits and spell craters share one shape. Each swing pops a few **dirt-block debris chunks** out of the floor, tinted by the stratum being dug (1de). |
| **Pickaxe** | Mine **rock props** (loose boulders/rock debris in the world) AND **excavate terrain at any depth** (1cs), including the stone band the shovel cannot break — the ground-breaking tool once a pit reaches stone. Terrain digs eject **stratum-tinted debris** (dirt blocks near the surface, rock once the pit reaches stone). Both cost stamina per successful strike. |
| **Fishing Rod** | Fish (gift from Jessica) |
| **Hammer** | Open the build menu (**hold Hammer + F**) |
| **Club** | Melee demons; knock out thrashing fish on the shore; knock out livestock **non-lethally** for cage capture (1df) |
| **Rosary** | Ranged holy orb — **one-shots** enemies but costs **1 Karma** per shot |

Drop items with **Q**; slot API: `SelectSlot` / `PeekSlot` / `AddItem` / `RemoveItem` / `MoveSlot`,
with `GetInventorySave()` / `LoadInventorySave()`.

Weapons are also physical bag items in this same 40-slot inventory (stack-counted, one copy per
equipped rig); the `WeaponInventory` owned list gates what can be equipped to the hand slots, and
equip/unequip is copy-accurate (§5.5).

### 5.2 Farming

- **10 seed types** (wheat, corn, potato, carrot, tomato, strawberry, pumpkin, onion, sugarcane,
  rice), planted via `TryPlantSeed` on tilled plots.
- Plant → multiple **growth stages** → harvest with the **Sickle**; quality bonuses scale rewards,
  plus skill XP and story-quest progress (`ToolManager.cs:1000-1035`).
- Farm quests push you from **50 → 150 wheat** harvested ("Mùa Thu Đầu Tiên" → "Bàn Tay Xanh").
- Crop risk via random events: weeds, pests, drought.

### 5.3 Fishing

- Stand by the west sea, cast with the **Fishing Rod** (LMB), wait for the **float to bubble**,
  then start the reeling **minigame** (keep the line in the green zone to fill the bar; ~80s
  window). FSM-driven.
- Fish that flop on the shore are knocked out with the **Club**, then picked up.
- Catch & sell prices: **Carp 15 / Salmon 25 / Tuna 40 / Puffer 60**.
- **Rod levels 1–3** and **bait** purchasable; the Fishing Shop vendor and Jessica's 3-fish quest
  are wired.

### 5.4 Crafting

- Crafting stations placed in player homes or found in towns — driven by
  **`CraftingManager`** with resolvable **station categories** (`ResolveStationCategory` /
  `InteractStation`).
- Weapons, armor, potions, food, tools.
- Recipes discovered through exploration, skill books, and the Crafting skill branch
  (`SkillType.Crafting`, e.g. "Steady Hands" — +3 Luck crafting quality).

### 5.5 Equipment

Equipment is split into **3 genres**, each mapped to a fixed set of gear slots (21 total).

#### Genres & Slots

| Genre | Slots | Qty |
|-------|-------|-----|
| **Armor** | Head, Body, Glove, Legging, Feet | 5 |
| **Weapon** | Left Hand, Right Hand | 2 |
| **Accessory** | 10 Fingers, Necklace, 2 Ear, Belt | 14 |

**Armor (5 slots):** The source of **physical damage reduction** (amplified by the **Defense** stat) and of **all elemental/magic resistance**. Per the equipment-only resistance rule (§3.4), no stat grants resistance — armor/gear does. Heavier armor weighs more (raising **EquipLoad**, gated by Endurance).

**Weapon (2 hand slots):** Every weapon is **one-hand capable**, so any two can be dual-wielded — including **two of the same weapon type** (one rig per hand = one owned copy per rig). Wielding is governed by the weapon's **Strength (Str) requirement** (by weight class: light / medium / heavy):

| Configuration | Requirement |
|---------------|-------------|
| **One-handed** (1 weapon) | Full Str requirement of that weapon |
| **Two-hand grip** (both slots) | **Reduced** Str requirement (~half) — lets low-Str builds use heavy weapons at the cost of no off-hand weapon/shield |
| **Dual-wield** (one per hand) | Roughly **2× the single-hand Str requirement** — high-Str builds can dual-wield greatswords, hammers, etc. |

Two-handing occupies both hand slots (no off-hand); dual-wielding occupies both with separate weapons (possibly two copies of the same weapon). Weapons carry their own **damage** (ranged uses `weapon.base`), **speed**, **range**, and a unique **Weapon Art** (§3.2 combat, costs FP). The **Knight** class raises equip-load carry; **Blacksmith** improves upgrades/repair/forging.

##### Same-Type Dual-Wield & Copy Accounting

Weapons are also **physical bag items** — stack-counted in the ToolManager inventory (§5.1) alongside tools — while the `WeaponInventory` **owned list** gates what can be dragged onto the hand slots. Equip/unequip is **copy-accurate** (never duplicates, never loses, an item):

- **Equip — `EquipOwnedWeapon` (CharacterInfoUI.cs):** dropping a weapon onto the hand that already holds it is a no-op. When the *other* hand holds the same id, a **spare copy in the bag** (`CountItem >= 1`) permits dual-wield and the spare is consumed (`RemoveItemAmount(weaponId, 1)`); with no spare the weapon is **moved** (the other hand's rig is destroyed first) so a single owned item is never duplicated onto the body. Any weapon displaced from the target slot returns to the bag first; if the bag is full the swap aborts ("Túi đồ đầy").
- **Unequip — `UnequipWeapon` (CharacterInfoUI.cs):** destroys one rig per copy released and refunds exactly that many copies back to the bag (`PutItem`/`AddItem`). An optional `sourceHand` clears only the dragged hand, so a second identical weapon on the other hand stays equipped; otherwise every hand holding the weapon is cleared.

**Accessory (14 slots):** 10 rings (one per finger), 1 necklace, 2 ear pieces, 1 belt. These grant **passive bonuses** (stat, status, luck, utility). The bulk of defensive **resistance/DR** comes from armor — accessories supplement it and carry build-defining passive mods.

#### Hand States the system tracks

- **Single** — one weapon, off-hand free (weapon, shield, or orb).
- **Dual** — one weapon per hand. Controls split per-hand on the mouse (§5.16): **LMB and RMB each
  drive one hand** (crossed sides whenever a ranged weapon is among the two). Melee swings on press,
  shield guards while held, magic fires uncharged, ranged holds-to-charge/release-to-fire.
  **Blocking is only possible through a shield hand while dual-wielding.**
- **Two-hand grip** — both hands on a single heavy weapon (reduced Str need).
- **Draw/stow (fight-only)** — equipped weapons are **drawn (in hand) only while fighting**;
  **casual mode always sheathes them onto the body** (back carry / waist scabbard, see §3.6),
  regardless of camera view (first person included — the fight-only rule was restored in `1ct`
  after the 1cr "keep drawn in first person" play-test read as an unwanted fighting pose in
  normal mode). Entering combat draws them again (animated `.ApplyPose` transition on the combat
  toggle; instant snap on a model rebuild); the rig's renderers stay on the Default layer, kept
  visible by `CameraModeSwitch`.

### 5.6 Night & Survival

- **6 PM → 6 AM**: demons rise and attack the player and structures (`hour >= 18 || hour < 6`).
- Regular demon ~50 HP, deals ~10 damage; **giant demons** have higher HP/damage.
- Manage **HP** and **Stamina**; **eat to recover** (food stamina/HP recovery via
  `ToolManager.FoodStaminaFor`).
- **Close doors at night** to block demons; demon-wave and giant-enemy events are in the
  RandomEvent roster (§5.13).

### 5.7 Karma (Phước Đức) & the Pagoda

- **Karma** fuels the **Rosary** (1 per shot); it **regenerates over time** (`KarmaManager.RegenKarma`,
  gated into days) and its **max grows** through meditation, building, and defeating enemies.
- Displayed as a **Karma bar** on the HUD (e.g. `Phước Đức: 5/10`); consumed by Rosary one-shot kills.
- **Pagoda**: the 4-tiered, curved-roof landmark east of the village beside the neighbor's house —
  pray, **meditate** (typing/meditation minigame), and watch the sunset. The **Monk** there is
  connected to the **exorcism quest** (Rosary kills only).
- **The three holy places** — **pagoda** (Buddhism / monk), **church** (Catholic / priest), and
  **taoist shrine** (Taoism / taoist priest) — are the worship sites for the Faith system
  (`ReligionManager`): worshipping joins/switches the player's faith, grants devotion (+1 per worship
  day, up to a cap) and unlocks that faith's blessing perks. Each site is built from its
  `WorldBuilder.Build*` structure + matching worship NPC. **QA/Test Ground**: the independent test
  platform (`NewWorldTestGround.EnableReligion`) places all three structures and worship NPCs so the
  Faith tab is testable without the legacy village.
- **Structure scale (1cz)**: all three holy sites now match the pagoda in size and detail —
  **church** is 16×14.8 for the podium (~21 tall, 13 parts: nave + arcade columns, gothic side windows +
  rose window, gold-cross apse, gabled nave roof with corbel-stepped gable ends, tall front steeple tower
  with belfry + gold spire + cross, stepped buttresses, interior pews/pulpit/altar); **shrine** is 13.2×12
  for the podium with a 4-tread stair running out to `z = −9.6` (~12.7 tall, 12 parts: single-storey Taoist
  hall — seven-column colonnade, front facade with a 3.0 m doorway, 18° gable roof with corbel-stepped gable
  ends, a ridge lantern straddling the ridge, and the Three Pure Ones on a rear dais with a yin-yang back
  wall, offering altar and tripod censer). Exclude-radii were raised (church 15, shrine 14)
  and the worship NPCs stand in front of each entrance (priest west of the church, taoist south of the
  shrine). **Test-lane NPC heights (1hp)**: the `MapBuilder` faith rigs are authored around a **body
  origin** (shoes at local `y −0.88`), so a root placed at the ground plane sinks them to the knees —
  0.915 m for all three. `NewWorldTestGround.StandOnGround` **measures** each rig's lowest renderer
  bound and drops the root so the feet rest on the platform, so no literal 0.915 can go stale. The
  taoist stands 2.9 m clear of the shrine's stair foot and the monk off the pagoda's podium, facing it.
- **Church rebuild (1hn)** — the church is now authored on one explicit datum ladder, so every block's
  support is readable in the source and none of the previously reported gaps remain:
  `slab 0.35 → terrace cap 0.40 → nave floor 0.50 → wall plinth 1.10 → wall band 4.30 → cornice 4.60
  (the roof's bearing) → ridge underside 7.35`. All 13 part roots sit at the site origin. Specifics:
  - the nave roof now spans the **full** nave (two `CreatePartPanelBetween` panels from an eave underside
    buried 6 cm in the cornice at `|z| = 6.15` to the ridge underside at `z = 0`), with a ridge cap laid
    along each panel's own top surface and corbel-stepped gable infill on both side walls;
  - **one axis**: the tower, belfry, spire roof and spire all share `z = −6.20`, so the spire sits on its
    own spire roof (previously the spire roof was centred near `z = 0` and the spire floated beside it);
  - the side walls carry **six real window openings** (sill band + lintel band + seven piers) with the
    glass inside the hole instead of buried in a solid slab, plus gold tracery and a projecting sill;
  - wall corners are closed by overlapping wall panels, corner pilasters and returns, stair treads are
    solid blocks from below grade, and the podium was extended in `z` to 14.8 so the **rear buttresses
    have a support** (they previously stood off the back of the podium);
  - every interior fitting (12 pillar bases, 6 pews, altar, pulpit and its two steps, candles) is seated
    on the walking surface; the tower landing meets the nave with one 0.20 m step instead of a 0.40 m drop.
- **Shrine rebuild (1ho)** — the two-tier shrine became a **single-storey hall with a ridge lantern**, on
  one explicit datum ladder, all 12 part roots at the site origin:
  `podium 0.45 → cap 0.60 → floor 0.75 → column top 4.20 → architrave 4.69 (the roof's bearing) →
  eave underside 4.05 → ridge underside 6.194 → ridge top 6.679 → lantern plate 6.599 → lantern deck 6.899
  → lantern eave 7.879 → lantern ridge 8.607 → spire base 8.791`. The old `Shrine_Tier2Floor` /
  `Shrine_Tier2Walls` / `Shrine_Roof2` / `Shrine_Spire` keys are **kept and repurposed** as the ridge
  lantern, not renamed (they are save keys) and not deleted. Specifics:
  - the **18° gable roof** bears on the architrave instead of balancing on it: the rake crosses the
    plate's top plane at `|x| = 4.63` and the beams run out to `6.20`, so the panel is buried 0–51 cm
    into the plate across 1.57 m. Hanging the eave 14 cm below the plate (as the church's cornice does)
    would put that crossing at 6.77, past the end of every beam — a roof bearing on nothing;
  - the lantern is a **saddle on the ridge cap**, 8 cm into it. Sinking it 45 cm to "straddle" the ridge
    put its bottom 3 cm under the roof's own ceiling at the crown: a 3.5 cm slot along the ridge, visible
    from inside the hall;
  - the front is a real **facade**: two piers either side of a 3.0 m doorway with a lintel whose ends
    run 20 cm into them, pilasters, a gold threshold and a gable board on top; the old colonnade's
    front-centre column position is now that doorway;
  - `CreatePartGableSteps` was generalised to take the wall's plane point and normal plus a **separate
    `gableHalfSpan`** — the shrine's gables are 6.0/6.1 wide under a 6.60 eave, and the helper had
    assumed the wall and the roof were the same width, which would have traced the wrong line (and,
    with the eave 64 cm below the wall top, produced negative-height bands);
  - the **Three Pure Ones** stand on one dais at `z = 2.20` (not 3.60, which put the middle figure inside
    the rear centre column): Yuanshi in gold with a ruyi and a fan, Lingbao in jade with a pearl, Daode
    in white over purple with a whisk and a beard, each with a lotus throne, mantle, sash and diadem;
  - the censer moved to `z = −2.60` (at −3.60 its bowl ran through the new facade), and the altar, its
    step and the kneeling mat to `z = −0.20 / −1.30 / +0.70`, keeping the whole interior walkable.
- **Hand-authored geometry convention (1hm)** — the three holy places are built from raw cubes by
  `WorldBuilder.Build{Pagoda,Church,Shrine}Part`, one `StructurePart_<Type>` root per part (13/12/14
  parts). Two authoring mistakes caused every structural gap ever reported in them, so geometry is
  now stated by its support instead of by a hand-computed centre:
  - blocks go in through `CreatePartBoxOn` (**bottom** face authored, never the centre);
  - roofs/ramps/stairs go in through `CreatePartPanelBetween` (**the two ends of the underside**),
    which makes an inverted pitch unauthorable — the four pagoda roofs had been authored as a tilt
    sign that pitched their outer eaves up, reading as butterfly roofs;
  - gable triangles close with `CreatePartGableSteps`: **uncentred** bands, each reaching the roof
    underside at its own **inner** edge (a centred step's inner edge is the ridge, so it would have to
    be as tall as the peak) and overshooting 6 cm into the slab — an intersection is invisible, a gap
    is a slit. The helper takes the wall's plane and a `gableHalfSpan` **separate** from the roof's
    `roofHalfSpan` (a wall narrower than the eave must trace the wall's line, not the roof's), and
    clamps the run to where the roof underside meets the wall top — otherwise, when the eave hangs
    below the wall's top line, the outer band's derived height goes negative and it builds a mirrored
    cube with an inside-out collider.
  The rebuilt church and shrine author every part in **site coordinates** (y = 0 = platform top, all
  part roots at the site origin) so the whole assembly is auditable in one frame. Part **type strings
  are the save/load keys** — never rename one; a renamed part silently builds nothing.

### 5.8 NPCs & Relationships

- **Jessica** (the neighbor girl): befriend via gifting, romance, and **marry** her — `WifeNPC`
  (marriage gated at **day 5**; wife lives in the mansion, dialog + per-day events).
- **Phú Ông / The Rich Man**: guards a secret behind the mansion — stake out at night and
  **report to the police** (story quest "Bí Mật Của Phú Ông"). The nightly drug deal starts from
  **21:00 on day 3** (`DEAL_HOUR` / `DEAL_START_DAY` in `RichManNPC`); the camera, bribe and leave
  rows all trigger inside a ±12 × ±8 m box around the **deal site** at `(0, 0, 95)` — the plot of
  land the night club used to occupy. 1hz removed the club *and* the NPC's 19:00–21:00 hangout
  routine that walked him to its entrance, so the breadcrumb is now one location-agnostic
  message per evening ("The rich man slips out at night... find him.") rather than a sign pointing
  at a building that is no longer there. See §6.4.
- **The Monk** (pagoda meditation/exorcism), **The Librarian** (holds every blueprint),
  and village merchants: **Fishing Shop**, **Chef**, **Café**, **Buffalo Shop**.
- **Friendship system**: gift villagers with hotbar items via number keys; some NPCs dislike
  certain gifts.
- NPCs provide quests, shops, lore, companionship across the open world.

### 5.9 Livestock & Pets

- **7 livestock species** spawn naturally around the farm over time; the **Buffalo** is a live
  entity sold/purchased at the **Buffalo Shop**.
- **Pets** (dog, and the **goblin** — late-game) follow and aid combat; the goblin has its own
  **command menu** (follow/stay/home), **own HP**, and **own storage**.
- **Livestock are damageable through the combat pipeline (`IDamageable`, 1df):** melee, weapon
  skills, spells, zones, projectiles, tornadoes, storms and damage-over-time all hurt them — pigs
  and goats fight back, chickens/ducks/turkeys flee, cows and sheep stay passive, and every hit
  flashes the animal red. **At 0 HP an animal explodes the old-game way:** all its voxel parts
  detach, gain colliders + rigidbodies and are blasted outward with impulse + torque (the same
  death burst as enemies, §7.1.1), self-cleaned after ~5 s, and the animal is gone (the spawner
  trickles replacements). No loot/meat. The **club is the non-lethal capture tool** — it knocks an
  animal out (~15 s, no HP loss) so it can be caged; lethal weapons/spells, not the club, kill. A
  knocked-out animal is briefly immune so damage-over-time can't finish it mid-capture.

### 5.10 Construction, Housing & Infrastructure

- **Build menu**: hold **Hammer + F** → blueprint list (from legacy `WorldBuilder`) with **cost**
  and **locked** state; LMB place, F cancel. Blueprint type selection is via the UI menu —
  the legacy **B/N** cycling key is **not implemented**.
- **Blueprints** are learned at the **library** for gold 🪙.
- Buildings: house, mansion, restaurant, café, library, watchtower, walls (plus the pagoda).
  The **night club** was removed in 1hz (see §6.4) — `ClubExteriorBuilder` and
  `MapBuilder.Nightclub` are gone, and no blueprint, `RebuildEssentialBuilding` case, or
  registry entry remains.
- Player homes can be built/decorated; **chests** for storage (`ChestStorageManager`), with
  **crafting stations** and **farming plots** attached.
- Walls and watchtowers defend the farm; **storms/earthquakes/tornadoes** (events) can damage buildings.

### 5.11 Economy

- Gold (🪙) is the unit; earn by selling produce, fish, quest rewards, and restaurant/café income.
- **Vendors and shops** in towns (`VendorShopManager`, `BuffaloShopManager`) with buy/sell tabs
  and price multipliers.
- Player trading is **not implemented**; it was designed around a dedicated server, which 1hz
  removed (`§4`), so there is nothing to trade *with* any more.

### 5.12 Quests

- **Story chain** (day-gated, some require the previous quest) — driven by `QuestManager`:

| Quest | Requirement | Day | Reward |
|-------|-------------|-----|--------|
| Chào Hỏi Hàng Xóm | Greet Jessica | 1 | — |
| Bí Mật Của Phú Ông | Stakeout + police report | 3 | 500 |
| Mùa Thu Đầu Tiên | Harvest 50 wheat | 3 | 150 |
| Bảo Vệ Đất | Defeat 10 enemies | 5 | 300 |
| Bàn Tay Xanh | Harvest 150 wheat | 8 | 400 |
| Xây Dựng Đại Phú | Earn 50,000 gold | 10 | 750 |
| Thợ Săn Quái Vật | Defeat 30 enemies | 12 | 600 |
| Trận Đấu Cuối Cùng | Defeat 50 enemies | 15 | 1,500 |
| Tỷ Phú | Earn 200,000 gold | 18 | 3,000 |

- **Daily** repeatable quests, **timed** quests, and **exorcism** quests (only **Rosary** kills
  count, with a popup per kill while incomplete).

### 5.13 Random Events

- `RandomEventManager` keeps **21 live event** definitions: crops advance, gold on ground,
  wounds heal, seeds rain, pests/plague, drought, weeds, fireflies/rainbow, exhaustion,
  fish rain, enemies approach, thief, animals dance, giant enemy, 3 monster waves, meteors,
  village celebration/fireworks, ghosts, buried treasure, earthquake, lightning/tornado, and more.
- Removed from live rotation (see §6.4): market crash/boom, new trade routes, migrant family.

### 5.14 Endings & Ending Tree

- **8 cutscene endings** are implemented (`EndingHappy`, `EndingSad`, `EndingFated`,
  `EndingDemon`, `EndingJustice`, `EndingNTR`, `EndingBlackmail`, `EndingBossBad`) but are
  **gated off** at runtime (`CutsceneManager.RemoveEndings = true`); the shipped game plays as an
  ongoing open-world RPG.
- The **Ending Tree** panel (`UIManager.Endings.cs`) remains wired to unlock/review endings
  from the main menu — re-enable by flipping the gate.

### 5.15 Meta Systems

- **Fast travel is REMOVED in 1hz** (`FastTravelSign` + `FastTravelMenu` deleted; see §6.4). What
  survives is the *map marker* half: `PoiKind.FastTravel` / `POIDefinition.IsFastTravelPoint` still
  drive the `✈` glyph and the `FT` kind name in `WorldMapUI.Refresh`, and `FastTravelNode` is still
  built by `POIGenerator` as a bonfire stand-in. `POIRegistry` currently registers **no**
  `PoiKind.FastTravel` entry, so no bonfire actually spawns — the marker path is dormant, not dead
  code, and is what a future fast-travel implementation would hang off. Nothing teleports the
  player any more: the road signs only ever opened the menu that no longer exists.
- **Save/Load**: multi-slot `SaveManager` (PlayerPrefs last-slot memory); **sleep on the bed** to save.
- **Settings**: mouse/touch sensitivity, invert Y, language (**Tiếng Việt / English**), PC / Mobile mode.
- **Game Stats** (`UIManager.HUD`): wheat harvested, enemies defeated, money earned, money stolen.
- **Gender selection** at start (cosmetic only); **tutorial book**; message banner; item tooltips.

### 5.16 Controls

- **WASD** move · **Space** jump · **Shift** sprint · **Mouse** look
- **LMB** use tool · **E** interact/open · **Q** drop item · **F** build menu (with Hammer)
- **1–0** hotbar — mobile touch support included.
- **Fighting mode** (weapon drawn): **LMB** attack / begin a magic aim · **RMB** block (melee **or** shield) or
  charge/draw (magic/ranged) · release **LMB** fires at the frozen charge level.
  **Beam spells** keep firing but **holding LMB extends the beam** and drains FP per second — releasing
  ends the channel early (see §3.8.1).
  **Dual-wield (both hands hold real weapons):** the buttons split per hand instead —
  **LMB → one hand, RMB → the other** (`PlayerController.HandleDualModeCombat`, §5.4):
  - **Same-side** by default: **LMB = left-hand weapon**, **RMB = right-hand weapon**.
  - **Crossed** (LMB → right hand, RMB → left hand) whenever a **ranged** weapon is one of the two,
    so the bow/throwing hammer keeps its hold-to-charge/release-to-fire draw on its own button.
  - Hand action: **Melee** swings on press · **Shield** guards while held (release drops the guard) ·
    **Magic** (mixed dual) loses charge and taps fire the armed spell uncharged · **Ranged** holds to
    charge and releases to fire.
  - **Trade-off:** dual = no block except via a shield hand (2 swords = 2 independent attack buttons).
    Pressing the attack button while a guard is raised drops the guard and swings. **Both-magic** and
    any 0/1-weapon loadout (incl. barehanded fists) keep the standard single-button scheme above.
- **Alt** (fighting mode, magic weapon held) — opens the dev/test **magic grid** pinned to the
  **right edge** of the screen (the old centre-screen ring/wheel is retired, note 1av). The grid is a
  tall, scrollable, clickable list of **every castable magic-category skill in the game** (base +
  branch schools — not just those the current profile has learned; no physical melee/ranged/stealth
  castables), **grouped by school** (`Skill.DamageKind`, §3.7). Clicking a row **only arms** that spell
  in the bottom-left "Armed: X" chip — it does **not** cast, learn, or top up focus (a click picks a
  spell, it never fires one). While the grid is open the controller **suppresses attack / aim / charge
  input** so a row click cannot leak into a cast (an aim already in progress is cancelled on open).
  Close the grid, then use the LMB/RMB charge/release flow above to fire the armed spell. The
  armed-chip + charge/release backend is otherwise unchanged; the grid only borrows it. Toggle closed
  with **Alt** or **Esc**; scroll with the mouse wheel or by dragging.

---

## 6. Systems Removed / Disabled (Code-Confirmed)

> **Correction to earlier drafts:** the old-game systems below were previously described as fully
> removed. Verifying the current code shows most were **kept** (often behind a runtime flag) rather
> than deleted. There is no archive copy: 1it deleted the project-root `_Archived/` folder, because
> every one of its `.cs` files except `Mob.cs` and `WorldBuilder.FastTravel.cs` had a live
> counterpart already in `Assets/Scripts` (its `README.md` claimed the opposite — that `CutsceneManager`,
> `WorldBuilder`, `QuestManager` and `EnemyController` were retired), and git history keeps the two
> unique files.

### 6.1 World Builder (Voxel Cube System) — **legacy generation disabled, content retained**

- `WorldBuilder.EnableLegacyGeneration` defaults to `false`, replacing the legacy finite voxel map
  with the chunk-based terrain system (§2).
- The WorldBuilder **content systems remain in use**: its blueprint list powers the build menu
  (§5.10), and its farming fields/pagoda are live.
- Legacy generation is disabled by `EnableLegacyGeneration = false`; there is no archive copy (1it).

### 6.2 Endings System — **implemented but gated**

- **8** cutscene endings exist (`EndingHappy`, `EndingSad`, `EndingFated`, `EndingDemon`,
  `EndingJustice`, `EndingNTR`, `EndingBlackmail`, `EndingBossBad`) with the **Ending Tree** UI
  wired in the main menu.
- They are **disabled at runtime**: `CutsceneManager.RemoveEndings = true` short-circuits every
  ending entry point to `EndingsRemoved`. The shipped game therefore plays as an ongoing
  open-world RPG; flip the gate (or delete the early-return) to re-enable endings.
- Cutscene helpers/road-driving partials remain active in `Scripts/Legacy/Cutscenes/` (1it removed the
  stale archive copy; **1jc** moved the whole `CutsceneManager.*` family under `Scripts/Legacy/`, which
  is **read-only** per AGENTS.md rule 18 - see §9.4c).

### 6.3 Story Quests — **retained**

- The Vietnamese **story quest chain is NOT removed**. `QuestManager` is active and drives the
  **9-day-gated story tiers** (§5.12) plus daily/timed/exorcism quests; `RandomEventManager` is
  active with 21 live event types (§5.13).

### 6.4 Truly Removed Content

- **Market crash / boom** and **new trade route** random events (sell-price halve/double, buy-price
  discount) — removed from the live roster; only localization strings remain. The Economy §5.11
  therefore keeps fixed vendor pricing.
- **Migrant family** ("Người di cư") subsystem and event — stripped from code (no references remain).
- **B/N building-type cycling key** in the build menu — not implemented; blueprint selection is
  done through the UI menu only (§5.10).

#### 1hz — the feature-removal pass

Five features were deleted in one pass. 29 scripts, 16 of them the `Networking/` folder, plus 29
`.meta` files and the three now-orphaned folder metas (`Networking.meta`, `NightClub.meta`,
`Vehicles.meta`).

| Removed | Files | What replaced it |
|---------|-------|------------------|
| **Multiplayer / dedicated server** | `Scripts/Networking/*` (16), `UI/NewWorld/MultiplayerBrowserUI`, `UI/NewWorld/MultiplayerIndicatorHUD` | nothing — the project is single-player (§4) |
| **Night club** | `Models/ClubExteriorBuilder`, `Models/MapBuilder.Nightclub`, the whole `Scripts/NightClub/` folder | nothing; the club site is now a fixed vector on the rich-man dealer path (§5.8) |
| **Fast travel** | `Vehicles/FastTravelSign`, `UI/FastTravelMenu`, `World/WorldBuilder.FastTravel` | the `✈` map marker only (§5.15, §7.2) |
| **Horse riding** | `Vehicles/HorseMount` | `Models/HorseModelBuilder` survives — the ending cutscene still spawns a horse |
| **The `Recovery` scene dump** | 50 tracked files under `Assets/_Recovery/` | `.gitignore`d and **deleted from disk in 1iv** (27 of them abandoned `0 (N).unity` sample scenes); history keeps them |

Two consequences worth naming, because neither is a compile error:

- **A save key outlived its builder.** `"NightClub"` was still in the essential-building restore
  list in `WorldBuilder.Persistence.cs` while its `RebuildEssentialBuilding` case was gone, so a
  pre-1hz save stamped the club's health/door/part state onto the *previous* building. Fixed in
  `LoadBuildingsFromSave` (see §9.3).
- **Producers outlived their consumer.** Four POI files kept constructing `FastTravelSign` objects
  for a menu that no longer existed, so the deletion did not compile until each producer was
  removed too. This is the subject of the new `AGENTS.md` rule 13.
- **A behavioural outlived its place.** `RichManNPC` kept a full nightly routine — a 19:00–21:00
  window, a path to the club entrance, a watch/pace idle loop, and a "he is at the bar" toast —
  pacing the player around a lot 1hz had emptied. Removed in the 1hz follow-up: the state machine,
  the pace spots, the idle timer and both bar strings are gone, and `IsPlayerInClub` /
  `ClubCenter` are now `IsPlayerAtDealSite` / `DealSiteCenter` (same coordinates, §5.8). The deal
  itself is untouched — `DEAL_HOUR` (21:00) is now the single named constant for the hour, so
  `TryStartDeal` and `ForceStartDealForWatch` cannot drift apart again.

---

## 7. World Design

### 7.1 Biomes

Generated from noise layers, each biome has unique terrain characteristics:

| Biome | Terrain | Enemies | Resources |
|-------|---------|---------|-----------|
| **Plains** | Flat, gentle hills | Slimes, Wolves | Crops, herbs |
| **Forest** | Dense, moderate height | Bandits, Treants | Wood, mushrooms |
| **Mountains** | Steep, high elevation | Golems, Drakes | Ore, gems |
| **Swamp** | Low, muddy | Undead, Slugs | Rare herbs, poisons |
| **Desert** | Sandy, dunes | Scorpions, Mummies | Cacti, ancient relics |
| **Tundra** | Snowy, icy | Yetis, Ice Wolves | Frost crystals |
| **Volcanic** | Molten, extreme heights | Fire elementals, Dragons | Obsidian, fire essence |
| **Deep** | Underground caves | Demons, Mimics | Dark crystals, loot |
| **Ocean** | Water terrain | Sea creatures | Pearls, coral |

#### 7.1.0 Enemy Races — one folder + script per race (1do)

Enemy races are **not** data-only strings anymore: every race owns a folder under
`Assets\Scripts\Enemies\<Race>\` containing `<Race>Enemy.cs` — a subclass of the shared
`EnemyController` FSM (`Enemies\_Shared\`, the unchanged brain) that stamps its own `EnemyId` and a
**distinct stat profile** via `ApplyRaceConfig()`. `EnemyCatalog` maps a race id to its script
(spawners `AddComponent` the race's type, so each folder's script IS that race's brain and edits to
it apply everywhere — `EnemySpawner`, test-platform rows/dummies). `BossController` lives in
`Enemies\Boss\`. `EnemyModelBuilder` still supplies all procedural models and `BuildEnemy` by id.
The 21-race roster (HP / Dmg / MoveSpeed ; notable extras):

| Race (folder) | HP | Dmg | Speed | Signatures |
|---|---|---|---|---|
| Slime | 80 | 8 | 1.2 | never flees |
| Wolf | 55 | 12 | 3.5 | Alert 7 |
| Goblin | 45 | 10 | 2.8 | quick skirmisher |
| Bandit | 60 | 12 | 2.6 | baseline humanoid |
| Treant | 95 | 14 | 1.4 | AttackRange 2.0 |
| Golem | 120 | 16 | 1.2 | Armor 3, DR 10% |
| Drake | 70 | 13 | 3.0 | fast |
| Undead | 75 | 11 | 1.6 | shambler |
| Slug | 65 | 9 | 0.9 | never flees |
| Scorpion | 50 | 12 | 2.2 | balanced |
| Mummy | 70 | 12 | 2.0 | balanced |
| Yeti | 100 | 15 | 1.8 | Armor 2 |
| Ice Wolf | 60 | 12 | 3.3 | Alert 7 |
| Fire Elemental | 60 | 14 | 2.4 | Armor 1 |
| Dragon | 140 | 20 | 1.6 | Armor 3, DR 15%, range 2.2 |
| Demon | 130 | 18 | 2.2 | Armor 2, DR 10%, range 2.0 |
| Mimic | 90 | 16 | 0.0 | stationary ambush, tiny aggro |
| Sea Creature | 85 | 13 | 2.0 | balanced |
| Skeleton | 50 | 10 | 2.5 | baseline (original profile) |
| Bat | 30 | 7 | 4.0 | Alert 8 / Chase 12 / Leash 20 |
| Dummy (training) | 1000 | 0 | 0 | Immortal, never dies, regen 15%/s |

Mimic has no movement at all (ambush chest); the two test dummies differ only in flat
`DamageReduction`. Day/night tier scaling (§7.3) and `TierScale` multiply these bases as before.

### 7.1.1 Enemy Death Explosion

Every enemy bursts into its own voxel blocks on death — the classic buster from the legacy runtime
(`ExplodeModel`, now mirrored in `EnemyController`): each model block detaches, gains a collider +
rigidbody, and is blasted outward/upward with impulse + torque, then cleaned up after ~5 s. Purely
visual — no damage, no knockback, no chain reactions. Loot still drops normally before the burst.
Training dummies are `Immortal` and never die, so they never burst.

### 7.2 Points of Interest

- Towns (NPCs, shops, crafting)
- Dungeons (combat, loot)
- Boss arenas
- Fishing spots
- Farming zones
- Player housing plots
- Hidden caves and secrets
- Skill book locations

Fast travel points are **not** a placed POI in 1hz: `PoiKind.FastTravel` has no registry entry
(see §5.15, §6.4). The `✈` marker still keys off `POIDefinition.IsFastTravelPoint`, which every
roster POI sets, so the world map still annotates towns/dungeons/arena/fishing/treasure with it.

### 7.3 Day/Night Cycle

- 24-minute real-time cycle (configurable).
- Enemies become stronger at night.
- Some areas only accessible at night.
- Sleep/rest at bonfires to skip to morning.

### 7.4 Weather System

- Clear, Rain, Storm, Snow, Fog.
- Weather affects combat (rain reduces fire damage, fog reduces visibility).
- Some enemies only spawn in certain weather.

---

## 8. UI/UX Design

### 8.1 HUD

- HP/FP/Stamina bars (bottom left)
- Compass/map (top)
- Skill bar (bottom center, 6-8 slots)
- Minimap with chunk boundaries (toggle)
- Enemy health bars (anchored to each enemy's model head during combat — the bar height is measured
  per enemy from the model's highest renderer, not a fixed offset, so small enemies (slime, bat) don't
  get bars floating far above them; bars stay glued to the head while the enemy moves)

### 8.2 Menus

- Main Menu (New Game, Continue, Settings) — the **Multiplayer** entry and
  `MultiplayerBrowserUI` were removed in 1hz (§6.4)
- Pause Menu (Inventory, Skills, Map, Quests, Settings, Quit)
- **Skills menu** — one **giant radial skill tree** (hub + branching layers) per SkillCatalog category,
  built in code (no asset files), grouped into colored sectors (Melee / Ranged / Magic / Stealth /
  Crafting / Fortitude / Shield). On the standard PHYSICAL wheel each category fans out inside its
  own wedge: **Shield takes a small slice (~30°)**, the other four (Melee / Ranged / Stealth /
  Fortitude) share the remaining arc equally (~82.5° each), and ring positions re-spread across each
  wedge so every category's branches fill their sector. Pannable + zoomable. Nodes show state
  (selected / learned / available / locked); **connection links are black**, and a clicked node's
  direct parent→child links **light up white** so grouping is readable while idle. Class & Race tabs
  show each class/race's compact radial tree.
- Character Creation (race select + stat/passive preview)
- **Character Info tab bar** — the 5 top tabs (Info / Skills / Inventory / Map / Faith) hang from
  the canvas top edge as a **32-unit band whose top edge is 8 units below it** (was 84 tall at 36,
  then 40 at 10). The band is a later sibling of the panel body, so it **draws over** it — the
  panels' own top rows are therefore authored to live in the corridor *below* it: Skills' Skill
  Points / Learned readouts and the General / Class / Race sub-toggles share one row at y 236,
  between the band's bottom edge (260) and the skill-tree viewport's top edge (200). Band height,
  top offset and label inset are single named constants (`TabBarHeight` / `TabBarTopY` /
  `TabLabelInsetY`) read by both the build pass and the aspect-fit pass, and the tab label font is
  clamped to its own box so a 1440p+ window cannot push glyphs past the button border. Every button
  in the menu — including Change Class / Change Race and the Faith panel's Switch Faith — is drawn
  with the full `stats menu full button` frame, not the short default art.
- **Info tab value fields** — the 11 stat inputs and the class / race summary rows are **framed**:
  a 1.5-unit border drawn *inside* each field's own rect as four flat strips, with the field's text
  inset 4 so the border never sits on the first character. The class / race rows are 34 tall for
  that inset (a 22.5pt line does not fit the 22 that a 30-tall row leaves) and sit above their
  original y −96 / −130. A border is never a 9-sliced sprite here: Unity scales a slice by the
  drawn rect's own dimension, so one ring cannot serve both a 64-wide stat field and a 700-wide
  summary row.
- Race & Stat Sheet (current race, stats, skill XP, classes)
- Inventory Menu (equipment, items, materials, consumables)
- Map Menu (world map with biome overlay, POIs, player markers) — POI rows carry the `✈`
  fast-travel marker from `IsFastTravelPoint`; see §5.15 for why the feature behind it is gone.

### 8.3 Interaction Prompts

- Context-sensitive interaction UI (existing system, adapted)
- NPC dialogue system (simplified from CountryLife)

---

## 9. Technical Specifications

### 9.1 Engine

- **Unity 6** (6000.x, 2026) — the project was migrated from Unity 2022; Unity 6 changes already
  absorbed: `Object.GetEntityId()` replaces the now-obsolete `GetInstanceID()`, and
  `Physics.OverlapBoxNonAlloc` takes the results buffer before the orientation.
- Universal Render Pipeline (URP) for performance
- No Netcode / networking package; `com.unity.multiplayer.center` remains in `Packages/manifest.json`
  but nothing references it (1hz §4, §6.4)

### 9.2 Target Performance

| Platform | Target FPS | Render Distance |
|----------|-----------|-----------------|
| PC (High) | 60 fps | 16-32 chunks |
| PC (Low) | 30 fps | 5-10 chunks |
| Mobile | 30 fps | 3-5 chunks |

### 9.2a PC Render Configuration (1ea)

The active PC URP config — QualitySettings level 1 → `PC_RPAsset.asset` guid `4b83569d` with
`PC_Renderer.asset` — is tuned "take the FPS" (user directive, 1ea) over eye-candy:

- **SSAO — off** (renderer feature `m_Active: 0`). The stylized banded terrain barely reads AO; cost
  was a full-res pass per frame.
- **MSAA — off** (`m_MSAA: 0`); **opaque-texture copy and depth-texture passthrough — off**
  (`m_RequireOpaqueTexture: 0`, `m_RequireDepthTexture: 0`). No shader in the project samples
  `_CameraOpaqueTexture`/`_CameraDepthTexture`. HDR stays ON.
- **Shadows** — main-light map 1024, **2 cascades**, soft shadows off (`m_SoftShadowQuality: 0`);
  additional-light realtime shadows off (`m_AdditionalLightShadowsSupported: 0`, atlas 512);
  `shadowDistance 40 → 32` so the two cascades don't under-resolve to the horizon. Distant sun-shadow
  resolution is the accepted trade; the stylized look is otherwise intact.
- **Streaming maintenance is change-driven** (1ea): the collider ring re-reconciles only when the focus
  crosses a chunk boundary / a collider request changes / a chunk finalizes or unloads, with a
  4-collider-per-poll PhysX cook budget; the distance-cull visibility sweep runs as a rolling
  1024-chunk burst; dispatch
  sort/removal and modified-tile border checks are allocation-free / O(1) set lookups. An idle,
  fully-streamed world pays ~zero per-frame terrain maintenance.
- **Idle streaming is zero-cost end-to-end** (1ee): the stream loop keeps the 0.05 s poll beat (now on
  the 1gd decoupled coroutine clock, `StreamHz` 20),
  but the whole pipeline (`StreamAround` / dispatch / finalize / collider / prop sync) early-outs while
  the focus stays in the same chunk centre, nothing re-armed the world-dirty flag, and no chunk is
  queued / in flight / ready to finalize — an idle player pays only the poll timer check and a few
  comparisons. Walking streams normally the moment the focus crosses into a new 30 m chunk box.
- **Per-frame leaks removed** (1ee): every `GetComponent<CombatController>()` on the player root routes
  through the existing lazy `CombatCached` property (the 1dr cache convention); Tab open/close caches
  `CharacterInfoUI` instead of a per-press scene scan; the HUD day/time label is quantized to its
  displayed 0.01 h step so TMP stops repainting every frame; and the test-platform perf readout
  (`EnableFpsStats`) now defaults **on** so the 1ea/1ee baselines are visible without a tick.
- **Magic projectiles are render-only and static** (1eb): no exhaust `ParticleSystem` (there is no
  per-flight ParticleSystem simulation left in magic) and no per-frame `OrbFx` pulse on projectile
  children — flight costs only the `SpellEffect` behavior, and the impact crater-debris stays pooled.
- **Far shell at 900 m** (1ef → 1eo): the game bootstrap defaults render radius to **30** chunks (~900 m;
  **1eo** cut it from 67 / ~2,010 m — the user asked to shrink the loaded range, and the max clamp now
  equals the default) with real
  `ChunkObject`s only inside `NearRingRadius` 9 (`WorldStreamer.FarShell.cs`) — the open
  ground out to the radius is background-generated coarse cell meshes (see §2.5). Camera
  far plane **2200 m** (kept from the 2 km era; now just clears the shell with margin) and the terrain
  shader's **horizon tonal lift** (1600-2050 m, ~60% peak,
  `_HorizonColor`)
  sit **beyond the loaded world** at this default, so no fog — the near/mid terrain stays fully crisp.
- **Idle streaming stays zero-cost with the shell** (1ef): the 1ee idle gate's `working` flag now also
  covers the far-shell queues (`_farInFlight` / `_farReady` / `_farPending` / `_farUnloadBacklog`), so
  an initial far fill or a shrinking shell keeps the poll alive only until it settles, then an idle
  player pays the same timer check + comparisons as before.

### 9.3 Save System

- Chunks: one binary `.dat` per terrain chunk (`worlds/{seed}/tc_{x}_{z}.dat`), storing only
  locally-deformed tiles (§2.6)
- Player: JSON save file (stats, inventory, position, skills, world flags)
- Buildings: the `NightClub` entry is **no longer** in the essential-building restore list in
  `WorldBuilder.Persistence.LoadBuildingsFromSave`, so a pre-1hz save carrying one falls through
  to the generic path, where `SpawnBuildingDirect` returns `false` for an unknown type and the
  entry is skipped. Listing a type there without a matching `RebuildEssentialBuilding` case was
  worse than a compile error: the builder appended nothing and
  `_buildings[_buildings.Count - 1]` stamped the club's health/door/part state onto the
  **previous** building.

### 9.4 Source Layout (1iu, 1iy, 1iz, 1ja, 1jb, 1jd, 1je, 1jf, 1jl)

**The controller / modelling / animation split is planned in `ARCHITECTURE.md`** — read that for the
target layout and the staged migration. This section keeps only the load-bearing invariants.

There are **no namespaces and no `.asmdef`**, so a folder is an organisational unit only and moving a
file can never break compilation. What *can* break is a `.meta`, so every move carries one and the
invariant check is that the `.cs` count and the `.cs.meta` count under `Assets/Scripts` are equal
(365/365 after 1it, unchanged by 1iu, 1iy and 1iz — 1iz changed no code). Two trees were added:

```
Assets/Scripts/
  Magic/          Look/  SpellLook            - the ONE place a spell's look is resolved (§3.8.3)
                  Fx/    SkillFx, SpellImpactFx, CastingCircle
                  Cast/  SpellCaster (+4 partials), SpellData, SpellEffect, SpellZone,
                         SpellStorm, SpellSummon, SpellTornado, SpellBeam, SpellDoT
                  Ui/    MagicWheelUI, MagicTestMatrix
  Animation/      PlayerAnimator, WeaponAnimator, WeaponStowAnimator
  Combat/         StaminaSystem.cs
                  Feedback/  DamageNumber, HitStop, ScreenShake, CombatFeedback
                  Status/    StatusEffectType, BlindStatus, ChillStatus, WetStatus,
                             CCZone, ElementSignatureStatus
```

`Assets/Scripts/Magic/README.md` and `Assets/Scripts/Animation/README.md` are the in-tree maps. They
name **symbols, not line numbers**, deliberately: a line number is a copy of a fact that rots on the
next edit above it, and a README is the one file no tool in this repo checks.

1iz added `ARCHITECTURE.md`, which plans to split the three concerns the folders currently blur.
Three measured findings drive it, and one of them contradicts the obvious reading of `Models/`:

- **`Models/` is a procedural geometry factory, not a model folder.** `Legacy/MapBuilder/` (it was
  `Models/MapBuilder/` until **1jc**) is 10
  partials of one class referenced by **40 files** — the most depended-on symbol in the codebase — and it
  builds `BuildCloud`, `BuildTornado`, `BuildCafe`, `BuildPoliceCar` and `BuildPlayerHouse` beside
  `BuildPlayerModel`. **8** of the flat files in `Models/` are genuinely per-thing model builders and they
  have 1–6 referrers each; the spell-side answer is `Models/Magic/`, **11** files (1jd).
- **Weapon *visuals* live under `Combat/`.** `Combat/Weapons/` is 28 files of combat logic, three of
  which decide where a sword sits in a hand: `WeaponRigBuilder`, `WeaponRigHost`, and the magic weapon
  behaviours.
- **The magic models had no file — now fixed (1ja).** `BuildStaff` / `BuildHolyBook` / `BuildBoneWand` /
  `BuildControlOrb` were four **contiguous** methods at **L199-275** of the then-379-line
  `Models/WeaponModelBuilder.cs`, numbered `// 11.`–`// 14.` among fourteen melee/ranged/shield weapons.
  Nothing in the repo was named "magic model", which is why they could not be found. Extraction was
  blocked by one thing only: `MakeBlock` was `private static`. 1ja moved the four verbatim into
  `Models/Magic/MagicWeaponModelBuilder.cs`, widened `MakeBlock` to `internal static`, and made the **11**
  palette entries they use `internal` (imported via `using static`) rather than copying them — the
  **15**-colour palette **cannot move**, because the other fourteen weapons share it.
- **The magic *spell* models had no file either — now fixed (1jb).** Same complaint as 1ja, different
  subsystem, so it landed in two new files beside `MagicWeaponModelBuilder.cs`:
  - `Models/Magic/MagicProjectileModelBuilder.cs` — the **18** static methods that were inside the
    *spawning* class `SpellCaster.Projectiles.cs` (2 `Create` overloads, `Attach`, `Build` + its
    10-shape switch, 7 primitive helpers). `SpellCaster.Projectiles.cs` keeps `FireProjectile` +
    `DecorateProjectile`; the file is named after the caster, so geometry was never its job.
  - `Models/Magic/MagicImpactModelBuilder.cs` — the **8** `SpellImpactStyle` families, which were a
    32-statement switch inside `SpellImpactFx.ImpactFlash` alongside the pooling.
  Both moves are behaviour-preserving and were verified statement-by-statement against the pre-move
  source. **Only the impact one changed a signature**, and only because the builder had to return its
  output: it used to hand three *parallel lists* (`_materials`/`_parts`/`_spins`) back to a fade loop
  that indexed all three by the same number. It now returns one `Part` per piece (transform +
  material + spin flag), so index-alignment is unrepresentable rather than merely correct. The pool,
  the per-frame budget, growth, tumble and fade stayed with `SpellImpactFx` — the *lifetime* of an
  effect is behaviour, and belongs to whoever owns the effect.
  - **Two of the three categories the request named were not files, and that is the finding.** Magic
    *circles* already had `Magic/Fx/CastingCircle.cs`, and the inline ring code in `SkillFx`
    (`BuildVisual`/`SpawnZoneRing`) is per-instance behaviour, not a shape library. The skill models
    are `SkillFx.BuildRockBody` — but `SkillFx` also holds `RingFlash` (22 call sites) and `SlashFlash`
    (8), which are **shared hit-reaction FX for every weapon class**, not spell identities. Lifting
    the rock alone would leave that class still unnamed and still mixed, so it is recorded as the next
    candidate instead of half-done here.
- **The spell *effects* still had no file — fixed by 1jd, which also retired that "next candidate".**
  1jb's own note above pointed at `SkillFx` and the casting circle as the remaining unnamed geometry;
  both were real, and they were nine bodies rather than two. One named builder each, all in
  `Models/Magic/` except the projectile one:

  | Builder | Moved out of | What stayed behind |
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

- **The summoned *ally* was the one summon body left unnamed — fixed by 1je, which is an addition, not a
  move.** The table above covers every body `SpellSummon` builds, but `SummonedAlly` is a separate
  component and was still four lines of `GameObject.CreatePrimitive(PrimitiveType.Cube)` inside it. Same
  failure as 1ij/1iz/1jd one layer further out: the model existed and had been shipping; it had no name
  and no home, and it was reachable only from live combat (`SummonEffect.Execute` in `ClassEffect`, plus
  `RaceEffect` — 2 call sites), so there was no way to look at it without fighting something.
  `SummonModelBuilder.BuildAlly` now builds a six-part hovering construct (ground ring, capsule shell,
  sphere head, front core, two splayed shoulder pods) and returns `AllyBody { Root, Renderer[] }` —
  one record rather than six out-params, because the component's despawn alpha and death disable both
  went through a single `MeshRenderer` and a multi-part body needs the whole set or the head and pods
  stay standing while the shell vanishes.
  It **hovers rather than walks** because the component moves by `MoveTowards` + `Face` with no animator,
  rig or walk cycle, so a bipedal rig would slide; head top is 2.06 authored and **1.65 m** live under the
  component's existing `localScale = 0.8f`, which is why that line was left untouched. Trim (RGB x0.55)
  and core (RGB x1.15 clamped) are derived inside the builder from the one colour the caller passes —
  `SummonedAlly.AllyColor`, now a named constant, because `SummonEffect` passes no `SpellData` and the
  component hard-codes its tint. **No collider was added**: the cube's collider was destroyed at spawn
  and nothing replaced it, so the ally has never been targetable, and adding hitboxes would change combat.
  The class-level `[RequireComponent(typeof(SphereCollider))]` is added by `AddComponent` and never
  removed — a pre-existing contradiction, left alone. Reading it needed an acceptance lane, so
  `NewWorldTestGround` grew `EnableSummonModels` + `SpawnSummonModels` (key **Numpad1**, three pedestals
  at `z = PlatformCenter.z + 11f`): the other two summon bodies ride along, because until now none of the
  three had a readout. See `PROGRESS.md` §1je.
- **The game opens in third person (1jf).** `CameraModeSwitch` is the camera owner, and it is the class to
  read for the view — **not `Player\Controller/ThirdPersonCamera.cs`**, which is a dead class: one
  declaration, **zero** code references (its only mention anywhere is a comment in `ScreenShake.cs`) and
  its `.meta` GUID is referenced by **zero** assets. The live path is `PlayerController.Camera`'s
  `SetupPlayerCamera`, which `AddComponent`s a `CameraModeSwitch` and calls `Setup` — so **the spawn view
  is the C# field default and nothing in the scene overrides it** (grep: `StartInFirstPerson` has exactly
  one reader, `CameraModeSwitch.OnEnable`; 1jf flipped it to `false`). F5 still toggles both ways, so this
  is a *spawn* choice only. `CurrentMode`/`IsFirstPerson` have **zero** external readers, which is what
  makes the flip self-contained: the mode is a render-side switch with no gameplay consumer, so no other
  system needed changing and none should have been reading it.
  Framing kept as authored: 6.5 m behind, camera at `ThirdPersonY = 2.6` m above the player's **feet**
  (not above the pivot — `UpdateThirdPerson` adds `up * (ThirdPersonY - pivot.localPosition.y)` to the
  pivot's *world* position, so the two cancel), looking at the 1.5 m pivot **plus the 1jl lateral offset**.
  Player-model layer 6 is culled in first person only; arms stay visible in both.
  - **The third-person camera sits over the player's right shoulder (1jl, widened 1jo).** `CameraModeSwitch` gained
    `ThirdPersonSideOffset` = **0.9 m** (negative = left, `0` = the pre-1jl centred look). 1jl shipped 0.6 m;
    1jo widened it to 0.9 m at the user's request. It is added as
  `pivot.right * offset` to **both** the camera position **and** the look-at point, so the view direction
  is byte-for-byte the pre-1jl one and the only thing that changes is where the character sits on screen —
  left of centre. Offsetting the position alone would have moved nothing visible: the camera would simply
  rotate to keep re-centring the pivot. First person is untouched (it snaps to the pivot and never enters
  `UpdateThirdPerson`), and the field is a plain initializer with no scene override for the same reason
    `StartInFirstPerson` is (grep: 0 hits in the one live scene).
    Raising the offset cannot bend a shot: the projectile aim is a *direction* taken off the look pivot
    (1jo), never the camera's position, so this field is framing only.
  The collision `SphereCast` still starts at the pivot, so the lateral term is now inside the direction it
  sweeps — a wall beside the player pulls the camera in, which it did not before.

  The dividing line is **shape vs. lifetime**: everything that only builds transforms moved; everything
  that decides *when a piece moves next frame* stayed, because that is behaviour. `SkillFx`'s
  `SlashFlash`/`RingFlash` keep their public signatures — 22 and 8 call sites, mostly non-spell — so the
  move was one level in, not a rename. Four of the nine needed a return record (`LineBody`, `TipOrb`,
  `Circle`, `Piece`) rather than a builder that remembers its last ring, which would be a second owner
  of a transform; `SpellBeam`'s tip orb is shared by the cone **and** the line, so it stayed outside the
  branch rather than being folded into the line builder. Verified with `tools/Compare-MovedModel.ps1`
  (**24** moved blocks, **24** identical literal streams, **8** declared literals hoisted to a named
  field or moved to the call site), whose `-Mutate` control was confirmed able to go red — rule 17's
  comparator, because with no compiler here the comparator *is* the compiler.

The two in-tree READMEs above and `ARCHITECTURE.md` all describe **symbol ownership** — which class owns
which behaviour — and no generator can derive that, so they stay hand-written. **Structure**, though, is
derivable, so 1iy made it a generated file
rather than a fifth hand-maintained map: `TREE.md` is written by `tools/Write-Tree.ps1` from
`git ls-files`, names the commit and timestamp it saw, and states on its first line that it must not be
hand-edited. It omits the 634 `.meta` files (54% of the repo by count), collapses `_ArtSource`,
`Resources`, `TextMesh Pro` and `ProjectSettings` to `[N files]`, and always expands `Assets/Scripts`
(370 non-`.meta` files — **368** `.cs` + 2 `README.md`; note its own header's "C# files | 370" is a
*different* 370, counted repo-wide and so including the two vendored
`Assets/TutorialInfo/Scripts/` files). Verified by rebuilding all **541** non-`.meta` paths out of the
rendered tree and diffing against `git ls-files`: **417** rendered leaves + **124** inside the four
collapsed directories = 541, with no invented paths and nothing uncovered. That verifier carries two
controls — a bogus path that must read absent and a real new file that must read present — and was
confirmed able to go red by withholding one real path on purpose, because a check nobody has seen fail
is not a check (AGENTS rule 7/8). It deliberately asserts **no design or process claim** — it points
here and at `AGENTS.md` / `PROGRESS.md` / `THINKING.md` rather than duplicating them.

Three placement decisions worth stating, because the "obvious" answer differs:

- **`Models/WeaponModelBuilder.cs` did not move into `Magic/`.** It builds **18** weapons,
  four of which are magic (`BuildStaff`, `BuildHolyBook`, `BuildBoneWand`, `BuildControlOrb` in its
  dispatch), so the magic four could not be separated by a path move without splitting the file — which is
  what 1ja did, and is the reason `Models/Magic/` exists beside it rather than instead of it. Same
  reasoning keeps `MagicWeaponBehavior` / `MagicWeaponMods` in `Combat/Weapons/`: they are a
  `WeaponCategory`, driven by `WeaponData` (§3.6), not part of the spell pipeline.
- **`PlayerController.Animation.cs` did not move into `Animation/`.** It is a
  `partial class PlayerController`, so it stays with the class it *is*. The test applied: group
  **independent components** (the three animators each stand alone), and leave a class's partials
  with their class.
- **Spell geometry is split by *when* it is drawn, and as of 1jb by *what draws it*.** The
  in-flight bodies were in `SpellCaster.Projectiles.cs` (`CreateProjectileDisplay` /
  `BuildProjectileBody`) and are now `Models/Magic/MagicProjectileModelBuilder.cs`, which is where the
  caster's two remaining call sites reach them. The falling rock and its SkyRock styles are still in
  `SkillFx.cs` (`FallRock` / `BuildRockBody`) — a *skill* model, deliberately not swept up by a stage
  about spell models (see the `SkillFx` note above). The projectile bodies are on the
  `MagicTestMatrix` bench (§3.8.3), which is why a change to either has an acceptance readout.

### 9.4a Source art lives outside `Assets/` (1iv)

90 files / 34 MB of source art moved to a repo-root **`_ArtSource/`** (`model/`, `UI component/`,
`texture/`, `xoanvnmexel/`). Outside `Assets/` Unity does not import them, so they cost no import
time, while git keeps them. Each moved asset's `.meta` travelled with it, so a folder dropped back
into `Assets/` keeps its GUIDs. Their former **folder** metas were deleted rather than moved: Unity
cannot resolve a folder GUID for a folder it cannot see.

**Four files stayed in `Assets/` because they are live, and the scan is what proved it** - the folder
names are the worst possible evidence, since all four sat in folders that look like pack junk:

| Kept | Why |
|---|---|
| `Assets/texture/{dirt_texture, fertilize, peashooter_seed}.png` | referenced **exactly once each** by `Assets/Scenes/SampleScene.unity`, the only scene in `EditorBuildSettings`, as the named fields `FieldTexture`, `FertilizerTexture` and `PeashooterSeedTexture` |
| `Assets/xoanvnmexel/XoanVnmexelStandard.ttf` | the source font of `Resources/VietPixel.asset` (`m_SourceFontFileGUID`), which `UiAssetCache` loads as the shared default UI font and TMP's own default font asset references |

So `Assets/texture/` is now three files rather than twelve. `dirt_texture.png` is **also** a
byte-identical duplicate of `Resources/texture/dirt_texture.png`, and both copies are live (the
scene holds one GUID, `Resources.Load` the other), so that pair was left alone - deduping it means
re-pointing a serialized GUID inside a scene file by hand, which is not reviewable without opening
the editor (rule 3).

Three further files were byte-identical to their `Resources/` twins *and* referenced by nothing, and
were deleted outright rather than archived: `grass_blade.png`, `leaves_texture.png`,
`wood_texture.png`.

### 9.4b `MapBuilder` is 10 files, and it is load-bearing (1iw)

`Assets/Scripts/Models/` held `MapBuilder` as 10 flat `MapBuilder.*.cs` partials - 5,153 lines and
**10 of the folder's 17 `.cs` files (59%)**. 1iw grouped them into
`Assets/Scripts/Models/MapBuilder/`, leaving `Models/` holding the single-purpose model
builders it always meant to hold (`Boss`/`Enemy`/`Goblin`/`Horse`/`Item`/`PlayerPartMesher`/
`WeaponModelBuilder`, plus **`WeaponProjectileModelBuilder`** from 1jd). **1jc** then moved that folder -
unchanged, GUIDs intact - to
`Assets/Scripts/Legacy/MapBuilder/`, because `MapBuilder` is old-game content (see §9.4c).

The grouping is cosmetic - C# does not care about folders, and the class name is unchanged, so no
caller was edited. **But the files are not unused, and must not be deleted on the strength of their
old location.** The consumers that make that concrete:

| Consumer | Calls |
|---|---|
| `Player/Races/RaceRig.cs`, `PlayerController.Animation.cs`, `Animation/PlayerAnimator.cs` | `BuildPlayerModel` |
| `Interactions/PlayerSitController.cs` | `BuildSitPlayerModel` |
| `Magic/Cast/SpellTornado.cs`, `SpellBeam.cs`, `SpellCaster.Cast.cs` | `BuildTornado` |
| `World/Chunks/ChunkObject.cs` | `BuildTree`, `BuildStone` |
| `Quests/RandomEventManager.cs` | `BuildCloud`, `BuildTornado` |
| `Pets/PetController.cs` | `MakeBlock` |
| `UI/UIManager.cs` | `RefreshWorldSignTexts` |
| `CutsceneManager.Ending*` (9 files), `WorldBuilder.*` (6), `NewWorldTestGround` | the village, cars, NPCs, benches |

The one that is easy to miss is `ChunkObject`: **the streamed terrain draws its trees and stones
through `MapBuilder`**, so the class is inside the new world's critical path, not just the legacy
village's model kit. Nine of the ten partials are buildings and NPCs that only the village and the
cutscenes use, so if the legacy content is ever retired those partials become genuinely dead - but
`PlayerModels.cs` (races, sitting) and `Nature.cs` (`BuildTree`/`BuildStone`/`BuildCloud`) would have
to be kept or promoted out first.

---

### 9.4c `Scripts/Legacy/` — the old game, quarantined read-only (1jc)

The old game's content is **32 files** in `Assets/Scripts/Legacy/`, and it is **not** part of this game:

| Folder | Files | Owns |
|---|---|---|
| `Cutscenes/` | 11 | `CutsceneManager` + the ten `Ending*` / `Driving` / `Helpers` partials (§6.2) |
| `WorldBuilder/` | 12 | the old block-built village: blueprints + part builders, persistence/save keys, NPCs, farming, mining, lights |
| `MapBuilder/` | 10 | the old prop kit: houses, mansion, nature, NPCs, police, restaurants, stores, vehicles (§9.4b) |

The move was `git mv` with every `.meta` travelling along, so **GUIDs are unchanged** and every
serialized reference still resolves — `Assets/Scenes/SampleScene.unity` (the only scene in
`EditorBuildSettings`) still carries a `WorldBuilder` and a `CutsceneManager` component, so the old game
still boots. `Assets/Scripts/Legacy/README.md` carries the same contract at the folder level, and
**AGENTS.md rule 18** is the authority: read-only in both directions, no new code here, no legacy
symbol resurrected outside.

**This is a fence, not a cleanup.** Nothing is deleted, and the legacy types are the *hub* the new
systems hang off — 23 live files name `WorldBuilder`, 15 name `MapBuilder`, 11 name `CutsceneManager`
(`GameManager`, `ToolManager`, `SaveManager`, `GoblinPet`, `PetController`, `ChunkObject`). Live code
calling into `Legacy/` is normal; the forbidden direction is legacy gaining a **new** dependency on live
code. If a task needs a legacy behaviour, it gets a new class outside the folder.

Consequence for tooling: `tools/StaticChecks.ps1` had four `Assets\Scripts\World\WorldBuilder*.cs`
paths, so rule 3's only instrument **died on `Resolve-Path` after the move and no check reported it** —
the only way it surfaced was running the script (AGENTS.md rule 18). It now reads the `Legacy\` paths on
purpose: checks 2/3/6 are the part-key parity guard (rule 9), and **a finding inside a `Legacy` file is
a report of a rule-18 violation, not a fix queue**.

---

## 10. Monetization (Future Consideration)

- No pay-to-win.
- Cosmetic-only microtransactions (skins, emotes).
- Expansion packs (new biomes, classes, story content).
