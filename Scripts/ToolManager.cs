using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public class ToolManager : MonoBehaviour
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
    private int _gunAmmo;
    private const int GunMaxAmmo = 6;
    private GameObject _carriedObject;
    private const float PickupRayDistance = 4f;
    private const float UseRayDistance = 10f;
    private bool _isSwinging;
    private bool _initialized;

    private static readonly Dictionary<string, float> ToolStaminaCost = new Dictionary<string, float>
    {
        { "axe", 15f },
        { "pickaxe", 15f },
        { "hoe", 12f },
        { "hammer", 20f },
        { "gun", 5f },
        { "scythe", 10f },
        { "watering_can", 8f },
        { "fertilizer", 5f },
    };

    private float StaminaCostFor(string item)
    {
        if (ToolStaminaCost.TryGetValue(item, out var cost))
            return cost;
        return 10f;
    }

    private bool TryUseTool(PlayerController player)
    {
        var item = GetSelectedItemType();
        if (item == null) return false;
        if (!player.SpendStamina(StaminaCostFor(item)))
        {
            _uiManager?.ShowMessage("Too tired!", 1f);
            return false;
        }
        PlaySwing();
        return true;
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
                "scythe" => "sword",
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

        if (Keyboard.current == null)
            return;

        if (_buildingMenuOpen)
        {
            if (Keyboard.current.fKey.wasPressedThisFrame)
                CloseBuildingMenu();
            else if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                EscapeHandledThisFrame = true;
                CloseBuildingMenu();
            }
            return;
        }

        if (GameManager.Instance.GamePaused)
            return;

        EnsureToolContainerAttached();

        if (GetSelectedItemType() == "hammer" && _worldBuilder != null)
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

            if (Keyboard.current.rKey.wasPressedThisFrame)
                _worldBuilder.RotateBuildingPreview(90);

            if (Keyboard.current.fKey.wasPressedThisFrame)
                ToggleBuildingMenu();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _buildingChosen = false;
                EscapeHandledThisFrame = true;
                SelectSlot(_selectedSlot - 1);
                return;
            }
        }

        if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
            SelectSlot(_selectedSlot - 1);
        if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
            SelectSlot(_selectedSlot + 1);

        TryAutoDeposit();

        UpdateResourceInfo();
    }

    private void UpdateResourceInfo()
    {
        if (_worldBuilder == null || _uiManager == null) return;

        if (_carriedObject != null)
        {
            var (material, amount) = GetCarriedResourceInfo(_carriedObject);
            if (material != null)
                _uiManager.SetInfoText("Carrying: " + amount.ToString("F2") + " " + material);
            else
                _uiManager.SetInfoText(null);
            return;
        }

        if (GetSelectedItemType() != "empty")
        {
            _uiManager.SetInfoText(null);
            return;
        }

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
                        parts.Add(woodRemaining.ToString("F1") + " wood");
                    if (stoneRemaining > 0.01f)
                        parts.Add(stoneRemaining.ToString("F1") + " stone");
                    string remainingText = parts.Count > 0 ? "Need: " + string.Join(", ", parts) : "Complete!";
                    _uiManager.SetInfoText(def.Name + " - " + remainingText);
                }
                else
                    _uiManager.SetInfoText(null);
            }
            else
            {
                _uiManager.SetInfoText(null);
            }
        }
        else if (root.name == "TreeFelled" || root.name == "BranchTop" || root.name == "RockDebris")
        {
            var (material, amount) = GetCarriedResourceInfo(root);
            string typeName = root.name == "TreeFelled" ? "Tree" : root.name == "BranchTop" ? "Branch" : "Debris";
            _uiManager.SetInfoText(typeName + " provides " + amount.ToString("F2") + " " + material);
        }
        else if (root.name == "FieldTile")
        {
            var field = _worldBuilder.GetFieldAt(root.transform.position);
            if (field != null)
            {
                string info;
                if (field.IsHarvested)
                {
                    info = "Field (harvested — till again)";
                }
                else if (field.HasCrop)
                {
                    string cropDisplay = field.CropType switch
                    {
                        "wheat" => "Lúa",
                        "corn" => "Ngô",
                        "potato" => "Khoai tây",
                        "carrot" => "Cà rốt",
                        "tomato" => "Cà chua",
                        "strawberry" => "Dâu tây",
                        "pumpkin" => "Bí ngô",
                        "onion" => "Hành tây",
                        "sugarcane" => "Mía",
                        "rice" => "Lúa nước",
                        _ => field.CropType
                    };
                    info = $"{cropDisplay} • Stage {field.Stage}/4";
                    if (field.Watered) info += " 💧";
                    if (field.Fertilized) info += " 🌱";
                }
                else if (field.Tilled)
                {
                    info = "Tilled field — plant a seed";
                }
                else
                {
                    info = "Field — use hoe to till";
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

        _selectedSlot = Mathf.Clamp(index, 0, _inventory.Length - 1);
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
                                _uiManager.ShowMessage("Too small to use.", 1f);
                                return;
                            }
                            if (_worldBuilder.DepositMaterial(bp, material, amount))
                            {
                                _uiManager.ShowMessage("Building completed!", 1.5f);
                                SoundManager.Instance?.Play("hammer");
                            }
                            else
                            {
                                _uiManager.ShowMessage("Supplied " + material + " x" + amount.ToString("F2") + ".", 1.5f);
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

        // Consume stamina + play swing animation for any tool/item use
        if (selectedItem != null && !TryUseTool(player))
            return;

        if (selectedItem == "gun")
        {
            ShootGun(player.transform.position, player.transform.forward);
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
                    Destroy(hitObj);
                    return;
                }

                if (hitObj.name == "Branch")
                {
                    var treeRoot = FindTreeRoot(hit.collider);
                    if (treeRoot != null && _worldBuilder.ChopBranch(treeRoot, hitObj, hit.point, hit.normal))
                    {
                        SoundManager.Instance?.Play("axe");
                    }
                    return;
                }

                if (hitObj.name == "TrunkSeg")
                {
                    var treeRoot = FindTreeRoot(hit.collider);
                    if (treeRoot != null && _worldBuilder.ChopBranch(treeRoot, hitObj, hit.point, hit.normal))
                    {
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
                            SoundManager.Instance?.Play("axe");
                        }
                    }
                    else if (_worldBuilder.ChopTree(treeRoot2, hit.point, hit.normal))
                    {
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
                    SoundManager.Instance?.Play("pickaxe");
                }
                else if (_worldBuilder.HitRock(rockRoot, hit.point, hit.normal))
                {
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
                    SoundManager.Instance?.Play("hoe");
                    _uiManager.ShowMessage("Field tilled.", 1.5f);
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
                    if (_worldBuilder.PlaceBlueprint(placePos))
                    {
                        SoundManager.Instance?.Play("hammer");
                        _uiManager.ShowMessage("Blueprint placed. Supply wood & stone.", 1.5f);
                    }
                    else
                    {
                        _uiManager.ShowMessage("Cannot place blueprint here.", 1.5f);
                    }
                    _buildingChosen = false;
                    return;
                }

                var hitObj = hit.collider.gameObject;

                if (_worldBuilder.FindBuilding(hitObj) != null)
                {
                    _worldBuilder.DamageBuilding(hitObj);
                    SoundManager.Instance?.Play("hammer");
                    return;
                }

                if (_worldBuilder.IsBlueprint(hitObj))
                {
                    _worldBuilder.RemoveBlueprint(hitObj);
                    SoundManager.Instance?.Play("hammer");
                    return;
                }

                if (_worldBuilder.PlaceBlueprint(placePos))
                {
                    SoundManager.Instance?.Play("hammer");
                    _uiManager.ShowMessage("Blueprint placed. Supply wood & stone.", 1.5f);
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
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage("Field watered.", 1.5f);
                    }
                }
                else
                {
                    _uiManager.ShowMessage("Use watering can on a growing crop.", 1.5f);
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
                        RemoveItem(_selectedSlot, 1);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage("Field fertilized!", 1.5f);
                    }
                }
                else
                {
                    _uiManager.ShowMessage("Use fertilizer on a growing crop.", 1.5f);
                }
                return;
            }

            if (TryPlantSeed(selectedItem, hit.point))
                return;

            if (selectedItem == "scythe")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.HasCrop && field.Stage >= 4)
                {
                    if (_worldBuilder.HarvestField(field, out var item))
                    {
                        AddItem(item, 1);
                        SoundManager.Instance?.Play("sword");
                        _uiManager.ShowMessage($"Harvested {item}.", 1.5f);
                        QuestManager.Instance?.AddProgress(item, 1);
                    }
                }
                return;
            }
        }
    }

    private bool TryPlantSeed(string itemType, Vector3 hitPoint)
    {
        var seedToCrop = new Dictionary<string, string>
        {
            { "wheat_seed", "wheat" },
            { "corn_seed", "corn" },
            { "potato_seed", "potato" },
            { "carrot_seed", "carrot" },
            { "tomato_seed", "tomato" },
            { "strawberry_seed", "strawberry" },
            { "pumpkin_seed", "pumpkin" },
            { "onion_seed", "onion" },
            { "sugarcane_seed", "sugarcane" },
            { "rice_seed", "rice" },
            { "wheat", "wheat" },
            { "corn", "corn" },
            { "potato", "potato" },
            { "carrot", "carrot" },
            { "tomato", "tomato" },
            { "strawberry", "strawberry" },
            { "pumpkin", "pumpkin" },
            { "onion", "onion" },
            { "sugarcane", "sugarcane" },
            { "rice", "rice" },
        };

        if (!seedToCrop.TryGetValue(itemType, out var cropType))
            return false;

        var field = _worldBuilder.GetFieldAt(hitPoint);
        if (field != null && field.Tilled && !field.HasCrop)
        {
            if (_worldBuilder.PlantCrop(field, cropType))
            {
                RemoveItem(_selectedSlot, 1);
                SoundManager.Instance?.Play("pop");
                string displayName = cropType switch
                {
                    "wheat" => "lúa",
                    "corn" => "ngô",
                    "potato" => "khoai tây",
                    "carrot" => "cà rốt",
                    "tomato" => "cà chua",
                    "strawberry" => "dâu tây",
                    "pumpkin" => "bí ngô",
                    "onion" => "hành tây",
                    "sugarcane" => "mía",
                    "rice" => "lúa nước",
                    _ => cropType
                };
                _uiManager.ShowMessage($"Đã trồng {displayName}.", 1.5f);
            }
        }
        else
        {
            _uiManager.ShowMessage("Dùng hạt giống trên đất đã cày.", 1.5f);
        }
        return true;
    }

    public void TryPickupNearby()
    {
        var cam = GetActiveCamera();
        if (cam == null)
            return;

        if (_carriedObject != null)
            return;

        var origin = cam.transform.position + cam.transform.forward * 0.3f;
        var ray = new Ray(origin, cam.transform.forward);
        ShowRayLine(ray.origin, ray.origin + ray.direction * PickupRayDistance);
        if (!Physics.Raycast(ray, out var hit, PickupRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return;

        Debug.Log($"TryPickupNearby: ray hit {hit.collider.gameObject.name}");

        if (hit.collider.transform.name == "BuffaloEntity")
        {
            var shop = Object.FindAnyObjectByType<BuffaloShopManager>();
            if (shop == null)
            {
                var go = new GameObject("BuffaloShopManager");
                shop = go.AddComponent<BuffaloShopManager>();
                shop.Initialize();
            }
            shop.Open();
            return;
        }

        if (hit.collider.transform.name == "VendorNPC")
        {
            var shop = Object.FindAnyObjectByType<VendorShopManager>();
            if (shop == null)
            {
                var go = new GameObject("VendorShopManager");
                shop = go.AddComponent<VendorShopManager>();
                shop.Initialize();
            }
            shop.Open();
            return;
        }

        if (TryPickupTool(hit.collider))
            return;

        // Check for felled tree / branch / debris first (carry them, don't delete)
        var root = hit.collider.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;

        if (root.name == "TreeFelled" || root.name == "BranchTop" || root.name == "RockDebris")
        {
            if (root.GetComponent<Rigidbody>() == null) return;
            _carriedObject = root;
            root.GetComponent<Rigidbody>().isKinematic = true;
            var cols = root.GetComponentsInChildren<Collider>();
            foreach (var c in cols)
                c.enabled = false;
            root.transform.SetParent(cam.transform);
            root.transform.localPosition = new Vector3(0.7f, -0.4f, 1.8f);
            root.transform.localRotation = Quaternion.identity;
            _uiManager.ShowMessage("Lifted.", 1f);
            return;
        }

        if (IsTree(hit.collider))
        {
            var treeRoot = FindTreeRoot(hit.collider);
            if (treeRoot != null && _worldBuilder.RemoveTree(treeRoot))
                SoundManager.Instance?.Play("axe");
        }
        else if (IsRock(hit.collider))
        {
            var rockRoot = hit.collider.gameObject;
            while (rockRoot.transform.parent != null && rockRoot.transform.parent.name != "WorldRoot")
                rockRoot = rockRoot.transform.parent.gameObject;
            if (_worldBuilder.RemoveRock(rockRoot))
                SoundManager.Instance?.Play("pickaxe");
        }
    }

    private bool IsTree(Collider collider)
    {
        if (collider == null)
            return false;
        var t = collider.transform;
        while (t != null)
        {
            if (t.name.StartsWith("Tree"))
                return true;
            t = t.parent;
        }
        return false;
    }

    private GameObject FindTreeRoot(Collider collider)
    {
        if (collider == null) return null;
        var t = collider.transform;
        while (t != null)
        {
            if (t.name.StartsWith("Tree"))
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    private bool TryPickupTool(Collider collider)
    {
        if (collider == null)
        {
            Debug.Log("TryPickupTool: collider is null");
            return false;
        }

        var pickupName = collider.gameObject.name;
        var pickupRoot = collider.gameObject;
        
        // Check if this is a visual child, if so get the parent (root pickup)
        if (pickupRoot.transform.parent != null && pickupRoot.transform.parent.name.StartsWith("Pickup_"))
        {
            pickupRoot = pickupRoot.transform.parent.gameObject;
            pickupName = pickupRoot.name;
        }
        
        Debug.Log($"TryPickupTool: hit {pickupName}");
        if (!pickupName.StartsWith("Pickup_"))
            return false;

        var itemType = pickupName.Substring("Pickup_".Length);
        AddItem(itemType, 1);
        SoundManager.Instance?.Play("pop");
        _uiManager.ShowMessage($"Picked up {itemType}.", 1.5f);
        Destroy(pickupRoot);
        return true;
    }

    private bool IsRock(Collider collider)
    {
        if (collider == null)
            return false;
        return collider.gameObject.name.StartsWith("Rock");
    }

    public void DropSelectedItem()
    {
        if (_carriedObject != null)
        {
            DropCarriedObject(GameManager.Instance?.Player);
            return;
        }

        var itemType = GetSelectedItemType();
        if (itemType == null)
            return;

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var cam = Camera.main;
        var throwOrigin = cam != null
            ? cam.transform.position + cam.transform.forward * 0.5f
            : player.transform.position + Vector3.up * 1.5f + player.transform.forward * 0.5f;
        var throwDir = cam != null ? cam.transform.forward : player.transform.forward;
        var throwVelocity = throwDir * 8f + Vector3.up * 3.5f;

        if (RemoveItem(_selectedSlot, 1))
        {
            if (_worldBuilder != null)
                _worldBuilder.ThrowPickup(itemType, throwOrigin, throwVelocity);

            _uiManager.ShowMessage($"Threw {itemType}.", 1.5f);
            UpdateInventoryUI();
        }
    }

    private void TryPickupFelledTree(Camera cam, PlayerController player)
    {
        var origin = cam.transform.position + cam.transform.forward * 0.3f;
        var useRay = new Ray(origin, cam.transform.forward);
        ShowRayLine(useRay.origin, useRay.origin + useRay.direction * PickupRayDistance);
        if (!Physics.Raycast(useRay, out var hit, PickupRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return;

        if (_carriedObject != null)
            return;

        var root = hit.collider.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;

        if (root.name != "TreeFelled" && root.name != "BranchTop" && root.name != "RockDebris")
            return;

        if (root.GetComponent<Rigidbody>() == null) return;

        _carriedObject = root;
        root.GetComponent<Rigidbody>().isKinematic = true;
        var cols = root.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            c.enabled = false;
        root.transform.SetParent(cam.transform);
        root.transform.localPosition = new Vector3(0.7f, -0.4f, 1.8f);
        root.transform.localRotation = Quaternion.identity;
        _uiManager.ShowMessage("Lifted.", 1f);
    }

    private void DropCarriedObject(PlayerController player)
    {
        if (_carriedObject == null) return;

        _carriedObject.transform.SetParent(null);
        var rb = _carriedObject.GetComponent<Rigidbody>();
        var cols = _carriedObject.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            c.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            var cam = GetActiveCamera();
            var throwDir = (cam != null ? cam.transform.forward : Vector3.forward) + Vector3.up * 0.3f;
            rb.linearVelocity = throwDir.normalized * 5f;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
            _carriedObject.transform.position = cam != null ? cam.transform.position + cam.transform.forward * 1.2f : player.transform.position + Vector3.up;
        }
        else if (player != null)
        {
            _carriedObject.transform.position = player.transform.position + player.transform.forward * 1.5f + Vector3.up * 0.5f;
        }
        _carriedObject = null;
        _uiManager.ShowMessage("Dropped.", 1f);
    }

    private (string material, float amount) GetCarriedResourceInfo(GameObject obj)
    {
        if (obj.name == "TreeFelled")
        {
            var trunk = obj.transform.Find("Trunk");
            float amount = trunk != null ? trunk.localScale.x * trunk.localScale.y * trunk.localScale.z * 5f : 0.05f;
            return ("wood", amount);
        }
        if (obj.name == "BranchTop")
        {
            var part = obj.transform.Find("BranchTopPart");
            float amount = part != null ? part.localScale.x * part.localScale.y * part.localScale.z * 5f : 0.05f;
            return ("wood", amount);
        }
        if (obj.name == "RockDebris")
        {
            var s = obj.transform.localScale;
            float amount = s.x * s.y * s.z * 20f;
            return ("stone", amount);
        }
        return (null, 0);
    }

    private void TryAutoDeposit()
    {
        if (_carriedObject == null || _worldBuilder == null || _uiManager == null) return;

        var cols = Physics.OverlapSphere(_carriedObject.transform.position, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            if (!_worldBuilder.IsBlueprint(c.gameObject)) continue;
            var bp = _worldBuilder.FindBlueprint(c.gameObject);
            if (bp == null) continue;

            var (material, amount) = GetCarriedResourceInfo(_carriedObject);
            if (material == null) return;

            if (amount < 0.05f)
            {
                _uiManager.ShowMessage("Too small to use.", 1f);
                return;
            }

            if (_worldBuilder.DepositMaterial(bp, material, amount))
            {
                _uiManager.ShowMessage("Building completed!", 1.5f);
                SoundManager.Instance?.Play("hammer");
            }
            else
            {
                _uiManager.ShowMessage("Supplied " + material + " x" + amount.ToString("F2") + ".", 1.5f);
            }
            Destroy(_carriedObject);
            _carriedObject = null;
            _uiManager?.SetInfoText(null);
            return;
        }
    }

    public void ReloadGun()
    {
        if (GetSelectedItemType() != "gun")
            return;

        var ammoSlot = FindSlotFor("ammo");
        if (ammoSlot < 0)
        {
            _uiManager.ShowMessage("No ammo to reload.", 1.5f);
            return;
        }

        var ammo = _inventory[ammoSlot].Count;
        var needed = GunMaxAmmo - _gunAmmo;
        var used = Mathf.Min(needed, ammo);
        _gunAmmo += used;
        RemoveItem(ammoSlot, used);
        SoundManager.Instance?.Play("gun");
        _uiManager.ShowMessage($"Reloaded {used} ammo.", 1.5f);
        UpdateAmmoText();
    }

    private void ShootGun(Vector3 origin, Vector3 direction)
    {
        if (_gunAmmo <= 0)
        {
            _uiManager.ShowMessage("Out of ammo.", 1.5f);
            return;
        }

        _gunAmmo--;
        UpdateAmmoText();
        SoundManager.Instance?.Play("gun");
        _uiManager.ShowMessage("Bang!", 1f);

        var shotOrigin = origin + direction * 0.3f;
        if (Physics.Raycast(shotOrigin, direction, out var hit, 20f))
        {
            if (IsTree(hit.collider))
            {
                var treeRoot = FindTreeRoot(hit.collider);
                if (treeRoot != null && _worldBuilder.RemoveTree(treeRoot))
                {
                    _uiManager.ShowMessage("Shot down a tree.", 1.5f);
                }
            }
            else if (IsRock(hit.collider))
            {
                var rockRoot = hit.collider.gameObject;
                while (rockRoot.transform.parent != null && rockRoot.transform.parent.name != "WorldRoot")
                    rockRoot = rockRoot.transform.parent.gameObject;
                if (_worldBuilder.RemoveRock(rockRoot))
                {
                    _uiManager.ShowMessage("Shot rock apart.", 1.5f);
                }
            }
        }
    }

    public void AddItem(string itemType, int amount)
    {
        itemType = NormalizeItemType(itemType);
        if (string.IsNullOrEmpty(itemType) || amount <= 0)
            return;

        var slot = FindSlotFor(itemType);
        if (slot >= 0)
        {
            _inventory[slot].Count += amount;
            UpdateInventoryUI();
            return;
        }

        var empty = FindEmptySlot();
        if (empty < 0)
        {
            _uiManager.ShowMessage("Inventory full.", 1.5f);
            return;
        }

        _inventory[empty] = new InventorySlot {Type = itemType, Count = amount};
        UpdateInventoryUI();
    }

    public bool RemoveItem(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= _inventory.Length)
            return false;

        var slot = _inventory[slotIndex];
        if (slot == null)
            return false;

        slot.Count -= amount;
        if (slot.Count <= 0)
            _inventory[slotIndex] = null;
        UpdateInventoryUI();
        return true;
    }

    public string GetSelectedItemType()
    {
        if (_selectedSlot < 0 || _selectedSlot >= _inventory.Length)
            return null;
        var slot = _inventory[_selectedSlot];
        return slot?.Type;
    }

    public InventorySlotSave[] GetInventorySave()
    {
        var result = new List<InventorySlotSave>();
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null)
                result.Add(new InventorySlotSave {Slot = i, Type = _inventory[i].Type, Count = _inventory[i].Count});
        }
        return result.ToArray();
    }

    public void LoadInventorySave(InventorySlotSave[] data)
    {
        for (int i = 0; i < _inventory.Length; i++)
            _inventory[i] = null;

        if (data == null)
            return;

        foreach (var slot in data)
        {
            if (slot.Slot >= 0 && slot.Slot < _inventory.Length)
                _inventory[slot.Slot] = new InventorySlot {Type = NormalizeItemType(slot.Type), Count = slot.Count};
        }

        UpdateInventoryUI();
    }

    public int GetGunAmmo() => _gunAmmo;
    public int GetGunMaxAmmo() => GunMaxAmmo;

    public void SetGunAmmo(int amount)
    {
        _gunAmmo = Mathf.Clamp(amount, 0, GunMaxAmmo);
        UpdateAmmoText();
    }

    private int FindSlotFor(string itemType)
    {
        itemType = NormalizeItemType(itemType);
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null && _inventory[i].Type == itemType)
                return i;
        }
        return -1;
    }

    private string NormalizeItemType(string itemType)
    {
        if (string.IsNullOrEmpty(itemType))
            return itemType;

        var normalized = itemType.Trim().ToLowerInvariant().Replace(" ", "_");

        // Map variant names from Python source / user data to canonical internal keys
        if (normalized == "mì_hảo_hảo" || normalized == "mi_hao_hao" || normalized == "mi_hao_hao")
            return "mi_hao_hao";

        return normalized;
    }

    public int FindEmptySlot()
    {
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] == null)
                return i;
        }
        return -1;
    }

    public int CountItem(string itemType)
    {
        itemType = NormalizeItemType(itemType);
        int total = 0;
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null && _inventory[i].Type == itemType)
                total += _inventory[i].Count;
        }
        return total;
    }

    public void RemoveAllItems(string itemType)
    {
        itemType = NormalizeItemType(itemType);
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null && _inventory[i].Type == itemType)
                _inventory[i] = null;
        }
        UpdateInventoryUI();
    }

    private void CreateToolContainer()
    {
        _toolContainer = new GameObject("ToolContainer");
        _toolContainer.transform.SetParent(Camera.main != null ? Camera.main.transform : transform);
        _toolContainer.transform.localPosition = new Vector3(0.7f, -0.6f, 1.5f);
        _toolContainer.transform.localRotation = Quaternion.identity;
        _toolContainer.transform.localScale = Vector3.one;
    }

    private void CreateRayVisualizer()
    {
        var rayObject = new GameObject("PickupRayVisualizer");
        rayObject.transform.SetParent(transform);
        _rayRenderer = rayObject.AddComponent<LineRenderer>();
        _rayRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _rayRenderer.startWidth = 0.02f;
        _rayRenderer.endWidth = 0.02f;
        _rayRenderer.positionCount = 2;
        _rayRenderer.startColor = Color.red;
        _rayRenderer.endColor = Color.red;
        _rayRenderer.useWorldSpace = true;
        _rayRenderer.enabled = false;
    }

    private void ShowRayLine(Vector3 start, Vector3 end)
    {
        if (_rayRenderer == null)
            CreateRayVisualizer();

        _rayRenderer.SetPosition(0, start);
        _rayRenderer.SetPosition(1, end);
        _rayRenderer.enabled = true;
        CancelInvoke(nameof(HideRayLine));
        Invoke(nameof(HideRayLine), 0.15f);
    }

    private void HideRayLine()
    {
        if (_rayRenderer != null)
            _rayRenderer.enabled = false;
    }

    private Camera GetActiveCamera()
    {
        return Camera.main != null ? Camera.main : Camera.current ?? Object.FindAnyObjectByType<Camera>();
    }

    private void EnsureToolContainerAttached()
    {
        if (_toolContainer == null)
            return;

        var cam = GetActiveCamera();
        if (cam == null)
            return;

        if (_toolContainer.transform.parent != cam.transform)
        {
            _toolContainer.transform.SetParent(cam.transform);
            _toolContainer.transform.localPosition = new Vector3(0.7f, -0.6f, 1.5f);
            _toolContainer.transform.localRotation = Quaternion.identity;
            _toolContainer.transform.localScale = Vector3.one;
        }
    }

    private void CreateToolModels()
    {
        // Base item
        CreateToolModel("arm", new Color(0.6f, 0.3f, 0.1f));
        
        // Tools
        CreateToolModel("axe", new Color(0.5f, 0.2f, 0.05f));
        CreateToolModel("pickaxe", new Color(0.5f, 0.5f, 0.5f));
        CreateToolModel("hoe", new Color(0.4f, 0.4f, 0.4f));
        CreateToolModel("hammer", new Color(0.2f, 0.2f, 0.2f));
        CreateToolModel("sword", new Color(0.8f, 0.8f, 0.8f));
        CreateToolModel("gun", new Color(0.05f, 0.05f, 0.05f));
        CreateToolModel("scythe", new Color(0.4f, 0.4f, 0.4f));
        
        // Items & seeds
        CreateToolModel("ammo", new Color(0.85f, 0.85f, 0.85f));
        CreateToolModel("fertilizer", new Color(0.2f, 0.7f, 0.2f));
        CreateToolModel("wheat_seed", new Color(0.7f, 0.5f, 0.2f));
        CreateToolModel("peashooter_seed", new Color(1f, 0.86f, 0.31f));
        CreateToolModel("corn_seed", new Color(1f, 0.86f, 0.24f));
        CreateToolModel("potato_seed", new Color(0.7f, 0.5f, 0.2f));
        CreateToolModel("carrot_seed", new Color(1f, 0.5f, 0f));
        CreateToolModel("tomato_seed", new Color(1f, 0.3f, 0.1f));
        CreateToolModel("strawberry_seed", new Color(1f, 0.2f, 0.2f));
        CreateToolModel("pumpkin_seed", new Color(1f, 0.7f, 0.1f));
        CreateToolModel("onion_seed", new Color(0.7f, 0.5f, 0.3f));
        CreateToolModel("sugarcane_seed", new Color(0.4f, 0.7f, 0.2f));
        CreateToolModel("rice_seed", new Color(0.9f, 0.85f, 0.4f));
        CreateToolModel("watering_can", new Color(0.4f, 0.5f, 0.6f));
        
        // Crops & resources
        CreateToolModel("wood", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("stone", new Color(0.5f, 0.5f, 0.5f));
        CreateToolModel("wheat", new Color(1f, 1f, 0.5f));
        CreateToolModel("damaged_wheat", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("corn", new Color(1f, 0.85f, 0.2f));
        CreateToolModel("damaged_corn", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("potato", new Color(0.627f, 0.431f, 0.235f));
        CreateToolModel("damaged_potato", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("carrot", new Color(1f, 0.55f, 0.1f));
        CreateToolModel("damaged_carrot", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("tomato", new Color(1f, 0.2f, 0.1f));
        CreateToolModel("damaged_tomato", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("strawberry", new Color(1f, 0.15f, 0.15f));
        CreateToolModel("damaged_strawberry", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("pumpkin", new Color(1f, 0.6f, 0.1f));
        CreateToolModel("damaged_pumpkin", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("onion", new Color(0.8f, 0.5f, 0.2f));
        CreateToolModel("damaged_onion", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("sugarcane", new Color(0.3f, 0.7f, 0.15f));
        CreateToolModel("damaged_sugarcane", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("rice", new Color(1f, 0.9f, 0.3f));
        CreateToolModel("damaged_rice", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("field", new Color(0.45f, 0.28f, 0.12f));
        CreateToolModel("mobspawner", new Color(0.25f, 0.25f, 0.25f));
        
        // Special items
        CreateToolModel("mi_hao_hao", new Color(0.8f, 0.3f, 0.2f));
    }

    private void CreateToolModel(string toolType, Color color)
    {
        var root = new GameObject(toolType + "_Tool");
        root.transform.SetParent(_toolContainer.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        if (ToolModelPrefab != null)
        {
            var prefabInstance = Instantiate(ToolModelPrefab, root.transform);
            prefabInstance.name = toolType + "_Model";
            prefabInstance.transform.localPosition = Vector3.zero;
            prefabInstance.transform.localRotation = Quaternion.identity;
            prefabInstance.transform.localScale = Vector3.one;
            ApplyToolMaterial(prefabInstance);
        }
        else
        {
            switch (toolType)
            {
                case "fertilizer":
                    var fertPart = new GameObject("Fertilizer");
                    fertPart.transform.SetParent(root.transform);
                    fertPart.transform.localPosition = Vector3.zero;
                    ItemBuilder.BuildFertilizer(fertPart.transform);
                    if (FertilizerTexture != null)
                        ApplyTextureToAllChildren(fertPart, FertilizerTexture);
                    break;
                case "peashooter_seed":
                    var peashooterPart = new GameObject("PeashooterSeed");
                    peashooterPart.transform.SetParent(root.transform);
                    peashooterPart.transform.localPosition = Vector3.zero;
                    ItemBuilder.BuildPeashooterSeed(peashooterPart.transform);
                    if (PeashooterSeedTexture != null)
                        ApplyTextureToAllChildren(peashooterPart, PeashooterSeedTexture);
                    break;
                case "wheat":
                    ItemBuilder.BuildWheatPickup(root.transform, new Color(1f, 1f, 0.5f));
                    break;
                case "mi_hao_hao":
                    if (MiHaoHaoModel != null)
                    {
                        var miHaoHaoInstance = Instantiate(MiHaoHaoModel, root.transform);
                        miHaoHaoInstance.name = "MiHaoHao_Model";
                        miHaoHaoInstance.transform.localPosition = Vector3.zero;
                        miHaoHaoInstance.transform.localRotation = Quaternion.identity;
                        miHaoHaoInstance.transform.localScale = Vector3.one;
                        if (MiHaoHaoTexture != null)
                            ApplyTextureToAllChildren(miHaoHaoInstance, MiHaoHaoTexture);
                    }
                    else
                    {
                        ItemBuilder.BuildMiHaoHao(root.transform);
                    }
                    break;
                default:
                    ItemBuilder.BuildItem(root.transform, toolType);
                    break;
            }
        }
        root.SetActive(false);
        _toolModels[toolType] = root;
        DestroyAllColliders(root);
    }

    private void ShowActiveToolModel()
    {
        foreach (var kvp in _toolModels)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(kvp.Key == GetSelectedItemType());
        }
    }

    private void DestroyAllColliders(GameObject root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            Destroy(collider);
        }
    }

    private void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        if (ToolMaterial != null)
        {
            renderer.material = ToolMaterial;
            if (ToolTexture != null)
                renderer.material.mainTexture = ToolTexture;
        }
        else if (ToolTexture != null)
        {
            var textureMaterial = new Material(Shader.Find("Standard"));
            textureMaterial.mainTexture = ToolTexture;
            renderer.material = textureMaterial;
        }
        else
        {
            renderer.material.color = color;
        }
    }

    private void ApplyToolMaterial(GameObject root)
    {
        if (ToolMaterial == null && ToolTexture == null)
            return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (ToolMaterial != null)
            {
                renderer.material = ToolMaterial;
                if (ToolTexture != null)
                    renderer.material.mainTexture = ToolTexture;
            }
            else if (ToolTexture != null)
            {
                var textureMaterial = new Material(Shader.Find("Standard"));
                textureMaterial.mainTexture = ToolTexture;
                renderer.material = textureMaterial;
            }
        }
    }

    private void ApplyTextureToAllChildren(GameObject go, Texture2D texture)
    {
        if (texture == null)
            return;

        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            var textureMaterial = new Material(Shader.Find("Standard"));
            textureMaterial.mainTexture = texture;
            renderer.material = textureMaterial;
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
        UpdateAmmoText();
    }

    private void UpdateAmmoText()
    {
        _uiManager?.UpdateAmmoText(_gunAmmo, GunMaxAmmo);
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

    private void ToggleBuildingMenu()
    {
        if (_buildingMenuOpen)
        {
            CloseBuildingMenu();
            return;
        }

        if (_buildingMenuPanel == null)
            CreateBuildingMenu();

        _buildingMenuOpen = true;
        _buildingMenuPanel.SetActive(true);
        GameManager.Instance?.TogglePause(true);
        GameManager.Instance?.UIManager?.ShowPauseMenu(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseBuildingMenu()
    {
        _buildingMenuOpen = false;
        if (_buildingMenuPanel != null)
            _buildingMenuPanel.SetActive(false);
        GameManager.Instance?.TogglePause(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UpdateBuildingPreviewVisibility();
    }

    private void CreateBuildingMenu()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        float sw = Screen.width;
        float sh = Screen.height;
        float panelW = Mathf.Min(sw * 0.50f, 500f);
        float panelH = Mathf.Min(sh * 0.65f, 420f);
        float fontS = Mathf.Max(14f, sh / 42f);
        float btnH = sh * 0.065f;
        float padding = sh * 0.015f;

        _buildingMenuPanel = new GameObject("BuildingMenu");
        _buildingMenuPanel.transform.SetParent(canvas.transform, false);
        var panelRect = _buildingMenuPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelW, panelH);
        var panelImg = _buildingMenuPanel.AddComponent<Image>();
        panelImg.color = new Color(0.18f, 0.2f, 0.27f, 0.95f);

        var title = MakeBMText("BuildTitle", _buildingMenuPanel.transform, "Xây dựng",
            new Vector2(0f, panelH * 0.42f), new Vector2(panelW - 80, fontS * 1.6f), (int)(fontS * 1.3f));

        MakeBMButton("BuildClose", _buildingMenuPanel.transform, "X",
            new Vector2(panelW * 0.44f, panelH * 0.42f), new Vector2(btnH, btnH),
            (int)fontS, new Color(0.75f, 0.38f, 0.41f), CloseBuildingMenu);

        float headerH = panelH * 0.18f;
        float viewportH = panelH - headerH - padding * 2;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(_buildingMenuPanel.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(0.5f, 0.5f);
        vpRect.anchorMax = new Vector2(0.5f, 0.5f);
        vpRect.pivot = new Vector2(0.5f, 0.5f);
        vpRect.anchoredPosition = new Vector2(0f, -padding);
        vpRect.sizeDelta = new Vector2(panelW - padding * 2, viewportH);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(0.12f, 0.13f, 0.18f, 1f);
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = padding;
        layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);

        var scrollRect = _buildingMenuPanel.AddComponent<ScrollRect>();
        scrollRect.viewport = vpRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;

        var wb = WorldBuilder.Instance;
        int count = wb != null ? wb.BuildingCount : 0;

        for (int i = 0; i < count; i++)
        {
            var def = wb.GetBuildingByIndex(i);
            int index = i;

            string costLabel = "";
            if (def.WoodCost > 0) costLabel += def.WoodCost + "🪵 ";
            if (def.StoneCost > 0) costLabel += def.StoneCost + "🪨";
            costLabel = costLabel.Trim();
            string btnLabel = GetVietnameseBuildingName(def.Name) + "    " + costLabel;

            var btn = MakeBMButton("BuildBtn_" + i, content.transform, btnLabel,
                Vector2.zero, new Vector2(panelW - padding * 4, btnH),
                (int)fontS, new Color(0.26f, 0.3f, 0.37f),
                () => SelectBuilding(index));

            var le = btn.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = btnH;
        }

        _buildingMenuPanel.SetActive(false);
    }

    private void SelectBuilding(int index)
    {
        var wb = WorldBuilder.Instance;
        if (wb == null) return;
        wb.CurrentBuildingIndex = index;
        _buildingChosen = true;
        var def = wb.GetBuildingByIndex(index);
        CloseBuildingMenu();
        _uiManager?.ShowMessage("Da chon: " + GetVietnameseBuildingName(def.Name) + ". Nhap trai de dat.", 2f);
    }

    private string GetVietnameseBuildingName(string name)
    {
        return name switch
        {
            "wood_wall" => "Tuong go",
            "stone_wall" => "Tuong da",
            "fence" => "Hang rao",
            "watchtower" => "Thap canh",
            "small_house" => "Nha nho",
            "wood_floor" => "San go",
            "stone_floor" => "San da",
            "stair" => "Cau thang",
            "table" => "Ban",
            "chair" => "Ghe",
            "sofa" => "Ghe so pha",
            _ => name
        };
    }

    private TMP_Text MakeBMText(string name, Transform parent, string text,
        Vector2 pos, Vector2 size, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            tmp.font = _uiManager.defaultTmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private Button MakeBMButton(string name, Transform parent, string label,
        Vector2 pos, Vector2 size, int fontSize, Color color,
        UnityEngine.Events.UnityAction callback)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(callback);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var tr = textGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            tmp.font = _uiManager.defaultTmpFont;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }
}
