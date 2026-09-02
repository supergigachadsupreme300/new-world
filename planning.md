# Planning Document — Implementation Roadmap

## Phase 0: Cleanup & Foundation (Week 1-2)

### Task 0.1: Archive Existing Systems

- [ ] Create `Scripts/_Archived/` directory
- [ ] Move `WorldBuilder/` (all 11 partials) to `_Archived/WorldBuilder/`
- [ ] Move `CutsceneManager/` (all 8 ending partials) to `_Archived/CutsceneManager/`
- [ ] Move `QuestManager/` to `_Archived/QuestManager/`
- [ ] Move `RandomEventManager/` to `_Archived/RandomEventManager/`
- [ ] Add README.md in `_Archived/` explaining these are preserved for future reference
- [ ] Remove references to archived systems from `GameManager`, `GameBootstrap`, `UIManager`

### Task 0.2: Project Restructure

Create new directory structure:

```
Scripts/
├── _Archived/           # Preserved old code
│   ├── WorldBuilder/    # Voxel cube world system (11 partials)
│   ├── CutsceneManager/ # 7 endings + cutscene system
│   ├── QuestManager/    # Vietnamese story quests
│   └── RandomEventManager/ # Tornado, chicken visits, etc.
├── Core/                # GameManager, Bootstrap, SaveManager, Settings
├── World/
│   ├── Chunks/          # Chunk generation, loading, saving
│   ├── Terrain/         # Noise, heightmap, mesh generation
│   ├── Biomes/          # Biome definitions, spawning rules
│   └── Streaming/       # Chunk loader, render distance, caching
├── Player/
│   ├── Controller/      # Movement, camera, input
│   ├── Combat/          # Attacks, dodge, block, parry
│   ├── Stats/           # Stat system, leveling, class unlocks
│   └── Inventory/       # Equipment, items, consumables
├── Combat/
│   ├── AI/              # Enemy AI, bosses
│   ├── Weapons/         # Weapon types, damage calc
│   ├── Skills/          # Skill tree, skill books, abilities
│   └── Effects/         # VFX, status effects, particles
├── Multiplayer/
│   ├── Server/          # Dedicated server logic
│   ├── Client/          # Client networking, prediction
│   └── Sync/            # State sync, anti-cheat
├── SideContent/         # Farming, fishing, crafting, NPCs, housing
├── UI/                  # All UI scripts (adapted from existing)
├── Audio/               # Sound system
└── Utils/               # Helpers, singletons, extensions
```

- [ ] Create all directories
- [ ] Move existing files into new structure
- [ ] Update all `using` statements across codebase
- [ ] Verify compilation after restructure

---

## Phase 1: Chunk Terrain System (Week 2-5)

### Task 1.1: Core Chunk Data Structure

- [ ] `ChunkData.cs` — Scriptable/serializable struct holding 5 vertex heights, chunk coords, metadata
- [ ] `ChunkCoord.cs` — Immutable struct for (x, z) pair with equality/hash overrides
- [ ] `ChunkKey.cs` — String key utility for file naming: `chunk_{x}_{z}`

### Task 1.2: Terrain Noise Generation

- [ ] `TerrainNoiseGenerator.cs` — Perlin noise with 5 configurable octave layers
- [ ] `NoiseLayerConfig.cs` — ScriptableObject defining frequency, amplitude per layer
- [ ] Seed-based deterministic generation (`seed + layerIndex * 7919` offset)
- [ ] `GetHeight(worldX, worldZ)` returns float height from combined noise layers

### Task 1.3: Chunk Mesh Generator

- [ ] `ChunkMeshGenerator.cs` — Creates mesh from ChunkData (5 vertices → 4 triangles)
- [ ] Triangle connectivity: shared edge vertices between chunks
- [ ] Random angle pivot for center vertex offset
- [ ] Normal calculation for lighting
- [ ] UV mapping for terrain textures

### Task 1.4: Chunk Loader & Render Distance

- [ ] `ChunkLoader.cs` — Tracks player position, calculates which chunks should be loaded
- [ ] `RenderDistanceController.cs` — Player setting for chunk radius (default 5, max 32)
- [ ] Priority queue: load closest chunks first
- [ ] Unload chunks that fall outside radius
- [ ] Object pooling for chunk GameObjects

