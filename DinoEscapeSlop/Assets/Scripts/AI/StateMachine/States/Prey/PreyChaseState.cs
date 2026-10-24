using UnityEngine;

public class PreyChaseState : ChaseState
{
    public PreyChaseState(EnemyAI ai) : base(ai)
    {
    }

    public override void Update()
    {
        // En persecución prioritaria inducida por alarma, las presas/raptores NO huyen de los dinosaurios grandes;
        // mantienen el foco absoluto en cazar al jugador
        if (!ai.IsPriorityChaseActive && ai.CanSeeBigDino(out EnemyAI bigDino) && !(bigDino is BlindEnemyAI))
        {
            ai.NotifyPackFlee(bigDino);
            return;
        }

        base.Update();
    }
}
