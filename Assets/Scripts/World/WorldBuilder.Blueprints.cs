using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;
using CountryLife.Helpers;

public partial class WorldBuilder
{
    public BuildingDefinition GetBuildingByIndex(int i) => _availableBuildings[i];

    public bool IsBlueprintUnlocked(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
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
        if (IsWallOrStair(definition.Name) && !HasFloorAt(snapped))
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

    public void PlaceNextImmigrantBlueprint()
    {
        if (_immigrantHousePositions == null || _immigrantBuilt == null)
            return;
        if (_nextImmigrantIndex >= _immigrantHousePositions.Count)
            return;
        if (_immigrantBuilt[_nextImmigrantIndex])
            return;
        IsImmigrantVillagePlaced = true;
        CreateImmigrantHouseBlueprint("small_house", _immigrantHousePositions[_nextImmigrantIndex], _nextImmigrantIndex);
    }

    public Vector3 GetImmigrantBlueprintPosition()
    {
        if (_immigrantHousePositions == null || _nextImmigrantIndex >= _immigrantHousePositions.Count)
            return Vector3.zero;
        return _immigrantHousePositions[_nextImmigrantIndex];
    }

    public void PlaceAllRemainingImmigrantBlueprints()
    {
        if (_immigrantHousePositions == null || _immigrantBuilt == null)
            return;
        IsImmigrantVillagePlaced = true;
        for (int i = 0; i < _immigrantHousePositions.Count; i++)
        {
            if (!_immigrantBuilt[i])
                CreateImmigrantHouseBlueprint("small_house", _immigrantHousePositions[i]);
        }
    }

    private void CreateImmigrantHouseBlueprint(string typeName, Vector3 position, int houseIndex = -1)
    {
        var blueprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueprint.name = "Blueprint";
        blueprint.transform.position = position + Vector3.up * (5f * 0.5f);
        blueprint.transform.rotation = Quaternion.identity;
        blueprint.transform.localScale = new Vector3(8f, 5f, 8f);
        var renderer = blueprint.GetComponent<MeshRenderer>();
        var mat = PickupVisualHelper.CreateTransparentMaterialFromBase(CreateSafeLitMaterial(), new Color(0.5f, 0.8f, 0.95f, 0.45f));
        renderer.material = mat;
        var collider = blueprint.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        blueprint.transform.SetParent(_worldRoot.transform);

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "BlueprintMarker";
        marker.transform.SetParent(blueprint.transform);
        marker.transform.localPosition = new Vector3(0f, -2.5f, 0f);
        marker.transform.localScale = new Vector3(9f, 0.05f, 9f);
        var markerMat = CreateSafeLitMaterial();
        if (markerMat != null)
            markerMat.color = new Color(0.2f, 0.6f, 1f, 0.7f);
        else
            markerMat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse")) { color = new Color(0.2f, 0.6f, 1f, 0.7f) };
        marker.GetComponent<MeshRenderer>().material = markerMat;
        var markerCol = marker.GetComponent<Collider>();
        if (markerCol != null) Object.Destroy(markerCol);

        var bpState = new BlueprintState
        {
            Entity = blueprint,
            Type = typeName,
            Position = position,
            Rotation = 0,
            WoodDeposited = 0,
            StoneDeposited = 0,
            WoodCost = _immigrantHouseWoodCost,
            StoneCost = _immigrantHouseStoneCost,
            IsImmigrantHouse = true,
            ImmigrantHouseIndex = houseIndex
        };
        CreateBlueprintLabel(blueprint, bpState, _immigrantHouseWoodCost, _immigrantHouseStoneCost, 5f);
        blueprint.AddComponent<BlueprintAutoDeposit>();
        _blueprints.Add(bpState);
        HideImmigrantMarker(houseIndex);
    }

    private void SpawnImmigrantFamily(int houseIndex)
    {
        if (_immigrantHousePositions == null || houseIndex < 0 || houseIndex >= _immigrantHousePositions.Count)
            return;
        Vector3 basePos = _immigrantHousePositions[houseIndex] + new Vector3(3.2f, 0.93f, 2.5f);
        int familySize = Random.Range(1, 5);
        for (int i = 0; i < familySize; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1f, 1f));
            float rotY = Random.Range(0f, 360f);
            var variation = MapBuilder.ImmigrantVariation.Random();
            var villager = MapBuilder.BuildImmigrantNpc(_worldRoot.transform, variation, basePos + offset, Quaternion.Euler(0f, rotY, 0f));
            villager.name = "ImmigrantVillager";
            RecordVillager(houseIndex, basePos + offset, rotY, variation);
        }
    }

    private void RecordVillager(int houseIndex, Vector3 pos, float rotY, MapBuilder.ImmigrantVariation v)
    {
        if (_savedVillagers == null) _savedVillagers = new List<VillagerSaveData>();
        _savedVillagers.Add(new VillagerSaveData
        {
            HouseIndex = houseIndex,
            Position = pos,
            RotationY = rotY,
            SkinColor = v.SkinColor, ShirtColor = v.ShirtColor, PantsColor = v.PantsColor,
            BootColor = v.BootColor, HatColor = v.HatColor, BundleColor = v.BundleColor,
            HairColor = v.HairColor, EyeSpacing = v.EyeSpacing, HeadScale = v.HeadScale,
            ArmLength = v.ArmLength, BodyWidth = v.BodyWidth, HeightOffset = v.HeightOffset,
            LegWidth = v.LegWidth, HatTilt = v.HatTilt, HasBeard = v.HasBeard, RolledSleeves = v.RolledSleeves
        });
    }

    public void RestoreSavedVillagers(List<VillagerSaveData> data)
    {
        if (data == null) return;
        _savedVillagers = new List<VillagerSaveData>(data);
        foreach (var vd in data)
        {
            var v = new MapBuilder.ImmigrantVariation
            {
                SkinColor = vd.SkinColor, ShirtColor = vd.ShirtColor, PantsColor = vd.PantsColor,
                BootColor = vd.BootColor, HatColor = vd.HatColor, BundleColor = vd.BundleColor,
                HairColor = vd.HairColor, EyeSpacing = vd.EyeSpacing, HeadScale = vd.HeadScale,
                ArmLength = vd.ArmLength, BodyWidth = vd.BodyWidth, HeightOffset = vd.HeightOffset,
                LegWidth = vd.LegWidth, HatTilt = vd.HatTilt, HasBeard = vd.HasBeard, RolledSleeves = vd.RolledSleeves
            };
            var villager = MapBuilder.BuildImmigrantNpc(_worldRoot.transform, v, vd.Position, Quaternion.Euler(0f, vd.RotationY, 0f));
            villager.name = "ImmigrantVillager";
        }
    }

    public List<VillagerSaveData> GetVillagerSaves() { return _savedVillagers; }

    [System.Serializable]
    public class VillagerSaveData
    {
        public int HouseIndex;
        public Vector3 Position;
        public float RotationY;
        public Color SkinColor, ShirtColor, PantsColor, BootColor, HatColor, BundleColor, HairColor;
        public float EyeSpacing, HeadScale, ArmLength, BodyWidth, HeightOffset, LegWidth, HatTilt;
        public bool HasBeard, RolledSleeves;
    }

    public bool[] GetImmigrantBuiltArray() { return _immigrantBuilt; }
    public int GetImmigrantNextIndex() { return _nextImmigrantIndex; }
    public bool IsImmigrantVillagePlacedState() { return IsImmigrantVillagePlaced; }

    public void LoadImmigrantVillageFromSave(bool[] built, int nextIndex, bool placed)
    {
        if (built == null && nextIndex == 0 && !placed)
            return;
        if (_immigrantHousePositions == null)
            GenerateImmigrantPositions();
        if (built != null && built.Length == _immigrantHousePositions.Count)
        {
            _immigrantBuilt = built;
            _nextImmigrantIndex = nextIndex;
            ImmigrantHousesBuilt = 0;
            for (int i = 0; i < built.Length; i++)
            {
                if (built[i])
                {
                    ImmigrantHousesBuilt++;
                    HideImmigrantMarker(i);
                }
            }
        }
        IsImmigrantVillagePlaced = placed;
    }

    public bool GetImmigrantArrived() { return ImmigrantHousesBuilt > 0 || ImmigrantNpc.Instance != null; }

    public void RestoreImmigrantArrival()
    {
        if (_immigrantHousePositions == null)
            GenerateImmigrantPositions();
        if (_nextImmigrantIndex >= _immigrantHousePositions.Count)
            return;
        if (ImmigrantNpc.Instance != null)
            return;
        var npc = MapBuilder.BuildImmigrantNpc(_worldRoot.transform, new Vector3(22f, 0.93f, -22f), Quaternion.Euler(0f, -90f, 0f));
        npc.AddComponent<ImmigrantNpc>();
    }

    public void StartImmigrantArrival()
    {
        if (_immigrantHousePositions == null)
            GenerateImmigrantPositions();
        if (_nextImmigrantIndex >= _immigrantHousePositions.Count)
            return;
        if (ImmigrantNpc.Instance != null)
        {
            if (ImmigrantNpc.Instance.IsDialogActive)
                return;
            var old = ImmigrantNpc.Instance;
            ImmigrantNpc.ClearInstance();
            Object.Destroy(old.gameObject);
        }
        StartCoroutine(RunImmigrantArrival());
    }

    private System.Collections.IEnumerator RunImmigrantArrival()
    {
        const float roadX = 14f;
        const float startZ = -95f;
        const float stopZ = -22f;
        const float departZ = 40f;
        const float carSpeed = 9f;
        const float walkSpeed = 8f;

        var car = MapBuilder.BuildCar(_worldRoot.transform, new Vector3(roadX, 0f, startZ));
        var wheels = new List<Transform>();
        foreach (var child in car.GetComponentsInChildren<Transform>())
        {
            if (child.name.StartsWith("Wheel"))
                wheels.Add(child);
        }

        while (Vector3.Distance(car.transform.position, new Vector3(roadX, 0f, stopZ)) > 0.2f)
        {
            car.transform.position = Vector3.MoveTowards(car.transform.position, new Vector3(roadX, 0f, stopZ), carSpeed * Time.deltaTime);
            foreach (var w in wheels)
                if (w != null)
                    w.Rotate(360f * Time.deltaTime, 0f, 0f);
            yield return null;
        }
        car.transform.position = new Vector3(roadX, 0f, stopZ);

        var npc = MapBuilder.BuildImmigrantNpc(_worldRoot.transform, new Vector3(roadX + 0.9f, 0.93f, stopZ), Quaternion.Euler(0f, -90f, 0f));
        npc.AddComponent<ImmigrantNpc>();

        Vector3 target = new Vector3(22f, 0.93f, -22f);
        while (Vector3.Distance(npc.transform.position, target) > 0.2f)
        {
            npc.transform.position = Vector3.MoveTowards(npc.transform.position, target, walkSpeed * Time.deltaTime);
            Vector3 dir = target - npc.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                npc.transform.rotation = Quaternion.LookRotation(dir.normalized);
            yield return null;
        }
        npc.transform.position = target;
        npc.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        while (Vector3.Distance(car.transform.position, new Vector3(roadX, 0f, departZ)) > 0.2f)
        {
            car.transform.position = Vector3.MoveTowards(car.transform.position, new Vector3(roadX, 0f, departZ), carSpeed * Time.deltaTime);
            foreach (var w in wheels)
                if (w != null)
                    w.Rotate(360f * Time.deltaTime, 0f, 0f);
            yield return null;
        }
        Object.Destroy(car);

        if (GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.ShowMessage(Localization.T("Người di cư đã đến làng! Hãy đến chào họ."), 3f);
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
        if (bp.IsEssential || bp.IsMansion || bp.IsImmigrantHouse)
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
        else if (bp.IsImmigrantHouse)
        {
            SpawnBuildingDirect("small_house", bp.Position, bp.Rotation);
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
        if (bp.IsImmigrantHouse)
        {
            int idx = bp.ImmigrantHouseIndex >= 0 ? bp.ImmigrantHouseIndex
                : (_immigrantHousePositions != null ? _immigrantHousePositions.IndexOf(bp.Position) : -1);
            if (idx >= 0 && idx < _immigrantHousePositions.Count && _immigrantBuilt != null && !_immigrantBuilt[idx])
            {
                _immigrantBuilt[idx] = true;
                ImmigrantHousesBuilt++;
                if (idx == _nextImmigrantIndex)
                    _nextImmigrantIndex++;
                QuestManager.Instance?.AddProgress(_immigrantQuestTarget, 1);
                SpawnImmigrantFamily(idx);
            }
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

