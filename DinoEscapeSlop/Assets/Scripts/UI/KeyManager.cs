using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    [Header("Animación de Llave")]
    [Tooltip("Velocidad de rotación continua sobre su propio eje en grados por segundo.")]
    public float rotationSpeed = 80f;

    [Tooltip("Frecuencia del suave balanceo vertical.")]
    public float bobFrequency = 2.5f;

    [Tooltip("Amplitud del balanceo vertical en metros.")]
    public float bobAmplitude = 0.12f;

    [Tooltip("Radio de recogida por proximidad.")]
    public float pickupDistance = 2.0f;

    private Vector3 initialPos;
    private Transform playerTransform;
    private bool isCollected = false;

    private void Start()
    {
        initialPos = transform.position;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = pickupDistance;
        }
    }

    private void Update()
    {
        if (isCollected) return;

        // 1. Rotación suave continua
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. Balanceo vertical
        float newY = initialPos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // 3. Verificación de proximidad al jugador
        if (playerTransform != null)
        {
            Vector3 diff = transform.position - playerTransform.position;
            Vector2 diff2D = new Vector2(diff.x, diff.z);
            if (diff2D.sqrMagnitude <= pickupDistance * pickupDistance && Mathf.Abs(diff.y) < 2.5f)
            {
                Collect();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (WinManager.Instance != null)
        {
            WinManager.Instance.AddKey();
        }
        Destroy(gameObject);
    }
}
