using System.Collections.Generic;
using UnityEngine;

public class AmbushPoint : MonoBehaviour
{
    public static readonly List<AmbushPoint> AllPoints = new List<AmbushPoint>();

    [Header("Configuración del Punto")]

    public EnemyAI assignedEnemy;

    public float gizmoRadius = 0.8f;

    public Color gizmoColor = new Color(1f, 0.55f, 0f, 0.85f);

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;
    public Vector3 Forward => transform.forward;

    public bool IsOccupied => assignedEnemy != null && assignedEnemy.gameObject.activeInHierarchy;

    private void OnEnable()
    {
        if (!AllPoints.Contains(this))
        {
            AllPoints.Add(this);
        }
    }

    private void OnDisable()
    {
        AllPoints.Remove(this);
    }

    public static AmbushPoint GetNearest(Vector3 pos, EnemyAI requester = null)
    {
        AmbushPoint best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < AllPoints.Count; i++)
        {
            AmbushPoint pt = AllPoints[i];
            if (pt == null || !pt.gameObject.activeInHierarchy) continue;

            if (pt.assignedEnemy != null && pt.assignedEnemy != requester && pt.assignedEnemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            float d = Vector3.Distance(pos, pt.Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = pt;
            }
        }

        return best;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawRay(transform.position, transform.forward * 2.2f);

        Vector3 right = transform.position + transform.forward * 1.6f + transform.right * 0.45f;
        Vector3 left = transform.position + transform.forward * 1.6f - transform.right * 0.45f;
        Vector3 tip = transform.position + transform.forward * 2.2f;
        Gizmos.DrawLine(tip, right);
        Gizmos.DrawLine(tip, left);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
        Vector3 coneLeft = Quaternion.Euler(0, -45, 0) * transform.forward * 4.0f;
        Vector3 coneRight = Quaternion.Euler(0, 45, 0) * transform.forward * 4.0f;
        Gizmos.DrawRay(transform.position, coneLeft);
        Gizmos.DrawRay(transform.position, coneRight);
        Gizmos.DrawLine(transform.position + coneLeft, transform.position + coneRight);
    }
}
