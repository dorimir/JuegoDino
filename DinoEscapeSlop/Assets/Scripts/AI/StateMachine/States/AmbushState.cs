using UnityEngine;

/// <summary>
/// Dino Emboscador - Estado OCULTO:
/// Quieto en su punto de emboscada. Si ve al jugador (con el mismo sigilo que el resto de dinos) pasa a ACECHO.
/// </summary>
public class AmbushHiddenState : IState
{
    private EnemyAI ai;

    public AmbushHiddenState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        StopMoving();
    }

    public void Update()
    {
        // EnemyMovement.Start puede mandarlo a un nodo: se mantiene quieto
        if (ai.Movement != null && !ai.Movement.HasReachedDestination) StopMoving();

        // Recupera suavemente su orientación original
        ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, ai.AmbushHomeRot, 90f * Time.deltaTime);

        ai.EnsurePlayerReference();
        if (ai.Player == null) return;

        if (ai.CanSeePlayer())
        {
            ai.StateMachine.ChangeState(ai.AmbushStalkState);
        }
    }

    private void StopMoving()
    {
        if (ai.Movement == null) return;
        ai.Movement.speed = ai.patrolSpeed;
        ai.Movement.ClearPath();
        ai.Movement.ResetVelocity();
    }

    public void Exit() { }
}

/// <summary>
/// Dino Emboscador - Estado ACECHO:
/// Se gira hacia el jugador y le da un tiempo de aviso. Si el jugador sigue dentro de ambushTriggerRange
/// durante ambushWarningTime, se lanza al ataque (ChaseState). Si lo pierde de vista, pasa a RECUPERACIÓN.
/// </summary>
public class AmbushStalkState : IState
{
    private EnemyAI ai;
    private float warningTimer;
    private float lostSightTimer;
    private const float MaxLostSight = 1.5f;
    private const float TurnSpeed = 180f;

    public AmbushStalkState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"[{ai.gameObject.name}] (Emboscador) acecha al jugador.");
        warningTimer = 0f;
        lostSightTimer = 0f;

        if (ai.Movement != null)
        {
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
        }
    }

    public void Update()
    {
        ai.EnsurePlayerReference();
        if (ai.Player == null)
        {
            ai.StateMachine.ChangeState(ai.AmbushRecoverState);
            return;
        }

        Vector3 toPlayer = ai.Player.position - ai.transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        // Pérdida de vista
        if (ai.CanSeePlayer()) lostSightTimer = 0f;
        else lostSightTimer += Time.deltaTime;

        if (lostSightTimer >= MaxLostSight || dist > ai.ambushStalkRange * 1.5f)
        {
            ai.StateMachine.ChangeState(ai.AmbushRecoverState);
            return;
        }

        // Girarse hacia el jugador
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, look, TurnSpeed * Time.deltaTime);
        }

        // Aviso: solo avanza si el jugador está dentro del radio de ataque
        if (dist <= ai.ambushTriggerRange)
        {
            warningTimer += Time.deltaTime;
            if (warningTimer >= ai.ambushWarningTime)
            {
                Debug.Log($"[{ai.gameObject.name}] (Emboscador) ¡SE LANZA AL ATAQUE!");
                ai.TriggerAttack(ai.Player);
            }
        }
        else
        {
            warningTimer = Mathf.Max(0f, warningTimer - Time.deltaTime);
        }
    }

    public void Exit() { }
}

/// <summary>
/// Dino Emboscador - Estado RECUPERACIÓN:
/// Fase 1: cansado y quieto (ventana de escape del jugador).
/// Fase 2: vuelve andando a su punto de emboscada con A* y pasa a OCULTO.
/// </summary>
public class AmbushRecoverState : IState
{
    private enum Phase { Tired, Returning }

    private EnemyAI ai;
    private Phase phase;
    private float timer;
    private float repathTimer;
    private const float RepathInterval = 1.0f;
    private const float HomeRadius = 0.8f;

    public AmbushRecoverState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        phase = Phase.Tired;
        timer = 0f;
        repathTimer = 0f;

        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
        }
    }

    public void Update()
    {
        if (phase == Phase.Tired)
        {
            timer += Time.deltaTime;
            if (timer >= ai.ambushTiredTime)
            {
                phase = Phase.Returning;
                repathTimer = RepathInterval;
                if (ai.Movement != null) ai.Movement.MoveTo(ai.AmbushHomePos);
            }
            return;
        }

        // Fase de vuelta
        Vector3 toHome = ai.AmbushHomePos - ai.transform.position;
        toHome.y = 0f;

        if (toHome.magnitude <= HomeRadius || (ai.Movement != null && ai.Movement.HasReachedDestination))
        {
            ai.StateMachine.ChangeState(ai.AmbushHiddenState);
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f && ai.Movement != null)
        {
            repathTimer = RepathInterval;
            ai.Movement.MoveTo(ai.AmbushHomePos);
        }
    }

    public void Exit() { }
}