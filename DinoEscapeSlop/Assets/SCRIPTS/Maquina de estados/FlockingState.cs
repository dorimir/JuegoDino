using System.Collections.Generic;
using UnityEngine;

public class FlockingState : IState
{
    private EnemyAI ai;

    public FlockingState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"{ai.gameObject.name} entra en FLOCKING (¿Es Líder?: {ai.isLeader})");

        if (ai.Movement == null)
        {
            Debug.LogError("Falta EnemyMovement en " + ai.gameObject.name);
            return;
        }

        if (ai.Movement.HasReachedDestination)
            EvaluateAndMove();
    }

    public void Update()
    {
        if (ai == null || ai.StateMachine == null || ai.Movement == null)
            return;

        if (ai.CanSeePlayer())
        {
            ai.StateMachine.ChangeState(
                ai.IsPrey ? (IState)ai.FleeState : (IState)ai.ChaseState
            );
            return;
        }

        if (ai.Movement.HasReachedDestination)
            EvaluateAndMove();
    }

    public void Exit() { }

    private void EvaluateAndMove()
    {
        if (ai == null || ai.Movement == null)
            return;

        if (ai.isLeader)
        {
            ai.Movement.GoToRandomNode();
            return;
        }

        Node nextNode = ChooseFollowerNode();

        if (nextNode != null)
            ai.Movement.MoveTo(nextNode.transform.position);
        else
            ai.Movement.GoToRandomNode();
    }

    private Node ChooseFollowerNode()
    {
        if (ai.Pathfinding == null || ai.leaderTransform == null)
            return null;

        Node currentNode = ai.Pathfinding.GetClosestNode(ai.transform.position);

        if (currentNode == null || currentNode.neighbors == null ||
            currentNode.neighbors.Count == 0)
            return null;

        List<Transform> nearbyAllies =
            GetNearbyAlliesByTag("Enemy", ai.FlockRadius);

        Node bestNode = null;
        float highestScore = float.MinValue;

        foreach (Node neighborNode in currentNode.neighbors)
        {
            if (neighborNode == null) continue;

            float score = 0f;

            float distToLeader = Vector3.Distance(
                neighborNode.transform.position,
                ai.leaderTransform.position
            );

            score -= distToLeader * ai.LeaderFollowWeight;

            foreach (Transform ally in nearbyAllies)
            {
                if (ally == ai.leaderTransform) continue;

                float distToAlly = Vector3.Distance(
                    neighborNode.transform.position,
                    ally.position
                );

                if (distToAlly < ai.TooCloseRadius)
                    score -= (ai.TooCloseRadius - distToAlly)
                             * ai.SeparationWeight;
            }

            if (score > highestScore)
            {
                highestScore = score;
                bestNode = neighborNode;
            }
        }

        return bestNode;
    }

    private List<Transform> GetNearbyAlliesByTag(string tag, float radius)
    {
        List<Transform> alliesFound = new List<Transform>();

        Collider[] hitColliders = Physics.OverlapSphere(
            ai.transform.position,
            radius
        );

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider != null &&
                hitCollider.CompareTag(tag) &&
                hitCollider.gameObject != ai.gameObject)
            {
                alliesFound.Add(hitCollider.transform);
            }
        }

        return alliesFound;
    }
}