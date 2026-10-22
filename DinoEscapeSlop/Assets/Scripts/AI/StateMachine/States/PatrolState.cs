using UnityEngine;

public class PatrolState : IState
{
    protected EnemyAI ai;

    public PatrolState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public virtual void Enter()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            if (ai.Movement.HasReachedDestination)
            {
                ai.Movement.GoToNextPatrolNode();
            }
        }
    }

    public virtual void Update()
    {
        if (CheckPerceptions()) return;

        if (ai.Movement != null && ai.Movement.HasReachedDestination)
        {
            ai.Movement.GoToNextPatrolNode();
        }
    }

    protected virtual bool CheckPerceptions()
    {
        if (ai.IsSmallDino)
        {
            if (ai.CanSeeBigDino(out EnemyAI bigDino))
            {
                ai.NotifyPackFlee(bigDino);
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
        }
        else
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
        }

        return false;
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

    public virtual void Exit()
    {
    }
}
