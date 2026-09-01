using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoSingleton<SaveManager>
{
    private GameManager _gameManager;
    private ToolManager _toolManager;
    private WorldBuilder _worldBuilder;
    private UIManager _uiManager;
    private QuestManager _questManager;

    public void Initialize(GameManager gameManager, ToolManager toolManager, WorldBuilder worldBuilder, UIManager uiManager, QuestManager questManager)
    {
        _gameManager = gameManager;
        _toolManager = toolManager;
        _worldBuilder = worldBuilder;
        _uiManager = uiManager;
        _questManager = questManager;
    }

    public void SaveGame()
    {
        SaveGame(PlayerPrefs.GetInt("LastSaveSlot", 0));
    }

    public void SaveGame(int slot)
    {
        if (_gameManager == null || _toolManager == null || _worldBuilder == null)
            return;

        var player = GameManager.Instance?.Player;
        var data = new SaveData
        {
            time = new TimeData
            {
                currentDay = _gameManager.CurrentDay,
                timeOfDay = _gameManager.TimeOfDay,
                playedSeconds = _gameManager.PlayTime
            },
            player = new PlayerData
            {
                position = player != null ? player.transform.position : Vector3.zero,
                rotationY = player != null ? player.transform.eulerAngles.y : 0f,
                hp = player != null ? player.HP : 100,
                stamina = player != null ? player.Stamina : 1000f,
                money = player != null ? player.Money : 0,
                gender = MapBuilder.ActiveGender
            },
            inventory = _toolManager.GetInventorySave(),
            fields = _worldBuilder.GetAllFieldsAsSave(),
            buildings = _worldBuilder.GetAllBuildingsAsSave(),
            mansionBlueprints = _worldBuilder.GetMansionBlueprintsAsSave(),
            quests = _questManager?.GetQuestSaves(),
            richSecret = RichManNPC.Instance != null && RichManNPC.Instance.Discovered,
            wifeStateJson = WifeNPC.Instance != null ? WifeNPC.Instance.SerializeState() : "",
            unlockedBlueprints = _worldBuilder.GetUnlockedBlueprintsAsSave(),
            immigrantBuiltMask = _worldBuilder.GetImmigrantBuiltArray(),
            immigrantNextIndex = _worldBuilder.GetImmigrantNextIndex(),
            immigrantVillagePlaced = _worldBuilder.IsImmigrantVillagePlacedState(),
            immigrantArrived = _worldBuilder.GetImmigrantArrived(),
            immigrantVillagers = _worldBuilder.GetVillagerSaves(),
            karmaCurrent = KarmaManager.Instance != null ? KarmaManager.Instance.CurrentKarma : 5f,
            karmaMax = KarmaManager.Instance != null ? KarmaManager.Instance.MaxKarma : 5f,
            skillStateJson = SkillManager.Instance != null ? SkillManager.Instance.SerializeState() : "",
            friendshipStateJson = FriendshipManager.Instance != null ? FriendshipManager.Instance.SerializeState() : "",
            fishingRodJson = FishingProgression.Instance != null ? FishingProgression.Instance.SerializeState() : "",
            chestStorageJson = ChestStorageManager.Instance != null ? ChestStorageManager.Instance.SerializeState() : "",
            goblinStorage = GoblinPet.Instance != null ? GoblinPet.Instance.GetStorageSaveItems() : null,
            goblinHeldSeed = GoblinPet.Instance != null ? GoblinPet.Instance.HeldSeedType : "",
            goblinCarriedCrop = GoblinPet.Instance != null ? GoblinPet.Instance.CarriedCrop : ""
        };

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSaveFilePath(slot), json);
        PlayerPrefs.SetInt("LastSaveSlot", slot);
        _uiManager?.ShowMessage(Localization.T("Đã lưu trò chơi!"), 2f);
    }

    public bool LoadGame()
    {
        return LoadGame(PlayerPrefs.GetInt("LastSaveSlot", 0));
    }

    public bool LoadGame(int slot)
    {
        var path = GetSaveFilePath(slot);
        if (!File.Exists(path))
        {
            _uiManager?.ShowMessage(Localization.T("Không tìm thấy file lưu!"), 2f);
            return false;
        }

        var json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
        {
            _uiManager?.ShowMessage(Localization.T("Không đọc được file lưu!"), 2f);
            return false;
        }

        if (_worldBuilder != null)
            _worldBuilder.ClearPersistentData();

        if (_gameManager != null)
        {
            var time = data.time ?? new TimeData { currentDay = 1, timeOfDay = 8f, playedSeconds = 0f };
            _gameManager.CurrentDay = time.currentDay;
            _gameManager.TimeOfDay = time.timeOfDay;
            _gameManager.PlayTime = time.playedSeconds;
            _gameManager.LoadGame();
            _gameManager.SetTimeOfDay(time.timeOfDay);
        }

        if (GameManager.Instance.Player != null)
        {
            var player = data.player ?? new PlayerData
            {
                position = GameManager.Instance.Player.transform.position,
                rotationY = 0f,
                hp = 100,
                stamina = 1000f,
                money = 0
            };
            GameManager.Instance.Player.transform.position = player.position;
            GameManager.Instance.Player.transform.rotation = Quaternion.Euler(0f, player.rotationY, 0f);
            GameManager.Instance.Player.HP = player.hp;
            GameManager.Instance.Player.Stamina = player.stamina;
            GameManager.Instance.Player.Money = player.money;
            MapBuilder.ActiveGender = player.gender;
            GameManager.Instance.Player.ApplyGender();
        }

        _toolManager?.LoadInventorySave(data.inventory);
        _worldBuilder?.LoadFieldsFromSave(data.fields);
        _worldBuilder?.LoadBuildingsFromSave(data.buildings);
        if (GoblinPet.Instance != null)
        {
            GoblinPet.Instance.LoadStorageSaveItems(data.goblinStorage);
            GoblinPet.Instance.LoadHeldSave(data.goblinHeldSeed, data.goblinCarriedCrop);
        }
        _worldBuilder?.LoadMansionBlueprintsFromSave(data.mansionBlueprints);
        _worldBuilder?.LoadUnlockedBlueprints(data.unlockedBlueprints);
        _worldBuilder?.LoadImmigrantVillageFromSave(data.immigrantBuiltMask, data.immigrantNextIndex, data.immigrantVillagePlaced);
        if (data.immigrantArrived)
            _worldBuilder?.RestoreImmigrantArrival();
        if (data.immigrantVillagers != null && data.immigrantVillagers.Count > 0)
            _worldBuilder?.RestoreSavedVillagers(data.immigrantVillagers);
        if (RichManNPC.Instance != null && json.Contains("\"richSecret\""))
            RichManNPC.Instance.SetDiscovered(data.richSecret);
        _questManager?.LoadQuestSaves(data.quests);
        if (WifeNPC.Instance != null)
        {
            if (string.IsNullOrEmpty(data.wifeStateJson))
                WifeNPC.Instance.ResetForNewGame();
            else
                WifeNPC.Instance.DeserializeState(data.wifeStateJson);
        }
        if (KarmaManager.Instance != null)
        {
            float kMax = data.karmaMax > 0f ? data.karmaMax : 5f;
            float kCur = data.karmaMax > 0f ? Mathf.Clamp(data.karmaCurrent, 0f, kMax) : kMax;
            KarmaManager.Instance.LoadSaveData(new KarmaManager.KarmaSaveData
            {
                currentKarma = kCur,
                maxKarma = kMax
            });
        }
        if (SkillManager.Instance != null && !string.IsNullOrEmpty(data.skillStateJson))
            SkillManager.Instance.DeserializeState(data.skillStateJson);

        if (FriendshipManager.Instance != null && !string.IsNullOrEmpty(data.friendshipStateJson))
            FriendshipManager.Instance.DeserializeState(data.friendshipStateJson);

        if (FishingProgression.Instance != null && !string.IsNullOrEmpty(data.fishingRodJson))
            FishingProgression.Instance.DeserializeState(data.fishingRodJson);

        if (ChestStorageManager.Instance != null && !string.IsNullOrEmpty(data.chestStorageJson))
            ChestStorageManager.Instance.DeserializeState(data.chestStorageJson);

        var spawner = Object.FindAnyObjectByType<LivestockSpawner>();
        if (spawner != null) spawner.Restart();

        GameManager.Instance?.ShowMainMenu(false);
        _uiManager?.ShowAllGameUI(true);
        _uiManager?.ShowPauseMenu(false);
        _uiManager?.ShowMessage(Localization.T("Đã tải trò chơi!"), 2f);
        if (GameManager.Instance?.Player != null)
            _uiManager?.UpdatePlayerHud(GameManager.Instance.Player.HP, GameManager.Instance.Player.MaxHP, GameManager.Instance.Player.Stamina, GameManager.Instance.Player.MaxStamina, GameManager.Instance.Player.Money);
        PlayerPrefs.SetInt("LastSaveSlot", slot);
        return true;
    }

    public bool GetSlotInfo(int slot, out int currentDay, out float timeOfDay, out float playedSeconds)
    {
        currentDay = 1;
        timeOfDay = 8f;
        playedSeconds = 0f;
        var path = GetSaveFilePath(slot);
        if (!File.Exists(path))
            return false;

        var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        if (data == null || data.time == null)
            return false;

        currentDay = data.time.currentDay;
        timeOfDay = data.time.timeOfDay;
        playedSeconds = data.time.playedSeconds;
        return true;
    }

    public int GetLastSaveSlot()
    {
        return PlayerPrefs.GetInt("LastSaveSlot", 0);
    }

    private string GetSaveFilePath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, "savegame_" + slot.ToString() + ".json");
    }

    [System.Serializable]
    private class SaveData
    {
        public TimeData time;
        public PlayerData player;
        public ToolManager.InventorySlotSave[] inventory;
        public WorldBuilder.FieldSaveData[] fields;
        public WorldBuilder.BuildingSaveData[] buildings;
        public WorldBuilder.MansionBlueprintSaveData[] mansionBlueprints;
        public List<QuestManager.QuestSave> quests;
        public bool richSecret;
        public string wifeStateJson;
        public string[] unlockedBlueprints;
        public bool[] immigrantBuiltMask;
        public int immigrantNextIndex;
        public bool immigrantVillagePlaced;
        public bool immigrantArrived;
        public List<WorldBuilder.VillagerSaveData> immigrantVillagers;
        public float karmaCurrent;
        public float karmaMax;
        public string skillStateJson;
        public string friendshipStateJson;
        public string fishingRodJson;
        public string chestStorageJson;
        public List<GoblinPet.GoblinStorageSaveItem> goblinStorage;
        public string goblinHeldSeed;
        public string goblinCarriedCrop;
    }

    [System.Serializable]
    private class TimeData
    {
        public int currentDay;
        public float timeOfDay;
        public float playedSeconds;
    }

    [System.Serializable]
    private class PlayerData
    {
        public Vector3 position;
        public float rotationY;
        public int hp;
        public float stamina;
        public long money;
        public PlayerGender gender;
    }
}
