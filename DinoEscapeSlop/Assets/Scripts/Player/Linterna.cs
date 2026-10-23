using System.Collections.Generic;
using UnityEngine;

public class Linterna : MonoBehaviour
{
    public static readonly List<Linterna> Encendidas = new List<Linterna>();

    [Header("Cono de Luz")]
    public float alcance = 12f;
    [Range(5f, 120f)] public float anguloCono = 45f;
    [Tooltip("Punto desde el que sale la luz. Si está vacío usa el propio objeto. Su eje Z azul debe apuntar por donde sale la luz.")]
    public Transform origen;

    [Header("Posición en la mano")]
    [Tooltip("Inclinación extra respecto a mirar recto (X positivo = apunta un poco hacia abajo).")]
    public Vector3 inclinacionEnMano = Vector3.zero;

    [Header("Control")]
    public KeyCode teclaToggle = KeyCode.Mouse0;
    public bool encendida;
    public bool soloSiRecogida = true;

    [Header("Luz visual (Spot Light)")]
    public Light luz;
    public float intensidad = 200f;

    private int holdLayer;
    private readonly Dictionary<EnemyAI, float> lastIlluminatedTimes = new Dictionary<EnemyAI, float>();

    public Transform Origen => origen != null ? origen : transform;

    // Rotación que debe tener el objeto para que su Origen mire hacia delante
    public Quaternion RotacionEnMano(Quaternion holdRot)
    {
        Quaternion relOrigen = Quaternion.Inverse(transform.rotation) * Origen.rotation;
        return holdRot * Quaternion.Euler(inclinacionEnMano) * Quaternion.Inverse(relOrigen);
    }

    private void Awake()
    {
        holdLayer = LayerMask.NameToLayer("holdLayer");
        soloSiRecogida = true;
        teclaToggle = KeyCode.Mouse0;

        if (luz != null)
        {
            luz.transform.SetParent(null);
            luz.gameObject.layer = 0;
            luz.type = LightType.Spot;
            luz.range = alcance;
            luz.spotAngle = anguloCono;
            luz.intensity = intensidad;
            luz.cullingMask = holdLayer >= 0 ? ~(1 << holdLayer) : ~0;
            luz.enabled = false;
        }

        SetModelColorGray();
    }

    private void Start()
    {
        soloSiRecogida = true;
        SetModelColorGray();
    }

