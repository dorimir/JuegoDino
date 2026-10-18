using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class WinManager : MonoBehaviour
{
    public static WinManager Instance { get; private set; }

    [Header("Escena")]
    [SerializeField] private string winSceneName = "WinScene";
    [SerializeField] private int keysNeeded = 10;

    [Header("Meta")]
    [Tooltip("Si lo dejas vacío, busca el objeto con tag 'Finish'")]
    [SerializeField] private GameObject finishObject;
    [SerializeField] private float finishRadius = 2f;

    [Header("Llaves")]
    [SerializeField] private float pickupRadius = 1.5f;

    [Header("UI")]
    [SerializeField] private TMP_Text keysText;
    [SerializeField] private TMP_Text plusOneText;
    [SerializeField] private string exitMessage = "Busca la salida";

    [Header("Efecto +1")]
    [SerializeField] private float popupDuration = 1f;
    [SerializeField] private float floatDistance = 40f;

    private int keysCollected = 0;
    private bool allKeysCollected = false;
    private bool won = false;
    private GameObject player;
    private Collider finishCollider;
    private readonly List<GameObject> keys = new List<GameObject>();
    private Coroutine popupRoutine;
    private Vector3 plusOneStartPos;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        keys.AddRange(GameObject.FindGameObjectsWithTag("Key"));

        if (finishObject == null)
            finishObject = GameObject.FindGameObjectWithTag("Finish");

        if (finishObject != null)
            finishCollider = finishObject.GetComponent<Collider>();

        if (player == null)
            Debug.LogWarning("WinManager: no se encontró ningún objeto con tag 'Player'.");
        if (finishObject == null)
            Debug.LogWarning("WinManager: no hay meta. Asigna 'Finish Object' o ponle tag 'Finish'.");

        if (plusOneText != null)
        {
            plusOneStartPos = plusOneText.rectTransform.localPosition;
            plusOneText.gameObject.SetActive(false);
        }

        Debug.Log("Llaves encontradas: " + keys.Count);
        UpdateKeysUI();
    }

    private void Update()
    {
        if (won || player == null) return;

        Vector3 playerPos = player.transform.position;

        // Recoger llaves por distancia (solo hasta tener todas)
        if (!allKeysCollected)
        {
            float sqrRadius = pickupRadius * pickupRadius;
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                if (keys[i] == null)
                {
                    keys.RemoveAt(i);
                    continue;
                }

                Vector3 diff = keys[i].transform.position - playerPos;
                if (diff.sqrMagnitude <= sqrRadius)
                {
                    Destroy(keys[i]);
                    keys.RemoveAt(i);
                    AddKey();

                    if (allKeysCollected) break;
                }
            }
        }

        // Comprobar meta
        if (allKeysCollected && finishObject != null && IsPlayerAtFinish(playerPos))
        {
            won = true;
            SceneManager.LoadScene(winSceneName);
        }
    }

    private bool IsPlayerAtFinish(Vector3 playerPos)
    {
        Vector3 closest = finishCollider != null
            ? finishCollider.ClosestPoint(playerPos)
            : finishObject.transform.position;

        return (closest - playerPos).sqrMagnitude <= finishRadius * finishRadius;
    }

    public void AddKey()
    {
        if (allKeysCollected) return;

        keysCollected++;

        if (keysCollected >= keysNeeded)
            allKeysCollected = true;

        UpdateKeysUI();

        if (plusOneText != null)
        {
            if (popupRoutine != null) StopCoroutine(popupRoutine);
            popupRoutine = StartCoroutine(PlusOneRoutine());
        }
    }

    private void UpdateKeysUI()
    {
        if (keysText == null) return;

        keysText.text = allKeysCollected
            ? exitMessage
            : $"Llaves: {keysCollected}/{keysNeeded}";
    }

    private IEnumerator PlusOneRoutine()
    {
        plusOneText.text = "+1";
        plusOneText.gameObject.SetActive(true);

        float t = 0f;
        while (t < popupDuration)
        {
            t += Time.deltaTime;
            float progress = t / popupDuration;

            plusOneText.rectTransform.localPosition =
                plusOneStartPos + Vector3.up * (floatDistance * progress);
            plusOneText.color = new Color(0f, 1f, 0f, 1f - progress);

            yield return null;
        }

        plusOneText.gameObject.SetActive(false);
        plusOneText.rectTransform.localPosition = plusOneStartPos;
    }
}