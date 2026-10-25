using UnityEngine;

public class ForcesSeek : MonoBehaviour
{
    private Vector3 target;
    private bool arrival;

    [SerializeField] private float smoothness = 0.5f;
    [SerializeField] private float repel = 0.5f;
    [SerializeField] private float radius = 10f;
    [SerializeField] private float maxSpeed = 2.5f;

    private void FixedUpdate()
    {
        RotateTowardsTarget(target);
        ApplyForwardForce(target);
    }

    public void SetTarget(Vector3 newTarget) => target = newTarget;
    public void setTarget(Vector3 newTarget) => SetTarget(newTarget);

    public void SetArrival(bool newArrival) => arrival = newArrival;
    public void setArrival(bool newArrival) => SetArrival(newArrival);

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        if (targetPosition == Vector3.zero) return;
        Vector3 desired = targetPosition - transform.position;
        if (desired.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(desired);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * maxSpeed);
    }

    private void ApplyForwardForce(Vector3 targetPosition)
    {
        if (targetPosition == Vector3.zero) return;
        if (!TryGetComponent<ConstantForce>(out var cf)) return;

        Vector3 force = Vector3.Lerp(transform.position, targetPosition, smoothness * Time.deltaTime);
        force = new Vector3(0, 0, Mathf.Abs(force.z));

        if (arrival)
        {
            Vector3 desired = targetPosition - transform.position;
            float distance = Mathf.Sqrt(desired.x * desired.x + desired.z * desired.z);

            if (distance < radius)
            {
                float ratio = distance / radius;
                Vector3 counterforce = new Vector3(0, 0, force.z * (1f + (1f - ratio)) * repel);
                cf.relativeForce = (force - counterforce) * ratio;
                return;
            }
        }

        cf.relativeForce = force;
    }
}

/// <summary>
/// Alias compatible con el nombre del archivo ForceSeek.cs.
/// </summary>
public class ForceSeek : ForcesSeek
{
}