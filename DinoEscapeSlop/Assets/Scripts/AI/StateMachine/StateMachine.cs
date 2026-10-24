using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    public IState CurrentState { get; private set; }

    private readonly List<IState> allowedStates = new List<IState>();

    public IReadOnlyList<IState> AllowedStates => allowedStates;

    public int CurrentStatePriority => (CurrentState is IPriorityState p) ? p.Priority : 0;

    public void AllowState(IState state)
    {
        if (state != null && !allowedStates.Contains(state))
        {
            allowedStates.Add(state);
        }
    }

    public bool IsStateAllowed(IState state)
    {
        return state != null && allowedStates.Contains(state);
    }

    public void Initialize(IState startingState)
    {
        if (startingState == null) return;

        AllowState(startingState);

        CurrentState = startingState;
        CurrentState.Enter();
    }

    public bool ChangeState(IState newState)
    {
        return ChangeState(newState, false);
    }

    public bool ChangeState(IState newState, bool force)
    {
        if (newState == null || newState == CurrentState) return false;

        // Validación estricta: Solo se puede transicionar a estados previamente autorizados para esta entidad
        if (allowedStates.Count > 0 && !allowedStates.Contains(newState))
        {
            Debug.LogWarning($"[StateMachine] Transición denegada: El estado {newState.GetType().Name} no está permitido en esta entidad.");
            return false;
        }

        // Validación de prioridad: Si el estado actual tiene mayor prioridad que el entrante y la transición no es forzada, se deniega
        int currentPriority = CurrentStatePriority;
        int newPriority = (newState is IPriorityState p) ? p.Priority : 0;

        if (!force && currentPriority > newPriority)
        {
            return false;
        }

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
        return true;
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}
