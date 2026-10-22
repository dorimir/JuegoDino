using UnityEngine;

public class WanderSeek : MonoBehaviour
{
    private Vector3 target;
    private bool arrival;

    [SerializeField] private float smoothness = 0.5f;
    [SerializeField] private float repel = 0.5f;
    [SerializeField] private float radius = 10f;
    [SerializeField] private float maxWanderStrength = 1f;
    [SerializeField] private float step = 45f;
    [SerializeField] private float maxspeed = 2.5f;

    private Vector3 wander = Vector3.zero;

    void FixedUpdate()
    {
        WanderRotation(target);
        ForceForward(target);
    }

    public void setTarget(Vector3 newTarget)
    {
        target = newTarget;
        wander = target - transform.position;
    }

    public void setArrival(bool newArrival) => arrival = newArrival;

    private void WanderRotation(Vector3 targetPosition)
    {
        if (targetPosition == Vector3.zero) return;
        Vector3 desired = targetPosition - transform.position;
        if (desired.sqrMagnitude < 0.001f) return;

        if (!arrival)
        {
            float distance = Mathf.Sqrt(desired.x * desired.x + desired.z * desired.z);
            float wanderRatio = (1f - (distance / 100f)) * distance;
            float wanderRatioAngle = Mathf.Abs(Mathf.Atan(wanderRatio / distance));
            float maxWanderStrengthAngle = wanderRatioAngle * maxWanderStrength;

            float newstep = (Random.value < 0.5f) ? step : -step;

            float newx = Mathf.Cos(newstep) * wander.x - Mathf.Sin(newstep) * wander.z;
            float newz = Mathf.Sin(newstep) * wander.x + Mathf.Cos(newstep) * wander.z;
            Vector3 newWander = new Vector3(newx, wander.y, newz);
            Vector3 desiredWander = new Vector3(
                desired.x != 0 ? newWander.x / desired.x : 0,
                desired.y != 0 ? newWander.y / desired.y : 0,
                desired.z != 0 ? newWander.z / desired.z : 0
            );

            float wAngle = Mathf.Abs(Mathf.Acos(Mathf.Clamp01(Mathf.Sqrt(desiredWander.x * desiredWander.x + desiredWander.z * desiredWander.z))));

            if (wAngle > maxWanderStrengthAngle)
            {
                float signAngle = newstep > 0 ? maxWanderStrengthAngle : -maxWanderStrengthAngle;
                newx = Mathf.Cos(signAngle) * desired.x - Mathf.Sin(signAngle) * desired.z;
                newz = Mathf.Sin(signAngle) * desired.x + Mathf.Cos(signAngle) * desired.z;
                newWander = new Vector3(newx, wander.y, newz);
            }

            wander = newWander;
            if (wander.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wander);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * maxspeed);
            }
        }
        else
        {
            Quaternion targetRotation = Quaternion.LookRotation(desired);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * maxspeed);
        }
    }

    private void ForceForward(Vector3 targetPosition)
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
