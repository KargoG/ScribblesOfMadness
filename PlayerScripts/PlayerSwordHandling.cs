using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// This component handles all player controlled functionality
/// related to the players sword. This includes things like
/// aiming, sword throwing, returning the sword, grappling
/// to the sword and behaviour related to that.
/// </summary>
public class PlayerSwordHandling : MonoBehaviour
{
    [SerializeField] private SwordMovement _sword = null;
    [SerializeField] private float _swordThrowingDistance = 10;

    // When throwing the sword at an enemy the player can grapple towards
    // it, if the grapple happens within this time window
    [SerializeField] private float _timeWindowForGrapple = 0.5f;
    // This timescale gets activated for the player, when able
    // to grapple towards an enemy.
    [SerializeField] private float _timeScaleOnEnemyHit = 0.3f;
    // the distance at which the aim assist kicks in
    [SerializeField] private float _minDistanceForAimAssist = 5;
    // A game object used to preview where the sword will hit if
    // thrown with the current input
    [SerializeField] private GameObject _hitPreview = null;
    [SerializeField] private LayerMask _wallLayers = 0;

    [SerializeField] private UnityEvent _onThrowingSword = new UnityEvent();
    [SerializeField] private UnityEvent _onRetrievingSword = new UnityEvent();
    [SerializeField] private UnityEvent _onGrapple = new UnityEvent();
    [SerializeField] private UnityEvent _onSwordStuck = new UnityEvent();

    [SerializeField] private int _damageOnHit = 1;

    private PlayerMovement _playerMovement = null;
    private CharacterController _characterController = null;

    // the parent of the sword while the player is holding it
    private Transform _swordHolder = null;
    private Vector3 _swordOffset = Vector3.zero;

    private bool _wantsToAttack = false;
    private SpriteRenderer _hitPreviewRenderer = null;
    
    // The transform of a sword used to indicate the aim direction
    // while the player is not holding the sword
    private Transform _ghostSword = null;

