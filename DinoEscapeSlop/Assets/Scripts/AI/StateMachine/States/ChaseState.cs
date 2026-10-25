using UnityEngine;

public class ChaseState : IState, IPriorityState
{
    protected EnemyAI ai;
    protected float repathTimer;
    protected const float RepathInterval = 0.25f;
    protected float lostSightTimer;
    protected float chaseDurationTimer;
    protected Vector3 lastKnownTargetPos;

    /// <summary>
    /// Nivel de prioridad del estado. Si este chase fue activado por la alarma de un avisador,
    /// adquiere prioridad máxima (100) y bloquea cualquier interrupción de patrulla, huida o investigación.
    /// </summary>
    public virtual int Priority => (ai != null && ai.isPriorityChase) ? 100 : 0;

    public ChaseState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public virtual void Enter()
    {
        repathTimer = 0f;
        lostSightTimer = 0f;
        chaseDurationTimer = 0f;

        ai.SetVisualState(EnemyAI.DinoVisualState.Chasing);
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.chaseSpeed;
            if (ai.dinoEmboscador) ai.Movement.chaseTurnRate = ai.ambushTurnRate;
        }

        if (!ai.isPriorityChase && ai.TargetDino != null)
        {
            lastKnownTargetPos = ai.TargetDino.transform.position;
        }
        else
        {
            ai.EnsurePlayerReference();
            lastKnownTargetPos = (ai.Player != null) ? ai.Player.position : ai.transform.position;
        }
    }

    public virtual void Update()
    {
        // Si aparece un depredador superior en el cono de visión, se cancela la persecución para huir
        if (!ai.isPriorityChase && ai.CanSeeThreat(out EnemyAI threat))
        {
            if (ai.IsSmallDino) ai.NotifyPackFlee(threat);
            else ai.TriggerFlee(threat);
            return;
        }

        if (!ai.isPriorityChase && ai.TargetDino != null)
        {
            if (ai is BlindEnemyAI || ai is AmbushEnemyAI || ai.dinoEmboscador)
            {
                EndChase();
                return;
            }

            if (ai.CanSeePlayer())
            {
                ai.TriggerAttack(ai.Player);
                return;
            }

            if (!ai.TargetDino.gameObject.activeInHierarchy)
            {
                EndChase();
                return;
            }

            float distToTarget = Vector2.Distance(
                new Vector2(ai.transform.position.x, ai.transform.position.z),
                new Vector2(ai.TargetDino.transform.position.x, ai.TargetDino.transform.position.z)
            );
            float vDiff = Mathf.Abs(ai.transform.position.y - ai.TargetDino.transform.position.y);

            if (distToTarget <= ai.AttackRange && vDiff < 2.5f)
            {
                ai.TargetDino.Die();
                EndChase();
                return;
            }

            bool preyVisible = ai.CanSeeSpecificDino(ai.TargetDino);
            if (preyVisible)
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
                    EndChase();
                    return;
                }
            }

            MoveTowardsTarget(lastKnownTargetPos, preyVisible);
            return;
        }

        ai.EnsurePlayerReference();
        if (ai.Player == null)
        {
            EndChase();
            return;
        }

        float dist = Vector2.Distance(
            new Vector2(ai.transform.position.x, ai.transform.position.z),
            new Vector2(ai.Player.position.x, ai.Player.position.z)
        );
        float verticalDiff = Mathf.Abs(ai.transform.position.y - ai.Player.position.y);

        if (dist <= ai.AttackRange && verticalDiff < 2.0f)
        {
            ai.KillPlayer();
            return;
        }

        // Emboscador: duración de persecución máxima antes de volver a ocultarse
        if (ai.dinoEmboscador)
        {
            chaseDurationTimer += Time.deltaTime;
            if (chaseDurationTimer >= ai.ambushAttackDuration)
            {
                EndChase();
                return;
            }
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
                EndChase();
                return;
            }
        }

        MoveTowardsTarget(lastKnownTargetPos, playerVisible);
    }

    protected virtual void MoveTowardsTarget(Vector3 targetPos, bool isTargetVisible)
    {
        if (ai.Movement == null) return;

        if (ai.Movement.HasClearLineTo(targetPos))
        {
            ai.Movement.SetDirectChaseTarget(targetPos);
        }
        else
        {
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                repathTimer = RepathInterval;
                ai.Movement.MoveTo(targetPos);
            }
        }

        if (!isTargetVisible && ai.Movement.HasReachedDestination)
        {
            float scanAngle = Mathf.Sin(Time.time * 3.5f) * 45f;
            Quaternion scanRot = Quaternion.Euler(0f, scanAngle, 0f) * ai.transform.rotation;
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, scanRot, 120f * Time.deltaTime);
        }
    }

    public virtual void EndChase()
    {
        if (ai == null) return;

        ai.isPriorityChase = false;
        if (ai.TargetDino != null)
        {
            ai.preyCooldownTimer = ai.preyChaseCooldown;
            ai.TargetDino = null;
        }

        if (ai.dinoEmboscador && ai.AmbushRecoverState != null)
        {
            ai.StateMachine.ChangeState(ai.AmbushRecoverState);
            return;
        }

        ai.TriggerPatrol();
    }

    public virtual void Exit()
    {
        if (ai == null) return;

        ai.isPriorityChase = false;
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        ai.TargetDino = null;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.chaseTurnRate = 340f;
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            if (!ai.dinoEmboscador) ai.Movement.GoToNearestPatrolNode();
        }
    }
}
