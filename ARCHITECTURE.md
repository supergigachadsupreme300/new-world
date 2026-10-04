# ARCHITECTURE.md — source structure: controller vs. modelling vs. animation

**Status: proposal, nothing moved yet.** Written by 1iz. Every count below was measured with
`Select-String` over an explicitly-built recursive file list of all **365** `.cs` files under
`Assets/Scripts` (not a shallow glob), so a zero here means "read all 365", not "read one level".
Verified by grep + reread only — **no build** (AGENTS rule 3).

The request: *group models and animations close together; the magic models are invisible; split the
controller, the modelling and the animation apart.* This document is the plan. The moves themselves are
staged at the bottom, smallest-blast-radius first, and each one is separately committable.

---

## 1. The measured current state

| Folder | `.cs` | What is actually in it |
|---|---:|---|
| `Models/` | 7 | `BossModelBuilder`, `EnemyModelBuilder`, `GoblinModelBuilder`, `HorseModelBuilder`, `ItemBuilder`, `PlayerPartMesher`, `WeaponModelBuilder` |
| `Models/MapBuilder/` | 10 | 10 partials of one class — **and it is not "models"** (see §2.1) |
| `Animation/` | 3 | `PlayerAnimator`, `WeaponAnimator`, `WeaponStowAnimator` |
| `Player/` | 32 | 6 `PlayerController` partials + `Races/` (9 files) + rest |

Reference counts (files referencing the symbol, self included):

```
MapBuilder           40 files     WeaponAnimator         5 files
ItemBuilder           7           WeaponStowAnimator     5 files
WeaponRigBuilder     14           PlayerAnimator         3 files
WeaponRigHost         9           WeaponModelBuilder     3 files
BossModelBuilder      3           PlayerPartMesher       2 files
EnemyModelBuilder     3           HorseModelBuilder      2 files
GoblinModelBuilder    3
```

**The controller → model → animation bridge is one 92-line file**: `Player/PlayerController.Animation.cs`.

---

## 2. Three category errors, which is why the folders read wrong

### 2.1 `Models/MapBuilder/` is a procedural *geometry factory*, not a model folder

`MapBuilder` is referenced by **40 files** — by far the most depended-on symbol in the codebase — and its
public surface is not models at all:

```
BuildCafe  BuildRiceRestaurant  BuildConvenienceStore  BuildShop  BuildLibrary
BuildFishingShop  BuildPoliceStation  BuildCar  BuildPoliceCar  BuildPlayerHouse
BuildWifeHouse  BuildRichManMansion  BuildTree  BuildCoconutTree  BuildStone
BuildBorderRock  BuildCloud  BuildTornado  CreateSolidMaterial  BuildStreetLamp
BuildPlayerModel  BuildSeatedPlayerModel  BuildAddictNpc  BuildMonkNpc  BuildPriestNpc
BuildTaoistNpc  BuildMarketVendor  BuildPoliceOfficer  BuildBuffalo  BuildLibrarianNpc ...
```

`BuildCloud` and `BuildTornado` sit in the same class as `BuildPlayerModel`. So `Models/` currently means
"geometry", and the 7 genuinely-model files are a minority of the folder they share a name with. This is
the root cause of §2.3.

### 2.2 Weapon **visuals** live under `Combat/`

`Combat/Weapons/` holds 28 files, almost all combat logic — but three of them are presentation:

| File | Really is |
|---|---|
| `WeaponRigBuilder.cs` | builds + poses the weapon **mesh** (`MakeBlock` calls into `WeaponModelBuilder`) |
| `WeaponRigHost.cs` | the `MonoBehaviour` that **owns a rig's transform** |
| `MagicWeaponBehavior.cs` / `MagicWeaponMods.cs` | magic weapon *stats*, correctly placed |

A folder named `Combat` holding the thing that decides where the sword sits in the hand is the second
place the "models and animations close together" instinct is pointing at.

### 2.3 The magic models had no file, which is why they were invisible

*(state measured at 1iz, before stage 1 landed)* `Models/WeaponModelBuilder.cs` was **379 lines** with
**18** weapon builders plus a `Build(weaponId, parent)` dispatch — 19 `Build*` symbols in all. Four of
those builders are magic, and they are **contiguous** — verified by reading the file, not inferred:

| Method | Body lines | Section header |
|---|---|---|
| `BuildStaff` | 201–215 | `// 11. MAGE'S STAFF` at 198 |
| `BuildHolyBook` | 220–233 | `// 12. HOLY BOOK` at 217 |
| `BuildBoneWand` | 238–253 | `// 13. BONE WAND` at 235 |
| `BuildControlOrb` | 258–273 | `// 14. CONTROL ORB` at 255 |

