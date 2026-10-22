using UnityEngine;

public class PreyFollowLeaderState : FollowLeaderState
{
    public PreyFollowLeaderState(EnemyAI ai) : base(ai)
    {
    }

    public override void Update()
    {
        if (ai.CanSeeBigDino(out EnemyAI bigDino))
        {
            ai.NotifyPackFlee(bigDino);
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
        for (int i = 0; i < EnemyAI.AllPrey.Count; i++)
        {
            EnemyAI other = EnemyAI.AllPrey[i];
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
