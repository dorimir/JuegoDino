using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private AStarPathfinding pathfinding;
    private List<Vector3> currentPath;
    private int targetIndex;

    [Header("Configuración de Velocidad")]
    public float speed = 3.2f;
    [Tooltip("Aceleración hacia adelante para ganar velocidad.")]
    public float acceleration = 9.0f;

    [Header("Comportamiento de Giro y Drift")]
    [Tooltip("Velocidad de rotación angular en patrulla (grados por segundo).")]
    public float patrolTurnRate = 180f;
    [Tooltip("Velocidad de rotación angular en persecución.")]
    public float chaseTurnRate = 340f;
    [Tooltip("Fricción lateral para el movimiento. Valores altos eliminan el derrape y patinaje lateral.")]
    public float driftFriction = 35.0f;
    [Tooltip("Inclinación corporal lateral (banking/roll) en grados al tomar curvas.")]
    public float maxBankAngle = 1.5f;
    [Tooltip("Radio horizontal para considerar un nodo o waypoint alcanzado.")]
    public float waypointRadius = 0.75f;

    [Header("Nodos del Enemigo (Solo Lider)")]
    public List<Node> patrolNodes = new List<Node>();

    [Header("Seguimiento Directo al Lider")]
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

    public float CurrentForwardSpeed => currentVelocity.magnitude;
    public Vector3 Velocity => currentVelocity;

    public void ResetVelocity()
    {
        currentVelocity = Vector3.zero;
        currentBankAngle = 0f;
    }

    private EnemyAI ai;
    private Vector3 directTargetPosition;
    private bool hasDirectTarget = false;

    public bool HasReachedDestination { get; private set; } = true;

    // --- Físicas de Drift, Inercia y Giro Natural ---
    private Vector3 currentVelocity = Vector3.zero;
    private float currentBankAngle = 0f;
    private Vector3 lastWaypointPos;

    // --- Vigilante Anti-Atascos (Watchdog) ---
    private Vector3 lastStuckCheckPosition;
    private float stuckTimer = 0f;
    private const float StuckCheckInterval = 0.8f;
    private const float MinMoveThreshold = 0.15f;

    private int currentPatrolIndex = -1;

    void Awake()
    {
        pathfinding = GetComponent<AStarPathfinding>();
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        ai = GetComponent<EnemyAI>();
        HasReachedDestination = true;

        lastStuckCheckPosition = transform.position;
        lastWaypointPos = transform.position;

        // Asegurar que no existan fuerzas de separación magnéticas entre dinosaurios
        separationRadius = 0f;
        separationStrength = 0f;

        // Asignar capa "Dinosaurios" (Layer 3) a todo el dinosaurio y sus hijos para evitar colisiones internas con máscaras de pared
        int dinoLayer = LayerMask.NameToLayer("Dinosaurios");
        if (dinoLayer != -1)
        {
            gameObject.layer = dinoLayer;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = dinoLayer;
            }
        }

        // Auto-calibrar el BoxCollider para asegurar dimensiones optimas para navegacion sin atascos
        BoxCollider box = GetComponent<BoxCollider>();
        CapsuleCollider cap = GetComponent<CapsuleCollider>();

        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);

        if (isSmall)
        {
            patrolNodes.Clear(); // Las presas recorren toda la red conectada de nodos del mapa de forma fluida

            if (box != null)
            {
                Vector3 s = box.size;
                s.x = Mathf.Min(s.x, 0.35f);
                s.y = Mathf.Clamp(s.y, 0.4f, 0.7f);
                s.z = Mathf.Min(s.z, 0.5f);
                box.size = s;
                box.center = new Vector3(0, s.y * 0.5f, 0);
            }
            if (cap != null)
            {
                cap.radius = Mathf.Min(cap.radius, 0.2f);
                cap.height = Mathf.Min(cap.height, 0.6f);
            }

            speed = 5.2f;
            acceleration = 14.0f;
            patrolTurnRate = 260f;
            chaseTurnRate = 420f;
            driftFriction = 45.0f; // Fricción lateral alta para eliminar derrapes y patinaje
            maxBankAngle = 1.5f;
        }
        else
        {
            if (box != null)
            {
                Vector3 s = box.size;
                s.x = Mathf.Min(s.x, 0.6f);
                s.y = Mathf.Clamp(s.y, 0.8f, 1.2f);
                s.z = Mathf.Min(s.z, 0.8f);
                box.size = s;

                Vector3 c = box.center;
                c.y = s.y * 0.5f;
                box.center = c;
            }
            if (cap != null)
            {
                cap.radius = Mathf.Min(cap.radius, 0.35f);
            }

            speed = 2.4f;
            acceleration = 9.0f;
            patrolTurnRate = 180f;
            chaseTurnRate = 340f;
            driftFriction = 35.0f;
            maxBankAngle = 1.5f;
        }

        // Asegurar que el dinosaurio posee un MeshCollider con convex = true
        MeshCollider meshCol = GetComponent<MeshCollider>();
        if (meshCol == null) meshCol = GetComponentInChildren<MeshCollider>();
        if (meshCol == null)
        {
            meshCol = gameObject.AddComponent<MeshCollider>();
            meshCol.convex = true;
            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                meshCol.sharedMesh = mf.sharedMesh;
            }
        }
        else
        {
            meshCol.convex = true;
        }
    }

    void Start()
    {
        if (pathfinding == null) pathfinding = GetComponent<AStarPathfinding>();
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        if (ai == null) ai = GetComponent<EnemyAI>();

        // Si es dinosaurio pequeño y ya tiene su camino asignado (por DistributePreyAcrossMap), no sobreescribir
        if (ai != null && ai.IsSmallDino)
        {
            if (HasReachedDestination)
            {
                GoToNextPatrolNode();
            }
            return;
        }

        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            GoToNextPatrolNode();
        }
        else
        {
            GoToNearestPatrolNode();
        }
    }

    void Update()
    {
        FollowPathAStar();

        // Si es un objeto autónomo sin EnemyAI y llegó al destino, pasar automáticamente al siguiente nodo
        if (ai == null && HasReachedDestination && patrolNodes != null && patrolNodes.Count > 0)
        {
            GoToNextPatrolNode();
        }

        // Resolución física suave de solapamiento físico entre dinosaurios (evita traspasarse)
        ResolveMutualCollisions();
    }

    void LateUpdate()
    {
    }

    /// <summary>
    /// Comprueba si existe una trayectoria libre de paredes u obstáculos sólidos
    /// entre el dinosaurio y la posición destino (en el plano horizontal a la altura del torso).
    /// </summary>
    public bool HasClearLineTo(Vector3 targetPosition, float clearanceRadius = -1f)
    {
        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        if (clearanceRadius < 0f)
        {
            clearanceRadius = isSmall ? 0.20f : 0.35f;
        }
        float rayHeight = isSmall ? 0.45f : 0.85f;
        Vector3 origin = new Vector3(transform.position.x, transform.position.y + rayHeight, transform.position.z);
        Vector3 target = new Vector3(targetPosition.x, transform.position.y + rayHeight, targetPosition.z);
        Vector3 dir = target - origin;
        float dist = dir.magnitude;

        if (dist <= 0.4f) return true;

        // Capas: excluir capa 3 (Dinosaurios) y capa 2 (Ignore Raycast)
        int wallLayerMask = ~((1 << 3) | (1 << 2));

        // 1. Linecast directo: detecta muros o esquinas intermedias
        if (Physics.Linecast(origin, target, out RaycastHit lineHit, wallLayerMask, QueryTriggerInteraction.Ignore))
        {
            bool isEnemy = lineHit.collider != null && (lineHit.collider.CompareTag("Enemy") || lineHit.collider.GetComponentInParent<EnemyAI>() != null);
            if (lineHit.collider != null && !lineHit.collider.transform.IsChildOf(transform) && !isEnemy)
            {
                bool isTargetPlayer = (ai != null && ai.Player != null && (lineHit.transform == ai.Player || lineHit.transform.IsChildOf(ai.Player)));
                if (!isTargetPlayer && lineHit.normal.y < 0.7f)
                {
                    return false;
                }
            }
        }

        // 2. SphereCast con offset frontal para comprobar el paso físico del cuerpo sin chocar con esquinas
        if (dist > clearanceRadius * 2f)
        {
            Vector3 fwdOrigin = origin + dir.normalized * 0.15f;
            float castDist = Mathf.Max(0.05f, dist - 0.3f);

            RaycastHit[] hits = Physics.SphereCastAll(fwdOrigin, clearanceRadius, dir.normalized, castDist, wallLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                bool isOtherEnemy = hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null;
                if (isOtherEnemy) continue;
                if (ai != null && ai.Player != null && (hit.transform == ai.Player || hit.transform.IsChildOf(ai.Player))) continue;
                if (hit.normal.y >= 0.7f) continue; // Suelo o rampas

                string colName = hit.collider.gameObject.name.ToLower();
                if (colName.Contains("plane") || colName.Contains("floor") || colName.Contains("suelo")) continue;

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Comprueba si el dinosaurio puede moverse directamente hacia un nodo específico mediante Raycast (sin muros que lo bloqueen).
    /// </summary>
    public bool CanMoveToNode(Node node)
    {
        if (node == null) return false;
        return HasClearLineTo(node.transform.position);
    }

    /// <summary>
    /// Devuelve todos los nodos de la escena a los que el dinosaurio puede moverse directamente mediante Raycast.
    /// </summary>
    public List<Node> GetReachableNodes()
    {
        List<Node> reachable = new List<Node>();
        Node[] allNodes = (pathfinding != null) ? pathfinding.GetAllNodes() : Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
        if (allNodes == null) return reachable;

        foreach (Node node in allNodes)
        {
            if (node != null && CanMoveToNode(node))
            {
                reachable.Add(node);
            }
        }
        return reachable;
    }

    /// <summary>
    /// Devuelve los nodos de patrulla asignados que son actualmente alcanzables directamente mediante Raycast.
    /// </summary>
    public List<Node> GetReachablePatrolNodes()
    {
        List<Node> reachable = new List<Node>();
        if (patrolNodes == null) return reachable;

        foreach (Node node in patrolNodes)
        {
            if (node != null && CanMoveToNode(node))
            {
                reachable.Add(node);
            }
        }
        return reachable;
    }

    // --- NAVEGACIÓN: Nodos como guía + Persecución directa libre si hay línea despejada ---
    public void MoveTo(Vector3 targetPosition)
    {
        directTargetPosition = targetPosition;
        hasDirectTarget = true;
        lastStuckCheckPosition = transform.position;
        stuckTimer = 0f;

        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);
        bool isFollower = (ai != null && ai.IsSmallDino && ai.LeaderAI != null);

        // 1. Si está persiguiendo o siguiendo al líder en manada y hay línea directa despejada, avanzar directo
        if ((isChasing || isFollower) && HasClearLineTo(targetPosition))
        {
            currentPath = null;
            targetIndex = 0;
            float horizontalDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(targetPosition.x, targetPosition.z)
            );
            float stopDist = isChasing ? 0.15f : (isFollower ? 0.35f : waypointRadius);
            HasReachedDestination = (horizontalDist <= stopDist);
            lastWaypointPos = transform.position;
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

                // Si ya estamos practicamente encima del primer nodo de la ruta (< 0.4m), comenzar en el siguiente
                if (currentPath.Count > 1)
                {
                    float distToFirst = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z),
                        new Vector2(currentPath[0].x, currentPath[0].z)
                    );

                    if (distToFirst <= 0.4f)
                    {
                        targetIndex = 1;
                    }
                    else if (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.FleeState)
                    {
                        // En huida, comprobar que el primer nodo del camino no acerque al pequeño hacia el dinosaurio grande
                        Vector3 threatPos = (ai.ChasingPredator != null) ? ai.ChasingPredator.transform.position : 
                                            ((ai.CurrentThreat != null) ? ai.CurrentThreat.position : ai.SpottedThreatPosition);
                        float myDistToThreat = Vector3.Distance(transform.position, threatPos);
                        float firstDistToThreat = Vector3.Distance(currentPath[0], threatPos);

                        if (firstDistToThreat < myDistToThreat)
                        {
                            targetIndex = 1;
                        }
                    }
                }

                HasReachedDestination = false;
                lastWaypointPos = transform.position;
            }
            else
            {
                targetIndex = 0;
                float horizontalDist = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(targetPosition.x, targetPosition.z)
                );
                HasReachedDestination = (horizontalDist <= waypointRadius);
                lastWaypointPos = transform.position;
            }
        }
        else
        {
            float horizontalDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(targetPosition.x, targetPosition.z)
            );
            HasReachedDestination = (horizontalDist <= waypointRadius);
            lastWaypointPos = transform.position;
        }
    }

    /// <summary>
    /// Establece un objetivo de persecución directo y continuo en tiempo real (evita frenados y paradas arbitrarias).
    /// </summary>
    public void SetDirectChaseTarget(Vector3 targetPos)
    {
        directTargetPosition = targetPos;
        hasDirectTarget = true;
        currentPath = null;
        targetIndex = 0;
        HasReachedDestination = false;
        lastWaypointPos = transform.position;
    }

    void FollowPathAStar()
    {
        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);
        bool isFollower = (ai != null && ai.IsSmallDino && ai.LeaderAI != null);

        // 1. Durante la persecución o seguimiento del líder: en cuanto se gire la esquina y haya línea de visión directa,
        // se abandona la guía de nodos inmediatamente para moverse de forma directa:
        if ((isChasing || isFollower) && hasDirectTarget && HasClearLineTo(directTargetPosition))
        {
            currentPath = null;
        }
            
            //BYPASS SEEK
            Vector3 currentWaypoint = currentPath[targetIndex];

            //Si el nodo actual es el ultimo
            if (targetIndex+1 >= currentPath.Count)
            {
                this.GetComponent<ForcesSeek>().setArrival(true); 
            }
            else 
            {
                this.GetComponent<ForcesSeek>().setArrival(false); 
            }

            // Mover hacia el nodo waypoint actual
            this.GetComponent<ForcesSeek>().setTarget(currentWaypoint); 

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
            // Desacelerar suavemente cuando no hay destino
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, acceleration * Time.deltaTime);
            return;
        }

        Vector3 toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
        float distToWaypoint = toWaypoint.magnitude;
        Vector3 desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;

        // --- COMPROBACIÓN DE LLEGADA AL WAYPOINT O NODO ---
        bool reachedWaypoint;
        if (currentPath != null && currentPath.Count > 0)
        {
            Vector3 previousPos = (targetIndex > 0) ? currentPath[targetIndex - 1] : lastWaypointPos;
            Vector3 segmentDir = new Vector3(targetWaypoint.x - previousPos.x, 0f, targetWaypoint.z - previousPos.z);
            bool hasPassedWaypointPlane = false;

            if (segmentDir.sqrMagnitude > 0.05f)
            {
                Vector3 pastVector = new Vector3(transform.position.x - targetWaypoint.x, 0f, transform.position.z - targetWaypoint.z);
                hasPassedWaypointPlane = (Vector3.Dot(pastVector, segmentDir.normalized) > 0f) && (distToWaypoint <= waypointRadius * 1.25f);
            }

            reachedWaypoint = (distToWaypoint <= waypointRadius) || hasPassedWaypointPlane;
        }
        else
        {
            // Persecución directa continua o seguimiento de líder:
            float stopDist = isChasing ? 0.15f : (isFollower ? 0.35f : waypointRadius);
            reachedWaypoint = (distToWaypoint <= stopDist);
        }

        // Avanzar al siguiente nodo de la ruta
        if (reachedWaypoint)
        {
            lastWaypointPos = targetWaypoint;
            targetIndex++;

            if (currentPath != null && targetIndex < currentPath.Count)
            {
                // Pasamos al siguiente nodo conectado en la ruta
                targetWaypoint = currentPath[targetIndex];
                toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
                distToWaypoint = toWaypoint.magnitude;
                desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;
            }
            else
            {
                // Fin de la ruta de nodos
                currentPath = null;
                if (isChasing || isFollower)
                {
                    // En persecución o seguimiento, se continúa al objetivo sin apagar el avance bruscamente
                    float stopDist = isChasing ? 0.15f : (isFollower ? 0.35f : waypointRadius);
                    HasReachedDestination = (distToWaypoint <= stopDist);
                    if (HasReachedDestination)
                    {
                        hasDirectTarget = false;
                        return;
                    }
                }
                else
                {
                    hasDirectTarget = false;
                    HasReachedDestination = true;
                    return;
                }
            }
        }

        // --- VIGILANTE ANTI-ATASCOS (ANTI-STUCK WATCHDOG) ---
        stuckTimer += Time.deltaTime;
        if (stuckTimer >= StuckCheckInterval)
        {
            float movedDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(lastStuckCheckPosition.x, lastStuckCheckPosition.z)
            );

            if (movedDist < MinMoveThreshold && !HasReachedDestination)
            {
                // Atasco detectado: si hay más waypoints, avanzar
                if (currentPath != null && targetIndex + 1 < currentPath.Count)
                {
                    targetIndex++;
                    lastWaypointPos = transform.position;
                }
                else
                {
                    if (patrolNodes != null && patrolNodes.Count > 0)
                    {
                        GoToNextPatrolNode();
                    }
                    else
                    {
                        ClearPath();
                    }
                }

                // Despegue suave
                transform.position += desiredDirection * 0.2f;
            }

            lastStuckCheckPosition = transform.position;
            stuckTimer = 0f;
        }

        // --- REGULACIÓN ORGÁNICA DE VELOCIDAD EN CURVAS ---
        float turnAngle = Vector3.Angle(transform.forward, desiredDirection);
        float speedMultiplier = 1.0f;
        if (!isChasing && turnAngle > 45f)
        {
            // Desaceleración moderada en giros cerrados para acentuar el derrape
            speedMultiplier = Mathf.Lerp(0.7f, 1.0f, Mathf.Cos(turnAngle * Mathf.Deg2Rad));
        }
        float targetSpeed = speed * speedMultiplier;

        // --- ROTACIÓN DEL CUERPO (HEADING) ---
        float currentTurnRate = isChasing ? chaseTurnRate : patrolTurnRate;
        if (desiredDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, currentTurnRate * Time.deltaTime);
        }

        // --- CONTROL DIRECTO DE VELOCIDAD (DRIFT REBAJADO CONSIDERABLEMENTE) ---
        Vector3 forwardDir = transform.forward;
        Vector3 rightDir = transform.right;

        // Proyectar velocidad actual en ejes locales
        float forwardSpeed = Vector3.Dot(currentVelocity, forwardDir);
        float lateralSpeed = Vector3.Dot(currentVelocity, rightDir);

        // Aceleración hacia adelante hacia la velocidad deseada
        forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, acceleration * Time.deltaTime);

        // Derrape lateral fuertemente rebajado: se amortigua de inmediato para evitar que el dinosaurio patine
        lateralSpeed = Mathf.MoveTowards(lateralSpeed, 0f, Mathf.Max(driftFriction, 45.0f) * Time.deltaTime);

        // Reconstruir vector de velocidad en el plano horizontal eliminando el patinaje lateral
        currentVelocity = (forwardDir * forwardSpeed) + (rightDir * (lateralSpeed * 0.05f));

        // --- INCLINACIÓN CORPORAL (BANKING ROLL VISUAL) ---
        float signedTurn = Vector3.SignedAngle(forwardDir, desiredDirection, Vector3.up);
        float targetBank = Mathf.Clamp(-signedTurn * 0.12f, -maxBankAngle, maxBankAngle);
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.deltaTime * 6f);

        // Aplicar el roll sobre la rotación del dinosaurio manteniendo el heading horizontal
        Quaternion horizontalRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = horizontalRot * Quaternion.Euler(0f, 0f, currentBankAngle);

        // --- DESPLAZAMIENTO SEGURO (PAREDES + DINOSAURIOS SÓLIDOS) ---
        Vector3 displacement = currentVelocity * Time.deltaTime;
        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        float bodyRadius = isSmall ? 0.20f : 0.35f;
        MoveSafely(displacement, bodyRadius);
    }

    /// <summary>
    /// Desplaza al dinosaurio resolviendo deslizamiento por paredes y bloqueando penetración contra otros dinosaurios.
    /// </summary>
    private void MoveSafely(Vector3 displacement, float bodyRadius = 0.20f)
    {
        displacement.y = 0f;
        if (displacement.sqrMagnitude < 0.000001f) return;

        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        float moveDist = displacement.magnitude;
        Vector3 moveDir = displacement / moveDist;
        float rayHeight = isSmall ? 0.45f : 0.85f;
        Vector3 rayOrigin = transform.position + Vector3.up * rayHeight;
        int wallMask = ~((1 << 3) | (1 << 2));

        // 1. Deslizamiento sobre paredes verticales (evita chocar o atravesar esquinas)
        if (Physics.SphereCast(rayOrigin, bodyRadius, moveDir, out RaycastHit hit, moveDist + 0.05f, wallMask, QueryTriggerInteraction.Ignore))
        {
            bool isEnemy = hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null;
            if (hit.transform != transform && !hit.transform.IsChildOf(transform) && !isEnemy && hit.normal.y < 0.7f)
            {
                if (ai == null || ai.Player == null || (hit.transform != ai.Player && !hit.transform.IsChildOf(ai.Player)))
                {
                    Vector3 slideDir = Vector3.ProjectOnPlane(displacement, hit.normal);
                    slideDir.y = 0f;
                    displacement = slideDir;
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);
                }
            }
        }

        // 2. Bloqueo de avance contra otros dinosaurios (solo en contacto físico real)
        if (EnemyAI.AllEnemies != null && EnemyAI.AllEnemies.Count > 0)
        {
            for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
            {
                EnemyAI other = EnemyAI.AllEnemies[i];
                if (other == null || other == ai || !other.gameObject.activeInHierarchy) continue;

                Vector3 toOther = other.transform.position - transform.position;
                toOther.y = 0f;
                float dist = toOther.magnitude;
                float physicalContactDist = (ai != null && ai.IsSmallDino && other.IsSmallDino) ? 0.38f : 0.95f;

                if (dist < physicalContactDist && dist > 0.001f)
                {
                    Vector3 dirToOther = toOther / dist;
                    float approachComponent = Vector3.Dot(displacement, dirToOther);

                    if (approachComponent > 0f)
                    {
                        displacement -= dirToOther * approachComponent;
                    }
                }
            }
        }

        transform.position += displacement;

        // 3. Depenetración suave contra muros verticales (ignora completamente suelos y planos horizontales)
        Vector3 curOrigin = transform.position + Vector3.up * rayHeight;
        Collider[] nearbyWalls = Physics.OverlapSphere(curOrigin, bodyRadius, wallMask, QueryTriggerInteraction.Ignore);
        for (int w = 0; w < nearbyWalls.Length; w++)
        {
            Collider wallCol = nearbyWalls[w];
            if (wallCol.transform == transform || wallCol.transform.IsChildOf(transform)) continue;
            bool isEnemy = wallCol.CompareTag("Enemy") || wallCol.GetComponentInParent<EnemyAI>() != null;
            if (isEnemy) continue;
            if (ai != null && ai.Player != null && (wallCol.transform == ai.Player || wallCol.transform.IsChildOf(ai.Player))) continue;

            // Descartar suelos o planos horizontales
            string colName = wallCol.gameObject.name.ToLower();
            if (colName.Contains("plane") || colName.Contains("floor") || colName.Contains("suelo")) continue;

            Vector3 closest = wallCol.ClosestPoint(curOrigin);
            // Si el punto más cercano está a nivel de suelo, es el suelo: ignorar
            if (closest.y < transform.position.y + 0.15f) continue;

            Vector3 diff = curOrigin - closest;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist < bodyRadius && dist > 0.005f)
            {
                Vector3 pushOut = (diff / dist) * (bodyRadius - dist);
                transform.position += pushOut;
                curOrigin += pushOut;
            }
        }
    }

    /// <summary>
    /// Resuelve de forma física suave cualquier solapamiento físico entre dinosaurios para impedir que se atraviesen,
    /// eliminando cualquier fuerza de rebote o repulsión magnética artificial a distancia.
    /// </summary>
    private void ResolveMutualCollisions()
    {
        if (EnemyAI.AllEnemies == null || EnemyAI.AllEnemies.Count == 0) return;

        int wallMask = ~((1 << 3) | (1 << 2));

        for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
        {
            EnemyAI other = EnemyAI.AllEnemies[i];
            if (other == null || other == ai || !other.gameObject.activeInHierarchy) continue;

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            // Solo actuar en caso de verdadera interpenetración física de mallas/cuerpos
            float requiredDist = (ai != null && ai.IsSmallDino && other.IsSmallDino) ? 0.36f : 0.90f;

            if (dist < requiredDist)
            {
                float overlap = requiredDist - dist;
                Vector3 pushDir;

                if (dist > 0.001f)
                {
                    pushDir = diff / dist;
                }
                else
                {
                    pushDir = (GetInstanceID() > other.GetInstanceID()) ? transform.right : -transform.right;
                }

                // Distribución de depenetración: el líder no cede, el seguidor amortigua suavemente
                float myWeight = 0.5f;
                if (ai != null && other != null)
                {
                    if (ai.isLeader && !other.isLeader) myWeight = 0.0f;
                    else if (!ai.isLeader && other.isLeader) myWeight = 1.0f;
                }

                // Factor suave (0.25f) para evitar sacudidas o rebotes de resorte
                Vector3 push = pushDir * (overlap * myWeight * 0.25f);
                if (push.sqrMagnitude > 0.00001f)
                {
                    float rayHeight = (ai != null && ai.IsSmallDino) ? 0.35f : 0.75f;
                    Vector3 checkOrigin = transform.position + Vector3.up * rayHeight;
                    if (!Physics.SphereCast(checkOrigin, 0.2f, push.normalized, out RaycastHit wallHit, push.magnitude + 0.1f, wallMask, QueryTriggerInteraction.Ignore))
                    {
                        transform.position += push;
                    }
                    else
                    {
                        Vector3 slidePush = Vector3.ProjectOnPlane(push, wallHit.normal);
                        slidePush.y = 0f;
                        transform.position += slidePush;
                    }
                }
            }
        }
    }

    public void ClearPath()
    {
        currentPath = null;
        targetIndex = 0;
        hasDirectTarget = false;
        HasReachedDestination = true;
        currentVelocity = Vector3.zero;
    }

    public void SetCurrentDestinationNode(Node node)
    {
        currentDestinationNode = node;
        if (node != null && !recentlyVisitedNodes.Contains(node))
        {
            recentlyVisitedNodes.Add(node);
        }
    }

    // Historial reciente de nodos visitados para recorrer toda la red de nodos sin ciclos repetitivos
    private readonly List<Node> recentlyVisitedNodes = new List<Node>();
    private const int MaxRecentNodesHistory = 14;
    private Node currentDestinationNode;

    /// <summary>
    /// Selecciona el siguiente nodo de patrulla navegando por toda la red de nodos conectada.
    /// Prioriza vecinos no visitados recientemente y mantiene una dirección de avance fluida para recorrer el mapa entero.
    /// </summary>
    public Node GetNextNetworkPatrolNode()
    {
        if (pathfinding == null)
        {
            pathfinding = GetComponent<AStarPathfinding>();
            if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        }

        Node currentNode = (currentDestinationNode != null) ? currentDestinationNode : GetNearestPatrolOrGraphNode();
        if (currentNode == null) return null;

        // Registrar nodo actual en el historial reciente
        if (!recentlyVisitedNodes.Contains(currentNode))
        {
            recentlyVisitedNodes.Add(currentNode);
            if (recentlyVisitedNodes.Count > MaxRecentNodesHistory)
            {
                recentlyVisitedNodes.RemoveAt(0);
            }
        }

        // Buscar entre los vecinos conectados directamente en el grafo con seguridad comprobada por Raycast
        if (currentNode.neighbors != null && currentNode.neighbors.Count > 0)
        {
            List<Node> validNeighbors = currentNode.neighbors.FindAll(n => n != null && CanMoveToNode(n));
            if (validNeighbors.Count == 0)
            {
                validNeighbors = currentNode.neighbors.FindAll(n => n != null);
            }

            if (validNeighbors.Count > 0)
            {
                // 1. Filtrar vecinos que NO hayan sido visitados recientemente
                List<Node> unvisitedNeighbors = validNeighbors.FindAll(n => !recentlyVisitedNodes.Contains(n));

                if (unvisitedNeighbors.Count > 0)
                {
                    // Priorizar el vecino que mantenga el avance más frontal (menor giro brusco)
                    Node bestNeighbor = unvisitedNeighbors[0];
                    float bestScore = -2f;
                    Vector3 fwd = transform.forward;
                    fwd.y = 0f;

                    for (int i = 0; i < unvisitedNeighbors.Count; i++)
                    {
                        Vector3 toNeighbor = unvisitedNeighbors[i].transform.position - transform.position;
                        toNeighbor.y = 0f;
                        if (toNeighbor.sqrMagnitude > 0.001f)
                        {
                            float dot = Vector3.Dot(fwd.normalized, toNeighbor.normalized);
                            if (dot > bestScore)
                            {
                                bestScore = dot;
                                bestNeighbor = unvisitedNeighbors[i];
                            }
                        }
                    }
                    return bestNeighbor;
                }
                else
                {
                    // Todos los vecinos ya fueron visitados: elegir el MENOS recientemente visitado
                    Node leastRecent = validNeighbors[0];
                    int lowestIndex = int.MaxValue;

                    for (int i = 0; i < validNeighbors.Count; i++)
                    {
                        int idx = recentlyVisitedNodes.IndexOf(validNeighbors[i]);
                        if (idx < lowestIndex)
                        {
                            lowestIndex = idx;
                            leastRecent = validNeighbors[i];
                        }
                    }
                    return leastRecent;
                }
            }
        }

        // Fallback: si no hay vecinos inmediatos, buscar cualquier nodo del grafo que no esté en historial
        Node[] allNodes = (pathfinding != null) ? pathfinding.GetAllNodes() : null;
        if (allNodes != null && allNodes.Length > 0)
        {
            for (int i = 0; i < allNodes.Length; i++)
            {
                if (allNodes[i] != null && allNodes[i] != currentNode && !recentlyVisitedNodes.Contains(allNodes[i]))
                {
                    return allNodes[i];
                }
            }
        }

        return null;
    }

    public void GoToNextPatrolNode()
    {
        // Para dinosaurios presa (pequeños): patrullar toda la red de nodos
        if (ai != null && ai.IsSmallDino)
        {
            Node nextNetworkNode = GetNextNetworkPatrolNode();
            if (nextNetworkNode != null)
            {
                currentDestinationNode = nextNetworkNode;
                MoveTo(nextNetworkNode.transform.position);
                return;
            }
        }

        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            List<Node> validNodes = patrolNodes.FindAll(n => n != null);
            if (validNodes.Count > 0)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % validNodes.Count;
                currentDestinationNode = validNodes[currentPatrolIndex];
                MoveTo(currentDestinationNode.transform.position);
                return;
            }
        }

        // Si no tiene patrolNodes fijos, recorrer la red de nodos del mapa
        Node fallbackNetworkNode = GetNextNetworkPatrolNode();
        if (fallbackNetworkNode != null)
        {
            currentDestinationNode = fallbackNetworkNode;
            MoveTo(fallbackNetworkNode.transform.position);
            return;
        }

        GoToRandomNode();
    }

    public Node GetNearestPatrolOrGraphNode()
    {
        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            Node nearestReachable = null;
            float minReachableDist = Mathf.Infinity;
            int nearestReachableIdx = -1;

            Node nearestFallback = null;
            float minFallbackDist = Mathf.Infinity;
            int nearestFallbackIdx = -1;

            for (int i = 0; i < patrolNodes.Count; i++)
            {
                if (patrolNodes[i] == null) continue;
                float d = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(patrolNodes[i].transform.position.x, patrolNodes[i].transform.position.z)
                );

                if (d < minFallbackDist)
                {
                    minFallbackDist = d;
                    nearestFallback = patrolNodes[i];
                    nearestFallbackIdx = i;
                }

                if (CanMoveToNode(patrolNodes[i]))
                {
                    if (d < minReachableDist)
                    {
                        minReachableDist = d;
                        nearestReachable = patrolNodes[i];
                        nearestReachableIdx = i;
                    }
                }
            }

            if (nearestReachable != null)
            {
                currentPatrolIndex = nearestReachableIdx;
                return nearestReachable;
            }

            if (nearestFallback != null)
            {
                currentPatrolIndex = nearestFallbackIdx;
                return nearestFallback;
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

    /// <summary>
    /// Envía al dinosaurio al nodo de patrulla más cercano de su lista (útil al regresar de persecución o huida).
    /// </summary>
    public void GoToNearestPatrolNode()
    {
        Node nearest = GetNearestPatrolOrGraphNode();
        if (nearest != null)
        {
            MoveTo(nearest.transform.position);
        }
        else
        {
            GoToNextPatrolNode();
        }
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
