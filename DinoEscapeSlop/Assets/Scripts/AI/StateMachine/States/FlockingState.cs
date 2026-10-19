using System.Collections.Generic;
using UnityEngine;

public class Flockingstate : IState
{
    private EnemyAI ai;
    private bool isReturningToNetwork = false;

    public Flockingstate(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"{ai.gameObject.name} entra en PATRULLA/FLOCKING (Es Lider?: {ai.isLeader})");

        // Restaurar velocidad y color normal de patrulla
        ai.SetChasingVisuals(false);
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
        }

        // Reanclar a la red de nodos si se encuentra fuera de ella tras una persecución
        ReturnToNodeNetwork();
    }

    public void Update()
    {
        // 1. REGLA FUNDAMENTAL DE PRESAS: Detección de dinosaurios grandes (Depredadores)
        // "si en cualquier momento uno de los dinosaurios grandes entra en su campo visual toda la manada huirá durante 10 segundos..."
        if (ai.IsPrey)
        {
            if (ai.CanSeeBigDino(out EnemyAI spottedPredator))
            {
                Debug.Log($"{ai.gameObject.name} (Presa) detectó al dinosaurio grande {spottedPredator.gameObject.name}. ¡Toda la manada huye!");
                ai.NotifyPackFlee(spottedPredator.transform, spottedPredator);
                return;
            }

            // 2. REGLA DE PRESAS: Detección del Jugador
            // Cualquier miembro de la manada que detecte al jugador alerta a toda la manada
            if (ai.CanSeePlayer())
            {
                Debug.Log($"{ai.gameObject.name} (Presa) vio al jugador. ¡Toda la manada entra en persecución!");
                ai.NotifyPackChase(ai.Player);
                return;
            }
        }
        else
        {
            // COMPORTAMIENTO DE DINOSAURIO GRANDE (Depredador)
            // Prioridad 1: Jugador humano
            if (ai.Player != null && ai.CanSeePlayer())
            {
                ai.CurrentChaseTarget = ai.Player;
                ai.StateMachine.ChangeState(ai.ChaseState);
                return;
            }

            // Prioridad 2: Presas ("dinosaurios que sean presas de otro")
            if (ai.CanSeePrey(out EnemyAI targetPrey))
            {
                Debug.Log($"{ai.gameObject.name} (Depredador) avistó presa {targetPrey.gameObject.name}. ¡Inicia cacería!");
                ai.CurrentChaseTarget = targetPrey.transform;
                ai.StateMachine.ChangeState(ai.ChaseState);
                return;
            }
        }

        // 2. Comportamiento para seguidores con líder
        if (!ai.isLeader && ai.leaderTransform != null)
        {
            // Si el seguidor está separado del líder por paredes u obstáculos:
            if (!ai.Movement.HasClearLineTo(ai.leaderTransform.position))
            {
                if (ai.Movement.HasReachedDestination)
                {
                    // Recalcula ruta guía hacia el líder
                    ai.Movement.MoveTo(ai.leaderTransform.position);
                }
            }
            return;
        }

        // 3. Si el líder (o enemigo autónomo) está regresando a la red de nodos:
        if (isReturningToNetwork)
        {
            if (ai.Movement.HasReachedDestination)
            {
                isReturningToNetwork = false;
                Debug.Log($"{ai.gameObject.name} ha regresado con éxito a la red de nodos.");
                EvaluateAndMove();
            }
            return;
        }

        // 4. Evalúa un nuevo nodo únicamente cuando ha llegado al nodo destino anterior
        if (ai.Movement.HasReachedDestination)
        {
            EvaluateAndMove();
        }
    }

    public void Exit()
    {
        isReturningToNetwork = false;
    }

    /// <summary>
    /// Guía al enemigo de vuelta a la red de nodos tras haber salido de ella para perseguir al jugador.
    /// La red actúa como una guía: si está lejos o tras paredes, vuelve al nodo más cercano para reanudar la ruta.
    /// </summary>
    public void ReturnToNodeNetwork()
    {
        if (ai.Movement == null) return;

        // --- CASO A: LÍDER O ENEMIGO AUTÓNOMO ---
        if (ai.isLeader || ai.leaderTransform == null)
        {
            Node nearestNode = ai.Movement.GetNearestPatrolOrGraphNode();
            if (nearestNode != null)
            {
                float dx = ai.transform.position.x - nearestNode.transform.position.x;
                float dz = ai.transform.position.z - nearestNode.transform.position.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                // Si el dinosaurio se alejó de la red (más de 1.5m en plano horizontal), navega de vuelta al nodo más cercano
                if (dist > 1.5f)
                {
                    isReturningToNetwork = true;
                    ai.Movement.MoveTo(nearestNode.transform.position);
                    return;
                }
            }

            isReturningToNetwork = false;
            EvaluateAndMove();
        }
        // --- CASO B: SEGUIDOR CON LÍDER ---
        else
        {
            isReturningToNetwork = false;

            // Si tiene línea despejada hacia el líder, vuelve directamente a la formación
            if (ai.Movement.HasClearLineTo(ai.leaderTransform.position))
            {
                ai.Movement.ClearPath();
            }
            else
            {
                // Si hay paredes u obstáculos en medio, usa la red de nodos como guía para reunirse con el líder
                ai.Movement.MoveTo(ai.leaderTransform.position);
            }
        }
    }

    private void EvaluateAndMove()
    {
        if (ai.Movement == null) return;

        // --- CASO 1: EL LÍDER (O AUTÓNOMO) ---
        if (ai.isLeader || ai.leaderTransform == null)
        {
            ai.Movement.GoToNextPatrolNode();
            return;
        }

        // --- CASO 2: EL SEGUIDOR ---
        if (ai.Movement.HasClearLineTo(ai.leaderTransform.position))
        {
            ai.Movement.ClearPath();
        }
        else
        {
            ai.Movement.MoveTo(ai.leaderTransform.position);
        }
    }
}
