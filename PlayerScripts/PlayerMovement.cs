using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// This component handles the movement of the player
/// including jumping and double jumping.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _movementSpeed = 10f;
    [SerializeField] private float _airbourneMovementSpeed = 10f;
    [SerializeField] private float _jumpSpeed = 10f;
    // how long can the player still jump once they arent grounded
    [SerializeField] private float _coyoteTime = 0.1f;

    [SerializeField] AudioSource _audioPlayer;
    [SerializeField] AudioClip _jump;

    [SerializeField] GameObject pauseMenu;

    private PlayerSwordHandling _swordHandler = null;
    private CharacterController _characterController = null;
    private Animator _anim = null;

    public Vector2 MovementInput { get; private set; } = Vector2.zero;
    private float _verticalSpeed = 0;

    // This timescale is used to slow the player down during special actions, like attacking
    public float MovementTimeScale { get; set; } = 1f;
    private float _lastGroundedTime = 0.0f;
    private PlayerInput input;

    private Vector3 _movement;
    public Vector3 Movement
    {
        get { return _movement; }
    }

    void Start()
    {
        input = GetComponent<PlayerInput>();
        _swordHandler = GetComponent<PlayerSwordHandling>();
        _characterController = GetComponent<CharacterController>();
        _anim = GetComponentInChildren<Animator>();
    }
    
    void Update()
    {
        HandleGravity();

        HandleMovement();
    }

    /// <summary>
    /// This method updates the players movement based on the custom gravity
    /// </summary>
    private void HandleGravity()
    {
        if (_characterController.isGrounded)
        {
            // if the player is grounded we reset their vertical speed
            _verticalSpeed = Mathf.Max(_verticalSpeed, -0.1f);

            _anim.SetBool("TouchingGround", true);

            // if the player is grounded we reset values that can only be true while airbourne
            KilledInAir = false;
            _hasJumped = false;

            _lastGroundedTime = Time.time;
        }
        else
        {
            // if the player isn't grounded we apply gravity
            _verticalSpeed += Physics.gravity.y * Time.deltaTime * MovementTimeScale;
            _anim.SetBool("TouchingGround", false);
        }
    }

    /// <summary>
    /// This method handles the frame to frame movement, based on the last
    /// player input.
    /// </summary>
    private void HandleMovement()
    {
        // The player can is grounded or is at least still under coyote time
        bool onGround = _characterController.isGrounded || Time.time - _lastGroundedTime < _coyoteTime;

        // we define our movement vector based on the speed and input of the player
        float usedMovementSpeed = onGround ? _movementSpeed : _airbourneMovementSpeed;
        _movement = new Vector3(MovementInput.x * usedMovementSpeed, _verticalSpeed, MovementInput.y * usedMovementSpeed);

        // We rotate the movement vector, since the camera is rotated 45 degrees
        _movement = Quaternion.AngleAxis(45, Vector3.up) * _movement;

        _characterController.Move(_movement * Time.deltaTime * MovementTimeScale);
    }

    /// <summary>
    /// This method handles the movement input of the player.
    /// </summary>
    /// <param name="input">The input of the left controller stick or WASD</param>
    private void OnMove(InputValue input)
    {
        // We grab the input
        MovementInput = input.Get<Vector2>();
        _anim.SetFloat("WalkSpeed", MovementInput.sqrMagnitude);

        // we show different player sprites depending on the movement direction
        _anim.SetBool("WalkingRight", MovementInput.x > 0);

        // if the is no input defining where the sword is aimed towards, the movement input
        // is being used as that input.
        if (_swordHandler.SwordDirection.sqrMagnitude < 0.1f)
            _swordHandler.AimSword(new Vector3(MovementInput.x, 0, MovementInput.y));
    }


    private bool _hasJumped = false;
    public bool KilledInAir { get; set; } = false;
    /// <summary>
    /// This method checks whether the player can jump.
    /// The player can jump in 3 scenarios.
    /// 1. The player is on the ground.
    /// 2. The player just left the ground and is still under cayote time
    /// 3. The player is in the air and killed an enemy since the last jump, while airbourne
    /// </summary>
    /// <returns>Can the player jump?</returns>
    private bool CanJump()
    {
        if (_characterController.isGrounded)
            return true;
        if ((!_hasJumped && Time.time - _lastGroundedTime < _coyoteTime))
            return true;

        if (KilledInAir)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// This method lets the player jump when the jump
    /// button is pressed.
    /// It first checks however, whether the player can jump.
    /// </summary>
    public void OnJump()
    {
        if (CanJump())
        {
            // We set the vertical speed so the movement happens in the update
            _verticalSpeed = _jumpSpeed;
            _lastGroundedTime = Time.time;
            _hasJumped = true;
            // We reset Killed in air, since it keeps track of whether we can jump in the air
            KilledInAir = false;
            _audioPlayer.PlayOneShot(_jump);
            _anim.SetTrigger("Jump");
            _anim.SetBool("TouchingGround", false);
        }
    }

    private void OnPause()
    {
        pauseMenu.SetActive(!pauseMenu.activeSelf);
    }
}
