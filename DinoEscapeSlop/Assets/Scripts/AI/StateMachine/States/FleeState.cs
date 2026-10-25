using System.Collections.Generic;
using UnityEngine;

public class FleeState : IState
{
    private EnemyAI ai;
    private EnemyAI triggeringPredator;
    private Transform triggeringThreat;
    private Vector3 lastKnownThreatPos;

    private Node currentFleeNode;
    private readonly List<Node> recentlyVisitedFleeNodes = new List<Node>();
    private const int MaxRecentFleeNodes = 6;

    private float outOfRangeTimer;
    private const float RequiredOutOfRangeDuration = 3.0f;

    private float stuckTimer;
    private Vector3 lastStuckCheckPos;
    private const float StuckInterval = 0.35f;
    private const float MinMoveThreshold = 0.15f;

    private float obstacleAvoidCooldown;
    private const float ObstacleAvoidCooldownDuration = 0.25f;

    private float nodeAdvanceCooldown;
    private const float NodeAdvanceCooldownDuration = 0.45f;

    public FleeState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        outOfRangeTimer = 0f;
        stuckTimer = 0f;
        obstacleAvoidCooldown = 0f;
        nodeAdvanceCooldown = 0f;
        lastStuckCheckPos = ai.transform.position;
        currentFleeNode = null;
        recentlyVisitedFleeNodes.Clear();

        triggeringPredator = ai.ChasingPredator;
        triggeringThreat = (triggeringPredator != null) ? triggeringPredator.transform : ai.CurrentThreat;

        if (triggeringPredator != null)
        {
            if (triggeringPredator == ai || triggeringPredator == ai.LeaderAI ||
                triggeringPredator.EffectiveLeader == ai.EffectiveLeader ||
                (triggeringPredator is BlindEnemyAI || triggeringPredator is AmbushEnemyAI || triggeringPredator.dinoEmboscador))
            {
                ai.TriggerPatrol();
                return;
            }

            if (ai.IsMediumDino && !triggeringPredator.IsBigDino)
            {
                ai.TriggerPatrol();
                return;
            }

            if (ai.IsSmallDino && !triggeringPredator.IsMediumDino && !triggeringPredator.IsBigDino)
            {
                ai.TriggerPatrol();
                return;
            }
        }

        if (triggeringThreat != null && ai.IsSmallDino)
        {
            EnemyAI threatAI = triggeringThreat.GetComponentInParent<EnemyAI>();
            if (threatAI != null && (threatAI is BlindEnemyAI || threatAI is AmbushEnemyAI || threatAI.dinoEmboscador))
            {
                ai.TriggerPatrol();
                return;
            }
        }

        if (triggeringThreat != null && triggeringThreat.gameObject.activeInHierarchy)
        {
            lastKnownThreatPos = triggeringThreat.position;
        }
        else if (ai.SpottedThreatPosition != Vector3.zero)
        {
            lastKnownThreatPos = ai.SpottedThreatPosition;
        }
        else
        {
            lastKnownThreatPos = ai.transform.position - ai.transform.forward * 5f;
        }

