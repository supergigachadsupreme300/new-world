using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;
using CountryLife.Helpers;

public partial class WorldBuilder : MonoSingleton<WorldBuilder>
{
public int BuildingCount => _buildings.Count;

    public int TreeCount = 150;
    public int RockCount = 75;
    public Vector3 GroundSize = new Vector3(600f, 0.2f, 600f);

    public int MapWidth = 40;
    public int MapDepth = 40;
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
private static readonly Vector3 PagodaBasePos = new Vector3(26f, 0f, 25f);
    private const float PagodaExcludeHalf = 12f;
    private Vector3 _pagodaPosition;
    public Vector3 PagodaPosition => _pagodaPosition;
    private readonly Vector3 _bossArenaCenter = new Vector3(280f, 0f, 90f);
    public Vector3 BossArenaCenter => _bossArenaCenter;
    private static Texture2D _cachedDirtTex;
    private GameObject _questBoss;
    public bool IsQuestBossAlive => _questBoss != null && _questBoss.activeInHierarchy;
    private GameObject _worldRoot;
    public GameObject WorldRoot => _worldRoot;
    public GameObject StaticWifeModel { get; private set; }
    private float _resourceRespawnTimer;
    private const float RespawnInterval = 60f;
    private const int MaxTrees = 375;
    private const int MaxRocks = 120;
    private int _treeNameCounter;
    private int _rockNameCounter;
    private GameObject _buildingPreview;
    // road bounds (published when building the road)
    private float _roadCenterX = 14f;
    private float _roadHalfWidth = 3.8f;
    private float _roadZStart = -100f;
    private float _roadZEnd = 100f;
    private float _roadTurnZ = 90f;
    private float _roadXEnd = 180f;
private Transform _shopRoot;
    private GameObject _policePostRoot;
    private GameObject _policeOfficerRoot;
    private GameObject _policeCarRoot;
    private GameObject _alignmentStrip;
    private readonly HashSet<GameObject> _openDoors = new HashSet<GameObject>();
    private bool _wasNight;
    private readonly List<GameObject> _clouds = new List<GameObject>();
    private readonly List<Light> _streetLights = new List<Light>();
    private int _worldFrameTick;
    private float _cloudSpawnTimer;
    private const int MaxClouds = 10;
    private const float CloudSpawnInterval = 35f;

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

    private readonly Dictionary<GameObject, List<RockCrackData>> _buildingPartCracks = new Dictionary<GameObject, List<RockCrackData>>();

