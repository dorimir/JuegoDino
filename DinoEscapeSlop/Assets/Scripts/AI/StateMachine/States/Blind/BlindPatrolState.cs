using UnityEngine;

public class BlindPatrolState : PatrolState
{
    protected BlindEnemyAI blindAI;

    public BlindPatrolState(BlindEnemyAI ai) : base(ai)
    {
        blindAI = ai;
    }

    public override void Update()
    {
        if (CheckPerceptions()) return;

        if (ai.Movement != null && ai.Movement.HasReachedDestination)
        {
            ai.Movement.GoToNextPatrolNode();
        }
    }

    protected override bool CheckPerceptions()
    {
        if (blindAI != null && blindAI.CheckForSounds(out Vector3 soundPos, out float intensity, out Transform soundSource))
        {
            if (soundSource != null && soundSource.GetComponentInParent<EnemyAI>()?.IsSmallDino == true)
            {
                return false;
            }

            if (intensity >= blindAI.highUrgencyThreshold)
            {
                blindAI.TriggerAttack(soundSource != null ? soundSource : blindAI.Player);
            }
            else
            {
                blindAI.TriggerInvestigate(soundPos, soundSource);
            }
            return true;
        }

        return false;
    }
}
