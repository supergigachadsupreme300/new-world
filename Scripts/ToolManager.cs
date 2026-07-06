using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static CountryLife.Helpers.PickupVisualHelper;

public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    [Header("Tool Graphics Overrides")]
    public GameObject ToolModelPrefab;
    public Material ToolMaterial;
    public Texture2D ToolTexture;

    [Header("Item Textures")]
    public Texture2D FertilizerTexture;
    public Texture2D SeedTexture;
    public Texture2D PeashooterSeedTexture;
    public Texture2D CornSeedTexture;

    [Header("Special Item Models")]
    public GameObject WheatModel;
    public Texture2D WheatTexture;
    public GameObject MiHaoHaoModel;
    public Texture2D MiHaoHaoTexture;

    private UIManager _uiManager;
    private WorldBuilder _worldBuilder;
    private readonly InventorySlot[] _inventory = new InventorySlot[10];
    private int _selectedSlot = -1;
    private readonly Dictionary<string, GameObject> _toolModels = new Dictionary<string, GameObject>();
    private GameObject _toolContainer;
    private LineRenderer _rayRenderer;
    private int _gunAmmo;
    private const int GunMaxAmmo = 6;
    private GameObject _carriedObject;
    private const float PickupRayDistance = 4f;
    private const float UseRayDistance = 10f;

    public void Initialize(UIManager uiManager, WorldBuilder worldBuilder)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        _uiManager = uiManager;
        _worldBuilder = worldBuilder;
        CreateToolContainer();
        CreateRayVisualizer();
        CreateToolModels();
        ResetSelection();
        UpdateInventoryUI();
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame || GameManager.Instance.GamePaused)
            return;

        if (Keyboard.current == null)
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
                    _worldBuilder.UpdatePreviewPosition(hit.point, true);
                else
                    _worldBuilder.UpdatePreviewPosition(Vector3.zero, false);
            }
        }

        if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
            SelectSlot(_selectedSlot - 1);
        if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
            SelectSlot(_selectedSlot + 1);
    }

    private void LateUpdate()
    {
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
                DropCarriedObject(player);
            }
            else
            {
                TryPickupFelledTree(cam, player);
            }
            return;
        }

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
                return;
            }

            if (selectedItem == "pickaxe" && IsRock(hit.collider))
            {
                var rockRoot = hit.collider.gameObject;
                while (rockRoot.transform.parent != null && rockRoot.transform.parent.name != "WorldRoot")
                    rockRoot = rockRoot.transform.parent.gameObject;

                if (_worldBuilder.HitRock(rockRoot, hit.point, hit.normal))
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
                var placePos = hit.point;
                if (_worldBuilder.PlaceBuilding(placePos))
                {
                    SoundManager.Instance?.Play("hammer");
                    _uiManager.ShowMessage("Building placed.", 1.5f);
                }
                else
                {
                    _uiManager.ShowMessage("Cannot place building here.", 1.5f);
                }
                return;
            }

            if (selectedItem == "wheat_seed")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.Tilled && !field.HasCrop)
                {
                    if (_worldBuilder.PlantCrop(field, "wheat"))
                    {
                        RemoveItem(_selectedSlot, 1);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage("Planted wheat.", 1.5f);
                    }
                }
                else
                {
                    _uiManager.ShowMessage("Use seed on a tilled field.", 1.5f);
                }
                return;
            }

            if (selectedItem == "corn_seed")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.Tilled && !field.HasCrop)
                {
                    if (_worldBuilder.PlantCrop(field, "corn"))
                    {
                        RemoveItem(_selectedSlot, 1);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage("Planted corn.", 1.5f);
                    }
                }
                else
                {
                    _uiManager.ShowMessage("Use corn seed on a tilled field.", 1.5f);
                }
                return;
            }

            if (selectedItem == "corn")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.Tilled && !field.HasCrop)
                {
                    if (_worldBuilder.PlantCrop(field, "corn"))
                    {
                        RemoveItem(_selectedSlot, 1);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage("Planted corn.", 1.5f);
                    }
                }
                return;
            }

            if (selectedItem == "potato" || selectedItem == "potato_seed")
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.Tilled && !field.HasCrop)
                {
                    if (_worldBuilder.PlantCrop(field, "potato"))
                    {
                        RemoveItem(_selectedSlot, 1);
                        SoundManager.Instance?.Play("pop");
                        _uiManager.ShowMessage("Planted potato.", 1.5f);
                    }
                }
                return;
            }

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

    public void TryPickupNearby()
    {
        var cam = GetActiveCamera();
        if (cam == null)
            return;

        var origin = cam.transform.position + cam.transform.forward * 0.3f;
        var ray = new Ray(origin, cam.transform.forward);
        ShowRayLine(ray.origin, ray.origin + ray.direction * PickupRayDistance);
        if (Physics.Raycast(ray, out var hit, PickupRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            Debug.Log($"TryPickupNearby: ray hit {hit.collider.gameObject.name}");
            if (TryPickupTool(hit.collider))
                return;

            if (IsTree(hit.collider))
            {
                var treeRoot = FindTreeRoot(hit.collider);
                if (treeRoot != null && _worldBuilder.RemoveTree(treeRoot))
                {
                    AddItem("wood", 1);
                    SoundManager.Instance?.Play("axe");
                    _uiManager.ShowMessage("Picked up wood from tree.", 1.5f);
                }
            }
            else if (IsRock(hit.collider))
            {
                var rockRoot = hit.collider.gameObject;
                while (rockRoot.transform.parent != null && rockRoot.transform.parent.name != "WorldRoot")
                    rockRoot = rockRoot.transform.parent.gameObject;
                if (_worldBuilder.RemoveRock(rockRoot))
                {
                    SoundManager.Instance?.Play("pickaxe");
                }
            }
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
        var dropPosition = Vector3.zero;
        if (player != null)
            dropPosition = player.transform.position + player.transform.forward * 1.5f + Vector3.up * 0.5f;

        if (RemoveItem(_selectedSlot, 1))
        {
            if (_worldBuilder != null)
                _worldBuilder.SpawnPickup(itemType, dropPosition);

            _uiManager.ShowMessage($"Dropped {itemType}.", 1.5f);
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
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }
        var cols = _carriedObject.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            c.enabled = true;

        if (player != null)
        {
            var dropPos = player.transform.position + player.transform.forward * 1.5f + Vector3.up * 0.5f;
            _carriedObject.transform.position = dropPos;
        }
        _carriedObject = null;
        _uiManager.ShowMessage("Dropped.", 1f);
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
                    AddItem("wood", 1);
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
        
        // Crops & resources
        CreateToolModel("wood", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("stone", new Color(0.5f, 0.5f, 0.5f));
        CreateToolModel("wheat", new Color(1f, 1f, 0.5f));
        CreateToolModel("damaged_wheat", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("corn", new Color(1f, 0.85f, 0.2f));
        CreateToolModel("damaged_corn", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("potato", new Color(0.627f, 0.431f, 0.235f));
        CreateToolModel("damaged_potato", new Color(0.6f, 0.4f, 0.2f));
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
}
