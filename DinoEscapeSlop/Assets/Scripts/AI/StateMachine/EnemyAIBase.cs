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
    [Tooltip("¿Este enemigo es el líder del grupo?")]
    public bool isLeader = false;

    [Tooltip("Transform del enemigo marcado como Líder (para los seguidores)")]
    public Transform leaderTransform = null;

    [Tooltip("Lista de seguidores de la manada (solo relevante si es líder)")]
    public List<EnemyAI> packFollowers = new List<EnemyAI>();


    [Header("Dinosaurio ciego")]
    public bool dinoCiego = false;

    public EnemyAI LeaderAI { get; private set; }

    /// <summary>
    /// Devuelve el líder efectivo de la manada.
    /// Si este dinosaurio sigue a un líder, devuelve a dicho líder.
    /// Si no tiene líder (dinosaurio autónomo o líder de manada), se devuelve a sí mismo.
    /// </summary>
    public EnemyAI EffectiveLeader
    {
        get
        {
            if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
            {
                return LeaderAI;
            }
            return this;
        }
    }

    /// <summary>
    /// Comprueba si dos dinosaurios pequeños se están viendo mutuamente:
    /// Ambos deben encontrarse dentro del campo de visión y rango del otro,
    /// y sin muros ni obstáculos que bloqueen la línea directa de visión entre sus ojos.
    /// Solo aplicable a dinosaurios pequeños (los grandes nunca forman manada).
    /// </summary>
    public bool MutuallySees(EnemyAI other)
    {
        if (other == null || other == this || !other.gameObject.activeInHierarchy) return false;
        if (!this.IsSmallDino || !other.IsSmallDino) return false;

        return this.CanSeeSpecificDino(other) && other.CanSeeSpecificDino(this);
    }

    /// <summary>
    /// Asigna a este dinosaurio como seguidor de un nuevo líder,
    /// añadiéndolo a su lista de seguidores y limpiando rutas de patrulla propias.
    /// </summary>
    public void SetLeader(EnemyAI newLeader)
    {
        if (newLeader == null || newLeader == this)
        {
            PromoteToLeader();
            return;
        }

        if (LeaderAI != null && LeaderAI != newLeader)
        {
            LeaderAI.packFollowers.Remove(this);
        }

        isLeader = false;
        LeaderAI = newLeader;
        leaderTransform = newLeader.transform;

        if (!newLeader.packFollowers.Contains(this))
        {
            newLeader.packFollowers.Add(this);
        }

        if (Movement != null)
        {
            Movement.patrolNodes.Clear();
            Movement.ClearPath();
        }
    }

    /// <summary>
    /// Promociona a este dinosaurio como líder de manada independiente si tiene seguidores,
    /// o como agente autónomo si se encuentra solo.
    /// </summary>
    public void PromoteToLeader()
    {
        if (LeaderAI != null)
        {
            LeaderAI.packFollowers.Remove(this);
        }

        LeaderAI = null;
        leaderTransform = null;
        isLeader = (packFollowers != null && packFollowers.Count > 0);

        if (Movement != null && Movement.HasReachedDestination)
        {
            Movement.GoToNextPatrolNode();
        }
    }

    /// <summary>
    /// Resuelve el encuentro cuando dos dinosaurios pequeños se ven mutuamente:
    /// Se elige aleatoriamente (50% / 50%) a uno como líder ganador.
    /// El otro pasa a comportamiento 'Follow the Leader',
    /// transfiriendo además todos sus seguidores al líder ganador.
    /// </summary>
    public static void ResolveEncounter(EnemyAI leaderA, EnemyAI leaderB)
    {
        if (leaderA == null || leaderB == null || leaderA == leaderB) return;
        if (!leaderA.gameObject.activeInHierarchy || !leaderB.gameObject.activeInHierarchy) return;
        if (leaderA.IsBigDino || leaderB.IsBigDino) return;

        // 50% de probabilidad para elegir aleatoriamente al líder ganador
        bool aWins = (Random.value < 0.5f);
        EnemyAI winner = aWins ? leaderA : leaderB;
        EnemyAI loser = aWins ? leaderB : leaderA;

        Debug.Log($"[Manada] ¡Dos dinosaurios pequeños se vieron mutuamente! Se introduce Follow the Leader: '{winner.gameObject.name}' queda como LÍDER (50%). '{loser.gameObject.name}' pasa a seguirlo.");

        winner.isLeader = true;
        winner.LeaderAI = null;
        winner.leaderTransform = null;

        // El perdedor deja de ser líder y pasa a seguir al ganador
        loser.SetLeader(winner);

        // Transferir los seguidores del perdedor a la manada del ganador
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

    [Header("Configuración de Presa / Depredador")]
    [Tooltip("true = dinosaurio presa pequeño, false = dinosaurio grande depredador")]
    public bool IsPrey = false;
    public bool isPrey { get => IsPrey; set => IsPrey = value; }

    public bool IsBigDino => !IsPrey;
    public bool IsSmallDino => IsPrey;

    public float SafeRange = 22f;      // distancia a la que deja de huir

    [Header("Objetivos Dinámicos")]
    public Transform CurrentThreat { get; set; }
    public EnemyAI ChasingPredator { get; set; }
    public Vector3 SpottedThreatPosition { get; set; }
    public Transform CurrentChaseTarget { get; set; }
    public EnemyAI TargetDino { get; set; }

    // Registros estáticos para búsqueda instantánea
    public static readonly List<EnemyAI> AllEnemies = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllPrey = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllPredators = new List<EnemyAI>();
    private static bool hasDistributedPrey = false;

    public FleeState FleeState { get; private set; }

    [Header("Configuración de Visión y Persecución")]
    [Tooltip("Distancia máxima de visión del enemigo (2.5x más largo: 7.5 metros).")]
    public float visionRange = 7.5f;

    [Tooltip("Campo de visión horizontal total en grados (Presas: 270 grados, Depredadores: 100 grados).")]
    public float visionAngle = 100f;

    [Tooltip("Altura de los ojos del dinosaurio para el cálculo de visión.")]
    public float eyeHeight = 0.65f;

    [Tooltip("Distancia de detección.")]
    public float DetectionRange = 7.5f;

    [Tooltip("Distancia a la que pierde de vista completamente.")]
    public float LoseRange = 18f;

    [Tooltip("Distancia mínima para atrapar / matar al jugador.")]
    public float AttackRange = 1.8f;

    [Header("Configuración de Velocidades")]
    [Tooltip("Velocidad normal de patrulla natural.")]
    public float patrolSpeed = 2.4f;

    [Tooltip("Velocidad rápida durante la persecución (notablemente más rápida).")]
    public float chaseSpeed = 7.8f;

    [Tooltip("Velocidad máxima durante la huida (sprint acelerado).")]
    public float fleeSpeed = 10.5f;

    [Header("Tiempos de Persecución y Desiste")]
    [Tooltip("Tiempo máximo que el dinosaurio grande perseguirá a un dinosaurio pequeño antes de desistir y volver a patrulla.")]
    public float preyMaxChaseTime = 3.5f;

    [Tooltip("Tiempo que tarda el dinosaurio grande en desistir tras perder de vista a un dinosaurio pequeño.")]
    public float preyLostSightTime = 2.0f;

    [Tooltip("Tiempo que tarda en desistir tras perder de vista al jugador.")]
    public float playerLostSightTime = 5.0f;

    [Tooltip("Tiempo de enfriamiento tras desistir de cazar a una presa antes de poder volver a perseguir presas.")]
    public float preyChaseCooldown = 3.0f;

    [HideInInspector]
    public float preyCooldownTimer = 0f;

    [Header("Configuración de Flocking")]
    [Tooltip("Radio en el que busca aliados cerca")]
    public float FlockRadius = 10f;

    [Tooltip("Distancia mínima para empezar a separarse de los aliados")]
    public float TooCloseRadius = 2.5f;

    [Header("Pesos del Flocking")]
    public float LeaderFollowWeight = 3f;
    public float SeparationWeight = 1.5f;
    public float CohesionWeight = 1.5f;
    public float AlignmentWeight = 1f;

    // Máquina y estados
    public StateMachine StateMachine { get; private set; }
    public Flockingstate FlockingState { get; private set; }
    public ChaseState ChaseState { get; private set; }

    // Estado de muerte del jugador
    public static bool GlobalPlayerDead = false;
    public static EnemyAI KillerDino { get; private set; }
    public bool isPlayerDead => GlobalPlayerDead;

    // Referencias para el cambio visual a rojo durante la persecución
    private Renderer[] dinoRenderers;
    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void OnEnable()
    {
        if (!AllEnemies.Contains(this)) AllEnemies.Add(this);
        UpdateStaticCategory();
    }

    private void OnDisable()
    {
        AllEnemies.Remove(this);
        AllPrey.Remove(this);
        AllPredators.Remove(this);
        if (LeaderAI != null)
        {
            LeaderAI.packFollowers.Remove(this);
        }
    }

    public void UpdateStaticCategory()
    {
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

    private void Awake()
    {
        Movement = GetComponent<EnemyMovement>();
        Pathfinding = GetComponent<AStarPathfinding>();
        if (Pathfinding == null) Pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();

        // Distinción entre dinosaurio grande y dinosaurio pequeño por el tamaño físico del modelo:
        float scaleY = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        Renderer rend = GetComponentInChildren<Renderer>();
        float boundsHeight = (rend != null) ? rend.bounds.size.y : 0f;
        bool isBig = (scaleY > 1.4f) || (boundsHeight > 1.4f);

        IsPrey = !isBig; // Grande = depredador (IsPrey = false), Pequeño = presa (IsPrey = true)
        UpdateStaticCategory();

        // Nada predestinado desde el spawn: todos los dinos comienzan de forma completamente autónoma
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();
        if (IsSmallDino)
        {
            // Pequeños: agentes autónomos independientes, sin líder predestinado
            visionAngle = 280f; // Cono de visión amplio y redondeado
            visionRange = 11.5f; // Cono de visión más largo (antes 7.5f)
            DetectionRange = 11.5f;
            eyeHeight = 0.35f;
            AttackRange = 1.3f;
            patrolSpeed = 5.2f;
            chaseSpeed = 9.8f;
            fleeSpeed = 13.0f;
        }
        else
        {
            // Grandes: depredadores solitarios, NUNCA participan en follow the leader
            visionAngle = 100f;
            visionRange = 8.5f;
            eyeHeight = 0.85f;
            AttackRange = 2.0f;
            patrolSpeed = 2.4f;
            chaseSpeed = 7.5f;
            fleeSpeed = 8.0f;
        }
        if (dinoCiego) //VALORES PARA DINO CIEGO
        {
            visionRange = 2.5f;
            visionAngle = 359f;
            DetectionRange = 2f;
            LoseRange = 3f;
        }

        StateMachine = new StateMachine();
        FlockingState = new Flockingstate(this);
        ChaseState = new ChaseState(this);
        FleeState = new FleeState(this);

        StateMachine.AllowState(FlockingState);
        StateMachine.AllowState(ChaseState);
        StateMachine.AllowState(FleeState);
    }

    private void Start()
    {
        EnsurePlayerReference();

        // Garantizar que nada quede predestinado desde el Inspector en el spawn
        isLeader = false;
        leaderTransform = null;
        LeaderAI = null;
        packFollowers.Clear();

        if (IsPrey)
        {
            // Espaciar a todos los dinosaurios pequeños por el mapa en nodos alejados entre sí
            if (!hasDistributedPrey)
            {
                hasDistributedPrey = true;
                DistributePreyAcrossMap();
            }
        }

        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
        }

        // Color inicial normal
        SetVisualState(DinoVisualState.Normal);

        StateMachine.Initialize(FlockingState);
    }

    private void Update()
    {
        if (preyCooldownTimer > 0f)
        {
            preyCooldownTimer -= Time.deltaTime;
        }

        if (GlobalPlayerDead)
        {
            // Permitir reiniciar rápidamente la escena con la tecla R
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
                FirstPersonMovement fpm = p.GetComponentInParent<FirstPersonMovement>();
                if (fpm == null) fpm = p.GetComponentInChildren<FirstPersonMovement>();
                Player = fpm != null ? fpm.transform : p.transform;
            }
        }
    }

    public enum DinoVisualState
    {
        Normal,
        Chasing,  // Rojo
        Fleeing   // Azul
    }

    /// <summary>
    /// Cambia el aspecto del dinosaurio: rojo al perseguir, azul al huir, y normal al patrullar.
    /// </summary>
    public void SetVisualState(DinoVisualState state)
    {
        if (dinoRenderers == null || dinoRenderers.Length == 0)
        {
            dinoRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (propBlock == null)
        {
            propBlock = new MaterialPropertyBlock();
        }

        Color targetColor;
        Color targetEmission;

        switch (state)
        {
            case DinoVisualState.Chasing:
                targetColor = new Color(1f, 0.12f, 0.12f, 1f); // Rojo intenso
                targetEmission = new Color(0.9f, 0.05f, 0.05f, 1f);
                break;
            case DinoVisualState.Fleeing:
                targetColor = new Color(0.15f, 0.55f, 1f, 1f); // Azul para huida
                targetEmission = new Color(0.05f, 0.45f, 1f, 1f);
                break;
            default:
                targetColor = Color.white;
                targetEmission = Color.black;
                break;
        }

        foreach (var rend in dinoRenderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, targetColor);
            propBlock.SetColor(ColorId, targetColor);
            propBlock.SetColor(EmissionColorId, targetEmission);
            rend.SetPropertyBlock(propBlock);

            // Modificación directa sobre material para asegurar visibilidad en shaders URP
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

    public void SetChasingVisuals(bool isChasing)
    {
        SetVisualState(isChasing ? DinoVisualState.Chasing : DinoVisualState.Normal);
    }

    public void SetFleeingVisuals(bool isFleeing)
    {
        SetVisualState(isFleeing ? DinoVisualState.Fleeing : DinoVisualState.Normal);
    }

    /// <summary>
    /// Distribuye a todos los dinosaurios pequeños por el mapa en nodos alejados entre sí
    /// utilizando Farthest Point Sampling sobre la red completa de nodos.
    /// </summary>
    public static void DistributePreyAcrossMap()
    {
        if (AllPrey == null || AllPrey.Count == 0) return;

        AStarPathfinding pf = Object.FindAnyObjectByType<AStarPathfinding>();
        if (pf == null) return;

        Node[] allNodes = pf.GetAllNodes();
        if (allNodes == null || allNodes.Length == 0) return;

        List<Node> validNodes = new List<Node>();
        for (int i = 0; i < allNodes.Length; i++)
        {
            if (allNodes[i] != null) validNodes.Add(allNodes[i]);
        }
        if (validNodes.Count == 0) return;

        int preyCount = AllPrey.Count;
        List<Node> chosenNodes = new List<Node>();

        // 1. Escoger el primer nodo en un extremo
        chosenNodes.Add(validNodes[0]);

        // 2. Farthest Point Sampling: cada siguiente nodo maximiza la distancia mínima a los ya escogidos
        while (chosenNodes.Count < preyCount && chosenNodes.Count < validNodes.Count)
        {
            Node bestCandidate = null;
            float maxMinDist = -1f;

            for (int i = 0; i < validNodes.Count; i++)
            {
                Node candidate = validNodes[i];
                if (chosenNodes.Contains(candidate)) continue;

                float minDistToChosen = float.MaxValue;
                for (int c = 0; c < chosenNodes.Count; c++)
                {
                    float d = Vector2.Distance(
                        new Vector2(candidate.transform.position.x, candidate.transform.position.z),
                        new Vector2(chosenNodes[c].transform.position.x, chosenNodes[c].transform.position.z)
                    );
                    if (d < minDistToChosen) minDistToChosen = d;
                }

                if (minDistToChosen > maxMinDist)
                {
                    maxMinDist = minDistToChosen;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate != null)
            {
                chosenNodes.Add(bestCandidate);
            }
            else
            {
                break;
            }
        }

        // 3. Situar a cada dinosaurio pequeño en uno de los nodos escogidos
        for (int i = 0; i < preyCount; i++)
        {
            EnemyAI prey = AllPrey[i];
            if (prey == null) continue;

            Node targetNode = chosenNodes[i % chosenNodes.Count];
            Vector3 spawnPos = targetNode.transform.position;
            spawnPos.y = prey.transform.position.y;

            prey.transform.position = spawnPos;

            if (targetNode.neighbors != null && targetNode.neighbors.Count > 0 && targetNode.neighbors[0] != null)
            {
                Vector3 fwd = targetNode.neighbors[0].transform.position - prey.transform.position;
                fwd.y = 0;
                if (fwd.sqrMagnitude > 0.001f)
                {
                    prey.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                }
            }

            if (prey.Movement != null)
            {
                prey.Movement.patrolNodes.Clear();
                prey.Movement.ClearPath();
                prey.Movement.ResetVelocity();
                prey.Movement.SetCurrentDestinationNode(targetNode);

                // Asignar inmediatamente el avance hacia el siguiente nodo conectado de la red
                Node nextPatrol = prey.Movement.GetNextNetworkPatrolNode();
                if (nextPatrol != null)
                {
                    prey.Movement.MoveTo(nextPatrol.transform.position);
                }
            }
        }

        Debug.Log($"[EnemyAI] {preyCount} dinosaurios pequeños distribuidos exitosamente por los nodos del mapa.");
    }

    /// <summary>
    /// Comprueba si algún dinosaurio enemigo / amenaza se encuentra en su campo de visión.
    /// Para dinosaurios presa (pequeños), SOLO se consideran amenazas los depredadores grandes (!other.IsPrey).
    /// Las demás presas pequeñas son de su misma especie y no provocan huida.
    /// </summary>
    public bool CanSeeOtherDinosaur(out EnemyAI spottedDino)
    {
        spottedDino = null;
        float range = visionRange > 0 ? visionRange : 8.0f;
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.2f;
        int wallMask = ~((1 << 3) | (1 << 2));

        for (int i = 0; i < AllEnemies.Count; i++)
        {
            EnemyAI other = AllEnemies[i];
            if (other == null || !other.gameObject.activeInHierarchy || other == this) continue;

            // Si soy presa pequeña: SOLO los depredadores grandes provocan huida
            if (IsPrey && other.IsPrey) continue;

            // Si soy depredador: los demás depredadores no provocan huida
            if (!IsPrey && !other.IsPrey) continue;

            Vector3 toOther = other.transform.position - transform.position;
            float dist = toOther.magnitude;
            if (dist > range) continue;

            Vector3 toOtherHoriz = new Vector3(toOther.x, 0f, toOther.z);
            if (toOtherHoriz.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.Angle(transform.forward, toOtherHoriz.normalized);
                if (angle > visionAngle * 0.5f) continue;
            }

            Vector3 targetPos = other.transform.position + Vector3.up * other.eyeHeight;
            Vector3 rayDir = targetPos - eyePos;
            float rayDist = rayDir.magnitude;

            if (!Physics.Raycast(eyePos, rayDir.normalized, rayDist, wallMask, QueryTriggerInteraction.Ignore))
            {
                spottedDino = other;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Activa el modo Ataque / Persecución (Chase). Puede perseguir al jugador (targetDino == null) o a un dinosaurio pequeño.
    /// </summary>
    public void TriggerAttack(Transform target, EnemyAI targetDino = null)
    {
        if (target == null) return;
        CurrentChaseTarget = target;
        TargetDino = targetDino;
        if (StateMachine != null)
        {
            if (StateMachine.CurrentState != ChaseState)
            {
                StateMachine.ChangeState(ChaseState);
            }
            else
            {
                ChaseState.Enter();
            }
        }
    }

    /// <summary>
    /// Activa el modo Huida (Flee) en dirección opuesta a donde se avistó al dinosaurio grande.
    /// </summary>
    public void TriggerFlee(EnemyAI threat)
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

    /// <summary>
    /// Activa el modo Patrulla (Patrol) por la red de nodos.
    /// </summary>
    public void TriggerPatrol()
    {
        CurrentChaseTarget = null;
        TargetDino = null;
        CurrentThreat = null;
        ChasingPredator = null;
        SpottedThreatPosition = Vector3.zero;
        if (StateMachine != null && StateMachine.CurrentState != FlockingState)
        {
            StateMachine.ChangeState(FlockingState);
        }
    }

    public void NotifyPackAttack(Transform target)
    {
        TriggerAttack(target);

        // Si soy seguidor, avisar al líder
        if (!isLeader && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            if (LeaderAI.StateMachine != null && LeaderAI.StateMachine.CurrentState != LeaderAI.ChaseState)
            {
                LeaderAI.TriggerAttack(target);
            }
        }

        // Si soy líder, avisar a todos los seguidores de la manada
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
        TriggerFlee(threat);

        // Si soy seguidor, avisar al líder de la amenaza
        if (!isLeader && LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            if (LeaderAI.StateMachine != null && LeaderAI.StateMachine.CurrentState != LeaderAI.FleeState)
            {
                LeaderAI.TriggerFlee(threat);
            }
        }

        // Si soy líder, avisar a todos los seguidores para que huyan de inmediato
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
        if (predator != null) NotifyPackFlee(predator);
        else if (threat != null)
        {
            EnemyAI threatAI = threat.GetComponent<EnemyAI>();
            if (threatAI != null) NotifyPackFlee(threatAI);
        }
    }

    public void NotifyPackPatrol() => TriggerPatrol();

    /// <summary>
    /// Comprueba si un dinosaurio específico se encuentra dentro del cono de visión y sin muros que bloqueen el paso.
    /// </summary>
    public bool CanSeeSpecificDino(EnemyAI targetDino)
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

        int wallMask = ~((1 << 3) | (1 << 2));
        if (Physics.Raycast(eyePos, rayDir.normalized, out RaycastHit hit, rayDist, wallMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.normal.y < 0.7f)
            {
                if (hit.transform != targetDino.transform && !hit.transform.IsChildOf(targetDino.transform) &&
                    hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Comprueba si algún dinosaurio grande (depredador) se encuentra dentro del campo de visión.
    /// Solo relevante para dinosaurios pequeños (presas). Los dinosaurios grandes ignoran a otros grandes.
    /// </summary>
    public bool CanSeeBigDino(out EnemyAI spottedPredator)
    {
        spottedPredator = null;
        if (IsBigDino) return false;

        float range = visionRange;
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.2f;
        int wallMask = ~((1 << 3) | (1 << 2));

        for (int i = 0; i < AllPredators.Count; i++)
        {
            EnemyAI predator = AllPredators[i];
            if (predator == null || !predator.gameObject.activeInHierarchy || predator == this) continue;

            Vector3 toPred = predator.transform.position - transform.position;
            float dist = toPred.magnitude;
            if (dist > range) continue;

            Vector3 toPredHoriz = new Vector3(toPred.x, 0f, toPred.z);
            if (toPredHoriz.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.Angle(transform.forward, toPredHoriz.normalized);
                if (angle > visionAngle * 0.5f) continue;
            }

            Vector3 targetPos = predator.transform.position + Vector3.up * predator.eyeHeight;
            Vector3 rayDir = targetPos - eyePos;
            float rayDist = rayDir.magnitude;

            if (!Physics.Raycast(eyePos, rayDir.normalized, out RaycastHit hit, rayDist, wallMask, QueryTriggerInteraction.Ignore) || hit.normal.y >= 0.7f)
            {
                spottedPredator = predator;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Comprueba si alguna presa pequeña se encuentra dentro del cono de visión del dinosaurio grande.
    /// Solo relevante para dinosaurios grandes.
    /// </summary>
    public bool CanSeePrey(out EnemyAI spottedPrey)
    {
        spottedPrey = null;
        if (IsSmallDino) return false;

        float range = visionRange;
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.3f;
        int wallMask = ~((1 << 3) | (1 << 2));

        for (int i = 0; i < AllPrey.Count; i++)
        {
            EnemyAI prey = AllPrey[i];
            if (prey == null || !prey.gameObject.activeInHierarchy || prey == this) continue;

            Vector3 toPrey = prey.transform.position - transform.position;
            float dist = toPrey.magnitude;
            if (dist > range) continue;

            Vector3 toPreyHoriz = new Vector3(toPrey.x, 0f, toPrey.z);
            if (toPreyHoriz.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.Angle(transform.forward, toPreyHoriz.normalized);
                if (angle > visionAngle * 0.5f) continue;
            }

            Vector3 targetPos = prey.transform.position + Vector3.up * prey.eyeHeight;
            Vector3 rayDir = targetPos - eyePos;
            float rayDist = rayDir.magnitude;

            if (!Physics.Raycast(eyePos, rayDir.normalized, out RaycastHit hit, rayDist, wallMask, QueryTriggerInteraction.Ignore) || hit.normal.y >= 0.7f)
            {
                spottedPrey = prey;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Muerte de una presa devorada por un depredador.
    /// Si era líder de manada, transfiere el liderazgo a uno de sus seguidores sobrevivientes.
    /// </summary>
    public void Die()
    {
        Debug.Log($"¡{gameObject.name} (Dinosaurio Pequeño) ha sido devorado por un dinosaurio grande!");
        if (LeaderAI != null)
        {
            LeaderAI.packFollowers.Remove(this);
        }

        // Si era líder y tenía seguidores, gestionar la manada sobreviviente
        if (isLeader && packFollowers != null && packFollowers.Count > 0)
        {
            List<EnemyAI> activeFollowers = packFollowers.FindAll(f => f != null && f.gameObject.activeInHierarchy && f != this);
            if (activeFollowers.Count == 1)
            {
                // Si solo queda 1 seguidor, vuelve a ser autónomo (sin líder predestinado)
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
                // Si quedan 2 o más, uno asume el liderazgo y los demás lo siguen
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
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Mata al jugador: detiene el tiempo de juego, libera el cursor y activa la pantalla de muerte.
    /// </summary>
    public void KillPlayer()
    {
        if (GlobalPlayerDead) return;
        GlobalPlayerDead = true;
        KillerDino = this;

        string killer = IsSmallDino ? "un dinosaurio pequeño" : $"el dinosaurio grande ({gameObject.name})";
        Debug.Log($"¡{killer} atrapó y mató al jugador! Fin de la partida.");
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Comprueba si cualquier miembro de la manada (líder o seguidores) tiene línea de visión directa con el jugador.
    /// Si al menos uno puede verlo, comparte la posición del jugador con toda la manada.
    /// </summary>
    public bool PackCanSeePlayer(out Vector3 targetPos)
    {
        targetPos = Vector3.zero;

        if (dinoCiego)
            return false;
        // Comprobación individual directa
        if (CanSeePlayer())
        {
            if (Player != null) targetPos = Player.position;
            return true;
        }

        // Si es líder, consultar a los seguidores de la manada
        if (isLeader)
        {
            if (packFollowers != null)
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
        }
        // Si es seguidor, consultar a través del líder
        else if (LeaderAI != null && LeaderAI.gameObject.activeInHierarchy)
        {
            return LeaderAI.PackCanSeePlayer(out targetPos);
        }

        return false;
    }

    /// <summary>
    /// Comprueba si el jugador se encuentra dentro del cono de visión
    /// y sin obstáculos sólidos en medio.
    /// </summary>
    public bool CanSeePlayer()
    {
        if (Player == null)
        {
            EnsurePlayerReference();
            if (Player == null) return false;
        }

        bool isChasing = (StateMachine != null && StateMachine.CurrentState == ChaseState);
        float baseRange = isChasing ? Mathf.Max(visionRange, (LoseRange > 0 ? LoseRange : 18.0f)) : visionRange;
        float effectiveRange = baseRange;

        // Modificadores según el estado de sigilo del jugador
        FirstPersonMovement playerMovement = Player.GetComponent<FirstPersonMovement>();
        if (playerMovement == null) playerMovement = Player.GetComponentInParent<FirstPersonMovement>();
        if (playerMovement == null) playerMovement = Player.GetComponentInChildren<FirstPersonMovement>();

        if (playerMovement != null)
        {
            if (playerMovement.IsCrouched && !isChasing)
            {
                effectiveRange *= playerMovement.stealthDetectionMultiplier; // Sigilo (Shift): 50% de rango al patrullar
            }
            else if (playerMovement.IsRunning)
            {
                effectiveRange *= 1.25f; // Al correr hace más ruido y es más visible
            }
        }

        Vector3 toPlayer = Player.position - transform.position;
        float distance = toPlayer.magnitude;

        // 1. Comprobación de distancia
        if (distance > effectiveRange) return false;

        // 2. Comprobación de ángulo horizontal del cono de visión
        Vector3 toPlayerHorizontal = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (toPlayerHorizontal.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, toPlayerHorizontal.normalized);
            // Durante persecución a corta distancia (< 3.5m), se mantiene percepción envolvente (evita perder de vista por un esquive)
            float maxAllowedAngle = (isChasing && distance < 3.5f) ? 360f : visionAngle;
            if (angle > maxAllowedAngle * 0.5f) return false;
        }

        // 3. Comprobación de línea de visión (raycast para obstáculos/paredes)
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.3f;
        Vector3 targetPos = Player.position + Vector3.up * 0.9f;
        Vector3 rayDir = targetPos - eyePos;
        float rayDist = rayDir.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(eyePos, rayDir.normalized, rayDist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            // Otros dinosaurios aliados no deben bloquear la visión del jugador
            bool isOtherEnemy = hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null;
            if (isOtherEnemy) continue;

            if (hit.transform == Player || hit.transform.IsChildOf(Player))
            {
                return true;
            }

            // Choca primero con un obstáculo o pared
            return false;
        }

        return false;
    }

    /// <summary>
    /// Muestra la pantalla y texto de muerte al ser atrapado por el dinosaurio
    /// </summary>
    private void OnGUI()
    {
        if (!GlobalPlayerDead || KillerDino != this) return;

        // Oscurecer la pantalla
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
        GUI.color = oldColor;

        // Título de muerte
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = Mathf.Clamp(Screen.height / 14, 34, 66);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.95f, 0.15f, 0.15f, 1f);

        // Subtítulo explicativo
        GUIStyle subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = Mathf.Clamp(Screen.height / 28, 18, 28);
        subStyle.alignment = TextAnchor.MiddleCenter;
        subStyle.normal.textColor = Color.white;

        // Instrucción para reiniciar
        GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = Mathf.Clamp(Screen.height / 36, 15, 22);
        hintStyle.alignment = TextAnchor.MiddleCenter;
        hintStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.95f);

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;

        string subText = IsSmallDino ? "Has sido alcanzado y devorado por un dinosaurio pequeño." : "Has sido alcanzado y devorado por el dinosaurio grande.";

        GUI.Label(new Rect(centerX - 350, centerY - 90, 700, 70), "¡HAS MUERTO!", titleStyle);
        GUI.Label(new Rect(centerX - 350, centerY - 15, 700, 40), subText, subStyle);
        GUI.Label(new Rect(centerX - 350, centerY + 45, 700, 40), "Presiona [ R ] para volver a intentarlo", hintStyle);
    }

    /// <summary>
    /// Gizmos para depuración del cono de visión redondeado en la vista de escena.
    /// </summary>
    private void OnDrawGizmos()
    {
        bool isChasing = (StateMachine != null && StateMachine.CurrentState == ChaseState);
        bool isFleeing = (StateMachine != null && StateMachine.CurrentState == FleeState);

        if (isChasing) Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f); // Rojo
        else if (isFleeing) Gizmos.color = new Color(0.15f, 0.55f, 1f, 0.9f); // Azul
        else Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.7f); // Amarillo normal

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        float range = visionRange > 0 ? visionRange : (IsSmallDino ? 11.5f : 8.5f);
        float halfAngle = visionAngle * 0.5f;

        // Cono de visión redondeado: dibuja un arco suave circular de 32 segmentos a lo largo de visionAngle
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

        // Rayo indicador frontal central
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.35f);
        Gizmos.DrawRay(eyePos, transform.forward * range);

        // Indicador visual de líder de manada (solo pequeños y solo si tiene seguidores activos)
        if (IsSmallDino && isLeader && packFollowers != null && packFollowers.Count > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.35f);

            // Dibujar líneas hacia los seguidores de la manada
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
