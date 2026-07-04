using System.Collections.Generic;
using UnityEngine;

public class FieldManager : MonoBehaviour
{
    public static FieldManager Instance { get; private set; }

    [Header("Field Textures")]
    public Texture2D FieldTexture;
    public Material FieldMaterial;

    [Header("Field Preview")]
    public Material FieldPreviewMaterial;
    public Color FieldPreviewColor = new Color(150f / 255f, 100f / 255f, 50f / 255f, 140f / 255f);

    private List<FieldData> _fields = new List<FieldData>();
    private GameObject _fieldPreview;
    private readonly float _fieldSize = 1f;
    private Dictionary<FieldData, float> _growthTimers = new Dictionary<FieldData, float>();

    // Crop visual configurations
    private static readonly Dictionary<string, CropVisual> CropVisuals = new Dictionary<string, CropVisual>
    {
        {
            "wheat", new CropVisual
            {
                YoungColor = Color.green,
                RipeColor = Color.yellow,
                PatchCount = 5,
                Style = CropStyle.Tall
            }
        },
        {
            "corn", new CropVisual
            {
                YoungColor = Color.green,
                RipeColor = new Color(1f, 210f / 255f, 60f / 255f),
                PatchCount = 4,
                Style = CropStyle.Stalk
            }
        },
        {
            "potato", new CropVisual
            {
                YoungColor = new Color(90f / 255f, 150f / 255f, 70f / 255f),
                RipeColor = new Color(120f / 255f, 180f / 255f, 80f / 255f),
                PatchCount = 6,
                Style = CropStyle.Low
            }
        },
    };

