# Game Design Document — "New World" (Working Title)

## 1. Game Overview

**Genre:** Open-World Action RPG (Elden Ring-inspired)
**Platform:** Unity (PC primary, Mobile secondary)
**Multiplayer:** Dedicated server with co-op/invasion/arena
**Core Loop:** Explore → Fight → Grow → Craft → Dominate

Seamless open-world with real-time action combat, classless progression via a **6-category skill-XP system**, an **11-stat** system, **15 unlockable classes**, and a **22-race system** (with passive-only racial kits), procedurally generated seed-based chunk terrain, and all existing CountryLife systems retained as optional side content. Combat is built on a **3-genre equipment** set (21 slots), an expandable **weapon architecture** (§3.6, Melee/Ranged/Magic), a **spell-casting pipeline** (§3.8) for magic, and **10 damage types** with **6 status effects** (§3.7).

---

## 2. World Generation System

### 2.1 Seed & Coordinate-Based World

- Every world defined by a **numeric seed** (long).
- World infinite in XZ plane, divided into **1x1 unit chunks**.
- Each chunk identified by **(chunkX, chunkZ)** integer pair.
- Same seed + coordinate always produces identical chunk (shared worlds on dedicated server).

### 2.2 Chunk Structure (4 Triangles, Heightmap)

Each chunk: **5 vertices** (4 corners + 1 center), split into **4 triangles** by X-diagonal.

```
C1─────────C2
 │ ╲  T1  ╱ │
 │   ╲   ╱  │
 │ T4 ╲╱ T2 │
 │     CE    │
 │ T3 ╱╲    │
 │   ╱   ╲  │
 │ ╱       ╲│
C3─────────C4
```

- **Corner vertices** shared with adjacent chunks (deterministic, never recalculated) → **zero gaps**.
- **Center vertex** unique per chunk, influenced by corners + noise.
- **Random angle pivot** applied to center vertex position offset for organic feel.
- **Strict edge matching:** edges computed from shared world coordinates → guaranteed seamless stitching.

#### Triangle Connectivity Rules

Each chunk has **5 vertices**:
- 4 corner vertices: shared between adjacent chunks (deterministic based on world coordinates)
- 1 center vertex: unique to the chunk
- 4 triangles: Top-Left, Top-Right, Bottom-Left, Bottom-Right

**No gaps allowed.** Edge vertices are deterministic based on world coordinates, guaranteeing seamless stitching between any two adjacent chunks regardless of load order.

### 2.3 Perlin Noise Layers (5 octaves)

Heights generated using **multiple octaves of Perlin noise**, each layer contributing to final terrain shape:

| Layer | Purpose | Frequency | Amplitude |
|-------|---------|-----------|-----------|
| 1 - Continental | Large-scale landmass shape | 0.001 | 40.0 |
| 2 - Hills | Rolling terrain | 0.005 | 15.0 |
| 3 - Detail | Small bumps and dips | 0.02 | 5.0 |
| 4 - Roughness | Micro-variance | 0.08 | 1.5 |
| 5 - Pivot Angle | Center vertex offset | 0.01 | 2.0 |

**Seed derivation:** Each noise layer uses `seed + layerIndex * 7919` as its seed offset to ensure different patterns per layer.

### 2.4 Neighbor-Dependent Generation

Each chunk's generation is influenced by its **4 direct neighbors** (N, S, E, W):
- Edge vertices are computed from the shared neighbor's edge (guaranteed seamless).
- Center vertex considers the heights of all 4 corners via interpolation + noise offset.
- This ensures smooth transitions and eliminates seams.

### 2.5 Chunk Loading & Render Distance

- The player controls **render distance** in chunk radius.
- **Default radius:** 5 chunks (121 chunks loaded).
- **Maximum radius:** 32 chunks (4,225 chunks loaded).
- At each frame, the system calculates which chunks are within radius of the player.
- Chunks entering radius: loaded from cache or generated.
- Chunks leaving radius: unloaded from memory (kept in cache on disk).

### 2.6 Chunk Persistence (File Caching)

- Each chunk saved as an individual file: `worlds/{seed}/chunk_{x}_{z}.dat`
- File contains: 5 vertex heights, terrain metadata, any modifications (damage, built structures).
- On first load: generate from noise → save to disk.
- On subsequent loads: read from disk (fast).
- Player modifications (terrain deformation, placed objects) are delta-patched into the chunk file.

---

## 3. Combat System

### 3.1 Real-Time Action Combat

Direct weapon/ability control with stamina management, dodge-rolling, blocking, and parrying. Inspired by Elden Ring's combat feel.

#### Core Mechanics

| Mechanic | Description |
|----------|-------------|
| **Light Attack** | Fast, low damage, low stamina cost |
| **Heavy Attack** | Slow, high damage, high stamina cost |
| **Dodge Roll** | i-frames during roll, costs stamina |
| **Block/Shield** | Reduces incoming damage, stamina drain on block |
| **Parry** | Frame-perfect timing for massive damage window |
| **Riposte** | Critical hit after successful parry |
| **Jump Attack** | Aerial downward strike, breaks guard |
| **Charged Attack** | Hold to charge for more damage |
| **Weapon Arts** | Unique per weapon type, costs FP (Focus Points) |

#### Damage Formula

```
Final Damage = (Attack Power x Skill Multiplier x Weakness Multiplier)
               - (Target Defense x Defense Multiplier)
               x Damage Type Modifier   # §3.7: attacker's DamageType vs
                                        #   target equipment resistance
               x Critical Modifier (if applicable)
```
The **DamageType Modifier** resolves the specific damage type (§3.7 — Physical, Fire, Ice, Lightning, Holy, Dark, Wind, Earth, Water, Arcane) of the weapon or spell against the target's per-type equipment resistance. `DamageCalculator` routes the attacker's type → the target's resistance table (see §3.6/§3.8).

#### Stamina System

- Stamina regenerates over time (pauses briefly after actions).
- Each action costs stamina.
- Stamina management is the core skill expression.

### 3.2 Classes (15 Unlockable)

The game uses a **classless unlock system**. Players start as a **Wanderer** (base class) and unlock classes by meeting stat thresholds or finding class trainers/items in the world.

#### Starting Base

- **Wanderer:** Balanced starting stats, no special abilities. Can go anywhere.

#### Unlockable Classes

