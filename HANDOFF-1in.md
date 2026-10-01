> **SUPERSEDED IN PART BY 1io.** The lane key changed: **F1 was already bound** to the combat-mode
> toggle (`Player\PlayerController.Interactions.cs:521`, `Keyboard.current.f1Key`), so pressing F1
> ran the audit AND toggled fighting mode. The lane is now **F13**. Any readout taken on F1 is
> void - re-take it on F13. See `PROGRESS.md` 1io and `THINKING.md` H59-H61.

# Handoff - 1in crater audit lane

**Written:** end of session, 2026-10-01
**HEAD:** `8f7ed9f` (pushed to `main`, `10f2500..8f7ed9f`)
**Branch:** `main`
**Rule 3 reminder:** no Unity build or play-test runs in this project. You are the compiler.

---

## TL;DR

1in shipped the **measurement** for the crater/deform shape problem and **no behaviour change**.
The craters look exactly as they did before. That is deliberate - AGENTS rule 7 requires measuring
before fixing, and the fix (1ex, a stored fine lattice) cannot be chosen from looking at it, because
three different mechanisms produce this same picture.

Skills: none applied. The installed `scenario-unity-*` skills govern driving a running editor over
MCP or in `-batchmode`, which rule 3 forbids here. They could be informative but could not verify
anything, and per rule 15 that difference must be stated rather than assumed.

---

## What shipped

### New file: `Assets/Scripts/World/Streaming/WorldStreamer.CraterAudit.cs`

A new `partial class WorldStreamer` exposing `CraterAudit()`. Strictly read-only: it reads tile data
and the resident mesh, and rebuilds / re-stamps / polls nothing, so every number describes the frame
the key was pressed on.

| Section | Measures | Question it settles |
|---|---|---|
| **A fingerprint** | `(BuildStamp, MeshStep, vertexCount)` buckets across the loaded set | the PREMISE for B/C/D |
| **B resolution** | deepest dished corner, corners spanned, dish radius vs the render path's sampling gap | "drawn" vs "smaller than promised" |
| **C profile** | per-ring min/mean/max dig out to the dish edge | monotone cone vs bowl-with-rim |
| **D expressibility** | corner spread vs the 1ew trigger, corners **above** pristine, adjacent-corner gaps | "never authored" vs "authored but coarse" |
| **VERDICT** | one line, keeps three outcomes distinct | where to walk and why |

**Why A runs first.** Rule 11: an un-restarted session can hold chunks built by two generator
versions. Every in-memory check still passes, because nothing in memory is wrong - it just is not the
code you think is running. A static proof about a generator is a claim about the premise, not about
the screen, so it goes ahead of the sections that look more like tests.

**Why D counts corners above pristine** instead of inferring a rim from the profile: a rim is a
*positive raise*, and the crater profile in `WorldStreamer.Deform.cs` is a monotone smoothstep dish
(`target = current - s * CraterStep`) that can only lower. This is the finding that stopped me from
"just adding sub-tile detail".

**Why D is scoped to the crater footprint** rather than the search band: the band locates the crater,
it is not the crater. The `Wall` / `Ring` / `Pillar` deform profiles *do* raise, so an unrelated one
nearby would otherwise be counted as this crater's rim.

**Why B reports `nodeGap n/a` at step 0:** at the 1ia default (`LowPolyFacets` off) every 1 m corner
is sampled, so the dish is drawn **in full** and the limit is SHAPE, not resolution. Saying so stops
the next reader "fixing" a carve that is already visible.

### Modified: `Assets/Scripts/Opt/NewWorldTestGround.cs`
- `EnableCraterAudit` (default **on**), `CraterAuditKey` (**F13** now; F1 as shipped - see banner),
  `_craterAuditText`
- Poll inserted before the unrelated early returns
- `RunCraterAudit()` - caches a headline for the HUD, logs the full table to the console
- HUD append, same persistence rule as F3/F4: the headline stays up until the next press

### Modified: `tools/StaticChecks.ps1`
`WorldStreamer.CraterAudit.cs` added to `$files` (rule 3). It is a `WorldStreamer` partial, so only
checks 1, 4 and 7 apply to it.

### Docs
- `PROGRESS.md` - new `## 1in` entry at the top
- `THINKING.md` - new `## 1in` section, H54-H58 marked confirmed/rejected
- `AGENTS.md` - rule 3 gained the declaration-order blind spot; rule 8 gained the corner-height convention
- `game-design.md` - F1 lane section + `EnableCraterAudit` row in the QA-toggle table

---

## Two mistakes the first draft made

Both were caught by rereading, not by any automated check. Both are now written into `AGENTS.md`.