    private const int CropMaxStages = 4;
    private const float CropMaxHP = 20f;
    private const float CropGrowthDelay = 4f;

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
        CreateFieldPreview();
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame)
            return;

        // Update crop growth timers
        var fieldsToRemove = new List<FieldData>();
        foreach (var kvp in _growthTimers)
        {
            kvp.Key.GrowthTimer -= Time.deltaTime;
            if (kvp.Key.GrowthTimer <= 0)
            {
                AdvanceCropGrowthInternal(kvp.Key);
                fieldsToRemove.Add(kvp.Key);
            }
        }
        foreach (var field in fieldsToRemove)
            _growthTimers.Remove(field);

        // Update field preview position based on raycast
        UpdateFieldPreview();
    }

    public FieldData CreateField(Vector3 pos)
    {
        var fieldRoot = new GameObject("Field");
        fieldRoot.transform.position = pos;
        
        var worldBuilder = WorldBuilder.Instance;
        if (worldBuilder != null && worldBuilder.WorldRoot != null)
            fieldRoot.transform.SetParent(worldBuilder.WorldRoot.transform);

        // Create visual ground indicator - match Python cube scale (1, 0.2, 1)
        var groundIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundIndicator.name = "FieldVisual";
        groundIndicator.transform.SetParent(fieldRoot.transform);
        groundIndicator.transform.localPosition = Vector3.up * 0.1f;
        groundIndicator.transform.localScale = new Vector3(_fieldSize, 0.2f, _fieldSize);
        
        var renderer = groundIndicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (FieldTexture != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.mainTexture = FieldTexture;
            }
            else
            {
                renderer.material.color = new Color(0.45f, 0.28f, 0.12f);
            }
        }
        Destroy(groundIndicator.GetComponent<Collider>());

        var fieldData = new FieldData
        {
            Entity = fieldRoot,
            Position = new Vector3(pos.x, 0.1f, pos.z),
            CropType = null,
            WheatStage = 0,
            WheatHP = 0,
            WheatNodes = new List<GameObject>(),
            HealthBar = null,
            PeashooterEntity = null,
            PeashooterHP = 0,
            FieldSize = _fieldSize
        };

        _fields.Add(fieldData);
        return fieldData;
    }

    public FieldData FindFieldByEntity(GameObject entity)
    {
        foreach (var field in _fields)
        {
            if (field.Entity == entity)
                return field;
        }
        return null;
    }

    public FieldData FindFieldByPosition(Vector3 pos)
    {
        foreach (var field in _fields)
        {
            if (Vector3.Distance(field.Position, pos) < 1.5f)
                return field;
        }
        return null;
    }

    public bool HasCrop(FieldData fieldData)
    {
        return fieldData != null && 
               !string.IsNullOrEmpty(fieldData.CropType) && 
               fieldData.WheatHP > 0;
    }

    public bool PlantCropOnField(FieldData fieldData, string cropType)
    {
        if (fieldData == null || HasCrop(fieldData))
            return false;

        if (!CropVisuals.ContainsKey(cropType))
            return false;

        fieldData.CropType = cropType;
        fieldData.WheatStage = 1;
        fieldData.WheatHP = CropMaxHP;
        fieldData.WheatNodes.Clear();

        var visuals = CropVisuals[cropType];
        int numPatches = Random.Range(Mathf.Max(3, visuals.PatchCount - 1), visuals.PatchCount + 1);

        for (int i = 0; i < numPatches; i++)
        {
            float width = Random.Range(0.22f, 0.42f);
            float depth = Random.Range(0.15f, 0.30f);
            float offsetX = Random.Range(-0.35f, 0.35f);
            float offsetZ = Random.Range(-0.35f, 0.35f);
            float initialHeight = Random.Range(0.55f, 1.15f);

            var patch = MakeCropPatch(fieldData, cropType, offsetX, offsetZ, initialHeight, width, depth);
            fieldData.WheatNodes.Add(patch);
        }

        // Create health bar
        var healthBarObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        healthBarObj.name = "HealthBar";
        healthBarObj.transform.SetParent(fieldData.Entity.transform);
        healthBarObj.transform.localPosition = new Vector3(0, 1.5f, 0);
        healthBarObj.transform.localScale = new Vector3(1f, 0.1f, 0.1f);
        Destroy(healthBarObj.GetComponent<Collider>());
        var hbRenderer = healthBarObj.GetComponent<Renderer>();
        if (hbRenderer != null)
            hbRenderer.material.color = Color.red;
        fieldData.HealthBar = healthBarObj;

        UpdateCropPatchVisuals(fieldData, 1);
        fieldData.GrowthTimer = CropGrowthDelay;
        _growthTimers[fieldData] = fieldData.GrowthTimer;
        return true;
    }

    private GameObject MakeCropPatch(FieldData fieldData, string cropType, float offsetX, float offsetZ, float initialHeight, float width, float depth)
    {
        var visuals = CropVisuals[cropType];
        var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        patch.name = $"{cropType}_Patch";
        patch.transform.SetParent(fieldData.Entity.transform);
        Destroy(patch.GetComponent<Collider>());

        var renderer = patch.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = visuals.YoungColor;

        // Store patch metadata
        var patchData = patch.AddComponent<CropPatchData>();
        patchData.InitialHeight = initialHeight;
        patchData.CropStyle = visuals.Style;

        // Match Python crop model specifications exactly
        float scaleY;
        float posY;
        float patchWidth = width;
        float patchDepth = depth;

        switch (visuals.Style)
        {
            case CropStyle.Stalk:  // corn - fixed width/depth
                scaleY = initialHeight * 0.25f;
                posY = 0.1f + initialHeight * 0.25f / 2f;
                patchWidth = 0.12f;
                patchDepth = 0.12f;
                break;
            case CropStyle.Low:    // potato - shorter scale
                scaleY = initialHeight * 0.2f;
                posY = 0.08f + initialHeight * 0.2f / 2f;
                break;
            default:               // wheat/tall
                scaleY = initialHeight * 0.25f;
                posY = 0.1f + initialHeight * 0.25f / 2f;
                break;
        }

        patch.transform.localScale = new Vector3(patchWidth, scaleY, patchDepth);
        patch.transform.localPosition = new Vector3(offsetX, posY, offsetZ);

        return patch;
    }

    private void UpdateCropPatchVisuals(FieldData fieldData, int stage)
    {
        if (fieldData.CropType == null || !CropVisuals.ContainsKey(fieldData.CropType))
            return;

        var visuals = CropVisuals[fieldData.CropType];
        float targetRatio = stage / (float)CropMaxStages;
        Color patchColor = stage >= CropMaxStages ? visuals.RipeColor : visuals.YoungColor;

        foreach (var patch in fieldData.WheatNodes)
        {
            if (patch == null)
                continue;

            var patchData = patch.GetComponent<CropPatchData>();
            if (patchData == null)
                continue;

            float targetHeight = patchData.InitialHeight * targetRatio;

            switch (patchData.CropStyle)
            {
                case CropStyle.Stalk:
                    patch.transform.localScale = new Vector3(patch.transform.localScale.x, targetHeight, patch.transform.localScale.z);
                    patch.transform.localPosition = new Vector3(
                        patch.transform.localPosition.x,
                        0.1f + targetHeight / 2f,
                        patch.transform.localPosition.z
                    );
                    break;
                case CropStyle.Low:
                    float scaledHeight = targetHeight * 0.25f;
                    patch.transform.localScale = new Vector3(patch.transform.localScale.x, scaledHeight, patch.transform.localScale.z);
                    patch.transform.localPosition = new Vector3(
                        patch.transform.localPosition.x,
                        0.08f + scaledHeight / 2f,
                        patch.transform.localPosition.z
                    );
                    break;
                default: // Tall
                    patch.transform.localScale = new Vector3(patch.transform.localScale.x, targetHeight, patch.transform.localScale.z);
                    patch.transform.localPosition = new Vector3(
                        patch.transform.localPosition.x,
                        0.1f + targetHeight / 2f,
                        patch.transform.localPosition.z
                    );
                    break;
            }

            var renderer = patch.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = patchColor;
        }
    }

    private void AdvanceCropGrowth(FieldData fieldData)
    {
        if (!HasCrop(fieldData) || fieldData.WheatStage >= CropMaxStages)
            return;

        fieldData.GrowthTimer = CropGrowthDelay;
        _growthTimers[fieldData] = fieldData.GrowthTimer;
    }

    private void AdvanceCropGrowthInternal(FieldData fieldData)
    {
        if (!HasCrop(fieldData) || fieldData.WheatStage >= CropMaxStages)
            return;

        fieldData.WheatStage++;
        UpdateCropPatchVisuals(fieldData, fieldData.WheatStage);

        if (fieldData.WheatStage < CropMaxStages)
            AdvanceCropGrowth(fieldData);
    }

    public string GetHarvestItem(FieldData fieldData)
    {
        if (fieldData.CropType == null)
            return "wheat";

        bool isRipe = fieldData.WheatStage >= CropMaxStages;

        return fieldData.CropType switch
        {
            "corn" => isRipe ? "corn" : "damaged_corn",
            "potato" => isRipe ? "potato" : "damaged_potato",
            _ => isRipe ? "wheat" : "damaged_wheat"
        };
    }

    public void DestroyWheat(FieldData fieldData)
    {
        if (fieldData == null)
            return;

        foreach (var patch in fieldData.WheatNodes)
        {
            if (patch != null)
                Destroy(patch);
        }
        fieldData.WheatNodes.Clear();
        fieldData.CropType = null;
        fieldData.WheatStage = 0;
        fieldData.WheatHP = 0;

        if (fieldData.HealthBar != null)
        {
            Destroy(fieldData.HealthBar);
            fieldData.HealthBar = null;
        }
    }

    public void UpdateWheatHealthBar(FieldData fieldData)
    {
        if (fieldData.HealthBar == null)
            return;

        float hpRatio = fieldData.WheatHP / CropMaxHP;
        fieldData.HealthBar.transform.localScale = new Vector3(hpRatio, 0.1f, 0.1f);
        fieldData.HealthBar.transform.localPosition = new Vector3(-0.5f + hpRatio / 2f, 1.5f, 0);
    }

    private void CreateFieldPreview()
    {
        // Use Cube instead of Quad - matches Python field creation scale(1, 0.2, 1)
        _fieldPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _fieldPreview.name = "FieldPreview";
        _fieldPreview.transform.localScale = new Vector3(_fieldSize, 0.2f, _fieldSize);
        
        var renderer = _fieldPreview.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = FieldPreviewColor;
        }
        Destroy(_fieldPreview.GetComponent<Collider>());
        _fieldPreview.SetActive(false);
    }

    private void UpdateFieldPreview()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        var ray = new Ray(cam.transform.position + cam.transform.forward * 0.3f, cam.transform.forward);
        
        // Check if raycast hits an existing field
        FieldData hitField = null;
        Vector3 hitNormal = Vector3.up;
        
        foreach (var field in _fields)
        {
            if (field.Entity == null) continue;
            
            var fieldVisual = field.Entity.GetComponentInChildren<Renderer>();
            if (fieldVisual != null)
            {
                var fieldCollider = fieldVisual.GetComponent<Collider>();
                if (fieldCollider == null)
                {
                    fieldCollider = field.Entity.GetComponent<Collider>();
                }
                
                if (fieldCollider != null && Physics.Raycast(ray, out RaycastHit fieldHit, 10f))
                {
                    if (fieldHit.collider == fieldCollider || fieldHit.collider.transform.IsChildOf(fieldCollider.transform))
                    {
                        hitField = field;
                        hitNormal = fieldHit.normal;
                        break;
                    }
                }
            }
        }
        
        if (hitField != null)
        {
            // Place adjacent field based on which face was hit
            Vector3 adjacentPos = CalculateAdjacentPosition(hitField.Entity.transform.position, hitNormal);
            _fieldPreview.transform.position = adjacentPos;
            _fieldPreview.SetActive(true);
            return;
        }
        
        // If no field hit, check for ground
        if (Physics.Raycast(ray, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.name == "Ground" || hit.collider.name == "FieldVisual")
            {
                // Snap preview to nearest field grid position (1 unit = field size)
                Vector3 hitPos = hit.point;
                Vector3 gridPos = new Vector3(
                    Mathf.Round(hitPos.x),
                    0.1f,  // Field height = 0.1 (cube with height 0.2, positioned at y=0.1)
                    Mathf.Round(hitPos.z)
                );
                
                _fieldPreview.transform.position = gridPos;
                _fieldPreview.SetActive(true);
                return;
            }
        }

        _fieldPreview.SetActive(false);
    }
    
    private Vector3 CalculateAdjacentPosition(Vector3 fieldPos, Vector3 hitNormal)
    {
        // Determine which face was hit based on normal
        float absX = Mathf.Abs(hitNormal.x);
        float absZ = Mathf.Abs(hitNormal.z);
        
        Vector3 adjacentPos = fieldPos;
        
        if (absX > absZ)  // Hit X-facing side
        {
            adjacentPos.x += Mathf.Sign(hitNormal.x) * _fieldSize;
        }
        else  // Hit Z-facing side
        {
            adjacentPos.z += Mathf.Sign(hitNormal.z) * _fieldSize;
        }
        
        adjacentPos.y = 0.1f;  // Position at field height
        return adjacentPos;
    }

    public void ShowFieldPreview(bool visible)
    {
        if (_fieldPreview != null)
            _fieldPreview.SetActive(visible);
    }

    public List<FieldData> GetAllFields() => _fields;
}

public class FieldData
{
    public GameObject Entity;
    public Vector3 Position;
    public string CropType;
    public int WheatStage;
    public float WheatHP;
    public List<GameObject> WheatNodes;
    public GameObject HealthBar;
    public GameObject PeashooterEntity;
    public float PeashooterHP;
    public float GrowthTimer;
    public float FieldSize;
}

public class CropVisual
{
    public Color YoungColor;
    public Color RipeColor;
    public int PatchCount;
    public CropStyle Style;
}

public enum CropStyle
{
    Tall,   // wheat
    Stalk,  // corn
    Low     // potato
}

public class CropPatchData : MonoBehaviour
{
    public float InitialHeight;
    public CropStyle CropStyle;
}
