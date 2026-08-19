using UnityEngine;
using TMPro;

public class InteractionPrompt : MonoBehaviour
{
    private TMP_Text _eKeyText;
    private TMP_Text _lmbText;
    private Camera _cam;
    private readonly float _rayRange = 4f;

    private string _currentEKeyLocKey;
    private string _currentLmbLocKey;

    private static readonly (string colliderName, string locKey)[] _interactables = {
        ("WifeNpc",        "Nói chuyện"),
        ("RichManNpc",     "Nói chuyện"),
        ("PoliceOfficer",  "Nói chuyện"),
        ("RestaurantNPC",  "Nói chuyện"),
        ("ImmigrantNpc",   "Nói chuyện"),
        ("PagodaMonkNpc",  "Cầu nguyện"),
        ("LibrarianNPC",   "Đọc sách"),
        ("BuffaloEntity",  "Tương tác"),
        ("Bed",            "Ngủ"),
        ("VendorNPC",      "Mua sắm"),
        ("ToolShopNPC",    "Mua sắm"),
        ("ConvenienceNPC", "Mua sắm"),
        ("GroceryNPC",     "Mua sắm"),
        ("CafeNPC",        "Mua sắm"),
        ("FishingShopNPC", "Mua sắm"),
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

        if (IsBlocked())
        {
            Hide();
            return;
        }

        var ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _rayRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            Hide();
            return;
        }

        string colliderName = hit.collider.transform.name;
        string eLocKey = ResolveEKeyLocKey(colliderName, hit.collider.gameObject);

        if (eLocKey != null)
        {
            _currentEKeyLocKey = eLocKey;
            _eKeyText.text = "E · " + Localization.T(eLocKey);
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
                _currentLmbLocKey = lmbLocKey;
                _lmbText.text = "LMB · " + Localization.T(lmbLocKey);
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

        if (go.name == "Door" || (go.transform.parent != null && go.transform.parent.name == "Door"))
            return "Mở cửa";

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

        return false;
    }
}
