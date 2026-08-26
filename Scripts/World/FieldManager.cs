using UnityEngine;

public class FieldManager : MonoBehaviour
{
    public static FieldManager Instance { get; private set; }

    [Header("Field Textures")]
    public Texture2D FieldTexture;
    public Material FieldMaterial;

    [Header("Field Preview")]
    public Material FieldPreviewMaterial;
    public Color FieldPreviewColor = new Color(150f / 255f, 100f / 255f, 50f / 255f, 140f / 255f);

    private GameObject _fieldPreview;
    private readonly float _fieldSize = 2f; // Match WorldBuilder field tile size

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        CreateFieldPreview();
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.InGame)
            return;

        // Update field preview only when the player is holding the hoe
        if (ToolManager.Instance == null || ToolManager.Instance.GetSelectedItemType() != "hoe")
        {
            if (_fieldPreview != null)
                _fieldPreview.SetActive(false);
            return;
        }

        UpdateFieldPreview();
    }

    private void CreateFieldPreview()
    {
        _fieldPreview = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fieldPreview.name = "FieldPreview";
        _fieldPreview.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _fieldPreview.transform.localScale = new Vector3(_fieldSize, _fieldSize, 1f);
        
        var renderer = _fieldPreview.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = FieldPreviewColor;
        }
        Destroy(_fieldPreview.GetComponent<Collider>());
        _fieldPreview.SetActive(false);
    }

    private void UpdateFieldPreview()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        var ray = new Ray(cam.transform.position + cam.transform.forward * 0.3f, cam.transform.forward);
        if (Physics.Raycast(ray, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.name == "FieldTile")
            {
                // Compute adjacent placement toward the player/camera direction
                Vector3 fieldCenter = hit.collider.transform.position;
                Vector3 cameraDir = cam.transform.position - fieldCenter;
                Vector3 cameraDirXZ = new Vector3(cameraDir.x, 0f, cameraDir.z);

                Vector3 adjacentPos = fieldCenter;
                if (cameraDirXZ.sqrMagnitude > 0.001f)
                {
                    if (Mathf.Abs(cameraDirXZ.x) > Mathf.Abs(cameraDirXZ.z))
                        adjacentPos.x += Mathf.Sign(cameraDirXZ.x) * _fieldSize;
                    else
                        adjacentPos.z += Mathf.Sign(cameraDirXZ.z) * _fieldSize;
                }

                adjacentPos.y = 0.01f;
                _fieldPreview.transform.position = adjacentPos;
                _fieldPreview.SetActive(true);
                return;
            }

            if (hit.collider.name == "Ground" || hit.collider.name == "FieldVisual")
            {
                Vector3 hitPos = hit.point;
                Vector3 gridPos = new Vector3(
                    Mathf.Round(hitPos.x),
                    0.01f,
                    Mathf.Round(hitPos.z)
                );

                _fieldPreview.transform.position = gridPos;
                _fieldPreview.SetActive(true);
                return;
            }
        }

        _fieldPreview.SetActive(false);
    }

    public bool TryGetPreviewPosition(out Vector3 previewPosition)
    {
        previewPosition = Vector3.zero;
        if (_fieldPreview == null || !_fieldPreview.activeSelf)
            return false;

        previewPosition = _fieldPreview.transform.position;
        return true;
    }
}
