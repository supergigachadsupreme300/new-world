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
│   ├── Stats/           # 11 stats, leveling, skill XP, class unlocks
│   ├── Races/           # 22 races: data, passives, unlock, rigs
│   ├── Creation/        # Character creation (race select)
│   └── Inventory/       # Equipment, items, consumables
├── Combat/              # (Player/Combat deprecated; all combat here)
│   ├── AI/              # Enemy AI, bosses
│   ├── Weapons/         # Weapon behaviors, damage calc, spells (§3.6/§3.8)
│   ├── Effects/         # Status effects, stamina, hit pause, numbers
│   └── Skills/          # Active/ultimate abilities (§3.3)
│   (Skill XP replaces the old Combat/Skills tree — see Phase 4)
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

- [x] `CombatController.cs` — Input handling for light/heavy/dodge/block/parry
- [x] `StaminaSystem.cs` — Stamina management, regen, drain per action
- [x] `DamageCalculator.cs` — Damage formula implementation
- [x] `HitboxSystem.cs` — Weapon hitbox generation, collision detection
- [x] `DamageType.cs` — enum of the 10 damage types (Physical, Fire, Ice, Lightning, Holy, Dark, Wind, Earth, Water, Arcane — §3.7)
- [x] `StatusEffectType.cs` — enum: Bleed, Poison, Rot, Frost, Burn, Stagger (separate from damage types; §3.7)
- [ ] `IDamageResistance.cs` — per-type resistance source (interface; equipment-backed in later phase)
- [ ] `NeutralResistance.cs` — placeholder neutral resistance until equipment exists

### Task 3.2: Weapon System (expandable architecture, see game-design.md §3.6)

- [x] `WeaponData.cs` — ScriptableObject weapon data base (id, weight, Str req, hand usage, base damage, speed, reach, scaling, **DamageType** (one of 10, §3.7), Weapon Art ref)
- [x] `WeaponCategory.cs` — enum: Melee, Ranged, Magic (expandable)
- [x] `IWeaponBehavior.cs` — behavior contract: BeginAttack / ActiveFrame / Cancel
- [x] `MeleeWeaponBehavior.cs` — hitbox arc sweep (via existing HitboxSystem)
- [x] `RangedWeaponBehavior.cs` — projectile/raycast, **consumes ammo**, accuracy from Dexterity
- [x] `MagicWeaponBehavior.cs` — routes to the spell pipeline (§3.8); staff/wand/book magic-mods (MagicDamageMult, CastTimeMod, CooldownMod) scale spells; FP cost; spell power from Wisdom
- [x] `SpellData.cs` — ScriptableObject spell base (DamageType, base power, FP cost, cast time, cooldown, range/area, projectile/instant/self, cast anim, status effect)
- [x] `SpellCaster.cs` — validates FP + cooldown, plays cast time, resolves via DamageCalculator (scaled by Wisdom); generic cooldown API (CooldownReady/StartCooldown) for weapon arts
- [x] `SpellEffect.cs` — projectile / instant / zone spell spawning
- [x] `WeaponDatabase.cs` — registry resolving equipped weapon's category → behavior
- [ ] Weapon types: Sword, Axe, Spear, Bow, Staff, Dagger, Shield, Claw, Katana, Hammer
- [x] `WeaponArt.cs` — Per-weapon unique ability (Range/Radius fields added for strike area)
- [x] `WeaponArtExecutor.cs` — triggers weapon art: FP cost, cooldown gate, forward strike + IDamageable
- [x] `IDamageable.cs` — health pool interface; all combat pipelines (melee/ranged/spell/art) apply via this
- [ ] Weapon upgrades, infusion system
- [x] **Wielding states** (see game-design.md §5.4): single (one-hand, off-hand free), dual (one per hand, needs ~2× Str), two-hand grip (both slots, ~halved Str requirement)

### Task 3.3: Animation & Feedback

