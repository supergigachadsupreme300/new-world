# Game Design Document — "New World" (Working Title)

## 1. Game Overview

**Genre:** Open-World Action RPG (Elden Ring-inspired)
**Platform:** Unity (PC primary, Mobile secondary)
**Multiplayer:** Dedicated server with co-op/invasion/arena
**Core Loop:** Explore → Fight → Grow → Craft → Dominate

Seamless open-world with real-time action combat, classless progression via a **6-category skill-XP system**, an **11-stat** system, **15 unlockable classes**, and a **22-race system** (with passive-only racial kits), procedurally generated seed-based chunk terrain, and all existing CountryLife systems retained as optional side content. Combat is built on a **3-genre equipment** set (21 slots), an expandable **weapon architecture** (§3.6, Melee/Ranged/Magic), a **spell-casting pipeline** (§3.8) for magic, and **10 damage types** with **7 status effects** (§3.7).

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
- **Default radius:** 20 chunks (1,681 chunks loaded ≈ 600 m half-width).
- **Maximum radius:** 160 chunks (code clamp, `RenderDistanceController.MaxRadius`).
- At each frame, the system calculates which chunks are within radius of the player.
- Chunks entering radius: loaded from cache or generated.
- Chunks leaving radius: unloaded from memory (kept in cache on disk).
- **Boot (current build):** only the **spawn chunk** is generated synchronously so the player is usable
  immediately; the rest of the visible ring builds in a background pass over ~1.5 s. The game bootstrap
  defaults render radius to **20** with a hard clamp of **160** chunks, and the LOD cull distance
  auto-matches the current render radius so culling never fights the visible ring.

### 2.6 Chunk Persistence (File Caching)

- Deformations are saved **per terrain chunk** (`30×30` local tiles) as a single binary file:
  `worlds/{seed}/tc_{x}_{z}.dat` (magic `"NWTC"`).
- A file stores only **locally deformed tiles** (`ChunkTileMod`: local coords + 4 corner heights),
  never pristine terrain. On load (`TryLoadChunk`) deformed corners restore their saved heights
  before noise-filling pristine corners — so un-modified chunks stay fully deterministic from the
  seed and only edited areas consume disk/IO.
- **Corner sentinel is NaN, not zero (1bk):** `BuildOrLoadChunk` prefills the corner grid with
  `float.NaN`; a zero-filled grid treats `0f` as a valid saved height (`float.IsNaN(0f)` is false)
  and collapses every unstamped corner to height 0 — i.e. an un-edited chunk renders flat. NaN
  marks "no saved value", so only genuinely saved corners are restored and all others re-roll from
  the seed noise.
- **Force-rebuild API (1bk):** `WorldStreamer.ForceRebuildArenaLane()` drops the in-memory
  arena-lane chunks (`tc_-1_0/-1_1/-1_2`) and re-queues them through `UnloadChunk` +
  `EnqueueChunkIfNeeded` (the normal streaming path), so a stale flat mesh is re-streamed from
  noise + saves **without writing to any `tc_*.dat` file**. Auto-fired on New Game and bound to
  editor **F12**; `GameManager` holds the streamer reference (resolved by `AutoResolveReferences`,
  the streamer is created by `GameBootstrap`).
- Dirty tiles record at **mark-time** (no IO); each chunk's accumulated tiles flush **batched** into
  one file write (default synchronous, one write per chunk per cast; unload and shutdown also flush).
- Player modifications (terrain deformation) are delta-patched into the chunk file on flush.
- The save path is **captured once on the main thread** (`ChunkSaveManager.Warmup`, called by
  `WorldStreamer.Awake`): `Application.persistentDataPath` is main-thread-only in Unity 6, but chunk
  generation resolves the file path on background threads — they read the cached string only.

### 2.7 Testing Arena — Independent Floating Platform (dev tool)

- The QA test bench (Opt/NewWorldTestGround) lays a **self-contained floating platform** that is fully
  INDEPENDENT of the world's procedural terrain: a solid mesh slab + collider (with 4 corner posts so
  it reads as a structure) is built in `Awake`, raised **clear of the natural ground** at the arena
  coordinate. The world's rolling terrain is **never edited in any way** — no carve, no flatten, no
  chunk-save writes, no prop clearing/suppression. The platform's top sits above a **coarse sample of
  the world's own best natural height** (same 5-octave noise the streamer uses, read-only, plus 12 m
  clearance) so terrain and trees never poke through.
