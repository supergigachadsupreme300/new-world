# Project tree

**Generated file - do not hand-edit.** Regenerate after any structural change:

```powershell
powershell -ExecutionPolicy Bypass -File tools\Write-Tree.ps1
```

| | |
|---|---|
| Source of truth | `git ls-files` (untracked and `.gitignore`d paths cannot appear) |
| Generated at commit | `c01d5a7` (HEAD when written - the commit *before* the one this file lands in) |
| Generated on | 2026-10-04 10:13 |
| Tracked files | 1173 = 539 non-`.meta` + 634 `.meta` |
| C# files | 368 |

Reading the tree:

- `.meta` files are omitted everywhere. There are 634 of them and none carries
  information a reader needs; every `.cs` has a paired `.cs.meta` and every folder under
  `Assets/` has a folder meta. `tools\StaticChecks.ps1` guards that invariant.
- `name/   [N files]` means the directory was collapsed for length. Nothing inside it is
  missing, only summarised. Raise `-ExpandBelow` to see more.
- `Assets/Scripts` is always expanded in full - it is the part people navigate.

Canonical docs: `AGENTS.md` (working rules), `game-design.md` (design),
`PROGRESS.md` (shipped work), `THINKING.md` (reasoning). This file deliberately asserts
no design or process claim, only structure - structure being the one thing it can be
regenerated to verify.

