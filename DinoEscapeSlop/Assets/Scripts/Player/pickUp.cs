using UnityEngine;

public class PickUpScript : MonoBehaviour
{
    [Header("Referencias")]
    public Transform holdPos;

    [Header("Coger / lanzar")]
    public float pickUpRange = 5f;
    public float throwForce = 15f;

    [Header("Suavizado de posición")]
    public float positionSmoothTime = 0.12f;
    public float maxFollowSpeed = 15f;
    public float maxLag = 0.4f;

    [Header("Rotación")]
    public float rotationSmoothness = 8f;
    public float rotationSensitivity = 3f;

    [Header("Soltar")]
    public float safeDropMargin = 0.3f;

    private GameObject heldObj;
    private Rigidbody heldObjRb;
    private Linterna heldLinterna;

    private GameObject hoveredObj;
    private string hoveredObjName = "";

    public GameObject HeldObj => heldObj;
    public GameObject HoveredObj => hoveredObj;
    public bool IsHoldingObject => heldObj != null;
    public bool IsHoldingSoundObject => heldObj != null && (heldObj.GetComponentInChildren<RastreoSonoro>() != null || heldObj.CompareTag("canPickUp"));

    private Transform[] heldTransforms;
    private int[] originalLayers;
    private bool originalKinematic;
    private bool originalDetectCollisions;

    private int holdLayer;
    private Vector3 moveVelocity;
    private Quaternion localRotation;

