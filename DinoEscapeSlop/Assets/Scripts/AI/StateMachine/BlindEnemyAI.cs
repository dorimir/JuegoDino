using UnityEngine;

public class BlindEnemyAI : EnemyAI
{
    [Header("Audición y Sonido")]

    public float hearingSensitivityMultiplier = 1.0f;

    public float runNoiseRadius = 15.0f;

    public float walkNoiseRadius = 6.5f;

    public float crouchNoiseRadius = 2.0f;

    public float jumpNoiseRadius = 10.0f;

    public float highUrgencyThreshold = 1.0f;

    [Header("Tiempos e Investigación")]

    public float investigateSpeed = 3.6f;

    public float lostSoundDuration = 2.5f;

    public float investigateWaitDuration = 3.5f;

    public BlindPatrolState BlindPatrol { get; protected set; }
    public BlindInvestigateState BlindInvestigate { get; protected set; }
    public BlindChaseState BlindChase { get; protected set; }

    public Vector3 LastHeardSoundPos { get; set; }
    public Transform LastSoundSource { get; set; }
    public float LastHeardTime { get; set; }

    public override bool CanSeePlayer() => false;
    public override bool CanSeeSpecificDino(EnemyAI targetDino) => false;
    public override bool CanSeeBigDino(out EnemyAI spottedPredator) { spottedPredator = null; return false; }
    public override bool CanSeePrey(out EnemyAI spottedPrey) { spottedPrey = null; return false; }

    protected override void SetupDefaultStats()
    {
        IsPrey = false;
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();

        visionAngle = 0f;
        visionRange = 0f;
        DetectionRange = 0f;
        LoseRange = 0f;
        eyeHeight = 0.85f;
        AttackRange = 2.2f;
        patrolSpeed = 2.2f;
        chaseSpeed = 8.5f;
        fleeSpeed = 8.0f;
    }

    protected override void SetupStateMachine()
    {
        StateMachine = new StateMachine();
        BlindPatrol = new BlindPatrolState(this);
        BlindInvestigate = new BlindInvestigateState(this);
        BlindChase = new BlindChaseState(this);
        FleeState = new FleeState(this);

        PatrolState = BlindPatrol;
        ChaseState = BlindChase;

        StateMachine.AllowState(BlindPatrol);
        StateMachine.AllowState(BlindInvestigate);
        StateMachine.AllowState(BlindChase);

    }

    public override float GetVisionOrFacingCone() => 110f;

    public override void TriggerScaredByLight(Transform threatPlayer)
    {
        return;
    }

    public override void NotifyPackFlee(EnemyAI threat)
    {
        return;
    }