**1. A half-metre bias.** The profile ring sampled `FloorToInt(cx + 0.5 + cos*r)`. Corners sit *at*
integer world coords (`ChunkData.Size == 1`; `WorldStreamer.ChunkBuild.cs:293-295` seeds corner
`(gx,gz)` from `GetHeight(seed, tc.X*cs+gx, tc.Z*cs+gz)`), so every ring sat half a metre out and the
numbers were quietly plausible. I had copied `CurrentHeightOf`'s convention, which really does sample
`(cx + 0.5, cz + 0.5)`. Two spellings of "a corner's height" exist in this codebase; read the
**writer**, not the reader that looks like it.

**2. A use-before-declaration.** `dishSpan = span;` written above `float span = 0f;` (CS0103).
`StaticChecks.ps1` reported `braces 44/44 parens 263/263` on a file that could not compile - the
script has **no declaration-order check**, so balance is not reachability. I did not add a naive
scanner, because one that fires on every legitimate field read trains the next reader to ignore it.

---

## Verification performed

- `tools\StaticChecks.ps1` - **0 candidates**; `WorldStreamer.CraterAudit.cs braces 44/44 parens
  263/263`; `NewWorldTestGround.cs braces 163/163 parens 967/967`
- grep + reread of every referenced member: `_loadedData`, `VoxelTerrainEnabled`,
  `EffectiveLowPolyStep`, `EffectiveRefineThreshold`, `ChunkData.IsValid`,
  `TerrainNoiseGenerator.GetHeight(long,float,float)`, and the corner owner table against
  `CurrentHeightOf` slot-for-slot
- grep "confirmed" **no `Key.F1` binding** anywhere in `Assets/Scripts` — **this was wrong.** The
  combat-mode toggle binds it as `Keyboard.current.f1Key` (`PlayerController.Interactions.cs:521`),
  the property-name spelling, which the grep pattern did not cover. 1io moved the lane to F13 and
  added `StaticChecks.ps1` check 8 so this cannot recur.
- **NOT compiled. NOT play-tested.** No Unity build runs in this project.

---

## What you need to do in Unity

1. **Compile.** Paste any console errors - I will fix them in a **new** commit, never an amend, and
   will sweep the whole class rather than patching the one line.
2. **Stand next to a fresh dent** and press **F13** (F1 also toggles fighting mode - see 1io). Do it
   for each of the three sources: projectile impact, Earth Crater spell, tool dig. Paste the whole
   table each time.
3. **Read section A first.** If it says `buildStamp MIXED`, restart Unity before trusting B/C/D.
4. **Tell me which look you are chasing:** the missing rim, the sinking neighbours, or the walls.

Pending play-tests carried over from earlier commits:

- **F2** (`e658acb`) - standing and walking captures; confirm the render-scale readout and the CPU/GPU
  split
- **Magic weapon rest pose** (`10f2500`) - staff / book / orb / wand, plus reparent-and-equip, ready
  sway, and a melee regression

---

## Next task, once the readout exists: 1ex

**Stored fine lattice.** Interior fine nodes only, with every tile **edge** left bilinear - that is
what keeps the existing edge-linearity no-crack proof and the cross-chunk seams untouched. Geometry
stops at the tile boundary, so this is a resolution change and not a seam fix.

Initial resolution 0.5 m (`RefineSubdiv = 2`, which stores only the tile-centre interior node).
0.25 m can follow via `RefineSubdiv = 4`. Touches `ChunkData` (sparse `Fine` array), the save format
(bump v1 -> v4), `GetDigDepth` (fine-aware), and `ApplyHeightEdits`. A tile refines when it has fine
nodes **or** when its corner spread exceeds the trigger.

Then the crater profile itself: raised rim, depth unchanged at `CraterStep = 1.1f` per cast, rim
bounded so it cannot ratchet upward without limit.

**Caves are deferred.** They currently exist only in the dormant voxel path
(`SculptVoxelCave`); the fine lattice is the infrastructure they need, but it is not the task.

**Do not** flip projectile `emitDebris: false`. That is deliberate 1gb behaviour and needs its own
decision.

---

## Key facts for whoever picks this up

- Crater sources: projectile radius `1.4f`; Earth `Mathf.Max(1.2f, spell.Radius)`; tool digs
  `~0.55f` / `0.5f`. All three ratchet through the same profile.
- The crater reaches `radius + 0.5f`, i.e. 3-4 coarse nodes across - genuinely below the resolution
  where a floor, a wall and a rim are separately expressible.
- `CraterFacetSkirt` is **dormant** while `LowPolyFacets` is off, which is the default.
- `LowPolyFacets` is applied at runtime by the test ground in `Awake`. I found no serialized override
  in any `.unity` or `.prefab`, so its value in your session is unknown to me - section A reports it.
- 1ew's refined 2x2 block fires on corner SPREAD (`DefaultRefineThreshold = 2.5f`), which a ~1.1 m
  crater never reaches; and its fine heights are bilinear from the *same* four coarse corners, so it
  could not add shape even if it did fire. "Just subdivide more" was wrong twice.
