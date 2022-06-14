using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This component manages the behaviour for
/// the boss "The alcoholic".
/// </summary>
public class TheAlcoholicBehaviour : AIBehaviorBase
{
    [SerializeField] private int _amountOfPhases = 3;
    [SerializeField] private List<GameObject> _walls = new List<GameObject>();
    [SerializeField] private GameObject _attackPrefab = null;
    [SerializeField] private GameObject _attackPreviewPrefab = null;
    [SerializeField] private GameObject _fullGroundAttack = null;
    [SerializeField] private GameObject _fullGroundAttackPreview = null;
    [SerializeField] private GameObject _thrownBottlePrefab = null;
    [SerializeField] private Transform _thrownBottleSpawnPoint = null;

    private int _currentPhase = 0;

    private Health _health = null;
    private PlayerMovement _player = null;

    void Start()
    {
        _health = GetComponent<Health>();
        _player = FindObjectOfType<PlayerMovement>();

        _health.OnChange.AddListener(EndPhase);
        _health.OnChange.AddListener(()=> { VibrationHandler.StrongVibration(0.2f); });

    }

    /// <summary>
    /// This method ends the current phase of the fight and starts the next.
    /// The method gets called every time the boss gets hit, since the boss only has 3 health in the first place.
    /// </summary>
    void EndPhase()
    {
        // when the phase switches we deactivate its area attack and walls the player uses to fight it
        _fullGroundAttack.SetActive(false);
        _walls[0].SetActive(false);
        _walls[1].SetActive(false);
        _walls[2].SetActive(false);
        _fullGroundAttackPreview.SetActive(false);

        // we increase and start the phase
        _currentPhase++;
        switch (_currentPhase)
        {
            default:
            case 0:
                StartFirstPhase();
                break;
            case 1:
                StartSecondPhase();
                break;
            case 2:
                StartThirdPhase();
                break;
        }
    }

    /// <summary>
    /// This method redirects to the real update method
    /// based on the current boss phase.
    /// </summary>
    void UpdateBehaviour()
    {
        switch(_currentPhase)
        {
            default:
            case 0:
                UpdateFirstPhase();
                break;
            case 1:
                UpdateSecondPhase();
                break;
            case 2:
                UpdateThirdPhase();
                break;
        }
    }

    [Space]
    [Header("First Phase Settings")]
    [SerializeField] private float _climbTimeInFirstPhase = 10f;
    [SerializeField] private float _timeOnGroundInFirstPhase = 5f;
    [SerializeField] private float _timeBetweenAttacksInFirstPhase = 2f;
    [SerializeField] private float _timeOfAttackPreviewInFirstPhase = 2f;
    [SerializeField] private float _timeAttackStaysInFirstPhase = 1f;
    float _timeOfLastPhaseChange = 0;
    float _timeOfLastAttack = 0;
    // During the first phase of the fight the boss attacks the player with pillars
    // comming out of the ground every couple of seconds.
    // After some time the boss starts attacking the player with an area attack,
    // that the player has to dodge by climbing a wall next to the boss.
    // The player beats this phase by climbing the wall and attacking the string
    // the boss is hanging on

    // the boss has 2 sub phase in each phase. 0 where the player dodges on the ground
    // and 1 where the player has to climb to a point where the boss can be hurt
    private int _currentSubPhase = 0; // 0 = ground; 1 = climb

    /// <summary>
    /// This method initializes all necessarry values for the first boss phase.
    /// </summary>
    private void StartFirstPhase()
    {
        _timeOfLastPhaseChange = Time.time;
        _timeOfLastAttack = Time.time - _timeBetweenAttacksInFirstPhase / 2;
        _currentSubPhase = 0;
    }