### Task 1.5: Chunk Persistence

- [ ] `ChunkSaveManager.cs` — Save/load individual chunks to `worlds/{seed}/chunk_{x}_{z}.dat`
- [ ] Binary serialization format (5 floats + metadata + modifications)
- [ ] Player modification delta system (terrain deformation, placed objects)
- [ ] Fast-load pipeline: check disk → load if exists → generate if not

### Task 1.6: Neighbor-Dependent Generation

- [ ] When generating chunk (x,z), query loaded neighbor data for edge heights
- [ ] Edge vertex positions computed from world coordinates (deterministic)
- [ ] Center vertex influenced by all 4 corners + noise offset
- [ ] Validation: verify no gaps on chunk boundaries (debug tool)

---

## Phase 2: Player Controller & Camera (Week 3-5)

### Task 2.1: Movement System

- [ ] `PlayerController.cs` — WASD/Joystick movement, gravity, jumping
- [ ] Sprint, crouch, climb (on slopes < threshold)
- [ ] Ground detection for chunk terrain (raycast to mesh)
- [ ] Swimming in water chunks

### Task 2.2: Camera System

- [ ] `CameraController.cs` — Third-person orbit camera (Elden Ring style)
- [ ] Lock-on targeting system
- [ ] Camera collision with terrain
- [ ] Smooth follow with configurable distances

---

## Phase 3: Combat System (Week 4-7)

### Task 3.1: Core Combat

- [ ] `CombatController.cs` — Input handling for light/heavy/dodge/block/parry
- [ ] `StaminaSystem.cs` — Stamina management, regen, drain per action
- [ ] `DamageCalculator.cs` — Damage formula implementation
- [ ] `HitboxSystem.cs` — Weapon hitbox generation, collision detection

### Task 3.2: Weapon System

- [ ] `WeaponBase.cs` — Base weapon class (damage, speed, range, scaling)
- [ ] Weapon types: Sword, Axe, Spear, Bow, Staff, Dagger, Shield, Claw, Katana, Hammer
- [ ] `WeaponArt.cs` — Per-weapon unique ability
- [ ] Weapon upgrades, infusion system

### Task 3.3: Animation & Feedback

- [ ] Combat animation states (idle, attack chain, dodge, block, hit, death)
- [ ] Hit pause / hit stop (brief freeze on impact for game feel)
- [ ] Screen shake, particle effects, sound triggers
- [ ] Enemy ragdoll on death

---

## Phase 4: Skill Tree & Stats (Week 5-8)

### Task 4.1: Stat System

- [ ] `PlayerStats.cs` — 8 core stats (Vigor through Arcane)
- [ ] Stat effects (HP, FP, stamina, damage scaling, etc.)
- [ ] Level-up system with XP from combat/quests/exploration
- [ ] Bonfire/rest point system for leveling

### Task 4.2: Skill Tree

- [ ] `SkillTree.cs` — Data structure for the full tree (nodes, connections, requirements)
- [ ] `SkillNode.cs` — Individual node (type, stat requirements, effects, prerequisites)
- [ ] `SkillTreeUI.cs` — Full-screen interactive tree visualization
- [ ] `SkillManager.cs` — Equipping active skills, tracking passives
- [ ] Skill Books: unlock new branches, add nodes, grant points

### Task 4.3: Class Unlock System

- [ ] `ClassUnlocker.cs` — Check stat thresholds → unlock class → grant class ability
- [ ] Class data (10 classes with requirements and unique mechanics)
- [ ] UI notification on class unlock

---

## Phase 5: Enemy AI & World Content (Week 6-10)

### Task 5.1: Enemy AI

- [ ] `EnemyController.cs` — FSM: Patrol, Alert, Chase, Attack, Flee, Dead
- [ ] `EnemySpawner.cs` — Biome-based spawn rules, density, day/night variants
- [ ] `BossController.cs` — Multi-phase boss AI, attack patterns, health bars
- [ ] Enemy types per biome (9 biomes x 3-5 enemy types each)

### Task 5.2: Loot & Drops

- [ ] `LootTable.cs` — Weighted drop tables per enemy
- [ ] `ItemDatabase.cs` — All items (weapons, armor, consumables, materials, skill books)
- [ ] World loot placement (chests, hidden drops)

