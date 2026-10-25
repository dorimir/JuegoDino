using UnityEngine;

public class PreyFollowLeaderState : FollowLeaderState
{
    public PreyFollowLeaderState(EnemyAI ai) : base(ai)
    {
    }

    public override void Update()
    {
        // El dinosaurio pequeño seguidor huye ante amenazas (dinosaurio mediano o grande)
        if (ai.CanSeeThreat(out EnemyAI threat))
        {
            ai.NotifyPackFlee(threat);
            return;
        }

        if (ai.CanSeePlayer())
        {
            ai.NotifyPackAttack(ai.Player);
            return;
        }

        CheckMutualEncounters();

        base.Update();
    }

    protected virtual void CheckMutualEncounters()
    {
        for (int i = 0; i < EnemyAI.AllSmallDinos.Count; i++)
        {
            EnemyAI other = EnemyAI.AllSmallDinos[i];
            if (other == null || other == ai || !other.gameObject.activeInHierarchy) continue;

            if (!ai.MutuallySees(other)) continue;

            EnemyAI leaderA = ai.EffectiveLeader;
            EnemyAI leaderB = other.EffectiveLeader;

            if (leaderA == leaderB) continue;

            EnemyAI.ResolveEncounter(leaderA, leaderB);
            break;
        }
    }
}
