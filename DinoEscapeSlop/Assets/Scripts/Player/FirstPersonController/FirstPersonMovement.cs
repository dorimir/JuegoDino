using System.Collections.Generic;
using UnityEngine;

public class FirstPersonMovement : MonoBehaviour
{
    [Header("Velocidad de Movimiento")]
    [Tooltip("Velocidad normal de movimiento (caminar/strafe con WASD).")]
    public float speed = 5.5f;

    [Tooltip("Velocidad al esprintar (activada con doble toque en W).")]
    public float runSpeed = 9f;

    [Tooltip("Velocidad en modo sigilo / agachado (manteniendo Shift).")]
    public float crouchSpeed = 2.6f;

    [Header("Respuesta y Suavizado WASD")]
    [Tooltip("Aceleración al empezar a moverse o acelerar.")]
    public float acceleration = 35f;

    [Tooltip("Frenado suave al soltar las teclas WASD.")]
    public float deceleration = 30f;

    [Tooltip("Multiplicador de respuesta al cambiar rápidamente de dirección (ej: de A a D).")]
    public float directionChangeMultiplier = 2.0f;

    [Header("Correr (Doble toque en W)")]
    public bool canRun = true;
    [Tooltip("Tiempo máximo en segundos entre dos pulsaciones de W para activar correr.")]
    public float doubleTapThreshold = 0.28f;
    public bool IsRunning { get; private set; }

    [Header("Agacharse / Sigilo (Shift)")]
    public bool canCrouch = true;
    public KeyCode crouchKey = KeyCode.LeftShift;
    public KeyCode secondaryCrouchKey = KeyCode.RightShift;
    public bool IsCrouched { get; private set; }

    [Tooltip("Velocidad de transición suave de cámara y collider.")]
    public float crouchTransitionSpeed = 10f;

    [Tooltip("Transform de la cabeza/cámara para agacharse.")]
    public Transform headToLower;

    [Tooltip("Multiplicador de altura de la cabeza al agacharse.")]
    [Range(0.2f, 0.9f)]
    public float crouchHeadRatio = 0.55f;

    [Tooltip("Collider a reducir al agacharse.")]
    public CapsuleCollider colliderToLower;

    [Tooltip("Multiplicador de altura del collider al agacharse.")]
    [Range(0.2f, 0.9f)]
    public float crouchColliderRatio = 0.55f;

    [Header("Sigilo")]
    [Tooltip("Multiplicador del rango de detección de los dinosaurios al ir en sigilo.")]
    [Range(0.1f, 1f)]
    public float stealthDetectionMultiplier = 0.5f;

    [Header("Salto (Espacio)")]
    public bool canJump = true;
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpForce = 5.5f;

    [Tooltip("Referencia al GroundCheck (si existe en los hijos).")]
    public GroundCheck groundCheck;

    // Eventos
    public event System.Action Jumped;
    public event System.Action CrouchStart;
    public event System.Action CrouchEnd;

    // Compatibilidad hacia atrás
    new Rigidbody rigidbody;
    public KeyCode runningKey = KeyCode.None;

    /// <summary> Funciones para sobreescribir la velocidad de movimiento. </summary>
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

    // Variables internas
    private Vector2 inputVector = Vector2.zero;
    private Vector2 currentVelocity2D = Vector2.zero;
    private float lastWPressTime = -10f;
    private float defaultHeadY = 1.6f;
    private float defaultColliderHeight = 1.8f;
    private Vector3 defaultColliderCenter = Vector3.up * 0.9f;
    private bool hasHeadDefault = false;
    private bool hasColliderDefault = false;
    private bool hasSeparateCrouchComponent = false;
    private bool hasSeparateJumpComponent = false;

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();

