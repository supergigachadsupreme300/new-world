/// <summary>Partial player controller — movement / locomotion cluster: running, sprinting, riding,
/// flight, water, dodge, jump/gravity, movement-input reading and the stamina economy.
/// Mechanically split from PlayerController.cs; no behavior or signature changes.</summary>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    public void SetInWater(bool inWater, float speedMul, bool allowJump)
    {
        InWater = inWater;
        _waterSpeedMul = inWater ? speedMul : 1f;
        _waterAllowJump = inWater ? allowJump : true;
    }

    public bool InWater { get; private set; }

    public bool IsMoving
    {
        get
        {
            if (_controller == null)
                return false;
            var v = _controller.velocity;
            return new Vector2(v.x, v.z).magnitude > 0.5f;
        }
    }

    public bool IsFlying => Time.time < _flightUntil;

    /// <summary>Max stamina (stamina cap). Reads the current <see cref="PlayerStats"/> maximum
    /// (Endurance-scaled + tree perks §3.3) when available; falls back to 1000 before stats are rigged.</summary>
    public float MaxStamina
    {
        get
        {
            var stats = StatsCached;
            return stats != null ? stats.MaxStamina : 1000f;
        }
    }

    private void HandleMovement()
    {
        bool dialogBlocked = (WifeNPC.Instance != null && WifeNPC.Instance.IsDialogActive) ||
                             (BuffaloDialog.Instance != null && BuffaloDialog.Instance.IsDialogActive) ||
                             (RichManNPC.Instance != null && RichManNPC.Instance.IsDialogActive) ||
                             (PoliceOfficerNPC.Instance != null && PoliceOfficerNPC.Instance.IsDialogActive) ||
                             (PagodaMonkNPC.Instance != null && PagodaMonkNPC.Instance.IsDialogActive) ||
                             (ChefNPC.Instance != null && ChefNPC.Instance.IsDialogActive) ||
                             (LibrarianNPC.Instance != null && LibrarianNPC.Instance.IsDialogActive) ||
                             (CraftingManager.Instance != null && CraftingManager.Instance.IsOpen);
        Vector2 input = dialogBlocked ? Vector2.zero : ReadMoveInput();
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        float mag = direction.magnitude;
        if (mag > 1f)
        {
            direction /= mag;
            mag = 1f;
        }

        bool canSprint = !InWater;
        bool flying = IsFlying;
        bool sprint = canSprint && !flying &&
            ((Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
             (GameInput.IsMobile && MobileInputController.IsHeld("sprint"))) &&
            Stamina > 0f && mag > 0f;
        var playerStats = StatsCached;
        float moveSpeedPerkMult = playerStats != null && playerStats.BaseMoveSpeed > 0f
            ? playerStats.MaxMoveSpeed / playerStats.BaseMoveSpeed : 1f;
        float speed = flying
            ? FlightSpeed
            : MoveSpeed * _waterSpeedMul * (sprint ? SprintMultiplier : 1f) * moveSpeedPerkMult;
        _lastEffectiveSpeed = speed;

        bool dodgePressed = !dialogBlocked && !flying && _controller != null && _controller.isGrounded &&
            ((Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) ||
             (GameInput.IsMobile && MobileInputController.Consume("dodge")));
        if (dodgePressed && !_dodging && Stamina >= DodgeCost)
        {
            _dodging = true;
            _dodgeTimer = DodgeDuration;
            _invulnerableUntil = Time.time + DodgeIFrameDuration;
            SpendStamina(DodgeCost);
        }

        if (_controller != null)
        {
            Vector3 move = transform.TransformDirection(direction) * speed;

            if (_dodging)
            {
                _dodgeTimer -= Time.deltaTime;
                if (_dodgeTimer <= 0f)
                    _dodging = false;
            }

            if (flying)
            {
                // Free vertical movement: hold Space to ascend, LeftCtrl to descend.
                float vertical = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.spaceKey.isPressed) vertical += 1f;
                    if (Keyboard.current.leftCtrlKey.isPressed) vertical -= 1f;
                }
                _velocity.y = vertical * FlightVerticalSpeed;
            }
            else if (_controller.isGrounded)
            {
                if (_velocity.y < 0f)
                    _velocity.y = -1f;

                bool jumpPressed =
                    _waterAllowJump && !dialogBlocked && !_dodging &&
                    ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                     MobileInputController.Consume("jump"));
                if (jumpPressed)
                {
                    _velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    _jumpFrame = Time.frameCount;
                }
                else if (_velocity.y > 0f && Time.frameCount > _jumpFrame + 2)
                {
                    // No jump in progress: any surviving upward velocity is a stray
                    // injection/residual, so kill it or it would lift the player forever.
                    _velocity.y = 0f;
                }
            }
            else
            {
                _velocity.y += Gravity * Time.deltaTime;
            }

            Vector3 finalMove = move + Vector3.up * _velocity.y;
            if (_dodging)
            {
                Vector3 dash = transform.forward * DodgeSpeed;
                dash.y = Mathf.Max(dash.y, _velocity.y);
                finalMove = dash + Vector3.up * _velocity.y;
            }
            _controller.Move(finalMove * Time.deltaTime);
        }

        if (sprint)
            Stamina = Mathf.Max(0f, Stamina - SprintCost * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
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

    private void HandleStamina()
    {
        bool sprinting = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
                         (GameInput.IsMobile && MobileInputController.IsHeld("sprint"));
        bool grounded = _controller != null && _controller.isGrounded;
        if (!sprinting || !grounded || Stamina <= 0f)
        {
            float regenMul = StaminaRegenMultiplier;
            if (Time.time < _staminaRegenModifierUntil)
                regenMul *= StaminaRegenModifier;
            else if (StaminaRegenModifier != 1f)
                StaminaRegenModifier = 1f;

            // Class passive: Taoist/Monk stamina-regen modifiers compound multiplicatively (§3.2.1).
            var passives = ClassPassivesCached;
            if (passives != null)
                regenMul *= passives.StaminaRegenMul;

            // Skill-tree perk: stamina-regen % (§3.3).
            var pStats = StatsCached;
            if (pStats != null)
                regenMul *= pStats.StaminaRegenMul;

            Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegenRate * regenMul * Time.deltaTime);

            // Base HP regen + class passive HP regen (Monk "Meditation", Taoist "Yi Symbol", aura buffs).
            float hpRegenFraction = 2f * (Stamina / MaxStamina) * Time.deltaTime;
            if (passives != null)
                hpRegenFraction += passives.HpRegenPerSecond * MaxHP * Time.deltaTime;
            if (pStats != null)
                hpRegenFraction += pStats.HealthRegenPerSecondFlat * MaxHP * Time.deltaTime;
            if (Time.time < _classBuffUntil && _classBuffHpRegenPerSecond > 0f)
                hpRegenFraction += _classBuffHpRegenPerSecond * MaxHP * Time.deltaTime;
            if (hpRegenFraction > 0f && HP < MaxHP)
            {
                HP = Mathf.Min(MaxHP, HP + Mathf.RoundToInt(hpRegenFraction));
                GameManager.Instance?.UIManager?.UpdatePlayerHud(HP, MaxHP, Stamina, MaxStamina, Money);
            }
        }
    }

    public bool SpendStamina(float amount)
    {
        if (Stamina < amount)
            return false;
        Stamina -= amount;
        return true;
    }

    public void ApplyStaminaRegenModifier(float modifier, float duration)
    {
        StaminaRegenModifier = modifier;
        _staminaRegenModifierUntil = Time.time + duration;
    }

    /// <summary>Seconds left on the eat/drink stamina-regen modifier (HUD status strip).</summary>
    public float StaminaBuffRemaining => Mathf.Max(0f, _staminaRegenModifierUntil - Time.time);

    /// <summary>True while a food/drink stamina-regen modifier is still active.</summary>
    public bool HasStaminaBuff => StaminaBuffRemaining > 0f;
}
