
using UnityEngine;

public class DinoSordo : MonoBehaviour
{
    [Header("Detección")]
    public Collider colliderOido;
    public float cooldown = 3f;

    [Header("Color")]
    public Color colorOido = new Color(0.6f, 0.1f, 0.9f);

    private EnemyAI ai;
    private Renderer[] renderers;
    private float proximoPermitido;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();

        if (ai == null)
            ai = GetComponentInParent<EnemyAI>();

        if (colliderOido == null)
            colliderOido = GetComponentInChildren<Collider>();

        renderers = GetComponentsInChildren<Renderer>(true);

        if (colliderOido != null)
            colliderOido.isTrigger = true;

        Debug.Log("[SORDO] " + name +
                  " | EnemyAI: " + (ai != null) +
                  " | Collider: " + (colliderOido != null));
    }

    private void Update()
    {
        if (ai == null || EnemyAI.GlobalPlayerDead)
            return;

        foreach (RastreoSonoro fuente in RastreoSonoro.Activos)
        {
            if (fuente == null || !fuente.Recogido)
                continue;

            float distancia = Vector3.Distance(
                colliderOido.bounds.ClosestPoint(fuente.transform.position),
                fuente.transform.position
            );

            if (distancia <= fuente.radioSonoro)
            {
                // Simular la detección del objeto.
                DetectarObjeto(fuente.GetComponent<Collider>());
                break;
            }
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        DetectarObjeto(other);
    }

    private void OnTriggerStay(Collider other)
    {
        DetectarObjeto(other);
    }

    
    private void DetectarObjeto(Collider other)
    {
        if (ai == null || colliderOido == null ||
            EnemyAI.GlobalPlayerDead)
            return;

        if (ai.IsPrey || ai.StateMachine == null ||
            ai.ChaseState == null)
            return;

        if (other == null) return;

        RastreoSonoro fuente = other.GetComponent<RastreoSonoro>();

        if (fuente == null)
            fuente = other.GetComponentInParent<RastreoSonoro>();

        if (fuente == null || !fuente.Recogido)
            return;

        Vector3 puntoCercano =
            colliderOido.ClosestPoint(fuente.transform.position);

        float distancia = Vector3.Distance(
            puntoCercano,
            fuente.transform.position
        );

        // Respetar el radio de sonido del objeto.
        if (distancia > fuente.radioSonoro)
            return;

        PintarMorado();

        if (ai.StateMachine.CurrentState == ai.ChaseState)
            return;

        if (Time.time < proximoPermitido)
            return;

        ai.EnsurePlayerReference();

        if (ai.Player == null)
            return;

        proximoPermitido = Time.time + cooldown;
        ai.TriggerAttack(ai.Player);
    }


    private void PintarMorado()
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            Material m = r.material;

            if (m.HasProperty(BaseColorId))
                m.SetColor(BaseColorId, colorOido);

            if (m.HasProperty(ColorId))
                m.SetColor(ColorId, colorOido);
        }
    }
}
