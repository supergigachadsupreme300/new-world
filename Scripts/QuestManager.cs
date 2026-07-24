using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private readonly List<QuestSave> _quests = new List<QuestSave>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitializeQuests()
    {
        if (_quests.Count > 0)
            return;

        _quests.Add(CreateQuest("Harvest wheat", "wheat", 100, 250));
        _quests.Add(CreateQuest("Slay monsters", "enemies", 30, 500));
        _quests.Add(CreateQuest("Earn coins", "money_earned", 100000, 1000));
        UpdateQuestUI();
    }

    public void AddProgress(string target, int amount)
    {
        if (string.IsNullOrEmpty(target))
            return;

        bool anyJustCompleted = false;
        foreach (var quest in _quests)
        {
            if (quest.Completed)
                continue;

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
        if (AllQuestsCompleted())
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
        UpdateQuestUI();
    }

    public List<QuestSave> GetQuestSaves()
    {
        return new List<QuestSave>(_quests);
    }

    private bool AllQuestsCompleted()
    {
        foreach (var quest in _quests)
        {
            if (!quest.Completed)
                return false;
        }
        return _quests.Count > 0;
    }

    private QuestSave CreateQuest(string name, string target, int count, int reward)
    {
        return new QuestSave
        {
            Name = name,
            Target = target,
            Count = count,
            Progress = 0,
            RewardMoney = reward,
            RewardClaimed = false,
            Completed = false
        };
    }

    private void UpdateQuestUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.UIManager == null)
            return;

        string hud = "";
        string panel = "";
        for (int i = 0; i < _quests.Count; i++)
        {
            var q = _quests[i];
            string status = q.Completed ? "Done" : $"{q.Progress}/{q.Count}";
            hud += $"{i + 1}. {q.Name}: {status}\n";
            panel += $"{i + 1}. {q.Name}: {status}";
            if (i < _quests.Count - 1)
                panel += "\n";
        }
        hud = hud.TrimEnd('\n');

        GameManager.Instance.UIManager.UpdateQuestHud(hud);
        GameManager.Instance.UIManager.UpdateQuestPanelText(hud);
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
    }
}