- [x] Combat animation states (idle, attack chain, dodge, block) — `CombatAnimation.cs` bridges CombatController state → Animator (controller asset authored in-editor)
- [x] Hit pause / hit stop (brief freeze on impact for game feel) — `HitStop.cs`
- [x] Screen shake, floating damage numbers — `ScreenShake.cs`, `DamageNumber.cs`, `CombatFeedback.cs` coordinator
- [ ] Particle effects, sound triggers (asset wiring in-editor)
- [x] Enemy ragdoll on death — `RagdollEnabler.cs` (rig authored in-editor)

---

## Phase 4: Races, Stats & Classes (Week 5-8)

> **Design decision (supersedes original "Skill Tree" intent):**
> The original Phase 4 plan called for a giant skill-tree + stat system. Scope review
> replaced the skill TREE with a **6-category Skill XP system** (no tree nodes — flat
> tier rewards per category), added a full **22-race system**, and kept the classless
> **15-class unlock** system. Rationale: races + stat % scaling + skill XP tiers deliver
> build variety with far less content than a full tree, and match the user's requested
> "passive-only racial kits."

### Task 4.1: Stat System (Foundation)

- [x] `StatType.cs` — enum: Health, Speed, Endurance, Strength, Dexterity, AttackSpeed, Defense, Intelligence, Wisdom, Faith, Luck (11 stats; Arcane removed)
- [x] `PlayerStats.cs` — 11 core stats with **% racial modifiers applied to TOTAL stat on-the-fly** (scales with leveling)
- [x] Derived values via formulas (see game-design.md §3.4): MaxHP=100+Health×12, MaxFP=50+Intelligence×10, MaxStamina=100+Endurance×10, EquipLoad=40+Endurance×2, CritChance=5%+Luck×0.15%, AttackSpeed from AttackSpeed stat (primary, large) + small Speed/Dexterity bonus, MoveSpeed/DodgeSpeed from Speed, DamageReduc from Defense, melee AtkPower from Strength (heavy) & Dexterity (light), ranged damage = weapon base (accuracy from Dexterity), resistances = equipment-only (symbolic k_* tuning knobs)
- [x] `GetTotal(Stat)` — returns `base * (1 + racialMod%)` always in sync with level-ups
- [x] `LevelUpSystem.cs` — total XP, progressive thresholds, grants stat points on level-up
- [x] Racial XP multiplier applied to all XP sources in LevelUpSystem
- [x] `PlayerStats` implements `IStatProvider` — real stats now feed Melee/Ranged/Spell scaling (was placeholder in Phase 3)

### Task 4.2: Skill XP System (Replaces Skill Tree)

- [x] `SkillType.cs` — enum: Melee, Ranged, Magic, Stealth, Crafting, Fortitude (was "Defense")
- [x] `SkillXpTracker.cs` — 6 categories, each with own XP bar + level
- [x] `AddXp(SkillType, amount)` → amount × (1 + racial XP bonus)
- [x] Tier rewards at category levels 5 / 10 / 15 / 20 / 25
- [ ] Hooked into: combat (Melee/Ranged), magic use, stealth actions, crafting, damage taken (Fortitude) *(integration hooks remain)*

### Task 4.3: Race System (22 Races)

- [x] `RaceData.cs` — `[CreateAssetMenu]` ScriptableObject data unit (raceId, name, lore, stat %, passive, XP bonus, weight, rig params)
- [x] `RaceDatabase.cs` — collection of all 22 `RaceData` assets + lookup + weighted random roll (default roster built programmatically; Human 50%, others ~2.38%)
- [x] `RacePassiveManager.cs` — dispatch that applies racial passives each frame
- [x] `RaceUnlockManager.cs` — MonoSingleton, persistent unlock set (creation + discovery), Human always unlocked
- [x] `RaceDiscoveryPoint.cs` — world altar that unlocks a race and allows mid-play transform
- [x] `RaceRig.cs` — monkey-swaps player model rig; procedural placeholder bodies now, real models drop in later
- [x] **Mid-play race change** at discovery altars (costs rare Ritual Stone; Human free) *(Ritual Stone consumption hooked via inventory)*
- [x] Full 22-race roster defined (see game-design.md §3.5)
- [x] Balance tiers: wide stat-budget spread, tank/utility/handicap races compensated via passives & XP (see game-design.md §3.5 "Stat Balance & Tiers")

