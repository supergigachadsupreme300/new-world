using UnityEngine;

public partial class ToolManager
{
    public void TryPickupNearby()
    {
        var cam = GetActiveCamera();
        if (cam == null)
            return;

        if (_carriedObject != null)
            return;

        var origin = cam.transform.position + cam.transform.forward * 0.3f;
        var ray = new Ray(origin, cam.transform.forward);
        ShowRayLine(ray.origin, ray.origin + ray.direction * PickupRayDistance);
        if (!Physics.Raycast(ray, out var hit, PickupRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return;

        Debug.Log($"TryPickupNearby: ray hit {hit.collider.gameObject.name}");

        if (hit.collider.transform.name == "BuffaloEntity")
        {
            var dlg = Object.FindAnyObjectByType<BuffaloDialog>();
            if (dlg == null)
            {
                var go = new GameObject("BuffaloDialog");
                dlg = go.AddComponent<BuffaloDialog>();
                dlg.Initialize();
            }
            dlg.Show();
            return;
        }

        if (hit.collider.transform.name == "VendorNPC")
        {
            var shop = Object.FindAnyObjectByType<VendorShopManager>();
            if (shop == null)
            {
                var go = new GameObject("VendorShopManager");
                shop = go.AddComponent<VendorShopManager>();
                shop.Initialize();
            }
            shop.Open();
            return;
        }

        if (hit.collider.transform.name == "RestaurantNPC")
        {
            if (ChefNPC.Instance != null && !ChefNPC.Instance.IsDialogActive)
                ChefNPC.Instance.Interact();
            return;
        }

        if (hit.collider.transform.name == "ToolShopNPC")
        {
            var shop = Object.FindAnyObjectByType<VendorShopManager>();
            if (shop == null)
            {
                var go = new GameObject("VendorShopManager");
                shop = go.AddComponent<VendorShopManager>();
                shop.Initialize();
            }
            shop.OpenTools();
            return;
        }

        if (hit.collider.transform.name == "ConvenienceNPC")
        {
            var shop = Object.FindAnyObjectByType<VendorShopManager>();
            if (shop == null)
            {
                var go = new GameObject("VendorShopManager");
                shop = go.AddComponent<VendorShopManager>();
                shop.Initialize();
            }
            shop.OpenConvenience();
            return;
        }

        if (hit.collider.transform.name == "GroceryNPC")
        {
            var shop = Object.FindAnyObjectByType<VendorShopManager>();
            if (shop == null)
            {
                var go = new GameObject("VendorShopManager");
                shop = go.AddComponent<VendorShopManager>();
                shop.Initialize();
            }
            shop.OpenGrocery();
            return;
        }

        var goblin = hit.collider.GetComponentInParent<GoblinPet>();
        if (goblin != null)
        {
            string selected = GetSelectedItemType();
            string crop = selected switch
            {
                "wheat_seed" => "wheat",
                "corn_seed" => "corn",
                "potato_seed" => "potato",
                "carrot_seed" => "carrot",
                "tomato_seed" => "tomato",
                "strawberry_seed" => "strawberry",
                "pumpkin_seed" => "pumpkin",
                "onion_seed" => "onion",
                "sugarcane_seed" => "sugarcane",
                "rice_seed" => "rice",
                _ => null
            };

            if (crop == null)
            {
                _uiManager.ShowMessage(Localization.T("Chọn hạt giống để đưa cho goblin."), 1.5f);
                return;
            }

            if (!goblin.CanAcceptSeed)
            {
                _uiManager.ShowMessage(goblin.IsDead ? Localization.T("Goblin đang bất tỉnh!") : Localization.T("Goblin đang bận!"), 1.5f);
                return;
            }

            if (!RemoveItem(_selectedSlot, 1))
            {
                _uiManager.ShowMessage(Localization.T("Không thể lấy hạt giống."), 1.5f);
                return;
            }

            goblin.GiveSeed(crop);
            SoundManager.Instance?.Play("pop");
            _uiManager.ShowMessage(Localization.T("Đã đưa hạt giống cho goblin."), 1.5f);
            return;
        }

        if (TryPickupTool(hit.collider))
            return;

        // Check for felled tree / branch / debris first (carry them, don't delete)
        var root = hit.collider.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;

        if (root.name == "CageWithAnimal" || root.name == "ThrownCage")
        {
            if (root.GetComponent<Rigidbody>() == null) return;
            _carriedObject = root;
            root.GetComponent<Rigidbody>().isKinematic = true;
            var cols = root.GetComponentsInChildren<Collider>();
            foreach (var c in cols)
                c.enabled = false;
            root.transform.SetParent(cam.transform);
            root.transform.localPosition = new Vector3(0.7f, -0.4f, 1.8f);
            root.transform.localRotation = Quaternion.identity;
            if (root.name == "CageWithAnimal")
            {
                var ci = root.GetComponent<CageWithAnimalInfo>();
                string n = ci != null ? Localization.AnimalName(ci.AnimalType.ToString()) : Localization.AnimalName("animal");
                _uiManager.ShowMessage(Localization.F("Đã nhặt lồng với {0}.", n), 1f);
            }
            else
                _uiManager.ShowMessage(Localization.T("Đã nhặt lên."), 1f);
            return;
        }

        if (GetSelectedItemType() != null)
            return;

        if (root.name == "TreeFelled" || root.name == "BranchTop" || root.name == "RockDebris")
        {
            if (root.GetComponent<Rigidbody>() == null) return;
            _carriedObject = root;
            root.GetComponent<Rigidbody>().isKinematic = true;
            var cols = root.GetComponentsInChildren<Collider>();
            foreach (var c in cols)
                c.enabled = false;
            root.transform.SetParent(cam.transform);
            root.transform.localPosition = new Vector3(0.7f, -0.4f, 1.8f);
            root.transform.localRotation = Quaternion.identity;
            _uiManager.ShowMessage(Localization.T("Đã nhặt lên."), 1f);
            return;
        }
    }

    private bool IsTree(Collider collider)
    {
        if (collider == null)
            return false;
        var t = collider.transform;
        while (t != null)
        {
            if (t.name.StartsWith("Tree"))
                return true;
            t = t.parent;
        }
        return false;
    }

    private GameObject FindTreeRoot(Collider collider)
    {
        if (collider == null) return null;
        var t = collider.transform;
        while (t != null)
        {
            if (t.name.StartsWith("Tree"))
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    private bool TryPickupTool(Collider collider)
    {
        if (collider == null)
        {
            Debug.Log("TryPickupTool: collider is null");
            return false;
        }

        var pickupName = collider.gameObject.name;
        var pickupRoot = collider.gameObject;
        
        // Check if this is a visual child, if so get the parent (root pickup)
        if (pickupRoot.transform.parent != null && pickupRoot.transform.parent.name.StartsWith("Pickup_"))
        {
            pickupRoot = pickupRoot.transform.parent.gameObject;
            pickupName = pickupRoot.name;
        }
        
        Debug.Log($"TryPickupTool: hit {pickupName}");
        var flapping = collider.GetComponentInParent<FlappingFish>();
        if (flapping != null && !flapping.IsPickable)
        {
            _uiManager.ShowMessage(Localization.T("Cá đang quẫy! Dùng gậy gõ cho nó xỉu."), 1.5f);
            return true;
        }
        if (!pickupName.StartsWith("Pickup_"))
            return false;

        var itemType = pickupName.Substring("Pickup_".Length);

        if (itemType.StartsWith("gold_") && int.TryParse(itemType.Substring("gold_".Length), out var coinAmount))
        {
            var coin = pickupRoot.GetComponent<CoinPickupBehavior>();
            if (coin != null && coin.Collected)
                return true;

            var player = GameManager.Instance?.Player;
            if (player != null)
                player.Money += coinAmount;
            SoundManager.Instance?.Play("pop");
            _uiManager.ShowMessage($"+{coinAmount}g", 1.5f);
            GameManager.Instance?.UIManager?.UpdatePlayerHud(
                player != null ? player.HP : 0,
                player != null ? player.MaxHP : 0,
                player != null ? player.Stamina : 0,
                player != null ? player.MaxStamina : 0,
                player != null ? player.Money : 0);
            Destroy(pickupRoot);
            return true;
        }

        AddItem(itemType, 1);
        SoundManager.Instance?.Play("pop");
        _uiManager.ShowMessage(Localization.F("Đã nhặt {0}.", Localization.ItemName(itemType)), 1.5f);
        Destroy(pickupRoot);
        return true;
    }

    private bool IsRock(Collider collider)
    {
        if (collider == null)
            return false;
        return collider.gameObject.name.StartsWith("Rock");
    }

    public void DropSelectedItem()
    {
        if (_carriedObject != null)
        {
            DropCarriedObject(GameManager.Instance?.Player);
            return;
        }

        var itemType = GetSelectedItemType();
        if (itemType == null)
            return;

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var cam = Camera.main;
        var throwOrigin = cam != null
            ? cam.transform.position + cam.transform.forward * 0.5f
            : player.transform.position + Vector3.up * 1.5f + player.transform.forward * 0.5f;
        var throwDir = cam != null ? cam.transform.forward : player.transform.forward;
        var throwVelocity = throwDir * 8f + Vector3.up * 3.5f;

        if (RemoveItem(_selectedSlot, 1))
        {
            if (_worldBuilder != null)
            {
                if (itemType == "cage_big" || itemType == "cage_small")
                    _worldBuilder.ThrowCage(itemType, throwOrigin, throwVelocity);
                else
                    _worldBuilder.ThrowPickup(itemType, throwOrigin, throwVelocity);
            }

            _uiManager.ShowMessage(Localization.F("Đã ném {0}.", Localization.ItemName(itemType)), 1.5f);
            UpdateInventoryUI();
            ShowActiveToolModel();
        }
    }

    private void TryPickupFelledTree(Camera cam, PlayerController player)
    {
        var origin = cam.transform.position + cam.transform.forward * 0.3f;
        var useRay = new Ray(origin, cam.transform.forward);
        ShowRayLine(useRay.origin, useRay.origin + useRay.direction * PickupRayDistance);
        if (!Physics.Raycast(useRay, out var hit, PickupRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return;

        if (_carriedObject != null)
            return;

        var root = hit.collider.gameObject;
        while (root.transform.parent != null && root.transform.parent.name != "WorldRoot")
            root = root.transform.parent.gameObject;

        if (root.name != "TreeFelled" && root.name != "BranchTop" && root.name != "RockDebris" && root.name != "CageWithAnimal" && root.name != "ThrownCage")
            return;

        if (root.GetComponent<Rigidbody>() == null) return;

        _carriedObject = root;
        root.GetComponent<Rigidbody>().isKinematic = true;
        var cols = root.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            c.enabled = false;
        root.transform.SetParent(cam.transform);
        root.transform.localPosition = new Vector3(0.7f, -0.4f, 1.8f);
        root.transform.localRotation = Quaternion.identity;
        _uiManager.ShowMessage(Localization.T("Đã nhặt lên."), 1f);
    }

    private void DropCarriedObject(PlayerController player)
    {
        if (_carriedObject == null) return;

        if (_carriedObject.name == "CageWithAnimal")
        {
            var info = _carriedObject.GetComponent<CageWithAnimalInfo>();
            if (info != null && _worldBuilder != null)
            {
                var cam = GetActiveCamera();
                var throwOrigin = cam != null
                    ? cam.transform.position + cam.transform.forward * 0.5f
                    : _carriedObject.transform.position;
                var throwDir = cam != null ? cam.transform.forward : Vector3.forward;
                var throwVelocity = throwDir * 8f + Vector3.up * 3.5f;

                _worldBuilder.ThrowCage(
                    info.AnimalType == Livestock.AnimalType.Cow ||
                    info.AnimalType == Livestock.AnimalType.Pig ||
                    info.AnimalType == Livestock.AnimalType.Sheep ||
                    info.AnimalType == Livestock.AnimalType.Goat
                        ? "cage_big" : "cage_small",
                    throwOrigin,
                    throwVelocity,
                    info.AnimalType);

                _uiManager.ShowMessage(Localization.T("Đang ném lồng..."), 1f);
            }
            Destroy(_carriedObject);
            _carriedObject = null;
            return;
        }

        if (_carriedObject.name == "ThrownCage")
        {
            var info = _carriedObject.GetComponent<ThrownCageInfo>();
            string cageType = info != null ? info.CageType : "cage_big";
            var cam = GetActiveCamera();
            var throwOrigin = cam != null
                ? cam.transform.position + cam.transform.forward * 0.5f
                : _carriedObject.transform.position;
            var throwDir = cam != null ? cam.transform.forward : Vector3.forward;
            var throwVelocity = throwDir * 8f + Vector3.up * 3.5f;
            Destroy(_carriedObject);
            _carriedObject = null;
            if (_worldBuilder != null)
                _worldBuilder.ThrowCage(cageType, throwOrigin, throwVelocity);
            _uiManager.ShowMessage(Localization.T("Đã ném lồng trống."), 1f);
            return;
        }

        _carriedObject.transform.SetParent(null);
        var rb = _carriedObject.GetComponent<Rigidbody>();
        var cols = _carriedObject.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            c.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            var cam = GetActiveCamera();
            var throwDir = (cam != null ? cam.transform.forward : Vector3.forward) + Vector3.up * 0.3f;
            rb.linearVelocity = throwDir.normalized * 5f;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
            _carriedObject.transform.position = cam != null ? cam.transform.position + cam.transform.forward * 1.2f : player.transform.position + Vector3.up;
        }
        else if (player != null)
        {
            _carriedObject.transform.position = player.transform.position + player.transform.forward * 1.5f + Vector3.up * 0.5f;
        }
        _carriedObject = null;
        _uiManager.ShowMessage(Localization.T("Đã bỏ xuống."), 1f);
    }

    private (string material, float amount) GetCarriedResourceInfo(GameObject obj)
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

    private void TryAutoDeposit()
    {
        if (_carriedObject == null || _worldBuilder == null || _uiManager == null) return;

        int count = Physics.OverlapSphereNonAlloc(_carriedObject.transform.position, 0.6f, _overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var c = _overlapBuffer[i];
            if (!_worldBuilder.IsBlueprint(c.gameObject)) continue;
            var bp = _worldBuilder.FindBlueprint(c.gameObject);
            if (bp == null) continue;

            var (material, amount) = GetCarriedResourceInfo(_carriedObject);
            if (material == null) return;

            if (amount < 0.05f)
            {
                Destroy(_carriedObject);
                _carriedObject = null;
                _uiManager.ShowMessage(Localization.T("Quá nhỏ để dùng."), 1f);
                return;
            }

            if (_worldBuilder.DepositMaterial(bp, material, amount))
                            {
                                _uiManager.ShowMessage(Localization.T("Xây dựng hoàn thành!"), 1.5f);
                                KarmaManager.Instance?.AddMaxKarma(1f);
                                SoundManager.Instance?.Play("hammer");
                            }
                            else
                            {
                                _uiManager.ShowMessage(Localization.F("Đã cung cấp {0} x{1}.", Localization.ItemName(material), amount.ToString("F2")), 1.5f);
                            }
            Destroy(_carriedObject);
            _carriedObject = null;
            _uiManager?.SetInfoText(null);
            return;
        }
    }
}
