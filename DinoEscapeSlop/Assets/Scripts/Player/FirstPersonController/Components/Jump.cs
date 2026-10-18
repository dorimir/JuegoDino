using UnityEngine;

public class Jump : MonoBehaviour
{
    new Rigidbody rigidbody;
    public float jumpStrength = 5.2f;
    public KeyCode jumpKey = KeyCode.Space;
    public event System.Action Jumped;

    [SerializeField, Tooltip("Prevents jumping when the transform is in mid-air.")]
    GroundCheck groundCheck;

    void Reset()
    {
        groundCheck = GetComponentInChildren<GroundCheck>();
    }

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (!groundCheck) groundCheck = GetComponentInChildren<GroundCheck>();
    }

    void Update()
    {
        // Jump when the Space key / Jump button is pressed and we are on the ground.
        if ((Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump")) && (!groundCheck || groundCheck.isGrounded))
        {
            rigidbody.linearVelocity = new Vector3(rigidbody.linearVelocity.x, jumpStrength, rigidbody.linearVelocity.z);
            Jumped?.Invoke();
        }
    }
}