    void Start()
    {
        holdLayer = LayerMask.NameToLayer("holdLayer");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObj == null)
                TryPickUp();
            else
                Release();
        }

        if (heldObj == null)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, pickUpRange))
            {
                if (hit.transform.CompareTag("canPickUp"))
                {
                    hoveredObj = hit.transform.gameObject;
                    PickupPrompt prompt = hoveredObj.GetComponent<PickupPrompt>();
                    if (prompt != null && !string.IsNullOrEmpty(prompt.objectDisplayName))
                    {
                        hoveredObjName = prompt.objectDisplayName;
                    }
                    else
                    {
                        string n = hoveredObj.name.Replace("(Clone)", "").Trim();
                        hoveredObjName = n.ToLower().Contains("linterna") ? "Linterna" : n;
                    }
                }
                else
                {
                    hoveredObj = null;
                }
            }
            else
            {
                hoveredObj = null;
            }
            return;
        }

        hoveredObj = null;

        // La linterna no se rota a mano
        if (heldLinterna == null && Input.GetKey(KeyCode.R))
        {
            float x = Input.GetAxis("Mouse X") * rotationSensitivity;
            float y = Input.GetAxis("Mouse Y") * rotationSensitivity;

            localRotation =
                Quaternion.AngleAxis(-x, Vector3.up) *
                Quaternion.AngleAxis(y, Vector3.right) *
                localRotation;
        }

        if (heldLinterna != null)
        {
            // La linterna se enciende con Clic Izquierdo (en Linterna.cs) y se lanza con F
            if (Input.GetKeyDown(KeyCode.F))
                Throw();
        }
        else
        {
            // Para otros objetos, se lanza con F o Clic Izquierdo
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Mouse0))
                Throw();
        }
    }

    void LateUpdate()
    {
        if (heldObj == null)
            return;

        Transform t = heldObj.transform;

        Vector3 pos = Vector3.SmoothDamp(
            t.position,
            holdPos.position,
            ref moveVelocity,
            positionSmoothTime,
            maxFollowSpeed
        );

        Vector3 offset = pos - holdPos.position;
        if (offset.magnitude > maxLag)
            pos = holdPos.position + offset.normalized * maxLag;

        t.position = pos;

        Quaternion targetRot = heldLinterna != null
            ? heldLinterna.RotacionEnMano(holdPos.rotation)
            : holdPos.rotation * localRotation;

        float k = 1f - Mathf.Exp(-rotationSmoothness * Time.deltaTime);
        t.rotation = Quaternion.Slerp(t.rotation, targetRot, k);
    }

    void TryPickUp()
    {
        RaycastHit hit;

        if (Physics.Raycast(transform.position, transform.forward, out hit, pickUpRange))
        {
            if (hit.transform.CompareTag("canPickUp"))
                PickUp(hit.transform.gameObject);
        }
    }

    void PickUp(GameObject obj)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[PICKUP] " + obj.name + " no tiene Rigidbody");
            return;
        }

        heldObj = obj;
        heldObjRb = rb;

        heldLinterna = obj.GetComponentInChildren<Linterna>();
        if (heldLinterna == null) heldLinterna = obj.GetComponentInParent<Linterna>();

        RastreoSonoro rs = obj.GetComponentInChildren<RastreoSonoro>();
        if (rs == null) rs = obj.GetComponentInParent<RastreoSonoro>();
        if (rs != null)
            rs.Recoger();

        originalKinematic = rb.isKinematic;
        originalDetectCollisions = rb.detectCollisions;

        heldTransforms = obj.GetComponentsInChildren<Transform>();
        originalLayers = new int[heldTransforms.Length];

        for (int i = 0; i < heldTransforms.Length; i++)
            originalLayers[i] = heldTransforms[i].gameObject.layer;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.detectCollisions = false;

        if (holdLayer >= 0)
            foreach (Transform tr in heldTransforms)
                tr.gameObject.layer = holdLayer;

        localRotation = Quaternion.Inverse(holdPos.rotation) * obj.transform.rotation;
        moveVelocity = Vector3.zero;
    }

    void Release(bool lanzado = false)
    {
        if (heldObj == null)
            return;

        PlaceSafely();

        for (int i = 0; i < heldTransforms.Length; i++)
            if (heldTransforms[i] != null)
                heldTransforms[i].gameObject.layer = originalLayers[i];

        heldObjRb.isKinematic = originalKinematic;
        heldObjRb.detectCollisions = originalDetectCollisions;

        RastreoSonoro rs = heldObj.GetComponentInChildren<RastreoSonoro>();
        if (rs == null) rs = heldObj.GetComponentInParent<RastreoSonoro>();
        if (rs != null)
        {
            if (lanzado) rs.Lanzar();
            else rs.Soltar();
        }

        heldObj = null;
        heldObjRb = null;
        heldLinterna = null;
        heldTransforms = null;
        originalLayers = null;
    }

    void Throw()
    {
        Rigidbody rb = heldObjRb;
        Release(true);

        if (rb != null)
            rb.AddForce(transform.forward * throwForce, ForceMode.VelocityChange);
    }

    void PlaceSafely()
    {
        int mask = ~0;
        if (holdLayer >= 0)
            mask = ~(1 << holdLayer);

        Vector3 from = transform.position;
        Vector3 to = heldObj.transform.position;

        RaycastHit hit;
        if (Physics.Linecast(from, to, out hit, mask, QueryTriggerInteraction.Ignore))
        {
            heldObj.transform.position = hit.point + hit.normal * safeDropMargin;
        }
    }

    private static GUIStyle hudPromptStyle;
    private static Texture2D hudBgTex;

    private void OnGUI()
    {
        InitHUDStyles();

        // 1. Si sostiene un objeto: mostrar controles en la parte inferior
        if (heldObj != null)
        {
            float w = 460f;
            float h = 40f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 70f;
            Rect bgRect = new Rect(x, y, w, h);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
            GUI.DrawTexture(bgRect, hudBgTex != null ? hudBgTex : Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 8f);
            GUI.color = oldColor;

            string promptText;
            if (heldLinterna != null)
            {
                promptText = $"<b><color=#FFD700>[ Clic Izq ]</color></b> Linterna ({(heldLinterna.encendida ? "ON" : "OFF")})  |  <b><color=#FFD700>[ F ]</color></b> Lanzar  |  <b><color=#FFD700>[ E ]</color></b> Soltar";
            }
            else
            {
                promptText = "<b><color=#FFD700>[ F ]</color></b> Lanzar  |  <b><color=#FFD700>[ E ]</color></b> Soltar  |  <b><color=#FFD700>[ R ]</color></b> Rotar";
            }

            GUI.Label(bgRect, promptText, hudPromptStyle);
        }
        // 2. Si está apuntando a un objeto que se puede recoger con la E:
        else if (hoveredObj != null)
        {
            float w = 270f;
            float h = 38f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.5f + 40f;
            Rect bgRect = new Rect(x, y, w, h);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
            GUI.DrawTexture(bgRect, hudBgTex != null ? hudBgTex : Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 8f);
            GUI.color = oldColor;

            string text = $"<b><color=#FFD700>[ E ]</color></b>  Recoger {hoveredObjName}";
            GUI.Label(bgRect, text, hudPromptStyle);
        }
    }

    private static void InitHUDStyles()
    {
        if (hudPromptStyle == null)
        {
            hudPromptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Normal,
                richText = true
            };
            hudPromptStyle.normal.textColor = Color.white;
        }

        if (hudBgTex == null)
        {
            hudBgTex = new Texture2D(1, 1);
            hudBgTex.SetPixel(0, 0, Color.white);
            hudBgTex.Apply();
        }
    }
}