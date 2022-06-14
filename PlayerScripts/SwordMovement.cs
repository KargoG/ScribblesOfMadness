using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SwordState
{
    holding,
    flying,
    stuck,
    returning
}

/// <summary>
/// This component handles the movement and current state
/// of the sword.
/// It also handles what happens when the sword hits something.
/// </summary>
public class SwordMovement : MonoBehaviour
{
    [SerializeField] private float _movementSpeed = 15;
    public float MovementSpeed
    {
        get { return _movementSpeed; }
    }

    public Vector3 MovementDirection { get; set; }
    public SwordState SwordState { get; set; } = SwordState.holding;

    private GameObject _objectSwordIsStuckIn = null;
    public GameObject ObjectSwordIsStuckIn { get { return _objectSwordIsStuckIn; }
        set
        {
            // if the sword is stuck in something, it becomes a solid collider
            _swordCol.isTrigger = !value;
            // it also completely freezes
            _rb.constraints = value ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            // and the player positioner gets activated to do its job
            PlayerPositioner.gameObject.SetActive(value);

            _objectSwordIsStuckIn = value;
        }
    }
    public DumbEnemy EnemySwordIsStuckIn { get; set; }


    public SwordPlayerPositioner PlayerPositioner { get; private set; }  = null;
    private Rigidbody _rb = null;
    private Collider _swordCol = null;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _swordCol = GetComponent<Collider>();
        PlayerPositioner = GetComponentInChildren<SwordPlayerPositioner>();
    }

    private void OnEnable()
    {
        // when the script is enabled, we adjust the forwards vector (which decides which way the sword is moving)
        Vector3 newForward = MovementDirection;
        newForward = Quaternion.AngleAxis(45, Vector3.up) * newForward;
        transform.forward = newForward;
    }

    private void FixedUpdate()
    {
        // the sword just keeps moving forwards
        _rb.MovePosition(transform.position + transform.forward * _movementSpeed * Time.deltaTime);
    }

    
    private void OnTriggerEnter(Collider other)
    {
        switch(SwordState)
        {
            case SwordState.flying:
                {
                    if (other.tag == "Player")
                        return;
                    if (other.GetComponent<AIBehaviorBase>())
                        return;

                    // if we hit something we can grapple towards, we check if something is right above the sword
                    if (Physics.SphereCast(PlayerPositioner.transform.position, 0.3f, Vector3.up, out RaycastHit _hit, 2))
                    {
                        // if something is there, we adjust the sword position to create enough space for the player
                        _rb.position = transform.position - Vector3.up * _hit.distance;
                    }

                    // we update the sword state
                    ObjectSwordIsStuckIn = other.gameObject;
                    SwordState = SwordState.stuck;
                    break;
                }
            case SwordState.returning:
                {
                    if (other.tag == "Player")
                    {
                        SwordState = SwordState.holding;
                    }
                    break;
                }
        }
    }
}
