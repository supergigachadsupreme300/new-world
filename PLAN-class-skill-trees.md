# Plan — Class Skill Trees

Each of the 17 classes gets its own **medium radial skill tree (13-15 skills: hub + 3 paths × 4-5 nodes, one capstone per path)**. Class skills are mechanically distinct from the ~1984-skill normal skill tree.

## Locked decisions
- Class skills are **auto-granted at class unlock** (no skill-point economy; prereqs are cosmetic — the tree shows the class's kit).
- Passives are **live only while that class is ACTIVE** — switching classes swaps modifier sets in/out.
- UI: a **General/Class sub-toggle** inside the existing Skills polar tree; class view reuses TreePan / MakeTreeNode / MakeTreeLine with the per-layer ring layout.
- New actives: Heal, Summon, Aura, Taunt, CC-Zone (slow/stun), Stealth, Lifesteal-Strike (7 new behaviors).
- New passive modifiers (15 kinds): MeleePower, SpellPower, Cooldown, AttackSpeed, Backstab, HealPower, Berserk(dynamic), ParryWindow, ConsumablePotency, DefenseMelee+EquipLoad, RangedHandling, AuraStrength, Blocking+StaggerResist, CraftSuccess+Repair, StaminaRegen, HpRegenPerSecond.
- Summoned allies are **procedural primitives** (no asset work).
- **All 15 modifiers wired into consumers** in the first pass.
- Each feature is committed and pushed separately.

## File map

### New
| File | Purpose |
|---|---|
| `Assets/Scripts/Combat/Skills/ClassSkill.cs` | Data: id, displayName, description, IsPassive, PrereqSkillIds, Cost, Layer, ClassEffects (IClassEffect[]), ClassMods (ClassMod[]) |
| `Assets/Scripts/Combat/Skills/ClassSkillCatalog.cs` | Static registry mirroring SkillCatalog; ~240 skills authored procedurally per class; EnsureBuilt/Find/ForClass |
| `Assets/Scripts/Combat/Skills/ClassEffect.cs` | IClassEffect + ClassSkillContext + 7 active behaviors + ClassMod/ClassModType enum |
| `Assets/Scripts/Combat/Skills/ClassSkillCaster.cs` | ExecuteClass(string id): validate active class, cooldown (SpellCaster keyed API), cost, run effect |
| `Assets/Scripts/Player/Stats/ClassPassiveManager.cs` | Mirrors RacePassiveManager/ReligionManager: caches active-class modifiers, subscribes to OnActiveClassChanged, 15+ getters |
| `Assets/Scripts/Combat/Effects/CCZone.cs` | Persistent placed zone ticking OverlapSphere, applies slow/stun to EnemyController, RingFlash visual |
| `Assets/Scripts/Combat/Effects/SummonedAlly.cs` | Combat minion (chase + attack), tag Companion, IDamageable, lifetime |

### Modified
| File | Change |
|---|---|
| `Assets/Scripts/Player/Stats/PlayerStats.cs` | Derived getters multiply by ClassPassiveManager (parry/equip/def/cooldown/heal/melee/magic/AS) |
| `Assets/Scripts/Player/PlayerController.cs` | Add Heal(int), IsInvisible, stealth + class regen hooks |
| `Assets/Scripts/Combat/AI/EnemyController.cs` | Add ForceTarget(taunt), _speedMultiplier/_stunned, skip invisible targets |
| `Assets/Scripts/Combat/Skills/SkillProfile.cs` / `SkillBindings.cs` | Class-skill fallback execution path |
| `Assets/Scripts/Player/Stats/ClassUnlocker.cs` | Add RestoreSave(...) + _restoredFromSave guard |
| `Assets/Scripts/Player/Stats/ClassData.cs` | ClassSkills[] roster reference |
| `Assets/Scripts/UI/NewWorld/CharacterInfoUI.cs` | General/Class toggle + BuildClassSkillTree() |
| `Assets/Scripts/Core/SaveManager.cs` | Save/Load unlockedClassIds + activeClassId |
| `Assets/Scripts/Combat/Weapons/SpellCaster.cs` | SpellPowerMul/CooldownMul scaling (via PlayerStats already) |
| `Assets/Scripts/Combat/Weapons/HitboxSystem.cs` (or melee hit pipeline) | Lifesteal proc |
| `Assets/Scripts/Combat/CombatController.cs` (parry/block path) | BlockingMul / StaggerResistMul |
| `Assets/Scripts/Core/Localization.cs` | Class-skill strings |
| `game-design.md` | §3.2.1 Class Skill Trees |

## Consumer wiring table (multiply/add at point of use)
| Modifier | Consumer |
|---|---|
| MeleePowerMul | PlayerStats.MeleeAtkPower |
| SpellPowerMul | PlayerStats.MagicAttackPower |
| CooldownMul | PlayerStats.CooldownMultiplier |
| AttackSpeedMul | PlayerStats.AttackSpeedScale |
| BackstabMul | backstab CriticalMultiplier (DamageCalculator callers) |
| HealPowerMul | PlayerStats.HealPowerMultiplier → HealEffect/consumables/PlayerController.Heal |
| BerserkScale | live = 1 + scale*(1 − HP/MaxHP), folded into MeleePowerMul |
| ParryWindowMul | PlayerStats.ParryWindow |
| ConsumablePotencyMul | consumable use path (ToolManager) |
| DefenseMeleeMul + EquipLoadBonus | PlayerStats.DamageReduction + EquipLoad |
| RangedHandlingMul | PlayerStats.RangedAccuracy |
| AuraStrength | AuraEffect/companion buff magnitude |
| BlockingMul + StaggerResistMul | CombatController block efficacy; poise/stagger intake |
| CraftSuccessMul + RepairMul | crafting/repair success rolls (ToolManager) |
| StaminaRegenMul | PlayerController.HandleStamina regen (product with religion) |
| HpRegenPerSecond | PlayerController.HandleStamina HP-regen line |

## Active wiring
- HealEffect → PlayerController.Heal(int) (new)
- TauntEffect → EnemyController.ForceTarget(Transform, float) + _tauntTimer suppressing TickTargets
- SummonEffect → SummonedAlly GO procedural, tag Companion
- CcZoneEffect → CCZone (slow/stun via EnemyController _speedMultiplier/_stunned)
- StealthEffect → PlayerController.IsInvisible; enemies skip invisible in TickTargets; hide renderers
- LifestealStrikeEffect → direct overlap damage + Heal(% damage)
- AuraEffect → timed buff stack (ApplyStaminaRegenModifier pattern), x AuraStrength

## Per-class tree authoring (~240 skills) — hub `{class}.hub` + 3 paths
| Class | Path A | Path B | Path C |
|---|---|---|---|
| Wanderer | Stamina regen → Heal "Field Bandage" | Consumable+Craft | small Melee+Def → basic strike |
| Warrior | MeleePower 8/8/10 → "Titan Swing" | Stagger+Def + "War Cry"(taunt) | AtkSpeed + "Whirlwind" |
| Mage | SpellPower 8/8/10 → fireball cast | Cooldown + "Frost Nova"(CC) | SpellPower + arcane bolt |
| Rogue | Backstab 10/10/12 → "Vanish"(stealth) | LifestealStrike "Vampiric Dagger" | crit + backstab capstone |
| Cleric | HealPower 8/8/10 → Heal "Cure Wounds" | Blocking + "Sanctuary" aura | Heal "Restoration" capstone |
| Berserker | BerserkScale + MeleePower | AtkSpeed + "Bloodlust"(lifesteal) | Melee + speed "Frenzy Toss" |
| Necromancer | Summon "Raise Skeleton" + SummonAtk | Summon "Raise Brute" capstone | lifesteal + bone strike |
| Samurai | ParryWindow 10/10/12 → riposte crit | parry-heal + "Iaijutsu" art | "Iai Stance" |
| Alchemist | Consumable 8/8/10 → "Philosopher's Stone" | CcZone "Acid Cloud" | CcZone "Sleep Dart" + repair |
| Knight | Def+EquipLoad tiers → Taunt "Challenge" | Blocking + "Stand Together" aura | Def capstone + strike |
| Archer | Ranged 8/8/10 → "Sniper Shot" | CcZone "Concussive Arrow"(stun) | wind shot + mobility |
| Enchanter | Cooldown + "Time Slow" CC | CcZone root tiers + "Gravity Well" | Cooldown capstone + aura |
| Brawler | Melee (unarmed) tiers + "Haymaker" | CcZone "Bone Throw" + Taunt "Bellow" | StaggerResist + "Whirlwind Fist" |
| Paladin | HealPower + Heal "Lay on Hands" | Taunt "Holy Provocation" + Blocking | "Smite" holy cast + "Aegis" aura |
| Bard | AuraStrength 8/8/10 → "Battle Hymn" | CcZone "Dissonance"(stun) | Heal-regen "Song of Rest" |
| Taoist | StaminaRegen 8/8/10 → "Talisman" cast | Cooldown + SpellPower | "Yi Symbol" regen capstone |
| Monk | Stamina+Def(unarmed) → "Iron Body" block | Heal "Meditation" + StaggerResist | CcZone "Chi Wave" + fist capstone |

## Save/Load
- SaveData: + `string[] unlockedClassIds`, `string activeClassId`.
- ClassUnlocker.RestoreSave(ids, activeId): clear/refill, set active, fire OnActiveClassChanged, set `_restoredFromSave` so Start/EvaluateAll don't clobber.
- Backward-compatible (missing fields → wanderer).

## Risks
- ~240-skill authoring is mechanical bulk → `Make()` helper (one line/skill).
- PlayerController/StaminaSystem dual track → class regen written religion-style.
- Enemy `_scanningTargets` companion gate → verify enemies aggro summons; fallback forces rescan.