### Task 5.3: Points of Interest

- [ ] `POIGenerator.cs` — Place towns, dungeons, bosses, fishing spots on world map
- [ ] Town system: NPCs, shops, crafting stations
- [ ] Dungeon system: rooms, enemies, boss, loot
- [ ] Fast travel network (bonfires, signs)

---

## Phase 6: Side Content Integration (Week 8-11)

### Task 6.1: Farming System (Adapted)

- [ ] Farming plots at designated fertile biome zones
- [ ] Adapt existing `FieldManager.cs` for new terrain system
- [ ] Crop growth tied to day/night cycle

### Task 6.2: Fishing System (Adapted)

- [ ] Fishing spots marked in world (water biome chunks)
- [ ] Adapt `FishingController.cs` and `FishingProgression.cs`
- [ ] Fish as consumables / sell items

### Task 6.3: Crafting System (Adapted)

- [ ] Adapt `CraftingManager.cs` — new recipes for weapons, armor, potions
- [ ] Crafting stations in towns and player homes
- [ ] Recipe discovery via skill books and exploration

### Task 6.4: NPC & Economy (Adapted)

- [ ] Adapt NPC scripts for shop/companion/quest roles
- [ ] `VendorShopManager` adapted for RPG economy
- [ ] Friendship system simplified

### Task 6.5: Housing System

- [ ] Player home plots at designated locations
- [ ] Build/decorate system (adapted from CountryLife blueprints)
- [ ] Chest storage, crafting stations, farming attachment

---

## Phase 7: Multiplayer (Week 10-14)

### Task 7.1: Server Infrastructure

- [ ] `GameServer.cs` — Dedicated server bootstrap
- [ ] Chunk synchronization (server generates → clients receive heights)
- [ ] Player session management

### Task 7.2: State Synchronization

- [ ] Player position/action sync (client prediction + server reconciliation)
- [ ] Enemy state sync (AI, health, attacks)
- [ ] Loot synchronization
- [ ] Chat/text communication

### Task 7.3: Game Modes

- [ ] Solo mode (local server)
- [ ] Co-op (2-4 players, invite system)
- [ ] Invasion (hostile PvP, 1-6 players)
- [ ] Arena (matchmaking PvP, 2-8 players)
- [ ] World Boss (4-16 players)

### Task 7.4: Anti-Cheat

- [ ] Server-authoritative damage
- [ ] Position validation
- [ ] Action rate limiting
- [ ] Chunk integrity checks

---

## Phase 8: UI/UX (Week 11-14)

### Task 8.1: HUD

- [ ] HP/FP/Stamina bars
- [ ] Compass + minimap with chunk grid overlay
- [ ] Skill bar (6-8 slots)
- [ ] Multiplayer indicators
- [ ] Enemy health bars

### Task 8.2: Menus

- [ ] Main Menu (New Game, Continue, Multiplayer, Settings)
- [ ] Pause Menu
- [ ] Full-screen Skill Tree UI
- [ ] Inventory/Equipment UI
- [ ] World Map UI
- [ ] Multiplayer Browser/Party UI

### Task 8.3: Interaction & Dialogue

- [ ] Context-sensitive prompts (adapted from existing)
- [ ] NPC dialogue system (simplified)

---

## Phase 9: Polish & Optimization (Week 14-16)

- [ ] LOD system for distant chunks
- [ ] Texture atlasing for terrain
- [ ] Occlusion culling
- [ ] Object pooling optimization
- [ ] Audio system (ambient, combat, music per biome)
- [ ] Save system stress testing
- [ ] Performance profiling (target FPS per platform)
- [ ] Bug fixes and balance pass

---

## Implementation Priority Summary

| Phase | Priority | Duration | Dependencies |
|-------|----------|----------|--------------|
| 0 - Cleanup | **Critical** | 2 weeks | None |
| 1 - Chunks | **Critical** | 3 weeks | Phase 0 |
| 2 - Player | **Critical** | 2 weeks | Phase 1 |
| 3 - Combat | **Critical** | 3 weeks | Phase 2 |
| 4 - Skills | **High** | 3 weeks | Phase 3 |
| 5 - World Content | **High** | 4 weeks | Phase 1, 3 |
| 6 - Side Content | **Medium** | 3 weeks | Phase 1, 2 |
| 7 - Multiplayer | **High** | 4 weeks | Phase 1, 3 |
| 8 - UI/UX | **High** | 3 weeks | All above |
| 9 - Polish | **Medium** | 2 weeks | All above |

