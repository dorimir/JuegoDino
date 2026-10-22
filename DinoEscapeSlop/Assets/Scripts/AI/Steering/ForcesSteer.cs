using UnityEngine;

public class ForcesSteer : MonoBehaviour
{
    public GameObject target;
    [SerializeField] private float smoothness = 0.5f;
    [SerializeField] private float repel = 0.5f;
    [SerializeField] private float radius = 10f;
    [SerializeField] private float maxspeed = 2.5f;

    private Vector3 force;
    private Vector3 desired;

    void FixedUpdate()
    {
        if (target == null) return;

        RotateTowardsTarget();

        force = Vector3.Lerp(transform.position, target.transform.position, smoothness * Time.deltaTime);
        force = new Vector3(0, 0, Mathf.Abs(force.z));

        desired = target.transform.position - transform.position;
        float distance = Mathf.Sqrt(desired.x * desired.x + desired.z * desired.z);

        ConstantForce cf = GetComponent<ConstantForce>();
        if (cf == null) return;

        if (distance < radius)
        {
            Vector3 counterforce = new Vector3(0, 0, force.z * (1f + (1f - distance / radius)) * repel);
            cf.relativeForce = force - counterforce;
        }
        else
        {
            cf.relativeForce = force;
        }
    }

    private void RotateTowardsTarget()
    {
        desired = target.transform.position - transform.position;
        if (desired.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(desired);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * maxspeed);
    }
}
