using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;
using CountryLife.Helpers;

public partial class WorldBuilder
{
    public BuildingDefinition GetBuildingByIndex(int i) => _availableBuildings[i];

    public int BlueprintOptionCount => EnableLegacyGeneration ? _buildings.Count : _availableBuildings.Length;

    public bool IsBlueprintUnlocked(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        if (!EnableLegacyGeneration) return true; // streaming world: all blueprints usable
        if (!ResearchCosts.ContainsKey(name)) return true;
        return _unlockedBlueprints.Contains(name);
    }

    public void UnlockBlueprint(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        _unlockedBlueprints.Add(name);
    }

    public List<(string Name, int Cost)> GetResearchableBlueprints()
    {
        var list = new List<(string Name, int Cost)>();
        foreach (var kvp in ResearchCosts)
        {
            if (!_unlockedBlueprints.Contains(kvp.Key))
                list.Add((kvp.Key, kvp.Value));
        }
        return list;
    }

    public string[] GetUnlockedBlueprintsAsSave()
    {
        return new List<string>(_unlockedBlueprints).ToArray();
    }

    public void LoadUnlockedBlueprints(string[] unlocked)
    {
        _unlockedBlueprints.Clear();
        if (unlocked == null) return;
        foreach (var name in unlocked)
        {
            if (!string.IsNullOrEmpty(name))
                _unlockedBlueprints.Add(name);
        }
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
        if (!IsBlueprintUnlocked(definition.Name))
        {
            GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Bản thiết kế này bị khóa. Hãy đến Thư Viện tìm hiểu thêm!"), 2f);
            return false;
        }
        var size = definition.Size;
        Vector3 snapped = SnapToGrid(position);
        if (EnableLegacyGeneration && IsWallOrStair(definition.Name) && !HasFloorAt(snapped))
        {
            Debug.Log("Must place on a floor first!");
            return false;
        }
        if (!CanPlaceBuilding(snapped, size, _currentRotation))
            return false;

        if (definition.SubBuildings != null && definition.SubBuildings.Length > 0)
        {
            string structureId = System.Guid.NewGuid().ToString();
            var subPlans = new List<(string Part, Vector3 Pos, Vector3 Size, Color Color, int Wood, int Stone)>();
            foreach (var sub in definition.SubBuildings)
            {
                Vector3 rotatedOffset = Quaternion.Euler(0, _currentRotation, 0) * sub.Offset;
                Vector3 subPos = snapped + rotatedOffset;
                if (!CanPlaceBuilding(subPos, sub.Size, _currentRotation))
                {
                    GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Không đủ chỗ cho toàn bộ công trình."), 1.5f);
                    return false;
                }
                subPlans.Add((sub.PartName, subPos, sub.Size, sub.Color, sub.WoodCost, sub.StoneCost));
            }
            foreach (var plan in subPlans)
            {
                var subBp = CreateSingleBlueprint(plan.Part, plan.Pos, plan.Size, plan.Color, plan.Wood, plan.Stone);
                subBp.StructureId = structureId;
                _blueprints.Add(subBp);
            }
            return true;
        }

        var bpState = CreateSingleBlueprint(definition.Name, snapped, size, definition.Color, definition.WoodCost, definition.StoneCost);
        _blueprints.Add(bpState);
        return true;
    }

    private BlueprintState CreateSingleBlueprint(string typeName, Vector3 position, Vector3 size, Color color, int woodCost, int stoneCost)
    {
        EnsureWorldRoot();
        var blueprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueprint.name = "Blueprint";
        blueprint.transform.position = position + Vector3.up * (size.y * 0.5f);
        blueprint.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
        blueprint.transform.localScale = size;
        var renderer = blueprint.GetComponent<MeshRenderer>();
        var mat = PickupVisualHelper.CreateTransparentMaterialFromBase(CreateSafeLitMaterial(), new Color(color.r, color.g, color.b, 0.15f));
        renderer.material = mat;
        var collider = blueprint.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        blueprint.transform.SetParent(_worldRoot.transform);

        var bpState = new BlueprintState
        {
            Entity = blueprint,
            Type = typeName,
            Position = position,
            Rotation = _currentRotation,
            WoodDeposited = 0,
            StoneDeposited = 0,
            WoodCost = woodCost,
            StoneCost = stoneCost
        };
        CreateBlueprintLabel(blueprint, bpState, woodCost, stoneCost, size.y);
        blueprint.AddComponent<BlueprintAutoDeposit>();
        return bpState;
    }

    public void PlaceMansionBlueprint(Vector3 position)
    {
        var sub = _mansionSubBuildings[0];
        Vector3 rawSize = sub.Size;
        Vector3 rotatedSize = new Vector3(rawSize.z, rawSize.y, rawSize.x);
        var bpState = CreateMansionBlueprint(sub.PartName, position, rotatedSize, sub.Color, sub.WoodCost, sub.StoneCost);
        bpState.Rotation = -90;
        bpState.StructureId = "mansion_" + System.Guid.NewGuid().ToString();
        bpState.IsMansion = true;
        _blueprints.Add(bpState);
    }

    public void BuildPagoda(Vector3 position)
    {
        _pagodaPosition = position;
        foreach (var sub in _pagodaSubBuildings)
        {
            var bp = new BlueprintState
            {
                Type = sub.PartName,
                Position = position + sub.Offset,
                Rotation = 90
            };
            SpawnStructurePart(bp);
        }
    }

    public void BuildChurch(Vector3 position)
    {
        _churchPosition = position;
        foreach (var sub in _churchSubBuildings)
        {
            var bp = new BlueprintState
            {
                Type = sub.PartName,
                Position = position + sub.Offset,
                Rotation = 90
            };
            SpawnStructurePart(bp);
        }
    }

    public void BuildShrine(Vector3 position)
    {
        _shrinePosition = position;
        foreach (var sub in _shrineSubBuildings)
        {
            var bp = new BlueprintState
            {
                Type = sub.PartName,
                Position = position + sub.Offset,
                Rotation = 0
            };
            SpawnStructurePart(bp);
        }
    }

    private BlueprintState CreateMansionBlueprint(string typeName, Vector3 position, Vector3 size, Color color, int woodCost, int stoneCost)
    {
        var blueprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueprint.name = "Blueprint";
        blueprint.transform.position = position + Vector3.up * (size.y * 0.5f);
        blueprint.transform.rotation = Quaternion.identity;
        blueprint.transform.localScale = size;
        var renderer = blueprint.GetComponent<MeshRenderer>();
        var mat = PickupVisualHelper.CreateTransparentMaterialFromBase(CreateSafeLitMaterial(), new Color(0.85f, 0.65f, 0.13f, 0.15f));
        renderer.material = mat;
        var collider = blueprint.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        blueprint.transform.SetParent(_worldRoot.transform);

        var bpState = new BlueprintState
        {
            Entity = blueprint,
            Type = typeName,
            Position = position,
            Rotation = 0,
            WoodDeposited = 0,
            StoneDeposited = 0,
            WoodCost = woodCost,
            StoneCost = stoneCost,
            IsMansion = true
        };
        CreateBlueprintLabel(blueprint, bpState, woodCost, stoneCost, size.y);
        blueprint.AddComponent<BlueprintAutoDeposit>();
        return bpState;
    }

    public bool IsMansionPart(BlueprintState bp)
    {
        return bp != null && bp.IsMansion;
    }

    public int GetMansionCompletedParts()
    {
        int count = 0;
        foreach (var bp in _blueprints)
        {
            if (bp.IsMansion)
                count++;
        }
        return _mansionTotalParts - count;
    }