    /// <summary>
    /// This method handles the frame to frame updates of the boss
    /// during the first phase of the boss fight.
    /// </summary>
    private void UpdateFirstPhase()
    {
        // if we are in the first subface, where the player dodges attacks on the ground,
        // we check if enough time has passed to start switching phases
        if ((_currentSubPhase == 0) && (Time.time - _timeOfLastPhaseChange) + _timeOfAttackPreviewInFirstPhase > _timeOnGroundInFirstPhase)
        {
            // if the preview for the area attack on the ground is not active we activate it
            if (!_fullGroundAttackPreview.activeSelf)
            {
                _fullGroundAttackPreview.SetActive(true);

                // we also activate the walls the player can use to save themselves from said attack
                _walls[0].SetActive(true);
            }
        }

        // if the boss has been in a sub phase for long enough it changes its subphase
        if (Time.time - _timeOfLastPhaseChange >= ((_currentSubPhase == 0) ? _timeOnGroundInFirstPhase : _climbTimeInFirstPhase))
        {
            _timeOfLastPhaseChange = Time.time;

            // we change the sub phase
            _currentSubPhase = (_currentSubPhase + 1) % 2;

            // dependent on which subphase we are switching turning the walls and area attack of the boss on or off
            _fullGroundAttack.SetActive(_currentSubPhase == 1);
            _walls[0].SetActive(_currentSubPhase == 1);
            _fullGroundAttackPreview.SetActive(false);
            return;
        }

        
        
        if(_currentSubPhase == 0) // if we are in the first subphase in which the player dodges attacks
        {
            if (Time.time - _timeOfLastAttack >= _timeBetweenAttacksInFirstPhase) // we check if the boss should attack
            {
                StartCoroutine(StartAttack(_player.transform.position, _timeOfAttackPreviewInFirstPhase, _timeAttackStaysInFirstPhase));
                _timeOfLastAttack = Time.time;
            }
        }
    }

    [Space]
    [Header("Second Phase Settings")]
    [SerializeField] private float _climbTimeInSecondPhase = 10f;
    [SerializeField] private float _timeOnGroundInSecondPhase = 5f;
    [SerializeField] private float _timeBetweenAttacksInSecondPhase = 2f;
    [SerializeField] private float _timeOfAttackPreviewInSecondPhase = 2f;
    [SerializeField] private float _timeAttackStaysInSecondPhase = 1f;
    [SerializeField] private float _minTimeBetweenBottleThrowsInSecondPhase = 1f;
    [SerializeField] private float _maxTimeBetweenBottleThrowsInSecondPhase = 2f;
    [SerializeField] private float _thrownBottleSpeedInSecondPhase = 5f;
    private float _timeOfLastBottleThrow = 0;
    private float _timeBetweenBottleThrows = 0;
    // The second phase of the fight works similar to the first.
    // The main difference is that the boss also starts throwing
    // bottles at the player.
    // The player beats this phase by climbing the wall in the second
    // sub phase and attacking the string the boss is hanging on

    /// <summary>
    /// This method initializes all necessarry values for the second boss phase.
    /// </summary>
    private void StartSecondPhase()
    {
        _timeOfLastBottleThrow = Time.time;
        _timeBetweenBottleThrows = UnityEngine.Random.Range(_minTimeBetweenBottleThrowsInSecondPhase, _maxTimeBetweenBottleThrowsInSecondPhase);
        _timeOfLastPhaseChange = Time.time;
        _timeOfLastAttack = Time.time - _timeBetweenAttacksInSecondPhase / 2;
        _currentSubPhase = 0;
    }

