using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private AStarPathfinding pathfinding;
    private List<Vector3> currentPath;
    private int targetIndex;

    public float speed = 2.4f;

    [Header("Nodos del Enemigo (Solo Lider)")]
    public List<Node> patrolNodes = new List<Node>();

    [Header("Seguimiento Directo al Lider")]
    [Tooltip("Distancia por detras del lider a la que intentan colocarse los seguidores")]
    public float followDistance = 3.0f;

    [Header("Franja Lateral (Dispersion)")]
    [Tooltip("Ancho de la franja para distribuir a los seguidores a los lados")]
    public float laneWidth = 3.0f;

    [Header("Ajustes de Separacion entre Aliados")]
    public float separationRadius = 2.5f;
    public float separationStrength = 2.0f;

    [Header("Prioridad del Lider")]
    [Tooltip("Distancia a la que el seguidor se echa a un lado al ver aproximarse al lider")]
    public float leaderGiveWayRadius = 5.0f;

    private EnemyAI ai;
    private float personalXOffset;
    private float personalZOffset;

    private Vector3 directTargetPosition;
    private bool hasDirectTarget = false;

    public bool HasReachedDestination { get; private set; } = true;

    void Awake()
    {
        pathfinding = GetComponent<AStarPathfinding>();
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        ai = GetComponent<EnemyAI>();
        HasReachedDestination = true;

        personalXOffset = Random.Range(-laneWidth * 0.5f, laneWidth * 0.5f);
        personalZOffset = Random.Range(-1.0f, 1.0f);
    }

    void Start()
    {
        if (pathfinding == null) pathfinding = GetComponent<AStarPathfinding>();
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        if (ai == null) ai = GetComponent<EnemyAI>();
    }

    void Update()
    {
        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);
        bool isFleeing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.FleeState);

        // El Lider, cualquier enemigo en persecucion / huida, o sin lider, se mueve con navegacion autonoma
        if (ai != null && (ai.isLeader || isChasing || isFleeing || ai.leaderTransform == null))
        {
            FollowPathAStar();
        }
        else
        {
            // Para seguidores: si están separados del líder (sin línea directa por paredes tras una persecución),
            // usan la red de nodos como guía hasta volver a tenerlo a la vista
            if (ai != null && ai.leaderTransform != null && !HasClearLineTo(ai.leaderTransform.position) && currentPath != null && currentPath.Count > 0)
            {
                FollowPathAStar();
            }
            else
            {
                // En cuanto tienen línea de visión despejada, siguen la formación directa alrededor del Líder con repulsión
                FollowLeaderDirect();
            }
        }
    }

    /// <summary>
    /// Comprueba si existe una línea directa libre de paredes u obstáculos sólidos
    /// entre el dinosaurio y la posición destino.
    /// </summary>
    public bool HasClearLineTo(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 target = targetPosition + Vector3.up * 0.5f;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;

        if (dist <= 0.8f) return true;

        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.2f, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Enemy")) continue;
            if (ai != null && ai.Player != null && (hit.transform == ai.Player || hit.transform.IsChildOf(ai.Player))) continue;

            // Choca con un obstáculo sólido o pared
            return false;
        }

        return true;
    }

    // --- NAVEGACIÓN: Nodos como guía + Persecución directa libre si hay línea despejada ---
    public void MoveTo(Vector3 targetPosition)
    {
        directTargetPosition = targetPosition;
        hasDirectTarget = true;

        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);

        // 1. Si está persiguiendo y hay línea directa despejada al objetivo, SALIR de la red de nodos y perseguir directo
        if (isChasing && HasClearLineTo(targetPosition))
        {
            currentPath = null;
            targetIndex = 0;
            float dist = Vector3.Distance(transform.position, targetPosition);
            HasReachedDestination = (dist <= 0.8f);
            return;
        }

        // 2. Si hay paredes u obstáculos intermedios, usar la red de nodos A* como GUÍA
        if (pathfinding == null)
        {
            pathfinding = GetComponent<AStarPathfinding>();
            if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        }

        if (pathfinding != null)
        {
            CancelInvoke();
            currentPath = pathfinding.FindPath(transform.position, targetPosition);

            if (currentPath != null && currentPath.Count > 0)
            {
                targetIndex = 0;

                // Atajo inteligente (String-Pulling): Salta directamente al waypoint visible más lejano del camino
                for (int i = currentPath.Count - 1; i >= 0; i--)
                {
                    if (HasClearLineTo(currentPath[i]))
                    {
                        targetIndex = i;
                        break;
                    }
                }

                HasReachedDestination = false;
            }
            else
            {
                // Si no hay nodos intermedios, avanzar directo hacia el destino
                targetIndex = 0;
                float dist = Vector3.Distance(transform.position, targetPosition);
                HasReachedDestination = (dist <= 0.8f);
            }
        }
        else
        {
            float dist = Vector3.Distance(transform.position, targetPosition);
            HasReachedDestination = (dist <= 0.8f);
        }
    }

    void FollowPathAStar()
    {
        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);

        // 1. Durante la persecución: en cuanto se gire la esquina y haya línea de visión directa al jugador,
        // se abandona la guía de nodos inmediatamente para perseguir de forma directa e implacable:
        if (isChasing && hasDirectTarget && HasClearLineTo(directTargetPosition))
        {
            currentPath = null;
        }
        // 2. Si sigue una ruta pero ya puede ver un waypoint más adelante, ataja hacia él de forma natural:
        else if (currentPath != null && currentPath.Count > 0 && targetIndex < currentPath.Count)
        {
            for (int i = currentPath.Count - 1; i > targetIndex; i--)
            {
                if (HasClearLineTo(currentPath[i]))
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        Vector3 targetWaypoint;

        if (currentPath != null && currentPath.Count > 0 && targetIndex < currentPath.Count)
        {
            targetWaypoint = currentPath[targetIndex];
        }
        else if (hasDirectTarget)
        {
            targetWaypoint = directTargetPosition;
        }
        else
        {
            HasReachedDestination = true;
            return;
        }

        Vector3 toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
        float distToWaypoint = toWaypoint.magnitude;
        Vector3 desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;

        // --- SUAVIZADO Y CURVATURA CONTINUA EN GIROS (CORNER BLENDING) ---
        // A) Transición suave y anticipada entre nodos intermedios de la ruta
        if (currentPath != null && targetIndex + 1 < currentPath.Count)
        {
            Vector3 nextPos = currentPath[targetIndex + 1];
            if (distToWaypoint < 2.0f && HasClearLineTo(nextPos))
            {
                float blend = Mathf.Clamp01((2.0f - distToWaypoint) / 1.5f);
                Vector3 toNext = new Vector3(nextPos.x - transform.position.x, 0f, nextPos.z - transform.position.z).normalized;
                desiredDirection = Vector3.Slerp(desiredDirection, toNext, blend * 0.85f).normalized;

                if (distToWaypoint < 0.6f || (distToWaypoint < 1.1f && Vector3.Dot(transform.forward, toNext) > 0.65f))
                {
                    targetIndex++;
                }
            }
        }
        // B) Transición continua y sin frenazos entre el último nodo del tramo y el siguiente nodo del circuito de patrulla
        else if (!isChasing && patrolNodes != null && patrolNodes.Count > 0 && distToWaypoint < 2.0f)
        {
            List<Node> validNodes = patrolNodes.FindAll(n => n != null);
            if (validNodes.Count > 0)
            {
                int nextPatrolIdx = (currentPatrolIndex + 1) % validNodes.Count;
                Vector3 nextPatrolPos = validNodes[nextPatrolIdx].transform.position;
                if (HasClearLineTo(nextPatrolPos))
                {
                    float blend = Mathf.Clamp01((2.0f - distToWaypoint) / 1.5f);
                    Vector3 toNextPatrol = new Vector3(nextPatrolPos.x - transform.position.x, 0f, nextPatrolPos.z - transform.position.z).normalized;
                    desiredDirection = Vector3.Slerp(desiredDirection, toNextPatrol, blend * 0.85f).normalized;

                    if (distToWaypoint < 0.7f || (distToWaypoint < 1.2f && Vector3.Dot(transform.forward, toNextPatrol) > 0.65f))
                    {
                        GoToNextPatrolNode();
                        return;
                    }
                }
            }
        }

        // --- BALANCEO Y MOVIMIENTO NATURAL EN RECTAS ---
        float turnAngle = Vector3.Angle(transform.forward, desiredDirection);
        if (!isChasing && distToWaypoint > 2.2f && turnAngle < 15f)
        {
            Vector3 side = Vector3.Cross(desiredDirection, Vector3.up);
            desiredDirection = (desiredDirection + side * (Mathf.Sin(Time.time * 1.6f) * 0.06f)).normalized;
        }

        // --- REGULACIÓN ORGÁNICA DE VELOCIDAD EN CURVAS ---
        // En patrulla, aminora suavemente la marcha al tomar una curva cerrada y acelera de nuevo en recta
        float speedMultiplier = 1.0f;
        if (!isChasing)
        {
            speedMultiplier = Mathf.Lerp(0.55f, 1.0f, Mathf.Cos(turnAngle * Mathf.Deg2Rad));
        }
        float currentSpeed = speed * speedMultiplier;

        // --- ROTACIÓN Y AVANCE FÍSICO ACOPLADOS (SIN DESLIZAMIENTOS LATERALES) ---
        float turnRate = isChasing ? 400f : 130f; // 130°/s en patrulla elegante y natural, 400°/s en persecución
        Vector3 steeringDirection = Vector3.RotateTowards(transform.forward, desiredDirection, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
        steeringDirection.y = 0;

        if (steeringDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(steeringDirection.normalized);

            float forwardAlignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDirection));
            Vector3 moveDir = Vector3.Lerp(desiredDirection, transform.forward, forwardAlignment).normalized;
            transform.position += moveDir * (currentSpeed * Time.deltaTime);
        }

        // Llegada estándar al waypoint
        if (distToWaypoint < 0.7f)
        {
            if (currentPath != null && targetIndex < currentPath.Count)
            {
                targetIndex++;
                if (targetIndex >= currentPath.Count)
                {
                    currentPath = null;
                }
            }
            else
            {
                HasReachedDestination = true;
                hasDirectTarget = false;
            }
        }
    }

    // --- SEGUIMIENTO DIRECTO (SEGUIDORES EN PATRULLA) ---
    void FollowLeaderDirect()
    {
        if (ai == null || ai.leaderTransform == null) return;

        Transform leader = ai.leaderTransform;
        Vector3 leaderForward = leader.forward;
        Vector3 leaderRight = leader.right;

        // 1. PUNTO BASE EN LA FORMACION (Atras y a los lados del lider)
        Vector3 targetFormationPos = leader.position
            - (leaderForward * (followDistance + personalZOffset))
            + (leaderRight * personalXOffset);

        targetFormationPos.y = transform.position.y;

        // 2. FUERZA DE SEPARACION Y EVASION DEL LIDER
        Vector3 separationPush = CalculateSeparationPush();

        // 3. POSICION DESTINO FINAL
        Vector3 finalTarget = targetFormationPos + separationPush;

        float distanceToTarget = Vector3.Distance(transform.position, finalTarget);

        if (distanceToTarget > 0.3f || separationPush.sqrMagnitude > 0.1f)
        {
            HasReachedDestination = false;

            transform.position = Vector3.MoveTowards(transform.position, finalTarget, speed * Time.deltaTime);

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

                // CASO A: EL OTRO ES EL LIDER -> Apartarse con fuerza hacia los lados
                if (otherAI != null && otherAI.isLeader)
                {
                    if (dist > 0.001f && dist < leaderGiveWayRadius)
                    {
                        Vector3 sidePush = Vector3.Cross(hit.transform.forward, Vector3.up);
                        if (Vector3.Dot(away, sidePush) < 0)
                        {
                            sidePush = -sidePush;
                        }

                        push += sidePush.normalized * (leaderGiveWayRadius - dist) * 2.5f;
                        count++;
                    }
                }
                // CASO B: EL OTRO ES UN SEGUIDOR -> Separacion entre aliados
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

    public void ClearPath()
    {
        currentPath = null;
        targetIndex = 0;
        hasDirectTarget = false;
        HasReachedDestination = true;
    }

    private int currentPatrolIndex = -1;

    public void GoToNextPatrolNode()
    {
        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            List<Node> validNodes = patrolNodes.FindAll(n => n != null);
            if (validNodes.Count > 0)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % validNodes.Count;
                MoveTo(validNodes[currentPatrolIndex].transform.position);
                return;
            }
        }

        GoToRandomNode();
    }

    public Node GetNearestPatrolOrGraphNode()
    {
        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            Node nearest = null;
            float minDist = Mathf.Infinity;
            int nearestIdx = 0;

            for (int i = 0; i < patrolNodes.Count; i++)
            {
                if (patrolNodes[i] == null) continue;
                float d = Vector3.Distance(transform.position, patrolNodes[i].transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = patrolNodes[i];
                    nearestIdx = i;
                }
            }

            if (nearest != null)
            {
                currentPatrolIndex = nearestIdx;
                return nearest;
            }
        }

        if (pathfinding == null)
        {
            pathfinding = GetComponent<AStarPathfinding>();
            if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        }

        if (pathfinding != null)
        {
            return pathfinding.GetClosestNode(transform.position);
        }

        return null;
    }

    public void GoToRandomNode()
    {
        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            List<Node> validNodes = patrolNodes.FindAll(n => n != null);
            if (validNodes.Count > 0)
            {
                Node randomNode = validNodes[Random.Range(0, validNodes.Count)];
                if (randomNode != null)
                {
                    MoveTo(randomNode.transform.position);
                    return;
                }
            }
        }

        if (pathfinding != null)
        {
            Node closest = pathfinding.GetClosestNode(transform.position);
            if (closest != null && closest.neighbors != null && closest.neighbors.Count > 0)
            {
                Node randNeighbor = closest.neighbors[Random.Range(0, closest.neighbors.Count)];
                if (randNeighbor != null)
                {
                    MoveTo(randNeighbor.transform.position);
                }
            }
        }
    }
}
