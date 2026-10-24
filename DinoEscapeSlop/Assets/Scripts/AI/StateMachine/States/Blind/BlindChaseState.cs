using UnityEngine;

public class BlindChaseState : ChaseState
{
    protected BlindEnemyAI blindAI;
    protected Vector3 targetSoundPos;

    public BlindChaseState(BlindEnemyAI ai) : base(ai)
    {
        blindAI = ai;
    }

    public override void Enter()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Chasing);
        lostSightTimer = 0f;
        repathTimer = 0f;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.chaseSpeed;
        }

        targetSoundPos = (blindAI != null) ? blindAI.LastHeardSoundPos : Vector3.zero;
        if (targetSoundPos == Vector3.zero)
        {
            ai.EnsurePlayerReference();
            targetSoundPos = (ai.Player != null) ? ai.Player.position : ai.transform.position;
        }

        if (ai.Movement != null)
        {
            ai.Movement.MoveTo(targetSoundPos);
        }
    }

    public override void Update()
    {
        ai.EnsurePlayerReference();
        if (ai.Player != null)
        {
            float distToPlayer = Vector2.Distance(
                new Vector2(ai.transform.position.x, ai.transform.position.z),
                new Vector2(ai.Player.position.x, ai.Player.position.z)
            );
            float vDiff = Mathf.Abs(ai.transform.position.y - ai.Player.position.y);

            if (distToPlayer <= ai.AttackRange && vDiff < 2.0f)
            {
                ai.KillPlayer();
                return;
            }
        }

        Vector3 soundPos = Vector3.zero;
        float intensity = 0f;
        Transform soundSource = null;
        bool heardSound = (blindAI != null) && blindAI.CheckForSounds(out soundPos, out intensity, out soundSource);
        if (heardSound)
        {
            lostSightTimer = 0f;
            targetSoundPos = soundPos;
            if (blindAI != null) blindAI.LastHeardSoundPos = soundPos;
        }
        else
        {
            lostSightTimer += Time.deltaTime;
            float maxLost = (blindAI != null) ? blindAI.lostSoundDuration : 2.5f;
            if (lostSightTimer >= maxLost)
            {
                if (ai.IsPriorityChaseActive)
                {
                    // En persecución prioritaria por alarma no se degrada a investigar;
                    // se mantiene dirigiéndose hacia la posición conocida del jugador
                    if (lostSightTimer >= ai.playerLostSightTime)
                    {
                        EndChase();
                        return;
                    }
                }
                else
                {
                    if (blindAI != null) blindAI.TriggerInvestigate(targetSoundPos);
                    else EndChase();
                    return;
                }
            }
        }

        MoveTowardsTarget(targetSoundPos, heardSound);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
