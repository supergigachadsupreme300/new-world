using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private float _autoTimer;
    private float _autoMin = 600f;
    private float _autoMax = 900f;

    private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
    private bool _eventInProgress;
    private UIManager _uiManager;

    private readonly List<RandomEvent> _events = new List<RandomEvent>();

    public int EventCount => _events.Count;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RegisterEvents();
    }

    void Start()
    {
        _autoTimer = UnityEngine.Random.Range(_autoMin, _autoMax);
    }

    public void Initialize(UIManager uiManager)
    {
        _uiManager = uiManager;
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame || GameManager.Instance.GamePaused)
            return;
        if (CutsceneManager.Instance != null && CutsceneManager.Instance.IsActive)
            return;

        _autoTimer -= Time.deltaTime;
        if (_autoTimer <= 0f)
        {
            _autoTimer = UnityEngine.Random.Range(_autoMin, _autoMax);
            TryAutoTrigger();
        }
    }

    // ── Event Registration ──

    private void RegisterEvents()
    {
        // BASIC (Quest 1: Harvest wheat)
        _events.Add(new RandomEvent
        {
            Name = "Bountiful Harvest",
            Description = "All your crops grow one stage!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1800f,
            Effect = EffectBountifulHarvest
        });
        _events.Add(new RandomEvent
        {
            Name = "Lucky Find",
            Description = "A gold coin appears on the ground!",
            Tier = 0,
            Weight = 4f,
            Cooldown = 600f,
            Effect = EffectLuckyFind
        });
        _events.Add(new RandomEvent
        {
            Name = "Stamina Refresh",
            Description = "You feel revitalized!",
            Tier = 0,
            Weight = 4f,
            Cooldown = 600f,
            Effect = EffectStaminaRefresh
        });
        _events.Add(new RandomEvent
        {
            Name = "Healing Spring",
            Description = "Your wounds are healed!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 900f,
            Effect = EffectHealingSpring
        });
        _events.Add(new RandomEvent
        {
            Name = "Free Seeds",
            Description = "Seeds fall from the sky!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 900f,
            Effect = EffectFreeSeeds
        });
        _events.Add(new RandomEvent
        {
            Name = "Pest Invasion",
            Description = "Pests are eating your crops!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectPestInvasion
        });
        _events.Add(new RandomEvent
        {
            Name = "Drought",
            Description = "The sun dries up all your fields!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectDrought
        });
        _events.Add(new RandomEvent
        {
            Name = "Strange Noises",
            Description = "You hear strange sounds nearby...",
            Tier = 0,
            Weight = 4f,
            Cooldown = 600f,
            Effect = EffectStrangeNoises
        });
        _events.Add(new RandomEvent
        {
            Name = "Crop Disease",
            Description = "A disease spreads through your crops!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectCropDisease
        });
        _events.Add(new RandomEvent
        {
            Name = "Fireflies",
            Description = "Fireflies dance around you!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 600f,
            Effect = EffectFireflies
        });
        _events.Add(new RandomEvent
        {
            Name = "Weed Growth",
            Description = "Weeds overtake some crops!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 900f,
            Effect = EffectWeedGrowth
        });
        _events.Add(new RandomEvent
        {
            Name = "Stamina Drain",
            Description = "You feel exhausted!",
            Tier = 0,
            Weight = 2f,
            Cooldown = 600f,
            Effect = EffectStaminaDrain
        });

        // ADVANCED (Quest 2: Slay monsters)
        _events.Add(new RandomEvent
        {
            Name = "Enemy Raid",
            Description = "Enemies are converging on your position!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1500f,
            Effect = EffectEnemyRaid
        });
        _events.Add(new RandomEvent
        {
            Name = "Storm Damage",
            Description = "A storm damages your buildings!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectStormDamage
        });
        _events.Add(new RandomEvent
        {
            Name = "Thief",
            Description = "A thief steals some of your money!",
            Tier = 1,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectThief
        });
        _events.Add(new RandomEvent
        {
            Name = "Wandering Merchant",
            Description = "A merchant has appeared on the road!",
            Tier = 1,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectWanderingMerchant
        });
        _events.Add(new RandomEvent
        {
            Name = "Market Crash",
            Description = "The market crashes! Sell prices halved!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectMarketCrash
        });
        _events.Add(new RandomEvent
        {
            Name = "Price Spike",
            Description = "A price boom! Sell prices doubled!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectPriceSpike
        });
        _events.Add(new RandomEvent
        {
            Name = "Bandit Ambush",
            Description = "Bandits surround you!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1500f,
            Effect = EffectBanditAmbush
        });
        _events.Add(new RandomEvent
        {
            Name = "Rainbow",
            Description = "A rainbow appears in the sky!",
            Tier = 1,
            Weight = 3f,
            Cooldown = 600f,
            Effect = EffectRainbow
        });
        _events.Add(new RandomEvent
        {
            Name = "Dancing Animals",
            Description = "Your animals start dancing!",
            Tier = 1,
            Weight = 3f,
            Cooldown = 600f,
            Effect = EffectDancingAnimals
        });
        _events.Add(new RandomEvent
        {
            Name = "Trade Route",
            Description = "A new trade route opens! Buy prices reduced!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectTradeRoute
        });
        _events.Add(new RandomEvent
        {
            Name = "Rat Infestation",
            Description = "Rats appear near your fields!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1200f,
            Effect = EffectRatInfestation
        });

        // RARE (Quest 3: Earn coins)
        _events.Add(new RandomEvent
        {
            Name = "Giant Enemy",
            Description = "A massive enemy has appeared!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 2400f,
            Effect = EffectGiantEnemy
        });
        _events.Add(new RandomEvent
        {
            Name = "Swarm Attack",
            Description = "A swarm of creatures attacks!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 2400f,
            Effect = EffectSwarmAttack
        });
        _events.Add(new RandomEvent
        {
            Name = "Meteor Shower",
            Description = "Meteors rain from the sky!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 2400f,
            Effect = EffectMeteorShower
        });
        _events.Add(new RandomEvent
        {
            Name = "Harvest Festival",
            Description = "The village celebrates! Quest progress boosted!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 3000f,
            Effect = EffectHarvestFestival
        });
        _events.Add(new RandomEvent
        {
            Name = "Fireworks",
            Description = "Fireworks light up the sky!",
            Tier = 2,
            Weight = 2f,
            Cooldown = 1200f,
            Effect = EffectFireworks
        });
        _events.Add(new RandomEvent
        {
            Name = "Ghostly Figures",
            Description = "Ghostly figures wander the land...",
            Tier = 2,
            Weight = 1f,
            Cooldown = 1800f,
            Effect = EffectGhostlyFigures
        });
        _events.Add(new RandomEvent
        {
            Name = "Treasure Map",
            Description = "A treasure has been buried at the map edge!",
            Tier = 2,
            Weight = 2f,
            Cooldown = 2400f,
            Effect = EffectTreasureMap
        });
    }

    // ── Trigger Logic ──

    private void TryAutoTrigger()
    {
        if (_eventInProgress) return;
        TriggerRandomEvent();
    }

    public void TriggerEventByIndex(int index)
    {
        if (_eventInProgress) return;
        if (index < 0 || index >= _events.Count) return;

        var e = _events[index];
        _eventInProgress = true;
        ShowBanner(e.Name, e.Description);
        StartCoroutine(RunEvent(e));
    }

    public string GetEventName(int index)
    {
        if (index < 0 || index >= _events.Count) return "";
        return _events[index].Name;
    }

    public string GetEventDescription(int index)
    {
        if (index < 0 || index >= _events.Count) return "";
        return _events[index].Description;
    }

    public Color GetEventColor(int index)
    {
        if (index < 0 || index >= _events.Count) return Color.white;
        int tier = _events[index].Tier;
        if (tier == 0) return new Color(0.3f, 0.75f, 0.3f);
        if (tier == 1) return new Color(0.3f, 0.5f, 0.9f);
        return new Color(0.9f, 0.75f, 0.1f);
    }

    public void TriggerRandomEvent()
    {
        if (_eventInProgress) return;

        int questTier = GetQuestTier();
        var eligible = new List<RandomEvent>();

        foreach (var e in _events)
        {
            if (e.Tier > questTier) continue;
            if (_cooldowns.ContainsKey(e.Name) && Time.time < _cooldowns[e.Name]) continue;
            eligible.Add(e);
        }

        if (eligible.Count == 0) return;

        float totalWeight = 0f;
        foreach (var e in eligible)
            totalWeight += e.Weight;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        RandomEvent chosen = eligible[0];
        foreach (var e in eligible)
        {
            cumulative += e.Weight;
            if (roll <= cumulative)
            {
                chosen = e;
                break;
            }
        }

        _eventInProgress = true;
        _cooldowns[chosen.Name] = Time.time + chosen.Cooldown;
        ShowBanner(chosen.Name, chosen.Description);
        StartCoroutine(RunEvent(chosen));
    }

    private IEnumerator RunEvent(RandomEvent e)
    {
        yield return new WaitForSeconds(1.5f);
        e.Effect?.Invoke();
        yield return new WaitForSeconds(0.5f);
        _eventInProgress = false;
    }

    private int GetQuestTier()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return 2;

        var saves = qm.GetQuestSaves();
        if (saves == null || saves.Count == 0) return 2;

        int completed = 0;
        foreach (var q in saves)
            if (q.Completed) completed++;

        if (completed >= 3) return 2;
        if (completed >= 2) return 1;
        if (completed >= 1) return 0;
        return 0;
    }

    private void ShowBanner(string title, string desc)
    {
        if (_uiManager != null)
            _uiManager.ShowMessage(title + ": " + desc, 4f);
    }

    // ── Helper: Player Position ──

    private Vector3 GetPlayerPos()
    {
        var p = GameManager.Instance?.Player;
        return p != null ? p.transform.position : new Vector3(0f, 0.5f, 0f);
    }

    private Transform GetWorldRoot()
    {
        return WorldBuilder.Instance?.WorldRoot?.transform;
    }

    // ═══════════════════════════════════════════════
    //  EVENT EFFECTS — BASIC
    // ═══════════════════════════════════════════════

    private void EffectBountifulHarvest()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        foreach (var field in wb.GetAllFields())
        {
            if (field.HasCrop && !field.IsHarvested && field.Stage < 4)
            {
                field.Stage++;
                field.GrowTimer = 0f;
            }
        }
    }

    private void EffectLuckyFind()
    {
        Vector3 playerPos = GetPlayerPos();
        Vector3 spawnPos = playerPos + new Vector3(UnityEngine.Random.Range(-3f, 3f), 10f, UnityEngine.Random.Range(-3f, 3f));
        SpawnCoinPickup(spawnPos, UnityEngine.Random.Range(50, 201));
    }

    private void SpawnCoinPickup(Vector3 position, int amount)
    {
        var root = GetWorldRoot();
        var go = new GameObject("Pickup_gold_" + amount);
        go.transform.SetParent(root);
        go.transform.position = position;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(go.transform, false);
        cube.transform.localScale = new Vector3(0.25f, 0.25f, 0.05f);
        var renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(1f, 0.84f, 0f);
        var col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.linearDamping = 0.5f;

        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(0.5f, 0.5f, 0.5f);

        go.AddComponent<CoinPickupBehavior>().Amount = amount;
    }

    private void EffectStaminaRefresh()
    {
        var player = GameManager.Instance?.Player;
        if (player != null)
            player.Stamina = player.MaxStamina;
    }

    private void EffectHealingSpring()
    {
        var player = GameManager.Instance?.Player;
        if (player != null)
            player.HP = player.MaxHP;
    }

    private void EffectFreeSeeds()
    {
        string[] seeds = { "wheat_seed", "corn_seed", "carrot_seed", "tomato_seed", "strawberry_seed", "pumpkin_seed", "onion_seed" };
        var tm = ToolManager.Instance;
        if (tm == null) return;
        for (int i = 0; i < 4; i++)
        {
            string seed = seeds[UnityEngine.Random.Range(0, seeds.Length)];
            tm.AddItem(seed, 1);
        }
    }

    private void EffectPestInvasion()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        var crops = new List<WorldBuilder.FieldState>();
        foreach (var field in wb.GetAllFields())
            if (field.HasCrop && !field.IsHarvested)
                crops.Add(field);

        int count = Mathf.Min(UnityEngine.Random.Range(1, 4), crops.Count);
        for (int i = 0; i < count; i++)
        {
            var field = crops[UnityEngine.Random.Range(0, crops.Count)];
            field.HasCrop = false;
            field.CropType = null;
            field.Stage = 0;
            field.GrowTimer = 0f;
            crops.Remove(field);
            if (crops.Count == 0) break;
        }
    }

    private void EffectDrought()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        foreach (var field in wb.GetAllFields())
            field.Watered = false;
    }

    private void EffectStrangeNoises()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 8f;
            offset.y = 0f;
            Vector3 spawnPos = playerPos + offset;
            spawnPos.y = 0.5f;

            var go = new GameObject("Mob_" + i);
            go.transform.SetParent(root);
            go.transform.position = spawnPos;
            var mob = go.AddComponent<Mob>();
            mob.Type = UnityEngine.Random.value > 0.5f ? Mob.MobType.Mouse : Mob.MobType.Crab;
        }
    }

    private void EffectCropDisease()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        var crops = new List<WorldBuilder.FieldState>();
        foreach (var field in wb.GetAllFields())
            if (field.HasCrop && !field.IsHarvested)
                crops.Add(field);

        int count = Mathf.Min(UnityEngine.Random.Range(2, 5), crops.Count);
        for (int i = 0; i < count; i++)
        {
            var field = crops[UnityEngine.Random.Range(0, crops.Count)];
            field.HasCrop = false;
            field.CropType = null;
            field.Stage = 0;
            field.GrowTimer = 0f;
            crops.Remove(field);
            if (crops.Count == 0) break;
        }
    }

    private void EffectFireflies()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 12; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 5f;
            offset.y = UnityEngine.Random.Range(0.5f, 2.5f);

            var go = new GameObject("Firefly_" + i);
            go.transform.SetParent(root);
            go.transform.position = playerPos + offset;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform, false);
            cube.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            var r = cube.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(1f, 1f, 0.3f, 0.9f);
            var c = cube.GetComponent<Collider>();
            if (c != null) Destroy(c);

            go.AddComponent<FireflyBehavior>();
        }
    }

    private void EffectWeedGrowth()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        var crops = new List<WorldBuilder.FieldState>();
        foreach (var field in wb.GetAllFields())
            if (field.HasCrop && !field.IsHarvested && field.Stage > 0)
                crops.Add(field);

        if (crops.Count == 0) return;
        var field2 = crops[UnityEngine.Random.Range(0, crops.Count)];
        field2.Stage = Mathf.Max(0, field2.Stage - 1);
    }

    private void EffectStaminaDrain()
    {
        var player = GameManager.Instance?.Player;
        if (player != null)
            player.Stamina = player.MaxStamina * 0.25f;
    }


    // ═══════════════════════════════════════════════
    //  EVENT EFFECTS — ADVANCED
    // ═══════════════════════════════════════════════

    private void SpawnFallingRock(Vector3 pos, Transform parent)
    {
        var go = new GameObject("RockDebris");
        go.transform.SetParent(parent);
        go.transform.position = pos;

        var scale = UnityEngine.Random.Range(0.4f, 0.8f);
        go.transform.localScale = new Vector3(scale, scale, scale);

        var cube = go.AddComponent<BoxCollider>();
        var r = go.GetComponent<Renderer>();
        if (r == null)
        {
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            r = go.GetComponent<Renderer>();
        }
        if (r != null) r.material.color = new Color(0.5f, 0.5f, 0.5f);

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 5f;
        rb.linearDamping = 0.3f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        go.AddComponent<ThrownItem>();
    }

    private void EffectEnemyRaid()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 12f;
            offset.y = 0f;
            SpawnEnemy(playerPos + offset, root);
        }
    }

    private void SpawnEnemy(Vector3 pos, Transform parent)
    {
        pos.y = 0.5f;
        var go = new GameObject("Enemy_" + UnityEngine.Random.Range(0, 1000));
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.5f, 0.9f, 0.3f);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        var bodyR = body.GetComponent<Renderer>();
        if (bodyR != null) bodyR.material.color = new Color(0.4f, 0.1f, 0.1f);
        var bodyC = body.GetComponent<Collider>();
        if (bodyC != null) Destroy(bodyC);

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(go.transform, false);
        head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        head.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        var headR = head.GetComponent<Renderer>();
        if (headR != null) headR.material.color = new Color(0.6f, 0.3f, 0.3f);
        var headC = head.GetComponent<Collider>();
        if (headC != null) Destroy(headC);

        var collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.5f, 1.2f, 0.3f);
        collider.center = new Vector3(0f, 0.6f, 0f);

        go.AddComponent<Rigidbody>().isKinematic = true;

        var enemy = go.AddComponent<EnemyController>();
        enemy.MaxHealth = 50;
        enemy.Damage = 10;
        enemy.MoveSpeed = 2.5f;
        enemy.ChaseRange = 15f;
    }

    private void EffectStormDamage()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        var buildings = new List<WorldBuilder.BuildingState>(wb.GetAllBuildings());
        if (buildings.Count == 0) return;

        var building = buildings[UnityEngine.Random.Range(0, buildings.Count)];
        int damage = building.MaxHealth / 4;
        building.CurrentHealth = Mathf.Max(0, building.CurrentHealth - damage);
    }

    private void EffectThief()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        long stolen = (long)(player.Money * UnityEngine.Random.Range(0.05f, 0.1f));
        player.Money -= stolen;
    }

    private void EffectWanderingMerchant()
    {
        Vector3 playerPos = GetPlayerPos();
        Vector3 merchantPos = playerPos + new Vector3(UnityEngine.Random.Range(-10f, 10f), 0f, UnityEngine.Random.Range(-10f, 10f));
        merchantPos.y = 0.5f;

        var root = GetWorldRoot();
        WorldBuilder.Instance?.SpawnVendorCartAt(merchantPos);
    }

    private void EffectMarketCrash()
    {
        ShowBanner("Market Crash", "Sell prices halved for 2 game-hours!");
        StartCoroutine(ModifySellPrices(0.5f, 2f));
    }

    private void EffectPriceSpike()
    {
        ShowBanner("Price Spike", "Sell prices doubled for 2 game-hours!");
        StartCoroutine(ModifySellPrices(2f, 2f));
    }

    private IEnumerator ModifySellPrices(float multiplier, float gameHours)
    {
        var vendor = FindObjectOfType<VendorShopManager>();
        if (vendor == null) yield break;

        vendor.ApplyPriceMultiplier(multiplier);

        float elapsed = 0f;
        float realDuration = gameHours * 60f / GameManager.Instance.TimeSpeed;
        while (elapsed < realDuration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.GamePaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        vendor.ApplyPriceMultiplier(1f / multiplier);
    }

    private void EffectBanditAmbush()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 6; i++)
        {
            float angle = (i / 6f) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * 10f, 0f, Mathf.Sin(angle) * 10f);
            SpawnEnemy(playerPos + offset, root);
        }
    }

    private void EffectRainbow()
    {
        StartCoroutine(RainbowEffect());
    }

    private IEnumerator RainbowEffect()
    {
        float duration = 3600f / Mathf.Max(GameManager.Instance?.TimeSpeed ?? 1f, 0.1f);
        float elapsed = 0f;
        Color originalAmbient = RenderSettings.ambientLight;

        while (elapsed < duration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.GamePaused)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float hue = Mathf.Repeat(t * 2f, 1f);
                RenderSettings.ambientLight = Color.HSVToRGB(hue, 0.3f, 0.8f);
            }
            yield return null;
        }

        RenderSettings.ambientLight = originalAmbient;
    }

    private void EffectDancingAnimals()
    {
        StartCoroutine(DancingAnimalsEffect());
    }

    private IEnumerator DancingAnimalsEffect()
    {
        var spawner = FindObjectOfType<LivestockSpawner>();
        if (spawner == null) yield break;

        var animals = spawner.GetActiveAnimals();
        if (animals == null || animals.Count == 0) yield break;

        float duration = 10f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            foreach (var animal in animals)
            {
                if (animal == null) continue;
                animal.transform.Rotate(Vector3.up, 360f * Time.deltaTime);
                animal.transform.position += Vector3.up * Mathf.Sin(elapsed * 8f) * 0.01f;
            }
            yield return null;
        }
    }

    private void EffectTradeRoute()
    {
        ShowBanner("Trade Route", "Buy prices reduced for 1 game-day!");
        StartCoroutine(ModifyBuyPrices(0.7f, 24f));
    }

    private IEnumerator ModifyBuyPrices(float multiplier, float gameHours)
    {
        var vendor = FindObjectOfType<VendorShopManager>();
        if (vendor == null) yield break;

        vendor.ApplyBuyPriceMultiplier(multiplier);

        float elapsed = 0f;
        float realDuration = gameHours * 60f / GameManager.Instance.TimeSpeed;
        while (elapsed < realDuration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.GamePaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        vendor.ApplyBuyPriceMultiplier(1f / multiplier);
    }

    private void EffectRatInfestation()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 6f;
            offset.y = 0f;
            Vector3 spawnPos = playerPos + offset;
            spawnPos.y = 0.5f;

            var go = new GameObject("Mob_Rat_" + i);
            go.transform.SetParent(root);
            go.transform.position = spawnPos;
            var mob = go.AddComponent<Mob>();
            mob.Type = Mob.MobType.Mouse;
        }
    }

    // ═══════════════════════════════════════════════
    //  EVENT EFFECTS — RARE
    // ═══════════════════════════════════════════════

    private void EffectGiantEnemy()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        Vector3 spawnPos = playerPos + new Vector3(UnityEngine.Random.Range(-5f, 5f), 0f, 12f);
        spawnPos.y = 0.5f;

        var go = new GameObject("GiantEnemy");
        go.transform.SetParent(root);
        go.transform.position = spawnPos;
        go.transform.localScale = Vector3.one * 2f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.5f, 0.9f, 0.3f);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        var bodyR = body.GetComponent<Renderer>();
        if (bodyR != null) bodyR.material.color = new Color(0.6f, 0.05f, 0.05f);
        var bodyC = body.GetComponent<Collider>();
        if (bodyC != null) Destroy(bodyC);

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(go.transform, false);
        head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        head.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        var headR = head.GetComponent<Renderer>();
        if (headR != null) headR.material.color = new Color(0.8f, 0.2f, 0.2f);
        var headC = head.GetComponent<Collider>();
        if (headC != null) Destroy(headC);

        var collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.5f, 1.2f, 0.3f);
        collider.center = new Vector3(0f, 0.6f, 0f);

        go.AddComponent<Rigidbody>().isKinematic = true;

        var enemy = go.AddComponent<EnemyController>();
        enemy.MaxHealth = 150;
        enemy.Damage = 25;
        enemy.MoveSpeed = 2f;
        enemy.ChaseRange = 20f;
        enemy.AttackRange = 2.5f;
    }

    private void EffectSwarmAttack()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 10; i++)
        {
            float angle = (i / 10f) * Mathf.PI * 2f;
            float radius = 15f + UnityEngine.Random.Range(-3f, 3f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            SpawnEnemy(playerPos + offset, root);
        }
    }

    private void EffectMeteorShower()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 5; i++)
        {
            Vector3 spawnPos = playerPos + new Vector3(UnityEngine.Random.Range(-15f, 15f), 25f + i * 3f, UnityEngine.Random.Range(-15f, 15f));
            SpawnFallingRock(spawnPos, root);
        }
        SpawnEnemy(playerPos + new Vector3(UnityEngine.Random.Range(-8f, 8f), 0f, 10f), root);
    }

    private void EffectHarvestFestival()
    {
        QuestManager.Instance?.AddProgress("money_earned", 500);
    }

    private void EffectFireworks()
    {
        StartCoroutine(FireworksEffect());
    }

    private IEnumerator FireworksEffect()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();

        for (int i = 0; i < 8; i++)
        {
            Vector3 pos = playerPos + new Vector3(UnityEngine.Random.Range(-10f, 10f), 8f + UnityEngine.Random.Range(0f, 5f), UnityEngine.Random.Range(-10f, 10f));

            var go = new GameObject("Firework_" + i);
            go.transform.SetParent(root);
            go.transform.position = pos;

            for (int j = 0; j < 6; j++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.transform.SetParent(go.transform, false);
                spark.transform.localScale = Vector3.one * 0.1f;
                spark.transform.localPosition = UnityEngine.Random.insideUnitSphere * 0.3f;
                var r = spark.GetComponent<Renderer>();
                if (r != null)
                {
                    float h = UnityEngine.Random.Range(0f, 1f);
                    r.material.color = Color.HSVToRGB(h, 1f, 1f);
                }
                var c = spark.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            Destroy(go, 2f);
            yield return new WaitForSeconds(0.4f);
        }
    }

    private void EffectGhostlyFigures()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();

        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 8f;
            offset.y = 0f;

            var go = new GameObject("Ghost_" + i);
            go.transform.SetParent(root);
            go.transform.position = playerPos + offset + Vector3.up * 0.5f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.4f, 1f, 0.3f);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var r = body.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.7f, 0.8f, 1f, 0.4f);
                r.material.SetFloat("_Mode", 3f);
                r.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                r.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                r.material.SetInt("_ZWrite", 0);
                r.material.DisableKeyword("_ALPHATEST_ON");
                r.material.EnableKeyword("_ALPHABLEND_ON");
                r.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                r.material.renderQueue = 3000;
            }
            var c = body.GetComponent<Collider>();
            if (c != null) Destroy(c);

            go.AddComponent<GhostBehavior>();
        }
    }

    private void EffectTreasureMap()
    {
        Vector3 playerPos = GetPlayerPos();
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 treasurePos = new Vector3(Mathf.Cos(angle) * 80f, 0.5f, Mathf.Sin(angle) * 80f);
        SpawnCoinPickup(treasurePos + Vector3.up * 8f, 1000);
        ShowBanner("Treasure Map", "A treasure chest has appeared at the map's edge!");
    }

    // ── Event Data ──

    private class RandomEvent
    {
        public string Name;
        public string Description;
        public int Tier;
        public float Weight;
        public float Cooldown;
        public Action Effect;
    }
}

