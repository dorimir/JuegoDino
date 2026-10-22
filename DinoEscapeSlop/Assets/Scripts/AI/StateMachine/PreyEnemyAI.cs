using UnityEngine;

public class PreyEnemyAI : EnemyAI
{
    public PreyPatrolState PreyPatrol { get; protected set; }
    public PreyChaseState PreyChase { get; protected set; }
    public PreyFollowLeaderState PreyFollowLeader { get; protected set; }

    protected override void SetupDefaultStats()
    {
        IsPrey = true;
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();

        visionAngle = 280f;
        visionRange = 11.5f;
        DetectionRange = 11.5f;
        eyeHeight = 0.35f;
        AttackRange = 1.3f;
        patrolSpeed = 5.2f;
        chaseSpeed = 9.8f;
        fleeSpeed = 13.0f;
    }

    protected override void SetupStateMachine()
    {
        StateMachine = new StateMachine();
        PreyPatrol = new PreyPatrolState(this);
        PreyChase = new PreyChaseState(this);
        FleeState = new FleeState(this);
        PreyFollowLeader = new PreyFollowLeaderState(this);

        PatrolState = PreyPatrol;
        ChaseState = PreyChase;
        FollowLeaderState = PreyFollowLeader;

        StateMachine.AllowState(PreyPatrol);
        StateMachine.AllowState(PreyChase);
        StateMachine.AllowState(FleeState);
        StateMachine.AllowState(PreyFollowLeader);
    }

    protected override void Start()
    {
        EnsurePlayerReference();

        if (!hasDistributedPrey)
        {
            hasDistributedPrey = true;
            PreyDistribution.DistributePreyAcrossMap(AllPrey);
        }

        if (Movement != null) Movement.speed = patrolSpeed;
        SetVisualState(DinoVisualState.Normal);

        if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            StateMachine.Initialize(PreyFollowLeader);
        }
        else
        {
            StateMachine.Initialize(PreyPatrol);
        }
    }

    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (target == null) return;
        CurrentChaseTarget = target;
        TargetDino = targetDino;
        if (StateMachine != null)
        {
            if (StateMachine.CurrentState != PreyChase) StateMachine.ChangeState(PreyChase);
            else PreyChase.Enter();
        }
    }

    public override void TriggerFlee(EnemyAI threat)
    {
        if (threat == null) return;
        CurrentThreat = threat.transform;
        ChasingPredator = threat;
        SpottedThreatPosition = threat.transform.position;
        if (StateMachine != null && StateMachine.CurrentState != FleeState)
        {
            StateMachine.ChangeState(FleeState);
        }
    }

    public override void TriggerPatrol()
    {
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine == null) return;

        if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            if (StateMachine.CurrentState != PreyFollowLeader) StateMachine.ChangeState(PreyFollowLeader);
        }
        else
        {
            if (StateMachine.CurrentState != PreyPatrol) StateMachine.ChangeState(PreyPatrol);
        }
    }
}
