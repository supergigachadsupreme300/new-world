using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;
using CountryLife.Helpers;

public partial class WorldBuilder
{
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

    public bool IsWallOrStair(string typeName)
    {
        return typeName == "wood_wall" || typeName == "stone_wall" || typeName == "stair";
    }

    public bool HasFloorAt(Vector3 position)
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
        float beachX = -180f;
        float sandW = 70f;
        float sandD = 600f;
        Color sandC = new Color(0.85f, 0.76f, 0.55f);
        Color seaC = new Color(0.2f, 0.5f, 0.8f);

        MakeBlock("Sand", _worldRoot.transform, new Vector3(sandW, 0.02f, sandD),
            new Vector3(beachX, 0f, 0f), sandC, false, true);

        var seaBlock = MakeBlock("Sea", _worldRoot.transform, new Vector3(240f, 0.06f, sandD),
            new Vector3(beachX - sandW * 0.5f - 120f, 0.03f, 0f), seaC, true, false);

        float seaZ = beachX - sandW * 0.5f - 120f;
        var waterVol = new GameObject("SeaWater");
        waterVol.transform.SetParent(_worldRoot.transform);
        waterVol.transform.localPosition = new Vector3(seaZ, 1.5f, 0f);
        var wc = waterVol.AddComponent<BoxCollider>();
        wc.size = new Vector3(240f, 4f, sandD);
        wc.isTrigger = true;
        waterVol.AddComponent<WaterVolume>();

        int numTrees = 30;
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

