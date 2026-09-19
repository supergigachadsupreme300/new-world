# THINKING — Working Reasoning Log

This file stores the **raw reasoning trail** behind tough investigations: every hypothesis
considered, the evidence for/against it, dead ends, and why the surviving candidates survived. It is
deliberately more verbose and tentative than the other docs.

Authority order (do not confuse these):
- `game-design.md` — the durable design of record (what the game IS).
- `PROGRESS.md` — the per-task handoff summary (what was done + status + play-test checklist).
- `THINKING.md` — **this file**: the messy "how I reasoned it out", including guesses that turned out
  wrong. Never cite a hypothesis here as if it were implemented behavior.

When an investigation closes, keep its section but mark the verdict (confirmed / rejected / fixed by
`1xx`), so the same wrong paths aren't walked twice.

---

## 1ct — Play-test fixes: pink terrain (1cs shader) + casual fighting pose (1cr revert) (SHIPPED in `1ct`)

### VERDICT
Two play-test bugs, two root causes, both confirmed by source review (no build, rule 3):

1. **Pink terrain** = `TerrainLayered.shader` got **rejected entirely**: its ForwardLit vertex called
   `GetVertexNormalInputs(input.normalOS, input.normalOS)` — URP 17.5 has no `(float3, float3)`
   overload, only `(float3)` and `(float3, float4 tangentOS)` (`ShaderVariablesFunctions.hlsl:22,31`)
   → HLSL compile error → subshader fails → Unity SRP shows magenta (the shader's URP-Lit `FallBack`
   is **not** used under URP). Trees/rocks looked fine because they keep their own working URP Lit
   materials — only the ground material was broken. Fix: single-arg `GetVertexNormalInputs(input.normalOS)`.
2. **Fighting pose in normal mode** = `1cr`'s `WeaponsDrawn => FightingMode || firstPerson`
   (`PlayerController.cs:1861`) kept the weapon drawn at port arms in every first-person frame,
   casual included. Fix (confirmed with user: "draw only while fighting"): `WeaponsDrawn => FightingMode`.

### Hypotheses & evidence
- **H1 — pink terrain is a shader *name/path* problem (e.g. material lost the shader on reload).**
  REJECTED. `GameBootstrap` assigns the layered shader to the ground material by name at boot; a
  missing/dropped shader reference would pink EVERYTHING on the ground renderer, which it did — but
  the giveaway was that trees/rocks (plain URP Lit) were unaffected, so the ground's **own material**
  was at fault, i.e. its shader failing to compile (not a shared/global shader that all materials
  reference). Compile failure under SRP => magenta.
- **H2 — the pink is a missing *keyword* (fog / shadows) tricking URP into an error branch.**
  REJECTED after reading `Core.hlsl`/`Lighting.hlsl` include chain: keywords select variants, they
  don't produce magenta. Magenta requires the shader itself to be uncompiled/rejected.
- **H3 — the exact HLSL error is the `GetVertexNormalInputs(f3,f3)` call.** CONFIRMED (compile error
  in the strict sense — no such overload). Audited every other API call against the 17.5 package:
  `UniversalFragmentPBR(InputData, SurfaceData)` exists exactly at `Lighting.hlsl:302` with matching
  field names; `TransformWorldToShadowCoord` lives in `Shadows.hlsl:356` (reachable via
  `Lighting→RealtimeLights→Shadows` and `Lighting→GlobalIllumination→SphericalHarmonics→Shadows`);
  `MixFog`/`ComputeFogFactor`/`GetVertexPositionInputs` fine; ShadowCaster's `_LightDirection`,
  `_LightPosition`, `ApplyShadowBias`, `_CASTING_PUNCTUAL_LIGHT_SHADOW`, `UNITY_REVERSED_Z` all match
  the URP 17.5 `ShadowCasterPass.hlsl`. Only the overload was wrong. The tangent overload needed a
  `float4` tangent — the terrain has no tangent stream and no normal map, so the single-`float3`
  overload is exactly right.
  - Side-check: shader references `half4 _Color; float _UseVertexColor;` at file scope without a
    `CBUFFER UnityPerMaterial` — legal (plain uniforms), just not SRP-batcher friendly; not the bug.
- **H4 — the fighting pose is a `/pose` console command or an animation state leak.**
  REJECTED. Grep found no pose command; the pose is entirely driven by `WeaponsDrawn` →
  `ReApplyWeaponPose` → `WeaponRigBuilder.ApplyPose(gameObject, draw)`. `ApplyPose` (`WeaponRigBuilder.cs:363`)
  is binary draw/stow (`WeaponStowAnimator.Snap/SetPose`) — there is NO neutral-carry pose, so the
  only ways to kill a port-arm look in casual are (a) stow (what the user chose) or (b) build a whole
  new "carry" pose API (rejected: not what the user asked for).
- **H5 — boot/test-ground drew the weapon regardless of `WeaponsDrawn`.**
  PARTLY TRUE: `NewWorldTestGround.SpawnAllWeapons` called `pc.ReApplyWeaponPose(instant: true)` at
  spawn with a comment claiming first-person visibility — under the 1cr rule that DREW the weapon at
  boot. With the revert, the same call now STOWS at boot (single code path) and the stale comment was
  rewritten. No extra toggle exists.
