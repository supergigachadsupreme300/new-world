using System.Collections.Generic;
using UnityEngine;

public class WorldBuilder : MonoBehaviour
{
    public static WorldBuilder Instance { get; private set; }

    public int TreeCount = 200;
    public int RockCount = 100;
    public Vector3 GroundSize = new Vector3(150f, 0.2f, 150f);

    public int MapWidth = 20;
    public int MapDepth = 20;
    public float TileSize = 2f;
    public string TerrainBlockResourcePath = "Models/TerrainBlock";

    [Header("World Graphics Overrides")]
    public GameObject TerrainBlockPrefab;
    public Texture2D GroundTexture;
    public Material GroundMaterial;
    public GameObject TreePrefab;
    public GameObject RockPrefab;
    public Material WorldMaterial;

    public Light SunLight;
    public GameObject GroundObject;
    public GameObject RoadObject;

    private readonly List<GameObject> _trees = new List<GameObject>();
    private readonly List<GameObject> _rocks = new List<GameObject>();
    private readonly List<FieldState> _fields = new List<FieldState>();
    private readonly List<BuildingState> _buildings = new List<BuildingState>();
    private GameObject _worldRoot;
    public GameObject WorldRoot => _worldRoot;
    private GameObject _buildingPreview;
    // road bounds (published when building the road)
    private float _roadCenterX = 14f;
    private float _roadHalfWidth = 3.8f;
    private float _roadZStart = -100f;
    private float _roadZEnd = 100f;

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

    private void Start()
    {
        // Auto-generate the world when the scene starts.
        GenerateWorld();

        if (_worldRoot == null)
            Debug.LogWarning("[WorldBuilder] World was not generated during Start(). Check CreateWorld() or scene setup.");
    }

    public void GenerateWorld()
    {
        if (_worldRoot != null)
        {
            Debug.Log("[WorldBuilder] World is already generated. Skipping duplicate generation.");
            return;
        }

        CreateWorld();
    }

    public void CreateWorld()
    {
        _worldRoot = new GameObject("WorldRoot");
        _worldRoot.transform.SetParent(null);
        _worldRoot.transform.position = Vector3.zero;
        _worldRoot.transform.rotation = Quaternion.identity;
        _worldRoot.isStatic = true;

        if (!CreateTerrainGrid())
            CreateGround();

        CreateSkyAndLight();
        BuildRoad();
        SpawnTrees(TreeCount);
        SpawnRocks(RockCount);
        BuildHouse();
        BuildShop();
        BuildWifeHouse();
        BuildBuffaloShop();
        SpawnBuffalo();
        SpawnToolPickups();
        InitializeBuildingPreview();
    }

