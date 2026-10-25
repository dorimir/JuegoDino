using UnityEngine;

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

        if (alarmAI.CanSeePlayer())
        {
            alarmAI.TriggerAttack(alarmAI.Player);
            return true;
        }

        if (!alarmAI.ignorePrey && alarmAI.preyCooldownTimer <= 0f && alarmAI.CanSeePrey(out EnemyAI smallDino))
        {
            alarmAI.TriggerAttack(smallDino.transform, smallDino);
            return true;
        }

        return false;
    }
}
