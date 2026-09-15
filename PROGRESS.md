# PROGRESS / Session Handoff Notes

Last updated: 2026-09-15. Read this first in a new session; then continue with the
`# OPEN TASKS` section (especially the axe/pickaxe bug). The **optimization sweep** ran Phases 0-5
(`1ag`-`1al` below); the sweep's planning doc (`OPTIMIZATION.md`) was retired once Phases 0-5 shipped —
only **Phase 6 / startup** (#17, #18) remains open, recorded under OPEN TASKS. Legacy working plans
(`PLAN.md`, `PLAN-class-skill-trees.md`, `planning.md`) were deleted; `game-design.md` is the single
durable design reference.

Companion docs: `game-design.md` (design), `GAME_DESCRIPTION.md` (player pitch).

---
## # OPEN TASKS

- **Axe/pickaxe bug** (from earlier sessions) — still open; see older entries below.
- **Optimization Phase 6 — startup (#17, #18)** (the old `OPTIMIZATION.md` carried the detail):
  - **#17** — `Core/GameBootstrap.cs:15-80` runs ~30 full-scene `FindAnyObjectByType` scans and
    initializes all managers synchronously. Fix: a registry to cache the lookups; split init across
    frames.
  - **#18** — `GameBootstrap.cs:107` + `Opt/NewWorldTestGround.cs:237-246`: boot spawn-chunk build is
    synchronous and the platform snap re-samples 61×61=3,721 noise points. Fix: reuse the spawn-chunk
    corners for the platform snap.

---
## 1ar. Redo projectiles as named shapes — every spell's projectile looks like its name

Follow-up on `1aq` review: user said projectiles should stop being same-colored balls — "bolt is the
shape of lightning, which should have generated like lightning from the thunder event". So projectile
visuals were split into **named `ProjectileShape`s** and each projectile spell's shape now matches its
name; a "Bolt"-named spell is built with the same jagged-segment technique as the thunder-storm
event's `RandomEventManager.SpawnJaggedBolt`, colored per element instead of being a sphere recolor.

- **`SpellData.cs`**: new `ProjectileShape` enum (`Auto, Bolt, Sphere, Shard, Lance, Spear, Blade,
  Splash, Comet, Missile, Dart`) + `Shape` field under a new `[Header("Presentation")]`.
- **`SpellCaster.cs`**: `DecorateProjectile(go, type, shape)`; `FireProjectile` passes `spell.Shape`;
  `AttachDefaultProjectileVisual` resolves `Auto` via `AutoShapeFor(DamageType)` (Fire→Sphere,
  Ice→Shard, Lightning→Bolt, Wind→Blade, Water→Splash, Earth→Shard, Physical→Dart, else→Sphere) and
  builds the body in `BuildProjectileBody`: `Bolt` (8 segment jittered cubes along +Z with taper —
  same look as `SpawnJaggedBolt`), `Shard` (drilling diamond), `Lance` (shaft+tip spike), `Spear`
  (dark shaft + diamond head), `Blade` (cross-blade spinning in-plane, `OrbFx.Swirl`), `Splash`
  (droplet + trailing drops), `Comet` (core + streak tail), `Missile` (clumped darts), `Dart`
  (sleek tip+body); removed the old per-type `Spark`/`Swirl` sphere builders. Added `Primitive`/
  `Materialize` helpers. `OrbFx.Mode` gained `Bolt`/`Swirl` pulse behaviors alongside Plain/Ember/
  Shard/Wisp.
- **Catalogs**: `SkillCatalog.Spell(...)` + class/race `MakeSpell(...)` gained a
  `projectileShape:`/`shape:` param. Assigned by name — every `*Bolt` spell (incl. Frost Bolt, Chain
  Lightning, Dark Bolt, Volt/Fork/Leap/Arc Spark/Volt Bolt, Fury Bolt, Shadow/Doom Bolt, Void Rend,
  Arcane/Force/Prism Bolt) → `Bolt`; `Ice Lance`/`Frost Pierce`/`Glacial Impale` → `Lance`; `Shadow
  Spear` → `Spear`; `Wind Blade`/`Razor Blade`/`Wind Scissor`/`Laceration` → `Blade`; `Scorch`/`Burn`/
  `Comet` → `Comet`; `Arcane Missiles` → `Missile`; `Tidal Surge`/`Water Bolt` → `Splash`;
  `Stone Shard` → `Shard`; Fireball/Chill Touch/Chill Soul/Frost Bite stay `Auto` (→ Sphere/Shard);
  class spells Mage Fireball (Auto→Sphere), Mage Arcane Bolt→Bolt, Archer Wind Shot→Dart, Taoist
  Talisman→Dart. `SpellSummon` turret bolts pass `_spell.Shape` too.
- **`magic-skills.md`** regenerated: new **Projectile Shapes** table (each shape, how it's built,
  which spells use it) + every projectile effect line ends with `shape:<effective>`.
- **`game-design.md`** updated: `projectile shape` bullet in the SpellData list, `ProjectileShape`
  doc comment in §3.8.1, and a shape table describing each shape + the Auto resolution rule.

### 1ar-status
- No CLI build — verified by code review: 31 `projectileShape:` assignments across both catalogs,
  `BuildProjectileBody` switch covers all 10 shaped cases with a default, no stale `Spark`/`Swirl`
  builder references, `OrbFx.Mode` members all defined, generator rerun idempotent (shape counts:
  Bolt×16, Shard×4, Lance×3, Blade×4, Comet×3, Missile×1, Splash×1, Spear×1, Sphere×1 = 34 total).
- Play-test after review: cast each school's projectile (Fireball sphere, Frost Bolt jagged bolt,
  Chain Lightning jagged bolt, Wind Blade spinning cross, Ice Lance spike, Shadow Spear, Tidal
  Surge splash, Arcane Missiles cluster, class spells) — confirm bolts read as lightning and 2× lance/
  spear tiers read distinct.

---
## 1au. Projectile trajectory now launches from the center of the casting circle

Play-test feedback on `1at` (missile homing): "the trajectory is not at the center of the magic
circle". The magic circle halo `CastingCircle` anchors to the magic rig/hand (`MagicHand`), and
`CastOrigin` is the same rig object, so the origin point already matched — the break was a hardcoded
**vertical lift**: `SpellCaster.FireProjectile` spawned projectiles at `pos + fwd·0.5 + Vector3.up·0.3`
and `PlayerController.UpdatePathPreview` drew the aim path from the same lifted point. That raised the
whole flight line ~0.3 above the halo's center plane, so the trajectory visually missed the circle's
heart.

- **`SpellCaster.FireProjectile`**: spawn is now just `pos + fwd * 0.5f` — on the aim line, no lift —
  so the trajectory passes through the casting circle's center. Self/terrain clearance stays safe via
  the `SpellEffect` caster-root skip + the small constant ground probe.
- **`PlayerController.UpdatePathPreview`**: the projectile path preview uses the same origin math
  (`pos + fwd * 0.5f`), keeping the pre-cast ray and the actual launch aligned (#1 preview-vs-fire
  drift fixed too).
- `game-design.md` "Charging & Casting Circle" notes the launch-from-center rule and the shared
  origin math.

### 1au-status
- No CLI build — verified by code review: rig == hand == CastOrigin == CastingCircle anchor confirmed
  in `WeaponRigBuilder` (weapon `go` becomes `combat.RightHand`, `magic.CastOrigin = go.transform`);
  both `FireProjectile` and `UpdatePathPreview` now use `pos + fwd*0.5f` (no `+Vector3.up*0.3f`).
- Play-test after review: aim a projectile spell (Arcane Missiles / Frost Bolt / Fireball) and confirm
  the path preview ray starts at the halo's center and the fired missile leaves from that same point.

---
## 1at. Missile homing follow-up: "raycast" meant the trajectory — prioritization is now per-frame along the flight path

User clarified `1as`: "when i said raycast i meant trajectory". The one-shot aim-line lock was the
wrong read — missiles should re-evaluate every frame along the path they are **bending on right
now**, and whatever foe sits on that trajectory is what they home to.

- **`SpellEffect.cs`**: `AcquireMissileTarget` (launch-time, aim-line raycast) replaced by
  `UpdateMissileTargeting` called every Update while the shape is Missile. Priority each frame:
  (1) `FirstEnemyOnTrajectory(Lookahead())` — a ray down the **current** `_dir` (the live
  trajectory; `Lookahead()` = spell Range or at least one second of flight), sorted by distance,
  first enemy root wins; (2) otherwise keep chasing the locked target's last known spot; (3) if
  never locked or the locked foe died, lock the nearest enemy in a 50° forward cone. Locked target
  re-acquires instantly whenever a foe drifts onto the trajectory. Docs reworded to "trajectory /
  flight path".

### 1at-status
- No CLI build — verified by code review: `FirstEnemyOnTrajectory`/`Lookahead`/`_locked` flag
  added, old launch-time lock removed, generator rerun ASCII-clean with the reworded Missile row.
- Play-test after review: cast Arcane Missiles past a row of foes — the missile should keep bending
  toward whichever one is on its current path and re-target if another steps into the trajectory
  mid-flight; killing the locked foe mid-flight should still finish toward the last spot.

---
## 1as. Add homing/pathfinding to the Missile projectile shape — it bends its way to the target

Follow-up on `1ar` review: user asked to "add pathfinder to missile, will bend its way to hit the
target, prioritize the target on the raycast". Projectile-delivery spells whose shape is
`ProjectileShape.Missile` (Arcane Missiles) now home instead of flying straight.

- **`SpellEffect.cs`**: `Launch` acquires a target when the spell's (resolved) shape is Missile —
  `AcquireMissileTarget` first raycasts along the aim line (`RaycastAll`, sorted by distance, skipping
  ground/caster-root/non-enemy) and locks the **first enemy on the raycast** ("prioritize the target
  on the raycast"); if none, falls back to the nearest enemy in a 50°-forward cone
  (`NearestEnemyInCone`, scored by distance + angle). `SteerTowardTarget` runs every update: bends
  `_dir` toward the (live or last-known) target spot at 240°/s via `Vector3.RotateTowards` and
  re-orients the body; the existing per-step raycast + ground-probe hitboxes then detonate on the
  chased target as usual. No target = flies straight. Target filter matches the turret's
  (`EnemyController`/`BossController` roots, excluding caster root / Player / Companion; ground
  colliders skipped).
- Docs updated: `magic-skills.md` regenerated (Missile shape row now says **homing** - locks the aim-
  raycast target and bends to chase), `game-design.md` §3.8.1 Missile row describes the lock priority
  + turn rate. `SpellData`/`SpellCaster` unchanged — homing is implicit to the Missile shape.

### 1as-status
- No CLI build — verified by code review: homing gated on `_spell.Shape == ProjectileShape.Missile`,
  `Array.Sort(RaycastHit[])` is valid (default distance comparer), `using System` already present,
  generator rerun ASCII-clean (fixed the em-dash mojibake in the ps1 shape row).
- Play-test after review: cast Arcane Missiles at a foe slightly off the aim line and at a foe on the
  line — missiles should bank toward both, prioritize the raycast target, and still detonate on
  intervening walls/ground; kill a mid-flight target and confirm the missile chases the last spot.

---
## 1aq. Add `magic-skills.md` — full generated list of all magic tree skills + SpellDelivery reference

User asked: "make a list of all the spell and their function, i'll tell you what need to be redo" —
scoped down to "only all the magic skills and put em in a file, also list me what deas delivery do".
Since the magic tree is huge (18 roots + ~400 L1/L2 branch nodes in `SkillCatalog.Magic.cs`, hand
transcription would be error-prone), a PowerShell generator script parses the skill files and emits
the listing into a new repo file `magic-skills.md`.

- `magic-skills.md` (repo root) contains: (1) **SpellDeliveries** reference — what each of the 7
  `SpellDelivery` values does (Instant / Projectile / Zone / Vortex / Beam / Summon / Storm), resolved
  from `SpellCaster.Execute`/`ResolveZone`; (2) **Base skills (roots)** table — all 18 base magic
  skills with passive/active, school, power/FP/cooldown/range/radius/status detail and description;
  (3) **Tree branches** — every L1 branch (and its L2 children, indented) parsed from the
  `bank.L1[...]` / `bank.L2[...]` branch tables, plus `A("id")` authoring refs to base skills.
- Generator lives at `C:\Users\antic\AppData\Local\Temp\opencode\gen-magic-list.ps1` (ASCII-safe;
  regenerable). No game code changed — `game-design.md` unaffected.
- Parsing verified by spot-checking generated rows against source lines (Fireball 25 power ✓, Holy
  Light zone/heal ✓, Storm Breath beam with drain 8 ✓, Tremor `terrain:Ring` ✓) and by counts (5
  authoring refs across the tree ✓).

### 1aq-status
- No CLI build — generator output verified by code review (read of `magic-skills.md`, spot-checked
  against `SkillCatalog.cs`/`SkillCatalog.Magic.cs` source lines).
- Play-test after review: user reviews `magic-skills.md` and tells us which magic skills / deliveries
  to redo.

---
## 1ap. Fix: talents list overlays the General skill tree on first open (TalentsView never hidden)

Bug: the player reported "the talent appears in the general skill tree". Root cause:
`BuildTalentsView` (CharacterInfoUI.cs) creates the `TalentsView` GameObject **active by default**,
and it was only ever toggled inside `SetSkillSubTab` (i.e. on a sub-tab button click). Since the
initial Skills tab defaults to `SkillSubTab.General` and `ShowTree`/`HideOtherTreeRoots` only hide
the three tree roots (General/Class/Race — never the talents view), the freshly-built TalentsView
rendered **on top of** the General tree (it is a later sibling, created last). The catalog itself
contains no "talent" skill, so no wheel node was the culprit.

- **Fix** (`UI/NewWorld/CharacterInfoUI.cs`): deactivate `TalentsView` at the end of
  `BuildTalentsView`. The Talents sub-tab still shows it via `SetSkillSubTab(tab == Talents)`; the
  General/Class/Race tabs render without the talent rows overlaying the wheels.
- No gameplay/design change — pure visibility default fix, `game-design.md` unaffected.

### 1ap-status
- No CLI build — code-review verified (edit is a single `SetActive(false)` + no new braces; grep
  confirms `_talentsView.SetActive` is still only in `SetSkillSubTab`).
- Play-test: (1) open Character Info → Skills with General selected: only the three wheels + legend
  (no `TalentPoints` / talent rows); (2) click Talents tab: rows appear; click back to General: rows
  disappear; (3) close character info while on Talents and reopen: panel still shows the correct tab.

---
## 1ao. Fix: map not generating — background chunk gen hit main-thread-only Application.persistentDataPath

Regression introduced by Phase 4 (`1ak`): `BuildOrLoadChunk` now calls `ChunkSaveManager.TryLoadChunk`
on the **background** generation threads, and `TryLoadChunk` → `ChunkFilePath` → `BaseDir` →
`Application.persistentDataPath` — which is **main-thread-only**. Every background chunk job threw
`get_persistentDataPath can only be called from the main thread` (caught, logged as a warning), so the
async pump produced nothing: only the synchronous main-thread boot chunk (`TerrainChunk_0_-1`) ever
spawned and the world was a void (no red errors, hence "no errors at all").

- **Diagnosed** with temporary `WorldStreamer` pump + background-job logging (commit `d970076`): the
  pump, dispatch, and finalize were healthy; the console showed the background warning with the
  `persistentDataPath` exception stack → root cause locked.
- **Fix** (`World/Chunks/ChunkSaveManager.cs`): `BaseDir` is now computed from
  `Application.persistentDataPath` **once** into a cached static string; new `Warmup()` populates it.
  `WorldStreamer.Awake()` calls `ChunkSaveManager.Warmup()` on the main thread before the first
  background dispatch, so worker threads only ever read the cached path. Debug logs removed.

### 1ao-status
- No CLI build — code-review verified (both touched files brace-balanced; grep confirms the **only**
  remaining `Application.` access in the world path is the cached warmup; `BackgroundGenerateChunk`'s
  try/catch keeps its warning for genuine failures). The prior debug commit `d970076` was superseded by
  this fix commit (debug lines removed in this entry's files).
- Play-test items: (1) boot → surrounding terrain should stream in around the player within ~1.5s
  (not just the boot chunk); (2) walk away and back → chunks persist/unload/reload, deformed areas
  still restore from `tc_{x}_{z}.dat`; (3) no `[WorldStreamer] Background chunk generation failed`
  warnings in the console.

---

Follow-up housekeeping requested by the user: delete planning documents that have no remaining use now
that the work they planned is shipped, and refresh `game-design.md` so it stays the single durable
design reference.

- **Deleted** (completed/superseded working plans, per user request):
  - `OPTIMIZATION.md` — the 6-phase audit/tracking sheet. Phases 0-5 shipped (`1ag`-`1al`); the last
    open item, **startup Phase 6 (#17/#18)**, was first folded into `PROGRESS.md` `# OPEN TASKS`
    (above) so no actionable detail is lost.
  - `PLAN.md` — the UI/polish/one-giant-skill-tree batch plan; all batches shipped/pushed.
  - `PLAN-class-skill-trees.md` — class/race radial trees plan; shipped (§3.2.1 in game-design).
  - `planning.md` — the original 10-Phase implementation roadmap; superseded by PROGRESS.md.
- **`game-design.md` updated** to match implemented behavior:
  - **§2.6 Chunk Persistence** — rewritten for the terrain-chunk save format: per-terrain-chunk files
    `worlds/{seed}/tc_{x}_{z}.dat` (`"NWTC"` magic) storing only **locally deformed tiles**
    (`ChunkTileMod`: local coords + 4 heights), loaded via `TryLoadChunk` and re-filling deformed
    corners before noise-filling pristine ones; dirty tiles flush batched per chunk (default
    synchronous one-write-per-cast).
  - **§9.1 Engine** — Unity 2022 LTS → **Unity 6 (6000.x)**, URP; notes `GetInstanceID`→`GetEntityId`.
  - **§9.3 Save System** — chunk files: `tc_{x}_{z}.dat` per terrain chunk, deformed-tiles only.

### 1an-status
- No CLI build — docs only. Verified: no remaining references to the deleted docs in `AGENTS.md` /
  `PROGRESS.md` / `game-design.md` (grep for `OPTIMIZATION.md`, `PLAN.md`, `planning.md` returns no
  roots outside stale PROGRESS history entries).
- Play-test: none needed (no gameplay code touched).

---
## 1am. Unity 6 compile fix — GetInstanceID → GetEntityId + OverlapBoxNonAlloc arg order

Follow-up to Phase 0 (the project opened in Unity 6, 2026): `Object.GetInstanceID()` is now
`[Obsolete]` as an error (CS0619) and `Physics.OverlapBoxNonAlloc` changed its argument order, so a
build reported 5 errors across 3 files. All fixed in code review; no Unity build run.

- `Opt/ObjectPooler.cs` — pool key type `Dictionary<int, Queue<GameObject>>` → `Dictionary<EntityId,
  Queue<GameObject>>`; `Warm`/`Get`/`ReleaseNow` now use `prefab.GetEntityId()` / `go.GetEntityId()`.
- `Combat/Weapons/HitboxSystem.cs` — `_hitThisSwing` is now `HashSet<EntityId>` keyed by
  `col.gameObject.GetEntityId()`; **`OverlapBoxNonAlloc` arg order fixed**: Unity 6's signature puts
  `Collider[] results` **before** `Quaternion orientation` (`position, halfExtents, _detectBuffer,
  rotation, HitLayers, TriggerInteraction`). The sphere overload was already correct.
- `Combat/AI/EnemyController.cs` — scan phase offset now derives from
  `GetEntityId().GetHashCode() & 0xFF` (the old `GetInstanceID() & 0xFF` bit-op doesn't exist on
  `EntityId`).

### 1am-status
- No CLI build — code-review verified (all three files brace-balanced; grep confirms zero
  `GetInstanceID` left in Assets; no project-local `EntityId` type that would shadow the engine
  struct; other `OverlapCapsuleNonAlloc`/`OverlapSphereNonAlloc` calls in Combat keep the unchanged
  non-rotated signatures).
- Play-test: enemy scan staggering, hitbox multi-hit detection, and object pooling continue to
  behave as before (pool reuse only observable as less GC).

## 1al. Optimization Phase 5 — skill-tree UI cache/pool (#15)

Audit: the skill tree destroyed+recreated ~3,000 GameObjects on every rebuild. That happened on the
Skills panel's first open **and** on every General/Class/Race sub-tab switch — `SetSkillSubTab` →
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
  `ShowTree` → `FitTreeToViewport()` → cheap repaint (`RefreshSkillTree`/`RefreshClassSkillTree`/
  `RefreshRaceSkillTree`). A **class or race change still forces a real rebuild** (build-id mismatch),
  so the change dialogs keep working.
- The old destroy loops that wiped ALL `_treeContent` children (killing sibling trees) now target only
  the current tree's own root. `MakeTreeNode` / `MakeTreeLine` / `MakeGeneralHeading` /
  `MakeClassTreeNode` / `MakeRaceSkillTreeNode` parent through the transient field `_treeBuildRoot`
  (set at the top of each builder) instead of hard-coded `_treeContent`.
- `FitTreeToViewport` now fits only the **current** sub-tab's node list (`switch _skillSubTab`), so a
  hidden tree's nodes can no longer inflate the other tree's fit box.
- Class/race builders no longer clear the General bookkeeping lists (`_treeNodes`/`_treeLines`/
  `_treeSkills`) — those GOs persist now, so returning to the General tab repaints correctly.

### 1al-status
- No CLI build — code-review verified (single file `CharacterInfoUI.cs` touched; brace-balanced
  369/369; grep-confirmed no remaining `SetParent(_treeContent` / `Destroy(_treeContent.GetChild`
  outside `EnsureTreeRoot`; `_treeBuildRoot` assigned in all three builders before any
  node/line/heading creation. Public API unchanged.)
- Regressions to watch on play-test: (1) **Sub-tab switching** — General/Class/Race should switch
  instantly with identical layout (wheel positions, class radial, race radial unchanged from before);
  (2) **class/race change dialog** — pick a different class/race, return to that sub-tab, confirm the
  tree actually rebuilds (build-id mismatch triggers it); (3) **per-tree fit** — each tree frames to
  the viewport on entry with no clipping (mixed-bounds bug fix); (4) **learned-state repaint** — learn
  a skill, switch away and back, confirm node colors/labels still update (persisted node lists).

## 1ak. Optimization Phase 4 — world streaming / terrain persistence rewrite

Audit hot spots #10, #11, #12, #13, #14 all live in the world/terrain pipeline and hit one of
three pain points: **revisit latency** (every chunk regenerated from noise on revisit even though
loads of tiny deformation files existed), **write burst** (each Earth cast wrote 250+ individual
per-tile files synchronously, each with tmp+move overhead), and **frame hitches** (full 900-tile
mesh rebuild + collider cook + prop spawn every cast/finalize). All six terrain files are touched:

**#10 — ChunkSaveManager rewritten for terrain-chunk granularity:**
- `World/Chunks/ChunkSaveManager.cs` — new binary format (`"NWTC"`, one file per terrain
  chunk under `worlds/{seed}/tc_{x}_{z}.dat`) holding only locally-deformed tiles (the old
  per-tile files are orphaned and harmless). `TryLoadChunk` returns a `ChunkSaveData` of
  `ChunkTileMod` structs (local coords + 4 heights + version stamp) which BuildOrLoadChunk uses
  to reconstruct deformed corner heights before noise-filling pristine corners.

**#10, #11a — Background generation now reads-or-generates:**
- `World/Streaming/WorldStreamer.cs` — `BackgroundGenerateChunk` and `GenerateChunkSync` both
  call the new `BuildOrLoadChunk(tc, seed)`, which tries `ChunkSaveManager.TryLoadChunk` first.
  If the file exists, deformed tiles restore their saved heights (gapless within + across chunks
  because DeformAt always deforms/Modifies/saves all co-affected neighbor tiles together);
  pristine corners remain deterministic noise. This cuts revisit CPU from ~961 octave samples
  to one small file read per chunk (only mod tiles restored from disk).

**#11a — Batched dirty-tile flush (250 writes → 1 file per cast):**
- `WorldStreamer.MarkDirty` now only adds to `_dirtyTiles` (no per-tile sync write);
  `FlushDirtyChunk(tc)` gathers all dirty tiles inside one `TerrainChunkCoord`, clones the
  heights, and writes a single `SaveChunk` atomically (when `ChunkSaveManager.SynchronousWrites`
  is true, which is the default — one sync write per chunk per cast is <1ms, eliminating the old
  250+ tiny writes). An Earth cast touching 2 chunks now writes 2 files (one per chunk) total,
  not 250. `UnloadChunk` and `OnDestroy` also flush (OnDestroy groups `_dirtyTiles` by
  `TerrainChunkCoord` and flushes each). The old per-tile `ChunkSaveManager.Save` code path is
  gone.

**#11b — Sub-region mesh patch (only touched quads rebuilt):**
- `WorldStreamer.RebuildChunkRegion(tc, obj, minCX…maxCZ)` builds `ChunkMeshData` for only the
  local-tile rectangle affected by the deformation (not all 900 tiles). `ChunkObject.PatchRegion`
  writes the rebuilt quads' vertices/UVs/normals into the cached `_merged` arrays and re-uploads
  only those channels, then re-cooks the collider once. Falls back to the full 900-tile
  `ApplyMerged` path when the region exceeds ~75% of the chunk.

**#12 — Time-budgeted finalize + incremental prop streaming + shared Random:**
- `FinalizeChunks` now measures wall-clock time (`Time.realtimeSinceStartup`) and breaks at ~6ms
  per poll tick, in addition to the `ChunksPerFrame` cap.
- Props are NO LONGER spawned inside `CreateChunkGameObject`. `ChunkObject.BeginProps(seed)` now
  queues the 900 local tile indices and a single deterministic `System.Random` instance (one
  `new Random` per chunk instead of 900). `WorldStreamer.StepChunkProps` drives
  `ChunkObject.StepProps(budget)` from a global budget of 40 tiles per poll tick, spreading
  prop spawning across the next few ticks so an 8-chunk fill never spikes a single frame.
  Determinism is preserved (same seed/chunk → same Random stream → same prop layout), though
  placement differs from the old per-tile Random approach (noted below).

**#13 — Shared cube mesh + shared materials for all terrain props:**
- `Models/MapBuilder.cs` — new `SharedCubeMesh()` peels a unit cube from a single CreatePrimitive
  and caches it; every `MakeBlock` call (rocks, houses, NPCs, cars — the whole game) now adds a
  `MeshFilter.sharedMesh = SharedCubeMesh()` + `BoxCollider` (no per-call hidden mesh allocation,
  no collider destroy; identical unit-cube shape).
- `Models/MapBuilder.Nature.cs` — new `MakeCubeShared(...)` helper uses the shared mesh + assigns
  material via `sharedMaterial` (not `r.material`). The three functions that spawned ~2-3 cubes
  per tree/rock (`GrowBranchSegment`, `SpawnLeaves`, `GrowLeafChain`) were leaking per-renderer
  Material copies (via `r.material = mat`); they now use `MakeCubeShared` with `r.sharedMaterial`.

**#14 — Single mesh upload (five setters → one pass):**
- `World/Terrain/ChunkMeshGenerator.cs` — `CreateMeshFromMerged` now calls
  `SetVertices/SetTriangles/SetNormals/SetUVs` then `mesh.UploadMeshData(false)` (one upload
  instead of five implicit per-property uploads on the old direct-setter path).

### 1ak-status
- No CLI build — code-review verified (all six touched files brace-balanced; no stale references to
  `SpawnProps`, `ChunkSaveManager.Save`, old `_dirty` field, or `Physics.OverlapSphere(` in Combat).
  `ChunkLodManager` and `ChunkValidator` public APIs unchanged. `DeformAt` access to locals fixed
  by parameter pass.)
- Regressions to watch on play-test: (1) **First revisit of a deformed area** — run an Earth
  spell, walk away, then return; verify heights persist and corners match (no gaps at chunk edges).
  (2) **Cross-chunk deformation** — run a large Earth cast near a chunk seam; walk away and return;
  verify both sides persisted and seam is smooth. (3) **Tree/rock placement** — return to a
  previously-generated area; trees should be deterministic (same positions) even after the
  single-Random refactor. (4) **Chunk unload+reload** — trigger a chunk unload (move far away and
  come back); verify deformed heights load from file and tree/rock placement is recreated identically
  via the determinism stream. (5) **Booting** — startup should feel identical (spawn-chunk props now
  stream over 2-3 ticks after terrain appears; boots under 1s total anyway).

Audit hot spots #4, #5, #7 all live inside combat: every spell tick/impact/burst ran **allocating**
`Physics.Overlap*` (a fresh `Collider[]` per call, several per second per spell), the caster rebuilt
`new List<string>(_cooldowns.Keys)` every frame, and combat FX/DamageNumber did `new GameObject` +
`new Material` per popup/strike with (in one case) a **permanent bolt leak**. Now:
- **NonAlloc buffers everywhere** — all remaining allocating overlaps in `Assets/Scripts/Combat`
  are gone (verified by grep — every call is now a `*NonAlloc` into a recycled buffer):
  - `Combat/Weapons/SpellCaster.cs` — `ResolveBurst` uses a shared `_overlapBuffer[128]` (#4); the
    cooldown tick iterates a reused `_cooldownKeys` list instead of `new List<>(Keys)` each frame (#5).
  - `Combat/Weapons/SpellZone.cs`, `SpellTornado.cs`, `SpellStorm.cs` — per-tick `_tickBuffer[128]`
    / `_strikeBuffer[128]`; `SpellStorm.RandomStrikePoint` and `ResolveStrike` share `_strikeBuffer`.
  - `Combat/Weapons/SpellEffect.cs` — impact + zone bursts share `_splashBuffer[128]` (its ground
    probe was already NonAlloc).
  - `Combat/Weapons/SpellBeam.cs` — channel tick now `OverlapCapsuleNonAlloc` into `_tickBuffer[64]`.
  - `Combat/Weapons/SpellSummon.cs` — `NearestEnemy` reuses its `_hitBuffer` via NonAlloc.
  - `Combat/Weapons/HitboxSystem.cs` — sphere/box both `NonAlloc` into instance `_detectBuffer[64]`
    (was the same allocating call twice per swing).
  - `Combat/Weapons/WeaponSkillExecutor.cs` — weapon-skill strikes share static `_strikeBuffer[64]`.
  - `Combat/Skills/IEffect.cs` (DamageZoneEffect), `ClassEffect.cs` (Taunt / LifestealStrike /
    ClassStrike), `RaceEffect.cs` (RaceStrike / RaceLifesteal / RaceTaunt / RaceRoar) — each uses a
    static `_buf[64]` (single-threaded, safe) instead of allocating `Collider[]`.
- **Pragmatic pooling (#7)**:
  - `Combat/Effects/DamageNumber.cs` — every hit used to spawn a new GameObject + TextMesh +
    Material. Now a static pool (cap 256): `Acquire()` reuses a released entry, alpha/scale reset on
    re-show, overflow destroys. API unchanged (`DamageNumber.Spawn` static overloads).
  - `Combat/Weapons/SpellStorm.cs` — `StrikeFlash` (the bright per-strike burst sphere) is pooled
    (cap 32) with one shared material; the lightning "bolt" bars reuse `SkillFx.SharedSpriteMaterial`.
    **Leak fix:** the old `SpawnStrikeFx` created 2 cubes per lightning strike that were never
    destroyed — now `BoltFader` shrinks them to nothing over 0.25s and destroys them.
  - `Combat/Effects/SkillFx.cs` — new `SharedSpriteMaterial(Color)` cache (keyed by damage-palette
    color, so the pool stays tiny); `SpellZone`/`SpellStorm` visuals share these instead of
    `new Material` per cast. Faders that animate alpha still own per-instance materials.

### 1aj-status
- No CLI build — code-review verified (all 13 touched files brace-balanced; every allocating
  `Physics.Overlap*` call in `Assets/Scripts/Combat` eliminated — grep-confirmed; no API changes
  outside internal fields; `SpellCaster` still `using System.Collections.Generic`).
- Regressions to watch on play-test: (1) **storm bolts** now fade out — confirm lightning strikes
  no longer leave two permanent cubes behind; (2) **DamageNumber pool** — many rapid hits should cap
  at ~256 simultaneous popups, alpha fully resets on reuse (watch for half-transparent numbers after
  heavy AoE); (3) spell zone/disc colors should look identical (shared palette materials — confirm
  heals/buffs/damage tints are still distinct); (4) bursts that used to hit >128 colliders would now
  truncate (no in-game content that dense — verify a big storm + 6 enemies still hits everything).

The audit's UI row all ran **every frame** with `GetComponent(InChildren)` lookups, string
formatting and TMP repaints. Now cached/dirty-checked:
- **`UI/NewWorld/PlayerBarsHUD.cs`** (#2) — `SpellCaster`/`PlayerStats` refs cached once per
  player object (`EnsureRefs`, re-resolved when the player root swaps) instead of
  `GetComponentInChildren` every frame; the HP/FP/Stam text labels now repaint only when the
  rounded value changes (`UpdateLabel` stores last ints) and the charge-% text only when the
  percent ticks.
- **`UI/NewWorld/SkillBarHUD.cs`** (#3) — the `new List + AddRange + Sort` per frame became a
  reused sorted cache (`_entries`/`_prevEntries`); label texts are repainted only when the
  binding set actually changed; the (cheap) cooldown `fillAmount` update remains per-frame.
- **`UI/NewWorld/MagicWheelUI.cs`** (#16) — per-slot `GetComponent<Image>` in `Paint` is a cached
  `_slotImages` list; `SpellCaster` cached against the current player; the armed chip and ring
  hint texts repaint only on change.
- **`UI/NewWorld/CompassMinimapHUD.cs`** — `Camera.main` cached, and the heading label only
  repaints when the yaw crosses a cardinal boundary (`CardinalIndex`).
- **`UI/NewWorld/EnemyHealthBarHUD.cs`** — `Camera.main` + the bar's `RectTransform` cached;
  `SetActive`/`fillAmount` are dirty-checked so steady frames skip canvas rebounds. **Also fixed a
  latent pooling bug:** a released bar re-used by a *different* enemy kept the previous enemy's Max,
  skewing its health fraction — Max now resets whenever the bar attaches to a new target.
- **`Opt/NewWorldTestGround.cs`** — the per-frame `PlayerController` lookup is now cached per
  player object.

### 1ai-status
- No CLI build — code-review verified (all six files brace-balanced; labels still formatted per
  call site). Play-test: damage/spend/charge bars still tick at the same thresholds; compass
  flips N→NE→E… correctly; the armed-magic chip appears/disappears with weapon swap; enemy bars
  track fills correctly — verify the re-pool fix by killing the boss (or 25 enemies) and confirming
  a reused bar shows the correct fraction on the next target. Skill-bar cooldown fills unchanged.

## 1ah. Optimization Phase 1 — enemy scan stagger, tornado pull + camera collision throttles

Per-frame physics hot spots from the OPTIMIZATION.md audit (#1, #6, #8):
- **`Combat/AI/EnemyController.cs`** — the per-enemy `OverlapSphereNonAlloc` target scan ran **every
  frame** (60 live enemies = 60 broad-phase queries/frame). Now staggered to **4 Hz** with a
  per-instance phase offset (`_nextScanTime = Time + (GetInstanceID() & 0xFF) * 0.001f` in Awake;
  `TickTargets` early-outs between scans). First scan is ~immediate; taunt lock still overrides
  scans. `ClosestTarget` uses `sqrMagnitude` instead of `Vector3.Distance`.
- **`World/TornadoBehavior.cs`** — the 30u `OverlapSphereNonAlloc` pull query ran every frame;
  now throttled to **~4 Hz** (caught objects keep orbiting via the per-frame `_pulled` velocity
  writes, so captures stay smooth).
- **`Player/Controller/ThirdPersonCamera.cs` + `Player/CameraModeSwitch.cs`** — the third-person
  terrain-collision `SphereCast` ran every LateUpdate; now re-run at **~10 Hz** with the cached
  clamp distance reused between casts (per-frame position math unchanged; result identical).
- `OpenWorldGrounding` (per-body raycast) skipped — already confirmed a dead, unreferenced script
  (see §3 parked cleanup).

### 1ah-status
- No CLI build — code-review verified (logic + braces checked; no API changes). Play-test in Unity:
  enemies still aggro/chase/attack normally (≤0.25 s scan latency at worst); spawn a wave of 40+
  enemies and compare frame time (CPU drop expected); tornado still spins, tows, swirls; third-person
  camera still avoids walls while turning/zooming. No observable feel regression expected.

Legacy commits (`c9103d9`, `77a685d`) rewrote `HitboxSystem` + `ObjectPooler` value keys from
`long`/`GetInstanceID()` to a custom `EntityId`/`GameObject.GetEntityId()` **that were never
defined** anywhere in `Assets` — a latent compile blocker for the whole project. Reverted both
files to plain `int` + `GetInstanceID()`:
- `Combat/Weapons/HitboxSystem.cs:40,107` — `HashSet<EntityId>` → `HashSet<int>`;
  `col.gameObject.GetEntityId()` → `GetInstanceID()`.
- `Opt/ObjectPooler.cs:14-15,33,47,88` — pool dictionary + keys → `int`/`GetInstanceID()`.
Verified: zero `EntityId`/`GetEntityId` references remain. No behavior change (`GetInstanceID` is a
stable, unique per-object id — correct identity for both swing dedup and pool keying).

### 1ag-status
- No CLI build — code-review verified (grep clean; semantic checker 0 diagnostics). Play-test in
  Unity: game compiles and boots; melee hits still dedupe per swing; nothing else observable changes.

## 1aa. Recent completed work (2026-09-13) — talent system, per-skill levels, Lightning school, 3-wheel skill tree, projectile path preview, free class/race switching
Commit `125a775` ("re-organize skill trê, add projectile path, add diferent skill type", 2026-09-13, TVQ01) —
the largest single commit in the history so far (58 files, +3691/−377). Some of the beam/summon/storm
delivery code from 1m (dated 09-12) landed in this same commit.

### Talent system — rankable player-level perks (new)
- `Assets/Scripts/Player/Stats/TalentCatalog.cs` (+ .meta) — in-code roster (no .assets), `TalentKind` =
  `PlayerXp` / `SkillTypeXp` / `Stat`; every talent `MaxRanks = 3`, effects **additive per rank**:
  - `t.fast_learner` "Fast Learner" — +5 % **character XP**/rank.
  - 6 skill-type talents (`t.melee` … `t.fortitude`; "Arcane Study", "Craftsmanship", …) — +6 % XP/rank
    for that skill type (applies to both the per-skill level and the category bar).
  - 11 stat talents (`t.health` … `t.luck`; "Vitality", "Fleet", "Might", "Sage", …) — +1 **flat stat
    point**/rank.
- `TalentTracker.cs` (+ .meta) — const `PointsPerLevel = 1`; subscribes to `LevelUpSystem.OnLevelUp`;
  `EnsureOn(root)` auto-adds `LevelUpSystem` first so boot paths work before the character panel exists.
  - **New game**: `GameManager` grants ONE **random talent at rank 1** (`GrantRandomFirstTalent`, idempotent
    via `FirstGranted`); further points = 1 per character level-up.
  - `CanSpend`/`TrySpend` (point → +1 rank), `RankOf`, bonus readers `PlayerXpBonus` / `TypeXpBonus` /
    `StatBonus` — all **live additive reads over owned ranks**, so restore never double-applies.
- Integration: `LevelUpSystem.AddXp` adds the character-XP talent bonus (stacks with race all-XP);
  `SkillXpTracker` adds `TypeXpBonus` to the category bar; `SkillProfile.GainUse` adds it to per-skill XP;
  `PlayerStats.GetTotal` = `base × (1 + race% + race-skill%) + talent stat points + temp buffs`.
- Persistence: `SaveData.talentStateJson` (`SaveManager.cs:76` write `Points/Owned/FirstGranted`,
  restore at `SaveManager.cs:200`); blank/malformed → clean slate.
- UI: `CharacterInfoUI` → **Skills panel gained a "Talents" sub-tab** (`SkillSubTab.Talents`): a
  "Talent Points: N" header plus one row per talent (name, `Rank x/3`, `EffectPerRank()` text) with a
  **Rank Up** button enabled when a point exists and rank < max.

### Per-skill levels ("different skill type") — every learned skill levels itself
- `SkillProfile.cs` — each learned skill now carries its own **level** (`SkillProgress`;
  `MaxSkillLevel = 100`). Every successful use grants **per-skill XP** (`XpPerUse = 12`; the category bar
  gets `CategoryXpPerUse = 10`) scaled by race `XpBonusAll` + talent `TypeXpBonus`; level-ups follow a
  **linear curve** (`BaseXpToNext = 20`, +15 per level). New `LevelOf(id)` / `TryGetProgress(...)`.
- UI (`CharacterInfoUI`): General-tree **node labels show "Lv N"** under the name once learned; the detail
  pane adds "Lv N · XP x/y" for learned **active** skills (passives show just the level).

### Magic: Lightning is now its own school (7th L1 root)
`SkillCatalog.Magic.cs` — L1 comment becomes "7 roots; 6 full + Lightning's own school". New **`magic_lightning`
root** (passive Int+3) with L1 branches **Volt** (Projectile/Stagger), **Stormcall** (Storm, 3.5 s),
**Deep Charge** (passive Int+3), **Sky Fury** (Beam 13 m, `channelDrainPerSecond 9`) plus four full L2
subtrees (`magic_lightning_volt/_storm/_charge/_fury` — 20 new nodes incl. Storm Rain, Sky Beam,
Devastation, Lightning Tempest). **`magic_chain` (Chain Lightning) moved** out of `magic_fireball` to the
Lightning root. Several branches retuned onto the 1m deliveries (ids unchanged):
- **Storm Breath** (was Zone) → **Beam** 10 m, drain 8/s · **Gust Totem** (was "Air Burst" Zone) → **Summon**
  wind totem, 6 s · **Healing Shrine** (was "Light's Embrace" Zone) → **Summon** heal aura, 8 s ·
  **Arcane Rune** (was "Aegis" Zone) → **Summon** turret, 5 s · **Arc Storm** (was Zone) → **Beam** 14 m,
  drain 10/s · **Thunderstorm** (was "Overload" Zone) → **Storm** 3 s.
- `SkillCatalog.cs` `Spell()` gained optional `deliveryRange`; `SpellContext` gained the ranged fields.

### Skill tree reorganization — three wheels on one board (CharacterInfoUI)
- The **General sub-tab now composes 3 independent tree wheels**: **Magic** (compact full-circle wheel,
  left, origin −2200,0), **Physical combat** (standard 4-wedge wheel — Melee/Ranged/Stealth/Fortitude,
  centre, 0,0), **Crafting** (compact full-circle, right, +2200,0); each wheel has its own hub bubble
  plus a heading ("MAGIC"/"PHYSICAL"/"CRAFTING"). Compact wheels reuse the tuned layer bands scaled for a
  single full circle (arc capacity ~6× wider).
- `FitTreeToViewport` switched from centred max-radius to a **bounding-box fit** (re-centres the content
  on the box — off-centre wheels no longer clip); `TreePan` zoom widened to **0.05×–20×**.
- **Magic nodes tint by element**: `NodeColor` lerps learned/available/locked state color 60 % toward the
  skill's `DamageKind` color (`DamageNumber.ColorFor`) — fireball reads orange, frostbolt icy-blue, etc.
- `EnsureProgression` now also adds `TalentTracker` (after `LevelUpSystem`) so the skills panel always has
  the tracker.

### Projectile path preview — full flight-path cone while charging (new)
- `Assets/Scripts/Combat/Weapons/ProjectilePathPreview.cs` (+ .meta) — prefab-free singleton, world-space:
  a **cone of 10 translucent rings** (`Rings=10`, `RingSegments=18`, `MaxHalfAngleDeg=45`) from the weapon
  along the aim line that **narrows as charge builds**, collapsing to a thin centre ray of the exact
  predicted trajectory at full charge; clipped at the first solid hit.
- `PlayerController.cs` (`_pathPreview` field via `ProjectilePathPreview.Instance`):
  - **Magic projectile spells** while charging → preview mirrors caster aim (spawn near the hand,
    `fwd×0.5 + up×0.3`, reach = `max(speed,1)×4` flight envelope), tinted by the **spell element**.
  - **Ranged** (regular draw AND the per-hand dual draw) → preview from the hand along aim, reach =
    `speed×lifetime`, spread from ranged accuracy; half-angle shrinks as charge grows, tinted by shot type.
  - Hidden on cancel/release/weapon-switch (`HidePathPreview`). New `MagicChargeFullTime = 1.2f` (ramp for
    `SpellChargeLevel`), public `LookPitch` (torso bends with camera), `_chargeDrained` tracks the 1v
    real-time FP drain.

### Class & race switching are now free
- `ClassUnlocker.cs` `SetActiveClass`: the `freeBaseline` (Wanderer) + `IsUnlocked` gate is **removed** — any
  **known class id switches freely**; returns false only for unknown ids. The class-change dialog shows
  "(current)" and no requirement summary / lock dimming; falls back to `ClassUnlocker.BuildDefaultClasses()`
  when the roster is empty.
- Race dialog: same "(current)" treatment; confirm now calls `SetActiveRace(requireStone: false,
  unlockIfNeeded: true)` — **no Ritual Stone cost, the target race auto-unlocks** for this character.

### UiAssetCache — central UI asset cache (new)
`Assets/Scripts/UI/NewWorld/UiAssetCache.cs` (+ .meta) caches `MenuTexture` + `DefaultFont`
(`VietPixel`), replacing per-file `Resources.Load("menu")` / `Resources.Load<TMP_FontAsset>("VietPixel")`
in `CharacterInfoUI`, `MenuPanelBase`, `TypingMinigame`, `FishingUI`.

### Farming — plots + seeds plant on drop
- `ToolManager.DropThrow.cs`: dropping **seeds onto a farm Plot** now plants directly
  (`FarmingManager.GetPlotAt` → `PlantPlot`, "pop" sfx) instead of only gifting the world.
- `ToolManager.cs` farming actions (till / water / fertilize / boost-growth / harvest) route through
  `FarmingManager` plots first with the legacy `_worldBuilder`-field fallback; `BuildingCount` →
  `BlueprintOptionCount` (`ToolManager.BuildingMenu.cs`). `FarmPlot.cs` / `FarmingManager.cs` gained the
  plot-query/harvest helpers.

### Ranged — runtime arrow fallback when no prefab
- `RangedWeaponBehavior.cs` (175 changed lines): `FireProjectile` with no projectile prefab now builds a
  **generated arrow** projectile at runtime (shaft + head) instead of the old hit-scan raycast tracer.
  (Throwing-hammer visual already existed from 1m.)

### World gen & boot perf
- `GameBootstrap.cs`: `FarmingManager` ensured at boot; render distance **3 → 5**, `MaxRadius 160`; only the
  **spawn chunk builds synchronously** — the surrounding chunks stream in via the background pass (~1.5 s).
- `ChunkLodManager.cs`: `EffectiveCullDistance()` **auto-scales the LOD cull distance to the streamer's
  render radius** (`_streamer`), so culling no longer fights the bumped radius; `WorldStreamer.cs` and
  `RenderDistanceController.cs` got the matching radius plumbing.
- `NewWorldTestGround.cs`: `SpawnBenchBudgeted` coroutine spawns the test bench **one lane group per frame**
  (was all-at-once in Awake, which blocked early frames) while preserving `SpawnBench` ordering; the platform
  max-height scan samples Perlin **every 2 m (61×61 vs 121×121, ~4× fewer noise calls)** with a +3 m clearance.

### Housekeeping
- **Accidental commit**: `Assets/_Recovery/0 (16).unity` + `0 (17).unity` (347-line unused crash scenes,
  with metas) snuck into this commit — they belong in the pending `_Recovery` cleanup (see §3).

### 1aa-status
- No CLI build available in this environment (Unity project) — compile/behaviour verified by code review
  only. Unity play-test pending: new game grants one random talent + Talents tab ranks it; level-ups grant
  talent points; skill detail shows Lv/XP climbing per use; Lightning root + Chain Lightning relocation;
  3-wheel General tree pans/zooms without clipping; path cone narrows on magic/ranged charge and clips at
  walls; free class/race switching; seed-drop planting on plots; render-distance bump + LOD cull;
  bench spawn no longer hitches at boot.

---

## 1ab. Recent completed work (2026-09-14) — Tornado spell uses the old environmental tornado model + function; Magic-wheel per-school wedges

- **`MapBuilder.BuildTornado`** gains a `widthScale` param (default 1 — town-tornado event unchanged).
- New **`SpellTornado.cs`** (Vortex delivery for `magic_tornado_spell`): rebuilds the old tall
  drifting debris funnel (`BuildTornado` → `TornadoBehavior`) scaled to the spell radius, tunes the
  TornadoBehavior fields to spell scale (so pulled objects don't ride up to the old 80-unit orbit),
  and layers SpellZone-style Wind damage ticks + enemy pull on top; destroyed after its lifetime (5 s).
- **`SpellCaster.SpawnVortex`** routes only the Tornado spell to `SpellTornado`; all other Vortex
  spells (e.g. Mini Tornado) keep the `SpellZone` funnel unchanged.
- **`CharacterInfoUI.BuildWheel`** (compact three-wheel board): each school/category now gets its own
  wedge around the full circle (`sectorHalf = π/groups − gap`, per-wedge hub + school label) instead of
  all nodes cramming into the bottom sector. Standard single-wheel layout is untouched.
- Docs: game-design.md §3.8 (Vortex exception → `SpellTornado`), SkillCatalog wind-line comment.

### 1ab-status
- No CLI build — code-review verified (braces, ids, per-wedge capacity). Play-test pending: cast
  Tornado — expect a big drifting debris tornado (~10 tall / ~7 wide) that pulls props via physics +
  enemies and ticks Wind damage for ~5 s; Magic wheel per-school wedges read cleanly around the circle.

---

## 1ac. Recent completed work (2026-09-14) — Tornado spell visibly spins and carries animals into a swirl

User: "the tornado spell currently does not spin and does not pull animals toward it, only makes
them fly up." Cause: funnel blocks were square prisms (Y-rotation invisible, no axis orbit), and
`Livestock`/pet controllers overwrote the Rigidbody X/Z every physics step while keeping Y — so
the orbit push was erased and only the vertical lift survived.

- **`TornadoBehavior.cs`** — blocks now orbit the tornado axis at their own radius + self-rotate
  (visible churn); defaults `BaseRotateSpeed 8→24`, `RotateSpeedVariation 4→16`. Pulled objects get
  a two-phase tow: ~0.8s drag into the axis at ground level, then lift + swirl. New carry flag via
  `ITornadoCarried` (set on capture, cleared on release/destroy). `AddDebrisBlock` orbit heights use
  `OrbitHeight` (was hardcoded 80) so spell-sized tornados keep debris low.
- **`MapBuilder.BuildTornado`** — blocks are rectangular (`width × h × width*1.55`) so rotation shows.
- New **`Assets/Scripts/World/ITornadoCarried.cs`**; **`Livestock.cs`** + **`PetController.cs`**
  implement it and stop writing their own velocity while carried.
- **`SpellTornado.cs`** — faster spin (BaseRotateSpeed 70 / Variation 55) + 13 `AddDebrisBlock`
  chunks for the old-game swirling debris look.
- Docs: game-design.md §3.8 Vortex note, SkillCatalog wind-line comment.

### 1ac-status
- No CLI build — code-review verified. Play-test pending: cast Tornado — the funnel should visibly
  churn; chickens/cows/pets and physics props get towed into the axis, then carried in a low swirl
  for the tornado's life (no more instant vertical pop). Town-event tornado inherits the same churn.

---

## 1ad. Recent completed work (2026-09-14) — Wind Walk flight spell

User: "add flight spell." A timed self-buff (Instant delivery that affects the caster) in the Wind
school after Tornado; prereq Gale Force.

- **`SpellData`** — new `SelfBuff` flag; **`SkillCatalog.Spell`** factory gains an optional
  `selfBuff` param. New skill **`magic_flight`** "Wind Walk": Wind, Focus 20, FP 18, Instant,
  cooldown 25 s, `duration 10` (self-buff), prereq `magic_gale` — baked into wind-wheel layout.
- **`SpellCaster.ResolveDirect`** — `SelfBuff` spells skip the hit scan and run a caster effect:
  `PlayerController.BeginFlight(Duration)` + RingFlash, then return.
- **`PlayerController`** — flight state (`BeginFlight`/`EndFlight`/`IsFlying`, `FlightSpeed 12`,
  `FlightVerticalSpeed 6`): flying disables sprint + dodge, moves at flight speed, and replaces the
  grounded/gravity vertical branch with free vertical movement (hold **Space** to ascend,
  **LeftCtrl** to descend; release to hover). Buffs stack by extending `_flightUntil`.
- Docs: game-design.md §3.8 `selfbuff` field line, SkillCatalog wind-line comment.

### 1ad-status
- No CLI build — code-review verified. Play-test pending: learn Wind Walk (requires Gale Force), cast
  it from the wheel or a hotkey — instant RingFlash, then 10 s of fly; Space/LeftCtrl move vertically,
  landing resumes normal movement. Cooldown 25 s; recast mid-flight extends the timer.

---

## 1ae. Recent completed work (2026-09-14) — Event tab unlocked in the pause menu (new game)

The Event Test panel (`UIManager.EventTest.cs`) existed but was dead: `CreateEventTestPanel` was never
called and nothing opened it. It is now reachable from the Pause menu.

- **`UIManager.cs`** — capture menu dimensions (`_menuPanelW/H/_menuPad`) in `InitializeUI` so the panel
  can be built lazily; new pause-menu button **"Sự Kiện"** (Events) between Quests and Settings; pause
  buttons re-spaced to a uniform 0.10·panelHeight pitch to fit all 7; button text re-applied on the
  localization refresh path.
- **`UIManager.EventTest.cs`** — `ShowEventTestPanel(true)` lazily calls `CreateEventTestPanel` on first
  open (list reflects events registered by then). The panel's event buttons already close the pause
  menu, resume the game, and `ForceEventByIndex` — unchanged.

### 1ae-status
- No CLI build — code-review verified. Play-test pending: Pause → **Sự Kiện** → pick an event (grouped
  Tier 0/1/2 grid) — the game resumes and that event triggers (e.g. tornado storm over the town).

---

## 1af. Recent completed work (2026-09-14) — Water + Earth magic schools: Wet status & terrain deformation

User: "add water and earth magic" (with the schools sharing the tree's existing depth: full L1/L2
rosters like the other schools; Water applies a **Wet** status; Earth has **no status** — its spells
reshape the terrain, "earth move would circle around modifying terrain"; school name on the wheel's
school bubbles).

- **Wet status** — `StatusEffectType.Wet = 6`. New **`WetStatus.cs`**: `SlowFactor 0.85` + `Duration 4 s`
  (re-applies `EnemyController.ApplySlow` on apply + first Update, so it stacks safely with Frost via
  `Mathf.Min`), and **conducts Ice/Lightning** — `IceLightningDamageBonus 1.4`. `SpellCaster.ApplyStatus`
  routes Wet → `WetStatus.Apply`; `ApplyHit` boosts Ice/Lightning vs wet targets via
  `WeaknessMultiplier 1.4` (Ice/Lightning × Wet = ×1.4).
- **`TerrainShape`** (`None/Ring/Spikes`) on `SpellData` + `Spell(...)` factory param. New
  **`TerrainDeformer.cs`** facade → **`WorldStreamer.DeformAt(center, radius, shape)`**: lifts affected
  tile-corner heights (Ring = stone wall at ~0.72·radius, Spikes = mound + deterministic hash peaks;
  smootherstep falloff), marks the tiles modified/dirty, and rebuilds the affected chunk's merged
  mesh+collider. Hooked in `SpellCaster.ResolveZone` — zone Earth spells deform the ground at impact
  before damage resolves.
- **Water school** — root **"Water Bolt"** (magic_water, Focus 14, projectile, Wet). L1: Tidal Stream
  (beam), Whirlpool (vortex), Mist Veil (zone), Deep Mind (Int+3), Healing Spring (instant heal).
  L2 tables ×5 (Tsunami storm, Maelstrom, Downpour, Healing Tide, etc.) — wet-applying throughout.
- **Earth school** — root **"Stone Shard"** (magic_earth, Focus 15, projectile). L1: Boulder Crash
  (zone knockback), Tremor (**Ring** deform), Spire Field (**Spikes** deform), Earth Bulwark (Def+3),
  Stone Effigy (summon). L2 tables ×5 — Ring/Spikes deform escalates (Seismic Ring, Epicenter, Stone
  Pillars, Crystal Field, Colossus, etc.).
- **Wheel labels** — `CharacterInfoUI.BuildWheel`: the compact-wheel school bubble never set its label
  text; now displays the root skill's name (`slbl.text = root.displayName`) — Magic grows from 7 to 9
  school wedges (Water Bolt / Stone Shard included).
- Docs: game-design.md §3.7 (Wet row, 6→7 statuses, signature list Water→Wet / Earth→terrain),
  §3.8 (`terrain shape` bullet), §3.3 (9 L1 roots; Magic 16/80/400/496, TOTAL 2046).

### 1af-status
- No CLI build — code-review verified. Play-test pending: learn/arm Water ("Wet") and Earth (Tremor /
  Spire Field deform the ground) schools from the magic wheel; verify wet-target Ice/Lightning bonus
  and that deformed chunks persist after reload.
- Compile-fix follow-up (2026-09-14, `ee3b97b`): `WorldStreamer.DeformAt`'s spike hash is now `int`
  (bitwise `&` was illegal on `float`), and the unedited-corner fallback `CornerOrBase` is a non-static
  method so it can call the instance `CurrentHeightOf`. No design change — pure compile fixes.

---

## 1m. Recent completed work (2026-09-12) — magic delivery overhaul (Beam / Summon / Storm) + throwing-hammer fix
User: duplicate-feeling spells across the magic schools should each behave distinctly. Three new spell
deliveries (Beam, Summon, Storm) added alongside projectile / instant / zone / vortex; 16 spells
reworked to use them. Spell **ids unchanged** (learned-skill data safe).
- `SpellData.cs` — `SpellDelivery` += `Beam=4, Summon=5, Storm=6`; new fields `TickInterval=0.5f`,
  `ChannelDrainPerSecond=0f`.
- `SkillCatalog.cs` `Spell()` factory gained optional `projectileSpeed=20f, tickInterval=0.5f,
  channelDrainPerSecond=0f`; new delivery drivers `Assets/Scripts/Combat/Weapons/SpellBeam.cs`,
  `SpellSummon.cs`, `SpellStorm.cs`.
- **Beam (user-approved controls):** the cast fires on the normal cast release (LMB-up at the frozen
  charge level); **holding LMB keeps the beam on while `ChannelDrainPerSecond` FP drains real time**
  (via `TrySpendFocus`; a rejected cast ends without killing a live beam — `StopChannel` runs after the
  spend). Ticks damage/heals every `TickInterval` on the capsule caster→aim; 0.4 s release-grace; ~1.6 s
  fixed-sustain fallback on mobile/no-mouse. `PlayerController.cs:921` blocks re-aim/melee while
  `SpellCaster.IsChanneling` (hold keeps the beam, doesn't retrigger).
- **Summon:** ground-targeted (AoE preview, like zone/vortex). Damage summons = persistent **turrets**
  firing bolts at the nearest enemy (`BoltPowerMultiplier=0.6`, reuses bolt flight via
  `SpellCaster.DecorateProjectile`); `Heals` summons = standing heal aura for `IHealable` allies.
- **Storm:** persistent ground zone striking repeatedly (`StrikesPerTick=2`, `StrikePowerMultiplier=0.8`,
  randomized 0–0.35 s delays via coroutines), element-styled FX (crossed bolt bars for Lightning).
- `SpellCaster.cs` — 3 new switch cases + `ResolveBeam/ResolveSummon/ResolveStorm`, `GroundTarget`
  helper, `IsChanneling`, `StopChannel`, `ForgetBeam`, `DecorateProjectile`.
- 16 reworked spells (ids unchanged): **Beams** — Searing Ray (magic_fireball_scorch_searing, Fire/Burn),
  Arc Storm (magic_chain_arc, Lightning/Stagger), Beacon (magic_focus_holylight_beacon, Holy heal+damage),
  Hunger (magic_dark_devour_hunger, Dark/Rot), Cold Stare (magic_frostbolt_chill_stare, Ice/Frost),
  Storm Breath (magic_gust_stormbreath, Wind/Knockback); **Summons** — Frost Obelisk
  (magic_frostbolt_glacier_wall, ice turret/Frost), Shadow Totem (magic_dark_shadowbolt_pool, Dark/Rot),
  Arcane Rune (magic_ward_aegis, Arcane), Healing Shrine (magic_heal_light, Holy heal aura),
  Ember Effigy (magic_fireball_meteor_ember, Fire/Burn), Gust Totem (magic_gust_airburst, Wind);
  **Storms** — Thunderstorm (magic_chain_overload, Lightning/Stagger), Meteor Rain
  (magic_fireball_meteor_rain, Fire), Blizzard (magic_blizzard in `SkillCatalog.BuildMagic`, Ice/Frost),
  Eclipse (magic_dark_nightfall_eclipse, Dark/Rot).
- **Throwing-hammer fix:** `throwing_hammer` is `WeaponCategory.Ranged` (WeaponCatalog.cs:105) so it
  fired via `RangedWeaponBehavior.FireProjectile`, whose no-prefab fallback spawned a *generated arrow*.
  New `BuildDefaultProjectileVisual()` in `RangedWeaponBehavior.cs` picks a tumbling hammer
  (`BuildHammerVisual`: handle + head + `TumbleSpin` 360°/s) for `id == "throwing_hammer"`, arrows
  otherwise. Checker 0 diagnostics.
- Docs: `game-design.md` §3.8 (delivery behaviors) + §5.16 (beam sustain); `PROGRESS.md` this section.
### 1m-status
- Source-compile verified by the semantic checker (0 diagnostics, run twice). Unity play-test pending:
  beam fire-on-release + LMB-hold sustain draining FP; summon turret bolting nearest enemy / heal aura;
  storm repeated strikes; hammer tumble visual; all 16 reworked spells from staff/wand/book.

---

## 1n. Recent completed work (2026-09-12) — charged magic casts skip the post-release cast time
User: "the endlag on spell cast is crazy … the wait time between the explosion and the projectile is
too long." Root cause: on LMB release the player already spent the wind-up charging, but
`SpellCaster.CastRoutine` then waited another full `SpellData.CastTime` (0.5 s default, 0.8 s Tornado)
between the CastingCircle burst and the actual delivery — a second wind-up after the burst.
- `SpellCaster.cs` `CastRoutine`: the cast-time wait now runs **only for uncharged casts**
  (`charge <= 0f`); a charged cast (`charge > 0`) resolves immediately on release, so the burst ring
  and the projectile/delivery land on the same frame. Applies to every delivery (projectile / instant /
  zone / vortex) and every charging caller (Alt-wheel, class/race casters) since all share
  `BeginCast -> CastRoutine`. Tap-casts keep the short wind-up (0.5 s) unchanged per the user's choice.
- Docs: `PROGRESS.md` this section.
### 1n-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: charge any
  magic spell (staff fireball/wind-blade/Tornado) to ~50-100%, release → burst + bolt appear together
  (no ~0.5 s / 0.8 s gap); tap-cast still shows the brief wind-up; FP cost / cooldown / charge-damage
  math unchanged.

---

## 1v. Recent completed work (2026-09-12) — released the charge cap + real-time mana drain while charging
User: "release the charge limit, and mana would decrease on realtime as player charge the spell." Magic
spells now overcharge past the old 2 s / level-1 ceiling while the focus pool lasts, and FP drains
continuously every aim frame instead of being spent all at once.
- **Charge cap released (magic only).** `PlayerController`: the magic aim branch grows `_chargeAccum`
  unbounded while RMB is held and FP > 0; new `SpellChargeLevel(hold)` = same ramp as the capped
  `MagicChargeLevel` but with no `Clamp01`, so level 1+ keeps scaling power/size/cost. Ranged draws keep
  the 2 s cap (`MagicChargeMaxTime`); the HUD bar and weapon anim stay clamped (represent the "readable"
  band).
- **Real-time FP drain.** Each aim frame: `drain = spell.FpCost × ChargeFpCostBonus × level ×
  FpChargeDrainRate × Δt`, capped at current FP, spent via `SpellCaster.TrySpendFocus` (also stays the
  regen delay), accumulated into `_chargeDrained` (reset on aim start / cancel / release). Charge growth
  **freezes when the pool hits 0** — releasing still fires at the level already paid (never a dud).
- **Prepaid settlement / single cost authority.** `SpellCaster.BeginCast(…, charge, prepaidFocus)`
  spends only `max(0, fpCost − prepaid)` and clamps the charge to what `prepaid + CurrentFp` can cover.
  To keep that coherent, the wheel-cast flat `SkillCost` spend is skipped for `SpellCastEffect` skills in
  `SkillProfile.ExecuteCharged` (drop the `Clamp01` on charge there too) — which also **fixes a
  pre-existing double-spend** (tap-cast cost 2× `FpCost` before; now exactly `FpCost`, full charge
  1.6× `FpCost` as documented). New `SkillContext.PrepaidFocus`; threaded through
  `MagicWheelUI.ReleaseArmedCast` and `SpellCastEffect.Execute`.
- Docs: `PROGRESS.md` this section.
### 1v-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending:
  - Hold RMB + LMB on an armed spell past full charge → FP bar ticks down in real time, casting
    circle / AoE marker keep growing past the old cap, charge stalls at empty FP.
  - Release → burst ring + delivery still land together (1n behavior kept), costs follow
    `FpCost × (1 + 0.6 × level)`.
  - Tap-cast (no hold) → costs exactly `FpCost`, resolves instantly now (cast wind-up removed, see 1w).
  - Cancelling a charge (weapon switch / mount / UI) discards the drained FP — deliberate.
  - Ranged (bow/throwing hammer) draw unchanged.

---

## 1w. Recent completed work (2026-09-12) — magic spam casts instant, FP-only, no more ring-without-bolt
User: "when the player click to spam magic multiple time the magic effect still play but the projectile
did not spawn." Root cause: the local release FX (`BurstCastingCircle` + ring) ran in `PlayerController`
*before* `ReleaseArmedCast`, so a cast rejected downstream (per-spell `SpellData.Cooldown` 4-10 s, the
profile `SkillCost.Cooldown` 2 s gate, or an empty FP pool) still played the ring while no bolt flew —
`ReleaseArmedCast` also returned `true` unconditionally, hiding the reject.
- **FP-only limiter (no magic cooldown).** `SpellCaster.BeginCast(…, fast: true)` skips the `IsReady`
  cooldown gate entirely and `CastRoutine` never writes `_cooldowns` on fast casts; `SkillProfile.
  ExecuteCharged` routes `SpellCastEffect` skills straight to `BeginCast(…, fast: true)` (skipping the
  profile's flat cost + 2 s cooldown gates). The equipped spell's FP cost + the real-time charge drain
  are the only limiter — every click casts as long as mana holds. Non-magic skills, class/race spells
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
  - Spam-click fireball/wind-blade → a bolt spawns on every click, instantly, until FP runs out; no
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

## 1o. Recent completed work (2026-09-12) — sword/shield defense animation + permanent-arm-corruption fix
User: "the sword and shield should have 2 set of animation for attack and defense… it only don't have
animation for that", then "if the player spam attack continuously the model might be bug and got
permanently altered". Root cause of the corruption: no single-owner phase machine — re-entering a
phase before it ended (spam/charge-cancel/guard) leaked `AcquireArms` claims, leaving `SuppressArms`
stuck so `PlayerAnimator` never restored the arms; plus attck/sway "rest" was captured from the live
(aesthetic-posed) bones, so `End()` re-committed a polluted pose as rest.
- `WeaponAnimator.cs`: new **defense guard** — the "defense" set alongside the attack swings. `PlayGuard()`
  / `EndGuard()` / `UpdateGuard()` ease the arms into a held guard pose (eased grab-in 0.18 s) while RMB
  blocking; per-weapon `GuardPoses` (t=1 hold keys): shields raise the face up in front, blades tuck a
  defensive guard, greatsword/warhammer/greataxe raise a two-hand cover, fists/gauntlets boxer guard,
  magic/ranged fall back to neutral (never block).
- **Single-owner phase fix**: `Acquire()`/`Release()` (idempotent, `_ownsArms`), `CaptureRest()` and a
  unified `End()` teardown. Every phase transition (attack/charge/guard/sway) abandons the old hold
  without releasing, so re-entrancy can't unbalance the arm-owner count; `OnDisable` does the same full
  teardown. Arm "rest" is now always **local identity** (the model's documented rest) instead of a live
  capture — a polluted capture can never bake an altered pose in. Weapon transform is still re-captured
  per phase (re-parent safe).
- `CombatController.cs`: `SetBlocking` drives `PlayGuard`/`EndGuard` on the state edge only, and
  `CanKeepBlocking()` (idle + loadout drawn) runs in `Update` so sheathing/stow drops the guard and the
  guard pose never fights the stow idle.
- `PlayerAnimator.cs`: watchdog — any rig driving the arms pings every frame (`PingArms`); if
  `SuppressArms` hangs > 0.5 s with no writer, owners are force-released + one log. Backstop if some
  unrelated flow ever leaks again.
### 1o-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending (guard pose
  angles are starting values — expect to tune):
  1. **No corruption**: spam LMB 30 s single + dual wield, interleave guard/attack/charge spam, walk
     + idle 5 s → arms always return to rest; no frozen/stuck arms; no `[PlayerAnimator] arm-owner
     claim hung` watchdog log during normal play.
  2. **Defense animation**: sword+shield → RMB hold raises the shield arm into a high guard, sword
     arm tucks (attack swings vs guard stance clearly distinct); release → settles back to the ready
     sway; blocked hits keep the guard until stamina breaks (then it drops).
  3. Fists/gauntlets boxer guard; greatsword two-hand cover; guard dropped cleanly on sheathe, and
     re-raised after re-draw while RMB still held.
- **Committed + pushed** with the rest of the session's work — 5 commits (`0063afd..cc3e787`, origin/main):
  `b6ff2ba` feat magic redesign · `9c44ea7` ui magic wheel circles · `ea56467` ui skill tree zoom/labels ·
  `eb9feaa` fix weapons-only start bag · `cc3e787` feat guard animation + arm-leak fix. Worktree clean.

---

## 1p. Recent completed work (2026-09-12) — removed the game-start tool/food seed
User: "remove item that is not weapon from the player inventory … just delete the code that add them
into player inventory when the game start". The non-weapons came from the test-bench seeding, so the
tool-kit spawn was simply deleted — no new inventory plumbing.
- `NewWorldTestGround.cs`: removed `EnableTools`, `SpawnToolKit()` (axe/pickaxe/hoe/hammer/scythe/
  watering_can/fertilizer/club/rosary/fishing_rod ×1 + banh_mi/com_tam/nuoc_dau/mi_chinh/xap_phong ×5)
  and its two call sites in `SpawnBench()` and `GrantBenchBag()`. Weapons-only grants
  (`SpawnAllWeapons`, weapon rack) unchanged.
- Non-weapons can still enter the bag mid-game via pickups/crafting/shops — only the start-of-game
  seeding was removed, as requested.
### 1p-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: new game /
  bench spawn starts with only the weapon grants in the bag (no axe, no food); weapon rack + all
  catalog weapons still spawn; rest of the bench lanes (farming/enemies/buildings/NPCs) unaffected.

---

## 1q. Recent completed work (2026-09-12) — skill tree: aggressive zoom + node labels auto-size
User: "increase the zoom ability of the skill tree and fix the bug that the text is too big compare
to the node". Scope confirmed via question: **aggressive** zoom range (Min 0.08 / Max 10) and
**auto-size** labels that keep wrapping.
- Root cause of the text bug: node boxes and label fonts were sized on unrelated scales — general
  nodes are 18x14 / 12x10 / 8x6 px (`MakeTreeNode`) and class/race nodes are size*2 x size (24/18/14
  tall), while labels used `Screen.height/…` with `Max()` minimum clamps (8/8/6pt general, 10/8/7pt
  class/race) that dominate at 1080p+ — an 8pt font in a 10px box, 6pt in a 6px box, spilling over.
- `CharacterInfoUI.cs` `MakeTreeNode` / `MakeClassTreeNode` / `MakeRaceSkillTreeNode`: labels now use
  TMP auto-size bounded by the node itself — `enableAutoSizing`, `fontSizeMin 2f`, `fontSizeMax`
  derived from the node (`nh * 0.85` general, `size * 0.75` class/race), wrapping kept, with
  `overflowMode = Ellipsis` only as a last-resort for names that can't fit even at min size.
- `TreePan`: `MinScale 0.28 -> 0.08` (full-wheel overview), `MaxScale 3 -> 10` (close reading of tiny
  nodes), scroll step `1.2 -> 1.25` so the wider range is usable. `FitTreeToViewport` already clamps
  to these constants, so the automatic fit is unchanged.
- `FitTreeToViewport`: now also folds `_raceTreeNodes` into the max-radius fit (was omitted — Race
  sub-tab could under-fit).
### 1q-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: scroll-zoom
  all the way in (10x) and out (0.08x) on General/Class/Race sub-tabs; long skill names auto-shrink to
  fit their node and never spill outside it; the Race tree fits the viewport on open; no label blur/
  NRE during zoom/pan.

---

## 1r. Recent completed work (2026-09-12) — magic redesign: school signatures, real healing, persistent zones
User: "the magic is not very creative, most of the magic using the same thing over". Scope (confirmed):
leverage existing/dormant systems rather than add new delivery types — statuses/DoT, real healing,
knockback, and persistent zones. Applied to **both** the base 12 magic tree and the ~70-spell design
bank. Existing FP/cooldown/cast pipeline untouched.
- **School signatures** — Fire→Burn, Ice→Frost, Lightning→Stagger, Dark→Rot, Wind→Knockback (Tornado
  stays a pull), Holy→heals, Arcane→Stagger (bind/hold). Spells that previously differed only by power
  now read distinctly on hit.
- `SpellData.cs`: added `Duration`, `Heals`, `Knockback`; status fields kept with `StatusProcChance = 1f`.
- `SkillCatalog.cs` `Spell(...)` helper: new optional `heals`, `knockback`, `duration`, `statusEffect`
  params — shared by both catalogs. `BuildMagic` base 12 retuned; **Ward**/*Arcane Ward* and
  **Blizzard** converted from weak `Zone` to aimed `Spell` zones (Blizzard persistent 2.5 s + Frost).
- `SkillCatalog.Magic.cs`: L1 blocks (focus/arcane/fireball/frostbolt/dark/gust) and L2 headliners
  retuned — chain fork/arc/overload/leap→Stagger, Scorch Burn, Deep Freeze Stagger, chilled bolts
  Frost, Consume Rot, Mini Tornado→**Vortex** (8 m, pulls), airburst Crack/Pressure/Shockwave knockback,
  Arcane Shackles/Hold Stagger, and the whole heal family (Greater Heal, Light's Embrace, Purify,
  Mending Light, Radiance, Beacon, Sunburst, Regrowth, Restore, Bloom) now carry `heals: true`.
- New `IHealable.cs`; `PlayerController` implements it (uses its existing `Heal`).
- New `SpellDoT.cs` — Bleed/Poison/Rot/Burn ticker (per-tick = power × 0.12 over 4 s; refreshes).
- New `SpellZone.cs` — unified persistent zone (tick damage × per-delivery multiplier — Zone 0.4,
  Vortex 1.0 — optional pull, Holy heals `IHealable` allies inside per tick; lifetime expiry). Replaces
  and deletes `WindVortex.cs`.
- `SpellCaster.cs`: `ResolveZone` routes `Duration > 0` to persistent `SpellZone` and heals allies when
  `Heals`; `ApplyHit` gained heal branch + `ApplyStatus` (proc chance) + `ApplyKnockback`;
  `ResolveDirect` self-heals for Instant heal spells; `SpawnVortex` now spawns `SpellZone` (pull 3.5).
- Docs: `game-design.md` §3.7/§3.8 (signatures, healing, persistent `SpellZone`, fields).
### 1r-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: hit an
  enemy with each school and confirm the status/icons (Burn/Frost/Stagger/Rot) and Wind knockback;
  cast a Holy heal at low HP and confirm self-heal, and a Holy zone near allies; verify Tornado pulls
  and Blizzard ticks + chills; confirm projectiles still never detonate at the caster's feet.

---

## 1s. Recent completed work (2026-09-12) — magic projectile bolts no longer detonate on the caster/feet
User reported magic projectile spells "hit the ground way too often". Root cause: the bolt spawns
exactly at the in-hand rig root (no Muzzle offset — ranged uses a child Muzzle at local (0, 0.1, 1))
and SpellEffect's flight raycast hit EVERYTHING with no owner-root skip. The `~0` layer-mask ray cast
from the hand position clipped the player's own CharacterController capsule on the first frame and
detonated at the caster's feet, reading as a ground hit.
- `SpellEffect.cs` (`Update`): the flight raycast now ignores hits on the caster's own root
  (`hit.collider.transform.root != _caster.transform.root`) and keeps flying — mirrors
  `RangedProjectile.Update`. The splash `OverlapSphere` already skipped the caster's root.
- `SpellCaster.cs` (`FireProjectile`): spawn lifted clear of the body like the bow Muzzle —
  `pos += fwd * 0.5 + up * 0.3`. Applied only to projectiles; Instant/Zone/Vortex placement untouched.
### 1s-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: fire a
  projectile spell straight ahead / slightly downhill / at a close 3–5 m target / with an NPC beside
  the caster — bolt leaves the hand and only bursts on real obstacles; no puff at the feet; no
  self-damage from splash.

---

## 1t. Recent completed work (2026-09-12) — casting circle: two LineRenderer rings on own children
User reported a play-test crash: `NullReferenceException … CastingCircle.Build() (line 144)` on first
magic aim. Root cause (verified via Unity docs/QA): a GameObject can hold only **one** Renderer
component — `gameObject.AddComponent<LineRenderer>()` for the second (inner) ring returns null in
Unity 6, so `_innerRing.useWorldSpace` threw.
- `CastingCircle.cs`: each halo ring now owns its own child GameObject ("OuterRing" / "InnerRing",
  parented at local origin under the CastingCircle transform) before `AddComponent<LineRenderer>()`.
  Visuals identical — `useWorldSpace = false` means both rings still render in local space around the
  circle's origin, which the parent transform positions/rotates onto the weapon.
### 1t-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: hold LMB
  with armed magic → halo + inner spin ring appear (no NRE), charge grows, cast-burst ring still fires.

---
User: "some of the spell in the alt magic circle i'm sure is a physical skill, i want that circle to
consist of magic skills only." Scope confirmed (via question): also align `EnsureArmedMagic` auto-arm.
- `MagicWheelUI.cs`:
  - `RebuildEntries` filter is now `!skill.IsPassive && skill.Type == SkillType.Magic` — melee, ranged,
    stealth, crafting & fortitude castables never enter the wheel. (`Skill.IsMagical` was NOT used: it
    flags elemental flavor and would wrongly include `melee_berserk`/`ranged_arrowrain` and wrongly
    exclude `magic_heal`; `Type` is the correct gate and is carried onto every expanded magic-tree
    branch in `SkillCatalog.ExpandTree`.)
  - `EnsureArmedMagic` aligned: the "keep currently armed" path also requires the armed skill to be
    Magic-type, and both fallback passes arm only Magic-type skills — auto-arm can never pick a
    physical skill as the "magic".
  - Class doc-comment updated ("learned castable magic skill … magic category only").
- Docs: `game-design.md` §5.16 (wheel lists learned magic-category skills only); `PROGRESS.md` this
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
    `< 6` → inner, `< 24` → middle, else outer. 1–6 spells = single inner circle; 7–24 = inner+middle.
  - Per-ring radii (% of canvas height): inner 0.17, middle 0.30, outer 0.42; per-ring base slot sizes
    inner 0.11 / middle 0.085 / outer 0.07, then shrunk by `(2π·r)/(n·GapRatio)` so arcs keep a gap.
    Every ring is a full circle starting at −90° (concentric).
  - `CreateSlot(index, ringIndex, ringCount, skill)`; new `List<float> _slotSizes` (built/cleared with
    slots) so `Paint()` hover hits each slot with its own radius (`size · 0.78`, nearest wins).
  - Removed single-`_slotSize` / `_ringRadius` fields. Hover/cooldown-dim/armed-colour logic unchanged.
- Docs: `game-design.md` §5.16 ("3 concentric circles"); `PROGRESS.md` this section.
### 1v-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: 1–6 learned
  spells (single inner circle), 7–24 (inner+middle), 25–64 (three circles), hover/highlight/dim/armed
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
- Docs: `PROGRESS.md` this section. (`game-design.md` §5.16 already specified the independence.)
- Behavior notes: a shield guard can now stay raised while the other hand swings; a dodge still blocks
  new presses; body-animator attack triggers don't fire during per-hand swings (weapon rigs drive the
  visuals via `NotifyWeaponAnimator(hand, …)`).
### 1w-status
- Source-compile verified by the semantic checker (0 diagnostics). Unity play-test pending: 2 swords
  alternating/spam per hand (independent cadence), sword+shield guard-drop, dodge-mid-swing cancel,
  crossed ranged+melee, both-magic/single unchanged.

---

## 1z. Recent completed work (2026-09-11) — per-hand dual-wield mouse scheme
User: with 2 weapons equipped, make each mouse button drive its own hand (2 swords = LMB/RMB attack
separately); trade-off = no block except via a shield hand; sword+shield = sword side attacks & shield
side guards; magic in a mixed dual loses its charge (fires uncharged); both-magic/0-or-1 weapon keep
the standard scheme; ranged keeps its charge and the buttons cross (LMB→right hand's weapon, RMB→left
hand's) so a bow in the right hand fires on LMB ("the first mouse clicked fires the other one").
- `CombatController.cs`:
  - `HasLoadedDual` (both hands = real non-fist rigs; fists never count), `BothHandsMagic` (both-magic
    keeps the normal aim/charge/fire flow), `HasRangedDual` (crossed-button trigger).
  - Public `CategoryOfHand(hand)` (wraps private `CategoryOf`).
  - `LightAttack()`/`FireRanged(charge)` now resolve through new `HandOf(behavior)`; added per-hand
    `LightAttackWith(hand)` and `FireRangedWith(hand, charge)` (resolve that hand's behavior — the
    dual scheme) that can't be used while blocking.
  - New `NotifyWeaponAnimator(hand, …)` animates only the acting hand in per-hand attacks; the
    existing broadcast `NotifyWeaponAnimators` stays for the heavy/mobile paths.
- `PlayerController.cs`:
  - New `_dualChargeL/_dualChargeR` accumulator per hand; `HandleDualModeCombat` +
    `HandleDualHand` dispatch each mouse button to one hand: Melee = swing on press, Shield = guard
    while held (release unguards), Magic (mixed) = `MagicWheelUI.EnsureArmedMagic` +
    `ReleaseArmedCast(0)` on press (uncharged), Ranged = hold-to-charge (`MagicChargeLevel`) +
    release-to-fire (`FireRangedWith`).
  - `dualMode = FightingMode && !IsMobile && HasLoadedDual && !BothHandsMagic`; guards skip the old
    `_aiming` block, LMB-press block and RMB-block branch while in dual mode. Attack press auto-releases
    a raised guard before swinging. Gates: Not FightingMode (stowed weapons) or mobile ⇒ stock behavior.
- Docs: `game-design.md` §5.4 (Dual hand-state bullet) + §5.16 controls (full dual scheme table);
  `PROGRESS.md` this section.
### 1z-status
- Source-compile verified by review (Unity project — no CLI build, not play-tested).

---

## 1y. Recent completed work (2026-09-11) — shield weapons (Block category)
User: "the game have shield skill but nt a sheild, add it in." User confirmed scope: new
`WeaponCategory.Shield`, 3 shields, shields = strictly better block.
- `WeaponCategory.cs`: new `Shield = 3` (`Melee/Ranged/Magic/Shield`).
- `WeaponData.cs`: new guard mods `BlockAbsorbPercent = 0.8f` (min — default 80% absorb, matching
  the old bare-hand guard) + `BlockStaminaDrainMult = 1f` under `[Header("Per-Category (Shield)")]`.
- New `Assets/Scripts/Combat/Weapons/ShieldWeaponBehavior.cs` (+ `.meta`, guid
  `d118249d90894ed682054489e68be0a2`): `[RequireComponent(HitboxSystem)]`, `IWeaponBehavior` bash —
  mirrors `MeleeWeaponBehavior` (Str-scaled hit, `Completed` fires when the $ hitbox is not active).
  - Repo `.meta` convention (verified this session): metas are the **minimal 2-line format**
    (`fileFormatVersion: 2` + `guid: …`) like `CastingCircle.cs.meta` / `MeleeWeaponBehavior.cs.meta`.
    The first shield commit wrote a full 11-line `MonoImporter` block instead, which Unity rejected
    ("YAML Parsing error — Parser Failure at line 11"), so the asset was ignored and the downstream
    compile died at `WeaponDatabase.cs:28` with `CS0246` for `ShieldWeaponBehavior`. Rewrote it to the
    2-line form (guid kept `d118249d90894ed682054489e68be0a2`) — imports and compiles in Unity.
- `WeaponCatalog.cs`: 3 shields via new `MakeShield` helper —
  `buckler` (wt 2, req 1, base 4, Dex scale, absorb 0.85, drain ×0.6, skill `wskill_buckler`),
  `round_shield` (wt 5, req 3, base 6, Str, absorb 0.9, ×0.7, `wskill_round_shield`),
  `tower_shield` (wt 9, req 6, base 8, Str, absorb 0.95, ×0.8, `wskill_tower_shield`).
- `WeaponModelBuilder.cs`: `BuildBuckler` / `BuildRoundShield` / `BuildTowerShield` proc-cube models +
  dispatcher cases.
- `WeaponRigBuilder.cs`: `Shield` BuildRig case (ShieldWeaponBehavior + databind); `DrawPoseFor`
  shield branch (flat on forearm, `(side*0.08, -0.35, 0.18)`, identity rot — face normal = +Z);
  `StowPoseFor` shield branch (flat on back, `(0, 0.32, -0.26)`, Euler(0,180,0)).
- `WeaponAnimator.cs`: shared `ShieldBashKeys` track (forward jab / lateral sweep / overhead slam);
  per-id defs `buckler`/`round_shield`/`tower_shield` (sd 0.28/0.32/0.38, so 0.40/0.46/0.55).
- Blocking is now shield-aware (`CombatController.cs` + `PlayerController.cs`):
  - `CombatController`: new `EquippedShield` (scans both hands for `Category==Shield`), `HasShield`,
    `BlockTakenMultiplier` (= 1 − shield `BlockAbsorbPercent`, else 0.2) and `BlockDrainMultiplier`
    (= shield `BlockStaminaDrainMult`, clamped ≥0.1). `OnBlockedHit` divides drain by class
    `BlockingMul` again and multiplies by `BlockDrainMultiplier`.
  - `PlayerController.TakeDamage`: blocked hits now use `combat.BlockTakenMultiplier` (was hard-coded
    ×0.2 — updated `if (amount <= 0) return;` guard kept).
  - `PlayerController` RMB gate: `IsMeleeEquipped(combat) || IsShieldEquipped(combat)` (new helper →
    `combat.HasShield`) so a shield-only or sword+shield guard blocks; bow/staff + shield still
    reserves RMB for aim/charge.
- No `NewWorldTestGround.cs` change needed: `SpawnAllWeapons` already loops the whole
  `WeaponCatalog.All` list (EnsureOwned + AddItem), so the 3 shields are granted & rack-displayed
  automatically.
- Docs: `game-design.md` §3.6 (4-category wording, Shield row + Layer 1/2/3 + Notes bullet on the
  off-hand defense / RMB pairing), §5.16 controls line ("block (melee or shield)");
  `PROGRESS.md` this section.

### 1y-status
- Unity integration complete: the `.meta` rewrite (2-line format) fixed both the YAML parse error and
  the CS0246, and the shield script now imports and compiles. Blocking math verified by code review
  only — not play-tested (no human-in-loop fight test yet).

---

## 1x. Recent completed work (2026-09-11) — skill-tree link highlight, magic wheel, casting circle

### 1x-a. Skill-tree links black; clicked node lights its links white
User: "change nodes link to black, add functions that light up the link when clicking a skill."
- `CharacterInfoUI.cs`: `LineInert` and `LineActive` both now `Color.black`; new `LineHighlight =
  Color.white` (~line 141-143). `RefreshSkillTree` picked every `_treeLine` line once more after
  painting: a line goes white if `target.id == selected` OR the selected skill appears in
  `target.PrereqSkillIds` (i.e. the clicked node's direct parent→child links).
- Decision (user-confirmed): the highlight algorithm lives in `CharacterInfoUI.cs`, NOT
  `SkillCatalog.Ranged.cs` / the catalog partials — those stay data-only.

### 1x-b. Magic wheel (Alt) — labels now visible + capacity 16 → 64
Complaint: wheel circles had no text (only the centre hint named the spell) and 16 slots was too
small once the spell pool grew.
- `MagicWheelUI.cs`:
  - `MaxEntries` 16 → 64.
  - New `_slotSize` / `_ringRadius` fields; `RebuildEntries` now sizes dynamically —
    `_slotSize = min(h*0.13f, (maxRadius*2π)/(count*1.15f))`, `_ringRadius = min(max(h*0.28f,
    count*_slotSize*1.15f/2π), maxRadius = h*0.42f)` (h = canvas height in px). Ring grows with skill
    count, slots shrink with 1.15× arc gap so all 64 fit on screen.
  - Fixed latent bug: `CreateSlot` built the label but never assigned `.text`
    (`slot.label.text = skill.displayName`), font `max(7f, slotSize*0.42f)`,
    `enableWordWrapping = false`, `overflowMode = Ellipsis` — long names truncate, full name in the
    centre hint on hover.
  - `Paint` hover hit-radius now `_slotSize * 0.78f` (was hardcoded) so picking stays accurate on the
    small slots.

### 1x-c. Casting circle — halo around the magic weapon while charging (new files)
Feature: visible ring around the weapon during aim/charge so a charging cast reads clearly.
- New `Assets/Scripts/Combat/Effects/CastingCircle.cs` (+ `.meta`, guid
  `0c99a538ff784eab94de853363fa8fc8` since the repo tracks meta files). Lazy singleton
  (`Instance`), prefab-free (builds objects in code like `AoeAimPreview`):
  - Disc (solid translucent) + outer LineRenderer ring (48 seg, 0.06 width) + inner spinning ring
    (36 seg, 0.03 width, faster spin). Orientation: ring plane ⊥ weapon up-axis
    (`Quaternion.LookRotation(up)`).
  - `Show(anchor, charge, color)`: radius 0.35 → 0.75, alpha 0.35 → 1, spin `18f + charge*60f` deg/s —
    all lerped by charge.
  - `Burst(radius, color, upDir)` → one-shot `SkillFx.RingFlash` on cast release.
  - `Hide()` sets all renderers inactive (keeps one-shot rings playing).
- `PlayerController.cs`: new `_castingCircle` field. `UpdateCastingCircle(charge)` called right after
  `UpdateAoePreview` in the magic aim path; on cancel `HideCastingCircle()`; on release
  `BurstCastingCircle(charge)` then `HideCastingCircle()`, then `MagicWheelUI.ReleaseArmedCast(charge)`.
  Helpers `Casting()`, `MagicHand(CombatController)`, `HandIsMagic(GameObject)` (~lines 1140-1210).

### 1x-status
- All three compile-level verified (git diff reviewed; no name conflicts). **Not yet visually
  confirmed in Unity** — next session: play with a magic weapon, Alt-wheel >64 spells, charge and
  eyeball the halo + white link highlight.

---

## 1. Recent completed work (race/class skill trees + general tree layout)

### 1a. Race skill tree system (files created this round)
- `Assets/Scripts/Combat/Skills/RaceSkill.cs` — `RaceSkill` + `RaceMod{kind,amount}` + `RaceModType`
  (31 enum values, including `AttackSpeedMul`, `DefenseMeleeMul`, `HpRegenPerSecond`...).
- `Assets/Scripts/Combat/Skills/RaceSkillCatalog.cs` — 22 race builders (`BuildHuman` … `BuildElf`),
  helper `Make(list, raceId, node, name, layer, passive, cost, prereqs, desc, IRaceEffect effect = null, params RaceMod[] mods)`.
- `Assets/Scripts/Combat/Skills/RaceEffect.cs`, `RaceSkillCaster.cs`.
- `Assets/Scripts/Player/Races/RaceSkillPassiveManager.cs`, `RaceSkillState.cs`.

Guidelines that MUST be preserved:
- Skill id convention: `rac.{raceId}.{node}`; cooldown key: `"race_" + id`.
- `RaceModType` semantics: `*Bonus` = flat add to race stat % modifier; `*Mul` = multiplier (1+Σ);
  `HpRegenPerSecond` / `EquipLoadBonus` = flat adds.
- `DamageType` has NO `Poison` — Serpent-kin venom uses `DamageType.Dark` (RaceSkillCatalog.cs:253).
- Multi-mod `Make(...)` calls: pass `null` for `effect` positionally, or use the named-array form
  `mods: new[] { M(...), M(...) }`. A named `mods:` followed by an unnamed arg is a compile error
  (this already burned us twice: CS8323 then CS1503).
- Current multi-mod nodes: Orc "Thick Hide" (line ~636), Dwarf "Thick Skin" (~975), Elf "Swift Blade" (~1048).
- Catalog is uniform for all races: 1 hub (L0) + 3 paths (L1) + 6 leaves (L2) = 10 nodes.

Integration (all verified to compile before this handoff):
- `PlayerStats.cs` — `AddTemporaryStatBuff`, `GetTotal` now adds race-skill stat bonus + temp buffs,
  many derived stats multiply by `ActiveRaceSkillMods?.XxxMul ?? 1f`. `DevMaxAllStats` stays `true`.
- `SkillProfile.cs` — `Execute` falls back to `RaceSkillCatalog`/`RaceSkillCaster` for `rac.*` ids.
- `RaceChangeManager.cs` — `ApplyRace` now always refreshes/ensures the race-skill components
  (`RaceSkillPassiveManager.Refresh()` + `RaceSkillState.SetRace(...)`).
- `CharacterInfoUI.cs` — new Race tab/tree/detail/hotkey-assign wiring.

### 1b. General skill tree layout recalc (JUST DONE — not yet visually confirmed)
Complaint: "Layer 1 has too many slots; skills that branch from layer 1 are taking those slots."
- Root cause is DATA, not layout: `SkillCatalog.ExpandTree` (SkillCatalog.cs:381) makes 5 L1 + 25 L2
  children per base skill ⇒ 2,046 nodes (66 L0 + 330 L1 + 1650 L2). L1 ring capped at ~60 slots per
  wedge so it overflowed; the 130px L1→L2 moat wasn't enough, so deep skills read as "still on Layer 1".
- User directive (final): "recalculate the space" — reduce the L1 area so L2 skills clearly land on
  Layer 2, WITHOUT reducing the catalog.
- Applied in `CharacterInfoUI.RebuildSkillTree` (constants block ~line 1010):
  - `ring0` 280 → 250; `moatBase` 300 → 250 (L1 ring1 r=500); `branchStep` 16 → 22 (L1 ring2 r=522);
    `moatBranch` 130 → **280** (L2 first ring r=802); `deepStep` 120 → 180; `hubR` 180 → 170.
  - `layerPitch` {20,10,12} → {22,10,14}.
  - Ring-allocation math untouched; L2 is still pinned to ring 3+ (never shares an L1 ring).
- Also verified: ring1 cap now floor(500*1.04/10)=52, ring2 54 → L1 band holds max 70 (Magic) fine.
- NOT yet visually confirmed in Unity — next session should eyeball the tree.

### 1b-update (2026-09-11): ROOT fix — Layer 1 is now true-roots × 5, not 10 bases × 5
The real bug was DATA: `SkillCatalog.ExpandTree` expanded ALL 10 raw L0 skills per category
(5 true roots + 5 hand-authored locked skills) → 55 eff-L1 nodes (Magic 78), not the intended 25.
Fixed in `SkillCatalog.ExpandTree` (SkillCatalog.cs:381): a 4-line pre-pass promotes every
Layer-0 skill that has a prereq to Layer 2 (the deep band), so expansion now only branches the true
roots → L1 = 25 per category (Magic/Fortitude 30, they have 6 roots), L2 = 125 (+ relabeled locks).
`CharacterInfoUI` tier-band comment updated (lines 1001-1008) to match. No layout constants changed.

### 1c-update (2026-09-11): TRUE-depth layering — L2 no longer overfiles, L3 owns the deepest chains
Follow-up complaint: "same problem on layer 2-3" — the flat "promote every lock to Layer 2" fix from
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

### 1d-update (2026-09-11): L1 uses its freed room — bigger nodes, wider gaps, closer to the roots
With L1 now a single row of 25/30, its ring band is repurposed for readability: L1 nodes grow
8x7 → 12x10 and `layerPitch` L1 10 → 16 (edge gap 2px → ~4px; ring1 cap 52 → 30, Magic/Fortitude
fill it exactly), label font bumps 7→8. `moatBase` 250 → 220 pulls ring1 in to r=470 so Layer 0 and
Layer 1 sit closer. Derived ring caps rechecked: r1=470(30), r3=772(57), r4=952(70), r5=1132(84) —
all L2 bands (126-152) and lone L3 nodes still fit.

### 1e-update (2026-09-11): L2 gets real room — bigger wheel, two spacious rings, no catalog cut
Complaint: "layer 2 doesn't have enough space, more than half the skills got pushed up to layer 3".
Root cause: ring3 (r=772) held only 57 L2 nodes vs 126-152, so 55-62% poured onto rings 4-5 (read as
"layer 3"). No catalog shrink (Option C chosen): `moatBranch` 280 → 550 and `deepStep` 180 → 260.
ring3 r=1042 (cap 77), ring4 r=1302 (cap 96) → every category's L2 fits 2 well-sized rings
(Magic 152 = 77+75, ring4 ~78% full), and true L3 (Tornado/Masterwork/Heart-Seeker) sits alone on
ring5 r=1562. L1 band untouched. New derived caps: r3=1042(77), r4=1302(96), r5=1562(116), r6=1822(135).

### 1f-update (2026-09-11): SHELVED — "2 synthetic grandchildren per L1" (skill cut)
Cut synthetic L2 grandkids 5 → 2 per L1 and pitched L2 to 16 so the band fit one ring. User REJECTED
the skill reduction: "i dont want to reduce the amount of skills, i want to keep the amount as it is".
Full catalog restored (5/L1). SUPERSEDED by 1g. Kept only `layerPitch` L2 = 16 from this attempt.

### 1g-update (2026-09-11): L2 keeps ALL skills — balanced 3-ring band, no ring packed to the seams
Complaint: cut is not acceptable; L2 must keep 5 grandchildren/L1 (126-152 nodes/category, ~1034 total).
Physics: that volume can't sit on one ring, so instead of greedily filling the innermost ring until it
is 100% full, `CharacterInfoUI` allocator (CharacterInfoUI.cs:1142) now SPREADS dense bands: each L2
node picks the least-loaded ring of a 3-ring band (rings 4-6 = r=1092/1372/1652), growing the band only
if capacity demands. Every ring ends up ~50-73% full with 6px gaps instead of one seam-packed ring.
Radii: `moatBranch` 550 → 600 (ring3 r=1092), `deepStep` 260 → 280 (r4=1372, r5=1652). L2 caps at
16px pitch: 70/89/107 (Magic 152 = ~51 per ring). True L3 (Tornado/Masterwork/Heart-Seeker) sits singly
on ring6 r=1932 (cap 167). SkillCatalog.ExpandTree back to `ci < 5`; class doc ~1034 restored.
L0 (6 roots) and L1 (exactly 25/30 on ring1) untouched.

### 1h-update (2026-09-11): L2 collapsed to a SINGLE ring — verified against real content files
Re-check after the six SkillCatalog.{Category}.cs content files landed (960 new skills, ExpandTree now
produces layer counts 126/126/152/126/126/151): every L2 child is `Layer=2` and max authored depth is 3
(only Tornado/Masterwork/Heart-Seeker), so no L4 and no overflow exists in DATA. What read as "pushed to
layer 3/4" was the 1g 3-ring L2 spread (rings 3-5) — L2 nodes visually occupied two extra rings.
User chose single-ring L2. Changes in CharacterInfoUI.cs:
- `layerPitch` L2 16 → 10; L2/L3 nodes 10x7 → 8x6 (2px gaps), so one ring seats all 126-152 nodes.
- `moatBranch` 600 → 998: ring3 r=1092 → 1490 (cap 154 @ 10px ≥ Magic 152). ring4 r=1770 (cap 153) = L3.
- Allocator band hard-coded 3-ring for L2 → `band = 1` (single ring; still grows outward only if a layer
  ever exceeds a ring's capacity).
Simulation (real counts): L2 = exactly ring3 for all 6 categories (126/126/152/126/126/151 ≤ 154),
L3 = exactly ring4 (Ranged/Magic/Crafting 1 each); wheel maxR shrinks 1972 → ~1810.

---

## 2. OPEN TASK — axe chopping / pickaxe mining is silently broken (PRIORITY)

### Symptom (confirmed by user answers)
- Game runs, hotbar visible (Casual mode), axe tool selected.
- Left-click on a tree/rock: **no chop mark, tree never progresses/falls**, and **no Console errors**.
- Player earlier reported "chop/mining animation runs" but the follow-up answers supersede that:
  no ChopMark, no progress, no errors.

### What has been ruled out (verified by reading code)
- `git status` shows only 4 modified files: `SkillProfile.cs`, `RaceChangeManager.cs`,
  `PlayerStats.cs`, `CharacterInfoUI.cs` (+ new race/class skill files). Nothing else.
- The full gather pipeline is UNCHANGED and has NO dependency on skills/races/classes/stats:
  - Input: `PlayerController.cs:873` LMB (not FightingMode) → `ToolManager.UseSelectedItem()`.
  - Tool paths: `ToolManager.cs:772-856` (axe) / 838-856 (pickaxe).
  - Stamina gate: `TryUseTool`/`SpendToolStamina` (ToolManager.cs:122-146) — only checks `player.Stamina`.
  - World: `WorldBuilder.TreeChop.cs` (`ChopTree`/`CutTree`, ChopMark creation at line 94) and
    `WorldBuilder.RockMining.cs` (`HitRock`, cracks at `UpdateRockCracks`), 4-hit completions.
- Registration: trees/rocks ARE added to `WorldBuilder._trees/_rocks` during `CreateWorld`
  (`WorldBuilder.WorldEnv.cs:407/437`, called from `WorldBuilder.cs:476-477`).
  `FindTreeRoot` (ToolManager.Pickup.cs:203) matches any parent named `Tree*`.
- Swing animation (`PlaySwing`, ToolManager.cs:274) only fires via `SpendToolStamina` AFTER a
  successful chop/mine — so if nothing ever progresses, the swing shouldn't be playing either.

### Why silent no-chop (ranked hypotheses for next session)
1. **Hit collider → `FindTreeRoot`/`IsRock` mismatch or unregistered root.** If the tree/rock the
   player clicks was NOT created by `WorldBuilder.CreateWorld` (e.g., a static/MapBuilder-built
   decor tree in the active test scene, or a saved-game rebuild), `_trees.Contains(treeRoot)` is
   false ⇒ `ChopTree`/`HitRock` returns false on every click: no mark, no progress, no error. Very likely.
2. **Raycast never hits the tree collider** (layer mask / collider removed by `MeshCombiner` or
   colliderless prefab leaves). Script then never enters the axe/pickaxe branch.
3. `_worldBuilder` null in `ToolManager` (would throw though — user reports no errors, so unlikely).
4. A runtime exception elsewhere silently swallows the click via the pre-gate at line 723
   (`TryUseTool` false ⇒ "Quá mệt!" toast — user would see the toast; they said no errors).

### Fix applied 2026-09-11 (lazy registration + chunk parent traversal) — needs Unity test
Root cause CONFIRMED: the new infinite-world path (`ChunkObject.SpawnProps`, `EnableLegacyGeneration
= false` default) spawns trees/rocks under `TerrainChunk_X_Z` and does NOT add them to
`WorldBuilder._trees`/`_rocks`. The raycast works fine (trunk/branch/rock cubes keep BoxColliders);
the game logic silently rejected the hit:
- Axe: `FindTreeRoot` finds `Tree_X_Z` (prefix match OK) but `ChopTree` -> `_trees.Contains` = false.
- Pickaxe: the rock walk-up stopped only on `"WorldRoot"`, so it walked PAST `Rock_X_Z` up to the
  `ChunkObject`, then `HitRock` -> `_rocks.Contains` = false.

Changes (code, not yet verified in Editor):
- `WorldBuilder.TreeChop.cs` — `ChopTree` (line ~30) and `RemoveTree` (line ~8): lazy-register the
  tree into `_trees` if missing instead of returning false.
- `WorldBuilder.RockMining.cs` — `HitRock` (line ~8) and `RemoveRock` (line ~421): same lazy
  registration into `_rocks`.
- `ToolManager.cs` — rock parent walk-up (pickaxe branch, line ~840) and debris walk-up (axe
  fallback, line ~825) now also stop when the parent is a `ChunkObject` (in addition to `"WorldRoot"`).

### Next session: verify in Unity
1. Open the scene the user tests in (infinite world / chunk path). Play, select axe, chop a tree:
   expect a black ChopMark after the first swing, trunk shrinks and a TreeFelled drops on the 4th hit.
2. Select pickaxe, mine a rock: expect cracks after each hit, rock shatters into RockDebris on the
   4th hit (debris is pick-up-able / smashable).
3. Also verify legacy path still works if `EnableLegacyGeneration` is ever toggled on.
4. If trees/rocks STILL silently do nothing, instrument with `Debug.Log` probes in
   `ToolManager.UseSelectedItem` (as previously planned) — now the most likely remaining cause would
   be the raycast itself missing (line 770), not registration.
5. Known minor leak (pre-existing): chunk-unloaded trees/rocks stay as stale null entries in
   `_trees`/`_rocks`; harmless to chopping (null-guarded) but inflates respawn counters.

---

## 5. Skill tree content design — continuation plan (2026-09-11)

### What's been done
- **SkillCatalog.cs refactored** to `public static partial class SkillCatalog` with:
  - `BranchSlot` class (IsAuthored, Id, Name, IsPassive, Cost, IsMagical, Kind, Effect, Desc).
  - `A(authoredId)` — shortcut for referencing an existing skill from Build*.
  - `S(id, name, effect, desc, cost, kind, magical, passive)` — shortcut for a new hand-written skill.
  - `DesignBank` (Dictionary L1[rootId] → 5 BranchSlot[], Dictionary L2[parentId] → 5 BranchSlot[]).
  - `_design` field + `Design` lazy property + `BuildDesignBank()` calling `Register*Design(bank)`.
  - **ExpandTree rewritten** to read from `Design.L1` / `Design.L2` tables instead of generating
    suffix names. Authored slots (A(...)) resolve existing skills by id; new slots (S(...)) get
    Add(...). Multi-root dedupe via `layer1Ids` preserved.
  - Old synthetic machinery removed: `_suffixesByType`, `_activeElements`, `_passiveStats`,
    `ScaledCost`, `ActiveCostForLayer`, `MakeChildEffect` all deleted.
- **Content files created 2026-09-11** — all 6 partial files now exist:
  `SkillCatalog.Melee.cs`, `SkillCatalog.Ranged.cs`, `SkillCatalog.Magic.cs`,
  `SkillCatalog.Stealth.cs`, `SkillCatalog.Crafting.cs`, `SkillCatalog.Fortitude.cs`.
  `Register*Design(bank)` methods are resolved; `BuildDesignBank()` compiles.
  VERIFIED (static sweep, no Unity compile): per-category L1/L2 tables are exact
  bijections (every L1 child has an L2 table, every L2 key is an L1 child),
  22 authored `A()` refs + 6× L0 root keys all resolve to existing `Build*` ids,
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

### Authored L1 skills (22) — already exist in Build*, placed via A() in tables

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

### Authored d2/d3 locks (10) — already exist, no table entries needed
- d2: melee_execute, ranged_arrowrain, magic_blizzard, magic_gale, stealth_shadowstep, craft_transmute, fort_wall
- d3: ranged_execute (Heart-Seeker), magic_tornado, craft_forge (Masterwork)

### Melee L1 design (25 slots — 4 authored + 21 new)

Root: melee_heavy_mastery (passive, Strength+3)
1. A(melee_tough)
2. S(melee_heavy_sunder, "Sunder", Slash(24, Physical), Stamina(14)) — "A blow that tears through armor."
3. S(melee_heavy_crag, "Crag Breaker", Slash(26, Earth), Stamina(16), Earth, true) — "A downward smash that cracks the ground."
4. S(melee_heavy_goliath, "Goliath Stance", Buff(Endurance, 3), passive) — "Permanent +3 Endurance."
5. S(melee_heavy_skullcrush, "Skullcrush", Zone(2f, 22f, Physical), Stamina(16)) — "A devastating overhead strike."

Root: melee_finesse (passive, Dexterity+3)
1. A(melee_couter)
2. S(melee_finesse_expose, "Expose Weakness", Slash(22, Physical), Stamina(12)) — "A surgical strike that finds the weak seam."
3. S(melee_finesse_flick, "Lightning Flick", Slash(26, Lightning), Stamina(14), Lightning, true) — "A blade flicker as fast as lightning."
4. S(melee_finesse_mirage, "Mirage Blade", Slash(24, Dark), Stamina(16), Dark, true) — "A feint that cuts from a shadow after-image."
5. S(melee_finesse_rhythm, "Blade Rhythm", Buff(Dexterity, 3), passive) — "Permanent +3 Dexterity."

Root: melee_cleave (active, Stamina 10, Slash 18 Physical)
1. A(melee_whirlwind)
2. A(melee_berserk)
3. S(melee_cleave_rending, "Rending Cleave", Slash(26, Physical), Stamina(16)) — "A cleave that bites deep and tears."
4. S(melee_cleave_ember, "Ember Sweep", Slash(28, Fire), Stamina(18), Fire, true) — "A cleave trailing a curtain of embers."
5. S(melee_cleave_tempest, "Tempest Cut", Zone(1.8f, 22f, Wind), Stamina(18), Wind, true) — "A sweeping cut that carries a storm."

Root: melee_lunge (active, Stamina 12, WeaponSkillEffect)
1. S(melee_lunge_piercer, "Piercer", Slash(20, Physical), Stamina(10)) — "A single lunging thrust aimed at vitals."
2. S(melee_lunge_bullrush, "Bull Rush", Slash(22, Physical), Stamina(14)) — "A lowered-shoulder lunge that bowls foes over."
3. S(melee_lunge_hotsteel, "Hot Steel", Slash(24, Fire), Stamina(16), Fire, true) — "A lunge searing the wound as it enters."
4. S(melee_lunge_shockjab, "Jab of Static", Slash(24, Lightning), Stamina(14), Lightning, true) — "A quick lunge crackling with static."
5. S(melee_lunge_longarm, "Long Arm", Slash(28, Ice), Stamina(18), Ice, true) — "An impossibly extended lunge chilling the target."

Root: melee_shieldbash (active, Stamina 14, Slash 22 Physical)
1. S(melee_shield_slam, "Shield Slam", Slash(24, Physical), Stamina(14)) — "A deafening full-body shield slam."
2. S(melee_shield_wallspike, "Spiked Wall", Zone(2f, 20f, Physical), Stamina(16)) — "A bristling shield line that lashes out."
3. S(melee_shield_sunwall, "Sunwall", Zone(2.2f, 24f, Holy), Stamina(18), Holy, true) — "A gleaming shield flare of holy light."
4. S(melee_shield_ironrip, "Iron Riposte", Slash(22, Physical), Stamina(14)) — "Brace and punish an enemy that hit you."
5. S(melee_shield_earthwarden, "Earthwarden", Zone(2f, 22f, Earth), Stamina(18), Earth, true) — "Strike the ground, sending rubble against foes."

### Melee L2 design (125 entries — 5 per L1 parent)

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

### Remaining categories — design approach (not yet drafted)

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
  L1 ~1.3× root power, L2 ~1.7×. Costs via `Stamina(amt)` / `Focus(amt)`.
- **Passive roots can spawn active L1 branches** (and vice versa) — makes the tree varied.
- **Elements**: rotate through Physical/Fire/Ice/Lightning/Holy/Dark/Wind/Earth/Water/Arcane.
  Each L1's 5 children should cover ~3-5 different elements for variety.
- **Authored d2/d3 locks** (execute, arrowrain, etc.) are NOT in L2 tables — they're in the
  build list already and appear alongside designed L2 children via the depth walk.

### Execution order
1. ✅ SkillCatalog.cs refactored (partial, BranchSlot, DesignBank, table-driven ExpandTree)
2. ✅ SkillCatalog.Melee.cs (25 L1 + 125 L2 = 150 entries)
3. ✅ SkillCatalog.Ranged.cs (25 L1 + 125 L2 = 150 entries)
4. ✅ SkillCatalog.Magic.cs (30 L1 + 150 L2 = 180 entries)
5. ✅ SkillCatalog.Stealth.cs (25 L1 + 125 L2 = 150 entries)
6. ✅ SkillCatalog.Crafting.cs (25 L1 + 125 L2 = 150 entries)
7. ✅ SkillCatalog.Fortitude.cs (30 L1 + 150 L2 = 180 entries)
8. ✅ Sweep: unique ids, prereq resolution, per-category counts, no orphan refs
   (static grep verified 2026-09-11 — see "Content files created" note above)
9. 🔲 Update PROGRESS.md counts (left in table form — current table still matches), commit with fix:/ui: prefix

### Known issues
- ~~SkillCatalog.cs class doc says ~1034 but actual count is ~1002~~ — FIXED (class doc now says ~1002;
  the 1002 total = 6 cats: 156/157/189/156/157/187 = L0+L1+L2+L3 per 1c-update counts).
- Dev saves referencing old `*_b1..b5` ids will lose those unlocks (acceptable — full content redesign).
- No Unity compile available — verification is static (grep for id graph) + user eyeball.
- `Assets/unused script.md` remains untracked — do not commit.

---

## 3. Parked / not started
- **Unused-file cleanup** (analysis delivered, waiting on user decision — do NOT act without one):
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
- Race skill tree: no point economy (auto-grant all) — by design for testing; revisit later.

---

## 4. Environment notes
- Windows, Unity project at `D:\unity\new world\new-world`. Shell is PowerShell 5.1 (no `&&`).
- Cannot compile/run Unity from this environment — verification is read-only code review only.
- CRLF warnings on `git diff` are cosmetic; do not "fix" line endings wholesale.
- There is an odd stray file `Assets/unused script.md` (untracked) — likely a leftover, unverified.