- **Legacy flatten saves purged (1bi):** `worlds/1337/tc_*.dat` full-chunk flatten files (900-mod tiles
  all keyed off the old hub's flat pad) were **deleted**, so the map streams back as the generator
  designed it — each tile with its natural rolling surface and dedicated noise sample, no more
  "one-surface test field". Sparse files (real Earth-spell/tool edits, e.g. freshly tilled lanes) are
  kept untouched.
- **Corner sentinel is NaN, not zero (1bk):** `WorldStreamer.BuildOrLoadChunk` prefills the corner grid
  with `float.NaN` so an unstamped corner regenerates from noise instead of reading `0f` as a valid
  saved height (which collapsed whole chunks flat at height 0). `WorldStreamer.ForceRebuildArenaLane()`
  unloads + re-queues the arena-lane chunks; it is auto-fired on New Game and via editor **F12**, and it
  never writes to `tc_*.dat` files.
- The whole bench sits flat on the platform's **single level top** (`PlatformTopY`): every lane —
  farming plots/tilled soil, livestock, enemies/dummies/boss, buildings, NPCs, weapon pedestals/racks,
  and the tool-pickup kit — keys its placement off that one height, so nothing hugs a slope and every
  prop stands edge-to-edge level.
- **No auto-teleport at boot (1bz):** the player starts on the world's boot chunk near `(0, terrain, -10)`
  and the platform is left in place for the player to walk to. `GetSpawnPoint` still returns the pad's
  top + 2 m, but `PlayerController.ResetPlayer` only re-homes the player there when they have **already
  reached the platform** (XZ within ~0.6 × platform and Y within 6 m of the top surface); otherwise it
  spawns on the boot chunk. `NewWorldTestGround.AutoTeleportPlayerOnStart` (default **off**) restores
  the old pull-onto-pad behaviour when enabled for dev sessions.
- **Boot order stays "ground first, then player"** (§2.7): the platform is built in `Awake` (before any
  lane), the benchmark lanes target `PlatformTopY`, and the only sync-ground is the boot chunk — so the
  player is never teleported over a void. The bench spawn is deferred (one lane group per frame); every
  lane runs in an isolated try/catch so one failing lane (e.g. one enemy spawn) logs instead of aborting
  the bench.
- The bench also lays the **tool/food discovery kit** along the platform's **east edge as real world
  pickups** (`Pickup_<id>` drops, `WorldBuilder.SpawnPickup`) instead of seeding the bag: the 10 tools
  (axe, pickaxe, hoe, hammer, scythe, watering_can, fertilizer, club, rosary, fishing_rod) + 5 food
  stacks (banh_mi, com_tam, nuoc_dau, mi_chinh, xap_phong ×5) — press E on a drop to collect it. The
  west edge hosts the weapon pedestals; a `PickupAmount` tag lets a single drop hand over a stack.

### 2.8 Physics Integrity Guard Rails

- **Problem:** one garbage/NaN vertex anywhere in the streamed terrain poisons the chunk `MeshCollider`
  (corrupted `bounds` → broken physics broadphase) and the CharacterController gets **depenetrated
  thousands of metres in a single step** ("take one step → teleported to -671, 5164").
- **Height sanitization:** `WorldStreamer` validates every height read from a `tc_*.dat` save via
  `IsSaneHeight` (finite **and** inside the ±200 m band — 5-octave noise max ≈ ±63.5 m + deformation
  headroom). Invalid/wild values are treated as **missing corners** and regenerate from noise; a mod
  tile's garbage slot falls back to the (already-sanitized) corner grid. As a final backstop,
  `ChunkMeshGenerator.SanitizeHeight` clamps every vertex Y in `BuildMeshData`,
  `BuildMergedMeshData` and `ChunkObject.PatchRegion`, so no code path can push a corrupted height
  into a MeshCollider.
- **Player CC fail-net:** `PlayerController` records `_lastSafePosition` every sane frame.
  `EnforcePhysicsSanity` (runs each `Update` before input) reverts the player if any coordinate is
  non-finite or a **single frame** moved them beyond a **speed-aware tolerance** of at least 150 m
  (`max(last-frame effective speed × 1.5, 150)` — 1cc) — fast-but-legit movement (buffed sprint) can
  never trip it even during a ~1 s frame hitch, while every real corrupted-collider launch
  (thousands of metres) still does. On a revert it logs the blast position, the local terrain height
  there, and sweeps nearby colliders for non-finite/oversized bounds to identify the culprit chunk.
- **Teleport routing:** every intentional teleport goes through `PlayerController.TeleportTo`
  (spawn/respawn, fast travel, sleep, load-game, test-platform entry), which stamps the destination as
  the new "last safe" position so the fail-net never false-positives on legit relocation.

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

### 3.2 Classes (17 Unlockable)

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
| 16 | **Taoist** | Wisdom + Intelligence >= 35 | Qi manipulation: enhanced spell cooldowns & stamina regen; demon damage bonus |
| 17 | **Monk** | Faith + Endurance >= 35 | Inner peace: meditation heals HP; reduced stagger, +defense while unarmed |

Classes are **exclusive — exactly ONE class is chosen at a time.** The player holds a single
`UnlockedClassIds` entry (the chosen class); switching via the Class tab *replaces* the choice
(no accumulating roster, no "unlock all eligible classes" pass). Any class with a highlighted
"(current)" state; the others are every-known-class options that swap the choice on confirm.
The rule *"player can only have 1 class, 1 race at a time"* is enforced in `ClassUnlocker`
(single-choice `UnlockedClassIds`, `SetActiveClass` replaces, `EvaluateAll` only guarantees a
valid baseline — Wanderer) and UI (`CharacterInfoUI.BuildClassOptions` marks the sole current).

**Switching (current build):** changing the active class is **not gated by unlock state** — any
known class may be chosen at any time and simply *becomes* the single unlocked class; the class
dialog no longer shows requirement summaries or locks. Race changes are likewise single-choice —
only Human or an actually-discovered race is selectable and a non-Human change costs a Ritual
Stone (no auto-unlock) — see §3.5.

#### 3.2.1 Class Skill Trees

Each class owns a small **radial skill tree** — one **hub** at the center plus **3 thematic paths** of
3 nodes each (path parent + 2 leaves/capstones), ~10 skills per class (~180 total across all classes).
Trees are built in code (`ClassSkillCatalog`) mirroring the main skill catalog; no asset files.

- **Auto-granted:** all 10 skills are granted the moment the class unlocks — there is no point
  economy and no per-node gating. The tree is informational (what the class grants), not a build budget.
- **Live only for the ACTIVE class:** passive modifiers are aggregated by `ClassPassiveManager` and
  swap in/out when the player switches classes — a Warrior passive stops applying the moment the
  active class changes to Mage (each has its own tree).
- **Castables** run through the shared `ClassSkillCaster` (per-class cooldown/cost keys), hotkey-bound
  via the same bindings UI as regular skills.
- **Modifier kinds (additive):** `MeleePowerMul`, `SpellPowerMul`, `CooldownMul`, `AttackSpeedMul`,
  `BackstabMul` (scales crits from behind), `HealPowerMul`, `BerserkScale` (dmg up as HP drops),
  `ParryWindowMul`, `ConsumablePotencyMul`, `DefenseMeleeMul`, `EquipLoadBonus`, `RangedHandlingMul`,
  `AuraStrength`, `BlockingMul` (÷ block stamina drain), `StaggerResistMul` (÷ knockback),
  `CraftSuccessMul`, `RepairMul`, `StaminaRegenMul`, `HpRegenPerSecond`.
- **Persistence:** the single chosen class id + the active class are saved/loaded
  (`SaveData.unlockedClassIds`, `activeClassId`); on restore any legacy multi-class roster
  collapses to the saved active class (Wanderer baseline if unknown), so old saves migrate
  gracefully and never re-gain the old "unlock everything" pass.
- **UI:** the Skills panel has a **General / Class / Race** sub-toggle. General shows the full 6-category tree;
  Class shows the active class's radial tree (hub + paths) with an auto-grant detail pane. (Talents live
  on the Character Info panel — see §3.9.)

#### Race ↔ Class Synergy

Each class's 3 paths are themed around different **racial archetypes** (§3.5), so the player's race
naturally synergizes with one path more than the others. This gives every race a "home" in multiple
classes, and every class appeals to 2-3 racial archetypes:

