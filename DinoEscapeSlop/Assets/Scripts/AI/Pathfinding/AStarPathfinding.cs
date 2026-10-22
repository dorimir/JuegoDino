using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinding : MonoBehaviour
{
    private Node[] cachedNodes;

    private void Awake()
    {
        CacheNodes();
    }

    public void CacheNodes()
    {
        cachedNodes = Object.FindObjectsByType<Node>(FindObjectsSortMode.None);
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Node startNode = GetClosestNode(startPos);
        Node targetNode = GetClosestNode(targetPos);

        if (startNode == null || targetNode == null) return null;

        if (startNode == targetNode)
        {
            List<Vector3> directPath = new List<Vector3>();
            float dStartSqr = (startPos.x - targetNode.transform.position.x) * (startPos.x - targetNode.transform.position.x) + 
                              (startPos.z - targetNode.transform.position.z) * (startPos.z - targetNode.transform.position.z);
            float dTargetSqr = (targetNode.transform.position.x - targetPos.x) * (targetNode.transform.position.x - targetPos.x) + 
                               (targetNode.transform.position.z - targetPos.z) * (targetNode.transform.position.z - targetPos.z);

            if (dStartSqr > 0.36f) directPath.Add(targetNode.transform.position);
            if (dTargetSqr > 0.36f) directPath.Add(targetPos);
            if (directPath.Count == 0) directPath.Add(targetPos);
            return directPath;
        }

        List<Node> openSet = new List<Node> { startNode };
        HashSet<Node> closedSet = new HashSet<Node>();

        Dictionary<Node, int> gCosts = new Dictionary<Node, int> { [startNode] = 0 };
        Dictionary<Node, int> hCosts = new Dictionary<Node, int> { [startNode] = GetDistance(startNode, targetNode) };
        Dictionary<Node, Node> parentMap = new Dictionary<Node, Node>();

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            int currentF = gCosts[currentNode] + hCosts[currentNode];

            for (int i = 1; i < openSet.Count; i++)
            {
                Node candidate = openSet[i];
                int candidateF = gCosts[candidate] + hCosts[candidate];

                if (candidateF < currentF || (candidateF == currentF && hCosts[candidate] < hCosts[currentNode]))
                {
                    currentNode = candidate;
                    currentF = candidateF;
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                List<Vector3> path = RetracePath(startNode, targetNode, parentMap);
                float dTargetSqr = (targetNode.transform.position.x - targetPos.x) * (targetNode.transform.position.x - targetPos.x) + 
                                   (targetNode.transform.position.z - targetPos.z) * (targetNode.transform.position.z - targetPos.z);
                if (path != null && dTargetSqr > 0.36f)
                {
                    path.Add(targetPos);
                }
                return path;
            }

            foreach (Node neighbor in currentNode.neighbors)
            {
                if (neighbor == null || closedSet.Contains(neighbor)) continue;

                int movementCostToNeighbor = gCosts[currentNode] + GetDistance(currentNode, neighbor);

                if (!gCosts.ContainsKey(neighbor) || movementCostToNeighbor < gCosts[neighbor])
                {
                    gCosts[neighbor] = movementCostToNeighbor;
                    hCosts[neighbor] = GetDistance(neighbor, targetNode);
                    parentMap[neighbor] = currentNode;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return null;
    }

    private List<Vector3> RetracePath(Node startNode, Node endNode, Dictionary<Node, Node> parentMap)
    {
        List<Vector3> path = new List<Vector3>();
        Node currentNode = endNode;
        HashSet<Node> visited = new HashSet<Node>();

        while (currentNode != null && !visited.Contains(currentNode))
        {
            visited.Add(currentNode);
            path.Add(currentNode.transform.position);

            if (currentNode == startNode) break;

            if (parentMap.TryGetValue(currentNode, out Node nextNode))
            {
                currentNode = nextNode;
            }
            else
            {
                break;
            }
        }

        path.Reverse();
        return path;
    }

    private int GetDistance(Node nodeA, Node nodeB)
    {
        float dx = nodeA.transform.position.x - nodeB.transform.position.x;
        float dz = nodeA.transform.position.z - nodeB.transform.position.z;
        return Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dz * dz) * 10f);
    }

    public Node[] GetAllNodes()
    {
        if (cachedNodes == null || cachedNodes.Length == 0)
        {
            CacheNodes();
        }
        return cachedNodes;
    }

    public static bool HasClearLine(Vector3 fromPos, Vector3 toPos, float clearanceRadius = 0.35f)
    {
        float testHeight = 0.75f;
        Vector3 origin = new Vector3(fromPos.x, testHeight, fromPos.z);
        Vector3 target = new Vector3(toPos.x, testHeight, toPos.z);
        Vector3 dir = target - origin;
        float dist = dir.magnitude;

        if (dist <= 0.4f) return true;

        int wallLayerMask = ~((1 << 3) | (1 << 2));

        if (Physics.Linecast(origin, target, out RaycastHit lineHit, wallLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (lineHit.collider != null && !lineHit.collider.CompareTag("Enemy") && lineHit.normal.y < 0.7f)
            {
                return false;
            }
        }

        if (dist > clearanceRadius * 2f)
        {
            Vector3 fwdOrigin = origin + dir.normalized * 0.15f;
            float castDist = Mathf.Max(0.05f, dist - 0.3f);

            RaycastHit[] hits = Physics.SphereCastAll(fwdOrigin, clearanceRadius, dir.normalized, castDist, wallLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.CompareTag("Enemy") || hit.normal.y >= 0.7f) continue;
                return false;
            }
        }

        return true;
    }

    public List<Node> GetReachableNodes(Vector3 worldPos)
    {
        List<Node> reachable = new List<Node>();
        if (cachedNodes == null || cachedNodes.Length == 0)
        {
            CacheNodes();
        }

        if (cachedNodes == null) return reachable;

        foreach (Node node in cachedNodes)
        {
            if (node != null && HasClearLine(worldPos, node.transform.position))
            {
                reachable.Add(node);
            }
        }

        return reachable;
    }

    public Node GetClosestNode(Vector3 worldPos)
    {
        if (cachedNodes == null || cachedNodes.Length == 0)
        {
            CacheNodes();
        }

        if (cachedNodes == null || cachedNodes.Length == 0) return null;

        Node closestReachable = null;
        float minReachableDistSqr = Mathf.Infinity;
        Node closestFallback = null;
        float minFallbackDistSqr = Mathf.Infinity;

        foreach (Node node in cachedNodes)
        {
            if (node == null) continue;

            float dx = worldPos.x - node.transform.position.x;
            float dz = worldPos.z - node.transform.position.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < minFallbackDistSqr)
            {
                minFallbackDistSqr = distSqr;
                closestFallback = node;
            }

            if (HasClearLine(worldPos, node.transform.position) && distSqr < minReachableDistSqr)
            {
                minReachableDistSqr = distSqr;
                closestReachable = node;
            }
        }

        return closestReachable != null ? closestReachable : closestFallback;
    }

    public Node GetFarthestNodeFrom(Node referenceNode)
    {
        if (referenceNode == null) return null;
        Node[] allNodes = GetAllNodes();
        if (allNodes == null || allNodes.Length == 0) return null;

        Node farthestNode = null;
        float maxDistSqr = -1f;
        Vector3 refPos = referenceNode.transform.position;

        foreach (Node node in allNodes)
        {
            if (node == null || node == referenceNode) continue;
            float dx = node.transform.position.x - refPos.x;
            float dz = node.transform.position.z - refPos.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr > maxDistSqr)
            {
                maxDistSqr = distSqr;
                farthestNode = node;
            }
        }

        return farthestNode;
    }

    public Node GetFarthestNodeFromPosition(Vector3 threatPos)
    {
        Node closestToThreat = GetClosestNode(threatPos);
        return closestToThreat != null ? GetFarthestNodeFrom(closestToThreat) : null;
    }
}
