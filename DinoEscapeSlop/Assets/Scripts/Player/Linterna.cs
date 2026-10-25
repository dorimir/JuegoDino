using System.Collections.Generic;
using UnityEngine;

public class Linterna : MonoBehaviour
{
    public static readonly List<Linterna> Encendidas = new List<Linterna>();

    [Header("Cono de Luz")]
    public float alcance = 12f;
    [Range(5f, 120f)] public float anguloCono = 45f;

    public Transform origen;

    [Header("Posición en la Mano")]

    public Vector3 offsetEnMano = new Vector3(0.28f, -0.22f, 0.52f);

    public Vector3 inclinacionEnMano = new Vector3(0f, -2f, 0f);

    [Header("Control")]
    public KeyCode teclaToggle = KeyCode.Mouse0;
    public bool encendida;
    public bool soloSiRecogida = true;

    [Header("Luz Visual (Spot Light)")]
    public Light luz;
    public float intensidad = 200f;

    [Header("Sonido de Caída / Impacto")]
    public float radioSonidoCaida = 22f;
    public float intensidadSonidoCaida = 2.0f;
    public float duracionAlertaCaida = 4.5f;

    public static bool HayImpactoReciente { get; private set; }
    public static Vector3 PosicionUltimoImpacto { get; private set; }
    public static float TiempoUltimoImpacto { get; private set; }
    public static Transform TransformLinterna { get; private set; }

    private AudioSource audioImpacto;
    private AudioClip clipImpacto;
    private float ultimoTiempoImpacto;
    private int holdLayer;
    private readonly Dictionary<EnemyAI, float> lastIlluminatedTimes = new Dictionary<EnemyAI, float>();
    private PickUpScript cachedPickup;
    private Transform cachedPlayerTransform;

    public Transform Origen => origen != null ? origen : transform;

    public Quaternion RotacionEnMano(Quaternion holdRot)
    {
        Quaternion relOrigen = Quaternion.Inverse(transform.rotation) * Origen.rotation;
        return holdRot * Quaternion.Euler(inclinacionEnMano) * Quaternion.Inverse(relOrigen);
    }

    private void Awake()
    {
        TransformLinterna = transform;
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

        audioImpacto = GetComponent<AudioSource>();
        if (audioImpacto == null)
        {
            audioImpacto = gameObject.AddComponent<AudioSource>();
            audioImpacto.spatialBlend = 1.0f;
            audioImpacto.minDistance = 2f;
            audioImpacto.maxDistance = 25f;
            audioImpacto.rolloffMode = AudioRolloffMode.Linear;
            audioImpacto.playOnAwake = false;
        }

        clipImpacto = Resources.Load<AudioClip>("Audio/SFX_Linterna_Impacto");
        if (clipImpacto != null)
        {
            audioImpacto.clip = clipImpacto;
        }
    }

    [Header("Posición de Spawn")]
    public static readonly Vector3 PosicionSpawnOriginal = new Vector3(-125.6f, 2.1f, 32.8f);

    public void ResetearPosicionYEstado()
    {
        transform.position = PosicionSpawnOriginal;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        encendida = false;
        if (luz != null)
        {
            luz.enabled = false;
            luz.transform.position = transform.position + transform.forward * 0.15f;
            luz.transform.rotation = transform.rotation;
        }
        Encendidas.Remove(this);

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        gameObject.layer = 0;
        HayImpactoReciente = false;
    }

    private void Start()
    {
        ResetearPosicionYEstado();
    }

    private void OnDestroy()
    {
        if (luz != null)
        {
            Destroy(luz.gameObject);
        }
    }

    private void OnDisable()
    {
        Encendidas.Remove(this);
    }

    public bool EnLaMano()
    {
        if (holdLayer >= 0 && gameObject.layer == holdLayer) return true;

        if (cachedPickup == null) cachedPickup = Object.FindAnyObjectByType<PickUpScript>();
        return cachedPickup != null && (cachedPickup.HeldObj == gameObject || (cachedPickup.HeldObj != null && transform.IsChildOf(cachedPickup.HeldObj.transform)));
    }

    private void Update()
    {
        bool inHand = EnLaMano();

        if (!inHand)
        {
            if (encendida)
            {
                encendida = false;
                if (luz != null) luz.enabled = false;
                Encendidas.Remove(this);
            }

            if (HayImpactoReciente && Time.time - TiempoUltimoImpacto > duracionAlertaCaida)
            {
                HayImpactoReciente = false;
            }
            return;
        }

        if (Input.GetKeyDown(teclaToggle))
        {
            encendida = !encendida;

            if (encendida)
            {
                if (!Encendidas.Contains(this)) Encendidas.Add(this);
                CheckIlluminatedDinosaurs();
            }
            else
            {
                Encendidas.Remove(this);
                TriggerFleeForIlluminatedDinosaurs();
            }
        }

        if (luz != null)
        {
            luz.enabled = encendida;
        }

        if (encendida)
        {
            CheckIlluminatedDinosaurs();
        }
    }

