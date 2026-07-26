using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private readonly List<QuestSave> _quests = new List<QuestSave>();
    private int _lastDay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame || GameManager.Instance.GamePaused)
            return;

        bool changed = CheckTimedQuests();
        changed |= CheckDayChange();
        if (changed)
            UpdateQuestUI();
    }

    public void InitializeQuests()
    {
        if (_quests.Count > 0)
            return;

        _lastDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;

        AddStoryQuestsForDay(_lastDay);
        GenerateDailyQuests(_lastDay);
        UpdateQuestUI();
    }

    private bool CheckDayChange()
    {
        if (GameManager.Instance == null)
            return false;

        int currentDay = GameManager.Instance.CurrentDay;
        if (currentDay == _lastDay)
            return false;

        _lastDay = currentDay;
        AddStoryQuestsForDay(currentDay);
        RefreshDailyQuests(currentDay);
        return true;
    }

    private bool CheckTimedQuests()
    {
        if (GameManager.Instance == null)
            return false;

        bool changed = false;
        float now = Time.time;

        for (int i = 0; i < _quests.Count; i++)
        {
            var q = _quests[i];
            if (q.Completed || q.Failed || q.TimeLimit <= 0f)
                continue;

            float elapsed = now - q.TimeStarted;
            if (elapsed >= q.TimeLimit)
            {
                q.Failed = true;
                changed = true;
                GameManager.Instance?.UIManager?.ShowMessage($"{q.Name} failed!", 2f);
            }
        }

        return changed;
    }

    private void AddStoryQuestsForDay(int day)
    {
        if (day >= 1) AddIfMissing(CreateStoryQuest("Welcome to the Farm", "wheat", 10, 50, "Plant and harvest your first wheat.", 1));
        if (day >= 3) AddIfMissing(CreateStoryQuest("First Harvest", "wheat", 50, 150, "Become a real farmer.", 3));
        if (day >= 5) AddIfMissing(CreateStoryQuest("Defend Your Land", "enemies", 10, 300, "Prove you can protect your farm.", 5));
        if (day >= 8) AddIfMissing(CreateStoryQuest("Green Thumb", "wheat", 150, 400, "Your crops are legendary.", 8));
        if (day >= 10) AddIfMissing(CreateStoryQuest("Build an Empire", "money_earned", 50000, 750, "Accumulate real wealth.", 10));
        if (day >= 12) AddIfMissing(CreateStoryQuest("Monster Slayer", "enemies", 30, 600, "Clear out the infestation.", 12));
        if (day >= 15) AddIfMissing(CreateStoryQuest("The Final Stand", "enemies", 50, 1500, "Face the ultimate threat.", 15));
        if (day >= 18) AddIfMissing(CreateStoryQuest("Tycoon", "money_earned", 200000, 3000, "Become the richest farmer alive.", 18));
    }

    private void AddIfMissing(QuestSave quest)
    {
        foreach (var q in _quests)
        {
            if (q.Name == quest.Name)
                return;
        }
        _quests.Add(quest);
    }

    private void GenerateDailyQuests(int day)
    {
        var pool = new List<QuestSave>
        {
            CreateDailyQuest("Quick Harvest", "wheat", 25, 100, "Harvest 25 wheat today."),
            CreateDailyQuest("Bumper Crop", "wheat", 60, 200, "Harvest 60 wheat today."),
            CreateDailyQuest("Pest Control", "enemies", 5, 150, "Kill 5 monsters today."),
            CreateDailyQuest("Monster Hunt", "enemies", 15, 350, "Kill 15 monsters today."),
            CreateDailyQuest("Side Hustle", "money_earned", 5000, 120, "Earn 5,000 gold today."),
            CreateDailyQuest("Big Earnings", "money_earned", 15000, 300, "Earn 15,000 gold today."),
            CreateDailyQuest("Wheat Streak", "wheat", 100, 250, "Harvest 100 wheat today."),
            CreateDailyQuest("Exterminator", "enemies", 25, 500, "Kill 25 monsters today."),
        };

        int pickCount = Mathf.Min(2, pool.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = Random.Range(i, pool.Count);
            var temp = pool[i];
            pool[i] = pool[idx];
            pool[idx] = temp;
        }

        for (int i = 0; i < pickCount; i++)
        {
            var q = pool[i];
            q.Name = $"[Daily] {q.Name}";
            _quests.Add(q);
        }
    }

    private void RefreshDailyQuests(int day)
    {
        _quests.RemoveAll(q => q.QuestType == "daily");
        GenerateDailyQuests(day);
    }

    public void AddProgress(string target, int amount)
    {
        if (string.IsNullOrEmpty(target))
            return;

        bool anyJustCompleted = false;
        foreach (var quest in _quests)
        {
            if (quest.Completed || quest.Failed)
                continue;

            if (quest.QuestType == "timed" && quest.TimeLimit > 0f)
            {
                float elapsed = Time.time - quest.TimeStarted;
                if (elapsed >= quest.TimeLimit)
                {
                    quest.Failed = true;
                    continue;
                }
            }

            if (quest.Target == target)
            {
                quest.Progress += amount;
                if (quest.Progress >= quest.Count)
                {
                    quest.Progress = quest.Count;
                    quest.Completed = true;
                    anyJustCompleted = true;
                }
            }
        }

        UpdateQuestUI();

        if (anyJustCompleted)
            AwardCompleted();
        if (AllMainQuestsCompleted())
            GameManager.Instance?.RequestHappyEnding();
    }

    private void AwardCompleted()
    {
        long total = 0;
        foreach (var q in _quests)
        {
            if (q.Completed && !q.RewardClaimed)
            {
                total += q.RewardMoney;
                q.RewardClaimed = true;
            }
        }
        if (total > 0 && GameManager.Instance?.Player != null)
        {
            GameManager.Instance.Player.Money += total;
            var msg = $"Quest complete! Received {total}g!";
            GameManager.Instance?.UIManager?.ShowMessage(msg, 3f);
        }
    }

    public void LoadQuestSaves(List<QuestSave> saved)
    {
        if (saved == null || saved.Count == 0)
            return;

        _quests.Clear();
        foreach (var q in saved)
            _quests.Add(q);
        if (GameManager.Instance != null)
            _lastDay = GameManager.Instance.CurrentDay;
        UpdateQuestUI();
    }

    public List<QuestSave> GetQuestSaves()
    {
        return new List<QuestSave>(_quests);
    }

    private bool AllMainQuestsCompleted()
    {
        foreach (var quest in _quests)
        {
            if (quest.QuestType == "story" && !quest.Completed)
                return false;
        }
        bool anyStory = false;
        foreach (var quest in _quests)
        {
            if (quest.QuestType == "story")
                anyStory = true;
        }
        return anyStory;
    }

    private QuestSave CreateStoryQuest(string name, string target, int count, int reward, string description, int requiredDay)
    {
        return new QuestSave
        {
            Name = name,
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            RewardClaimed = false,
            Completed = false,
            QuestType = "story",
            TimeLimit = 0f,
            TimeStarted = 0f,
            RequiredDay = requiredDay,
            Description = description,
            Failed = false
        };
    }

    private QuestSave CreateDailyQuest(string name, string target, int count, int reward, string description)
    {
        return new QuestSave
        {
            Name = name,
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            RewardClaimed = false,
            Completed = false,
            QuestType = "daily",
            TimeLimit = 0f,
            TimeStarted = 0f,
            RequiredDay = 0,
            Description = description,
            Failed = false
        };
    }

    public QuestSave CreateTimedQuest(string name, string target, int count, int reward, float timeLimit, string description)
    {
        return new QuestSave
        {
            Name = $"[Timed] {name}",
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            RewardClaimed = false,
            Completed = false,
            QuestType = "timed",
            TimeLimit = timeLimit,
            TimeStarted = Time.time,
            RequiredDay = 0,
            Description = description,
            Failed = false
        };
    }

    public void AddTimedQuest(QuestSave timedQuest)
    {
        AddIfMissing(timedQuest);
        UpdateQuestUI();
    }

    private void UpdateQuestUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.UIManager == null)
            return;

        string hud = "";
        string panel = "";

        var storyQuests = new List<QuestSave>();
        var dailyQuests = new List<QuestSave>();
        var timedQuests = new List<QuestSave>();

        foreach (var q in _quests)
        {
            if (q.QuestType == "story") storyQuests.Add(q);
            else if (q.QuestType == "daily") dailyQuests.Add(q);
            else if (q.QuestType == "timed") timedQuests.Add(q);
            else storyQuests.Add(q);
        }

        if (storyQuests.Count > 0)
        {
            panel += "--- Story Quests ---\n";
            foreach (var q in storyQuests)
            {
                string status = GetQuestStatusString(q);
                panel += $"{q.Name}: {status}\n";
                if (hud.Length < 200)
                    hud += $"{q.Name}: {status}\n";
            }
        }

        if (dailyQuests.Count > 0)
        {
            panel += "\n--- Daily Quests ---\n";
            foreach (var q in dailyQuests)
            {
                string status = GetQuestStatusString(q);
                panel += $"{q.Name}: {status}\n";
                if (hud.Length < 200)
                    hud += $"{q.Name}: {status}\n";
            }
        }

        if (timedQuests.Count > 0)
        {
            panel += "\n--- Timed Quests ---\n";
            foreach (var q in timedQuests)
            {
                string status = GetQuestStatusString(q);
                panel += $"{q.Name}: {status}\n";
                if (hud.Length < 200)
                    hud += $"{q.Name}: {status}\n";
            }
        }

        hud = hud.TrimEnd('\n');
        panel = panel.TrimEnd('\n');

        GameManager.Instance.UIManager.UpdateQuestHud(hud);
        GameManager.Instance.UIManager.UpdateQuestPanelText(panel);
    }

    private string GetQuestStatusString(QuestSave q)
    {
        if (q.Failed)
            return "FAILED";
        if (q.Completed)
            return "DONE";

        if (q.QuestType == "timed" && q.TimeLimit > 0f)
        {
            float elapsed = Time.time - q.TimeStarted;
            float remaining = Mathf.Max(0f, q.TimeLimit - elapsed);
            int mins = Mathf.FloorToInt(remaining / 60f);
            int secs = Mathf.FloorToInt(remaining % 60f);
            return $"{q.Progress}/{q.Count} [{mins}:{secs:D2}]";
        }

        return $"{q.Progress}/{q.Count}";
    }

    [System.Serializable]
    public class QuestSave
    {
        public string Name;
        public string Target;
        public int Count;
        public int Progress;
        public int RewardMoney;
        public bool RewardClaimed;
        public bool Completed;
        public string QuestType;
        public float TimeLimit;
        public float TimeStarted;
        public int RequiredDay;
        public string Description;
        public bool Failed;
    }
}
