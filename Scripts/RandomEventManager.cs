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
    private float _autoMin = 100f;
    private float _autoMax = 200f;

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
        // BASIC (0-1 quests completed)
        _events.Add(new RandomEvent
        {
            Name = "Mùa Màng Bội Thu",
            Description = "Tất cả mùa màng của bạn đều tăng một giai đoạn!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1800f,
            Effect = EffectBountifulHarvest
        });
        _events.Add(new RandomEvent
        {
            Name = "Tìm Thấy May Mắn",
            Description = "Một đồng vàng xuất hiện trên mặt đất!",
            Tier = 0,
            Weight = 4f,
            Cooldown = 600f,
            Effect = EffectLuckyFind
        });
        _events.Add(new RandomEvent
        {
            Name = "Phục Hồi Thể Lực",
            Description = "Bạn cảm thấy sảng khoái!",
            Tier = 0,
            Weight = 4f,
            Cooldown = 600f,
            Effect = EffectStaminaRefresh
        });
        _events.Add(new RandomEvent
        {
            Name = "Suối Chữa Lành",
            Description = "Vết thương của bạn đã lành!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 900f,
            Effect = EffectHealingSpring
        });
        _events.Add(new RandomEvent
        {
            Name = "Hạt Giống Miễn Phí",
            Description = "Hạt giống rơi từ trên trời!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 900f,
            Effect = EffectFreeSeeds
        });
        _events.Add(new RandomEvent
        {
            Name = "Sâu Bệnh Tấn Công",
            Description = "Sâu đang ăn mùa màng của bạn!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectPestInvasion
        });
        _events.Add(new RandomEvent
        {
            Name = "Hạn Hán",
            Description = "Mặt trời làm khô hết ruộng của bạn!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectDrought
        });
        _events.Add(new RandomEvent
        {
            Name = "Âm Thanh Kỳ Lạ",
            Description = "Bạn nghe thấy âm thanh kỳ lạ ở gần...",
            Tier = 0,
            Weight = 4f,
            Cooldown = 600f,
            NightOnly = true,
            Effect = EffectStrangeNoises
        });
        _events.Add(new RandomEvent
        {
            Name = "Bệnh Mùa Màng",
            Description = "Bệnh đang lây lan khắp mùa màng!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectCropDisease
        });
        _events.Add(new RandomEvent
        {
            Name = "Đom Đóm",
            Description = "Đom đóm nhảy múa xung quanh bạn!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 600f,
            Effect = EffectFireflies
        });
        _events.Add(new RandomEvent
        {
            Name = "Cỏ Dại Mọc Lên",
            Description = "Cỏ dại mọc um tùm trên mùa màng!",
            Tier = 0,
            Weight = 3f,
            Cooldown = 900f,
            Effect = EffectWeedGrowth
        });
        _events.Add(new RandomEvent
        {
            Name = "Hết Sức",
            Description = "Bạn cảm thấy kiệt sức!",
            Tier = 0,
            Weight = 2f,
            Cooldown = 600f,
            Effect = EffectStaminaDrain
        });
        _events.Add(new RandomEvent
        {
            Name = "Thương Nhân Lang Thang",
            Description = "Một thương nhân đã xuất hiện trên đường!",
            Tier = 0,
            Weight = 2f,
            Cooldown = 1200f,
            Effect = EffectWanderingMerchant
        });

        // ADVANCED (2 quests completed)
        _events.Add(new RandomEvent
        {
            Name = "Kẻ Thù Tấn Công",
            Description = "Kẻ thù đang tiến về phía bạn!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1500f,
            NightOnly = true,
            Effect = EffectEnemyRaid
        });
        _events.Add(new RandomEvent
        {
            Name = "Bão Gây Hại",
            Description = "Bão phá hủy công trình của bạn!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectStormDamage
        });
        _events.Add(new RandomEvent
        {
            Name = "Kẻ Trộm",
            Description = "Kẻ trộm lấy mất một phần tiền của bạn!",
            Tier = 1,
            Weight = 3f,
            Cooldown = 1200f,
            Effect = EffectThief
        });
        _events.Add(new RandomEvent
        {
            Name = "Thị Trường Sụp Đổ",
            Description = "Thị trường sụp đổ! Giá bán giảm một nửa!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectMarketCrash
        });
        _events.Add(new RandomEvent
        {
            Name = "Giá Tăng Cao",
            Description = "Giá tăng vọt! Giá bán gấp đôi!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectPriceSpike
        });
        _events.Add(new RandomEvent
        {
            Name = "Cầu Vồng",
            Description = "Một cầu vồng xuất hiện trên bầu trời!",
            Tier = 1,
            Weight = 3f,
            Cooldown = 600f,
            Effect = EffectRainbow
        });
        _events.Add(new RandomEvent
        {
            Name = "Động Vật Nhảy Múa",
            Description = "Động vật của bạn bắt đầu nhảy múa!",
            Tier = 1,
            Weight = 3f,
            Cooldown = 600f,
            Effect = EffectDancingAnimals
        });
        _events.Add(new RandomEvent
        {
            Name = "Tuyến Thương Mại",
            Description = "Tuyến thương mại mới mở! Giá mua giảm!",
            Tier = 1,
            Weight = 2f,
            Cooldown = 1800f,
            Effect = EffectTradeRoute
        });
        // RARE (3+ quests completed)
        _events.Add(new RandomEvent
        {
            Name = "Kẻ Thù Khổng Lồ",
            Description = "Một kẻ thù khổng lồ đã xuất hiện!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 2400f,
            NightOnly = true,
            Effect = EffectGiantEnemy
        });
        _events.Add(new RandomEvent
        {
            Name = "Đàn Tấn Công",
            Description = "Một đàn quái vật tấn công!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 2400f,
            NightOnly = true,
            Effect = EffectSwarmAttack
        });
        _events.Add(new RandomEvent
        {
            Name = "Mưa Sao Băng",
            Description = "Sao băng rơi từ bầu trời!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 2400f,
            Effect = EffectMeteorShower
        });
        _events.Add(new RandomEvent
        {
            Name = "Lễ Hội Thu Hoạch",
            Description = "Làng ăn mừng! Tiến độ nhiệm vụ tăng!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 3000f,
            Effect = EffectHarvestFestival
        });
        _events.Add(new RandomEvent
        {
            Name = "Pháo Hoa",
            Description = "Pháo hoa thắp sáng bầu trời!",
            Tier = 2,
            Weight = 2f,
            Cooldown = 1200f,
            Effect = EffectFireworks
        });
        _events.Add(new RandomEvent
        {
            Name = "Hình Ảnh Bóng Ma",
            Description = "Bóng ma lang thang khắp đất...",
            Tier = 2,
            Weight = 1f,
            Cooldown = 1800f,
            Effect = EffectGhostlyFigures
        });
        _events.Add(new RandomEvent
        {
            Name = "Bản Đồ Kho Báu",
            Description = "Kho báu đã được chôn ở rìa bản đồ!",
            Tier = 2,
            Weight = 2f,
            Cooldown = 2400f,
            Effect = EffectTreasureMap
        });
        _events.Add(new RandomEvent
        {
            Name = "Động Đất",
            Description = "Mặt đất rung chuyển dữ dội! Nhà cửa bị hư hại!",
            Tier = 1,
            Weight = 1.5f,
            Cooldown = 2400f,
            Effect = EffectEarthquake
        });
        _events.Add(new RandomEvent
        {
            Name = "Sấm Sét",
            Description = "Sét đánh xuống từ bầu trời!",
            Tier = 1,
            Weight = 1.5f,
            Cooldown = 2000f,
            Effect = EffectLightningStorm
        });
        _events.Add(new RandomEvent
        {
            Name = "Lốc Xoáy",
            Description = "Một cơn lốc xoáy quét qua thị trấn!",
            Tier = 2,
            Weight = 1f,
            Cooldown = 3000f,
            Effect = EffectTornado
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
        // Intentionally bypasses tier gating: used by scripted story blocks (e.g. WorldBuilder quest triggers).
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
        return Localization.T(_events[index].Name);
    }

    public string GetEventDescription(int index)
    {
        if (index < 0 || index >= _events.Count) return "";
        return Localization.T(_events[index].Description);
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
            if (e.NightOnly)
            {
                float hour = GameManager.Instance != null ? GameManager.Instance.TimeOfDay : 12f;
                bool isNight = hour >= 18f || hour < 6f;
                if (!isNight) continue;
            }
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
            _uiManager.ShowMessage(Localization.T(title) + ": " + Localization.T(desc), 4f);
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
                wb.RefreshFieldVisual(field);
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
        if (Physics.Raycast(position, Vector3.down, out var groundHit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            position.y = groundHit.point.y + 0.5f;

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
        rb.isKinematic = true;
        rb.useGravity = false;

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
            wb.RefreshFieldVisual(field);
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
        Mob.MobType[] types = new[] { Mob.MobType.Mouse, Mob.MobType.Crab, Mob.MobType.Mouse, Mob.MobType.Crab };
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 8f;
            offset.y = 0f;
            Vector3 spawnPos = playerPos + offset;
            spawnPos.y = 0.5f;

            var go = new GameObject("Mob_" + i);
            go.transform.SetParent(root);
            go.transform.position = spawnPos;
            var mob = go.AddComponent<Mob>();
            mob.Type = types[i];
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
            wb.RefreshFieldVisual(field);
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
        wb.RefreshFieldVisual(field2);
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

    private void SpawnFallingRock(Vector3 pos, Vector3 target, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "GiantRock";
        go.transform.SetParent(parent);
        go.transform.position = pos;

        float scale = UnityEngine.Random.Range(10f, 15f);
        go.transform.localScale = new Vector3(scale, scale, scale);
        go.transform.rotation = Quaternion.Euler(
            UnityEngine.Random.Range(0f, 360f),
            UnityEngine.Random.Range(0f, 360f),
            UnityEngine.Random.Range(0f, 360f));

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            float grey = UnityEngine.Random.Range(0.25f, 0.45f);
            r.material.color = new Color(grey, grey * 0.85f, grey * 0.75f);
        }

        StartCoroutine(DropRock(go, scale, target));
    }

    private IEnumerator DropRock(GameObject rock, float scale, Vector3 target)
    {
        if (rock == null) yield break;
        float speed = 40f;
        float groundY = scale * 0.5f;

        while (rock != null)
        {
            Vector3 pos = rock.transform.position;
            Vector3 toTarget = target - pos;
            toTarget.y = 0f;
            float horizDist = toTarget.magnitude;

            if (pos.y <= groundY && horizDist < 3f) break;

            Vector3 step = toTarget.normalized * speed * Time.deltaTime;
            pos.x += step.x;
            pos.z += step.z;
            pos.y -= speed * 0.5f * Time.deltaTime;
            rock.transform.position = pos;
            yield return null;
        }

        if (rock == null) yield break;

        Vector3 landPos = rock.transform.position;
        landPos.y = groundY;
        rock.transform.position = landPos;

        var wb = WorldBuilder.Instance;
        if (wb != null)
        {
            float checkRadius = scale * 0.6f;
            Collider[] hits = Physics.OverlapSphere(landPos, checkRadius);
            foreach (var col in hits)
            {
                var building = wb.FindBuilding(col.gameObject);
                if (building != null)
                    wb.ApplyMeteorDamage(building, scale);
            }
        }

        var mgr = RandomEventManager.Instance;
        Color rockColor = Color.gray;
        var r = rock.GetComponent<Renderer>();
        if (r != null) rockColor = r.material.color;

        if (mgr != null)
        {
            var root = WorldBuilder.Instance?.WorldRoot?.transform;
            mgr.ShatterRock(landPos, scale, rockColor, root);
            mgr.SpawnImpactDebris(landPos, root);
            mgr.StartCoroutine(mgr.CameraShake(1.5f, 0.4f));
        }

        float elapsed = 0f;
        Vector3 origScale = rock.transform.localScale;
        while (elapsed < 0.3f && rock != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.3f;
            rock.transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t);
            yield return null;
        }
        if (rock != null) Destroy(rock);
    }

    private void ShatterRock(Vector3 pos, float parentScale, Color color, Transform parent)
    {
        int count = UnityEngine.Random.Range(10, 13);
        for (int i = 0; i < count; i++)
        {
            var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "RockChunk";
            chunk.transform.SetParent(parent);
            chunk.transform.position = pos;

            float s = parentScale * UnityEngine.Random.Range(0.08f, 0.18f);
            chunk.transform.localScale = new Vector3(s, s, s);
            chunk.transform.rotation = Quaternion.Euler(
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f));

            var cr = chunk.GetComponent<Renderer>();
            if (cr != null)
            {
                float variation = UnityEngine.Random.Range(-0.08f, 0.08f);
                cr.material.color = new Color(
                    Mathf.Clamp01(color.r + variation),
                    Mathf.Clamp01(color.g + variation),
                    Mathf.Clamp01(color.b + variation));
            }

            var rb = chunk.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = parentScale * 0.3f;
            rb.linearDamping = 1f;

            Vector3 dir = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(0.5f, 2.5f),
                UnityEngine.Random.Range(-1f, 1f)).normalized;
            rb.linearVelocity = dir * UnityEngine.Random.Range(8f, 18f);

            Destroy(chunk, 30f);
        }
    }

    private void EffectEnemyRaid()
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

    private void SpawnEnemy(Vector3 pos, Transform parent)
    {
        pos.y = 0.5f;
        var go = new GameObject("Enemy_" + UnityEngine.Random.Range(0, 1000));
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one;

        go.AddComponent<Rigidbody>().isKinematic = true;

        var enemy = go.AddComponent<EnemyController>();
        enemy.MaxHealth = 50;
        enemy.Damage = 10;
        enemy.MoveSpeed = 2.5f;
        enemy.ChaseRange = 15f;
        enemy.IsGiant = false;
    }

    private void EffectStormDamage()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        var buildings = new List<WorldBuilder.BuildingState>(wb.GetAllBuildings());
        if (buildings.Count == 0) return;

        var building = buildings[UnityEngine.Random.Range(0, buildings.Count)];
        int damage = Mathf.Max(1, building.MaxHealth / 4);
        wb.DamageBuildingDirect(building, damage);
    }

    private void EffectEarthquake()
    {
        StartCoroutine(CameraShake(3f, 0.3f));
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        int dmg = Mathf.Max(1, 15);
        foreach (var b in wb.GetAllBuildings())
            wb.DamageBuildingDirect(b, dmg);
    }

    private void EffectLightningStorm()
    {
        StartCoroutine(LightningStormEffect());
    }

    private IEnumerator LightningStormEffect()
    {
        var wb = WorldBuilder.Instance;
        var root = GetWorldRoot();
        Vector3 playerPos = GetPlayerPos();

        Vector3 cloudPos = playerPos + new Vector3(
            UnityEngine.Random.Range(-10f, 10f), 50f,
            UnityEngine.Random.Range(-10f, 10f));
        var cloud = MapBuilder.BuildCloud(root, cloudPos, 4f);
        foreach (var r in cloud.GetComponentsInChildren<Renderer>())
            r.material.color = new Color(0.15f, 0.15f, 0.18f);

        float duration = 10f;
        float elapsed = 0f;
        float nextStrike = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            nextStrike -= Time.deltaTime;

            if (nextStrike <= 0f)
            {
                nextStrike = UnityEngine.Random.Range(1f, 2f);

                Vector3 strikePos = new Vector3(
                    playerPos.x + UnityEngine.Random.Range(-12f, 12f), 0.5f,
                    playerPos.z + UnityEngine.Random.Range(-12f, 12f));
                float boltHeight = cloudPos.y - 0.5f;
                float boltMidY = 0.5f + boltHeight * 0.5f;

                var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bolt.name = "LightningBolt";
                bolt.transform.SetParent(root);
                bolt.transform.position = new Vector3(strikePos.x, boltMidY, strikePos.z);
                bolt.transform.localScale = new Vector3(0.5f, boltHeight, 0.5f);
                var br = bolt.GetComponent<Renderer>();
                if (br != null) br.material.color = Color.white;
                Destroy(bolt.GetComponent<Collider>());
                Destroy(bolt, 0.2f);

                if (wb != null)
                {
                    Collider[] hits = Physics.OverlapSphere(strikePos, 5f);
                    foreach (var col in hits)
                    {
                        var building = wb.FindBuilding(col.gameObject);
                        if (building != null)
                            wb.DamageBuildingDirect(building, Mathf.Max(1, building.MaxHealth / 4));
                    }
                }
            }

            yield return null;
        }

        if (cloud != null) Destroy(cloud);
    }

    private void EffectTornado()
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        var buildings = new List<WorldBuilder.BuildingState>(wb.GetAllBuildings());
        if (buildings.Count == 0) return;

        var root = GetWorldRoot();
        Vector3 playerPos = GetPlayerPos();
        var tornado = MapBuilder.BuildTornado(root, playerPos + new Vector3(0f, 0f, 5f));
        Destroy(tornado, 30f);

        var tb = tornado.GetComponent<TornadoBehavior>();

        for (int i = 0; i < 3 && buildings.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, buildings.Count);
            var b = buildings[idx];
            buildings.RemoveAt(idx);
            wb.DamageBuildingDirect(b, Mathf.Max(1, b.MaxHealth / 3));

            if (tb == null) continue;

            Color debrisColor = GetBuildingDebrisColor(b.Type);
            int debrisCount = UnityEngine.Random.Range(3, 6);
            for (int j = 0; j < debrisCount; j++)
            {
                float s = UnityEngine.Random.Range(0.3f, 0.8f);
                tb.AddDebrisBlock(Vector3.one * s, debrisColor);
            }
        }
    }

    private Color GetBuildingDebrisColor(string type)
    {
        switch (type)
        {
            case "PlayerHouse": return new Color(0.63f, 0.39f, 0.18f);
            case "WifeHouse": return new Color(0.522f, 0.337f, 0.18f);
            case "Shop": return new Color(0.58f, 0.361f, 0.165f);
            default: return new Color(0.35f, 0.32f, 0.28f);
        }
    }

    private void EffectThief()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;
        long stolen = (long)(player.Money * UnityEngine.Random.Range(0.05f, 0.1f));
        stolen = Math.Min(stolen, 500L);
        if (stolen > 0)
            stolen = Math.Max(1L, stolen);
        player.Money -= stolen;
        if (stolen > 0)
            GameStats.AddMoneyStolen(stolen);
    }

    private void EffectWanderingMerchant()
    {
        Vector3 playerPos = GetPlayerPos();
        Vector3 merchantPos = playerPos + new Vector3(UnityEngine.Random.Range(-10f, 10f), 0f, UnityEngine.Random.Range(-10f, 10f));
        merchantPos.y = 0.5f;

        var root = GetWorldRoot();
        Vector3 toPlayer = playerPos - merchantPos;
        toPlayer.y = 0f;
        Quaternion cartRot = toPlayer.sqrMagnitude > 0.001f
            ? Quaternion.FromToRotation(new Vector3(0f, 0f, -1f), toPlayer.normalized)
            : Quaternion.identity;
        WorldBuilder.Instance?.SpawnVendorCartAt(merchantPos, cartRot);
    }

    private void EffectMarketCrash()
    {
        ShowBanner("Thị Trường Sụp Đổ", "Giá bán giảm một nửa trong 2 giờ!");
        StartCoroutine(ModifySellPrices(0.5f, 2f));
    }

    private void EffectPriceSpike()
    {
        ShowBanner("Giá Tăng Cao", "Giá bán gấp đôi trong 2 giờ!");
        StartCoroutine(ModifySellPrices(2f, 2f));
    }

    private IEnumerator ModifySellPrices(float multiplier, float gameHours)
    {
        var vendor = FindObjectOfType<VendorShopManager>();
        if (vendor == null) yield break;

        vendor.ApplyPriceMultiplier(multiplier);

        float elapsed = 0f;
        float realDuration = gameHours / Mathf.Max(GameManager.Instance != null ? GameManager.Instance.TimeSpeed : 1f, 0.01f);
        while (elapsed < realDuration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.GamePaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        vendor.ResetSellPrices();
    }

    private void EffectRainbow()
    {
        StartCoroutine(RainbowEffect());
    }

    private IEnumerator RainbowEffect()
    {
        float duration = 1f / Mathf.Max(GameManager.Instance?.TimeSpeed ?? 1f, 0.01f);
        var root = GetWorldRoot();

        Vector3 center = new Vector3(
            UnityEngine.Random.Range(-210f, -150f),
            0f,
            UnityEngine.Random.Range(30f, 80f));
        int bands = 7;
        float outerRadius = 180f;
        float step = 3.5f;
        int cubesPerBand = 100;

        Color[] rainbowColors = new Color[]
        {
            new Color(1f, 0f, 0f),     // Red
            new Color(1f, 0.5f, 0f),   // Orange
            new Color(1f, 1f, 0f),     // Yellow
            new Color(0f, 1f, 0f),     // Green
            new Color(0f, 0.5f, 1f),   // Blue
            new Color(0.3f, 0f, 0.5f), // Indigo
            new Color(0.6f, 0f, 1f)    // Violet
        };

        var allBlocks = new List<GameObject>(bands * cubesPerBand);

        for (int b = 0; b < bands; b++)
        {
            float radius = outerRadius - b * step;
            for (int i = 0; i < cubesPerBand; i++)
            {
                float t = (float)i / (cubesPerBand - 1);
                float theta = t * Mathf.PI;
                float x = Mathf.Cos(theta) * radius;
                float y = Mathf.Sin(theta) * radius;

                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "RainbowBlock";
                block.transform.SetParent(root);
                block.transform.position = center + new Vector3(x, y, 0f);
                block.transform.localScale = new Vector3(6f, 6f, 6f);

                var cr = block.GetComponent<Renderer>();
                if (cr != null) cr.material.color = rainbowColors[b];

                var col = block.GetComponent<Collider>();
                if (col != null) Destroy(col);

                allBlocks.Add(block);
            }
        }

        float elapsed = 0f;
        Color originalAmbient = RenderSettings.ambientLight;

        while (elapsed < duration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.GamePaused)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float hue = Mathf.Repeat(t * 2f, 1f);
                RenderSettings.ambientLight = Color.HSVToRGB(hue, 0.15f, 0.9f);
            }
            yield return null;
        }

        RenderSettings.ambientLight = originalAmbient;

        float fadeTime = 2f;
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeTime)
        {
            fadeElapsed += Time.deltaTime;
            float alpha = 1f - (fadeElapsed / fadeTime);
            float s = 6f * alpha;
            for (int i = 0; i < allBlocks.Count; i++)
            {
                if (allBlocks[i] != null)
                    allBlocks[i].transform.localScale = new Vector3(s, s, s);
            }
            yield return null;
        }

        foreach (var b in allBlocks)
            if (b != null) Destroy(b);
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
        ShowBanner("Tuyến Thương Mại", "Giá mua giảm trong 1 ngày!");
        StartCoroutine(ModifyBuyPrices(0.7f, 24f));
    }

    private IEnumerator ModifyBuyPrices(float multiplier, float gameHours)
    {
        var vendor = FindObjectOfType<VendorShopManager>();
        if (vendor == null) yield break;

        vendor.ApplyBuyPriceMultiplier(multiplier);

        float elapsed = 0f;
        float realDuration = gameHours / Mathf.Max(GameManager.Instance != null ? GameManager.Instance.TimeSpeed : 1f, 0.01f);
        while (elapsed < realDuration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.GamePaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        vendor.ResetBuyPrices();
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

        go.AddComponent<Rigidbody>().isKinematic = true;

        var enemy = go.AddComponent<EnemyController>();
        enemy.MaxHealth = 150;
        enemy.Damage = 25;
        enemy.MoveSpeed = 2f;
        enemy.ChaseRange = 20f;
        enemy.AttackRange = 2.5f;
        enemy.IsGiant = true;
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
        StartCoroutine(MeteorShowerEffect());
    }

    private IEnumerator MeteorShowerEffect()
    {
        Vector3 playerPos = GetPlayerPos();
        var root = GetWorldRoot();
        for (int i = 0; i < 6; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float dist = UnityEngine.Random.Range(40f, 60f);
            Vector3 spawnPos = playerPos + new Vector3(
                Mathf.Cos(angle) * dist,
                80f + UnityEngine.Random.Range(0f, 40f),
                Mathf.Sin(angle) * dist);
            Vector3 target = playerPos + new Vector3(
                UnityEngine.Random.Range(-20f, 20f), 0f,
                UnityEngine.Random.Range(-20f, 20f));
            SpawnFallingRock(spawnPos, target, root);
            yield return new WaitForSeconds(0.8f);
        }
    }

    private void EffectHarvestFestival()
    {
        QuestManager.Instance?.AddProgress("money_earned", 500);
    }

    private void EffectFireworks()
    {
        StartCoroutine(FireworksEffect());
    }

    public void PlayFireworks(Vector3 center, Transform parent, int totalBursts = 5)
    {
        StartCoroutine(FireworksEffect(center, parent, totalBursts));
    }

    private IEnumerator FireworksEffect()
    {
        yield return FireworksEffect(GetPlayerPos(), GetWorldRoot(), 10);
    }

    private IEnumerator FireworksEffect(Vector3 center, Transform parent, int totalBursts)
    {
        for (int i = 0; i < totalBursts; i++)
        {
            Vector3 pos = center + new Vector3(
                UnityEngine.Random.Range(-12f, 12f),
                10f + UnityEngine.Random.Range(0f, 6f),
                UnityEngine.Random.Range(-12f, 12f));

            int sparkCount = UnityEngine.Random.Range(12, 18);
            Color baseColor = Color.HSVToRGB(UnityEngine.Random.Range(0f, 1f), 1f, 1f);

            for (int j = 0; j < sparkCount; j++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.transform.SetParent(parent);
                spark.transform.position = pos;
                float sz = UnityEngine.Random.Range(0.08f, 0.14f);
                spark.transform.localScale = new Vector3(sz, sz, sz);
                var sr = spark.GetComponent<Renderer>();
                Color sparkColor = UnityEngine.Random.value > 0.5f ? baseColor : Color.HSVToRGB(UnityEngine.Random.Range(0f, 1f), 1f, 1f);
                if (sr != null)
                {
                    sr.material.color = sparkColor;
                    sr.material.EnableKeyword("_EMISSION");
                    sr.material.SetColor("_EmissionColor", sparkColor * 0.4f);
                }
                var sc = spark.GetComponent<Collider>();
                if (sc != null) Destroy(sc);

                Vector3 dir = UnityEngine.Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.7f + 0.3f;
                Vector3 velocity = dir * UnityEngine.Random.Range(4f, 9f);

                StartCoroutine(AnimateSpark(spark, sr, velocity, sparkColor));
            }

            yield return new WaitForSeconds(0.35f);
        }
    }

    private IEnumerator AnimateSpark(GameObject spark, Renderer rend, Vector3 velocity, Color color)
    {
        if (spark == null) yield break;
        var root = WorldBuilder.Instance?.WorldRoot?.transform;
        float lifetime = 1.5f;
        float elapsed = 0f;
        float trailTimer = 0f;

        while (elapsed < lifetime && spark != null)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            velocity.y -= 9.8f * dt;

            spark.transform.position += velocity * dt;
            trailTimer += dt;

            if (trailTimer >= 0.04f && spark != null)
            {
                trailTimer = 0f;
                SpawnTrailDot(spark.transform.position, color, root);
            }

            float t = elapsed / lifetime;
            float fade = 1f - t;
            if (rend != null)
            {
                Color c = color * fade;
                c.a = 1f;
                rend.material.color = c;
                rend.material.SetColor("_EmissionColor", c * 0.4f);
            }
            float s = Mathf.Lerp(1f, 0.2f, t);
            if (spark != null)
                spark.transform.localScale = Vector3.one * s * 0.1f;

            yield return null;
        }

        if (spark != null) Destroy(spark);
    }

    private void SpawnTrailDot(Vector3 pos, Color color, Transform parent)
    {
        var dot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dot.transform.SetParent(parent);
        dot.transform.position = pos;
        dot.transform.localScale = Vector3.one * 0.04f;
        var r = dot.GetComponent<Renderer>();
        if (r != null) r.material.color = color * 0.7f;
        var c = dot.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Destroy(dot, 0.5f);
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
        Vector3 offset = new Vector3(Mathf.Cos(angle) * 40f, 0.5f, Mathf.Sin(angle) * 40f);
        Vector3 treasurePos = new Vector3(playerPos.x, 0.5f, playerPos.z) + offset;
        SpawnCoinPickup(treasurePos + Vector3.up * 8f, 1000);
        ShowBanner("Bản Đồ Kho Báu", "Rương kho báu đã xuất hiện ở rìa bản đồ!");
    }

    private class RandomEvent
    {
        public string Name;
        public string Description;
        public int Tier;
        public float Weight;
        public float Cooldown;
        public bool NightOnly;
        public Action Effect;
    }

    public void SpawnImpactDebris(Vector3 pos, Transform parent)
    {
        for (int i = 0; i < 8; i++)
        {
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.transform.SetParent(parent);
            debris.transform.position = pos;
            float s = UnityEngine.Random.Range(0.08f, 0.18f);
            debris.transform.localScale = new Vector3(s, s, s);
            var dr = debris.GetComponent<Renderer>();
            if (dr != null)
            {
                float g = UnityEngine.Random.Range(0.3f, 0.5f);
                dr.material.color = new Color(g, g * 0.8f, g * 0.7f);
            }
            var dc = debris.GetComponent<Collider>();
            if (dc != null) Destroy(dc);
            var drb = debris.AddComponent<Rigidbody>();
            drb.useGravity = true;
            drb.mass = 0.5f;
            drb.linearDamping = 0.5f;
            Vector3 dir = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(0.5f, 2f),
                UnityEngine.Random.Range(-1f, 1f)).normalized;
            drb.linearVelocity = dir * UnityEngine.Random.Range(3f, 7f);
            Destroy(debris, 3f);
        }
    }

    public IEnumerator CameraShake(float duration, float intensity)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;
        Vector3 originalPos = cam.transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-1f, 1f) * intensity;
            float y = UnityEngine.Random.Range(-1f, 1f) * intensity * 0.5f;
            cam.transform.position = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cam.transform.position = originalPos;
        if (follow != null) follow.enabled = true;
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
    private bool _collected;

    public bool Collected => _collected;

    void Update()
    {
        if (_collected) return;

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
            _collected = true;
            player.Money += Amount;
            GameStats.AddMoneyEarned(Amount);
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
