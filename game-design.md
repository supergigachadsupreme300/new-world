# Game Design Document — "New World" (Working Title)

## 1. Game Overview

**Genre:** Open-World Action RPG (Elden Ring-inspired)
**Platform:** Unity (PC primary, Mobile secondary)
**Multiplayer:** Dedicated server with co-op/invasion/arena
**Core Loop:** Explore → Fight → Grow → Craft → Dominate

Seamless open-world with real-time action combat, classless progression via a **6-category skill-XP system**, an **11-stat** system, **15 unlockable classes**, and a **22-race system** (with passive-only racial kits), procedurally generated seed-based chunk terrain, and all existing CountryLife systems retained as optional side content. Combat is built on a **3-genre equipment** set (21 slots), an expandable **weapon architecture** (§3.6, Melee/Ranged/Magic), a **spell-casting pipeline** (§3.8) for magic, and **10 damage types** with **7 status effects** (§3.7).

---

## 2. World Generation System

### 2.1 Seed & Coordinate-Based World

- Every world defined by a **numeric seed** (long).
- World infinite in XZ plane, divided into **1x1 unit chunks**.
- Each chunk identified by **(chunkX, chunkZ)** integer pair.
- Same seed + coordinate always produces identical chunk (shared worlds on dedicated server).

### 2.2 Chunk Structure (4 Triangles, Heightmap)

Each chunk: **5 vertices** (4 corners + 1 center), split into **4 triangles** by X-diagonal.

```
C1─────────C2
 │ ╲  T1  ╱ │
 │   ╲   ╱  │
 │ T4 ╲╱ T2 │
 │     CE    │
 │ T3 ╱╲    │
 │   ╱   ╲  │
 │ ╱       ╲│
C3─────────C4
```

- **Corner vertices** shared with adjacent chunks (deterministic, never recalculated) → **zero gaps**.
- **Center vertex** unique per chunk, influenced by corners + noise.
- **Random angle pivot** applied to center vertex position offset for organic feel.
- **Strict edge matching:** edges computed from shared world coordinates → guaranteed seamless stitching.

#### Triangle Connectivity Rules

Each chunk has **5 vertices**:
- 4 corner vertices: shared between adjacent chunks (deterministic based on world coordinates)
- 1 center vertex: unique to the chunk
- 4 triangles: Top-Left, Top-Right, Bottom-Left, Bottom-Right

**No gaps allowed.** Edge vertices are deterministic based on world coordinates, guaranteeing seamless stitching between any two adjacent chunks regardless of load order.

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
  full-fidelity `ChunkObject`s — deformable, collidable, prop-bearing, LOD'd. `StreamAround` receives
  the NEAR ring, not the render radius, and keeps one hysteresis ring (near+1) loaded, so the real
  chunk world is 361 chunks (was 3,721 at radius 30) and the LOD/collider/prop wins of 1dq/1di/1e6 ride
  a fixed-size ring instead of scaling with the render distance. Dispatch is **nearest-first and each
  chunk never regenerates** (**1em**: the dispatch loop had no loaded-chunk guard and its cleanup only
  dropped entries that were BOTH pending AND loaded — but finalize clears the pending mark, so
  finalized chunks were re-dispatched forever, refilling the in-flight slots with the same nearest
  chunks and starving the rest of the ring into a permanent ~24-chunk bubble around the player; the
far shell was immune because it skips completed cells, which is why 300 m→~900 m rendered while the
   0-300 m disc stayed empty).