    private readonly BuildingDefinition[] _availableBuildings = new[]
    {
        new BuildingDefinition("wood_wall", new Vector3(6f, 3f, 0.5f), ColorPalette.HouseWood, 4, 0),
        new BuildingDefinition("stone_wall", new Vector3(5f, 3f, 0.5f), new Color(0.41f, 0.41f, 0.41f), 0, 4),
        new BuildingDefinition("fence", new Vector3(4f, 1.5f, 0.3f), new Color(0.69f, 0.51f, 0.25f), 2, 0,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "PostLeft",   LocalPosition = new Vector3(-1.85f, 0f, 0f), LocalScale = new Vector3(0.15f, 1.5f, 0.15f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "PostCenter", LocalPosition = new Vector3(0f, 0f, 0f),      LocalScale = new Vector3(0.15f, 1.5f, 0.15f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "PostRight",  LocalPosition = new Vector3(1.85f, 0f, 0f),  LocalScale = new Vector3(0.15f, 1.5f, 0.15f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "RailTop",    LocalPosition = new Vector3(0f, 0.55f, 0f),  LocalScale = new Vector3(3.85f, 0.1f, 0.1f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "RailMid",    LocalPosition = new Vector3(0f, 0f, 0f),      LocalScale = new Vector3(3.85f, 0.1f, 0.1f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "RailBottom", LocalPosition = new Vector3(0f, -0.55f, 0f), LocalScale = new Vector3(3.85f, 0.1f, 0.1f),  MaterialType = "wood" }
            }),
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
        new BuildingDefinition("stone_floor", new Vector3(4f, 0.3f, 4f), new Color(0.41f, 0.41f, 0.41f), 0, 3),
        new BuildingDefinition("stair", new Vector3(3f, 3f, 1.5f), new Color(0.60f, 0.40f, 0.20f), 3, 1),
        new BuildingDefinition("table", new Vector3(2f, 1f, 2f), new Color(0.65f, 0.45f, 0.22f), 2, 0),
        new BuildingDefinition("chair", new Vector3(1f, 1.5f, 1f), new Color(0.58f, 0.38f, 0.18f), 1, 0),
        new BuildingDefinition("sofa", new Vector3(2f, 1f, 1.5f), new Color(0.55f, 0.35f, 0.16f), 3, 0),
        new BuildingDefinition("goblin_hut", new Vector3(3f, 2.5f, 3f), new Color(0.45f, 0.33f, 0.18f), 8, 4,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Floor",        LocalPosition = new Vector3(0f, -1.2f, 0f),    LocalScale = new Vector3(3f, 0.25f, 3f),    MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Back",    LocalPosition = new Vector3(0f, 0f, -1.4f),   LocalScale = new Vector3(3f, 2.4f, 0.2f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Left",    LocalPosition = new Vector3(-1.4f, 0f, 0f),   LocalScale = new Vector3(0.2f, 2.4f, 3f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Right",   LocalPosition = new Vector3(1.4f, 0f, 0f),    LocalScale = new Vector3(0.2f, 2.4f, 3f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_FrontL",  LocalPosition = new Vector3(-0.9f, 0f, 1.4f), LocalScale = new Vector3(1.2f, 2.4f, 0.2f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_FrontR",  LocalPosition = new Vector3(0.9f, 0f, 1.4f),  LocalScale = new Vector3(1.2f, 2.4f, 0.2f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Roof",         LocalPosition = new Vector3(0f, 1.2f, 0f),    LocalScale = new Vector3(3.3f, 0.25f, 3.3f), MaterialType = "stone" }
            }),
        new BuildingDefinition("door", new Vector3(3f, 4f, 0.3f), new Color(0.55f, 0.35f, 0.16f), 3, 0,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Panel", LocalPosition = new Vector3(1.5f, 0f, 0f), LocalScale = new Vector3(3f, 4f, 0.3f), MaterialType = "wood" }
            }),
        new BuildingDefinition("wife_house", new Vector3(14f, 9f, 14f), ColorPalette.WifeHouseWood, 20, 10,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Floor_1F",    LocalPosition = new Vector3(0f, -2.25f, 0f),   LocalScale = new Vector3(14f, 0.5f, 14f),   MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_1F_Front", LocalPosition = new Vector3(0f, 0f, 6.75f),  LocalScale = new Vector3(14f, 5f, 0.5f),    MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_1F_Back",  LocalPosition = new Vector3(0f, 0f, -6.75f), LocalScale = new Vector3(14f, 5f, 0.5f),    MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_1F_Right", LocalPosition = new Vector3(6.75f, 0f, 0f),  LocalScale = new Vector3(0.5f, 5f, 14f),     MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Floor_2F",    LocalPosition = new Vector3(0f, 2.75f, 0f),   LocalScale = new Vector3(14f, 0.5f, 14f),   MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_2F_Front", LocalPosition = new Vector3(0f, 4.5f, 6.75f), LocalScale = new Vector3(14f, 4f, 0.5f),    MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_2F_Back",  LocalPosition = new Vector3(0f, 4.5f, -6.75f), LocalScale = new Vector3(14f, 4f, 0.5f),   MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_2F_Right", LocalPosition = new Vector3(6.75f, 4.5f, 0f), LocalScale = new Vector3(0.5f, 4f, 14f),     MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Ceiling",     LocalPosition = new Vector3(0f, 6.65f, 0f),   LocalScale = new Vector3(14f, 0.3f, 14f),   MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Left_Upper", LocalPosition = new Vector3(-6.75f, 4.5f, -4.25f), LocalScale = new Vector3(0.5f, 9f, 5.5f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Left_Lower", LocalPosition = new Vector3(-6.75f, 4.5f, 4.25f),  LocalScale = new Vector3(0.5f, 9f, 5.5f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Foundation",  LocalPosition = new Vector3(0f, -2.53f, 0f),   LocalScale = new Vector3(15.5f, 0.5f, 15.5f), MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Roof_Left",  LocalPosition = new Vector3(3.5f, 7.95f, 0f),  LocalScale = new Vector3(8.5f, 0.6f, 17.6f), MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Roof_Right", LocalPosition = new Vector3(-3.5f, 7.95f, 0f), LocalScale = new Vector3(8.5f, 0.6f, 17.6f), MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Ridge",      LocalPosition = new Vector3(0f, 9.15f, 0f),    LocalScale = new Vector3(0.7f, 0.35f, 18f),  MaterialType = "stone" }
            },
            woodColor: ColorPalette.WifeHouseWood,
            stoneColor: new Color(0.439f, 0.4f, 0.361f)),
        new BuildingDefinition("structure_house", new Vector3(16f, 6f, 10f), new Color(0.6f, 0.4f, 0.2f), 0, 0,
            subBuildings: new SubBuildingDefinition[]
            {
                new SubBuildingDefinition { PartName = "Foundation", Offset = new Vector3(0f, -2.5f, 0f), Size = new Vector3(16f, 0.5f, 10f), WoodCost = 0, StoneCost = 8, Color = new Color(0.4f, 0.4f, 0.4f) },
                new SubBuildingDefinition { PartName = "Floor", Offset = new Vector3(0f, -2.0f, 0f), Size = new Vector3(16f, 0.3f, 10f), WoodCost = 6, StoneCost = 0, Color = new Color(0.71f, 0.53f, 0.27f) },
                new SubBuildingDefinition { PartName = "Walls", Offset = new Vector3(0f, 0f, 0f), Size = new Vector3(16f, 5f, 10f), WoodCost = 12, StoneCost = 0, Color = ColorPalette.HouseWood },
                new SubBuildingDefinition { PartName = "Roof", Offset = new Vector3(0f, 2.7f, 0f), Size = new Vector3(17f, 0.4f, 11f), WoodCost = 0, StoneCost = 8, Color = new Color(0.5f, 0.5f, 0.5f) },
                new SubBuildingDefinition { PartName = "Door", Offset = new Vector3(0f, -0.5f, 5.15f), Size = new Vector3(3f, 4f, 0.3f), WoodCost = 3, StoneCost = 0, Color = new Color(0.55f, 0.35f, 0.16f) },
                new SubBuildingDefinition { PartName = "Interior", Offset = new Vector3(0f, -0.5f, 0f), Size = new Vector3(12f, 3f, 8f), WoodCost = 6, StoneCost = 0, Color = new Color(0.5f, 0.35f, 0.2f) }
            }),
        new BuildingDefinition("library", new Vector3(10f, 6f, 8f), new Color(0.6f, 0.42f, 0.25f), 12, 8,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Floor",        LocalPosition = new Vector3(0f, -2.85f, 0f),  LocalScale = new Vector3(10f, 0.3f, 8f),    MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Front",   LocalPosition = new Vector3(0f, 0f, 3.85f),   LocalScale = new Vector3(9.7f, 5.7f, 0.3f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Back",    LocalPosition = new Vector3(0f, 0f, -3.85f),  LocalScale = new Vector3(9.7f, 5.7f, 0.3f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Left",    LocalPosition = new Vector3(-4.85f, 0f, 0f),  LocalScale = new Vector3(0.3f, 5.7f, 7.7f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Right",   LocalPosition = new Vector3(4.85f, 0f, 0f),   LocalScale = new Vector3(0.3f, 5.7f, 7.7f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Roof",         LocalPosition = new Vector3(0f, 2.85f, 0f),   LocalScale = new Vector3(10.3f, 0.3f, 8.3f), MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Bookshelf_Back", LocalPosition = new Vector3(0f, 0.5f, -3.3f), LocalScale = new Vector3(6f, 3f, 0.3f),   MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Table",        LocalPosition = new Vector3(2f, -1.1f, 1.2f), LocalScale = new Vector3(2.2f, 0.9f, 1.2f), MaterialType = "wood" }
            }),
        new BuildingDefinition("well", new Vector3(3f, 4f, 3f), new Color(0.45f, 0.42f, 0.38f), 4, 6,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Base",  LocalPosition = new Vector3(0f, -1.4f, 0f),  LocalScale = new Vector3(3f, 0.4f, 3f),     MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Ring",  LocalPosition = new Vector3(0f, -0.6f, 0f),  LocalScale = new Vector3(3f, 1.2f, 3f),     MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "PostL", LocalPosition = new Vector3(-1.2f, 0.4f, 0f), LocalScale = new Vector3(0.2f, 2.4f, 0.2f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "PostR", LocalPosition = new Vector3(1.2f, 0.4f, 0f),  LocalScale = new Vector3(0.2f, 2.4f, 0.2f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Roof",  LocalPosition = new Vector3(0f, 1.8f, 0f),    LocalScale = new Vector3(3.4f, 0.2f, 3.4f), MaterialType = "stone" }
            }),
        new BuildingDefinition("fountain", new Vector3(5f, 3f, 5f), new Color(0.55f, 0.55f, 0.6f), 0, 10,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Basin",  LocalPosition = new Vector3(0f, -1f, 0f),   LocalScale = new Vector3(5f, 0.4f, 5f),    MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Wall",   LocalPosition = new Vector3(0f, -0.5f, 0f), LocalScale = new Vector3(5f, 0.7f, 5f),    MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Pillar", LocalPosition = new Vector3(0f, 0f, 0f),    LocalScale = new Vector3(0.6f, 1.6f, 0.6f), MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Water",  LocalPosition = new Vector3(0f, 0.5f, 0f),  LocalScale = new Vector3(2f, 0.4f, 2f),    MaterialType = "stone" }
            }),
        new BuildingDefinition("workshop", new Vector3(10f, 5f, 8f), new Color(0.55f, 0.4f, 0.22f), 10, 6,
            new BuildingPartDefinition[]
            {
                new BuildingPartDefinition { PartName = "Floor",     LocalPosition = new Vector3(0f, -2.35f, 0f),  LocalScale = new Vector3(10f, 0.3f, 8f),    MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Front", LocalPosition = new Vector3(0f, 0f, 3.85f),  LocalScale = new Vector3(9.7f, 4.7f, 0.3f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Back",  LocalPosition = new Vector3(0f, 0f, -3.85f), LocalScale = new Vector3(9.7f, 4.7f, 0.3f), MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Left",  LocalPosition = new Vector3(-4.85f, 0f, 0f), LocalScale = new Vector3(0.3f, 4.7f, 7.7f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Wall_Right", LocalPosition = new Vector3(4.85f, 0f, 0f),  LocalScale = new Vector3(0.3f, 4.7f, 7.7f),  MaterialType = "wood" },
                new BuildingPartDefinition { PartName = "Roof",       LocalPosition = new Vector3(0f, 2.35f, 0f),   LocalScale = new Vector3(10.3f, 0.3f, 8.3f), MaterialType = "stone" },
                new BuildingPartDefinition { PartName = "Anvil",      LocalPosition = new Vector3(-2f, -1.6f, 1f),  LocalScale = new Vector3(1f, 0.6f, 0.6f),   MaterialType = "stone" }
            })
    };

    // Blueprints that must be researched at the Library before they can be placed.
    private static readonly Dictionary<string, int> ResearchCosts = new Dictionary<string, int>
    {
        { "stone_wall", 60 },
        { "fence", 40 },
        { "watchtower", 120 },
        { "small_house", 100 },
        { "stone_floor", 60 },
        { "stair", 80 },
        { "table", 40 },
        { "chair", 40 },
        { "sofa", 80 },
        { "goblin_hut", 120 },
        { "door", 60 },
        { "wife_house", 150 },
        { "structure_house", 150 },
        { "library", 150 },
        { "well", 100 },
        { "fountain", 100 },
        { "workshop", 200 }
    };

    private readonly HashSet<string> _unlockedBlueprints = new HashSet<string>();

    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    //  MANSION MEGA STRUCTURE DEFINITION
    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    private static readonly SubBuildingDefinition[] _mansionSubBuildings = new SubBuildingDefinition[]
    {
        new SubBuildingDefinition { PartName = "Mansion", Offset = Vector3.zero, Size = new Vector3(24f, 9f, 17f), WoodCost = 1250, StoneCost = 1000, Color = new Color(0.6f, 0.45f, 0.25f) },
    };

    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    //  PAGODA MEGA STRUCTURE DEFINITION
    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    private static readonly SubBuildingDefinition[] _pagodaSubBuildings = new SubBuildingDefinition[]
    {
        // === FOUNDATION (stone: 60) ===
        new SubBuildingDefinition { PartName = "Pagoda_Foundation",       Offset = new Vector3(0f, 0.2f, 0f),     Size = new Vector3(14f, 0.5f, 14f),  WoodCost = 0,   StoneCost = 60, Color = new Color(0.42f, 0.42f, 0.42f) },

        // === BASE TIER (wood: 70) ===
        new SubBuildingDefinition { PartName = "Pagoda_BaseFloor",        Offset = new Vector3(0f, 0.75f, 0f),    Size = new Vector3(12f, 0.3f, 12f),  WoodCost = 25,  StoneCost = 0,  Color = new Color(0.6f, 0.28f, 0.14f) },
        new SubBuildingDefinition { PartName = "Pagoda_BaseWalls",        Offset = new Vector3(0f, 2.55f, 0f),    Size = new Vector3(11f, 3.3f, 11f),  WoodCost = 45,  StoneCost = 0,  Color = new Color(0.55f, 0.12f, 0.1f) },
        new SubBuildingDefinition { PartName = "Pagoda_Roof1",            Offset = new Vector3(0f, 5f, 0f),       Size = new Vector3(17.2f, 0.5f, 17.2f), WoodCost = 0, StoneCost = 40, Color = new Color(0.55f, 0.16f, 0.12f) },

        // === MID TIER (wood: 55) ===
        new SubBuildingDefinition { PartName = "Pagoda_MidFloor",         Offset = new Vector3(0f, 6.8f, 0f),     Size = new Vector3(8f, 0.3f, 8f),    WoodCost = 20,  StoneCost = 0,  Color = new Color(0.6f, 0.28f, 0.14f) },
        new SubBuildingDefinition { PartName = "Pagoda_MidWalls",         Offset = new Vector3(0f, 8.15f, 0f),    Size = new Vector3(7.2f, 2.2f, 7.2f), WoodCost = 35, StoneCost = 0,  Color = new Color(0.55f, 0.12f, 0.1f) },
        new SubBuildingDefinition { PartName = "Pagoda_Roof2",            Offset = new Vector3(0f, 10f, 0f),      Size = new Vector3(12f, 0.5f, 12f),  WoodCost = 0,   StoneCost = 35, Color = new Color(0.55f, 0.16f, 0.12f) },

        // === TOP TIER (wood: 40) ===
        new SubBuildingDefinition { PartName = "Pagoda_TopFloor",         Offset = new Vector3(0f, 11.3f, 0f),    Size = new Vector3(5.5f, 0.3f, 5.5f), WoodCost = 15, StoneCost = 0,  Color = new Color(0.6f, 0.28f, 0.14f) },
        new SubBuildingDefinition { PartName = "Pagoda_TopWalls",         Offset = new Vector3(0f, 12.4f, 0f),    Size = new Vector3(4.9f, 1.7f, 4.9f), WoodCost = 25, StoneCost = 0,  Color = new Color(0.55f, 0.12f, 0.1f) },
        new SubBuildingDefinition { PartName = "Pagoda_Roof3",            Offset = new Vector3(0f, 14f, 0f),      Size = new Vector3(8.8f, 0.45f, 8.8f), WoodCost = 0, StoneCost = 30, Color = new Color(0.55f, 0.16f, 0.12f) },

        // === 4TH TIER (wood: 25) ===
        new SubBuildingDefinition { PartName = "Pagoda_Tier4Floor",       Offset = new Vector3(0f, 14.9f, 0f),    Size = new Vector3(3.4f, 0.3f, 3.4f), WoodCost = 10, StoneCost = 0,  Color = new Color(0.6f, 0.28f, 0.14f) },
        new SubBuildingDefinition { PartName = "Pagoda_Tier4Walls",       Offset = new Vector3(0f, 15.9f, 0f),    Size = new Vector3(3.1f, 1.3f, 3.1f), WoodCost = 15, StoneCost = 0,  Color = new Color(0.55f, 0.12f, 0.1f) },
        new SubBuildingDefinition { PartName = "Pagoda_Roof4",            Offset = new Vector3(0f, 17.2f, 0f),    Size = new Vector3(5.6f, 0.4f, 5.6f), WoodCost = 0,   StoneCost = 25, Color = new Color(0.55f, 0.16f, 0.12f) },

        // === SPIRE (wood: 5) ===
        new SubBuildingDefinition { PartName = "Pagoda_Spire",            Offset = new Vector3(0f, 18.3f, 0f),    Size = new Vector3(0.3f, 2.6f, 0.3f), WoodCost = 5, StoneCost = 0,  Color = new Color(1f, 0.84f, 0.2f) },
    };

    private const int _mansionTotalParts = 1;
    private const string _mansionQuestTarget = "mansion";
    private const string _immigrantQuestTarget = "immigrant_house";
    private const int _immigrantHouseWoodCost = 10;
    private const int _immigrantHouseStoneCost = 6;
    private static readonly Vector3 MansionBasePos = new Vector3(-8f, 0f, -30f);

    private List<Vector3> _immigrantHousePositions;
    private bool[] _immigrantBuilt;
    private int _nextImmigrantIndex;
    private List<GameObject> _immigrantPlotMarkers;
    private List<VillagerSaveData> _savedVillagers;
    public bool IsImmigrantVillagePlaced { get; private set; }
    public int ImmigrantHousesBuilt { get; private set; }
    public int MaxImmigrantHouses => _immigrantHousePositions != null ? _immigrantHousePositions.Count : 0;
    public bool AllImmigrantHousesBuilt => _immigrantHousePositions != null && _nextImmigrantIndex >= _immigrantHousePositions.Count;

    private void GenerateImmigrantPositions()
    {
        _immigrantHousePositions = new List<Vector3>();
        for (float x = -100f; x <= 0f; x += 15f)
            _immigrantHousePositions.Add(new Vector3(x, 0f, -70f));
        for (float x = 25f; x <= 140f; x += 15f)
            _immigrantHousePositions.Add(new Vector3(x, 0f, 200f));
        for (float x = -100f; x <= -25f; x += 15f)
            _immigrantHousePositions.Add(new Vector3(x, 0f, -30f));
        foreach (float x in new float[] { 45f, 60f, 75f, 90f, 105f, 120f, 135f, 150f, 180f, 210f, 240f })
            _immigrantHousePositions.Add(new Vector3(x, 0f, 70f));
foreach (float x in new float[] { 255f, 270f, 285f, 90f, 105f, 120f, 135f, 150f, 180f, 210f, 240f })
            _immigrantHousePositions.Add(new Vector3(x, 0f, 110f));
        foreach (float x in new float[] { 25f, 40f, 80f, 100f, 115f, 130f })
            _immigrantHousePositions.Add(new Vector3(x, 0f, 160f));
_immigrantBuilt = new bool[_immigrantHousePositions.Count];
    }
    public void HideImmigrantMarker(int index)
    {
        if (_immigrantPlotMarkers == null || index < 0 || index >= _immigrantPlotMarkers.Count) return;
        var m = _immigrantPlotMarkers[index];
        if (m != null) m.SetActive(false);
    }
    private int _currentBuildingIndex;
    private int _currentRotation;
    private readonly HashSet<Vector3Int> _floorPositions = new HashSet<Vector3Int>();

    private void Start()
    {
        GroundSize = new Vector3(600f, 0.2f, 600f);
        MapWidth = 40;
        MapDepth = 40;
        TreeCount = 150;

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

GenerateImmigrantPositions();

        if (!CreateTerrainGrid())
            CreateGround();

CreateSkyAndLight();
        BuildRoad();
        PlaceStreetLights();
        BuildRockyBorder();
        LoadTreeTextures();
        SpawnTrees(TreeCount);
        SpawnRocks(RockCount);
        _treeNameCounter = TreeCount;
        _rockNameCounter = RockCount;
        BuildHouse();
        SpawnBuildingDirect("goblin_hut", new Vector3(0f, 0f, 16f), 0);
        BuildBeach();
        BuildShop();
        BuildRestaurant();
        BuildCafe();
        BuildLibrary();
        BuildNightClub();
        MapBuilder.BuildConvenienceStore(_worldRoot.transform, new Vector3(24f, 0f, 60f), 1f, Quaternion.Euler(0f, 180f, 0f));
        BuildWifeHouse();
        BuildRichManMansion();
        BuildFishingShop();
        BuildPolicePost();
SpawnBuffalo();
        SpawnMobs();
        InitializeBuildingPreview();
BuildPagoda(PagodaBasePos);
        var monk = MapBuilder.BuildMonkNpc(_worldRoot.transform, new Vector3(24f, 1.815f, 27f), Quaternion.Euler(0f, -90f, 0f));
        monk.AddComponent<PagodaMonkNPC>();
        BuildBossArena();
        PruneTreesAndRocksNearStructures();

        SpawnInitialClouds();

var spawnerGo = new GameObject("LivestockSpawner");
        spawnerGo.transform.SetParent(_worldRoot.transform);
        spawnerGo.AddComponent<LivestockSpawner>();

SittableSeat.Register(_worldRoot.transform);
        NavGrid.EnsureCreated();
    }
    private void BuildBossArena()
    {
        Color stoneC = new Color(0.42f, 0.4f, 0.38f);
        Color stoneDark = new Color(0.3f, 0.28f, 0.27f);
        Color runeGlow = new Color(1f, 0.4f, 0.08f);
        Color boneC = new Color(0.88f, 0.84f, 0.78f);

        Vector3 center = _bossArenaCenter;
        float radius = 8f;
        int ringSegments = 28;

        // Platform fill (flat, no collider)
        MakeBlock("BossArenaFloor", _worldRoot.transform,
            new Vector3(radius * 2f, 0.06f, radius * 2f),
            center + new Vector3(0f, 0.02f, 0f), stoneDark, true);

        // Outer stone ring
        for (int i = 0; i < ringSegments; i++)
        {
            float a = (i / (float)ringSegments) * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(a) * radius, 0.14f, Mathf.Sin(a) * radius);
            MakeBlock("BossArenaRing", _worldRoot.transform,
                new Vector3(1.4f, 0.28f, 1.4f), pos, stoneC, true);
        }

        // Pillars
        int pillars = 8;
        for (int i = 0; i < pillars; i++)
        {
            float a = (i / (float)pillars) * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(a) * (radius + 1.6f), 1.2f, Mathf.Sin(a) * (radius + 1.6f));
            MakeBlock("BossArenaPillar", _worldRoot.transform,
                new Vector3(0.8f, 2.4f, 0.8f), pos, stoneC, true);
            MakeBlock("BossArenaPillarCap", _worldRoot.transform,
                new Vector3(1.0f, 0.2f, 1.0f), pos + new Vector3(0f, 1.3f, 0f), stoneDark, true);
        }

        // Glowing rune ring on the floor
        int runes = 12;
        for (int i = 0; i < runes; i++)
        {
            float a = (i / (float)runes) * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(a) * 4.5f, 0.05f, Mathf.Sin(a) * 4.5f);
            MakeBlock("BossRune", _worldRoot.transform,
                new Vector3(0.7f, 0.04f, 0.25f), pos, runeGlow, true);
        }

        // Central demon sigil (cross of embers)
        MakeBlock("BossSigilH", _worldRoot.transform, new Vector3(3.6f, 0.05f, 0.6f), center + new Vector3(0f, 0.05f, 0f), runeGlow, true);
        MakeBlock("BossSigilV", _worldRoot.transform, new Vector3(0.6f, 0.05f, 3.6f), center + new Vector3(0f, 0.05f, 0f), runeGlow, true);

        // Bone/skull decorations on the ring
        for (int i = 0; i < 6; i++)
        {
            float a = (i / 6f) * Mathf.PI * 2f + 0.35f;
            Vector3 pos = center + new Vector3(Mathf.Cos(a) * (radius + 0.3f), 0.36f, Mathf.Sin(a) * (radius + 0.3f));
            MakeBlock("BossBonePile", _worldRoot.transform,
                new Vector3(0.5f, 0.24f, 0.35f), pos, boneC, true);
        }
    }
    public GameObject SpawnQuestBoss()
    {
        if (_questBoss != null && _questBoss.activeInHierarchy)
            return _questBoss;

        var go = new GameObject("DemonKing");
        go.transform.SetParent(_worldRoot.transform);
        go.transform.position = _bossArenaCenter + new Vector3(0f, 0.5f, 0f);
        go.transform.localScale = Vector3.one;

        go.AddComponent<Rigidbody>().isKinematic = true;

        var boss = go.AddComponent<EnemyController>();
        boss.MaxHealth = 150;
        boss.Damage = 12;
        boss.MoveSpeed = 1.8f;
        boss.ChaseRange = 18f;
        boss.AttackRange = 2.5f;
        boss.AttackCooldown = 1f;
        boss.IsBoss = true;
        boss.IsGiant = false;

        _questBoss = go;
        SoundManager.Instance?.Play("bonk", 0.6f);
        return go;
    }
    private void SpawnInitialClouds()
    {
        int half = Mathf.FloorToInt(GroundSize.x * 0.5f) - 20;
        for (int i = 0; i < 8; i++)
        {
            Vector3 pos = new Vector3(
                UnityEngine.Random.Range(-half, half + 1),
                UnityEngine.Random.Range(60f, 80f),
                UnityEngine.Random.Range(-half, half + 1));
            float scale = UnityEngine.Random.Range(1.5f, 3f);
            var cloud = MapBuilder.BuildCloud(_worldRoot.transform, pos, scale);
            cloud.AddComponent<CloudBehavior>();
            _clouds.Add(cloud);
        }
    }

    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    //  ALIGNMENT STRIP + WORLD VISIBILITY
    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

