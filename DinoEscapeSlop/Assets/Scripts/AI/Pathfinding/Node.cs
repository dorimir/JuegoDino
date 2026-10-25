using System.Collections.Generic;
using UnityEngine;

public class Node : MonoBehaviour
{
    [Header("Conexiones")]
    public List<Node> neighbors = new List<Node>();

    [HideInInspector] public int gCost;
    [HideInInspector] public int hCost;
    [HideInInspector] public int fCost => gCost + hCost;
    [HideInInspector] public Node parent;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.3f);

        if (neighbors != null)
        {
            Gizmos.color = Color.green;
            foreach (Node neighbor in neighbors)
            {
                if (neighbor != null)
                {
                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
                }
            }
        }
    }
}
