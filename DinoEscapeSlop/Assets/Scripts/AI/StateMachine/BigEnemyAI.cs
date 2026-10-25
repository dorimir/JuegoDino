using UnityEngine;

/// <summary>
/// Inteligencia Artificial específica para el dinosaurio grande (T-Rex / depredador supremo).
/// Caza al jugador y a los dinosaurios medianos. Ignora a los pequeños. Nunca huye de nadie.
/// </summary>
public class BigEnemyAI : EnemyAI
{
    protected override void DeterminePreyOrPredator()
    {
        Tier = DinoTier.Big;
    }

    protected override void SetupDefaultStats()
    {
        Tier = DinoTier.Big;

        // Limpiar referencias de manada (el grande no tiene manada)
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();

        // Estadísticas del depredador supremo: imponente, deliberado, imparable
        visionAngle   = 100f;
        visionRange   = 14.0f;
        DetectionRange = 14.0f;
        eyeHeight     = 1.8f;
        AttackRange   = 3.2f;

        patrolSpeed   = 1.6f;
        chaseSpeed    = 5.5f;
        fleeSpeed     = 0f;    // El grande NUNCA huye
        SafeRange     = 0f;    // Sin rango de seguridad
    }

    protected override void SetupStateMachine()
    {
        Tier = DinoTier.Big;
        UpdateStaticCategory();

        StateMachine = new StateMachine();

        PatrolState bigPatrol = new PatrolState(this);
        ChaseState  bigChase  = new ChaseState(this);

        PatrolState    = bigPatrol;
        ChaseState     = bigChase;
        FleeState      = null;   // Sin estado de huida
        FollowLeaderState = null;

        StateMachine.AllowState(bigPatrol);
        StateMachine.AllowState(bigChase);
    }

    protected override void Start()
    {
        EnsurePlayerReference();

        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
        }

        SetVisualState(DinoVisualState.Normal);
        StateMachine.Initialize(PatrolState);
    }

    public override void TriggerFlee(EnemyAI threat)
    {
        // El dinosaurio grande NUNCA huye
    }

    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (target == null) return;

        // El grande caza al jugador y a los medianos; ignora a los pequeños
        if (targetDino != null && !targetDino.IsMediumDino) return;

        CurrentChaseTarget = target;
        TargetDino = targetDino;

        if (targetDino != null && targetDino.gameObject.activeInHierarchy)
        {
            targetDino.NotifyPackFlee(this);
        }

        if (StateMachine != null && ChaseState != null)
        {
            if (StateMachine.CurrentState != ChaseState) StateMachine.ChangeState(ChaseState);
            else ChaseState.Enter();
        }
    }

    public override void NotifyPackFlee(EnemyAI threat)
    {
        // El grande no huye ni avisa a nadie
    }

    public override void TriggerPatrol()
    {
        if (IsPriorityChaseActive) return;
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine == null) return;
        if (PatrolState != null && StateMachine.CurrentState != PatrolState)
        {
            StateMachine.ChangeState(PatrolState);
        }
    }
}