- **Far shell (1ef):** from ring near+1 out to the render radius, `WorldStreamer.FarShell.cs` covers
  the ground with one coarse **cell mesh** per aligned span block — level-of-detail sectors generated on
  the ThreadPool from the SAME per-chunk corner grid the real chunks use (save stamps + noise), so the
  map stays watertight and shares the real ring's seam exactly. Cells: **span-1** rim cells (3 m step)
  at rings 10-14 own the loaded/unloaded **active shadow** (inactive under a real ring-10 chunk, active
  the same poll it unloads — zero hole, zero z-fight), **span-3** cells (rings ≥15, 90 m wide) and
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
   keeps the idle gate busy), with **chunk save files written on a background worker** (§2.6). Cells are dispatched **near-first**
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
  hard clamp of **160** (1ef) — and the LOD cull distance auto-matches the current render radius so
  culling never fights the visible ring (far cells are static, not LOD-registered, so the cull budget
  still scales with the REAL near ring).
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
- **Chunk LOD (1e6):** every chunk now grows two decimated **child meshes**, `Lod1` (every 2nd tile
  corner → ~1/4 the triangles) and `Lod2` (every 3rd tile → ~1/9), sampled from its own merged
  top-terrain block. `ChunkLodManager`'s bands (0-30 m full / 30-60 m `Lod1` / 60 m+ `Lod2`, cull
  beyond the streamed radius) now actually switch between them: the **root `MeshRenderer` is disabled
  while a detail band is active** (before this fix the root stayed enabled and every distant chunk
  drew its full ~1800-tri mesh *plus* the detail). The children are built **lazily** (only when a
  band first selects them) and marked stale by every `ApplyMerged`/`PatchRegion`, so a band switch
  refreshes the decimated grid from the current terrain first — deformation never renders a
  pre-excavation hole, and near-band chunks never pay for LOD at all. Physics is untouched (the
  collider lives on the root and rides the full mesh, §2.5 collider-on-demand).
- **Transient-object pooling (1e6):** the generic `ObjectPooler` (Phase 9, previously unused) is now
  live on the boot root and backs the high-churn cosmetic spawns — spell **impact VFX** (both the
  projectile-impact and direct-hit paths) and the **excavation debris** burst from `SpawnCraterDebris`.
  `ObjectPooler.SpawnTransient` uses the pool when present and falls back to plain
  `Instantiate`+`Destroy` otherwise; pooled particle effects replay from frame 0 on reuse (`Clear`+
  `Play`). Debris cubes and impact effects are fully rewritten on every use (position/scale/material/
  velocity), so pooling is invisible apart from the allocation drop. Enemy death debris (the model
  parts themselves) and loot drops are deliberately **not** pooled — they are structural/persistent,
  not transient clones.

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
  body (the exact live-cast visuals via `SpellCaster.CreateProjectileDisplay`; Comet/Earth Meteor show
  the summonFallingRock boulder, zone/beam/storm/summon/instant spells show their school-colored default
  icon) + a world-TMP label — pure visuals (no colliders/interaction) so each spell's magic model can be
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
- **Teleport routing:** every intentional teleport goes through `PlayerController.TeleportTo`
  (spawn/respawn, fast travel, sleep, load-game, test-platform entry), which stamps the destination as
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
- **LOD + far shell follow the mode (1eu):** voxel chunks build stepped **LOD children** now
  (`BuildVoxelLodChild` — a decimated coarse full-size column grid feeds the same pooled-LOD child
  mesh), and the far shell's `BuildFarSector` gains a stepped `BuildVoxelFarSector` twin that samples
  the same deterministic integer column tops on its 3 m grid — no more smooth far disc around a stepped
  world.
- **Known limits (documented):** the mesher's side-wall pass reads only the topmost run, so interior
  cavity side walls are NOT rendered — the rim of a carve reads as a slot into the void until per-run
  side-wall meshing lands. `ChunkSync` network sync of voxel edits is deferred; two perpendicular walls
  with different drop sizes can crack cosmetically at a 90° step corner.

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

