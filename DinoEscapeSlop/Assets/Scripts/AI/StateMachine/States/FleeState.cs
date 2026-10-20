using UnityEngine;

/// <summary>
/// Modo Huida (Flee):
/// Los dinosaurios pequeños huyen hacia el nodo más alejado del dinosaurio grande que han visto,
/// asegurando que dicho nodo siempre pueda ser alcanzado directamente mediante Raycast.
/// La huida durará mientras se mantenga en el rango del dinosaurio grande, más 3 segundos continuos fuera de su rango.
/// </summary>
public class FleeState : IState
{
    private EnemyAI ai;
    private float outOfRangeTimer;
    private const float RequiredOutOfRangeDuration = 3.0f; // 3 segundos fuera de rango según especificación
    private Vector3 targetSafePosition;
    private float repathTimer;
    private const float RepathInterval = 0.5f;

    public FleeState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"[{ai.gameObject.name}] ha entrado en MODO HUIDA (Color azul). Huyendo hacia el nodo más alejado alcanzable por Raycast.");
        outOfRangeTimer = 0f;
        repathTimer = RepathInterval;

        // Visual azul para indicar huida
        ai.SetVisualState(EnemyAI.DinoVisualState.Fleeing);

        // Velocidad rápida de sprint de huida (13.0 m/s)
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.fleeSpeed;
            ai.Movement.ClearPath();
        }

        FleeToFarthestNodeFromBigDino();
    }

    /// <summary>
    /// Los pequeños huyen hacia el nodo más alejado del dinosaurio grande que han visto,
    /// asegurando que dicho nodo siempre pueda ser alcanzado directamente mediante Raycast.
    /// </summary>
    private void FleeToFarthestNodeFromBigDino()
    {
        // 1. Obtener la posición del dinosaurio grande observado
        Transform bigDinoTransform = null;
        if (ai.ChasingPredator != null && ai.ChasingPredator.gameObject.activeInHierarchy)
        {
            bigDinoTransform = ai.ChasingPredator.transform;
        }
        else if (ai.CurrentThreat != null && ai.CurrentThreat.gameObject.activeInHierarchy)
        {
            bigDinoTransform = ai.CurrentThreat;
        }

        Vector3 bigDinoPos = (bigDinoTransform != null) ? bigDinoTransform.position : ai.SpottedThreatPosition;

        if (ai.Pathfinding == null)
        {
            ai.Pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        }

        Node[] allNodes = (ai.Pathfinding != null) ? ai.Pathfinding.GetAllNodes() : Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
        if (allNodes == null || allNodes.Length == 0) return;

        Vector3 myPos = ai.transform.position;
        Vector3 toBigDino = bigDinoPos - myPos;
        toBigDino.y = 0f;
        float currentDistToThreat = toBigDino.magnitude;

        // 2. Buscar entre todos los nodos que el pequeño puede ALCANZAR MEDIANTE RAYCAST
        Node farthestRaycastNode = null;
        float maxDistToBigDino = -1f;

        for (int i = 0; i < allNodes.Length; i++)
        {
            Node candidate = allNodes[i];
            if (candidate == null) continue;

            // COMPROBACIÓN OBLIGATORIA: El nodo DEBE poder ser alcanzado directamente mediante Raycast
            bool canReachViaRaycast = (ai.Movement != null) ? ai.Movement.CanMoveToNode(candidate) : IsNodeReachableViaRaycast(candidate);
            if (!canReachViaRaycast) continue;

            float distToBig = Vector3.Distance(candidate.transform.position, bigDinoPos);

            // Evitar elegir nodos que obliguen a correr en el vector frontal directo hacia el dinosaurio grande
            Vector3 toNode = candidate.transform.position - myPos;
            toNode.y = 0f;
            if (toNode.sqrMagnitude > 0.05f && toBigDino.sqrMagnitude > 0.05f)
            {
                float dotToThreat = Vector3.Dot(toNode.normalized, toBigDino.normalized);
                if (dotToThreat > 0.25f && distToBig <= currentDistToThreat)
                {
                    continue;
                }
            }

            // Seleccionar el nodo alcanzable por Raycast que esté más alejado del dinosaurio grande
            if (distToBig > maxDistToBigDino)
            {
                maxDistToBigDino = distToBig;
                farthestRaycastNode = candidate;
            }
        }

        // 3. Ir hacia el nodo alcanzable por Raycast más alejado
        if (farthestRaycastNode != null)
        {
            targetSafePosition = farthestRaycastNode.transform.position;
            Debug.Log($"[{ai.gameObject.name}] (Pequeño) huyendo mediante Raycast al nodo más alejado del grande: '{farthestRaycastNode.name}' (Distancia al grande: {maxDistToBigDino:F1}m)");

            if (ai.Movement != null)
            {
                ai.Movement.MoveTo(targetSafePosition);
            }
        }
        else
        {
            // Fallback 100% verificado por Raycast:
            // Proyectar un Raycast/SphereCast en la dirección opuesta al grande para encontrar el punto más alejado transitable
            Vector3 fleeDir = -toBigDino.normalized;
            if (fleeDir.sqrMagnitude < 0.001f) fleeDir = -ai.transform.forward;

            float safeRayDist = 15f;
            int wallMask = ~((1 << 3) | (1 << 2));
            float rayHeight = (ai != null && ai.IsSmallDino) ? 0.45f : 0.85f;
            Vector3 origin = myPos + Vector3.up * rayHeight;

            if (Physics.SphereCast(origin, 0.20f, fleeDir, out RaycastHit hit, 20f, wallMask, QueryTriggerInteraction.Ignore))
            {
                safeRayDist = Mathf.Max(1.0f, hit.distance - 0.6f);
            }

            targetSafePosition = myPos + fleeDir * safeRayDist;

            if (ai.Movement != null)
            {
                ai.Movement.MoveTo(targetSafePosition);
            }
        }
    }

    private bool IsNodeReachableViaRaycast(Node node)
    {
        if (node == null) return false;
        if (ai.Movement != null) return ai.Movement.CanMoveToNode(node);

        Vector3 eyePos = ai.transform.position + Vector3.up * 0.45f;
        Vector3 targetPos = node.transform.position + Vector3.up * 0.45f;
        int wallMask = ~((1 << 3) | (1 << 2));

        if (Physics.Linecast(eyePos, targetPos, out RaycastHit hit, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < 0.7f) return false;
        }
        return true;
    }

    public void Update()
    {
        // 1. Determinar el rango de visión/detección del dinosaurio grande
        float threatRange = (ai.ChasingPredator != null) ? ai.ChasingPredator.visionRange : 8.5f;

        // 2. Determinar la distancia física actual hacia la amenaza
        float distToThreat = float.MaxValue;
        bool threatActive = false;

        if (ai.ChasingPredator != null && ai.ChasingPredator.gameObject.activeInHierarchy)
        {
            distToThreat = Vector3.Distance(ai.transform.position, ai.ChasingPredator.transform.position);
            threatActive = true;
        }
        else if (ai.CurrentThreat != null && ai.CurrentThreat.gameObject.activeInHierarchy)
        {
            distToThreat = Vector3.Distance(ai.transform.position, ai.CurrentThreat.position);
            threatActive = true;
        }

        // 3. Comprobar si se encuentra dentro o fuera del rango del dinosaurio grande
        bool isInsideThreatRange = threatActive && (distToThreat <= threatRange);

        if (isInsideThreatRange)
        {
            // Dentro del rango: reiniciar el temporizador de salida
            outOfRangeTimer = 0f;

            // Al llegar al nodo o periódicamente, seleccionar el siguiente nodo alcanzable por Raycast más alejado
            repathTimer -= Time.deltaTime;
            bool reachedDestination = (ai.Movement != null && ai.Movement.HasReachedDestination);

            if (reachedDestination || repathTimer <= 0f)
            {
                repathTimer = RepathInterval;
                FleeToFarthestNodeFromBigDino();
            }
        }
        else
        {
            // Fuera del rango: acumular tiempo
            outOfRangeTimer += Time.deltaTime;

            // La huida dura hasta salir del rango del dinosaurio grande más 3 segundos
            if (outOfRangeTimer >= RequiredOutOfRangeDuration)
            {
                Debug.Log($"[{ai.gameObject.name}] ha salido del rango del dinosaurio grande ({threatRange:F1}m) + {RequiredOutOfRangeDuration}s completados. Regresando a patrulla.");
                EndFlee();
                return;
            }
        }
    }

    private void EndFlee()
    {
        ai.TriggerPatrol();
    }

    public void Exit()
    {
        // Restaurar aspecto normal del dinosaurio
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        ai.CurrentThreat = null;
        ai.ChasingPredator = null;
        ai.SpottedThreatPosition = Vector3.zero;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            ai.Movement.GoToNearestPatrolNode();
        }
    }
}
