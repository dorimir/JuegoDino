using UnityEngine;

/// <summary>
/// Estado de alarma para el dinosaurio avisador (AlarmEnemyAI).
/// Detiene el movimiento del dinosaurio por completo, lo mantiene quieto
/// encarando al jugador y difunde la posición del jugador a los demás dinosaurios.
/// </summary>
public class AlarmState : ChaseState, IPriorityState
{
    protected AlarmEnemyAI alarmAI;
    protected float broadcastTimer;

    public override int Priority => 100;

    public AlarmState(AlarmEnemyAI ai) : base(ai)
    {
        alarmAI = ai;
    }

    public override void Enter()
    {
        repathTimer = 0f;
        lostSightTimer = 0f;
        broadcastTimer = 0f;

        ai.SetVisualState(EnemyAI.DinoVisualState.Alarm);

        if (ai.Movement != null)
        {
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            ai.Movement.speed = 0f;
        }

        ai.EnsurePlayerReference();
        lastKnownTargetPos = (ai.Player != null) ? ai.Player.position : ai.transform.position;

        if (alarmAI != null)
        {
            alarmAI.PlayAlarmAudio();
            alarmAI.AlertNearbyDinos(lastKnownTargetPos);
        }
    }

    public override void Update()
    {
        ai.EnsurePlayerReference();
        if (ai.Player == null)
        {
            EndChase();
            return;
        }

        if (ai.Movement != null)
        {
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
        }

        float distToPlayer = Vector2.Distance(
            new Vector2(ai.transform.position.x, ai.transform.position.z),
            new Vector2(ai.Player.position.x, ai.Player.position.z)
        );
        float verticalDiff = Mathf.Abs(ai.transform.position.y - ai.Player.position.y);

        if (distToPlayer <= ai.AttackRange && verticalDiff < 2.0f)
        {
            ai.KillPlayer();
            return;
        }

        Vector3 toPlayer = ai.Player.position - ai.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized);
            float turnSpeed = (alarmAI != null) ? alarmAI.turnSpeedWhenAlerting : 180f;
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        bool playerVisible = ai.CanSeePlayer();
        if (playerVisible)
        {
            lostSightTimer = 0f;
            lastKnownTargetPos = ai.Player.position;

            broadcastTimer -= Time.deltaTime;
            if (broadcastTimer <= 0f)
            {
                float interval = (alarmAI != null) ? alarmAI.alertBroadcastInterval : 1.0f;
                broadcastTimer = interval;

                if (alarmAI != null)
                {
                    alarmAI.AlertNearbyDinos(lastKnownTargetPos);
                }
            }
        }
        else
        {
            lostSightTimer += Time.deltaTime;
            float maxLost = (alarmAI != null && alarmAI.lostSightDuration > 0f) ? alarmAI.lostSightDuration : 4.0f;
            if (lostSightTimer >= maxLost)
            {
                EndChase();
            }
        }
    }

    protected override void MoveTowardsTarget(Vector3 targetPos, bool isTargetVisible)
    {
    }

    public override void EndChase()
    {
        if (alarmAI != null)
        {
            alarmAI.StopAlarmAudio();
        }
        base.EndChase();
    }

    public override void Exit()
    {
        if (alarmAI != null)
        {
            alarmAI.StopAlarmAudio();
        }

        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        ai.TargetDino = null;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            ai.Movement.GoToNearestPatrolNode();
        }
    }
}
