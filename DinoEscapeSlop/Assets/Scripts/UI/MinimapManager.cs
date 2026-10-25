using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance { get; private set; }

    [Header("Posición y Dimensiones")]

    public float margin = 16f;

    public float mapSize = 250f;

    [Header("Configuración de Zoom y Modos")]

    public float tacticalZoomSize = 34f;

    public float globalOverviewSize = 85f;

    public float cameraHeight = 65f;

    public KeyCode toggleModeKey = KeyCode.M;

    public KeyCode zoomInKey = KeyCode.Equals;

    public KeyCode zoomOutKey = KeyCode.Minus;

    [Header("Elementos Visuales")]
    public bool showVisionCones = true;
    public bool showStateLabels = true;
    public bool showKeys = true;
    public bool showExitDoor = true;
    public bool showSoundObject = true;
    public bool showStatusSummary = true;

    [Header("Colores de Estados de Dinosaurios")]
    public Color patrolColor = new Color(0.2f, 0.9f, 0.35f, 1f);
    public Color packColor = new Color(0.2f, 0.85f, 0.8f, 1f);
    public Color alertColor = new Color(1f, 0.85f, 0.1f, 1f);
    public Color stalkColor = new Color(1f, 0.55f, 0.05f, 1f);
    public Color chaseColor = new Color(1f, 0.15f, 0.15f, 1f);
    public Color alarmColor = new Color(1f, 0.1f, 0.85f, 1f);
    public Color fleeColor = new Color(0.25f, 0.65f, 1f, 1f);
    public Color hiddenColor = new Color(0.7f, 0.35f, 0.95f, 1f);

    public enum DinoStateCategory
    {
        Patrol,
        PackFollow,
        Investigate,
        Stalk,
        Hidden,
        Chase,
        Alarm,
        Flee,
        Recover
    }

    private Camera minimapCamera;
    private RenderTexture minimapTexture;
    private GameObject minimapCamObject;
    private Transform playerTransform;

    private bool isTacticalZoom = true;
    private float currentOrthoSize;
    private Vector3 currentCamPos;
    private readonly Vector3 globalCenter = new Vector3(-30f, 65f, 15f);

    private readonly Dictionary<EnemyAI, DinoStateCategory> lastDinoStates = new Dictionary<EnemyAI, DinoStateCategory>();
    private readonly Dictionary<EnemyAI, float> stateChangeTimes = new Dictionary<EnemyAI, float>();

    private Texture2D circleTexture;
    private Texture2D arrowTexture;
    private Texture2D ringTexture;
    private Texture2D borderTexture;
    private Texture2D darkBgTexture;

    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle summaryStyle;
    private GUIStyle buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SubscribirEscenaMinimapa()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.Contains("Menu") || scene.name.Contains("Win") || scene.name.Contains("Lose"))
        {
            return;
        }

        MinimapManager existing = Object.FindAnyObjectByType<MinimapManager>();
        if (existing == null)
        {
            GameObject managerObj = new GameObject("MinimapManager");
            managerObj.AddComponent<MinimapManager>();
        }
        else
        {
            existing.ResetMinimap();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitMinimap()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene.Contains("Menu") || currentScene.Contains("Win") || currentScene.Contains("Lose"))
        {
            return;
        }

        if (Object.FindAnyObjectByType<MinimapManager>() == null)
        {
            GameObject managerObj = new GameObject("MinimapManager");
            managerObj.AddComponent<MinimapManager>();
        }
    }

    public void ResetMinimap()
    {
        isTacticalZoom = true;
        currentOrthoSize = tacticalZoomSize;
        lastDinoStates.Clear();
        stateChangeTimes.Clear();
        FindPlayer();
        if (minimapCamera != null)
        {
            minimapCamera.orthographicSize = currentOrthoSize;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currentOrthoSize = tacticalZoomSize;
        GenerateProceduralTextures();
        InitializeCamera();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (minimapTexture != null)
        {
            if (minimapCamera != null)
            {
                minimapCamera.targetTexture = null;
            }
            minimapTexture.Release();
            Destroy(minimapTexture);
        }

        if (minimapCamObject != null)
        {
            Destroy(minimapCamObject);
        }

        if (circleTexture != null) Destroy(circleTexture);
        if (arrowTexture != null) Destroy(arrowTexture);
        if (ringTexture != null) Destroy(ringTexture);
        if (borderTexture != null) Destroy(borderTexture);
        if (darkBgTexture != null) Destroy(darkBgTexture);
    }

    private void InitializeCamera()
    {
        minimapCamObject = new GameObject("Minimap_TopDownCamera");
        minimapCamObject.transform.SetParent(transform);

        minimapCamera = minimapCamObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = currentOrthoSize;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        minimapCamera.nearClipPlane = 0.5f;
        minimapCamera.farClipPlane = 150f;

        minimapCamera.cullingMask = ~((1 << 5) | (1 << 6));
        minimapCamera.depth = -10;

        minimapTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 2
        };
        minimapTexture.Create();

        minimapCamera.targetTexture = minimapTexture;

        minimapCamObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        currentCamPos = globalCenter;
        minimapCamObject.transform.position = currentCamPos;
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            currentCamPos = new Vector3(playerTransform.position.x, cameraHeight, playerTransform.position.z);
            if (minimapCamObject != null)
            {
                minimapCamObject.transform.position = currentCamPos;
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
        }

        if (Input.GetKeyDown(toggleModeKey))
        {
            isTacticalZoom = !isTacticalZoom;
        }

        if (isTacticalZoom)
        {
            if (Input.GetKey(zoomInKey) || Input.GetKey(KeyCode.KeypadPlus))
            {
                tacticalZoomSize = Mathf.Clamp(tacticalZoomSize - 18f * Time.deltaTime, 15f, 65f);
            }
            if (Input.GetKey(zoomOutKey) || Input.GetKey(KeyCode.KeypadMinus))
            {
                tacticalZoomSize = Mathf.Clamp(tacticalZoomSize + 18f * Time.deltaTime, 15f, 65f);
            }
        }

        UpdateCameraTransform();
        UpdateStateTracking();
    }

    private void UpdateCameraTransform()
    {
        if (minimapCamera == null) return;

        float targetOrtho = isTacticalZoom ? tacticalZoomSize : globalOverviewSize;
        currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrtho, Time.deltaTime * 7f);
        minimapCamera.orthographicSize = currentOrthoSize;

        Vector3 targetPos = (isTacticalZoom && playerTransform != null)
            ? new Vector3(playerTransform.position.x, cameraHeight, playerTransform.position.z)
            : new Vector3(globalCenter.x, cameraHeight, globalCenter.z);

        currentCamPos = Vector3.Lerp(currentCamPos, targetPos, Time.deltaTime * 8f);
        minimapCamObject.transform.position = currentCamPos;
    }

    private void UpdateStateTracking()
    {
        if (EnemyAI.AllEnemies == null) return;

        float now = Time.time;
        for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
        {
            EnemyAI enemy = EnemyAI.AllEnemies[i];
            if (enemy == null) continue;

            DinoStateCategory currentCategory = ClassifyState(enemy);

            if (!lastDinoStates.TryGetValue(enemy, out DinoStateCategory prevCategory))
            {
                lastDinoStates[enemy] = currentCategory;
            }
            else if (prevCategory != currentCategory)
            {
                lastDinoStates[enemy] = currentCategory;
                stateChangeTimes[enemy] = now;
            }
        }
    }

    private DinoStateCategory ClassifyState(EnemyAI enemy)
    {
        if (enemy == null || enemy.StateMachine == null) return DinoStateCategory.Patrol;

        IState cur = enemy.StateMachine.CurrentState;

        if (cur is AlarmState) return DinoStateCategory.Alarm;
        if (cur is ChaseState) return DinoStateCategory.Chase;
        if (cur is FleeState) return DinoStateCategory.Flee;
        if (cur is AmbushStalkState) return DinoStateCategory.Stalk;
        if (cur is AmbushHiddenState) return DinoStateCategory.Hidden;
        if (cur is AmbushRecoverState) return DinoStateCategory.Recover;
        if (cur is BlindInvestigateState) return DinoStateCategory.Investigate;
        if (cur is FollowLeaderState) return DinoStateCategory.PackFollow;

        return DinoStateCategory.Patrol;
    }

    public void GetStateDisplayInfo(EnemyAI enemy, out string stateName, out Color stateColor)
    {
        DinoStateCategory category = ClassifyState(enemy);

        switch (category)
        {
            case DinoStateCategory.Alarm:
                stateName = "¡ALARMA!";
                stateColor = alarmColor;
                break;
            case DinoStateCategory.Chase:
                stateName = "CAZA";
                stateColor = chaseColor;
                break;
            case DinoStateCategory.Flee:
                stateName = "HUIDA";
                stateColor = fleeColor;
                break;
            case DinoStateCategory.Stalk:
                stateName = "ACECHO";
                stateColor = stalkColor;
                break;
            case DinoStateCategory.Hidden:
                stateName = "OCULTO";
                stateColor = hiddenColor;
                break;
            case DinoStateCategory.Recover:
                stateName = "CANSADO";
                stateColor = new Color(0.6f, 0.6f, 0.7f);
                break;
            case DinoStateCategory.Investigate:
                stateName = "INVESTIGA";
                stateColor = alertColor;
                break;
            case DinoStateCategory.PackFollow:
                stateName = "MANADA";
                stateColor = packColor;
                break;
            default:
                stateName = "PATRULLA";
                stateColor = patrolColor;
                break;
        }
    }

    public static string GetArchetypeName(EnemyAI enemy)
    {
        if (enemy is AlarmEnemyAI) return "Avisador";
        if (enemy is BlindEnemyAI) return "Ciego";
        if (enemy is AmbushEnemyAI || enemy.dinoEmboscador) return "Emboscador";
        if (enemy.IsBigDino) return "T-Rex";
        if (enemy.IsMediumDino) return "Mediano";
        if (enemy.IsSmallDino) return "PequeÃ±o";
        return "Dino";
    }

    private void OnGUI()
    {
        if (EnemyAI.GlobalPlayerDead) return;

        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene.Contains("Menu") || activeScene.Contains("Win") || activeScene.Contains("Lose")) return;

        InitStyles();

        float topMargin = margin;
        float leftMargin = margin;

        Rect headerRect = new Rect(leftMargin, topMargin, mapSize, 24f);
        DrawHeader(headerRect);

        Rect mapRect = new Rect(leftMargin, topMargin + 26f, mapSize, mapSize);
        DrawMinimapViewport(mapRect);

        DrawExitDoor(mapRect);
        DrawKeys(mapRect);
        DrawSoundObject(mapRect);
        DrawDinosaurs(mapRect);
        DrawPlayerMarker(mapRect);

        if (showStatusSummary)
        {
            Rect footerRect = new Rect(leftMargin, mapRect.yMax + 4f, mapSize, 36f);
            DrawStatusFooter(footerRect);
        }
    }

    private void DrawHeader(Rect rect)
    {
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
        GUI.DrawTexture(rect, darkBgTexture, ScaleMode.StretchToFill, true, 4f);
        GUI.color = Color.white;

        string modeText = isTacticalZoom ? "TÁCTICO" : "GLOBAL";
        GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width - 90f, rect.height), $"<b>MAPA</b> <color=#00E5FF>[{modeText}]</color>", headerStyle);

        Rect btnRect = new Rect(rect.xMax - 82f, rect.y + 2f, 76f, rect.height - 4f);
        string btnLabel = isTacticalZoom ? "[M] Global" : "[M] Zoom";
        if (GUI.Button(btnRect, btnLabel, buttonStyle))
        {
            isTacticalZoom = !isTacticalZoom;
        }
    }

    private void DrawMinimapViewport(Rect mapRect)
    {

        GUI.color = new Color(0.02f, 0.03f, 0.05f, 1f);
        GUI.DrawTexture(new Rect(mapRect.x - 2f, mapRect.y - 2f, mapRect.width + 4f, mapRect.height + 4f), darkBgTexture);

        if (minimapTexture != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(mapRect, minimapTexture, ScaleMode.StretchToFill);
        }

        GUI.color = new Color(0.15f, 0.65f, 0.95f, 0.65f);
        GUI.DrawTexture(mapRect, borderTexture);

        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        float midX = mapRect.x + mapRect.width * 0.5f;
        float midY = mapRect.y + mapRect.height * 0.5f;
        GUI.DrawTexture(new Rect(midX - 0.5f, mapRect.y, 1f, mapRect.height), darkBgTexture);
        GUI.DrawTexture(new Rect(mapRect.x, midY - 0.5f, mapRect.width, 1f), darkBgTexture);
        GUI.color = Color.white;
    }

    private Vector2? WorldToMinimapPoint(Vector3 worldPos, Rect mapRect)
    {
        if (minimapCamera == null) return null;

        Vector3 vp = minimapCamera.WorldToViewportPoint(worldPos);
        if (vp.z <= 0f) return null;

        float px = mapRect.x + vp.x * mapRect.width;
        float py = mapRect.y + (1f - vp.y) * mapRect.height;

        if (px >= mapRect.x - 6f && px <= mapRect.xMax + 6f && py >= mapRect.y - 6f && py <= mapRect.yMax + 6f)
        {
            return new Vector2(px, py);
        }

        return null;
    }

    private void DrawExitDoor(Rect mapRect)
    {
        if (!showExitDoor) return;

        Vector3 doorWorldPos = new Vector3(10.67f, 0.027f, 77.98f);
        GameObject doorObj = GameObject.Find("ExitDoor") ?? GameObject.Find("Door");
        if (doorObj != null) doorWorldPos = doorObj.transform.position;

        Vector2? screenPos = WorldToMinimapPoint(doorWorldPos, mapRect);
        if (!screenPos.HasValue) return;

        Vector2 pt = screenPos.Value;
        float iconSize = 14f;
        Rect iconRect = new Rect(pt.x - iconSize * 0.5f, pt.y - iconSize * 0.5f, iconSize, iconSize);

        GUI.color = new Color(0.2f, 1f, 0.7f, 0.95f);
        GUI.DrawTexture(iconRect, circleTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(pt.x - 30f, pt.y - 18f, 60f, 16f), "SALIDA", labelStyle);
    }

    private void DrawKeys(Rect mapRect)
    {
        if (!showKeys) return;

        GameObject[] keys = GameObject.FindGameObjectsWithTag("Key");
        if (keys == null || keys.Length == 0) return;

        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == null) continue;

            Vector2? pos = WorldToMinimapPoint(keys[i].transform.position, mapRect);
            if (!pos.HasValue) continue;

            Vector2 pt = pos.Value;
            float kSize = 7f;
            Rect kRect = new Rect(pt.x - kSize * 0.5f, pt.y - kSize * 0.5f, kSize, kSize);

            GUI.color = new Color(1f, 0.88f, 0.2f, 0.95f);
            GUI.DrawTexture(kRect, circleTexture);
        }
        GUI.color = Color.white;
    }

    private void DrawSoundObject(Rect mapRect)
    {
        if (!showSoundObject) return;

        GameObject soundObj = GameObject.Find("Object+TriggerSonoro") ?? GameObject.FindGameObjectWithTag("canPickUp");
        if (soundObj == null) return;

        Vector2? pos = WorldToMinimapPoint(soundObj.transform.position, mapRect);
        if (!pos.HasValue) return;

        Vector2 pt = pos.Value;
        float sSize = 10f;
        Rect sRect = new Rect(pt.x - sSize * 0.5f, pt.y - sSize * 0.5f, sSize, sSize);

        GUI.color = new Color(1f, 0.55f, 0.1f, 0.95f);
        GUI.DrawTexture(sRect, circleTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(pt.x - 25f, pt.y - 16f, 50f, 16f), "SONORO", labelStyle);
    }

    private void DrawDinosaurs(Rect mapRect)
    {
        if (EnemyAI.AllEnemies == null || EnemyAI.AllEnemies.Count == 0) return;

        float now = Time.time;
        float pixelsPerWorldUnit = mapSize / (minimapCamera != null ? minimapCamera.orthographicSize * 2f : 70f);

        for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
        {
            EnemyAI enemy = EnemyAI.AllEnemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            Vector2? point = WorldToMinimapPoint(enemy.transform.position, mapRect);
            if (!point.HasValue) continue;

            Vector2 screenPos = point.Value;
            GetStateDisplayInfo(enemy, out string stateName, out Color stateColor);
            string archetype = GetArchetypeName(enemy);

            float forwardAngle = enemy.transform.eulerAngles.y;

            if (showVisionCones)
            {
                if (enemy is BlindEnemyAI blind)
                {

                    float hearingRadiusPx = blind.walkNoiseRadius * pixelsPerWorldUnit * 0.65f;
                    DrawHollowRing(screenPos, hearingRadiusPx, new Color(stateColor.r, stateColor.g, stateColor.b, 0.22f));
                }
                else
                {
                    float visionDistPx = Mathf.Clamp(enemy.visionRange * pixelsPerWorldUnit, 10f, 55f);
                    float coneAngle = enemy.GetVisionOrFacingCone();
                    DrawVisionArc(screenPos, forwardAngle, coneAngle, visionDistPx, new Color(stateColor.r, stateColor.g, stateColor.b, 0.24f));
                }
            }

            if (stateChangeTimes.TryGetValue(enemy, out float changeTime))
            {
                float timeSinceChange = now - changeTime;
                if (timeSinceChange < 1.6f)
                {
                    float progress = timeSinceChange / 1.6f;
                    float pulseRadius = Mathf.Lerp(8f, 26f, progress);
                    float pulseAlpha = (1f - progress) * 0.85f;
                    DrawHollowRing(screenPos, pulseRadius, new Color(stateColor.r, stateColor.g, stateColor.b, pulseAlpha));
                }
            }

            float markerSize = enemy.IsSmallDino ? 11f : (enemy.IsBigDino ? 21f : 15f);
            Rect markerRect = new Rect(screenPos.x - markerSize * 0.5f, screenPos.y - markerSize * 0.5f, markerSize, markerSize);

            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(markerRect.x - 1.5f, markerRect.y - 1.5f, markerRect.width + 3f, markerRect.height + 3f), circleTexture);

            GUI.color = stateColor;
            GUI.DrawTexture(markerRect, circleTexture);

            Matrix4x4 oldMat = GUI.matrix;
            GUIUtility.RotateAroundPivot(forwardAngle, screenPos);
            GUI.color = Color.white;
            float arrowSize = markerSize * 0.85f;
            GUI.DrawTexture(new Rect(screenPos.x - arrowSize * 0.5f, screenPos.y - arrowSize * 0.6f, arrowSize, arrowSize), arrowTexture);
            GUI.matrix = oldMat;

            if (showStateLabels)
            {
                string labelText = $"{archetype}\n<color=#{ColorUtility.ToHtmlStringRGB(stateColor)}>[{stateName}]</color>";
                Rect labelRect = new Rect(screenPos.x - 45f, screenPos.y - 28f, 90f, 24f);
                GUI.Label(labelRect, labelText, labelStyle);
            }
        }
        GUI.color = Color.white;
    }

    private void DrawPlayerMarker(Rect mapRect)
    {
        if (playerTransform == null) return;

        Vector2? pos = WorldToMinimapPoint(playerTransform.position, mapRect);
        if (!pos.HasValue) return;

        Vector2 pt = pos.Value;
        float markerSize = 16f;

        if (Linterna.Encendidas != null && Linterna.Encendidas.Count > 0)
        {
            float camAngle = Camera.main != null ? Camera.main.transform.eulerAngles.y : playerTransform.eulerAngles.y;
            DrawVisionArc(pt, camAngle, 55f, 42f, new Color(1f, 0.95f, 0.35f, 0.30f));
        }

        GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.95f);
        GUI.DrawTexture(new Rect(pt.x - markerSize * 0.5f - 2f, pt.y - markerSize * 0.5f - 2f, markerSize + 4f, markerSize + 4f), circleTexture);

        GUI.color = new Color(0.1f, 1f, 0.6f, 1f);
        GUI.DrawTexture(new Rect(pt.x - markerSize * 0.5f, pt.y - markerSize * 0.5f, markerSize, markerSize), circleTexture);

        float playerAngle = Camera.main != null ? Camera.main.transform.eulerAngles.y : playerTransform.eulerAngles.y;
        Matrix4x4 oldMat = GUI.matrix;
        GUIUtility.RotateAroundPivot(playerAngle, pt);
        GUI.color = Color.white;
        float arrSize = markerSize * 0.9f;
        GUI.DrawTexture(new Rect(pt.x - arrSize * 0.5f, pt.y - arrSize * 0.65f, arrSize, arrSize), arrowTexture);
        GUI.matrix = oldMat;

        GUI.color = Color.white;
        GUI.Label(new Rect(pt.x - 20f, pt.y + 9f, 40f, 14f), "<b>TÚ</b>", labelStyle);
    }

    private void DrawStatusFooter(Rect rect)
    {
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
        GUI.DrawTexture(rect, darkBgTexture, ScaleMode.StretchToFill, true, 4f);
        GUI.color = Color.white;

        int total = 0;
        int chaseCount = 0;
        int alertCount = 0;
        int patrolCount = 0;

        if (EnemyAI.AllEnemies != null)
        {
            for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
            {
                EnemyAI e = EnemyAI.AllEnemies[i];
                if (e == null || !e.gameObject.activeInHierarchy) continue;

                total++;
                DinoStateCategory cat = ClassifyState(e);
                if (cat == DinoStateCategory.Chase || cat == DinoStateCategory.Alarm) chaseCount++;
                else if (cat == DinoStateCategory.Investigate || cat == DinoStateCategory.Stalk) alertCount++;
                else patrolCount++;
            }
        }

        string text = $"Dinos: <b>{total}</b>  |  <color=#33E566>Patrulla: {patrolCount}</color>  |  <color=#FFD500>Alerta: {alertCount}</color>  |  <color=#FF3333>Caza: {chaseCount}</color>";
        GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 16f), text, summaryStyle);

        string hint = "<color=#AAAAAA>[M] Alternar modo  |  [+/-] Zoom táctico</color>";
        GUI.Label(new Rect(rect.x + 6f, rect.y + 18f, rect.width - 12f, 14f), hint, summaryStyle);
    }

    private void DrawHollowRing(Vector2 center, float radius, Color color)
    {
        GUI.color = color;
        float diam = radius * 2f;
        Rect r = new Rect(center.x - radius, center.y - radius, diam, diam);
        GUI.DrawTexture(r, ringTexture);
        GUI.color = Color.white;
    }

    private void DrawVisionArc(Vector2 center, float forwardAngle, float coneAngle, float length, Color color)
    {
        int segments = 10;
        float halfAngle = coneAngle * 0.5f;
        float startAngle = forwardAngle - halfAngle;
        float step = coneAngle / segments;

        Vector2 prevEdge = center + AngleToVector(startAngle) * length;

        for (int i = 1; i <= segments; i++)
        {
            float curAngle = startAngle + i * step;
            Vector2 nextEdge = center + AngleToVector(curAngle) * length;

            DrawLine(center, nextEdge, color, 1f);
            DrawLine(prevEdge, nextEdge, color, 1.2f);
            prevEdge = nextEdge;
        }

        Vector2 firstEdge = center + AngleToVector(startAngle) * length;
        DrawLine(center, firstEdge, color, 1f);
    }

    private static Vector2 AngleToVector(float degrees)
    {
        float rad = (degrees - 90f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void DrawLine(Vector2 p1, Vector2 p2, Color color, float width)
    {
        Color old = GUI.color;
        GUI.color = color;

        Vector2 diff = p2 - p1;
        float length = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        Matrix4x4 oldMat = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y - width * 0.5f, length, width), darkBgTexture);
        GUI.matrix = oldMat;

        GUI.color = old;
    }

    private void GenerateProceduralTextures()
    {
        circleTexture = CreateCircleTexture(32);
        arrowTexture = CreateArrowTexture(32);
        ringTexture = CreateRingTexture(48, 0.20f);
        borderTexture = CreateBorderTexture(256, 2);
        darkBgTexture = new Texture2D(1, 1);
        darkBgTexture.SetPixel(0, 0, Color.white);
        darkBgTexture.Apply();
    }

    private static Texture2D CreateCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((center - dist) + 0.6f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateRingTexture(int size, float thicknessRatio)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float innerRadius = center * (1f - thicknessRatio);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alphaOuter = Mathf.Clamp01((center - dist) + 0.6f);
                float alphaInner = Mathf.Clamp01((dist - innerRadius) + 0.6f);
                float alpha = Mathf.Min(alphaOuter, alphaInner);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateArrowTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float normY = (float)y / (size - 1);
            float halfWidth = (1f - normY) * (size * 0.45f);
            float centerX = (size - 1) * 0.5f;

            for (int x = 0; x < size; x++)
            {
                float diffX = Mathf.Abs(x - centerX);
                float alpha = (diffX <= halfWidth && normY >= 0.12f && normY <= 0.95f) ? 1f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateBorderTexture(int size, int thickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = (x < thickness || x >= size - thickness || y < thickness || y >= size - thickness);
                pixels[y * size + x] = isBorder ? Color.white : new Color(0, 0, 0, 0);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
            headerStyle.normal.textColor = Color.white;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            labelStyle.normal.textColor = Color.white;
        }

        if (summaryStyle == null)
        {
            summaryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            summaryStyle.normal.textColor = Color.white;
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
        }
    }
}
