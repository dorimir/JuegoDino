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
    [Tooltip("Posición exacta de la puerta de salida")]
    [SerializeField] private Vector3 doorPosition = new Vector3(10.670000076293946f, 0.027003999799489976f, 77.9800033569336f);
    [SerializeField] private GameObject finishObject;
    [SerializeField] private float finishRadius = 3.0f;

    [Header("Llaves")]
    [SerializeField] private float pickupRadius = 2.0f;

    [Header("UI")]
    [SerializeField] private TMP_Text keysText;
    [SerializeField] private TMP_Text plusOneText;
    [SerializeField] private string exitMessage = "¡Salida Desbloqueada! Cruza la puerta con el objeto sonoro";

    [Header("Efecto +1")]
    [SerializeField] private float popupDuration = 1f;
    [SerializeField] private float floatDistance = 40f;

    private int keysCollected = 0;
    private bool allKeysCollected = false;
    private bool won = false;
    private GameObject player;
    private GameObject soundObject;
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
        keys.Clear();
        keys.AddRange(GameObject.FindGameObjectsWithTag("Key"));

        // Garantizar que cada llave tenga el script de animación y recogida KeyPickup
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i] != null && keys[i].GetComponent<KeyPickup>() == null)
            {
                keys[i].AddComponent<KeyPickup>();
            }
        }

        // Localizar el objeto sonoro en la escena
        FindSoundObject();

        if (finishObject == null)
            finishObject = GameObject.FindGameObjectWithTag("Finish");

        if (finishObject == null)
        {
            GameObject doorGo = GameObject.Find("ExitDoor") ?? GameObject.Find("Door");
            if (doorGo != null) finishObject = doorGo;
        }

        if (finishObject != null)
            finishCollider = finishObject.GetComponent<Collider>();

        if (player == null)
            Debug.LogWarning("WinManager: no se encontró ningún objeto con tag 'Player'.");

        if (keysText == null)
        {
            GameObject kc = GameObject.Find("KeyCounter");
            if (kc != null) keysText = kc.GetComponent<TMP_Text>();
        }

        if (plusOneText != null)
        {
            plusOneStartPos = plusOneText.rectTransform.localPosition;
            plusOneText.gameObject.SetActive(false);
        }

        Debug.Log("Llaves encontradas: " + keys.Count);
        UpdateKeysUI();
    }

    private void FindSoundObject()
    {
        soundObject = GameObject.Find("Object+TriggerSonoro");
        if (soundObject == null)
        {
            RastreoSonoro rs = Object.FindAnyObjectByType<RastreoSonoro>();
            if (rs != null)
            {
                soundObject = rs.GetComponentInParent<Rigidbody>()?.gameObject ?? rs.gameObject;
            }
        }
    }

    private void Update()
    {
        if (won || player == null) return;

        Vector3 playerPos = player.transform.position;

        // Recoger llaves por distancia 2D (solo hasta tener todas)
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
                Vector2 diff2D = new Vector2(diff.x, diff.z);

                if (diff2D.sqrMagnitude <= sqrRadius && Mathf.Abs(diff.y) < 2.5f)
                {
                    GameObject keyObj = keys[i];
                    keys.RemoveAt(i);
                    Destroy(keyObj);
                    AddKey();

                    if (allKeysCollected) break;
                }
            }
        }

        // Comprobar si el jugador alcanza la puerta con las 10 llaves Y el objeto sonoro
        Vector3 exitPos = finishObject != null ? finishObject.transform.position : doorPosition;
        bool isAtDoor = IsPlayerAtFinish(playerPos, exitPos);
        bool hasSound = HasSoundObject(playerPos, exitPos);

        if (allKeysCollected && hasSound && isAtDoor)
        {
            won = true;
            SceneManager.LoadScene(winSceneName);
        }
    }

    private bool HasSoundObject(Vector3 playerPos, Vector3 exitPos)
    {
        if (soundObject == null) FindSoundObject();

        // 1. Verificar si el jugador lo lleva en las manos (PickUpScript)
        PickUpScript pickup = player.GetComponent<PickUpScript>() ?? player.GetComponentInChildren<PickUpScript>();
        if (pickup != null && pickup.IsHoldingSoundObject) return true;

        // 2. Verificar si el objeto sonoro está junto al jugador o en la puerta
        if (soundObject != null)
        {
            float distToPlayer = Vector3.Distance(soundObject.transform.position, playerPos);
            float distToDoor = Vector3.Distance(soundObject.transform.position, exitPos);
            if (distToPlayer <= 4.0f || distToDoor <= 5.0f) return true;
        }

        return false;
    }

    private bool IsPlayerAtFinish(Vector3 playerPos, Vector3 exitPos)
    {
        Vector3 closest = finishCollider != null
            ? finishCollider.ClosestPoint(playerPos)
            : exitPos;

        Vector2 diff2D = new Vector2(closest.x - playerPos.x, closest.z - playerPos.z);
        return diff2D.sqrMagnitude <= finishRadius * finishRadius && Mathf.Abs(closest.y - playerPos.y) < 3.5f;
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
        if (keysText == null)
        {
            GameObject kc = GameObject.Find("KeyCounter");
            if (kc != null) keysText = kc.GetComponent<TMP_Text>();
        }

        if (keysText != null)
        {
            if (!allKeysCollected)
            {
                keysText.text = $"Llaves: {keysCollected} / {keysNeeded}";
            }
            else
            {
                keysText.text = exitMessage;
            }
        }
    }

    private void OnGUI()
    {
        if (won || EnemyAI.GlobalPlayerDead) return;

        Vector3 exitPos = finishObject != null ? finishObject.transform.position : doorPosition;
        Vector3 playerPos = (player != null) ? player.transform.position : Vector3.zero;
        bool hasSound = (player != null) && HasSoundObject(playerPos, exitPos);
        bool isAtDoor = (player != null) && IsPlayerAtFinish(playerPos, exitPos);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 16;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.alignment = TextAnchor.MiddleCenter;

        if (!allKeysCollected)
        {
            boxStyle.normal.textColor = new Color(1f, 0.88f, 0.2f);
            string extra = isAtDoor ? "\n(¡Faltan llaves para abrir la puerta!)" : "";
            GUI.Box(new Rect(Screen.width - 250, 20, 230, isAtDoor ? 54 : 42), $"🔑 Llaves: {keysCollected} / {keysNeeded}{extra}", boxStyle);
        }
        else if (!hasSound)
        {
            boxStyle.normal.textColor = new Color(1f, 0.5f, 0.1f);
            GUI.Box(new Rect(Screen.width - 320, 20, 300, 52), "📦 ¡10 Llaves conseguidas!\n¡Trae el objeto sonoro a la puerta!", boxStyle);
        }
        else
        {
            boxStyle.normal.textColor = new Color(0.2f, 1f, 0.3f);
            GUI.Box(new Rect(Screen.width - 300, 20, 280, 52), "🚪 ¡Todo listo (Llaves + Objeto)!\nCruza la puerta para escapar", boxStyle);
        }
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
