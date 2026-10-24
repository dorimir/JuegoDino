using UnityEngine;

public static class SceneSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void SanitizeActiveScene()
    {
        EliminarPilares();
        AsegurarBoxCollidersEnMuros();
        AsegurarMeshCollidersEnDinosaurios();
        ValidarArquetiposDinosaurios();
        AsegurarPickupPrompts();
        AsegurarPosicionLinterna();
        AsegurarAudioListenerUnico();
        AsegurarEnemigosEnNodos();
    }

    public static void EliminarPilares()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
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
        foreach (GameObject go in allObjects)
        {
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

            if (isWall && go.GetComponent<Collider>() == null)
            {
                Renderer rend = go.GetComponent<Renderer>();
                if (rend != null)
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
        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null) continue;

            MeshCollider mc = enemy.GetComponent<MeshCollider>();
            if (mc == null) mc = enemy.GetComponentInChildren<MeshCollider>();

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
        foreach (EnemyAI enemy in enemies)
        {
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
            }
            else if (enemy is AlarmEnemyAI alarm)
            {
                alarm.IsPrey = false;
                alarm.dinoCiego = false;
                alarm.isLeader = false;
                alarm.leaderTransform = null;
                alarm.packFollowers.Clear();
            }
            else
            {
                // Dinosaurio grande básico (EnemyAI)
                enemy.IsPrey = false;
                enemy.dinoCiego = false;
            }

            enemy.UpdateStaticCategory();
        }
    }

    public static void AsegurarPickupPrompts()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (go == null) continue;

            bool isPickup = go.CompareTag("canPickUp") || go.GetComponent<Linterna>() != null || go.GetComponent<RastreoSonoro>() != null;
            if (isPickup)
            {
                // Ignorar jugador, cámaras o controladores
                if (go.CompareTag("Player") || go.GetComponent<Camera>() != null || go.GetComponent<FirstPersonMovement>() != null)
                    continue;

                PickupPrompt prompt = go.GetComponent<PickupPrompt>();
                if (prompt == null)
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

    public static void AsegurarPosicionLinterna()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Linterna linterna = Object.FindAnyObjectByType<Linterna>();
        if (player != null && linterna != null)
        {
            Vector3 sideOffset = player.transform.right * 0.75f + player.transform.forward * 0.35f;
            Vector3 spawnPos = player.transform.position + sideOffset;
            spawnPos.y = player.transform.position.y + 0.12f;

            if (Physics.Raycast(spawnPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, ~((1 << 2) | (1 << 3)), QueryTriggerInteraction.Ignore))
            {
                spawnPos.y = hit.point.y + 0.12f;
            }

            linterna.transform.position = spawnPos;
            linterna.transform.rotation = Quaternion.identity;
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
            if (al == null) continue;

            if (primaryListener != null && al != primaryListener)
            {
                al.enabled = false;
            }
        }
    }

    public static void AsegurarEnemigosEnNodos()
    {
        EnemyMovement[] allMovements = Object.FindObjectsByType<EnemyMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EnemyMovement mov in allMovements)
        {
            if (mov != null && mov.gameObject.activeInHierarchy)
            {
                mov.SnapToNode();
            }
        }
    }
}
