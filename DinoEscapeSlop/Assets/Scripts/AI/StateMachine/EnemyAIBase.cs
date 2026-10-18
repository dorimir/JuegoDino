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

    [Header("Configuración de Flee")]
    public bool IsPrey = false;        // true = huye del jugador, false = lo persigue
    public float SafeRange = 22f;      // distancia a la que deja de huir

    public FleeState FleeState { get; private set; }

    [Header("Configuración de Visión y Persecución")]
    [Tooltip("Distancia máxima de visión del enemigo (2.5x más largo: 7.5 metros).")]
    public float visionRange = 7.5f;

    [Tooltip("Campo de visión horizontal total en grados.")]
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

    [Tooltip("Velocidad rápida durante la persecución al jugador (notablemente más rápida).")]
    public float chaseSpeed = 7.8f;

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

    private void Awake()
    {
        // En esta fase de prueba de enemigo base, solo opera el líder activo
        if (!isLeader)
        {
            gameObject.SetActive(false);
            return;
        }

        Movement = GetComponent<EnemyMovement>();
        Pathfinding = GetComponent<AStarPathfinding>();
        if (Pathfinding == null) Pathfinding = Object.FindAnyObjectByType<AStarPathfinding>();

        // Cono de visión 2.5 veces más largo (7.5 metros)
        visionRange = 7.5f;
        DetectionRange = 7.5f;

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

        if (Movement != null)
        {
            Movement.speed = patrolSpeed;
        }

        // Color inicial normal
        SetChasingVisuals(false);

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

    /// <summary>
    /// Cambia el aspecto del dinosaurio a color rojo intenso durante el estado de persecución,
    /// y lo restaura a su textura original al volver a patrulla.
    /// </summary>
    public void SetChasingVisuals(bool isChasing)
    {
        if (dinoRenderers == null || dinoRenderers.Length == 0)
        {
            dinoRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (propBlock == null)
        {
            propBlock = new MaterialPropertyBlock();
        }

        Color targetColor = isChasing ? new Color(1f, 0.12f, 0.12f, 1f) : Color.white;
        Color targetEmission = isChasing ? new Color(0.9f, 0.05f, 0.05f, 1f) : Color.black;

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
                    if (isChasing) rend.material.EnableKeyword("_EMISSION");
                    else rend.material.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    /// <summary>
    /// Mata al jugador: detiene el tiempo de juego, libera el cursor y activa la pantalla de muerte.
    /// </summary>
    public void KillPlayer()
    {
        if (isPlayerDead) return;
        isPlayerDead = true;

        Debug.Log($"¡{gameObject.name} atrapó y mató al jugador! Fin de la partida.");
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Comprueba si el jugador se encuentra dentro del cono de visión de 7.5 metros
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

        // 1. Comprobación de distancia (cono de 7.5 metros)
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

        GUI.Label(new Rect(centerX - 350, centerY - 90, 700, 70), "¡HAS MUERTO!", titleStyle);
        GUI.Label(new Rect(centerX - 350, centerY - 15, 700, 40), "Has sido alcanzado y devorado por el dinosaurio.", subStyle);
        GUI.Label(new Rect(centerX - 350, centerY + 45, 700, 40), "Presiona [ R ] para volver a intentarlo", hintStyle);
    }

    /// <summary>
    /// Gizmos para depuración del cono de visión de 7.5 metros en la vista de escena
    /// </summary>
    private void OnDrawGizmos()
    {
        bool isChasing = (StateMachine != null && StateMachine.CurrentState == ChaseState);
        Gizmos.color = isChasing ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(1f, 0.92f, 0.016f, 0.7f);

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