So the magic block was **L199-275**, and `BuildLute`'s header at L276 was the boundary. Searching for a
magic model found nothing, because there was no magic model *file* — only four methods numbered among
fourteen melee/ranged/shield ones. Note these four are also **exactly** the defs that carry a *rotation*
accent, which is why 1im's rest-pose drift showed up on magic only (AGENTS rule 16).

**The extraction is not a copy-paste, and the blocker is narrower than it looks.** The four methods call
one private helper and eleven colours:

- `MakeBlock(name, parent, scale, position, color)` was declared **`private static`** — a new class cannot
  call it as written. It also reaches across folders into `MapBuilder.CreateSolidMaterial(color)`, so it
  is not a self-contained helper. **Resolved in stage 1** by widening it to `internal static`.
- The palette is **15** `static readonly Color` fields. The magic four use **11** of them
  (`DarkWood`, `Gold`, `BlueCrystal`, `PaleGold`, `Bronze`, `Cream`, `LeatherBrown`, `Ivory`,
  `DarkPurple`, `DarkSteel`, `Cyan`); the **4** unused by them are `SteelSilver`, `DarkGold`, `WoodBrown`
  and `WarmBrown`. **The palette therefore cannot move with the magic methods** — the other fourteen
  weapons use the same fields. Widening or relocating the *helper* is the actual work; the colours stay
  put. **Resolved in stage 1** by making the 11 shared fields `internal` and importing them, rather than
  copying them.
- The dispatch `Build(string weaponId, Transform parent)` matched `"staff"`, `"holy_book"`, `"bone_wand"`,
  `"control_orb"` to those four methods. Its three callers (`WeaponRigBuilder`, `NewWorldTestGround`, and
  the builder itself) needed **no change**, because the new class exposes the same four names — confirmed
  by grep, which found those four names at **zero** call sites outside the class.

---

## 3. Target structure

Three top-level concerns, split on **what decides the value**, not on what the thing is called.

```
Assets/Scripts/
  Player/                     CONTROLLER - reads input, moves a capsule, owns game state.
    PlayerController.cs           the 6 partials STAY together: one class, one file family.
    Races/                        race data + passives (data, not visuals)
    Interactions/

  Avatar/                     AVATAR - the thing you see. Owns geometry + pose for the player only.
    Model/
      PlayerPartMesher.cs         body mesh from race ratios
      PlayerModelFactory.cs       NEW - wraps MapBuilder.BuildPlayerModel; extracted in stage 3
    Animation/
      PlayerAnimator.cs           MOVED from Animation/ (it poses the avatar, not a weapon)
      PlayerAvatar.cs             NEW - extracted from PlayerController.Animation.cs in stage 3

  Animation/                 ANIMATION - anything that writes a transform over time.
    WeaponAnimator.cs             weapon swing/charge/guard accents
    WeaponStowAnimator.cs         held <-> stowed pose lerp
    README.md                     keeps its rest-pose invariant section

  Geometry/                  MODELLING - all procedural mesh construction. Nothing here reads input.
    Actors/                      Boss/Enemy/Goblin/Horse/Item builders
    World/                       MapBuilder/ (buildings, vehicles, nature, clouds) - 40-file dep
    Weapons/
      WeaponModelBuilder.cs       the 14 non-magic weapons + dispatch
      Magic/                      the 4 magic bodies - landed in stage 1, moves here in stage 2
        MagicWeaponModelBuilder.cs
```

### The one-line test for a proposed move

> **Does this code read input, write a transform over time, or construct a mesh?**
> Exactly one answer → that folder. Two answers → it is a bridge; name the bridge after the thing it
> bridges (`PlayerAvatar`), do not file it under either side.

### Invariants that must survive every stage

1. **A class's partials stay with the class.** `PlayerController.*` (6 files) and `MapBuilder.*`
   (10 files) are never separated from their class, and `PlayerController.Animation.cs` is *not* moved in
   stages 1–2. This is the existing `Animation/README.md` rule; stage 3 replaces it with something stronger.
2. **No namespaces, no `.asmdef`** — so a move cannot break compilation. **`.meta` parity is the real
   check**: `.cs` count == `.cs.meta` count under `Assets/Scripts` (366/366 after stage 1; re-measure,
   do not carry the number forward).
3. **A folder name is not evidence of purpose.** §2.1 and §2.2 are both this mistake. When you move a
   file, re-read what calls it; do not trust the folder it came from.

---

## 4. Migration stages, smallest blast radius first

Each stage is one commit and is independently verifiable by grep + meta parity. Do not batch them.

### Stage 1 — give the magic models their own file  *(SHIPPED in 1ja)*  *(fixes the actual complaint)*

