using UnityEngine;

/// <summary>
/// Estado de patrulla específico para el dinosaurio avisador/alarma (AlarmEnemyAI).
/// Hereda de PatrolState. Cuando detecta al jugador dentro de su rango y cono de visión,
/// en lugar de perseguirlo, desencadena el estado de alerta/alarma en el que se queda quieto
/// y avisa a los demás dinosaurios del mapa.
/// </summary>
public class AlarmPatrolState : PatrolState
{
    protected AlarmEnemyAI alarmAI;

    public AlarmPatrolState(AlarmEnemyAI ai) : base(ai)
    {
        alarmAI = ai;
    }

    public override void Enter()
    {
        base.Enter();
        if (alarmAI != null)
        {
            alarmAI.StopAlarmAudio();
        }
    }

    protected override bool CheckPerceptions()
    {
        if (alarmAI == null) return false;

        // Si el centinela ve al jugador, entra en el estado de alarma
        if (alarmAI.CanSeePlayer())
        {
            alarmAI.TriggerAttack(alarmAI.Player);
            return true;
        }

        // Si está configurado para cazar presas y no las ignora
        if (!alarmAI.ignorePrey && alarmAI.preyCooldownTimer <= 0f && alarmAI.CanSeePrey(out EnemyAI smallDino))
        {
            alarmAI.TriggerAttack(smallDino.transform, smallDino);
            return true;
        }

        return false;
    }
}