| Class | Path A (archetype) | Path B (archetype) | Path C (archetype) | Best Races |
|---|---|---|---|---|
| Wanderer | Survivor (all-around) | Crafter (utility) | Fighter (melee) | Human, Dwarf, Orc |
| Warrior | Brute (raw power) | Bulwark (defense/taunt) | Duelist (speed) | Orc/Golem, Fire Giant, Werewolf |
| Mage | Fire (damage) | Frost (control) | Arcane (cooldown) | Draconic, Ice Giant, Wraith/Elf |
| Rogue | Shadow (backstab) | Vampiric (lifesteal) | Dagger (speed) | Vampire, Serpent-kin, Harpy |
| Cleric | Light (healing) | Guardian (aura) | Restoration (regen) | Celestial/Angel, Angel, Dwarf |
| Berserker | Rage (berserk) | Frenzy (speed) | Might (raw) | Orc/Demonkin, Werewolf, Orc/Fire Giant |
| Necromancer | Undead (summons) | Blood (lifesteal) | Shadow (spells) | Wraith/Undead, Vampire, Elf |
| Samurai | Blade (parry) | Bushido (balance) | Precision (backstab) | Elf, Human, Skeleton |
| Alchemist | Potion (consumables) | Toxin (CC) | Forge (craft) | Gnome/Goblin, Serpent-kin, Dwarf |
| Knight | Iron (defense) | Wall (blocking) | Crusader (holy) | Golem/Fire Giant, Orc, Draconic/Angel |
| Archer | Marksman (ranged) | Wind (speed) | Trapper (CC) | Harpy/Elf, Elf, Goblin |
| Enchanter | Time (cooldown) | Frost (CC) | Charm (aura) | Elf, Ice Giant, Succubus |
| Brawler | Fist (power) | Grapple (CC) | Shout (defense) | Orc/Werewolf, Demonkin, Golem |
| Paladin | Oath (heal+def) | Guard (blocking) | Smite (holy) | Celestial/Angel, Golem, Draconic |
| Bard | Song (aura) | Dissonance (CC) | Drums (speed) | Celestial, Succubus, Orc/Elf |
| Taoist | Qi (stamina) | Symbol (spell) | Flow (CC) | Undead/Elf, Celestial, Elf/Fishmen |
| Monk | Body (defense) | Mind (heal) | Fist (melee) | Golem, Celestial, Orc |
| Blacksmith | Forge (craft) | Anvil (repair) | Ember (block) | Dwarf, Golem, Draconic |

### 3.3 Skill System (3-layer branching tree + use-based XP)

A **use-based skill progression** spans 7 categories with a **3-layer branching tree** (~2,077 skills total). No fixed class requirements — any player can advance any category based on how they play. Skills level by gaining XP in their category (with racial multipliers) and grant flat tier rewards at levels 5/10/15/20/25.

**Per-skill levels (current build):** on top of the category bar, every **learned skill also levels
itself** — each successful use grants skill-level XP (unaffected by prereqs, boosted by the race's
all-XP bonus and any matching talent, §3.9), following a linear threshold curve to a cap of level 100.
The skill detail pane shows "Lv N · XP x/y" for a learned active skill, and learned nodes in the tree
display their level. Magic's tree currently runs **9 L1 roots — the six classic schools plus Lightning,
Water, and Earth as their own schools** (Chain Lightning hangs under Lightning's root, not Fireball's;
Water soaks with Wet; Earth reshapes terrain instead of applying a status).

#### 3-Layer Branching Structure

Each category has a **3-layer tree**:

```
Layer 0 (base):     5-14 skills per category — foundational passives and core actives
                     ↓ each branches into 5
Layer 1 (branch):   25-70 skills per category — specialized variants (elemental, stat focus)
                     ↓ each branches into 5
Layer 2 (deep):     125-350 skills per category — mastery-level abilities
```

| Category | Layer 0 | Layer 1 | Layer 2 | Total |
|----------|---------|---------|---------|-------|
| Melee | 10 | 50 | 250 | 310 |
| Ranged | 10 | 50 | 250 | 310 |
| Magic | 16 | 80 | 400 | 496 |
| Stealth | 10 | 50 | 250 | 310 |
| Crafting | 10 | 50 | 250 | 310 |
| Fortitude | 10 | 50 | 250 | 310 |
| Shield | 1 | 5 | 25 | 31 |
| **TOTAL** | **67** | **335** | **1675** | **2077** |

**Prerequisites:** Each Layer 1 skill requires its parent Layer 0 skill. Each Layer 2 skill requires its parent Layer 1 skill. This creates clean branching paths — players must invest down a specific branch.

**Specialization:** Players earn ~1 skill point per category level-up (max ~25 points per category at level 25). With 310+ skills per category, players must **specialize** in 1-2 branches rather than filling the whole tree.

**Effect scaling:** Layer 1 skills are ~1.3× stronger than their parent. Layer 2 skills are ~1.7× stronger. Costs scale proportionally (1.35× per layer). **All tree passives are themed perks** (`Perk(PassivePerkType, value)`, aggregated by `PassivePerkManager` → `PlayerStats` per §3.3) — flat stat-buff passives are retired. There are **20 perk kinds** spanning offense (AttackPower / SpellDamage / CritChance / CritDamage / Backstab / AttackSpeed), resources (StaminaMax / FocusMax / StaminaRegen / FocusRegen / HealthRegen / MaxHealth), defense (DamageReduction / BlockEfficiency / StaggerResist / ParryWindow), and utility (MovementSpeed / CooldownReduction / HealPower / LootLuck). Each node chooses the kind that fits its name/branch identity and carries unique tooltip flavor (see the `SkillCatalog.*.cs` partials; 446 perk nodes across Melee / Ranged / Magic / Stealth / Fortitude / Crafting). Percent perks accumulate as **integer percents** (`Σ 5+3 → PassivePerkManager.Mul = 1.08`), flats accumulate raw via `Sum` — the class/race modifier managers instead take fractional amounts (`0.08` = +8%). Perks compound with the same class (`ClassPassiveManager`) and race (`RaceSkillPassiveManager`) multipliers where they overlap (§3.2.1, §3.2.2).

#### Skill Categories

```
   [MELEE]  [RANGED]  [MAGIC]  [SHIELD]
      \        |        /        /
      [SURVIVAL: STEALTH + CRAFTING + FORTITUDE]
```

**Shield category (current build):** the shield-tree (root `shield_bash`, 5 branches, 25 children) was
split out of the Melee tree so the shield reads as its own identity — **bash / guard / counter**, played
with the *equipped shield* (every Shield skill drives `ShieldWeaponBehavior`'s own bash animation + face
hitbox with knockback). Shield skills **require a shield in hand** — a learned Shield skill can't fire
(or spend its cost) while the player carries no shield. The wheel puts Shield under the center PHYSICAL
tree (5 wedges: Melee / Ranged / Stealth / Fortitude / Shield).

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

In the implementation the player's walk/sprint velocity is `(BaseMoveSpeed + Speed·k_mov) × TreeMul(MovementSpeedPercent)`, scaled by the controller's `MoveSpeed` via `MaxMoveSpeed/BaseMoveSpeed` (class/race passives layer in too), so `MoveSpeed = base + Speed·k_mov` matches the formula above. Tree perk percentages aggregate as **integer percents**: `PassivePerkManager.Mul(kind) = 1 + Σpercent/100` (e.g. the 40 movement perks sum to +158% → ×2.58), with flats accumulating raw via `Sum` (e.g. `HealthRegenPerSecond` 0.003 = +0.3%/s). A temporary **dev all-stats floor** (`DevMaxAllStats`, which forced Speed=100) was **removed in 1cc**, and a 100× multiplier-aggregation bug (`Mul = 1 + Σpercent` instead of `/100`) was **fixed in 1cd**, so velocity again reflects the character's real Speed stat and the intended perk package.