- **Layer 1 — `WeaponData` (ScriptableObject, data-only).** Shared fields: id, display name, weight (equip-load), Str requirement (weight class, §5.5), hand usage (single / dual / two-hand), base damage, speed, attack reach, scaling stat(s) + coefficients, `WeaponCategory`, `DamageType` (one of the 10 damage types, §3.7), and a Weapon Art reference. **Magic weapons** additionally carry magic mods — `MagicDamageMult`, `CastTimeMod`, `CooldownMod` (staff/wand/book scale spells). **Shield weapons** additionally carry guard mods — `BlockAbsorbPercent` (fraction of a blocked hit absorbed) and `BlockStaminaDrainMult` (multiplier on per-hit block stamina cost).
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
  watchdog force-releases a hung arm-owner claim as a backstop.

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
  along the cast).
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
- **summonFallingRock** (sky spells: a big rock drops from the sky onto the target and the burst
  resolves on landing — see §3.8.1 Delivery Behaviors "Sky spells")
- **projectile shape** (`ProjectileShape`, §3.8.1): the *visual* built for a Projectile-delivery
  spell. When a spell leaves it `Auto`, `SpellCaster.AutoShapeFor` picks the school default; every
  bolt/lance/blade/spear-named spell sets it explicitly so projectiles read as their name.
- **terrain shape** (Earth school signature, §3.8): an optional `TerrainShape` reshapes the tiled
  heightmap before damage resolves — as **smooth feathered per-corner edits**, never flat blocks.
  **Ring** rears a raised annular wall around the impact, **Spikes** erupts rock spikes beneath it,
  **Wall** rears an elongated ridge along the cast direction (~2.6 m on a first cast, tall enough to
  fully block the player's CharacterController), **Pillar** thrusts a tall column up at the center,
  and **Crater** excavates a smooth dish. Heights are written as continuous per-corner
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
  WorldStreamer's ±200 m mesh-safety sanity band. Vertex colors painted at build time then reveal the dug depth
  below the pristine noise surface as discrete strata bands: **grass (surface) → dirt (~0.65–2.3 m
  down) → stone (≥ 2.7 m down)**, small blends between bands (1cs). The shovel can only dig the
  soft bands and stops at stone; the pickaxe excavates at any depth. Every crater is a genuine
  smooth dish — corners keep their own slope, the rim feathers out — and it is permanent (1cs).
  **Excavation ejects debris matching the stratum it just reached (1de):** `WorldStreamer
  .SpawnCraterDebris` pops 3–5 physical cubes out of the fresh dent — dirt blocks (dirt-brown) while
  the floor digs through grass/dirt, rock (grey, the same look as pickaxe rock destruction,
  `WorldBuilder.SpawnRockDebris`) once the pit reaches the stone band — tinted by the same
  `TerrainBandColor` the pit walls render and destroyed after ~2.5 s so repeated digs never litter.
  Only a Crater throws debris; the raised shapes never do.
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
  taller ridge along the cast) deform at the aim point via `ResolveZone`; Storm strikes
  (Rockfall → Crater) dent under each boulder via `SpellStorm.DeformGround`; Summons (the golem
  line → Spikes) erupt a small rock field where the construct rises via `ResolveSummon`; the
  Projectile root (Stone Shard) carves its crater at the impact point. Because raised shapes cap
  and only craters excavate, no raised shape — zone, storm, summon, or projectile — can ever stack
  unbounded, and craters dig as deep as the player has patience for.
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
target. A **Crater** is the deliberate inverse (see the terrain-shape bullet): each cast/swing lowers
the floor one `CraterStep` below its current height, so excavation is bounded only by the mesh-safety
sanity band. Two robustness fixes ride along: `SpellCaster`'s zone aim probe skips **raised terrain taller than
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

**Sky spells** (`SummonFallingRock`, the meteor/boulder family) summon a **big rock** that drops from
high above the ground target and reads as the spell landing: the burst (damage, knockback, terrain
deform) is deferred until the rock hits the ground (Zone deliveries ~0.6-0.8 s drop; Storm strikes
drop a smaller rock per strike and fire their flash/damage/deform on landing; the meteor-line Comet
projectile flies as a rough burning boulder). Built by `SkillFx.FallRock` — a collider-less visual
(never triggers the knockback-terrain-root bug 1cx), self-destroying, shards + ring flash on impact.
Spells: Fire Meteor, Asteroid, Earth Meteor, Comet, Meteor Rain, Rockfall.

