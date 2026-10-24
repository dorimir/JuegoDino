using UnityEngine;

/// <summary>
/// Modo Persecución / Ataque (Chase):
/// Al detectar al jugador, el dinosaurio entra en modo ataque y lo persigue a gran velocidad (8.5 m/s, color rojo).
/// - Conducción directa en tiempo real sin pausas intermedias si la línea está despejada.
/// - Si el jugador dobla una esquina o muro, usa A* hacia el último punto conocido.
/// - Si pasan 5 segundos completos sin ver al jugador, desiste y regresa a Patrulla.
/// - Si en cualquier momento detecta a un depredador grande, la supervivencia prima y pasa inmediatamente a modo Huida (Flee).
/// - Al alcanzar el AttackRange (1.3m), mata al jugador (KillPlayer) y activa la pantalla de muerte.
/// </summary>
public class ChaseState : IState
{
    private EnemyAI ai;
    private float repathTimer;
    private const float RepathInterval = 0.25f;

    private float chaseDurationTimer;
    private float lostSightTimer;
    private Vector3 lastKnownTargetPos;

    public ChaseState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        string targetName = (ai.TargetDino != null) ? ai.TargetDino.name : "el jugador";
        Debug.Log($"[{ai.gameObject.name}] ha entrado en MODO ATAQUE (Color rojo activado). Persiguiendo a {targetName}.");
        repathTimer = 0f;
        chaseDurationTimer = 0f;
        lostSightTimer = 0f;

        // Visual rojo para modo ataque
        ai.SetVisualState(EnemyAI.DinoVisualState.Chasing);

