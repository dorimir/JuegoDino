using UnityEngine;

public class FollowLeaderState : IState
{
    protected EnemyAI ai;
    protected float repathTimer;
    protected const float RepathInterval = 0.3f;

    public FollowLeaderState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public virtual void Enter()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        repathTimer = 0f;
        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
        }
    }

    public virtual void Update()
    {
        EnemyAI leader = ai.LeaderAI;
        if (leader == null || !leader.gameObject.activeInHierarchy)
        {
            ai.PromoteToLeader();
            return;
        }

        if (ai.Movement == null) return;

        int slotIndex = leader.packFollowers.IndexOf(ai);
        if (slotIndex < 0)
        {
            leader.packFollowers.Add(ai);
            slotIndex = leader.packFollowers.IndexOf(ai);
        }

        int row = (slotIndex / 2) + 1;
        float side = (slotIndex % 2 == 0) ? -1f : 1f;

        float backDist = 1.8f * row;
        float lateralDist = side * (0.6f + (row - 1) * 0.25f);

        Vector3 leaderPos = leader.transform.position;
        Vector3 leaderFwd = leader.transform.forward;
        Vector3 leaderRight = leader.transform.right;

        Vector3 targetPos = leaderPos - (leaderFwd * backDist) + (leaderRight * lateralDist);
        targetPos.y = ai.transform.position.y;

        if (!ai.Movement.HasClearLineTo(targetPos, 0.15f))
        {
            targetPos = leaderPos - (leaderFwd * backDist);
            targetPos.y = ai.transform.position.y;
        }

        float distToLeader = Vector3.Distance(ai.transform.position, leaderPos);
        float distToSlot = Vector3.Distance(ai.transform.position, targetPos);

        if (distToLeader > 4.5f)
        {
            ai.Movement.speed = ai.patrolSpeed * 1.35f;
        }
        else if (distToSlot < 0.35f)
        {
            float leaderSpeed = (leader.Movement != null) ? leader.Movement.speed : ai.patrolSpeed;
            ai.Movement.speed = leaderSpeed;

            if (leader.Movement != null && leader.Movement.Velocity.magnitude > 0.1f)
            {
                ai.transform.rotation = Quaternion.RotateTowards(
                    ai.transform.rotation,
                    Quaternion.Euler(0f, leader.transform.eulerAngles.y, 0f),
                    ai.Movement.patrolTurnRate * Time.deltaTime
                );
            }
        }
        else if (distToLeader < 1.3f)
        {
            ai.Movement.speed = ai.patrolSpeed * 0.5f;
        }
        else
        {
            ai.Movement.speed = ai.patrolSpeed;
        }

        if (ai.Movement.HasClearLineTo(targetPos, 0.15f))
        {
            ai.Movement.MoveTo(targetPos);
        }
        else
        {
            repathTimer += Time.deltaTime;
            if (repathTimer >= RepathInterval || ai.Movement.HasReachedDestination)
            {
                repathTimer = 0f;
                ai.Movement.MoveTo(leaderPos);
            }
        }
    }

    public virtual void Exit()
    {
    }
}
