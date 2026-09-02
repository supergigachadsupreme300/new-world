# Game Design Document — "New World" (Working Title)

## 1. Game Overview

**Genre:** Open-World Action RPG (Elden Ring-inspired)
**Platform:** Unity (PC primary, Mobile secondary)
**Multiplayer:** Dedicated server with co-op/invasion/arena
**Core Loop:** Explore → Fight → Grow → Craft → Dominate

Seamless open-world with real-time action combat, classless progression via a massive unlockable skill tree, procedurally generated seed-based chunk terrain, and all existing CountryLife systems retained as optional side content.

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
               - (Defense x Defense Multiplier)
               x Elemental Modifier
               x Critical Modifier (if applicable)
```

#### Stamina System

- Stamina regenerates over time (pauses briefly after actions).
- Each action costs stamina.
- Stamina management is the core skill expression.

### 3.2 Classes (10 Unlockable)

The game uses a **classless unlock system**. Players start as a **Wanderer** (base class) and unlock classes by meeting stat thresholds or finding class trainers/items in the world.

#### Starting Base

- **Wanderer:** Balanced starting stats, no special abilities. Can go anywhere.

#### Unlockable Classes

| # | Class | Unlock Requirement | Unique Mechanic |
|---|-------|-------------------|-----------------|
| 1 | **Warrior** | Str >= 20 | Weapon Arts enhanced, stance breaking |
| 2 | **Mage** | Int >= 20 | Spell casting, magic damage |
| 3 | **Rogue** | Dex >= 20 | Backstab bonus, stealth attacks |
| 4 | **Cleric** | Fth >= 20 | Healing miracles, buffs |
| 5 | **Berserker** | Str + End >= 35 | Damage increases as HP drops |
| 6 | **Necromancer** | Int + Fth >= 35 | Summon undead allies |
| 7 | **Samurai** | Dex + End >= 35 | Perfect parry window extended |
| 8 | **Alchemist** | Any 2 stats >= 18 | Enhanced consumable effects |
| 9 | **Blood Knight** | Str + Dex >= 35 | Lifesteal on hits |
| 10 | **Void Walker** | Int + Mind >= 35 | Teleport dodge, void damage |

Classes are **not exclusive** — if stats allow, a player can unlock multiple classes and mix abilities.

### 3.3 Skill Tree System

A **giant interconnected skill tree** spanning multiple paths. No fixed class requirements — any player can go any direction based on stats and choices.

#### Tree Structure

```
                         [CORE]
                    /    / | | \    \
                  [STR] [DEX] [INT] [FTH] [ARC]
                  /|\    /|\   /|\   /|\   /|\
               Passive  Active  Weapon Arts  Ultimates
                  \  |  /  \ |  /   \  |  /
                [HYBRID PATHS — multi-stat thresholds]
                         |
                [ULTIMATE ABILITIES — endgame]
```

#### Skill Types

| Type | Description | Examples |
|------|-------------|---------|
| **Passive** | Permanent stat boost or effect | +10% stamina regen, +5% crit |
| **Active** | Equippable combat ability | Fireball, Heal, Backstab |
| **Weapon Art** | Weapon-specific unique skill | Whirlwind, Shield Bash, Arrow Rain |
| **Ultimate** | Powerful endgame ability | Meteor, Time Slow, Blood Rite |

#### Skill Book Expansion

- Skills are initially locked behind nodes in the tree.
- **Skill Books** found in the world or bought from merchants can:
  - Unlock new branches of the tree
  - Add new skill nodes
  - Grant bonus skill points
  - Reveal hidden ultimate paths
- The tree is **expandable** — new skill books can add entirely new branches post-launch.

### 3.4 Stats (8 Core)

| Stat | Effect |
|------|--------|
| **Vigor** | Max HP, HP regen, resistance to status effects |
| **Mind** | Max FP (mana), skill cooldown reduction |
| **Endurance** | Max stamina, equip load (heavier armor/weapons) |
| **Strength** | Melee damage, heavy weapon scaling, stagger power |
| **Dexterity** | Attack speed, ranged accuracy, crit chance, light weapon scaling |
| **Intelligence** | Magic damage, spell power, magic resistance |
| **Faith** | Miracle/healing power, elemental resistance, buff duration |
| **Arcane** | Discovery (loot chance), status effect buildup, luck |

**Leveling:** Earn XP from combat, quests, exploration. Spend points on stats at bonfires/rest points.

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

All existing CountryLife systems are retained as optional side content within the open world.

### 5.1 Farming

- Farming plots can be claimed at designated fertile areas.
- Plant, water, harvest cycle.
- Crops sold at vendors or used in cooking/consumables.

### 5.2 Fishing

- Fishing spots marked on world map.
- Minigame with rod/bait selection.
- Fish used for food buffs, sold, or collected.

### 5.3 Crafting

- Crafting stations placed in player homes or found in towns.
- Weapons, armor, potions, food, tools.
- Recipes discovered through exploration and skill books.

### 5.4 NPC Relationships

- NPCs scattered throughout the world.
- Friendship/relationship system (simplified from CountryLife).
- NPCs provide quests, shops, lore, companionship.

### 5.5 Livestock & Pets

- Animal husbandry at player homesteads.
- Pets with combat companionship.
- Goblin helper for automation (late-game unlock).

### 5.6 Economy

- Vendors and shops in towns.
- Player trading (via dedicated server).
- Currency earned from combat, quests, farming, fishing.

### 5.7 Housing

- Player homes that can be built/decorated.
- Chests for storage.
- Crafting stations and farming plots attached.

---

## 6. Systems Removed (Code Archived)

### 6.1 World Builder (Voxel Cube System)

- The `WorldBuilder` and all partial classes are **removed from active use**.
- Code is **archived** in `Scripts/_Archived/WorldBuilder/` for future reference/re-implementation.
- Replaced by the chunk-based terrain system.

### 6.2 Endings System

- All 7 cutscene endings (Happy, Sad, Fated, Demon, Justice, NTR, Blackmail) are **removed**.
- The `CutsceneManager` and ending partial classes are **archived** in `Scripts/_Archived/CutsceneManager/`.
- The game now has **no endings** — it is an ongoing open-world RPG.

### 6.3 Story Quests

- The Vietnamese story quest chain is **removed**.
- `QuestManager` and `RandomEventManager` archived in `Scripts/_Archived/`.
- Replaced by open-world quests, side quests, and exploration.

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
- Skill Tree Menu (full-screen interactive tree)
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
