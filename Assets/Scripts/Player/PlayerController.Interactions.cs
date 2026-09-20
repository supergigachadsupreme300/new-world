/// <summary>Partial player controller — interaction / looting cluster: NPC/dialog interactions,
/// E/G key handling, chests, vendors, horse mounting, fast travel, crafting stations, and item
/// pickup. Mechanically split from PlayerController.cs; no behavior or signature changes.</summary>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    public bool IsSitting => _sitController != null && _sitController.IsSitting;

    private bool TrySitNearby()
    {
        if (_sitController == null)
            return false;
        var seat = SittableSeat.FindNearest(transform.position, 2.6f);
        if (seat == null)
            return false;
        _sitController.BeginSit(seat);
        return true;
    }

    private void HandleInteractionKeys()
    {
        bool wifeDialog = WifeNPC.Instance != null && WifeNPC.Instance.IsDialogActive;
        bool buffaloDialog = BuffaloDialog.Instance != null && BuffaloDialog.Instance.IsDialogActive;
        bool richManDialog = RichManNPC.Instance != null && RichManNPC.Instance.IsDialogActive;
        bool policeDialog = PoliceOfficerNPC.Instance != null && PoliceOfficerNPC.Instance.IsDialogActive;
        bool monkDialog = PagodaMonkNPC.Instance != null && PagodaMonkNPC.Instance.IsDialogActive;
        bool chefDialog = ChefNPC.Instance != null && ChefNPC.Instance.IsDialogActive;
        bool cafeBaristaDialog = CafeBarista.Instance != null && CafeBarista.Instance.IsDialogActive;
        bool librarianDialog = LibrarianNPC.Instance != null && LibrarianNPC.Instance.IsDialogActive;
        bool fishingShopDialog = FishingShopNPC.Instance != null && FishingShopNPC.Instance.IsDialogActive;
        bool goblinMenuOpen = GoblinCommandMenu.Instance != null && GoblinCommandMenu.Instance.IsOpen;
        bool craftingOpen = CraftingManager.Instance != null && CraftingManager.Instance.IsOpen;
        bool dialogBlocked = wifeDialog || buffaloDialog || richManDialog || policeDialog || monkDialog || chefDialog || cafeBaristaDialog || librarianDialog || fishingShopDialog || goblinMenuOpen || craftingOpen;

        bool ePressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
                        (!wifeDialog && MobileInputController.Consume("interact"));
        if (ePressed && buffaloDialog)
            BuffaloDialog.Instance.Advance();
        if (ePressed && richManDialog)
            RichManNPC.Instance.Advance();
        if (ePressed && policeDialog)
            PoliceOfficerNPC.Instance.Advance();
        if (ePressed && monkDialog)
            PagodaMonkNPC.Instance.Advance();
        if (ePressed && chefDialog)
            ChefNPC.Instance.Advance();
        if (ePressed && librarianDialog)
            LibrarianNPC.Instance.Advance();
        if (ePressed && fishingShopDialog)
            FishingShopNPC.Instance.Advance();
        if (ePressed && cafeBaristaDialog)
            CafeBarista.Instance.Advance();
        if (richManDialog && RichManNPC.Instance != null && RichManNPC.Instance.IsEndingChoiceShown)
        {
            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
                RichManNPC.Instance.ChooseLeave();
            else if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
                RichManNPC.Instance.ChooseBribe();
        }

        if (!dialogBlocked)
        {
            if (ePressed)
            {
                if (IsRiding)
                {
                    HorseMount.Instance?.Dismount();
                    return;
                }
                var wb = WorldBuilder.Instance;
                if (RichManNPC.Instance != null && RichManNPC.Instance.TryEavesdropDeal(transform.position))
                    return;
                var cam = Camera.main;
                if (cam != null && wb != null)
                {
                    var ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    {
                        var stand = hit.collider.GetComponentInParent<WeaponRackStand>();
                        if (stand != null)
                        {
                            PickupWeaponStand(stand);
                            return;
                        }
                        if (hit.collider.transform.name == "WifeNpc")
                        {
                            if (WifeNPC.Instance != null && !WifeNPC.Instance.IsDialogActive)
                                WifeNPC.Instance.Interact();
                            QuestManager.Instance?.AddProgress("greet", 1);
                            return;
                        }
                        if (hit.collider.transform.name == "Bed")
                        {
                            if (SleepManager.Instance != null)
                                SleepManager.Instance.Open();
                            return;
                        }
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
                        if (hit.collider.transform.name == "RichManNpc")
                        {
                            if (RichManNPC.Instance != null && !RichManNPC.Instance.IsDialogActive)
                                RichManNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "PoliceOfficer")
                        {
                            if (PoliceOfficerNPC.Instance != null && !PoliceOfficerNPC.Instance.IsDialogActive)
                                PoliceOfficerNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "RestaurantNPC")
                        {
                            if (ChefNPC.Instance != null && !ChefNPC.Instance.IsDialogActive)
                                ChefNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "PagodaMonkNpc")
                        {
                            if (PagodaMonkNPC.Instance != null && !PagodaMonkNPC.Instance.IsDialogActive)
                                PagodaMonkNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "PriestNpc")
                        {
                            if (PriestNPC.Instance != null && !PriestNPC.Instance.IsDialogActive)
                                PriestNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "TaoistPriestNpc")
                        {
                            if (TaoistPriestNPC.Instance != null && !TaoistPriestNPC.Instance.IsDialogActive)
                                TaoistPriestNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "ToolShopNPC")
                        {
                            OpenVendorShop("tools");
                            return;
                        }
                        if (hit.collider.transform.name == "ConvenienceNPC")
                        {
                            OpenVendorShop("convenience");
                            return;
                        }
                        if (hit.collider.transform.name == "GroceryNPC")
                        {
                            OpenVendorShop("grocery");
                            return;
                        }
                        if (hit.collider.transform.name == "CafeNPC")
                        {
                            if (CafeBarista.Instance != null && !CafeBarista.Instance.IsDialogActive)
                                CafeBarista.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "FishingShopNPC")
                        {
                            if (FishingShopNPC.Instance != null && !FishingShopNPC.Instance.IsDialogActive)
                                FishingShopNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name == "LibrarianNPC")
                        {
                            if (LibrarianNPC.Instance != null && !LibrarianNPC.Instance.IsDialogActive)
                                LibrarianNPC.Instance.Interact();
                            return;
                        }
                        if (hit.collider.transform.name.StartsWith("GoblinPet"))
                        {
                            var goblin = hit.collider.GetComponentInParent<GoblinPet>();
                            if (goblin != null)
                                GoblinCommandMenu.Ensure().Open(goblin);
                            return;
                        }
                        if (hit.collider.transform.name.StartsWith("GoblinChest"))
                        {
                            GoblinChestMenu.Ensure().Open();
                            return;
                        }
                        var chestHit = hit.collider.transform;
                        while (chestHit != null && chestHit.name != "chest")
                            chestHit = chestHit.parent;
                        if (chestHit != null)
                        {
                            PlayerChestMenu.Ensure().OpenAt(chestHit.position);
                            return;
                        }
                        var rideHorse = hit.collider.GetComponentInParent<HorseMount>();
                        if (rideHorse != null)
                        {
                            rideHorse.ToggleMount();
                            return;
                        }
                        var roadSign = hit.collider.GetComponentInParent<FastTravelSign>();
                        if (roadSign != null)
                        {
                            FastTravelMenu.Ensure().Open();
                            return;
                        }
                        if (CraftingManager.ResolveStationCategory(hit.collider) != null)
                        {
                            CraftingManager.Ensure().InteractStation(hit.collider);
                            return;
                        }
                        if (wb.TryToggleDoor(hit)) return;
                    }
                }
                if (!(ToolManager.Instance?.TryPickupNearby() ?? false))
                    TrySitNearby();
            }
        }

        bool gPressed = (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) ||
                        MobileInputController.Consume("invite");
        if (!dialogBlocked && gPressed)
        {
            var npcGO = GameObject.Find("WifeNpc");
            if (npcGO != null && Vector3.Distance(transform.position, npcGO.transform.position) < 6f)
            {
                WifeNPC.Instance?.InviteToHouse();
            }
        }

        bool leftClick = !FishingController.IsFishingActive &&
                         ((!GameInput.IsMobile && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                          MobileInputController.Consume("use"));
        if (FightingMode)
        {
            // Re-rig ONLY after a model reload (gender/race change) destroyed the hand rigs, and
            // only for the weapons that were equipped before the rebuild. An intentional drag-out /
            // unequip never gets resurrected.
            if (_pendingAutoRig.Count > 0)
            {
                var combat = GetComponent<CombatController>();
                if (combat != null && combat.RightHand == null && combat.LeftHand == null)
                {
                    foreach (var pending in _pendingAutoRig)
                    {
                        var weapon = WeaponCatalog.Find(pending.id);
                        if (weapon != null)
                            WeaponRigBuilder.EquipInto(gameObject, weapon, pending.isLeft);
                    }
                    ReApplyWeaponPose(instant: true);
                }
                _pendingAutoRig.Clear();
            }
            // Nothing equipped (fought barehanded before the reload) — put the fists back on.
            var combatNow = GetComponent<CombatController>();
            if (combatNow != null && combatNow.RightHand == null && combatNow.LeftHand == null)
                WeaponRigBuilder.EnsureFists(gameObject);
        }
        // Per-hand dual-wield (both hands hold real weapons, NOT both magic): LMB and RMB drive each
        // hand directly instead of the magic aim / RMB-block scheme — so dual loadouts never enter
        // the single-weapon aim flow below. Runs regardless of dialog so a held ranged release fires.
        var dualCombat = GetComponent<CombatController>();
        bool dualMode = FightingMode && dualCombat != null && !GameInput.IsMobile &&
                        dualCombat.HasLoadedDual && !dualCombat.BothHandsMagic;
        if (dualMode)
            HandleDualModeCombat(dualCombat, !dialogBlocked && !MagicWheelUI.IsOpen && !MagicTestMatrix.IsOpen);
        // Aim/charge: while _aiming (armed magic or ranged), RMB builds the charge level and RMB
        // release freezes it; releasing LMB fires at the current level. Runs even while dialog-ish
        // UI is up so the release isn't mired.
        if (_aiming && !dualMode)
        {
            var combat = GetComponent<CombatController>();
            if (ShouldCancelCharge())
            {
                _aiming = false;
                _chargeRmbHeld = false;
                _chargeAccum = 0f;
                _chargeDrained = 0f;
                HideAoePreview();
                HideCastingCircle();
                HidePathPreview();
                combat?.EndCharge(false);
            }
            else
            {
                bool rmbDown = !GameInput.IsMobile && Mouse.current != null && Mouse.current.rightButton.isPressed;
                _chargeRmbHeld = rmbDown;
                var armedSpell = ArmedSpell();
                bool armedMagic = armedSpell != null;
                bool chargeGrow = false;
                if (rmbDown)
                {
                    if (armedSpell != null)
                    {
                        // Magic overcharges past level 1 until the pool runs dry; each aim frame
                        // drains focus in real time (gated by remaining FP) so holding is a gamble.
                        var caster = GetComponent<SpellCaster>();
                        if (caster != null)
                        {
                            float level = SpellChargeLevel(_chargeAccum);
                            if (caster.CurrentFp > 0f)
                            {
                                float drain = armedSpell.FpCost * caster.ChargeFpCostBonus *
                                              level * caster.FpChargeDrainRate * Time.deltaTime;
                                drain = Mathf.Min(drain, caster.CurrentFp);
                                if (caster.TrySpendFocus(drain))
                                    _chargeDrained += drain;
                            }
                            chargeGrow = caster.CurrentFp > 0f;
                        }
                    }
                    else
                    {
                        // Ranged keeps its capped draw (no focus involved).
                        _chargeAccum = Mathf.Min(_chargeAccum + Time.deltaTime, MagicChargeMaxTime);
                    }
                }
                if (chargeGrow)
                    _chargeAccum += Time.deltaTime;
                float previewLevel = armedMagic ? SpellChargeLevel(_chargeAccum) : MagicChargeLevel(_chargeAccum);
                float hudLevel = MagicChargeLevel(_chargeAccum);
                combat?.SetChargeLevel(Mathf.Clamp01(hudLevel));

                UpdateAoePreview(previewLevel);
                UpdateCastingCircle(previewLevel);
                UpdatePathPreview(previewLevel, armedSpell);

                bool lmbUp = !GameInput.IsMobile && Mouse.current != null &&
                             (Mouse.current.leftButton.wasReleasedThisFrame || !Mouse.current.leftButton.isPressed);
                if (lmbUp)
                {
                    float charge = armedMagic ? SpellChargeLevel(_chargeAccum) : MagicChargeLevel(_chargeAccum);
                    float prepaid = _chargeDrained;
                    _aiming = false;
                    _chargeRmbHeld = false;
                    _chargeAccum = 0f;
                    _chargeDrained = 0f;
                    combat?.EndCharge(true);
                    HidePathPreview();
                    if (MagicWheelUI.HasArmedMagic())
                    {
                        bool previewShown = _aoePreview != null && _aoePreview.IsActive;
                        if (MagicWheelUI.ReleaseArmedCast(charge, prepaid))
                        {
                            BurstCastingCircle(charge);
                            HideCastingCircle();
                            // Keep the marker up until the spell actually lands, then it hides itself.
                            if (previewShown)
                            {
                                var caster = GetComponent<SpellCaster>();
                                if (caster != null) _aoePreview.Lock(caster);
                                else HideAoePreview();
                            }
                        }
                        else
                        {
                            // Rejected (e.g. tap with an empty pool): tear the charge down silently —
                            // no ring-without-bolt phantom.
                            HideCastingCircle();
                            if (previewShown) HideAoePreview();
                        }
                    }
                    else if (IsRangedEquipped(combat))
                    {
                        HideAoePreview();
                        combat.FireRanged(charge);
                    }
                    else
                        combat?.EndCharge(false);
                }
            }
        }
        if (!dialogBlocked && leftClick && !MagicWheelUI.IsOpen && !MagicTestMatrix.IsOpen && !dualMode && !BeamChanneling())
        {
            if (FightingMode)
            {
                var combatPress = GetComponent<CombatController>();
                // Auto-arm a spell on demand so magic aim/charge/fire works without the Alt wheel first.
                bool aimable = !GameInput.IsMobile && !WeaponTransitionBusy() &&
                    (MagicWheelUI.EnsureArmedMagic() || IsRangedEquipped(combatPress));
                if (aimable)
                {
                    // Hold LMB to aim (no charge yet); the release fires, driven above.
                    _aiming = true;
                    _chargeDrained = 0f;
                    combatPress?.PlayCharge();
                }
                else if (!WeaponTransitionBusy() && !MagicWheelUI.ConsumeArmedCast())
                {
                    combatPress?.LightAttack();
                }
            }
            else
                ToolManager.Instance?.UseSelectedItem();
        }
        if (!dialogBlocked && !GameInput.IsMobile && !MagicWheelUI.IsOpen && !MagicTestMatrix.IsOpen && Mouse.current != null && !dualMode)
        {
            if (FightingMode)
            {
                var combat = GetComponent<CombatController>();
                if (combat != null)
                {
                    if (IsMeleeEquipped(combat) || IsShieldEquipped(combat))
                    {
                        // RMB hold = block for melee weapons and shields (incl. fists, incl. a
                        // shield held in the off-hand). Melee no longer has a heavy attack — the
                        // finisher swing is dropped for melee, and shields never charge.
                        if (!WeaponTransitionBusy())
                            combat.SetBlocking(Mouse.current.rightButton.isPressed);
                    }
                    // Ranged/magic: RMB is the charge/draw — driven by the aim session above.
                }
                if (Mouse.current.rightButton.wasPressedThisFrame)
                    return;
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                var cam = Camera.main;
            if (cam != null)
            {
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.Raycast(ray, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    string hitName = hit.collider.transform.name;

                    if (hitName == "BuffaloEntity")
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

                    if (hitName == "VendorNPC")
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

                    if (hitName == "ToolShopNPC")
                    {
                        OpenVendorShop("tools");
                        return;
                    }

                    if (hitName == "ConvenienceNPC")
                    {
                        OpenVendorShop("convenience");
                        return;
                    }

                    if (hitName == "GroceryNPC")
                    {
                        OpenVendorShop("grocery");
                        return;
                    }
                }
            }
        }
        }
        if (!dialogBlocked && ((Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) ||
            MobileInputController.Consume("drop")))
            ToolManager.Instance?.DropSelectedItem();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            WorldBuilder.Instance?.RotateBuildingPreview(90);
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            GameManager.Instance?.UIManager?.ToggleSkillPanel();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            GameManager.Instance?.UIManager?.ToggleFriendPanel();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            ToolManager.Instance?.SortInventory();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            HorseMount.Instance?.Dismount();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            var info = Object.FindAnyObjectByType<CharacterInfoUI>();
            if (info != null)
            {
                if (info.IsShown)
                    info.Close();
                else
                    info.Show();
            }
        }
        if (!dialogBlocked && GameManager.Instance?.UIManager != null)
            GameManager.Instance.UIManager.HandleFriendPanelKeys();
        bool friendOpen = GameManager.Instance?.UIManager != null && GameManager.Instance.UIManager.FriendPanelVisible;
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(0);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(1);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(2);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(3);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit5Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(4);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit6Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(5);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit7Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(6);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit8Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(7);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit9Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(8);
        if (!dialogBlocked && !friendOpen && !FightingMode && Keyboard.current != null && Keyboard.current.digit0Key.wasPressedThisFrame)
            ToolManager.Instance?.SelectSlot(9);
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            ToggleCombatMode();
        if (!dialogBlocked && Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            // Toggle two-hand grip on a single held weapon (no-op while dual-wielding).
            var combat = GetComponent<CombatController>();
            if (combat != null)
                combat.SetTwoHand(!combat.TwoHandIntent);
        }
    }

    private void PickupWeaponStand(WeaponRackStand stand)
    {
        if (stand == null || stand.Collected) return;
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var inv = player.GetComponent<WeaponInventory>();
        if (inv == null)
            inv = player.gameObject.AddComponent<WeaponInventory>();

        var weapon = WeaponCatalog.Find(stand.WeaponId);
        string name = weapon != null && !string.IsNullOrEmpty(weapon.displayName) ? weapon.displayName : stand.WeaponId;

        if (inv.Own(stand.WeaponId))
            ShowPrompt(Localization.F("Picked up {0}.", name));
        else
            ShowPrompt(Localization.F("{0} is already in your inventory.", name));

        // Take it as a normal item: hotbar (0-9) first, then the backpack storage grid.
        var tm = ToolManager.Instance;
        if (tm == null)
        {
            var go = new GameObject("ToolManager");
            tm = go.AddComponent<ToolManager>();
        }
        if (!tm.AddItem(stand.WeaponId, 1))
            ShowPrompt(Localization.T("Túi đồ đầy."));

        stand.Collect();
    }

    private static void ShowPrompt(string message)
    {
        var prompt = Object.FindAnyObjectByType<ContextPromptUI>();
        if (prompt != null)
            prompt.ShowPrompt(message, 2.5f);
    }

    private void OpenVendorShop(string mode)
    {
        var shop = Object.FindAnyObjectByType<VendorShopManager>();
        if (shop == null)
        {
            var go = new GameObject("VendorShopManager");
            shop = go.AddComponent<VendorShopManager>();
            shop.Initialize();
        }
        switch (mode)
        {
            case "tools": shop.OpenTools(); break;
            case "convenience": shop.OpenConvenience(); break;
            case "grocery": shop.OpenGrocery(); break;
            default: shop.Open(); break;
        }
    }
}
