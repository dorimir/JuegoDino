using UnityEngine;

[RequireComponent(typeof(FirstPersonMovement))]
public class RuidoJugador : MonoBehaviour
{
    public static RuidoJugador Instancia { get; private set; }

    [Header("Radios de Ruido (Metros)")]
    public float radioCaminar = 6f;
    public float radioCorrer = 10f;
    public float radioSalto = 8f;

    [Header("Detección")]
    [Tooltip("Velocidad horizontal mínima para considerar que camina.")]
    public float velocidadMinima = 0.3f;

    public bool Sonando { get; private set; }
    public float RadioActual { get; private set; }

    private FirstPersonMovement playerMovement;
    private Rigidbody playerRigidbody;

    private void Awake()
    {
        Instancia = this;
        playerMovement = GetComponent<FirstPersonMovement>();
        playerRigidbody = GetComponent<Rigidbody>();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void Update()
    {
        Sonando = false;
        RadioActual = 0f;

        if (playerMovement == null || playerMovement.IsCrouched) return;

        Vector3 velocity = playerRigidbody != null ? playerRigidbody.linearVelocity : Vector3.zero;
        float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;

        bool isWalking = horizontalSpeed > velocidadMinima;
        bool isInAir = !playerMovement.IsGrounded();

        if (isInAir)
        {
            Sonando = true;
            RadioActual = radioSalto;
        }
        else if (isWalking)
        {
            Sonando = true;
            RadioActual = playerMovement.IsRunning ? radioCorrer : radioCaminar;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Sonando) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, RadioActual);
    }
}