        // Autodetectar cámara si no está asignada
        if (!headToLower)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) headToLower = cam.transform;
        }

        // Autodetectar collider si no está asignado
        if (!colliderToLower)
        {
            colliderToLower = GetComponent<CapsuleCollider>();
        }

        // Autodetectar groundCheck si no está asignado
        if (!groundCheck)
        {
            groundCheck = GetComponentInChildren<GroundCheck>();
        }

        // Guardar valores por defecto para agacharse suavemente
        if (headToLower != null)
        {
            defaultHeadY = headToLower.localPosition.y;
            hasHeadDefault = true;
        }

        if (colliderToLower != null)
        {
            defaultColliderHeight = colliderToLower.height;
            defaultColliderCenter = colliderToLower.center;
            hasColliderDefault = true;
        }

        hasSeparateCrouchComponent = GetComponent<Crouch>() != null;
        hasSeparateJumpComponent = GetComponent<Jump>() != null;
    }

    void Update()
    {
        // 1. Muestrear entradas WASD en Update para respuesta instantánea sin pérdida de frames
        SampleWASDInput();

        // 2. Lógica de correr por doble toque en W
        HandleDoubleTapRun();

        // 3. Lógica de agacharse con Shift
        HandleCrouch();

        // 4. Lógica de salto con Espacio
        HandleJump();

        // 5. Animación suave de cámara y collider
        UpdateCrouchVisuals();
    }

    void FixedUpdate()
    {
        // 1. Determinar velocidad objetivo según el estado
        float targetSpeed = speed;

        if (IsCrouched)
        {
            targetSpeed = crouchSpeed;
        }
        else if (IsRunning)
        {
            targetSpeed = runSpeed;
        }

        if (speedOverrides.Count > 0)
        {
            targetSpeed = speedOverrides[speedOverrides.Count - 1]();
        }

        Vector2 targetVelocity = inputVector * targetSpeed;

        // 2. Aceleración con respuesta ágil a cambios de dirección (ej: de A a D)
        float currentRate;
        if (targetVelocity.sqrMagnitude > 0.001f)
        {
            bool isReversing = Vector2.Dot(currentVelocity2D, targetVelocity) < 0f;
            currentRate = isReversing ? (acceleration * directionChangeMultiplier) : acceleration;
        }
        else
        {
            currentRate = deceleration;
        }

        currentVelocity2D = Vector2.MoveTowards(currentVelocity2D, targetVelocity, currentRate * Time.fixedDeltaTime);

        // 3. Desplazamiento libre relativo a la orientación del personaje (A/D = lateral, W/S = frontal)
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 worldMove = (right * currentVelocity2D.x) + (forward * currentVelocity2D.y);

        // 4. Aplicar al Rigidbody manteniendo la velocidad vertical (gravedad e impulsos de salto)
        rigidbody.linearVelocity = new Vector3(worldMove.x, rigidbody.linearVelocity.y, worldMove.z);
    }

    private void SampleWASDInput()
    {
        // Detección directa de teclas WASD y flechas (garantiza respuesta 100% inmediata)
        float h = 0f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;

        float v = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

        // Fallback a ejes virtuales si no se presionaron teclas directas (joysticks/gamepads)
        if (h == 0f) h = Input.GetAxisRaw("Horizontal");
        if (v == 0f) v = Input.GetAxisRaw("Vertical");

        inputVector = new Vector2(h, v);

        // Normalizar entrada diagonal para evitar velocidad extra al presionar W+A o W+D
        if (inputVector.sqrMagnitude > 1f)
        {
            inputVector.Normalize();
        }
    }

    private void HandleDoubleTapRun()
    {
        if (!canRun || IsCrouched)
        {
            IsRunning = false;
            return;
        }

        // Doble toque rápido en W para empezar a correr
        if (Input.GetKeyDown(KeyCode.W))
        {
            float timeSinceLastW = Time.time - lastWPressTime;
            if (timeSinceLastW <= doubleTapThreshold && timeSinceLastW > 0.03f)
            {
                IsRunning = true;
            }
            lastWPressTime = Time.time;
        }

        // Si suelta W, deja de correr (pero puede seguir desplazándose con A, S, D a velocidad normal)
        if (IsRunning && !Input.GetKey(KeyCode.W))
        {
            IsRunning = false;
        }
    }

    private void HandleCrouch()
    {
        if (!canCrouch)
        {
            if (IsCrouched)
            {
                IsCrouched = false;
                CrouchEnd?.Invoke();
            }
            return;
        }

        if (hasSeparateCrouchComponent)
        {
            Crouch c = GetComponent<Crouch>();
            if (c != null)
            {
                IsCrouched = c.IsCrouched;
                return;
            }
        }

        // Mantener Shift para agacharse y modo sigilo
        bool isHoldingShift = Input.GetKey(crouchKey) || Input.GetKey(secondaryCrouchKey);

        if (isHoldingShift != IsCrouched)
        {
            IsCrouched = isHoldingShift;
            if (IsCrouched)
            {
                IsRunning = false; // Agacharse interrumpe el sprint
                CrouchStart?.Invoke();
            }
            else
            {
                CrouchEnd?.Invoke();
            }
        }
    }

    private void HandleJump()
    {
        if (!canJump || IsCrouched || hasSeparateJumpComponent)
        {
            return;
        }

        // Saltar con ESPACIO
        if (Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump"))
        {
            if (IsGrounded())
            {
                rigidbody.linearVelocity = new Vector3(rigidbody.linearVelocity.x, jumpForce, rigidbody.linearVelocity.z);
                Jumped?.Invoke();
            }
        }
    }

    private void UpdateCrouchVisuals()
    {
        if (hasSeparateCrouchComponent) return;

        // Transición suave de cámara
        if (headToLower != null && hasHeadDefault)
        {
            float targetHeadY = IsCrouched ? (defaultHeadY * crouchHeadRatio) : defaultHeadY;
            float newHeadY = Mathf.Lerp(headToLower.localPosition.y, targetHeadY, Time.deltaTime * crouchTransitionSpeed);
            headToLower.localPosition = new Vector3(headToLower.localPosition.x, newHeadY, headToLower.localPosition.z);
        }

        // Transición suave de collider
        if (colliderToLower != null && hasColliderDefault)
        {
            float targetHeight = IsCrouched ? (defaultColliderHeight * crouchColliderRatio) : defaultColliderHeight;
            float newHeight = Mathf.Lerp(colliderToLower.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            colliderToLower.height = newHeight;
            colliderToLower.center = new Vector3(defaultColliderCenter.x, newHeight * 0.5f, defaultColliderCenter.z);
        }
    }

    public bool IsGrounded()
    {
        if (groundCheck != null)
        {
            return groundCheck.isGrounded;
        }

        float checkDistance = 1.05f;
        if (colliderToLower != null)
        {
            checkDistance = (colliderToLower.height * 0.5f) + 0.15f;
        }

        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, checkDistance);
    }
}