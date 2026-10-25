using UnityEngine;

[DisallowMultipleComponent]
public class PickupPrompt : MonoBehaviour
{
    [Header("Configuración del Prompt")]

    public string promptKey = "E";

    public string actionText = "Recoger";

    public string objectDisplayName = "";

    public float maxDistance = 6.0f;

    public float heightOffset = 0.35f;

    public bool onlyWhenLookingAt = true;

    public bool showWorldBadge = false;

    private Camera playerCam;
    private PickUpScript playerPickup;
    private Collider cachedCollider;

    private static GUIStyle badgeStyle;
    private static Texture2D bgTexture;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (string.IsNullOrEmpty(objectDisplayName))
        {
            objectDisplayName = CleanObjectName(gameObject.name);
        }

        if (!CompareTag("canPickUp"))
        {
            gameObject.tag = "canPickUp";
        }
    }

    private void Start()
    {
        FindPlayerReferences();
    }

    private void FindPlayerReferences()
    {
        if (playerCam != null) return;

        playerCam = Camera.main;
        if (playerCam == null)
        {
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && (cams[i].CompareTag("MainCamera") || cams[i].name.ToLower().Contains("firstperson") || cams[i].name.ToLower().Contains("player")))
                {
                    playerCam = cams[i];
                    break;
                }
            }
        }

        if (playerCam != null)
        {
            playerPickup = playerCam.GetComponent<PickUpScript>() ?? playerCam.GetComponentInParent<PickUpScript>();
        }
    }

    private void OnGUI()
    {
        if (!showWorldBadge) return;

        FindPlayerReferences();
        if (playerCam == null) return;

        if (playerPickup != null && playerPickup.HeldObj == gameObject) return;

        Vector3 targetWorldPos = (cachedCollider != null)
            ? cachedCollider.bounds.center + Vector3.up * heightOffset
            : transform.position + Vector3.up * heightOffset;

        Vector3 camPos = playerCam.transform.position;
        Vector3 toTarget = targetWorldPos - camPos;
        float dist = toTarget.magnitude;

        if (dist > maxDistance || dist < 0.2f) return;

        int mask = ~((1 << 2) | (1 << 3));

        if (onlyWhenLookingAt)
        {
            bool isLookingAt = false;
            Ray lookRay = new Ray(playerCam.transform.position, playerCam.transform.forward);
            if (Physics.Raycast(lookRay, out RaycastHit lookHit, maxDistance, mask, QueryTriggerInteraction.Ignore))
            {
                if (lookHit.transform == transform || lookHit.transform.IsChildOf(transform) || transform.IsChildOf(lookHit.transform))
                {
                    isLookingAt = true;
                }
            }

            if (!isLookingAt && playerPickup != null && playerPickup.HoveredObj != null)
            {
                if (playerPickup.HoveredObj == gameObject || playerPickup.HoveredObj.transform.IsChildOf(transform) || transform.IsChildOf(playerPickup.HoveredObj.transform))
                {
                    isLookingAt = true;
                }
            }

            if (!isLookingAt) return;
        }
        else
        {
            if (Vector3.Dot(playerCam.transform.forward, toTarget.normalized) < 0.35f) return;
        }

        if (Physics.Linecast(camPos, targetWorldPos, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform) && !transform.IsChildOf(hit.transform))
            {
                return;
            }
        }

        Vector3 screenPos = playerCam.WorldToScreenPoint(targetWorldPos);
        if (screenPos.z <= 0f) return;

        InitStyles();

        float guiY = Screen.height - screenPos.y;
        float width = 175f;
        float height = 34f;
        Rect rect = new Rect(screenPos.x - width * 0.5f, guiY - height * 0.5f, width, height);

        Color oldColor = GUI.color;
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
        GUI.DrawTexture(rect, bgTexture != null ? bgTexture : Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 8f);
        GUI.color = oldColor;

        string displayLabel = $"<b><color=#FFD700>[ {promptKey} ]</color></b> {actionText} {objectDisplayName}";
        GUI.Label(rect, displayLabel, badgeStyle);
    }

    private static void InitStyles()
    {
        if (badgeStyle == null)
        {
            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                richText = true
            };
            badgeStyle.normal.textColor = Color.white;
        }

        if (bgTexture == null)
        {
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, Color.white);
            bgTexture.Apply();
        }
    }

    private static string CleanObjectName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "Objeto";
        string clean = rawName.Replace("(Clone)", "").Trim();
        if (clean.ToLower().Contains("linterna") || clean.ToLower().Contains("flashlight")) return "Linterna";
        if (clean.ToLower().Contains("sonoro") || clean.ToLower().Contains("sound") || clean.ToLower().Contains("radio")) return "Objeto Sonoro";
        return clean;
    }
}
