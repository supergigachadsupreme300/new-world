using System;
using UnityEngine;

/// <summary>
/// A world fishing spot (planning Task 6.2, game-design §5.2 "Fishing spots marked on world
/// map"). Placed at water biome locations by <see cref="FishingSpotPlacer"/> (or by hand).
/// When the player stands in its trigger it periodically attempts a cast; a successful catch
/// awards a rolled species (from <see cref="FishRegistry"/>) to the player's tool inventory,
/// where fish act as consumables / sell items. A generated bobber + water-ring stand in until
/// real art arrives.
/// </summary>
public class FishingSpot : MonoBehaviour
{
    [Header("Spot")]
    public string SpotId = "fishing_spot";
    [Tooltip("Declared biome this spot belongs to (used by the placer).")]
    public BiomeType Biome = BiomeType.Plains;
    [Tooltip("Auto-catch cadence.")]
    public float CastInterval = 4f;
    [Tooltip("Multiplies catch success chance.")]
    [Range(0.1f, 1f)] public float LuckFactor = 1f;
    public bool RequiresPlayerInside = true;

    private GameObject _bobber;
    private float _timer;
    private float _catchProgress;

    private static readonly float BaseCatchChance = 0.6f;

    private void Update()
    {
        if (RequiresPlayerInside && !IsPlayerInside()) return;
        if (GameManager.Instance == null || !GameManager.Instance.InGame) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = CastInterval;
        _catchProgress += 1f;
        if (_catchProgress < 2f) return; // two casts before an outcome
        _catchProgress = 0f;

        var fish = FishRegistry.RollFish();
        if (fish == null) return;

        float chance = BaseCatchChance * LuckFactor;
        if (UnityEngine.Random.value > chance)
        {
            ShowMessage("No luck...");
            return;
        }

        var tm = ToolManager.Instance;
        if (tm == null) return;
        if (!tm.AddItem(fish.ToolItemId, 1))
        {
            ShowMessage("Inventory full!");
            return;
        }
        ShowMessage(fish.DisplayName);
        QuestManager.Instance?.AddProgress("fish_catch", 1);
        SkillManager.Instance?.AddXP(SkillManager.Track.Fishing, SkillManager.FishingXpPerCatch);
    }

    private bool IsPlayerInside()
    {
        var player = GameManager.Instance?.Player;
        return player != null &&
            Vector3.Distance(player.transform.position, transform.position) <= 2.2f;
    }

    private void ShowMessage(string text)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.UIManager?.ShowMessage(Localization.T(text), 1.6f);
    }

    /// <summary>Build the spot's trigger + simple bobber visual. Called by the placer on load.</summary>
    public void BuildSpot()
    {
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = false;
        col.radius = 0.6f;

        _bobber = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _bobber.name = "FishBobber";
        _bobber.transform.SetParent(transform, false);
        _bobber.transform.localScale = Vector3.one * 0.25f;
        _bobber.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        var r = _bobber.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(1f, 0.4f, 0.25f);
        Destroy(_bobber.GetComponent<Collider>());
    }
}