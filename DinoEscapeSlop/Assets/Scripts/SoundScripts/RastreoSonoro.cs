
using UnityEngine;
using System.Collections.Generic;

public class RastreoSonoro : MonoBehaviour
{
    public static readonly List<RastreoSonoro> Activos =
        new List<RastreoSonoro>();

    [Header("Sonido")]
    public AudioSource sonido;
    public float radioSonoro = 15f;
    public bool Recogido;

    [Header("Lanzamiento")]
    public float tiempoMaxVuelo = 6f;
    public float graciaInicial = 0.3f;
    public float velocidadParada = 0.5f;

    private Rigidbody rbObjeto;
    private bool enVuelo;
    private float inicioVuelo;

    private void Awake()
    {
        rbObjeto = GetComponent<Rigidbody>();

        if (sonido != null)
        {
            sonido.loop = true;
            sonido.Stop();
        }
    }

    private void OnEnable()
    {
        if (!Activos.Contains(this))
            Activos.Add(this);
    }

    private void OnDisable()
    {
        Activos.Remove(this);
    }

    private void Update()
    {
        if (enVuelo)
        {
            if (Time.time - inicioVuelo >= tiempoMaxVuelo)
            {
                Soltar();
            }
            else if (Time.time - inicioVuelo >= graciaInicial &&
                     rbObjeto != null &&
                     rbObjeto.linearVelocity.magnitude < velocidadParada)
            {
                Soltar();
            }
        }
    }

    public void Recoger()
    {
        enVuelo = false;
        ActivarSonido(true);
    }

    public void Soltar()
    {
        enVuelo = false;
        ActivarSonido(false);
    }

    public void Lanzar()
    {
        inicioVuelo = Time.time;
        enVuelo = true;
        ActivarSonido(true);
    }

    private void ActivarSonido(bool activo)
    {
        Recogido = activo;

        if (sonido == null) return;

        if (activo && !sonido.isPlaying)
            sonido.Play();
        else if (!activo && sonido.isPlaying)
            sonido.Stop();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!enVuelo) return;
        if (Time.time - inicioVuelo < graciaInicial) return;

        if (collision.collider.CompareTag("Player") ||
            collision.transform.root.CompareTag("Player"))
            return;

        Soltar();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radioSonoro);
    }
}
