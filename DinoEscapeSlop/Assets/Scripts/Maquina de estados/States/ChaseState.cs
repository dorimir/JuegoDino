using UnityEngine;

public class ChaseState : IState
{
    private EnemyAI ai;
    private float repathTimer;
    private const float RepathInterval = 0.25f;

    public ChaseState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public void Enter()
    {
        Debug.Log($"{ai.gameObject.name} ENTRA EN CHASE");
        ai.IsChasing = true;
        repathTimer = 0f;

        Renderer rend = ai.GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = Color.red;
    }

    public void Exit()
    {
        Debug.Log($"{ai.gameObject.name} SALE DE CHASE");
        ai.IsChasing = false;

        Renderer rend = ai.GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = Color.white;
    }

    public void Update()
    {
        if (ai.Player == null || ai.Movement == null)
        {
            if (ai.Movement == null)
                Debug.LogError("Falta EnemyMovement en " + ai.gameObject.name);

            ai.StateMachine.ChangeState(ai.FlockingState);
            return;
        }

        float distance = Vector3.Distance(
            ai.transform.position,
            ai.Player.position
        );

        if (distance > ai.LoseRange)
        {
            ai.LastLostTime = Time.time;
            ai.StateMachine.ChangeState(ai.FlockingState);
            return;
        }

        repathTimer -= Time.deltaTime;

        if (repathTimer <= 0f)
        {
            ai.Movement.MoveTo(ai.Player.position);
            repathTimer = RepathInterval;
        }
    }
}