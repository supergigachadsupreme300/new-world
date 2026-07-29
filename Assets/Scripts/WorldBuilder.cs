using System.Collections.Generic;
using UnityEngine;

public class WorldBuilder : MonoBehaviour
{
    public static WorldBuilder Instance { get; private set; }

    public int TreeCount = 200;
    public int RockCount = 100;
    public Vector3 GroundSize = new Vector3(150f, 0.2f, 150f);

    public Light SunLight;
    public GameObject GroundObject;
    public GameObject RoadObject;

    private readonly List<GameObject> _trees = new List<GameObject>();
    private readonly List<GameObject> _rocks = new List<GameObject>();
    private readonly List<FieldState> _fields = new List<FieldState>();
    private readonly List<BuildingState> _buildings = new List<BuildingState>();
    private GameObject _worldRoot;
    private GameObject _buildingPreview;

    private readonly BuildingDefinition[] _availableBuildings = new[]
    {
        new BuildingDefinition("wood_wall", new Vector3(6f, 3f, 0.5f), new Color(0.63f, 0.39f, 0.18f)),
        new BuildingDefinition("stone_wall", new Vector3(5f, 3f, 0.5f), new Color(0.41f, 0.41f, 0.41f)),
        new BuildingDefinition("fence", new Vector3(4f, 1.5f, 0.3f), new Color(0.69f, 0.51f, 0.25f)),
        new BuildingDefinition("watchtower", new Vector3(3f, 8f, 3f), new Color(0.51f, 0.33f, 0.16f)),
        new BuildingDefinition("small_house", new Vector3(8f, 5f, 8f), new Color(0.78f, 0.63f, 0.39f)),
        new BuildingDefinition("wood_floor", new Vector3(4f, 0.3f, 4f), new Color(0.71f, 0.53f, 0.27f))
    };

    private int _currentBuildingIndex;
    private int _currentRotation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void CreateWorld()
    {
        _worldRoot = new GameObject("WorldRoot");
        _worldRoot.transform.SetParent(transform);
        CreateSkyAndLight();
        CreateGround();
        BuildRoad();
        SpawnTrees(TreeCount);
        SpawnRocks(RockCount);
        BuildHouse();
        BuildShop();
        BuildWifeHouse();
        SpawnBuffalo();
        InitializeBuildingPreview();
    }

    public void ResetWorld()
    {
        foreach (var tree in _trees)
            Destroy(tree);
        _trees.Clear();

        foreach (var rock in _rocks)
            Destroy(rock);
        _rocks.Clear();

        foreach (var field in _fields)
        {
            if (field.FieldObject != null) Destroy(field.FieldObject);
            if (field.CropObject != null) Destroy(field.CropObject);
        }
        _fields.Clear();

        foreach (var building in _buildings)
        {
            if (building.Entity != null) Destroy(building.Entity);
        }
        _buildings.Clear();

        if (_buildingPreview != null)
            Destroy(_buildingPreview);

        if (RoadObject != null) Destroy(RoadObject);
        if (GroundObject != null) Destroy(GroundObject);
        if (SunLight != null) Destroy(SunLight.gameObject);
        if (_worldRoot != null) Destroy(_worldRoot);
    }

    public void UpdateWorld(float deltaTime)
    {
        foreach (var field in _fields)
        {
            if (!field.HasCrop || field.IsHarvested)
                continue;

            field.GrowTimer += deltaTime;
            if (field.GrowTimer >= field.NextStageTime && field.Stage < 3)
            {
                field.GrowTimer = 0f;
                field.Stage++;
                UpdateCropVisual(field);
            }
        }
    }

    public void SetDayNight(float hour)
    {
        if (SunLight == null)
            return;

        float normalized = Mathf.InverseLerp(0f, 24f, hour);
        var skyFactor = Mathf.Clamp01(Mathf.Cos(normalized * Mathf.PI * 2f) * -0.5f + 0.5f);
        SunLight.intensity = Mathf.Lerp(0.2f, 1.0f, skyFactor);
        RenderSettings.ambientIntensity = Mathf.Lerp(0.3f, 1f, skyFactor);
        RenderSettings.ambientLight = Color.Lerp(new Color(0.08f, 0.08f, 0.15f), Color.white, skyFactor);
    }

