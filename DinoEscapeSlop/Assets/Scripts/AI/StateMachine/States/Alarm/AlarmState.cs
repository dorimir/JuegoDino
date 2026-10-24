using UnityEngine;

/// <summary>
/// Estado de alarma para el dinosaurio avisador (AlarmEnemyAI).
/// Hereda de ChaseState. A diferencia del dinosaurio básico que persigue al jugador,
/// este estado detiene el movimiento del dinosaurio por completo, lo mantiene quieto
/// encarando al jugador y difunde la posición del jugador a todos los dinosaurios
/// dentro del radio de alarma (medio mapa) para que acudan en modo persecución (chase).
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

        // Establecer estado visual de alerta (tonalidad violeta / emisión)
        ai.SetVisualState(EnemyAI.DinoVisualState.Alert);

        // Quedarse quieto en el lugar: cancelar rutas y detener velocidad
        if (ai.Movement != null)
        {
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            ai.Movement.speed = 0f;
        }

        ai.EnsurePlayerReference();
        if (ai.Player != null)
        {
            lastKnownTargetPos = ai.Player.position;
        }
        else
        {
            lastKnownTargetPos = ai.transform.position;
        }

        // Iniciar sonido de alarma y difundir el aviso inmediato a los dinosaurios en rango
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

        // Asegurar que el dinosaurio permanezca quieto durante toda la alerta
        if (ai.Movement != null)
        {
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
        }

        // Si el jugador se aproxima demasiado a su boca/rango de ataque, lo elimina
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

        // Girar en el sitio orientando el frente hacia el jugador
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

            // Transmisión periódica a los dinosaurios perseguidores para actualizar la posición
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
            // El jugador ha salido del cono o se ha ocultado tras una cobertura
            lostSightTimer += Time.deltaTime;
            float maxLost = (alarmAI != null && alarmAI.lostSightDuration > 0f) ? alarmAI.lostSightDuration : 4.0f;
            if (lostSightTimer >= maxLost)
            {
                EndChase();
                return;
            }
        }
    }

    protected override void MoveTowardsTarget(Vector3 targetPos, bool isTargetVisible)
    {
        // En este enemigo no hay desplazamiento hacia el objetivo; permanece inmóvil
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