    protected override void Start()
    {
        EnsurePlayerReference();
        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
            Movement.SnapToGround(true);
        }
        SetVisualState(DinoVisualState.Normal);
        StateMachine.Initialize(BlindPatrol);
    }

    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (targetDino != null && targetDino.IsSmallDino) return;
        if (target != null && target.GetComponentInParent<EnemyAI>()?.IsSmallDino == true) return;

        TargetDino = null;

        if (target != null)
        {
            CurrentChaseTarget = target;
            LastSoundSource = target;
            LastHeardSoundPos = target.position;
        }

        if (StateMachine != null && BlindChase != null)
        {
            if (StateMachine.CurrentState != BlindChase) StateMachine.ChangeState(BlindChase);
            else BlindChase.Enter();
        }
    }

    public virtual void TriggerInvestigate(Vector3 soundPos, Transform source = null)
    {
        if (IsPriorityChaseActive) return;
        if (source != null && source.GetComponentInParent<EnemyAI>()?.IsSmallDino == true) return;

        LastHeardSoundPos = soundPos;
        LastSoundSource = source;

        if (StateMachine != null && BlindInvestigate != null)
        {
            if (StateMachine.CurrentState != BlindInvestigate) StateMachine.ChangeState(BlindInvestigate);
            else BlindInvestigate.Enter();
        }
    }

    public override void TriggerPatrol()
    {
        if (IsPriorityChaseActive) return;
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine != null && BlindPatrol != null && StateMachine.CurrentState != BlindPatrol)
        {
            StateMachine.ChangeState(BlindPatrol);
        }
    }

    public override void TriggerFlee(EnemyAI threat)
    {
    }

    public void NotificarSonidoPrioritario(Vector3 soundPos, float intensity, Transform soundSource)
    {
        LastHeardSoundPos = soundPos;
        LastSoundSource = soundSource;
        LastHeardTime = Time.time;

        if (intensity >= highUrgencyThreshold)
        {
            TriggerAttack(soundSource != null ? soundSource : Player);
        }
        else
        {
            TriggerInvestigate(soundPos, soundSource);
        }
    }

    public bool CheckForSounds(out Vector3 soundPos, out float intensity, out Transform soundSource)
    {
        soundPos = Vector3.zero;
        intensity = 0f;
        soundSource = null;

        if (RastreoSonoro.HaySonidoObjetoActivo(transform.position, hearingSensitivityMultiplier, out Vector3 objPos, out float objIntensity, out Transform objSource))
        {
            soundPos = objPos;
            intensity = objIntensity;
            soundSource = objSource;
            LastHeardTime = Time.time;
            LastHeardSoundPos = soundPos;
            LastSoundSource = soundSource;
            return true;
        }

        if (Linterna.HaySonidoCaidaActivo(transform.position, hearingSensitivityMultiplier, out Vector3 linternaPos, out float linternaIntensity, out Transform linternaSource))
        {
            soundPos = linternaPos;
            intensity = linternaIntensity;
            soundSource = linternaSource;
            LastHeardTime = Time.time;
            LastHeardSoundPos = soundPos;
            LastSoundSource = soundSource;
            return true;
        }

        EnsurePlayerReference();
        if (Player != null)
        {
            FirstPersonMovement fpm = Player.GetComponent<FirstPersonMovement>() ??
                                      Player.GetComponentInParent<FirstPersonMovement>() ??
                                      Player.GetComponentInChildren<FirstPersonMovement>();

            Rigidbody rb = (fpm != null) ? fpm.GetComponent<Rigidbody>() : Player.GetComponent<Rigidbody>();
            float horizSpeed = (rb != null) ? new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude : 0f;

            float playerNoiseRadius = 0f;
            float playerIntensity = 0f;

            if (fpm != null && fpm.IsRunning)
            {
                playerNoiseRadius = runNoiseRadius;
                playerIntensity = 1.6f;
            }
            else if (fpm != null && fpm.IsCrouched)
            {
                if (horizSpeed > 0.15f)
                {
                    playerNoiseRadius = crouchNoiseRadius;
                    playerIntensity = 0.35f;
                }
            }
            else if (horizSpeed > 0.2f)
            {
                playerNoiseRadius = walkNoiseRadius;
                playerIntensity = 0.8f;
            }

            if ((rb != null && Mathf.Abs(rb.linearVelocity.y) > 2.0f) || (fpm != null && !fpm.IsGrounded()))
            {
                if (jumpNoiseRadius > playerNoiseRadius)
                {
                    playerNoiseRadius = jumpNoiseRadius;
                    playerIntensity = Mathf.Max(playerIntensity, 1.3f);
                }
            }

            if (playerNoiseRadius > 0f)
            {
                float dist = Vector3.Distance(transform.position, Player.position);
                if (dist <= playerNoiseRadius * hearingSensitivityMultiplier)
                {
                    soundPos = Player.position;
                    intensity = playerIntensity;
                    soundSource = Player;
                    LastHeardTime = Time.time;
                    LastHeardSoundPos = soundPos;
                    LastSoundSource = soundSource;
                    return true;
                }
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.1f, 0.9f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, runNoiseRadius * hearingSensitivityMultiplier);

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, walkNoiseRadius * hearingSensitivityMultiplier);
    }
}
