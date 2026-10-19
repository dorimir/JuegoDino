using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private AStarPathfinding pathfinding;
    private List<Vector3> currentPath;
    private int targetIndex;

    public float speed = 5f;

    [Header("Nodos del Enemigo")]
    [Tooltip("Asigna aqu� en el Inspector �nicamente los nodos entre los que quieres que se mueva este enemigo.")]
    public List<Node> patrolNodes = new List<Node>();

    // Indica a la m�quina de estados si el enemigo complet� su trayecto
    public bool HasReachedDestination { get; private set; }

    void Start()
    {
        pathfinding = FindObjectOfType<AStarPathfinding>();
        GoToRandomNode();
    }

    void Update()
    {
        FollowPath();
    }

    public void MoveTo(Vector3 targetPosition)
    {
        if (pathfinding == null) return;

        // Cancelamos invocaciones previas para evitar cambios de ruta automáticos involuntarios
        CancelInvoke();

        currentPath = pathfinding.FindPath(transform.position, targetPosition);

        //estamos en un camino, no hemos llegado al destino final
        if (currentPath != null && currentPath.Count > 0)
        {
            targetIndex = 0;
            HasReachedDestination = false;

        }
        else
        {
            HasReachedDestination = true;
        }
    }

    void FollowPath()
    {
        if (currentPath == null || currentPath.Count == 0) return;

        Vector3 currentWaypoint = currentPath[targetIndex];

        //Si el nodo actual es el ultimo
        if (targetIndex+1 >= currentPath.Count)
        {
            this.GetComponent<WanderSeek2>().setArrival(true); 
        }
        else 
        {
            this.GetComponent<WanderSeek2>().setArrival(false); 
        }

        // Mover hacia el nodo waypoint actual
        this.GetComponent<WanderSeek2>().setTarget(currentWaypoint); 
        //transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, speed * Time.deltaTime);

        // Comprobaci�n de llegada al waypoint actual
        if (Vector3.Distance(transform.position, currentWaypoint) < 0.2f)
        {
            targetIndex++;

            // �Llegamos al final del camino completo?
            if (targetIndex >= currentPath.Count)
            {
                currentPath = null;
                HasReachedDestination = true; // Notificamos a la FSM que hemos llegado al destino final
            }
        }
    }

    /// <summary>
    /// Selecciona un nodo aleatorio �NICAMENTE de la lista de nodos asignada en el Inspector.
    /// </summary>
    /// ESTO DEBERIA ESTAR CONDICIONADO POR EL ESTADO WANDER
    public void GoToRandomNode()
    {
        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            Node randomNode = patrolNodes[Random.Range(0, patrolNodes.Count)];

            if (randomNode != null)
            {
                MoveTo(randomNode.transform.position);
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyMovement] El objeto {gameObject.name} no tiene nodos asignados en su lista 'Patrol Nodes'.");
        }
    }
}