```
├── _ArtSource/   [38 files]
├── Assets/
│   ├── Resources/   [26 files]
│   ├── Scenes/
│   │   └── SampleScene.unity
│   ├── Scripts/
│   │   ├── Animation/
│   │   │   ├── PlayerAnimator.cs
│   │   │   ├── README.md
│   │   │   ├── WeaponAnimator.cs
│   │   │   └── WeaponStowAnimator.cs
│   │   ├── Combat/
│   │   │   ├── Feedback/
│   │   │   │   ├── CombatFeedback.cs
│   │   │   │   ├── DamageNumber.cs
│   │   │   │   ├── HitStop.cs
│   │   │   │   └── ScreenShake.cs
│   │   │   ├── Skills/
│   │   │   │   ├── ClassEffect.cs
│   │   │   │   ├── ClassSkill.cs
│   │   │   │   ├── ClassSkillCaster.cs
│   │   │   │   ├── ClassSkillCatalog.cs
│   │   │   │   ├── IEffect.cs
│   │   │   │   ├── PassivePerkEffect.cs
│   │   │   │   ├── PassivePerkManager.cs
│   │   │   │   ├── PassivePerkType.cs
│   │   │   │   ├── RaceEffect.cs
│   │   │   │   ├── RaceSkill.cs
│   │   │   │   ├── RaceSkillCaster.cs
│   │   │   │   ├── RaceSkillCatalog.cs
│   │   │   │   ├── Skill.cs
│   │   │   │   ├── SkillBindings.cs
│   │   │   │   ├── SkillCatalog.Crafting.cs
│   │   │   │   ├── SkillCatalog.cs
│   │   │   │   ├── SkillCatalog.Fortitude.cs
│   │   │   │   ├── SkillCatalog.Magic.cs
│   │   │   │   ├── SkillCatalog.Melee.cs
│   │   │   │   ├── SkillCatalog.Ranged.cs
│   │   │   │   ├── SkillCatalog.Shield.cs
│   │   │   │   ├── SkillCatalog.Stealth.cs
│   │   │   │   ├── SkillContext.cs
│   │   │   │   ├── SkillCost.cs
│   │   │   │   ├── SkillProfile.cs
│   │   │   │   └── SummonedAlly.cs
│   │   │   ├── Status/
│   │   │   │   ├── BlindStatus.cs
│   │   │   │   ├── CCZone.cs
│   │   │   │   ├── ChillStatus.cs
│   │   │   │   ├── ElementSignatureStatus.cs
│   │   │   │   ├── StatusEffectType.cs
│   │   │   │   └── WetStatus.cs
│   │   │   ├── Weapons/
│   │   │   │   ├── AoeAimPreview.cs
│   │   │   │   ├── CombatController.cs
│   │   │   │   ├── DamageCalculator.cs
│   │   │   │   ├── DamageType.cs
│   │   │   │   ├── HitboxSystem.cs
│   │   │   │   ├── IAmmoProvider.cs
│   │   │   │   ├── IDamageable.cs
│   │   │   │   ├── IDamageResistance.cs
│   │   │   │   ├── IHealable.cs
│   │   │   │   ├── InfiniteAmmo.cs
│   │   │   │   ├── IStatProvider.cs
│   │   │   │   ├── IWeaponBehavior.cs
│   │   │   │   ├── MagicWeaponBehavior.cs
│   │   │   │   ├── MagicWeaponMods.cs
│   │   │   │   ├── MeleeWeaponBehavior.cs
│   │   │   │   ├── NeutralResistance.cs
│   │   │   │   ├── ProjectilePathPreview.cs
│   │   │   │   ├── RangedProjectile.cs
│   │   │   │   ├── RangedWeaponBehavior.cs
│   │   │   │   ├── ShieldWeaponBehavior.cs
│   │   │   │   ├── WeaponCatalog.cs
│   │   │   │   ├── WeaponCategory.cs
│   │   │   │   ├── WeaponData.cs
│   │   │   │   ├── WeaponDatabase.cs
│   │   │   │   ├── WeaponInventory.cs
│   │   │   │   ├── WeaponRigBuilder.cs
│   │   │   │   ├── WeaponRigHost.cs
│   │   │   │   ├── WeaponSkill.cs
│   │   │   │   └── WeaponSkillExecutor.cs
│   │   │   └── StaminaSystem.cs
│   │   ├── Core/
│   │   │   ├── ColorPalette.cs
│   │   │   ├── GameBootstrap.cs
│   │   │   ├── GameInput.cs
│   │   │   ├── GameManager.cs
│   │   │   ├── GameStats.cs
│   │   │   ├── Localization.cs
│   │   │   ├── MeshCombiner.cs
│   │   │   ├── MonoSingleton.cs
│   │   │   ├── SaveManager.cs
│   │   │   └── SettingsManager.cs
│   │   ├── Crafting/
│   │   │   └── CraftingManager.cs
│   │   ├── Cutscenes/
│   │   │   ├── CutsceneManager.cs
│   │   │   ├── CutsceneManager.Driving.cs
│   │   │   ├── CutsceneManager.EndingBlackmail.cs
│   │   │   ├── CutsceneManager.EndingBossBad.cs
│   │   │   ├── CutsceneManager.EndingDemon.cs
│   │   │   ├── CutsceneManager.EndingFated.cs
│   │   │   ├── CutsceneManager.EndingHappy.cs
│   │   │   ├── CutsceneManager.EndingJustice.cs
│   │   │   ├── CutsceneManager.EndingNTR.cs
│   │   │   ├── CutsceneManager.EndingSad.cs
│   │   │   └── CutsceneManager.Helpers.cs
│   │   ├── Enemies/
│   │   │   ├── _Shared/
│   │   │   │   ├── EnemyCatalog.cs
│   │   │   │   └── EnemyController.cs
│   │   │   ├── Bandit/
│   │   │   │   └── BanditEnemy.cs
│   │   │   ├── Bat/
│   │   │   │   └── BatEnemy.cs
│   │   │   ├── Boss/
│   │   │   │   └── BossController.cs
│   │   │   ├── Demon/
│   │   │   │   └── DemonEnemy.cs
│   │   │   ├── Dragon/
│   │   │   │   └── DragonEnemy.cs
│   │   │   ├── Drake/
│   │   │   │   └── DrakeEnemy.cs
│   │   │   ├── Dummy/
│   │   │   │   └── DummyEnemy.cs
│   │   │   ├── FireElemental/
│   │   │   │   └── FireElementalEnemy.cs
│   │   │   ├── Goblin/
│   │   │   │   └── GoblinEnemy.cs
│   │   │   ├── Golem/
│   │   │   │   └── GolemEnemy.cs
│   │   │   ├── IceWolf/
│   │   │   │   └── IceWolfEnemy.cs
│   │   │   ├── Mimic/
│   │   │   │   └── MimicEnemy.cs
│   │   │   ├── Mummy/
│   │   │   │   └── MummyEnemy.cs
│   │   │   ├── Scorpion/
│   │   │   │   └── ScorpionEnemy.cs
│   │   │   ├── SeaCreature/
│   │   │   │   └── SeaCreatureEnemy.cs
│   │   │   ├── Skeleton/
│   │   │   │   └── SkeletonEnemy.cs
│   │   │   ├── Slime/
│   │   │   │   └── SlimeEnemy.cs
│   │   │   ├── Slug/
│   │   │   │   └── SlugEnemy.cs
│   │   │   ├── Treant/
│   │   │   │   └── TreantEnemy.cs
│   │   │   ├── Undead/
│   │   │   │   └── UndeadEnemy.cs
│   │   │   ├── Wolf/
│   │   │   │   └── WolfEnemy.cs
│   │   │   └── Yeti/
│   │   │       └── YetiEnemy.cs
│   │   ├── Fishing/
│   │   │   ├── FishingController.cs
│   │   │   ├── FishingProgression.cs
│   │   │   └── FlappingFish.cs
│   │   ├── Interactions/
│   │   │   ├── PlayerSitController.cs
│   │   │   └── SittableSeat.cs
│   │   ├── Livestock/
│   │   │   ├── FlyingCrane.cs
│   │   │   ├── Livestock.cs
│   │   │   └── LivestockSpawner.cs
│   │   ├── Magic/
│   │   │   ├── Cast/
│   │   │   │   ├── SpellBeam.cs
│   │   │   │   ├── SpellCaster.ApplyHit.cs
│   │   │   │   ├── SpellCaster.Cast.cs
│   │   │   │   ├── SpellCaster.Channels.cs
│   │   │   │   ├── SpellCaster.cs
│   │   │   │   ├── SpellCaster.Projectiles.cs
│   │   │   │   ├── SpellData.cs
│   │   │   │   ├── SpellDoT.cs
│   │   │   │   ├── SpellEffect.cs
│   │   │   │   ├── SpellStorm.cs
│   │   │   │   ├── SpellSummon.cs
│   │   │   │   ├── SpellTornado.cs
│   │   │   │   └── SpellZone.cs
│   │   │   ├── Fx/
│   │   │   │   ├── CastingCircle.cs
│   │   │   │   ├── SkillFx.cs
│   │   │   │   └── SpellImpactFx.cs
│   │   │   ├── Look/
│   │   │   │   └── SpellLook.cs
│   │   │   ├── Ui/
│   │   │   │   ├── MagicTestMatrix.cs
│   │   │   │   └── MagicWheelUI.cs
│   │   │   └── README.md
│   │   ├── Misc/
│   │   │   ├── KarmaManager.cs
│   │   │   ├── MainMenuController.cs
│   │   │   ├── PalmProjectile.cs
│   │   │   ├── PickupVisualHelper.cs
│   │   │   ├── ReligionManager.cs
│   │   │   ├── SleepManager.cs
│   │   │   ├── SoundManager.cs
│   │   │   ├── UIHelper.cs
│   │   │   └── WifeDonationField.cs
│   │   ├── Models/
│   │   │   ├── Magic/
│   │   │   │   └── MagicWeaponModelBuilder.cs
│   │   │   ├── MapBuilder/
│   │   │   │   ├── MapBuilder.cs
│   │   │   │   ├── MapBuilder.Houses.cs
│   │   │   │   ├── MapBuilder.Mansion.cs
│   │   │   │   ├── MapBuilder.Nature.cs
│   │   │   │   ├── MapBuilder.NPCs.cs
│   │   │   │   ├── MapBuilder.PlayerModels.cs
│   │   │   │   ├── MapBuilder.Police.cs
│   │   │   │   ├── MapBuilder.Restaurants.cs
│   │   │   │   ├── MapBuilder.Stores.cs
│   │   │   │   └── MapBuilder.Vehicles.cs
│   │   │   ├── BossModelBuilder.cs
│   │   │   ├── EnemyModelBuilder.cs
│   │   │   ├── GoblinModelBuilder.cs
│   │   │   ├── HorseModelBuilder.cs
│   │   │   ├── ItemBuilder.cs
│   │   │   ├── PlayerPartMesher.cs
│   │   │   └── WeaponModelBuilder.cs
│   │   ├── Navigation/
│   │   │   └── NavGrid.cs
│   │   ├── NPCs/
│   │   │   ├── CafeBarista.cs
│   │   │   ├── ChefNPC.cs
│   │   │   ├── FishingShopNPC.cs
│   │   │   ├── FriendshipManager.cs
│   │   │   ├── LibrarianNPC.cs
│   │   │   ├── PagodaMonkNPC.cs
│   │   │   ├── PoliceOfficerNPC.cs
│   │   │   ├── PriestNPC.cs
│   │   │   ├── RichManNPC.cs
│   │   │   ├── TaoistPriestNPC.cs
│   │   │   └── WifeNPC.cs
│   │   ├── Opt/
│   │   │   ├── AudioManager.cs
│   │   │   ├── BootInitDeferrer.cs
│   │   │   ├── ChunkDistanceCull.cs
│   │   │   ├── ComponentRegistry.cs
│   │   │   ├── CullManager.cs
│   │   │   ├── NewWorldTestGround.cs
│   │   │   ├── ObjectPooler.cs
│   │   │   ├── PickupAmount.cs
│   │   │   └── WeaponRackStand.cs
│   │   ├── Pets/
│   │   │   ├── GoblinPet.cs
│   │   │   └── PetController.cs
│   │   ├── Player/
│   │   │   ├── Controller/
│   │   │   │   └── ThirdPersonCamera.cs
│   │   │   ├── Races/
│   │   │   │   ├── RaceChangeManager.cs
│   │   │   │   ├── RaceData.cs
│   │   │   │   ├── RaceDatabase.cs
│   │   │   │   ├── RaceDiscoveryPoint.cs
│   │   │   │   ├── RacePassiveManager.cs
│   │   │   │   ├── RaceRig.cs
│   │   │   │   ├── RaceSkillPassiveManager.cs
│   │   │   │   ├── RaceSkillState.cs
│   │   │   │   └── RaceUnlockManager.cs
│   │   │   ├── Stats/
│   │   │   │   ├── ClassData.cs
│   │   │   │   ├── ClassPassiveManager.cs
│   │   │   │   ├── ClassUnlocker.cs
│   │   │   │   ├── LevelUpSystem.cs
│   │   │   │   ├── PlayerStats.cs
│   │   │   │   ├── SkillType.cs
│   │   │   │   ├── SkillXpTracker.cs
│   │   │   │   ├── StatType.cs
│   │   │   │   ├── TalentCatalog.cs
│   │   │   │   └── TalentTracker.cs
│   │   │   ├── CameraFollow.cs
│   │   │   ├── CameraModeSwitch.cs
│   │   │   ├── MobileActionButton.cs
│   │   │   ├── MobileInputController.cs
│   │   │   ├── PlayerController.Animation.cs
│   │   │   ├── PlayerController.Camera.cs
│   │   │   ├── PlayerController.Combat.cs
│   │   │   ├── PlayerController.cs
│   │   │   ├── PlayerController.Interactions.cs
│   │   │   ├── PlayerController.Movement.cs
│   │   │   ├── TouchLookArea.cs
│   │   │   └── VirtualJoystick.cs
│   │   ├── Quests/
│   │   │   ├── QuestManager.cs
│   │   │   └── RandomEventManager.cs
│   │   ├── Shop/
│   │   │   ├── BuffaloDialog.cs
│   │   │   ├── BuffaloShopManager.cs
│   │   │   └── VendorShopManager.cs
│   │   ├── Skills/
│   │   │   └── SkillManager.cs
│   │   ├── Storage/
│   │   │   └── ChestStorageManager.cs
│   │   ├── Tools/
│   │   │   ├── ToolManager.BuildingMenu.cs
│   │   │   ├── ToolManager.cs
│   │   │   ├── ToolManager.Dig.cs
│   │   │   ├── ToolManager.DropThrow.cs
│   │   │   ├── ToolManager.Inventory.cs
│   │   │   ├── ToolManager.Pickup.cs
│   │   │   ├── ToolManager.ToolModels.cs
│   │   │   ├── ToolManager.Weapons.cs
│   │   │   └── TypingMinigame.cs
│   │   ├── UI/
│   │   │   ├── NewWorld/
│   │   │   │   ├── CharacterCreationUI.cs
│   │   │   │   ├── CharacterInfoUI.cs
│   │   │   │   ├── CharacterInfoUI.Equipment.cs
│   │   │   │   ├── CharacterInfoUI.Faith.cs
│   │   │   │   ├── CharacterInfoUI.Inventory.cs
│   │   │   │   ├── CharacterInfoUI.Map.cs
│   │   │   │   ├── CharacterInfoUI.Skills.cs
│   │   │   │   ├── CharacterInfoUI.Stats.cs
│   │   │   │   ├── CompassMinimapHUD.cs
│   │   │   │   ├── ContextPromptUI.cs
│   │   │   │   ├── EnemyHealthBarHUD.cs
│   │   │   │   ├── EquipmentSystem.cs
│   │   │   │   ├── GearCatalog.cs
│   │   │   │   ├── GearData.cs
│   │   │   │   ├── HudCanvas.cs
│   │   │   │   ├── InventoryEquipmentUI.cs
│   │   │   │   ├── ItemDragHandle.cs
│   │   │   │   ├── ItemDropTarget.cs
│   │   │   │   ├── ItemTooltipUI.cs
│   │   │   │   ├── MenuPanelBase.cs
│   │   │   │   ├── NewWorldSystems.cs
│   │   │   │   ├── NpcDialogueUI.cs
│   │   │   │   ├── PlayerBarsHUD.cs
│   │   │   │   ├── RaceStatSheetUI.cs
│   │   │   │   ├── SkillBarHUD.cs
│   │   │   │   ├── TooltipSlot.cs
│   │   │   │   ├── UiAssetCache.cs
│   │   │   │   ├── WeaponDragHandle.cs
│   │   │   │   ├── WeaponDropTarget.cs
│   │   │   │   └── WorldMapUI.cs
│   │   │   ├── FishingUI.cs
│   │   │   ├── GoblinChestMenu.cs
│   │   │   ├── GoblinCommandMenu.cs
│   │   │   ├── InteractionPrompt.cs
│   │   │   ├── PlayerChestMenu.cs
│   │   │   ├── UIManager.cs
│   │   │   ├── UIManager.Endings.cs
│   │   │   ├── UIManager.EventTest.cs
│   │   │   ├── UIManager.Feedback.cs
│   │   │   ├── UIManager.Friendship.cs
│   │   │   ├── UIManager.HUD.cs
│   │   │   ├── UIManager.Library.cs
│   │   │   ├── UIManager.MainMenu.cs
│   │   │   ├── UIManager.Menus.cs
│   │   │   ├── UIManager.SaveLoad.cs
│   │   │   ├── UIManager.Settings.cs
│   │   │   ├── UIManager.Skills.cs
│   │   │   └── UIManager.Tutorial.cs
│   │   └── World/
│   │       ├── Biomes/
│   │       │   ├── BiomeData.cs
│   │       │   ├── BiomeRegistry.cs
│   │       │   └── EnemySpawner.cs
│   │       ├── Chunks/
│   │       │   ├── ChunkCoord.cs
│   │       │   ├── ChunkData.cs
│   │       │   ├── ChunkKey.cs
│   │       │   ├── ChunkObject.cs
│   │       │   └── ChunkSaveManager.cs
│   │       ├── Crafting/
│   │       │   ├── CraftingStation.cs
│   │       │   ├── RecipeData.cs
│   │       │   ├── RecipeDiscovery.cs
│   │       │   └── RecipeRegistry.cs
│   │       ├── Farming/
│   │       │   ├── CropData.cs
│   │       │   ├── CropRegistry.cs
│   │       │   ├── FarmingManager.cs
│   │       │   ├── FarmingZone.cs
│   │       │   └── FarmPlot.cs
│   │       ├── Fishing/
│   │       │   ├── FishData.cs
│   │       │   ├── FishingSpot.cs
│   │       │   ├── FishingSpotPlacer.cs
│   │       │   └── FishRegistry.cs
│   │       ├── Housing/
│   │       │   ├── HomeBuilder.cs
│   │       │   ├── HomeChest.cs
│   │       │   └── HousePlot.cs
│   │       ├── Loot/
│   │       │   ├── ItemData.cs
│   │       │   ├── ItemDatabase.cs
│   │       │   ├── LootContainer.cs
│   │       │   ├── LootDrop.cs
│   │       │   └── LootTable.cs
│   │       ├── Npcs/
│   │       │   ├── FriendshipSimplified.cs
│   │       │   ├── NpcController.cs
│   │       │   ├── NpcRole.cs
│   │       │   └── WorldNpcPlacer.cs
│   │       ├── Poi/
│   │       │   ├── DungeonSystem.cs
│   │       │   ├── FastTravelNode.cs
│   │       │   ├── POIDefinition.cs
│   │       │   ├── POIGenerator.cs
│   │       │   ├── POIRegistry.cs
│   │       │   └── Town.cs
│   │       ├── Streaming/
│   │       │   ├── ColliderRequestRegistry.cs
│   │       │   ├── RenderDistanceController.cs
│   │       │   ├── TerrainChunkCoord.cs
│   │       │   ├── TerrainChunkMeshData.cs
│   │       │   ├── WorldStreamer.ChunkBuild.cs
│   │       │   ├── WorldStreamer.CornerAudit.cs
│   │       │   ├── WorldStreamer.CraterAudit.cs
│   │       │   ├── WorldStreamer.cs
│   │       │   ├── WorldStreamer.Deform.cs
│   │       │   ├── WorldStreamer.FarShell.cs
│   │       │   ├── WorldStreamer.Mesh.cs
│   │       │   ├── WorldStreamer.Props.cs
│   │       │   ├── WorldStreamer.Streaming.cs
│   │       │   └── WorldStreamer.Voxel.cs
│   │       ├── Terrain/
│   │       │   ├── Voxel/
│   │       │   │   ├── VoxelChunkData.cs
│   │       │   │   └── VoxelMesher.cs
│   │       │   ├── ChunkMeshData.cs
│   │       │   ├── ChunkMeshGenerator.cs
│   │       │   ├── ChunkValidator.cs
│   │       │   ├── TerrainDeformer.cs
│   │       │   └── TerrainNoiseGenerator.cs
│   │       ├── CloudBehavior.cs
│   │       ├── FieldManager.cs
│   │       ├── ITornadoCarried.cs
│   │       ├── SteamEffect.cs
│   │       ├── TornadoBehavior.cs
│   │       ├── WaterVolume.cs
│   │       ├── WorldBuilder.Blueprints.cs
│   │       ├── WorldBuilder.BuildingDamage.cs
│   │       ├── WorldBuilder.cs
│   │       ├── WorldBuilder.Environment.cs
│   │       ├── WorldBuilder.Farming.cs
│   │       ├── WorldBuilder.FunctionalBuildings.cs
│   │       ├── WorldBuilder.Lights.cs
│   │       ├── WorldBuilder.NPCs.cs
│   │       ├── WorldBuilder.Persistence.cs
│   │       ├── WorldBuilder.RockMining.cs
│   │       ├── WorldBuilder.TreeChop.cs
│   │       └── WorldBuilder.WorldEnv.cs
│   ├── Settings/
│   │   ├── DefaultVolumeProfile.asset
│   │   ├── Mobile_Renderer.asset
│   │   ├── Mobile_RPAsset.asset
│   │   ├── PC_Renderer.asset
│   │   ├── PC_RPAsset.asset
│   │   ├── SampleSceneProfile.asset
│   │   └── UniversalRenderPipelineGlobalSettings.asset
│   ├── Shaders/
│   │   └── TerrainLayered.shader
│   ├── TextMesh Pro/   [33 files]
│   ├── texture/
│   │   ├── dirt_texture.png
│   │   ├── fertilize.png
│   │   └── peashooter_seed.png
│   ├── TutorialInfo/
│   │   ├── Icons/
│   │   │   └── URP.png
│   │   ├── Scripts/
│   │   │   ├── Editor/
│   │   │   │   └── ReadmeEditor.cs
│   │   │   └── Readme.cs
│   │   └── Layout.wlt
│   ├── xoanvnmexel/
│   │   └── XoanVnmexelStandard.ttf
│   ├── DefaultVolumeProfile.asset
│   └── UniversalRenderPipelineGlobalSettings.asset
├── Packages/
│   ├── manifest.json
│   └── packages-lock.json
├── ProjectSettings/   [27 files]
├── tools/
│   ├── StaticChecks.ps1
│   └── Write-Tree.ps1
├── .gitignore
├── .vsconfig
├── __azurite_db_blob__.json
├── __azurite_db_blob_extent__.json
├── __azurite_db_queue__.json
├── __azurite_db_queue_extent__.json
├── __azurite_db_table__.json
├── AGENTS.md
├── ARCHITECTURE.md
├── Assembly-CSharp.csproj
├── Assembly-CSharp-Editor.csproj
├── AzuriteConfig
├── country life.sln
├── GAME_DESCRIPTION.md
├── GAME_DESCRIPTION.txt
├── game-design.md
├── GAMEPLAY_IMPROVEMENT_PLAN.txt
├── HANDOFF-1in.md
├── InputSystem_Actions.inputactions
├── magic-skills.md
├── PROGRESS.md
├── Readme.asset
├── THINKING.md
└── TREE.md
```
