using UnityEngine;

public class PreyChaseState : ChaseState
{
    public PreyChaseState(EnemyAI ai) : base(ai)
    {
    }

    public override void Update()
    {
        if (ai.CanSeeBigDino(out EnemyAI bigDino) && !(bigDino is BlindEnemyAI))
        {
            ai.NotifyPackFlee(bigDino);
            return;
        }

        base.Update();
    }
}
