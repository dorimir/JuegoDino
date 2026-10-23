using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyAI : MonoBehaviour
{
    [Header("Referencias de Componentes")]
    public EnemyMovement Movement { get; set; }
    public AStarPathfinding Pathfinding { get; set; }

    [Header("Jugador")]
    public Transform Player;

    [Header("Configuración de Rol y Líder")]
    public bool isLeader = false;
    public Transform leaderTransform = null;
    public List<EnemyAI> packFollowers = new List<EnemyAI>();
    public EnemyAI LeaderAI { get; protected set; }

    public EnemyAI EffectiveLeader => (IsSmallDino && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy) ? LeaderAI : this;
    public Vector3 SpawnPosition => (Movement != null) ? Movement.SpawnPosition : transform.position;
    public Quaternion SpawnRotation => (Movement != null) ? Movement.SpawnRotation : transform.rotation;

    [Header("Configuración de Presa / Depredador")]
    [HideInInspector] public bool dinoCiego = false;
    public bool IsPrey = false;
    public bool isPrey { get => IsPrey; set => IsPrey = value; }
    public bool IsBigDino => !IsPrey;
    public bool IsSmallDino => IsPrey;
    public float SafeRange = 22f;

    [Header("Objetivos Dinámicos")]
    public Transform CurrentThreat { get; set; }
    public EnemyAI ChasingPredator { get; set; }
    public Vector3 SpottedThreatPosition { get; set; }
    public Transform CurrentChaseTarget { get; set; }
    public EnemyAI TargetDino { get; set; }

    public static readonly List<EnemyAI> AllEnemies = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllPrey = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllPredators = new List<EnemyAI>();
    protected static bool hasDistributedPrey = false;

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

    public enum DinoVisualState { Normal, Chasing, Fleeing, Alert }

    protected virtual void OnEnable()
    {
        if (!AllEnemies.Contains(this)) AllEnemies.Add(this);
        UpdateStaticCategory();
    }

    protected virtual void OnDisable()
    {
        AllEnemies.Remove(this);
        AllPrey.Remove(this);
        AllPredators.Remove(this);
        if (LeaderAI != null) LeaderAI.packFollowers.Remove(this);
    }

    public void UpdateStaticCategory()
    {
        if (!AllEnemies.Contains(this)) AllEnemies.Add(this);
        AllPrey.Remove(this);
        AllPredators.Remove(this);

        if (IsPrey)
        {
            if (!AllPrey.Contains(this)) AllPrey.Add(this);
        }
        else
        {
            if (!AllPredators.Contains(this)) AllPredators.Add(this);
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
        if (this is SmallEnemyAI) { IsPrey = true; return; }
        if (this is BlindEnemyAI) { IsPrey = false; return; }

        string goName = gameObject.name.ToLower();
        if (goName.Contains("presa")) { IsPrey = true; return; }
        if (goName.Contains("ciego") || goName.Contains("blind")) { IsPrey = false; return; }
        if (goName.Contains("lider") && !goName.Contains("presa")) { IsPrey = false; return; }
        if ((goName.Contains("seguidor") && !goName.Contains("presa")) || goName.Contains("sumiso") || goName.Contains("depredador") || goName.Contains("predator")) { IsPrey = false; return; }

        // Si ya fue configurado en el Inspector (como los prefabs o instancias de escena con IsPrey = true), respetarlo
        if (IsPrey) return;

        // Fallback: los depredadores tienen escala ~2.08 o 3.0, las presas son pequeñas ~0.9 - 1.0
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        IsPrey = (scale < 1.5f);

        if (!IsPrey)
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
            else if (isLeader)
            {
                LeaderAI = null;
            }
            packFollowers.Clear();
        }

        if (IsSmallDino)
        {
            visionAngle = 280f;
            visionRange = 11.5f;
            DetectionRange = 11.5f;
            eyeHeight = 0.35f;
            AttackRange = 1.3f;
            patrolSpeed = 5.2f;
            chaseSpeed = 9.8f;
            fleeSpeed = 13.0f;
            SafeRange = 12.0f;
        }
        else
        {
            visionAngle = 100f;
            visionRange = 8.5f;
            eyeHeight = 0.85f;
            AttackRange = 2.0f;
            patrolSpeed = 2.4f;
            chaseSpeed = 7.5f;
            fleeSpeed = 8.0f;
            SafeRange = 16.0f;
        }
    }

    protected virtual void SetupStateMachine()
    {
        StateMachine = new StateMachine();
        if (IsSmallDino)
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
        else
        {
            PatrolState predPatrol = new PatrolState(this);
            ChaseState predChase = new ChaseState(this);
            FleeState flee = new FleeState(this);

            PatrolState = predPatrol;
            ChaseState = predChase;
            FleeState = flee;
            FollowLeaderState = null;

            StateMachine.AllowState(predPatrol);
            StateMachine.AllowState(predChase);
            StateMachine.AllowState(flee);
        }
    }

    protected virtual void Start()
    {
        EnsurePlayerReference();

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
            else if (isLeader)
            {
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

        if (IsSmallDino && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy && FollowLeaderState != null)
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
                GlobalPlayerDead = false;
                hasDistributedPrey = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        StateMachine.Update();
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

    public void SetVisualState(DinoVisualState state)
    {
        if (dinoRenderers == null || dinoRenderers.Length == 0) dinoRenderers = GetComponentsInChildren<Renderer>(true);
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        Color targetColor = Color.white;
        Color targetEmission = Color.black;

        if (state == DinoVisualState.Chasing)
        {
            targetColor = new Color(1f, 0.12f, 0.12f, 1f);
            targetEmission = new Color(0.9f, 0.05f, 0.05f, 1f);
        }
        else if (state == DinoVisualState.Fleeing)
        {
            targetColor = new Color(0.15f, 0.55f, 1f, 1f);
            targetEmission = new Color(0.05f, 0.45f, 1f, 1f);
        }
        else if (state == DinoVisualState.Alert)
        {
            targetColor = new Color(0.6f, 0.1f, 0.9f, 1f);
            targetEmission = new Color(0.5f, 0.05f, 0.8f, 1f);
        }

        foreach (var rend in dinoRenderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, targetColor);
            propBlock.SetColor(ColorId, targetColor);
            propBlock.SetColor(EmissionColorId, targetEmission);
            rend.SetPropertyBlock(propBlock);

            if (rend.material != null)
            {
                if (rend.material.HasProperty(BaseColorId)) rend.material.SetColor(BaseColorId, targetColor);
                if (rend.material.HasProperty(ColorId)) rend.material.SetColor(ColorId, targetColor);
                if (rend.material.HasProperty(EmissionColorId))
                {
                    rend.material.SetColor(EmissionColorId, targetEmission);
                    if (state != DinoVisualState.Normal) rend.material.EnableKeyword("_EMISSION");
                    else rend.material.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    public void SetChasingVisuals(bool isChasing) => SetVisualState(isChasing ? DinoVisualState.Chasing : DinoVisualState.Normal);

    public void SetFleeingVisuals(bool isFleeing) => SetVisualState(isFleeing ? DinoVisualState.Fleeing : DinoVisualState.Normal);

    public void SetAlertVisuals(bool isAlert) => SetVisualState(isAlert ? DinoVisualState.Alert : DinoVisualState.Normal);

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
        if (leaderA.IsBigDino || leaderB.IsBigDino) return;

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
        if (this is BlindEnemyAI)
        {
            if (targetDino != null && targetDino.IsSmallDino) return;
            if (target.GetComponentInParent<EnemyAI>()?.IsSmallDino == true) return;
        }

        CurrentChaseTarget = target;
        TargetDino = targetDino;

        if (targetDino != null && targetDino.gameObject.activeInHierarchy)
        {
            if (!(this is BlindEnemyAI))
            {
                targetDino.NotifyPackFlee(this);
            }
        }

        if (StateMachine != null && ChaseState != null)
        {
            if (StateMachine.CurrentState != ChaseState) StateMachine.ChangeState(ChaseState);
            else ChaseState.Enter();
        }
    }

    public virtual void TriggerFlee(EnemyAI threat)
    {
        // Los dinosaurios grandes son depredadores dominantes y nunca huyen de otros dinosaurios
        if (IsBigDino) return;
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

    /// <summary>
    /// Retorna el ángulo en grados del cono de visión o del campo frontal sensible a la luz.
    /// </summary>
    public virtual float GetVisionOrFacingCone()
    {
        if (visionAngle > 0f) return visionAngle;
        return IsSmallDino ? 280f : 110f;
    }

    /// <summary>
    /// Hace que el dinosaurio huya despavorido del jugador si este enciende y apaga la linterna
    /// apuntándole dentro de su cono de visión.
    /// </summary>
    public virtual void TriggerScaredByLight(Transform threatPlayer)
    {
        if (threatPlayer == null) EnsurePlayerReference();
        Transform threat = threatPlayer != null ? threatPlayer : Player;
        if (threat == null) return;

        CurrentThreat = threat;
        ChasingPredator = null;
        SpottedThreatPosition = threat.position;
        SafeRange = 25f;

        if (StateMachine != null && FleeState != null)
        {
            if (StateMachine.CurrentState != FleeState)
            {
                StateMachine.ChangeState(FleeState);
            }
            else
            {
                FleeState.Enter();
            }
        }

        // Si es dinosaurio pequeño / presa, alertar a la manada
        if (IsSmallDino)
        {
            if (packFollowers != null)
            {
                for (int i = 0; i < packFollowers.Count; i++)
                {
                    EnemyAI follower = packFollowers[i];
                    if (follower != null && follower.gameObject.activeInHierarchy && follower != this)
                    {
                        follower.TriggerScaredByLight(threat);
                    }
                }
            }
            if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy && LeaderAI != this)
            {
                LeaderAI.TriggerScaredByLight(threat);
            }
        }
    }

    public virtual void TriggerPatrol()
    {
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;

        if (StateMachine == null) return;

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

    public void NotifyPackFlee(EnemyAI threat)
    {
        if (threat == null || threat == this) return;
        if (threat is BlindEnemyAI) return; // Los pequeños no huyen de los ciegos
        if (threat == LeaderAI || threat.EffectiveLeader == EffectiveLeader) return;
        if (threat.IsPrey || threat is SmallEnemyAI) return;

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
            if (predator is BlindEnemyAI) return;
            NotifyPackFlee(predator);
        }
        else if (threat != null)
        {
            EnemyAI threatAI = threat.GetComponentInParent<EnemyAI>();
            if (threatAI != null)
            {
                if (threatAI is BlindEnemyAI) return;
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

    public virtual bool CanSeeBigDino(out EnemyAI spottedPredator)
    {
        spottedPredator = null;
        if (IsBigDino) return false;

        for (int i = 0; i < AllPredators.Count; i++)
        {
            EnemyAI predator = AllPredators[i];
            if (predator == null || !predator.gameObject.activeInHierarchy || predator == this) continue;
            if (predator == LeaderAI || predator.EffectiveLeader == EffectiveLeader) continue;
            if (predator.IsPrey || predator is SmallEnemyAI || predator is BlindEnemyAI) continue;

            if (CanSeeSpecificDino(predator))
            {
                spottedPredator = predator;
                return true;
            }
        }

        return false;
    }

    public virtual bool CanSeePrey(out EnemyAI spottedPrey)
    {
        spottedPrey = null;
        if (IsSmallDino) return false;

        for (int i = 0; i < AllPrey.Count; i++)
        {
            EnemyAI prey = AllPrey[i];
            if (prey == null || !prey.gameObject.activeInHierarchy || prey == this) continue;
            if (prey == LeaderAI || prey.EffectiveLeader == EffectiveLeader) continue;
            if (!prey.IsPrey && !(prey is SmallEnemyAI)) continue;

            if (CanSeeSpecificDino(prey))
            {
                spottedPrey = prey;
                return true;
            }
        }

        return false;
    }

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
        AllPrey.Remove(this);
        AllPredators.Remove(this);
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

        RaycastHit[] hits = Physics.RaycastAll(eyePos, rayDir.normalized, rayDist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
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