Example — a race with **Health +20%**: at base Health 30 → total 36 → MaxHP = 100 + 36×12 = **532** (vs unmodified 460). Because the bonus scales with the total stat, it represents ~16–18% more HP in the late game.

**Damage calculator wiring:** Strength/Dexterity/Wisdom/Luck feed the `DamageCalculator` context (AttackPower, LightAttackPower, ElementalPower, CriticalMultiplier). Ranged damage uses the **weapon's base damage** directly. Incoming damage is reduced by **equipment-based resistances** (armor physical DR, gear elemental/magic resist) — no stat contributes resistance. The 7 skill-XP categories (Melee, Ranged, Magic, Stealth, Crafting, Fortitude, **Shield**) are separate from the 11 stats.

### 3.5 Race System (22 Races)

Players pick a race at **character creation** (weighted-random roll that auto-commits, or manual pick) — that single race is the player's chosen race. Races are also **discoverable in the world** at altar/ritual sites, which mark them as selectable. The race system is **exclusive — exactly ONE race is active at a time** (§"player can only have 1 class, 1 race at a time"). A **mid-play change** is possible to Human (always free) or an actually-discovered race, and a non-Human change **costs a rare Ritual Stone** (the Change Race tab lists only Human + discovered races and never auto-unlocks; the test ground grants no full-roster unlock and no starter stones).

#### How Races Modify Stats

Racial stat modifiers are **percentages applied to the TOTAL stat on-the-fly**, so they **scale as the player levels**:

```
TotalStat = BaseStat × (1 + RacialPercent)
```

- Positive % (e.g. Str +20%) multiplies the whole stat, growing stronger with investment.
- Negative % (e.g. Dex -10%) is a permanent handicap the player must build around.
- Modifiers recalc immediately whenever the player levels or changes race.

#### Skill XP Categories (7)

Instead of a node-based skill tree, skills level via **use-based XP**. Each category has its own XP bar and flat tier rewards at levels 5/10/15/20/25. Races grant **XP multipliers** in categories that match their archetype, pushing builds in a natural direction.

| Category | Tracks | Example Uses |
|----------|--------|--------------|
| **Melee** | Melee proficiency | Sword/mace/axe damage, combos, stagger |
| **Ranged** | Ranged proficiency | Bow accuracy, crossbow, thrown weapons |
| **Magic** | Spell/miracle proficiency | Spell power, cast speed, FP efficiency |
| **Stealth** | Stealth/survival proficiency | Sneak damage, detection range, lockpicking |
| **Crafting** | Crafting/gathering proficiency | Potion potency, upgrade success, yield |
| **Fortitude** | Defensive proficiency | Shield stability, armor effectiveness (physical DR), perk effectiveness |
| **Shield** | Shield proficiency | Bash/slam/counter skills in the Shield tree; bash power, knockback (requires a shield equipped) |

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
  *(Current build: the change dialog calls `SetActiveRace(requireStone: false, unlockIfNeeded: true)` —
  changing race is **free and auto-unlocks the target race** for this character; the Ritual Stone cost
  applies to the world-discovery flow.)*
- On change: `PlayerStats` modifiers refresh, `RaceRig` swaps the model, `RacePassiveManager` re-applies passives. Current HP/FP/stamina preserved as % of their new max.

#### Race Visuals (Separate Rigs)

- Each race has its **own rig** (model + scale + offset + material tint).
- Rigs are **data-driven** (`RaceData` → `RaceRig`). Procedural placeholder bodies ship now; real 3D models drop in later without code changes.

#### Expandability

- `RaceData` is a ScriptableObject — adding a race = creating a new `.asset` (zero code changes).
- New races can ship post-launch via updates / content drops.

### 3.6 Weapon Architecture (Expandable)

Weapons are built on a **4-category base — Melee, Ranged, Magic, Shield** — structured so new categories/subtypes drop in without touching existing code. The core principle: separate **what a weapon is** (data) from **how it attacks** (delivery behavior) from **how damage resolves** (damage pipeline).

#### Layers

- **Layer 1 — `WeaponData` (ScriptableObject, data-only).** Shared fields: id, display name, weight (equip-load), Str requirement (weight class, §5.5), hand usage (single / dual / two-hand), base damage, speed, attack reach, scaling stat(s) + coefficients, `WeaponCategory`, `DamageType` (one of the 10 damage types, §3.7), and a Weapon Art reference. **Magic weapons** additionally carry magic mods — `MagicDamageMult`, `CastTimeMod`, `CooldownMod` (staff/wand/book scale spells). **Shield weapons** additionally carry guard mods — `BlockAbsorbPercent` (fraction of a blocked hit absorbed) and `BlockStaminaDrainMult` (multiplier on per-hit block stamina cost).
- **Layer 2 — `WeaponCategory` enum (expandable).** `Melee`, `Ranged`, `Magic`, `Shield`. Future values (Thrown, Summon, Hybrid, …) slot in as new enum entries + one behavior class each.
- **Layer 3 — Behavior modules via `IWeaponBehavior`.** A minimal contract: `BeginAttack(cmd)`, `ActiveFrame()`, `Cancel()`. One concrete module per category:
  - **`MeleeWeaponBehavior`** → existing `HitboxSystem` arc sweep.
  - **`RangedWeaponBehavior`** → projectile/raycast, **consumes ammo** (arrows/bolts from inventory), accuracy from Dexterity.
  - **`MagicWeaponBehavior`** → routes to the spell/skills pipeline; the equipped staff/wand/book's magic mods scale the spell (damage %, cast time, cooldown); costs FP; spell power from Wisdom.
  - **`ShieldWeaponBehavior`** → short hitbox bash on LMB (the equip's bash art, the §3.3 Shield category's bash identity) + enables the RMB guard; the shield's guard mods make blocking strictly stronger than the bare-hand guard.
  - `CombatController` talks **only** to `IWeaponBehavior` — it never knows melee vs ranged vs magic. **Adding a weapon kind = one new behavior class.**
- **Layer 4 — Damage pipeline & registry.** `DamageCalculator` (existing flexible `HitContext`) stays the single damage formula, extended to carry the weapon's `DamageType` (one of the 10 damage types, §3.7) for per-hit element/resist resolution. `WeaponDatabase` (ScriptableObject registry) holds all weapon assets and resolves each equipped weapon's category → behavior.

#### Per-Category Mechanics

| Category | Delivery | Damage Source | Key Stat | Resource |
|----------|----------|---------------|----------|----------|
| **Melee** | Hitbox arc | weapon.base + Str/Dex scaling | Str (heavy) / Dex (light) | Stamina |
| **Ranged** | Projectile / raycast | `weapon.base` (weapon ceiling) | Dex (accuracy) | **Ammo** (arrows/bolts) |
| **Magic** | Spell / skill pipeline | spell base × Wisdom, modulated by weapon magic-mods | Wisdom | FP |
| **Shield** | Short bash arc + guard | weapon.base + Str scaling | Str | **Stamina** (bash + block drain) · absorbs more / drains less than the bare-hand guard |

