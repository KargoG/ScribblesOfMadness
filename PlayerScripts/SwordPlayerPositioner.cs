using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This component handles properly positioning the
/// player on top of the sword. This usually only
/// happens at the end of a grapple
/// </summary>
public class SwordPlayerPositioner : MonoBehaviour
{
    private SwordMovement _sword = null;
    private PlayerMovement _player = null;

    void Start()
    {
        _sword = GetComponentInParent<SwordMovement>();
        _player = FindObjectOfType<PlayerMovement>();
    }

    /// <summary>
    /// This method checks whether the player is close enough
    /// to the sword to climb it.
    /// </summary>
    /// <returns>Is the player close enough to climb the sword?</returns>
    public bool CanPlayerClimb()
    {
        // We get all colliders close to the sword
        BoxCollider col = GetComponent<BoxCollider>();
        Collider[] colliders = Physics.OverlapBox(transform.position + col.center, (col.size / 2) * 1.1f);

        // If one belongs to the player we return true, otherwise we return false.
        foreach (Collider inBox in colliders)
        {
            if (inBox.gameObject.tag == "Player")
                return true;
        }
        return false;
    }


    private void OnTriggerEnter(Collider other)
    {
        if(_sword.SwordState == SwordState.stuck)
        {
            if(other.tag == "Player")
            {
                Vector2 playerToSwordDirection = new Vector2(transform.position.x, transform.position.z) - new Vector2(other.transform.position.x, other.transform.position.z);
                playerToSwordDirection.Normalize();
                // if the player is not currently frozen
                if (_player.MovementTimeScale > 0)
                {
                    // if the player is moving towards the sword,
                    // we start moving the player on top of it
                    if (Vector2.Dot(playerToSwordDirection, _player.MovementInput) > 0) PositionPlayer();
                }
            }
        }
    }

    
    public void PositionPlayer()
    {
        StartCoroutine(MovePlayerToSword(0.1f));
    }
    
    /// <summary>
    /// This IEnumerator moves the player smoothly
    /// onto the sword.
    /// </summary>
    /// <param name="time">the duration of the move</param>
    IEnumerator MovePlayerToSword(float time)
    {
        float timer = 0f;
        Vector3 startPos = _player.transform.position;

        while(timer < time)
        {
            // we over time lerp the player on top of the sword
            timer += Time.deltaTime;
            _player.transform.position = Vector3.Lerp(startPos, transform.position, timer / time);
            yield return new WaitForEndOfFrame();
        }
    }
}
