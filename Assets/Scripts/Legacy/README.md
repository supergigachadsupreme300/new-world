# Legacy/ — the OLD game. Read-only.

Everything in this folder belongs to a **previous, different game** that shares this repository. The
current game is the streamed voxel world (`World/Streaming`, `World/Terrain`, `World/Chunks`) and the
systems built on top of it. None of the code here is part of that game.

**Rule 18 in `AGENTS.md` is the contract: read-only, in both directions.**

- **Do not edit, rename, move or delete anything in this folder.** That includes "just a typo" and
  "just a comment that names a file that moved". Untouched has to keep meaning *still runs*:
  `Assets/Scenes/SampleScene.unity` — the only scene in `EditorBuildSettings` — still carries a
  `WorldBuilder` and a `CutsceneManager` component, so the old game still boots out of this folder.
- **Do not add new code here, and do not resurrect a legacy symbol outside this folder.** This is a
  one-way door, not a shared workbench.
- **Live code may still call INTO here.** 23 live files name `WorldBuilder`, 15 name `MapBuilder`,
  11 name `CutsceneManager` — the legacy types are the hub the new systems hang off
  (`GameManager`, `ToolManager`, `GoblinPet`, `PetController`, `ChunkObject`). What is forbidden is the
  other direction: legacy gaining a **new** dependency on live code, because that is how old-game
  behaviour starts failing when new code changes.
- **If a task needs a behaviour that only exists here, build a NEW class outside this folder.** Copy the
  idea, not the file; then diff what you copied against the source so you know what changed
  (`AGENTS.md` rule 17 — a move's diff proves nothing).

| Folder | Files | Owns |
|---|---|---|
| `Cutscenes/` | 11 | `CutsceneManager` + the ten `Ending*` / `Driving` / `Helpers` partials — the old game's cutscene and ending system |
| `WorldBuilder/` | 12 | `WorldBuilder` + its partials — the old game's block-built village: blueprints, part builders, persistence/save keys, NPCs, farming, mining, lights |
| `MapBuilder/` | 10 | `MapBuilder` + its partials — the old game's prop/furniture builder: houses, mansion, nature, NPCs, police, restaurants, stores, vehicles |

## Notes for anyone reading or grepping

- **Part keys are save keys.** `Church_*` / `Shrine_*` / `Pagoda_*` (and the building keys in
  `WorldBuilder.Persistence`) are written to save files and dispatched by `SpawnStructurePart`. A renamed
  key or a deleted `case` builds **nothing, silently** — the village loads and looks fine. This is why
  `tools/StaticChecks.ps1` still READS `Legacy/WorldBuilder/WorldBuilder*.cs` (checks 2/3/6, part-key
  parity), and why a finding inside a `Legacy` file is a report of a rule-18 violation, **not** a fix queue.
- **`MapBuilder` is load-bearing for the new world.** `World/Chunks/ChunkObject.cs` calls
  `MapBuilder.BuildTree` / `BuildStone`, so the streamed terrain draws its props through quarantined
  code. "Under Legacy" reads as cosmetic; it is not.
- Paths in old notes, old `PROGRESS.md` entries and old commits refer to the pre-`1jc` locations
  (`Scripts/Cutscenes/`, `Scripts/World/WorldBuilder*.cs`, `Scripts/Models/MapBuilder/`). The files moved;
  the GUIDs did not, so every serialized reference still resolves.