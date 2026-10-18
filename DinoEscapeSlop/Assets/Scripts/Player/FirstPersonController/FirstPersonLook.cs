using UnityEngine;

public class FirstPersonLook : MonoBehaviour
{
    [SerializeField]
    Transform character;
    public float sensitivity = 2;
    public float smoothing = 1.5f;

    Vector2 velocity;
    Vector2 frameVelocity;

    void Reset()
    {
        // Get the character from the FirstPersonMovement in parents.
        FirstPersonMovement fpm = GetComponentInParent<FirstPersonMovement>();
        if (fpm) character = fpm.transform;
    }

    void Start()
    {
        // Asegurar que character esté asignado incluso en tiempo de ejecución
        if (!character)
        {
            FirstPersonMovement fpm = GetComponentInParent<FirstPersonMovement>();
            if (fpm) character = fpm.transform;
        }

        // Lock the mouse cursor to the game screen.
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Get smooth velocity.
        Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        Vector2 rawFrameVelocity = Vector2.Scale(mouseDelta, Vector2.one * sensitivity);
        frameVelocity = Vector2.Lerp(frameVelocity, rawFrameVelocity, 1 / smoothing);
        velocity += frameVelocity;
        velocity.y = Mathf.Clamp(velocity.y, -90, 90);

        // Rotate camera up-down and controller left-right from velocity.
        transform.localRotation = Quaternion.AngleAxis(-velocity.y, Vector3.right);
        if (character)
        {
            character.localRotation = Quaternion.AngleAxis(velocity.x, Vector3.up);
        }
    }
}