| # | Class | Unlock Requirement | Unique Mechanic |
|---|-------|-------------------|-----------------|
| 1 | **Warrior** | Str >= 20 | Weapon Arts enhanced, stance breaking |
| 2 | **Mage** | Wisdom >= 20 | Spell casting, magic damage |
| 3 | **Rogue** | Dex >= 20 | Backstab bonus, stealth attacks |
| 4 | **Cleric** | Fth >= 20 | Healing miracles, buffs |
| 5 | **Berserker** | Str + End >= 35 | Damage increases as HP drops |
| 6 | **Necromancer** | Wisdom + Fth >= 35 | Summon undead allies |
| 7 | **Samurai** | Dex + End >= 35 | Perfect parry window extended |
| 8 | **Alchemist** | Any 2 stats >= 18 | Enhanced consumable effects |
| 9 | **Knight** | Defense + Str >= 35 | Buffs **defense & melee together** — stronger at each than a baseline but weaker than dedicated Paladin (defense) or Warrior (melee); increases equip-load carry (armor grants more defense) |
| 10 | **Archer** | Dex >= 20 | Ranged **accuracy & handling** (faster nock/reload, less sway) |
| 11 | **Enchanter** | Intelligence + Wisdom >= 35 | Control/zone mage (slow, roots, area denial) |
| 12 | **Brawler** | Str >= 20 | Unarmed/grapple crowd control |
| 13 | **Paladin** | Fth + End >= 35 | Holy tank/support (taunt, guard allies, sacred armor effectiveness) |
| 14 | **Bard** | Fth + Intelligence >= 35 | Party-wide buffs/auras |
| 15 | **Blacksmith** | Crafting skill >= level 10 | Crafting/forge support: gear upgrade success, repair, forging bonuses (skill-based, not stat) |

Classes are **not exclusive** — if stats allow, a player can unlock multiple classes and mix abilities.

### 3.3 Skill System (use-based XP — supersedes original skill tree)

> **Scope update:** The originally planned giant skill TREE was replaced by a simpler, more
> content-efficient **use-based Skill XP system** (6 categories, flat tier rewards) to pair
> with the race system. See §3.5. Equippable active/ultimate skills and skill books remain
> planned; only the node-tree progression was removed.

A **use-based skill progression** spans multiple combat/utility categories. No fixed class requirements — any player can advance any category based on how they play. Skills level by gaining XP in their category (with racial multipliers) and grant flat tier rewards at levels 5/10/15/20/25.

#### Skill Categories

```
   [MELEE]  [RANGED]  [MAGIC]
      \        |        /
      [SURVIVAL: STEALTH + CRAFTING + FORTITUDE]
```

#### Skill Types

| Type | Description | Examples |
|------|-------------|---------|
| **Category Passive** | Tier reward from leveling a category | +10% stamina regen, +5% crit |
| **Active** | Equippable combat ability | Fireball, Heal, Backstab |
| **Weapon Art** | Weapon-specific unique skill | Whirlwind, Shield Bash, Arrow Rain |
| **Ultimate** | Powerful endgame ability | Meteor, Time Slow, Blood Rite |

#### Skill Book Expansion

- **Skill Books** found in the world or bought from merchants can:
  - Unlock new **Active / Weapon Art / Ultimate** skills
  - Grant bonus skill points or category XP
  - Reveal hidden ultimate paths
- The system is **expandable** — new skill books can add entirely new skills and categories post-launch.

### 3.4 Stats (11 Core)

The stat system was redesigned into **11 stats**. The **Arcane stat** was removed (its functions split into **Luck** and **Wisdom**) — note this is distinct from the **Arcane damage type** (§3.7), which remains a separate combat element. Old names were renamed for clarity: *Vigor→Health*, *Mind→Intelligence*, *Intelligence→Wisdom*. New stats added: **Speed**, **Defense**, **Luck**, **AttackSpeed**.