#### Notes

- Weapons carry a **single `DamageType`** — one of the **10 damage types** (§3.7); the damage pipeline resolves that element/type's resist/weakness.
- Dual-wield can pair **two of the same weapon type** — each hand holds one owned copy (one rig = one copy), subject to the §5.5 copy-accurate accounting rule: equipping the second hand consumes a spare bag copy, and without a spare the weapon *moves* instead of duplicating.
- Shields are the **off-hand defense** (§5.5): a shield weapon equips to either hand; while held it enables RMB blocking and raises the guard's damage absorb (up to 95% on tower shields, vs. the bare-hand guard's 80%) while cutting the per-hit stamina drain to as little as 60%. Holding a shield *without* a melee weapon still blocks; with a **ranged or magic** weapon in the other hand the loadout enters the §5.16 per-hand dual scheme — the ranged (or magic) hand keeps its own draw/charge button while the shield hand guards while held (crossed-button mapping when a ranged weapon is present).
- Magic weapons are **equipped gear that scales/alters spells** rather than delivering their own attacks — distinct from melee/ranged, which deliver their own.
- Hand/wielding integration (§5.5): the equipped hand slots hold `WeaponData`; the categories of equipped weapons determine which behaviors are active. Wielding states modulate Str requirement as specified.
- Ranged ammo ties into the Inventory/consumables system.

#### Visuals — attack + defense animation sets

Melee and shield weapons ship **two animation sets each**: an attack swing set and a defense guard
hold. Both live in `WeaponAnimator` (mounted on each weapon rig by `WeaponRigBuilder`) as
keyframed pose tracks driving the arm pivots — "the animation pack lives on the weapon".

- **Attack set** — the windup → strike → recover limb pose-tracks per weapon (slash / jab / bash
  chains, e.g. the sword's 4-swing set, the shield's bash set). The arms are owned during the swing
  (`PlayerAnimator.SuppressArms`) and restored to rest on recovery.
- **Drawn hold pose** — the rest pose of a drawn weapon (the rotation from
  `WeaponRigBuilder.DrawPoseFor`). One-hand blades **and the staff** share one angle in the fist:
  90° yaw so the length reads side-on to the camera plus a 30° off-vertical cant, so the staff grips
  exactly like the sword rather than hanging dead-vertical. Other magic focuses (book / wand / orb /
  lute) keep their own natural upright hold at a short grip-height below the hand.
- **Defense set (guard)** — holding RMB (block) eases the arms into a held guard pose
  (`PlayGuard` / `EndGuard`, ~0.18 s grab-in) that stays raised while blocking:
  - **Shields** raise the shield face up in front — the cover stance.
  - **One-hand blades** (sword/dagger) tuck a defensive guard before the chest.
  - **Two-handers** (greatsword/warhammer/greataxe/katana/lance) raise the weapon in a two-hand cover.
  - **Fists/gauntlets** hold a boxer guard. The off-hand mirrors automatically.
  - **Magic/ranged** never block (their RMB is charge/draw) and fall back to neutral.
  `CombatController.SetBlocking` raises/drops the guard on the state edge only, and `CanKeepBlocking`
  drops it on sheathe/stow so the guard pose never fights the stow idle; a stamina-break on a
  blocked hit drops it too.
- **Robustness** — `WeaponAnimator` is a **single-owner phase machine** (attack / charge / guard /
  ready-sway; each phase owns the arms exactly once and releases on `End`/`OnDisable`), and the arm
  rest is always the model's local identity. Rapid attack spam, charge-cancel, and guard→attack
  juggling can never leak arm ownership or bake an altered pose into the model; a `PlayerAnimator`
  watchdog force-releases a hung arm-owner claim as a backstop.

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
| **Chill** | Cold **build-gauge** (`ChillStatus`): each Ice hit adds 1 cold (2 if the target is **Wet** — water conducts); at **5 cold** it converts into a full **Frost** freeze. The gauge decays on its own; **Fire melts** it instantly (§3.7 fire-vs-ice). The **Ice** signature |
| **Frost** (freeze) | Heavy freeze slow (`ApplySlow` 0.5, ~3.5 s) — delivered by crossing 5 chill stacks, or directly by literal deep-freeze spells |
| **Burn** | Fire damage-over-time + light stagger buildup |
| **Stagger** | Poise break / crowd-control (stun — the **Lightning** signature) |
| **Wet** | Soaked — slight slow (`WetStatus`: ApplySlow 0.85) + **conducts**: Ice/Lightning deal +40% vs a wet target. Applied by **Water** spells. Fog douses fire: applying **Wet instantly puts out an active Burn** (SpellDoT douse). A wet foe also **cannot be ignited** while soaked — water-vs-fire always wins. |
| **Burn** | Fire damage-over-time + light stagger buildup. Gated: **won't catch on a wet target**, and a water hit douses it outright (§3.7). While active it **melts Chill/Frost instantly** (fire-vs-ice). |
| **Blind** | Black fog (`BlindStatus`) engulfs the victim, reducing its field of vision — the **Dark** signature |

Damage-over-time statuses (Bleed/Poison/Rot/Burn) are driven by `SpellDoT.cs` (refreshes on re-apply;
per-tick = spell power × 0.12 over 4 s); Chill/Frost route to `EnemyController.ApplySlow`,
Stagger to `EnemyController.ApplyStun`, Blind to `BlindStatus`, Wet to `WetStatus`.
Each magic school's **signature status is applied automatically to every magic attack** of that
element (an explicit per-skill `statusEffect:` overrides the default) — Fire→Burn, Ice→Chill
(deep-freeze spells use the heavier Frost), Lightning→Stagger (stun), Dark→Blind, Water→Wet,
Arcane→**no status** (pure force), Wind→Knockback, Holy→heals (§3.8), Earth→**no status — it
  reshapes terrain itself** (ring / spike / wall / pillar / crater ground deformation on the impact
  point, §3.8; the deep **Meteor** Earth skill strikes the ground and carves a permanent crater
  where it lands, and the deep **Earth Wall** (gated behind Landslide) rears a taller stone ridge
  along the cast).
  **(current build) every damaging Earth spell deforms the ground when it lands** — not just the
  tagged zones: Zone impacts dent (Crater) or rear (Ring/Spikes/Wall/Pillar) at the aim point
  (Boulder Crash, Crash and Tectonic carve craters; Aftershock rears a ring); Storm strikes
  (Rockfall) pit the ground under each boulder; Summons (the golem line) erupt a small raised rock
  field where the construct tears out of the earth; the root Stone Shard projectile carves its
  crater where the shard strikes — never at the caster's footing.
  **Beyond Earth, every magic projectile leaves a small impact dent where it strikes**
  (`SpellEffect.ResolveProjectileImpact` — fireball, frost bolt, arcane bolt, lightning, dark,
  wind blade, water bolt, etc. carve a small Crater under the impact point), so bolts visibly
  disturb the terrain; Earth's craters stay larger and depth-notable (the school's signature).

