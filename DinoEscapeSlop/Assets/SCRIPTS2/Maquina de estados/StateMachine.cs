using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    public IState CurrentState { get; private set; }

    private List<IState> allowedStates = new List<IState>();

    public void AllowState(IState state)
    {
        if (state != null && !allowedStates.Contains(state))
        {
            allowedStates.Add(state);
        }
    }

    public void Initialize(IState startingState)
    {
        if (startingState == null) return;

        AllowState(startingState);

        CurrentState = startingState;
        CurrentState.Enter();
    }

    public void ChangeState(IState newState)
    {
        if (newState == null || newState == CurrentState) return;

        if (!allowedStates.Contains(newState))
        {
            Debug.LogWarning($"[StateMachine] Transição bloqueada: O estado '{newState.GetType().Name}' não está registrado na lista de estados permitidos.");
            return;
        }

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}