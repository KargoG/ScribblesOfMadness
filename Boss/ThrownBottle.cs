using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This component is meant for the bottles, thrown by the boss "The alcoholic".
/// The component moves the bottle over time to the targeted position.
/// </summary>
public class ThrownBottle : EnvironmentalHazard
{
    public Vector3 MovementDiretion { get; set; }

    public float FlyingSpeed { get; set; }

    void Update()
    {
        transform.Translate(MovementDiretion * FlyingSpeed * Time.deltaTime, Space.World);
    }

    protected override void OnTriggerEnter(Collider other)
    {
        // The boss can't hit itself
        if (other.CompareTag("Boss"))
            return;

        base.OnTriggerEnter(other);

        // If we are hitting something thats neither the player, nor their weapon the bottle breaks
        if (!other.CompareTag("Player") && !other.CompareTag("Weapon"))
        {
            Destroy(gameObject);
        }
    }
}
