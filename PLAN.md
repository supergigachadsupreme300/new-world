# Plan: UI polish + player model visuals + per-weapon attack animation

> STATUS: Sections 1-12 shipped and pushed (commits 2b4bd6a, d8f5f46, 61827f0, 7fd5218, f097440).
> Section 13 (streaming perf rework) implemented, semantic-checker clean (0 diagnostics); pushed.
> Section 14 (white player / hidden sword / backpack layout) implemented, semantic-checker clean; pushed.
> Section 15 (bulletproof color + always-visible sword + unique per-weapon arm animations) implemented,
>   semantic-checker clean (0 diagnostics); pushed.
> Section 15.5 play-test follow-ups (drop-to-equip consumes the bag item; in-hand weapon scale +25%)
>   implemented, semantic-checker clean (0 diagnostics); not yet pushed.
> Verify checklists 10/12/13/14/15 still need user play-testing (Unity can't be run in this env).

This file is the durable plan for the current batch of work. It survives context compaction.
Mark each step as it is completed. When a step changes scope (discovered while implementing),
update this file to reflect reality.

Context note: weapons are already inventory items; F1 toggles fighting mode; equipped weapons
are rigged onto the player model hands (`PlayerModel/ShoulderR/HandR`, `PlayerModel/ShoulderL/HandL`)
by `WeaponRigBuilder.EquipInto`. Batch 11 (below) fixes trees/stones vanishing, the HUD crash,
prompt spam, the skill bar, red ✕ close buttons, stat "+" gating, and bar drain visibility.

Body size reference for the Character Info menu: body half-extents ≈ ±503 x, ±240 y
(`MenuPanelBase.UiScale` = 1.2, all CharacterInfoUI coordinates use `S = 1.0`).

---

## 0. Plan file
- `PLAN.md` at repo root — this file.

## 1. Player model color (root cause)
- File: `Assets/Scripts/Models/MapBuilder.cs` (`ApplyBlockColor`, lines 13-26).
- Current: clones `r.sharedMaterial` (default cube material) → renders grey/pink under URP.
- Fix: create a cached `Material` per color using `Shader.Find("Universal Render Pipeline/Lit")`
  (fallback `Standard`), like `WeaponModelBuilder` already does. Takes effect for player, NPCs,
  buildings, etc. (they already supply colors).

## 2. Walk/run/move animation — new `PlayerAnimator`
- New file: `Assets/Scripts/Player/PlayerAnimator.cs`.
- Added by `PlayerController.LoadPlayerModel` (PlayerController.cs:964-980) on the `PlayerModel` root.
- Reads `PlayerController` horizontal velocity (walk < ~0.8*MoveSpeed, run/sprint above).
- Drives pivots `PlayerModel/ShoulderL|R`, `PlayerModel/HipL|R`:
  - Arms + legs swing on local Z with sinusoidal phase; legs opposite phase to each other,
    arms opposite to the same-side leg.
  - Amplitude + cadence scale with speed (walk slower/deeper, run faster/shallower).
  - Idle: subtle breathing (small Body scale/rotate bob).
- Skipped while sitting (`PlayerSitController.IsSitting`) or riding (`PlayerController.IsRiding`).
- Rest pose is local identity — no conflict with weapon hand attach (weapon sits under `HandL/R`).

## 3. Per-weapon attack animation — new `WeaponAnimator`
- New file: `Assets/Scripts/Combat/Weapons/WeaponAnimator.cs`.
- Added in `WeaponRigBuilder.BuildRig` (and automatically inherited by `CloneRig` via BuildRig).
  The animation lives ON the weapon GameObject, so "the model animates depending on the weapon".
- `PlayAttack(bool heavy)`: animates the weapon rig's own local transform through
  windup → strike → recover; duration = `CombatController.LightAttackDuration` (0.25s) or
  `HeavyAttackDuration` (0.45s). Returns to the rest pose captured at OnEnable
  (the `AttachToHand` pose: pos (0,-0.06,0), rot Euler(-12,0,0)).
- Style selected by weapon id (WeaponData.id / category):
  - iron_sword / katana / dagger: horizontal slash (+Y sweep ~ -100°).
  - greatsword / greataxe / warhammer: overhead chop (+X down ~ +130°).
  - lance: forward thrust (+Z lunge).
  - gauntlets: alternating quick jabs; off-hand clone staggered by half-period.
  - longbow: draw (raise + aim), loose (release + small recoil).
  - throwing_hammer: overhead wind-up then fling (+X forward).
  - staff / bone_wand / control_orb / lute / holy_book: raise + arc cast + scale pulse.
- Hook: `CombatController.LightAttack()/HeavyAttack()` (after `BeginAttack`/`OnAttackStarted`)
  call a new `NotifyWeaponAnimators()` that invokes `PlayAttack` on both hands'
  `GetComponentsInChildren<WeaponAnimator>()`.

## 4. Fighting mode: equipped weapon visible + swing
- Re-rig after model rebuild: in `PlayerController.Update`, if `FightingMode` and both
  `combat.RightHand`/`LeftHand` are null/missing → `TryAutoRigWeapon()` (covers model reload via
  `LoadPlayerModel`, which destroys old hand transforms + weapons).
- Grip pose nudge in `WeaponRigBuilder.AttachToHand`: localPosition → `(±0.02, -0.05, 0)`
  (outward X, right positive / left negative) so the blade clears the torso.
- Swing now provided by WeaponAnimator (item 3).

## 5. "Press E" prompt during fighting mode (root causes)
- `Assets/Scripts/Opt/NewWorldTestGround.cs` (~lines 467-472): rack proximity poll prints
  `"E - <weapon>"` every 0.2s when within 3.2u of a stand. Add: skip while `pc.FightingMode`.
- `Assets/Scripts/UI/InteractionPrompt.cs` (`LateUpdate`, lines 52-137): hide the persistent
  E-key / LMB prompts entirely while `FightingMode` (no contextual interact prompts in combat).

## 6. Skill bar — only in fighting mode + more spacing
- File: `Assets/Scripts/UI/NewWorld/SkillBarHUD.cs`.
  - Show only in fighting mode: gate the overlay on `PlayerController.FightingMode` in `Update`
    (combined with existing `_visible` + `ShowOnInGame`).
  - Increase slot spacing `slotSize * 1.8` → `slotSize * 2.2`.
- File: `Assets/Scripts/Player/PlayerController.cs` (`ToggleCombatMode`): on entering fighting
  mode call `SkillBarHUD.SetVisible(true)`; on exit `SetVisible(false)` (currently reversed).

## 7. Info tab — stats under XP bar in two columns
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs` (`BuildInfoTab`, lines ~282-311).
- 11 stat rows (PlayerStats.StatCount) currently in one right column.
- New layout: two columns of 6 + 5 directly under the XP bar.
  - Column A (stats 0-5): x0 = -330 (name), value at x0+160, "+" at x0+230.
    top row y = 96, row pitch 26.
  - Column B (stats 6-10): x0 = -40, same offsets / rows.
  - Class line y -80, Race line y -115, Change class/race buttons y -145.
- All within body bounds (±503 x, ±240 y).

## 8. Skills tab — layout + detail close
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs`.
  - `BuildSkillTypeBar` (lines 1349-1387): chip row y 176 → 190 ("higher" = near the title).
    `SkillPoints` / `CategoryLevel` info labels (lines 252-253) y 208 → 222.
  - `BuildSkillTree` (lines 408-434): viewport `P(0,-60) Sz(1000,360)` →
    `P(0,-55) Sz(940,370)`; tree content `Sz(680,540)` → `Sz(1000,700)` so deep radial trees
    (hub 78 + spoke 118 * depth) fit inside the mask.
  - `BuildSkillDetail` (lines 436-471): add an "✕" close button (top-right of the pane) that
    deselects `_selectedSkill` and refreshes (`RefreshSkillTree`) — hides the detail pane only,
    keeps the tab open. "Đóng" (bottom) keeps closing the whole menu.

## 9. Inventory tab — borders + slot sizes
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs`.
  - Equipment sheet (`BuildEquipmentSheet` + `SlotButton`, lines 955-1004):
    - Slot size `Sz(84,30)` → `Sz(62,42)` (narrower + taller).
    - Vertical pitch 38 → 44 (pos.y rows: -10, -54, -98, -142, -186, -230).
    - `EquipShiftX` -190 → -185 (sheet re-centered; left edge ~-435, right edge ~-83).
  - Backpack grid (`BuildStorageGrid`, lines 817-858):
    - Slots `Sz(40,46)` → `Sz(58,64)`; position `(250+col·44, 196-row·50)` →
      `(180+col·62, 190-row·68)`; label font from `h/96` → `h/80`.
    - Header `P(230,212)` → `P(180,214)`.
    - 5 cols × 62 → x span 180..428, right edge 486 (inside +503).
  - Hotbar mirror (`BuildHotbarMirror`, lines 861-902):
    - Label `P(140,-142)` → `P(60,-148)`; slots `Sz(32,40)` → `Sz(44,50)`;
      x `(140+i·36)` → `(60+i·42)`; y -176 → -185 (bottom ≈ -235).
  - Everything stays within ±(490, 232) so no side overflow at any window aspect.

## 10. Verify + ship
- Semantic checker: `dotnet run -- "D:\unity\new world\new-world\Assets" "D:\unity\Unity Hub\Editor\6000.5.1f1\Editor\Data\Managed"`
  in `C:\Users\antic\AppData\Local\Temp\opencode\syntaxcheck` → expect 0 diagnostics.
- UnitPlay test checklist (needs user, Unity can't be run here):
  1. Enter fighting mode near the weapon rack → no "E - Iron Sword" prompt.
  2. Info tab: stats under XP bar in two columns; + buttons work; class/race lines visible.
  3. Skills tab: category row near title; tree taller and fits; select a node → detail pane
     with X; X hides detail, tab stays open; Đóng closes whole menu.
  4. Inventory tab: nothing past either border; equipment slots narrower/taller; bag slots
     bigger; hotbar mirror slots bigger.
  5. Skill bar visible ONLY in fighting mode, slots spaced farther apart.
  6. Player model is colored; walks/runs visibly; sits still when idle.
  7. Squire iron sword + F1: sword visible in hand; LMB/RMB swing like the weapon's style.
- Commit: stage only touched scripts; push to `main`.

---

## 11. Batch 2 — bug fixes + HUD/skill-bar QoL

### 11.1 Trees/stones vanish right after spawning (root cause found)
- File: `Assets/Scripts/Opt/ChunkLodManager.cs` (`RegisterChunk`).
- Cause: every child of a terrain tile root was indexed into `entry.Details` — including the
  `Tree_*`/`Rock_*` props (`ChunkObject.SpawnProps`). `ApplyBand` then ran
  `kv.Value.SetActive(false)` over *all* Details children, hiding every prop within 2 frames of
  the tile spawning (terrain kept its root renderer).
- Fix: index only children whose name starts with `"Lod"` (matches the configured band
  `DetailName`s). Props are no longer toggled by the LOD system.

### 11.2 Info tab "+" button — smaller + only when a stat point is available
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs`.
- Button size `Sz(30,30)` → `Sz(22,22)` at `x0+222` (`BuildInfoTab`).
- `RefreshInfo`: each allocator's `gameObject.SetActive(canSpend)` where
  `canSpend = level != null && level.AvailablePoints > 0` (all hidden when 0 points).

### 11.3 Red ✕ close buttons
- File: `Assets/Scripts/UI/NewWorld/MenuPanelBase.cs`.
  - New shared helper `MakeRedClose(...)` (flat red image + white ✕ glyph).
  - The bottom gray "Đóng" pill is replaced by a red ✕ in the panel top-right corner.
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs` — skill-detail close now uses the same
  red ✕ helper.

### 11.4 `WeaponData` GetComponent crash on attack
- File: `Assets/Scripts/Combat/Weapons/CombatController.cs` (`CategoryOf`).
- Crash: `hand.GetComponent<WeaponData>()` — `WeaponData` is a ScriptableObject, not a Component.
- Fix: read `hand.GetComponent<WeaponRigHost>()?.Data`, fallback `WeaponCategory.Melee`.

### 11.5 No fighting-mode entry prompt
- File: `Assets/Scripts/Player/PlayerController.cs` (`ToggleCombatMode`).
- Removed `ShowPrompt("Fighting mode: Left click … Right click …")` on entering fighting mode
  (the rack prompt was already gated in batch 1).

### 11.6 Dynamic skill bar + bound-key display + assign on next key
- File: `Assets/Scripts/Combat/Skills/SkillBindings.cs` — new `KeyOf(skillId)` reverse lookup +
  `Bindings` enumerable.
- File: `Assets/Scripts/UI/NewWorld/SkillBarHUD.cs` — rewritten: renders one smaller slot
  (`slotSize = h*0.055`, spacing `slotSize*1.35`) per bound skill (sorted by key), showing skill
  name + key label (e.g. `G`, `1`); slots grow automatically as bindings are added (cap 12 
  rendered, unlimited keys bindable); still shown only in fighting mode.
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs`:
  - Skill detail appends a `Key: <key>` line when the selected skill is bound.
  - Subscribes `SkillBindings.OnKeyCaptured` → `RefreshSkillTree()` so the key label updates the
    instant the capture key is pressed.
  - "Bind Key" button relabeled "Assign Key". (Assign → press next key already worked;
    `SkillBindings.Update` fires the skill on every press of that key.)

### 11.7 HP/FP/Stamina bars visibly drain on decrease
- File: `Assets/Scripts/UI/NewWorld/PlayerBarsHUD.cs`.
- Fills now ease down toward their target (`DrainRate` ≈ 1.6/s) so any decrease is visibly
  rendered even with fast regen; increases/level-ups still apply instantly.
- Damage flash detection fixed (compared to the pre-sync raw value, previously dead code).
- `OnEnable` guards against creating duplicate canvases/bars on re-enable.

## 12. Batch 2 verify
- Semantic checker clean (0 diagnostics).
- UnitPlay checklist (needs user):
  1. Walk near fresh terrain chunks → trees/rocks stay visible (previously vanished on render).
  2. Info tab: small "+" buttons; hidden entirely when Stat Points = 0.
  3. Tab menus close via a red ✕ top-right; skill-detail closes via red ✕.
  4. F1 / LMB in fighting mode → no ArgumentException; sword swings normally.
  5. Entering fighting mode shows NO prompt text.
  6. Learn a castable skill → select it → detail shows e.g. "Key: G"; Assign Key + press G →
     key label updates and G fires the skill every press; skill bar grows one small slot per bind.
  7. Take damage / spend stamina or mana → the top-left bars visibly shorten (drain), then refill.

---

## 13. Batch 3 — fix "extremely lag" (per-chunk terrain merge)

### 13.1 Root cause
The streamer made **1 GameObject per 1x1-metre tile**: at render radius 3 that is 49 chunks x
900 tiles = **44,100 GameObjects**, each with its own 2-triangle mesh, own MeshCollider, own draw
call (plus the radius+2 keep margin could hold up to ~108,900). Physics, draw calls and the
per-frame `FindObjectsByType` sweep in EnemyHealthBarHUD all scaled with that object count; the
32-tile/0.1s finalize budget took ~138s to fill the radius and `GameBootstrap` built 9 chunks
(8,100 tiles) synchronously on one frame at startup.

### 13.2 Changes
- `Assets/Scripts/World/Terrain/ChunkMeshGenerator.cs`:
  - `BuildMergedMeshData(ChunkMeshData[] tiles)` — thread-safe merge of a chunk's 900 tile arrays
    into one 1800-triangle chunk-local mesh (3600 verts). Per-tile UVs/normals preserved, so
    visuals are identical to the old setup.
  - `CreateMeshFromMerged(...)` — main-thread Unity Mesh build.
- `Assets/Scripts/World/Streaming/TerrainChunkMeshData.cs` — carries `Tiles` (for tile-heightmap
  persistence) + new `Merged` arrays + `MergedChunkMeshData` struct.
- `Assets/Scripts/World/Chunks/ChunkObject.cs` — reworked to ONE object per `TerrainChunkCoord`:
  merged mesh + one collider; `SpawnProps` keeps the exact per-tile 1/200 RNG placement, props
  parented to the chunk root at world positions; `Release()` cleans mesh + props.
- `Assets/Scripts/World/Streaming/WorldStreamer.cs`:
  - New authoritative `LoadedChunks` dict (chunk -> ChunkObject); per-tile `_loadedData`/
    `_loadedObjects` kept (900 entries per chunk, same object) so `TryGetData`/`IsLoaded`/
    `ChunkValidator`/`ChunkSaveManager` work unchanged.
  - Stream/unload/save reworked to **chunk granularity** (`UnloadChunk(TerrainChunkCoord)`),
    hysteresis keep margin radius+2 → radius+1.
  - `FinalizeChunks()` — one GameObject (mesh + collider) per completed chunk; budget
    `Clamp(chunksPerFrame, 1, 8)` per 0.1s so the whole radius loads in ~1s with no spikes.
  - `GenerateChunkSync(tc)` now builds a single merged mesh (3x3 spawn area = ~9 objects, not
    8,100); `EnsureChunk(coord)` generates the whole containing chunk.
- `Assets/Scripts/Core/GameBootstrap.cs` — unchanged call site (3x3 sync now trivially cheap).
- `Assets/Scripts/UI/NewWorld/NewWorldSystems.cs` — LOD registration deltas on `LoadedChunks`
  (per chunk) instead of per-tile sweep.
- `Assets/Scripts/UI/NewWorld/EnemyHealthBarHUD.cs` — enemy discovery throttled to every 0.5s
  into a reused list (no per-frame 44k-object scene sweep).

### 13.3 Effects
~44,100 geometry objects / draw calls / physics colliders → ~50 per radius; startup + streaming
hitches and the per-frame HUD sweep eliminated. Terrain shape, trees/rocks, tile persistence and
the tile-level public API are unchanged.

### 13.4 Verify (needs user — Unity can't be run here)
- [ ] New-game spawn: no long one-frame hitch; ground present immediately.
- [ ] Run in any direction: chunks stream in smoothly, no frame spikes; terrain/trees/rocks stay
      visible; no console errors (watch for `ArgumentOutOfRange`/missing `ChunkData`).
- [ ] Mining (axe/pickaxe), hoe on fields, NPC/shop interactions, sit, fight — all still work
      (ground now has one collider per 30x30 chunk).
- [ ] Character Info tab + skills + red ✕ close still fine; enemy health bars still render.
- [ ] FPS stable vs. the previous build.
- [ ] Commit: stage only touched scripts + PLAN.md; push to `main`.

---

## 14. Batch 4 — white player, hidden iron sword, backpack layout

### 14.1 Player is all white (root cause)
- File: `Assets/Scripts/Models/MapBuilder.cs` (`ApplyBlockColor`, lines 13-30).
- Cause: `new Material(Shader.Find("Universal Render Pipeline/Lit"))` then `mat.color = color`
  sets only `_Color`. URP Lit's base color is `_BaseColor`, so the tint was ignored → every
  `MakeBlock` (player body, crops, props) rendered white.
- Fix: also `mat.SetColor("_BaseColor", color)` (kept `_Color` for the `Standard` fallback).
- Same fix in `Assets/Scripts/Models/WeaponModelBuilder.cs` (`MakeBlock`, line 37) so weapons
  (in hand and on the rack) are tinted too.

### 14.2 Iron sword equipped but invisible + no attack animation (root cause)
- Files: `WeaponRigBuilder` + `PlayerController`.
- Cause: `EquipInto → AttachToHand` looks up `PlayerModel/ShoulderR/HandR`; if the model/limbs
  don't exist at equip time (boot order, or the model is reloaded afterwards) it silently parents
  the rig onto the **player root** at local `(0,0,0)`. With blade top at y≈0.9 from the pivot the
  whole sword sits inside the torso → invisible. `TryAutoRigWeapon` then never re-rigs because
  `combat.RightHand` is non-null, and the `WeaponAnimator` swing plays on the hidden rig → "no
  attack animation whatsoever".
- Fix:
  - New public `WeaponRigBuilder.ReparentToHands(GameObject playerRoot)` — for each non-null
    `combat.RightHand/LeftHand`, if the rig's parent is not the matching hand bone and the bone
    now exists, re-parent it and re-apply the `AttachToHand` pose (shared `ApplyHandPose`).
  - `PlayerController.LoadPlayerModel` calls it after rebuilding the model.
  - `TryAutoRigWeapon` calls it before its early-return (so toggling combat mode re-seats a
    hidden rig). The existing Update re-rig still covers the destroyed-with-old-model case.

### 14.3 Backpack layout (Inventory tab)
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs`.
- Backpack = the "Backpack (storage)" 30-slot grid (right of the equipment sheet). Its bottom
  row (y −150..−214) overlapped the "Use bar (1-0)" mirror row (slots top at y −185), burying the
  bar and blocking bag↔bar switching.
- Fix:
  - `BuildStorageGrid`: slots base `(160,190) → (120,214)`, header `P(160,214) → P(120,238)`
    (left 40, up 24). Grid x span now 120..368; bottom row top −126, bottom −190.
  - `BuildHotbarMirror`: header `P(40,-148) → P(40,-162)`, slots `y −185 → −199` (down 14) so the
    bar is fully clear below the grid.
  - Equipment sheet right edge ≈ −123 → clear of the grid's new left edge.

### 14.4 Verify (needs user — Unity can't be run here)
- [ ] Player model colored (skin/hair/shirt/pants), not white; crops/trees/props tinted too.
- [ ] Enter fighting mode (F1): iron sword visible in the right hand; LMB/RMB shows the slash
      swing animation on the equipped sword.
- [ ] Inventory tab: backpack grid sits left (~40px) and up (~24px); the "Use bar (1-0)" row is
      fully visible under it and clickable — switching between bag and bar works.
- [ ] Weapons on the rack are tinted (not white).
- [ ] Semantic checker 0 diagnostics; commit only touched scripts + PLAN.md; push to `main`.

## 15. Batch 5 — bulletproof color, always-visible sword, unique per-weapon animations

User played batch 4 fresh and reported all three issues persisted (player still white, sword still
invisible when equipped, no using animation) plus the new request: a *unique using animation for
each weapon*. Batch 5 attacks each with a guarantee instead of another guess.

### 15.1 Bulletproof player/weapon coloring
- Files: `Assets/Scripts/Models/MapBuilder.cs`, `Assets/Scripts/Models/WeaponModelBuilder.cs`.
- `CreateSolidMaterial(Color)` (new public, shared by both): shader chain `Universal Render
  Pipeline/Lit` → `Standard` → `Unlit/Color`. Sets `_Color` (Standard path), and when the
  material exposes `_BaseMap` (URP Lit) assigns a cached 1×1 color `Texture2D` (covers URP/Lit
  builds that ignore the scalar) plus `_BaseColor = white` (avoid double-multiply); otherwise sets
  `_BaseColor` directly. Logs the resolved shader + color once to the console.
- `ApplyBlockColor` now caches via `CreateSolidMaterial`.

### 15.2 Sword always visible
- File: `Assets/Scripts/Combat/Weapons/WeaponRigBuilder.cs`.
- `AttachToHand` fallback (no hand bone) no longer parents at `(0,0,0)` inside the body — parks at
  the natural hand spot relative to the root `(±0.33, 0.72, 0.05)`, leaned −12°, so it reads as
  held even in first person. `ReparentToHands` migrates it onto the real bone the moment it exists.
- New `LogRigging` (static bool, default on) + one-time `[WeaponRig]` console lines for attach vs
  fallback, re-parent, and the final RightHand/LeftHand parent names — paste them if it still fails.

### 15.3 Unique per-weapon using animation (arm + weapon)
- Files: `Assets/Scripts/Combat/Weapons/WeaponAnimator.cs` (rewritten),
  `Assets/Scripts/Player/PlayerAnimator.cs`.
- `WeaponAnimator` drives the weapon's local swing **and** the owning shoulder pivot, with a
  per-weapon `MotionProfile` for all 15 ids: iron_sword quick slash; katana wide slash; greatsword
  overhead chop; greataxe bigger chop; warhammer slower deeper chop; lance two-handed lunge;
  gauntlets alternating punches; longbow raise/draw/loose; throwing_hammer overhand fling; dagger
  double jab; and five distinct casts (staff/holy_book/bone_wand/control_orb/lute) each with its
  own arc/bob/pulse. Two-handers mirror the off arm.
- Coordination with the walk/idle animator: new `PlayerAnimator.SuppressArms` (public) skips its
  shoulder writes while an attack is active; `WeaponAnimator` sets it on `PlayAttack`, restores it
  on recovery/`OnDisable`. Arm pose is composed on top of the captured base rotation, and the
  weapon rest pose is re-captured at every attack start, so re-parenting never breaks the swing.
- New public `PlayerAnimator.ShoulderL/R` accessors used for the mirrored supporting hand.

### 15.4 Verify (needs user — Unity can't be run here)
- [ ] Fresh Play (restart editor): player is colored, not white. First-attack console shows
      `[MapBuilder] Block material shader: '...'`.
- [ ] F1 fighting mode: sword visible in hand in both first person and third person (F5); console
      shows `[WeaponRig] Wpn_iron_sword attached to 'HandR'` (not the fallback line) if the hand
      model is present. LMB/RMB: you see a sword slash swing.
- [ ] Equip several weapons (rack or inventory): each produces its own distinct using motion
      (chop vs thrust vs punches vs draw vs cast, etc.).
- [ ] Backpack grid still left/up of the use bar (batch 4), bar clickable.
- [ ] Semantic checker 0 diagnostics; commit only touched scripts + PLAN.md; push to `main`.

### 15.5 Play-test follow-ups — weapon stays in bag on equip + weapon too small in hand
- User: dragging a weapon into the L/R hand slot left the item in the inventory, and the equipped
  weapon looked too small next to the player.
- File: `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs` — `EquipOwnedWeapon` now removes one copy
  of the weapon from the ToolManager inventory after a successful rig (`RemoveItemAmount`),
  then refreshes both grid/hotbar and the equipment sheet. Removal is skipped if the rig build
  failed; a weapon that was owned-but-never-picked-up (or the boot auto-equip) is a no-op.
- File: `Assets/Scripts/Combat/Weapons/WeaponRigBuilder.cs` — new `EquipScale = 1.25f` applied to
  the rig's local scale in `ApplyHandPose` and the chest-side fallback park, so in-hand weapons
  read proper-size against the ~1.5-unit blocky body (rack display scale unchanged).