    private void LateUpdate()
    {
        if (luz == null) return;

        luz.gameObject.layer = 0;
        luz.transform.position = Origen.position + Origen.forward * 0.15f;
        luz.transform.rotation = Origen.rotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (EnLaMano()) return;
        if (collision.collider.CompareTag("Player") || collision.transform.root.CompareTag("Player")) return;
        if (Time.timeSinceLevelLoad < 1.0f) return;

        float impactVel = collision.relativeVelocity.magnitude;
        if (impactVel < 0.35f && (Time.time - ultimoTiempoImpacto < 0.4f)) return;
        if (Time.time - ultimoTiempoImpacto < 0.25f) return;

        ultimoTiempoImpacto = Time.time;
        EmitirSonidoCaida();
    }

    public void EmitirSonidoCaida()
    {
        HayImpactoReciente = true;
        PosicionUltimoImpacto = transform.position;
        TiempoUltimoImpacto = Time.time;
        TransformLinterna = transform;

        if (audioImpacto != null)
        {
            if (clipImpacto == null) clipImpacto = Resources.Load<AudioClip>("Audio/SFX_Linterna_Impacto");
            if (clipImpacto != null)
            {
                audioImpacto.pitch = Random.Range(0.85f, 1.15f);
                audioImpacto.PlayOneShot(clipImpacto, 1.0f);
            }
        }

        NotificarImpactoACiegos(transform.position, radioSonidoCaida, intensidadSonidoCaida);
    }

    public static void NotificarImpactoACiegos(Vector3 pos, float radio, float intensidad)
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
                    blind.NotificarSonidoPrioritario(pos, intensidad, TransformLinterna);
                }
            }
        }
    }

    public static bool HaySonidoCaidaActivo(Vector3 listenerPos, float multiplier, out Vector3 soundPos, out float intensity, out Transform soundSource)
    {
        soundPos = Vector3.zero;
        intensity = 0f;
        soundSource = null;

        if (!HayImpactoReciente) return false;
        if (Time.time - TiempoUltimoImpacto > 4.5f)
        {
            HayImpactoReciente = false;
            return false;
        }

        float dist = Vector3.Distance(listenerPos, PosicionUltimoImpacto);
        if (dist <= 22f * multiplier)
        {
            soundPos = PosicionUltimoImpacto;
            intensity = 2.0f;
            soundSource = TransformLinterna;
            return true;
        }

        return false;
    }

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
            if (dino is BlindEnemyAI || dino.dinoCiego) continue;

            Vector3 dinoEye = dino.transform.position + Vector3.up * dino.eyeHeight;
            Vector3 dinoCenter = dino.transform.position + Vector3.up * Mathf.Max(0.25f, dino.eyeHeight * 0.5f);

            Vector3 toDino = dinoCenter - originPos;
            float dist = toDino.magnitude;
            if (dist > alcance || dist < 0.2f) continue;

            float angleFromBeam = Vector3.Angle(originFwd, toDino.normalized);
            if (angleFromBeam > anguloCono * 0.5f) continue;

            bool occluded = true;
            if (!Physics.Linecast(originPos, dinoEye, out RaycastHit hit, wallMask, QueryTriggerInteraction.Ignore))
            {
                occluded = false;
            }
            else if (hit.transform == dino.transform || hit.transform.IsChildOf(dino.transform) || dino.transform.IsChildOf(hit.transform))
            {
                occluded = false;
            }
            else if (!Physics.Linecast(originPos, dinoCenter, out RaycastHit hitCenter, wallMask, QueryTriggerInteraction.Ignore))
            {
                occluded = false;
            }
            else if (hitCenter.transform == dino.transform || hitCenter.transform.IsChildOf(dino.transform) || dino.transform.IsChildOf(hitCenter.transform))
            {
                occluded = false;
            }

            if (occluded) continue;

            lastIlluminatedTimes[dino] = Time.time;

            if (dino.StateMachine != null && dino.StateMachine.CurrentState != dino.FleeState)
            {
                Transform pTransform = GetPlayerTransform();
                dino.TriggerScaredByLight(pTransform);
            }
        }
    }

    private void TriggerFleeForIlluminatedDinosaurs()
    {
        Transform playerTransform = GetPlayerTransform();
        float now = Time.time;
        List<EnemyAI> toRemove = new List<EnemyAI>();

        foreach (var kvp in lastIlluminatedTimes)
        {
            EnemyAI dino = kvp.Key;
            if (dino == null || dino is BlindEnemyAI || dino.dinoCiego) continue;
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
        if (cachedPlayerTransform != null) return cachedPlayerTransform;

        if (cachedPickup == null) cachedPickup = Object.FindAnyObjectByType<PickUpScript>();
        if (cachedPickup != null)
        {
            cachedPlayerTransform = cachedPickup.transform.root;
            return cachedPlayerTransform;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            cachedPlayerTransform = p.transform;
            return cachedPlayerTransform;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cachedPlayerTransform = cam.transform.root;
            return cachedPlayerTransform;
        }

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
