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

        if (ai.isLeader && ai.packFollowers != null && ai.packFollowers.Count > 0 && ai.Movement != null)
        {
            float maxFollowerDist = 0f;
            for (int i = 0; i < ai.packFollowers.Count; i++)
            {
                if (ai.packFollowers[i] != null && ai.packFollowers[i].gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(ai.transform.position, ai.packFollowers[i].transform.position);
                    if (d > maxFollowerDist) maxFollowerDist = d;
                }
            }

            ai.Movement.speed = (maxFollowerDist > 4.5f) ? (ai.patrolSpeed * 0.45f) : ai.patrolSpeed;
        }

        if (ai.Movement != null && ai.Movement.HasReachedDestination)
        {
            ai.Movement.GoToNextPatrolNode();
        }
    }

    protected virtual bool CheckPerceptions()
    {
        // 1. Detección de amenazas según jerarquía (Pequeño huye de Mediano y Grande; Mediano huye de Grande)
        if (ai.CanSeeThreat(out EnemyAI threat))
        {
            if (ai.IsSmallDino) ai.NotifyPackFlee(threat);
            else ai.TriggerFlee(threat);
            return true;
        }

        // 2. Detección y caza del Jugador (los tres arquetipos cazan al jugador)
        if (ai.CanSeePlayer())
        {
            if (ai.IsSmallDino) ai.NotifyPackAttack(ai.Player);
            else ai.TriggerAttack(ai.Player);
            return true;
        }

        // 3. Detección y caza de presas según jerarquía (Grande caza Mediano; Mediano caza Pequeño)
        if (ai.preyCooldownTimer <= 0f && ai.CanSeeHuntablePrey(out EnemyAI huntablePrey))
        {
            ai.TriggerAttack(huntablePrey.transform, huntablePrey);
            return true;
        }

        // 4. Lógica de manada para dinosaurios pequeños
        if (ai.IsSmallDino)
        {
            CheckMutualEncounters();

            if (ai.LeaderAI != null && ai.LeaderAI.gameObject.activeInHierarchy)
            {
                ai.TriggerPatrol();
                return true;
            }
        }

        return false;
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

    public virtual void Exit()
    {
    }
}
