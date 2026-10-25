using System.Collections.Generic;
using UnityEngine;

public static class SceneSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SubscribirEventosEscena()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        SanitizeActiveScene();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void SanitizeActiveScene()
    {
        AudioListener.pause = false;
        AudioListener.volume = 1f;
        FirstPersonLook.IsCameraLocked = false;

        AsegurarPosicionesObjetos();
        EliminarPilares();
        AsegurarBoxCollidersEnMuros();
        AsegurarPuntosDeEmboscada();
        AsegurarDinosaurioEmboscador();
        AsegurarMeshCollidersEnDinosaurios();
        ValidarArquetiposDinosaurios();
        AsegurarMaterialesDinosaurios();
        AsegurarPickupPrompts();
        AsegurarAudioListenerUnico();
        AsegurarEnemigosEnNodos();
        AsegurarMinimapa();
    }

    /// <summary>
    /// Configura y garantiza las posiciones exactas de spawn del mapa para:
    /// - Las 10 llaves distribuidas en el escenario.
    /// - La linterna.
    /// - El jugador.
    /// - El objeto sonoro.
    /// </summary>
    public static void AsegurarPosicionesObjetos()
    {
        // 1. Posición y velocidad del Jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = new Vector3(-113.2f, 0f, 33.3f);
            if (player.TryGetComponent<Rigidbody>(out var prb)) { prb.linearVelocity = Vector3.zero; prb.angularVelocity = Vector3.zero; }
            FirstPersonMovement fpm = player.GetComponent<FirstPersonMovement>();
            if (fpm != null)
            {
                fpm.speed = 8.5f;
                fpm.runSpeed = 13.5f;
                fpm.crouchSpeed = 4.2f;
                fpm.acceleration = 50f;
                fpm.deceleration = 45f;
            }
        }

        // 2. Posición, escala y modelo 3D de la Linterna
        Linterna linterna = Object.FindAnyObjectByType<Linterna>();
        if (linterna != null)
        {
            AsegurarModeloLinterna(linterna);
        }

        // 3. Posición del Objeto Sonoro
        GameObject soundObj = GameObject.Find("Object+TriggerSonoro") ?? GameObject.FindGameObjectWithTag("canPickUp");
        if (soundObj != null)
        {
            soundObj.transform.position = new Vector3(3.6f, 1.91f, -39f);
            if (soundObj.TryGetComponent<Rigidbody>(out var srb)) { srb.linearVelocity = Vector3.zero; srb.angularVelocity = Vector3.zero; }
            RastreoSonoro rs = soundObj.GetComponentInChildren<RastreoSonoro>() ?? soundObj.GetComponentInParent<RastreoSonoro>();
            if (rs != null) { rs.radioSonoro = 20f; }
        }

        // 4. Posiciones exactas y distribuidas de las 10 llaves por todo el mapa
        Vector3[] keyPositions = new Vector3[]
        {
            new Vector3(-129.5f, 0.6f, 22.0f),  // Key 0: Zona Inicio / Almacén Oeste
            new Vector3(-142.0f, 0.6f, -42.0f), // Key 1: Extremo Suroeste (callejón sin salida)
            new Vector3(-104.5f, 0.6f, 0.0f),   // Key 2: Territorio de la Manada de Raptors
            new Vector3(-110.0f, 0.6f, -48.0f), // Key 3: Pasillo Sur-Oeste
            new Vector3(-63.4f, 0.6f, -22.0f),  // Key 4: Pasillo Emboscador Oeste
            new Vector3(-30.5f, 0.6f, 15.0f),   // Key 5: Cruce Central Emboscada y Alarma
            new Vector3(3.6f, 0.6f, -46.0f),    // Key 6: Cámara Sur / Objeto Sonoro
            new Vector3(32.0f, 0.6f, -2.5f),    // Key 7: Pasillo Emboscada Este
            new Vector3(65.0f, 0.6f, -20.0f),   // Key 8: Sector Este (Dino Ciego)
            new Vector3(55.1f, 0.6f, 48.4f)     // Key 9: Sector Noreste / Pasillo Salida
        };

        string[] keyNames = new string[]
        {
            "Key", "Key (1)", "Key (2)", "Key (3)", "Key (4)",
            "Key (5)", "Key (6)", "Key (7)", "Key (8)", "Key (9)"
        };

        List<GameObject> remainingKeys = new List<GameObject>(GameObject.FindGameObjectsWithTag("Key"));

        for (int i = 0; i < keyPositions.Length; i++)
        {
            GameObject keyObj = GameObject.Find(keyNames[i]);
            if (keyObj == null)
            {
                keyObj = GameObject.Find(keyNames[i].ToLower());
            }
            if (keyObj == null && i > 0)
            {
                keyObj = GameObject.Find($"Key({i})") ?? GameObject.Find($"key({i})");
            }
            if (keyObj == null && remainingKeys.Count > 0)
            {
                keyObj = remainingKeys[0];
            }

            if (keyObj != null)
            {
                keyObj.transform.position = keyPositions[i];
                remainingKeys.Remove(keyObj);
            }
        }
    }

    public static void EliminarPilares()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null) continue;

            string name = go.name;
            if (name.Contains("EndPost") || name.Equals("Post") || name.StartsWith("Post ") || name.Contains("Wall Posts"))
            {
                go.SetActive(false);
                Object.Destroy(go);
            }
        }
    }

    public static void AsegurarBoxCollidersEnMuros()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null) continue;

            string name = go.name;
            bool isWall = name.Contains("Wall_") || name.StartsWith("Wall") || name.Contains("Pared");

            if (!isWall && go.transform.parent != null)
            {
                string parentName = go.transform.parent.name;
                if (parentName.Contains("Wall") || parentName.Contains("Pared") || parentName.Contains("Walls"))
                {
                    isWall = true;
                }
            }

            if (isWall && !go.TryGetComponent<Collider>(out _))
            {
                if (go.TryGetComponent<Renderer>(out var rend))
                {
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    Bounds localBounds = rend.localBounds;
                    bc.center = localBounds.center;
                    bc.size = localBounds.size;
                }
            }
        }
    }

    public static void AsegurarMeshCollidersEnDinosaurios()
    {
        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null) continue;

            MeshCollider mc = enemy.GetComponent<MeshCollider>() ?? enemy.GetComponentInChildren<MeshCollider>();
            if (mc == null)
            {
                mc = enemy.gameObject.AddComponent<MeshCollider>();
                mc.convex = true;

                MeshFilter mf = enemy.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
            else
            {
                mc.convex = true;
            }
        }
    }

    public static void ValidarArquetiposDinosaurios()
    {
        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null) continue;

            if (enemy is BlindEnemyAI blind)
            {
                blind.IsPrey = false;
                blind.dinoCiego = true;
                blind.isLeader = false;
                blind.leaderTransform = null;
                blind.packFollowers.Clear();
            }
            else if (enemy is SmallEnemyAI small)
            {
                small.IsPrey = true;
                small.dinoCiego = false;
                small.isLeader = false;
                small.leaderTransform = null;
                small.LeaderAI = null;
                small.packFollowers.Clear();
            }
            else if (enemy is AlarmEnemyAI alarm)
            {
                alarm.IsPrey = false;
                alarm.dinoCiego = false;
                alarm.isLeader = false;
                alarm.leaderTransform = null;
                alarm.packFollowers.Clear();
            }
            else if (enemy is AmbushEnemyAI || enemy.dinoEmboscador)
            {
                enemy.IsPrey = false;
                enemy.dinoEmboscador = true;
                enemy.dinoCiego = false;
                enemy.isLeader = false;
                enemy.leaderTransform = null;
                enemy.packFollowers.Clear();
            }
            else
            {
                if (enemy.IsPrey || enemy.gameObject.name.ToLower().Contains("presa"))
                {
                    enemy.IsPrey = true;
                    enemy.dinoCiego = false;
                    enemy.isLeader = false;
                    enemy.leaderTransform = null;
                    enemy.LeaderAI = null;
                    enemy.packFollowers.Clear();
                }
                else
                {
                    enemy.IsPrey = false;
                    enemy.dinoCiego = false;
                }
            }

            enemy.UpdateStaticCategory();
        }
    }

    private static Material matGrande;
    private static Material matRaptor;
    private static Material matAlarma;
    private static Material matCiego;
    private static Material matEmboscador;

    public static void AsegurarMaterialesDinosaurios()
    {
        if (matGrande == null) matGrande = Resources.Load<Material>("Materials/Dinos/M_Dino_Grande");
        if (matRaptor == null) matRaptor = Resources.Load<Material>("Materials/Dinos/M_Dino_Raptor");
        if (matAlarma == null) matAlarma = Resources.Load<Material>("Materials/Dinos/M_Dino_Alarma");
        if (matCiego == null) matCiego = Resources.Load<Material>("Materials/Dinos/M_Dino_Ciego");
        if (matEmboscador == null) matEmboscador = Resources.Load<Material>("Materials/Dinos/M_Dino_Emboscador");

        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null) continue;

            Material targetMat = matGrande;
            if (enemy is BlindEnemyAI) targetMat = matCiego;
            else if (enemy is AlarmEnemyAI) targetMat = matAlarma;
            else if (enemy is SmallEnemyAI || enemy.IsSmallDino || enemy.IsPrey) targetMat = matRaptor;
            else if (enemy is AmbushEnemyAI || enemy.dinoEmboscador) targetMat = matEmboscador;

            if (targetMat == null) continue;

            Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                if (rend == null) continue;

                string rName = rend.gameObject.name;
                if (rName.IndexOf("trigger", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rName.IndexOf("sound", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rName.IndexOf("sphere", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                Material[] mats = rend.sharedMaterials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != targetMat)
                    {
                        mats[m] = targetMat;
                        changed = true;
                    }
                }
                if (changed)
                {
                    rend.sharedMaterials = mats;
                }
            }
        }
    }

    public static void AsegurarPickupPrompts()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null) continue;

            bool isPickup = go.CompareTag("canPickUp") || go.GetComponent<Linterna>() != null || go.GetComponent<RastreoSonoro>() != null;
            if (isPickup)
            {
                if (go.CompareTag("Player") || go.GetComponent<Camera>() != null || go.GetComponent<FirstPersonMovement>() != null)
                {
                    continue;
                }

                if (!go.TryGetComponent<PickupPrompt>(out var prompt))
                {
                    prompt = go.AddComponent<PickupPrompt>();
                }

                if (go.GetComponent<Linterna>() != null)
                {
                    prompt.objectDisplayName = "Linterna";
                }
                else if (go.GetComponent<RastreoSonoro>() != null)
                {
                    prompt.objectDisplayName = "Objeto Sonoro";
                }
            }
        }
    }

    public static void AsegurarAudioListenerUnico()
    {
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera playerCam = Camera.main;
        if (playerCam == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCam = p.GetComponentInChildren<Camera>();
        }

        AudioListener primaryListener = (playerCam != null) ? playerCam.GetComponent<AudioListener>() : null;
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener al = listeners[i];
            if (al != null && primaryListener != null && al != primaryListener)
            {
                al.enabled = false;
            }
        }
    }

    public static void AsegurarEnemigosEnNodos()
    {
        EnemyMovement[] allMovements = Object.FindObjectsByType<EnemyMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allMovements.Length; i++)
        {
            EnemyMovement mov = allMovements[i];
            if (mov != null && mov.gameObject.activeInHierarchy)
            {
                EnemyAI ai = mov.GetComponent<EnemyAI>();
                if (ai != null && (ai.dinoEmboscador || ai is AmbushEnemyAI)) continue;
                mov.SnapToNode();
            }
        }
    }

    public static void AsegurarMinimapa()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
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

    /// <summary>
    /// Garantiza y fija de forma permanente las posiciones y orientaciones actuales de los puntos de emboscada.
    /// </summary>
    public static void AsegurarPuntosDeEmboscada()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene.Contains("Menu") || currentScene.Contains("Win") || currentScene.Contains("Lose")) return;

        // Posiciones fijas y permanentes de los puntos de emboscada actuales
        (string name, Vector3 pos, Vector3 euler)[] pointConfigs = new[]
        {
            ("AmbushPoint_PasilloOeste", new Vector3(-107.57f, 0f, 62.3899994f), new Vector3(0f, 180f, 0f)),
            ("AmbushPoint_CruceCentro", new Vector3(-30.5f, 0f, 23.5f), new Vector3(0f, 180f, 0f)),
            ("AmbushPoint_PasilloEste", new Vector3(25.1f, 0f, -2.5f), new Vector3(0f, -90f, 0f))
        };

        for (int i = 0; i < pointConfigs.Length; i++)
        {
            var cfg = pointConfigs[i];
            GameObject go = GameObject.Find(cfg.name);
            if (go == null)
            {
                go = new GameObject(cfg.name);
            }

            // Fijar permanentemente la posición y orientación del punto
            go.transform.position = cfg.pos;
            go.transform.rotation = Quaternion.Euler(cfg.euler);

            if (!go.TryGetComponent<AmbushPoint>(out _))
            {
                go.AddComponent<AmbushPoint>();
            }
        }
    }

    /// <summary>
    /// Garantiza la existencia, posición fija y configuración permanente del dinosaurio emboscador en la escena.
    /// </summary>
    public static void AsegurarDinosaurioEmboscador()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene.Contains("Menu") || currentScene.Contains("Win") || currentScene.Contains("Lose")) return;

        AmbushEnemyAI ambushDino = Object.FindAnyObjectByType<AmbushEnemyAI>(FindObjectsInactive.Include);
        EnemyAI anyAmbush = ambushDino;

        if (anyAmbush == null)
        {
            EnemyAI[] all = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && (all[i].dinoEmboscador || all[i].gameObject.name.ToLower().Contains("emboscad")))
                {
                    anyAmbush = all[i];
                    break;
                }
            }
        }

        AmbushPoint primaryPoint = GameObject.Find("AmbushPoint_PasilloOeste")?.GetComponent<AmbushPoint>() ??
                                   AmbushPoint.GetNearest(new Vector3(-107.57f, 0f, 62.3899994f));

        if (anyAmbush == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/DInoEmboscador");
            if (prefab != null)
            {
                GameObject spawned = Object.Instantiate(prefab);
                spawned.name = "DinoEmboscador";
                anyAmbush = spawned.GetComponent<EnemyAI>();
            }
        }

        if (anyAmbush != null)
        {
            anyAmbush.dinoEmboscador = true;
            anyAmbush.IsPrey = false;
            anyAmbush.isLeader = false;
            anyAmbush.leaderTransform = null;
            anyAmbush.packFollowers.Clear();

            Vector3 fixedPos = new Vector3(-107.57f, 0f, 62.3899994f);
            Quaternion fixedRot = Quaternion.Euler(0f, 180f, 0f);

            anyAmbush.transform.position = fixedPos;
            anyAmbush.transform.rotation = fixedRot;
            anyAmbush.AmbushHomePos = fixedPos;
            anyAmbush.AmbushHomeRot = fixedRot;

            if (primaryPoint != null)
            {
                primaryPoint.assignedEnemy = anyAmbush;
            }

            if (anyAmbush is AmbushEnemyAI aAI)
            {
                aAI.assignedAmbushPoint = primaryPoint;
            }
        }
    }

    public static void AsegurarModeloLinterna(Linterna linterna)
    {
        if (linterna == null) return;

        MeshFilter mf = linterna.GetComponent<MeshFilter>();
        if (mf == null) mf = linterna.gameObject.AddComponent<MeshFilter>();

        MeshRenderer mr = linterna.GetComponent<MeshRenderer>();
        if (mr == null) mr = linterna.gameObject.AddComponent<MeshRenderer>();

        // Si la malla no está asignada o tiene 0 vértices, generar una malla 3D procedural realista
        if (mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0)
        {
            mf.sharedMesh = GenerarMallaLinterna();
        }

        // Asegurar material adecuado si falta
        if (mr.sharedMaterial == null || mr.sharedMaterial.shader == null || mr.sharedMaterial.name.Contains("Default"))
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (litShader != null)
            {
                Material mat = new Material(litShader);
                mat.name = "M_Linterna_Metalica";
                mat.color = new Color(0.2f, 0.22f, 0.25f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.2f, 0.22f, 0.25f));
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.85f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
                mr.sharedMaterial = mat;
            }
        }

        // Posición spawn, escala y collider
        linterna.transform.position = new Vector3(-125.6f, 2.1f, 32.8f);
        linterna.transform.rotation = Quaternion.identity;
        linterna.transform.localScale = Vector3.one;
        linterna.encendida = false;
        if (linterna.luz != null)
        {
            linterna.luz.enabled = false;
            linterna.luz.transform.position = linterna.transform.position + linterna.transform.forward * 0.15f;
            linterna.luz.transform.rotation = linterna.transform.rotation;
        }
        linterna.gameObject.layer = 0;

        if (linterna.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        AudioSource asrc = linterna.GetComponent<AudioSource>();
        if (asrc == null)
        {
            asrc = linterna.gameObject.AddComponent<AudioSource>();
            asrc.spatialBlend = 1.0f;
            asrc.minDistance = 2f;
            asrc.maxDistance = 25f;
            asrc.rolloffMode = AudioRolloffMode.Linear;
            asrc.playOnAwake = false;
        }

        BoxCollider bc = linterna.GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.size = new Vector3(0.15f, 0.15f, 0.46f);
            bc.center = Vector3.zero;
        }

        // Asegurar origen de emisión
        if (linterna.origen == null)
        {
            linterna.origen = linterna.transform;
        }
    }

    public static Mesh GenerarMallaLinterna()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Linterna_Procedural_Mesh";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        int segments = 16;
        float[] zRings = new float[] { -0.18f, -0.10f, 0.02f, 0.06f, 0.13f, 0.14f };
        float[] rRings = new float[] {  0.025f, 0.025f, 0.025f, 0.045f, 0.045f, 0.040f };

        // Anillos del cuerpo (mango, cuello cónico y cabezal con bisel)
        for (int ring = 0; ring < zRings.Length; ring++)
        {
            float z = zRings[ring];
            float r = rRings[ring];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos * r, sin * r, z));
                normals.Add(new Vector3(cos, sin, 0f).normalized);
                uvs.Add(new Vector2((float)i / segments, (float)ring / (zRings.Length - 1)));
            }
        }

        int ringStride = segments + 1;
        for (int ring = 0; ring < zRings.Length - 1; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                int current = ring * ringStride + i;
                int next = current + ringStride;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);

                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        // Tapa trasera (end cap en z = -0.18f)
        int backCenterIdx = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, -0.18f));
        normals.Add(Vector3.back);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int backRingStart = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(cos * 0.025f, sin * 0.025f, -0.18f));
            normals.Add(Vector3.back);
            uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles.Add(backCenterIdx);
            triangles.Add(backRingStart + i + 1);
            triangles.Add(backRingStart + i);
        }

        // Lente frontal (glass lens en z = 0.14f)
        int frontCenterIdx = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, 0.14f));
        normals.Add(Vector3.forward);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int frontRingStart = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(cos * 0.040f, sin * 0.040f, 0.14f));
            normals.Add(Vector3.forward);
            uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles.Add(frontCenterIdx);
            triangles.Add(frontRingStart + i);
            triangles.Add(frontRingStart + i + 1);
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }
}