| Stat | Effect |
|------|--------|
| **Health** | Max HP, HP regen, resistance to status effects |
| **Speed** | Movement speed, dodge speed, **small** attack-speed bonus |
| **Endurance** | Max stamina, equip load (heavier armor/weapons) |
| **Strength** | Melee damage (heavy), stagger power |
| **Dexterity** | Light/one-handed melee damage, ranged **accuracy**, parry window, dodge i-frames, weapon-swap speed |
| **AttackSpeed** | **Primary** source of attack speed (larger per-point than Speed's bonus) |
| **Defense** | Flat **physical** damage reduction (equipment/armor-based) |
| **Intelligence** | Max FP (mana), skill cooldown reduction |
| **Wisdom** | Magic damage, spell power |
| **Faith** | Miracle/healing power, buff duration |
| **Luck** | Loot quality, crit chance, crafting luck, status-effect luck |

**Leveling:** Earn XP from combat, quests, exploration. Spend points on stats at bonfires/rest points.

**Stat splits (design notes):**
- **Speed vs AttackSpeed vs Dexterity:** Speed = raw velocity (movement velocity + dodge speed), with only a *small* effect on attack speed. AttackSpeed = the dedicated stat for **how fast you swing/attack** — a large per-point effect. Dexterity = *precision finesse*: light/one-handed melee damage, ranged accuracy, parry window, dodge i-frame quality, weapon-swap speed (it also contributes a little attack speed as finesse).
- **Strength vs Dexterity (damage):** Strength scales **heavy/melee** damage and stagger. Dexterity scales **light/one-handed melee** and finesse. Ranged **damage** scales with the **weapon itself** (bows/arrows have their own damage ceiling) — stats instead govern how well a player *uses* a ranged weapon: **Dexterity** for accuracy, **Endurance** for equip load (heavy bows), **Speed/Dexterity** for handling.
- **Defense vs Health:** Health = your HP pool and regeneration. Defense = flat reduction of incoming **physical** damage. **All resistance — physical, elemental, magic — comes from equipment (armor/gear) only, not from stats.** No stat grants damage resistance.

#### Derived Stat Formulas

Stats convert to gameplay numbers via these formulas. **Racial % modifiers apply to the stat BEFORE these formulas run**, so a racial bonus compounds (grows) as the player invests and levels that stat. Scaling coefficients marked `k_*` are **balance knobs** finalized during implementation/tuning.

```
MaxHP          = 100 + (Health × 12)
MoveSpeed      = base + (Speed × k_mov)               # movement velocity
DodgeSpeed     ×= 1 + (Speed × k_dodge)
AttackSpeed    ×= 1 + (AttackSpeed × k_as)            # PRIMARY attack-speed source (large)
AttackSpeed    ×= 1 + (Speed × k_as_speed)            # small bonus only (k_as_speed ≪ k_as)
AttackSpeed    ×= 1 + (Dexterity × k_as_dex)          # small finesse bonus (k_as_dex ≪ k_as)
MaxStamina     = 100 + (Endurance × 10)
EquipLoad      = 40  + (Endurance × 2)                # weight units of armor/weapons
MeleeAtkPower  = base + (Strength × k_str)            # heavy/melee → DamageCalculator.AttackPower
StaggerPower   = base + (Strength × k_stag)
LightAtkPower  = base + (Dexterity × k_lt)            # light/one-handed melee
RangedAccuracy = 1 + (Dexterity × k_racc)
RangedDmg      = weapon.base                          # ranged damage = weapon ceiling, NOT stats
ParryWindow    = base + (Dexterity × k_parry)
DamageReduc    = clamp(Defense × k_def, 0, 0.8)       # flat physical DR, capped
MaxFP          = 50  + (Intelligence × 10)
CooldownMult   = 1 − (Intelligence × k_cool)          # faster ability cooldowns
MagicAtkPower  = base + (Wisdom × k_mag)              # → DamageCalculator.ElementalPower
HealPower      ×= 1 + (Faith × k_heal)
BuffDuration   ×= 1 + (Faith × k_buff)
CritChance     = 5%  + (Luck × 0.15%)                 # → DamageCalculator critical
LootQuality    = base + (Luck × k_loot)
CraftLuck      = base + (Luck × k_craft)
StatusProcLuck = base + (Luck × k_status)             # poison/bleed/rot/frost procs
```

Example — a race with **Health +20%**: at base Health 30 → total 36 → MaxHP = 100 + 36×12 = **532** (vs unmodified 460). Because the bonus scales with the total stat, it represents ~16–18% more HP in the late game.

**Damage calculator wiring:** Strength/Dexterity/Wisdom/Luck feed the `DamageCalculator` context (AttackPower, LightAttackPower, ElementalPower, CriticalMultiplier). Ranged damage uses the **weapon's base damage** directly. Incoming damage is reduced by **equipment-based resistances** (armor physical DR, gear elemental/magic resist) — no stat contributes resistance. The 6 skill-XP categories (Melee, Ranged, Magic, Stealth, Crafting, **Fortitude**) are separate from the 11 stats.

### 3.5 Race System (22 Races)

Players pick a race at **character creation** (weighted-random roll that auto-commits, or manual pick). Races are also **discoverable in the world** at altar/ritual sites, which unlock them and allow a **mid-play race change** (costs a rare Ritual Stone; Human is always free to swap to).

#### How Races Modify Stats

Racial stat modifiers are **percentages applied to the TOTAL stat on-the-fly**, so they **scale as the player levels**:

```
TotalStat = BaseStat × (1 + RacialPercent)
```

- Positive % (e.g. Str +20%) multiplies the whole stat, growing stronger with investment.
- Negative % (e.g. Dex -10%) is a permanent handicap the player must build around.
- Modifiers recalc immediately whenever the player levels or changes race.

#### Skill XP Categories (6)

Instead of a node-based skill tree, skills level via **use-based XP**. Each category has its own XP bar and flat tier rewards at levels 5/10/15/20/25. Races grant **XP multipliers** in categories that match their archetype, pushing builds in a natural direction.

| Category | Tracks | Example Uses |
|----------|--------|--------------|
| **Melee** | Melee proficiency | Sword/mace/axe damage, combos, stagger |
| **Ranged** | Ranged proficiency | Bow accuracy, crossbow, thrown weapons |
| **Magic** | Spell/miracle proficiency | Spell power, cast speed, FP efficiency |
| **Stealth** | Stealth/survival proficiency | Sneak damage, detection range, lockpicking |
| **Crafting** | Crafting/gathering proficiency | Potion potency, upgrade success, yield |
| **Fortitude** | Defensive proficiency | Shield stability, armor effectiveness (physical DR), perk effectiveness |

#### The 22 Races

Stat modifiers shown as %. Weights shown for the random-roll. XP bonus = skill categories that level faster.

| # | Race | Stat Modifiers | Passive | Weight | XP Bonus |
|---|------|----------------|---------|--------|----------|
| 1 | **Human** | None (AS+0) | +15% XP from all sources | 50% | All +15% |
| 2 | **Fire Giant** | Health+20 Str+20 End+15 Speed-5 Int-15 AS-15 | Fire resistance (50%), lava walk | ~2.38% | Endurance +15% |
| 3 | **Serpent-kin** | Luck+15 Dex+15 End+10 Str-10 AS+5 | Venom Blade: physical attacks apply venom DoT for 8s | ~2.38% | Magic +10%, Stealth +10% |
| 4 | **Draconic** | Str+20 Wisdom+15 End+5 Int-10 AS+5 | Fire resistance (40%), Dragon Roar (stagger nearby, 30s CD) | ~2.38% | Strength +10%, Faith +5% |
| 5 | **Golem** | Str+25 End+25 Health+15 Speed-10 Int-20 AS-15 | **Stone Skin:** 25% physical + 25% magic dmg reduction; move speed -20% | ~2.38% | Endurance +15%, Fortitude +20% |
| 6 | **Celestial** | Faith+25 Int+10 Health+10 Str-10 AS+5 | Healing miracles 20% stronger | ~2.38% | Faith +15% |
| 7 | **Wraith** | Wisdom+25 Luck+15 Int+10 Health-15 AS+5 | **Immaterial:** pass through all physical objects, immune to physical dmg, spell-caster only; no dash/run. Takes +30% magic dmg, +50% holy dmg | ~2.38% | Magic +15% |
| 8 | **Undead** | Health+10 End+15 Dex+10 Int-10 AS+5 | Infinite stamina. Takes +25% fire dmg, +25% holy dmg | ~2.38% | Endurance +15% |
| 9 | **Skeleton** | Dex+20 Str+10 End+10 Health-15 AS+10 | Bleed immune, +20% move speed, infinite stamina | ~2.38% | Fortitude +15% |
| 10 | **Werewolf** | Str+20 Dex+20 End+5 Int-15 AS+10 | Night: +25% move speed + 2% HP regen/s. Claws deal bleed | ~2.38% | Melee +15% |
| 11 | **Goblin** | Dex+20 Luck+15 End+5 Str-10 AS+5 | +20% loot quality, 15% smaller hitbox | ~2.38% | Crafting +15%, Stealth +10% |
| 12 | **Orc** | Str+25 Health+15 End+10 Int-15 AS+5 | +15% stagger damage, passive HP regen (1% max HP/s) | ~2.38% | Melee +15%, Endurance +10% |
| 13 | **Ice Giant** | Health+15 Str+20 End+20 Speed-5 Int-15 AS-15 | Cold immune, freeze aura (nearby enemies slowed 20%) | ~2.38% | Endurance +10%, Strength +10% |
| 14 | **Vampire** | Dex+20 Luck+10 Wisdom+10 Int+5 Str-10 AS+10 | 5% lifesteal on hit, +15% move speed. Sunlight: 5% max HP burn/s | ~2.38% | Magic +10%, Stealth +10% |
| 15 | **Demonkin** | Str+20 Wisdom+15 End+10 Faith-15 AS+5 | Fire resistance (40%), fire aura (1% max HP/s to nearby) | ~2.38% | Magic +10%, Melee +10% |
| 16 | **Angel** | Faith+25 Int+15 Wisdom+10 Str-10 AS+5 | Elemental resist (20% fire/ice/lightning/magic) via gear; weak to physical (+15%) and dark (+25%) | ~2.38% | Faith +15% |
| 17 | **Succubus/Incubus** | Dex+15 Wisdom+10 Luck+10 Int+10 Str-15 Health-10 AS+10 | Charm Gaze: opposite-gender targets have 10% chance to be confused | ~2.38% | Magic +15% |
| 18 | **Fishmen** | Dex+10 End+15 Health+15 Str+10 Faith-10 AS+5 | Swim speed +50%, breathe underwater, water dmg immune | ~2.38% | Ranged +10%, Crafting +10% |
| 19 | **Harpy** | Dex+25 Luck+15 Str-15 End-20 AS+10 | Glide (slow fall), jump height +30% | ~2.38% | Ranged +15% |
| 20 | **Dwarf** | Str+15 Faith+10 End+25 Dex-10 AS-5 | +20% crafting yield, forge discounts | ~2.38% | Crafting +15%, Fortitude +10% |
| 21 | **Gnome** | Luck+35, all 10 other stats -10% | **Lucky Find:** +40% loot bonus (best loot/crit/craft/status luck in the game), 15% smaller hitbox | ~2.38% | Magic +15%, Crafting +10% |
| 22 | **Elf** | Dex+20 Wisdom+15 Str-10 AS+10 | +8% all XP, enhanced perception (see hidden at +20% range) | ~2.38% | Magic +10%, Ranged +10% |

#### Stat Balance & Tiers

Races deliberately use a **wide net-stat-budget spread**, because racial % modifiers compound with leveling (§3.4). Races with a lower stat budget are compensated with **stronger passives, utility, or XP bonuses** so every archetype stays viable — races differ in *where* their power sits as much as *how much* raw stat power they carry.

| Tier | Net Budget | Races | Compensation for the gap |
|------|-----------|-------|--------------------------|
| **Strong** | +45 | Vampire, Fishmen, Angel | Vampire: harsh sunburn (5% max HP/s in daylight); Fishmen: situational water-bias; Angel: weak to physical & dark |
| **Good** | +40 | Celestial, Wraith, Werewolf, Orc | Each has a meaningful defensive/utility weakness |
| **Fine** | +35 | Serpent, Draconic, Skeleton, Goblin, Demonkin, Dwarf, Elf | Moderate weaknesses; XP bonuses |
| **Mid** | +30 | Undead, Succubus | XP bonuses + light weakness (fire/holy, frailty) |
| **Tank** | +20 | Fire Giant, Golem, Ice Giant | Strong defensive passives: Stone Skin (physical+magic −25%), freeze aura, fire resistance — pure-tank identity |
| **Aerial** | +15 | Harpy | Mobility (glide, enhanced jump) + Ranged XP; glass-cannon utility |
| **Baseline** | 0 | Human | +15% XP from all sources; the default/no-penalty race |
| **Handicap** | −65 | Gnome | **Best loot/crit/craft/status luck in the game** via Lucky Find + Luck stat (top-tier loot) + Magic/Crafting XP — high-risk glass cannon, weak in every other stat (all 10 others −10) |

> **Note on Human vs Elf:** Human (0 stat budget, **+15% XP**) is the **safe default** (50% weight, no penalties) — the only race whose identity is raw XP gain. Elf (+35 budget, +8% XP) is a stricter min-max pick trading XP for stats. Human's identity is reliability + faster progression; Elf's is raw stat advantage.

#### Race Selection & Weighted Random

- **Human 50%** chance; **each of the other 21 races ~2.38%** (50% ÷ 21).
- The roll **auto-commits** (player keeps what they roll).
- All races remain **manually pickable** if the player prefers a specific one.
- Locked races are revealed by **world discovery points** (altars/ritual sites); interacting unlocks them for this and future characters and enables mid-play transform.

#### Race Change (Mid-Play)

- Discovered races can be swapped to at any **Race Discovery Point**.
- Cost: **1 Ritual Stone** (rare consumable). Human is always free.
- On change: `PlayerStats` modifiers refresh, `RaceRig` swaps the model, `RacePassiveManager` re-applies passives. Current HP/FP/stamina preserved as % of their new max.

#### Race Visuals (Separate Rigs)

- Each race has its **own rig** (model + scale + offset + material tint).
- Rigs are **data-driven** (`RaceData` → `RaceRig`). Procedural placeholder bodies ship now; real 3D models drop in later without code changes.

#### Expandability

- `RaceData` is a ScriptableObject — adding a race = creating a new `.asset` (zero code changes).
- New races can ship post-launch via updates / content drops.

### 3.6 Weapon Architecture (Expandable)

Weapons are built on a **3-category base — Melee, Ranged, Magic** — structured so new categories/subtypes drop in without touching existing code. The core principle: separate **what a weapon is** (data) from **how it attacks** (delivery behavior) from **how damage resolves** (damage pipeline).

#### Layers

- **Layer 1 — `WeaponData` (ScriptableObject, data-only).** Shared fields: id, display name, weight (equip-load), Str requirement (weight class, §5.5), hand usage (single / dual / two-hand), base damage, speed, attack reach, scaling stat(s) + coefficients, `WeaponCategory`, `DamageType` (one of the 10 damage types, §3.7), and a Weapon Art reference. **Magic weapons** additionally carry magic mods — `MagicDamageMult`, `CastTimeMod`, `CooldownMod` (staff/wand/book scale spells).
- **Layer 2 — `WeaponCategory` enum (expandable).** `Melee`, `Ranged`, `Magic`. Future values (Thrown, Shield, Summon, Hybrid, …) slot in as new enum entries + one behavior class each.
- **Layer 3 — Behavior modules via `IWeaponBehavior`.** A minimal contract: `BeginAttack(cmd)`, `ActiveFrame()`, `Cancel()`. One concrete module per category:
  - **`MeleeWeaponBehavior`** → existing `HitboxSystem` arc sweep.
  - **`RangedWeaponBehavior`** → projectile/raycast, **consumes ammo** (arrows/bolts from inventory), accuracy from Dexterity.
  - **`MagicWeaponBehavior`** → routes to the spell/skills pipeline; the equipped staff/wand/book's magic mods scale the spell (damage %, cast time, cooldown); costs FP; spell power from Wisdom.
  - `CombatController` talks **only** to `IWeaponBehavior` — it never knows melee vs ranged vs magic. **Adding a weapon kind = one new behavior class.**
- **Layer 4 — Damage pipeline & registry.** `DamageCalculator` (existing flexible `HitContext`) stays the single damage formula, extended to carry the weapon's `DamageType` (one of the 10 damage types, §3.7) for per-hit element/resist resolution. `WeaponDatabase` (ScriptableObject registry) holds all weapon assets and resolves each equipped weapon's category → behavior.

#### Per-Category Mechanics

| Category | Delivery | Damage Source | Key Stat | Resource |
|----------|----------|---------------|----------|----------|
| **Melee** | Hitbox arc | weapon.base + Str/Dex scaling | Str (heavy) / Dex (light) | Stamina |
| **Ranged** | Projectile / raycast | `weapon.base` (weapon ceiling) | Dex (accuracy) | **Ammo** (arrows/bolts) |
| **Magic** | Spell / skill pipeline | spell base × Wisdom, modulated by weapon magic-mods | Wisdom | FP |

#### Notes

- Weapons carry a **single `DamageType`** — one of the **10 damage types** (§3.7); the damage pipeline resolves that element/type's resist/weakness.
- Dual-wield can pair **two of the same weapon type** — each hand holds one owned copy (one rig = one copy), subject to the §5.5 copy-accurate accounting rule: equipping the second hand consumes a spare bag copy, and without a spare the weapon *moves* instead of duplicating.
- Magic weapons are **equipped gear that scales/alters spells** rather than delivering their own attacks — distinct from melee/ranged, which deliver their own.
- Hand/wielding integration (§5.5): the equipped hand slots hold `WeaponData`; the categories of equipped weapons determine which behaviors are active. Wielding states modulate Str requirement as specified.
- Ranged ammo ties into the Inventory/consumables system.

### 3.7 Damage & Status Types

All damage is one of **10 damage types**. Every weapon, spell, and ability declares a **single `DamageType`** (per the single-element rule in §3.6); armor/gear provides resistance per type (equipment-only rule, §3.4). The `DamageCalculator` resolves the attacker's type against the target's resistance.

#### The 10 Damage Types

| # | Type | Description |
|---|------|-------------|
| 1 | **Physical** | Weapon/kinetic damage (blunt, slash, pierce — aggregated as one type). Reduced by Defense/armor. |
| 2 | **Fire** | Heat/burn damage. |
| 3 | **Ice** | Frost/cold damage. |
| 4 | **Lightning** | Electric damage. |
| 5 | **Holy** | Light/divine damage (strong vs undead/dark). |
| 6 | **Dark** | Shadow/void damage (strong vs holy). |
| 7 | **Wind** | Air/force damage. |
| 8 | **Earth** | Stone/ground damage. |
| 9 | **Water** | Water/fluid damage. |
| 10 | **Arcane** | Generic magic/arcane damage — the distinct "magic" damage type. |

*Physical and Arcane are themselves damage types; weapon **categories** (Melee/Ranged/Magic-delivery, §3.6) are a separate dimension — a melee weapon can deal Fire, a staff can deal Ice, etc.*

#### Status Effects (separate dimension)

Status effects are **not damage types** — they are applied **on hit** and do DoT / crowd-control, scaled by **Luck** (`StatusProcLuck`, §3.4):

| Status | Effect |
|--------|--------|
| **Bleed** | Accumulating damage-over-time on repeated hits |
| **Poison** | Damage-over-time over a duration |
| **Rot** | Strong, lingering damage-over-time |
| **Frost** (frostbite) | Builds up, then a burst + slow |
| **Burn** | Fire damage-over-time + light stagger buildup |
| **Stagger** | Poise break / crowd-control (interrupts actions) |

### 3.8 Spell-Casting Pipeline

Spells are how the **Magic** weapon category (staff / wand / book) deals damage and casts abilities. The pipeline connects the weapon architecture (§3.6), the skill system (§3.3), the stats (§3.4 Wisdom/Intelligence), and the damage types (§3.7).

#### SpellData (ScriptableObject)

A spell is a data asset carrying:

- id, display name, icon
- `DamageType` (one of the 10 damage types, §3.7) — or **none** for pure utility/heal spells
- base power
- **FP cost**, **cast time**, **cooldown**
- range, area/radius, projectile vs instant vs self/zone
- cast animation reference
- optional status-effect application (e.g., applies Burn/Frost; §3.7)

#### Casting Flow

1. Player equips a **Magic weapon** (staff/wand/book) in a hand slot.
2. The weapon's magic mods — `MagicDamageMult`, `CastTimeMod`, `CooldownMod` — modulate the spell before resolution.
3. `MagicWeaponBehavior.BeginAttack` routes the cast to `SpellCaster`.
4. `SpellCaster` validates **FP** (`MaxFP` from Intelligence) and **cooldown**; if valid, begins the **cast time**.
5. On cast completion, a `SpellEffect` spawns (projectile / instant / zone).
6. `DamageCalculator` resolves the spell with its `DamageType` against the target's equipment resistance; **Wisdom** scales spell power (`MagicAtkPower`), and `CooldownMult` from Intelligence shortens reuse.

#### Spell Sources

- **Equipped weapon** — a staff/wand/book in a hand slot (its magic-mods apply).
- **Active skills** (§3.3) — spells granted via skills/skill books can also be cast from the skill bar; they route through the same `SpellCaster` so the pipeline is shared.

#### Expandability

Adding a spell = creating a new `SpellData` asset (zero code changes), consistent with the rest of the data-driven systems.

---

## 4. Multiplayer System (Dedicated Server)

### 4.1 Architecture

- **Dedicated server** runs the authoritative world state.
- Players connect as **clients**.
- Server handles: chunk generation, enemy AI, loot drops, world state, anti-cheat.
- Client handles: input, rendering, audio, local effects.

### 4.2 Multiplayer Modes

| Mode | Description | Players |
|------|-------------|---------|
| **Solo** | Play alone on a server (local or remote) | 1 |
| **Co-op** | Invite friends to your world | 2-4 |
| **Invasion** | Hostile players enter your world to fight | 1-6 |
| **Arena** | PvP duel zones with matchmaking | 2-8 |
| **World Boss** | Open-world bosses with multiplayer participation | 4-16 |

### 4.3 Networking Requirements

- Chunk synchronization (server generates, clients receive height data).
- Player position/action synchronization.
- Enemy state sync (AI, health, attacks).
- Loot synchronization.
- Chat/text communication.
- Matchmaking and session management.

### 4.4 Anti-Cheat

- Server-authoritative damage calculation.
- Position validation (no teleport hacking).
- Action rate limiting.
- Chunk data integrity checks.

---

## 5. Retained Side Content (from CountryLife)

All existing CountryLife systems are retained as optional side content within the open world. The
following inventory is code-confirmed against `Assets/Scripts` (see §6 for what was actually removed).

### 5.1 Tools & Inventory

The **ToolManager** drives tools and inventory: **40 item slots** — a **10-slot hotbar** (number
keys, Minecraft-style quick bar) plus a **30-slot backpack storage grid** (Character Info →
Inventory tab). Tools swap a matching **3D model** on equip (`ToolManager.ToolModels.cs`).

| Tool | Use |
|------|-----|
| **Hoe** | Till soil for planting |
| **Sickle** | Harvest crops (yields quality bonuses, skill XP, quest progress) |
| **Axe / Mattock** | Gather materials |
| **Fishing Rod** | Fish (gift from Jessica) |
| **Hammer** | Open the build menu (**hold Hammer + F**) |
| **Club** | Melee demons; knock out thrashing fish on the shore |
| **Rosary** | Ranged holy orb — **one-shots** enemies but costs **1 Karma** per shot |

Drop items with **Q**; slot API: `SelectSlot` / `PeekSlot` / `AddItem` / `RemoveItem` / `MoveSlot`,
with `GetInventorySave()` / `LoadInventorySave()`.

Weapons are also physical bag items in this same 40-slot inventory (stack-counted, one copy per
equipped rig); the `WeaponInventory` owned list gates what can be equipped to the hand slots, and
equip/unequip is copy-accurate (§5.5).

### 5.2 Farming

- **10 seed types** (wheat, corn, potato, carrot, tomato, strawberry, pumpkin, onion, sugarcane,
  rice), planted via `TryPlantSeed` on tilled plots.
- Plant → multiple **growth stages** → harvest with the **Sickle**; quality bonuses scale rewards,
  plus skill XP and story-quest progress (`ToolManager.cs:1000-1035`).
- Farm quests push you from **50 → 150 wheat** harvested ("Mùa Thu Đầu Tiên" → "Bàn Tay Xanh").
- Crop risk via random events: weeds, pests, drought.

### 5.3 Fishing

- Stand by the west sea, cast with the **Fishing Rod** (LMB), wait for the **float to bubble**,
  then start the reeling **minigame** (keep the line in the green zone to fill the bar; ~80s
  window). FSM-driven.
- Fish that flop on the shore are knocked out with the **Club**, then picked up.
- Catch & sell prices: **Carp 15 / Salmon 25 / Tuna 40 / Puffer 60**.
- **Rod levels 1–3** and **bait** purchasable; the Fishing Shop vendor and Jessica's 3-fish quest
  are wired.

### 5.4 Crafting

- Crafting stations placed in player homes or found in towns — driven by
  **`CraftingManager`** with resolvable **station categories** (`ResolveStationCategory` /
  `InteractStation`).
- Weapons, armor, potions, food, tools.
- Recipes discovered through exploration, skill books, and the Crafting skill branch
  (`SkillType.Crafting`, e.g. "Steady Hands" — +3 Luck crafting quality).

### 5.5 Equipment

Equipment is split into **3 genres**, each mapped to a fixed set of gear slots (21 total).

#### Genres & Slots

| Genre | Slots | Qty |
|-------|-------|-----|
| **Armor** | Head, Body, Glove, Legging, Feet | 5 |
| **Weapon** | Left Hand, Right Hand | 2 |
| **Accessory** | 10 Fingers, Necklace, 2 Ear, Belt | 14 |

**Armor (5 slots):** The source of **physical damage reduction** (amplified by the **Defense** stat) and of **all elemental/magic resistance**. Per the equipment-only resistance rule (§3.4), no stat grants resistance — armor/gear does. Heavier armor weighs more (raising **EquipLoad**, gated by Endurance).

**Weapon (2 hand slots):** Every weapon is **one-hand capable**, so any two can be dual-wielded — including **two of the same weapon type** (one rig per hand = one owned copy per rig). Wielding is governed by the weapon's **Strength (Str) requirement** (by weight class: light / medium / heavy):

| Configuration | Requirement |
|---------------|-------------|
| **One-handed** (1 weapon) | Full Str requirement of that weapon |
| **Two-hand grip** (both slots) | **Reduced** Str requirement (~half) — lets low-Str builds use heavy weapons at the cost of no off-hand weapon/shield |
| **Dual-wield** (one per hand) | Roughly **2× the single-hand Str requirement** — high-Str builds can dual-wield greatswords, hammers, etc. |

Two-handing occupies both hand slots (no off-hand); dual-wielding occupies both with separate weapons (possibly two copies of the same weapon). Weapons carry their own **damage** (ranged uses `weapon.base`), **speed**, **range**, and a unique **Weapon Art** (§3.2 combat, costs FP). The **Knight** class raises equip-load carry; **Blacksmith** improves upgrades/repair/forging.

##### Same-Type Dual-Wield & Copy Accounting

Weapons are also **physical bag items** — stack-counted in the ToolManager inventory (§5.1) alongside tools — while the `WeaponInventory` **owned list** gates what can be dragged onto the hand slots. Equip/unequip is **copy-accurate** (never duplicates, never loses, an item):

- **Equip — `EquipOwnedWeapon` (CharacterInfoUI.cs):** dropping a weapon onto the hand that already holds it is a no-op. When the *other* hand holds the same id, a **spare copy in the bag** (`CountItem >= 1`) permits dual-wield and the spare is consumed (`RemoveItemAmount(weaponId, 1)`); with no spare the weapon is **moved** (the other hand's rig is destroyed first) so a single owned item is never duplicated onto the body. Any weapon displaced from the target slot returns to the bag first; if the bag is full the swap aborts ("Túi đồ đầy").
- **Unequip — `UnequipWeapon` (CharacterInfoUI.cs):** destroys one rig per copy released and refunds exactly that many copies back to the bag (`PutItem`/`AddItem`). An optional `sourceHand` clears only the dragged hand, so a second identical weapon on the other hand stays equipped; otherwise every hand holding the weapon is cleared.

**Accessory (14 slots):** 10 rings (one per finger), 1 necklace, 2 ear pieces, 1 belt. These grant **passive bonuses** (stat, status, luck, utility). The bulk of defensive **resistance/DR** comes from armor — accessories supplement it and carry build-defining passive mods.

#### Hand States the system tracks

- **Single** — one weapon, off-hand free (weapon, shield, or orb).
- **Dual** — one weapon per hand.
- **Two-hand grip** — both hands on a single heavy weapon (reduced Str need).

### 5.6 Night & Survival

- **6 PM → 6 AM**: demons rise and attack the player and structures (`hour >= 18 || hour < 6`).
- Regular demon ~50 HP, deals ~10 damage; **giant demons** have higher HP/damage.
- Manage **HP** and **Stamina**; **eat to recover** (food stamina/HP recovery via
  `ToolManager.FoodStaminaFor`).
- **Close doors at night** to block demons; demon-wave and giant-enemy events are in the
  RandomEvent roster (§5.13).

### 5.7 Karma (Phước Đức) & the Pagoda

- **Karma** fuels the **Rosary** (1 per shot); it **regenerates over time** (`KarmaManager.RegenKarma`,
  gated into days) and its **max grows** through meditation, building, and defeating enemies.
- Displayed as a **Karma bar** on the HUD (e.g. `Phước Đức: 5/10`); consumed by Rosary one-shot kills.
- **Pagoda**: the 4-tiered, curved-roof landmark east of the village beside the neighbor's house —
  pray, **meditate** (typing/meditation minigame), and watch the sunset. The **Monk** there is
  connected to the **exorcism quest** (Rosary kills only).

### 5.8 NPCs & Relationships

- **Jessica** (the neighbor girl): befriend via gifting, romance, and **marry** her — `WifeNPC`
  (marriage gated at **day 5**; wife lives in the mansion, dialog + per-day events).
- **Phú Ông / The Rich Man**: guards a secret behind the mansion — stake out at night and
  **report to the police** (story quest "Bí Mật Của Phú Ông").
- **The Monk** (pagoda meditation/exorcism), **The Librarian** (holds every blueprint),
  and village merchants: **Fishing Shop**, **Chef**, **Café**, **Buffalo Shop**.
- **Friendship system**: gift villagers with hotbar items via number keys; some NPCs dislike
  certain gifts.
- NPCs provide quests, shops, lore, companionship across the open world.

### 5.9 Livestock & Pets

- **7 livestock species** spawn naturally around the farm over time; the **Buffalo** is a live
  entity sold/purchased at the **Buffalo Shop**.
- **Pets** (dog, and the **goblin** — late-game) follow and aid combat; the goblin has its own
  **command menu** (follow/stay/home), **own HP**, and **own storage**.

### 5.10 Construction, Housing & Infrastructure

- **Build menu**: hold **Hammer + F** → blueprint list (from legacy `WorldBuilder`) with **cost**
  and **locked** state; LMB place, F cancel. Blueprint type selection is via the UI menu —
  the legacy **B/N** cycling key is **not implemented**.
- **Blueprints** are learned at the **library** for gold 🪙.
- Buildings: house, mansion, restaurant, café, library, night club, watchtower, walls (plus the pagoda).
- Player homes can be built/decorated; **chests** for storage (`ChestStorageManager`), with
  **crafting stations** and **farming plots** attached.
- Walls and watchtowers defend the farm; **storms/earthquakes/tornadoes** (events) can damage buildings.

### 5.11 Economy

- Gold (🪙) is the unit; earn by selling produce, fish, quest rewards, and restaurant/café income.
- **Vendors and shops** in towns (`VendorShopManager`, `BuffaloShopManager`) with buy/sell tabs
  and price multipliers.
- Player trading intended via dedicated server (`§4`).

### 5.12 Quests

- **Story chain** (day-gated, some require the previous quest) — driven by `QuestManager`:

| Quest | Requirement | Day | Reward |
|-------|-------------|-----|--------|
| Chào Hỏi Hàng Xóm | Greet Jessica | 1 | — |
| Bí Mật Của Phú Ông | Stakeout + police report | 3 | 500 |
| Mùa Thu Đầu Tiên | Harvest 50 wheat | 3 | 150 |
| Bảo Vệ Đất | Defeat 10 enemies | 5 | 300 |
| Bàn Tay Xanh | Harvest 150 wheat | 8 | 400 |
| Xây Dựng Đại Phú | Earn 50,000 gold | 10 | 750 |
| Thợ Săn Quái Vật | Defeat 30 enemies | 12 | 600 |
| Trận Đấu Cuối Cùng | Defeat 50 enemies | 15 | 1,500 |
| Tỷ Phú | Earn 200,000 gold | 18 | 3,000 |

- **Daily** repeatable quests, **timed** quests, and **exorcism** quests (only **Rosary** kills
  count, with a popup per kill while incomplete).

### 5.13 Random Events

- `RandomEventManager` keeps **21 live event** definitions: crops advance, gold on ground,
  wounds heal, seeds rain, pests/plague, drought, weeds, fireflies/rainbow, exhaustion,
  fish rain, enemies approach, thief, animals dance, giant enemy, 3 monster waves, meteors,
  village celebration/fireworks, ghosts, buried treasure, earthquake, lightning/tornado, and more.
- Removed from live rotation (see §6.4): market crash/boom, new trade routes, migrant family.

### 5.14 Endings & Ending Tree

- **8 cutscene endings** are implemented (`EndingHappy`, `EndingSad`, `EndingFated`,
  `EndingDemon`, `EndingJustice`, `EndingNTR`, `EndingBlackmail`, `EndingBossBad`) but are
  **gated off** at runtime (`CutsceneManager.RemoveEndings = true`); the shipped game plays as an
  ongoing open-world RPG.
- The **Ending Tree** panel (`UIManager.Endings.cs`) remains wired to unlock/review endings
  from the main menu — re-enable by flipping the gate.

### 5.15 Meta Systems

- **Fast travel**: road **signs** (`FastTravelSign`) open `FastTravelMenu` (scrollable list).
- **Save/Load**: multi-slot `SaveManager` (PlayerPrefs last-slot memory); **sleep on the bed** to save.
- **Settings**: mouse/touch sensitivity, invert Y, language (**Tiếng Việt / English**), PC / Mobile mode.
- **Game Stats** (`UIManager.HUD`): wheat harvested, enemies defeated, money earned, money stolen.
- **Gender selection** at start (cosmetic only); **tutorial book**; message banner; item tooltips.

### 5.16 Controls

- **WASD** move · **Space** jump · **Shift** sprint · **Mouse** look
- **LMB** use tool · **E** interact/open · **Q** drop item · **F** build menu (with Hammer)
- **1–0** hotbar — mobile touch support included.

---

## 6. Systems Removed / Disabled (Code-Confirmed)

> **Correction to earlier drafts:** the old-game systems below were previously described as fully
> removed. Verifying the current code shows most were **kept** (often behind a runtime flag) rather
> than deleted. Legacy code copied out of use lives in the **project-root `_Archived/`** folder
> (`WorldBuilder/`, `CutsceneManager/`, `Quests/`, `Enemies/`, `README.md`) — not under
> `Assets/Scripts/_Archived/` (that path is empty).

### 6.1 World Builder (Voxel Cube System) — **legacy generation disabled, content retained**

- `WorldBuilder.EnableLegacyGeneration` defaults to `false`, replacing the legacy finite voxel map
  with the chunk-based terrain system (§2).
- The WorldBuilder **content systems remain in use**: its blueprint list powers the build menu
  (§5.10), and its farming fields/pagoda are live.
- Legacy generation code is archived at `_Archived/WorldBuilder/` for reference/re-implementation.

### 6.2 Endings System — **implemented but gated**

- **8** cutscene endings exist (`EndingHappy`, `EndingSad`, `EndingFated`, `EndingDemon`,
  `EndingJustice`, `EndingNTR`, `EndingBlackmail`, `EndingBossBad`) with the **Ending Tree** UI
  wired in the main menu.
- They are **disabled at runtime**: `CutsceneManager.RemoveEndings = true` short-circuits every
  ending entry point to `EndingsRemoved`. The shipped game therefore plays as an ongoing
  open-world RPG; flip the gate (or delete the early-return) to re-enable endings.
- Cutscene helpers/road-driving partials remain active; reference copy at `_Archived/CutsceneManager/`.

### 6.3 Story Quests — **retained**

- The Vietnamese **story quest chain is NOT removed**. `QuestManager` is active and drives the
  **9-day-gated story tiers** (§5.12) plus daily/timed/exorcism quests; `RandomEventManager` is
  active with 21 live event types (§5.13).

### 6.4 Truly Removed Content

- **Market crash / boom** and **new trade route** random events (sell-price halve/double, buy-price
  discount) — removed from the live roster; only localization strings remain. The Economy §5.11
  therefore keeps fixed vendor pricing.
- **Migrant family** ("Người di cư") subsystem and event — stripped from code (no references remain).
- **B/N building-type cycling key** in the build menu — not implemented; blueprint selection is
  done through the UI menu only (§5.10).

---

## 7. World Design

### 7.1 Biomes

Generated from noise layers, each biome has unique terrain characteristics:

| Biome | Terrain | Enemies | Resources |
|-------|---------|---------|-----------|
| **Plains** | Flat, gentle hills | Slimes, Wolves | Crops, herbs |
| **Forest** | Dense, moderate height | Bandits, Treants | Wood, mushrooms |
| **Mountains** | Steep, high elevation | Golems, Drakes | Ore, gems |
| **Swamp** | Low, muddy | Undead, Slugs | Rare herbs, poisons |
| **Desert** | Sandy, dunes | Scorpions, Mummies | Cacti, ancient relics |
| **Tundra** | Snowy, icy | Yetis, Ice Wolves | Frost crystals |
| **Volcanic** | Molten, extreme heights | Fire elementals, Dragons | Obsidian, fire essence |
| **Deep** | Underground caves | Demons, Mimics | Dark crystals, loot |
| **Ocean** | Water terrain | Sea creatures | Pearls, coral |

### 7.2 Points of Interest

- Towns (NPCs, shops, crafting)
- Dungeons (combat, loot)
- Boss arenas
- Fast travel points
- Fishing spots
- Farming zones
- Player housing plots
- Hidden caves and secrets
- Skill book locations

### 7.3 Day/Night Cycle

- 24-minute real-time cycle (configurable).
- Enemies become stronger at night.
- Some areas only accessible at night.
- Sleep/rest at bonfires to skip to morning.

### 7.4 Weather System

- Clear, Rain, Storm, Snow, Fog.
- Weather affects combat (rain reduces fire damage, fog reduces visibility).
- Some enemies only spawn in certain weather.

---

## 8. UI/UX Design

### 8.1 HUD

- HP/FP/Stamina bars (bottom left)
- Compass/map (top)
- Skill bar (bottom center, 6-8 slots)
- Minimap with chunk boundaries (toggle)
- Multiplayer indicators (player names, health bars)
- Enemy health bars (above enemies during combat)

### 8.2 Menus

- Main Menu (New Game, Continue, Multiplayer, Settings)
- Pause Menu (Inventory, Skills, Map, Quests, Settings, Quit)
- Character Creation (race select + stat/passive preview)
- Race & Stat Sheet (current race, stats, skill XP, classes)
- Inventory Menu (equipment, items, materials, consumables)
- Map Menu (world map with biome overlay, POIs, player markers)
- Multiplayer Menu (server browser, friends, party)

### 8.3 Interaction Prompts

- Context-sensitive interaction UI (existing system, adapted)
- NPC dialogue system (simplified from CountryLife)

---

## 9. Technical Specifications

### 9.1 Engine

- Unity 2022 LTS or newer
- Universal Render Pipeline (URP) for performance
- Dedicated server framework (Netcode structure)

### 9.2 Target Performance

| Platform | Target FPS | Render Distance |
|----------|-----------|-----------------|
| PC (High) | 60 fps | 16-32 chunks |
| PC (Low) | 30 fps | 5-10 chunks |
| Mobile | 30 fps | 3-5 chunks |

### 9.3 Save System

- Chunks: individual .dat files per chunk (binary format)
- Player: JSON save file (stats, inventory, position, skills, world flags)
- Server: authoritative world state stored server-side

---

## 10. Monetization (Future Consideration)

- No pay-to-win.
- Cosmetic-only microtransactions (skins, emotes).
- Expansion packs (new biomes, classes, story content).
