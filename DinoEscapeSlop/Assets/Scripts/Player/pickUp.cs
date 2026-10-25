using UnityEngine;

public class PickUpScript : MonoBehaviour
{
    [Header("Referencias")]
    public Transform holdPos;

    [Header("Coger / Lanzar")]
    public float pickUpRange = 5f;
    public float throwForce = 15f;

    [Header("Suavizado de PosiciÃ³n")]
    public float positionSmoothTime = 0.12f;
    public float maxFollowSpeed = 15f;
    public float maxLag = 0.4f;

    [Header("RotaciÃ³n")]
    public float rotationSmoothness = 8f;
    public float rotationSensitivity = 3f;

    [Header("Soltar")]
    public float safeDropMargin = 0.3f;

    private GameObject heldObj;
    private Rigidbody heldObjRb;
    private Linterna heldLinterna;

    private GameObject hoveredObj;
    private string hoveredObjName = string.Empty;

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

    private static GUIStyle hudPromptStyle;
    private static Texture2D hudBgTex;

    private void Start()
    {
        holdLayer = LayerMask.NameToLayer("holdLayer");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObj == null)
            {
                TryPickUp();
            }
            else
            {
                Release();
            }
        }

        if (heldObj == null)
        {
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, pickUpRange) && hit.transform.CompareTag("canPickUp"))
            {
                hoveredObj = hit.transform.gameObject;
                if (hoveredObj.TryGetComponent<PickupPrompt>(out var prompt) && !string.IsNullOrEmpty(prompt.objectDisplayName))
                {
                    hoveredObjName = prompt.objectDisplayName;
                }
                else
                {
                    string cleanName = hoveredObj.name.Replace("(Clone)", "").Trim();
                    hoveredObjName = cleanName.ToLower().Contains("linterna") ? "Linterna" : cleanName;
                }
            }
            else
            {
                hoveredObj = null;
            }
            return;
        }

        hoveredObj = null;

        if (heldLinterna == null && Input.GetKey(KeyCode.R))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSensitivity;

            localRotation = Quaternion.AngleAxis(-mouseX, Vector3.up) *
                            Quaternion.AngleAxis(mouseY, Vector3.right) *
                            localRotation;
        }

        bool shouldThrow = Input.GetKeyDown(KeyCode.F) || (heldLinterna == null && Input.GetKeyDown(KeyCode.Mouse0));
        if (shouldThrow)
        {
            Throw();
        }
    }

    private void LateUpdate()
    {
        if (heldObj == null) return;

        Transform objTransform = heldObj.transform;

        Vector3 targetHoldPosition;
        Quaternion baseHoldRotation;

        if (heldLinterna != null)
        {
            Transform camTransform = (holdPos != null && holdPos.parent != null)
                ? holdPos.parent
                : (Camera.main != null ? Camera.main.transform : transform);

            targetHoldPosition = camTransform.TransformPoint(heldLinterna.offsetEnMano);
            baseHoldRotation = camTransform.rotation;
        }
        else
        {
            targetHoldPosition = holdPos != null ? holdPos.position : transform.position + transform.forward * 1.5f;
            baseHoldRotation = holdPos != null ? holdPos.rotation : transform.rotation;
        }

        Vector3 targetPosition = Vector3.SmoothDamp(
            objTransform.position,
            targetHoldPosition,
            ref moveVelocity,
            positionSmoothTime,
            maxFollowSpeed
        );

        Vector3 offset = targetPosition - targetHoldPosition;
        if (offset.magnitude > maxLag)
        {
            targetPosition = targetHoldPosition + offset.normalized * maxLag;
        }

        objTransform.position = targetPosition;

        Quaternion targetRotation = (heldLinterna != null)
            ? heldLinterna.RotacionEnMano(baseHoldRotation)
            : baseHoldRotation * localRotation;

        float smoothFactor = 1f - Mathf.Exp(-rotationSmoothness * Time.deltaTime);
        objTransform.rotation = Quaternion.Slerp(objTransform.rotation, targetRotation, smoothFactor);
    }

    private void TryPickUp()
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, pickUpRange) && hit.transform.CompareTag("canPickUp"))
        {
            PickUp(hit.transform.gameObject);
        }
    }

    private void PickUp(GameObject obj)
    {
        if (!obj.TryGetComponent<Rigidbody>(out var rb)) return;

        heldObj = obj;
        heldObjRb = rb;

        heldLinterna = obj.GetComponentInChildren<Linterna>() ?? obj.GetComponentInParent<Linterna>();

        RastreoSonoro rs = obj.GetComponentInChildren<RastreoSonoro>() ?? obj.GetComponentInParent<RastreoSonoro>();
        if (rs != null)
        {
            rs.Recoger();
        }

        originalKinematic = rb.isKinematic;
        originalDetectCollisions = rb.detectCollisions;

        heldTransforms = obj.GetComponentsInChildren<Transform>();
        originalLayers = new int[heldTransforms.Length];

        for (int i = 0; i < heldTransforms.Length; i++)
        {
            originalLayers[i] = heldTransforms[i].gameObject.layer;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.detectCollisions = false;

        if (holdLayer >= 0)
        {
            foreach (Transform tr in heldTransforms)
            {
                tr.gameObject.layer = holdLayer;
            }
        }

        Quaternion holdRot = holdPos != null ? holdPos.rotation : transform.rotation;
        localRotation = Quaternion.Inverse(holdRot) * obj.transform.rotation;
        moveVelocity = Vector3.zero;
    }

    private void Release(bool lanzado = false)
    {
        if (heldObj == null) return;

        PlaceSafely();

        for (int i = 0; i < heldTransforms.Length; i++)
        {
            if (heldTransforms[i] != null)
            {
                heldTransforms[i].gameObject.layer = originalLayers[i];
            }
        }

        if (heldObjRb != null)
        {
            heldObjRb.isKinematic = originalKinematic;
            heldObjRb.detectCollisions = originalDetectCollisions;
        }

        RastreoSonoro rs = heldObj.GetComponentInChildren<RastreoSonoro>() ?? heldObj.GetComponentInParent<RastreoSonoro>();
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

    public void ForceRelease()
    {
        if (heldObj != null)
        {
            Release(false);
        }
    }

    private void Throw()
    {
        Rigidbody rb = heldObjRb;
        Release(true);

        if (rb != null)
        {
            rb.AddForce(transform.forward * throwForce, ForceMode.VelocityChange);
        }
    }

    private void PlaceSafely()
    {
        int mask = holdLayer >= 0 ? ~(1 << holdLayer) : ~0;
        Vector3 from = transform.position;
        Vector3 to = heldObj.transform.position;

        if (Physics.Linecast(from, to, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore))
        {
            heldObj.transform.position = hit.point + hit.normal * safeDropMargin;
        }
    }

    private void OnGUI()
    {
        InitHUDStyles();

        if (heldObj != null)
        {
            float width = 460f;
            float height = 40f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 70f;
            Rect bgRect = new Rect(x, y, width, height);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
            GUI.DrawTexture(bgRect, hudBgTex != null ? hudBgTex : Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 8f);
            GUI.color = oldColor;

            string promptText = (heldLinterna != null)
                ? $"<b><color=#FFD700>[ Clic Izq ]</color></b> Linterna ({(heldLinterna.encendida ? "ON" : "OFF")})  |  <b><color=#FFD700>[ F ]</color></b> Lanzar  |  <b><color=#FFD700>[ E ]</color></b> Soltar"
                : "<b><color=#FFD700>[ F ]</color></b> Lanzar  |  <b><color=#FFD700>[ E ]</color></b> Soltar  |  <b><color=#FFD700>[ R ]</color></b> Rotar";

            GUI.Label(bgRect, promptText, hudPromptStyle);
        }
        else if (hoveredObj != null)
        {
            float width = 270f;
            float height = 38f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.5f + 40f;
            Rect bgRect = new Rect(x, y, width, height);

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
