using UnityEngine;

public class BlindInvestigateState : IState
{
    protected BlindEnemyAI ai;
    protected Vector3 targetSoundPos;
    protected float waitTimer;
    protected bool reachedTarget;
    protected float repathTimer;
    protected float sweepTimer;
    protected Quaternion initialSweepRotation;

    public BlindInvestigateState(BlindEnemyAI ai)
    {
        this.ai = ai;
    }

    public virtual void Enter()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Alert);
        targetSoundPos = ai.LastHeardSoundPos;
        waitTimer = 0f;
        reachedTarget = false;
        repathTimer = 0f;
        sweepTimer = 0f;
        initialSweepRotation = ai.transform.rotation;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.investigateSpeed;
            if (targetSoundPos != Vector3.zero)
            {
                ai.Movement.MoveTo(targetSoundPos);
            }
        }
    }

    public virtual void Update()
    {
        if (ai.CheckForSounds(out Vector3 newSoundPos, out float intensity, out Transform soundSource))
        {
            if (soundSource != null)
            {
                EnemyAI sourceAI = soundSource.GetComponentInParent<EnemyAI>();
                if (sourceAI != null && sourceAI.IsSmallDino) return;
            }

            if (intensity >= ai.highUrgencyThreshold)
            {
                ai.TriggerAttack(soundSource != null ? soundSource : ai.Player);
                return;
            }

            targetSoundPos = newSoundPos;
            ai.LastHeardSoundPos = newSoundPos;
            reachedTarget = false;
            waitTimer = 0f;

            if (ai.Movement != null)
            {
                ai.Movement.MoveTo(targetSoundPos);
            }
        }

        if (!reachedTarget)
        {
            float dist2D = Vector2.Distance(
                new Vector2(ai.transform.position.x, ai.transform.position.z),
                new Vector2(targetSoundPos.x, targetSoundPos.z)
            );

            if (dist2D <= 1.5f || (ai.Movement != null && ai.Movement.HasReachedDestination))
            {
                reachedTarget = true;
                waitTimer = 0f;
                initialSweepRotation = ai.transform.rotation;
                if (ai.Movement != null)
                {
                    ai.Movement.ClearPath();
                    ai.Movement.ResetVelocity();
                }
            }
            else
            {
                repathTimer -= Time.deltaTime;
                if (repathTimer <= 0f)
                {
                    repathTimer = 0.4f;
                    if (ai.Movement != null)
                    {
                        ai.Movement.MoveTo(targetSoundPos);
                    }
                }
            }
        }
        else
        {
            sweepTimer += Time.deltaTime;
            float angle = Mathf.Sin(sweepTimer * 2.2f) * 55f;
            Quaternion targetRot = initialSweepRotation * Quaternion.Euler(0f, angle, 0f);
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, targetRot, 90f * Time.deltaTime);

            waitTimer += Time.deltaTime;
            if (waitTimer >= ai.investigateWaitDuration)
            {
                ai.TriggerPatrol();
            }
        }
    }

    public virtual void Exit()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
    }
}
