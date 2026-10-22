using UnityEngine;

public class PredatorEnemyAI : EnemyAI
{
    public PredatorPatrolState PredatorPatrol { get; protected set; }
    public PredatorChaseState PredatorChase { get; protected set; }

    protected override void SetupDefaultStats()
    {
        IsPrey = false;
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();

        visionAngle = 100f;
        visionRange = 8.5f;
        eyeHeight = 0.85f;
        DetectionRange = 8.5f;
        LoseRange = 18f;
        AttackRange = 2.0f;
        patrolSpeed = 2.4f;
        chaseSpeed = 7.5f;
        fleeSpeed = 8.0f;
    }

    protected override void SetupStateMachine()
    {
        StateMachine = new StateMachine();
        PredatorPatrol = new PredatorPatrolState(this);
        PredatorChase = new PredatorChaseState(this);

        PatrolState = PredatorPatrol;
        ChaseState = PredatorChase;

        StateMachine.AllowState(PredatorPatrol);
        StateMachine.AllowState(PredatorChase);
    }

    protected override void Start()
    {
        EnsurePlayerReference();
        if (Movement != null) Movement.speed = patrolSpeed;
        SetVisualState(DinoVisualState.Normal);
        StateMachine.Initialize(PredatorPatrol);
    }

    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (target == null) return;
        CurrentChaseTarget = target;
        TargetDino = targetDino;

        if (targetDino != null && targetDino.gameObject.activeInHierarchy)
        {
            targetDino.NotifyPackFlee(this);
        }

        if (StateMachine != null)
        {
            if (StateMachine.CurrentState != PredatorChase) StateMachine.ChangeState(PredatorChase);
            else PredatorChase.Enter();
        }
    }

    public override void TriggerPatrol()
    {
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;
        if (StateMachine != null && StateMachine.CurrentState != PredatorPatrol)
        {
            StateMachine.ChangeState(PredatorPatrol);
        }
    }
}
