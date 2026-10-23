using UnityEngine;

[RequireComponent(typeof(FirstPersonMovement))]
public class RuidoJugador : MonoBehaviour
{
    public static RuidoJugador Instancia { get; private set; }

    [Header("Radios de ruido (metros)")]
    public float radioCaminar = 6f;
    public float radioCorrer = 10f;
    public float radioSalto = 8f;

    [Header("Detección")]
    [Tooltip("Velocidad horizontal mínima para considerar que camina.")]
    public float velocidadMinima = 0.3f;

    public bool Sonando { get; private set; }
    public float RadioActual { get; private set; }

    private FirstPersonMovement mov;
    private Rigidbody rb;
    private bool anterior;

    private void Awake()
    {
        Instancia = this;
        mov = GetComponent<FirstPersonMovement>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private void Update()
    {
        Sonando = false;
        RadioActual = 0f;

        // En sigilo (Shift) no hace ruido
        if (mov.IsCrouched) { Log(); return; }

        Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
        float velHoriz = new Vector2(v.x, v.z).magnitude;

        bool caminando = velHoriz > velocidadMinima;
        bool enAire = !mov.IsGrounded();

        if (enAire)
        {
            Sonando = true;
            RadioActual = radioSalto;
        }
        else if (caminando)
        {
            Sonando = true;
            RadioActual = mov.IsRunning ? radioCorrer : radioCaminar;
        }

        Log();
    }

    private void Log()
    {
        if (Sonando == anterior) return;
        anterior = Sonando;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Sonando) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, RadioActual);
    }
}