// ═══════════════════════════════════════════════
//  BEHAVIOR COMPONENTS
// ═══════════════════════════════════════════════

public class CoinPickupBehavior : MonoBehaviour
{
    public int Amount;

    private float _lifetime = 30f;
    private float _bobTimer;

    void Update()
    {
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        _bobTimer += Time.deltaTime * 3f;
        transform.position += Vector3.up * Mathf.Sin(_bobTimer) * 0.002f;

        var player = GameManager.Instance?.Player;
        if (player == null) return;
        if (Vector3.Distance(transform.position, player.transform.position) < 1.5f)
        {
            player.Money += Amount;
            SoundManager.Instance?.Play("pop");
            GameManager.Instance?.UIManager?.ShowMessage("+" + Amount + "g", 1.5f);
            Destroy(gameObject);
        }
    }
}

public class FireflyBehavior : MonoBehaviour
{
    private float _lifetime = 60f;
    private Vector3 _offset;

    void Start()
    {
        _offset = UnityEngine.Random.insideUnitSphere * 2f;
    }

    void Update()
    {
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        _offset += UnityEngine.Random.insideUnitSphere * Time.deltaTime * 0.5f;
        transform.position += _offset * Time.deltaTime;
        _offset *= 0.98f;
    }
}

public class GhostBehavior : MonoBehaviour
{
    private float _lifetime = 20f;
    private Vector3 _direction;

    void Start()
    {
        _direction = UnityEngine.Random.insideUnitSphere;
        _direction.y = 0f;
        _direction.Normalize();
    }

    void Update()
    {
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += _direction * Time.deltaTime * 1.5f;
        transform.position += Vector3.up * Mathf.Sin(Time.time * 2f) * 0.005f;

        if (UnityEngine.Random.value < 0.01f)
            _direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-45f, 45f), 0f) * _direction;
    }
}
