using UnityEngine;
using UnityEngine.SceneManagement;

public class ChaseState : IState
{
    private EnemyAI ai;
    private float repathTimer;
    private const float RepathInterval = 0.2f;

    private float lostSightTimer;
    private float totalChaseTimer;
    private Vector3 lastKnownPlayerPos;

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

        if (ai.Player != null)
        {
            lastKnownPlayerPos = ai.Player.position;
        }
    }

    public void Update()
    {
        if (ai.Player == null)
        {
            ai.StateMachine.ChangeState(ai.FlockingState);
            return;
        }

        // 1. Comprobar si ha atrapado al jugador
        float distanceToPlayer = Vector3.Distance(ai.transform.position, ai.Player.position);
        if (distanceToPlayer <= ai.AttackRange)
        {
            ai.KillPlayer();
            return;
        }

        // 2. Temporizador total de persecución (máximo 10 segundos sin atrapar al jugador)
        totalChaseTimer += Time.deltaTime;
        if (totalChaseTimer >= 10.0f)
        {
            Debug.Log($"{ai.gameObject.name}: Han transcurrido 10 segundos de persecución sin atrapar al jugador. Regresando a patrulla.");
            ai.StateMachine.ChangeState(ai.FlockingState);
            return;
        }

        // 3. Comprobación del cono de visión y pérdida de vista (3 segundos)
        bool canSee = ai.CanSeePlayer();
        if (canSee)
        {
            lostSightTimer = 0f; // Reinicia el temporizador de pérdida de vista
            lastKnownPlayerPos = ai.Player.position;
        }
        else
        {
            lostSightTimer += Time.deltaTime;
            if (lostSightTimer >= 3.0f)
            {
                Debug.Log($"{ai.gameObject.name}: El jugador ha dejado de estar a la vista por 3 segundos. Regresando a patrulla.");
                ai.StateMachine.ChangeState(ai.FlockingState);
                return;
            }
        }

        // 4. Navegación hacia el jugador (o hacia su última posición vista si se oculta brevemente)
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            Vector3 target = canSee ? ai.Player.position : lastKnownPlayerPos;
            ai.Movement.MoveTo(target);
            repathTimer = RepathInterval;
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