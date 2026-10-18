using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        WinManager.Instance.AddKey();
        Destroy(gameObject);
    }
}