    /// <summary>
    /// This method handles the frame to frame updates of the boss
    /// during the second phase of the boss fight.
    /// </summary>
    private void UpdateSecondPhase()
    {
        // if we are in the first subface, where the player dodges attacks on the ground,
        // we check if enough time has passed to start switching phases
        if ((_currentSubPhase == 0) && (Time.time - _timeOfLastPhaseChange) + _timeOfAttackPreviewInSecondPhase > _timeOnGroundInSecondPhase)
        {
            // if the preview for the area attack on the ground is not active we activate it
            if (!_fullGroundAttackPreview.activeSelf)
            {
                _fullGroundAttackPreview.SetActive(true);
                // we also activate the walls the player can use to save themselves from said attack
                _walls[1].SetActive(true);
            }
        }

        // if the boss has been in a sub phase for long enough it changes its subphase
        if (Time.time - _timeOfLastPhaseChange >= ((_currentSubPhase == 0) ? _timeOnGroundInSecondPhase : _climbTimeInSecondPhase))
        {
            _timeOfLastPhaseChange = Time.time;

            // we change the sub phase
            _currentSubPhase = (_currentSubPhase + 1) % 2;

            // dependent on which subphase we are switching turning the walls and area attack of the boss on or off
            _fullGroundAttack.SetActive(_currentSubPhase == 1);
            _walls[1].SetActive(_currentSubPhase == 1);
            _fullGroundAttackPreview.SetActive(false);
            return;
        }

        if (_currentSubPhase == 0) // if we are in the first subphase in which the player dodges attacks
        {
            if (Time.time - _timeOfLastAttack >= _timeBetweenAttacksInSecondPhase) // we check if the boss should attack
            {
                StartCoroutine(StartAttack(_player.transform.position, _timeOfAttackPreviewInSecondPhase, _timeAttackStaysInSecondPhase));
                _timeOfLastAttack = Time.time;
            }
        }
        else if(_currentSubPhase == 1) // if we are in the first subphase in which the player climbs to the bosses weakpoint
        {
            if (Time.time - _timeOfLastBottleThrow >= _timeBetweenBottleThrows) // we check if the boss should throw a bottle at the player
            {
                ThrowBottle(_player.transform.position, _thrownBottleSpeedInSecondPhase);
                _timeOfLastBottleThrow = Time.time;
                _timeBetweenBottleThrows = UnityEngine.Random.Range(_minTimeBetweenBottleThrowsInSecondPhase, _maxTimeBetweenBottleThrowsInSecondPhase);
            }
        }
    }

    [Space]
    [Header("Third Phase Settings")]
    [SerializeField] private float _climbTimeInThirdPhase = 10f;
    [SerializeField] private float _timeOnGroundInThirdPhase = 5f;
    [SerializeField] private float _timeBetweenAttacksInThirdPhase = 2f;
    [SerializeField] private float _timeOfAttackPreviewInThirdPhase = 2f;
    [SerializeField] private float _timeAttackStaysInThirdPhase = 1f;
    [SerializeField] private float _minTimeBetweenBottleThrowsInThirdPhase = 0.75f;
    [SerializeField] private float _maxTimeBetweenBottleThrowsInThirdPhase = 1.25f;
    [SerializeField] private float _thrownBottleSpeedInThirdPhase = 5f;


    // The third phase of the fight is the same as the second.
    // The only difference is that the boss attacks a lot faster.
    // The player beats this phase by climbing the wall in the second
    // sub phase and attacking the string the boss is hanging on

    /// <summary>
    /// This method initializes all necessarry values for the third boss phase.
    /// </summary>
    private void StartThirdPhase()
    {
        _timeOfLastBottleThrow = Time.time;
        _timeBetweenBottleThrows = UnityEngine.Random.Range(_minTimeBetweenBottleThrowsInThirdPhase, _maxTimeBetweenBottleThrowsInThirdPhase);
        _timeOfLastPhaseChange = Time.time;
        _timeOfLastAttack = Time.time - _timeBetweenAttacksInThirdPhase / 2;
        _currentSubPhase = 0;
    }

