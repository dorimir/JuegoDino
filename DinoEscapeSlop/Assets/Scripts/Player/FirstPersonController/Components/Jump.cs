using UnityEngine;

public class Jump : MonoBehaviour
{
    private new Rigidbody rigidbody;
    public float jumpStrength = 5.2f;
    public KeyCode jumpKey = KeyCode.Space;
    public event System.Action Jumped;

    [SerializeField]
    private GroundCheck groundCheck;

    private void Reset()
    {
        groundCheck = GetComponentInChildren<GroundCheck>();
    }

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (groundCheck == null)
        {
            groundCheck = GetComponentInChildren<GroundCheck>();
        }
    }

    private void Update()
    {
        if ((Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump")) && (groundCheck == null || groundCheck.isGrounded))
        {
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = new Vector3(rigidbody.linearVelocity.x, jumpStrength, rigidbody.linearVelocity.z);
                Jumped?.Invoke();
            }
        }
    }
}