### Task 4.4: Character Creation

- [x] `CharacterCreation.cs` — **weighted random race roll that auto-commits** (Human 50%, each other ~2.38%)
- [x] Spawns `RaceRig`, applies stat/passive/XP, writes unlock state
- [ ] Preview total stats + passive + XP bonus on the creation screen *(UI pending, Phase 8)*

### Task 4.5: Class Unlock System

- [x] `ClassData.cs` — `[CreateAssetMenu]` ScriptableObject: 15 classes (name, stat requirement, unique ability, icon)
- [x] `ClassUnlocker.cs` — checks `PlayerStats` totals vs thresholds, grants ability, fires event
- [x] Classless non-exclusive unlocks (per game-design.md §3.2)
- [ ] UI notification on class unlock *(UI pending, Phase 8)*

---

## Phase 5: Enemy AI & World Content (Week 6-10)

### Task 5.1: Enemy AI

- [x] `EnemyController.cs` — FSM: Patrol, Alert, Chase, Attack, Flee, Dead
- [x] `EnemySpawner.cs` — Biome-based spawn rules, density, day/night variants
- [x] `BossController.cs` — Multi-phase boss AI, attack patterns, health bars
- [x] Enemy types per biome (9 biomes x 3-5 enemy types each) — rosters defined in `BiomeRegistry.cs` (game-design §7.1); model/prefab variants for each id still needed

### Task 5.2: Loot & Drops

- [x] `LootTable.cs` — Weighted drop tables per enemy
- [x] `ItemDatabase.cs` — All items (weapons, armor, consumables, materials, skill books)
- [x] World loot placement (chests, hidden drops)

### Task 5.3: Points of Interest

- [x] `POIGenerator.cs` — Place towns, dungeons, bosses, fishing spots on world map
- [x] Town system: NPCs, shops, crafting stations
- [x] Dungeon system: rooms, enemies, boss, loot
- [x] Fast travel network (bonfires, signs)

---

## Phase 6: Side Content Integration (Week 8-11)

### Task 6.1: Farming System (Adapted)

- [x] Farming plots at designated fertile biome zones
- [x] Adapt existing `FieldManager.cs` for new terrain system
- [x] Crop growth tied to day/night cycle

### Task 6.2: Fishing System (Adapted)

- [x] Fishing spots marked in world (`FishSpot` + `FishSpotPlacer` at water-biome / `PoiKind.Fishing` POIs)
- [x] Adapt `FishingController.cs` and `FishingProgression.cs` (reuse species, `SkillManager` fishing track, `ToolManager` inventory)
- [x] Fish as consumables / sell items (`FishData.SellValue` + `IsConsumable`)

### Task 6.3: Crafting System (Adapted)

- [x] New discoverable recipes for weapons, armor, potions, food (`RecipeRegistry`, `ItemDatabase` results)
- [x] Crafting stations in towns (`CraftingStation` placed by `Town.Build`) and player homes (Task 6.5)
- [x] Recipe discovery via skill books and exploration (`RecipeDiscovery`, `skill_book_*` unlocks)

### Task 6.4: NPC & Economy (Adapted)

- [x] Adapt NPC scripts for shop/companion/quest roles (`NpcController` + `WorldNpcPlacer`)
- [x] `VendorShopManager` adapted for RPG economy (`EconomyProvider` bridges `ItemDatabase` values + `ToolManager` + `player.Money`)
- [x] Friendship system simplified (`FriendshipSimplified` adapter over `FriendshipManager` heart levels + discounts)

### Task 6.5: Housing System

- [x] Player home plots at designated locations (`HousePlot` + `HousePlotPlacer`)
- [x] Build/decorate system (`HomeBuilder`, simplified from CountryLife blueprints)
- [x] Chest storage (`HomeChest`), crafting stations (reuses `CraftingStation`), farming attachment (`FarmPlot` grid)

