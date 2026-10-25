using UnityEngine;

[ExecuteInEditMode]
public class Zoom : MonoBehaviour
{
    private new Camera camera;

    [Header("Field of View")]
    public float defaultFOV = 60f;
    public float maxZoomFOV = 15f;

    [Header("Zoom Settings")]
    [Range(0f, 1f)]
    public float currentZoom;
    public float sensitivity = 1f;

    private void Awake()
    {
        camera = GetComponent<Camera>();
        if (camera != null)
        {
            defaultFOV = camera.fieldOfView;
        }
    }

    private void Update()
    {
        if (camera == null) return;

        currentZoom = Mathf.Clamp01(currentZoom + Input.mouseScrollDelta.y * sensitivity * 0.05f);
        camera.fieldOfView = Mathf.Lerp(defaultFOV, maxZoomFOV, currentZoom);
    }
}