    public void CreateAlignmentStrip()
    {
        if (_alignmentStrip != null) return;

        _alignmentStrip = new GameObject("AlignmentStrip");

        float roadCx = 14f;
        float roadHw = 3.8f;
        float stripLen = 140f;
        float stripCenterZ = -300f - stripLen / 2f;

        // Road extension south of map edge
        Color asphaltC = new Color(0.235f, 0.243f, 0.275f);
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "RoadExtension";
        road.transform.SetParent(_alignmentStrip.transform);
        road.transform.localScale = new Vector3(roadHw * 2f, 0.06f, stripLen);
        road.transform.localPosition = new Vector3(roadCx, 0.03f, stripCenterZ);
        road.GetComponent<Renderer>().material.color = asphaltC;

        // Curbs
        Color curbC = new Color(0.46f, 0.45f, 0.42f);
        foreach (int side in new[] { -1, 1 })
        {
            var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "KerbExtension";
            curb.transform.SetParent(_alignmentStrip.transform);
            curb.transform.localScale = new Vector3(0.55f, 0.22f, stripLen);
            curb.transform.localPosition = new Vector3(roadCx + side * (roadHw + 0.35f), 0.11f, stripCenterZ);
            curb.GetComponent<Renderer>().material.color = curbC;
            Destroy(curb.GetComponent<Collider>());
        }

        // White edge lines
        Color whiteC = Color.white;
        foreach (int side in new[] { -1, 1 })
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "EdgeLineExtension";
            line.transform.SetParent(_alignmentStrip.transform);
            line.transform.localScale = new Vector3(0.18f, 0.03f, stripLen);
            line.transform.localPosition = new Vector3(roadCx + side * (roadHw - 0.22f), 0.03f, stripCenterZ);
            line.GetComponent<Renderer>().material.color = whiteC;
            Destroy(line.GetComponent<Collider>());
        }