    private bool CreateTerrainGrid()
    {
        var terrainPrefab = TerrainBlockPrefab != null ? TerrainBlockPrefab : Resources.Load<GameObject>(TerrainBlockResourcePath);
        if (terrainPrefab == null)
        {
            Debug.LogWarning($"[WorldBuilder] Terrain block prefab not found at Resources/{TerrainBlockResourcePath}. Using fallback ground mesh.");
            return false;
        }

        var terrainRoot = new GameObject("TerrainGrid");
        terrainRoot.transform.SetParent(_worldRoot.transform);

        var gridWidth = Mathf.Max(MapWidth, Mathf.CeilToInt(GroundSize.x / TileSize));
        var gridDepth = Mathf.Max(MapDepth, Mathf.CeilToInt(GroundSize.z / TileSize));
        float originOffsetX = (gridWidth - 1) * TileSize * 0.5f;
        float originOffsetZ = (gridDepth - 1) * TileSize * 0.5f;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                var tile = Instantiate(terrainPrefab, terrainRoot.transform);
                tile.name = $"TerrainBlock_{x}_{z}";
                tile.transform.position = new Vector3(x * TileSize - originOffsetX, 0f, z * TileSize - originOffsetZ);
                tile.transform.localRotation = Quaternion.identity;
                tile.transform.localScale = Vector3.one;

                if (tile.GetComponent<Collider>() == null)
                    tile.AddComponent<BoxCollider>();
            }
        }

        Debug.Log($"[WorldBuilder] Generated terrain grid {MapWidth}x{MapDepth} from Resources/{TerrainBlockResourcePath}.");
        return true;
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
        RoadObject = null;
        if (GroundObject != null) Destroy(GroundObject);
        GroundObject = null;
        if (SunLight != null) Destroy(SunLight.gameObject);
        SunLight = null;
        if (_buildingPreview != null)
            Destroy(_buildingPreview);
        _buildingPreview = null;
        if (_worldRoot != null)
            Destroy(_worldRoot);
        _worldRoot = null;
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
        // Use published road bounds if available
        if (RoadObject == null)
            return false;

        return position.x >= (_roadCenterX - _roadHalfWidth - 0.5f) && position.x <= (_roadCenterX + _roadHalfWidth + 0.5f)
               && position.z >= _roadZStart && position.z <= _roadZEnd;
    }

    private void SpawnToolPickups()
    {
        var toolTypes = new[] { "axe", "pickaxe", "hoe", "gun", "hammer", "seed", "corn", "potato" };
        // Arrange all items in a single row along X axis for easy testing
        for (int i = 0; i < toolTypes.Length; i++)
        {
            var position = new Vector3(-15f + i * 3f, 0.5f, -15f);
            CreateToolPickup(toolTypes[i], position);
        }
    }

    public GameObject SpawnPickup(string toolType, Vector3 position)
    {
        return CreateToolPickup(toolType, position);
    }

    private GameObject CreateToolPickup(string toolType, Vector3 position)
    {
        // Create a pickup root and composite visual children to match Python models
        var pickup = new GameObject("Pickup_" + toolType);
        pickup.transform.SetParent(_worldRoot.transform);
        pickup.transform.position = position;
        // build simple composite visuals matching python sizes
        switch (toolType)
        {
            case "axe":
                CreatePickupCube(pickup.transform, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.15f), new Color(0.5f, 0.2f, 0.05f)); // handle
                CreatePickupCube(pickup.transform, new Vector3(0f, 0.5f, 0.25f), new Vector3(0.2f, 0.3f, 0.7f), new Color(0.6f, 0.6f, 0.6f));
                CreatePickupCube(pickup.transform, new Vector3(0f, 0.5f, 0.5f), new Vector3(0.2f, 0.5f, 0.2f), new Color(0.6f, 0.6f, 0.6f));
                break;
            case "pickaxe":
                CreatePickupCube(pickup.transform, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.15f), new Color(0.5f, 0.2f, 0.05f));
                CreatePickupCube(pickup.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.8f), new Color(0.6f, 0.6f, 0.6f));
                CreatePickupCube(pickup.transform, new Vector3(0f, 0.4f, 0.35f), new Vector3(0.25f, 0.125f, 0.25f), new Color(0.6f, 0.6f, 0.6f));
                CreatePickupCube(pickup.transform, new Vector3(0f, 0.4f, -0.35f), new Vector3(0.25f, 0.125f, 0.25f), new Color(0.6f, 0.6f, 0.6f));
                break;
            case "hoe":
                CreatePickupCube(pickup.transform, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.8f, 0.18f), new Color(0.5f, 0.2f, 0.05f));
                CreatePickupCube(pickup.transform, new Vector3(0f, 0.4f, 0.3f), new Vector3(0.3f, 0.15f, 0.7f), new Color(0.6f, 0.6f, 0.6f));
                break;
            case "gun":
                CreatePickupCube(pickup.transform, new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.5f, 0.15f), Color.black);
                CreatePickupCube(pickup.transform, new Vector3(0f, 0.2f, 0.4f), new Vector3(0.2f, 0.2f, 1f), new Color(0.6f, 0.6f, 0.6f));
                break;
            default:
                CreatePickupCube(pickup.transform, Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f), Color.white);
                break;
        }

        // ensure exactly one trigger collider on the root
        var rootCollider = pickup.AddComponent<BoxCollider>();
        rootCollider.isTrigger = true;
        rootCollider.size = new Vector3(0.6f, 0.6f, 0.6f);
        return pickup;
    }

    private void CreatePickupCube(Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        var rend = part.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = color;
        // remove collider on visual child to avoid interfering with root collider
        var col = part.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
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
        var sky = Object.FindAnyObjectByType<Light>();
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
            if (GroundMaterial != null)
            {
                renderer.material = GroundMaterial;
            }
            else
            {
                if (GroundTexture != null)
                {
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.mainTexture = GroundTexture;
                }
                else
                {
                    var texture = Resources.Load<Texture2D>("Textures/grass");
                    if (texture != null)
                        renderer.material.mainTexture = texture;
                    else
                        renderer.material.color = new Color(0.3f, 0.6f, 0.25f);
                }
            }
        }
        var groundCollider = GroundObject.AddComponent<BoxCollider>();
        groundCollider.size = new Vector3(10f, 0.01f, 10f);
        groundCollider.center = Vector3.zero;
    }

    private void BuildRoad()
    {
        // Road parameters aligned with the ground limits and centered vertically.
        float road_cx = 14.0f;
        float road_hw = 4.0f;
        float road_len = Mathf.Max(10f, GroundSize.z - 10f);
        float road_zc = 0f;

        RoadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RoadObject.name = "Road";
        RoadObject.transform.SetParent(_worldRoot.transform);
        RoadObject.transform.localScale = new Vector3(road_hw * 2f, 0.08f, road_len);
        RoadObject.transform.position = new Vector3(road_cx, 0.04f, road_zc);
        var renderer = RoadObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.235f, 0.243f, 0.274f);

        var collider = RoadObject.GetComponent<BoxCollider>();
        if (collider == null)
            RoadObject.AddComponent<BoxCollider>();

        // publish bounds used by spawn logic
        _roadCenterX = road_cx;
        _roadHalfWidth = road_hw;
        _roadZStart = road_zc - road_len / 2f;
        _roadZEnd = road_zc + road_len / 2f;
    }

    private void SpawnTrees(int count)
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 5;
        for (int i = 0; i < count; i++)
        {
            int x, z;
            while (true)
            {
                x = Random.Range(-half, half + 1);
                z = Random.Range(-half, half + 1);
                if (!IsReservedSpawnLocation(x, z))
                    break;
            }

GameObject treeRoot;
        if (TreePrefab != null)
        {
            treeRoot = Instantiate(TreePrefab, _worldRoot.transform);
            treeRoot.name = "Tree" + i;
            treeRoot.transform.position = new Vector3(x, 0f, z);
        }
        else
        {
            treeRoot = new GameObject("Tree" + i);
            treeRoot.transform.SetParent(_worldRoot.transform);
            treeRoot.transform.position = new Vector3(x, 0f, z);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trunk.name = "Trunk";
            trunk.transform.SetParent(treeRoot.transform);
            trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            trunk.transform.localScale = new Vector3(0.8f, 3f, 0.8f);
            var trunkRenderer = trunk.GetComponent<Renderer>();
            if (trunkRenderer != null)
                trunkRenderer.material.color = new Color(0.36f, 0.23f, 0.12f);
            Destroy(trunk.GetComponent<Collider>());

            var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.SetParent(treeRoot.transform);
            leaves.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            leaves.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
            var leafRenderer = leaves.GetComponent<Renderer>();
            if (leafRenderer != null)
                leafRenderer.material.color = new Color(0.17f, 0.55f, 0.12f);
            Destroy(leaves.GetComponent<Collider>());
        }

            _trees.Add(treeRoot);
        }
    }

    private void SpawnRocks(int count)
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 5;
        for (int i = 0; i < count; i++)
        {
            int x, z;
            while (true)
            {
                x = Random.Range(-half, half + 1);
                z = Random.Range(-half, half + 1);
                if (!IsReservedSpawnLocation(x, z))
                    break;
            }

            GameObject rock;
            if (RockPrefab != null)
            {
                rock = Instantiate(RockPrefab, _worldRoot.transform);
                rock.name = "Rock" + i;
                rock.transform.position = new Vector3(x, 0f, z);
            }
            else
            {
                rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Rock" + i;
                rock.transform.SetParent(_worldRoot.transform);
                rock.transform.position = new Vector3(x, 1f, z);
                rock.transform.localScale = new Vector3(2f, 1.2f, 2f);
                var renderer = rock.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.45f, 0.45f, 0.45f);
            }
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
        CreateWall(houseRoot.transform, new Vector3(0.5f, 5f, 10f), new Vector3(5f, 2.5f, 0f), new Color(0.63f, 0.39f, 0.18f));
        CreateWall(houseRoot.transform, new Vector3(10f, 0.5f, 10f), new Vector3(0f, 0f, 0f), new Color(0.63f, 0.39f, 0.18f));
        CreateRoof(houseRoot.transform, new Vector3(10.5f, 0.5f, 10.5f), new Vector3(0f, 5.3f, 0f), new Color(0.4f, 0.25f, 0.15f));
        // Door on front wall (facing toward lower Z)
        CreateDoor(houseRoot.transform, new Vector3(1.5f, 3f, 0.2f), new Vector3(0f, 1.5f, -5.1f), new Color(0.6f, 0.4f, 0.2f));
    }

    private void BuildShop()
    {
        var shopRoot = new GameObject("Shop");
        shopRoot.transform.SetParent(_worldRoot.transform);
        shopRoot.transform.position = new Vector3(14f, 0f, -30f);
        CreateWall(shopRoot.transform, new Vector3(8f, 4f, 0.5f), new Vector3(0f, 2f, -4f), new Color(0.4f, 0.4f, 0.55f));
        CreateWall(shopRoot.transform, new Vector3(8f, 4f, 0.5f), new Vector3(0f, 2f, 4f), new Color(0.4f, 0.4f, 0.55f));
        CreateWall(shopRoot.transform, new Vector3(0.5f, 4f, 8f), new Vector3(-4f, 2f, 0f), new Color(0.4f, 0.4f, 0.55f));
        CreateWall(shopRoot.transform, new Vector3(0.5f, 4f, 8f), new Vector3(4f, 2f, 0f), new Color(0.4f, 0.4f, 0.55f));
        CreateWall(shopRoot.transform, new Vector3(8.5f, 0.5f, 8.5f), new Vector3(0f, 0f, 0f), new Color(0.37f, 0.32f, 0.21f));
        CreateRoof(shopRoot.transform, new Vector3(8.5f, 0.5f, 8.5f), new Vector3(0f, 4.2f, 0f), new Color(0.37f, 0.32f, 0.21f));
        CreateSign(shopRoot.transform, "Vendor", new Vector3(2f, 0.6f, 0.2f), new Vector3(0f, 3.9f, -3.9f));
        CreateVendor(shopRoot.transform, new Vector3(0f, 0f, -3.5f));
        SpawnVendorNPC(shopRoot.transform, new Vector3(0f, 0f, -2f));
    }

    private void BuildWifeHouse()
    {
        var spouseRoot = new GameObject("WifeHouse");
        spouseRoot.transform.SetParent(_worldRoot.transform);
        spouseRoot.transform.position = new Vector3(30f, 0f, 0f);
        
        // Only 3 walls - no wall on the side facing the road (west side at -X direction toward road at x=14)
        CreateWall(spouseRoot.transform, new Vector3(7f, 4f, 0.5f), new Vector3(0f, 2f, -3.5f), new Color(0.52f, 0.34f, 0.18f)); // Front wall
        CreateWall(spouseRoot.transform, new Vector3(7f, 4f, 0.5f), new Vector3(0f, 2f, 3.5f), new Color(0.52f, 0.34f, 0.18f));  // Back wall
        CreateWall(spouseRoot.transform, new Vector3(0.5f, 4f, 7f), new Vector3(3f, 2f, 0f), new Color(0.52f, 0.34f, 0.18f));    // Right wall
        CreateWall(spouseRoot.transform, new Vector3(7.5f, 0.5f, 7.5f), new Vector3(0f, 0f, 0f), new Color(0.45f, 0.26f, 0.16f)); // Floor
        
        CreateRoof(spouseRoot.transform, new Vector3(7.5f, 0.5f, 7.5f), new Vector3(0f, 4.2f, 0f), new Color(0.4f, 0.25f, 0.15f));
        // Door facing toward the road (west side at -X)
        CreateDoor(spouseRoot.transform, new Vector3(1.2f, 2.8f, 0.2f), new Vector3(-3.1f, 1.4f, 0f), new Color(0.6f, 0.4f, 0.2f));
        CreateSign(spouseRoot.transform, "Home", new Vector3(1.5f, 0.5f, 0.2f), new Vector3(0f, 3.7f, 0f));
        SpawnWifeNPC(spouseRoot.transform, new Vector3(1f, 1f, 0f));
    }

    private void SpawnBuffalo()
    {
        var buffalo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        buffalo.name = "Buffalo";
        buffalo.transform.SetParent(_worldRoot.transform);
        buffalo.transform.position = new Vector3(-18f, 1f, 62f);
        buffalo.transform.localScale = new Vector3(1.2f, 1.4f, 0.8f);
        var renderer = buffalo.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.4f, 0.25f, 0.1f);
        Destroy(buffalo.GetComponent<Collider>());

        var buffaloParent = new GameObject("BuffaloEntity");
        buffaloParent.transform.SetParent(_worldRoot.transform);
        buffaloParent.transform.position = buffalo.transform.position;
        buffalo.transform.SetParent(buffaloParent.transform);
    }

    private bool IsReservedSpawnLocation(int x, int z)
    {
        bool nearHouse = Mathf.Abs(x) <= 8 && Mathf.Abs(z) <= 8;
        bool nearShop = x >= 10 && x <= 18 && z >= -34 && z <= -26;
        bool nearRoad = x >= (_roadCenterX - _roadHalfWidth - 3f) && x <= (_roadCenterX + _roadHalfWidth + 3f)
                        && z >= _roadZStart - 10f && z <= _roadZEnd + 10f;
        bool nearWifeHouse = x >= 22 && x <= 38 && Mathf.Abs(z) <= 8;
        bool nearBuffaloShop = x >= -26 && x <= -14 && z >= 64 && z <= 76;
        return nearHouse || nearShop || nearRoad || nearWifeHouse || nearBuffaloShop;
    }

    private void BuildBuffaloShop()
    {
        var buffaloShop = new GameObject("BuffaloShop");
        buffaloShop.transform.SetParent(_worldRoot.transform);
        buffaloShop.transform.position = new Vector3(0f, 0f, 60f);
        CreateFloor(buffaloShop.transform, new Vector3(6.5f, 0.2f, 6.5f), new Vector3(0f, 0.1f, 0f), new Color(0.43f, 0.28f, 0.16f));
        CreateWall(buffaloShop.transform, new Vector3(6f, 3f, 0.5f), new Vector3(0f, 1.5f, -3f), new Color(0.43f, 0.28f, 0.16f));
        CreateWall(buffaloShop.transform, new Vector3(2.5f, 3f, 0.5f), new Vector3(-1.75f, 1.5f, 3f), new Color(0.43f, 0.28f, 0.16f));
        CreateWall(buffaloShop.transform, new Vector3(2.5f, 3f, 0.5f), new Vector3(1.75f, 1.5f, 3f), new Color(0.43f, 0.28f, 0.16f));
        CreateWall(buffaloShop.transform, new Vector3(0.5f, 3f, 6f), new Vector3(-3f, 1.5f, 0f), new Color(0.43f, 0.28f, 0.16f));
        CreateWall(buffaloShop.transform, new Vector3(0.5f, 3f, 6f), new Vector3(3f, 1.5f, 0f), new Color(0.43f, 0.28f, 0.16f));
        CreateRoof(buffaloShop.transform, new Vector3(7f, 0.5f, 7f), new Vector3(0f, 4f, 0f), new Color(0.28f, 0.18f, 0.10f));
        CreateSign(buffaloShop.transform, "Buffalo Shop", new Vector3(3f, 1f, 0.2f), new Vector3(0f, 3.3f, 0f));
        CreateVendor(buffaloShop.transform, new Vector3(0f, 0f, 0f));
    }

    private void CreateVendor(Transform parent, Vector3 localPosition)
    {
        // Reference: Python vendor structure with cart body and wheels
        var vendorRoot = new GameObject("Vendor");
        vendorRoot.transform.SetParent(parent);
        vendorRoot.transform.localPosition = localPosition;

        // Generate random cart color (similar to Python)
        Color cartColor = new Color(
            Random.Range(80f, 255f) / 255f,
            Random.Range(50f, 220f) / 255f,
            Random.Range(50f, 220f) / 255f
        );

        // Cart body - main container
        CreateCartPart(vendorRoot.transform, "CartBody", new Vector3(0f, 0.9f, 0f), 
                       new Vector3(4f, 1.4f, 2f), cartColor);

        // Cart top - darker shade
        Color darkColor = new Color(
            Mathf.Max(0, cartColor.r - (20f / 255f)),
            Mathf.Max(0, cartColor.g - (40f / 255f)),
            Mathf.Max(0, cartColor.b - (20f / 255f))
        );
        CreateCartPart(vendorRoot.transform, "CartTop", new Vector3(0f, 1.6f, 0f), 
                       new Vector3(4.2f, 0.4f, 2.2f), darkColor);

        // Cart stand/support
        CreateCartPart(vendorRoot.transform, "CartStand", new Vector3(2f, 1.1f, 0f), 
                       new Vector3(0.2f, 0.5f, 1.8f), Color.gray);

        // Cart stand front wheel support
        CreateCartPart(vendorRoot.transform, "CartStandFront", new Vector3(2f, 0.5f, 0f), 
                       new Vector3(0.5f, 0.7f, 2f), Color.white);

        // Create wheels
        Vector3[] wheelPositions = new Vector3[]
        {
            new Vector3(-1.4f, -0.35f, -1f),
            new Vector3(1.4f, -0.35f, -1f),
            new Vector3(-1.4f, -0.35f, 1f),
            new Vector3(1.4f, -0.35f, 1f)
        };

        foreach (Vector3 pos in wheelPositions)
        {
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wheel.name = "CartWheel";
            wheel.transform.SetParent(vendorRoot.transform);
            wheel.transform.localPosition = pos;
            wheel.transform.localScale = new Vector3(0.8f, 0.8f, 0.2f);
            Destroy(wheel.GetComponent<Collider>());
            var wheelRenderer = wheel.GetComponent<Renderer>();
            if (wheelRenderer != null)
                wheelRenderer.material.color = Color.black;

            // Wheel rim
            CreateCartPart(wheel.transform, "WheelRim", Vector3.zero, 
                          new Vector3(0.4f, 0.4f, 0.06f), cartColor);
        }
    }

    private void CreateCartPart(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
        Destroy(part.GetComponent<Collider>());
    }

    private void SpawnWifeNPC(Transform parent, Vector3 localPosition)
    {
        var wife = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        wife.name = "Wife";
        wife.transform.SetParent(parent);
        wife.transform.localPosition = localPosition;
        wife.transform.localScale = new Vector3(0.8f, 1.7f, 0.8f);
        var renderer = wife.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(1f, 0.8f, 0.7f);
        Destroy(wife.GetComponent<Collider>());
    }

    private void SpawnVendorNPC(Transform parent, Vector3 localPosition)
    {
        var vendor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        vendor.name = "Vendor";
        vendor.transform.SetParent(parent);
        vendor.transform.localPosition = localPosition;
        vendor.transform.localScale = new Vector3(0.75f, 1.8f, 0.75f);
        var renderer = vendor.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.8f, 0.6f, 0.3f);
        Destroy(vendor.GetComponent<Collider>());
    }

    private void CreateDoor(Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.SetParent(parent);
        door.transform.localScale = scale;
        door.transform.localPosition = position;
        var renderer = door.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
        Destroy(door.GetComponent<Collider>());
    }

    private void CreateSign(Transform parent, string text, Vector3 scale, Vector3 localPosition)
    {
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Sign_" + text.Replace(" ", "");
        sign.transform.SetParent(parent);
        sign.transform.localScale = scale;
        sign.transform.localPosition = localPosition;
        var renderer = sign.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.95f, 0.8f, 0.3f);
        Destroy(sign.GetComponent<Collider>());
    }

    public void SetBuildingPreviewVisible(bool visible)
    {
        if (_buildingPreview == null)
            return;

        _buildingPreview.SetActive(visible);
        if (visible)
            UpdateBuildingPreview();
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
    }

    private void UpdateBuildingPreview()
    {
        if (_buildingPreview == null)
            return;

        var definition = _availableBuildings[_currentBuildingIndex];
        _buildingPreview.transform.localScale = definition.Size;
        _buildingPreview.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
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

    private void CreateFloor(Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(parent);
        floor.transform.localScale = scale;
        floor.transform.localPosition = position;
        var renderer = floor.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
        Destroy(floor.GetComponent<Collider>());
    }

    private void CreateRoof(Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(parent);
        roof.transform.localScale = scale;
        roof.transform.localPosition = position;
        var renderer = roof.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
        Destroy(roof.GetComponent<Collider>());
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
