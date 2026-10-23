using System.Collections.Generic;
using UnityEngine;

public class Linterna : MonoBehaviour
{
    public static readonly List<Linterna> Encendidas = new List<Linterna>();

    [Header("Cono")]
    public float alcance = 12f;
    [Range(5f, 120f)] public float anguloCono = 40f;
    [Tooltip("Punto desde el que sale la luz. Si está vacío usa el propio objeto. Su eje Z azul debe apuntar por donde sale la luz.")]
    public Transform origen;

    [Header("Posición en la mano")]
    [Tooltip("Inclinación extra respecto a mirar recto (X positivo = apunta un poco hacia abajo).")]
    public Vector3 inclinacionEnMano = Vector3.zero;

    [Header("Control")]
    public KeyCode teclaToggle = KeyCode.F;
    public bool encendida;
    public bool soloSiRecogida = true;

    [Header("Luz visual (Spot Light)")]
    public Light luz;
    public float intensidad = 200f;

    private int holdLayer;

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
    }

    private void OnDestroy()
    {
        if (luz != null) Destroy(luz.gameObject);
    }

    private void OnDisable()
    {
        Encendidas.Remove(this);
    }

    private bool EnLaMano()
    {
        return holdLayer >= 0 && gameObject.layer == holdLayer;
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaToggle) && (!soloSiRecogida || EnLaMano()))
            encendida = !encendida;

        if (luz != null) luz.enabled = encendida;

        if (encendida && !Encendidas.Contains(this)) Encendidas.Add(this);
        if (!encendida && Encendidas.Contains(this)) Encendidas.Remove(this);
    }

    private void LateUpdate()
    {
        if (luz == null) return;

        luz.gameObject.layer = 0;
        luz.transform.position = Origen.position;
        luz.transform.rotation = Origen.rotation;
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