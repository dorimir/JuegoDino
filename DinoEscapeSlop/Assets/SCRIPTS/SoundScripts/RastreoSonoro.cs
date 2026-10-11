using UnityEngine;

public class RastreoSonoro : MonoBehaviour
{
    [Header("Modo")]
    public bool esConoDeVision = false;

    [Header("Objeto sonoro")]
    public AudioSource sonido;
    public float radioSonoro = 15f;
    public bool Recogido;

    [Header("Lanzamiento")]
    public float tiempoMaxVuelo = 6f;
    public float graciaInicial = 0.3f;
    public float velocidadParada = 0.5f;

    private EnemyAI aiPropia;
    private GameObject zona;
    private SphereCollider zonaCol;
    private Rigidbody rbObjeto;
    private bool estadoAnterior;

    private bool enVuelo;
    private float inicioVuelo;

    private void Awake()
    {
        if (esConoDeVision)
        {
            aiPropia = GetComponentInParent<EnemyAI>();
            Debug.Log($"[CONO] Awake en {name} | EnemyAI: {(aiPropia != null)}");
            return;
        }

        rbObjeto = GetComponent<Rigidbody>();

        zona = new GameObject("ZonaSonora_" + name);
        zona.layer = 0;

        zonaCol = zona.AddComponent<SphereCollider>();
        zonaCol.isTrigger = true;
        zonaCol.radius = radioSonoro;

        Rigidbody rb = zona.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        zona.AddComponent<ZonaSonoraRef>().dueno = this;
        zona.SetActive(false);

        if (sonido != null) { sonido.loop = true; sonido.Stop(); }
        Debug.Log("[SONORO] Awake en " + name + ": zona creada");
    }

    private void OnDestroy()
    {
        if (zona != null) Destroy(zona);
    }

    private void Update()
    {
        if (esConoDeVision || zona == null) return;

        zonaCol.radius = radioSonoro;

        if (enVuelo)
        {
            float t = Time.time - inicioVuelo;

            if (t > tiempoMaxVuelo)
            {
                Debug.Log("[SONORO] Tiempo máximo de vuelo: se apaga");
                Soltar();
            }
            else if (t > graciaInicial && rbObjeto != null &&
                     rbObjeto.linearVelocity.magnitude < velocidadParada)
            {
                Debug.Log("[SONORO] Objeto parado: se apaga");
                Soltar();
            }
        }

        if (Recogido == estadoAnterior) return;
        estadoAnterior = Recogido;

        zona.SetActive(Recogido);

        if (sonido != null)
        {
            if (Recogido) sonido.Play();
            else sonido.Stop();
        }

        Debug.Log(Recogido ? "[SONORO] Esfera ACTIVA" : "[SONORO] Esfera APAGADA");
    }

    private void LateUpdate()
    {
        if (!esConoDeVision && zona != null && Recogido)
            zona.transform.position = transform.position;
    }

    public void Recoger()
    {
        enVuelo = false;
        Recogido = true;
    }

    public void Soltar()
    {
        enVuelo = false;
        Recogido = false;
    }

    public void Lanzar()
    {
        enVuelo = true;
        inicioVuelo = Time.time;
        Recogido = true;
        Debug.Log("[SONORO] Lanzar() recibido: en vuelo");
    }

    private void OnCollisionEnter(Collision col)
    {
        if (esConoDeVision || !enVuelo) return;
        if (Time.time - inicioVuelo < graciaInicial) return;
        if (col.collider.CompareTag("Player") || col.transform.root.CompareTag("Player")) return;

        Debug.Log("[SONORO] Ha tocado " + col.collider.name + ": se apaga");
        Soltar();
    }

    // ---------------- CONO ----------------
    private void OnTriggerEnter(Collider other)
    {
        if (!esConoDeVision || aiPropia == null) return;

        ZonaSonoraRef zs = other.GetComponent<ZonaSonoraRef>();
        if (zs != null) { OirSonido(zs.dueno); return; }

        if (EsJugador(other))
            aiPropia.PlayerInCone = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!esConoDeVision || aiPropia == null) return;

        ZonaSonoraRef zs = other.GetComponent<ZonaSonoraRef>();
        if (zs != null) OirSonido(zs.dueno);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!esConoDeVision || aiPropia == null) return;

        if (other.GetComponent<ZonaSonoraRef>() == null && EsJugador(other))
            aiPropia.PlayerInCone = false;
    }

    private void OirSonido(RastreoSonoro fuente)
    {
        if (fuente == null || !fuente.Recogido) return;
        if (aiPropia.IsPrey || aiPropia.IsChasing) return;

        Debug.Log($"{aiPropia.name} oye el objeto -> CHASE");
        aiPropia.StateMachine.ChangeState(aiPropia.ChaseState);
    }

    private bool EsJugador(Collider other)
    {
        return other.CompareTag("Player") || other.transform.root.CompareTag("Player");
    }

    private void OnDrawGizmosSelected()
    {
        if (esConoDeVision) return;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radioSonoro);
    }
}