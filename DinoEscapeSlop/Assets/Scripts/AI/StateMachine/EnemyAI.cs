using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyAI : MonoBehaviour
{
    public enum DinoTier
    {
        Small,   // Dinosaurio Pequeño (1/3 escala, manada)
        Medium,  // Dinosaurio Mediano (escala normal)
        Big      // Dinosaurio Grande (T-Rex)
    }

    [Header("Referencias de Componentes")]
    public EnemyMovement Movement { get; set; }
    public AStarPathfinding Pathfinding { get; set; }

    [Header("Jugador")]
    public Transform Player;

    [Header("Jerarquía de Dinosaurios")]
    public DinoTier Tier = DinoTier.Medium;
    public bool IsSmallDino => Tier == DinoTier.Small || IsPrey || this is SmallEnemyAI;
    public bool IsMediumDino => Tier == DinoTier.Medium && !IsSmallDino && !IsBigDino;
    public bool IsBigDino => Tier == DinoTier.Big;

    [Header("Configuración de Rol y Líder")]
    public bool isLeader = false;
    public Transform leaderTransform = null;
    public List<EnemyAI> packFollowers = new List<EnemyAI>();
    public EnemyAI LeaderAI { get; set; }

    public EnemyAI EffectiveLeader => (IsSmallDino && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy) ? LeaderAI : this;
    public Vector3 SpawnPosition => (Movement != null) ? Movement.SpawnPosition : transform.position;
    public Quaternion SpawnRotation => (Movement != null) ? Movement.SpawnRotation : transform.rotation;

    [Header("Configuración de Presa / Depredador (Compatibilidad)")]
    [HideInInspector] public bool dinoCiego = false;
    public bool IsPrey
    {
        get => Tier == DinoTier.Small;
        set
        {
            if (value) Tier = DinoTier.Small;
            else if (Tier == DinoTier.Small) Tier = DinoTier.Medium;
        }
    }
    public bool isPrey { get => IsPrey; set => IsPrey = value; }
    public float SafeRange = 22f;

    [Header("Dinosaurio emboscador")]
    [Tooltip("Espera quieto en su sitio, acecha y se lanza en una carrera corta.")]
    public bool dinoEmboscador = false;
    [Tooltip("Distancia a la que detecta al jugador y empieza a acechar.")]
    public float ambushStalkRange = 9f;
    [Tooltip("Distancia a la que, tras el aviso, se lanza al ataque.")]
    public float ambushTriggerRange = 5f;
    [Tooltip("Segundos de aviso (acecho) antes de atacar.")]
    public float ambushWarningTime = 1.2f;
    [Tooltip("Duración máxima de la carrera de ataque.")]
    public float ambushAttackDuration = 2f;
    [Tooltip("Velocidad de la carrera de ataque.")]
    public float ambushAttackSpeed = 11f;
    [Tooltip("Giro máximo durante el ataque (grados/s). Bajo = esquivable.")]
    public float ambushTurnRate = 90f;
    [Tooltip("Segundos que queda cansado tras atacar.")]
    public float ambushTiredTime = 4f;

    [HideInInspector] public Vector3 AmbushHomePos;
    [HideInInspector] public Quaternion AmbushHomeRot;
    public AmbushHiddenState AmbushHiddenState { get; protected set; }
    public AmbushStalkState AmbushStalkState { get; protected set; }
    public AmbushRecoverState AmbushRecoverState { get; protected set; }

    [Header("Objetivos Dinámicos")]
    public Transform CurrentThreat { get; set; }
    public EnemyAI ChasingPredator { get; set; }
    public Vector3 SpottedThreatPosition { get; set; }
    public Transform CurrentChaseTarget { get; set; }
    public EnemyAI TargetDino { get; set; }

    public static readonly List<EnemyAI> AllEnemies = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllSmallDinos = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllMediumDinos = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllBigDinos = new List<EnemyAI>();
    public static List<EnemyAI> AllPrey => AllSmallDinos;
    public static List<EnemyAI> AllPredators => AllBigDinos;
    protected static bool hasDistributedPrey = false;
    private static readonly RaycastHit[] visionRaycastHits = new RaycastHit[16];

    [Header("Configuración de Visión y Persecución")]
    public float visionRange = 7.5f;
    public float visionAngle = 100f;
    public float eyeHeight = 0.65f;
    public float DetectionRange = 7.5f;
    public float LoseRange = 18f;
    public float AttackRange = 1.8f;

    [Header("Configuración de Velocidades")]
    public float patrolSpeed = 2.4f;
    public float chaseSpeed = 7.8f;
    public float fleeSpeed = 10.5f;

    [Header("Tiempos de Persecución y Desiste")]
    public float preyMaxChaseTime = 3.5f;
    public float preyLostSightTime = 2.0f;
    public float playerLostSightTime = 5.0f;
    public float preyChaseCooldown = 3.0f;
    [HideInInspector] public float preyCooldownTimer = 0f;

    [Header("Prioridad de Chase por Alarma")]
    [Tooltip("Indica si este enemigo se encuentra en el estado de persecución prioritario inducido por la alarma.")]
    public bool isPriorityChase = false;
    public bool IsPriorityChaseActive => isPriorityChase && StateMachine != null && (StateMachine.CurrentState == ChaseState || StateMachine.CurrentStatePriority > 0);

    [Header("Configuración de Flocking")]
    public float FlockRadius = 10f;
    public float TooCloseRadius = 2.5f;
    public float LeaderFollowWeight = 3f;
    public float SeparationWeight = 1.5f;
    public float CohesionWeight = 1.5f;
    public float AlignmentWeight = 1f;

    public StateMachine StateMachine { get; protected set; }
    public PatrolState PatrolState { get; protected set; }
    public ChaseState ChaseState { get; protected set; }
    public FleeState FleeState { get; protected set; }
    public FollowLeaderState FollowLeaderState { get; protected set; }

    public static bool GlobalPlayerDead = false;
    public static EnemyAI KillerDino { get; private set; }
    public bool isPlayerDead => GlobalPlayerDead;

    private Renderer[] dinoRenderers;
    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public enum DinoVisualState
    {
        Normal,
        Chasing,
        Fleeing,
        Alert,
        Alarm,
        Stalking,
        Tired
    }

    protected virtual void OnEnable()
    {
        if (!AllEnemies.Contains(this)) AllEnemies.Add(this);
        UpdateStaticCategory();
    }

    protected virtual void OnDisable()
    {
        AllEnemies.Remove(this);
        AllSmallDinos.Remove(this);
        AllMediumDinos.Remove(this);
        AllBigDinos.Remove(this);
        if (LeaderAI != null) LeaderAI.packFollowers.Remove(this);
    }

    public void UpdateStaticCategory()
    {
        if (!AllEnemies.Contains(this)) AllEnemies.Add(this);
        AllSmallDinos.Remove(this);
        AllMediumDinos.Remove(this);
        AllBigDinos.Remove(this);

        if (IsSmallDino)
        {
            if (!AllSmallDinos.Contains(this)) AllSmallDinos.Add(this);
        }
        else if (IsBigDino)
        {
            if (!AllBigDinos.Contains(this)) AllBigDinos.Add(this);
        }
        else
        {
            if (!AllMediumDinos.Contains(this)) AllMediumDinos.Add(this);
        }
    }

    protected virtual void Awake()
    {
        Movement = GetComponent<EnemyMovement>();
        Pathfinding = GetComponent<AStarPathfinding>();
        if (Pathfinding == null) Pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();

        DeterminePreyOrPredator();
        UpdateStaticCategory();
        SetupDefaultStats();
        SetupStateMachine();
    }

    protected virtual void DeterminePreyOrPredator()
    {
        if (this is SmallEnemyAI)
        {
            Tier = DinoTier.Small;
            return;
        }
        if (this is BigEnemyAI)
        {
            Tier = DinoTier.Big;
            return;
        }
        if (this is BlindEnemyAI || this is AlarmEnemyAI || this is AmbushEnemyAI)
        {
            Tier = DinoTier.Medium;
            if (this is AmbushEnemyAI) dinoEmboscador = true;
            return;
        }

        string goName = gameObject.name.ToLower();
        if (goName.Contains("presa") || goName.Contains("small") || goName.Contains("pequeño") || goName.Contains("pequeno"))
        {
            Tier = DinoTier.Small;
            return;
        }

        if (goName.Contains("emboscad") || goName.Contains("ambush"))
        {
            Tier = DinoTier.Medium;
            dinoEmboscador = true;
            return;
        }

        if (goName.Contains("ciego") || goName.Contains("blind"))
        {
            Tier = DinoTier.Medium;
            dinoCiego = true;
            return;
        }

        if (goName.Contains("alarma") || goName.Contains("avisador") ||
            goName.Contains("screamer") || goName.Contains("spotter"))
        {
            Tier = DinoTier.Medium;
            return;
        }

        // Dino Grande (T-Rex)
        if ((goName.Contains("lider") || goName.Contains("dinogrande")) && !goName.Contains("presa"))
        {
            Tier = DinoTier.Big;
            return;
        }

        // Dino Mediano
        if (goName.Contains("seguidor") && !goName.Contains("presa"))
        {
            Tier = DinoTier.Medium;
            return;
        }

        if (goName.Contains("grande") || goName.Contains("t-rex") || goName.Contains("trex"))
        {
            Tier = DinoTier.Big;
            return;
        }

        if (goName.Contains("mediano") || goName.Contains("medium"))
        {
            Tier = DinoTier.Medium;
            return;
        }

        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        if (scale < 1.1f) Tier = DinoTier.Small;
        else if (scale >= 2.5f) Tier = DinoTier.Big;
        else Tier = DinoTier.Medium;

        if (!IsSmallDino)
        {
            isLeader = false;
            leaderTransform = null;
            LeaderAI = null;
            packFollowers.Clear();
        }
    }

    protected virtual void SetupDefaultStats()
    {
        if (!IsSmallDino)
        {
            isLeader = false;
            leaderTransform = null;
            LeaderAI = null;
            packFollowers.Clear();
        }
        else
        {
            if (leaderTransform != null && LeaderAI == null)
            {
                LeaderAI = leaderTransform.GetComponent<EnemyAI>();
            }
            else if (isLeader && (packFollowers == null || packFollowers.Count == 0))
            {
                isLeader = false;
                LeaderAI = null;
            }
            packFollowers.Clear();
        }

        if (IsSmallDino)
        {
            // Dino pequeño (manada): más rápido, amplio campo de visión, huye de mediano y grande
            visionAngle = 260f;
            visionRange = 11.5f;
            DetectionRange = 11.5f;
            eyeHeight = 0.35f;
            AttackRange = 1.3f;
            patrolSpeed = 4.8f;
            chaseSpeed = 9.8f;
            fleeSpeed = 13.0f;
            SafeRange = 14.0f;
        }
        else if (IsBigDino)
        {
            // Dino grande (T-Rex): más lento que el mediano, caza jugador y medianos, nunca huye
            visionAngle = 100f;
            visionRange = 12.0f;
            DetectionRange = 12.0f;
            eyeHeight = 1.6f;
            AttackRange = 2.8f;
            patrolSpeed = 1.8f;
            chaseSpeed = 5.8f;
            fleeSpeed = 0f;
            SafeRange = 0f;
        }
        else
        {
            // Dino mediano: velocidad normal (actual), caza jugador y pequeños, huye del grande
            visionAngle = 110f;
            visionRange = 9.0f;
            DetectionRange = 9.0f;
            eyeHeight = 0.85f;
            AttackRange = 2.0f;
            patrolSpeed = 2.4f;
            chaseSpeed = 7.5f;
            fleeSpeed = 8.8f;
            SafeRange = 16.0f;
        }
    }

    protected virtual void SetupStateMachine()
    {
        StateMachine = new StateMachine();
        if (dinoEmboscador)
        {
            Tier = DinoTier.Medium;
            UpdateStaticCategory();
            visionRange = ambushStalkRange;
            visionAngle = 120f;
            eyeHeight = 0.85f;
            AttackRange = 2.0f;
            chaseSpeed = ambushAttackSpeed;
            AmbushHomePos = transform.position;
            AmbushHomeRot = transform.rotation;

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
        else if (IsSmallDino)
        {
            PreyPatrolState preyPatrol = new PreyPatrolState(this);
            PreyChaseState preyChase = new PreyChaseState(this);
            FleeState flee = new FleeState(this);
            PreyFollowLeaderState follow = new PreyFollowLeaderState(this);

            PatrolState = preyPatrol;
            ChaseState = preyChase;
            FleeState = flee;
            FollowLeaderState = follow;

            StateMachine.AllowState(preyPatrol);
            StateMachine.AllowState(preyChase);
            StateMachine.AllowState(flee);
            StateMachine.AllowState(follow);
        }
        else if (IsBigDino)
        {
            // Dino grande: patrulla y caza (no huye de otros dinosaurios)
            PatrolState predPatrol = new PatrolState(this);
            ChaseState predChase = new ChaseState(this);

            PatrolState = predPatrol;
            ChaseState = predChase;
            FleeState = null;
            FollowLeaderState = null;

            StateMachine.AllowState(predPatrol);
            StateMachine.AllowState(predChase);
        }
        else
        {
            // Dino mediano: patrulla, caza (jugador y pequeños) y huye (del grande)
            PatrolState medPatrol = new PatrolState(this);
            ChaseState medChase = new ChaseState(this);
            FleeState flee = new FleeState(this);

            PatrolState = medPatrol;
            ChaseState = medChase;
            FleeState = flee;
            FollowLeaderState = null;

            StateMachine.AllowState(medPatrol);
            StateMachine.AllowState(medChase);
            StateMachine.AllowState(flee);
        }
    }

    protected virtual void Start()
    {
        EnsurePlayerReference();

        if (dinoEmboscador)
        {
            AmbushHomePos = transform.position;
            AmbushHomeRot = transform.rotation;
        }

        if (IsSmallDino)
        {
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
            else if (isLeader && (packFollowers == null || packFollowers.Count == 0))
            {
                isLeader = false;
                LeaderAI = null;
            }
        }
        else
        {
            isLeader = false;
            leaderTransform = null;
            LeaderAI = null;
            packFollowers.Clear();
        }

        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
        }

        SetVisualState(DinoVisualState.Normal);

        if (dinoEmboscador && AmbushHiddenState != null)
        {
            StateMachine.Initialize(AmbushHiddenState);
        }
        else if (IsSmallDino && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy && FollowLeaderState != null)
        {
            StateMachine.Initialize(FollowLeaderState);
        }
        else
        {
            StateMachine.Initialize(PatrolState);
        }
    }

    protected virtual void Update()
    {
        if (preyCooldownTimer > 0f) preyCooldownTimer -= Time.deltaTime;

        if (GlobalPlayerDead)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
            return;
        }

        if (dinoEmboscador)
        {
            if (StateMachine != null &&
                StateMachine.CurrentState != AmbushRecoverState &&
                StateMachine.CurrentState != AmbushHiddenState &&
                StateMachine.CurrentState != AmbushStalkState &&
                StateMachine.CurrentState != ChaseState &&
                StateMachine.CurrentState != FleeState)
            {
                StateMachine.ChangeState(AmbushRecoverState);
            }
        }

        StateMachine.Update();
        SyncVisualStateWithCurrentState();
    }

    public void EnsurePlayerReference()
    {
        if (Player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                FirstPersonMovement fpm = p.GetComponentInParent<FirstPersonMovement>() ?? p.GetComponentInChildren<FirstPersonMovement>();
                Player = fpm != null ? fpm.transform : p.transform;
            }
        }
    }

    public DinoVisualState CurrentVisualState { get; private set; } = DinoVisualState.Normal;

    public Renderer[] GetDinoRenderers()
    {
        if (dinoRenderers == null || dinoRenderers.Length == 0)
        {
            List<Renderer> valid = new List<Renderer>();
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r == null) continue;
                string n = r.gameObject.name;
                if (n.IndexOf("trigger", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("sound", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("sphere", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                valid.Add(r);
            }
            dinoRenderers = valid.ToArray();
        }
        return dinoRenderers;
    }

    public void SetVisualState(DinoVisualState state)
    {
        CurrentVisualState = state;
        Renderer[] renderers = GetDinoRenderers();
        if (renderers == null || renderers.Length == 0) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        if (state == DinoVisualState.Normal)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null) continue;
                rend.SetPropertyBlock(null);

                Material sharedMat = rend.sharedMaterial;
                if (sharedMat != null && sharedMat.HasProperty(EmissionColorId))
                {
                    sharedMat.DisableKeyword("_EMISSION");
                }
            }
            return;
        }

        Color targetColor;
        Color targetEmission;

        switch (state)
        {
            case DinoVisualState.Chasing:
                targetColor = new Color(0.95f, 0.10f, 0.10f, 1f);
                targetEmission = new Color(1f, 0.08f, 0.08f, 1f) * 1.5f;
                break;

            case DinoVisualState.Fleeing:
                targetColor = new Color(0.12f, 0.58f, 1f, 1f);
                targetEmission = new Color(0.08f, 0.55f, 1f, 1f) * 1.5f;
                break;

            case DinoVisualState.Alert:
                targetColor = new Color(1f, 0.85f, 0.08f, 1f);
                targetEmission = new Color(1f, 0.80f, 0.05f, 1f) * 1.3f;
                break;

            case DinoVisualState.Alarm:
                targetColor = new Color(1f, 0.12f, 0.85f, 1f);
                targetEmission = new Color(1f, 0.08f, 0.85f, 1f) * 1.5f;
                break;

            case DinoVisualState.Stalking:
                targetColor = new Color(1f, 0.50f, 0.05f, 1f);
                targetEmission = new Color(1f, 0.45f, 0.05f, 1f) * 1.3f;
                break;

            case DinoVisualState.Tired:
                targetColor = new Color(0.50f, 0.52f, 0.58f, 1f);
                targetEmission = Color.black;
                break;

            default:
                targetColor = Color.white;
                targetEmission = Color.black;
                break;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null) continue;

            propBlock.Clear();
            propBlock.SetColor(BaseColorId, targetColor);
            propBlock.SetColor(ColorId, targetColor);
            if (targetEmission != Color.black)
            {
                propBlock.SetColor(EmissionColorId, targetEmission);
            }
            rend.SetPropertyBlock(propBlock);

            Material sharedMat = rend.sharedMaterial;
            if (sharedMat != null && sharedMat.HasProperty(EmissionColorId))
            {
                if (targetEmission != Color.black)
                {
                    sharedMat.EnableKeyword("_EMISSION");
                }
                else
                {
                    sharedMat.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    public virtual DinoVisualState DetermineVisualState(IState state)
    {
        if (state == null) return DinoVisualState.Normal;
        if (state is ChaseState) return DinoVisualState.Chasing;
        if (state is FleeState) return DinoVisualState.Fleeing;
        if (state is AlarmState) return DinoVisualState.Alarm;
        if (state is BlindInvestigateState) return DinoVisualState.Alert;
        if (state is AmbushStalkState) return DinoVisualState.Stalking;
        if (state is AmbushRecoverState) return DinoVisualState.Tired;
        return DinoVisualState.Normal;
    }

    public void SyncVisualStateWithCurrentState()
    {
        if (StateMachine == null || StateMachine.CurrentState == null) return;
        DinoVisualState expected = DetermineVisualState(StateMachine.CurrentState);
        if (expected != CurrentVisualState)
        {
            SetVisualState(expected);
        }
    }

    public void SetChasingVisuals(bool isChasing) => SetVisualState(isChasing ? DinoVisualState.Chasing : DinoVisualState.Normal);
    public void SetFleeingVisuals(bool isFleeing) => SetVisualState(isFleeing ? DinoVisualState.Fleeing : DinoVisualState.Normal);
    public void SetAlertVisuals(bool isAlert) => SetVisualState(isAlert ? DinoVisualState.Alert : DinoVisualState.Normal);
    public void SetAlarmVisuals(bool isAlarm) => SetVisualState(isAlarm ? DinoVisualState.Alarm : DinoVisualState.Normal);
    public void SetStalkingVisuals(bool isStalking) => SetVisualState(isStalking ? DinoVisualState.Stalking : DinoVisualState.Normal);

    public bool MutuallySees(EnemyAI other)
    {
        if (other == null || other == this || !other.gameObject.activeInHierarchy) return false;
        if (!IsSmallDino || !other.IsSmallDino) return false;
        return CanSeeSpecificDino(other) && other.CanSeeSpecificDino(this);
    }

    public void SetLeader(EnemyAI newLeader)
    {
        if (!IsSmallDino) return;
        if (newLeader == null || newLeader == this)
        {
            PromoteToLeader();
            return;
        }

        if (LeaderAI != null && LeaderAI != newLeader) LeaderAI.packFollowers.Remove(this);

        isLeader = false;
        LeaderAI = newLeader;
        leaderTransform = newLeader.transform;

        if (!newLeader.packFollowers.Contains(this)) newLeader.packFollowers.Add(this);

        if (Movement != null)
        {
            Movement.patrolNodes.Clear();
            Movement.ClearPath();
        }

        TriggerPatrol();
    }

    public void PromoteToLeader()
    {
        if (!IsSmallDino) return;
        if (LeaderAI != null) LeaderAI.packFollowers.Remove(this);

        LeaderAI = null;
        leaderTransform = null;
        isLeader = (packFollowers != null && packFollowers.Count > 0);

        if (Movement != null && Movement.HasReachedDestination) Movement.GoToNextPatrolNode();
        TriggerPatrol();
    }

    public static void ResolveEncounter(EnemyAI leaderA, EnemyAI leaderB)
    {
        if (leaderA == null || leaderB == null || leaderA == leaderB) return;
        if (!leaderA.gameObject.activeInHierarchy || !leaderB.gameObject.activeInHierarchy) return;
        if (!leaderA.IsSmallDino || !leaderB.IsSmallDino) return;

        bool aWins = (Random.value < 0.5f);
        EnemyAI winner = aWins ? leaderA : leaderB;
        EnemyAI loser = aWins ? leaderB : leaderA;

        winner.isLeader = true;
        winner.LeaderAI = null;
        winner.leaderTransform = null;
        loser.SetLeader(winner);

        if (loser.packFollowers != null && loser.packFollowers.Count > 0)
        {
            List<EnemyAI> followersToTransfer = new List<EnemyAI>(loser.packFollowers);
            loser.packFollowers.Clear();

            for (int i = 0; i < followersToTransfer.Count; i++)
            {
                EnemyAI follower = followersToTransfer[i];
                if (follower != null && follower != winner && follower.gameObject.activeInHierarchy)
                {
                    follower.SetLeader(winner);
                }
            }
        }
    }

    public virtual void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (target == null) return;
        if (IsPriorityChaseActive && targetDino != null) return;
        if (this is BlindEnemyAI || this is AmbushEnemyAI || dinoEmboscador)
        {
            if (targetDino != null) return;
            if (target.GetComponentInParent<EnemyAI>() != null) return;
        }

        // Jerarquía de caza:
        if (targetDino != null)
        {
            // Pequeño no caza otros dinosaurios
            if (IsSmallDino) return;
            // Mediano solo caza pequeños
            if (IsMediumDino && !targetDino.IsSmallDino) return;
            // Grande solo caza medianos
            if (IsBigDino && !targetDino.IsMediumDino) return;
        }

        CurrentChaseTarget = target;
        TargetDino = targetDino;

        if (targetDino != null && targetDino.gameObject.activeInHierarchy && this is not BlindEnemyAI && this is not AmbushEnemyAI && !dinoEmboscador)
        {
            targetDino.NotifyPackFlee(this);
        }

        if (StateMachine != null && ChaseState != null)
        {
            if (StateMachine.CurrentState != ChaseState &&
                StateMachine.CurrentState != FleeState) StateMachine.ChangeState(ChaseState);
            else ChaseState.Enter();
        }
    }

    public virtual void TriggerAlarmChase(Transform target, Vector3 targetPos)
    {
        if (target == null) return;

        EnsurePlayerReference();
        isPriorityChase = true;
        CurrentChaseTarget = target;
        TargetDino = null;

        if (this is BlindEnemyAI blind)
        {
            blind.LastHeardSoundPos = targetPos;
            blind.LastSoundSource = target;
        }

        if (IsSmallDino)
        {
            if (packFollowers != null)
            {
                for (int i = 0; i < packFollowers.Count; i++)
                {
                    EnemyAI follower = packFollowers[i];
                    if (follower != null && follower.gameObject.activeInHierarchy && !follower.isPriorityChase)
                    {
                        follower.TriggerAlarmChase(target, targetPos);
                    }
                }
            }
            if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy && !LeaderAI.isPriorityChase)
            {
                LeaderAI.TriggerAlarmChase(target, targetPos);
            }
        }

        if (StateMachine != null && ChaseState != null)
        {
            if (StateMachine.CurrentState != ChaseState &&
                StateMachine.CurrentState != FleeState)
            {
                StateMachine.ChangeState(ChaseState, force: true);
            }
            else
            {
                ChaseState.Enter();
            }
        }

        if (Movement != null)
        {
            Movement.speed = chaseSpeed;
            Movement.MoveTo(targetPos);
        }
    }

    public virtual void TriggerFlee(EnemyAI threat)
    {
        if (this is BlindEnemyAI || dinoCiego) return;
        if (IsPriorityChaseActive) return;
        if (IsBigDino) return; // El dinosaurio grande nunca huye de otros dinosaurios
        if (threat == null || threat == this) return;
        if (threat is BlindEnemyAI || threat is AmbushEnemyAI || threat.dinoEmboscador) return;
        if (threat == LeaderAI || threat.EffectiveLeader == EffectiveLeader) return;

        // Jerarquía de huida:
        // Mediano solo huye del Grande
        if (IsMediumDino && !threat.IsBigDino) return;
        // Pequeño huye de Mediano y de Grande
        if (IsSmallDino && !threat.IsMediumDino && !threat.IsBigDino) return;

        CurrentThreat = threat.transform;
        ChasingPredator = threat;
        SpottedThreatPosition = threat.transform.position;
        if (StateMachine != null && FleeState != null && StateMachine.CurrentState != FleeState)
        {
            StateMachine.ChangeState(FleeState);
        }
    }

    public virtual float GetVisionOrFacingCone()
    {
        if (visionAngle > 0f) return visionAngle;
        return IsSmallDino ? 260f : 110f;
    }

    public virtual void TriggerScaredByLight(Transform threatPlayer)
    {
        if (this is BlindEnemyAI || dinoCiego) return;
        isPriorityChase = false;
        TargetDino = null;
        CurrentChaseTarget = null;
        if (StateMachine != null && StateMachine.CurrentState == FleeState) return;

        if (threatPlayer == null) EnsurePlayerReference();
        Transform threat = threatPlayer != null ? threatPlayer : Player;
        if (threat == null) return;

        CurrentThreat = threat;
        ChasingPredator = null;
        SpottedThreatPosition = threat.position;
        SafeRange = 25f;

        if (StateMachine != null && FleeState != null)
        {
            StateMachine.ChangeState(FleeState);
        }

        if (IsSmallDino)
        {
            if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy && LeaderAI != this)
            {
                if (LeaderAI.StateMachine != null && LeaderAI.StateMachine.CurrentState != LeaderAI.FleeState)
                {
                    LeaderAI.TriggerScaredByLight(threat);
                }
            }

            if (packFollowers != null)
            {
                for (int i = 0; i < packFollowers.Count; i++)
                {
                    EnemyAI follower = packFollowers[i];
                    if (follower != null && follower.gameObject.activeInHierarchy && follower != this)
                    {
                        if (follower.StateMachine != null && follower.StateMachine.CurrentState != follower.FleeState)
                        {
                            follower.TriggerScaredByLight(threat);
                        }
                    }
                }
            }
        }
    }

    public virtual void TriggerPatrol()
    {
        if (IsPriorityChaseActive) return;
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine == null) return;

        if (dinoEmboscador && AmbushRecoverState != null)
        {
            if (StateMachine.CurrentState != AmbushRecoverState) StateMachine.ChangeState(AmbushRecoverState);
            return;
        }

        if (IsSmallDino && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy && FollowLeaderState != null)
        {
            if (StateMachine.CurrentState != FollowLeaderState) StateMachine.ChangeState(FollowLeaderState);
        }
        else if (PatrolState != null)
        {
            if (StateMachine.CurrentState != PatrolState) StateMachine.ChangeState(PatrolState);
        }
    }

    public void NotifyPackAttack(Transform target)
    {
        TriggerAttack(target);

        if (!IsSmallDino) return;

        if (!isLeader && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            if (LeaderAI.StateMachine != null && LeaderAI.StateMachine.CurrentState != LeaderAI.ChaseState)
            {
                LeaderAI.TriggerAttack(target);
            }
        }

        if (packFollowers != null)
        {
            for (int i = 0; i < packFollowers.Count; i++)
            {
                EnemyAI follower = packFollowers[i];
                if (follower != null && follower.gameObject.activeInHierarchy &&
                    follower.StateMachine != null && follower.StateMachine.CurrentState != follower.ChaseState)
                {
                    follower.TriggerAttack(target);
                }
            }
        }
    }

    public void NotifyPackChase(Transform target) => NotifyPackAttack(target);

    public virtual void NotifyPackFlee(EnemyAI threat)
    {
        if (this is BlindEnemyAI || dinoCiego) return;
        if (IsBigDino) return;
        if (threat == null || threat == this) return;
        if (threat is BlindEnemyAI || threat is AmbushEnemyAI || threat.dinoEmboscador) return;
        if (threat == LeaderAI || threat.EffectiveLeader == EffectiveLeader) return;
        if (IsMediumDino && !threat.IsBigDino) return;
        if (IsSmallDino && !threat.IsMediumDino && !threat.IsBigDino) return;

        TriggerFlee(threat);

        if (!IsSmallDino) return;

        if (!isLeader && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            if (LeaderAI.StateMachine != null && LeaderAI.StateMachine.CurrentState != LeaderAI.FleeState)
            {
                LeaderAI.TriggerFlee(threat);
            }
        }

        if (packFollowers != null)
        {
            for (int i = 0; i < packFollowers.Count; i++)
            {
                EnemyAI follower = packFollowers[i];
                if (follower != null && follower.gameObject.activeInHierarchy &&
                    follower.StateMachine != null && follower.StateMachine.CurrentState != follower.FleeState)
                {
                    follower.TriggerFlee(threat);
                }
            }
        }
    }

    public void NotifyPackFlee(Transform threat, EnemyAI predator = null)
    {
        if (predator != null)
        {
            if (predator is BlindEnemyAI || predator is AmbushEnemyAI || predator.dinoEmboscador) return;
            NotifyPackFlee(predator);
        }
        else if (threat != null)
        {
            EnemyAI threatAI = threat.GetComponentInParent<EnemyAI>();
            if (threatAI != null)
            {
                if (threatAI is BlindEnemyAI || threatAI is AmbushEnemyAI || threatAI.dinoEmboscador) return;
                NotifyPackFlee(threatAI);
            }
        }
    }

    public void NotifyPackPatrol() => TriggerPatrol();

    public virtual bool CanSeeSpecificDino(EnemyAI targetDino)
    {
        if (targetDino == null || !targetDino.gameObject.activeInHierarchy) return false;

        bool isChasing = (StateMachine != null && StateMachine.CurrentState == ChaseState);
        float baseRange = isChasing ? Mathf.Max(visionRange, (LoseRange > 0 ? LoseRange : 18.0f)) : visionRange;
        Vector3 toTarget = targetDino.transform.position - transform.position;
        float dist = toTarget.magnitude;
        if (dist > baseRange) return false;

        Vector3 toTargetHoriz = new Vector3(toTarget.x, 0f, toTarget.z);
        if (toTargetHoriz.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, toTargetHoriz.normalized);
            float maxAngle = (isChasing && dist < 3.5f) ? 360f : visionAngle;
            if (angle > maxAngle * 0.5f) return false;
        }

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.3f;
        Vector3 targetPos = targetDino.transform.position + Vector3.up * targetDino.eyeHeight;
        Vector3 rayDir = targetPos - eyePos;
        float rayDist = rayDir.magnitude;
        if (rayDist < 0.001f) return true;

        RaycastHit[] hits = Physics.RaycastAll(eyePos, rayDir.normalized, rayDist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            if (hit.transform == targetDino.transform || hit.transform.IsChildOf(targetDino.transform) ||
                hit.collider.GetComponentInParent<EnemyAI>() == targetDino)
            {
                return true;
            }

            bool isOtherEnemy = hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null;
            if (isOtherEnemy) continue;

            if (hit.normal.y >= 0.7f) continue;

            return false;
        }

        return true;
    }

    public virtual bool CanSeeThreat(out EnemyAI spottedThreat)
    {
        spottedThreat = null;
        if (IsBigDino || this is BlindEnemyAI || dinoCiego) return false;

        // 1. Dinosaurio Mediano: huye del dinosaurio Grande (T-Rex)
        if (IsMediumDino)
        {
            for (int i = 0; i < AllBigDinos.Count; i++)
            {
                EnemyAI big = AllBigDinos[i];
                if (big == null || !big.gameObject.activeInHierarchy || big == this) continue;
                if (big is BlindEnemyAI || big is AmbushEnemyAI || big.dinoEmboscador) continue;

                if (CanSeeSpecificDino(big))
                {
                    spottedThreat = big;
                    return true;
                }
            }
            return false;
        }

        // 2. Dinosaurio Pequeño: huye de Medianos y de Grandes
        if (IsSmallDino)
        {
            // Primero evaluar si hay dinosaurios Grandes en visión
            for (int i = 0; i < AllBigDinos.Count; i++)
            {
                EnemyAI big = AllBigDinos[i];
                if (big == null || !big.gameObject.activeInHierarchy || big == this) continue;
                if (big is BlindEnemyAI || big is AmbushEnemyAI || big.dinoEmboscador) continue;

                if (CanSeeSpecificDino(big))
                {
                    spottedThreat = big;
                    return true;
                }
            }

            // Luego evaluar si hay dinosaurios Medianos en visión
            for (int i = 0; i < AllMediumDinos.Count; i++)
            {
                EnemyAI med = AllMediumDinos[i];
                if (med == null || !med.gameObject.activeInHierarchy || med == this) continue;
                if (med is BlindEnemyAI || med is AmbushEnemyAI || med.dinoEmboscador) continue;

                if (CanSeeSpecificDino(med))
                {
                    spottedThreat = med;
                    return true;
                }
            }
        }

        return false;
    }

    public virtual bool CanSeeHuntablePrey(out EnemyAI spottedPrey)
    {
        spottedPrey = null;
        if (IsSmallDino || this is BlindEnemyAI || this is AmbushEnemyAI || dinoEmboscador) return false;

        // 1. Dinosaurio Grande: caza dinosaurios Medianos
        if (IsBigDino)
        {
            for (int i = 0; i < AllMediumDinos.Count; i++)
            {
                EnemyAI med = AllMediumDinos[i];
                if (med == null || !med.gameObject.activeInHierarchy || med == this) continue;
                if (med is BlindEnemyAI || med is AmbushEnemyAI || med.dinoEmboscador) continue;

                if (CanSeeSpecificDino(med))
                {
                    spottedPrey = med;
                    return true;
                }
            }
            return false;
        }

        // 2. Dinosaurio Mediano: caza dinosaurios Pequeños
        if (IsMediumDino)
        {
            for (int i = 0; i < AllSmallDinos.Count; i++)
            {
                EnemyAI small = AllSmallDinos[i];
                if (small == null || !small.gameObject.activeInHierarchy || small == this) continue;
                if (small is BlindEnemyAI || small is AmbushEnemyAI || small.dinoEmboscador) continue;

                if (CanSeeSpecificDino(small))
                {
                    spottedPrey = small;
                    return true;
                }
            }
        }

        return false;
    }

    public virtual bool CanSeeBigDino(out EnemyAI spottedPredator) => CanSeeThreat(out spottedPredator);
    public virtual bool CanSeePrey(out EnemyAI spottedPrey) => CanSeeHuntablePrey(out spottedPrey);

    public virtual void Die()
    {
        if (LeaderAI != null) LeaderAI.packFollowers.Remove(this);

        if (isLeader && packFollowers != null && packFollowers.Count > 0)
        {
            List<EnemyAI> activeFollowers = packFollowers.FindAll(f => f != null && f.gameObject.activeInHierarchy && f != this);
            if (activeFollowers.Count == 1)
            {
                EnemyAI survivor = activeFollowers[0];
                survivor.isLeader = false;
                survivor.LeaderAI = null;
                survivor.leaderTransform = null;
                survivor.packFollowers.Clear();
                if (survivor.Movement != null && survivor.Movement.HasReachedDestination)
                {
                    survivor.Movement.GoToNextPatrolNode();
                }
            }
            else if (activeFollowers.Count > 1)
            {
                EnemyAI newLeader = activeFollowers[Random.Range(0, activeFollowers.Count)];
                activeFollowers.Remove(newLeader);
                newLeader.isLeader = true;
                newLeader.LeaderAI = null;
                newLeader.leaderTransform = null;

                for (int i = 0; i < activeFollowers.Count; i++)
                {
                    activeFollowers[i].SetLeader(newLeader);
                }
            }
            packFollowers.Clear();
        }

        AllEnemies.Remove(this);
        AllSmallDinos.Remove(this);
        AllMediumDinos.Remove(this);
        AllBigDinos.Remove(this);
        gameObject.SetActive(false);
    }

    public void KillPlayer()
    {
        if (GlobalPlayerDead) return;
        GlobalPlayerDead = true;
        KillerDino = this;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 1. Quitar el sonido y silenciar todo el audio del juego
        AudioListener.pause = true;
        AudioListener.volume = 0f;

        AudioSource[] allAudio = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < allAudio.Length; i++)
        {
            if (allAudio[i] != null && allAudio[i].isPlaying)
            {
                allAudio[i].Stop();
            }
        }

        // 2. Bloquear el movimiento de la cámara
        FirstPersonLook.IsCameraLocked = true;

        // 3. Forzar soltar cualquier objeto que se estuviese sosteniendo
        PickUpScript pickup = Object.FindAnyObjectByType<PickUpScript>();
        if (pickup != null && pickup.IsHoldingObject)
        {
            pickup.ForceRelease();
        }
    }

    public static void RestartGame()
    {
        ResetGlobalState();
        string activeScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(activeScene);
    }

    public static void ResetGlobalState()
    {
        GlobalPlayerDead = false;
        KillerDino = null;
        hasDistributedPrey = false;
        AllEnemies.Clear();
        AllSmallDinos.Clear();
        AllMediumDinos.Clear();
        AllBigDinos.Clear();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Restaurar sonido y desbloquear cámara
        AudioListener.pause = false;
        AudioListener.volume = 1f;
        FirstPersonLook.IsCameraLocked = false;
    }

    protected virtual void OnDestroy()
    {
        AllEnemies.Remove(this);
        AllSmallDinos.Remove(this);
        AllMediumDinos.Remove(this);
        AllBigDinos.Remove(this);
        if (KillerDino == this) KillerDino = null;
    }

    public bool PackCanSeePlayer(out Vector3 targetPos)
    {
        targetPos = Vector3.zero;

        if (CanSeePlayer())
        {
            if (Player != null) targetPos = Player.position;
            return true;
        }

        if (isLeader && packFollowers != null)
        {
            for (int i = 0; i < packFollowers.Count; i++)
            {
                EnemyAI follower = packFollowers[i];
                if (follower != null && follower.gameObject.activeInHierarchy && follower.CanSeePlayer())
                {
                    if (follower.Player != null) targetPos = follower.Player.position;
                    return true;
                }
            }
        }
        else if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            return LeaderAI.PackCanSeePlayer(out targetPos);
        }

        return false;
    }

    public virtual bool CanSeePlayer()
    {
        if (Player == null)
        {
            EnsurePlayerReference();
            if (Player == null) return false;
        }

        bool isChasing = (StateMachine != null && StateMachine.CurrentState == ChaseState);
        float effectiveRange = isChasing ? Mathf.Max(visionRange, (LoseRange > 0 ? LoseRange : 18.0f)) : visionRange;

        FirstPersonMovement playerMovement = Player.GetComponent<FirstPersonMovement>() ??
                                             Player.GetComponentInParent<FirstPersonMovement>() ??
                                             Player.GetComponentInChildren<FirstPersonMovement>();

        if (playerMovement != null)
        {
            if (playerMovement.IsCrouched && !isChasing) effectiveRange *= playerMovement.stealthDetectionMultiplier;
            else if (playerMovement.IsRunning) effectiveRange *= 1.25f;
        }

        Vector3 toPlayer = Player.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance > effectiveRange) return false;

        Vector3 toPlayerHorizontal = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (toPlayerHorizontal.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, toPlayerHorizontal.normalized);
            float maxAllowedAngle = (isChasing && distance < 3.5f) ? 360f : visionAngle;
            if (angle > maxAllowedAngle * 0.5f) return false;
        }

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.3f;
        Vector3 targetPos = Player.position + Vector3.up * 0.9f;
        Vector3 rayDir = targetPos - eyePos;
        float rayDist = rayDir.magnitude;

        int hitCount = Physics.RaycastNonAlloc(eyePos, rayDir.normalized, visionRaycastHits, rayDist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount - 1; i++)
        {
            for (int j = i + 1; j < hitCount; j++)
            {
                if (visionRaycastHits[j].distance < visionRaycastHits[i].distance)
                {
                    RaycastHit temp = visionRaycastHits[i];
                    visionRaycastHits[i] = visionRaycastHits[j];
                    visionRaycastHits[j] = temp;
                }
            }
        }

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = visionRaycastHits[i];
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            bool isOtherEnemy = hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null;
            if (isOtherEnemy) continue;

            if (hit.transform == Player || hit.transform.IsChildOf(Player)) return true;
            return false;
        }

        return false;
    }

    private void OnGUI()
    {
        EnemyDeathUI.DrawDeathScreen(this);
    }

    private void OnDrawGizmos()
    {
        bool isChasing = (StateMachine != null && StateMachine.CurrentState == ChaseState);
        bool isFleeing = (StateMachine != null && StateMachine.CurrentState == FleeState);

        if (isChasing) Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        else if (isFleeing) Gizmos.color = new Color(0.15f, 0.55f, 1f, 0.9f);
        else Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.7f);

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        float range = visionRange > 0 ? visionRange : (IsSmallDino ? 11.5f : 8.5f);
        float halfAngle = visionAngle * 0.5f;

        int segments = 32;
        float startAngle = -halfAngle;
        float step = visionAngle / segments;

        Vector3 startDir = Quaternion.Euler(0, startAngle, 0) * transform.forward;
        Gizmos.DrawRay(eyePos, startDir * range);

        Vector3 prevPoint = eyePos + startDir * range;
        for (int i = 1; i <= segments; i++)
        {
            float curAngle = startAngle + i * step;
            Vector3 nextPoint = eyePos + Quaternion.Euler(0, curAngle, 0) * transform.forward * range;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        Vector3 endDir = Quaternion.Euler(0, halfAngle, 0) * transform.forward;
        Gizmos.DrawRay(eyePos, endDir * range);

        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.35f);
        Gizmos.DrawRay(eyePos, transform.forward * range);

        if (IsSmallDino && isLeader && packFollowers != null && packFollowers.Count > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.35f);

            Gizmos.color = Color.cyan;
            for (int i = 0; i < packFollowers.Count; i++)
            {
                if (packFollowers[i] != null && packFollowers[i].gameObject.activeInHierarchy)
                {
                    Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, packFollowers[i].transform.position + Vector3.up * 0.5f);
                }
            }
        }
    }
}
