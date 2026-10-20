using UnityEngine;

/// <summary>
/// Modo Patrulla (Patrol) y Manada Dinámica (Follow the Leader):
/// - En el spawn, NINGÚN dinosaurio está predestinado: todos comienzan patrullando de forma autónoma.
/// - Los dinosaurios grandes NUNCA participan en el Follow the Leader ni forman manadas.
/// - SOLO se introduce el comportamiento Follow the Leader cuando dos dinosaurios pequeños se ven MUTUAMENTE.
///   Al verse mutuamente, se elige aleatoriamente (50/50) a uno como líder y el otro pasa a seguirlo.
/// - Si detectan a un depredador grande -> Entran en modo huida (Flee) hacia el nodo más alejado.
/// - Si detectan al jugador -> Entran en modo persecución / ataque (Chase).
/// </summary>
public class Flockingstate : IState
{
    private EnemyAI ai;
    private float followRepathTimer = 0f;
    private const float FollowRepathInterval = 0.3f;

    public Flockingstate(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        // Visual normal
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        followRepathTimer = 0f;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;

            if (ai.IsSmallDino && ai.LeaderAI != null && ai.LeaderAI.gameObject.activeInHierarchy)
            {
                ai.Movement.ClearPath();
            }
            else
            {
                if (ai.Movement.HasReachedDestination)
                {
                    ai.Movement.GoToNextPatrolNode();
                }
            }
        }
    }

    public void Update()
    {
        // -----------------------------------------------------------------------------------
        // REGLAS DE DETECCIÓN SEGÚN TIPO DE DINOSAURIO:
        // -----------------------------------------------------------------------------------

        if (ai.IsBigDino)
        {
            // 1. Dinosaurio Grande encuentra al Jugador -> Modo Ataque
            if (ai.CanSeePlayer())
            {
                Debug.Log($"[{ai.gameObject.name}] (Grande) detectó al Jugador. ¡Entra en MODO ATAQUE!");
                ai.TriggerAttack(ai.Player);
                return;
            }

            // 2. Dinosaurio Grande encuentra a un Dinosaurio Pequeño -> Modo Ataque (si no está en enfriamiento tras desistir)
            if (ai.preyCooldownTimer <= 0f && ai.CanSeePrey(out EnemyAI smallDino))
            {
                Debug.Log($"[{ai.gameObject.name}] (Grande) detectó al dinosaurio pequeño '{smallDino.name}'. ¡Entra en MODO ATAQUE!");
                ai.TriggerAttack(smallDino.transform, smallDino);
                return;
            }

            // 3. Dinosaurio Grande encuentra a otro Grande -> Sigue patrullando a su ritmo (ignorar)
            // Los grandes NUNCA forman manadas ni siguen a nadie
        }
        else
        {
            // 1. Dinosaurio Pequeño encuentra a un Dinosaurio Grande -> Modo Huida (Supervivencia primero)
            if (ai.CanSeeBigDino(out EnemyAI bigDino))
            {
                Debug.Log($"[{ai.gameObject.name}] (Pequeño) detectó al dinosaurio grande '{bigDino.name}'. ¡Entra en MODO HUIDA!");
                ai.NotifyPackFlee(bigDino);
                return;
            }

            // 2. Dinosaurio Pequeño encuentra al Jugador -> Modo Ataque
            if (ai.CanSeePlayer())
            {
                Debug.Log($"[{ai.gameObject.name}] (Pequeño) detectó al Jugador. ¡Entra en MODO ATAQUE!");
                ai.NotifyPackAttack(ai.Player);
                return;
            }

            // 3. Dinosaurio Pequeño: SOLO se introduce Follow the Leader cuando dos pequeños se ven MUTUAMENTE
            CheckEncounterWithOtherSmallDinos();
        }

        // -----------------------------------------------------------------------------------
        // COMPORTAMIENTO DE MOVIMIENTO: LÍDER / AUTÓNOMO VS SEGUIDOR (FOLLOW THE LEADER)
        // -----------------------------------------------------------------------------------
        if (ai.IsSmallDino && ai.LeaderAI != null && ai.LeaderAI.gameObject.activeInHierarchy)
        {
            // Seguidor de manada: ejecuta comportamiento Follow the Leader
            UpdateFollowTheLeader();
        }
        else
        {
            // Autónomo (inicio sin manada) o Líder: patrulla de forma independiente por la red de nodos
            if (ai.Movement != null && ai.Movement.HasReachedDestination)
            {
                ai.Movement.GoToNextPatrolNode();
            }
        }
    }

    /// <summary>
    /// Comprueba si dos dinosaurios pequeños se ven mutuamente.
    /// SOLO cuando dos pequeños se ven mutuamente (y nunca los grandes, ni predestinado de inicio)
    /// se introduce el comportamiento Follow the Leader eligiendo uno aleatoriamente como líder al 50%.
    /// </summary>
    private void CheckEncounterWithOtherSmallDinos()
    {
        if (ai.IsBigDino) return;

        for (int i = 0; i < EnemyAI.AllPrey.Count; i++)
        {
            EnemyAI other = EnemyAI.AllPrey[i];
            if (other == null || other == ai || !other.gameObject.activeInHierarchy) continue;

            // CONDICIÓN ESTRICTA: Ambos pequeños deben verse MUTUAMENTE
            if (!ai.MutuallySees(other)) continue;

            // Obtener el líder efectivo de cada uno
            EnemyAI leaderA = ai.EffectiveLeader;
            EnemyAI leaderB = other.EffectiveLeader;

            // Si ya forman parte de la misma manada, no hay nada que resolver
            if (leaderA == leaderB) continue;

            // Se han visto mutuamente: resolver elección de líder al 50% y formar manada
            EnemyAI.ResolveEncounter(leaderA, leaderB);
            break;
        }
    }

    /// <summary>
    /// Comportamiento 'Follow the Leader':
    /// El dinosaurio seguidor se posiciona en formación detrás del líder de la manada,
    /// adaptando su velocidad (acelera si se queda atrás, frena si está muy cerca),
    /// usando navegación directa en espacios abiertos y A* para doblar esquinas.
    /// </summary>
    private void UpdateFollowTheLeader()
    {
        EnemyAI leader = ai.LeaderAI;
        if (leader == null || !leader.gameObject.activeInHierarchy)
        {
            // Si el líder no existe o murió, vuelve a patrulla autónoma
            ai.PromoteToLeader();
            return;
        }

        if (ai.Movement == null) return;

        // Calcular posición objetivo en formación escalonada (cuña / fila) detrás del líder
        int slotIndex = leader.packFollowers.IndexOf(ai);
        if (slotIndex < 0)
        {
            leader.packFollowers.Add(ai);
            slotIndex = leader.packFollowers.IndexOf(ai);
        }

        int row = (slotIndex / 2) + 1;
        float side = (slotIndex % 2 == 0) ? -1f : 1f;

        float backDist = 1.8f * row;
        float lateralDist = side * (0.6f + (row - 1) * 0.25f);

        Vector3 leaderPos = leader.transform.position;
        Vector3 leaderFwd = leader.transform.forward;
        Vector3 leaderRight = leader.transform.right;

        // Posición ideal con compensación lateral
        Vector3 targetPos = leaderPos - (leaderFwd * backDist) + (leaderRight * lateralDist);
        targetPos.y = ai.transform.position.y;

        // Si la posición lateral choca con una pared, colocarse justo detrás en fila india
        if (!ai.Movement.HasClearLineTo(targetPos, 0.15f))
        {
            targetPos = leaderPos - (leaderFwd * backDist);
            targetPos.y = ai.transform.position.y;
        }

        float distToLeader = Vector3.Distance(ai.transform.position, leaderPos);
        float distToSlot = Vector3.Distance(ai.transform.position, targetPos);

        // --- REGULACIÓN DE VELOCIDAD PARA MANTENER LA FORMACIÓN ---
        if (distToLeader > 4.5f)
        {
            // Se está quedando atrás: acelerar para alcanzar al grupo
            ai.Movement.speed = ai.patrolSpeed * 1.35f;
        }
        else if (distToSlot < 0.35f)
        {
            // Ya está en su posición asignada: emparejar velocidad con el líder
            float leaderSpeed = (leader.Movement != null) ? leader.Movement.speed : ai.patrolSpeed;
            ai.Movement.speed = leaderSpeed;

            // Orientarse en la misma dirección que el líder
            if (leader.Movement != null && leader.Movement.Velocity.magnitude > 0.1f)
            {
                ai.transform.rotation = Quaternion.RotateTowards(
                    ai.transform.rotation,
                    Quaternion.Euler(0f, leader.transform.eulerAngles.y, 0f),
                    ai.Movement.patrolTurnRate * Time.deltaTime
                );
            }
        }
        else if (distToLeader < 1.3f)
        {
            // Demasiado pegado al líder: frenar un poco para darle espacio
            ai.Movement.speed = ai.patrolSpeed * 0.5f;
        }
        else
        {
            // Distancia normal
            ai.Movement.speed = ai.patrolSpeed;
        }

        // --- CONDUCCIÓN HACIA LA POSICIÓN OBJETIVO ---
        bool hasClearToSlot = ai.Movement.HasClearLineTo(targetPos, 0.15f);

        if (hasClearToSlot)
        {
            // Línea despejada directa: navegación directa al slot
            ai.Movement.MoveTo(targetPos);
        }
        else
        {
            // Hay esquinas o paredes intermedias (el líder dobló una esquina):
            // Usar A* periódicamente hacia la posición del líder
            followRepathTimer += Time.deltaTime;
            if (followRepathTimer >= FollowRepathInterval || ai.Movement.HasReachedDestination)
            {
                followRepathTimer = 0f;
                ai.Movement.MoveTo(leaderPos);
            }
        }
    }

    public void Exit()
    {
    }
}
