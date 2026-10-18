using System.Collections.Generic;
using UnityEngine;

public class SeparationSteering : MonoBehaviour
{
    [Header("Parámetros de Steering")]
    public float maxSpeed = 5f;
    public float maxForce = 10f;

    [Header("Separación")]
    public float separationRadius = 2.5f;
    public float separationWeight = 3f;

    private Vector3 velocity;

    /// <summary>
    /// Mueve al enemigo hacia una posición objetivo (nodo A*) aplicando la fuerza de separación continua.
    /// </summary>
    public void SteerTowardsWithSeparation(Vector3 targetPosition, List<Transform> nearbyAllies)
    {
        // 1. FUERZA DE NAVEGACIÓN (Seek hacia el objetivo/nodo)
        Vector3 desiredVelocity = (targetPosition - transform.position);
        desiredVelocity.y = 0; // Mantener movimiento en plano horizontal

        if (desiredVelocity.sqrMagnitude > 0.01f)
        {
            desiredVelocity = desiredVelocity.normalized * maxSpeed;
        }

        Vector3 seekForce = desiredVelocity - velocity;

        // 2. FUERZA DE SEPARACIÓN (Repulsión respecto a aliados)
        Vector3 separationForce = ComputeSeparationForce(nearbyAllies);

        // 3. SUMA Y APLICACIÓN DE FUERZAS
        Vector3 totalSteering = seekForce + (separationForce * separationWeight);
        totalSteering = Vector3.ClampMagnitude(totalSteering, maxForce);

        // Actualizar velocidad y posición
        velocity = Vector3.ClampMagnitude(velocity + totalSteering * Time.deltaTime, maxSpeed);
        velocity.y = 0;

        transform.position += velocity * Time.deltaTime;

        // Rotación suave hacia la dirección de movimiento real
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

        foreach (var ally in allies)
        {
            if (ally == null || ally == transform) continue;

            Vector3 pushAway = transform.position - ally.position;
            pushAway.y = 0;
            float distance = pushAway.magnitude;

            // Si está dentro del radio de separación, aplicar fuerza inversamente proporcional a la distancia
            if (distance > 0.001f && distance < separationRadius)
            {
                force += (pushAway.normalized / distance);
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
        // Visualizar el radio de separación en el editor
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
}