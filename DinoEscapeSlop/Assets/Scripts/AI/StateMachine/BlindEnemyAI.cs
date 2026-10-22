using UnityEngine;

public class BlindEnemyAI : EnemyAI
{
    [Header("Audición y Sonido")]
    [Tooltip("Multiplicador general de la sensibilidad auditiva del dinosaurio ciego.")]
    public float hearingSensitivityMultiplier = 1.0f;

    [Tooltip("Radio de detección de sonido cuando el jugador corre (doble toque W).")]
    public float runNoiseRadius = 15.0f;

    [Tooltip("Radio de detección de sonido cuando el jugador camina normalmente.")]
    public float walkNoiseRadius = 6.5f;

    [Tooltip("Radio de detección de sonido cuando el jugador va agachado (Shift sigilo).")]
    public float crouchNoiseRadius = 2.0f;

    [Tooltip("Radio de detección de sonido cuando el jugador salta o aterriza.")]
    public float jumpNoiseRadius = 10.0f;

    [Tooltip("Umbral de intensidad a partir del cual el sonido provoca persecución directa en vez de investigación.")]
    public float highUrgencyThreshold = 1.0f;

    [Header("Tiempos e Investigación")]
    [Tooltip("Velocidad de movimiento mientras investiga un ruido.")]
    public float investigateSpeed = 3.6f;

    [Tooltip("Tiempo máximo de silencio en persecución antes de volver a investigar.")]
    public float lostSoundDuration = 2.5f;

    [Tooltip("Tiempo que permanece rastreando y olfateando en el lugar del sonido.")]
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

        PatrolState = BlindPatrol;
        ChaseState = BlindChase;

        StateMachine.AllowState(BlindPatrol);
        StateMachine.AllowState(BlindInvestigate);
        StateMachine.AllowState(BlindChase);
    }

    protected override void Start()
    {
        EnsurePlayerReference();
        if (Movement != null) Movement.speed = patrolSpeed;
        SetVisualState(DinoVisualState.Normal);
        StateMachine.Initialize(BlindPatrol);
    }

    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
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

    public bool CheckForSounds(out Vector3 soundPos, out float intensity, out Transform soundSource)
    {
        soundPos = Vector3.zero;
        intensity = 0f;
        soundSource = null;

        // 1. Detección del jugador por ruido físico
        EnsurePlayerReference();
        if (Player != null)
        {
            FirstPersonMovement fpm = Player.GetComponent<FirstPersonMovement>() ??
                                     Player.GetComponentInParent<FirstPersonMovement>() ??
                                     Player.GetComponentInChildren<FirstPersonMovement>();

            Rigidbody rb = fpm != null ? fpm.GetComponent<Rigidbody>() : Player.GetComponent<Rigidbody>();
            float horizSpeed = rb != null ? new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude : 0f;

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

            if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 2.0f)
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
                    return true;
                }
            }
        }

        // 2. Detección de objetos sonoros (RastreoSonoro)
        for (int i = 0; i < RastreoSonoro.Activos.Count; i++)
        {
            RastreoSonoro fuente = RastreoSonoro.Activos[i];
            if (fuente == null || !fuente.gameObject.activeInHierarchy) continue;

            bool isEmittingSound = fuente.Recogido || (fuente.sonido != null && fuente.sonido.isPlaying);
            if (!isEmittingSound) continue;

            float dist = Vector3.Distance(transform.position, fuente.transform.position);
            if (dist <= fuente.radioSonoro * hearingSensitivityMultiplier)
            {
                soundPos = fuente.transform.position;
                intensity = 1.4f;
                soundSource = fuente.transform;
                LastHeardTime = Time.time;
                return true;
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
