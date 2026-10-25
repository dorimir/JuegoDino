using UnityEngine;

public class PreyPatrolState : PatrolState
{
    public PreyPatrolState(EnemyAI ai) : base(ai)
    {
    }

    protected override bool CheckPerceptions()
    {

        if (ai.CanSeeThreat(out EnemyAI threat))
        {
            ai.NotifyPackFlee(threat);
            return true;
        }

        if (ai.CanSeePlayer())
        {
            ai.NotifyPackAttack(ai.Player);
            return true;
        }

        CheckMutualEncounters();

        if (ai.LeaderAI != null && ai.LeaderAI.gameObject.activeInHierarchy)
        {
            ai.TriggerPatrol();
            return true;
        }

        return false;
    }
}
