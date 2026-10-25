using System.Collections.Generic;
using UnityEngine;

public class RastreoSonoro : MonoBehaviour
{
    public static readonly List<RastreoSonoro> Activos = new List<RastreoSonoro>();

    [Header("Sonido")]
    public AudioSource sonido;
    public float radioSonoro = 20f;
    public bool Recogido;

    [Header("Lanzamiento")]
    public float tiempoMaxVuelo = 6f;
    public float graciaInicial = 0.3f;
    public float velocidadParada = 0.5f;

    [Header("Distracción al Aterrizar")]
    public float duracionSonidoAterrizaje = 8f;

    private Rigidbody rbObjeto;
    private bool enVuelo;
    private float inicioVuelo;
    private bool sonandoPorAterrizaje;
    private float tiempoAterrizaje;

    public bool EstaEmiteSonidoActivo => Recogido || enVuelo || sonandoPorAterrizaje || (sonido != null && sonido.isPlaying);

    private void Awake()
    {
        rbObjeto = GetComponent<Rigidbody>();

        if (TryGetComponent<MeshRenderer>(out var mr))
        {
            mr.enabled = false;
        }

        if (sonido != null)
        {
            sonido.loop = true;
            sonido.Stop();
        }
    }

    private void OnEnable()
    {
        if (!Activos.Contains(this))
        {
            Activos.Add(this);
        }
    }

    private void OnDisable()
    {
        Activos.Remove(this);
    }

    private void Update()
    {
        if (enVuelo)
        {
            float flightTime = Time.time - inicioVuelo;
            if (flightTime >= tiempoMaxVuelo)
            {
                Aterrizar();
            }
            else if (flightTime >= graciaInicial && rbObjeto != null && rbObjeto.linearVelocity.magnitude < velocidadParada)
            {
                Aterrizar();
            }
        }
        else if (sonandoPorAterrizaje)
        {
            if (Time.time - tiempoAterrizaje >= duracionSonidoAterrizaje)
            {
                sonandoPorAterrizaje = false;
                ActivarSonido(false);
            }
        }
    }

    public void Recoger()
    {
        enVuelo = false;
        sonandoPorAterrizaje = false;
        ActivarSonido(true);
    }

    public void Soltar()
    {
        enVuelo = false;
        sonandoPorAterrizaje = false;
        ActivarSonido(false);
    }

    public void Lanzar()
    {
        inicioVuelo = Time.time;
        enVuelo = true;
        sonandoPorAterrizaje = false;
        ActivarSonido(true);
    }

    private void Aterrizar()
    {
        enVuelo = false;
        sonandoPorAterrizaje = true;
        tiempoAterrizaje = Time.time;
        ActivarSonido(true);

        NotificarACiegos(transform.position, radioSonoro, 2.2f);
    }

    private void ActivarSonido(bool activo)
    {
        Recogido = activo;
        if (sonido == null) return;

        if (activo && !sonido.isPlaying)
        {
            sonido.Play();
        }
        else if (!activo && sonido.isPlaying)
        {
            sonido.Stop();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!enVuelo || Time.time - inicioVuelo < graciaInicial) return;

        if (collision.collider.CompareTag("Player") || collision.transform.root.CompareTag("Player"))
        {
            return;
        }

        Aterrizar();
    }

    public static bool HaySonidoObjetoActivo(Vector3 listenerPos, float multiplier, out Vector3 soundPos, out float intensity, out Transform soundSource)
    {
        soundPos = Vector3.zero;
        intensity = 0f;
        soundSource = null;

        float bestDist = float.MaxValue;
        RastreoSonoro bestFuente = null;

        for (int i = 0; i < Activos.Count; i++)
        {
            RastreoSonoro fuente = Activos[i];
            if (fuente == null || !fuente.gameObject.activeInHierarchy) continue;
            if (!fuente.EstaEmiteSonidoActivo) continue;

            float dist = Vector3.Distance(listenerPos, fuente.transform.position);
            float effectiveRadius = (fuente.radioSonoro > 0f ? fuente.radioSonoro : 20f) * multiplier;

            if (dist <= effectiveRadius && dist < bestDist)
            {
                bestDist = dist;
                bestFuente = fuente;
            }
        }

        if (bestFuente != null)
        {
            soundPos = bestFuente.transform.position;
            intensity = 2.2f;
            soundSource = bestFuente.transform;
            return true;
        }

        return false;
    }

    public static void NotificarACiegos(Vector3 pos, float radio, float intensidad)
    {
        if (EnemyAI.AllEnemies == null) return;
        for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
        {
            EnemyAI enemy = EnemyAI.AllEnemies[i];
            if (enemy is BlindEnemyAI blind && blind.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(blind.transform.position, pos);
                if (dist <= radio * blind.hearingSensitivityMultiplier)
                {
                    blind.NotificarSonidoPrioritario(pos, intensidad, null);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radioSonoro);
    }
}
