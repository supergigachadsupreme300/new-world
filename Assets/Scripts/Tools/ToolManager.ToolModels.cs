using UnityEngine;

public partial class ToolManager
{
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
        return Camera.main != null ? Camera.main : Camera.current ?? Object.FindAnyObjectByType<Camera>();
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
        CreateToolModel("scythe", new Color(0.4f, 0.4f, 0.4f));
        
        // Items & seeds
        CreateToolModel("fertilizer", new Color(0.2f, 0.7f, 0.2f));
        CreateToolModel("wheat_seed", new Color(0.7f, 0.5f, 0.2f));
        CreateToolModel("peashooter_seed", new Color(1f, 0.86f, 0.31f));
        CreateToolModel("corn_seed", new Color(1f, 0.86f, 0.24f));
        CreateToolModel("potato_seed", new Color(0.7f, 0.5f, 0.2f));
        CreateToolModel("carrot_seed", new Color(1f, 0.5f, 0f));
        CreateToolModel("tomato_seed", new Color(1f, 0.3f, 0.1f));
        CreateToolModel("strawberry_seed", new Color(1f, 0.2f, 0.2f));
        CreateToolModel("pumpkin_seed", new Color(1f, 0.7f, 0.1f));
        CreateToolModel("onion_seed", new Color(0.7f, 0.5f, 0.3f));
        CreateToolModel("sugarcane_seed", new Color(0.4f, 0.7f, 0.2f));
        CreateToolModel("rice_seed", new Color(0.9f, 0.85f, 0.4f));
        CreateToolModel("watering_can", new Color(0.4f, 0.5f, 0.6f));
        
        // Crops & resources
        CreateToolModel("wood", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("stone", new Color(0.5f, 0.5f, 0.5f));
        CreateToolModel("wheat", new Color(1f, 1f, 0.5f));
        CreateToolModel("damaged_wheat", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("corn", new Color(1f, 0.85f, 0.2f));
        CreateToolModel("damaged_corn", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("potato", new Color(0.627f, 0.431f, 0.235f));
        CreateToolModel("damaged_potato", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("carrot", new Color(1f, 0.55f, 0.1f));
        CreateToolModel("damaged_carrot", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("tomato", new Color(1f, 0.2f, 0.1f));
        CreateToolModel("damaged_tomato", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("strawberry", new Color(1f, 0.15f, 0.15f));
        CreateToolModel("damaged_strawberry", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("pumpkin", new Color(1f, 0.6f, 0.1f));
        CreateToolModel("damaged_pumpkin", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("onion", new Color(0.8f, 0.5f, 0.2f));
        CreateToolModel("damaged_onion", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("sugarcane", new Color(0.3f, 0.7f, 0.15f));
        CreateToolModel("damaged_sugarcane", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("rice", new Color(1f, 0.9f, 0.3f));
        CreateToolModel("damaged_rice", new Color(0.6f, 0.4f, 0.2f));
        CreateToolModel("field", new Color(0.45f, 0.28f, 0.12f));
        CreateToolModel("mobspawner", new Color(0.25f, 0.25f, 0.25f));
        
        // Special items
        CreateToolModel("mi_hao_hao", new Color(0.8f, 0.3f, 0.2f));

        // Convenience store
        CreateToolModel("nuoc_dau", new Color(0.75f, 0.9f, 0.75f));
        CreateToolModel("tra_da", new Color(0.75f, 0.5f, 0.3f));
        CreateToolModel("soda", new Color(0.85f, 0.2f, 0.15f));
        CreateToolModel("banh_mi", new Color(0.75f, 0.5f, 0.2f));
        CreateToolModel("banh_tet", new Color(0.2f, 0.55f, 0.2f));
        CreateToolModel("keo", new Color(0.95f, 0.35f, 0.55f));
        CreateToolModel("cafe_den", new Color(0.3f, 0.18f, 0.08f));

        // Grocery store
        CreateToolModel("tu_gao", new Color(0.95f, 0.93f, 0.88f));
        CreateToolModel("duong", new Color(0.95f, 0.92f, 0.82f));
        CreateToolModel("muoi", new Color(0.93f, 0.94f, 0.96f));
        CreateToolModel("xap_phong", new Color(0.75f, 0.85f, 0.95f));
        CreateToolModel("mi_chinh", new Color(0.9f, 0.2f, 0.18f));

        // Restaurant dishes
        CreateToolModel("com_trang", new Color(1f, 0.97f, 0.9f));
        CreateToolModel("com_tam", new Color(0.85f, 0.75f, 0.55f));
        CreateToolModel("com_ga", new Color(1f, 0.6f, 0.35f));
        CreateToolModel("com_chieu", new Color(0.9f, 0.6f, 0.2f));
        
        // Livestock tools
        CreateToolModel("club", new Color(0.5f, 0.25f, 0.05f));
        CreateToolModel("cage_big", new Color(0.5f, 0.5f, 0.55f));
        CreateToolModel("cage_small", new Color(0.55f, 0.55f, 0.6f));
        CreateToolModel("fishing_rod", new Color(0.5f, 0.3f, 0.08f));
        CreateToolModel("fishing_bait", new Color(0.75f, 0.4f, 0.3f));
        CreateToolModel("fishing_chum", new Color(0.8f, 0.15f, 0.1f));
        CreateToolModel("rosary", new Color(1f, 0.84f, 0.2f));
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
                case "fertilizer":
                    var fertPart = new GameObject("Fertilizer");
                    fertPart.transform.SetParent(root.transform);
                    fertPart.transform.localPosition = Vector3.zero;
                    ItemBuilder.BuildFertilizer(fertPart.transform);
                    if (FertilizerTexture != null)
                        ApplyTextureToAllChildren(fertPart, FertilizerTexture);
                    break;
                case "peashooter_seed":
                    var peashooterPart = new GameObject("PeashooterSeed");
                    peashooterPart.transform.SetParent(root.transform);
                    peashooterPart.transform.localPosition = Vector3.zero;
                    ItemBuilder.BuildPeashooterSeed(peashooterPart.transform);
                    if (PeashooterSeedTexture != null)
                        ApplyTextureToAllChildren(peashooterPart, PeashooterSeedTexture);
                    break;
                case "wheat":
                    ItemBuilder.BuildWheatPickup(root.transform, new Color(1f, 1f, 0.5f));
                    break;
                case "mi_hao_hao":
                    if (MiHaoHaoModel != null)
                    {
                        var miHaoHaoInstance = Instantiate(MiHaoHaoModel, root.transform);
                        miHaoHaoInstance.name = "MiHaoHao_Model";
                        miHaoHaoInstance.transform.localPosition = Vector3.zero;
                        miHaoHaoInstance.transform.localRotation = Quaternion.identity;
                        miHaoHaoInstance.transform.localScale = Vector3.one;
                        if (MiHaoHaoTexture != null)
                            ApplyTextureToAllChildren(miHaoHaoInstance, MiHaoHaoTexture);
                    }
                    else
                    {
                        ItemBuilder.BuildMiHaoHao(root.transform);
                    }
                    break;
                default:
                    ItemBuilder.BuildItem(root.transform, toolType);
                    break;
            }
        }
        root.SetActive(false);
        _toolModels[toolType] = root;
        DestroyAllColliders(root);
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

    private void ApplyTextureToAllChildren(GameObject go, Texture2D texture)
    {
        if (texture == null)
            return;

        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            var textureMaterial = new Material(Shader.Find("Standard"));
            textureMaterial.mainTexture = texture;
            renderer.material = textureMaterial;
        }
    }
}