        // Velocidad rápida durante persecución
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.chaseSpeed;
            // Emboscador: giro limitado para que el ataque se pueda esquivar
            if (ai.dinoEmboscador) ai.Movement.chaseTurnRate = ai.ambushTurnRate;
        }

        if (ai.TargetDino != null)
        {
            lastKnownTargetPos = ai.TargetDino.transform.position;
        }
        else
        {
            ai.EnsurePlayerReference();
            if (ai.Player != null)
            {
                lastKnownTargetPos = ai.Player.position;
            }
            else
            {
                lastKnownTargetPos = ai.transform.position;
            }
        }
    }

    public void Update()
    {
        // 1. REGLA DE SUPERVIVENCIA: Si es un dinosaurio pequeño y detecta a un dinosaurio grande, cancela el ataque y huye inmediatamente
        if (ai.IsSmallDino && ai.CanSeeBigDino(out EnemyAI bigDino))
        {
            Debug.Log($"[{ai.gameObject.name}] (Pequeño) avistó al dinosaurio grande '{bigDino.name}' durante el ataque. ¡Entra en MODO HUIDA!");
            ai.TriggerFlee(bigDino);
            return;
        }

        // 2. PRIORIDAD ABSOLUTA DEL DINOSAURIO GRANDE: Cazar al jugador es prioritario sobre cazar dinosaurios pequeños
        if (ai.IsBigDino && ai.TargetDino != null && ai.CanSeePlayer())
        {
            Debug.Log($"[{ai.gameObject.name}] (Grande) vio al JUGADOR mientras perseguía a '{ai.TargetDino.name}'. ¡Prioridad máxima al jugador! Cambiando a cazar al jugador.");
            ai.TriggerAttack(ai.Player);
            return;
        }

        // 3. Comportamiento según el objetivo de ataque:
        if (ai.TargetDino != null)
        {
            // --- OBJETIVO: DINOSAURIO PEQUEÑO ---
            if (!ai.TargetDino.gameObject.activeInHierarchy)
            {
                EndChase();
                return;
            }

            // Límite de tiempo total de persecución continua del grande hacia dinosaurios pequeños (rebajado a 3.5s)
            chaseDurationTimer += Time.deltaTime;
            float maxChase = (ai.preyMaxChaseTime > 0f) ? ai.preyMaxChaseTime : 3.5f;
            if (chaseDurationTimer >= maxChase)
            {
                Debug.Log($"[{ai.gameObject.name}] (Grande): Límite de tiempo de persecución a presa alcanzado ({maxChase:F1}s). Desistiendo y regresando a patrulla.");
                EndChase();
                return;
            }

            Vector2 myPosHoriz = new Vector2(ai.transform.position.x, ai.transform.position.z);
            Vector2 targetPosHoriz = new Vector2(ai.TargetDino.transform.position.x, ai.TargetDino.transform.position.z);
            float distanceToTarget = Vector2.Distance(myPosHoriz, targetPosHoriz);
            float verticalDiff = Mathf.Abs(ai.transform.position.y - ai.TargetDino.transform.position.y);

            // Al alcanzar el rango de ataque, devorar a la presa
            if (distanceToTarget <= ai.AttackRange && verticalDiff < 2.0f)
            {
                Debug.Log($"[{ai.gameObject.name}] (Grande) atrapó y devoró al dinosaurio pequeño '{ai.TargetDino.name}'.");
                ai.TargetDino.Die();
                EndChase();
                return;
            }

            // Comprobación de visión directa del dinosaurio presa
            bool dinoVisible = ai.CanSeeSpecificDino(ai.TargetDino);
            if (dinoVisible)
            {
                lostSightTimer = 0f;
                lastKnownTargetPos = ai.TargetDino.transform.position;
            }
            else
            {
                lostSightTimer += Time.deltaTime;
                float lostLimit = (ai.preyLostSightTime > 0f) ? ai.preyLostSightTime : 2.0f;
                if (lostSightTimer >= lostLimit)
                {
                    Debug.Log($"[{ai.gameObject.name}] (Grande): La presa escapó de la vista por {lostLimit:F1}s. Regresando a patrulla.");
                    EndChase();
                    return;
                }
            }
        }
        else
        {
            // --- OBJETIVO: JUGADOR ---
            ai.EnsurePlayerReference();
            if (ai.Player == null)
            {
                EndChase();
                return;
            }

            // Emboscador: la carrera dura un tiempo fijo y luego se cansa
            if (ai.dinoEmboscador)
            {
                chaseDurationTimer += Time.deltaTime;
                if (chaseDurationTimer >= ai.ambushAttackDuration)
                {
                    EndChase();
                    return;
                }
            }

            Vector2 myPosHoriz = new Vector2(ai.transform.position.x, ai.transform.position.z);
            Vector2 playerPosHoriz = new Vector2(ai.Player.position.x, ai.Player.position.z);
            float distanceToPlayer = Vector2.Distance(myPosHoriz, playerPosHoriz);
            float verticalDiff = Mathf.Abs(ai.transform.position.y - ai.Player.position.y);

            if (distanceToPlayer <= ai.AttackRange && verticalDiff < 2.0f)
            {
                ai.KillPlayer();
                return;
            }

            bool playerVisible = ai.CanSeePlayer();
            if (playerVisible)
            {
                lostSightTimer = 0f;
                lastKnownTargetPos = ai.Player.position;
            }
            else
            {
                lostSightTimer += Time.deltaTime;
                float lostLimit = (ai.playerLostSightTime > 0f) ? ai.playerLostSightTime : 5.0f;
                if (lostSightTimer >= lostLimit)
                {
                    Debug.Log($"[{ai.gameObject.name}]: El jugador escapó de la vista por {lostLimit:F1}s. Pasa a BÚSQUEDA.");
                    ai.TriggerSearch(lastKnownTargetPos);
                    return;
                }
            }
        }

        // 3. Persecución directa en tiempo real o navegación por red de nodos A*
        Vector3 targetPos = lastKnownTargetPos;

        if (ai.Movement != null)
        {
            bool hasClearLine = ai.Movement.HasClearLineTo(targetPos);

            if (hasClearLine)
            {
                // Conducción directa continua hacia el objetivo
                ai.Movement.SetDirectChaseTarget(targetPos);
            }
            else
            {
                // Navegación por red de nodos hacia la última posición conocida
                repathTimer -= Time.deltaTime;
                if (repathTimer <= 0f)
                {
                    repathTimer = RepathInterval;
                    ai.Movement.MoveTo(targetPos);
                }
            }

            // Si ya alcanzó la última posición conocida y no ve al objetivo, barrer con la mirada
            bool isTargetCurrentlyVisible = (ai.TargetDino != null) ? ai.CanSeeSpecificDino(ai.TargetDino) : ai.CanSeePlayer();
            if (!isTargetCurrentlyVisible && ai.Movement.HasReachedDestination)
            {
                float scanAngle = Mathf.Sin(Time.time * 3.5f) * 45f;
                Quaternion scanRot = Quaternion.Euler(0f, scanAngle, 0f) * ai.transform.rotation;
                ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, scanRot, 120f * Time.deltaTime);
            }
        }
    }

    private void EndChase()
    {
        if (ai.TargetDino != null)
        {
            ai.preyCooldownTimer = ai.preyChaseCooldown;
        }
        ai.TargetDino = null;
        ai.TriggerPatrol();
    }

    public void Exit()
    {
        // Restaurar aspecto normal del dinosaurio
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        ai.TargetDino = null;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            if (!ai.dinoEmboscador) ai.Movement.GoToNearestPatrolNode();
        }
    }
}