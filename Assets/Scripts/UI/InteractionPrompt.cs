using UnityEngine;
using TMPro;

public class InteractionPrompt : MonoBehaviour
{
    private TMP_Text _eKeyText;
    private TMP_Text _lmbText;
    private Camera _cam;
    private readonly float _rayRange = 4f;
    private int _raycastFrameCounter;
    private string _lastHitName;
    private GameObject _lastHitGo;

    private string _currentEKeyLocKey;
    private string _currentLmbLocKey;
    private string _currentEKeyText;
    private string _currentLmbKeyText;

    private static readonly (string colliderName, string locKey)[] _interactables = {
        ("WifeNpc",            "Nói chuyện"),
        ("RichManNpc",         "Nói chuyện"),
        ("PoliceOfficer",      "Nói chuyện"),
        ("RestaurantNPC",      "Nói chuyện"),
        ("ImmigrantNpc",       "Nói chuyện"),
        ("PagodaMonkNpc",      "Cầu nguyện"),
        ("LibrarianNPC",       "Đọc sách"),
        ("BuffaloEntity",      "Tương tác"),
        ("Bed",                "Ngủ"),
        ("VendorNPC",          "Mua sắm"),
        ("ToolShopNPC",        "Mua sắm"),
        ("ConvenienceNPC",     "Mua sắm"),
        ("GroceryNPC",         "Mua sắm"),
        ("CafeNPC",            "Mua sắm"),
        ("FishingShopNPC",     "Mua sắm"),
        ("HorseMount",         "Cưỡi ngựa"),
        ("RoadSign",           "Di chuyển nhanh"),
    };

