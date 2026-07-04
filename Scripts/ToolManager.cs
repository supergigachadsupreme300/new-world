using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    [Header("Tool Graphics Overrides")]
    public GameObject ToolModelPrefab;
    public Material ToolMaterial;
    public Texture2D ToolTexture;

    private UIManager _uiManager;
    private WorldBuilder _worldBuilder;
    private readonly InventorySlot[] _inventory = new InventorySlot[10];
    private int _selectedSlot;
    private readonly Dictionary<string, GameObject> _toolModels = new Dictionary<string, GameObject>();
    private GameObject _toolContainer;
    private LineRenderer _rayRenderer;
    private int _gunAmmo;
    private const int GunMaxAmmo = 6;
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
        _selectedSlot = 0;
        ShowActiveToolModel();
        UpdateBuildingPreviewVisibility();
    }

    public void SelectSlot(int index)
    {
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

        if (selectedItem == null)
            return;

        if (selectedItem == "gun")
        {
            ShootGun(player.transform.position, player.transform.forward);
            return;
        }

        var cam = GetActiveCamera();
        if (cam == null)
            return;

        var useRay = new Ray(cam.transform.position, cam.transform.forward);
        ShowRayLine(useRay.origin, useRay.origin + useRay.direction * UseRayDistance);
        if (Physics.Raycast(useRay, out var hit, UseRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (selectedItem == "axe" && IsTree(hit.collider))
            {
                if (_worldBuilder.RemoveTree(hit.collider.gameObject))
                {
                    AddItem("wood", 1);
                    SoundManager.Instance?.Play("axe");
                    _uiManager.ShowMessage("Chopped wood!", 1.5f);
                }
                return;
            }

            if (selectedItem == "pickaxe" && IsRock(hit.collider))
            {
                if (_worldBuilder.RemoveRock(hit.collider.gameObject))
                {
                    AddItem("stone", 1);
                    SoundManager.Instance?.Play("pickaxe");
                    _uiManager.ShowMessage("Collected stone!", 1.5f);
                }
                return;
            }

            if (selectedItem == "hoe" && hit.collider.name == "Ground")
            {
                var field = _worldBuilder.TillGround(hit.point);
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

            if (selectedItem == "seed" || selectedItem == "corn_seed")
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

            var cam = GetActiveCamera();
            if (selectedItem == "scythe" && cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 5f))
            {
                var field = _worldBuilder.GetFieldAt(hit.point);
                if (field != null && field.HasCrop && field.Stage >= 3)
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

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        ShowRayLine(ray.origin, ray.origin + ray.direction * PickupRayDistance);
        if (Physics.Raycast(ray, out var hit, PickupRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            Debug.Log($"TryPickupNearby: ray hit {hit.collider.gameObject.name}");
            if (TryPickupTool(hit.collider))
                return;

            if (IsTree(hit.collider))
            {
                if (_worldBuilder.RemoveTree(hit.collider.gameObject))
                {
                    AddItem("wood", 1);
                    SoundManager.Instance?.Play("axe");
                    _uiManager.ShowMessage("Picked up wood from tree.", 1.5f);
                }
            }
            else if (IsRock(hit.collider))
            {
                if (_worldBuilder.RemoveRock(hit.collider.gameObject))
                {
                    AddItem("stone", 1);
                    SoundManager.Instance?.Play("pickaxe");
                    _uiManager.ShowMessage("Picked up stone.", 1.5f);
                }
            }
        }
    }

    private bool IsTree(Collider collider)
    {
        if (collider == null)
            return false;
        return collider.gameObject.name.StartsWith("Tree");
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

        if (Physics.Raycast(origin, direction, out var hit, 20f))
        {
            if (IsTree(hit.collider))
            {
                if (_worldBuilder.RemoveTree(hit.collider.gameObject))
                {
                    AddItem("wood", 1);
                    _uiManager.ShowMessage("Shot down a tree.", 1.5f);
                }
            }
            else if (IsRock(hit.collider))
            {
                if (_worldBuilder.RemoveRock(hit.collider.gameObject))
                {
                    AddItem("stone", 1);
                    _uiManager.ShowMessage("Shot rock apart.", 1.5f);
                }
            }
        }
    }

    public void AddItem(string itemType, int amount)
    {
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
                _inventory[slot.Slot] = new InventorySlot {Type = slot.Type, Count = slot.Count};
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
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] != null && _inventory[i].Type == itemType)
                return i;
        }
        return -1;
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i] == null)
                return i;
        }
        return -1;
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
        return Camera.main != null ? Camera.main : Camera.current ?? FindObjectOfType<Camera>();
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
        CreateToolModel("fertilizer", new Color(0.2f, 0.7f, 0.2f));
        CreateToolModel("seed", new Color(0.7f, 0.5f, 0.2f));
        CreateToolModel("peashooter_seed", new Color(1f, 0.86f, 0.31f));
        CreateToolModel("corn_seed", new Color(1f, 0.86f, 0.24f));
        CreateToolModel("potato_seed", new Color(0.7f, 0.5f, 0.2f));
        
        // Crops & resources
        CreateToolModel("wheat", new Color(1f, 1f, 0.5f));
        CreateToolModel("damaged_wheat", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("corn", new Color(1f, 0.85f, 0.2f));
        CreateToolModel("potato", new Color(0.7f, 0.5f, 0.2f));
        CreateToolModel("damaged_corn", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("damaged_potato", new Color(0.6f, 0.4f, 0.2f));
        
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
                case "arm":
                    CreateCubePart(root.transform, color, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 1f, 0.3f));
                    break;
                case "axe":
                CreateCubePart(root.transform, color * 0.6f, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.15f));
                CreateCubePart(root.transform, color * 0.9f, new Vector3(0f, 0.5f, 0.25f), new Vector3(0.2f, 0.3f, 0.7f));
                CreateCubePart(root.transform, color * 0.9f, new Vector3(0f, 0.5f, 0.5f), new Vector3(0.2f, 0.5f, 0.2f));
                break;
            case "pickaxe":
                CreateCubePart(root.transform, color * 0.6f, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.15f));
                CreateCubePart(root.transform, color * 0.75f, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.8f));
                CreateCubePart(root.transform, color * 0.75f, new Vector3(0f, 0.4f, 0.35f), new Vector3(0.25f, 0.125f, 0.25f));
                CreateCubePart(root.transform, color * 0.75f, new Vector3(0f, 0.4f, -0.35f), new Vector3(0.25f, 0.125f, 0.25f));
                break;
            case "hoe":
                CreateCubePart(root.transform, color * 0.6f, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.8f, 0.18f));
                CreateCubePart(root.transform, color * 0.8f, new Vector3(0f, 0.4f, 0.3f), new Vector3(0.3f, 0.15f, 0.7f));
                break;
            case "hammer":
                CreateCubePart(root.transform, color * 0.8f, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.15f));
                CreateCubePart(root.transform, color, new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.2f, 0.4f));
                break;
            case "sword":
                CreateCubePart(root.transform, color * 0.8f, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.4f, 0.1f));
                CreateCubePart(root.transform, new Color(1f, 0.84f, 0f), new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.05f, 0.2f));
                CreateCubePart(root.transform, Color.white, new Vector3(0f, 0.7f, 0f), new Vector3(0.05f, 1f, 0.3f));
                var swordTip = new GameObject("Sword_Tip");
                swordTip.transform.SetParent(root.transform);
                swordTip.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                swordTip.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tip.transform.SetParent(swordTip.transform, false);
                tip.transform.localScale = new Vector3(0.05f, 0.3f, 0.3f);
                ApplyColor(tip, Color.white);
                break;
            case "gun":
                CreateCubePart(root.transform, Color.black, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.5f, 0.15f));
                CreateCubePart(root.transform, color * 0.9f, new Vector3(0f, 0.2f, 0.4f), new Vector3(0.2f, 0.2f, 1f));
                break;
            case "scythe":
                CreateCubePart(root.transform, color * 0.8f, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.8f, 0.1f));
                CreateCubePart(root.transform, color, new Vector3(0.1f, 0.5f, 0f), new Vector3(0.05f, 0.35f, 0.05f), Quaternion.Euler(0f, 0f, 45f));
                CreateCubePart(root.transform, color, new Vector3(0.2f, 0.7f, 0f), new Vector3(0.05f, 0.2f, 0.05f));
                CreateCubePart(root.transform, color, new Vector3(0.1f, 0.9f, 0f), new Vector3(0.05f, 0.35f, 0.05f), Quaternion.Euler(0f, 0f, -45f));
                break;
            case "fertilizer":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.3f, 0.3f));
                break;
            case "seed":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.15f, 0f), new Vector3(0.2f, 0.2f, 0.1f));
                break;
            case "peashooter_seed":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.15f, 0f), new Vector3(0.2f, 0.2f, 0.1f));
                break;
            case "corn_seed":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.15f, 0f), new Vector3(0.2f, 0.2f, 0.1f));
                break;
            case "potato_seed":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.15f, 0f), new Vector3(0.2f, 0.2f, 0.1f));
                break;
            case "wheat":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 0.2f));
                break;
            case "damaged_wheat":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 0.2f));
                break;
            case "corn":
                // 5 rotated rectangles
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * 72f;
                    var rotated = Quaternion.Euler(0f, angle, 0f);
                    CreateCubePart(root.transform, color, new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.27f, 0.15f), rotated);
                }
                break;
            case "potato":
                // 2 overlapping spheres
                CreateSphere(root.transform, color, 0.18f);
                var potatoSmall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                potatoSmall.transform.SetParent(root.transform);
                potatoSmall.transform.localPosition = new Vector3(0.08f, 0.05f, 0f);
                potatoSmall.transform.localScale = Vector3.one * 0.14f;
                ApplyColor(potatoSmall, color * 0.9f);
                break;
            case "damaged_corn":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 0.2f));
                break;
            case "damaged_potato":
                CreateCubePart(root.transform, color, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 0.2f));
                break;
            case "mi_hao_hao":
                // Simplified noodle representation
                CreateCubePart(root.transform, color, new Vector3(0f, 0.08f, 0f), new Vector3(0.2f, 0.15f, 0.2f));
                CreateCubePart(root.transform, color * 1.1f, new Vector3(0f, 0.01f, 0f), new Vector3(0.25f, 0.08f, 0.15f));
                break;
            default:
                CreateCubePart(root.transform, color, new Vector3(0f, 0.1f, 0f), new Vector3(0.25f, 0.2f, 0.15f));
                break;
        }
        root.SetActive(false);
        _toolModels[toolType] = root;
        DestroyAllColliders(root);
    }
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

    private void CreateCubePart(Transform parent, Color color, Vector3 localPosition, Vector3 localScale)
    {
        CreateCubePart(parent, color, localPosition, localScale, Quaternion.identity);
    }

    private void CreateCubePart(Transform parent, Color color, Vector3 localPosition, Vector3 localScale, Quaternion rotation)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.transform.SetParent(parent);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = rotation;
        part.transform.localScale = localScale;
        ApplyColor(part, color);
    }

    private void CreateHandle(Transform parent, Color color, Vector3 scale)
    {
        var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.transform.SetParent(parent);
        handle.transform.localPosition = new Vector3(0f, 0f, 0f);
        handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        handle.transform.localScale = scale;
        ApplyColor(handle, color);
    }

    private void CreateHead(Transform parent, Color color, Vector3 scale, Vector3 localPosition)
    {
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(parent);
        head.transform.localPosition = localPosition;
        head.transform.localScale = scale;
        ApplyColor(head, color);
    }

    private void CreateBlade(Transform parent, Color color, Vector3 scale)
    {
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.transform.SetParent(parent);
        blade.transform.localPosition = new Vector3(0f, 0.15f, 0.2f);
        blade.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        blade.transform.localScale = scale;
        ApplyColor(blade, color);
    }

    private void CreateBody(Transform parent, Color color, Vector3 scale)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(parent);
        body.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        body.transform.localScale = scale;
        ApplyColor(body, color);
    }

    private void CreateBarrel(Transform parent, Color color, Vector3 scale, Vector3 localPosition)
    {
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.transform.SetParent(parent);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localPosition = localPosition;
        barrel.transform.localScale = scale;
        ApplyColor(barrel, color);
    }

    private void CreateSphere(Transform parent, Color color, float radius)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(parent);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * radius;
        ApplyColor(sphere, color);
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
