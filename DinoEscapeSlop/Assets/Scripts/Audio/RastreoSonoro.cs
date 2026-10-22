using UnityEngine;

public class RastreoSonoro : MonoBehaviour
{
    public Collider Area;
    public GameObject Player;
    public AudioSource sonido;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && sonido != null)
        {
            sonido.Play();
        }
    }
}
