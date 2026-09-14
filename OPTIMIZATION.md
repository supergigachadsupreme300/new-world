# Optimization Plan — "New World"

Read-only code audit of `Assets/Scripts` (327 files) performed 2026-09-14. This file tracks the
optimization sweep; each phase is a separate commit, verified by code review per AGENTS.md (no CLI
build; user play-tests in Unity afterwards), and recorded in `PROGRESS.md`.

## Findings (ranked)

### Per-frame CPU/GC
| # | Hot spot | File:Line | Fix |
|---|----------|-----------|-----|
| 1 | Every enemy scans `OverlapSphereNonAlloc` every frame (up to 60 live) + `GetComponent<PlayerController>` per collider | `Combat/AI/EnemyController.cs:200` | Stagger scans (~4 Hz, per-enemy offset); cache player ref; squared distances |
| 2 | HUD: `GetComponentInChildren<SpellCaster/PlayerStats>` ×3/frame + `UpdateLabel` string rebuild every frame | `UI/NewWorld/PlayerBarsHUD.cs:117,176-212` | Cache refs once; dirty-check label text (the `_last*` pattern already exists for fills) |
| 3 | Skill bar: `new List<KeyValuePair>` + `Sort` + per-slot strings every frame while fighting | `UI/NewWorld/SkillBarHUD.cs:75-92` | Rebuild rows only on binding/skill change; cache component refs |
| 4 | Spell ticks use **allocating** `OverlapSphere/Box/Capsule` | `SpellZone.cs:66`, `SpellStorm.cs:89,121`, `SpellBeam.cs` tick, `SpellSummon.cs:125`, `SpellTornado.cs:81`, `HitboxSystem.cs:95-103` | Shared static NonAlloc buffers |
| 5 | `new List<string>(_cooldowns.Keys)` every frame | `Combat/Weapons/SpellCaster.cs:98` | Iterate entries; dirty-flag the key growth |
| 6 | Tornado: 30u `OverlapSphereNonAlloc` + per-block/per-pulled transforms every frame | `World/TornadoBehavior.cs:157` | Throttle pull to ~4 Hz; skip un-pulled block math |
| 7 | **No pooling** — every projectile/zone/FX/DamageNumber is `new GameObject`/`Instantiate` + `new Material` | `SpellCaster.cs:438-577`, `SpellStorm.cs:153-183`, `DamageNumber.cs`, `RangedWeaponBehavior.cs`, `SkillFx.cs` | Pragmatic pooling of DamageNumber + zone/storm FX; reuse materials |
| 8 | Camera/grounding SphereCasts + Raycasts every frame | `ThirdPersonCamera.cs:108`, `CameraModeSwitch.cs:146`, `OpenWorldGrounding.cs:31` | Throttle to ~10-15 Hz |
| 9 | Aim previews recompute 100-180 line positions/frame while charging | `ProjectilePathPreview.cs:103`, `AoeAimPreview.cs:66`, `CastingCircle.cs:93` | Only while charging (already); reduce when stale |

### World streaming / terrain
| # | Hot spot | File:Line | Fix |
|---|----------|-----------|-----|
| 10 | `ChunkSaveManager.TryLoad` is **never called** — every chunk re-runs 961×(5-octave Perlin) from noise | `World/Chunks/ChunkSaveManager.cs:38`, `WorldStreamer.cs:221-272` | Call `TryLoad` before generating; only generate on cache miss |
| 11 | DeformAt: synchronous per-tile file writes (250+ on one Earth cast) + whole 900-tile chunk rebuild + full collider rebake per cast | `WorldStreamer.cs:439-447,462-575` | Batch dirty tiles into one async save; rebuild only touched sub-region |
| 12 | Main-thread finalize burst: up to 8 chunks/0.1s × (mesh upload + 7200-tri collider cook + ~450 prop GOs, 900 `new Random`/chunk) | `WorldStreamer.cs:282-298`, `ChunkObject.cs:64-85` | Time-budget finalize; spawn props incrementally; share one `Random` |
| 13 | Prop material leak: `r.material` per cube (not `sharedMaterial`) — never freed | `MapBuilder.Nature.cs:219-304` | `sharedMaterial` + shared cube meshes |
| 14 | Mesh uploaded via 5 sequential setters (5 dirty+upload passes) | `ChunkMeshGenerator.cs:206-212` | `SetVertices` etc + one `UploadMeshData(false)` |

### UI
| # | Hot spot | File:Line | Fix |
|---|----------|-----------|-----|
| 15 | Skill tree destroys+recreates ~3,000 GOs on rebuild; re-paints whole tree on every open/node click | `UI/NewWorld/CharacterInfoUI.cs:1030,1573-1636,3072` | Cache/pool nodes+lines; re-paint only on data change |
| 16 | Magic wheel rebuilds up to 64 slots on every Alt-press; Paint does per-slot `GetComponent` + string every frame | `UI/NewWorld/MagicWheelUI.cs:167-247,362-427` | Pool slots; cache components; dirty-check armed chip |

### Startup
| # | Hot spot | File:Line | Fix |
|---|----------|-----------|-----|
| 17 | ~30 full-scene `FindAnyObjectByType` scans + all manager init synchronous | `Core/GameBootstrap.cs:15-80` | Registry to cache lookups; split init across frames |
| 18 | Boot sync spawn-chunk build + 61×61=3,721 platform noise samples | `GameBootstrap.cs:107`, `Opt/NewWorldTestGround.cs:237-246` | Reuse spawn-chunk corners for platform snap |

### P0 — compile risk (precondition)
- `EntityId` + `GameObject.GetEntityId()` referenced in `Combat/Weapons/HitboxSystem.cs:40,107` and
  `Opt/ObjectPooler.cs` but **defined nowhere in Assets** (verified). Legacy commits introduced the
  names but never the types. Fix: revert both files to `int`/`GetInstanceID()` (Phase 0).

## Phases

- **Phase 0** — compile risk: `int` + `GetInstanceID()` in `HitboxSystem` / `ObjectPooler`.
- **Phase 1** — frame-rate: enemy scan stagger (#1), tornado pull throttle (#6), camera throttles (#8).
- **Phase 2** — HUD/UI allocs: `PlayerBarsHUD` (#2), `SkillBarHUD` (#3), `MagicWheelUI` (#16), compass/enemy-bar caching.
- **Phase 3** — spell/FX GC (#4, #5, #7): NonAlloc buffers, cooldown iteration, pragmatic pooling of
  DamageNumber + zone/storm FX, material reuse.
- **Phase 4** — world streaming (#10-#14): wire `TryLoad`, batch DeformAt saves + sub-region rebuild,
  time-budget finalize, incremental props, `sharedMaterial`, single mesh upload.
- **Phase 5** — skill-tree UI cache/pool (#15).
- **Phase 6** — startup (#17, #18).

## Status

- [x] Audit + plan written
- [x] Phase 0 — EntityId fix
- [ ] Phase 1 — enemy/camera/tornado frame-rate
- [ ] Phase 2 — HUD/UI allocations
- [ ] Phase 3 — spell/FX GC + pragmatic pooling
- [ ] Phase 4 — world streaming
- [ ] Phase 5 — skill-tree UI
- [ ] Phase 6 — startup