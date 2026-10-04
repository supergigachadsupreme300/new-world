# Magic/ — spell casting, look, effects and UI

Everything that defines *what a spell is* and *what it looks like* lives here. Weapons that happen to
be magic (`MagicWeaponBehavior`, `MagicWeaponMods`, `WeaponCategory.Magic`) stay in
`../Combat/Weapons/`, because they are a **weapon category** driven by `WeaponData`, not part of the
spell pipeline.

| Folder | Owns | Start here |
|---|---|---|
| `Look/` | the single source of a spell's visual identity | **`SpellLook.Resolve(spell)`** — impact/cast/body style, colour, scale. Every consumer resolves through this; do not re-derive a spell's look anywhere else. |
| `Fx/` | what a spell **emits** | `SkillFx.FallRock` / `SkillFx.BuildRockBody` (the falling rock and its SkyRock styles), `SkillFx.SlashFlash`, `SkillFx.RingFlash`, `SpellImpactFx.Spawn`, `CastingCircle` |
| `Cast/` | the spell pipeline | `SpellCaster` + its partials, `SpellData` (the ScriptableObject every spell is authored on), the delivery components `SpellEffect` / `SpellZone` / `SpellStorm` / `SpellTornado` / `SpellSummon` / `SpellBeam`, and `SpellDoT` |
| `Ui/` | the two screens that read all of the above | `MagicWheelUI` (in-game picker), `MagicTestMatrix` (the QA bench that mounts the real builders so a visual change has a readout) |

## "Where is the code that builds a magic model?"

There is no single file, and that is deliberate — the bodies are split by *when* they are drawn, not
by what they are:

- **In-flight body (orb, swarm, generic bolt)** — `SpellCaster.CreateProjectileDisplay` /
  `SpellCaster.BuildProjectileBody`, in `Cast/SpellCaster.Projectiles.cs`. This is also the method
  `MagicTestMatrix` and the QA bench call, so what you change is what you see.
- **Falling rock body and its SkyRock styles** — `SkillFx.BuildRockBody` / `SkillFx.FallRock`, in
  `Fx/SkillFx.cs`.
- **The four magic *weapon* models** (staff, holy book, bone wand, control orb) —
  `Models/WeaponModelBuilder.cs` in `../Models/`. That file builds **all twenty** weapons, so the
  magic four cannot move here without splitting the file; look for `BuildStaff`, `BuildHolyBook`,
  `BuildBoneWand`, `BuildControlOrb` (dispatched by `WeaponModelBuilder.Build(weaponId, …)`).

## Two rules that survive a move

- **Partial classes stay together.** All five `SpellCaster.*` files are in `Cast/` even though only
  `.Projectiles` owns geometry. Do not split the group to make a body builder easier to find; add a
  line to this file instead.
- **No `using` was needed.** There are no namespaces in this project and no `.asmdef`, so a folder is
  an organisational unit only — moving a file can never break compilation. What *can* break is a
  `.meta`: every move here carries its `.meta` with it, and the check that proves it is that the
  `.cs` count and the `.cs.meta` count under `Assets/Scripts` are equal.