using System.Collections.Generic;
using UnityEngine;

public class SeparationSteering : MonoBehaviour
{
    [Header("ParÃ¡metros de Steering")]
    public float maxSpeed = 5f;
    public float maxForce = 10f;

    [Header("SeparaciÃ³n")]
    public float separationRadius = 2.5f;
    public float separationWeight = 3f;

    private Vector3 velocity;

    public void SteerTowardsWithSeparation(Vector3 targetPosition, List<Transform> nearbyAllies)
    {
        Vector3 desiredVelocity = targetPosition - transform.position;
        desiredVelocity.y = 0;

        if (desiredVelocity.sqrMagnitude > 0.01f)
        {
            desiredVelocity = desiredVelocity.normalized * maxSpeed;
        }

        Vector3 seekForce = desiredVelocity - velocity;
        Vector3 separationForce = ComputeSeparationForce(nearbyAllies);
        Vector3 totalSteering = Vector3.ClampMagnitude(seekForce + (separationForce * separationWeight), maxForce);

        velocity = Vector3.ClampMagnitude(velocity + totalSteering * Time.deltaTime, maxSpeed);
        velocity.y = 0;

        transform.position += velocity * Time.deltaTime;

        if (velocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
        }
    }

    private Vector3 ComputeSeparationForce(List<Transform> allies)
    {
        Vector3 force = Vector3.zero;
        int count = 0;

        if (allies == null) return force;

        for (int i = 0; i < allies.Count; i++)
        {
            Transform ally = allies[i];
            if (ally == null || ally == transform) continue;

            Vector3 pushAway = transform.position - ally.position;
            pushAway.y = 0;
            float distance = pushAway.magnitude;

            if (distance > 0.001f && distance < separationRadius)
            {
                force += pushAway.normalized / distance;
                count++;
            }
        }

        if (count > 0)
        {
            force /= count;
            if (force.sqrMagnitude > 0.001f)
            {
                force = force.normalized * maxSpeed - velocity;
            }
        }

        return force;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
}

public class SeparationStereing : SeparationSteering
{
}
