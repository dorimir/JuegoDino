using UnityEngine;

public class Crouch : MonoBehaviour
{
    public KeyCode key = KeyCode.LeftShift;

    [Header("Slow Movement")]
    [Tooltip("Movement to slow down when crouched.")]
    public FirstPersonMovement movement;
    [Tooltip("Movement speed when crouched.")]
    public float movementSpeed = 2.4f;

    [Header("Low Head")]
    [Tooltip("Head to lower when crouched.")]
    public Transform headToLower;
    [HideInInspector]
    public float? defaultHeadYLocalPosition;
    public float crouchYHeadPosition = 0.9f;

    [Tooltip("Collider to lower when crouched.")]
    public CapsuleCollider colliderToLower;
    [HideInInspector]
    public float? defaultColliderHeight;

    [Header("Smoothing")]
    public float transitionSpeed = 10f;

    public bool IsCrouched { get; private set; }
    public event System.Action CrouchStart, CrouchEnd;

    void Reset()
    {
        movement = GetComponentInParent<FirstPersonMovement>();
        headToLower = movement != null ? movement.GetComponentInChildren<Camera>()?.transform : null;
        colliderToLower = movement != null ? movement.GetComponentInChildren<CapsuleCollider>() : null;
    }

    void Start()
    {
        if (!movement) movement = GetComponentInParent<FirstPersonMovement>();
        if (!headToLower && movement) headToLower = movement.GetComponentInChildren<Camera>()?.transform;
        if (!colliderToLower && movement) colliderToLower = movement.GetComponentInChildren<CapsuleCollider>();

        if (headToLower && !defaultHeadYLocalPosition.HasValue)
        {
            defaultHeadYLocalPosition = headToLower.localPosition.y;
        }

        if (colliderToLower && !defaultColliderHeight.HasValue)
        {
            defaultColliderHeight = colliderToLower.height;
        }
    }

    void LateUpdate()
    {
        bool shiftPressed = Input.GetKey(key) || Input.GetKey(KeyCode.RightShift);

        if (shiftPressed)
        {
            if (!IsCrouched)
            {
                IsCrouched = true;
                SetSpeedOverrideActive(true);
                CrouchStart?.Invoke();
            }
        }
        else
        {
            if (IsCrouched)
            {
                IsCrouched = false;
                SetSpeedOverrideActive(false);
                CrouchEnd?.Invoke();
            }
        }

        // Transición suave
        float targetHeadY = IsCrouched ? crouchYHeadPosition : (defaultHeadYLocalPosition ?? 1.6f);
        if (headToLower)
        {
            float newY = Mathf.Lerp(headToLower.localPosition.y, targetHeadY, Time.deltaTime * transitionSpeed);
            headToLower.localPosition = new Vector3(headToLower.localPosition.x, newY, headToLower.localPosition.z);
        }

        float defaultHeight = defaultColliderHeight ?? 1.8f;
        float targetColHeight = IsCrouched ? (defaultHeight * 0.55f) : defaultHeight;
        if (colliderToLower)
        {
            float newColHeight = Mathf.Lerp(colliderToLower.height, targetColHeight, Time.deltaTime * transitionSpeed);
            colliderToLower.height = newColHeight;
            colliderToLower.center = Vector3.up * newColHeight * 0.5f;
        }
    }

    #region Speed override.
    void SetSpeedOverrideActive(bool state)
    {
        if (!movement) return;

        if (state)
        {
            if (!movement.speedOverrides.Contains(SpeedOverride))
            {
                movement.speedOverrides.Add(SpeedOverride);
            }
        }
        else
        {
            if (movement.speedOverrides.Contains(SpeedOverride))
            {
                movement.speedOverrides.Remove(SpeedOverride);
            }
        }
    }

    float SpeedOverride() => movementSpeed;
    #endregion
}
