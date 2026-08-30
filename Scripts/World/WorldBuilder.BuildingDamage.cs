using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;
using CountryLife.Helpers;

public partial class WorldBuilder
{
    private Camera _blueprintCam;

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
                        RemoveBuildingPartCracks(ps.Entity);
                        ReplacePartWithGhost(building, ps);
                    }
                    else
                    {
                        AddCracksToBuildingPart(ps.Entity);
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
                            RemoveBuildingPartCracks(ps.Entity);
                            ReplacePartWithGhost(building, ps);
                        }
                        else
                        {
                            AddCracksToBuildingPart(ps.Entity);
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
            else
                AddCracksToBuildingPart(building.Entity);
        }

        UpdateBuildingDurabilityLabel(building);
    }

    public void ApplyMeteorDamage(BuildingState building, float meteorScale)
    {
        if (building == null) return;
        int damage = Mathf.RoundToInt(meteorScale * 4f);
        DamageBuildingDirect(building, damage);
    }

    public void DamageBuildingDirect(BuildingState building, int damage)
    {
        if (building == null) return;
        if (building.PartStates != null && building.PartStates.Count > 0)
        {
            foreach (var ps in building.PartStates)
            {
                if (ps.Entity != null)
                    AddCracksToBuildingPart(ps.Entity);
            }

            int partsToDestroy = Mathf.Max(1, damage / 25);
            for (int i = 0; i < partsToDestroy && building.DestroyedParts < building.TotalParts; i++)
            {
                foreach (var ps in building.PartStates)
                {
                    if (ps.Entity != null)
                    {
                        RemoveBuildingPartCracks(ps.Entity);
                        ps.CurrentHealth = 0;
                        ReplacePartWithGhost(building, ps);
                        break;
                    }
                }
            }
            if (building.TotalParts > 0 && (float)building.DestroyedParts / building.TotalParts > 0.6f)
                RevertBuildingToBlueprint(building);
            else
                UpdateBuildingDurabilityLabel(building);
        }
        else
        {
            building.CurrentHealth -= damage;
            if (building.CurrentHealth <= 0)
                RevertBuildingToBlueprint(building);
            else
            {
                AddCracksToBuildingPart(building.Entity);
                UpdateBuildingDurabilityLabel(building);
            }
        }
    }

    public List<BuildingPartDebrisInfo> DamageBuildingDirectWithDebris(BuildingState building, int damage)
    {
        var debris = new List<BuildingPartDebrisInfo>();
        if (building == null) return debris;

        if (building.PartStates != null && building.PartStates.Count > 0)
        {
            foreach (var ps in building.PartStates)
            {
                if (ps.Entity != null)
                    AddCracksToBuildingPart(ps.Entity);
            }

            int partsToDestroy = Mathf.Max(1, damage / 25);
            int destroyed = 0;
            foreach (var ps in building.PartStates)
            {
                if (destroyed >= partsToDestroy) break;
                if (ps.Entity == null) continue;

                var info = new BuildingPartDebrisInfo
                {
                    LocalPosition = ps.Entity.transform.localPosition,
                    LocalRotation = ps.Entity.transform.localRotation,
                    LocalScale = ps.Entity.transform.localScale,
                    PartColor = GetPartColor(ps.Entity)
                };
                debris.Add(info);

                RemoveBuildingPartCracks(ps.Entity);
                ps.CurrentHealth = 0;
                ReplacePartWithGhost(building, ps);
                destroyed++;
            }

            if (building.TotalParts > 0 && (float)building.DestroyedParts / building.TotalParts > 0.6f)
                RevertBuildingToBlueprint(building);
            else
                UpdateBuildingDurabilityLabel(building);
        }
        else
        {
            var info = new BuildingPartDebrisInfo
            {
                LocalPosition = Vector3.zero,
                LocalRotation = Quaternion.identity,
                LocalScale = Vector3.one * 2f,
                PartColor = GetBuildingDebrisColorByType(building.Type)
            };
            debris.Add(info);

            building.CurrentHealth -= damage;
            if (building.CurrentHealth <= 0)
                RevertBuildingToBlueprint(building);
            else
            {
                AddCracksToBuildingPart(building.Entity);
                UpdateBuildingDurabilityLabel(building);
            }
        }

        return debris;
    }

    private Color GetPartColor(GameObject entity)
    {
        if (entity == null) return ColorPalette.StoneGray;
        var renderer = entity.GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
            return renderer.sharedMaterial.color;
        return ColorPalette.StoneGray;
    }

    private Color GetBuildingDebrisColorByType(string type)
    {
        switch (type)
        {
            case "PlayerHouse": return ColorPalette.HouseWood;
            case "WifeHouse": return ColorPalette.WifeHouseWood;
            case "Shop": return ColorPalette.ShopWood;
            default: return ColorPalette.StoneGray;
        }
    }

    private void ReplacePartWithGhost(BuildingState building, BuildingPartState ps)
    {
        if (ps.GhostEntity != null)
        {
            Object.Destroy(ps.GhostEntity);
            ps.GhostEntity = null;
            ps.GhostLabel = null;
        }

        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "PartGhost_" + ps.PartName;
        ghost.transform.SetParent(building.Entity.transform);
        ghost.transform.localPosition = ps.Entity.transform.localPosition;
        ghost.transform.localRotation = ps.Entity.transform.localRotation;
        ghost.transform.localScale = ps.Entity.transform.localScale;

        var renderer = ghost.GetComponent<MeshRenderer>();
        var mat = PickupVisualHelper.CreateTransparentMaterialFromBase(CreateSafeLitMaterial(), new Color(0.2f, 0.5f, 1f, 0.15f));
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
        ps.GhostLabel = labelObj.transform;
        float labelY = ps.Entity.transform.localScale.y * 0.5f + 0.3f;
        labelObj.transform.localPosition = new Vector3(0f, labelY, 0f);

        var tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.fontSize = 0.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        var parts = new List<string>();
        int ceilWood = Mathf.CeilToInt(woodCost);
        int ceilStone = Mathf.CeilToInt(stoneCost);
        if (ceilWood > 0) parts.Add(Localization.T("Gỗ:") + $" {ceilWood}");
        if (ceilStone > 0) parts.Add(Localization.T("Đá:") + $" {ceilStone}");
        tmp.text = string.Join(" ", parts);

        var ghostData = ghost.AddComponent<GhostPartData>();
        ghostData.LocalPosition = ps.Entity.transform.localPosition;
        ghostData.LocalRotation = ps.Entity.transform.localRotation;
        ghostData.LocalScale = ps.Entity.transform.localScale;
        var partRenderer = ps.Entity.GetComponent<Renderer>();
        ghostData.Color = partRenderer != null ? partRenderer.material.color : new Color(0.6f, 0.4f, 0.2f);

        Object.Destroy(ps.Entity);
        ps.Entity = null;
        ps.GhostEntity = ghost;
    }

    public bool TryRepairGhost(RaycastHit hit)
    {
        var hitGo = hit.collider.gameObject;
        foreach (var b in _buildings)
        {
            if (b.PartStates == null) continue;
            foreach (var ps in b.PartStates)
            {
                if (ps.GhostEntity == null) continue;
                bool isGhostHit = hitGo == ps.GhostEntity ||
                    (hitGo.transform.parent != null && hitGo.transform.parent.gameObject == ps.GhostEntity);
                if (!isGhostHit) continue;

                float woodCost = 1f, stoneCost = 0f;
                int partCount = b.TotalParts;
                if (partCount > 0)
                {
                    if (b.IsEssential)
                    {
                        GetEssentialCosts(b.Type, out float totalWood, out float totalStone);
                        woodCost = totalWood / partCount;
                        stoneCost = totalStone / partCount;
                    }
                    else
                    {
                        var def = System.Array.Find(_availableBuildings, d => d.Name == b.Type);
                        if (def != null)
                        {
                            woodCost = (float)def.WoodCost / partCount;
                            stoneCost = (float)def.StoneCost / partCount;
                        }
                    }
                }

                int needWood = Mathf.CeilToInt(woodCost);
                int needStone = Mathf.CeilToInt(stoneCost);
                var tm = ToolManager.Instance;
                if (tm == null) return false;
                if (tm.CountItem("wood") < needWood || tm.CountItem("stone") < needStone)
                {
                    GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Không đủ gỗ/đá để sửa chữa."), 1.5f);
                    return false;
                }

                var data = ps.GhostEntity.GetComponent<GhostPartData>();
                if (data == null) return false;

                tm.RemoveItemAmount("wood", needWood);
                tm.RemoveItemAmount("stone", needStone);

                var rebuilt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rebuilt.name = "BuildingPart_" + ps.PartName;
                rebuilt.transform.SetParent(b.Entity.transform);
                rebuilt.transform.localPosition = data.LocalPosition;
                rebuilt.transform.localRotation = data.LocalRotation;
                rebuilt.transform.localScale = data.LocalScale;
                var r = rebuilt.GetComponent<MeshRenderer>();
                if (r != null) r.material.color = data.Color;
                rebuilt.AddComponent<BoxCollider>();

                ps.Entity = rebuilt;
                ps.CurrentHealth = 4;
                Object.Destroy(ps.GhostEntity);
                ps.GhostEntity = null;
                ps.GhostLabel = null;
                b.CurrentHealth = Mathf.Min(b.MaxHealth, b.CurrentHealth + 4);
                UpdateBuildingDurabilityLabel(b);
                SoundManager.Instance?.Play("hammer");
                GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Đã sửa chữa xong."), 1.5f);
                return true;
            }
        }
        return false;
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

        if (building.PartStates == null || building.PartStates.Count == 0)
        {
            float damageFraction = 1f - (float)current / max;
            var def = System.Array.Find(_availableBuildings, d => d.Name == building.Type);
            if (def != null)
            {
                float repairWood = Mathf.Max(0, Mathf.Ceil(def.WoodCost * damageFraction));
                float repairStone = Mathf.Max(0, Mathf.Ceil(def.StoneCost * damageFraction));
                if (repairWood > 0 || repairStone > 0)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    if (repairWood > 0) parts.Add($"{Localization.T("Gỗ:")}{repairWood:F0}");
                    if (repairStone > 0) parts.Add($"{Localization.T("Đá:")}{repairStone:F0}");
                    text += "\n" + string.Join(" ", parts);
                }
            }
        }

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
                RemoveBuildingPartCracks(ps.Entity);
                if (ps.GhostEntity != null)
                {
                    Object.Destroy(ps.GhostEntity);
                    ps.GhostEntity = null;
                    ps.GhostLabel = null;
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
            HandleGoblinHutRemoval(state);
            CreateEssentialBlueprint(state);
            NavGrid.Instance?.MarkDirty();
            return;
        }

        var def = System.Array.Find(_availableBuildings, d => d.Name == state.Type);
        if (def == null) return;

        if (state.Entity != null)
            Object.Destroy(state.Entity);
        _buildings.Remove(state);
        HandleGoblinHutRemoval(state);
        NavGrid.Instance?.MarkDirty();

        var blueprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blueprint.name = "Blueprint";
        Vector3 size = def.Size;
        blueprint.transform.position = state.Position + Vector3.up * (size.y * 0.5f);
        blueprint.transform.rotation = Quaternion.Euler(0f, state.Rotation, 0f);
        blueprint.transform.localScale = size;
        var renderer = blueprint.GetComponent<MeshRenderer>();
        var mat = PickupVisualHelper.CreateTransparentMaterialFromBase(CreateSafeLitMaterial(), new Color(0.2f, 0.5f, 1f, 0.15f));
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
            StoneDeposited = 0,
            WoodCost = def.WoodCost,
            StoneCost = def.StoneCost
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
        var mat = PickupVisualHelper.CreateTransparentMaterialFromBase(CreateSafeLitMaterial(), new Color(0.2f, 0.5f, 1f, 0.15f));
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

    public void GetEssentialCosts(string type, out float wood, out float stone)
    {
        switch (type)
        {
            case "PlayerHouse": wood = 50; stone = 30; break;
            case "Shop":        wood = 40; stone = 20; break;
            case "WifeHouse":   wood = 60; stone = 40; break;
            case "RichMansion": wood = 80; stone = 50; break;
            case "Restaurant":  wood = 70; stone = 45; break;
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
        if (bp.IsMansion) return;
        if (!string.IsNullOrEmpty(bp.StructureId))
        {
            for (int i = _blueprints.Count - 1; i >= 0; i--)
            {
                var other = _blueprints[i];
                if (other != null && other.StructureId == bp.StructureId)
                {
                    DestroyBlueprintLabel(other);
                    if (other.Entity != null)
                        Object.Destroy(other.Entity);
                    _blueprints.RemoveAt(i);
                }
            }
            return;
        }
        DestroyBlueprintLabel(bp);
        if (bp.Entity != null)
            Object.Destroy(bp.Entity);
        _blueprints.Remove(bp);
    }

    private void CreateBlueprintLabel(GameObject blueprint, BlueprintState bp, BuildingDefinition def)
    {
        CreateBlueprintLabel(blueprint, bp, def.WoodCost, def.StoneCost, def.Size.y);
    }

    private void CreateBlueprintLabel(GameObject blueprint, BlueprintState bp, float woodCost, float stoneCost, float sizeY = 3f)
    {
        var labelObj = new GameObject("BlueprintLabel");
        labelObj.transform.SetParent(blueprint.transform, false);
        labelObj.transform.localPosition = Vector3.zero;

        var tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = GetBlueprintRemainingText(bp, woodCost, stoneCost);
        tmp.fontSize = Mathf.Clamp(sizeY * 0.18f, 0.5f, 1f);
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
            parts.Add(Localization.T("Gỗ:") + $" {woodRemaining:F1}");
        if (stoneRemaining > 0.01f)
            parts.Add(Localization.T("Đá:") + $" {stoneRemaining:F1}");
        if (parts.Count == 0)
            return Localization.T("Hoàn thành!");
        return Localization.T("Cần:") + " " + string.Join(", ", parts);
    }

    private void UpdateBlueprintLabels()
    {
        if (_blueprintCam == null)
            _blueprintCam = Camera.main;
        var cam = _blueprintCam;
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
                    if (ps.GhostLabel != null)
                    {
                        ps.GhostLabel.LookAt(ps.GhostLabel.position + cam.transform.rotation * Vector3.forward,
                            cam.transform.rotation * Vector3.up);
                    }
                }
            }
        }
    }

    public void RefreshBlueprintLabels()
    {
        foreach (var bp in _blueprints)
        {
            if (bp.Label == null) continue;
            var tmp = bp.Label.GetComponent<TextMeshPro>();
            if (tmp == null) continue;
            tmp.text = GetBlueprintRemainingText(bp, bp.WoodCost, bp.StoneCost);
        }
    }
}

