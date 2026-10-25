using UnityEngine;

public class PreyChaseState : ChaseState
{
    public PreyChaseState(EnemyAI ai) : base(ai)
    {
    }

    public override void Update()
    {
        // El dinosaurio pequeño huye si ve a un dinosaurio mediano o grande
        if (!ai.IsPriorityChaseActive && ai.CanSeeThreat(out EnemyAI threat))
        {
            ai.NotifyPackFlee(threat);
            return;
        }

        base.Update();
    }
}
