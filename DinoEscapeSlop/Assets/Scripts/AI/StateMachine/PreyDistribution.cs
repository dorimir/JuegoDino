using System.Collections.Generic;
using UnityEngine;

public static class PreyDistribution
{
    public static void DistributePreyAcrossMap(List<EnemyAI> preyList)
    {
        if (preyList == null || preyList.Count == 0) return;

        AStarPathfinding pf = Object.FindAnyObjectByType<AStarPathfinding>();
        if (pf == null) return;

        Node[] allNodes = pf.GetAllNodes();
        if (allNodes == null || allNodes.Length == 0) return;

        List<Node> validNodes = new List<Node>();
        for (int i = 0; i < allNodes.Length; i++)
        {
            if (allNodes[i] != null) validNodes.Add(allNodes[i]);
        }
        if (validNodes.Count == 0) return;

        int preyCount = preyList.Count;
        List<Node> chosenNodes = new List<Node> { validNodes[0] };

        while (chosenNodes.Count < preyCount && chosenNodes.Count < validNodes.Count)
        {
            Node bestCandidate = null;
            float maxMinDist = -1f;

            for (int i = 0; i < validNodes.Count; i++)
            {
                Node candidate = validNodes[i];
                if (chosenNodes.Contains(candidate)) continue;

                float minDistToChosen = float.MaxValue;
                for (int c = 0; c < chosenNodes.Count; c++)
                {
                    float d = Vector2.Distance(
                        new Vector2(candidate.transform.position.x, candidate.transform.position.z),
                        new Vector2(chosenNodes[c].transform.position.x, chosenNodes[c].transform.position.z)
                    );
                    if (d < minDistToChosen) minDistToChosen = d;
                }

                if (minDistToChosen > maxMinDist)
                {
                    maxMinDist = minDistToChosen;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate != null) chosenNodes.Add(bestCandidate);
            else break;
        }

        for (int i = 0; i < preyCount; i++)
        {
            EnemyAI prey = preyList[i];
            if (prey == null) continue;

            Node targetNode = chosenNodes[i % chosenNodes.Count];
            Vector3 spawnPos = targetNode.transform.position;
            spawnPos.y = prey.transform.position.y;
            prey.transform.position = spawnPos;

            if (targetNode.neighbors != null && targetNode.neighbors.Count > 0 && targetNode.neighbors[0] != null)
            {
                Vector3 fwd = targetNode.neighbors[0].transform.position - prey.transform.position;
                fwd.y = 0;
                if (fwd.sqrMagnitude > 0.001f)
                {
                    prey.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                }
            }

            if (prey.Movement != null)
            {
                prey.Movement.patrolNodes.Clear();
                prey.Movement.ClearPath();
                prey.Movement.ResetVelocity();
                prey.Movement.SetCurrentDestinationNode(targetNode);

                Node nextPatrol = prey.Movement.GetNextNetworkPatrolNode();
                if (nextPatrol != null)
                {
                    prey.Movement.MoveTo(nextPatrol.transform.position);
                }
            }
        }
    }
}
