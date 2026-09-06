# Plan: UI polish + player model visuals + per-weapon attack animation

> STATUS: Sections 1-9 implemented and semantic-checker clean (0 diagnostics).
> Section 10 remains (UnitPlay visual verification + commit).

This file is the durable plan for the current batch of work. It survives context compaction.
Mark each step as it is completed. When a step changes scope (discovered while implementing),
update this file to reflect reality.

Context note: weapons are already inventory items; F1 toggles fighting mode; equipped weapons
are rigged onto the player model hands (`PlayerModel/ShoulderR/HandR`, `PlayerModel/ShoulderL/HandL`)
by `WeaponRigBuilder.EquipInto`. This batch fixes visuals, animation, and the tab-menu layout.

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