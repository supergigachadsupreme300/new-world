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
    private readonly float _fieldSize = 2f; // Match WorldBuilder field tile size
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

        // Update field preview only when the player is holding the hoe
        if (ToolManager.Instance == null || ToolManager.Instance.GetSelectedItemType() != "hoe")
        {
            if (_fieldPreview != null)
                _fieldPreview.SetActive(false);
            return;
        }

        UpdateFieldPreview();
    }

    public FieldData CreateField(Vector3 pos)
    {
        var fieldRoot = new GameObject("Field");
        fieldRoot.transform.position = pos;
        
        var worldBuilder = WorldBuilder.Instance;
        if (worldBuilder != null && worldBuilder.WorldRoot != null)
            fieldRoot.transform.SetParent(worldBuilder.WorldRoot.transform);

        // Create visual ground indicator to match WorldBuilder field tile
        var groundIndicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
        groundIndicator.name = "FieldVisual";
        groundIndicator.transform.SetParent(fieldRoot.transform);
        groundIndicator.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        groundIndicator.transform.localPosition = Vector3.up * 0.01f;
        groundIndicator.transform.localScale = new Vector3(_fieldSize, _fieldSize, 1f);
        
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
            Position = new Vector3(pos.x, 0.01f, pos.z),
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
        var patch = new GameObject($"{cropType}_Patch");
        patch.transform.SetParent(fieldData.Entity.transform);
        patch.transform.localPosition = new Vector3(offsetX, 0f, offsetZ);
        patch.transform.localRotation = Quaternion.identity;
        // Store patch metadata and child pieces
        var patchData = patch.AddComponent<CropPatchData>();
        patchData.InitialHeight = initialHeight;
        patchData.CropStyle = visuals.Style;
        patchData.Parts = new List<Transform>();

        if (visuals.Style == CropStyle.Tall)
        {
            int stalkCount = Random.Range(2, 5);
            for (int i = 0; i < stalkCount; i++)
            {
                float stalkWidth = Random.Range(0.06f, 0.1f);
                float stalkDepth = Random.Range(0.02f, 0.05f);
                float stalkHeight = initialHeight * 0.25f;
                float x = Random.Range(-0.15f, 0.15f);
                float z = Random.Range(-0.05f, 0.05f);
                var stalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stalk.transform.SetParent(patch.transform);
                stalk.transform.localScale = new Vector3(stalkWidth, stalkHeight, stalkDepth);
                stalk.transform.localPosition = new Vector3(x, stalkHeight / 2f + 0.05f, z);
                ApplyColor(stalk, visuals.YoungColor);
                Destroy(stalk.GetComponent<Collider>());
                patchData.Parts.Add(stalk.transform);
            }
        }
        else if (visuals.Style == CropStyle.Stalk)
        {
            float bottomHeight = initialHeight * 0.25f;
            var bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottom.transform.SetParent(patch.transform);
            bottom.transform.localScale = new Vector3(0.12f, bottomHeight, 0.12f);
            bottom.transform.localPosition = new Vector3(0f, bottomHeight / 2f + 0.05f, 0f);
            ApplyColor(bottom, visuals.YoungColor);
            Destroy(bottom.GetComponent<Collider>());
            patchData.Parts.Add(bottom.transform);
        }
        else // Low / potato
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.transform.SetParent(patch.transform);
            root.transform.localScale = new Vector3(0.08f, 0.06f, 0.07f);
            root.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ApplyColor(root, new Color(0.65f, 0.45f, 0.2f));
            Destroy(root.GetComponent<Collider>());
            patchData.Parts.Add(root.transform);

            int leafCount = Random.Range(3, 5);
            for (int i = 0; i < leafCount; i++)
            {
                float angle = i * Mathf.PI * 2f / leafCount;
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.transform.SetParent(patch.transform);
                leaf.transform.localScale = new Vector3(0.06f, 0.01f, 0.08f);
                leaf.transform.localRotation = Quaternion.Euler(30f, i * 360f / leafCount, 0f);
                leaf.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 0.05f,
                    0.09f,
                    Mathf.Sin(angle) * 0.05f
                );
                ApplyColor(leaf, visuals.YoungColor);
                Destroy(leaf.GetComponent<Collider>());
                patchData.Parts.Add(leaf.transform);
            }
        }

        return patch;
    }

    private void UpdateCropPatchVisuals(FieldData fieldData, int stage)
    {
        if (fieldData.CropType == null || !CropVisuals.ContainsKey(fieldData.CropType))
            return;

        var visuals = CropVisuals[fieldData.CropType];
        float targetRatio = stage / (float)CropMaxStages;
        bool isRipe = stage >= CropMaxStages;

        foreach (var patch in fieldData.WheatNodes)
        {
            if (patch == null)
                continue;

            var patchData = patch.GetComponent<CropPatchData>();
            if (patchData == null)
                continue;

            float targetHeight = patchData.InitialHeight * targetRatio;
            Color baseColor = isRipe ? visuals.RipeColor : visuals.YoungColor;

            if (patchData.CropStyle == CropStyle.Tall)
            {
                for (int i = 0; i < patchData.Parts.Count; i++)
                {
                    var part = patchData.Parts[i];
                    float partHeight = targetHeight;
                    part.localScale = new Vector3(part.localScale.x, partHeight, part.localScale.z);
                    part.localPosition = new Vector3(part.localPosition.x, partHeight / 2f + 0.05f, part.localPosition.z);
                    ApplyColor(part.gameObject, baseColor);
                }
            }
            else if (patchData.CropStyle == CropStyle.Stalk)
            {
                if (patchData.Parts.Count >= 1)
                {
                    var bottom = patchData.Parts[0];
                    float bottomHeight = targetHeight;
                    bottom.localScale = new Vector3(bottom.localScale.x, bottomHeight, bottom.localScale.z);
                    bottom.localPosition = new Vector3(0f, bottomHeight / 2f + 0.05f, 0f);
                    ApplyColor(bottom.gameObject, visuals.YoungColor);

                    if (isRipe)
                    {
                        if (patchData.Parts.Count < 2)
                        {
                            float earHeight = targetHeight * 0.4f;
                            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            top.transform.SetParent(patch.transform);
                            top.transform.localScale = new Vector3(0.18f, earHeight, 0.14f);
                            top.transform.localPosition = new Vector3(0f, bottomHeight + earHeight / 2f + 0.05f, 0f);
                            ApplyColor(top, visuals.RipeColor);
                            Destroy(top.GetComponent<Collider>());
                            patchData.Parts.Add(top.transform);
                        }
                        else
                        {
                            var top = patchData.Parts[1];
                            float earHeight = targetHeight * 0.4f;
                            top.localScale = new Vector3(top.localScale.x, earHeight, top.localScale.z);
                            top.localPosition = new Vector3(0f, bottomHeight + earHeight / 2f + 0.05f, 0f);
                            ApplyColor(top.gameObject, visuals.RipeColor);
                        }
                    }
                }
            }
            else // Low/potato
            {
                for (int i = 0; i < patchData.Parts.Count; i++)
                {
                    var part = patchData.Parts[i];
                    if (i == 0)
                    {
                        float rootScale = 1f + 0.3f * targetRatio;
                        part.localScale = new Vector3(0.08f * rootScale, 0.06f * rootScale, 0.07f * rootScale);
                        part.localPosition = new Vector3(0f, 0.03f * rootScale, 0f);
                        ApplyColor(part.gameObject, new Color(0.65f, 0.45f, 0.2f));
                    }
                    else
                    {
                        float leafHeight = 0.09f + 0.07f * targetRatio;
                        float radius = 0.05f + 0.07f * targetRatio;
                        int leafIndex = i - 1;
                        int totalLeaves = patchData.Parts.Count - 1;
                        if (totalLeaves > 0)
                        {
                            float angle = leafIndex * Mathf.PI * 2f / totalLeaves;
                            part.localScale = new Vector3(
                                0.05f + 0.05f * targetRatio,
                                0.01f,
                                0.07f + 0.06f * targetRatio
                            );
                            part.transform.localRotation = Quaternion.Euler(30f, leafIndex * 360f / totalLeaves, 0f);
                            part.localPosition = new Vector3(
                                Mathf.Cos(angle) * radius,
                                leafHeight,
                                Mathf.Sin(angle) * radius
                            );
                        }
                        ApplyColor(part.gameObject, visuals.YoungColor);
                    }
                }
            }
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

    private void CreateFieldPreview()
    {
        _fieldPreview = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fieldPreview.name = "FieldPreview";
        _fieldPreview.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _fieldPreview.transform.localScale = new Vector3(_fieldSize, _fieldSize, 1f);
        
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
        if (Physics.Raycast(ray, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.name == "FieldTile")
            {
                // Compute adjacent placement toward the player/camera direction
                Vector3 fieldCenter = hit.collider.transform.position;
                Vector3 cameraDir = cam.transform.position - fieldCenter;
                Vector3 cameraDirXZ = new Vector3(cameraDir.x, 0f, cameraDir.z);

                Vector3 adjacentPos = fieldCenter;
                if (cameraDirXZ.sqrMagnitude > 0.001f)
                {
                    if (Mathf.Abs(cameraDirXZ.x) > Mathf.Abs(cameraDirXZ.z))
                        adjacentPos.x += Mathf.Sign(cameraDirXZ.x) * _fieldSize;
                    else
                        adjacentPos.z += Mathf.Sign(cameraDirXZ.z) * _fieldSize;
                }

                adjacentPos.y = 0.01f;
                _fieldPreview.transform.position = adjacentPos;
                _fieldPreview.SetActive(true);
                return;
            }

            if (hit.collider.name == "Ground" || hit.collider.name == "FieldVisual")
            {
                Vector3 hitPos = hit.point;
                Vector3 gridPos = new Vector3(
                    Mathf.Round(hitPos.x),
                    0.01f,
                    Mathf.Round(hitPos.z)
                );

                _fieldPreview.transform.position = gridPos;
                _fieldPreview.SetActive(true);
                return;
            }
        }

        _fieldPreview.SetActive(false);
    }
    
    private void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;
        renderer.material.color = color;
    }

    public bool TryGetPreviewPosition(out Vector3 previewPosition)
    {
        previewPosition = Vector3.zero;
        if (_fieldPreview == null || !_fieldPreview.activeSelf)
            return false;

        previewPosition = _fieldPreview.transform.position;
        return true;
    }
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
    public List<Transform> Parts;
}