        // Ground left of road (grass)
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        var grassMat = new Material(urpShader != null ? urpShader : Shader.Find("Standard"));
        var tex = Resources.Load<Texture2D>("texture/grass_blade");
        if (tex != null)
        {
            grassMat.mainTexture = tex;
            grassMat.mainTextureScale = new Vector2(4f, stripLen / 5f);
        }
        else
        {
            grassMat.color = ColorPalette.GrassGreen;
        }

        float groundLeftX = -300f;
        float groundWidth = (roadCx - roadHw) - groundLeftX;
        var groundL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundL.name = "GroundExtensionL";
        groundL.transform.SetParent(_alignmentStrip.transform);
        groundL.transform.localScale = new Vector3(groundWidth, 0.05f, stripLen);
        groundL.transform.localPosition = new Vector3((groundLeftX + roadCx - roadHw) / 2f, 0.0f, stripCenterZ);
        groundL.GetComponent<Renderer>().material = grassMat;

        // Ground right of road (grass)
        float groundRightX = 400f;
        float groundWidthR = groundRightX - (roadCx + roadHw);
        var groundR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundR.name = "GroundExtensionR";
        groundR.transform.SetParent(_alignmentStrip.transform);
        groundR.transform.localScale = new Vector3(groundWidthR, 0.05f, stripLen);
        groundR.transform.localPosition = new Vector3((roadCx + roadHw + groundRightX) / 2f, 0.0f, stripCenterZ);
        groundR.GetComponent<Renderer>().material = grassMat;

