using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyAI : MonoBehaviour
{
    [Header("Referencias de Componentes")]
    public EnemyMovement Movement { get; private set; }
    public AStarPathfinding Pathfinding { get; private set; }

    [Header("Jugador")]
    public Transform Player;

    [Header("Configuración de Rol y Líder")]
    [Tooltip("¿Este enemigo es el líder del grupo?")]
    public bool isLeader = true;

    [Tooltip("Transform del enemigo marcado como Líder (para los seguidores)")]
    public Transform leaderTransform;

    [Tooltip("Lista de seguidores de la manada (solo relevante si es líder)")]
    public List<EnemyAI> packFollowers = new List<EnemyAI>();

    public EnemyAI LeaderAI { get; private set; }

    [Header("Configuración de Presa / Depredador")]
    [Tooltip("true = dinosaurio presa pequeño, false = dinosaurio grande depredador")]
    public bool IsPrey = false;
    public bool isPrey { get => IsPrey; set => IsPrey = value; }

    public float SafeRange = 22f;      // distancia a la que deja de huir

    [Header("Objetivos Dinámicos")]
    public Transform CurrentThreat { get; set; }
    public EnemyAI ChasingPredator { get; set; }
    public Transform CurrentChaseTarget { get; set; }

    // Registros estáticos para búsqueda instantánea
    public static readonly List<EnemyAI> AllEnemies = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllPrey = new List<EnemyAI>();
    public static readonly List<EnemyAI> AllPredators = new List<EnemyAI>();

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
    public bool isPlayerDead { get; private set; } = false;

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
        // Si no está marcado como líder pero tampoco tiene un líder asignado, asumir rol autónomo/líder
        if (!isLeader && leaderTransform == null)
        {
            isLeader = true;
        }

        Movement = GetComponent<EnemyMovement>();
        Pathfinding = GetComponent<AStarPathfinding>();
        if (Pathfinding == null) Pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();

        UpdateStaticCategory();

        if (IsPrey)
        {
            visionAngle = 270f;
            eyeHeight = 0.35f;
            AttackRange = 1.3f;
            patrolSpeed = 4.5f;
            chaseSpeed = 8.5f;
            fleeSpeed = 10.5f;
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

        // Enlazar seguidores con el líder de la manada
        if (!isLeader && leaderTransform != null)
        {
            LeaderAI = leaderTransform.GetComponent<EnemyAI>();
            if (LeaderAI != null && !LeaderAI.packFollowers.Contains(this))
            {
                LeaderAI.packFollowers.Add(this);
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
        if (isPlayerDead)
        {
            // Permitir reiniciar rápidamente la escena con la tecla R
            if (Input.GetKeyDown(KeyCode.R))
            {
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
    /// Notifica a toda la manada que inicien la persecución coordinada del objetivo.
    /// </summary>
    public void NotifyPackChase(Transform target)
    {
        // Si lo detecta un seguidor, redirigir a través del líder para alertar a toda la manada
        if (!isLeader && LeaderAI != null)
        {
            LeaderAI.NotifyPackChase(target);
            return;
        }

        CurrentChaseTarget = target;
        if (StateMachine != null && StateMachine.CurrentState != ChaseState)
        {
            StateMachine.ChangeState(ChaseState);
        }

        for (int i = 0; i < packFollowers.Count; i++)
        {
            var follower = packFollowers[i];
            if (follower == null || !follower.gameObject.activeInHierarchy) continue;
            follower.CurrentChaseTarget = target;
            if (follower.StateMachine != null && follower.StateMachine.CurrentState != follower.ChaseState)
            {
                follower.StateMachine.ChangeState(follower.ChaseState);
            }
        }
    }

    /// <summary>
    /// Notifica a toda la manada que inicien la huida inmediata de un depredador grande.
    /// </summary>
    public void NotifyPackFlee(Transform threat, EnemyAI predator = null)
    {
        // Si lo detecta un seguidor, redirigir a través del líder para sincronizar a toda la manada
        if (!isLeader && LeaderAI != null)
        {
            LeaderAI.NotifyPackFlee(threat, predator);
            return;
        }

        CurrentThreat = threat;
        ChasingPredator = predator;
        if (StateMachine != null && StateMachine.CurrentState != FleeState)
        {
            StateMachine.ChangeState(FleeState);
        }

        for (int i = 0; i < packFollowers.Count; i++)
        {
            var follower = packFollowers[i];
            if (follower == null || !follower.gameObject.activeInHierarchy) continue;
            follower.CurrentThreat = threat;
            follower.ChasingPredator = predator;
            if (follower.StateMachine != null && follower.StateMachine.CurrentState != follower.FleeState)
            {
                follower.StateMachine.ChangeState(follower.FleeState);
            }
        }
    }

    /// <summary>
    /// Notifica a toda la manada que cancelen persecución o huida y regresen a patrulla.
    /// </summary>
    public void NotifyPackPatrol()
    {
        // Si lo inicia un seguidor, redirigir a través del líder para sincronizar a toda la manada
        if (!isLeader && LeaderAI != null)
        {
            LeaderAI.NotifyPackPatrol();
            return;
        }

        CurrentChaseTarget = null;
        CurrentThreat = null;
        ChasingPredator = null;

        if (StateMachine != null && StateMachine.CurrentState != FlockingState)
        {
            StateMachine.ChangeState(FlockingState);
        }

        for (int i = 0; i < packFollowers.Count; i++)
        {
            var follower = packFollowers[i];
            if (follower == null || !follower.gameObject.activeInHierarchy) continue;
            follower.CurrentChaseTarget = null;
            follower.CurrentThreat = null;
            follower.ChasingPredator = null;
            if (follower.StateMachine != null && follower.StateMachine.CurrentState != follower.FlockingState)
            {
                follower.StateMachine.ChangeState(follower.FlockingState);
            }
        }
    }

    /// <summary>
    /// Índice de ranura en la formación de la manada (0, 1, 2, 3...)
    /// </summary>
    public int FollowerIndex
    {
        get
        {
            if (isLeader || LeaderAI == null) return -1;
            return LeaderAI.packFollowers.IndexOf(this);
        }
    }

    /// <summary>
    /// Devuelve el desplazamiento local (X lateral, Z longitudinal) de la formación en cuña (V-Formation).
    /// </summary>
    public static Vector3 GetFormationSlotLocal(int slotIndex)
    {
        if (slotIndex < 0) return Vector3.zero;
        int row = (slotIndex / 2) + 1; // Fila 1, 2, 3...
        float side = (slotIndex % 2 == 0) ? -1f : 1f; // Izquierda (-), Derecha (+)
        float backDist = row * 1.5f;                  // 1.5m, 3.0m... hacia atrás
        float lateralDist = side * (1.1f + (row - 1) * 0.9f); // 1.1m, 2.0m... hacia los lados
        return new Vector3(lateralDist, 0f, -backDist);
    }

    /// <summary>
    /// Comprueba si algún dinosaurio grande (depredador) se encuentra dentro del campo de visión.
    /// </summary>
    public bool CanSeeBigDino(out EnemyAI spottedPredator)
    {
        spottedPredator = null;
        if (!IsPrey) return false;

        float range = visionRange;
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.2f;

        for (int i = 0; i < AllPredators.Count; i++)
        {
            EnemyAI predator = AllPredators[i];
            if (predator == null || !predator.gameObject.activeInHierarchy) continue;

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

            RaycastHit[] hits = Physics.RaycastAll(eyePos, rayDir.normalized, rayDist, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool clear = false;
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.CompareTag("Enemy") && hit.transform != predator.transform && !hit.transform.IsChildOf(predator.transform)) continue;

                if (hit.transform == predator.transform || hit.transform.IsChildOf(predator.transform))
                {
                    clear = true;
                    break;
                }

                break;
            }

            if (clear)
            {
                spottedPredator = predator;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Comprueba si alguna presa pequeña se encuentra dentro del campo de visión del depredador.
    /// </summary>
    public bool CanSeePrey(out EnemyAI spottedPrey)
    {
        spottedPrey = null;
        if (IsPrey) return false;

        float range = visionRange;
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.3f;

        for (int i = 0; i < AllPrey.Count; i++)
        {
            EnemyAI prey = AllPrey[i];
            if (prey == null || !prey.gameObject.activeInHierarchy) continue;

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

            RaycastHit[] hits = Physics.RaycastAll(eyePos, rayDir.normalized, rayDist, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool clear = false;
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.CompareTag("Enemy") && hit.transform != prey.transform && !hit.transform.IsChildOf(prey.transform)) continue;

                if (hit.transform == prey.transform || hit.transform.IsChildOf(prey.transform))
                {
                    clear = true;
                    break;
                }

                break;
            }

            if (clear)
            {
                spottedPrey = prey;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Muerte de una presa devorada por un depredador.
    /// </summary>
    public void Die()
    {
        Debug.Log($"¡{gameObject.name} (Presa) ha sido devorado!");
        if (LeaderAI != null)
        {
            LeaderAI.packFollowers.Remove(this);
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
        if (isPlayerDead) return;
        isPlayerDead = true;

        string killer = IsPrey ? "la manada de dinosaurios" : gameObject.name;
        Debug.Log($"¡{killer} atrapó y mató al jugador! Fin de la partida.");
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

        float effectiveRange = visionRange;

        // Modificadores según el estado de sigilo del jugador
        FirstPersonMovement playerMovement = Player.GetComponent<FirstPersonMovement>();
        if (playerMovement == null) playerMovement = Player.GetComponentInParent<FirstPersonMovement>();
        if (playerMovement == null) playerMovement = Player.GetComponentInChildren<FirstPersonMovement>();

        if (playerMovement != null)
        {
            if (playerMovement.IsCrouched)
            {
                effectiveRange *= playerMovement.stealthDetectionMultiplier; // Sigilo (Shift): 50% de rango
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
            if (angle > visionAngle * 0.5f) return false;
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
            if (hit.collider.CompareTag("Enemy")) continue;

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
        if (!isPlayerDead) return;

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

        string subText = IsPrey ? "Has sido alcanzado y devorado por la manada de dinosaurios." : "Has sido alcanzado y devorado por el dinosaurio.";

        GUI.Label(new Rect(centerX - 350, centerY - 90, 700, 70), "¡HAS MUERTO!", titleStyle);
        GUI.Label(new Rect(centerX - 350, centerY - 15, 700, 40), subText, subStyle);
        GUI.Label(new Rect(centerX - 350, centerY + 45, 700, 40), "Presiona [ R ] para volver a intentarlo", hintStyle);
    }

    /// <summary>
    /// Gizmos para depuración del cono de visión de 7.5 metros en la vista de escena
    /// </summary>
    private void OnDrawGizmos()
    {
        bool isChasing = (StateMachine != null && StateMachine.CurrentState == ChaseState);
        bool isFleeing = (StateMachine != null && StateMachine.CurrentState == FleeState);

        if (isChasing) Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f); // Rojo
        else if (isFleeing) Gizmos.color = new Color(0.15f, 0.55f, 1f, 0.9f); // Azul
        else Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.7f); // Amarillo normal

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        float halfAngle = visionAngle * 0.5f;
        Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * transform.forward;

        float range = visionRange > 0 ? visionRange : 7.5f;
        Gizmos.DrawRay(eyePos, transform.forward * range);
        Gizmos.DrawRay(eyePos, leftDir * range);
        Gizmos.DrawRay(eyePos, rightDir * range);
        Gizmos.DrawLine(eyePos + leftDir * range, eyePos + transform.forward * range);
        Gizmos.DrawLine(eyePos + rightDir * range, eyePos + transform.forward * range);

        if (isLeader)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.35f);
        }
    }
}