---

## Phase 7: Multiplayer (Week 10-14)

### Task 7.1: Server Infrastructure

- [x] `GameServer.cs` — Dedicated server bootstrap (`GameServer` + `NetServerHost`, `NEWWORLD_SERVER` gate)
- [x] Chunk synchronization (`ChunkSync`: server generates heights → broadcasts `ChunkData` batches)
- [x] Player session management (`PlayerSession`, handshake/heartbeat/timeout, endpoint-keyed registry)

### Task 7.2: State Synchronization

- [x] Player position/action sync (`PlayerStateSync`: client prediction + server reconciliation)
- [x] Enemy state sync (`EnemyStateSync`: AI, health, attacks → client snapshots)
- [x] Loot synchronization (`LootSync`: spawn / collect / despawn agreement)
- [x] Chat/text communication (`ChatSync`: server relay + inbound buffer)

### Task 7.3: Game Modes

- [x] Solo mode (`NetLobby.EnterSoloLocal` — local non-listening server)
- [x] Co-op (2-4 players, invite system — `NetLobby.Invite` slot reservation)
- [x] Invasion (hostile PvP, 1-6 players — `GameMode` hostile flag)
- [x] Arena (matchmaking PvP, 2-8 players — `Matchmaker`)
- [x] World Boss (4-16 players — `BossEvent` authoritative boss broadcast)

### Task 7.4: Anti-Cheat

- [x] Server-authoritative damage (`AntiCheat.ValidateDamage` + `DamageIntent`)
- [x] Position validation (`AntiCheat.ValidatePosition` — teleport/hop rejection + clamp)
- [x] Action rate limiting (`AntiCheat.ActionBudget` — per-session token bucket)
- [x] Chunk integrity checks (`AntiCheat.HashChunk` / `ValidateChunkHash` — FNV-1a)

---

## Phase 8: UI/UX (Week 11-14)

### Task 8.1: HUD

- [x] HP/FP/Stamina bars (`PlayerBarsHUD`)
- [x] Compass + minimap with chunk grid overlay (`CompassMinimapHUD`)
- [x] Skill bar (6-8 slots) (`SkillBarHUD`)
- [x] Multiplayer indicators (`MultiplayerIndicatorHUD`)
- [x] Enemy health bars (`EnemyHealthBarHUD`)

### Task 8.2: Menus

- [x] Main Menu (New Game, Continue, Multiplayer, Settings) — adapted (exists)
- [x] Pause Menu — adapted (exists)
- [x] Character Creation UI (race select + stat preview, Phase 4) — `CharacterCreationUI`
- [x] Race/Stat Sheet UI (Phase 4) — `RaceStatSheetUI`
- [x] Inventory/Equipment UI — `InventoryEquipmentUI` + `EquipmentSystem`
- [x] World Map UI — `WorldMapUI`
- [x] Multiplayer Browser/Party UI — `MultiplayerBrowserUI` (+ `NetServerHost.Lobbies` accessor)

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
| 4 - Races, Stats & Classes | **High** | 3 weeks | Phase 3 |
| 5 - World Content | **High** | 4 weeks | Phase 1, 3 |
| 6 - Side Content | **Medium** | 3 weeks | Phase 1, 2 |
| 7 - Multiplayer | **High** | 4 weeks | Phase 1, 3 |
| 8 - UI/UX | **High** | 3 weeks | All above |
| 9 - Polish | **Medium** | 2 weeks | All above |

**Estimated Total:** ~16 weeks (4 months) for MVP

---

## File Structure Overview (Scripts to Create)