- **H6 — `WeaponsDrawn` revert is safe for all *hand-visibility* consumers.** CONFIRMED by grep:
  `CharacterInfoUI` (2613, 3612) only uses it to pose the preview after equip/cycle (stowed while
  casual is correct); `CameraModeSwitch.SetMode`'s `ReApplyWeaponPose` becomes a harmless no-op when
  casual; `LoadPlayerModel` re-rig + respawn re-pose flow through the same property. The arms/hands
  themselves stay visible in first person (that's `CameraModeSwitch` layer logic, untouched) — the
  player still sees their hands, just not a raised weapon.

### Dead ends & gotchas
- **FallBack is a red herring for SRP:** `FallBack "Universal Render Pipeline/Lit"` in the subshader
  is ignored under URP — don't rely on it to save a broken pass. The only fix path is a valid
  subshader.
- **`VertexNormalInputs` vs `TransformObjectToWorldNormal`:** I considered dropping the struct and
  just normalizing `TransformObjectToWorldNormal(input.normalOS)` — equivalent for a no-tangent
  surface, but the struct form is the URP-idiomatic one and shares the code path the working */
  /* URP-content objects use, so I kept `GetVertexNormalInputs(f3)`.
- **Doc drift:** `PROGRESS`'s `1cr` entry and `THINKING`'s 1cr verdict describe keep-drawn-in-first-
  person as shipped behavior — 1ct explicitly **reverses** that decision per play-test feedback;
  `game-design.md` §3.6 and §5.5 were rewritten in the same pass so no doc still claims the old rule.

### Follow-up: ShadowCaster `_LightDirection` undeclared (fixed in 1ct second commit)
- **New evidence after the first 1ct fix:** ForwardLit now compiles, but the ShadowCaster pass failed
  with `undeclared identifier '_LightDirection' at TerrainLayered.shader(145)`. My 1ct H3 audit was
  wrong in one detail: I confirmed the *names* `_LightDirection`/`_LightPosition`/`ApplyShadowBias`
  exist in URP 17.5's `Shaders/ShadowCasterPass.hlsl` — but those two `float3` globals are declared
  **inside that pass-utility file** (`ShadowCasterPass.hlsl:13-14`), NOT in `Shadows.hlsl` (which only
  defines `ApplyShadowBias`/`ApplyNormalBias` functions and shadow-matrix helpers). Since the custom
  caster includes only `Core.hlsl` + `Shadows.hlsl`, the variables were never in scope. **CONFIRMED:
  URP declares them itself only in its own inclue-able shadow-caster file `ShadowCasterPass.hlsl`.
  Fix: declare `float3 _LightDirection; float3 _LightPosition;` in the caster's `HLSLPROGRAM` block,
  byte-for-byte matching URP's own declaration** (verified again by reading `ShadowCasterPass.hlsl:13-14`
  right before the fix). Lesson: when a URP pass-support file declares its uniforms in the body of
  that file (not in a `ShaderLibrary/*.hlsl`), a custom shader must duplicate those declarations.

### Follow-up: "blue by day, black at night" = fragment computed the WRONG shadow-coord variant (fixed in 1ct third commit)
- **Symptom:** shader compiles (both compile bugs gone) but the terrain has NO direct sun light —
  lit only by sky/ambient, so it reads blue-ish at start and goes black at night (trees/rocks were
  fine, which was the tell: same scene, same sun, same ambient — so the difference is the MESH, i.e.
  my shader not the scene lighting).
- **H1 — no main light in the scene.** REJECTED: trees/rocks (URP Lit) visibly lit by a sun.
- **H2 — vertex colors never reach the GPU, albedo black, only environment reflection visible**
  (blue sky sheen by day / black at night). Tested by reading the pipeline: `BuildMeshData` fills
  per-vertex `colors` (ChunkMeshGenerator.cs:313,350,408-411), the merge keeps them, and
  `CreateMeshFromMerged` calls `mesh.SetColors(md.Colors)` (line 596-597); `PatchRegion` re-uploads
  too. Vertex colors ARE uploaded → albedo is grass/dirt/stone, not black. REJECTED.
- **H3 — `shadowAttenuation` collides to ~0 in the same frame the terrain is drawn → direct term
  zero → only GI (sky reflection + SH ambient).** CONFIRMED as the mechanism, with the exact wiring
  error: `UniversalFragmentPBR` (Lighting.hlsl:336) computes `GetMainLight(inputData, shadowMask,
  aoFactor)`; `MainLightShadow` reads the main light's shadow coord. I populated
  `inputData.shadowCoord` with `TransformWorldToShadowCoord(positionWS)` in the FRAGMENT. URP Lit
  instead fills it in the VERTEX via `GetShadowCoord(vertexInput)` (Shadows.hlsl:529-536), which
  UNDER `_MAIN_LIGHT_SHADOWS_SCREEN` (screen-space shadows; on in this project's URP asset) returns
  `ComputeScreenPos(positionCS)` — a SCREEN coord — and only otherwise falls back to the atlas
  transform. Passing atlas coords into the screen-space sampler = sampling a `_ScreenSpaceShadowMapTexture`
  at garbage UVs → `MainLightRealtimeShadow` ≈ 0 → `shadowAttenuation` ≈ 0 → `LightingPhysicallyBased`
  ≈ 0 → terrain colors = GI only (blue-sky ambient; black when sky darkens). Trees/rocks use the
  correct `GetShadowCoord`, so they kept their sun — exactly the observed asymmetry.
- **Fix:** mirror Lit — `Varyings.shadowCoord`, compute `GetShadowCoord(posInputs)` in `vert`,
  `lightingInput.shadowCoord = input.shadowCoord` in `frag`, plus `normalizedScreenSpaceUV =
  GetNormalizedScreenSpaceUV(positionCS)` (ShaderVariablesFunctions.hlsl:594) for correct screen-
  coord-adjacent GI/reflection inputs. `Shadows.hlsl` explicitly included for forward pass too.
  **Confirmed-by-verification:** `GetShadowCoord` branches exactly as described at Shadows.hlsl:531-534.

---

## 1cs — Infinite digging depth + terrain strata (grass → dirt → stone) (SHIPPED in `1cs`)

### VERDICT
Digging now goes infinitely deep (bounded only by the ±200 m mesh-sanity band) and the terrain is
vertex-colored at mesh-build time into discrete strata bands (grass → dirt → stone) derived from
`pristine noise at the corner − current vertex Y`. Craters ratchet a fixed `CraterStep` down per
cast/swing (deliberately NOT idempotent anymore), while the raised shapes keep their idempotent
`Max`-cap. No save-format or hash change — the colors are derived, never stored.

### Hypotheses & evidence
- **H1 — store a per-corner "layer" value in the save.** REJECTED. Would change `ChunkTileMod`'s
  4-float payload, the `tc_*.dat` format version/hash, `AntiCheat`/`ChunkSync` hashing and every
  save-format doc/const. Too wide a blast radius for a cosmetic read. The save already stores each
  corner's CURRENT height; depth below the *pristine* surface is the only extra datum needed, and it
  is recomputable from the seed (deterministic).
- **H2 — derive band from height alone (absolute Y).** REJECTED. Terrain rolls over ~±60 m; banding by
  absolute Y would paint whole mountains dirt/stone and river-beds grass regardless of excavation.
  Depth below the local noise surface is the only self-consistent measure ("how far am I below where
  this corner was born").
- **H3 — depth from a saved "original height" copy in memory only.** REJECTED after closer look: on
  first load after an edit, pristine height doesn't exist in memory for a loaded chunk (only the
  saved deformed corner + the noise formula). Since the noise is pure-deterministic function of the
  seed + corner coords, sampling `GetHeight(seed, cx, cz)` directly is both simpler and always
  correct, for pristine, deformed, and raised corners alike. **CONFIRMED as the implementation.**
- **H4 — idempotent "grind" is fine (revert the 1cm crater clamp, keep everything else).**
  **CONFIRMED by user.** The user explicitly wants digging to be able to go arbitrarily deep, which
  is the exact inverse of the `1cm` "crater floors clamp at noise − 1.8" idempotency. Decided to keep
  the raised shapes idempotent AND make craters compounding again (the pre-1cm behavior) — a clean
  split, and the `1cm`/`1cj` fixes that matter (no whole-chunk rise, smooth per-corner profiles,
  mesh atomicity, no flat slabs) are untouched. The old cap code (`CraterMaxDepth`) and its XML
  claims were fully removed; stale prose in `DeformAt`'s doc and `game-design.md`/`magic-skills.md`
  was rewritten in the same pass so no doc still claims craters clamp.
- **H5 — band boundaries as soft blends vs hard cuts.** User asked for "discrete strata bands" — small
  blends (0.3 m) keep the band transitions readable as layers without hard maché seams, per-corner at
  the SAME coords used for the height, so the color field is watertight across tile edges
  (neighbours share corners → identical colors — same contract that keeps the mesh gapless).
  **CONFIRMED.**

### Dead ends & gotchas
- **Rename drift:** the first implementation named the band constants `*BlendStart/BlendEnd`, then the
  public rename to `*BandStart/BandEnd` left `TerrainBandColor`'s body referencing the old names (a
  compile error caught by grep before doc pass). Fixed + grep-verified no `BlendStart`/`CraterMaxDepth`
  remain in `Assets/Scripts`.
- **Wall-pass color origin:** wall vertices are chunk-LOCAL `(ex, ez)`; their world corner is
  `tiles[0].Coord` (the chunk's min-tile world coord) + `(ex, ez)`. The defensive `tiles` fill in
  `BuildMergedMeshData` guarantees `tiles[0]` non-null before the color pass, so no NRE. Both
  `BuildMergedMeshData` call sites build the 900-array in the same (lx, lz) local order — cross-checked
  the two call sites in `WorldStreamer.cs:407` and `:879`.
- **`PatchRegion` must pass the seed too:** the newly-added seed parameter demanded a call-site update
  that the project can't compiler-check — grep-verified exactly 2 `PatchRegion` refs (definition +
  `WorldStreamer.cs:1120`), both 6-arg after the edit.
- **Crater rim "digs sideways":** repeating a cast at the same center ratchets the rim corners down a
  hair too (small `s`), slowly WIDENING the pit as it deepens. Accepted — reads as a natural bowl and
  matches "repeat casts keep digging"; flagged in the play-test checklist, not a bug.
- **Shader is the unverifiable risk:** no build is run (rule 3); `TerrainLayered.shader` is a hand-
  written URP ForwardLit (PBR + fog) + ShadowCaster + DepthOnly. If it fails to compile under URP 17.5
  the terrain goes magenta/pink in play-test and the fix is a shader compile pass, NOT the C# code.
  Fallback chain (`Shader.Find` → URP Lit → white base) is intentionally boring.
- **Tool gating uses the same math:** `shovel` gate compares `DigDepthAt(hit.point)` against
  `StoneBandEnd` (2.7); the pit floor at the hit point is interpolated across its corner heights and
  noise, and the floored integer corner sample is a faithful proxy for the band the tool is digging
  in. `Dig` shares the Crater shape so tool pits and spell craters stay one code path.

---

## 1cr — Weapon not visible on hand (held in inventory slots) (RESOLVED — shipped in `1cr`)

### VERDICT
Equipped weapons were stowed onto body anchors (`StowBack`/`StowWaist`) whenever NOT fighting; in
first person those anchors are behind the head-mounted camera, so the weapon could never appear on
screen. Fixed by making "visually drawn" = `FightingMode || firstPerson`, re-applied on combat
toggles, camera switches (F5), model rebuilds, and equip. Only third-person casual sheathes.

### Hypotheses & evidence
- **H1 — a weapon id is missing a `WeaponModelBuilder.Build` case → null visual.** REJECTED. Checked
  every catalog id vs the dispatch; all 18 weapons (incl. the 3 shields) have builders.
- **H2 — weapon renderers land on a culled layer in first person.** REJECTED. `CameraModeSwitch`
  culling only clears the body bit (layer 6); rigs stay on Default (layer 0), visible in both modes.
- **H3 — attach/hand-bone lookup fails.** REJECTED. `FindHand` resolves `PlayerModel/Torso/ShoulderX/
  …/HandX` for the standing model and `ReparentToHands` re-seats park-fallback rigs after rebuilds.
- **H4 — the weapon IS stowed, but the stow anchors are positioned behind the first-person camera.**
  **CONFIRMED.** `ToggleCombatMode` casual → `ApplyPose(draw:false)` → `WeaponStowAnimator` moves the
  rig to `StowBack`/`StowWaist` under Torso; the first-person camera looks forward from the head pivot
  and never sees behind the body. Fix: draw whenever first person.
- **H5 — stow/draw could stay per-combat-only if the user prefers.** User chose "Show in hand when
  first person" over "always in hand / drop stow" and "keep stow everywhere" — so sheathing only in
  third-person casual was the accepted target.

### Implementation notes
- Central source of truth: `PlayerController.WeaponsDrawn` + `ReApplyWeaponPose`.
- Combat toggles pass `instant:false` to keep the existing draw/stow *animation*; model rebuild /
  camera-switch equip-snap use `instant:true`. `CameraModeSwitch.SetMode` invoking the player's
  re-pose is safe during Awake because `ReApplyWeaponPose` no-ops without a `CombatController`.

---

## 1cq — Translucent Wind/Ice + denser Fire projectiles (RESOLVED — shipped in `1cq`)

### VERDICT
Wind/Ice bodies made translucent by passing a lower alpha through the existing `"Sprites/Default"`
material; Fire made "hotter" by switching the sphere to `OrbFx.Ember` flicker and roughly doubling
the ember exhaust (`EmissionRate`/`MaxParticles` up, `StartSize` up). No new shaders or assets.

### Hypotheses & evidence
- **H1 — translucency needs a shader change.** REJECTED. `Sprites/Default` (the projectile body
  shader) already blends with `SrcAlpha/OneMinusSrcAlpha`, proven by existing semi-transparent
  visuals: `CCZone` (alpha 0.4 disc), `CastingCircle`, `AoeAimPreview`, `ProjectilePathPreview` —
  all just set `material.color.a`. So the fix is purely data: give the color alpha.
- **H2 — set alpha in `DamageNumber.ColorFor`.** REJECTED — it drives ~30 call sites (bodies,
  particles, UI, skill FX across many files). Overriding alpha inside the two builders keeps Wind/
  Ice translucent without tinting damage popups or other effects.
- **H3 — the `Unlit/Color` fallback would kill transparency.** Confirmed finding, not a fix here:
  `Materialize` picks `Sprites/Default` first and only falls back to the opaque `Unlit/Color` if the
  former is missing (it never is in practice). Recorded as a doc caveat, matching how the rest of
  the codebase (RingFader, CastingCircle) already relies on this.
- **H4 — Ice/Lance share geometry?** Ice's *Lance* shape (Ice Lance/Frost Pierce, explicit `Lance`
  builder) is a separate, solid spike — kept opaque. Only the Auto `Shard` frost chip becomes
  glassy, which matches "ice magic should be transparent" for the generic chip while named lances
  keep their heft.
- **H5 — fire = bump just one number.** REJECTED — a single bump reads as a minor density change.
  Combined three levers (rate ×1.67, cap ×1.75 for sustained flight, size ×1.33) + a flickery body
  gives the obvious "more fiery" read. Kept the existing additive `Particles/Additive` exhaust and
  the gradient fade (rates seen as alpha/brightness of the glow).
- **H6 — Comet/Scorch/Burn also need the boost.** Already Ember-flickered AND have their own dense
  `Comet(...)` body; exhaust boost applies automatically (per-element tuning is shared by all fire
  delivery visuals). No per-shape change needed.

### Known limits (noted, not fixed)
- Translucency is Z-write-off sprite blending: two projectiles crossing can overdraw, but projectiles
  are transient and fast — acceptable, same as friendly/enemy cast circles today.

---

## 1cp — Earth magic projectile as rock debris (RESOLVED — shipped in `1cp`)

### VERDICT (read this first)
Confirmed approach: a **new `ProjectileShape.Debris`** (tumbling grey rock-clump, dressed like
`WorldBuilder.SpawnRockDebris`) wired to the Earth school + an **impact debris burst** out of the
crater. Trail below records the dead ends checked.

### Hypotheses & evidence
- **H1 — swap the existing `Shard` builder to rock chunks.** REJECTED. `Shard` is Ice's `Auto` shape
  too (`SpellCaster.AutoShapeFor(Ice) → Shard`), so editing it silently reskins frost chips. Instead a
  new enum value keeps Ice's diamond and gives Earth its own builder.
- **H2 — change only `AutoShapeFor(Earth)`.** INSUFFICIENT on its own. The Earth school's only
  projectile spell sets `projectileShape: ProjectileShape.Shard` **explicitly** on its `Spell(...)`
  line, so it bypasses Auto resolution. Confirmed via `SpellCastEffect Spell(...)` helper: the
  `projectileShape` param lands straight on `spell.Shape`. Fix = flip that one line AND the Auto
  default (for summoned-turret earth shots `SpellSummon.DecorateProjectile`, which pass `Auto`).
- **H3 — reuse the world debris pieces themselves.** The world chunks have `Rigidbody`/colliders and
  are interactable by the pickaxe/pickup system (`SmashDebris`, name `RockDebris`); reusing them as
  renderer-only projectile chunks risks the flight raycast self-hitting or pickups grabbing a
  fleeting chunk. The projectile keeps **renderer-only** primitives (matching the other shapes'
  "no collider" contract) but borrows the exact color formula.
- **H4 — tumbling = single Y spin (existing OrbFx path).** REJECTED for the chunks: a shared Y-spin
  looks like a drill, not debris. Added `Mode.Tumble` with a per-object `Random.onUnitSphere` axis
  captured in `Start()`; existing modes keep the old `Rotate(0, spin, 0)` branch untouched.
- **H5 — impact burst reuses the in-flight cluster builder.** REJECTED. The flight body is a static
  cluster parented to the projectile; impact needs independent physics chunks that fall out of the
  crater. Small throwaway `SpawnImpactDebris` (3-5 cubes, up-bias scatter, `Destroy` after 2.5 s),
  mirroring SpawnRockDebris' mass/velocity values.
- **H6 — could the burst chunks collide with the just-carved crater collider?** Terrain collider
  rebuilds once per deformer apply (1ck made the swap atomic) — the physics chunks spawn above the
  impact point and fall freely; the crater floor is solid walkable terrain. No special handling.

### Known limitation (noted, not fixed)
The StarEffigy/Golem **summon** spells (Stone Effigy/Sentinel/Guardian/Colossus) fire rocks via
`SpellSummon` with `Auto` shape → they now read as the same Debris cluster. Consistent, intended;
not a regression.

---

## 1co — Race look on the block player model (RESOLVED — shipped in `1co`)

### VERDICT (read this first)
Confirmed approach: **palette + body ratios on the shared block model**, race resolved from the
model's parent at build time, uniform RigScale raised for the giants, and a model rebuild on race
change. Walking list of hypotheses below is the trail.

### Hypotheses & evidence
- **H1 — tint the block model via `RaceRig.RigTint`.** REJECTED. One flat tint hits every renderer:
  eye whites go dark, hair/clothes/pants/shoes can't differ, and RigTint is inherently uniform per
  race. `RaceRig.ApplyRace` therefore keeps *only* the uniform scale and the prefab-branch tint.
- **H2 — swap a per-race prefab at build.** REJECTED for now. Needs authored `.asset` bodies; nothing
  exists yet. `RigPrefab` stays as the documented later path (drop-in, no code change).
- **H3 — per-race palette + ratio data on `RaceData`.** CONFIRMED (chosen). 6 colors + 6 knobs with
  Human defaults = original colors / 1, so Human output is a strict no-op (checked `ApplyRaceLook`
  exits when all ratios == 1 and the colors fall back to the same constexprs).
- **H4 — rebuild the model on race change.** CONFIRMED as a gap: `OnActiveRaceChanged`
  (RaceChangeManager.cs:125) had **zero subscribers**; `LoadPlayerModel` runs on Awake/ApplyGender
  only. Fix: idempotent subscription in `LoadPlayerModel` guarded by `_raceSubscribed`. Dead end I
  avoided: subscribing in Awake only — respawn/reparent paths that reload the model may drop the
  subscription; guarding inside `LoadPlayerModel` covers every reload.
- **H5 — `ActiveRace` allocates 22 ScriptableObjects per get.** CONFIRMED (calls `BuildDefaultRoster`).
  Fix: static cached `RaceDatabase.DefaultRoster`. (RebuildIndex still runs the serialized `Races` on
  OnEnable — unchanged.)
- **H6 — cutscene models.** CONFIRMED via call-site trace: `BuildSeatedPlayerModel` parents the model
  to the **car**, which has no `RaceChangeManager` → always Human. `null`-parent fallback also covers
  any other host; the seated model built off the player root (`PlayerSitController`) *is* race-aware.
- **H7 — head counter-scale.** The standing model's Hair/Eyes/Neck live under the **Torso pivot**, not
  the root, so a root-only scale would leave them embedded after Height/Bulk/RigScale → must recurse
  and counter-scale names Head/Neck/Eye*/Hair*/Ponytail* by `(Head/Bulk, Head/Height, Head/Bulk)`.
  Dead end checked: relying on RigScale only would look correct until the first Height/Bulk knob — so
  ratios are authored on top of the uniform scale, ratios decide the *look*.
- **H8 — foot calibration.** Standing leg chain: Hip at -0.25, Thigh/Shin/Shoe bottom ≈ -0.62 → foot
  bottom = `root.y − 0.25 − 0.62`; after scaling root by h and legs by leg the bottom sits at
  `root.y − 0.25*h − 0.62*leg`; setting `root.y = h*(0.25 + 0.62*leg)` re-lands it at 0 (Human ≈ 0.87
  vs 0.86 — 1 cm, absorbed by boots). Seated/sit models skip calibration (their pose isn't ground
  planted; a bottom-heavier sit for short-legged races is acceptable).
- **H9 — raise vs lengthen giants.** User chose **raise** (global `RigScale`, also enlarges the
  hitbox for "feel"). Goblin/Gnome scale unchanged (15% smaller hitbox is a real contract in §3.5).

### Known limitation (noted, not fixed)
`SaveManager` has no race field (grep of save/restore paths found none) — a saved game loads as Human
until the player re-picks a race. Pre-existing behavior; remains out of scope.

---

## 1cm — Earth Wall repeat cast "makes the entire chunk moving" (RESOLVED — fix shipped in `1cm`)

### VERDICT (read this first — the trail below is the *before* picture)
Confirmed root cause: **`DeformAt` was additive, not idempotent.** Raise was `current + s*lift`
(capped at `noise+lift`) and crater `max(current − s*1.8, noise−1.8)`. Because `current` already held
the previous cast's raise, a repeat cast added the raise **again**, lifting the whole influence
footprint toward the cap on each of the first several casts (the low-influence flanks included), so
the ground rose across a wide swath of the chunk on cast #2+ → "the entire chunk moving". Craters
compounded identically (deeper each cast).

**My "provable no-op" claim below (H1) was WRONG.** The cap bounds the *final* height but does not
make the operation idempotent; the additive form still compounds up to that cap. The tell I missed:
`DeformAt`'s own comment admitted "repeat casts (which stack the ridge on the previous height)".
`1bo` bounded the height, not the compounding.

Fix shipped (`1cm`):
- `DeformAt` uses absolute per-corner targets — raise `Mathf.Max(current, noise + s*lift)`, crater
  `Mathf.Min(current, noise − s*depth)` → idempotent.
- H7 addressed: `TerrainDeformer.ResolveGroundTarget` skips a forward hit that is the chunk's OWN
  `ChunkObject` terrain collider **and** sits above pristine noise + 0.25 m (a reared wall), so a
  repeat cast targets the intended ground rather than the wall face. (NOTE: the wall is the terrain
  `MeshCollider` itself — there is no separate wall collider/layer, which is why the "ignore a layer"
  idea below was unworkable.)
- H8 addressed: `ChunkObject.ApplyMerged` re-points filter + collider at the new mesh before
  destroying the old one. (`PatchRegion`'s `null → assign` is a single synchronous call; physics
  never observes the null, so it was left as-is with a comment.)

The hypotheses below are kept as the raw trail; see each for its final status.

### The report
> "when cast earthwall it only work the first time and the next time it make the entire chunk moving"

Repro facts gathered from the user (3 clarifying questions):
- **Deterministic** — happens on **every** cast after the first, never intermittent.
- **Same spot, standing close** — the second cast targets roughly the same spot as the first, and the
  player is standing near where the first wall came up.
- **"Moving" is not pinned down** — the user can't say for sure whether it's the player being
  shoved/sinking, the chunk's surface re-meshing, or the chunk relocating. So the investigation has
  to be robust to all three readings.

### Prior related history (from PROGRESS.md)
This is the third report in the same symptom family, so start from what earlier fixes did:
- `1bn` — Earth Wall fix: taller blocking wall + no more player "teleport" on repeat casts (added the
  caster keep-out ring, `keepOutR ≈ 0.9`, so raised shapes can't grow directly under the capsule).
- `1bo` — Earth Wall repeat casts no longer "shrink the world": raised shapes are **height-capped**
  (`newHeights = current + s*lift` then clamp), so they can't stack unbounded.
- `1cl` — per-corner caps (no flat plateaus) + tops-first merged mesh.

So the naive "it stacks taller each cast and launches you" cause was already fixed by `1bo`, and the
"flat plateau re-arms legacy walls" cause by `1cl`. This new report survives both, which is a strong
hint the remaining cause is NOT in the height math at all.

### Mental model of the cast pipeline (walked end-to-end)
1. `SpellCaster.ResolveZone` (`SpellCaster.cs:922-961`): ray from caster along aim → `aimHit`;
   then a down-probe from `aimHit.point` → `groundHit` → `center = groundHit.point`.
2. `TerrainDeformer.Apply(center, radius, Wall, fwd)` → `WorldStreamer.DeformAt`.
3. `DeformAt` (`WorldStreamer.cs:704-833`): builds the Wall ridge influence over an AABB of world
   corner coords, computes `newHeights[corner]`, calls `ApplyHeightEdits`.
4. `ApplyHeightEdits` (`1028-1066`): writes the 4 corners into every loaded tile in the AABB, bumps
   `Version`, `MarkDirty`, then per touched chunk `RebuildChunkRegion` + `FlushDirtyChunk`.
5. `RebuildChunkRegion` (`1075-1115`): if `ChunkContainsFlatTile` OR region ≥ 75% area → full rebuild;
   else build just the region and `PatchRegion` the existing merged mesh.
6. `ChunkObject.PatchRegion` (`76-135`) / `ApplyMerged` (`51-75`): `SetVertices`/`SetNormals`/UV,
   `mesh.bounds`, then recook the `MeshCollider` (`_mc.sharedMesh = null; _mc.sharedMesh = mesh`).

### Hypotheses and verdicts

**H1 — heights stack/grind on repeat (dirty heights).** **WRONG — this turned out to BE the bug.**
(Originally mislabeled "REJECTED".) `DeformAt` computed `current + s*lift` (capped at `noise + lift`)
for raises and `max(current − s*1.8, noise − 1.8)` for craters. The clamp bounds the *final* height but
does **not** make the operation idempotent: `current` already includes cast #1's raise, so each repeat
cast adds it again and compounds toward the cap — lifting the whole influence footprint (low-influence
flanks included) and reading as the entire chunk's ground rising. Craters compounded identically.
`DeformAt`'s own comment admitted "stack the ridge on the previous height"; I read past it. Fixed by
switching to absolute per-corner targets with `Mathf.Max`/`Mathf.Min`.

**H2 — coordinate/frame mismatch (corners vs tiles, chunk origin).** REJECTED.
Checked `ChunkData.Size = 1f`, `TerrainChunkCoord.ChunkSize = 30`, `FromTile` `FloorToInt`, chunk
origin `(tc.X*30, 0, tc.Z*30)`, and `DeformAt` corner ints. All agree; negative coords handled. A
mismatch here could have shifted a whole region, so it was worth ruling out, but it's clean.

**H3 — `PatchRegion` writes into wall-band vertex slots (interleaved layout).** REJECTED (fixed by 1cl).
That was exactly `1cl` root cause B. `BuildMergedMeshData` is now tops-first (all 4×900 top verts
contiguous, walls last), so `PatchRegion`'s fixed `(lz*cs + lx)*4` offsets are always valid. Confirmed
by reread (`ChunkMeshGenerator.cs:285-314` pass 2, `316-403` pass 3) and no other consumer of
`_merged.Vertices` exists.

**H4 — something literally translates the chunk transform.** REJECTED.
Grepped for `transform.position`/`SetParent` in `Assets/Scripts/World`; `CreateChunkGameObject` sets
the chunk position once at `(tc.X*cs, 0, tc.Z*cs)` and nothing moves/reparents a `ChunkObject` after.
`ChunkObject.Release` only destroys props + mesh. So "the chunk moved" cannot be a literal transform
move.

**H5 — the wall crest reads as a flat tile, flipping `ChunkContainsFlatTile` → full rebuild.**
REJECTED. `IsFlatTile` needs all 4 corners within `0.001f`. A ridge tile's corners are
`noise(corner) + 2.6` with genuine per-corner noise slope, so they are not flat. (This was the
leading theory for a while and it's attractive because it would explain "every second+ cast" — cast
#1 creates the state that flips #2 — but the math doesn't support it.)

**H6 — the repeat cast's region crosses the 75%-of-chunk threshold → full rebuild.** REJECTED for the
plain case. Default Earth Wall radius 3.6; charged max `sizeScale ≈ 1.8` → radius ≈ 6.5, reach
≈ 7 → AABB ≈ 13×13 = 169 tiles, far below `0.75 × 900 = 675`. Would only matter if heavily charged
AND near a chunk edge; user says same spot every time, so not the default explanation.

**H7 — the second cast's aim ray hits the first wall's collider. ADDRESSED (real, but a secondary
contributor).** `ResolveZone` did a plain `Physics.Raycast(pos, fwd, range)` with no layer filtering,
so after cast #1 a repeat cast's `aimHit` could land on the 2.6 m ridge and leave `center` on/near the
wall instead of the intended ground — moving the deform + collider recook. There is no separate wall
collider/layer to filter (the wall IS the chunk's terrain `MeshCollider`), so the fix is geometric:
`TerrainDeformer.ResolveGroundTarget` now skips a forward hit only when it is the chunk's OWN
`ChunkObject` collider **and** sits above pristine noise + 0.25 m (a reared shape), then re-probes;
craters/ground/entities/props are never skipped. `ResolveZone` uses it.

**H8 — null-collider physics frame during recook. ADDRESSED (defensive).**
`ApplyMerged` used to do `_mc.sharedMesh = null; … _mc.sharedMesh = mesh` (and `PatchRegion` still
does). The corrupt-order path is fixed: `ApplyMerged` now assigns the new mesh to the filter/collider
**before** destroying the old mesh. `PatchRegion`'s null→assign is one synchronous call the physics
step never observes, so it was left as-is with a clarifying comment.

**H9 — full-chunk re-mesh re-emits side bands around the smooth ridge. REJECTED / not needed.**
H5/H6 already argued the default repeat Wall doesn't flip to `FullRebuildChunk`, and once H1 (additive
compounding) explained the chunk-wide rise, no full-rebuild re-mesh was required to account for the
symptom. Not investigated further.

**H10 — background-generate race. REJECTED.**
`BackgroundGenerateChunk` does run `BuildOrLoadChunk` off-thread, but the report is fully
deterministic (every second+ cast), which a race can't explain. Superseded by H1.

### Dead ends worth remembering
- Time was spent chasing a "fill origin" bug in `BuildMergedMeshData`'s defensive null-tile fill
  (`fillOrigin = tiles[i].Coord` of the first non-null tile, then used as the origin for later null
  tiles). It IS a latent correctness smell if the first non-null tile isn't at local (0,0), but the
  clean path (`BuildOrLoadChunk` on `anyMissing`) means the fill rarely fires, and it doesn't explain
  a repeat-cast symptom. Don't re-chase it for 1cm.
- Considered "player walks up the wall then the next cast embeds them" — but the `1bn` keep-out ring
  (`keepOutR ≈ 0.9` around `casterFeet`) prevents a raise within a capsule radius of the feet, so the
  capsule can't be newly embedded by the cast itself.

### Facts to carry forward (verified by reread)
- **CORRECTED:** repeat-cast height math was **NOT** a no-op — the additive form compounded up to the
  cap (H1). Now genuinely idempotent via absolute `Max`/`Min` targets.
- Coordinates/origins are consistent (H2).
- Merged mesh is tops-first; `PatchRegion` offsets are safe (H3).
- Nothing translates a `ChunkObject` at runtime (H4).
- `IsFlatTile` and the 75% threshold do NOT flip for a plain repeat Wall (H5/H6).

### Next session plan (EXECUTED — outcome in VERDICT)
1. Instrument cast #1 vs #2 (temporary logging, or careful read): log `center`, which collider the
   aim ray hits, the route chosen (`PatchRegion` vs `FullRebuildChunk`), and the region `w,h`. This
   single step should collapse H7 vs H9.
2. Read `ChunkMeshGenerator.EdgeIsRaised` / `EdgeHeights` + collapse threshold; confirm whether a
   smooth 2.6 m ridge edge emits side bands on a full rebuild (H9).
3. Then decide the fix from evidence. Likely one or more of: (a) exclude the deformed terrain
   collider from the **aim** ray only, (b) make the collider recook atomic (no null frame), (c) keep
   repeat casts on the region-patch route.
4. Play-test: cast Earth Wall 3-4× at the same spot standing on the chunk → every cast must look and
   behave like the first (ridge ≈ 2.6 m, no whole-chunk re-mesh, no jerk); walk away and back → wall
   persists identical.

### Open questions I still wanted answered (resolved)
**Answered:** the compounding is per-cast and independent of charge/radius (the flanks compound up to
the cap regardless); the "move" is the terrain re-mesh at cast time, not a delayed physics pop (H8 is
not the cause); a Crater repeat also compounded (ground got deeper each cast), consistent with H1
rather than H7 alone.

- Was the first cast the DEFAULT radius or heavily charged? (changes H6/H9 exposure)
- Does the "move" happen at the instant of the cast, or a frame or two later (physics step → H8)?
- Does a non-Wall repeat cast (Crater at the same spot) also do it? If no, H7 (aim hitting the raised
  collider) is a much stronger candidate than the generic recook/rebuild paths (H8/H9).
