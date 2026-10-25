using UnityEngine;

public static class EnemyCollisionHandler
{
    private const int WallMask = ~((1 << 3) | (1 << 2));

    public static void ConfigureColliders(GameObject gameObject, Transform transform, bool isSmall, bool isBig = false)
    {
        int dinoLayer = LayerMask.NameToLayer("Dinosaurios");
        if (dinoLayer != -1)
        {
            gameObject.layer = dinoLayer;
            foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = dinoLayer;
            }
        }

        BoxCollider box = gameObject.GetComponent<BoxCollider>();
        CapsuleCollider cap = gameObject.GetComponent<CapsuleCollider>();

        if (isSmall)
        {
            if (box != null)
            {
                Vector3 s = box.size;
                s.x = Mathf.Min(s.x, 0.35f);
                s.y = Mathf.Clamp(s.y, 0.4f, 0.7f);
                s.z = Mathf.Min(s.z, 0.5f);
                box.size = s;
                box.center = new Vector3(0, s.y * 0.5f, 0);
            }
            if (cap != null)
            {
                cap.radius = Mathf.Min(cap.radius, 0.2f);
                cap.height = Mathf.Min(cap.height, 0.6f);
            }
        }
        else if (isBig)
        {
            if (box != null)
            {
                Vector3 s = box.size;
                s.x = Mathf.Min(s.x, 0.95f);
                s.y = Mathf.Clamp(s.y, 1.2f, 2.0f);
                s.z = Mathf.Min(s.z, 1.3f);
                box.size = s;
                box.center = new Vector3(0, s.y * 0.5f, 0);
            }
            if (cap != null)
            {
                cap.radius = Mathf.Min(cap.radius, 0.55f);
                cap.height = Mathf.Min(cap.height, 1.7f);
            }
        }
        else
        {
            if (box != null)
            {
                Vector3 s = box.size;
                s.x = Mathf.Min(s.x, 0.6f);
                s.y = Mathf.Clamp(s.y, 0.8f, 1.2f);
                s.z = Mathf.Min(s.z, 0.8f);
                box.size = s;
                box.center = new Vector3(0, s.y * 0.5f, 0);
            }
            if (cap != null)
            {
                cap.radius = Mathf.Min(cap.radius, 0.35f);
            }
        }

