using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    private GameManager _gameManager;
    private ToolManager _toolManager;
    private WorldBuilder _worldBuilder;
    private UIManager _uiManager;
    private QuestManager _questManager;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

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

        WifeNPC.Instance?.SaveState();

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
                position = GameManager.Instance.Player != null ? GameManager.Instance.Player.transform.position : Vector3.zero,
                rotationY = GameManager.Instance.Player != null ? GameManager.Instance.Player.transform.eulerAngles.y : 0f,
                hp = GameManager.Instance.Player != null ? GameManager.Instance.Player.HP : 100,
                stamina = GameManager.Instance.Player != null ? GameManager.Instance.Player.Stamina : 1000f,
                money = GameManager.Instance.Player != null ? GameManager.Instance.Player.Money : 0
            },
            inventory = _toolManager.GetInventorySave(),
            fields = _worldBuilder.GetAllFieldsAsSave(),
            buildings = _worldBuilder.GetAllBuildingsAsSave(),
            mansionBlueprints = _worldBuilder.GetMansionBlueprintsAsSave(),
            quests = _questManager?.GetQuestSaves(),
            richSecret = RichManNPC.Instance != null && RichManNPC.Instance.Discovered,
            wifeStateJson = WifeNPC.Instance != null ? WifeNPC.Instance.SerializeState() : ""
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
        }

        _toolManager?.LoadInventorySave(data.inventory);
        _worldBuilder?.LoadFieldsFromSave(data.fields);
        _worldBuilder?.LoadBuildingsFromSave(data.buildings);
        _worldBuilder?.LoadMansionBlueprintsFromSave(data.mansionBlueprints);
        if (RichManNPC.Instance != null && json.Contains("\"richSecret\""))
            RichManNPC.Instance.SetDiscovered(data.richSecret);
        _questManager?.LoadQuestSaves(data.quests);
        if (WifeNPC.Instance != null)
        {
            if (string.IsNullOrEmpty(data.wifeStateJson))
                WifeNPC.Instance.LoadState();
            else
                WifeNPC.Instance.DeserializeState(data.wifeStateJson);
        }

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
    }
}