    public bool IsOnRoad(Vector3 position)
    {
        if (RoadObject == null)
            return false;

        var roadPos = RoadObject.transform.position;
        var size = RoadObject.transform.localScale;
        var halfX = size.x * 0.5f;
        var halfZ = size.z * 0.5f;
        return position.x >= roadPos.x - halfX && position.x <= roadPos.x + halfX && position.z >= roadPos.z - halfZ && position.z <= roadPos.z + halfZ;
    }

    public FieldState GetFieldAt(Vector3 position)
    {
        foreach (var field in _fields)
        {
            if (Vector3.Distance(field.FieldObject.transform.position, position) < 2f)
                return field;
        }
        return null;
    }

    public FieldState TillGround(Vector3 position)
    {
        position.y = 0f;
        var field = GetFieldAt(position);
        if (field != null)
        {
            field.Tilled = true;
            UpdateFieldVisual(field);
            return field;
        }

        var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tile.name = "FieldTile";
        tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        tile.transform.position = position + Vector3.up * 0.01f;
        tile.transform.localScale = new Vector3(2f, 2f, 2f);
        tile.transform.SetParent(_worldRoot.transform);
        tile.GetComponent<MeshRenderer>().material.color = new Color(0.45f, 0.28f, 0.12f);
        tile.AddComponent<BoxCollider>().isTrigger = true;

        field = new FieldState
        {
            FieldObject = tile,
            Tilled = true,
            Stage = 0,
            HasCrop = false,
            GrowTimer = 0f,
            NextStageTime = 12f
        };
        _fields.Add(field);
        return field;
    }

    public bool PlantCrop(FieldState field, string cropType)
    {
        if (field == null || !field.Tilled || field.HasCrop)
            return false;

        field.CropType = cropType;
        field.HasCrop = true;
        field.Stage = 1;
        field.GrowTimer = 0f;
        field.NextStageTime = 12f;
        UpdateCropVisual(field);
        return true;
    }

    public bool HarvestField(FieldState field, out string harvestedItem)
    {
        harvestedItem = null;
        if (field == null || !field.HasCrop || field.Stage < 3)
            return false;

        harvestedItem = field.CropType switch
        {
            "wheat" => "wheat",
            "corn" => "corn",
            "potato" => "potato",
            _ => field.CropType
        };

        if (field.CropObject != null)
            Destroy(field.CropObject);

        field.HasCrop = false;
        field.IsHarvested = true;
        field.CropType = null;
        field.Stage = 0;
        UpdateFieldVisual(field);
        return true;
    }

    public bool RemoveTree(GameObject tree)
    {
        if (tree == null)
            return false;

        if (_trees.Contains(tree))
        {
            Destroy(tree);
            _trees.Remove(tree);
            return true;
        }
        return false;
    }

    public bool RemoveRock(GameObject rock)
    {
        if (rock == null)
            return false;
        if (_rocks.Contains(rock))
        {
            Destroy(rock);
            _rocks.Remove(rock);
            return true;
        }
        return false;
    }

    public void CycleBuildingType(int delta)
    {
        _currentBuildingIndex = (_currentBuildingIndex + delta + _availableBuildings.Length) % _availableBuildings.Length;
        UpdateBuildingPreview();
    }

    public void RotateBuildingPreview(int degrees)
    {
        _currentRotation = (_currentRotation + degrees) % 360;
        UpdateBuildingPreview();
    }

    public bool PlaceBuilding(Vector3 position)
    {
        var definition = _availableBuildings[_currentBuildingIndex];
        var size = definition.Size;
        if (!CanPlaceBuilding(position, size, _currentRotation))
            return false;

        var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = definition.Name;
        building.transform.position = position + Vector3.up * (size.y * 0.5f);
        building.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
        building.transform.localScale = size;
        building.GetComponent<MeshRenderer>().material.color = definition.Color;
        building.AddComponent<BoxCollider>();
        building.transform.SetParent(_worldRoot.transform);

        _buildings.Add(new BuildingState
        {
            Entity = building,
            Type = definition.Name,
            Position = position,
            Rotation = _currentRotation,
            CurrentHealth = 100,
            MaxHealth = 100
        });
        return true;
    }