    private void SetModelColorGray()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(0.52f, 0.52f, 0.54f, 1f);
        }
    }

    private void OnDestroy()
    {
        if (luz != null) Destroy(luz.gameObject);
    }

    private void OnDisable()
    {
        Encendidas.Remove(this);
    }

    public bool EnLaMano()
    {
        if (holdLayer >= 0 && gameObject.layer == holdLayer) return true;
        PickUpScript pickup = Object.FindAnyObjectByType<PickUpScript>();
        if (pickup != null && (pickup.HeldObj == gameObject || (pickup.HeldObj != null && transform.IsChildOf(pickup.HeldObj.transform))))
        {
            return true;
        }
        return false;
    }

    private void Update()
    {
        bool inHand = EnLaMano();

        // Solo se puede encender cuando la tengas en la mano.
        // Si no está en la mano, se apaga de inmediato si estaba encendida.
        if (!inHand)
        {
            if (encendida)
            {
                encendida = false;
                if (luz != null) luz.enabled = false;
                Encendidas.Remove(this);
            }
            return;
        }

        if (Input.GetKeyDown(teclaToggle))
        {
            encendida = !encendida;

            if (encendida)
            {
                if (!Encendidas.Contains(this)) Encendidas.Add(this);
                // Al encender la linterna, comprobar inmediatamente a quién se apunta
                CheckIlluminatedDinosaurs();
            }
            else
            {
                if (Encendidas.Contains(this)) Encendidas.Remove(this);
                // Al apagar la linterna tras haber apuntado a los dinosaurios en su cono de visión,
                // tanto el grande como el pequeño huyen del jugador!
                TriggerFleeForIlluminatedDinosaurs();
            }
        }

        if (luz != null) luz.enabled = encendida;

        // Mientras esté encendida en la mano, verificar dinos apuntados dentro de su cono de visión
        if (encendida)
        {
            CheckIlluminatedDinosaurs();
        }
    }

    private void LateUpdate()
    {
        if (luz == null) return;

        luz.gameObject.layer = 0;
        luz.transform.position = Origen.position;
        luz.transform.rotation = Origen.rotation;
    }

    /// <summary>
    /// Comprueba qué dinosaurios están siendo apuntados por el haz de la linterna
    /// y frente a los cuales el jugador está dentro de su cono de visión.
    /// </summary>
    private void CheckIlluminatedDinosaurs()
    {
        if (EnemyAI.AllEnemies == null || EnemyAI.AllEnemies.Count == 0) return;

        Vector3 originPos = Origen.position;
        Vector3 originFwd = Origen.forward;
        int wallMask = ~((1 << 2) | (1 << 3));

        for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
        {
            EnemyAI dino = EnemyAI.AllEnemies[i];
            if (dino == null || !dino.gameObject.activeInHierarchy) continue;

            // 1. Distancia
            Vector3 dinoEye = dino.transform.position + Vector3.up * dino.eyeHeight;
            Vector3 toDino = dinoEye - originPos;
            float dist = toDino.magnitude;
            if (dist > alcance || dist < 0.2f) continue;

            // 2. Apuntándole con el haz de la linterna
            float angleFromBeam = Vector3.Angle(originFwd, toDino.normalized);
            if (angleFromBeam > anguloCono * 0.5f) continue;

            // 3. Línea de visión no obstruida por muros
            if (Physics.Linecast(originPos, dinoEye, out RaycastHit hit, wallMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != dino.transform && !hit.transform.IsChildOf(dino.transform) && !dino.transform.IsChildOf(hit.transform))
                {
                    continue;
                }
            }

            // 4. ¿Está el jugador dentro del cono de visión del dinosaurio?
            Vector3 dinoToPlayer = originPos - dino.transform.position;
            dinoToPlayer.y = 0f;
            if (dinoToPlayer.sqrMagnitude < 0.001f) continue;

            float angleFromDino = Vector3.Angle(dino.transform.forward, dinoToPlayer.normalized);
            float maxDinoCone = dino.GetVisionOrFacingCone();

            if (angleFromDino <= maxDinoCone * 0.5f)
            {
                lastIlluminatedTimes[dino] = Time.time;
            }
        }
    }

    /// <summary>
    /// Provoca la huida de todos los dinosaurios que hayan sido apuntados con la linterna
    /// en su cono de visión antes de apagarla (enciende y apaga).
    /// </summary>
    private void TriggerFleeForIlluminatedDinosaurs()
    {
        Transform playerTransform = GetPlayerTransform();
        float now = Time.time;
        List<EnemyAI> toRemove = new List<EnemyAI>();

        foreach (var kvp in lastIlluminatedTimes)
        {
            EnemyAI dino = kvp.Key;
            float timeSinceIlluminated = now - kvp.Value;

            if (dino != null && dino.gameObject.activeInHierarchy && timeSinceIlluminated <= 2.5f)
            {
                float dist = Vector3.Distance(transform.position, dino.transform.position);
                if (dist <= alcance + 4f)
                {
                    dino.TriggerScaredByLight(playerTransform);
                    toRemove.Add(dino);
                }
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            lastIlluminatedTimes.Remove(toRemove[i]);
        }
    }

    private Transform GetPlayerTransform()
    {
        PickUpScript pickup = Object.FindAnyObjectByType<PickUpScript>();
        if (pickup != null) return pickup.transform.root;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) return p.transform;

        Camera cam = Camera.main;
        if (cam != null) return cam.transform.root;

        return transform;
    }

    public bool Ilumina(Collider col)
    {
        if (!encendida || col == null) return false;

        Vector3 o = Origen.position;
        Vector3 punto = col.bounds.ClosestPoint(o);
        Vector3 dir = punto - o;
        float dist = dir.magnitude;

        if (dist > alcance) return false;
        if (dist < 0.01f) return true;

        return Vector3.Angle(Origen.forward, dir) <= anguloCono * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        Transform t = Origen;
        Gizmos.color = new Color(1f, 0.95f, 0.3f, 0.8f);

        float r = Mathf.Tan(anguloCono * 0.5f * Mathf.Deg2Rad) * alcance;
        Vector3 centro = t.position + t.forward * alcance;

        Vector3[] p = {
            centro + t.up * r, centro - t.up * r,
            centro + t.right * r, centro - t.right * r
        };

        foreach (Vector3 v in p) Gizmos.DrawLine(t.position, v);
        Gizmos.DrawLine(p[0], p[2]); Gizmos.DrawLine(p[2], p[1]);
        Gizmos.DrawLine(p[1], p[3]); Gizmos.DrawLine(p[3], p[0]);
    }
}
