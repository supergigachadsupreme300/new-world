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
        if (_gameManager == null || _toolManager == null || _worldBuilder == null)
            return;

        var data = new SaveData
        {
            time = new TimeData
            {
                currentDay = _gameManager.CurrentDay,
                timeOfDay = _gameManager.TimeOfDay
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
            gunAmmo = _toolManager.GetGunAmmo(),
            fields = _worldBuilder.GetAllFieldsAsSave(),
            buildings = _worldBuilder.GetAllBuildingsAsSave(),
            quest = _questManager?.GetQuestSave()
        };

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSaveFilePath(), json);
        _uiManager?.ShowMessage("Đã lưu game thành công!", 2f);
    }

    public void LoadGame()
    {
        var path = GetSaveFilePath();
        if (!File.Exists(path))
        {
            _uiManager?.ShowMessage("Không tìm thấy file save!", 2f);
            return;
        }

        var json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
        {
            _uiManager?.ShowMessage("Không thể đọc file save!", 2f);
            return;
        }

        if (_worldBuilder != null)
        {
            _worldBuilder.ResetWorld();
            _worldBuilder.CreateWorld();
        }

        if (_gameManager != null)
        {
            _gameManager.CurrentDay = data.time.currentDay;
            _gameManager.TimeOfDay = data.time.timeOfDay;
            _gameManager.LoadGame();
            _gameManager.SetTimeOfDay(data.time.timeOfDay);
        }

        if (GameManager.Instance.Player != null)
        {
            GameManager.Instance.Player.transform.position = data.player.position;
            GameManager.Instance.Player.transform.rotation = Quaternion.Euler(0f, data.player.rotationY, 0f);
            GameManager.Instance.Player.HP = data.player.hp;
            GameManager.Instance.Player.Stamina = data.player.stamina;
            GameManager.Instance.Player.Money = data.player.money;
        }

        _toolManager?.LoadInventorySave(data.inventory);
        _toolManager?.SetGunAmmo(data.gunAmmo);
        _worldBuilder?.LoadFieldsFromSave(data.fields);
        _worldBuilder?.LoadBuildingsFromSave(data.buildings);
        _questManager?.LoadQuestSave(data.quest);

        GameManager.Instance?.ShowMainMenu(false);
        _uiManager?.ShowAllGameUI(true);
        _uiManager?.ShowPauseMenu(false);
        _uiManager?.ShowMessage("Đã load game!", 2f);
        if (GameManager.Instance?.Player != null)
            _uiManager?.UpdatePlayerHud(GameManager.Instance.Player.HP, GameManager.Instance.Player.MaxHP, GameManager.Instance.Player.Stamina, GameManager.Instance.Player.MaxStamina, GameManager.Instance.Player.Money);
    }

    private string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, "savegame.json");
    }

    [System.Serializable]
    private class SaveData
    {
        public TimeData time;
        public PlayerData player;
        public ToolManager.InventorySlotSave[] inventory;
        public int gunAmmo;
        public WorldBuilder.FieldSaveData[] fields;
        public WorldBuilder.BuildingSaveData[] buildings;
        public QuestManager.QuestSave quest;
    }

    [System.Serializable]
    private class TimeData
    {
        public int currentDay;
        public float timeOfDay;
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