    public bool CanPlaceBuilding(Vector3 position, Vector3 size, int rotation)
    {
        var half = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
        var bounds = new Bounds(position + Vector3.up * half.y, new Vector3(size.x, size.y, size.z));

        foreach (var building in _buildings)
        {
            if (building.Entity == null)
                continue;
            if (bounds.Intersects(building.Entity.GetComponent<Collider>().bounds))
                return false;
        }
        return true;
    }

    private void CreateSkyAndLight()
    {
        var sky = GameObject.FindObjectOfType<Light>();
        if (sky == null)
        {
            var sunObject = new GameObject("SunLight");
            SunLight = sunObject.AddComponent<Light>();
            SunLight.type = LightType.Directional;
            SunLight.color = new Color(1f, 0.98f, 0.92f);
            SunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sunObject.transform.SetParent(_worldRoot.transform);
        }
        else
        {
            SunLight = sky;
        }
    }

    private void CreateGround()
    {
        GroundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        GroundObject.name = "Ground";
        GroundObject.transform.SetParent(_worldRoot.transform);
        GroundObject.transform.localScale = new Vector3(GroundSize.x / 10f, 1f, GroundSize.z / 10f);
        GroundObject.transform.position = Vector3.zero;

        var renderer = GroundObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            var texture = Resources.Load<Texture2D>("Textures/grass");
            if (texture != null)
                renderer.material.mainTexture = texture;
            else
                renderer.material.color = new Color(0.3f, 0.6f, 0.25f);
        }
        GroundObject.AddComponent<BoxCollider>().size = new Vector3(10f, 0.01f, 10f);
    }

    private void BuildRoad()
    {
        RoadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RoadObject.name = "Road";
        RoadObject.transform.SetParent(_worldRoot.transform);
        RoadObject.transform.localScale = new Vector3(10f, 0.1f, 80f);
        RoadObject.transform.position = new Vector3(14f, 0.05f, 30f);
        var renderer = RoadObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Color.black;

        var collider = RoadObject.GetComponent<BoxCollider>();
        if (collider == null)
            RoadObject.AddComponent<BoxCollider>();
    }

    private void SpawnTrees(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var position = GetRandomWorldPosition();
            if (Mathf.Abs(position.x) <= 9f && Mathf.Abs(position.z) <= 9f)
            {
                i--;
                continue;
            }

            var tree = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tree.name = "Tree" + i;
            tree.tag = "Tree";
            tree.transform.SetParent(_worldRoot.transform);
            tree.transform.position = position + Vector3.up * 2.5f;
            tree.transform.localScale = new Vector3(2f, 6f, 2f);
            var renderer = tree.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.15f, 0.45f, 0.1f);
            _trees.Add(tree);
        }
    }

    private void SpawnRocks(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var position = GetRandomWorldPosition();
            if (Mathf.Abs(position.x) <= 9f && Mathf.Abs(position.z) <= 9f)
            {
                i--;
                continue;
            }

            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock" + i;
            rock.tag = "Rock";
            rock.transform.SetParent(_worldRoot.transform);
            rock.transform.position = position + Vector3.up * 1f;
            rock.transform.localScale = new Vector3(2f, 2f, 2f);
            var renderer = rock.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.gray;
            _rocks.Add(rock);
        }
    }

    private void BuildHouse()
    {
        var houseRoot = new GameObject("PlayerHouse");
        houseRoot.transform.SetParent(_worldRoot.transform);
        houseRoot.transform.position = Vector3.zero;

        CreateWall(houseRoot.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, -5f), new Color(0.63f, 0.39f, 0.18f));
        CreateWall(houseRoot.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, 5f), new Color(0.63f, 0.39f, 0.18f));
        CreateWall(houseRoot.transform, new Vector3(0.5f, 5f, 10f), new Vector3(-5f, 2.5f, 0f), new Color(0.63f, 0.39f, 0.18f));
        CreateWall(houseRoot.transform, new Vector3(10f, 0.5f, 10f), new Vector3(0f, 0f, 0f), new Color(0.63f, 0.39f, 0.18f));
    }

    private void BuildShop()
    {
        var shopRoot = new GameObject("Shop");
        shopRoot.transform.SetParent(_worldRoot.transform);
        shopRoot.transform.position = new Vector3(0f, 0f, 60f);
        CreateWall(shopRoot.transform, new Vector3(8f, 4f, 0.5f), new Vector3(0f, 2f, -4f), new Color(0.4f, 0.4f, 0.55f));
        CreateWall(shopRoot.transform, new Vector3(8f, 4f, 0.5f), new Vector3(0f, 2f, 4f), new Color(0.4f, 0.4f, 0.55f));
    }

    private void BuildWifeHouse()
    {
        var spouseRoot = new GameObject("WifeHouse");
        spouseRoot.transform.SetParent(_worldRoot.transform);
        spouseRoot.transform.position = new Vector3(30f, 0f, 0f);
        CreateWall(spouseRoot.transform, new Vector3(7f, 4f, 0.5f), new Vector3(0f, 2f, -3.5f), new Color(0.52f, 0.34f, 0.18f));
        CreateWall(spouseRoot.transform, new Vector3(7f, 4f, 0.5f), new Vector3(0f, 2f, 3.5f), new Color(0.52f, 0.34f, 0.18f));
    }

    private void SpawnBuffalo()
    {
        var buffalo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        buffalo.name = "Buffalo";
        buffalo.transform.SetParent(_worldRoot.transform);
        buffalo.transform.position = new Vector3(-20f, 0.5f, 70f);
        buffalo.transform.localScale = new Vector3(2f, 1f, 1f);
        var renderer = buffalo.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.4f, 0.25f, 0.1f);
    }

    private void InitializeBuildingPreview()
    {
        _buildingPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _buildingPreview.name = "BuildingPreview";
        _buildingPreview.transform.SetParent(_worldRoot.transform);
        _buildingPreview.GetComponent<Collider>().enabled = false;
        var renderer = _buildingPreview.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0f, 1f, 0f, 0.4f);
        _buildingPreview.SetActive(false);
        UpdateBuildingPreview();
    }

    private void UpdateBuildingPreview()
    {
        if (_buildingPreview == null)
            return;

        var definition = _availableBuildings[_currentBuildingIndex];
        _buildingPreview.transform.localScale = definition.Size;
        _buildingPreview.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
        _buildingPreview.SetActive(true);
    }

    private Vector3 GetRandomWorldPosition()
    {
        float half = GroundSize.x * 0.5f - 5f;
        float x = Random.Range(-half, half);
        float z = Random.Range(-half, half);
        return new Vector3(x, 0f, z);
    }

    private void CreateWall(Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(parent);
        wall.transform.localScale = scale;
        wall.transform.localPosition = position;
        var renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    private void UpdateFieldVisual(FieldState field)
    {
        if (field == null)
            return;

        if (field.IsHarvested)
        {
            field.FieldObject.GetComponent<MeshRenderer>().material.color = new Color(0.25f, 0.15f, 0.1f);
            return;
        }

        if (field.HasCrop)
        {
            field.FieldObject.GetComponent<MeshRenderer>().material.color = new Color(0.35f, 0.2f, 0.08f);
            if (field.CropObject == null)
                UpdateCropVisual(field);
            return;
        }

        field.FieldObject.GetComponent<MeshRenderer>().material.color = field.Tilled ? new Color(0.45f, 0.28f, 0.12f) : new Color(0.6f, 0.4f, 0.2f);
    }

    private void UpdateCropVisual(FieldState field)
    {
        if (field == null)
            return;

        if (field.CropObject != null)
        {
            Destroy(field.CropObject);
            field.CropObject = null;
        }

        if (!field.HasCrop)
            return;

        var crop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crop.name = field.CropType + "Crop";
        crop.transform.SetParent(field.FieldObject.transform);
        crop.transform.localPosition = Vector3.up * 0.2f;
        crop.transform.localScale = Vector3.one * (0.5f + field.Stage * 0.25f);
        var renderer = crop.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = field.CropType switch
            {
                "corn" => new Color(1f, 0.85f, 0.2f),
                "potato" => new Color(0.62f, 0.43f, 0.18f),
                _ => new Color(1f, 0.85f, 0.4f)
            };
        }
        field.CropObject = crop;
    }

    public IEnumerable<FieldState> GetAllFields() => _fields;
    public IEnumerable<BuildingState> GetAllBuildings() => _buildings;

    public FieldSaveData[] GetAllFieldsAsSave()
    {
        var saved = new FieldSaveData[_fields.Count];
        for (int i = 0; i < _fields.Count; i++)
        {
            var field = _fields[i];
            saved[i] = new FieldSaveData
            {
                position = field.FieldObject != null ? field.FieldObject.transform.position : Vector3.zero,
                tilled = field.Tilled,
                hasCrop = field.HasCrop,
                cropType = field.CropType,
                stage = field.Stage,
                growTimer = field.GrowTimer,
                isHarvested = field.IsHarvested
            };
        }
        return saved;
    }

    public void LoadFieldsFromSave(FieldSaveData[] data)
    {
        if (data == null)
            return;

        foreach (var fieldSave in data)
        {
            var field = TillGround(fieldSave.position);
            if (field != null)
            {
                field.Tilled = fieldSave.tilled;
                field.IsHarvested = fieldSave.isHarvested;
                if (fieldSave.hasCrop && !string.IsNullOrEmpty(fieldSave.cropType))
                {
                    field.HasCrop = true;
                    field.CropType = fieldSave.cropType;
                    field.Stage = fieldSave.stage;
                    field.GrowTimer = fieldSave.growTimer;
                    UpdateCropVisual(field);
                }
                UpdateFieldVisual(field);
            }
        }
    }

    public BuildingSaveData[] GetAllBuildingsAsSave()
    {
        var result = new BuildingSaveData[_buildings.Count];
        for (int i = 0; i < _buildings.Count; i++)
        {
            var b = _buildings[i];
            result[i] = new BuildingSaveData
            {
                type = b.Type,
                position = b.Position,
                rotation = b.Rotation,
                currentHealth = b.CurrentHealth,
                maxHealth = b.MaxHealth
            };
        }
        return result;
    }

    public void LoadBuildingsFromSave(BuildingSaveData[] data)
    {
        if (data == null)
            return;

        foreach (var build in data)
        {
            _currentBuildingIndex = 0;
            for (int i = 0; i < _availableBuildings.Length; i++)
            {
                if (_availableBuildings[i].Name == build.type)
                {
                    _currentBuildingIndex = i;
                    break;
                }
            }
            _currentRotation = build.rotation;
            if (PlaceBuilding(build.position))
            {
                var last = _buildings[_buildings.Count - 1];
                last.CurrentHealth = build.currentHealth;
                last.MaxHealth = build.maxHealth;
            }
        }
    }

    [System.Serializable]
    public class FieldSaveData
    {
        public Vector3 position;
        public bool tilled;
        public bool hasCrop;
        public string cropType;
        public int stage;
        public float growTimer;
        public bool isHarvested;
    }

    [System.Serializable]
    public class BuildingSaveData
    {
        public string type;
        public Vector3 position;
        public int rotation;
        public int currentHealth;
        public int maxHealth;
    }

    [System.Serializable]
    public class FieldState
    {
        public GameObject FieldObject;
        public GameObject CropObject;
        public bool Tilled;
        public bool HasCrop;
        public bool IsHarvested;
        public string CropType;
        public int Stage;
        public float GrowTimer;
        public float NextStageTime;
    }

    [System.Serializable]
    public class BuildingState
    {
        public GameObject Entity;
        public string Type;
        public Vector3 Position;
        public int Rotation;
        public int CurrentHealth;
        public int MaxHealth;
    }

    private class BuildingDefinition
    {
        public string Name;
        public Vector3 Size;
        public Color Color;

        public BuildingDefinition(string name, Vector3 size, Color color)
        {
            Name = name;
            Size = size;
            Color = color;
        }
    }
}
