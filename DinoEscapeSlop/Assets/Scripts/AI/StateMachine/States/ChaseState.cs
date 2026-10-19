using UnityEngine;
using UnityEngine.SceneManagement;

public class ChaseState : IState
{
    private EnemyAI ai;
    private float repathTimer;
    private const float RepathInterval = 0.2f;

    private float lostSightTimer;
    private float totalChaseTimer;
    private Vector3 lastKnownTargetPos;

    public ChaseState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"{ai.gameObject.name} ha entrado en estado: CHASE (Color rojo activado)");
        repathTimer = 0f;
        lostSightTimer = 0f;
        totalChaseTimer = 0f;

        // Poner al dinosaurio rojo para indicar el cambio de estado
        ai.SetChasingVisuals(true);

        // Aumentar notablemente la velocidad al perseguir
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.chaseSpeed;
        }

        Transform target = GetTarget();
        if (target != null)
        {
            lastKnownTargetPos = target.position;
        }
    }

    private Transform GetTarget()
    {
        if (ai.CurrentChaseTarget != null && ai.CurrentChaseTarget.gameObject.activeInHierarchy)
        {
            return ai.CurrentChaseTarget;
        }
        return ai.Player;
    }

    public void Update()
    {
        // REGLA FUNDAMENTAL DE PRESAS: Si en cualquier momento un dinosaurio grande entra en su campo visual,
        // toda la manada interrumpe la persecución y huye inmediatamente.
        if (ai.IsPrey)
        {
            if (ai.CanSeeBigDino(out EnemyAI spottedPredator))
            {
                Debug.Log($"{ai.gameObject.name} (Presa) detectó al depredador {spottedPredator.gameObject.name} en persecución. ¡Toda la manada huye!");
                ai.NotifyPackFlee(spottedPredator.transform, spottedPredator);
                return;
            }
        }

        Transform target = GetTarget();
        if (target == null)
        {
            EndChase();
            return;
        }

        bool isTargetPlayer = (ai.Player != null && (target == ai.Player || target.IsChildOf(ai.Player)));

        // Si es un depredador persiguiendo a una presa, pero el jugador entra en su cono de visión,
        // prioriza inmediatamente cazar al jugador humano
        if (!ai.IsPrey && !isTargetPlayer && ai.Player != null && ai.CanSeePlayer())
        {
            target = ai.Player;
            ai.CurrentChaseTarget = ai.Player;
            isTargetPlayer = true;
            lastKnownTargetPos = target.position;
        }

        // 1. Comprobar si ha atrapado al objetivo
        float distanceToTarget = Vector3.Distance(ai.transform.position, target.position);
        if (distanceToTarget <= ai.AttackRange)
        {
            if (isTargetPlayer)
            {
                ai.KillPlayer();
                return;
            }
            else
            {
                // Depredador devoró a una presa
                EnemyAI preyAI = target.GetComponent<EnemyAI>();
                if (preyAI != null)
                {
                    preyAI.Die();
                }
                EndChase();
                return;
            }
        }

        // 2. Temporizador total de persecución:
        // Las presas se rinden en 5 segundos exactos si no atrapan al jugador ("si no lo atrapan en 5 segundos se rendirán y volverán a patrullar")
        // Los dinosaurios grandes persiguen hasta 10 segundos.
        totalChaseTimer += Time.deltaTime;
        float maxChaseDuration = ai.IsPrey ? 5.0f : 10.0f;
        if (totalChaseTimer >= maxChaseDuration)
        {
            Debug.Log($"{ai.gameObject.name}: Han transcurrido {maxChaseDuration} segundos sin atrapar al objetivo. Rendición y regreso a patrulla.");
            EndChase();
            return;
        }

        // 3. Comprobación de visión y pérdida de vista
        bool canSee = false;
        if (isTargetPlayer)
        {
            canSee = ai.CanSeePlayer();
        }
        else
        {
            if (ai.CanSeePrey(out EnemyAI spottedPrey) && spottedPrey.transform == target)
            {
                canSee = true;
            }
        }

        if (canSee)
        {
            lostSightTimer = 0f;
            lastKnownTargetPos = target.position;
        }
        else
        {
            lostSightTimer += Time.deltaTime;
            float lostLimit = ai.IsPrey ? 2.5f : 3.0f;
            if (lostSightTimer >= lostLimit)
            {
                Debug.Log($"{ai.gameObject.name}: El objetivo ha dejado de estar a la vista por {lostLimit}s. Regresando a patrulla.");
                EndChase();
                return;
            }
        }

        // 4. Navegación hacia el objetivo
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            Vector3 dest = canSee ? target.position : lastKnownTargetPos;
            ai.Movement.MoveTo(dest);
            repathTimer = RepathInterval;
        }
    }

    private void EndChase()
    {
        if (ai.IsPrey)
        {
            ai.NotifyPackPatrol();
        }
        else
        {
            ai.CurrentChaseTarget = null;
            ai.StateMachine.ChangeState(ai.FlockingState);
        }
    }

    public void Exit()
    {
        // Restaurar color normal del dinosaurio
        ai.SetChasingVisuals(false);

        // Restaurar velocidad de patrulla al terminar la persecución
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
        }
    }
}