using UnityEngine;

public static class SceneSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void SanitizeActiveScene()
    {
        EliminarPilares();
        AsegurarBoxCollidersEnMuros();
        AsegurarMeshCollidersEnDinosaurios();
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
}
