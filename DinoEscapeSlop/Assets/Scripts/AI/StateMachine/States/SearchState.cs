using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modo Búsqueda (Search):
/// Se activa cuando un dinosaurio pierde de vista al jugador durante la persecución.
/// 1. Va a la última posición conocida del jugador.
/// 2. Allí se detiene y barre la zona con la mirada.
/// 3. Revisa hasta 3 nodos cercanos a ese punto (alcanzables por Raycast, sin repetir).
/// 4. Si ve al jugador en cualquier momento -> Persecución. Si ve a un depredador grande (pequeños) -> Huida.
/// 5. Si no encuentra nada, vuelve a Patrulla.
/// </summary>
public class SearchState : IState
{
    private EnemyAI ai;

    private enum Phase { GoingToLastPos, Scanning }
    private Phase phase;

    private readonly List<Node> visited = new List<Node>();
    private int nodesChecked;
    private float scanTimer;
    private float moveTimer;
    private float totalTimer;
    private float scanBaseYaw;

    private const float SearchRadius = 12f;   // radio alrededor del último punto conocido
    private const int NodesToCheck = 3;       // nodos extra que revisa
    private const float ScanTime = 1.8f;      // segundos mirando alrededor en cada punto
    private const float MoveTimeout = 6f;     // seguridad por si se atasca
    private const float TotalTimeout = 25f;   // límite total de búsqueda

    public SearchState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"[{ai.gameObject.name}] ha perdido al jugador. Entra en MODO BÚSQUEDA.");
        visited.Clear();
        nodesChecked = 0;
        totalTimer = 0f;

        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);

        if (ai.Movement != null)
        {
            // Paso rápido: a medio camino entre patrulla y persecución
            ai.Movement.speed = (ai.patrolSpeed + ai.chaseSpeed) * 0.5f;
        }
        GoTo(ai.SearchOrigin);
    }

    public void Update()
    {
        // 1. Si vuelve a ver algo, la supervivencia y el ataque priman
        if (ai.IsSmallDino && ai.CanSeeBigDino(out EnemyAI bigDino))
        {
            ai.NotifyPackFlee(bigDino);
            return;
        }

        if (ai.CanSeePlayer())
        {
            Debug.Log($"[{ai.gameObject.name}] encontró al jugador durante la búsqueda. ¡MODO ATAQUE!");
            if (ai.IsSmallDino) ai.NotifyPackAttack(ai.Player);
            else ai.TriggerAttack(ai.Player);
            return;
        }

        // 2. Límite total
        totalTimer += Time.deltaTime;
        if (totalTimer >= TotalTimeout || ai.Movement == null)
        {
            ai.TriggerPatrol();
            return;
        }

        // 3. Fases
        if (phase == Phase.GoingToLastPos)
        {
            moveTimer += Time.deltaTime;
            if (ai.Movement.HasReachedDestination || moveTimer >= MoveTimeout)
            {
                StartScan();
            }
        }
        else
        {
            scanTimer += Time.deltaTime;

            // Barrido de la mirada de lado a lado
            float sweep = Mathf.Sin(scanTimer * 3f) * 70f;
            Quaternion look = Quaternion.Euler(0f, scanBaseYaw + sweep, 0f);
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, look, 200f * Time.deltaTime);

            if (scanTimer >= ScanTime)
            {
                GoToNextSearchNode();
            }
        }
    }

    private void GoTo(Vector3 pos)
    {
        phase = Phase.GoingToLastPos;
        moveTimer = 0f;
        if (ai.Movement != null) ai.Movement.MoveTo(pos);
    }

    private void StartScan()
    {
        phase = Phase.Scanning;
        scanTimer = 0f;
        scanBaseYaw = ai.transform.eulerAngles.y;
        ai.Movement.ClearPath();
        ai.Movement.ResetVelocity();
    }

    private void GoToNextSearchNode()
    {
        if (nodesChecked >= NodesToCheck)
        {
            ai.TriggerPatrol();
            return;
        }

        Node next = PickSearchNode();
        if (next == null)
        {
            ai.TriggerPatrol();
            return;
        }

        nodesChecked++;
        visited.Add(next);
        GoTo(next.transform.position);
    }

    /// <summary>
    /// Elige al azar un nodo visible (Raycast) cerca del último punto conocido, no revisado y no pegado al dino.
    /// </summary>
    private Node PickSearchNode()
    {
        List<Node> reachable = ai.Movement.GetReachableNodes();
        List<Node> candidates = new List<Node>();

        for (int i = 0; i < reachable.Count; i++)
        {
            Node n = reachable[i];
            if (n == null || visited.Contains(n)) continue;

            Vector3 p = n.transform.position;
            float dOrigin = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(ai.SearchOrigin.x, ai.SearchOrigin.z));
            float dMe = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(ai.transform.position.x, ai.transform.position.z));

            if (dOrigin <= SearchRadius && dMe >= 3f) candidates.Add(n);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public void Exit()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
            ai.Movement.ResetVelocity();
            ai.Movement.GoToNearestPatrolNode();
        }
    }
}