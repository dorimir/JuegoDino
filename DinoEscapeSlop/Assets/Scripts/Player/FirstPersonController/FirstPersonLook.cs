using UnityEngine;

public class FirstPersonLook : MonoBehaviour
{
    [SerializeField] private Transform character;
    public float sensitivity = 2f;
    public float smoothing = 1.5f;

    public static bool IsCameraLocked { get; set; }

    private Vector2 velocity;
    private Vector2 frameVelocity;

    private void Reset()
    {
        FirstPersonMovement fpm = GetComponentInParent<FirstPersonMovement>();
        if (fpm != null)
        {
            character = fpm.transform;
        }
    }

    private void Start()
    {
        IsCameraLocked = false;

        if (character == null)
        {
            FirstPersonMovement fpm = GetComponentInParent<FirstPersonMovement>();
            if (fpm != null)
            {
                character = fpm.transform;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (EnemyAI.GlobalPlayerDead || IsCameraLocked)
        {
            frameVelocity = Vector2.zero;
            return;
        }

        Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        Vector2 rawFrameVelocity = Vector2.Scale(mouseDelta, Vector2.one * sensitivity);
        frameVelocity = Vector2.Lerp(frameVelocity, rawFrameVelocity, 1f / smoothing);
        velocity += frameVelocity;
        velocity.y = Mathf.Clamp(velocity.y, -90f, 90f);

        transform.localRotation = Quaternion.AngleAxis(-velocity.y, Vector3.right);
        if (character != null)
        {
            character.localRotation = Quaternion.AngleAxis(velocity.x, Vector3.up);
        }
    }
}