        ai.SetVisualState(EnemyAI.DinoVisualState.Fleeing);
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.fleeSpeed;
            ai.Movement.ClearPath();
        }

        // Iniciar la huida continua de nodo a nodo
        AdvanceToNextFleeNode();
    }

    public void Update()
    {
        if (ai.CanSeeThreat(out EnemyAI visiblePredator))
        {
            triggeringPredator = visiblePredator;
            triggeringThreat = visiblePredator.transform;
            ai.ChasingPredator = visiblePredator;
            ai.CurrentThreat = visiblePredator.transform;
        }

        Vector3 threatPos = GetThreatPosition();
        Vector3 myPos = ai.transform.position;
        float distToThreat = Vector3.Distance(myPos, threatPos);
        bool threatActive = HasActiveThreat();

        float safeDistance = (ai.SafeRange > 0f) ? ai.SafeRange : (ai.IsSmallDino ? 12f : 16f);

        bool threatDirectlyVisible = false;
        bool threatActivelyChasingMe = false;

        if (triggeringPredator != null && triggeringPredator.gameObject.activeInHierarchy)
        {
            threatDirectlyVisible = ai.CanSeeSpecificDino(triggeringPredator);
            threatActivelyChasingMe = (triggeringPredator.TargetDino == ai || triggeringPredator.CurrentChaseTarget == ai.transform);
        }
        else if (ai.ChasingPredator != null && ai.ChasingPredator.gameObject.activeInHierarchy)
        {
            threatDirectlyVisible = ai.CanSeeSpecificDino(ai.ChasingPredator);
            threatActivelyChasingMe = (ai.ChasingPredator.TargetDino == ai || ai.ChasingPredator.CurrentChaseTarget == ai.transform);
        }
        else if (ai.Player != null && triggeringThreat == ai.Player)
        {
            threatDirectlyVisible = ai.CanSeePlayer();
            threatActivelyChasingMe = false;
        }

        bool isThreatTooClose = threatActive && (distToThreat < 4.5f || threatActivelyChasingMe || (threatDirectlyVisible && distToThreat < safeDistance));

        if (isThreatTooClose)
        {
            outOfRangeTimer = 0f;
            if (ai.Movement != null)
            {
                ai.Movement.speed = ai.fleeSpeed;
            }

            float bodyRadius = ai.IsSmallDino ? 0.28f : (ai.IsBigDino ? 0.85f : 0.50f);

            if (nodeAdvanceCooldown > 0f) nodeAdvanceCooldown -= Time.deltaTime;

            // 1. Pipeline de Nodos: cuando el dinosaurio estÃ¡ a 0.9m o menos del nodo actual,
            // selecciona con control de cadencia el siguiente nodo para mantener inercia continua sin saturar CPU.
            float distToTargetNode = (currentFleeNode != null)
                ? Vector2.Distance(new Vector2(myPos.x, myPos.z), new Vector2(currentFleeNode.transform.position.x, currentFleeNode.transform.position.z))
                : 0f;

            bool arrived = (ai.Movement != null && ai.Movement.HasReachedDestination);
            if (currentFleeNode == null || arrived || (distToTargetNode <= 0.90f && nodeAdvanceCooldown <= 0f))
            {
                nodeAdvanceCooldown = NodeAdvanceCooldownDuration;
                AdvanceToNextFleeNode();
            }

            // 2. Whisker frontal continuo: si una pared u obstÃ¡culo se aproxima a < 1.5m, reorientar al siguiente nodo
            if (obstacleAvoidCooldown > 0f) obstacleAvoidCooldown -= Time.deltaTime;
            else if (IsObstacleAhead(myPos, ai.transform.forward, bodyRadius, 1.5f))
            {
                obstacleAvoidCooldown = ObstacleAvoidCooldownDuration;
                AdvanceToNextFleeNode(forceAlternative: true);
            }

            // 3. Sistema anti-atasco: si no avanza al menos 15cm en 0.35s, forzar siguiente nodo
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= StuckInterval)
            {
                float moved = Vector2.Distance(new Vector2(myPos.x, myPos.z), new Vector2(lastStuckCheckPos.x, lastStuckCheckPos.z));
                if (moved < MinMoveThreshold)
                {
                    AdvanceToNextFleeNode(forceAlternative: true);
                }
                lastStuckCheckPos = myPos;
                stuckTimer = 0f;
            }
        }
        else
        {
            outOfRangeTimer += Time.deltaTime;
            if (outOfRangeTimer >= RequiredOutOfRangeDuration)
            {
                ai.TriggerPatrol();
            }
        }
    }

    public void Exit()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        ai.CurrentThreat = null;
        ai.ChasingPredator = null;
        ai.SpottedThreatPosition = Vector3.zero;
        triggeringPredator = null;
        triggeringThreat = null;
        currentFleeNode = null;
        recentlyVisitedFleeNodes.Clear();

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            ai.Movement.GoToNearestPatrolNode();
        }
    }

    /// <summary>
    /// Avanza de forma ininterrumpida de nodo a nodo en direcciÃ³n contraria al depredador.
    /// Garantiza al 100% que siempre se asigna un nodo destino para que la presa jamÃ¡s se detenga.
    /// </summary>
    private void AdvanceToNextFleeNode(bool forceAlternative = false)
    {
        Vector3 myPos = ai.transform.position;
        Vector3 threatPos = GetThreatPosition();

        Vector3 awayFromThreat = myPos - threatPos;
        awayFromThreat.y = 0f;
        Vector3 fleeDir = (awayFromThreat.sqrMagnitude > 0.001f) ? awayFromThreat.normalized : -ai.transform.forward;
        fleeDir.y = 0f;
        if (fleeDir.sqrMagnitude < 0.001f) fleeDir = Vector3.back;
        fleeDir.Normalize();

        float currentDistToThreat = awayFromThreat.magnitude;
        float bodyRadius = ai.IsSmallDino ? 0.28f : (ai.IsBigDino ? 0.85f : 0.50f);

        if (currentFleeNode != null && !recentlyVisitedFleeNodes.Contains(currentFleeNode))
        {
            recentlyVisitedFleeNodes.Add(currentFleeNode);
            if (recentlyVisitedFleeNodes.Count > MaxRecentFleeNodes)
            {
                recentlyVisitedFleeNodes.RemoveAt(0);
            }
        }

        Node nextNode = SelectFleeNodeWithFallbacks(myPos, threatPos, fleeDir, currentDistToThreat, bodyRadius, forceAlternative);

        if (nextNode != null)
        {
            currentFleeNode = nextNode;
            if (ai.Movement != null)
            {
                ai.Movement.SetCurrentDestinationNode(nextNode);
                ai.Movement.MoveTo(nextNode.transform.position);
            }
        }
    }

    /// <summary>
    /// SelecciÃ³n de nodo en 5 niveles para asegurar que la presa nunca se quede parada:
    /// Nivel 1: Nodo Ã³ptimo por lookahead que aleja en direcciÃ³n opuesta con pasaje libre.
    /// Nivel 2: Mejor vecino despejado que no se dirija frontalmente hacia el depredador.
    /// Nivel 3: Cualquier vecino del nodo actual con pasaje libre.
    /// Nivel 4: Vecino conectado en el grafo que maximice la distancia a la amenaza.
    /// Nivel 5: Nodo mÃ¡s lejano de la escena respecto a la amenaza.
    /// </summary>
    private Node SelectFleeNodeWithFallbacks(Vector3 myPos, Vector3 threatPos, Vector3 fleeDir, float currentDistToThreat, float bodyRadius, bool forceAlternative)
    {
        if (ai.Pathfinding == null) ai.Pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        Node[] allNodes = (ai.Pathfinding != null) ? ai.Pathfinding.GetAllNodes() : null;

        // Recopilar candidatos: vecinos del nodo actual + nodos visibles en pasillo
        List<Node> candidates = new List<Node>();

        if (currentFleeNode != null && currentFleeNode.neighbors != null)
        {
            for (int i = 0; i < currentFleeNode.neighbors.Count; i++)
            {
                Node neighbor = currentFleeNode.neighbors[i];
                if (neighbor != null && !candidates.Contains(neighbor))
                {
                    candidates.Add(neighbor);
                }
            }
        }

        if (allNodes != null)
        {
            for (int i = 0; i < allNodes.Length; i++)
            {
                Node node = allNodes[i];
                if (node == null || candidates.Contains(node)) continue;

                float distToNode = Vector2.Distance(new Vector2(myPos.x, myPos.z), new Vector2(node.transform.position.x, node.transform.position.z));
                if (distToNode <= 14f && CanPassBetweenPoints(myPos, node.transform.position, bodyRadius, ai.transform))
                {
                    candidates.Add(node);
                }
            }
        }

        // --- NIVEL 1: BÃºsqueda Profunda (Lookahead) ---
        Node bestNode = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            Node candidate = candidates[i];
            if (candidate == null) continue;
            if (forceAlternative && candidate == currentFleeNode) continue;

            Vector3 nodePos = candidate.transform.position;
            if (!CanPassBetweenPoints(myPos, nodePos, bodyRadius, ai.transform)) continue;

            float distNodeToThreat = Vector3.Distance(nodePos, threatPos);
            Vector3 toNode = nodePos - myPos;
            toNode.y = 0f;
            if (toNode.sqrMagnitude < 0.01f) continue;

            Vector3 nodeDir = toNode.normalized;
            float dotFlee = Vector3.Dot(nodeDir, fleeDir);

            // Descartar si camina directamente de frente al depredador
            if (dotFlee < -0.35f && distNodeToThreat < currentDistToThreat) continue;

            // Lookahead de ramas de escape (los vecinos del grafo ya tienen conexiÃ³n de pasaje)
            float lookaheadBonus = 0f;
            if (candidate.neighbors != null)
            {
                for (int j = 0; j < candidate.neighbors.Count; j++)
                {
                    Node n2 = candidate.neighbors[j];
                    if (n2 == null || n2 == candidate) continue;
                    float dN2 = Vector3.Distance(n2.transform.position, threatPos);
                    if (dN2 > distNodeToThreat) lookaheadBonus += 8.0f;
                }
            }

            float distGain = distNodeToThreat - currentDistToThreat;
            float score = (distGain * 4.0f) + (dotFlee * 25.0f) + lookaheadBonus;

            if (recentlyVisitedFleeNodes.Contains(candidate))
            {
                score -= 20.0f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestNode = candidate;
            }
        }

        if (bestNode != null) return bestNode;

        // --- NIVEL 2: Cualquier candidato despejado que no acerque al depredador ---
        Node bestLevel2 = null;
        float maxDistLevel2 = -1f;
        for (int i = 0; i < candidates.Count; i++)
        {
            Node candidate = candidates[i];
            if (candidate == null || (forceAlternative && candidate == currentFleeNode)) continue;

            if (CanPassBetweenPoints(myPos, candidate.transform.position, bodyRadius, ai.transform))
            {
                float d = Vector3.Distance(candidate.transform.position, threatPos);
                if (d > maxDistLevel2)
                {
                    maxDistLevel2 = d;
                    bestLevel2 = candidate;
                }
            }
        }

        if (bestLevel2 != null) return bestLevel2;

        // --- NIVEL 3: Cualquier vecino del nodo actual con pasaje libre ---
        if (currentFleeNode != null && currentFleeNode.neighbors != null)
        {
            for (int i = 0; i < currentFleeNode.neighbors.Count; i++)
            {
                Node neighbor = currentFleeNode.neighbors[i];
                if (neighbor != null && neighbor != currentFleeNode && CanPassBetweenPoints(myPos, neighbor.transform.position, bodyRadius, ai.transform))
                {
                    return neighbor;
                }
            }
        }

        // --- NIVEL 4: Vecino conectado en el grafo mÃ¡s lejano al depredador ---
        if (currentFleeNode != null && currentFleeNode.neighbors != null && currentFleeNode.neighbors.Count > 0)
        {
            Node bestNeighbor = null;
            float maxNbrDist = -1f;
            for (int i = 0; i < currentFleeNode.neighbors.Count; i++)
            {
                Node n = currentFleeNode.neighbors[i];
                if (n == null || n == currentFleeNode) continue;
                float d = Vector3.Distance(n.transform.position, threatPos);
                if (d > maxNbrDist)
                {
                    maxNbrDist = d;
                    bestNeighbor = n;
                }
            }
            if (bestNeighbor != null) return bestNeighbor;
        }

        // --- NIVEL 5: Nodo global mÃ¡s lejano de la escena ---
        if (ai.Pathfinding != null)
        {
            Node farthest = ai.Pathfinding.GetFarthestNodeFromPosition(threatPos);
            if (farthest != null) return farthest;
        }

        return (allNodes != null && allNodes.Length > 0) ? allNodes[0] : null;
    }

    /// <summary>
    /// Verifica a altura uniforme horizontal (0.45m) si hay pasaje libre de colisiones entre dos puntos.
    /// Evita falsos positivos por la altura vertical del gizmo de los nodos.
    /// </summary>
    public static bool CanPassBetweenPoints(Vector3 fromPos, Vector3 toPos, float bodyRadius, Transform ignoreTransform = null)
    {
        float testHeight = 0.45f;
        Vector3 origin = new Vector3(fromPos.x, testHeight, fromPos.z);
        Vector3 target = new Vector3(toPos.x, testHeight, toPos.z);
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.35f) return true;

        Vector3 normDir = dir / dist;
        int wallMask = ~((1 << 3) | (1 << 2));

        // 1. Linecast directo horizontal
        if (Physics.Linecast(origin, target, out RaycastHit lineHit, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (IsValidWallObstacle(lineHit, ignoreTransform)) return false;
        }

        // 2. SphereCast con radio de paso
        float castRadius = Mathf.Min(bodyRadius, 0.22f);
        if (dist > castRadius * 2f)
        {
            Vector3 fwdOrigin = origin + normDir * 0.1f;
            float castDist = Mathf.Max(0.05f, dist - 0.2f);
            if (Physics.SphereCast(fwdOrigin, castRadius, normDir, out RaycastHit sphereHit, castDist, wallMask, QueryTriggerInteraction.Ignore))
            {
                if (IsValidWallObstacle(sphereHit, ignoreTransform)) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whisker frontal para detectar esquinas o muros antes de impactar.
    /// </summary>
    private bool IsObstacleAhead(Vector3 pos, Vector3 forward, float radius, float checkDist)
    {
        float rayHeight = 0.45f;
        Vector3 origin = new Vector3(pos.x, rayHeight, pos.z);
        int wallMask = ~((1 << 3) | (1 << 2));

        if (Physics.SphereCast(origin, radius * 0.70f, forward, out RaycastHit hit, checkDist, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (IsValidWallObstacle(hit, ai.transform)) return true;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 leftDir = Quaternion.Euler(0f, -22f, 0f) * forward;
        if (Physics.Raycast(origin - right * (radius * 0.5f), leftDir, out RaycastHit leftHit, checkDist * 0.85f, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (IsValidWallObstacle(leftHit, ai.transform)) return true;
        }

        Vector3 rightDir = Quaternion.Euler(0f, 22f, 0f) * forward;
        if (Physics.Raycast(origin + right * (radius * 0.5f), rightDir, out RaycastHit rightHit, checkDist * 0.85f, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (IsValidWallObstacle(rightHit, ai.transform)) return true;
        }

        return false;
    }

    private static bool IsValidWallObstacle(RaycastHit hit, Transform ignoreTransform)
    {
        if (hit.collider == null) return false;
        if (hit.normal.y >= 0.7f) return false; // Filtra suelos y planos horizontales sin allocs
        if (ignoreTransform != null && (hit.transform == ignoreTransform || hit.transform.IsChildOf(ignoreTransform))) return false;

        if (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null) return false;

        string colName = hit.collider.gameObject.name;
        if (colName.IndexOf("plane", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("suelo", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }

    private Vector3 GetThreatPosition()
    {
        if (triggeringPredator != null && triggeringPredator.gameObject.activeInHierarchy)
        {
            lastKnownThreatPos = triggeringPredator.transform.position;
            return lastKnownThreatPos;
        }

        if (triggeringThreat != null && triggeringThreat.gameObject.activeInHierarchy)
        {
            lastKnownThreatPos = triggeringThreat.position;
            return lastKnownThreatPos;
        }

        if (ai.ChasingPredator != null && ai.ChasingPredator.gameObject.activeInHierarchy)
        {
            lastKnownThreatPos = ai.ChasingPredator.transform.position;
            return lastKnownThreatPos;
        }

        if (ai.CurrentThreat != null && ai.CurrentThreat.gameObject.activeInHierarchy)
        {
            lastKnownThreatPos = ai.CurrentThreat.position;
            return lastKnownThreatPos;
        }

        if (ai.SpottedThreatPosition != Vector3.zero)
        {
            return ai.SpottedThreatPosition;
        }

        return lastKnownThreatPos;
    }

    private bool HasActiveThreat()
    {
        if (triggeringThreat != null && ai.Player != null && triggeringThreat == ai.Player) return true;
        if (ai.CurrentThreat != null && ai.Player != null && ai.CurrentThreat == ai.Player) return true;

        if (ai.IsBigDino) return false;

        EnemyAI threatAI = triggeringPredator ?? ai.ChasingPredator;
        if (threatAI == null && triggeringThreat != null) threatAI = triggeringThreat.GetComponentInParent<EnemyAI>();
        if (threatAI == null && ai.CurrentThreat != null) threatAI = ai.CurrentThreat.GetComponentInParent<EnemyAI>();

        if (threatAI != null && threatAI.gameObject.activeInHierarchy)
        {
            if (threatAI is BlindEnemyAI || threatAI is AmbushEnemyAI || threatAI.dinoEmboscador) return false;
            if (ai.IsMediumDino) return threatAI.IsBigDino;
            if (ai.IsSmallDino) return threatAI.IsMediumDino || threatAI.IsBigDino;
        }

        return false;
    }
}
