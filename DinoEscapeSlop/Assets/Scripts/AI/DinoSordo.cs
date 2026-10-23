using UnityEngine;

public class DinoSordo : MonoBehaviour
{
    [Header("Detección")]
    public Collider colliderOido;
    public float cooldown = 3f;

    private EnemyAI ai;
    private float proximoPermitido;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        if (ai == null) ai = GetComponentInParent<EnemyAI>();

        if (colliderOido == null)
            colliderOido = GetComponentInChildren<Collider>();

        Debug.Log("[SORDO] " + name +
                  " | EnemyAI: " + (ai != null) +
                  " | Collider: " + (colliderOido != null));
    }

    private void Update()
    {
        if (ai == null || colliderOido == null || EnemyAI.GlobalPlayerDead)
            return;
        if (ai.StateMachine == null || ai.ChaseState == null)
            return;

        if (OyeObjeto() || OyePasos())
            Reaccionar();
    }

    // Ruido del objeto sonoro (solo si está en la mano o en vuelo)
    private bool OyeObjeto()
    {
        foreach (RastreoSonoro fuente in RastreoSonoro.Activos)
        {
            if (fuente == null || !fuente.Recogido) continue;

            if (Distancia(fuente.transform.position) <= fuente.radioSonoro)
                return true;
        }
        return false;
    }

    // Ruido de los pasos / saltos del jugador (no suena en sigilo ni quieto)
    private bool OyePasos()
    {
        RuidoJugador pasos = RuidoJugador.Instancia;
        if (pasos == null || !pasos.Sonando) return false;

        return Distancia(pasos.transform.position) <= pasos.RadioActual;
    }

    private float Distancia(Vector3 pos)
    {
        Vector3 cercano = colliderOido.bounds.ClosestPoint(pos);
        return Vector3.Distance(cercano, pos);
    }

    private void Reaccionar()
    {
        if (ai.StateMachine.CurrentState == ai.ChaseState) return;
        if (Time.time < proximoPermitido) return;

        ai.EnsurePlayerReference();
        if (ai.Player == null) return;

        proximoPermitido = Time.time + cooldown;
        Debug.Log(name + " oye un ruido -> CHASE");
        ai.TriggerAttack(ai.Player);
    }
}