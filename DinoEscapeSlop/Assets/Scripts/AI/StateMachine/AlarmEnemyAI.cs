using System.Collections.Generic;
using UnityEngine;

public class AlarmEnemyAI : EnemyAI
{
    [Header("Configuración de Alerta y Rango")]

    public float alertRadius = 100.0f;

    public bool autoCalculateHalfMapRadius = false;

    public float alertBroadcastInterval = 1.0f;

    public float lostSightDuration = 4.0f;

    public float turnSpeedWhenAlerting = 180f;

    public bool ignorePrey = true;

    [Header("Audio de Alarma")]

    public AudioClip alarmSound;

    public AudioSource alarmAudioSource;

    public AlarmPatrolState AlarmPatrol { get; protected set; }
    public AlarmState AlarmAlert { get; protected set; }

    private readonly List<EnemyAI> lastAlertedDinos = new List<EnemyAI>();

    protected override void DeterminePreyOrPredator()
    {
        IsPrey = false;
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();
    }

    protected override void SetupDefaultStats()
    {
        IsPrey = false;
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();

        visionAngle = 110f;
        visionRange = 12.0f;
        DetectionRange = 12.0f;
        LoseRange = 18.0f;
        eyeHeight = 0.85f;
        AttackRange = 2.0f;

        patrolSpeed = 2.4f;
        chaseSpeed = 0f;
        fleeSpeed = 8.0f;
        SafeRange = 16.0f;
    }

    protected override void SetupStateMachine()
    {
        StateMachine = new StateMachine();
        AlarmPatrol = new AlarmPatrolState(this);
        AlarmAlert = new AlarmState(this);
        FleeState = new FleeState(this);

        PatrolState = AlarmPatrol;
        ChaseState = AlarmAlert;
        FollowLeaderState = null;

        StateMachine.AllowState(AlarmPatrol);
        StateMachine.AllowState(AlarmAlert);
        StateMachine.AllowState(FleeState);
    }

    protected override void Awake()
    {
        base.Awake();

        if (alarmAudioSource == null)
        {
            alarmAudioSource = GetComponent<AudioSource>();
        }

        if (autoCalculateHalfMapRadius)
        {
            CalculateHalfMapRadius();
        }
    }

    protected override void Start()
    {
        base.Start();

        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
            Movement.SnapToGround(true);
        }

        SetVisualState(DinoVisualState.Normal);
        StateMachine.Initialize(AlarmPatrol);
    }

    public void CalculateHalfMapRadius()
    {
        if (Pathfinding == null) Pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();
        if (Pathfinding == null) return;

        Node[] nodes = Pathfinding.GetAllNodes();
        if (nodes == null || nodes.Length == 0) return;

        Vector3 min = nodes[0].transform.position;
        Vector3 max = nodes[0].transform.position;

        for (int i = 1; i < nodes.Length; i++)
        {
            if (nodes[i] == null) continue;
            min = Vector3.Min(min, nodes[i].transform.position);
            max = Vector3.Max(max, nodes[i].transform.position);
        }

        Vector3 size = max - min;
        float halfExtent = Mathf.Max(size.x, size.z) * 0.5f;
        if (halfExtent > 10f)
        {
            alertRadius = halfExtent;
        }
    }

    public override void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (target == null || (ignorePrey && targetDino != null)) return;

        CurrentChaseTarget = target;
        TargetDino = targetDino;

        if (StateMachine != null && AlarmAlert != null)
        {
            if (StateMachine.CurrentState != AlarmAlert) StateMachine.ChangeState(AlarmAlert);
            else AlarmAlert.Enter();
        }
    }

    public override void TriggerPatrol()
    {
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine != null && AlarmPatrol != null && StateMachine.CurrentState != AlarmPatrol)
        {
            StateMachine.ChangeState(AlarmPatrol);
        }
    }

    public virtual void AlertNearbyDinos(Vector3 playerPos)
    {
        EnsurePlayerReference();
        Transform targetTransform = (Player != null) ? Player : null;
        if (targetTransform == null) return;

        lastAlertedDinos.Clear();

        for (int i = 0; i < AllEnemies.Count; i++)
        {
            EnemyAI dino = AllEnemies[i];
            if (dino == null || !dino.gameObject.activeInHierarchy || dino == this || dino is AlarmEnemyAI) continue;

            float dist = Vector3.Distance(transform.position, dino.transform.position);
            if (dist <= alertRadius)
            {
                lastAlertedDinos.Add(dino);
                NotifyDinoToChase(dino, targetTransform, playerPos);
            }
        }
    }

    protected virtual void NotifyDinoToChase(EnemyAI dino, Transform targetTransform, Vector3 playerPos)
    {
        if (dino == null || targetTransform == null) return;
        dino.TriggerAlarmChase(targetTransform, playerPos);
    }

    public virtual void PlayAlarmAudio()
    {
        if (alarmSound == null) return;

        if (alarmAudioSource == null)
        {
            alarmAudioSource = gameObject.AddComponent<AudioSource>();
            alarmAudioSource.spatialBlend = 1.0f;
            alarmAudioSource.maxDistance = alertRadius;
            alarmAudioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        if (!alarmAudioSource.isPlaying)
        {
            alarmAudioSource.clip = alarmSound;
            alarmAudioSource.loop = true;
            alarmAudioSource.Play();
        }
    }

    public override void TriggerScaredByLight(Transform threatPlayer)
    {
        StopAlarmAudio();
        base.TriggerScaredByLight(threatPlayer);
    }

    public virtual void StopAlarmAudio()
    {
        if (alarmAudioSource != null && alarmAudioSource.isPlaying)
        {
            alarmAudioSource.Stop();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, alertRadius);

        if (StateMachine != null && StateMachine.CurrentState == AlarmAlert)
        {
            Gizmos.color = new Color(1f, 0.35f, 0f, 0.85f);
            for (int i = 0; i < lastAlertedDinos.Count; i++)
            {
                if (lastAlertedDinos[i] != null && lastAlertedDinos[i].gameObject.activeInHierarchy)
                {
                    Gizmos.DrawLine(transform.position + Vector3.up, lastAlertedDinos[i].transform.position + Vector3.up);
                }
            }
        }
    }
}

public class ScreamerEnemyAI : AlarmEnemyAI
{
}
