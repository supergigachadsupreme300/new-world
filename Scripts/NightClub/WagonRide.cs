using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-steered driving for the club's delivery wagon. While the player is
/// seated on the wagon's seat (via the existing sit system) the wagon advances
/// along its horse direction (local -X) and steers with W/S + A/D (or the
/// mobile joystick). Movement is gated by NavGrid walkability so the wagon
/// stops at structures and water instead of clipping through them.
/// </summary>
public class WagonRide : MonoBehaviour
{
    private const float ForwardSpeed = 7f;
    private const float ReverseSpeed = 4f;
    private const float TurnSpeed = 90f;

    private float _yaw;
    private bool _wasRiding;

    void Start()
    {
        _yaw = transform.localEulerAngles.y;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GamePaused)
            return;

        var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null)
            return;
        var sit = player.GetComponent<PlayerSitController>();
        bool riding = sit != null && sit.IsSitting && sit.Seat != null && sit.Seat.transform.IsChildOf(transform);
        if (riding)
        {
            if (!_wasRiding)
                GameManager.Instance?.UIManager?.ShowMessage(Localization.T("Lái xe: W/A/D — E để xuống"), 2.5f);
            _wasRiding = true;
        }
        else
        {
            _wasRiding = false;
        }
        if (!riding)
            return;

        Vector2 input = ReadInput();

        float throttle = input.y > 0f ? ForwardSpeed : input.y < 0f ? ReverseSpeed : 0f;
        if (input.x != 0f)
        {
            _yaw -= input.x * TurnSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }
        if (throttle == 0f)
            return;

        Vector3 forward = -transform.right; // horse leads along local -X
        Vector3 next = transform.position + forward * throttle * Time.deltaTime;
        if (NavGrid.Instance != null && !NavGrid.Instance.IsWalkableAt(next + forward * 0.9f))
            return;
        next.y = transform.position.y;
        transform.position = next;
    }

    private Vector2 ReadInput()
    {
        if (GameInput.IsMobile)
        {
            var joy = MobileInputController.MoveAxis;
            if (joy != Vector2.zero)
                return joy;
        }

        if (Keyboard.current == null)
            return Vector2.zero;

        float x = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            x += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            x -= 1f;

        float y = 0f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            y -= 1f;

        return new Vector2(x, y);
    }
}