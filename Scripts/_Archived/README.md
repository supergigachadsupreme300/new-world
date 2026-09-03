# _Archived — Preserved Structure Code

This folder is a **safety archive** of the previous game systems that have been
retired when the project's genre changed from *"CountryLife"* (a Vietnamese
life/farming simulator with scripted endings) to an **open-world action RPG**.

The archived code is intentionally **preserved, unchanged**, so it can be
re-visited or re-implemented in the future. It is NOT part of the active build
entry path, but (where referenced) the live scripts keep small compatibility
shims so the project continues to compile.

> Important: Files listed here are snapshots. Do not edit them in place expecting
> a live effect — edit the live files in the main `Scripts/` tree instead.

---

## Why it is archived

| System | Why it was retired |
|--------|--------------------|
| `CutsceneManager/` | The game had 7 scripted narrative endings (Happy, Sad, Fated, Demon, Justice, NTR, Blackmail, BossBad). With the genre change to an ongoing open-world RPG there are **no endings**. The cutscene routines are preserved here for possible future re-use as narrative quest moments. |
| `WorldBuilder/` | The legacy voxel-cube world map. The open world is now generated as a **seed + coordinate chunk terrain** (see `Scripts/World/`). The voxel world builder is kept for future re-implementation. |
| `Quests/` | The linear Vietnamese story quest chain (`QuestManager`) and scripted random events (`RandomEventManager`) no longer fit an open-world RPG. |
| `Enemies/` | The legacy `EnemyController`, `Mob`, and the `EnemyModelBuilder`/`BossModelBuilder` voxel-model builders. They were moved here so the new open-world enemy AI (`Scripts/Combat/AI/`) can own the canonical `EnemyController` name. Live references (GameManager, WorldBuilder, RandomEventManager, pets, cutscenes) were neutralized — the old enemy runtime is fully retired. |

---

## How the live code stays compiling

Because dozens of live scripts (`GameManager`, `EnemyController`, NPCs, tools,
UI, pets...) still reference the archived types, the live copies under
`Scripts/` are **kept in place** but disabled at runtime:

- `CutsceneManager.RemoveEndings = true` (default) routes every ending entry
  point to a no-op so endings can never trigger during play.
- The legacy `Enemies/` types are **not** kept live — they were moved out and all
  live references removed, so the new `Scripts/Combat/AI/EnemyController` class
  can replace them without a name collision.

---

## What to do for a future re-implementation

- **Endings as quest moments:** copy the routines from `CutsceneManager/` back
  into the live manager and set `RemoveEndings = false`, or expose them as quest
  cutscenes.
- **Voxel worlds / blueprints:** copy `WorldBuilder/` files back and wire them
  into `GameBootstrap`, then re-enable the world build.
- **Story quests:** copy `Quests/` files back, adapt to the open-world quest
  system, and re-register in `QuestManager`.
