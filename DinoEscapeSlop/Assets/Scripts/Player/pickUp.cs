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

    public GameObject HeldObj => heldObj;
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
            return;

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

        if (Input.GetKeyDown(KeyCode.Mouse0))
            Throw();
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
}