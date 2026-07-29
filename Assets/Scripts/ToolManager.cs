using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    private UIManager _uiManager;
    private WorldBuilder _worldBuilder;
    private readonly InventorySlot[] _inventory = new InventorySlot[10];
    private int _selectedSlot;
    private readonly Dictionary<string, GameObject> _toolModels = new Dictionary<string, GameObject>();
    private GameObject _toolContainer;
    private int _gunAmmo;
    private const int GunMaxAmmo = 6;

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
        CreateToolModels();
        ResetSelection();
        UpdateInventoryUI();
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame || GameManager.Instance.GamePaused)
            return;

        if (Input.GetKeyDown(KeyCode.LeftBracket))
            SelectSlot(_selectedSlot - 1);
        if (Input.GetKeyDown(KeyCode.RightBracket))
            SelectSlot(_selectedSlot + 1);
    }

    public void ResetSelection()
    {
        _selectedSlot = 0;
        ShowActiveToolModel();
    }

    public void SelectSlot(int index)
    {
        _selectedSlot = Mathf.Clamp(index, 0, _inventory.Length - 1);
        ShowActiveToolModel();
        UpdateInventoryUI();
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

        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out var hit, 10f))
        {
            if (selectedItem == "axe" && hit.collider.CompareTag("Tree"))
            {
                if (_worldBuilder.RemoveTree(hit.collider.gameObject))
                {
                    AddItem("wood", 1);
                    SoundManager.Instance?.Play("axe");
                    _uiManager.ShowMessage("Chopped wood!", 1.5f);
                }
                return;
            }

            if (selectedItem == "pickaxe" && hit.collider.CompareTag("Rock"))
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

            if (selectedItem == "seed")
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

            if (selectedItem == "potato")
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

            if (selectedItem == "scythe" && Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, 5f))
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
        var ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out var hit, 4f))
        {
            if (hit.collider.CompareTag("Tree"))
            {
                if (_worldBuilder.RemoveTree(hit.collider.gameObject))
                {
                    AddItem("wood", 1);
                    SoundManager.Instance?.Play("axe");
                    _uiManager.ShowMessage("Picked up wood from tree.", 1.5f);
                }
            }
            else if (hit.collider.CompareTag("Rock"))
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

    public void DropSelectedItem()
    {
        var itemType = GetSelectedItemType();
        if (itemType == null)
            return;

        if (RemoveItem(_selectedSlot, 1))
        {
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
            if (hit.collider.CompareTag("Tree"))
            {
                if (_worldBuilder.RemoveTree(hit.collider.gameObject))
                {
                    AddItem("wood", 1);
                    _uiManager.ShowMessage("Shot down a tree.", 1.5f);
                }
            }
            else if (hit.collider.CompareTag("Rock"))
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
    }

    private void CreateToolModels()
    {
        CreateToolModel("axe", new Color(0.5f, 0.2f, 0.05f));
        CreateToolModel("pickaxe", new Color(0.5f, 0.5f, 0.5f));
        CreateToolModel("hoe", new Color(0.4f, 0.4f, 0.4f));
        CreateToolModel("hammer", new Color(0.2f, 0.2f, 0.2f));
        CreateToolModel("sword", new Color(0.8f, 0.8f, 0.8f));
        CreateToolModel("gun", new Color(0.05f, 0.05f, 0.05f));
        CreateToolModel("scythe", new Color(0.4f, 0.4f, 0.4f));
        CreateToolModel("fertilizer", new Color(0.2f, 0.7f, 0.2f));
        CreateToolModel("seed", new Color(0.7f, 0.5f, 0.2f));
        CreateToolModel("corn", new Color(1f, 0.85f, 0.2f));
        CreateToolModel("potato", new Color(0.7f, 0.5f, 0.2f));
    }

    private void CreateToolModel(string toolType, Color color)
    {
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.name = toolType + "_Tool";
        model.transform.SetParent(_toolContainer.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = new Vector3(0.25f, 0.4f, 0.15f);
        var renderer = model.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
        model.SetActive(false);
        _toolModels[toolType] = model;
        Destroy(model.GetComponent<Collider>());
    }

    private void ShowActiveToolModel()
    {
        foreach (var kvp in _toolModels)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(kvp.Key == GetSelectedItemType());
        }
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
