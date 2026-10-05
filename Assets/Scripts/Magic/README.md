# Magic/ — spell casting, look, effects and UI

Everything that defines *what a spell is* and *what it looks like* lives here. Weapons that happen to
be magic (`MagicWeaponBehavior`, `MagicWeaponMods`, `WeaponCategory.Magic`) stay in
`../Combat/Weapons/`, because they are a **weapon category** driven by `WeaponData`, not part of the
spell pipeline.

| Folder | Owns | Start here |
|---|---|---|
| `Look/` | the single source of a spell's visual identity | **`SpellLook.Resolve(spell)`** — impact/cast/body style, colour, scale. Every consumer resolves through this; do not re-derive a spell's look anywhere else. |
| `Fx/` | what a spell **emits** | `SpellImpactFx.Spawn` (pooled flash; its *shapes* are `../Models/Magic/MagicImpactModelBuilder.cs`), `SkillFx.FallRock` / `SkillFx.BuildRockBody` (the falling rock and its SkyRock styles), `SkillFx.SlashFlash`, `SkillFx.RingFlash` (shared hit reactions — not spell identities), `CastingCircle` |
| `Cast/` | the spell pipeline | `SpellCaster` + its partials, `SpellData` (the ScriptableObject every spell is authored on), the delivery components `SpellEffect` / `SpellZone` / `SpellStorm` / `SpellTornado` / `SpellSummon` / `SpellBeam`, and `SpellDoT` |
| `Ui/` | the two screens that read all of the above | `MagicWheelUI` (in-game picker), `MagicTestMatrix` (the QA bench that mounts the real builders so a visual change has a readout) |

## "Where is the code that builds a magic model?"

As of **1jb** most of it does have a name. The bodies are still split by *when* they are drawn, but
the shape builders are no longer buried in the class that happens to spawn them:

- **In-flight body (orb, swarm, generic bolt)** — `MagicProjectileModelBuilder`, in
  `../Models/Magic/MagicProjectileModelBuilder.cs` (1jb). `CreateProjectileDisplay` /
  `BuildProjectileBody` and their 10-shape switch used to be `static` methods inside the *casting*
  class, so a bench that only wanted to show a body had to pretend to cast a spell. `SpellCaster`
  still calls it, and `MagicTestMatrix` / the QA bench call it too, so what you change is what you see.
- **Impact flash shapes** (Burst, Ring, Sphere, Cross, Shards, Bloom, Pillar) —
  `MagicImpactModelBuilder`, in `../Models/Magic/MagicImpactModelBuilder.cs` (1jb). Only the *shapes*
  moved: `SpellImpactFx.Spawn` and the pooled `ImpactFlash` still own the budget, growth, tumble and
  fade, because the lifetime of an effect belongs to whoever owns the effect.
- **Falling rock body and its SkyRock styles** — `SkillFx.BuildRockBody` / `SkillFx.FallRock`, in
  `Fx/SkillFx.cs`. **Still unnamed on purpose.** `SkillFx` also owns `SlashFlash` and `RingFlash`,
  which are hit reactions for *every* weapon class rather than spell identities, so lifting the rock
  out would leave the class still mixed. It is a skill model, not a spell model.
- **The four magic *weapon* models** (staff, holy book, bone wand, control orb) —
  `MagicWeaponModelBuilder`, in `../Models/Magic/` (1ja). `Models/WeaponModelBuilder.cs` still holds
  the shared palette and `MakeBlock` they import, and its dispatch still lists all **18** weapons
  (14 non-magic here + those 4).

## Two rules that survive a move

- **Partial classes stay together.** All five `SpellCaster.*` files are in `Cast/`. 1jb moved the
  geometry *out* of the group rather than splitting it, so this rule and a named model file are not in
  conflict: do not split the partials to make something easier to find; add a line to this file.
- **No `using` was needed.** There are no namespaces in this project and no `.asmdef`, so a folder is
  an organisational unit only — moving a file can never break compilation. What *can* break is a
  `.meta`: every move here carries its `.meta` with it, and the check that proves it is that the
  `.cs` count and the `.cs.meta` count under `Assets/Scripts` are equal.