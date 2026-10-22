using UnityEngine;

public class PredatorChaseState : ChaseState
{
    protected float chaseDurationTimer;

    public PredatorChaseState(EnemyAI ai) : base(ai)
    {
    }

    public override void Enter()
    {
        base.Enter();
        chaseDurationTimer = 0f;
        if (ai.TargetDino != null)
        {
            lastKnownTargetPos = ai.TargetDino.transform.position;
        }
    }

    public override void Update()
    {
        if (ai.TargetDino != null)
        {
            if (ai.CanSeePlayer())
            {
                ai.TriggerAttack(ai.Player);
                return;
            }

            if (!ai.TargetDino.gameObject.activeInHierarchy)
            {
                EndChase();
                return;
            }

            chaseDurationTimer += Time.deltaTime;
            float maxChase = (ai.preyMaxChaseTime > 0f) ? ai.preyMaxChaseTime : 3.5f;
            if (chaseDurationTimer >= maxChase)
            {
                EndChase();
                return;
            }

            float dist = Vector2.Distance(
                new Vector2(ai.transform.position.x, ai.transform.position.z),
                new Vector2(ai.TargetDino.transform.position.x, ai.TargetDino.transform.position.z)
            );
            float verticalDiff = Mathf.Abs(ai.transform.position.y - ai.TargetDino.transform.position.y);

            if (dist <= ai.AttackRange && verticalDiff < 2.0f)
            {
                ai.TargetDino.Die();
                EndChase();
                return;
            }

            bool preyVisible = ai.CanSeeSpecificDino(ai.TargetDino);
            if (preyVisible)
            {
                lostSightTimer = 0f;
                lastKnownTargetPos = ai.TargetDino.transform.position;
            }
            else
            {
                lostSightTimer += Time.deltaTime;
                float lostLimit = (ai.preyLostSightTime > 0f) ? ai.preyLostSightTime : 2.0f;
                if (lostSightTimer >= lostLimit)
                {
                    EndChase();
                    return;
                }
            }

            MoveTowardsTarget(lastKnownTargetPos, preyVisible);
            return;
        }

        base.Update();
    }

    public override void EndChase()
    {
        if (ai.TargetDino != null)
        {
            ai.preyCooldownTimer = ai.preyChaseCooldown;
        }
        base.EndChase();
    }
}