Fifth, **Projectile Shapes** — projectile visuals are split into named shapes rather than one element
color swap, so each spell looks like its name and not a recolor of the same ball. Since `1ec` every
body is a **voxel cube-cluster**: a front-leading cube in the school color with progressively
**smaller, darker cubes stacked behind it** (a bright hot core fading into a tapering square tail),
built once and fully static (no sphere meshes remain on projectiles):

| Shape | Rendered as |
|---|---|
| **Bolt** | Jagged 8-segment cube chain along the flight axis (already a cube chain tapering 0.17→0.05, the same segment technique as the thunder-storm event's `SpawnJaggedBolt`) — used by every spell with "Bolt" in the name: Frost Bolt, Chain Lightning, Dark Bolt, Volt, Fork/Leap/Arc/Volt Bolt, Fury Bolt, Shadow/Doom Bolt, Void Rend, and the class-flavored Arcane Bolt. |
| **Sphere** | Hot voxel orb: a 0.24 lead cube + 4 jittered cubes shrinking to ~0.05 behind it, each darker — the Fireball and every generic orb. (Scorch/Burn/Comet use the Comet shape instead.) |
| **Shard** | Translucent glass lead chip (45° diamond) + 2 smaller, dimmer glass chips trailing — frost chips (the Ice school default; Chill Touch). |
| **Debris** | Clustered grey rock cubes (mixed sizes, random rotations, one leading chunk) — the Earth school's Stone Shard. Dressed like the world's breakable-rock debris (`Color.Lerp(gray, black, rand)` cubes) with two chunks dusted in the earthy tan accent so it reads as magic; a short debris burst also kicks out of the crater at impact. |
| **Lance** | Long straight pointed spike (shaft + tip) with two small trailing flecks behind its tail — Ice Lance, Frost Pierce, Glacial Impale. |
| **Spear** | Tapered spear: dark shaft + broad diamond head + trailing flecks behind — Shadow Spear. |
| **Blade** | Flat translucent cross-blade (alpha ~0.4 so wind reads as a ghost of air) + two small ghost cubes trailing — Wind Blade, Razor Blade, Wind Scissor, Laceration. |
| **Splash** | Water drop cube + a trailing splash of 3 smaller, darker cube drops — Water Bolt, Tidal Surge. |
| **Comet** | Small voxel core cluster + a fading streak tail cube — Scorch, Burn, Comet, Frost Bite. The meteor-line **Comet** (`SummonFallingRock`) trades the cluster core for a rough **burning boulder** + chunks + tail, so it reads as a rock tearing through the sky. |
| **Missile** | Three 2-cube mini dart-stacks; **homing** — `SpellEffect.UpdateMissileTargeting` probes the **current trajectory** every frame and prioritizes the target on the flight path (the foe it is about to fly into), otherwise keeps chasing the locked target's last spot (or locks the nearest foe ahead if never locked), steering smoothly at 240°/s so the flight bends; no target = flies straight. Arcane Missiles, Chill Soul. |
| **Dart** | Sleek thin bolt-line with a tip + small trailing fleck — physical shots (Archer Wind Shot, Taoist Talisman). |

`Auto` resolves per school: Fire→Sphere, Ice→Shard, Lightning→Bolt, Wind→Blade, Water→Splash,
Earth→Debris, Physical→Dart, everything else→Sphere. Builders live in `SpellCaster.BuildProjectileBody`
(cube primitives only, via the `Cluster` / `AddTrailingFlecks` helpers), colored per damage type;
**since `1eb` the body is fully static — no exhaust particles and no in-flight pulse** (the old `OrbFx`
scale-pulse/spin modes and the `AttachProjectileParticles` exhaust `ParticleSystem` were removed), so
projectiles in flight cost only their `SpellEffect`; turret summons render the projectile through the
same call (`SpellSummon` passes the turret spell's shape). Translucency (Wind/Ice) is set via
`material.color.a` and relies on the `"Sprites/Default"` shader blending (the `"Unlit/Color"`
fallback would render opaque).

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
- While aiming/charging, the held **magic weapon shows a "casting circle" halo**: a translucent disc
  beneath the tip plus an outer ring and a spinning inner rune ring wrapping the weapon, ramping its
  radius, brightness, and spin speed with charge level and tinted by the **armed spell's element**.
  Releasing the cast pops a one-shot expanding ring at the weapon. (`CastingCircle.cs`, driven by
  `PlayerController`; split aim → charge → release is used by both magic and ranged.) Unarmed casts
  still play a plain hand glow instead of the halo.
- Projectile spells launch **from the casting circle's center**: the spawn point sits on the aim line
  at the rig/hand origin (a small forward muzzle offset only, no vertical lift), so the flight
  trajectory passes through the circle's heart. The pre-cast **path preview** mirrors the exact launch
  (`SpellCaster.FireProjectile` ↔ `PlayerController.UpdatePathPreview` share the same origin math).
- Zone/vortex spells additionally show a **ground AoE preview** ring that also grows with charge.
- Projectile deliveries (magic **projectile** spells, and ranged draws — regular and per-hand dual) show a
  **flight-path cone** while charging: a stack of translucent rings from the hand along the aim line that
  **narrows as the charge builds**, collapsing to a thin centre ray of the exact predicted trajectory at
  full charge, and clipped at the first solid hit. Magic previews are tinted by the spell's element;
  ranged previews are tinted by shot type and spread outward with low accuracy. (`ProjectilePathPreview.cs`,
  driven by `PlayerController`; hidden on cancel/release/weapon switch.) Ranged weapons with no projectile
  prefab fire a runtime-generated arrow instead of a hit-scan tracer.

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

## 4. Multiplayer System (Dedicated Server)

### 4.1 Architecture

- **Dedicated server** runs the authoritative world state.
- Players connect as **clients**.
- Server handles: chunk generation, enemy AI, loot drops, world state, anti-cheat.
- Client handles: input, rendering, audio, local effects.

### 4.2 Multiplayer Modes

| Mode | Description | Players |
|------|-------------|---------|
| **Solo** | Play alone on a server (local or remote) | 1 |
| **Co-op** | Invite friends to your world | 2-4 |
| **Invasion** | Hostile players enter your world to fight | 1-6 |
| **Arena** | PvP duel zones with matchmaking | 2-8 |
| **World Boss** | Open-world bosses with multiplayer participation | 4-16 |

### 4.3 Networking Requirements

- Chunk synchronization (server generates, clients receive height data).
- Player position/action synchronization.
- Enemy state sync (AI, health, attacks).
- Loot synchronization.
- Chat/text communication.
- Matchmaking and session management.

### 4.4 Anti-Cheat

- Server-authoritative damage calculation.
- Position validation (no teleport hacking).
- Action rate limiting.
- Chunk data integrity checks.

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
  **church** is 16×13 (~20 tall, 13 parts: nave + arcade columns, gothic side windows + rose window,
  gold-cross apse, gabled nave roof, tall front steeple tower with belfry + gold spire + cross, stepped
  buttresses, interior pews/pulpit/altar); **shrine** is 14×12 (~14 tall, 12 parts: two-tier pagoda-style
  hall — tiled tier-1 roof, upper tier floor/balustrade + tier-2 roof — topped by a jewelled gold spire,
  with a yin-yang back wall, deity statue, offering altar, and a large tripod incense censer at the
  entrance). Exclude-radii were raised (church 15, shrine 14) and the worship NPCs stand in front of
  each entrance (priest west of the church, taoist south of the shrine).

### 5.8 NPCs & Relationships

- **Jessica** (the neighbor girl): befriend via gifting, romance, and **marry** her — `WifeNPC`
  (marriage gated at **day 5**; wife lives in the mansion, dialog + per-day events).
- **Phú Ông / The Rich Man**: guards a secret behind the mansion — stake out at night and
  **report to the police** (story quest "Bí Mật Của Phú Ông").
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
- Buildings: house, mansion, restaurant, café, library, night club, watchtower, walls (plus the pagoda).
- Player homes can be built/decorated; **chests** for storage (`ChestStorageManager`), with
  **crafting stations** and **farming plots** attached.
- Walls and watchtowers defend the farm; **storms/earthquakes/tornadoes** (events) can damage buildings.

### 5.11 Economy

- Gold (🪙) is the unit; earn by selling produce, fish, quest rewards, and restaurant/café income.
- **Vendors and shops** in towns (`VendorShopManager`, `BuffaloShopManager`) with buy/sell tabs
  and price multipliers.
- Player trading intended via dedicated server (`§4`).

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

- **Fast travel**: road **signs** (`FastTravelSign`) open `FastTravelMenu` (scrollable list).
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
> than deleted. Legacy code copied out of use lives in the **project-root `_Archived/`** folder
> (`WorldBuilder/`, `CutsceneManager/`, `Quests/`, `Enemies/`, `README.md`) — not under
> `Assets/Scripts/_Archived/` (that path is empty).

### 6.1 World Builder (Voxel Cube System) — **legacy generation disabled, content retained**

- `WorldBuilder.EnableLegacyGeneration` defaults to `false`, replacing the legacy finite voxel map
  with the chunk-based terrain system (§2).
- The WorldBuilder **content systems remain in use**: its blueprint list powers the build menu
  (§5.10), and its farming fields/pagoda are live.
- Legacy generation code is archived at `_Archived/WorldBuilder/` for reference/re-implementation.

### 6.2 Endings System — **implemented but gated**

- **8** cutscene endings exist (`EndingHappy`, `EndingSad`, `EndingFated`, `EndingDemon`,
  `EndingJustice`, `EndingNTR`, `EndingBlackmail`, `EndingBossBad`) with the **Ending Tree** UI
  wired in the main menu.
- They are **disabled at runtime**: `CutsceneManager.RemoveEndings = true` short-circuits every
  ending entry point to `EndingsRemoved`. The shipped game therefore plays as an ongoing
  open-world RPG; flip the gate (or delete the early-return) to re-enable endings.
- Cutscene helpers/road-driving partials remain active; reference copy at `_Archived/CutsceneManager/`.

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
- Fast travel points
- Fishing spots
- Farming zones
- Player housing plots
- Hidden caves and secrets
- Skill book locations

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
- Multiplayer indicators (player names, health bars)
- Enemy health bars (anchored to each enemy's model head during combat — the bar height is measured
  per enemy from the model's highest renderer, not a fixed offset, so small enemies (slime, bat) don't
  get bars floating far above them; bars stay glued to the head while the enemy moves)

### 8.2 Menus

- Main Menu (New Game, Continue, Multiplayer, Settings)
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
- Race & Stat Sheet (current race, stats, skill XP, classes)
- Inventory Menu (equipment, items, materials, consumables)
- Map Menu (world map with biome overlay, POIs, player markers)
- Multiplayer Menu (server browser, friends, party)

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
- Dedicated server framework (Netcode structure)

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
  4-collider-per-poll PhysX cook budget; LOD band audits run as a rolling 1024-chunk burst; dispatch
  sort/removal and modified-tile border checks are allocation-free / O(1) set lookups. An idle,
  fully-streamed world pays ~zero per-frame terrain maintenance.
- **Idle streaming is zero-cost end-to-end** (1ee): `WorldStreamer.Update` keeps the 0.05 s poll beat,
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
- Server: authoritative world state stored server-side

---

## 10. Monetization (Future Consideration)

- No pay-to-win.
- Cosmetic-only microtransactions (skins, emotes).
- Expansion packs (new biomes, classes, story content).
