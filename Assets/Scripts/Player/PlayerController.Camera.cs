/// <summary>Partial player controller — camera / free-look cluster: mouse look, look-rotation
/// overrides, camera pivot creation and first/third-person follow setup.
/// Mechanically split from PlayerController.cs; no behavior or signature changes.</summary>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    /// <summary>Camera far plane (1ef): must clear the far shell's outer edge at the default
    /// render radius (67 chunks + 2 keep = 2,070 m) with margin — Unity's default 1,000 would
    /// clip the whole mid/horizon shell. Sized for the default radius; pushing Render Distance
    /// far past ~73 chunks clips at this plane.</summary>
    private const float CameraFarPlane = 2200f;

    public void SnapLookYaw(float yaw)
    {
        _yaw = yaw;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    public void SetLookRotation(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = pitch;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    /// <summary>Current vertical look pitch (°; positive = looking down, negative = up).
    /// Used by the procedural torso/upper-body animation to bend with the camera.</summary>
    public float LookPitch => _pitch;

    private void HandleMouseLook()
    {
        if (MagicWheelUI.IsOpen)
            return;
        Vector2 delta = Vector2.zero;
        if (!GameInput.IsMobile && Mouse.current != null)
            delta = Mouse.current.delta.ReadValue();
        if (GameInput.IsMobile)
            delta += MobileInputController.TakeLookDelta();

        if (delta == Vector2.zero)
            return;

        float sens = SettingsManager.MouseSensitivity;
        _yaw += delta.x * sens * 0.02f;
        _pitch -= delta.y * sens * 0.02f * (SettingsManager.InvertY ? -1f : 1f);
        _pitch = Mathf.Clamp(_pitch, -60f, 60f);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.fieldOfView = 60f;
        cameraComponent.farClipPlane = CameraFarPlane;
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraObject.transform.position = transform.position + new Vector3(0f, 1.5f, -4f);
        cameraObject.transform.rotation = Quaternion.LookRotation(transform.position + Vector3.up * 1.5f - cameraObject.transform.position);
    }

    private void SetupPlayerCamera()
    {
        if (_cameraPivot == null)
        {
            _cameraPivot = new GameObject("CameraPivot").transform;
            _cameraPivot.SetParent(transform);
            _cameraPivot.localPosition = new Vector3(0f, 1.5f, 0f);
            _cameraPivot.localRotation = Quaternion.identity;
        }

        var cam = Camera.main;
        if (cam == null)
            return;

        cam.tag = "MainCamera";
        cam.farClipPlane = CameraFarPlane;
        if (cam.transform.parent != null)
            cam.transform.SetParent(null);

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        cam.transform.position = _cameraPivot.position;
        cam.transform.rotation = _cameraPivot.rotation;

        follow.Target = _cameraPivot;
        follow.Offset = Vector3.zero;
        follow.SmoothSpeed = 20f;

        // First / third-person camera switch.
        var switcher = GetComponent<CameraModeSwitch>();
        if (switcher == null)
            switcher = gameObject.AddComponent<CameraModeSwitch>();
        switcher.Setup(this, cam, _cameraPivot);
        _cameraMode = switcher;
    }

    /// <summary>Public accessor for the camera pivot (used by <see cref="CameraModeSwitch"/>).</summary>
    public Transform PlayerCameraPivot => _cameraPivot;
}
