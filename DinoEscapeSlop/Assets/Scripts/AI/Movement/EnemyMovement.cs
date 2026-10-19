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
    [Tooltip("Fricción lateral para el derrape (drift). Valores entre 3.5 y 5.5 generan un derrape suave, natural y vistoso.")]
    public float driftFriction = 4.2f;
    [Tooltip("Inclinación corporal lateral (banking/roll) en grados al tomar curvas a velocidad.")]
    public float maxBankAngle = 7.0f;
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

    private EnemyAI ai;
    private float personalXOffset;
    private float personalZOffset;

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

        personalXOffset = Random.Range(-laneWidth * 0.5f, laneWidth * 0.5f);
        personalZOffset = Random.Range(-1.0f, 1.0f);
        lastStuckCheckPosition = transform.position;
        lastWaypointPos = transform.position;

        // Auto-calibrar el BoxCollider para asegurar dimensiones optimas para navegacion sin atascos
        BoxCollider box = GetComponent<BoxCollider>();
        CapsuleCollider cap = GetComponent<CapsuleCollider>();

        if (ai != null && ai.IsPrey)
        {
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

            speed = 4.5f;
            acceleration = 12.0f;
            patrolTurnRate = 220f;
            chaseTurnRate = 380f;
            driftFriction = 5.0f;
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
        }
    }

    void Start()
    {
        if (pathfinding == null) pathfinding = GetComponent<AStarPathfinding>();
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        if (ai == null) ai = GetComponent<EnemyAI>();

        // Asegurar que si el enemigo es líder o autónomo y tiene nodos de patrulla, aparezca exactamente en un nodo
        if (patrolNodes != null && patrolNodes.Count > 0 && patrolNodes[0] != null)
        {
            if (ai == null || ai.isLeader || ai.leaderTransform == null)
            {
                Vector3 nodePos = patrolNodes[0].transform.position;
                transform.position = new Vector3(nodePos.x, transform.position.y, nodePos.z);
            }
        }
        else if (ai != null && !ai.isLeader && ai.leaderTransform != null)
        {
            // Para seguidores, asegurar que aparezcan en su ranura designada sin solaparse
            float distToLeader = Vector3.Distance(transform.position, ai.leaderTransform.position);
            if (distToLeader > 8f || distToLeader < 0.4f)
            {
                int slot = ai.FollowerIndex;
                if (slot < 0) slot = 0;
                Vector3 slotLocal = EnemyAI.GetFormationSlotLocal(slot);
                Vector3 spawnPos = ai.leaderTransform.position + (ai.leaderTransform.rotation * slotLocal);
                spawnPos.y = transform.position.y;
                transform.position = spawnPos;
            }
        }

        // Si no tiene EnemyAI (objeto independiente de prueba como Cube), iniciar patrulla
        if (ai == null && patrolNodes != null && patrolNodes.Count > 0)
        {
            GoToNextPatrolNode();
        }
    }

    void Update()
    {
        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);
        bool isFleeing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.FleeState);

        // El Líder, cualquier enemigo en persecución / huida, o sin líder, se mueve con navegación autónoma
        if (ai == null || ai.isLeader || isChasing || isFleeing || ai.leaderTransform == null)
        {
            FollowPathAStar();

            // Si es un objeto autónomo sin EnemyAI y llegó al destino, pasar automáticamente al siguiente nodo
            if (ai == null && HasReachedDestination && patrolNodes != null && patrolNodes.Count > 0)
            {
                GoToNextPatrolNode();
            }
        }
        else
        {
            // Para seguidores: si están separados del líder (sin línea directa por paredes tras una persecución),
            // usan la red de nodos como guía hasta volver a tenerlo a la vista
            if (ai.leaderTransform != null && !HasClearLineTo(ai.leaderTransform.position) && currentPath != null && currentPath.Count > 0)
            {
                FollowPathAStar();
            }
            else
            {
                // En cuanto tienen línea de visión despejada, siguen la formación en cuña alrededor del Líder
                FollowLeaderDirect();
            }
        }

        // Resolución de solapamiento físico entre dinosaurios (no se pueden atravesar)
        ResolveMutualCollisions();
    }

    void LateUpdate()
    {
        // Garantizar al final de cada frame que los dinosaurios nunca se traspasen
        ResolveMutualCollisions();
    }

    /// <summary>
    /// Comprueba si existe una trayectoria libre de paredes u obstáculos sólidos
    /// entre el dinosaurio y la posición destino (en el plano horizontal a la altura del torso).
    /// </summary>
    public bool HasClearLineTo(Vector3 targetPosition, float clearanceRadius = 0.35f)
    {
        float rayHeight = 0.75f;
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
            if (lineHit.collider != null && !lineHit.collider.transform.IsChildOf(transform) && !lineHit.collider.CompareTag("Enemy"))
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
                if (hit.collider.CompareTag("Enemy")) continue;
                if (ai != null && ai.Player != null && (hit.transform == ai.Player || hit.transform.IsChildOf(ai.Player))) continue;
                if (hit.normal.y >= 0.7f) continue; // Suelo o rampas

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

        // 1. Si está persiguiendo y hay línea directa despejada al objetivo, SALIR de la red de nodos y perseguir directo
        if (isChasing && HasClearLineTo(targetPosition))
        {
            currentPath = null;
            targetIndex = 0;
            float horizontalDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(targetPosition.x, targetPosition.z)
            );
            HasReachedDestination = (horizontalDist <= waypointRadius);
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

    void FollowPathAStar()
    {
        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);

        // 1. Durante la persecución: en cuanto se gire la esquina y haya línea de visión directa al jugador,
        // se abandona la guía de nodos inmediatamente para perseguir de forma directa:
        if (isChasing && hasDirectTarget && HasClearLineTo(directTargetPosition))
        {
            currentPath = null;
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
            // Desacelerar suavemente cuando no hay destino
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, acceleration * Time.deltaTime);
            return;
        }

        Vector3 toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
        float distToWaypoint = toWaypoint.magnitude;
        Vector3 desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;

        // --- COMPROBACIÓN ESTRICTA DE LLEGADA AL NODO (DEBE PASAR POR EL NODO) ---
        Vector3 previousPos = (currentPath != null && targetIndex > 0) ? currentPath[targetIndex - 1] : lastWaypointPos;
        Vector3 segmentDir = new Vector3(targetWaypoint.x - previousPos.x, 0f, targetWaypoint.z - previousPos.z);
        bool hasPassedWaypointPlane = false;

        if (segmentDir.sqrMagnitude > 0.05f)
        {
            Vector3 pastVector = new Vector3(transform.position.x - targetWaypoint.x, 0f, transform.position.z - targetWaypoint.z);
            // Solo se considera superado si está físicamente en el entorno del nodo (<= 1.25x waypointRadius) y cruzó el plano
            hasPassedWaypointPlane = (Vector3.Dot(pastVector, segmentDir.normalized) > 0f) && (distToWaypoint <= waypointRadius * 1.25f);
        }

        bool reachedWaypoint = (distToWaypoint <= waypointRadius) || hasPassedWaypointPlane;

        // Avanzar al siguiente nodo de la ruta ÚNICAMENTE cuando se ha alcanzado físicamente el nodo actual
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
                // Fin de la ruta: se ha llegado al último nodo
                currentPath = null;
                hasDirectTarget = false;
                HasReachedDestination = true;
                return;
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

        // --- FÍSICAS DE DRIFT / DERRAPE NATURAL (INERCIA Y FRICCIÓN LATERAL) ---
        Vector3 forwardDir = transform.forward;
        Vector3 rightDir = transform.right;

        // Proyectar velocidad actual en ejes locales
        float forwardSpeed = Vector3.Dot(currentVelocity, forwardDir);
        float lateralSpeed = Vector3.Dot(currentVelocity, rightDir);

        // Aceleración hacia adelante hacia la velocidad deseada
        forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, acceleration * Time.deltaTime);

        // Derrape lateral: la velocidad lateral no se elimina de golpe,
        // decae con driftFriction permitiendo que el dinosaurio derrape hacia afuera en la curva
        lateralSpeed = Mathf.MoveTowards(lateralSpeed, 0f, driftFriction * Time.deltaTime);

        // Reconstruir vector de velocidad en el plano horizontal
        currentVelocity = (forwardDir * forwardSpeed) + (rightDir * lateralSpeed);

        // --- INCLINACIÓN CORPORAL (BANKING ROLL VISUAL) ---
        float signedTurn = Vector3.SignedAngle(forwardDir, desiredDirection, Vector3.up);
        float targetBank = Mathf.Clamp(-signedTurn * 0.12f, -maxBankAngle, maxBankAngle);
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.deltaTime * 6f);

        // Aplicar el roll sobre la rotación del dinosaurio manteniendo el heading horizontal
        Quaternion horizontalRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = horizontalRot * Quaternion.Euler(0f, 0f, currentBankAngle);

        // --- DESPLAZAMIENTO SEGURO (PAREDES + DINOSAURIOS SÓLIDOS) ---
        Vector3 displacement = currentVelocity * Time.deltaTime;
        float bodyRadius = (ai != null && ai.IsPrey) ? 0.22f : 0.35f;
        MoveSafely(displacement, bodyRadius);
    }

    /// <summary>
    /// Desplaza al dinosaurio resolviendo deslizamiento por paredes y bloqueando penetración contra otros dinosaurios.
    /// </summary>
    private void MoveSafely(Vector3 displacement, float bodyRadius = 0.28f)
    {
        displacement.y = 0f;
        if (displacement.sqrMagnitude < 0.000001f) return;

        float moveDist = displacement.magnitude;
        Vector3 moveDir = displacement / moveDist;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.75f;
        int wallMask = ~((1 << 3) | (1 << 2));

        // 1. Deslizamiento sobre paredes
        if (Physics.SphereCast(rayOrigin, bodyRadius, moveDir, out RaycastHit hit, moveDist + 0.1f, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform) && !hit.collider.CompareTag("Enemy") && hit.normal.y < 0.7f)
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

        // 2. Bloqueo de avance contra otros dinosaurios (No se pueden atravesar)
        if (EnemyAI.AllEnemies != null && EnemyAI.AllEnemies.Count > 0)
        {
            for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
            {
                EnemyAI other = EnemyAI.AllEnemies[i];
                if (other == null || other == ai || !other.gameObject.activeInHierarchy) continue;

                Vector3 toOther = other.transform.position - transform.position;
                toOther.y = 0f;
                float dist = toOther.magnitude;
                float minSeparation = (ai != null && ai.IsPrey && other.IsPrey) ? 0.75f : 1.35f;

                if (dist < minSeparation + 0.35f)
                {
                    Vector3 dirToOther = (dist > 0.001f) ? (toOther / dist) : transform.forward;
                    float approachComponent = Vector3.Dot(displacement, dirToOther);

                    if (approachComponent > 0f)
                    {
                        displacement -= dirToOther * approachComponent;
                    }
                }
            }
        }

        transform.position += displacement;
    }

    /// <summary>
    /// Resuelve de forma física e inmediata cualquier solapamiento entre dinosaurios para impedir que se atraviesen.
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
            float requiredDist = (ai != null && ai.IsPrey && other.IsPrey) ? 0.85f : 1.45f;

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

                // Prioridad: Si uno es el líder y el otro seguidor, el líder manda y el seguidor cede el 100%
                float myWeight = 0.5f;
                if (ai != null && other != null)
                {
                    if (ai.isLeader && !other.isLeader) myWeight = 0.0f;
                    else if (!ai.isLeader && other.isLeader) myWeight = 1.0f;
                }

                Vector3 push = pushDir * (overlap * myWeight);
                if (push.sqrMagnitude > 0.00001f)
                {
                    Vector3 checkOrigin = transform.position + Vector3.up * 0.75f;
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

    /// <summary>
    /// Comprueba la distancia libre lateral antes de colisionar con una pared para adaptar el ancho de la formación.
    /// </summary>
    private float GetSideClearance(Vector3 origin, Vector3 direction, float maxRange = 3.0f)
    {
        int wallMask = ~((1 << 3) | (1 << 2));
        Vector3 rayStart = origin + Vector3.up * 0.75f;
        if (Physics.Raycast(rayStart, direction.normalized, out RaycastHit hit, maxRange, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < 0.7f)
            {
                return hit.distance;
            }
        }
        return maxRange;
    }

    // --- SEGUIMIENTO EN FORMACIÓN (MANADA DE PRESAS ORGÁNICA) ---
    void FollowLeaderDirect()
    {
        if (ai == null || ai.leaderTransform == null) return;

        Transform leader = ai.leaderTransform;
        Vector3 leaderForward = leader.forward;
        Vector3 leaderRight = leader.right;

        // 1. Obtener ranura designada en la cuña / V-formation
        int slotIndex = ai.FollowerIndex;
        if (slotIndex < 0) slotIndex = 0;
        Vector3 slotLocal = EnemyAI.GetFormationSlotLocal(slotIndex);

        // 2. Adaptación orgánica al ancho del pasillo (evita que la formación choque con paredes laterales)
        float leftClearance = GetSideClearance(leader.position, -leaderRight, 3.0f);
        float rightClearance = GetSideClearance(leader.position, leaderRight, 3.0f);

        float clampedX = slotLocal.x;
        if (slotLocal.x < 0f)
        {
            float maxLeft = Mathf.Max(0.45f, leftClearance - 0.45f);
            clampedX = -Mathf.Min(Mathf.Abs(slotLocal.x), maxLeft);
        }
        else if (slotLocal.x > 0f)
        {
            float maxRight = Mathf.Max(0.45f, rightClearance - 0.45f);
            clampedX = Mathf.Min(slotLocal.x, maxRight);
        }

        Vector3 targetFormationPos = leader.position
            + (leaderForward * slotLocal.z)
            + (leaderRight * clampedX);
        targetFormationPos.y = transform.position.y;

        // 3. Separación sutil entre seguidores
        Vector3 separationPush = CalculateSeparationPush();
        Vector3 finalTarget = targetFormationPos + separationPush;

        Vector3 toTarget = finalTarget - transform.position;
        toTarget.y = 0f;
        float distanceToTarget = toTarget.magnitude;

        // 4. Velocidad del líder para sincronizar el paso de la manada
        float leaderCurrentSpeed = (ai.LeaderAI != null && ai.LeaderAI.Movement != null)
            ? ai.LeaderAI.Movement.currentVelocity.magnitude
            : speed;

        bool isLeaderMoving = leaderCurrentSpeed > 0.35f;

        // 5. Dirección y regulación de velocidad de formación
        float targetSpeed = 0f;
        Vector3 desiredDir = leaderForward;

        if (isLeaderMoving)
        {
            HasReachedDestination = false;

            if (distanceToTarget > 1.2f)
            {
                // Quedando atrás: acelerar para mantener la formación compacta
                targetSpeed = Mathf.Max(speed, leaderCurrentSpeed * 1.35f);
                desiredDir = toTarget.normalized;
            }
            else if (distanceToTarget < 0.35f)
            {
                // En posición óptima: mantener la velocidad del líder
                targetSpeed = leaderCurrentSpeed;
                desiredDir = (toTarget.sqrMagnitude > 0.001f) ? Vector3.Lerp(leaderForward, toTarget.normalized, 0.4f) : leaderForward;
            }
            else
            {
                // Ajustando distancia dentro de la ranura
                targetSpeed = Mathf.Lerp(leaderCurrentSpeed * 0.7f, leaderCurrentSpeed * 1.2f, distanceToTarget / 1.2f);
                desiredDir = toTarget.normalized;
            }
        }
        else
        {
            // El líder está quieto / esperando
            if (distanceToTarget > 0.35f)
            {
                HasReachedDestination = false;
                targetSpeed = speed * 0.75f;
                desiredDir = toTarget.normalized;
            }
            else
            {
                HasReachedDestination = true;
                targetSpeed = 0f;
                desiredDir = leaderForward;
                transform.rotation = Quaternion.Slerp(transform.rotation, leader.rotation, Time.deltaTime * 6f);
            }
        }

        // 6. Rotación corporal hacia la dirección deseada
        if (desiredDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, patrolTurnRate * Time.deltaTime);
        }

        // 7. Físicas de inercia y drift natural del seguidor
        Vector3 fwd = transform.forward;
        Vector3 rgt = transform.right;

        float fwdSpeed = Vector3.Dot(currentVelocity, fwd);
        float latSpeed = Vector3.Dot(currentVelocity, rgt);

        fwdSpeed = Mathf.MoveTowards(fwdSpeed, targetSpeed, acceleration * Time.deltaTime);
        latSpeed = Mathf.MoveTowards(latSpeed, 0f, driftFriction * Time.deltaTime);

        currentVelocity = (fwd * fwdSpeed) + (rgt * latSpeed);

        // 8. Inclinación corporal (Banking Roll)
        float signedTurn = Vector3.SignedAngle(fwd, desiredDir, Vector3.up);
        float targetBank = Mathf.Clamp(-signedTurn * 0.12f, -maxBankAngle, maxBankAngle);
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.deltaTime * 6f);
        Quaternion horizontalRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = horizontalRot * Quaternion.Euler(0f, 0f, currentBankAngle);

        // 9. Desplazamiento seguro con colisiones activas (Paredes + Otros dinosaurios)
        Vector3 displacement = currentVelocity * Time.deltaTime;
        float bodyRadius = (ai != null && ai.IsPrey) ? 0.22f : 0.35f;
        MoveSafely(displacement, bodyRadius);
    }

    private Vector3 CalculateSeparationPush()
    {
        Vector3 push = Vector3.zero;
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, leaderGiveWayRadius > 0.1f ? leaderGiveWayRadius : 2.5f);
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
                    float giveWay = leaderGiveWayRadius > 0.1f ? leaderGiveWayRadius : 2.5f;
                    if (dist > 0.001f && dist < giveWay)
                    {
                        Vector3 sidePush = Vector3.Cross(hit.transform.forward, Vector3.up);
                        if (Vector3.Dot(away, sidePush) < 0)
                        {
                            sidePush = -sidePush;
                        }

                        push += sidePush.normalized * (giveWay - dist) * 2.5f;
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
            push = Vector3.ClampMagnitude(push, 2.0f);
        }

        return push;
    }

    public void ClearPath()
    {
        currentPath = null;
        targetIndex = 0;
        hasDirectTarget = false;
        HasReachedDestination = true;
        currentVelocity = Vector3.zero;
    }

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
