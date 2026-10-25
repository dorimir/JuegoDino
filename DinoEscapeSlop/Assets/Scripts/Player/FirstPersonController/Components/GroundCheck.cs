using UnityEngine;

[ExecuteInEditMode]
public class GroundCheck : MonoBehaviour
{
    [Tooltip("Distancia máxima respecto al suelo.")]
    public float distanceThreshold = 0.15f;

    [Tooltip("Indica si el personaje se encuentra sobre el suelo.")]
    public bool isGrounded = true;

    public event System.Action Grounded;

    private const float OriginOffset = 0.001f;
    private Vector3 RaycastOrigin => transform.position + Vector3.up * OriginOffset;
    private float RaycastDistance => distanceThreshold + OriginOffset;

    private void LateUpdate()
    {
        bool isGroundedNow = Physics.Raycast(RaycastOrigin, Vector3.down, distanceThreshold * 2f);

        if (isGroundedNow && !isGrounded)
        {
            Grounded?.Invoke();
        }

        isGrounded = isGroundedNow;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.white : Color.red;
        Gizmos.DrawLine(RaycastOrigin, RaycastOrigin + Vector3.down * RaycastDistance);
    }
}
