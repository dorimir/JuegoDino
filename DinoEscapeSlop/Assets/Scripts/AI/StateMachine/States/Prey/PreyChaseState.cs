using UnityEngine;

public class PreyChaseState : ChaseState
{
    public PreyChaseState(EnemyAI ai) : base(ai)
    {
    }

    public override void Update()
    {

        if (!ai.IsPriorityChaseActive && ai.CanSeeThreat(out EnemyAI threat))
        {
            ai.NotifyPackFlee(threat);
            return;
        }

        base.Update();
    }
}
