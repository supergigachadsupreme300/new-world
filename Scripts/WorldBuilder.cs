using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public class WorldBuilder : MonoBehaviour
{
    public static WorldBuilder Instance { get; private set; }

    public int TreeCount = 100;
    public int RockCount = 0;
    public Vector3 GroundSize = new Vector3(300f, 0.2f, 300f);

    public int MapWidth = 20;
    public int MapDepth = 20;
    public float TileSize = 1f;
    public string TerrainBlockResourcePath = "Models/TerrainBlock";

    [Header("World Graphics Overrides")]
    public GameObject TerrainBlockPrefab;
    public Texture2D GroundTexture;
    public Material GroundMaterial;
    public GameObject TreePrefab;
    public GameObject RockPrefab;

    public Light SunLight;
    public GameObject GroundObject;
    public GameObject RoadObject;

    private readonly List<GameObject> _trees = new List<GameObject>();
    private readonly List<GameObject> _rocks = new List<GameObject>();
    private readonly List<FieldState> _fields = new List<FieldState>();
    private readonly List<BuildingState> _buildings = new List<BuildingState>();
    private readonly List<BlueprintState> _blueprints = new List<BlueprintState>();
    private GameObject _worldRoot;
    public GameObject WorldRoot => _worldRoot;
    private GameObject _buildingPreview;
    // road bounds (published when building the road)
    private float _roadCenterX = 14f;
    private float _roadHalfWidth = 3.8f;
    private float _roadZStart = -100f;
    private float _roadZEnd = 100f;
    private Transform _shopRoot;
    private GameObject _vendorSpawnButton;

    private class VendorCart
    {
        public GameObject Root;
        public List<GameObject> Wheels;
        public GameObject VendorModel;
        public Vector3 ArrivalPos;
        public Vector3? ExitTarget;
        public float Speed;
        public bool Rising;
        public bool Moving;
        public bool Exiting;
        public float TargetGroundY;
        public float ModelBaseY;
        public bool VendorExiting;
        public bool VendorReady;
        public GameObject VendorNPC;
        public Vector3 VendorExitStart;
        public Vector3 VendorExitTarget;
        public float VendorExitTimer;
    }
    private readonly List<VendorCart> _vendorCarts = new List<VendorCart>();

    private class TreeChopState
    {
        public GameObject TreeRoot;
        public GameObject TrunkObject;
        public GameObject ChopMark;
        public float TrunkHeight;
        public float TrunkWidth;
        public float ChopProgress;
        public Vector3 HitWorldPoint;
        public Vector3 HitNormal;
        public float HitLocalY;
        public bool IsHitOnX;
        public Vector3 CenterWorld;
        public float InitialDepth;
        public bool IsChopped;
    }
    private readonly Dictionary<GameObject, TreeChopState> _treeChopStates = new Dictionary<GameObject, TreeChopState>();
    private readonly Dictionary<GameObject, BranchChopState> _branchChopStates = new Dictionary<GameObject, BranchChopState>();

    private class BranchChopState
    {
        public GameObject BranchObject;
        public GameObject TreeRoot;
        public GameObject ChopMark;
        public float ChopProgress;
        public Vector3 HitWorldPoint;
        public Vector3 HitNormal;
        public float HitLocalY;
        public bool IsHitOnX;
        public Vector3 CenterWorld;
        public float InitialDepth;
    }

    private class RockCrackData
    {
        public GameObject Obj;
        public int Face;       // 0:+Z 1:-Z 2:+X 3:-X 4:+Y 5:-Y
        public float PosU, PosV;
        public float Angle;    // radians on the face plane
        public float Length;
        public float Thickness;
    }

    private class RockCrackState
    {
        public GameObject RockRoot;
        public int HitCount;
        public bool IsDestroyed;
        public readonly List<RockCrackData> Cracks = new List<RockCrackData>();
    }

    private readonly Dictionary<GameObject, RockCrackState> _rockCrackStates = new Dictionary<GameObject, RockCrackState>();

    private readonly BuildingDefinition[] _availableBuildings = new[]
    {
        new BuildingDefinition("wood_wall", new Vector3(6f, 3f, 0.5f), new Color(0.63f, 0.39f, 0.18f), 4, 0),
        new BuildingDefinition("stone_wall", new Vector3(5f, 3f, 0.5f), new Color(0.41f, 0.41f, 0.41f), 0, 4),
        new BuildingDefinition("fence", new Vector3(4f, 1.5f, 0.3f), new Color(0.69f, 0.51f, 0.25f), 2, 0),
        new BuildingDefinition("watchtower", new Vector3(3f, 8f, 3f), new Color(0.51f, 0.33f, 0.16f), 8, 4),
        new BuildingDefinition("small_house", new Vector3(8f, 5f, 8f), new Color(0.78f, 0.63f, 0.39f), 10, 6,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Floor",    LocalPosition = new Vector3(0f, -2.35f, 0f),   LocalScale = new Vector3(8f, 0.3f, 8f),   MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Front", LocalPosition = new Vector3(0f, 0f, 3.85f),  LocalScale = new Vector3(7.7f, 4.7f, 0.3f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Back",  LocalPosition = new Vector3(0f, 0f, -3.85f), LocalScale = new Vector3(7.7f, 4.7f, 0.3f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Left",  LocalPosition = new Vector3(-3.85f, 0f, 0f), LocalScale = new Vector3(0.3f, 4.7f, 7.7f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Right", LocalPosition = new Vector3(3.85f, 0f, 0f),  LocalScale = new Vector3(0.3f, 4.7f, 7.7f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Roof",      LocalPosition = new Vector3(0f, 2.35f, 0f),   LocalScale = new Vector3(8.3f, 0.3f, 8.3f), MaterialType = "stone" },
            }),
        new BuildingDefinition("wood_floor", new Vector3(4f, 0.3f, 4f), new Color(0.71f, 0.53f, 0.27f), 3, 0),
        new BuildingDefinition("stone_floor", new Vector3(4f, 0.3f, 4f), new Color(0.41f, 0.41f, 0.41f), 0, 3)
    };

    private int _currentBuildingIndex;
    private int _currentRotation;
    private readonly HashSet<Vector3Int> _floorPositions = new HashSet<Vector3Int>();

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
        BuildRockyBorder();
        SpawnTrees(TreeCount);
        SpawnRocks(RockCount);
        BuildHouse();
        BuildBeach();
        BuildShop();
        BuildWifeHouse();
        SpawnBuffalo();
        CreateVendorSpawnButton();
        SpawnToolPickups();
        SpawnMobs();
        CreateCropDemo();
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
            if (building.PartStates != null)
            {
                foreach (var ps in building.PartStates)
                    ps.GhostEntity = null;
            }
        }
        _buildings.Clear();
        _floorPositions.Clear();

        foreach (var bp in _blueprints)
        {
            DestroyBlueprintLabel(bp);
            if (bp.Entity != null) Destroy(bp.Entity);
            _blueprints.Clear();
        }
        _blueprints.Clear();

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

            if (field.WaterTimer > 0f)
            {
                field.WaterTimer -= deltaTime;
                if (field.WaterTimer <= 0f)
                {
                    field.Watered = false;
                    UpdateFieldVisual(field);
                }
            }

            if (!field.Watered)
                continue;

            float growTime = field.NextStageTime;
            if (field.Fertilized)
                growTime *= 0.5f;

            field.GrowTimer += deltaTime;
            if (field.GrowTimer >= growTime && field.Stage < 4)
            {
                field.GrowTimer = 0f;
                field.Stage++;
                UpdateCropVisual(field);
            }
        }

        UpdateBlueprintLabels();

        var toRemove = new List<VendorCart>();
        foreach (var v in _vendorCarts)
        {
            if (v.Exiting)
            {
                if (v.ExitTarget.HasValue)
                {
                    var dir = v.ExitTarget.Value - v.Root.transform.position;
                    float dist = dir.magnitude;
                    if (dist < 0.5f)
                    {
                        Object.Destroy(v.Root);
                        toRemove.Add(v);
                        continue;
                    }
                    v.Root.transform.position += dir.normalized * v.Speed * deltaTime;
                }
            }
            else if (v.Rising)
            {
                var pos = v.Root.transform.position;
                pos.y += 4f * deltaTime;
                if (pos.y >= v.TargetGroundY)
                {
                    pos.y = v.TargetGroundY;
                    v.Rising = false;
                    v.Moving = true;
                }
                v.Root.transform.position = pos;
            }
            else if (v.Moving)
            {
                var dir = v.ArrivalPos - v.Root.transform.position;
                float dist = dir.magnitude;
                if (dist < 0.1f)
                {
                    v.Root.transform.position = v.ArrivalPos;
                    v.Moving = false;
                }
                else
                {
                    v.Root.transform.position += dir.normalized * v.Speed * deltaTime;
                }
            }

            // Rotate wheels
            if (v.Wheels != null && (v.Rising || v.Moving || v.Exiting))
            {
                float rot = 360f * deltaTime;
                foreach (var w in v.Wheels)
                {
                    if (w != null)
                        w.transform.Rotate(0f, 0f, rot);
                }
            }

            // Bob vendor NPC (inside the truck)
            if (v.VendorReady && v.VendorNPC != null)
            {
                float bob = Mathf.Sin(Time.time * 2f) * 0.04f;
                var lp = v.VendorNPC.transform.localPosition;
                lp.y = v.ModelBaseY + bob;
                v.VendorNPC.transform.localPosition = lp;
            }
        }
        foreach (var v in toRemove)
        {
            _vendorCarts.Remove(v);
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
        var toolTypes = new[] { "axe", "pickaxe", "hoe", "hammer", "scythe", "watering_can", "wheat_seed", "corn_seed", "carrot_seed", "tomato_seed", "strawberry_seed", "pumpkin_seed", "onion_seed", "sugarcane_seed", "rice_seed", "wheat", "corn", "potato", "carrot", "tomato", "strawberry", "pumpkin", "onion", "sugarcane", "rice", "peashooter_seed", "fertilizer", "mobspawner" };
        int itemsPerRow = 8;
        for (int i = 0; i < toolTypes.Length; i++)
        {
            int row = i / itemsPerRow;
            int col = i % itemsPerRow;
            var position = new Vector3(-14f + col * 4f, 0.5f, -12f - row * 4f);
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

    private void SpawnMobs()
    {
        var mobPositions = new[]
        {
            new { pos = new Vector3(8f, 0.5f, 5f), type = Mob.MobType.Mouse },
            new { pos = new Vector3(-5f, 0.5f, 10f), type = Mob.MobType.Mouse },
            new { pos = new Vector3(15f, 0.5f, 40f), type = Mob.MobType.Crab },
            new { pos = new Vector3(20f, 0.5f, -5f), type = Mob.MobType.Mouse },
            new { pos = new Vector3(-85f, 0.5f, -20f), type = Mob.MobType.Crab },
        };

        foreach (var m in mobPositions)
        {
            var go = new GameObject(m.type.ToString());
            go.transform.SetParent(_worldRoot.transform);
            go.transform.position = m.pos;
            var mob = go.AddComponent<Mob>();
            mob.Type = m.type;
        }
    }

    private void CreateCropDemo()
    {
        var demoRoot = new GameObject("CropDemo");
        demoRoot.transform.SetParent(_worldRoot.transform);

        string[] cropTypes = { "wheat", "corn", "potato", "carrot", "tomato", "strawberry", "pumpkin", "onion", "sugarcane", "rice" };
        string[] cropLabels = { "Lúa", "Ngô", "Khoai tây", "Cà rốt", "Cà chua", "Dâu tây", "Bí ngô", "Hành tây", "Mía", "Lúa nước" };
        float startX = -12f;
        float startZ = 6f;
        float xStep = 3.5f;
        float zStep = 2.8f;

        for (int c = 0; c < cropTypes.Length; c++)
        {
            for (int s = 1; s <= 4; s++)
            {
                float x = startX + (s - 1) * xStep;
                float z = startZ + c * zStep;

                var plot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                plot.name = "FieldTile";
                plot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                plot.transform.position = new Vector3(x, 0.01f, z);
                plot.transform.localScale = new Vector3(2f, 2f, 2f);
                plot.transform.SetParent(demoRoot.transform);
                plot.GetComponent<MeshRenderer>().material.color = new Color(0.35f, 0.2f, 0.08f);
                Destroy(plot.GetComponent<Collider>());
                AddFieldBorder(plot.transform);

                var plotLabel = new GameObject("PlotLabel");
                plotLabel.transform.SetParent(demoRoot.transform);
                plotLabel.transform.position = new Vector3(x, 1f, z);
                var tmp = plotLabel.AddComponent<TextMeshPro>();
                tmp.text = cropLabels[c] + "\nS" + s;
                tmp.fontSize = 0.3f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.outlineWidth = 0.2f;
                tmp.outlineColor = Color.black;

                var cropRoot = new GameObject(cropTypes[c] + "_Stage" + s);
                cropRoot.transform.SetParent(plot.transform, false);
                cropRoot.transform.localPosition = Vector3.up * 0.05f;
                cropRoot.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                switch (cropTypes[c])
                {
                    case "corn": CreateFieldCorn(cropRoot.transform, s); break;
                    case "potato": CreateFieldPotato(cropRoot.transform, s); break;
                    case "carrot": CreateFieldCarrot(cropRoot.transform, s); break;
                    case "tomato": CreateFieldTomato(cropRoot.transform, s); break;
                    case "strawberry": CreateFieldStrawberry(cropRoot.transform, s); break;
                    case "pumpkin": CreateFieldPumpkin(cropRoot.transform, s); break;
                    case "onion": CreateFieldOnion(cropRoot.transform, s); break;
                    case "sugarcane": CreateFieldSugarcane(cropRoot.transform, s); break;
                    case "rice": CreateFieldRice(cropRoot.transform, s); break;
                    default: CreateFieldWheat(cropRoot.transform, s); break;
                }
            }
        }
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
        AddFieldBorder(tile.transform);

        field = new FieldState
        {
            FieldObject = tile,
            Tilled = true,
            Stage = 0,
            HasCrop = false,
            GrowTimer = 0f,
            NextStageTime = 12f,
            Watered = false,
            Fertilized = false,
            WaterTimer = 0f
        };
        _fields.Add(field);
        return field;
    }

    public bool PlantCrop(FieldState field, string cropType)
    {
        if (field == null || !field.Tilled || field.HasCrop)
            return false;

        string actualCropType = cropType switch
        {
            "wheat_seed" => "wheat",
            "corn_seed" => "corn",
            "wheat" => "wheat",
            "corn" => "corn",
            "potato" => "potato",
            "potato_seed" => "potato",
            "carrot_seed" => "carrot",
            "carrot" => "carrot",
            "tomato_seed" => "tomato",
            "tomato" => "tomato",
            "strawberry_seed" => "strawberry",
            "strawberry" => "strawberry",
            "pumpkin_seed" => "pumpkin",
            "pumpkin" => "pumpkin",
            "onion_seed" => "onion",
            "onion" => "onion",
            "sugarcane_seed" => "sugarcane",
            "sugarcane" => "sugarcane",
            "rice_seed" => "rice",
            "rice" => "rice",
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
            "carrot" => "carrot",
            "tomato" => "tomato",
            "strawberry" => "strawberry",
            "pumpkin" => "pumpkin",
            "onion" => "onion",
            "sugarcane" => "sugarcane",
            "rice" => "rice",
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

    public bool WaterField(Vector3 position)
    {
        var field = GetFieldAt(position);
        if (field == null || !field.Tilled || !field.HasCrop || field.IsHarvested)
            return false;
        field.Watered = true;
        field.WaterTimer = 30f;
        UpdateFieldVisual(field);
        return true;
    }

    public bool FertilizeField(Vector3 position)
    {
        var field = GetFieldAt(position);
        if (field == null || !field.Tilled || !field.HasCrop || field.IsHarvested)
            return false;
        field.Fertilized = true;
        UpdateFieldVisual(field);
        return true;
    }

    public bool RemoveTree(GameObject tree)
    {
        if (tree == null)
            return false;

        if (_trees.Contains(tree))
        {
            if (_treeChopStates.TryGetValue(tree, out var state))
            {
                if (state.ChopMark != null) Destroy(state.ChopMark);
                _treeChopStates.Remove(tree);
            }
            Destroy(tree);
            _trees.Remove(tree);
            return true;
        }
        return false;
    }

    public bool ChopTree(GameObject treeRoot, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (treeRoot == null || !_trees.Contains(treeRoot))
            return false;

        if (_treeChopStates.TryGetValue(treeRoot, out var state))
        {
            if (state.IsChopped) return false;

            state.ChopProgress = Mathf.Min(1f, state.ChopProgress + 0.25f);
            UpdateChopMarkVisual(state);

            if (state.ChopProgress >= 1f)
            {
                CutTree(state);
                return true;
            }
            return false;
        }
        else
        {
            var trunk = FindTrunk(treeRoot);
            if (trunk == null) return false;

            float trunkH = trunk.transform.localScale.y;
            float trunkW = trunk.transform.localScale.x;

            state = new TreeChopState
            {
                TreeRoot = treeRoot,
                TrunkObject = trunk,
                TrunkHeight = trunkH,
                TrunkWidth = trunkW,
                ChopProgress = 0.25f,
                HitWorldPoint = hitPoint,
                HitNormal = hitNormal,
                HitLocalY = trunk.transform.InverseTransformPoint(hitPoint).y,
                ChopMark = null,
                IsChopped = false
            };

            Vector3 trunkLocal = trunk.transform.InverseTransformPoint(hitPoint);
            Vector3 localNormal = trunk.transform.InverseTransformDirection(hitNormal);
            state.IsHitOnX = Mathf.Abs(localNormal.x) > Mathf.Abs(localNormal.z);
            if (state.IsHitOnX)
                trunkLocal.z = 0f;
            else
                trunkLocal.x = 0f;
            state.CenterWorld = trunk.transform.TransformPoint(trunkLocal);
            state.InitialDepth = Mathf.Lerp(0.05f, trunkW * 1.2f, 0.25f);

            _treeChopStates[treeRoot] = state;
            CreateChopMark(state);
            return false;
        }
    }

    private GameObject FindTrunk(GameObject treeRoot)
    {
        foreach (Transform child in treeRoot.transform)
        {
            if (child.name == "Trunk")
                return child.gameObject;
        }
        return null;
    }

    private void CreateChopMark(TreeChopState state)
    {
        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "ChopMark";
        Destroy(mark.GetComponent<Collider>());

        mark.transform.SetParent(state.TreeRoot.transform, true);
        mark.transform.position = state.CenterWorld;
        mark.transform.rotation = state.TrunkObject.transform.rotation * Quaternion.Euler(0f, 90f, 0f);

        var r = mark.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.black;
        state.ChopMark = mark;
        UpdateChopMarkVisual(state);
    }

    private void UpdateChopMarkVisual(TreeChopState state)
    {
        if (state.ChopMark == null) return;
        float tw = state.TrunkWidth;
        Transform mt = state.ChopMark.transform;

        Vector3 inward = -state.HitNormal.normalized;
        float depth = Mathf.Lerp(0.05f, tw * 1.2f, state.ChopProgress);
        float depthExtra = depth - state.InitialDepth;

        if (state.IsHitOnX)
            mt.localScale = new Vector3(tw * 1.2f, 0.08f, depth);
        else
            mt.localScale = new Vector3(depth, 0.08f, tw * 1.2f);

        mt.position = state.CenterWorld + inward * depthExtra * 0.5f;
    }

    private void CutTree(TreeChopState state)
    {
        state.IsChopped = true;

        Transform trunk = state.TrunkObject.transform;
        Vector3 trunkPos = trunk.localPosition;
        Quaternion trunkRot = trunk.localRotation;
        float fullH = state.TrunkHeight;
        float fullW = state.TrunkWidth;
        Vector3 trunkUp = trunkRot * Vector3.up;

        Vector3 chopLocal = state.TreeRoot.transform.InverseTransformPoint(state.CenterWorld);
        Vector3 bottomLocal = trunkPos + trunkRot * new Vector3(0, -fullH / 2f, 0);
        float cutHeight = Mathf.Max(0.1f, Vector3.Dot(chopLocal - bottomLocal, trunkUp));

        trunk.localPosition = Vector3.Lerp(bottomLocal, chopLocal, 0.5f);
        trunk.localScale = new Vector3(fullW, cutHeight, fullW);

        float topH = fullH - cutHeight;

        var toMove = new List<Transform>();
        foreach (Transform child in state.TreeRoot.transform)
        {
            if (child.name == "Trunk") continue;
            if (child.name == "Leaf")
            {
                Object.Destroy(child.gameObject);
                continue;
            }
            if (Vector3.Dot(child.localPosition, trunkUp) > Vector3.Dot(chopLocal, trunkUp) + 0.3f)
            {
                if (child.GetComponent<Collider>() == null)
                    child.gameObject.AddComponent<BoxCollider>();
                toMove.Add(child);
            }
        }

        if (topH > 0.3f)
        {
            var topRoot = new GameObject("TreeFelled");
            topRoot.transform.position = state.TreeRoot.transform.position;
            topRoot.transform.rotation = state.TreeRoot.transform.rotation;
            var rb = topRoot.AddComponent<Rigidbody>();
            rb.mass = 10f;

            var topTrunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topTrunk.name = "Trunk";
            topTrunk.transform.SetParent(topRoot.transform);
            topTrunk.transform.localScale = new Vector3(fullW, topH, fullW);
            topTrunk.transform.localPosition = chopLocal + trunkUp * (topH / 2f);
            topTrunk.transform.localRotation = trunkRot;
            var topTrunkR = topTrunk.GetComponent<Renderer>();
            if (topTrunkR != null)
            {
                var origR = state.TrunkObject.GetComponent<Renderer>();
                topTrunkR.material.color = origR != null ? origR.material.color : new Color(0.36f, 0.23f, 0.12f);
            }

            foreach (var child in toMove)
                child.SetParent(topRoot.transform, true);

            _trees.Add(topRoot);
        }

        if (state.ChopMark != null)
        {
            Destroy(state.ChopMark);
            state.ChopMark = null;
        }

        _treeChopStates.Remove(state.TreeRoot);
    }

    public bool ChopBranch(GameObject treeRoot, GameObject branch, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (branch == null) return false;

        if (_branchChopStates.TryGetValue(branch, out var state))
        {
            state.ChopProgress = Mathf.Min(1f, state.ChopProgress + 0.25f);
            UpdateBranchChopMarkVisual(state);

            if (state.ChopProgress >= 1f)
            {
                CutBranch(state);
                _branchChopStates.Remove(branch);
                return true;
            }
            return false;
        }

        var bt = branch.transform;
        float branchH = bt.localScale.y;
        float branchW = bt.localScale.x;

        Vector3 trunkLocal = bt.InverseTransformPoint(hitPoint);
        Vector3 localNormal = bt.InverseTransformDirection(hitNormal);
        bool isHitOnX = Mathf.Abs(localNormal.x) > Mathf.Abs(localNormal.z);

        if (isHitOnX) trunkLocal.z = 0f;
        else trunkLocal.x = 0f;

        state = new BranchChopState
        {
            BranchObject = branch,
            TreeRoot = branch.name == "TrunkSeg" ? treeRoot : null,
            ChopProgress = 0.25f,
            HitWorldPoint = hitPoint,
            HitNormal = hitNormal,
            HitLocalY = trunkLocal.y,
            IsHitOnX = isHitOnX,
            CenterWorld = bt.TransformPoint(trunkLocal),
            InitialDepth = Mathf.Lerp(0.05f, branchW * 1.2f, 0.25f),
        };

        CreateBranchChopMark(state);
        _branchChopStates[branch] = state;
        return false;
    }

    private void CreateBranchChopMark(BranchChopState state)
    {
        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "ChopMark";
        Destroy(mark.GetComponent<Collider>());

        mark.transform.SetParent(state.BranchObject.transform.parent, true);
        mark.transform.position = state.CenterWorld;
        mark.transform.rotation = state.BranchObject.transform.rotation * Quaternion.Euler(0f, 90f, 0f);

        var r = mark.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.black;
        state.ChopMark = mark;
        UpdateBranchChopMarkVisual(state);
    }

    private void UpdateBranchChopMarkVisual(BranchChopState state)
    {
        if (state.ChopMark == null) return;
        float tw = state.BranchObject.transform.localScale.x;
        Transform mt = state.ChopMark.transform;

        Vector3 inward = -state.HitNormal.normalized;
        float depth = Mathf.Lerp(0.05f, tw * 1.2f, state.ChopProgress);
        float depthExtra = depth - state.InitialDepth;

        if (state.IsHitOnX)
            mt.localScale = new Vector3(tw * 1.2f, 0.08f, depth);
        else
            mt.localScale = new Vector3(depth, 0.08f, tw * 1.2f);

        mt.position = state.CenterWorld + inward * depthExtra * 0.5f;
    }

    private void CutBranch(BranchChopState state)
    {
        GameObject branchObj = state.BranchObject;
        if (branchObj == null) return;

        Transform branch = branchObj.transform;
        Transform parent = branch.parent;
        Vector3 branchPos = branch.localPosition;
        Quaternion branchRot = branch.localRotation;
        float fullH = branch.localScale.y;
        float fullW = branch.localScale.x;
        Vector3 branchUp = branchRot * Vector3.up;

        Vector3 chopLocal = parent.InverseTransformPoint(state.CenterWorld);
        Vector3 bottomLocal = branchPos + branchRot * new Vector3(0, -fullH / 2f, 0);
        float cutHeight = Mathf.Max(0.1f, Vector3.Dot(chopLocal - bottomLocal, branchUp));
        float topH = fullH - cutHeight;

        branch.localPosition = Vector3.Lerp(bottomLocal, chopLocal, 0.5f);
        branch.localScale = new Vector3(fullW, cutHeight, fullW);

        if (topH > 0.2f)
        {
            var topRoot = new GameObject("BranchTop");
            Vector3 topCenter = chopLocal + branchUp * (topH / 2f);
            topRoot.transform.position = parent.TransformPoint(topCenter);
            topRoot.transform.rotation = parent.rotation;
            var rb = topRoot.AddComponent<Rigidbody>();
            rb.mass = 3f;

            var topBranch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topBranch.name = "BranchTopPart";
            topBranch.transform.SetParent(topRoot.transform);
            topBranch.transform.localScale = new Vector3(fullW, topH, fullW);
            topBranch.transform.localPosition = Vector3.zero;
            topBranch.transform.localRotation = branchRot;
            var r = topBranch.GetComponent<Renderer>();
            if (r != null)
            {
                var origR = branchObj.GetComponent<Renderer>();
                r.material.color = origR != null ? origR.material.color : new Color(0.36f, 0.23f, 0.12f);
            }

            Vector3 origTipLocal = branchPos + branchRot * new Vector3(0, fullH / 2f, 0);
            MoveSubBranches(parent, topRoot.transform, origTipLocal, fullW);

            if (state.TreeRoot != null)
            {
                topRoot.name = "TreeFelled";
                rb.mass = 10f;

                foreach (Transform child in state.TreeRoot.transform)
                {
                    if (child == branchObj.transform || child.name == "Trunk" || child.name == "Leaf") continue;
                    if (Vector3.Dot(child.localPosition, branchUp) > Vector3.Dot(chopLocal, branchUp) + 0.3f)
                    {
                        if (child.GetComponent<Collider>() == null)
                            child.gameObject.AddComponent<BoxCollider>();
                        child.SetParent(topRoot.transform, true);
                    }
                }

                _trees.Add(topRoot);
            }
        }

        if (state.ChopMark != null)
        {
            Destroy(state.ChopMark);
            state.ChopMark = null;
        }
    }

    private void MoveSubBranches(Transform parent, Transform target, Vector3 tipLocal, float tipWidth)
    {
        var matches = new List<(Transform t, Vector3 childTip, float childWidth)>();
        foreach (Transform child in parent)
        {
            if (child.name != "Branch") continue;
            Vector3 childBottom = child.localPosition + child.localRotation * new Vector3(0, -child.localScale.y / 2f, 0);
            if (Vector3.Distance(childBottom, tipLocal) < (tipWidth + child.localScale.x) * 0.5f)
            {
                Vector3 childTip = child.localPosition + child.localRotation * new Vector3(0, child.localScale.y / 2f, 0);
                matches.Add((child, childTip, child.localScale.x));
            }
        }
        foreach (var (t, childTip, childWidth) in matches)
        {
            t.SetParent(target, true);
            MoveSubBranches(parent, target, childTip, childWidth);
        }
    }

    public bool HitRock(GameObject rockRoot, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (rockRoot == null || !_rocks.Contains(rockRoot))
            return false;

        if (_rockCrackStates.TryGetValue(rockRoot, out var state))
        {
            if (state.IsDestroyed) return false;
            state.HitCount++;
            UpdateRockCracks(rockRoot, state);

            if (state.HitCount >= 4)
            {
                state.IsDestroyed = true;
                foreach (var crack in state.Cracks)
                {
                    if (crack.Obj != null) Destroy(crack.Obj);
                }
                _rockCrackStates.Remove(rockRoot);
                SpawnRockDebris(rockRoot);
                Destroy(rockRoot);
                _rocks.Remove(rockRoot);
                return true;
            }
            return false;
        }

        state = new RockCrackState { RockRoot = rockRoot, HitCount = 1 };
        UpdateRockCracks(rockRoot, state);
        _rockCrackStates[rockRoot] = state;
        return false;
    }

    private void UpdateRockCracks(GameObject rockRoot, RockCrackState state)
    {
        var rock = rockRoot.transform.Find("Rock");
        if (rock == null) return;

        float w = rock.localScale.x;
        float h = rock.localScale.y;
        float d = rock.localScale.z;
        float minDim = Mathf.Min(w, h, d);
        Vector3 rockPos = rock.localPosition;

        if (state.Cracks.Count == 0)
        {
            // First hit — spawn cracks evenly across all 6 faces
            for (int f = 0; f < 6; f++)
            {
                for (int i = 0; i < 4; i++)
                {
                    var data = new RockCrackData();
                    data.Face = f;
                    GetFaceGeometry(data.Face, w, h, d, out Vector3 centerOff, out Vector3 tanU, out Vector3 tanV, out Vector3 _, out float extU, out float extV);
                    data.Length = Random.Range(minDim * 0.2f, minDim * 0.4f);
                    data.Thickness = Random.Range(0.025f, 0.045f);
                    data.Angle = Random.Range(0f, Mathf.PI);
                    float margin = Mathf.Min(0.02f, Mathf.Min(extU, extV) * 0.1f);
                    float avail = Mathf.Min(extU, extV) * 0.5f - margin;
                    float halfLen = Mathf.Min(data.Length * 0.5f, Mathf.Max(0.005f, avail * 0.9f));
                    data.PosU = Random.Range(-extU * 0.5f + halfLen + margin, extU * 0.5f - halfLen - margin);
                    data.PosV = Random.Range(-extV * 0.5f + halfLen + margin, extV * 0.5f - halfLen - margin);
                    data.Obj = BuildCrackPrimitive(rockRoot);
                    ApplyCrackTransform(data.Obj, data, w, h, d, rockPos);
                    state.Cracks.Add(data);
                }
            }
        }
        else
        {
            // Subsequent hits — grow existing cracks
            float growthLen = minDim * 0.25f;
            float growthThick = 0.008f;

            for (int i = state.Cracks.Count - 1; i >= 0; i--)
            {
                var data = state.Cracks[i];
                if (data.Obj == null) { state.Cracks.RemoveAt(i); continue; }

                float newLen = data.Length + growthLen;
                float newThick = data.Thickness + growthThick;
                float halfLen = newLen * 0.5f;
                float cosA = Mathf.Cos(data.Angle);
                float sinA = Mathf.Sin(data.Angle);

                GetFaceGeometry(data.Face, w, h, d, out Vector3 centerOff, out Vector3 tanU, out Vector3 tanV, out Vector3 _, out float extU, out float extV);
                float halfU = extU * 0.5f;
                float halfV = extV * 0.5f;

                float t1 = float.MaxValue, t2 = float.MaxValue;
                float e1u = data.PosU + cosA * halfLen;
                float e1v = data.PosV + sinA * halfLen;
                if (Mathf.Abs(e1u) > halfU || Mathf.Abs(e1v) > halfV)
                    t1 = RayRectIntersection(data.PosU, data.PosV, cosA, sinA, halfU, halfV);
                float e2u = data.PosU - cosA * halfLen;
                float e2v = data.PosV - sinA * halfLen;
                if (Mathf.Abs(e2u) > halfU || Mathf.Abs(e2v) > halfV)
                    t2 = RayRectIntersection(data.PosU, data.PosV, -cosA, -sinA, halfU, halfV);

                float cappedHalf = halfLen;
                if (t1 < float.MaxValue && t1 < cappedHalf) cappedHalf = t1;
                if (t2 < float.MaxValue && t2 < cappedHalf) cappedHalf = t2;

                float excessTotal = (halfLen - cappedHalf) * 2f;

                data.Length = Mathf.Max(0.01f, cappedHalf * 2f);
                data.Thickness = newThick;
                ApplyCrackTransform(data.Obj, data, w, h, d, rockPos);

                // Branching — existing cracks spawn side branches
                float branchChance = 0.3f;
                if (data.Length > minDim * 0.12f && Random.value < branchChance)
                {
                    int branchCount = Random.Range(1, 3);
                    for (int b = 0; b < branchCount; b++)
                    {
                        float branchT = Random.Range(-data.Length * 0.35f, data.Length * 0.35f);
                        float branchAngle = data.Angle + Random.Range(0.4f, 1.3f) * (Random.value > 0.5f ? 1f : -1f);
                        float branchLen = data.Length * Random.Range(0.25f, 0.55f);
                        float branchThick = data.Thickness * Random.Range(0.4f, 0.7f);

                        float bU = data.PosU + cosA * branchT;
                        float bV = data.PosV + sinA * branchT;

                        // Ensure branch center is within face bounds, otherwise skip
                        if (Mathf.Abs(bU) > halfU * 0.9f || Mathf.Abs(bV) > halfV * 0.9f)
                            continue;

                        var branchData = new RockCrackData
                        {
                            Face = data.Face,
                            PosU = bU,
                            PosV = bV,
                            Angle = branchAngle,
                            Length = Mathf.Max(0.04f, branchLen),
                            Thickness = branchThick,
                            Obj = BuildCrackPrimitive(rockRoot)
                        };
                        ApplyCrackTransform(branchData.Obj, branchData, w, h, d, rockPos);
                        state.Cracks.Add(branchData);
                    }
                }

                if (excessTotal > 0.02f)
                {
                    int extraCount = Mathf.Max(1, Mathf.RoundToInt(excessTotal / 0.15f));
                    for (int e = 0; e < extraCount; e++)
                    {
                        var newData = new RockCrackData();
                        newData.Face = Random.Range(0, 6);
                        GetFaceGeometry(newData.Face, w, h, d, out Vector3 nc, out Vector3 ntU, out Vector3 ntV, out Vector3 _, out float nExtU, out float nExtV);
                        float lenPortion = excessTotal / extraCount * Random.Range(0.7f, 1.3f);
                        newData.Length = Mathf.Max(0.04f, lenPortion);
                        newData.Thickness = Random.Range(0.015f, 0.03f);
                        newData.Angle = Random.Range(0f, Mathf.PI);
                        float hLen = newData.Length * 0.5f;
                        float mg = Mathf.Min(0.02f, Mathf.Min(nExtU, nExtV) * 0.1f);
                        newData.PosU = Random.Range(-nExtU * 0.5f + hLen + mg, nExtU * 0.5f - hLen - mg);
                        newData.PosV = Random.Range(-nExtV * 0.5f + hLen + mg, nExtV * 0.5f - hLen - mg);
                        newData.Obj = BuildCrackPrimitive(rockRoot);
                        ApplyCrackTransform(newData.Obj, newData, w, h, d, rockPos);
                        state.Cracks.Add(newData);
                    }
                }
            }
        }
    }

    private void SpawnRockDebris(GameObject rockRoot)
    {
        Vector3 origin = rockRoot.transform.position;
        var rock = rockRoot.transform.Find("Rock");
        float totalVolume = 0.027f;
        if (rock != null)
        {
            Vector3 s = rock.localScale;
            totalVolume = s.x * s.y * s.z;
        }

        int count = Random.Range(4, 7);
        float[] fractions = new float[count];
        float sum = 0;
        for (int i = 0; i < count; i++)
        {
            fractions[i] = Random.Range(0.5f, 1.5f);
            sum += fractions[i];
        }
        float efficiency = 0.85f;
        const float minVolume = 0.008f;

        for (int i = 0; i < count; i++)
        {
            float volume = fractions[i] / sum * totalVolume * efficiency;
            if (volume < minVolume) continue;

            float s = Mathf.Pow(volume, 1f / 3f);
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "RockDebris";
            piece.transform.position = origin + Random.insideUnitSphere * s * 0.5f;
            piece.transform.localScale = Vector3.one * s;
            var r = piece.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(Color.gray, Color.black, Random.value * 0.5f);
            var rb = piece.AddComponent<Rigidbody>();
            rb.mass = volume * 1000f;
            Vector3 vel = new Vector3(Random.Range(-2f, 2f), Random.Range(4f, 8f), Random.Range(-2f, 2f));
            rb.linearVelocity = vel;
            rb.angularVelocity = Random.insideUnitSphere * 5f;
        }
    }

    public void SmashDebris(GameObject piece)
    {
        if (piece == null) return;
        var s = piece.transform.localScale;
        float volume = s.x * s.y * s.z;
        const float minVolume = 0.008f;

        int splitCount = Random.Range(2, 4);
        float[] fractions = new float[splitCount];
        float sum = 0;
        for (int i = 0; i < splitCount; i++)
        {
            fractions[i] = Random.Range(0.3f, 0.7f);
            sum += fractions[i];
        }
        float efficiency = 0.7f;

        Vector3 pos = piece.transform.position;
        Destroy(piece);

        for (int i = 0; i < splitCount; i++)
        {
            float vol = fractions[i] / sum * volume * efficiency;
            if (vol < minVolume) continue;

            float side = Mathf.Pow(vol, 1f / 3f);
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "RockDebris";
            p.transform.position = pos + Random.insideUnitSphere * side * 0.3f;
            p.transform.localScale = Vector3.one * side;
            var r = p.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.Lerp(Color.gray, Color.black, Random.value * 0.5f);
            var rb = p.AddComponent<Rigidbody>();
            rb.mass = vol * 1000f;
            rb.linearVelocity = Random.insideUnitSphere * 3f + Vector3.up * 2f;
            rb.angularVelocity = Random.insideUnitSphere * 5f;
        }
    }

    public void SplitWoodDebris(GameObject debris)
    {
        if (debris == null) return;
        string partName = debris.name == "TreeFelled" ? "Trunk" : "BranchTopPart";
        var part = debris.transform.Find(partName);
        if (part == null) return;

        var s = part.localScale;
        float halfH = s.y * 0.5f;
        const float minH = 0.1f;

        if (halfH < minH)
        {
            Destroy(debris);
            return;
        }

        Vector3 worldPos = part.position;
        Vector3 up = part.up;
        var matColor = part.GetComponent<Renderer>()?.material.color ?? new Color(0.36f, 0.23f, 0.12f);

        part.localScale = new Vector3(s.x, halfH, s.z);
        part.position = worldPos - up * halfH * 0.5f;

        var split = new GameObject(debris.name);
        split.transform.position = worldPos + up * halfH * 0.5f;
        split.transform.rotation = debris.transform.rotation;
        var rb = split.AddComponent<Rigidbody>();
        rb.mass = s.x * halfH * s.z * 500f;

        var splitPart = GameObject.CreatePrimitive(PrimitiveType.Cube);
        splitPart.name = partName;
        splitPart.transform.SetParent(split.transform);
        splitPart.transform.localScale = new Vector3(s.x, halfH, s.z);
        splitPart.transform.localPosition = Vector3.zero;
        splitPart.transform.localRotation = part.localRotation;
        var r = splitPart.GetComponent<Renderer>();
        if (r != null)
            r.material.color = matColor;
    }

    private void GetFaceGeometry(int face, float w, float h, float d,
        out Vector3 centerOffset, out Vector3 tanU, out Vector3 tanV, out Vector3 normal, out float extU, out float extV)
    {
        switch (face)
        {
            case 0: centerOffset = new Vector3(0, 0, d * 0.5f); tanU = Vector3.right; tanV = Vector3.up; normal = Vector3.forward; extU = w; extV = h; break;
            case 1: centerOffset = new Vector3(0, 0, -d * 0.5f); tanU = Vector3.right; tanV = Vector3.up; normal = Vector3.back; extU = w; extV = h; break;
            case 2: centerOffset = new Vector3(w * 0.5f, 0, 0); tanU = Vector3.up; tanV = Vector3.forward; normal = Vector3.right; extU = h; extV = d; break;
            case 3: centerOffset = new Vector3(-w * 0.5f, 0, 0); tanU = Vector3.up; tanV = Vector3.forward; normal = Vector3.left; extU = h; extV = d; break;
            case 4: centerOffset = new Vector3(0, h * 0.5f, 0); tanU = Vector3.right; tanV = Vector3.forward; normal = Vector3.up; extU = w; extV = d; break;
            default: centerOffset = new Vector3(0, -h * 0.5f, 0); tanU = Vector3.right; tanV = Vector3.forward; normal = Vector3.down; extU = w; extV = d; break;
        }
    }

    private float RayRectIntersection(float originU, float originV, float dirU, float dirV, float halfU, float halfV)
    {
        float t = float.MaxValue;
        if (dirU > 0.001f) { float tu = (halfU - originU) / dirU; if (tu > 0 && tu < t) t = tu; }
        else if (dirU < -0.001f) { float tu = (-halfU - originU) / dirU; if (tu > 0 && tu < t) t = tu; }
        if (dirV > 0.001f) { float tv = (halfV - originV) / dirV; if (tv > 0 && tv < t) t = tv; }
        else if (dirV < -0.001f) { float tv = (-halfV - originV) / dirV; if (tv > 0 && tv < t) t = tv; }
        return t;
    }

    private GameObject BuildCrackPrimitive(GameObject parent)
    {
        var crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crack.name = "Crack";
        Destroy(crack.GetComponent<Collider>());
        crack.transform.SetParent(parent.transform);
        var r = crack.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.black;
        return crack;
    }

    private void ApplyCrackTransform(GameObject crack, RockCrackData data, float w, float h, float d, Vector3 rockPos)
    {
        GetFaceGeometry(data.Face, w, h, d, out Vector3 centerOff, out Vector3 tanU, out Vector3 tanV, out Vector3 normal, out float extU, out float extV);
        Vector3 pos3D = centerOff + data.PosU * tanU + data.PosV * tanV;
        crack.transform.localPosition = pos3D + rockPos;
        Vector3 longDir = (Mathf.Cos(data.Angle) * tanU + Mathf.Sin(data.Angle) * tanV).normalized;
        Vector3 zAxis = Vector3.Cross(longDir, normal).normalized;
        crack.transform.localRotation = Quaternion.LookRotation(zAxis, normal);
        crack.transform.localScale = new Vector3(data.Length, data.Thickness, data.Thickness);
    }

    public bool RemoveRock(GameObject rock)
    {
        if (rock == null)
            return false;
        if (_rocks.Contains(rock))
        {
            if (_rockCrackStates.TryGetValue(rock, out var state))
            {
                foreach (var crack in state.Cracks)
                {
                    if (crack.Obj != null) Destroy(crack.Obj);
                }
                _rockCrackStates.Remove(rock);
            }
            SpawnRockDebris(rock);
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

    public bool PlaceBlueprint(Vector3 position)
    {
        var definition = _availableBuildings[_currentBuildingIndex];
        var size = definition.Size;
        Vector3 snapped = SnapToGrid(position);
        if (!IsFloorType(definition.Name) && !HasFloorAt(snapped))
        {
            Debug.Log("Must place on a floor first!");
            return false;
        }
        if (!CanPlaceBuilding(snapped, size, _currentRotation))
            return false;

        var blueprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueprint.name = "Blueprint";
        blueprint.transform.position = snapped + Vector3.up * (size.y * 0.5f);
        blueprint.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
        blueprint.transform.localScale = size;
        var renderer = blueprint.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat != null)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0f);
            mat.renderQueue = 3000;
        }
        else
        {
            mat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        }
        mat.color = new Color(0.2f, 0.5f, 1f, 0.15f);
        renderer.material = mat;
        var collider = blueprint.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        blueprint.transform.SetParent(_worldRoot.transform);

        var bpState = new BlueprintState
        {
            Entity = blueprint,
            Type = definition.Name,
            Position = snapped,
            Rotation = _currentRotation,
            WoodDeposited = 0,
            StoneDeposited = 0
        };
        CreateBlueprintLabel(blueprint, bpState, definition);
        blueprint.AddComponent<BlueprintAutoDeposit>();
        _blueprints.Add(bpState);
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
        foreach (var bp in _blueprints)
        {
            if (bp.Entity == null)
                continue;
            if (bounds.Intersects(bp.Entity.GetComponent<Collider>().bounds))
                return false;
        }
        return true;
    }

    public bool IsBlueprint(GameObject obj)
    {
        while (obj.transform.parent != null && obj.transform.parent.name != "WorldRoot")
            obj = obj.transform.parent.gameObject;
        return obj.name == "Blueprint";
    }

    public BlueprintState FindBlueprint(GameObject obj)
    {
        while (obj.transform.parent != null && obj.transform.parent.name != "WorldRoot")
            obj = obj.transform.parent.gameObject;
        foreach (var bp in _blueprints)
        {
            if (bp.Entity == obj)
                return bp;
        }
        return null;
    }

    public bool DepositMaterial(BlueprintState bp, string materialType, float amount)
    {
        if (bp == null) return false;

        if (materialType == "wood")
            bp.WoodDeposited += amount;
        else if (materialType == "stone")
            bp.StoneDeposited += amount;
        else
            return false;

        float woodCost, stoneCost;
        BuildingDefinition def = null;
        if (bp.IsEssential)
        {
            woodCost = bp.WoodCost;
            stoneCost = bp.StoneCost;
        }
        else
        {
            def = System.Array.Find(_availableBuildings, d => d.Name == bp.Type);
            if (def == null) return false;
            woodCost = def.WoodCost;
            stoneCost = def.StoneCost;
        }

        // Update label text to reflect remaining materials needed
        if (bp.Label != null)
        {
            var tmp = bp.Label.GetComponent<TextMeshPro>();
            if (tmp != null)
                tmp.text = GetBlueprintRemainingText(bp, woodCost, stoneCost);
        }

        if (bp.WoodDeposited >= woodCost && bp.StoneDeposited >= stoneCost)
        {
            CompleteBlueprint(bp, def);
            return true;
        }
        return false;
    }

    private GameObject CreateBuildingEntity(string typeName, Vector3 position, int rotation, out List<BuildingPartState> partStates)
    {
        partStates = null;
        var def = System.Array.Find(_availableBuildings, d => d.Name == typeName);
        if (def == null) return null;

        if (def.Parts != null && def.Parts.Length > 0)
        {
            var root = new GameObject(def.Name);
            root.transform.position = position + Vector3.up * (def.Size.y * 0.5f);
            root.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            root.transform.SetParent(_worldRoot.transform);
            var rootCollider = root.AddComponent<BoxCollider>();
            rootCollider.size = Vector3.one;
            rootCollider.isTrigger = false;

            partStates = new List<BuildingPartState>();
            foreach (var partDef in def.Parts)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = "BuildingPart_" + partDef.PartName;
                part.transform.SetParent(root.transform);
                part.transform.localPosition = partDef.LocalPosition;
                part.transform.localScale = partDef.LocalScale;
                part.transform.localRotation = Quaternion.identity;

                Color partColor = partDef.MaterialType == "stone" ? def.StoneColor : def.WoodColor;
                part.GetComponent<MeshRenderer>().material.color = partColor;
                part.AddComponent<BoxCollider>();

                partStates.Add(new BuildingPartState
                {
                    PartName = partDef.PartName,
                    Entity = part,
                    CurrentHealth = 4
                });
            }
            return root;
        }
        else
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = def.Name;
            building.transform.position = position + Vector3.up * (def.Size.y * 0.5f);
            building.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            building.transform.localScale = def.Size;
            building.GetComponent<MeshRenderer>().material.color = def.Color;
            building.AddComponent<BoxCollider>();
            building.transform.SetParent(_worldRoot.transform);
            return building;
        }
    }

    public BuildingDefinition GetBuildingDefinition(string typeName)
    {
        return System.Array.Find(_availableBuildings, d => d.Name == typeName);
    }

    public bool SpawnBuildingDirect(string typeName, Vector3 position, int rotation, List<BuildingPartState> partStates = null)
    {
        var building = CreateBuildingEntity(typeName, position, rotation, out var createdParts);
        if (building == null) return false;
        if (partStates != null) createdParts = partStates;
        _buildings.Add(new BuildingState
        {
            Entity = building,
            Type = typeName,
            Position = position,
            Rotation = rotation,
            PartStates = createdParts,
            CurrentHealth = 100,
            MaxHealth = 100
        });
        return true;
    }

    private void CompleteBlueprint(BlueprintState bp, BuildingDefinition def)
    {
        if (bp.IsEssential)
        {
            RebuildEssentialBuilding(bp);
        }
        else
        {
            SpawnBuildingDirect(def.Name, bp.Position, bp.Rotation);
        }
        if (IsFloorType(bp.Type))
        {
            var key = new Vector3Int(Mathf.RoundToInt(bp.Position.x), 0, Mathf.RoundToInt(bp.Position.z));
            _floorPositions.Add(key);
        }
        DestroyBlueprintLabel(bp);
        if (bp.Entity != null)
            Destroy(bp.Entity);
        _blueprints.Remove(bp);
    }

    private void RebuildEssentialBuilding(BlueprintState bp)
    {
        GameObject root = null;
        switch (bp.Type)
        {
            case "PlayerHouse":
                root = MapBuilder.BuildPlayerHouse(_worldRoot.transform, bp.Position);
                break;
            case "Shop":
                root = MapBuilder.BuildShop(_worldRoot.transform, bp.Position);
                _shopRoot = root.transform;
                break;
            case "WifeHouse":
                root = MapBuilder.BuildWifeHouse(_worldRoot.transform, bp.Position);
                break;
        }
        if (root != null)
        {
            _buildings.Add(new BuildingState
            {
                Entity = root,
                Type = bp.Type,
                Position = bp.Position,
                Rotation = bp.Rotation,
                PartStates = CollectColliderParts(root, bp.Type),
                CurrentHealth = 100,
                MaxHealth = 100,
                IsEssential = true
            });
        }
    }

    public BuildingState FindBuilding(GameObject obj)
    {
        Transform t = obj.transform;
        while (t.parent != null && t.parent.name != "WorldRoot")
            t = t.parent;
        foreach (var b in _buildings)
        {
            if (b.Entity == t.gameObject)
                return b;
        }
        return null;
    }

    public void DamageBuilding(GameObject hitObj)
    {
        var building = FindBuilding(hitObj);
        if (building == null) return;

        if (building.PartStates != null && building.PartStates.Count > 0)
        {
            bool partFound = false;
            foreach (var ps in building.PartStates)
            {
                if (ps.Entity == null) continue;
                if (hitObj == ps.Entity || (hitObj.transform.parent != null && hitObj.transform.parent.gameObject == ps.Entity))
                {
                    ps.CurrentHealth--;
                    if (ps.CurrentHealth <= 0)
                    {
                        ReplacePartWithGhost(building, ps);
                    }
                    partFound = true;
                    break;
                }
            }
            if (!partFound)
            {
                foreach (var ps in building.PartStates)
                {
                    if (ps.Entity != null)
                    {
                        ps.CurrentHealth--;
                        if (ps.CurrentHealth <= 0)
                        {
                            ReplacePartWithGhost(building, ps);
                        }
                        break;
                    }
                }
            }

            if (building.TotalParts > 0 && (float)building.DestroyedParts / building.TotalParts > 0.6f)
                RevertBuildingToBlueprint(building);
        }
        else
        {
            building.CurrentHealth -= 25;
            if (building.CurrentHealth <= 0)
                RevertBuildingToBlueprint(building);
        }

        UpdateBuildingDurabilityLabel(building);
    }

    private void ReplacePartWithGhost(BuildingState building, BuildingPartState ps)
    {
        if (ps.GhostEntity != null)
        {
            Object.Destroy(ps.GhostEntity);
            ps.GhostEntity = null;
        }

        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "PartGhost_" + ps.PartName;
        ghost.transform.SetParent(building.Entity.transform);
        ghost.transform.localPosition = ps.Entity.transform.localPosition;
        ghost.transform.localRotation = ps.Entity.transform.localRotation;
        ghost.transform.localScale = ps.Entity.transform.localScale;

        var renderer = ghost.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat != null)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0f);
            mat.renderQueue = 3000;
        }
        else
        {
            mat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        }
        mat.color = new Color(0.2f, 0.5f, 1f, 0.15f);
        renderer.material = mat;

        var collider = ghost.GetComponent<BoxCollider>();
        collider.isTrigger = true;

        float woodCost = 1f, stoneCost = 0f;
        int partCount = building.TotalParts;
        if (partCount > 0)
        {
            if (building.IsEssential)
            {
                GetEssentialCosts(building.Type, out float totalWood, out float totalStone);
                woodCost = totalWood / partCount;
                stoneCost = totalStone / partCount;
            }
            else
            {
                var def = System.Array.Find(_availableBuildings, d => d.Name == building.Type);
                if (def != null)
                {
                    woodCost = (float)def.WoodCost / partCount;
                    stoneCost = (float)def.StoneCost / partCount;
                }
            }
        }

        var labelObj = new GameObject("GhostLabel");
        labelObj.transform.SetParent(ghost.transform, false);
        float labelY = ps.Entity.transform.localScale.y * 0.5f + 0.3f;
        labelObj.transform.localPosition = new Vector3(0f, labelY, 0f);

        var tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.fontSize = 0.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        var parts = new List<string>();
        if (woodCost > 0.01f) parts.Add($"W:{woodCost:F1}");
        if (stoneCost > 0.01f) parts.Add($"S:{stoneCost:F1}");
        tmp.text = string.Join(" ", parts);

        Object.Destroy(ps.Entity);
        ps.Entity = null;
        ps.GhostEntity = ghost;
    }

    private float GetBuildingTopY(GameObject entity)
    {
        var renderers = entity.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.max.y;
        }
        return entity.transform.position.y + 2f;
    }

    private void UpdateBuildingDurabilityLabel(BuildingState building)
    {
        if (building.Entity == null) return;

        int current, max;
        if (building.PartStates != null && building.PartStates.Count > 0)
        {
            int total = building.TotalParts;
            int destroyed = building.DestroyedParts;
            int remaining = total - destroyed;
            current = remaining;
            max = total;
        }
        else
        {
            current = building.CurrentHealth;
            max = building.MaxHealth;
        }

        if (current >= max)
        {
            if (building.DurabilityLabel != null)
            {
                Object.Destroy(building.DurabilityLabel);
                building.DurabilityLabel = null;
            }
            return;
        }

        string text = $"{current}/{max}";

        if (building.DurabilityLabel == null)
        {
            var labelObj = new GameObject("DurabilityLabel");
            labelObj.transform.SetParent(_worldRoot.transform);
            var tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.fontSize = 1.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = Color.black;
            building.DurabilityLabel = labelObj;
        }

        float topY = GetBuildingTopY(building.Entity);
        building.DurabilityLabel.transform.position = new Vector3(building.Entity.transform.position.x, topY + 0.5f, building.Entity.transform.position.z);

        var tmp2 = building.DurabilityLabel.GetComponent<TextMeshPro>();
        if (tmp2 != null)
        {
            tmp2.text = text;
            tmp2.color = current <= max * 0.25f ? Color.red : Color.yellow;
        }
    }

    private void RevertBuildingToBlueprint(BuildingState state)
    {
        if (state.DurabilityLabel != null)
        {
            Object.Destroy(state.DurabilityLabel);
            state.DurabilityLabel = null;
        }

        if (state.PartStates != null)
        {
            foreach (var ps in state.PartStates)
            {
                if (ps.GhostEntity != null)
                {
                    Object.Destroy(ps.GhostEntity);
                    ps.GhostEntity = null;
                }
            }
        }

        if (IsFloorType(state.Type))
        {
            var key = new Vector3Int(Mathf.RoundToInt(state.Position.x), 0, Mathf.RoundToInt(state.Position.z));
            _floorPositions.Remove(key);
        }

        if (state.IsEssential)
        {
            if (state.Entity != null)
                Object.Destroy(state.Entity);
            _buildings.Remove(state);
            CreateEssentialBlueprint(state);
            return;
        }

        var def = System.Array.Find(_availableBuildings, d => d.Name == state.Type);
        if (def == null) return;

        if (state.Entity != null)
            Object.Destroy(state.Entity);
        _buildings.Remove(state);

        var blueprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueprint.name = "Blueprint";
        Vector3 size = def.Size;
        blueprint.transform.position = state.Position + Vector3.up * (size.y * 0.5f);
        blueprint.transform.rotation = Quaternion.Euler(0f, state.Rotation, 0f);
        blueprint.transform.localScale = size;
        var renderer = blueprint.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat != null)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0f);
            mat.renderQueue = 3000;
        }
        else
        {
            mat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        }
        mat.color = new Color(0.2f, 0.5f, 1f, 0.15f);
        renderer.material = mat;
        var collider = blueprint.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        blueprint.transform.SetParent(_worldRoot.transform);

        var bpState = new BlueprintState
        {
            Entity = blueprint,
            Type = state.Type,
            Position = state.Position,
            Rotation = state.Rotation,
            WoodDeposited = 0,
            StoneDeposited = 0
        };
        CreateBlueprintLabel(blueprint, bpState, def);
        blueprint.AddComponent<BlueprintAutoDeposit>();
        _blueprints.Add(bpState);
    }

    private void CreateEssentialBlueprint(BuildingState state)
    {
        float size = 6f;
        var blueprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueprint.name = "Blueprint";
        blueprint.transform.position = state.Position + Vector3.up * (size * 0.5f);
        blueprint.transform.localScale = Vector3.one * size;
        var renderer = blueprint.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat != null)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0f);
            mat.renderQueue = 3000;
        }
        else
        {
            mat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        }
        mat.color = new Color(0.2f, 0.5f, 1f, 0.15f);
        renderer.material = mat;
        var collider = blueprint.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        blueprint.transform.SetParent(_worldRoot.transform);

        float woodCost, stoneCost;
        GetEssentialCosts(state.Type, out woodCost, out stoneCost);

        var bpState = new BlueprintState
        {
            Entity = blueprint,
            Type = state.Type,
            Position = state.Position,
            Rotation = state.Rotation,
            WoodDeposited = 0,
            StoneDeposited = 0,
            IsEssential = true,
            WoodCost = woodCost,
            StoneCost = stoneCost
        };
        CreateEssentialBlueprintLabel(blueprint, bpState);
        blueprint.AddComponent<BlueprintAutoDeposit>();
        _blueprints.Add(bpState);
    }

    private void GetEssentialCosts(string type, out float wood, out float stone)
    {
        switch (type)
        {
            case "PlayerHouse": wood = 50; stone = 30; break;
            case "Shop":        wood = 40; stone = 20; break;
            case "WifeHouse":   wood = 60; stone = 40; break;
            default:            wood = 30; stone = 20; break;
        }
    }

    private void CreateEssentialBlueprintLabel(GameObject blueprint, BlueprintState bp)
    {
        var labelObj = new GameObject("BlueprintLabel");
        labelObj.transform.SetParent(blueprint.transform, false);
        labelObj.transform.localPosition = Vector3.zero;

        var tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = GetBlueprintRemainingText(bp, bp.WoodCost, bp.StoneCost);
        tmp.fontSize = 1f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = Color.black;

        bp.Label = labelObj;
    }

    public void RemoveBlueprint(GameObject hitObj)
    {
        var bp = FindBlueprint(hitObj);
        if (bp == null) return;
        if (bp.IsEssential) return;
        DestroyBlueprintLabel(bp);
        if (bp.Entity != null)
            Object.Destroy(bp.Entity);
        _blueprints.Remove(bp);
    }

    private void CreateBlueprintLabel(GameObject blueprint, BlueprintState bp, BuildingDefinition def)
    {
        var labelObj = new GameObject("BlueprintLabel");
        labelObj.transform.SetParent(blueprint.transform, false);
        labelObj.transform.localPosition = Vector3.zero;

        var tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = GetBlueprintRemainingText(bp, def.WoodCost, def.StoneCost);
        tmp.fontSize = Mathf.Clamp(def.Size.y * 0.18f, 0.5f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = Color.black;

        bp.Label = labelObj;
    }

    private void DestroyBlueprintLabel(BlueprintState bp)
    {
        if (bp.Label != null)
        {
            Object.Destroy(bp.Label);
            bp.Label = null;
        }
    }

    private string GetBlueprintRemainingText(BlueprintState bp, float woodCost, float stoneCost)
    {
        float woodRemaining = woodCost - bp.WoodDeposited;
        float stoneRemaining = stoneCost - bp.StoneDeposited;
        var parts = new List<string>();
        if (woodRemaining > 0.01f)
            parts.Add($"Wood: {woodRemaining:F1}");
        if (stoneRemaining > 0.01f)
            parts.Add($"Stone: {stoneRemaining:F1}");
        if (parts.Count == 0)
            return "Complete!";
        return "Need: " + string.Join(", ", parts);
    }

    private void UpdateBlueprintLabels()
    {
        var cam = Camera.main;
        if (cam == null) return;

        foreach (var bp in _blueprints)
        {
            if (bp.Label != null)
            {
                bp.Label.transform.LookAt(bp.Label.transform.position + cam.transform.rotation * Vector3.forward,
                    cam.transform.rotation * Vector3.up);
            }
        }

        foreach (var building in _buildings)
        {
            if (building.DurabilityLabel != null)
            {
                building.DurabilityLabel.transform.LookAt(building.DurabilityLabel.transform.position + cam.transform.rotation * Vector3.forward,
                    cam.transform.rotation * Vector3.up);
            }
            if (building.PartStates != null)
            {
                foreach (var ps in building.PartStates)
                {
                    if (ps.GhostEntity != null)
                    {
                        var ghostLabel = ps.GhostEntity.transform.Find("GhostLabel");
                        if (ghostLabel != null)
                        {
                            ghostLabel.LookAt(ghostLabel.position + cam.transform.rotation * Vector3.forward,
                                cam.transform.rotation * Vector3.up);
                        }
                    }
                }
            }
        }
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
        float roadLen = 300f;
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

    private void BuildRockyBorder()
    {
        float half = GroundSize.x * 0.5f;
        float spacing = 2.5f;
        float westX = -200f;

        void SpawnBorderSegment(Vector3 pos, float scale)
        {
            var rock = MapBuilder.BuildBorderRock(_worldRoot.transform, pos, scale);
            rock.name = "BorderRock";
        }

        for (float x = westX; x <= half; x += spacing)
        {
            SpawnBorderSegment(new Vector3(x, 0f, half), Random.Range(0.8f, 1.2f));
        }

        for (float x = westX; x <= half; x += spacing)
        {
            SpawnBorderSegment(new Vector3(x, 0f, -half), Random.Range(0.8f, 1.2f));
        }

        for (float z = -half; z <= half; z += spacing)
        {
            SpawnBorderSegment(new Vector3(half, 0f, z), Random.Range(0.8f, 1.2f));
        }

        for (float z = -half; z <= half; z += spacing)
        {
            SpawnBorderSegment(new Vector3(westX, 0f, z), Random.Range(0.8f, 1.2f));
        }
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
            treeRoot = MapBuilder.BuildTree(_worldRoot.transform, new Vector3(x, 0f, z));
            treeRoot.name = "Tree" + i;
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
                rock = MapBuilder.BuildStone(_worldRoot.transform, new Vector3(x, 0f, z));
                rock.name = "Rock" + i;
            }
            _rocks.Add(rock);
        }
    }

    private void BuildHouse()
    {
        var house = MapBuilder.BuildPlayerHouse(_worldRoot.transform, Vector3.zero);
        _buildings.Add(new BuildingState
        {
            Entity = house,
            Type = "PlayerHouse",
            Position = house.transform.position,
            Rotation = 0,
            PartStates = CollectColliderParts(house, "PlayerHouse"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
    }

    private List<BuildingPartState> CollectColliderParts(GameObject root, string prefix)
    {
        var parts = new List<BuildingPartState>();
        var colliders = root.GetComponentsInChildren<BoxCollider>();
        int index = 0;
        foreach (var col in colliders)
        {
            if (col.isTrigger) continue;
            parts.Add(new BuildingPartState
            {
                PartName = $"{prefix}_{index}",
                Entity = col.gameObject,
                CurrentHealth = 4
            });
            index++;
        }
        return parts;
    }

    private bool IsFloorType(string typeName)
    {
        return typeName == "wood_floor" || typeName == "stone_floor";
    }

    private bool HasFloorAt(Vector3 position)
    {
        int px = Mathf.RoundToInt(position.x);
        int pz = Mathf.RoundToInt(position.z);
        foreach (var fp in _floorPositions)
        {
            if (Mathf.Abs(fp.x - px) <= 2 && Mathf.Abs(fp.z - pz) <= 2)
                return true;
        }
        return false;
    }

    private void BuildBeach()
    {
        float beachX = -90f;
        float sandW = 35f;
        float sandD = 300f;
        Color sandC = new Color(0.85f, 0.76f, 0.55f);
        Color seaC = new Color(0.2f, 0.5f, 0.8f);

        MakeBlock("Sand", _worldRoot.transform, new Vector3(sandW, 0.02f, sandD),
            new Vector3(beachX, 0f, 0f), sandC, false, true);

        MakeBlock("Sea", _worldRoot.transform, new Vector3(120f, 0.06f, sandD),
            new Vector3(beachX - sandW * 0.5f - 60f, 0.03f, 0f), seaC, false, true);

        int numTrees = 50;
        for (int i = 0; i < numTrees; i++)
        {
            float x = beachX + Random.Range(-sandW * 0.35f, sandW * 0.35f);
            float z = Random.Range(-sandD * 0.4f, sandD * 0.4f);
            var tree = MapBuilder.BuildCoconutTree(_worldRoot.transform, new Vector3(x, 0f, z), Random.Range(0.8f, 1.2f));
            _trees.Add(tree);
        }
    }

    private void BuildShop()
    {
        var shop = MapBuilder.BuildShop(_worldRoot.transform, new Vector3(0f, 0f, 60f));
        _shopRoot = shop.transform;
        _buildings.Add(new BuildingState
        {
            Entity = shop,
            Type = "Shop",
            Position = shop.transform.position,
            Rotation = 0,
            PartStates = CollectColliderParts(shop, "Shop"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
    }

    private void BuildWifeHouse()
    {
        var wifeHouse = MapBuilder.BuildWifeHouse(_worldRoot.transform, new Vector3(33f, 0f, 0f));
        _buildings.Add(new BuildingState
        {
            Entity = wifeHouse,
            Type = "WifeHouse",
            Position = wifeHouse.transform.position,
            Rotation = 0,
            PartStates = CollectColliderParts(wifeHouse, "WifeHouse"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
        MapBuilder.BuildWifeNpc(_worldRoot.transform, new Vector3(30f, 0.86f, 0f), 1f, Quaternion.Euler(0f, 90f, 0f));
    }

    private void SpawnBuffalo()
    {
        if (_shopRoot == null) return;
        MapBuilder.BuildBuffalo(_shopRoot, new Vector3(-4.8f, 0f, 0f), 1.5f, Quaternion.Euler(0f, 0f, 0f));
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
        _vendorSpawnButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _vendorSpawnButton.name = "VendorSpawnButton";
        _vendorSpawnButton.transform.SetParent(_worldRoot.transform);
        _vendorSpawnButton.transform.position = new Vector3(0f, 0.2f, -9f);
        _vendorSpawnButton.transform.localScale = new Vector3(1.5f, 0.15f, 1.5f);
        var rend = _vendorSpawnButton.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.761f, 0.647f, 0.137f);
        var col = _vendorSpawnButton.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    public bool IsNearVendorSpawnButton(Vector3 position, float range = 3f)
    {
        if (_vendorSpawnButton == null) return false;
        return Vector3.Distance(position, _vendorSpawnButton.transform.position) <= range;
    }

    public void SpawnVendorCart()
    {
        // Mark existing vendors to exit
        foreach (var v in _vendorCarts)
        {
            if (!v.Exiting)
            {
                v.Exiting = true;
                v.Moving = false;
                v.ExitTarget = new Vector3(15f, 0.5f, 40f + Random.Range(-2f, 2f));
            }
        }

        var cart = new VendorCart();
        cart.Root = new GameObject("VendorCart");
        cart.Root.transform.SetParent(_worldRoot.transform);
        cart.Root.transform.position = new Vector3(15f, -4f, -30f);
        cart.Root.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        cart.ArrivalPos = new Vector3(15f, 0.5f, -8f);
        cart.TargetGroundY = 0.5f;
        cart.Speed = 6f;
        cart.Rising = true;
        cart.Wheels = new List<GameObject>();

        Color cartColor = new Color(
            Random.Range(80f, 255f) / 255f,
            Random.Range(50f, 220f) / 255f,
            Random.Range(50f, 220f) / 255f
        );
        Color darkColor = new Color(
            Mathf.Max(0, cartColor.r - 20f / 255f),
            Mathf.Max(0, cartColor.g - 40f / 255f),
            Mathf.Max(0, cartColor.b - 20f / 255f)
        );

        // ── Classic food truck ──
        // Identity rotation: local +Z = world +Z = front (movement), local -X = world -X = window toward player

        float halfW = 1.3f;  // half width (X), truck is 2.6m wide
        float halfD = 1.8f;  // half depth (Z), truck is 3.6m long
        float wallH = 1.6f;
        float floorY = 0.2f;
        float roofY = 2.0f;
        float cabDepth = 0.8f;
        float cabFrontZ = halfD;
        float cabBackZ = halfD - cabDepth;

        var modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(cart.Root.transform);
        modelRoot.transform.localPosition = Vector3.zero;
        modelRoot.transform.localRotation = Quaternion.identity;

        // Floor
        MakeBlock("TruckFloor", modelRoot.transform, new Vector3(halfW * 2f, 0.2f, halfD * 2f),
            new Vector3(0f, floorY, 0f), darkColor, true);

        // ── Front face (local +Z) ──
        MakeBlock("WallFront", modelRoot.transform, new Vector3(halfW * 2f - 0.2f, wallH, 0.15f),
            new Vector3(0f, floorY + wallH * 0.5f, cabFrontZ), cartColor, true);
        MakeBlock("Bumper", modelRoot.transform, new Vector3(halfW * 2f - 0.4f, 0.3f, 0.2f),
            new Vector3(0f, 0.15f, cabFrontZ + 0.1f), Color.gray, true);
        MakeBlock("Grille", modelRoot.transform, new Vector3(halfW * 2f - 0.4f, 0.5f, 0.1f),
            new Vector3(0f, floorY + 0.35f, cabFrontZ + 0.01f), new Color(0.15f, 0.15f, 0.15f), true);
        MakeBlock("Windshield", modelRoot.transform, new Vector3(halfW * 2f - 0.6f, 0.7f, 0.1f),
            new Vector3(0f, floorY + 1.05f, cabFrontZ + 0.01f), new Color(0.5f, 0.75f, 1f), true);
        MakeBlock("HeadlightL", modelRoot.transform, new Vector3(0.2f, 0.2f, 0.08f),
            new Vector3(-halfW + 0.3f, floorY + 0.5f, cabFrontZ + 0.12f), Color.white, true);
        MakeBlock("HeadlightR", modelRoot.transform, new Vector3(0.2f, 0.2f, 0.08f),
            new Vector3(halfW - 0.3f, floorY + 0.5f, cabFrontZ + 0.12f), Color.white, true);

        // ── Back wall (local -Z) ──
        MakeBlock("WallBack", modelRoot.transform, new Vector3(halfW * 2f - 0.2f, wallH, 0.15f),
            new Vector3(0f, floorY + wallH * 0.5f, -halfD), cartColor, true);

        // ── Right wall (local +X) — solid, full length ──
        MakeBlock("WallRight", modelRoot.transform, new Vector3(0.2f, wallH, halfD * 2f - 0.2f),
            new Vector3(halfW, floorY + wallH * 0.5f, 0f), cartColor, true);

        // ── Left side (local -X) — cab wall + counter (window above) + back wall ──
        float cabBackOffset = 0.1f;
        float winFrontZ = cabBackZ - cabBackOffset;
        float winBackZ = -halfD + 0.6f;
        float winLen = winFrontZ - winBackZ;
        float winCenterZ = (winFrontZ + winBackZ) * 0.5f;
        float xL = -halfW;

        MakeBlock("CabWallL", modelRoot.transform, new Vector3(0.17f, wallH, cabDepth - cabBackOffset),
            new Vector3(xL, floorY + wallH * 0.5f, halfD - cabDepth * 0.5f - cabBackOffset * 0.5f), cartColor, true);

        float counterH = 0.6f;
        MakeBlock("Counter", modelRoot.transform, new Vector3(0.17f, counterH, winLen),
            new Vector3(xL, floorY + counterH * 0.5f, winCenterZ), darkColor, true);

        float backLenL = winBackZ - (-halfD);
        float backCenterZ = (-halfD + winBackZ) * 0.5f;
        MakeBlock("WallBackL", modelRoot.transform, new Vector3(0.17f, wallH, backLenL),
            new Vector3(xL, floorY + wallH * 0.5f, backCenterZ), cartColor, true);

        // ── Roof ──
        MakeBlock("Roof", modelRoot.transform, new Vector3(halfW * 2f + 0.4f, 0.2f, halfD * 2f + 0.6f),
            new Vector3(0f, roofY, 0f), darkColor, true);

        // ── Awning over the window (left side) ──
        MakeBlock("Awning", modelRoot.transform, new Vector3(0.5f, 0.1f, winLen + 0.2f),
            new Vector3(xL - 0.3f, roofY - 0.05f, winCenterZ), darkColor, true);

        // ── Stripe along the body (on right wall) ──
        MakeBlock("Stripe", modelRoot.transform, new Vector3(0.08f, 0.08f, halfD * 2f - 0.4f),
            new Vector3(halfW + 0.06f, floorY + 0.45f, 0f), Color.white, true);

        // ── Wheels ──
        Vector3[] wheelPos = new Vector3[]
        {
            new Vector3(-halfW - 0.5f, -0.3f, -halfD + 0.5f),
            new Vector3(halfW + 0.5f, -0.3f, -halfD + 0.5f),
            new Vector3(-halfW - 0.5f, -0.3f, halfD - 0.5f),
            new Vector3(halfW + 0.5f, -0.3f, halfD - 0.5f)
        };
        foreach (var wp in wheelPos)
        {
            var w = MakeBlock("Wheel", modelRoot.transform, new Vector3(0.9f, 0.9f, 0.25f),
                wp, Color.black, true);
            MakeBlock("WheelRim", w.transform, new Vector3(0.45f, 0.45f, 0.08f),
                new Vector3(0f, 0f, 0.08f), cartColor, true);
            cart.Wheels.Add(w);
        }

        // ── Vendor NPC inside, near the window opening ──
        var vendorRoot = new GameObject("Vendor");
        vendorRoot.transform.SetParent(cart.Root.transform);
        vendorRoot.transform.localPosition = new Vector3(xL + 0.5f, 0f, winCenterZ);
        vendorRoot.transform.localRotation = Quaternion.identity;
        MakeBlock("VendorBody", vendorRoot.transform, new Vector3(0.6f, 1.2f, 0.5f),
            new Vector3(0f, floorY + 1.0f, 0f), new Color(0.565f, 0.78f, 0.945f), true);
        MakeBlock("VendorHead", vendorRoot.transform, new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0f, floorY + 1.9f, 0f), Color.white, true);
        MakeBlock("VendorArmL", vendorRoot.transform, new Vector3(0.15f, 0.6f, 0.15f),
            new Vector3(-0.4f, floorY + 1.3f, 0f), new Color(0.565f, 0.78f, 0.945f), true);
        MakeBlock("VendorArmR", vendorRoot.transform, new Vector3(0.15f, 0.6f, 0.15f),
            new Vector3(0.4f, floorY + 1.3f, 0f), new Color(0.565f, 0.78f, 0.945f), true);

        cart.VendorModel = vendorRoot;
        cart.ModelBaseY = vendorRoot.transform.localPosition.y;

        // Interaction trigger at the window (local -X side)
        var interactGO = new GameObject("VendorNPC");
        interactGO.transform.SetParent(cart.Root.transform);
        interactGO.transform.localPosition = new Vector3(xL - 0.1f, 1.0f, winCenterZ);
        var interactCol = interactGO.AddComponent<BoxCollider>();
        interactCol.isTrigger = true;
        interactCol.size = new Vector3(0.4f, 1.2f, winLen - 0.2f);

        // NPC bobbing
        cart.VendorReady = true;
        cart.VendorNPC = vendorRoot;

        _vendorCarts.Add(cart);
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
        _buildingPreview.SetActive(false);
        var renderer = _buildingPreview.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat != null)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0f);
            mat.renderQueue = 3000;
        }
        else
        {
            mat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        }
        renderer.material = mat;
    }

    private void UpdateBuildingPreview()
    {
        if (_buildingPreview == null)
            return;

        var definition = _availableBuildings[_currentBuildingIndex];
        _buildingPreview.transform.localScale = definition.Size;
        _buildingPreview.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
        var renderer = _buildingPreview.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(definition.Color.r, definition.Color.g, definition.Color.b, 0.04f);
    }

    public float SnapSize = 1f;

    private Vector3 SnapToGrid(Vector3 position)
    {
        float grid = SnapSize;
        return new Vector3(
            Mathf.Round(position.x / grid) * grid,
            position.y,
            Mathf.Round(position.z / grid) * grid
        );
    }

    public void UpdatePreviewPosition(Vector3 position, bool isValid)
    {
        if (_buildingPreview == null)
            return;

        var definition = _availableBuildings[_currentBuildingIndex];
        Vector3 snapped = SnapToGrid(position);
        bool floorOk = IsFloorType(definition.Name) || HasFloorAt(snapped);

        if (!isValid || !floorOk)
        {
            if (_buildingPreview.activeInHierarchy)
                _buildingPreview.SetActive(false);
            return;
        }

        if (!_buildingPreview.activeInHierarchy)
            _buildingPreview.SetActive(true);

        _buildingPreview.transform.position = snapped + Vector3.up * (definition.Size.y * 0.5f);

        var renderer = _buildingPreview.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(definition.Color.r, definition.Color.g, definition.Color.b, 0.04f);
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

        var renderer = field.FieldObject.GetComponent<MeshRenderer>();

        if (field.IsHarvested)
        {
            renderer.material.color = new Color(0.25f, 0.15f, 0.1f);
            return;
        }

        if (field.HasCrop)
        {
            if (field.Watered && field.Fertilized)
                renderer.material.color = new Color(0.20f, 0.40f, 0.20f);
            else if (field.Fertilized)
                renderer.material.color = new Color(0.25f, 0.45f, 0.15f);
            else if (field.Watered)
                renderer.material.color = new Color(0.30f, 0.35f, 0.18f);
            else
                renderer.material.color = new Color(0.55f, 0.32f, 0.10f);
            if (field.CropObject == null)
                UpdateCropVisual(field);
            return;
        }

        renderer.material.color = field.Tilled ? new Color(0.45f, 0.28f, 0.12f) : new Color(0.6f, 0.4f, 0.2f);
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
            case "carrot":
                CreateFieldCarrot(cropRoot.transform, field.Stage);
                break;
            case "tomato":
                CreateFieldTomato(cropRoot.transform, field.Stage);
                break;
            case "strawberry":
                CreateFieldStrawberry(cropRoot.transform, field.Stage);
                break;
            case "pumpkin":
                CreateFieldPumpkin(cropRoot.transform, field.Stage);
                break;
            case "onion":
                CreateFieldOnion(cropRoot.transform, field.Stage);
                break;
            case "sugarcane":
                CreateFieldSugarcane(cropRoot.transform, field.Stage);
                break;
            case "rice":
                CreateFieldRice(cropRoot.transform, field.Stage);
                break;
            default:
                CreateFieldWheat(cropRoot.transform, field.Stage);
                break;
        }

        field.CropObject = cropRoot;
    }

    private void AddFieldBorder(Transform tile)
    {
        var borderColor = new Color(0.2f, 0.1f, 0.03f);
        var rot = Quaternion.Euler(-90f, 0f, 0f);
        for (int i = 0; i < 4; i++)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.transform.SetParent(tile, false);
            edge.transform.localRotation = rot;
            edge.transform.localPosition = i < 2
                ? new Vector3(0f, (i == 0 ? -0.5f : 0.5f), -0.005f)
                : new Vector3((i == 2 ? -0.5f : 0.5f), 0f, -0.005f);
            edge.transform.localScale = i < 2
                ? new Vector3(1f, 0.02f, 0.01f)
                : new Vector3(0.01f, 0.02f, 1f);
            edge.GetComponent<Renderer>().material.color = borderColor;
            Destroy(edge.GetComponent<Collider>());
        }
    }

    private void CreateFieldWheat(Transform parent, int stage)
    {
        int bladeCount = Random.Range(8, 14);
        float height = 0.25f + stage * 0.08f;
        Color color = stage >= 3 ? new Color(1f, 0.9f, 0.2f) : new Color(0.85f, 0.8f, 0.2f);

        for (int i = 0; i < bladeCount; i++)
        {
            float width = Random.Range(0.05f, 0.08f);
            float depth = 0.03f;
            float x = Random.Range(-0.3f, 0.3f);
            float z = Random.Range(-0.3f, 0.3f);
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(parent, false);
            blade.transform.localScale = new Vector3(width, height, depth);
            blade.transform.localPosition = new Vector3(x, height / 2f, z);
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
            var rend = blade.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = color;
            Destroy(blade.GetComponent<Collider>());
        }
    }

    private void CreateFieldCorn(Transform parent, int stage)
    {
        float stalkHeight = 0.3f + stage * 0.1f;
        var stalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stalk.transform.SetParent(parent, false);
        stalk.transform.localScale = new Vector3(0.08f, stalkHeight, 0.08f);
        stalk.transform.localPosition = new Vector3(0f, stalkHeight / 2f, 0f);
        var rendStalk = stalk.GetComponent<Renderer>();
        if (rendStalk != null)
            rendStalk.material.color = new Color(0.3f, 0.7f, 0.25f);
        Destroy(stalk.GetComponent<Collider>());

        if (stage >= 3)
            CreateCornEar(parent, 0f, 0f, stalkHeight);

        if (stage >= 4)
        {
            for (int t = 0; t < 2; t++)
            {
                float stalk2X = Random.Range(-0.15f, 0.15f);
                float stalk2Z = Random.Range(-0.15f, 0.15f);
                float h = 0.25f + stage * 0.08f;
                var stalk2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stalk2.transform.SetParent(parent, false);
                stalk2.transform.localScale = new Vector3(0.06f, h, 0.06f);
                stalk2.transform.localPosition = new Vector3(stalk2X, h / 2f, stalk2Z);
                stalk2.GetComponent<Renderer>().material.color = new Color(0.3f, 0.7f, 0.25f);
                Destroy(stalk2.GetComponent<Collider>());
                CreateCornEar(parent, stalk2X, stalk2Z, h);
            }
        }
    }

    private void CreateCornEar(Transform parent, float xOff, float zOff, float stalkH)
    {
        Color cornColor = new Color(1f, 0.85f, 0.2f);
        float earY = stalkH * 1.0f;
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                float angle = j * 72f;
                var kernel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kernel.transform.SetParent(parent, false);
                kernel.transform.localScale = new Vector3(0.12f, 0.02f, 0.03f);
                kernel.transform.localRotation = Quaternion.Euler(0f, angle + i * 18f, 0f);
                kernel.transform.localPosition = new Vector3(xOff, earY + i * 0.02f, zOff);
                var rend = kernel.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = cornColor;
                Destroy(kernel.GetComponent<Collider>());
            }
        }
    }

    private void CreateFieldPotato(Transform parent, int stage)
    {
        float targetRatio = stage / 4f;

        for (int t = 0; t < 3; t++)
        {
            float xOff = Random.Range(-0.08f, 0.08f);
            float zOff = Random.Range(-0.08f, 0.08f);
            var tuber = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tuber.transform.SetParent(parent, false);
            float rootScale = 1f + 0.3f * targetRatio;
            tuber.transform.localScale = new Vector3(0.1f * rootScale, 0.08f * rootScale, 0.09f * rootScale);
            tuber.transform.localPosition = new Vector3(xOff, 0.03f * rootScale, zOff);
            var rendTuber = tuber.GetComponent<Renderer>();
            if (rendTuber != null)
                rendTuber.material.color = new Color(0.65f, 0.45f, 0.2f);
            Destroy(tuber.GetComponent<Collider>());
        }

        int leafCount = 6 + stage;
        float radius = 0.08f + 0.1f * targetRatio;
        float leafHeight = 0.12f + 0.1f * targetRatio;
        Color leafColor = new Color(0.3f, 0.7f, 0.25f);

        for (int i = 0; i < leafCount; i++)
        {
            float angle = i * Mathf.PI * 2f / leafCount;
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.transform.SetParent(parent, false);
            leaf.transform.localScale = new Vector3(
                0.06f + 0.06f * targetRatio,
                0.015f,
                0.08f + 0.08f * targetRatio
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

    private void CreateFieldCarrot(Transform parent, int stage)
    {
        float ratio = stage / 4f;
        for (int c = 0; c < 3; c++)
        {
            float xOff = Random.Range(-0.1f, 0.1f);
            float zOff = Random.Range(-0.1f, 0.1f);
            float rootSize = 0.05f + ratio * 0.06f;
            float topHeight = 0.08f + ratio * 0.12f;
            float leafBaseY = 0.01f + rootSize * 0.5f;
            float leafSpread = 0.025f + ratio * 0.02f;
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f + c * 30f;
                float rad = angle * Mathf.Deg2Rad;
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.transform.SetParent(parent, false);
                leaf.transform.localScale = new Vector3(0.025f, topHeight, 0.05f);
                leaf.transform.localRotation = Quaternion.Euler(35f, angle, 0f);
                leaf.transform.localPosition = new Vector3(xOff + Mathf.Sin(rad) * leafSpread, leafBaseY + topHeight * 0.5f, zOff + Mathf.Cos(rad) * leafSpread);
                var rend = leaf.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.2f, 0.6f, 0.15f);
                Destroy(leaf.GetComponent<Collider>());
            }
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.transform.SetParent(parent, false);
            root.transform.localScale = new Vector3(0.04f + ratio * 0.04f, rootSize, 0.04f + ratio * 0.04f);
            root.transform.localPosition = new Vector3(xOff, 0.01f, zOff);
            var rendRoot = root.GetComponent<Renderer>();
            if (rendRoot != null) rendRoot.material.color = new Color(1f, 0.55f, 0.1f);
            Destroy(root.GetComponent<Collider>());
        }
    }

    private void CreateFieldTomato(Transform parent, int stage)
    {
        float ratio = stage / 4f;
        float stalkHeight = 0.15f + ratio * 0.12f;
        var stalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stalk.transform.SetParent(parent, false);
        stalk.transform.localScale = new Vector3(0.05f, stalkHeight, 0.05f);
        stalk.transform.localPosition = new Vector3(0f, stalkHeight / 2f, 0f);
        var rendStalk = stalk.GetComponent<Renderer>();
        if (rendStalk != null) rendStalk.material.color = new Color(0.2f, 0.5f, 0.15f);
        Destroy(stalk.GetComponent<Collider>());

        if (stage >= 2)
        {
            for (int t = 0; t < stage; t++)
            {
                float fruitSize = 0.05f + (stage - 1) * 0.03f;
                float angle = t * 90f;
                var fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fruit.transform.SetParent(parent, false);
                fruit.transform.localScale = Vector3.one * fruitSize;
                fruit.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.06f, stalkHeight * 0.3f + t * stalkHeight * 0.2f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.06f);
                var rendFruit = fruit.GetComponent<Renderer>();
                if (rendFruit != null) rendFruit.material.color = stage >= 4 ? new Color(1f, 0.2f, 0.1f) : new Color(0.5f, 0.8f, 0.2f);
                Destroy(fruit.GetComponent<Collider>());
            }
        }
    }

    private void CreateFieldStrawberry(Transform parent, int stage)
    {
        float ratio = stage / 4f;
        float bushSize = 0.08f + ratio * 0.08f;
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f;
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.transform.SetParent(parent, false);
            leaf.transform.localScale = new Vector3(0.04f, 0.012f, bushSize * 0.5f);
            leaf.transform.localRotation = Quaternion.Euler(20f, angle, 0f);
            leaf.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * bushSize * 0.35f, bushSize * 0.4f, Mathf.Sin(angle * Mathf.Deg2Rad) * bushSize * 0.35f);
            var rend = leaf.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.15f, 0.55f, 0.1f);
            Destroy(leaf.GetComponent<Collider>());
        }
        if (stage >= 3)
        {
            int fruitCount = stage == 3 ? 5 : 8;
            for (int i = 0; i < fruitCount; i++)
            {
                float angle = i * (360f / fruitCount) + 10f;
                var fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fruit.transform.SetParent(parent, false);
                float fSize = 0.04f;
                fruit.transform.localScale = Vector3.one * fSize;
                fruit.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * bushSize * 0.5f, 0.025f, Mathf.Sin(angle * Mathf.Deg2Rad) * bushSize * 0.5f);
                var rend = fruit.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(1f, 0.15f, 0.15f);
                Destroy(fruit.GetComponent<Collider>());
            }
        }
    }

    private void CreateFieldPumpkin(Transform parent, int stage)
    {
        float ratio = stage / 4f;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            var vine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vine.transform.SetParent(parent, false);
            float vineLen = 0.06f + ratio * 0.12f;
            vine.transform.localScale = new Vector3(0.03f, 0.015f, vineLen);
            vine.transform.localRotation = Quaternion.Euler(0f, angle, 20f);
            vine.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * vineLen * 0.3f, 0.015f, Mathf.Sin(angle * Mathf.Deg2Rad) * vineLen * 0.3f);
            var rend = vine.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.2f, 0.5f, 0.1f);
            Destroy(vine.GetComponent<Collider>());
        }
        for (int p = 0; p < 2; p++)
        {
            float xOff = Random.Range(-0.06f, 0.06f);
            float zOff = Random.Range(-0.06f, 0.06f);
            var pumpkin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pumpkin.transform.SetParent(parent, false);
            float pSize = 0.06f + ratio * 0.1f;
            pumpkin.transform.localScale = Vector3.one * pSize;
            pumpkin.transform.localPosition = new Vector3(xOff, pSize * 0.5f, zOff);
            var rendP = pumpkin.GetComponent<Renderer>();
            if (rendP != null) rendP.material.color = stage >= 3 ? new Color(1f, 0.6f, 0.1f) : new Color(0.8f, 0.7f, 0.3f);
            Destroy(pumpkin.GetComponent<Collider>());
        }
    }

    private void CreateFieldOnion(Transform parent, int stage)
    {
        float ratio = stage / 4f;
        int shootCount = 5 + stage * 2;
        for (int i = 0; i < shootCount; i++)
        {
            float shootHeight = 0.08f + ratio * 0.12f;
            var shoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shoot.transform.SetParent(parent, false);
            shoot.transform.localScale = new Vector3(0.02f, shootHeight, 0.02f);
            float xOff = Random.Range(-0.08f, 0.08f);
            float zOff = Random.Range(-0.08f, 0.08f);
            shoot.transform.localPosition = new Vector3(xOff, shootHeight / 2f, zOff);
            shoot.transform.localRotation = Quaternion.Euler(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
            var rend = shoot.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.2f, 0.5f, 0.1f);
            Destroy(shoot.GetComponent<Collider>());
        }
        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.transform.SetParent(parent, false);
        float bSize = 0.07f + ratio * 0.08f;
        bulb.transform.localScale = Vector3.one * bSize;
        bulb.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        var rendB = bulb.GetComponent<Renderer>();
        if (rendB != null) rendB.material.color = stage >= 3 ? new Color(0.8f, 0.5f, 0.2f) : new Color(0.7f, 0.6f, 0.4f);
        Destroy(bulb.GetComponent<Collider>());
    }

    private void CreateFieldSugarcane(Transform parent, int stage)
    {
        int stalkCount = 2 + stage;
        for (int s = 0; s < stalkCount; s++)
        {
            float stalkHeight = 0.2f + stage * 0.08f;
            float xOff = Random.Range(-0.12f, 0.12f);
            float zOff = Random.Range(-0.12f, 0.12f);
            var stalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stalk.transform.SetParent(parent, false);
            stalk.transform.localScale = new Vector3(0.05f, stalkHeight, 0.05f);
            stalk.transform.localPosition = new Vector3(xOff, stalkHeight / 2f, zOff);
            var rend = stalk.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.3f, 0.7f, 0.15f);
            Destroy(stalk.GetComponent<Collider>());

            for (int i = 1; i < stage; i++)
            {
                float yPos = i * (stalkHeight / stage);
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.transform.SetParent(parent, false);
                segment.transform.localScale = new Vector3(0.06f, 0.015f, 0.06f);
                segment.transform.localPosition = new Vector3(xOff, yPos, zOff);
                var rendS = segment.GetComponent<Renderer>();
                if (rendS != null) rendS.material.color = new Color(0.6f, 0.8f, 0.3f);
                Destroy(segment.GetComponent<Collider>());
            }
        }
    }

    private void CreateFieldRice(Transform parent, int stage)
    {
        int stalkCount = 3 + stage;
        for (int s = 0; s < stalkCount; s++)
        {
            float stalkHeight = 0.15f + stage * 0.08f;
            float xOff = Random.Range(-0.15f, 0.15f);
            float zOff = Random.Range(-0.15f, 0.15f);
            var stalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stalk.transform.SetParent(parent, false);
            stalk.transform.localScale = new Vector3(0.03f, stalkHeight, 0.03f);
            stalk.transform.localPosition = new Vector3(xOff, stalkHeight / 2f, zOff);
            var rend = stalk.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.25f, 0.6f, 0.15f);
            Destroy(stalk.GetComponent<Collider>());

            if (stage >= 3)
            {
                int grainCount = stage == 3 ? 5 : 8;
                Color grainColor = stage >= 4 ? new Color(1f, 0.9f, 0.3f) : new Color(0.8f, 0.8f, 0.4f);
                for (int i = 0; i < grainCount; i++)
                {
                    float angle = i * (360f / grainCount);
                    var grain = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    grain.transform.SetParent(parent, false);
                    grain.transform.localScale = new Vector3(0.04f, 0.08f, 0.025f);
                    grain.transform.localRotation = Quaternion.Euler(0f, angle, 25f);
                    grain.transform.localPosition = new Vector3(xOff + Mathf.Cos(angle * Mathf.Deg2Rad) * 0.05f, stalkHeight + 0.02f, zOff + Mathf.Sin(angle * Mathf.Deg2Rad) * 0.05f);
                    var rendG = grain.GetComponent<Renderer>();
                    if (rendG != null) rendG.material.color = grainColor;
                    Destroy(grain.GetComponent<Collider>());
                }
            }
        }
    }

    public void ClearPersistentData()
    {
        foreach (var kvp in _treeChopStates)
        {
            if (kvp.Value.ChopMark != null) Destroy(kvp.Value.ChopMark);
        }
        _treeChopStates.Clear();

        foreach (var kvp in _branchChopStates)
        {
            if (kvp.Value.ChopMark != null) Destroy(kvp.Value.ChopMark);
        }
        _branchChopStates.Clear();

        foreach (var kvp in _rockCrackStates)
        {
            foreach (var crack in kvp.Value.Cracks)
            {
                if (crack.Obj != null) Destroy(crack.Obj);
            }
        }
        _rockCrackStates.Clear();

        foreach (var field in _fields)
        {
            if (field.FieldObject != null) Destroy(field.FieldObject);
            if (field.CropObject != null) Destroy(field.CropObject);
        }
        _fields.Clear();

        foreach (var building in _buildings)
        {
            if (building.Entity != null) Destroy(building.Entity);
            if (building.DurabilityLabel != null) Destroy(building.DurabilityLabel);
            if (building.PartStates != null)
            {
                foreach (var ps in building.PartStates)
                    ps.GhostEntity = null;
            }
        }
        _buildings.Clear();
        _floorPositions.Clear();

        var demo = _worldRoot?.transform.Find("CropDemo");
        if (demo != null) Destroy(demo.gameObject);
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
                isHarvested = field.IsHarvested,
                watered = field.Watered,
                fertilized = field.Fertilized,
                waterTimer = field.WaterTimer
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
                field.Watered = fieldSave.watered;
                field.Fertilized = fieldSave.fertilized;
                field.WaterTimer = fieldSave.waterTimer;
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
            int[] partHealths = null;
            if (b.PartStates != null)
            {
                partHealths = new int[b.PartStates.Count];
                for (int j = 0; j < b.PartStates.Count; j++)
                    partHealths[j] = b.PartStates[j].CurrentHealth;
            }
            result[i] = new BuildingSaveData
            {
                type = b.Type,
                position = b.Position,
                rotation = b.Rotation,
                currentHealth = b.CurrentHealth,
                maxHealth = b.MaxHealth,
                partHealths = partHealths
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
            if (SpawnBuildingDirect(build.type, build.position, build.rotation))
            {
                var last = _buildings[_buildings.Count - 1];
                last.CurrentHealth = build.currentHealth;
                last.MaxHealth = build.maxHealth;
                if (last.PartStates != null && build.partHealths != null)
                {
                    int count = Mathf.Min(last.PartStates.Count, build.partHealths.Length);
                    for (int p = 0; p < count; p++)
                        last.PartStates[p].CurrentHealth = build.partHealths[p];
                }
            }
        }

        RebuildFloorPositions();
    }

    private void RebuildFloorPositions()
    {
        _floorPositions.Clear();
        foreach (var building in _buildings)
        {
            if (IsFloorType(building.Type))
            {
                var key = new Vector3Int(Mathf.RoundToInt(building.Position.x), 0, Mathf.RoundToInt(building.Position.z));
                _floorPositions.Add(key);
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
        public bool watered;
        public bool fertilized;
        public float waterTimer;
    }

    [System.Serializable]
    public class BuildingSaveData
    {
        public string type;
        public Vector3 position;
        public int rotation;
        public int currentHealth;
        public int maxHealth;
        public int[] partHealths;
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
        public bool Watered;
        public bool Fertilized;
        public float WaterTimer;
    }

    [System.Serializable]
    public class BuildingPartState
    {
        public string PartName;
        public GameObject Entity;
        public int CurrentHealth;
        public GameObject GhostEntity;
    }

    [System.Serializable]
    public class BuildingState
    {
        public GameObject Entity;
        public string Type;
        public Vector3 Position;
        public int Rotation;
        public List<BuildingPartState> PartStates;
        public int CurrentHealth;
        public int MaxHealth;
        public bool IsEssential;
        public GameObject DurabilityLabel;

        public int TotalParts => PartStates?.Count ?? 1;
        public int DestroyedParts
        {
            get
            {
                if (PartStates == null || PartStates.Count == 0) return CurrentHealth <= 0 ? 1 : 0;
                int count = 0;
                foreach (var ps in PartStates)
                {
                    if (ps.Entity == null) count++;
                }
                return count;
            }
        }
    }

    public class BlueprintState
    {
        public GameObject Entity;
        public string Type;
        public Vector3 Position;
        public int Rotation;
        public float WoodDeposited;
        public float StoneDeposited;
        public GameObject Label;
        public bool IsEssential;
        public float WoodCost;
        public float StoneCost;
    }

    public (string material, float amount) GetResourceAmount(GameObject obj)
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

    [System.Serializable]
    public class BuildingPartDefinition
    {
        public string PartName;
        public Vector3 LocalPosition;
        public Vector3 LocalScale;
        public string MaterialType;
    }

    public class BuildingDefinition
    {
        public string Name;
        public Vector3 Size;
        public Color Color;
        public int WoodCost;
        public int StoneCost;
        public BuildingPartDefinition[] Parts;
        public Color WoodColor;
        public Color StoneColor;

        public BuildingDefinition(string name, Vector3 size, Color color, int woodCost, int stoneCost,
            BuildingPartDefinition[] parts = null, Color? woodColor = null, Color? stoneColor = null)
        {
            Name = name;
            Size = size;
            Color = color;
            WoodCost = woodCost;
            StoneCost = stoneCost;
            Parts = parts;
            WoodColor = woodColor ?? new Color(0.63f, 0.39f, 0.18f);
            StoneColor = stoneColor ?? new Color(0.41f, 0.41f, 0.41f);
        }
    }
}

public class BlueprintAutoDeposit : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var root = other.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;

        if (root.name != "TreeFelled" && root.name != "BranchTop" && root.name != "RockDebris")
            return;

        var wb = WorldBuilder.Instance;
        if (wb == null) return;

        var bp = wb.FindBlueprint(gameObject);
        if (bp == null) return;

        var info = wb.GetResourceAmount(root);
        if (info.material == null || info.amount < 0.05f) return;

        wb.DepositMaterial(bp, info.material, info.amount);
        Destroy(root);
    }
}
