using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSitController : MonoBehaviour
{
    public bool IsSitting { get; private set; }

    public SittableSeat Seat => _seat;

    private PlayerController _player;
    private CharacterController _controller;
    private SittableSeat _seat;
    private GameObject _standingModel;
    private GameObject _sitModel;
    private float _seatYaw;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        _controller = GetComponent<CharacterController>();
    }

    public void BeginSit(SittableSeat seat)
    {
        if (seat == null || IsSitting)
            return;
        _seat = seat;

        var existing = transform.Find("PlayerModel");
        if (existing != null)
            _standingModel = existing.gameObject;

        _sitModel = MapBuilder.BuildSitPlayerModel(transform);
        Vector3 facing = _seat.Facing;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            facing.Normalize();
        else
            facing = transform.forward;
        _seatYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        _sitModel.transform.rotation = Quaternion.Euler(0f, _seatYaw, 0f);

        if (_standingModel != null)
            _standingModel.SetActive(false);
        if (_controller != null)
            _controller.enabled = false;

        SnapToSeat();
        IsSitting = true;
        _player?.SnapLookYaw(_seatYaw);
    }

    private void SnapToSeat()
    {
        if (_seat == null)
            return;
        transform.position = _seat.WorldAnchor;

        if (_sitModel != null)
            _sitModel.transform.rotation = transform.rotation;
    }

    public void UpdateSitting()
    {
        if (!IsSitting)
            return;
        if (_seat == null)
        {
            EndSit();
            return;
        }
        SnapToSeat();

        if (_player != null)
            _player.Stamina = Mathf.Min(_player.MaxStamina, _player.Stamina + 1f * Time.deltaTime);

        if (Keyboard.current != null &&
            (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            EndSit();
        }
        else if (GameInput.IsMobile && MobileInputController.Consume("interact"))
        {
            EndSit();
        }
    }

    public void EndSit()
    {
        if (!IsSitting)
            return;
        IsSitting = false;

        if (_sitModel != null)
        {
            Destroy(_sitModel);
            _sitModel = null;
        }
        if (_standingModel != null)
            _standingModel.SetActive(true);

        if (_seat != null)
        {
            Vector3 standPos = _seat.WorldAnchor + _seat.Facing * 0.8f;
            standPos.y = _seat.WorldAnchor.y;
            transform.position = standPos;
        }
        _seat = null;

        if (_controller != null)
            _controller.enabled = true;

        GameManager.Instance?.UIManager?.ShowMessage(
            Localization.T("Bạn đã đứng dậy."), 1.5f);
    }
}