    public void Initialize(TMP_Text eKeyText, TMP_Text lmbText)
    {
        _eKeyText = eKeyText;
        _lmbText = lmbText;
        _cam = Camera.main;
        Localization.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        Localization.OnLanguageChanged -= OnLanguageChanged;
    }

    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _eKeyText == null) return;

        var pc = GameManager.Instance?.Player;
        if (pc != null && pc.IsSitting)
        {
            _currentEKeyText = "E · " + Localization.T("Đứng dậy");
            _currentEKeyLocKey = "Đứng dậy";
            _eKeyText.text = _currentEKeyText;
            _eKeyText.gameObject.SetActive(true);
            if (_lmbText != null) _lmbText.gameObject.SetActive(false);
            return;
        }

        if (IsBlocked())
        {
            Hide();
            return;
        }

        _raycastFrameCounter++;
        bool shouldRaycast = _raycastFrameCounter >= 3;
        if (shouldRaycast) _raycastFrameCounter = 0;

        string colliderName = _lastHitName;
        GameObject colliderGo = _lastHitGo;

        if (shouldRaycast)
        {
            var ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (!Physics.Raycast(ray, out var hit, _rayRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                _lastHitName = null;
                _lastHitGo = null;
                Hide();
                return;
            }
            colliderName = hit.collider.transform.name;
            _lastHitName = colliderName;
            _lastHitGo = hit.collider.gameObject;
        }
        else if (colliderName == null)
        {
            return;
        }

        string eLocKey = ResolveEKeyLocKey(colliderName, colliderGo);

        if (eLocKey != null)
        {
            if (eLocKey != _currentEKeyLocKey)
            {
                _currentEKeyText = "E · " + Localization.T(eLocKey);
                _currentEKeyLocKey = eLocKey;
                _eKeyText.text = _currentEKeyText;
            }
            _eKeyText.gameObject.SetActive(true);
        }
        else
        {
            _currentEKeyLocKey = null;
            _eKeyText.gameObject.SetActive(false);
        }

        if (_lmbText != null)
        {
            string lmbLocKey = ResolveLmbLocKey(colliderName);
            if (lmbLocKey != null)
            {
                if (lmbLocKey != _currentLmbLocKey)
                {
                    _currentLmbKeyText = "LMB · " + Localization.T(lmbLocKey);
                    _currentLmbLocKey = lmbLocKey;
                    _lmbText.text = _currentLmbKeyText;
                }
                _lmbText.gameObject.SetActive(true);
            }
            else
            {
                _currentLmbLocKey = null;
                _lmbText.gameObject.SetActive(false);
            }
        }
    }

    private string ResolveEKeyLocKey(string colliderName, GameObject go)
    {
        foreach (var (name, locKey) in _interactables)
        {
            if (colliderName == name) return locKey;
        }

        if (colliderName != null && colliderName.StartsWith("GoblinPet"))
            return "Điều khiển";

        if (colliderName != null && colliderName.StartsWith("GoblinChest"))
            return "Mở kho";

        if (colliderName != null && colliderName.StartsWith("EventBlock_"))
            return "Kích hoạt";

        if (go != null && (go.name == "Door" || (go.transform.parent != null && go.transform.parent.name == "Door")))
            return "Mở cửa";

        if (go != null && CraftingManager.ResolveStationCategory(go.GetComponent<Collider>()) != null)
            return "Chế Tạo";

        var pc = GameManager.Instance?.Player;
        if (pc != null && !pc.IsSitting &&
            SittableSeat.FindNearest(pc.transform.position, 2.6f) != null)
            return "Ngồi";

        return null;
    }

    private string ResolveLmbLocKey(string colliderName)
    {
        var tool = ToolManager.Instance?.GetSelectedItemType();
        if (string.IsNullOrEmpty(tool) || tool == "empty" || tool == "hammer")
            return null;

        if (tool == "axe" && colliderName.Contains("Tree"))
            return "Chặt";
        if (tool == "pickaxe" && colliderName.Contains("Rock"))
            return "Đào";
        if (tool == "hoe" && colliderName.Contains("Field"))
            return "Cày";
        if (tool == "club" && (colliderName.Contains("Livestock") || colliderName.Contains("Fish")))
            return "Đánh";

        return null;
    }

    private void Hide()
    {
        _currentEKeyLocKey = null;
        _currentLmbLocKey = null;
        _eKeyText.gameObject.SetActive(false);
        if (_lmbText != null) _lmbText.gameObject.SetActive(false);
    }

    private void OnLanguageChanged()
    {
        if (_currentEKeyLocKey != null && _eKeyText != null && _eKeyText.gameObject.activeSelf)
        {
            _eKeyText.text = "E · " + Localization.T(_currentEKeyLocKey);
        }
        if (_currentLmbLocKey != null && _lmbText != null && _lmbText.gameObject.activeSelf)
        {
            _lmbText.text = "LMB · " + Localization.T(_currentLmbLocKey);
        }
    }

    private static bool IsBlocked()
    {
        if (GameManager.Instance != null && GameManager.Instance.GamePaused) return true;

        if (WifeNPC.Instance != null && WifeNPC.Instance.IsDialogActive) return true;
        if (BuffaloDialog.Instance != null && BuffaloDialog.Instance.IsDialogActive) return true;
        if (RichManNPC.Instance != null && RichManNPC.Instance.IsDialogActive) return true;
        if (PoliceOfficerNPC.Instance != null && PoliceOfficerNPC.Instance.IsDialogActive) return true;
        if (PagodaMonkNPC.Instance != null && PagodaMonkNPC.Instance.IsDialogActive) return true;
        if (ChefNPC.Instance != null && ChefNPC.Instance.IsDialogActive) return true;
        if (CafeBarista.Instance != null && CafeBarista.Instance.IsDialogActive) return true;
        if (LibrarianNPC.Instance != null && LibrarianNPC.Instance.IsDialogActive) return true;
        if (ImmigrantNpc.Instance != null && ImmigrantNpc.Instance.IsDialogActive) return true;
        if (FishingShopNPC.Instance != null && FishingShopNPC.Instance.IsDialogActive) return true;
        if (GoblinCommandMenu.Instance != null && GoblinCommandMenu.Instance.IsOpen) return true;

        return false;
    }
}
