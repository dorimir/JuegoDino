using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inteligencia Artificial específica para los dinosaurios pequeños (presas/velociraptores).
/// Hereda de EnemyAI y maneja comportamiento de manada, seguimiento a líderes,
/// amplio campo de visión periférica y estados de patrulla, persecución y huida.
/// </summary>
public class SmallEnemyAI : EnemyAI
{
    [Header("Referencias de Estados de Presa / Dinosaurio Pequeño")]
    public PreyPatrolState SmallPatrol { get; protected set; }
    public PreyChaseState SmallChase { get; protected set; }
    public PreyFollowLeaderState SmallFollowLeader { get; protected set; }

    protected override void SetupDefaultStats()
    {
        IsPrey = true;
        if (leaderTransform != null && LeaderAI == null)
        {
            LeaderAI = leaderTransform.GetComponent<EnemyAI>();
        }
        else if (isLeader)
        {
            LeaderAI = null;
        }
        packFollowers.Clear();

        // Visión periférica amplia típica de dinosaurios pequeños/presas
        visionAngle = 280f;
        visionRange = 11.5f;
        DetectionRange = 11.5f;
        eyeHeight = 0.35f;
        AttackRange = 1.3f;

        // Velocidades ágiles
        patrolSpeed = 5.2f;
        chaseSpeed = 9.8f;
        fleeSpeed = 13.0f;
        SafeRange = 12.0f;

        // Tiempos de persecución y desiste
        preyMaxChaseTime = 3.5f;
        preyLostSightTime = 2.0f;
        playerLostSightTime = 5.0f;
        preyChaseCooldown = 3.0f;

        // Parámetros de flocking en manada
        FlockRadius = 10f;
        TooCloseRadius = 2.5f;
        LeaderFollowWeight = 3f;
        SeparationWeight = 1.5f;
        CohesionWeight = 1.5f;
        AlignmentWeight = 1f;
    }

    protected override void SetupStateMachine()
    {
        StateMachine = new StateMachine();
        SmallPatrol = new PreyPatrolState(this);
        SmallChase = new PreyChaseState(this);
        FleeState = new FleeState(this);
        SmallFollowLeader = new PreyFollowLeaderState(this);

        PatrolState = SmallPatrol;
        ChaseState = SmallChase;
        FollowLeaderState = SmallFollowLeader;

        StateMachine.AllowState(SmallPatrol);
        StateMachine.AllowState(SmallChase);
        StateMachine.AllowState(FleeState);
        StateMachine.AllowState(SmallFollowLeader);
    }

    protected override void Start()
    {
        EnsurePlayerReference();

        if (leaderTransform != null)
        {
            LeaderAI = leaderTransform.GetComponent<EnemyAI>();
            if (LeaderAI != null)
            {
                isLeader = false;
                if (!LeaderAI.packFollowers.Contains(this))
                {
                    LeaderAI.packFollowers.Add(this);
                }
            }
        }
        else if (isLeader)
        {
            LeaderAI = null;
        }

        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
            Movement.SnapToGround(true);
        }
        SetVisualState(DinoVisualState.Normal);

        if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            StateMachine.Initialize(SmallFollowLeader);
        }
        else
        {
            StateMachine.Initialize(SmallPatrol);
        }
    }

    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (target == null) return;
        if (IsPriorityChaseActive && targetDino != null) return;
        CurrentChaseTarget = target;
        TargetDino = targetDino;

        if (StateMachine != null && SmallChase != null)
        {
            if (StateMachine.CurrentState != SmallChase) StateMachine.ChangeState(SmallChase);
            else SmallChase.Enter();
        }
    }

    public override void TriggerFlee(EnemyAI threat)
    {
        if (IsPriorityChaseActive) return; // La persecución por alarma tiene prioridad absoluta sobre huir
        if (threat == null || threat == this) return;
        if (threat is BlindEnemyAI) return; // Los pequeños no huyen de los ciegos
        if (threat == LeaderAI || threat.EffectiveLeader == EffectiveLeader) return;
        if (threat.IsPrey || threat is SmallEnemyAI) return;

        CurrentThreat = threat.transform;
        ChasingPredator = threat;
        SpottedThreatPosition = threat.transform.position;

        if (StateMachine != null && FleeState != null && StateMachine.CurrentState != FleeState)
        {
            StateMachine.ChangeState(FleeState);
        }
    }

    public override void TriggerPatrol()
    {
        if (IsPriorityChaseActive) return; // La persecución por alarma tiene prioridad sobre patrullar
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine == null) return;

        if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy && SmallFollowLeader != null)
        {
            if (StateMachine.CurrentState != SmallFollowLeader) StateMachine.ChangeState(SmallFollowLeader);
        }
        else if (SmallPatrol != null)
        {
            if (StateMachine.CurrentState != SmallPatrol) StateMachine.ChangeState(SmallPatrol);
        }
    }
}