        // Ground behind road
        float behindWidth = groundRightX - groundLeftX;
        var groundB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundB.name = "GroundExtensionB";
        groundB.transform.SetParent(_alignmentStrip.transform);
        groundB.transform.localScale = new Vector3(behindWidth, 0.05f, stripLen);
        groundB.transform.localPosition = new Vector3(0f, -0.01f, stripCenterZ);
        groundB.GetComponent<Renderer>().material.color = ColorPalette.GrassGreen;
        Destroy(groundB.GetComponent<Collider>());

        Destroy(road.GetComponent<Collider>());
    }
    public void HideWorld()
    {
        if (_worldRoot != null)
            _worldRoot.SetActive(false);
    }
    public void ShowWorld()
    {
        if (_worldRoot != null)
            _worldRoot.SetActive(true);
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
                {
                    ps.GhostEntity = null;
                    ps.GhostLabel = null;
                }
            }
        }
        _buildings.Clear();
        _floorPositions.Clear();

        foreach (var bp in _blueprints)
        {
            DestroyBlueprintLabel(bp);
            if (bp.Entity != null) Destroy(bp.Entity);
        }
        _blueprints.Clear();

        if (_buildingPreview != null)
            Destroy(_buildingPreview);

_streetLights.Clear();
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
        _worldFrameTick++;
        _resourceRespawnTimer += deltaTime;
        if (_resourceRespawnTimer >= RespawnInterval)
        {
            _resourceRespawnTimer -= RespawnInterval;
            RespawnResources();
        }

        _cloudSpawnTimer += deltaTime;
        if ((_worldFrameTick & 15) == 0)
            _clouds.RemoveAll(c => c == null);
        if (_cloudSpawnTimer >= CloudSpawnInterval && _clouds.Count < MaxClouds)
        {
            _cloudSpawnTimer = 0f;
            Vector3 playerPos = GameManager.Instance?.Player?.transform.position ?? Vector3.zero;
            Vector3 cloudPos = playerPos + new Vector3(
                UnityEngine.Random.Range(-40f, 40f),
                UnityEngine.Random.Range(60f, 80f),
                UnityEngine.Random.Range(-40f, 40f));
            float scale = UnityEngine.Random.Range(1f, 3f);
            var cloud = MapBuilder.BuildCloud(_worldRoot.transform, cloudPos, scale);
            cloud.AddComponent<CloudBehavior>();
            _clouds.Add(cloud);
        }

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
bool isNight = hour >= 18f || hour < 6f;
        if (isNight && !_wasNight)
        {
            _wasNight = true;
            CloseAllDoors();
            SetStreetLights(true);
        }
        else if (!isNight && _wasNight)
        {
            _wasNight = false;
            SetStreetLights(false);
        }

        if (SunLight == null)
            return;

        float t = hour / 24f;
        float elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f) * 80f;
        float sunY = Mathf.Lerp(-180f, 180f, t);
        SunLight.transform.rotation = Quaternion.Euler(elevation, sunY, 0f);

        Color sunColor;
        float sunIntensity;
        Color ambient;
        float ambientIntensity;
        bool fog;
        Color fogColor = default;
        float fogDensity = 0f;

        if (hour >= 6f && hour < 17f)
        {
            sunIntensity = 2f;
            sunColor = new Color(1f, 0.925f, 0.77f);
            ambient = new Color(0.5f, 0.7f, 1f);
            ambientIntensity = 0.8f;
            fog = false;
        }
        else
        {
            float dayFactor = Mathf.Clamp01((elevation + 10f) / 90f);
            sunIntensity = Mathf.Lerp(0.05f, 2f, dayFactor);

            float warmFactor = 0f;
            if (hour >= 5f && hour < 6f)
                warmFactor = Mathf.InverseLerp(5f, 6f, hour);
            else if (hour >= 17f && hour < 18f)
                warmFactor = 1f - Mathf.InverseLerp(17f, 18f, hour);
            Color baseSunColor = Color.Lerp(
                new Color(1f, 0.925f, 0.77f),
                new Color(1f, 0.5f, 0.15f),
                warmFactor);
            if (elevation < -5f)
            {
                float nightFactor = Mathf.InverseLerp(-5f, -30f, elevation);
                baseSunColor = Color.Lerp(baseSunColor, new Color(0.1f, 0.1f, 0.3f), nightFactor);
            }
            sunColor = baseSunColor;

            Color skyColor;
            if (elevation > 15f)
            {
                skyColor = new Color(0.5f, 0.7f, 1f);
            }
            else if (elevation > -5f)
            {
                float sunriseT = Mathf.InverseLerp(-5f, 15f, elevation);
                skyColor = Color.Lerp(new Color(0.8f, 0.3f, 0.1f), new Color(0.5f, 0.7f, 1f), sunriseT);
            }
            else
            {
                float nightT = Mathf.InverseLerp(-5f, -30f, elevation);
                skyColor = Color.Lerp(new Color(0.09f, 0.09f, 0.15f), new Color(0.06f, 0.06f, 0.12f), nightT);
            }

            ambient = skyColor;
            ambientIntensity = Mathf.Lerp(0.3f, 0.8f, dayFactor);

            float fogFactor = 1f - Mathf.Abs(elevation - 10f) / 25f;
            fogFactor = Mathf.Clamp01(fogFactor);
            if (fogFactor > 0.01f)
            {
                fog = true;
                fogColor = Color.Lerp(skyColor, new Color(1f, 0.6f, 0.3f), elevation > 0f ? 0.3f : 0.5f);
                fogDensity = fogFactor * 0.015f;
            }
            else
            {
                fog = false;
            }
        }

        if (SunLight.color != sunColor)
            SunLight.color = sunColor;
        if (SunLight.intensity != sunIntensity)
            SunLight.intensity = sunIntensity;
        if (RenderSettings.ambientLight != ambient)
            RenderSettings.ambientLight = ambient;
        if (RenderSettings.ambientIntensity != ambientIntensity)
            RenderSettings.ambientIntensity = ambientIntensity;
        if (RenderSettings.fog != fog)
            RenderSettings.fog = fog;
        if (fog)
        {
            if (RenderSettings.fogColor != fogColor)
                RenderSettings.fogColor = fogColor;
            if (RenderSettings.fogDensity != fogDensity)
                RenderSettings.fogDensity = fogDensity;
        }
    }
    private void SetStreetLights(bool on)
    {
        for (int i = _streetLights.Count - 1; i >= 0; i--)
        {
            var l = _streetLights[i];
            if (l == null)
            {
                _streetLights.RemoveAt(i);
                continue;
            }
            l.enabled = on;
        }
    }
    public bool IsOnRoad(Vector3 position)
    {
        if (RoadObject == null)
            return false;

        bool onNS = position.x >= (_roadCenterX - _roadHalfWidth - 0.5f) && position.x <= (_roadCenterX + _roadHalfWidth + 0.5f)
               && position.z >= _roadZStart && position.z <= _roadZEnd;
        bool onEW = position.x >= (_roadCenterX - 0.5f) && position.x <= (_roadXEnd + 0.5f)
               && position.z >= (_roadTurnZ - _roadHalfWidth - 0.5f) && position.z <= (_roadTurnZ + _roadHalfWidth + 0.5f);
        bool onSouthBranch = position.x >= -120.5f && position.x <= (_roadCenterX + 0.5f)
               && position.z >= (-50f - _roadHalfWidth - 0.5f) && position.z <= (-50f + _roadHalfWidth + 0.5f);
        bool onNorthBranch = position.x >= (_roadCenterX - 0.5f) && position.x <= 150.5f
               && position.z >= (180f - _roadHalfWidth - 0.5f) && position.z <= (180f + _roadHalfWidth + 0.5f);
        return onNS || onEW || onSouthBranch || onNorthBranch;
    }
    public float GetRoadSurfaceY()
    {
        if (RoadObject == null)
            return 0.06f;
        Vector3 size = RoadObject.transform.localScale;
        return RoadObject.transform.position.y + size.y * 0.5f;
    }
