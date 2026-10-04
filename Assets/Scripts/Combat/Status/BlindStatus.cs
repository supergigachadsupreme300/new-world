using UnityEngine;

/// <summary>
/// Dark-magic signature status (§3.7). Attached to the hit target's root. Black fog engulfs
/// the victim for the duration — a semi-transparent dark sphere that follows the character,
/// cutting their field of vision (when the victim is the local player the fog clings to the
/// main camera instead, so the player themselves sees the world dim around them). Re-applying
/// refreshes the duration.
/// </summary>
public class BlindStatus : MonoBehaviour
{
    /// <summary>Seconds the blindness lingers.</summary>
    public float Duration = 4f;

    /// <summary>Seconds the status has left (HUD strip reads this under the player bars).</summary>
    public float Remaining => Mathf.Max(0f, _expiresAt - Time.time);

    /// <summary>Radius of the fog dome around a world target.</summary>
    private const float FogRadius = 1.6f;

    /// <summary>Camera fog is scaled up so the player stands inside the dark.</summary>
    private const float CameraFogScale = 1.4f;

    /// <summary>Opacity of the fog (0..1).</summary>
    private const float FogAlpha = 0.42f;

    private float _expiresAt;
    private Transform _fog;
    private Material _fogMaterial;
    private bool _isPlayer;
    private float _radius;

    // 1e5: cached main camera for the per-frame fog follow (was Camera.main every frame).
    private Camera _mainCam;

    /// <summary>Apply/refresh the blind status on a hit target.</summary>
    public static BlindStatus Apply(GameObject target, float duration)
    {
        if (target == null) return null;
        var root = target.transform.root.gameObject;
        var blind = root.GetComponent<BlindStatus>();
        if (blind == null)
            blind = root.AddComponent<BlindStatus>();
        blind.Duration = Mathf.Max(duration, 0.5f);
        blind._expiresAt = Time.time + blind.Duration;
        blind.EnsureFog();
        return blind;
    }

    private void EnsureFog()
    {
        if (_fog != null) return;

        _isPlayer = GetComponent<PlayerController>() != null;
        _radius = _isPlayer ? FogRadius * CameraFogScale : FogRadius;

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "BlindFog";
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * (_radius * 2f);

        var renderer = sphere.GetComponent<MeshRenderer>();
        _fogMaterial = new Material(Shader.Find("Standard"));
        _fogMaterial.SetFloat("_Mode", 2f);
        _fogMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _fogMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _fogMaterial.SetInt("_ZWrite", 0);
        _fogMaterial.DisableKeyword("_ALPHATEST_ON");
        _fogMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _fogMaterial.EnableKeyword("_ALPHABLEND_ON");
        _fogMaterial.renderQueue = 3000;
        _fogMaterial.color = new Color(0f, 0f, 0f, FogAlpha);
        renderer.sharedMaterial = _fogMaterial;

        _fog = sphere.transform;
    }

    private void Update()
    {
        FollowFog();
        if (Time.time >= _expiresAt)
            Destroy(this);
    }

    /// <summary>For the local player the fog hugs the camera (reduces their field of vision);
    /// for any other target it clings to the character body so the caster sees them engulfed.</summary>
    private void FollowFog()
    {
        if (_fog == null) return;
        if (_isPlayer)
        {
            if (_mainCam == null)
                _mainCam = Camera.main;
            if (_mainCam != null)
                _fog.position = _mainCam.transform.position - _mainCam.transform.forward * (_radius * 0.25f) + Vector3.up * 0.2f;
        }
        else
        {
            _fog.position = transform.position + Vector3.up * (_radius * 0.85f);
        }
    }

    private void OnDestroy()
    {
        if (_fog != null) Destroy(_fog.gameObject);
        if (_fogMaterial != null) Destroy(_fogMaterial);
    }
}