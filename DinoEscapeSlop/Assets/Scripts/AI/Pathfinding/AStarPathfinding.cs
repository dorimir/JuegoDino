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

    /// <summary>
    /// Calcula la ruta A* entre dos posiciones del mundo y devuelve una lista de posiciones Vector3.
    /// Utiliza estructuras locales para permitir que multiples enemigos calculen rutas concurrentemente
    /// sin alterar los componentes Node de la escena.
    /// </summary>
    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Node startNode = GetClosestNode(startPos);
        Node targetNode = GetClosestNode(targetPos);

        if (startNode == null || targetNode == null) return null;

        // Si el enemigo y el objetivo estan en el mismo nodo mas cercano
        if (startNode == targetNode)
        {
            List<Vector3> directPath = new List<Vector3>();
            if (Vector3.Distance(startPos, targetNode.transform.position) > 0.6f)
            {
                directPath.Add(targetNode.transform.position);
            }
            if (Vector3.Distance(targetNode.transform.position, targetPos) > 0.6f)
            {
                directPath.Add(targetPos);
            }
            if (directPath.Count == 0)
            {
                directPath.Add(targetPos);
            }
            return directPath;
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        Dictionary<Node, int> gCosts = new Dictionary<Node, int>();
        Dictionary<Node, int> hCosts = new Dictionary<Node, int>();
        Dictionary<Node, Node> parentMap = new Dictionary<Node, Node>();

        openSet.Add(startNode);
        gCosts[startNode] = 0;
        hCosts[startNode] = GetDistance(startNode, targetNode);

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
                if (path != null && Vector3.Distance(targetNode.transform.position, targetPos) > 0.6f)
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

        return null; // Si no hay camino posible
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

            if (currentNode == startNode)
            {
                break;
            }

            if (parentMap.ContainsKey(currentNode))
            {
                currentNode = parentMap[currentNode];
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
        return Mathf.RoundToInt(Vector3.Distance(nodeA.transform.position, nodeB.transform.position) * 10f);
    }

    /// <summary>
    /// Encuentra el nodo del grafo mas cercano a una posicion del mundo
    /// </summary>
    public Node GetClosestNode(Vector3 worldPos)
    {
        if (cachedNodes == null || cachedNodes.Length == 0)
        {
            CacheNodes();
        }

        if (cachedNodes == null || cachedNodes.Length == 0)
        {
            return null;
        }

        Node closestNode = null;
        float minDistance = Mathf.Infinity;

        foreach (Node node in cachedNodes)
        {
            if (node == null) continue;

            float distance = Vector3.Distance(worldPos, node.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestNode = node;
            }
        }

        return closestNode;
    }
}
