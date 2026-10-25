using System.Collections.Generic;
using UnityEngine;

public class FirstPersonMovement : MonoBehaviour
{
    [Header("Velocidad de Movimiento")]

    public float speed = 8.5f;

    public float runSpeed = 13.5f;

    public float crouchSpeed = 4.2f;

    [Header("Respuesta y Suavizado WASD")]

    public float acceleration = 50f;

    public float deceleration = 45f;

    public float directionChangeMultiplier = 2.0f;

    [Header("Correr (Doble toque en W)")]
    public bool canRun = true;

    public float doubleTapThreshold = 0.28f;
    public bool IsRunning { get; private set; }

    [Header("Agacharse / Sigilo (Shift)")]
    public bool canCrouch = true;
    public KeyCode crouchKey = KeyCode.LeftShift;
    public KeyCode secondaryCrouchKey = KeyCode.RightShift;
    public bool IsCrouched { get; private set; }

    public float crouchTransitionSpeed = 10f;

    public Transform headToLower;

    [Range(0.2f, 0.9f)]
    public float crouchHeadRatio = 0.55f;

    public CapsuleCollider colliderToLower;

    [Range(0.2f, 0.9f)]
    public float crouchColliderRatio = 0.55f;

    [Header("Sigilo")]

    [Range(0.1f, 1f)]
    public float stealthDetectionMultiplier = 0.5f;

    [Header("Salto (Espacio)")]
    public bool canJump = true;
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpForce = 5.5f;

    public GroundCheck groundCheck;

    public event System.Action Jumped;
    public event System.Action CrouchStart;
    public event System.Action CrouchEnd;

    new Rigidbody rigidbody;
    public KeyCode runningKey = KeyCode.None;

    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

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

        if (!headToLower)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) headToLower = cam.transform;
        }

        if (!colliderToLower)
        {
            colliderToLower = GetComponent<CapsuleCollider>();
        }

        if (!groundCheck)
        {
            groundCheck = GetComponentInChildren<GroundCheck>();
        }

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
        if (EnemyAI.GlobalPlayerDead) return;

        SampleWASDInput();

        HandleDoubleTapRun();

        HandleCrouch();

        HandleJump();

        UpdateCrouchVisuals();
    }

    void FixedUpdate()
    {
        if (EnemyAI.GlobalPlayerDead) return;

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

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 worldMove = (right * currentVelocity2D.x) + (forward * currentVelocity2D.y);

        rigidbody.linearVelocity = new Vector3(worldMove.x, rigidbody.linearVelocity.y, worldMove.z);
    }

    private void SampleWASDInput()
    {

        float h = 0f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;

        float v = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

        if (h == 0f) h = Input.GetAxisRaw("Horizontal");
        if (v == 0f) v = Input.GetAxisRaw("Vertical");

        inputVector = new Vector2(h, v);

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

        if (Input.GetKeyDown(KeyCode.W))
        {
            float timeSinceLastW = Time.time - lastWPressTime;
            if (timeSinceLastW <= doubleTapThreshold && timeSinceLastW > 0.03f)
            {
                IsRunning = true;
            }
            lastWPressTime = Time.time;
        }

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

        bool isHoldingShift = Input.GetKey(crouchKey) || Input.GetKey(secondaryCrouchKey);

        if (isHoldingShift != IsCrouched)
        {
            IsCrouched = isHoldingShift;
            if (IsCrouched)
            {
                IsRunning = false;
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

        if (headToLower != null && hasHeadDefault)
        {
            float targetHeadY = IsCrouched ? (defaultHeadY * crouchHeadRatio) : defaultHeadY;
            float newHeadY = Mathf.Lerp(headToLower.localPosition.y, targetHeadY, Time.deltaTime * crouchTransitionSpeed);
            headToLower.localPosition = new Vector3(headToLower.localPosition.x, newHeadY, headToLower.localPosition.z);
        }

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

        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, checkDistance, ~0, QueryTriggerInteraction.Ignore);
    }
}