### 3.8 Spell-Casting Pipeline

Spells are how the **Magic** weapon category (staff / wand / book) deals damage and casts abilities. The pipeline connects the weapon architecture (§3.6), the skill system (§3.3), the stats (§3.4 Wisdom/Intelligence), and the damage types (§3.7).

#### SpellData (ScriptableObject)

A spell is a data asset carrying:

- id, display name, icon
- `DamageType` (one of the 10 damage types, §3.7) — or **none** for pure utility/heal spells
- base power
- **FP cost**, **cast time**, **cooldown**
- range, area/radius, delivery: projectile / instant / zone / **vortex** (persistent damage-zone that pulls, e.g. the Tornado wind spell) / **beam** / **summon** / **storm** (see §3.8.1 Delivery Behaviors)
- **duration** (zone/vortex lifetime; `> 0` makes the zone **persistent**, ticked by `SpellZone.cs`)
- **selfbuff** (Instant delivery grants a timed caster effect instead of damage/heal — e.g. **Wind Walk**: `PlayerController.BeginFlight(Duration)`, free vertical movement for the buff's seconds)
- **heals** (Holy/utility spells: instant/self-heal, or an ally-heal aura when on a zone; only `IHealable` targets — the player — are ever healed, enemies still take damage)
- **knockback** (impulse applied to enemies; the Wind school signature)
- **projectile shape** (`ProjectileShape`, §3.8.1): the *visual* built for a Projectile-delivery
  spell. When a spell leaves it `Auto`, `SpellCaster.AutoShapeFor` picks the school default; every
  bolt/lance/blade/spear-named spell sets it explicitly so projectiles read as their name.
- **terrain shape** (Earth school signature, §3.8): an optional `TerrainShape` reshapes the tiled
  heightmap before damage resolves — **Ring** rears a circular stone wall around the impact,
  **Spikes** erupts spires beneath it, **Wall** rears an elongated stone ridge along the cast
  direction (2.6 m high — tall enough to fully block the player's CharacterController), **Pillar**
  thrusts a tall flat-topped column up at the center, and **Crater** excavates a shallow solid-
  floored dish (ground deform via `TerrainDeformer` → `WorldStreamer.DeformAt`, which lifts/lowers
  the affected tile corners, rebuilds the merged chunk mesh+collider, and persists the edit as a
  terrain modification). Raised shapes (Ring/Spikes/Wall/Pillar) skip corners inside a small
  keep-out ring (~0.9 m) around the player's feet to prevent the ground from growing directly
  under the capsule and violently depenetrating it on the next physics step ("teleport" on repeat
  casts). **Crater is the Earth projectile signature**: a Crater-shaped projectile (the root
  Stone Shard) carves its crater where the shard **strikes** —
  `SpellEffect.ResolveProjectileImpact` down-probes the ground at impact and deforms it there, so
  a cast never dents the caster's own feet; the pit is permanent and depth-clamped (never a
  bottomless void). **Every non-Earth magic projectile (fire/ice/arcane/lightning/dark/wind/water)
  also leaves a small uniform impact dent** (a fixed ~1.4 m Crater where the bolt strikes) through
  the same path, so any bolt visibly disturbs the terrain — Earth retains the bigger, spell-scaled
  craters and the raised shapes (Ring/Spikes/Wall/Pillar) as its signature. All edits survive
  forever (§2.6 saves them per chunk). Earth spells use terrain shapes instead of a status effect.
  **(current build) every damaging Earth spell carries a terrain shape, regardless of delivery:**
  Zone impacts (Boulder Crash, Crash, Tectonic → Crater; Aftershock, the tremor ring family, Spire
  Field etc. → Ring/Spikes/Pillar/Wall; the deep Earth Wall, gated behind Landslide → Wall, rears a
  taller ridge along the cast) deform at the aim point via `ResolveZone`; Storm strikes
  (Rockfall → Crater) dent under each boulder via `SpellStorm.DeformGround`; Summons (the golem
  line → Spikes) erupt a small rock field where the construct rises via `ResolveSummon`; the
  Projectile root (Stone Shard) carves its crater at the impact point. The crater/dent floor stays
  depth-clamped everywhere, so no shape — zone, storm, summon, or projectile — can ever carve a void.
- cast animation reference
- optional status-effect application with a proc chance (e.g., applies Burn/Frost/Stagger; §3.7)

Persistent zones are handled by the unified **`SpellZone`** (tick damage scaled by a per-delivery
multiplier — Zone ×0.4, Vortex ×1.0 — optional pull, plus Holy ally-healing of `IHealable` inside
per tick); it replaces the former one-off `WindVortex`. The **Tornado** wind spell is Vortex's one
exception: `SpellCaster` routes it to **`SpellTornado`**, which rebuilds the old environmental
tornado model + function (`MapBuilder.BuildTornado` → `TornadoBehavior`: a tall drifting funnel of
rectangular debris blocks that churns around the axis, tows caught rigidbodies into the axis first,
then carries them on a low orbit — creatures implement `ITornadoCarried` so their own controller
yields while carried; scaled down to the spell radius) and layers the same damage ticks + enemy
pull on top; all other Vortex spells keep the `SpellZone` funnel.

#### §3.8.1 Delivery Behaviors

Beyond the core projectile / instant / zone / vortex, spells use three richer deliveries so spells in
a school read distinctly instead of feeling like copies:

- **Beam** — a channeled ray from the caster to the aim point. The cast **fires on the normal cast
  release** (LMB-up at the frozen charge level, §3.8); **holding LMB keeps the beam on while
  `ChannelDrainPerSecond` FP drains in real time** (via `TrySpendFocus` — a rejected spend ends the
  beam). Each `TickInterval` (default 0.5 s) it ticks damage (or healing for `heals` spells) to
  everything inside the beam capsule caster→aim. A short release-grace (~0.4 s) lets a sloppy release
  keep the ray a moment; on mobile / no-mouse the beam auto-sustains ~1.6 s. While channeling, LMB is
  consumed by the sustain (`IsChanneling` guard) so the beam can't be re-aimed or switched to melee.
  Examples: Searing Ray, Arc Storm, Beacon (heal), Hunger, Cold Stare, Storm Breath.
- **Summon** — ground-targeted (shows the AoE preview ring). **Damage** summons are persistent
  **turrets** that repeatedly fire bolts at the nearest enemy (`BoltPowerMultiplier` ×0.6, reusing the
  projectile flight); **`heals`** summons are standing **heal auras** mending `IHealable` allies inside
  (enemies still take damage). Examples: Frost Obelisk, Shadow Totem, Arcane Rune, Healing Shrine,
  Ember Effigy, Gust Totem.
- **Storm** — a persistent ground zone that **strikes repeatedly** while it lasts: `StrikesPerTick`
  (2) bolts per tick at `StrikePowerMultiplier` ×0.8 with randomized sub-second delays, element-styled
  visuals (e.g. crossed bolt bars on Lightning). Examples: Thunderstorm, Meteor Rain, Blizzard, Eclipse.

Fifth, **Projectile Shapes** — projectile visuals are split into named shapes rather than one element
color swap, so each spell looks like its name and not a recolor of the same ball:

| Shape | Rendered as |
|---|---|
| **Bolt** | Jagged segmented bolt along the flight axis (the same segment technique as the thunder-storm event's `SpawnJaggedBolt`), fast crackle pulse — used by every spell with "Bolt" in the name: Frost Bolt, Chain Lightning, Dark Bolt, Volt, Fork/Leap/Arc/Volt Bolt, Fury Bolt, Shadow/Doom Bolt, Void Rend, and the class-flavored Arcane Bolt. |
| **Sphere** | Classic orb (soft breathe) — school default / the Fireball. |
| **Shard** | Diamond crystal that drills forward (spin + breathe) — stone shards and generic frost chips (Stone Shard, Chill Touch, Chill Soul, Frost Bite). |
| **Lance** | Long straight pointed spike (shaft + tip), heavier than a bolt — Ice Lance, Frost Pierce, Glacial Impale. |
| **Spear** | Tapered spear: dark shaft + broad diamond head — Shadow Spear. |
| **Blade** | Flat cross-blade that spins in its own plane — Wind Blade, Razor Blade, Wind Scissor, Laceration. |
| **Splash** | Oblate droplet with a trailing splash of smaller drops — Water Bolt, Tidal Surge. |
| **Comet** | Bright core + fading streak tail (ember flicker) — Scorch, Burn, Comet. |
| **Missile** | Small clump of darts; **homing** — `SpellEffect.UpdateMissileTargeting` probes the **current trajectory** every frame and prioritizes the target on the flight path (the foe it is about to fly into), otherwise keeps chasing the locked target's last spot (or locks the nearest foe ahead if never locked), steering smoothly at 240°/s so the flight bends; no target = flies straight. Arcane Missiles. |
| **Dart** | Sleek thin bolt-line with a tip — physical shots (Archer Wind Shot, Taoist Talisman). |

`Auto` resolves per school: Fire→Sphere, Ice→Shard, Lightning→Bolt, Wind→Blade, Water→Splash,
Earth→Shard, Physical→Dart, everything else→Sphere. Builders live in `SpellCaster.BuildProjectileBody`
(primitives + `OrbFx` pulse modes: Plain/Ember/Shard/Bolt/Wisp/Swirl), colored per damage type and
layered with `AttachProjectileParticles` trails; turret summons render the projectile through the same
call (`SpellSummon` passes the turret spell's shape).

#### Casting Flow

1. Player equips a **Magic weapon** (staff/wand/book) in a hand slot.
2. The weapon's magic mods — `MagicDamageMult`, `CastTimeMod`, `CooldownMod` — modulate the spell before resolution.
3. `MagicWeaponBehavior.BeginAttack` routes the cast to `SpellCaster`.
4. `SpellCaster` validates **FP** (`MaxFP` from Intelligence) and **cooldown**; if valid, begins the **cast time**.
5. On cast completion, a `SpellEffect` spawns (projectile / instant / zone) or a persistent **zone/vortex** is summoned (`SpellZone` — e.g. Tornado/Blizzard, or a holy healing aura).
6. `DamageCalculator` resolves the spell with its `DamageType` against the target's equipment resistance; **Wisdom** scales spell power (`MagicAtkPower`), and `CooldownMult` from Intelligence shortens reuse.

#### Healing

**Holy** spells (and a few utility spells) instead carry `heals`. An **Instant** heal is applied to the
caster via `SpellCaster.ResolveDirect`; a **Zone** heal both damages enemies in radius and mends any
`IHealable` ally inside (the player), and a persistent zone heals on each tick. Healing scales with the
same Wisdom-derived spell power; only `IHealable` targets are ever healed — enemies are never healed.

#### Charging & Casting Circle

- Arming a spell from the **Alt magic grid** (§5.16) then hold **LMB** to start the aim pose (hands raise); **RMB**
  builds a **charge level** (0–100%, ~2 s, no auto-fire). **Releasing LMB** fires at the frozen level.
  Charge scales the cast: FP cost (up to ×1.6), damage (up to ×2.0), and AoE radius (up to ×1.8), so
  a deeper charge is always a gamble for more FP — never a dud.
- While aiming/charging, the held **magic weapon shows a "casting circle" halo**: a translucent disc
  beneath the tip plus an outer ring and a spinning inner rune ring wrapping the weapon, ramping its
  radius, brightness, and spin speed with charge level and tinted by the **armed spell's element**.
  Releasing the cast pops a one-shot expanding ring at the weapon. (`CastingCircle.cs`, driven by
  `PlayerController`; split aim → charge → release is used by both magic and ranged.) Unarmed casts
  still play a plain hand glow instead of the halo.
- Projectile spells launch **from the casting circle's center**: the spawn point sits on the aim line
  at the rig/hand origin (a small forward muzzle offset only, no vertical lift), so the flight
  trajectory passes through the circle's heart. The pre-cast **path preview** mirrors the exact launch
  (`SpellCaster.FireProjectile` ↔ `PlayerController.UpdatePathPreview` share the same origin math).
- Zone/vortex spells additionally show a **ground AoE preview** ring that also grows with charge.
- Projectile deliveries (magic **projectile** spells, and ranged draws — regular and per-hand dual) show a
  **flight-path cone** while charging: a stack of translucent rings from the hand along the aim line that
  **narrows as the charge builds**, collapsing to a thin centre ray of the exact predicted trajectory at
  full charge, and clipped at the first solid hit. Magic previews are tinted by the spell's element;
  ranged previews are tinted by shot type and spread outward with low accuracy. (`ProjectilePathPreview.cs`,
  driven by `PlayerController`; hidden on cancel/release/weapon switch.) Ranged weapons with no projectile
  prefab fire a runtime-generated arrow instead of a hit-scan tracer.

#### Spell Sources

- **Equipped weapon** — a staff/wand/book in a hand slot (its magic-mods apply).
- **Active skills** (§3.3) — spells granted via skills/skill books can also be cast from the skill bar; they route through the same `SpellCaster` so the pipeline is shared.

#### Expandability

Adding a spell = creating a new `SpellData` asset (zero code changes), consistent with the rest of the data-driven systems.

### 3.9 Talent System (Player-Level Perks)

A small **rankable perk layer** sitting on top of character leveling — separate from the skill trees and
the stat points you spend per level-up. It rewards long-term play and lets every build tune how it
progression-by-progression grows.

- **Earning:** ranks are granted **freely** (no talent-point currency) — click *Rank Up* on any talent,
  capped at max rank. A brand-new character is granted **one random talent at rank 1** at game creation
  so the system is immediately visible.
- **Talents (26 total, all max rank 3, effects additive per rank):**
  - *Fast Learner* — **+5 % character XP** per rank.
  - Seven **skill-type** talents (Melee/Ranged/Magic/Stealth/Crafting/Fortitude/Shield, e.g. "Arcane Study",
    "*Shield Work*"), **+6 % XP per rank** for that skill type — boosts both the per-skill levels (§3.3)
    and the category bar.
  - Eleven **stat** talents (one per core stat, e.g. "Vitality" = Health), **+1 flat stat point per rank**
    layered onto the stat total.
  - *Critical Eye* — **+2 % critical-hit chance** per rank.
  - *Executioner* — **+15 % crit damage** per rank.
  - *Ambush* — **+10 % backstab damage** per rank.
  - *Bulwark* — **+10 % block stamina efficiency** per rank.
  - *Grounded* — **+10 % stagger resistance** per rank.
  - *Second Wind* — **+10 % stamina regeneration** per rank.
  - *Arcane Spring* — **+10 % focus regeneration** per rank.
- **Effect reads are live:** XP bonuses are applied as a +% on every XP grant; stat talents add flat
  points inside `GetTotal` (base × race/race-skill % **+** the stat talents' flat add + temp buffs). The
  combat/regen talents fold **additively** into the same `PlayerStats` getters as the skill-tree perks
  (crit chance, crit/backstab/block/stagger multipliers, stamina/focus regen), so HitboxSystem,
  CombatController, PlayerController and SpellCaster pick them up with no extra plumbing. Because
  bonuses are computed from owned ranks on every read, saving/loading can never double-apply them.
- **Persistence & UI:** owned ranks + the first-grant flag are saved (`talentStateJson`); the
  **Character Info panel** lists all talents below the stat/level block (one vertical scroll), each with
  a *Rank Up* button enabled while the talent is below max rank. There is no talent-point counter.

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
- **Dual** — one weapon per hand. Controls split per-hand on the mouse (§5.16): **LMB and RMB each
  drive one hand** (crossed sides whenever a ranged weapon is among the two). Melee swings on press,
  shield guards while held, magic fires uncharged, ranged holds-to-charge/release-to-fire.
  **Blocking is only possible through a shield hand while dual-wielding.**
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
- **The three holy places** — **pagoda** (Buddhism / monk), **church** (Catholic / priest), and
  **taoist shrine** (Taoism / taoist priest) — are the worship sites for the Faith system
  (`ReligionManager`): worshipping joins/switches the player's faith, grants devotion (+1 per worship
  day, up to a cap) and unlocks that faith's blessing perks. Each site is built from its
  `WorldBuilder.Build*` structure + matching worship NPC. **QA/Test Ground**: the independent test
  platform (`NewWorldTestGround.EnableReligion`) places all three structures and worship NPCs so the
  Faith tab is testable without the legacy village.

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
- **Fighting mode** (weapon drawn): **LMB** attack / begin a magic aim · **RMB** block (melee **or** shield) or
  charge/draw (magic/ranged) · release **LMB** fires at the frozen charge level.
  **Beam spells** keep firing but **holding LMB extends the beam** and drains FP per second — releasing
  ends the channel early (see §3.8.1).
  **Dual-wield (both hands hold real weapons):** the buttons split per hand instead —
  **LMB → one hand, RMB → the other** (`PlayerController.HandleDualModeCombat`, §5.4):
  - **Same-side** by default: **LMB = left-hand weapon**, **RMB = right-hand weapon**.
  - **Crossed** (LMB → right hand, RMB → left hand) whenever a **ranged** weapon is one of the two,
    so the bow/throwing hammer keeps its hold-to-charge/release-to-fire draw on its own button.
  - Hand action: **Melee** swings on press · **Shield** guards while held (release drops the guard) ·
    **Magic** (mixed dual) loses charge and taps fire the armed spell uncharged · **Ranged** holds to
    charge and releases to fire.
  - **Trade-off:** dual = no block except via a shield hand (2 swords = 2 independent attack buttons).
    Pressing the attack button while a guard is raised drops the guard and swings. **Both-magic** and
    any 0/1-weapon loadout (incl. barehanded fists) keep the standard single-button scheme above.
- **Alt** (fighting mode, magic weapon held) — opens the dev/test **magic grid** pinned to the
  **right edge** of the screen (the old centre-screen ring/wheel is retired, note 1av). The grid is a
  tall, scrollable, clickable list of **every castable magic-category skill in the game** (base +
  branch schools — not just those the current profile has learned; no physical melee/ranged/stealth
  castables), **grouped by school** (`Skill.DamageKind`, §3.7). Clicking a row **top-ups focus,
  test-grants the skill if unlearned, arms it in the bottom-left "Armed: X" chip, and fast-casts it
  at the current aim** — so any spell can be tried without spending skill points (cooldowns still
  apply). The armed-chip + charge/release flow above is unchanged; the grid only borrows the armed
  cast backend. Toggle closed with **Alt** or **Esc**; scroll with the mouse wheel or by dragging.

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
- **Skills menu** — one **giant radial skill tree** (hub + branching layers) per SkillCatalog category,
  built in code (no asset files), grouped into colored sectors (Melee / Ranged / Magic / Stealth /
  Crafting / Fortitude / Shield). On the standard PHYSICAL wheel each category fans out inside its
  own wedge: **Shield takes a small slice (~30°)**, the other four (Melee / Ranged / Stealth /
  Fortitude) share the remaining arc equally (~82.5° each), and ring positions re-spread across each
  wedge so every category's branches fill their sector. Pannable + zoomable. Nodes show state
  (selected / learned / available / locked); **connection links are black**, and a clicked node's
  direct parent→child links **light up white** so grouping is readable while idle. Class & Race tabs
  show each class/race's compact radial tree.
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

- **Unity 6** (6000.x, 2026) — the project was migrated from Unity 2022; Unity 6 changes already
  absorbed: `Object.GetEntityId()` replaces the now-obsolete `GetInstanceID()`, and
  `Physics.OverlapBoxNonAlloc` takes the results buffer before the orientation.
- Universal Render Pipeline (URP) for performance
- Dedicated server framework (Netcode structure)

### 9.2 Target Performance

| Platform | Target FPS | Render Distance |
|----------|-----------|-----------------|
| PC (High) | 60 fps | 16-32 chunks |
| PC (Low) | 30 fps | 5-10 chunks |
| Mobile | 30 fps | 3-5 chunks |

### 9.3 Save System

- Chunks: one binary `.dat` per terrain chunk (`worlds/{seed}/tc_{x}_{z}.dat`), storing only
  locally-deformed tiles (§2.6)
- Player: JSON save file (stats, inventory, position, skills, world flags)
- Server: authoritative world state stored server-side

---

## 10. Monetization (Future Consideration)

- No pay-to-win.
- Cosmetic-only microtransactions (skins, emotes).
- Expansion packs (new biomes, classes, story content).
