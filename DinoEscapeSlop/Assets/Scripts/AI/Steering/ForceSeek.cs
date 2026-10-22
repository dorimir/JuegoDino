using UnityEngine;

public class ForcesSeek : MonoBehaviour
{
    private Vector3 target;
    private bool arrival;
    [SerializeField] private float smoothness = 0.5f;
    [SerializeField] private float repel = 0.5f;
    [SerializeField] private float radius = 10f;
    [SerializeField] private float maxspeed = 2.5f;

    void FixedUpdate()
    {
        RotateTowardsTarget(target);
        ApplyForwardForce(target);
    }

    public void setTarget(Vector3 newTarget) => target = newTarget;

    public void setArrival(bool newArrival) => arrival = newArrival;

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        if (targetPosition == Vector3.zero) return;
        Vector3 desired = targetPosition - transform.position;
        if (desired.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(desired);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * maxspeed);
    }

    private void ApplyForwardForce(Vector3 targetPosition)
    {
        if (targetPosition == Vector3.zero) return;
        ConstantForce cf = GetComponent<ConstantForce>();
        if (cf == null) return;

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