    private void BuildRestaurant()
    {
        var restaurant = MapBuilder.BuildRiceRestaurant(_worldRoot.transform, new Vector3(0f, 0f, 75f));
        _buildings.Add(new BuildingState
        {
            Entity = restaurant,
            Type = "Restaurant",
            Position = restaurant.transform.position,
            Rotation = 0,
            PartStates = CollectColliderParts(restaurant, "Restaurant"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
    }

    private void BuildCafe()
    {
        var cafe = MapBuilder.BuildCafe(_worldRoot.transform, new Vector3(0f, 0f, 45f), 1f, Quaternion.Euler(0f, 90f, 0f));
        _buildings.Add(new BuildingState
        {
            Entity = cafe,
            Type = "Cafe",
            Position = cafe.transform.position,
            Rotation = 90,
            PartStates = CollectColliderParts(cafe, "Cafe"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
    }

    private void BuildLibrary()
    {
        var library = MapBuilder.BuildLibrary(_worldRoot.transform, new Vector3(-2f, 0f, 30f));
        _buildings.Add(new BuildingState
        {
            Entity = library,
            Type = "Library",
            Position = library.transform.position,
            Rotation = 0,
            PartStates = CollectColliderParts(library, "Library"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
        var librarian = MapBuilder.BuildLibrarianNpc(_worldRoot.transform, new Vector3(-2f, 0.95f, 26.62f), Quaternion.Euler(0f, 180f, 0f));
        librarian.AddComponent<LibrarianNPC>();
    }

    private void BuildNightClub()
    {
        var club = MapBuilder.BuildNightClub(_worldRoot.transform, new Vector3(0f, 0f, 95f), 1f, Quaternion.Euler(0f, 90f, 0f));
        _buildings.Add(new BuildingState
        {
            Entity = club,
            Type = "NightClub",
            Position = club.transform.position,
            Rotation = 90,
            PartStates = CollectColliderParts(club, "NightClub"),
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
        StaticWifeModel = WifeNPC.BuildWifeNpc(_worldRoot.transform, new Vector3(30f, 0.86f, 0f), 1f, Quaternion.Euler(0f, 90f, 0f));
        WifeDonationField.Build(_worldRoot.transform, new Vector3(33f, 0.5f, -10.5f));
    }

    private void BuildRichManMansion()
    {
        var mansion = MapBuilder.BuildRichManMansion(_worldRoot.transform, new Vector3(60f, 0f, 145f), 1f, Quaternion.Euler(0f, -90f, 0f));
        _buildings.Add(new BuildingState
        {
            Entity = mansion,
            Type = "RichMansion",
            Position = mansion.transform.position,
            Rotation = -90,
            PartStates = CollectColliderParts(mansion, "RichMansion"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
        RichManNPC.BuildRichManNpc(_worldRoot.transform, new Vector3(60f, 0.86f, 141f), 1f, Quaternion.Euler(0f, 90f, 0f));
    }

    private void BuildFishingShop()
    {
        var shop = MapBuilder.BuildFishingShop(_worldRoot.transform, new Vector3(30f, 0f, 46f), 1f, Quaternion.Euler(0f, 180f, 0f));
        _buildings.Add(new BuildingState
        {
            Entity = shop,
            Type = "FishingShop",
            Position = shop.transform.position,
            Rotation = 180,
            PartStates = CollectColliderParts(shop, "FishingShop"),
            CurrentHealth = 100,
            MaxHealth = 100,
            IsEssential = true
        });
        MapBuilder.BuildFishingShopkeeper(_worldRoot.transform, new Vector3(30f, 0.85f, 46f), Quaternion.Euler(0f, 0f, 0f));
        var fishNpc = _worldRoot.transform.Find("FishingShopNPC");
        if (fishNpc != null)
            fishNpc.gameObject.AddComponent<FishingShopNPC>();
    }

    private void SpawnBuffalo()
    {
        if (_shopRoot == null) return;
        MapBuilder.BuildBuffalo(_shopRoot, new Vector3(-3.2f, 0f, 0f), 1.5f, Quaternion.Euler(0f, 270f, 0f));
    }

    private bool IsReservedSpawnLocation(int x, int z)
    {
bool nearHouse = Mathf.Abs(x) <= 9 && Mathf.Abs(z) <= 9;
        bool nearShop = Mathf.Abs(x) <= 9 && z >= 51 && z <= 69;
        bool nearStore = x >= 20 && x <= 32 && z >= 51 && z <= 69;
        bool nearRestaurant = Mathf.Abs(x) <= 10 && z >= 66 && z <= 84;
        bool nearRoad = x >= (_roadCenterX - _roadHalfWidth - 3f) && x <= (_roadCenterX + _roadHalfWidth + 3f)
                        && z >= _roadZStart - 10f && z <= _roadZEnd + 10f;
        bool nearRoadTurn = x >= (_roadCenterX - 3f) && x <= (_roadXEnd + 3f)
                        && z >= (_roadTurnZ - _roadHalfWidth - 3f) && z <= (_roadTurnZ + _roadHalfWidth + 3f);
        bool nearPolicePost = x >= 18 && x <= 40 && z >= 72 && z <= 92;
        bool nearWifeHouse = x >= 20 && x <= 42 && Mathf.Abs(z) <= 10;
        bool nearRichMansion = x >= 42 && x <= 78 && z >= 127 && z <= 163;
        bool nearFishingShop = x >= 21 && x <= 39 && z >= 37 && z <= 55;
        bool nearMansion = x >= -14 && x <= 14 && z >= -42 && z <= -18;
        bool nearPagoda = Mathf.Abs(x - PagodaBasePos.x) <= PagodaExcludeHalf && Mathf.Abs(z - PagodaBasePos.z) <= PagodaExcludeHalf;
        bool nearCafe = Mathf.Abs(x) <= 10 && z >= 33 && z <= 57;
        bool nearLibrary = x >= -9 && x <= 6 && z >= 24 && z <= 38;
        bool nearClub = x >= -12 && x <= 12 && z >= 84 && z <= 106;
        bool nearClubCorridor = x >= -12 && x <= 40 && z >= 84 && z <= 106;
        bool nearSouthBranch = x >= -123 && x <= 17 && z >= -54 && z <= -46;
        bool nearNorthBranch = x >= 11 && x <= 153 && z >= 173 && z <= 187;
        bool nearBossArena = Mathf.Abs(x - _bossArenaCenter.x) <= 12 && Mathf.Abs(z - _bossArenaCenter.z) <= 12;
        bool nearImmigrantPlot = IsNearImmigrantPlot(x, z);
        return nearHouse || nearShop || nearStore || nearRestaurant || nearRoad || nearRoadTurn || nearPolicePost || nearWifeHouse || nearRichMansion || nearFishingShop || nearMansion || nearPagoda || nearCafe || nearLibrary || nearClub || nearClubCorridor || nearSouthBranch || nearNorthBranch || nearBossArena || nearImmigrantPlot;
    }

    private bool IsNearImmigrantPlot(int x, int z)
    {
        if (_immigrantHousePositions == null) return false;
        for (int i = 0; i < _immigrantHousePositions.Count; i++)
        {
            var p = _immigrantHousePositions[i];
            if (Mathf.Abs(x - p.x) <= 7f && Mathf.Abs(z - p.z) <= 7f)
                return true;
        }
return false;
    }

    private void BuildPolicePost()
    {
        Vector3 postPos = new Vector3(30f, 0f, 78f);
        _policePostRoot = MapBuilder.BuildPoliceStation(_worldRoot.transform, postPos, Quaternion.Euler(0f, -90f, 0f));
        _policeOfficerRoot = MapBuilder.BuildPoliceOfficer(_worldRoot.transform, new Vector3(21.5f, 0.93f, 78f), Quaternion.Euler(0f, 90f, 0f));
        _policeOfficerRoot.AddComponent<PoliceOfficerNPC>();
        MapBuilder.MakeBlock("ParkingPad", _worldRoot.transform,
            new Vector3(2.7f, 0.06f, 4.6f), new Vector3(24.2f, 0.03f, 82.8f),
            new Color(0.55f, 0.55f, 0.56f), true);
        var car = MapBuilder.BuildPoliceCar(_worldRoot.transform, new Vector3(24.2f, 0f, 82.8f));
        car.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        _policeCarRoot = car;
    }

    public bool TryToggleDoor(RaycastHit hit)
    {
        var door = FindDoor(hit.collider.gameObject);
        if (door == null) return false;
        if (_openDoors.Contains(door)) _openDoors.Remove(door); else _openDoors.Add(door);
        StartCoroutine(AnimateDoor(door));
        return true;
    }

    private GameObject FindDoor(GameObject obj)
    {
        if (obj.name == "Door") return obj;
        if (obj.transform.parent != null && obj.transform.parent.name == "Door") return obj.transform.parent.gameObject;
        return null;
    }

    private System.Collections.IEnumerator AnimateDoor(GameObject door)
    {
        bool isOpen = _openDoors.Contains(door);
        float start = door.transform.localRotation.eulerAngles.y;
        if (start > 180f) start -= 360f;

        float swingDir = -90f;
        var panel = door.transform.Find("DoorPanel");
        if (panel != null && panel.localPosition.z < 0f) swingDir = 90f;
        float end = isOpen ? swingDir : 0f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            float angle = Mathf.LerpAngle(start, end, t);
            door.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        door.transform.localRotation = Quaternion.Euler(0f, end, 0f);
        if (panel != null)
        {
            var col = panel.GetComponent<Collider>();
            if (col != null) col.enabled = !isOpen;
        }
    }

    private void CloseAllDoors()
    {
        if (_worldRoot == null)
            return;

        foreach (var door in _worldRoot.GetComponentsInChildren<Transform>(true))
        {
            if (door.name == "Door")
                door.localRotation = Quaternion.identity;
        }
        _openDoors.Clear();
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
        SoundManager.Instance?.Play("mexican_truck");
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

        // â”€â”€ Classic food truck â”€â”€
        // Identity rotation: local +Z = world +Z = front (movement), local -X = world -X = window toward player

        float halfW = 1.3f;  // half width (X), truck is 2.6m wide
        float halfD = 1.8f;  // half depth (Z), truck is 3.6m long
        float wallH = 1.8f;
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

        // â”€â”€ Front face (local +Z) â”€â”€
        MakeBlock("WallFront", modelRoot.transform, new Vector3(halfW * 2f - 0.2f, wallH, 0.15f),
            new Vector3(0f, floorY + wallH * 0.5f, cabFrontZ), cartColor, true);
        MakeBlock("Bumper", modelRoot.transform, new Vector3(halfW * 2f - 0.4f, 0.3f, 0.2f),
            new Vector3(0f, 0.15f, cabFrontZ + 0.6f), Color.gray, true);
        MakeBlock("Grille", modelRoot.transform, new Vector3(halfW * 2f - 0.4f, 0.5f, 0.1f),
            new Vector3(0f, floorY + 0.35f, cabFrontZ + 0.55f), new Color(0.15f, 0.15f, 0.15f), true);
        MakeBlock("Windshield", modelRoot.transform, new Vector3(halfW * 2f - 0.6f, 0.7f, 0.1f),
            new Vector3(0f, floorY + 1.05f, cabFrontZ + 0.01f), new Color(0.5f, 0.75f, 1f), true);
        // â”€â”€ Hood (between grille and windshield) â”€â”€
        MakeBlock("Hood", modelRoot.transform, new Vector3(halfW * 2f - 0.4f, 0.5f, 0.5f),
            new Vector3(0f, floorY + 0.85f, cabFrontZ + 0.25f), darkColor, true);
        MakeBlock("HeadlightL", modelRoot.transform, new Vector3(0.2f, 0.2f, 0.08f),
            new Vector3(-halfW + 0.3f, floorY + 0.5f, cabFrontZ + 0.55f), Color.white, true);
        MakeBlock("HeadlightR", modelRoot.transform, new Vector3(0.2f, 0.2f, 0.08f),
            new Vector3(halfW - 0.3f, floorY + 0.5f, cabFrontZ + 0.55f), Color.white, true);

        // â”€â”€ Back wall (local -Z) â”€â”€
        MakeBlock("WallBack", modelRoot.transform, new Vector3(halfW * 2f - 0.2f, wallH, 0.15f),
            new Vector3(0f, floorY + wallH * 0.5f, -halfD), cartColor, true);

        // â”€â”€ Right wall (local +X) â€” solid, full length â”€â”€
        MakeBlock("WallRight", modelRoot.transform, new Vector3(0.2f, wallH, halfD * 2f - 0.2f),
            new Vector3(halfW, floorY + wallH * 0.5f, 0f), cartColor, true);

        // â”€â”€ Left side (local -X) â€” cab wall + counter (window above) + back wall â”€â”€
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

        // â”€â”€ Roof â”€â”€
        MakeBlock("Roof", modelRoot.transform, new Vector3(halfW * 2f + 0.4f, 0.2f, halfD * 2f + 0.6f),
            new Vector3(0f, roofY, 0f), darkColor, true);

        // â”€â”€ Awning over the window (left side) â”€â”€
        MakeBlock("Awning", modelRoot.transform, new Vector3(0.5f, 0.1f, winLen + 0.2f),
            new Vector3(xL - 0.3f, roofY - 0.05f, winCenterZ), darkColor, true);

        // â”€â”€ Stripe along the body (on right wall) â”€â”€
        MakeBlock("Stripe", modelRoot.transform, new Vector3(0.08f, 0.08f, halfD * 2f - 0.4f),
            new Vector3(halfW + 0.06f, floorY + 0.45f, 0f), Color.white, true);

        // â”€â”€ Wheels â”€â”€
        Vector3[] wheelPos = new Vector3[]
        {
            new Vector3(-halfW - 0.5f, -0.1f, -halfD + 0.2f),
            new Vector3(halfW + 0.5f, -0.1f, -halfD + 0.2f),
            new Vector3(-halfW - 0.5f, -0.1f, halfD - 0.2f),
            new Vector3(halfW + 0.5f, -0.1f, halfD - 0.2f)
        };
        foreach (var wp in wheelPos)
        {
            var w = MakeBlock("Wheel", modelRoot.transform, new Vector3(0.9f, 0.9f, 0.25f),
                wp, Color.black, true);
            w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            MakeBlock("WheelRim", w.transform, new Vector3(0.08f, 0.45f, 0.45f),
                new Vector3(0.08f, 0f, 0f), cartColor, true);
            cart.Wheels.Add(w);
        }

        // â”€â”€ Vendor NPC inside, near the window opening â”€â”€
        var vendorRoot = new GameObject("Vendor");
        vendorRoot.transform.SetParent(cart.Root.transform);
        vendorRoot.transform.localPosition = new Vector3(xL + 0.5f, -0.4f, winCenterZ);
        vendorRoot.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        MakeBlock("VendorBody", vendorRoot.transform, new Vector3(0.6f, 1.2f, 0.5f),
            new Vector3(0f, floorY + 1.0f, 0f), new Color(0.565f, 0.78f, 0.945f), true);
        MakeBlock("VendorHead", vendorRoot.transform, new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0f, floorY + 1.9f, 0f), Color.white, true);
        MakeBlock("VendorArmL", vendorRoot.transform, new Vector3(0.15f, 0.6f, 0.15f),
            new Vector3(-0.4f, floorY + 1.3f, 0f), new Color(0.565f, 0.78f, 0.945f), true);
        MakeBlock("VendorArmR", vendorRoot.transform, new Vector3(0.15f, 0.6f, 0.15f),
            new Vector3(0.4f, floorY + 1.3f, 0f), new Color(0.565f, 0.78f, 0.945f), true);

        // Face (toward the window / player, local +Z)
        Color vSkin = new Color(0.95f, 0.85f, 0.75f);
        Color vDark = new Color(0.15f, 0.12f, 0.1f);
        Color vRosy = new Color(0.95f, 0.6f, 0.5f);
        Color vGold = new Color(0.92f, 0.78f, 0.3f);
        MakeBlock("VendorEyeWhiteL", vendorRoot.transform, new Vector3(0.08f, 0.07f, 0.03f),
            new Vector3(-0.09f, floorY + 2.13f, 0.255f), Color.white, true);
        MakeBlock("VendorEyeWhiteR", vendorRoot.transform, new Vector3(0.08f, 0.07f, 0.03f),
            new Vector3(0.09f, floorY + 2.13f, 0.255f), Color.white, true);
        MakeBlock("VendorEyeIrisL", vendorRoot.transform, new Vector3(0.04f, 0.045f, 0.03f),
            new Vector3(-0.09f, floorY + 2.125f, 0.275f), vDark, true);
        MakeBlock("VendorEyeIrisR", vendorRoot.transform, new Vector3(0.04f, 0.045f, 0.03f),
            new Vector3(0.09f, floorY + 2.125f, 0.275f), vDark, true);
        MakeBlock("VendorEyebrowL", vendorRoot.transform, new Vector3(0.09f, 0.02f, 0.02f),
            new Vector3(-0.09f, floorY + 2.185f, 0.26f), vDark, true);
        MakeBlock("VendorEyebrowR", vendorRoot.transform, new Vector3(0.09f, 0.02f, 0.02f),
            new Vector3(0.09f, floorY + 2.185f, 0.26f), vDark, true);
        MakeBlock("VendorNose", vendorRoot.transform, new Vector3(0.06f, 0.06f, 0.04f),
            new Vector3(0f, floorY + 2.10f, 0.29f), vSkin, true);
        MakeBlock("VendorCheekL", vendorRoot.transform, new Vector3(0.04f, 0.03f, 0.02f),
            new Vector3(-0.15f, floorY + 2.08f, 0.26f), vRosy, true);
        MakeBlock("VendorCheekR", vendorRoot.transform, new Vector3(0.04f, 0.03f, 0.02f),
            new Vector3(0.15f, floorY + 2.08f, 0.26f), vRosy, true);
        MakeBlock("VendorSmile", vendorRoot.transform, new Vector3(0.11f, 0.02f, 0.02f),
            new Vector3(0f, floorY + 2.02f, 0.26f), vDark, true);

        // Apron, buttons and collar
        MakeBlock("VendorApron", vendorRoot.transform, new Vector3(0.5f, 0.5f, 0.03f),
            new Vector3(0f, floorY + 1.15f, 0.27f), new Color(0.88f, 0.86f, 0.82f), true);
        MakeBlock("VendorApronBtn1", vendorRoot.transform, new Vector3(0.03f, 0.03f, 0.025f),
            new Vector3(0f, floorY + 1.05f, 0.295f), vGold, true);
        MakeBlock("VendorApronBtn2", vendorRoot.transform, new Vector3(0.03f, 0.03f, 0.025f),
            new Vector3(0f, floorY + 1.25f, 0.295f), vGold, true);
        MakeBlock("VendorCollar", vendorRoot.transform, new Vector3(0.24f, 0.04f, 0.03f),
            new Vector3(0f, floorY + 1.65f, 0.27f), Color.white, true);

        // Straw hat (above head, visible over the roof)
        MakeBlock("VendorHatBrim", vendorRoot.transform, new Vector3(0.55f, 0.04f, 0.55f),
            new Vector3(0f, floorY + 2.38f, 0f), new Color(0.75f, 0.6f, 0.35f), true);
        MakeBlock("VendorHatBand", vendorRoot.transform, new Vector3(0.3f, 0.03f, 0.3f),
            new Vector3(0f, floorY + 2.42f, 0f), new Color(0.62f, 0.15f, 0.18f), true);
        MakeBlock("VendorHatCrown1", vendorRoot.transform, new Vector3(0.3f, 0.09f, 0.3f),
            new Vector3(0f, floorY + 2.45f, 0f), new Color(0.75f, 0.6f, 0.35f), true);
        MakeBlock("VendorHatCrown2", vendorRoot.transform, new Vector3(0.2f, 0.08f, 0.2f),
            new Vector3(0f, floorY + 2.52f, 0f), new Color(0.75f, 0.6f, 0.35f), true);

        // Neck
        MakeBlock("VendorNeck", vendorRoot.transform, new Vector3(0.12f, 0.1f, 0.12f),
            new Vector3(0f, floorY + 1.7f, 0f), vSkin, true);
        // Ears
        MakeBlock("VendorEarL", vendorRoot.transform, new Vector3(0.04f, 0.06f, 0.04f),
            new Vector3(-0.27f, floorY + 2.1f, 0f), vSkin, true);
        MakeBlock("VendorEarR", vendorRoot.transform, new Vector3(0.04f, 0.06f, 0.04f),
            new Vector3(0.27f, floorY + 2.1f, 0f), vSkin, true);
        // Hands on counter
        MakeBlock("VendorHandL", vendorRoot.transform, new Vector3(0.12f, 0.1f, 0.12f),
            new Vector3(-0.4f, floorY + 0.85f, 0.2f), vSkin, true);
        MakeBlock("VendorHandR", vendorRoot.transform, new Vector3(0.12f, 0.1f, 0.12f),
            new Vector3(0.4f, floorY + 0.85f, 0.2f), vSkin, true);
        // Belt
        Color vBelt = new Color(0.25f, 0.18f, 0.1f);
        MakeBlock("VendorBelt", vendorRoot.transform, new Vector3(0.55f, 0.05f, 0.52f),
            new Vector3(0f, floorY + 0.78f, 0f), vBelt, true);
        MakeBlock("VendorBeltBuckle", vendorRoot.transform, new Vector3(0.06f, 0.05f, 0.03f),
            new Vector3(0f, floorY + 0.78f, 0.27f), vGold, true);
        // Money pouch on belt
        MakeBlock("VendorPouch", vendorRoot.transform, new Vector3(0.08f, 0.08f, 0.05f),
            new Vector3(0.22f, floorY + 0.85f, 0.15f), new Color(0.42f, 0.28f, 0.15f), true);
        MakeBlock("VendorPouchStrap", vendorRoot.transform, new Vector3(0.02f, 0.1f, 0.02f),
            new Vector3(0.22f, floorY + 0.95f, 0.15f), new Color(0.42f, 0.28f, 0.15f), true);
        // Shirt pocket
        MakeBlock("VendorPocket", vendorRoot.transform, new Vector3(0.08f, 0.06f, 0.02f),
            new Vector3(-0.12f, floorY + 1.45f, 0.27f), new Color(0.46f, 0.68f, 0.845f), true);
        // Sleeve cuffs
        MakeBlock("VendorCuffL", vendorRoot.transform, new Vector3(0.16f, 0.05f, 0.16f),
            new Vector3(-0.4f, floorY + 1.6f, 0f), Color.white, true);
        MakeBlock("VendorCuffR", vendorRoot.transform, new Vector3(0.16f, 0.05f, 0.16f),
            new Vector3(0.4f, floorY + 1.6f, 0f), Color.white, true);

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

    public void SpawnVendorCartAt(Vector3 position, Quaternion rotation = default)
    {
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
        SoundManager.Instance?.Play("mexican_truck");
        cart.Root = new GameObject("VendorCart");
        cart.Root.transform.SetParent(_worldRoot.transform);
        cart.Root.transform.position = position + Vector3.up * 2f;
        cart.Root.transform.rotation = rotation != default ? rotation : Quaternion.identity;
        cart.ArrivalPos = position;
        cart.TargetGroundY = 0.5f;
        cart.Speed = 6f;
        cart.Rising = true;
        cart.Wheels = new List<GameObject>();

        Color cartColor = new Color(
            Random.Range(80f, 255f) / 255f,
            Random.Range(50f, 220f) / 255f,
            Random.Range(50f, 220f) / 255f
        );

        float halfW = 1.3f;
        float halfD = 1.8f;
        float wallH = 1.8f;
        float floorY = 0.2f;
        float roofY = 2.0f;

        var modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(cart.Root.transform);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.SetParent(modelRoot.transform, false);
        floor.transform.localScale = new Vector3(halfW * 2f, 0.1f, halfD * 2f);
        floor.transform.localPosition = new Vector3(0f, floorY, 0f);
        floor.GetComponent<Renderer>().material.color = cartColor;
        Object.Destroy(floor.GetComponent<Collider>());

        var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.transform.SetParent(modelRoot.transform, false);
        back.transform.localScale = new Vector3(halfW * 2f, wallH, 0.1f);
        back.transform.localPosition = new Vector3(0f, floorY + wallH / 2f, -halfD);
        back.GetComponent<Renderer>().material.color = cartColor;
        Object.Destroy(back.GetComponent<Collider>());

        var front = GameObject.CreatePrimitive(PrimitiveType.Cube);
        front.transform.SetParent(modelRoot.transform, false);
        front.transform.localScale = new Vector3(halfW * 2f, wallH, 0.1f);
        front.transform.localPosition = new Vector3(0f, floorY + wallH / 2f, halfD);
        front.GetComponent<Renderer>().material.color = cartColor;
        Object.Destroy(front.GetComponent<Collider>());

        var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.transform.SetParent(modelRoot.transform, false);
        left.transform.localScale = new Vector3(0.1f, wallH, halfD * 2f);
        left.transform.localPosition = new Vector3(-halfW, floorY + wallH / 2f, 0f);
        left.GetComponent<Renderer>().material.color = cartColor;
        Object.Destroy(left.GetComponent<Collider>());

        var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        right.transform.SetParent(modelRoot.transform, false);
        right.transform.localScale = new Vector3(0.1f, wallH, halfD * 2f);
        right.transform.localPosition = new Vector3(halfW, floorY + wallH / 2f, 0f);
        right.GetComponent<Renderer>().material.color = cartColor;
        Object.Destroy(right.GetComponent<Collider>());

        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.SetParent(modelRoot.transform, false);
        roof.transform.localScale = new Vector3(halfW * 2f + 0.2f, 0.1f, halfD * 2f + 0.2f);
        roof.transform.localPosition = new Vector3(0f, roofY, 0f);
        roof.GetComponent<Renderer>().material.color = cartColor;
        Object.Destroy(roof.GetComponent<Collider>());

        var vendorRoot = MapBuilder.BuildImmigrantNpc(cart.Root.transform, new Vector3(-halfW - 0.6f, floorY, 0f));

        // Shop sign on roof
        var signPole = GameObject.CreatePrimitive(PrimitiveType.Cube);
        signPole.transform.SetParent(modelRoot.transform, false);
        signPole.transform.localScale = new Vector3(0.03f, 0.6f, 0.03f);
        signPole.transform.localPosition = new Vector3(-1.5f, 2.3f, 0f);
        signPole.GetComponent<Renderer>().material.color = new Color(0.3f, 0.3f, 0.3f);
        Object.Destroy(signPole.GetComponent<Collider>());

        var signBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        signBoard.transform.SetParent(modelRoot.transform, false);
        signBoard.transform.localScale = new Vector3(0.5f, 0.15f, 0.3f);
        signBoard.transform.localPosition = new Vector3(-1.5f, 2.6f, 0f);
        signBoard.GetComponent<Renderer>().material.color = new Color(0.9f, 0.8f, 0.1f);
        Object.Destroy(signBoard.GetComponent<Collider>());

        for (int i = 0; i < 4; i++)
        {
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.transform.SetParent(cart.Root.transform, false);
            wheel.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);
            float wx = (i % 2 == 0 ? -1f : 1f) * halfW * 0.7f;
            float wz = (i < 2 ? 1f : -1f) * halfD * 0.6f;
            wheel.transform.localPosition = new Vector3(wx, 0.15f, wz);
            wheel.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.15f);
            Object.Destroy(wheel.GetComponent<Collider>());
            cart.Wheels.Add(wheel);
        }

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
        renderer.material = PickupVisualHelper.CreateTransparentMaterialFromBase(CreateSafeLitMaterial(), Color.white);
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
        bool floorOk = !IsWallOrStair(definition.Name) || HasFloorAt(snapped);

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
}

