using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class WorldBuilder
{
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

        foreach (var kvp in _buildingPartCracks)
        {
            foreach (var crack in kvp.Value)
            {
                if (crack.Obj != null) Destroy(crack.Obj);
            }
        }
        _buildingPartCracks.Clear();

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
                {
                    ps.GhostEntity = null;
                    ps.GhostLabel = null;
                }
            }
        }
        _buildings.Clear();
        _floorPositions.Clear();
        _openDoors.Clear();

        foreach (var bp in _blueprints)
        {
            DestroyBlueprintLabel(bp);
            if (bp.Entity != null) Destroy(bp.Entity);
        }
        _blueprints.Clear();

        BlueprintAutoDeposit.ClearConsumedRoots();

_unlockedBlueprints.Clear();
        if (_savedVillagers != null) _savedVillagers.Clear();
    }

    public IEnumerable<FieldState> GetAllFields() => _fields;
    public IEnumerable<BuildingState> GetAllBuildings() => _buildings;

    public bool HasGoblinHut()
    {
        foreach (var b in _buildings)
        {
            if (b == null) continue;
            if (b.Type == "goblin_hut") return true;
        }
        return false;
    }

    private void HandleGoblinHutRemoval(BuildingState state)
    {
        if (state == null || state.Type != "goblin_hut") return;
        if (HasGoblinHut()) return;
        GameManager.Instance?.DespawnGoblin();
    }

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
                partHealths = partHealths,
                doorOpen = IsDoorOpen(b.Entity)
            };
        }
        return result;
    }

    private bool IsDoorOpen(GameObject root)
    {
        if (root == null)
            return false;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Door" && _openDoors.Contains(t.gameObject))
                return true;
        }
        return false;
    }

    private void ApplySavedDoorState(GameObject root, bool doorOpen)
    {
        if (root == null || !doorOpen)
            return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Door")
            {
                t.localRotation = Quaternion.Euler(0f, -90f, 0f);
                _openDoors.Add(t.gameObject);
                var panel = t.Find("DoorPanel");
                if (panel != null)
                {
                    var col = panel.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                }
                return;
            }
        }
    }

    public void LoadBuildingsFromSave(BuildingSaveData[] data)
    {
        if (data == null)
            return;

        foreach (var build in data)
        {
            if (build.type != null && build.type.StartsWith("structure_part_"))
            {
                SpawnStructurePart(new BlueprintState
                {
                    Type = build.type.Replace("structure_part_", ""),
                    Position = build.position,
                    Rotation = build.rotation
                });
                var lastPart = _buildings[_buildings.Count - 1];
                lastPart.CurrentHealth = build.currentHealth;
                lastPart.MaxHealth = build.maxHealth;
                ApplySavedDoorState(lastPart.Entity, build.doorOpen);
                continue;
            }

            if (build.type == "PlayerHouse" || build.type == "Shop" || build.type == "WifeHouse" || build.type == "RichMansion" || build.type == "Restaurant" || build.type == "Cafe" || build.type == "Library" || build.type == "NightClub")
            {
                RebuildEssentialBuilding(new BlueprintState
                {
                    Type = build.type,
                    Position = build.position,
                    Rotation = build.rotation
                });
                var lastEssential = _buildings[_buildings.Count - 1];
                lastEssential.CurrentHealth = build.currentHealth;
                lastEssential.MaxHealth = build.maxHealth;
                if (lastEssential.PartStates != null && build.partHealths != null)
                {
                    int essentialCount = Mathf.Min(lastEssential.PartStates.Count, build.partHealths.Length);
                    for (int ep = 0; ep < essentialCount; ep++)
                        lastEssential.PartStates[ep].CurrentHealth = build.partHealths[ep];
                }
                ApplySavedDoorState(lastEssential.Entity, build.doorOpen);
                continue;
            }

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
                ApplySavedDoorState(last.Entity, build.doorOpen);
            }
        }

PruneTreesAndRocksNearStructures();
        RebuildFloorPositions();
    }

