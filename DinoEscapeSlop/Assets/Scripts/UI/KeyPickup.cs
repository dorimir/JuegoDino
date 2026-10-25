using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    [Header("AnimaciÃ³n de Llave")]

    public float rotationSpeed = 80f;

    public float bobFrequency = 2.5f;

    public float bobAmplitude = 0.12f;

    public float pickupDistance = 2.0f;

    private Vector3 initialPos;
    private Transform playerTransform;
    private bool isCollected;

    private void Start()
    {
        initialPos = transform.position;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        if (!TryGetComponent<SphereCollider>(out var col))
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = pickupDistance;
        }

        transform.position = initialPos;
    }

    private void Update()
    {
        if (isCollected) return;

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        float newY = initialPos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

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