**Estimated Total:** ~16 weeks (4 months) for MVP

---

## File Structure Overview (Scripts to Create)

### Core
```
Scripts/Core/
├── GameManager.cs           # Adapt existing
├── GameBootstrap.cs         # Adapt existing
├── SaveManager.cs           # Adapt existing
├── SettingsManager.cs       # Adapt existing
├── Localization.cs          # Keep if needed
├── MonoSingleton.cs         # Keep
├── GameStats.cs             # Adapt existing
├── ColorPalette.cs          # Keep
└── MeshCombiner.cs          # Keep
```

### World/Chunks
```
Scripts/World/Chunks/
├── ChunkData.cs             # NEW - chunk data structure
├── ChunkCoord.cs            # NEW - coordinate pair
├── ChunkKey.cs              # NEW - file naming utility
├── ChunkLoader.cs           # NEW - streaming controller
├── ChunkSaveManager.cs      # NEW - persistence
├── ChunkObjectPool.cs       # NEW - object pooling
└── ChunkValidator.cs        # NEW - debug gap checker
```

### World/Terrain
```
Scripts/World/Terrain/
├── TerrainNoiseGenerator.cs # NEW - 5-layer Perlin noise
├── NoiseLayerConfig.cs      # NEW - ScriptableObject
├── ChunkMeshGenerator.cs    # NEW - mesh from ChunkData
├── TerrainMaterial.cs       # NEW - terrain shader/material
└── BiomeNoiseGenerator.cs   # NEW - biome determination
```

### World/Biomes
```
Scripts/World/Biomes/
├── BiomeData.cs             # NEW - biome definition
├── BiomeRegistry.cs         # NEW - all biomes
├── EnemySpawner.cs          # NEW - biome spawn rules
└── POIGenerator.cs          # NEW - points of interest
```

### World/Streaming
```
Scripts/World/Streaming/
├── RenderDistanceController.cs # NEW - player setting
├── ChunkPriorityQueue.cs       # NEW - load order
└── WorldStreamer.cs            # NEW - orchestrator
```

### Player/Controller
```
Scripts/Player/Controller/
├── PlayerController.cs      # NEW - movement/input
├── CameraController.cs      # NEW - 3rd person orbit
└── PlayerInputHandler.cs    # NEW - input mapping
```

### Player/Combat
```
Scripts/Player/Combat/
├── CombatController.cs      # NEW - combat input/states
├── StaminaSystem.cs         # NEW - stamina management
├── HitboxSystem.cs          # NEW - weapon hitboxes
└── LockOnTargeting.cs       # NEW - target lock system
```

### Player/Stats
```
Scripts/Player/Stats/
├── PlayerStats.cs           # NEW - 8 core stats
├── StatEffects.cs           # NEW - stat modifiers
├── LevelUpSystem.cs         # NEW - XP and leveling
├── ClassUnlocker.cs         # NEW - class unlock checks
└── ClassData.cs             # NEW - 10 class definitions
```

### Player/Inventory
```
Scripts/Player/Inventory/
├── InventorySystem.cs       # NEW - inventory management
├── EquipmentManager.cs      # NEW - gear slots
├── ItemBase.cs              # NEW - item base class
└── ConsumableManager.cs     # NEW - consumable effects
```

### Combat/AI
```
Scripts/Combat/AI/
├── EnemyController.cs       # NEW - FSM enemy AI
├── EnemyTypes.cs            # NEW - enemy definitions
├── BossController.cs        # NEW - multi-phase bosses
├── EnemyAnimation.cs        # NEW - AI-driven animation
└── DetectionSystem.cs       # NEW - sight/hearing ranges
```

### Combat/Weapons
```
Scripts/Combat/Weapons/
├── WeaponBase.cs            # NEW - weapon stats/scaling
├── WeaponType.cs            # NEW - weapon type enum
├── WeaponArt.cs             # NEW - unique weapon ability
├── DamageCalculator.cs      # NEW - damage formula
└── WeaponUpgradeSystem.cs   # NEW - upgrades/infusion
```

