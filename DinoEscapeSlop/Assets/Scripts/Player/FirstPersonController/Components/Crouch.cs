using UnityEngine;

public class Crouch : MonoBehaviour
{
    public KeyCode key = KeyCode.LeftShift;

    [Header("Movimiento")]

    public FirstPersonMovement movement;

    public float movementSpeed = 2.4f;

    [Header("Ajuste de Altura")]

    public Transform headToLower;
    [HideInInspector]
    public float? defaultHeadYLocalPosition;
    public float crouchYHeadPosition = 0.9f;

    public CapsuleCollider colliderToLower;
    [HideInInspector]
    public float? defaultColliderHeight;

    [Header("Suavizado")]
    public float transitionSpeed = 10f;

    public bool IsCrouched { get; private set; }
    public event System.Action CrouchStart, CrouchEnd;

    private void Reset()
    {
        movement = GetComponentInParent<FirstPersonMovement>();
        headToLower = movement != null ? movement.GetComponentInChildren<Camera>()?.transform : null;
        colliderToLower = movement != null ? movement.GetComponentInChildren<CapsuleCollider>() : null;
    }

    private void Start()
    {
        if (movement == null) movement = GetComponentInParent<FirstPersonMovement>();
        if (headToLower == null && movement != null) headToLower = movement.GetComponentInChildren<Camera>()?.transform;
        if (colliderToLower == null && movement != null) colliderToLower = movement.GetComponentInChildren<CapsuleCollider>();

        if (headToLower != null && !defaultHeadYLocalPosition.HasValue)
        {
            defaultHeadYLocalPosition = headToLower.localPosition.y;
        }

        if (colliderToLower != null && !defaultColliderHeight.HasValue)
        {
            defaultColliderHeight = colliderToLower.height;
        }
    }

    private void LateUpdate()
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
        else if (IsCrouched)
        {
            IsCrouched = false;
            SetSpeedOverrideActive(false);
            CrouchEnd?.Invoke();
        }

        float targetHeadY = IsCrouched ? crouchYHeadPosition : (defaultHeadYLocalPosition ?? 1.6f);
        if (headToLower != null)
        {
            float newY = Mathf.Lerp(headToLower.localPosition.y, targetHeadY, Time.deltaTime * transitionSpeed);
            headToLower.localPosition = new Vector3(headToLower.localPosition.x, newY, headToLower.localPosition.z);
        }

        float defaultHeight = defaultColliderHeight ?? 1.8f;
        float targetColHeight = IsCrouched ? (defaultHeight * 0.55f) : defaultHeight;
        if (colliderToLower != null)
        {
            float newColHeight = Mathf.Lerp(colliderToLower.height, targetColHeight, Time.deltaTime * transitionSpeed);
            colliderToLower.height = newColHeight;
            colliderToLower.center = Vector3.up * (newColHeight * 0.5f);
        }
    }

    private void SetSpeedOverrideActive(bool state)
    {
        if (movement == null) return;

        if (state)
        {
            if (!movement.speedOverrides.Contains(SpeedOverride))
            {
                movement.speedOverrides.Add(SpeedOverride);
            }
        }
        else
        {
            movement.speedOverrides.Remove(SpeedOverride);
        }
    }

    private float SpeedOverride() => movementSpeed;
}
