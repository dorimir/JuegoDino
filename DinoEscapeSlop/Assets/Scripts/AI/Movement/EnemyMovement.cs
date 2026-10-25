using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private AStarPathfinding pathfinding;
    private List<Vector3> currentPath;
    private int targetIndex;
    private EnemyAI ai;
    private Vector3 directTargetPosition;
    private bool hasDirectTarget;

    [Header("ConfiguraciÃ³n de Velocidad")]
    public float speed = 3.2f;
    public float acceleration = 9.0f;

    [Header("Comportamiento de Giro y Drift")]
    public float patrolTurnRate = 180f;
    public float chaseTurnRate = 340f;
    public float driftFriction = 35.0f;
    public float maxBankAngle = 1.5f;
    public float waypointRadius = 0.75f;

    [Header("Ajuste a Ras de Suelo")]

    public float groundCheckDistance = 10.0f;

    public float groundOffset = 0.0f;

    public float groundSnapSpeed = 25.0f;

    [Header("Nodos de Patrulla")]
    public List<Node> patrolNodes = new List<Node>();

    private Vector3 currentVelocity = Vector3.zero;
    private float currentBankAngle;
    private Vector3 lastWaypointPos;

    private Vector3 lastStuckCheckPosition;
    private float stuckTimer;
    private const float StuckCheckInterval = 0.8f;
    private const float MinMoveThreshold = 0.15f;

    private int currentPatrolIndex = -1;
    private readonly List<Node> recentlyVisitedNodes = new List<Node>();
    private const int MaxRecentNodesHistory = 14;
    private Node currentDestinationNode;
    public Node CurrentDestinationNode => currentDestinationNode;

    public float CurrentForwardSpeed => currentVelocity.magnitude;
    public Vector3 Velocity => currentVelocity;
    public bool HasReachedDestination { get; private set; } = true;
    public Vector3 SpawnPosition { get; private set; }
    public Quaternion SpawnRotation { get; private set; }

    public void ResetVelocity()
    {
        currentVelocity = Vector3.zero;
        currentBankAngle = 0f;
    }

    void Awake()
    {
        pathfinding = GetComponent<AStarPathfinding>();
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        ai = GetComponent<EnemyAI>();
        HasReachedDestination = true;
        SpawnPosition = transform.position;
        SpawnRotation = transform.rotation;

        lastStuckCheckPosition = transform.position;
        lastWaypointPos = transform.position;

        if (patrolNodes != null)
        {
            patrolNodes.RemoveAll(n => n == null);
        }

        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);

        if (isSmall)
        {
            patrolNodes.Clear();
            speed = 4.8f;
            acceleration = 14.0f;
            patrolTurnRate = 260f;
            chaseTurnRate = 420f;
            driftFriction = 45.0f;
            maxBankAngle = 1.5f;
            waypointRadius = 0.55f;
        }
        else if (isBig)
        {
            speed = 1.8f;
            acceleration = 6.5f;
            patrolTurnRate = 130f;
            chaseTurnRate = 240f;
            driftFriction = 28.0f;
            maxBankAngle = 1.0f;
            waypointRadius = 1.10f;
        }
        else
        {
            speed = 2.4f;
            acceleration = 9.0f;
            patrolTurnRate = 180f;
            chaseTurnRate = 340f;
            driftFriction = 35.0f;
            maxBankAngle = 1.5f;
            waypointRadius = 0.75f;
        }

        EnemyCollisionHandler.ConfigureColliders(gameObject, transform, isSmall, isBig);
    }

    void Start()
    {
        SnapToNode();
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        if (ai == null) ai = GetComponent<EnemyAI>();

        if (patrolNodes != null && patrolNodes.Count > 0)
        {
            int foundIdx = patrolNodes.FindIndex(n => n == currentDestinationNode);
            if (foundIdx >= 0)
            {
                currentPatrolIndex = foundIdx;
            }
        }

        if (HasReachedDestination)
        {
            GoToNextPatrolNode();
        }
    }

    void Update()
    {
        FollowPathAStar();

        if (ai == null && HasReachedDestination)
        {
            GoToNextPatrolNode();
        }

        EnemyCollisionHandler.ResolveMutualCollisions(ai, transform, GetInstanceID());
        SnapToGround(false);
    }

    public bool HasClearLineTo(Vector3 targetPosition, float clearanceRadius = -1f)
    {
        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);
        if (clearanceRadius < 0f) clearanceRadius = isSmall ? 0.20f : (isBig ? 0.55f : 0.35f);

        float rayHeight = isSmall ? 0.45f : (isBig ? 1.45f : 0.85f);
        Vector3 origin = new Vector3(transform.position.x, transform.position.y + rayHeight, transform.position.z);
        float targetY = (Mathf.Abs(targetPosition.y - transform.position.y) > 0.5f) ? targetPosition.y + rayHeight : transform.position.y + rayHeight;
        Vector3 target = new Vector3(targetPosition.x, targetY, targetPosition.z);
        Vector3 dir = target - origin;
        float dist = dir.magnitude;

        if (dist <= 0.4f) return true;

        int wallLayerMask = ~((1 << 3) | (1 << 2));

        if (Physics.Linecast(origin, target, out RaycastHit lineHit, wallLayerMask, QueryTriggerInteraction.Ignore))
        {
            bool isEnemy = lineHit.collider != null && (lineHit.collider.CompareTag("Enemy") || lineHit.collider.GetComponentInParent<EnemyAI>() != null);
            if (lineHit.collider != null && !lineHit.collider.transform.IsChildOf(transform) && !isEnemy)
            {
                bool isTargetPlayer = (ai != null && ai.Player != null && (lineHit.transform == ai.Player || lineHit.transform.IsChildOf(ai.Player)));
                if (!isTargetPlayer && lineHit.normal.y < 0.7f)
                {
                    string colName = lineHit.collider.gameObject.name.ToLower();
                    if (!colName.Contains("plane") && !colName.Contains("floor") && !colName.Contains("suelo"))
                    {
                        return false;
                    }
                }
            }
        }

        if (dist > clearanceRadius * 2f)
        {
            Vector3 fwdOrigin = origin + dir.normalized * 0.15f;
            float castDist = Mathf.Max(0.05f, dist - 0.3f);

            RaycastHit[] hits = Physics.SphereCastAll(fwdOrigin, clearanceRadius, dir.normalized, castDist, wallLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null) continue;
                if (ai != null && ai.Player != null && (hit.transform == ai.Player || hit.transform.IsChildOf(ai.Player))) continue;
                if (hit.normal.y >= 0.7f) continue;

                string colName = hit.collider.gameObject.name.ToLower();
                if (colName.Contains("plane") || colName.Contains("floor") || colName.Contains("suelo")) continue;

                return false;
            }
        }

        return true;
    }

    public bool CanMoveToNode(Node node)
    {
        if (node == null) return false;
        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);
        float clearance = isSmall ? 0.15f : (isBig ? 0.40f : 0.22f);
        return HasClearLineTo(node.transform.position, clearance);
    }

    public List<Node> GetReachableNodes()
    {
        List<Node> reachable = new List<Node>();
        Node[] allNodes = (pathfinding != null) ? pathfinding.GetAllNodes() : Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
        if (allNodes == null) return reachable;

        foreach (Node node in allNodes)
        {
            if (node != null && CanMoveToNode(node)) reachable.Add(node);
        }
        return reachable;
    }

    public List<Node> GetReachablePatrolNodes()
    {
        List<Node> reachable = new List<Node>();
        if (patrolNodes == null) return reachable;

        foreach (Node node in patrolNodes)
        {
            if (node != null && CanMoveToNode(node)) reachable.Add(node);
        }
        return reachable;
    }

    public void MoveTo(Vector3 targetPosition)
    {
        directTargetPosition = targetPosition;
        hasDirectTarget = true;
        HasReachedDestination = false;
        lastStuckCheckPosition = transform.position;
        stuckTimer = 0f;

        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);
        bool isFollower = (ai != null && ai.IsSmallDino && ai.LeaderAI != null);
        bool isFleeing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.FleeState);

        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);
        float clearance = isSmall ? 0.20f : (isBig ? 0.50f : 0.35f);

        if (HasClearLineTo(targetPosition, clearance))
        {
            currentPath = null;
            targetIndex = 0;
            float horizontalDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(targetPosition.x, targetPosition.z)
            );
            float stopDist = isChasing ? 0.15f : (isFollower ? 0.35f : (isFleeing ? 0.35f : waypointRadius));
            HasReachedDestination = (horizontalDist <= stopDist);
            lastWaypointPos = transform.position;
            return;
        }

        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();

        if (pathfinding != null)
        {
            CancelInvoke();

            currentPath = pathfinding.FindPath(transform.position, targetPosition, clearance);

            if (currentPath != null && currentPath.Count > 0)
            {
                targetIndex = 0;
                if (currentPath.Count > 1)
                {
                    Vector3 toFirst = currentPath[0] - transform.position;
                    toFirst.y = 0f;
                    float distToFirst = toFirst.magnitude;

                    Vector3 toSecond = currentPath[1] - transform.position;
                    toSecond.y = 0f;

                    bool firstIsBehind = (distToFirst > 0.05f) && (Vector3.Dot(transform.forward, toFirst.normalized) < 0.1f);
                    bool clearToSecond = HasClearLineTo(currentPath[1]);

                    if (distToFirst <= 0.8f && clearToSecond)
                    {
                        targetIndex = 1;
                    }
                    else if (firstIsBehind && clearToSecond)
                    {
                        targetIndex = 1;
                    }
                    else if (clearToSecond && distToFirst <= 1.5f && toSecond.sqrMagnitude < toFirst.sqrMagnitude)
                    {
                        targetIndex = 1;
                    }
                    else if (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.FleeState)
                    {
                        Vector3 threatPos = (ai.ChasingPredator != null) ? ai.ChasingPredator.transform.position :
                                            ((ai.CurrentThreat != null) ? ai.CurrentThreat.position : ai.SpottedThreatPosition);
                        if (Vector3.Distance(currentPath[0], threatPos) < Vector3.Distance(transform.position, threatPos))
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

    public void SetDirectChaseTarget(Vector3 targetPos)
    {
        directTargetPosition = targetPos;
        hasDirectTarget = true;
        currentPath = null;
        targetIndex = 0;
        HasReachedDestination = false;
        lastWaypointPos = transform.position;
    }

    private void FollowPathAStar()
    {
        bool isChasing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.ChaseState);
        bool isFollower = (ai != null && ai.IsSmallDino && ai.LeaderAI != null);
        bool isFleeing = (ai != null && ai.StateMachine != null && ai.StateMachine.CurrentState == ai.FleeState);
        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);
        float clearance = isSmall ? 0.20f : (isBig ? 0.50f : 0.35f);

        if (hasDirectTarget && HasClearLineTo(directTargetPosition, clearance))
        {
            currentPath = null;
        }
        else if (currentPath != null && currentPath.Count > 0 && targetIndex < currentPath.Count)
        {

            for (int i = currentPath.Count - 1; i > targetIndex; i--)
            {
                if (HasClearLineTo(currentPath[i], clearance))
                {
                    targetIndex = i;
                    lastWaypointPos = transform.position;
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
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, acceleration * Time.deltaTime);
            return;
        }

        Vector3 toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
        float distToWaypoint = toWaypoint.magnitude;
        Vector3 desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;

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
            float stopDist = isChasing ? 0.15f : (isFollower ? 0.35f : waypointRadius);
            reachedWaypoint = (distToWaypoint <= stopDist);
        }

        if (reachedWaypoint)
        {
            lastWaypointPos = targetWaypoint;
            targetIndex++;

            if (currentPath != null && targetIndex < currentPath.Count)
            {
                targetWaypoint = currentPath[targetIndex];
                toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
                distToWaypoint = toWaypoint.magnitude;
                desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;
            }
            else
            {
                currentPath = null;
                if (isChasing || isFollower || isFleeing)
                {
                    float stopDist = isChasing ? 0.15f : (isFollower ? 0.35f : (isFleeing ? 0.5f : waypointRadius));
                    HasReachedDestination = (distToWaypoint <= stopDist);
                    if (HasReachedDestination)
                    {
                        if (!isFleeing) hasDirectTarget = false;
                        return;
                    }
                }
                else
                {
                    hasDirectTarget = false;

                    GoToNextPatrolNode();

                    if (currentPath != null && currentPath.Count > 0 && targetIndex < currentPath.Count)
                    {
                        targetWaypoint = currentPath[targetIndex];
                        toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
                        distToWaypoint = toWaypoint.magnitude;
                        desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;
                    }
                    else if (hasDirectTarget)
                    {
                        targetWaypoint = directTargetPosition;
                        toWaypoint = new Vector3(targetWaypoint.x - transform.position.x, 0f, targetWaypoint.z - transform.position.z);
                        distToWaypoint = toWaypoint.magnitude;
                        desiredDirection = distToWaypoint > 0.001f ? toWaypoint.normalized : transform.forward;
                    }
                    else
                    {
                        HasReachedDestination = true;
                        return;
                    }
                }
            }
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer >= StuckCheckInterval)
        {
            float movedDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(lastStuckCheckPosition.x, lastStuckCheckPosition.z)
            );

            if (movedDist < MinMoveThreshold && !HasReachedDestination)
            {
                if (currentPath != null && targetIndex + 1 < currentPath.Count)
                {
                    targetIndex++;
                    lastWaypointPos = transform.position;
                }
                else
                {
                    GoToNextPatrolNode();
                }

                int wallMask = ~((1 << 3) | (1 << 2));
                Vector3 unstuckDir = desiredDirection;
                if (!Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.2f, unstuckDir, out _, 0.25f, wallMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position += unstuckDir * 0.15f;
                }
            }

            lastStuckCheckPosition = transform.position;
            stuckTimer = 0f;
        }

        desiredDirection = ApplyWhiskerAvoidance(desiredDirection, isSmall, out float wallAvoidanceUrgency);

        float turnAngle = Vector3.Angle(transform.forward, desiredDirection);
        float speedMultiplier = (!isChasing && turnAngle > 45f) ? Mathf.Lerp(0.7f, 1.0f, Mathf.Cos(turnAngle * Mathf.Deg2Rad)) : 1.0f;

        if (wallAvoidanceUrgency > 0.15f)
        {
            speedMultiplier *= Mathf.Lerp(1.0f, 0.45f, (wallAvoidanceUrgency - 0.15f) / 0.85f);
        }

        float targetSpeed = speed * speedMultiplier;

        float currentTurnRate = (isChasing || isFleeing) ? chaseTurnRate : patrolTurnRate;

        float effectiveTurnRate = Mathf.Lerp(currentTurnRate, Mathf.Max(currentTurnRate, 340f), wallAvoidanceUrgency);

        if (desiredDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, effectiveTurnRate * Time.deltaTime);
        }

        Vector3 forwardDir = transform.forward;
        Vector3 rightDir = transform.right;

        float forwardSpeed = Vector3.Dot(currentVelocity, forwardDir);
        float lateralSpeed = Vector3.Dot(currentVelocity, rightDir);

        forwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, acceleration * Time.deltaTime);
        lateralSpeed = Mathf.MoveTowards(lateralSpeed, 0f, Mathf.Max(driftFriction, 45.0f) * Time.deltaTime);

        currentVelocity = (forwardDir * forwardSpeed) + (rightDir * (lateralSpeed * 0.05f));

        float signedTurn = Vector3.SignedAngle(forwardDir, desiredDirection, Vector3.up);
        float targetBank = Mathf.Clamp(-signedTurn * 0.12f, -maxBankAngle, maxBankAngle);
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.deltaTime * 6f);

        Quaternion horizontalRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = horizontalRot * Quaternion.Euler(0f, 0f, currentBankAngle);

        Vector3 displacement = currentVelocity * Time.deltaTime;
        float bodyRadius = isSmall ? 0.28f : (isBig ? 0.85f : 0.50f);
        EnemyCollisionHandler.MoveSafely(ai, transform, ref currentVelocity, displacement, bodyRadius);
        SnapToGround(false);
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

    public Node GetNextNetworkPatrolNode()
    {
        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();

        Node currentNode = (currentDestinationNode != null) ? currentDestinationNode : GetNearestPatrolOrGraphNode();
        if (currentNode == null) return null;

        if (!recentlyVisitedNodes.Contains(currentNode))
        {
            recentlyVisitedNodes.Add(currentNode);
            if (recentlyVisitedNodes.Count > MaxRecentNodesHistory)
            {
                recentlyVisitedNodes.RemoveAt(0);
            }
        }

        if (currentNode.neighbors != null && currentNode.neighbors.Count > 0)
        {
            List<Node> validNeighbors = currentNode.neighbors.FindAll(n => n != null && CanMoveToNode(n));
            if (validNeighbors.Count == 0) validNeighbors = currentNode.neighbors.FindAll(n => n != null);

            if (validNeighbors.Count > 0)
            {
                List<Node> unvisitedNeighbors = validNeighbors.FindAll(n => !recentlyVisitedNodes.Contains(n));
                if (unvisitedNeighbors.Count > 0)
                {
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

        Node[] allNodes = (pathfinding != null) ? pathfinding.GetAllNodes() : null;
        if (allNodes != null)
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

                if (CanMoveToNode(patrolNodes[i]) && d < minReachableDist)
                {
                    minReachableDist = d;
                    nearestReachable = patrolNodes[i];
                    nearestReachableIdx = i;
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

        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        return pathfinding != null ? pathfinding.GetClosestNode(transform.position) : null;
    }

    public void GoToNearestPatrolNode()
    {
        Node nearest = GetNearestPatrolOrGraphNode();
        if (nearest != null) MoveTo(nearest.transform.position);
        else GoToNextPatrolNode();
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
                if (randNeighbor != null) MoveTo(randNeighbor.transform.position);
            }
        }
    }

    public void SnapToNode()
    {
        if (ai == null) ai = GetComponent<EnemyAI>();
        if (ai != null && (ai.dinoEmboscador || ai is AmbushEnemyAI)) return;

        if (pathfinding == null) pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        Node spawnNode = (pathfinding != null) ? pathfinding.GetClosestNode(transform.position) : null;

        if (spawnNode != null)
        {

            transform.position = spawnNode.transform.position;
            SpawnPosition = spawnNode.transform.position;

            if (spawnNode.neighbors != null && spawnNode.neighbors.Count > 0 && spawnNode.neighbors[0] != null)
            {
                Vector3 fwd = spawnNode.neighbors[0].transform.position - transform.position;
                fwd.y = 0;
                if (fwd.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                }
            }
            SpawnRotation = transform.rotation;

            SetCurrentDestinationNode(spawnNode);
        }
    }

    public Vector3 ApplyWhiskerAvoidance(Vector3 desiredDir, bool isSmall)
    {
        return ApplyWhiskerAvoidance(desiredDir, isSmall, out _);
    }

    public Vector3 ApplyWhiskerAvoidance(Vector3 desiredDir, bool isSmall, out float wallAvoidanceUrgency)
    {
        wallAvoidanceUrgency = 0f;
        if (desiredDir.sqrMagnitude < 0.0001f) return desiredDir;

        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);
        float bodyRadius = isSmall ? 0.28f : (isBig ? 0.85f : 0.50f);
        float rayHeight = isSmall ? 0.45f : (isBig ? 1.45f : 0.85f);
        Vector3 origin = transform.position + Vector3.up * rayHeight;

        float curSpeed = Mathf.Max(speed, currentVelocity.magnitude);
        float baseLookahead = isSmall ? 1.35f : (isBig ? 2.65f : 1.95f);
        float forwardLookahead = Mathf.Clamp(baseLookahead + (curSpeed * 0.38f), 1.35f, 4.5f);
        int wallLayerMask = ~((1 << 3) | (1 << 2));

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 refHeading = (forward * 0.6f + desiredDir.normalized * 0.4f).normalized;
        if (refHeading.sqrMagnitude < 0.001f) refHeading = forward;
        Vector3 refRight = Vector3.Cross(Vector3.up, refHeading).normalized;

        Vector3 avoidanceForce = Vector3.zero;
        float maxUrgency = 0f;

        float frontalSphereRadius = bodyRadius * 0.55f;
        if (Physics.SphereCast(origin, frontalSphereRadius, refHeading, out RaycastHit centerHit, forwardLookahead, wallLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (IsValidWallObstacle(centerHit))
            {
                float hitDist = Mathf.Max(0.02f, centerHit.distance);
                float urgency = 1.0f - Mathf.Clamp01(hitDist / forwardLookahead);

                float weight = urgency * urgency;
                maxUrgency = Mathf.Max(maxUrgency, urgency);

                Vector3 wallNormal = new Vector3(centerHit.normal.x, 0f, centerHit.normal.z).normalized;
                if (wallNormal.sqrMagnitude < 0.001f) wallNormal = -refHeading;

                Vector3 tangentA = Vector3.Cross(Vector3.up, wallNormal).normalized;
                Vector3 tangentB = -tangentA;

                Vector3 chosenTangent;
                float dotA = Vector3.Dot(desiredDir, tangentA);
                float dotB = Vector3.Dot(desiredDir, tangentB);

                if (Mathf.Abs(dotA - dotB) > 0.05f)
                {
                    chosenTangent = (dotA > dotB) ? tangentA : tangentB;
                }
                else
                {

                    chosenTangent = (Vector3.Dot(tangentA, refRight) > 0f) ? tangentA : tangentB;
                }

                Vector3 steerDir = (chosenTangent * 1.15f + wallNormal * 0.85f).normalized;
                avoidanceForce += steerDir * (weight * 2.4f);
            }
        }

        float[] probeAngles = new float[] { -26f, 26f, -55f, 55f };
        float[] probeLengths = new float[] {
            forwardLookahead * 0.90f,
            forwardLookahead * 0.90f,
            forwardLookahead * 0.65f,
            forwardLookahead * 0.65f
        };
        float[] probeWeights = new float[] { 1.4f, 1.4f, 1.0f, 1.0f };

        float leftMinDist = float.MaxValue;
        float rightMinDist = float.MaxValue;

        for (int i = 0; i < probeAngles.Length; i++)
        {
            float angle = probeAngles[i];
            float length = probeLengths[i];
            float pWeight = probeWeights[i];

            Vector3 probeDir = Quaternion.Euler(0f, angle, 0f) * refHeading;

            if (Physics.Raycast(origin, probeDir, out RaycastHit hit, length, wallLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (IsValidWallObstacle(hit))
                {
                    float hitDist = Mathf.Max(0.02f, hit.distance);
                    float urgency = 1.0f - Mathf.Clamp01(hitDist / length);
                    float weight = urgency * urgency * pWeight;
                    maxUrgency = Mathf.Max(maxUrgency, urgency);

                    if (angle < 0f && hitDist < leftMinDist) leftMinDist = hitDist;
                    else if (angle > 0f && hitDist < rightMinDist) rightMinDist = hitDist;

                    Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                    if (wallNormal.sqrMagnitude < 0.001f) wallNormal = (angle < 0f) ? refRight : -refRight;

                    Vector3 lateralPush = (angle < 0f) ? refRight : -refRight;
                    Vector3 probeSteer = (wallNormal * 0.75f + lateralPush * 0.55f).normalized;
                    avoidanceForce += probeSteer * weight;
                }
            }
        }

        if (leftMinDist < float.MaxValue || rightMinDist < float.MaxValue)
        {
            if (leftMinDist < rightMinDist)
            {
                float bias = 1.0f - Mathf.Clamp01(leftMinDist / forwardLookahead);
                avoidanceForce += refRight * (bias * 0.7f);
            }
            else if (rightMinDist < leftMinDist)
            {
                float bias = 1.0f - Mathf.Clamp01(rightMinDist / forwardLookahead);
                avoidanceForce -= refRight * (bias * 0.7f);
            }
        }

        wallAvoidanceUrgency = maxUrgency;

        if (avoidanceForce.sqrMagnitude > 0.001f)
        {

            float blendWeight = Mathf.Clamp01(maxUrgency * 1.5f);
            Vector3 corrected = Vector3.Lerp(desiredDir.normalized, avoidanceForce.normalized, blendWeight);
            corrected.y = 0f;
            if (corrected.sqrMagnitude > 0.001f)
            {
                return corrected.normalized;
            }
        }

        return desiredDir;
    }

    private bool IsValidWallObstacle(RaycastHit hit)
    {
        if (hit.collider == null) return false;
        if (hit.collider.isTrigger) return false;
        if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform)) return false;
        if (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null) return false;
        if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<FirstPersonMovement>() != null) return false;
        if (hit.collider.CompareTag("canPickUp")) return false;
        if (ai != null && ai.Player != null && (hit.transform == ai.Player || hit.transform.IsChildOf(ai.Player))) return false;
        if (hit.normal.y >= 0.65f) return false;

        string name = hit.collider.gameObject.name.ToLower();
        if (name.Contains("plane") || name.Contains("floor") || name.Contains("suelo")) return false;

        return true;
    }

    public void SnapToGround(bool instant = false)
    {
        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);
        float rayHeight = isSmall ? 0.8f : (isBig ? 2.2f : 1.5f);
        Vector3 rayStart = new Vector3(transform.position.x, transform.position.y + rayHeight, transform.position.z);
        float maxDist = rayHeight + 4.5f;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, maxDist, ~((1 << 2) | (1 << 3)), QueryTriggerInteraction.Ignore);
        RaycastHit bestHit = default;
        bool foundGround = false;
        float highestGroundY = float.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform)) continue;
            if (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null) continue;
            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<FirstPersonMovement>() != null) continue;
            if (hit.collider.CompareTag("canPickUp")) continue;

            if (hit.normal.y >= 0.35f)
            {
                if (hit.point.y <= rayStart.y + 0.1f && hit.point.y > highestGroundY)
                {
                    highestGroundY = hit.point.y;
                    bestHit = hit;
                    foundGround = true;
                }
            }
        }

        if (foundGround)
        {
            float targetY = bestHit.point.y;
            Vector3 pos = transform.position;
            if (instant)
            {
                pos.y = targetY;
            }
            else
            {
                pos.y = Mathf.MoveTowards(pos.y, targetY, Mathf.Max(groundSnapSpeed, 25f) * Time.deltaTime);
            }
            transform.position = pos;
        }
    }
}
