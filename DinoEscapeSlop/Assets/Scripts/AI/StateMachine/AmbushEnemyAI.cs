using UnityEngine;

/// <summary>
/// Inteligencia Artificial para el dinosaurio emboscador (DinoEmboscador).
/// Permanece oculto en un punto de emboscada (AmbushPoint) esperando a que el jugador pase por delante.
/// Al detectarlo, acecha brevemente y lanza una feroz persecución a gran velocidad.
/// Si el jugador logra escapar perdiendo la línea de visión o distanciándose, el dinosaurio se recupera
/// y regresa navegando al punto de emboscada para reiniciar su acecho.
/// </summary>
public class AmbushEnemyAI : EnemyAI
{
    [Header("Configuración del Punto de Emboscada")]
    [Tooltip("Punto de emboscada asignado donde se ocultará el dinosaurio.")]
    public AmbushPoint assignedAmbushPoint;

    [Tooltip("Si está activo, al escapar el jugador el dinosaurio regresará al punto de emboscada más cercano en lugar de obligatoriamente al asignado.")]
    public bool returnToNearestAmbushPoint = false;

    protected override void DeterminePreyOrPredator()
    {
        IsPrey = false;
        dinoEmboscador = true;
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();
    }

    protected override void SetupDefaultStats()
    {
        IsPrey = false;
        dinoEmboscador = true;
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();

        ambushStalkRange = 9.0f;
        visionRange = ambushStalkRange;
        ambushTriggerRange = 8.0f;
        ambushWarningTime = 0.8f;
        ambushAttackDuration = 7.0f;
        ambushAttackSpeed = 10.5f;
        ambushTurnRate = 160f;
        ambushTiredTime = 2.5f;

        visionAngle = 110f;
        AttackRange = 2.0f;
        eyeHeight = 0.85f;
        patrolSpeed = 3.0f;
        chaseSpeed = ambushAttackSpeed;
        LoseRange = 16.0f;
        playerLostSightTime = 3.5f;

        if (assignedAmbushPoint != null)
        {
            AmbushHomePos = assignedAmbushPoint.Position;
            AmbushHomeRot = assignedAmbushPoint.Rotation;
        }
        else
        {
            AmbushHomePos = transform.position;
            AmbushHomeRot = transform.rotation;
        }
    }

    protected override void SetupStateMachine()
    {
        StateMachine = new StateMachine();

        AmbushHiddenState = new AmbushHiddenState(this);
        AmbushStalkState = new AmbushStalkState(this);
        AmbushRecoverState = new AmbushRecoverState(this);
        ChaseState = new ChaseState(this);
        FleeState = new FleeState(this);

        StateMachine.AllowState(AmbushHiddenState);
        StateMachine.AllowState(AmbushStalkState);
        StateMachine.AllowState(AmbushRecoverState);
        StateMachine.AllowState(ChaseState);
        StateMachine.AllowState(FleeState);
    }

    protected override void Start()
    {
        EnsurePlayerReference();

        if (assignedAmbushPoint == null)
        {
            AmbushPoint nearest = AmbushPoint.GetNearest(transform.position, this);
            if (nearest != null && Vector3.Distance(transform.position, nearest.Position) <= 4.0f)
            {
                SetAmbushPoint(nearest);
            }
        }

        if (assignedAmbushPoint != null)
        {
            transform.position = assignedAmbushPoint.Position;
            transform.rotation = assignedAmbushPoint.Rotation;
            AmbushHomePos = assignedAmbushPoint.Position;
            AmbushHomeRot = assignedAmbushPoint.Rotation;
            assignedAmbushPoint.assignedEnemy = this;
        }
        else
        {
            AmbushHomePos = transform.position;
            AmbushHomeRot = transform.rotation;
        }

        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
            Movement.ClearPath();
            Movement.ResetVelocity();
        }

        SetVisualState(DinoVisualState.Normal);
        StateMachine.Initialize(AmbushHiddenState);
    }

    public void SetAmbushPoint(AmbushPoint point)
    {
        assignedAmbushPoint = point;
        if (point != null)
        {
            point.assignedEnemy = this;
            AmbushHomePos = point.Position;
            AmbushHomeRot = point.Rotation;
        }
    }

    public AmbushPoint FindNearestAmbushPoint()
    {
        return AmbushPoint.GetNearest(transform.position, this);
    }

    public override void TriggerPatrol()
    {
        if (IsPriorityChaseActive) return;
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine != null && AmbushRecoverState != null)
        {
            if (StateMachine.CurrentState != AmbushRecoverState)
            {
                StateMachine.ChangeState(AmbushRecoverState);
            }
        }
    }

    /// <summary>
    /// El dinosaurio emboscador no huye de la luz de la linterna.
    /// Al ser iluminado directamente, descubre de inmediato al jugador e inicia el ataque.
    /// </summary>
    public override void TriggerScaredByLight(Transform threatPlayer)
    {
        base.TriggerScaredByLight(threatPlayer);
    }

    /// <summary>
    /// El dinosaurio emboscador no busca ni persigue dinosaurios pequeños / presas.
    /// Se enfoca exclusivamente en esperar y emboscar al jugador.
    /// </summary>
    public override bool CanSeePrey(out EnemyAI spottedPrey)
    {
        spottedPrey = null;
        return false;
    }

    /// <summary>
    /// El dinosaurio emboscador solo ataca al jugador; ignora cualquier objetivo de dinosaurio pequeño.
    /// </summary>
    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (targetDino != null && targetDino.IsSmallDino) return;
        if (target != null && target.GetComponentInParent<EnemyAI>()?.IsSmallDino == true) return;

        TargetDino = null;
        base.TriggerAttack(target, null);
    }

    /// <summary>
    /// El dinosaurio emboscador ignora las llamadas de huida de otros dinosaurios.
    /// </summary>
    public override void NotifyPackFlee(EnemyAI threat)
    {
        // No responde a la huida de la manada
    }
}