### Combat/Skills
```
Scripts/Combat/Skills/
├── SkillTree.cs             # NEW - tree data structure
├── SkillNode.cs             # NEW - node definition
├── SkillManager.cs          # NEW - equip/track skills
├── SkillBook.cs             # NEW - unlock new branches
├── ActiveSkill.cs           # NEW - equippable abilities
├── PassiveSkill.cs          # NEW - permanent effects
└── UltimateSkill.cs         # NEW - endgame abilities
```

### Combat/Effects
```
Scripts/Combat/Effects/
├── HitPause.cs              # NEW - hit stop effect
├── StatusEffect.cs          # NEW - poison, bleed, etc.
├── DamageNumbers.cs         # NEW - floating damage text
└── ScreenShake.cs           # NEW - camera shake
```

### Multiplayer
```
Scripts/Multiplayer/Server/
├── GameServer.cs            # NEW - server bootstrap
├── ServerChunkManager.cs    # NEW - server-side chunks
├── PlayerSession.cs         # NEW - session management
└── Matchmaking.cs           # NEW - queue/match system

Scripts/Multiplayer/Client/
├── ClientNetworkManager.cs  # NEW - client connection
├── ClientPrediction.cs      # NEW - input prediction
└── Reconciliation.cs        # NEW - server reconciliation

Scripts/Multiplayer/Sync/
├── StateSynchronizer.cs     # NEW - entity sync
├── AntiCheat.cs             # NEW - validation
├── ChunkSync.cs             # NEW - chunk data sync
└── LootSync.cs              # NEW - loot synchronization
```

### SideContent
```
Scripts/SideContent/
├── Farming/                 # Adapt from existing
│   ├── FieldManager.cs
│   └── CropGrowth.cs
├── Fishing/                 # Adapt from existing
│   ├── FishingController.cs
│   └── FishingProgression.cs
├── Crafting/                # Adapt from existing
│   ├── CraftingManager.cs
│   └── RecipeDatabase.cs
├── NPCs/                    # Adapt from existing
│   ├── NPCBase.cs
│   ├── VendorShop.cs
│   └── FriendshipManager.cs
├── Pets/                    # Adapt from existing
│   ├── PetController.cs
│   └── GoblinHelper.cs
├── Housing/                 # Adapt from existing
│   ├── HousingManager.cs
│   ├── BuildingPlacement.cs
│   └── ChestStorage.cs
└── Livestock/               # Adapt from existing
    ├── Livestock.cs
    └── AnimalSpawner.cs
```

### UI
```
Scripts/UI/
├── HUDManager.cs            # NEW - main HUD
├── HealthBar.cs             # Adapt from existing
├── SkillBarUI.cs            # NEW - skill hotbar
├── MinimapUI.cs             # NEW - minimap with chunks
├── SkillTreeUI.cs           # NEW - full-screen tree
├── InventoryUI.cs           # NEW - inventory screen
├── EquipmentUI.cs           # NEW - gear screen
├── WorldMapUI.cs            # NEW - world map
├── MultiplayerUI.cs         # NEW - server browser/party
├── DialogueUI.cs            # Adapt from existing
├── MainMenuUI.cs            # Adapt from existing
├── PauseMenuUI.cs           # Adapt from existing
└── InteractionPromptUI.cs   # Adapt from existing
```

### Utils
```
Scripts/Utils/
├── MonoSingleton.cs         # Keep
├── ObjectPool.cs            # NEW - generic pooling
├── BinarySerializer.cs      # NEW - chunk serialization
├── NoiseUtils.cs            # NEW - noise math helpers
└── MathUtils.cs             # NEW - geometry/vector utils
```

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Chunk seams/gaps | Medium | High | Strict edge vertex rules, debug validation tool |
| Performance at max radius | High | High | LOD, object pooling, async chunk loading |
| Multiplayer desync | Medium | High | Server-authoritative, reconciliation, tick-based sync |
| Skill tree balance | High | Medium | Spreadsheet balancing, iterative playtesting |
| Scope creep | High | High | Strict phase adherence, MVP-first approach |
| Mobile performance | Medium | Medium | Lower max radius, simplified shaders, profiling early |