        MeshCollider meshCol = gameObject.GetComponent<MeshCollider>();
        if (meshCol == null) meshCol = gameObject.GetComponentInChildren<MeshCollider>();
        if (meshCol == null)
        {
            meshCol = gameObject.AddComponent<MeshCollider>();
            meshCol.convex = true;
            MeshFilter mf = gameObject.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                meshCol.sharedMesh = mf.sharedMesh;
            }
        }
        else
        {
            meshCol.convex = true;
        }
    }

    public static void MoveSafely(EnemyAI ai, Transform transform, ref Vector3 currentVelocity, Vector3 displacement, float bodyRadius)
    {
        displacement.y = 0f;
        if (displacement.sqrMagnitude < 0.000001f) return;

        bool isSmall = (ai != null) ? ai.IsSmallDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) <= 1.4f);
        bool isBig = (ai != null) ? ai.IsBigDino : (Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) >= 2.5f);
        float moveDist = displacement.magnitude;
        Vector3 moveDir = displacement / moveDist;
        float rayHeight = isSmall ? 0.45f : (isBig ? 1.45f : 0.85f);
        Vector3 rayOrigin = transform.position + Vector3.up * rayHeight;

        if (Physics.SphereCast(rayOrigin, bodyRadius, moveDir, out RaycastHit hit, moveDist + 0.05f, WallMask, QueryTriggerInteraction.Ignore))
        {
            bool isEnemy = hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<EnemyAI>() != null;
            bool isPickup = hit.collider.isTrigger || hit.collider.CompareTag("canPickUp") || hit.collider.CompareTag("Key");
            if (hit.transform != transform && !hit.transform.IsChildOf(transform) && !isEnemy && !isPickup && hit.normal.y < 0.7f)
            {
                if (ai == null || ai.Player == null || (hit.transform != ai.Player && !hit.transform.IsChildOf(ai.Player)))
                {
                    Vector3 slideDir = Vector3.ProjectOnPlane(displacement, hit.normal);
                    slideDir.y = 0f;

                    if (slideDir.sqrMagnitude > 0.0001f && Physics.SphereCast(rayOrigin, bodyRadius, slideDir.normalized, out RaycastHit secondHit, slideDir.magnitude + 0.04f, WallMask, QueryTriggerInteraction.Ignore))
                    {
                        if (secondHit.collider != null && !secondHit.collider.isTrigger && !secondHit.collider.CompareTag("Enemy") && secondHit.normal.y < 0.7f)
                        {
                            slideDir = Vector3.ProjectOnPlane(slideDir, secondHit.normal);
                            slideDir.y = 0f;
                        }
                    }

                    displacement = slideDir;
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);
                }
            }
        }

        if (EnemyAI.AllEnemies != null)
        {
            for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
            {
                EnemyAI other = EnemyAI.AllEnemies[i];
                if (other == null || other == ai || !other.gameObject.activeInHierarchy) continue;

                Vector3 toOther = other.transform.position - transform.position;
                toOther.y = 0f;
                float dist = toOther.magnitude;
                float physicalContactDist = (ai != null && ai.IsSmallDino && other.IsSmallDino)
                    ? 0.38f
                    : ((ai != null && ai.IsBigDino) || other.IsBigDino ? 1.30f : 0.90f);

                if (dist < physicalContactDist && dist > 0.001f)
                {
                    Vector3 dirToOther = toOther / dist;
                    float approachComponent = Vector3.Dot(displacement, dirToOther);
                    if (approachComponent > 0f)
                    {
                        displacement -= dirToOther * approachComponent;
                    }
                }
            }
        }

        transform.position += displacement;

        Vector3 curOrigin = transform.position + Vector3.up * rayHeight;
        Collider[] nearbyWalls = Physics.OverlapSphere(curOrigin, bodyRadius, WallMask, QueryTriggerInteraction.Ignore);
        for (int w = 0; w < nearbyWalls.Length; w++)
        {
            Collider wallCol = nearbyWalls[w];
            if (wallCol.transform == transform || wallCol.transform.IsChildOf(transform)) continue;
            if (wallCol.isTrigger || wallCol.CompareTag("canPickUp") || wallCol.CompareTag("Key")) continue;
            if (wallCol.CompareTag("Enemy") || wallCol.GetComponentInParent<EnemyAI>() != null) continue;
            if (ai != null && ai.Player != null && (wallCol.transform == ai.Player || wallCol.transform.IsChildOf(ai.Player))) continue;

            string colName = wallCol.gameObject.name.ToLower();
            if (colName.Contains("plane") || colName.Contains("floor") || colName.Contains("suelo")) continue;

            Vector3 closest = wallCol.ClosestPoint(curOrigin);
            if (closest.y < transform.position.y + 0.15f) continue;

            Vector3 diff = curOrigin - closest;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist < bodyRadius && dist > 0.005f)
            {
                Vector3 pushOut = (diff / dist) * (bodyRadius - dist);
                transform.position += pushOut;
                curOrigin += pushOut;
            }
        }
    }

    public static void ResolveMutualCollisions(EnemyAI ai, Transform transform, int instanceID)
    {
        if (EnemyAI.AllEnemies == null || EnemyAI.AllEnemies.Count == 0) return;

        for (int i = 0; i < EnemyAI.AllEnemies.Count; i++)
        {
            EnemyAI other = EnemyAI.AllEnemies[i];
            if (other == null || other == ai || !other.gameObject.activeInHierarchy) continue;

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float requiredDist = (ai != null && ai.IsSmallDino && other.IsSmallDino)
                ? 0.36f
                : ((ai != null && ai.IsBigDino) || other.IsBigDino ? 1.30f : 0.85f);

            if (dist < requiredDist)
            {
                float overlap = requiredDist - dist;
                Vector3 pushDir = dist > 0.001f ? diff / dist : (instanceID > other.GetInstanceID() ? transform.right : -transform.right);

                float myWeight = 0.5f;
                if (ai != null)
                {
                    if (ai.IsBigDino && !other.IsBigDino) myWeight = 0.0f;
                    else if (!ai.IsBigDino && other.IsBigDino) myWeight = 1.0f;
                    else if (ai.isLeader && !other.isLeader) myWeight = 0.0f;
                    else if (!ai.isLeader && other.isLeader) myWeight = 1.0f;
                }

                Vector3 push = pushDir * (overlap * myWeight * 0.25f);
                if (push.sqrMagnitude > 0.00001f)
                {
                    float rayHeight = (ai != null && ai.IsSmallDino) ? 0.35f : ((ai != null && ai.IsBigDino) ? 1.35f : 0.75f);
                    Vector3 checkOrigin = transform.position + Vector3.up * rayHeight;
                    if (!Physics.SphereCast(checkOrigin, 0.2f, push.normalized, out RaycastHit wallHit, push.magnitude + 0.1f, WallMask, QueryTriggerInteraction.Ignore))
                    {
                        transform.position += push;
                    }
                    else
                    {
                        Vector3 slidePush = Vector3.ProjectOnPlane(push, wallHit.normal);
                        slidePush.y = 0f;
                        transform.position += slidePush;
                    }
                }
            }
        }
    }
}
