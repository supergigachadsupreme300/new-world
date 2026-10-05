# Magic/ — spell casting, look, effects and UI

Everything that defines *what a spell is* and *what it looks like* lives here. Weapons that happen to
be magic (`MagicWeaponBehavior`, `MagicWeaponMods`, `WeaponCategory.Magic`) stay in
`../Combat/Weapons/`, because they are a **weapon category** driven by `WeaponData`, not part of the
spell pipeline.

| Folder | Owns | Start here |
|---|---|---|
| `Look/` | the single source of a spell's visual identity | **`SpellLook.Resolve(spell)`** — impact/cast/body style, colour, scale. Every consumer resolves through this; do not re-derive a spell's look anywhere else. |
| `Fx/` | what a spell **emits** | `SpellImpactFx.Spawn` (pooled flash; its *shapes* are `../Models/Magic/MagicImpactModelBuilder.cs`), `SkillFx.FallRock` / `SkillFx.BuildRockBody` (the falling rock and its SkyRock styles), `SkillFx.SlashFlash`, `SkillFx.RingFlash` (shared hit reactions — not spell identities; their geometry is `../Models/Magic/SkillFxModelBuilder.cs`), `CastingCircle` (geometry in `../Models/Magic/CastingCircleModelBuilder.cs`) |
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
  `Fx/SkillFx.cs`. **Still unnamed on purpose, and 1jd narrowed the reason.** Its geometry is now in
  `../Models/Magic/SkillFxModelBuilder.cs` (1jd), so `SkillFx` is no longer holding shape code for the
  flashes. What still keeps the rock here is that `BuildRockBody` is driven by
  `SpellLook.SkyRockStyle` and by the falling rock's own per-cast animation, so its lifetime belongs to
  `FallRock`; lifting it out would only move the *spawn* call away from the code that decides when to
  spawn. It is a skill model, not a spell model.
- **The four magic *weapon* models** (staff, holy book, bone wand, control orb) —
  `MagicWeaponModelBuilder`, in `../Models/Magic/` (1ja). `Models/WeaponModelBuilder.cs` still holds
  the shared palette and `MakeBlock` they import, and its dispatch still lists all **18** weapons
  (14 non-magic here + those 4).

## What 1jd added: every spell effect has a model file too

Before 1jd the answer above was "most of it". Nine more spell/skill bodies were still inline in the
component that spawns them, so *"where is the code that builds the casting circle?"* meant reading
`CastingCircle.cs`. Each is now a named builder in `../Models/Magic/`, and each call site keeps the
**per-frame** work (pulse, orbit, fade, spin) in the component:

| Builder | Owns | Call site keeps |
|---|---|---|
| `SpellBeamModelBuilder` | line body, funnel discs + debris, the **shared** tip orb | `SpellBeam.PulseVisual` — the funnel flare and debris orbit |
| `SpellZoneModelBuilder` | pull funnel, ground zone disc + halo | the zone's own lifetime |
| `SpellStormModelBuilder` | lightning bar pair, `BoltFader` | `SpellStorm`'s strike scheduling |
| `SummonModelBuilder` | totem body, familiar summoning circle | `SpellSummon`'s pulse |
| `SkillFxModelBuilder` | slash + ring flash geometry, `SlashFader`, `RingFader` | nothing — these are one-shot |
| `CastingCircleModelBuilder` | all ten pieces (disc, two rings, rune + ticks, hex, spokes, arc, waves) | `CastingCircle`'s per-frame pulse and rotation |
| `AoeAimPreviewModelBuilder` | footprint disc, edge ring, beacon | `AoeAimPreview`'s pulsing |
| `CcZoneFxModelBuilder` | the CC ground ring | nothing — one-shot |
| `WeaponProjectileModelBuilder` (in `../Models/`, not `Magic/`) | arrow, throwing hammer, `TumbleSpin` | `RangedWeaponBehavior`'s aim |

Two conventions these nine established, so the tenth follows them:

- **A builder returns handles, it does not keep them.** Where a component needs per-frame access it
  gets a record back (`LineBody`, `TipOrb`, `Circle`, `Piece`) instead of the builder holding state.
  The alternative — a builder that remembers its last ring — is a second owner of a transform.
- **The public API stays where the callers already are.** `SkillFx.SlashFlash` and
  `SkillFx.RingFlash` are still the entry points, because 20-odd non-spell callers use them; only the
  geometry moved. A move that renames the public entry point is a second task, not a free side effect.

## Two rules that survive a move

- **Partial classes stay together.** All five `SpellCaster.*` files are in `Cast/`. 1jb moved the
  geometry *out* of the group rather than splitting it, so this rule and a named model file are not in
  conflict: do not split the partials to make something easier to find; add a line to this file.
- **No `using` was needed.** There are no namespaces in this project and no `.asmdef`, so a folder is
  an organisational unit only — moving a file can never break compilation. What *can* break is a
  `.meta`: every move here carries its `.meta` with it, and the check that proves it is that the
  `.cs` count and the `.cs.meta` count under `Assets/Scripts` are equal.