Extract `WeaponModelBuilder.cs` **L199-275** (`BuildStaff`, `BuildHolyBook`, `BuildBoneWand`,
`BuildControlOrb`) into `Models/Magic/MagicWeaponModelBuilder.cs`.

> **Correction to the plan as first written.** This stage originally named
> `Geometry/Weapons/Magic/MagicWeaponModelBuilder.cs`, which does not exist and is not created until
> stage 2 — the target was one stage ahead of itself. Stage 1 therefore lands at `Models/Magic/`, which
> is where a reader looks, and stage 2 moves the whole `Models/` tree including this folder. Writing the
> destination down before the stage that creates it is how a plan acquires a path that cannot be opened.

- **The blocker was `MakeBlock`, not the palette.** It was `private static`, so the smallest correct fix
  was to widen it to `internal static` and have the new class import it — there are no namespaces and no
  `.asmdef`, so `internal` reaches it. The alternative (extract `MakeBlock` + a shared palette into a
  `BlockMesh` helper both classes use) is cleaner but touches the other fourteen weapons, so it is a
  *second* commit, not part of this one. Shipped as `internal` + `using static WeaponModelBuilder;`.
- **Did not copy `MakeBlock` or the palette into the new class.** Two copies of a mesh helper is exactly
  the "second spelling that rots" failure (AGENTS rule 8), and the colour constants would then need
  copying too.
- Kept the `Build(weaponId, parent)` switch in place; the four cases now call
  `MagicWeaponModelBuilder.Build*`. All three callers are untouched.
- Carried the `// 11.`–`// 14.` section headers with the methods. These are the weapon's index in
  `WeaponCatalog` registration order, **not** a per-file counter, so `WeaponModelBuilder` deliberately
  keeps a visible gap at 11–14 rather than renumbering to close it. Both files say so in a comment.

### Stage 2 — `Models/` → `Geometry/{Actors,Weapons,World}`  *(pure moves, zero code edits)*

- `MapBuilder/` → `Geometry/World/MapBuilder/`
- `BossModelBuilder`, `EnemyModelBuilder`, `GoblinModelBuilder`, `HorseModelBuilder`, `ItemBuilder`,
  `PlayerPartMesher` → `Geometry/Actors/`
- `WeaponModelBuilder.cs` → `Geometry/Weapons/`
- Verify: `.cs`/`.cs.meta` parity, 0 orphan metas, `grep` that no file addresses any of these **by path**
  (`AssetDatabase.LoadAssetAtPath`, an `.asmdef`, an editor tool). Last time this was measured it was 0 —
  re-measure, do not carry the number forward.

### Stage 3 — extract the avatar bridge  *(the real decoupling; highest risk)*

`Player/PlayerController.Animation.cs` currently does **three** separable jobs:

1. build/reload the model (`LoadPlayerModel` → `MapBuilder.BuildPlayerModel`, renderer layers 6/7),
2. wire the animator (`AddComponent<PlayerAnimator>()`),
3. re-seat and re-pose weapon rigs (`WeaponRigBuilder.ReparentToHands`, `ReApplyWeaponPose`,
   `_pendingAutoRig`).

Move all three into a `PlayerAvatar` component under `Avatar/`. The controller keeps exactly one call
site (`_avatar.Reload()`), and `WeaponsDrawn` stays on the controller because it is a **gameplay** read
(`=> FightingMode`), not a visual one.

*Why this is last:* it is the only stage that changes signatures instead of paths, and with no compiler
(rule 3) a signature error is invisible to review. It also invalidates the `Animation/README.md`
paragraph that currently justifies keeping the partial where it is — that paragraph must be rewritten in
the same commit, not left to rot (AGENTS rule 8, stale-comment bullet).

### Stage 4 — pull the weapon rig out of `Combat/` *(optional, cosmetic)*

`WeaponRigBuilder.cs` + `WeaponRigHost.cs` → `Geometry/Weapons/`. Lower value than 1–3: both are
correctly *named* and only misplaced by folder. Do it only if stages 1–3 have landed clean.

---

## 5. What this document deliberately does not do

- It does not re-derive a private formula, a dispatch table, or a `cs:line` reference — those rot
  silently (AGENTS rule 7/8). Every claim here names a **symbol**, and every count was measured.
- It does not claim the refactor is safe to do in one pass. Stage 3 is a real code change; the stages
  exist so that a mistake is attributable to one of them.
- `MapBuilder`'s 40 dependents are called out as a **cost to be aware of**, not as a reason to split the
  class. 1iw already found that `MapBuilder` is load-bearing through `World/Chunks/ChunkObject.cs`
  (streamed terrain draws its trees and stones through a class under `Models/`). A path move does not
  change that; **an extraction would**, and that is the reason stage 3 does not touch it.