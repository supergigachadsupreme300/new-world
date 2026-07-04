using System.Collections.Generic;
using UnityEngine;
using static CountryLife.Helpers.PickupVisualHelper;

public class WorldBuilder : MonoBehaviour
{
    public static WorldBuilder Instance { get; private set; }

    public int TreeCount = 0;
    public int RockCount = 0;
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
    private Transform _shopRoot;

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
        SpawnBuffalo();
        CreateVendorSpawnButton();
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
            if (field.GrowTimer >= field.NextStageTime && field.Stage < 4)
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
        var toolTypes = new[] { "axe", "pickaxe", "hoe", "gun", "hammer", "wheat_seed", "corn_seed", "wheat", "corn", "potato", "peashooter_seed", "fertilizer", "mobspawner", "scythe", "ammo" };
        // Arrange all items in a grid for easy testing: 3 rows, easy to see
        int itemsPerRow = 5;
        for (int i = 0; i < toolTypes.Length; i++)
        {
            int row = i / itemsPerRow;
            int col = i % itemsPerRow;
            var position = new Vector3(-12f + col * 4f, 0.5f, -12f - row * 4f);
            CreateToolPickup(toolTypes[i], position);
        }
    }

    public GameObject SpawnPickup(string toolType, Vector3 position)
    {
        return CreateToolPickup(toolType, position);
    }

    private GameObject CreateToolPickup(string toolType, Vector3 position)
    {
        var pickup = new GameObject("Pickup_" + toolType);
        pickup.transform.SetParent(_worldRoot.transform);
        pickup.transform.position = position;

        if (!string.IsNullOrEmpty(toolType))
            ItemBuilder.BuildItem(pickup.transform, toolType);

        var rootCollider = pickup.AddComponent<BoxCollider>();
        rootCollider.isTrigger = true;
        rootCollider.size = new Vector3(0.6f, 0.6f, 0.6f);
        return pickup;
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

        // Only specific seed types can plant crops
        string actualCropType = cropType switch
        {
            "wheat_seed" => "wheat",
            "corn_seed" => "corn",
            "wheat" => "wheat",
            "corn" => "corn",
            "potato" => "potato",
            _ => null
        };

        if (actualCropType == null)
            return false;

        field.CropType = actualCropType;
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
        if (field == null || !field.HasCrop || field.Stage < 4)
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

    private GameObject MakeBlock(string name, Transform parent, Vector3 scale, Vector3 position, Color color, bool removeCollider = false, bool addCollider = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.material.color = color;
        if (removeCollider) Destroy(go.GetComponent<Collider>());
        if (addCollider && go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
        return go;
    }

    private void BuildRoad()
    {
        float roadCx = 14f;
        float roadHw = 3.8f;
        float roadLen = 150f;
        float roadZc = 17f;

        Color curbC = new Color(0.46f, 0.45f, 0.42f);
        Color whiteC = Color.white;
        Color yellowC = new Color(0.92f, 0.80f, 0.18f);
        Color asphaltC = new Color(0.235f, 0.243f, 0.275f);

        RoadObject = MakeBlock("Road", _worldRoot.transform,
            new Vector3(roadHw * 2f, 0.06f, roadLen),
            new Vector3(roadCx, 0.03f, roadZc), asphaltC, false, true);

        // Kerbs
        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("Kerb", _worldRoot.transform,
                new Vector3(0.55f, 0.22f, roadLen),
                new Vector3(roadCx + side * (roadHw + 0.27f), 0.11f, roadZc), curbC, true);
        }

        // White edge lines
        foreach (int side in new[] { -1, 1 })
        {
            MakeBlock("EdgeLine", _worldRoot.transform,
                new Vector3(0.18f, 0.03f, roadLen),
                new Vector3(roadCx + side * (roadHw - 0.22f), 0.03f, roadZc), whiteC, true);
        }

        // Yellow dashed center line
        float dashLen = 2.8f;
        float dashGap = 2.2f;
        float dashStep = dashLen + dashGap;
        float zStart = roadZc - roadLen / 2f + dashLen / 2f;
        int numDashes = Mathf.FloorToInt(roadLen / dashStep);
        for (int i = 0; i < numDashes; i++)
        {
            MakeBlock("CenterDash", _worldRoot.transform,
                new Vector3(0.18f, 0.03f, dashLen),
                new Vector3(roadCx, 0.03f, zStart + i * dashStep), yellowC, true);
        }

        // Publish bounds
        _roadCenterX = roadCx;
        _roadHalfWidth = roadHw;
        _roadZStart = roadZc - roadLen / 2f;
        _roadZEnd = roadZc + roadLen / 2f;
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
        var root = new GameObject("PlayerHouse");
        root.transform.SetParent(_worldRoot.transform);
        root.transform.position = Vector3.zero;

        Color woodC = new Color(0.63f, 0.39f, 0.18f);
        Color roofC = new Color(0.635f, 0.243f, 0.149f);
        Color ridgeC = new Color(0.345f, 0.11f, 0.039f);
        Color eaveC = new Color(0.569f, 0.345f, 0.157f);
        Color stoneC = new Color(0.439f, 0.4f, 0.361f);
        Color chimneyC = new Color(0.384f, 0.333f, 0.29f);
        Color winC = new Color(0.549f, 0.784f, 0.863f);
        Color frameC = new Color(0.165f, 0.094f, 0.031f);
        Color shuttC = new Color(0.227f, 0.376f, 0.173f);
        Color porchC = new Color(0.58f, 0.361f, 0.165f);

        // ── Walls + floor ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, -5f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 5f, 0.5f), new Vector3(0f, 2.5f, 5f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 10f), new Vector3(-5f, 2.5f, 0f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 3.5f), new Vector3(5f, 2.5f, -3.25f), woodC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 5f, 3.5f), new Vector3(5f, 2.5f, 3.25f), woodC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1f, 3f), new Vector3(5f, 4.5f, 0f), woodC);
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.5f, 10f), Vector3.zero, woodC);

        // ── Gabled roof ──
        float rise = 3f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.6f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.65f, 10f + overhang * 2f),
            new Vector3(halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.65f, 10f + overhang * 2f),
            new Vector3(-halfW / 2f, 5f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.68f, 0.38f, 10f + overhang * 2f + 0.2f),
            new Vector3(0f, 5f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.55f, 0.32f, 10f + overhang * 2f + 0.2f),
            new Vector3(halfW, 5.05f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.55f, 0.32f, 10f + overhang * 2f + 0.2f),
            new Vector3(-halfW, 5.05f, 0f), eaveC);

        // Gable end fill
        foreach (float gz in new[] { -5f, 5f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 6; i++)
            {
                float t = (i + 0.5f) / 6f;
                float sw = 10f * (1f - t) + 0.2f;
                float sy = 5f + (i + 0.5f) * rise / 6f;
                float sh = rise / 6f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), woodC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.5f, 11.5f),
            new Vector3(0f, -0.27f, 0f), stoneC);
        MakeBlock("Foundation", root.transform, new Vector3(10.8f, 0.22f, 10.8f),
            new Vector3(0f, -0.52f, 0f), stoneC);

        // ── Chimney ──
        float chX = 2.8f, chZ = 2f;
        float chBot = 5f - 0.8f;
        float chTop = 5f + rise + 1.2f;
        float chH = chTop - chBot;
        MakeBlock("Chimney", root.transform, new Vector3(1.3f, chH, 1.3f),
            new Vector3(chX, (chBot + chTop) / 2f, chZ), chimneyC);
        MakeBlock("ChimneyCap", root.transform, new Vector3(1.65f, 0.44f, 1.65f),
            new Vector3(chX, chTop + 0.22f, chZ), new Color(0.259f, 0.212f, 0.18f));

        // ── Front wall windows ──
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.4f, 0.14f),
                new Vector3(wx, 2.8f, -5.03f), winC);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
                new Vector3(wx, 2.8f, -5.03f), frameC);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.1f, 0.16f),
                new Vector3(wx, 2.8f, -5.03f), frameC);
            MakeBlock("Shutter", root.transform, new Vector3(0.22f, 1.4f, 0.12f),
                new Vector3(wx - 0.88f, 2.8f, -5.03f), shuttC);
            MakeBlock("Shutter", root.transform, new Vector3(0.22f, 1.4f, 0.12f),
                new Vector3(wx + 0.88f, 2.8f, -5.03f), shuttC);
        }

        // ── Back wall window ──
        MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.4f, 0.14f),
            new Vector3(0f, 2.8f, 5.03f), winC);
        MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
            new Vector3(0f, 2.8f, 5.03f), frameC);
        MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.1f, 0.16f),
            new Vector3(0f, 2.8f, 5.03f), frameC);

        // ── Left wall window ──
        MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.4f, 1.4f),
            new Vector3(-5.03f, 2.8f, 0f), winC);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.1f, 1.4f),
            new Vector3(-5.03f, 2.8f, 0f), frameC);
        MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.4f, 0.1f),
            new Vector3(-5.03f, 2.8f, 0f), frameC);

        // ── Right side entrance ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.32f, 4.2f, 0.32f),
            new Vector3(5.03f, 2.1f, -1.55f), frameC);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.32f, 4.2f, 0.32f),
            new Vector3(5.03f, 2.1f, 1.55f), frameC);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.32f, 0.35f, 3.42f),
            new Vector3(5.03f, 4.35f, 0f), frameC);
        MakeBlock("Porch", root.transform, new Vector3(1.2f, 0.3f, 4.2f),
            new Vector3(5.62f, 4.05f, 0f), porchC);
        MakeBlock("PorchColumn", root.transform, new Vector3(0.24f, 4.05f, 0.24f),
            new Vector3(6.12f, 2f, -1.8f), frameC);
        MakeBlock("PorchColumn", root.transform, new Vector3(0.24f, 4.05f, 0.24f),
            new Vector3(6.12f, 2f, 1.8f), frameC);

        // ── Bed ──
        var bed = MakeBlock("Bed", root.transform, new Vector3(2.8f, 0.5f, 1.8f),
            new Vector3(1.2f, 0.5f, -1.8f), new Color(0.608f, 0.216f, 0.216f));
        MakeBlock("BedPillow", bed.transform, new Vector3(0.2f, 0.5f, 0.4f),
            new Vector3(-0.4f, 0.7f, 0f), Color.white);
        MakeBlock("Headboard", bed.transform, new Vector3(0.1f, 2.2f, 1f),
            new Vector3(-0.55f, 0.5f, 0f), new Color(0.345f, 0.196f, 0.07f));
    }

    private void BuildShop()
    {
        var root = new GameObject("Shop");
        root.transform.SetParent(_worldRoot.transform);
        root.transform.position = new Vector3(0f, 0f, 60f);
        _shopRoot = root.transform;

        Color wallC = new Color(0.404f, 0.361f, 0.302f);
        Color roofC = new Color(0.871f, 0.161f, 0.11f);
        Color ridgeC = new Color(0.537f, 0.067f, 0.118f);
        Color eaveC = new Color(0.18f, 0.18f, 0.18f);
        Color stoneC = new Color(0.439f, 0.4f, 0.361f);
        Color floorC = new Color(0.357f, 0.275f, 0.18f);
        Color frameC = new Color(0.2f, 0.125f, 0.078f);
        Color counterC = new Color(0.584f, 0.294f, 0.165f);
        Color shelfC = new Color(0.455f, 0.275f, 0.157f);
        Color winC = new Color(0.549f, 0.784f, 0.863f);
        Color signC = new Color(0.886f, 0.753f, 0.098f);
        Color awningC = new Color(0.843f, 0.184f, 0.161f);
        Color itemC = new Color(0.949f, 0.584f, 0.094f);

        // ── Walls ──
        MakeBlock("Wall", root.transform, new Vector3(10f, 4f, 0.5f), new Vector3(0f, 2f, -5f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(10f, 4f, 0.5f), new Vector3(0f, 2f, 5f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 10f), new Vector3(-5f, 2f, 0f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(5f, 2f, -3.25f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, 4f, 3.5f), new Vector3(5f, 2f, 3.25f), wallC);
        MakeBlock("Transom", root.transform, new Vector3(0.5f, 1.2f, 3f), new Vector3(5f, 3.4f, 0f), wallC);
        MakeBlock("Floor", root.transform, new Vector3(10f, 0.5f, 10f), Vector3.zero, floorC);

        // ── Gabled roof ──
        float rise = 2.5f;
        float halfW = 5f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.2f;
        float roofZ = 10f + overhang * 2f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.5f, roofZ),
            new Vector3(-halfW / 2f, 4f + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.55f, 0.3f, roofZ + 0.2f),
            new Vector3(0f, 4f + rise + 0.1f, 0f), ridgeC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(halfW, 4.1f, 0f), eaveC);
        MakeBlock("Eave", root.transform, new Vector3(0.5f, 0.25f, roofZ + 0.2f),
            new Vector3(-halfW, 4.1f, 0f), eaveC);

        foreach (float gz in new[] { -5f, 5f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float sw = 10f * (1f - t) + 0.2f;
                float sy = 4f + (i + 0.5f) * rise / 5f;
                float sh = rise / 5f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(11.5f, 0.4f, 11.5f),
            new Vector3(0f, -0.2f, 0f), stoneC);

        // ── Sign ──
        MakeBlock("Sign", root.transform, new Vector3(3.5f, 0.8f, 0.2f),
            new Vector3(5.08f, 3.6f, 0f), signC);

        // ── Entrance awning ──
        MakeBlock("Awning", root.transform, new Vector3(1.5f, 0.15f, 3.5f),
            new Vector3(5.8f, 3.8f, 0f), awningC);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(6.6f, 1.9f, -1.5f), frameC);
        MakeBlock("AwningPost", root.transform, new Vector3(0.12f, 3.8f, 0.12f),
            new Vector3(6.6f, 1.9f, 1.5f), frameC);

        // ── Windows ──
        foreach (float wz in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(0.14f, 1.2f, 1.2f),
                new Vector3(-5.03f, 2.2f, wz), winC);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 0.08f, 1.2f),
                new Vector3(-5.03f, 2.2f, wz), frameC);
            MakeBlock("WinFrame", root.transform, new Vector3(0.16f, 1.2f, 0.08f),
                new Vector3(-5.03f, 2.2f, wz), frameC);
        }
        foreach (float wx in new[] { -3f, 3f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.4f, 1.2f, 0.14f),
                new Vector3(wx, 2.2f, -5.03f), winC);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.2f, 0.16f),
                new Vector3(wx, 2.2f, -5.03f), frameC);
            MakeBlock("WinFrame", root.transform, new Vector3(1.4f, 0.08f, 0.16f),
                new Vector3(wx, 2.2f, -5.03f), frameC);
        }

        // ── Counter where buffalo stands ──
        MakeBlock("Counter", root.transform, new Vector3(1.8f, 1f, 4f),
            new Vector3(-2.4f, 0.5f, 0f), counterC);
        MakeBlock("CounterTop", root.transform, new Vector3(1.8f, 0.08f, 4.2f),
            new Vector3(-2.4f, 0.96f, 0f), new Color(0.757f, 0.62f, 0.404f));
        MakeBlock("CounterFront", root.transform, new Vector3(0.03f, 0.8f, 4f),
            new Vector3(-1.5f, 0.4f, 0f), new Color(0.624f, 0.369f, 0.192f));

        // ── Shelves behind counter ──
        MakeBlock("ShelfPost", root.transform, new Vector3(0.12f, 4f, 0.12f),
            new Vector3(-4.4f, 2f, -3.5f), frameC);
        MakeBlock("ShelfPost", root.transform, new Vector3(0.12f, 4f, 0.12f),
            new Vector3(-4.4f, 2f, 3.5f), frameC);
        for (int i = 0; i < 3; i++)
        {
            float sy = 0.5f + i * 1.4f;
            MakeBlock("ShelfBoard", root.transform, new Vector3(0.12f, 0.08f, 7f),
                new Vector3(-4.4f, sy, 0f), shelfC);
            MakeBlock("ShelfItem", root.transform, new Vector3(0.25f, 0.25f, 0.25f),
                new Vector3(-4.4f, sy + 0.2f, -1.5f + i * 1.5f), itemC);
        }

        // ── Door frame ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, -1.5f), frameC);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(5.04f, 1.75f, 1.5f), frameC);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(5.04f, 3.65f, 0f), frameC);
    }

    private void BuildWifeHouse()
    {
        var root = new GameObject("WifeHouse");
        root.transform.SetParent(_worldRoot.transform);
        root.transform.position = new Vector3(33f, 0f, 0f);

        Color wallC = new Color(0.522f, 0.337f, 0.18f);
        Color roofC = new Color(0.404f, 0.204f, 0.114f);
        Color ridgeC = new Color(0.345f, 0.11f, 0.039f);
        Color floorC = new Color(0.447f, 0.263f, 0.157f);
        Color frameC = new Color(0.165f, 0.094f, 0.031f);
        Color winC = new Color(0.549f, 0.784f, 0.863f);
        Color stoneC = new Color(0.439f, 0.4f, 0.361f);
        Color balcC = new Color(0.325f, 0.208f, 0.114f);
        Color stairC = new Color(0.455f, 0.353f, 0.2f);
        Color sofaC = new Color(0.416f, 0.612f, 0.69f);
        Color tableC = new Color(0.361f, 0.259f, 0.145f);
        Color plantC = new Color(0.22f, 0.51f, 0.2f);

        // ── 1st floor ──
        float h1 = 5f;
        MakeBlock("Wall", root.transform, new Vector3(14f, h1, 0.5f), new Vector3(0f, h1 / 2f, -7f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(14f, h1, 0.5f), new Vector3(0f, h1 / 2f, 7f), wallC);
        MakeBlock("Wall", root.transform, new Vector3(0.5f, h1, 14f), new Vector3(7f, h1 / 2f, 0f), wallC);
        MakeBlock("Floor", root.transform, new Vector3(14f, 0.5f, 14f), Vector3.zero, floorC);

        // ── 2nd floor ──
        float h2 = 4f;
        MakeBlock("Wall2F", root.transform, new Vector3(14f, h2, 0.5f), new Vector3(0f, h1 + 0.25f + h2 / 2f, -7f), wallC);
        MakeBlock("Wall2F", root.transform, new Vector3(14f, h2, 0.5f), new Vector3(0f, h1 + 0.25f + h2 / 2f, 7f), wallC);
        MakeBlock("Wall2F", root.transform, new Vector3(0.5f, h2, 14f), new Vector3(7f, h1 + 0.25f + h2 / 2f, 0f), wallC);
        MakeBlock("Floor2F", root.transform, new Vector3(14f, 0.5f, 14f),
            new Vector3(0f, h1 + 0.25f, 0f), floorC);
        MakeBlock("Ceiling", root.transform, new Vector3(14f, 0.3f, 14f),
            new Vector3(0f, h1 + h2 + 0.4f, 0f), floorC);

        // ── Open side (-X) with balcony access ──
        MakeBlock("WallSide", root.transform, new Vector3(0.5f, h1 + h2 + 0.5f, 14f),
            new Vector3(-7f, (h1 + h2 + 0.5f) / 2f, 0f), wallC);

        // ── Gabled roof ──
        float rise = 3.5f;
        float halfW = 7f;
        float panelLen = Mathf.Sqrt(halfW * halfW + rise * rise);
        float tilt = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        float overhang = 1.8f;
        float roofZ = 14f + overhang * 2f;
        float roofY = h1 + h2 + 0.55f;

        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        MakeBlock("RoofPanel", root.transform, new Vector3(panelLen, 0.6f, roofZ),
            new Vector3(-halfW / 2f, roofY + rise / 2f, 0f), roofC).transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        MakeBlock("Ridge", root.transform, new Vector3(0.7f, 0.35f, roofZ + 0.2f),
            new Vector3(0f, roofY + rise + 0.1f, 0f), ridgeC);

        foreach (float gz in new[] { -7f, 7f })
        {
            float gzFace = gz + (gz > 0 ? 1f : -1f) * 0.04f;
            for (int i = 0; i < 7; i++)
            {
                float t = (i + 0.5f) / 7f;
                float sw = 14f * (1f - t) + 0.2f;
                float sy = roofY + (i + 0.5f) * rise / 7f;
                float sh = rise / 7f + 0.15f;
                MakeBlock("GableFill", root.transform, new Vector3(sw, sh, 0.55f),
                    new Vector3(0f, sy, gzFace), wallC);
            }
        }

        // ── Stone foundation ──
        MakeBlock("Foundation", root.transform, new Vector3(15.5f, 0.5f, 15.5f),
            new Vector3(0f, -0.27f, 0f), stoneC);

        // ── Windows ──
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass", root.transform, new Vector3(1.6f, 1.6f, 0.14f),
                new Vector3(wx, 2f, -7.03f), winC);
            MakeBlock("WinFrame", root.transform, new Vector3(0.1f, 1.6f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC);
            MakeBlock("WinFrame", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, 2f, -7.03f), frameC);
        }
        foreach (float wx in new[] { -4f, 4f })
        {
            MakeBlock("WinGlass2F", root.transform, new Vector3(1.6f, 1.4f, 0.14f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), winC);
            MakeBlock("WinFrame2F", root.transform, new Vector3(0.1f, 1.4f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC);
            MakeBlock("WinFrame2F", root.transform, new Vector3(1.6f, 0.1f, 0.16f),
                new Vector3(wx, h1 + 0.25f + 1.4f, -7.03f), frameC);
        }

        // ── Entrance on -X side (facing road) ──
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, -1.6f), frameC);
        MakeBlock("DoorFrame", root.transform, new Vector3(0.3f, 4.5f, 0.3f),
            new Vector3(-7.04f, 2.25f, 1.6f), frameC);
        MakeBlock("DoorLintel", root.transform, new Vector3(0.3f, 0.35f, 3.5f),
            new Vector3(-7.04f, 4.75f, 0f), frameC);

        // ── Balcony (at -X, 2F) ──
        float balcY = h1 + 0.25f;
        MakeBlock("BalconyDeck", root.transform, new Vector3(4f, 0.2f, 8f),
            new Vector3(-8.5f, balcY, 0f), balcC);
        for (float bz = -3.5f; bz <= 3.5f; bz += 1f)
        {
            MakeBlock("BalconyRail", root.transform, new Vector3(0.08f, 1.2f, 0.08f),
                new Vector3(-8.5f, balcY + 0.7f, bz), frameC);
        }
        MakeBlock("BalconyRailing", root.transform, new Vector3(0.08f, 1.2f, 7.6f),
            new Vector3(-9.52f, balcY + 0.7f, 0f), frameC);
        MakeBlock("BalconyHandrail", root.transform, new Vector3(0.08f, 0.12f, 8f),
            new Vector3(-8.5f, balcY + 1.2f, 0f), frameC);

        // ── Staircase (interior, +Z side) ──
        for (int i = 0; i < 6; i++)
        {
            float sy = i * 0.8f + 0.4f;
            float sz = -2.5f - i * 0.6f;
            MakeBlock("Stair", root.transform, new Vector3(2f, 0.15f, 1f),
                new Vector3(4.5f, sy, sz), stairC);
            MakeBlock("StairRiser", root.transform, new Vector3(2f, 0.3f, 0.1f),
                new Vector3(4.5f, sy - 0.15f, sz - 0.5f), stairC);
        }
        MakeBlock("StairLanding", root.transform, new Vector3(2.5f, 0.15f, 2f),
            new Vector3(4.5f, 5.4f, -6.5f), stairC);

        // ── 2F staircase to balcony door frame ──
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, -1.5f), frameC);
        MakeBlock("DoorFrame2F", root.transform, new Vector3(0.25f, 3.5f, 0.25f),
            new Vector3(-7.04f, balcY + 1.75f, 1.5f), frameC);
        MakeBlock("DoorLintel2F", root.transform, new Vector3(0.25f, 0.3f, 3.25f),
            new Vector3(-7.04f, balcY + 3.65f, 0f), frameC);

        // ── 1F furniture: sofas + coffee table ──
        MakeBlock("Sofa", root.transform, new Vector3(3f, 0.8f, 1.2f),
            new Vector3(-2.5f, 0.4f, 3f), sofaC);
        MakeBlock("SofaBack", root.transform, new Vector3(3f, 0.6f, 0.2f),
            new Vector3(-2.5f, 0.7f, 4.2f), sofaC);
        MakeBlock("Table", root.transform, new Vector3(1.5f, 0.5f, 1f),
            new Vector3(-2.5f, 0.25f, -4f), tableC);
        MakeBlock("Chair", root.transform, new Vector3(0.8f, 0.7f, 0.8f),
            new Vector3(-0.5f, 0.35f, -4f), wallC);
    }

    private void SpawnBuffalo()
    {
        if (_shopRoot == null) return;

        var root = new GameObject("BuffaloEntity");
        root.transform.SetParent(_shopRoot);
        root.transform.localPosition = new Vector3(-3.8f, 0f, 0f);
        root.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        root.transform.localScale = Vector3.one * 1.5f;

        Color bodyC = new Color(0.227f, 0.153f, 0.063f);
        Color hornC = new Color(0.518f, 0.518f, 0.518f);
        Color eyeC = new Color(0.059f, 0.039f, 0.016f);

        MakeBlock("Body", root.transform, new Vector3(1f, 0.9f, 0.6f), new Vector3(0f, 0.5f, 0f), bodyC, true);
        MakeBlock("Head", root.transform, new Vector3(0.4f, 0.45f, 0.5f), new Vector3(0.5f, 0.65f, 0f), bodyC, true);
        MakeBlock("HornL", root.transform, new Vector3(0.3f, 0.04f, 0.04f), new Vector3(0.3f, 0.85f, -0.25f), hornC, true);
        MakeBlock("HornR", root.transform, new Vector3(0.3f, 0.04f, 0.04f), new Vector3(0.3f, 0.85f, 0.25f), hornC, true);
        MakeBlock("EyeL", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.55f, 0.65f, -0.2f), eyeC, true);
        MakeBlock("EyeR", root.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.55f, 0.65f, 0.2f), eyeC, true);
        for (int side = -1; side <= 1; side += 2)
        {
            MakeBlock("Leg", root.transform, new Vector3(0.2f, 0.6f, 0.2f),
                new Vector3(side * 0.35f, 0.2f, side * 0.25f), bodyC, true);
            MakeBlock("Leg", root.transform, new Vector3(0.2f, 0.6f, 0.2f),
                new Vector3(side * 0.35f, 0.2f, -side * 0.25f), bodyC, true);
        }
    }

    private bool IsReservedSpawnLocation(int x, int z)
    {
        bool nearHouse = Mathf.Abs(x) <= 9 && Mathf.Abs(z) <= 9;
        bool nearShop = Mathf.Abs(x) <= 9 && z >= 51 && z <= 69;
        bool nearRoad = x >= (_roadCenterX - _roadHalfWidth - 3f) && x <= (_roadCenterX + _roadHalfWidth + 3f)
                        && z >= _roadZStart - 10f && z <= _roadZEnd + 10f;
        bool nearWifeHouse = x >= 20 && x <= 42 && Mathf.Abs(z) <= 10;
        return nearHouse || nearShop || nearRoad || nearWifeHouse;
    }

    private void CreateVendorSpawnButton()
    {
        var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
        button.name = "VendorSpawnButton";
        button.transform.SetParent(_worldRoot.transform);
        button.transform.position = new Vector3(0f, 0.2f, -9f);
        button.transform.localScale = new Vector3(1.5f, 0.15f, 1.5f);
        var rend = button.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.761f, 0.647f, 0.137f);
        Destroy(button.GetComponent<Collider>());
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

        var cropRoot = new GameObject(field.CropType + "Crop");
        cropRoot.transform.SetParent(field.FieldObject.transform, false);
        cropRoot.transform.localPosition = Vector3.up * 0.05f;
        cropRoot.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        switch (field.CropType)
        {
            case "corn":
                CreateFieldCorn(cropRoot.transform, field.Stage);
                break;
            case "potato":
                CreateFieldPotato(cropRoot.transform, field.Stage);
                break;
            default:
                CreateFieldWheat(cropRoot.transform, field.Stage);
                break;
        }

        field.CropObject = cropRoot;
    }

    private void CreateFieldWheat(Transform parent, int stage)
    {
        int bladeCount = Random.Range(3, 6);
        float height = 0.15f + stage * 0.05f;
        Color color = stage >= 3 ? new Color(1f, 0.9f, 0.2f) : new Color(0.85f, 0.8f, 0.2f);

        for (int i = 0; i < bladeCount; i++)
        {
            float width = Random.Range(0.04f, 0.06f);
            float depth = 0.02f;
            float x = Random.Range(-0.2f, 0.2f);
            float z = Random.Range(-0.15f, 0.15f);
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(parent, false);
            blade.transform.localScale = new Vector3(width, height, depth);
            blade.transform.localPosition = new Vector3(x, height / 2f, z);
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-10f, 10f));
            var rend = blade.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = color;
            Destroy(blade.GetComponent<Collider>());
        }
    }

    private void CreateFieldCorn(Transform parent, int stage)
    {
        float stalkHeight = 0.18f + stage * 0.07f;
        var stalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stalk.transform.SetParent(parent, false);
        stalk.transform.localScale = new Vector3(0.06f, stalkHeight, 0.06f);
        stalk.transform.localPosition = new Vector3(0f, stalkHeight / 2f, 0f);
        var rendStalk = stalk.GetComponent<Renderer>();
        if (rendStalk != null)
            rendStalk.material.color = new Color(0.3f, 0.7f, 0.25f);
        Destroy(stalk.GetComponent<Collider>());

        if (stage >= 4)
        {
            Color cornColor = new Color(1f, 0.85f, 0.2f);
            int layers = 3;
            int rectsPerLayer = 4;
            float earStartY = stalkHeight * 0.7f;
            for (int i = 0; i < layers; i++)
            {
                for (int j = 0; j < rectsPerLayer; j++)
                {
                    float angle = j * (360f / rectsPerLayer);
                    var rect = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rect.transform.SetParent(parent, false);
                    rect.transform.localScale = new Vector3(0.1f, 0.03f, 0.03f);
                    rect.transform.localRotation = Quaternion.Euler(0f, angle + i * 15f, 0f);
                    rect.transform.localPosition = new Vector3(0f, earStartY + i * 0.03f, 0f);
                    var rend = rect.GetComponent<Renderer>();
                    if (rend != null)
                        rend.material.color = cornColor;
                    Destroy(rect.GetComponent<Collider>());
                }
            }
        }
    }

    private void CreateFieldPotato(Transform parent, int stage)
    {
        float targetRatio = stage / 4f;

        var tuber = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tuber.transform.SetParent(parent, false);
        float rootScale = 1f + 0.3f * targetRatio;
        tuber.transform.localScale = new Vector3(0.08f * rootScale, 0.06f * rootScale, 0.07f * rootScale);
        tuber.transform.localPosition = new Vector3(0f, 0.03f * rootScale, 0f);
        var rendTuber = tuber.GetComponent<Renderer>();
        if (rendTuber != null)
            rendTuber.material.color = new Color(0.65f, 0.45f, 0.2f);
        Destroy(tuber.GetComponent<Collider>());

        int leafCount = 4;
        float radius = 0.05f + 0.07f * targetRatio;
        float leafHeight = 0.09f + 0.07f * targetRatio;
        Color leafColor = new Color(0.3f, 0.7f, 0.25f);

        for (int i = 0; i < leafCount; i++)
        {
            float angle = i * Mathf.PI * 2f / leafCount;
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.transform.SetParent(parent, false);
            leaf.transform.localScale = new Vector3(
                0.05f + 0.05f * targetRatio,
                0.01f,
                0.07f + 0.06f * targetRatio
            );
            leaf.transform.localRotation = Quaternion.Euler(30f, i * 360f / leafCount, 0f);
            leaf.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                leafHeight,
                Mathf.Sin(angle) * radius
            );
            var rendLeaf = leaf.GetComponent<Renderer>();
            if (rendLeaf != null)
                rendLeaf.material.color = leafColor;
            Destroy(leaf.GetComponent<Collider>());
        }
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
