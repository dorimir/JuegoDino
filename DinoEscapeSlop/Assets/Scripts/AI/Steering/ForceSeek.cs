using UnityEngine;

public class ForcesSeek : MonoBehaviour
{
    private Vector3 target;
    private bool Arrival = false;
    private float smooothness = 0.5f;
    private float repel = 0.5f;
    private float radius = 10f;
    private Vector3 counterforce = new Vector3(0,0,0);
    private Vector3 force = new Vector3(0,0,0);
    private Vector3 desired = new Vector3(0,0,0);
    private float maxspeed = 2.5F; 

    void Start(){ }

    void FixedUpdate()
    {
        rotation(target);
        forceForward(target);
    }

    public void setTarget(Vector3 newTarget)
    {
        target = newTarget;
    }

    public void setArrival(bool newArrival)
    {
        Arrival = newArrival;
    }

    private void rotation(Vector3 targetPosition)
    {
        if (targetPosition == Vector3.zero) return;
        desired = targetPosition - this.transform.position;
        if (desired.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(desired);
        this.transform.rotation = Quaternion.Slerp(this.transform.rotation, targetRotation, Time.deltaTime * maxspeed);
    }

    private void forceForward(Vector3 targetPosition)
    {
        if (targetPosition == Vector3.zero) return;
        ConstantForce cf = GetComponent<ConstantForce>();
        if (cf == null) return;

        force = Vector3.Lerp(this.transform.position, targetPosition, smooothness * Time.deltaTime);

        if (force.z < 0)
        {
            force = new Vector3(0, 0, -force.z);
        }
        else
        {
            force = new Vector3(0, 0, force.z);
        }

        if (Arrival)
        {
            desired = targetPosition - this.transform.position;
            float distance = Mathf.Sqrt(desired.x * desired.x + desired.z * desired.z);
            if (distance < 0) distance = -distance;
            
            if (distance < radius)
            {
                float ratio = distance / radius;
                counterforce = new Vector3(0, 0, force.z * (1f + (1 - ratio)) * repel); 
                force = new Vector3(0, 0, force.z);
                cf.relativeForce = (force - counterforce) * ratio; 
            }
            else cf.relativeForce = force; 
        }
        else cf.relativeForce = force; 
    }
}
