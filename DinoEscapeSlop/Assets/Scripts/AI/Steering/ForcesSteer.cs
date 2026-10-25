using UnityEngine;

public class ForcesSteer : MonoBehaviour
{
    public GameObject target;
    [SerializeField] private float smoothness = 0.5f;
    [SerializeField] private float repel = 0.5f;
    [SerializeField] private float radius = 10f;
    [SerializeField] private float maxSpeed = 2.5f;

    private ConstantForce constantForceComponent;

    private void Awake()
    {
        constantForceComponent = GetComponent<ConstantForce>();
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.transform.position - transform.position;
        RotateTowardsTarget(desired);

        if (constantForceComponent == null) return;

        Vector3 force = Vector3.Lerp(transform.position, target.transform.position, smoothness * Time.deltaTime);
        force = new Vector3(0, 0, Mathf.Abs(force.z));

        float distance = Mathf.Sqrt(desired.x * desired.x + desired.z * desired.z);

        if (distance < radius)
        {
            Vector3 counterforce = new Vector3(0, 0, force.z * (1f + (1f - distance / radius)) * repel);
            constantForceComponent.relativeForce = force - counterforce;
        }
        else
        {
            constantForceComponent.relativeForce = force;
        }
    }

    private void RotateTowardsTarget(Vector3 desired)
    {
        if (desired.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(desired);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * maxSpeed);
    }
}