    public Vector3 SwordDirection { get; private set; } = Vector3.zero;
    void Start()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _characterController = GetComponent<CharacterController>();
        _swordHolder = _sword.transform.parent;
        _swordOffset = _sword.transform.localPosition;
        _hitPreviewRenderer = _hitPreview.GetComponentInChildren<SpriteRenderer>();
        _ghostSword = _swordHolder.Find("GhostSword");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_sword.transform.position + _sword.transform.forward, 2);
    }

    private void Update()
    {
        if (!_sword.gameObject.activeSelf)
            return;
        
        // if we would hit something when throwing the sword with the current input
        if (Physics.Raycast(_ghostSword.position, Quaternion.Euler(0, 45, 0) * -_swordHolder.transform.forward, out RaycastHit hit, 100, _wallLayers))
        {
            // we show the hit preview
            _hitPreview.gameObject.SetActive(true);
            _hitPreview.transform.position = hit.point + hit.normal * 0.3f;
            _hitPreview.transform.up = hit.normal;
            // we color the preview based on whether we would hit a wall or an enemy
            _hitPreviewRenderer.color = Mathf.Pow(2, hit.collider.gameObject.layer) == _enemies ? Color.red : Color.white;
        }
        else
        {
            _hitPreview.gameObject.SetActive(false);
        }
    }

    // A mask defining all layers the player can grapple
    [SerializeField] private LayerMask _grappleables = 0;
    [SerializeField] private LayerMask _enemies = 0;

    /// <summary>
    /// This IEnumerator handles letting the sword fly through the air.
    /// The method lets the following things happen in succession:
    /// 
    /// 1. If the player doesn't hold the sword it gets returned
    /// 2. The sword starts flying into the input direction
    /// 3. If the sword hits an enemy the grapple window start
    /// 3.1. If the player decides to grapple towards the enemy, we
    ///      stop the sword and start grappling (the following steps will not happen).
    /// 4. We end the grapple window and continue the sword throw
    /// 5. Does the sword hit a wall?
    /// 5.a. Yes, so the sword gets stuck and we start grappling towards
    ///      the wall if the player chooses.
    /// 5.b. No, so the sword returns to the player.
    /// </summary>
    private IEnumerator HandleSwordFlying()
    {
        // 1.
        // We start by preparing the throw

        // If the player is currently grappling or retrieving the sword, the sword can't be thrown
        if (_grappling || _sword.SwordState != SwordState.holding && _sword.SwordState != SwordState.returning)
            yield break;

        // During the preperation we first freeze the players movement to
        // not mess up the aim and positioning during the preparation.
        _playerMovement.MovementTimeScale = 0f;

        if (_sword.SwordState == SwordState.returning)
        {
            // We wait until the player holds the sword
            yield return new WaitUntil(() => { return _sword.SwordState == SwordState.holding; });
            yield return null;
        }

        // 2.
        // Once the player holds the sword we set the proper wanted
        // timescale and start throwing the sword
        _playerMovement.MovementTimeScale = 0.2f;


        // If the player is not aiming we use the movement input for the shooting direction
        Vector3 newSwordDirection = SwordDirection;
        if (newSwordDirection.sqrMagnitude < 0.1f)
            newSwordDirection = new Vector3(_playerMovement.MovementInput.x, 0, _playerMovement.MovementInput.y);

        Vector3 shootingDirection = newSwordDirection.sqrMagnitude > 0.1f ?
            newSwordDirection :
            new Vector3(_sword.MovementDirection.x, 0, _sword.MovementDirection.z);
        shootingDirection = Quaternion.Euler(0, 45, 0) * shootingDirection;


        // We check if something is directly next to the player in attack direction
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 0.5f, shootingDirection, 2, _grappleables);
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, shootingDirection * 2, Color.red, 2f);
        if (hits.Length > 0)
        {
            // if there is we abort the grapple
            _playerMovement.MovementTimeScale = 1f;
            yield break;
        }

        // We spherecast forward to be able to provide aim assist if we hit an enemy
        if(Physics.SphereCast(_sword.transform.position, 2f, shootingDirection, out RaycastHit hit, _swordThrowingDistance * 1.2f, _enemies))
        {
            // aim assist only applies from a certain distance
            if(hit.distance > _minDistanceForAimAssist)
                newSwordDirection = Quaternion.Euler(0, -45, 0) * (hit.collider.transform.position - _sword.transform.position).normalized;
        }

        // We play an animation and wait for it to finish
        LeanTween.rotateAround(_swordHolder.gameObject, Vector3.up, 180, 0.1f);
        yield return new WaitForSeconds(0.1f);

        // we start letting the sword actually fly
        _onThrowingSword.Invoke();
        _sword.SwordState = SwordState.flying;
        _sword.transform.parent = null;

        if (newSwordDirection.sqrMagnitude > 0.1f)
            _sword.MovementDirection = newSwordDirection;


        _sword.enabled = true;

        // 3.
        bool continueFlying = false;

        do
        {
            continueFlying = false;
            // We let the sword fly until it is potentially done (see method IsSwordDone)
            yield return new WaitUntil(IsSwordDone);

            if (_sword.EnemySwordIsStuckIn) // If the sword hits an enemy
            {
                // 3.1
                // we prepare letting the player grapple towards the enemy
                _wantsToAttack = false;
                VibrationHandler.StrongVibration(0.1f);
                _playerMovement.MovementTimeScale = 1f;
                _unscaledTimeOfHit = Time.unscaledTime;
                Time.timeScale = _timeScaleOnEnemyHit;

                // And wait to see whether the player wants to grapple
                yield return new WaitUntil(DecidedToGrapple);

                Time.timeScale = 1;

                if (_wantsToAttack) // If the player wants to grapple towards the enemy
                {
                    // we start grappling and stop the sword throw
                    _sword.enabled = false;
                    StartCoroutine(GrappleToSword(true));
                    _sword.EnemySwordIsStuckIn.GetComponent<Health>().Damage(2);
                    _sword.EnemySwordIsStuckIn = null;
                    yield break;
                }

                // 4.
                // If the player doesn't want to grapple, we just attack the enemy
                _sword.EnemySwordIsStuckIn.GetComponent<Health>().Damage(1);
                _sword.EnemySwordIsStuckIn = null;
                
                // and let the sword continue
                continueFlying = true;
            }
        } while (continueFlying);

        // 5.
        if (_sword.ObjectSwordIsStuckIn)
        {
            _onSwordStuck.Invoke();
        }
        else
        {
            GetSwordBack();
        }

        // We reset to the state before the player threw the sword
        _sword.enabled = false;
        _playerMovement.MovementTimeScale = 1f;

        // 5.a.
        // If the players sword is stuck and the player continues to press throwing the sword
        // we start the grapple towards the sword
        if (_sword.SwordState == SwordState.stuck && _stillPressing)
            StartCoroutine(GrappleToSword(false));
    }

    private float _unscaledTimeOfHit = 0;
    /// <summary>
    /// This method finds out whether the player
    /// wants to grapple during a given time window.
    /// This method is only meant as a check when the
    /// players sword hits an enemy mid throw.
    /// </summary>
    /// <returns>Did the player decide to grapple (press the button during the timeframe).</returns>
    private bool DecidedToGrapple()
    {
        return _wantsToAttack || Time.unscaledTime - _unscaledTimeOfHit > _timeWindowForGrapple;
    }

    /// <summary>
    /// This method tells us whether the sword is currently done or possibly done
    /// flying. This can be true in 2 different scenarios:
    /// 1. The sword has reached its maximal flying distance
    /// 2. the sword is stuck in anything (objects or enemies)
    /// </summary>
    /// <returns>Is the sword potentially done flying forwards?</returns>
    private bool IsSwordDone()
    {
        if ((transform.position - _sword.transform.position).sqrMagnitude > _swordThrowingDistance * _swordThrowingDistance)
            return true;

        return _sword.ObjectSwordIsStuckIn || _sword.EnemySwordIsStuckIn;
    }

    /// <summary>
    /// This method returns the players sword.
    /// </summary>
    public void GetSwordBack()
    {
        _sword.ObjectSwordIsStuckIn = null;

        _sword.transform.parent = _swordHolder;
        _sword.SwordState = SwordState.returning;
        _onRetrievingSword.Invoke();

        _sword.enabled = false;
        LeanTween.moveLocal(_sword.gameObject, _swordOffset, 0.1f).setOnComplete(() =>
        {
            _sword.transform.localRotation = Quaternion.Euler(0, -135, 0);
            _sword.SwordState = SwordState.holding;
        });
    }

    [SerializeField] private float _grappleSpeed = 5f;
    private bool _grappling = false;
    /// <summary>
    /// This IEnumerator pulls the player towards the sword.
    /// </summary>
    /// <param name="pickSword">should the sword get returned to the player on arrival?</param>
    private IEnumerator GrappleToSword(bool pickSword = false)
    {
        SwordPlayerPositioner onSwordPositioner = _sword.PlayerPositioner;

        // we freeze all default movement of the player
        _playerMovement.MovementTimeScale = 0;
        _grappling = true;
        _onGrapple.Invoke();
        while (!onSwordPositioner.CanPlayerClimb())
        {
            // we move the player frame by frame until the player is close
            // enough to the sword, to get positioned on top of it
            Vector3 playerCenter = transform.position + Vector3.up;
            Vector3 direction = onSwordPositioner.transform.position - playerCenter;
            direction.Normalize();
            _characterController.Move(direction * Time.deltaTime * _grappleSpeed);

            yield return null;
        }

        if (pickSword)
        {
            // if the player should pick up the swordthis happens
            // and afterwards jumps, to not instantly fall
            GetSwordBack();
            _playerMovement.OnJump();
        }
        else // Otherwise the player gets put on top of the sword
            onSwordPositioner.PositionPlayer();

        _grappling = false;
        _playerMovement.MovementTimeScale = 1f;
    }

    private bool _stillPressing = false;
    /// <summary>
    /// This method catches the throw sword input.
    /// When called, this method throws the players sword.
    /// </summary>
    private void OnPressThrowSword()
    {
        _stillPressing = true;
        // if the sword is stuck somewhere we return it first
        if (_sword.SwordState == SwordState.stuck && !_grappling)
        {
            GetSwordBack();
        }
        StartCoroutine(HandleSwordFlying());
    }

    private void OnReleaseThrowSword()
    {
        _stillPressing = false;
    }    

    /// <summary>
    /// This method catches and acts on the
    /// return sword input.
    /// When called, it returns the players sword
    /// if its stuck somewhere.
    /// </summary>
    private void OnReturnSword()
    {
        // We only return the sword if its stuck somewhere and we are not
        // currently grappling towards it.
        if (_sword.SwordState == SwordState.stuck && !_grappling) GetSwordBack();
    }

    /// <summary>
    /// This method catches and acts on the
    /// Grapple to sword input.
    /// When called, it starts the grappling to the
    /// sword.
    /// </summary>
    private void OnGrappleToSword()
    {
        // We only grapple to the sword if its stuck somewhere and
        // we are not already grappling towards it.
        if (_sword.SwordState == SwordState.stuck && !_grappling)
            StartCoroutine(GrappleToSword());
    }

    /// <summary>
    /// This method catches and acts on the
    /// Grapple to enemy input.
    /// When called, it marks, that the player wants to
    /// grapple towards a hit enemy.
    /// </summary>
    private void OnGrappleToEnemy()
    {
        if (_sword.SwordState == SwordState.flying && !_grappling && _sword.EnemySwordIsStuckIn)
            _wantsToAttack = true;
    }

    /// <summary>
    /// This method catches the aim input.
    /// </summary>
    private void OnAim(InputValue input)
    {
        Vector2 aim = input.Get<Vector2>();
        SwordDirection = new Vector3(aim.x, 0, aim.y);
        AimSword(SwordDirection);
    }

    /// <summary>
    /// This method rotates the sword to preview the
    /// general direction the sword would fly if thrown.
    /// </summary>
    /// <param name="aimDirection">The direction, to aim into.</param>
    public void AimSword(Vector3 aimDirection)
    {
        if (aimDirection.sqrMagnitude > 0.1f)
            _swordHolder.transform.forward = -aimDirection;
    }
}
