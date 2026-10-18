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

        // Si no estaba registrado previamente, lo registra automáticamente para no bloquear la transición
        if (!allowedStates.Contains(newState))
        {
            allowedStates.Add(newState);
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