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
        List<EnemyAI> leaders = new List<EnemyAI>();
        List<EnemyAI> followers = new List<EnemyAI>();

        for (int i = 0; i < preyCount; i++)
        {
            EnemyAI prey = preyList[i];
            if (prey == null) continue;

            if (prey.LeaderAI != null && prey.LeaderAI != prey)
            {
                followers.Add(prey);
            }
            else
            {
                leaders.Add(prey);
            }
        }

        if (leaders.Count == 0)
        {
            leaders.AddRange(followers);
            followers.Clear();
        }

        int leaderCount = leaders.Count;
        List<Node> chosenNodes = new List<Node> { validNodes[0] };

        while (chosenNodes.Count < leaderCount && chosenNodes.Count < validNodes.Count)
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

        for (int i = 0; i < leaderCount; i++)
        {
            EnemyAI leader = leaders[i];
            if (leader == null) continue;

            Node targetNode = chosenNodes[i % chosenNodes.Count];
            Vector3 spawnPos = targetNode.transform.position;
            leader.transform.position = spawnPos;

            if (leader.Movement != null)
            {
                leader.Movement.SnapToGround(true);
            }

            if (targetNode.neighbors != null && targetNode.neighbors.Count > 0 && targetNode.neighbors[0] != null)
            {
                Vector3 fwd = targetNode.neighbors[0].transform.position - leader.transform.position;
                fwd.y = 0;
                if (fwd.sqrMagnitude > 0.001f)
                {
                    leader.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                }
            }

            if (leader.Movement != null)
            {
                leader.Movement.patrolNodes.Clear();
                leader.Movement.ClearPath();
                leader.Movement.ResetVelocity();
                leader.Movement.SetCurrentDestinationNode(targetNode);

                Node nextPatrol = leader.Movement.GetNextNetworkPatrolNode();
                if (nextPatrol != null)
                {
                    leader.Movement.MoveTo(nextPatrol.transform.position);
                }
            }
        }

        for (int i = 0; i < followers.Count; i++)
        {
            EnemyAI follower = followers[i];
            if (follower == null || follower.LeaderAI == null) continue;

            EnemyAI leader = follower.LeaderAI;
            int slotIndex = leader.packFollowers.IndexOf(follower);
            if (slotIndex < 0) slotIndex = i;

            int row = (slotIndex / 2) + 1;
            float side = (slotIndex % 2 == 0) ? -1f : 1f;

            Vector3 leaderPos = leader.transform.position;
            Vector3 leaderFwd = leader.transform.forward;
            Vector3 leaderRight = leader.transform.right;

            Vector3 spawnPos = leaderPos - (leaderFwd * (1.6f * row)) + (leaderRight * (side * 0.9f));
            follower.transform.position = spawnPos;
            follower.transform.rotation = leader.transform.rotation;

            if (follower.Movement != null)
            {
                follower.Movement.SnapToGround(true);
                follower.Movement.patrolNodes.Clear();
                follower.Movement.ClearPath();
                follower.Movement.ResetVelocity();
            }
        }
    }
}
