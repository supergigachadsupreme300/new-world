using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }
    public static bool EscapeHandledThisFrame { get; private set; }

    [Header("Tool Graphics Overrides")]
    public GameObject ToolModelPrefab;
    public Material ToolMaterial;
    public Texture2D ToolTexture;

    [Header("Item Textures")]
    public Texture2D FertilizerTexture;
    public Texture2D PeashooterSeedTexture;

    [Header("Special Item Models")]
    public GameObject MiHaoHaoModel;
    public Texture2D MiHaoHaoTexture;

    private UIManager _uiManager;
    private WorldBuilder _worldBuilder;
    private readonly InventorySlot[] _inventory = new InventorySlot[10];
    private int _selectedSlot = -1;
    private readonly Dictionary<string, GameObject> _toolModels = new Dictionary<string, GameObject>();
    private GameObject _toolContainer;
    private GameObject _buildingMenuPanel;
    private bool _buildingMenuOpen;
    private bool _buildingChosen;
    private LineRenderer _rayRenderer;
    private GameObject _carriedObject;
    private string _lastCarryInfoText;
    private const float PickupRayDistance = 4f;
    private const float UseRayDistance = 10f;
    private const float PalmProjectileSpeed = 25f;
    private bool _isSwinging;
    private bool _initialized;
    private readonly Collider[] _overlapBuffer = new Collider[8];
    private int _rayFrameCounter;
    private bool _canRaycast;

    private static readonly Dictionary<string, float> ToolStaminaCost = new Dictionary<string, float>
    {
        { "axe", 15f },
        { "pickaxe", 15f },
        { "hoe", 12f },
        { "hammer", 20f },
        { "scythe", 10f },
        { "watering_can", 8f },
        { "fertilizer", 5f },
        { "club", 12f },
        { "rosary", 12f },
    };

    private float StaminaCostFor(string item)
    {
        if (ToolStaminaCost.TryGetValue(item, out var cost))
        {
            var sm = SkillManager.Instance;
            if (sm != null)
            {
                if (IsFarmingTool(item))
                    cost *= sm.FarmingStaminaEfficiency();
                cost *= sm.GlobalStaminaEfficiency();
            }
            return Mathf.Max(1f, cost);
        }
        return 10f;
    }

    private static bool IsFarmingTool(string itemType)
    {
        if (itemType == null) return false;
        switch (itemType)
        {
            case "hoe":
            case "scythe":
            case "watering_can":
            case "fertilizer":
                return true;
            default:
                return itemType.EndsWith("_seed");
        }
    }

    private bool RequiresStamina(string itemType)
    {
        if (itemType == null) return false;
        switch (itemType)
        {
            case "axe":
            case "pickaxe":
            case "hoe":
            case "scythe":
            case "hammer":
            case "club":
            case "rosary":
            case "watering_can":
            case "fertilizer":
            case "mi_chinh":
                return true;
            default:
                return itemType.EndsWith("_seed");
        }
    }

    private bool TryUseTool(PlayerController player)
    {
        var item = GetSelectedItemType();
        if (item == null) return false;
        if (player.Stamina < StaminaCostFor(item))
        {
            _uiManager?.ShowMessage(Localization.T("Quá mệt!"), 1f);
            return false;
        }
        return true;
    }

    private bool SpendToolStamina(PlayerController player)
    {
        if (player == null) return false;
        var item = GetSelectedItemType();
        if (item == null) return false;
        if (!player.SpendStamina(StaminaCostFor(item)))
        {
            _uiManager?.ShowMessage(Localization.T("Quá mệt!"), 1f);
            return false;
        }
        PlaySwing();
        return true;
    }

    private bool TryEatFood(string itemType, PlayerController player)
    {
        int staminaRestore = FoodStaminaFor(itemType);
        int hpRestore = FoodHealFor(itemType);
        if (staminaRestore <= 0 && hpRestore <= 0)
            return false;

        player.Stamina = Mathf.Min(player.MaxStamina, player.Stamina + staminaRestore);
        player.HP = Mathf.Min(player.MaxHP, player.HP + hpRestore);
        RemoveItemAmount(itemType, 1);
        if (itemType == "cafe_den")
        {
            player.ApplyStaminaRegenModifier(0.5f, 120f);
            _uiManager?.ShowMessage(Localization.F("Đã uống {0}. Hồi phục +{1} Thể Lực, nhưng hồi Thể Lực chậm lại trong 120 giây!", Localization.ItemName(itemType), staminaRestore), 2.5f);
        }
        else if (itemType == "xoi_gac")
        {
            player.ApplyStaminaRegenModifier(1.2f, 120f);
            _uiManager?.ShowMessage(Localization.F("Đã ăn {0}. Hồi phục +{1} Thể Lực và hồi Thể Lực nhanh hơn 20% trong 120 giây!", Localization.ItemName(itemType), staminaRestore), 2.5f);
        }
        else
        {
            _uiManager?.ShowMessage(Localization.F("Đã ăn {0}. Hồi phục +{1} Thể Lực, +{2} Máu!", Localization.ItemName(itemType), staminaRestore, hpRestore), 1.5f);
        }
        SoundManager.Instance?.Play("pop", 0.8f);

        var cam = GetActiveCamera();
        if (cam != null)
            SteamEffect.SpawnPuff(cam.transform.position + cam.transform.forward * 0.9f + Vector3.up * -0.15f);

        StartCoroutine(EatAnimation());
        UpdateInventoryUI();
        ShowActiveToolModel();
        return true;
    }

    private IEnumerator EatAnimation()
    {
        var tool = GetActiveToolModel();
        if (tool == null)
            yield break;

        float dur = 0.4f;
        float elapsed = 0f;
        Vector3 startScale = tool.transform.localScale;
        Vector3 startPos = tool.transform.localPosition;
        Quaternion startRot = tool.transform.localRotation;

        while (elapsed < dur)
        {
            float t = elapsed / dur;
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
            tool.transform.localScale = startScale * pulse;
            tool.transform.localPosition = startPos + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.12f;
            tool.transform.localRotation = startRot * Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 25f, 0f, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        tool.transform.localScale = startScale;
        tool.transform.localPosition = startPos;
        tool.transform.localRotation = startRot;
    }

    private int FoodStaminaFor(string itemType)
    {
        switch (itemType)
        {
            case "mi_hao_hao": return 100;
            case "com_trang": return 150;
            case "com_tam": return 300;
            case "com_ga": return 450;
            case "com_chieu": return 650;
            case "nuoc_dau": return 150;
            case "tra_da": return 80;
            case "soda": return 120;
            case "banh_mi": return 200;
            case "banh_tet": return 350;
            case "keo": return 60;
            case "tu_gao": return 100;
            case "duong": return 30;
            case "muoi": return 40;
            case "cafe_den": return 800;
            case "xoi_gac": return 350;
            case "sup_bi_ngo": return 300;
            case "mut_ca_rot": return 200;
            case "trai_cay_kho": return 180;
            case "dua_chua": return 150;
            case "ruou_gao": return 500;
            case "tuong_ot": return 120;
            case "ruou_tang": return 700;
            case "tinh_duoc": return 1000;
            default: return 0;
        }
    }

    private int FoodHealFor(string itemType)
    {
        switch (itemType)
        {
            case "nuoc_dau": return 15;
            case "tra_da": return 10;
            case "soda": return 10;
            case "banh_mi": return 15;
            case "banh_tet": return 25;
            case "keo": return 5;
            case "tu_gao": return 40;
            case "duong": return 10;
            case "muoi": return 20;
            case "sup_bi_ngo": return 20;
            case "dua_chua": return 15;
            case "trai_cay_kho": return 12;
            case "ruou_tang": return 40;
            case "tinh_duoc": return 60;
            default: return 0;
        }
    }

    private void PlaySwing()
    {
        if (_isSwinging) return;
        StartCoroutine(SwingAnimation());
    }

    private IEnumerator SwingAnimation()
    {
        _isSwinging = true;

        var itemType = GetSelectedItemType();
        if (itemType != null)
        {
            var sound = itemType switch
            {
                "scythe" => "sickle",
                _ => itemType
            };
            SoundManager.Instance?.Play(sound);
        }

        var tool = GetActiveToolModel();
        if (tool != null)
        {
            float dur = 0.12f;
            float elapsed = 0f;
            Quaternion start = tool.transform.localRotation;
            Quaternion swing = start * Quaternion.Euler(50f, 0f, 0f);

            while (elapsed < dur)
            {
                tool.transform.localRotation = Quaternion.Slerp(start, swing, elapsed / dur);
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < dur)
            {
                tool.transform.localRotation = Quaternion.Slerp(swing, start, elapsed / dur);
                elapsed += Time.deltaTime;
                yield return null;
            }

            tool.transform.localRotation = start;
        }
        _isSwinging = false;
    }

    private GameObject GetActiveToolModel()
    {
        var type = GetSelectedItemType();
        if (type == null) return null;
        _toolModels.TryGetValue(type, out var model);
        return model;
    }

    public void Initialize(UIManager uiManager, WorldBuilder worldBuilder)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        if (_initialized) return;
        Instance = this;
        _uiManager = uiManager;
        _worldBuilder = worldBuilder;
        CreateToolContainer();
        CreateRayVisualizer();
        CreateToolModels();
        ResetSelection();
        UpdateInventoryUI();
        _initialized = true;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame)
            return;

        if (_buildingMenuOpen)
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                CloseBuildingMenu();
            else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                EscapeHandledThisFrame = true;
                CloseBuildingMenu();
            }
            return;
        }

        if (GameManager.Instance.GamePaused)
            return;

        EnsureToolContainerAttached();
        _rayFrameCounter++;
        _canRaycast = _rayFrameCounter % 3 == 0;

        if (GetSelectedItemType() == "hammer" && _worldBuilder != null)
        {
            if (_buildingChosen && _canRaycast)
            {
                var cam = GetActiveCamera();
                if (cam != null)
                {
                    var origin = cam.transform.position + cam.transform.forward * 0.3f;
                    var ray = new Ray(origin, cam.transform.forward);
                    if (Physics.Raycast(ray, out var hit, UseRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    {
                        _worldBuilder.UpdatePreviewPosition(hit.point, true);
                    }
                    else
                        _worldBuilder.UpdatePreviewPosition(Vector3.zero, false);
                }
            }
            else
                _worldBuilder.UpdatePreviewPosition(Vector3.zero, false);

            if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
                MobileInputController.Consume("rotate"))
                _worldBuilder.RotateBuildingPreview(90);

            if ((Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
                MobileInputController.Consume("build"))
                ToggleBuildingMenu();

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                EscapeHandledThisFrame = true;
                if (_buildingChosen)
                {
                    _buildingChosen = false;
                    _uiManager?.ShowMessage(Localization.T("Đã huỷ đặt công trình."), 1.2f);
                    return;
                }
                SelectSlot(_selectedSlot - 1);
                return;
            }
        }

        if (Keyboard.current != null && Keyboard.current.leftBracketKey.wasPressedThisFrame)
            SelectSlot(_selectedSlot - 1);
        if (Keyboard.current != null && Keyboard.current.rightBracketKey.wasPressedThisFrame)
            SelectSlot(_selectedSlot + 1);

        TryAutoDeposit();

        UpdateResourceInfo();
    }

    private void UpdateResourceInfo()
    {
        if (_worldBuilder == null || _uiManager == null) return;

        if (_carriedObject != null)
        {
            string infoText = null;
            if (_carriedObject.name == "CageWithAnimal")
            {
                var info = _carriedObject.GetComponent<CageWithAnimalInfo>();
                string animalName = info != null ? Localization.AnimalName(info.AnimalType.ToString()) : Localization.AnimalName("animal");
                infoText = Localization.F("Đang mang: Lồng với {0} (Q để ném)", animalName);
            }
            else
            {
                var (material, amount) = GetCarriedResourceInfo(_carriedObject);
                if (material != null)
                    infoText = Localization.F("Đang mang: {0} {1}", amount.ToString("F2"), Localization.ItemName(material));
            }

            if (infoText != _lastCarryInfoText)
            {
                _lastCarryInfoText = infoText;
                _uiManager.SetInfoText(infoText);
            }
            return;
        }

        if (GetSelectedItemType() != "empty")
        {
            _uiManager.SetInfoText(null);
            return;
        }

        if (!_canRaycast) return;

        var cam = GetActiveCamera();
        if (cam == null) return;

        var origin = cam.transform.position + cam.transform.forward * 0.3f;
        var ray = new Ray(origin, cam.transform.forward);
        if (!Physics.Raycast(ray, out var hit, PickupRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            _uiManager.SetInfoText(null);
            return;
        }

        var root = hit.collider.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;

        if (root.name == "Blueprint")
        {
            var bp = _worldBuilder.FindBlueprint(root);
            if (bp != null)
            {
                var def = _worldBuilder.GetBuildingDefinition(bp.Type);
                if (def != null)
                {
                    float woodRemaining = def.WoodCost - bp.WoodDeposited;
                    float stoneRemaining = def.StoneCost - bp.StoneDeposited;
                    var parts = new System.Collections.Generic.List<string>();
                    if (woodRemaining > 0.01f)
                        parts.Add(woodRemaining.ToString("F1") + " " + Localization.ItemName("wood"));
                    if (stoneRemaining > 0.01f)
                        parts.Add(stoneRemaining.ToString("F1") + " " + Localization.ItemName("stone"));
                    string remainingText = parts.Count > 0 ? Localization.F("Cần: {0}", string.Join(", ", parts)) : Localization.T("Hoàn thành!");
                    _uiManager.SetInfoText(Localization.BuildingName(def.Name) + " - " + remainingText);
                }
                else if (bp.IsEssential || bp.IsMansion)
                {
                    float woodRemaining = bp.WoodCost - bp.WoodDeposited;
                    float stoneRemaining = bp.StoneCost - bp.StoneDeposited;
                    var parts = new System.Collections.Generic.List<string>();
                    if (woodRemaining > 0.01f)
                        parts.Add(woodRemaining.ToString("F1") + " " + Localization.ItemName("wood"));
                    if (stoneRemaining > 0.01f)
                        parts.Add(stoneRemaining.ToString("F1") + " " + Localization.ItemName("stone"));
                    string remainingText = parts.Count > 0 ? Localization.F("Cần: {0}", string.Join(", ", parts)) : Localization.T("Hoàn thành!");
                    if (bp.IsMansion)
                    {
                        string partName = Localization.MansionPartName(bp.Type);
                        _uiManager.SetInfoText(Localization.F("Dinh Thự - {0} - {1}", partName, remainingText));
                    }
                    else
                    {
                        _uiManager.SetInfoText(Localization.BuildingName(bp.Type) + " - " + remainingText);
                    }
                }
                else
                    _uiManager.SetInfoText(null);
            }
            else
            {
                _uiManager.SetInfoText(null);
            }
        }
        else if (root.name.StartsWith("PartGhost_"))
        {
            var ghostBld = _worldBuilder.FindBuilding(root);
            if (ghostBld != null && ghostBld.IsEssential)
            {
                _worldBuilder.GetEssentialCosts(ghostBld.Type, out float totalWood, out float totalStone);
                var parts = new System.Collections.Generic.List<string>();
                if (totalWood > 0.01f)
                    parts.Add(Localization.T("Gỗ:") + $" {Mathf.CeilToInt(totalWood)}");
                if (totalStone > 0.01f)
                    parts.Add(Localization.T("Đá:") + $" {Mathf.CeilToInt(totalStone)}");
                string costText = parts.Count > 0 ? Localization.F("Cần: {0}", string.Join(", ", parts)) : Localization.T("Hoàn thành!");
                _uiManager.SetInfoText(Localization.BuildingName(ghostBld.Type) + " - " + costText);
            }
            else
                _uiManager.SetInfoText(null);
        }
        else if (root.name == "TreeFelled" || root.name == "BranchTop" || root.name == "RockDebris" || root.name == "CageWithAnimal" || root.name == "ThrownCage")
        {
            if (root.name == "CageWithAnimal")
            {
                var cageInfo = root.GetComponent<CageWithAnimalInfo>();
                string name = cageInfo != null ? Localization.AnimalName(cageInfo.AnimalType.ToString()) : Localization.AnimalName("animal");
                _uiManager.SetInfoText(Localization.F("Lồng với {0} (E để nhặt)", name));
            }
            else if (root.name == "ThrownCage")
            {
                _uiManager.SetInfoText(Localization.T("Lồng (E để nhặt)"));
            }
            else
            {
                var (material, amount) = GetCarriedResourceInfo(root);
                string typeName = root.name == "TreeFelled" ? Localization.T("Cây") : root.name == "BranchTop" ? Localization.T("Cành") : Localization.T("Mảnh Vụn");
                _uiManager.SetInfoText(Localization.F("{0} cung cấp {1} {2}", typeName, amount.ToString("F2"), Localization.ItemName(material)));
            }
        }
        else if (root.name == "FieldTile")
        {
            var field = _worldBuilder.GetFieldAt(root.transform.position);
            if (field != null)
            {
                string info;
                if (field.IsHarvested)
                {
                    info = Localization.T("Ruộng (đã thu hoạch — cày lại)");
                }
                else if (field.HasCrop)
                {
                    string cropDisplay = Localization.ItemName(field.CropType);
                    info = Localization.F("{0} • Giai Đoạn {1}/4", cropDisplay, field.Stage);
                    if (field.Watered) info += " 💧";
                    if (field.Fertilized) info += " 🌱";
                }
                else if (field.Tilled)
                {
                    info = Localization.T("Ruộng đã cày — gieo hạt giống");
                }
                else
                {
                    info = Localization.T("Ruộng — dùng cuốc để cày");
                }
                _uiManager.SetInfoText(info);
            }
            else
            {
                _uiManager.SetInfoText(null);
            }
        }
        else
        {
            _uiManager.SetInfoText(null);
        }
    }

    private void LateUpdate()
    {
        EscapeHandledThisFrame = false;
        EnsureToolContainerAttached();
    }

    public void ResetSelection()
    {
        _selectedSlot = -1;
        ShowActiveToolModel();
        UpdateBuildingPreviewVisibility();
    }

    public void SelectSlot(int index)
    {
        if (_carriedObject != null)
            DropCarriedObject(GameManager.Instance?.Player);

        int prev = _selectedSlot;
        _selectedSlot = Mathf.Clamp(index, 0, _inventory.Length - 1);
        if (_buildingChosen && (index != prev))
        {
            _buildingChosen = false;
            if (_worldBuilder != null)
                _worldBuilder.UpdatePreviewPosition(Vector3.zero, false);
        }
        ShowActiveToolModel();
        UpdateInventoryUI();
        UpdateBuildingPreviewVisibility();
    }

    public void UseSelectedItem()
    {
        var selectedItem = GetSelectedItemType();
        var player = GameManager.Instance?.Player;
        if (player == null)
            return;

        var cam = GetActiveCamera();
        if (cam == null)
            return;

        if (selectedItem == null)
        {
            if (_carriedObject != null)
            {
                var depOrigin = cam.transform.position + cam.transform.forward * 0.3f;
                var depRay = new Ray(depOrigin, cam.transform.forward);
                if (Physics.Raycast(depRay, out var depHit, UseRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    if (_worldBuilder.IsBlueprint(depHit.collider.gameObject))
                    {
                        var bp = _worldBuilder.FindBlueprint(depHit.collider.gameObject);
                        var (material, amount) = GetCarriedResourceInfo(_carriedObject);
                        if (material != null && bp != null)
                        {
                            if (amount < 0.05f)
                            {
                                Destroy(_carriedObject);
                                _carriedObject = null;
                                _uiManager.ShowMessage(Localization.T("Quá nhỏ để dùng."), 1f);
                                return;
                            }
                            if (_worldBuilder.DepositMaterial(bp, material, amount))
                            {
                                _uiManager.ShowMessage(Localization.T("Xây dựng hoàn thành!"), 1.5f);
                                KarmaManager.Instance?.AddMaxKarma(1f);
                                SoundManager.Instance?.Play("hammer");
                            }
                            else
                            {
                                _uiManager.ShowMessage(Localization.F("Đã cung cấp {0} x{1}.", Localization.ItemName(material), amount.ToString("F2")), 1.5f);
                            }
                            Destroy(_carriedObject);
                            _carriedObject = null;
                            return;
                        }
                    }
                }
                return;
            }
            return;
        }

        if (selectedItem == "fishing_rod")
        {
            var fc = Object.FindAnyObjectByType<FishingController>();
            if (fc == null)
            {
                var fGo = new GameObject("FishingController");
                fc = fGo.AddComponent<FishingController>();
            }
            fc.TryStartFishing();
            return;
        }

        if (selectedItem == "xap_phong")
        {
            if (player.HP >= player.MaxHP)
            {
                _uiManager.ShowMessage(Localization.T("Máu đã đầy!"), 1f);
                return;
            }
            player.HP = Mathf.Min(player.MaxHP, player.HP + 25);
            RemoveItemAmount(selectedItem, 1);
            _uiManager.ShowMessage(Localization.F("Đã dùng {0}. +25 Máu!", Localization.ItemName(selectedItem)), 1.5f);
            SoundManager.Instance?.Play("pop", 0.8f);
            UpdateInventoryUI();
            ShowActiveToolModel();
            return;
        }

        if (TryEatFood(selectedItem, player))
            return;

        // Consume stamina + play swing animation only for items that have a real action
        if (selectedItem != null && RequiresStamina(selectedItem) && !TryUseTool(player))
            return;

        if (selectedItem == "rosary")
        {
            var km = KarmaManager.Instance;
            if (km == null || !km.ConsumeKarma(1f))
            {
                _uiManager?.ShowMessage(Localization.T("H\u1EBFt ph\u1ee9c \u0111\u1EE9c!"), 1.5f);
                return;
            }
            SpendToolStamina(player);
            LaunchPalmProjectile(cam);
            return;
        }

        if (selectedItem == "club")
        {
            var clubOrigin = cam.transform.position + cam.transform.forward * 0.3f;
            var clubRay = new Ray(clubOrigin, cam.transform.forward);
            if (Physics.Raycast(clubRay, out var clubHit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var target = clubHit.collider.GetComponentInParent<Livestock>();
                if (target != null)
                {
                    SpendToolStamina(player);
                    target.TakeDamage(20);
                    SoundManager.Instance?.Play("club");
                    return;
                }

                var flapping = clubHit.collider.GetComponentInParent<FlappingFish>();
                if (flapping != null && !flapping.IsStunned && !flapping.IsPickable)
                {
                    SpendToolStamina(player);
                    flapping.KnockOut();
                    SoundManager.Instance?.Play("club");
                    return;
                }
            }
            return;
        }

        var origin = cam.transform.position + cam.transform.forward * 0.3f;
        var useRay = new Ray(origin, cam.transform.forward);
        ShowRayLine(useRay.origin, useRay.origin + useRay.direction * UseRayDistance);
        if (Physics.Raycast(useRay, out var hit, UseRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (selectedItem == "axe")
            {
                var hitObj = hit.collider.gameObject;

                if (hitObj.name == "Leaf")
                {
                    if (!SpendToolStamina(player))
                        return;
                    Destroy(hitObj);
                    return;
                }

                if (hitObj.name == "Branch")
                {
                    var treeRoot = FindTreeRoot(hit.collider);
                    if (treeRoot != null && _worldBuilder.ChopBranch(treeRoot, hitObj, hit.point, hit.normal))
                    {
                        SpendToolStamina(player);
                        SoundManager.Instance?.Play("axe");
                    }
                    return;
                }

                if (hitObj.name == "TrunkSeg")
                {
                    var treeRoot = FindTreeRoot(hit.collider);
                    if (treeRoot != null && _worldBuilder.ChopBranch(treeRoot, hitObj, hit.point, hit.normal))
                    {
                        SpendToolStamina(player);
                        SoundManager.Instance?.Play("axe");
                    }
                    return;
                }

                var treeRoot2 = FindTreeRoot(hit.collider);
                if (treeRoot2 != null)
                {
                    if (treeRoot2.transform.Find("Trunk") == null)
                    {
                        if (_worldBuilder.RemoveTree(treeRoot2))
                        {
                            SpendToolStamina(player);
                            SoundManager.Instance?.Play("axe");
                        }
                    }
                    else if (_worldBuilder.ChopTree(treeRoot2, hit.point, hit.normal))
                    {
                        SpendToolStamina(player);
                        SoundManager.Instance?.Play("axe");
                    }
                }
                else
                {
                    var debrisRoot = hit.collider.gameObject;
                    while (debrisRoot.transform.parent != null && debrisRoot.transform.parent.name != "WorldRoot")
                        debrisRoot = debrisRoot.transform.parent.gameObject;
                    if (debrisRoot.name == "BranchTop" || debrisRoot.name == "TreeFelled")
                    {
                        _worldBuilder.SplitWoodDebris(debrisRoot);
                        SpendToolStamina(player);
                        SoundManager.Instance?.Play("axe");
                    }
                }
                return;
            }

            if (selectedItem == "pickaxe" && IsRock(hit.collider))
            {
                var rockRoot = hit.collider.gameObject;
                while (rockRoot.transform.parent != null && rockRoot.transform.parent.name != "WorldRoot")
                    rockRoot = rockRoot.transform.parent.gameObject;

                if (rockRoot.name == "RockDebris")
                {
                    _worldBuilder.SmashDebris(rockRoot);
                    SpendToolStamina(player);
                    SoundManager.Instance?.Play("pickaxe");
                }
                else if (_worldBuilder.HitRock(rockRoot, hit.point, hit.normal))
                {
                    SpendToolStamina(player);
                    SoundManager.Instance?.Play("pickaxe");
                }
                return;
            }

            if (selectedItem == "hoe")
            {
                Vector3 placePosition = hit.point;
                if (FieldManager.Instance != null && FieldManager.Instance.TryGetPreviewPosition(out var previewPos))
                {
                    placePosition = previewPos;
                }

                var field = _worldBuilder.TillGround(placePosition);
                if (field != null)
                {
                    SpendToolStamina(player);
                    SoundManager.Instance?.Play("hoe");
                    _uiManager.ShowMessage(Localization.T("Ruộng đã cày."), 1.5f);
                }
                return;
            }

            if (selectedItem == "hammer")
            {
                if (_buildingMenuOpen)
                    return;

                var placePos = hit.point;

                if (_buildingChosen)
                {
                    var bDef = _worldBuilder.GetBuildingByIndex(_worldBuilder.CurrentBuildingIndex);
                    if (_worldBuilder.IsWallOrStair(bDef.Name) && !_worldBuilder.HasFloorAt(placePos))
                    {
                        _uiManager.ShowMessage(Localization.T("Cần sàn! Tường và cầu thang cần sàn trước."), 1.5f);
                    }
                    else if (_worldBuilder.PlaceBlueprint(placePos))
                    {
                        SpendToolStamina(player);
                        SoundManager.Instance?.Play("hammer");
                        _uiManager.ShowMessage(Localization.T("Bản thiết kế đã đặt. Cung cấp gỗ & đá."), 1.5f);
                    }
                    else
                    {
                        _uiManager.ShowMessage(Localization.T("Không thể đặt ở đây."), 1.5f);
                    }
                    _buildingChosen = false;
                    return;
                }

                var hitObj = hit.collider.gameObject;

                if (_worldBuilder.TryRepairGhost(hit))
                {
                    SpendToolStamina(player);
                    return;
                }

                if (_worldBuilder.FindBuilding(hitObj) != null)
                {
                    _worldBuilder.DamageBuilding(hitObj);
                    SpendToolStamina(player);
                    SoundManager.Instance?.Play("hammer");
                    return;
                }

                if (_worldBuilder.IsBlueprint(hitObj))
                {
                    _worldBuilder.RemoveBlueprint(hitObj);
                    SpendToolStamina(player);
                    SoundManager.Instance?.Play("hammer");
                    return;
                }

                if (_worldBuilder.PlaceBlueprint(placePos))
                {
                    SpendToolStamina(player);
                    SoundManager.Instance?.Play("hammer");
                    _uiManager.ShowMessage(Localization.T("Bản thiết kế đã đặt. Cung cấp gỗ & đá."), 1.5f);
                }
                return;
            }

            if (selectedItem == "watering_can")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.Tilled && field.HasCrop && !field.IsHarvested)
                {
                    if (_worldBuilder.WaterField(hit.point))
                    {
                        SpendToolStamina(player);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage(Localization.T("Ruộng đã tưới."), 1.5f);
                        QuestManager.Instance?.AddProgress("water", 1);
                    }
                }
                else
                {
                    _uiManager.ShowMessage(Localization.T("Dùng bình tưới cho cây đang trồng."), 1.5f);
                }
                return;
            }

            if (selectedItem == "fertilizer")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.Tilled && field.HasCrop && !field.IsHarvested)
                {
                    if (_worldBuilder.FertilizeField(hit.point))
                    {
                        SpendToolStamina(player);
                        RemoveItem(_selectedSlot, 1);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage(Localization.T("Ruộng đã bón phân!"), 1.5f);
                    }
                }
                else
                {
                    _uiManager.ShowMessage(Localization.T("Dùng phân bón cho cây đang trồng."), 1.5f);
                }
                return;
            }

            if (selectedItem == "mi_chinh")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.Tilled && field.HasCrop && !field.IsHarvested)
                {
                    if (_worldBuilder.BoostFieldGrowth(hit.point))
                    {
                        SpendToolStamina(player);
                        RemoveItem(_selectedSlot, 1);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage(Localization.T("Ruộng đã lớn nhanh hơn!"), 1.5f);
                    }
                }
                else
                {
                    _uiManager.ShowMessage(Localization.T("Dùng mì chính cho cây đang trồng."), 1.5f);
                }
                return;
            }

            if (TryPlantSeed(selectedItem, hit.point))
            {
                SpendToolStamina(player);
                return;
            }

            if (selectedItem == "scythe")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.HasCrop && field.Stage >= 4)
                {
                    var harvestedItem = field.CropType;
                    if (!CanHoldItem(harvestedItem))
                    {
                        _uiManager.ShowMessage(Localization.T("Túi đồ đầy."), 1.5f);
                        return;
                    }
                    if (_worldBuilder.HarvestField(field, out var item))
                    {
                        SpendToolStamina(player);
                        AddItem(item, 1);
                        if (item == "wheat")
                            GameStats.AddWheat(1);
                        SkillManager.Instance?.AddXP(SkillManager.Track.Farming, FarmingXPFor(item));
                        var sm = SkillManager.Instance;
                        if (sm != null && sm.BonusCropChance() > 0f && Random.value < sm.BonusCropChance() && CanHoldItem(item))
                        {
                            AddItem(item, 1);
                            _uiManager.ShowMessage(Localization.F("Năng suất! +1 {0} nhờ kỹ năng Canh Tác.", Localization.ItemName(item)), 1.5f);
                        }
                        SoundManager.Instance?.Play("sickle");
                        _uiManager.ShowMessage(Localization.F("Đã thu hoạch {0}.", Localization.ItemName(item)), 1.5f);
                        QuestManager.Instance?.AddProgress(item, 1);
                    }
                }
                return;
            }
        }
    }

    private void UpdateBuildingPreviewVisibility()
    {
        if (_worldBuilder != null)
            _worldBuilder.SetBuildingPreviewVisible(GetSelectedItemType() == "hammer");
    }

    private void UpdateInventoryUI()
    {
        _uiManager?.UpdateInventoryText(_inventory, _selectedSlot);
    }

    public void RefreshInventoryUI()
    {
        UpdateInventoryUI();
    }

    [System.Serializable]
    public class InventorySlot
    {
        public string Type;
        public int Count;
    }

    [System.Serializable]
    public class InventorySlotSave
    {
        public int Slot;
        public string Type;
        public int Count;
    }
}
