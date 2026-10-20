using UnityEngine;

/// <summary>
/// Mantenimiento y saneamiento automático del escenario al iniciar la partida:
/// 1. Elimina todos los pilares dispersos por el mapa (EndPost / Post).
/// 2. Garantiza que absolutamente todos los muros posean un BoxCollider para que los dinosaurios no los atraviesen.
/// 3. Garantiza que todos los dinosaurios posean su MeshCollider convexo correctamente configurado.
/// </summary>
public static class SceneSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void SanitizeActiveScene()
    {
        // 1. Eliminar cualquier pilar disperso por el mapa
        EliminarPilares();

        // 2. Garantizar BoxCollider en todos los muros del escenario
        AsegurarBoxCollidersEnMuros();

        // 3. Garantizar MeshCollider en todos los dinosaurios
        AsegurarMeshCollidersEnDinosaurios();
    }

    /// <summary>
    /// Desactiva y destruye cualquier pilar (EndPost / Post) presente en el escenario.
    /// </summary>
    public static void EliminarPilares()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int pilaresEliminados = 0;

        foreach (GameObject go in allObjects)
        {
            if (go == null) continue;

            string name = go.name;
            if (name.Contains("EndPost") || name.Equals("Post") || name.StartsWith("Post ") || name.Contains("Wall Posts"))
            {
                go.SetActive(false);
                Object.Destroy(go);
                pilaresEliminados++;
            }
        }

        if (pilaresEliminados > 0)
        {
            Debug.Log($"[SceneSanitizer] Se han eliminado {pilaresEliminados} pilares (EndPost / Post) del escenario.");
        }
    }

    /// <summary>
    /// Comprueba todos los muros del escenario y se asegura de que tengan un BoxCollider
    /// para evitar que los dinosaurios o el jugador los puedan atravesar.
    /// </summary>
    public static void AsegurarBoxCollidersEnMuros()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int murosAsegurados = 0;

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

            if (isWall)
            {
                Collider col = go.GetComponent<Collider>();
                if (col == null)
                {
                    Renderer rend = go.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        BoxCollider bc = go.AddComponent<BoxCollider>();
                        Bounds localBounds = rend.localBounds;
                        bc.center = localBounds.center;
                        bc.size = localBounds.size;
                        murosAsegurados++;
                    }
                }
            }
        }

        if (murosAsegurados > 0)
        {
            Debug.Log($"[SceneSanitizer] Se han agregado BoxColliders a {murosAsegurados} muros que no poseían colisionador.");
        }
    }

    /// <summary>
    /// Comprueba que todos los dinosaurios de la escena posean un MeshCollider convexo.
    /// </summary>
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
}
