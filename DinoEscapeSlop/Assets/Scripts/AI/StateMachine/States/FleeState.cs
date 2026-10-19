using UnityEngine;

public class FleeState : IState
{
    private EnemyAI ai;
    private Node previousNode;
    private float reevaluateTimer;
    private float totalFleeTimer;

    private const float ReevaluateInterval = 0.35f;
    private const float BacktrackPenalty = 5f;
    private const float MaxFleeDuration = 10.0f; // 10 segundos exactos

    public FleeState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"{ai.gameObject.name} entra en estado: FLEE (Color azul activado, huyendo de depredador)");
        previousNode = null;
        reevaluateTimer = 0f; // Elige ruta de huida inmediatamente
        totalFleeTimer = 0f;

        // Poner al dinosaurio azul para indicar el estado de huida
        ai.SetVisualState(EnemyAI.DinoVisualState.Fleeing);

        // Velocidad rápida para huir
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.fleeSpeed;
        }
    }

    public void Update()
    {
        totalFleeTimer += Time.deltaTime;

        // Regla 1: Huir durante un máximo de 10 segundos
        // "toda la manada huira durante 10 segundos o hasta que el dinosaurio grande deje de perseguirlos"
        if (totalFleeTimer >= MaxFleeDuration)
        {
            Debug.Log($"{ai.gameObject.name}: Han transcurrido 10 segundos de huida. Regresando a patrulla.");
            EndFlee();
            return;
        }

        // Regla 2: "o hasta que el dinosaurio grande deje de perseguirlos"
        if (ai.CurrentThreat == null || !ai.CurrentThreat.gameObject.activeInHierarchy)
        {
            EndFlee();
            return;
        }

        bool predatorIsActivelyChasing = false;
        if (ai.ChasingPredator != null && ai.ChasingPredator.gameObject.activeInHierarchy)
        {
            bool isPredatorChasing = (ai.ChasingPredator.StateMachine != null &&
                                      ai.ChasingPredator.StateMachine.CurrentState == ai.ChasingPredator.ChaseState);

            if (isPredatorChasing)
            {
                Transform predTarget = ai.ChasingPredator.CurrentChaseTarget;
                if (predTarget == ai.transform || (ai.LeaderAI != null && predTarget == ai.LeaderAI.transform))
                {
                    predatorIsActivelyChasing = true;
                }
                else if (ai.isLeader)
                {
                    for (int i = 0; i < ai.packFollowers.Count; i++)
                    {
                        var follower = ai.packFollowers[i];
                        if (follower != null && predTarget == follower.transform)
                        {
                            predatorIsActivelyChasing = true;
                            break;
                        }
                    }
                }
            }
        }

        // Si el depredador NO está persiguiendo activamente a la manada (o ya desistió):
        float distanceToThreat = Vector3.Distance(ai.transform.position, ai.CurrentThreat.position);
        if (!predatorIsActivelyChasing)
        {
            // Si el dinosaurio grande ha dejado de perseguirlos y han huido al menos 2 segundos
            // o ya están a distancia segura:
            if (distanceToThreat > ai.SafeRange || totalFleeTimer >= 2.5f)
            {
                Debug.Log($"{ai.gameObject.name}: El dinosaurio grande ya no persigue a la manada (distancia: {distanceToThreat:F1}m). Fin de la huida.");
                EndFlee();
                return;
            }
        }

        reevaluateTimer -= Time.deltaTime;

        // Si es un seguidor de la manada, el líder decide la ruta de huida; el seguidor sigue al líder a máxima velocidad
        if (ai.IsPrey && !ai.isLeader && ai.leaderTransform != null)
        {
            if (ai.Movement != null)
            {
                ai.Movement.speed = ai.fleeSpeed;
            }
            if (!ai.Movement.HasClearLineTo(ai.leaderTransform.position))
            {
                if (ai.Movement.HasReachedDestination || reevaluateTimer <= 0f)
                {
                    ai.Movement.MoveTo(ai.leaderTransform.position);
                    reevaluateTimer = 0.35f;
                }
            }
            return;
        }

        // El líder elige nuevo nodo de escape al llegar al destino o periódicamente
        if (ai.Movement.HasReachedDestination || reevaluateTimer <= 0f)
        {
            Node next = ChooseEscapeNode();

            if (next != null)
                ai.Movement.MoveTo(next.transform.position);
            else
                ai.Movement.GoToRandomNode();

            reevaluateTimer = ReevaluateInterval;
        }
    }

    private void EndFlee()
    {
        if (ai.IsPrey)
        {
            ai.NotifyPackPatrol();
        }
        else
        {
            ai.StateMachine.ChangeState(ai.FlockingState);
        }
    }

    public void Exit()
    {
        // Restaurar aspecto normal del dinosaurio
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
        }
    }

    private Node ChooseEscapeNode()
    {
        if (ai.Pathfinding == null) return null;

        Node currentNode = ai.Pathfinding.GetClosestNode(ai.transform.position);
        if (currentNode == null || currentNode.neighbors == null || currentNode.neighbors.Count == 0)
            return null;

        Vector3 threatPos = ai.CurrentThreat != null ? ai.CurrentThreat.position : (ai.Player != null ? ai.Player.position : ai.transform.position);

        Node bestNode = null;
        float highestScore = float.MinValue;

        foreach (Node neighbor in currentNode.neighbors)
        {
            if (neighbor == null) continue;

            // Cuanto más lejos del depredador/amenaza, mejor
            float score = Vector3.Distance(neighbor.transform.position, threatPos);

            // Evita ir y volver entre dos nodos
            if (neighbor == previousNode)
                score -= BacktrackPenalty;

            if (score > highestScore)
            {
                highestScore = score;
                bestNode = neighbor;
            }
        }

        previousNode = currentNode;
        return bestNode;
    }
}