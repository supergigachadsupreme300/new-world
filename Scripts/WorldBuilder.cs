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
    private GameObject _vendorSpawnButton;

    private class VendorCart
    {
        public GameObject Root;
        public List<GameObject> Wheels;
        public GameObject VendorModel;
        public Vector3 ArrivalPos;
        public Vector3? ExitTarget;
        public float Speed;
        public bool Moving;
        public bool Exiting;
        public float ModelBaseY;
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
        public GameObject ChopMark;
        public float ChopProgress;
        public Vector3 HitWorldPoint;
        public Vector3 HitNormal;
        public float HitLocalY;
        public bool IsHitOnX;
        public Vector3 CenterWorld;
        public float InitialDepth;
    }

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

        // Vendor cart movement
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
            if (v.Wheels != null && (v.Moving || v.Exiting))
            {
                float rot = 360f * deltaTime;
                foreach (var w in v.Wheels)
                {
                    if (w != null)
                        w.transform.Rotate(rot, 0f, 0f);
                }
            }

            // Bob vendor model
            if (v.VendorModel != null)
            {
                float bob = Mathf.Sin(Time.time * 2f) * 0.05f;
                var lp = v.VendorModel.transform.localPosition;
                lp.y = v.ModelBaseY + bob;
                v.VendorModel.transform.localPosition = lp;
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
            if (topTrunkR != null) topTrunkR.material.color = new Color(0.36f, 0.23f, 0.12f);

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
        Vector3 branchPos = branch.localPosition;
        Quaternion branchRot = branch.localRotation;
        float fullH = branch.localScale.y;
        float fullW = branch.localScale.x;
        Vector3 branchUp = branchRot * Vector3.up;

        Vector3 chopLocal = branch.parent.InverseTransformPoint(state.CenterWorld);
        Vector3 bottomLocal = branchPos + branchRot * new Vector3(0, -fullH / 2f, 0);
        float cutHeight = Mathf.Max(0.1f, Vector3.Dot(chopLocal - bottomLocal, branchUp));
        float topH = fullH - cutHeight;

        branch.localPosition = Vector3.Lerp(bottomLocal, chopLocal, 0.5f);
        branch.localScale = new Vector3(fullW, cutHeight, fullW);

        if (topH > 0.2f)
        {
            var topRoot = new GameObject("BranchTop");
            topRoot.transform.position = branch.parent.TransformPoint(chopLocal + branchUp * (topH / 2f));
            topRoot.transform.rotation = branch.parent.rotation;
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
        }

        if (state.ChopMark != null)
        {
            Destroy(state.ChopMark);
            state.ChopMark = null;
        }
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
        MapBuilder.BuildPlayerHouse(_worldRoot.transform, Vector3.zero);
    }

    private void BuildShop()
    {
        var shop = MapBuilder.BuildShop(_worldRoot.transform, new Vector3(0f, 0f, 60f));
        _shopRoot = shop.transform;
    }

    private void BuildWifeHouse()
    {
        MapBuilder.BuildWifeHouse(_worldRoot.transform, new Vector3(33f, 0f, 0f));
    }

    private void SpawnBuffalo()
    {
        if (_shopRoot == null) return;
        MapBuilder.BuildBuffalo(_shopRoot, new Vector3(-3.8f, 0f, 0f), 1.5f, Quaternion.Euler(0f, 90f, 0f));
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
        cart.Root.transform.position = new Vector3(15f, 0.5f, -30f);
        cart.Root.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        cart.ArrivalPos = new Vector3(15f, 0.5f, -8f);
        cart.Speed = 6f;
        cart.Moving = true;
        cart.Wheels = new List<GameObject>();

        Color cartColor = new Color(
            Random.Range(80f, 255f) / 255f,
            Random.Range(50f, 220f) / 255f,
            Random.Range(50f, 220f) / 255f
        );

        // Cart body
        MakeBlock("CartBody", cart.Root.transform, new Vector3(4f, 1.4f, 2f),
            new Vector3(0f, 0.9f, 0f), cartColor, true);
        Color darkColor = new Color(
            Mathf.Max(0, cartColor.r - 20f / 255f),
            Mathf.Max(0, cartColor.g - 40f / 255f),
            Mathf.Max(0, cartColor.b - 20f / 255f)
        );
        MakeBlock("CartTop", cart.Root.transform, new Vector3(4.2f, 0.4f, 2.2f),
            new Vector3(0f, 1.6f, 0f), darkColor, true);
        MakeBlock("CartStand", cart.Root.transform, new Vector3(0.2f, 0.5f, 1.8f),
            new Vector3(2f, 1.1f, 0f), Color.gray, true);
        MakeBlock("CartStandFront", cart.Root.transform, new Vector3(0.5f, 0.7f, 2f),
            new Vector3(2f, 0.5f, 0f), Color.white, true);

        // Wheels
        Vector3[] wheelPos = new Vector3[]
        {
            new Vector3(-1.4f, -0.35f, -1f),
            new Vector3(1.4f, -0.35f, -1f),
            new Vector3(-1.4f, -0.35f, 1f),
            new Vector3(1.4f, -0.35f, 1f)
        };
        foreach (var wp in wheelPos)
        {
            var w = MakeBlock("Wheel", cart.Root.transform, new Vector3(0.8f, 0.8f, 0.2f),
                wp, Color.black, true);
            MakeBlock("WheelRim", w.transform, new Vector3(0.4f, 0.4f, 0.06f),
                new Vector3(0f, 0f, 0.08f), cartColor, true);
            cart.Wheels.Add(w);
        }

        // Vendor character (simple blocky version)
        var vendorRoot = new GameObject("Vendor");
        vendorRoot.transform.SetParent(cart.Root.transform);
        vendorRoot.transform.localPosition = new Vector3(0f, 0f, 1.8f);
        MakeBlock("VendorBody", vendorRoot.transform, new Vector3(0.5f, 1f, 0.4f),
            new Vector3(0f, 1f, 0f), new Color(0.565f, 0.78f, 0.945f), true);
        MakeBlock("VendorHead", vendorRoot.transform, new Vector3(0.45f, 0.45f, 0.45f),
            new Vector3(0f, 1.9f, 0f), Color.white, true);
        MakeBlock("VendorArmL", vendorRoot.transform, new Vector3(0.15f, 0.6f, 0.15f),
            new Vector3(-0.35f, 1.2f, 0f), new Color(0.565f, 0.78f, 0.945f), true);
        MakeBlock("VendorArmR", vendorRoot.transform, new Vector3(0.15f, 0.6f, 0.15f),
            new Vector3(0.35f, 1.2f, 0f), new Color(0.565f, 0.78f, 0.945f), true);
        MakeBlock("VendorLegL", vendorRoot.transform, new Vector3(0.18f, 0.7f, 0.18f),
            new Vector3(-0.15f, 0.35f, 0f), Color.blue, true);
        MakeBlock("VendorLegR", vendorRoot.transform, new Vector3(0.18f, 0.7f, 0.18f),
            new Vector3(0.15f, 0.35f, 0f), Color.blue, true);

        cart.VendorModel = vendorRoot;
        cart.ModelBaseY = vendorRoot.transform.localPosition.y;

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
            renderer.material.color = new Color(definition.Color.r, definition.Color.g, definition.Color.b, 0.4f);
    }

    public void UpdatePreviewPosition(Vector3 position, bool isValid)
    {
        if (_buildingPreview == null)
            return;

        if (!isValid)
        {
            if (_buildingPreview.activeInHierarchy)
                _buildingPreview.SetActive(false);
            return;
        }

        if (!_buildingPreview.activeInHierarchy)
            _buildingPreview.SetActive(true);

        var definition = _availableBuildings[_currentBuildingIndex];
        _buildingPreview.transform.position = position + Vector3.up * (definition.Size.y * 0.5f);

        var renderer = _buildingPreview.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(definition.Color.r, definition.Color.g, definition.Color.b, 0.4f);
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
            float earY = stalkHeight * 0.65f;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    float angle = j * 72f;
                    var kernel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    kernel.transform.SetParent(parent, false);
                    kernel.transform.localScale = new Vector3(0.1f, 0.02f, 0.025f);
                    kernel.transform.localRotation = Quaternion.Euler(0f, angle + i * 18f, 0f);
                    kernel.transform.localPosition = new Vector3(0f, earY + i * 0.02f, 0f);
                    var rend = kernel.GetComponent<Renderer>();
                    if (rend != null)
                        rend.material.color = cornColor;
                    Destroy(kernel.GetComponent<Collider>());
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
