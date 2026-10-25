using UnityEngine;

/// <summary>
/// Dino Emboscador - Estado OCULTO:
/// Permanece quieto en su punto de emboscada hasta detectar visualmente al jugador.
/// Se alinea con la orientación del punto de emboscada y vigila su campo de visión.
/// </summary>
public class AmbushHiddenState : IState
{
    private readonly EnemyAI ai;

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
        if (ai.Movement != null && !ai.Movement.HasReachedDestination)
        {
            StopMoving();
        }

        // Rotación suave hacia la orientación del punto de emboscada
        if (ai.AmbushHomeRot != Quaternion.identity)
        {
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, ai.AmbushHomeRot, 120f * Time.deltaTime);
        }

        ai.EnsurePlayerReference();
        if (ai.Player == null) return;

        // Detecta al jugador pasando por delante dentro de su cono de visión
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
/// Encarar al jugador durante un breve tiempo de advertencia antes de lanzar la persecución.
/// Si el jugador se acerca demasiado o permanece en su visión, ataca.
/// Si el jugador huye y rompe la línea de visión rápidamente, vuelve al punto de emboscada.
/// </summary>
public class AmbushStalkState : IState
{
    private readonly EnemyAI ai;
    private float warningTimer;
    private float lostSightTimer;
    private const float MaxLostSight = 1.5f;
    private const float TurnSpeed = 220f;

    public AmbushStalkState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        warningTimer = 0f;
        lostSightTimer = 0f;
        ai.SetVisualState(EnemyAI.DinoVisualState.Stalking);

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

        if (ai.CanSeePlayer())
        {
            lostSightTimer = 0f;
        }
        else
        {
            lostSightTimer += Time.deltaTime;
        }

        // Si pierde de vista al jugador o este se aleja demasiado del rango de acecho
        if (lostSightTimer >= MaxLostSight || dist > ai.ambushStalkRange * 1.5f)
        {
            ai.StateMachine.ChangeState(ai.AmbushRecoverState);
            return;
        }

        // Girar hacia el jugador
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, look, TurnSpeed * Time.deltaTime);
        }

        // Cuenta atrás de acecho antes de abalanzarse
        if (dist <= ai.ambushTriggerRange || dist <= ai.visionRange)
        {
            warningTimer += Time.deltaTime;
            // Si el jugador está muy cerca (< 3.5m) o ha concluido el aviso de acecho, lanza el ataque
            if (dist <= 3.5f || warningTimer >= ai.ambushWarningTime)
            {
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
/// Fase de cansancio tras la persecución y posterior regreso navegando al punto de emboscada.
/// Al llegar al punto de emboscada, reanuda el estado oculto para seguir acechando.
/// </summary>
public class AmbushRecoverState : IState
{
    private enum Phase { Tired, Returning }

    private readonly EnemyAI ai;
    private Phase phase;
    private float timer;
    private float repathTimer;
    private const float RepathInterval = 0.5f;
    private const float HomeRadius = 1.0f;

    public AmbushRecoverState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        phase = Phase.Tired;
        timer = 0f;
        repathTimer = 0f;

        ai.SetVisualState(EnemyAI.DinoVisualState.Tired);

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
                repathTimer = 0f;

                if (ai is AmbushEnemyAI ambushAI && ambushAI.returnToNearestAmbushPoint)
                {
                    AmbushPoint nearest = ambushAI.FindNearestAmbushPoint();
                    if (nearest != null)
                    {
                        ambushAI.SetAmbushPoint(nearest);
                    }
                }

                ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
                if (ai.Movement != null)
                {
                    ai.Movement.speed = ai.patrolSpeed;
                    ai.Movement.MoveTo(ai.AmbushHomePos);
                }
            }
            return;
        }

        Vector3 toHome = ai.AmbushHomePos - ai.transform.position;
        toHome.y = 0f;

        // Si ha llegado al punto de emboscada o el pathfinding ha completado el trayecto
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