    public void CompleteMansionImmediately()
    {
        bool hasAny = false;
        for (int i = 0; i < _blueprints.Count; i++)
        {
            if (_blueprints[i].IsMansion) { hasAny = true; break; }
        }
        if (!hasAny && !HasMansionStructure())
            PlaceMansionBlueprint(MansionBasePos);

        var snapshot = new List<BlueprintState>(_blueprints);
        for (int i = 0; i < snapshot.Count; i++)
        {
            var bp = snapshot[i];
            if (!bp.IsMansion) continue;
            bp.WoodDeposited = bp.WoodCost;
            bp.StoneDeposited = bp.StoneCost;
            CompleteBlueprint(bp, null);
        }
    }

public bool HasMansionStructure()
    {
        if (_worldRoot == null) return false;
        if (HasPlayerMansion()) return true;
        var parts = _worldRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].name.StartsWith("StructurePart_Mansion_") || parts[i].name == "StructurePart_Mansion")
                return true;
        }
        return false;
    }

    public bool HasPlayerMansion()
    {
        if (_worldRoot == null) return false;
        var parts = _worldRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            var t = parts[i];
            if (t.name != "RichMansion") continue;
            if (Mathf.Abs(t.position.x - MansionBasePos.x) <= 0.6f &&
                Mathf.Abs(t.position.z - MansionBasePos.z) <= 0.6f)
                return true;
        }
        return false;
    }

    public Vector3? GetMansionPosition()
    {
        if (_worldRoot == null) return null;
        for (int i = 0; i < _blueprints.Count; i++)
        {
            if (_blueprints[i].IsMansion)
                return _blueprints[i].Position;
        }
        if (HasPlayerMansion())
            return MansionBasePos;
        return MansionBasePos;
    }

    public Vector3? GetRandomCloudPosition()
    {
        _clouds.RemoveAll(c => c == null);
        if (_clouds.Count == 0) return null;
        var c = _clouds[UnityEngine.Random.Range(0, _clouds.Count)];
        return c.transform.position;
    }

    public bool AreAnyMansionBlueprintsActive()
    {
        foreach (var bp in _blueprints)
        {
            if (bp.IsMansion)
                return true;
        }
        return false;
    }

    public bool CanPlaceBuilding(Vector3 position, Vector3 size, int rotation)
    {
        var half = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
        float e = 0.05f;
        var bounds = new Bounds(position + Vector3.up * half.y, new Vector3(size.x - e * 2, size.y - e * 2, size.z - e * 2));

        foreach (var building in _buildings)
        {
            if (building.Entity == null)
                continue;
            var cols = building.Entity.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col == null || col.isTrigger)
                    continue;
                if (bounds.Intersects(col.bounds))
                    return false;
            }
        }
        foreach (var bp in _blueprints)
        {
            if (bp.Entity == null)
                continue;
            var col = bp.Entity.GetComponent<Collider>();
            if (col == null)
                continue;
            if (bounds.Intersects(col.bounds))
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
        if (bp.IsEssential || bp.IsMansion)
        {
            woodCost = bp.WoodCost;
            stoneCost = bp.StoneCost;
        }
        else if (!string.IsNullOrEmpty(bp.StructureId))
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

        if (typeName == "door")
        {
            var root = new GameObject("Door");
            root.transform.position = position + Vector3.up * (def.Size.y * 0.5f);
            root.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            root.transform.SetParent(_worldRoot.transform);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "DoorPanel";
            panel.transform.SetParent(root.transform);
            panel.transform.localPosition = new Vector3(1.5f, 0f, 0f);
            panel.transform.localScale = new Vector3(3f, 4f, 0.3f);
            panel.transform.localRotation = Quaternion.identity;
            panel.GetComponent<MeshRenderer>().material.color = def.WoodColor;
            var panelCollider = panel.AddComponent<BoxCollider>();
            panelCollider.size = new Vector3(3f, 4f, 0.3f);

            partStates = new List<BuildingPartState>
            {
                new BuildingPartState { PartName = "Panel", Entity = panel, CurrentHealth = 4 }
            };
            return root;
        }

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

                Color partColor;
                if (typeName == "small_house")
                {
                    Color[] wallPalette = {
                        new Color(0.78f, 0.55f, 0.35f),
                        new Color(0.65f, 0.72f, 0.55f),
                        new Color(0.82f, 0.75f, 0.6f),
                        new Color(0.7f, 0.5f, 0.4f),
                        new Color(0.6f, 0.68f, 0.72f),
                        new Color(0.75f, 0.65f, 0.75f),
                        new Color(0.85f, 0.82f, 0.72f),
                        new Color(0.68f, 0.6f, 0.5f),
                        new Color(0.72f, 0.58f, 0.45f),
                        new Color(0.6f, 0.65f, 0.55f)
                    };
                    Color[] roofPalette = {
                        new Color(0.75f, 0.35f, 0.25f),
                        new Color(0.45f, 0.45f, 0.5f),
                        new Color(0.4f, 0.55f, 0.35f),
                        new Color(0.65f, 0.4f, 0.3f),
                        new Color(0.5f, 0.35f, 0.25f),
                        new Color(0.55f, 0.3f, 0.2f)
                    };
                    Color[] floorPalette = {
                        new Color(0.72f, 0.55f, 0.32f),
                        new Color(0.55f, 0.38f, 0.22f),
                        new Color(0.68f, 0.45f, 0.3f),
                        new Color(0.6f, 0.5f, 0.38f)
                    };
                    if (partDef.PartName == "Roof")
                        partColor = roofPalette[Random.Range(0, roofPalette.Length)];
                    else if (partDef.PartName == "Floor")
                        partColor = floorPalette[Random.Range(0, floorPalette.Length)];
                    else
                        partColor = wallPalette[Random.Range(0, wallPalette.Length)];
                }
                else
                {
                    partColor = partDef.MaterialType == "stone" ? def.StoneColor : def.WoodColor;
                }
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
        EnsureWorldRoot();
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
if (typeName == "goblin_hut")
        {
            GameManager.Instance?.EnsureGoblin();
            BuildGoblinChest(building.transform);
        }
        if (building != null)
            SittableSeat.Register(building.transform);
        NavGrid.Instance?.MarkDirty();
        return true;
    }

    private void BuildGoblinChest(Transform hutRoot)
    {
        if (hutRoot == null) return;
        var existing = hutRoot.Find("GoblinChest");
        if (existing != null) return;

        var chest = new GameObject("GoblinChest");
        chest.transform.SetParent(hutRoot, false);
        chest.transform.localRotation = Quaternion.identity;
        chest.transform.localPosition = Vector3.zero;

        const float chestBaseHalf = 0.225f;
        float topY = float.NaN;
        var floor = hutRoot.Find("BuildingPart_Floor");
        if (floor != null)
        {
            float floorTopLocal = floor.localPosition.y + floor.localScale.y * 0.5f;
            topY = floorTopLocal;
        }
        else
        {
            Vector3 groundSpot = hutRoot.TransformPoint(new Vector3(-0.85f, 0f, 0.95f));
            float groundY = NavGrid.Instance != null ? NavGrid.Instance.SampleGroundY(groundSpot) : hutRoot.position.y;
            topY = hutRoot.InverseTransformPoint(new Vector3(groundSpot.x, groundY, groundSpot.z)).y;
        }

        chest.transform.localPosition = new Vector3(-0.85f, topY + chestBaseHalf, 0.95f);

        var baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseCube.name = "ChestBase";
        baseCube.transform.SetParent(chest.transform, false);
        baseCube.transform.localPosition = new Vector3(0f, 0f, 0f);
        baseCube.transform.localScale = new Vector3(0.8f, 0.4f, 0.45f);
        baseCube.GetComponent<MeshRenderer>().material.color = new Color(0.55f, 0.35f, 0.18f);

        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "ChestLid";
        lid.transform.SetParent(chest.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.26f, 0f);
        lid.transform.localScale = new Vector3(0.8f, 0.12f, 0.45f);
        lid.GetComponent<MeshRenderer>().material.color = new Color(0.45f, 0.28f, 0.14f);

        var metalBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        metalBand.name = "ChestBand";
        metalBand.transform.SetParent(chest.transform, false);
        metalBand.transform.localPosition = new Vector3(0f, 0.02f, 0.2f);
        metalBand.transform.localScale = new Vector3(0.8f, 0.34f, 0.04f);
        metalBand.GetComponent<MeshRenderer>().material.color = new Color(0.75f, 0.65f, 0.4f);

        var collider = chest.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.8f, 0.5f, 0.45f);
        collider.center = new Vector3(0f, 0.11f, 0f);
    }

    private void CompleteBlueprint(BlueprintState bp, BuildingDefinition def)
    {
        if (bp.IsMansion)
        {
            MapBuilder.BuildRichManMansion(_worldRoot.transform, bp.Position, 1f, Quaternion.Euler(0f, bp.Rotation, 0f));
        }
        else if (bp.IsEssential)
        {
            RebuildEssentialBuilding(bp);
        }
else if (!string.IsNullOrEmpty(bp.StructureId))
        {
            SpawnStructurePart(bp);
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
        if (bp.IsMansion)
        {
            QuestManager.Instance?.AddProgress(_mansionQuestTarget, 1);
        }
        DestroyBlueprintLabel(bp);
        if (bp.Entity != null)
            Destroy(bp.Entity);
        _blueprints.Remove(bp);
        NavGrid.Instance?.MarkDirty();
    }

    private void SpawnStructurePart(BlueprintState bp)
    {
        var root = new GameObject("StructurePart_" + bp.Type);
        root.transform.position = bp.Position;
        root.transform.rotation = Quaternion.Euler(0f, bp.Rotation, 0f);
        root.transform.SetParent(_worldRoot.transform);

        Color woodColor = ColorPalette.HouseWood;
        Color stoneColor = new Color(0.41f, 0.41f, 0.41f);

        if (bp.Type.StartsWith("Pagoda_"))
        {
            if (bp.Type == "Pagoda_Foundation")
                _pagodaPosition = bp.Position;
            BuildPagodaPart(root.transform, bp.Type);
        }
        else if (bp.Type.StartsWith("Church_"))
        {
            if (bp.Type == "Church_Foundation")
                _churchPosition = bp.Position;
            BuildChurchPart(root.transform, bp.Type);
        }
        else if (bp.Type.StartsWith("Shrine_"))
        {
            if (bp.Type == "Shrine_Foundation")
                _shrinePosition = bp.Position;
            BuildShrinePart(root.transform, bp.Type);
        }
        else switch (bp.Type)
        {
            case "Foundation":
                CreatePartCube(root.transform, Vector3.up * 0.25f, new Vector3(16f, 0.5f, 10f), stoneColor);
                break;
            case "Floor":
                CreatePartCube(root.transform, Vector3.up * 0.15f, new Vector3(16f, 0.3f, 10f), woodColor);
                break;
            case "Walls":
                CreatePartCube(root.transform, new Vector3(0, 2.5f, 4.85f), new Vector3(16f, 5f, 0.3f), woodColor);
                CreatePartCube(root.transform, new Vector3(0, 2.5f, -4.85f), new Vector3(16f, 5f, 0.3f), woodColor);
                CreatePartCube(root.transform, new Vector3(-7.85f, 2.5f, 0), new Vector3(0.3f, 5f, 10f), woodColor);
                CreatePartCube(root.transform, new Vector3(7.85f, 2.5f, 0), new Vector3(0.3f, 5f, 10f), woodColor);
                break;
            case "Roof":
                CreatePartCube(root.transform, new Vector3(0, 5.2f, 0), new Vector3(17f, 0.4f, 11f), stoneColor);
                break;
            case "Door":
                root.name = "StructurePart_Door";
                var doorPivot = new GameObject("Door");
                doorPivot.transform.SetParent(root.transform);
                doorPivot.transform.localPosition = new Vector3(-1.5f, 2f, 5.15f);
                doorPivot.transform.localRotation = Quaternion.identity;
                var doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                doorPanel.name = "DoorPanel";
                doorPanel.transform.SetParent(doorPivot.transform);
                doorPanel.transform.localPosition = new Vector3(1.5f, 0f, 0f);
                doorPanel.transform.localScale = new Vector3(3f, 4f, 0.3f);
                doorPanel.transform.localRotation = Quaternion.identity;
                doorPanel.GetComponent<MeshRenderer>().material.color = woodColor;
                doorPanel.AddComponent<BoxCollider>();
                break;
            case "Interior":
                CreatePartCube(root.transform, new Vector3(-4f, 1.5f, -3f), new Vector3(3f, 1.5f, 2f), woodColor);
                CreatePartCube(root.transform, new Vector3(4f, 1.5f, -3f), new Vector3(3f, 1.5f, 2f), woodColor);
                CreatePartCube(root.transform, new Vector3(0, 2f, -4f), new Vector3(6f, 2f, 0.5f), woodColor);
                break;

            // ═══════════════════════════════════════════════
            //  MANSION MEGA STRUCTURE PARTS
            // ═══════════════════════════════════════════════
            case "Mansion_Foundation":
                CreatePartCube(root.transform, new Vector3(0, 0.11f, 0), new Vector3(25f, 0.22f, 17f), stoneColor);
                break;
            case "Mansion_PorchSlab":
                CreatePartCube(root.transform, new Vector3(0, 0.03f, 0), new Vector3(4f, 0.06f, 4f), stoneColor);
                break;
            case "Mansion_BackPatio":
                CreatePartCube(root.transform, new Vector3(0, 0.03f, 0), new Vector3(4f, 0.06f, 4f), stoneColor);
                break;
            case "Mansion_1F_Floor":
                CreatePartCube(root.transform, new Vector3(0, 0.25f, 0), new Vector3(24f, 0.5f, 16f), woodColor);
                break;
            case "Mansion_1F_ExteriorWalls":
                {
                    float wallH = 5f;
                    float hw = 12f;
                    float hd = 8f;
                    CreatePartCube(root.transform, new Vector3(-7f, wallH * 0.5f, hd), new Vector3(10f, wallH, 0.35f), woodColor);
                    CreatePartCube(root.transform, new Vector3(7f, wallH * 0.5f, hd), new Vector3(10f, wallH, 0.35f), woodColor);
                    CreatePartCube(root.transform, new Vector3(0, wallH * 0.5f, -hd), new Vector3(24f, wallH, 0.35f), woodColor);
                    CreatePartCube(root.transform, new Vector3(-hw, wallH * 0.5f, 0), new Vector3(0.35f, wallH, 16f), woodColor);
                    CreatePartCube(root.transform, new Vector3(hw, wallH * 0.5f, 0), new Vector3(0.35f, wallH, 16f), woodColor);
                }
                break;
            case "Mansion_1F_InteriorWalls":
                {
                    float wallH = 2.6f;
                    CreatePartCube(root.transform, new Vector3(0, wallH * 0.5f, 1f), new Vector3(0.25f, wallH, 6f), woodColor);
                    CreatePartCube(root.transform, new Vector3(5f, wallH * 0.5f, 1f), new Vector3(0.25f, wallH, 6f), woodColor);
                    CreatePartCube(root.transform, new Vector3(-5f, wallH * 0.5f, -2f), new Vector3(10f, wallH, 0.25f), woodColor);
                    CreatePartCube(root.transform, new Vector3(8f, wallH * 0.5f, -1.5f), new Vector3(6f, wallH, 0.25f), woodColor);
                }
                break;
            case "Mansion_FrontDoor":
                {
                    Color doorColor = new Color(0.5f, 0.3f, 0.15f);
                    CreatePartCube(root.transform, new Vector3(0, 3.75f, 0), new Vector3(4f, 0.5f, 0.35f), doorColor);
                    CreatePartCubeRotated(root.transform, new Vector3(-1.55f, 2f, 0.21f), new Vector3(1f, 4f, 0.35f), doorColor, Quaternion.Euler(0f, 25f, 0f));
                    CreatePartCubeRotated(root.transform, new Vector3(1.55f, 2f, 0.21f), new Vector3(1f, 4f, 0.35f), doorColor, Quaternion.Euler(0f, -25f, 0f));
                }
                break;
            case "Mansion_LivingRoom":
                {
                    Color furnColor = new Color(0.55f, 0.35f, 0.16f);
                    CreatePartCube(root.transform, new Vector3(-2f, 0f, 1f), new Vector3(4f, 1f, 1.5f), furnColor);
                    CreatePartCube(root.transform, new Vector3(-2f, 0f, -1f), new Vector3(4f, 1f, 1.5f), furnColor);
                    CreatePartCube(root.transform, new Vector3(-2f, -0.25f, 0), new Vector3(2f, 0.5f, 2f), furnColor);
                    CreatePartCube(root.transform, new Vector3(2f, 0f, 0), new Vector3(2f, 1f, 2f), new Color(0.65f, 0.45f, 0.22f));
                    CreatePartCube(root.transform, new Vector3(-2f, -0.25f, -2.5f), new Vector3(3f, 0.5f, 0.3f), furnColor);
                }
                break;
            case "Mansion_Kitchen":
                {
                    Color counterColor = new Color(0.7f, 0.5f, 0.3f);
                    CreatePartCube(root.transform, new Vector3(-3f, 0.5f, 2f), new Vector3(4f, 2f, 1f), counterColor);
                    CreatePartCube(root.transform, new Vector3(-3f, 0.5f, -1.5f), new Vector3(4f, 2f, 1f), counterColor);
                    CreatePartCube(root.transform, new Vector3(3f, 0.5f, 0), new Vector3(2f, 2f, 3f), counterColor);
                    CreatePartCube(root.transform, new Vector3(0, 0f, 0), new Vector3(3f, 1f, 2f), new Color(0.65f, 0.45f, 0.25f));
                }
                break;
            case "Mansion_DiningRoom":
                {
                    Color tableColor = new Color(0.6f, 0.4f, 0.2f);
                    CreatePartCube(root.transform, new Vector3(0, -0.35f, 0), new Vector3(4f, 0.3f, 2.5f), tableColor);
                    CreatePartCube(root.transform, new Vector3(-1.5f, 0f, 1.5f), new Vector3(0.8f, 1f, 0.8f), tableColor);
                    CreatePartCube(root.transform, new Vector3(1.5f, 0f, 1.5f), new Vector3(0.8f, 1f, 0.8f), tableColor);
                    CreatePartCube(root.transform, new Vector3(-1.5f, 0f, -1.5f), new Vector3(0.8f, 1f, 0.8f), tableColor);
                    CreatePartCube(root.transform, new Vector3(1.5f, 0f, -1.5f), new Vector3(0.8f, 1f, 0.8f), tableColor);
                }
                break;
            case "Mansion_Bathroom1F":
                {
                    Color tileColor = new Color(0.85f, 0.85f, 0.85f);
                    CreatePartCube(root.transform, new Vector3(0, -0.1f, -1f), new Vector3(2f, 0.8f, 1.5f), tileColor);
                    CreatePartCube(root.transform, new Vector3(1f, -0.25f, 0.5f), new Vector3(1f, 0.5f, 1f), tileColor);
                }
                break;
            case "Mansion_2F_Floor":
                CreatePartCube(root.transform, new Vector3(0, 0.15f, 0), new Vector3(24f, 0.3f, 16f), woodColor);
                break;
            case "Mansion_2F_ExteriorWalls":
                {
                    float wallH = 4f;
                    float hw = 12f;
                    float hd = 8f;
                    CreatePartCube(root.transform, new Vector3(0, wallH * 0.5f, hd), new Vector3(24f, wallH, 0.35f), woodColor);
                    CreatePartCube(root.transform, new Vector3(0, wallH * 0.5f, -hd), new Vector3(24f, wallH, 0.35f), woodColor);
                    CreatePartCube(root.transform, new Vector3(-hw, wallH * 0.5f, 0), new Vector3(0.35f, wallH, 16f), woodColor);
                    CreatePartCube(root.transform, new Vector3(hw, wallH * 0.5f, 0), new Vector3(0.35f, wallH, 16f), woodColor);
                }
                break;
            case "Mansion_2F_InteriorWalls":
                {
                    float wallH = 3.5f;
                    CreatePartCube(root.transform, new Vector3(0, wallH * 0.5f, 1f), new Vector3(0.25f, wallH, 6f), woodColor);
                    CreatePartCube(root.transform, new Vector3(5f, wallH * 0.5f, 1f), new Vector3(0.25f, wallH, 6f), woodColor);
                    CreatePartCube(root.transform, new Vector3(-5f, wallH * 0.5f, -2f), new Vector3(10f, wallH, 0.25f), woodColor);
                    CreatePartCube(root.transform, new Vector3(8f, wallH * 0.5f, -1.5f), new Vector3(6f, wallH, 0.25f), woodColor);
                }
                break;
            case "Mansion_Staircase":
                {
                    for (int s = 0; s < 6; s++)
                    {
                        float y = (s + 1) * 0.4416f - 0.1f;
                        float z = -s * 0.4f;
                        CreatePartCube(root.transform, new Vector3(0, y, z), new Vector3(2.5f, 0.2f, 0.55f), woodColor);
                    }
                    CreatePartCube(root.transform, new Vector3(1.3f, 1.5f, -2.1f), new Vector3(0.2f, 2.8f, 0.2f), woodColor);
                    CreatePartCube(root.transform, new Vector3(-1.3f, 1.5f, -2.1f), new Vector3(0.2f, 2.8f, 0.2f), woodColor);
                }
                break;
            case "Mansion_MasterBedroom":
                {
                    Color bedColor = new Color(0.52f, 0.33f, 0.18f);
                    CreatePartCube(root.transform, new Vector3(-2f, 0f, 0), new Vector3(3f, 1f, 2.5f), bedColor);
                    CreatePartCube(root.transform, new Vector3(-2f, 0.7f, 0), new Vector3(2.8f, 0.4f, 2.3f), new Color(0.9f, 0.9f, 0.95f));
                    CreatePartCube(root.transform, new Vector3(-3.4f, 0.1f, 0), new Vector3(0.3f, 1.2f, 2.5f), bedColor);
                    CreatePartCube(root.transform, new Vector3(2f, 0f, -2f), new Vector3(2f, 1f, 1f), new Color(0.58f, 0.38f, 0.18f));
                    CreatePartCube(root.transform, new Vector3(2f, 1f, -2f), new Vector3(2f, 1f, 0.3f), new Color(0.58f, 0.38f, 0.18f));
                }
                break;
            case "Mansion_Bedroom2":
                {
                    Color bedColor2 = new Color(0.58f, 0.38f, 0.2f);
                    CreatePartCube(root.transform, new Vector3(-2f, 0f, 0), new Vector3(2.5f, 1f, 2f), bedColor2);
                    CreatePartCube(root.transform, new Vector3(-2f, 0.65f, 0), new Vector3(2.3f, 0.3f, 1.8f), new Color(0.85f, 0.85f, 0.9f));
                    CreatePartCube(root.transform, new Vector3(2f, 0f, -1.5f), new Vector3(1.5f, 1f, 0.8f), bedColor2);
                    CreatePartCube(root.transform, new Vector3(2f, 0.65f, -1.5f), new Vector3(1.5f, 0.3f, 0.8f), new Color(0.9f, 0.9f, 0.95f));
                }
                break;
            case "Mansion_Bedroom3":
                {
                    Color bedColor3 = new Color(0.6f, 0.42f, 0.22f);
                    CreatePartCube(root.transform, new Vector3(-1.5f, 0f, 0), new Vector3(2.5f, 1f, 2f), bedColor3);
                    CreatePartCube(root.transform, new Vector3(-1.5f, 0.65f, 0), new Vector3(2.3f, 0.3f, 1.8f), new Color(0.88f, 0.88f, 0.92f));
                    CreatePartCube(root.transform, new Vector3(2f, 0.25f, 0), new Vector3(1.5f, 1.5f, 2f), new Color(0.55f, 0.38f, 0.2f));
                }
                break;
            case "Mansion_Bathroom2F":
                {
                    Color tileColor2 = new Color(0.85f, 0.85f, 0.85f);
                    CreatePartCube(root.transform, new Vector3(0, -0.1f, -1f), new Vector3(2f, 0.8f, 1.5f), tileColor2);
                    CreatePartCube(root.transform, new Vector3(0.5f, -0.25f, 0.5f), new Vector3(1f, 0.5f, 1f), tileColor2);
                }
                break;
            case "Mansion_HallwayDecor":
                {
                    CreatePartCube(root.transform, new Vector3(-2f, 0.25f, 0), new Vector3(1.5f, 1.5f, 0.4f), new Color(0.55f, 0.38f, 0.2f));
                    CreatePartCube(root.transform, new Vector3(1f, 0f, 0), new Vector3(1f, 1f, 1f), new Color(0.6f, 0.4f, 0.2f));
                    CreatePartCube(root.transform, new Vector3(1f, 0.9f, 0), new Vector3(0.8f, 0.8f, 0.3f), new Color(0.5f, 0.35f, 0.18f));
                }
                break;
            case "Mansion_MainRoof":
                {
                    float roofHalfD = 9f;
                    float roofRise = 3.5f;
                    float roofTilt = Mathf.Atan2(roofRise, roofHalfD) * Mathf.Rad2Deg;
                    float roofPanelLen = Mathf.Sqrt(roofHalfD * roofHalfD + roofRise * roofRise);
                    float roofPanelY = roofRise / 2f;
                    CreatePartCubeRotated(root.transform, new Vector3(0f, roofPanelY, roofHalfD / 2f),
                        new Vector3(26f, 0.5f, roofPanelLen), stoneColor, Quaternion.Euler(roofTilt, 0f, 0f));
                    CreatePartCubeRotated(root.transform, new Vector3(0f, roofPanelY, -roofHalfD / 2f),
                        new Vector3(26f, 0.5f, roofPanelLen), stoneColor, Quaternion.Euler(-roofTilt, 0f, 0f));
                    CreatePartCube(root.transform, new Vector3(0f, roofRise, 0f),
                        new Vector3(26.2f, 0.35f, 0.7f), new Color(0.45f, 0.42f, 0.38f));
                }
                break;
            case "Mansion_PorchRoof":
                CreatePartCube(root.transform, new Vector3(0, 0.15f, 0), new Vector3(8f, 0.3f, 5f), stoneColor);
                break;
            case "Mansion_Balcony":
                {
                    CreatePartCube(root.transform, new Vector3(0, 0.15f, 0), new Vector3(6f, 0.3f, 3f), woodColor);
                    CreatePartCube(root.transform, new Vector3(-2.85f, 0.5f, 0), new Vector3(0.15f, 1f, 3f), woodColor);
                    CreatePartCube(root.transform, new Vector3(0, 0.5f, 1.35f), new Vector3(6f, 1f, 0.15f), woodColor);
                    CreatePartCube(root.transform, new Vector3(2.85f, 0.5f, 0), new Vector3(0.15f, 1f, 3f), woodColor);
                }
                break;
            case "Mansion_GardenPath":
                CreatePartCube(root.transform, new Vector3(0, 0.03f, 0), new Vector3(2f, 0.06f, 10f), stoneColor);
                break;
            case "Mansion_Fence":
                {
                    float fenceH = 1.8f;
                    Color fenceColor = new Color(0.69f, 0.51f, 0.25f);
                    CreatePartCube(root.transform, new Vector3(-7f, fenceH * 0.5f, 10f), new Vector3(10f, fenceH, 0.15f), fenceColor);
                    CreatePartCube(root.transform, new Vector3(7f, fenceH * 0.5f, 10f), new Vector3(10f, fenceH, 0.15f), fenceColor);
                    CreatePartCube(root.transform, new Vector3(0, fenceH * 0.5f, -10f), new Vector3(28f, fenceH, 0.15f), fenceColor);
                    CreatePartCube(root.transform, new Vector3(-14f, fenceH * 0.5f, 0), new Vector3(0.15f, fenceH, 20f), fenceColor);
                    CreatePartCube(root.transform, new Vector3(14f, fenceH * 0.5f, 0), new Vector3(0.15f, fenceH, 20f), fenceColor);
                }
                break;
        }

        _buildings.Add(new BuildingState
        {
            Entity = root,
            Type = "structure_part_" + bp.Type,
            Position = bp.Position,
            Rotation = bp.Rotation,
            CurrentHealth = 100,
            MaxHealth = 100
        });
    }

    private GameObject CreatePartCube(Transform parent, Vector3 localPos, Vector3 scale, Color color)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = scale;
        cube.transform.localRotation = Quaternion.identity;
        cube.GetComponent<MeshRenderer>().material.color = color;
        if (cube.GetComponent<BoxCollider>() == null) cube.AddComponent<BoxCollider>();
        return cube;
    }

    private void CreatePartCubeRotated(Transform parent, Vector3 localPos, Vector3 scale, Color color, Quaternion rotation)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = scale;
        cube.transform.localRotation = rotation;
        cube.GetComponent<MeshRenderer>().material.color = color;
        if (cube.GetComponent<BoxCollider>() == null) cube.AddComponent<BoxCollider>();
    }

    // ── Structure-part geometry helpers ───────────────────────────────────
    // The holy places are hand-authored out of raw cubes. Every gap ever found
    // in them traced back to one of two authoring mistakes: a block placed by
    // its CENTRE, so its bottom face had to be re-derived by hand and missed the
    // support underneath by a fraction of a metre, or a roof panel written as a
    // tilt SIGN, so an inverted pitch was invisible in the source. These helpers
    // take the numbers a human can actually reason about instead - a bottom
    // face, or the two ends of a slope - and do the arithmetic once, here.
    // Authoring convention for the rebuilt holy places: geometry is written in
    // SITE coordinates (y = 0 is the platform top) with every part root sitting
    // at the site origin, so the whole assembly is auditable in a single frame
    // and a support's top surface is always written next to what rests on it.
    // See THINKING.md 1hm for the derivations these replace.

    /// <summary>
    /// Block placed by its bottom face, so the gap to whatever supports it is
    /// the difference of two numbers written on the same line.
    /// </summary>
    private GameObject CreatePartBoxOn(Transform parent, Vector3 xz, float bottomY, Vector3 size, Color color)
    {
        return CreatePartCube(parent, new Vector3(xz.x, bottomY + size.y * 0.5f, xz.y), size, color);
    }

    private GameObject CreatePartBoxOn(Transform parent, float x, float bottomY, float z, Vector3 size, Color color)
    {
        return CreatePartCube(parent, new Vector3(x, bottomY + size.y * 0.5f, z), size, color);
    }

    /// <summary>
    /// Slab whose UNDERSIDE runs from a to b - the workhorse for roofs, ramps and
    /// stairs. <paramref name="across"/> is the slab's horizontal width
    /// direction: (1,0,0) for a panel that slopes along Z, (0,0,1) for one that
    /// slopes along X. a and b must differ only in the vertical plane
    /// perpendicular to it. The slab is <paramref name="thickness"/> deep
    /// measured perpendicular to the slope, so handing it the eave's underside
    /// and the ridge's underside makes it touch both by construction - there is
    /// no tilt sign left to get backwards.
    /// </summary>
    private GameObject CreatePartPanelBetween(Transform parent, Vector3 a, Vector3 b, float halfWidth, float thickness, Vector3 across, Color color)
    {
        Vector3 slope = (b - a).normalized;
        Vector3 width = Vector3.ProjectOnPlane(across, slope).normalized;
        var rot = Quaternion.LookRotation(slope, width);
        Vector3 mid = (a + b) * 0.5f + (rot * Vector3.up) * (thickness * 0.5f);
        return CreatePartCubeRotated(parent, mid, new Vector3(halfWidth * 2f, thickness, Vector3.Distance(a, b)), color, rot);
    }

    /// <summary>
    /// Corbel-stepped gable infill in the plane x = xCentre, closing the triangle
    /// between a wall top and a roof whose underside falls from ridgeY at z = 0
    /// to eaveY at |z| = halfSpan. Emits <paramref name="courses"/> uncentred
    /// bands per side, mirrored about z = 0. A band's top reaches the roof
    /// underside at that band's INNER edge, which is the highest point of the
    /// roof anywhere over the band, so the union of bands has no gap; the extra
    /// 6 cm then drives each band up into the roof slab instead of leaving a slit
    /// at its outer edge. A centred step cannot do this - its inner edge is
    /// z = 0, so it would have to be as tall as the ridge.
    /// </summary>
    private void CreatePartGableSteps(Transform parent, float xCentre, float thickness, float wallTop, float eaveY, float ridgeY, float halfSpan, int courses, Color color)
    {
        float rise = ridgeY - eaveY;
        float band = halfSpan / courses;
        for (int k = 0; k < courses; k++)
        {
            float outer = halfSpan - band * k;
            float inner = outer - band;
            float top = ridgeY - rise * (inner / halfSpan) + 0.06f;
            for (int sz = -1; sz <= 1; sz += 2)
                CreatePartBoxOn(parent, xCentre, wallTop, sz * (inner + outer) * 0.5f, new Vector3(thickness, top - wallTop, outer - inner), color);
        }
    }

    private void BuildPagodaPart(Transform root, string partType)
    {
        Color woodC = new Color(0.6f, 0.28f, 0.14f);
        Color wallC = new Color(0.4f, 0.2f, 0.09f);
        Color pillarC = new Color(0.55f, 0.12f, 0.1f);
        Color roofC = new Color(0.55f, 0.16f, 0.12f);
        Color ridgeC = new Color(0.36f, 0.11f, 0.08f);
        Color stoneC = new Color(0.42f, 0.42f, 0.42f);
        Color lightStoneC = new Color(0.56f, 0.54f, 0.51f);
        Color doorC = new Color(0.62f, 0.1f, 0.08f);
        Color goldC = new Color(1f, 0.84f, 0.2f);
        Color glassC = new Color(0.13f, 0.13f, 0.17f);

        switch (partType)
        {
            case "Pagoda_Foundation":
                CreatePartCube(root, new Vector3(0, 0.05f, 0), new Vector3(14f, 0.5f, 14f), stoneC);
                CreatePartCube(root, new Vector3(0, 0.34f, 0), new Vector3(12.6f, 0.22f, 12.6f), lightStoneC);
                CreatePartCube(root, new Vector3(6.6f, 0.55f, 6.6f), new Vector3(0.7f, 0.14f, 0.7f), stoneC);
                CreatePartCube(root, new Vector3(-6.6f, 0.55f, 6.6f), new Vector3(0.7f, 0.14f, 0.7f), stoneC);
                CreatePartCube(root, new Vector3(6.6f, 0.55f, -6.6f), new Vector3(0.7f, 0.14f, 0.7f), stoneC);
                CreatePartCube(root, new Vector3(-6.6f, 0.55f, -6.6f), new Vector3(0.7f, 0.14f, 0.7f), stoneC);
                CreatePartCube(root, new Vector3(0, 0.16f, -6.85f), new Vector3(3f, 0.16f, 1f), stoneC);
                CreatePartCube(root, new Vector3(0, 0.38f, -6.55f), new Vector3(3f, 0.16f, 1f), stoneC);
                CreatePartCube(root, new Vector3(0, 0.6f, -6.25f), new Vector3(3f, 0.16f, 1f), lightStoneC);
                break;

            case "Pagoda_BaseFloor":
                CreatePartCube(root, new Vector3(0, 0f, 0), new Vector3(12f, 0.3f, 12f), woodC);
                for (int rp = -4; rp <= 4; rp += 2)
                {
                    CreatePartCube(root, new Vector3(rp, 0.4f, 5.95f), new Vector3(0.12f, 0.5f, 0.12f), woodC);
                    if (rp != 0)
                        CreatePartCube(root, new Vector3(rp, 0.4f, -5.95f), new Vector3(0.12f, 0.5f, 0.12f), woodC);
                    CreatePartCube(root, new Vector3(5.95f, 0.4f, rp), new Vector3(0.12f, 0.5f, 0.12f), woodC);
                    CreatePartCube(root, new Vector3(-5.95f, 0.4f, rp), new Vector3(0.12f, 0.5f, 0.12f), woodC);
                }
                CreatePartCube(root, new Vector3(0, 0.62f, 6f), new Vector3(12f, 0.08f, 0.09f), woodC);
                CreatePartCube(root, new Vector3(-3.9f, 0.62f, -6f), new Vector3(4.2f, 0.08f, 0.09f), woodC);
                CreatePartCube(root, new Vector3(3.9f, 0.62f, -6f), new Vector3(4.2f, 0.08f, 0.09f), woodC);
                CreatePartCube(root, new Vector3(6f, 0.62f, 0), new Vector3(0.09f, 0.08f, 12f), woodC);
                CreatePartCube(root, new Vector3(-6f, 0.62f, 0), new Vector3(0.09f, 0.08f, 12f), woodC);
                for (int ux = -1; ux <= 1; ux += 2)
                {
                    CreatePartCube(root, new Vector3(ux * 2.2f, 0.2f, -5.5f), new Vector3(0.42f, 0.1f, 0.42f), stoneC);
                    CreatePartCube(root, new Vector3(ux * 2.2f, 0.39f, -5.5f), new Vector3(0.34f, 0.26f, 0.34f), lightStoneC);
                    CreatePartCube(root, new Vector3(ux * 2.2f, 0.56f, -5.5f), new Vector3(0.4f, 0.08f, 0.4f), goldC);
                }
                CreatePartCube(root, new Vector3(5.95f, 0.4f, 5.95f), new Vector3(0.16f, 0.5f, 0.16f), woodC);
                CreatePartCube(root, new Vector3(-5.95f, 0.4f, 5.95f), new Vector3(0.16f, 0.5f, 0.16f), woodC);
                CreatePartCube(root, new Vector3(5.95f, 0.4f, -5.95f), new Vector3(0.16f, 0.5f, 0.16f), woodC);
                CreatePartCube(root, new Vector3(-5.95f, 0.4f, -5.95f), new Vector3(0.16f, 0.5f, 0.16f), woodC);
                CreatePartCube(root, new Vector3(0, 0.32f, -2f), new Vector3(1.4f, 0.3f, 1f), stoneC);
                CreatePartCube(root, new Vector3(0, 0.55f, -2f), new Vector3(0.5f, 0.16f, 0.4f), lightStoneC);
                CreatePartCube(root, new Vector3(0, 0.72f, -2f), new Vector3(0.28f, 0.2f, 0.28f), goldC);
                CreatePartCube(root, new Vector3(0, 0.9f, -2f), new Vector3(0.16f, 0.16f, 0.16f), goldC);
                CreatePartCube(root, new Vector3(0, 0.145f, -4f), new Vector3(11.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, -2f), new Vector3(11.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 0f), new Vector3(11.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 2f), new Vector3(11.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 4f), new Vector3(11.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));

                // seated Buddha statue at the back, facing the door (face + hands on the -Z side)
                CreatePartCube(root, new Vector3(0, 0.15f, 1.5f), new Vector3(1.7f, 0.2f, 1.7f), stoneC);
                CreatePartCube(root, new Vector3(0, 0.31f, 1.5f), new Vector3(1.45f, 0.12f, 1.45f), goldC);
                CreatePartCube(root, new Vector3(0, 0.43f, 1.5f), new Vector3(1.18f, 0.1f, 1.18f), goldC);
                CreatePartCube(root, new Vector3(0, 0.9f, 1.5f), new Vector3(0.72f, 0.85f, 0.62f), goldC);
                CreatePartCube(root, new Vector3(0, 0.58f, 1.36f), new Vector3(0.68f, 0.2f, 0.2f), goldC);
                CreatePartCube(root, new Vector3(0, 1.24f, 1.5f), new Vector3(0.5f, 0.42f, 0.42f), goldC);
                CreatePartCube(root, new Vector3(0, 1.06f, 1.33f), new Vector3(0.52f, 0.16f, 0.16f), goldC);
                CreatePartCube(root, new Vector3(0.26f, 1.06f, 1.4f), new Vector3(0.14f, 0.16f, 0.12f), goldC);
                CreatePartCube(root, new Vector3(-0.26f, 1.06f, 1.4f), new Vector3(0.14f, 0.16f, 0.12f), goldC);
                CreatePartCube(root, new Vector3(0, 1.52f, 1.5f), new Vector3(0.44f, 0.42f, 0.44f), goldC);
                CreatePartCube(root, new Vector3(0, 1.77f, 1.5f), new Vector3(0.18f, 0.14f, 0.18f), goldC);
                CreatePartCube(root, new Vector3(0, 1.85f, 1.5f), new Vector3(0.09f, 0.06f, 0.09f), goldC);
                CreatePartCube(root, new Vector3(0, 1.52f, 1.68f), new Vector3(0.03f, 0.62f, 0.62f), goldC);
                // face features (toward the door)
                CreatePartCube(root, new Vector3(-0.1f, 1.54f, 1.27f), new Vector3(0.1f, 0.07f, 0.04f), doorC);
                CreatePartCube(root, new Vector3(0.1f, 1.54f, 1.27f), new Vector3(0.1f, 0.07f, 0.04f), doorC);
                CreatePartCube(root, new Vector3(0, 1.6f, 1.27f), new Vector3(0.26f, 0.05f, 0.04f), doorC);
                CreatePartCube(root, new Vector3(0, 1.48f, 1.29f), new Vector3(0.12f, 0.03f, 0.03f), doorC);
                // robe folds draped over the pedestal
                CreatePartCube(root, new Vector3(0, 0.74f, 1.3f), new Vector3(0.6f, 0.26f, 0.1f), goldC);
                CreatePartCube(root, new Vector3(0, 0.68f, 1.27f), new Vector3(0.56f, 0.12f, 0.08f), doorC);

                // incense table + censer in front of the statue
                CreatePartCube(root, new Vector3(0, 0.55f, 0.5f), new Vector3(1.3f, 0.5f, 0.6f), woodC);
                CreatePartCube(root, new Vector3(0, 0.84f, 0.5f), new Vector3(1.4f, 0.08f, 0.66f), goldC);
                CreatePartCube(root, new Vector3(0, 1.02f, 0.5f), new Vector3(0.4f, 0.3f, 0.4f), goldC);
                CreatePartCube(root, new Vector3(0, 1.2f, 0.5f), new Vector3(0.18f, 0.08f, 0.18f), doorC);
                break;

            case "Pagoda_BaseWalls":
                {
                    float half = 5.5f;
                    // pillars around the base (world 0.9..4.3); the -Z face is the open entrance (no center pillar)
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, 0.05f, sz * half), new Vector3(0.5f, 3.4f, 0.5f), pillarC);
                        CreatePartCube(root, new Vector3(sx * half, 0.05f, 0), new Vector3(0.5f, 3.4f, 0.5f), pillarC);
                    }
                    CreatePartCube(root, new Vector3(0, 0.05f, half), new Vector3(0.5f, 3.4f, 0.5f), pillarC);

                    // lower wall panels (world 0.9..2.4) — raised to meet the window sills
                    CreatePartCube(root, new Vector3(0, -0.9f, half), new Vector3(10.5f, 1.5f, 0.25f), wallC);
                    CreatePartCube(root, new Vector3(half, -0.9f, 0), new Vector3(0.25f, 1.5f, 10.5f), wallC);
                    CreatePartCube(root, new Vector3(-half, -0.9f, 0), new Vector3(0.25f, 1.5f, 10.5f), wallC);
                    CreatePartCube(root, new Vector3(-3.425f, -0.9f, -half), new Vector3(3.65f, 1.5f, 0.25f), wallC);
                    CreatePartCube(root, new Vector3(3.425f, -0.9f, -half), new Vector3(3.65f, 1.5f, 0.25f), wallC);

                    // window faces (+Z, +X, -X): sill, glass, jambs, lintel, fills, top beam
                    Vector3[] winCenters = { new Vector3(0, 0.55f, half), new Vector3(half, 0.55f, 0), new Vector3(-half, 0.55f, 0) };
                    Vector3[] winTans = { new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, 1) };
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 c = winCenters[i];
                        Vector3 t = winTans[i];
                        bool axisX = i == 0;
                        CreatePartCube(root, c + Vector3.down * 0.6f, new Vector3(axisX ? 10.5f : 0.2f, 0.2f, axisX ? 0.2f : 10.5f), ridgeC);
                        CreatePartCube(root, c, new Vector3(axisX ? 1.4f : 0.06f, 1.2f, axisX ? 0.06f : 1.4f), glassC);
                        CreatePartCube(root, c - t * 0.8f, new Vector3(axisX ? 0.18f : 0.2f, 1.2f, axisX ? 0.2f : 0.18f), pillarC);
                        CreatePartCube(root, c + t * 0.8f, new Vector3(axisX ? 0.18f : 0.2f, 1.2f, axisX ? 0.2f : 0.18f), pillarC);
                        CreatePartCube(root, c + Vector3.up * 0.6f, new Vector3(axisX ? 1.8f : 0.2f, 0.18f, axisX ? 0.2f : 1.8f), ridgeC);
                        CreatePartCube(root, c - t * 3.075f, new Vector3(axisX ? 4.35f : 0.18f, 1.2f, axisX ? 0.18f : 4.35f), wallC);
                        CreatePartCube(root, c + t * 3.075f, new Vector3(axisX ? 4.35f : 0.18f, 1.2f, axisX ? 0.18f : 4.35f), wallC);
                        CreatePartCube(root, c + Vector3.up * 0.95f, new Vector3(axisX ? 10.5f : 0.2f, 0.2f, axisX ? 0.2f : 10.5f), ridgeC);
                        CreatePartCube(root, c, new Vector3(0.09f, 1.22f, 0.09f), ridgeC);
                        CreatePartCube(root, c + Vector3.up * 0.4f, new Vector3(axisX ? 1.42f : 0.09f, 0.09f, axisX ? 0.09f : 1.42f), ridgeC);
                    }

                    // mid band ring (world 3.75..3.95) — closes lintel/fills up to the top beam
                    CreatePartCube(root, new Vector3(0, 1.3f, half), new Vector3(10.5f, 0.2f, 0.25f), wallC);
                    CreatePartCube(root, new Vector3(0, 1.3f, -half), new Vector3(10.5f, 0.2f, 0.25f), wallC);
                    CreatePartCube(root, new Vector3(half, 1.3f, 0), new Vector3(0.25f, 0.2f, 10.5f), wallC);
                    CreatePartCube(root, new Vector3(-half, 1.3f, 0), new Vector3(0.25f, 0.2f, 10.5f), wallC);

                    // -Z face: wide open entrance (no door) + upper fills + top beam
                    CreatePartCube(root, new Vector3(-3.425f, 0.55f, -half), new Vector3(3.65f, 1.2f, 0.18f), wallC);
                    CreatePartCube(root, new Vector3(3.425f, 0.55f, -half), new Vector3(3.65f, 1.2f, 0.18f), wallC);
                    CreatePartCube(root, new Vector3(0, 1.5f, -half), new Vector3(10.5f, 0.2f, 0.2f), ridgeC);
                    // entrance lintel (world 3.65..4.15) — leaves generous headroom below for the player
                    CreatePartCube(root, new Vector3(0, 1.35f, -half + 0.08f), new Vector3(3.6f, 0.5f, 0.2f), wallC);
                    var doorstep = CreatePartCube(root, new Vector3(0, -1.6f, -half + 0.04f), new Vector3(3.6f, 0.1f, 0.18f), stoneC);
                    foreach (var dc in doorstep.GetComponents<Collider>()) Destroy(dc);

                    // parapet ring under Roof1 (world ~4.14..5.48) — closes the wall-to-roof gap
                    CreatePartCube(root, new Vector3(0, 2.26f, half), new Vector3(10.5f, 1.34f, 0.3f), wallC);
                    CreatePartCube(root, new Vector3(0, 2.26f, -half), new Vector3(10.5f, 1.34f, 0.3f), wallC);
                    CreatePartCube(root, new Vector3(half, 2.26f, 0), new Vector3(0.3f, 1.34f, 10.5f), wallC);
                    CreatePartCube(root, new Vector3(-half, 2.26f, 0), new Vector3(0.3f, 1.34f, 10.5f), wallC);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, 2.26f, sz * half), new Vector3(0.5f, 1.34f, 0.5f), wallC);

                    // dougong brackets raised onto the parapet, just under the roof
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            CreatePartCube(root, new Vector3(sx * half, 2.62f, sz * half), new Vector3(0.55f, 0.2f, 0.55f), wallC);
                            CreatePartCube(root, new Vector3(sx * half, 2.72f, sz * half), new Vector3(0.58f, 0.1f, 0.58f), goldC);
                            CreatePartCube(root, new Vector3(sx * half, 2.84f, sz * half), new Vector3(0.6f, 0.16f, 0.6f), goldC);
                        }
                        CreatePartCube(root, new Vector3(0, 2.62f, sx * half), new Vector3(0.55f, 0.2f, 0.55f), wallC);
                        CreatePartCube(root, new Vector3(0, 2.72f, sx * half), new Vector3(0.58f, 0.1f, 0.58f), goldC);
                        CreatePartCube(root, new Vector3(0, 2.84f, sx * half), new Vector3(0.6f, 0.16f, 0.6f), goldC);
                        CreatePartCube(root, new Vector3(sx * half, 2.62f, 0), new Vector3(0.55f, 0.2f, 0.55f), wallC);
                        CreatePartCube(root, new Vector3(sx * half, 2.72f, 0), new Vector3(0.58f, 0.1f, 0.58f), goldC);
                        CreatePartCube(root, new Vector3(sx * half, 2.84f, 0), new Vector3(0.6f, 0.16f, 0.6f), goldC);
                    }

                    // hanging lanterns at front (-Z) corners
                    for (int lx = -1; lx <= 1; lx += 2)
                    {
                        CreatePartCube(root, new Vector3(lx * 4.2f, 1.25f, -5.9f), new Vector3(0.06f, 0.6f, 0.06f), goldC);
                        CreatePartCube(root, new Vector3(lx * 4.2f, 0.98f, -5.9f), new Vector3(0.5f, 0.09f, 0.5f), goldC);
                        CreatePartCube(root, new Vector3(lx * 4.2f, 0.7f, -5.9f), new Vector3(0.42f, 0.48f, 0.42f), doorC);
                        CreatePartCube(root, new Vector3(lx * 4.2f, 0.46f, -5.9f), new Vector3(0.3f, 0.07f, 0.3f), goldC);
                    }
                }
                break;

            case "Pagoda_Roof1":
                CreatePartCubeRotated(root, new Vector3(0, 0.425f, 4.2f), new Vector3(17.2f, 0.55f, 9.4f), roofC, Quaternion.Euler(14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(0, 0.425f, -4.2f), new Vector3(17.2f, 0.55f, 9.4f), roofC, Quaternion.Euler(-14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(4.2f, 0.425f, 0), new Vector3(9.4f, 0.55f, 17.2f), roofC, Quaternion.Euler(0f, 0f, -14f));
                CreatePartCubeRotated(root, new Vector3(-4.2f, 0.425f, 0), new Vector3(9.4f, 0.55f, 17.2f), roofC, Quaternion.Euler(0f, 0f, 14f));
                CreatePartCube(root, new Vector3(0, -0.62f, 8.55f), new Vector3(17.2f, 0.22f, 0.18f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.62f, -8.55f), new Vector3(17.2f, 0.22f, 0.18f), ridgeC);
                CreatePartCube(root, new Vector3(8.55f, -0.62f, 0), new Vector3(0.18f, 0.22f, 17.2f), ridgeC);
                CreatePartCube(root, new Vector3(-8.55f, -0.62f, 0), new Vector3(0.18f, 0.22f, 17.2f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.76f, 8.62f), new Vector3(17.2f, 0.05f, 0.12f), goldC);
                CreatePartCube(root, new Vector3(0, -0.76f, -8.62f), new Vector3(17.2f, 0.05f, 0.12f), goldC);
                CreatePartCube(root, new Vector3(8.62f, -0.76f, 0), new Vector3(0.12f, 0.05f, 17.2f), goldC);
                CreatePartCube(root, new Vector3(-8.62f, -0.76f, 0), new Vector3(0.12f, 0.05f, 17.2f), goldC);
                for (int eb = -4; eb <= 4; eb += 4)
                {
                    CreatePartCube(root, new Vector3(eb, -0.66f, 8.68f), new Vector3(0.14f, 0.14f, 0.14f), goldC);
                    CreatePartCube(root, new Vector3(eb, -0.66f, -8.68f), new Vector3(0.14f, 0.14f, 0.14f), goldC);
                    CreatePartCube(root, new Vector3(8.68f, -0.66f, eb), new Vector3(0.14f, 0.14f, 0.14f), goldC);
                    CreatePartCube(root, new Vector3(-8.68f, -0.66f, eb), new Vector3(0.14f, 0.14f, 0.14f), goldC);
                }
                CreatePartCube(root, new Vector3(0, 1.35f, 0), new Vector3(7f, 0.5f, 7f), roofC);
                CreatePartCube(root, new Vector3(0, 1.75f, 0), new Vector3(0.4f, 0.4f, 0.4f), goldC);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        CreatePartCubeRotated(root, new Vector3(sx * 8.35f, -0.52f, sz * 8.35f), new Vector3(1.2f, 0.12f, 1.2f), ridgeC, Quaternion.Euler(0f, 45f, 0f));
                        CreatePartCubeRotated(root, new Vector3(sx * 8.5f, -0.42f, sz * 8.5f), new Vector3(0.5f, 0.5f, 0.5f), ridgeC, Quaternion.Euler(45f, 45f, 0f));
                    }
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartCubeRotated(root, new Vector3(sx * 5.935f, 0.005f, sz * 5.935f), new Vector3(0.16f, 0.16f, 7f), ridgeC, Quaternion.LookRotation(new Vector3(sx * 4.87f, -1.18f, sz * 4.87f)));
                CreatePartCube(root, new Vector3(0, -0.85f, -8.42f), new Vector3(0.06f, 0.3f, 0.06f), goldC);
                CreatePartCube(root, new Vector3(0, -1.15f, -8.42f), new Vector3(0.55f, 0.6f, 0.55f), goldC);
                CreatePartCube(root, new Vector3(0, -1.48f, -8.42f), new Vector3(0.28f, 0.14f, 0.28f), goldC);
                break;

            case "Pagoda_MidFloor":
                CreatePartCube(root, new Vector3(0, 0f, 0), new Vector3(8f, 0.3f, 8f), woodC);
                CreatePartCube(root, new Vector3(0, 0.145f, -2.5f), new Vector3(7.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, -1.25f), new Vector3(7.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 0f), new Vector3(7.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 1.25f), new Vector3(7.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 2.5f), new Vector3(7.9f, 0.012f, 0.03f), new Color(0.45f, 0.2f, 0.1f));
                break;

            case "Pagoda_MidWalls":
                {
                    float half = 3.6f;
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, -0.1f, sz * half), new Vector3(0.45f, 2.2f, 0.45f), pillarC);
                    CreatePartCube(root, new Vector3(0, -0.725f, half), new Vector3(6.8f, 0.95f, 0.12f), wallC);
                    CreatePartCube(root, new Vector3(0, -0.725f, -half), new Vector3(6.8f, 0.95f, 0.12f), wallC);
                    CreatePartCube(root, new Vector3(half, -0.725f, 0), new Vector3(0.12f, 0.95f, 6.8f), wallC);
                    CreatePartCube(root, new Vector3(-half, -0.725f, 0), new Vector3(0.12f, 0.95f, 6.8f), wallC);
                    CreatePartCube(root, new Vector3(0, -0.18f, half), new Vector3(6.8f, 0.14f, 0.14f), roofC);
                    CreatePartCube(root, new Vector3(0, -0.18f, -half), new Vector3(6.8f, 0.14f, 0.14f), roofC);
                    CreatePartCube(root, new Vector3(half, -0.18f, 0), new Vector3(0.14f, 0.14f, 6.8f), roofC);
                    CreatePartCube(root, new Vector3(-half, -0.18f, 0), new Vector3(0.14f, 0.14f, 6.8f), roofC);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            CreatePartCube(root, new Vector3(sx * half, 1f, sz * half), new Vector3(0.5f, 0.18f, 0.5f), wallC);
                            CreatePartCube(root, new Vector3(sx * half, 1.18f, sz * half), new Vector3(0.55f, 0.14f, 0.55f), goldC);
                        }
                    // parapet ring under Roof2 (world ~9.42..10.2)
                    CreatePartCube(root, new Vector3(0, 1.66f, half), new Vector3(6.8f, 0.78f, 0.14f), wallC);
                    CreatePartCube(root, new Vector3(0, 1.66f, -half), new Vector3(6.8f, 0.78f, 0.14f), wallC);
                    CreatePartCube(root, new Vector3(half, 1.66f, 0), new Vector3(0.14f, 0.78f, 6.8f), wallC);
                    CreatePartCube(root, new Vector3(-half, 1.66f, 0), new Vector3(0.14f, 0.78f, 6.8f), wallC);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, 1.66f, sz * half), new Vector3(0.45f, 0.78f, 0.45f), wallC);
                }
                break;

            case "Pagoda_Roof2":
                CreatePartCubeRotated(root, new Vector3(0, 0.25f, 3f), new Vector3(12f, 0.5f, 7f), roofC, Quaternion.Euler(14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(0, 0.25f, -3f), new Vector3(12f, 0.5f, 7f), roofC, Quaternion.Euler(-14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(3f, 0.25f, 0), new Vector3(7f, 0.5f, 12f), roofC, Quaternion.Euler(0f, 0f, -14f));
                CreatePartCubeRotated(root, new Vector3(-3f, 0.25f, 0), new Vector3(7f, 0.5f, 12f), roofC, Quaternion.Euler(0f, 0f, 14f));
                CreatePartCube(root, new Vector3(0, -0.48f, 5.9f), new Vector3(12f, 0.2f, 0.16f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.48f, -5.9f), new Vector3(12f, 0.2f, 0.16f), ridgeC);
                CreatePartCube(root, new Vector3(5.9f, -0.48f, 0), new Vector3(0.16f, 0.2f, 12f), ridgeC);
                CreatePartCube(root, new Vector3(-5.9f, -0.48f, 0), new Vector3(0.16f, 0.2f, 12f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.58f, 5.98f), new Vector3(12f, 0.05f, 0.1f), goldC);
                CreatePartCube(root, new Vector3(0, -0.58f, -5.98f), new Vector3(12f, 0.05f, 0.1f), goldC);
                CreatePartCube(root, new Vector3(5.98f, -0.58f, 0), new Vector3(0.1f, 0.05f, 12f), goldC);
                CreatePartCube(root, new Vector3(-5.98f, -0.58f, 0), new Vector3(0.1f, 0.05f, 12f), goldC);
                CreatePartCube(root, new Vector3(0, 0.85f, 0), new Vector3(4.5f, 0.45f, 4.5f), roofC);
                CreatePartCube(root, new Vector3(0, 1.15f, 0), new Vector3(0.35f, 0.35f, 0.35f), goldC);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        CreatePartCubeRotated(root, new Vector3(sx * 5.85f, -0.42f, sz * 5.85f), new Vector3(1f, 0.1f, 1f), ridgeC, Quaternion.Euler(0f, 45f, 0f));
                        CreatePartCubeRotated(root, new Vector3(sx * 6f, -0.32f, sz * 6f), new Vector3(0.42f, 0.42f, 0.42f), ridgeC, Quaternion.Euler(45f, 45f, 0f));
                    }
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartCubeRotated(root, new Vector3(sx * 4.32f, -0.07f, sz * 4.32f), new Vector3(0.14f, 0.14f, 6f), ridgeC, Quaternion.LookRotation(new Vector3(sx * 4.15f, -1f, sz * 4.15f)));
                break;

            case "Pagoda_TopFloor":
                CreatePartCube(root, new Vector3(0, 0f, 0), new Vector3(5.5f, 0.3f, 5.5f), woodC);
                CreatePartCube(root, new Vector3(0, 0.145f, -1.5f), new Vector3(5.4f, 0.012f, 0.025f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 0f), new Vector3(5.4f, 0.012f, 0.025f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 1.5f), new Vector3(5.4f, 0.012f, 0.025f), new Color(0.45f, 0.2f, 0.1f));
                break;

            case "Pagoda_TopWalls":
                {
                    float half = 2.45f;
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, -0.1f, sz * half), new Vector3(0.4f, 1.7f, 0.4f), pillarC);
                    CreatePartCube(root, new Vector3(0, -0.55f, half), new Vector3(4.6f, 0.8f, 0.1f), wallC);
                    CreatePartCube(root, new Vector3(0, -0.55f, -half), new Vector3(4.6f, 0.8f, 0.1f), wallC);
                    CreatePartCube(root, new Vector3(half, -0.55f, 0), new Vector3(0.1f, 0.8f, 4.6f), wallC);
                    CreatePartCube(root, new Vector3(-half, -0.55f, 0), new Vector3(0.1f, 0.8f, 4.6f), wallC);
                    CreatePartCube(root, new Vector3(0, -0.09f, half), new Vector3(4.6f, 0.12f, 0.12f), roofC);
                    CreatePartCube(root, new Vector3(0, -0.09f, -half), new Vector3(4.6f, 0.12f, 0.12f), roofC);
                    CreatePartCube(root, new Vector3(half, -0.09f, 0), new Vector3(0.12f, 0.12f, 4.6f), roofC);
                    CreatePartCube(root, new Vector3(-half, -0.09f, 0), new Vector3(0.12f, 0.12f, 4.6f), roofC);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            CreatePartCube(root, new Vector3(sx * half, 0.85f, sz * half), new Vector3(0.45f, 0.16f, 0.45f), wallC);
                            CreatePartCube(root, new Vector3(sx * half, 1f, sz * half), new Vector3(0.5f, 0.12f, 0.5f), goldC);
                        }
                    // parapet ring under Roof3 (world ~13.48..13.92)
                    CreatePartCube(root, new Vector3(0, 1.3f, half), new Vector3(4.6f, 0.44f, 0.12f), wallC);
                    CreatePartCube(root, new Vector3(0, 1.3f, -half), new Vector3(4.6f, 0.44f, 0.12f), wallC);
                    CreatePartCube(root, new Vector3(half, 1.3f, 0), new Vector3(0.12f, 0.44f, 4.6f), wallC);
                    CreatePartCube(root, new Vector3(-half, 1.3f, 0), new Vector3(0.12f, 0.44f, 4.6f), wallC);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, 1.3f, sz * half), new Vector3(0.4f, 0.44f, 0.4f), wallC);
                }
                break;

            case "Pagoda_Roof3":
                CreatePartCubeRotated(root, new Vector3(0, 0.05f, 2.2f), new Vector3(8.8f, 0.45f, 5.4f), roofC, Quaternion.Euler(14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(0, 0.05f, -2.2f), new Vector3(8.8f, 0.45f, 5.4f), roofC, Quaternion.Euler(-14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(2.2f, 0.05f, 0), new Vector3(5.4f, 0.45f, 8.8f), roofC, Quaternion.Euler(0f, 0f, -14f));
                CreatePartCubeRotated(root, new Vector3(-2.2f, 0.05f, 0), new Vector3(5.4f, 0.45f, 8.8f), roofC, Quaternion.Euler(0f, 0f, 14f));
                CreatePartCube(root, new Vector3(0, -0.44f, 4.3f), new Vector3(8.8f, 0.18f, 0.14f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.44f, -4.3f), new Vector3(8.8f, 0.18f, 0.14f), ridgeC);
                CreatePartCube(root, new Vector3(4.3f, -0.44f, 0), new Vector3(0.14f, 0.18f, 8.8f), ridgeC);
                CreatePartCube(root, new Vector3(-4.3f, -0.44f, 0), new Vector3(0.14f, 0.18f, 8.8f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.52f, 4.38f), new Vector3(8.8f, 0.04f, 0.08f), goldC);
                CreatePartCube(root, new Vector3(0, -0.52f, -4.38f), new Vector3(8.8f, 0.04f, 0.08f), goldC);
                CreatePartCube(root, new Vector3(4.38f, -0.52f, 0), new Vector3(0.08f, 0.04f, 8.8f), goldC);
                CreatePartCube(root, new Vector3(-4.38f, -0.52f, 0), new Vector3(0.08f, 0.04f, 8.8f), goldC);
                CreatePartCube(root, new Vector3(0, 0.55f, 0), new Vector3(3f, 0.4f, 3f), roofC);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        CreatePartCubeRotated(root, new Vector3(sx * 4.35f, -0.42f, sz * 4.35f), new Vector3(0.9f, 0.1f, 0.9f), ridgeC, Quaternion.Euler(0f, 45f, 0f));
                        CreatePartCubeRotated(root, new Vector3(sx * 4.45f, -0.32f, sz * 4.45f), new Vector3(0.38f, 0.38f, 0.38f), ridgeC, Quaternion.Euler(45f, 45f, 0f));
                    }
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartCubeRotated(root, new Vector3(sx * 3.16f, -0.18f, sz * 3.16f), new Vector3(0.12f, 0.12f, 4.8f), ridgeC, Quaternion.LookRotation(new Vector3(sx * 3.32f, -0.8f, sz * 3.32f)));
                for (int lx = -1; lx <= 1; lx += 2)
                {
                    CreatePartCube(root, new Vector3(lx * 1.8f, -0.7f, -4.25f), new Vector3(0.05f, 0.25f, 0.05f), goldC);
                    CreatePartCube(root, new Vector3(lx * 1.8f, -0.98f, -4.25f), new Vector3(0.3f, 0.07f, 0.3f), goldC);
                    CreatePartCube(root, new Vector3(lx * 1.8f, -1.18f, -4.25f), new Vector3(0.26f, 0.32f, 0.26f), doorC);
                    CreatePartCube(root, new Vector3(lx * 1.8f, -1.36f, -4.25f), new Vector3(0.18f, 0.05f, 0.18f), goldC);
                }
                break;

            case "Pagoda_Tier4Floor":
                CreatePartCube(root, new Vector3(0, 0f, 0), new Vector3(3.4f, 0.3f, 3.4f), woodC);
                CreatePartCube(root, new Vector3(0, 0.145f, -1f), new Vector3(3.3f, 0.012f, 0.025f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 0f), new Vector3(3.3f, 0.012f, 0.025f), new Color(0.45f, 0.2f, 0.1f));
                CreatePartCube(root, new Vector3(0, 0.145f, 1f), new Vector3(3.3f, 0.012f, 0.025f), new Color(0.45f, 0.2f, 0.1f));
                break;

            case "Pagoda_Tier4Walls":
                {
                    float half = 1.55f;
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, -0.2f, sz * half), new Vector3(0.35f, 1.3f, 0.35f), pillarC);
                        CreatePartCube(root, new Vector3(0, -0.2f, sx * half), new Vector3(0.35f, 1.3f, 0.35f), pillarC);
                        CreatePartCube(root, new Vector3(sx * half, -0.2f, 0), new Vector3(0.35f, 1.3f, 0.35f), pillarC);
                    }
                    CreatePartCube(root, new Vector3(0, -0.55f, half), new Vector3(2.8f, 0.6f, 0.09f), wallC);
                    CreatePartCube(root, new Vector3(0, -0.55f, -half), new Vector3(2.8f, 0.6f, 0.09f), wallC);
                    CreatePartCube(root, new Vector3(half, -0.55f, 0), new Vector3(0.09f, 0.6f, 2.8f), wallC);
                    CreatePartCube(root, new Vector3(-half, -0.55f, 0), new Vector3(0.09f, 0.6f, 2.8f), wallC);
                    CreatePartCube(root, new Vector3(0, -0.19f, half), new Vector3(2.8f, 0.1f, 0.1f), roofC);
                    CreatePartCube(root, new Vector3(0, -0.19f, -half), new Vector3(2.8f, 0.1f, 0.1f), roofC);
                    CreatePartCube(root, new Vector3(half, -0.19f, 0), new Vector3(0.1f, 0.1f, 2.8f), roofC);
                    CreatePartCube(root, new Vector3(-half, -0.19f, 0), new Vector3(0.1f, 0.1f, 2.8f), roofC);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            CreatePartCube(root, new Vector3(sx * half, 0.55f, sz * half), new Vector3(0.4f, 0.12f, 0.4f), wallC);
                            CreatePartCube(root, new Vector3(sx * half, 0.7f, sz * half), new Vector3(0.42f, 0.1f, 0.42f), goldC);
                        }
                    // parapet ring under Roof4 (world ~16.71..17.08)
                    CreatePartCube(root, new Vector3(0, 0.995f, half), new Vector3(2.8f, 0.37f, 0.1f), wallC);
                    CreatePartCube(root, new Vector3(0, 0.995f, -half), new Vector3(2.8f, 0.37f, 0.1f), wallC);
                    CreatePartCube(root, new Vector3(half, 0.995f, 0), new Vector3(0.1f, 0.37f, 2.8f), wallC);
                    CreatePartCube(root, new Vector3(-half, 0.995f, 0), new Vector3(0.1f, 0.37f, 2.8f), wallC);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            CreatePartCube(root, new Vector3(sx * half, 0.995f, sz * half), new Vector3(0.35f, 0.37f, 0.35f), wallC);
                }
                break;

            case "Pagoda_Roof4":
                CreatePartCubeRotated(root, new Vector3(0, 0f, 1.6f), new Vector3(5.6f, 0.4f, 3.6f), roofC, Quaternion.Euler(14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(0, 0f, -1.6f), new Vector3(5.6f, 0.4f, 3.6f), roofC, Quaternion.Euler(-14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(1.6f, 0f, 0), new Vector3(3.6f, 0.4f, 5.6f), roofC, Quaternion.Euler(0f, 0f, -14f));
                CreatePartCubeRotated(root, new Vector3(-1.6f, 0f, 0), new Vector3(3.6f, 0.4f, 5.6f), roofC, Quaternion.Euler(0f, 0f, 14f));
                CreatePartCube(root, new Vector3(0, -0.5f, 3.4f), new Vector3(5.6f, 0.16f, 0.12f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.5f, -3.4f), new Vector3(5.6f, 0.16f, 0.12f), ridgeC);
                CreatePartCube(root, new Vector3(3.4f, -0.5f, 0), new Vector3(0.12f, 0.16f, 5.6f), ridgeC);
                CreatePartCube(root, new Vector3(-3.4f, -0.5f, 0), new Vector3(0.12f, 0.16f, 5.6f), ridgeC);
                CreatePartCube(root, new Vector3(0, -0.6f, 3.45f), new Vector3(5.6f, 0.04f, 0.08f), goldC);
                CreatePartCube(root, new Vector3(0, -0.6f, -3.45f), new Vector3(5.6f, 0.04f, 0.08f), goldC);
                CreatePartCube(root, new Vector3(3.45f, -0.6f, 0), new Vector3(0.08f, 0.04f, 5.6f), goldC);
                CreatePartCube(root, new Vector3(-3.45f, -0.6f, 0), new Vector3(0.08f, 0.04f, 5.6f), goldC);
                CreatePartCube(root, new Vector3(0, 0.45f, 0), new Vector3(2f, 0.35f, 2f), roofC);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        CreatePartCubeRotated(root, new Vector3(sx * 3.35f, -0.42f, sz * 3.35f), new Vector3(0.6f, 0.08f, 0.6f), ridgeC, Quaternion.Euler(0f, 45f, 0f));
                        CreatePartCubeRotated(root, new Vector3(sx * 3.45f, -0.32f, sz * 3.45f), new Vector3(0.3f, 0.3f, 0.3f), ridgeC, Quaternion.Euler(45f, 45f, 0f));
                    }
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartCubeRotated(root, new Vector3(sx * 2.17f, -0.14f, sz * 2.17f), new Vector3(0.1f, 0.1f, 3.4f), ridgeC, Quaternion.LookRotation(new Vector3(sx * 2.35f, -0.57f, sz * 2.35f)));
                break;

            case "Pagoda_Spire":
                CreatePartCube(root, new Vector3(0, -0.42f, 0), new Vector3(0.8f, 0.3f, 0.8f), goldC);
                CreatePartCube(root, new Vector3(0, 0.1f, 0), new Vector3(1.2f, 0.45f, 1.2f), goldC);
                CreatePartCube(root, new Vector3(0, 1.55f, 0), new Vector3(0.3f, 2.6f, 0.3f), goldC);
                CreatePartCube(root, new Vector3(0, 0.7f, 0), new Vector3(0.7f, 0.16f, 0.7f), goldC);
                CreatePartCube(root, new Vector3(0, 1.9f, 0), new Vector3(0.6f, 0.14f, 0.6f), goldC);
                CreatePartCube(root, new Vector3(0, 2.3f, 0), new Vector3(0.55f, 0.12f, 0.55f), goldC);
                CreatePartCube(root, new Vector3(0, 2.6f, 0), new Vector3(0.5f, 0.5f, 0.5f), goldC);
                CreatePartCubeRotated(root, new Vector3(0, 2.95f, 0), new Vector3(0.35f, 0.35f, 0.35f), goldC, Quaternion.Euler(45f, 0f, 45f));
                break;
        }
    }

    private void BuildChurchPart(Transform root, string partType)
    {
        Color stoneBase = new Color(0.52f, 0.51f, 0.5f);
        Color lightStoneC = new Color(0.8f, 0.78f, 0.74f);
        Color whiteWallC = new Color(0.93f, 0.91f, 0.86f);
        Color roofRedC = new Color(0.5f, 0.14f, 0.11f);
        Color roofDarkC = new Color(0.32f, 0.09f, 0.08f);
        Color darkWoodC = new Color(0.38f, 0.24f, 0.14f);
        Color goldC = new Color(1f, 0.84f, 0.2f);
        Color glassC = new Color(0.55f, 0.7f, 0.85f);
        Color flameC = new Color(0.95f, 0.85f, 0.5f);

        // Site datum ladder, y = 0 at the platform top. Every block below is
        // stated by the face that rests on something, never by a centre, so the
        // support of each block is readable on adjacent lines.
        const float slabTop = 0.35f;   // main foundation slab top (terrace)
        const float capTop = 0.40f;    // interior terrace cap top
        const float floorTop = 0.50f;  // nave walking surface
        const float plinthTop = 1.10f; // top of the wall plinth band
        const float wallTop = 4.30f;   // top of the white wall band
        const float corniceTop = 4.60f;// top of the wall cornice = roof bearing
        const float wallX = 6.80f;     // side wall centre plane
        const float wallZ = 6.15f;     // front/back wall centre plane
        const float roofHalfW = 8.40f; // nave roof half width
        const float roofThick = 0.50f;
        const float towerZ = -6.20f;   // every spire-stage part shares this axis
        const float roofTan = 0.4571f;   // nave pitch, tan(24.56 deg)
        const float roofVert = 0.550f;   // roofThick * sqrt(1 + roofTan^2)
        const float sprTan = 0.7813f;    // spire pitch, tan(38 deg)
        const float sprVert = 0.571f;    // spire thickness * sqrt(1 + sprTan^2)

        switch (partType)
        {
            case "Church_Foundation":
                // Main slab reaches past the rear buttresses so they have a
                // support, and sinks 0.15 into the platform so it cannot float.
                CreatePartBoxOn(root, 0f, -0.15f, 0f, new Vector3(16f, 0.50f, 14.80f), stoneBase);
                // Terrace cap, inset from the slab edge so the side buttresses
                // land on bare slab, not half-sunk into the cap.
                CreatePartBoxOn(root, 0f, capTop - 0.16f, 0f, new Vector3(13f, 0.16f, 11f), lightStoneC);
                // Solid stair down from the tower landing: each tread is a full
                // block from below grade, never a thin slab on air.
                float[] treadTop = { 0.72f, 0.54f, 0.36f, 0.18f };
                float[] treadZ = { -10.15f, -11.05f, -11.95f, -12.85f };
                float[] treadW = { 6.40f, 7.00f, 7.60f, 8.20f };
                for (int i = 0; i < 4; i++)
                    CreatePartBoxOn(root, 0f, -0.05f, treadZ[i], new Vector3(treadW[i], treadTop[i] + 0.05f, 0.90f), lightStoneC);
                // Terrace corner bollards on bare slab.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartBoxOn(root, sx * 7.2f, slabTop, sz * 6.9f, new Vector3(0.40f, 0.50f, 0.40f), lightStoneC);
                break;
            case "Church_NaveFloor":
                // Runs under both side walls and into the front/back walls.
                CreatePartBoxOn(root, 0f, 0.30f, 0.20f, new Vector3(13.60f, 0.20f, 12.30f), lightStoneC);
                // Aisle runner, seated on the walking surface.
                CreatePartBoxOn(root, 0f, floorTop, 0.20f, new Vector3(1.80f, 0.08f, 8.60f), stoneBase);
                // Tower landing meets the nave with one 0.20 step instead of a drop.
                CreatePartBoxOn(root, 0f, floorTop, -2.95f, new Vector3(3.00f, 0.20f, 0.50f), lightStoneC);
                break;
            case "Church_Pillars":
                // Three pieces, bottom-referenced, so a base cannot hover.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int ix = 0; ix < 2; ix++)
                        for (int iz = 0; iz < 3; iz++)
                        {
                            float px = sx * (ix == 0 ? 1.70f : 4.70f);
                            float pz = (iz - 1) * 3.0f;
                            CreatePartBoxOn(root, px, floorTop, pz, new Vector3(0.90f, 0.45f, 0.90f), stoneBase);
                            CreatePartBoxOn(root, px, 0.95f, pz, new Vector3(0.60f, 3.20f, 0.60f), lightStoneC);
                            CreatePartBoxOn(root, px, 4.15f, pz, new Vector3(0.80f, 0.35f, 0.80f), lightStoneC);
                            CreatePartBoxOn(root, px, 4.45f, pz, new Vector3(0.84f, 0.17f, 0.84f), goldC);
                        }
                break;
            case "Church_SideWalls":
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    float x = sx * wallX;
                    // Plinth, wall band and cornice; the cornice is what the roof
                    // bears on, so its top is the roof's bearing datum.
                    CreatePartBoxOn(root, x, slabTop, 0f, new Vector3(0.55f, plinthTop - slabTop, 12.20f), stoneBase);
                    // Sill band and lintel band span the full wall; the piers
                    // between them leave six real window openings, so the glass
                    // is inside a hole rather than buried in a solid slab.
                    CreatePartBoxOn(root, x, plinthTop, 0f, new Vector3(0.35f, 0.65f, 12.20f), lightStoneC);
                    CreatePartBoxOn(root, x, 3.45f, 0f, new Vector3(0.35f, wallTop - 3.45f, 12.20f), whiteWallC);
                    float[] pierZ = { -5.825f, -4f, -2f, 0f, 2f, 4f, 5.825f };
                    float[] pierW = { 0.35f, 0.70f, 0.70f, 0.70f, 0.70f, 0.70f, 0.35f };
                    for (int i = 0; i < pierZ.Length; i++)
                        CreatePartBoxOn(root, x, plinthTop, pierZ[i], new Vector3(0.35f, 3.45f - plinthTop, pierW[i]), whiteWallC);
                    // Six traceried openings, glass in the middle of the gap.
                    for (int iz = 0; iz < 6; iz++)
                    {
                        float wz = (iz - 2.5f) * 2.0f;
                        CreatePartBoxOn(root, x, 1.75f, wz, new Vector3(0.10f, 1.70f, 1.30f), glassC);
                        // Gold frame: sill, head and two mullions.
                        CreatePartBoxOn(root, x, 1.70f, wz, new Vector3(0.30f, 0.12f, 1.44f), goldC);
                        CreatePartBoxOn(root, x, 3.39f, wz, new Vector3(0.30f, 0.12f, 1.44f), goldC);
                        CreatePartBoxOn(root, x, 1.75f, wz - 0.43f, new Vector3(0.28f, 1.70f, 0.10f), goldC);
                        CreatePartBoxOn(root, x, 1.75f, wz + 0.43f, new Vector3(0.28f, 1.70f, 0.10f), goldC);
                        // Stone sill projecting outboard of the wall face.
                        CreatePartBoxOn(root, x + sx * 0.22f, 1.63f, wz, new Vector3(0.30f, 0.12f, 1.60f), lightStoneC);
                    }
                    // Corner pilasters, seated on the plinth, closing the end piers
                    // and clear of the outermost window reveal.
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartBoxOn(root, x, slabTop, sz * 5.90f, new Vector3(0.45f, wallTop - slabTop, 0.45f), whiteWallC);
                    CreatePartBoxOn(root, x, wallTop, 0f, new Vector3(0.45f, corniceTop - wallTop, 12.60f), lightStoneC);
                }
                break;
            case "Church_FrontWall":
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    float cx = sx * 4.85f; // 3.9 wide, overlapping the tower face
                    CreatePartBoxOn(root, cx, slabTop, -wallZ, new Vector3(3.90f, plinthTop - slabTop, 0.55f), stoneBase);
                    CreatePartBoxOn(root, cx, plinthTop, -wallZ, new Vector3(3.90f, wallTop - plinthTop, 0.40f), whiteWallC);
                    // Tall lancet with a pointed head, applied to the outer face:
                    // the band is solid, so the reveal is proud and overlaps it.
                    float fz = -wallZ - 0.20f;
                    CreatePartBoxOn(root, cx, 1.70f, fz, new Vector3(1.30f, 2.00f, 0.30f), glassC);
                    CreatePartBoxOn(root, cx, 1.64f, fz, new Vector3(1.44f, 0.14f, 0.40f), goldC);
                    CreatePartBoxOn(root, cx, 3.64f, fz, new Vector3(1.44f, 0.14f, 0.40f), goldC);
                    CreatePartBoxOn(root, cx, 1.70f, fz, new Vector3(0.12f, 2.00f, 0.40f), goldC);
                    // Pointed head: two slabs leaning in to a point above the head.
                    CreatePartCubeRotated(root, new Vector3(cx - 0.33f, 4.05f, fz - 0.05f), new Vector3(0.80f, 0.30f, 0.30f), lightStoneC, Quaternion.Euler(0f, 0f, 35f));
                    CreatePartCubeRotated(root, new Vector3(cx + 0.33f, 4.05f, fz - 0.05f), new Vector3(0.80f, 0.30f, 0.30f), lightStoneC, Quaternion.Euler(0f, 0f, -35f));
                    // Corner pilaster and cornice return.
                    CreatePartBoxOn(root, sx * 6.60f, slabTop, -wallZ, new Vector3(0.50f, wallTop - slabTop, 0.50f), whiteWallC);
                    CreatePartBoxOn(root, sx * 4.95f, wallTop, -wallZ, new Vector3(4.10f, corniceTop - wallTop, 0.50f), lightStoneC);
                }
                break;
            case "Church_BackWall":
                CreatePartBoxOn(root, 0f, slabTop, wallZ, new Vector3(13.80f, plinthTop - slabTop, 0.55f), stoneBase);
                CreatePartBoxOn(root, 0f, plinthTop, wallZ, new Vector3(13.80f, wallTop - plinthTop, 0.40f), whiteWallC);
                // The band's inner face is at wallZ - 0.20, so every applied piece
                // is deep enough to bury 0.10 in the wall instead of hovering.
                float inZ = wallZ - 0.30f;
                // Four tall lancets flanking a central gold cross panel.
                for (int ix = 0; ix < 4; ix++)
                {
                    float wx = (ix - 1.5f) * 2.20f;
                    CreatePartBoxOn(root, wx, 1.75f, inZ, new Vector3(1.00f, 1.90f, 0.40f), glassC);
                    CreatePartBoxOn(root, wx, 1.69f, inZ, new Vector3(1.14f, 0.14f, 0.46f), goldC);
                    CreatePartBoxOn(root, wx, 3.59f, inZ, new Vector3(1.14f, 0.14f, 0.46f), goldC);
                    CreatePartBoxOn(root, wx, 1.75f, inZ, new Vector3(0.10f, 1.90f, 0.46f), goldC);
                }
                CreatePartBoxOn(root, 0f, floorTop, inZ, new Vector3(2.60f, 2.60f, 0.40f), lightStoneC);
                CreatePartBoxOn(root, 0f, 2.60f, inZ - 0.25f, new Vector3(0.18f, 1.10f, 0.16f), goldC);
                CreatePartBoxOn(root, 0f, 2.90f, inZ - 0.25f, new Vector3(0.80f, 0.18f, 0.16f), goldC);
                // Reredos wings, seated on the floor (rotation is about Y only, so
                // the centre Y arithmetic stays exact).
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    CreatePartCubeRotated(root, new Vector3(sx * 5.40f, floorTop + 0.95f, inZ - 0.30f), new Vector3(1.90f, 1.90f, 0.30f), lightStoneC, Quaternion.Euler(0f, sx * 35f, 0f));
                    CreatePartCubeRotated(root, new Vector3(sx * 5.10f, floorTop + 0.50f, inZ - 0.48f), new Vector3(1.60f, 1.00f, 0.18f), whiteWallC, Quaternion.Euler(0f, sx * 35f, 0f));
                }
                CreatePartBoxOn(root, 0f, wallTop, wallZ, new Vector3(14.00f, corniceTop - wallTop, 0.50f), lightStoneC);
                break;
            case "Church_Roof":
                // Two panels, each stated by the two ends of its underside: the
                // eave is buried 6 cm into the cornice and the ridge underside is
                // roofVert above the panel tops. No tilt sign is involved.
                float eaveUnder = corniceTop - 0.06f;
                float ridgeUnder = eaveUnder + wallZ * roofTan;
                for (int sz = -1; sz <= 1; sz += 2)
                    CreatePartPanelBetween(root, new Vector3(0f, eaveUnder, sz * wallZ), new Vector3(0f, ridgeUnder, 0f), roofHalfW, roofThick, Vector3.right, roofRedC);
                // Ridge cap laid along each panel's own top surface, sunk 5 cm.
                float peakTop = ridgeUnder + roofVert;
                for (int sz = -1; sz <= 1; sz += 2)
                    CreatePartPanelBetween(root, new Vector3(0f, peakTop - 0.05f, 0f), new Vector3(0f, peakTop - 0.05f - 1.2f * roofTan, sz * 1.2f), 8.45f, 0.22f, Vector3.right, roofDarkC);
                CreatePartBoxOn(root, 0f, peakTop - 0.20f, 0f, new Vector3(0.50f, 0.40f, 0.50f), goldC);
                // Gable infill on both side walls: the same eave/ridge undersides
                // the panels were built from, so the steps track the real pitch.
                for (int sx = -1; sx <= 1; sx += 2)
                    CreatePartGableSteps(root, sx * wallX, 0.45f, wallTop, eaveUnder, ridgeUnder, wallZ, 10, whiteWallC);
                break;
            case "Church_Tower":
                // Apron is the raised landing and it is wider than the tower so the
                // front buttresses have something to bear on; it sinks below grade
                // so its overhang past the slab still has a support.
                CreatePartBoxOn(root, 0f, -0.05f, towerZ, new Vector3(7.80f, 0.95f, 7.00f), stoneBase);
                CreatePartBoxOn(root, 0f, 0.90f, towerZ, new Vector3(6.20f, 6.60f, 6.20f), lightStoneC);
                // Corner pilasters, seated on the apron and standing proud of the top.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartBoxOn(root, sx * 2.90f, 0.90f, towerZ + sz * 2.90f, new Vector3(0.60f, 6.70f, 0.60f), whiteWallC);
                // Door: real opening framed by jambs, seated on the landing. Every
                // applied piece is centred 5 cm proud of the tower face and 30 cm
                // deep, so it buries 10 cm in the solid face instead of hovering.
                float faceZ = towerZ - 3.10f;
                for (int sx = -1; sx <= 1; sx += 2)
                    CreatePartBoxOn(root, sx * 0.95f, 0.90f, faceZ - 0.01f, new Vector3(0.25f, 2.80f, 0.30f), stoneBase);
                CreatePartBoxOn(root, 0f, 3.70f, faceZ - 0.01f, new Vector3(2.30f, 0.30f, 0.35f), stoneBase);
                CreatePartBoxOn(root, 0f, 0.90f, faceZ - 0.05f, new Vector3(1.50f, 2.60f, 0.24f), darkWoodC);
                for (int i = 0; i < 3; i++)
                    CreatePartBoxOn(root, 0f, 1.30f + i * 0.70f, faceZ - 0.20f, new Vector3(1.50f, 0.12f, 0.14f), goldC);
                CreatePartBoxOn(root, 0f, 3.50f, faceZ - 0.25f, new Vector3(0.20f, 0.30f, 0.20f), goldC);
                // Rose window over the door, applied proud of the solid tower face.
                CreatePartBoxOn(root, 0f, 4.65f, faceZ - 0.05f, new Vector3(1.50f, 1.50f, 0.30f), glassC);
                for (int i = 0; i < 4; i++)
                {
                    float a = 22.5f + i * 45f;
                    CreatePartCubeRotated(root, new Vector3(0f, 5.40f, faceZ - 0.18f), new Vector3(0.10f, 1.90f, 0.20f), goldC, Quaternion.Euler(0f, 0f, a));
                }
                CreatePartCubeRotated(root, new Vector3(0f, 5.40f, faceZ - 0.18f), new Vector3(1.90f, 0.12f, 0.20f), goldC, Quaternion.Euler(0f, 0f, 45f));
                CreatePartCubeRotated(root, new Vector3(0f, 5.40f, faceZ - 0.18f), new Vector3(1.90f, 0.12f, 0.20f), goldC, Quaternion.Euler(0f, 0f, -45f));
                CreatePartBoxOn(root, 0f, 6.05f, faceZ - 0.15f, new Vector3(1.80f, 0.16f, 0.30f), goldC);
                // Applied side lancets on the tower flanks, same proud-face rule.
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    CreatePartBoxOn(root, sx * 3.12f, 2.10f, towerZ, new Vector3(0.24f, 1.80f, 1.20f), glassC);
                    CreatePartBoxOn(root, sx * 3.15f, 1.98f, towerZ, new Vector3(0.28f, 0.16f, 1.36f), goldC);
                    CreatePartBoxOn(root, sx * 3.15f, 3.84f, towerZ, new Vector3(0.28f, 0.16f, 1.36f), goldC);
                    CreatePartBoxOn(root, sx * 3.15f, 2.10f, towerZ - 0.62f, new Vector3(0.28f, 1.80f, 0.16f), goldC);
                    CreatePartBoxOn(root, sx * 3.15f, 2.10f, towerZ + 0.62f, new Vector3(0.28f, 1.80f, 0.16f), goldC);
                }
                // Rear arch panel facing the nave.
                CreatePartBoxOn(root, 0f, 0.90f, towerZ + 3.05f, new Vector3(2.40f, 2.60f, 0.30f), whiteWallC);
                CreatePartBoxOn(root, 0f, 3.50f, towerZ + 3.05f, new Vector3(2.80f, 0.25f, 0.35f), lightStoneC);
                // Cornice, bearing the belfry.
                CreatePartBoxOn(root, 0f, 7.50f, towerZ, new Vector3(6.80f, 0.40f, 6.80f), lightStoneC);
                break;
            case "Church_Belfry":
                CreatePartBoxOn(root, 0f, 7.80f, towerZ, new Vector3(4.60f, 2.50f, 4.60f), lightStoneC);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartBoxOn(root, sx * 2.20f, 7.80f, towerZ + sz * 2.20f, new Vector3(0.45f, 2.50f, 0.45f), whiteWallC);
                // Louvres stand proud of the solid belfry faces so they read.
                for (int side = 0; side < 4; side++)
                {
                    bool onX = side < 2;
                    float sgn = (side % 2 == 0) ? 1f : -1f;
                    for (int i = 0; i < 3; i++)
                    {
                        float o = (i - 1) * 0.55f;
                        if (onX) CreatePartBoxOn(root, sgn * 2.33f, 8.20f, towerZ + o, new Vector3(0.20f, 1.80f, 0.12f), darkWoodC);
                        else CreatePartBoxOn(root, o, 8.20f, towerZ + sgn * 2.33f, new Vector3(0.12f, 1.80f, 0.20f), darkWoodC);
                    }
                }
                // Cornice is wider than the spire panels it carries, so their
                // edges finish inside it instead of coplanar with its edge.
                CreatePartBoxOn(root, 0f, 10.30f, towerZ, new Vector3(5.60f, 0.30f, 5.60f), lightStoneC);
                break;
            case "Church_SpireRoof":
                // Square pyramid: every eave start shares the belfry axis, and
                // the four panels are stated as underside endpoints so the pitch
                // cannot be written backwards. Eave is buried in the cornice.
                float sprHalf = 2.72f;
                float eaveY = 10.45f;
                float apexY = eaveY + sprHalf * sprTan;
                float sprThick = 0.45f;
                for (int sz = -1; sz <= 1; sz += 2)
                    CreatePartPanelBetween(root, new Vector3(0f, eaveY, towerZ + sz * sprHalf), new Vector3(0f, apexY, towerZ), sprHalf, sprThick, Vector3.right, roofRedC);
                for (int sx = -1; sx <= 1; sx += 2)
                    CreatePartPanelBetween(root, new Vector3(sx * sprHalf, eaveY, towerZ), new Vector3(0f, apexY, towerZ), sprHalf, sprThick, Vector3.forward, roofRedC);
                // Gold eave trim capping each panel's end, and hip corner blocks.
                for (int sz = -1; sz <= 1; sz += 2)
                    CreatePartBoxOn(root, 0f, eaveY + 0.05f, towerZ + sz * 2.76f, new Vector3(5.44f, 0.55f, 0.16f), goldC);
                for (int sx = -1; sx <= 1; sx += 2)
                    CreatePartBoxOn(root, sx * 2.76f, eaveY + 0.05f, towerZ, new Vector3(0.16f, 0.55f, 5.44f), goldC);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartBoxOn(root, sx * sprHalf, eaveY + 0.05f, towerZ + sz * sprHalf, new Vector3(0.34f, 0.60f, 0.34f), goldC);
                break;
            case "Church_Spire":
                // Stacked on the spire roof's own apex, on the shared axis. Each
                // stage overlaps the one below by 0.10 so no seam can open.
                float apexTop = apexY + sprVert;
                CreatePartBoxOn(root, 0f, apexTop - 0.15f, towerZ, new Vector3(1.80f, 0.85f, 1.80f), roofDarkC);
                CreatePartBoxOn(root, 0f, apexTop + 0.60f, towerZ, new Vector3(1.10f, 5.40f, 1.10f), roofDarkC);
                for (int i = 0; i < 3; i++)
                    CreatePartBoxOn(root, 0f, apexTop + 1.80f + i * 1.70f, towerZ, new Vector3(1.25f, 0.16f, 1.25f), goldC);
                CreatePartBoxOn(root, 0f, apexTop + 5.90f, towerZ, new Vector3(0.90f, 0.80f, 0.90f), roofDarkC);
                CreatePartBoxOn(root, 0f, apexTop + 6.60f, towerZ, new Vector3(0.90f, 1.00f, 0.90f), goldC);
                CreatePartCubeRotated(root, new Vector3(0f, apexTop + 7.45f, towerZ), new Vector3(0.35f, 0.35f, 0.35f), goldC, Quaternion.Euler(45f, 0f, 45f));
                CreatePartBoxOn(root, 0f, apexTop + 7.30f, towerZ, new Vector3(0.14f, 1.20f, 0.14f), goldC);
                CreatePartBoxOn(root, 0f, apexTop + 8.00f, towerZ, new Vector3(0.75f, 0.14f, 0.14f), goldC);
                break;
            case "Church_Buttresses":
                // Side buttresses clear the terrace cap and bear on bare slab; every
                // stage is pulled inboard far enough to stay buried in the one below.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int i = 0; i < 4; i++)
                    {
                        float bz = (i - 1.5f) * 3.2f;
                        CreatePartBoxOn(root, sx * 7.10f, slabTop, bz, new Vector3(0.90f, 1.80f, 1.10f), stoneBase);
                        CreatePartBoxOn(root, sx * 7.15f, 2.05f, bz, new Vector3(0.50f, 1.30f, 0.80f), stoneBase);
                        CreatePartBoxOn(root, sx * 7.15f, 3.25f, bz, new Vector3(0.50f, 0.35f, 0.70f), lightStoneC);
                    }
                // Front buttresses bear on the tower apron and hug the tower face.
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    CreatePartBoxOn(root, sx * 3.35f, 0.90f, -7.70f, new Vector3(0.70f, 1.80f, 1.10f), stoneBase);
                    CreatePartBoxOn(root, sx * 3.40f, 2.60f, -7.70f, new Vector3(0.50f, 1.30f, 0.80f), stoneBase);
                    CreatePartBoxOn(root, sx * 3.40f, 3.80f, -7.70f, new Vector3(0.50f, 0.35f, 0.70f), lightStoneC);
                }
                // Rear buttresses reach the slab, which was extended for them, and
                // overlap the back wall's outer face instead of meeting it flush.
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    CreatePartBoxOn(root, sx * 6.40f, slabTop, 6.75f, new Vector3(0.90f, 1.80f, 1.00f), stoneBase);
                    CreatePartBoxOn(root, sx * 6.55f, 2.05f, 6.75f, new Vector3(0.50f, 1.30f, 0.90f), stoneBase);
                    CreatePartBoxOn(root, sx * 6.60f, 3.25f, 6.75f, new Vector3(0.50f, 0.35f, 0.90f), lightStoneC);
                }
                break;
            case "Church_Interior":
                // Altar, seated on the walking surface.
                CreatePartBoxOn(root, 0f, floorTop, 4.30f, new Vector3(3.00f, 0.80f, 1.10f), stoneBase);
                CreatePartBoxOn(root, 0f, 1.20f, 4.30f, new Vector3(3.10f, 0.15f, 1.20f), lightStoneC);
                CreatePartBoxOn(root, 0f, 1.30f, 4.30f, new Vector3(0.16f, 0.80f, 0.16f), goldC);
                CreatePartBoxOn(root, 0f, 1.85f, 4.30f, new Vector3(0.60f, 0.14f, 0.16f), goldC);
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    CreatePartBoxOn(root, sx * 1.10f, 1.30f, 4.30f, new Vector3(0.14f, 0.45f, 0.14f), flameC);
                    CreatePartBoxOn(root, sx * 1.10f, 1.75f, 4.30f, new Vector3(0.08f, 0.30f, 0.08f), goldC);
                }
                // Pulpit and its two approach steps, all seated on the floor. The
                // step nearest the pulpit is the taller one, so the climb rises
                // toward the platform instead of away from it.
                CreatePartBoxOn(root, -4.20f, floorTop, 1.50f, new Vector3(1.50f, 1.60f, 1.20f), stoneBase);
                CreatePartBoxOn(root, -4.20f, 2.00f, 1.50f, new Vector3(1.62f, 0.30f, 1.32f), lightStoneC);
                CreatePartBoxOn(root, -3.30f, floorTop, 1.60f, new Vector3(0.60f, 0.25f, 1.00f), stoneBase);
                CreatePartBoxOn(root, -2.80f, floorTop, 1.60f, new Vector3(0.60f, 0.15f, 1.00f), stoneBase);
                CreatePartBoxOn(root, -3.55f, 2.20f, 1.50f, new Vector3(0.20f, 0.45f, 0.20f), goldC);
                // Six pews, seat and back both referenced to the floor.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int i = 0; i < 3; i++)
                    {
                        float pz = (i - 1) * 2.80f;
                        CreatePartBoxOn(root, sx * 3.20f, floorTop, pz, new Vector3(1.50f, 0.45f, 2.40f), darkWoodC);
                        CreatePartBoxOn(root, sx * 3.20f + sx * 0.58f, floorTop + 0.35f, pz, new Vector3(0.16f, 0.60f, 2.40f), darkWoodC);
                    }
                break;
        }
    }

    private void BuildShrinePart(Transform root, string partType)
    {
        Color stoneBase = new Color(0.4f, 0.38f, 0.36f);
        Color stoneDark = new Color(0.34f, 0.33f, 0.31f);
        Color woodFloorC = new Color(0.42f, 0.28f, 0.16f);
        Color pillarC = new Color(0.45f, 0.44f, 0.42f);
        Color tileC = new Color(0.35f, 0.45f, 0.38f);
        Color ridgeC = new Color(0.22f, 0.3f, 0.25f);
        Color goldC = new Color(1f, 0.84f, 0.2f);
        Color blackC = new Color(0.08f, 0.08f, 0.09f);
        Color whiteC = new Color(0.95f, 0.95f, 0.94f);
        Color robeC = new Color(0.62f, 0.12f, 0.16f);
        Color bronzeC = new Color(0.45f, 0.24f, 0.12f);
        Color woodDarkC = new Color(0.3f, 0.18f, 0.1f);
        Color skinC = new Color(0.55f, 0.5f, 0.45f);
        Color flameC = new Color(0.95f, 0.85f, 0.5f);
        Color smokeC = new Color(0.85f, 0.82f, 0.78f);

        switch (partType)
        {
            case "Shrine_Foundation":
                CreatePartCube(root, new Vector3(0f, 0f, 0f), new Vector3(14f, 0.6f, 12f), stoneBase);
                CreatePartCube(root, new Vector3(0f, 0.3f, 0f), new Vector3(13f, 0.25f, 11f), new Color(0.52f, 0.5f, 0.48f));
                CreatePartCube(root, new Vector3(0f, 0.15f, -6f), new Vector3(8f, 0.35f, 0.9f), stoneBase);
                CreatePartCube(root, new Vector3(0f, 0.45f, -6.55f), new Vector3(6.4f, 0.35f, 0.9f), stoneBase);
                CreatePartCube(root, new Vector3(0f, 0.75f, -7.1f), new Vector3(4.8f, 0.35f, 0.9f), stoneBase);
                CreatePartCube(root, new Vector3(0f, 1.02f, -7.6f), new Vector3(3.4f, 0.16f, 0.8f), goldC);
                break;

            case "Shrine_Floor":
                CreatePartCube(root, new Vector3(0f, 0f, 0f), new Vector3(12f, 0.25f, 10f), woodFloorC);
                CreatePartCube(root, new Vector3(0f, 0.22f, 0f), new Vector3(10.4f, 0.05f, 8.4f), new Color(0.5f, 0.34f, 0.2f));
                CreatePartCube(root, new Vector3(0f, 0.24f, -4.4f), new Vector3(2.6f, 0.06f, 0.9f), goldC);
                break;

            case "Shrine_Pillars":
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        CreatePartCube(root, new Vector3(sx * 4.9f, 0.4f, sz * 3.9f), new Vector3(0.95f, 0.4f, 0.95f), stoneDark);
                        CreatePartCube(root, new Vector3(sx * 4.9f, 2.3f, sz * 3.9f), new Vector3(0.6f, 3.6f, 0.6f), pillarC);
                        CreatePartCube(root, new Vector3(sx * 4.9f, 4.2f, sz * 3.9f), new Vector3(0.95f, 0.4f, 0.95f), stoneDark);
                        CreatePartCube(root, new Vector3(sx * 4.9f, 4.35f, sz * 3.9f), new Vector3(0.8f, 0.14f, 0.8f), goldC);
                    }
                    CreatePartCube(root, new Vector3(sx * 4.9f, 0.4f, 0f), new Vector3(0.95f, 0.4f, 0.95f), stoneDark);
                    CreatePartCube(root, new Vector3(sx * 4.9f, 2.3f, 0f), new Vector3(0.6f, 3.6f, 0.6f), pillarC);
                    CreatePartCube(root, new Vector3(sx * 4.9f, 4.2f, 0f), new Vector3(0.95f, 0.4f, 0.95f), stoneDark);
                    CreatePartCube(root, new Vector3(sx * 4.9f, 4.35f, 0f), new Vector3(0.8f, 0.14f, 0.8f), goldC);
                }
                for (int mz = -1; mz <= 1; mz += 2)
                {
                    CreatePartCube(root, new Vector3(0f, 0.4f, mz * 3.9f), new Vector3(0.95f, 0.4f, 0.95f), stoneDark);
                    CreatePartCube(root, new Vector3(0f, 2.3f, mz * 3.9f), new Vector3(0.6f, 3.6f, 0.6f), pillarC);
                    CreatePartCube(root, new Vector3(0f, 4.2f, mz * 3.9f), new Vector3(0.95f, 0.4f, 0.95f), stoneDark);
                    CreatePartCube(root, new Vector3(0f, 4.35f, mz * 3.9f), new Vector3(0.8f, 0.14f, 0.8f), goldC);
                    CreatePartCube(root, new Vector3(0f, 4.5f, mz * 3.9f), new Vector3(10.2f, 0.45f, 0.35f), ridgeC);
                }
                for (int mx = -1; mx <= 1; mx += 2)
                    CreatePartCube(root, new Vector3(mx * 4.9f, 4.5f, 0f), new Vector3(0.35f, 0.45f, 7.8f), ridgeC);
                break;

            case "Shrine_BackWall":
                CreatePartCube(root, new Vector3(0f, 2f, 4.9f), new Vector3(9.2f, 4.2f, 0.4f), stoneDark);
                CreatePartCube(root, new Vector3(-4.5f, 2f, 4.9f), new Vector3(0.45f, 4.2f, 0.45f), robeC);
                CreatePartCube(root, new Vector3(4.5f, 2f, 4.9f), new Vector3(0.45f, 4.2f, 0.45f), robeC);
                CreatePartCube(root, new Vector3(0f, 4.5f, 4.9f), new Vector3(8.6f, 0.3f, 0.1f), goldC);
                CreatePartCube(root, new Vector3(0f, 2.6f, 4.7f), new Vector3(2.2f, 2.2f, 0.1f), goldC);
                CreatePartCube(root, new Vector3(0f, 2.6f, 4.64f), new Vector3(1.9f, 1.9f, 0.08f), whiteC);
                CreatePartCube(root, new Vector3(0f, 2.6f, 4.59f), new Vector3(0.95f, 1.9f, 0.05f), blackC);
                CreatePartCube(root, new Vector3(0.48f, 3.18f, 4.56f), new Vector3(0.34f, 0.34f, 0.06f), whiteC);
                CreatePartCube(root, new Vector3(-0.48f, 2.02f, 4.56f), new Vector3(0.34f, 0.34f, 0.06f), blackC);
                break;

            case "Shrine_Roof":
                CreatePartCubeRotated(root, new Vector3(0f, 0.5f, -3.2f), new Vector3(14.4f, 0.5f, 5.4f), tileC, Quaternion.Euler(-14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(0f, 0.5f, 3.2f), new Vector3(14.4f, 0.5f, 5.4f), tileC, Quaternion.Euler(14f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(-3.2f, 0.5f, 0f), new Vector3(5.4f, 0.5f, 13.2f), tileC, Quaternion.Euler(0f, 0f, 14f));
                CreatePartCubeRotated(root, new Vector3(3.2f, 0.5f, 0f), new Vector3(5.4f, 0.5f, 13.2f), tileC, Quaternion.Euler(0f, 0f, -14f));
                CreatePartCube(root, new Vector3(0f, 1.6f, 0f), new Vector3(7f, 0.45f, 7f), ridgeC);
                CreatePartCube(root, new Vector3(0f, 2.15f, 0f), new Vector3(0.75f, 0.5f, 0.75f), goldC);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        CreatePartCubeRotated(root, new Vector3(sx * 4.9f, -0.2f, sz * 4.9f), new Vector3(0.7f, 0.2f, 0.7f), goldC, Quaternion.Euler(0f, 45f, 0f));
                break;

            case "Shrine_Tier2Floor":
                CreatePartCube(root, new Vector3(0f, 0.1f, 0f), new Vector3(8.6f, 0.25f, 7.6f), woodFloorC);
                for (int ex = -1; ex <= 1; ex += 2)
                    CreatePartCube(root, new Vector3(ex * 4.3f, 0.3f, 0f), new Vector3(0.16f, 0.12f, 7.6f), ridgeC);
                for (int ez = -1; ez <= 1; ez += 2)
                    CreatePartCube(root, new Vector3(0f, 0.3f, ez * 3.8f), new Vector3(8.6f, 0.12f, 0.16f), ridgeC);
                CreatePartCube(root, new Vector3(0f, 0.4f, -3.92f), new Vector3(8.6f, 0.2f, 0.12f), ridgeC);
                for (int px = -3; px <= 3; px += 2)
                    CreatePartCube(root, new Vector3(px, 0.55f, -3.92f), new Vector3(0.12f, 0.5f, 0.12f), goldC);
                break;

            case "Shrine_Tier2Walls":
                for (int cx = -1; cx <= 1; cx += 2)
                    for (int cz = -1; cz <= 1; cz += 2)
                        CreatePartCube(root, new Vector3(cx * 3.6f, 1f, cz * 3f), new Vector3(0.5f, 2f, 0.5f), pillarC);
                CreatePartCube(root, new Vector3(0f, 0.95f, -3.05f), new Vector3(7.4f, 0.9f, 0.16f), ridgeC);
                CreatePartCube(root, new Vector3(0f, 0.95f, 3.05f), new Vector3(7.4f, 0.9f, 0.16f), ridgeC);
                CreatePartCube(root, new Vector3(-3.6f, 0.95f, 0f), new Vector3(0.16f, 0.9f, 6f), ridgeC);
                CreatePartCube(root, new Vector3(3.6f, 0.95f, 0f), new Vector3(0.16f, 0.9f, 6f), ridgeC);
                CreatePartCube(root, new Vector3(0f, 1.45f, -3.05f), new Vector3(7.4f, 0.1f, 0.2f), goldC);
                CreatePartCube(root, new Vector3(0f, 1.45f, 3.05f), new Vector3(7.4f, 0.1f, 0.2f), goldC);
                CreatePartCube(root, new Vector3(-3.6f, 1.45f, 0f), new Vector3(0.2f, 0.1f, 6f), goldC);
                CreatePartCube(root, new Vector3(3.6f, 1.45f, 0f), new Vector3(0.2f, 0.1f, 6f), goldC);
                for (int lx = -1; lx <= 1; lx += 2)
                    for (int lz = -1; lz <= 1; lz += 2)
                    {
                        CreatePartCube(root, new Vector3(lx * 3.85f, 1.15f, lz * 3.25f), new Vector3(0.5f, 0.6f, 0.5f), goldC);
                        CreatePartCube(root, new Vector3(lx * 3.85f, 1.5f, lz * 3.25f), new Vector3(0.14f, 0.16f, 0.14f), goldC);
                    }
                break;

            case "Shrine_Roof2":
                CreatePartCubeRotated(root, new Vector3(0f, 0.45f, -2.4f), new Vector3(10.4f, 0.45f, 4f), tileC, Quaternion.Euler(-18f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(0f, 0.45f, 2.4f), new Vector3(10.4f, 0.45f, 4f), tileC, Quaternion.Euler(18f, 0f, 0f));
                CreatePartCubeRotated(root, new Vector3(-2.4f, 0.45f, 0f), new Vector3(4f, 0.45f, 9.6f), tileC, Quaternion.Euler(0f, 0f, 18f));
                CreatePartCubeRotated(root, new Vector3(2.4f, 0.45f, 0f), new Vector3(4f, 0.45f, 9.6f), tileC, Quaternion.Euler(0f, 0f, -18f));
                CreatePartCube(root, new Vector3(0f, 1.5f, 0f), new Vector3(4.4f, 0.4f, 4.4f), ridgeC);
                CreatePartCube(root, new Vector3(0f, 2f, 0f), new Vector3(0.6f, 0.4f, 0.6f), goldC);
                break;

            case "Shrine_Spire":
                CreatePartCube(root, new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 0.6f, 1.2f), ridgeC);
                CreatePartCube(root, new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 0.18f, 1.5f), goldC);
                CreatePartCube(root, new Vector3(0f, 2.6f, 0f), new Vector3(0.95f, 2.8f, 0.95f), goldC);
                CreatePartCube(root, new Vector3(0f, 4.4f, 0f), new Vector3(1.2f, 0.16f, 1.2f), goldC);
                CreatePartCube(root, new Vector3(0f, 5f, 0f), new Vector3(1f, 0.14f, 1f), goldC);
                CreatePartCube(root, new Vector3(0f, 5.5f, 0f), new Vector3(0.8f, 0.12f, 0.8f), goldC);
                CreatePartCube(root, new Vector3(0f, 6.2f, 0f), new Vector3(0.9f, 0.7f, 0.9f), goldC);
                CreatePartCubeRotated(root, new Vector3(0f, 6.9f, 0f), new Vector3(0.4f, 0.4f, 0.4f), goldC, Quaternion.Euler(45f, 0f, 45f));
                break;

            case "Shrine_Incense":
                CreatePartCube(root, new Vector3(0f, 0.25f, -3.6f), new Vector3(2f, 0.6f, 2f), stoneBase);
                CreatePartCube(root, new Vector3(-0.55f, 0.15f, -3.55f), new Vector3(0.3f, 0.7f, 0.3f), bronzeC);
                CreatePartCube(root, new Vector3(0.55f, 0.15f, -3.55f), new Vector3(0.3f, 0.7f, 0.3f), bronzeC);
                CreatePartCube(root, new Vector3(0f, 0.15f, -4.1f), new Vector3(0.3f, 0.7f, 0.3f), bronzeC);
                CreatePartCube(root, new Vector3(0f, 1.05f, -3.6f), new Vector3(1.9f, 0.8f, 1.9f), bronzeC);
                CreatePartCube(root, new Vector3(0f, 1.5f, -3.6f), new Vector3(2f, 0.1f, 2f), goldC);
                CreatePartCube(root, new Vector3(0f, 1.9f, -3.6f), new Vector3(1.3f, 0.7f, 1.3f), bronzeC);
                CreatePartCube(root, new Vector3(0f, 2.4f, -3.6f), new Vector3(0.32f, 0.35f, 0.32f), goldC);
                CreatePartCube(root, new Vector3(0f, 2.9f, -3.6f), new Vector3(0.14f, 0.6f, 0.14f), smokeC);
                CreatePartCube(root, new Vector3(0f, 3.3f, -3.6f), new Vector3(0.1f, 0.5f, 0.1f), smokeC);
                CreatePartCube(root, new Vector3(0f, 1.75f, -2.95f), new Vector3(0.8f, 0.15f, 0.5f), stoneBase);
                CreatePartCube(root, new Vector3(-0.2f, 2f, -2.95f), new Vector3(0.04f, 0.4f, 0.04f), goldC);
                CreatePartCube(root, new Vector3(0.2f, 2f, -2.95f), new Vector3(0.04f, 0.4f, 0.04f), goldC);
                break;

            case "Shrine_Deity":
                CreatePartCube(root, new Vector3(0f, 0.5f, 3f), new Vector3(2.4f, 0.9f, 1.5f), stoneBase);
                CreatePartCube(root, new Vector3(0f, 1f, 3f), new Vector3(2.5f, 0.1f, 1.6f), goldC);
                CreatePartCube(root, new Vector3(0f, 1.35f, 2.95f), new Vector3(2f, 0.35f, 1.3f), robeC);
                CreatePartCube(root, new Vector3(0f, 2.15f, 2.9f), new Vector3(1.2f, 1.1f, 0.9f), robeC);
                CreatePartCube(root, new Vector3(0f, 2.15f, 2.45f), new Vector3(0.24f, 1.1f, 0.2f), goldC);
                CreatePartCube(root, new Vector3(0f, 2.8f, 2.95f), new Vector3(1.9f, 0.5f, 0.95f), robeC);
                CreatePartCube(root, new Vector3(0f, 2.45f, 2.35f), new Vector3(0.5f, 0.5f, 0.3f), goldC);
                CreatePartCube(root, new Vector3(0f, 3.3f, 2.95f), new Vector3(0.75f, 0.7f, 0.75f), skinC);
                CreatePartCube(root, new Vector3(-0.16f, 3.35f, 3.28f), new Vector3(0.05f, 0.08f, 0.05f), blackC);
                CreatePartCube(root, new Vector3(0.16f, 3.35f, 3.28f), new Vector3(0.05f, 0.08f, 0.05f), blackC);
                CreatePartCube(root, new Vector3(0f, 3.7f, 2.95f), new Vector3(0.9f, 0.45f, 0.9f), blackC);
                CreatePartCube(root, new Vector3(0f, 3.55f, 2.95f), new Vector3(0.95f, 0.1f, 0.95f), goldC);
                CreatePartCube(root, new Vector3(0f, 3.95f, 2.95f), new Vector3(0.2f, 0.18f, 0.2f), goldC);
                CreatePartCube(root, new Vector3(1f, 2.15f, 2.9f), new Vector3(0.12f, 1.7f, 0.12f), goldC);
                CreatePartCubeRotated(root, new Vector3(1f, 3.05f, 2.9f), new Vector3(0.2f, 0.2f, 0.2f), goldC, Quaternion.Euler(45f, 0f, 45f));
                break;

            case "Shrine_Altar":
                CreatePartCube(root, new Vector3(0f, 0.45f, 1.6f), new Vector3(2.6f, 0.9f, 1.2f), woodDarkC);
                CreatePartCube(root, new Vector3(0f, 0.95f, 1.6f), new Vector3(2.7f, 0.08f, 1.3f), goldC);
                for (int cdx = -1; cdx <= 1; cdx += 2)
                {
                    CreatePartCube(root, new Vector3(cdx * 0.7f, 1.25f, 1.6f), new Vector3(0.14f, 0.5f, 0.14f), goldC);
                    CreatePartCube(root, new Vector3(cdx * 0.7f, 1.55f, 1.6f), new Vector3(0.07f, 0.14f, 0.07f), flameC);
                }
                CreatePartCube(root, new Vector3(0f, 1.1f, 1.6f), new Vector3(0.5f, 0.3f, 0.5f), bronzeC);
                CreatePartCube(root, new Vector3(-0.55f, 1.05f, 2f), new Vector3(0.4f, 0.2f, 0.4f), blackC);
                CreatePartCube(root, new Vector3(0.55f, 1.05f, 2f), new Vector3(0.4f, 0.2f, 0.4f), blackC);
                CreatePartCube(root, new Vector3(0f, 0.15f, 0.6f), new Vector3(2.2f, 0.1f, 1.2f), robeC);
                CreatePartCube(root, new Vector3(0f, 0.3f, 2.3f), new Vector3(1.8f, 0.3f, 0.6f), stoneBase);
                break;
        }
    }

    private GameObject RebuildEssentialBuilding(BlueprintState bp)
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
                SpawnBuffalo();
                break;
            case "WifeHouse":
                root = MapBuilder.BuildWifeHouse(_worldRoot.transform, bp.Position);
                break;
            case "RichMansion":
                root = MapBuilder.BuildRichManMansion(_worldRoot.transform, bp.Position, 1f, Quaternion.Euler(0f, bp.Rotation, 0f));
                break;
            case "Restaurant":
                root = MapBuilder.BuildRiceRestaurant(_worldRoot.transform, bp.Position);
                break;
            case "Cafe":
                root = MapBuilder.BuildCafe(_worldRoot.transform, bp.Position, 1f, Quaternion.Euler(0f, bp.Rotation, 0f));
                break;
            case "Library":
                root = MapBuilder.BuildLibrary(_worldRoot.transform, bp.Position, 1f, Quaternion.Euler(0f, bp.Rotation, 0f));
                break;
            case "NightClub":
                root = MapBuilder.BuildNightClub(_worldRoot.transform, bp.Position, 1f, Quaternion.Euler(0f, bp.Rotation, 0f));
                break;
        }
if (root != null)
        {
            SittableSeat.Register(root.transform);
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
        return root;
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

}