private void PruneTreesAndRocksNearStructures()
    {
        float exHalf = PagodaExcludeHalf;
        PruneTreesAndRocksInBox(PagodaBasePos.x, PagodaBasePos.z, exHalf, exHalf);
        PruneTreesAndRocksInBox(_bossArenaCenter.x, _bossArenaCenter.z, 12f, 12f);
    }

    private void PruneTreesAndRocksInBox(float cx, float cz, float halfX, float halfZ)
    {
        for (int i = _trees.Count - 1; i >= 0; i--)
        {
            var t = _trees[i];
            if (t == null)
            {
                _trees.RemoveAt(i);
                continue;
            }
            var p = t.transform.position;
            if (Mathf.Abs(p.x - cx) <= halfX && Mathf.Abs(p.z - cz) <= halfZ)
            {
                Destroy(t);
                _trees.RemoveAt(i);
            }
        }
        for (int i = _rocks.Count - 1; i >= 0; i--)
        {
            var r = _rocks[i];
            if (r == null)
            {
                _rocks.RemoveAt(i);
                continue;
            }
            var p = r.transform.position;
            if (Mathf.Abs(p.x - cx) <= halfX && Mathf.Abs(p.z - cz) <= halfZ)
            {
                Destroy(r);
                _rocks.RemoveAt(i);
            }
        }
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
        public bool doorOpen;
    }

    [System.Serializable]
    public class MansionBlueprintSaveData
    {
        public string type;
        public Vector3 position;
        public float woodDeposited;
        public float stoneDeposited;
        public string structureId;
        public bool completed;
    }

    public MansionBlueprintSaveData[] GetMansionBlueprintsAsSave()
    {
        var result = new List<MansionBlueprintSaveData>();
        foreach (var bp in _blueprints)
        {
            if (!bp.IsMansion) continue;
            result.Add(new MansionBlueprintSaveData
            {
                type = bp.Type,
                position = bp.Position,
                woodDeposited = bp.WoodDeposited,
                stoneDeposited = bp.StoneDeposited,
                structureId = bp.StructureId,
                completed = false
            });
        }
        foreach (var b in _buildings)
        {
            if (b.Type != "RichMansion") continue;
            result.Add(new MansionBlueprintSaveData
            {
                type = "Mansion",
                position = b.Position,
                woodDeposited = 0,
                stoneDeposited = 0,
                structureId = "mansion",
                completed = true
            });
        }
        return result.ToArray();
    }

    public void LoadMansionBlueprintsFromSave(MansionBlueprintSaveData[] data)
    {
        if (data == null || data.Length == 0)
        {
            PlaceMansionBlueprint(MansionBasePos);
            return;
        }

        foreach (var saved in data)
        {
            if (saved.completed)
            {
                if (!HasMansionStructure())
                    BuildRichManMansion();
                continue;
            }

            var subDef = _mansionSubBuildings[0];
            Vector3 partPos = MansionBasePos;
            Vector3 rawSize = subDef.Size;
            Vector3 rotatedSize = new Vector3(rawSize.z, rawSize.y, rawSize.x);
            var bpState = CreateMansionBlueprint(subDef.PartName, partPos, rotatedSize, subDef.Color, subDef.WoodCost, subDef.StoneCost);
            bpState.WoodDeposited = saved.woodDeposited;
            bpState.StoneDeposited = saved.stoneDeposited;
            bpState.StructureId = saved.structureId;
            bpState.IsMansion = true;

            if (bpState.Label != null)
            {
                var tmp = bpState.Label.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null)
                    tmp.text = GetBlueprintRemainingText(bpState, subDef.WoodCost, subDef.StoneCost);
            }

            if (bpState.WoodDeposited >= subDef.WoodCost && bpState.StoneDeposited >= subDef.StoneCost)
            {
                CompleteBlueprint(bpState, null);
            }
            else
            {
                _blueprints.Add(bpState);
            }
        }
    }

    private void SpawnMansionPartDirect(string typeName, Vector3 position)
    {
        var fakeBp = new BlueprintState
        {
            Type = typeName,
            Position = position,
            Rotation = -90,
            IsMansion = true
        };
        SpawnStructurePart(fakeBp);
    }
}

