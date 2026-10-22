using UnityEngine;

public class PredatorPatrolState : PatrolState
{
    public PredatorPatrolState(EnemyAI ai) : base(ai)
    {
    }

    protected override bool CheckPerceptions()
    {
        if (ai.CanSeePlayer())
        {
            ai.TriggerAttack(ai.Player);
            return true;
        }

        if (ai.preyCooldownTimer <= 0f && ai.CanSeePrey(out EnemyAI smallDino))
        {
            ai.TriggerAttack(smallDino.transform, smallDino);
            return true;
        }

        return false;
    }
}
