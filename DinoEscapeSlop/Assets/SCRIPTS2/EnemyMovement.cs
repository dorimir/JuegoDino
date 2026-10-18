using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private AStarPathfinding pathfinding;
    private List<Vector3> currentPath;
    private int targetIndex;

    public float speed = 5f;

    [Header("Nodos del Enemigo (Solo Líder)")]
    public List<Node> patrolNodes = new List<Node>();

    [Header("Seguimiento Directo al Líder")]
    [Tooltip("Distancia por detrás del líder a la que intentan colocarse los seguidores")]
    public float followDistance = 3.0f;

    [Header("Franja Lateral (Dispersión)")]
    [Tooltip("Ancho de la franja para distribuir a los seguidores a los lados")]
    public float laneWidth = 3.0f;

    [Header("Ajustes de Separación entre Aliados")]
    public float separationRadius = 2.5f;
    public float separationStrength = 2.0f;

    [Header("Prioridad del Líder")]
    [Tooltip("Distancia a la que el seguidor se echa a un lado al ver aproximarse al líder")]
    public float leaderGiveWayRadius = 5.0f;

    private EnemyAI ai;
    private float personalXOffset;
    private float personalZOffset;

    public bool HasReachedDestination { get; private set; }

    void Start()
    {
        pathfinding = FindObjectOfType<AStarPathfinding>();
        ai = GetComponent<EnemyAI>();
        HasReachedDestination = true;

        // Posición fija relativa dentro de la formación
        personalXOffset = Random.Range(-laneWidth * 0.5f, laneWidth * 0.5f);
        personalZOffset = Random.Range(-1.0f, 1.0f);
    }

    void Update()
    {
        if (ai != null && ai.isLeader)
        {
            // El Líder se mueve mediante el grafo de nodos A*
            FollowPathAStar();
        }
        else
        {
            // Los Seguidores siguen la formación alrededor del Líder con repulsión
            FollowLeaderDirect();
        }
    }

    // --- NAVEGACIÓN A* (LÍDER) ---
    public void MoveTo(Vector3 targetPosition)
    {
        if (pathfinding == null) return;

        CancelInvoke();

        currentPath = pathfinding.FindPath(transform.position, targetPosition);

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

    void FollowPathAStar()
    {
        if (currentPath == null || currentPath.Count == 0) return;

        Vector3 targetWaypoint = currentPath[targetIndex];

        transform.position = Vector3.MoveTowards(transform.position, targetWaypoint, speed * Time.deltaTime);

        Vector3 moveDirection = (targetWaypoint - transform.position);
        moveDirection.y = 0;
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDirection), Time.deltaTime * 8f);
        }

        if (Vector3.Distance(transform.position, targetWaypoint) < 0.8f)
        {
            targetIndex++;

            if (targetIndex >= currentPath.Count)
            {
                currentPath = null;
                HasReachedDestination = true;
            }
        }
    }

    // --- SEGUIMIENTO DIRECTO (SEGUIDORES) ---
    void FollowLeaderDirect()
    {
        if (ai == null || ai.leaderTransform == null) return;

        Transform leader = ai.leaderTransform;
        Vector3 leaderForward = leader.forward;
        Vector3 leaderRight = leader.right;

        // 1. PUNTO BASE EN LA FORMACIÓN (Atrás y a los lados del líder)
        Vector3 targetFormationPos = leader.position
            - (leaderForward * (followDistance + personalZOffset))
            + (leaderRight * personalXOffset);

        targetFormationPos.y = transform.position.y;

        // 2. FUERZA DE SEPARACIÓN Y EVASIÓN DEL LÍDER
        Vector3 separationPush = CalculateSeparationPush();

        // 3. POSICIÓN DESTINO FINAL
        Vector3 finalTarget = targetFormationPos + separationPush;

        float distanceToTarget = Vector3.Distance(transform.position, finalTarget);

        if (distanceToTarget > 0.3f || separationPush.sqrMagnitude > 0.1f)
        {
            HasReachedDestination = false;

            // Movimiento suave hacia el objetivo
            transform.position = Vector3.MoveTowards(transform.position, finalTarget, speed * Time.deltaTime);

            // Rotación mirando a la dirección real de avance
            Vector3 moveDir = (finalTarget - transform.position);
            moveDir.y = 0;

            if (moveDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 8f);
            }
        }
        else
        {
            HasReachedDestination = true;

            // Orientarse en la misma dirección que el líder al estar detenido
            transform.rotation = Quaternion.Slerp(transform.rotation, leader.rotation, Time.deltaTime * 5f);
        }
    }

    private Vector3 CalculateSeparationPush()
    {
        Vector3 push = Vector3.zero;
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, leaderGiveWayRadius);
        int count = 0;

        foreach (var hit in hitColliders)
        {
            if (hit.gameObject != gameObject && hit.CompareTag("Enemy"))
            {
                EnemyAI otherAI = hit.GetComponent<EnemyAI>();
                Vector3 away = transform.position - hit.transform.position;
                away.y = 0;
                float dist = away.magnitude;

                // CASO A: EL OTRO ES EL LÍDER -> Apartarse con fuerza hacia los lados
                if (otherAI != null && otherAI.isLeader)
                {
                    if (dist > 0.001f && dist < leaderGiveWayRadius)
                    {
                        Vector3 sidePush = Vector3.Cross(hit.transform.forward, Vector3.up);
                        if (Vector3.Dot(away, sidePush) < 0)
                        {
                            sidePush = -sidePush;
                        }

                        // Fuerza de empuje lateral para despejar la trayectoria del líder
                        push += sidePush.normalized * (leaderGiveWayRadius - dist) * 2.5f;
                        count++;
                    }
                }
                // CASO B: EL OTRO ES UN SEGUIDOR -> Separación entre aliados
                else if (dist > 0.001f && dist < separationRadius)
                {
                    push += (away.normalized / dist) * separationStrength;
                    count++;
                }
            }
        }

        if (count > 0)
        {
            push = Vector3.ClampMagnitude(push, 3.0f);
        }

        return push;
    }

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
    }
}