    /// <summary>
    /// This method handles the frame to frame updates of the boss
    /// during the third phase of the boss fight.
    /// </summary>
    private void UpdateThirdPhase()
    {
        // if we are in the first subface, where the player dodges attacks on the ground,
        // we check if enough time has passed to start switching phases
        if ((_currentSubPhase == 0) && (Time.time - _timeOfLastPhaseChange) + _timeOfAttackPreviewInThirdPhase > _timeOnGroundInThirdPhase)
        {
            // if the preview for the area attack on the ground is not active we activate it
            if (!_fullGroundAttackPreview.activeSelf)
            {
                _fullGroundAttackPreview.SetActive(true);
                // we also activate the walls the player can use to save themselves from said attack
                _walls[2].SetActive(true);
            }
        }

        // if the boss has been in a sub phase for long enough it changes its subphase
        if (Time.time - _timeOfLastPhaseChange >= ((_currentSubPhase == 0) ? _timeOnGroundInThirdPhase : _climbTimeInThirdPhase))
        {
            _timeOfLastPhaseChange = Time.time;

            // we change the sub phase
            _currentSubPhase = (_currentSubPhase + 1) % 2;

            // dependent on which subphase we are switching turning the walls and area attack of the boss on or off
            _fullGroundAttack.SetActive(_currentSubPhase == 1);
            _walls[2].SetActive(_currentSubPhase == 1);
            _fullGroundAttackPreview.SetActive(false);
            return;
        }

        if (_currentSubPhase == 0) // if we are in the first subphase in which the player dodges attacks
        {
            if (Time.time - _timeOfLastAttack >= _timeBetweenAttacksInThirdPhase) // we check if the boss should attack
            {
                StartCoroutine(StartAttack(_player.transform.position, _timeOfAttackPreviewInThirdPhase, _timeAttackStaysInThirdPhase));
                _timeOfLastAttack = Time.time;
            }
        }
        else if (_currentSubPhase == 1)// if we are in the first subphase in which the player climbs to the bosses weakpoint
        {
            if (Time.time - _timeOfLastBottleThrow >= _timeBetweenBottleThrows) // we check if the boss should throw a bottle at the player
            {
                ThrowBottle(_player.transform.position, _thrownBottleSpeedInThirdPhase);
                _timeOfLastBottleThrow = Time.time;
                _timeBetweenBottleThrows = UnityEngine.Random.Range(_minTimeBetweenBottleThrowsInThirdPhase, _maxTimeBetweenBottleThrowsInThirdPhase);
            }
        }
    }

    /// <summary>
    /// This IEnumerator creates a piller, that rises from
    /// the ground, attacking the player.
    /// </summary>
    /// <param name="pos">The position to attack</param>
    /// <param name="previewTime">How long the attack is previewed before its executed</param>
    /// <param name="attackStayTime">How long the pillar stays active.</param>
    /// <returns></returns>
    private IEnumerator StartAttack(Vector3 pos, float previewTime, float attackStayTime)
    {
        // First we show the attack preview for the defined time
        pos.y = transform.parent.position.y;
        GameObject preview = Instantiate(_attackPreviewPrefab, pos, Quaternion.identity);
        yield return new WaitForSeconds(previewTime);

        // Then we destroy the preview and show the actual attack for the defined time
        Destroy(preview);
        GameObject attack = Instantiate(_attackPrefab, pos, Quaternion.identity);
        yield return new WaitForSeconds(attackStayTime);
        Destroy(attack);
    }

    /// <summary>
    /// This IEnumerator creates a bottle, that gets thrown at
    /// the player as an attack.
    /// </summary>
    /// <param name="pos">The position to throw the bottle towards</param>
    /// <param name="bottleSpeed">The flying speed of the bottle</param>
    private void ThrowBottle(Vector3 pos, float bottleSpeed)
    {
        // We spawn the bottle and set its speed
        GameObject attack = Instantiate(_thrownBottlePrefab, _thrownBottleSpawnPoint.position, Quaternion.identity);
        ThrownBottle bottle = attack.GetComponent<ThrownBottle>();
        bottle.FlyingSpeed = bottleSpeed;

        // We set its direction, since a thrown bottle should not be homing in on its target
        bottle.MovementDiretion = (pos - _thrownBottleSpawnPoint.position).normalized;
        attack.transform.up = Vector3.Cross(bottle.MovementDiretion, transform.right);
    }

    public void StartFight()
    {
        StartFirstPhase();
        behaviors.AddListener(UpdateBehaviour);
    }
}
