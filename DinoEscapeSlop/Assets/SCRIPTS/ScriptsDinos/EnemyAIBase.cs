using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Datos del dinosaurio")]
    public DinoBase data;

    [Header("Visión")]
    public Transform VisionCone;
    public Transform EyePoint;
    public LayerMask ObstacleMask;
    public bool PlayerInCone { get; set; }

    [Header("Referencias de Componentes")]
    public EnemyMovement Movement { get; private set; }
    public AStarPathfinding Pathfinding { get; private set; }

    [Header("Jugador")]
    public Transform Player;

    [Header("Rol y Líder")]
    public bool isLeader = false;
    public Transform leaderTransform;

    [Header("Flee")]
    public bool IsPrey = false;
    public float SafeRange = 22f;
    public FleeState FleeState { get; private set; }

    [Header("Chase")]
    public float DetectionRange = 15f;
    public float LoseRange = 25f;
    public float AttackRange = 2.5f;
    public float ReacquireCooldown = 2f;
    public float LastLostTime { get; set; } = -999f;
    public bool IsChasing { get; set; }

    [Header("Flocking")]
    public float FlockRadius = 10f;
    public float TooCloseRadius = 2.5f;

    [Header("Pesos del Flocking")]
    public float LeaderFollowWeight = 3f;
    public float SeparationWeight = 1.5f;
    public float CohesionWeight = 1.5f;
    public float AlignmentWeight = 1f;

    public StateMachine StateMachine { get; private set; }
    public FlockingState FlockingState { get; private set; }
    public ChaseState ChaseState { get; private set; }

    private void Awake()
    {
        Movement = GetComponent<EnemyMovement>();
        Pathfinding = FindObjectOfType<AStarPathfinding>();

        StateMachine = new StateMachine();
        FlockingState = new FlockingState(this);
        ChaseState = new ChaseState(this);
        FleeState = new FleeState(this);

        if (data != null)
            AttackRange = data.attackRange;
    }

    private void Start()
    {
        if (Player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                Player = p.transform;
        }

        StateMachine.Initialize(FlockingState);
        Debug.Log("Estado inicial: Flocking");
    }

    private void Update()
    {
        StateMachine.Update();
    }

    public bool CanSeePlayer()
    {
        if (Time.time < LastLostTime + ReacquireCooldown)
            return false;

        if (Player == null || !PlayerInCone)
            return false;

        Vector3 eye = EyePoint != null ? EyePoint.position
                                       : transform.position + Vector3.up * 1.5f;
        Vector3 target = Player.position + Vector3.up * 1f;

        return !Physics.Linecast(eye, target, ObstacleMask);
    }

    private void OnDrawGizmos()
    {
        if (isLeader)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.4f);
        }
        else if (leaderTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                transform.position + Vector3.up * 0.5f,
                leaderTransform.position + Vector3.up * 0.5f
            );
        }

        Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, TooCloseRadius);

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, FlockRadius);
    }
}