### Networking
```
Scripts/Networking/
├── NetMessage.cs             # NEW - opcodes + binary message writer/reader (no netcode package)
├── UdpNetTransport.cs        # NEW - INetTransport abstraction + dependency-free UDP impl
├── GameServer.cs             # NEW - dedicated server bootstrap (sessions, handshake, dispatch)
├── PlayerSession.cs          # NEW - session state (endpoint, ack, liveness, replicated pos)
├── ChunkSync.cs              # NEW - server generates heights → broadcasts ChunkData batches
├── PlayerStateSync.cs        # NEW - client prediction + server reconciliation (player pos/action)
├── EnemyStateSync.cs         # NEW - server-authoritative enemy snapshots (AI, health, attacks)
├── LootSync.cs               # NEW - spawn/collect/despawn loot agreement across clients
├── ChatSync.cs               # NEW - server-relayed chat + client inbound buffer
├── GameMode.cs               # NEW - mode definitions (Solo/Co-op/Invasion/Arena/WorldBoss)
├── NetLobby.cs               # NEW - lobby aggregation + co-op invites + solo local boot
├── Matchmaker.cs             # NEW - arena matchmaking (2-8 players) + announce
├── BossEvent.cs              # NEW - authoritative world-boss broadcast (4-16 players)
├── AntiCheat.cs              # NEW - damage/position/action-rate/chunk-integrity guards
└── NetServerHost.cs          # NEW - MonoBehaviour harness, guarded by NEWWORLD_SERVER
```
> **Networking note:** Phase 7 adds a transport-agnostic layer with no external netcode
> package required. The server owns the authoritative session registry + timers
> (Task 7.1); state sync (Task 7.2), game modes (7.3), and anti-cheat (7.4) ride the
> same <see cref="GameServer"/> message path. `NetServerHost` boots the server only in
> builds compiled with the `NEWWORLD_SERVER` symbol so dedicated builds are separated
> from the singleplayer client.

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
└── EnemySpawner.cs          # NEW - biome spawn rules
```

### World/Poi
```
Scripts/World/Poi/
├── POIDefinition.cs         # NEW - POI data (kind, biome, loot, boss, fast travel)
├── POIRegistry.cs           # NEW - programmatic POI roster per biome
├── POIGenerator.cs          # NEW - places towns/dungeons/arenas/fishing/travel
├── Town.cs                  # NEW - town system (shop/crafting markers + fast travel)
├── DungeonSystem.cs         # NEW - rooms, enemies, boss, loot
└── FastTravelNode.cs        # NEW - bonfire/sign fast-travel node
```
> **Fast travel note:** Task 5.3 composes the existing `FastTravelSign` +
> `FastTravelMenu` (Vehicles/UI) — the POI generator spawns `FastTravelSign`
> components that the menu auto-discovers, so the fast-travel network reuses the
> shipped sign UI rather than creating a parallel system.

### World/Farming
```
Scripts/World/Farming/
├── CropData.cs              # NEW - crop definition (CropType, growth, harvest)
├── CropRegistry.cs          # NEW - programmatic crop roster
├── FarmPlot.cs              # NEW - tilled plot + day/night-driven growth
├── FarmingManager.cs        # NEW - adapts FieldManager raycast preview + growth ticker
└── FarmingZone.cs           # NEW - farming plots at fertile biome zones
```
> **Farming adaptation note:** Task 6.1 builds on the legacy `FieldManager`
> placement concept but raycasts any ground (not CountryLife collider names) and
> drives crop growth from `GameManager.TimeOfDay` (game-design §7.3): plots only
> advance during daylight and require water, matching §5.1 plant/water/harvest.

### World/Fishing
```
Scripts/World/Fishing/
├── FishData.cs              # NEW - fish definition (FishType, weight, flop, sell, consumable)
├── FishRegistry.cs          # NEW - programmatic fish roster (legacy species + reef/eel)
├── FishingSpot.cs           # NEW - world fishing spot; rolls FishRegistry, grants via ToolManager
└── FishingSpotPlacer.cs     # NEW - places fishing spots at water-biome / fishing POIs
```
> **Fishing adaptation note:** Task 6.2 reuses the existing `FishingController`
> minigame species (`fish_carp`/`fish_salmon`/`fish_tuna`/`fish_pufferfish`) and
> feeds catches into the same `ToolManager` inventory + `SkillManager` fishing
> track + `QuestManager` `fish_catch`. New `FishingSpot` marks spots at water
> biomes / `PoiKind.Fishing` POIs anywhere on the map instead of the legacy
> hardcoded sea (`x < -140`). Fish carry `SellValue` for the vendor economy and are
> marked consumable/food (§5.2).

### World/Crafting
```
Scripts/World/Crafting/
├── RecipeData.cs             # NEW - discoverable recipe definition (RecipeData, RecipeKind, IngredientSpec)
├── RecipeRegistry.cs         # NEW - programmatic recipe roster (weapons, armor, potions, food)
├── RecipeDiscovery.cs        # NEW - gated known-recipes; skill-book unlocks (§5.3)
└── CraftingStation.cs        # NEW - world crafting panel (discovery-aware, ToolManager economy)
```
> **Crafting adaptation note:** Task 6.3 keeps the legacy `CraftingManager`
> (cooking/preserves/brewing stations) untouched and adds a discovery-aware
> `CraftingStation` for weapon/armor/potion/food. Recipes reference `ItemDatabase`
> ids so crafted goods flow through the shared item economy; recipes marked
> `RequiresDiscovery` are gated by `RecipeDiscovery`, unlocked by consuming a
> `skill_book_*` item (§5.3). `Town.Build` now also places a `CraftingStation`
> (Task 6.5 reuses it for player homes).

### World/Npcs
```
Scripts/World/Npcs/
├── NpcRole.cs                # NEW - NPC definition + role/shop-mode enums
├── NpcController.cs          # NEW - shop/quest/follower NPC, opens VendorShopManager
├── EconomyProvider.cs        # NEW - RPG economy bridge (ItemDatabase prices + ToolManager + money)
├── FriendshipSimplified.cs   # NEW - thin adapter over FriendshipManager hearts/discount
└── WorldNpcPlacer.cs         # NEW - places data-driven NPCs in towns / homes
```
> **NPC & economy adaptation note:** Task 6.4 keeps the legacy `VendorShopManager`,
> `FriendshipManager`, and the CountryLife NPC scripts intact and adds a
> data-driven `NpcController` that composes them: a Vendor opens the shipped
> vendor UI in a configured mode, a QuestGiver drives `QuestManager`, a Follower
> is a simplified follow companion. `EconomyProvider` prices `ItemDatabase` items
> from `BaseValue` and moves money/inventory through `ToolManager` +
> `PlayerController.Money` + `GameStats`/`QuestManager`. `FriendshipSimplified`
> delegates hearts/discount to `FriendshipManager` (no parallel friendship system).

### World/Housing
```
Scripts/World/Housing/
├── HousePlot.cs             # NEW - fixed home plot; BuildHome erects shell + attachments
├── HomeBuilder.cs           # NEW - simplified build/decorate from blueprint concept
├── HomeChest.cs             # NEW - home storage (deposit/withdraw via ToolManager)
└── HousePlotPlacer.cs       # NEW - places home plots at designated locations
```
> **Housing adaptation note:** Task 6.5 keeps the CountryLife `WorldBuilder` blueprint
> pipeline untouched and adds a light open-world house layer. A `HousePlot` is placed at
> designated locations by `HousePlotPlacer`; `BuildHome()` erects foundation/walls/roof
> placeholders and attaches a home `CraftingStation` (Task 6.3), a `HomeChest` for storage,
> and a small `FarmPlot` grid for the farming attachment (§5.1/§5.3). `HomeBuilder.TryBuild`
> spends tool wood to place extra build objects from the plot.

### World/Loot
```
Scripts/World/Loot/
├── ItemData.cs              # NEW - item definition (ItemData, ItemType)
├── ItemDatabase.cs          # NEW - all items
├── LootTable.cs             # NEW - weighted per-enemy drop tables
├── LootDrop.cs              # NEW - world pickup + IItemCollection/ILootLuckProvider seams
├── LootContainer.cs         # NEW - chests / world loot containers
└── WorldLootPlacement.cs    # NEW - chest + hidden-drop seeding
```

### World/Streaming
```
Scripts/World/Streaming/
├── RenderDistanceController.cs # NEW - player setting
├── ChunkPriorityQueue.cs       # NEW - load order
└── WorldStreamer.cs            # NEW - orchestrator
```

### UI/NewWorld
```
Scripts/UI/NewWorld/
├── HudCanvas.cs                # NEW - overlay canvas + fill-bar factory
├── PlayerBarsHUD.cs            # NEW - HP/FP/Stamina fill bars (Task 8.1)
├── CompassMinimapHUD.cs        # NEW - compass + minimap with chunk-grid overlay
├── SkillBarHUD.cs              # NEW - 6-slot skill bar
├── MultiplayerIndicatorHUD.cs  # NEW - MP session/chat indicators
├── EnemyHealthBarHUD.cs        # NEW - pooled enemy health bars
├── MenuPanelBase.cs            # NEW - modal menu overlay base (Task 8.2)
├── RaceStatSheetUI.cs          # NEW - race/stat sheet + stat allocation preview
├── CharacterCreationUI.cs      # NEW - race select + stat preview
├── InventoryEquipmentUI.cs     # NEW - inventory + equipment (<EquipmentSystem>)
├── EquipmentSystem.cs          # NEW - minimal equip-slot backer (Weapon/Armor)
├── WorldMapUI.cs               # NEW - POI registry + chunk-coordinate map
└── MultiplayerBrowserUI.cs     # NEW - session/lobby browser + party entry
```
> **UI note:** Phase 8 HUD components are self-contained MonoBehaviours that build their own
> screen-space overlays, composing the existing UIManager + PlayerController/PlayerStats/
> SkillManager/EnemyController contracts without rewriting the live HUD. Add the components
> to a scene/`Prefabs` to activate each overlay individually.

### Player/Controller
```
Scripts/Player/Controller/
├── PlayerController.cs      # NEW - movement/input
├── CameraController.cs      # NEW - 3rd person orbit
└── PlayerInputHandler.cs    # NEW - input mapping
```

### Player/Combat
> **Deprecated location.** `Scripts/Player/Combat/` is empty in the repo. All combat code
> lives under **`Scripts/Combat/`** instead (Weapons, Effects, Skills, AI) — see the
> those blocks below:
```
Scripts/Combat/
├── Weapons/    # CombatController, HitboxSystem, DamageCalculator, Weapon* modules (§3.6)
├── Effects/    # StatusEffect, StaminaSystem, HitPause, DamageNumbers, ScreenShake
├── Skills/     # Active/ultimate ability system (§3.3)
└── AI/         # enemy AI
```

### Player/Stats
```
Scripts/Player/Stats/
├── StatType.cs              # NEW - 11 core stat enum
├── PlayerStats.cs           # NEW - 11 core stats + % racial modifiers
├── LevelUpSystem.cs         # NEW - XP and leveling
├── SkillType.cs             # NEW - 6 skill category enum
├── SkillXpTracker.cs        # NEW - skill XP with racial multipliers
├── ClassUnlocker.cs         # NEW - class unlock checks
└── ClassData.cs             # NEW - 15 class definitions
```

### Player/Races
```
Scripts/Player/Races/
├── RaceData.cs              # NEW - race ScriptableObject (22 assets)
├── RaceDatabase.cs          # NEW - race collection + weighted roll
├── RaceRig.cs               # NEW - separate rig/model swap
├── RacePassiveManager.cs    # NEW - passive dispatch
├── RaceUnlockManager.cs     # NEW - persistent unlock state
└── RaceDiscoveryPoint.cs    # NEW - world altar revealing races
```

### Player/Creation
```
Scripts/Player/Creation/
└── CharacterCreation.cs     # NEW - weighted random + manual race select
```

### Player/Inventory
```
Scripts/Player/Inventory/
├── InventorySystem.cs       # NEW - inventory management
├── EquipmentManager.cs      # NEW - 21 gear slots (5 armor, 2 weapon, 14 accessory)
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
├── WeaponData.cs             # NEW - SO weapon data base
├── WeaponCategory.cs         # NEW - Melee/Ranged/Magic enum
├── WeaponScalingStat.cs      # (enum inside WeaponData.cs) scaling stat
├── DamageType.cs             # NEW - 10 damage types enum (§3.7)
├── IWeaponBehavior.cs        # NEW - behavior contract
├── MeleeWeaponBehavior.cs    # NEW - hitbox arc
├── RangedWeaponBehavior.cs   # NEW - projectile + ammo
├── RangedProjectile.cs       # NEW - projectile flight/impact
├── MagicWeaponBehavior.cs    # NEW - routes to spell pipeline
├── MagicWeaponMods.cs        # NEW - weapon magic mods container
├── SpellData.cs              # NEW - SO spell base (§3.8)
├── SpellCaster.cs            # NEW - validates FP/cast, resolves via DamageCalculator
├── SpellEffect.cs            # NEW - projectile / instant / zone spawn
├── WeaponDatabase.cs         # NEW - category → behavior registry
├── WeaponArt.cs              # NEW - unique weapon ability
├── IDamageResistance.cs      # NEW - per-type resistance source (interface)
├── NeutralResistance.cs      # NEW - placeholder neutral resistance
├── IStatProvider.cs          # NEW - stat access for scaling (Phase 4 wires real stats)
├── IAmmoProvider.cs          # NEW - ammo source (Inventory wires it later)
├── InfiniteAmmo.cs           # NEW - placeholder infinite ammo
├── IDamageable.cs            # NEW - health pool receiver interface
├── DamageCalculator.cs       # NEW - damage formula (§3.7 10-type)
├── CombatController.cs       # (rewired to IWeaponBehavior §3.6)
├── HitboxSystem.cs           # (carries DamageType + resistance; applies via IDamageable)
├── WeaponArtExecutor.cs      # NEW - triggers weapon art (FP cost, cooldown, damage)
└── WeaponUpgradeSystem.cs    # TODO - upgrades/infusion
```

### Combat/Skills
```
Scripts/Combat/Skills/
├── SkillManager.cs          # NEW - equip/track active skills
├── ActiveSkill.cs           # NEW - equippable abilities
├── PassiveSkill.cs          # NEW - permanent effects
├── UltimateSkill.cs         # NEW - endgame abilities
└── SkillBook.cs             # NEW - unlock new skills
# NOTE: Skill TREE replaced by SkillXpTracker (Player/Stats) in Phase 4.
# SkillManager here manages equippable ACTIVE/ultimate abilities only.
# Active-skill spells route through the shared SpellCaster (see Combat/Weapons §3.8).
```

### Combat/Effects
```
Scripts/Combat/Effects/
├── StaminaSystem.cs        # (existing) stamina pool
├── StatusEffectType.cs     # NEW - Bleed/Poison/Rot/Frost/Burn/Stagger enum (§3.7)
├── HitStop.cs              # NEW - hit stop / time freeze (Task 3.3)
├── ScreenShake.cs          # NEW - camera shake (Task 3.3)
├── DamageNumber.cs         # NEW - floating damage text (Task 3.3)
├── CombatAnimation.cs      # NEW - CombatController state → Animator bridge (Task 3.3)
├── CombatFeedback.cs       # NEW - coordinates hitstop/shake/damage numbers (Task 3.3)
├── RagdollEnabler.cs       # NEW - corpse ragdoll on death (Task 3.3)
└── StatusEffect.cs         # TODO - runtime applying/DoT for status effects
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
├── RaceStatsUI.cs           # NEW - race/stat sheet (Phase 4)
├── MinimapUI.cs             # NEW - minimap with chunks
├── InventoryUI.cs           # NEW - inventory screen
├── EquipmentUI.cs           # NEW - gear screen (21 slots: 5 armor, 2 weapon, 14 accessory)
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