public GameObject SpawnPickup(string toolType, Vector3 position)
    {
        return CreateToolPickup(toolType, position);
    }
    public GameObject ThrowPickup(string toolType, Vector3 position, Vector3 velocity)
    {
        var pickup = CreateToolPickup(toolType, position);

        var triggerCol = pickup.GetComponent<BoxCollider>();
        if (triggerCol != null)
            triggerCol.isTrigger = false;

        var rb = pickup.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Random.insideUnitSphere * 2f;

        pickup.AddComponent<ThrownItem>();

        return pickup;
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
    public GameObject ThrowCage(string cageType, Vector3 position, Vector3 velocity, Livestock.AnimalType? capturedAnimal = null)
    {
        var pickup = new GameObject("ThrownCage");
        pickup.transform.SetParent(_worldRoot.transform);
        pickup.transform.position = position;

        if (capturedAnimal.HasValue)
        {
            var root = new GameObject("CageModel");
            root.transform.SetParent(pickup.transform, false);

            bool isBig = cageType == "cage_big";
            float w = isBig ? 0.5f : 0.35f;
            float h = isBig ? 0.4f : 0.3f;
            float d = isBig ? 0.4f : 0.3f;

            ItemBuilder.BuildDetailedCage(root.transform, w, h, d);
            Livestock.BuildModelInto(root.transform, capturedAnimal.Value);
        }
        else
        {
            ItemBuilder.BuildItem(pickup.transform, cageType);
        }

        var rootCollider = pickup.AddComponent<BoxCollider>();
        rootCollider.isTrigger = false;
        rootCollider.size = new Vector3(0.6f, 0.6f, 0.6f);

        var rb = pickup.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Random.insideUnitSphere * 2f;

        var thrown = pickup.AddComponent<ThrownCageProjectile>();
        thrown.CageType = cageType;
        var cageInfo = pickup.AddComponent<ThrownCageInfo>();
        cageInfo.CageType = cageType;
        if (capturedAnimal.HasValue)
        {
            thrown.HasCapturedAnimal = true;
            thrown.CapturedAnimal = capturedAnimal.Value;
        }

        return pickup;
    }
    public GameObject SpawnCageWithAnimal(Vector3 position, Livestock.AnimalType animalType)
    {
        var cage = new GameObject("CageWithAnimal");
        cage.transform.SetParent(_worldRoot.transform);
        cage.transform.position = position + Vector3.up * 0.3f;

        bool isBig = animalType == Livestock.AnimalType.Cow ||
                     animalType == Livestock.AnimalType.Pig ||
                     animalType == Livestock.AnimalType.Sheep ||
                     animalType == Livestock.AnimalType.Goat;

        float w = isBig ? 1.6f : 1.0f;
        float h = isBig ? 1.2f : 0.8f;
        float d = isBig ? 1.3f : 0.8f;

        var root = new GameObject("CageModel");
        root.transform.SetParent(cage.transform, false);

        ItemBuilder.BuildDetailedCage(root.transform, w, h, d);
        Livestock.BuildModelInto(root.transform, animalType);

        var rb = cage.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = true;

        var col = cage.AddComponent<BoxCollider>();
        col.size = new Vector3(w + 0.2f, h + 0.2f, d + 0.2f);

        var info = cage.AddComponent<CageWithAnimalInfo>();
        info.AnimalType = animalType;

        return cage;
    }
    private Color GetAnimalColor(Livestock.AnimalType type)
    {
        switch (type)
        {
            case Livestock.AnimalType.Cow: return new Color(0.95f, 0.95f, 0.95f);
            case Livestock.AnimalType.Pig: return new Color(0.95f, 0.65f, 0.6f);
            case Livestock.AnimalType.Sheep: return new Color(0.95f, 0.93f, 0.88f);
            case Livestock.AnimalType.Goat: return new Color(0.6f, 0.45f, 0.3f);
            case Livestock.AnimalType.Chicken: return new Color(0.95f, 0.93f, 0.88f);
            case Livestock.AnimalType.Duck: return new Color(0.92f, 0.9f, 0.85f);
            case Livestock.AnimalType.Turkey: return new Color(0.5f, 0.3f, 0.15f);
            default: return Color.gray;
        }
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
    private void UpdateFieldVisual(FieldState field)
    {
        if (field == null || field.FieldObject == null)
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

        if (field.Tilled)
        {
            if (_cachedDirtTex == null)
                _cachedDirtTex = Resources.Load<Texture2D>("texture/dirt_texture");
            if (_cachedDirtTex != null)
            {
                renderer.material.mainTexture = _cachedDirtTex;
            }
            else
            {
                renderer.material.color = new Color(0.45f, 0.28f, 0.12f);
            }
        }
        else
        {
            renderer.material.color = new Color(0.6f, 0.4f, 0.2f);
        }
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
    public void RefreshFieldVisual(FieldState field)
    {
        if (field == null)
            return;
        UpdateCropVisual(field);
        UpdateFieldVisual(field);
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
        public Transform GhostLabel;
    }
    public class BuildingPartDebrisInfo
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public Color PartColor;
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
        public string StructureId;
        public bool IsStructureParent;
        public bool IsMansion;
        public bool IsImmigrantHouse;
        public int ImmigrantHouseIndex = -1;
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

    [System.Serializable]
    public class SubBuildingDefinition
    {
        public string PartName;
        public Vector3 Offset;
        public Vector3 Size;
        public int WoodCost;
        public int StoneCost;
        public Color Color;
        public BuildingPartDefinition[] Parts;
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
        public SubBuildingDefinition[] SubBuildings;

        public BuildingDefinition(string name, Vector3 size, Color color, int woodCost, int stoneCost,
            BuildingPartDefinition[] parts = null, Color? woodColor = null, Color? stoneColor = null,
            SubBuildingDefinition[] subBuildings = null)
        {
            Name = name;
            Size = size;
            Color = color;
            WoodCost = woodCost;
            StoneCost = stoneCost;
            Parts = parts;
            WoodColor = woodColor ?? ColorPalette.HouseWood;
            StoneColor = stoneColor ?? new Color(0.41f, 0.41f, 0.41f);
            SubBuildings = subBuildings;
        }
    }
}

public class BlueprintAutoDeposit : MonoBehaviour
{
    private static readonly System.Collections.Generic.HashSet<GameObject> _consumedRoots = new System.Collections.Generic.HashSet<GameObject>();

    public static void ClearConsumedRoots() { _consumedRoots.Clear(); }

    private void OnTriggerEnter(Collider other)
    {
        var root = other.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;

        if (_consumedRoots.Contains(root))
            return;

        if (root.name != "TreeFelled" && root.name != "BranchTop" && root.name != "RockDebris")
            return;

        var wb = WorldBuilder.Instance;
        if (wb == null) return;

        var bp = wb.FindBlueprint(gameObject);
        if (bp == null) return;

        var info = wb.GetResourceAmount(root);
        if (info.material == null || info.amount < 0.05f) return;

        _consumedRoots.Add(root);
        wb.DepositMaterial(bp, info.material, info.amount);
        Destroy(root);
    }
}

public class ThrownItem : MonoBehaviour
{
    private float _spawnTime;
    private Rigidbody _rb;

    private void FixedUpdate()
    {
        if (_rb == null) return;
        if (Time.time - _spawnTime < 0.3f) return;

        if (_rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            Land();
        }
    }
    private void Land()
    {
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        var cols = GetComponents<Collider>();
        foreach (var c in cols)
        {
            if (c is BoxCollider bc)
            {
                bc.isTrigger = true;
                bc.size = new Vector3(0.6f, 0.6f, 0.6f);
            }
        }

        var pos = transform.position;
        pos.y = Mathf.Max(pos.y, 0.3f);
        transform.position = pos;

        transform.rotation = Quaternion.identity;

        Destroy(this);
        Destroy(gameObject, 60f);
    }
}

public class ThrownCageProjectile : MonoBehaviour
{
    public string CageType;
    public Livestock.AnimalType CapturedAnimal;
    public bool HasCapturedAnimal;
    private float _spawnTime;
    private Rigidbody _rb;
    private bool _landed;
    private static readonly Collider[] _captureBuffer = new Collider[32];

    private void FixedUpdate()
    {
        if (_rb == null || _landed) return;
        if (Time.time - _spawnTime < 0.3f) return;

        if (_rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            if (Land()) return;
            if (HasCapturedAnimal)
            {
                ReleaseAnimal();
            }
            else
            {
                CheckForCapture();
            }
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (_landed) return;
        if (Time.time - _spawnTime < 0.3f) return;

        if (Land()) return;

        if (HasCapturedAnimal)
        {
            ReleaseAnimal();
            return;
        }

        var livestock = collision.gameObject.GetComponentInParent<Livestock>();
        if (livestock == null)
            livestock = collision.gameObject.GetComponent<Livestock>();

        if (livestock != null && livestock.IsKnockedOut)
        {
            TryCapture(livestock);
        }
    }
    private void CheckForCapture()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, 1f, _captureBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var col = _captureBuffer[i];
            var livestock = col.GetComponentInParent<Livestock>();
            if (livestock == null) livestock = col.GetComponent<Livestock>();
            if (livestock != null && livestock.IsKnockedOut)
            {
                TryCapture(livestock);
                return;
            }
        }
    }
    private void TryCapture(Livestock livestock)
    {
        bool isBig = CageType == "cage_big";
        bool animalIsBig = livestock.Type == Livestock.AnimalType.Cow ||
                           livestock.Type == Livestock.AnimalType.Pig ||
                           livestock.Type == Livestock.AnimalType.Sheep ||
                           livestock.Type == Livestock.AnimalType.Goat;

        if (isBig == animalIsBig)
        {
            var animalType = livestock.Type;
            var wb = WorldBuilder.Instance;
            if (wb != null)
                wb.SpawnCageWithAnimal(transform.position, animalType);
            Destroy(livestock.gameObject);
            GameManager.Instance?.UIManager?.ShowMessage("Captured " + animalType + "!", 2f);
            Destroy(gameObject);
        }
        else
        {
            GameManager.Instance?.UIManager?.ShowMessage("Wrong cage size!", 1.5f);
        }
    }
    private void ReleaseAnimal()
    {
        var wb = WorldBuilder.Instance;
        if (wb != null)
        {
            var pos = transform.position;
            pos.y = Mathf.Max(pos.y, 0.5f);
            var go = new GameObject("Livestock_" + CapturedAnimal);
            go.transform.SetParent(wb.WorldRoot.transform);
            go.transform.position = pos;
            var livestock = go.AddComponent<Livestock>();
            livestock.Type = CapturedAnimal;
            livestock.StartSpawnAnimation();
            GameManager.Instance?.UIManager?.ShowMessage(CapturedAnimal + " released!", 1.5f);
        }
        HasCapturedAnimal = false;

        var cageModel = transform.Find("CageModel");
        if (cageModel != null)
        {
            var animalModel = cageModel.Find("Model");
            if (animalModel != null) Destroy(animalModel.gameObject);
        }
    }
    private bool Land()
    {
        _landed = true;

        if (WifeDonationField.TryDonateCage(transform))
            return true;

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        var cols = GetComponents<Collider>();
        foreach (var c in cols)
        {
            if (c is BoxCollider bc)
            {
                bc.isTrigger = true;
                bc.size = new Vector3(0.6f, 0.6f, 0.6f);
            }
        }

        var pos = transform.position;
        pos.y = Mathf.Max(pos.y, 0.3f);
        transform.position = pos;
        transform.rotation = Quaternion.identity;

        Destroy(this);
        return false;
    }
}

public class CageWithAnimalInfo : MonoBehaviour
{
    public Livestock.AnimalType AnimalType;
}

public class ThrownCageInfo : MonoBehaviour
{
    public string CageType;
}

public class GhostPartData : MonoBehaviour
{
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public Vector3 LocalScale;
    public Color Color;
}
